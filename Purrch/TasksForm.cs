using System;
using System.Drawing;
using System.Windows.Forms;

namespace Purrch
{
    /// The to-do window. Add a task and press Enter; tick it off and the pet gets
    /// fed. Closing hides it (state is kept) rather than destroying it.
    public class TasksForm : Form
    {
        private readonly TextBox input;
        private readonly FlowLayoutPanel list;

        public TasksForm()
        {
            Text = "Purrch — Tasks";
            ClientSize = new Size(400, 500);
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 9.5f);

            var header = new Label
            {
                Text = "Today",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Padding = new Padding(4, 8, 0, 0),
            };

            input = new TextBox { Dock = DockStyle.Fill };
            input.PlaceholderText = "Add a task and press Enter…";
            input.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { TaskStore.Shared.Add(input.Text); input.Clear(); e.SuppressKeyPress = true; }
            };
            var inputWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 6) };
            inputWrap.Controls.Add(input);

            list = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
            };

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(14) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(inputWrap, 0, 1);
            layout.Controls.Add(list, 0, 2);
            Controls.Add(layout);

            TaskStore.Shared.Changed += OnChanged;
            Rebuild();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            else { TaskStore.Shared.Changed -= OnChanged; base.OnFormClosing(e); }
        }

        // Deferred so the list isn't rebuilt while a checkbox is mid-event.
        private void OnChanged() { if (IsHandleCreated) BeginInvoke((Action)Rebuild); }

        private void Rebuild()
        {
            list.SuspendLayout();
            list.Controls.Clear();
            foreach (var it in TaskStore.Shared.Today())
                list.Controls.Add(MakeRow(it));
            list.ResumeLayout();
        }

        private CheckBox MakeRow(TodoItem it)
        {
            int carried = TaskStore.Shared.CarriedDays(it);
            string suffix = (!it.IsDone && carried > 0) ? $"   ({carried}d)" : "";
            var cb = new CheckBox
            {
                Text = it.Title + suffix,
                Checked = it.IsDone,
                AutoSize = true,
                Margin = new Padding(2, 3, 2, 3),
                Font = it.IsDone ? new Font(Font, FontStyle.Strikeout) : Font,
                ForeColor = it.IsDone ? Color.Gray
                          : (carried > 0 ? Color.FromArgb(150, 90, 40) : SystemColors.ControlText),
            };
            cb.CheckedChanged += (s, e) => TaskStore.Shared.Toggle(it.Id);
            return cb;
        }
    }
}
