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
    public static partial class Program
    {
        sealed class CountdownSlot
        {
            public ResolvedItem Entry;
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

        static Cfg Config = new Cfg();
        static Window W, Editor, ShareWin;
        static Border Root, DecorSlot, ProgressHairline;
        static Grid RootGrid;
        static StackPanel Host, CardsHost;
        static TranslateTransform CardsSlide;
        static IntPtr Hwnd;
        static DispatcherTimer MainTimer;
        static DateTime LastWrite;
        static List<ResolvedItem> ResolvedItems = new List<ResolvedItem>();
        static ResolvedItem _clickedEntry;
        static readonly List<CountdownSlot> Slots = new List<CountdownSlot>();
        static bool Light, _systemLight = true, Dragging, Moved, Preview, MenuOpen, SessionLocked, _fullscreenHidden;
        static double Offset = 236, StartLeft;
        static int StartX, Index;
        static FrameworkElement Star;
        static AnimationClock StarClock;
        static string _renderedSkinId;
        static bool _renderedLight, _renderedGlass, _renderedProg;
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
            bool pause = !Config.motion || SessionLocked || _fullscreenHidden || !W.IsVisible || W.Opacity <= 0 || W.WindowState == WindowState.Minimized;
            if (pause)
            {
                if (StarClock != null) { StarClock.Controller.Remove(); Star.BeginAnimation(UIElement.OpacityProperty, null); StarClock = null; }
            }
            else if (StarClock == null)
            {
                var breathing = new DoubleAnimation(ActiveSkinId == "midautumn" ? 0.72 : 0.52, 1.0, TimeSpan.FromSeconds(ActiveSkinId == "midautumn" ? 4 : 3))
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
                : TimeSpan.FromMilliseconds(Config.show_seconds ? 500 : 1800);
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
        const uint SWP_NOOWNERZORDER = 0x0200;

        const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        const uint WINEVENT_OUTOFCONTEXT = 0x0000;

        public const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;
        public const int DWMWA_DISALLOW_PEEK = 11;
        public const int DWMWA_EXCLUDED_FROM_PEEK = 12;

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
        static readonly uint MsgTaskbarCreated = RegisterWindowMessage("TaskbarCreated");
        static readonly uint MsgWakeStudio = RegisterWindowMessage("TaskbarCountdownWakeStudio2027");

        delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
        [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
        [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)] static extern uint RegisterWindowMessage(string lpString);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] static extern int GetWindowLong32(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] static extern int SetWindowLong32(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);

        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        const int WM_TRAYICON = 0x8009;
        const int WM_LBUTTONUP = 0x0202;
        const int WM_LBUTTONDBLCLK = 0x0203;
        const int WM_RBUTTONUP = 0x0205;
        const uint NIM_ADD = 0x00000000;
        const uint NIM_MODIFY = 0x00000001;
        const uint NIM_DELETE = 0x00000002;
        const uint NIF_MESSAGE = 0x00000001;
        const uint NIF_ICON = 0x00000002;
        const uint NIF_TIP = 0x00000004;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, System.Text.StringBuilder lpIconPath, ref ushort lpiIcon);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr hIcon);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);

        static bool _trayAdded;
        static IntPtr _trayHIcon = IntPtr.Zero;

        static IntPtr GetTrayIconHandle()
        {
            if (_trayHIcon != IntPtr.Zero) return _trayHIcon;
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    var sb = new System.Text.StringBuilder(exePath, 260);
                    ushort idx = 0;
                    _trayHIcon = ExtractAssociatedIcon(IntPtr.Zero, sb, ref idx);
                }
            }
            catch { }
            return _trayHIcon;
        }

        static void SyncTrayIcon(bool forceReadd = false)
        {
            if (Preview || Hwnd == IntPtr.Zero) return;
            bool want = Config != null && Config.show_tray_icon;
            if (want)
            {
                if (_trayAdded && !forceReadd) return;
                var nid = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = Hwnd,
                    uID = 101,
                    uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                    uCallbackMessage = WM_TRAYICON,
                    hIcon = GetTrayIconHandle(),
                    szTip = "拾光倒计时 · 左键浮层 / 双击工作台 / 右键菜单"
                };
                if (Shell_NotifyIcon(NIM_ADD, ref nid) || Shell_NotifyIcon(NIM_MODIFY, ref nid))
                    _trayAdded = true;
            }
            else
            {
                RemoveTrayIcon(false);
            }
        }

        static void RemoveTrayIcon(bool destroyIcon)
        {
            if (_trayAdded && Hwnd != IntPtr.Zero)
            {
                try
                {
                    var nid = new NOTIFYICONDATA { cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)), hWnd = Hwnd, uID = 101 };
                    Shell_NotifyIcon(NIM_DELETE, ref nid);
                }
                catch { }
                _trayAdded = false;
            }
            if (destroyIcon && _trayHIcon != IntPtr.Zero)
            {
                try { DestroyIcon(_trayHIcon); } catch { }
                _trayHIcon = IntPtr.Zero;
            }
        }

        static void ShowContextMenuAtCursor()
        {
            var menu = W != null ? W.ContextMenu : null;
            if (menu == null) return;
            POINT p;
            if (!GetCursorPos(out p)) return;
            double sx = ScaleX(), sy = ScaleY();
            double xRel = p.X / sx - W.Left - 12;
            double yRel = p.Y / sy - W.Top;
            menu.PlacementTarget = W;
            menu.Placement = PlacementMode.Custom;
            menu.CustomPopupPlacementCallback = (popup, target, offset) => new[] {
                new CustomPopupPlacement(new Point(xRel, yRel - popup.Height - 4), PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(new Point(xRel, -popup.Height + 6), PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(new Point(xRel, W.ActualHeight - 4), PopupPrimaryAxis.Horizontal)
            };
            menu.IsOpen = true;
        }

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
            if (Hwnd != IntPtr.Zero && !Preview && !Dragging && !_fullscreenHidden && hwnd != Hwnd)
            {
                SetWindowPos(Hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
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
                    if (modified)
                    {
                        Marshal.StructureToPtr(wp, lParam, false);
                    }
                }
                catch { }
            }
            else if (msg == WM_TRAYICON && !Preview)
            {
                int ev = lParam.ToInt32() & 0xFFFF;
                if (ev == WM_LBUTTONUP)
                {
                    if (W != null) W.Dispatcher.BeginInvoke(new Action(() => ToggleFlyout(null)));
                }
                else if (ev == WM_LBUTTONDBLCLK)
                {
                    if (W != null) W.Dispatcher.BeginInvoke(new Action(() => { CloseFlyout(); OpenSettings(0); }));
                }
                else if (ev == WM_RBUTTONUP)
                {
                    if (W != null) W.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CloseFlyout();
                        _clickedEntry = null;
                        SetForegroundWindow(Hwnd);
                        ShowContextMenuAtCursor();
                    }));
                }
                handled = true;
                return IntPtr.Zero;
            }
            else if (msg == MsgWakeStudio && !Preview)
            {
                try
                {
                    if (W != null) W.Dispatcher.BeginInvoke(new Action(() => OpenSettings(0)));
                }
                catch { }
            }
            else if (msg == MsgTaskbarCreated && !Preview)
            {
                try
                {
                    IntPtr tray = FindWindow("Shell_TrayWnd", null);
                    if (tray != IntPtr.Zero)
                    {
                        SetWindowLongPtr(hwnd, -8, tray);
                        int trueVal = 1;
                        DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref trueVal, sizeof(int));
                        DwmSetWindowAttribute(hwnd, DWMWA_DISALLOW_PEEK, ref trueVal, sizeof(int));
                        DwmSetWindowAttribute(hwnd, DWMWA_EXCLUDED_FROM_PEEK, ref trueVal, sizeof(int));
                    }
                    Position();
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
                    SyncTrayIcon(true);
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
            if (string.IsNullOrEmpty(c.click_action)) c.click_action = "flyout";
            if (string.IsNullOrEmpty(c.anchor)) c.anchor = "free";
            if (string.IsNullOrEmpty(c.font_style)) c.font_style = "modern";
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
            if (W != null) { if (fullRebuild) Build(true); else { RefreshResolved(); BuildCards(false); } SyncTimerInterval(); }
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

        public static SkinSpec FindSkin(string id)
        {
            if (string.IsNullOrEmpty(id)) id = "default";
            if (id == "auto") id = DetectFestivalSkin();
            for (int i = 0; i < SkinCatalog.Length; i++)
                if (SkinCatalog[i].Id == id) return SkinCatalog[i];
            return SkinCatalog[0];
        }

        public static SkinPalette PickSkinPalette(string id, bool light)
        {
            var spec = FindSkin(id);
            return light ? spec.LightPal : spec.DarkPal;
        }

        static string DetectFestivalSkin()
        {
            var now = DateTime.Now;
            foreach (var it in ResolvedItems)
            {
                if (it.IsExpired || it.IsCountup) continue;
                string id = FestivalForName(it.Name);
                if (id != "default") return id;
            }
            return "default";
        }

        public static string FestivalForName(string name)
        {
            var n = name ?? "";
            if (n.Contains("圣诞")) return "christmas";
            if (n.Contains("元旦") || n.Contains("新年")) return "newyear";
            if (n.Contains("春节") || n.Contains("过年") || n.Contains("除夕")) return "spring";
            if (n.Contains("中秋")) return "midautumn";
            if (n.Contains("国庆")) return "national";
            return "default";
        }

        static string HexA(string alpha, string rgb) { return "#" + alpha + rgb.TrimStart('#'); }
        static Color C_(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
        static string ResolveSkinId() { return FindSkin(Config.skin).Id; }
        static string DetailColorFor(string skinId, bool light) { return PickSkinPalette(skinId, light).Detail; }
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
            var canvas = new Canvas { Width = 24, Height = 26 };
            string detail = DetailColorFor(skinId, light);
            switch (skinId)
            {
                case "newyear":
                case "aurora":
                    string subRay = skinId == "aurora" ? (light ? "#68A6B8" : "#5AA0C2") : (light ? "#7C9AC2" : "#8BA4C3");
                    StrokePath(canvas, "M12,4 L12,22 M3,13 L21,13 M6,7 L18,19 M18,7 L6,19", subRay, 0.95);
                    AddPoly(canvas, "12,5 13.8,11 20,13 13.8,15 12,21 10.2,15 4,13 10.2,11", C_(detail));
                    star = (FrameworkElement)canvas.Children[canvas.Children.Count - 1];
                    break;
                case "xuanpaper":
                    var sealRect = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(3), BorderBrush = Brush(detail), BorderThickness = new Thickness(1.2), Background = Brush(HexA("18", detail)) };
                    sealRect.Child = new TextBlock { Text = "时", FontFamily = SerifFont, FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = Brush(detail), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    Canvas.SetLeft(sealRect, 4); Canvas.SetTop(sealRect, 5); canvas.Children.Add(sealRect);
                    star = sealRect;
                    break;
                case "vinyl":
                    var disc = new System.Windows.Shapes.Ellipse { Width = 17, Height = 17, Stroke = Brush(detail), StrokeThickness = 1.4, Fill = Brush(HexA("24", detail)) };
                    Canvas.SetLeft(disc, 3.5); Canvas.SetTop(disc, 4.5); canvas.Children.Add(disc);
                    var centerDot = new System.Windows.Shapes.Ellipse { Width = 5, Height = 5, Fill = Brush(detail) };
                    Canvas.SetLeft(centerDot, 9.5); Canvas.SetTop(centerDot, 10.5); canvas.Children.Add(centerDot);
                    star = centerDot;
                    break;
                case "monet":
                    var petal = StrokePath(canvas, "M12,5 C16,10 16,16 12,21 C8,16 8,10 12,5 Z", detail, 1.25);
                    petal.Fill = Brush(HexA("24", detail));
                    StrokePath(canvas, "M5,17 Q12,14 19,17", detail, 1.0);
                    star = petal;
                    break;
                case "spring":
                    StrokePath(canvas, "M12,1.5 L12,4 M8,5 L16,5 M8,21 L16,21 M12,22 L12,25", detail, 1.15);
                    var lantern = StrokePath(canvas, "M12,5.5 C2.5,5.5 2.5,20.5 12,20.5 C21.5,20.5 21.5,5.5 12,5.5 Z", detail, 1.25);
                    lantern.Fill = Brush(light ? "#28B53F31" : "#2CF0C577");
                    star = StrokePath(canvas, "M12,10 L12,16 M9.5,13 L14.5,13", light ? "#D88928" : "#FFE099", 1.4);
                    break;
                case "midautumn":
                    var moon = new System.Windows.Shapes.Ellipse { Width = 15, Height = 15, Fill = Brush(detail) };
                    Canvas.SetLeft(moon, 4.5); Canvas.SetTop(moon, 3.5); canvas.Children.Add(moon); star = moon;
                    StrokePath(canvas, "M2,21.5 L8,21.5 C10,18.5 14,18.5 16,21.5 L22,21.5", light ? "#7B8AA8" : "#A4B5C6", 1.0);
                    break;
                case "national":
                    var flag = StrokePath(canvas, "M3,23.5 L3,4 M4,5 C10,2 16,8 22,5 L22,18 C16,21 10,15 4,18", detail, 1.15);
                    flag.Fill = Brush(light ? "#22B33329" : "#22F1D28E");
                    AddPoly(canvas, "10,7 11,9.5 13.5,9.7 11.5,11.4 12.1,14 10,12.5 7.9,14 8.5,11.4 6.5,9.7 9,9.5", C_(detail));
                    star = (FrameworkElement)canvas.Children[canvas.Children.Count - 1];
                    break;
                case "cream":
                    var sun = new System.Windows.Shapes.Ellipse { Width = 10, Height = 10, Fill = Brush(detail) };
                    Canvas.SetLeft(sun, 7); Canvas.SetTop(sun, 5.5); canvas.Children.Add(sun); star = sun;
                    StrokePath(canvas, "M3,19 L21,19 M6,22 L18,22", light ? "#C49E82" : "#9E826E", 1.1);
                    break;
                case "celadon":
                    var leaf = StrokePath(canvas, "M4.5,21 C4.5,11 11,5 19.5,5 C19.5,13.5 14,21 4.5,21 Z", detail, 1.25);
                    leaf.Fill = Brush(light ? "#2235785F" : "#227EC5A7");
                    StrokePath(canvas, "M4.5,21 C9,16 13.5,11.5 18.5,6", detail, 0.95);
                    star = leaf;
                    break;
                case "christmas":
                    string pine = light ? "#286B51" : "#8CC4A3";
                    var tree = new System.Windows.Shapes.Path
                    {
                        Data = Geometry.Parse("M12,6 L8,11.5 L10,11.5 L5.5,17 L8.8,17 L4.5,22 L19.5,22 L15.2,17 L18.5,17 L14,11.5 L16,11.5 Z"),
                        Fill = Brush(light ? "#2C286B51" : "#2C9CD4B5"),
                        Stroke = Brush(pine),
                        StrokeThickness = 1.15,
                        StrokeLineJoin = PenLineJoin.Round
                    };
                    canvas.Children.Add(tree);
                    StrokePath(canvas, "M12,22 L12,24.8", pine, 1.35);
                    AddPoly(canvas, "12,1 12.8,3 15,3.7 12.8,4.4 12,6.5 11.2,4.4 9,3.7 11.2,3", C_(detail));
                    star = (FrameworkElement)canvas.Children[canvas.Children.Count - 1];
                    break;
                default:
                    return null;
            }
            return canvas;
        }

        static TextBlock Text(string value, double size, Brush color)
        {
            return new TextBlock { Text = value, FontSize = size, Foreground = color, VerticalAlignment = VerticalAlignment.Center };
        }

        public static List<ResolvedItem> GetResolvedItems(DateTime now)
        {
            var list = new List<ResolvedItem>();
            if (Config == null || Config.items == null) return list;
            foreach (var it in Config.items)
            {
                if (it == null || !it.enabled || it.archived) continue;
                var res = ResolveItem(it, now);
                if (res != null) list.Add(res);
            }
            if (string.Equals(Config.sort_mode, "manual", StringComparison.OrdinalIgnoreCase))
            {
                // Preserve user's drag-and-drop manual order (with optional pinned item first)
                list = list
                    .OrderByDescending(x => !string.IsNullOrEmpty(Config.pinned_name) && string.Equals(x.Name, Config.pinned_name, StringComparison.Ordinal))
                    .ToList();
                return list;
            }
            // Smart ordering: Pinned first -> Today arrivals -> Upcoming by soonest -> Countups -> Expired
            list = list
                .OrderByDescending(x => !string.IsNullOrEmpty(Config.pinned_name) && string.Equals(x.Name, Config.pinned_name, StringComparison.Ordinal))
                .ThenByDescending(x => x.IsToday)
                .ThenBy(x => x.IsCountup ? 1 : (x.IsExpired ? 2 : 0))
                .ThenBy(x => x.IsCountup ? -x.DaysValue : x.DaysValue)
                .ThenBy(x => x.TargetDate)
                .ToList();
            return list;
        }

        static void RefreshResolved()
        {
            ResolvedItems = GetResolvedItems(DateTime.Now);
        }

        static void LoadPos()
        {
            try
            {
                if (!File.Exists(PosPath)) return;
                var data = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(PosPath));
                if (data != null && data.ContainsKey("offset")) Offset = Convert.ToDouble(data["offset"], CultureInfo.InvariantCulture);
            }
            catch (Exception ex) { Log(ex); }
        }

        const string WatchdogMutexName = "Local\\DshCountdownWatchdog2027";
        const string WatchdogStopEventName = "Local\\DshCountdownWatchdogStop2027";
        const string WidgetMutexName = "Local\\DshCountdownWpf2027";

        static bool IsWidgetProcessRunning()
        {
            using (var widget = new Mutex(false, WidgetMutexName))
            {
                bool acquired;
                try { acquired = widget.WaitOne(0); }
                catch (AbandonedMutexException) { acquired = true; }
                if (acquired) widget.ReleaseMutex();
                return !acquired;
            }
        }

        static void SignalWatchdogStop()
        {
            try
            {
                using (var ev = new EventWaitHandle(false, EventResetMode.ManualReset, WatchdogStopEventName))
                    ev.Set();
            }
            catch { }
        }

        static void RunSelfWatchdog()
        {
            string exe = Path.Combine(Dir, "Countdown.exe");
            string pending = exe + ".pending";
            string oldExe = exe + ".old";
            bool handoffToNewBinary = false;

            bool created;
            using (var guard = new Mutex(true, WatchdogMutexName, out created))
            {
                if (!created) return;

                using (var stopEv = new EventWaitHandle(false, EventResetMode.ManualReset, WatchdogStopEventName))
                {
                    stopEv.Reset();

                    // Remove any previous hot-swapped binary once unlocked
                    for (int i = 0; i < 8 && File.Exists(oldExe); i++)
                    {
                        try { File.Delete(oldExe); break; }
                        catch { Thread.Sleep(200); }
                    }

                    while (File.Exists(exe) || File.Exists(pending))
                    {
                        if (stopEv.WaitOne(0)) return;
                        try
                        {
                            if (!IsWidgetProcessRunning())
                            {
                                if (stopEv.WaitOne(0)) return;
                                if (File.Exists(pending))
                                {
                                    bool swapped = false;
                                    for (int i = 0; i < 10; i++)
                                    {
                                        try
                                        {
                                            if (File.Exists(oldExe)) File.Delete(oldExe);
                                            if (File.Exists(exe)) File.Move(exe, oldExe);
                                            File.Move(pending, exe);
                                            swapped = true;
                                            break;
                                        }
                                        catch (IOException) { Thread.Sleep(150); }
                                        catch (UnauthorizedAccessException) { Thread.Sleep(150); }
                                    }
                                    if (swapped)
                                    {
                                        handoffToNewBinary = true;
                                        break;
                                    }
                                }
                                if (File.Exists(exe) && !stopEv.WaitOne(0))
                                {
                                    using (var p = Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Dir, UseShellExecute = false })) { }
                                }
                            }
                        }
                        catch (Exception ex) { Log(ex); }

                        if (stopEv.WaitOne(1200)) return;
                    }
                }
            }

            // Spawn the newly swapped single-EXE watchdog AFTER releasing the mutex handle
            if (handoffToNewBinary && File.Exists(exe))
            {
                try
                {
                    using (var p = Process.Start(new ProcessStartInfo(exe, "--watchdog") { WorkingDirectory = Dir, UseShellExecute = false })) { }
                }
                catch (Exception ex) { Log(ex); }
            }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Any(a => string.Equals(a, "--watchdog", StringComparison.OrdinalIgnoreCase)))
            {
                RunSelfWatchdog();
                return;
            }
            bool settings = args.Any(a => a == "--settings");
            Preview = args.Any(a => a == "--preview");
            bool first;
            using (var mutex = new Mutex(true, settings ? "Local\\DshCountdownSettings2027" : Preview ? "Local\\DshCountdownPreview2027" : WidgetMutexName, out first))
            {
                if (!first)
                {
                    // Single-instance friendly wake-up: clicking Countdown.exe again opens Studio!
                    if (!Preview) PostMessage(HWND_BROADCAST, MsgWakeStudio, IntPtr.Zero, IntPtr.Zero);
                    return;
                }
                try
                {
                    if (!File.Exists(ConfigPath))
                    {
                        var example = Path.Combine(Dir, "config.example.json");
                        if (File.Exists(example)) File.Copy(example, ConfigPath, false);
                    }
                    Config = ReadConfig();
                    LastWrite = File.Exists(ConfigPath) ? File.GetLastWriteTimeUtc(ConfigPath) : DateTime.MinValue;
                    _systemLight = ReadSystemLight();
                    LoadPos();
                    var app = new Application();
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    if (settings) { OpenSettings(0); Editor.Closed += (s, e) => app.Shutdown(); }
                    else CreateWidget();
                    app.Run();
                }
                catch (Exception ex) { Log(ex); }
            }
        }

        static void CreateWidget()
        {
            W = new Window
            {
                Title = Preview ? "拾光倒计时 · 预览" : "任务栏倒计时",
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

            IntPtr tray = FindWindow("Shell_TrayWnd", null);
            if (tray != IntPtr.Zero && !Preview)
            {
                new WindowInteropHelper(W).Owner = tray;
            }

            if (!Preview)
            {
                _foregroundDelegate = OnForegroundChanged;
                _foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _foregroundDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
            }

            Root = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Padding = new Thickness(6, 0, 6, 0), MinHeight = 38, ClipToBounds = true };
            RootGrid = new Grid();
            Root.Child = RootGrid;

            Host = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DecorSlot = new Border { VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
            CardsSlide = new TranslateTransform(0, 0);
            CardsHost = new StackPanel { Orientation = Orientation.Horizontal, RenderTransform = CardsSlide };
            Host.Children.Add(DecorSlot);
            Host.Children.Add(CardsHost);
            RootGrid.Children.Add(Host);

            // Bottom 2px ambient progress hairline
            ProgressHairline = new Border
            {
                Height = 2,
                CornerRadius = new CornerRadius(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(4, 0, 4, 1),
                Opacity = 0.78,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            RootGrid.Children.Add(ProgressHairline);

            W.Content = Root;

            W.SourceInitialized += (s, e) =>
            {
                Hwnd = new WindowInteropHelper(W).Handle;
                if (!Preview)
                {
                    AddWindowExStyle(Hwnd, 0x08000000 | 0x80);
                    if (tray != IntPtr.Zero)
                    {
                        SetWindowLongPtr(Hwnd, -8, tray);
                    }
                    int trueVal = 1;
                    DwmSetWindowAttribute(Hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref trueVal, sizeof(int));
                    DwmSetWindowAttribute(Hwnd, DWMWA_DISALLOW_PEEK, ref trueVal, sizeof(int));
                    DwmSetWindowAttribute(Hwnd, DWMWA_EXCLUDED_FROM_PEEK, ref trueVal, sizeof(int));
                    SyncTrayIcon();
                }
                var source = HwndSource.FromHwnd(Hwnd);
                if (source != null) source.AddHook(WndProc);
            };

            W.MouseLeftButtonDown += (s, e) =>
            {
                _clickedEntry = FindHitEntry(e.OriginalSource as DependencyObject);
                POINT p; if (!GetCursorPos(out p)) return;
                Dragging = true; Moved = false; StartX = p.X; StartLeft = W.Left; W.CaptureMouse();
            };
            W.MouseMove += (s, e) =>
            {
                if (!Dragging) return;
                POINT p; GetCursorPos(out p); double dx = (p.X - StartX) / ScaleX();
                if (Math.Abs(dx) > 4) Moved = true;
                if (Moved) W.Left = StartLeft + dx;
            };
            W.MouseLeftButtonUp += (s, e) =>
            {
                if (!Dragging) return;
                Dragging = false; W.ReleaseMouseCapture();
                if (Moved)
                {
                    Config.anchor = "free";
                    RECT r; if (Taskbar(out r)) Offset = W.Left - r.Left / ScaleX();
                    Position();
                    try { File.WriteAllText(PosPath, Json.Serialize(new { offset = Offset })); } catch (Exception ex) { Log(ex); }
                }
                else
                {
                    if (string.Equals(Config.click_action, "seconds", StringComparison.OrdinalIgnoreCase))
                    {
                        Config.show_seconds = !Config.show_seconds;
                        Persist(false, false);
                    }
                    else
                    {
                        ToggleFlyout(_clickedEntry);
                    }
                }
            };
            W.LostMouseCapture += (s, e) => Dragging = false;

            // Mouse wheel scrolling on the taskbar pill cycles smoothly through moments!
            W.PreviewMouseWheel += (s, e) =>
            {
                if (ResolvedItems.Count <= 1) return;
                if (Config.display_mode == "single" || Config.display_mode == "rotate" || Config.display_mode == "dual")
                {
                    e.Handled = true;
                    int dir = e.Delta < 0 ? 1 : -1;
                    Index = (Index + dir + ResolvedItems.Count) % ResolvedItems.Count;
                    RotationClock.Restart();
                    BuildCards(true, dir);
                }
            };

            W.MouseEnter += (s, e) =>
            {
                if (ActiveSkinId == "minimal" && Root != null) Root.Background = Brush(Light ? "#18000000" : "#1CFFFFFF");
            };
            W.MouseLeave += (s, e) =>
            {
                if (ActiveSkinId == "minimal" && Root != null) Root.Background = Brush(Light ? "#01FFFFFF" : "#01000000");
            };
            W.PreviewMouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                CloseFlyout();
                _clickedEntry = FindHitEntry(e.OriginalSource as DependencyObject);
                ShowContextMenuAtCursor();
            };

            Microsoft.Win32.SystemEvents.SessionSwitch += SessionChanged;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += PreferenceChanged;
            W.IsVisibleChanged += (s, e) =>
            {
                if (!Preview && W.Visibility != Visibility.Visible)
                {
                    W.Visibility = Visibility.Visible;
                }
                SyncStar();
            };
            W.StateChanged += (s, e) =>
            {
                if (!Preview && W.WindowState != WindowState.Normal)
                {
                    W.WindowState = WindowState.Normal;
                }
                SyncStar();
            };
            W.Closed += (s, e) =>
            {
                CloseFlyout();
                RemoveTrayIcon(true);
                if (_foregroundHook != IntPtr.Zero)
                {
                    UnhookWinEvent(_foregroundHook);
                    _foregroundHook = IntPtr.Zero;
                }
                Microsoft.Win32.SystemEvents.SessionSwitch -= SessionChanged;
                Microsoft.Win32.SystemEvents.UserPreferenceChanged -= PreferenceChanged;
                StopStar();
                if (MainTimer != null) MainTimer.Stop();
                if (Editor != null) { try { Editor.Close(); } catch { } }
                if (ShareWin != null) { try { ShareWin.Close(); } catch { } }
                Application.Current.Shutdown();
            };
            W.ContextMenu = Menu();
            Build(true); W.Show(); Position();
            MainTimer = new DispatcherTimer();
            MainTimer.Tick += (s, e) => Tick();
            SyncTimerInterval();
            MainTimer.Start();
        }

        static ResolvedItem FindHitEntry(DependencyObject cur)
        {
            while (cur != null)
            {
                var fe = cur as FrameworkElement;
                if (fe != null && fe.Tag is ResolvedItem) return (ResolvedItem)fe.Tag;
                cur = (cur is Visual || cur is System.Windows.Media.Media3D.Visual3D) ? VisualTreeHelper.GetParent(cur) : LogicalTreeHelper.GetParent(cur);
            }
            return null;
        }

        static ContextMenu Menu()
        {
            var menu = new ContextMenu { PlacementTarget = Root, Placement = PlacementMode.Top, VerticalOffset = -8, MinWidth = 172 };
            Theme(menu, IsLight());
            AddMenu(menu, "展开期待浮层", "M3,11 L8,5 L13,11", "Accent", () => OpenFlyout(_clickedEntry));
            AddMenu(menu, "生成社交卡片…", "M2,3.5 C2,2.7 2.7,2 3.5,2 L10.5,2 C11.3,2 12,2.7 12,3.5 L12,10.5 C12,11.3 11.3,12 10.5,12 L3.5,12 C2.7,12 2,11.3 2,10.5 Z M3.6,9.4 L5.8,7.0 L7.6,8.7 L9.2,7.2 L10.6,9.4", "Accent", () => OpenShareForResolved(_clickedEntry));
            menu.Items.Add(new Separator());
            AddMenu(menu, "拾光工作台（设置）…", "M2,4.5 L12,4.5 M2,9.5 L12,9.5 M5,2.8 L5,6.2 M9,7.8 L9,11.2", "Muted", () => OpenSettings(0));
            AddMenu(menu, "外观画廊与换肤…", "M7,1.8 A5.2,5.2 0 1,0 12.2,7 C12.2,5.8 11.2,5.2 10.1,5.2 L9,5.2 C8.2,5.2 7.8,5.8 7.8,6.5", "Muted", () => OpenSettings(1));
            menu.Items.Add(new Separator());
            AddMenu(menu, "退出倒计时", "M4.2,3.8 C2.9,4.7 2.1,6.2 2.1,7.9 C2.1,10.6 4.3,12.8 7,12.8 C9.7,12.8 11.9,10.6 11.9,7.9 C11.9,6.2 11.1,4.7 9.8,3.8 M7,1.8 L7,6.8", "Danger", () =>
            {
                if (!Preview)
                {
                    SignalWatchdogStop();
                    foreach (var p in Process.GetProcessesByName("CountdownWatchdog"))
                    {
                        try { if (string.Equals(Path.GetDirectoryName(p.MainModule.FileName), Dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) p.Kill(); }
                        catch (Exception ex) { Log(ex); }
                        finally { p.Dispose(); }
                    }
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
            RefreshResolved();
            ApplySkin(forceTheme);
            BuildCards(false);
            SyncStar();
        }

        static void ApplySkin(bool force)
        {
            Light = IsLight();
            ActiveSkinId = ResolveSkinId();
            if (!force && _renderedSkinId == ActiveSkinId && _renderedLight == Light && _renderedGlass == Config.glass && _renderedProg == Config.show_progress_bar) return;
            var sk = PickSkinPalette(ActiveSkinId, Light);
            Ink = Brush(sk.Text);
            Muted = Brush(sk.Muted);
            ValueInk = Brush(sk.Accent);
            DetailBrush = Brush(sk.Detail);
            bool isMinimal = ActiveSkinId == "minimal";
            bool themed = ActiveSkinId != "default" && !isMinimal;

            if (isMinimal)
            {
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
                string a = Config.glass ? (themed ? "CE" : "C0") : "FF";
                var gb = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                gb.GradientStops.Add(new GradientStop(C_(HexA(a, sk.Bg1)), 0));
                gb.GradientStops.Add(new GradientStop(C_(HexA(a, sk.Bg2)), 1));
                gb.Freeze();
                Root.Background = gb;

                Color rimTop = Light
                    ? Color.FromArgb(Config.glass ? (byte)230 : (byte)170, 255, 255, 255)
                    : Color.FromArgb(Config.glass ? (byte)92 : (byte)64, 255, 255, 255);
                Color rimBot = Light ? Color.FromArgb(34, 0, 0, 0) : Color.FromArgb(24, 255, 255, 255);
                if (themed && !Light) rimTop = C_(HexA("62", sk.Detail));
                var borderGrad = new LinearGradientBrush(rimTop, rimBot, new Point(0, 0), new Point(0.5, 1));
                borderGrad.Freeze();
                Root.BorderBrush = borderGrad;
                Root.BorderThickness = new Thickness(1);
                Root.CornerRadius = new CornerRadius(10);
                Root.Padding = new Thickness(6, 0, 6, 0);
            }

            if (W.ContextMenu != null && (force || _renderedLight != Light)) SetPalette(W.ContextMenu, Light, false);

            // Only show side emblem decor when single/rotate or themed without clutter
            if (force || _renderedSkinId != ActiveSkinId || _renderedLight != Light)
            {
                StopStar();
                var deco = CreateDecor(ActiveSkinId, Light, out Star);
                DecorSlot.Child = deco;
                if (deco != null)
                {
                    DecorSlot.Margin = new Thickness(5, 0, 5, 0);
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
            _renderedProg = Config.show_progress_bar;
        }

        static void BuildCards(bool animateSlide, int slideDir = 1)
        {
            CardsHost.Children.Clear();
            Slots.Clear();
            if (Index >= ResolvedItems.Count) Index = 0;

            if (ResolvedItems.Count == 0)
            {
                var empty = Text("添加一个值得期待的日子", 11, Muted);
                empty.Margin = new Thickness(10, 9, 10, 9);
                CardsHost.Children.Add(empty);
                ProgressHairline.Visibility = Visibility.Collapsed;
            }
            else
            {
                bool themed = ActiveSkinId != "default" && ActiveSkinId != "minimal";
                var sepBrush = Brush(themed ? HexA("2E", DetailColor()) : Light ? "#1E000000" : "#22FFFFFF");
                string mode = (Config.display_mode ?? "expand").ToLowerInvariant();
                int count = mode == "single" || mode == "rotate" ? 1
                    : mode == "dual" ? Math.Min(2, ResolvedItems.Count)
                    : ResolvedItems.Count;
                int start = (mode == "single" || mode == "rotate" || mode == "dual") ? Index : 0;

                ResolvedItem primaryForProg = null;
                for (int n = 0; n < count; n++)
                {
                    if (n > 0) CardsHost.Children.Add(new Border { Width = 1, Height = 16, Background = sepBrush, VerticalAlignment = VerticalAlignment.Center });
                    int itemIdx = (start + n) % ResolvedItems.Count;
                    var entry = ResolvedItems[itemIdx];
                    if (n == 0) primaryForProg = entry;
                    bool highlight = n == 0 || entry.IsToday;
                    CardsHost.Children.Add(Card(entry, highlight));
                }

                if ((mode == "rotate" || mode == "single") && ResolvedItems.Count > 1)
                {
                    var page = Text((Index + 1) + "/" + ResolvedItems.Count, 9.5, Muted);
                    page.FontFamily = WidgetNumFont;
                    page.Margin = new Thickness(1, 0, 8, 0);
                    page.ToolTip = "滚轮切换日程";
                    CardsHost.Children.Add(page);
                }

                // Update bottom ambient progress hairline
                if (Config.show_progress_bar && ActiveSkinId != "minimal" && primaryForProg != null)
                {
                    ProgressHairline.Background = primaryForProg.IsToday ? Brush("#D89E34") : Brush(HexA("9E", DetailColor()));
                    ProgressHairline.Visibility = Visibility.Visible;
                }
                else
                {
                    ProgressHairline.Visibility = Visibility.Collapsed;
                }
            }

            Update();
            W.UpdateLayout();

            // Size bottom hairline to exact percentage of pill width
            if (ProgressHairline.Visibility == Visibility.Visible && ResolvedItems.Count > 0)
            {
                int pIdx = Math.Max(0, Math.Min(Index, ResolvedItems.Count - 1));
                double avail = Math.Max(16, RootGrid.ActualWidth - 8);
                ProgressHairline.Width = Math.Max(10, avail * ResolvedItems[pIdx].Progress);
            }

            Position();

            // Silky slide+fade micro-animation when cycling moments!
            if (animateSlide && Config.motion && CardsSlide != null)
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                CardsSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(slideDir * 8, 0, TimeSpan.FromMilliseconds(210)) { EasingFunction = ease });
                CardsHost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.25, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
            }
        }

        static UIElement Card(ResolvedItem entry, bool highlight)
        {
            var col = new StackPanel { Background = Brushes.Transparent, Tag = entry, Margin = new Thickness(8, 3, 9, 4), MinWidth = Config.show_seconds ? 132 : 52 };
            string tip = entry.Name + "\n" + entry.TargetDate.ToString("yyyy-MM-dd HH:mm");
            if (!string.IsNullOrEmpty(entry.LunarDisplay)) tip += "  (" + entry.LunarDisplay + ")";
            if (!string.IsNullOrEmpty(entry.RepeatLabel)) tip += "  ·  " + entry.RepeatLabel;
            tip += "\n单击打开期待浮层 · 滚轮切页 · 拖动定位 · 右键菜单";
            col.ToolTip = tip;

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            // Semantic micro-icon per event!
            var icon = BuildEventIcon(entry.ResolvedIcon, 10, entry.IsToday ? Brush("#D89E34") : (highlight ? DetailBrush : Muted), 1.4);
            icon.Margin = new Thickness(0, 0, 4, 0);
            titleRow.Children.Add(icon);

            var name = Text(entry.Name, 10, entry.IsToday ? Brush("#D89E34") : (highlight ? DetailBrush : Muted));
            name.FontWeight = highlight ? FontWeights.SemiBold : FontWeights.Normal;
            name.MaxWidth = 114;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            titleRow.Children.Add(name);
            col.Children.Add(titleRow);

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0.5, 0, 0) };
            var value = Text("", 17.5, Ink);
            value.FontFamily = ResolveWidgetNumberFont();
            value.FontWeight = FontWeights.Bold;
            var suffix = Text("", 9.5, Muted);
            suffix.Margin = new Thickness(3, 3, 0, 0);
            if (Config.show_seconds) suffix.FontFamily = WidgetMonoFont;
            row.Children.Add(value); row.Children.Add(suffix); col.Children.Add(row);
            Slots.Add(new CountdownSlot { Entry = entry, Value = value, Suffix = suffix });
            return col;
        }

        static void Update()
        {
            var now = DateTime.Now;
            foreach (var slot in Slots)
            {
                string suffix;
                bool numeric;
                string value = FormatResolvedShortValue(slot.Entry, now, Config.show_seconds, out suffix, out numeric);
                if (slot.Entry.IsToday && !numeric) value = "✦ 今天";
                double fontSize = numeric ? 17.5 : 13;
                Brush fg = slot.Entry.IsToday ? Brush(Light ? "#B57818" : "#F0C975")
                    : (slot.Entry.IsExpired ? Muted : ValueInk);
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
            double barLeft = r.Left / sx;

            double desiredOffset;
            string anch = (Config.anchor ?? "free").ToLowerInvariant();
            if (anch == "left")
            {
                desiredOffset = 14;
            }
            else if (anch == "right")
            {
                desiredOffset = Math.Max(12, width - W.ActualWidth - 260);
            }
            else
            {
                desiredOffset = Offset;
            }

            desiredOffset = Math.Max(4, Math.Min(desiredOffset, Math.Max(4, width - W.ActualWidth - 4)));
            if (anch == "free") Offset = desiredOffset;
            double left = barLeft + desiredOffset;
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
                if (!Preview && Editor == null && ShareWin == null && FlyoutWin == null && File.Exists(PendingExePath) && HasWatchdog())
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
                    _systemLight = ReadSystemLight();
                    if (File.Exists(ConfigPath) && File.GetLastWriteTimeUtc(ConfigPath) != LastWrite)
                    {
                        Config = ReadConfig();
                        LastWrite = File.GetLastWriteTimeUtc(ConfigPath);
                        Build();
                        SyncTrayIcon();
                        RotationClock.Restart();
                        SyncTimerInterval();
                    }
                    if (IsLight() != Light) Build();
                    if (Config.skin == "auto" && ResolveSkinId() != ActiveSkinId) Build();

                    // Fullscreen auto-hide etiquette
                    IntPtr tray = FindWindow("Shell_TrayWnd", null);
                    bool shouldHide = !Preview && Config.hide_fullscreen && FlyoutWin == null && IsFullscreenAppActive(Hwnd, tray);
                    if (shouldHide != _fullscreenHidden)
                    {
                        _fullscreenHidden = shouldHide;
                        W.Opacity = shouldHide ? 0 : 1;
                        W.IsHitTestVisible = !shouldHide;
                        SyncStar();
                    }
                    else
                    {
                        SyncStar();
                    }

                    Position();
                    if (Hwnd != IntPtr.Zero && !_fullscreenHidden)
                    {
                        if (W.Opacity != 1) W.Opacity = 1;
                        if (!W.IsHitTestVisible) W.IsHitTestVisible = true;
                        SetWindowPos(Hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
                    }
                }

                if (Config.display_mode == "rotate" && ResolvedItems.Count > 1 && RotationClock.Elapsed.TotalSeconds >= Config.rotate_seconds)
                {
                    Index = (Index + 1) % ResolvedItems.Count;
                    RotationClock.Restart();
                    BuildCards(true, 1);
                }
                Update();
            }
            catch (Exception ex)
            {
                LastWrite = File.GetLastWriteTimeUtc(ConfigPath);
                Log(ex);
            }
        }
    }
}
