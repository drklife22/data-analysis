using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

namespace WalkingFriend
{
    internal sealed partial class FriendData
    {
        internal double TogetherSeconds;
        internal double Satiety = 100;
        internal const double SnackPortion = 25;
        internal bool CanSnack { get { return Satiety < 100; } }
        internal long CelebratedHundreds;
        internal HashSet<string> Discoveries = new HashSet<string>();
        internal static string StorageOverride;
        internal static string DataDirectory { get { return StorageOverride ?? Settings.AltDir; } }
        internal static string DataPath { get { return Path.Combine(DataDirectory, Name); } }
        internal bool Hungry { get { return PlayOptions.Current.HungerCry && (PlayOptions.Current.HungerMinutes == 0 || Satiety <= 20); } }
        internal static double Number(XmlNode node, string name, double fallback)
        {
            double value; var a = node.Attributes[name];
            return a != null && double.TryParse(a.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !double.IsNaN(value) && !double.IsInfinity(value) ? value : fallback;
        }
        internal static void LoadCompanion()
        {
            Current = new FriendData();
            string canonical = DataPath, portable = Path.Combine(Settings.ExeDir, Name);
            var paths = new[] { canonical, portable }.Distinct().Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc).ToList();
            if (File.Exists(canonical))
            {
                try { var header = new XmlDocument(); header.XmlResolver = null; header.Load(canonical);
                    string schema = header.DocumentElement.GetAttribute("schema");
                    if (schema == "204" || schema == "205") { paths.Remove(canonical); paths.Insert(0, canonical); } }
                catch { }
            }
            if (File.Exists(canonical + ".bak")) paths.Add(canonical + ".bak");
            foreach (string path in paths)
            {
                try
                {
                    var doc = new XmlDocument(); doc.XmlResolver = null;
                    using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) doc.Load(reader);
                    XmlElement root = doc.DocumentElement; if (root == null || root.Name != "friend") continue;
                    var data = new FriendData(); long value;
                    if (long.TryParse(root.GetAttribute("affinity"), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) data.Affinity = Math.Max(0, value);
                    data.TogetherSeconds = Math.Max(0, Number(root, "togetherSeconds", 0));
                    data.Satiety = Math.Max(0, Math.Min(100, Number(root, "satiety", 100)));
                    data.CelebratedHundreds = data.Affinity / 100;
                    if (long.TryParse(root.GetAttribute("celebratedHundreds"), out value)) data.CelebratedHundreds = Math.Max(0, Math.Min(data.Affinity / 100, value));
                    DateTime date;
                    var notes = root.SelectSingleNode("notes"); if (notes != null) data.Notes = notes.InnerText;
                    var timer = root.SelectSingleNode("timer");
                    if (timer != null)
                    {
                        var until = timer.Attributes["until"];
                        if (until != null && DateTime.TryParse(until.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date)) data.TimerUtc = date.ToUniversalTime();
                        data.TimerText = timer.InnerText;
                    }
                    foreach (XmlNode t in root.SelectNodes("tasks/task"))
                        data.Todos.Add(new TodoItem { Text = t.InnerText, Done = t.Attributes["done"] != null && t.Attributes["done"].Value == "1" });
                    foreach (XmlNode egg in root.SelectNodes("discoveries/egg")) data.Discoveries.Add(egg.InnerText);
                    Current = data; return;
                }
                catch { }
            }
        }
        internal bool SaveCompanion()
        {
            var doc = new XmlDocument(); var root = doc.CreateElement("friend"); doc.AppendChild(root);
            root.SetAttribute("schema", "205"); root.SetAttribute("affinity", Affinity.ToString(CultureInfo.InvariantCulture));
            root.SetAttribute("togetherSeconds", TogetherSeconds.ToString("R", CultureInfo.InvariantCulture));
            root.SetAttribute("satiety", Satiety.ToString("R", CultureInfo.InvariantCulture));
            root.SetAttribute("celebratedHundreds", CelebratedHundreds.ToString(CultureInfo.InvariantCulture));
            var notes = doc.CreateElement("notes"); notes.InnerText = Notes; root.AppendChild(notes);
            var tasks = doc.CreateElement("tasks"); root.AppendChild(tasks);
            foreach (var t in Todos) { var e = doc.CreateElement("task"); e.InnerText = t.Text; e.SetAttribute("done", t.Done ? "1" : "0"); tasks.AppendChild(e); }
            var timer = doc.CreateElement("timer"); timer.InnerText = TimerText; timer.SetAttribute("until", TimerUtc.ToString("o")); root.AppendChild(timer);
            var eggs = doc.CreateElement("discoveries"); root.AppendChild(eggs);
            foreach (string name in Discoveries.OrderBy(x => x)) { var e = doc.CreateElement("egg"); e.InnerText = name; eggs.AppendChild(e); }
            foreach (string directory in new[] { DataDirectory, Settings.ExeDir }.Distinct())
            {
                string tmp = null;
                try
                {
                    Directory.CreateDirectory(directory); string dest = Path.Combine(directory, Name);
                    tmp = dest + "." + Guid.NewGuid().ToString("N") + ".tmp"; doc.Save(tmp);
                    if (File.Exists(dest))
                    {
                        try { File.Replace(tmp, dest, dest + ".bak"); }
                        catch { File.Copy(dest, dest + ".bak", true); File.Copy(tmp, dest, true); File.Delete(tmp); }
                    }
                    else File.Move(tmp, dest);
                    lastSave = DateTime.UtcNow; return true;
                }
                catch { try { if (tmp != null) File.Delete(tmp); } catch { } }
            }
            return false;
        }
        internal void AdvanceLife(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            double oldMinutes = Math.Floor(TogetherSeconds / 60);
            TogetherSeconds += seconds;
            Satiety = Math.Max(0, Satiety - seconds * 80.0 / (Math.Max(1, PlayOptions.Current.HungerMinutes) * 60.0));
            int points = (int)Math.Min(int.MaxValue, Math.Floor(TogetherSeconds / 60) - oldMinutes);
            if (points > 0) Like(points);
        }
        internal bool TrySnack()
        {
            if (!CanSnack) return false;
            Satiety = Math.Min(100, Satiety + SnackPortion); Like(5); Save(); return true;
        }
        internal bool TakeMilestone()
        { if (Affinity / 100 <= CelebratedHundreds) return false; CelebratedHundreds++; Save(); return true; }
        internal void Discover(string name) { if (Discoveries.Add(name)) Save(); }
        internal static string FormatTogether(double seconds)
        {
            long total = Math.Max(0, (long)(seconds / 60));
            return (total / 60).ToString("N0") + "시간 " + (total % 60) + "분";
        }
    }

    internal sealed partial class FriendPanel
    {
        private Label lifeSummary, lifeDetails, discoverySummary, taskHeader;
        private Button snackButton;
        private void AddLifeTab(TabControl tabs, App app)
        {
            var tab = new TabPage("친구 기록") { BackColor = BackColor, Padding = new Padding(12) }; tabs.TabPages.Add(tab);
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            tab.Controls.Add(panel);
            lifeSummary = FriendUI.Label(""); lifeSummary.MaximumSize = new Size(430, 0); lifeSummary.Font = new Font(Font.FontFamily, 13, FontStyle.Bold); panel.Controls.Add(lifeSummary);
            lifeDetails = FriendUI.Label(""); panel.Controls.Add(lifeDetails);
            snackButton = FriendUI.Button("간식 주기", delegate { var pet = app.LiveWindows.Select(w => w.Pet).FirstOrDefault(); if (pet != null) pet.Feed(app); UpdateLifeView(); }); panel.Controls.Add(snackButton);
            panel.Controls.Add(FriendUI.Label("생활 설정")); var options = PlayOptions.Current;
            Func<string, decimal, decimal, NumericUpDown> number = delegate(string title, decimal value, decimal max)
            {
                var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
                var label = FriendUI.Label(title); label.Width = 224; label.AutoSize = false; row.Controls.Add(label);
                var input = new NumericUpDown { Name = "hungerMinutes", Minimum = 0, Maximum = max, Value = value, Width = 72, Margin = new Padding(4, 5, 4, 0) }; row.Controls.Add(input);
                row.Controls.Add(FriendUI.Label("분")); panel.Controls.Add(row); return input;
            };
            panel.Controls.Add(FriendUI.Label("간식은 포만감 +25씩, 최대 100까지 채워줘요."));
            panel.Controls.Add(FriendUI.Label("함께한 시간 1분마다 친밀도도 1씩 쌓여요."));
            var hunger = number("간식 없이 배고파지기까지", options.HungerMinutes, 360);
            panel.Controls.Add(FriendUI.Label("0분으로 설정하면 항상 우는 표정을 볼 수 있어요."));
            var cry = new CheckBox { AutoSize = true, Text = "배고프면 계속 우는 표정", Checked = options.HungerCry, Margin = new Padding(4, 6, 4, 4) }; panel.Controls.Add(cry);
            var secrets = new CheckBox { AutoSize = true, Text = "숨겨진 반응 사용", Checked = options.Secrets, Margin = new Padding(4, 6, 4, 4) }; panel.Controls.Add(secrets);
            panel.Controls.Add(FriendUI.Button("생활 설정 저장", delegate
            {
                options.HungerMinutes = (int)hunger.Value; options.HungerCry = cry.Checked; options.Secrets = secrets.Checked;
                if (!options.Save()) MessageBox.Show(this, "생활 설정을 저장하지 못했어.", "걸어다니는 친구"); UpdateLifeView();
            }));
            discoverySummary = FriendUI.Label(""); discoverySummary.MaximumSize = new Size(430, 0); panel.Controls.Add(discoverySummary);
            var hints = FriendUI.Label("어질어질: 친구 머리 위에서 마우스를 한 바퀴 빙글빙글.\n몰래 군무: 친구 둘 이상일 때 커서를 떨어뜨리고 4초.\n우클릭 → 동작 바로 보기에서도 확인할 수 있어요.");
            hints.MaximumSize = new Size(430, 0); panel.Controls.Add(hints);
            UpdateLifeView();
        }
        private void UpdateLifeView()
        {
            if (lifeSummary == null) return; var d = FriendData.Current;
            lifeSummary.Text = "♥ 친밀도 " + d.Affinity.ToString("N0") + "   ·   함께 " + FriendData.FormatTogether(d.TogetherSeconds);
            lifeDetails.Text = "포만감 " + (int)Math.Floor(d.Satiety) + " / 100\n다음 꽃다발: 친밀도 " + ((d.Affinity / 100 + 1) * 100).ToString("N0")
                + "\n" + (d.CanSnack ? "지금 간식을 줄 수 있어." : "지금은 배가 꽉 찼어");
            snackButton.Enabled = d.CanSnack;
            var names = new[] { new[] { "dance", "몰래 하는 군무" }, new[] { "dizzy", "어질어질" }, new[] { "pilot", "비밀 조종사" } };
            discoverySummary.Text = "발견한 비밀 " + d.Discoveries.Count + " / 3\n" + string.Join("  ·  ", names.Where(p => d.Discoveries.Contains(p[0])).Select(p => p[1]).ToArray());
            if (taskHeader != null) taskHeader.Text = "하나씩 끝내자!   친밀도 " + d.Affinity.ToString("N0");
        }
    }
}
