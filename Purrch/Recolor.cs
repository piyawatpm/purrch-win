using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Purrch
{
    /// Swaps the sprites' placeholder key colours (eyes, inner ears, collar, bell)
    /// for user-chosen ones — the same trick the macOS build uses. Operates in
    /// place on a 32-bpp ARGB bitmap; matching is tolerant by a few levels because
    /// PNG decoding can shift channel values slightly.
    internal static class Recolor
    {
        public struct Map { public byte fr, fg, fb, tr, tg, tb; }

        public static void Apply(Bitmap bmp, List<Map> maps, int tolerance = 8)
        {
            if (maps == null || maps.Count == 0) return;
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            int n = data.Stride * data.Height;
            var buf = new byte[n];
            Marshal.Copy(data.Scan0, buf, 0, n);
            // 32bppArgb is laid out B, G, R, A in memory.
            for (int i = 0; i < n; i += 4)
            {
                if (buf[i + 3] < 128) continue;          // skip the antialiased fringe
                byte b = buf[i], g = buf[i + 1], r = buf[i + 2];
                foreach (var m in maps)
                {
                    if (Diff(r, m.fr) <= tolerance && Diff(g, m.fg) <= tolerance && Diff(b, m.fb) <= tolerance)
                    {
                        buf[i] = m.tb; buf[i + 1] = m.tg; buf[i + 2] = m.tr;
                        break;
                    }
                }
            }
            Marshal.Copy(buf, 0, data.Scan0, n);
            bmp.UnlockBits(data);
        }

        private static int Diff(byte a, byte b) => a > b ? a - b : b - a;
    }
}
