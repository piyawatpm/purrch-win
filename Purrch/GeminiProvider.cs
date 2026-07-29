using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Purrch
{
    /// Calls Google's Gemini image model to redraw a pet photo as a pixel-art
    /// sprite (bring-your-own key), plus a free offline mock for testing the flow.
    internal static class GeminiProvider
    {
        private const string Model = "gemini-2.5-flash-image";

        public static async Task<Bitmap> RenderAsync(string apiKey, Bitmap photo, Bitmap template, string species)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) throw new Exception("Add your Gemini API key first.");
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent";
            string prompt =
                $"Redraw the {species} in the FIRST image as a small pixel-art game sprite. " +
                "Match the pose, framing, and proportions of the SECOND image (a reference silhouette): " +
                "full body, side three-quarter view, standing, facing right. " +
                "Keep the real pet's fur colours, markings, ear shape, and face. " +
                "Clean pixel art, limited palette, crisp hard pixels, transparent background, no shadow, no ground, no text.";

            var body = new
            {
                contents = new[]
                {
                    new { parts = new object[]
                    {
                        new { text = prompt },
                        new { inlineData = new { mimeType = "image/png", data = ToBase64(photo) } },
                        new { inlineData = new { mimeType = "image/png", data = ToBase64(template) } },
                    } }
                },
                generationConfig = new { responseModalities = new[] { "IMAGE" } },
            };

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            http.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var resp = await http.PostAsync(url, content);
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Gemini error {(int)resp.StatusCode}. {Trunc(text, 300)}");

            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("candidates", out var cands))
                foreach (var cand in cands.EnumerateArray())
                    if (cand.TryGetProperty("content", out var c) && c.TryGetProperty("parts", out var parts))
                        foreach (var part in parts.EnumerateArray())
                            if (part.TryGetProperty("inlineData", out var inline) && inline.TryGetProperty("data", out var d))
                            {
                                var bytes = Convert.FromBase64String(d.GetString());
                                using var ms = new MemoryStream(bytes);
                                return new Bitmap(ms);
                            }
            throw new Exception("Gemini returned no image.");
        }

        /// No-key stand-in: upscales + tints the pose template so the whole flow
        /// can be exercised without spending anything.
        public static Bitmap MockRender(Bitmap template)
        {
            int scale = 8;
            var big = new Bitmap(template.Width * scale, template.Height * scale, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(big))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(template, new Rectangle(0, 0, big.Width, big.Height),
                            new Rectangle(0, 0, template.Width, template.Height), GraphicsUnit.Pixel);
            }
            PortraitPipeline.TintOpaque(big, Color.FromArgb(226, 148, 74));
            return big;
        }

        private static string ToBase64(Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }

        private static string Trunc(string s, int n) => s == null ? "" : (s.Length <= n ? s : s.Substring(0, n));
    }
}
