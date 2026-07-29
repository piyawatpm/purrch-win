using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Purrch
{
    /// "Make it look like my pet": pick a photo, an image model (Gemini, BYO key)
    /// redraws it as the idle sprite, and its coat colours are pulled onto the
    /// rest of the rig. A free mock render runs when no key is set.
    public class PortraitForm : Form
    {
        private readonly AppSettings settings;
        private readonly SpriteLibrary lib;
        private readonly Action onApply;

        private Bitmap source;
        private PortraitResult result;
        private string species;

        private readonly TextBox keyBox;
        private readonly ComboBox speciesBox;
        private readonly PictureBox preview;
        private readonly Label status;
        private readonly Button generateBtn, applyBtn;

        public PortraitForm(AppSettings settings, SpriteLibrary lib, Action onApply)
        {
            this.settings = settings; this.lib = lib; this.onApply = onApply;
            species = settings.Species;

            Text = "Purrch — Make it look like my pet";
            ClientSize = new Size(430, 480);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false; MaximizeBox = false;
            Font = new Font("Segoe UI", 9.5f);

            var intro = new Label
            {
                Text = "Upload a photo and an image model redraws your pet as the companion. "
                     + "It becomes the resting look; movement uses the rig in your pet's colours.",
                Dock = DockStyle.Fill, ForeColor = Color.DimGray, Padding = new Padding(2, 4, 2, 0),
            };

            var chooseBtn = new Button { Text = "Choose photo…", AutoSize = true };
            chooseBtn.Click += (s, e) => ChoosePhoto();
            speciesBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
            speciesBox.Items.AddRange(new[] { "Cat", "Dog" });
            speciesBox.SelectedIndex = species == "dog" ? 1 : 0;
            speciesBox.SelectedIndexChanged += (s, e) => species = speciesBox.SelectedIndex == 1 ? "dog" : "cat";
            var top = new FlowLayoutPanel { Dock = DockStyle.Fill };
            top.Controls.Add(chooseBtn);
            top.Controls.Add(new Label { Text = "Species", AutoSize = true, Padding = new Padding(14, 6, 4, 0) });
            top.Controls.Add(speciesBox);

            keyBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
            keyBox.PlaceholderText = "Gemini API key — leave empty for a free test render";
            keyBox.Text = KeyStore.Load();

            preview = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(245, 245, 247), BorderStyle = BorderStyle.FixedSingle };
            preview.Paint += PreviewPaint;

            generateBtn = new Button { Text = "Generate", AutoSize = true };
            generateBtn.Click += async (s, e) => await Generate();
            applyBtn = new Button { Text = "Apply", AutoSize = true, Enabled = false };
            applyBtn.Click += (s, e) => ApplyResult();
            var revertBtn = new Button { Text = "Revert to default", AutoSize = true };
            revertBtn.Click += (s, e) => { PortraitStore.Revert(settings, lib); onApply?.Invoke(); status.Text = "Reverted to the default look."; };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill };
            actions.Controls.Add(generateBtn);
            actions.Controls.Add(applyBtn);
            actions.Controls.Add(revertBtn);

            status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray };

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(12) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.Controls.Add(intro, 0, 0);
            layout.Controls.Add(top, 0, 1);
            layout.Controls.Add(keyBox, 0, 2);
            layout.Controls.Add(preview, 0, 3);
            layout.Controls.Add(actions, 0, 4);
            layout.Controls.Add(status, 0, 5);
            Controls.Add(layout);
        }

        private void ChoosePhoto()
        {
            using var dlg = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                using var f = new Bitmap(dlg.FileName);
                source?.Dispose();
                source = new Bitmap(f);
                result = null;
                applyBtn.Enabled = false;
                status.Text = "Photo loaded.";
                preview.Invalidate();
            }
            catch { status.Text = "Couldn't open that image."; }
        }

        private async Task Generate()
        {
            if (source == null) { status.Text = "Choose a photo first."; return; }
            generateBtn.Enabled = false;
            status.Text = "Rendering…";
            string key = keyBox.Text.Trim();
            KeyStore.Save(key);

            var template = lib.IdleTemplate(species);
            if (template == null) { status.Text = "Couldn't load the pose template."; generateBtn.Enabled = true; return; }
            var photo = FitPhoto(source, 1024);
            string sp = species;
            try
            {
                Bitmap raw = key.Length == 0
                    ? GeminiProvider.MockRender(template)
                    : await GeminiProvider.RenderAsync(key, photo, template, sp);
                var res = await Task.Run(() => PortraitPipeline.Process(raw, sp, lib.FW, lib.FH, lib.Manifest.ground));
                raw.Dispose();
                result?.Idle?.Dispose();
                result = res;
                applyBtn.Enabled = true;
                status.Text = key.Length == 0 ? "Test render (no key used)." : "Done — preview below.";
                preview.Invalidate();
            }
            catch (Exception ex) { status.Text = ex.Message; }
            finally { generateBtn.Enabled = true; template.Dispose(); photo.Dispose(); }
        }

        private void ApplyResult()
        {
            if (result == null) return;
            PortraitStore.Apply(result, settings, lib);
            onApply?.Invoke();
            status.Text = "Applied ✓ your companion now looks like your pet.";
        }

        private void PreviewPaint(object sender, PaintEventArgs e)
        {
            var img = result?.Idle ?? source;
            if (img == null) return;
            var pb = preview.ClientSize;
            bool sprite = result?.Idle != null;
            e.Graphics.InterpolationMode = sprite ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            double sc = Math.Min((double)pb.Width / img.Width, (double)pb.Height / img.Height);
            if (sprite) sc = Math.Min(sc, 8);
            int w = (int)(img.Width * sc), h = (int)(img.Height * sc);
            e.Graphics.DrawImage(img, (pb.Width - w) / 2, (pb.Height - h) / 2, w, h);
        }

        private static Bitmap FitPhoto(Bitmap img, int maxSide)
        {
            int m = Math.Max(img.Width, img.Height);
            if (m <= maxSide) return new Bitmap(img);
            double s = (double)maxSide / m;
            var b = new Bitmap(Math.Max(1, (int)(img.Width * s)), Math.Max(1, (int)(img.Height * s)), PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(img, 0, 0, b.Width, b.Height); }
            return b;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            else base.OnFormClosing(e);
        }
    }
}
