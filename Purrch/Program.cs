using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Purrch
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();   // visual styles + PerMonitorV2 DPI (from the csproj)
            Application.Run(new PetContext());
        }
    }

    /// Owns the pet window, tray, clock, brain, and sprite library.
    class PetContext : ApplicationContext
    {
        private readonly SpriteLibrary lib = new();
        private readonly PetForm form = new();
        private readonly Brain brain;
        private readonly System.Windows.Forms.Timer timer = new();
        private readonly NotifyIcon tray = new();
        private readonly AppSettings settings = AppSettings.Load();
        private readonly Sounds sounds;

        private DateTime last = DateTime.UtcNow;
        private Bitmap backbuffer;
        private int bufScale = -1;

        private bool pressing, dragging;
        private Point downPos;

        private ToolStripMenuItem updateItem;
        private string updateUrl;

        private readonly PetForm bowlForm = new();
        private bool bowlShown;
        private bool playedCrunch;
        private TasksForm tasksForm;

        private readonly PetForm bubbleForm = new();
        private bool bubbleShown;
        private string bubbleCachedText;
        private Bitmap bubbleBmp;

        private readonly PetForm toyForm = new();
        private bool toyShown;
        private int toyFrame;
        private double toyFrameT;

        public PetContext()
        {
            brain = new Brain(lib.FW, lib.FH, lib.Manifest.ground)
            {
                Species = settings.Species,
                Scale = Math.Max(2, Math.Min(4, settings.Scale)),
            };
            brain.AnimMs = a => lib.Ms(a);
            brain.AnimFrames = a => lib.FrameCount(a);
            sounds = new Sounds(lib) { Enabled = settings.Sound };
            brain.OnChatter = () => sounds.Play(brain.Species == "dog" ? "bark" : "meow");

            TaskStore.Shared.PruneHistory();
            TaskStore.Shared.TaskCompleted += () => brain.Feed();
            TaskStore.Shared.AllDoneToday += () =>
            {
                brain.Celebrate();
                sounds.Play(brain.Species == "dog" ? "bark" : "meow");
                tray.ShowBalloonTip(4000, "Purrch", "All done for today ♥", ToolTipIcon.Info);
            };

            settings.SyncStartup();

            form.Show();
            WireMouse();
            BuildTray();

            timer.Interval = 33;   // ~30 fps
            timer.Tick += (s, e) => Tick();
            timer.Start();

            SystemEvents.DisplaySettingsChanged += (s, e) => brain.UpdateScreen();

            _ = CheckForUpdatesAsync();   // quietly look for a newer release at startup
        }

        private void Tick()
        {
            var now = DateTime.UtcNow;
            double dt = Math.Min(0.1, (now - last).TotalSeconds);
            last = now;
            brain.Update(dt);
            Render();
            RenderBowl();
            RenderBubble();
            RenderToy(dt);

            if (brain.State == PetState.Eat && !playedCrunch) { playedCrunch = true; sounds.Play("crunch"); }
            else if (brain.State != PetState.Eat) playedCrunch = false;
        }

        private void Render()
        {
            int w = brain.SpriteW, h = brain.SpriteH;
            if (backbuffer == null || bufScale != brain.Scale)
            {
                backbuffer?.Dispose();
                backbuffer = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                bufScale = brain.Scale;
            }

            var frames = lib.Frames(brain.Species, brain.CurrentAnim, brain.Dir < 0);
            var frame = frames[Math.Min(brain.Frame, frames.Length - 1)];

            using (var g = Graphics.FromImage(backbuffer))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.Clear(Color.Transparent);
                g.CompositingMode = CompositingMode.SourceOver;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(frame, new Rectangle(0, 0, w, h), new Rectangle(0, 0, lib.FW, lib.FH), GraphicsUnit.Pixel);
            }
            form.SetBitmap(backbuffer, brain.WindowTopLeft());
        }

        // The food bowl is a second layered window shown only while there's a meal.
        private void RenderBowl()
        {
            if (brain.BowlX == null)
            {
                if (bowlShown) { bowlForm.Visible = false; bowlShown = false; }
                return;
            }
            var frames = lib.Bowl(brain.BowlKind);
            if (frames.Length < 2) return;
            var img = brain.BowlFull ? frames[0] : frames[1];
            int scale = brain.Scale;
            int w = img.Width * scale, h = img.Height * scale;
            using var buf = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(buf))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.Clear(Color.Transparent);
                g.CompositingMode = CompositingMode.SourceOver;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(img, new Rectangle(0, 0, w, h), new Rectangle(0, 0, img.Width, img.Height), GraphicsUnit.Pixel);
            }
            int left = (int)Math.Round(brain.BowlX.Value - w / 2.0);
            int top = (int)Math.Round(brain.BowlY - h);
            if (!bowlShown) { bowlForm.Show(); bowlShown = true; }
            bowlForm.SetBitmap(buf, new Point(left, top));
        }

        private void OpenTasks()
        {
            if (tasksForm == null || tasksForm.IsDisposed) tasksForm = new TasksForm();
            tasksForm.Show();
            tasksForm.WindowState = FormWindowState.Normal;
            tasksForm.BringToFront();
            tasksForm.Activate();
        }

        // A speech bubble drawn above the pet's head while it has something to say.
        private void RenderBubble()
        {
            if (string.IsNullOrEmpty(brain.BubbleText))
            {
                if (bubbleShown) { bubbleForm.Visible = false; bubbleShown = false; }
                return;
            }
            if (brain.BubbleText != bubbleCachedText)
            {
                bubbleBmp?.Dispose();
                bubbleBmp = BuildBubble(brain.BubbleText);
                bubbleCachedText = brain.BubbleText;
            }
            int petTop = brain.WindowTopLeft().Y;
            int left = (int)Math.Round(brain.X - bubbleBmp.Width / 2.0);
            int top = petTop - bubbleBmp.Height - 2;
            if (!bubbleShown) { bubbleForm.Show(); bubbleShown = true; }
            bubbleForm.SetBitmap(bubbleBmp, new Point(left, top));
        }

        private static Bitmap BuildBubble(string text)
        {
            using var font = new Font("Segoe UI", 10f, FontStyle.Bold);
            SizeF sz;
            using (var probe = new Bitmap(1, 1))
            using (var g0 = Graphics.FromImage(probe))
                sz = g0.MeasureString(text, font);

            int padX = 12, padY = 7, tail = 6;
            int w = (int)Math.Ceiling(sz.Width) + padX * 2;
            int h = (int)Math.Ceiling(sz.Height) + padY * 2 + tail;
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                var body = new Rectangle(0, 0, w - 1, h - tail - 1);
                using (var path = RoundedRect(body, 8))
                using (var fill = new SolidBrush(Color.FromArgb(240, 255, 255, 255)))
                using (var pen = new Pen(Color.FromArgb(200, 90, 90, 90)))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(pen, path);
                    var tri = new[]
                    {
                        new Point(w / 2 - 6, h - tail - 1),
                        new Point(w / 2 + 6, h - tail - 1),
                        new Point(w / 2, h - 1),
                    };
                    g.FillPolygon(fill, tri);
                }
                using var ink = new SolidBrush(Color.FromArgb(40, 40, 45));
                g.DrawString(text, font, ink, padX, padY);
            }
            return bmp;
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // The toy is a small layered window the pet chases; the mouse animates as it runs.
        private void RenderToy(double dt)
        {
            if (brain.ToyX == null)
            {
                if (toyShown) { toyForm.Visible = false; toyShown = false; }
                return;
            }
            var frames = lib.Toy(brain.ToyKind);
            if (frames.Length < 2) return;
            toyFrameT += dt * 1000;
            if (toyFrameT >= 110) { toyFrameT -= 110; toyFrame = (toyFrame + 1) % 2; }
            var img = frames[brain.ToyRunning ? toyFrame : 0];

            int scale = brain.Scale;
            int w = img.Width * scale, h = img.Height * scale;
            using var buf = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(buf))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.Clear(Color.Transparent);
                g.CompositingMode = CompositingMode.SourceOver;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                if (!brain.ToyFacingRight) { g.TranslateTransform(w, 0); g.ScaleTransform(-1, 1); }
                g.DrawImage(img, new Rectangle(0, 0, w, h), new Rectangle(0, 0, img.Width, img.Height), GraphicsUnit.Pixel);
            }
            int left = (int)Math.Round(brain.ToyX.Value - w / 2.0);
            int top = (int)Math.Round(brain.ToyY - h);
            if (!toyShown) { toyForm.Show(); toyShown = true; }
            toyForm.SetBitmap(buf, new Point(left, top));
        }

        private void DropToy(string kind) => brain.PlaceToy(kind);

        // A short press pokes the pet; dragging past a few pixels picks it up, and
        // releasing drops it to fall to the floor.
        private void WireMouse()
        {
            form.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                pressing = true; dragging = false;
                downPos = Control.MousePosition;
                form.Capture = true;
            };
            form.MouseMove += (s, e) =>
            {
                if (!pressing) return;
                var p = Control.MousePosition;
                if (!dragging && Math.Abs(p.X - downPos.X) + Math.Abs(p.Y - downPos.Y) > 6)
                {
                    dragging = true;
                    brain.Grab(p.X, p.Y);
                }
                if (dragging) brain.MoveTo(p.X, p.Y);
            };
            form.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtons.Left || !pressing) return;
                pressing = false;
                form.Capture = false;
                if (dragging)
                {
                    brain.Release();
                }
                else
                {
                    brain.Poke();
                    sounds.Play(brain.Species == "dog" ? "bark" : "meow");
                }
                dragging = false;
            };
        }

        private void BuildTray()
        {
            tray.Icon = LoadTrayIcon();
            tray.Text = "Purrch";
            tray.Visible = true;

            var menu = new ContextMenuStrip();

            var cat = new ToolStripMenuItem("Cat", null, (s, e) => SetSpecies("cat"));
            var dog = new ToolStripMenuItem("Dog", null, (s, e) => SetSpecies("dog"));
            menu.Items.Add(cat);
            menu.Items.Add(dog);
            menu.Items.Add(new ToolStripSeparator());

            var sizes = new ToolStripMenuItem("Size");
            foreach (var pair in new[] { ("Small", 2), ("Medium", 3), ("Large", 4) })
            {
                int val = pair.Item2;
                sizes.DropDownItems.Add(new ToolStripMenuItem(pair.Item1, null, (s, e) => SetScale(val)));
            }
            menu.Items.Add(sizes);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Tasks…", null, (s, e) => OpenTasks()));

            var toys = new ToolStripMenuItem("Drop a toy");
            toys.DropDownItems.Add(new ToolStripMenuItem("Mouse", null, (s, e) => DropToy("mouse")));
            toys.DropDownItems.Add(new ToolStripMenuItem("Ball", null, (s, e) => DropToy("ball")));
            toys.DropDownItems.Add(new ToolStripMenuItem("Feather", null, (s, e) => DropToy("feather")));
            menu.Items.Add(toys);

            var sound = new ToolStripMenuItem("Sound", null, (s, e) => ToggleSound());
            menu.Items.Add(sound);

            var launch = new ToolStripMenuItem("Launch at login", null, (s, e) => ToggleLaunch());
            menu.Items.Add(launch);
            menu.Items.Add(new ToolStripSeparator());

            updateItem = new ToolStripMenuItem("Check for updates…", null, (s, e) => OnUpdateClicked());
            menu.Items.Add(updateItem);
            menu.Items.Add(new ToolStripMenuItem("Quit Purrch", null, (s, e) => Quit()));

            menu.Opening += (s, e) =>
            {
                cat.Checked = brain.Species == "cat";
                dog.Checked = brain.Species == "dog";
                foreach (ToolStripMenuItem it in sizes.DropDownItems)
                    it.Checked = (it.Text == "Small" && brain.Scale == 2)
                              || (it.Text == "Medium" && brain.Scale == 3)
                              || (it.Text == "Large" && brain.Scale == 4);
                sound.Checked = settings.Sound;
                launch.Checked = settings.LaunchAtLogin;
            };

            tray.ContextMenuStrip = menu;
        }

        private Icon LoadTrayIcon()
        {
            try
            {
                using var s = lib.ResourceStream("tray.png");
                if (s != null)
                    using (var bmp = new Bitmap(s))
                        return Icon.FromHandle(bmp.GetHicon());
            }
            catch { /* fall back below */ }
            return SystemIcons.Application;
        }

        private void SetSpecies(string sp) { brain.Species = sp; settings.Species = sp; settings.Save(); }

        private void SetScale(int sc) { brain.Scale = sc; settings.Scale = sc; settings.Save(); }

        private void ToggleLaunch()
        {
            settings.LaunchAtLogin = !settings.LaunchAtLogin;
            settings.SyncStartup();
            settings.Save();
        }

        private void ToggleSound()
        {
            settings.Sound = !settings.Sound;
            sounds.Enabled = settings.Sound;
            settings.Save();
        }

        // Checks GitHub Releases for a newer version. A manual scan (the tray
        // button) reports the result in a clear dialog; the quiet startup check
        // just updates the tray. It never downloads or runs an exe itself.
        private async Task CheckForUpdatesAsync(bool manual = false)
        {
            if (manual && updateItem != null) updateItem.Text = "Checking…";
            var info = await Updater.CheckAsync();

            void ShowResult()
            {
                if (info.Available)
                {
                    updateUrl = info.PageUrl;
                    if (updateItem != null) updateItem.Text = $"Get update {info.Version} →";
                    if (manual)
                    {
                        var r = MessageBox.Show(
                            $"Purrch {info.Version} is available.\nYou have v{CurrentVersion()}.\n\nOpen the download page?",
                            "Purrch — update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        if (r == DialogResult.Yes) OpenUrl(updateUrl);
                    }
                    else
                    {
                        tray.ShowBalloonTip(6000, "Purrch update available",
                            $"{info.Version} is ready — open the tray menu to get it.", ToolTipIcon.Info);
                    }
                }
                else
                {
                    if (updateItem != null) updateItem.Text = "Check for updates…";
                    if (manual)
                        MessageBox.Show($"You're on the latest version (v{CurrentVersion()}).",
                            "Purrch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }

            if (form.IsHandleCreated) form.BeginInvoke((Action)ShowResult);
            else ShowResult();
        }

        // Tray "Check for updates" click: if startup already found one, go straight
        // to the download; otherwise scan now and report the result.
        private void OnUpdateClicked()
        {
            if (!string.IsNullOrEmpty(updateUrl)) OpenUrl(updateUrl);
            else _ = CheckForUpdatesAsync(manual: true);
        }

        private static string CurrentVersion()
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "?" : $"{v.Major}.{v.Minor}.{v.Build}";
        }

        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { /* ignore */ }
        }

        private void Quit()
        {
            timer.Stop();
            tray.Visible = false;
            tray.Dispose();
            form.Close();
            ExitThread();
        }
    }
}
