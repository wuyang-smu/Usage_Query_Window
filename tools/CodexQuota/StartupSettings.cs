using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

partial class QuotaWidget {
    static DispatcherTimer updateTimer;
    static bool updateNoticeShown;
    static StackPanel CreateStartupSettings(bool guide,out Action labels) {
        var foreground=guide ? new SolidColorBrush(Color.FromRgb(255,181,128)) : Brushes.WhiteSmoke;
        var panel=new StackPanel();
        var auto=new CheckBox {IsChecked=AppServices.AutoCheck,Margin=new Thickness(0,0,0,8)};
        var startup=new CheckBox {Margin=new Thickness(0,0,0,8)};
        try {startup.IsChecked=AppServices.IsStartupEnabled();} catch(Exception) {startup.IsEnabled=false;}
        var check=new Button {Padding=new Thickness(10,5,10,5),HorizontalAlignment=HorizontalAlignment.Left};
        var status=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};
        var open=new Button {Padding=new Thickness(10,5,10,5),Margin=new Thickness(0,8,0,0),HorizontalAlignment=HorizontalAlignment.Left};
        foreach(var element in new UIElement[]{auto,startup,check,status,open}) {var control=element as System.Windows.Controls.Control;if(control!=null) control.Foreground=foreground;var text=element as TextBlock;if(text!=null) text.Foreground=foreground;panel.Children.Add(element);}
        Action sync=()=>{
            auto.Content=Text("自动检查更新（启动及每6小时）","Check updates on launch and every 6 hours");startup.Content=Text("开机启动","Start with Windows");check.Content=Text("立即检查更新","Check now");open.Content=Text("打开发布页面","Open release page");
            check.IsEnabled=!AppServices.Checking;open.Visibility=AppServices.LatestUrl.Length>0 ? Visibility.Visible : Visibility.Collapsed;
            status.Text=AppServices.Checking ? Text("正在检查…","Checking…") : AppServices.Error.Length>0 ? Text("检查失败，可稍后重试。","Check failed. Try again later.") : AppServices.LatestUrl.Length>0 ? Text("发现新版本：","New version: ")+AppServices.LatestVersion : AppServices.LatestVersion.Length>0 ? Text("当前已是最新版本。","You are up to date.") : Text("当前版本：","Current version: ")+AppServices.CurrentVersion;
        };
        labels=sync;sync();AppServices.Changed+=sync;
        settingsWindow.Closed+=(s,e)=>AppServices.Changed-=sync;
        auto.Click+=(s,e)=>{
            try {AppServices.SetAutoCheck(auto.IsChecked==true);if(AppServices.AutoCheck) AppServices.CheckUpdates();}
            catch(Exception) {auto.IsChecked=AppServices.AutoCheck;MessageBox.Show(Text("无法保存更新设置。","Could not save update settings."));}
        };
        startup.Click+=(s,e)=>{
            try {AppServices.SetStartup(startup.IsChecked==true);}
            catch(Exception) {startup.IsChecked=AppServices.IsStartupEnabled();MessageBox.Show(Text("无法修改开机启动设置。","Could not change startup settings."));}
        };
        check.Click+=(s,e)=>AppServices.CheckUpdates();open.Click+=(s,e)=>AppServices.OpenRelease();return panel;
    }
    static void InitializeUpdates() {
        AppServices.Load(System.IO.Path.GetDirectoryName(PreferenceFile()));
        AppServices.Changed+=()=>{
            if(AppServices.LatestUrl.Length>0 && !updateNoticeShown) {
                updateNoticeShown=true;
                if(MessageBox.Show(Text("发现新版，打开 GitHub 发布页面？","A new version is available. Open the GitHub release page?"),"Codex Usage Widget",MessageBoxButton.YesNo)==MessageBoxResult.Yes) AppServices.OpenRelease();
            }
        };
        updateTimer=new DispatcherTimer {Interval=TimeSpan.FromHours(6)};
        updateTimer.Tick+=(s,e)=>{if(AppServices.AutoCheck) AppServices.CheckUpdates();};updateTimer.Start();
        window.Closed+=(s,e)=>updateTimer.Stop();if(AppServices.AutoCheck) AppServices.CheckUpdates();
    }
}