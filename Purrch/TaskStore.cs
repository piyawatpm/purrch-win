using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Purrch
{
    public class TodoItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "";
        public DateTime CreatedOn { get; set; }        // start of the day it was added
        public DateTime? CompletedAt { get; set; }     // null while still open
        public bool IsDone => CompletedAt != null;
    }

    /// A flat to-do list persisted to %APPDATA%\Purrch\tasks.json. Ported from the
    /// macOS build: there's no per-day bucketing — an open task simply stays open,
    /// so it reappears every day until it's done ("carry forward" is the default).
    public class TaskStore
    {
        public static readonly TaskStore Shared = new();

        public event Action Changed;
        public event Action TaskCompleted;   // an open task ticked off — earns a meal
        public event Action AllDoneToday;    // the last open task finished

        private List<TodoItem> items = new();
        private readonly string filePath;

        private TaskStore()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Purrch");
            Directory.CreateDirectory(dir);
            filePath = Path.Combine(dir, "tasks.json");
            Load();
        }

        /// Everything still open (whenever added) plus whatever was finished today.
        public List<TodoItem> Today()
        {
            var list = items.Where(i => !i.IsDone).OrderBy(i => i.CreatedOn).ToList();
            list.AddRange(DoneToday());
            return list;
        }

        public List<TodoItem> DoneToday() =>
            items.Where(i => i.CompletedAt != null && i.CompletedAt.Value.Date == DateTime.Now.Date)
                 .OrderBy(i => i.CompletedAt).ToList();

        public int OpenCount => items.Count(i => !i.IsDone);

        /// Days an open task has been rolling over. 0 means it was added today.
        public int CarriedDays(TodoItem it) => Math.Max(0, (DateTime.Now.Date - it.CreatedOn.Date).Days);

        public void Add(string title)
        {
            title = (title ?? "").Trim();
            if (title.Length == 0) return;
            items.Add(new TodoItem { Title = title, CreatedOn = DateTime.Now.Date });
            Save();
        }

        public void Toggle(string id)
        {
            var it = items.FirstOrDefault(i => i.Id == id);
            if (it == null) return;
            bool wasOpen = !it.IsDone;
            it.CompletedAt = it.IsDone ? (DateTime?)null : DateTime.Now;
            Save();
            if (wasOpen)
            {
                TaskCompleted?.Invoke();
                if (OpenCount == 0 && DoneToday().Count > 0) AllDoneToday?.Invoke();
            }
        }

        public void Remove(string id) { items.RemoveAll(i => i.Id == id); Save(); }

        /// Drops finished tasks older than the retention window so the file can't grow forever.
        public void PruneHistory(int days = 365)
        {
            var cutoff = DateTime.Now.AddDays(-days);
            int before = items.Count;
            items.RemoveAll(i => i.CompletedAt != null && i.CompletedAt.Value < cutoff);
            if (items.Count != before) Save();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(filePath))
                    items = JsonSerializer.Deserialize<List<TodoItem>>(File.ReadAllText(filePath)) ?? new();
            }
            catch { items = new(); }
        }

        private void Save()
        {
            try { File.WriteAllText(filePath, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true })); }
            catch { /* best effort */ }
            Changed?.Invoke();
        }
    }
}
