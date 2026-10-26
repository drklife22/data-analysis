using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace WalkingFriend
{
    internal static class StartupSmoke
    {
        internal static void Run(string dir)
        {
            Directory.CreateDirectory(dir);
            FriendData.StorageOverride = Path.Combine(dir, "data"); FriendData.Name = "startup-check.xml"; FriendData.Current = new FriendData();
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            PlayOptions.Current = new PlayOptions { Walls = false, Ceiling = false, Windows = false, Escape = false, Idle = false, Fullscreen = false };
            Sprites.Load(1.25f);
            if (!StartupArt.UsedPrepared) throw new Exception("Startup should use prepared animation assets");
            using (Bitmap sample = new Bitmap(2, 1, PixelFormat.Format32bppArgb))
            {
                sample.SetPixel(0, 0, Color.FromArgb(128, 255, 64, 32)); sample.SetPixel(1, 0, Color.FromArgb(0, 255, 255, 255));
                byte[] bytes = LayerPixels.Premultiplied(sample);
                if (bytes[0] != 16 || bytes[1] != 32 || bytes[2] != 128 || bytes[3] != 128 || bytes[4] != 0 || bytes[5] != 0 || bytes[6] != 0 || bytes[7] != 0) throw new Exception("Layered window alpha conversion failed");
            }
            var app = new App(); app.Settings.HourlyOn = false; app.Settings.LunchOn = false; app.Settings.LeaveOn = false;
            Exception failure = null; int count = 0;
            var timer = new System.Windows.Forms.Timer { Interval = 200 };
            timer.Tick += delegate
            {
                if (++count < 5) return;
                timer.Stop();
                try
                {
                    if (app.LiveWindows.Count != 1) throw new Exception("Startup did not create a pet window");
                    var pet = app.LiveWindows[0];
                    if (!pet.Visible || !pet.HasPresented || !Screen.PrimaryScreen.Bounds.IntersectsWith(pet.Bounds)) throw new Exception("Startup window is not visible on the monitor");
                    using (Bitmap b = new Bitmap(pet.Width, pet.Height))
                    { using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(pet.Location, Point.Empty, b.Size); b.Save(Path.Combine(dir, "startup_visible.png"), ImageFormat.Png); }
                    TestThrowInput(app, pet);
                    pet.Hide(); app.RecoverVisible(); Application.DoEvents();
                    if (!pet.Visible || !pet.HasPresented || Math.Abs(pet.Pet.X - Screen.PrimaryScreen.WorkingArea.Width / 2.0) > 5) throw new Exception("Visible recovery failed");
                    File.WriteAllText(Path.Combine(dir, "startup_checks.txt"), "PASS prepared animation resource loaded\nPASS per-pixel alpha converted correctly\nPASS actual App constructor creates visible pet\nPASS first frame is presented\nPASS real window mouse handlers start throw flight\nPASS hidden pet is recovered to monitor\n");
                    Console.WriteLine("Startup checks passed: actual App window shown and recovered.");
                }
                catch (Exception e) { failure = e; }
                finally { app.Quit(); }
            };
            timer.Start(); Application.Run(app); timer.Dispose(); if (failure != null) throw failure;
        }
        private static void TestThrowInput(App app, PetWindow window)
        {
            var p = window.Pet; p.X = p.Area.Left + p.Area.Width * .4; p.Y = p.Area.Top + p.Area.Height * .5; p.SetState(PetState.Walk, app);
            var start = new Point((int)p.X, (int)p.Y - 20); Cursor.Position = start;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(PetWindow).GetMethod("OnMouseDown", flags).Invoke(window, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0) });
            for (int i = 1; i <= 6; i++)
            {
                System.Threading.Thread.Sleep(16); Cursor.Position = new Point(start.X + i * 20, start.Y - i * 3);
                typeof(PetWindow).GetMethod("OnMouseMove", flags).Invoke(window, new object[] { new MouseEventArgs(MouseButtons.Left, 0, 10, 10, 0) });
            }
            typeof(PetWindow).GetMethod("OnMouseUp", flags).Invoke(window, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0) });
            if (p.State != PetState.Thrown) throw new Exception("Window drag release did not start throw flight");
            typeof(PetWindow).GetMethod("OnMouseCaptureChanged", flags).Invoke(window, new object[] { EventArgs.Empty });
            if (p.State != PetState.Thrown) throw new Exception("Mouse capture release reset the throw");
        }
    }
}
