using System;
using System.Drawing;
using System.Windows.Forms;

namespace Purrch
{
    public enum PetState { Idle, Walk, Sit, Groom, Sleep, Stretch, Yawn, Happy, Drag, Fall, Loaf, Scratch, Sniff, Wiggle }

    /// The behaviour loop: wanders the bottom of the screen, pauses to sit, groom,
    /// stretch, yawn, or nap after a long idle, and can be picked up and dropped.
    /// Screen-space anchor is (X = horizontal centre, FeetY = the floor line).
    public class Brain
    {
        public double X, FeetY;
        public int Dir = 1;                 // 1 = right, -1 = left
        public PetState State = PetState.Idle;
        public int Frame;
        public string Species = "cat";
        public int Scale = 3;
        public bool Dragging;
        public double Speed = 44;           // px / second while walking

        // Wired to the sprite library so timing follows the shared manifest.
        public Func<string, double> AnimMs = _ => 150;
        public Func<string, int> AnimFrames = _ => 1;

        private readonly int FW, FH, ground;
        private readonly Random rng = new();
        private Rectangle screen;
        private double frameT, stateT, hold = 2, velY, grabDX, grabDY;

        public Brain(int fw, int fh, int groundRow)
        {
            FW = fw; FH = fh; ground = groundRow;
            UpdateScreen();
            X = screen.Left + screen.Width * 0.4;
            FeetY = FloorY;
        }

        public void UpdateScreen()
        {
            // WorkingArea, not Bounds, so the floor sits on the visible desktop
            // rather than behind the taskbar.
            screen = Screen.PrimaryScreen != null ? Screen.PrimaryScreen.WorkingArea : new Rectangle(0, 0, 1280, 720);
        }

        public double FloorY => screen.Bottom;
        public int SpriteW => FW * Scale;
        public int SpriteH => FH * Scale;
        private int FootInset => FH - ground;   // sprite rows between the feet and the canvas bottom

        public string CurrentAnim => State switch
        {
            PetState.Idle => "idle",
            PetState.Walk => "walk",
            PetState.Sit => "sit",
            PetState.Groom => "groom",
            PetState.Sleep => "sleep",
            PetState.Stretch => "stretch",
            PetState.Yawn => "yawn",
            PetState.Happy => "happy",
            PetState.Drag => "drag",
            PetState.Fall => "fall",
            PetState.Loaf => "loaf",
            PetState.Scratch => "scratch",
            PetState.Sniff => "sniff",
            PetState.Wiggle => "wiggle",
            _ => "idle",
        };

        /// Window top-left so the pet's feet land on FeetY and it's centred on X.
        public Point WindowTopLeft()
        {
            int left = (int)Math.Round(X - SpriteW / 2.0);
            int top = (int)Math.Round(FeetY + FootInset * Scale - SpriteH);
            return new Point(left, top);
        }

        // --- interaction ---
        // Grab remembers where on the pet you took hold, so it doesn't snap to the
        // cursor when picked up.
        public void Grab(int cursorX, int cursorY) { Dragging = true; grabDX = X - cursorX; grabDY = FeetY - cursorY; SetState(PetState.Drag); }
        public void MoveTo(int cursorX, int cursorY) { X = cursorX + grabDX; FeetY = cursorY + grabDY; }
        public void Release() { Dragging = false; velY = 0; SetState(PetState.Fall); }
        public void Poke() { if (State == PetState.Sleep) SetState(PetState.Idle); SetState(PetState.Happy); stateT = 0; hold = 1.2; }

        private void SetState(PetState s)
        {
            if (State == s) return;
            State = s; Frame = 0; frameT = 0;
        }

        public void Update(double dt)
        {
            if (State == PetState.Fall)
            {
                velY += 1400 * dt;
                FeetY += velY * dt;
                if (FeetY >= FloorY) { FeetY = FloorY; velY = 0; SetState(PetState.Idle); }
            }
            else if (!Dragging)
            {
                stateT += dt;
                if (stateT >= hold) { stateT = 0; Decide(); }

                if (State == PetState.Walk)
                {
                    X += Dir * Speed * dt;
                    double half = SpriteW / 2.0;
                    if (X < screen.Left + half) { X = screen.Left + half; Dir = 1; }
                    if (X > screen.Right - half) { X = screen.Right - half; Dir = -1; }
                }
            }

            double ms = AnimMs(CurrentAnim);
            int fc = AnimFrames(CurrentAnim);
            frameT += dt * 1000;
            while (frameT >= ms) { frameT -= ms; Frame = (Frame + 1) % Math.Max(1, fc); }
        }

        private void Decide()
        {
            if (State == PetState.Walk)
            {
                if (rng.NextDouble() < 0.12)
                {
                    SetState(PetState.Sleep);           // occasionally curl up for a real nap
                    hold = 6 + rng.NextDouble() * 8;
                }
                else
                {
                    var rest = new[] { PetState.Idle, PetState.Sit, PetState.Groom, PetState.Stretch, PetState.Yawn, PetState.Loaf, PetState.Scratch, PetState.Sniff, PetState.Wiggle, PetState.Idle };
                    SetState(rest[rng.Next(rest.Length)]);
                    hold = 2 + rng.NextDouble() * 4;
                }
            }
            else if (State == PetState.Happy)
            {
                SetState(PetState.Idle);
                hold = 2;
            }
            else
            {
                SetState(PetState.Walk);
                Dir = rng.NextDouble() < 0.5 ? -1 : 1;
                hold = 2 + rng.NextDouble() * 5;
            }
        }
    }
}
