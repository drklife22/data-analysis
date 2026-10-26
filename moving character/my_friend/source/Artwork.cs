using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WalkingFriend
{
    internal static class FeatureArt
    {
        internal static Sprite TouchCry, IdleCry, Sit, Slide, Chase, Dizzy;
        internal static Sprite[] Helicopter, Climb, ClimbDown, Ceiling, Held, Eating;
        internal static int CeilingTopInset;
        internal static int CeilingContactHeight { get { return Ceiling[0].H - CeilingTopInset; } }
        internal static void Load()
        {
            int w = Sprites.Walk1.W, h = Sprites.Walk1.H;
            TouchCry = LoadFit("cry_touch.png", w, h, false);
            IdleCry = LoadFit("cry_idle.png", w, h, false);
            Sit = LoadFit("sit.png", w, h, false);
            Slide = LoadFit("slide.png", w, h, true);
            Dizzy = LoadFit("dizzy.png", w, h, false);
            Chase = LoadFit("chase.png", (int)Math.Round(w * 1.4), h, true);
            Climb = new Sprite[12]; ClimbDown = new Sprite[12]; Ceiling = new Sprite[12]; Held = new Sprite[12];
            using (Bitmap raw = Raw("climb.png"))
            using (Bitmap baseImage = Fit(raw, w - 8, (int)Math.Round(h * .76)))
            {
                for (int i = 0; i < 12; i++)
                {
                    Bitmap up = OuterOutline(Glide(baseImage, w, i));
                    Bitmap ceiling = (Bitmap)up.Clone();
                    ceiling.RotateFlip(RotateFlipType.RotateNoneFlipY);
                    Ceiling[i] = ToSprite(ceiling, false);
                    // Nose up, with the whole underside against the wall; climbing down turns the nose to the floor.
                    up.RotateFlip(RotateFlipType.Rotate270FlipNone);
                    Bitmap down = (Bitmap)up.Clone(); down.RotateFlip(RotateFlipType.RotateNoneFlipY);
                    Climb[i] = ToSprite(up, false); ClimbDown[i] = ToSprite(down, false);
                }
            }
            using (Bitmap raw = Raw("held.png"))
            using (Bitmap carry = Fit(raw, w, h))
            {
                for (int i = 0; i < 12; i++)
                    using (Bitmap moving = Paws(carry, i)) Held[i] = ToSprite(OuterOutline((Bitmap)moving.Clone()), false);
            }
            CeilingTopInset = Bounds(Ceiling[0].Right).Top;
            Helicopter = new Sprite[24];
            using (Bitmap raw = Raw("helicopter.png"))
            {
                // The falling pose floats down with a soft swing instead of a turning rotor.
                int cw = (int)Math.Round(w * 1.45), ch = h + 6;
                using (Bitmap body = Fit(raw, cw - 8, h))
                {
                    for (int i = 0; i < Helicopter.Length; i++)
                    {
                        Bitmap frame = new Bitmap(cw, ch, PixelFormat.Format32bppArgb);
                        using (Graphics g = Graphics.FromImage(frame))
                        {
                            Quality(g); g.Clear(Color.Transparent);
                            double phase = i * Math.PI * 2 / Helicopter.Length;
                            g.TranslateTransform(cw / 2f, ch * .35f);
                            g.RotateTransform((float)(Math.Sin(phase) * 7));
                            g.TranslateTransform(-body.Width / 2f, (float)(-ch * .35f + 3 + Math.Sin(phase * 2) * 1.5 * Sprites.Scale));
                            g.DrawImage(body, 0, 0, body.Width, body.Height);
                        }
                        Helicopter[i] = ToSprite(OuterOutline(frame), false);
                    }
                }
            }
            Eating = new Sprite[4];
            Sprite sip = LoadFit("eat2.png", w, h, false);
            for (int i = 0; i < 4; i++) Eating[i] = sip;
        }
        private static Bitmap Raw(string name)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            { if (stream == null) throw new InvalidOperationException("Missing artwork: " + name); using (var b = new Bitmap(stream)) return new Bitmap(b); }
        }
        private static Sprite LoadFit(string name, int w, int h, bool facesLeft)
        { using (Bitmap b = Raw(name)) return ToSprite(OuterOutline(Fit(b, w, h)), facesLeft); }
        internal static void Quality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }
        internal static Rectangle Bounds(Bitmap b)
        {
            int l = b.Width, t = b.Height, r = -1, bot = -1;
            var rect = new Rectangle(0, 0, b.Width, b.Height); var data = b.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] bytes = new byte[data.Stride * b.Height]; Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++)
                    if (bytes[y * data.Stride + x * 4 + 3] > 30) { l = Math.Min(l, x); t = Math.Min(t, y); r = Math.Max(r, x); bot = Math.Max(bot, y); }
            }
            finally { b.UnlockBits(data); }
            return r < l ? new Rectangle(0, 0, b.Width, b.Height) : Rectangle.FromLTRB(l, t, r + 1, bot + 1);
        }
        internal static Bitmap Fit(Bitmap b, int w, int h)
        {
            Rectangle r = Bounds(b); double f = Math.Min((w - 4.0) / r.Width, (h - 3.0) / r.Height);
            float dw = (float)(r.Width * f), dh = (float)(r.Height * f);
            Bitmap result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            { Quality(g); g.Clear(Color.Transparent); using (var attr = new ImageAttributes()) { attr.SetWrapMode(WrapMode.TileFlipXY); g.DrawImage(b, new Rectangle((int)Math.Round((w - dw) / 2), (int)Math.Round(h - 1 - dh), (int)Math.Round(dw), (int)Math.Round(dh)), r.X, r.Y, r.Width, r.Height, GraphicsUnit.Pixel, attr); } }
            return result;
        }
        internal static Sprite ToSprite(Bitmap b, bool facesLeft)
        {
            Bitmap mirror = (Bitmap)b.Clone(); mirror.RotateFlip(RotateFlipType.RotateNoneFlipX);
            return new Sprite { Right = facesLeft ? mirror : b, Left = facesLeft ? b : mirror };
        }
        private static Bitmap Glide(Bitmap b, int width, int frame)
        {
            // The whole airplane surges gently along its path, so no single part wobbles on its own.
            Bitmap target = new Bitmap(width, b.Height, PixelFormat.Format32bppArgb);
            float shift = (float)(Math.Sin(frame * Math.PI * 2 / 12.0) * width * .04);
            using (Graphics g = Graphics.FromImage(target))
            { Quality(g); g.Clear(Color.Transparent); g.DrawImage(b, (width - b.Width) / 2f + shift, 0, b.Width, b.Height); }
            return target;
        }
        private static Bitmap Paws(Bitmap b, int frame)
        {
            Bitmap target = (Bitmap)b.Clone(); int w = b.Width, h = b.Height;
            double phase = frame * Math.PI * 2 / 12.0;
            // A local warp at the two little paws leaves the eyes, mouth and head fixed.
            for (int y = (int)(h * .74); y < h; y++) for (int x = 1; x < w - 1; x++)
            {
                double weight = 0;
                foreach (double center in new[] { .46, .86 })
                {
                    double dx = (x / (double)w - center) / .09, dy = (y / (double)h - .89) / .16;
                    double d = dx * dx + dy * dy;
                    if (d < 1) weight += Math.Pow(1 - d, 2) * Math.Sin(phase + (center > .6 ? Math.PI : 0));
                }
                int sy = (int)Math.Round(y - weight * 2.2 * Sprites.Scale);
                target.SetPixel(x, y, sy < 0 || sy >= h ? Color.Transparent : b.GetPixel(x, sy));
            }
            return target;
        }
        internal static Bitmap OuterOutline(Bitmap source)
        {
            int w = source.Width, h = source.Height; Color[] px = new Color[w * h]; bool[] outside = new bool[w * h]; var queue = new Queue<int>();
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = source.GetPixel(x, y);
            Action<int> push = delegate(int i) { if (i >= 0 && i < outside.Length && !outside[i] && px[i].A < 180) { outside[i] = true; queue.Enqueue(i); } };
            for (int x = 0; x < w; x++) { push(x); push((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { push(y * w); push(y * w + w - 1); }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % w, y = i / w;
                if (x > 0) push(i - 1); if (x < w - 1) push(i + 1); if (y > 0) push(i - w); if (y < h - 1) push(i + w);
            }
            Bitmap output = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            double radius = .72 * Sprites.Scale; int reach = (int)Math.Ceiling(radius + 1);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                int i = y * w + x; Color c = px[i];
                // Match the dark exterior ink while leaving every interior face line alone.
                if (!outside[i] && c.A > 180 && Math.Max(c.R, Math.Max(c.G, c.B)) < 145)
                {
                    bool atEdge = false; int edgeReach = Math.Max(1, (int)Math.Ceiling(1.8 * Sprites.Scale));
                    for (int dy = -edgeReach; dy <= edgeReach && !atEdge; dy++) for (int dx = -edgeReach; dx <= edgeReach; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && ny >= 0 && nx < w && ny < h && outside[ny * w + nx] && dx * dx + dy * dy <= edgeReach * edgeReach) { atEdge = true; break; }
                    }
                    if (atEdge) c = Color.FromArgb(c.A, 25, 20, 20);
                }
                if (outside[i])
                {
                    double best = 0;
                    for (int dy = -reach; dy <= reach; dy++) for (int dx = -reach; dx <= reach; dx++)
                    {
                        int nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        Color q = px[ny * w + nx]; if (q.A < 200) continue;
                        double alpha = Math.Max(0, Math.Min(1, radius + .7 - Math.Sqrt(dx * dx + dy * dy))) * q.A;
                        best = Math.Max(best, alpha);
                    }
                    if (best > 0)
                    {
                        double under = best * (1 - c.A / 255.0), a = c.A + under;
                        c = Color.FromArgb((int)Math.Round(a), (int)Math.Round((c.R * c.A + 25 * under) / a), (int)Math.Round((c.G * c.A + 20 * under) / a), (int)Math.Round((c.B * c.A + 20 * under) / a));
                    }
                }
                output.SetPixel(x, y, c);
            }
            source.Dispose(); return output;
        }
    }
}
