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

        /// The per-frame bitmaps for a species/animation, flipped when facing left.
        public Bitmap[] Frames(string species, string anim, bool facingLeft)
        {
            string key = species + "|" + anim + "|" + (facingLeft ? "L" : "R");
            if (cache.TryGetValue(key, out var cached)) return cached;

            string file = $"{species}__bell__{anim}.png";
            if (!resByFile.TryGetValue(file, out var res))
                resByFile.TryGetValue($"cat__bell__{anim}.png", out res);
            if (res == null) resByFile.TryGetValue($"{species}__bell__idle.png", out res);
            if (res == null) resByFile.TryGetValue("cat__bell__idle.png", out res);

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
                    if (facingLeft) fr.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    frames[i] = fr;
                }
            }
            cache[key] = frames;
            return frames;
        }
    }
}
