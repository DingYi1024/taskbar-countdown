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

namespace CountdownWidget
{
    public static partial class Program
    {
        static ResourceDictionary _themeDict;
        static ImageSource _logo;

        static void SetPalette(FrameworkElement view, bool light)
        {
            string[] keys = { "Canvas", "Surface", "Field", "Ink", "Muted", "Line", "Accent", "Danger" };
            string[] colors = light
                ? new[] { "#F3F5F7", "#FFFFFF", "#F4F6F8", "#24303D", "#76818E", "#E0E5EC", "#527BA6", "#C85A4A" }
                : new[] { "#1C2026", "#252B33", "#303843", "#EBF0F6", "#96A3B3", "#343D49", "#678CB5", "#E07B6D" };
            for (int i = 0; i < keys.Length; i++) view.Resources[keys[i]] = Brush(colors[i]);
        }

        static void Theme(FrameworkElement view, bool light)
        {
            if (_themeDict == null)
            {
                using (var stream = typeof(Program).Assembly.GetManifestResourceStream("Theme.xaml"))
                    _themeDict = (ResourceDictionary)XamlReader.Load(stream);
            }
            if (!view.Resources.MergedDictionaries.Contains(_themeDict))
                view.Resources.MergedDictionaries.Add(_themeDict);
            SetPalette(view, light);
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

        sealed class EditRow
        {
            public CheckBox Enabled;
            public TextBox Name, Date;
            public TextBlock Status;
            public Border View;
        }

        static string NextSolarDate(int month, int day)
        {
            var today = DateTime.Today;
            var d = new DateTime(today.Year, month, day);
            if (d < today) d = new DateTime(today.Year + 1, month, day);
            return d.ToString("yyyy-MM-dd");
        }

        static string NextInList(params string[] candidates)
        {
            var today = DateTime.Today;
            foreach (string s in candidates)
            {
                var d = ParseDate(s);
                if (d.HasValue && d.Value.Date >= today) return s;
            }
            return candidates[candidates.Length - 1];
        }

        static void OpenSettings()
        {
            if (Editor != null)
            {
                if (Editor.WindowState == WindowState.Minimized) Editor.WindowState = WindowState.Normal;
                Editor.Activate();
                return;
            }

            Window editor;
            var outer = CreateDialogChrome("倒计时设置", 790, 740, IsLight(), null, true, out editor, "倒计时");
            editor.MinWidth = 700;
            editor.MinHeight = 600;
            Editor = editor;
            editor.Closed += (s, e) => Editor = null;

            var shell = new DockPanel { Margin = new Thickness(28, 6, 28, 22) };
            outer.Children.Add(shell);

            var footer = new DockPanel { Margin = new Thickness(0, 16, 0, 0) };
            DockPanel.SetDock(footer, Dock.Bottom);
            shell.Children.Add(footer);

            var save = ActionButton("保存并应用", () => { });
            save.SetResourceReference(Button.BackgroundProperty, "Accent");
            save.Foreground = Brushes.White;
            save.BorderThickness = new Thickness(0);
            save.Padding = new Thickness(22, 10, 22, 10);
            DockPanel.SetDock(save, Dock.Right);
            footer.Children.Add(save);

            var cancel = ActionButton("取消", () => editor.Close());
            cancel.Margin = new Thickness(0, 0, 10, 0);
            cancel.Padding = new Thickness(16, 10, 16, 10);
            DockPanel.SetDock(cancel, Dock.Right);
            footer.Children.Add(cancel);
            footer.Children.Add(Label("让期待，留在眼前。", 11, "Muted"));

            var top = new StackPanel();
            DockPanel.SetDock(top, Dock.Top);
            shell.Children.Add(top);

            var title = Label("值得期待的日子", 24, "Ink");
            title.FontWeight = FontWeights.SemiBold;
            top.Children.Add(title);

            var intro = Label("安静地记录时间，认真地期待下一次相逢。", 12, "Muted");
            intro.Margin = new Thickness(0, 5, 0, 16);
            top.Children.Add(intro);

            var panel = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(18, 15, 18, 15), Margin = new Thickness(0, 0, 0, 16) };
            panel.SetResourceReference(Border.BackgroundProperty, "Surface");
            top.Children.Add(panel);

            var prefs = new StackPanel();
            panel.Child = prefs;
            var section = Label("显示偏好", 13, "Ink");
            section.FontWeight = FontWeights.SemiBold;
            section.Margin = new Thickness(0, 0, 0, 11);
            prefs.Children.Add(section);

            var grid = new Grid();
            prefs.Children.Add(grid);
            double[] prefCols = { 1.0, 0.72, 1.0, 1.15 };
            foreach (double w in prefCols)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });

            var mode = new ComboBox();
            mode.Items.Add("并排显示");
            mode.Items.Add("逐个轮播");
            mode.SelectedIndex = Config.display_mode == "rotate" ? 1 : 0;

            var interval = new TextBox { Text = Config.rotate_seconds.ToString(), MaxLength = 2, Height = 36, Padding = new Thickness(10, 0, 10, 0) };
            interval.SetResourceReference(Control.BackgroundProperty, "Field");
            interval.SetResourceReference(Control.BorderBrushProperty, "Line");

            var theme = new ComboBox();
            theme.Items.Add("跟随系统");
            theme.Items.Add("浅色 · 云白");
            theme.Items.Add("深色 · 石墨");
            theme.SelectedIndex = Config.theme == "light" ? 1 : Config.theme == "dark" ? 2 : 0;

            var skinIds = new List<string> { "auto" };
            var skin = new ComboBox();
            skin.Items.Add("跟随最近节日");
            for (int i = 0; i < SkinCatalog.Length; i++)
            {
                skinIds.Add(SkinCatalog[i].Id);
                skin.Items.Add(SkinCatalog[i].Label);
            }
            int si = skinIds.IndexOf(string.IsNullOrEmpty(Config.skin) ? "default" : Config.skin);
            skin.SelectedIndex = si >= 0 ? si : 1;

            FrameworkElement[] fields = { mode, interval, theme, skin };
            string[] labels = { "显示方式", "轮播间隔 / 秒", "外观主题", "皮肤 / 主题" };
            for (int i = 0; i < 4; i++)
            {
                var col = new StackPanel { Margin = new Thickness(0, 0, i == 3 ? 0 : 12, 0) };
                var cap = Label(labels[i], 11, "Muted");
                cap.Margin = new Thickness(0, 0, 0, 6);
                col.Children.Add(cap);
                col.Children.Add(fields[i]);
                Grid.SetColumn(col, i);
                grid.Children.Add(col);
            }

            mode.SelectionChanged += (s, e) => interval.IsEnabled = mode.SelectedIndex == 1;
            interval.IsEnabled = mode.SelectedIndex == 1;
            Func<bool> editorLight = () => theme.SelectedIndex == 1 || (theme.SelectedIndex == 0 && IsSystemLight());
            theme.SelectionChanged += (s, e) => SetPalette(editor, editorLight());

            var toggles = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
            prefs.Children.Add(toggles);
            var seconds = new CheckBox { Content = "显示时分秒", IsChecked = Config.show_seconds, Margin = new Thickness(0, 0, 28, 0) };
            var glass = new CheckBox { Content = "半透明卡片", IsChecked = Config.glass };
            var motion = new CheckBox { Content = "动态效果", IsChecked = Config.motion, Margin = new Thickness(28, 0, 0, 0), ToolTip = "节日装饰轻柔呼吸；锁屏或窗口隐藏时暂停。" };
            toggles.Children.Add(seconds);
            toggles.Children.Add(glass);
            toggles.Children.Add(motion);

            var listTitle = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            top.Children.Add(listTitle);
            var listLabel = Label("我的日子", 14, "Ink");
            listLabel.FontWeight = FontWeights.SemiBold;
            listTitle.Children.Add(listLabel);
            var count = Label("", 11, "Muted");
            count.HorizontalAlignment = HorizontalAlignment.Right;
            listTitle.Children.Add(count);

            Action<Grid> setRowCols = g =>
            {
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(185) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            };

            var captions = new Grid { Margin = new Thickness(2, 0, 16, 6) };
            top.Children.Add(captions);
            setRowCols(captions);
            string[] headers = { "显示", "名称", "目标日期与时间", "剩余", "", "" };
            for (int i = 0; i < 6; i++)
            {
                var label = Label(headers[i], 10, "Muted");
                if (i == 1 || i == 2) label.Margin = new Thickness(8, 0, 12, 0);
                else if (i == 3) { label.HorizontalAlignment = HorizontalAlignment.Right; label.Margin = new Thickness(0, 0, 8, 0); }
                Grid.SetColumn(label, i);
                captions.Children.Add(label);
            }

            var addBar = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
            DockPanel.SetDock(addBar, Dock.Bottom);
            shell.Children.Add(addBar);

            var add = ActionButton("＋ 添加日子", () => { });
            DockPanel.SetDock(add, Dock.Left);
            addBar.Children.Add(add);

            var preset = ActionButton("节日预设  ⌄", () => { });
            preset.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(preset, Dock.Left);
            addBar.Children.Add(preset);

            var shareAll = ActionButton("生成社交卡片", () => { });
            shareAll.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(shareAll, Dock.Left);
            addBar.Children.Add(shareAll);

            var hint = Label("格式：年-月-日 时:分   ·   Ctrl+S 保存", 10, "Muted");
            hint.HorizontalAlignment = HorizontalAlignment.Right;
            addBar.Children.Add(hint);

            var list = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
            shell.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });

            var rows = new List<EditRow>();
            Action updateCount = () => count.Text = rows.Count(r => r.Enabled.IsChecked == true) + " 个显示 · " + rows.Count + " 个日子";
            Func<string> selectedSkinId = () => (skin.SelectedIndex >= 0 && skin.SelectedIndex < skinIds.Count) ? skinIds[skin.SelectedIndex] : "default";

            Action<EditRow> openShareFor = targetRow =>
            {
                int initial;
                var entries = BuildShareEntries(
                    rows,
                    r => r.Name.Text, r => r.Date.Text, r => r.Enabled.IsChecked == true,
                    r => r == targetRow,
                    out initial);
                if (entries.Count == 0) { MessageBox.Show(editor, "请先填写至少一个有效日期的日子。", "生成社交卡片"); return; }
                OpenShareCard(entries, initial, selectedSkinId(), editorLight());
            };
            shareAll.Click += (s, e) => openShareFor(null);

            Action<EditRow> updateStatus = r =>
            {
                var parsed = ParseDate(r.Date.Text);
                if (!parsed.HasValue) { r.Status.Text = "日期无效"; r.Status.SetResourceReference(TextBlock.ForegroundProperty, "Danger"); return; }
                var d = DayText(parsed.Value, DateTime.Now, false);
                if (d == "已到期" || d == "今天") { r.Status.Text = d; r.Status.SetResourceReference(TextBlock.ForegroundProperty, d == "今天" ? "Accent" : "Muted"); }
                else { r.Status.Text = d + " 天后"; r.Status.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); }
            };

            Action<CfgItem> addRow = item =>
            {
                var row = new EditRow();
                row.View = new Border { Padding = new Thickness(2, 5, 2, 5), BorderThickness = new Thickness(0, 0, 0, 1) };
                row.View.SetResourceReference(Border.BorderBrushProperty, "Line");
                var line = new Grid();
                row.View.Child = line;
                setRowCols(line);

                row.Enabled = new CheckBox { IsChecked = item.enabled, ToolTip = "在任务栏显示" };
                line.Children.Add(row.Enabled);
                row.Enabled.Checked += (s, e) => updateCount();
                row.Enabled.Unchecked += (s, e) => updateCount();

                row.Name = new TextBox { Text = item.name, MaxLength = 40, Margin = new Thickness(0, 0, 12, 0), ToolTip = "日子名称" };
                Grid.SetColumn(row.Name, 1);
                line.Children.Add(row.Name);

                row.Date = new TextBox { Text = item.date, Margin = new Thickness(0, 0, 12, 0), ToolTip = "例如 2027-01-01 00:00" };
                Grid.SetColumn(row.Date, 2);
                line.Children.Add(row.Date);

                row.Status = Label("", 11, "Muted");
                row.Status.HorizontalAlignment = HorizontalAlignment.Right;
                row.Status.Margin = new Thickness(0, 0, 8, 0);
                Grid.SetColumn(row.Status, 3);
                line.Children.Add(row.Status);
                row.Date.TextChanged += (s, e) => updateStatus(row);
                updateStatus(row);

                var share = ActionButton("卡片", () => openShareFor(row));
                share.Padding = new Thickness(6);
                share.Background = Brushes.Transparent;
                share.BorderThickness = new Thickness(0);
                share.SetResourceReference(Button.ForegroundProperty, "Accent");
                share.ToolTip = "为此日子生成社交卡片";
                Grid.SetColumn(share, 4);
                line.Children.Add(share);

                var remove = ActionButton("移除", () => { rows.Remove(row); list.Children.Remove(row.View); updateCount(); });
                remove.Padding = new Thickness(6);
                remove.Background = Brushes.Transparent;
                remove.BorderThickness = new Thickness(0);
                remove.SetResourceReference(Button.ForegroundProperty, "Muted");
                Grid.SetColumn(remove, 5);
                line.Children.Add(remove);

                rows.Add(row);
                list.Children.Add(row.View);
                updateCount();
            };

            foreach (var item in Config.items.Where(i => i != null)) addRow(item);
            updateCount();

            add.Click += (s, e) =>
            {
                addRow(new CfgItem { name = "新的期待", date = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd HH:mm"), enabled = true });
                rows.Last().Name.Focus();
                rows.Last().Name.SelectAll();
                rows.Last().View.BringIntoView();
            };

            var presetMenu = new ContextMenu();
            Theme(presetMenu, IsLight());
            string[] names = { "圣诞", "元旦", "春节", "劳动节", "中秋", "国庆" };
            string[] dates = {
                NextSolarDate(12, 25),
                NextSolarDate(1, 1),
                NextInList("2027-02-06", "2028-01-26", "2029-02-13"),
                NextSolarDate(5, 1),
                NextInList("2027-09-15", "2028-10-03", "2029-09-22"),
                NextSolarDate(10, 1)
            };
            for (int i = 0; i < names.Length; i++)
            {
                int n = i;
                AddMenu(presetMenu, names[n] + "  " + dates[n], () =>
                {
                    addRow(new CfgItem { name = names[n], date = dates[n] + " 00:00", enabled = true });
                    rows.Last().View.BringIntoView();
                });
            }
            preset.Click += (s, e) => { SetPalette(presetMenu, editorLight()); presetMenu.PlacementTarget = preset; presetMenu.IsOpen = true; };

            Action doSave = () =>
            {
                int secs;
                if (!int.TryParse(interval.Text, out secs) || secs < 2 || secs > 60)
                {
                    MessageBox.Show(editor, "轮播间隔请输入 2 到 60 之间的整数。", "检查设置");
                    interval.Focus();
                    return;
                }
                var next = new Cfg
                {
                    display_mode = mode.SelectedIndex == 1 ? "rotate" : "expand",
                    rotate_seconds = secs,
                    theme = theme.SelectedIndex == 1 ? "light" : theme.SelectedIndex == 2 ? "dark" : "system",
                    skin = selectedSkinId(),
                    glass = glass.IsChecked == true,
                    show_seconds = seconds.IsChecked == true,
                    motion = motion.IsChecked == true
                };
                foreach (var row in rows)
                {
                    var date = ParseDate(row.Date.Text);
                    if (string.IsNullOrWhiteSpace(row.Name.Text) || !date.HasValue)
                    {
                        MessageBox.Show(editor, "请填写名称和有效日期，例如：2027-01-01 00:00。", "检查日子");
                        if (!date.HasValue) row.Date.Focus(); else row.Name.Focus();
                        return;
                    }
                    next.items.Add(new CfgItem { name = row.Name.Text.Trim(), date = date.Value.ToString("yyyy-MM-dd HH:mm:ss"), enabled = row.Enabled.IsChecked == true });
                }
                try { SaveConfig(next); editor.Close(); }
                catch (Exception ex) { MessageBox.Show(editor, "保存失败：" + ex.Message); }
            };

            save.Click += (s, e) => doSave();
            editor.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) editor.Close();
                else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; doSave(); }
            };
            editor.Show();
            editor.Activate();
        }
    }
}
