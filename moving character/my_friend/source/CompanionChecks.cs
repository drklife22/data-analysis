using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

namespace WalkingFriend
{
    internal static class CompanionChecks
    {
        private static readonly List<string> results = new List<string>();
        private static void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException("FAILED: " + name); results.Add("PASS " + name); Console.WriteLine("PASS " + name); }
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory); FriendData.StorageOverride = Path.Combine(directory, "data");
            FriendData.Name = "companion-check.xml";
            DesktopSurfaces.TestMouse = delegate { return new Point(-5000, -5000); };
            DesktopSurfaces.TestProvider = delegate { return new List<SurfaceWindow>(); };
            PlayOptions.Current = new PlayOptions { Idle = false, Escape = false, Walls = false, Ceiling = false, Windows = false };
            Sprites.Load(1.25f);
            Directory.CreateDirectory(FriendData.DataDirectory);
            File.WriteAllText(FriendData.DataPath, "<friend schema=\"204\" affinity=\"42\" lastSnackUtc=\"2099-01-01T00:00:00Z\"><notes>예전 메모</notes><tasks><task done=\"1\">기존 할 일</task></tasks></friend>");
            FriendData.Load();
            Check(FriendData.Current.Affinity == 42 && FriendData.Current.Notes == "예전 메모" && FriendData.Current.Todos.Count == 1 && FriendData.Current.Satiety == 100, "version 2.03 records migrate without losing points or notes");
            FriendData.Current = new FriendData(); var data = FriendData.Current;
            data.AdvanceLife(3600);
            Check(data.Hungry && Math.Abs(data.TogetherSeconds - 3600) < .01, "one hour together reaches hunger threshold");
            Check(data.Affinity == 60, "each active minute earns one affinity point");
            Check(data.TrySnack() && data.Satiety == 45 && data.Affinity == 65 && !data.Hungry, "snack adds twenty five satiety and clears crying");
            Check(data.TrySnack() && data.TrySnack() && data.TrySnack() && data.Satiety == 100 && data.Affinity == 80, "successive snacks have no time restriction and stop at one hundred");
            Check(!data.TrySnack() && data.Affinity == 80, "full pet refuses extra food without awarding points");
            data.Notes = "기록 유지 ♥"; data.Todos.Add(new TodoItem { Text = "준비 완료", Done = true }); data.Affinity = 95; data.Like(5);
            Check(data.TakeMilestone() && !data.TakeMilestone(), "hundred milestone is claimed exactly once");
            data.Save(); FriendData.Load(); data = FriendData.Current;
            Check(data.Affinity == 100 && data.CelebratedHundreds == 1 && data.TogetherSeconds == 3600 && data.Notes == "기록 유지 ♥" && data.Todos.Count == 1, "restart restores affinity time tasks and milestone");
            Check(!data.TrySnack(), "full satiety persists through restart");
            data.Satiety = 60; Check(data.TrySnack() && data.Satiety == 85, "only satiety determines whether another snack is allowed");
            data.TogetherSeconds = 3659; data.Save(); FriendData.Load(); data = FriendData.Current; long minuteAffinity = data.Affinity;
            data.AdvanceLife(1); Check(data.Affinity == minuteAffinity + 1 && data.TogetherSeconds == 3660, "partial active minute resumes across restart without duplicate points");
            data.Affinity = 199; data.Like(1); Check(data.TakeMilestone() && data.CelebratedHundreds == 2, "two hundred earns the next flower");
            data.Affinity = 99999; data.Like(5); data.Save(); FriendData.Load(); data = FriendData.Current;
            Check(data.Affinity == 100004, "cumulative affinity passes the old limit and survives restart");
            data.Affinity = data.CelebratedHundreds * 100; data.Satiety = 10;
            var world = new CheckWorld(); var pet = FloorPet(world, 300, PetState.Walk);
            Check(pet.GetLook(world.Now).Sprite == FeatureArt.IdleCry, "hungry walking pet keeps crying expression");
            CaptureArt(pet, world.Now, directory, "hungry-art");
            world.Step(pet, 4); Check(pet.GetLook(world.Now).Sprite == FeatureArt.IdleCry, "hungry expression does not expire after a few seconds");
            pet.Feed(world);
            Check(data.Satiety == 35 && pet.State == PetState.Eating && !data.Hungry, "feeding immediately clears hunger"); CaptureArt(pet, world.Now, directory, "eating-art");
            PlayOptions.Current.HungerMinutes = 0; data.Satiety = 100; var alwaysCry = FloorPet(world, 300, PetState.Walk);
            Check(alwaysCry.GetLook(world.Now).Sprite == FeatureArt.IdleCry, "zero minutes keeps crying even with full satiety");
            PlayOptions.Current.HungerCry = false;
            Check(alwaysCry.GetLook(world.Now).Sprite != FeatureArt.IdleCry, "crying option can turn off the always crying expression");
            PlayOptions.Current.HungerCry = true; PlayOptions.Current.HungerMinutes = 1; data.Satiety = 100; data.AdvanceLife(60);
            Check(data.Hungry, "one minute hunger setting works below the old thirty minute minimum");
            PlayOptions.Current.HungerMinutes = 60; data.Satiety = 100;
            var fedClick = FloorPet(world, 300, PetState.Walk); bool fedCry = false;
            for (int i = 0; i < 8; i++) { fedClick.Poke(world); fedCry |= fedClick.State == PetState.TouchCry || fedClick.GetLook(world.Now).Sprite == FeatureArt.IdleCry; }
            Check(!fedCry, "fed friends never cry during normal click reactions");
            fedClick.SetState(PetState.IdleCry, world); Check(fedClick.GetLook(world.Now).Sprite != FeatureArt.IdleCry, "old crying state clears its tears as soon as hunger ends");
            fedClick.SetState(PetState.Walk, world); fedClick.Demonstrate(world, "cry"); Check(fedClick.State != PetState.TouchCry, "crying demonstration requires hunger");
            var chaseWorld = new CheckWorld(); var chasePet = FloorPet(chaseWorld, 320, PetState.Walk); chasePet.Demonstrate(chaseWorld, "chase");
            double chaseStart = chasePet.X; chaseWorld.Step(chasePet, .02); chaseWorld.Step(chasePet, .5);
            Check(chasePet.State == PetState.Walk && chasePet.GetLook(chaseWorld.Now).Sprite == FeatureArt.Chase, "chase demonstration runs after the fish");
            Check(Math.Abs(chasePet.X - chaseStart) > 34 * Sprites.Scale * PlayOptions.Current.Speed * .52 * 1.5, "chasing runs faster than walking");
            chaseWorld.Step(chasePet, 4); Check(chasePet.GetLook(chaseWorld.Now).Sprite != FeatureArt.Chase, "chasing ends and normal walking returns");
            var restWorld = new CheckWorld(); var restPet = FloorPet(restWorld, 320, PetState.Walk);
            PlayOptions.Current.Idle = true; restWorld.Now = 100; restWorld.Step(restPet, .02);
            Check(restPet.State == PetState.Resting, "default autonomous sitting starts normally");
            restWorld.Step(restPet, 12); Check(restPet.State == PetState.Resting, "default sitting lasts longer than the original eleven seconds");
            PlayOptions.Current.ShortRest = true; var shortWorld = new CheckWorld(); var shortPet = FloorPet(shortWorld, 320, PetState.Walk); shortWorld.Now = 100; shortWorld.Step(shortPet, .02);
            Check(shortPet.State == PetState.Resting, "short rest mode still starts sitting"); shortWorld.Step(shortPet, 12);
            Check(shortPet.State != PetState.Resting, "short rest option restores the original six to eleven seconds");
            PlayOptions.Current.ShortRest = false; PlayOptions.Current.Idle = false;
            var neutral = FloorPet(world, 300, PetState.Pause); CaptureArt(neutral, world.Now, directory, "neutral-art");
            pet = FloorPet(world, 300, PetState.Pause); pet.StartFlower(world, 100);
            Check((pet.GetLook(world.Now).Secret & 1) != 0, "milestone displays the bouquet"); Capture(pet, world.Now, directory, "flower");
            CaptureArt(pet, world.Now + 1, directory, "flower-art");
            string oldKey = pet.GetLook(world.Now).Key(); data.Like(1);
            Check(oldKey == pet.GetLook(world.Now).Key(), "affinity accumulation does not place numbers beside the pet");
            Check(!new PlayOptions().ShowAffinity, "affinity display is hidden by default");
            PlayOptions.Current.ShowAffinity = true; Check(pet.GetLook(world.Now).AffinityLabel == data.Affinity.ToString("N0"), "affinity display can be enabled with its heart badge"); Capture(pet, world.Now, directory, "affinity-enabled"); PlayOptions.Current.ShowAffinity = false;
            data.Satiety = 100;
            foreach (PetState state in new[] { PetState.Walk, PetState.Resting, PetState.Sleep, PetState.Sliding })
            {
                var a = FloorPet(world, 320, state); var b = FloorPet(world, 325, PetState.Walk);
                App.ResolveCompanionContacts(world, new List<Pet> { a, b }, null);
                Check(!Overlap(a, b, world.Now), "collision separates walking friend from " + state);
                Check(a.Dir == -b.Dir, "collision reverses directions for " + state);
            }
            var fastA = FloorPet(world, 450, PetState.Sliding); var fastB = FloorPet(world, 200, PetState.Sliding);
            var positions = new Dictionary<Pet, PointF> { { fastA, new PointF(200, 400) }, { fastB, new PointF(450, 400) } };
            App.ResolveCompanionContacts(world, new List<Pet> { fastA, fastB }, positions);
            Check(fastA.X < fastB.X && !Overlap(fastA, fastB, world.Now), "fast slides cannot tunnel through each other");
            var crowded = Enumerable.Range(0, 7).Select(i => FloorPet(world, 320, PetState.Walk)).ToList();
            App.ResolveCompanionContacts(world, crowded, null);
            bool clear = true; for (int i = 0; i < crowded.Count; i++) for (int j = i + 1; j < crowded.Count; j++) clear &= !Overlap(crowded[i], crowded[j], world.Now);
            Check(clear, "seven overlapping friends settle into separate spaces");
            var dragged = FloorPet(world, 280, PetState.Drag); var standing = FloorPet(world, 281, PetState.Pause);
            App.ResolveCompanionContacts(world, new List<Pet> { dragged, standing }, null);
            Check(Math.Abs(dragged.X - 280) < .01 && !Overlap(dragged, standing, world.Now), "dragged friend pushes others without losing pointer position");
            dragged.X = -10; standing.X = 0;
            App.ResolveCompanionContacts(world, new List<Pet> { dragged, standing }, null);
            Check(dragged.X == -10 && !Overlap(dragged, standing, world.Now), "dragging near the edge preserves the pointer position");
            var wallA = FloorPet(world, 300, PetState.Walk); var wallB = FloorPet(world, 300, PetState.Walk);
            wallA.Demonstrate(world, "wall"); wallB.Demonstrate(world, "wall");
            wallA.Y = FeatureArt.Climb[0].H + 2; wallB.Y = wallA.Y + 1;
            double wallX = wallA.X;
            App.ResolveCompanionContacts(world, new List<Pet> { wallA, wallB }, null);
            Check(!Overlap(wallA, wallB, world.Now) && wallA.X == wallX && wallB.X == wallX, "wall climbers separate vertically without leaving the wall");
            Check(wallA.ContactBody(world.Now).Top >= 0 && wallB.ContactBody(world.Now).Top >= 0, "wall collision near ceiling keeps both bodies on screen");
            PlayOptions.Current.Escape = true; var reactionWorld = new CheckWorld(); var reaction = FloorPet(reactionWorld, 300, PetState.Walk);
            reactionWorld.Now = 2;
            DesktopSurfaces.TestMouse = delegate { return new Point((int)reaction.X + 4, (int)reaction.Y - 25); };
            reactionWorld.Step(reaction, .18); Check(reaction.State == PetState.Sliding, "slide starts within two tenths of a second");
            DesktopSurfaces.TestMouse = delegate { return new Point(-5000, -5000); };
            var circleWorld = new CheckWorld(); var dizzy = FloorPet(circleWorld, 320, PetState.Pause);
            double circleX = dizzy.X, circleY = dizzy.Y - Sprites.Walk1.H * .8;
            for (int i = 0; i <= 82 && dizzy.State != PetState.Dizzy; i++)
            {
                double angle = i * Math.PI * 2 / 80;
                Point mouse = new Point((int)(circleX + Math.Cos(angle) * Sprites.Walk1.W * .55), (int)(circleY + Math.Sin(angle) * Sprites.Walk1.W * .55));
                DesktopSurfaces.TestMouse = delegate { return mouse; }; circleWorld.Step(dizzy, .02);
            }
            Check(dizzy.State == PetState.Dizzy && data.Discoveries.Contains("dizzy"), "one reachable head circle discovers dizziness with escape enabled"); Capture(dizzy, circleWorld.Now, directory, "dizzy");
            var slidingCircle = FloorPet(circleWorld, 320, PetState.Walk); slidingCircle.Demonstrate(circleWorld, "slide");
            circleX = slidingCircle.X; circleY = slidingCircle.Y - Sprites.Walk1.H * .8;
            for (int i = 0; i <= 82 && slidingCircle.State != PetState.Dizzy; i++)
            { double a = -i * Math.PI * 2 / 80; Point mouse = new Point((int)(circleX + Math.Cos(a) * Sprites.Walk1.W * .55), (int)(circleY + Math.Sin(a) * Sprites.Walk1.W * .55)); DesktopSurfaces.TestMouse = delegate { return mouse; }; circleWorld.Step(slidingCircle, .02); }
            Check(slidingCircle.State == PetState.Dizzy, "opposite direction circle also works during an existing slide");
            var direct = FloorPet(circleWorld, 320, PetState.Walk); direct.Demonstrate(circleWorld, "dizzy"); Check(direct.State == PetState.Dizzy, "dizziness can be viewed directly without its gesture");
            DesktopSurfaces.TestMouse = delegate { return new Point(-5000, -5000); }; PlayOptions.Current.Escape = false;
            var flightWorld = new CheckWorld(); var pilot = FloorPet(flightWorld, 320, PetState.Walk);
            PlayOptions.Current.Ceiling = true;
            for (int toss = 0; toss < 3; toss++)
            {
                double handX = pilot.X, handY = pilot.Y - 20;
                pilot.BeginDrag(flightWorld, handX, handY);
                for (int i = 1; i <= 4; i++) { flightWorld.Now += .02; pilot.DragTo(flightWorld, handX + i * 12, handY - i * 5); }
                pilot.EndDrag(flightWorld);
                Check(pilot.State == PetState.Thrown && (pilot.GetLook(flightWorld.Now).Secret & 2) == 0, "throw " + (toss + 1) + " begins with the original toss without early goggles");
            }
            double flightX = pilot.X; flightWorld.Step(pilot, .12);
            Check(pilot.State == PetState.Thrown && pilot.X > flightX + 20, "third throw keeps its original sideways toss momentum");
            flightWorld.Step(pilot, .6); double flightY = pilot.Y; flightWorld.Step(pilot, .12);
            Check(pilot.Y < flightY && pilot.IsPilotFlight && (pilot.GetLook(flightWorld.Now).Secret & 2) != 0, "third toss changes to an upward helicopter with pilot goggles"); Capture(pilot, flightWorld.Now, directory, "pilot");
            flightWorld.Step(pilot, 1.5);
            Check(pilot.State == PetState.Ceiling && Math.Abs(pilot.Y - FeatureArt.CeilingContactHeight) < .01, "pilot arrives attached to the ceiling");
            double ceilingX = pilot.X; flightWorld.Step(pilot, .2);
            Check(pilot.State == PetState.Ceiling && pilot.X != ceilingX, "pilot crawls horizontally after reaching the ceiling"); Capture(pilot, flightWorld.Now, directory, "pilot-ceiling");
            Check((pilot.GetLook(flightWorld.Now).Secret & 2) == 0, "goggles disappear after the pilot reaches the ceiling");
            Flick(pilot, flightWorld, -1); flightWorld.Step(pilot, .7);
            Check(!pilot.IsPilotFlight && (pilot.GetLook(flightWorld.Now).Secret & 2) == 0, "ordinary throw after a pilot flight has no leftover goggles");
            var stale = FloorPet(flightWorld, 250, PetState.Walk);
            Flick(stale, flightWorld, 1); Flick(stale, flightWorld, 1);
            stale.BeginDrag(flightWorld, stale.X, stale.Y - 20); flightWorld.Now += .3; stale.DragTo(flightWorld, stale.X + 1, stale.Y - 20); stale.EndDrag(flightWorld);
            Flick(stale, flightWorld, 1); flightWorld.Step(stale, .7);
            Check(!stale.IsPilotFlight && (stale.GetLook(flightWorld.Now).Secret & 2) == 0, "gentle placement resets an unfinished throw sequence");
            var caught = FloorPet(flightWorld, 250, PetState.Walk); for (int i = 0; i < 3; i++) Flick(caught, flightWorld, 1); flightWorld.Step(caught, .7);
            Check(caught.IsPilotFlight, "fresh third toss enters the pilot ascent");
            caught.BeginDrag(flightWorld, caught.X, caught.Y - 20); flightWorld.Now += .3; caught.EndDrag(flightWorld);
            Check(!caught.IsPilotFlight && (caught.GetLook(flightWorld.Now).Secret & 2) == 0, "catching a pilot cancels goggles immediately");
            var expired = FloorPet(flightWorld, 250, PetState.Walk); Flick(expired, flightWorld, 1); Flick(expired, flightWorld, 1); flightWorld.Now += 11; Flick(expired, flightWorld, 1); flightWorld.Step(expired, .7);
            Check(!expired.IsPilotFlight, "old throws outside ten seconds cannot produce pilot goggles");
            var dancers = Enumerable.Range(0, 3).Select(i => FloorPet(world, 160 + i * 90, PetState.Walk)).ToList();
            foreach (var dancer in dancers) dancer.StartSecretDance(world, world.Now);
            Check(dancers.Select(p => p.GetLook(world.Now + 1).HopY).Distinct().Count() == 1, "secret dancers share the same beat"); Capture(dancers[0], world.Now + 1, directory, "dance");
            var owner = (App)FormatterServices.GetUninitializedObject(typeof(App));
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(App).GetField("clock", flags).SetValue(owner, System.Diagnostics.Stopwatch.StartNew());
            typeof(App).GetField("windows", flags).SetValue(owner, new List<PetWindow>());
            using (var menu = new ContextMenuStrip())
            { menu.Items.Add("닫기"); owner.AddFeatureMenu(menu, null); Check(menu.Items[0].Text.Contains("친밀도 " + data.Affinity.ToString("N0")), "right click menu displays current affinity"); }
            data.Like(1);
            using (var menu = new ContextMenuStrip())
            { menu.Items.Add("닫기"); owner.AddFeatureMenu(menu, null); Check(menu.Items[0].Text.Contains("친밀도 " + data.Affinity.ToString("N0")), "opening the menu again reflects new affinity points"); }
            double before = data.TogetherSeconds; var pulse = typeof(App).GetMethod("CompanionTick", flags);
            pulse.Invoke(owner, new object[] { 1.0, true }); pulse.Invoke(owner, new object[] { 7.0, false }); pulse.Invoke(owner, new object[] { 7.5, true });
            Check(Math.Abs(data.TogetherSeconds - before - 1.5) < .01, "locked time is excluded and active time is counted once");
            var closeTrio = Enumerable.Range(0, 2).Select(i => FloorPet(world, 200 + i * 180, PetState.Walk)).ToList();
            DesktopSurfaces.TestMouse = delegate { return new Point(40, 20); };
            var dance = typeof(App).GetMethod("CheckSecretDance", flags);
            dance.Invoke(owner, new object[] { 1.0, closeTrio });
            DesktopSurfaces.TestMouse = delegate { return new Point(580, 20); };
            dance.Invoke(owner, new object[] { 5.1, closeTrio });
            Check(closeTrio.All(p => p.State == PetState.SecretDance), "two separated friends dance after four seconds even while the cursor moves elsewhere");
            App.ResolveCompanionContacts(world, closeTrio, null);
            Check(!Overlap(closeTrio[0], closeTrio[1], world.Now), "secret dance does not require overlapping bodies");
            data.Discover("dance"); data.Save(); FriendData.Load(); Check(FriendData.Current.Discoveries.Count == 3, "all secret discoveries survive restart");
            using (var glow = EdgeGlowWindow.CreateGlowBitmap(new Size(120, 40), 0))
            { Check(glow.GetPixel(60, 0).A > 150 && glow.GetPixel(60, 39).A == 0, "edge glow fades inward with a transparent center"); glow.Save(Path.Combine(directory, "edge-glow.png"), ImageFormat.Png); }
            Check(EdgeGlowController.Depth(new Size(1920, 1080), 1) >= 125, "default glow spreads much wider than the old forty pixels");
            Check(EdgeGlowController.PulseAlpha(.85, 1) >= 245 && EdgeGlowController.PulseAlpha(2.55, 1) >= 200, "breathing glow stays visible through its full brightness cycle");
            GlowChecks(directory);
            UiChecks(directory);
            var savedOptions = PlayOptions.Current; string savedName = PlayOptions.Name;
            try
            {
                PlayOptions.Name = "companion-options-check.ini"; string path = Path.Combine(Settings.ExeDir, PlayOptions.Name);
                File.WriteAllText(path, "SnackMinutes=30\nHungerMinutes=1\nShortRest=1\nShowAffinity=1\n"); PlayOptions.Current = new PlayOptions(); PlayOptions.Load();
                Check(PlayOptions.Current.HungerMinutes == 1 && PlayOptions.Current.ShortRest, "old snack interval no longer clamps hunger setting and sitting options load");
                Check(!PlayOptions.Current.ShowAffinity, "old default visible affinity migrates to hidden for this update");
                PlayOptions.Current.ShowAffinity = true;
                PlayOptions.Current.HungerMinutes = 0; Check(PlayOptions.Current.Save(), "zero minute hunger and sitting settings save"); PlayOptions.Current = new PlayOptions(); PlayOptions.Load();
                Check(PlayOptions.Current.HungerMinutes == 0 && PlayOptions.Current.ShortRest && PlayOptions.Current.ShowAffinity, "zero minute hunger, sitting, and chosen affinity display survive restart"); File.Delete(path);
            }
            finally { PlayOptions.Name = savedName; PlayOptions.Current = savedOptions; }
            Check(File.ReadAllBytes(Assembly.GetExecutingAssembly().Location).Take(2).SequenceEqual(new byte[] { 77, 90 }), "compiled file has a Windows executable header");
            File.WriteAllLines(Path.Combine(directory, "companion-checks.txt"), results.ToArray()); Console.WriteLine("All " + results.Count + " companion checks passed.");
        }
        private static Pet FloorPet(CheckWorld world, double x, PetState state)
        { var pet = new Pet(world, x, world.Areas[0].Bottom); pet.Y = world.Areas[0].Bottom; pet.SetState(state, world); return pet; }
        private static void Flick(Pet pet, CheckWorld world, int direction)
        {
            double x = pet.X, y = pet.Y - 20; pet.BeginDrag(world, x, y);
            for (int i = 1; i <= 4; i++) { world.Now += .02; pet.DragTo(world, x + direction * i * 12, y - i * 5); }
            pet.EndDrag(world);
        }
        private static bool Overlap(Pet a, Pet b, double now)
        { RectangleF x = a.ContactBody(now), y = b.ContactBody(now); return Math.Min(x.Right, y.Right) - Math.Max(x.Left, y.Left) > .5 && Math.Min(x.Bottom, y.Bottom) - Math.Max(x.Top, y.Top) > .5; }
        private static void Capture(Pet pet, double now, string directory, string name)
        { PointF origin; using (var image = Pet.Render(pet.GetLook(now), out origin)) image.Save(Path.Combine(directory, name + ".png"), ImageFormat.Png); }
        private static void CaptureArt(Pet pet, double now, string directory, string name)
        { var look = pet.GetLook(now); look.Bubble = null; look.AffinityLabel = null; PointF origin; using (var image = Pet.Render(look, out origin)) image.Save(Path.Combine(directory, name + ".png"), ImageFormat.Png); }
        private static void GlowChecks(string directory)
        {
            var monitor = new Rectangle(-800, 100, 800, 480); int depth = EdgeGlowController.Depth(monitor.Size, 1);
            var parts = new[] { new Rectangle(monitor.Left, monitor.Top, monitor.Width, depth), new Rectangle(monitor.Left, monitor.Bottom - depth, monitor.Width, depth), new Rectangle(monitor.Left, monitor.Top + depth, depth, monitor.Height - depth * 2), new Rectangle(monitor.Right - depth, monitor.Top + depth, depth, monitor.Height - depth * 2) };
            for (int theme = 0; theme < 4; theme++) using (var image = new Bitmap(monitor.Width, monitor.Height))
            {
                using (var g = Graphics.FromImage(image))
                {
                    g.Clear(Color.Transparent);
                    foreach (var part in parts) using (var strip = EdgeGlowWindow.CreateGlowBitmap(part, monitor, depth, theme)) g.DrawImageUnscaled(strip, part.Left - monitor.Left, part.Top - monitor.Top);
                }
                Check(image.GetPixel(0, 0).A >= 250 && image.GetPixel(400, 240).A == 0, "glow theme " + theme + " has strong corners and a transparent center");
                Check(image.GetPixel(0, 0).ToArgb() != image.GetPixel(799, 479).ToArgb(), "glow theme " + theme + " combines several colors");
                image.Save(Path.Combine(directory, "glow-theme-" + theme + ".png"), ImageFormat.Png);
            }
            var settings = new Settings(); settings.ReadOptions(new Dictionary<string, string> { { "EdgeGlowTheme", "3" }, { "EdgeGlowWidth", "2" }, { "EdgeGlowStrength", "2" } });
            var text = new System.Text.StringBuilder(); settings.WriteOptions(text);
            Check(settings.EdgeGlowTheme == 3 && settings.EdgeGlowWidth == 2 && settings.EdgeGlowStrength == 2 && text.ToString().Contains("EdgeGlowTheme=3"), "glow color width and brightness serialize with alarm preferences");
        }
        private static void UiChecks(string directory)
        {
            var app = (App)FormatterServices.GetUninitializedObject(typeof(App)); app.Settings = new Settings();
            using (var card = new HourlyNotice(null))
            {
                card.SetMessage("오후 7시야.\n잠깐 숨 돌리고 가자."); card.EnableEdgeGlow(true); card.Show(); Application.DoEvents();
                Check(card.GlowRunning && card.Visible, "hourly card starts a persistent glow controller");
                card.SetMessage("오후 8시야.\n시간을 확인해줘."); Application.DoEvents(); Check(card.GlowRunning, "updating an open hourly card keeps glow alive");
                card.Refresh(); Application.DoEvents();
                using (var image = new Bitmap(card.Width, card.Height)) { using (Graphics g = Graphics.FromImage(image)) g.CopyFromScreen(card.Location, Point.Empty, image.Size); image.Save(Path.Combine(directory, "hourly-card.png"), ImageFormat.Png); }
                ((Button)card.Controls.Find("confirm", true)[0]).PerformClick(); Application.DoEvents(); Check(!card.GlowRunning && card.IsDisposed, "confirmation closes both the card and glow controller");
            }
            using (var card = new HourlyNotice(null))
            { card.EnableEdgeGlow(true); card.Show(); Application.DoEvents(); ((Button)card.Controls.Find("close", true)[0]).PerformClick(); Application.DoEvents(); Check(!card.GlowRunning, "close button also disposes the glow controller"); }
            using (var panel = new FriendPanel(app))
            { panel.Show(); Application.DoEvents(); panel.Refresh(); Application.DoEvents(); using (var image = new Bitmap(panel.Width, panel.Height)) { using (Graphics g = Graphics.FromImage(image)) g.CopyFromScreen(panel.Location, Point.Empty, image.Size); image.Save(Path.Combine(directory, "friend-record.png"), ImageFormat.Png); } Check(panel.Width > 400, "friend records and living settings render");
                var hunger = (NumericUpDown)panel.Controls.Find("hungerMinutes", true)[0]; Check(hunger.Minimum == 0, "hunger setting allows zero and values under thirty minutes"); panel.Close(); }
        }
    }
}
