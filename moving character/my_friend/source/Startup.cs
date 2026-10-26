using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WalkingFriend
{
    internal static class StartupArt
    {
        private static readonly string Resource = "prepared-sprites.bin";
        internal static bool UsedPrepared;
        internal static void Export(string path)
        {
            Sprites.Load(1);
            var items = new Dictionary<string, Sprite>();
            items.Add("walk", Sprites.Walk1); items.Add("nap", Sprites.Nap); items.Add("wake", Sprites.Wake); items.Add("dance", Sprites.Dance);
            for (int i = 0; i < Sprites.WalkFrames.Length; i++) items.Add("walk/" + i, Sprites.WalkFrames[i]);
            items.Add("touch", FeatureArt.TouchCry); items.Add("weep", FeatureArt.IdleCry); items.Add("sit", FeatureArt.Sit); items.Add("slide", FeatureArt.Slide); items.Add("chase", FeatureArt.Chase);
            Add(items, "rotor", FeatureArt.Helicopter); Add(items, "climb", FeatureArt.Climb); Add(items, "ceiling", FeatureArt.Ceiling); Add(items, "held", FeatureArt.Held); Add(items, "eat", FeatureArt.Eating);
            for (int i = 0; i < Sprites.StopFrames.Length; i++) for (int j = 1; j < 8; j++)
            { items.Add("stop/" + i + "/" + j, Sprites.StopFrames[i][j]); items.Add("start/" + i + "/" + j, Sprites.StartFrames[i][j]); }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write("JeonjuChiikawa.PreparedSprites.v1"); w.Write(items.Count);
                foreach (var item in items)
                {
                    w.Write(item.Key);
                    foreach (Bitmap b in new[] { item.Value.Right, item.Value.Left })
                    {
                        using (var bytes = new MemoryStream()) { b.Save(bytes, ImageFormat.Png); byte[] data = bytes.ToArray(); w.Write(data.Length); w.Write(data); }
                    }
                }
            }
            Console.WriteLine("Prepared " + items.Count + " animation sprites");
        }
        private static void Add(Dictionary<string, Sprite> d, string prefix, Sprite[] sprites)
        { for (int i = 0; i < sprites.Length; i++) d.Add(prefix + "/" + i, sprites[i]); }
        internal static bool TryLoad(float dpi)
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Resource);
            if (stream == null) return false;
            using (stream)
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadString() != "JeonjuChiikawa.PreparedSprites.v1") throw new InvalidDataException("그림 준비 파일 형식이 맞지 않아요.");
                int count = reader.ReadInt32(); if (count < 50 || count > 1000) throw new InvalidDataException("그림 개수가 맞지 않아요.");
                var sprites = new Dictionary<string, Sprite>();
                for (int i = 0; i < count; i++)
                {
                    string name = reader.ReadString(); Bitmap[] pair = new Bitmap[2];
                    for (int j = 0; j < 2; j++)
                    {
                        int n = reader.ReadInt32(); if (n < 8 || n > 1000000) throw new InvalidDataException("그림 크기가 맞지 않아요.");
                        byte[] data = reader.ReadBytes(n); if (data.Length != n) throw new EndOfStreamException();
                        using (var png = new MemoryStream(data))
                        using (var b = new Bitmap(png))
                        {
                            int w = Math.Max(1, (int)Math.Round(b.Width * dpi)), h = Math.Max(1, (int)Math.Round(b.Height * dpi));
                            pair[j] = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                            using (Graphics g = Graphics.FromImage(pair[j]))
                            {
                                g.Clear(Color.Transparent); FeatureArt.Quality(g);
                                if (dpi == 1) g.DrawImageUnscaled(b, 0, 0); else g.DrawImage(b, new Rectangle(0, 0, w, h), 0, 0, b.Width, b.Height, GraphicsUnit.Pixel);
                            }
                        }
                    }
                    sprites.Add(name, new Sprite { Right = pair[0], Left = pair[1] });
                }
                Sprites.Scale = dpi; Sprites.Walk1 = sprites["walk"]; Sprites.Walk2 = Sprites.Walk1; Sprites.Nap = sprites["nap"]; Sprites.Wake = sprites["wake"]; Sprites.Dance = sprites["dance"];
                Sprites.WalkFrames = Read(sprites, "walk", 12);
                FeatureArt.TouchCry = sprites["touch"]; FeatureArt.IdleCry = sprites["weep"]; FeatureArt.Sit = sprites["sit"]; FeatureArt.Slide = sprites["slide"]; FeatureArt.Chase = sprites["chase"];
                FeatureArt.Helicopter = Read(sprites, "rotor", 24); FeatureArt.Climb = Read(sprites, "climb", 12); FeatureArt.Ceiling = Read(sprites, "ceiling", 12); FeatureArt.Held = Read(sprites, "held", 12); FeatureArt.Eating = Read(sprites, "eat", 4);
                FeatureArt.CeilingTopInset = FeatureArt.Bounds(FeatureArt.Ceiling[0].Right).Top;
                Sprites.StopFrames = new Sprite[12][]; Sprites.StartFrames = new Sprite[12][];
                for (int i = 0; i < 12; i++)
                {
                    Sprites.StopFrames[i] = new Sprite[9]; Sprites.StartFrames[i] = new Sprite[9];
                    Sprites.StopFrames[i][0] = Sprites.WalkFrames[i]; Sprites.StopFrames[i][8] = Sprites.Wake;
                    Sprites.StartFrames[i][0] = Sprites.Wake; Sprites.StartFrames[i][8] = Sprites.WalkFrames[i];
                    for (int j = 1; j < 8; j++) { Sprites.StopFrames[i][j] = sprites["stop/" + i + "/" + j]; Sprites.StartFrames[i][j] = sprites["start/" + i + "/" + j]; }
                }
                UsedPrepared = true; return true;
            }
        }
        private static Sprite[] Read(Dictionary<string, Sprite> d, string prefix, int count)
        { var result = new Sprite[count]; for (int i = 0; i < count; i++) result[i] = d[prefix + "/" + i]; return result; }
    }

    internal static class StartupDiagnostics
    {
        internal static string Phase = "시작 준비";
        internal static string LogPath;
        internal static void Mark(string phase)
        {
            Phase = phase;
            if (LogPath == null)
            {
                foreach (string dir in new[] { AppDomain.CurrentDomain.BaseDirectory, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "걸어다니는친구") })
                    try { Directory.CreateDirectory(dir); string p = Path.Combine(dir, "걸어다니는친구_실행기록.txt"); File.WriteAllText(p, "걸어다니는 친구 1.0\r\n"); LogPath = p; break; } catch { }
            }
            try { if (LogPath != null) File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + phase + Environment.NewLine); } catch { }
        }
        internal static void Failure(Exception error)
        {
            string phase = Phase; FeatureLog.Write(error);
            Mark("실패: " + phase + "\r\n" + error);
            try { MessageBox.Show("친구를 화면에 표시하지 못했어.\n\n멈춘 부분: " + phase + "\n" + error.Message + "\n\n실행 기록: " + (LogPath ?? "저장하지 못함"), "걸어다니는 친구 · 실행 확인", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
        }
    }

    internal static class LayerPixels
    {
        internal static byte[] Premultiplied(Bitmap bitmap)
        {
            int rowSize = bitmap.Width * 4; byte[] bytes = new byte[rowSize * bitmap.Height];
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < bitmap.Height; y++) Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), bytes, y * rowSize, rowSize);
            }
            finally { bitmap.UnlockBits(data); }
            for (int i = 0; i < bytes.Length; i += 4)
            { int a = bytes[i + 3]; bytes[i] = (byte)((bytes[i] * a + 127) / 255); bytes[i + 1] = (byte)((bytes[i + 1] * a + 127) / 255); bytes[i + 2] = (byte)((bytes[i + 2] * a + 127) / 255); }
            return bytes;
        }
    }

    internal sealed partial class PetWindow
    {
        private bool conventionalDisplay;
        private Bitmap conventionalFrame;
        private bool displayFailureReported;
        private bool firstPresented;
        internal bool HasPresented { get { return firstPresented; } }
        private void PresentBitmap(Bitmap bitmap, int x, int y)
        {
            if (!DesktopSurfaces.IsWindows || conventionalDisplay) { Conventional(bitmap, x, y); firstPresented = true; return; }
            try { Alpha(bitmap, x, y); firstPresented = true; }
            catch (Exception e)
            {
                FeatureLog.Write(e); StartupDiagnostics.Mark("투명 표시 대체: " + e.Message);
                Conventional(bitmap, x, y); firstPresented = true;
                if (!displayFailureReported) { displayFailureReported = true; }
            }
        }
        private void Conventional(Bitmap bitmap, int x, int y)
        {
            if (!conventionalDisplay)
            {
                conventionalDisplay = true; BackColor = Color.Magenta; TransparencyKey = Color.Magenta;
                DoubleBuffered = true;
            }
            Bitmap previous = conventionalFrame; conventionalFrame = (Bitmap)bitmap.Clone();
            ClientSize = bitmap.Size; Location = new Point(x, y); Invalidate(); Update(); if (previous != null) previous.Dispose();
        }
        private void PaintConventional(PaintEventArgs e)
        { if (conventionalFrame != null) { e.Graphics.Clear(Color.Magenta); e.Graphics.DrawImageUnscaled(conventionalFrame, 0, 0); } }
        private void MovePresented(int x, int y)
        { if (conventionalDisplay || !DesktopSurfaces.IsWindows) Location = new Point(x, y); else Native.SetWindowPos(Handle, IntPtr.Zero, x, y, 0, 0, 533u); }
        [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
        {
            public uint Size; public int Width, Height; public ushort Planes, Bits; public uint Compression, ImageSize;
            public int XPels, YPels; public uint Used, Important, Color;
        }
        [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        private void Alpha(Bitmap bitmap, int x, int y)
        {
            IntPtr screen = Native.GetDC(IntPtr.Zero); if (screen == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            IntPtr dc = IntPtr.Zero, image = IntPtr.Zero, previous = IntPtr.Zero;
            try
            {
                dc = Native.CreateCompatibleDC(screen); if (dc == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                byte[] bytes = LayerPixels.Premultiplied(bitmap);
                var info = new BitmapInfo { Size = 40, Width = bitmap.Width, Height = -bitmap.Height, Planes = 1, Bits = 32, Compression = 0, ImageSize = (uint)bytes.Length };
                IntPtr bits; image = CreateDIBSection(screen, ref info, 0, out bits, IntPtr.Zero, 0);
                if (image == IntPtr.Zero || bits == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                Marshal.Copy(bytes, 0, bits, bytes.Length); previous = Native.SelectObject(dc, image);
                if (previous == IntPtr.Zero || previous == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
                var size = new Native.SIZE(bitmap.Width, bitmap.Height); var src = new Native.POINT(0, 0); var dst = new Native.POINT(x, y);
                var blend = new Native.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
                if (!Native.UpdateLayeredWindow(Handle, screen, ref dst, ref size, dc, ref src, 0, ref blend, 2)) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                if (previous != IntPtr.Zero && previous != new IntPtr(-1) && dc != IntPtr.Zero) Native.SelectObject(dc, previous);
                if (image != IntPtr.Zero) Native.DeleteObject(image); if (dc != IntPtr.Zero) Native.DeleteDC(dc); Native.ReleaseDC(IntPtr.Zero, screen);
            }
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        { if (conventionalFrame != null) { conventionalFrame.Dispose(); conventionalFrame = null; } base.OnFormClosed(e); }
    }

    internal sealed partial class App
    {
        private double forceVisibleUntil = 12;
        internal List<PetWindow> LiveWindows { get { return windows; } }
        internal void RecoverVisible()
        {
            hidden = false; autoHidden = false; forceVisibleUntil = Now + 15;
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            if (windows.Count == 0) Spawn();
            int index = 0;
            foreach (var w in windows)
            {
                w.Pet.ReleaseFeatures(this); w.Pet.Area = area; w.Pet.Hidden = false;
                w.Pet.X = area.Left + area.Width / 2.0 + (index++ - (windows.Count - 1) / 2.0) * Sprites.Walk1.W;
                w.Pet.Y = area.Top + FeatureArt.Helicopter[0].H + 10; w.Pet.Vy = 0; w.Pet.SetState(PetState.Falling, this);
                w.Invalidate2(); w.Show(); w.Present(Now); w.KeepOnTop();
            }
            lastTick = Now; UpdateMenus();
        }
    }
}
