using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows.Forms;

namespace WalkingFriend
{
    internal static class FeatureLog
    {
        private static DateTime last;
        internal static void Write(Exception error)
        {
            if ((DateTime.UtcNow - last).TotalSeconds < 2) return; last = DateTime.UtcNow;
            try { Directory.CreateDirectory(Settings.AltDir); string p = Path.Combine(Settings.AltDir, "걸어다니는친구_오류.log"); if (File.Exists(p) && new FileInfo(p).Length > 2000000) File.WriteAllText(p, ""); File.AppendAllText(p, DateTime.Now.ToString("s") + " " + error + Environment.NewLine); } catch { }
        }
    }
    internal sealed class CheckWorld : IWorld
    {
        public List<Rectangle> Areas { get; private set; }
        public Random Rng { get; private set; }
        public double Now { get; set; }
        internal CheckWorld() { Areas = new List<Rectangle> { new Rectangle(0, 0, 640, 400) }; Rng = new Random(271); }
        internal void Step(Pet p, double secs)
        { for (int i = 0; i < (int)Math.Ceiling(secs / .02); i++) { Now += .02; p.Update(this, .02, new List<Pet> { p }, DesktopSurfaces.Mouse.X); } }
    }
    internal static class UpgradeChecks
    {
        private static List<string> results = new List<string>();
        internal static void Check(bool b, string message)
        { if (!b) throw new InvalidOperationException("CHECK FAILED: " + message); results.Add("PASS " + message); Console.WriteLine("PASS " + message); }
        internal static void Run(string dir)
        {
            Directory.CreateDirectory(dir); FriendData.StorageOverride = Path.Combine(dir, "data"); FriendData.Name = "regression-check.xml"; FriendData.Current = new FriendData(); DesktopSurfaces.TestMouse = delegate { return new Point(-5000, -5000); }; DesktopSurfaces.TestProvider = delegate { return new List<SurfaceWindow>(); };
            PlayOptions.Current = new PlayOptions { Walls = false, Ceiling = false, Windows = false, Idle = false, Escape = false };
            Sprites.Load(1.25f);
            Capture(dir);
            var world = new CheckWorld(); var p = new Pet(world, 300, 0);
            double start = p.Y; world.Step(p, .2); Check(p.State == PetState.Falling && p.Y - start < 30, "initial helicopter drop is slow");
            world.Step(p, 6); Check(p.Y == 400 && p.State != PetState.Falling, "helicopter lands on floor");
            p.Poke(world); Check(p.State == PetState.Squish, "first click preserves squash reaction"); world.Step(p, .6);
            p.Poke(world); Check(p.State == PetState.Hop, "second fed click chooses hop without crying");
            p.Poke(world); Check(p.State == PetState.Squish, "third fed click cycles back to squash"); world.Step(p, 1);
            p.Poke(world); Check(p.State == PetState.Hop, "fed click reactions alternate squash and hop"); world.Step(p, .8);
            p.BeginDrag(world, p.X, p.Y - 20); p.DragTo(world, 350, 130); p.EndDrag(world); Check(p.State == PetState.Falling, "release from hand starts helicopter drop");
            start = p.Y; world.Step(p, .2); Check(p.Y - start < 30, "release uses the same slow drop as startup"); world.Step(p, 6);
            FriendData.Current.Satiety = 100; PlayOptions.Current.Idle = true; bool rested = false, wept = false;
            for (int i = 0; i < 12000; i++) { world.Step(p, .02); rested |= p.State == PetState.Resting; wept |= p.State == PetState.IdleCry; }
            Check(rested, "autonomous behavior still includes sitting when fed");
            FriendData.Current.Satiety = 10; world.Step(p, 60); wept = p.GetLook(world.Now).Sprite == FeatureArt.IdleCry;
            Check(wept, "hunger keeps the crying expression during autonomous behavior"); FriendData.Current.Satiety = 100;
            PlayOptions.Current.Idle = false; PlayOptions.Current.Escape = true; p.SetState(PetState.Walk, world); p.X = 300; p.Y = 400;
            DesktopSurfaces.TestMouse = delegate { return new Point((int)p.X + 4, (int)p.Y - 25); };
            p.InputHeld = true; world.Step(p, 1); Check(p.State != PetState.Sliding, "holding mouse prevents escape during interaction"); p.InputHeld = false;
            world.Step(p, .2); Check(p.State == PetState.Sliding, "hover triggers sliding promptly after holding ends"); world.Step(p, 2); Check(p.X >= 0 && p.X <= 640, "sliding stays within monitor");
            DesktopSurfaces.TestMouse = delegate { return new Point(-5000, -5000); }; PlayOptions.Current.Escape = false;
            PlayOptions.Current.Walls = PlayOptions.Current.Ceiling = true;
            p.SetState(PetState.Walk, world); p.X = 610; p.Y = 400; p.Dir = 1; world.Step(p, .4); Check(p.State == PetState.Wall, "walking into screen edge starts wall climb");
            world.Step(p, 8); Check(p.State == PetState.Ceiling, "wall climb transitions to ceiling");
            PlayOptions.Current.Focus = true; world.Step(p, .02); Check(p.State == PetState.Falling, "focus mode returns ceiling pet to floor"); world.Step(p, 6); PlayOptions.Current.Focus = false;
            var windows = new List<SurfaceWindow> { new SurfaceWindow(new IntPtr(7), new Rectangle(200, 150, 250, 180)) };
            DesktopSurfaces.TestProvider = delegate { return windows; }; DesktopSurfaces.Refresh(); PlayOptions.Current.Windows = true;
            p.Demonstrate(world, "window"); world.Step(p, 4); Check(p.SupportWindow != IntPtr.Zero && p.Y == 150, "pet climbs onto open window top");
            double oldX = p.X; windows[0].Bounds = new Rectangle(250, 180, 250, 180); DesktopSurfaces.Refresh(); world.Step(p, .02);
            Check(Math.Abs(p.X - oldX - 50) < 2 && p.Y == 180, "pet follows window movement");
            windows.Clear(); DesktopSurfaces.Refresh(); world.Step(p, .02); Check(p.SupportWindow == IntPtr.Zero && p.State == PetState.Falling, "closing support window releases pet safely");
            world.Step(p, 6);
            PlayOptions.Current.Focus = true; DesktopSurfaces.TestProvider = delegate { return new List<SurfaceWindow> { new SurfaceWindow(new IntPtr(8), new Rectangle(100, 220, 400, 100)) }; }; DesktopSurfaces.Refresh();
            p.BeginDrag(world, p.X, p.Y - 15); p.DragTo(world, 300, 100); p.EndDrag(world); world.Step(p, 6); Check(p.SupportWindow == IntPtr.Zero && p.Y == 400, "focus mode ignores window tops when falling");
            PlayOptions.Current.Focus = false;
            FriendData.Current.Satiety = 70; p.Feed(world); Check(p.State == PetState.Eating && FriendData.Current.Affinity >= 5, "feeding responds and increases saved affinity"); world.Step(p, 4);
            var s = new Settings { HourlyOn = true, QuietWeekdays = true, QuietStart = 9, QuietEnd = 18 };
            Check(!s.HourAllowed(new DateTime(2026, 10, 13, 9, 0, 0)) && !s.HourAllowed(new DateTime(2026, 10, 13, 18, 0, 0)) && s.HourAllowed(new DateTime(2026, 10, 13, 19, 0, 0)) && s.HourAllowed(new DateTime(2026, 10, 17, 9, 0, 0)), "existing weekday hourly exclusions retain both endpoints");
            using (Bitmap b = new Bitmap(24, 24))
            {
                using (Graphics g = Graphics.FromImage(b)) { g.Clear(Color.Transparent); g.FillRectangle(Brushes.White, 5, 5, 14, 14); g.FillRectangle(Brushes.Black, 10, 10, 2, 2); }
                using (Bitmap thick = FeatureArt.OuterOutline((Bitmap)b.Clone())) Check(thick.GetPixel(4, 10).A > 0 && thick.GetPixel(10, 10).ToArgb() == b.GetPixel(10, 10).ToArgb() && thick.GetPixel(9, 10).ToArgb() == b.GetPixel(9, 10).ToArgb(), "outer contour thickening preserves interior eyes and fill");
            }
            Capture(dir);
            for (int i = 0; i < FeatureArt.Helicopter.Length; i++)
            { Check(FeatureArt.Helicopter[i].H > Sprites.Walk1.H && FeatureArt.Helicopter[i].W >= Sprites.Walk1.W, "helicopter frame " + i + " preserves body size"); }
            Check(FeatureArt.Helicopter[0].Right.GetPixel(0, 0).A == 0, "animation assets retain transparent background");
            CheckMotionOptions(dir);
            CheckCeilingAndThrow(dir);
            TestUi(dir);
            Check(File.ReadAllBytes(System.Reflection.Assembly.GetExecutingAssembly().Location).Take(2).SequenceEqual(new byte[] { 77, 90 }), "Windows executable header is valid");
            File.WriteAllLines(Path.Combine(dir, "checks.txt"), results.ToArray());
            Console.WriteLine("All " + results.Count + " checks passed.");
        }
        private static double Travel(PetState state, double walkSpeed, double fallSpeed, double slideSpeed)
        {
            PlayOptions.Current = new PlayOptions { Walls = false, Ceiling = false, Windows = false, Idle = false, Escape = false, Speed = walkSpeed, FallSpeed = fallSpeed, SlideSpeed = slideSpeed };
            var w = new CheckWorld(); var p = new Pet(w, 180, 0); p.Dir = 1;
            if (state != PetState.Falling) { p.Y = w.Areas[0].Bottom; p.SetState(PetState.Walk, w); }
            if (state == PetState.Sliding) p.Demonstrate(w, "slide");
            double start = state == PetState.Falling ? p.Y : p.X;
            w.Step(p, .2);
            return (state == PetState.Falling ? p.Y : p.X) - start;
        }
        private static void CheckMotionOptions(string dir)
        {
            var previous = PlayOptions.Current;
            string previousName = PlayOptions.Name;
            var culture = System.Threading.Thread.CurrentThread.CurrentCulture;
            PlayOptions.Name = "motion-check-" + Guid.NewGuid().ToString("N") + ".ini";
            try
            {
                double slowFall = Travel(PetState.Falling, 1, .5, 1), fastFall = Travel(PetState.Falling, 1, 2, 1);
                Check(fastFall > slowFall * 3.5, "fall speed setting changes helicopter descent");
                Check(Math.Abs(fastFall - Travel(PetState.Falling, 2, 2, .5)) < .01, "fall setting is independent of walking and sliding");
                double slowSlide = Travel(PetState.Sliding, 1, 1, .5), fastSlide = Travel(PetState.Sliding, 1, 1, 2);
                Check(fastSlide > slowSlide * 3.5, "slide speed setting changes escape motion");
                Check(Math.Abs(fastSlide - Travel(PetState.Sliding, .5, 3, 2)) < .01, "slide setting is independent of walking and falling");
                Check(Math.Abs(Travel(PetState.Walk, 1, .5, 3) - Travel(PetState.Walk, 1, 3, .5)) < .01, "fall and slide controls preserve walking speed");
                double oldFall = Travel(PetState.Falling, 1, 1, 1), defaultFall = Travel(PetState.Falling, 1, new PlayOptions().FallSpeed, 1);
                Check(defaultFall > oldFall * 1.15 && defaultFall < oldFall * 1.25, "default helicopter descent is slightly faster");

                var world = new CheckWorld(); var pet = new Pet(world, 300, 0); pet.Y = 400;
                PlayOptions.Current = new PlayOptions { Idle = false, Escape = false, Walls = false, Ceiling = false, Windows = false };
                var modes = new[] { ClickReaction.Squash, ClickReaction.Cry, ClickReaction.Hop };
                var states = new[] { PetState.Squish, PetState.TouchCry, PetState.Hop };
                for (int i = 0; i < modes.Length; i++)
                {
                    FriendData.Current.Satiety = modes[i] == ClickReaction.Cry ? 10 : 100;
                    PlayOptions.Current.ClickMode = modes[i]; pet.SetState(PetState.Walk, world); pet.Poke(world);
                    bool first = pet.State == states[i]; world.Step(pet, 2); pet.Poke(world);
                    Check(first && pet.State == states[i], "fixed click mode " + modes[i] + " stays selected on repeated clicks");
                }
                PlayOptions.Current.ClickMode = ClickReaction.Random;
                FriendData.Current.Satiety = 10;
                var seen = new HashSet<PetState>();
                for (int i = 0; i < 30; i++) { pet.SetState(PetState.Walk, world); pet.Poke(world); seen.Add(pet.State); }
                Check(seen.SetEquals(states), "random click mode uses all three reactions");
                FriendData.Current.Satiety = 100;
                PlayOptions.Current.ClickMode = ClickReaction.None; pet.SetState(PetState.Walk, world); pet.Poke(world);
                Check(pet.State == PetState.Walk, "disabled click reactions leave walking alone");
                PlayOptions.Current.Walls = PlayOptions.Current.Ceiling = true; pet.Demonstrate(world, "wall"); world.Step(pet, .1);
                var look = pet.GetLook(world.Now);
                Check(look.Sprite.H > look.Sprite.W && !look.Left && Math.Abs(pet.X + look.Sprite.W / 2.0 - 640) < 1, "right wall uses upright pose with paws against the edge");
                pet.SetState(PetState.Walk, world); pet.X = 30; pet.Y = 400; pet.Dir = -1; world.Step(pet, .2);
                look = pet.GetLook(world.Now);
                Check(pet.State == PetState.Wall && look.Left && Math.Abs(pet.X - look.Sprite.W / 2.0) < 1, "left wall mirrors the upright pose toward the edge");
                PlayOptions.Current.Windows = true;
                DesktopSurfaces.TestProvider = delegate { return new List<SurfaceWindow> { new SurfaceWindow(new IntPtr(21), new Rectangle(200, 150, 250, 180)) }; };
                pet.Demonstrate(world, "window"); world.Step(pet, .1); look = pet.GetLook(world.Now);
                Check(pet.State == PetState.WindowClimb && look.Sprite.H > look.Sprite.W && look.Left && Math.Abs(pet.X - look.Sprite.W / 2.0 - 450) < 1, "window side uses upright pose attached along the body");
                DesktopSurfaces.TestProvider = delegate { return new List<SurfaceWindow>(); }; DesktopSurfaces.Refresh();

                PlayOptions.Current = new PlayOptions();
                App app = (App)FormatterServices.GetUninitializedObject(typeof(App)); app.Settings = new Settings();
                using (var form = new PlaySettingsForm(app))
                {
                    form.Show(); Application.DoEvents();
                    ((NumericUpDown)form.Controls.Find("walkSpeed", true)[0]).Value = 1.4m;
                    ((NumericUpDown)form.Controls.Find("fallSpeed", true)[0]).Value = 2.3m;
                    ((NumericUpDown)form.Controls.Find("slideSpeed", true)[0]).Value = .7m;
                    ((ComboBox)form.Controls.Find("clickMode", true)[0]).SelectedIndex = (int)ClickReaction.Hop;
                    System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
                    ((Button)form.Controls.Find("save", true)[0]).PerformClick(); Application.DoEvents();
                }
                Check(PlayOptions.Current.Speed == 1.4 && PlayOptions.Current.FallSpeed == 2.3 && PlayOptions.Current.SlideSpeed == .7 && PlayOptions.Current.ClickMode == ClickReaction.Hop, "settings dialog applies each speed and selected click reaction");
                PlayOptions.Current = new PlayOptions(); PlayOptions.Load();
                Check(PlayOptions.Current.Speed == 1.4 && PlayOptions.Current.FallSpeed == 2.3 && PlayOptions.Current.SlideSpeed == .7 && PlayOptions.Current.ClickMode == ClickReaction.Hop, "speeds and click selection survive reloading with decimal-comma locale");
                string path = Path.Combine(Settings.ExeDir, PlayOptions.Name);
                File.WriteAllText(path, "Walls=0\nSpeed=1.4\n"); PlayOptions.Current = new PlayOptions(); PlayOptions.Load();
                Check(!PlayOptions.Current.Walls && PlayOptions.Current.Speed == 1.4 && PlayOptions.Current.FallSpeed == 1.2 && PlayOptions.Current.SlideSpeed == 1 && PlayOptions.Current.ClickMode == ClickReaction.Cycle, "existing settings retain preferences and receive new defaults");
                File.WriteAllText(path, "Speed=Infinity\nFallSpeed=NaN\nSlideSpeed=-100\nClickMode=unknown\n"); PlayOptions.Current = new PlayOptions(); PlayOptions.Load();
                Check(PlayOptions.Current.Speed == 1 && PlayOptions.Current.FallSpeed == 1.2 && PlayOptions.Current.SlideSpeed == .5 && PlayOptions.Current.ClickMode == ClickReaction.Cycle, "invalid settings fall back safely without blocking startup");
            }
            finally
            {
                foreach (string root in new[] { Settings.ExeDir, Settings.AltDir })
                { string path = Path.Combine(root, PlayOptions.Name); if (File.Exists(path)) File.Delete(path); }
                PlayOptions.Name = previousName; PlayOptions.Current = previous;
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            }
        }
        private static Pet Flick(CheckWorld world, double x, double y, double dx, double dy, double duration, double pause)
        {
            var p = new Pet(world, x, y); p.Y = y; p.SetState(PetState.Walk, world);
            p.BeginDrag(world, x, y - 20);
            for (int i = 1; i <= 6; i++)
            {
                world.Now += duration / 6;
                p.DragTo(world, x + dx * i / 6, y - 20 + dy * i / 6);
                p.Update(world, duration / 6, new List<Pet> { p }, x + dx * i / 6);
            }
            world.Now += pause; p.EndDrag(world); return p;
        }
        private static CheckWorld FlightWorld()
        { var w = new CheckWorld(); w.Areas.Clear(); w.Areas.Add(new Rectangle(0, 0, 1280, 720)); return w; }
        private static void CheckCeilingAndThrow(string dir)
        {
            var previous = PlayOptions.Current; var provider = DesktopSurfaces.TestProvider;
            try
            {
                PlayOptions.Current = new PlayOptions { Idle = false, Escape = false, Walls = true, Ceiling = true, Windows = false };
                DesktopSurfaces.TestProvider = delegate { return new List<SurfaceWindow>(); }; DesktopSurfaces.Refresh();
                var world = FlightWorld(); var p = new Pet(world, 600, 400); p.Demonstrate(world, "ceiling");
                var look = p.GetLook(world.Now);
                Check(look.Sprite.W > look.Sprite.H && FeatureArt.Climb[0].H > FeatureArt.Climb[0].W, "ceiling crawls horizontally while wall stays upright");
                bool touching = true;
                for (int i = 0; i < 12; i++)
                {
                    Rectangle ink = FeatureArt.Bounds(FeatureArt.Ceiling[i].Right);
                    double top = p.Y - FeatureArt.Ceiling[i].H + ink.Top;
                    touching &= Math.Abs(top - p.Area.Top) <= 1.5 * Sprites.Scale;
                }
                Check(touching, "ceiling animation stays attached to the upper edge");
                p.Demonstrate(world, "wall"); p.Y = p.Area.Top + FeatureArt.Climb[0].H + 1; world.Step(p, .02);
                Check(p.State == PetState.Ceiling && p.X + FeatureArt.Ceiling[0].W / 2.0 <= p.Area.Right && p.Y - FeatureArt.CeilingContactHeight == p.Area.Top, "wall to ceiling corner keeps the horizontal body visible and attached");
                p.X = p.Area.Right - FeatureArt.Ceiling[0].W / 2.0 - .2; p.Dir = 1; world.Step(p, .02);
                Check(p.State == PetState.Wall && p.Y - FeatureArt.Climb[0].H >= p.Area.Top, "ceiling to right wall turns upright without clipping the head");
                p.Demonstrate(world, "ceiling"); p.X = p.Area.Left + FeatureArt.Ceiling[0].W / 2.0 + .2; p.Dir = -1; world.Step(p, .02);
                Check(p.State == PetState.Wall && p.GetLook(world.Now).Left, "ceiling to left wall turns toward the left edge");
                PlayOptions.Current.Walls = PlayOptions.Current.Ceiling = false;

                world = FlightWorld(); p = Flick(world, 500, 400, 110, -25, .096, 0);
                Check(p.State == PetState.Thrown && FeatureArt.Held.Contains(p.GetLook(world.Now).Sprite), "fast release starts flight before opening the helicopter");
                double startX = p.X; world.Step(p, .1);
                Check(p.State == PetState.Thrown && p.X > startX + 30 && Math.Abs(p.GetLook(world.Now).Angle) > 1, "right flick flies quickly with a tilted flailing pose");
                world.Step(p, .65);
                Check(p.State == PetState.Falling && FeatureArt.Helicopter.Contains(p.GetLook(world.Now).Sprite), "flight changes to the rotating helicopter before landing");
                double x1 = p.X; world.Step(p, .12); double firstDrift = p.X - x1; x1 = p.X; world.Step(p, .12);
                Check(firstDrift > 0 && p.X - x1 < firstDrift, "horizontal momentum fades smoothly after the rotor opens");
                world.Step(p, 7); Check(p.Y == 720 && p.State != PetState.Falling && p.State != PetState.Thrown, "thrown pet lands and resumes floor behavior");
                world = FlightWorld(); p = Flick(world, 700, 400, -100, -20, .096, 0); startX = p.X; world.Step(p, .1);
                Check(p.State == PetState.Thrown && p.X < startX - 30 && p.Dir == -1, "left flick follows the direction of the hand");
                world = FlightWorld(); p = Flick(world, 500, 400, 20, 0, .6, 0);
                Check(p.State == PetState.Falling, "slow placement keeps the original helicopter drop");
                world = FlightWorld(); p = Flick(world, 500, 400, 100, -20, .096, .2);
                Check(p.State == PetState.Falling, "holding still before release cancels stale throw momentum");
                world = FlightWorld(); p = Flick(world, 500, 720, 100, 0, .096, 0); world.Step(p, .1);
                Check(p.State == PetState.Thrown && p.Y < 720, "a fast floor toss lifts off before helicopter descent");
                p.BeginDrag(world, p.X, p.Y - 20); world.Now += .2; p.EndDrag(world);
                Check(p.State == PetState.Falling, "catching a flying pet clears the old throw velocity");
                bool contained = true;
                foreach (var delta in new[] { new Point(300, -120), new Point(-300, -120), new Point(0, -220), new Point(0, 220) })
                {
                    world = FlightWorld(); world.Areas[0] = new Rectangle(-640, 40, 1280, 720);
                    p = Flick(world, 0, 450, delta.X, delta.Y, .096, 0);
                    for (int i = 0; i < 200 && (p.State == PetState.Thrown || p.State == PetState.Falling); i++)
                    {
                        world.Step(p, .02); var pose = p.GetLook(world.Now); PointF origin;
                        using (var frame = Pet.Render(pose, out origin))
                        {
                            Rectangle ink = FeatureArt.Bounds(frame);
                            contained &= p.X - origin.X + ink.Left >= p.Area.Left - 1 && p.X - origin.X + ink.Right <= p.Area.Right + 1 && p.Y + pose.HopY - origin.Y + ink.Top >= p.Area.Top - 1 && p.Y + pose.HopY - origin.Y + ink.Bottom <= p.Area.Bottom + 1;
                        }
                    }
                }
                Check(contained, "strong throws keep all artwork visible on offset monitors");
                PlayOptions.Current.Windows = true;
                DesktopSurfaces.TestProvider = delegate { return new List<SurfaceWindow> { new SurfaceWindow(new IntPtr(31), new Rectangle(100, 500, 1100, 180)) }; }; DesktopSurfaces.Refresh();
                world = FlightWorld(); p = Flick(world, 500, 330, 70, 80, .096, 0); world.Step(p, 2);
                Check(p.SupportWindow == new IntPtr(31) && p.Y == 500, "a downward throw opens its rotor before landing on an open window");
                DesktopSurfaces.TestProvider = delegate { return new List<SurfaceWindow>(); }; DesktopSurfaces.Refresh(); PlayOptions.Current.Windows = false;
                double[] falls = new double[2];
                for (int i = 0; i < 2; i++)
                {
                    PlayOptions.Current.FallSpeed = i == 0 ? .5 : 2;
                    world = FlightWorld(); p = Flick(world, 500, 350, 80, -25, .096, 0); world.Step(p, .7);
                    double y = p.Y; world.Step(p, .2); falls[i] = p.Y - y;
                }
                Check(falls[1] > falls[0] * 1.5, "fall speed setting also controls descent after a throw");
                PlayOptions.Current.FallSpeed = 1.2;
                CaptureFlight(dir);
            }
            finally { PlayOptions.Current = previous; DesktopSurfaces.TestProvider = provider; DesktopSurfaces.Refresh(); }
        }
        private static void CaptureFlight(string dir)
        {
            var world = FlightWorld(); var p = Flick(world, 420, 350, 100, -30, .096, 0);
            using (var board = new Bitmap(960, 540))
            using (var g = Graphics.FromImage(board))
            using (var font = new Font("DejaVu Sans", 11))
            {
                g.Clear(Color.FromArgb(247, 244, 240)); double[] steps = { 0, .1, .2, .24, .2, .7 };
                for (int i = 0; i < steps.Length; i++)
                {
                    if (steps[i] > 0) world.Step(p, steps[i]); PointF origin;
                    using (var frame = Pet.Render(p.GetLook(world.Now), out origin))
                    {
                        int x = i % 3 * 320, y = i / 3 * 270;
                        g.DrawString(p.State + "  " + (world.Now - .096).ToString("0.00") + "s", font, Brushes.DimGray, x + 14, y + 12);
                        double scale = Math.Min(2, Math.Min(280.0 / frame.Width, 210.0 / frame.Height));
                        int width = (int)(frame.Width * scale), height = (int)(frame.Height * scale);
                        g.DrawImage(frame, x + (320 - width) / 2, y + 42, width, height);
                    }
                }
                board.Save(Path.Combine(dir, "throw_storyboard.png"), ImageFormat.Png);
            }
            using (var board = new Bitmap(600, 245))
            using (var g = Graphics.FromImage(board))
            {
                g.Clear(Color.FromArgb(247, 244, 240)); g.DrawLine(Pens.DimGray, 0, 30, 600, 30);
                g.DrawImage(FeatureArt.Ceiling[3].Right, 180, 30 - FeatureArt.CeilingTopInset * 3, FeatureArt.Ceiling[3].W * 3, FeatureArt.Ceiling[3].H * 3);
                board.Save(Path.Combine(dir, "ceiling_contact.png"), ImageFormat.Png);
            }
        }
        private static void TestUi(string dir)
        {
            App app = (App)FormatterServices.GetUninitializedObject(typeof(App)); app.Settings = new Settings();
            var forms = new Form[] { new PlaySettingsForm(app), new FriendPanel(app), new SettingsForm(app), new HourlyNotice(null) };
            ((HourlyNotice)forms[3]).SetMessage("지금은 오후 3시야!\n직접 확인하고 닫아줘.");
            string[] names = { "movement_settings", "tools_panel", "alarm_settings", "hourly_notice" };
            for (int i = 0; i < forms.Length; i++)
            {
                using (var f = forms[i])
                {
                    f.Font = new Font("Noto Sans CJK KR", 9); f.Show(); Application.DoEvents(); f.PerformLayout(); f.Refresh(); Application.DoEvents();
                    using (Bitmap b = new Bitmap(f.Width, f.Height))
                    { using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(f.Location, Point.Empty, b.Size); b.Save(Path.Combine(dir, names[i] + ".png"), ImageFormat.Png); }
                    Check(f.Width > 250 && f.Height > 120, names[i] + " opens with usable layout"); f.Close();
                }
            }
        }
        private static void Capture(string dir)
        {
            string[] names = { "walk", "squash", "touch_cry", "hop", "sit", "idle_cry", "helicopter", "wall", "ceiling", "slide", "held", "snack" };
            var states = new[] { PetState.Walk, PetState.Squish, PetState.TouchCry, PetState.Hop, PetState.Resting, PetState.IdleCry, PetState.Falling, PetState.Wall, PetState.Ceiling, PetState.Sliding, PetState.Drag, PetState.Eating };
            using (Bitmap atlas = new Bitmap(960, 640))
            using (Graphics g = Graphics.FromImage(atlas))
            {
                g.Clear(Color.FromArgb(247, 244, 240)); g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                for (int i = 0; i < names.Length; i++)
                {
                    var w = new CheckWorld(); var p = new Pet(w, 100, 100); p.SetState(states[i], w); p.StateT = .25;
                    if (states[i] == PetState.Squish) p.StartSquish(w, .5, .24, PetState.Walk, false);
                    p.StateT = .25;
                    PointF origin; using (Bitmap image = Pet.Render(p.GetLook(.25), out origin))
                    {
                        image.Save(Path.Combine(dir, names[i] + ".png"), ImageFormat.Png);
                        int x = i % 4 * 240, y = i / 4 * 213;
                        using (Font font = new Font("DejaVu Sans", 11, FontStyle.Bold)) g.DrawString(names[i], font, Brushes.DimGray, x + 12, y + 12);
                        double f = Math.Min(170.0 / image.Width, 160.0 / image.Height); int dw = (int)(image.Width * f), dh = (int)(image.Height * f);
                        g.DrawImage(image, new Rectangle(x + (240 - dw) / 2, y + 40 + (160 - dh), dw, dh));
                    }
                }
                atlas.Save(Path.Combine(dir, "animation_atlas.png"), ImageFormat.Png);
            }
            using (Bitmap atlas = new Bitmap(960, 240))
            using (Graphics g = Graphics.FromImage(atlas))
            {
                g.Clear(Color.FromArgb(247, 244, 240));
                for (int i = 0; i < 8; i++) g.DrawImage(FeatureArt.Helicopter[i * 3].Right, new Rectangle(i * 120 + 20, 10, 90, 200));
                atlas.Save(Path.Combine(dir, "rotor_frames.png"), ImageFormat.Png);
            }
        }
    }
}
