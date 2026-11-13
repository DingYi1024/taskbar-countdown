using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
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
        public bool motion { get; set; }
        public List<CfgItem> items { get; set; }

        public Cfg()
        {
            display_mode = "expand";
            rotate_seconds = 4;
            glass = true;
            theme = "system";
            skin = "default";
            motion = true;
            items = new List<CfgItem>();
        }
    }

    public static partial class Program
    {
        sealed class SkinPalette
        {
            public string Bg1, Bg2, Text, Muted, Accent, Detail;
        }

        sealed class SkinSpec
        {
            public string Id, Label, Seal;
            public bool LightDefault;
            public SkinPalette LightPal, DarkPal;
            public string Oc1, Oc2, Glow1, Glow2;
        }

        sealed class TimedItem
        {
            public CfgItem Item;
            public DateTime Date;
        }

        sealed class CountdownSlot
        {
            public DateTime Target;
            public TextBlock Value, Suffix;
        }

        static readonly string Dir = AppDomain.CurrentDomain.BaseDirectory;
        static readonly string ConfigPath = Path.Combine(Dir, "config.json");
        static readonly string PosPath = Path.Combine(Dir, "pos.json");
        static readonly string PendingExePath = Path.Combine(Dir, "Countdown.exe.pending");
        static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        static readonly Stopwatch RotationClock = Stopwatch.StartNew();
        static readonly Stopwatch LowFreqClock = Stopwatch.StartNew();
        static readonly FontFamily WidgetNumFont = new FontFamily("Bahnschrift, Segoe UI Variable Display, Segoe UI");
        static readonly FontFamily WidgetMonoFont = new FontFamily("Consolas, Bahnschrift, Segoe UI");

        static readonly SkinSpec[] SkinCatalog = {
            new SkinSpec {
                Id = "default", Label = "默认 · 原生融肤", Seal = null, LightDefault = false,
                LightPal = new SkinPalette { Bg1 = "#FAFBFD", Bg2 = "#EEF2F7", Text = "#1D2733", Muted = "#637283", Accent = "#35689E", Detail = "#35689E" },
                DarkPal  = new SkinPalette { Bg1 = "#232933", Bg2 = "#191E26", Text = "#F0F4F8", Muted = "#9AA6B5", Accent = "#7CB2E8", Detail = "#7CB2E8" },
                Oc1 = "#CFD8E4", Oc2 = "#E4E8EF", Glow1 = "#FFFFFF", Glow2 = "#95A8C2"
            },
            new SkinSpec {
                Id = "minimal", Label = "极简 · 悬浮无框", Seal = null, LightDefault = false,
                LightPal = new SkinPalette { Bg1 = "#00FFFFFF", Bg2 = "#00FFFFFF", Text = "#1D2632", Muted = "#5A6574", Accent = "#2F669E", Detail = "#2F669E" },
                DarkPal  = new SkinPalette { Bg1 = "#00000000", Bg2 = "#00000000", Text = "#F4F7FA", Muted = "#A7B3C2", Accent = "#84B9EC", Detail = "#84B9EC" },
                Oc1 = "#CFD8E4", Oc2 = "#E4E8EF", Glow1 = "#FFFFFF", Glow2 = "#95A8C2"
            },
            new SkinSpec {
                Id = "cream", Label = "奶油 · 暖麦", Seal = "暖麦", LightDefault = true,
                LightPal = new SkinPalette { Bg1 = "#FCF8F2", Bg2 = "#F3EADF", Text = "#3A2B22", Muted = "#856F61", Accent = "#B56A36", Detail = "#B56A36" },
                DarkPal  = new SkinPalette { Bg1 = "#2E251F", Bg2 = "#211A15", Text = "#F7EFE5", Muted = "#C2AEA0", Accent = "#E7AD6B", Detail = "#E7AD6B" },
                Oc1 = "#EFE2D3", Oc2 = "#E3CBB2", Glow1 = "#FFF9F2", Glow2 = "#CFA17E"
            },
            new SkinSpec {
                Id = "celadon", Label = "晨雾 · 青瓷", Seal = "青瓷", LightDefault = true,
                LightPal = new SkinPalette { Bg1 = "#F2F7F4", Bg2 = "#E3EDE7", Text = "#1E352D", Muted = "#5B776D", Accent = "#35785F", Detail = "#35785F" },
                DarkPal  = new SkinPalette { Bg1 = "#1A2B25", Bg2 = "#12201B", Text = "#EBF3EF", Muted = "#97B3A8", Accent = "#7EC5A7", Detail = "#7EC5A7" },
                Oc1 = "#DCE7E0", Oc2 = "#C8D9CF", Glow1 = "#F8FCFA", Glow2 = "#8BB39E"
            },
            new SkinSpec {
                Id = "aurora", Label = "极光 · 磨砂", Seal = "极光", LightDefault = false,
                LightPal = new SkinPalette { Bg1 = "#EFF6FB", Bg2 = "#E2ECF7", Text = "#172A3E", Muted = "#58738E", Accent = "#2C799D", Detail = "#2C799D" },
                DarkPal  = new SkinPalette { Bg1 = "#162638", Bg2 = "#141D33", Text = "#ECF5FB", Muted = "#8FB1C9", Accent = "#6ED2C8", Detail = "#6ED2C8" },
                Oc1 = "#D4E3EE", Oc2 = "#E2E6F2", Glow1 = "#FFFFFF", Glow2 = "#7DBAC7"
            },
            new SkinSpec {
                Id = "christmas", Label = "圣诞 · 松霜", Seal = "冬序", LightDefault = false,
                LightPal = new SkinPalette { Bg1 = "#F3F7F4", Bg2 = "#E5EFE8", Text = "#1A372C", Muted = "#577568", Accent = "#286B51", Detail = "#A87424" },
                DarkPal  = new SkinPalette { Bg1 = "#1C382F", Bg2 = "#122620", Text = "#F5EEDF", Muted = "#A9C0B2", Accent = "#E8CF92", Detail = "#E8CF92" },
                Oc1 = "#D6E2DC", Oc2 = "#EAE5DA", Glow1 = "#FFFFFF", Glow2 = "#92B8A3"
            },
            new SkinSpec {
                Id = "newyear", Label = "元旦 · 星序", Seal = "新岁", LightDefault = false,
                LightPal = new SkinPalette { Bg1 = "#F2F6FC", Bg2 = "#E3EBF7", Text = "#1A2B45", Muted = "#5B7090", Accent = "#2E5E9C", Detail = "#2E5E9C" },
                DarkPal  = new SkinPalette { Bg1 = "#203048", Bg2 = "#152135", Text = "#F2EEE5", Muted = "#AAB8CD", Accent = "#E6CD92", Detail = "#E6CD92" },
                Oc1 = "#D5DFEC", Oc2 = "#EAE4D8", Glow1 = "#FFFFFF", Glow2 = "#9FB4D1"
            },
            new SkinSpec {
                Id = "spring", Label = "春节 · 灯笺", Seal = "新春", LightDefault = true,
                LightPal = new SkinPalette { Bg1 = "#FBF3E8", Bg2 = "#F4E3CE", Text = "#4F2D23", Muted = "#8A6557", Accent = "#B53F31", Detail = "#B53F31" },
                DarkPal  = new SkinPalette { Bg1 = "#3D1D1C", Bg2 = "#2A1212", Text = "#FAF0E2", Muted = "#D4AFA3", Accent = "#F0C577", Detail = "#F0C577" },
                Oc1 = "#F1E3D1", Oc2 = "#E4C9AD", Glow1 = "#FFF9F2", Glow2 = "#D1866E"
            },
            new SkinSpec {
                Id = "midautumn", Label = "中秋 · 月泊", Seal = "秋夕", LightDefault = false,
                LightPal = new SkinPalette { Bg1 = "#F5F6FA", Bg2 = "#E7EAF3", Text = "#212840", Muted = "#656D89", Accent = "#A87224", Detail = "#A87224" },
                DarkPal  = new SkinPalette { Bg1 = "#242B42", Bg2 = "#191F33", Text = "#F1E9D7", Muted = "#ADB5C9", Accent = "#E5CE93", Detail = "#E5CE93" },
                Oc1 = "#D9DFEB", Oc2 = "#EAE3D7", Glow1 = "#FFFFFF", Glow2 = "#A3B0CC"
            },
            new SkinSpec {
                Id = "national", Label = "国庆 · 朱锦", Seal = "华诞", LightDefault = false,
                LightPal = new SkinPalette { Bg1 = "#FCF3F1", Bg2 = "#F4E2DF", Text = "#4D1E1C", Muted = "#8B5B57", Accent = "#B33329", Detail = "#B33329" },
                DarkPal  = new SkinPalette { Bg1 = "#612422", Bg2 = "#471918", Text = "#FFF1DC", Muted = "#DFB7AC", Accent = "#F1D28E", Detail = "#F1D28E" },
                Oc1 = "#EFE2D7", Oc2 = "#E3C7BA", Glow1 = "#FFF8F2", Glow2 = "#C87A6D"
            }
        };

        static Cfg Config = new Cfg();
        static Window W, Editor, ShareWin;
        static Border Root, DecorSlot;
        static StackPanel Host, CardsHost;
        static IntPtr Hwnd;
        static DispatcherTimer MainTimer;
        static DateTime LastWrite;
        static List<TimedItem> Items = new List<TimedItem>();
        static TimedItem _rightClickedEntry;
        static readonly List<CountdownSlot> Slots = new List<CountdownSlot>();
        static bool Light, _systemLight = true, Dragging, Moved, Preview, MenuOpen, SessionLocked;
        static double Offset = 236, StartLeft;
        static int StartX, Index;
        static FrameworkElement Star;
        static AnimationClock StarClock;
        static string _renderedSkinId;
        static bool _renderedLight, _renderedGlass;
        static string ActiveSkinId = "default";
        static Brush Ink = Brushes.Black, Muted = Brushes.Gray, ValueInk = Brushes.Black, DetailBrush = Brushes.Gold;

        static void StopStar()
        {
            if (StarClock != null) StarClock.Controller.Remove();
            if (Star != null) Star.BeginAnimation(UIElement.OpacityProperty, null);
            StarClock = null; Star = null;
        }

        static void SyncStar()
        {
            if (Star == null || W == null) return;
            bool pause = !Config.motion || SessionLocked || !W.IsVisible || W.Opacity <= 0 || W.WindowState == WindowState.Minimized;
            if (pause)
            {
                if (StarClock != null) { StarClock.Controller.Remove(); Star.BeginAnimation(UIElement.OpacityProperty, null); StarClock = null; }
            }
            else if (StarClock == null)
            {
                var breathing = new DoubleAnimation(ActiveSkinId == "midautumn" ? 0.75 : 0.55, 1.0, TimeSpan.FromSeconds(ActiveSkinId == "midautumn" ? 4 : 3))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                Timeline.SetDesiredFrameRate(breathing, 15);
                StarClock = breathing.CreateClock();
                Star.ApplyAnimationClock(UIElement.OpacityProperty, StarClock);
            }
        }

        static void SyncTimerInterval()
        {
            if (MainTimer == null) return;
            var target = MenuOpen
                ? TimeSpan.FromMilliseconds(100)
                : TimeSpan.FromMilliseconds(Config.show_seconds ? 500 : 2000);
            if (MainTimer.Interval != target) MainTimer.Interval = target;
        }

        static void SessionChanged(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
        {
            if (W == null) return;
            W.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock || e.Reason == Microsoft.Win32.SessionSwitchReason.RemoteDisconnect || e.Reason == Microsoft.Win32.SessionSwitchReason.ConsoleDisconnect) SessionLocked = true;
                if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionUnlock || e.Reason == Microsoft.Win32.SessionSwitchReason.RemoteConnect || e.Reason == Microsoft.Win32.SessionSwitchReason.ConsoleConnect) SessionLocked = false;
                SyncStar();
            }));
        }

        static void PreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            _systemLight = ReadSystemLight();
        }

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)]
        struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        const int WM_WINDOWPOSCHANGING = 0x0046;
        const int WM_SHOWWINDOW = 0x0018;
        const int WM_SYSCOMMAND = 0x0112;
        const int SC_MINIMIZE = 0xF020;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOZORDER = 0x0004;
        const uint SWP_NOACTIVATE = 0x0010;
        const uint SWP_SHOWWINDOW = 0x0040;
        const uint SWP_HIDEWINDOW = 0x0080;
        const uint SWP_NOCOPYBITS = 0x0100;
        const uint SWP_NOOWNERZORDER = 0x0200;

        const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        const uint WINEVENT_OUTOFCONTEXT = 0x0000;

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        static readonly uint MsgTaskbarCreated = RegisterWindowMessage("TaskbarCreated");

        delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
        [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
        [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)] static extern uint RegisterWindowMessage(string lpString);
        [DllImport("user32.dll")] static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] static extern int GetWindowLong32(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] static extern int SetWindowLong32(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);

        static IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hwnd, index, value) : new IntPtr(SetWindowLong32(hwnd, index, value.ToInt32()));
        }

        static void AddWindowExStyle(IntPtr hwnd, int styleBits)
        {
            if (IntPtr.Size == 8)
                SetWindowLongPtr64(hwnd, -20, new IntPtr(GetWindowLongPtr64(hwnd, -20).ToInt64() | (uint)styleBits));
            else
                SetWindowLong32(hwnd, -20, GetWindowLong32(hwnd, -20) | styleBits);
        }

        static WinEventDelegate _foregroundDelegate;
        static IntPtr _foregroundHook;

        static void OnForegroundChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (Hwnd != IntPtr.Zero && !Preview && !Dragging)
            {
                SetWindowPos(Hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOCOPYBITS | SWP_NOOWNERZORDER);
            }
        }

        static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (!Preview)
            {
                if (msg == WM_SHOWWINDOW && wParam == IntPtr.Zero)
                {
                    handled = true;
                    return IntPtr.Zero;
                }
                if (msg == WM_SYSCOMMAND && ((int)wParam & 0xFFF0) == SC_MINIMIZE)
                {
                    handled = true;
                    return IntPtr.Zero;
                }
            }

            if (msg == WM_WINDOWPOSCHANGING)
            {
                try
                {
                    var wp = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));
                    bool modified = false;
                    if (!Preview && (wp.flags & SWP_HIDEWINDOW) != 0)
                    {
                        wp.flags &= ~SWP_HIDEWINDOW;
                        wp.flags |= SWP_SHOWWINDOW;
                        modified = true;
                    }
                    if ((wp.flags & SWP_NOCOPYBITS) == 0)
                    {
                        wp.flags |= SWP_NOCOPYBITS;
                        modified = true;
                    }
                    if (modified)
                    {
                        Marshal.StructureToPtr(wp, lParam, false);
                    }
                }
                catch { }
            }
            else if (msg == MsgTaskbarCreated && !Preview)
            {
                try
                {
                    Position();
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOCOPYBITS | SWP_NOOWNERZORDER);
                }
                catch { }
            }
            return IntPtr.Zero;
        }

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

        static void SaveConfig(Cfg c, bool resetIndex = true, bool fullRebuild = true)
        {
            string temp = ConfigPath + ".tmp";
            File.WriteAllText(temp, Json.Serialize(c), new System.Text.UTF8Encoding(false));
            if (File.Exists(ConfigPath)) File.Replace(temp, ConfigPath, null); else File.Move(temp, ConfigPath);
            Config = c;
            LastWrite = File.GetLastWriteTimeUtc(ConfigPath);
            if (resetIndex) { Index = 0; RotationClock.Restart(); }
            if (W != null) { if (fullRebuild) Build(true); else BuildCards(); SyncTimerInterval(); }
        }

        static bool ReadSystemLight()
        {
            return Convert.ToInt32(Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 1)) != 0;
        }

        internal static bool IsSystemLight() { return _systemLight; }

        static bool IsLight()
        {
            if (Config.theme == "light") return true;
            if (Config.theme == "dark") return false;
            return _systemLight;
        }

        static SolidColorBrush Brush(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        static SkinSpec FindSkin(string id)
        {
            if (string.IsNullOrEmpty(id)) id = "default";
            if (id == "auto") id = DetectFestivalSkin();
            for (int i = 0; i < SkinCatalog.Length; i++)
                if (SkinCatalog[i].Id == id) return SkinCatalog[i];
            return SkinCatalog[0];
        }

        static SkinPalette PickSkinPalette(string id, bool light)
        {
            var spec = FindSkin(id);
            return light ? spec.LightPal : spec.DarkPal;
        }

        static string DetectFestivalSkin()
        {
            var now = DateTime.Now;
            foreach (var it in Items)
            {
                if (it.Date <= now) continue;
                string id = FestivalForName(it.Item.name);
                if (id != "default") return id;
            }
            return "default";
        }

        public static string FestivalForName(string name)
        {
            var n = name ?? "";
            if (n.Contains("圣诞")) return "christmas";
            if (n.Contains("元旦") || n.Contains("新年")) return "newyear";
            if (n.Contains("春节") || n.Contains("过年")) return "spring";
            if (n.Contains("中秋")) return "midautumn";
            if (n.Contains("国庆")) return "national";
            return "default";
        }

        static string HexA(string alpha, string rgb) { return "#" + alpha + rgb.TrimStart('#'); }
        static Color C_(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
        static string ResolveSkinId() { return FindSkin(Config.skin).Id; }
        static string DetailColorFor(string skinId, bool light) { return PickSkinPalette(skinId, light).Detail; }
        static string DetailColorFor(string skinId) { return DetailColorFor(skinId, FindSkin(skinId).LightDefault); }
        static string DetailColor() { return DetailColorFor(ActiveSkinId, Light); }

        static void AddPoly(Canvas c, string pts, Color fill)
        {
            var coll = new PointCollection();
            foreach (var pair in pts.Split(' '))
            {
                var xy = pair.Split(',');
                coll.Add(new Point(double.Parse(xy[0], CultureInfo.InvariantCulture), double.Parse(xy[1], CultureInfo.InvariantCulture)));
            }
            var brush = new SolidColorBrush(fill);
            brush.Freeze();
            c.Children.Add(new System.Windows.Shapes.Polygon { Points = coll, Fill = brush });
        }

        static System.Windows.Shapes.Path StrokePath(Canvas canvas, string data, string color, double width)
        {
            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(data),
                Stroke = Brush(color),
                StrokeThickness = width,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            canvas.Children.Add(path);
            return path;
        }

        static FrameworkElement CreateDecor(string skinId, bool light, out FrameworkElement star)
        {
            star = null;
            if (skinId == "minimal" || skinId == "default") return null;
            var canvas = new Canvas { Width = 26, Height = 28 };
            string detail = DetailColorFor(skinId, light);
            switch (skinId)
            {
                case "newyear":
                case "aurora":
                    string subRay = skinId == "aurora" ? (light ? "#68A6B8" : "#5AA0C2") : (light ? "#7C9AC2" : "#8BA4C3");
                    StrokePath(canvas, "M13,4 L13,24 M3,14 L23,14 M6.5,7.5 L19.5,20.5 M19.5,7.5 L6.5,20.5", subRay, 1.0);
                    AddPoly(canvas, "13,5 14.9,11.6 21.5,14 14.9,16.4 13,23 11.1,16.4 4.5,14 11.1,11.6", C_(detail));
                    star = (FrameworkElement)canvas.Children[canvas.Children.Count - 1];
                    break;
                case "spring":
                    StrokePath(canvas, "M13,1 L13,4 M8.5,5 L17.5,5 M8.5,23 L17.5,23 M13,24 L13,27 M10,25.5 L10,27 M16,25.5 L16,27", detail, 1.2);
                    var lantern = StrokePath(canvas, "M13,5.5 C2,5.5 2,22.5 13,22.5 C24,22.5 24,5.5 13,5.5 Z", detail, 1.3);
                    lantern.Fill = Brush(light ? "#28B53F31" : "#2CF0C577");
                    StrokePath(canvas, "M10,6.5 C6.5,11 6.5,17 10,21.5 M16,6.5 C19.5,11 19.5,17 16,21.5", detail, 0.85);
                    star = StrokePath(canvas, "M13,10.5 L13,17.5 M10,14 L16,14", light ? "#D88928" : "#FFE099", 1.5);
                    break;
                case "midautumn":
                    var moon = new System.Windows.Shapes.Ellipse { Width = 17, Height = 17, Fill = Brush(detail) };
                    Canvas.SetLeft(moon, 4.5); Canvas.SetTop(moon, 3); canvas.Children.Add(moon); star = moon;
                    StrokePath(canvas, "M7.5,6.5 C5,11 6.8,16 10.5,17", light ? "#FFF6DF" : "#F6E6B9", 0.9);
                    StrokePath(canvas, "M1.5,22 L8.5,22 C10.5,18.5 15.5,18.5 17.5,22 L24.5,22 M5,25.5 L21,25.5", light ? "#7B8AA8" : "#A4B5C6", 1.05);
                    break;
                case "national":
                    var flag = StrokePath(canvas, "M3,25.5 L3,4 M4,5 C10.5,1.5 17,8.5 23.5,5 L23.5,19 C17,22.5 10.5,15.5 4,19", detail, 1.2);
                    flag.Fill = Brush(light ? "#22B33329" : "#22F1D28E");
                    AddPoly(canvas, "10.5,7 11.6,9.8 14.5,10 12.2,11.9 12.9,14.8 10.5,13.2 8.1,14.8 8.8,11.9 6.5,10 9.4,9.8", C_(detail));
                    star = (FrameworkElement)canvas.Children[canvas.Children.Count - 1];
                    break;
                case "cream":
                    var sun = new System.Windows.Shapes.Ellipse { Width = 11, Height = 11, Fill = Brush(detail) };
                    Canvas.SetLeft(sun, 7.5); Canvas.SetTop(sun, 5.5); canvas.Children.Add(sun); star = sun;
                    StrokePath(canvas, "M3,20 L23,20 M6,23.5 L20,23.5", light ? "#C49E82" : "#9E826E", 1.15);
                    break;
                case "celadon":
                    var leaf = StrokePath(canvas, "M5,22 C5,11 12,5 21,5 C21,14 15,22 5,22 Z", detail, 1.3);
                    leaf.Fill = Brush(light ? "#2235785F" : "#227EC5A7");
                    StrokePath(canvas, "M5,22 C10,16.5 14.5,12 20,6", detail, 1.0);
                    star = leaf;
                    break;
                case "christmas":
                    string pine = light ? "#286B51" : "#8CC4A3";
                    var tree = new System.Windows.Shapes.Path
                    {
                        Data = Geometry.Parse("M13,6 L8.5,12 L11,12 L6,18 L9.5,18 L4.5,23.5 L21.5,23.5 L16.5,18 L20,18 L15,12 L17.5,12 Z"),
                        Fill = Brush(light ? "#2C286B51" : "#2C9CD4B5"),
                        Stroke = Brush(pine),
                        StrokeThickness = 1.2,
                        StrokeLineJoin = PenLineJoin.Round,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    };
                    canvas.Children.Add(tree);
                    StrokePath(canvas, "M13,23.5 L13,26.5", pine, 1.4);
                    AddPoly(canvas, "13,0.5 13.9,2.8 16.2,3.6 13.9,4.4 13,6.7 12.1,4.4 9.8,3.6 12.1,2.8", C_(detail));
                    star = (FrameworkElement)canvas.Children[canvas.Children.Count - 1];
                    var berry = new System.Windows.Shapes.Ellipse { Width = 3.5, Height = 3.5, Fill = Brush(light ? "#C44D3D" : "#D67B6E") };
                    Canvas.SetLeft(berry, 9.5); Canvas.SetTop(berry, 16.5); canvas.Children.Add(berry);
                    break;
                default:
                    return null;
            }
            return canvas;
        }

        static FrameworkElement CreateDecor(string skinId, out FrameworkElement star)
        {
            return CreateDecor(skinId, FindSkin(skinId).LightDefault, out star);
        }

        static TextBlock Text(string value, double size, Brush color)
        {
            return new TextBlock { Text = value, FontSize = size, Foreground = color, VerticalAlignment = VerticalAlignment.Center };
        }

        static List<ShareEntry> BuildShareEntries<T>(IEnumerable<T> source, Func<T, string> nameOf, Func<T, string> dateOf, Func<T, bool> enabledOf, Func<T, bool> isPreferred, out int initialIndex)
        {
            var sorted = source
                .Select(s => new { Src = s, Date = ParseDate(dateOf(s)) })
                .Where(x => x.Date.HasValue)
                .OrderBy(x => x.Date.Value)
                .ToList();
            var entries = new List<ShareEntry>(sorted.Count);
            initialIndex = -1;
            for (int i = 0; i < sorted.Count; i++)
            {
                var x = sorted[i];
                if (isPreferred != null && isPreferred(x.Src)) initialIndex = i;
                string rawName = nameOf(x.Src);
                entries.Add(new ShareEntry
                {
                    Name = string.IsNullOrWhiteSpace(rawName) ? "倒计时" : rawName.Trim(),
                    Date = x.Date.Value,
                    Enabled = enabledOf(x.Src)
                });
            }
            if (initialIndex < 0 && entries.Count > 0)
            {
                var now = DateTime.Now;
                int best = -1;
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i].Enabled && entries[i].Date > now && (best < 0 || entries[i].Date < entries[best].Date)) best = i;
                initialIndex = best >= 0 ? best : 0;
            }
            return entries;
        }

        static DockPanel CreateDialogChrome(string title, double width, double height, bool light, Window owner, bool showMinimize, out Window win, string headerLabel)
        {
            win = new Window
            {
                Title = title,
                Width = width,
                Height = Math.Min(height, SystemParameters.WorkArea.Height - 36),
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
                Owner = owner,
                Topmost = true,
                ShowInTaskbar = owner == null,
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 12,
                UseLayoutRounding = true,
                Icon = Logo()
            };
            Theme(win, light);

            var frame = new Border { CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1) };
            frame.SetResourceReference(Border.BackgroundProperty, "Canvas");
            frame.SetResourceReference(Border.BorderBrushProperty, "Line");
            win.Content = frame;

            var outer = new DockPanel();
            frame.Child = outer;

            var targetWin = win;
            var chrome = new DockPanel { Height = 44, Margin = new Thickness(18, 0, 12, 0), Background = Brushes.Transparent };
            DockPanel.SetDock(chrome, Dock.Top);
            outer.Children.Add(chrome);
            chrome.MouseLeftButtonDown += (s, e) => { if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed) targetWin.DragMove(); };

            var close = ActionButton("✕", () => targetWin.Close());
            close.Padding = new Thickness(13, 6, 13, 6);
            close.Background = Brushes.Transparent;
            close.BorderThickness = new Thickness(0);
            close.ToolTip = "关闭";
            DockPanel.SetDock(close, Dock.Right);
            chrome.Children.Add(close);

            if (showMinimize)
            {
                var min = ActionButton("—", () => targetWin.WindowState = WindowState.Minimized);
                min.Padding = close.Padding;
                min.Background = Brushes.Transparent;
                min.BorderThickness = new Thickness(0);
                min.ToolTip = "最小化";
                DockPanel.SetDock(min, Dock.Right);
                chrome.Children.Add(min);
            }

            var brand = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Transparent };
            brand.Children.Add(new Image { Source = Logo(), Width = 19, Height = 19, Margin = new Thickness(0, 0, 8, 0) });
            brand.Children.Add(Label(headerLabel ?? title, 12, "Muted"));
            chrome.Children.Add(brand);

            return outer;
        }

        [STAThread]
        public static void Main(string[] args)
        {
            Preview = args.Contains("--preview");
            bool settings = args.Contains("--settings");
            bool created;
            using (var mutex = new Mutex(true, "Local\\DshCountdown" + (Preview ? "PreviewV2" : settings ? "SettingsV2" : "Wpf2027"), out created))
            {
                if (!created) return;
                try
                {
                    _systemLight = ReadSystemLight();
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
                    if (p != null && p.ContainsKey("offset")) Offset = p["offset"];
                }
            }
            catch (Exception ex) { Log(ex); }

            W = new Window
            {
                Title = Preview ? "倒计时 · 预览" : "任务栏倒计时",
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = Preview,
                ShowActivated = Preview,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight,
                UseLayoutRounding = true,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
                Icon = Logo()
            };
            TextOptions.SetTextFormattingMode(W, TextFormattingMode.Display);

            if (!Preview)
            {
                _foregroundDelegate = OnForegroundChanged;
                _foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _foregroundDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
            }

            Root = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(5, 0, 5, 0), MinHeight = 38 };
            Host = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DecorSlot = new Border { VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
            CardsHost = new StackPanel { Orientation = Orientation.Horizontal };
            Host.Children.Add(DecorSlot);
            Host.Children.Add(CardsHost);
            Root.Child = Host;
            W.Content = Root;

            W.SourceInitialized += (s, e) =>
            {
                Hwnd = new WindowInteropHelper(W).Handle;
                if (!Preview) AddWindowExStyle(Hwnd, 0x08000000 | 0x80);
                var source = HwndSource.FromHwnd(Hwnd);
                if (source != null) source.AddHook(WndProc);
            };
            W.MouseLeftButtonDown += (s, e) =>
            {
                POINT p; if (!GetCursorPos(out p)) return;
                Dragging = true; Moved = false; StartX = p.X; StartLeft = W.Left; W.CaptureMouse();
            };
            W.MouseMove += (s, e) =>
            {
                if (!Dragging) return;
                POINT p; GetCursorPos(out p); double dx = (p.X - StartX) / ScaleX();
                if (Math.Abs(dx) > 3) Moved = true;
                if (Moved) W.Left = StartLeft + dx;
            };
            W.MouseLeftButtonUp += (s, e) =>
            {
                if (!Dragging) return;
                Dragging = false; W.ReleaseMouseCapture();
                if (Moved)
                {
                    RECT r; if (Taskbar(out r)) Offset = W.Left - r.Left / ScaleX();
                    Position();
                    try { File.WriteAllText(PosPath, Json.Serialize(new { offset = Offset })); } catch (Exception ex) { Log(ex); }
                }
                else
                {
                    Config.show_seconds = !Config.show_seconds;
                    Persist(false, false);
                }
            };
            W.LostMouseCapture += (s, e) => Dragging = false;
            W.MouseEnter += (s, e) => { if (ActiveSkinId == "minimal" && Root != null) Root.Background = Brush(Light ? "#1A000000" : "#1CFFFFFF"); };
            W.MouseLeave += (s, e) => { if (ActiveSkinId == "minimal" && Root != null) Root.Background = Brush(Light ? "#01FFFFFF" : "#01000000"); };
            W.PreviewMouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                _rightClickedEntry = null;
                var cur = e.OriginalSource as DependencyObject;
                while (cur != null)
                {
                    var fe = cur as FrameworkElement;
                    if (fe != null && fe.Tag is TimedItem) { _rightClickedEntry = (TimedItem)fe.Tag; break; }
                    cur = (cur is Visual || cur is System.Windows.Media.Media3D.Visual3D) ? VisualTreeHelper.GetParent(cur) : LogicalTreeHelper.GetParent(cur);
                }
                var menu = W.ContextMenu;
                if (menu != null)
                {
                    POINT p; GetCursorPos(out p);
                    double xRel = p.X / ScaleX() - W.Left - 12;
                    menu.PlacementTarget = W;
                    menu.Placement = PlacementMode.Custom;
                    menu.CustomPopupPlacementCallback = (popup, target, offset) => new[] {
                        new CustomPopupPlacement(new Point(xRel, -popup.Height + 6), PopupPrimaryAxis.Horizontal),
                        new CustomPopupPlacement(new Point(xRel, W.ActualHeight - 4), PopupPrimaryAxis.Horizontal)
                    };
                    menu.IsOpen = true;
                }
            };

            Microsoft.Win32.SystemEvents.SessionSwitch += SessionChanged;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += PreferenceChanged;
            W.IsVisibleChanged += (s, e) => SyncStar();
            W.StateChanged += (s, e) => SyncStar();
            W.Closed += (s, e) =>
            {
                if (_foregroundHook != IntPtr.Zero)
                {
                    UnhookWinEvent(_foregroundHook);
                    _foregroundHook = IntPtr.Zero;
                }
                Microsoft.Win32.SystemEvents.SessionSwitch -= SessionChanged;
                Microsoft.Win32.SystemEvents.UserPreferenceChanged -= PreferenceChanged;
                StopStar();
                if (MainTimer != null) MainTimer.Stop();
                Application.Current.Shutdown();
            };
            W.ContextMenu = Menu();
            Build(true); W.Show(); Position();
            MainTimer = new DispatcherTimer();
            MainTimer.Tick += (s, e) => Tick();
            SyncTimerInterval();
            MainTimer.Start();
        }

        static void OpenShareFromWidget()
        {
            int initial;
            var entries = BuildShareEntries(
                Config.items.Where(i => i != null),
                i => i.name, i => i.date, i => i.enabled,
                i => _rightClickedEntry != null && ReferenceEquals(i, _rightClickedEntry.Item),
                out initial);
            OpenShareCard(entries, initial, Config.skin, IsLight());
        }

        static ContextMenu Menu()
        {
            var menu = new ContextMenu { PlacementTarget = Root, Placement = PlacementMode.Top, VerticalOffset = -8, MinWidth = 156 };
            Theme(menu, IsLight());
            AddMenu(menu, "生成社交卡片…", "M2,3.5 C2,2.7 2.7,2 3.5,2 L10.5,2 C11.3,2 12,2.7 12,3.5 L12,10.5 C12,11.3 11.3,12 10.5,12 L3.5,12 C2.7,12 2,11.3 2,10.5 Z M3.6,9.4 L5.8,7.0 L7.6,8.7 L9.2,7.2 L10.6,9.4", "Accent", OpenShareFromWidget);
            AddMenu(menu, "设置…", "M2,4.5 L12,4.5 M2,9.5 L12,9.5 M5,2.8 L5,6.2 M9,7.8 L9,11.2", "Muted", OpenSettings);
            menu.Items.Add(new Separator());
            AddMenu(menu, "退出倒计时", "M4.2,3.8 C2.9,4.7 2.1,6.2 2.1,7.9 C2.1,10.6 4.3,12.8 7,12.8 C9.7,12.8 11.9,10.6 11.9,7.9 C11.9,6.2 11.1,4.7 9.8,3.8 M7,1.8 L7,6.8", "Danger", () =>
            {
                if (!Preview) foreach (var p in Process.GetProcessesByName("CountdownWatchdog"))
                {
                    try { if (string.Equals(Path.GetDirectoryName(p.MainModule.FileName), Dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) p.Kill(); }
                    catch (Exception ex) { Log(ex); }
                    finally { p.Dispose(); }
                }
                W.Close();
            });
            menu.Opened += (s, e) => { MenuOpen = true; SyncTimerInterval(); };
            menu.Closed += (s, e) => { MenuOpen = false; SyncTimerInterval(); RotationClock.Restart(); };
            return menu;
        }

        static void AddMenu(ContextMenu m, string label, Action action)
        {
            var item = new MenuItem { Header = label };
            item.Click += (s, e) => action();
            m.Items.Add(item);
        }

        static void AddMenu(ContextMenu m, string label, string iconData, string iconBrushKey, Action action)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var icon = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(iconData),
                Width = 14, Height = 14,
                StrokeThickness = 1.4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Margin = new Thickness(0, 0, 9, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            icon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, iconBrushKey);
            var txt = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
            if (iconBrushKey == "Danger") txt.SetResourceReference(TextBlock.ForegroundProperty, "Danger");
            sp.Children.Add(icon);
            sp.Children.Add(txt);
            var item = new MenuItem { Header = sp };
            item.Click += (s, e) => action();
            m.Items.Add(item);
        }

        static void Persist(bool resetIndex, bool fullRebuild)
        {
            try { SaveConfig(Config, resetIndex, fullRebuild); }
            catch (Exception ex) { MessageBox.Show("无法保存设置：" + ex.Message); }
        }

        static void Build(bool forceTheme = false)
        {
            Items = Config.items
                .Where(i => i != null && i.enabled)
                .Select(i => new { Item = i, Date = ParseDate(i.date) })
                .Where(x => x.Date.HasValue)
                .OrderBy(x => x.Date.Value)
                .Select(x => new TimedItem { Item = x.Item, Date = x.Date.Value })
                .ToList();
            ApplySkin(forceTheme);
            BuildCards();
            SyncStar();
        }

        static void ApplySkin(bool force)
        {
            Light = IsLight();
            ActiveSkinId = ResolveSkinId();
            if (!force && _renderedSkinId == ActiveSkinId && _renderedLight == Light && _renderedGlass == Config.glass) return;
            var sk = PickSkinPalette(ActiveSkinId, Light);
            Ink = Brush(sk.Text);
            Muted = Brush(sk.Muted);
            ValueInk = Brush(sk.Accent);
            DetailBrush = Brush(sk.Detail);
            bool isMinimal = ActiveSkinId == "minimal";
            bool themed = ActiveSkinId != "default" && !isMinimal;

            if (isMinimal)
            {
                // Alpha = 1/255 prevents WS_EX_LAYERED click-through to Windows taskbar while staying visually frameless.
                Root.Background = Brush(Light ? "#01FFFFFF" : "#01000000");
                Root.BorderBrush = Brushes.Transparent;
                Root.BorderThickness = new Thickness(0);
                Root.CornerRadius = new CornerRadius(8);
                Root.Padding = new Thickness(10, 0, 10, 0);
                CardsHost.Effect = new DropShadowEffect
                {
                    Color = Light ? Colors.White : Colors.Black,
                    BlurRadius = 4, ShadowDepth = 1, Direction = 270,
                    Opacity = Light ? 0.65 : 0.60
                };
            }
            else
            {
                CardsHost.Effect = null;
                string a = Config.glass ? (themed ? "CA" : "BC") : "FF";
                var gb = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                gb.GradientStops.Add(new GradientStop(C_(HexA(a, sk.Bg1)), 0));
                gb.GradientStops.Add(new GradientStop(C_(HexA(a, sk.Bg2)), 1));
                gb.Freeze();
                Root.Background = gb;

                Color rimTop = Light
                    ? Color.FromArgb(Config.glass ? (byte)220 : (byte)160, 255, 255, 255)
                    : Color.FromArgb(Config.glass ? (byte)85 : (byte)64, 255, 255, 255);
                Color rimBot = Light ? Color.FromArgb(36, 0, 0, 0) : Color.FromArgb(24, 255, 255, 255);
                if (themed && !Light) rimTop = C_(HexA("58", sk.Detail));
                var borderGrad = new LinearGradientBrush(rimTop, rimBot, new Point(0, 0), new Point(0.5, 1));
                borderGrad.Freeze();
                Root.BorderBrush = borderGrad;
                Root.BorderThickness = new Thickness(1);
                Root.CornerRadius = new CornerRadius(10);
                Root.Padding = new Thickness(6, 0, 6, 0);
            }

            if (W.ContextMenu != null && (force || _renderedLight != Light)) SetPalette(W.ContextMenu, Light);
            if (force || _renderedSkinId != ActiveSkinId || _renderedLight != Light)
            {
                StopStar();
                var deco = CreateDecor(ActiveSkinId, Light, out Star);
                DecorSlot.Child = deco;
                if (deco != null)
                {
                    DecorSlot.Margin = new Thickness(6, 0, 6, 0);
                    DecorSlot.Visibility = Visibility.Visible;
                }
                else
                {
                    DecorSlot.Margin = new Thickness(0);
                    DecorSlot.Visibility = Visibility.Collapsed;
                }
            }
            _renderedSkinId = ActiveSkinId;
            _renderedLight = Light;
            _renderedGlass = Config.glass;
        }

        static void BuildCards()
        {
            CardsHost.Children.Clear();
            Slots.Clear();
            if (Index >= Items.Count) Index = 0;
            if (Items.Count == 0)
            {
                var empty = Text("添加一个值得期待的日子", 11, Muted);
                empty.Margin = new Thickness(10, 9, 10, 9);
                CardsHost.Children.Add(empty);
            }
            else
            {
                bool themed = ActiveSkinId != "default" && ActiveSkinId != "minimal";
                var sepBrush = Brush(themed ? HexA("2E", DetailColor()) : Light ? "#1E000000" : "#22FFFFFF");
                var now = DateTime.Now;
                int nearestIdx = -1;
                for (int i = 0; i < Items.Count; i++)
                    if (Items[i].Date > now) { nearestIdx = i; break; }
                int start = Config.display_mode == "rotate" ? Index : 0;
                int count = Config.display_mode == "rotate" ? 1 : Items.Count;
                for (int n = 0; n < count; n++)
                {
                    if (n > 0) CardsHost.Children.Add(new Border { Width = 1, Height = 16, Background = sepBrush, VerticalAlignment = VerticalAlignment.Center });
                    int itemIdx = start + n;
                    CardsHost.Children.Add(Card(Items[itemIdx], itemIdx == nearestIdx));
                }
                if (Config.display_mode == "rotate" && Items.Count > 1)
                {
                    var page = Text((Index + 1) + "/" + Items.Count, 9.5, Muted);
                    page.FontFamily = WidgetNumFont;
                    page.Margin = new Thickness(2, 0, 9, 0);
                    CardsHost.Children.Add(page);
                }
            }
            Update(); W.UpdateLayout(); Position();
        }

        static UIElement Card(TimedItem entry, bool isNearest)
        {
            var item = entry.Item;
            var target = entry.Date;
            bool matchFest = ActiveSkinId != "default" && ActiveSkinId != "minimal" && FestivalForName(item.name) == ActiveSkinId;
            bool highlight = isNearest || matchFest;
            var col = new StackPanel { Background = Brushes.Transparent, Tag = entry, Margin = new Thickness(9, 3.5, 10, 3.5), MinWidth = Config.show_seconds ? 132 : 50 };
            col.ToolTip = item.name + "\n" + target.ToString("yyyy-MM-dd HH:mm") + "\n单击切换时分秒 · 拖动调整位置 · 右键设置";

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (highlight)
            {
                titleRow.Children.Add(new Border
                {
                    Width = 2.5, Height = 9, CornerRadius = new CornerRadius(1.25),
                    Background = DetailBrush, Margin = new Thickness(0, 0, 4.5, 0), VerticalAlignment = VerticalAlignment.Center
                });
            }
            var name = Text(string.IsNullOrWhiteSpace(item.name) ? "倒计时" : item.name, 10, highlight ? DetailBrush : Muted);
            name.FontWeight = highlight ? FontWeights.SemiBold : FontWeights.Normal;
            name.MaxWidth = 118;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            titleRow.Children.Add(name);
            col.Children.Add(titleRow);

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0.5, 0, 0) };
            var value = Text("", 18, Ink); value.FontFamily = WidgetNumFont; value.FontWeight = FontWeights.Bold;
            var suffix = Text("", 10, Muted); suffix.Margin = new Thickness(3.5, 3, 0, 0);
            if (Config.show_seconds) suffix.FontFamily = WidgetMonoFont;
            row.Children.Add(value); row.Children.Add(suffix); col.Children.Add(row);
            Slots.Add(new CountdownSlot { Target = target, Value = value, Suffix = suffix });
            return col;
        }

        static void Update()
        {
            var now = DateTime.Now;
            foreach (var slot in Slots)
            {
                var target = slot.Target;
                string value = DayText(target, now, Config.show_seconds);
                bool numeric = value != "已到期" && value != "今天";
                double fontSize = numeric ? 18 : 13.5;
                Brush fg = target <= now ? Muted : ValueInk;
                string suffix = !numeric ? "" : Config.show_seconds ? "天  " + (target - now).ToString(@"hh\:mm\:ss") : "天";
                if (slot.Value.Text != value) slot.Value.Text = value;
                if (slot.Value.FontSize != fontSize) slot.Value.FontSize = fontSize;
                if (!ReferenceEquals(slot.Value.Foreground, fg)) slot.Value.Foreground = fg;
                if (slot.Suffix.Text != suffix) slot.Suffix.Text = suffix;
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
            double left = r.Left / sx + Offset;
            double top = r.Top / sy + Math.Max(0, ((r.Bottom - r.Top) / sy - W.ActualHeight) / 2);
            if (Math.Abs(W.Left - left) > 0.1) W.Left = left;
            if (Math.Abs(W.Top - top) > 0.1) W.Top = top;
        }

        static bool HasWatchdog()
        {
            try { using (Mutex.OpenExisting("Local\\DshCountdownWatchdog2027")) return true; }
            catch { return false; }
        }

        static void Tick()
        {
            try
            {
                if (!Preview && Editor == null && ShareWin == null && File.Exists(PendingExePath) && HasWatchdog())
                {
                    W.Close();
                    return;
                }

                bool menuOpen = MenuOpen || (W.ContextMenu != null && W.ContextMenu.IsOpen);
                if (menuOpen)
                {
                    var menu = W.ContextMenu;
                    if (menu != null && menu.IsOpen)
                    {
                        bool down = (GetAsyncKeyState(0x01) & 0x8000) != 0 || (GetAsyncKeyState(0x02) & 0x8000) != 0;
                        if (down)
                        {
                            bool outside = true;
                            var src = PresentationSource.FromVisual(menu) as HwndSource;
                            if (src != null && src.Handle != IntPtr.Zero)
                            {
                                RECT mr; POINT cp;
                                if (GetWindowRect(src.Handle, out mr) && GetCursorPos(out cp))
                                    outside = cp.X < mr.Left || cp.X > mr.Right || cp.Y < mr.Top || cp.Y > mr.Bottom;
                            }
                            if (outside) menu.IsOpen = false;
                        }
                    }
                    return;
                }

                if (LowFreqClock.ElapsedMilliseconds >= 1800)
                {
                    LowFreqClock.Restart();
                    SyncStar();
                    _systemLight = ReadSystemLight();
                    if (File.Exists(ConfigPath) && File.GetLastWriteTimeUtc(ConfigPath) != LastWrite)
                    {
                        Config = ReadConfig();
                        LastWrite = File.GetLastWriteTimeUtc(ConfigPath);
                        Build();
                        RotationClock.Restart();
                        SyncTimerInterval();
                    }
                    if (IsLight() != Light) Build();
                    if (Config.skin == "auto" && ResolveSkinId() != ActiveSkinId) Build();
                    Position();
                    if (Hwnd != IntPtr.Zero)
                    {
                        if (W.Opacity != 1) W.Opacity = 1;
                        if (!W.IsHitTestVisible) W.IsHitTestVisible = true;
                        SetWindowPos(Hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOCOPYBITS | SWP_NOOWNERZORDER);
                    }
                }

                if (Config.display_mode == "rotate" && Items.Count > 1 && RotationClock.Elapsed.TotalSeconds >= Config.rotate_seconds)
                {
                    Index = (Index + 1) % Items.Count;
                    RotationClock.Restart();
                    BuildCards();
                }
                Update();
            }
            catch (Exception ex)
            {
                // Retry invalid/incomplete external edits only when their timestamp changes.
                LastWrite = File.GetLastWriteTimeUtc(ConfigPath);
                Log(ex);
            }
        }
    }
}
