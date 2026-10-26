using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WalkingFriend
{
    internal sealed partial class Pet
    {
        private static Font affinityFont;
        private static float affinityFontScale;
        private static Font AffinityFont(float scale)
        {
            if (affinityFont == null || affinityFontScale != scale)
            { if (affinityFont != null) affinityFont.Dispose(); affinityFont = new Font("Segoe UI", 10 * scale, FontStyle.Bold, GraphicsUnit.Pixel); affinityFontScale = scale; }
            return affinityFont;
        }
        private static RectangleF AffinityBadgeBounds(Look look, float width, float height, float scale)
        {
            if (string.IsNullOrEmpty(look.AffinityLabel)) return RectangleF.Empty;
            float badgeWidth;
            using (var image = new Bitmap(1, 1)) using (var g = Graphics.FromImage(image))
                badgeWidth = Math.Max(38 * scale, g.MeasureString(look.AffinityLabel, AffinityFont(scale), PointF.Empty, StringFormat.GenericTypographic).Width + 22 * scale);
            return new RectangleF(look.AffinityOnLeft ? -width * .5f - 3 * scale - badgeWidth : width * .5f + 3 * scale,
                -height * .57f - 9 * scale, (float)Math.Ceiling(badgeWidth), 20 * scale);
        }
        private static void DrawAffinityBadge(Graphics g, Look look, RectangleF bounds, float scale)
        {
            if (bounds.IsEmpty) return;
            using (var path = RoundRect(bounds, 8 * scale))
            using (var fill = new SolidBrush(Color.FromArgb(245, 255, 248, 246)))
            using (var border = new Pen(Color.FromArgb(225, 237, 196, 207), scale))
            { g.FillPath(fill, path); g.DrawPath(border, path); }
            using (var outline = new Pen(Color.FromArgb(218, 105, 139), scale)) DrawHeart(g, bounds.Left + 8 * scale, bounds.Top + 10 * scale, 4 * scale, outline);
            using (var text = new SolidBrush(Color.FromArgb(113, 73, 91)))
            using (var format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.LineAlignment = StringAlignment.Center; format.Alignment = StringAlignment.Center;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.DrawString(look.AffinityLabel, AffinityFont(scale), text, new RectangleF(bounds.Left + 16 * scale, bounds.Top, bounds.Width - 17 * scale, bounds.Height), format);
            }
        }
        private static void DrawSecretArt(Graphics g, Look look, float width, float height, float scale)
        {
            if (look.Secret == 0) return;
            GraphicsState saved = g.Save();
            try
            {
                if (look.Angle != 0)
                { g.TranslateTransform(look.Px, look.Py); g.RotateTransform(look.Angle); g.TranslateTransform(-look.Px, -look.Py); }
                g.ScaleTransform(look.Sx, look.Sy);
                using (var outline = new Pen(Color.FromArgb(43, 36, 42), 2.2f * scale))
                {
                    outline.StartCap = outline.EndCap = LineCap.Round; outline.LineJoin = LineJoin.Round;
                    if ((look.Secret & 1) != 0)
                    {
                        float x = width * .38f, y = -height * .47f;
                        using (var stem = new Pen(Color.FromArgb(90, 141, 97), 2.4f * scale))
                        { for (int j = -1; j <= 1; j++) g.DrawLine(stem, x - 3 * scale, y + 25 * scale, x + j * 9 * scale, y - (j == 0 ? 7 : 0) * scale); }
                        using (var wrapping = new SolidBrush(Color.FromArgb(255, 224, 231)))
                        {
                            var points = new[] { new PointF(x - 13 * scale, y + 8 * scale), new PointF(x + 10 * scale, y + 8 * scale), new PointF(x - 2 * scale, y + 28 * scale) };
                            g.FillPolygon(wrapping, points); g.DrawPolygon(outline, points);
                        }
                        using (var paw = new SolidBrush(Color.White))
                        { g.FillEllipse(paw, x - 9 * scale, y + 17 * scale, 10 * scale, 7 * scale); g.DrawEllipse(outline, x - 9 * scale, y + 17 * scale, 10 * scale, 7 * scale); }
                        for (int j = -1; j <= 1; j++)
                        {
                            float fx = x + j * 9 * scale, fy = y - (j == 0 ? 9 : 0) * scale;
                            for (int i = 0; i < 5; i++)
                            {
                                double angle = i * Math.PI * 2 / 5 - Math.PI / 2;
                                float px = fx + (float)Math.Cos(angle) * 4 * scale, py = fy + (float)Math.Sin(angle) * 4 * scale;
                                using (var petal = new SolidBrush(j == 0 ? Color.FromArgb(208, 183, 238) : Color.FromArgb(245, 164, 185)))
                                { g.FillEllipse(petal, px - 3.5f * scale, py - 3.5f * scale, 7 * scale, 7 * scale); g.DrawEllipse(outline, px - 3.5f * scale, py - 3.5f * scale, 7 * scale, 7 * scale); }
                            }
                            using (var center = new SolidBrush(Color.FromArgb(255, 218, 117)))
                            { g.FillEllipse(center, fx - 3 * scale, fy - 3 * scale, 6 * scale, 6 * scale); g.DrawEllipse(outline, fx - 3 * scale, fy - 3 * scale, 6 * scale, 6 * scale); }
                        }
                    }
                    if ((look.Secret & 2) != 0)
                    {
                        float body = Sprites.Walk1.H;
                        // Placed over the eyes of the falling picture.
                        float y = -height + body * (height > body * 1.15f ? .74f : .52f);
                        g.TranslateTransform(-Sprites.Walk1.W * .115f, 0);
                        g.DrawLine(outline, -Sprites.Walk1.W * .30f, y, Sprites.Walk1.W * .30f, y);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            float x = side * Sprites.Walk1.W * .15f;
                            var lens = new RectangleF(x - 8 * scale, y - 6 * scale, 16 * scale, 12 * scale);
                            using (var shape = RoundRect(lens, 4 * scale))
                            using (var glass = new SolidBrush(Color.FromArgb(230, 170, 218, 237)))
                            { g.FillPath(glass, shape); g.DrawPath(outline, shape); }
                            using (var shine = new Pen(Color.White, 1.4f * scale)) g.DrawLine(shine, x - 4 * scale, y - 3 * scale, x - 1 * scale, y - 4 * scale);
                        }
                        g.DrawLine(outline, -3 * scale, y, 3 * scale, y);
                    }
                    if ((look.Secret & 4) != 0)
                    {
                        for (int side = -1; side <= 1; side += 2)
                        {
                            float x = side < 0 ? -width * .18f : width * .05f;
                            float y = -height * (side < 0 ? .55f : .525f);
                            using (var white = new SolidBrush(Color.White)) g.FillEllipse(white, x - 8 * scale, y - 8 * scale, 16 * scale, 16 * scale);
                            var points = new PointF[43];
                            for (int i = 0; i < points.Length; i++) { double a = i * Math.PI / 10; double r = .4 + i * .13; points[i] = new PointF(x + (float)(Math.Cos(a) * r * scale), y + (float)(Math.Sin(a) * r * scale)); }
                            g.DrawLines(outline, points);
                        }
                        using (var stars = new Pen(Color.FromArgb(222, 172, 71), 2 * scale))
                        for (int i = 0; i < 3; i++)
                        { double a = look.SecretPhase * 3 + i * Math.PI * 2 / 3; float x = (float)Math.Cos(a) * width * .35f, sy = -height - 6 * scale + (float)Math.Sin(a) * 6 * scale; g.DrawLine(stars, x - 3 * scale, sy, x + 3 * scale, sy); g.DrawLine(stars, x, sy - 3 * scale, x, sy + 3 * scale); }
                    }
                    if ((look.Secret & 8) != 0)
                    {
                        using (var note = new SolidBrush(Color.FromArgb(176, 111, 160)))
                        using (var stem = new Pen(Color.FromArgb(176, 111, 160), 2 * scale))
                        for (int side = -1; side <= 1; side += 2)
                        { float x = side * (width * .47f + 8 * scale), y = -height * .8f + (float)Math.Sin(look.SecretPhase * 5 + side) * 4 * scale; g.FillEllipse(note, x - 3 * scale, y, 6 * scale, 4 * scale); g.DrawLine(stem, x + 2 * scale, y + 2 * scale, x + 2 * scale, y - 10 * scale); g.DrawLine(stem, x + 2 * scale, y - 10 * scale, x + 7 * scale, y - 8 * scale); }
                    }
                }
            }
            finally { g.Restore(saved); }
        }
        private static void DrawHeart(Graphics g, float x, float y, float size, Pen outline)
        {
            using (var path = new GraphicsPath()) using (var fill = new SolidBrush(Color.FromArgb(235, 126, 154)))
            {
                path.AddBezier(x, y + size * .7f, x - size * 1.25f, y - size * .1f, x - size * .5f, y - size, x, y - size * .32f);
                path.AddBezier(x, y - size * .32f, x + size * .5f, y - size, x + size * 1.25f, y - size * .1f, x, y + size * .7f);
                g.FillPath(fill, path); g.DrawPath(outline, path);
            }
        }
    }
}
