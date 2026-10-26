using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

namespace WalkingFriend
{
    internal sealed class TodoItem
    {
        internal string Text; internal bool Done;
        public override string ToString() { return Text; }
    }
    internal sealed partial class FriendData
    {
        internal static FriendData Current = new FriendData();
        internal List<TodoItem> Todos = new List<TodoItem>();
        internal string Notes = "";
        internal long Affinity;
        internal DateTime TimerUtc = DateTime.MinValue;
        internal string TimerText = "잠깐 쉬어가자!";
        private DateTime lastSave = DateTime.MinValue;
        internal static string Name = "걸어다니는친구_할일.xml";
        internal static void Load() { LoadCompanion(); }
        internal bool Save() { return SaveCompanion(); }
        internal void Like(int points)
        {
            if (points <= 0) return;
            Affinity = Affinity > long.MaxValue - points ? long.MaxValue : Affinity + points;
            if ((DateTime.UtcNow - lastSave).TotalSeconds > 10) Save();
        }
        internal void PollTimer(App app)
        {
            if (TimerUtc == DateTime.MinValue || DateTime.UtcNow < TimerUtc) return;
            TimerUtc = DateTime.MinValue; Save();
            var notice = new HourlyNotice(app.TrayIconImage); notice.Text = "걸어다니는 친구 · 타이머";
            notice.SetMessage(string.IsNullOrWhiteSpace(TimerText) ? "설정한 시간이 됐어!" : TimerText); notice.Show();
            app.Announce(TimerText);
        }
    }
    internal static class FriendUI
    {
        internal static void Setup(Form f, Icon icon)
        {
            f.Font = new Font("Malgun Gothic", 9f); f.BackColor = Color.FromArgb(255, 252, 249);
            f.StartPosition = FormStartPosition.CenterScreen; f.AutoScaleMode = AutoScaleMode.Dpi;
            f.MinimizeBox = false; f.MaximizeBox = false; f.FormBorderStyle = FormBorderStyle.FixedDialog;
            if (icon != null) f.Icon = icon;
        }
        internal static Button Button(string text, EventHandler click)
        { var b = new Button { Text = text, AutoSize = true, Padding = new Padding(6, 3, 6, 3), Margin = new Padding(4) }; b.Click += click; return b; }
        internal static Label Label(string text)
        { return new Label { Text = text, AutoSize = true, ForeColor = Color.FromArgb(68, 50, 50), Margin = new Padding(4, 8, 4, 8) }; }
    }
    internal sealed class PlaySettingsForm : Form
    {
        internal PlaySettingsForm(App app)
        {
            FriendUI.Setup(this, app.TrayIconImage); Text = "걸어다니는 친구 · 움직임과 반응"; ClientSize = new Size(480, 640);
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(18, 12, 18, 12) };
            Controls.Add(panel);
            panel.Controls.Add(FriendUI.Label("친구가 어떻게 놀지 골라줘"));
            var o = PlayOptions.Current;
            Func<string, bool, CheckBox> choice = delegate(string text, bool value) { var c = new CheckBox { Text = text, Checked = value, AutoSize = true, Margin = new Padding(4, 3, 4, 3) }; panel.Controls.Add(c); return c; };
            var walls = choice("화면 양옆 벽 타기", o.Walls);
            var ceiling = choice("화면 천장 타기", o.Ceiling);
            var windows = choice("열린 창 옆을 올라가고 창 위에서 걷기 · 쉬기", o.Windows);
            var idle = choice("혼자 앉아서 쉬기", o.Idle);
            var shortRest = choice("앉아있는 시간 줄이기 (기존 6~11초)", o.ShortRest);
            var escape = choice("커서가 가까이 오면 슬라이딩으로 도망가기", o.Escape);
            var follow = choice("커서를 따라오기", o.Follow);
            escape.CheckedChanged += delegate { if (escape.Checked) follow.Checked = false; };
            follow.CheckedChanged += delegate { if (follow.Checked) escape.Checked = false; };
            var focus = choice("얌전 모드: 바닥에만 머물고 도망가지 않기", o.Focus);
            var work = choice("평일 09:00~18:00에는 자동으로 얌전 모드", o.WorkFocus);
            var full = choice("전체화면 영상 · 게임을 볼 때 자동으로 숨기기", o.Fullscreen);
            var affinity = choice("친구 옆에 친밀도 숫자 표시", o.ShowAffinity);
            panel.Controls.Add(FriendUI.Label("속도 조절  ·  숫자가 클수록 빨라져요"));
            Func<string, string, double, decimal, NumericUpDown> speedRow = delegate(string label, string name, double value, decimal maximum)
            {
                var r = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 1, 0, 1) };
                var caption = FriendUI.Label(label); caption.AutoSize = false; caption.Width = 206; caption.Height = 23; caption.Margin = new Padding(4, 5, 4, 0); r.Controls.Add(caption);
                var number = new NumericUpDown { Name = name, Minimum = .5m, Maximum = maximum, DecimalPlaces = 1, Increment = .1m, Value = (decimal)value, Width = 82, Margin = new Padding(4) };
                r.Controls.Add(number); var unit = FriendUI.Label("배"); unit.Margin = new Padding(4, 5, 4, 0); r.Controls.Add(unit); panel.Controls.Add(r); return number;
            };
            var speed = speedRow("걷기 · 기어오르기", "walkSpeed", o.Speed, 2m);
            var fallSpeed = speedRow("헬리콥터 낙하", "fallSpeed", o.FallSpeed, 3m);
            var slideSpeed = speedRow("슬라이딩", "slideSpeed", o.SlideSpeed, 3m);
            var delayRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            delayRow.Controls.Add(FriendUI.Label("슬라이딩 반응 대기"));
            var escapeDelay = new NumericUpDown { Name = "escapeDelay", Minimum = .05m, Maximum = 1m, DecimalPlaces = 2, Increment = .05m, Value = (decimal)o.EscapeDelay, Width = 80, Margin = new Padding(4) };
            delayRow.Controls.Add(escapeDelay); delayRow.Controls.Add(FriendUI.Label("초")); panel.Controls.Add(delayRow);
            panel.Controls.Add(FriendUI.Label("클릭했을 때 반응"));
            var clickMode = new ComboBox { Name = "clickMode", DropDownStyle = ComboBoxStyle.DropDownList, Width = 412, Margin = new Padding(4) };
            clickMode.Items.AddRange(new object[] { "눌리기 · 폴짝 뛰기 (배고프면 울기도)", "무작위 반응 (배고프면 울기도)", "눌리기만", "배고프면 울기 · 배부르면 눌리기", "폴짝 뛰기만", "클릭 반응 끄기" });
            clickMode.SelectedIndex = (int)o.ClickMode; panel.Controls.Add(clickMode);
            panel.Controls.Add(FriendUI.Label("살짝 놓으면 헬리콥터, 휙 던지면 날아간 뒤 내려와요.\n저장한 속도와 클릭 반응은 다음 실행에도 유지돼요."));
            var row = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(18, 3, 18, 3), WrapContents = false };
            var save = FriendUI.Button("저장", delegate
            {
                o.Walls = walls.Checked; o.Ceiling = ceiling.Checked; o.Windows = windows.Checked; o.Idle = idle.Checked;
                o.ShortRest = shortRest.Checked;
                o.Escape = escape.Checked; o.Follow = follow.Checked; o.Focus = focus.Checked; o.WorkFocus = work.Checked; o.Fullscreen = full.Checked;
                o.ShowAffinity = affinity.Checked;
                o.Speed = (double)speed.Value; o.FallSpeed = (double)fallSpeed.Value; o.SlideSpeed = (double)slideSpeed.Value; o.ClickMode = (ClickReaction)clickMode.SelectedIndex; o.EscapeDelay = (double)escapeDelay.Value;
                if (!o.Save()) MessageBox.Show(this, "설정을 파일에 저장하지 못했어. 지금 실행 중에는 적용돼.", "걸어다니는 친구"); Close();
            });
            save.Name = "save"; row.Controls.Add(save); var cancel = FriendUI.Button("취소", delegate { Close(); }); row.Controls.Add(cancel); Controls.Add(row); AcceptButton = save; CancelButton = cancel;
        }
    }
    internal sealed partial class FriendPanel : Form
    {
        private readonly CheckedListBox tasks;
        private readonly TextBox notes;
        private readonly Label remaining;
        private readonly System.Windows.Forms.Timer timer;
        private bool populating, notesDirty;
        private DateTime notesChanged;
        internal FriendPanel(App app)
        {
            FriendUI.Setup(this, app.TrayIconImage); Text = "걸어다니는 친구 · 오늘의 작은 도우미"; ClientSize = new Size(510, 495);
            var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 7) }; Controls.Add(tabs); AddLifeTab(tabs, app);
            var todo = new TabPage("할 일") { BackColor = BackColor, Padding = new Padding(12) }; tabs.TabPages.Add(todo);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 }; layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); todo.Controls.Add(layout);
            taskHeader = FriendUI.Label("하나씩 끝내자!   친밀도 " + FriendData.Current.Affinity); layout.Controls.Add(taskHeader, 0, 0);
            tasks = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle }; layout.Controls.Add(tasks, 0, 1);
            var entryRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            var entry = new TextBox { Width = 230, Margin = new Padding(3, 9, 3, 3), MaxLength = 200 };
            entryRow.Controls.Add(entry); entryRow.Controls.Add(FriendUI.Button("추가", delegate { AddTask(entry); }));
            entryRow.Controls.Add(FriendUI.Button("삭제", delegate
            { int i = tasks.SelectedIndex; if (i >= 0) { FriendData.Current.Todos.RemoveAt(i); RefreshTasks(); SaveOrWarn(); } }));
            entry.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { AddTask(entry); e.SuppressKeyPress = true; } };
            layout.Controls.Add(entryRow, 0, 2);
            RefreshTasks();
            tasks.ItemCheck += delegate(object sender, ItemCheckEventArgs e)
            {
                if (populating || e.Index >= FriendData.Current.Todos.Count) return;
                FriendData.Current.Todos[e.Index].Done = e.NewValue == CheckState.Checked;
                if (e.NewValue == CheckState.Checked) { FriendData.Current.Like(2); app.Announce("하나 끝냈다! 수고했어"); } SaveOrWarn();
            };
            var memo = new TabPage("메모") { BackColor = BackColor, Padding = new Padding(12) }; tabs.TabPages.Add(memo);
            notes = new TextBox { Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, Text = FriendData.Current.Notes, MaxLength = 100000 };
            memo.Controls.Add(notes); notes.TextChanged += delegate { FriendData.Current.Notes = notes.Text; notesDirty = true; notesChanged = DateTime.UtcNow; };
            var time = new TabPage("타이머") { BackColor = BackColor, Padding = new Padding(15) }; tabs.TabPages.Add(time);
            var clock = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false }; time.Controls.Add(clock);
            clock.Controls.Add(FriendUI.Label("집중 시간이나 잠깐 쉬는 시간을 정해줘."));
            var duration = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            var minutes = new NumericUpDown { Minimum = 1, Maximum = 240, Value = 25, Width = 80, Margin = new Padding(4) }; duration.Controls.Add(minutes); duration.Controls.Add(FriendUI.Label("분 뒤에 알려주기")); clock.Controls.Add(duration);
            var message = new TextBox { Width = 400, Text = FriendData.Current.TimerText, MaxLength = 200, Margin = new Padding(4, 8, 4, 8) }; clock.Controls.Add(message);
            var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            actions.Controls.Add(FriendUI.Button("시작", delegate { FriendData.Current.TimerText = message.Text; FriendData.Current.TimerUtc = DateTime.UtcNow.AddMinutes((double)minutes.Value); SaveOrWarn(); UpdateTimer(); }));
            actions.Controls.Add(FriendUI.Button("타이머 취소", delegate { FriendData.Current.TimerUtc = DateTime.MinValue; SaveOrWarn(); UpdateTimer(); })); clock.Controls.Add(actions);
            remaining = FriendUI.Label(""); remaining.Font = new Font(Font.FontFamily, 20, FontStyle.Bold); clock.Controls.Add(remaining);
            clock.Controls.Add(FriendUI.Label("시간이 되면 직접 닫는 알림창이 떠요.\n도우미 창을 닫아도 타이머는 계속 돌아가요."));
            timer = new System.Windows.Forms.Timer { Interval = 500 }; timer.Tick += delegate { UpdateTimer(); UpdateLifeView(); if (notesDirty && (DateTime.UtcNow - notesChanged).TotalSeconds > 1) { notesDirty = false; SaveOrWarn(); } }; timer.Start();
            FormClosed += delegate { timer.Stop(); timer.Dispose(); FriendData.Current.Notes = notes.Text; FriendData.Current.Save(); }; UpdateTimer();
        }
        private void AddTask(TextBox entry)
        { if (string.IsNullOrWhiteSpace(entry.Text)) return; FriendData.Current.Todos.Add(new TodoItem { Text = entry.Text.Trim() }); entry.Clear(); RefreshTasks(); SaveOrWarn(); entry.Focus(); }
        private void RefreshTasks()
        { populating = true; tasks.Items.Clear(); foreach (var t in FriendData.Current.Todos) tasks.Items.Add(t, t.Done); populating = false; }
        private void UpdateTimer()
        {
            var d = FriendData.Current.TimerUtc;
            if (d == DateTime.MinValue) remaining.Text = "타이머 대기 중";
            else { int seconds = Math.Max(0, (int)Math.Ceiling((d - DateTime.UtcNow).TotalSeconds)); remaining.Text = string.Format("{0:00}:{1:00} 남았어", seconds / 60, seconds % 60); }
        }
        private void SaveOrWarn()
        { if (!FriendData.Current.Save()) MessageBox.Show(this, "파일에 저장하지 못했어. 저장 위치의 쓰기 권한을 확인해 줘.", "걸어다니는 친구"); }
    }
}
