using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace WalkingFriend
{
    internal sealed partial class Pet
    {
        private double flowerUntil, flowerStarted, orbitAngle, orbitTotal, orbitAt, orbitStarted, dizzyCooldown;
        private double orbitCenterX, orbitCenterY;
        private Point orbitMouse;
        private double orbitMotionAngle, orbitCurveTotal;
        private bool orbitMotionSeen;
        private bool orbitSeen;
        private int orbitDirection, throwCount;
        private double throwSequenceStarted;
        private bool pilotFlight;
        private double danceStarted;
        internal bool IsCompanionFloorPose
        { get { return State == PetState.Walk || State == PetState.Pause || State == PetState.Resting || State == PetState.IdleCry || State == PetState.Squish; } }
        internal bool IsPilotFlight { get { return pilotFlight && State == PetState.PilotRise; } }
        internal bool CanDance(double now)
        { return !Hidden && !InputHeld && now >= flowerUntil && (IsCompanionFloorPose || State == PetState.Sliding); }
        private void ResetSecretThrows()
        { throwCount = 0; throwSequenceStarted = 0; pilotFlight = false; }
        internal void RegisterSecretThrow(IWorld w)
        {
            pilotFlight = false;
            if (!PlayOptions.Current.Secrets) { ResetSecretThrows(); return; }
            if (throwCount == 0 || w.Now - throwSequenceStarted > 10) { throwCount = 0; throwSequenceStarted = w.Now; }
            throwCount++;
            if (throwCount >= 3)
            { throwCount = 0; pilotFlight = true; FriendData.Current.Discover("pilot"); Bubble = "비밀 조종사! 천장으로 출발!"; BubbleUntil = w.Now + 4; }
        }
        internal void StartFlower(IWorld w, long milestone)
        {
            flowerStarted = w.Now; flowerUntil = w.Now + 6; mouseGrace = flowerUntil + 1;
            Bubble = "꽃다발 받아줘 ♥"; BubbleUntil = flowerUntil;
            SetState(PetState.Pause, w);
        }
        internal void StartSecretDance(IWorld w, double togetherStart)
        { danceStarted = togetherStart; extraDuration = 6; mouseGrace = w.Now + 8; orbitSeen = false; orbitTotal = 0; SetState(PetState.SecretDance, w); }
        internal void StartDizzy(IWorld w)
        {
            dizzyCooldown = w.Now + 20; orbitTotal = 0; orbitSeen = false;
            extraDuration = 4; mouseGrace = w.Now + 5; Bubble = "빙글빙글… 어질어질!"; BubbleUntil = w.Now + 3.5;
            SetState(PetState.Dizzy, w); FriendData.Current.Discover("dizzy");
        }
        private bool CirclingMouse(double now) { return orbitSeen && Math.Abs(orbitTotal) > .10 && Math.Abs(orbitCurveTotal) > .16 && now - orbitAt < .45; }
        private bool AdvanceSecrets(IWorld w, double dt)
        {
            if (!PlayOptions.Current.Secrets)
            { ResetSecretThrows(); if (State == PetState.PilotRise) SetState(PetState.Falling, w); }
            if (State == PetState.PilotRise)
            {
                orbitSeen = false; orbitTotal = 0; StateT += dt; animT += dt;
                if (Bubble != null && w.Now > BubbleUntil) Bubble = null;
                double half = FeatureArt.Helicopter[0].W / 2.0 + 1;
                X += helicopterDrift * dt; helicopterDrift *= Math.Exp(-4 * dt);
                X = Math.Max(Area.Left + half, Math.Min(Area.Right - half, X));
                Y -= 185 * S * dt;
                if (Y <= Area.Top + FeatureArt.Helicopter[0].H + 1)
                {
                    ClearAttachments(); pilotFlight = false; Vy = 0; helicopterDrift = 0;
                    half = FeatureArt.Ceiling[0].W / 2.0;
                    X = Math.Max(Area.Left + half + 1, Math.Min(Area.Right - half - 1, X));
                    if (X > Area.Right - FeatureArt.Ceiling[0].W * 2) Dir = -1;
                    else if (X < Area.Left + FeatureArt.Ceiling[0].W * 2) Dir = 1;
                    Y = Area.Top + FeatureArt.CeilingContactHeight; mouseGrace = w.Now + 1; SetState(PetState.Ceiling, w);
                    Bubble = "천장 도착! 살금살금…"; BubbleUntil = w.Now + 2.5;
                }
                return true;
            }
            if (State == PetState.Dizzy || State == PetState.SecretDance)
            {
                StateT += dt; animT += dt;
                if (Bubble != null && w.Now > BubbleUntil) Bubble = null;
                SnapToFloor(w);
                if (StateT >= extraDuration) { SetState(PetState.Walk, w); DelayIdle(w); }
                return true;
            }
            if (w.Now < flowerUntil && IsCompanionFloorPose)
            { animT += dt; SnapToFloor(w); return true; }
            if (!PlayOptions.Current.Secrets || (!IsCompanionFloorPose && State != PetState.Sliding) || InputHeld || w.Now < dizzyCooldown)
            { orbitSeen = false; orbitTotal = 0; return false; }
            Point mouse = DesktopSurfaces.Mouse;
            if (Math.Abs(mouse.X - X) > Sprites.Walk1.W * 3.5 || mouse.Y < Y - Sprites.Walk1.H * 4 || mouse.Y > Y + Sprites.Walk1.H * .6)
            { orbitSeen = false; orbitTotal = 0; return false; }
            bool fresh = !orbitSeen || w.Now - orbitAt > .8 || w.Now - orbitStarted > 6;
            if (fresh) { orbitCenterX = X; orbitCenterY = Y - Sprites.Walk1.H * .8; }
            double dx = mouse.X - orbitCenterX, dy = mouse.Y - orbitCenterY;
            double radius = Math.Sqrt(dx * dx + dy * dy);
            if (radius < Sprites.Walk1.W * .12 || radius > Sprites.Walk1.W * 3.5)
            { orbitSeen = false; orbitTotal = 0; return false; }
            double angleNow = Math.Atan2(dy, dx);
            if (fresh)
            { orbitSeen = true; orbitStarted = orbitAt = w.Now; orbitAngle = angleNow; orbitTotal = orbitCurveTotal = 0; orbitDirection = 0; orbitMouse = mouse; orbitMotionSeen = false; return false; }
            double mx = mouse.X - orbitMouse.X, my = mouse.Y - orbitMouse.Y; orbitMouse = mouse;
            if (mx * mx + my * my >= 1)
            {
                double motion = Math.Atan2(my, mx);
                if (orbitMotionSeen)
                {
                    double turn = motion - orbitMotionAngle;
                    while (turn > Math.PI) turn -= Math.PI * 2;
                    while (turn < -Math.PI) turn += Math.PI * 2;
                    orbitCurveTotal += turn;
                }
                orbitMotionAngle = motion; orbitMotionSeen = true;
            }
            double difference = angleNow - orbitAngle;
            while (difference > Math.PI) difference -= Math.PI * 2;
            while (difference < -Math.PI) difference += Math.PI * 2;
            orbitAngle = angleNow;
            if (Math.Abs(difference) > 1.9) { orbitSeen = false; orbitTotal = 0; return false; }
            if (Math.Abs(difference) > .012)
            {
                int direction = Math.Sign(difference);
                if (orbitDirection != 0 && direction != orbitDirection) orbitTotal = 0;
                orbitDirection = direction; orbitTotal += difference; orbitAt = w.Now;
            }
            if (Math.Abs(orbitTotal) >= Math.PI * 2 - .18 && Math.Abs(orbitCurveTotal) > Math.PI) { StartDizzy(w); return true; }
            if (CirclingMouse(w.Now))
            {
                // Keep the circle's center still, and give a deliberate swirl priority over escape.
                if (State == PetState.Sliding) SetState(PetState.Pause, w);
                mouseGrace = w.Now + .5; hoverSince = -1; animT += dt; SnapToFloor(w); return true;
            }
            return false;
        }
        private void CompanionLook(ref Look look, double now)
        {
            if (PlayOptions.Current.ShowAffinity)
            { look.AffinityLabel = FriendData.Current.Affinity.ToString("N0"); look.AffinityOnLeft = X + Sprites.Walk1.W * .5 + (22 + look.AffinityLabel.Length * 8) * S > Area.Right; }
            if (FriendData.Current.Hungry && IsCompanionFloorPose)
            { look.Sprite = FeatureArt.IdleCry; look.Left = false; }
            if (now < flowerUntil && IsCompanionFloorPose)
            { look.Sprite = Sprites.Wake; look.Secret |= 1; look.SecretPhase = (float)(now - flowerStarted); }
            if (IsPilotFlight)
            { look.Secret |= 2; look.SecretPhase = (float)animT; }
            if (State == PetState.Dizzy)
            { look.Sprite = FeatureArt.Sit; look.Left = false; look.Angle = (float)(Math.Sin(StateT * 7) * 6); look.Secret |= 4; look.SecretPhase = (float)StateT; }
            if (State == PetState.SecretDance)
            {
                double phase = now - danceStarted;
                look.Sprite = Sprites.Dance; look.Left = ((int)(phase / .6) % 2 == 0);
                look.HopY = (float)(-Math.Abs(Math.Sin(phase * Math.PI * 2.5)) * 12 * S);
                look.Angle = (float)(Math.Sin(phase * Math.PI * 2.5) * 9); look.Secret |= 8; look.SecretPhase = (float)phase;
            }
        }
        internal RectangleF ContactBody(double now)
        {
            Look look = GetLook(now); Rectangle ink;
            if (contactScale != Sprites.Scale) { contactScale = Sprites.Scale; contactInk.Clear(); }
            if (!contactInk.TryGetValue(look.Sprite.Right, out ink)) { ink = FeatureArt.Bounds(look.Sprite.Right); contactInk[look.Sprite.Right] = ink; }
            // Body collision excludes balloons and decorative particles.
            float left = (float)X + (ink.Left - look.Sprite.W * .5f) * look.Sx;
            float top = (float)Y - look.Sprite.H * look.Sy + ink.Top * look.Sy;
            if (State == PetState.Falling)
            { top = (float)Y - Sprites.Walk1.H; left = (float)X - Sprites.Walk1.W * .43f; return new RectangleF(left, top, Sprites.Walk1.W * .86f, Sprites.Walk1.H * .95f); }
            return new RectangleF(left, top, Math.Max(8, ink.Width * look.Sx), Math.Max(8, ink.Height * look.Sy));
        }
        internal void Bump(IWorld w, int direction)
        {
            Dir = direction;
            if (State == PetState.Wall) { wallDescending = direction > 0; return; }
            if (State == PetState.Thrown) { throwVx = direction * Math.Max(110 * S, Math.Abs(throwVx) * .65); return; }
            if (State == PetState.Falling) { helicopterDrift = direction * 70 * S; return; }
            if (!IsCompanionFloorPose && State != PetState.Sliding && State != PetState.TouchCry && State != PetState.Hop && State != PetState.Eating) return;
            if (w.Now < contactReactionUntil) return;
            contactReactionUntil = w.Now + .45;
            StartSquish(w, .22, .12, PetState.Walk, false); mouseGrace = w.Now + .6;
        }
        private double contactReactionUntil;
        private static float contactScale;
        private static readonly Dictionary<Bitmap, Rectangle> contactInk = new Dictionary<Bitmap, Rectangle>();
        internal void ClampContact()
        {
            if (State == PetState.Drag || State == PetState.Wall || State == PetState.WindowClimb) return;
            double half = Sprites.Walk1.W * .46;
            X = Math.Max(Area.Left + half, Math.Min(Area.Right - half, X));
            if (SupportWindow != IntPtr.Zero) X = Math.Max(SupportBounds.Left + half, Math.Min(SupportBounds.Right - half, X));
        }
        internal void ClampWallContact()
        {
            if (State != PetState.Wall && State != PetState.WindowClimb) return;
            Y = Math.Max(Area.Top + FeatureArt.Climb[0].H + 1, Math.Min(Area.Bottom, Y));
        }
    }

    internal sealed partial class App
    {
        private double companionClock = -1, companionSavedAt, danceAwayAt, nextSecretDance;
        private bool danceAwaySeen;
        private void CompanionTick(double now, bool active)
        {
            double dt = companionClock < 0 ? 0 : now - companionClock; companionClock = now;
            if (active && dt > 0 && dt < 2) FriendData.Current.AdvanceLife(dt);
            if (now - companionSavedAt >= 30) { companionSavedAt = now; FriendData.Current.Save(); }
            if (!active || hidden || autoHidden) return;
            var pet = windows.Select(w => w.Pet).FirstOrDefault(p => !p.Hidden && p.IsCompanionFloorPose);
            if (pet != null && FriendData.Current.TakeMilestone()) pet.StartFlower(this, FriendData.Current.CelebratedHundreds * 100);
        }
        private void CheckSecretDance(double now, List<Pet> all)
        {
            Point mouse = DesktopSurfaces.Mouse;
            var eligible = all.Where(p => p.CanDance(now)).ToList();
            if (!PlayOptions.Current.Secrets || PlayOptions.IsFocus(DateTime.Now) || eligible.Count < 2)
            { danceAwaySeen = false; return; }
            if (eligible.Any(p => Math.Abs(mouse.X - p.X) < Sprites.Walk1.W * 1.6 && Math.Abs(mouse.Y - (p.Y - Sprites.Walk1.H * .5)) < Sprites.Walk1.H * 1.5))
            { danceAwaySeen = false; return; }
            if (!danceAwaySeen) { danceAwayAt = now; danceAwaySeen = true; }
            if (now < nextSecretDance || now - danceAwayAt < 4) return;
            foreach (var group in eligible.GroupBy(p => new { p.Area, p.SupportWindow }))
            {
                var pets = group.OrderBy(p => p.X).ToList();
                for (int i = 0; i + 1 < pets.Count; i++)
                {
                    var dancers = pets.Skip(i).TakeWhile(p => p.X - pets[i].X <= Sprites.Walk1.W * 8).Where(p => Math.Abs(p.Y - pets[i].Y) < Sprites.Walk1.H * .4).ToList();
                    if (dancers.Count < 2) continue;
                    BeginSecretDance(dancers, now); return;
                }
            }
        }
        private void BeginSecretDance(List<Pet> dancers, double now)
        {
            foreach (var p in dancers) p.StartSecretDance(this, now);
            nextSecretDance = now + 45; danceAwaySeen = false; FriendData.Current.Discover("dance");
        }
        internal void DemonstrateSecretDance(Pet selected)
        {
            if (selected == null) selected = windows.Select(w => w.Pet).FirstOrDefault(p => !p.Hidden);
            if (selected == null) return;
            var dancers = windows.Select(w => w.Pet).Where(p => p.CanDance(Now) && p.Area == selected.Area && p.SupportWindow == selected.SupportWindow).ToList();
            if (dancers.Count < 2)
            { selected.Bubble = "친구 둘이 바닥에 모이면 춤출 수 있어 ♪"; selected.BubbleUntil = Now + 4; return; }
            BeginSecretDance(dancers, Now);
        }
        internal static void ResolveCompanionContacts(IWorld world, List<Pet> all, Dictionary<Pet, PointF> oldPositions)
        {
            var live = all.Where(p => !p.Hidden).ToArray();
            for (int pass = 0; pass < Math.Max(8, live.Length * 3); pass++)
            {
                bool moved = false;
                for (int i = 0; i < live.Length; i++) for (int j = i + 1; j < live.Length; j++)
                {
                    Pet a = live[i], b = live[j]; RectangleF ra = a.ContactBody(world.Now), rb = b.ContactBody(world.Now);
                    bool touching = ra.IntersectsWith(rb);
                    PointF oldA, oldB;
                    bool crossed = pass == 0 && oldPositions != null && oldPositions.TryGetValue(a, out oldA) && oldPositions.TryGetValue(b, out oldB)
                        && Math.Abs(a.Y - b.Y) < Sprites.Walk1.H * .25 && Math.Abs(oldA.Y - oldB.Y) < Sprites.Walk1.H * .25
                        && (oldA.X - oldB.X) * (a.X - b.X) < 0;
                    if (!touching && !crossed) continue;
                    float overlapX = Math.Min(ra.Right, rb.Right) - Math.Max(ra.Left, rb.Left);
                    float overlapY = Math.Min(ra.Bottom, rb.Bottom) - Math.Max(ra.Top, rb.Top);
                    bool wallA = a.State == PetState.Wall || a.State == PetState.WindowClimb, wallB = b.State == PetState.Wall || b.State == PetState.WindowClimb;
                    bool vertical = !crossed && ((wallA && wallB) || (wallA && b.State == PetState.Drag) || (wallB && a.State == PetState.Drag));
                    int sign = a.X < b.X ? -1 : 1;
                    if (crossed && oldPositions[a].X != oldPositions[b].X) sign = oldPositions[a].X < oldPositions[b].X ? -1 : 1;
                    if (vertical)
                    {
                        double distance = overlapY + 3 * Sprites.Scale; sign = a.Y < b.Y ? -1 : 1;
                        if (a.State != PetState.Drag) a.Y += sign * distance * (b.State == PetState.Drag ? 1 : .5);
                        if (b.State != PetState.Drag) b.Y -= sign * distance * (a.State == PetState.Drag ? 1 : .5);
                        a.ClampWallContact(); b.ClampWallContact();
                    }
                    else
                    {
                        double needed = (ra.Width + rb.Width) / 2.0 + 3 * Sprites.Scale;
                        double midpoint = (a.X + b.X) * .5;
                        if (a.State == PetState.Drag || wallA) b.X = a.X - sign * needed;
                        else if (b.State == PetState.Drag || wallB) a.X = b.X + sign * needed;
                        else { a.X = midpoint + sign * needed / 2; b.X = midpoint - sign * needed / 2; }
                        a.ClampContact(); b.ClampContact();
                    }
                    if (pass == 0) { if (a.State != PetState.Drag) a.Bump(world, sign); if (b.State != PetState.Drag) b.Bump(world, -sign); }
                    moved = true;
                }
                if (!moved) break;
            }
        }
    }
}
