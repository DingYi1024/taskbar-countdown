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
            public bool Enabled;
        }

        public sealed class CardOpts
        {
            public bool IsList;
            public int EntryIndex;
            public int TemplateIndex; // 0 = Editorial, 1 = Atmosphere, 2 = Ticket
            public int RatioIndex;    // 0 = 3:4, 1 = 1:1, 2 = 16:9
            public int SkinIndex;     // 0 = follow, 1..10 = fixed presets in ShareSkinChoices
            public string Quote;
            public bool ShowDate;
            public bool ShowWatermark;
        }

        sealed class CardPalette
        {
            public Brush Text, Muted, Accent, Surface, SurfaceStrong, Hairline, Track, PillBg;
            public string DetailHex;
            public bool Light;
        }

        sealed class ShareSkinChoice
        {
            public string Label, SkinId;
            public bool Light;
        }

        static readonly FontFamily NumFont = new FontFamily("Bahnschrift, Segoe UI Variable Display, Segoe UI");
        static readonly FontFamily SerifFont = new FontFamily("Source Han Serif SC, Noto Serif SC, STSong, Georgia, Microsoft YaHei UI");
        static readonly FontFamily SansFont = new FontFamily("Microsoft YaHei UI, DengXian, Segoe UI");
        static readonly FontFamily MonoFont = new FontFamily("Consolas, Segoe UI");
        static DrawingBrush _cachedDotGrid;

        static readonly ShareSkinChoice[] ShareSkinChoices = {
            new ShareSkinChoice { Label = "跟随当前皮肤", SkinId = null, Light = false },
            new ShareSkinChoice { Label = "云白 · 纸感",   SkinId = "default",   Light = true },
            new ShareSkinChoice { Label = "石墨 · 暗夜",   SkinId = "default",   Light = false },
            new ShareSkinChoice { Label = "圣诞 · 松霜",   SkinId = "christmas", Light = false },
            new ShareSkinChoice { Label = "元旦 · 星序",   SkinId = "newyear",   Light = false },
            new ShareSkinChoice { Label = "春节 · 灯笺",   SkinId = "spring",    Light = true },
            new ShareSkinChoice { Label = "中秋 · 月泊",   SkinId = "midautumn", Light = false },
            new ShareSkinChoice { Label = "国庆 · 朱锦",   SkinId = "national",  Light = false },
            new ShareSkinChoice { Label = "奶油 · 暖麦",   SkinId = "cream",     Light = true },
            new ShareSkinChoice { Label = "晨雾 · 青瓷",   SkinId = "celadon",   Light = true },
            new ShareSkinChoice { Label = "极光 · 磨砂",   SkinId = "aurora",    Light = false }
        };

        static string ResolveCardSkin(int skinIndex, string currentConfigSkin, List<ShareEntry> entries, out bool isLight)
        {
            if (skinIndex > 0 && skinIndex < ShareSkinChoices.Length)
            {
                var ch = ShareSkinChoices[skinIndex];
                isLight = ch.Light;
                return ch.SkinId;
            }
            isLight = IsLight();
            if (string.IsNullOrEmpty(currentConfigSkin) || currentConfigSkin == "default" || currentConfigSkin == "minimal")
                return "default";
            if (currentConfigSkin == "auto")
            {
                var now = DateTime.Now;
                foreach (var e in entries.Where(x => x.Enabled && x.Date > now).OrderBy(x => x.Date))
                {
                    string f = FestivalForName(e.Name);
                    if (f != "default") return f;
                }
                return "default";
            }
            return currentConfigSkin;
        }

        static CardPalette BuildPalette(string skinId, SkinPalette sk, bool light, string detailHex)
        {
            return new CardPalette
            {
                Text = Brush(sk.Text),
                Muted = Brush(sk.Muted),
                Accent = Brush(sk.Accent),
                Surface = Brush(light ? "#70FFFFFF" : "#18FFFFFF"),
                SurfaceStrong = Brush(light ? "#AAFFFFFF" : "#26FFFFFF"),
                Hairline = Brush(skinId != "default" ? HexA("38", detailHex) : (light ? "#1E000000" : "#24FFFFFF")),
                Track = Brush(light ? "#1C000000" : "#24FFFFFF"),
                PillBg = Brush(HexA(light ? "1E" : "2B", detailHex)),
                DetailHex = detailHex,
                Light = light
            };
        }

        static string WeekdayZh(DateTime d)
        {
            string[] w = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
            return w[(int)d.DayOfWeek];
        }

        static string ShortWeekdayZh(DateTime d)
        {
            string[] w = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            return w[(int)d.DayOfWeek];
        }

        static string MonthEn(int m)
        {
            string[] ms = { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
            return ms[Math.Max(0, Math.Min(11, m - 1))];
        }

        static int IsoWeekOfYear(DateTime d)
        {
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(d, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        static string RelativeSpanTag(DateTime target, DateTime now)
        {
            if (target <= now) return "已抵达";
            int days = (target.Date - now.Date).Days;
            if (days == 0) return "就在今天";
            if (days == 1) return "就在明天";
            if (days < 7) return "本周将至";
            if (days <= 30) return "本月内";
            int months = Math.Max(1, (int)Math.Round(days / 30.0));
            if (months < 12) return "约 " + months + " 个月后";
            return "跨年期待";
        }

        static string DetailedDurationText(DateTime target, DateTime now)
        {
            if (target <= now) return "时间已抵达约定时刻";
            var span = target - now;
            int weeks = (int)span.TotalDays / 7;
            int remDays = (int)span.TotalDays % 7;
            if (weeks > 0) return weeks + " 周 " + remDays + " 天 " + span.Hours.ToString("00") + " 时";
            return (int)span.TotalDays + " 天 " + span.Hours.ToString("00") + " 时 " + span.Minutes.ToString("00") + " 分";
        }

        static string CompactWeekText(DateTime target, DateTime now)
        {
            if (target <= now) return "已到期";
            int days = (target.Date - now.Date).Days;
            if (days < 7) return days + " 天";
            int w = days / 7;
            int r = days % 7;
            return r == 0 ? (w + " 周") : (w + " 周 " + r + " 天");
        }

        static List<ShareEntry> OrderedEntries(List<ShareEntry> entries, DateTime now, out int maxDays)
        {
            var enabled = entries.Where(e => e.Enabled).ToList();
            if (enabled.Count == 0) enabled = entries.ToList();
            var upcoming = enabled.Where(e => e.Date > now).OrderBy(e => e.Date).ToList();
            var past = enabled.Where(e => e.Date <= now).OrderByDescending(e => e.Date).ToList();
            upcoming.AddRange(past);
            maxDays = upcoming.Where(e => e.Date > now)
                .Select(e => (int)Math.Ceiling((e.Date.Date - now.Date).TotalDays))
                .DefaultIfEmpty(100)
                .Max();
            return upcoming;
        }

        static double EntryHorizonRatio(DateTime target, DateTime now, int maxDays)
        {
            if (target <= now) return 1.0;
            double days = Math.Max(0, (target.Date - now.Date).TotalDays);
            double span = Math.Max(30.0, maxDays);
            return Math.Max(0.14, Math.Min(0.96, 1.0 - (days / (span * 1.18))));
        }

        static void AddCols(Grid grid, params GridLength[] cols)
        {
            foreach (var c in cols) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = c });
        }

        static RadialGradientBrush SoftGlow(string hex, byte centerAlpha)
        {
            Color c = C_(hex);
            var b = new RadialGradientBrush { GradientOrigin = new Point(0.5, 0.5), Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5 };
            b.GradientStops.Add(new GradientStop(Color.FromArgb(centerAlpha, c.R, c.G, c.B), 0.0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(centerAlpha / 3), c.R, c.G, c.B), 0.52));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1.0));
            b.Freeze();
            return b;
        }

        static DrawingBrush DotGridBrush()
        {
            if (_cachedDotGrid != null) return _cachedDotGrid;
            var dot = new SolidColorBrush(Color.FromArgb(12, 0, 0, 0));
            dot.Freeze();
            var dg = new DrawingGroup();
            dg.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 20, 20))));
            dg.Children.Add(new GeometryDrawing(dot, null, new EllipseGeometry(new Point(10, 10), 0.9, 0.9)));
            var db = new DrawingBrush(dg) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 20, 20), ViewportUnits = BrushMappingMode.Absolute };
            db.Freeze();
            _cachedDotGrid = db;
            return db;
        }

        static Grid BuildBackdrop(int width, int height, string skinId, bool light, bool withDots)
        {
            var spec = FindSkin(skinId);
            string oc1 = spec.Oc1, oc2 = spec.Oc2, g1 = spec.Glow1, g2 = spec.Glow2;
            if (spec.Id == "default" && light) { oc1 = "#E9EFF6"; oc2 = "#D8E2EE"; g1 = "#FFFFFF"; g2 = "#9CB6D4"; }

            var outerBg = new LinearGradientBrush(C_(oc1), C_(oc2), new Point(0, 0), new Point(1, 1));
            outerBg.Freeze();
            var root = new Grid { Width = width, Height = height, Background = outerBg, ClipToBounds = true, UseLayoutRounding = true };
            System.Windows.Documents.TextElement.SetFontFamily(root, SansFont);

            root.Children.Add(new Ellipse
            {
                Width = width * 1.15, Height = width * 1.15, Fill = SoftGlow(g1, 165),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(-width * 0.42, -width * 0.42, 0, 0)
            });
            root.Children.Add(new Ellipse
            {
                Width = width * 1.05, Height = width * 1.05, Fill = SoftGlow(g2, 95),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -width * 0.36, -width * 0.36)
            });
            if (withDots) root.Children.Add(new Rectangle { Fill = DotGridBrush() });
            return root;
        }

        static FrameworkElement BuildWatermark(bool show, Brush mutedBrush)
        {
            if (!show) return null;
            var wm = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.75 };
            wm.Children.Add(new Image { Source = Logo(), Width = 12, Height = 12, Margin = new Thickness(0, 0, 5, 0) });
            wm.Children.Add(new TextBlock { Text = "桌面倒计时", FontSize = 10, Foreground = mutedBrush, VerticalAlignment = VerticalAlignment.Center });
            return wm;
        }

        static DockPanel BuildQuoteFooterRow(string quote, bool quoteMarks, FontFamily font, double fontSize, bool showWatermark, CardPalette p, bool wrap)
        {
            var row = new DockPanel();
            var wm = BuildWatermark(showWatermark, p.Muted);
            if (wm != null) { DockPanel.SetDock(wm, Dock.Right); row.Children.Add(wm); }
            var tb = new TextBlock
            {
                Text = quoteMarks ? ("“" + quote + "”") : quote,
                FontFamily = font,
                FontSize = fontSize,
                Foreground = p.Muted,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            if (wrap) tb.TextWrapping = TextWrapping.Wrap;
            else tb.TextTrimming = TextTrimming.CharacterEllipsis;
            row.Children.Add(tb);
            return row;
        }

        static FrameworkElement BuildProgressTrack(double ratio, double height, Brush accentBrush, Brush trackBrush)
        {
            double pct = Math.Max(0.02, Math.Min(1.0, ratio)) * 100.0;
            var track = new Grid { Height = height };
            track.Children.Add(new Border { Background = trackBrush, CornerRadius = new CornerRadius(height / 2) });
            var barGrid = new Grid();
            AddCols(barGrid, new GridLength(pct, GridUnitType.Star), new GridLength(Math.Max(0.01, 100.0 - pct), GridUnitType.Star));
            var fill = new Border { Background = accentBrush, CornerRadius = new CornerRadius(height / 2) };
            Grid.SetColumn(fill, 0);
            barGrid.Children.Add(fill);
            track.Children.Add(barGrid);
            return track;
        }

        static double YearElapsedRatio(DateTime now, out int passedDays, out int leftDays)
        {
            var yearStart = new DateTime(now.Year, 1, 1);
            var yearEnd = new DateTime(now.Year + 1, 1, 1);
            int total = (int)(yearEnd - yearStart).TotalDays;
            passedDays = Math.Max(1, Math.Min(total, (int)(now.Date - yearStart).TotalDays + 1));
            leftDays = Math.Max(0, total - passedDays);
            return Math.Max(0, Math.Min(1.0, (now - yearStart).TotalDays / (yearEnd - yearStart).TotalDays));
        }

        static FrameworkElement BuildYearProgress(DateTime now, CardPalette p, bool compact)
        {
            int passed, left;
            double ratio = YearElapsedRatio(now, out passed, out left);
            var box = new StackPanel();
            var top = new DockPanel { Margin = new Thickness(0, 0, 0, compact ? 5 : 6) };
            box.Children.Add(top);
            var pctTxt = new TextBlock { Text = (ratio * 100.0).ToString("0.0", CultureInfo.InvariantCulture) + "%", FontFamily = NumFont, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = p.Text };
            DockPanel.SetDock(pctTxt, Dock.Right);
            top.Children.Add(pctTxt);
            top.Children.Add(new TextBlock
            {
                Text = compact ? (now.Year + " 年度进度") : (now.Year + " 年度进度 · 已过 " + passed + " 天 · 余 " + left + " 天"),
                FontSize = 10.5, Foreground = p.Muted
            });
            box.Children.Add(BuildProgressTrack(ratio, 4, p.Accent, p.Track));
            return box;
        }

        static FrameworkElement BuildCalendarBadge(DateTime now, CardPalette p)
        {
            var badge = new Border
            {
                CornerRadius = new CornerRadius(8), Background = p.SurfaceStrong, BorderBrush = p.Hairline,
                BorderThickness = new Thickness(1), Padding = new Thickness(9, 5, 9, 5), VerticalAlignment = VerticalAlignment.Center
            };
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = MonthEn(now.Month) + " · " + ShortWeekdayZh(now), FontFamily = MonoFont, FontSize = 8.5, Foreground = p.Accent, HorizontalAlignment = HorizontalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = now.Day.ToString("00"), FontFamily = NumFont, FontSize = 17, FontWeight = FontWeights.Bold, Foreground = p.Text, HorizontalAlignment = HorizontalAlignment.Center, LineHeight = 19, Margin = new Thickness(0, 1, 0, 0) });
            badge.Child = sp;
            return badge;
        }

        static FrameworkElement BuildAmbientArt(string skinId, string detailHex, double size, double opacity)
        {
            FrameworkElement unused;
            var deco = CreateDecor(skinId, out unused);
            if (deco != null) return new Viewbox { Width = size, Height = size, Stretch = Stretch.Uniform, Opacity = opacity, IsHitTestVisible = false, Child = deco };

            var cv = new Canvas { Width = 120, Height = 120, Opacity = opacity, IsHitTestVisible = false };
            var ringBrush = Brush(detailHex);
            var e1 = new Ellipse { Width = 110, Height = 110, Stroke = ringBrush, StrokeThickness = 1.0, StrokeDashArray = new DoubleCollection { 3, 4 } };
            Canvas.SetLeft(e1, 5); Canvas.SetTop(e1, 5); cv.Children.Add(e1);
            var e2 = new Ellipse { Width = 76, Height = 76, Stroke = ringBrush, StrokeThickness = 0.8 };
            Canvas.SetLeft(e2, 22); Canvas.SetTop(e2, 22); cv.Children.Add(e2);
            cv.Children.Add(new Polygon { Points = new PointCollection { new Point(92, 16), new Point(94, 22), new Point(100, 24), new Point(94, 26), new Point(92, 32), new Point(90, 26), new Point(84, 24), new Point(90, 22) }, Fill = ringBrush });
            return new Viewbox { Width = size, Height = size, Stretch = Stretch.Uniform, Child = cv };
        }

        static FrameworkElement BuildSealStamp(string skinId, string detailHex)
        {
            string text = FindSkin(skinId).Seal;
            if (string.IsNullOrEmpty(text)) return null;
            return new Border
            {
                BorderBrush = Brush(HexA("99", detailHex)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 3, 7, 3),
                Background = Brush(HexA("18", detailHex)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = text, FontFamily = SerifFont, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brush(detailHex) }
            };
        }

        static FrameworkElement BuildPill(string text, CardPalette p, bool strong)
        {
            return new Border
            {
                Background = strong ? p.PillBg : p.Surface,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(7, 2.5, 7, 2.5),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = text, FontSize = 9.5, FontWeight = strong ? FontWeights.SemiBold : FontWeights.Normal, Foreground = strong ? p.Accent : p.Muted }
            };
        }

        static FrameworkElement BuildStatTile(string label, string value, string sub, CardPalette p)
        {
            var tile = new Border { Background = p.Surface, BorderBrush = p.Hairline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10, 12, 10) };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = label, FontSize = 9.5, Foreground = p.Muted });
            sp.Children.Add(new TextBlock { Text = value, FontFamily = NumFont, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = p.Text, Margin = new Thickness(0, 3, 0, 1), TextTrimming = TextTrimming.CharacterEllipsis });
            sp.Children.Add(new TextBlock { Text = sub, FontSize = 9.5, Foreground = p.Accent, TextTrimming = TextTrimming.CharacterEllipsis });
            tile.Child = sp;
            return tile;
        }

        static FrameworkElement BuildInsightRow(List<ShareEntry> ordered, DateTime now, int shownCount, CardPalette p, bool showDate)
        {
            int passed, left;
            double yrRatio = YearElapsedRatio(now, out passed, out left);
            var grid = new Grid();
            AddCols(grid, new GridLength(1.15, GridUnitType.Star), new GridLength(10), new GridLength(1.0, GridUnitType.Star));

            var yrTile = new Border { Background = p.Surface, BorderBrush = p.Hairline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10, 12, 10) };
            var yrSp = new StackPanel();
            var yrTop = new DockPanel();
            var pct = new TextBlock { Text = (yrRatio * 100.0).ToString("0.0", CultureInfo.InvariantCulture) + "%", FontFamily = NumFont, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = p.Text };
            DockPanel.SetDock(pct, Dock.Right); yrTop.Children.Add(pct);
            yrTop.Children.Add(new TextBlock { Text = now.Year + " 年度进度", FontSize = 10, Foreground = p.Muted, VerticalAlignment = VerticalAlignment.Center });
            yrSp.Children.Add(yrTop);
            var track = BuildProgressTrack(yrRatio, 4, p.Accent, p.Track);
            track.Margin = new Thickness(0, 6, 0, 5);
            yrSp.Children.Add(track);
            yrSp.Children.Add(new TextBlock { Text = "已过 " + passed + " 天 · 余 " + left + " 天", FontSize = 9.5, Foreground = p.Muted });
            yrTile.Child = yrSp;
            Grid.SetColumn(yrTile, 0); grid.Children.Add(yrTile);

            string spanTitle = "期待概览", spanVal = ordered.Count + " 个日程", spanSub = "最近节点已就绪";
            var upcoming = ordered.Where(e => e.Date > now).ToList();
            if (upcoming.Count >= 2)
            {
                int firstD = (int)Math.Ceiling((upcoming[0].Date.Date - now.Date).TotalDays);
                int lastD = (int)Math.Ceiling((upcoming[upcoming.Count - 1].Date.Date - now.Date).TotalDays);
                spanTitle = "日程跨度";
                spanVal = firstD + " — " + lastD + " 天";
                int extra = ordered.Count - shownCount;
                spanSub = extra > 0 ? ("另有 " + extra + " 个待办日程") : (showDate ? (upcoming[0].Date.ToString("MM.dd") + " 至 " + upcoming[upcoming.Count - 1].Date.ToString("MM.dd")) : ("共 " + ordered.Count + " 个期待日"));
            }
            else if (upcoming.Count == 1)
            {
                spanTitle = "最近期待";
                spanVal = upcoming[0].Name;
                spanSub = RelativeSpanTag(upcoming[0].Date, now);
            }

            var spanTile = BuildStatTile(spanTitle, spanVal, spanSub, p);
            Grid.SetColumn(spanTile, 2); grid.Children.Add(spanTile);
            return grid;
        }

        static double HeroNumberSize(int ratioIndex, string dayStr)
        {
            double baseSize = ratioIndex == 1 ? 84 : (ratioIndex == 2 ? 88 : 114);
            if (dayStr.Length >= 4) return baseSize * 0.72;
            if (dayStr.Length == 3) return baseSize * 0.86;
            return baseSize;
        }

        static FrameworkElement BuildHeroDayMetric(string dayStr, bool isNum, double numSize, string unitLabel, FontFamily unitFont, double unitSize, Thickness unitMargin, string zeroText, string pastText, FontFamily statusFont, double statusSize, CardPalette p, bool center, bool stackUnit)
        {
            if (!isNum)
            {
                return new TextBlock
                {
                    Text = dayStr == "今天" ? zeroText : pastText,
                    FontFamily = statusFont,
                    FontSize = statusSize,
                    FontWeight = FontWeights.Bold,
                    Foreground = dayStr == "今天" ? p.Accent : p.Muted,
                    HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left
                };
            }
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left
            };
            row.Children.Add(new TextBlock { Text = dayStr, FontFamily = NumFont, FontSize = numSize, FontWeight = FontWeights.Bold, Foreground = p.Text, LineHeight = numSize });
            if (stackUnit)
            {
                var u = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = unitMargin };
                u.Children.Add(new TextBlock { Text = "DAYS", FontFamily = MonoFont, FontSize = 10, Foreground = p.Muted });
                u.Children.Add(new TextBlock { Text = unitLabel, FontFamily = unitFont, FontSize = unitSize, FontWeight = FontWeights.SemiBold, Foreground = p.Accent });
                row.Children.Add(u);
            }
            else
            {
                row.Children.Add(new TextBlock { Text = unitLabel, FontFamily = unitFont, FontSize = unitSize, FontWeight = FontWeights.SemiBold, Foreground = p.Accent, VerticalAlignment = VerticalAlignment.Bottom, Margin = unitMargin });
            }
            return row;
        }

        static Border BuildCardShell(Grid root, Thickness margin, double cornerRadius, SkinPalette sk, CardPalette p, bool glassStyle)
        {
            var cardBg = glassStyle
                ? new LinearGradientBrush(C_(HexA("F0", sk.Bg1)), C_(HexA("F8", sk.Bg2)), new Point(0, 0), new Point(1, 1))
                : new LinearGradientBrush(C_(sk.Bg1), C_(sk.Bg2), new Point(0, 0), new Point(0, 1));
            cardBg.Freeze();

            var borderGrad = new LinearGradientBrush(
                p.Light ? Color.FromArgb(165, 255, 255, 255) : Color.FromArgb(90, 255, 255, 255),
                p.Light ? Color.FromArgb(30, 0, 0, 0) : Color.FromArgb(20, 255, 255, 255),
                new Point(0, 0), new Point(1, 1));
            borderGrad.Freeze();

            root.Children.Add(new Border
            {
                Margin = margin, CornerRadius = new CornerRadius(cornerRadius), Background = cardBg,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 42, ShadowDepth = 12, Direction = 270, Opacity = p.Light ? 0.16 : 0.34 }
            });

            var card = new Border
            {
                Margin = margin, CornerRadius = new CornerRadius(cornerRadius), Background = cardBg,
                BorderBrush = glassStyle ? (Brush)borderGrad : p.Hairline, BorderThickness = new Thickness(glassStyle ? 1.3 : 1.0)
            };
            root.Children.Add(card);
            return card;
        }

        static FrameworkElement BuildHeroFocusTile(ShareEntry item, DateTime now, int ratioIndex, bool showDate, string skinId, CardPalette p, bool withArt)
        {
            string dayStr = DayText(item.Date, now, false);
            bool isNum = dayStr != "今天" && dayStr != "已到期";

            var hero = new Border { Background = p.SurfaceStrong, BorderBrush = p.Hairline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), ClipToBounds = true };
            var innerGrid = new Grid();
            hero.Child = innerGrid;

            if (withArt)
            {
                innerGrid.Children.Add(new Border
                {
                    Child = BuildAmbientArt(skinId, p.DetailHex, ratioIndex == 2 ? 110 : 126, skinId == "default" ? 0.14 : 0.22),
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, -14, -18)
                });
            }
            innerGrid.Children.Add(new Border { Width = 3.5, HorizontalAlignment = HorizontalAlignment.Left, Background = p.Accent, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 12, 0, 12) });

            var pad = new Border { Padding = new Thickness(ratioIndex == 2 ? 16 : 18, ratioIndex == 2 ? 12 : 14, ratioIndex == 2 ? 16 : 18, ratioIndex == 2 ? 12 : 14) };
            innerGrid.Children.Add(pad);
            var stack = new StackPanel(); pad.Child = stack;

            var topDock = new DockPanel(); stack.Children.Add(topDock);
            var rightNum = new Border { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            rightNum.Child = BuildHeroDayMetric(dayStr, isNum, ratioIndex == 2 ? 36 : 44, "天", SansFont, 12, new Thickness(4, 0, 0, 7), dayStr, dayStr, SansFont, 20, p, false, false);
            DockPanel.SetDock(rightNum, Dock.Right); topDock.Children.Add(rightNum);

            var leftInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; topDock.Children.Add(leftInfo);
            var badges = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            badges.Children.Add(BuildPill("NEXT · 最近期待", p, true));
            var rel = BuildPill(RelativeSpanTag(item.Date, now), p, false);
            rel.Margin = new Thickness(6, 0, 0, 0);
            badges.Children.Add(rel);
            leftInfo.Children.Add(badges);

            leftInfo.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(item.Name) ? "倒计时" : item.Name, FontSize = ratioIndex == 2 ? 18 : 20, FontWeight = FontWeights.Bold, Foreground = p.Text, TextTrimming = TextTrimming.CharacterEllipsis });
            string subLine = showDate ? (item.Date.ToString("yyyy.MM.dd") + " · " + WeekdayZh(item.Date)) : DetailedDurationText(item.Date, now);
            leftInfo.Children.Add(new TextBlock { Text = subLine, FontSize = 10.5, Foreground = p.Muted, Margin = new Thickness(0, 3, 0, 0) });

            var meter = BuildProgressTrack(EntryHorizonRatio(item.Date, now, 120), 3.5, p.Accent, p.Track);
            meter.Margin = new Thickness(0, 10, 0, 0);
            stack.Children.Add(meter);
            return hero;
        }

        static void PopulateScheduleCards(StackPanel container, List<ShareEntry> items, int startIndex, DateTime now, int ratioIndex, bool showDate, CardPalette p, bool compact, int maxDays)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                string dText = DayText(item.Date, now, false);
                bool isNum = dText != "今天" && dText != "已到期";

                var card = new Border
                {
                    Background = p.Surface, BorderBrush = p.Hairline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(13, compact ? 8 : 10, 13, compact ? 8 : 10),
                    Margin = new Thickness(0, 0, 0, i == items.Count - 1 ? 0 : (compact ? 6 : 8))
                };
                var stack = new StackPanel(); card.Child = stack;
                var row = new DockPanel(); stack.Children.Add(row);

                var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
                DockPanel.SetDock(right, Dock.Right); row.Children.Add(right);
                if (isNum)
                {
                    right.Children.Add(new TextBlock { Text = dText, FontFamily = NumFont, FontSize = compact ? 20 : 23, FontWeight = FontWeights.Bold, Foreground = p.Text, VerticalAlignment = VerticalAlignment.Bottom });
                    right.Children.Add(new TextBlock { Text = "天", FontSize = 10, Foreground = p.Accent, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(3, 0, 0, 3) });
                }
                else
                {
                    right.Children.Add(new TextBlock { Text = dText, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = dText == "今天" ? p.Accent : p.Muted });
                }

                var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; row.Children.Add(left);
                var titleLine = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                titleLine.Children.Add(new TextBlock { Text = (startIndex + i + 1).ToString("00"), FontFamily = MonoFont, FontSize = 10, Foreground = p.Accent, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                titleLine.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(item.Name) ? "倒计时" : item.Name, FontSize = compact ? 13 : 14, FontWeight = FontWeights.SemiBold, Foreground = p.Text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = ratioIndex == 2 ? 190 : 210 });
                titleLine.Children.Add(new Border
                {
                    Background = p.PillBg, CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 1.5, 6, 1.5),
                    Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = RelativeSpanTag(item.Date, now), FontSize = 9, Foreground = p.Accent }
                });
                left.Children.Add(titleLine);

                if (showDate) left.Children.Add(new TextBlock { Text = item.Date.ToString("yyyy.MM.dd") + " · " + WeekdayZh(item.Date), FontSize = 10, Foreground = p.Muted, Margin = new Thickness(19, 2, 0, 0) });
                if (!compact)
                {
                    var meter = BuildProgressTrack(EntryHorizonRatio(item.Date, now, maxDays), 2.5, p.Accent, p.Track);
                    meter.Margin = new Thickness(19, 7, 0, 0);
                    stack.Children.Add(meter);
                }
                container.Children.Add(card);
            }
        }

        static FrameworkElement BuildBentoEventTile(ShareEntry item, int seq, DateTime now, bool showDate, CardPalette p, int maxDays)
        {
            string dText = DayText(item.Date, now, false);
            bool isNum = dText != "今天" && dText != "已到期";

            var tile = new Border { Background = p.Surface, BorderBrush = p.Hairline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(13, 11, 13, 11) };
            var dock = new DockPanel(); tile.Child = dock;

            var top = new DockPanel(); DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top);
            var rel = BuildPill(RelativeSpanTag(item.Date, now), p, true);
            DockPanel.SetDock(rel, Dock.Right); top.Children.Add(rel);
            top.Children.Add(new TextBlock { Text = seq.ToString("00"), FontFamily = MonoFont, FontSize = 10, Foreground = p.Muted, VerticalAlignment = VerticalAlignment.Center });

            var bottom = new StackPanel { Margin = new Thickness(0, 8, 0, 0) }; DockPanel.SetDock(bottom, Dock.Bottom); dock.Children.Add(bottom);
            if (showDate) bottom.Children.Add(new TextBlock { Text = item.Date.ToString("MM.dd") + " · " + ShortWeekdayZh(item.Date), FontSize = 9.5, Foreground = p.Muted, Margin = new Thickness(0, 0, 0, 5) });
            bottom.Children.Add(BuildProgressTrack(EntryHorizonRatio(item.Date, now, maxDays), 3, p.Accent, p.Track));

            var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) }; dock.Children.Add(mid);
            mid.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(item.Name) ? "倒计时" : item.Name, FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = p.Text, TextTrimming = TextTrimming.CharacterEllipsis });
            var metric = BuildHeroDayMetric(dText, isNum, 30, "天", SansFont, 11, new Thickness(4, 0, 0, 4), dText, dText, SansFont, 18, p, false, false);
            metric.Margin = new Thickness(0, isNum ? 3 : 6, 0, 0);
            mid.Children.Add(metric);
            return tile;
        }

        static FrameworkElement BuildSingleSpecRow(ShareEntry entry, DateTime now, CardOpts opts, CardPalette p, bool compact)
        {
            int passed, left;
            double yrRatio = YearElapsedRatio(now, out passed, out left);
            var grid = new Grid();
            AddCols(grid, new GridLength(1, GridUnitType.Star), new GridLength(8), new GridLength(1, GridUnitType.Star), new GridLength(8), new GridLength(1, GridUnitType.Star));

            var t1 = BuildStatTile(opts.ShowDate ? "目标日期" : "目标星期", opts.ShowDate ? entry.Date.ToString("yyyy.MM.dd") : WeekdayZh(entry.Date), opts.ShowDate ? WeekdayZh(entry.Date) : ("第 " + IsoWeekOfYear(entry.Date) + " 周"), p);
            Grid.SetColumn(t1, 0); grid.Children.Add(t1);

            var t2 = BuildStatTile("距离换算", CompactWeekText(entry.Date, now), entry.Date > now ? ("约 " + ((int)Math.Ceiling((entry.Date.Date - now.Date).TotalDays) * 24).ToString("N0", CultureInfo.InvariantCulture) + " 小时") : "已抵达", p);
            Grid.SetColumn(t2, 2); grid.Children.Add(t2);

            var t3 = BuildStatTile(now.Year + " 年度进度", (yrRatio * 100.0).ToString("0.0", CultureInfo.InvariantCulture) + "%", "余 " + left + " 天跨年", p);
            Grid.SetColumn(t3, 4); grid.Children.Add(t3);

            if (compact) grid.Margin = new Thickness(0, 8, 0, 0);
            return grid;
        }

        static void BuildEditorialCard(Grid root, int w, int h, List<ShareEntry> entries, CardOpts opts, string skinId, SkinPalette sk, CardPalette p)
        {
            var margin = opts.RatioIndex == 2 ? new Thickness(54, 38, 54, 38) : opts.RatioIndex == 1 ? new Thickness(44) : new Thickness(46, 48, 46, 48);
            var card = BuildCardShell(root, margin, 18, sk, p, false);
            var innerPad = new Border { Padding = opts.RatioIndex == 2 ? new Thickness(28, 22, 28, 20) : opts.RatioIndex == 1 ? new Thickness(24, 22, 24, 20) : new Thickness(28, 26, 28, 22) };
            card.Child = innerPad;

            var now = DateTime.Now;
            string quote = string.IsNullOrWhiteSpace(opts.Quote) ? "安静地记录时间，认真地期待下一次相逢。" : opts.Quote.Trim();

            if (!opts.IsList && entries.Count > 0)
            {
                var entry = entries[Math.Max(0, Math.Min(opts.EntryIndex, entries.Count - 1))];
                string dayStr = DayText(entry.Date, now, false);
                bool isNum = dayStr != "今天" && dayStr != "已到期";

                if (opts.RatioIndex == 2)
                {
                    var cols = new Grid();
                    AddCols(cols, new GridLength(1.15, GridUnitType.Star), new GridLength(24), new GridLength(1.0, GridUnitType.Star));
                    innerPad.Child = cols;

                    var left = new DockPanel(); Grid.SetColumn(left, 0); cols.Children.Add(left);
                    var topBar = new DockPanel(); DockPanel.SetDock(topBar, Dock.Top); left.Children.Add(topBar);
                    var seal = BuildSealStamp(skinId, p.DetailHex);
                    if (seal != null) { DockPanel.SetDock(seal, Dock.Right); topBar.Children.Add(seal); }
                    topBar.Children.Add(new TextBlock { Text = MonthEn(now.Month) + " " + now.Day.ToString("00") + " · " + now.Year + " · W" + IsoWeekOfYear(now), FontFamily = MonoFont, FontSize = 10, Foreground = p.Muted, VerticalAlignment = VerticalAlignment.Center });

                    var bottom = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
                    DockPanel.SetDock(bottom, Dock.Bottom); left.Children.Add(bottom);
                    bottom.Children.Add(BuildYearProgress(now, p, false));
                    var bRow = BuildQuoteFooterRow(quote, true, SerifFont, 11.5, opts.ShowWatermark, p, false);
                    bRow.Margin = new Thickness(0, 10, 0, 0);
                    bottom.Children.Add(bRow);

                    var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; left.Children.Add(mid);
                    mid.Children.Add(BuildPill("UPCOMING · 倒数聚焦", p, true));
                    mid.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(entry.Name) ? "倒计时" : entry.Name, FontFamily = SerifFont, FontSize = 30, FontWeight = FontWeights.Bold, Foreground = p.Text, TextWrapping = TextWrapping.Wrap, MaxHeight = 82, Margin = new Thickness(0, 8, 0, 0) });
                    string meta = opts.ShowDate ? (entry.Date.ToString("yyyy.MM.dd") + " · " + WeekdayZh(entry.Date) + " · " + CompactWeekText(entry.Date, now)) : DetailedDurationText(entry.Date, now);
                    mid.Children.Add(new TextBlock { Text = meta, FontSize = 11, Foreground = p.Muted, Margin = new Thickness(0, 8, 0, 0) });

                    var sep = new Border { Width = 1, Background = p.Hairline, HorizontalAlignment = HorizontalAlignment.Center };
                    Grid.SetColumn(sep, 1); cols.Children.Add(sep);

                    var right = new Grid(); Grid.SetColumn(right, 2); cols.Children.Add(right);
                    right.Children.Add(new Border { Child = BuildAmbientArt(skinId, p.DetailHex, 150, skinId == "default" ? 0.14 : 0.22), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, -10, -10) });
                    var numBox = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                    right.Children.Add(numBox);
                    if (isNum) numBox.Children.Add(new TextBlock { Text = "DAYS REMAINING", FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Muted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) });
                    numBox.Children.Add(BuildHeroDayMetric(dayStr, isNum, HeroNumberSize(2, dayStr), "天", SerifFont, 18, new Thickness(6, 0, 0, 14), "就是今天", "已如约而至", SerifFont, 32, p, true, false));
                }
                else
                {
                    var layout = new DockPanel(); innerPad.Child = layout;
                    var topBlock = new StackPanel(); DockPanel.SetDock(topBlock, Dock.Top); layout.Children.Add(topBlock);
                    var topBar = new DockPanel(); topBlock.Children.Add(topBar);
                    var calBadge = BuildCalendarBadge(now, p); DockPanel.SetDock(calBadge, Dock.Right); topBar.Children.Add(calBadge);
                    var seal = BuildSealStamp(skinId, p.DetailHex);
                    if (seal != null) { seal.Margin = new Thickness(0, 0, 8, 0); DockPanel.SetDock(seal, Dock.Right); topBar.Children.Add(seal); }

                    var headLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    headLeft.Children.Add(new TextBlock { Text = "DAILY FOCUS · " + now.Year + " 第 " + IsoWeekOfYear(now) + " 周", FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Accent, Margin = new Thickness(0, 0, 0, 3) });
                    headLeft.Children.Add(new TextBlock { Text = "距离期待之日", FontFamily = SerifFont, FontSize = 18, FontWeight = FontWeights.Bold, Foreground = p.Text });
                    topBar.Children.Add(headLeft);
                    topBlock.Children.Add(new Border { Height = 1, Background = p.Hairline, Margin = new Thickness(0, 12, 0, 0) });

                    var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
                    footer.Children.Add(BuildSingleSpecRow(entry, now, opts, p, false));
                    footer.Children.Add(new Border { Height = 1, Background = p.Hairline, Margin = new Thickness(0, 14, 0, 10) });
                    footer.Children.Add(BuildQuoteFooterRow(quote, true, SerifFont, 11.5, opts.ShowWatermark, p, true));

                    var centerGrid = new Grid(); layout.Children.Add(centerGrid);
                    centerGrid.Children.Add(new Border { Child = BuildAmbientArt(skinId, p.DetailHex, opts.RatioIndex == 1 ? 145 : 190, skinId == "default" ? 0.14 : 0.24), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, -4, 0) });

                    var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
                    centerGrid.Children.Add(center);
                    center.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 10), Child = BuildPill("NEXT MILESTONE · " + RelativeSpanTag(entry.Date, now), p, true) });
                    center.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(entry.Name) ? "倒计时" : entry.Name, FontFamily = SerifFont, FontSize = opts.RatioIndex == 1 ? 26 : 34, FontWeight = FontWeights.Bold, Foreground = p.Text, TextWrapping = TextWrapping.Wrap, MaxWidth = w - 180 });

                    var metric = BuildHeroDayMetric(dayStr, isNum, HeroNumberSize(opts.RatioIndex, dayStr), "天后", SerifFont, 16, new Thickness(8, 0, 0, opts.RatioIndex == 1 ? 12 : 16), "就是今天", "已如约而至", SerifFont, opts.RatioIndex == 1 ? 32 : 40, p, false, true);
                    metric.Margin = isNum ? new Thickness(0, opts.RatioIndex == 1 ? 8 : 12, 0, 0) : new Thickness(0, 18, 0, 10);
                    center.Children.Add(metric);

                    if (isNum && opts.RatioIndex == 0)
                    {
                        var horizonWrap = new StackPanel { Margin = new Thickness(0, 14, 0, 0), Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
                        var hLabels = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
                        var rLbl = new TextBlock { Text = opts.ShowDate ? entry.Date.ToString("MM.dd") : "目标日", FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Accent };
                        DockPanel.SetDock(rLbl, Dock.Right); hLabels.Children.Add(rLbl);
                        hLabels.Children.Add(new TextBlock { Text = "TODAY " + now.ToString("MM.dd"), FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Muted });
                        horizonWrap.Children.Add(hLabels);
                        horizonWrap.Children.Add(BuildProgressTrack(EntryHorizonRatio(entry.Date, now, 120), 4, p.Accent, p.Track));
                        center.Children.Add(horizonWrap);
                    }
                }
            }
            else
            {
                int maxDays;
                var ordered = OrderedEntries(entries, now, out maxDays);
                if (opts.RatioIndex == 2)
                {
                    var visible = ordered.Take(4).ToList();
                    var cols = new Grid();
                    AddCols(cols, new GridLength(0.95, GridUnitType.Star), new GridLength(22), new GridLength(1.15, GridUnitType.Star));
                    innerPad.Child = cols;

                    var left = new DockPanel(); Grid.SetColumn(left, 0); cols.Children.Add(left);
                    var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(topBar, Dock.Top); left.Children.Add(topBar);
                    var seal = BuildSealStamp(skinId, p.DetailHex);
                    if (seal != null) { DockPanel.SetDock(seal, Dock.Right); topBar.Children.Add(seal); }
                    var titleSp = new StackPanel();
                    titleSp.Children.Add(new TextBlock { Text = "TIMELINE · " + now.Year + " · 共 " + ordered.Count + " 项", FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Accent });
                    titleSp.Children.Add(new TextBlock { Text = "值得期待的日子", FontFamily = SerifFont, FontSize = 21, FontWeight = FontWeights.Bold, Foreground = p.Text, Margin = new Thickness(0, 2, 0, 0) });
                    topBar.Children.Add(titleSp);

                    var bottom = new StackPanel { Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(bottom, Dock.Bottom); left.Children.Add(bottom);
                    bottom.Children.Add(BuildYearProgress(now, p, true));
                    var bRow = BuildQuoteFooterRow(quote, true, SerifFont, 11, opts.ShowWatermark, p, false);
                    bRow.Margin = new Thickness(0, 8, 0, 0);
                    bottom.Children.Add(bRow);

                    if (visible.Count > 0)
                    {
                        var hero = BuildHeroFocusTile(visible[0], now, 2, opts.ShowDate, skinId, p, true);
                        hero.VerticalAlignment = VerticalAlignment.Center;
                        left.Children.Add(hero);
                    }

                    var sep = new Border { Width = 1, Background = p.Hairline, HorizontalAlignment = HorizontalAlignment.Center };
                    Grid.SetColumn(sep, 1); cols.Children.Add(sep);

                    var rightStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(rightStack, 2); cols.Children.Add(rightStack);
                    var rest = visible.Skip(1).ToList();
                    if (rest.Count > 0) PopulateScheduleCards(rightStack, rest, 1, now, 2, opts.ShowDate, p, false, maxDays);
                    else rightStack.Children.Add(new TextBlock { Text = "只保留这一个最期待的节点。", FontSize = 12, Foreground = p.Muted, HorizontalAlignment = HorizontalAlignment.Center });
                }
                else
                {
                    int maxTotal = opts.RatioIndex == 1 ? 4 : 5;
                    var visible = ordered.Take(maxTotal).ToList();
                    var rest = visible.Skip(1).ToList();
                    bool compactRows = rest.Count >= 4 || opts.RatioIndex == 1;

                    var layout = new DockPanel(); innerPad.Child = layout;
                    var headerBlock = new StackPanel(); DockPanel.SetDock(headerBlock, Dock.Top); layout.Children.Add(headerBlock);
                    var header = new DockPanel(); headerBlock.Children.Add(header);
                    var calBadge = BuildCalendarBadge(now, p); DockPanel.SetDock(calBadge, Dock.Right); header.Children.Add(calBadge);
                    var seal = BuildSealStamp(skinId, p.DetailHex);
                    if (seal != null) { seal.Margin = new Thickness(0, 0, 8, 0); DockPanel.SetDock(seal, Dock.Right); header.Children.Add(seal); }

                    var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    titleStack.Children.Add(new TextBlock { Text = "TIMELINE · " + now.Year + " 第 " + IsoWeekOfYear(now) + " 周 · 共 " + ordered.Count + " 个期待", FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Accent, Margin = new Thickness(0, 0, 0, 3) });
                    titleStack.Children.Add(new TextBlock { Text = "值得期待的日子", FontFamily = SerifFont, FontSize = opts.RatioIndex == 1 ? 20 : 22, FontWeight = FontWeights.Bold, Foreground = p.Text });
                    header.Children.Add(titleStack);
                    headerBlock.Children.Add(new Border { Height = 1, Background = p.Hairline, Margin = new Thickness(0, 11, 0, 0) });

                    var footer = new StackPanel { Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
                    footer.Children.Add((opts.RatioIndex == 0 && rest.Count <= 3)
                        ? BuildInsightRow(ordered, now, visible.Count, p, opts.ShowDate)
                        : BuildYearProgress(now, p, opts.RatioIndex == 1));
                    footer.Children.Add(new Border { Height = 1, Background = p.Hairline, Margin = new Thickness(0, 11, 0, 9) });
                    footer.Children.Add(BuildQuoteFooterRow(quote, true, SerifFont, 11.5, opts.ShowWatermark, p, true));

                    var bodyStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; layout.Children.Add(bodyStack);
                    if (visible.Count > 0) bodyStack.Children.Add(BuildHeroFocusTile(visible[0], now, opts.RatioIndex, opts.ShowDate, skinId, p, true));
                    if (rest.Count > 0)
                    {
                        var secHeader = new DockPanel { Margin = new Thickness(2, 12, 2, 7) };
                        var countTxt = new TextBlock { Text = "按时间排序", FontSize = 9.5, Foreground = p.Accent, HorizontalAlignment = HorizontalAlignment.Right };
                        DockPanel.SetDock(countTxt, Dock.Right); secHeader.Children.Add(countTxt);
                        secHeader.Children.Add(new TextBlock { Text = "后续期待日程", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = p.Muted });
                        bodyStack.Children.Add(secHeader);
                        PopulateScheduleCards(bodyStack, rest, 1, now, opts.RatioIndex, opts.ShowDate, p, compactRows, maxDays);
                    }
                }
            }
        }

        static void BuildAtmosphereCard(Grid root, int w, int h, List<ShareEntry> entries, CardOpts opts, string skinId, SkinPalette sk, CardPalette p)
        {
            var margin = opts.RatioIndex == 2 ? new Thickness(54, 38, 54, 38) : opts.RatioIndex == 1 ? new Thickness(44) : new Thickness(46, 48, 46, 48);
            var card = BuildCardShell(root, margin, 22, sk, p, true);

            double innerW = Math.Max(1, w - margin.Left - margin.Right - 2.6);
            double innerH = Math.Max(1, h - margin.Top - margin.Bottom - 2.6);
            var innerClip = new RectangleGeometry(new Rect(0, 0, innerW, innerH), 20.5, 20.5);
            innerClip.Freeze();
            var cardGrid = new Grid { ClipToBounds = true, Clip = innerClip };
            card.Child = cardGrid;

            double artSize = opts.RatioIndex == 2 ? 220 : (opts.RatioIndex == 1 ? 210 : 265);
            cardGrid.Children.Add(new Border
            {
                Child = BuildAmbientArt(skinId, p.DetailHex, artSize, skinId == "default" ? 0.11 : 0.17),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -artSize * 0.12, -artSize * 0.10)
            });
            cardGrid.Children.Add(new Ellipse
            {
                Width = 280, Height = 280, Fill = SoftGlow(p.DetailHex, skinId == "default" ? (byte)32 : (byte)55),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -70, -70), IsHitTestVisible = false
            });

            var contentPad = new Border { Padding = opts.RatioIndex == 2 ? new Thickness(30, 22, 30, 20) : opts.RatioIndex == 1 ? new Thickness(24, 22, 24, 20) : new Thickness(28, 26, 28, 22) };
            cardGrid.Children.Add(contentPad);

            var now = DateTime.Now;
            string quote = string.IsNullOrWhiteSpace(opts.Quote) ? "安静地记录时间，认真地期待下一次相逢。" : opts.Quote.Trim();

            if (!opts.IsList && entries.Count > 0)
            {
                var entry = entries[Math.Max(0, Math.Min(opts.EntryIndex, entries.Count - 1))];
                string dayStr = DayText(entry.Date, now, false);
                bool isNum = dayStr != "今天" && dayStr != "已到期";

                var layout = new DockPanel(); contentPad.Child = layout;
                var topBar = new DockPanel(); DockPanel.SetDock(topBar, Dock.Top); layout.Children.Add(topBar);
                var calBadge = BuildCalendarBadge(now, p); DockPanel.SetDock(calBadge, Dock.Right); topBar.Children.Add(calBadge);
                var seal = BuildSealStamp(skinId, p.DetailHex);
                topBar.Children.Add(seal ?? BuildPill("COUNTDOWN · " + now.Year, p, true));

                var footer = new StackPanel { Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
                footer.Children.Add(BuildSingleSpecRow(entry, now, opts, p, false));
                footer.Children.Add(new Border { Height = 1, Background = p.Hairline, Margin = new Thickness(0, 12, 0, 10) });
                footer.Children.Add(BuildQuoteFooterRow(quote, false, SansFont, 11.5, opts.ShowWatermark, p, true));

                if (opts.RatioIndex == 2)
                {
                    var mid = new Grid(); layout.Children.Add(mid);
                    AddCols(mid, new GridLength(1.15, GridUnitType.Star), new GridLength(1, GridUnitType.Star));

                    var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(left, 0); mid.Children.Add(left);
                    left.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(entry.Name) ? "倒计时" : entry.Name, FontSize = 30, FontWeight = FontWeights.Bold, Foreground = p.Text, TextWrapping = TextWrapping.Wrap });
                    left.Children.Add(new TextBlock { Text = DetailedDurationText(entry.Date, now), FontSize = 11.5, Foreground = p.Accent, Margin = new Thickness(0, 8, 0, 0) });

                    var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }; Grid.SetColumn(right, 1); mid.Children.Add(right);
                    right.Children.Add(BuildHeroDayMetric(dayStr, isNum, 80, "天", SansFont, 18, new Thickness(6, 0, 0, 14), "就是今天", "已如约而至", SansFont, 32, p, true, false));
                }
                else
                {
                    var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }; layout.Children.Add(center);
                    center.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(entry.Name) ? "倒计时" : entry.Name, FontSize = opts.RatioIndex == 1 ? 26 : 31, FontWeight = FontWeights.Bold, Foreground = p.Text, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = w - 180 });
                    center.Children.Add(new Border { Width = 34, Height = 2.5, CornerRadius = new CornerRadius(1.2), Background = p.Accent, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 14), Opacity = 0.9 });
                    var metric = BuildHeroDayMetric(dayStr, isNum, HeroNumberSize(opts.RatioIndex, dayStr), "天", SansFont, 19, new Thickness(8, 0, 0, 16), "就是今天", "已如约而至", SansFont, opts.RatioIndex == 1 ? 34 : 40, p, true, false);
                    center.Children.Add(metric);
                    var pill = BuildPill(RelativeSpanTag(entry.Date, now) + " · " + DetailedDurationText(entry.Date, now), p, true);
                    pill.Margin = new Thickness(0, 14, 0, 0);
                    center.Children.Add(pill);
                }
            }
            else
            {
                int maxDays;
                var ordered = OrderedEntries(entries, now, out maxDays);
                var layout = new DockPanel(); contentPad.Child = layout;

                var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) }; DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header);
                var rightPill = BuildPill(ordered.Count + " 个期待日 · " + now.ToString("MM.dd"), p, true);
                DockPanel.SetDock(rightPill, Dock.Right); header.Children.Add(rightPill);
                var seal = BuildSealStamp(skinId, p.DetailHex);
                if (seal != null) { seal.Margin = new Thickness(0, 0, 8, 0); DockPanel.SetDock(seal, Dock.Right); header.Children.Add(seal); }
                header.Children.Add(new TextBlock { Text = "值得期待的日子", FontSize = opts.RatioIndex == 1 ? 20 : 22, FontWeight = FontWeights.Bold, Foreground = p.Text, VerticalAlignment = VerticalAlignment.Center });

                var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
                footer.Children.Add(new Border { Height = 1, Background = p.Hairline, Margin = new Thickness(0, 0, 0, 9) });
                footer.Children.Add(BuildQuoteFooterRow(quote, false, SansFont, 11.5, opts.ShowWatermark, p, true));

                if (opts.RatioIndex == 2)
                {
                    var cols = new Grid(); layout.Children.Add(cols);
                    AddCols(cols, new GridLength(0.95, GridUnitType.Star), new GridLength(14), new GridLength(1.2, GridUnitType.Star));

                    var leftCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(leftCol, 0); cols.Children.Add(leftCol);
                    if (ordered.Count > 0) leftCol.Children.Add(BuildHeroFocusTile(ordered[0], now, 2, opts.ShowDate, skinId, p, false));
                    leftCol.Children.Add(new Border { Background = p.Surface, BorderBrush = p.Hairline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(0, 10, 0, 0), Child = BuildYearProgress(now, p, false) });

                    var rightGrid = new Grid { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(rightGrid, 2); cols.Children.Add(rightGrid);
                    AddCols(rightGrid, new GridLength(1, GridUnitType.Star), new GridLength(10), new GridLength(1, GridUnitType.Star));
                    rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
                    rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    var bentoRest = ordered.Skip(1).Take(4).ToList();
                    for (int i = 0; i < bentoRest.Count; i++)
                    {
                        var tile = BuildBentoEventTile(bentoRest[i], i + 2, now, opts.ShowDate, p, maxDays);
                        Grid.SetRow(tile, (i / 2) * 2); Grid.SetColumn(tile, (i % 2) * 2); rightGrid.Children.Add(tile);
                    }
                }
                else
                {
                    var body = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; layout.Children.Add(body);
                    if (ordered.Count > 0) body.Children.Add(BuildHeroFocusTile(ordered[0], now, opts.RatioIndex, opts.ShowDate, skinId, p, false));

                    var bentoRest = ordered.Skip(1).Take(opts.RatioIndex == 1 ? 2 : 4).ToList();
                    if (bentoRest.Count > 0)
                    {
                        var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
                        AddCols(grid, new GridLength(1, GridUnitType.Star), new GridLength(10), new GridLength(1, GridUnitType.Star));
                        bool needInsightTile = (bentoRest.Count % 2 == 1) && opts.RatioIndex == 0;
                        int rows = (bentoRest.Count + (needInsightTile ? 1 : 0) + 1) / 2;
                        for (int r = 0; r < rows; r++)
                        {
                            if (r > 0) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
                            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        }
                        for (int i = 0; i < bentoRest.Count; i++)
                        {
                            var tile = BuildBentoEventTile(bentoRest[i], i + 2, now, opts.ShowDate, p, maxDays);
                            Grid.SetRow(tile, (i / 2) * 2); Grid.SetColumn(tile, (i % 2) * 2); grid.Children.Add(tile);
                        }
                        if (needInsightTile)
                        {
                            int passed, left;
                            double yrRatio = YearElapsedRatio(now, out passed, out left);
                            var yrTile = new Border { Background = p.Surface, BorderBrush = p.Hairline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(13, 11, 13, 11) };
                            var yd = new DockPanel(); yrTile.Child = yd;
                            var yTop = new TextBlock { Text = now.Year + " 年度进度", FontSize = 10, Foreground = p.Muted };
                            DockPanel.SetDock(yTop, Dock.Top); yd.Children.Add(yTop);
                            var yBot = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
                            DockPanel.SetDock(yBot, Dock.Bottom); yd.Children.Add(yBot);
                            yBot.Children.Add(new TextBlock { Text = "已过 " + passed + " 天 · 余 " + left + " 天", FontSize = 9.5, Foreground = p.Muted, Margin = new Thickness(0, 0, 0, 5) });
                            yBot.Children.Add(BuildProgressTrack(yrRatio, 3, p.Accent, p.Track));
                            yd.Children.Add(new TextBlock { Text = (yrRatio * 100.0).ToString("0.0", CultureInfo.InvariantCulture) + "%", FontFamily = NumFont, FontSize = 28, FontWeight = FontWeights.Bold, Foreground = p.Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) });
                            Grid.SetRow(yrTile, (bentoRest.Count / 2) * 2); Grid.SetColumn(yrTile, (bentoRest.Count % 2) * 2); grid.Children.Add(yrTile);
                        }
                        body.Children.Add(grid);
                    }

                    if (bentoRest.Count % 2 == 0 || opts.RatioIndex == 1)
                    {
                        body.Children.Add(new Border
                        {
                            Background = p.Surface, BorderBrush = p.Hairline, BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(10), Padding = new Thickness(13, 9, 13, 9), Margin = new Thickness(0, 10, 0, 0),
                            Child = BuildYearProgress(now, p, false)
                        });
                    }
                }
            }
        }

        static FrameworkElement BuildBarcode(Brush brush, double height)
        {
            int[] widths = { 2, 1, 3, 1, 2, 2, 1, 3, 1, 1, 2, 3, 1, 2, 1, 2, 1, 3, 2, 1 };
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            for (int i = 0; i < widths.Length; i++)
                sp.Children.Add(new Rectangle { Width = widths[i], Height = height, Fill = brush, Margin = new Thickness(0, 0, (i % 3 == 0) ? 2.5 : 1.5, 0) });
            return sp;
        }

        static void PopulateTicketStub(Border stubArea, string quote, string stubCode, bool showWatermark, CardPalette p, int ratioIndex)
        {
            var stubDock = new DockPanel();
            stubArea.Child = stubDock;

            var rightStub = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(14, 0, 0, 0) };
            DockPanel.SetDock(rightStub, Dock.Right); stubDock.Children.Add(rightStub);
            rightStub.Children.Add(BuildBarcode(p.Muted, ratioIndex == 2 ? 22 : 27));
            rightStub.Children.Add(new TextBlock { Text = stubCode, FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Accent, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 0) });

            var leftStub = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stubDock.Children.Add(leftStub);
            leftStub.Children.Add(new TextBlock { Text = "“" + quote + "”", FontFamily = SerifFont, FontSize = 11.5, Foreground = p.Text, TextWrapping = TextWrapping.Wrap, Opacity = 0.88 });
            var wm = BuildWatermark(showWatermark, p.Muted);
            if (wm != null) { wm.Margin = new Thickness(0, 6, 0, 0); leftStub.Children.Add(wm); }
        }

        static void BuildTicketCard(Grid root, int w, int h, List<ShareEntry> entries, CardOpts opts, string skinId, SkinPalette sk, CardPalette p)
        {
            double mX = opts.RatioIndex == 2 ? 54 : 46;
            double mY = opts.RatioIndex == 2 ? 38 : (opts.RatioIndex == 1 ? 42 : 48);
            double cw = w - mX * 2, ch = h - mY * 2;
            double notchY = opts.RatioIndex == 0 ? ch * 0.74 : ch * 0.72;
            double notchR = 13;

            var notches = new GeometryGroup();
            notches.Children.Add(new EllipseGeometry(new Point(0, notchY), notchR, notchR));
            notches.Children.Add(new EllipseGeometry(new Point(cw, notchY), notchR, notchR));
            var ticketGeo = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, cw, ch), 18, 18), notches);
            ticketGeo.Freeze();

            var cardBg = new LinearGradientBrush(C_(sk.Bg1), C_(sk.Bg2), new Point(0, 0), new Point(0, 1));
            cardBg.Freeze();
            var borderBrush = Brush(skinId != "default" ? HexA("68", p.DetailHex) : (p.Light ? "#22000000" : "#2EFFFFFF"));

            var ticketHost = new Grid { Width = cw, Height = ch, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            root.Children.Add(ticketHost);
            ticketHost.Children.Add(new System.Windows.Shapes.Path { Data = ticketGeo, Fill = new SolidColorBrush(Color.FromArgb(p.Light ? (byte)42 : (byte)85, 0, 0, 0)), Margin = new Thickness(0, 10, 0, -10), Effect = new BlurEffect { Radius = 30 } });
            ticketHost.Children.Add(new System.Windows.Shapes.Path { Data = ticketGeo, Fill = cardBg, Stroke = borderBrush, StrokeThickness = 1.2 });

            var inner = new Grid { Width = cw, Height = ch, Clip = ticketGeo };
            ticketHost.Children.Add(inner);
            inner.Children.Add(new Border { Child = BuildAmbientArt(skinId, p.DetailHex, opts.RatioIndex == 2 ? 135 : 155, skinId == "default" ? 0.09 : 0.14), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 52, 20, 0) });
            inner.Children.Add(new Line { X1 = notchR + 8, X2 = cw - notchR - 8, Y1 = notchY, Y2 = notchY, Stroke = p.Hairline, StrokeThickness = 1.2, StrokeDashArray = new DoubleCollection { 4, 4 } });

            inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(notchY) });
            inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ch - notchY) });

            var topArea = new Border { Padding = opts.RatioIndex == 2 ? new Thickness(28, 20, 28, 16) : new Thickness(26, 22, 26, 16) };
            Grid.SetRow(topArea, 0); inner.Children.Add(topArea);
            var stubArea = new Border { Padding = opts.RatioIndex == 2 ? new Thickness(28, 12, 28, 14) : new Thickness(26, 14, 26, 16) };
            Grid.SetRow(stubArea, 1); inner.Children.Add(stubArea);

            var now = DateTime.Now;
            string quote = string.IsNullOrWhiteSpace(opts.Quote) ? "安静地记录时间，认真地期待下一次相逢。" : opts.Quote.Trim();
            var topDock = new DockPanel(); topArea.Child = topDock;

            if (!opts.IsList && entries.Count > 0)
            {
                var entry = entries[Math.Max(0, Math.Min(opts.EntryIndex, entries.Count - 1))];
                string dayStr = DayText(entry.Date, now, false);
                bool isNum = dayStr != "今天" && dayStr != "已到期";

                var routeHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(routeHeader, Dock.Top); topDock.Children.Add(routeHeader);
                var seal = BuildSealStamp(skinId, p.DetailHex);
                if (seal != null) { DockPanel.SetDock(seal, Dock.Right); routeHeader.Children.Add(seal); }
                routeHeader.Children.Add(new TextBlock { Text = "BOARDING PASS · TIME TRANSIT", FontFamily = MonoFont, FontSize = 10, Foreground = p.Muted, VerticalAlignment = VerticalAlignment.Center });

                var specBottom = new Border { Margin = new Thickness(0, 10, 0, 0), Child = BuildSingleSpecRow(entry, now, opts, p, false) };
                DockPanel.SetDock(specBottom, Dock.Bottom); topDock.Children.Add(specBottom);

                if (opts.RatioIndex == 2)
                {
                    var cols = new Grid { VerticalAlignment = VerticalAlignment.Center };
                    AddCols(cols, new GridLength(1.2, GridUnitType.Star), new GridLength(1, GridUnitType.Star));
                    topDock.Children.Add(cols);

                    var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(left, 0); cols.Children.Add(left);
                    left.Children.Add(new TextBlock { Text = now.ToString("MM.dd") + " TODAY  ──→  " + (opts.ShowDate ? entry.Date.ToString("MM.dd") : "TARGET"), FontFamily = MonoFont, FontSize = 10, Foreground = p.Accent, Margin = new Thickness(0, 0, 0, 4) });
                    left.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(entry.Name) ? "倒计时" : entry.Name, FontFamily = SerifFont, FontSize = 28, FontWeight = FontWeights.Bold, Foreground = p.Text, TextWrapping = TextWrapping.Wrap });

                    var right = new Border { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right }; Grid.SetColumn(right, 1); cols.Children.Add(right);
                    right.Child = BuildHeroDayMetric(dayStr, isNum, 72, "DAYS", MonoFont, 11, new Thickness(6, 0, 0, 12), "TODAY", "ARRIVED", MonoFont, 24, p, false, false);
                }
                else
                {
                    var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; topDock.Children.Add(center);
                    var rbGrid = new DockPanel();
                    var rRight = new TextBlock { Text = opts.ShowDate ? entry.Date.ToString("yyyy.MM.dd") : RelativeSpanTag(entry.Date, now), FontFamily = MonoFont, FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = p.Accent };
                    DockPanel.SetDock(rRight, Dock.Right); rbGrid.Children.Add(rRight);
                    rbGrid.Children.Add(new TextBlock { Text = "ORIGIN " + now.ToString("MM.dd") + "  ───→  DEST", FontFamily = MonoFont, FontSize = 10, Foreground = p.Muted });
                    center.Children.Add(new Border { Background = p.SurfaceStrong, BorderBrush = p.Hairline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 12), Child = rbGrid });

                    center.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(entry.Name) ? "倒计时" : entry.Name, FontFamily = SerifFont, FontSize = opts.RatioIndex == 1 ? 26 : 30, FontWeight = FontWeights.Bold, Foreground = p.Text, TextWrapping = TextWrapping.Wrap });
                    var metric = BuildHeroDayMetric(dayStr, isNum, HeroNumberSize(opts.RatioIndex, dayStr) * 0.92, "天后抵达", SerifFont, 15, new Thickness(8, 0, 0, 14), "就是今天", "已如约而至", SerifFont, 34, p, false, true);
                    metric.Margin = new Thickness(0, opts.RatioIndex == 1 ? 8 : 14, 0, 0);
                    center.Children.Add(metric);

                    if (isNum && opts.RatioIndex == 0)
                    {
                        var prog = BuildProgressTrack(EntryHorizonRatio(entry.Date, now, 120), 4, p.Accent, p.Track);
                        prog.Margin = new Thickness(0, 14, 0, 0);
                        center.Children.Add(prog);
                    }
                }
                PopulateTicketStub(stubArea, quote, "SEQ " + (isNum ? dayStr.PadLeft(3, '0') : "000"), opts.ShowWatermark, p, opts.RatioIndex);
            }
            else
            {
                int maxDays;
                var ordered = OrderedEntries(entries, now, out maxDays);

                var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(header, Dock.Top); topDock.Children.Add(header);
                var seal = BuildSealStamp(skinId, p.DetailHex);
                if (seal != null) { DockPanel.SetDock(seal, Dock.Right); header.Children.Add(seal); }
                var hLeft = new StackPanel();
                hLeft.Children.Add(new TextBlock { Text = "ITINERARY PASS · " + now.ToString("yyyy.MM.dd") + " · 共 " + ordered.Count + " 程", FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Accent, Margin = new Thickness(0, 0, 0, 2) });
                hLeft.Children.Add(new TextBlock { Text = "下一程期待清单", FontFamily = SerifFont, FontSize = opts.RatioIndex == 2 ? 19 : 21, FontWeight = FontWeights.Bold, Foreground = p.Text });
                header.Children.Add(hLeft);

                var botBar = new Border { Background = p.Surface, BorderBrush = p.Hairline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 10, 0, 0), Child = BuildYearProgress(now, p, opts.RatioIndex != 0) };
                DockPanel.SetDock(botBar, Dock.Bottom); topDock.Children.Add(botBar);

                var listBody = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; topDock.Children.Add(listBody);
                var visible = ordered.Take(opts.RatioIndex == 0 ? 4 : 3).ToList();
                if (visible.Count > 0 && opts.RatioIndex == 0)
                {
                    listBody.Children.Add(BuildHeroFocusTile(visible[0], now, 0, opts.ShowDate, skinId, p, false));
                    var rest = visible.Skip(1).ToList();
                    if (rest.Count > 0)
                    {
                        var sub = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
                        PopulateScheduleCards(sub, rest, 1, now, 0, opts.ShowDate, p, true, maxDays);
                        listBody.Children.Add(sub);
                    }
                }
                else
                {
                    PopulateScheduleCards(listBody, visible, 0, now, opts.RatioIndex, opts.ShowDate, p, opts.RatioIndex == 2, maxDays);
                }
                PopulateTicketStub(stubArea, quote, "STOPS " + ordered.Count.ToString("00"), opts.ShowWatermark, p, opts.RatioIndex);
            }
        }

        static FrameworkElement BuildShareCardVisual(List<ShareEntry> entries, CardOpts opts, string currentConfigSkin, out int width, out int height)
        {
            if (opts.RatioIndex == 1) { width = 540; height = 540; }
            else if (opts.RatioIndex == 2) { width = 800; height = 450; }
            else { width = 540; height = 720; }

            bool light;
            string skinId = ResolveCardSkin(opts.SkinIndex, currentConfigSkin, entries, out light);
            var sk = PickSkinPalette(skinId, light);
            var palette = BuildPalette(skinId, sk, light, sk.Detail);

            bool withDots = opts.TemplateIndex == 0 || opts.TemplateIndex == 2;
            var root = BuildBackdrop(width, height, skinId, light, withDots);

            if (opts.TemplateIndex == 1) BuildAtmosphereCard(root, width, height, entries, opts, skinId, sk, palette);
            else if (opts.TemplateIndex == 2) BuildTicketCard(root, width, height, entries, opts, skinId, sk, palette);
            else BuildEditorialCard(root, width, height, entries, opts, skinId, sk, palette);

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

        static void OpenShareCard(List<ShareEntry> entries, int initialIndex, string currentConfigSkin, bool editorIsLight)
        {
            if (entries == null || entries.Count == 0)
            {
                if (Editor != null) MessageBox.Show(Editor, "请先至少添加一个有效日期的日子。", "生成社交卡片");
                else MessageBox.Show("请先至少添加一个有效日期的日子。", "生成社交卡片");
                return;
            }
            if (ShareWin != null) { try { ShareWin.Close(); } catch { } ShareWin = null; }
            if (initialIndex < 0 || initialIndex >= entries.Count) initialIndex = 0;

            Window win;
            var outer = CreateDialogChrome("生成社交卡片", 900, 700, editorIsLight, Editor, false, out win, null);

            var body = new Grid { Margin = new Thickness(24, 6, 24, 24) };
            AddCols(body, new GridLength(1.2, GridUnitType.Star), new GridLength(22), new GridLength(300));
            outer.Children.Add(body);

            var previewPane = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(16), BorderThickness = new Thickness(1) };
            previewPane.SetResourceReference(Border.BackgroundProperty, "Surface");
            previewPane.SetResourceReference(Border.BorderBrushProperty, "Line");
            Grid.SetColumn(previewPane, 0); body.Children.Add(previewPane);
            var viewbox = new Viewbox { Stretch = Stretch.Uniform }; previewPane.Child = viewbox;

            var controlPane = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(18) };
            controlPane.SetResourceReference(Border.BackgroundProperty, "Surface");
            Grid.SetColumn(controlPane, 2); body.Children.Add(controlPane);

            var ctrlDock = new DockPanel(); controlPane.Child = ctrlDock;
            var actionStack = new StackPanel { Margin = new Thickness(0, 14, 0, 0) }; DockPanel.SetDock(actionStack, Dock.Bottom); ctrlDock.Children.Add(actionStack);

            var copyBtn = ActionButton("复制图片到剪贴板", () => { });
            copyBtn.SetResourceReference(Button.BackgroundProperty, "Accent");
            copyBtn.Foreground = Brushes.White; copyBtn.BorderThickness = new Thickness(0);
            copyBtn.Padding = new Thickness(16, 11, 16, 11); copyBtn.Margin = new Thickness(0, 0, 0, 8);
            actionStack.Children.Add(copyBtn);

            var saveBtn = ActionButton("保存为高清 PNG…", () => { });
            saveBtn.Padding = new Thickness(16, 10, 16, 10);
            actionStack.Children.Add(saveBtn);

            var statusHint = Label("", 11, "Muted");
            statusHint.HorizontalAlignment = HorizontalAlignment.Center; statusHint.Margin = new Thickness(0, 8, 0, 0);
            actionStack.Children.Add(statusHint);

            var form = new StackPanel();
            ctrlDock.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });

            Func<string, ComboBox> addCombo = caption =>
            {
                var cap = Label(caption, 11, "Muted"); cap.Margin = new Thickness(0, 0, 0, 5); form.Children.Add(cap);
                var cb = new ComboBox { Margin = new Thickness(0, 0, 0, 11) }; form.Children.Add(cb);
                return cb;
            };

            var typePicker = addCombo("卡片内容");
            typePicker.Items.Add("单日聚焦卡"); typePicker.Items.Add("多日期待清单"); typePicker.SelectedIndex = 0;

            var dayCap = Label("选择日子", 11, "Muted"); dayCap.Margin = new Thickness(0, 0, 0, 5); form.Children.Add(dayCap);
            var dayPicker = new ComboBox { Margin = new Thickness(0, 0, 0, 11) };
            foreach (var e in entries) dayPicker.Items.Add(e.Name + "  (" + e.Date.ToString("yyyy-MM-dd") + ")");
            dayPicker.SelectedIndex = initialIndex; form.Children.Add(dayPicker);

            var tplPicker = addCombo("版式设计");
            tplPicker.Items.Add("杂志日历 · 单向历风"); tplPicker.Items.Add("极光氛围 · 节日插画"); tplPicker.Items.Add("时光票根 · 登机牌");
            tplPicker.SelectedIndex = 0;

            var ratioPicker = addCombo("画布比例");
            ratioPicker.Items.Add("竖版海报 · 3:4 (1080×1440)"); ratioPicker.Items.Add("方形卡片 · 1:1 (1080×1080)"); ratioPicker.Items.Add("横版封面 · 16:9 (1600×900)");
            ratioPicker.SelectedIndex = 0;

            var skinPicker = addCombo("色彩皮肤");
            foreach (var sc in ShareSkinChoices) skinPicker.Items.Add(sc.Label);
            skinPicker.SelectedIndex = 0;

            var quoteCap = Label("卡片寄语", 11, "Muted"); quoteCap.Margin = new Thickness(0, 0, 0, 5); form.Children.Add(quoteCap);
            var quoteBox = new TextBox { Text = "安静地记录时间，认真地期待下一次相逢。", MaxLength = 60, Height = 36, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 0, 12) };
            quoteBox.SetResourceReference(Control.BackgroundProperty, "Field");
            quoteBox.SetResourceReference(Control.BorderBrushProperty, "Line");
            form.Children.Add(quoteBox);

            var showDateChk = new CheckBox { Content = "显示目标日期", IsChecked = true, Margin = new Thickness(0, 0, 0, 9) };
            var showWmChk = new CheckBox { Content = "显示角标水印", IsChecked = true };
            form.Children.Add(showDateChk); form.Children.Add(showWmChk);

            Func<CardOpts> readOpts = () => new CardOpts
            {
                IsList = typePicker.SelectedIndex == 1,
                EntryIndex = Math.Max(0, dayPicker.SelectedIndex),
                TemplateIndex = Math.Max(0, tplPicker.SelectedIndex),
                RatioIndex = Math.Max(0, ratioPicker.SelectedIndex),
                SkinIndex = Math.Max(0, skinPicker.SelectedIndex),
                Quote = quoteBox.Text,
                ShowDate = showDateChk.IsChecked == true,
                ShowWatermark = showWmChk.IsChecked == true
            };

            Action refresh = () =>
            {
                bool isList = typePicker.SelectedIndex == 1;
                dayCap.Visibility = isList ? Visibility.Collapsed : Visibility.Visible;
                dayPicker.Visibility = isList ? Visibility.Collapsed : Visibility.Visible;
                int w, h;
                viewbox.Child = BuildShareCardVisual(entries, readOpts(), currentConfigSkin, out w, out h);
                statusHint.Text = "导出尺寸 " + (w * 2) + " × " + (h * 2) + " px";
            };

            foreach (var cb in new[] { typePicker, dayPicker, tplPicker, ratioPicker, skinPicker })
                cb.SelectionChanged += (s, e) => refresh();
            foreach (var chk in new[] { showDateChk, showWmChk })
            {
                chk.Checked += (s, e) => refresh();
                chk.Unchecked += (s, e) => refresh();
            }

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
                catch (Exception ex) { MessageBox.Show(win, "复制失败：" + ex.Message, "生成社交卡片"); }
            };

            saveBtn.Click += (s, e) =>
            {
                try
                {
                    var opts = readOpts();
                    string baseName = opts.IsList ? "期待清单" : entries[Math.Max(0, Math.Min(opts.EntryIndex, entries.Count - 1))].Name;
                    foreach (char ch in System.IO.Path.GetInvalidFileNameChars()) baseName = baseName.Replace(ch, '_');
                    var dlg = new Microsoft.Win32.SaveFileDialog
                    {
                        Title = "保存社交卡片",
                        Filter = "PNG 图片|*.png",
                        FileName = "倒计时-" + baseName + ".png",
                        InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                    };
                    if (dlg.ShowDialog(win) == true)
                    {
                        var enc = new PngBitmapEncoder();
                        enc.Frames.Add(BitmapFrame.Create(RenderShareBitmap(entries, opts, currentConfigSkin)));
                        using (var fs = File.Create(dlg.FileName)) enc.Save(fs);
                    }
                }
                catch (Exception ex) { MessageBox.Show(win, "保存失败：" + ex.Message, "生成社交卡片"); }
            };

            win.KeyDown += (s, e) => { if (e.Key == Key.Escape) win.Close(); };
            win.Closed += (s, e) => { quoteDebounce.Stop(); resetTimer.Stop(); ShareWin = null; };
            ShareWin = win;
            refresh();
            win.Show();
        }
    }
}
