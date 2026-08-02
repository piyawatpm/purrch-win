using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Purrch
{
    public class CoatPalette
    {
        public Color Outline, Dark, Mid, Light, Rim;

        public string Encoded => $"{H(Outline)},{H(Dark)},{H(Mid)},{H(Light)},{H(Rim)}";

        public static CoatPalette Decode(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var p = s.Split(',');
            if (p.Length < 5) return null;
            return new CoatPalette { Outline = C(p[0]), Dark = C(p[1]), Mid = C(p[2]), Light = C(p[3]), Rim = C(p[4]) };
        }

        private static string H(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        private static Color C(string s) { try { return ColorTranslator.FromHtml(s); } catch { return Color.Gray; } }
    }

    public class PortraitResult { public Bitmap Idle; public CoatPalette Palette; public string Species; }

    /// Photo → pixel-art idle sprite: cut out, fit to the sprite frame with the
    /// feet on the floor, and sample a coat ramp so the rest of the rig can match.
    public static class PortraitPipeline
    {
        public static PortraitResult Process(Bitmap raw, string species, int frameW, int frameH, int ground, bool crisp = true)
        {
            using var subject = EnsureTransparent(raw);
            var box = AlphaBBox(subject) ?? new Rectangle(0, 0, subject.Width, subject.Height);
            using var cropped = subject.Clone(box, PixelFormat.Format32bppArgb);

            double maxW = frameW - 2, maxH = Math.Max(1, ground - 2);
            double scale = Math.Min(maxW / cropped.Width, maxH / cropped.Height);
            int newW = Math.Max(1, (int)Math.Round(cropped.Width * scale));
            int newH = Math.Max(1, (int)Math.Round(cropped.Height * scale));

            var idle = new Bitmap(frameW, frameH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(idle))
            {
                // Crisp (AI pixel art) keeps hard pixels; the free photo path
                // downscales smoothly then posterises so it reads as pixel art.
                g.InterpolationMode = crisp
                    ? System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor
                    : System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                int x = (frameW - newW) / 2;
                int y = ground - newH;                 // content bottom (feet) on the ground row
                g.DrawImage(cropped, new Rectangle(x, y, newW, newH),
                            new Rectangle(0, 0, cropped.Width, cropped.Height), GraphicsUnit.Pixel);
            }
            if (!crisp) Posterize(idle, 6);
            return new PortraitResult { Idle = idle, Palette = ExtractPalette(idle) ?? Fallback(species), Species = species };
        }

        private static void Posterize(Bitmap bmp, int levels)
        {
            double step = 255.0 / Math.Max(1, levels - 1);
            byte Snap(byte v) => (byte)Math.Min(255, Math.Round(v / step) * step);
            Edit(bmp, (r, g, b, a) => a <= 20 ? (r, g, b, a) : (Snap(r), Snap(g), Snap(b), a));
        }

        public static void TintOpaque(Bitmap bmp, Color c) =>
            Edit(bmp, (r, g, b, a) => a > 20 ? (c.R, c.G, c.B, a) : (r, g, b, a));

        private static Bitmap EnsureTransparent(Bitmap raw)
        {
            var bmp = new Bitmap(raw.Width, raw.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) g.DrawImage(raw, 0, 0, raw.Width, raw.Height);
            if (TransparentFraction(bmp) > 0.02) return bmp;
            // Opaque background: knock out pixels near the corner colour.
            var bg = CornerColor(bmp);
            Edit(bmp, (r, g, b, a) =>
                (Math.Abs(r - bg.R) + Math.Abs(g - bg.G) + Math.Abs(b - bg.B) < 42) ? (r, g, b, (byte)0) : (r, g, b, a));
            return bmp;
        }

        private static CoatPalette ExtractPalette(Bitmap idle)
        {
            var cols = new List<(double lum, Color c)>();
            ForEachOpaque(idle, (r, g, b) => cols.Add((0.299 * r + 0.587 * g + 0.114 * b, Color.FromArgb(r, g, b))));
            if (cols.Count < 8) return null;
            cols.Sort((a, b) => a.lum.CompareTo(b.lum));
            Color At(double p) => cols[Math.Min(cols.Count - 1, Math.Max(0, (int)((cols.Count - 1) * p)))].c;
            var dark = At(0.20); var mid = At(0.50); var light = At(0.85);
            return new CoatPalette { Outline = Scale(dark, 0.5), Dark = dark, Mid = mid, Light = light, Rim = Mix(light, Color.White, 0.35) };
        }

        public static CoatPalette Fallback(string species) => species == "dog"
            ? new CoatPalette { Outline = Rgb(70, 52, 40), Dark = Rgb(196, 168, 128), Mid = Rgb(224, 200, 160), Light = Rgb(242, 226, 196), Rim = Rgb(252, 244, 228) }
            : new CoatPalette { Outline = Rgb(9, 9, 13), Dark = Rgb(25, 25, 31), Mid = Rgb(37, 37, 46), Light = Rgb(52, 52, 64), Rim = Rgb(110, 114, 138) };

        // --- pixel helpers (32bppArgb is B,G,R,A in memory) ---

        private static void Edit(Bitmap bmp, Func<byte, byte, byte, byte, (byte, byte, byte, byte)> fn)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            int n = data.Stride * data.Height;
            var buf = new byte[n];
            Marshal.Copy(data.Scan0, buf, 0, n);
            for (int i = 0; i < n; i += 4)
            {
                var (r, g, b, a) = fn(buf[i + 2], buf[i + 1], buf[i], buf[i + 3]);
                buf[i] = b; buf[i + 1] = g; buf[i + 2] = r; buf[i + 3] = a;
            }
            Marshal.Copy(buf, 0, data.Scan0, n);
            bmp.UnlockBits(data);
        }

        private static void ForEachOpaque(Bitmap bmp, Action<byte, byte, byte> fn)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int n = data.Stride * data.Height;
            var buf = new byte[n];
            Marshal.Copy(data.Scan0, buf, 0, n);
            for (int i = 0; i < n; i += 4)
                if (buf[i + 3] > 200) fn(buf[i + 2], buf[i + 1], buf[i]);
            bmp.UnlockBits(data);
        }

        private static double TransparentFraction(Bitmap bmp)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int n = data.Stride * data.Height;
            var buf = new byte[n];
            Marshal.Copy(data.Scan0, buf, 0, n);
            bmp.UnlockBits(data);
            int t = 0, total = 0;
            for (int i = 0; i < n; i += 4) { total++; if (buf[i + 3] < 16) t++; }
            return total == 0 ? 0 : (double)t / total;
        }

        private static Rectangle? AlphaBBox(Bitmap bmp, byte threshold = 20)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int stride = data.Stride, h = data.Height, w = bmp.Width;
            var buf = new byte[stride * h];
            Marshal.Copy(data.Scan0, buf, 0, buf.Length);
            bmp.UnlockBits(data);
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (buf[y * stride + x * 4 + 3] > threshold)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
            if (maxX < minX || maxY < minY) return null;
            return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static Color CornerColor(Bitmap bmp)
        {
            int w = bmp.Width - 1, h = bmp.Height - 1;
            var c = new[] { bmp.GetPixel(0, 0), bmp.GetPixel(w, 0), bmp.GetPixel(0, h), bmp.GetPixel(w, h) };
            int r = 0, g = 0, b = 0;
            foreach (var p in c) { r += p.R; g += p.G; b += p.B; }
            return Color.FromArgb(r / 4, g / 4, b / 4);
        }

        private static Color Rgb(int r, int g, int b) => Color.FromArgb(r, g, b);
        private static Color Scale(Color c, double f) =>
            Color.FromArgb(Clamp(c.R * f), Clamp(c.G * f), Clamp(c.B * f));
        private static Color Mix(Color a, Color b, double t) =>
            Color.FromArgb(Clamp(a.R * (1 - t) + b.R * t), Clamp(a.G * (1 - t) + b.G * t), Clamp(a.B * (1 - t) + b.B * t));
        private static int Clamp(double v) => Math.Min(255, Math.Max(0, (int)Math.Round(v)));
    }

    /// Persists the generated look and re-applies it at launch.
    public static class PortraitStore
    {
        private static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Purrch");
        private static string IdlePath => Path.Combine(Dir, "custom_idle.png");

        public static void Apply(PortraitResult r, AppSettings settings, SpriteLibrary lib)
        {
            try { Directory.CreateDirectory(Dir); r.Idle.Save(IdlePath, ImageFormat.Png); } catch { }
            settings.CustomCoat = r.Palette.Encoded;
            settings.CustomPetEnabled = true;
            settings.Species = r.Species;
            settings.Save();
            lib.SetCustomPet((Bitmap)r.Idle.Clone(), r.Palette, r.Species);
        }

        public static void Revert(AppSettings settings, SpriteLibrary lib)
        {
            settings.CustomPetEnabled = false;
            settings.Save();
            lib.ClearCustomPet();
        }

        public static void Restore(AppSettings settings, SpriteLibrary lib)
        {
            if (!settings.CustomPetEnabled) return;
            try
            {
                if (!File.Exists(IdlePath)) return;
                using var img = new Bitmap(IdlePath);
                lib.SetCustomPet(new Bitmap(img), CoatPalette.Decode(settings.CustomCoat), settings.Species);
            }
            catch { }
        }
    }
}
