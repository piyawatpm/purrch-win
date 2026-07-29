using System;
using System.Drawing;
using System.Windows.Forms;

namespace Purrch
{
    public enum PetState { Idle, Walk, Sit, Groom, Sleep, Stretch, Yawn, Happy, Drag, Fall, Loaf, Scratch, Sniff, Wiggle, Eat, Jump, Play, Pounce }

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
        public string Mode = "roam";        // roam | follow (the cursor)
        public double CursorX;              // fed by the controller each tick

        // Wired to the sprite library so timing follows the shared manifest.
        public Func<string, double> AnimMs = _ => 150;
        public Func<string, int> AnimFrames = _ => 1;

        private readonly int FW, FH, ground;
        private readonly Random rng = new();
        private Rectangle screen;
        private double frameT, stateT, hold = 2, velY, grabDX, grabDY;

        // Feeding: a bowl on the floor the pet walks over to and eats.
        public double? BowlX;
        public double BowlY;
        public string BowlKind = "kibble";
        public bool BowlFull = true;
        private double? walkTarget;
        private double eatTimer;
        private static readonly string[] BowlKinds = { "kibble", "fish", "treat", "milk" };

        // Chatter: an occasional speech bubble above the pet.
        public string BubbleText;
        public Action OnChatter;
        private double bubbleTimer, chatterTimer;
        private static readonly string[] CatSays = { "meow", "mrrp?", "got a treat?", "pet me?", "purr…", "hi ♥", "mew" };
        private static readonly string[] DogSays = { "woof!", "borf", "treat?", "play?", "hi ♥", "wag", "arf" };

        // Toys: a mouse/ball/feather the pet chases and pounces on.
        public double? ToyX;
        public double ToyY;
        public string ToyKind = "mouse";
        public bool ToyRunning;
        public bool ToyFacingRight = true;
        private bool toyCaught;
        private double playTimer;

        public Brain(int fw, int fh, int groundRow)
        {
            FW = fw; FH = fh; ground = groundRow;
            UpdateScreen();
            X = screen.Left + screen.Width * 0.4;
            FeetY = FloorY;
            chatterTimer = 20 + rng.NextDouble() * 40;
        }

        public void Say(string text, double seconds = 3.5) { BubbleText = text; bubbleTimer = seconds; }

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
            PetState.Eat => "eat",
            PetState.Jump => "jump",
            PetState.Play => "play",
            PetState.Pounce => "pounce",
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
        public void Grab(int cursorX, int cursorY) { Dragging = true; grabDX = X - cursorX; grabDY = FeetY - cursorY; walkTarget = null; BowlX = null; ToyX = null; SetState(PetState.Drag); }
        public void MoveTo(int cursorX, int cursorY) { X = cursorX + grabDX; FeetY = cursorY + grabDY; }
        public void Release() { Dragging = false; velY = 0; SetState(PetState.Fall); }
        public void Poke() { if (State == PetState.Sleep) SetState(PetState.Idle); SetState(PetState.Happy); stateT = 0; hold = 1.2; }

        public void Wake() { if (State == PetState.Sleep) SetState(PetState.Idle); }

        // Drops a bowl a little ahead on the same surface and walks over to eat it.
        public void Feed()
        {
            if (Dragging || BowlX != null) return;
            Wake();
            double lo = screen.Left + SpriteW / 2.0, hi = screen.Right - SpriteW / 2.0;
            double reach = Math.Min(130, screen.Width * 0.2);
            double noseGap = SpriteW * 0.30;
            double dir = Dir;
            double ahead = X + dir * reach;
            if (ahead < lo || ahead > hi) dir = -dir;
            double standX = Math.Min(Math.Max(X + dir * reach, lo), hi);
            BowlX = Math.Min(Math.Max(standX + dir * noseGap, screen.Left), screen.Right);
            BowlY = FloorY;
            BowlKind = BowlKinds[rng.Next(BowlKinds.Length)];
            BowlFull = true;
            Dir = standX >= X ? 1 : -1;
            walkTarget = standX;
            if (State != PetState.Walk) SetState(PetState.Walk);
        }

        public void Celebrate()
        {
            if (State == PetState.Eat) return;
            SetState(PetState.Happy);
            stateT = 0; hold = 2.5;
        }

        public void Jump()
        {
            if (Dragging || State == PetState.Eat || FeetY < FloorY - 1) return;
            velY = -520;                 // upward impulse; the fall physics bring it back
            SetState(PetState.Jump);
        }

        public void ComeHere(double x)
        {
            Wake(); BowlX = null; ToyX = null;
            walkTarget = Math.Min(Math.Max(x, screen.Left + SpriteW / 2.0), screen.Right - SpriteW / 2.0);
        }

        public void SitNow() { Wake(); BowlX = null; ToyX = null; walkTarget = null; SetState(PetState.Sit); stateT = 0; hold = 9999; }
        public void ForceSleep() { BowlX = null; ToyX = null; walkTarget = null; SetState(PetState.Sleep); stateT = 0; hold = 9999; }

        // Drops a toy on the floor a little away so there's a chase.
        public void PlaceToy(string kind)
        {
            if (Dragging) return;
            Wake();
            BowlX = null; walkTarget = null;     // a toy cancels any meal
            ToyKind = kind;
            double margin = 40;
            double tx = screen.Left + margin + rng.NextDouble() * Math.Max(1, screen.Width - 2 * margin);
            if (Math.Abs(tx - X) < 120) tx = X + (tx >= X ? 160 : -160);
            ToyX = Math.Min(Math.Max(tx, screen.Left + 10), screen.Right - 10);
            ToyY = FloorY;
            ToyRunning = false;
            toyCaught = false;
            ToyFacingRight = ToyX.Value >= X;
        }

        private void SetState(PetState s)
        {
            if (State == s) return;
            State = s; Frame = 0; frameT = 0;
        }

        public void Update(double dt)
        {
            if (State == PetState.Fall || State == PetState.Jump)
            {
                velY += 1400 * dt;
                FeetY += velY * dt;
                if (FeetY >= FloorY) { FeetY = FloorY; velY = 0; SetState(PetState.Idle); }
            }
            else if (State == PetState.Eat)
            {
                eatTimer -= dt;
                if (eatTimer <= 1.2) BowlFull = false;      // the bowl empties as he eats
                if (eatTimer <= 0) { BowlX = null; SetState(PetState.Idle); }
            }
            else if (ToyX != null && !Dragging)
            {
                if (!toyCaught)
                {
                    // a mouse scurries away when the pet gets close
                    if (ToyKind == "mouse" && Math.Abs(ToyX.Value - X) < 70)
                    {
                        double away = (ToyX.Value - X) >= 0 ? 1 : -1;
                        ToyX = Math.Min(Math.Max(ToyX.Value + away * 130 * dt, screen.Left + 10), screen.Right - 10);
                        ToyRunning = true; ToyFacingRight = away > 0;
                    }
                    else ToyRunning = false;

                    Dir = ToyX.Value >= X ? 1 : -1;
                    if (State != PetState.Walk) SetState(PetState.Walk);
                    double step = Speed * 1.25 * dt;
                    if (Math.Abs(ToyX.Value - X) <= step + 6)
                    {
                        toyCaught = true; ToyRunning = false;
                        SetState(PetState.Pounce); playTimer = 2.6;
                    }
                    else X += Dir * step;
                }
                else
                {
                    playTimer -= dt;
                    if (State == PetState.Pounce && playTimer <= 1.6) SetState(PetState.Play);
                    if (playTimer <= 0) { ToyX = null; toyCaught = false; SetState(PetState.Idle); }
                }
            }
            else if (walkTarget != null && !Dragging)
            {
                double target = walkTarget.Value;
                Dir = target >= X ? 1 : -1;
                if (State != PetState.Walk) SetState(PetState.Walk);
                double step = Speed * dt;
                if (Math.Abs(target - X) <= step + 1)
                {
                    X = target;
                    walkTarget = null;
                    if (BowlX != null) { SetState(PetState.Eat); eatTimer = 3.4; }
                    else SetState(PetState.Idle);
                }
                else X += Dir * step;
            }
            else if (!Dragging)
            {
                if (Mode == "follow")
                {
                    double half = SpriteW / 2.0;
                    double gap = CursorX - X;
                    if (Math.Abs(gap) > 90)
                    {
                        Dir = gap >= 0 ? 1 : -1;
                        if (State != PetState.Walk) SetState(PetState.Walk);
                        X = Math.Min(Math.Max(X + Dir * Speed * dt, screen.Left + half), screen.Right - half);
                    }
                    else if (State == PetState.Walk) SetState(PetState.Idle);
                }
                else
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
            }

            double ms = AnimMs(CurrentAnim);
            int fc = AnimFrames(CurrentAnim);
            frameT += dt * 1000;
            while (frameT >= ms) { frameT -= ms; Frame = (Frame + 1) % Math.Max(1, fc); }

            // speech bubble timing + occasional spontaneous chatter
            if (BubbleText != null) { bubbleTimer -= dt; if (bubbleTimer <= 0) BubbleText = null; }
            chatterTimer -= dt;
            if (chatterTimer <= 0)
            {
                chatterTimer = 25 + rng.NextDouble() * 50;
                if (BubbleText == null && State != PetState.Sleep && State != PetState.Drag
                    && State != PetState.Fall && State != PetState.Eat)
                {
                    Say((Species == "dog" ? DogSays : CatSays)[rng.Next((Species == "dog" ? DogSays : CatSays).Length)]);
                    OnChatter?.Invoke();
                }
            }
        }

        private void Decide()
        {
            if (rng.NextDouble() < 0.08 && FeetY >= FloorY - 1) { Jump(); return; }

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
