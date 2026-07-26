using System;
using System.Drawing;
using System.Windows.Forms;
using static Purrch.Native;

namespace Purrch
{
    /// A per-pixel-alpha, always-on-top, no-taskbar window that draws the pet.
    /// Fully-transparent pixels let mouse clicks fall through to the desktop, so
    /// only the pet itself is interactive — no manual hit-testing needed.
    public class PetForm : Form
    {
        public PetForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Text = "Purrch";
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        // Never steal focus when shown or clicked.
        protected override bool ShowWithoutActivation => true;

        /// Pushes a 32-bpp premultiplied ARGB bitmap to the screen at `pos`.
        public void SetBitmap(Bitmap bmp, Point pos)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero, oldBitmap = IntPtr.Zero;
            try
            {
                hBitmap = bmp.GetHbitmap(Color.FromArgb(0));  // premultiplied bits from a PArgb bitmap
                oldBitmap = SelectObject(memDc, hBitmap);

                var size = new SIZE(bmp.Width, bmp.Height);
                var src = new POINT(0, 0);
                var dst = new POINT(pos.X, pos.Y);
                var blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA,
                };
                UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero) { SelectObject(memDc, oldBitmap); DeleteObject(hBitmap); }
                DeleteDC(memDc);
            }
        }
    }
}
