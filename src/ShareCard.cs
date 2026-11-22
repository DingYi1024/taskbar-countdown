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
            public int TemplateIndex; // 0=流光极夜, 1=日落琥珀, 2=宣纸宋韵, 3=黑胶唱片, 4=青松杂志, 5=拍立得
            public int RatioIndex;    // 0=3:4, 1=1:1, 2=16:9, 3=9:19.5 Lockscreen
            public int SkinIndex;     // kept for backward compatibility with tests/callers
            public string Quote;
            public bool ShowDate = true;
            public bool ShowWatermark = true;
            public bool PaperTexture = true;
            public bool RoundedCorners = false;
            public string PhotoPath;
        }

        sealed class ArtTheme
        {
            public string Id, Name, Tag;
            public string BgTop, BgMid, BgBot;
            public string SwatchA, SwatchB;
            public string Ink, Muted, Accent, Hairline;
            public bool IsLight;
            public string BadgeText;
        }

        static readonly ArtTheme[] CuratedThemes = {
            new ArtTheme {
                Id = "aurora", Name = "流光 · 极夜", Tag = "深邃极光氛围",
                BgTop = "#0B1120", BgMid = "#101E36", BgBot = "#080D18",
                SwatchA = "#0D1A33", SwatchB = "#2C9E9A",
                Ink = "#F5F8FC", Muted = "#8EA4BF", Accent = "#5EEAD4", Hairline = "#24FFFFFF",
                IsLight = false, BadgeText = "AURORA"
            },
            new ArtTheme {
                Id = "sunset", Name = "日落 · 琥珀", Tag = "电影暮色渐变",
                BgTop = "#22142B", BgMid = "#8B3743", BgBot = "#E38B4F",
                SwatchA = "#5E2640", SwatchB = "#EB9A56",
                Ink = "#FFF8EF", Muted = "#F0C9B5", Accent = "#FFE09E", Hairline = "#2EFFFFFF",
                IsLight = false, BadgeText = "GOLDEN HOUR"
            },
            new ArtTheme {
                Id = "xuan", Name = "宣纸 · 宋韵", Tag = "东方朱砂画卷",
                BgTop = "#F7F2E6", BgMid = "#F2E9D7", BgBot = "#EAE0CA",
                SwatchA = "#F5EFE0", SwatchB = "#B83326",
                Ink = "#1E1916", Muted = "#7A6D60", Accent = "#B83326", Hairline = "#1E1E1916",
                IsLight = true, BadgeText = "墨韵"
            },
            new ArtTheme {
                Id = "vinyl", Name = "黑胶 · 唱片", Tag = "暗房复古辉光",
                BgTop = "#141312", BgMid = "#1B1917", BgBot = "#100F0E",
                SwatchA = "#1A1816", SwatchB = "#E09142",
                Ink = "#F6EFE4", Muted = "#9B9082", Accent = "#F0A44B", Hairline = "#22FFFFFF",
                IsLight = false, BadgeText = "SIDE A"
            },
            new ArtTheme {
                Id = "editorial", Name = "青松 · 杂志", Tag = "典雅国际排版",
                BgTop = "#15322C", BgMid = "#102621", BgBot = "#0B1A17",
                SwatchA = "#183B33", SwatchB = "#DEC082",
                Ink = "#F7F3E8", Muted = "#9EB8AE", Accent = "#E8CA86", Hairline = "#24FFFFFF",
                IsLight = false, BadgeText = "EDITION"
            },
            new ArtTheme {
                Id = "polaroid", Name = "拍立得 · 记忆", Tag = "支持拖入照片",
                BgTop = "#F9F5EE", BgMid = "#F5EFE4", BgBot = "#F0E8DA",
                SwatchA = "#475A7B", SwatchB = "#F5EFE4",
                Ink = "#201D1A", Muted = "#7E7469", Accent = "#C85A34", Hairline = "#1B201D1A",
                IsLight = true, BadgeText = "MEMORY"
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

        static string WeekdayZh(DateTime d)
        {
            string[] w = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            return w[(int)d.DayOfWeek];
        }

        static string MonthEn(int m)
        {
            string[] names = { "", "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
            return m >= 1 && m <= 12 ? names[m] : "";
        }

        static string EntryDayNumber(ShareEntry e, DateTime now, out bool isNum, out string unit)
        {
            if (e.IsCountup)
            {
                int elapsed = Math.Max(1, (now.Date - e.Date.Date).Days + 1);
                isNum = true;
                unit = "天";
                return elapsed.ToString(CultureInfo.InvariantCulture);
            }
            if (e.Date <= now)
            {
                isNum = false;
                unit = "";
                return "已抵达";
            }
            int d = (e.Date.Date - now.Date).Days;
            if (d == 0)
            {
                isNum = false;
                unit = "";
                return "就是今天";
            }
            isNum = true;
            unit = "天";
            return d.ToString(CultureInfo.InvariantCulture);
        }

        static List<ShareEntry> OrderEntries(List<ShareEntry> entries, DateTime now)
        {
            return entries
                .OrderBy(e => e.IsCountup ? 1 : (e.Date <= now ? 2 : 0))
                .ThenBy(e => e.IsCountup ? -Math.Abs((now.Date - e.Date.Date).Days) : (e.Date.Date - now.Date).Days)
                .ThenBy(e => e.Date)
                .ToList();
        }

        static double CalcProgress(ShareEntry e, DateTime now)
        {
            if (e.IsCountup) return Math.Min(1.0, ((now.Date - e.Date.Date).Days % 365) / 365.0);
            DateTime start = (e.StartDate > DateTime.MinValue && e.StartDate < e.Date) ? e.StartDate : e.Date.AddYears(-1);
            double total = Math.Max(1.0, (e.Date - start).TotalDays);
            return Math.Max(0.06, Math.Min(1.0, (now - start).TotalDays / total));
        }

        static void AddRadialGlow(Canvas canvas, double cx, double cy, double rx, double ry, string hexCore, byte alphaCore)
        {
            var c = C_(hexCore);
            var rad = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.5, 0.5),
                Center = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5
            };
            rad.GradientStops.Add(new GradientStop(Color.FromArgb(alphaCore, c.R, c.G, c.B), 0.0));
            rad.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(alphaCore * 0.42), c.R, c.G, c.B), 0.48));
            rad.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1.0));
            rad.Freeze();

            var el = new Ellipse { Width = rx * 2, Height = ry * 2, Fill = rad, IsHitTestVisible = false };
            Canvas.SetLeft(el, cx - rx);
            Canvas.SetTop(el, cy - ry);
            canvas.Children.Add(el);
        }

        static Grid BuildBackdropLayer(int w, int h, ArtTheme th)
        {
            var layer = new Grid { Width = w, Height = h, ClipToBounds = true, IsHitTestVisible = false };

            // Base rich multi-stop gradient
            var bg = new LinearGradientBrush { StartPoint = new Point(0.08, 0), EndPoint = new Point(0.92, 1) };
            bg.GradientStops.Add(new GradientStop(C_(th.BgTop), 0.0));
            bg.GradientStops.Add(new GradientStop(C_(th.BgMid), 0.54));
            bg.GradientStops.Add(new GradientStop(C_(th.BgBot), 1.0));
            bg.Freeze();
            layer.Background = bg;

            var art = new Canvas { Width = w, Height = h, IsHitTestVisible = false };
            layer.Children.Add(art);

            if (th.Id == "aurora")
            {
                AddRadialGlow(art, w * 0.85, h * 0.14, w * 0.68, w * 0.68, "#38D9C5", 105);
                AddRadialGlow(art, w * 0.12, h * 0.78, w * 0.62, w * 0.62, "#5E6AF5", 95);
                AddRadialGlow(art, w * 0.72, h * 0.60, w * 0.48, w * 0.48, "#A162F7", 62);
                // Delicate celestial orbital ring
                var orb = new Ellipse
                {
                    Width = w * 0.88, Height = w * 0.88,
                    Stroke = Brush("#16FFFFFF"), StrokeThickness = 1.0
                };
                Canvas.SetLeft(orb, w * 0.42);
                Canvas.SetTop(orb, -w * 0.18);
                art.Children.Add(orb);
            }
            else if (th.Id == "sunset")
            {
                AddRadialGlow(art, w * 0.76, h * 0.24, w * 0.60, w * 0.60, "#FFCB77", 120);
                AddRadialGlow(art, w * 0.20, h * 0.85, w * 0.65, w * 0.65, "#4E1D47", 110);
                // Glowing minimal solar disc
                var sun = new Ellipse
                {
                    Width = w * 0.34, Height = w * 0.34,
                    Fill = new RadialGradientBrush(Color.FromArgb(90, 255, 240, 210), Color.FromArgb(0, 255, 200, 130))
                };
                Canvas.SetLeft(sun, w * 0.62);
                Canvas.SetTop(sun, h * 0.12);
                art.Children.Add(sun);
            }
            else if (th.Id == "xuan")
            {
                // Subtle warm paper wash + vermilion moon/sun disc + ink-wash distant ridge
                AddRadialGlow(art, w * 0.78, h * 0.20, w * 0.24, w * 0.24, "#C43B2E", 34);
                var sunDisc = new Ellipse
                {
                    Width = w * 0.24, Height = w * 0.24,
                    Fill = Brush("#14B83326"),
                    Stroke = Brush("#1CB83326"),
                    StrokeThickness = 1.0
                };
                Canvas.SetLeft(sunDisc, w * 0.68);
                Canvas.SetTop(sunDisc, h * 0.14);
                art.Children.Add(sunDisc);

                // Soft ink-wash mountain wave along lower backdrop
                var waveGeo = Geometry.Parse(string.Format(CultureInfo.InvariantCulture,
                    "M 0,{0} C {1},{2} {3},{4} {5},{6} L {5},{7} L 0,{7} Z",
                    h * 0.84, w * 0.30, h * 0.76, w * 0.68, h * 0.90, w, h * 0.80, h));
                art.Children.Add(new System.Windows.Shapes.Path { Data = waveGeo, Fill = Brush("#0C241E1A") });

                // Top architectural vermilion accent bar
                var topAccent = new Rectangle { Width = w, Height = 6, Fill = Brush("#B83326") };
                Canvas.SetLeft(topAccent, 0);
                Canvas.SetTop(topAccent, 0);
                art.Children.Add(topAccent);
            }
            else if (th.Id == "vinyl")
            {
                // Off-center vinyl LP grooves in right-upper backdrop
                double discR = Math.Min(w, h) * 0.52;
                double cx = w * 0.92;
                double cy = h * 0.34;
                AddRadialGlow(art, cx, cy, discR * 1.15, discR * 1.15, "#E89B4C", 36);
                for (int i = 0; i < 11; i++)
                {
                    double r = discR * (0.36 + i * 0.062);
                    var ring = new Ellipse
                    {
                        Width = r * 2, Height = r * 2,
                        Stroke = Brush(i % 3 == 0 ? "#1CE8A85A" : "#12FFFFFF"),
                        StrokeThickness = i == 10 ? 1.5 : 0.9
                    };
                    Canvas.SetLeft(ring, cx - r);
                    Canvas.SetTop(ring, cy - r);
                    art.Children.Add(ring);
                }
                var labelCore = new Ellipse { Width = discR * 0.56, Height = discR * 0.56, Fill = Brush("#26E89B4C"), Stroke = Brush("#44E89B4C"), StrokeThickness = 1 };
                Canvas.SetLeft(labelCore, cx - discR * 0.28);
                Canvas.SetTop(labelCore, cy - discR * 0.28);
                art.Children.Add(labelCore);
            }
            else if (th.Id == "editorial")
            {
                AddRadialGlow(art, w * 0.84, h * 0.16, w * 0.58, w * 0.58, "#2C6B5B", 90);
                AddRadialGlow(art, w * 0.14, h * 0.88, w * 0.52, w * 0.52, "#D4AE6A", 42);
                // Crisp architectural frame inset
                var inset = new Rectangle
                {
                    Width = w - 36, Height = h - 36,
                    Stroke = Brush("#1EE8CA86"), StrokeThickness = 1.0
                };
                Canvas.SetLeft(inset, 18);
                Canvas.SetTop(inset, 18);
                art.Children.Add(inset);
            }

            // Always apply whisper-subtle analog tactile grain for print warmth
            layer.Children.Add(new Rectangle { Fill = PaperGrainBrush(th.IsLight), Opacity = th.IsLight ? 0.85 : 0.72, IsHitTestVisible = false });
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

        static FrameworkElement BuildFooter(string quote, bool showWatermark, ArtTheme th, bool serifQuote)
        {
            var foot = new DockPanel();
            if (showWatermark)
            {
                var wm = new TextBlock
                {
                    Text = "拾光",
                    FontFamily = SerifFont,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(th.Muted),
                    Opacity = 0.78,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(14, 0, 0, 0)
                };
                DockPanel.SetDock(wm, Dock.Right);
                foot.Children.Add(wm);
            }
            var qt = new TextBlock
            {
                Text = "“" + quote + "”",
                FontFamily = serifQuote ? SerifFont : SansFont,
                FontSize = 11.5,
                Foreground = Brush(th.Muted),
                Opacity = 0.92,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            foot.Children.Add(qt);
            return foot;
        }

        // Standard curated poster layout (handles Aurora / Sunset / Xuan / Vinyl / Editorial across Single, List, 3:4, 1:1, 16:9, and 9:19.5 Lockscreen!)
        static void BuildCuratedPoster(Grid root, int w, int h, List<ShareEntry> entries, CardOpts opts, ArtTheme th)
        {
            bool isSquare = opts.RatioIndex == 1;
            bool isWide = opts.RatioIndex == 2;
            bool isLock = opts.RatioIndex == 3;
            var now = DateTime.Now;
            string quote = string.IsNullOrWhiteSpace(opts.Quote) ? LiteraryQuotes[0] : opts.Quote.Trim();
            bool useSerifTitle = th.Id == "xuan" || th.Id == "sunset" || th.Id == "editorial";

            root.Children.Add(BuildBackdropLayer(w, h, th));

            // Balanced generous poster margins (lockscreen reserves top 29% cleanly for phone clock!)
            Thickness pad = isLock ? new Thickness(42, 336, 42, 86)
                : isWide ? new Thickness(48, 34, 48, 30)
                : isSquare ? new Thickness(40, 34, 40, 30)
                : new Thickness(44, 42, 44, 36);

            var content = new Grid { Margin = pad };
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Top eyebrow
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: Main artwork body
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: Footer quote
            root.Children.Add(content);

            // --- Row 0: Refined Minimal Header ---
            var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, isSquare ? 14 : 20) };
            Grid.SetRow(topBar, 0);
            content.Children.Add(topBar);

            if (th.Id == "xuan")
            {
                // Vermilion traditional rectangular seal on right
                var seal = new Border
                {
                    BorderBrush = Brush(th.Accent),
                    BorderThickness = new Thickness(1.3),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(7, 2.5, 7, 2.5),
                    VerticalAlignment = VerticalAlignment.Center
                };
                seal.Child = new TextBlock { Text = th.BadgeText, FontFamily = SerifFont, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brush(th.Accent) };
                DockPanel.SetDock(seal, Dock.Right);
                topBar.Children.Add(seal);
            }
            else
            {
                var dateStamp = new TextBlock
                {
                    Text = MonthEn(now.Month) + " " + now.Day.ToString("00") + " · " + WeekdayZh(now),
                    FontFamily = MonoFont,
                    FontSize = 10.5,
                    Foreground = Brush(th.Muted),
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(dateStamp, Dock.Right);
                topBar.Children.Add(dateStamp);
            }

            string leftEyebrow = th.Id == "xuan"
                ? (now.ToString("yyyy.MM.dd") + " · " + LunarHelper.SolarToFullLunar(now))
                : (th.BadgeText + " · " + now.Year);
            topBar.Children.Add(new TextBlock
            {
                Text = leftEyebrow,
                FontFamily = th.Id == "xuan" ? SerifFont : MonoFont,
                FontSize = 10.5,
                Foreground = Brush(th.Accent),
                Opacity = 0.92,
                VerticalAlignment = VerticalAlignment.Center
            });

            // --- Row 2: Clean Footer ---
            var footWrap = new StackPanel { Margin = new Thickness(0, isSquare ? 12 : 18, 0, 0) };
            Grid.SetRow(footWrap, 2);
            content.Children.Add(footWrap);
            footWrap.Children.Add(new Border { Height = 1, Background = Brush(th.Hairline), Margin = new Thickness(0, 0, 0, 12) });
            footWrap.Children.Add(BuildFooter(quote, opts.ShowWatermark, th, useSerifTitle));

            // --- Row 1: Main Visual Composition ---
            var body = new Grid { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(body, 1);
            content.Children.Add(body);

            var ordered = OrderEntries(entries, now);
            if (ordered.Count == 0) return;

            if (!opts.IsList)
            {
                // === SINGLE-DAY ART POSTER ===
                var entry = entries[Math.Max(0, Math.Min(opts.EntryIndex, entries.Count - 1))];
                bool isNum; string unit;
                string dayStr = EntryDayNumber(entry, now, out isNum, out unit);
                double prog = CalcProgress(entry, now);

                if (isWide)
                {
                    body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
                    body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

                    var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 24, 0) };
                    Grid.SetColumn(left, 0); body.Children.Add(left);

                    left.Children.Add(new Border { Width = 34, Height = 3, Background = Brush(th.Accent), CornerRadius = new CornerRadius(1.5), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 14) });
                    left.Children.Add(new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(entry.Name) ? "期待之日" : entry.Name,
                        FontFamily = useSerifTitle ? SerifFont : SansFont,
                        FontSize = 38,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brush(th.Ink),
                        TextWrapping = TextWrapping.Wrap
                    });
                    if (opts.ShowDate)
                    {
                        string dt = entry.Date.ToString("yyyy.MM.dd") + " · " + WeekdayZh(entry.Date);
                        if (!string.IsNullOrEmpty(entry.LunarLabel)) dt += " · " + entry.LunarLabel;
                        left.Children.Add(new TextBlock { Text = dt, FontFamily = MonoFont, FontSize = 11.5, Foreground = Brush(th.Muted), Margin = new Thickness(0, 10, 0, 0) });
                    }

                    var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(right, 1); body.Children.Add(right);
                    right.Children.Add(new TextBlock
                    {
                        Text = dayStr,
                        FontFamily = isNum ? NumFont : SerifFont,
                        FontSize = isNum ? 132 : 46,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brush(th.Id == "xuan" || th.Id == "vinyl" ? th.Accent : th.Ink),
                        LineHeight = isNum ? 118 : 52,
                        LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                        VerticalAlignment = VerticalAlignment.Bottom
                    });
                    if (isNum)
                    {
                        right.Children.Add(new TextBlock
                        {
                            Text = entry.IsCountup ? "天" : "天后",
                            FontFamily = useSerifTitle ? SerifFont : SansFont,
                            FontSize = 18,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = Brush(th.Accent),
                            VerticalAlignment = VerticalAlignment.Bottom,
                            Margin = new Thickness(8, 0, 0, 14)
                        });
                    }
                }
                else
                {
                    var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    body.Children.Add(stack);

                    // Accent lead line + subtle tag
                    var leadRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, isSquare ? 14 : 20) };
                    leadRow.Children.Add(new Border { Width = 28, Height = 3, Background = Brush(th.Accent), CornerRadius = new CornerRadius(1.5), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
                    leadRow.Children.Add(new TextBlock
                    {
                        Text = entry.IsCountup ? "已相伴走过" : (dayStr == "就是今天" ? "如约而至" : "距离期待之日"),
                        FontFamily = useSerifTitle ? SerifFont : SansFont,
                        FontSize = isLock ? 15 : 13,
                        Foreground = Brush(th.Muted),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    stack.Children.Add(leadRow);

                    // Event Title
                    stack.Children.Add(new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(entry.Name) ? "期待之日" : entry.Name,
                        FontFamily = useSerifTitle ? SerifFont : SansFont,
                        FontSize = isLock ? 46 : (isSquare ? 34 : 42),
                        FontWeight = FontWeights.Bold,
                        Foreground = Brush(th.Ink),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, isSquare ? 14 : 22)
                    });

                    // Giant Typographic Hero Numeral
                    double numSize = isLock ? 168 : (isSquare ? 124 : 156);
                    var heroRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, isSquare ? 20 : 30) };
                    heroRow.Children.Add(new TextBlock
                    {
                        Text = dayStr,
                        FontFamily = isNum ? NumFont : SerifFont,
                        FontSize = isNum ? numSize : (isSquare ? 48 : 58),
                        FontWeight = FontWeights.Bold,
                        Foreground = Brush(th.Id == "xuan" || th.Id == "vinyl" ? th.Accent : th.Ink),
                        LineHeight = isNum ? numSize * 0.88 : 62,
                        LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                        VerticalAlignment = VerticalAlignment.Bottom
                    });
                    if (isNum)
                    {
                        var unitCol = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(12, 0, 0, isSquare ? 10 : 14) };
                        unitCol.Children.Add(new TextBlock { Text = entry.IsCountup ? "DAYS" : "DAYS LEFT", FontFamily = MonoFont, FontSize = 10, Foreground = Brush(th.Muted) });
                        unitCol.Children.Add(new TextBlock { Text = entry.IsCountup ? "天" : "天后", FontFamily = useSerifTitle ? SerifFont : SansFont, FontSize = isLock ? 24 : 20, FontWeight = FontWeights.SemiBold, Foreground = Brush(th.Accent), Margin = new Thickness(0, 2, 0, 0) });
                        heroRow.Children.Add(unitCol);
                    }
                    stack.Children.Add(heroRow);

                    // Razor-thin refined progress hairline (no clunky box!)
                    var track = new Grid { Height = 3, Margin = new Thickness(0, 0, 0, 14) };
                    track.Children.Add(new Border { Background = Brush(th.Hairline), CornerRadius = new CornerRadius(1.5) });
                    var fillGrid = new Grid();
                    fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(prog, GridUnitType.Star) });
                    fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.01, 1.0 - prog), GridUnitType.Star) });
                    var fillBar = new Border { Background = Brush(th.Accent), CornerRadius = new CornerRadius(1.5) };
                    Grid.SetColumn(fillBar, 0);
                    fillGrid.Children.Add(fillBar);
                    track.Children.Add(fillGrid);
                    stack.Children.Add(track);

                    // Clean bottom meta strip (date + lunar + percentage — zero gray boxes!)
                    if (opts.ShowDate)
                    {
                        var metaDock = new DockPanel();
                        var pctTxt = new TextBlock { Text = ((int)Math.Round(prog * 100)) + "%", FontFamily = MonoFont, FontSize = isLock ? 13 : 11.5, FontWeight = FontWeights.SemiBold, Foreground = Brush(th.Accent) };
                        DockPanel.SetDock(pctTxt, Dock.Right);
                        metaDock.Children.Add(pctTxt);

                        string dt = entry.Date.ToString("yyyy.MM.dd") + " · " + WeekdayZh(entry.Date);
                        if (!string.IsNullOrEmpty(entry.LunarLabel)) dt += " · " + entry.LunarLabel;
                        metaDock.Children.Add(new TextBlock { Text = dt, FontFamily = MonoFont, FontSize = isLock ? 13 : 11.5, Foreground = Brush(th.Muted) });
                        stack.Children.Add(metaDock);
                    }
                }
            }
            else
            {
                // === EDITORIAL LIST POSTER (ZERO SaaS boxes, ZERO progress bars!) ===
                // Hero Focal Nearest Event on top + Ultra-clean typographic index below!
                var hero = ordered[0];
                var rest = ordered.Skip(1).Take(isSquare ? 2 : 3).ToList();

                bool hNum; string hUnit;
                string hStr = EntryDayNumber(hero, now, out hNum, out hUnit);

                var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                body.Children.Add(stack);

                // Top Hero Focus Block
                var heroDock = new DockPanel { Margin = new Thickness(0, 0, 0, isSquare ? 18 : 28) };
                stack.Children.Add(heroDock);

                var heroRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(16, 0, 0, 0) };
                double hFontSize = isLock ? 114 : (isSquare ? 78 : 96);
                heroRight.Children.Add(new TextBlock
                {
                    Text = hStr,
                    FontFamily = hNum ? NumFont : SerifFont,
                    FontSize = hNum ? hFontSize : 36,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brush(th.Id == "xuan" || th.Id == "vinyl" ? th.Accent : th.Ink),
                    LineHeight = hNum ? hFontSize * 0.88 : 40,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    VerticalAlignment = VerticalAlignment.Bottom
                });
                if (hNum)
                {
                    heroRight.Children.Add(new TextBlock
                    {
                        Text = "天",
                        FontFamily = useSerifTitle ? SerifFont : SansFont,
                        FontSize = isLock ? 20 : 16,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brush(th.Accent),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(5, 0, 0, 10)
                    });
                }
                DockPanel.SetDock(heroRight, Dock.Right);
                heroDock.Children.Add(heroRight);

                var heroLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
                heroLeft.Children.Add(new TextBlock
                {
                    Text = "NEXT · 最近期待",
                    FontFamily = MonoFont,
                    FontSize = isLock ? 11.5 : 10,
                    Foreground = Brush(th.Accent),
                    Margin = new Thickness(0, 0, 0, 8)
                });
                heroLeft.Children.Add(new TextBlock
                {
                    Text = hero.Name,
                    FontFamily = useSerifTitle ? SerifFont : SansFont,
                    FontSize = isLock ? 42 : (isSquare ? 30 : 36),
                    FontWeight = FontWeights.Bold,
                    Foreground = Brush(th.Ink),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                if (opts.ShowDate)
                {
                    heroLeft.Children.Add(new TextBlock
                    {
                        Text = hero.Date.ToString("yyyy.MM.dd") + " · " + WeekdayZh(hero.Date),
                        FontFamily = MonoFont,
                        FontSize = isLock ? 13 : 11.5,
                        Foreground = Brush(th.Muted),
                        Margin = new Thickness(0, 6, 0, 4)
                    });
                }
                heroDock.Children.Add(heroLeft);

                // Bold architectural accent rule separating Hero and Upcoming Index
                var rule = new Grid { Height = 2, Margin = new Thickness(0, 0, 0, isSquare ? 8 : 14) };
                rule.Children.Add(new Border { Background = Brush(th.Hairline) });
                rule.Children.Add(new Border { Width = 56, Background = Brush(th.Accent), HorizontalAlignment = HorizontalAlignment.Left });
                stack.Children.Add(rule);

                // Clean Frameless Editorial Index Rows
                for (int i = 0; i < rest.Count; i++)
                {
                    var it = rest[i];
                    bool rNum; string rUnit;
                    string rStr = EntryDayNumber(it, now, out rNum, out rUnit);

                    var rowBorder = new Border
                    {
                        BorderBrush = Brush(th.Hairline),
                        BorderThickness = new Thickness(0, 0, 0, i < rest.Count - 1 ? 1 : 0),
                        Padding = new Thickness(0, isLock ? 20 : (isSquare ? 13 : 17), 0, isLock ? 20 : (isSquare ? 13 : 17))
                    };
                    var row = new DockPanel();
                    rowBorder.Child = row;

                    // Right aligned crisp days
                    var rightVal = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    rightVal.Children.Add(new TextBlock
                    {
                        Text = rStr,
                        FontFamily = rNum ? NumFont : SerifFont,
                        FontSize = isLock ? 34 : (isSquare ? 24 : 28),
                        FontWeight = FontWeights.Bold,
                        Foreground = Brush(th.Ink),
                        VerticalAlignment = VerticalAlignment.Bottom
                    });
                    if (rNum)
                    {
                        rightVal.Children.Add(new TextBlock
                        {
                            Text = "天",
                            FontFamily = SansFont,
                            FontSize = isLock ? 13 : 11.5,
                            Foreground = Brush(th.Muted),
                            VerticalAlignment = VerticalAlignment.Bottom,
                            Margin = new Thickness(4, 0, 0, 4)
                        });
                    }
                    DockPanel.SetDock(rightVal, Dock.Right);
                    row.Children.Add(rightVal);

                    // Left index + title + date
                    var leftRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    leftRow.Children.Add(new TextBlock
                    {
                        Text = (i + 2).ToString("00"),
                        FontFamily = MonoFont,
                        FontSize = isLock ? 13.5 : 11.5,
                        Foreground = Brush(th.Accent),
                        VerticalAlignment = VerticalAlignment.Center,
                        Width = isLock ? 32 : 26
                    });
                    var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    nameStack.Children.Add(new TextBlock
                    {
                        Text = it.Name,
                        FontFamily = useSerifTitle ? SerifFont : SansFont,
                        FontSize = isLock ? 21 : (isSquare ? 16 : 18),
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brush(th.Ink),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });
                    if (opts.ShowDate)
                    {
                        nameStack.Children.Add(new TextBlock
                        {
                            Text = it.Date.ToString("yyyy.MM.dd") + " · " + WeekdayZh(it.Date),
                            FontFamily = MonoFont,
                            FontSize = isLock ? 11.5 : 10,
                            Foreground = Brush(th.Muted),
                            Margin = new Thickness(0, 2, 0, 0)
                        });
                    }
                    leftRow.Children.Add(nameStack);
                    row.Children.Add(leftRow);

                    stack.Children.Add(rowBorder);
                }
            }
        }

        // Curated Theme 5: Authentic Polaroid Photo Memory Card
        static void BuildPolaroidPoster(Grid root, int w, int h, List<ShareEntry> entries, CardOpts opts, ArtTheme th)
        {
            bool isSquare = opts.RatioIndex == 1;
            bool isWide = opts.RatioIndex == 2;
            bool isLock = opts.RatioIndex == 3;
            var now = DateTime.Now;
            string quote = string.IsNullOrWhiteSpace(opts.Quote) ? LiteraryQuotes[0] : opts.Quote.Trim();

            var ordered = OrderEntries(entries, now);
            var entry = (!opts.IsList && entries.Count > 0)
                ? entries[Math.Max(0, Math.Min(opts.EntryIndex, entries.Count - 1))]
                : (ordered.Count > 0 ? ordered[0] : new ShareEntry { Name = "期待之日", Date = now.AddDays(30) });

            bool isNum; string unit;
            string dayStr = EntryDayNumber(entry, now, out isNum, out unit);

            // Warm cream archival Polaroid stock
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
                    frame.Children.Add(new Rectangle
                    {
                        Fill = new ImageBrush(photoBmp) { Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center }
                    });
                    // Subtle film bottom vignette so overlaid text is always crisp
                    var vig = new LinearGradientBrush(Color.FromArgb(15, 0, 0, 0), Color.FromArgb(170, 12, 10, 10), new Point(0, 0.2), new Point(0, 1));
                    vig.Freeze();
                    frame.Children.Add(new Rectangle { Fill = vig });
                }
                else
                {
                    // Cinematic dusk mountain-horizon default art (gorgeous even before dragging a photo!)
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

                // Classic Contax orange-amber film date stamp in bottom-right
                frame.Children.Add(new TextBlock
                {
                    Text = "'" + now.ToString("yy MM dd"),
                    FontFamily = MonoFont,
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brush("#EEF2B26B"),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 16, 14)
                });

                // Hero big number on bottom-left of photo
                var hero = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(20, 0, 0, 14)
                };
                double pNumSize = isSquare ? 68 : 92;
                hero.Children.Add(new TextBlock
                {
                    Text = dayStr,
                    FontFamily = isNum ? NumFont : SerifFont,
                    FontSize = isNum ? pNumSize : 36,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    LineHeight = isNum ? pNumSize * 0.88 : 40,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    VerticalAlignment = VerticalAlignment.Bottom
                });
                if (isNum)
                {
                    hero.Children.Add(new TextBlock
                    {
                        Text = entry.IsCountup ? "天" : "天后",
                        FontFamily = SerifFont,
                        FontSize = 17,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brush("#EBFFFFFF"),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(7, 0, 0, 9)
                    });
                }
                frame.Children.Add(hero);
                return frame;
            };

            if (isWide)
            {
                var pad = new Grid { Margin = new Thickness(28) };
                pad.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
                pad.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                pad.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.95, GridUnitType.Star) });
                root.Children.Add(pad);

                var photoBox = new Border { CornerRadius = new CornerRadius(4), ClipToBounds = true, Child = buildPhotoWell() };
                Grid.SetColumn(photoBox, 0); pad.Children.Add(photoBox);

                var right = new DockPanel { Margin = new Thickness(0, 10, 4, 8) };
                Grid.SetColumn(right, 2); pad.Children.Add(right);

                var foot = BuildFooter(quote, opts.ShowWatermark, th, true);
                DockPanel.SetDock(foot, Dock.Bottom); right.Children.Add(foot);

                var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                right.Children.Add(mid);
                mid.Children.Add(new TextBlock { Text = entry.Name, FontFamily = SerifFont, FontSize = 32, FontWeight = FontWeights.Bold, Foreground = Brush(th.Ink), TextWrapping = TextWrapping.Wrap });
                if (opts.ShowDate)
                {
                    mid.Children.Add(new TextBlock { Text = entry.Date.ToString("yyyy.MM.dd") + " · " + WeekdayZh(entry.Date), FontFamily = MonoFont, FontSize = 11.5, Foreground = Brush(th.Muted), Margin = new Thickness(0, 10, 0, 0) });
                }
            }
            else
            {
                var pad = new Grid { Margin = isLock ? new Thickness(28, 300, 28, 64) : new Thickness(26, 26, 26, 24) };
                pad.RowDefinitions.Add(new RowDefinition { Height = new GridLength(isSquare ? 1.65 : 2.1, GridUnitType.Star) });
                pad.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
                root.Children.Add(pad);

                var photoBox = new Border { CornerRadius = new CornerRadius(4), ClipToBounds = true, Child = buildPhotoWell() };
                Grid.SetRow(photoBox, 0); pad.Children.Add(photoBox);

                var chin = new DockPanel { Margin = new Thickness(4, 20, 4, 2) };
                Grid.SetRow(chin, 1); pad.Children.Add(chin);

                var foot = BuildFooter(quote, opts.ShowWatermark, th, true);
                DockPanel.SetDock(foot, Dock.Bottom); chin.Children.Add(foot);

                var chinMain = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                chin.Children.Add(chinMain);

                chinMain.Children.Add(new TextBlock
                {
                    Text = entry.Name,
                    FontFamily = SerifFont,
                    FontSize = isSquare ? 26 : 32,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brush(th.Ink),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                if (opts.IsList && ordered.Count > 1)
                {
                    // Minimal one-line upcoming ribbon for Polaroid list mode (clean & uncluttered!)
                    var others = ordered.Skip(1).Take(3).Select(x =>
                    {
                        bool n; string u;
                        string d = EntryDayNumber(x, now, out n, out u);
                        return x.Name + " " + d + (n ? "天" : "");
                    });
                    chinMain.Children.Add(new TextBlock
                    {
                        Text = string.Join("  ·  ", others),
                        FontFamily = SansFont,
                        FontSize = 11.5,
                        Foreground = Brush(th.Muted),
                        Margin = new Thickness(0, 8, 0, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });
                }
                else if (opts.ShowDate)
                {
                    string meta = entry.Date.ToString("yyyy.MM.dd") + " · " + WeekdayZh(entry.Date);
                    if (!string.IsNullOrEmpty(entry.LunarLabel)) meta += " · " + entry.LunarLabel;
                    chinMain.Children.Add(new TextBlock { Text = meta, FontFamily = MonoFont, FontSize = 11, Foreground = Brush(th.Muted), Margin = new Thickness(0, 6, 0, 0) });
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
            else BuildCuratedPoster(root, width, height, entries, opts, th);

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
        // Minimalist Gallery-Grade Share Atelier (3 effortless steps, zero bloat!)
        // =========================================================================
        static FrameworkElement BuildShareAtelierView(List<ShareEntry> entries, int initialIndex, string currentConfigSkin, Window hostWin, out Action disposeTimers)
        {
            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.22, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(312) });

            // Left Clean Gallery Preview Stage (NO black cave, NO debug pill!)
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

            bool isListMode = false;
            int selTheme = 0; // Default to lush 流光 · 极夜 (instant wow!)
            int selRatio = 0; // 0=3:4, 1=1:1, 3=9:19.5
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
            form.Children.Add(makeSegmented(new[] { "单个日子", "近期清单" }, 0, idx => { isListMode = idx == 1; if (refresh != null) refresh(); }));

            var dayPicker = new ComboBox { Margin = new Thickness(0, -4, 0, 14) };
            foreach (var e in entries) dayPicker.Items.Add(e.Name + "  (" + e.Date.ToString("yyyy-MM-dd") + ")");
            dayPicker.SelectedIndex = Math.Max(0, Math.Min(initialIndex, Math.Max(0, entries.Count - 1)));
            form.Children.Add(dayPicker);

            // Step 2: 6 Curated Visual Art Theme Cards (2 cols × 3 rows — what you see is what you get!)
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
                txtCol.Children.Add(new TextBlock
                {
                    Text = th.Name,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(th.IsLight ? "#1F1A16" : "#FFFFFF")
                });
                txtCol.Children.Add(new TextBlock
                {
                    Text = th.Tag,
                    FontSize = 9.5,
                    Foreground = Brush(th.IsLight ? "#6E6256" : "#D4E0EE"),
                    Margin = new Thickness(0, 2, 0, 0)
                });
                inner.Children.Add(txtCol);

                var chk = new Border
                {
                    Width = 16, Height = 16, CornerRadius = new CornerRadius(8),
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                    Background = Brush(th.IsLight ? "#B83326" : "#FFFFFF"),
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

            // Contextual Polaroid photo bar (only visible when Polaroid theme is active)
            var photoBar = new DockPanel { Margin = new Thickness(0, 0, 0, 12), Visibility = Visibility.Collapsed };
            var clearPhotoBtn = ActionButton("恢复默认", () => { customPhoto = null; if (refresh != null) refresh(); });
            clearPhotoBtn.Padding = new Thickness(8, 5, 8, 5);
            clearPhotoBtn.FontSize = 10.5;
            clearPhotoBtn.Margin = new Thickness(6, 0, 0, 0);
            DockPanel.SetDock(clearPhotoBtn, Dock.Right);
            photoBar.Children.Add(clearPhotoBtn);
            var pickPhotoBtn = ActionButton("选择照片（或拖入左侧）…", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Title = "选择拍立得相纸照片", Filter = "图片文件|*.png;*.jpg;*.jpeg;*.webp;*.bmp" };
                if (dlg.ShowDialog(hostWin) == true) { customPhoto = dlg.FileName; if (refresh != null) refresh(); }
            });
            pickPhotoBtn.Padding = new Thickness(10, 6, 10, 6);
            pickPhotoBtn.FontSize = 11;
            photoBar.Children.Add(pickPhotoBtn);
            form.Children.Add(photoBar);

            // Step 3: Clean 3-Ratio Bar
            var ratioCap = Label("画幅", 11, "Muted"); ratioCap.Margin = new Thickness(0, 2, 0, 6); form.Children.Add(ratioCap);
            int[] uiRatios = { 0, 1, 3 };
            form.Children.Add(makeSegmented(new[] { "3:4 海报", "1:1 方卡", "手机锁屏" }, 0, idx => { selRatio = uiRatios[idx]; if (refresh != null) refresh(); }));

            // Quote line + light inspiration button
            var quoteHead = new DockPanel { Margin = new Thickness(0, 2, 0, 6) };
            var quoteBox = new TextBox { Text = LiteraryQuotes[0], MaxLength = 50, Height = 34 };
            var diceBtn = ActionButton("换一句", () => { quoteBox.Text = PickNextQuote(quoteBox.Text); });
            diceBtn.Padding = new Thickness(6, 1, 6, 1);
            diceBtn.FontSize = 10.5;
            diceBtn.Background = Brushes.Transparent;
            diceBtn.BorderThickness = new Thickness(0);
            diceBtn.SetResourceReference(Button.ForegroundProperty, "Accent");
            DockPanel.SetDock(diceBtn, Dock.Right);
            quoteHead.Children.Add(diceBtn);
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
