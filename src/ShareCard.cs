using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CountdownWidget
{
    public static partial class Program
    {
        public sealed class ShareEntry
        {
            public string Name;
            public DateTime Date;
            public DateTime StartDate;
            public bool Enabled;
            public bool IsCountup;
            public string LunarLabel;
            public string IconId;
        }

        public sealed class CardOpts
        {
            public bool IsList;
            public int EntryIndex;
            public int TemplateIndex; // 0=松霜冬序, 1=流光极夜, 2=日落琥珀, 3=宣纸墨韵, 4=黑胶琥珀, 5=拍立得
            public int RatioIndex;    // 0=3:4, 1=1:1, 2=16:9, 3=9:19.5 Lockscreen
            public int SkinIndex;     // kept for backward compatibility
            public string Quote;
            public bool ShowDate = true;
            public bool ShowWatermark = true;
            public bool PaperTexture = true;
            public bool RoundedCorners = false;
            public string PhotoPath;
        }

        sealed class ArtTheme
        {
            public string Id, Name, Tag, Seal;
            public string BgTop, BgMid, BgBot;
            public string SwatchA, SwatchB;
            public string Surface, SurfaceStrong, Hairline, Track;
            public string Ink, Muted, Accent, PillBg, PillFg;
            public bool IsLight;
        }

        static readonly ArtTheme[] CuratedThemes = {
            new ArtTheme {
                Id = "pine", Name = "松霜 · 冬序", Tag = "经典深林暖金", Seal = "冬序",
                BgTop = "#1C382F", BgMid = "#162E26", BgBot = "#10221C",
                SwatchA = "#1E3D33", SwatchB = "#D4B672",
                Surface = "#1CFFFFFF", SurfaceStrong = "#28FFFFFF", Hairline = "#24FFFFFF", Track = "#22FFFFFF",
                Ink = "#F6F0E2", Muted = "#A7BFB2", Accent = "#E8CE90", PillBg = "#2BE8CE90", PillFg = "#F3DEA8",
                IsLight = false
            },
            new ArtTheme {
                Id = "aurora", Name = "流光 · 极夜", Tag = "深邃极光氛围", Seal = "极光",
                BgTop = "#0C1426", BgMid = "#12213B", BgBot = "#09101E",
                SwatchA = "#0F1E3A", SwatchB = "#38C8BD",
                Surface = "#18FFFFFF", SurfaceStrong = "#24FFFFFF", Hairline = "#22FFFFFF", Track = "#20FFFFFF",
                Ink = "#F2F7FC", Muted = "#90A8C4", Accent = "#63E6D8", PillBg = "#2863E6D8", PillFg = "#8FF2E6",
                IsLight = false
            },
            new ArtTheme {
                Id = "sunset", Name = "日落 · 琥珀", Tag = "电影暮色渐变", Seal = "暮色",
                BgTop = "#26162E", BgMid = "#753042", BgBot = "#C76B45",
                SwatchA = "#5D2640", SwatchB = "#E89352",
                Surface = "#1CFFFFFF", SurfaceStrong = "#28FFFFFF", Hairline = "#28FFFFFF", Track = "#26FFFFFF",
                Ink = "#FFF8EF", Muted = "#EFC9B7", Accent = "#FFDC94", PillBg = "#2EFFDC94", PillFg = "#FFE8B8",
                IsLight = false
            },
            new ArtTheme {
                Id = "xuan", Name = "宣纸 · 墨韵", Tag = "温润纸感朱砂", Seal = "墨韵",
                BgTop = "#F9F5EB", BgMid = "#F3ECDD", BgBot = "#ECE2CE",
                SwatchA = "#F6EFE0", SwatchB = "#B53528",
                Surface = "#B8FFFFFF", SurfaceStrong = "#E6FFFFFF", Hairline = "#1E2B221A", Track = "#1A2B221A",
                Ink = "#231E1A", Muted = "#7A6E62", Accent = "#B33428", PillBg = "#1CB33428", PillFg = "#A32A1F",
                IsLight = true
            },
            new ArtTheme {
                Id = "vinyl", Name = "黑胶 · 琥珀", Tag = "暗房复古辉光", Seal = "黑胶",
                BgTop = "#1C1A18", BgMid = "#161413", BgBot = "#100F0E",
                SwatchA = "#1E1C19", SwatchB = "#E09242",
                Surface = "#16FFFFFF", SurfaceStrong = "#22FFFFFF", Hairline = "#20FFFFFF", Track = "#20FFFFFF",
                Ink = "#F6EFE3", Muted = "#A09588", Accent = "#F0A44B", PillBg = "#28F0A44B", PillFg = "#F7BE74",
                IsLight = false
            },
            new ArtTheme {
                Id = "polaroid", Name = "拍立得 · 记忆", Tag = "支持拖入照片", Seal = "留影",
                BgTop = "#FAF6EE", BgMid = "#F5EFE3", BgBot = "#EFE7D8",
                SwatchA = "#445778", SwatchB = "#F5EFE3",
                Surface = "#CCFFFFFF", SurfaceStrong = "#EBFFFFFF", Hairline = "#1C241E18", Track = "#18241E18",
                Ink = "#221E1A", Muted = "#7B7065", Accent = "#C45732", PillBg = "#1CC45732", PillFg = "#B44B27",
                IsLight = true
            }
        };

        static readonly FontFamily SansFont = new FontFamily("Microsoft YaHei UI, Segoe UI");
        static readonly FontFamily SerifFont = new FontFamily("Source Han Serif SC, Noto Serif SC, STSong, SimSun, Georgia");
        static readonly FontFamily NumFont = new FontFamily("Bahnschrift, DIN Alternate, Segoe UI");
        static readonly FontFamily MonoFont = new FontFamily("Consolas, Cascadia Mono, Segoe UI");

        static ShareEntry ToShareEntry(ResolvedItem r)
        {
            if (r == null) return null;
            return new ShareEntry
            {
                Name = r.Name,
                Date = r.TargetDate,
                StartDate = r.CycleStartDate,
                Enabled = r.Source == null || r.Source.enabled,
                IsCountup = r.IsCountup,
                LunarLabel = r.LunarDisplay,
                IconId = r.ResolvedIcon
            };
        }

        static string WeekdayFullZh(DateTime d)
        {
            string[] w = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
            return w[(int)d.DayOfWeek];
        }

        static string WeekdayShortZh(DateTime d)
        {
            string[] w = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            return w[(int)d.DayOfWeek];
        }

        static string MonthEn(int m)
        {
            string[] names = { "", "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
            return m >= 1 && m <= 12 ? names[m] : "";
        }

        static int IsoWeek(DateTime d)
        {
            var cal = CultureInfo.InvariantCulture.Calendar;
            return cal.GetWeekOfYear(d, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        static string RelativeSpanTag(ShareEntry e, DateTime now)
        {
            if (e.IsCountup)
            {
                int days = Math.Max(1, (now.Date - e.Date.Date).Days + 1);
                if (days >= 365) return "已相伴 " + (days / 365) + " 年+";
                return "纪念正数";
            }
            if (e.Date <= now) return "已抵达";
            int d = (e.Date.Date - now.Date).Days;
            if (d == 0) return "就是今天";
            if (d == 1) return "就在明天";
            if (d < 7) return "就在本周";
            if (d < 28) return "约 " + Math.Max(1, (int)Math.Round(d / 7.0)) + " 周后";
            return "约 " + Math.Max(1, (int)Math.Round(d / 30.0)) + " 个月后";
        }

        static string CompactWeekText(ShareEntry e, DateTime now)
        {
            int d = e.IsCountup ? Math.Max(1, (now.Date - e.Date.Date).Days + 1) : Math.Max(0, (e.Date.Date - now.Date).Days);
            int w = d / 7, rem = d % 7;
            if (w == 0) return d + " 天";
            return rem == 0 ? (w + " 周") : (w + " 周 " + rem + " 天");
        }

        static string EntryDayNumber(ShareEntry e, DateTime now, out bool isNum, out string unit)
        {
            if (e.IsCountup)
            {
                int elapsed = Math.Max(1, (now.Date - e.Date.Date).Days + 1);
                isNum = true; unit = "天";
                return elapsed.ToString(CultureInfo.InvariantCulture);
            }
            if (e.Date <= now) { isNum = false; unit = ""; return "已抵达"; }
            int d = (e.Date.Date - now.Date).Days;
            if (d == 0) { isNum = false; unit = ""; return "今天"; }
            isNum = true; unit = "天";
            return d.ToString(CultureInfo.InvariantCulture);
        }

        static List<ShareEntry> OrderEntries(List<ShareEntry> entries, DateTime now, out int maxDays)
        {
            var list = entries
                .OrderBy(e => e.IsCountup ? 1 : (e.Date <= now ? 2 : 0))
                .ThenBy(e => e.IsCountup ? -Math.Abs((now.Date - e.Date.Date).Days) : (e.Date.Date - now.Date).Days)
                .ThenBy(e => e.Date)
                .ToList();
            maxDays = 1;
            foreach (var e in list)
            {
                if (!e.IsCountup && e.Date > now)
                    maxDays = Math.Max(maxDays, (e.Date.Date - now.Date).Days);
            }
            return list;
        }

        // Comparative closeness bar for list cards: closer upcoming event => fuller glowing bar!
        static double ListProgressRatio(ShareEntry e, DateTime now, int maxDays)
        {
            if (e.IsCountup) return 0.78;
            if (e.Date <= now) return 1.0;
            int d = Math.Max(0, (e.Date.Date - now.Date).Days);
            if (d == 0) return 1.0;
            int ceiling = Math.Max(maxDays + 18, 100);
            double r = 1.0 - (double)d / ceiling;
            return Math.Max(0.14, Math.Min(0.92, r));
        }

        // Real cycle progress for single-day focus card
        static double RealSingleProgress(ShareEntry e, DateTime now, out DateTime cycleStart)
        {
            if (e.IsCountup)
            {
                cycleStart = e.Date;
                int elapsed = Math.Max(1, (now.Date - e.Date.Date).Days + 1);
                return Math.Max(0.08, Math.Min(1.0, (elapsed % 365) / 365.0));
            }
            cycleStart = (e.StartDate > DateTime.MinValue && e.StartDate < e.Date) ? e.StartDate : e.Date.AddYears(-1);
            double total = Math.Max(1.0, (e.Date - cycleStart).TotalDays);
            return Math.Max(0.06, Math.Min(1.0, (now - cycleStart).TotalDays / total));
        }

        static double YearProgress(DateTime now, out int passed, out int remain)
        {
            int total = DateTime.IsLeapYear(now.Year) ? 366 : 365;
            passed = now.DayOfYear;
            remain = Math.Max(0, total - passed);
            return Math.Max(0.01, Math.Min(1.0, (double)passed / total));
        }

        static Grid BuildProgressTrack(double ratio, double height, string fillHex, string trackHex)
        {
            double clamped = Math.Max(0.04, Math.Min(1.0, ratio));
            var g = new Grid { Height = height };
            g.Children.Add(new Border { Background = Brush(trackHex), CornerRadius = new CornerRadius(height / 2) });
            var split = new Grid();
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(clamped, GridUnitType.Star) });
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.001, 1.0 - clamped), GridUnitType.Star) });
            var fill = new Border { Background = Brush(fillHex), CornerRadius = new CornerRadius(height / 2) };
            Grid.SetColumn(fill, 0);
            split.Children.Add(fill);
            g.Children.Add(split);
            return g;
        }

        static Border BuildPill(string text, string bgHex, string fgHex, double fontSize = 9.5)
        {
            var b = new Border
            {
                Background = Brush(bgHex),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center
            };
            b.Child = new TextBlock { Text = text, FontSize = fontSize, FontWeight = FontWeights.SemiBold, Foreground = Brush(fgHex) };
            return b;
        }

        static void AddRadialGlow(Canvas canvas, double cx, double cy, double rx, double ry, string hexCore, byte alphaCore)
        {
            var c = C_(hexCore);
            var rad = new RadialGradientBrush { GradientOrigin = new Point(0.5, 0.5), Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5 };
            rad.GradientStops.Add(new GradientStop(Color.FromArgb(alphaCore, c.R, c.G, c.B), 0.0));
            rad.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(alphaCore * 0.42), c.R, c.G, c.B), 0.48));
            rad.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1.0));
            rad.Freeze();
            var el = new Ellipse { Width = rx * 2, Height = ry * 2, Fill = rad, IsHitTestVisible = false };
            Canvas.SetLeft(el, cx - rx); Canvas.SetTop(el, cy - ry);
            canvas.Children.Add(el);
        }

        // Thematic vector artwork tucked subtly behind Hero big number (just like the Pine Tree in reference!)
        static FrameworkElement BuildHeroEmblem(string themeId, string heroName, string accentHex, double size)
        {
            var vb = new Viewbox { Width = size, Height = size, Stretch = Stretch.Uniform, IsHitTestVisible = false };
            var cv = new Canvas { Width = 120, Height = 120 };
            var stroke = Brush(HexA("36", accentHex));
            var fillSoft = Brush(HexA("16", accentHex));

            string fest = FestivalForName(heroName);
            if (themeId == "pine" || fest == "christmas")
            {
                // Iconic Christmas pine tree + top star + soft ornament dot
                cv.Children.Add(new System.Windows.Shapes.Path
                {
                    Data = Geometry.Parse("M 60,24 L 36,56 L 48,56 L 26,84 L 42,84 L 20,108 L 100,108 L 78,84 L 94,84 L 72,56 L 84,56 Z"),
                    Stroke = stroke, StrokeThickness = 3.2, Fill = fillSoft, StrokeLineJoin = PenLineJoin.Round
                });
                cv.Children.Add(new System.Windows.Shapes.Path
                {
                    Data = Geometry.Parse("M 60,6 L 63.5,14.5 L 72.5,15.5 L 65.5,21.5 L 67.5,30 L 60,25.5 L 52.5,30 L 54.5,21.5 L 47.5,15.5 L 56.5,14.5 Z"),
                    Fill = Brush(HexA("44", accentHex))
                });
                var orn = new Ellipse { Width = 13, Height = 13, Fill = Brush("#44C85242") };
                Canvas.SetLeft(orn, 53); Canvas.SetTop(orn, 74); cv.Children.Add(orn);
            }
            else if (fest == "spring")
            {
                // Traditional festive lantern
                cv.Children.Add(new System.Windows.Shapes.Path
                {
                    Data = Geometry.Parse("M 60,12 L 60,24 M 42,24 L 78,24 M 42,86 L 78,86 M 60,86 L 60,108 M 60,25 C 22,25 22,85 60,85 C 98,85 98,25 60,25 Z M 60,25 C 42,25 42,85 60,85 M 60,25 C 78,25 78,85 60,85"),
                    Stroke = stroke, StrokeThickness = 2.8, Fill = fillSoft, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
                });
            }
            else if (themeId == "vinyl")
            {
                // Concentric vinyl grooves
                for (int i = 0; i < 4; i++)
                {
                    double d = 44 + i * 20;
                    var e = new Ellipse { Width = d, Height = d, Stroke = stroke, StrokeThickness = 1.8 };
                    Canvas.SetLeft(e, 60 - d / 2); Canvas.SetTop(e, 60 - d / 2); cv.Children.Add(e);
                }
                var core = new Ellipse { Width = 22, Height = 22, Fill = fillSoft, Stroke = stroke, StrokeThickness = 2 };
                Canvas.SetLeft(core, 49); Canvas.SetTop(core, 49); cv.Children.Add(core);
            }
            else if (themeId == "xuan")
            {
                // Poetic ink moon + wave
                var moon = new Ellipse { Width = 78, Height = 78, Fill = fillSoft, Stroke = stroke, StrokeThickness = 2.2 };
                Canvas.SetLeft(moon, 21); Canvas.SetTop(moon, 18); cv.Children.Add(moon);
                cv.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M 10,82 Q 40,66 64,78 T 112,72"), Stroke = stroke, StrokeThickness = 2.4 });
            }
            else
            {
                // Celestial star & orbital rings (Aurora / Sunset)
                var r1 = new Ellipse { Width = 86, Height = 86, Stroke = stroke, StrokeThickness = 2.0, StrokeDashArray = new DoubleCollection { 4, 4 } };
                Canvas.SetLeft(r1, 17); Canvas.SetTop(r1, 17); cv.Children.Add(r1);
                var r2 = new Ellipse { Width = 52, Height = 52, Stroke = stroke, StrokeThickness = 1.6, Fill = fillSoft };
                Canvas.SetLeft(r2, 34); Canvas.SetTop(r2, 34); cv.Children.Add(r2);
                cv.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M 86,20 L 89,29 L 98,32 L 89,35 L 86,44 L 83,35 L 74,32 L 83,29 Z"), Fill = Brush(HexA("55", accentHex)) });
            }

            vb.Child = cv;
            return vb;
        }

        static Grid BuildBackdropLayer(int w, int h, ArtTheme th)
        {
            var layer = new Grid { Width = w, Height = h, ClipToBounds = true, IsHitTestVisible = false };
            var bg = new LinearGradientBrush { StartPoint = new Point(0.1, 0), EndPoint = new Point(0.9, 1) };
            bg.GradientStops.Add(new GradientStop(C_(th.BgTop), 0.0));
            bg.GradientStops.Add(new GradientStop(C_(th.BgMid), 0.55));
            bg.GradientStops.Add(new GradientStop(C_(th.BgBot), 1.0));
            bg.Freeze();
            layer.Background = bg;

            var art = new Canvas { Width = w, Height = h, IsHitTestVisible = false };
            layer.Children.Add(art);

            if (th.Id == "pine")
            {
                AddRadialGlow(art, w * 0.85, h * 0.16, w * 0.56, w * 0.56, "#3D7A62", 52);
                AddRadialGlow(art, w * 0.14, h * 0.86, w * 0.52, w * 0.52, "#D4B672", 24);
            }
            else if (th.Id == "aurora")
            {
                AddRadialGlow(art, w * 0.84, h * 0.14, w * 0.64, w * 0.64, "#38D9C5", 95);
                AddRadialGlow(art, w * 0.14, h * 0.82, w * 0.60, w * 0.60, "#5E6AF5", 85);
            }
            else if (th.Id == "sunset")
            {
                AddRadialGlow(art, w * 0.78, h * 0.22, w * 0.58, w * 0.58, "#FFCB77", 105);
                AddRadialGlow(art, w * 0.18, h * 0.84, w * 0.60, w * 0.60, "#4B1C45", 90);
            }
            else if (th.Id == "xuan")
            {
                AddRadialGlow(art, w * 0.82, h * 0.18, w * 0.34, w * 0.34, "#C43B2E", 22);
                var topAccent = new Rectangle { Width = w, Height = 5, Fill = Brush("#B33428") };
                art.Children.Add(topAccent);
            }
            else if (th.Id == "vinyl")
            {
                AddRadialGlow(art, w * 0.88, h * 0.24, w * 0.56, w * 0.56, "#E89B4C", 38);
            }

            layer.Children.Add(new Rectangle { Fill = PaperGrainBrush(th.IsLight), Opacity = th.IsLight ? 0.85 : 0.70, IsHitTestVisible = false });
            return layer;
        }

        static BitmapImage TryLoadPhoto(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.DecodePixelWidth = 1400;
                bi.UriSource = new Uri(path, UriKind.Absolute);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch { return null; }
        }

        static Border BuildCalendarStamp(DateTime now, ArtTheme th)
        {
            var box = new Border
            {
                Background = Brush(th.SurfaceStrong),
                BorderBrush = Brush(th.Hairline),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(11, 6, 11, 6),
                VerticalAlignment = VerticalAlignment.Center
            };
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = MonthEn(now.Month) + " · " + WeekdayShortZh(now),
                FontFamily = MonoFont,
                FontSize = 8.5,
                Foreground = Brush(th.Muted),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = now.Day.ToString("00"),
                FontFamily = NumFont,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brush(th.Ink),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            });
            box.Child = sp;
            return box;
        }

        static Border BuildSealBadge(string sealText, ArtTheme th)
        {
            var b = new Border
            {
                Background = Brush(th.Surface),
                BorderBrush = Brush(HexA("70", th.Accent)),
                BorderThickness = new Thickness(1.1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 5, 10, 5),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            b.Child = new TextBlock { Text = sealText, FontFamily = SerifFont, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Brush(th.Accent) };
            return b;
        }

        static FrameworkElement BuildFooter(string quote, bool showWatermark, ArtTheme th)
        {
            var wrap = new StackPanel();
            wrap.Children.Add(new Border { Height = 1, Background = Brush(th.Hairline), Margin = new Thickness(0, 0, 0, 11) });
            var row = new DockPanel();
            if (showWatermark)
            {
                var wm = new TextBlock
                {
                    Text = "拾光倒计时",
                    FontFamily = SansFont,
                    FontSize = 10,
                    Foreground = Brush(th.Muted),
                    Opacity = 0.78,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 0, 0)
                };
                DockPanel.SetDock(wm, Dock.Right);
                row.Children.Add(wm);
            }
            row.Children.Add(new TextBlock
            {
                Text = "“" + quote + "”",
                FontFamily = SerifFont,
                FontSize = 11.5,
                Foreground = Brush(th.Muted),
                Opacity = 0.94,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });
            wrap.Children.Add(row);
            return wrap;
        }

        // Rich Hero + Bento Poster (Combines the beloved progress bars & year-progress Bento with curated lush themes!)
        static void BuildRichBentoPoster(Grid root, int w, int h, List<ShareEntry> entries, CardOpts opts, ArtTheme th)
        {
            bool isSquare = opts.RatioIndex == 1;
            bool isWide = opts.RatioIndex == 2;
            bool isLock = opts.RatioIndex == 3;
            var now = DateTime.Now;
            string quote = string.IsNullOrWhiteSpace(opts.Quote) ? LiteraryQuotes[0] : opts.Quote.Trim();
            int yPassed, yRemain;
            double yProg = YearProgress(now, out yPassed, out yRemain);

            root.Children.Add(BuildBackdropLayer(w, h, th));

            Thickness pad = isLock ? new Thickness(34, 275, 34, 68)
                : isWide ? new Thickness(38, 26, 38, 24)
                : isSquare ? new Thickness(32, 26, 32, 22)
                : new Thickness(34, 30, 34, 26);

            var dock = new DockPanel { Margin = pad };
            root.Children.Add(dock);

            int maxDays;
            var ordered = OrderEntries(entries, now, out maxDays);
            if (ordered.Count == 0) return;

            // --- Header ---
            var headerWrap = new StackPanel { Margin = new Thickness(0, 0, 0, isSquare ? 12 : 18) };
            DockPanel.SetDock(headerWrap, Dock.Top);
            dock.Children.Add(headerWrap);

            var header = new DockPanel();
            headerWrap.Children.Add(header);

            var rightBadges = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            rightBadges.Children.Add(BuildSealBadge(th.Seal, th));
            rightBadges.Children.Add(BuildCalendarStamp(now, th));
            DockPanel.SetDock(rightBadges, Dock.Right);
            header.Children.Add(rightBadges);

            var titleLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            string eyebrow = opts.IsList
                ? ("TIMELINE  ·  " + now.Year + " 第 " + IsoWeek(now) + " 周  ·  共 " + ordered.Count + " 个期待")
                : ("DAILY FOCUS  ·  " + now.Year + " 第 " + IsoWeek(now) + " 周  ·  " + LunarHelper.SolarToFullLunar(now));
            titleLeft.Children.Add(new TextBlock { Text = eyebrow, FontFamily = MonoFont, FontSize = 9.5, Foreground = Brush(th.Accent), Opacity = 0.9 });
            titleLeft.Children.Add(new TextBlock
            {
                Text = opts.IsList ? "值得期待的日子" : "距离期待之日",
                FontFamily = SerifFont,
                FontSize = isSquare ? 22 : 25,
                FontWeight = FontWeights.Bold,
                Foreground = Brush(th.Ink),
                Margin = new Thickness(0, 4, 0, 0)
            });
            header.Children.Add(titleLeft);
            headerWrap.Children.Add(new Border { Height = 1, Background = Brush(th.Hairline), Margin = new Thickness(0, 12, 0, 0) });

            // --- Footer ---
            var foot = BuildFooter(quote, opts.ShowWatermark, th);
            foot.Margin = new Thickness(0, isSquare ? 10 : 14, 0, 0);
            DockPanel.SetDock(foot, Dock.Bottom);
            dock.Children.Add(foot);

            if (opts.IsList)
            {
                // === LIST MODE: Hero Card w/ Progress Bar + Upcoming Progress Cards + Bottom Year Progress & Span Bento ===
                var hero = ordered[0];
                var rest = ordered.Skip(1).Take(isSquare ? 2 : 3).ToList();

                // Bottom Dual Bento Row (2026 年度进度 + 日程跨度)
                if (!isWide)
                {
                    var bentoRow = new Grid { Margin = new Thickness(0, isSquare ? 10 : 16, 0, 0) };
                    bentoRow.ColumnDefinitions.Add(new ColumnDefinition());
                    bentoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
                    bentoRow.ColumnDefinitions.Add(new ColumnDefinition());
                    DockPanel.SetDock(bentoRow, Dock.Bottom);
                    dock.Children.Add(bentoRow);

                    // Bento 1: Year Progress
                    var b1 = new Border { Background = Brush(th.Surface), BorderBrush = Brush(th.Hairline), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 11, 14, 11) };
                    var b1Stack = new StackPanel(); b1.Child = b1Stack;
                    var b1Top = new DockPanel { Margin = new Thickness(0, 0, 0, 7) };
                    var b1Pct = new TextBlock { Text = (yProg * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%", FontFamily = NumFont, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brush(th.Ink) };
                    DockPanel.SetDock(b1Pct, Dock.Right); b1Top.Children.Add(b1Pct);
                    b1Top.Children.Add(new TextBlock { Text = now.Year + " 年度进度", FontSize = 10.5, Foreground = Brush(th.Muted), VerticalAlignment = VerticalAlignment.Center });
                    b1Stack.Children.Add(b1Top);
                    b1Stack.Children.Add(BuildProgressTrack(yProg, 4.5, th.Accent, th.Track));
                    b1Stack.Children.Add(new TextBlock { Text = "已过 " + yPassed + " 天 · 余 " + yRemain + " 天", FontSize = 9.5, Foreground = Brush(th.Muted), Margin = new Thickness(0, 6, 0, 0) });
                    Grid.SetColumn(b1, 0); bentoRow.Children.Add(b1);

                    // Bento 2: Schedule Span
                    var b2 = new Border { Background = Brush(th.Surface), BorderBrush = Brush(th.Hairline), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 11, 14, 11) };
                    var b2Stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; b2.Child = b2Stack;
                    var lastShown = rest.Count > 0 ? rest[rest.Count - 1] : hero;
                    int minD = Math.Max(0, (hero.Date.Date - now.Date).Days);
                    int maxD = Math.Max(minD, (lastShown.Date.Date - now.Date).Days);
                    b2Stack.Children.Add(new TextBlock { Text = "日程跨度", FontSize = 10.5, Foreground = Brush(th.Muted) });
                    b2Stack.Children.Add(new TextBlock
                    {
                        Text = minD == maxD ? (minD + " 天") : (minD + " — " + maxD + " 天"),
                        FontFamily = NumFont,
                        FontSize = 17,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brush(th.Ink),
                        Margin = new Thickness(0, 3, 0, 2)
                    });
                    b2Stack.Children.Add(new TextBlock
                    {
                        Text = hero.Date.ToString("MM.dd") + " 至 " + lastShown.Date.ToString("MM.dd"),
                        FontFamily = MonoFont,
                        FontSize = 10,
                        Foreground = Brush(th.Accent)
                    });
                    Grid.SetColumn(b2, 2); bentoRow.Children.Add(b2);
                }

                // Middle Area: Hero Card + Upcoming Cards
                var midStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                dock.Children.Add(midStack);

                // --- Hero Focus Card ---
                var heroCard = new Border
                {
                    Background = Brush(th.SurfaceStrong),
                    BorderBrush = Brush(th.Hairline),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    ClipToBounds = true,
                    Margin = new Thickness(0, 0, 0, isSquare ? 10 : 14)
                };
                var heroGrid = new Grid();
                heroCard.Child = heroGrid;

                // Subtle thematic emblem behind Hero number (like the pine tree!)
                var emblem = BuildHeroEmblem(th.Id, hero.Name, th.Accent, isLock ? 115 : 102);
                emblem.HorizontalAlignment = HorizontalAlignment.Right;
                emblem.VerticalAlignment = VerticalAlignment.Bottom;
                emblem.Margin = new Thickness(0, 0, 8, -8);
                heroGrid.Children.Add(emblem);

                // Warm accent vertical strip on left
                heroGrid.Children.Add(new Border
                {
                    Width = 4,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Background = Brush(th.Accent),
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 14, 0, 14)
                });

                var heroPad = new StackPanel { Margin = new Thickness(18, isSquare ? 12 : 15, 18, isSquare ? 12 : 15) };
                heroGrid.Children.Add(heroPad);

                var heroTop = new DockPanel { Margin = new Thickness(0, 0, 0, 11) };
                heroPad.Children.Add(heroTop);

                bool hNum; string hUnit;
                string hStr = EntryDayNumber(hero, now, out hNum, out hUnit);
                var heroRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                heroRight.Children.Add(new TextBlock
                {
                    Text = hStr,
                    FontFamily = hNum ? NumFont : SerifFont,
                    FontSize = hNum ? (isLock ? 62 : (isSquare ? 46 : 54)) : 28,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brush(th.Ink),
                    LineHeight = hNum ? (isLock ? 56 : 48) : 32,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    VerticalAlignment = VerticalAlignment.Bottom
                });
                if (hNum)
                {
                    heroRight.Children.Add(new TextBlock
                    {
                        Text = "天",
                        FontSize = 14,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brush(th.Accent),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(4, 0, 0, 6)
                    });
                }
                DockPanel.SetDock(heroRight, Dock.Right);
                heroTop.Children.Add(heroRight);

                var heroLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var tagRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 7) };
                tagRow.Children.Add(BuildPill("NEXT · 最近期待", th.PillBg, th.PillFg, 9));
                var relPill = BuildPill(RelativeSpanTag(hero, now), th.Surface, th.Muted, 9);
                relPill.Margin = new Thickness(6, 0, 0, 0);
                tagRow.Children.Add(relPill);
                heroLeft.Children.Add(tagRow);

                heroLeft.Children.Add(new TextBlock
                {
                    Text = hero.Name,
                    FontFamily = SansFont,
                    FontSize = isLock ? 24 : (isSquare ? 19 : 22),
                    FontWeight = FontWeights.Bold,
                    Foreground = Brush(th.Ink),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                if (opts.ShowDate)
                {
                    heroLeft.Children.Add(new TextBlock
                    {
                        Text = hero.Date.ToString("yyyy.MM.dd") + " · " + WeekdayFullZh(hero.Date),
                        FontFamily = MonoFont,
                        FontSize = 10,
                        Foreground = Brush(th.Muted),
                        Margin = new Thickness(0, 3, 0, 0)
                    });
                }
                heroTop.Children.Add(heroLeft);

                // Intuitive luminous progress bar on Hero Card!
                heroPad.Children.Add(BuildProgressTrack(ListProgressRatio(hero, now, maxDays), 4.5, th.Accent, th.Track));
                midStack.Children.Add(heroCard);

                // Sub-section header
                if (rest.Count > 0)
                {
                    var subHead = new DockPanel { Margin = new Thickness(3, 0, 3, 8) };
                    var shRight = new TextBlock { Text = "按时间排序", FontSize = 9.5, Foreground = Brush(th.Accent), Opacity = 0.85 };
                    DockPanel.SetDock(shRight, Dock.Right); subHead.Children.Add(shRight);
                    subHead.Children.Add(new TextBlock { Text = "后续期待日程", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brush(th.Muted) });
                    midStack.Children.Add(subHead);
                }

                // Upcoming Bento Cards with stepped comparative progress bars!
                for (int i = 0; i < rest.Count; i++)
                {
                    var it = rest[i];
                    bool rNum; string rUnit;
                    string rStr = EntryDayNumber(it, now, out rNum, out rUnit);

                    var c = new Border
                    {
                        Background = Brush(th.Surface),
                        BorderBrush = Brush(th.Hairline),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(14, isLock ? 14 : (isSquare ? 9 : 11), 14, isLock ? 14 : (isSquare ? 9 : 11)),
                        Margin = new Thickness(0, 0, 0, i < rest.Count - 1 ? (isLock ? 12 : 9) : 0)
                    };
                    var cs = new StackPanel(); c.Child = cs;

                    var rTop = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
                    cs.Children.Add(rTop);

                    var rVal = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    rVal.Children.Add(new TextBlock
                    {
                        Text = rStr,
                        FontFamily = rNum ? NumFont : SerifFont,
                        FontSize = rNum ? (isLock ? 30 : 25) : 16,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brush(th.Ink),
                        VerticalAlignment = VerticalAlignment.Bottom
                    });
                    if (rNum)
                    {
                        rVal.Children.Add(new TextBlock
                        {
                            Text = "天",
                            FontSize = 11,
                            Foreground = Brush(th.Accent),
                            VerticalAlignment = VerticalAlignment.Bottom,
                            Margin = new Thickness(3.5, 0, 0, 3)
                        });
                    }
                    DockPanel.SetDock(rVal, Dock.Right);
                    rTop.Children.Add(rVal);

                    var lGrid = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    lGrid.Children.Add(new TextBlock
                    {
                        Text = (i + 2).ToString("00"),
                        FontFamily = MonoFont,
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brush(th.Accent),
                        Width = 22,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, 4, 0, 0)
                    });
                    var txtSp = new StackPanel();
                    var nameLine = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    nameLine.Children.Add(new TextBlock { Text = it.Name, FontSize = isLock ? 17 : 15, FontWeight = FontWeights.Bold, Foreground = Brush(th.Ink) });
                    var rp = BuildPill(RelativeSpanTag(it, now), th.SurfaceStrong, th.Muted, 8.5);
                    rp.Margin = new Thickness(8, 0, 0, 0);
                    nameLine.Children.Add(rp);
                    txtSp.Children.Add(nameLine);

                    if (opts.ShowDate)
                    {
                        txtSp.Children.Add(new TextBlock
                        {
                            Text = it.Date.ToString("yyyy.MM.dd") + " · " + WeekdayFullZh(it.Date),
                            FontFamily = MonoFont,
                            FontSize = 9.5,
                            Foreground = Brush(th.Muted),
                            Margin = new Thickness(0, 3, 0, 0)
                        });
                    }
                    lGrid.Children.Add(txtSp);
                    rTop.Children.Add(lGrid);

                    var pb = BuildProgressTrack(ListProgressRatio(it, now, maxDays), 3.5, th.Accent, th.Track);
                    pb.Margin = new Thickness(22, 0, 0, 0);
                    cs.Children.Add(pb);

                    midStack.Children.Add(c);
                }
            }
            else
            {
                // === SINGLE-DAY FOCUS MODE: Elevated Hero Card + Real Cycle Progress + Triple Bento Metrics ===
                var entry = entries[Math.Max(0, Math.Min(opts.EntryIndex, entries.Count - 1))];
                bool isNum; string unit;
                string dayStr = EntryDayNumber(entry, now, out isNum, out unit);
                DateTime cycleStart;
                double realProg = RealSingleProgress(entry, now, out cycleStart);

                // Bottom Triple Bento Stats
                if (!isWide)
                {
                    var bento3 = new Grid { Margin = new Thickness(0, isSquare ? 10 : 18, 0, 0) };
                    bento3.ColumnDefinitions.Add(new ColumnDefinition());
                    bento3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
                    bento3.ColumnDefinitions.Add(new ColumnDefinition());
                    bento3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
                    bento3.ColumnDefinitions.Add(new ColumnDefinition());
                    DockPanel.SetDock(bento3, Dock.Bottom);
                    dock.Children.Add(bento3);

                    Func<string, string, string, double?, Border> makeTile = (k, v, s, progOpt) =>
                    {
                        var tile = new Border { Background = Brush(th.Surface), BorderBrush = Brush(th.Hairline), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10, 12, 10) };
                        var sp = new StackPanel(); tile.Child = sp;
                        sp.Children.Add(new TextBlock { Text = k, FontSize = 9.5, Foreground = Brush(th.Muted) });
                        sp.Children.Add(new TextBlock { Text = v, FontFamily = NumFont, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Brush(th.Ink), Margin = new Thickness(0, 3, 0, 3), TextTrimming = TextTrimming.CharacterEllipsis });
                        if (progOpt.HasValue)
                        {
                            var pb = BuildProgressTrack(progOpt.Value, 3.2, th.Accent, th.Track);
                            pb.Margin = new Thickness(0, 1, 0, 4);
                            sp.Children.Add(pb);
                        }
                        sp.Children.Add(new TextBlock { Text = s, FontSize = 9.5, Foreground = Brush(th.Accent), TextTrimming = TextTrimming.CharacterEllipsis });
                        return tile;
                    };

                    int hoursApprox = Math.Max(0, (int)Math.Round(Math.Abs((entry.Date - now).TotalHours)));
                    var t1 = makeTile("目标日期", entry.Date.ToString("yyyy.MM.dd"), !string.IsNullOrEmpty(entry.LunarLabel) ? entry.LunarLabel : WeekdayFullZh(entry.Date), null);
                    var t2 = makeTile("距离换算", CompactWeekText(entry, now), "约 " + hoursApprox.ToString("N0", CultureInfo.InvariantCulture) + " 小时", null);
                    var t3 = makeTile(now.Year + " 年度进度", (yProg * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%", "余 " + yRemain + " 天跨年", yProg);
                    Grid.SetColumn(t1, 0); bento3.Children.Add(t1);
                    Grid.SetColumn(t2, 2); bento3.Children.Add(t2);
                    Grid.SetColumn(t3, 4); bento3.Children.Add(t3);
                }

                // Center Hero Card
                var heroCard = new Border
                {
                    Background = Brush(th.SurfaceStrong),
                    BorderBrush = Brush(th.Hairline),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    ClipToBounds = true,
                    VerticalAlignment = VerticalAlignment.Center
                };
                dock.Children.Add(heroCard);

                var hg = new Grid();
                heroCard.Child = hg;

                var emblem = BuildHeroEmblem(th.Id, entry.Name, th.Accent, isSquare ? 135 : 165);
                emblem.HorizontalAlignment = HorizontalAlignment.Right;
                emblem.VerticalAlignment = VerticalAlignment.Center;
                emblem.Margin = new Thickness(0, 0, 14, 0);
                hg.Children.Add(emblem);

                hg.Children.Add(new Border
                {
                    Width = 4.5,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Background = Brush(th.Accent),
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 22, 0, 22)
                });

                var hp = new StackPanel { Margin = new Thickness(26, isSquare ? 20 : 28, 26, isSquare ? 20 : 26) };
                hg.Children.Add(hp);

                var pillRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
                pillRow.Children.Add(BuildPill((entry.IsCountup ? "ANNIVERSARY · " : "NEXT MILESTONE · ") + RelativeSpanTag(entry, now), th.PillBg, th.PillFg, 9.5));
                hp.Children.Add(pillRow);

                hp.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(entry.Name) ? "期待之日" : entry.Name,
                    FontFamily = SerifFont,
                    FontSize = isSquare ? 30 : 38,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brush(th.Ink),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8)
                });

                var numRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, isSquare ? 16 : 24) };
                double bigSz = isLock ? 128 : (isSquare ? 94 : 118);
                numRow.Children.Add(new TextBlock
                {
                    Text = dayStr,
                    FontFamily = isNum ? NumFont : SerifFont,
                    FontSize = isNum ? bigSz : 48,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brush(th.Ink),
                    LineHeight = isNum ? bigSz * 0.90 : 52,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    VerticalAlignment = VerticalAlignment.Bottom
                });
                if (isNum)
                {
                    var uCol = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(10, 0, 0, 10) };
                    uCol.Children.Add(new TextBlock { Text = entry.IsCountup ? "DAYS" : "DAYS LEFT", FontFamily = MonoFont, FontSize = 9.5, Foreground = Brush(th.Muted) });
                    uCol.Children.Add(new TextBlock { Text = entry.IsCountup ? "天" : "天后", FontFamily = SerifFont, FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brush(th.Accent) });
                    numRow.Children.Add(uCol);
                }
                hp.Children.Add(numRow);

                // Real Progress Bar with Start/Target dates & Percentage!
                var pHead = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                var pRight = new TextBlock
                {
                    Text = entry.Date.ToString("yyyy.MM.dd") + "  ·  " + ((int)Math.Round(realProg * 100)) + "%",
                    FontFamily = MonoFont,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(th.Accent)
                };
                DockPanel.SetDock(pRight, Dock.Right);
                pHead.Children.Add(pRight);
                pHead.Children.Add(new TextBlock
                {
                    Text = (entry.IsCountup ? "START " : "FROM ") + cycleStart.ToString("yyyy.MM.dd"),
                    FontFamily = MonoFont,
                    FontSize = 10,
                    Foreground = Brush(th.Muted)
                });
                hp.Children.Add(pHead);
                hp.Children.Add(BuildProgressTrack(realProg, 5.5, th.Accent, th.Track));
            }
        }

        // Theme 5: Authentic Polaroid Photo Card (with progress bar + year progress storytelling!)
        static void BuildPolaroidPoster(Grid root, int w, int h, List<ShareEntry> entries, CardOpts opts, ArtTheme th)
        {
            bool isSquare = opts.RatioIndex == 1;
            bool isWide = opts.RatioIndex == 2;
            bool isLock = opts.RatioIndex == 3;
            var now = DateTime.Now;
            string quote = string.IsNullOrWhiteSpace(opts.Quote) ? LiteraryQuotes[0] : opts.Quote.Trim();
            int yPassed, yRemain;
            double yProg = YearProgress(now, out yPassed, out yRemain);

            int maxDays;
            var ordered = OrderEntries(entries, now, out maxDays);
            var entry = (!opts.IsList && entries.Count > 0)
                ? entries[Math.Max(0, Math.Min(opts.EntryIndex, entries.Count - 1))]
                : (ordered.Count > 0 ? ordered[0] : new ShareEntry { Name = "期待之日", Date = now.AddDays(30) });

            bool isNum; string unit;
            string dayStr = EntryDayNumber(entry, now, out isNum, out unit);
            DateTime cStart;
            double prog = opts.IsList ? ListProgressRatio(entry, now, maxDays) : RealSingleProgress(entry, now, out cStart);

            var stockBg = new LinearGradientBrush(C_(th.BgTop), C_(th.BgBot), new Point(0, 0), new Point(1, 1));
            stockBg.Freeze();
            root.Background = stockBg;
            root.Children.Add(new Rectangle { Fill = PaperGrainBrush(true), Opacity = 0.85, IsHitTestVisible = false });

            var photoBmp = TryLoadPhoto(opts.PhotoPath);

            Func<Grid> buildPhotoWell = () =>
            {
                var frame = new Grid { ClipToBounds = true };
                if (photoBmp != null)
                {
                    frame.Children.Add(new Rectangle { Fill = new ImageBrush(photoBmp) { Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center } });
                    var vig = new LinearGradientBrush(Color.FromArgb(15, 0, 0, 0), Color.FromArgb(170, 12, 10, 10), new Point(0, 0.2), new Point(0, 1));
                    vig.Freeze();
                    frame.Children.Add(new Rectangle { Fill = vig });
                }
                else
                {
                    var sky = new LinearGradientBrush { StartPoint = new Point(0.1, 0), EndPoint = new Point(0.9, 1) };
                    sky.GradientStops.Add(new GradientStop(C_("#1D2B44"), 0.0));
                    sky.GradientStops.Add(new GradientStop(C_("#4B5074"), 0.45));
                    sky.GradientStops.Add(new GradientStop(C_("#C27C68"), 0.82));
                    sky.GradientStops.Add(new GradientStop(C_("#E9B27B"), 1.0));
                    sky.Freeze();
                    frame.Background = sky;
                    var skyArt = new Canvas { Width = 600, Height = 500, IsHitTestVisible = false };
                    AddRadialGlow(skyArt, 440, 140, 210, 210, "#FFE0A8", 115);
                    skyArt.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M 0,430 Q 160,330 320,395 T 600,360 L 600,500 L 0,500 Z"), Fill = Brush("#55141A29") });
                    skyArt.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M 0,460 Q 220,385 410,435 T 600,415 L 600,500 L 0,500 Z"), Fill = Brush("#880D111C") });
                    frame.Children.Add(new Viewbox { Stretch = Stretch.UniformToFill, Child = skyArt });
                    var vig = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(135, 10, 12, 20), new Point(0, 0.35), new Point(0, 1));
                    vig.Freeze();
                    frame.Children.Add(new Rectangle { Fill = vig });
                }

                frame.Children.Add(new Rectangle { Fill = PaperGrainBrush(false), Opacity = 0.8, IsHitTestVisible = false });

                // Top-left floating pill
                var topBadge = new Border
                {
                    Background = Brush("#66000000"), CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(9, 4, 9, 4), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(14, 14, 0, 0)
                };
                topBadge.Child = new TextBlock { Text = now.Year + " 年度已过 " + (yProg * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%", FontFamily = MonoFont, FontSize = 9.5, Foreground = Brushes.White };
                frame.Children.Add(topBadge);

                // Film date stamp bottom-right
                frame.Children.Add(new TextBlock
                {
                    Text = "'" + now.ToString("yy MM dd"), FontFamily = MonoFont, FontSize = 11.5, FontWeight = FontWeights.Bold,
                    Foreground = Brush("#EEF2B26B"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 16, 14)
                });

                var hero = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(18, 0, 0, 12) };
                double pNumSize = isSquare ? 64 : 84;
                hero.Children.Add(new TextBlock { Text = dayStr, FontFamily = isNum ? NumFont : SerifFont, FontSize = isNum ? pNumSize : 34, FontWeight = FontWeights.Bold, Foreground = Brushes.White, LineHeight = isNum ? pNumSize * 0.88 : 38, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, VerticalAlignment = VerticalAlignment.Bottom });
                if (isNum) hero.Children.Add(new TextBlock { Text = entry.IsCountup ? "天" : "天后", FontFamily = SerifFont, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brush("#EBFFFFFF"), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(6, 0, 0, 8) });
                frame.Children.Add(hero);
                return frame;
            };

            if (isWide)
            {
                var pad = new Grid { Margin = new Thickness(26) };
                pad.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
                pad.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                pad.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.95, GridUnitType.Star) });
                root.Children.Add(pad);
                var photoBox = new Border { CornerRadius = new CornerRadius(5), ClipToBounds = true, Child = buildPhotoWell() };
                Grid.SetColumn(photoBox, 0); pad.Children.Add(photoBox);
                var right = new DockPanel { Margin = new Thickness(0, 6, 4, 4) };
                Grid.SetColumn(right, 2); pad.Children.Add(right);
                var foot = BuildFooter(quote, opts.ShowWatermark, th);
                DockPanel.SetDock(foot, Dock.Bottom); right.Children.Add(foot);
                var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                right.Children.Add(mid);
                mid.Children.Add(new TextBlock { Text = entry.Name, FontFamily = SerifFont, FontSize = 30, FontWeight = FontWeights.Bold, Foreground = Brush(th.Ink), TextWrapping = TextWrapping.Wrap });
                mid.Children.Add(new TextBlock { Text = entry.Date.ToString("yyyy.MM.dd") + " · " + WeekdayFullZh(entry.Date), FontFamily = MonoFont, FontSize = 10.5, Foreground = Brush(th.Muted), Margin = new Thickness(0, 6, 0, 12) });
                mid.Children.Add(BuildProgressTrack(prog, 4, th.Accent, th.Track));
            }
            else
            {
                var pad = new Grid { Margin = isLock ? new Thickness(26, 280, 26, 56) : new Thickness(24, 24, 24, 22) };
                pad.RowDefinitions.Add(new RowDefinition { Height = new GridLength(isSquare ? 1.55 : 1.78, GridUnitType.Star) });
                pad.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
                root.Children.Add(pad);

                var photoBox = new Border { CornerRadius = new CornerRadius(5), ClipToBounds = true, Child = buildPhotoWell() };
                Grid.SetRow(photoBox, 0); pad.Children.Add(photoBox);

                var chin = new DockPanel { Margin = new Thickness(2, 16, 2, 0) };
                Grid.SetRow(chin, 1); pad.Children.Add(chin);

                var foot = BuildFooter(quote, opts.ShowWatermark, th);
                DockPanel.SetDock(foot, Dock.Bottom); chin.Children.Add(foot);

                var chinMain = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                chin.Children.Add(chinMain);

                var headRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
                var seal = BuildSealBadge(RelativeSpanTag(entry, now), th);
                seal.Margin = new Thickness(0);
                DockPanel.SetDock(seal, Dock.Right); headRow.Children.Add(seal);
                var tSp = new StackPanel();
                tSp.Children.Add(new TextBlock { Text = entry.Name, FontFamily = SerifFont, FontSize = isSquare ? 24 : 28, FontWeight = FontWeights.Bold, Foreground = Brush(th.Ink), TextTrimming = TextTrimming.CharacterEllipsis });
                tSp.Children.Add(new TextBlock { Text = entry.Date.ToString("yyyy.MM.dd") + " · " + WeekdayFullZh(entry.Date), FontFamily = MonoFont, FontSize = 10, Foreground = Brush(th.Muted), Margin = new Thickness(0, 2, 0, 0) });
                headRow.Children.Add(tSp);
                chinMain.Children.Add(headRow);

                chinMain.Children.Add(BuildProgressTrack(prog, 4.5, th.Accent, th.Track));

                if (opts.IsList && ordered.Count > 1)
                {
                    var others = ordered.Skip(1).Take(3).Select(x => { bool n; string u; string d = EntryDayNumber(x, now, out n, out u); return x.Name + " " + d + (n ? "天" : ""); });
                    chinMain.Children.Add(new TextBlock { Text = "后续期待：" + string.Join("  ·  ", others), FontSize = 10.5, Foreground = Brush(th.Muted), Margin = new Thickness(0, 8, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
                }
            }
        }

        static FrameworkElement BuildShareCardVisual(List<ShareEntry> entries, CardOpts opts, string currentConfigSkin, out int width, out int height)
        {
            if (opts.RatioIndex == 1) { width = 540; height = 540; }
            else if (opts.RatioIndex == 2) { width = 800; height = 450; }
            else if (opts.RatioIndex == 3) { width = 540; height = 1170; }
            else { width = 540; height = 720; }

            int themeIdx = Math.Max(0, Math.Min(opts.TemplateIndex, CuratedThemes.Length - 1));
            var th = CuratedThemes[themeIdx];

            var root = new Grid { Width = width, Height = height, Background = Brush(th.BgTop), ClipToBounds = true, UseLayoutRounding = true };
            System.Windows.Documents.TextElement.SetFontFamily(root, SansFont);
            TextOptions.SetTextRenderingMode(root, TextRenderingMode.Grayscale);
            TextOptions.SetTextFormattingMode(root, TextFormattingMode.Ideal);
            RenderOptions.SetBitmapScalingMode(root, BitmapScalingMode.HighQuality);

            if (th.Id == "polaroid") BuildPolaroidPoster(root, width, height, entries, opts, th);
            else BuildRichBentoPoster(root, width, height, entries, opts, th);

            if (opts.RoundedCorners)
            {
                root.Clip = new RectangleGeometry(new Rect(0, 0, width, height), 24, 24);
            }
            return root;
        }

        public static RenderTargetBitmap RenderShareBitmap(List<ShareEntry> entries, CardOpts opts, string currentConfigSkin)
        {
            int w, h;
            var visual = BuildShareCardVisual(entries, opts, currentConfigSkin, out w, out h);
            visual.Measure(new Size(w, h));
            visual.Arrange(new Rect(0, 0, w, h));
            visual.UpdateLayout();
            var rtb = new RenderTargetBitmap(w * 2, h * 2, 192, 192, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }

        // =========================================================================
        // Clean 3-Step Share Atelier UI (Simple controls, rich shareable Bento cards!)
        // =========================================================================
        static FrameworkElement BuildShareAtelierView(List<ShareEntry> entries, int initialIndex, string currentConfigSkin, Window hostWin, out Action disposeTimers)
        {
            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.22, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(312) });

            // Left Clean Gallery Preview Stage
            var previewStage = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(24),
                AllowDrop = true
            };
            previewStage.SetResourceReference(Border.BackgroundProperty, "Field");
            Grid.SetColumn(previewStage, 0);
            body.Children.Add(previewStage);

            var previewFrame = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 30, ShadowDepth = 8, Direction = 270, Opacity = 0.22 }
            };
            previewStage.Child = new Viewbox { Stretch = Stretch.Uniform, Child = previewFrame };

            // Right Clean 3-Step Control Panel
            var controlPane = new Border { CornerRadius = new CornerRadius(14), Padding = new Thickness(18, 16, 18, 16), BorderThickness = new Thickness(1) };
            controlPane.SetResourceReference(Border.BackgroundProperty, "Surface");
            controlPane.SetResourceReference(Border.BorderBrushProperty, "Line");
            Grid.SetColumn(controlPane, 2);
            body.Children.Add(controlPane);

            var ctrlDock = new DockPanel();
            controlPane.Child = ctrlDock;

            // Bottom Actions
            var actionStack = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            DockPanel.SetDock(actionStack, Dock.Bottom);
            ctrlDock.Children.Add(actionStack);

            var copyBtn = ActionButton("复制图片到剪贴板", () => { });
            copyBtn.SetResourceReference(Button.BackgroundProperty, "Accent");
            copyBtn.Foreground = Brushes.White;
            copyBtn.BorderThickness = new Thickness(0);
            copyBtn.Padding = new Thickness(16, 10, 16, 10);
            copyBtn.Margin = new Thickness(0, 0, 0, 8);
            actionStack.Children.Add(copyBtn);

            var saveBtn = ActionButton("保存为高清 PNG…", () => { });
            saveBtn.Padding = new Thickness(16, 9, 16, 9);
            actionStack.Children.Add(saveBtn);

            var footMeta = new DockPanel { Margin = new Thickness(2, 10, 2, 0) };
            var wmChk = new CheckBox { Content = "角标", IsChecked = true, FontSize = 10.5 };
            DockPanel.SetDock(wmChk, Dock.Right);
            footMeta.Children.Add(wmChk);
            var statusHint = Label("", 10.5, "Muted");
            statusHint.VerticalAlignment = VerticalAlignment.Center;
            footMeta.Children.Add(statusHint);
            actionStack.Children.Add(footMeta);

            var form = new StackPanel();
            ctrlDock.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });

            bool isListMode = true; // Default to rich multi-day Bento timeline!
            int selTheme = 0;       // Default to iconic 松霜 · 冬序!
            int selRatio = 0;       // 0=3:4, 1=1:1, 3=9:19.5
            string customPhoto = null;
            Action refresh = null;

            Func<string[], int, Action<int>, Border> makeSegmented = (labels, initSel, onSelect) =>
            {
                var wrap = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(3), Margin = new Thickness(0, 0, 0, 12) };
                wrap.SetResourceReference(Border.BackgroundProperty, "Field");
                var g = new Grid();
                for (int i = 0; i < labels.Length; i++) g.ColumnDefinitions.Add(new ColumnDefinition());
                wrap.Child = g;
                var btns = new List<Button>();
                Action<int> hi = idx =>
                {
                    for (int k = 0; k < btns.Count; k++)
                    {
                        bool on = k == idx;
                        if (on)
                        {
                            btns[k].SetResourceReference(Button.BackgroundProperty, "Surface");
                            btns[k].SetResourceReference(Button.ForegroundProperty, "Ink");
                            btns[k].FontWeight = FontWeights.SemiBold;
                        }
                        else
                        {
                            btns[k].Background = Brushes.Transparent;
                            btns[k].SetResourceReference(Button.ForegroundProperty, "Muted");
                            btns[k].FontWeight = FontWeights.Normal;
                        }
                    }
                };
                for (int i = 0; i < labels.Length; i++)
                {
                    int cap = i;
                    var b = ActionButton(labels[i], () => { hi(cap); onSelect(cap); });
                    b.BorderThickness = new Thickness(0);
                    b.Padding = new Thickness(6, 6, 6, 6);
                    b.FontSize = 11.5;
                    Grid.SetColumn(b, i);
                    g.Children.Add(b);
                    btns.Add(b);
                }
                hi(initSel);
                return wrap;
            };

            // Step 1: Content Mode + Day Selector
            var modeCap = Label("内容", 11, "Muted"); modeCap.Margin = new Thickness(0, 0, 0, 6); form.Children.Add(modeCap);
            form.Children.Add(makeSegmented(new[] { "期待清单", "单个日子" }, 0, idx => { isListMode = idx == 0; if (refresh != null) refresh(); }));

            var dayPicker = new ComboBox { Margin = new Thickness(0, -4, 0, 14) };
            foreach (var e in entries) dayPicker.Items.Add(e.Name + "  (" + e.Date.ToString("yyyy-MM-dd") + ")");
            dayPicker.SelectedIndex = Math.Max(0, Math.Min(initialIndex, Math.Max(0, entries.Count - 1)));
            form.Children.Add(dayPicker);

            // Step 2: 6 Curated Visual Theme Cards (2 cols × 3 rows)
            var styleCap = Label("视觉风格", 11, "Muted"); styleCap.Margin = new Thickness(0, 0, 0, 7); form.Children.Add(styleCap);

            var themeGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            themeGrid.ColumnDefinitions.Add(new ColumnDefinition());
            themeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            themeGrid.ColumnDefinitions.Add(new ColumnDefinition());
            themeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            themeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            themeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.Children.Add(themeGrid);

            var themeCards = new List<Border>();
            var themeChecks = new List<Border>();
            Action syncThemes = () =>
            {
                for (int i = 0; i < themeCards.Count; i++)
                {
                    bool on = i == selTheme;
                    themeCards[i].BorderThickness = new Thickness(on ? 2 : 1);
                    themeCards[i].SetResourceReference(Border.BorderBrushProperty, on ? "Accent" : "Line");
                    themeChecks[i].Visibility = on ? Visibility.Visible : Visibility.Collapsed;
                }
            };

            for (int i = 0; i < CuratedThemes.Length; i++)
            {
                int cap = i;
                var th = CuratedThemes[i];
                var card = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(10, 9, 10, 9),
                    Margin = new Thickness(0, 0, 0, 8),
                    Cursor = Cursors.Hand,
                    Background = new LinearGradientBrush(C_(th.SwatchA), C_(th.SwatchB), new Point(0, 0), new Point(1, 1))
                };
                var inner = new Grid();
                card.Child = inner;

                var txtCol = new StackPanel();
                txtCol.Children.Add(new TextBlock { Text = th.Name, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Brush(th.IsLight ? "#1F1A16" : "#FFFFFF") });
                txtCol.Children.Add(new TextBlock { Text = th.Tag, FontSize = 9.5, Foreground = Brush(th.IsLight ? "#6E6256" : "#DAE4EE"), Margin = new Thickness(0, 2, 0, 0) });
                inner.Children.Add(txtCol);

                var chk = new Border
                {
                    Width = 16, Height = 16, CornerRadius = new CornerRadius(8),
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                    Background = Brush(th.IsLight ? "#B33428" : "#FFFFFF"),
                    Visibility = Visibility.Collapsed
                };
                chk.Child = new TextBlock { Text = "✓", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = Brush(th.IsLight ? "#FFFFFF" : "#12151C"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                inner.Children.Add(chk);

                card.MouseLeftButtonUp += (s, e) => { selTheme = cap; syncThemes(); if (refresh != null) refresh(); };
                Grid.SetRow(card, i / 2);
                Grid.SetColumn(card, (i % 2) * 2);
                themeGrid.Children.Add(card);
                themeCards.Add(card);
                themeChecks.Add(chk);
            }
            syncThemes();

            // Contextual Polaroid photo bar
            var photoBar = new DockPanel { Margin = new Thickness(0, 0, 0, 12), Visibility = Visibility.Collapsed };
            var clearPhotoBtn = ActionButton("恢复默认", () => { customPhoto = null; if (refresh != null) refresh(); });
            clearPhotoBtn.Padding = new Thickness(8, 5, 8, 5); clearPhotoBtn.FontSize = 10.5; clearPhotoBtn.Margin = new Thickness(6, 0, 0, 0);
            DockPanel.SetDock(clearPhotoBtn, Dock.Right); photoBar.Children.Add(clearPhotoBtn);
            var pickPhotoBtn = ActionButton("选择照片（或拖入左侧）…", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Title = "选择拍立得相纸照片", Filter = "图片文件|*.png;*.jpg;*.jpeg;*.webp;*.bmp" };
                if (dlg.ShowDialog(hostWin) == true) { customPhoto = dlg.FileName; if (refresh != null) refresh(); }
            });
            pickPhotoBtn.Padding = new Thickness(10, 6, 10, 6); pickPhotoBtn.FontSize = 11;
            photoBar.Children.Add(pickPhotoBtn);
            form.Children.Add(photoBar);

            // Step 3: Clean 3-Ratio Bar
            var ratioCap = Label("画幅", 11, "Muted"); ratioCap.Margin = new Thickness(0, 2, 0, 6); form.Children.Add(ratioCap);
            int[] uiRatios = { 0, 1, 3 };
            form.Children.Add(makeSegmented(new[] { "3:4 海报", "1:1 方卡", "手机锁屏" }, 0, idx => { selRatio = uiRatios[idx]; if (refresh != null) refresh(); }));

            // Quote + light inspiration button
            var quoteHead = new DockPanel { Margin = new Thickness(0, 2, 0, 6) };
            var quoteBox = new TextBox { Text = LiteraryQuotes[0], MaxLength = 50, Height = 34 };
            var diceBtn = ActionButton("换一句", () => { quoteBox.Text = PickNextQuote(quoteBox.Text); });
            diceBtn.Padding = new Thickness(6, 1, 6, 1); diceBtn.FontSize = 10.5; diceBtn.Background = Brushes.Transparent; diceBtn.BorderThickness = new Thickness(0);
            diceBtn.SetResourceReference(Button.ForegroundProperty, "Accent");
            DockPanel.SetDock(diceBtn, Dock.Right); quoteHead.Children.Add(diceBtn);
            quoteHead.Children.Add(Label("寄语", 11, "Muted"));
            form.Children.Add(quoteHead);
            form.Children.Add(quoteBox);

            Func<CardOpts> readOpts = () => new CardOpts
            {
                IsList = isListMode,
                EntryIndex = Math.Max(0, dayPicker.SelectedIndex),
                TemplateIndex = selTheme,
                RatioIndex = selRatio,
                SkinIndex = 0,
                Quote = quoteBox.Text,
                ShowDate = true,
                ShowWatermark = wmChk.IsChecked == true,
                PaperTexture = true,
                RoundedCorners = false,
                PhotoPath = customPhoto
            };

            refresh = () =>
            {
                dayPicker.Visibility = isListMode ? Visibility.Collapsed : Visibility.Visible;
                photoBar.Visibility = selTheme == 5 ? Visibility.Visible : Visibility.Collapsed;
                clearPhotoBtn.Visibility = string.IsNullOrEmpty(customPhoto) ? Visibility.Collapsed : Visibility.Visible;
                int w, h;
                previewFrame.Child = BuildShareCardVisual(entries, readOpts(), currentConfigSkin, out w, out h);
                statusHint.Text = (w * 2) + " × " + (h * 2) + " px";
            };

            previewStage.Drop += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        customPhoto = files[0];
                        selTheme = 5;
                        syncThemes();
                        refresh();
                    }
                }
            };

            dayPicker.SelectionChanged += (s, e) => refresh();
            wmChk.Checked += (s, e) => refresh();
            wmChk.Unchecked += (s, e) => refresh();

            var quoteDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            quoteDebounce.Tick += (s, e) => { quoteDebounce.Stop(); refresh(); };
            quoteBox.TextChanged += (s, e) => { quoteDebounce.Stop(); quoteDebounce.Start(); };

            var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            resetTimer.Tick += (s, e) => { resetTimer.Stop(); copyBtn.Content = "复制图片到剪贴板"; };

            copyBtn.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetImage(RenderShareBitmap(entries, readOpts(), currentConfigSkin));
                    copyBtn.Content = "已复制到剪贴板 ✓";
                    resetTimer.Stop(); resetTimer.Start();
                }
                catch (Exception ex) { MessageBox.Show(hostWin, "复制失败：" + ex.Message, "社交卡片工坊"); }
            };

            saveBtn.Click += (s, e) =>
            {
                try
                {
                    var opts = readOpts();
                    string baseName = opts.IsList ? "期待清单" : (entries.Count > 0 ? entries[Math.Max(0, Math.Min(opts.EntryIndex, entries.Count - 1))].Name : "倒计时");
                    foreach (char ch in System.IO.Path.GetInvalidFileNameChars()) baseName = baseName.Replace(ch, '_');
                    var dlg = new Microsoft.Win32.SaveFileDialog
                    {
                        Title = "保存社交卡片",
                        Filter = "PNG 图片|*.png",
                        FileName = "拾光-" + baseName + ".png",
                        InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                    };
                    if (dlg.ShowDialog(hostWin) == true)
                    {
                        var enc = new PngBitmapEncoder();
                        enc.Frames.Add(BitmapFrame.Create(RenderShareBitmap(entries, opts, currentConfigSkin)));
                        using (var fs = File.Create(dlg.FileName)) enc.Save(fs);
                    }
                }
                catch (Exception ex) { MessageBox.Show(hostWin, "保存失败：" + ex.Message, "社交卡片工坊"); }
            };

            disposeTimers = () => { quoteDebounce.Stop(); resetTimer.Stop(); };
            refresh();
            return body;
        }

        static void OpenShareCard(List<ShareEntry> entries, int initialIndex, string currentConfigSkin, bool editorIsLight)
        {
            if (entries == null || entries.Count == 0)
            {
                if (Editor != null) MessageBox.Show(Editor, "请先至少添加一个有效日期的日子。", "社交卡片工坊");
                else MessageBox.Show("请先至少添加一个有效日期的日子。", "社交卡片工坊");
                return;
            }
            if (ShareWin != null) { try { ShareWin.Close(); } catch { } ShareWin = null; }
            if (initialIndex < 0 || initialIndex >= entries.Count) initialIndex = 0;

            Window win;
            Border rootFrame;
            Action<bool> updateTheme;
            var outer = CreateStudioChrome("社交卡片工坊", 920, 660, editorIsLight, Editor, true, out win, "社交卡片工坊", out rootFrame, out updateTheme);

            Action disposeTimers;
            var content = BuildShareAtelierView(entries, initialIndex, currentConfigSkin, win, out disposeTimers);
            var padWrap = new Border { Margin = new Thickness(18, 4, 18, 18), Child = content };
            outer.Children.Add(padWrap);

            win.KeyDown += (s, e) => { if (e.Key == Key.Escape) win.Close(); };
            win.Closed += (s, e) => { disposeTimers(); ShareWin = null; };
            ShareWin = win;
            win.Show();
            win.Activate();
        }
    }
}
