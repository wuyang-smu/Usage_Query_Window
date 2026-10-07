using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Diagnostics;
using System.Windows.Data;

public class SegmentedVisual : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(double), typeof(SegmentedVisual), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register("Fill", typeof(Brush), typeof(SegmentedVisual), new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register("Kind", typeof(string), typeof(SegmentedVisual), new FrameworkPropertyMetadata("quota", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register("Caption", typeof(string), typeof(SegmentedVisual), new FrameworkPropertyMetadata("—", FrameworkPropertyMetadataOptions.AffectsRender));
    public string Caption { get { return (string)GetValue(CaptionProperty); } set { SetValue(CaptionProperty, value); } }
    public double Value { get { return (double)GetValue(ValueProperty); } set { SetValue(ValueProperty, value); } }
    public Brush Fill { get { return (Brush)GetValue(FillProperty); } set { SetValue(FillProperty, value); } }
    public string Kind { get { return (string)GetValue(KindProperty); } set { SetValue(KindProperty, value); } }
    static Geometry Outline(double width, double height, double slope, double inset) {
        double radius = Math.Min(3, Math.Max(0,(height-2*inset)/2));
        var geometry = new RectangleGeometry(new Rect(inset,inset,Math.Max(0,width-2*inset),Math.Max(0,height-2*inset)),radius,radius);
        geometry.Freeze(); return geometry;
    }
    protected override void OnRender(DrawingContext context) {
        bool time = Kind.StartsWith("time"), labels = Kind.EndsWith("center");
        bool fullTicks = time || Kind.Contains("full");
        bool detail = Kind.Contains("detail");
        double width = ActualWidth, height = ActualHeight;
        if (width <= 0 || height <= 0) return;
        var empty = Theme.Brush("track");
        double[] boundaries = time ? (Kind.Contains("week") ? new double[] {0,100.0/7,200.0/7,300.0/7,400.0/7,500.0/7,600.0/7,100} : new double[] {0,20,40,60,80,100}) : new double[] { 0,20,40,60,80,100 };
        double end = width * Math.Max(0, Math.Min(100, Value)) / 100;
        double slope = width * 0.30;
        var shape = Outline(width,height,slope,0);
        context.DrawGeometry(empty, null, shape);
        context.PushClip(shape);
        if (end > 0) context.DrawRectangle(Fill, null, new Rect(0,0,end,height));
        FormattedText caption = labels && !string.IsNullOrEmpty(Caption) ? new FormattedText(Caption, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 13, Brushes.WhiteSmoke, VisualTreeHelper.GetDpi(this).PixelsPerDip) : null;
        double textLeft = caption == null ? -1 : (width-caption.Width)/2;
        double textRight = caption == null ? -1 : (width+caption.Width)/2;
        var bigMark = new Pen(Theme.Brush("ticks"),detail ? 2 : 3.5);
        var smallMark = new Pen(Theme.Brush("ticks"),2);
        for (int i = 1; i < boundaries.Length - 1; i++) {
            double x = width * boundaries[i] / 100;
            context.DrawLine(bigMark,new Point(x,fullTicks ? 0 : detail ? height*0.5 : height*0.75),new Point(x,height));
        }
        foreach (int tick in time ? new int[0] : new[] { 10,30,50,70,90 }) {
            double x = width*tick/100;
            context.DrawLine(smallMark,new Point(x,fullTicks ? 0 : height*0.75),new Point(x,height));
        }
        context.Pop();
        var borderBrush = Theme.Brush("barBorder");
        context.DrawGeometry(null, new Pen(borderBrush,1.2) { LineJoin = PenLineJoin.Bevel }, Outline(width,height,slope,0.6));
        if (caption != null) {
            Point origin = new Point(textLeft,(height-caption.Height)/2);
            // Outline only the glyphs, keeping the bar visible behind and between characters.
            caption.SetForegroundBrush(Theme.Brush("caption"));
            context.DrawText(caption,origin);
        }
    }
}

class QuotaWidget
{
    class Limit { public double Remaining; public long Reset; }
    class Snapshot {
        public DateTimeOffset Time, HourTime, WeekTime;
        public Limit Hour, Week;
        public bool Live;
        public Snapshot LiveBaseline;
    }
    static Window window;
    static Snapshot snapshot;
    static bool reading;
    static string diagnostic = "";
    static bool queryFailed;
    static string queryError = "";
    static bool english, checking, expanded = true, dragging;
    static bool locked, appearanceOpen;
    static DispatcherTimer collapseTimer;
    static DispatcherTimer zoomPaintTimer;
    static DateTime zoomPaintUntil;
    static bool settingsOpen;
    static bool pinIcons = true;
    static Window settingsWindow;
    static System.Windows.Controls.Primitives.Popup welcomePopup;
    static void ConfigureSegments() {
        foreach (string name in new[] { "HourBar", "WeekBar", "CompactHourBar", "CompactWeekBar", "HourTimeBar", "WeekTimeBar", "CompactHourTimeBar", "CompactWeekTimeBar" }) {
            var bar = Control<ProgressBar>(name);
            bar.Uid = "—";
            bar.Tag = name.Contains("Time") ? "time" : "quota";
            if (name.Contains("Time") && name.Contains("Week")) bar.Tag += "-week";
            if (name.StartsWith("Compact") && !name.Contains("Time")) bar.Tag += "-center";
            else if (!name.Contains("Time")) bar.Tag += "-detail";
            var visual = new FrameworkElementFactory(typeof(SegmentedVisual));
            visual.SetBinding(SegmentedVisual.ValueProperty, new Binding("Value") { RelativeSource = RelativeSource.TemplatedParent });
            visual.SetBinding(SegmentedVisual.FillProperty, new Binding("Foreground") { RelativeSource = RelativeSource.TemplatedParent });
            visual.SetBinding(SegmentedVisual.KindProperty, new Binding("Tag") { RelativeSource = RelativeSource.TemplatedParent });
            visual.SetBinding(SegmentedVisual.CaptionProperty, new Binding("Uid") { RelativeSource = RelativeSource.TemplatedParent });
            bar.Template = new ControlTemplate(typeof(ProgressBar)) { VisualTree = visual };
        }
    }
    static double zoom = 1;
    static void LoadZoom() {
        try {
            string path = Path.Combine(Path.GetDirectoryName(PreferenceFile()), "scale.txt");
            double stored;
            if (File.Exists(path) && double.TryParse(File.ReadAllText(path), NumberStyles.Float, CultureInfo.InvariantCulture, out stored)
                && !double.IsNaN(stored) && !double.IsInfinity(stored)) zoom = Math.Max(0.4, Math.Min(1.5, stored));
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    static void SaveZoom() {
        if (checking) return;
        try {
            string folder = Path.GetDirectoryName(PreferenceFile()); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "scale.txt"), zoom.ToString("0.00", CultureInfo.InvariantCulture));
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    static void ResizeWidget() {
        ApplyScaleLayout();
        var root = Control<Border>("Root");
        double right = (double.IsNaN(window.Left) ? 0 : window.Left) + window.Width;
        double bottom = (double.IsNaN(window.Top) ? 0 : window.Top) + window.Height;
        root.LayoutTransform = new ScaleTransform(zoom, zoom);
        window.Width = 360 * zoom;
        // Commit LayoutTransform's internal sizing state before reading DesiredSize.
        // A realized window otherwise reports the previous zoom's height.
        window.UpdateLayout();
        foreach (var child in Children(root)) { var element = child as UIElement; if (element != null) element.InvalidateMeasure(); }
        root.InvalidateMeasure();
        root.Measure(new Size(360 * zoom, double.PositiveInfinity));
        window.Width = 360 * zoom;
        window.Height = Math.Ceiling(root.DesiredSize.Height);
        window.Left = right - window.Width;
        window.Top = bottom - window.Height;
        window.UpdateLayout();
    }
    static void ChangeZoom(int wheelDelta) {
        if (wheelDelta == 0) return;
        SetZoom(zoom + wheelDelta / 120.0 * 0.05);
    }
    static void SetZoom(double value) {
        if (double.IsNaN(value) || double.IsInfinity(value)) return;
        zoom = Math.Max(0.4, Math.Min(1.5, Math.Round(value, 2)));
        Render(); SaveZoom();
    }
    static void ApplyScaleLayout() {
        bool mini = zoom < 0.8;
        bool compact = !expanded || mini;
        foreach (string name in new[] { "SettingsButton", "LockButton", "RefreshButton", "CloseButton" }) {
            var icon = (Viewbox)Control<Button>(name).Content;
            icon.Width = icon.Height = 13 / Math.Max(1,zoom);
        }
        bool showIcons = pinIcons || expanded;
        Control<Grid>("Toolbar").Visibility = showIcons ? Visibility.Visible : Visibility.Collapsed;
        // Compact rows leave one pixel after the final time bar; expanded rows do not.
        Control<Grid>("Toolbar").Margin = new Thickness(0,compact ? -1 : 0,0,0);
        Control<Grid>("LayoutRoot").RowDefinitions[1].Height = new GridLength(showIcons ? (compact ? 16 : 17) : 0);
        Control<StackPanel>("Details").Visibility = expanded && !mini ? Visibility.Visible : Visibility.Collapsed;
        Control<Grid>("Compact").Visibility = expanded && !mini ? Visibility.Collapsed : Visibility.Visible;
        foreach (string prefix in new[] { "Hour", "Week" }) {
            Control<TextBlock>("Compact" + prefix + "Label").Visibility = mini ? Visibility.Collapsed : Visibility.Visible;
            Control<ProgressBar>("Compact" + prefix + "Bar").Tag = mini ? "quota-detail-center" : "quota-center";
            if (mini) Control<ProgressBar>("Compact" + prefix + "Bar").Uid = "";
        }
        if (mini) Control<TextBlock>("EmptyUsage").Visibility = Visibility.Collapsed;
    }
    static string Text(string chinese, string en) { return english ? en : chinese; }
    static string ToolbarPreferenceFile() { return Path.Combine(Path.GetDirectoryName(PreferenceFile()), "toolbar.txt"); }
    static void LoadToolbarPreference() {
        try { pinIcons = !File.Exists(ToolbarPreferenceFile()) || File.ReadAllText(ToolbarPreferenceFile()).Trim() != "hide"; }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    static void SetPinnedIcons(bool value) {
        pinIcons = value; Render();
        if (checking) return;
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(ToolbarPreferenceFile()));
            File.WriteAllText(ToolbarPreferenceFile(),pinIcons ? "pin" : "hide");
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    static void SelectLanguage(bool value) { english = value; ApplyLanguage(); SaveLanguage(); }
    static string WelcomeFile() { return Path.Combine(Path.GetDirectoryName(PreferenceFile()), "settings-tip-v1.txt"); }
    static void DismissWelcome(bool remember) {
        if (welcomePopup != null) welcomePopup.IsOpen = false;
        if (!remember || checking) return;
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(WelcomeFile()));
            File.WriteAllText(WelcomeFile(), "acknowledged");
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    static void ShowWelcome() {
        if (!checking && File.Exists(WelcomeFile())) return;
        var body = new StackPanel();
        body.Children.Add(new TextBlock {
            Text = Text("在这里调整语言、颜色和大小，也可以用滚轮缩放。", "Adjust language, colors and size here. You can also resize with the mouse wheel."),
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.WhiteSmoke, MaxWidth = 240, FontSize = 12
        });
        var dismiss = new Button { Content = Text("知道了", "Got it"), Margin = new Thickness(0,10,0,0), Padding = new Thickness(10,4,10,4), HorizontalAlignment = HorizontalAlignment.Right };
        dismiss.Click += (s,e) => DismissWelcome(true); body.Children.Add(dismiss);
        var bubble = new Grid();
        bubble.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        bubble.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        bubble.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(30,39,58)), BorderBrush = Brushes.SlateGray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(14), Child = body });
        var pointer = new System.Windows.Shapes.Path {
            Data = Geometry.Parse("M 0,0 L 8,8 L 16,0"), Fill = new SolidColorBrush(Color.FromRgb(30,39,58)),
            Stroke = Brushes.SlateGray, StrokeThickness = 1, Width = 16, Height = 8, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(10,-1,0,0)
        };
        Grid.SetRow(pointer,1); bubble.Children.Add(pointer);
        welcomePopup = new System.Windows.Controls.Primitives.Popup {
            PlacementTarget = Control<Button>("SettingsButton"), Placement = System.Windows.Controls.Primitives.PlacementMode.Top,
            AllowsTransparency = true, StaysOpen = true, Child = bubble
        };
        if (checking) {
            if (bubble.Children.Count != 2 || body.Children.Count != 2) throw new Exception("Welcome bubble construction failed.");
            dismiss.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        } else welcomePopup.IsOpen = true;
    }
    static void OpenSettings() {
        if (settingsOpen) return;
        settingsOpen = true; collapseTimer.Stop(); DismissWelcome(false);
        var dialog = new Window { Width = 360, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            Owner = checking ? null : window, WindowStartupLocation = WindowStartupLocation.CenterOwner, Topmost = true,
            Background = new SolidColorBrush(Color.FromRgb(27,33,48)), Foreground = Brushes.WhiteSmoke,
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12, ShowInTaskbar = false };
        settingsWindow = dialog;
        var panel = new StackPanel { Margin = new Thickness(18) }; dialog.Content = panel;
        var languageLabel = new TextBlock { Margin = new Thickness(0,0,0,6) }; panel.Children.Add(languageLabel);
        var language = new ComboBox { SelectedIndex = english ? 1 : 0, Margin = new Thickness(0,0,0,16) };
        language.Items.Add("中文"); language.Items.Add("English"); panel.Children.Add(language);
        var scaleLabel = new TextBlock { Margin = new Thickness(0,0,0,6) }; panel.Children.Add(scaleLabel);
        var scaleRow = new Grid(); scaleRow.ColumnDefinitions.Add(new ColumnDefinition()); scaleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(66) }); scaleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        var slider = new Slider { Minimum = 40, Maximum = 150, Value = zoom * 100, TickFrequency = 1, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,12,0) };
        var input = new TextBox { Text = (zoom * 100).ToString("0",CultureInfo.InvariantCulture), Padding = new Thickness(5), VerticalContentAlignment = VerticalAlignment.Center };
        var percent = new TextBlock { Text = "%", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4,0,0,0) };
        scaleRow.Children.Add(slider); Grid.SetColumn(input,1); scaleRow.Children.Add(input); Grid.SetColumn(percent,2); scaleRow.Children.Add(percent); panel.Children.Add(scaleRow);
        var note = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,8,0,14), Foreground = Brushes.LightSlateGray }; panel.Children.Add(note);
        var colors = new Button { Padding = new Thickness(10,6,10,6), Margin = new Thickness(0,0,0,16) }; panel.Children.Add(colors);
        var toolbarOption = new CheckBox { IsChecked = pinIcons, Foreground = Brushes.WhiteSmoke, Margin = new Thickness(0,0,0,16) }; panel.Children.Add(toolbarOption);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var done = new Button { Padding = new Thickness(10,5,10,5), IsCancel = true };
        actions.Children.Add(done); panel.Children.Add(actions);
        Action labels = () => {
            dialog.Title = Text("系统设置", "Settings"); languageLabel.Text = Text("语言", "Language");
            scaleLabel.Text = Text("缩放比例", "Scale");
            note.Text = Text("40%–150% · 支持滚轮缩放。低于 80% 时仅显示条形和按钮。修改自动保存。", "40%–150% · Mouse wheel supported. Below 80%, only bars and buttons are shown. Changes save automatically.");
            colors.Content = Text("配色设置…", "Colors…"); done.Content = Text("完成", "Done");
            toolbarOption.Content = Text("收起时保留底部按钮", "Keep bottom buttons when collapsed");
        };
        labels();
        language.SelectionChanged += (s,e) => { SelectLanguage(language.SelectedIndex == 1); labels(); };
        toolbarOption.Checked += (s,e) => SetPinnedIcons(true);
        toolbarOption.Unchecked += (s,e) => SetPinnedIcons(false);
        slider.ValueChanged += (s,e) => { SetZoom(slider.Value / 100); input.Text = (zoom * 100).ToString("0",CultureInfo.InvariantCulture); input.ClearValue(System.Windows.Controls.Control.BorderBrushProperty); };
        Action commit = () => {
            double value;
            if (!double.TryParse(input.Text.Trim().TrimEnd('%'),NumberStyles.Float,CultureInfo.InvariantCulture,out value) || double.IsNaN(value) || double.IsInfinity(value)) {
                input.BorderBrush = Brushes.OrangeRed; return;
            }
            SetZoom(value / 100); slider.Value = zoom * 100;
            input.Text = (zoom * 100).ToString("0",CultureInfo.InvariantCulture); input.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
        };
        input.LostKeyboardFocus += (s,e) => commit();
        input.KeyDown += (s,e) => { if (e.Key == System.Windows.Input.Key.Enter) { commit(); e.Handled = true; } };
        colors.Click += (s,e) => OpenAppearance();
        done.Click += (s,e) => { commit(); dialog.Close(); };
        try {
            if (checking) {
                double before = zoom; bool wasEnglish = english;
                bool previousPin = pinIcons;
                toolbarOption.IsChecked = !previousPin;
                if (pinIcons == previousPin) throw new Exception("Toolbar setting did not update.");
                toolbarOption.IsChecked = previousPin;
                slider.Value = 40;
                if (zoom != 0.4 || Control<StackPanel>("Details").Visibility != Visibility.Collapsed || Control<ProgressBar>("CompactHourBar").Uid != "") throw new Exception("Settings slider / mini layout failed.");
                input.Text = "125"; commit();
                if (zoom != 1.25 || slider.Value != 125) throw new Exception("Scale input failed.");
                input.Text = "NaN"; commit(); if (zoom != 1.25) throw new Exception("Invalid scale accepted.");
                input.Text = "999"; commit(); if (zoom != 1.5) throw new Exception("Scale input upper bound failed.");
                input.Text = "10"; commit(); if (zoom != 0.4) throw new Exception("Scale input lower bound failed.");
                language.SelectedIndex = wasEnglish ? 0 : 1;
                if (english == wasEnglish || dialog.Title != Text("系统设置", "Settings")) throw new Exception("Settings language selection failed.");
                language.SelectedIndex = wasEnglish ? 1 : 0;
                colors.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                SetZoom(before);
                panel.Measure(new Size(324,double.PositiveInfinity)); panel.Arrange(new Rect(panel.DesiredSize)); panel.UpdateLayout();
                var bitmap = new RenderTargetBitmap(360,(int)Math.Ceiling(panel.DesiredSize.Height+36),96,96,PixelFormats.Pbgra32);
                var drawing = new DrawingVisual(); using (var context = drawing.RenderOpen()) { context.DrawRectangle(dialog.Background,null,new Rect(0,0,bitmap.PixelWidth,bitmap.PixelHeight)); context.PushTransform(new TranslateTransform(18,18)); context.DrawRectangle(new VisualBrush(panel),null,new Rect(panel.RenderSize)); }
                bitmap.Render(drawing); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var stream = File.Create(settingsPreviewPath)) encoder.Save(stream);
            } else {
                dialog.WindowStartupLocation = WindowStartupLocation.Manual;
                panel.Measure(new Size(dialog.Width,double.PositiveInfinity));
                PositionSettings(dialog,panel.DesiredSize.Height+SystemParameters.WindowCaptionHeight+2*SystemParameters.FixedFrameHorizontalBorderHeight);
                dialog.ContentRendered += (s,e) => PositionSettings(dialog);
                dialog.ShowDialog();
            }
        } finally { settingsOpen = false; settingsWindow = null; if (!window.IsMouseOver) collapseTimer.Start(); }
    }
    static string settingsPreviewPath;
    static void PositionSettings(Window dialog, double estimatedHeight = 0) {
        const double gap = 8;
        var area = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(window).Handle).WorkingArea;
        var source = PresentationSource.FromVisual(window);
        var transform = source == null ? Matrix.Identity : source.CompositionTarget.TransformFromDevice;
        var topLeft = transform.Transform(new Point(area.Left,area.Top));
        var bottomRight = transform.Transform(new Point(area.Right,area.Bottom));
        double height = dialog.ActualHeight > 0 ? dialog.ActualHeight : estimatedHeight;
        dialog.Left = Math.Max(topLeft.X,Math.Min(bottomRight.X-dialog.Width,window.Left+(window.Width-dialog.Width)/2));
        double above = window.Top-height-gap;
        dialog.Top = above >= topLeft.Y ? above : window.Top+window.Height+gap;
    }
    static void CheckRealizedLayout(string report) {
        // Realize the native window without displaying it or querying an account.
        window.Opacity = 0; window.ShowActivated = false; window.ShowInTaskbar = false;
        window.Left = 100; window.Top = 100;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        snapshot = new Snapshot { Time = DateTimeOffset.UtcNow, Live = true,
            Hour = new Limit { Remaining = 65, Reset = now + 18000 },
            Week = new Limit { Remaining = 72, Reset = now + 604800 } };
        window.Show(); window.UpdateLayout();
        SetZoom(1); SetExpanded(true); window.UpdateLayout(); Render(); window.UpdateLayout();
        double detailHeight = window.Height;
        SetExpanded(false); window.UpdateLayout(); Render(); window.UpdateLayout();
        double compactHeight = window.Height;
        foreach (bool details in new[] {false,true}) {
            SetExpanded(details); window.UpdateLayout();
            var lastBar = Control<ProgressBar>(details ? "WeekTimeBar" : "CompactWeekTimeBar");
            double barBottom = lastBar.TranslatePoint(new Point(0,lastBar.ActualHeight),window).Y;
            foreach (string name in new[] {"SettingsButton","LockButton","RefreshButton","CloseButton"}) {
                var icon = (Viewbox)Control<Button>(name).Content;
                double iconTop = icon.TranslatePoint(new Point(0,0),window).Y;
                double iconBottom = icon.TranslatePoint(new Point(0,icon.ActualHeight),window).Y;
                double topGap = iconTop-barBottom, bottomGap = window.ActualHeight-1-iconBottom;
                if (topGap < -0.1 || topGap > 2.2 || bottomGap < -0.1 || bottomGap > 2.2)
                    throw new Exception(string.Format(CultureInfo.InvariantCulture,"Icon spacing incorrect: {0}, details={1}, top={2}, bottom={3}",name,details,topGap,bottomGap));
            }
        }
        var observations = new List<string>();
        foreach (bool en in new[] {false,true}) {
            SelectLanguage(en);
            foreach (bool pin in new[] {false,true}) {
                locked = false; SetExpanded(false); window.UpdateLayout();
                if (pin) ToggleLock(); else SetExpanded(true);
                foreach (double scale in new[] {0.4,0.75,0.8,1.0,1.5,0.4,0.8,1.0}) {
                    SetZoom(scale);
                    double expected = (scale < 0.8 ? compactHeight : detailHeight) * scale;
                    observations.Add(string.Format(CultureInfo.InvariantCulture,"{0}, lock={1}, scale={2}, height={3}, expected={4}",en,pin,scale,window.Height,expected));
                    if (Math.Abs(window.Height - expected) > 2)
                        throw new Exception("Immediate height incorrect: " + observations.Last());
                    window.UpdateLayout();
                    if (Math.Abs(window.ActualHeight - expected) > 2)
                        throw new Exception("Realized height incorrect: " + observations.Last());
                    foreach (string name in new[] {"SettingsButton","LockButton","RefreshButton","CloseButton"}) {
                        var icon = (Viewbox)Control<Button>(name).Content;
                        Rect visualBounds = icon.TransformToAncestor(window).TransformBounds(new Rect(0,0,icon.ActualWidth,icon.ActualHeight));
                        double expectedIcon = 13*Math.Min(1,scale);
                        if (Math.Abs(visualBounds.Width-expectedIcon) > 1 || Math.Abs(visualBounds.Height-expectedIcon) > 1)
                            throw new Exception("Icon scale cap failed: " + name + " at " + scale);
                    }
                }
                locked = false; SetExpanded(false); window.UpdateLayout();
            }
        }
        locked = false; SetZoom(1); SetExpanded(false);
        double stableBottom = window.Top+window.Height;
        SetPinnedIcons(false); window.UpdateLayout();
        if (Control<Grid>("Toolbar").Visibility != Visibility.Collapsed || Control<Grid>("LayoutRoot").RowDefinitions[1].Height.Value != 0)
            throw new Exception("Unpinned collapsed toolbar still occupies space.");
        if (Math.Abs(window.Top+window.Height-stableBottom) > 0.1) throw new Exception("Unpinning moved the bottom anchor.");
        double collapsedWithoutIcons = window.Height;
        SetExpanded(true); window.UpdateLayout();
        if (Control<Grid>("Toolbar").Visibility != Visibility.Visible || window.Height <= collapsedWithoutIcons || Math.Abs(window.Top+window.Height-stableBottom) > 0.1)
            throw new Exception("Unpinned toolbar did not expand upward with a fixed bottom.");
        double anchoredIconBottom = Control<Button>("SettingsButton").TranslatePoint(new Point(0,17),window).Y+window.Top;
        SetExpanded(false); window.UpdateLayout(); SetExpanded(true); window.UpdateLayout();
        if (Math.Abs(Control<Button>("SettingsButton").TranslatePoint(new Point(0,17),window).Y+window.Top-anchoredIconBottom) > 0.1)
            throw new Exception("Icon position changed between expansions.");
        SetZoom(0.4); SetExpanded(false); window.UpdateLayout();
        if (Control<Grid>("Toolbar").Visibility != Visibility.Collapsed) throw new Exception("Mini collapsed toolbar not hidden.");
        SetExpanded(true); window.UpdateLayout();
        if (Control<Grid>("Toolbar").Visibility != Visibility.Visible) throw new Exception("Mini hover cannot restore toolbar.");
        SetPinnedIcons(true); SetZoom(1); SetExpanded(false);
        window.Top = 450;
        var settingsTest = new Window { Width = 360, Height = 240, Opacity = 0, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
        settingsWindow = settingsTest;
        try {
            settingsTest.Show(); settingsTest.UpdateLayout(); PositionSettings(settingsTest);
            if (settingsTest.Top+settingsTest.ActualHeight > window.Top-7) throw new Exception("Settings overlaps owner above.");
            SetExpanded(true); SetZoom(1.5);
            if (settingsTest.Top+settingsTest.ActualHeight > window.Top-7) throw new Exception("Settings does not follow owner resizing.");
            window.Top = 0; PositionSettings(settingsTest);
            if (settingsTest.Top < window.Top+window.Height+7) throw new Exception("Settings below fallback overlaps owner.");
        } finally { settingsWindow = null; settingsTest.Close(); }
        window.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,-120) { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseWheelEvent });
        if (!zoomPaintTimer.IsEnabled) throw new Exception("Wheel redraw burst did not start.");
        var frame = new DispatcherFrame();
        var finish = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        finish.Tick += (s,e) => frame.Continue = false;
        finish.Start(); Dispatcher.PushFrame(frame); finish.Stop();
        if (zoomPaintTimer.IsEnabled) throw new Exception("Wheel redraw burst did not stop.");
        File.WriteAllText(report,"PASS: realized-window lock expansion and scale transitions update height immediately in both languages; settings above owner, resize following, below fallback; wheel redraw burst starts and stops. No network calls or preference writes. Physical wheel gestures not exercised.\r\n"+string.Join("\r\n",observations));
    }
    static string ThemeFile() { return Path.Combine(Path.GetDirectoryName(PreferenceFile()), "colors.json"); }
    static void LoadTheme() {
        try {
            if (!File.Exists(ThemeFile())) return;
            var saved = new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(ThemeFile()));
            foreach (string key in Theme.Keys) if (saved != null && saved.ContainsKey(key) && Theme.Valid(saved[key])) Theme.Colors[key] = saved[key];
        } catch (IOException) { } catch (UnauthorizedAccessException) { } catch (ArgumentException) { }
    }
    static IEnumerable<DependencyObject> Children(DependencyObject root) {
        for (int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {
            var child = VisualTreeHelper.GetChild(root,i); yield return child;
            foreach (var nested in Children(child)) yield return nested;
        }
    }
    static void ApplyTheme() {
        var root = Control<Border>("Root");
        root.Background = Theme.Brush("background"); root.BorderBrush = Theme.Brush("windowBorder");
        foreach (var child in Children(root)) {
            var text = child as TextBlock;
            if (text != null) text.Foreground = Theme.Brush(text.Name == "TitleLabel" ? "title" : "label");
            var shape = child as System.Windows.Shapes.Shape;
            if (shape != null) shape.Stroke = Theme.Brush("icons");
            var segmented = child as SegmentedVisual;
            if (segmented != null) segmented.InvalidateVisual();
        }
        foreach (string name in new[] { "HourValue", "WeekValue" }) Control<TextBlock>(name).Foreground = Theme.Brush("valueText");
        foreach (string name in new[] { "HourReset", "WeekReset" }) Control<TextBlock>(name).Foreground = Theme.Brush("timeText");

        Control<Button>("SettingsButton").ToolTip = Text("系统设置 · 语言、配色、缩放（也可滚轮缩放）", "Settings · language, colors, scale (or use the mouse wheel)");
        Render();
    }
    static void OpenAppearance() {
        if (appearanceOpen) return;
        appearanceOpen = true; collapseTimer.Stop(); SetExpanded(true);
        var original = new Dictionary<string,string>(Theme.Colors);
        bool accepted = false;
        var dialog = new Window { Owner = checking ? null : (settingsWindow ?? window), Title = Text("外观设置", "Appearance"), Width = 470, Height = 610,
            MinWidth = 430, MinHeight = 380, WindowStartupLocation = WindowStartupLocation.CenterOwner, Topmost = true,
            Background = new SolidColorBrush(Color.FromRgb(27,33,48)), Foreground = Brushes.WhiteSmoke, FontSize = 13 };
        var panel = new DockPanel { Margin = new Thickness(16) }; dialog.Content = panel;
        var help = new TextBlock { Text = Text("点击色块选色，或输入 #RRGGBB / #AARRGGBB。修改即时预览。", "Click a swatch or enter #RRGGBB / #AARRGGBB. Changes preview immediately."), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) };
        DockPanel.SetDock(help,Dock.Top); panel.Children.Add(help);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,12,0,0) };
        DockPanel.SetDock(actions,Dock.Bottom); panel.Children.Add(actions);
        var reset = new Button { Content = Text("恢复默认", "Defaults"), Padding = new Thickness(10,5,10,5), Margin = new Thickness(0,0,8,0) };
        var cancel = new Button { Content = Text("取消", "Cancel"), IsCancel = true, Padding = new Thickness(10,5,10,5), Margin = new Thickness(0,0,8,0) };
        var save = new Button { Content = Text("保存", "Save"), IsDefault = true, Padding = new Thickness(10,5,10,5) };
        actions.Children.Add(reset); actions.Children.Add(cancel); actions.Children.Add(save);
        var list = new StackPanel(); panel.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var editors = new Dictionary<string,TextBox>();
        for (int i=0;i<Theme.Keys.Length;i++) {
            string key = Theme.Keys[i];
            var row = new Grid { Margin = new Thickness(0,0,0,8) };
            row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });
            var label = new TextBlock { Text = english ? Theme.English[i] : Theme.Chinese[i], VerticalAlignment = VerticalAlignment.Center }; row.Children.Add(label);
            var swatch = new Button { Background = Theme.Brush(key), Margin = new Thickness(0,0,8,0), ToolTip = Text("选择颜色", "Choose color") }; Grid.SetColumn(swatch,1); row.Children.Add(swatch);
            var edit = new TextBox { Text = Theme.Colors[key], Padding = new Thickness(5), VerticalContentAlignment = VerticalAlignment.Center }; Grid.SetColumn(edit,2); row.Children.Add(edit); editors[key] = edit;
            edit.TextChanged += (s,e) => {
                bool valid = Theme.Valid(edit.Text); edit.BorderBrush = valid ? Brushes.Gray : Brushes.OrangeRed;
                save.IsEnabled = editors.Values.All(t => Theme.Valid(t.Text));
                if (valid) { Theme.Colors[key] = edit.Text.ToUpperInvariant(); swatch.Background = Theme.Brush(key); ApplyTheme(); }
            };
            swatch.Click += (s,e) => {
                var color = (Color)ColorConverter.ConvertFromString(Theme.Colors[key]);
                using (var picker = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.Color.FromArgb(color.R,color.G,color.B) }) {
                    if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) edit.Text = string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}",color.A,picker.Color.R,picker.Color.G,picker.Color.B);
                }
            };
            list.Children.Add(row);
        }
        reset.Click += (s,e) => { var defaults = Theme.Defaults(); foreach (string key in Theme.Keys) editors[key].Text = defaults[key]; };
        cancel.Click += (s,e) => dialog.Close();
        save.Click += (s,e) => {
            try {
                if (!checking) { Directory.CreateDirectory(Path.GetDirectoryName(ThemeFile())); File.WriteAllText(ThemeFile(),new JavaScriptSerializer().Serialize(Theme.Colors)); }
                accepted = true; dialog.Close();
            } catch (Exception ex) { MessageBox.Show(dialog,Text("颜色设置无法保存：", "Could not save colors: ") + ex.Message); }
        };
        try {
            if (checking) {
                editors["quotaGreen"].Text = "#123456";
                if (Theme.Colors["quotaGreen"] != "#123456") throw new Exception("Color preview failed.");
                editors["quotaGreen"].Text = "invalid";
                if (save.IsEnabled || Theme.Colors["quotaGreen"] != "#123456") throw new Exception("Invalid color handling failed.");
                reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!save.IsEnabled || Theme.Colors["quotaGreen"] != Theme.Defaults()["quotaGreen"]) throw new Exception("Color defaults failed.");
                var decoded = new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(new JavaScriptSerializer().Serialize(Theme.Colors));
                if (Theme.Keys.Any(key => decoded[key] != Theme.Colors[key])) throw new Exception("Color serialization failed.");
                editors["background"].Text = "#FF223344";
                if (Control<Border>("Root").Background.ToString() != "#FF223344") throw new Exception("Background preview failed.");
            } else dialog.ShowDialog();
        }
        finally { if (!accepted) Theme.Colors = original; appearanceOpen = false; ApplyTheme(); if (!window.IsMouseOver) collapseTimer.Start(); }
    }
    static string PreferenceFile() {
        string local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (string.IsNullOrEmpty(local)) local = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), "AppData", "Local");
        return Path.Combine(local, "CodexQuota", "language.txt");
    }
    static void LoadLanguage() {
        try { english = File.Exists(PreferenceFile()) && File.ReadAllText(PreferenceFile()).Trim() == "en"; }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    static void SaveLanguage() {
        if (checking) return;
        try {
            string path = PreferenceFile(); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, english ? "en" : "zh");
        } catch (IOException) { Control<Button>("SettingsButton").ToolTip = Text("语言偏好无法保存", "Could not save language preference"); }
        catch (UnauthorizedAccessException) { Control<Button>("SettingsButton").ToolTip = Text("语言偏好无法保存", "Could not save language preference"); }
    }
    static void ApplyLanguage() {
        window.Title = Text("Codex 额度", "Codex Usage");
        Control<TextBlock>("TitleLabel").Text = window.Title;
        Control<TextBlock>("HourLabel").Text = Text("5 小时剩余", "5-hour remaining");
        Control<TextBlock>("WeekLabel").Text = Text("本周剩余", "Weekly remaining");
        Control<Button>("SettingsButton").ToolTip = Text("系统设置 · 语言、配色、缩放（也可滚轮缩放）", "Settings · language, colors, scale (or use the mouse wheel)");

        Control<TextBlock>("CompactHourLabel").Text = Text("5小时", "5h");
        Control<TextBlock>("CompactWeekLabel").Text = Text("周", "Wk");

        Control<Button>("RefreshButton").ToolTip = Text("主动查询最新账户额度", "Query current account usage");
        Control<Button>("CloseButton").ToolTip = Text("关闭悬浮窗", "Close widget");
        UpdateLock();
        Render();
    }
    static void UpdateLock() {
        Control<Button>("LockButton").ToolTip = locked ? Text("解除锁定", "Unlock expanded view") : Text("锁定展开", "Lock expanded view");
        Control<System.Windows.Shapes.Path>("LockGlyph").Stroke = Theme.Brush("icons");
        Control<System.Windows.Shapes.Path>("LockGlyph").Data = Geometry.Parse(locked
            ? "M 6,10 V 7 A 4,4 0 0 1 14,7 V 10 M 3,10 H 17 V 17 H 3 Z"
            : "M 6,10 V 7 A 4,4 0 0 1 14,7 M 3,10 H 17 V 17 H 3 Z");
    }
    static void ToggleLock() {
        locked = !locked; UpdateLock();
        if (locked) { collapseTimer.Stop(); SetExpanded(true); }
        else if (Control<Grid>("QuotaArea").IsMouseOver) { collapseTimer.Stop(); SetExpanded(true); }
        else collapseTimer.Start();
    }
    static void SetExpanded(bool value) {
        if (!value && locked) return;
        if (expanded == value) return;
        expanded = value;
        Control<TextBlock>("EmptyUsage").Margin = new Thickness(0,value ? 30 : 0,0,6);
        Render();
    }
    static void NormalizeToolbarIcons() {
        foreach (string name in new[] { "SettingsButton", "LockButton", "RefreshButton", "CloseButton" }) {
            var button = Control<Button>(name);
            var view = (Viewbox)button.Content;
            var canvas = (Canvas)view.Child;
            Rect bounds = Rect.Empty;
            foreach (System.Windows.Shapes.Shape shape in canvas.Children) {
                Geometry geometry;
                var path = shape as System.Windows.Shapes.Path;
                if (path != null) geometry = path.Data;
                else geometry = new EllipseGeometry(new Rect(Canvas.GetLeft(shape),Canvas.GetTop(shape),shape.Width,shape.Height));
                bounds.Union(geometry.GetRenderBounds(new Pen(Brushes.White,shape.StrokeThickness)));
            }
            canvas.Width = bounds.Width; canvas.Height = bounds.Height;
            foreach (System.Windows.Shapes.Shape shape in canvas.Children)
                shape.RenderTransform = new TranslateTransform(-bounds.Left,-bounds.Top);
            button.Height = 17; button.VerticalContentAlignment = VerticalAlignment.Center;
            view.Width = view.Height = 13; view.Stretch = Stretch.Fill;
        }
    }
    static void ConfigureInteraction() {
        NormalizeToolbarIcons();
        zoomPaintTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        zoomPaintTimer.Tick += (s,e) => {
            if (DateTime.UtcNow >= zoomPaintUntil) { zoomPaintTimer.Stop(); return; }
            Render();
        };
        collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        collapseTimer.Tick += (s, e) => {
            collapseTimer.Stop();
            if (!locked && !appearanceOpen && !settingsOpen && !dragging && !Control<Grid>("QuotaArea").IsMouseOver) SetExpanded(false);
        };
        Control<Grid>("QuotaArea").MouseEnter += (s, e) => { collapseTimer.Stop(); SetExpanded(true); };
        Control<Grid>("QuotaArea").MouseLeave += (s, e) => { if (!dragging) collapseTimer.Start(); };
        window.PreviewMouseWheel += (s, e) => {
            ChangeZoom(e.Delta);
            zoomPaintUntil = DateTime.UtcNow.AddMilliseconds(250); zoomPaintTimer.Start();
            e.Handled = true;
        };
        Control<Button>("LockButton").Click += (s, e) => ToggleLock();
        Control<Button>("SettingsButton").Click += (s, e) => OpenSettings();
        Control<Button>("CloseButton").Click += (s,e) => window.Close();
        window.LocationChanged += (s,e) => { if (settingsWindow != null && settingsWindow.IsVisible) PositionSettings(settingsWindow); };
        window.SizeChanged += (s,e) => { if (settingsWindow != null && settingsWindow.IsVisible) PositionSettings(settingsWindow); };
        window.MouseLeftButtonDown += (s, e) => {
            var source = e.OriginalSource as DependencyObject;
            while (source != null) {
                if (source is Button || source is MenuItem) return;
                source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
            }
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
            dragging = true; collapseTimer.Stop();
            try { window.DragMove(); }
            finally { dragging = false; if (!window.IsMouseOver) collapseTimer.Start(); }
        };
        window.Closed += (s, e) => { collapseTimer.Stop(); zoomPaintTimer.Stop(); if (welcomePopup != null) welcomePopup.IsOpen = false; };
        ApplyLanguage(); ResizeWidget();
    }
    static string FindCodex() {
        string local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrEmpty(local)) {
            string root = Path.Combine(local, "OpenAI", "Codex", "bin");
            if (Directory.Exists(root)) {
                string found = Directory.EnumerateFiles(root, "codex.exe", SearchOption.AllDirectories)
                    .OrderByDescending(p => File.GetLastWriteTimeUtc(p)).FirstOrDefault();
                if (found != null) return found;
            }
        }
        foreach (string folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')) {
            if (string.IsNullOrWhiteSpace(folder)) continue;
            string file = Path.Combine(folder.Trim('"'), "codex.exe");
            if (File.Exists(file)) return file;
        }
        throw new Exception("找不到 Codex 查询程序");
    }
    static Dictionary<string, object> RpcReply(Process process, JavaScriptSerializer json, int id, DateTime deadline) {
        while (DateTime.UtcNow < deadline) {
            var read = process.StandardOutput.ReadLineAsync();
            if (!read.Wait(Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds)))
                throw new TimeoutException("额度查询超时");
            if (read.Result == null) throw new Exception("Codex 查询连接已结束");
            var reply = Map(json.DeserializeObject(read.Result));
            object replyId = Value(reply, "id");
            if (replyId == null || Convert.ToInt32(replyId) != id) continue;
            if (Value(reply, "error") != null) throw new Exception("Codex 拒绝额度查询，错误代码：" + Value(Map(Value(reply, "error")), "code"));
            return Map(Value(reply, "result"));
        }
        throw new TimeoutException("额度查询超时");
    }
    static Snapshot QuerySnapshot() {
        var info = new ProcessStartInfo(FindCodex(), "app-server --stdio") {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
        };
        using (var process = new Process { StartInfo = info }) {
            process.Start();
            // Drain diagnostics without recording authentication details or server output.
            process.ErrorDataReceived += (s, e) => { };
            process.BeginErrorReadLine();
            try {
                DateTime deadline = DateTime.UtcNow.AddSeconds(20);
                var json = new JavaScriptSerializer { MaxJsonLength = 1048576 };
                process.StandardInput.WriteLine("{\"method\":\"initialize\",\"id\":1,\"params\":{\"clientInfo\":{\"name\":\"codex_quota_widget\",\"title\":\"Codex Quota Widget\",\"version\":\"1.2\"}}}");
                process.StandardInput.Flush();
                RpcReply(process, json, 1, deadline);
                process.StandardInput.WriteLine("{\"method\":\"initialized\",\"params\":{}}");
                process.StandardInput.WriteLine("{\"method\":\"account/rateLimits/read\",\"id\":2}");
                process.StandardInput.Flush();
                var result = RpcReply(process, json, 2, deadline);
                var buckets = Map(Value(result, "rateLimitsByLimitId"));
                var limits = Map(Value(buckets, "codex")) ?? Map(Value(result, "rateLimits"));
                if (limits == null) throw new Exception("账户未返回额度信息");
                return ParseWindows(limits, DateTimeOffset.UtcNow, true);
            } finally {
                try { process.StandardInput.Close(); } catch (IOException) { }
                if (!process.WaitForExit(2000)) { process.Kill(); process.WaitForExit(2000); }
            }
        }
    }
    static Snapshot NewerSnapshot(Snapshot current, Snapshot candidate) {
        if (candidate == null) return current;
        // A successful account read is authoritative, including absent quota windows.
        if (current == null || candidate.Live) return candidate;
        var baseline = current.Live ? current : current.LiveBaseline;
        if (baseline != null && candidate.Time <= baseline.Time) return current;
        DateTimeOffset hourTime = current.HourTime == default(DateTimeOffset) ? current.Time : current.HourTime;
        DateTimeOffset weekTime = current.WeekTime == default(DateTimeOffset) ? current.Time : current.WeekTime;
        bool hour = AcceptLocalLimit(current.Hour, candidate.Hour, hourTime, candidate.Time,
            baseline == null || baseline.Hour != null);
        bool week = AcceptLocalLimit(current.Week, candidate.Week, weekTime, candidate.Time,
            baseline == null || baseline.Week != null);
        if (!hour && !week) return current;
        return new Snapshot {
            Time = candidate.Time, Live = false, LiveBaseline = baseline,
            Hour = hour ? candidate.Hour : current.Hour, Week = week ? candidate.Week : current.Week,
            HourTime = hour ? candidate.Time : hourTime, WeekTime = week ? candidate.Time : weekTime
        };
    }
    static bool AcceptLocalLimit(Limit current, Limit candidate, DateTimeOffset observed,
        DateTimeOffset timestamp, bool available) {
        if (!available || candidate == null || timestamp <= observed) return false;
        if (current == null) return true;
        // Log timestamps can advance while their embedded rate-limit data is cached.
        if (current.Reset > 0 && candidate.Reset < current.Reset) return false;
        if (candidate.Reset > current.Reset) return true;
        return candidate.Remaining <= current.Remaining;
    }
    static void CheckSnapshotSelection(string report) {
        var start = DateTimeOffset.FromUnixTimeSeconds(1800000000);
        var live = new Snapshot { Time = start, Live = true,
            Hour = new Limit { Remaining = 100, Reset = 1800018000 },
            Week = new Limit { Remaining = 65, Reset = 1800604800 } };
        var stale = new Snapshot { Time = start.AddSeconds(30),
            Hour = new Limit { Remaining = 65, Reset = 1800000000 },
            Week = new Limit { Remaining = 65, Reset = 1800604800 } };
        var selected = NewerSnapshot(live, stale);
        if (selected.Hour != live.Hour || selected.Hour.Remaining != 100)
            throw new Exception("Old 5-hour cycle replaced active query.");
        stale.Time = start.AddSeconds(60); stale.Week.Remaining = 64;
        selected = NewerSnapshot(selected, stale);
        if (selected.Hour != live.Hour || selected.Week.Remaining != 64)
            throw new Exception("Independent quota window selection failed.");
        var fresh = new Snapshot { Time = start.AddSeconds(45),
            Hour = new Limit { Remaining = 99, Reset = live.Hour.Reset },
            Week = new Limit { Remaining = 63, Reset = live.Week.Reset } };
        selected = NewerSnapshot(selected, fresh);
        if (selected.Hour.Remaining != 99 || selected.Week.Remaining != 64)
            throw new Exception("Per-window timestamps lost during partial update.");
        var cached = new Snapshot { Time = start.AddSeconds(90),
            Hour = new Limit { Remaining = 100, Reset = live.Hour.Reset } };
        if (NewerSnapshot(selected, cached) != selected)
            throw new Exception("Cached same-cycle balance replaced newer usage.");
        fresh.Time = start.AddSeconds(-1); fresh.Hour.Remaining = 98;
        if (NewerSnapshot(selected, fresh) != selected)
            throw new Exception("Pre-query local record accepted.");
        fresh.Time = start.AddSeconds(120); fresh.Hour.Remaining = 98;
        selected = NewerSnapshot(selected, fresh);
        if (selected.Hour.Remaining != 98 || selected.Week.Remaining != 63)
            throw new Exception("Current local data did not resume updates.");
        var nextCycle = new Snapshot { Time = start.AddHours(5),
            Hour = new Limit { Remaining = 100, Reset = live.Hour.Reset + 18000 } };
        selected = NewerSnapshot(selected, nextCycle);
        if (selected.Hour.Remaining != 100 || selected.Week.Remaining != 63)
            throw new Exception("Next cycle or missing-window preservation failed.");
        var missingReset = new Snapshot { Time = start.AddHours(6),
            Hour = new Limit { Remaining = 50, Reset = 0 } };
        if (NewerSnapshot(selected, missingReset) != selected || NewerSnapshot(selected, null) != selected)
            throw new Exception("Incomplete fallback replaced current data.");
        var weekOnly = new Snapshot { Time = start.AddHours(6), Live = true, Week = live.Week };
        selected = NewerSnapshot(selected, weekOnly);
        if (selected != weekOnly || NewerSnapshot(selected, nextCycle).Hour != null)
            throw new Exception("Live missing-window authority failed.");
        nextCycle.Time = start.AddHours(7);
        if (NewerSnapshot(selected, nextCycle).Hour != null)
            throw new Exception("Local record resurrected unavailable live window.");
        File.WriteAllText(report, "PASS: old-cycle rollback (100% to 65%) blocked; independent windows and per-window timestamps; cached same-cycle increase and pre-query records rejected; valid local updates and next-cycle resets accepted; missing/reset-less fallback preserved; authoritative live reads and unavailable quota windows preserved.\r\nDeterministic data-only check. No account queries or preference writes.\r\n");
    }
    static Dictionary<string, object> Map(object value) { return value as Dictionary<string, object>; }
    static object Value(Dictionary<string, object> map, string key) {
        object value; return map != null && map.TryGetValue(key, out value) ? value : null;
    }
    static Limit ParseLimit(object value) {
        var map = Map(value);
        object used = Value(map, "used_percent") ?? Value(map, "usedPercent");
        object reset = Value(map, "resets_at") ?? Value(map, "resetsAt");
        if (used == null) return null;
        return new Limit { Remaining = Math.Max(0, Math.Min(100, 100 - Convert.ToDouble(used))),
            Reset = reset == null ? 0 : Convert.ToInt64(reset) };
    }
    static Snapshot ParseWindows(Dictionary<string,object> limits, DateTimeOffset timestamp, bool live) {
        var result = new Snapshot { Time = timestamp, Live = live };
        foreach (string slot in new[] { "primary", "secondary" }) {
            var data = Map(Value(limits,slot));
            var limit = ParseLimit(data); if (limit == null) continue;
            object duration = Value(data,"windowDurationMins") ?? Value(data,"window_minutes");
            if (duration == null) { if (slot == "primary") result.Hour = limit; else result.Week = limit; }
            else { double minutes = Convert.ToDouble(duration); if (minutes == 300) result.Hour = limit; else if (minutes == 10080) result.Week = limit; }
        }
        return result;
    }
    static Snapshot ReadSnapshot() {
        string profile = Environment.GetEnvironmentVariable("USERPROFILE");
        if (string.IsNullOrEmpty(profile)) profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string home = Path.Combine(profile, ".codex");
        diagnostic = "Profile: " + home;
        var files = new List<FileInfo>();
        foreach (string folder in new[] { "sessions", "archived_sessions" }) {
            string root = Path.Combine(home, folder);
            if (!Directory.Exists(root)) continue;
            try { files.AddRange(Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories).Select(p => new FileInfo(p))); }
            catch (IOException ex) { diagnostic += "\r\n" + ex.Message; } catch (UnauthorizedAccessException ex) { diagnostic += "\r\n" + ex.Message; }
        }
        Snapshot latest = null;
        diagnostic += "\r\nFiles: " + files.Count;
        var json = new JavaScriptSerializer { MaxJsonLength = 1048576 };
        foreach (var file in files.OrderByDescending(f => f.LastWriteTimeUtc).Take(16)) {
            try {
                using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                    long offset = Math.Max(0, stream.Length - 524288);
                    stream.Seek(offset, SeekOrigin.Begin);
                    using (var reader = new StreamReader(stream)) {
                        if (offset > 0) reader.ReadLine();
                        string line;
                        while ((line = reader.ReadLine()) != null) {
                            if (!line.Contains("\"rate_limits\"")) continue;
                            try {
                                var record = Map(json.DeserializeObject(line));
                                var limits = Map(Value(Map(Value(record, "payload")), "rate_limits"));
                                var id = Value(limits, "limit_id") as string;
                                if (id != null && id != "codex") continue;
                                if (limits == null) continue;
                                var time = DateTimeOffset.Parse((string)Value(record, "timestamp"), CultureInfo.InvariantCulture);
                                if (latest == null || time > latest.Time) latest = ParseWindows(limits, time, false);
                            } catch (ArgumentException ex) { diagnostic += "\r\nParse: " + ex.Message; } catch (FormatException ex) { diagnostic += "\r\nParse: " + ex.Message; } catch (InvalidCastException ex) { diagnostic += "\r\nParse: " + ex.Message; }
                        }
                    }
                }
            } catch (IOException ex) { diagnostic += "\r\n" + ex.Message; } catch (UnauthorizedAccessException ex) { diagnostic += "\r\n" + ex.Message; }
        }
        return latest;
    }
    static string ResetText(Limit limit) {
        if (limit.Reset == 0) return Text("重置时间未知", "Reset time unavailable");
        var span = DateTimeOffset.FromUnixTimeSeconds(limit.Reset) - DateTimeOffset.UtcNow;
        if (span.TotalSeconds <= 0) return Text("已到重置时间 · 等待更新", "Reset reached · awaiting update");
        return span.TotalDays >= 1 ? string.Format(Text("{0}天 {1}小时后重置", "Resets in {0}d {1}h"), (int)span.TotalDays, span.Hours)
            : string.Format(Text("{0}小时 {1}分后重置", "Resets in {0}h {1}m"), (int)span.TotalHours, span.Minutes);
    }
    static string CompactReset(Limit limit) {
        if (limit == null || limit.Reset == 0) return "—";
        var span = DateTimeOffset.FromUnixTimeSeconds(limit.Reset) - DateTimeOffset.UtcNow;
        long minutes = Math.Max(0, (long)Math.Ceiling(span.TotalMinutes));
        return (minutes >= 1440 ? (minutes / 1440).ToString() + "d " : "")
            + ((minutes / 60) % 24).ToString() + ":" + (minutes % 60).ToString("00");
    }
    static readonly Brush Green = new SolidColorBrush(Color.FromRgb(35,122,87));
    static readonly Brush Yellow = new SolidColorBrush(Color.FromRgb(246,211,101));
    static readonly Brush Orange = new SolidColorBrush(Color.FromRgb(255,160,85));
    static readonly Brush Red = new SolidColorBrush(Color.FromRgb(255,107,120));
    static Brush QuotaColor(double remaining) {
        return remaining < 10 ? Red : remaining < 20 ? Orange : remaining < 30 ? Yellow : Green;
    }
    static Brush TimeColor(double remaining) {
        return remaining > 60 ? Red : remaining > 40 ? Orange : remaining > 20 ? Yellow : Green;
    }
    static double TimePercent(Limit limit, double hours) {
        return limit.Reset == 0 ? 0 : Math.Max(0, Math.Min(100,
            (DateTimeOffset.FromUnixTimeSeconds(limit.Reset) - DateTimeOffset.UtcNow).TotalHours / hours * 100));
    }
    static void StyleQuota(string prefix, Limit limit, double hours) {
        Brush quota = QuotaColor(limit.Remaining);
        double time = TimePercent(limit, hours);
        Brush countdown = TimeColor(time);
        quota = Theme.Brush("quota" + (limit.Remaining < 10 ? "Red" : limit.Remaining < 20 ? "Orange" : limit.Remaining < 30 ? "Yellow" : "Green"));
        countdown = Theme.Brush("time" + (time > 60 ? "Red" : time > 40 ? "Orange" : time > 20 ? "Yellow" : "Green"));
        Control<ProgressBar>(prefix + "Bar").Foreground = quota;
        Control<ProgressBar>("Compact" + prefix + "Bar").Foreground = quota;
        Control<TextBlock>(prefix + "Value").Foreground = Theme.Brush("valueText");
        foreach (string name in new[] { prefix + "TimeBar", "Compact" + prefix + "TimeBar" }) {
            Control<ProgressBar>(name).Value = time;
            Control<ProgressBar>(name).Foreground = countdown;
            Control<ProgressBar>(name).ToolTip = Text("距重置剩余时间", "Time remaining until reset");
        }
        Control<TextBlock>(prefix + "Reset").Foreground = Theme.Brush("timeText");
        Control<TextBlock>("Compact" + prefix + "Reset").Foreground = Theme.Brush("timeText");
        Control<ProgressBar>("Compact" + prefix + "Bar").Uid = limit.Remaining.ToString("0.#") + "% · " + CompactReset(limit);
        Control<ProgressBar>("Compact" + prefix + "TimeBar").Uid = CompactReset(limit);
    }
    static T Control<T>(string name) where T : class { return window.FindName(name) as T; }
    static void ShowLimit(string prefix, bool show) {
        foreach (string suffix in new[] { "Info", "Bar", "TimeBar" }) Control<FrameworkElement>(prefix+suffix).Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        foreach (string suffix in new[] { "Label", "Bar", "TimeBar" }) Control<FrameworkElement>("Compact"+prefix+suffix).Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        var rows = Control<Grid>("Compact").RowDefinitions;
        int start = prefix == "Hour" ? 0 : 3;
        rows[start].Height = new GridLength(show ? 21 : 0); rows[start+1].Height = new GridLength(show ? 7 : 0);
        rows[2].Height = new GridLength(snapshot != null && snapshot.Hour != null && snapshot.Week != null ? 6 : 0);
    }
    static void RenderLimit(string prefix, Limit limit, double hours) {
        if (limit == null) return;
        Control<TextBlock>(prefix+"Value").Text = limit.Remaining.ToString("0.#") + "%";
        Control<ProgressBar>(prefix+"Bar").Value = Control<ProgressBar>("Compact"+prefix+"Bar").Value = limit.Remaining;
        Control<TextBlock>(prefix+"Reset").Text = Control<TextBlock>("Compact"+prefix+"Reset").Text = CompactReset(limit);
        StyleQuota(prefix,limit,hours);
    }
    static void Render() {
        ShowLimit("Hour",snapshot != null && snapshot.Hour != null);
        ShowLimit("Week",snapshot != null && snapshot.Week != null);
        Control<TextBlock>("EmptyUsage").Visibility = snapshot == null || (snapshot.Hour == null && snapshot.Week == null) ? Visibility.Visible : Visibility.Collapsed;
        Control<TextBlock>("EmptyUsage").Text = Text("暂无额度数据", "No usage limits available");
        Control<TextBlock>("Updated").Foreground = Theme.Brush(queryFailed || (snapshot != null && (DateTimeOffset.UtcNow - snapshot.Time).TotalMinutes >= 5) ? "alert" : "status");
        if (reading) { Control<TextBlock>("Updated").Text = Text("正在读取额度…", "Reading usage…"); ResizeWidget(); return; }
        if (snapshot == null) {
            Control<TextBlock>("HourValue").Text = "—";
            Control<TextBlock>("WeekValue").Text = "—";
            Control<TextBlock>("Updated").Text = queryFailed ? Text("查询失败 · 暂无本地记录", "Query failed · no local data") : Text("等待额度数据", "Waiting for usage data");
            Control<TextBlock>("CompactHourReset").Text = "—";
            Control<TextBlock>("CompactWeekReset").Text = "—";
            ResizeWidget();
            return;
        }
        RenderLimit("Hour",snapshot.Hour,5);
        RenderLimit("Week",snapshot.Week,168);
        Control<TextBlock>("Updated").Text = (queryFailed ? Text("查询失败 · ", "Query failed · ") : snapshot.Live ? Text("账户查询 ", "Account query ") : Text("记录更新 ", "Local record ")) + snapshot.Time.ToLocalTime().ToString("HH:mm:ss")
            + ((DateTimeOffset.UtcNow - snapshot.Time).TotalMinutes >= 5 ? Text(" · 可能已过期", " · May be stale") : "");
        Control<TextBlock>("Updated").ToolTip = (queryFailed ? Text("主动查询失败，保留上次数据。请确认 Codex 已登录且网络可用。\n", "Query failed; keeping previous data. Check Codex sign-in and network.\n") : "")
            + Text("打开和点击刷新时主动查询；每 30 秒只读取本地记录。\n数据时间：", "Queries on launch and refresh; reads local records every 30 seconds.\nData timestamp: ") + snapshot.Time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        ResizeWidget();
    }
    static async void Refresh(bool active) {
        if (reading) return;
        reading = true;
        Control<Button>("RefreshButton").IsEnabled = false;
        if (active) Control<TextBlock>("Updated").Text = Text("正在查询账户额度…", "Querying account usage…");
        try {
            bool fallback = false;
            try {
                var candidate = await Task.Run(() => active ? QuerySnapshot() : ReadSnapshot());
                snapshot = NewerSnapshot(snapshot, candidate);
                if (active) { queryFailed = false; queryError = ""; }
            } catch (Exception ex) {
                if (active) { queryFailed = true; queryError = ex.Message; fallback = true; }
            }
            if (fallback) {
                try { snapshot = NewerSnapshot(snapshot, await Task.Run(() => ReadSnapshot())); } catch (Exception) { }
            }
        }
        finally { reading = false; Control<Button>("RefreshButton").IsEnabled = true; Render(); }
    }
    static void SavePreview(string path) {
        var root = Control<Border>("Root");
        var size = new Size(window.Width, window.Height);
        window.Content = null;
        root.LayoutTransform = Transform.Identity;
        root.Measure(new Size(360, double.PositiveInfinity));
        var baseSize = new Size(360, root.DesiredSize.Height);
        root.Arrange(new Rect(baseSize)); root.UpdateLayout();
        var unscaled = new RenderTargetBitmap((int)Math.Ceiling(baseSize.Width), (int)Math.Ceiling(baseSize.Height), 96, 96, PixelFormats.Pbgra32);
        unscaled.Render(root);
        var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen()) {
            context.DrawImage(unscaled, new Rect(0, 0, baseSize.Width * zoom, baseSize.Height * zoom));
        }
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var output = File.Create(path)) encoder.Save(output);
        window.Content = root;
        ResizeWidget();
    }
    static void CheckUi(string report) {
        var originalColors = new Dictionary<string,string>(Theme.Colors);
        OpenAppearance();
        if (appearanceOpen || Theme.Keys.Any(key => Theme.Colors[key] != originalColors[key])) throw new Exception("Color cancellation failed.");
        Control<Button>("LockButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        SetExpanded(false);
        if (!locked || !expanded) throw new Exception("Lock did not preserve expanded view.");
        Control<Button>("LockButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (locked) throw new Exception("Unlock failed.");
        if (!collapseTimer.IsEnabled) throw new Exception("Unlock did not schedule collapse without another hover event.");
        var unlockFrame = new DispatcherFrame();
        var unlockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        unlockTimer.Tick += (s,e) => unlockFrame.Continue = false;
        unlockTimer.Start(); Dispatcher.PushFrame(unlockFrame); unlockTimer.Stop();
        if (expanded) throw new Exception("Unlock did not collapse without another hover event.");
        collapseTimer.Stop();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        snapshot = new Snapshot { Time = DateTimeOffset.UtcNow, Live = true,
            Hour = new Limit { Remaining = 72, Reset = now + 16200 },
            Week = new Limit { Remaining = 42, Reset = now + 3 * 86400 + 12 * 3600 + 29 * 60 } };
        window.Left = 200; window.Top = 200;
        string folder = Path.GetDirectoryName(Path.GetFullPath(report));
        foreach (int language in new[] { 0, 1 }) {
            SelectLanguage(language == 1);
            if (english != (language == 1)) throw new Exception("Language selection failed.");
            Control<Grid>("QuotaArea").RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = System.Windows.Input.Mouse.MouseEnterEvent });
            if (!expanded || Control<StackPanel>("Details").Visibility != Visibility.Visible) throw new Exception("Hover expansion failed.");
            double bottom = window.Top + window.Height;
            SavePreview(Path.Combine(folder, english ? "english-details.png" : "chinese-details.png"));
            Control<Grid>("QuotaArea").RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = System.Windows.Input.Mouse.MouseLeaveEvent });
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            timer.Tick += (s, e) => frame.Continue = false;
            timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
            if (expanded || Control<Grid>("Compact").Visibility != Visibility.Visible) throw new Exception("Delayed collapse failed.");
            Control<Grid>("Toolbar").RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = System.Windows.Input.Mouse.MouseEnterEvent });
            if (expanded || Control<Grid>("Toolbar").Visibility != Visibility.Visible) throw new Exception("Toolbar hover expanded quota area.");
            if (Math.Abs(window.Top + window.Height - bottom) > 0.1) throw new Exception("Window anchor moved.");
            if (Control<TextBlock>("CompactWeekLabel").Text != Text("周","Wk") || Control<TextBlock>("CompactHourLabel").Text != Text("5小时","5h")) throw new Exception("Compact labels not localized.");
            if (!Control<TextBlock>("CompactWeekReset").Text.Contains("d ")) throw new Exception("Countdown formatting failed.");
            SavePreview(Path.Combine(folder, english ? "english-compact.png" : "chinese-compact.png"));
        }
        queryFailed = true; snapshot.Time = snapshot.Time.AddMinutes(-6); Render();
        if (!Control<TextBlock>("Updated").Text.Contains("Query failed") || !Control<TextBlock>("Updated").Text.Contains("May be stale")) throw new Exception("Failure translation failed.");
        foreach (double value in new[] { 0.0, 9.99, 10, 19.99, 20, 29.99, 30, 100 }) {
            Brush expected = value < 10 ? Red : value < 20 ? Orange : value < 30 ? Yellow : Green;
            if (QuotaColor(value) != expected) throw new Exception("Quota color boundary failed.");
        }
        foreach (double value in new[] { 0.0, 20, 20.01, 40, 40.01, 60, 60.01, 100 }) {
            Brush expected = value > 60 ? Red : value > 40 ? Orange : value > 20 ? Yellow : Green;
            if (TimeColor(value) != expected) throw new Exception("Countdown color boundary failed.");
        }
        snapshot.Hour.Remaining = 25; snapshot.Week.Remaining = 15;
        snapshot.Hour.Reset = now + 1800; snapshot.Week.Reset = now + 6 * 86400;
        queryFailed = false; Render();
        if (Control<ProgressBar>("CompactHourBar").Foreground.ToString() != Theme.Brush("quotaYellow").ToString() || Control<ProgressBar>("WeekBar").Foreground.ToString() != Theme.Brush("quotaOrange").ToString()) throw new Exception("Applied color failed.");
        SavePreview(Path.Combine(folder, "color-compact.png")); SetExpanded(true);
        SavePreview(Path.Combine(folder, "color-details.png"));
        foreach (int language in new[] { 0, 1 }) {
            SelectLanguage(language == 1);
            foreach (bool details in new[] { false, true }) {
                SetExpanded(details);
                foreach (double target in new[] { 0.4, 0.75, 0.8, 1.0, 1.5 }) {
                    zoom = 1; ResizeWidget();
                    double right = window.Left + window.Width, bottom = window.Top + window.Height;
                    int delta = (int)Math.Round((target - 1) / 0.05) * 120;
                    window.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, delta) { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseWheelEvent });
                    if (Math.Abs(zoom - target) > 0.001 || Math.Abs(window.Width - 360 * target) > 0.1) throw new Exception("Wheel scaling failed.");
                    if (Math.Abs(window.Left + window.Width - right) > 0.1 || Math.Abs(window.Top + window.Height - bottom) > 0.1) throw new Exception("Scale anchor moved.");
                    Control<Border>("Root").Measure(new Size(window.Width, double.PositiveInfinity));
                    if (Control<Border>("Root").DesiredSize.Height > window.Height + 1) throw new Exception("Scaled content clipped.");
                    if (target < 0.8 && (Control<StackPanel>("Details").Visibility != Visibility.Collapsed || Control<TextBlock>("CompactHourLabel").Visibility != Visibility.Collapsed || Control<ProgressBar>("CompactHourBar").Uid != "")) throw new Exception("Mini text not hidden.");
                    if (target >= 0.8 && (Control<TextBlock>("CompactHourLabel").Visibility != Visibility.Visible || string.IsNullOrEmpty(Control<ProgressBar>("CompactHourBar").Uid))) throw new Exception("Text not restored after scaling.");
                    if (((string)Control<ProgressBar>("CompactHourBar").Tag).Contains("detail") != (target < 0.8) || (string)Control<ProgressBar>("HourBar").Tag != "quota-detail") throw new Exception("Quota tick height mode failed.");
                    if (language == 1 && target != 1) SavePreview(Path.Combine(folder, "scale-" + (int)(target * 100) + (details ? "-details.png" : "-compact.png")));
                }
            }
        }
        ChangeZoom(12000); if (zoom != 1.5) throw new Exception("Upper zoom bound failed.");
        ChangeZoom(-12000); if (zoom != 0.4) throw new Exception("Lower zoom bound failed.");
        SetZoom(1);
        foreach (bool en in new[] {false,true}) {
            SelectLanguage(en); settingsPreviewPath = Path.Combine(folder,en ? "settings-en.png" : "settings-zh.png");
            Control<Button>("SettingsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ShowWelcome();
        }
        foreach (double amount in new[] { 79.5, 85, 90.5, 99.5, 100 }) {
            snapshot.Hour.Remaining = snapshot.Week.Remaining = amount;
            snapshot.Hour.Reset = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)(18000 * amount / 100);
            snapshot.Week.Reset = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)(604800 * amount / 100);
            snapshot.Time = DateTimeOffset.UtcNow;
            Render(); SetExpanded(false);
            SavePreview(Path.Combine(folder, "boundary-" + amount.ToString("0.#", CultureInfo.InvariantCulture) + ".png"));
        }
        var parser = new JavaScriptSerializer();
        var weekOnly = Map(parser.DeserializeObject("{\"primary\":{\"usedPercent\":28,\"windowDurationMins\":10080},\"secondary\":null}"));
        snapshot = ParseWindows(weekOnly,DateTimeOffset.UtcNow,true);
        if (snapshot.Hour != null || snapshot.Week == null || snapshot.Week.Remaining != 72) throw new Exception("Primary weekly window misclassified.");
        snapshot.Week.Reset = now + 86400;
        Render();
        foreach (bool details in new[] {false,true}) {
            SetExpanded(details);
            if (Control<FrameworkElement>("HourBar").Visibility != Visibility.Collapsed || Control<FrameworkElement>("WeekBar").Visibility != Visibility.Visible) throw new Exception("Weekly-only visibility failed.");
            SavePreview(Path.Combine(folder,details ? "weekly-only-details.png" : "weekly-only-compact.png"));
        }
        snapshot = ParseWindows(Map(parser.DeserializeObject("{\"primary\":null,\"secondary\":null}")),DateTimeOffset.UtcNow,true); Render();
        if (Control<TextBlock>("EmptyUsage").Visibility != Visibility.Visible) throw new Exception("Missing quotas not indicated.");
        SetExpanded(false); SavePreview(Path.Combine(folder,"no-limits.png"));
        snapshot = ParseWindows(Map(parser.DeserializeObject("{\"primary\":{\"used_percent\":10,\"window_minutes\":300},\"secondary\":null}")),DateTimeOffset.UtcNow,false); Render();
        if (snapshot.Hour == null || snapshot.Week != null) throw new Exception("Hourly-only parse failed.");
        File.WriteAllText(report, "PASS: nullable quotas, primary weekly-window classification, hourly-only local format, weekly-only layout, no-limits message; color preview, invalid-color rejection, defaults, JSON roundtrip, cancellation; settings language selection, slider and numeric scale input, invalid input, welcome bubble construction; hover collapse/expand, colors, wheel events at 40/75/80/100/150 percent in both languages and both modes, scale limits, anchored resizing and no vertical clipping.\r\nOffline previews generated. No network calls or preference writes.\r\nLive Pro account query, physical color picker, mouse dragging, welcome popup placement and saved-preference reload not exercised by this offline check.\r\n");
    }
    [STAThread]
    static int Main(string[] args) {
        try {
            if (args.Length == 2 && args[0] == "--snapshot-check") {
                CheckSnapshotSelection(args[1]); return 0;
            }
            checking = args.Length == 2 && (args[0] == "--check" || args[0] == "--ui-check" || args[0] == "--layout-check");
            using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("Widget.xaml"))
                window = (Window)XamlReader.Load(source);
            ConfigureSegments();
            using (var icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("Widget.ico")) {
                var decoder = BitmapDecoder.Create(icon, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                window.Icon = decoder.Frames[0];
            }
            Control<Button>("RefreshButton").Click += (s, e) => Refresh(true);
            if (!checking) { LoadLanguage(); LoadZoom(); LoadTheme(); LoadToolbarPreference(); }
            ConfigureInteraction();
            ApplyTheme();
            if (args.Length == 2 && args[0] == "--layout-check") {
                CheckRealizedLayout(args[1]); window.Close(); return 0;
            }
            if (args.Length == 2 && args[0] == "--ui-check") {
                CheckUi(args[1]); window.Close(); return 0;
            }
            if (args.Length == 2 && args[0] == "--check") {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                Control<Button>("RefreshButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var frame = new DispatcherFrame();
                var deadline = DateTime.UtcNow.AddSeconds(25);
                var checkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                checkTimer.Tick += (s, e) => { if (!reading || DateTime.UtcNow > deadline) frame.Continue = false; };
                checkTimer.Start(); Dispatcher.PushFrame(frame); checkTimer.Stop();
                if (reading || !Control<Button>("RefreshButton").IsEnabled) throw new Exception("Refresh did not finish.");
                if (snapshot == null) throw new Exception("No local quota snapshot found.\r\n" + diagnostic);
                if (queryFailed || !snapshot.Live) throw new Exception("Active account query failed: " + queryError);
                var old = new Snapshot { Time = snapshot.Time.AddMinutes(-1), Hour = snapshot.Hour, Week = snapshot.Week };
                if (NewerSnapshot(snapshot, old) != snapshot) throw new Exception("Stale local record overwrote live query.");
                File.WriteAllText(args[1], string.Format("UI and embedded icon loaded successfully\r\nManual refresh completed a live account query\r\nStale local snapshot protection passed\r\nUpdated: {0:o}\r\n5-hour remaining: {1}%\r\nWeekly remaining: {2}%\r\n", snapshot.Time, snapshot.Hour.Remaining, snapshot.Week.Remaining));
                window.Close(); return 0;
            }
            window.Left = SystemParameters.WorkArea.Right - window.Width - 20;
            window.Top = SystemParameters.WorkArea.Bottom - window.Height - 20;
            var scanTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            scanTimer.Tick += (s, e) => Refresh(false);
            var clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            clockTimer.Tick += (s, e) => Render();
            window.Closed += (s, e) => { scanTimer.Stop(); clockTimer.Stop(); };
            window.Loaded += (s, e) => { Refresh(true); ShowWelcome(); if (!window.IsMouseOver) collapseTimer.Start(); };
            Render(); scanTimer.Start(); clockTimer.Start();
            new Application().Run(window);
            return 0;
        } catch (Exception ex) {
            if (checking) File.WriteAllText(args[1], ex.ToString());
            else MessageBox.Show(Text("无法启动额度窗口：", "Could not start usage widget: ") + ex.Message, Text("Codex 额度启动失败", "Codex Usage startup failed"));
            return 1;
        }
    }
}



