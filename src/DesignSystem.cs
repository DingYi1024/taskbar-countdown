using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;

namespace CountdownWidget
{
    public static partial class Program
    {
        public sealed class SkinPalette
        {
            public string Bg1, Bg2, Text, Muted, Accent, Detail;
        }

        public sealed class SkinSpec
        {
            public string Id, Label, Subtitle, Seal;
            public bool LightDefault;
            public SkinPalette LightPal, DarkPal;
            public string Oc1, Oc2, Glow1, Glow2;
        }

        public static readonly SkinSpec[] SkinCatalog = {
            new SkinSpec {
                Id = "default", Label = "原生 · 融肤", Subtitle = "克制通透，融于桌面", Seal = null, LightDefault = false,
                LightPal = new SkinPalette { Bg1 = "#FAFBFD", Bg2 = "#EEF2F7", Text = "#1B2430", Muted = "#607082", Accent = "#2F6299", Detail = "#2F6299" },
                DarkPal  = new SkinPalette { Bg1 = "#232933", Bg2 = "#191E26", Text = "#F2F5F9", Muted = "#9BA8B8", Accent = "#7BB4EC", Detail = "#7BB4EC" },
                Oc1 = "#CFD8E4", Oc2 = "#E4E8EF", Glow1 = "#FFFFFF", Glow2 = "#95A8C2"
            },
            new SkinSpec {
                Id = "minimal", Label = "极简 · 无框", Subtitle = "纯文字悬浮，隐于任务栏", Seal = null, LightDefault = false,
                LightPal = new SkinPalette { Bg1 = "#00FFFFFF", Bg2 = "#00FFFFFF", Text = "#1B2430", Muted = "#566373", Accent = "#2C6099", Detail = "#2C6099" },
                DarkPal  = new SkinPalette { Bg1 = "#00000000", Bg2 = "#00000000", Text = "#F4F7FA", Muted = "#A7B3C2", Accent = "#84B9EC", Detail = "#84B9EC" },
                Oc1 = "#CFD8E4", Oc2 = "#E4E8EF", Glow1 = "#FFFFFF", Glow2 = "#95A8C2"
            },
            new SkinSpec {
                Id = "xuanpaper", Label = "宣纸 · 墨韵", Subtitle = "温润古纸与朱砂印记", Seal = "墨韵", LightDefault = true,
                LightPal = new SkinPalette { Bg1 = "#FBF8F1", Bg2 = "#F1EAD9", Text = "#241F1B", Muted = "#7A7065", Accent = "#A83226", Detail = "#A83226" },
                DarkPal  = new SkinPalette { Bg1 = "#262320", Bg2 = "#1B1816", Text = "#F4EFE6", Muted = "#ABA195", Accent = "#DC6B57", Detail = "#D9A760" },
                Oc1 = "#ECE5D5", Oc2 = "#DDD3BE", Glow1 = "#FFFDF8", Glow2 = "#C49A6C"
            },
            new SkinSpec {
                Id = "vinyl", Label = "黑胶 · 琥珀", Subtitle = "暗调黑胶与复古辉光", Seal = "黑胶", LightDefault = false,
                LightPal = new SkinPalette { Bg1 = "#F6F2EB", Bg2 = "#EAE2D5", Text = "#261E18", Muted = "#786A5E", Accent = "#B85D1F", Detail = "#B85D1F" },
                DarkPal  = new SkinPalette { Bg1 = "#1E1C1A", Bg2 = "#131211", Text = "#F5EDE0", Muted = "#9E9487", Accent = "#E89B4C", Detail = "#E89B4C" },
                Oc1 = "#2E2A26", Oc2 = "#1D1B19", Glow1 = "#E89B4C", Glow2 = "#6E4A26"
            },
            new SkinSpec {
                Id = "monet", Label = "莫奈 · 睡莲", Subtitle = "低饱和油画雾蓝淡紫", Seal = "睡莲", LightDefault = true,
                LightPal = new SkinPalette { Bg1 = "#F1F3FA", Bg2 = "#E4ECEC", Text = "#1E2538", Muted = "#606B84", Accent = "#5264A8", Detail = "#6E5FA3" },
                DarkPal  = new SkinPalette { Bg1 = "#1F2438", Bg2 = "#172030", Text = "#EEF1FA", Muted = "#9DA7C4", Accent = "#A7B4EA", Detail = "#C2A6E3" },
                Oc1 = "#DDE3F2", Oc2 = "#D5E4E3", Glow1 = "#FFFFFF", Glow2 = "#9EA9D6"
            },
            new SkinSpec {
                Id = "cream", Label = "奶油 · 暖麦", Subtitle = "柔光暖麦与秋日拿铁", Seal = "暖麦", LightDefault = true,
                LightPal = new SkinPalette { Bg1 = "#FCF8F2", Bg2 = "#F3EADF", Text = "#3A2B22", Muted = "#856F61", Accent = "#B56A36", Detail = "#B56A36" },
                DarkPal  = new SkinPalette { Bg1 = "#2E251F", Bg2 = "#211A15", Text = "#F7EFE5", Muted = "#C2AEA0", Accent = "#E7AD6B", Detail = "#E7AD6B" },
                Oc1 = "#EFE2D3", Oc2 = "#E3CBB2", Glow1 = "#FFF9F2", Glow2 = "#CFA17E"
            },
            new SkinSpec {
                Id = "celadon", Label = "晨雾 · 青瓷", Subtitle = "天青烟雨与玉质微光", Seal = "青瓷", LightDefault = true,
                LightPal = new SkinPalette { Bg1 = "#F2F7F4", Bg2 = "#E3EDE7", Text = "#1E352D", Muted = "#5B776D", Accent = "#35785F", Detail = "#35785F" },
                DarkPal  = new SkinPalette { Bg1 = "#1A2B25", Bg2 = "#12201B", Text = "#EBF3EF", Muted = "#97B3A8", Accent = "#7EC5A7", Detail = "#7EC5A7" },
                Oc1 = "#DCE7E0", Oc2 = "#C8D9CF", Glow1 = "#F8FCFA", Glow2 = "#8BB39E"
            },
            new SkinSpec {
                Id = "aurora", Label = "极光 · 磨砂", Subtitle = "冰岛极昼与星河蓝绿", Seal = "极光", LightDefault = false,
                LightPal = new SkinPalette { Bg1 = "#EFF6FB", Bg2 = "#E2ECF7", Text = "#172A3E", Muted = "#58738E", Accent = "#2C799D", Detail = "#2C799D" },
                DarkPal  = new SkinPalette { Bg1 = "#162638", Bg2 = "#141D33", Text = "#ECF5FB", Muted = "#8FB1C9", Accent = "#6ED2C8", Detail = "#6ED2C8" },
                Oc1 = "#D4E3EE", Oc2 = "#E2E6F2", Glow1 = "#FFFFFF", Glow2 = "#7DBAC7"
            },
            new SkinSpec {
                Id = "christmas", Label = "圣诞 · 松霜", Subtitle = "冬日松林与暖金星辉", Seal = "冬序", LightDefault = false,
                LightPal = new SkinPalette { Bg1 = "#F3F7F4", Bg2 = "#E5EFE8", Text = "#1A372C", Muted = "#577568", Accent = "#286B51", Detail = "#A87424" },
                DarkPal  = new SkinPalette { Bg1 = "#1C382F", Bg2 = "#122620", Text = "#F5EEDF", Muted = "#A9C0B2", Accent = "#E8CF92", Detail = "#E8CF92" },
                Oc1 = "#D6E2DC", Oc2 = "#EAE5DA", Glow1 = "#FFFFFF", Glow2 = "#92B8A3"
            },
            new SkinSpec {
                Id = "newyear", Label = "元旦 · 星序", Subtitle = "岁首星轨与清朗深蓝", Seal = "新岁", LightDefault = false,
                LightPal = new SkinPalette { Bg1 = "#F2F6FC", Bg2 = "#E3EBF7", Text = "#1A2B45", Muted = "#5B7090", Accent = "#2E5E9C", Detail = "#2E5E9C" },
                DarkPal  = new SkinPalette { Bg1 = "#203048", Bg2 = "#152135", Text = "#F2EEE5", Muted = "#AAB8CD", Accent = "#E6CD92", Detail = "#E6CD92" },
                Oc1 = "#D5DFEC", Oc2 = "#EAE4D8", Glow1 = "#FFFFFF", Glow2 = "#9FB4D1"
            },
            new SkinSpec {
                Id = "spring", Label = "春节 · 灯笺", Subtitle = "温雅红笺与万家灯火", Seal = "新春", LightDefault = true,
                LightPal = new SkinPalette { Bg1 = "#FBF3E8", Bg2 = "#F4E3CE", Text = "#4F2D23", Muted = "#8A6557", Accent = "#B53F31", Detail = "#B53F31" },
                DarkPal  = new SkinPalette { Bg1 = "#3D1D1C", Bg2 = "#2A1212", Text = "#FAF0E2", Muted = "#D4AFA3", Accent = "#F0C577", Detail = "#F0C577" },
                Oc1 = "#F1E3D1", Oc2 = "#E4C9AD", Glow1 = "#FFF9F2", Glow2 = "#D1866E"
            },
            new SkinSpec {
                Id = "midautumn", Label = "中秋 · 月泊", Subtitle = "澄澈秋夕与素月流金", Seal = "秋夕", LightDefault = false,
                LightPal = new SkinPalette { Bg1 = "#F5F6FA", Bg2 = "#E7EAF3", Text = "#212840", Muted = "#656D89", Accent = "#A87224", Detail = "#A87224" },
                DarkPal  = new SkinPalette { Bg1 = "#242B42", Bg2 = "#191F33", Text = "#F1E9D7", Muted = "#ADB5C9", Accent = "#E5CE93", Detail = "#E5CE93" },
                Oc1 = "#D9DFEB", Oc2 = "#EAE3D7", Glow1 = "#FFFFFF", Glow2 = "#A3B0CC"
            },
            new SkinSpec {
                Id = "national", Label = "国庆 · 朱锦", Subtitle = "赤金锦缎与盛世华章", Seal = "华诞", LightDefault = false,
                LightPal = new SkinPalette { Bg1 = "#FCF3F1", Bg2 = "#F4E2DF", Text = "#4D1E1C", Muted = "#8B5B57", Accent = "#B33329", Detail = "#B33329" },
                DarkPal  = new SkinPalette { Bg1 = "#612422", Bg2 = "#471918", Text = "#FFF1DC", Muted = "#DFB7AC", Accent = "#F1D28E", Detail = "#F1D28E" },
                Oc1 = "#EFE2D7", Oc2 = "#E3C7BA", Glow1 = "#FFF8F2", Glow2 = "#C87A6D"
            }
        };

        public static readonly string[] EventIconIds = { "auto", "star", "cake", "plane", "heart", "briefcase", "book", "coin", "music", "home", "sparkle", "tree", "lantern", "moon", "flag" };

        static readonly Dictionary<string, string> EventIconLabels = new Dictionary<string, string> {
            { "auto", "智能匹配" }, { "star", "星标" }, { "cake", "生日" }, { "plane", "旅行" },
            { "heart", "纪念" }, { "briefcase", "工作" }, { "book", "考试" }, { "coin", "发薪" },
            { "music", "演出" }, { "home", "归家" }, { "sparkle", "新岁" }, { "tree", "圣诞" },
            { "lantern", "春节" }, { "moon", "中秋" }, { "flag", "国庆" }
        };

        public static string EventIconLabel(string id)
        {
            string lbl;
            return EventIconLabels.TryGetValue(id ?? "auto", out lbl) ? lbl : "星标";
        }

        public static string EventIconPathData(string iconId)
        {
            switch (iconId)
            {
                case "cake":
                    return "M3,12.5 L13,12.5 M4,12.5 L4,8.5 C4,7.7 4.7,7 5.5,7 L10.5,7 C11.3,7 12,7.7 12,8.5 L12,12.5 M8,7 L8,4.8 M8,2.5 C7.3,3.3 7.3,4.2 8,4.6 C8.7,4.2 8.7,3.3 8,2.5 Z";
                case "plane":
                    return "M13.5,2.5 L2,7.2 L6.5,8.8 L10.8,5.2 L7.8,9.8 L9.6,13.8 L13.5,2.5 Z";
                case "heart":
                    return "M8,13.2 C8,13.2 2.5,9.6 2.5,5.8 C2.5,3.9 3.9,2.6 5.6,2.6 C6.7,2.6 7.5,3.2 8,4.0 C8.5,3.2 9.3,2.6 10.4,2.6 C12.1,2.6 13.5,3.9 13.5,5.8 C13.5,9.6 8,13.2 8,13.2 Z";
                case "briefcase":
                    return "M2.5,6 C2.5,5.2 3.2,4.5 4,4.5 L12,4.5 C12.8,4.5 13.5,5.2 13.5,6 L13.5,11.8 C13.5,12.6 12.8,13.3 12,13.3 L4,13.3 C3.2,13.3 2.5,12.6 2.5,11.8 Z M5.8,4.5 L5.8,3.2 C5.8,2.6 6.2,2.2 6.8,2.2 L9.2,2.2 C9.8,2.2 10.2,2.6 10.2,3.2 L10.2,4.5 M2.5,8.5 L13.5,8.5";
                case "book":
                    return "M2.5,3.5 C4.5,3.0 6.5,3.3 8,4.4 C9.5,3.3 11.5,3.0 13.5,3.5 L13.5,12.5 C11.5,12.0 9.5,12.3 8,13.3 C6.5,12.3 4.5,12.0 2.5,12.5 Z M8,4.4 L8,13.3";
                case "coin":
                    return "M8,2.2 A5.8,5.8 0 1,0 8,13.8 A5.8,5.8 0 1,0 8,2.2 Z M6,6.2 L8,8.0 L10,6.2 M8,8.0 L8,11.2 M6.2,9.2 L9.8,9.2";
                case "music":
                    return "M6,11.8 A1.7,1.7 0 1,0 6,11.81 Z M12.2,10.6 A1.7,1.7 0 1,0 12.2,10.61 Z M7.7,11.5 L7.7,3.6 L13.8,2.4 L13.8,10.3";
                case "home":
                    return "M2.2,7.5 L8,2.5 L13.8,7.5 M4,6.5 L4,13.2 L12,13.2 L12,6.5 M6.8,13.2 L6.8,9.5 L9.2,9.5 L9.2,13.2";
                case "sparkle":
                    return "M8,1.8 L9.4,6.4 L14,8 L9.4,9.6 L8,14.2 L6.6,9.6 L2,8 L6.6,6.4 Z";
                case "tree":
                    return "M8,2 L4.5,6.8 L6.5,6.8 L3.5,11.2 L12.5,11.2 L9.5,6.8 L11.5,6.8 Z M8,11.2 L8,14";
                case "lantern":
                    return "M8,1.5 L8,3.2 M5.5,3.4 L10.5,3.4 M5.5,11.8 L10.5,11.8 M8,12 L8,14.5 M8,3.6 C3.2,3.6 3.2,11.6 8,11.6 C12.8,11.6 12.8,3.6 8,3.6 Z";
                case "moon":
                    return "M11.6,10.4 A5.2,5.2 0 1,1 7.2,2.4 A4.1,4.1 0 0,0 11.6,10.4 Z";
                case "flag":
                    return "M3.5,14 L3.5,2.2 M3.5,3 C6.5,1.8 9.5,4.2 12.8,3 L12.8,9.5 C9.5,10.7 6.5,8.3 3.5,9.5";
                default: // star
                    return "M8,2.1 L9.7,5.8 L13.8,6.2 L10.7,8.9 L11.6,13 L8,10.9 L4.4,13 L5.3,8.9 L2.2,6.2 L6.3,5.8 Z";
            }
        }

        public static FrameworkElement BuildEventIcon(string iconId, double size, Brush strokeBrush, double strokeWidth = 1.35)
        {
            var vb = new Viewbox { Width = size, Height = size, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };
            var canvas = new Canvas { Width = 16, Height = 16 };
            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(EventIconPathData(iconId)),
                Stroke = strokeBrush,
                StrokeThickness = strokeWidth,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            canvas.Children.Add(path);
            vb.Child = canvas;
            return vb;
        }

        public static FrameworkElement BuildVectorIcon(string pathData, double size, string brushResourceKey, double strokeWidth = 1.45)
        {
            var vb = new Viewbox { Width = size, Height = size, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };
            var canvas = new Canvas { Width = 16, Height = 16 };
            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(pathData),
                StrokeThickness = strokeWidth,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            path.SetResourceReference(Shape.StrokeProperty, brushResourceKey);
            canvas.Children.Add(path);
            vb.Child = canvas;
            return vb;
        }

        // Procedural tactile micro paper grain texture (zero external asset, cached once)
        static ImageBrush _grainLight, _grainDark;

        public static Brush PaperGrainBrush(bool light)
        {
            if (light && _grainLight != null) return _grainLight;
            if (!light && _grainDark != null) return _grainDark;

            const int size = 96;
            var wb = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
            byte[] pixels = new byte[size * size * 4];
            uint seed = light ? 0x51A7C391u : 0x8F2B4D17u;
            for (int i = 0; i < size * size; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                byte v = (byte)((seed >> 24) & 0xFF);
                bool darkSpeck = (v & 1) == 0;
                byte alpha = (byte)(v % 12); // ultra subtle 0..11 / 255 (~2.5% average)
                int off = i * 4;
                if (light)
                {
                    byte col = darkSpeck ? (byte)55 : (byte)255;
                    pixels[off + 0] = col;
                    pixels[off + 1] = col;
                    pixels[off + 2] = col;
                    pixels[off + 3] = alpha;
                }
                else
                {
                    byte col = darkSpeck ? (byte)255 : (byte)15;
                    pixels[off + 0] = col;
                    pixels[off + 1] = col;
                    pixels[off + 2] = col;
                    pixels[off + 3] = (byte)(alpha * 0.85);
                }
            }
            wb.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);
            wb.Freeze();

            var ib = new ImageBrush(wb)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, size, size),
                ViewportUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.None
            };
            ib.Freeze();
            if (light) _grainLight = ib; else _grainDark = ib;
            return ib;
        }

        // Dynamic palette adapting subtly to active skin accent
        static string ActiveAccentHex(bool light)
        {
            string sid = ResolveSkinId();
            if (sid == "default" || sid == "minimal") return light ? "#3A6B9F" : "#78B2EA";
            return PickSkinPalette(sid, light).Accent;
        }

        static void SetPalette(FrameworkElement view, bool light, bool micaWindow = false)
        {
            string accent = ActiveAccentHex(light);
            string[] keys = { "Canvas", "Surface", "SurfaceHover", "Field", "Ink", "Muted", "Line", "Accent", "AccentSoft", "Danger", "Gold" };
            string[] colors = light
                ? new[] {
                    micaWindow ? "#D6F6F5F2" : "#F5F4F1",
                    micaWindow ? "#E8FFFFFF" : "#FFFFFF",
                    "#EBEAE6",
                    "#F1F0EC",
                    "#1F242B",
                    "#6D7580",
                    "#E2E0DA",
                    accent,
                    HexA("1C", accent),
                    "#C24B3E",
                    "#C6892B"
                }
                : new[] {
                    micaWindow ? "#CC171A20" : "#171A20",
                    micaWindow ? "#B8222730" : "#21262F",
                    "#2D3440",
                    "#292F3A",
                    "#F0F3F8",
                    "#929DAB",
                    "#313845",
                    accent,
                    HexA("28", accent),
                    "#E07265",
                    "#E5C478"
                };
            for (int i = 0; i < keys.Length; i++) view.Resources[keys[i]] = Brush(colors[i]);
        }

        static FontFamily ResolveWidgetNumberFont()
        {
            string fs = (Config.font_style ?? "modern").ToLowerInvariant();
            if (fs == "editorial") return new FontFamily("Source Han Serif SC, Noto Serif SC, STSong, Georgia, Segoe UI");
            if (fs == "mono") return new FontFamily("Consolas, Cascadia Mono, Bahnschrift, Segoe UI");
            return WidgetNumFont;
        }

        // Native Win11 Mica Window with true native shadow, rounded corners, and resize
        static DockPanel CreateStudioChrome(string title, double width, double height, bool light, Window owner, bool resizable, out Window win, string headerLabel, out Border rootFrame, out Action<bool> updateTheme)
        {
            var w = new Window
            {
                Title = title,
                Width = width,
                Height = Math.Min(height, SystemParameters.WorkArea.Height - 40),
                MinWidth = resizable ? 800 : width,
                MinHeight = resizable ? 560 : Math.Min(height, SystemParameters.WorkArea.Height - 40),
                WindowStyle = WindowStyle.SingleBorderWindow,
                ResizeMode = resizable ? ResizeMode.CanResize : ResizeMode.NoResize,
                AllowsTransparency = false,
                Background = Brushes.Transparent,
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
                Owner = owner,
                Topmost = false,
                ShowInTaskbar = owner == null,
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 12,
                UseLayoutRounding = true,
                Icon = Logo()
            };

            WindowChrome.SetWindowChrome(w, new WindowChrome
            {
                CaptionHeight = 36,
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(-1),
                ResizeBorderThickness = resizable ? new Thickness(6) : new Thickness(0),
                UseAeroCaptionButtons = true
            });

            Theme(w, light);

            var frame = new Border { BorderThickness = new Thickness(0), Background = Brushes.Transparent };
            w.Content = frame;

            bool micaActive = false;
            Action<bool> applyTheme = isLight =>
            {
                var handle = new WindowInteropHelper(w).Handle;
                if (handle != IntPtr.Zero)
                {
                    micaActive = ApplyWin11Backdrop(w, handle, isLight, 2 /* Mica */);
                }
                SetPalette(w, isLight, micaActive);
                if (!micaActive)
                {
                    w.SetResourceReference(Window.BackgroundProperty, "Canvas");
                    frame.SetResourceReference(Border.BackgroundProperty, "Canvas");
                }
                else
                {
                    w.Background = Brushes.Transparent;
                    frame.Background = Brushes.Transparent;
                }
            };

            w.SourceInitialized += (s, e) => applyTheme(light);

            var outer = new DockPanel();
            frame.Child = outer;

            // Transparent native titlebar strip: right 146px reserved for crisp native Win11 caption buttons (zero overlap!)
            var chrome = new DockPanel { Height = 36, Margin = new Thickness(16, 0, 146, 0), Background = Brushes.Transparent };
            DockPanel.SetDock(chrome, Dock.Top);
            outer.Children.Add(chrome);

            var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            brand.Children.Add(new Image { Source = Logo(), Width = 17, Height = 17, Margin = new Thickness(0, 0, 8, 0) });
            var lbl = Label(headerLabel ?? title, 12, "Muted");
            lbl.FontWeight = FontWeights.Medium;
            brand.Children.Add(lbl);
            chrome.Children.Add(brand);

            w.StateChanged += (s, e) =>
            {
                frame.Padding = w.WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
            };

            win = w;
            rootFrame = frame;
            updateTheme = applyTheme;
            return outer;
        }

        // Custom Zero-Dependency Solar + Lunar Calendar Picker Popup
        static void OpenCalendarPicker(UIElement anchor, bool light, CfgItem current, Action<string, bool, int, int, bool> onApply, Action onClosed = null)
        {
            var popup = new Popup
            {
                PlacementTarget = anchor,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                VerticalOffset = 6
            };
            if (onClosed != null) popup.Closed += (s, e) => onClosed();

            var shell = new Border
            {
                Width = 312,
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14),
                Margin = new Thickness(12),
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 22, ShadowDepth = 6, Direction = 270, Opacity = light ? 0.16 : 0.42 }
            };
            Theme(shell, light);
            shell.SetResourceReference(Border.BackgroundProperty, "Surface");
            shell.SetResourceReference(Border.BorderBrushProperty, "Line");
            popup.Child = shell;

            bool isLunarMode = string.Equals(current.calendar, "lunar", StringComparison.OrdinalIgnoreCase);
            DateTime? parsed = ParseDate(current.date);
            DateTime selectedDate = parsed.HasValue ? parsed.Value.Date : DateTime.Today.AddDays(30);
            int selHour = parsed.HasValue ? parsed.Value.Hour : 0;
            int selMin = parsed.HasValue ? parsed.Value.Minute : 0;
            int viewYear = selectedDate.Year;
            int viewMonth = selectedDate.Month;
            int lunarM = current.lunar_month >= 1 && current.lunar_month <= 12 ? current.lunar_month : 1;
            int lunarD = current.lunar_day >= 1 && current.lunar_day <= 30 ? current.lunar_day : 1;
            bool lunarLeap = current.lunar_leap;

            if (!isLunarMode && current.lunar_month == 0)
            {
                int ly;
                LunarHelper.SolarToLunar(selectedDate, out ly, out lunarM, out lunarD, out lunarLeap);
            }

            var root = new StackPanel();
            shell.Child = root;

            // Mode switcher: 公历 | 农历
            var modeWrap = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(3), Margin = new Thickness(0, 0, 0, 12) };
            modeWrap.SetResourceReference(Border.BackgroundProperty, "Field");
            var modeGrid = new Grid();
            modeGrid.ColumnDefinitions.Add(new ColumnDefinition());
            modeGrid.ColumnDefinitions.Add(new ColumnDefinition());
            modeWrap.Child = modeGrid;
            root.Children.Add(modeWrap);

            var solarTab = new Button { Content = "公历日历", Padding = new Thickness(8, 6, 8, 6), BorderThickness = new Thickness(0), FontSize = 11.5 };
            var lunarTab = new Button { Content = "中国农历", Padding = new Thickness(8, 6, 8, 6), BorderThickness = new Thickness(0), FontSize = 11.5 };
            Grid.SetColumn(solarTab, 0); Grid.SetColumn(lunarTab, 1);
            modeGrid.Children.Add(solarTab); modeGrid.Children.Add(lunarTab);

            var solarPanel = new StackPanel();
            var lunarPanel = new StackPanel();
            root.Children.Add(solarPanel);
            root.Children.Add(lunarPanel);

            // --- Solar Panel ---
            var nav = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            solarPanel.Children.Add(nav);
            var prevY = ActionButton("«", () => { }); prevY.Padding = new Thickness(7, 3, 7, 3); prevY.Background = Brushes.Transparent; prevY.BorderThickness = new Thickness(0);
            var prevM = ActionButton("‹", () => { }); prevM.Padding = new Thickness(7, 3, 7, 3); prevM.Background = Brushes.Transparent; prevM.BorderThickness = new Thickness(0);
            var nextY = ActionButton("»", () => { }); nextY.Padding = new Thickness(7, 3, 7, 3); nextY.Background = Brushes.Transparent; nextY.BorderThickness = new Thickness(0);
            var nextM = ActionButton("›", () => { }); nextM.Padding = new Thickness(7, 3, 7, 3); nextM.Background = Brushes.Transparent; nextM.BorderThickness = new Thickness(0);
            DockPanel.SetDock(prevY, Dock.Left); DockPanel.SetDock(prevM, Dock.Left);
            DockPanel.SetDock(nextY, Dock.Right); DockPanel.SetDock(nextM, Dock.Right);
            nav.Children.Add(prevY); nav.Children.Add(prevM); nav.Children.Add(nextY); nav.Children.Add(nextM);
            var monthTitle = Label("", 12.5, "Ink");
            monthTitle.FontWeight = FontWeights.SemiBold;
            monthTitle.HorizontalAlignment = HorizontalAlignment.Center;
            nav.Children.Add(monthTitle);

            var weekRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            string[] wk = { "一", "二", "三", "四", "五", "六", "日" };
            for (int i = 0; i < 7; i++)
            {
                weekRow.ColumnDefinitions.Add(new ColumnDefinition());
                var wt = Label(wk[i], 10, "Muted");
                wt.HorizontalAlignment = HorizontalAlignment.Center;
                Grid.SetColumn(wt, i);
                weekRow.Children.Add(wt);
            }
            solarPanel.Children.Add(weekRow);

            var dayGrid = new Grid { Height = 204 };
            for (int c = 0; c < 7; c++) dayGrid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int r = 0; r < 6; r++) dayGrid.RowDefinitions.Add(new RowDefinition());
            solarPanel.Children.Add(dayGrid);

            // --- Lunar Panel ---
            var lunarRow = new Grid { Margin = new Thickness(0, 4, 0, 10) };
            lunarRow.ColumnDefinitions.Add(new ColumnDefinition());
            lunarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            lunarRow.ColumnDefinitions.Add(new ColumnDefinition());
            lunarPanel.Children.Add(lunarRow);

            var lmBox = new ComboBox();
            string[] mNames = { "正月", "二月", "三月", "四月", "五月", "六月", "七月", "八月", "九月", "十月", "冬月", "腊月" };
            for (int i = 0; i < 12; i++) lmBox.Items.Add(mNames[i]);
            lmBox.SelectedIndex = Math.Max(0, Math.Min(11, lunarM - 1));
            Grid.SetColumn(lmBox, 0); lunarRow.Children.Add(lmBox);

            var ldBox = new ComboBox();
            string[] dNames = { "初一", "初二", "初三", "初四", "初五", "初六", "初七", "初八", "初九", "初十",
                                "十一", "十二", "十三", "十四", "十五", "十六", "十七", "十八", "十九", "二十",
                                "廿一", "廿二", "廿三", "廿四", "廿五", "廿六", "廿七", "廿八", "廿九", "三十" };
            for (int i = 0; i < 30; i++) ldBox.Items.Add(dNames[i]);
            ldBox.SelectedIndex = Math.Max(0, Math.Min(29, lunarD - 1));
            Grid.SetColumn(ldBox, 2); lunarRow.Children.Add(ldBox);

            var leapChk = new CheckBox { Content = "闰月（仅闰年该月生效）", IsChecked = lunarLeap, Margin = new Thickness(0, 0, 0, 10), FontSize = 11 };
            lunarPanel.Children.Add(leapChk);

            var lunarPreview = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 8) };
            lunarPreview.SetResourceReference(Border.BackgroundProperty, "Field");
            var lunarPreviewText = Label("", 11.5, "Accent");
            lunarPreview.Child = lunarPreviewText;
            lunarPanel.Children.Add(lunarPreview);

            // --- Shared Time & Footer Row ---
            var timeRow = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
            root.Children.Add(timeRow);
            var timeLbl = Label("时刻", 11, "Muted");
            timeLbl.Margin = new Thickness(0, 0, 8, 0);
            DockPanel.SetDock(timeLbl, Dock.Left); timeRow.Children.Add(timeLbl);

            var hourBox = new TextBox { Text = selHour.ToString("00"), Width = 42, Height = 28, TextAlignment = TextAlignment.Center, MaxLength = 2 };
            hourBox.SetResourceReference(Control.BackgroundProperty, "Field");
            hourBox.SetResourceReference(Control.BorderBrushProperty, "Line");
            var colon = Label(":", 12, "Muted"); colon.Margin = new Thickness(4, 0, 4, 0);
            var minBox = new TextBox { Text = selMin.ToString("00"), Width = 42, Height = 28, TextAlignment = TextAlignment.Center, MaxLength = 2 };
            minBox.SetResourceReference(Control.BackgroundProperty, "Field");
            minBox.SetResourceReference(Control.BorderBrushProperty, "Line");
            DockPanel.SetDock(hourBox, Dock.Left); DockPanel.SetDock(colon, Dock.Left); DockPanel.SetDock(minBox, Dock.Left);
            timeRow.Children.Add(hourBox); timeRow.Children.Add(colon); timeRow.Children.Add(minBox);

            var okBtn = ActionButton("确定", () => { });
            okBtn.SetResourceReference(Button.BackgroundProperty, "Accent");
            okBtn.Foreground = Brushes.White; okBtn.BorderThickness = new Thickness(0);
            okBtn.Padding = new Thickness(14, 5, 14, 5);
            DockPanel.SetDock(okBtn, Dock.Right); timeRow.Children.Add(okBtn);

            var todayBtn = ActionButton("今天", () => { });
            todayBtn.Padding = new Thickness(10, 5, 10, 5);
            todayBtn.Margin = new Thickness(0, 0, 6, 0);
            todayBtn.Background = Brushes.Transparent;
            DockPanel.SetDock(todayBtn, Dock.Right); timeRow.Children.Add(todayBtn);

            Action refreshLunarPreview = () =>
            {
                int m = lmBox.SelectedIndex + 1;
                int d = ldBox.SelectedIndex + 1;
                bool lp = leapChk.IsChecked == true;
                DateTime prev;
                DateTime next = LunarHelper.NextLunarOccurrence(m, d, lp, DateTime.Now, TimeSpan.Zero, out prev);
                lunarPreviewText.Text = "下一次对应公历：" + next.ToString("yyyy-MM-dd") + " (" + WeekdayZhShort(next) + ")";
            };

            Action renderDays = null;
            renderDays = () =>
            {
                monthTitle.Text = viewYear + " 年 " + viewMonth + " 月";
                dayGrid.Children.Clear();
                var first = new DateTime(viewYear, viewMonth, 1);
                int startOffset = ((int)first.DayOfWeek + 6) % 7; // Monday = 0
                DateTime cursor = first.AddDays(-startOffset);
                var today = DateTime.Today;

                for (int idx = 0; idx < 42; idx++)
                {
                    DateTime cellDate = cursor.AddDays(idx);
                    bool inMonth = cellDate.Month == viewMonth;
                    bool isSel = cellDate == selectedDate;
                    bool isToday = cellDate == today;

                    var cell = new Border
                    {
                        CornerRadius = new CornerRadius(6),
                        Margin = new Thickness(1.5),
                        Cursor = Cursors.Hand,
                        BorderThickness = new Thickness(isToday && !isSel ? 1 : 0)
                    };
                    if (isSel) cell.SetResourceReference(Border.BackgroundProperty, "Accent");
                    else cell.Background = Brushes.Transparent;
                    if (isToday && !isSel) cell.SetResourceReference(Border.BorderBrushProperty, "Accent");

                    var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                    var dNum = new TextBlock
                    {
                        Text = cellDate.Day.ToString(),
                        FontSize = 11.5,
                        FontWeight = isSel || isToday ? FontWeights.SemiBold : FontWeights.Normal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Opacity = inMonth ? 1.0 : 0.32
                    };
                    if (isSel) dNum.Foreground = Brushes.White;
                    else dNum.SetResourceReference(TextBlock.ForegroundProperty, "Ink");

                    string lunarCell = LunarHelper.SolarToShortLunarCell(cellDate);
                    bool isFest = lunarCell == "春节" || lunarCell == "元宵" || lunarCell == "端午" || lunarCell == "七夕" || lunarCell == "中秋" || lunarCell == "重阳";
                    var lTxt = new TextBlock
                    {
                        Text = lunarCell,
                        FontSize = 8.5,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Opacity = inMonth ? (isSel ? 0.9 : 0.8) : 0.25
                    };
                    if (isSel) lTxt.Foreground = Brushes.White;
                    else lTxt.SetResourceReference(TextBlock.ForegroundProperty, isFest ? "Accent" : "Muted");

                    sp.Children.Add(dNum);
                    sp.Children.Add(lTxt);
                    cell.Child = sp;

                    DateTime captured = cellDate;
                    cell.MouseEnter += (s, e) => { if (captured != selectedDate) cell.SetResourceReference(Border.BackgroundProperty, "Field"); };
                    cell.MouseLeave += (s, e) => { if (captured != selectedDate) cell.Background = Brushes.Transparent; };
                    cell.MouseLeftButtonUp += (s, e) =>
                    {
                        selectedDate = captured;
                        viewYear = captured.Year;
                        viewMonth = captured.Month;
                        renderDays();
                    };

                    Grid.SetRow(cell, idx / 7);
                    Grid.SetColumn(cell, idx % 7);
                    dayGrid.Children.Add(cell);
                }
            };

            Action syncTabs = () =>
            {
                solarPanel.Visibility = isLunarMode ? Visibility.Collapsed : Visibility.Visible;
                lunarPanel.Visibility = isLunarMode ? Visibility.Visible : Visibility.Collapsed;
                todayBtn.Visibility = isLunarMode ? Visibility.Collapsed : Visibility.Visible;
                if (!isLunarMode)
                {
                    solarTab.SetResourceReference(Button.BackgroundProperty, "Surface");
                    solarTab.FontWeight = FontWeights.SemiBold;
                    lunarTab.Background = Brushes.Transparent;
                    lunarTab.FontWeight = FontWeights.Normal;
                    renderDays();
                }
                else
                {
                    lunarTab.SetResourceReference(Button.BackgroundProperty, "Surface");
                    lunarTab.FontWeight = FontWeights.SemiBold;
                    solarTab.Background = Brushes.Transparent;
                    solarTab.FontWeight = FontWeights.Normal;
                    refreshLunarPreview();
                }
            };

            solarTab.Click += (s, e) => { isLunarMode = false; syncTabs(); };
            lunarTab.Click += (s, e) => { isLunarMode = true; syncTabs(); };
            prevY.Click += (s, e) => { viewYear--; renderDays(); };
            nextY.Click += (s, e) => { viewYear++; renderDays(); };
            prevM.Click += (s, e) => { var dt = new DateTime(viewYear, viewMonth, 1).AddMonths(-1); viewYear = dt.Year; viewMonth = dt.Month; renderDays(); };
            nextM.Click += (s, e) => { var dt = new DateTime(viewYear, viewMonth, 1).AddMonths(1); viewYear = dt.Year; viewMonth = dt.Month; renderDays(); };
            todayBtn.Click += (s, e) => { selectedDate = DateTime.Today; viewYear = selectedDate.Year; viewMonth = selectedDate.Month; renderDays(); };
            lmBox.SelectionChanged += (s, e) => refreshLunarPreview();
            ldBox.SelectionChanged += (s, e) => refreshLunarPreview();
            leapChk.Checked += (s, e) => refreshLunarPreview();
            leapChk.Unchecked += (s, e) => refreshLunarPreview();

            okBtn.Click += (s, e) =>
            {
                int hh, mm;
                if (!int.TryParse(hourBox.Text, out hh) || hh < 0 || hh > 23) hh = 0;
                if (!int.TryParse(minBox.Text, out mm) || mm < 0 || mm > 59) mm = 0;
                if (isLunarMode)
                {
                    int m = lmBox.SelectedIndex + 1;
                    int d = ldBox.SelectedIndex + 1;
                    bool lp = leapChk.IsChecked == true;
                    DateTime prev;
                    DateTime next = LunarHelper.NextLunarOccurrence(m, d, lp, DateTime.Now, new TimeSpan(hh, mm, 0), out prev);
                    onApply(next.ToString("yyyy-MM-dd HH:mm:ss"), true, m, d, lp);
                }
                else
                {
                    DateTime result = selectedDate.Date.Add(new TimeSpan(hh, mm, 0));
                    onApply(result.ToString("yyyy-MM-dd HH:mm:ss"), false, 0, 0, false);
                }
                popup.IsOpen = false;
            };

            syncTabs();
            popup.IsOpen = true;
        }

        static string WeekdayZhShort(DateTime d)
        {
            string[] w = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            return w[(int)d.DayOfWeek];
        }
    }
}
