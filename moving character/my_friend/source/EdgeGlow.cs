using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WalkingFriend
{
    internal sealed class EdgeGlowController : IDisposable
    {
        private readonly Form owner;
        private readonly List<EdgeGlowWindow> windows = new List<EdgeGlowWindow>();
        private readonly System.Windows.Forms.Timer pulse = new System.Windows.Forms.Timer { Interval = 40 };
        private readonly int theme, width, strength;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private bool disposed;
        internal bool Running { get { return !disposed; } }
        internal EdgeGlowController(Form owner, int theme = 0, int width = 1, int strength = 1)
        {
            this.owner = owner; this.theme = theme; this.width = width; this.strength = strength;
            pulse.Tick += delegate { if (owner.IsDisposed) { Dispose(); return; } byte alpha = PulseAlpha(clock.Elapsed.TotalSeconds, strength); foreach (var w in windows) w.SetGlowAlpha(alpha); };
            if (DesktopSurfaces.IsWindows) { Rebuild(); SystemEvents.DisplaySettingsChanged += DisplayChanged; }
            pulse.Start();
        }
        private void DisplayChanged(object sender, EventArgs e)
        {
            if (disposed || owner.IsDisposed || !owner.IsHandleCreated) return;
            try { owner.BeginInvoke((Action)delegate { if (!disposed) Rebuild(); }); } catch { }
        }
        private void Rebuild()
        {
            foreach (var w in windows) w.Dispose(); windows.Clear();
            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle r = screen.Bounds; int depth = Depth(r.Size, width);
                var parts = new[] { new Rectangle(r.Left, r.Top, r.Width, depth), new Rectangle(r.Left, r.Bottom - depth, r.Width, depth),
                    new Rectangle(r.Left, r.Top + depth, depth, Math.Max(1, r.Height - depth * 2)), new Rectangle(r.Right - depth, r.Top + depth, depth, Math.Max(1, r.Height - depth * 2)) };
                for (int i = 0; i < parts.Length; i++)
                {
                    try { var w = new EdgeGlowWindow(parts[i], r, depth, theme); windows.Add(w); w.Show(); w.SetGlowAlpha(PulseAlpha(0, strength)); }
                    catch (Exception error) { FeatureLog.Write(error); }
                }
            }
        }
        internal static int Depth(Size screen, int width)
        {
            double fraction = width <= 0 ? .08 : width == 1 ? .12 : .16;
            int shortest = Math.Min(screen.Width, screen.Height);
            return Math.Max(1, Math.Min(shortest / 3, Math.Min(360, Math.Max(12, (int)Math.Round(shortest * fraction)))));
        }
        internal static byte PulseAlpha(double seconds, int strength)
        {
            double middle = strength <= 0 ? 190 : strength == 1 ? 227.5 : 240;
            double amplitude = strength <= 0 ? 25 : strength == 1 ? 22.5 : 15;
            return (byte)Math.Round(middle + amplitude * Math.Sin(seconds * Math.PI * 2 / 3.4));
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; pulse.Stop(); pulse.Dispose();
            if (DesktopSurfaces.IsWindows) SystemEvents.DisplaySettingsChanged -= DisplayChanged;
            foreach (var w in windows) w.Dispose(); windows.Clear(); clock.Stop();
        }
    }

    internal sealed class EdgeGlowWindow : Form
    {
        private IntPtr dc, image, previous;
        private readonly Rectangle area;
        private byte previousAlpha;
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { var p = base.CreateParams; p.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST | 0x20; return p; }
        }
        internal EdgeGlowWindow(Rectangle area, Rectangle monitor, int depth, int theme)
        {
            this.area = area; AutoScaleMode = AutoScaleMode.None; FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual; Bounds = area;
            try
            {
                using (Bitmap bitmap = CreateGlowBitmap(area, monitor, depth, theme))
                {
                    byte[] bytes = LayerPixels.Premultiplied(bitmap); IntPtr screen = Native.GetDC(IntPtr.Zero);
                    try
                    {
                        dc = Native.CreateCompatibleDC(screen); if (dc == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                        var info = new BitmapInfo { Size = 40, Width = bitmap.Width, Height = -bitmap.Height, Planes = 1, Bits = 32, ImageSize = (uint)bytes.Length };
                        IntPtr bits; image = CreateDIBSection(screen, ref info, 0, out bits, IntPtr.Zero, 0);
                        if (image == IntPtr.Zero || bits == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                        Marshal.Copy(bytes, 0, bits, bytes.Length); previous = Native.SelectObject(dc, image);
                        if (previous == IntPtr.Zero || previous == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                    finally { Native.ReleaseDC(IntPtr.Zero, screen); }
                }
            }
            catch { Dispose(); throw; }
        }
        internal static Bitmap CreateGlowBitmap(Size size, int side)
        {
            int depth = side < 2 ? size.Height : size.Width;
            Rectangle monitor = side < 2 ? new Rectangle(0, 0, size.Width, Math.Max(depth * 4, size.Height)) : new Rectangle(0, 0, depth * 4, size.Height + depth * 2);
            Rectangle part = side == 0 ? new Rectangle(0, 0, size.Width, depth) : side == 1 ? new Rectangle(0, monitor.Height - depth, size.Width, depth)
                : side == 2 ? new Rectangle(0, depth, depth, size.Height) : new Rectangle(monitor.Width - depth, depth, depth, size.Height);
            return CreateGlowBitmap(part, monitor, depth, 0);
        }
        internal static Bitmap CreateGlowBitmap(Rectangle part, Rectangle monitor, int depth, int theme)
        {
            var bitmap = new Bitmap(Math.Max(1, part.Width), Math.Max(1, part.Height), PixelFormat.Format32bppArgb);
            var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var bytes = new byte[data.Stride * bitmap.Height];
                var palette = Palette(theme);
                for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++)
                {
                    int gx = part.Left - monitor.Left + x, gy = part.Top - monitor.Top + y;
                    int distance = Math.Min(Math.Min(gx, monitor.Width - 1 - gx), Math.Min(gy, monitor.Height - 1 - gy));
                    double t = Math.Max(0, Math.Min(1, distance / (double)Math.Max(1, depth - 1)));
                    int alpha = (int)Math.Round(255 * (.78 * Math.Pow(1 - t, 1.35) + .22 * Math.Exp(-distance / 3.0) * (1 - t)));
                    double position = .55 * gx / Math.Max(1.0, monitor.Width - 1) + .45 * gy / Math.Max(1.0, monitor.Height - 1);
                    double colorIndex = Math.Max(0, Math.Min(palette.Length - 1, position * (palette.Length - 1)));
                    int index = Math.Min(palette.Length - 2, (int)colorIndex); double mix = colorIndex - index;
                    Color a = palette[index], b = palette[index + 1]; int offset = y * data.Stride + x * 4;
                    bytes[offset] = (byte)Math.Round(a.B + (b.B - a.B) * mix);
                    bytes[offset + 1] = (byte)Math.Round(a.G + (b.G - a.G) * mix);
                    bytes[offset + 2] = (byte)Math.Round(a.R + (b.R - a.R) * mix);
                    bytes[offset + 3] = (byte)Math.Max(0, Math.Min(255, alpha));
                }
                Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
            }
            finally { bitmap.UnlockBits(data); }
            return bitmap;
        }
        private static Color[] Palette(int theme)
        {
            if (theme == 1) return new[] { Color.FromArgb(255, 186, 93), Color.FromArgb(255, 115, 137), Color.FromArgb(202, 111, 222), Color.FromArgb(133, 136, 249) };
            if (theme == 2) return new[] { Color.FromArgb(104, 240, 188), Color.FromArgb(107, 219, 249), Color.FromArgb(145, 154, 255), Color.FromArgb(224, 139, 249) };
            if (theme == 3) return new[] { Color.FromArgb(255, 117, 146), Color.FromArgb(255, 188, 98), Color.FromArgb(225, 230, 104), Color.FromArgb(106, 222, 157), Color.FromArgb(99, 197, 244), Color.FromArgb(170, 139, 248), Color.FromArgb(246, 138, 207) };
            return new[] { Color.FromArgb(255, 172, 117), Color.FromArgb(255, 143, 185), Color.FromArgb(181, 147, 255), Color.FromArgb(105, 205, 255) };
        }
        internal void SetGlowAlpha(byte alpha)
        {
            if (IsDisposed || dc == IntPtr.Zero || alpha == previousAlpha) return;
            IntPtr screen = Native.GetDC(IntPtr.Zero);
            try
            {
                var src = new Native.POINT(0, 0); var dst = new Native.POINT(area.Left, area.Top); var size = new Native.SIZE(area.Width, area.Height);
                var blend = new Native.BLENDFUNCTION { BlendOp = 0, SourceConstantAlpha = alpha, AlphaFormat = 1 };
                if (!Native.UpdateLayeredWindow(Handle, screen, ref dst, ref size, dc, ref src, 0, ref blend, 2)) throw new Win32Exception(Marshal.GetLastWin32Error());
                previousAlpha = alpha;
            }
            catch (Exception error) { FeatureLog.Write(error); }
            finally { Native.ReleaseDC(IntPtr.Zero, screen); }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84) { m.Result = new IntPtr(-1); return; }
            if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; }
            base.WndProc(ref m);
        }
        protected override void Dispose(bool disposing)
        {
            if (dc != IntPtr.Zero)
            {
                if (previous != IntPtr.Zero && previous != new IntPtr(-1)) Native.SelectObject(dc, previous);
                if (image != IntPtr.Zero) { Native.DeleteObject(image); image = IntPtr.Zero; }
                Native.DeleteDC(dc); dc = IntPtr.Zero;
            }
            base.Dispose(disposing);
        }
        [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
        { public uint Size; public int Width, Height; public ushort Planes, Bits; public uint Compression, ImageSize; public int XPels, YPels; public uint Used, Important, Color; }
        [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    }
}
