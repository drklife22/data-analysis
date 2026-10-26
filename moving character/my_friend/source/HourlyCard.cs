using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WalkingFriend
{
    internal sealed class HourlyNotice : Form
    {
        private readonly Label header, caption, clock, message;
        private readonly Button confirm, close;
        private readonly List<Font> ownedFonts = new List<Font>();
        private readonly float scale;
        private bool edgeRequested;
        private EdgeGlowController glow;
        private int glowTheme, glowWidth = 1, glowStrength = 1;
        internal bool GlowRunning { get { return glow != null && glow.Running; } }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        { get { var p = base.CreateParams; p.ExStyle |= Native.WS_EX_NOACTIVATE; if (DesktopSurfaces.IsWindows) p.ClassStyle |= 0x20000; return p; } }
        internal HourlyNotice(Icon icon)
        {
            scale = Math.Max(1, Native.SystemDpiScale()); AutoScaleMode = AutoScaleMode.None;
            Text = "걸어다니는 친구 · 정각 알림"; FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = true; TopMost = true; MaximizeBox = false; MinimizeBox = false; DoubleBuffered = true;
            BackColor = Color.FromArgb(255, 253, 249); ForeColor = Color.FromArgb(45, 48, 64); if (icon != null) Icon = icon;
            header = LabelAt("걸어다니는 친구", 24, 21, 264, 22, 9, Color.FromArgb(110, 103, 115));
            caption = LabelAt("정각 알림", 24, 58, 290, 19, 9, Color.FromArgb(153, 117, 92));
            clock = LabelAt("00:00", 20, 77, 310, 72, 38, ForeColor); clock.Font = OwnFont("Segoe UI", 38, FontStyle.Regular);
            message = LabelAt("", 24, 155, 312, 46, 10, Color.FromArgb(91, 87, 101));
            close = new CardButton(true) { Text = "×", Name = "close", AccessibleName = "알림 닫기", Bounds = Rect(311, 15, 32, 32), Font = OwnFont("Segoe UI", 14, FontStyle.Regular) };
            close.Click += delegate { Close(); }; Controls.Add(close);
            confirm = new CardButton(false) { Text = "확인했어", Name = "confirm", Bounds = Rect(239, 218, 97, 38), Font = OwnFont("Malgun Gothic", 9.5f, FontStyle.Regular) };
            confirm.Click += delegate { Close(); }; Controls.Add(confirm); AcceptButton = confirm; CancelButton = close;
            ClientSize = new Size(Pixel(360), Pixel(277)); header.MouseDown += DragCard;
            MouseDown += DragCard; caption.MouseDown += DragCard;
            SetMessage("지금은 " + DateTime.Now.ToString("tt h") + "시야!");
        }
        private int Pixel(float value) { return (int)Math.Round(value * scale); }
        private Rectangle Rect(float x, float y, float width, float height) { return new Rectangle(Pixel(x), Pixel(y), Pixel(width), Pixel(height)); }
        private Font OwnFont(string name, float size, FontStyle style)
        { var f = new Font(name, size * scale, style, GraphicsUnit.Point); ownedFonts.Add(f); return f; }
        private Label LabelAt(string text, float x, float y, float width, float height, float size, Color color)
        {
            var label = new Label { Text = text, Bounds = Rect(x, y, width, height), ForeColor = color, BackColor = BackColor, Font = OwnFont("Malgun Gothic", size, FontStyle.Regular) };
            Controls.Add(label); return label;
        }
        internal void SetMessage(string text)
        {
            bool timer = Text.Contains("타이머"); DateTime now = DateTime.Now;
            caption.Text = timer ? "타이머" : "정각 알림"; clock.Text = timer ? "시간이 됐어" : now.Hour.ToString("00") + ":00";
            clock.Font = timer ? OwnFont("Malgun Gothic", 23, FontStyle.Regular) : clock.Font;
            string friendly = (text ?? "").Replace("지금은 ", "").Trim();
            message.Text = friendly;
            int maximum = Math.Max(Pixel(46), Screen.FromPoint(Cursor.Position).WorkingArea.Height / 3);
            Size measured = TextRenderer.MeasureText(friendly, message.Font, new Size(Pixel(312), maximum), TextFormatFlags.WordBreak);
            message.Height = Math.Min(maximum, Math.Max(Pixel(46), measured.Height + Pixel(5)));
            message.AutoEllipsis = true; confirm.Top = message.Bottom + Pixel(17); ClientSize = new Size(Pixel(360), confirm.Bottom + Pixel(21));
            if (!Visible) PositionCard();
        }
        private void PositionCard()
        {
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(area.Left + Math.Max(0, (area.Width - Width) / 2), area.Top + Math.Min(Pixel(48), Math.Max(0, area.Height - Height)));
        }
        internal void EnableEdgeGlow(bool enable, int theme = 0, int width = 1, int strength = 1)
        {
            bool changed = glowTheme != theme || glowWidth != width || glowStrength != strength;
            glowTheme = theme; glowWidth = width; glowStrength = strength; edgeRequested = enable;
            if (!enable || changed) StopGlow();
            if (enable && Visible && glow == null) glow = new EdgeGlowController(this, glowTheme, glowWidth, glowStrength);
        }
        private void StopGlow() { if (glow != null) { glow.Dispose(); glow = null; } }
        protected override void OnShown(EventArgs e)
        { base.OnShown(e); if (edgeRequested && glow == null) glow = new EdgeGlowController(this, glowTheme, glowWidth, glowStrength); }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e); if (ClientSize.Width < 2 || ClientSize.Height < 2) return;
            using (var path = Rounded(new RectangleF(0, 0, ClientSize.Width - 1, ClientSize.Height - 1), 21 * Math.Max(1, scale)))
            { Region old = Region; Region = new Region(path); if (old != null) old.Dispose(); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Rounded(new RectangleF(.6f, .6f, ClientSize.Width - 1.2f, ClientSize.Height - 1.2f), 21 * scale))
            using (var pen = new Pen(Color.FromArgb(226, 219, 213), 1)) e.Graphics.DrawPath(pen, path);
        }
        protected override void OnFormClosed(FormClosedEventArgs e) { StopGlow(); base.OnFormClosed(e); }
        protected override void Dispose(bool disposing)
        { StopGlow(); base.Dispose(disposing); if (disposing) { foreach (var f in ownedFonts) f.Dispose(); ownedFonts.Clear(); } }
        private void DragCard(object sender, MouseEventArgs e)
        { if (e.Button != MouseButtons.Left || !DesktopSurfaces.IsWindows) return; ReleaseCapture(); SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero); }
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
        internal static GraphicsPath Rounded(RectangleF rect, float radius)
        {
            var path = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
        private sealed class CardButton : Button
        {
            private readonly bool icon; private bool hover;
            internal CardButton(bool icon) { this.icon = icon; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); }
            protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Parent == null ? Color.FromArgb(255, 253, 249) : Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color bg = icon ? (hover ? Color.FromArgb(242, 236, 229) : Color.FromArgb(255, 253, 249)) : (hover ? Color.FromArgb(78, 81, 101) : Color.FromArgb(60, 63, 81));
                using (var path = Rounded(new RectangleF(0, 0, Width - 1, Height - 1), icon ? 8 : 12))
                using (var brush = new SolidBrush(bg)) e.Graphics.FillPath(brush, path);
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, icon ? Color.FromArgb(108, 103, 114) : Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5));
            }
        }
    }
}
