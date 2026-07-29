using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using System.Windows.Forms;

namespace Purrch
{
    /// User choices persisted to %APPDATA%\Purrch\settings.json, plus the native
    /// launch-at-login hook (a value under the HKCU Run key).
    public class AppSettings
    {
        public string Species { get; set; } = "cat";
        public int Scale { get; set; } = 3;
        public bool LaunchAtLogin { get; set; }
        public bool Sound { get; set; } = true;
        public string Style { get; set; } = "bell";   // collar: none|band|bell|bowtie|bandana
        public string Mode { get; set; } = "roam";     // roam | follow
        public string EyeHex { get; set; } = "#DEC64E";
        public string EarHex { get; set; } = "#3E2F31";
        public string CollarHex { get; set; } = "#2E2840";
        public string BellHex { get; set; } = "#CEB058";

        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "Purrch";

        private static string Dir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Purrch");
        private static string FilePath => Path.Combine(Dir, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            }
            catch { /* fall through to defaults */ }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
            }
            catch { /* best effort */ }
        }

        /// Reflects the on-disk Run key into `LaunchAtLogin` and re-points it at the
        /// current executable if enabled (handles the app being moved).
        public void SyncStartup()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
                if (key == null) return;
                bool present = key.GetValue(RunValue) != null;
                if (LaunchAtLogin) key.SetValue(RunValue, "\"" + Application.ExecutablePath + "\"");
                else if (present) key.DeleteValue(RunValue, false);
            }
            catch { /* best effort */ }
        }
    }
}
