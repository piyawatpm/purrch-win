using System;
using System.Drawing;
using System.Windows.Forms;

namespace Purrch
{
    /// A small settings window: species, collar style, and follow/roam mode.
    /// Changes apply live and persist. Closing hides it.
    public class SettingsForm : Form
    {
        private static readonly string[] Styles = { "none", "band", "bell", "bowtie", "bandana" };
        private static readonly string[] StyleLabels = { "None", "Band", "Bell", "Bow tie", "Bandana" };

        public SettingsForm(AppSettings settings, Action apply)
        {
            Text = "Purrch — Settings";
            ClientSize = new Size(320, 190);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 9.5f);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(16) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 3; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

            var species = Combo(new[] { "Cat", "Dog" }, settings.Species == "dog" ? 1 : 0);
            species.SelectedIndexChanged += (s, e) =>
            {
                settings.Species = species.SelectedIndex == 1 ? "dog" : "cat";
                settings.Save(); apply();
            };

            var collar = Combo(StyleLabels, Math.Max(0, Array.IndexOf(Styles, settings.Style)));
            collar.SelectedIndexChanged += (s, e) =>
            {
                settings.Style = Styles[collar.SelectedIndex];
                settings.Save(); apply();
            };

            var mode = Combo(new[] { "Free roam", "Follow cursor" }, settings.Mode == "follow" ? 1 : 0);
            mode.SelectedIndexChanged += (s, e) =>
            {
                settings.Mode = mode.SelectedIndex == 1 ? "follow" : "roam";
                settings.Save(); apply();
            };

            layout.Controls.Add(Caption("Species"), 0, 0);
            layout.Controls.Add(species, 1, 0);
            layout.Controls.Add(Caption("Collar"), 0, 1);
            layout.Controls.Add(collar, 1, 1);
            layout.Controls.Add(Caption("Mode"), 0, 2);
            layout.Controls.Add(mode, 1, 2);
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            else base.OnFormClosing(e);
        }
    }
}
