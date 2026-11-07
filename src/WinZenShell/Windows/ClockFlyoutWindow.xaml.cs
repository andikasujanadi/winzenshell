using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinZenShell.Native;

namespace WinZenShell.Windows;

public partial class ClockFlyoutWindow : Window
{
    private readonly DispatcherTimer _tickTimer;
    private SolidColorBrush _accentBrush = new(System.Windows.Media.Color.FromRgb(0x36, 0x84, 0xE3));

    public DateTime LastDeactivatedTime { get; private set; } = DateTime.MinValue;

    public ClockFlyoutWindow()
    {
        InitializeComponent();

        DrawClockTicks();

        _tickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _tickTimer.Tick += (_, _) => UpdateClock();

        Deactivated += (_, _) =>
        {
            LastDeactivatedTime = DateTime.UtcNow;
            Hide();
        };
        Loaded += (_, _) => UpdateClock();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int ncrp = 1; // DWMNCRP_DISABLED
        Native.Win32Api.DwmSetWindowAttribute(hwnd, 2, ref ncrp, sizeof(int));
        int borderColor = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
        Native.Win32Api.DwmSetWindowAttribute(hwnd, 34, ref borderColor, sizeof(int));
        int cornerPref = 1; // DWMWCP_DONOTROUND
        Native.Win32Api.DwmSetWindowAttribute(hwnd, 33, ref cornerPref, sizeof(int));
    }

    private void DrawClockTicks()
    {
        TickCanvas.Children.Clear();
        const double cx = 80;
        const double cy = 80;
        const double radius = 70;

        for (int i = 0; i < 12; i++)
        {
            // Skip ticks where numerals 12, 3, 6, 9 are rendered
            if (i % 3 == 0) continue;

            double angleDeg = i * 30.0;
            double angleRad = (angleDeg - 90.0) * Math.PI / 180.0;

            double tickLength = 6.0;
            double thickness = 2.0;
            var color = System.Windows.Media.Color.FromRgb(200, 200, 200);

            double xOuter = cx + (radius * Math.Cos(angleRad));
            double yOuter = cy + (radius * Math.Sin(angleRad));
            double xInner = cx + ((radius - tickLength) * Math.Cos(angleRad));
            double yInner = cy + ((radius - tickLength) * Math.Sin(angleRad));

            var line = new Line
            {
                X1 = xOuter,
                Y1 = yOuter,
                X2 = xInner,
                Y2 = yInner,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            TickCanvas.Children.Add(line);
        }
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;

        // Smooth hands or second step
        double sec = now.Second + (now.Millisecond / 1000.0);
        double min = now.Minute + (sec / 60.0);
        double hr = (now.Hour % 12) + (min / 60.0);

        SecondHandTransform.Angle = sec * 6.0;
        MinuteHandTransform.Angle = min * 6.0;
        HourHandTransform.Angle = hr * 30.0;

        TxtDigitalTime.Text = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        TxtDigitalDate.Text = now.ToString("dddd, MMMM d, yyyy");

        var tz = TimeZoneInfo.Local;
        TimeSpan offset = tz.GetUtcOffset(now);
        string offsetStr = (offset >= TimeSpan.Zero ? "+" : "-") + offset.ToString(@"hh\:mm");
        TxtTimezone.Text = $"UTC{offsetStr} • {tz.StandardName}";
    }

    public void ShowAt(double anchorScreenX, double anchorWidth, double taskbarTop, double taskbarHeight, string dockEdge, string? theme = null, string? accent = null)
    {
        ApplyAccentColor(accent);
        ApplyTheme(theme);
        UpdateClock();
        if (!_tickTimer.IsEnabled)
        {
            _tickTimer.Start();
        }

        double flyoutWidth = Width;
        double flyoutHeight = Height;

        double left = anchorScreenX + (anchorWidth / 2.0) - (flyoutWidth / 2.0);
        const double floatingGap = 10.0;
        double top;

        if (string.Equals(dockEdge, "Top", StringComparison.OrdinalIgnoreCase))
        {
            top = taskbarTop + taskbarHeight + floatingGap;
        }
        else
        {
            top = taskbarTop - flyoutHeight - floatingGap;
        }

        var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;
        double monitorLeft = mon.Left;
        double monitorRight = mon.Right;
        const double sideMargin = 12.0;

        if (left + flyoutWidth > monitorRight - sideMargin)
            left = monitorRight - flyoutWidth - sideMargin;
        if (left < monitorLeft + sideMargin)
            left = monitorLeft + sideMargin;

        Left = left;
        Top = top;

        Show();
        Activate();
        Focus();
    }

    public void ApplyAccentColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            _accentBrush = new SolidColorBrush(color);
            Resources["AccentBrush"] = _accentBrush;
            if (HeaderIconPath != null) HeaderIconPath.Fill = _accentBrush;
            if (SecondHandLine != null) SecondHandLine.Stroke = _accentBrush;
            if (CenterPivotOuter != null) CenterPivotOuter.Fill = _accentBrush;
            if (TxtDigitalDate != null) TxtDigitalDate.Foreground = _accentBrush;
        }
        catch { }
    }

    public void ApplyTheme(string? theme)
    {
        bool isLight = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
        if (isLight)
        {
            OuterBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF3, 0xF3, 0xF3));
            OuterBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x28, 0x00, 0x00, 0x00));
            TxtHeaderTitle.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1B, 0x1B, 0x1B));
            DigitalCard.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
            DigitalCard.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x18, 0x00, 0x00, 0x00));
            TxtDigitalTime.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1B, 0x1B, 0x1B));
            TxtDigitalDate.Foreground = _accentBrush;
            TzBadge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEA, 0xEA, 0xEA));
            TxtTimezone.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x5C, 0x5C, 0x5C));
        }
        else
        {
            OuterBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
            OuterBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0x3E, 0x3E));
            TxtHeaderTitle.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
            DigitalCard.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x26, 0x26, 0x26));
            DigitalCard.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x38, 0x38, 0x38));
            TxtDigitalTime.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
            TxtDigitalDate.Foreground = _accentBrush;
            TzBadge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0x22, 0x22));
            TxtTimezone.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xA4, 0xA4, 0xA4));
        }
        if (HeaderIconPath != null) HeaderIconPath.Fill = _accentBrush;
        if (SecondHandLine != null) SecondHandLine.Stroke = _accentBrush;
        if (CenterPivotOuter != null) CenterPivotOuter.Fill = _accentBrush;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Don't dispose on close, just hide
        e.Cancel = true;
        Hide();
    }
}
