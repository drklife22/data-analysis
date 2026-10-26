using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("마루는 강쥐")]
[assembly: AssemblyProduct("마루는 강쥐")]
[assembly: AssemblyFileVersion("2.0.6.0")]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: AssemblyVersion("2.0.6.0")]
namespace WalkingFriend
{
	internal sealed partial class App : ApplicationContext, IWorld
	{
		public const int MaxPets = 16;

		public const string Title = "마루는 강쥐";

		private readonly List<PetWindow> windows = new List<PetWindow>();

		private readonly Random rng = new Random();

		private readonly Stopwatch clock = Stopwatch.StartNew();

		private readonly System.Windows.Forms.Timer timer;

		private List<Rectangle> areas = new List<Rectangle>();

		private double lastTick;

		private double lastSlow;

		private double lastTop;

		private bool hidden;

		private bool paused;

		private bool locked;

		private NotifyIcon tray;

		private ToolStripMenuItem trayHideItem;

		private SettingsForm settingsForm;

		public Settings Settings;

		public Icon TrayIconImage;

		private DateTime lunchDone = DateTime.MinValue;

		private DateTime leaveGatherDone = DateTime.MinValue;

		private DateTime leaveDone = DateTime.MinValue;

		private double previewLeaveAt = -1.0;

		private string previewLeaveMsg;

		private bool napping;

		private DateTime hourlyLast;

		private string hourlyPendingText;

		private double hourlyPendingExpiry;

		private int hourlyActivityVersion;

		private HourlyNotice hourlyNotice;

		public List<Rectangle> Areas
		{
			get
			{
				return areas;
			}
		}

		public Random Rng
		{
			get
			{
				return rng;
			}
		}

		public double Now
		{
			get
			{
				return clock.Elapsed.TotalSeconds;
			}
		}

		public App()
		{
			Settings = Settings.Load();
			RefreshAreas();
			TrayIconImage = Sprites.MakeTrayIcon();
			BuildTray();
			SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
			SystemEvents.SessionSwitch += OnSessionSwitch;
			timer = new System.Windows.Forms.Timer
			{
				Interval = 30
			};
			timer.Tick += OnTick;
			lastTick = Now;
			timer.Start();
			Spawn();
			StartupOption.RebindIfEnabled();
		}

		private void RefreshAreas()
		{
			List<Rectangle> list = new List<Rectangle>();
			try
			{
				Screen[] allScreens = Screen.AllScreens;
				foreach (Screen screen in allScreens)
				{
					Rectangle workingArea = screen.WorkingArea;
					if (workingArea.Width > 0 && workingArea.Height > 0)
					{
						list.Add(workingArea);
					}
				}
			}
			catch
			{
			}
			if (list.Count == 0)
			{
				list.Add(new Rectangle(0, 0, 1280, 720));
			}
			areas = list;
			foreach (PetWindow window in windows)
			{
				Pet pet = window.Pet;
				if (!areas.Contains(pet.Area))
				{
					pet.Area = Pet.FindArea(this, pet.X, Math.Min(pet.Y, pet.Area.Bottom));
				}
				if (pet.X < (double)pet.Area.Left)
				{
					pet.X = pet.Area.Left + 20;
				}
				if (pet.X > (double)pet.Area.Right)
				{
					pet.X = pet.Area.Right - 20;
				}
				if (pet.Y > (double)pet.Area.Bottom && pet.State != PetState.Drag)
				{
					pet.Y = pet.Area.Bottom;
				}
			}
		}

		private void OnDisplayChanged(object sender, EventArgs e)
		{
			RefreshAreas();
		}

		private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
		{
			if (e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.ConsoleDisconnect || e.Reason == SessionSwitchReason.RemoteDisconnect)
			{
				locked = true;
			}
			else if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.ConsoleConnect || e.Reason == SessionSwitchReason.RemoteConnect)
			{
				locked = false;
			}
		}

		public void Spawn()
		{
			if (windows.Count < 16)
			{
				Rectangle area;
				try
				{
					area = Screen.PrimaryScreen.WorkingArea;
				}
				catch
				{
					area = areas[0];
				}
				double num = Sprites.Walk1.W;
				double x = (double)area.Left + num + rng.NextDouble() * Math.Max(1.0, (double)area.Width - num * 2.0);
				double y = (double)area.Top + (double)Sprites.Walk1.H * 0.6;
				Pet pet = new Pet(this, x, y);
				pet.Area = area;
				pet.Hidden = hidden || autoHidden;
				PetWindow petWindow = new PetWindow(this, pet);
				windows.Add(petWindow);
				if (!hidden && !autoHidden)
				{
					petWindow.Show();
					petWindow.Present(Now);
				}
				UpdateMenus();
			}
		}

		public void Remove(PetWindow w)
		{
			windows.Remove(w);
			try
			{
				w.Close();
				w.Dispose();
			}
			catch
			{
			}
			UpdateMenus();
		}

		public void BringToFront(PetWindow w)
		{
			w.KeepOnTop();
		}

		public void ToggleHidden()
		{
			hidden = !hidden;
			foreach (PetWindow window in windows)
			{
				window.Pet.Hidden = hidden || autoHidden;
				if (hidden || autoHidden)
				{
					window.Hide();
					continue;
				}
				window.Show();
				window.Invalidate2();
				window.Present(Now);
				window.KeepOnTop();
			}
			if (!hidden && windows.Count == 0)
			{
				Spawn();
			}
			lastTick = Now;
			UpdateMenus();
		}

		private void UpdateMenus()
		{
			if (trayHideItem != null)
			{
				trayHideItem.Text = ((!hidden) ? "모두 숨기기" : "모두 보이기");
			}
		}

		private void BuildTray()
		{
			ContextMenuStrip menu = new ContextMenuStrip();
			menu.Items.Add("친구 한 마리 더 부르기", null, delegate
			{
				if (hidden)
				{
					ToggleHidden();
				}
				Spawn();
			});
			trayHideItem = new ToolStripMenuItem("모두 숨기기", null, delegate
			{
				ToggleHidden();
			});
			menu.Items.Add(trayHideItem);
			menu.Items.Add(new ToolStripSeparator());
			menu.Items.Add("알림 설정...", null, delegate
			{
				OpenSettings();
			});
			menu.Items.Add(new ToolStripSeparator());
			menu.Items.Add("종료", null, delegate
			{
				Quit();
			});
			AddFeatureMenu(menu, null);
			menu.Opening += delegate
			{
				menu.Items[0].Enabled = windows.Count < 16;
			};
			tray = new NotifyIcon
			{
				Icon = TrayIconImage,
				Text = "마루는 강쥐",
				ContextMenuStrip = menu,
				Visible = true
			};
			tray.DoubleClick += delegate
			{
				ToggleHidden();
			};
		}

		public void ShowPetMenu(PetWindow w, Point at)
		{
			ContextMenuStrip menu = new ContextMenuStrip();
			ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem("한 마리 더 부르기", null, delegate
			{
				Spawn();
			});
			toolStripMenuItem.Enabled = windows.Count < 16;
			menu.Items.Add(toolStripMenuItem);
			menu.Items.Add("이 친구 보내기", null, delegate
			{
				Remove(w);
			});
			menu.Items.Add("모두 숨기기", null, delegate
			{
				if (!hidden)
				{
					ToggleHidden();
				}
			});
			menu.Items.Add(new ToolStripSeparator());
			menu.Items.Add("알림 설정...", null, delegate
			{
				OpenSettings();
			});
			menu.Items.Add("종료", null, delegate
			{
				Quit();
			});
			AddFeatureMenu(menu, w.Pet);
			menu.Closed += delegate
			{
				BeginInvokeSafe(delegate
				{
					menu.Dispose();
				});
			};
			try
			{
				Native.SetForegroundWindow(w.Handle);
			}
			catch
			{
			}
			menu.Show(at);
		}

		private void BeginInvokeSafe(Action a)
		{
			System.Windows.Forms.Timer t = new System.Windows.Forms.Timer
			{
				Interval = 50
			};
			t.Tick += delegate
			{
				t.Stop();
				t.Dispose();
				try
				{
					a();
				}
				catch
				{
				}
			};
			t.Start();
		}

		private void OpenSettings()
		{
			if (settingsForm != null && !settingsForm.IsDisposed)
			{
				settingsForm.Activate();
				return;
			}
			settingsForm = new SettingsForm(this);
			settingsForm.FormClosed += delegate
			{
				settingsForm = null;
			};
			settingsForm.Show();
			settingsForm.Activate();
		}

		public void OnSettingsChanged()
		{
			ClearBubbleHourly();
			DateTime now = DateTime.Now;
			TimeSpan? timeSpan = Settings.ParseTime(Settings.LunchTime);
			if (timeSpan.HasValue && now.TimeOfDay > timeSpan.Value.Add(TimeSpan.FromSeconds(60.0)))
			{
				lunchDone = now.Date;
			}
			else if (lunchDone == now.Date)
			{
				lunchDone = DateTime.MinValue;
			}
			TimeSpan? timeSpan2 = Settings.ParseTime(Settings.LeaveTime);
			if (timeSpan2.HasValue && now.TimeOfDay > timeSpan2.Value.Add(TimeSpan.FromSeconds(60.0)))
			{
				leaveDone = now.Date;
				leaveGatherDone = now.Date;
			}
			else if (leaveDone == now.Date)
			{
				leaveDone = DateTime.MinValue;
				leaveGatherDone = DateTime.MinValue;
			}
		}

		public void PreviewLunch(string msg)
		{
			if (hidden)
			{
				ToggleHidden();
			}
			if (windows.Count == 0)
			{
				Spawn();
			}
			foreach (PetWindow window in windows)
			{
				window.Pet.Cheer(this, (!string.IsNullOrEmpty(msg)) ? msg : Settings.LunchMsg, 8.0);
			}
		}

		public void PreviewLeave(string msg)
		{
			if (hidden)
			{
				ToggleHidden();
			}
			if (windows.Count == 0)
			{
				Spawn();
			}
			previewLeaveAt = Now + 10.0;
			previewLeaveMsg = ((!string.IsNullOrEmpty(msg)) ? msg : Settings.LeaveMsg);
			StartGather(previewLeaveAt);
		}

		private void StartGather(double deadline)
		{
			foreach (IGrouping<Rectangle, Pet> item in from w in windows
				select w.Pet into p
				where p.CanReact
				group p by p.Area)
			{
				List<Pet> list = item.OrderBy((Pet p) => p.X).ToList();
				double num = (double)Sprites.Walk1.W * 0.95;
				double num2 = (double)item.Key.Left + (double)item.Key.Width / 2.0;
				for (int i = 0; i < list.Count; i++)
				{
					double targetX = num2 + ((double)i - (double)(list.Count - 1) / 2.0) * num;
					list[i].Gather(this, targetX, deadline);
				}
			}
		}

		private void CheerAll(string msg)
		{
			foreach (PetWindow window in windows)
			{
				window.Pet.Cheer(this, msg, 8.0);
			}
		}

		private void OnTick(object sender, EventArgs e)
		{
			double now = Now;
			double num = now - lastTick;
			lastTick = now;
			if (num > 0.1)
			{
				num = 0.1;
			}
			if (num < 0.0)
			{
				num = 0.0;
			}
			if (now - lastSlow >= 1.0)
			{
				lastSlow = now;
				SlowTick(now);
			}
			CompanionTick(now, !paused);
			if (!paused) FriendData.Current.PollTimer(this);
			if (paused || hidden)
			{
				return;
			}
			CheckAlarms(now);
			MaintainHourly(now);
			if (!FeatureTick(now)) return;
			List<Pet> all = windows.Select((PetWindow w) => w.Pet).ToList();
			double mouseX = Cursor.Position.X;
			PetWindow[] array = windows.ToArray();
            var oldPositions = all.ToDictionary(p => p, p => new PointF((float)p.X, (float)p.Y));
			foreach (PetWindow petWindow in array)
			{
				try
				{
					petWindow.Pet.Update(this, num, all, mouseX);

				}
				catch (Exception error)
				{ FeatureLog.Write(error); }
			}
            ResolveCompanionContacts(this, all, oldPositions);
            CheckSecretDance(now, all);
            foreach (PetWindow window in array) try { window.Present(now); } catch (Exception error) { FeatureLog.Write(error); }
			if (!(now - lastTop >= 2.0))
			{
				return;
			}
			lastTop = now;
			foreach (PetWindow window in windows)
			{
				window.KeepOnTop();
			}
		}

		private void SlowTick(double now)
		{
			bool flag = Native.ScreenSaverRunning();
			bool flag2 = paused;
			paused = locked || flag;
			if (flag2 && !paused)
			{
				lastTick = Now;
			}
			RefreshAreas();
			if (paused || hidden)
			{
				return;
			}
			double num = Native.IdleSeconds();
			if (Settings.NapOn && num >= (double)Settings.NapMinutes * 60.0)
			{
				napping = true;
				foreach (PetWindow window in windows)
				{
					window.Pet.FallAsleep(this);
				}
			}
			else
			{
				if (!(num < 2.0) || !napping)
				{
					return;
				}
				napping = false;
				foreach (PetWindow window2 in windows)
				{
					window2.Pet.WakeUp(this);
				}
			}
		}

		private void CheckAlarms(double now)
		{
			if (previewLeaveAt > 0.0 && now >= previewLeaveAt)
			{
				previewLeaveAt = -1.0;
				CheerAll(previewLeaveMsg);
			}
			DateTime now2 = DateTime.Now;
			TimeSpan timeOfDay = now2.TimeOfDay;
			DateTime date = now2.Date;
			if (Settings.LunchOn && lunchDone != date)
			{
				TimeSpan? timeSpan = Settings.ParseTime(Settings.LunchTime);
				if (timeSpan.HasValue && timeOfDay >= timeSpan.Value && timeOfDay < timeSpan.Value + TimeSpan.FromSeconds(60.0))
				{
					lunchDone = date;
					napping = false;
					CheerAll(Settings.LunchMsg);
				}
			}
			if (Settings.LeaveOn)
			{
				TimeSpan? timeSpan2 = Settings.ParseTime(Settings.LeaveTime);
				if (timeSpan2.HasValue)
				{
					TimeSpan t = timeSpan2.Value - TimeSpan.FromSeconds(10.0);
					if (leaveGatherDone != date && timeOfDay >= t && timeOfDay < timeSpan2.Value)
					{
						leaveGatherDone = date;
						napping = false;
						StartGather(now + (timeSpan2.Value - timeOfDay).TotalSeconds);
					}
					if (leaveDone != date && timeOfDay >= timeSpan2.Value && timeOfDay < timeSpan2.Value + TimeSpan.FromSeconds(60.0))
					{
						leaveDone = date;
						leaveGatherDone = date;
						napping = false;
						CheerAll(Settings.LeaveMsg);
					}
				}
			}
			if (OptionsHourlyDue(now2))
			{
				napping = false;
				BeginHourly(HourlyMessage(now2, Settings.LunchOn, Settings.LunchTime, Settings.LunchMsg, Settings.LeaveOn, Settings.LeaveTime, Settings.LeaveMsg));
			}
		}

		public void Quit()
		{
			CloseFeatureWindows();
			ClearHourly();
			ActivityInput.Stop();
			timer.Stop();
			SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
			SystemEvents.SessionSwitch -= OnSessionSwitch;
			if (tray != null)
			{
				tray.Visible = false;
				tray.Dispose();
				tray = null;
			}
			PetWindow[] array = windows.ToArray();
			foreach (PetWindow petWindow in array)
			{
				try
				{
					petWindow.Close();
					petWindow.Dispose();
				}
				catch
				{
				}
			}
			windows.Clear();
			ExitThread();
		}

		public static bool HourlyDue(DateTime now, ref DateTime lastHour, bool enabled)
		{
			if (!enabled || now.Minute != 0)
			{
				return false;
			}
			DateTime dateTime = now.AddTicks(-(now.Ticks % 36000000000L));
			if (dateTime == lastHour)
			{
				return false;
			}
			lastHour = dateTime;
			return true;
		}

		public static string HourlyText(DateTime now)
		{
			int num = now.Hour % 12;
			if (num == 0)
			{
				num = 12;
			}
			return "지금은 " + ((now.Hour >= 12) ? "오후 " : "오전 ") + num + "시야!";
		}

		public static bool AlarmAtHour(DateTime now, bool enabled, string time)
		{
			TimeSpan result;
			return enabled && TimeSpan.TryParse(time, out result) && result.Ticks == now.Hour * 36000000000L;
		}

		public static string HourlyMessage(DateTime now, bool lunchOn, string lunchTime, string lunchMessage, bool leaveOn, string leaveTime, string leaveMessage)
		{
			string text = HourlyText(now);
			if (AlarmAtHour(now, lunchOn, lunchTime) && !string.IsNullOrEmpty(lunchMessage))
			{
				text = text + "\n" + lunchMessage;
			}
			if (AlarmAtHour(now, leaveOn, leaveTime) && !string.IsNullOrEmpty(leaveMessage))
			{
				text = text + "\n" + leaveMessage;
			}
			return text;
		}

		internal void BeginBubbleHourly(string message)
		{
			hourlyPendingText = message;
			hourlyPendingExpiry = double.PositiveInfinity;
			hourlyActivityVersion = ActivityInput.Start();
			CheerAll(message);
			MaintainBubbleHourly(Now);
		}

		internal void MaintainBubbleHourly(double now)
		{
			if (hourlyPendingText == null)
			{
				return;
			}
			if (!Settings.HourlyOn)
			{
				ClearBubbleHourly();
				return;
			}
			if (now >= hourlyPendingExpiry)
			{
				ClearBubbleHourly();
				return;
			}
			if (double.IsPositiveInfinity(hourlyPendingExpiry))
			{
				DateTime now2 = DateTime.Now;
				DateTime d = now2.AddTicks(-(now2.Ticks % 36000000000L));
				if (hourlyLast != DateTime.MinValue && hourlyLast != d)
				{
					hourlyLast = d;
					BeginBubbleHourly(HourlyMessage(now2, Settings.LunchOn, Settings.LunchTime, Settings.LunchMsg, Settings.LeaveOn, Settings.LeaveTime, Settings.LeaveMsg));
					return;
				}
				if (ActivityInput.Version() != hourlyActivityVersion)
				{
					hourlyPendingExpiry = now + 8.0;
					ActivityInput.Stop();
				}
			}
			foreach (PetWindow window in windows)
			{
				window.Pet.hourlyBubble = hourlyPendingText;
			}
		}

		internal void ClearBubbleHourly()
		{
			ActivityInput.Stop();
			foreach (PetWindow window in windows)
			{
				window.Pet.hourlyBubble = null;
				if (window.Pet.Bubble == hourlyPendingText)
				{
					window.Pet.Bubble = null;
				}
			}
			hourlyPendingText = null;
			hourlyPendingExpiry = 0.0;
		}

		internal bool OptionsHourlyDue(DateTime now)
		{
			if (now.Minute != 0)
			{
				return false;
			}
			DateTime d = now.AddTicks(-(now.Ticks % 36000000000L));
			if (hourlyLast == d)
			{
				return false;
			}
			hourlyLast = d;
			return Settings.HourAllowed(now);
		}

		internal void BeginHourly(string message)
		{
			if (Settings.HourlyWindow)
			{
				ShowHourWindow(message);
			}
			else
			{
				BeginBubbleHourly(message);
			}
		}

		internal void ShowHourWindow(string message)
		{
			ClearBubbleHourly();
			if (hourlyNotice == null || hourlyNotice.IsDisposed)
			{
				hourlyNotice = new HourlyNotice(TrayIconImage);
				hourlyNotice.SetMessage(message);
                hourlyNotice.EnableEdgeGlow(Settings.EdgeGlow, Settings.EdgeGlowTheme, Settings.EdgeGlowWidth, Settings.EdgeGlowStrength);
				hourlyNotice.Show();
			}
			else
			{
				hourlyNotice.SetMessage(message);
                hourlyNotice.EnableEdgeGlow(Settings.EdgeGlow, Settings.EdgeGlowTheme, Settings.EdgeGlowWidth, Settings.EdgeGlowStrength);
				hourlyNotice.Show();
				hourlyNotice.BringToFront();
			}
		}

		internal void MaintainHourly(double now)
		{
			if (!Settings.HourlyWindow)
			{
				if (!Settings.HourAllowed(DateTime.Now))
				{
					ClearBubbleHourly();
				}
				else
				{
					MaintainBubbleHourly(now);
				}
			}
		}

		internal void ClearHourly()
		{
			ClearBubbleHourly();
			if (hourlyNotice != null && !hourlyNotice.IsDisposed)
			{
				hourlyNotice.Close();
			}
			hourlyNotice = null;
		}
	}
	internal static class Native
	{
		public struct POINT
		{
			public int x;

			public int y;

			public POINT(int x, int y)
			{
				this.x = x;
				this.y = y;
			}
		}

		public struct SIZE
		{
			public int cx;

			public int cy;

			public SIZE(int cx, int cy)
			{
				this.cx = cx;
				this.cy = cy;
			}
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct BLENDFUNCTION
		{
			public byte BlendOp;

			public byte BlendFlags;

			public byte SourceConstantAlpha;

			public byte AlphaFormat;
		}

		public struct LASTINPUTINFO
		{
			public uint cbSize;

			public uint dwTime;
		}

		public const int WS_EX_LAYERED = 524288;

		public const int WS_EX_TOOLWINDOW = 128;

		public const int WS_EX_NOACTIVATE = 134217728;

		public const int WS_EX_TOPMOST = 8;

		public const int ULW_ALPHA = 2;

		public const byte AC_SRC_OVER = 0;

		public const byte AC_SRC_ALPHA = 1;

		public const uint SWP_NOSIZE = 1u;

		public const uint SWP_NOMOVE = 2u;

		public const uint SWP_NOZORDER = 4u;

		public const uint SWP_NOACTIVATE = 16u;

		public const uint SWP_NOOWNERZORDER = 512u;

		public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

		public const uint SPI_GETSCREENSAVERRUNNING = 114u;

		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

		[DllImport("user32.dll")]
		public static extern IntPtr GetDC(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

		[DllImport("gdi32.dll")]
		public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

		[DllImport("gdi32.dll")]
		public static extern bool DeleteDC(IntPtr hdc);

		[DllImport("gdi32.dll")]
		public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

		[DllImport("gdi32.dll")]
		public static extern bool DeleteObject(IntPtr hObject);

		[DllImport("gdi32.dll")]
		public static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

		[DllImport("user32.dll")]
		public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

		[DllImport("user32.dll")]
		public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

		[DllImport("user32.dll")]
		public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);

		[DllImport("user32.dll")]
		public static extern bool SetProcessDPIAware();

		[DllImport("user32.dll")]
		public static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern bool DestroyIcon(IntPtr hIcon);

		public static double IdleSeconds()
		{
			try
			{
				LASTINPUTINFO plii = default(LASTINPUTINFO);
				plii.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
				if (!GetLastInputInfo(ref plii))
				{
					return 0.0;
				}
				uint tickCount = (uint)Environment.TickCount;
				return (double)(tickCount - plii.dwTime) / 1000.0;
			}
			catch
			{
				return 0.0;
			}
		}

		public static bool ScreenSaverRunning()
		{
			try
			{
				bool pvParam = false;
				SystemParametersInfo(114u, 0u, ref pvParam, 0u);
				return pvParam;
			}
			catch
			{
				return false;
			}
		}

		public static float SystemDpiScale()
		{
			try
			{
				IntPtr dC = GetDC(IntPtr.Zero);
				int deviceCaps = GetDeviceCaps(dC, 88);
				ReleaseDC(IntPtr.Zero, dC);
				if (deviceCaps <= 0)
				{
					return 1f;
				}
				return (float)deviceCaps / 96f;
			}
			catch
			{
				return 1f;
			}
		}
	}
	internal enum PetState
	{
		Falling,
		Walk,
		Pause,
		Squish,
		Drag,
		Climb,
		Sleep,
		Waking,
		Gather,
		WaitHome,
		Cheer, Resting, IdleCry, TouchCry, Hop, Sliding, Wall, Ceiling, WindowClimb, Eating, Thrown, Dizzy, SecretDance, PilotRise
	}
	internal interface IWorld
	{
		List<Rectangle> Areas
		{
			get;
		}

		Random Rng
		{
			get;
		}

		double Now
		{
			get;
		}
	}
	internal sealed partial class Pet
	{
		internal struct Look
		{
			public Sprite Sprite;

			public bool Left;

			public float Sx;

			public float Sy;

			public float Angle;

			public float Px;

			public float Py;

			public float HopY;

			public string Bubble;

			public int ZPhase;
            public int Secret;
            public float SecretPhase;
            public string AffinityLabel;
            public bool AffinityOnLeft;

			public string Key()
			{
				return string.Format("{0}|{1}|{2:0.00}|{3:0.00}|{4:0}|{5:0}|{6:0}|{7}|{8}", Sprite.GetHashCode(), Left ? 1 : 0, Sx, Sy, Angle, Px, Py, Bubble ?? string.Empty, ZPhase) + "|" + Secret + "|" + (Secret == 0 ? 0 : (int)(SecretPhase * 16)) + "|" + AffinityLabel + "|" + AffinityOnLeft;
			}
		}

		public double X;

		public double Y;

		public int Dir = 1;

		public PetState State;

		public double StateT;

		public double Vy;

		public Rectangle Area;

		public bool Hidden;

		private double animT;

		private double stateDur;

		private double squishDur = 0.4;

		private double squishAmp = 0.22;

		private PetState afterSquish = PetState.Walk;

		private bool flipAfterSquish;

		private double collideCooldown;

		private Rectangle climbTo;

		private int climbDir;

		public double DragPx;

		public double DragPy;

		private double angle;

		private double angVel;

		private double lastMouseX;

		private double mouseVx;

		private double gatherX;

		private double gatherDeadline;

		public string Bubble;

		public double BubbleUntil;

		private double cheerHopUntil;

		private PetState? queuedAfterLand;

		private static Font bubbleFont;

		private static FontFamily zFamily;

		internal string hourlyBubble;

		private bool smoothSeen;

		private bool smoothActive;

		private bool smoothStopping;

		private bool smoothLeft;

		private int smoothClass;

		private int smoothFrame;

		private double smoothAt;

		private float smoothLastHop;

		private float smoothFromHop;

		private Sprite smoothLastSprite;

		private float S
		{
			get
			{
				return Sprites.Scale;
			}
		}

		private double WalkSpeed
		{
			get
			{
				return 34f * S * (Chasing ? 2.3 : 1.0);
			}
		}

		private double HalfW
		{
			get
			{
				return (double)Sprites.Walk1.W * 0.36;
			}
		}

		public bool CanReact
		{
			get
			{
				return State != PetState.Drag;
			}
		}

		private static Font BubbleFont
		{
			get
			{
				if (bubbleFont == null)
				{
					try
					{
						bubbleFont = new Font("Malgun Gothic", 11f * Sprites.Scale, FontStyle.Bold, GraphicsUnit.Pixel);
					}
					catch
					{
						bubbleFont = new Font(FontFamily.GenericSansSerif, 11f * Sprites.Scale, FontStyle.Bold, GraphicsUnit.Pixel);
					}
				}
				return bubbleFont;
			}
		}

		private static FontFamily ZFamily
		{
			get
			{
				if (zFamily == null)
				{
					try
					{
						zFamily = new FontFamily("Segoe UI");
					}
					catch
					{
						zFamily = FontFamily.GenericSansSerif;
					}
				}
				return zFamily;
			}
		}

		public Pet(IWorld w, double x, double y)
		{
			X = x;
			Y = y;
			Dir = ((w.Rng.Next(2) != 0) ? 1 : (-1));
			Area = FindArea(w, x, y);
			SetState(PetState.Falling, w);
			InitializeFeatures(w);
		}

		public static Rectangle FindArea(IWorld w, double x, double y)
		{
			List<Rectangle> areas = w.Areas;
			Rectangle result = areas[0];
			double num = double.MaxValue;
			foreach (Rectangle item in areas)
			{
				double num2 = ((x < (double)item.Left) ? ((double)item.Left - x) : ((!(x >= (double)item.Right)) ? 0.0 : (x - (double)item.Right + 1.0)));
				double num3 = ((y < (double)item.Top) ? ((double)item.Top - y) : ((!(y > (double)item.Bottom)) ? 0.0 : (y - (double)item.Bottom)));
				double num4 = num2 * 1000.0 + num3;
				if (num4 < num)
				{
					num = num4;
					result = item;
				}
			}
			return result;
		}

		private static Rectangle? AreaAtX(IWorld w, double x, double nearY)
		{
			Rectangle? result = null;
			double num = double.MaxValue;
			foreach (Rectangle area in w.Areas)
			{
				if (!(x < (double)area.Left) && !(x >= (double)area.Right))
				{
					double num2 = Math.Abs((double)area.Bottom - nearY);
					if (num2 < num)
					{
						num = num2;
						result = area;
					}
				}
			}
			return result;
		}

		public void SetState(PetState s, IWorld w)
		{
			State = s;
			StateT = 0.0;
			switch (s)
			{
			case PetState.Walk:
				stateDur = 3.0 + w.Rng.NextDouble() * 7.0;
				break;
			case PetState.Pause:
				stateDur = 1.2 + w.Rng.NextDouble() * 3.0;
				break;
			case PetState.Waking:
				stateDur = 1.6;
				break;
			}
		}

		public void StartSquish(IWorld w, double dur, double amp, PetState after, bool flip)
		{
			squishDur = dur;
			squishAmp = amp;
			afterSquish = after;
			flipAfterSquish = flip;
			SetState(PetState.Squish, w);
		}

		public void Poke(IWorld w)
		{
			ClickFeature(w);
		}

		public void BeginDrag(IWorld w, double mx, double my)
		{
			ReleaseFeatures(w);
			DragPx = mx - X;
			DragPy = my - Y;
			if (DragPy > (double)(-4f * S))
			{
				DragPy = -4f * S;
			}
			angle = 0.0;
			angVel = 0.0;
			lastMouseX = mx;
			mouseVx = 0.0;
			queuedAfterLand = null;
			BeginThrowTracking(w.Now, mx, my);
			SetState(PetState.Drag, w);
		}

		public void DragTo(IWorld w, double mx, double my)
		{
			TrackDrag(w.Now, mx, my);
			X = mx - DragPx;
			Y = my - DragPy;
		}

		public void EndDrag(IWorld w)
		{
			if (State != PetState.Drag) return;
			TrackDrag(w.Now, X + DragPx, Y + DragPy);
			ReleaseFeatures(w);
			Area = FindArea(w, X, Y);
			if (X < (double)Area.Left + HalfW)
			{
				X = (double)Area.Left + HalfW;
			}
			if (X > (double)Area.Right - HalfW)
			{
				X = (double)Area.Right - HalfW;
			}
			Vy = 0.0;
			if (TryThrow(w)) return;
            ResetSecretThrows();
			if (Y >= (double)Area.Bottom)
			{
				Y = Area.Bottom;
				StartSquish(w, 0.25, 0.14, PetState.Walk, false);
			}
			else
			{
				SetState(PetState.Falling, w);
			}
		}

		public void FallAsleep(IWorld w)
		{
			if (State == PetState.Walk || State == PetState.Pause || State == PetState.Squish)
			{
				SetState(PetState.Sleep, w);
			}
		}

		public void WakeUp(IWorld w)
		{
			if (State == PetState.Sleep)
			{
				SetState(PetState.Waking, w);
			}
		}

		public void Cheer(IWorld w, string msg, double bubbleSec)
		{
			if (State == PetState.Drag)
			{
				Bubble = msg;
				BubbleUntil = w.Now + bubbleSec;
				return;
			}
			Bubble = msg;
			BubbleUntil = w.Now + bubbleSec;
			cheerHopUntil = w.Now + 3.2;
			if (State == PetState.Falling || State == PetState.Climb || State == PetState.Thrown)
			{
				queuedAfterLand = PetState.Cheer;
			}
			else
			{
				SetState(PetState.Cheer, w);
			}
		}

		public void Gather(IWorld w, double targetX, double deadline)
		{
			if (State != PetState.Drag && State != 0 && State != PetState.Climb && State != PetState.Thrown)
			{
				gatherX = Math.Max((double)Area.Left + HalfW, Math.Min((double)Area.Right - HalfW, targetX));
				gatherDeadline = deadline;
				SetState(PetState.Gather, w);
			}
		}

		public void Update(IWorld w, double dt, List<Pet> all, double mouseX)
		{
			if (AdvanceFeatures(w, dt)) return;
			StateT += dt;
			animT += dt;
			if (collideCooldown > 0.0)
			{
				collideCooldown -= dt;
			}
			if (Bubble != null && w.Now > BubbleUntil)
			{
				Bubble = null;
			}
			switch (State)
			{
			case PetState.Falling:
				UpdateFalling(w, dt);
				break;
			case PetState.Walk:
				UpdateWalk(w, dt, all);
				break;
			case PetState.Pause:
				SnapToFloor(w);
				if (StateT > stateDur)
				{
					if (w.Rng.NextDouble() < 0.35)
					{
						Dir = -Dir;
					}
					SetState(PetState.Walk, w);
				}
				break;
			case PetState.Squish:
				if (StateT >= squishDur)
				{
					if (flipAfterSquish)
					{
						Dir = -Dir;
					}
					SetState(afterSquish, w);
				}
				break;
			case PetState.Drag:
			{
				double num = (mouseX - lastMouseX) / Math.Max(dt, 0.001);
				lastMouseX = mouseX;
				mouseVx = mouseVx * 0.7 + num * 0.3;
				double num2 = Math.Max(-45.0, Math.Min(45.0, (0.0 - mouseVx) * 0.05 / (double)S));
				num2 += Math.Sin(animT * 3.2) * 5.0;
				angVel += (num2 - angle) * 60.0 * dt;
				angVel -= angVel * 7.0 * dt;
				angle += angVel * dt;
				break;
			}
			case PetState.Climb:
				UpdateClimb(w, dt);
				break;
			case PetState.Sleep:
				SnapToFloor(w);
				break;
			case PetState.Waking:
				SnapToFloor(w);
				if (StateT > stateDur)
				{
					SetState(PetState.Walk, w);
				}
				break;
			case PetState.Gather:
				UpdateGather(w, dt);
				break;
			case PetState.WaitHome:
				SnapToFloor(w);
				break;
			case PetState.Cheer:
				SnapToFloor(w);
				if (Bubble == null && w.Now > cheerHopUntil)
				{
					SetState(PetState.Walk, w);
				}
				break;
			}
		}

		private void SnapToFloor(IWorld w)
		{
			if (SurfaceFloor(w)) return;
			if (Y < (double)(Area.Bottom - 1))
			{
				Vy = 0.0;
				SetState(PetState.Falling, w);
			}
			else if (Y > (double)Area.Bottom)
			{
				Y = Area.Bottom;
			}
		}

		private void UpdateFalling(IWorld w, double dt)
		{
			HelicopterDrop(w, dt);
		}

		private void UpdateWalk(IWorld w, double dt, List<Pet> all)
		{
			SnapToFloor(w);
			if (State != PetState.Walk)
			{
				return;
			}
			if (StateT > stateDur)
			{
				SetState(PetState.Pause, w);
				return;
			}
			MoveHoriz(w, dt, WalkSpeed);
		}

		private void MoveHoriz(IWorld w, double dt, double speed)
		{
			speed *= PlayOptions.Current.Speed;
			if (WalkOnWindow(w, dt, speed)) return;
			if (BoundaryFeature(w, X + Dir * speed * dt)) return;
			double num = X + (double)Dir * speed * dt;
			double halfW = HalfW;
			if (!((Dir <= 0) ? (num - halfW < (double)Area.Left) : (num + halfW > (double)Area.Right)))
			{
				X = num;
				return;
			}
			double x = ((Dir <= 0) ? (Area.Left - 2) : (Area.Right + 1));
			Rectangle? rectangle = AreaAtX(w, x, Y);
			if (!rectangle.HasValue || rectangle.Value == Area)
			{
				Dir = -Dir;
				X = ((Dir <= 0) ? ((double)Area.Right - halfW) : ((double)Area.Left + halfW));
				if (State == PetState.Walk && w.Rng.NextDouble() < 0.4)
				{
					SetState(PetState.Pause, w);
				}
				return;
			}
			Rectangle value = rectangle.Value;
			if ((double)value.Bottom < Y - 2.0)
			{
				X = ((Dir <= 0) ? ((double)Area.Left + halfW * 0.2) : ((double)Area.Right - halfW * 0.2));
				climbTo = value;
				climbDir = Dir;
				SetState(PetState.Climb, w);
				return;
			}
			X = num;
			double num2 = ((Dir <= 0) ? Area.Left : Area.Right);
			if ((Dir <= 0) ? (X < num2) : (X >= num2))
			{
				Area = value;
				if (Y < (double)(value.Bottom - 1))
				{
					Vy = 0.0;
					SetState(PetState.Falling, w);
				}
				else
				{
					Y = value.Bottom;
				}
			}
		}

		private void UpdateClimb(IWorld w, double dt)
		{
			Y -= (double)(30f * S) * dt;
			if (Y <= (double)climbTo.Bottom)
			{
				Y = climbTo.Bottom;
				Area = climbTo;
				X = ((climbDir <= 0) ? ((double)climbTo.Right - HalfW * 0.5) : ((double)climbTo.Left + HalfW * 0.5));
				Dir = climbDir;
				if (queuedAfterLand == PetState.Cheer)
				{
					queuedAfterLand = null;
					SetState(PetState.Cheer, w);
				}
				else
				{
					StartSquish(w, 0.25, 0.1, PetState.Walk, false);
				}
			}
		}

		private void UpdateGather(IWorld w, double dt)
		{
			SnapToFloor(w);
			if (State == PetState.Gather)
			{
				double value = gatherX - X;
				if (Math.Abs(value) < 2.0)
				{
					X = gatherX;
					SetState(PetState.WaitHome, w);
					return;
				}
				Dir = Math.Sign(value);
				double num = Math.Max(0.4, gatherDeadline - w.Now - 0.6);
				double num2 = Math.Max(WalkSpeed, Math.Abs(value) / num);
				double num3 = Math.Min(Math.Abs(value), num2 * dt);
				X += (double)Dir * num3;
			}
		}

		public Look GetLook(double now)
		{
			Look look = default(Look);
			look.Sx = 1f;
			look.Sy = 1f;
			look.ZPhase = -1;
			look.Left = Dir < 0;
			look.Sprite = Sprites.WalkingFrame(animT);
			Look look2 = look;
			int num = (int)(animT / 0.26) % 2;
			switch (State)
			{
			case PetState.Walk:
				look2.Sprite = ((num != 0) ? Sprites.WalkingFrame(animT) : Sprites.WalkingFrame(animT));
				look2.HopY = (float)(Math.Abs(Math.Sin(animT * 6.9813170079773181)) * -1.5 * (double)Sprites.Scale);
				break;
			case PetState.Gather:
				look2.Sprite = (((int)(animT / 0.14) % 2 != 0) ? Sprites.WalkingFrame(animT) : Sprites.WalkingFrame(animT));
				break;
			case PetState.Climb:
				look2.Sprite = ((num != 0) ? Sprites.WalkingFrame(animT) : Sprites.WalkingFrame(animT));
				look2.Left = climbDir < 0;
				break;
			case PetState.Falling:
				look2.Sprite = Sprites.WalkingFrame(animT);
				look2.Sy = 1.06f;
				look2.Sx = 0.96f;
				break;
			case PetState.Squish:
			{
				double num4 = Math.Sin(Math.PI * Math.Min(1.0, StateT / squishDur));
				look2.Sy = (float)(1.0 - squishAmp * num4);
				look2.Sx = (float)(1.0 + squishAmp * 0.7 * num4);
				look2.Sprite = Sprites.WalkingFrame(animT);
				break;
			}
			case PetState.Drag:
				look2.Sprite = Sprites.WalkingFrame(animT);
				look2.Angle = (float)angle;
				look2.Px = (float)DragPx;
				look2.Py = (float)DragPy;
				break;
			case PetState.Sleep:
			{
				look2.Sprite = Sprites.Nap;
				double num3 = Math.Sin(animT * 2.2);
				look2.Sy = (float)(1.0 + 0.025 * num3);
				look2.Sx = (float)(1.0 - 0.012 * num3);
				look2.ZPhase = (int)(animT % 2.4 / 2.4 * 24.0);
				break;
			}
			case PetState.Waking:
				look2.Sprite = Sprites.Wake;
				if (StateT < 0.45)
				{
					look2.HopY = (float)((0.0 - Math.Sin(Math.PI * StateT / 0.45)) * 12.0 * (double)S);
				}
				break;
			case PetState.WaitHome:
				look2.Sprite = Sprites.Wake;
				break;
			case PetState.Cheer:
				look2.Sprite = Sprites.Wake;
				if (now < cheerHopUntil)
				{
					double num2 = StateT % 0.42 / 0.42;
					look2.HopY = (float)((0.0 - Math.Sin(Math.PI * num2)) * 11.0 * (double)S);
				}
				break;
			default:
				look2.Sprite = Sprites.Wake;
				break;
			}
			look2.Bubble = Bubble;
			look2.Bubble = PersistentBubble(hourlyBubble, look2.Bubble);
			SmoothPose(ref look2, now);
			FeatureLook(ref look2, now);
            CompanionLook(ref look2, now);
			return look2;
		}

		public static Bitmap Render(Look L, out PointF origin)
		{
			float scale = Sprites.Scale;
			Sprite sprite = L.Sprite;
			Bitmap bitmap = ((!L.Left) ? sprite.Right : sprite.Left);
			float num = sprite.W;
			float num2 = sprite.H;
			Matrix matrix = new Matrix();
			if (L.Angle != 0f)
			{
				matrix.Translate(L.Px, L.Py);
				matrix.Rotate(L.Angle);
				matrix.Translate(0f - L.Px, 0f - L.Py);
			}
			matrix.Scale(L.Sx, L.Sy);
			PointF[] array = new PointF[4]
			{
				new PointF((0f - num) / 2f, 0f - num2),
				new PointF(num / 2f, 0f - num2),
				new PointF(num / 2f, 0f),
				new PointF((0f - num) / 2f, 0f)
			};
			matrix.TransformPoints(array);
			float num3 = float.MaxValue;
			float num4 = float.MaxValue;
			float num5 = float.MinValue;
			float num6 = float.MinValue;
			PointF[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				PointF pointF = array2[i];
				num3 = Math.Min(num3, pointF.X);
				num4 = Math.Min(num4, pointF.Y);
				num5 = Math.Max(num5, pointF.X);
				num6 = Math.Max(num6, pointF.Y);
			}
			float num7 = num4;
			SizeF sizeF = SizeF.Empty;
			RectangleF r = RectangleF.Empty;
			float num8 = 7f * scale;
			if (!string.IsNullOrEmpty(L.Bubble))
			{
				using (Bitmap image = new Bitmap(1, 1))
				{
					using (Graphics graphics = Graphics.FromImage(image))
					{
						graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
						sizeF = graphics.MeasureString(L.Bubble, BubbleFont, (int)(170f * scale), StringFormat.GenericTypographic);
					}
				}
				float num9 = 7f * scale;
				float num10 = (float)Math.Ceiling(sizeF.Width + num9 * 2f + 2f * scale);
				float num11 = (float)Math.Ceiling(sizeF.Height + num9 * 1.4f + 2f * scale);
				float x = (0f - num10) / 2f;
				float y = num7 - 3f * scale - num8 - num11;
				r = new RectangleF(x, y, num10, num11);
				num3 = Math.Min(num3, r.Left);
				num5 = Math.Max(num5, r.Right);
				num4 = Math.Min(num4, r.Top);
			}
			if (L.ZPhase >= 0)
			{
				num4 = Math.Min(num4, num7 - 30f * scale);
				num5 = Math.Max(num5, num / 2f + 22f * scale);
				num3 = Math.Min(num3, (0f - num) / 2f - 22f * scale);
			}
            if (L.Secret != 0)
            { num3 = Math.Min(num3, -num / 2 - 22 * scale); num5 = Math.Max(num5, num / 2 + 28 * scale); if ((L.Secret & ~1) != 0) num4 = Math.Min(num4, -num2 - 24 * scale); }
            RectangleF affinityBadge = AffinityBadgeBounds(L, num, num2, scale);
            if (!affinityBadge.IsEmpty)
            { num3 = Math.Min(num3, affinityBadge.Left); num5 = Math.Max(num5, affinityBadge.Right); num4 = Math.Min(num4, affinityBadge.Top); num6 = Math.Max(num6, affinityBadge.Bottom); }
			int num12 = 3;
			int val = (int)Math.Ceiling(num5 - num3) + num12 * 2;
			int val2 = (int)Math.Ceiling(num6 - num4) + num12 * 2;
			origin = new PointF(0f - num3 + (float)num12, 0f - num4 + (float)num12);
			Bitmap bitmap2 = new Bitmap(Math.Max(1, val), Math.Max(1, val2), PixelFormat.Format32bppArgb);
			using (Graphics graphics2 = Graphics.FromImage(bitmap2))
			{
				graphics2.Clear(Color.Transparent);
				graphics2.SmoothingMode = SmoothingMode.AntiAlias;
				graphics2.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics2.PixelOffsetMode = PixelOffsetMode.HighQuality;
				graphics2.CompositingQuality = CompositingQuality.HighQuality;
				Matrix matrix2 = new Matrix();
				matrix2.Translate(origin.X, origin.Y);
				matrix2.Multiply(matrix);
				graphics2.Transform = matrix2;
				if (L.Sx == 1f && L.Sy == 1f && L.Angle == 0f)
				{
					graphics2.ResetTransform();
					graphics2.InterpolationMode = InterpolationMode.NearestNeighbor;
					graphics2.PixelOffsetMode = PixelOffsetMode.Half;
					int x2 = (int)Math.Round(origin.X - num / 2f);
					int y2 = (int)Math.Round(origin.Y - num2);
					graphics2.DrawImage(bitmap, new Rectangle(x2, y2, bitmap.Width, bitmap.Height), 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel);
					graphics2.InterpolationMode = InterpolationMode.HighQualityBicubic;
					graphics2.PixelOffsetMode = PixelOffsetMode.HighQuality;
				}
				else
				{
					graphics2.DrawImage(bitmap, new RectangleF((0f - num) / 2f, 0f - num2, num, num2));
				}
				graphics2.ResetTransform();
				graphics2.TranslateTransform(origin.X, origin.Y);
                DrawSecretArt(graphics2, L, num, num2, scale);
                DrawAffinityBadge(graphics2, L, affinityBadge, scale);
				if (L.ZPhase >= 0)
				{
					DrawZ(graphics2, L, num, num2 * L.Sy, scale);
				}
				if (!r.IsEmpty)
				{
					float rad = 8f * scale;
					using (GraphicsPath path = RoundRect(r, rad))
					{
						float num13 = 0f;
						PointF[] array3 = new PointF[3]
						{
							new PointF(num13 - 5f * scale, r.Bottom - 1f),
							new PointF(num13 + 1f * scale, r.Bottom + num8),
							new PointF(num13 + 6f * scale, r.Bottom - 1f)
						};
						using (Pen pen = new Pen(Color.FromArgb(255, 30, 30, 30), 2f * scale))
						{
							using (SolidBrush brush = new SolidBrush(Color.White))
							{
								pen.LineJoin = LineJoin.Round;
								graphics2.FillPath(brush, path);
								graphics2.DrawPath(pen, path);
								graphics2.FillPolygon(brush, array3);
								graphics2.DrawLine(pen, array3[0], array3[1]);
								graphics2.DrawLine(pen, array3[1], array3[2]);
								using (Pen pen2 = new Pen(Color.White, 2.2f * scale))
								{
									graphics2.DrawLine(pen2, array3[0].X + 2.2f * scale, r.Bottom - 0.5f * scale, array3[2].X - 2.2f * scale, r.Bottom - 0.5f * scale);
								}
							}
						}
					}
					graphics2.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
					using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(255, 40, 40, 40)))
					{
						RectangleF layoutRectangle = new RectangleF(r.Left + (r.Width - sizeF.Width) / 2f, r.Top + (r.Height - sizeF.Height) / 2f, sizeF.Width + 2f, sizeF.Height + 2f);
						StringFormat stringFormat = new StringFormat(StringFormat.GenericTypographic);
						stringFormat.Alignment = StringAlignment.Center;
						StringFormat format = stringFormat;
						graphics2.DrawString(L.Bubble, BubbleFont, brush2, layoutRectangle, format);
						return bitmap2;
					}
				}
				return bitmap2;
			}
		}

		private static void DrawZ(Graphics g, Look L, float w, float h, float s)
		{
			float num = ((!L.Left) ? 1 : (-1));
			float num2 = num * w * 0.22f;
			float num3 = (0f - h) * 0.82f;
			for (int i = 0; i < 2; i++)
			{
				float num4 = ((float)L.ZPhase / 24f + (float)i * 0.5f) % 1f;
				float num5 = (float)((i != 0) ? 13 : 9) * s * (0.7f + 0.5f * num4);
				float num6 = num2 + num * (4f + num4 * 16f) * s + (float)Math.Sin((double)num4 * 6.28) * 2f * s;
				float num7 = num3 - num4 * 26f * s;
				int num8 = (int)(255f * Math.Min(1f, Math.Min(num4 * 4f, (1f - num4) * 3f)));
				if (num8 <= 0)
				{
					continue;
				}
				string s2 = ((i != 0) ? "Z" : "z");
				using (GraphicsPath graphicsPath = new GraphicsPath())
				{
					graphicsPath.AddString(s2, ZFamily, 1, num5, new PointF(num6 - num5 * 0.3f, num7 - num5), StringFormat.GenericTypographic);
					Pen pen = new Pen(Color.FromArgb(num8, 255, 255, 255), 2.4f * s);
					pen.LineJoin = LineJoin.Round;
					using (Pen pen2 = pen)
					{
						using (SolidBrush brush = new SolidBrush(Color.FromArgb(num8, 70, 100, 170)))
						{
							g.DrawPath(pen2, graphicsPath);
							g.FillPath(brush, graphicsPath);
						}
					}
				}
			}
		}

		private static GraphicsPath RoundRect(RectangleF r, float rad)
		{
			GraphicsPath graphicsPath = new GraphicsPath();
			float num = rad * 2f;
			graphicsPath.AddArc(r.Left, r.Top, num, num, 180f, 90f);
			graphicsPath.AddArc(r.Right - num, r.Top, num, num, 270f, 90f);
			graphicsPath.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
			graphicsPath.AddArc(r.Left, r.Bottom - num, num, num, 90f, 90f);
			graphicsPath.CloseFigure();
			return graphicsPath;
		}

		internal static string PersistentBubble(string hourly, string ordinary)
		{
			if (string.IsNullOrEmpty(hourly))
			{
				return ordinary;
			}
			if (string.IsNullOrEmpty(ordinary) || hourly == ordinary || hourly.IndexOf(ordinary, StringComparison.Ordinal) >= 0)
			{
				return hourly;
			}
			return hourly + "\n" + ordinary;
		}

		internal void SmoothPose(ref Look look, double now)
		{
			Sprite sprite = look.Sprite;
			float hopY = look.HopY;
			int num = ((State == PetState.Walk) ? 1 : ((State == PetState.Pause || State == PetState.WaitHome) ? 2 : 0));
			if (smoothSeen && smoothLeft == look.Left && num != 0 && smoothClass != 0 && num != smoothClass)
			{
				int num2 = Sprites.WalkIndex((num != 2) ? sprite : smoothLastSprite);
				if (num2 >= 0 && Sprites.StopFrames != null)
				{
					smoothActive = true;
					smoothStopping = num == 2;
					smoothFrame = num2;
					smoothAt = now;
					smoothFromHop = smoothLastHop;
				}
			}
			if (num == 0 || (smoothSeen && smoothLeft != look.Left))
			{
				smoothActive = false;
			}
			if (smoothActive)
			{
				double num3 = Math.Max(0.0, Math.Min(1.0, (now - smoothAt) / 0.18));
				double num4 = num3 * num3 * (3.0 - 2.0 * num3);
				int num5 = Math.Max(0, Math.Min(8, (int)Math.Round(num4 * 8.0)));
				int num6 = ((!smoothStopping) ? Sprites.WalkIndex(sprite) : smoothFrame);
				if (num6 < 0)
				{
					num6 = smoothFrame;
				}
				look.Sprite = ((!smoothStopping) ? Sprites.StartFrames : Sprites.StopFrames)[num6][num5];
				look.HopY = (float)((double)smoothFromHop * (1.0 - num4) + (double)hopY * num4);
				if (num3 >= 1.0)
				{
					smoothActive = false;
				}
			}
			smoothSeen = true;
			smoothClass = num;
			smoothLeft = look.Left;
			smoothLastSprite = sprite;
			smoothLastHop = look.HopY;
		}
	}
	internal sealed partial class PetWindow : Form
	{
		public readonly Pet Pet;

		private readonly App app;

		private string lastKey;

		private PointF origin;

		private int curX = int.MinValue;

		private int curY = int.MinValue;

		private bool mouseDown;

		private bool dragging;

		private Point downPos;

		protected override bool ShowWithoutActivation
		{
			get
			{
				return true;
			}
		}

		protected override CreateParams CreateParams
		{
			get
			{
				CreateParams createParams = base.CreateParams;
				createParams.ExStyle |= DesktopSurfaces.IsWindows ? 134742152 : 134217864;
				return createParams;
			}
		}

		public PetWindow(App app, Pet pet)
		{
			this.app = app;
			Pet = pet;
			base.FormBorderStyle = FormBorderStyle.None;
			base.ShowInTaskbar = false;
			base.StartPosition = FormStartPosition.Manual;
			base.TopMost = true;
			Text = "마루는 강쥐";
			base.Size = new Size(1, 1);
			base.Location = new Point(-32000, -32000);
		}

		protected override void OnPaintBackground(PaintEventArgs e)
		{
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			PaintConventional(e);
		}

		public void Present(double now)
		{
			if (!base.IsHandleCreated)
			{
				return;
			}
			Pet.Look look = Pet.GetLook(now);
			string a = look.Key();
			bool flag = a != lastKey;
			Bitmap bitmap = null;
			if (flag)
			{
				bitmap = Pet.Render(look, out origin);
				lastKey = a;
			}
			int num = (int)Math.Round(Pet.X + Pet.FeatureJitterX - (double)origin.X);
			int num2 = (int)Math.Round(Pet.Y + (double)look.HopY - (double)origin.Y);
			if (bitmap != null)
			{
				using (bitmap)
				{
					SetBitmap(bitmap, num, num2);
				}
				curX = num;
				curY = num2;
			}
			else if (num != curX || num2 != curY)
			{
				MovePresented(num, num2);
				curX = num;
				curY = num2;
			}
		}

		public void Invalidate2()
		{
			lastKey = null;
		}

		private void SetBitmap(Bitmap bitmap, int x, int y)
		{
			PresentBitmap(bitmap, x, y);
		}

		public void KeepOnTop()
		{
			if (!DesktopSurfaces.IsWindows) { TopMost = true; return; }
			if (base.IsHandleCreated && base.Visible)
			{
				Native.SetWindowPos(base.Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, 19u);
			}
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			if (e.Button == MouseButtons.Left)
			{
				mouseDown = true;
				Pet.InputHeld = true;
				dragging = false;
				downPos = Cursor.Position;
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			if (!mouseDown)
			{
				return;
			}
			Point position = Cursor.Position;
			if (!dragging)
			{
				int num = position.X - downPos.X;
				int num2 = position.Y - downPos.Y;
				if (num * num + num2 * num2 >= 16)
				{
					dragging = true;
					Pet.BeginDrag(app, downPos.X, downPos.Y);
					app.BringToFront(this);
				}
			}
			if (dragging)
			{
				Pet.DragTo(app, position.X, position.Y);
			}
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			if (e.Button == MouseButtons.Left && mouseDown)
			{
				mouseDown = false;
				Pet.InputHeld = false;
				if (dragging)
				{
					dragging = false;
					Point position = Cursor.Position;
					if (Pet.State == PetState.Drag) Pet.DragTo(app, position.X, position.Y);
					Pet.EndDrag(app);
				}
				else
				{
					Pet.Poke(app);
				}
			}
			else if (e.Button == MouseButtons.Right)
			{
				app.ShowPetMenu(this, Cursor.Position);
			}
		}

		protected override void OnMouseCaptureChanged(EventArgs e)
		{
			base.OnMouseCaptureChanged(e);
			if (!base.Capture) Pet.InputHeld = false;
			if (dragging && !base.Capture)
			{
				dragging = false;
				mouseDown = false;
				Pet.InputHeld = false;
				Pet.EndDrag(app);
			}
		}
	}
	internal static class Program
	{
		[STAThread]
        private static void Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--export-assets") { StartupArt.Export(args[1]); return; }
            if (args.Length >= 2 && args[0] == "--startup-test") { StartupSmoke.Run(args[1]); Environment.Exit(0); return; }
            if (args.Length >= 2 && args[0] == "--companion-test") { CompanionChecks.Run(args[1]); Environment.Exit(0); return; }
            if (args.Length >= 2 && args[0] == "--upgrade-test") { UpgradeChecks.Run(args[1]); Environment.Exit(0); return; }
            if (args.Length >= 2 && args[0] == "--render-test") { RenderTest.Run(args[1]); return; }
            try
            {
                StartupDiagnostics.Mark("실행 시작");
                bool created;
                using (Mutex mutex = new Mutex(true, "WalkingFriend_custom_single_instance", out created))
                {
                    if (!created)
                    {
                        MessageBox.Show("친구가 이미 실행 중이야.\n작업표시줄 친구 아이콘을 우클릭해서 ‘화면 가운데로 다시 부르기’를 눌러줘.", "마루는 강쥐", MessageBoxButtons.OK, MessageBoxIcon.Information); return;
                    }
                    try { Native.SetProcessDPIAware(); } catch { }
                    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                    Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { StartupDiagnostics.Failure(e.Exception); };
                    StartupDiagnostics.Mark("설정 확인"); PlayOptions.Load(); FriendData.Load();
                    StartupDiagnostics.Mark("준비된 그림 불러오기"); Sprites.Load(Native.SystemDpiScale());
                    StartupDiagnostics.Mark("친구를 화면에 표시"); App app = new App();
                    StartupDiagnostics.Mark("정상 실행 — 친구 표시 완료"); Application.Run(app); GC.KeepAlive(mutex);
                }
            }
            catch (Exception error) { StartupDiagnostics.Failure(error); }
        }
    }

	internal sealed class RenderTest : IWorld
	{
		public List<Rectangle> Areas
		{
			get;
			private set;
		}

		public Random Rng
		{
			get;
			private set;
		}

		public double Now
		{
			get;
			private set;
		}

		public static void Run(string outDir)
		{
			Directory.CreateDirectory(outDir);
			Sprites.Load(1.25f);
			RenderTest w = new RenderTest();
			w.Rng = new Random(3);
			w.Areas = new List<Rectangle>
			{
				new Rectangle(0, 0, 400, 300),
				new Rectangle(400, 60, 300, 200)
			};
			Pet pet = new Pet(w, 340.0, 40.0);
			List<Pet> all = new List<Pet>
			{
				pet
			};
			StreamWriter log = new StreamWriter(Path.Combine(outDir, "log.txt"));
			int shot = 0;
			Action<string> action = delegate(string name)
			{
				PointF origin;
				using (Bitmap bitmap = Pet.Render(pet.GetLook(w.Now), out origin))
				{
					bitmap.Save(Path.Combine(outDir, string.Format("{0:00}_{1}.png", shot++, name)), ImageFormat.Png);
				}
			};
			double dt = 0.03;
			Action<double> action2 = delegate(double secs)
			{
				for (double num = 0.0; num < secs; num += dt)
				{
					w.Now += dt;
					pet.Update(w, dt, all, 0.0);
				}
				log.WriteLine("{0:0.00}s state={1} x={2:0.0} y={3:0.0} dir={4} area={5}", w.Now, pet.State, pet.X, pet.Y, pet.Dir, pet.Area);
			};
			action2(0.1);
			action("fall");
			action2(1.5);
			action("land");
			for (int i = 0; i < 40; i++)
			{
				action2(0.5);
				if (i < 3)
				{
					action("walk" + i);
				}
			}
			pet.Poke(w);
			action2(0.2);
			action("squish");
			action2(1.0);
			pet.BeginDrag(w, pet.X, pet.Y - 30.0);
			for (int j = 0; j < 10; j++)
			{
				w.Now += dt;
				pet.DragTo(w, pet.X + pet.DragPx + 15.0, pet.Y + pet.DragPy);
				pet.Update(w, dt, all, pet.X + pet.DragPx);
			}
			action("drag");
			pet.EndDrag(w);
			action2(1.5);
			pet.SetState(PetState.Pause, w);
			pet.FallAsleep(w);
			action2(0.5);
			action("sleep1");
			action2(0.8);
			action("sleep2");
			pet.WakeUp(w);
			action2(0.2);
			action("wake");
			action2(2.0);
			pet.Cheer(w, "점심 먹으러 가자!", 8.0);
			action2(0.2);
			action("cheer");
			action2(9.0);
			pet.Gather(w, 200.0, w.Now + 10.0);
			action2(3.0);
			action("gather");
			action2(8.0);
			pet.Cheer(w, "퇴근이다! 오늘도 수고했어", 8.0);
			action2(0.3);
			action("leave");
			log.Close();
		}
	}
	internal sealed class Settings
	{
		public const string FileName = "걸어다니는친구_설정.ini";

		public bool LunchOn = true;

		public string LunchTime = "12:00";

		public string LunchMsg = "점심 먹으러 가자!";

		public bool LeaveOn = true;

		public string LeaveTime = "18:00";

		public string LeaveMsg = "퇴근이다! 오늘도 수고했어";

		public bool NapOn = true;

		public int NapMinutes = 10;

		internal bool HourlyOn;

		internal bool HourlyWindow;
        internal bool EdgeGlow = true;
        internal int EdgeGlowTheme = 0, EdgeGlowWidth = 1, EdgeGlowStrength = 1;

		internal bool QuietWeekdays;

		internal int QuietStart;

		internal int QuietEnd;

		public static string ExeDir
		{
			get
			{
				return AppDomain.CurrentDomain.BaseDirectory;
			}
		}

		public static string AltDir
		{
			get
			{
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "걸어다니는친구");
			}
		}

		public Settings()
		{
			HourlyOn = true;
			OptionsDefaults();
		}

		public static Settings Load()
		{
			Settings settings = new Settings();
			string path = Path.Combine(ExeDir, "걸어다니는친구_설정.ini");
			if (!File.Exists(path))
			{
				path = Path.Combine(AltDir, "걸어다니는친구_설정.ini");
			}
			if (!File.Exists(path))
			{
				return settings;
			}
			try
			{
				Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				string[] array = File.ReadAllLines(path, Encoding.UTF8);
				foreach (string text in array)
				{
					string text2 = text.Trim();
					if (text2.Length != 0 && text2[0] != ';' && text2[0] != '#' && text2[0] != '[')
					{
						int num = text2.IndexOf('=');
						if (num > 0)
						{
							dictionary[text2.Substring(0, num).Trim()] = text2.Substring(num + 1).Trim();
						}
					}
				}
				settings.HourlyOn = ReadHourlySetting(dictionary);
				settings.ReadOptions(dictionary);
				string value;
				if (dictionary.TryGetValue("LunchOn", out value))
				{
					settings.LunchOn = value != "0";
				}
				if (dictionary.TryGetValue("LunchTime", out value) && ParseTime(value).HasValue)
				{
					settings.LunchTime = value;
				}
				if (dictionary.TryGetValue("LunchMsg", out value))
				{
					settings.LunchMsg = value;
				}
				if (dictionary.TryGetValue("LeaveOn", out value))
				{
					settings.LeaveOn = value != "0";
				}
				if (dictionary.TryGetValue("LeaveTime", out value) && ParseTime(value).HasValue)
				{
					settings.LeaveTime = value;
				}
				if (dictionary.TryGetValue("LeaveMsg", out value))
				{
					settings.LeaveMsg = value;
				}
				if (dictionary.TryGetValue("NapOn", out value))
				{
					settings.NapOn = value != "0";
				}
				if (dictionary.TryGetValue("NapMinutes", out value))
				{
					int result;
					if (int.TryParse(value, out result))
					{
						settings.NapMinutes = Math.Max(1, Math.Min(600, result));
						return settings;
					}
					return settings;
				}
				return settings;
			}
			catch
			{
				return settings;
			}
		}

		public string Save()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("; 마루는 강쥐 설정 파일. 지우면 처음 설정으로 돌아가요.");
			stringBuilder.AppendLine("[알림]");
			stringBuilder.AppendLine("LunchOn=" + ((!LunchOn) ? "0" : "1"));
			stringBuilder.AppendLine("LunchTime=" + LunchTime);
			stringBuilder.AppendLine("LunchMsg=" + OneLine(LunchMsg));
			stringBuilder.AppendLine("LeaveOn=" + ((!LeaveOn) ? "0" : "1"));
			stringBuilder.AppendLine("LeaveTime=" + LeaveTime);
			stringBuilder.AppendLine("LeaveMsg=" + OneLine(LeaveMsg));
			stringBuilder.AppendLine("[낮잠]");
			stringBuilder.AppendLine("NapOn=" + ((!NapOn) ? "0" : "1"));
			stringBuilder.AppendLine("NapMinutes=" + NapMinutes);
			AppendHourlySetting(stringBuilder, HourlyOn);
			WriteOptions(stringBuilder);
			string contents = stringBuilder.ToString();
			UTF8Encoding encoding = new UTF8Encoding(true);
			try
			{
				string text = Path.Combine(ExeDir, "걸어다니는친구_설정.ini");
				File.WriteAllText(text, contents, encoding);
				return text;
			}
			catch
			{
			}
			try
			{
				Directory.CreateDirectory(AltDir);
				string text2 = Path.Combine(AltDir, "걸어다니는친구_설정.ini");
				File.WriteAllText(text2, contents, encoding);
				return text2;
			}
			catch
			{
				return null;
			}
		}

		private static string OneLine(string s)
		{
			return (s ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
		}

		public static TimeSpan? ParseTime(string v)
		{
			if (string.IsNullOrEmpty(v))
			{
				return null;
			}
			string[] array = v.Trim().Split(new char[1]
			{
				':'
			});
			if (array.Length != 2)
			{
				return null;
			}
			int result;
			int result2;
			if (!int.TryParse(array[0], out result) || !int.TryParse(array[1], out result2))
			{
				return null;
			}
			if (result < 0 || result > 23 || result2 < 0 || result2 > 59)
			{
				return null;
			}
			return new TimeSpan(result, result2, 0);
		}

		public static bool ReadHourlySetting(Dictionary<string, string> values)
		{
			string value;
			return !values.TryGetValue("HourlyOn", out value) || value != "0";
		}

		public static void AppendHourlySetting(StringBuilder text, bool enabled)
		{
			text.AppendLine("[정각]");
			text.AppendLine("HourlyOn=" + ((!enabled) ? "0" : "1"));
		}

		internal void OptionsDefaults()
		{
			HourlyWindow = true;
			QuietWeekdays = false;
			QuietStart = 9;
			QuietEnd = 18;
		}

		internal void ReadOptions(Dictionary<string, string> values)
		{
			string value;
			if (values.TryGetValue("HourlyWindow", out value))
			{
				HourlyWindow = value != "0";
			}
            if (values.TryGetValue("EdgeGlow", out value)) EdgeGlow = value != "0";
            int glowOption;
            if (values.TryGetValue("EdgeGlowTheme", out value) && int.TryParse(value, out glowOption)) EdgeGlowTheme = Math.Max(0, Math.Min(3, glowOption));
            if (values.TryGetValue("EdgeGlowWidth", out value) && int.TryParse(value, out glowOption)) EdgeGlowWidth = Math.Max(0, Math.Min(2, glowOption));
            if (values.TryGetValue("EdgeGlowStrength", out value) && int.TryParse(value, out glowOption)) EdgeGlowStrength = Math.Max(0, Math.Min(2, glowOption));
			if (values.TryGetValue("QuietWeekdays", out value))
			{
				QuietWeekdays = value == "1";
			}
			int result;
			if (values.TryGetValue("QuietStart", out value) && int.TryParse(value, out result))
			{
				QuietStart = Math.Max(0, Math.Min(23, result));
			}
			if (values.TryGetValue("QuietEnd", out value) && int.TryParse(value, out result))
			{
				QuietEnd = Math.Max(0, Math.Min(23, result));
			}
		}

		internal void WriteOptions(StringBuilder text)
		{
			text.AppendLine("HourlyWindow=" + ((!HourlyWindow) ? "0" : "1"));
            text.AppendLine("EdgeGlow=" + (EdgeGlow ? "1" : "0"));
            text.AppendLine("EdgeGlowTheme=" + EdgeGlowTheme);
            text.AppendLine("EdgeGlowWidth=" + EdgeGlowWidth);
            text.AppendLine("EdgeGlowStrength=" + EdgeGlowStrength);
			text.AppendLine("QuietWeekdays=" + ((!QuietWeekdays) ? "0" : "1"));
			text.AppendLine("QuietStart=" + QuietStart);
			text.AppendLine("QuietEnd=" + QuietEnd);
		}

		internal bool HourAllowed(DateTime now)
		{
			if (!HourlyOn)
			{
				return false;
			}
			if (!QuietWeekdays || now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
			{
				return true;
			}
			bool flag = ((QuietStart <= QuietEnd) ? (now.Hour >= QuietStart && now.Hour <= QuietEnd) : (now.Hour >= QuietStart || now.Hour <= QuietEnd));
			return !flag;
		}
	}
	internal sealed class SettingsForm : Form
	{
		private readonly App app;

		private CheckBox cLunch;

		private CheckBox cLeave;

		private CheckBox cNap;

		private TextBox tLunch;

		private TextBox tLunchMsg;

		private TextBox tLeave;

		private TextBox tLeaveMsg;

		private NumericUpDown nNap;

		private CheckBox cHourly;

		private AdvancedControls options;

		public SettingsForm(App app)
		{
			SettingsForm settingsForm = this;
			this.app = app;
			Text = "마루는 강쥐 - 알림 설정";
			base.FormBorderStyle = FormBorderStyle.FixedDialog;
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.StartPosition = FormStartPosition.CenterScreen;
			base.AutoScaleMode = AutoScaleMode.Dpi;
			AutoSize = true;
			base.AutoSizeMode = AutoSizeMode.GrowAndShrink;
			base.TopMost = true;
			base.ShowInTaskbar = true;
			base.Padding = new Padding(12);
			try
			{
				Font = new Font("Malgun Gothic", 9f);
			}
			catch
			{
			}
			try
			{
				base.Icon = app.TrayIconImage;
			}
			catch
			{
			}
			Settings settings = app.Settings;
			TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
			{
				ColumnCount = 3,
				AutoSize = true,
				Dock = DockStyle.Fill,
				Padding = new Padding(0)
			};
			tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			cLunch = new CheckBox
			{
				Text = "점심 알림",
				Checked = settings.LunchOn,
				AutoSize = true,
				Anchor = AnchorStyles.Left
			};
			tLunch = new TextBox
			{
				Text = settings.LunchTime,
				Width = 60
			};
			tableLayoutPanel.Controls.Add(cLunch, 0, 0);
			tableLayoutPanel.Controls.Add(Lbl("시간"), 1, 0);
			tableLayoutPanel.Controls.Add(tLunch, 2, 0);
			tLunchMsg = new TextBox
			{
				Text = settings.LunchMsg,
				Width = 230
			};
			tableLayoutPanel.Controls.Add(Lbl(string.Empty), 0, 1);
			tableLayoutPanel.Controls.Add(Lbl("문구"), 1, 1);
			tableLayoutPanel.Controls.Add(tLunchMsg, 2, 1);
			cLeave = new CheckBox
			{
				Text = "퇴근 알림",
				Checked = settings.LeaveOn,
				AutoSize = true,
				Anchor = AnchorStyles.Left,
				Margin = new Padding(3, 12, 3, 3)
			};
			tLeave = new TextBox
			{
				Text = settings.LeaveTime,
				Width = 60,
				Margin = new Padding(3, 12, 3, 3)
			};
			Label label = Lbl("시간");
			label.Margin = new Padding(3, 15, 3, 3);
			tableLayoutPanel.Controls.Add(cLeave, 0, 2);
			tableLayoutPanel.Controls.Add(label, 1, 2);
			tableLayoutPanel.Controls.Add(tLeave, 2, 2);
			tLeaveMsg = new TextBox
			{
				Text = settings.LeaveMsg,
				Width = 230
			};
			tableLayoutPanel.Controls.Add(Lbl(string.Empty), 0, 3);
			tableLayoutPanel.Controls.Add(Lbl("문구"), 1, 3);
			tableLayoutPanel.Controls.Add(tLeaveMsg, 2, 3);
			cNap = new CheckBox
			{
				Text = "낮잠",
				Checked = settings.NapOn,
				AutoSize = true,
				Anchor = AnchorStyles.Left,
				Margin = new Padding(3, 12, 3, 3)
			};
			FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
			{
				AutoSize = true,
				Margin = new Padding(0, 9, 0, 0),
				WrapContents = false
			};
			nNap = new NumericUpDown
			{
				Minimum = 1m,
				Maximum = 600m,
				Value = Math.Max(1, Math.Min(600, settings.NapMinutes)),
				Width = 60
			};
			flowLayoutPanel.Controls.Add(nNap);
			flowLayoutPanel.Controls.Add(new Label
			{
				Text = "분 동안 입력이 없으면 잠들어요",
				AutoSize = true,
				Margin = new Padding(3, 6, 3, 3)
			});
			Label label2 = Lbl(string.Empty);
			label2.Margin = new Padding(3, 15, 3, 3);
			tableLayoutPanel.Controls.Add(cNap, 0, 4);
			tableLayoutPanel.Controls.Add(label2, 1, 4);
			tableLayoutPanel.Controls.Add(flowLayoutPanel, 2, 4);
			Label control = new Label
			{
				Text = "시간은 12:00처럼 시:분으로 적어 주세요.",
				AutoSize = true,
				ForeColor = Color.DimGray,
				Margin = new Padding(3, 10, 3, 3)
			};
			tableLayoutPanel.Controls.Add(control, 0, 5);
			tableLayoutPanel.SetColumnSpan(control, 3);
			FlowLayoutPanel flowLayoutPanel2 = new FlowLayoutPanel
			{
				AutoSize = true,
				FlowDirection = FlowDirection.LeftToRight,
				Margin = new Padding(0, 12, 0, 0),
				WrapContents = false
			};
			Button button = new Button
			{
				Text = "점심 미리보기",
				AutoSize = true
			};
			Button button2 = new Button
			{
				Text = "퇴근 미리보기",
				AutoSize = true
			};
			Button button3 = new Button
			{
				Text = "저장",
				AutoSize = true,
				Margin = new Padding(18, 3, 3, 3)
			};
			Button button4 = new Button
			{
				Text = "취소",
				AutoSize = true
			};
			button.Click += delegate
			{
				app.PreviewLunch(settingsForm.tLunchMsg.Text);
			};
			button2.Click += delegate
			{
				app.PreviewLeave(settingsForm.tLeaveMsg.Text);
			};
			button3.Click += delegate
			{
				settingsForm.DoSave();
			};
			button4.Click += delegate
			{
				settingsForm.Close();
			};
			flowLayoutPanel2.Controls.AddRange(new Control[4]
			{
				button,
				button2,
				button3,
				button4
			});
			tableLayoutPanel.Controls.Add(flowLayoutPanel2, 0, 6);
			tableLayoutPanel.SetColumnSpan(flowLayoutPanel2, 3);
			base.Controls.Add(tableLayoutPanel);
			base.AcceptButton = button3;
			base.CancelButton = button4;
			cHourly = AddHourlyControl(tableLayoutPanel, this.app.Settings.HourlyOn);
			AttachOptions();
		}

		private static Label Lbl(string s)
		{
			Label label = new Label();
			label.Text = s;
			label.AutoSize = true;
			label.Anchor = AnchorStyles.Left;
			label.Margin = new Padding(3, 6, 3, 3);
			return label;
		}

		private void DoSave()
		{
			if (!Settings.ParseTime(tLunch.Text).HasValue)
			{
				Warn("점심 시간을 12:00처럼 적어 주세요.");
				tLunch.Focus();
				return;
			}
			if (!Settings.ParseTime(tLeave.Text).HasValue)
			{
				Warn("퇴근 시간을 18:00처럼 적어 주세요.");
				tLeave.Focus();
				return;
			}
			Settings settings = app.Settings;
			if (SaveOptions())
			{
				settings.HourlyOn = cHourly.Checked;
				settings.LunchOn = cLunch.Checked;
				settings.LunchTime = Norm(tLunch.Text);
				settings.LunchMsg = tLunchMsg.Text.Trim();
				settings.LeaveOn = cLeave.Checked;
				settings.LeaveTime = Norm(tLeave.Text);
				settings.LeaveMsg = tLeaveMsg.Text.Trim();
				settings.NapOn = cNap.Checked;
				settings.NapMinutes = (int)nNap.Value;
				if (settings.LunchMsg.Length == 0)
				{
					settings.LunchMsg = "점심 먹으러 가자!";
				}
				if (settings.LeaveMsg.Length == 0)
				{
					settings.LeaveMsg = "퇴근이다! 오늘도 수고했어";
				}
				app.OnSettingsChanged();
				if (settings.Save() == null)
				{
					Warn("설정 파일을 저장하지 못했어요. 이번에 켜 있는 동안만 적용돼요.");
				}
				Close();
			}
		}

		private static string Norm(string v)
		{
			TimeSpan value = Settings.ParseTime(v).Value;
			return string.Format("{0:00}:{1:00}", value.Hours, value.Minutes);
		}

		private void Warn(string m)
		{
			MessageBox.Show(this, m, "마루는 강쥐", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}

		public static CheckBox AddHourlyControl(TableLayoutPanel panel, bool enabled)
		{
			panel.SuspendLayout();
			try
			{
				panel.RowCount = Math.Max(panel.RowCount, 7) + 2;
				IEnumerator enumerator = panel.Controls.GetEnumerator();
				try
				{
					while (enumerator.MoveNext())
					{
						Control control = (Control)enumerator.Current;
						panel.SetRow(control, panel.GetRow(control) + 2);
					}
				}
				finally
				{
					IDisposable disposable;
					if ((disposable = enumerator as IDisposable) != null)
					{
						disposable.Dispose();
					}
				}
				CheckBox checkBox = new CheckBox();
				checkBox.Text = "정각 알림 (매시간 현재 시각 알려주기)";
				checkBox.Checked = enabled;
				checkBox.AutoSize = true;
				checkBox.Anchor = AnchorStyles.Left;
				checkBox.Margin = new Padding(3, 3, 3, 3);
				panel.Controls.Add(checkBox, 0, 0);
				panel.SetColumnSpan(checkBox, 3);
				Label label = new Label();
				label.Text = "표시 방식과 평일 제외 시간은 아래에서 선택하세요.";
				label.AutoSize = true;
				label.ForeColor = Color.DimGray;
				label.Margin = new Padding(3, 0, 3, 12);
				panel.Controls.Add(label, 0, 1);
				panel.SetColumnSpan(label, 3);
				return checkBox;
			}
			finally
			{
				panel.ResumeLayout(true);
			}
		}

		internal void AttachOptions()
		{
			Text = "마루는 강쥐 · 자동 시작 / 알림 설정";
			MinimumSize = new Size(600, 0);
			TableLayoutPanel tableLayoutPanel = (TableLayoutPanel)cHourly.Parent;
			tableLayoutPanel.SuspendLayout();
			try
			{
				tableLayoutPanel.RowCount++;
				IEnumerator enumerator = tableLayoutPanel.Controls.GetEnumerator();
				try
				{
					while (enumerator.MoveNext())
					{
						Control control = (Control)enumerator.Current;
						if (tableLayoutPanel.GetRow(control) >= 2)
						{
							tableLayoutPanel.SetRow(control, tableLayoutPanel.GetRow(control) + 1);
						}
					}
				}
				finally
				{
					IDisposable disposable;
					if ((disposable = enumerator as IDisposable) != null)
					{
						disposable.Dispose();
					}
				}
				options = new AdvancedControls(app);
				tableLayoutPanel.Controls.Add(options, 0, 2);
				tableLayoutPanel.SetColumnSpan(options, 3);
				tableLayoutPanel.RowStyles.Clear();
				for (int i = 0; i < tableLayoutPanel.RowCount; i++)
				{
					tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
				}
			}
			finally
			{
				tableLayoutPanel.ResumeLayout(true);
			}
			AutoSize = false;
			int num = Math.Max(620, tableLayoutPanel.GetPreferredSize(Size.Empty).Width + base.Padding.Horizontal + 24);
			base.ClientSize = new Size(num, Math.Max(540, tableLayoutPanel.GetPreferredSize(new Size(num - base.Padding.Horizontal, 0)).Height + base.Padding.Vertical + 16));
			PerformLayout();
		}

		internal bool SaveOptions()
		{
			try
			{
				options.SaveTo(app.Settings);
				return true;
			}
			catch (Exception ex)
			{
				Warn("자동 시작 설정을 저장하지 못했어요.\n" + ex.Message);
				return false;
			}
		}
	}
	internal sealed class Sprite
	{
		public Bitmap Right;

		public Bitmap Left;

		public int W
		{
			get
			{
				return Right.Width;
			}
		}

		public int H
		{
			get
			{
				return Right.Height;
			}
		}
	}
	internal static class Sprites
	{
		public static Sprite Walk1;

		public static Sprite Walk2;

		public static Sprite Nap;

		public static Sprite Wake;

		public static Sprite Dance;

		public static float Scale = 1f;

		public const float StoredToMini = 0.333333343f;

		// The new character has a smaller body inside the frame than the round original, so every picture is drawn larger.
		public const float ArtScale = 1.4f;

		internal static Sprite[] WalkFrames;

		internal static Sprite[][] StopFrames;

		internal static Sprite[][] StartFrames;

		public static void Load(float dpiScale)
		{
			if (StartupArt.TryLoad(dpiScale)) return;
			Scale = dpiScale;
			float f = 0.333333343f * dpiScale * ArtScale;
			Walk1 = Make("walk1.png", f, true);
			Walk2 = Make("walk1.png", f, true);
			Nap = Make("wake.png", f);
			Wake = Make("wake.png", f);
			Dance = Make("dance.png", f);
			WalkFrames = new Sprite[12];
			WalkFrames[0] = MakeAnimated("walk1.png", f, true, 0);
			WalkFrames[1] = MakeAnimated("walk1.png", f, true, 1);
			WalkFrames[2] = MakeAnimated("walk1.png", f, true, 2);
			WalkFrames[3] = MakeAnimated("walk1.png", f, true, 3);
			WalkFrames[4] = MakeAnimated("walk1.png", f, true, 4);
			WalkFrames[5] = MakeAnimated("walk1.png", f, true, 5);
			WalkFrames[6] = MakeAnimated("walk1.png", f, true, 6);
			WalkFrames[7] = MakeAnimated("walk1.png", f, true, 7);
			WalkFrames[8] = MakeAnimated("walk1.png", f, true, 8);
			WalkFrames[9] = MakeAnimated("walk1.png", f, true, 9);
			WalkFrames[10] = MakeAnimated("walk1.png", f, true, 10);
			WalkFrames[11] = MakeAnimated("walk1.png", f, true, 11);
			PrepareTransitions();
			FeatureArt.Load();
		}

		private static Bitmap LoadRaw(string name)
		{
			Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
			if (manifestResourceStream == null)
			{
				string filename = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sprites"), name);
				return new Bitmap(filename);
			}
			using (manifestResourceStream)
			{
				using (Bitmap original = new Bitmap(manifestResourceStream))
				{
					return new Bitmap(original);
				}
			}
		}

		private static Sprite Make(string name, float f, bool facesLeft = false)
		{
			using (Bitmap bitmap = Fit(LoadRaw(name), name))
			{
				int width = Math.Max(1, (int)Math.Round((float)bitmap.Width * f));
				int height = Math.Max(1, (int)Math.Round((float)bitmap.Height * f));
				Bitmap bitmap2 = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
				using (Graphics graphics = Graphics.FromImage(bitmap2))
				{
					graphics.Clear(Color.Transparent);
					graphics.CompositingMode = CompositingMode.SourceOver;
					graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
					graphics.SmoothingMode = SmoothingMode.HighQuality;
					using (ImageAttributes imageAttributes = new ImageAttributes())
					{
						imageAttributes.SetWrapMode(WrapMode.TileFlipXY);
						graphics.DrawImage(bitmap, new Rectangle(0, 0, width, height), 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, imageAttributes);
					}
				}
				bitmap2 = FeatureArt.OuterOutline(bitmap2);
				Bitmap bitmap3 = (Bitmap)bitmap2.Clone();
				bitmap3.RotateFlip(RotateFlipType.RotateNoneFlipX);
				Sprite result;
				if (facesLeft)
				{
					Sprite sprite = new Sprite();
					sprite.Right = bitmap3;
					sprite.Left = bitmap2;
					result = sprite;
				}
				else
				{
					Sprite sprite = new Sprite();
					sprite.Right = bitmap2;
					sprite.Left = bitmap3;
					result = sprite;
				}
				return result;
			}
		}

		public static Icon MakeTrayIcon()
		{
			try
			{
				int num = Math.Max(16, (int)Math.Round(16f * Scale));
				using (Bitmap bitmap2 = LoadRaw("walk1.png"))
				{
					using (Bitmap bitmap = new Bitmap(num, num, PixelFormat.Format32bppArgb))
					{
						using (Graphics graphics = Graphics.FromImage(bitmap))
						{
							graphics.Clear(Color.Transparent);
							graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
							graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
							float num2 = Math.Min((float)num / (float)bitmap2.Width, (float)num / (float)bitmap2.Height);
							float num3 = (float)bitmap2.Width * num2;
							float num4 = (float)bitmap2.Height * num2;
							graphics.DrawImage(bitmap2, ((float)num - num3) / 2f, ((float)num - num4) / 2f, num3, num4);
						}
						IntPtr hicon = bitmap.GetHicon();
						Icon result = (Icon)Icon.FromHandle(hicon).Clone();
						Native.DestroyIcon(hicon);
						return result;
					}
				}
			}
			catch
			{
				return SystemIcons.Application;
			}
		}

		public static Bitmap Animate(Bitmap original, int frame)
		{
			// A gentle waddle: the body rocks around the feet and bobs a little, so any artwork can walk.
			try
			{
				int width = original.Width;
				int height = original.Height;
				double phase = (double)frame * Math.PI * 2.0 / 12.0;
				float angle = (float)(Math.Sin(phase) * 5.0);
				float lift = (float)(Math.Abs(Math.Cos(phase)) * 3.0);
				Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
				using (Graphics graphics = Graphics.FromImage(bitmap))
				{
					FeatureArt.Quality(graphics);
					graphics.Clear(Color.Transparent);
					graphics.TranslateTransform(width / 2f, height - 1);
					graphics.RotateTransform(angle);
					graphics.TranslateTransform(-width / 2f, -(height - 1) - lift);
					graphics.DrawImage(original, 0, 0, width, height);
				}
				return bitmap;
			}
			finally
			{
				original.Dispose();
			}
		}

		public static Bitmap Fit(Bitmap original, string resourceName)
		{
			// Places the visible part of any picture at the bottom centre of the 167x168 working canvas.
			Rectangle rectangle = FeatureArt.Bounds(original);
			double num = Math.Min(155.0 / (double)rectangle.Width, 160.0 / (double)rectangle.Height);
			int num2 = Math.Max(1, (int)Math.Round((double)rectangle.Width * num));
			int num3 = Math.Max(1, (int)Math.Round((double)rectangle.Height * num));
			Bitmap bitmap = new Bitmap(167, 168, PixelFormat.Format32bppArgb);
			try
			{
				using (Graphics graphics = Graphics.FromImage(bitmap))
				{
					using (ImageAttributes imageAttributes = new ImageAttributes())
					{
						graphics.Clear(Color.Transparent);
						graphics.CompositingMode = CompositingMode.SourceCopy;
						graphics.CompositingQuality = CompositingQuality.HighQuality;
						graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
						graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
						imageAttributes.SetWrapMode(WrapMode.TileFlipXY);
						graphics.DrawImage(original, new Rectangle((167 - num2) / 2, 166 - num3, num2, num3), rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, GraphicsUnit.Pixel, imageAttributes);
					}
				}
				return bitmap;
			}
			catch
			{
				bitmap.Dispose();
				throw;
			}
			finally
			{
				original.Dispose();
			}
		}

		private static Sprite MakeAnimated(string name, float f, bool facesLeft, int frame)
		{
			using (Bitmap bitmap = Animate(Fit(LoadRaw(name), name), frame))
			{
				int width = Math.Max(1, (int)Math.Round((float)bitmap.Width * f));
				int height = Math.Max(1, (int)Math.Round((float)bitmap.Height * f));
				Bitmap bitmap2 = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
				using (Graphics graphics = Graphics.FromImage(bitmap2))
				{
					graphics.Clear(Color.Transparent);
					graphics.CompositingMode = CompositingMode.SourceOver;
					graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
					graphics.SmoothingMode = SmoothingMode.HighQuality;
					using (ImageAttributes imageAttributes = new ImageAttributes())
					{
						imageAttributes.SetWrapMode(WrapMode.TileFlipXY);
						graphics.DrawImage(bitmap, new Rectangle(0, 0, width, height), 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, imageAttributes);
					}
				}
				bitmap2 = FeatureArt.OuterOutline(bitmap2);
				Bitmap bitmap3 = (Bitmap)bitmap2.Clone();
				bitmap3.RotateFlip(RotateFlipType.RotateNoneFlipX);
				Sprite result;
				if (facesLeft)
				{
					Sprite sprite = new Sprite();
					sprite.Right = bitmap3;
					sprite.Left = bitmap2;
					result = sprite;
				}
				else
				{
					Sprite sprite = new Sprite();
					sprite.Right = bitmap2;
					sprite.Left = bitmap3;
					result = sprite;
				}
				return result;
			}
		}

		internal static Sprite WalkingFrame(double time)
		{
			return WalkFrames[(int)(time / 0.075) % 12];
		}

		internal static void SlimContour(Bitmap canvas)
		{
			int width = canvas.Width;
			int height = canvas.Height;
			BitmapData bitmapData = canvas.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
			try
			{
				int stride = bitmapData.Stride;
				byte[] array = new byte[stride * height];
				Marshal.Copy(bitmapData.Scan0, array, 0, array.Length);
				byte[] array2 = (byte[])array.Clone();
				for (int i = 1; i < height - 1; i++)
				{
					for (int j = 1; j < width - 1; j++)
					{
						int num = i * stride + j * 4;
						int num2 = array[num + 3];
						if (num2 == 0)
						{
							continue;
						}
						int num3 = Math.Max(array[num], Math.Max(array[num + 1], array[num + 2]));
						int num4 = Math.Min(array[num], Math.Min(array[num + 1], array[num + 2]));
						if (num3 - num4 > 24 || num3 > 225)
						{
							continue;
						}
						bool flag = false;
						for (int k = -6; k <= 6; k++)
						{
							if (flag)
							{
								break;
							}
							for (int l = -6; l <= 6; l++)
							{
								if (l * l + k * k <= 36)
								{
									int num5 = j + l;
									int num6 = i + k;
									if (num5 < 0 || num6 < 0 || num5 >= width || num6 >= height || array[num6 * stride + num5 * 4 + 3] < 24)
									{
										flag = true;
										break;
									}
								}
							}
						}
						if (!flag)
						{
							continue;
						}
						int num7 = num2;
						double num8 = (double)(array[num] + array[num + 1] + array[num + 2]) / 3.0;
						double num9 = num8;
						int[] array3 = new int[4]
						{
							num - 4,
							num + 4,
							num - stride,
							num + stride
						};
						foreach (int num10 in array3)
						{
							num7 = Math.Min(num7, array[num10 + 3]);
							if (array[num10 + 3] >= 200)
							{
								int num11 = Math.Max(array[num10], Math.Max(array[num10 + 1], array[num10 + 2]));
								int num12 = Math.Min(array[num10], Math.Min(array[num10 + 1], array[num10 + 2]));
								if (num11 - num12 <= 24)
								{
									num9 = Math.Max(num9, (double)(array[num10] + array[num10 + 1] + array[num10 + 2]) / 3.0);
								}
							}
						}
						if (num7 < num2)
						{
							array2[num + 3] = (byte)Math.Round((double)num2 + 0.95 * (double)(num7 - num2));
						}
						else if (num9 > num8)
						{
							byte b = (byte)Math.Round(num8 + 0.95 * (num9 - num8));
							int num13 = num + 1;
							byte b2;
							array2[num + 2] = (b2 = b);
							array2[num13] = b2;
							array2[num] = b2;
						}
					}
				}
				Marshal.Copy(array2, 0, bitmapData.Scan0, array2.Length);
			}
			finally
			{
				canvas.UnlockBits(bitmapData);
			}
		}

		internal static void PrepareTransitions()
		{
			ReleaseTransitions(StopFrames);
			ReleaseTransitions(StartFrames);
			StopFrames = new Sprite[WalkFrames.Length][];
			StartFrames = new Sprite[WalkFrames.Length][];
			for (int i = 0; i < WalkFrames.Length; i++)
			{
				StopFrames[i] = new Sprite[9];
				StartFrames[i] = new Sprite[9];
				StopFrames[i][0] = WalkFrames[i];
				StopFrames[i][8] = Wake;
				StartFrames[i][0] = Wake;
				StartFrames[i][8] = WalkFrames[i];
				for (int j = 1; j < 8; j++)
				{
					StopFrames[i][j] = MixSprite(WalkFrames[i], Wake, (double)j / 8.0);
					StartFrames[i][j] = MixSprite(Wake, WalkFrames[i], (double)j / 8.0);
				}
			}
		}

		internal static void ReleaseTransitions(Sprite[][] frames)
		{
			if (frames == null)
			{
				return;
			}
			foreach (Sprite[] array in frames)
			{
				if (array == null)
				{
					continue;
				}
				for (int j = 1; j < array.Length - 1; j++)
				{
					if (array[j] != null)
					{
						array[j].Right.Dispose();
						array[j].Left.Dispose();
					}
				}
			}
		}

		internal static int WalkIndex(Sprite sprite)
		{
			if (WalkFrames == null)
			{
				return -1;
			}
			for (int i = 0; i < WalkFrames.Length; i++)
			{
				if (object.ReferenceEquals(sprite, WalkFrames[i]))
				{
					return i;
				}
			}
			return -1;
		}

		internal static Sprite MixSprite(Sprite from, Sprite to, double amount)
		{
			Bitmap right = from.Right;
			Bitmap right2 = to.Right;
			if (right.Size != right2.Size)
			{
				throw new InvalidOperationException("Transition canvas sizes differ");
			}
			int width = right.Width;
			int height = right.Height;
			Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
			BitmapData bitmapData = null;
			BitmapData bitmapData2 = null;
			BitmapData bitmapData3 = null;
			try
			{
				bitmapData = right.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
				bitmapData2 = right2.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
				bitmapData3 = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
				byte[] array = new byte[bitmapData.Stride * height];
				byte[] array2 = new byte[bitmapData2.Stride * height];
				byte[] array3 = new byte[bitmapData3.Stride * height];
				Marshal.Copy(bitmapData.Scan0, array, 0, array.Length);
				Marshal.Copy(bitmapData2.Scan0, array2, 0, array2.Length);
				for (int i = 0; i < height; i++)
				{
					for (int j = 0; j < width; j++)
					{
						int num = i * bitmapData.Stride + j * 4;
						int num2 = i * bitmapData2.Stride + j * 4;
						int num3 = i * bitmapData3.Stride + j * 4;
						double num4 = (double)(int)array[num + 3] * (1.0 - amount);
						double num5 = (double)(int)array2[num2 + 3] * amount;
						double num6 = num4 + num5;
						array3[num3 + 3] = (byte)Math.Round(num6);
						if (num6 > 0.0)
						{
							for (int k = 0; k < 3; k++)
							{
								array3[num3 + k] = (byte)Math.Round(((double)(int)array[num + k] * num4 + (double)(int)array2[num2 + k] * num5) / num6);
							}
						}
					}
				}
				Marshal.Copy(array3, 0, bitmapData3.Scan0, array3.Length);
			}
			catch
			{
				if (bitmapData3 != null)
				{
					bitmap.UnlockBits(bitmapData3);
					bitmapData3 = null;
				}
				bitmap.Dispose();
				throw;
			}
			finally
			{
				if (bitmapData != null)
				{
					right.UnlockBits(bitmapData);
				}
				if (bitmapData2 != null)
				{
					right2.UnlockBits(bitmapData2);
				}
				if (bitmapData3 != null)
				{
					bitmap.UnlockBits(bitmapData3);
				}
			}
			Bitmap bitmap2 = (Bitmap)bitmap.Clone();
			bitmap2.RotateFlip(RotateFlipType.RotateNoneFlipX);
			Sprite sprite = new Sprite();
			sprite.Right = bitmap;
			sprite.Left = bitmap2;
			return sprite;
		}

		internal static void ThickenContour(Bitmap canvas)
		{
			int width = canvas.Width;
			int height = canvas.Height;
			BitmapData bitmapData = canvas.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
			try
			{
				int stride = bitmapData.Stride;
				byte[] array = new byte[stride * height];
				Marshal.Copy(bitmapData.Scan0, array, 0, array.Length);
				byte[] array2 = (byte[])array.Clone();
				double[] array3 = new double[width * height];
				for (int i = 0; i < height; i++)
				{
					for (int j = 0; j < width; j++)
					{
						int num = i * stride + j * 4;
						int num2 = array[num + 3];
						if (num2 == 0)
						{
							continue;
						}
						int num3 = Math.Max(array[num], Math.Max(array[num + 1], array[num + 2]));
						int num4 = Math.Min(array[num], Math.Min(array[num + 1], array[num + 2]));
						if (num3 - num4 > 24 || num3 > 225)
						{
							continue;
						}
						bool flag = false;
						for (int k = -6; k <= 6; k++)
						{
							if (flag)
							{
								break;
							}
							for (int l = -6; l <= 6; l++)
							{
								if (l * l + k * k <= 36)
								{
									int num5 = j + l;
									int num6 = i + k;
									if (num5 < 0 || num6 < 0 || num5 >= width || num6 >= height || array[num6 * stride + num5 * 4 + 3] < 24)
									{
										flag = true;
										break;
									}
								}
							}
						}
						if (flag)
						{
							double num7 = (double)(array[num] + array[num + 1] + array[num + 2]) / 3.0;
							array3[i * width + j] = (double)num2 * Math.Max(0.0, Math.Min(1.0, (255.0 - num7) / 211.0));
						}
					}
				}
				for (int m = 0; m < height; m++)
				{
					for (int n = 0; n < width; n++)
					{
						int num8 = m * stride + n * 4;
						int num9 = array[num8 + 3];
						int num10 = Math.Max(array[num8], Math.Max(array[num8 + 1], array[num8 + 2]));
						int num11 = Math.Min(array[num8], Math.Min(array[num8 + 1], array[num8 + 2]));
						if (num9 > 128 && num10 - num11 > 24)
						{
							continue;
						}
						double num12 = array3[m * width + n];
						double num13 = num12;
						for (int num14 = -1; num14 <= 1; num14++)
						{
							for (int num15 = -1; num15 <= 1; num15++)
							{
								if (num15 != 0 || num14 != 0)
								{
									int num16 = n + num15;
									int num17 = m + num14;
									if (num16 >= 0 && num17 >= 0 && num16 < width && num17 < height)
									{
										double num18 = Math.Max(0.0, Math.Min(1.0, 1.7 - Math.Sqrt(num15 * num15 + num14 * num14)));
										num13 = Math.Max(num13, array3[num17 * width + num16] * num18);
									}
								}
							}
						}
						if (num13 <= num12 + 0.1 || num12 >= 254.9)
						{
							continue;
						}
						double num19 = Math.Max(0.0, Math.Min(255.0, 255.0 * (num13 - num12) / (255.0 - num12)));
						double num20 = (double)num9 * (1.0 - num19 / 255.0);
						double num21 = num19 + num20;
						array2[num8 + 3] = (byte)Math.Round(num21);
						if (num21 > 0.0)
						{
							for (int num22 = 0; num22 < 3; num22++)
							{
								array2[num8 + num22] = (byte)Math.Round((44.0 * num19 + (double)(int)array[num8 + num22] * num20) / num21);
							}
						}
					}
				}
				Marshal.Copy(array2, 0, bitmapData.Scan0, array2.Length);
			}
			finally
			{
				canvas.UnlockBits(bitmapData);
			}
		}
	}
	internal static class ActivityInput
	{
		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		private delegate IntPtr HookCallback(int code, IntPtr message, IntPtr data);

		private static readonly HookCallback KeyboardDelegate;

		private static readonly HookCallback MouseDelegate;

		private static readonly bool[] Pressed;

		private static IntPtr keyboardHandle;

		private static IntPtr mouseHandle;

		private static int generation;

		private static HookCallback hookCache0;

		private static HookCallback hookCache1;

		[DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
		private static extern IntPtr SetWindowsHookEx(int kind, HookCallback callback, IntPtr module, uint thread);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool UnhookWindowsHookEx(IntPtr handle);

		[DllImport("user32.dll")]
		private static extern IntPtr CallNextHookEx(IntPtr handle, int code, IntPtr message, IntPtr data);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW")]
		private static extern IntPtr GetModuleHandle(string name);

		[DllImport("user32.dll")]
		private static extern short GetAsyncKeyState(int key);

		private static IntPtr KeyboardHook(int code, IntPtr message, IntPtr data)
		{
			if (code >= 0 && (message.ToInt64() == 256 || message.ToInt64() == 260))
			{
				Interlocked.Increment(ref generation);
			}
			return CallNextHookEx(IntPtr.Zero, code, message, data);
		}

		private static IntPtr MouseHook(int code, IntPtr message, IntPtr data)
		{
			if (code >= 0)
			{
				long num = message.ToInt64();
				if (num == 513 || num == 516 || num == 519 || num == 523 || num == 522 || num == 526)
				{
					Interlocked.Increment(ref generation);
				}
			}
			return CallNextHookEx(IntPtr.Zero, code, message, data);
		}

		private static bool IsMouseKey(int key)
		{
			return key == 1 || key == 2 || key == 4 || key == 5 || key == 6;
		}

		private static bool ReadPressed(int key)
		{
			try
			{
				return GetAsyncKeyState(key) < 0;
			}
			catch
			{
				return false;
			}
		}

		internal static int Start()
		{
			Stop();
			try
			{
				IntPtr moduleHandle = GetModuleHandle(null);
				keyboardHandle = SetWindowsHookEx(13, KeyboardDelegate, moduleHandle, 0u);
				mouseHandle = SetWindowsHookEx(14, MouseDelegate, moduleHandle, 0u);
			}
			catch
			{
				Stop();
			}
			if (keyboardHandle == IntPtr.Zero || mouseHandle == IntPtr.Zero)
			{
				for (int i = 1; i < 255; i++)
				{
					Pressed[i] = ReadPressed(i);
				}
			}
			return Interlocked.CompareExchange(ref generation, 0, 0);
		}

		internal static int Version()
		{
			if (keyboardHandle == IntPtr.Zero || mouseHandle == IntPtr.Zero)
			{
				for (int i = 1; i < 255; i++)
				{
					if (!((!IsMouseKey(i)) ? (keyboardHandle != IntPtr.Zero) : (mouseHandle != IntPtr.Zero)))
					{
						bool flag = ReadPressed(i);
						if (flag && !Pressed[i])
						{
							Interlocked.Increment(ref generation);
						}
						Pressed[i] = flag;
					}
				}
			}
			return Interlocked.CompareExchange(ref generation, 0, 0);
		}

		internal static void Stop()
		{
			if (keyboardHandle != IntPtr.Zero)
			{
				try
				{
					UnhookWindowsHookEx(keyboardHandle);
				}
				catch
				{
				}
				keyboardHandle = IntPtr.Zero;
			}
			if (mouseHandle != IntPtr.Zero)
			{
				try
				{
					UnhookWindowsHookEx(mouseHandle);
				}
				catch
				{
				}
				mouseHandle = IntPtr.Zero;
			}
		}

		static ActivityInput()
		{
			if (hookCache0 == null)
			{
				hookCache0 = KeyboardHook;
			}
			KeyboardDelegate = hookCache0;
			if (hookCache1 == null)
			{
				hookCache1 = MouseHook;
			}
			MouseDelegate = hookCache1;
			Pressed = new bool[256];
		}
	}
	internal static class StartupOption
	{
		private const string KeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

		private const string ValueName = "VFriendMini";

		internal static string Command(string path)
		{
			return "\"" + Path.GetFullPath(path) + "\"";
		}

		internal static bool IsEnabled()
		{
			try
			{
				using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", false))
				{
					return registryKey != null && registryKey.GetValue("VFriendMini") is string;
				}
			}
			catch
			{
				return false;
			}
		}

		internal static void SetEnabled(bool enabled)
		{
			if (enabled)
			{
				using (RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
				{
					if (registryKey == null)
					{
						throw new IOException("자동 시작 등록 위치를 열 수 없어요.");
					}
					registryKey.SetValue("VFriendMini", Command(Application.ExecutablePath), RegistryValueKind.String);
				}
				return;
			}
			using (RegistryKey registryKey2 = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
			{
				if (registryKey2 != null)
				{
					registryKey2.DeleteValue("VFriendMini", false);
				}
			}
		}

		internal static void RebindIfEnabled()
		{
			try
			{
				string text;
				using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", false))
				{
					text = ((registryKey != null) ? (registryKey.GetValue("VFriendMini") as string) : null);
				}
				if (text != null && !string.Equals(text, Command(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase))
				{
					SetEnabled(true);
				}
			}
			catch
			{
			}
		}
	}
	internal sealed class AdvancedControls : TableLayoutPanel
	{
		private readonly App app;

		internal readonly CheckBox startup;

		internal readonly CheckBox popup;
        internal readonly CheckBox glow;
        internal readonly ComboBox glowTheme, glowWidth, glowStrength;

		internal readonly CheckBox quiet;

		internal readonly NumericUpDown start;

		internal readonly NumericUpDown end;

		internal AdvancedControls(App owner)
		{
			app = owner;
			AutoSize = true;
			AutoSizeMode = AutoSizeMode.GrowAndShrink;
			base.ColumnCount = 1;
			base.RowCount = 8;
			Dock = DockStyle.Fill;
			base.Margin = new Padding(3, 0, 3, 14);
			startup = Choice("Windows 로그인 시 자동으로 켜기", StartupOption.IsEnabled());
			base.Controls.Add(startup, 0, 0);
			popup = Choice("별도 정각 알림창 사용 (직접 닫을 때까지 유지)", app.Settings.HourlyWindow);
			base.Controls.Add(popup, 0, 1);
            glow = Choice("알림창을 닫을 때까지 화면 가장자리 빛 유지", app.Settings.EdgeGlow);
            base.Controls.Add(glow, 0, 5);
            var colorRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(24, 2, 0, 0) };
            colorRow.Controls.Add(Hint("빛 색상"));
            glowTheme = GlowChoice("glowTheme", new[] { "피치 · 핑크 · 보라 · 하늘", "노을", "오로라", "무지개" }, app.Settings.EdgeGlowTheme, 220);
            colorRow.Controls.Add(glowTheme); base.Controls.Add(colorRow, 0, 6);
            var glowRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(24, 2, 0, 0) };
            glowRow.Controls.Add(Hint("퍼지는 폭"));
            glowWidth = GlowChoice("glowWidth", new[] { "보통", "넓게", "아주 넓게" }, app.Settings.EdgeGlowWidth, 100); glowRow.Controls.Add(glowWidth);
            glowRow.Controls.Add(Hint("진하기"));
            glowStrength = GlowChoice("glowStrength", new[] { "은은하게", "선명하게", "강하게" }, app.Settings.EdgeGlowStrength, 100); glowRow.Controls.Add(glowStrength);
            base.Controls.Add(glowRow, 0, 7);
			quiet = Choice("평일(월~금) 아래 시간에는 정각 알림 끄기", app.Settings.QuietWeekdays);
			quiet.CheckedChanged += QuietChanged;
			base.Controls.Add(quiet, 0, 2);
			FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
			{
				AutoSize = true,
				WrapContents = false,
				Margin = new Padding(24, 2, 0, 0)
			};
			start = Hour(app.Settings.QuietStart);
			end = Hour(app.Settings.QuietEnd);
			flowLayoutPanel.Controls.Add(start);
			flowLayoutPanel.Controls.Add(Hint("시 ~"));
			flowLayoutPanel.Controls.Add(end);
			flowLayoutPanel.Controls.Add(Hint("시 (양 끝 정각 포함)"));
			base.Controls.Add(flowLayoutPanel, 0, 3);
			FlowLayoutPanel flowLayoutPanel2 = new FlowLayoutPanel
			{
				AutoSize = true,
				WrapContents = false
			};
			Label label = Hint("9~18 설정: 평일 09~18시 제외, 주말은 알림");
			label.Margin = new Padding(3, 9, 14, 0);
			flowLayoutPanel2.Controls.Add(label);
			Button button = new Button();
			button.Text = "정각창 미리보기";
			button.AutoSize = true;
			button.Click += PreviewClick;
			flowLayoutPanel2.Controls.Add(button);
			base.Controls.Add(flowLayoutPanel2, 0, 4);
			QuietChanged(null, EventArgs.Empty);
		}

		private static CheckBox Choice(string text, bool value)
		{
			CheckBox checkBox = new CheckBox();
			checkBox.Text = text;
			checkBox.Checked = value;
			checkBox.AutoSize = true;
			checkBox.Anchor = AnchorStyles.Left;
			checkBox.Margin = new Padding(3, 3, 3, 3);
			return checkBox;
		}

		private static NumericUpDown Hour(int value)
		{
			NumericUpDown numericUpDown = new NumericUpDown();
			numericUpDown.Minimum = 0m;
			numericUpDown.Maximum = 23m;
			numericUpDown.Value = Math.Max(0, Math.Min(23, value));
			numericUpDown.Width = 58;
			return numericUpDown;
		}
        private static ComboBox GlowChoice(string name, string[] choices, int selected, int width)
        {
            var input = new ComboBox { Name = name, DropDownStyle = ComboBoxStyle.DropDownList, Width = width, Margin = new Padding(3) };
            input.Items.AddRange(choices); input.SelectedIndex = Math.Max(0, Math.Min(choices.Length - 1, selected)); return input;
        }

		private static Label Hint(string text)
		{
			Label label = new Label();
			label.Text = text;
			label.AutoSize = true;
			label.ForeColor = Color.DimGray;
			label.Margin = new Padding(3, 6, 3, 0);
			return label;
		}

		private void QuietChanged(object sender, EventArgs e)
		{
			if (start != null)
			{
				start.Enabled = quiet.Checked;
			}
			if (end != null)
			{
				end.Enabled = quiet.Checked;
			}
		}

		private void PreviewClick(object sender, EventArgs e)
		{
			HourlyNotice hourlyNotice = new HourlyNotice(app.TrayIconImage);
			DateTime now = DateTime.Now;
			int num = now.Hour % 12;
			if (num == 0)
			{
				num = 12;
			}
			hourlyNotice.SetMessage("지금은 " + ((now.Hour >= 12) ? "오후 " : "오전 ") + num + "시야!\n(미리보기)");
            hourlyNotice.EnableEdgeGlow(glow.Checked, glowTheme.SelectedIndex, glowWidth.SelectedIndex, glowStrength.SelectedIndex);
			hourlyNotice.Show();
		}

		internal void SaveTo(Settings settings)
		{
			StartupOption.SetEnabled(startup.Checked);
			settings.HourlyWindow = popup.Checked;
            settings.EdgeGlow = glow.Checked;
            settings.EdgeGlowTheme = glowTheme.SelectedIndex; settings.EdgeGlowWidth = glowWidth.SelectedIndex; settings.EdgeGlowStrength = glowStrength.SelectedIndex;
			settings.QuietWeekdays = quiet.Checked;
			settings.QuietStart = (int)start.Value;
			settings.QuietEnd = (int)end.Value;
		}
	}
}
