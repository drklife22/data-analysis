using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WalkingFriend
{
    internal enum ClickReaction { Cycle, Random, Squash, Cry, Hop, None }

    internal sealed class PlayOptions
    {
        internal bool Walls = true, Ceiling = true, Windows = true, Idle = true;
        internal bool Escape = true, Follow = false, Focus = false, WorkFocus = false, Fullscreen = false;
        internal double Speed = 1.0;
        internal double FallSpeed = 1.2, SlideSpeed = 1.0;
        internal double EscapeDelay = .12;
        internal bool HungerCry = true, Secrets = true, ShortRest = false, ShowAffinity = false;
        internal int HungerMinutes = 60;
        internal ClickReaction ClickMode = ClickReaction.Cycle;
        internal static PlayOptions Current = new PlayOptions();
        internal static string Name = "걸어다니는친구_움직임.ini";
        internal static bool IsFocus(DateTime now)
        {
            var o = Current;
            return o.Focus || (o.WorkFocus && now.DayOfWeek != DayOfWeek.Saturday && now.DayOfWeek != DayOfWeek.Sunday && now.Hour >= 9 && now.Hour < 18);
        }
        internal static void Load()
        {
            string p = Path.Combine(Settings.ExeDir, Name);
            if (!File.Exists(p)) p = Path.Combine(Settings.AltDir, Name);
            try
            {
                bool? showAffinity = null; int affinityDisplayVersion = 0;
                foreach (string line in File.ReadAllLines(p, Encoding.UTF8))
                {
                    int i = line.IndexOf('='); if (i < 1) continue;
                    string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                    bool b = v != "0";
                    switch (k)
                    {
                        case "Walls": Current.Walls = b; break;
                        case "Ceiling": Current.Ceiling = b; break;
                        case "Windows": Current.Windows = b; break;
                        case "Idle": Current.Idle = b; break;
                        case "Escape": Current.Escape = b; break;
                        case "Follow": Current.Follow = b; break;
                        case "Focus": Current.Focus = b; break;
                        case "WorkFocus": Current.WorkFocus = b; break;
                        case "Fullscreen": Current.Fullscreen = b; break;
                        case "Speed": Current.Speed = ReadSpeed(v, Current.Speed, 2); break;
                        case "FallSpeed": Current.FallSpeed = ReadSpeed(v, Current.FallSpeed, 3); break;
                        case "SlideSpeed": Current.SlideSpeed = ReadSpeed(v, Current.SlideSpeed, 3); break;
                        case "HungerCry": Current.HungerCry = b; break;
                        case "Secrets": Current.Secrets = b; break;
                        case "ShortRest": Current.ShortRest = b; break;
                        case "ShowAffinity": showAffinity = b; break;
                        case "AffinityDisplayVersion": int.TryParse(v, out affinityDisplayVersion); break;
                        case "EscapeDelay":
                            double delay;
                            if (double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out delay) && !double.IsNaN(delay) && !double.IsInfinity(delay)) Current.EscapeDelay = Math.Max(.05, Math.Min(1, delay));
                            break;
                        case "HungerMinutes": int hunger; if (int.TryParse(v, out hunger)) Current.HungerMinutes = Math.Max(0, Math.Min(360, hunger)); break;
                        case "ClickMode":
                            ClickReaction mode;
                            if (Enum.TryParse<ClickReaction>(v, true, out mode) && Enum.IsDefined(typeof(ClickReaction), mode)) Current.ClickMode = mode;
                            break;
                    }
                }
                if (affinityDisplayVersion >= 206 && showAffinity.HasValue) Current.ShowAffinity = showAffinity.Value;
            }
            catch { }
        }
        private static double ReadSpeed(string text, double fallback, double maximum)
        {
            double value;
            if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) || double.IsNaN(value) || double.IsInfinity(value)) return fallback;
            return Math.Max(.5, Math.Min(maximum, value));
        }
        internal bool Save()
        {
            string text = "; 마루는 강쥐 움직임 설정\r\n";
            foreach (var f in typeof(PlayOptions).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
                if (f.FieldType == typeof(bool)) text += f.Name + "=" + ((bool)f.GetValue(this) ? "1" : "0") + "\r\n";
            text += "Speed=" + Speed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "\r\n";
            text += "FallSpeed=" + FallSpeed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "\r\n";
            text += "SlideSpeed=" + SlideSpeed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "\r\n";
            text += "ClickMode=" + ClickMode + "\r\n";
            text += "EscapeDelay=" + EscapeDelay.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "\r\n";
            text += "HungerMinutes=" + HungerMinutes + "\r\n";
            text += "AffinityDisplayVersion=206\r\n";
            foreach (string dir in new[] { Settings.ExeDir, Settings.AltDir })
            {
                try { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, Name), text, new UTF8Encoding(true)); return true; } catch { }
            }
            return false;
        }
    }

    internal sealed class SurfaceWindow
    {
        internal IntPtr Handle;
        internal Rectangle Bounds;
        internal SurfaceWindow(IntPtr h, Rectangle r) { Handle = h; Bounds = r; }
    }

    internal static class DesktopSurfaces
    {
        internal static Func<List<SurfaceWindow>> TestProvider;
        internal static List<SurfaceWindow> Cached = new List<SurfaceWindow>();
        internal static Func<Point> TestMouse;
        internal static bool IsWindows { get { return Environment.OSVersion.Platform == PlatformID.Win32NT; } }
        internal static Point Mouse { get { if (TestMouse != null) return TestMouse(); try { return Cursor.Position; } catch { return new Point(-10000, -10000); } } }
        internal static bool MousePressed { get { try { return Control.MouseButtons != MouseButtons.None; } catch { return false; } } }
        internal static void Refresh()
        {
            if (TestProvider != null) { Cached = TestProvider(); return; }
            if (!IsWindows) { Cached.Clear(); return; }
            var result = new List<SurfaceWindow>();
            try
            {
                EnumWindows(delegate(IntPtr h, IntPtr unused)
                {
                    uint pid; GetWindowThreadProcessId(h, out pid);
                    if (pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id || !IsWindowVisible(h) || IsIconic(h)) return true;
                    StringBuilder cls = new StringBuilder(128); GetClassName(h, cls, cls.Capacity);
                    string c = cls.ToString();
                    if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd" || c.Contains("tooltips")) return true;
                    int cloaked;
                    try { if (DwmGetWindowAttributeInt(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true; } catch { }
                    Rectangle r;
                    if (!TryBounds(h, out r) || r.Width < 160 || r.Height < 90) return true;
                    bool visibleTop = false;
                    foreach (double part in new[] { .18, .5, .82 })
                    {
                        IntPtr hit = WindowFromPoint(new WPoint(r.Left + (int)(r.Width * part), r.Top + 5));
                        if (hit == h || GetAncestor(hit, 2) == h) { visibleTop = true; break; }
                    }
                    if (visibleTop) result.Add(new SurfaceWindow(h, r));
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
            Cached = result;
        }
        internal static bool TryBounds(IntPtr h, out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            if (TestProvider != null)
            {
                var t = TestProvider().FirstOrDefault(w => w.Handle == h);
                if (t == null) return false; bounds = t.Bounds; return true;
            }
            if (!IsWindows || !IsWindowVisible(h) || IsIconic(h)) return false;
            WRect r;
            try { if (DwmGetWindowAttributeRect(h, 9, out r, Marshal.SizeOf(typeof(WRect))) == 0) { bounds = r.ToRectangle(); return true; } } catch { }
            if (!GetWindowRect(h, out r)) return false;
            bounds = r.ToRectangle(); return true;
        }
        internal static bool TopVisible(IntPtr h, double x, int top)
        {
            if (TestProvider != null) return true;
            try { var hit = WindowFromPoint(new WPoint((int)x, top + 5)); return hit == h || GetAncestor(hit, 2) == h; } catch { return false; }
        }
        internal static bool FullscreenActive()
        {
            if (!IsWindows) return false;
            try
            {
                IntPtr h = GetForegroundWindow(); uint pid; GetWindowThreadProcessId(h, out pid);
                if (pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id || IsZoomed(h)) return false;
                StringBuilder cls = new StringBuilder(128); GetClassName(h, cls, cls.Capacity);
                if (cls.ToString() == "Progman" || cls.ToString() == "WorkerW") return false;
                Rectangle r; if (!TryBounds(h, out r)) return false;
                Rectangle s = Screen.FromRectangle(r).Bounds;
                return r.Left <= s.Left + 2 && r.Top <= s.Top + 2 && r.Right >= s.Right - 2 && r.Bottom >= s.Bottom - 2;
            }
            catch { return false; }
        }
        [StructLayout(LayoutKind.Sequential)] private struct WRect { public int L, T, R, B; public Rectangle ToRectangle() { return Rectangle.FromLTRB(L, T, R, B); } }
        [StructLayout(LayoutKind.Sequential)] private struct WPoint { public int X, Y; internal WPoint(int x, int y) { X = x; Y = y; } }
        private delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out WRect r);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr h, StringBuilder text, int count);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr h, uint flag);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(WPoint point);
        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] private static extern int DwmGetWindowAttributeInt(IntPtr h, int a, out int v, int size);
        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] private static extern int DwmGetWindowAttributeRect(IntPtr h, int a, out WRect r, int size);
    }

    internal sealed partial class Pet
    {
        internal IntPtr SupportWindow;
        internal Rectangle SupportBounds;
        internal bool InputHeld;
        internal PetState AfterReaction = PetState.Walk;
        internal int ClickCycle;
        private double extraDuration = 6;
        private double nextIdle, nextClimb, mouseGrace, nextEscape, hoverSince = -1;
        private bool hadNearMouse;
        private int wallSide;
        private bool wallDescending;
        private IntPtr approachWindow;
        private int approachSide;
        private double lookJitterX;
        private bool focusWasOn;
        private struct DragSample
        {
            internal double Time, X, Y;
            internal DragSample(double time, double x, double y) { Time = time; X = x; Y = y; }
        }
        private readonly List<DragSample> dragSamples = new List<DragSample>();
        private double throwVx, throwVy, throwDuration, helicopterDrift;
        internal void InitializeFeatures(IWorld w)
        {
            nextIdle = w.Now + 15 + w.Rng.NextDouble() * 15;
            nextClimb = w.Now + 25 + w.Rng.NextDouble() * 20;
            mouseGrace = w.Now + 1;
            // The first drop is fully visible, including the rotor.
            Y = Math.Max(Y, Area.Top + FeatureArt.Helicopter[0].H + 5);
        }
        private void DelayIdle(IWorld w)
        {
            nextIdle = w.Now + 20 + w.Rng.NextDouble() * 30;
            nextClimb = Math.Max(nextClimb, w.Now + 15);
        }
        private void ClearAttachments()
        {
            SupportWindow = IntPtr.Zero; approachWindow = IntPtr.Zero; wallDescending = false;
        }
        internal void ReleaseFeatures(IWorld w)
        {
            ClearAttachments(); hoverSince = -1; hadNearMouse = false; mouseGrace = w.Now + .8;
            throwVx = throwVy = helicopterDrift = 0;
            pilotFlight = false;
        }
        private void BeginThrowTracking(double now, double x, double y)
        { dragSamples.Clear(); TrackDrag(now, x, y); }
        private void TrackDrag(double now, double x, double y)
        {
            var sample = new DragSample(now, x, y);
            int last = dragSamples.Count - 1;
            if (last >= 0 && now - dragSamples[last].Time < .0001) dragSamples[last] = sample;
            else dragSamples.Add(sample);
            while (dragSamples.Count > 1 && now - dragSamples[0].Time > .12) dragSamples.RemoveAt(0);
        }
        private bool TryThrow(IWorld w)
        {
            if (dragSamples.Count < 2) { dragSamples.Clear(); return false; }
            var first = dragSamples[0]; var last = dragSamples[dragSamples.Count - 1];
            double dt = last.Time - first.Time;
            double vx = (last.X - first.X) / Math.Max(.008, dt), vy = (last.Y - first.Y) / Math.Max(.008, dt);
            dragSamples.Clear();
            double speed = Math.Sqrt(vx * vx + vy * vy);
            if (dt < .008 || speed < 300 * S) return false;
            double limit = Math.Min(1, 1600 * S / speed);
            throwVx = vx * limit * .7;
            throwVy = Math.Max(-700 * S, Math.Min(700 * S, vy * limit * .7 - 80 * S));
            // A floor toss lifts off before the rotor catches it.
            if (Y > Area.Bottom - 24 * S) throwVy = Math.Min(throwVy, -240 * S);
            double half = Sprites.Walk1.W * .75;
            X = Math.Max(Area.Left + half, Math.Min(Area.Right - half, X));
            Y = Math.Max(Area.Top + FeatureArt.Helicopter[0].H + 1, Math.Min(Area.Bottom - Sprites.Walk1.H * .25, Y));
            throwDuration = .45 + .15 * Math.Min(1, Math.Max(0, -throwVy / (600 * S)));
            if (Math.Abs(throwVx) > 25 * S) Dir = throwVx < 0 ? -1 : 1;
            RegisterSecretThrow(w);
            SetState(PetState.Thrown, w);
            DelayIdle(w); return true;
        }
        private void OpenHelicopter(IWorld w)
        {
            double half = FeatureArt.Helicopter[0].W / 2.0 + 1;
            X = Math.Max(Area.Left + half, Math.Min(Area.Right - half, X));
            Y = Math.Max(Area.Top + FeatureArt.Helicopter[0].H + 1, Math.Min(Area.Bottom - 1, Y));
            Vy = Math.Max(0, Math.Min(95 * S * PlayOptions.Current.FallSpeed, throwVy * .25));
            helicopterDrift = throwVx * .45; throwVx = throwVy = 0;
            if (pilotFlight) { Vy = 0; SetState(PetState.PilotRise, w); }
            else SetState(PetState.Falling, w);
        }
        private void AdvanceThrow(IWorld w, double dt)
        {
            double oldY = Y;
            throwVy += 900 * S * dt;
            double nx = X + throwVx * dt, ny = Y + throwVy * dt;
            throwVx *= Math.Exp(-1.6 * dt);
            Area = FindArea(w, nx, ny);
            double half = Sprites.Walk1.W * .75;
            bool edge = nx < Area.Left + half || nx > Area.Right - half;
            X = Math.Max(Area.Left + half, Math.Min(Area.Right - half, nx));
            double top = Area.Top + FeatureArt.Helicopter[0].H + 1;
            bool hitTop = ny < top;
            Y = Math.Max(top, Math.Min(Area.Bottom - 1, ny));
            bool nearFloor = throwVy > 0 && ny >= Area.Bottom - 10 * S;
            bool nearWindow = false;
            if (throwVy > 0 && PlayOptions.Current.Windows && !PlayOptions.IsFocus(DateTime.Now))
            {
                foreach (var win in DesktopSurfaces.Cached)
                {
                    Rectangle r;
                    if (!DesktopSurfaces.TryBounds(win.Handle, out r) || r.Top < oldY || r.Top > ny + 18 * S || X < r.Left + HalfW || X > r.Right - HalfW || r.Top < top || !DesktopSurfaces.TopVisible(win.Handle, X, r.Top)) continue;
                    Y = Math.Min(Y, r.Top - 1); nearWindow = true;
                }
            }
            if (edge) throwVx = -throwVx * .2;
            if (hitTop) throwVy = Math.Max(0, -throwVy * .2);
            if (StateT >= throwDuration || edge || hitTop || nearFloor || nearWindow) OpenHelicopter(w);
        }
        internal void ClickFeature(IWorld w)
        {
            if (State == PetState.Drag || State == PetState.Falling || State == PetState.Thrown || State == PetState.PilotRise) return;
            mouseGrace = w.Now + .7; hoverSince = -1;
            if (State == PetState.Sleep) { SetState(PetState.Waking, w); return; }
            if (State == PetState.Gather || State == PetState.WaitHome || State == PetState.Cheer) return;
            if (PlayOptions.Current.ClickMode == ClickReaction.None) return;
            PetState resume = State == PetState.Wall || State == PetState.Ceiling || State == PetState.WindowClimb ? State : PetState.Walk;
            if (State == PetState.TouchCry || State == PetState.Hop || State == PetState.Squish) resume = AfterReaction;
            AfterReaction = resume;
            int choice;
            bool hungry = FriendData.Current.Hungry;
            switch (PlayOptions.Current.ClickMode)
            {
                case ClickReaction.Random: choice = w.Rng.Next(hungry ? 3 : 2); if (!hungry && choice == 1) choice = 2; break;
                case ClickReaction.Squash: choice = 0; break;
                case ClickReaction.Cry: choice = hungry ? 1 : 0; break;
                case ClickReaction.Hop: choice = 2; break;
                default: choice = hungry ? ClickCycle++ % 3 : (ClickCycle++ % 2 == 0 ? 0 : 2); break;
            }
            if (choice == 0) StartSquish(w, .42, .24, resume, false);
            else if (choice == 1) { extraDuration = 1.6; SetState(PetState.TouchCry, w); }
            else { extraDuration = .7; SetState(PetState.Hop, w); }
            DelayIdle(w);
            FriendData.Current.Like(1);
        }
        internal void Feed(IWorld w)
        {
            if (State == PetState.Drag || State == PetState.Falling || State == PetState.Thrown || State == PetState.PilotRise) { Bubble = "내려가면 간식 먹자!"; BubbleUntil = w.Now + 3; return; }
            if (State == PetState.Wall || State == PetState.Ceiling || State == PetState.WindowClimb) { ClearAttachments(); SetState(PetState.Falling, w); return; }
            if (!FriendData.Current.TrySnack())
            { Bubble = "배가 꽉 찼어! 조금 출출해지면 먹자"; BubbleUntil = w.Now + 3; return; }
            AfterReaction = PetState.Walk; extraDuration = 3;
            SetState(PetState.Eating, w); Bubble = "냠냠! 고마워"; BubbleUntil = w.Now + 3;
            mouseGrace = w.Now + 5; DelayIdle(w);
        }
        internal void Demonstrate(IWorld w, string kind)
        {
            mouseGrace = w.Now + 8; hoverSince = -1;
            if (kind == "fall")
            {
                ReleaseFeatures(w); X = Area.Left + Area.Width * .5; Y = Area.Top + FeatureArt.Helicopter[0].H + 5; Vy = 0; SetState(PetState.Falling, w); return;
            }
            if (kind == "wall")
            {
                ClearAttachments(); wallSide = 1; wallDescending = false; X = Area.Right - FeatureArt.Climb[0].W * .5; Y = Area.Bottom; SetState(PetState.Wall, w); return;
            }
            if (kind == "ceiling")
            {
                ClearAttachments(); X = Area.Left + Area.Width * .5; Y = Area.Top + FeatureArt.CeilingContactHeight; Dir = 1; SetState(PetState.Ceiling, w); return;
            }
            if (kind == "window")
            {
                DesktopSurfaces.Refresh();
                var win = DesktopSurfaces.Cached.FirstOrDefault(a => a.Bounds.Top > Area.Top + Sprites.Walk1.H && a.Bounds.Top < Area.Bottom - 40);
                if (win != null)
                {
                    ClearAttachments(); SupportWindow = win.Handle; SupportBounds = win.Bounds; approachSide = win.Bounds.Right + FeatureArt.Climb[0].W < Area.Right ? 1 : -1;
                    X = approachSide == 1 ? win.Bounds.Right + FeatureArt.Climb[0].W / 2.0 : win.Bounds.Left - FeatureArt.Climb[0].W / 2.0;
                    Y = Math.Min(Area.Bottom, win.Bounds.Bottom); SetState(PetState.WindowClimb, w);
                }
                else { Bubble = "창 크기를 줄여서 띄워줘!"; BubbleUntil = w.Now + 4; }
                return;
            }
            if (State == PetState.Falling || State == PetState.Drag || State == PetState.Thrown) return;
            if (kind == "dizzy")
            { ClearAttachments(); SnapToFloor(w); StartDizzy(w); return; }
            AfterReaction = PetState.Walk;
            if ((kind == "cry" || kind == "weep") && !FriendData.Current.Hungry)
            { Bubble = "배고플 때만 눈물이 나와!"; BubbleUntil = w.Now + 3; return; }
            if (kind == "cry") { extraDuration = 3; SetState(PetState.TouchCry, w); }
            if (kind == "sit") { extraDuration = PlayOptions.Current.ShortRest ? 8 : 25; SetState(PetState.Resting, w); }
            if (kind == "weep") { extraDuration = 3; SetState(PetState.IdleCry, w); }
            if (kind == "hop") { extraDuration = .7; SetState(PetState.Hop, w); }
            if (kind == "slide") { extraDuration = .85; SetState(PetState.Sliding, w); }
            if (kind == "chase") { ClearAttachments(); SnapToFloor(w); SetState(PetState.Walk, w); stateDur = Math.Max(stateDur, 3.5); chaseUntil = w.Now + 3; }
            DelayIdle(w);
        }
        internal bool AdvanceFeatures(IWorld w, double dt)
        {
            var opts = PlayOptions.Current;
            chaseClock = w.Now;
            if (AdvanceSecrets(w, dt)) return true;
            bool focus = PlayOptions.IsFocus(DateTime.Now);
            if (focus && !focusWasOn && (SupportWindow != IntPtr.Zero || State == PetState.Wall || State == PetState.Ceiling || State == PetState.WindowClimb))
            {
                ClearAttachments(); SetState(PetState.Falling, w);
            }
            focusWasOn = focus;
            if ((State == PetState.Wall && !opts.Walls) || (State == PetState.Ceiling && !opts.Ceiling) || (State == PetState.WindowClimb && !opts.Windows))
            { ClearAttachments(); SetState(PetState.Falling, w); }
            if (SupportWindow != IntPtr.Zero && State != PetState.Falling && State != PetState.Drag)
            {
                Rectangle r;
                if (!DesktopSurfaces.TryBounds(SupportWindow, out r)) { ClearAttachments(); SetState(PetState.Falling, w); }
                else
                {
                    X += r.Left - SupportBounds.Left;
                    if (State != PetState.WindowClimb) Y = r.Top;
                    SupportBounds = r;
                    if (State != PetState.WindowClimb && (X < r.Left + 5 || X > r.Right - 5 || !DesktopSurfaces.TopVisible(SupportWindow, X, r.Top)))
                    { ClearAttachments(); Y += 3; SetState(PetState.Falling, w); }
                }
            }
            if (State == PetState.Falling) return false;
            if (State == PetState.Walk || State == PetState.Pause || State == PetState.Resting)
            {
                Point mouse = DesktopSurfaces.Mouse;
                bool near = Math.Abs(mouse.X - X) < Sprites.Walk1.W * .9 && mouse.Y >= Y - Sprites.Walk1.H - 15 * S && mouse.Y <= Y + 8 * S;
                if (near && !hadNearMouse) hoverSince = w.Now;
                if (!near) hoverSince = -1;
                hadNearMouse = near;
                if (!focus && opts.Escape && near && hoverSince >= 0 && w.Now - hoverSince > opts.EscapeDelay && w.Now > mouseGrace && w.Now > nextEscape && !CirclingMouse(w.Now) && !InputHeld && !DesktopSurfaces.MousePressed)
                {
                    Dir = mouse.X < X ? 1 : -1;
                    if (X > Area.Right - Sprites.Walk1.W * 2) Dir = -1;
                    if (X < Area.Left + Sprites.Walk1.W * 2) Dir = 1;
                    extraDuration = .7 + w.Rng.NextDouble() * .25;
                    SetState(PetState.Sliding, w); nextEscape = w.Now + 2.5; hoverSince = -1; DelayIdle(w);
                }
                else if (!focus && opts.Follow && !opts.Escape && w.Now > mouseGrace && Math.Abs(mouse.Y - Y) < 140 * S && !InputHeld && Math.Abs(mouse.X - X) > Sprites.Walk1.W)
                {
                    Dir = mouse.X < X ? -1 : 1;
                    chaseUntil = Math.Max(chaseUntil, w.Now + .25);
                }
                if (nextChase == 0) nextChase = w.Now + 25 + w.Rng.NextDouble() * 20;
                if (!focus && State == PetState.Walk && SupportWindow == IntPtr.Zero && approachWindow == IntPtr.Zero && w.Now > nextChase)
                { nextChase = w.Now + 45 + w.Rng.NextDouble() * 45; chaseUntil = w.Now + 2.2 + w.Rng.NextDouble(); }
                if (opts.Idle && w.Now >= nextIdle && !InputHeld && !near && (State == PetState.Walk || State == PetState.Pause))
                {
                    extraDuration = opts.ShortRest ? (focus ? 4 : 6 + w.Rng.NextDouble() * 5) : 20 + w.Rng.NextDouble() * 10;
                    if (!focus && FriendData.Current.Hungry) { extraDuration = 2.4 + w.Rng.NextDouble(); SetState(PetState.IdleCry, w); }
                    else SetState(PetState.Resting, w);
                    DelayIdle(w);
                }
                if (!focus && opts.Windows && State == PetState.Walk && SupportWindow == IntPtr.Zero && w.Now > nextClimb)
                {
                    nextClimb = w.Now + 35 + w.Rng.NextDouble() * 30;
                    var possible = DesktopSurfaces.Cached.Where(a => a.Bounds.Top >= Area.Top + 70 * S && a.Bounds.Top < Area.Bottom - 70 * S && a.Bounds.IntersectsWith(Area)).ToList();
                    if (possible.Count > 0)
                    {
                        var a = possible[w.Rng.Next(possible.Count)];
                        if (a.Bounds.Left - FeatureArt.Climb[0].W >= Area.Left) { approachWindow = a.Handle; approachSide = -1; }
                        else if (a.Bounds.Right + FeatureArt.Climb[0].W <= Area.Right) { approachWindow = a.Handle; approachSide = 1; }
                    }
                }
                if (approachWindow != IntPtr.Zero && State == PetState.Walk)
                {
                    Rectangle r;
                    if (!DesktopSurfaces.TryBounds(approachWindow, out r)) approachWindow = IntPtr.Zero;
                    else
                    {
                        double target = approachSide < 0 ? r.Left - FeatureArt.Climb[0].W / 2.0 : r.Right + FeatureArt.Climb[0].W / 2.0;
                        if (Math.Abs(target - X) <= WalkSpeed * opts.Speed * dt + 2)
                        { X = target; SupportWindow = approachWindow; SupportBounds = r; approachWindow = IntPtr.Zero; SetState(PetState.WindowClimb, w); }
                        else Dir = target < X ? -1 : 1;
                    }
                }
            }
            if (State < PetState.Resting) return false;
            StateT += dt; animT += dt;
            if (Bubble != null && w.Now > BubbleUntil) Bubble = null;
            switch (State)
            {
                case PetState.Thrown:
                    AdvanceThrow(w, dt);
                    break;
                case PetState.Resting:
                case PetState.IdleCry:
                    if (StateT >= extraDuration) { SetState(PetState.Walk, w); DelayIdle(w); }
                    break;
                case PetState.TouchCry:
                case PetState.Hop:
                case PetState.Eating:
                    if (StateT >= extraDuration) SetState(AfterReaction, w);
                    break;
                case PetState.Sliding:
                    if (StateT >= extraDuration) { SetState(PetState.Walk, w); break; }
                    if (SupportWindow != IntPtr.Zero) WalkOnWindow(w, dt, 210 * S * opts.SlideSpeed);
                    else { X += Dir * 210 * S * opts.SlideSpeed * dt; X = Math.Max(Area.Left + HalfW, Math.Min(Area.Right - HalfW, X)); }
                    break;
                case PetState.Wall:
                    X = wallSide < 0 ? Area.Left + FeatureArt.Climb[0].W / 2.0 : Area.Right - FeatureArt.Climb[0].W / 2.0;
                    Y += (wallDescending ? 1 : -1) * 42 * S * opts.Speed * dt;
                    if (!wallDescending && Y <= Area.Top + FeatureArt.Climb[0].H + 3)
                    {
                        if (opts.Ceiling && !focus)
                        {
                            double half = FeatureArt.Ceiling[0].W / 2.0;
                            X = Math.Max(Area.Left + half, Math.Min(Area.Right - half, X));
                            Y = Area.Top + FeatureArt.CeilingContactHeight; Dir = -wallSide; SetState(PetState.Ceiling, w);
                        }
                        else { ClearAttachments(); SetState(PetState.Falling, w); }
                    }
                    else if (wallDescending && Y >= Area.Bottom) { Y = Area.Bottom; Dir = -wallSide; StartSquish(w, .28, .13, PetState.Walk, false); }
                    break;
                case PetState.Ceiling:
                    Y = Area.Top + FeatureArt.CeilingContactHeight;
                    X += Dir * 36 * S * opts.Speed * dt;
                    if (StateT > 14 && w.Rng.NextDouble() < dt * .08) { ClearAttachments(); SetState(PetState.Falling, w); }
                    else if ((Dir < 0 && X <= Area.Left + FeatureArt.Ceiling[0].W / 2.0) || (Dir > 0 && X >= Area.Right - FeatureArt.Ceiling[0].W / 2.0))
                    {
                        if (!opts.Walls) { ClearAttachments(); SetState(PetState.Falling, w); }
                        else
                        {
                            wallSide = Dir; wallDescending = true;
                            X = wallSide < 0 ? Area.Left + FeatureArt.Climb[0].W / 2.0 : Area.Right - FeatureArt.Climb[0].W / 2.0;
                            Y = Area.Top + FeatureArt.Climb[0].H + 1; SetState(PetState.Wall, w);
                        }
                    }
                    break;
                case PetState.WindowClimb:
                    Rectangle rect;
                    if (SupportWindow == IntPtr.Zero || !DesktopSurfaces.TryBounds(SupportWindow, out rect)) { ClearAttachments(); SetState(PetState.Falling, w); break; }
                    X = approachSide < 0 ? rect.Left - FeatureArt.Climb[0].W / 2.0 : rect.Right + FeatureArt.Climb[0].W / 2.0;
                    Y -= 46 * S * opts.Speed * dt;
                    if (Y <= rect.Top + FeatureArt.Climb[0].H * .55)
                    { Y = rect.Top; X = approachSide < 0 ? rect.Left + HalfW + 8 : rect.Right - HalfW - 8; Dir = -approachSide; extraDuration = 5 + w.Rng.NextDouble() * 5; SetState(PetState.Resting, w); }
                    break;
            }
            return true;
        }
        internal bool BoundaryFeature(IWorld w, double nextX)
        {
            if (!PlayOptions.Current.Walls || PlayOptions.IsFocus(DateTime.Now) || State != PetState.Walk || SupportWindow != IntPtr.Zero || approachWindow != IntPtr.Zero) return false;
            bool hit = Dir > 0 ? nextX + HalfW >= Area.Right : nextX - HalfW <= Area.Left;
            if (!hit) return false;
            double outside = Dir > 0 ? Area.Right + 1 : Area.Left - 1;
            if (w.Areas.Any(r => r != Area && outside >= r.Left && outside < r.Right && Math.Abs(r.Bottom - Y) < 4)) return false;
            wallSide = Dir; wallDescending = false; SetState(PetState.Wall, w); return true;
        }
        internal bool WalkOnWindow(IWorld w, double dt, double speed)
        {
            if (SupportWindow == IntPtr.Zero) return false;
            Rectangle r;
            if (!DesktopSurfaces.TryBounds(SupportWindow, out r)) { ClearAttachments(); SetState(PetState.Falling, w); return true; }
            double nx = X + Dir * speed * dt;
            if (nx <= r.Left + HalfW || nx >= r.Right - HalfW)
            {
                Dir = -Dir; nx = Math.Max(r.Left + HalfW + 2, Math.Min(r.Right - HalfW - 2, nx));
                if (State == PetState.Walk && w.Rng.NextDouble() < .35) SetState(PetState.Pause, w);
            }
            X = nx; Y = r.Top; return true;
        }
        internal bool SurfaceFloor(IWorld w)
        {
            if (SupportWindow == IntPtr.Zero) return false;
            Rectangle r; if (!DesktopSurfaces.TryBounds(SupportWindow, out r)) { ClearAttachments(); SetState(PetState.Falling, w); return true; }
            Y = r.Top; return true;
        }
        internal void HelicopterDrop(IWorld w, double dt)
        {
            double oldY = Y;
            if (Math.Abs(helicopterDrift) > .1)
            {
                double half = FeatureArt.Helicopter[0].W / 2.0 + 1;
                X = Math.Max(Area.Left + half, Math.Min(Area.Right - half, X + helicopterDrift * dt));
                helicopterDrift *= Math.Exp(-4 * dt);
            }
            double speed = PlayOptions.Current.FallSpeed;
            Vy = Math.Min(95 * S * speed, Math.Max(35 * S * speed, Vy + 55 * S * speed * dt));
            Y += Vy * dt;
            Area = FindArea(w, X, Math.Min(Y, Area.Bottom));
            SurfaceWindow land = null;
            if (PlayOptions.Current.Windows && !PlayOptions.IsFocus(DateTime.Now))
            {
                foreach (var win in DesktopSurfaces.Cached)
                {
                    Rectangle r;
                    if (!DesktopSurfaces.TryBounds(win.Handle, out r)) continue;
                    if (r.Top < oldY - .5 || r.Top > Y || X < r.Left + HalfW || X > r.Right - HalfW || r.Top < Area.Top + Sprites.Walk1.H || !DesktopSurfaces.TopVisible(win.Handle, X, r.Top)) continue;
                    if (land == null || r.Top < land.Bounds.Top) land = new SurfaceWindow(win.Handle, r);
                }
            }
            if (land != null)
            {
                SupportWindow = land.Handle; SupportBounds = land.Bounds; Y = land.Bounds.Top; Vy = 0; helicopterDrift = 0;
                StartSquish(w, .3, .15, queuedAfterLand ?? PetState.Walk, false); queuedAfterLand = null; DelayIdle(w); mouseGrace = w.Now + .8;
            }
            else if (Y >= Area.Bottom)
            {
                Y = Area.Bottom; Vy = 0; helicopterDrift = 0; ClearAttachments();
                StartSquish(w, .3, .15, queuedAfterLand ?? PetState.Walk, false); queuedAfterLand = null; DelayIdle(w); mouseGrace = w.Now + .8;
            }
        }
        internal void FeatureLook(ref Look look, double now)
        {
            lookJitterX = 0;
            int frame = (int)(animT / .09) % 12;
            if (State == PetState.Thrown)
            {
                look.Sprite = FeatureArt.Held[frame]; look.Sx = look.Sy = 1; look.HopY = 0;
                look.Px = 0; look.Py = -look.Sprite.H * .5f;
                look.Angle = (float)(Math.Max(-32, Math.Min(32, throwVx / (900 * S) * 30)) + Math.Sin(StateT * 13) * 6);
            }
            else if (State == PetState.Falling || State == PetState.PilotRise)
            { look.Sprite = FeatureArt.Helicopter[(int)(animT / .045) % FeatureArt.Helicopter.Length]; look.Left = false; look.Sx = look.Sy = 1; }
            else if (State == PetState.TouchCry)
            { look.Sprite = FriendData.Current.Hungry ? FeatureArt.TouchCry : FeatureArt.Sit; look.Left = false; if (FriendData.Current.Hungry) { lookJitterX = Math.Sin(StateT * 52) * .6 * S; look.HopY = (float)(Math.Sin(StateT * 40) * .4 * S); } }
            else if (State == PetState.IdleCry)
            { look.Sprite = FriendData.Current.Hungry ? FeatureArt.IdleCry : FeatureArt.Sit; look.Left = false; look.Sy = (float)(1 + Math.Sin(StateT * 8) * .012); if (FriendData.Current.Hungry) lookJitterX = Math.Sin(StateT * 21) * .45 * S; }
            else if (State == PetState.Resting || State == PetState.Eating)
            { look.Sprite = State == PetState.Eating ? FeatureArt.Eating[frame % 4] : FeatureArt.Sit; look.Sy = (float)(1 + Math.Sin(animT * 2.0) * .012); }
            else if (State == PetState.Hop)
            {
                look.Sprite = Sprites.WalkingFrame(animT); double t = StateT / extraDuration;
                if (t < .16) { look.Sy = (float)(1 - .14 * Math.Sin(t / .16 * Math.PI)); look.Sx = 1.06f; }
                else { double q = Math.Min(1, (t - .16) / .84); look.HopY = (float)(-Math.Sin(q * Math.PI) * 22 * S); }
            }
            else if (State == PetState.Sliding)
            { look.Sprite = FeatureArt.Slide; look.Sy = .82f; look.HopY = (float)(Math.Sin(animT * 20) * .6 * S); }
            else if (State == PetState.Wall || State == PetState.WindowClimb || State == PetState.Climb)
            {
                look.Sprite = (State == PetState.Wall && wallDescending ? FeatureArt.ClimbDown : FeatureArt.Climb)[frame];
                look.Left = State == PetState.WindowClimb ? approachSide > 0 : State == PetState.Climb ? climbDir < 0 : wallSide < 0;
            }
            else if (State == PetState.Ceiling)
            { look.Sprite = FeatureArt.Ceiling[frame]; look.Left = Dir < 0; }
            else if (State == PetState.Drag) { look.Sprite = FeatureArt.Held[frame]; }
            else if (Chasing)
            { look.Sprite = FeatureArt.Chase; look.Sx = look.Sy = 1; look.HopY = (float)(-Math.Abs(Math.Sin(animT * 13)) * 3 * S); }
        }
        internal double FeatureJitterX { get { return lookJitterX; } }
        // Running after the fish: a short fast dash now and then, and whenever the friend follows the cursor.
        private double chaseUntil = -1, nextChase, chaseClock;
        internal bool Chasing { get { return State == PetState.Walk && chaseClock < chaseUntil; } }
    }

    internal sealed partial class App
    {
        private bool autoHidden;
        private double surfaceRefreshAt;
        private PlaySettingsForm playSettingsForm;
        private FriendPanel friendPanel;
        internal bool FeatureTick(double now)
        {
            if (now - surfaceRefreshAt > .35) { surfaceRefreshAt = now; DesktopSurfaces.Refresh(); }
            if (hidden || paused) return false;
            bool hide = now >= forceVisibleUntil && PlayOptions.Current.Fullscreen && DesktopSurfaces.FullscreenActive();
            if (hide != autoHidden)
            {
                autoHidden = hide;
                foreach (var w in windows)
                {
                    w.Pet.Hidden = hide;
                    if (hide) w.Hide(); else { w.Show(); w.Invalidate2(); w.Present(now); w.KeepOnTop(); }
                }
            }
            FriendData.Current.PollTimer(this);
            return !autoHidden;
        }
        internal void ShowPlaySettings()
        {
            if (playSettingsForm != null && !playSettingsForm.IsDisposed) { playSettingsForm.Activate(); return; }
            playSettingsForm = new PlaySettingsForm(this); playSettingsForm.FormClosed += delegate { playSettingsForm = null; }; playSettingsForm.Show();
        }
        internal void ShowFriendPanel()
        {
            if (friendPanel != null && !friendPanel.IsDisposed) { friendPanel.Activate(); return; }
            friendPanel = new FriendPanel(this); friendPanel.FormClosed += delegate { friendPanel = null; }; friendPanel.Show();
        }
        internal void Announce(string text)
        {
            foreach (var w in windows) { w.Pet.Bubble = text; w.Pet.BubbleUntil = Now + 5; }
        }
        internal void AddFeatureMenu(ContextMenuStrip menu, Pet pet)
        {
            menu.Items.Insert(0, new ToolStripMenuItem("♥ 친밀도 " + FriendData.Current.Affinity.ToString("N0") + "  ·  함께 " + FriendData.FormatTogether(FriendData.Current.TogetherSeconds)) { Enabled = false });
            int at = Math.Max(0, menu.Items.Count - 1);
            menu.Items.Insert(at++, new ToolStripMenuItem("화면 가운데로 다시 부르기", null, delegate { RecoverVisible(); }));
            var features = new ToolStripMenuItem("움직임 · 반응 설정...", null, delegate { ShowPlaySettings(); });
            menu.Items.Insert(at++, features);
            menu.Items.Insert(at++, new ToolStripMenuItem("친구 기록 · 할 일 · 메모...", null, delegate { ShowFriendPanel(); }));
            menu.Items.Insert(at++, new ToolStripMenuItem("간식 주기  ·  포만감 " + (int)Math.Floor(FriendData.Current.Satiety) + "/100", null, delegate
            {
                Pet p = pet ?? windows.Select(a => a.Pet).FirstOrDefault(); if (p != null) p.Feed(this);
            }));
            var demo = new ToolStripMenuItem("동작 바로 보기");
            foreach (var pair in new[] { new[] { "앉아서 쉬기", "sit" }, new[] { "배고플 때 우는 모습", "cry" }, new[] { "폴짝 뛰기", "hop" }, new[] { "슬라이딩", "slide" }, new[] { "물고기 쫓아가기", "chase" }, new[] { "어질어질", "dizzy" }, new[] { "둥실둥실 내려오기", "fall" }, new[] { "벽 타기", "wall" }, new[] { "천장 타기", "ceiling" }, new[] { "창 타기", "window" } })
            {
                string key = pair[1]; demo.DropDownItems.Add(pair[0], null, delegate { Pet p = pet ?? windows.Select(a => a.Pet).FirstOrDefault(); if (p != null) p.Demonstrate(this, key); });
            }
            demo.DropDownItems.Add("몰래 하는 군무", null, delegate { DemonstrateSecretDance(pet); });
            menu.Items.Insert(at++, demo);
            var focus = new ToolStripMenuItem("얌전 모드 (바닥에서만)"); focus.Checked = PlayOptions.Current.Focus;
            focus.Click += delegate { PlayOptions.Current.Focus = !PlayOptions.Current.Focus; PlayOptions.Current.Save(); focus.Checked = PlayOptions.Current.Focus; };
            menu.Items.Insert(at++, focus);
            menu.Items.Insert(at++, new ToolStripSeparator());
        }
        internal void CloseFeatureWindows()
        {
            FriendData.Current.Save();
            if (playSettingsForm != null) playSettingsForm.Close();
            if (friendPanel != null) friendPanel.Close();
        }
    }
}
