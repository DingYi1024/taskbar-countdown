using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CountdownWidget
{
    public static partial class Program
    {
        static ResourceDictionary _themeDict;
        static ImageSource _logo;

        static void Theme(FrameworkElement view, bool light)
        {
            if (_themeDict == null)
            {
                using (var stream = typeof(Program).Assembly.GetManifestResourceStream("Theme.xaml"))
                    _themeDict = (ResourceDictionary)XamlReader.Load(stream);
            }
            if (!view.Resources.MergedDictionaries.Contains(_themeDict))
                view.Resources.MergedDictionaries.Add(_themeDict);
            SetPalette(view, light, false);
        }

        static TextBlock Label(string text, double size, string color)
        {
            var t = new TextBlock { Text = text, FontSize = size, VerticalAlignment = VerticalAlignment.Center };
            t.SetResourceReference(TextBlock.ForegroundProperty, color);
            return t;
        }

        static Button ActionButton(string text, Action action)
        {
            var b = new Button { Content = text };
            b.Click += (s, e) => action();
            return b;
        }

        static ImageSource Logo()
        {
            if (_logo == null)
            {
                using (var stream = typeof(Program).Assembly.GetManifestResourceStream("logo.png"))
                {
                    var image = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    image.Freeze();
                    _logo = image;
                }
            }
            return _logo;
        }

        static string NextSolarDate(int month, int day)
        {
            var today = DateTime.Today;
            var d = new DateTime(today.Year, month, day);
            if (d < today) d = new DateTime(today.Year + 1, month, day);
            return d.ToString("yyyy-MM-dd");
        }

        static DateTime NextFridayEvening()
        {
            var now = DateTime.Now;
            int diff = ((int)DayOfWeek.Friday - (int)now.DayOfWeek + 7) % 7;
            var target = now.Date.AddDays(diff).AddHours(18);
            if (target <= now) target = target.AddDays(7);
            return target;
        }

        static void OpenSettings(int initialTab = 0)
        {
            if (Editor != null)
            {
                if (Editor.WindowState == WindowState.Minimized) Editor.WindowState = WindowState.Normal;
                Editor.Activate();
                return;
            }

            bool curLight = IsLight();
            Window editor;
            Border rootFrame;
            Action<bool> updateWinTheme;
            var outer = CreateStudioChrome("拾光工作台 · 任务栏倒计时", 960, 680, curLight, null, true, out editor, "拾光倒计时 · Studio 2.0", out rootFrame, out updateWinTheme);
            Editor = editor;

            Action disposeAtelier = null;
            editor.Closed += (s, e) =>
            {
                if (disposeAtelier != null) { try { disposeAtelier(); } catch { } disposeAtelier = null; }
                Editor = null;
            };

            var workspace = new Grid { Margin = new Thickness(16, 4, 16, 16) };
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(192) });
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outer.Children.Add(workspace);

            // --- Left Sidebar ---
            var sidebar = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 14, 12, 12), BorderThickness = new Thickness(1) };
            sidebar.SetResourceReference(Border.BackgroundProperty, "Surface");
            sidebar.SetResourceReference(Border.BorderBrushProperty, "Line");
            Grid.SetColumn(sidebar, 0);
            workspace.Children.Add(sidebar);

            var sideDock = new DockPanel();
            sidebar.Child = sideDock;

            // Live sync indicator at bottom of sidebar
            var syncBadge = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 10, 0, 0) };
            syncBadge.SetResourceReference(Border.BackgroundProperty, "Field");
            var syncRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var syncDot = new Ellipse { Width = 7, Height = 7, Margin = new Thickness(0, 0, 7, 0) };
            syncDot.SetResourceReference(Shape.FillProperty, "Accent");
            var syncTxt = Label("所有修改实时同步", 10.5, "Muted");
            syncRow.Children.Add(syncDot);
            syncRow.Children.Add(syncTxt);
            syncBadge.Child = syncRow;
            DockPanel.SetDock(syncBadge, Dock.Bottom);
            sideDock.Children.Add(syncBadge);

            var syncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1400) };
            syncTimer.Tick += (s, e) => { syncTimer.Stop(); syncTxt.Text = "所有修改实时同步"; };

            var navStack = new StackPanel();
            sideDock.Children.Add(navStack);

            var brandHead = new StackPanel { Margin = new Thickness(6, 2, 6, 16) };
            var bTitle = Label("拾光工作台", 16, "Ink"); bTitle.FontWeight = FontWeights.SemiBold;
            brandHead.Children.Add(bTitle);
            var bSub = Label("把期待留在眼前", 10.5, "Muted"); bSub.Margin = new Thickness(0, 2, 0, 0);
            brandHead.Children.Add(bSub);
            navStack.Children.Add(brandHead);

            // Right content host
            var contentHost = new Grid();
            Grid.SetColumn(contentHost, 2);
            workspace.Children.Add(contentHost);

            int activeTab = Math.Max(0, Math.Min(3, initialTab));
            var navButtons = new List<Border>();
            Action refreshCurrentTab = null;

            // Bounded Ctrl+Z undo stack for moment list operations
            var undoStack = new List<string>();
            Action pushUndo = () =>
            {
                try
                {
                    string snap = Json.Serialize(new
                    {
                        items = Config.items ?? new List<CfgItem>(),
                        pinned_name = Config.pinned_name,
                        sort_mode = Config.sort_mode ?? "smart"
                    });
                    if (undoStack.Count > 0 && undoStack[undoStack.Count - 1] == snap) return;
                    undoStack.Add(snap);
                    if (undoStack.Count > 30) undoStack.RemoveAt(0);
                }
                catch { }
            };

            Action<bool> liveCommit = fullRebuild =>
            {
                try
                {
                    SaveConfig(Config, false, fullRebuild);
                    SyncTrayIcon();
                    bool nextLight = IsLight();
                    updateWinTheme(nextLight);
                    syncTxt.Text = "✓ 已同步至任务栏";
                    syncTimer.Stop();
                    syncTimer.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(editor, "保存失败：" + ex.Message, "拾光工作台");
                }
            };

            Action performUndo = () =>
            {
                if (undoStack.Count == 0) return;
                try
                {
                    string snap = undoStack[undoStack.Count - 1];
                    undoStack.RemoveAt(undoStack.Count - 1);
                    var restored = Json.Deserialize<Cfg>(snap);
                    if (restored != null)
                    {
                        Config.items = restored.items ?? new List<CfgItem>();
                        Config.pinned_name = restored.pinned_name;
                        Config.sort_mode = string.IsNullOrEmpty(restored.sort_mode) ? "smart" : restored.sort_mode;
                        liveCommit(false);
                        syncTxt.Text = "↶ 已撤销上一步";
                        syncTimer.Stop();
                        syncTimer.Start();
                        if (refreshCurrentTab != null && activeTab == 0) refreshCurrentTab();
                    }
                }
                catch { }
            };

            string[] navTitles = { "时光清单", "外观画廊", "社交工坊", "系统偏好" };
            string[] navIcons = {
                "M3,4.5 L13,4.5 M3,8 L13,8 M3,11.5 L9.5,11.5",
                "M8,2.2 A5.8,5.8 0 1,0 13.8,8 C13.8,6.7 12.6,6 11.4,6 L10.2,6 C9.3,6 8.8,6.6 8.8,7.3 C8.8,8.2 9.4,8.8 9.4,9.6 C9.4,10.5 8.6,11.2 7.6,11.2",
                "M2.5,3.8 C2.5,3.0 3.1,2.4 3.9,2.4 L12.1,2.4 C12.9,2.4 13.5,3.0 13.5,3.8 L13.5,12.2 C13.5,13.0 12.9,13.6 12.1,13.6 L3.9,13.6 C3.1,13.6 2.5,13.0 2.5,12.2 Z M4.2,10.8 L6.8,7.8 L9.0,10.0 L10.8,8.1 L12.2,10.8",
                "M2.5,5 L13.5,5 M2.5,11 L13.5,11 M6,3.2 L6,6.8 M10.5,9.2 L10.5,12.8"
            };

            Action syncNavHighlight = () =>
            {
                for (int i = 0; i < navButtons.Count; i++)
                {
                    bool on = i == activeTab;
                    if (on) navButtons[i].SetResourceReference(Border.BackgroundProperty, "AccentSoft");
                    else navButtons[i].Background = Brushes.Transparent;
                    var sp = (StackPanel)navButtons[i].Child;
                    var txt = (TextBlock)sp.Children[1];
                    txt.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
                    txt.SetResourceReference(TextBlock.ForegroundProperty, on ? "Accent" : "Ink");
                }
            };

            for (int i = 0; i < navTitles.Length; i++)
            {
                int cap = i;
                var itemBorder = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 9, 10, 9),
                    Margin = new Thickness(0, 0, 0, 4),
                    Cursor = Cursors.Hand
                };
                var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var ic = BuildVectorIcon(navIcons[i], 14, "Muted", 1.45);
                ic.Margin = new Thickness(0, 0, 10, 0);
                sp.Children.Add(ic);
                sp.Children.Add(Label(navTitles[i], 12.5, "Ink"));
                itemBorder.Child = sp;

                itemBorder.MouseEnter += (s, e) => { if (activeTab != cap) itemBorder.SetResourceReference(Border.BackgroundProperty, "Field"); };
                itemBorder.MouseLeave += (s, e) => { if (activeTab != cap) itemBorder.Background = Brushes.Transparent; };
                itemBorder.MouseLeftButtonUp += (s, e) =>
                {
                    if (activeTab == cap) return;
                    activeTab = cap;
                    syncNavHighlight();
                    if (refreshCurrentTab != null) refreshCurrentTab();
                };
                navButtons.Add(itemBorder);
                navStack.Children.Add(itemBorder);
            }

            // Helper to build segmented pill bar inside Studio
            Func<string[], int, Action<int>, Border> makeStudioSegmented = (labels, initIdx, onSel) =>
            {
                var wrap = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(3) };
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
                    var b = ActionButton(labels[i], () => { hi(cap); onSel(cap); });
                    b.BorderThickness = new Thickness(0);
                    b.Padding = new Thickness(10, 6, 10, 6);
                    b.FontSize = 11.5;
                    Grid.SetColumn(b, i);
                    g.Children.Add(b);
                    btns.Add(b);
                }
                hi(initIdx);
                return wrap;
            };

            // =====================================================================
            // TAB 0: 时光清单 (Moments Card Manager with Solar+Lunar Calendar Picker)
            // =====================================================================
            Func<FrameworkElement> buildMomentsTab = () =>
            {
                var dock = new DockPanel();

                // Header Row
                var head = new DockPanel { Margin = new Thickness(2, 2, 2, 14) };
                DockPanel.SetDock(head, Dock.Top);
                dock.Children.Add(head);

                var actRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(actRow, Dock.Right);
                head.Children.Add(actRow);

                var undoBtn = ActionButton("↶ 撤销", () => performUndo());
                undoBtn.Margin = new Thickness(0, 0, 8, 0);
                undoBtn.Padding = new Thickness(10, 6, 10, 6);
                undoBtn.FontSize = 11.5;
                undoBtn.ToolTip = "撤销上一步修改 (Ctrl+Z)";
                actRow.Children.Add(undoBtn);

                var sortBtn = ActionButton("", () => { });
                sortBtn.Margin = new Thickness(0, 0, 8, 0);
                sortBtn.Padding = new Thickness(10, 6, 10, 6);
                sortBtn.FontSize = 11.5;
                sortBtn.ToolTip = "点击切换「智能按临近排序」或「按下方拖拽自定义顺序」";
                actRow.Children.Add(sortBtn);

                var presetBtn = ActionButton("智能节日与场景预设 ▾", () => { });
                presetBtn.Margin = new Thickness(0, 0, 8, 0);
                actRow.Children.Add(presetBtn);

                var addBtn = ActionButton("＋ 添加日子", () => { });
                addBtn.SetResourceReference(Button.BackgroundProperty, "Accent");
                addBtn.Foreground = Brushes.White;
                addBtn.BorderThickness = new Thickness(0);
                actRow.Children.Add(addBtn);

                var headText = new StackPanel();
                var hTitle = Label("时光清单", 20, "Ink"); hTitle.FontWeight = FontWeights.SemiBold;
                headText.Children.Add(hTitle);
                var hSub = Label("", 11.5, "Muted"); hSub.Margin = new Thickness(0, 3, 0, 0);
                headText.Children.Add(hSub);
                head.Children.Add(headText);

                var listStack = new StackPanel { Margin = new Thickness(0, 0, 8, 12) };
                var scroller = new ScrollViewer { Content = listStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
                dock.Children.Add(scroller);

                Action renderCards = null;
                sortBtn.Click += (s, e) =>
                {
                    pushUndo();
                    Config.sort_mode = string.Equals(Config.sort_mode, "manual", StringComparison.OrdinalIgnoreCase) ? "smart" : "manual";
                    liveCommit(false);
                    if (renderCards != null) renderCards();
                };

                renderCards = () =>
                {
                    listStack.Children.Clear();
                    var now = DateTime.Now;
                    int enabledCount = Config.items.Count(x => x != null && x.enabled && !x.archived);
                    bool isManual = string.Equals(Config.sort_mode, "manual", StringComparison.OrdinalIgnoreCase);
                    undoBtn.Visibility = undoStack.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                    sortBtn.Content = isManual ? "排序：自定义顺序" : "排序：智能临近";
                    hSub.Text = "共 " + Config.items.Count + " 个日子 · " + enabledCount + " 个在任务栏展示 · 左侧 ⋮⋮ 可拖拽排序 · Ctrl+Z 撤销";

                    if (Config.items.Count == 0)
                    {
                        var emptyCard = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(28), BorderThickness = new Thickness(1) };
                        emptyCard.SetResourceReference(Border.BackgroundProperty, "Surface");
                        emptyCard.SetResourceReference(Border.BorderBrushProperty, "Line");
                        var esp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                        esp.Children.Add(Label("清单还是空的", 15, "Ink"));
                        var esub = Label("点击右上角「＋ 添加日子」或从「智能节日预设」一键导入", 11.5, "Muted");
                        esub.Margin = new Thickness(0, 6, 0, 0);
                        esp.Children.Add(esub);
                        emptyCard.Child = esp;
                        listStack.Children.Add(emptyCard);
                        return;
                    }

                    for (int idx = 0; idx < Config.items.Count; idx++)
                    {
                        var item = Config.items[idx];
                        if (item == null) continue;
                        int capturedIdx = idx;
                        var resolved = ResolveItem(item, now);

                        var card = new Border
                        {
                            CornerRadius = new CornerRadius(11),
                            Padding = new Thickness(10, 11, 14, 11),
                            Margin = new Thickness(0, 0, 0, 9),
                            BorderThickness = new Thickness(1),
                            AllowDrop = true
                        };
                        card.SetResourceReference(Border.BackgroundProperty, "Surface");
                        card.SetResourceReference(Border.BorderBrushProperty, "Line");

                        card.DragOver += (s, e) =>
                        {
                            if (e.Data.GetDataPresent("TaskbarCountdownItemIdx"))
                            {
                                e.Effects = DragDropEffects.Move;
                                card.SetResourceReference(Border.BorderBrushProperty, "Accent");
                                e.Handled = true;
                            }
                            else e.Effects = DragDropEffects.None;
                        };
                        card.DragLeave += (s, e) =>
                        {
                            card.SetResourceReference(Border.BorderBrushProperty, "Line");
                        };
                        card.Drop += (s, e) =>
                        {
                            card.SetResourceReference(Border.BorderBrushProperty, "Line");
                            if (!e.Data.GetDataPresent("TaskbarCountdownItemIdx")) return;
                            int fromIdx = (int)e.Data.GetData("TaskbarCountdownItemIdx");
                            int toIdx = capturedIdx;
                            if (fromIdx == toIdx || fromIdx < 0 || fromIdx >= Config.items.Count || toIdx < 0 || toIdx >= Config.items.Count) return;
                            pushUndo();
                            var moving = Config.items[fromIdx];
                            Config.items.RemoveAt(fromIdx);
                            Config.items.Insert(toIdx, moving);
                            Config.sort_mode = "manual";
                            liveCommit(false);
                            renderCards();
                            e.Handled = true;
                        };

                        var cardStack = new StackPanel();
                        card.Child = cardStack;

                        // Row 1: Drag Grip | Toggle | Icon | Name TextBox | Date Picker Button | Days Badge | Actions
                        var topRow = new Grid();
                        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });   // Drag grip
                        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });   // Toggle
                        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });   // Icon
                        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name
                        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(168) });  // Date picker btn
                        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });   // Status pill
                        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(116) });  // Share + Delete
                        cardStack.Children.Add(topRow);

                        // Tactile 6-dot drag grip handle
                        var grip = new Border { Background = Brushes.Transparent, Cursor = Cursors.SizeAll, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(2, 6, 4, 6), ToolTip = "按住拖动调整卡片顺序" };
                        var gripTxt = Label("⋮⋮", 12, "Muted");
                        gripTxt.Opacity = 0.65;
                        grip.Child = gripTxt;
                        Point dragStart = new Point();
                        bool dragArmed = false;
                        grip.PreviewMouseLeftButtonDown += (s, e) => { dragStart = e.GetPosition(null); dragArmed = true; };
                        grip.PreviewMouseMove += (s, e) =>
                        {
                            if (!dragArmed || e.LeftButton != MouseButtonState.Pressed) return;
                            Vector diff = dragStart - e.GetPosition(null);
                            if (Math.Abs(diff.X) > 3 || Math.Abs(diff.Y) > 3)
                            {
                                dragArmed = false;
                                DragDrop.DoDragDrop(card, new DataObject("TaskbarCountdownItemIdx", capturedIdx), DragDropEffects.Move);
                            }
                        };
                        grip.PreviewMouseLeftButtonUp += (s, e) => { dragArmed = false; };
                        Grid.SetColumn(grip, 0); topRow.Children.Add(grip);

                        var enChk = new CheckBox { IsChecked = item.enabled, ToolTip = "在任务栏展示" };
                        enChk.Checked += (s, e) => { pushUndo(); item.enabled = true; liveCommit(false); renderCards(); };
                        enChk.Unchecked += (s, e) => { pushUndo(); item.enabled = false; liveCommit(false); renderCards(); };
                        Grid.SetColumn(enChk, 1); topRow.Children.Add(enChk);

                        // Clickable category icon badge (cycles icon!)
                        string curIcon = resolved != null ? resolved.ResolvedIcon : "star";
                        var iconBtn = new Border
                        {
                            Width = 28, Height = 28, CornerRadius = new CornerRadius(8),
                            Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            ToolTip = "点击切换分类图标（当前：" + EventIconLabel(item.icon) + "）"
                        };
                        iconBtn.SetResourceReference(Border.BackgroundProperty, "AccentSoft");
                        iconBtn.Child = BuildEventIcon(curIcon, 14, (Brush)editor.Resources["Accent"], 1.45);
                        iconBtn.MouseLeftButtonUp += (s, e) =>
                        {
                            pushUndo();
                            int curIdx = Array.IndexOf(EventIconIds, item.icon ?? "auto");
                            item.icon = EventIconIds[(curIdx + 1) % EventIconIds.Length];
                            liveCommit(false);
                            renderCards();
                        };
                        Grid.SetColumn(iconBtn, 2); topRow.Children.Add(iconBtn);

                        var nameBox = new TextBox { Text = item.name, MaxLength = 40, Margin = new Thickness(0, 0, 10, 0), FontSize = 13, FontWeight = FontWeights.Medium };
                        nameBox.LostFocus += (s, e) =>
                        {
                            string trimmed = (nameBox.Text ?? "").Trim();
                            if (!string.IsNullOrEmpty(trimmed) && trimmed != item.name)
                            {
                                pushUndo();
                                item.name = trimmed;
                                liveCommit(false);
                                renderCards();
                            }
                        };
                        nameBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) Keyboard.ClearFocus(); };
                        Grid.SetColumn(nameBox, 3); topRow.Children.Add(nameBox);

                        // Calendar Date Picker Button (NO manual typing!)
                        string dateBtnLabel = (resolved != null && resolved.IsLunar)
                            ? (resolved.LunarDisplay + " ▾")
                            : ((resolved != null ? resolved.TargetDate.ToString("yyyy-MM-dd") : item.date) + " ▾");
                        var dateBtn = ActionButton(dateBtnLabel, () => { });
                        dateBtn.Padding = new Thickness(10, 6, 10, 6);
                        dateBtn.Margin = new Thickness(0, 0, 8, 0);
                        dateBtn.FontSize = 11.5;
                        dateBtn.FontFamily = WidgetMonoFont;
                        dateBtn.ToolTip = "点击打开公历 / 农历可视化日历选择器";
                        dateBtn.Click += (s, e) =>
                        {
                            OpenCalendarPicker(dateBtn, IsLight(), item, (dateStr, isLun, lm, ld, lp) =>
                            {
                                pushUndo();
                                item.date = dateStr;
                                item.calendar = isLun ? "lunar" : "solar";
                                item.lunar_month = lm;
                                item.lunar_day = ld;
                                item.lunar_leap = lp;
                                if (isLun && (string.IsNullOrEmpty(item.repeat) || item.repeat == "none")) item.repeat = "yearly";
                                liveCommit(false);
                                renderCards();
                            });
                        };
                        Grid.SetColumn(dateBtn, 4); topRow.Children.Add(dateBtn);

                        // Remaining / Elapsed Pill
                        string statTxt = "日期无效";
                        if (resolved != null)
                        {
                            statTxt = resolved.IsToday ? "✦ 今天"
                                : resolved.IsExpired ? "已抵达"
                                : resolved.IsCountup ? ("第 " + resolved.DaysValue + " 天")
                                : (resolved.DaysValue + " 天后");
                        }
                        var statPill = new Border { CornerRadius = new CornerRadius(7), Padding = new Thickness(8, 4, 8, 4), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                        statPill.SetResourceReference(Border.BackgroundProperty, resolved != null && resolved.IsToday ? "AccentSoft" : "Field");
                        var statLbl = new TextBlock { Text = statTxt, FontSize = 11.5, FontWeight = FontWeights.SemiBold, FontFamily = WidgetNumFont };
                        statLbl.SetResourceReference(TextBlock.ForegroundProperty, resolved == null ? "Danger" : (resolved.IsToday ? "Gold" : "Accent"));
                        statPill.Child = statLbl;
                        Grid.SetColumn(statPill, 5); topRow.Children.Add(statPill);

                        // Right Action Buttons: Share | Delete
                        var actCol = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                        var shareBtn = ActionButton("卡片", () =>
                        {
                            if (resolved != null) OpenShareForResolved(resolved);
                        });
                        shareBtn.Padding = new Thickness(8, 5, 8, 5);
                        shareBtn.Background = Brushes.Transparent;
                        shareBtn.BorderThickness = new Thickness(0);
                        shareBtn.SetResourceReference(Button.ForegroundProperty, "Accent");
                        shareBtn.FontSize = 11.5;
                        actCol.Children.Add(shareBtn);

                        var delBtn = ActionButton("移除", () =>
                        {
                            pushUndo();
                            Config.items.RemoveAt(capturedIdx);
                            liveCommit(false);
                            renderCards();
                        });
                        delBtn.Padding = new Thickness(8, 5, 8, 5);
                        delBtn.Background = Brushes.Transparent;
                        delBtn.BorderThickness = new Thickness(0);
                        delBtn.SetResourceReference(Button.ForegroundProperty, "Muted");
                        delBtn.FontSize = 11.5;
                        actCol.Children.Add(delBtn);

                        Grid.SetColumn(actCol, 6); topRow.Children.Add(actCol);

                        // Row 2: Rich metadata pills (Kind: 倒数/正数 | Repeat cycle | Pin to taskbar | Solar subtitle for lunar)
                        var metaRow = new DockPanel { Margin = new Thickness(104, 8, 0, 0) };
                        cardStack.Children.Add(metaRow);

                        bool isPinned = resolved != null && string.Equals(Config.pinned_name, resolved.Name, StringComparison.Ordinal);
                        var pinToggle = ActionButton(isPinned ? "★ 任务栏主显" : "☆ 设为主显", () =>
                        {
                            pushUndo();
                            Config.pinned_name = isPinned ? "" : (resolved != null ? resolved.Name : item.name);
                            liveCommit(false);
                            renderCards();
                        });
                        pinToggle.Padding = new Thickness(8, 2.5, 8, 2.5);
                        pinToggle.FontSize = 10.5;
                        pinToggle.Background = Brushes.Transparent;
                        pinToggle.BorderThickness = new Thickness(0);
                        pinToggle.SetResourceReference(Button.ForegroundProperty, isPinned ? "Accent" : "Muted");
                        DockPanel.SetDock(pinToggle, Dock.Right);
                        metaRow.Children.Add(pinToggle);

                        var pills = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                        metaRow.Children.Add(pills);

                        // Kind pill toggle (倒数日 <-> 纪念正数)
                        bool isUp = string.Equals(item.kind, "countup", StringComparison.OrdinalIgnoreCase);
                        var kindBtn = ActionButton(isUp ? "正数 · 纪念日" : "倒数 · 期待日", () =>
                        {
                            pushUndo();
                            item.kind = isUp ? "countdown" : "countup";
                            if (item.kind == "countup") item.repeat = "none";
                            liveCommit(false);
                            renderCards();
                        });
                        kindBtn.Padding = new Thickness(8, 2.5, 8, 2.5);
                        kindBtn.FontSize = 10;
                        kindBtn.Margin = new Thickness(0, 0, 6, 0);
                        kindBtn.ToolTip = "点击切换「倒数日」或「纪念正数日」";
                        pills.Children.Add(kindBtn);

                        // Repeat cycle pill (only when countdown)
                        if (!isUp)
                        {
                            string[] repIds = { "none", "yearly", "monthly", "weekly" };
                            string[] repLbls = { "单次日程", "每年重复", "每月重复", "每周重复" };
                            int rIdx = Math.Max(0, Array.IndexOf(repIds, (item.repeat ?? "none").ToLowerInvariant()));
                            var repBtn = ActionButton("↻ " + repLbls[rIdx], () =>
                            {
                                pushUndo();
                                item.repeat = repIds[(rIdx + 1) % repIds.Length];
                                liveCommit(false);
                                renderCards();
                            });
                            repBtn.Padding = new Thickness(8, 2.5, 8, 2.5);
                            repBtn.FontSize = 10;
                            repBtn.Margin = new Thickness(0, 0, 6, 0);
                            repBtn.ToolTip = "点击切换重复周期（过期自动滚动至下一周期）";
                            if (rIdx > 0)
                            {
                                repBtn.SetResourceReference(Button.BackgroundProperty, "AccentSoft");
                                repBtn.SetResourceReference(Button.ForegroundProperty, "Accent");
                                repBtn.BorderThickness = new Thickness(0);
                            }
                            pills.Children.Add(repBtn);
                        }

                        if (resolved != null && resolved.IsLunar)
                        {
                            var solHint = Label("下一次公历 " + resolved.TargetDate.ToString("yyyy-MM-dd"), 10, "Muted");
                            solHint.Margin = new Thickness(4, 0, 0, 0);
                            pills.Children.Add(solHint);
                        }

                        listStack.Children.Add(card);
                    }
                };

                addBtn.Click += (s, e) =>
                {
                    pushUndo();
                    Config.items.Add(new CfgItem
                    {
                        name = "新的期待",
                        date = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd 00:00:00"),
                        enabled = true,
                        kind = "countdown",
                        repeat = "none",
                        calendar = "solar",
                        start_date = DateTime.Today.ToString("yyyy-MM-dd"),
                        icon = "auto"
                    });
                    liveCommit(false);
                    renderCards();
                    scroller.ScrollToEnd();
                };

                // Rich Smart Preset Menu (Lunar Auto-Calculating Holidays + Modern Work/Life Scenarios!)
                var presetMenu = new ContextMenu();
                Theme(presetMenu, IsLight());
                Action<string, Func<CfgItem>> addPreset = (label, factory) =>
                {
                    AddMenu(presetMenu, label, () =>
                    {
                        pushUndo();
                        Config.items.Add(factory());
                        liveCommit(false);
                        renderCards();
                        scroller.ScrollToEnd();
                    });
                };

                addPreset("🏮 春节（农历正月初一 · 永久每年自动推算）", () => new CfgItem { name = "春节", date = DateTime.Today.ToString("yyyy-MM-dd 00:00:00"), calendar = "lunar", lunar_month = 1, lunar_day = 1, repeat = "yearly", icon = "lantern", enabled = true });
                addPreset("🌕 中秋节（农历八月十五 · 每年重复）", () => new CfgItem { name = "中秋", date = DateTime.Today.ToString("yyyy-MM-dd 00:00:00"), calendar = "lunar", lunar_month = 8, lunar_day = 15, repeat = "yearly", icon = "moon", enabled = true });
                addPreset("🎋 端午节（农历五月初五 · 每年重复）", () => new CfgItem { name = "端午", date = DateTime.Today.ToString("yyyy-MM-dd 00:00:00"), calendar = "lunar", lunar_month = 5, lunar_day = 5, repeat = "yearly", icon = "sparkle", enabled = true });
                addPreset("✨ 七夕节（农历七月初七 · 每年重复）", () => new CfgItem { name = "七夕", date = DateTime.Today.ToString("yyyy-MM-dd 00:00:00"), calendar = "lunar", lunar_month = 7, lunar_day = 7, repeat = "yearly", icon = "heart", enabled = true });
                presetMenu.Items.Add(new Separator());
                addPreset("🎄 圣诞节（12月25日 · 每年重复）", () => new CfgItem { name = "圣诞", date = NextSolarDate(12, 25) + " 00:00:00", repeat = "yearly", icon = "tree", enabled = true });
                addPreset("🎆 元旦跨年（1月1日 · 每年重复）", () => new CfgItem { name = "元旦", date = NextSolarDate(1, 1) + " 00:00:00", repeat = "yearly", icon = "sparkle", enabled = true });
                addPreset("🚩 国庆节（10月1日 · 每年重复）", () => new CfgItem { name = "国庆", date = NextSolarDate(10, 1) + " 00:00:00", repeat = "yearly", icon = "flag", enabled = true });
                presetMenu.Items.Add(new Separator());
                addPreset("💼 周五下班倒计时（每周五 18:00 循环）", () => new CfgItem { name = "周五下班", date = NextFridayEvening().ToString("yyyy-MM-dd HH:mm:ss"), repeat = "weekly", icon = "briefcase", enabled = true });
                addPreset("💰 每月发薪日（每月10日自动循环）", () => new CfgItem { name = "发薪日", date = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 10).ToString("yyyy-MM-dd 00:00:00"), repeat = "monthly", icon = "coin", enabled = true });
                addPreset("❤️ 纪念日 / 正数日（从今天开始累计）", () => new CfgItem { name = "重要纪念日", date = DateTime.Today.ToString("yyyy-MM-dd 00:00:00"), kind = "countup", repeat = "none", icon = "heart", enabled = true });

                presetBtn.Click += (s, e) => { SetPalette(presetMenu, IsLight(), false); presetMenu.PlacementTarget = presetBtn; presetMenu.IsOpen = true; };

                renderCards();
                return dock;
            };

            // =====================================================================
            // TAB 1: 外观画廊 (Live Taskbar Sandbox + 14 Visual Skin Swatch Cards)
            // =====================================================================
            Func<FrameworkElement> buildAppearanceTab = () =>
            {
                var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
                var stack = new StackPanel { Margin = new Thickness(2, 2, 10, 16) };
                scroll.Content = stack;

                var hTitle = Label("外观画廊", 20, "Ink"); hTitle.FontWeight = FontWeights.SemiBold;
                stack.Children.Add(hTitle);
                var hSub = Label("点击任意色卡即刻同步到你的 Windows 任务栏，无需额外保存。", 11.5, "Muted");
                hSub.Margin = new Thickness(0, 3, 0, 14);
                stack.Children.Add(hSub);

                // --- 1:1 Live Taskbar Preview Sandbox ---
                bool sandboxDark = !IsLight();
                var sandboxCard = new Border { CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 16) };
                sandboxCard.SetResourceReference(Border.BorderBrushProperty, "Line");
                var sandboxGrid = new Grid { Height = 122 };
                sandboxCard.Child = sandboxGrid;
                stack.Children.Add(sandboxCard);

                var wallpaperLayer = new Border();
                sandboxGrid.Children.Add(wallpaperLayer);

                var simTaskbar = new Border { Height = 48, VerticalAlignment = VerticalAlignment.Bottom, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(18, 0, 18, 0) };
                sandboxGrid.Children.Add(simTaskbar);

                var tbDock = new DockPanel();
                simTaskbar.Child = tbDock;
                var fakeClock = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
                DockPanel.SetDock(fakeClock, Dock.Right);
                var fcTime = new TextBlock { Text = DateTime.Now.ToString("HH:mm"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right };
                var fcDate = new TextBlock { Text = DateTime.Now.ToString("yyyy/M/d"), FontSize = 9.5, HorizontalAlignment = HorizontalAlignment.Right };
                fakeClock.Children.Add(fcTime); fakeClock.Children.Add(fcDate);
                tbDock.Children.Add(fakeClock);

                var pillPreviewHost = new ContentControl { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
                tbDock.Children.Add(pillPreviewHost);

                var wallSwitchBtn = ActionButton("切换预览壁纸", () => { });
                wallSwitchBtn.Padding = new Thickness(9, 4, 9, 4);
                wallSwitchBtn.FontSize = 10.5;
                wallSwitchBtn.HorizontalAlignment = HorizontalAlignment.Right;
                wallSwitchBtn.VerticalAlignment = VerticalAlignment.Top;
                wallSwitchBtn.Margin = new Thickness(0, 10, 12, 0);
                sandboxGrid.Children.Add(wallSwitchBtn);

                Action refreshSandbox = () =>
                {
                    wallpaperLayer.Background = sandboxDark
                        ? (Brush)new LinearGradientBrush(C_("#141E30"), C_("#243B55"), new Point(0, 0), new Point(1, 1))
                        : (Brush)new LinearGradientBrush(C_("#CFE0F5"), C_("#E9DFD3"), new Point(0, 0), new Point(1, 1));
                    simTaskbar.Background = Brush(sandboxDark ? "#D4181C22" : "#DCEFF3F8");
                    simTaskbar.BorderBrush = Brush(sandboxDark ? "#22FFFFFF" : "#18000000");
                    fcTime.Foreground = Brush(sandboxDark ? "#EEF2F7" : "#1D2430");
                    fcDate.Foreground = Brush(sandboxDark ? "#A8B3C2" : "#5F6B7A");
                    wallSwitchBtn.Content = sandboxDark ? "☀ 浅色桌面预览" : "🌙 深色桌面预览";

                    bool previewLight = Config.theme == "light" ? true : (Config.theme == "dark" ? false : !sandboxDark);
                    string sid = ResolveSkinId();
                    var sk = PickSkinPalette(sid, previewLight);
                    bool isMin = sid == "minimal";
                    bool themed = sid != "default" && !isMin;

                    var pill = new Border
                    {
                        Height = 36,
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(10, 0, 12, 0),
                        BorderThickness = new Thickness(isMin ? 0 : 1)
                    };
                    if (isMin)
                    {
                        pill.Background = Brushes.Transparent;
                    }
                    else
                    {
                        string a = Config.glass ? (themed ? "D0" : "C2") : "FF";
                        pill.Background = new LinearGradientBrush(C_(HexA(a, sk.Bg1)), C_(HexA(a, sk.Bg2)), new Point(0, 0), new Point(1, 1));
                        pill.BorderBrush = Brush(previewLight ? "#24000000" : "#30FFFFFF");
                    }

                    var pillStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    var now = DateTime.Now;
                    var sampleList = GetResolvedItems(now).Take(Config.display_mode == "single" || Config.display_mode == "rotate" ? 1 : (Config.display_mode == "dual" ? 2 : 3)).ToList();
                    if (sampleList.Count == 0)
                    {
                        pillStack.Children.Add(new TextBlock { Text = "春节 85 天", Foreground = Brush(sk.Text), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
                    }
                    else
                    {
                        for (int k = 0; k < sampleList.Count; k++)
                        {
                            if (k > 0) pillStack.Children.Add(new Border { Width = 1, Height = 14, Background = Brush(previewLight ? "#22000000" : "#26FFFFFF"), Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
                            var r = sampleList[k];
                            var itemCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                            var titleLine = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                            var ic = BuildEventIcon(r.ResolvedIcon, 10, Brush(sk.Detail), 1.45);
                            ic.Margin = new Thickness(0, 0, 4, 0);
                            titleLine.Children.Add(ic);
                            titleLine.Children.Add(new TextBlock { Text = r.Name, FontSize = 9.5, Foreground = Brush(sk.Muted) });
                            itemCol.Children.Add(titleLine);

                            string suf; bool num;
                            string val = FormatResolvedShortValue(r, now, Config.show_seconds, out suf, out num);
                            var valRow = new StackPanel { Orientation = Orientation.Horizontal };
                            valRow.Children.Add(new TextBlock { Text = val, FontFamily = ResolveWidgetNumberFont(), FontSize = num ? 16 : 12.5, FontWeight = FontWeights.Bold, Foreground = Brush(sk.Accent) });
                            if (!string.IsNullOrEmpty(suf)) valRow.Children.Add(new TextBlock { Text = suf, FontSize = 9.5, Foreground = Brush(sk.Muted), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(2.5, 0, 0, 2) });
                            itemCol.Children.Add(valRow);
                            pillStack.Children.Add(itemCol);
                        }
                    }

                    var pillGrid = new Grid();
                    pillGrid.Children.Add(pillStack);
                    if (Config.show_progress_bar && !isMin)
                    {
                        var progLine = new Border { Height = 2, Width = 68, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, CornerRadius = new CornerRadius(1), Background = Brush(HexA("AA", sk.Accent)), Margin = new Thickness(0, 0, 0, 1) };
                        pillGrid.Children.Add(progLine);
                    }
                    pill.Child = pillGrid;
                    pillPreviewHost.Content = pill;
                };
                wallSwitchBtn.Click += (s, e) => { sandboxDark = !sandboxDark; refreshSandbox(); };

                // --- Layout & Behavior Controls Card ---
                var cfgCard = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(16, 14, 16, 14), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 16) };
                cfgCard.SetResourceReference(Border.BackgroundProperty, "Surface");
                cfgCard.SetResourceReference(Border.BorderBrushProperty, "Line");
                stack.Children.Add(cfgCard);

                var cfgGrid = new Grid();
                cfgGrid.ColumnDefinitions.Add(new ColumnDefinition());
                cfgGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
                cfgGrid.ColumnDefinitions.Add(new ColumnDefinition());
                cfgCard.Child = cfgGrid;

                var leftCfg = new StackPanel(); Grid.SetColumn(leftCfg, 0); cfgGrid.Children.Add(leftCfg);
                var rightCfg = new StackPanel(); Grid.SetColumn(rightCfg, 2); cfgGrid.Children.Add(rightCfg);

                // Mode selector
                string[] modeIds = { "single", "dual", "expand", "rotate" };
                int curModeIdx = Math.Max(0, Array.IndexOf(modeIds, (Config.display_mode ?? "expand").ToLowerInvariant()));
                var mCap = Label("任务栏胶囊形态", 11, "Muted"); mCap.Margin = new Thickness(0, 0, 0, 6); leftCfg.Children.Add(mCap);
                var rotateRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), Visibility = curModeIdx == 3 ? Visibility.Visible : Visibility.Collapsed };
                leftCfg.Children.Add(makeStudioSegmented(new[] { "单焦", "双联", "并排", "轮播" }, curModeIdx, idx =>
                {
                    Config.display_mode = modeIds[idx];
                    rotateRow.Visibility = idx == 3 ? Visibility.Visible : Visibility.Collapsed;
                    liveCommit(false);
                    refreshSandbox();
                }));

                rotateRow.Children.Add(Label("轮播切换间隔（秒）", 11, "Muted"));
                var rotBox = new TextBox { Text = Config.rotate_seconds.ToString(), Width = 48, Height = 28, Margin = new Thickness(8, 0, 0, 0), TextAlignment = TextAlignment.Center, MaxLength = 2 };
                rotBox.LostFocus += (s, e) =>
                {
                    int v;
                    if (int.TryParse(rotBox.Text, out v) && v >= 2 && v <= 60) { Config.rotate_seconds = v; liveCommit(false); }
                    else rotBox.Text = Config.rotate_seconds.ToString();
                };
                rotateRow.Children.Add(rotBox);
                leftCfg.Children.Add(rotateRow);

                // Font style selector
                string[] fontIds = { "modern", "editorial", "mono" };
                int curFontIdx = Math.Max(0, Array.IndexOf(fontIds, (Config.font_style ?? "modern").ToLowerInvariant()));
                var fCap = Label("数字排版风格", 11, "Muted"); fCap.Margin = new Thickness(0, 12, 0, 6); leftCfg.Children.Add(fCap);
                leftCfg.Children.Add(makeStudioSegmented(new[] { "现代工业", "人文衬线", "极客等宽" }, curFontIdx, idx =>
                {
                    Config.font_style = fontIds[idx];
                    liveCommit(false);
                    refreshSandbox();
                }));

                // Theme selector
                string[] themeIds = { "system", "light", "dark" };
                int curThemeIdx = Math.Max(0, Array.IndexOf(themeIds, (Config.theme ?? "system").ToLowerInvariant()));
                var tCap = Label("明暗外观", 11, "Muted"); tCap.Margin = new Thickness(0, 0, 0, 6); rightCfg.Children.Add(tCap);
                rightCfg.Children.Add(makeStudioSegmented(new[] { "跟随系统", "云白浅色", "曜石深色" }, curThemeIdx, idx =>
                {
                    Config.theme = themeIds[idx];
                    liveCommit(true);
                    if (refreshCurrentTab != null) refreshCurrentTab();
                }));

                // Taskbar magnetic anchor
                string[] anchorIds = { "free", "left", "right" };
                int curAnchorIdx = Math.Max(0, Array.IndexOf(anchorIds, (Config.anchor ?? "free").ToLowerInvariant()));
                var aCap = Label("任务栏位置吸附", 11, "Muted"); aCap.Margin = new Thickness(0, 12, 0, 6); rightCfg.Children.Add(aCap);
                rightCfg.Children.Add(makeStudioSegmented(new[] { "自由拖动", "贴靠左侧", "贴靠托盘" }, curAnchorIdx, idx =>
                {
                    Config.anchor = anchorIds[idx];
                    liveCommit(false);
                }));

                // Toggles row below
                var togglesWrap = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
                Grid.SetColumnSpan(togglesWrap, 3);
                var cfgOuter = new StackPanel();
                cfgCard.Child = cfgOuter;
                cfgOuter.Children.Add(cfgGrid);
                cfgOuter.Children.Add(togglesWrap);

                Action<string, bool, Action<bool>> addSwitch = (lbl, init, onToggle) =>
                {
                    var chk = new CheckBox { Content = lbl, IsChecked = init, Margin = new Thickness(0, 0, 22, 6), FontSize = 11.5 };
                    chk.Checked += (s, e) => { onToggle(true); refreshSandbox(); };
                    chk.Unchecked += (s, e) => { onToggle(false); refreshSandbox(); };
                    togglesWrap.Children.Add(chk);
                };
                addSwitch("半透明磨砂感", Config.glass, v => { Config.glass = v; liveCommit(true); });
                addSwitch("底部时光进度光丝", Config.show_progress_bar, v => { Config.show_progress_bar = v; liveCommit(true); });
                addSwitch("微光动效与翻牌", Config.motion, v => { Config.motion = v; liveCommit(true); });
                addSwitch("任务栏直接显示时分秒", Config.show_seconds, v => { Config.show_seconds = v; liveCommit(false); });

                // --- Visual 14-Skin Swatch Gallery (3-Column Grid) ---
                var galHead = Label("精选质感皮肤（14 款 · 点击即换）", 13.5, "Ink");
                galHead.FontWeight = FontWeights.SemiBold;
                galHead.Margin = new Thickness(2, 0, 0, 10);
                stack.Children.Add(galHead);

                var gallery = new Grid();
                gallery.ColumnDefinitions.Add(new ColumnDefinition());
                gallery.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
                gallery.ColumnDefinitions.Add(new ColumnDefinition());
                gallery.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
                gallery.ColumnDefinitions.Add(new ColumnDefinition());
                stack.Children.Add(gallery);

                var skinIdsAll = new List<string> { "auto" };
                for (int i = 0; i < SkinCatalog.Length; i++) skinIdsAll.Add(SkinCatalog[i].Id);

                int rowsNeeded = (skinIdsAll.Count + 2) / 3;
                for (int r = 0; r < rowsNeeded; r++) gallery.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var skinBorders = new List<Border>();
                Action syncSkinSelection = () =>
                {
                    string activeS = string.IsNullOrEmpty(Config.skin) ? "default" : Config.skin;
                    for (int i = 0; i < skinBorders.Count; i++)
                    {
                        bool on = string.Equals(skinIdsAll[i], activeS, StringComparison.OrdinalIgnoreCase);
                        skinBorders[i].BorderThickness = new Thickness(on ? 2 : 1);
                        skinBorders[i].SetResourceReference(Border.BorderBrushProperty, on ? "Accent" : "Line");
                    }
                };

                bool galleryLight = IsLight();
                for (int i = 0; i < skinIdsAll.Count; i++)
                {
                    string sid = skinIdsAll[i];
                    bool isAuto = sid == "auto";
                    var spec = isAuto ? null : FindSkin(sid);
                    var pal = isAuto ? PickSkinPalette("christmas", galleryLight) : (galleryLight ? spec.LightPal : spec.DarkPal);

                    var card = new Border
                    {
                        CornerRadius = new CornerRadius(11),
                        Padding = new Thickness(12, 11, 12, 11),
                        Margin = new Thickness(0, 0, 0, 10),
                        Cursor = Cursors.Hand
                    };
                    card.SetResourceReference(Border.BackgroundProperty, "Surface");

                    var cStack = new StackPanel();
                    card.Child = cStack;

                    // Mini pill swatch preview
                    var swatch = new Border
                    {
                        Height = 40,
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(10, 0, 10, 0),
                        Margin = new Thickness(0, 0, 0, 8),
                        BorderThickness = new Thickness(1),
                        BorderBrush = Brush(galleryLight ? "#1A000000" : "#24FFFFFF")
                    };
                    if (sid == "minimal")
                    {
                        swatch.Background = new LinearGradientBrush(C_(galleryLight ? "#E4ECF5" : "#1E2530"), C_(galleryLight ? "#F2ECE4" : "#252C38"), 0);
                    }
                    else if (isAuto)
                    {
                        swatch.Background = new LinearGradientBrush(C_("#FBF3E8"), C_("#E5EFE8"), new Point(0, 0), new Point(1, 1));
                    }
                    else
                    {
                        swatch.Background = new LinearGradientBrush(C_(pal.Bg1), C_(pal.Bg2), new Point(0, 0), new Point(1, 1));
                    }

                    var swDock = new DockPanel();
                    swatch.Child = swDock;
                    var swNum = new TextBlock { Text = "85 天", FontFamily = WidgetNumFont, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Brush(isAuto ? "#286B51" : pal.Accent), VerticalAlignment = VerticalAlignment.Center };
                    DockPanel.SetDock(swNum, Dock.Right); swDock.Children.Add(swNum);
                    var swLbl = new TextBlock { Text = isAuto ? "✦ 智能" : (spec.Seal ?? "期待"), FontSize = 10.5, Foreground = Brush(isAuto ? "#4F2D23" : pal.Text), VerticalAlignment = VerticalAlignment.Center };
                    swDock.Children.Add(swLbl);
                    cStack.Children.Add(swatch);

                    var sName = Label(isAuto ? "跟随最近节日" : spec.Label, 12, "Ink");
                    sName.FontWeight = FontWeights.SemiBold;
                    cStack.Children.Add(sName);
                    var sSub = Label(isAuto ? "临近节日自动切换主题" : (spec.Subtitle ?? ""), 10, "Muted");
                    sSub.Margin = new Thickness(0, 2, 0, 0);
                    cStack.Children.Add(sSub);

                    card.MouseLeftButtonUp += (s, e) =>
                    {
                        Config.skin = sid;
                        liveCommit(true);
                        syncSkinSelection();
                        refreshSandbox();
                    };

                    Grid.SetRow(card, i / 3);
                    Grid.SetColumn(card, (i % 3) * 2);
                    gallery.Children.Add(card);
                    skinBorders.Add(card);
                }

                syncSkinSelection();
                refreshSandbox();
                return scroll;
            };

            // =====================================================================
            // TAB 2: 社交工坊 (Embedded Share Atelier 2.0)
            // =====================================================================
            Func<FrameworkElement> buildAtelierTab = () =>
            {
                if (disposeAtelier != null) { try { disposeAtelier(); } catch { } disposeAtelier = null; }
                var resolvedAll = GetResolvedItems(DateTime.Now);
                var entries = resolvedAll.Select(r => ToShareEntry(r)).ToList();
                if (entries.Count == 0)
                {
                    entries.Add(new ShareEntry { Name = "春节", Date = LunarHelper.NextLunarOccurrence(1, 1, false, DateTime.Now, TimeSpan.Zero, out _dummyPrev), Enabled = true });
                }
                Action disp;
                var panel = BuildShareAtelierView(entries, 0, Config.skin, editor, out disp);
                disposeAtelier = disp;
                return panel;
            };

            // =====================================================================
            // TAB 3: 系统偏好 (Startup Switch, Fullscreen Auto-Hide, Backup/Restore)
            // =====================================================================
            Func<FrameworkElement> buildSystemTab = () =>
            {
                var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
                var stack = new StackPanel { Margin = new Thickness(2, 2, 10, 16) };
                scroll.Content = stack;

                var hTitle = Label("系统与习惯", 20, "Ink"); hTitle.FontWeight = FontWeights.SemiBold;
                stack.Children.Add(hTitle);
                var hSub = Label("将原本散落的脚本收归此处，开箱即享原生级桌面礼仪。", 11.5, "Muted");
                hSub.Margin = new Thickness(0, 3, 0, 16);
                stack.Children.Add(hSub);

                Func<string, StackPanel> makeSectionCard = secTitle =>
                {
                    var card = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(18, 15, 18, 15), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 14) };
                    card.SetResourceReference(Border.BackgroundProperty, "Surface");
                    card.SetResourceReference(Border.BorderBrushProperty, "Line");
                    var sp = new StackPanel();
                    var t = Label(secTitle, 13.5, "Ink"); t.FontWeight = FontWeights.SemiBold; t.Margin = new Thickness(0, 0, 0, 12);
                    sp.Children.Add(t);
                    card.Child = sp;
                    stack.Children.Add(card);
                    return sp;
                };

                // Card 1: Startup & Desktop Etiquette
                var s1 = makeSectionCard("启动与桌面礼仪");

                Action<StackPanel, string, string, UIElement> addSettingRow = (parent, titleTxt, descTxt, rightCtrl) =>
                {
                    var row = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
                    DockPanel.SetDock(rightCtrl, Dock.Right);
                    row.Children.Add(rightCtrl);
                    var l = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
                    var lt = Label(titleTxt, 12.5, "Ink"); lt.FontWeight = FontWeights.Medium;
                    l.Children.Add(lt);
                    l.Children.Add(Label(descTxt, 10.5, "Muted"));
                    row.Children.Add(l);
                    parent.Children.Add(row);
                };

                var startupChk = new CheckBox { IsChecked = IsStartupEnabled() };
                startupChk.Checked += (s, e) => { SetStartupEnabled(true); syncTxt.Text = "✓ 已开启开机自启"; syncTimer.Stop(); syncTimer.Start(); };
                startupChk.Unchecked += (s, e) => { SetStartupEnabled(false); syncTxt.Text = "已关闭开机自启"; syncTimer.Stop(); syncTimer.Start(); };
                addSettingRow(s1, "开机自动启动", "以单文件自托管守护模式随 Windows 登录静默启动（Countdown.exe --watchdog），无需手工运行脚本。", startupChk);

                var fsChk = new CheckBox { IsChecked = Config.hide_fullscreen };
                fsChk.Checked += (s, e) => { Config.hide_fullscreen = true; liveCommit(false); };
                fsChk.Unchecked += (s, e) => { Config.hide_fullscreen = false; liveCommit(false); };
                addSettingRow(s1, "全屏应用自动隐身", "看视频、演示 PPT 或全屏游戏时自动淡出避让，退出全屏立即无感恢复。", fsChk);

                var trayChk = new CheckBox { IsChecked = Config.show_tray_icon };
                trayChk.Checked += (s, e) => { Config.show_tray_icon = true; liveCommit(false); };
                trayChk.Unchecked += (s, e) => { Config.show_tray_icon = false; liveCommit(false); };
                addSettingRow(s1, "系统托盘兜底图标", "在右下角托盘区常驻图标（左键开浮层 / 双击开工作台 / 右键菜单），即使任务栏被遮挡也能随时找到它。", trayChk);

                int clickIdx = string.Equals(Config.click_action, "seconds", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                var clickSeg = makeStudioSegmented(new[] { "展开期待浮层", "切换时分秒" }, clickIdx, idx =>
                {
                    Config.click_action = idx == 1 ? "seconds" : "flyout";
                    liveCommit(false);
                });
                clickSeg.Width = 216;
                addSettingRow(s1, "左键单击任务栏胶囊", "推荐展开「期待浮层」，不撑宽任务栏即可查看精确秒表、全部日程与快捷记一笔。", clickSeg);

                // Card 2: Position & Quick Actions
                var s2 = makeSectionCard("任务栏定位与调试");
                var posBtns = new StackPanel { Orientation = Orientation.Horizontal };
                var resetPosBtn = ActionButton("重置任务栏位置", () =>
                {
                    Config.anchor = "free";
                    Offset = 236;
                    try { File.WriteAllText(PosPath, Json.Serialize(new { offset = Offset })); } catch { }
                    Position();
                    liveCommit(false);
                });
                resetPosBtn.Margin = new Thickness(0, 0, 10, 0);
                posBtns.Children.Add(resetPosBtn);

                var openFlyBtn = ActionButton("立即体验期待浮层", () => OpenFlyout());
                posBtns.Children.Add(openFlyBtn);
                s2.Children.Add(posBtns);

                // Card 3: Data Backup & Portability
                var s3 = makeSectionCard("数据隐私与迁移");
                var pDesc = Label("所有日程与偏好均 100% 存储于本地 config.json，零联网、零追踪。支持一键备份并在新电脑恢复。", 11, "Muted");
                pDesc.TextWrapping = TextWrapping.Wrap;
                pDesc.Margin = new Thickness(0, 0, 0, 12);
                s3.Children.Add(pDesc);

                var ioBtns = new StackPanel { Orientation = Orientation.Horizontal };
                var exportBtn = ActionButton("导出配置备份…", () =>
                {
                    var dlg = new Microsoft.Win32.SaveFileDialog { Title = "导出倒计时配置", Filter = "JSON 配置文件|*.json", FileName = "taskbar-countdown-backup.json" };
                    if (dlg.ShowDialog(editor) == true)
                    {
                        File.WriteAllText(dlg.FileName, Json.Serialize(Config), new System.Text.UTF8Encoding(false));
                        syncTxt.Text = "✓ 已导出配置备份"; syncTimer.Stop(); syncTimer.Start();
                    }
                });
                exportBtn.Margin = new Thickness(0, 0, 10, 0);
                ioBtns.Children.Add(exportBtn);

                var importBtn = ActionButton("从备份恢复…", () =>
                {
                    var dlg = new Microsoft.Win32.OpenFileDialog { Title = "导入倒计时配置", Filter = "JSON 配置文件|*.json" };
                    if (dlg.ShowDialog(editor) == true)
                    {
                        try
                        {
                            var imported = Json.Deserialize<Cfg>(File.ReadAllText(dlg.FileName));
                            if (imported != null)
                            {
                                pushUndo();
                                imported.items = imported.items ?? new List<CfgItem>();
                                Config = imported;
                                liveCommit(true);
                                syncTxt.Text = "✓ 已恢复备份配置";
                            }
                        }
                        catch (Exception ex) { MessageBox.Show(editor, "配置文件无效：" + ex.Message, "导入失败"); }
                    }
                });
                ioBtns.Children.Add(importBtn);
                s3.Children.Add(ioBtns);

                // Shortcut tips footer
                var tipCard = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 10, 14, 10) };
                tipCard.SetResourceReference(Border.BackgroundProperty, "Field");
                tipCard.Child = Label("小技巧：鼠标悬停在任务栏胶囊上滚动滚轮，即可丝滑翻页切换不同期待日子 · 按住胶囊左键拖动可自由微调位置 · Esc 快速关闭本窗口", 10.5, "Muted");
                stack.Children.Add(tipCard);

                return scroll;
            };

            refreshCurrentTab = () =>
            {
                contentHost.Children.Clear();
                if (activeTab == 0) contentHost.Children.Add(buildMomentsTab());
                else if (activeTab == 1) contentHost.Children.Add(buildAppearanceTab());
                else if (activeTab == 2) contentHost.Children.Add(buildAtelierTab());
                else contentHost.Children.Add(buildSystemTab());
            };

            syncNavHighlight();
            refreshCurrentTab();

            editor.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    editor.Close();
                }
                else if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && !(Keyboard.FocusedElement is TextBox))
                {
                    performUndo();
                    e.Handled = true;
                }
            };
            editor.Show();
            editor.Activate();
        }

        static DateTime _dummyPrev;
    }
}
