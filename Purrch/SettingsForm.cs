using System;
using System.Drawing;
using System.Windows.Forms;

namespace Purrch
{
    /// Settings: species, collar style, follow/roam mode, and eye/ear/collar/bell
    /// colours. Everything applies live and persists. Closing hides the window.
    public class SettingsForm : Form
    {
        private static readonly string[] Styles = { "none", "band", "bell", "bowtie", "bandana" };
        private static readonly string[] StyleLabels = { "None", "Band", "Bell", "Bow tie", "Bandana" };

        public SettingsForm(AppSettings settings, Action apply)
        {
            Text = "Purrch — Settings";
            ClientSize = new Size(340, 348);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 9.5f);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int row = 0;
            void AddRow(string label, Control c)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
                layout.Controls.Add(Caption(label), 0, row);
                layout.Controls.Add(c, 1, row);
                row++;
            }

            var species = Combo(new[] { "Cat", "Dog" }, settings.Species == "dog" ? 1 : 0);
            species.SelectedIndexChanged += (s, e) => { settings.Species = species.SelectedIndex == 1 ? "dog" : "cat"; settings.Save(); apply(); };
            AddRow("Species", species);

            var collar = Combo(StyleLabels, Math.Max(0, Array.IndexOf(Styles, settings.Style)));
            collar.SelectedIndexChanged += (s, e) => { settings.Style = Styles[collar.SelectedIndex]; settings.Save(); apply(); };
            AddRow("Collar", collar);

            var mode = Combo(new[] { "Free roam", "Follow cursor" }, settings.Mode == "follow" ? 1 : 0);
            mode.SelectedIndexChanged += (s, e) => { settings.Mode = mode.SelectedIndex == 1 ? "follow" : "roam"; settings.Save(); apply(); };
            AddRow("Mode", mode);

            AddRow("Eyes", Swatch(() => settings.EyeHex, v => { settings.EyeHex = v; settings.Save(); apply(); }));
            AddRow("Inner ears", Swatch(() => settings.EarHex, v => { settings.EarHex = v; settings.Save(); apply(); }));
            AddRow("Collar colour", Swatch(() => settings.CollarHex, v => { settings.CollarHex = v; settings.Save(); apply(); }));
            AddRow("Bell colour", Swatch(() => settings.BellHex, v => { settings.BellHex = v; settings.Save(); apply(); }));

            Controls.Add(layout);
        }

        private static Label Caption(string text) =>
            new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

        private static ComboBox Combo(string[] items, int selected)
        {
            var c = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(3, 8, 3, 8) };
            c.Items.AddRange(items);
            c.SelectedIndex = selected;
            return c;
        }

        private static Button Swatch(Func<string> get, Action<string> set)
        {
            var btn = new Button { Dock = DockStyle.Fill, Margin = new Padding(3, 8, 3, 8), FlatStyle = FlatStyle.Flat, Text = "" };
            btn.BackColor = Hex(get());
            btn.Click += (s, e) =>
            {
                using var dlg = new ColorDialog { Color = btn.BackColor, FullOpen = true };
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    btn.BackColor = dlg.Color;
                    set($"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}");
                }
            };
            return btn;
        }

        private static Color Hex(string s)
        {
            try { return ColorTranslator.FromHtml(s); } catch { return Color.Gray; }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            else base.OnFormClosing(e);
        }
    }
}
