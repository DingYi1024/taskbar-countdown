using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;

namespace CountdownWidget
{
    public static partial class Program
    {
        static Window FlyoutWin;
        static DateTime _flyoutClosedAt = DateTime.MinValue;
        static bool _flyoutLockDismiss;

        static void ToggleFlyout(ResolvedItem preferredFocus = null)
        {
            if (FlyoutWin != null)
            {
                CloseFlyout();
                return;
            }
            if ((DateTime.UtcNow - _flyoutClosedAt).TotalMilliseconds < 260) return;
            OpenFlyout(preferredFocus);
        }

        static void CloseFlyout()
        {
            if (FlyoutWin == null) return;
            var fw = FlyoutWin;
            FlyoutWin = null;
            _flyoutClosedAt = DateTime.UtcNow;
            try { fw.Close(); } catch { }
        }

        static void OpenFlyout(ResolvedItem preferredFocus = null)
        {
            CloseFlyout();
            bool light = IsLight();
            var now = DateTime.Now;
            var allResolved = GetResolvedItems(now);

            ResolvedItem focused = preferredFocus;
            if (focused == null && !string.IsNullOrEmpty(Config.pinned_name))
                focused = allResolved.FirstOrDefault(x => string.Equals(x.Name, Config.pinned_name, StringComparison.Ordinal));
            if (focused == null)
                focused = allResolved.FirstOrDefault(x => !x.IsExpired && !x.IsCountup) ?? allResolved.FirstOrDefault();

            const double flyW = 376;
            double maxH = Math.Min(560, SystemParameters.WorkArea.Height - 80);

            var fly = new Window
            {
                Title = "拾光浮层",
                Width = flyW,
                MaxHeight = maxH,
                SizeToContent = SizeToContent.Height,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = false,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 12,
                UseLayoutRounding = true
            };

            WindowChrome.SetWindowChrome(fly, new WindowChrome
            {
                CaptionHeight = 0,
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(-1),
                ResizeBorderThickness = new Thickness(0),
                UseAeroCaptionButtons = false
            });

            Theme(fly, light);

            var outerSlide = new TranslateTransform(0, 12);
            var rootBorder = new Border
            {
                Padding = new Thickness(16, 15, 16, 14),
                BorderThickness = new Thickness(1),
                RenderTransform = outerSlide,
                Opacity = 0
            };
            rootBorder.SetResourceReference(Border.BackgroundProperty, "Canvas");
            rootBorder.SetResourceReference(Border.BorderBrushProperty, "Line");
            fly.Content = rootBorder;

            var mainStack = new StackPanel();
            rootBorder.Child = mainStack;

            // --- Header: Today solar + lunar + year progress ---
            var topHeader = new DockPanel { Margin = new Thickness(2, 0, 2, 12) };
            mainStack.Children.Add(topHeader);

            var openStudioBtn = ActionButton("工作台 ↗", () => { CloseFlyout(); OpenSettings(0); });
            openStudioBtn.Padding = new Thickness(10, 4, 10, 4);
            openStudioBtn.FontSize = 11;
            openStudioBtn.Background = Brushes.Transparent;
            DockPanel.SetDock(openStudioBtn, Dock.Right);
            topHeader.Children.Add(openStudioBtn);

            var dateHead = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var todaySolar = Label(now.ToString("M月d日") + " · " + WeekdayZhShort(now), 13.5, "Ink");
            todaySolar.FontWeight = FontWeights.SemiBold;
            dateHead.Children.Add(todaySolar);
            var todayLunar = Label(LunarHelper.SolarToFullLunar(now), 10.5, "Muted");
            todayLunar.Margin = new Thickness(0, 1, 0, 0);
            dateHead.Children.Add(todayLunar);
            topHeader.Children.Add(dateHead);

            // --- Day 0 Arrival Celebration Banner (if any item is today) ---
            var todayArrival = allResolved.FirstOrDefault(x => x.IsToday);
            if (todayArrival != null)
            {
                var banner = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(12, 9, 12, 9),
                    Margin = new Thickness(0, 0, 0, 10),
                    BorderThickness = new Thickness(1)
                };
                var goldBg = new LinearGradientBrush(
                    C_(light ? "#FFF8E8" : "#3A301C"),
                    C_(light ? "#FBF0D9" : "#2A2316"),
                    new Point(0, 0), new Point(1, 1));
                goldBg.Freeze();
                banner.Background = goldBg;
                banner.SetResourceReference(Border.BorderBrushProperty, "Gold");

                var bd = new DockPanel();
                banner.Child = bd;
                var celebShare = ActionButton("纪念卡", () =>
                {
                    var captured = todayArrival;
                    CloseFlyout();
                    OpenShareForResolved(captured);
                });
                celebShare.Padding = new Thickness(9, 4, 9, 4);
                celebShare.FontSize = 10.5;
                celebShare.SetResourceReference(Button.BackgroundProperty, "Gold");
                celebShare.Foreground = Brushes.White;
                celebShare.BorderThickness = new Thickness(0);
                DockPanel.SetDock(celebShare, Dock.Right);
                bd.Children.Add(celebShare);

                var celebLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var cTitle = new TextBlock { Text = "✦ 今日如约而至 · " + todayArrival.Name, FontSize = 12, FontWeight = FontWeights.SemiBold };
                cTitle.SetResourceReference(TextBlock.ForegroundProperty, "Gold");
                celebLeft.Children.Add(cTitle);
                celebLeft.Children.Add(Label("期待已久的时刻，终于成为今天。", 10, "Muted"));
                bd.Children.Add(celebLeft);
                mainStack.Children.Add(banner);
            }

            // --- Hero Focus Card Container ---
            var heroHost = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(15, 13, 15, 13),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 10)
            };
            heroHost.SetResourceReference(Border.BackgroundProperty, "Surface");
            heroHost.SetResourceReference(Border.BorderBrushProperty, "Line");
            mainStack.Children.Add(heroHost);

            TextBlock heroBigNum = null;
            TextBlock heroLiveClock = null;
            TextBlock heroSubMeta = null;
            ResolvedItem activeHero = focused;

            Action rebuildHero = () =>
            {
                heroBigNum = null;
                heroLiveClock = null;
                heroSubMeta = null;

                if (activeHero == null)
                {
                    var emptySp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 14) };
                    emptySp.Children.Add(Label("还没有期待的日子", 13, "Ink"));
                    var emSub = Label("点击下方「＋ 快速记一笔」添加第一个里程碑", 11, "Muted");
                    emSub.Margin = new Thickness(0, 4, 0, 0);
                    emptySp.Children.Add(emSub);
                    heroHost.Child = emptySp;
                    return;
                }

                var hStack = new StackPanel();
                heroHost.Child = hStack;

                // Top row: Icon + Title + Pin & Share buttons
                var hTop = new DockPanel();
                hStack.Children.Add(hTop);

                var btnRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(btnRow, Dock.Right);
                hTop.Children.Add(btnRow);

                bool isPinned = string.Equals(Config.pinned_name, activeHero.Name, StringComparison.Ordinal);
                var pinBtn = ActionButton(isPinned ? "已主显" : "设为主显", () =>
                {
                    Config.pinned_name = isPinned ? "" : activeHero.Name;
                    Persist(true, false);
                    CloseFlyout();
                    OpenFlyout(activeHero);
                });
                pinBtn.Padding = new Thickness(8, 3.5, 8, 3.5);
                pinBtn.FontSize = 10.5;
                pinBtn.Margin = new Thickness(0, 0, 6, 0);
                if (isPinned)
                {
                    pinBtn.SetResourceReference(Button.BackgroundProperty, "AccentSoft");
                    pinBtn.SetResourceReference(Button.ForegroundProperty, "Accent");
                    pinBtn.BorderThickness = new Thickness(0);
                }
                btnRow.Children.Add(pinBtn);

                var shareBtn = ActionButton("分享卡", () =>
                {
                    var cap = activeHero;
                    CloseFlyout();
                    OpenShareForResolved(cap);
                });
                shareBtn.Padding = new Thickness(8, 3.5, 8, 3.5);
                shareBtn.FontSize = 10.5;
                shareBtn.SetResourceReference(Button.BackgroundProperty, "Accent");
                shareBtn.Foreground = Brushes.White;
                shareBtn.BorderThickness = new Thickness(0);
                btnRow.Children.Add(shareBtn);

                var titleCluster = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var iconBadge = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(7), Margin = new Thickness(0, 0, 8, 0) };
                iconBadge.SetResourceReference(Border.BackgroundProperty, "AccentSoft");
                iconBadge.Child = BuildEventIcon(activeHero.ResolvedIcon, 13, (Brush)fly.Resources["Accent"], 1.45);
                titleCluster.Children.Add(iconBadge);

                var titleTxt = new TextBlock
                {
                    Text = activeHero.Name,
                    FontSize = 14.5,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 146
                };
                titleTxt.SetResourceReference(TextBlock.ForegroundProperty, "Ink");
                titleCluster.Children.Add(titleTxt);
                hTop.Children.Add(titleCluster);

                // Middle: Giant tabular countdown + live HH:MM:SS
                var metricRow = new DockPanel { Margin = new Thickness(0, 10, 0, 8) };
                hStack.Children.Add(metricRow);

                var rightClockStack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 5) };
                DockPanel.SetDock(rightClockStack, Dock.Right);
                metricRow.Children.Add(rightClockStack);

                heroLiveClock = new TextBlock
                {
                    FontFamily = WidgetMonoFont,
                    FontSize = 15,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                heroLiveClock.SetResourceReference(TextBlock.ForegroundProperty, "Ink");
                rightClockStack.Children.Add(heroLiveClock);

                heroSubMeta = new TextBlock
                {
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                heroSubMeta.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
                rightClockStack.Children.Add(heroSubMeta);

                var leftNum = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
                if (activeHero.IsCountup)
                {
                    var pre = Label("已第 ", 11, "Muted");
                    pre.VerticalAlignment = VerticalAlignment.Bottom;
                    pre.Margin = new Thickness(0, 0, 2, 7);
                    leftNum.Children.Add(pre);
                }
                heroBigNum = new TextBlock
                {
                    FontFamily = ResolveWidgetNumberFont(),
                    FontSize = activeHero.IsToday || activeHero.IsExpired ? 26 : 42,
                    FontWeight = FontWeights.Bold,
                    LineHeight = 42,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                heroBigNum.SetResourceReference(TextBlock.ForegroundProperty, activeHero.IsToday ? "Gold" : "Accent");
                leftNum.Children.Add(heroBigNum);

                if (!activeHero.IsToday && !activeHero.IsExpired)
                {
                    var unit = Label(activeHero.IsCountup ? "天" : "天后", 12, "Muted");
                    unit.VerticalAlignment = VerticalAlignment.Bottom;
                    unit.Margin = new Thickness(4, 0, 0, 6);
                    leftNum.Children.Add(unit);
                }
                metricRow.Children.Add(leftNum);

                // Progress bar + real cycle start/end labels
                var progBar = new Grid { Height = 4.5, Margin = new Thickness(0, 2, 0, 6) };
                var pTrack = new Border { CornerRadius = new CornerRadius(2.25) };
                pTrack.SetResourceReference(Border.BackgroundProperty, "Field");
                progBar.Children.Add(pTrack);
                double pct = Math.Max(0.03, Math.Min(1.0, activeHero.Progress)) * 100.0;
                var fillGrid = new Grid();
                fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(pct, GridUnitType.Star) });
                fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.01, 100.0 - pct), GridUnitType.Star) });
                var pFill = new Border { CornerRadius = new CornerRadius(2.25) };
                pFill.SetResourceReference(Border.BackgroundProperty, activeHero.IsToday ? "Gold" : "Accent");
                Grid.SetColumn(pFill, 0);
                fillGrid.Children.Add(pFill);
                progBar.Children.Add(fillGrid);
                hStack.Children.Add(progBar);

                var metaRow = new DockPanel();
                var pctLbl = Label(((int)Math.Round(activeHero.Progress * 100)) + "%", 10, "Accent");
                pctLbl.FontFamily = WidgetMonoFont;
                DockPanel.SetDock(pctLbl, Dock.Right);
                metaRow.Children.Add(pctLbl);

                string dateLine = activeHero.TargetDate.ToString("yyyy.MM.dd") + " · " + WeekdayZhShort(activeHero.TargetDate);
                if (!string.IsNullOrEmpty(activeHero.LunarDisplay)) dateLine += " · " + activeHero.LunarDisplay;
                if (!string.IsNullOrEmpty(activeHero.RepeatLabel)) dateLine += " · " + activeHero.RepeatLabel;
                metaRow.Children.Add(Label(dateLine, 10, "Muted"));
                hStack.Children.Add(metaRow);
            };

            Action tickHeroClock = () =>
            {
                if (activeHero == null || heroBigNum == null) return;
                var tNow = DateTime.Now;
                if (activeHero.IsCountup)
                {
                    TimeSpan sp = tNow > activeHero.TargetDate ? (tNow - activeHero.TargetDate) : TimeSpan.Zero;
                    heroBigNum.Text = activeHero.DaysValue.ToString(CultureInfo.InvariantCulture);
                    heroLiveClock.Text = sp.ToString(@"hh\:mm\:ss");
                    heroSubMeta.Text = "起点 " + activeHero.TargetDate.ToString("yyyy.MM.dd");
                }
                else if (activeHero.IsToday)
                {
                    heroBigNum.Text = "就是今天";
                    heroLiveClock.Text = activeHero.TargetDate > tNow ? (activeHero.TargetDate - tNow).ToString(@"hh\:mm\:ss") : "✦ 抵达时刻";
                    heroSubMeta.Text = "祝这一天温暖明朗";
                }
                else if (activeHero.IsExpired)
                {
                    heroBigNum.Text = "已抵达";
                    heroLiveClock.Text = "00:00:00";
                    heroSubMeta.Text = "可在工作台转为纪念日";
                }
                else
                {
                    TimeSpan rem = activeHero.TargetDate - tNow;
                    heroBigNum.Text = activeHero.DaysValue.ToString(CultureInfo.InvariantCulture);
                    heroLiveClock.Text = rem.ToString(@"hh\:mm\:ss");
                    int wks = activeHero.DaysValue / 7;
                    int rds = activeHero.DaysValue % 7;
                    heroSubMeta.Text = wks > 0 ? ("约 " + wks + " 周 " + rds + " 天") : ("仅剩 " + (int)rem.TotalHours + " 小时");
                }
            };

            // --- Compact Upcoming List ---
            var listScroll = new ScrollViewer
            {
                MaxHeight = 192,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 0, 10)
            };
            var listStack = new StackPanel();
            listScroll.Content = listStack;
            mainStack.Children.Add(listScroll);

            Action rebuildList = null;
            rebuildList = () =>
            {
                listStack.Children.Clear();
                foreach (var entry in allResolved)
                {
                    var captured = entry;
                    bool selected = activeHero != null && ReferenceEquals(activeHero.Source, entry.Source);

                    var card = new Border
                    {
                        CornerRadius = new CornerRadius(9),
                        Padding = new Thickness(10, 8, 10, 8),
                        Margin = new Thickness(0, 0, 0, 5),
                        BorderThickness = new Thickness(1),
                        Cursor = Cursors.Hand
                    };
                    card.SetResourceReference(Border.BackgroundProperty, selected ? "Surface" : "Field");
                    card.SetResourceReference(Border.BorderBrushProperty, selected ? "Accent" : "Line");

                    var row = new DockPanel();
                    card.Child = row;

                    string rightNum = entry.IsToday ? "今天" : entry.IsExpired ? "已抵达" : entry.IsCountup ? ("第 " + entry.DaysValue + " 天") : (entry.DaysValue + " 天");
                    var badge = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 2.5, 7, 2.5), VerticalAlignment = VerticalAlignment.Center };
                    badge.SetResourceReference(Border.BackgroundProperty, selected ? "AccentSoft" : "Surface");
                    var badgeTxt = new TextBlock { Text = rightNum, FontFamily = WidgetNumFont, FontSize = 11.5, FontWeight = FontWeights.SemiBold };
                    badgeTxt.SetResourceReference(TextBlock.ForegroundProperty, entry.IsToday ? "Gold" : (selected ? "Accent" : "Ink"));
                    badge.Child = badgeTxt;
                    DockPanel.SetDock(badge, Dock.Right);
                    row.Children.Add(badge);

                    var leftCluster = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    var ic = BuildEventIcon(entry.ResolvedIcon, 13, (Brush)fly.Resources[selected ? "Accent" : "Muted"], 1.35);
                    ic.Margin = new Thickness(0, 0, 8, 0);
                    leftCluster.Children.Add(ic);

                    var txtCol = new StackPanel();
                    var nm = new TextBlock { Text = entry.Name, FontSize = 12, FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 175 };
                    nm.SetResourceReference(TextBlock.ForegroundProperty, "Ink");
                    txtCol.Children.Add(nm);

                    string sub = entry.TargetDate.ToString("yyyy.MM.dd");
                    if (!string.IsNullOrEmpty(entry.RepeatLabel)) sub += " · " + entry.RepeatLabel;
                    else if (!string.IsNullOrEmpty(entry.LunarDisplay)) sub += " · " + entry.LunarDisplay;
                    txtCol.Children.Add(Label(sub, 9.5, "Muted"));
                    leftCluster.Children.Add(txtCol);
                    row.Children.Add(leftCluster);

                    card.MouseLeftButtonUp += (s, e) =>
                    {
                        activeHero = captured;
                        rebuildHero();
                        tickHeroClock();
                        rebuildList();
                    };
                    listStack.Children.Add(card);
                }
            };

            rebuildHero();
            tickHeroClock();
            rebuildList();

            // --- Inline Quick-Add Drawer (collapsed by default) ---
            var quickAddPanel = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(11, 10, 11, 10),
                Margin = new Thickness(0, 0, 0, 10),
                BorderThickness = new Thickness(1),
                Visibility = Visibility.Collapsed
            };
            quickAddPanel.SetResourceReference(Border.BackgroundProperty, "Surface");
            quickAddPanel.SetResourceReference(Border.BorderBrushProperty, "Accent");
            mainStack.Children.Add(quickAddPanel);

            var qaStack = new StackPanel();
            quickAddPanel.Child = qaStack;
            var qaName = new TextBox { Height = 30, Margin = new Thickness(0, 0, 0, 8), Text = "" };
            qaName.ToolTip = "输入期待的日子名称";
            qaStack.Children.Add(qaName);

            DateTime qaTarget = DateTime.Today.AddDays(30);
            bool qaIsLunar = false;
            int qaLM = 0, qaLD = 0;
            bool qaLeap = false;

            var qaChipRow = new DockPanel();
            qaStack.Children.Add(qaChipRow);

            var qaConfirm = ActionButton("添加", () => { });
            qaConfirm.SetResourceReference(Button.BackgroundProperty, "Accent");
            qaConfirm.Foreground = Brushes.White;
            qaConfirm.BorderThickness = new Thickness(0);
            qaConfirm.Padding = new Thickness(11, 4, 11, 4);
            qaConfirm.FontSize = 11;
            DockPanel.SetDock(qaConfirm, Dock.Right);
            qaChipRow.Children.Add(qaConfirm);

            var qaDateBtn = ActionButton(qaTarget.ToString("yyyy-MM-dd") + " ▾", () => { });
            qaDateBtn.Padding = new Thickness(8, 4, 8, 4);
            qaDateBtn.FontSize = 10.5;
            qaDateBtn.Margin = new Thickness(0, 0, 6, 0);
            DockPanel.SetDock(qaDateBtn, Dock.Left);
            qaChipRow.Children.Add(qaDateBtn);

            var chips = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            foreach (int daysAdd in new[] { 7, 30, 100 })
            {
                int dAdd = daysAdd;
                var ch = ActionButton("+" + dAdd + "天", () =>
                {
                    qaIsLunar = false;
                    qaTarget = DateTime.Today.AddDays(dAdd);
                    qaDateBtn.Content = qaTarget.ToString("yyyy-MM-dd") + " ▾";
                });
                ch.Padding = new Thickness(6, 3.5, 6, 3.5);
                ch.FontSize = 10;
                ch.Margin = new Thickness(0, 0, 4, 0);
                chips.Children.Add(ch);
            }
            qaChipRow.Children.Add(chips);

            qaDateBtn.Click += (s, e) =>
            {
                _flyoutLockDismiss = true;
                var tempItem = new CfgItem { date = qaTarget.ToString("yyyy-MM-dd 00:00:00"), calendar = qaIsLunar ? "lunar" : "solar", lunar_month = qaLM, lunar_day = qaLD, lunar_leap = qaLeap };
                OpenCalendarPicker(qaDateBtn, light, tempItem, (dateStr, isLun, lm, ld, lp) =>
                {
                    var pDate = ParseDate(dateStr);
                    if (pDate.HasValue) qaTarget = pDate.Value;
                    qaIsLunar = isLun; qaLM = lm; qaLD = ld; qaLeap = lp;
                    qaDateBtn.Content = (isLun ? LunarHelper.FormatLunarMonthDay(lm, ld, lp) : qaTarget.ToString("yyyy-MM-dd")) + " ▾";
                }, () => { _flyoutLockDismiss = false; if (FlyoutWin != null) FlyoutWin.Activate(); });
            };

            Action submitQuickAdd = () =>
            {
                string nm = (qaName.Text ?? "").Trim();
                if (string.IsNullOrEmpty(nm)) { qaName.Focus(); return; }
                var newItem = new CfgItem
                {
                    name = nm,
                    date = qaTarget.ToString("yyyy-MM-dd HH:mm:ss"),
                    enabled = true,
                    kind = qaTarget.Date < DateTime.Today ? "countup" : "countdown",
                    repeat = qaIsLunar ? "yearly" : "none",
                    calendar = qaIsLunar ? "lunar" : "solar",
                    lunar_month = qaLM,
                    lunar_day = qaLD,
                    lunar_leap = qaLeap,
                    start_date = DateTime.Today.ToString("yyyy-MM-dd"),
                    icon = "auto"
                };
                Config.items.Add(newItem);
                Persist(false, true);
                CloseFlyout();
                OpenFlyout(ResolveItem(newItem, DateTime.Now));
            };
            qaConfirm.Click += (s, e) => submitQuickAdd();
            qaName.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; submitQuickAdd(); } };

            // --- Bottom Footer: Quick Add toggle + Year progress + Quick Skin Dots ---
            var footSep = new Border { Height = 1, Margin = new Thickness(0, 0, 0, 10) };
            footSep.SetResourceReference(Border.BackgroundProperty, "Line");
            mainStack.Children.Add(footSep);

            var bottomRow = new DockPanel();
            mainStack.Children.Add(bottomRow);

            var quickAddBtn = ActionButton("＋ 记一笔", () =>
            {
                bool show = quickAddPanel.Visibility != Visibility.Visible;
                quickAddPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                if (show) { qaName.Text = ""; qaName.Focus(); }
            });
            quickAddBtn.Padding = new Thickness(10, 5, 10, 5);
            quickAddBtn.FontSize = 11;
            DockPanel.SetDock(quickAddBtn, Dock.Left);
            bottomRow.Children.Add(quickAddBtn);

            // Quick skin swatch dots on bottom right
            var swatchBar = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(swatchBar, Dock.Right);
            bottomRow.Children.Add(swatchBar);

            string[] quickSkins = { "default", "minimal", "xuanpaper", "vinyl", "celadon", "spring", "christmas" };
            foreach (string sid in quickSkins)
            {
                string capSid = sid;
                var spec = FindSkin(capSid);
                var pal = light ? spec.LightPal : spec.DarkPal;
                bool active = string.Equals(Config.skin, capSid, StringComparison.OrdinalIgnoreCase);

                var dotWrap = new Border
                {
                    Width = 20, Height = 20, CornerRadius = new CornerRadius(10),
                    Margin = new Thickness(3, 0, 0, 0),
                    BorderThickness = new Thickness(active ? 1.8 : 1),
                    Padding = new Thickness(2),
                    Cursor = Cursors.Hand,
                    ToolTip = spec.Label
                };
                dotWrap.SetResourceReference(Border.BorderBrushProperty, active ? "Accent" : "Line");
                var innerDot = new Ellipse
                {
                    Fill = capSid == "minimal"
                        ? (Brush)new LinearGradientBrush(Colors.White, C_("#B8C4D4"), 45)
                        : (Brush)new LinearGradientBrush(C_(pal.Bg1), C_(pal.Accent), new Point(0, 0), new Point(1, 1))
                };
                dotWrap.Child = innerDot;
                dotWrap.MouseLeftButtonUp += (s, e) =>
                {
                    Config.skin = capSid;
                    Persist(false, true);
                    CloseFlyout();
                    OpenFlyout(activeHero);
                };
                swatchBar.Children.Add(dotWrap);
            }

            // Live 1-second timer for Hero clock
            var clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            clockTimer.Tick += (s, e) => tickHeroClock();

            fly.SourceInitialized += (s, e) =>
            {
                IntPtr h = new WindowInteropHelper(fly).Handle;
                bool acrylicOk = ApplyWin11Backdrop(fly, h, light, 3 /* Transient Acrylic */);
                SetPalette(fly, light, acrylicOk);
                if (!acrylicOk) fly.SetResourceReference(Window.BackgroundProperty, "Canvas");
                else fly.Background = Brushes.Transparent;
                AddWindowExStyle(h, 0x80 /* WS_EX_TOOLWINDOW */);
            };

            fly.Loaded += (s, e) =>
            {
                PositionFlyout(fly);
                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                var slide = new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(210)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                rootBorder.BeginAnimation(UIElement.OpacityProperty, fade);
                outerSlide.BeginAnimation(TranslateTransform.YProperty, slide);
                clockTimer.Start();
            };

            fly.SizeChanged += (s, e) => PositionFlyout(fly);
            fly.KeyDown += (s, e) => { if (e.Key == Key.Escape) CloseFlyout(); };
            fly.Deactivated += (s, e) => { if (!_flyoutLockDismiss) CloseFlyout(); };
            fly.Closed += (s, e) =>
            {
                clockTimer.Stop();
                if (ReferenceEquals(FlyoutWin, fly))
                {
                    FlyoutWin = null;
                    _flyoutClosedAt = DateTime.UtcNow;
                }
            };

            FlyoutWin = fly;
            fly.Show();
            fly.Activate();
        }

        static void PositionFlyout(Window fly)
        {
            if (fly == null || W == null) return;
            var work = SystemParameters.WorkArea;
            double left = W.Left + (W.ActualWidth - fly.ActualWidth) / 2.0;
            left = Math.Max(work.Left + 10, Math.Min(work.Right - fly.ActualWidth - 10, left));
            double top = W.Top - fly.ActualHeight - 10;
            if (top < work.Top + 10) top = work.Top + 10;
            fly.Left = left;
            fly.Top = top;
        }

        static void OpenShareForResolved(ResolvedItem target)
        {
            var now = DateTime.Now;
            var all = GetResolvedItems(now);
            int idx = 0;
            var entries = new List<ShareEntry>();
            for (int i = 0; i < all.Count; i++)
            {
                var r = all[i];
                if (target != null && ReferenceEquals(r.Source, target.Source)) idx = i;
                entries.Add(ToShareEntry(r));
            }
            if (entries.Count == 0) return;
            OpenShareCard(entries, idx, Config.skin, IsLight());
        }
    }
}
