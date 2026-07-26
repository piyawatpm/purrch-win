using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Purrch
{
    public struct UpdateInfo
    {
        public bool Available;
        public string Version;   // e.g. "v0.3.0"
        public string PageUrl;   // the release page to open
    }

    /// Checks GitHub Releases for a newer version. It only *reports* an update and
    /// links to the download — it never silently fetches or replaces an executable,
    /// which keeps it clear of antivirus/behavioural heuristics.
    public static class Updater
    {
        private const string LatestApi = "https://api.github.com/repos/piyawatpm/purrch-win/releases/latest";

        public static async Task<UpdateInfo> CheckAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Purrch-Updater");
                http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

                var json = await http.GetStringAsync(LatestApi);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
                string url = root.TryGetProperty("html_url", out var u) ? u.GetString() : null;

                var latest = ParseVersion(tag);
                var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

                return new UpdateInfo
                {
                    Available = latest != null && latest > current,
                    Version = tag ?? "",
                    PageUrl = url ?? "",
                };
            }
            catch
            {
                return new UpdateInfo { Available = false };
            }
        }

        private static Version ParseVersion(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            var s = tag.TrimStart('v', 'V');
            return Version.TryParse(s, out var v) ? v : null;
        }
    }
}
