using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace CountdownWidget
{
    public class CfgItem
    {
        public string name { get; set; }
        public string date { get; set; }
        public bool enabled { get; set; }
    }
    public class Cfg
    {
        public string display_mode { get; set; }
        public int rotate_seconds { get; set; }
        public bool glass { get; set; }
        public bool show_seconds { get; set; }
        public string theme { get; set; }
        public string skin { get; set; }
        public List<CfgItem> items { get; set; }
        public Cfg() { display_mode = "expand"; rotate_seconds = 4; glass = true; theme = "system"; skin = "default"; items = new List<CfgItem>(); }
    }
    public static partial class Program
    {
        static readonly string Dir = AppDomain.CurrentDomain.BaseDirectory;
        static readonly string ConfigPath = Path.Combine(Dir, "config.json");
        static readonly string PosPath = Path.Combine(Dir, "pos.json");
        static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        static Cfg Config = new Cfg();
        static Window W, Editor;
        static Border Root;
        static StackPanel Host;
        static IntPtr Hwnd;
        static DateTime LastWrite;
        static readonly System.Diagnostics.Stopwatch RotationClock = System.Diagnostics.Stopwatch.StartNew();
        static List<CfgItem> Items = new List<CfgItem>();
        static readonly List<Tuple<CfgItem, TextBlock, TextBlock>> Slots = new List<Tuple<CfgItem, TextBlock, TextBlock>>();
        static bool Light, Dragging, Moved;
        static double Offset = 236, StartLeft;
        static int StartX, Index;
        static bool Preview;
        static bool MenuOpen;
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [DllImport("user32.dll")] static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential)] struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }
        static IntPtr _mouseHook = IntPtr.Zero;
        static LowLevelMouseProc _mouseProc;
        static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam) {
            try {
                if (nCode >= 0) {
                    int msg = wParam.ToInt32();
                    if (msg == 0x0201 || msg == 0x0204) {
                        var menu = W != null ? W.ContextMenu : null;
                        if (menu != null && menu.IsOpen) {
                            POINT cp; GetCursorPos(out cp);
                            bool inside = false;
                            var src = PresentationSource.FromVisual(menu) as HwndSource;
                            if (src != null && src.Handle != IntPtr.Zero) {
                                RECT r;
                                if (GetWindowRect(src.Handle, out r))
                                    inside = cp.X >= r.Left && cp.X <= r.Right && cp.Y >= r.Top && cp.Y <= r.Bottom;
                            }
                            if (!inside) menu.Dispatcher.BeginInvoke(new Action(() => { try { menu.IsOpen = false; } catch { } }));
                        }
                    }
                }
            } catch { }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] static extern int SetWindowLong(IntPtr hwnd, int index, int value);

        public static DateTime? ParseDate(string value)
        {
            DateTime d;
            string[] formats = { "yyyy-M-d H:mm", "yyyy-M-d H:mm:ss", "yyyy-M-d", "yyyy/M/d H:mm", "yyyy/M/d" };
            return DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out d) ? (DateTime?)d : null;
        }
        // Days-only mode counts calendar days; detailed mode counts complete 24-hour periods.
        public static string DayText(DateTime target, DateTime now, bool detailed)
        {
            if (target <= now) return "已到期";
            int days = detailed ? (int)(target - now).TotalDays : (target.Date - now.Date).Days;
            return !detailed && days == 0 ? "今天" : days.ToString(CultureInfo.InvariantCulture);
        }
        static void Log(Exception ex) { try { File.AppendAllText(Path.Combine(Dir, "widget_error.log"), DateTime.Now + " " + ex + Environment.NewLine); } catch { } }
        static Cfg ReadConfig()
        {
            if (!File.Exists(ConfigPath)) return new Cfg();
            var c = Json.Deserialize<Cfg>(File.ReadAllText(ConfigPath)) ?? new Cfg();
            c.items = c.items ?? new List<CfgItem>();
            c.rotate_seconds = Math.Max(2, Math.Min(60, c.rotate_seconds));
            return c;
        }
        static void SaveConfig(Cfg c)
        {
            string temp = ConfigPath + ".tmp";
            File.WriteAllText(temp, Json.Serialize(c), new System.Text.UTF8Encoding(false));
            if (File.Exists(ConfigPath)) File.Replace(temp, ConfigPath, null); else File.Move(temp, ConfigPath);
            Config = c; LastWrite = File.GetLastWriteTimeUtc(ConfigPath); Index = 0; RotationClock.Restart();
            if (W != null) Build();
        }
        static bool IsLight()
        {
            if (Config.theme == "light") return true;
            if (Config.theme == "dark") return false;
            return Convert.ToInt32(Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 1)) != 0;
        }
        static SolidColorBrush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        static string _skinText = "#26313B", _skinMuted = "#6B7580", _skinAccent = "#26313B";
        static Brush Ink { get { return Brush(_skinText); } }
        static Brush Muted { get { return Brush(_skinMuted); } }
        static Brush ValueInk { get { return Brush(_skinAccent); } }
        static string ActiveSkinId = "default";
        sealed class SkinDef { public string Bg1, Bg2, Text, Muted, Accent; }
        static SkinDef PickSkin(string id, bool light) {
            if (string.IsNullOrEmpty(id)) id = "default";
            if (id == "auto") id = DetectFestivalSkin();
            switch (id) {
                case "christmas": return light ? new SkinDef{Bg1="#FFFFFF",Bg2="#E4F2E7",Text="#1F3B2A",Muted="#5C7A67",Accent="#C0392B"} : new SkinDef{Bg1="#1B3324",Bg2="#12241A",Text="#EAF5EC",Muted="#9CBDA6",Accent="#FF6B5A"};
                case "newyear": return light ? new SkinDef{Bg1="#FFFFFF",Bg2="#EAF0FB",Text="#23324A",Muted="#6B7A8F",Accent="#B8860B"} : new SkinDef{Bg1="#1E2A44",Bg2="#141C2E",Text="#EEF2FA",Muted="#9FB0C9",Accent="#E8C15A"};
                case "spring": return light ? new SkinDef{Bg1="#FFFFFF",Bg2="#FFE6DE",Text="#4A1B15",Muted="#8A5A50",Accent="#D6452E"} : new SkinDef{Bg1="#3A1712",Bg2="#26100C",Text="#FFEDE8",Muted="#C99A90",Accent="#FF7A5A"};
                case "midautumn": return light ? new SkinDef{Bg1="#FFFFFF",Bg2="#F4EBD0",Text="#2A2E4A",Muted="#6E7391",Accent="#B8860B"} : new SkinDef{Bg1="#20263F",Bg2="#141830",Text="#F0F1FA",Muted="#A0A6C4",Accent="#E3B45A"};
                case "national": return light ? new SkinDef{Bg1="#FFFFFF",Bg2="#FFE6DE",Text="#4A1B15",Muted="#8A5A50",Accent="#C0392B"} : new SkinDef{Bg1="#3A1712",Bg2="#26100C",Text="#FFEDE8",Muted="#C99A90",Accent="#FF7A5A"};
                default: return light ? new SkinDef{Bg1="#FFFFFF",Bg2="#EEF3F8",Text="#26313B",Muted="#6B7580",Accent="#26313B"} : new SkinDef{Bg1="#2A2D33",Bg2="#20242A",Text="#EDF0F4",Muted="#A6AFBA",Accent="#EDF0F4"};
            }
        }
        static string DetectFestivalSkin() {
            var it = Items.FirstOrDefault();
            if (it == null) return "default";
            var n = it.name ?? "";
            if (n.Contains("圣诞")) return "christmas";
            if (n.Contains("元旦") || n.Contains("新年")) return "newyear";
            if (n.Contains("春节") || n.Contains("过年")) return "spring";
            if (n.Contains("中秋")) return "midautumn";
            if (n.Contains("国庆")) return "national";
            return "default";
        }
        static string HexA(string alpha, string rgb) { return "#" + alpha + rgb.TrimStart('#'); }
        static string ResolveSkinId() {
            string id = Config.skin;
            if (string.IsNullOrEmpty(id)) return "default";
            if (id == "auto") return DetectFestivalSkin();
            return id;
        }
        static void AddPoly(Canvas c, string pts, Color fill) {
            var coll = new PointCollection();
            foreach (var pair in pts.Split(' ')) {
                var xy = pair.Split(',');
                coll.Add(new Point(double.Parse(xy[0], CultureInfo.InvariantCulture), double.Parse(xy[1], CultureInfo.InvariantCulture)));
            }
            c.Children.Add(new System.Windows.Shapes.Polygon { Points = coll, Fill = new SolidColorBrush(fill) });
        }
        static Color C_(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
        static FrameworkElement Decor() {
            var cv = new Canvas { Width = 20, Height = 22 };
            Color gold = C_("#E0B341"), green = C_("#2E7D4F"), red = C_("#D6452E");
            switch (ActiveSkinId) {
                case "christmas":
                    var trunk = new System.Windows.Shapes.Rectangle { Width = 4, Height = 5, Fill = new SolidColorBrush(C_("#7A4A21")) };
                    Canvas.SetLeft(trunk, 8); Canvas.SetTop(trunk, 16); cv.Children.Add(trunk);
                    AddPoly(cv, "10,2 4,9 16,9", green);
                    AddPoly(cv, "10,6 2,14 18,14", green);
                    AddPoly(cv, "10,10 0,19 20,19", green);
                    AddPoly(cv, "10,0 8.6,3 11.4,3", gold);
                    return cv;
                case "newyear":
                    AddPoly(cv, "10,0 12.2,7 20,7 13.8,11.5 16,19 10,14.5 4,19 6.2,11.5 0,7 7.8,7", gold);
                    var d1 = new System.Windows.Shapes.Ellipse { Width = 3, Height = 3, Fill = new SolidColorBrush(gold) }; Canvas.SetLeft(d1, 16); Canvas.SetTop(d1, 0); cv.Children.Add(d1);
                    return cv;
                case "spring":
                    var cap1 = new System.Windows.Shapes.Rectangle { Width = 8, Height = 2, Fill = new SolidColorBrush(gold) }; Canvas.SetLeft(cap1, 6); Canvas.SetTop(cap1, 2); cv.Children.Add(cap1);
                    var body = new System.Windows.Shapes.Ellipse { Width = 16, Height = 14, Fill = new SolidColorBrush(red) }; Canvas.SetLeft(body, 2); Canvas.SetTop(body, 4); cv.Children.Add(body);
                    var cap2 = new System.Windows.Shapes.Rectangle { Width = 8, Height = 2, Fill = new SolidColorBrush(gold) }; Canvas.SetLeft(cap2, 6); Canvas.SetTop(cap2, 18); cv.Children.Add(cap2);
                    var tassel = new System.Windows.Shapes.Rectangle { Width = 1, Height = 4, Fill = new SolidColorBrush(gold) }; Canvas.SetLeft(tassel, 10); Canvas.SetTop(tassel, 20); cv.Children.Add(tassel);
                    return cv;
                case "midautumn":
                    var moon = new System.Windows.Shapes.Ellipse { Width = 16, Height = 16, Fill = new SolidColorBrush(C_("#F0D48A")) }; Canvas.SetLeft(moon, 2); Canvas.SetTop(moon, 3); cv.Children.Add(moon);
                    var d3 = new System.Windows.Shapes.Ellipse { Width = 2, Height = 2, Fill = new SolidColorBrush(gold) }; Canvas.SetLeft(d3, 0); Canvas.SetTop(d3, 0); cv.Children.Add(d3);
                    return cv;
                case "national":
                    AddPoly(cv, "10,0 12.4,6.8 19.5,6.8 13.8,11 16,18 10,13.8 4,18 6.2,11 0.5,6.8 7.6,6.8", red);
                    return cv;
                default:
                    return null;
            }
        }
        static TextBlock Text(string value, double size, Brush color)
        {
            return new TextBlock { Text = value, FontSize = size, Foreground = color, VerticalAlignment = VerticalAlignment.Center };
        }
        [STAThread] public static void Main(string[] args)
        {
            Preview = args.Contains("--preview");
            bool settings = args.Contains("--settings");
            bool created;
            using (var mutex = new Mutex(true, "Local\\DshCountdown" + (Preview ? "PreviewV2" : settings ? "SettingsV2" : "Wpf2027"), out created))
            {
                if (!created) return;
                try
                {
                    try { Config = ReadConfig(); LastWrite = File.GetLastWriteTimeUtc(ConfigPath); } catch (Exception ex) { Log(ex); }
                    var app = new Application();
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    if (settings) { OpenSettings(); Editor.Closed += (s, e) => app.Shutdown(); }
                    else CreateWidget();
                    app.Run();
                }
                catch (Exception ex) { Log(ex); MessageBox.Show("倒计时无法启动：" + ex.Message, "倒计时"); }
            }
        }
        static void CreateWidget()
        {
            try
            {
                if (File.Exists(PosPath))
                {
                    var p = Json.Deserialize<Dictionary<string, double>>(File.ReadAllText(PosPath));
                    if (p.ContainsKey("offset")) Offset = p["offset"];
                }
            }
            catch (Exception ex) { Log(ex); }
            W = new Window { Title = Preview ? "倒计时 · 预览" : "任务栏倒计时", WindowStyle = WindowStyle.None,
                AllowsTransparency = true, Background = Brushes.Transparent, ShowInTaskbar = Preview,
                ShowActivated = Preview, Topmost = true, ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight, UseLayoutRounding = true,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI") };
            TextOptions.SetTextFormattingMode(W, TextFormattingMode.Display);
            W.Icon = Logo();
            Root = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(5, 0, 5, 0) };
            Host = new StackPanel { Orientation = Orientation.Horizontal };
            Root.Child = Host; W.Content = Root;
            W.SourceInitialized += (s, e) => {
                Hwnd = new WindowInteropHelper(W).Handle;
                if (!Preview) SetWindowLong(Hwnd, -20, GetWindowLong(Hwnd, -20) | 0x08000000 | 0x80);
            };
            W.MouseLeftButtonDown += (s, e) => {
                POINT p; if (!GetCursorPos(out p)) return;
                Dragging = true; Moved = false; StartX = p.X; StartLeft = W.Left; W.CaptureMouse();
            };
            W.MouseMove += (s, e) => {
                if (!Dragging) return;
                POINT p; GetCursorPos(out p); double dx = (p.X - StartX) / ScaleX();
                if (Math.Abs(dx) > 3) Moved = true;
                if (Moved) W.Left = StartLeft + dx;
            };
            W.MouseLeftButtonUp += (s, e) => {
                if (!Dragging) return;
                Dragging = false; W.ReleaseMouseCapture();
                if (Moved) {
                    RECT r; if (Taskbar(out r)) Offset = W.Left - r.Left / ScaleX();
                    Position();
                    try { File.WriteAllText(PosPath, Json.Serialize(new { offset = Offset })); } catch (Exception ex) { Log(ex); }
                } else ToggleSeconds();
            };
            W.LostMouseCapture += (s, e) => Dragging = false;
            W.PreviewMouseRightButtonUp += (s, e) => {
                e.Handled = true;
                var menu = W.ContextMenu;
                if (menu != null) {
                    POINT p; GetCursorPos(out p);
                    double sx = ScaleX();
                    double xRel = p.X / sx - W.Left;
                    menu.PlacementTarget = W;
                    menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Custom;
                    menu.CustomPopupPlacementCallback = (popup, target, offset) => new[] {
                        new System.Windows.Controls.Primitives.CustomPopupPlacement(new Point(xRel, -popup.Height - 8), System.Windows.Controls.Primitives.PopupPrimaryAxis.Horizontal),
                        new System.Windows.Controls.Primitives.CustomPopupPlacement(new Point(xRel, W.ActualHeight + 8), System.Windows.Controls.Primitives.PopupPrimaryAxis.Horizontal)
                    };
                    menu.IsOpen = true;
                }
            };
            W.Closed += (s, e) => { if (_mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; } Application.Current.Shutdown(); };
            try { _mouseProc = HookProc; _mouseHook = SetWindowsHookEx(14, _mouseProc, IntPtr.Zero, 0); } catch (Exception ex) { Log(ex); }
            Build(); W.Show(); Position();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += (s, e) => Tick(); timer.Start();
        }
        static ContextMenu Menu()
        {
            var menu = new ContextMenu { PlacementTarget = Root, Placement = System.Windows.Controls.Primitives.PlacementMode.Top, VerticalOffset = -8, MinWidth = 192 };
            Theme(menu, IsLight());
            menu.Opened += (s,e) => { MenuOpen = true; };
            menu.Closed += (s,e) => { MenuOpen = false; RotationClock.Restart(); };
            AddMenu(menu, "设置…", OpenSettings);
            AddMenu(menu, "退出倒计时", () => {
                if (!Preview) foreach (var p in System.Diagnostics.Process.GetProcessesByName("CountdownWatchdog")) {
                    try { if (string.Equals(Path.GetDirectoryName(p.MainModule.FileName), Dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) p.Kill(); } catch (Exception ex) { Log(ex); } finally { p.Dispose(); }
                }
                W.Close();
            });
            return menu;
        }
        static void AddMenu(ContextMenu m, string label, Action action) { var item = new MenuItem { Header = label }; item.Click += (s, e) => action(); m.Items.Add(item); }
        static void Persist() { try { SaveConfig(Config); } catch (Exception ex) { MessageBox.Show("无法保存设置：" + ex.Message); } }
        static void ToggleSeconds() { Config.show_seconds = !Config.show_seconds; Persist(); }
        static void Build()
        {
            Light = IsLight();
            ActiveSkinId = ResolveSkinId();
            var sk = PickSkin(ActiveSkinId, Light);
            _skinText = sk.Text; _skinMuted = sk.Muted; _skinAccent = sk.Accent;
            string a = Config.glass ? "E8" : "FF";
            var gb = new LinearGradientBrush();
            gb.StartPoint = new Point(0, 0); gb.EndPoint = new Point(0, 1);
            gb.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(HexA(a, sk.Bg1)), 0));
            gb.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(HexA(a, sk.Bg2)), 1));
            Root.Background = gb;
            Root.BorderBrush = Brush(Light ? "#26000000" : "#33FFFFFF");
            Host.Children.Clear(); Slots.Clear();
            var deco = Decor();
            if (deco != null) { deco.VerticalAlignment = VerticalAlignment.Center; deco.Margin = new Thickness(10, 0, 2, 0); Host.Children.Add(deco); }
            Items = Config.items.Where(i => i != null && i.enabled && ParseDate(i.date).HasValue).OrderBy(i => ParseDate(i.date).Value).ToList();
            if (Index >= Items.Count) Index = 0;
            W.ContextMenu = Menu();
            if (Items.Count == 0) {
                var empty = Text("添加一个值得期待的日子", 11, Muted); empty.Margin = new Thickness(10, 9, 10, 9); Host.Children.Add(empty);
            } else {
                var visible = Config.display_mode == "rotate" ? new List<CfgItem> { Items[Index] } : Items;
                for (int n = 0; n < visible.Count; n++) {
                    if (n > 0) Host.Children.Add(new Border { Width = 1, Height = 18, Background = Brush(Light ? "#16000000" : "#20FFFFFF"), VerticalAlignment = VerticalAlignment.Center });
                    Host.Children.Add(Card(visible[n]));
                }
                if (Config.display_mode == "rotate" && Items.Count > 1) {
                    var page = Text((Index + 1) + "/" + Items.Count, 9, Muted); page.Margin = new Thickness(2, 0, 9, 0); Host.Children.Add(page);
                }
            }
            Update(); W.UpdateLayout(); Position();
        }
        static UIElement Card(CfgItem item)
        {
            var col = new StackPanel { Margin = new Thickness(11, 3, 11, 3), MinWidth = Config.show_seconds ? 130 : 48 };
            col.ToolTip = item.name + "\n" + ParseDate(item.date).Value.ToString("yyyy-MM-dd HH:mm") + "\n单击切换时分秒 · 拖动调整位置 · 右键设置";
            var name = Text(string.IsNullOrWhiteSpace(item.name) ? "倒计时" : item.name, 10, Muted);
            name.MaxWidth = 120; name.TextTrimming = TextTrimming.CharacterEllipsis; col.Children.Add(name);
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var value = Text("", 17, Ink); value.FontWeight = FontWeights.SemiBold;
            var suffix = Text("", 10, Muted); suffix.Margin = new Thickness(4, 3, 0, 0);
            row.Children.Add(value); row.Children.Add(suffix); col.Children.Add(row);
            Slots.Add(Tuple.Create(item, value, suffix)); return col;
        }
        static void Update()
        {
            var now = DateTime.Now;
            foreach (var slot in Slots) {
                var target = ParseDate(slot.Item1.date).Value;
                string value = DayText(target, now, Config.show_seconds);
                slot.Item2.Text = value;
                bool numeric = value != "已到期" && value != "今天";
                slot.Item2.FontSize = numeric ? 17 : 13;
                slot.Item2.Foreground = target <= now ? Muted : ValueInk;
                slot.Item3.Text = !numeric ? "" : Config.show_seconds ? "天  " + (target - now).ToString(@"hh\:mm\:ss") : "天";
            }
        }
        static double ScaleX() { var src = PresentationSource.FromVisual(W); return src == null ? 1 : src.CompositionTarget.TransformToDevice.M11; }
        static double ScaleY() { var src = PresentationSource.FromVisual(W); return src == null ? 1 : src.CompositionTarget.TransformToDevice.M22; }
        static bool Taskbar(out RECT r) { return GetWindowRect(FindWindow("Shell_TrayWnd", null), out r); }
        static void Position()
        {
            if (W == null || Dragging) return;
            if (Preview) { W.Left = 160; W.Top = 160; return; }
            RECT r; if (!Taskbar(out r)) return;
            double sx = ScaleX(), sy = ScaleY();
            double width = (r.Right - r.Left) / sx;
            Offset = Math.Max(0, Math.Min(Offset, Math.Max(0, width - W.ActualWidth)));
            W.Left = r.Left / sx + Offset;
            W.Top = r.Top / sy + Math.Max(0, ((r.Bottom - r.Top) / sy - W.ActualHeight) / 2);
        }
        static void Tick()
        {
            try {
                bool menuOpen = MenuOpen || (W.ContextMenu != null && W.ContextMenu.IsOpen);
                if (menuOpen) {
                    var menu = W.ContextMenu;
                    if (menu != null && menu.IsOpen) {
                        bool down = (GetAsyncKeyState(0x01) & 0x8000) != 0 || (GetAsyncKeyState(0x02) & 0x8000) != 0;
                        if (down) {
                            bool outside = true;
                            var src = PresentationSource.FromVisual(menu) as HwndSource;
                            if (src != null && src.Handle != IntPtr.Zero) {
                                RECT mr; POINT cp;
                                if (GetWindowRect(src.Handle, out mr) && GetCursorPos(out cp))
                                    outside = cp.X < mr.Left || cp.X > mr.Right || cp.Y < mr.Top || cp.Y > mr.Bottom;
                            }
                            if (outside) menu.IsOpen = false;
                        }
                    }
                }
                if (!menuOpen) {
                    if (File.Exists(ConfigPath) && File.GetLastWriteTimeUtc(ConfigPath) != LastWrite) {
                        Config = ReadConfig(); LastWrite = File.GetLastWriteTimeUtc(ConfigPath); Build(); RotationClock.Restart();
                    }
                    if (IsLight() != Light) Build();
                    if (Config.display_mode == "rotate" && Items.Count > 1 && RotationClock.Elapsed.TotalSeconds >= Config.rotate_seconds) {
                        Index = (Index + 1) % Items.Count; RotationClock.Restart(); Build();
                    }
                    Position();
                }
                Update();
                if (Hwnd != IntPtr.Zero) {
                    W.Opacity = 1; W.IsHitTestVisible = true;
                    SetWindowPos(Hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x13);
                }
            } catch (Exception ex) {
                // Retry invalid/incomplete external edits only when their timestamp changes.
                LastWrite = File.GetLastWriteTimeUtc(ConfigPath); Log(ex);
            }
        }


    }
}

