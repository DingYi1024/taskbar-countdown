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
            public DateTime? StartDate;
            public bool IsCountup;
            public bool IsToday;
            public string LunarLabel;
            public string RepeatLabel;
            public string IconId;
        }

        public sealed class CardOpts
        {
            public bool IsList;
            public int EntryIndex;
            public int TemplateIndex; // 0 = Editorial, 1 = Atmosphere, 2 = Ticket, 3 = SwissGrid, 4 = Polaroid
            public int RatioIndex;    // 0 = 3:4, 1 = 1:1, 2 = 16:9, 3 = 9:19.5 Lockscreen
            public int SkinIndex;     // 0 = follow, 1..N = fixed presets in ShareSkinChoices
            public string Quote;
            public bool ShowDate;
            public bool ShowWatermark;
            public bool RoundedCorners;
            public bool PaperTexture;
            public string PhotoPath;

            public CardOpts()
            {
                ShowDate = true;
                ShowWatermark = true;
                RoundedCorners = false;
                PaperTexture = true;
            }
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

        static readonly ShareSkinChoice[] ShareSkinChoices = {
            new ShareSkinChoice { Label = "跟随当前",     SkinId = null,        Light = false },
            new ShareSkinChoice { Label = "云白 · 纸感",  SkinId = "default",   Light = true },
            new ShareSkinChoice { Label = "石墨 · 暗夜",  SkinId = "default",   Light = false },
            new ShareSkinChoice { Label = "宣纸 · 墨韵",  SkinId = "xuanpaper", Light = true },
            new ShareSkinChoice { Label = "黑胶 · 琥珀",  SkinId = "vinyl",     Light = false },
            new ShareSkinChoice { Label = "莫奈 · 睡莲",  SkinId = "monet",     Light = true },
            new ShareSkinChoice { Label = "奶油 · 暖麦",  SkinId = "cream",     Light = true },
            new ShareSkinChoice { Label = "晨雾 · 青瓷",  SkinId = "celadon",   Light = true },
            new ShareSkinChoice { Label = "极光 · 磨砂",  SkinId = "aurora",    Light = false },
            new ShareSkinChoice { Label = "圣诞 · 松霜",  SkinId = "christmas", Light = false },
            new ShareSkinChoice { Label = "元旦 · 星序",  SkinId = "newyear",   Light = false },
            new ShareSkinChoice { Label = "春节 · 灯笺",  SkinId = "spring",    Light = true },
            new ShareSkinChoice { Label = "中秋 · 月泊",  SkinId = "midautumn", Light = false },
            new ShareSkinChoice { Label = "国庆 · 朱锦",  SkinId = "national",  Light = false }
        };

        public static ShareEntry ToShareEntry(ResolvedItem r)
        {
            return new ShareEntry
            {
                Name = r.Name,
                Date = r.TargetDate,
                Enabled = r.Source == null || r.Source.enabled,
                StartDate = r.CycleStartDate,
                IsCountup = r.IsCountup,
                IsToday = r.IsToday,
                LunarLabel = r.LunarDisplay,
                RepeatLabel = r.RepeatLabel,
                IconId = r.ResolvedIcon
            };
        }

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
                Surface = Brush(light ? "#78FFFFFF" : "#18FFFFFF"),
                SurfaceStrong = Brush(light ? "#B4FFFFFF" : "#26FFFFFF"),
                Hairline = Brush(skinId != "default" ? HexA("38", detailHex) : (light ? "#1E000000" : "#24FFFFFF")),
                Track = Brush(light ? "#1C000000" : "#24FFFFFF"),
                PillBg = Brush(HexA(light ? "1D" : "2B", detailHex)),
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

        static string RelativeSpanTag(ShareEntry entry, DateTime now)
        {
            if (entry.IsCountup) return "已相伴纪念";
            if (entry.IsToday || (entry.Date.Date == now.Date && entry.Date >= now)) return "就在今天";
            if (entry.Date <= now) return "已抵达";
            int days = (entry.Date.Date - now.Date).Days;
            if (days == 1) return "就在明天";
            if (days < 7) return "本周将至";
            if (days <= 30) return "本月内";
            int months = Math.Max(1, (int)Math.Round(days / 30.0));
            if (months < 12) return "约 " + months + " 个月后";
            return "跨年期待";
        }

        static string EntryDayNumber(ShareEntry entry, DateTime now, out bool isNum, out string unitHint)
        {
            if (entry.IsCountup)
            {
                isNum = true;
                unitHint = "天";
                return Math.Max(1, (now.Date - entry.Date.Date).Days + 1).ToString(CultureInfo.InvariantCulture);
            }
            string d = DayText(entry.Date, now, false);
            if (entry.IsToday || d == "今天")
            {
                isNum = false;
                unitHint = "";
                return "今天";
            }
            if (d == "已到期")
            {
                isNum = false;
                unitHint = "";
                return "已到期";
            }
            isNum = true;
            unitHint = "天后";
            return d;
        }

        static string DetailedDurationText(ShareEntry entry, DateTime now)
        {
            if (entry.IsCountup)
            {
                int days = Math.Max(1, (now.Date - entry.Date.Date).Days + 1);
                int w = days / 7, r = days % 7;
                return "起始于 " + entry.Date.ToString("yyyy.MM.dd") + (w > 0 ? (" · " + w + " 周 " + r + " 天") : "");
            }
            if (entry.Date <= now) return "时间已抵达约定时刻";
            var span = entry.Date - now;
            int weeks = (int)span.TotalDays / 7;
            int remDays = (int)span.TotalDays % 7;
            if (weeks > 0) return weeks + " 周 " + remDays + " 天 " + span.Hours.ToString("00") + " 时";
            return (int)span.TotalDays + " 天 " + span.Hours.ToString("00") + " 时 " + span.Minutes.ToString("00") + " 分";
        }

        static string CompactWeekText(ShareEntry entry, DateTime now)
        {
            if (entry.IsCountup)
            {
                int d = Math.Max(1, (now.Date - entry.Date.Date).Days + 1);
                return d >= 7 ? ((d / 7) + " 周 " + (d % 7) + " 天") : (d + " 天");
            }
            if (entry.Date <= now) return "已抵达";
            int days = (entry.Date.Date - now.Date).Days;
            if (days < 7) return days + " 天";
            int w = days / 7;
            int r = days % 7;
            return r == 0 ? (w + " 周") : (w + " 周 " + r + " 天");
        }

        static double RealEntryProgress(ShareEntry entry, DateTime now, out DateTime startRef)
        {
            if (entry.IsCountup)
            {
                startRef = entry.Date;
                double mod = ((now.Date - entry.Date.Date).Days % 365 + 365) % 365;
                return Math.Max(0.05, Math.Min(1.0, mod / 365.0));
            }
            if (entry.StartDate.HasValue && entry.StartDate.Value < entry.Date)
            {
                startRef = entry.StartDate.Value;
            }
            else
            {
                DateTime jan1 = new DateTime(now.Year, 1, 1);
                startRef = jan1 < entry.Date ? jan1 : entry.Date.AddDays(-90);
            }
            if (entry.Date <= now) return 1.0;
            double total = Math.Max(1.0, (entry.Date - startRef).TotalDays);
            double elapsed = Math.Max(0.0, (now - startRef).TotalDays);
            return Math.Max(0.04, Math.Min(0.98, elapsed / total));
        }

        static List<ShareEntry> OrderedEntries(List<ShareEntry> entries, DateTime now, out int maxDays)
        {
            var enabled = entries.Where(e => e.Enabled).ToList();
            if (enabled.Count == 0) enabled = entries.ToList();
            var upcoming = enabled.Where(e => !e.IsCountup && e.Date > now).OrderBy(e => e.Date).ToList();
            var countups = enabled.Where(e => e.IsCountup).OrderByDescending(e => (now.Date - e.Date.Date).Days).ToList();
            var past = enabled.Where(e => !e.IsCountup && e.Date <= now).OrderByDescending(e => e.Date).ToList();
            upcoming.AddRange(countups);
            upcoming.AddRange(past);
            maxDays = upcoming.Where(e => !e.IsCountup && e.Date > now)
                .Select(e => (int)Math.Ceiling((e.Date.Date - now.Date).TotalDays))
                .DefaultIfEmpty(100)
                .Max();
            return upcoming;
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

        static FrameworkElement BuildWatermark(bool show, Brush mutedBrush)
        {
            if (!show) return null;
            var wm = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.78 };
            wm.Children.Add(new Image { Source = Logo(), Width = 12, Height = 12, Margin = new Thickness(0, 0, 5, 0) });
            wm.Children.Add(new TextBlock { Text = "拾光倒计时", FontSize = 10, Foreground = mutedBrush, VerticalAlignment = VerticalAlignment.Center });
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
            var sp = new StackPanel();
            var hdr = new DockPanel { Margin = new Thickness(0, 0, 0, compact ? 4 : 5) };
            var right = new TextBlock { Text = (ratio * 100.0).ToString("0.0", CultureInfo.InvariantCulture) + "% · 余 " + left + " 天", FontFamily = MonoFont, FontSize = compact ? 9 : 9.5, Foreground = p.Accent };
            DockPanel.SetDock(right, Dock.Right); hdr.Children.Add(right);
            hdr.Children.Add(new TextBlock { Text = now.Year + " 年度进度 · 已过 " + passed + " 天", FontSize = compact ? 9.5 : 10, Foreground = p.Muted });
            sp.Children.Add(hdr);
            sp.Children.Add(BuildProgressTrack(ratio, compact ? 3.5 : 4.5, p.Accent, p.Track));
            return sp;
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
            sp.Children.Add(new TextBlock { Text = now.Day.ToString("00"), FontFamily = NumFont, FontSize = 18, FontWeight = FontWeights.Bold, Foreground = p.Text, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -1, 0, -2) });
            badge.Child = sp;
            return badge;
        }

        static FrameworkElement BuildStatTile(string label, string value, string sub, CardPalette p)
        {
            var tile = new Border { Background = p.Surface, BorderBrush = p.Hairline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(11, 8, 11, 8) };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = label, FontSize = 9.5, Foreground = p.Muted });
            sp.Children.Add(new TextBlock { Text = value, FontFamily = NumFont, FontSize = 14.5, FontWeight = FontWeights.Bold, Foreground = p.Text, Margin = new Thickness(0, 2, 0, 1) });
            if (!string.IsNullOrEmpty(sub)) sp.Children.Add(new TextBlock { Text = sub, FontSize = 9.5, Foreground = p.Accent, TextTrimming = TextTrimming.CharacterEllipsis });
            tile.Child = sp;
            return tile;
        }

        static FrameworkElement BuildSealStamp(string skinId, string detailHex)
        {
            string seal = FindSkin(skinId).Seal;
            if (seal == null) return null;
            return new Border
            {
                BorderBrush = Brush(HexA("99", detailHex)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 3, 7, 3),
                Background = Brush(HexA("18", detailHex)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = seal, FontFamily = SerifFont, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brush(detailHex) }
            };
        }

        static FrameworkElement BuildPill(string text, CardPalette p, bool strong)
        {
            return new Border
            {
                Background = strong ? p.PillBg : p.Surface,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = text, FontSize = 9.5, FontWeight = strong ? FontWeights.SemiBold : FontWeights.Normal, Foreground = strong ? p.Accent : p.Muted }
            };
        }

        static FrameworkElement BuildAmbientArt(string skinId, string accentHex, double size, double opacity)
        {
            var vb = new Viewbox { Width = size, Height = size, Stretch = Stretch.Uniform, Opacity = opacity, IsHitTestVisible = false };
            var c = new Canvas { Width = 200, Height = 200 };
            Brush stroke = Brush(accentHex);
            Brush fill = Brush(HexA("24", accentHex));

            if (skinId == "christmas")
            {
                c.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M100,26 L68,74 L86,74 L52,120 L76,120 L40,164 L160,164 L124,120 L148,120 L114,74 L132,74 Z"), Fill = fill, Stroke = stroke, StrokeThickness = 2.6, StrokeLineJoin = PenLineJoin.Round });
                c.Children.Add(new Line { X1 = 100, Y1 = 164, X2 = 100, Y2 = 184, Stroke = stroke, StrokeThickness = 4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
                AddPoly(c, "100,6 104,17 115,21 104,25 100,36 96,25 85,21 96,17", C_(accentHex));
            }
            else if (skinId == "spring")
            {
                c.Children.Add(new Line { X1 = 100, Y1 = 8, X2 = 100, Y2 = 34, Stroke = stroke, StrokeThickness = 3 });
                c.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M100,34 C34,34 34,146 100,146 C166,146 166,34 100,34 Z"), Fill = fill, Stroke = stroke, StrokeThickness = 2.8 });
                c.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M82,38 C60,70 60,110 82,142 M118,38 C140,70 140,110 118,142"), Stroke = stroke, StrokeThickness = 2 });
                c.Children.Add(new Line { X1 = 76, Y1 = 34, X2 = 124, Y2 = 34, Stroke = stroke, StrokeThickness = 5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
                c.Children.Add(new Line { X1 = 78, Y1 = 146, X2 = 122, Y2 = 146, Stroke = stroke, StrokeThickness = 5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
                c.Children.Add(new Line { X1 = 100, Y1 = 148, X2 = 100, Y2 = 186, Stroke = stroke, StrokeThickness = 2.8, StrokeEndLineCap = PenLineCap.Round });
            }
            else if (skinId == "midautumn")
            {
                var moon = new Ellipse { Width = 106, Height = 106, Fill = fill, Stroke = stroke, StrokeThickness = 2.6 };
                Canvas.SetLeft(moon, 47); Canvas.SetTop(moon, 24); c.Children.Add(moon);
                c.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M20,152 Q60,136 100,152 T180,152 M36,170 Q75,158 115,170 T174,170"), Stroke = stroke, StrokeThickness = 2.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            }
            else if (skinId == "newyear" || skinId == "aurora")
            {
                AddPoly(c, "100,18 113,80 175,93 113,106 100,168 87,106 25,93 87,80", C_(accentHex));
                c.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M44,38 L156,150 M156,38 L44,150"), Stroke = stroke, StrokeThickness = 1.6, Opacity = 0.65 });
            }
            else if (skinId == "national")
            {
                c.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M38,176 L38,26 M40,34 C90,14 128,58 172,34 L172,118 C128,142 90,98 40,118"), Fill = fill, Stroke = stroke, StrokeThickness = 2.8, StrokeLineJoin = PenLineJoin.Round });
                AddPoly(c, "84,54 90,71 108,71 94,82 99,99 84,89 69,99 74,82 60,71 78,71", C_(accentHex));
            }
            else if (skinId == "celadon")
            {
                c.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M36,164 C36,88 86,36 164,36 C164,112 114,164 36,164 Z"), Fill = fill, Stroke = stroke, StrokeThickness = 2.6, StrokeLineJoin = PenLineJoin.Round });
                c.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M36,164 C84,116 118,82 158,42"), Stroke = stroke, StrokeThickness = 2.0, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            }
            else if (skinId == "xuanpaper")
            {
                var ring = new Ellipse { Width = 134, Height = 134, Stroke = stroke, StrokeThickness = 3.2, Opacity = 0.72 };
                Canvas.SetLeft(ring, 33); Canvas.SetTop(ring, 33); c.Children.Add(ring);
                c.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M30,148 Q75,112 114,132 T180,108"), Stroke = stroke, StrokeThickness = 2.4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            }
            else if (skinId == "vinyl")
            {
                foreach (double d in new[] { 164.0, 134.0, 104.0, 74.0, 38.0 })
                {
                    var gr = new Ellipse { Width = d, Height = d, Stroke = stroke, StrokeThickness = d == 38.0 ? 2.5 : 1.3, Opacity = d == 38.0 ? 0.9 : 0.45 };
                    Canvas.SetLeft(gr, (200 - d) / 2); Canvas.SetTop(gr, (200 - d) / 2);
                    c.Children.Add(gr);
                }
            }
            else if (skinId == "monet")
            {
                var p1 = new Ellipse { Width = 148, Height = 92, Fill = fill, Stroke = stroke, StrokeThickness = 1.8 };
                Canvas.SetLeft(p1, 26); Canvas.SetTop(p1, 60); c.Children.Add(p1);
                c.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M100,48 C118,74 118,108 100,132 C82,108 82,74 100,48 Z M100,132 C126,104 148,96 158,110 C138,130 116,138 100,132 Z M100,132 C74,104 52,96 42,110 C62,130 84,138 100,132 Z"), Fill = fill, Stroke = stroke, StrokeThickness = 1.8 });
            }
            else
            {
                var ring1 = new Ellipse { Width = 146, Height = 146, Stroke = stroke, StrokeThickness = 1.6, StrokeDashArray = new DoubleCollection { 4, 6 } };
                Canvas.SetLeft(ring1, 27); Canvas.SetTop(ring1, 27); c.Children.Add(ring1);
                var ring2 = new Ellipse { Width = 94, Height = 94, Stroke = stroke, StrokeThickness = 1.6 };
                Canvas.SetLeft(ring2, 53); Canvas.SetTop(ring2, 53); c.Children.Add(ring2);
                AddPoly(c, "152,32 156,43 167,47 156,51 152,62 148,51 137,47 148,43", C_(accentHex));
            }

            vb.Child = c;
            return vb;
        }

        static double HeroNumberSize(int ratioIndex, string dayStr)
        {
            int len = (dayStr ?? "").Length;
            if (ratioIndex == 2) return len >= 4 ? 64 : (len == 3 ? 78 : 92);
            if (ratioIndex == 1) return len >= 4 ? 60 : (len == 3 ? 72 : 84);
            return len >= 4 ? 74 : (len == 3 ? 90 : 108);
        }

        static StackPanel BuildHeroDayMetric(string dayStr, bool isNum, double numSize, string unitLabel, FontFamily unitFont, double unitSize, Thickness unitMargin, string todayText, string pastText, FontFamily textFont, double textSize, CardPalette p, bool center, bool showDaysSub)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left };
            if (!isNum)
            {
                row.Children.Add(new TextBlock
                {
                    Text = dayStr == "今天" ? todayText : pastText,
                    FontFamily = textFont,
                    FontSize = textSize,
                    FontWeight = FontWeights.Bold,
                    Foreground = p.Accent
                });
                return row;
            }
            row.Children.Add(new TextBlock
            {
                Text = dayStr,
                FontFamily = NumFont,
                FontSize = numSize,
                FontWeight = FontWeights.Bold,
                Foreground = p.Text,
                LineHeight = numSize * 0.92,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                VerticalAlignment = VerticalAlignment.Bottom
            });
            if (showDaysSub)
            {
                var unitCol = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = unitMargin };
                unitCol.Children.Add(new TextBlock { Text = "DAYS", FontFamily = MonoFont, FontSize = 9, Foreground = p.Muted, Margin = new Thickness(1, 0, 0, 1) });
                unitCol.Children.Add(new TextBlock { Text = unitLabel, FontFamily = unitFont, FontSize = unitSize, FontWeight = FontWeights.SemiBold, Foreground = p.Accent });
                row.Children.Add(unitCol);
            }
            else
            {
                row.Children.Add(new TextBlock { Text = unitLabel, FontFamily = unitFont, FontSize = unitSize, FontWeight = FontWeights.SemiBold, Foreground = p.Accent, VerticalAlignment = VerticalAlignment.Bottom, Margin = unitMargin });
            }
            return row;
        }

        static Border BuildCardShell(Grid root, int w, int h, SkinPalette sk, CardPalette p, bool glassStyle, bool rounded, bool paperTexture)
        {
            var cardBg = glassStyle
                ? new LinearGradientBrush(C_(sk.Bg1), C_(sk.Bg2), new Point(0, 0), new Point(1, 1))
                : new LinearGradientBrush(C_(sk.Bg1), C_(sk.Bg2), new Point(0, 0), new Point(0, 1));
            cardBg.Freeze();

            var borderGrad = new LinearGradientBrush(
                p.Light ? Color.FromArgb(165, 255, 255, 255) : Color.FromArgb(90, 255, 255, 255),
                p.Light ? Color.FromArgb(30, 0, 0, 0) : Color.FromArgb(20, 255, 255, 255),
                new Point(0, 0), new Point(1, 1));
            borderGrad.Freeze();

            double r = rounded ? 16 : 0;
            var card = new Border
            {
                Width = w, Height = h,
                CornerRadius = new CornerRadius(r),
                Background = cardBg,
                BorderBrush = rounded ? (glassStyle ? (Brush)borderGrad : p.Hairline) : Brushes.Transparent,
                BorderThickness = new Thickness(rounded ? (glassStyle ? 1.2 : 1.0) : 0),
                ClipToBounds = true
            };
            root.Children.Add(card);

            var hostGrid = new Grid { ClipToBounds = true };
            card.Child = hostGrid;

            if (paperTexture)
            {
                hostGrid.Children.Add(new Rectangle { Fill = PaperGrainBrush(p.Light), IsHitTestVisible = false });
            }

            var contentSlot = new Border();
            hostGrid.Children.Add(contentSlot);
            return contentSlot;
        }

        static FrameworkElement BuildHeroFocusTile(ShareEntry item, DateTime now, int ratioIndex, bool showDate, string skinId, CardPalette p, bool withArt)
        {
            bool isNum;
            string unitHint;
            string dayStr = EntryDayNumber(item, now, out isNum, out unitHint);

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

            var pad = new Border { Padding = ratioIndex == 2 ? new Thickness(16, 12, 16, 12) : new Thickness(18, 13, 18, 13) };
            innerGrid.Children.Add(pad);

            var dock = new DockPanel();
            pad.Child = dock;

            var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 0, 0, 0) };
            DockPanel.SetDock(right, Dock.Right); dock.Children.Add(right);
            right.Children.Add(BuildHeroDayMetric(dayStr, isNum, ratioIndex == 2 ? 38 : 44, "天", SansFont, 12, new Thickness(4, 0, 0, 6), "就是今天", "已抵达", SansFont, 20, p, false, false));

            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; dock.Children.Add(left);
            var badges = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            badges.Children.Add(BuildPill(item.IsCountup ? "ANNIVERSARY · 纪念日" : "NEXT · 最近期待", p, true));
            var rel = BuildPill(RelativeSpanTag(item, now), p, false); rel.Margin = new Thickness(6, 0, 0, 0);
            badges.Children.Add(rel);
            left.Children.Add(badges);

            left.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(item.Name) ? "倒计时" : item.Name, FontFamily = SerifFont, FontSize = ratioIndex == 2 ? 18 : 20, FontWeight = FontWeights.Bold, Foreground = p.Text, TextTrimming = TextTrimming.CharacterEllipsis });
            if (showDate)
            {
                string dLine = item.Date.ToString("yyyy.MM.dd") + " · " + WeekdayZh(item.Date);
                if (!string.IsNullOrEmpty(item.LunarLabel)) dLine += " · " + item.LunarLabel;
                left.Children.Add(new TextBlock { Text = dLine, FontFamily = MonoFont, FontSize = 10, Foreground = p.Muted, Margin = new Thickness(0, 3, 0, 0) });
            }

            DateTime sRef;
            double ratio = RealEntryProgress(item, now, out sRef);
            var prog = BuildProgressTrack(ratio, 3.5, p.Accent, p.Track);
            prog.Margin = new Thickness(0, 9, 0, 0);
            left.Children.Add(prog);

            return hero;
        }

        static FrameworkElement BuildInsightRow(List<ShareEntry> ordered, DateTime now, int shownCount, CardPalette p, bool showDate)
        {
            int passed, left;
            double yrRatio = YearElapsedRatio(now, out passed, out left);
            var upcoming = ordered.Where(e => !e.IsCountup && e.Date > now).ToList();

            var grid = new Grid();
            AddCols(grid, new GridLength(1.15, GridUnitType.Star), new GridLength(10), new GridLength(0.85, GridUnitType.Star));

            var c1 = new Border { Background = p.Surface, BorderBrush = p.Hairline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 9, 12, 9) };
            var s1 = new StackPanel();
            var h1 = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
            var p1 = new TextBlock { Text = (yrRatio * 100.0).ToString("0.0", CultureInfo.InvariantCulture) + "%", FontFamily = NumFont, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = p.Text };
            DockPanel.SetDock(p1, Dock.Right); h1.Children.Add(p1);
            h1.Children.Add(new TextBlock { Text = now.Year + " 年度进度", FontSize = 10, Foreground = p.Muted, VerticalAlignment = VerticalAlignment.Center });
            s1.Children.Add(h1);
            s1.Children.Add(BuildProgressTrack(yrRatio, 4, p.Accent, p.Track));
            s1.Children.Add(new TextBlock { Text = "已过 " + passed + " 天 · 余 " + left + " 天", FontSize = 9.5, Foreground = p.Muted, Margin = new Thickness(0, 5, 0, 0) });
            c1.Child = s1;
            Grid.SetColumn(c1, 0); grid.Children.Add(c1);

            string spanTitle = upcoming.Count >= 2 ? "日程跨度" : "期待概览";
            string spanVal = upcoming.Count >= 2
                ? ((upcoming.First().Date.Date - now.Date).Days + " — " + (upcoming.Last().Date.Date - now.Date).Days + " 天")
                : ("共 " + ordered.Count + " 个日程");
            string spanSub = upcoming.Count >= 2 && showDate
                ? (upcoming.First().Date.ToString("MM.dd") + " 至 " + upcoming.Last().Date.ToString("MM.dd"))
                : (ordered.Count > shownCount ? ("另有 " + (ordered.Count - shownCount) + " 项在队列") : "每一天都值得期待");
            var c2 = BuildStatTile(spanTitle, spanVal, spanSub, p);
            Grid.SetColumn(c2, 2); grid.Children.Add(c2);

            return grid;
        }

        static void PopulateScheduleCards(StackPanel container, List<ShareEntry> list, int startIndex, DateTime now, int ratioIndex, bool showDate, CardPalette p, bool compact, int maxDays)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                bool isNum;
                string unitHint;
                string dText = EntryDayNumber(item, now, out isNum, out unitHint);

                var card = new Border
                {
                    Background = p.Surface, BorderBrush = p.Hairline, BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = compact ? new Thickness(12, 7, 13, 7) : new Thickness(13, 9, 14, 9),
                    Margin = new Thickness(0, 0, 0, i == list.Count - 1 ? 0 : (compact ? 6 : 8))
                };
                var stack = new StackPanel(); card.Child = stack;

                var top = new DockPanel(); stack.Children.Add(top);
                var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
                DockPanel.SetDock(right, Dock.Right); top.Children.Add(right);
                right.Children.Add(new TextBlock { Text = dText, FontFamily = isNum ? NumFont : SansFont, FontSize = isNum ? (compact ? 20 : 22) : 13, FontWeight = FontWeights.Bold, Foreground = item.Date <= now && !item.IsCountup ? p.Muted : p.Text, VerticalAlignment = VerticalAlignment.Bottom });
                if (isNum) right.Children.Add(new TextBlock { Text = "天", FontSize = 10, Foreground = p.Accent, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(3, 0, 0, 3) });

                var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; top.Children.Add(left);
                var titleLine = new StackPanel { Orientation = Orientation.Horizontal };
                titleLine.Children.Add(new TextBlock { Text = (startIndex + i + 1).ToString("00"), FontFamily = MonoFont, FontSize = 10, Foreground = p.Accent, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                titleLine.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(item.Name) ? "倒计时" : item.Name, FontSize = compact ? 13 : 14, FontWeight = FontWeights.SemiBold, Foreground = p.Text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = ratioIndex == 2 ? 210 : 230 });
                titleLine.Children.Add(new Border
                {
                    Background = p.PillBg, CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 1.5, 6, 1.5),
                    Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = RelativeSpanTag(item, now), FontSize = 9, Foreground = p.Accent }
                });
                left.Children.Add(titleLine);

                if (showDate) left.Children.Add(new TextBlock { Text = item.Date.ToString("yyyy.MM.dd") + " · " + WeekdayZh(item.Date), FontSize = 10, Foreground = p.Muted, Margin = new Thickness(19, 2, 0, 0) });
                if (!compact)
                {
                    DateTime sRef;
                    var meter = BuildProgressTrack(RealEntryProgress(item, now, out sRef), 2.5, p.Accent, p.Track);
                    meter.Margin = new Thickness(19, 7, 0, 0);
                    stack.Children.Add(meter);
                }
                container.Children.Add(card);
            }
        }

        static FrameworkElement BuildBentoEventTile(ShareEntry item, int seq, DateTime now, bool showDate, CardPalette p)
        {
            bool isNum;
            string unitHint;
            string dText = EntryDayNumber(item, now, out isNum, out unitHint);

            var tile = new Border { Background = p.Surface, BorderBrush = p.Hairline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(13, 11, 13, 11) };
            var dock = new DockPanel(); tile.Child = dock;

            var top = new DockPanel(); DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top);
            var rel = BuildPill(RelativeSpanTag(item, now), p, true);
            DockPanel.SetDock(rel, Dock.Right); top.Children.Add(rel);
            top.Children.Add(new TextBlock { Text = seq.ToString("00"), FontFamily = MonoFont, FontSize = 10, Foreground = p.Muted, VerticalAlignment = VerticalAlignment.Center });

            var bottom = new StackPanel { Margin = new Thickness(0, 8, 0, 0) }; DockPanel.SetDock(bottom, Dock.Bottom); dock.Children.Add(bottom);
            if (showDate) bottom.Children.Add(new TextBlock { Text = item.Date.ToString("MM.dd") + " · " + ShortWeekdayZh(item.Date), FontSize = 9.5, Foreground = p.Muted, Margin = new Thickness(0, 0, 0, 5) });
            DateTime sRef;
            bottom.Children.Add(BuildProgressTrack(RealEntryProgress(item, now, out sRef), 3, p.Accent, p.Track));

            var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) }; dock.Children.Add(mid);
            mid.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(item.Name) ? "倒计时" : item.Name, FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = p.Text, TextTrimming = TextTrimming.CharacterEllipsis });
            var metric = BuildHeroDayMetric(dText, isNum, 30, "天", SansFont, 11, new Thickness(4, 0, 0, 4), "今天", "已抵达", SansFont, 18, p, false, false);
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

            string dateCap = entry.IsCountup ? "纪念起点" : (opts.ShowDate ? "目标日期" : "目标星期");
            string dateSub = !string.IsNullOrEmpty(entry.LunarLabel) ? entry.LunarLabel : (opts.ShowDate ? WeekdayZh(entry.Date) : ("第 " + IsoWeekOfYear(entry.Date) + " 周"));
            var t1 = BuildStatTile(dateCap, opts.ShowDate ? entry.Date.ToString("yyyy.MM.dd") : WeekdayZh(entry.Date), dateSub, p);
            Grid.SetColumn(t1, 0); grid.Children.Add(t1);

            string hoursSub = entry.IsCountup
                ? ("已走过 " + (Math.Max(1, (now.Date - entry.Date.Date).Days) * 24).ToString("N0", CultureInfo.InvariantCulture) + " 小时")
                : (entry.Date > now ? ("约 " + ((int)Math.Ceiling((entry.Date.Date - now.Date).TotalDays) * 24).ToString("N0", CultureInfo.InvariantCulture) + " 小时") : "已抵达");
            var t2 = BuildStatTile(entry.IsCountup ? "相伴换算" : "距离换算", CompactWeekText(entry, now), hoursSub, p);
            Grid.SetColumn(t2, 2); grid.Children.Add(t2);

            var t3 = BuildStatTile(now.Year + " 年度进度", (yrRatio * 100.0).ToString("0.0", CultureInfo.InvariantCulture) + "%", "余 " + left + " 天跨年", p);
            Grid.SetColumn(t3, 4); grid.Children.Add(t3);

            if (compact) grid.Margin = new Thickness(0, 8, 0, 0);
            return grid;
        }

        // --- Template 0: Editorial Calendar (Rebalanced, zero upper dead void!) ---
        static void BuildEditorialCard(Grid root, int w, int h, List<ShareEntry> entries, CardOpts opts, string skinId, SkinPalette sk, CardPalette p)
        {
            var card = BuildCardShell(root, w, h, sk, p, false, opts.RoundedCorners, opts.PaperTexture);
            bool isLock = opts.RatioIndex == 3;
            var innerPad = new Border
            {
                Padding = isLock
                    ? new Thickness(38, 300, 38, 64) // Lockscreen safe top zone for phone clock!
                    : (opts.RatioIndex == 2 ? new Thickness(44, 28, 44, 24) : (opts.RatioIndex == 1 ? new Thickness(32, 26, 32, 22) : new Thickness(36, 32, 36, 28)))
            };
            card.Child = innerPad;

            var now = DateTime.Now;
            string quote = string.IsNullOrWhiteSpace(opts.Quote) ? LiteraryQuotes[0] : opts.Quote.Trim();

            if (!opts.IsList && entries.Count > 0)
            {
                var entry = entries[Math.Max(0, Math.Min(opts.EntryIndex, entries.Count - 1))];
                bool isNum;
                string unitHint;
                string dayStr = EntryDayNumber(entry, now, out isNum, out unitHint);

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
                    mid.Children.Add(BuildPill(entry.IsCountup ? "ANNIVERSARY · 纪念正数" : "UPCOMING · 倒数聚焦", p, true));
                    mid.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(entry.Name) ? "倒计时" : entry.Name, FontFamily = SerifFont, FontSize = 32, FontWeight = FontWeights.Bold, Foreground = p.Text, TextWrapping = TextWrapping.Wrap, MaxHeight = 86, Margin = new Thickness(0, 8, 0, 0) });
                    string meta = opts.ShowDate ? (entry.Date.ToString("yyyy.MM.dd") + " · " + WeekdayZh(entry.Date) + " · " + CompactWeekText(entry, now)) : DetailedDurationText(entry, now);
                    mid.Children.Add(new TextBlock { Text = meta, FontSize = 11, Foreground = p.Muted, Margin = new Thickness(0, 8, 0, 0) });

                    var sep = new Border { Width = 1, Background = p.Hairline, HorizontalAlignment = HorizontalAlignment.Center };
                    Grid.SetColumn(sep, 1); cols.Children.Add(sep);

                    var right = new Grid(); Grid.SetColumn(right, 2); cols.Children.Add(right);
                    right.Children.Add(new Border { Child = BuildAmbientArt(skinId, p.DetailHex, 165, skinId == "default" ? 0.14 : 0.22), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, -10, -10) });
                    var numBox = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                    right.Children.Add(numBox);
                    if (isNum) numBox.Children.Add(new TextBlock { Text = entry.IsCountup ? "DAYS TOGETHER" : "DAYS REMAINING", FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Muted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) });
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
                    headLeft.Children.Add(new TextBlock { Text = "DAILY FOCUS · " + now.Year + " 第 " + IsoWeekOfYear(now) + " 周 · " + LunarHelper.SolarToFullLunar(now), FontFamily = MonoFont, FontSize = 9, Foreground = p.Accent, Margin = new Thickness(0, 0, 0, 3) });
                    headLeft.Children.Add(new TextBlock { Text = entry.IsCountup ? "值得铭记的时光" : "距离期待之日", FontFamily = SerifFont, FontSize = 19, FontWeight = FontWeights.Bold, Foreground = p.Text });
                    topBar.Children.Add(headLeft);
                    topBlock.Children.Add(new Border { Height = 1, Background = p.Hairline, Margin = new Thickness(0, 14, 0, 0) });

                    var footer = new StackPanel { Margin = new Thickness(0, 14, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
                    footer.Children.Add(BuildSingleSpecRow(entry, now, opts, p, false));
                    footer.Children.Add(new Border { Height = 1, Background = p.Hairline, Margin = new Thickness(0, 14, 0, 10) });
                    footer.Children.Add(BuildQuoteFooterRow(quote, true, SerifFont, 11.5, opts.ShowWatermark, p, true));

                    // Rebalanced center composition: subtle upper breathing room + bold visual weight
                    var centerGrid = new Grid { Margin = new Thickness(0, opts.RatioIndex == 1 ? 8 : 22, 0, 8) };
                    layout.Children.Add(centerGrid);
                    centerGrid.Children.Add(new Border
                    {
                        Child = BuildAmbientArt(skinId, p.DetailHex, opts.RatioIndex == 1 ? 155 : 220, skinId == "default" ? 0.16 : 0.25),
                        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, -6, 0)
                    });

                    var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
                    centerGrid.Children.Add(center);
                    center.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 12), Child = BuildPill((entry.IsCountup ? "ANNIVERSARY · " : "NEXT MILESTONE · ") + RelativeSpanTag(entry, now), p, true) });
                    center.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(entry.Name) ? "倒计时" : entry.Name, FontFamily = SerifFont, FontSize = opts.RatioIndex == 1 ? 28 : 38, FontWeight = FontWeights.Bold, Foreground = p.Text, TextWrapping = TextWrapping.Wrap, MaxWidth = w - 130 });

                    var metric = BuildHeroDayMetric(dayStr, isNum, HeroNumberSize(opts.RatioIndex, dayStr), unitHint, SerifFont, 17, new Thickness(10, 0, 0, opts.RatioIndex == 1 ? 12 : 16), "就是今天", "已如约而至", SerifFont, opts.RatioIndex == 1 ? 34 : 44, p, false, true);
                    metric.Margin = isNum ? new Thickness(0, opts.RatioIndex == 1 ? 10 : 16, 0, 0) : new Thickness(0, 18, 0, 10);
                    center.Children.Add(metric);

                    if (isNum && (opts.RatioIndex == 0 || isLock))
                    {
                        DateTime sRef;
                        double realRatio = RealEntryProgress(entry, now, out sRef);
                        var horizonWrap = new StackPanel { Margin = new Thickness(0, 22, 0, 0), Width = 280, HorizontalAlignment = HorizontalAlignment.Left };
                        var hLabels = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                        var rLbl = new TextBlock { Text = (opts.ShowDate ? entry.Date.ToString("yyyy.MM.dd") : "目标日") + " · " + ((int)Math.Round(realRatio * 100)) + "%", FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Accent };
                        DockPanel.SetDock(rLbl, Dock.Right); hLabels.Children.Add(rLbl);
                        hLabels.Children.Add(new TextBlock { Text = (entry.IsCountup ? "START " : "FROM ") + sRef.ToString("yyyy.MM.dd"), FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Muted });
                        horizonWrap.Children.Add(hLabels);
                        horizonWrap.Children.Add(BuildProgressTrack(realRatio, 4.5, p.Accent, p.Track));
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
                    int maxTotal = opts.RatioIndex == 1 ? 4 : (isLock ? 6 : 5);
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

        // --- Template 1: Atmosphere / Aurora Illustration ---
        static void BuildAtmosphereCard(Grid root, int w, int h, List<ShareEntry> entries, CardOpts opts, string skinId, SkinPalette sk, CardPalette p)
        {
            var card = BuildCardShell(root, w, h, sk, p, true, opts.RoundedCorners, opts.PaperTexture);
            var cardGrid = new Grid { ClipToBounds = true };
            card.Child = cardGrid;

            bool isLock = opts.RatioIndex == 3;
            double artSize = opts.RatioIndex == 2 ? 260 : (opts.RatioIndex == 1 ? 230 : 300);
            cardGrid.Children.Add(new Border
            {
                Child = BuildAmbientArt(skinId, p.DetailHex, artSize, skinId == "default" ? 0.12 : 0.19),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -artSize * 0.10, -artSize * 0.08)
            });
            cardGrid.Children.Add(new Ellipse
            {
                Width = 360, Height = 360, Fill = SoftGlow(p.DetailHex, skinId == "default" ? (byte)38 : (byte)64),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -90, -90), IsHitTestVisible = false
            });
            cardGrid.Children.Add(new Ellipse
            {
                Width = 300, Height = 300, Fill = SoftGlow(sk.Accent, (byte)28),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(-110, -110, 0, 0), IsHitTestVisible = false
            });

            var contentPad = new Border
            {
                Padding = isLock
                    ? new Thickness(38, 300, 38, 64)
                    : (opts.RatioIndex == 2 ? new Thickness(44, 28, 44, 24) : (opts.RatioIndex == 1 ? new Thickness(32, 26, 32, 22) : new Thickness(36, 32, 36, 28)))
            };
            cardGrid.Children.Add(contentPad);

            var now = DateTime.Now;
            string quote = string.IsNullOrWhiteSpace(opts.Quote) ? LiteraryQuotes[0] : opts.Quote.Trim();

            if (!opts.IsList && entries.Count > 0)
            {
                var entry = entries[Math.Max(0, Math.Min(opts.EntryIndex, entries.Count - 1))];
                bool isNum;
                string unitHint;
                string dayStr = EntryDayNumber(entry, now, out isNum, out unitHint);

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
                    left.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(entry.Name) ? "倒计时" : entry.Name, FontSize = 32, FontWeight = FontWeights.Bold, Foreground = p.Text, TextWrapping = TextWrapping.Wrap });
                    left.Children.Add(new TextBlock { Text = DetailedDurationText(entry, now), FontSize = 11.5, Foreground = p.Accent, Margin = new Thickness(0, 8, 0, 0) });

                    var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }; Grid.SetColumn(right, 1); mid.Children.Add(right);
                    right.Children.Add(BuildHeroDayMetric(dayStr, isNum, 84, "天", SansFont, 18, new Thickness(6, 0, 0, 14), "就是今天", "已如约而至", SansFont, 32, p, true, false));
                }
                else
                {
                    var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }; layout.Children.Add(center);
                    center.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(entry.Name) ? "倒计时" : entry.Name, FontSize = opts.RatioIndex == 1 ? 28 : 34, FontWeight = FontWeights.Bold, Foreground = p.Text, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = w - 120 });
                    center.Children.Add(new Border { Width = 36, Height = 2.5, CornerRadius = new CornerRadius(1.2), Background = p.Accent, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 16), Opacity = 0.9 });
                    var metric = BuildHeroDayMetric(dayStr, isNum, HeroNumberSize(opts.RatioIndex, dayStr), "天", SansFont, 19, new Thickness(8, 0, 0, 16), "就是今天", "已如约而至", SansFont, opts.RatioIndex == 1 ? 34 : 42, p, true, false);
                    center.Children.Add(metric);
                    var pill = BuildPill(RelativeSpanTag(entry, now) + " · " + DetailedDurationText(entry, now), p, true);
                    pill.Margin = new Thickness(0, 16, 0, 0);
                    center.Children.Add(pill);
                }
            }
            else
            {
                int maxDays;
                var ordered = OrderedEntries(entries, now, out maxDays);
                var visible = ordered.Take(opts.RatioIndex == 2 ? 4 : 5).ToList();
                var rest = visible.Skip(1).ToList();

                var layout = new DockPanel(); contentPad.Child = layout;
                var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, 12) }; DockPanel.SetDock(topBar, Dock.Top); layout.Children.Add(topBar);
                var calBadge = BuildCalendarBadge(now, p); DockPanel.SetDock(calBadge, Dock.Right); topBar.Children.Add(calBadge);
                var seal = BuildSealStamp(skinId, p.DetailHex);
                if (seal != null) { seal.Margin = new Thickness(0, 0, 8, 0); DockPanel.SetDock(seal, Dock.Right); topBar.Children.Add(seal); }
                var titleSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                titleSp.Children.Add(new TextBlock { Text = "UPCOMING · " + now.Year + " · " + ordered.Count + " 个里程碑", FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Accent });
                titleSp.Children.Add(new TextBlock { Text = "值得期待的日子", FontSize = opts.RatioIndex == 2 ? 19 : 21, FontWeight = FontWeights.Bold, Foreground = p.Text, Margin = new Thickness(0, 2, 0, 0) });
                topBar.Children.Add(titleSp);

                var footer = new StackPanel { Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
                footer.Children.Add(BuildYearProgress(now, p, opts.RatioIndex != 0));
                var fRow = BuildQuoteFooterRow(quote, false, SansFont, 11.5, opts.ShowWatermark, p, opts.RatioIndex != 2);
                fRow.Margin = new Thickness(0, 9, 0, 0);
                footer.Children.Add(fRow);

                if (opts.RatioIndex == 2)
                {
                    var bento = new Grid { VerticalAlignment = VerticalAlignment.Center };
                    AddCols(bento, new GridLength(1.05, GridUnitType.Star), new GridLength(12), new GridLength(1.25, GridUnitType.Star));
                    layout.Children.Add(bento);
                    if (visible.Count > 0) { var hero = BuildHeroFocusTile(visible[0], now, 2, opts.ShowDate, skinId, p, false); Grid.SetColumn(hero, 0); bento.Children.Add(hero); }
                    var rightStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(rightStack, 2); bento.Children.Add(rightStack);
                    if (rest.Count > 0) PopulateScheduleCards(rightStack, rest, 1, now, 2, opts.ShowDate, p, true, maxDays);
                }
                else
                {
                    var bodyStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; layout.Children.Add(bodyStack);
                    if (visible.Count > 0) bodyStack.Children.Add(BuildHeroFocusTile(visible[0], now, opts.RatioIndex, opts.ShowDate, skinId, p, false));
                    if (rest.Count > 0)
                    {
                        var grid2 = new Grid { Margin = new Thickness(0, 10, 0, 0) };
                        AddCols(grid2, new GridLength(1, GridUnitType.Star), new GridLength(9), new GridLength(1, GridUnitType.Star));
                        int rowCount = (rest.Count + 1) / 2;
                        for (int r = 0; r < rowCount; r++) grid2.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        for (int i = 0; i < rest.Count; i++)
                        {
                            var tile = BuildBentoEventTile(rest[i], i + 2, now, opts.ShowDate, p);
                            tile.Margin = new Thickness(0, 0, 0, (i / 2) == rowCount - 1 ? 0 : 9);
                            Grid.SetRow(tile, i / 2);
                            Grid.SetColumn(tile, (i % 2) == 0 ? 0 : 2);
                            if (rest.Count % 2 == 1 && i == rest.Count - 1) Grid.SetColumnSpan(tile, 3);
                            grid2.Children.Add(tile);
                        }
                        bodyStack.Children.Add(grid2);
                    }
                }
            }
        }

        // --- Template 2: Boarding Pass / Time Ticket ---
        static void PopulateTicketStub(Border stubArea, string quote, string serialText, bool showWatermark, CardPalette p, int ratioIndex)
        {
            var stubDock = new DockPanel(); stubArea.Child = stubDock;
            var rightStub = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(14, 0, 0, 0) };
            DockPanel.SetDock(rightStub, Dock.Right); stubDock.Children.Add(rightStub);
            var bars = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            double[] barWidths = { 2, 1, 3, 1, 2, 1, 1, 3, 2, 1, 2, 1, 3, 1, 2, 1, 2 };
            foreach (double bw in barWidths)
                bars.Children.Add(new Border { Width = bw, Height = ratioIndex == 2 ? 22 : 28, Background = p.Text, Opacity = 0.65, Margin = new Thickness(1.4, 0, 0, 0) });
            rightStub.Children.Add(bars);
            rightStub.Children.Add(new TextBlock { Text = serialText, FontFamily = MonoFont, FontSize = 8.5, Foreground = p.Muted, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 0) });

            var leftStub = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stubDock.Children.Add(leftStub);
            leftStub.Children.Add(new TextBlock { Text = "“" + quote + "”", FontFamily = SerifFont, FontSize = 11.5, Foreground = p.Text, TextWrapping = TextWrapping.Wrap, Opacity = 0.88 });
            var wm = BuildWatermark(showWatermark, p.Muted);
            if (wm != null) { wm.Margin = new Thickness(0, 6, 0, 0); leftStub.Children.Add(wm); }
        }

        static void BuildTicketCard(Grid root, int w, int h, List<ShareEntry> entries, CardOpts opts, string skinId, SkinPalette sk, CardPalette p)
        {
            double cw = w, ch = h;
            bool isLock = opts.RatioIndex == 3;
            double notchY = isLock ? ch * 0.78 : (opts.RatioIndex == 0 ? ch * 0.74 : ch * 0.72);
            double notchR = 12;
            double cornerR = opts.RoundedCorners ? 16 : 0;

            var notches = new GeometryGroup();
            notches.Children.Add(new EllipseGeometry(new Point(0, notchY), notchR, notchR));
            notches.Children.Add(new EllipseGeometry(new Point(cw, notchY), notchR, notchR));
            var ticketGeo = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, cw, ch), cornerR, cornerR), notches);
            ticketGeo.Freeze();

            var cardBg = new LinearGradientBrush(C_(sk.Bg1), C_(sk.Bg2), new Point(0, 0), new Point(0, 1));
            cardBg.Freeze();
            var borderBrush = Brush(skinId != "default" ? HexA("68", p.DetailHex) : (p.Light ? "#22000000" : "#2EFFFFFF"));

            var cardBorder = new System.Windows.Shapes.Path
            {
                Data = ticketGeo,
                Fill = cardBg,
                Stroke = opts.RoundedCorners ? borderBrush : Brushes.Transparent,
                StrokeThickness = opts.RoundedCorners ? 1.2 : 0
            };
            root.Children.Add(cardBorder);

            var inner = new Grid { Width = cw, Height = ch, Clip = ticketGeo };
            root.Children.Add(inner);
            if (opts.PaperTexture) inner.Children.Add(new Rectangle { Fill = PaperGrainBrush(p.Light), IsHitTestVisible = false });
            inner.Children.Add(new Border { Child = BuildAmbientArt(skinId, p.DetailHex, opts.RatioIndex == 2 ? 150 : 185, skinId == "default" ? 0.09 : 0.14), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, isLock ? 280 : 52, 20, 0) });
            inner.Children.Add(new Line { X1 = notchR + 8, X2 = cw - notchR - 8, Y1 = notchY, Y2 = notchY, Stroke = p.Hairline, StrokeThickness = 1.2, StrokeDashArray = new DoubleCollection { 4, 4 } });

            inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(notchY) });
            inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ch - notchY) });

            var topArea = new Border
            {
                Padding = isLock
                    ? new Thickness(38, 300, 38, 22)
                    : (opts.RatioIndex == 2 ? new Thickness(44, 26, 44, 18) : new Thickness(36, 28, 36, 18))
            };
            Grid.SetRow(topArea, 0); inner.Children.Add(topArea);
            var stubArea = new Border { Padding = opts.RatioIndex == 2 ? new Thickness(44, 14, 44, 18) : new Thickness(36, 16, 36, isLock ? 48 : 18) };
            Grid.SetRow(stubArea, 1); inner.Children.Add(stubArea);

            var now = DateTime.Now;
            string quote = string.IsNullOrWhiteSpace(opts.Quote) ? LiteraryQuotes[0] : opts.Quote.Trim();
            var topDock = new DockPanel(); topArea.Child = topDock;

            if (!opts.IsList && entries.Count > 0)
            {
                var entry = entries[Math.Max(0, Math.Min(opts.EntryIndex, entries.Count - 1))];
                bool isNum;
                string unitHint;
                string dayStr = EntryDayNumber(entry, now, out isNum, out unitHint);

                var header = new DockPanel(); DockPanel.SetDock(header, Dock.Top); topDock.Children.Add(header);
                var seal = BuildSealStamp(skinId, p.DetailHex);
                if (seal != null) { DockPanel.SetDock(seal, Dock.Right); header.Children.Add(seal); }
                else
                {
                    var badge = BuildPill("BOARDING PASS", p, true);
                    DockPanel.SetDock(badge, Dock.Right); header.Children.Add(badge);
                }
                header.Children.Add(new TextBlock { Text = "TIME PASS · NO." + now.ToString("yyyyMMdd"), FontFamily = MonoFont, FontSize = 10, Foreground = p.Accent, VerticalAlignment = VerticalAlignment.Center });

                var route = new Grid { Margin = new Thickness(0, 12, 0, 0) };
                DockPanel.SetDock(route, Dock.Bottom); topDock.Children.Add(route);
                AddCols(route, new GridLength(1, GridUnitType.Star), new GridLength(1, GridUnitType.Star), new GridLength(1, GridUnitType.Star));
                var c1 = new StackPanel(); c1.Children.Add(new TextBlock { Text = "DEPART · 今日", FontFamily = MonoFont, FontSize = 8.5, Foreground = p.Muted }); c1.Children.Add(new TextBlock { Text = now.ToString("yyyy.MM.dd"), FontFamily = NumFont, FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = p.Text, Margin = new Thickness(0, 2, 0, 0) }); Grid.SetColumn(c1, 0); route.Children.Add(c1);
                var c2 = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center }; c2.Children.Add(new TextBlock { Text = "ARRIVE · 抵达", FontFamily = MonoFont, FontSize = 8.5, Foreground = p.Muted, HorizontalAlignment = HorizontalAlignment.Center }); c2.Children.Add(new TextBlock { Text = opts.ShowDate ? entry.Date.ToString("yyyy.MM.dd") : WeekdayZh(entry.Date), FontFamily = NumFont, FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = p.Accent, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) }); Grid.SetColumn(c2, 1); route.Children.Add(c2);
                var c3 = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right }; c3.Children.Add(new TextBlock { Text = "SPAN · 跨度", FontFamily = MonoFont, FontSize = 8.5, Foreground = p.Muted, HorizontalAlignment = HorizontalAlignment.Right }); c3.Children.Add(new TextBlock { Text = CompactWeekText(entry, now), FontFamily = NumFont, FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = p.Text, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 2, 0, 0) }); Grid.SetColumn(c3, 2); route.Children.Add(c3);

                var mid = new Grid { VerticalAlignment = VerticalAlignment.Center }; topDock.Children.Add(mid);
                if (opts.RatioIndex == 2)
                {
                    AddCols(mid, new GridLength(1.15, GridUnitType.Star), new GridLength(1, GridUnitType.Star));
                    var ml = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(ml, 0); mid.Children.Add(ml);
                    ml.Children.Add(new TextBlock { Text = "DESTINATION · 目的地", FontFamily = MonoFont, FontSize = 9, Foreground = p.Muted });
                    ml.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(entry.Name) ? "倒计时" : entry.Name, FontFamily = SerifFont, FontSize = 28, FontWeight = FontWeights.Bold, Foreground = p.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
                    var mr = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right }; Grid.SetColumn(mr, 1); mid.Children.Add(mr);
                    mr.Children.Add(BuildHeroDayMetric(dayStr, isNum, 74, "天", SansFont, 16, new Thickness(6, 0, 0, 12), "就是今天", "已如约抵达", SerifFont, 28, p, false, false));
                }
                else
                {
                    var ms = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; mid.Children.Add(ms);
                    ms.Children.Add(new TextBlock { Text = "DESTINATION · 奔赴目的地", FontFamily = MonoFont, FontSize = 9, Foreground = p.Muted });
                    ms.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(entry.Name) ? "倒计时" : entry.Name, FontFamily = SerifFont, FontSize = opts.RatioIndex == 1 ? 26 : 34, FontWeight = FontWeights.Bold, Foreground = p.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 8) });
                    ms.Children.Add(BuildHeroDayMetric(dayStr, isNum, opts.RatioIndex == 1 ? 66 : 86, "天后抵达", SansFont, 14, new Thickness(8, 0, 0, 14), "就是今天", "已如约抵达", SerifFont, 32, p, false, false));
                }

                PopulateTicketStub(stubArea, quote, "DAYS-" + entry.Date.ToString("yyMMdd"), opts.ShowWatermark, p, opts.RatioIndex);
            }
            else
            {
                int maxDays;
                var ordered = OrderedEntries(entries, now, out maxDays);
                var visible = ordered.Take(opts.RatioIndex == 0 ? 4 : 3).ToList();

                var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(header, Dock.Top); topDock.Children.Add(header);
                var badge = BuildSealStamp(skinId, p.DetailHex) ?? BuildPill("ITINERARY · " + ordered.Count + " 站", p, true);
                DockPanel.SetDock(badge, Dock.Right); header.Children.Add(badge);
                var hLeft = new StackPanel();
                hLeft.Children.Add(new TextBlock { Text = "TIME ITINERARY · " + now.Year + " 联程票", FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Accent });
                hLeft.Children.Add(new TextBlock { Text = "值得期待的下一站", FontFamily = SerifFont, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = p.Text, Margin = new Thickness(0, 2, 0, 0) });
                header.Children.Add(hLeft);

                var listBody = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; topDock.Children.Add(listBody);
                if (opts.RatioIndex == 0 && visible.Count > 0)
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

        // --- NEW Template 3: Swiss International Typographic Grid Poster ---
        static void BuildSwissGridCard(Grid root, int w, int h, List<ShareEntry> entries, CardOpts opts, string skinId, SkinPalette sk, CardPalette p)
        {
            var card = BuildCardShell(root, w, h, sk, p, false, opts.RoundedCorners, opts.PaperTexture);
            bool isLock = opts.RatioIndex == 3;
            var now = DateTime.Now;
            string quote = string.IsNullOrWhiteSpace(opts.Quote) ? LiteraryQuotes[0] : opts.Quote.Trim();

            var wrap = new Grid
            {
                Margin = isLock
                    ? new Thickness(34, 290, 34, 58)
                    : (opts.RatioIndex == 2 ? new Thickness(38, 26, 38, 22) : new Thickness(32, 28, 32, 26))
            };
            card.Child = wrap;

            var dock = new DockPanel();
            wrap.Children.Add(dock);

            // Architectural top index strip
            var topGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            AddCols(topGrid, new GridLength(1, GridUnitType.Star), new GridLength(1, GridUnitType.Star), new GridLength(1, GridUnitType.Star));
            DockPanel.SetDock(topGrid, Dock.Top);
            dock.Children.Add(topGrid);

            var tLeft = new TextBlock { Text = "NO. " + now.Year + " / W" + IsoWeekOfYear(now).ToString("00"), FontFamily = MonoFont, FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = p.Text };
            var tMid = new TextBlock { Text = MonthEn(now.Month) + " " + now.Day.ToString("00") + " · " + ShortWeekdayZh(now), FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Muted, HorizontalAlignment = HorizontalAlignment.Center };
            var tRight = new TextBlock { Text = "SWISS CHRONOGRAPH", FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Accent, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(tLeft, 0); Grid.SetColumn(tMid, 1); Grid.SetColumn(tRight, 2);
            topGrid.Children.Add(tLeft); topGrid.Children.Add(tMid); topGrid.Children.Add(tRight);

            var topRule = new Border { Height = 2.5, Background = p.Text, Margin = new Thickness(0, 0, 0, 16) };
            DockPanel.SetDock(topRule, Dock.Top);
            dock.Children.Add(topRule);

            // Bottom Swiss footer grid
            var footStack = new StackPanel();
            DockPanel.SetDock(footStack, Dock.Bottom);
            dock.Children.Add(footStack);
            footStack.Children.Add(new Border { Height = 1.2, Background = p.Text, Opacity = 0.65, Margin = new Thickness(0, 12, 0, 10) });
            footStack.Children.Add(BuildQuoteFooterRow(quote, false, SansFont, 11, opts.ShowWatermark, p, true));

            if (!opts.IsList && entries.Count > 0)
            {
                var entry = entries[Math.Max(0, Math.Min(opts.EntryIndex, entries.Count - 1))];
                bool isNum;
                string unitHint;
                string dayStr = EntryDayNumber(entry, now, out isNum, out unitHint);

                var mainGrid = new Grid();
                dock.Children.Add(mainGrid);

                if (opts.RatioIndex == 2)
                {
                    AddCols(mainGrid, new GridLength(1.0, GridUnitType.Star), new GridLength(1.2, GridUnitType.Star));
                    var leftCol = new DockPanel { Margin = new Thickness(0, 0, 20, 0) };
                    Grid.SetColumn(leftCol, 0); mainGrid.Children.Add(leftCol);

                    var metaBot = new StackPanel();
                    DockPanel.SetDock(metaBot, Dock.Bottom); leftCol.Children.Add(metaBot);
                    DateTime sRef;
                    double rProg = RealEntryProgress(entry, now, out sRef);
                    metaBot.Children.Add(new TextBlock { Text = (entry.IsCountup ? "ORIGIN " : "TARGET ") + entry.Date.ToString("yyyy.MM.dd") + " · " + ((int)Math.Round(rProg * 100)) + "%", FontFamily = MonoFont, FontSize = 10, Foreground = p.Muted, Margin = new Thickness(0, 0, 0, 6) });
                    metaBot.Children.Add(BuildProgressTrack(rProg, 5, p.Accent, p.Track));

                    var titleCol = new StackPanel(); leftCol.Children.Add(titleCol);
                    titleCol.Children.Add(new Border { Height = 6, Width = 44, Background = p.Accent, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 12) });
                    titleCol.Children.Add(new TextBlock { Text = entry.Name, FontSize = 36, FontWeight = FontWeights.Bold, Foreground = p.Text, TextWrapping = TextWrapping.Wrap });
                    titleCol.Children.Add(new TextBlock { Text = RelativeSpanTag(entry, now) + " / " + CompactWeekText(entry, now), FontFamily = MonoFont, FontSize = 11, Foreground = p.Accent, Margin = new Thickness(0, 8, 0, 0) });

                    var rightCol = new Border { BorderBrush = p.Hairline, BorderThickness = new Thickness(1, 0, 0, 0), Padding = new Thickness(24, 0, 0, 0) };
                    Grid.SetColumn(rightCol, 1); mainGrid.Children.Add(rightCol);
                    var numWrap = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
                    rightCol.Child = numWrap;
                    numWrap.Children.Add(new TextBlock { Text = isNum ? dayStr : (dayStr == "今天" ? "00" : "END"), FontFamily = NumFont, FontSize = dayStr.Length >= 3 ? 124 : 156, FontWeight = FontWeights.Bold, Foreground = p.Accent, LineHeight = 140, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, HorizontalAlignment = HorizontalAlignment.Right });
                    numWrap.Children.Add(new TextBlock { Text = entry.IsCountup ? "DAYS ELAPSED · 已历天数" : "DAYS TO ARRIVAL · 剩余天数", FontFamily = MonoFont, FontSize = 10, Foreground = p.Muted, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 4, 0) });
                }
                else
                {
                    var vStack = new DockPanel();
                    mainGrid.Children.Add(vStack);

                    // Top bold title block
                    var topMeta = new DockPanel();
                    DockPanel.SetDock(topMeta, Dock.Top);
                    vStack.Children.Add(topMeta);

                    var accentBlock = new Border { Width = 10, Height = 56, Background = p.Accent, Margin = new Thickness(0, 4, 14, 0), VerticalAlignment = VerticalAlignment.Top };
                    DockPanel.SetDock(accentBlock, Dock.Left);
                    topMeta.Children.Add(accentBlock);

                    var tStack = new StackPanel();
                    topMeta.Children.Add(tStack);
                    tStack.Children.Add(new TextBlock { Text = entry.IsCountup ? "ANNIVERSARY ARCHIVE" : "TARGET MILESTONE", FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Accent, Margin = new Thickness(0, 0, 0, 4) });
                    tStack.Children.Add(new TextBlock { Text = entry.Name, FontSize = opts.RatioIndex == 1 ? 30 : 38, FontWeight = FontWeights.Bold, Foreground = p.Text, TextWrapping = TextWrapping.Wrap });
                    if (opts.ShowDate)
                    {
                        string dt = entry.Date.ToString("yyyy.MM.dd") + " · " + WeekdayZh(entry.Date);
                        if (!string.IsNullOrEmpty(entry.LunarLabel)) dt += " · " + entry.LunarLabel;
                        tStack.Children.Add(new TextBlock { Text = dt, FontFamily = MonoFont, FontSize = 10.5, Foreground = p.Muted, Margin = new Thickness(0, 5, 0, 0) });
                    }

                    // Bottom architectural data bar
                    var dataStrip = new Grid { Margin = new Thickness(0, 12, 0, 0) };
                    DockPanel.SetDock(dataStrip, Dock.Bottom);
                    vStack.Children.Add(dataStrip);
                    AddCols(dataStrip, new GridLength(1, GridUnitType.Star), new GridLength(1, GridUnitType.Star));
                    DateTime sRef;
                    double rProg = RealEntryProgress(entry, now, out sRef);
                    var dLeft = new StackPanel();
                    dLeft.Children.Add(new TextBlock { Text = "PROGRESS · 进度", FontFamily = MonoFont, FontSize = 9, Foreground = p.Muted, Margin = new Thickness(0, 0, 0, 5) });
                    dLeft.Children.Add(BuildProgressTrack(rProg, 5, p.Accent, p.Track));
                    dLeft.Children.Add(new TextBlock { Text = sRef.ToString("MM.dd") + " → " + entry.Date.ToString("MM.dd") + "  " + ((int)Math.Round(rProg * 100)) + "%", FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Text, Margin = new Thickness(0, 5, 0, 0) });
                    Grid.SetColumn(dLeft, 0); dataStrip.Children.Add(dLeft);

                    var dRight = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
                    dRight.Children.Add(new TextBlock { Text = "INTERVAL · 跨度", FontFamily = MonoFont, FontSize = 9, Foreground = p.Muted, HorizontalAlignment = HorizontalAlignment.Right });
                    dRight.Children.Add(new TextBlock { Text = CompactWeekText(entry, now), FontFamily = NumFont, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = p.Text, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 2, 0, 0) });
                    Grid.SetColumn(dRight, 1); dataStrip.Children.Add(dRight);

                    // Monumental Swiss oversized numeral in center
                    var heroBox = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 12, 0, 12) };
                    vStack.Children.Add(heroBox);
                    heroBox.Children.Add(new Border { Height = 1, Background = p.Hairline, VerticalAlignment = VerticalAlignment.Top });
                    heroBox.Children.Add(new Border { Height = 1, Background = p.Hairline, VerticalAlignment = VerticalAlignment.Bottom });

                    var bigStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 18, 0, 16) };
                    heroBox.Children.Add(bigStack);
                    double swissSize = opts.RatioIndex == 1 ? (dayStr.Length >= 3 ? 118 : 148) : (dayStr.Length >= 3 ? 156 : 196);
                    bigStack.Children.Add(new TextBlock
                    {
                        Text = isNum ? dayStr : (dayStr == "今天" ? "00" : "—"),
                        FontFamily = NumFont,
                        FontSize = swissSize,
                        FontWeight = FontWeights.Bold,
                        Foreground = p.Accent,
                        LineHeight = swissSize * 0.86,
                        LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                        VerticalAlignment = VerticalAlignment.Bottom
                    });
                    var unitStack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(12, 0, 0, 14) };
                    unitStack.Children.Add(new TextBlock { Text = entry.IsCountup ? "DAYS\nELAPSED" : "DAYS\nLEFT", FontFamily = MonoFont, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = p.Muted, LineHeight = 14 });
                    unitStack.Children.Add(new TextBlock { Text = isNum ? unitHint : (dayStr == "今天" ? "就是今天" : "已抵达"), FontSize = 18, FontWeight = FontWeights.Bold, Foreground = p.Text, Margin = new Thickness(0, 6, 0, 0) });
                    bigStack.Children.Add(unitStack);
                }
            }
            else
            {
                int maxDays;
                var ordered = OrderedEntries(entries, now, out maxDays);
                var visible = ordered.Take(opts.RatioIndex == 1 ? 4 : 5).ToList();
                var listStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                dock.Children.Add(listStack);

                for (int i = 0; i < visible.Count; i++)
                {
                    var item = visible[i];
                    bool isNum; string u;
                    string dStr = EntryDayNumber(item, now, out isNum, out u);

                    var rowBorder = new Border { BorderBrush = p.Hairline, BorderThickness = new Thickness(0, 0, 0, i == visible.Count - 1 ? 0 : 1), Padding = new Thickness(0, 11, 0, 11) };
                    var row = new DockPanel(); rowBorder.Child = row;

                    var numTxt = new TextBlock { Text = isNum ? (dStr + " D") : dStr, FontFamily = NumFont, FontSize = i == 0 ? 34 : 24, FontWeight = FontWeights.Bold, Foreground = i == 0 ? p.Accent : p.Text, VerticalAlignment = VerticalAlignment.Center };
                    DockPanel.SetDock(numTxt, Dock.Right); row.Children.Add(numTxt);

                    var idxTxt = new TextBlock { Text = (i + 1).ToString("00"), FontFamily = MonoFont, FontSize = 11, Foreground = p.Accent, Width = 28, VerticalAlignment = VerticalAlignment.Center };
                    DockPanel.SetDock(idxTxt, Dock.Left); row.Children.Add(idxTxt);

                    var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    info.Children.Add(new TextBlock { Text = item.Name, FontSize = i == 0 ? 20 : 16, FontWeight = FontWeights.Bold, Foreground = p.Text, TextTrimming = TextTrimming.CharacterEllipsis });
                    if (opts.ShowDate) info.Children.Add(new TextBlock { Text = item.Date.ToString("yyyy.MM.dd") + " · " + RelativeSpanTag(item, now), FontFamily = MonoFont, FontSize = 9.5, Foreground = p.Muted, Margin = new Thickness(0, 2, 0, 0) });
                    row.Children.Add(info);
                    listStack.Children.Add(rowBorder);
                }
            }
        }

        // --- NEW Template 4: Analog Polaroid Photo Card (Supports User Photo!) ---
        static BitmapSource LoadPhotoSafe(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
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
            catch
            {
                return null;
            }
        }

        static void BuildPolaroidCard(Grid root, int w, int h, List<ShareEntry> entries, CardOpts opts, string skinId, SkinPalette sk, CardPalette p)
        {
            var paperLight = new SkinPalette { Bg1 = "#FAF8F3", Bg2 = "#F1ECE2", Text = "#24201C", Muted = "#7A736A", Accent = sk.Accent, Detail = sk.Detail };
            var paperDark = new SkinPalette { Bg1 = "#23211F", Bg2 = "#191716", Text = "#F4EFE6", Muted = "#9F978D", Accent = sk.Accent, Detail = sk.Detail };
            var paperSk = p.Light ? paperLight : paperDark;
            var paperPal = BuildPalette(skinId, paperSk, p.Light, sk.Detail);

            var card = BuildCardShell(root, w, h, paperSk, paperPal, false, opts.RoundedCorners, true);
            bool isWide = opts.RatioIndex == 2;
            bool isLock = opts.RatioIndex == 3;
            var now = DateTime.Now;
            string quote = string.IsNullOrWhiteSpace(opts.Quote) ? LiteraryQuotes[0] : opts.Quote.Trim();

            var entry = entries.Count > 0 ? entries[Math.Max(0, Math.Min(opts.EntryIndex, entries.Count - 1))] : new ShareEntry { Name = "期待之日", Date = now.AddDays(30), Enabled = true };
            bool isNum; string unitHint;
            string dayStr = EntryDayNumber(entry, now, out isNum, out unitHint);

            var photoBmp = LoadPhotoSafe(opts.PhotoPath);

            // Helper to construct the framed film photograph canvas
            Func<Grid> buildPhotoFrame = () =>
            {
                var frame = new Grid { ClipToBounds = true };
                if (photoBmp != null)
                {
                    frame.Background = new ImageBrush(photoBmp) { Stretch = Stretch.UniformToFill };
                    // Cinematic vignette overlay for legible film badge
                    var vig = new LinearGradientBrush(Color.FromArgb(40, 0, 0, 0), Color.FromArgb(140, 0, 0, 0), new Point(0, 0), new Point(0, 1));
                    vig.Freeze();
                    frame.Children.Add(new Rectangle { Fill = vig });
                }
                else
                {
                    // Lush artistic gradient + ambient artwork fallback when no user photo is attached
                    var artGrad = new LinearGradientBrush(C_(sk.Bg1), C_(sk.Accent), new Point(0, 0), new Point(1, 1));
                    artGrad.Freeze();
                    frame.Background = artGrad;
                    var dim = new LinearGradientBrush(Color.FromArgb(30, 0, 0, 0), Color.FromArgb(130, 0, 0, 0), new Point(0, 0), new Point(0, 1));
                    dim.Freeze();
                    frame.Children.Add(new Rectangle { Fill = dim });
                    frame.Children.Add(new Border
                    {
                        Child = BuildAmbientArt(skinId, "#FFFFFF", isWide ? 180 : 230, 0.28),
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                    });
                }

                // Subtle analog film grain over photo
                frame.Children.Add(new Rectangle { Fill = PaperGrainBrush(false), Opacity = 0.85, IsHitTestVisible = false });

                // Film timestamp stamp in bottom-right of photo (classic Contax / Kodak orange-amber date stamp)
                var stamp = new TextBlock
                {
                    Text = "'" + now.ToString("yy MM dd"),
                    FontFamily = MonoFont,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brush("#E8F2B066"),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 14, 12)
                };
                frame.Children.Add(stamp);

                // Floating countdown pill in top-left of photo
                var topBadge = new Border
                {
                    Background = Brush("#66000000"),
                    BorderBrush = Brush("#44FFFFFF"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 5, 10, 5),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(14, 14, 0, 0)
                };
                topBadge.Child = new TextBlock
                {
                    Text = (entry.IsCountup ? "DAY " : "T-MINUS ") + (isNum ? dayStr : "00") + " · " + RelativeSpanTag(entry, now),
                    FontFamily = MonoFont,
                    FontSize = 9.5,
                    Foreground = Brushes.White
                };
                frame.Children.Add(topBadge);

                // Overlay Hero big number on bottom-left of photo
                var photoHero = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(16, 0, 0, 12)
                };
                photoHero.Children.Add(new TextBlock
                {
                    Text = isNum ? dayStr : (dayStr == "今天" ? "今天" : "已抵达"),
                    FontFamily = isNum ? NumFont : SerifFont,
                    FontSize = isNum ? (opts.RatioIndex == 1 ? 56 : 74) : 32,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    LineHeight = isNum ? (opts.RatioIndex == 1 ? 52 : 68) : 34,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    VerticalAlignment = VerticalAlignment.Bottom
                });
                if (isNum)
                {
                    photoHero.Children.Add(new TextBlock
                    {
                        Text = unitHint,
                        FontFamily = SerifFont,
                        FontSize = 15,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brush("#E6FFFFFF"),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(6, 0, 0, 8)
                    });
                }
                frame.Children.Add(photoHero);
                return frame;
            };

            if (isWide)
            {
                var pad = new Border { Padding = new Thickness(24) };
                card.Child = pad;
                var cols = new Grid();
                AddCols(cols, new GridLength(1.25, GridUnitType.Star), new GridLength(24), new GridLength(0.95, GridUnitType.Star));
                pad.Child = cols;

                var photoWrap = new Border { CornerRadius = new CornerRadius(6), ClipToBounds = true, BorderBrush = paperPal.Hairline, BorderThickness = new Thickness(1), Child = buildPhotoFrame() };
                Grid.SetColumn(photoWrap, 0); cols.Children.Add(photoWrap);

                var right = new DockPanel { Margin = new Thickness(0, 8, 4, 8) };
                Grid.SetColumn(right, 2); cols.Children.Add(right);

                var foot = BuildQuoteFooterRow(quote, true, SerifFont, 11.5, opts.ShowWatermark, paperPal, true);
                DockPanel.SetDock(foot, Dock.Bottom); right.Children.Add(foot);

                var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                right.Children.Add(mid);
                mid.Children.Add(new TextBlock { Text = "POLAROID · " + now.Year, FontFamily = MonoFont, FontSize = 9.5, Foreground = paperPal.Accent, Margin = new Thickness(0, 0, 0, 6) });
                mid.Children.Add(new TextBlock { Text = entry.Name, FontFamily = SerifFont, FontSize = 30, FontWeight = FontWeights.Bold, Foreground = paperPal.Text, TextWrapping = TextWrapping.Wrap });
                if (opts.ShowDate)
                {
                    mid.Children.Add(new TextBlock { Text = entry.Date.ToString("yyyy.MM.dd") + " · " + WeekdayZh(entry.Date), FontFamily = MonoFont, FontSize = 10.5, Foreground = paperPal.Muted, Margin = new Thickness(0, 8, 0, 0) });
                }
                DateTime sRef;
                var pb = BuildProgressTrack(RealEntryProgress(entry, now, out sRef), 3.5, paperPal.Accent, paperPal.Track);
                pb.Margin = new Thickness(0, 16, 0, 0);
                mid.Children.Add(pb);
            }
            else
            {
                var pad = new Border { Padding = isLock ? new Thickness(26, 285, 26, 48) : new Thickness(24, 24, 24, 22) };
                card.Child = pad;

                var rows = new Grid();
                rows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(opts.RatioIndex == 1 ? 1.55 : 1.85, GridUnitType.Star) });
                rows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
                pad.Child = rows;

                var photoWrap = new Border { CornerRadius = new CornerRadius(5), ClipToBounds = true, BorderBrush = paperPal.Hairline, BorderThickness = new Thickness(1), Child = buildPhotoFrame() };
                Grid.SetRow(photoWrap, 0); rows.Children.Add(photoWrap);

                // Classic Polaroid wide paper chin below photo
                var chin = new DockPanel { Margin = new Thickness(4, 18, 4, 2) };
                Grid.SetRow(chin, 1); rows.Children.Add(chin);

                var foot = BuildQuoteFooterRow(quote, true, SerifFont, 11.5, opts.ShowWatermark, paperPal, true);
                DockPanel.SetDock(foot, Dock.Bottom); chin.Children.Add(foot);

                var titleArea = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
                chin.Children.Add(titleArea);

                var seal = BuildSealStamp(skinId, paperPal.DetailHex);
                if (seal != null) { DockPanel.SetDock(seal, Dock.Right); titleArea.Children.Add(seal); }

                var txtStack = new StackPanel();
                titleArea.Children.Add(txtStack);
                txtStack.Children.Add(new TextBlock { Text = entry.Name, FontFamily = SerifFont, FontSize = opts.RatioIndex == 1 ? 24 : 30, FontWeight = FontWeights.Bold, Foreground = paperPal.Text, TextTrimming = TextTrimming.CharacterEllipsis });
                if (opts.ShowDate)
                {
                    string meta = entry.Date.ToString("yyyy.MM.dd") + " · " + WeekdayZh(entry.Date);
                    if (!string.IsNullOrEmpty(entry.LunarLabel)) meta += " · " + entry.LunarLabel;
                    txtStack.Children.Add(new TextBlock { Text = meta, FontFamily = MonoFont, FontSize = 10, Foreground = paperPal.Muted, Margin = new Thickness(0, 4, 0, 0) });
                }
            }
        }

        static FrameworkElement BuildShareCardVisual(List<ShareEntry> entries, CardOpts opts, string currentConfigSkin, out int width, out int height)
        {
            if (opts.RatioIndex == 1) { width = 540; height = 540; }
            else if (opts.RatioIndex == 2) { width = 800; height = 450; }
            else if (opts.RatioIndex == 3) { width = 540; height = 1170; } // 9:19.5 Lockscreen (2x => 1080x2340)
            else { width = 540; height = 720; }

            bool light;
            string skinId = ResolveCardSkin(opts.SkinIndex, currentConfigSkin, entries, out light);
            var sk = PickSkinPalette(skinId, light);
            var palette = BuildPalette(skinId, sk, light, sk.Detail);

            var root = new Grid { Width = width, Height = height, ClipToBounds = true, UseLayoutRounding = true };
            System.Windows.Documents.TextElement.SetFontFamily(root, SansFont);
            TextOptions.SetTextRenderingMode(root, TextRenderingMode.Grayscale);
            TextOptions.SetTextFormattingMode(root, TextFormattingMode.Ideal);

            if (opts.TemplateIndex == 1) BuildAtmosphereCard(root, width, height, entries, opts, skinId, sk, palette);
            else if (opts.TemplateIndex == 2) BuildTicketCard(root, width, height, entries, opts, skinId, sk, palette);
            else if (opts.TemplateIndex == 3) BuildSwissGridCard(root, width, height, entries, opts, skinId, sk, palette);
            else if (opts.TemplateIndex == 4) BuildPolaroidCard(root, width, height, entries, opts, skinId, sk, palette);
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

        // Reusable Share Atelier 2.0 Panel (used both embedded inside Studio and standalone)
        static FrameworkElement BuildShareAtelierView(List<ShareEntry> entries, int initialIndex, string currentConfigSkin, Window hostWin, out Action disposeTimers)
        {
            var body = new Grid();
            AddCols(body, new GridLength(1.18, GridUnitType.Star), new GridLength(20), new GridLength(324));

            // Left Preview Studio Pane (supports drag-and-drop photo for Polaroid!)
            var previewPane = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                BorderThickness = new Thickness(1),
                AllowDrop = true,
                Background = Brush("#141518"),
                BorderBrush = Brush("#282A32"),
                ClipToBounds = true
            };
            Grid.SetColumn(previewPane, 0); body.Children.Add(previewPane);

            var previewCanvasGrid = new Grid();
            previewPane.Child = previewCanvasGrid;

            // Canvas top indicator - assures user of 100% clean full-bleed export
            var canvasHeader = new DockPanel { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(4, 2, 4, 0) };
            var badge100 = new Border
            {
                Background = Brush("#202229"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 3, 8, 3),
                HorizontalAlignment = HorizontalAlignment.Left,
                BorderBrush = Brush("#30333C"),
                BorderThickness = new Thickness(1)
            };
            badge100.Child = new TextBlock { Text = "✦ 所见即所得纯净导出 · 100% 满幅无多余黑底边框", FontFamily = MonoFont, FontSize = 9.5, Foreground = Brush("#8892B0") };
            canvasHeader.Children.Add(badge100);
            previewCanvasGrid.Children.Add(canvasHeader);

            var previewFrame = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 24, 0, 16),
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 32, ShadowDepth = 10, Direction = 270, Opacity = 0.32 }
            };
            var viewbox = new Viewbox { Stretch = Stretch.Uniform, Child = previewFrame };
            previewCanvasGrid.Children.Add(viewbox);

            // Right De-Dropdowned Control Studio Pane
            var controlPane = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(16), BorderThickness = new Thickness(1) };
            controlPane.SetResourceReference(Border.BackgroundProperty, "Surface");
            controlPane.SetResourceReference(Border.BorderBrushProperty, "Line");
            Grid.SetColumn(controlPane, 2); body.Children.Add(controlPane);

            var ctrlDock = new DockPanel(); controlPane.Child = ctrlDock;
            var actionStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) }; DockPanel.SetDock(actionStack, Dock.Bottom); ctrlDock.Children.Add(actionStack);

            var copyBtn = ActionButton("复制图片到剪贴板", () => { });
            copyBtn.SetResourceReference(Button.BackgroundProperty, "Accent");
            copyBtn.Foreground = Brushes.White; copyBtn.BorderThickness = new Thickness(0);
            copyBtn.Padding = new Thickness(16, 10, 16, 10); copyBtn.Margin = new Thickness(0, 0, 0, 7);
            actionStack.Children.Add(copyBtn);

            var saveBtn = ActionButton("保存为高清 PNG…", () => { });
            saveBtn.Padding = new Thickness(16, 9, 16, 9);
            actionStack.Children.Add(saveBtn);

            var statusHint = Label("", 10.5, "Muted");
            statusHint.HorizontalAlignment = HorizontalAlignment.Center; statusHint.Margin = new Thickness(0, 7, 0, 0);
            actionStack.Children.Add(statusHint);

            var form = new StackPanel();
            ctrlDock.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });

            bool isListMode = false;
            int selTpl = 0;
            int selRatio = 0;
            int selSkin = 0;
            string customPhoto = null;

            Action refresh = null;

            // Helper for segmented pills
            Func<string[], int, Action<int>, Border> makeSegmented = (labels, initSel, onSelect) =>
            {
                var wrap = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(3), Margin = new Thickness(0, 0, 0, 11) };
                wrap.SetResourceReference(Border.BackgroundProperty, "Field");
                var g = new Grid();
                for (int i = 0; i < labels.Length; i++) g.ColumnDefinitions.Add(new ColumnDefinition());
                wrap.Child = g;
                var btns = new List<Button>();
                Action<int> highlight = idx =>
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
                    var b = ActionButton(labels[i], () => { highlight(cap); onSelect(cap); });
                    b.BorderThickness = new Thickness(0);
                    b.Padding = new Thickness(6, 5.5, 6, 5.5);
                    b.FontSize = 11;
                    Grid.SetColumn(b, i);
                    g.Children.Add(b);
                    btns.Add(b);
                }
                highlight(initSel);
                return wrap;
            };

            // 1. Mode segmented pill
            var modeCap = Label("卡片内容", 10.5, "Muted"); modeCap.Margin = new Thickness(0, 0, 0, 5); form.Children.Add(modeCap);
            form.Children.Add(makeSegmented(new[] { "单日聚焦", "期待清单" }, 0, idx => { isListMode = idx == 1; if (refresh != null) refresh(); }));

            // 2. Day picker (single mode)
            var dayCap = Label("选择日子", 10.5, "Muted"); dayCap.Margin = new Thickness(0, 0, 0, 5); form.Children.Add(dayCap);
            var dayPicker = new ComboBox { Margin = new Thickness(0, 0, 0, 11) };
            foreach (var e in entries) dayPicker.Items.Add(e.Name + "  (" + e.Date.ToString("yyyy-MM-dd") + ")");
            dayPicker.SelectedIndex = Math.Max(0, Math.Min(initialIndex, Math.Max(0, entries.Count - 1)));
            form.Children.Add(dayPicker);

            // 3. Visual 5-Template Tile Grid
            var tplCap = Label("版式设计（5 套精选）", 10.5, "Muted"); tplCap.Margin = new Thickness(0, 0, 0, 6); form.Children.Add(tplCap);
            var tplGrid = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            form.Children.Add(tplGrid);
            string[] tplNames = { "单向历", "极光画", "登机牌", "大字报", "拍立得" };
            var tplCards = new List<Border>();
            Action syncTplCards = () =>
            {
                for (int i = 0; i < tplCards.Count; i++)
                {
                    bool on = i == selTpl;
                    tplCards[i].SetResourceReference(Border.BorderBrushProperty, on ? "Accent" : "Line");
                    tplCards[i].SetResourceReference(Border.BackgroundProperty, on ? "AccentSoft" : "Field");
                }
            };
            for (int i = 0; i < tplNames.Length; i++)
            {
                int cap = i;
                var tc = new Border
                {
                    Width = 53, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1.3),
                    Padding = new Thickness(4, 6, 4, 6), Margin = new Thickness(0, 0, 5, 5), Cursor = Cursors.Hand
                };
                var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                // Mini wireframe glyph
                var wf = new Border { Width = 24, Height = 18, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 0, 4), BorderThickness = new Thickness(1) };
                wf.SetResourceReference(Border.BorderBrushProperty, "Muted");
                wf.SetResourceReference(Border.BackgroundProperty, "Surface");
                var wfInner = new StackPanel { Margin = new Thickness(2.5), VerticalAlignment = VerticalAlignment.Center };
                if (i == 0) { wfInner.Children.Add(new Border { Height = 2, Width = 10, Background = Brush("#888888"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 2) }); wfInner.Children.Add(new Border { Height = 4, Width = 14, Background = Brush("#557799"), HorizontalAlignment = HorizontalAlignment.Left }); }
                else if (i == 1) { wfInner.Children.Add(new Ellipse { Width = 9, Height = 9, Fill = Brush("#6699BB"), HorizontalAlignment = HorizontalAlignment.Center }); }
                else if (i == 2) { wfInner.Children.Add(new Border { Height = 6, Background = Brushes.Transparent }); wfInner.Children.Add(new Border { Height = 1, Background = Brush("#888888") }); }
                else if (i == 3) { wfInner.Children.Add(new Border { Height = 2, Background = Brush("#333333"), Margin = new Thickness(0, 0, 0, 2) }); wfInner.Children.Add(new Border { Height = 6, Width = 12, Background = Brush("#B54433"), HorizontalAlignment = HorizontalAlignment.Left }); }
                else { wfInner.Children.Add(new Border { Height = 9, Background = Brush("#667788"), CornerRadius = new CornerRadius(1), Margin = new Thickness(0, 0, 0, 2) }); wfInner.Children.Add(new Border { Height = 1.5, Width = 10, Background = Brush("#888888"), HorizontalAlignment = HorizontalAlignment.Left }); }
                wf.Child = wfInner;
                sp.Children.Add(wf);
                var tl = Label(tplNames[i], 10, "Ink"); tl.HorizontalAlignment = HorizontalAlignment.Center;
                sp.Children.Add(tl);
                tc.Child = sp;
                tc.MouseLeftButtonUp += (s, e) => { selTpl = cap; syncTplCards(); if (refresh != null) refresh(); };
                tplCards.Add(tc);
                tplGrid.Children.Add(tc);
            }
            syncTplCards();

            // 4. Photo Picker Bar (shown cleanly when Polaroid template is selected)
            var photoBar = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 0, 0, 10), Visibility = Visibility.Collapsed };
            photoBar.SetResourceReference(Border.BackgroundProperty, "Field");
            var pbDock = new DockPanel(); photoBar.Child = pbDock;
            var clearPhotoBtn = ActionButton("清除", () => { customPhoto = null; if (refresh != null) refresh(); });
            clearPhotoBtn.Padding = new Thickness(7, 3, 7, 3); clearPhotoBtn.FontSize = 10; clearPhotoBtn.Background = Brushes.Transparent; clearPhotoBtn.BorderThickness = new Thickness(0);
            DockPanel.SetDock(clearPhotoBtn, Dock.Right); pbDock.Children.Add(clearPhotoBtn);
            var pickPhotoBtn = ActionButton("选择相纸照片…", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Title = "选择拍立得相纸照片", Filter = "图片文件|*.png;*.jpg;*.jpeg;*.webp;*.bmp" };
                if (dlg.ShowDialog(hostWin) == true) { customPhoto = dlg.FileName; if (refresh != null) refresh(); }
            });
            pickPhotoBtn.Padding = new Thickness(9, 4, 9, 4); pickPhotoBtn.FontSize = 10.5;
            DockPanel.SetDock(pickPhotoBtn, Dock.Left); pbDock.Children.Add(pickPhotoBtn);
            var photoHint = Label("或拖入左侧预览区", 10, "Muted"); photoHint.Margin = new Thickness(8, 0, 0, 0);
            pbDock.Children.Add(photoHint);
            form.Children.Add(photoBar);

            // 5. Aspect Ratio Segmented Pill (includes 9:19.5 Phone Lockscreen!)
            var ratioCap = Label("画布比例", 10.5, "Muted"); ratioCap.Margin = new Thickness(0, 0, 0, 5); form.Children.Add(ratioCap);
            form.Children.Add(makeSegmented(new[] { "3:4 竖版", "1:1 方卡", "16:9 横封", "手机锁屏" }, 0, idx => { selRatio = idx; if (refresh != null) refresh(); }));

            // 6. Tactile Color Swatch Dots (replaces 11-item skin dropdown!)
            var skinCapRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var skinActiveName = Label(ShareSkinChoices[0].Label, 10, "Accent");
            DockPanel.SetDock(skinActiveName, Dock.Right); skinCapRow.Children.Add(skinActiveName);
            skinCapRow.Children.Add(Label("色彩皮肤", 10.5, "Muted"));
            form.Children.Add(skinCapRow);

            var swatchWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 11) };
            form.Children.Add(swatchWrap);
            var swatchBorders = new List<Border>();
            Action syncSwatches = () =>
            {
                skinActiveName.Text = ShareSkinChoices[selSkin].Label;
                for (int i = 0; i < swatchBorders.Count; i++)
                {
                    bool on = i == selSkin;
                    swatchBorders[i].BorderThickness = new Thickness(on ? 2 : 1);
                    swatchBorders[i].SetResourceReference(Border.BorderBrushProperty, on ? "Accent" : "Line");
                }
            };
            for (int i = 0; i < ShareSkinChoices.Length; i++)
            {
                int cap = i;
                var sc = ShareSkinChoices[i];
                Brush fill;
                if (i == 0)
                {
                    fill = new LinearGradientBrush(C_("#FAFBFD"), C_("#35689E"), 45);
                }
                else
                {
                    var pal = PickSkinPalette(sc.SkinId, sc.Light);
                    fill = new LinearGradientBrush(C_(pal.Bg1), C_(pal.Accent), new Point(0, 0), new Point(1, 1));
                }
                var orb = new Border
                {
                    Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Padding = new Thickness(2),
                    Margin = new Thickness(0, 0, 6, 6), Cursor = Cursors.Hand, ToolTip = sc.Label,
                    Child = new Ellipse { Fill = fill }
                };
                orb.MouseLeftButtonUp += (s, e) => { selSkin = cap; syncSwatches(); if (refresh != null) refresh(); };
                swatchBorders.Add(orb);
                swatchWrap.Children.Add(orb);
            }
            syncSwatches();

            // 7. Quote Box + Curated Literary Inspiration Dice Button
            var quoteHead = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
            var quoteBox = new TextBox { Text = LiteraryQuotes[0], MaxLength = 60, Height = 34, Margin = new Thickness(0, 0, 0, 11) };
            var diceBtn = ActionButton("换一句 ✦", () => { quoteBox.Text = PickNextQuote(quoteBox.Text); });
            diceBtn.Padding = new Thickness(8, 2, 8, 2); diceBtn.FontSize = 10; diceBtn.Background = Brushes.Transparent; diceBtn.BorderThickness = new Thickness(0);
            diceBtn.SetResourceReference(Button.ForegroundProperty, "Accent");
            DockPanel.SetDock(diceBtn, Dock.Right); quoteHead.Children.Add(diceBtn);
            quoteHead.Children.Add(Label("卡片寄语", 10.5, "Muted"));
            form.Children.Add(quoteHead);
            form.Children.Add(quoteBox);

            // 8. Fine toggles (2×2 compact grid)
            var optGrid = new Grid { Margin = new Thickness(0, 2, 0, 4) };
            optGrid.ColumnDefinitions.Add(new ColumnDefinition());
            optGrid.ColumnDefinitions.Add(new ColumnDefinition());
            optGrid.RowDefinitions.Add(new RowDefinition());
            optGrid.RowDefinitions.Add(new RowDefinition());
            form.Children.Add(optGrid);

            var showDateChk = new CheckBox { Content = "目标日期", IsChecked = true, FontSize = 11, Margin = new Thickness(0, 0, 0, 8) };
            var showWmChk = new CheckBox { Content = "角标水印", IsChecked = true, FontSize = 11, Margin = new Thickness(0, 0, 0, 8) };
            var textureChk = new CheckBox { Content = "纸感肌理", IsChecked = true, FontSize = 11 };
            var roundChk = new CheckBox { Content = "圆角贴纸", IsChecked = false, FontSize = 11, ToolTip = "关闭时导出满幅直角海报，发朋友圈/小红书不产生透明黑角" };
            Grid.SetRow(showDateChk, 0); Grid.SetColumn(showDateChk, 0);
            Grid.SetRow(showWmChk, 0); Grid.SetColumn(showWmChk, 1);
            Grid.SetRow(textureChk, 1); Grid.SetColumn(textureChk, 0);
            Grid.SetRow(roundChk, 1); Grid.SetColumn(roundChk, 1);
            optGrid.Children.Add(showDateChk); optGrid.Children.Add(showWmChk);
            optGrid.Children.Add(textureChk); optGrid.Children.Add(roundChk);

            Func<CardOpts> readOpts = () => new CardOpts
            {
                IsList = isListMode,
                EntryIndex = Math.Max(0, dayPicker.SelectedIndex),
                TemplateIndex = selTpl,
                RatioIndex = selRatio,
                SkinIndex = selSkin,
                Quote = quoteBox.Text,
                ShowDate = showDateChk.IsChecked == true,
                ShowWatermark = showWmChk.IsChecked == true,
                PaperTexture = textureChk.IsChecked == true,
                RoundedCorners = roundChk.IsChecked == true,
                PhotoPath = customPhoto
            };

            refresh = () =>
            {
                dayCap.Visibility = isListMode ? Visibility.Collapsed : Visibility.Visible;
                dayPicker.Visibility = isListMode ? Visibility.Collapsed : Visibility.Visible;
                photoBar.Visibility = selTpl == 4 ? Visibility.Visible : Visibility.Collapsed;
                clearPhotoBtn.Visibility = string.IsNullOrEmpty(customPhoto) ? Visibility.Collapsed : Visibility.Visible;
                int w, h;
                previewFrame.Child = BuildShareCardVisual(entries, readOpts(), currentConfigSkin, out w, out h);
                statusHint.Text = "导出尺寸 " + (w * 2) + " × " + (h * 2) + " px  ·  " + (roundChk.IsChecked == true ? "透明圆角 PNG" : "满幅高清 PNG");
            };

            previewPane.Drop += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        customPhoto = files[0];
                        selTpl = 4;
                        syncTplCards();
                        refresh();
                    }
                }
            };

            dayPicker.SelectionChanged += (s, e) => refresh();
            foreach (var chk in new[] { showDateChk, showWmChk, textureChk, roundChk })
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
            var outer = CreateStudioChrome("社交卡片工坊", 940, 700, editorIsLight, Editor, true, out win, "社交卡片工坊", out rootFrame, out updateTheme);

            Action disposeTimers;
            var content = BuildShareAtelierView(entries, initialIndex, currentConfigSkin, win, out disposeTimers);
            var padWrap = new Border { Margin = new Thickness(20, 4, 20, 20), Child = content };
            outer.Children.Add(padWrap);

            win.KeyDown += (s, e) => { if (e.Key == Key.Escape) win.Close(); };
            win.Closed += (s, e) => { disposeTimers(); ShareWin = null; };
            ShareWin = win;
            win.Show();
            win.Activate();
        }
    }
}
