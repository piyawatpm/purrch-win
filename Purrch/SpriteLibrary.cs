using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Purrch
{
    public class AnimInfo { public int frames { get; set; } public double msPerFrame { get; set; } }

    public class Manifest
    {
        public int frameWidth { get; set; } = 40;
        public int frameHeight { get; set; } = 32;
        public int ground { get; set; } = 28;
        public Dictionary<string, AnimInfo> animations { get; set; } = new();
    }

    /// Loads the embedded sprite sheets (shared with the macOS build) and slices
    /// them into per-frame bitmaps in both facing directions, cached on demand.
    public class SpriteLibrary
    {
        public Manifest Manifest { get; }
        public int FW => Manifest.frameWidth;
        public int FH => Manifest.frameHeight;

        private readonly Assembly asm = Assembly.GetExecutingAssembly();
        private readonly Dictionary<string, string> resByFile = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Bitmap[]> cache = new();
        private List<Recolor.Map> paletteMaps = new();

        /// Remaps the eye / inner-ear / collar / bell key colours to user choices,
        /// then clears the frame cache so the next draw reflects them.
        public void SetPalette(Color eye, Color ear, Color collar, Color bell)
        {
            paletteMaps = new List<Recolor.Map>
            {
                Key(222, 198, 78, eye),
                Key(156, 132, 44, Shade(eye, 0.70)),
                Key(84, 52, 62, ear),
                Key(46, 40, 64, collar),
                Key(206, 176, 88, bell),
                Key(180, 72, 72, collar),   // bandana cloth follows the collar colour
            };
            cache.Clear();
        }

        private static Recolor.Map Key(int r, int g, int b, Color to) =>
            new Recolor.Map { fr = (byte)r, fg = (byte)g, fb = (byte)b, tr = to.R, tg = to.G, tb = to.B };

        private static Color Shade(Color c, double f) =>
            Color.FromArgb((int)(c.R * f), (int)(c.G * f), (int)(c.B * f));

        // Photo likeness: a custom idle sprite (the pet's own pixels) plus a coat
        // remap so every other animation takes on the pet's colours.
        private Bitmap customIdle;
        private List<Recolor.Map> coatMaps = new();

        public void SetCustomPet(Bitmap idle, CoatPalette coat, string species)
        {
            customIdle = idle;
            coatMaps = coat == null ? new List<Recolor.Map>() : BuildCoatMaps(coat, species);
            cache.Clear();
        }

        public void ClearCustomPet()
        {
            customIdle = null;
            coatMaps = new List<Recolor.Map>();
            cache.Clear();
        }

        private static List<Recolor.Map> BuildCoatMaps(CoatPalette coat, string species)
        {
            (int r, int g, int b)[] from = species == "dog"
                ? new[] { (70, 52, 40), (196, 168, 128), (224, 200, 160), (242, 226, 196), (252, 244, 228) }
                : new[] { (9, 9, 13), (25, 25, 31), (37, 37, 46), (52, 52, 64), (110, 114, 138) };
            Color[] to = { coat.Outline, coat.Dark, coat.Mid, coat.Light, coat.Rim };
            var maps = new List<Recolor.Map>();
            for (int i = 0; i < 5; i++) maps.Add(Key(from[i].r, from[i].g, from[i].b, to[i]));
            return maps;
        }

        /// The stock idle frame for a species — the pose reference sent to the model.
        public Bitmap IdleTemplate(string species)
        {
            string file = $"{species}__bell__idle.png";
            if (!resByFile.TryGetValue(file, out var res)) resByFile.TryGetValue("cat__bell__idle.png", out res);
            if (res == null) return null;
            using var s = asm.GetManifestResourceStream(res);
            using var sheet = new Bitmap(s);
            var fr = new Bitmap(FW, FH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(fr))
                g.DrawImage(sheet, new Rectangle(0, 0, FW, FH), new Rectangle(0, 0, FW, FH), GraphicsUnit.Pixel);
            return fr;
        }

        // Wraps the custom idle in a gentle vertical bob so it looks alive.
        private static Bitmap[] BreathingFrames(Bitmap idle, bool facingLeft)
        {
            int[] offsets = { 0, 0, 0, 0, 1, 1, 1, 1 };
            var frames = new Bitmap[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                var fr = new Bitmap(idle.Width, idle.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(fr)) g.DrawImage(idle, 0, offsets[i]);
                if (facingLeft) fr.RotateFlip(RotateFlipType.RotateNoneFlipX);
                frames[i] = fr;
            }
            return frames;
        }

        public SpriteLibrary()
        {
            // Map each embedded resource to its bare filename. Resource names look
            // like "Purrch.Assets.Sprites.cat__bell__idle.png"; the filename is the
            // last two dot-segments (name + extension) since sprite files carry no
            // other dots.
            foreach (var name in asm.GetManifestResourceNames())
            {
                var parts = name.Split('.');
                if (parts.Length < 2) continue;
                var file = parts[parts.Length - 2] + "." + parts[parts.Length - 1];
                resByFile[file] = name;
            }

            Manifest = LoadManifest() ?? new Manifest();
        }

        private Manifest LoadManifest()
        {
            if (!resByFile.TryGetValue("sprites.json", out var res)) return null;
            using var s = asm.GetManifestResourceStream(res);
            if (s == null) return null;
            return JsonSerializer.Deserialize<Manifest>(s);
        }

        public double Ms(string anim) => Manifest.animations.TryGetValue(anim, out var i) ? i.msPerFrame : 150;
        public int FrameCount(string anim) => Manifest.animations.TryGetValue(anim, out var i) ? Math.Max(1, i.frames) : 1;

        /// Raw stream for an embedded asset by bare filename (e.g. "tray.png").
        public Stream ResourceStream(string file)
            => resByFile.TryGetValue(file, out var res) ? asm.GetManifestResourceStream(res) : null;

        private readonly Dictionary<string, Bitmap[]> bowlCache = new();

        /// A food bowl as [full, empty]; the sheet is two frames side by side.
        public Bitmap[] Bowl(string kind)
        {
            if (bowlCache.TryGetValue(kind, out var cached)) return cached;
            if (!resByFile.TryGetValue($"bowl_{kind}.png", out var res))
                resByFile.TryGetValue("bowl_kibble.png", out res);
            if (res == null) { var none = Array.Empty<Bitmap>(); bowlCache[kind] = none; return none; }

            using var stream = asm.GetManifestResourceStream(res);
            using var sheet = new Bitmap(stream);
            int w = sheet.Width / 2, h = sheet.Height;
            var frames = new Bitmap[2];
            for (int i = 0; i < 2; i++)
            {
                var fr = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(fr))
                    g.DrawImage(sheet, new Rectangle(0, 0, w, h), new Rectangle(i * w, 0, w, h), GraphicsUnit.Pixel);
                frames[i] = fr;
            }
            bowlCache[kind] = frames;
            return frames;
        }

        private readonly Dictionary<string, Bitmap[]> toyCache = new();

        /// A toy as two animation frames (mouse | ball | feather).
        public Bitmap[] Toy(string kind)
        {
            if (toyCache.TryGetValue(kind, out var cached)) return cached;
            string file = kind == "ball" ? "toy_ball.png" : kind == "feather" ? "toy_feather.png" : "mouse.png";
            if (!resByFile.TryGetValue(file, out var res)) resByFile.TryGetValue("mouse.png", out res);
            if (res == null) { var none = Array.Empty<Bitmap>(); toyCache[kind] = none; return none; }

            using var stream = asm.GetManifestResourceStream(res);
            using var sheet = new Bitmap(stream);
            int w = sheet.Width / 2, h = sheet.Height;
            var frames = new Bitmap[2];
            for (int i = 0; i < 2; i++)
            {
                var fr = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(fr))
                    g.DrawImage(sheet, new Rectangle(0, 0, w, h), new Rectangle(i * w, 0, w, h), GraphicsUnit.Pixel);
                frames[i] = fr;
            }
            toyCache[kind] = frames;
            return frames;
        }

        /// The per-frame bitmaps for a species/collar-style/animation, flipped when
        /// facing left.
        public Bitmap[] Frames(string species, string style, string anim, bool facingLeft)
        {
            string key = species + "|" + style + "|" + anim + "|" + (facingLeft ? "L" : "R");
            if (cache.TryGetValue(key, out var cached)) return cached;

            // The photo likeness replaces the idle clip outright — it's the pet's
            // own pixels, so it isn't recoloured — with a breathing bob added.
            if (customIdle != null && anim == "idle")
            {
                var breathe = BreathingFrames(customIdle, facingLeft);
                cache[key] = breathe;
                return breathe;
            }

            string file = $"{species}__{style}__{anim}.png";
            if (!resByFile.TryGetValue(file, out var res))
                resByFile.TryGetValue($"{species}__bell__{anim}.png", out res);
            if (res == null) resByFile.TryGetValue($"cat__bell__{anim}.png", out res);
            if (res == null) resByFile.TryGetValue("cat__bell__idle.png", out res);
            if (res == null)
            {
                // No sheet resolved (shouldn't happen with the shipped assets) — a
                // transparent placeholder keeps the 30fps loop from throwing.
                var blank = new[] { new Bitmap(FW, FH, PixelFormat.Format32bppArgb) };
                cache[key] = blank;
                return blank;
            }

            int n = FrameCount(anim);
            var frames = new Bitmap[n];
            using (var stream = asm.GetManifestResourceStream(res))
            using (var sheet = new Bitmap(stream))
            {
                for (int i = 0; i < n; i++)
                {
                    var fr = new Bitmap(FW, FH, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(fr))
                    {
                        g.DrawImage(sheet, new Rectangle(0, 0, FW, FH),
                                    new Rectangle(i * FW, 0, FW, FH), GraphicsUnit.Pixel);
                    }
                    Recolor.Apply(fr, paletteMaps);
                    Recolor.Apply(fr, coatMaps);
                    if (facingLeft) fr.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    frames[i] = fr;
                }
            }
            cache[key] = frames;
            return frames;
        }
    }
}
