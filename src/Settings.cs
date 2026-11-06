using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Controls.Primitives;

namespace CountdownWidget {
 public static partial class Program {
    static void SetPalette(FrameworkElement view, bool light) {
        string[] keys = { "Canvas", "Surface", "Field", "Ink", "Muted", "Line", "Accent" };
        string[] colors = light ? new[] { "#F3F5F7", "#FFFFFF", "#F4F6F8", "#24303D", "#76818E", "#DFE5EB", "#527BA6" } : new[] { "#1C2026", "#252B33", "#303843", "#EBF0F6", "#96A3B3", "#3C4653", "#678CB5" };
        for(int i=0;i<keys.Length;i++) view.Resources[keys[i]]=Brush(colors[i]);
    }
    static void Theme(FrameworkElement view, bool light) {
        using(var stream=typeof(Program).Assembly.GetManifestResourceStream("Theme.xaml")) view.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(stream));
        SetPalette(view, light);
    }
    static TextBlock Label(string text,double size,string color) { var t=new TextBlock {Text=text,FontSize=size,VerticalAlignment=VerticalAlignment.Center}; t.SetResourceReference(TextBlock.ForegroundProperty,color); return t; }
    static Button ActionButton(string text,Action action) {var b=new Button {Content=text}; b.Click+=(s,e)=>action();return b;}
    static ImageSource Logo() { using(var stream=typeof(Program).Assembly.GetManifestResourceStream("logo.png")) { var image=BitmapFrame.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);image.Freeze();return image; } }
    sealed class EditRow {public CheckBox Enabled;public TextBox Name,Date;public Border View;}
    static void OpenSettings() {
        if(Editor!=null){Editor.Topmost=true;Editor.Activate();return;}
        var editor=new Window {Title="倒计时设置",Width=790,Height=760,MinWidth=700,MinHeight=600,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,WindowStartupLocation=WindowStartupLocation.CenterScreen,Topmost=true,ShowInTaskbar=true,FontFamily=new FontFamily("Microsoft YaHei UI, Segoe UI"),FontSize=12,UseLayoutRounding=true,Icon=Logo()};
        editor.Height=Math.Min(editor.Height,SystemParameters.WorkArea.Height-30);Editor=editor;
        Theme(editor,IsLight());
        editor.Closed+=(s,e)=>Editor=null;
        var frame=new Border {CornerRadius=new CornerRadius(14),BorderThickness=new Thickness(1)};frame.SetResourceReference(Border.BackgroundProperty,"Canvas");frame.SetResourceReference(Border.BorderBrushProperty,"Line");editor.Content=frame;
        var outer=new DockPanel();frame.Child=outer;
        var chrome=new DockPanel {Height=48,Margin=new Thickness(18,0,12,0)};DockPanel.SetDock(chrome,Dock.Top);outer.Children.Add(chrome);
        var close=ActionButton("✕",()=>editor.Close());close.Padding=new Thickness(13,6,13,6);close.Background=Brushes.Transparent;close.BorderThickness=new Thickness(0);close.ToolTip="关闭";DockPanel.SetDock(close,Dock.Right);chrome.Children.Add(close);
        var minimize=ActionButton("—",()=>editor.WindowState=WindowState.Minimized);minimize.Padding=close.Padding;minimize.Background=Brushes.Transparent;minimize.BorderThickness=new Thickness(0);minimize.ToolTip="最小化";DockPanel.SetDock(minimize,Dock.Right);chrome.Children.Add(minimize);
        var brand=new StackPanel {Orientation=Orientation.Horizontal,Background=Brushes.Transparent};brand.Children.Add(new Image {Source=Logo(),Width=22,Height=22,Margin=new Thickness(0,0,9,0)});brand.Children.Add(Label("倒计时",12,"Muted"));brand.MouseLeftButtonDown+=(s,e)=>{if(e.ButtonState==MouseButtonState.Pressed)editor.DragMove();};chrome.Children.Add(brand);
        var shell=new DockPanel {Margin=new Thickness(28,10,28,24)};outer.Children.Add(shell);
        var footer=new DockPanel {Margin=new Thickness(0,18,0,0)};DockPanel.SetDock(footer,Dock.Bottom);shell.Children.Add(footer);
        var save=ActionButton("保存并应用",()=>{});save.SetResourceReference(Button.BackgroundProperty,"Accent");save.Foreground=Brushes.White;save.BorderThickness=new Thickness(0);save.Padding=new Thickness(22,11,22,11);DockPanel.SetDock(save,Dock.Right);footer.Children.Add(save);
        var cancel=ActionButton("取消",()=>editor.Close());cancel.Margin=new Thickness(0,0,10,0);DockPanel.SetDock(cancel,Dock.Right);footer.Children.Add(cancel);
        footer.Children.Add(Label("让期待，留在眼前。",11,"Muted"));
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);shell.Children.Add(top);
        var title=Label("值得期待的日子",26,"Ink");title.FontWeight=FontWeights.SemiBold;top.Children.Add(title);
        var intro=Label("安静地记录时间，认真地期待下一次相逢。",12,"Muted");intro.Margin=new Thickness(0,7,0,22);top.Children.Add(intro);
        var panel=new Border {CornerRadius=new CornerRadius(10),Padding=new Thickness(18),Margin=new Thickness(0,0,0,22)};panel.SetResourceReference(Border.BackgroundProperty,"Surface");top.Children.Add(panel);
        var prefs=new StackPanel();panel.Child=prefs;
        var section=Label("显示偏好",13,"Ink");section.FontWeight=FontWeights.SemiBold;section.Margin=new Thickness(0,0,0,12);prefs.Children.Add(section);
        var grid=new Grid();prefs.Children.Add(grid);for(int i=0;i<3;i++)grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(i==2?0.7:1,GridUnitType.Star)});
        var mode=new ComboBox();mode.Items.Add("并排显示");mode.Items.Add("逐个轮播");mode.SelectedIndex=Config.display_mode=="rotate"?1:0;
        var theme=new ComboBox();theme.Items.Add("跟随系统");theme.Items.Add("浅色 · 云白");theme.Items.Add("深色 · 石墨");theme.SelectedIndex=Config.theme=="light"?1:Config.theme=="dark"?2:0;
        var interval=new TextBox {Text=Config.rotate_seconds.ToString(),MaxLength=2};
        FrameworkElement[] fields={mode,theme,interval};string[] labels={"显示方式","外观主题","轮播间隔 / 秒"};
        for(int i=0;i<3;i++){var col=new StackPanel {Margin=new Thickness(0,0,i==2?0:16,0)};var cap=Label(labels[i],11,"Muted");cap.Margin=new Thickness(0,0,0,7);col.Children.Add(cap);col.Children.Add(fields[i]);Grid.SetColumn(col,i);grid.Children.Add(col);}
        mode.SelectionChanged+=(s,e)=>interval.IsEnabled=mode.SelectedIndex==1;interval.IsEnabled=mode.SelectedIndex==1;
        theme.SelectionChanged+=(s,e)=>{bool light=theme.SelectedIndex==1 || (theme.SelectedIndex==0 && Convert.ToInt32(Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","SystemUsesLightTheme",1))!=0);SetPalette(editor,light);};
        var toggles=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,18,0,0)};prefs.Children.Add(toggles);
        var seconds=new CheckBox {Content="显示时分秒",IsChecked=Config.show_seconds,Margin=new Thickness(0,0,28,0)};
        var glass=new CheckBox {Content="半透明卡片",IsChecked=Config.glass};toggles.Children.Add(seconds);toggles.Children.Add(glass);
        var listTitle=new DockPanel {Margin=new Thickness(0,0,0,10)};top.Children.Add(listTitle);var listLabel=Label("我的日子",14,"Ink");listLabel.FontWeight=FontWeights.SemiBold;listTitle.Children.Add(listLabel);var count=Label("",11,"Muted");count.HorizontalAlignment=HorizontalAlignment.Right;listTitle.Children.Add(count);
        var captions=new Grid {Margin=new Thickness(14,0,14,8)};top.Children.Add(captions);captions.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(50)});captions.ColumnDefinitions.Add(new ColumnDefinition());captions.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(220)});captions.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(54)});
        string[] headers={"显示","名称","目标日期与时间",""};for(int i=0;i<4;i++){var label=Label(headers[i],10,"Muted");Grid.SetColumn(label,i);captions.Children.Add(label);}
        var addBar=new DockPanel {Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(addBar,Dock.Bottom);shell.Children.Add(addBar);
        var add=ActionButton("＋ 添加日子",()=>{});DockPanel.SetDock(add,Dock.Left);addBar.Children.Add(add);
        var preset=ActionButton("节日预设  ⌄",()=>{});preset.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(preset,Dock.Left);addBar.Children.Add(preset);
        var hint=Label("年-月-日 时:分",10,"Muted");hint.HorizontalAlignment=HorizontalAlignment.Right;addBar.Children.Add(hint);
        var list=new StackPanel();shell.Children.Add(new ScrollViewer {Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        var rows=new List<EditRow>();Action updateCount=()=>count.Text=rows.Count(r=>r.Enabled.IsChecked==true)+" 个显示 · "+rows.Count+" 个日子";
        Action<CfgItem> addRow=item=>{
            var row=new EditRow();row.View=new Border {Padding=new Thickness(2,6,2,6),BorderThickness=new Thickness(0,0,0,1)};row.View.SetResourceReference(Border.BorderBrushProperty,"Line");
            var line=new Grid();row.View.Child=line;line.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(50)});line.ColumnDefinitions.Add(new ColumnDefinition());line.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(220)});line.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(54)});
            row.Enabled=new CheckBox {IsChecked=item.enabled,ToolTip="在任务栏显示"};line.Children.Add(row.Enabled);row.Enabled.Checked+=(s,e)=>updateCount();row.Enabled.Unchecked+=(s,e)=>updateCount();
            row.Name=new TextBox {Text=item.name,MaxLength=40,Margin=new Thickness(0,0,12,0),ToolTip="日子名称"};Grid.SetColumn(row.Name,1);line.Children.Add(row.Name);
            row.Date=new TextBox {Text=item.date,Margin=new Thickness(0,0,12,0),ToolTip="例如 2027-01-01 00:00"};Grid.SetColumn(row.Date,2);line.Children.Add(row.Date);
            var remove=ActionButton("移除",()=>{rows.Remove(row);list.Children.Remove(row.View);updateCount();});remove.Padding=new Thickness(7);remove.Background=Brushes.Transparent;remove.BorderThickness=new Thickness(0);remove.SetResourceReference(Button.ForegroundProperty,"Muted");Grid.SetColumn(remove,3);line.Children.Add(remove);
            rows.Add(row);list.Children.Add(row.View);updateCount();
        };
        foreach(var item in Config.items.Where(i=>i!=null))addRow(item);updateCount();
        add.Click+=(s,e)=>{addRow(new CfgItem {name="新的期待",date=DateTime.Today.AddDays(30).ToString("yyyy-MM-dd HH:mm"),enabled=true});rows.Last().Name.Focus();rows.Last().Name.SelectAll();rows.Last().View.BringIntoView();};
        var presetMenu=new ContextMenu();Theme(presetMenu,IsLight());string[] names={"圣诞","元旦","春节","劳动节","中秋","国庆"};string[] dates={"2026-12-25","2027-01-01","2027-02-06","2027-05-01","2027-09-15","2027-10-01"};for(int i=0;i<names.Length;i++){int n=i;AddMenu(presetMenu,names[n]+"  "+dates[n],()=>addRow(new CfgItem {name=names[n],date=dates[n]+" 00:00",enabled=true}));}
        preset.Click+=(s,e)=>{SetPalette(presetMenu,theme.SelectedIndex==1 || (theme.SelectedIndex==0 && IsLight()));presetMenu.PlacementTarget=preset;presetMenu.IsOpen=true;};
        save.Click+=(s,e)=>{
            int secs;if(!int.TryParse(interval.Text,out secs)||secs<2||secs>60){MessageBox.Show(editor,"轮播间隔请输入 2 到 60 之间的整数。","检查设置");interval.Focus();return;}
            var next=new Cfg {display_mode=mode.SelectedIndex==1?"rotate":"expand",rotate_seconds=secs,theme=theme.SelectedIndex==1?"light":theme.SelectedIndex==2?"dark":"system",glass=glass.IsChecked==true,show_seconds=seconds.IsChecked==true};
            foreach(var row in rows){var date=ParseDate(row.Date.Text);if(string.IsNullOrWhiteSpace(row.Name.Text)||!date.HasValue){MessageBox.Show(editor,"请填写名称和有效日期，例如：2027-01-01 00:00。","检查日子");if(!date.HasValue)row.Date.Focus();else row.Name.Focus();return;}next.items.Add(new CfgItem {name=row.Name.Text.Trim(),date=date.Value.ToString("yyyy-MM-dd HH:mm:ss"),enabled=row.Enabled.IsChecked==true});}
            try{SaveConfig(next);editor.Close();}catch(Exception ex){MessageBox.Show(editor,"保存失败："+ex.Message);}
        };
        editor.PreviewKeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Escape)editor.Close();};
        editor.Show();editor.Activate();
    }
 }
}
