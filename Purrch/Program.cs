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

        // Checks GitHub Releases and, if there's a newer version, points the tray
        // menu at the download. It never fetches or runs an exe itself.
        private async Task CheckForUpdatesAsync(bool manual = false)
        {
            var info = await Updater.CheckAsync();
            void ShowResult()
            {
                if (info.Available)
                {
                    updateUrl = info.PageUrl;
                    if (updateItem != null) updateItem.Text = $"Get update {info.Version} →";
                    tray.ShowBalloonTip(6000, "Purrch update available",
                        $"{info.Version} is ready — open the tray menu to get it.", ToolTipIcon.Info);
                }
                else if (manual)
                {
                    tray.ShowBalloonTip(4000, "Purrch", "You're on the latest version.", ToolTipIcon.Info);
                }
            }
            if (form.IsHandleCreated) form.BeginInvoke((Action)ShowResult);
            else ShowResult();
        }

        private void OnUpdateClicked()
        {
            if (!string.IsNullOrEmpty(updateUrl))
            {
                try { Process.Start(new ProcessStartInfo(updateUrl) { UseShellExecute = true }); }
                catch { /* ignore */ }
            }
            else
            {
                _ = CheckForUpdatesAsync(manual: true);
            }
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
