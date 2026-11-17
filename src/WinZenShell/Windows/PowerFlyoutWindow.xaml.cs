using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WinZenShell.Native;
using Button = System.Windows.Controls.Button;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace WinZenShell.Windows;

public partial class PowerFlyoutWindow : Window
{
    private SolidColorBrush _accentBrush = new(Color.FromRgb(0x36, 0x84, 0xE3));
    private bool _isLightTheme;
    public DateTime LastDeactivatedTime { get; private set; } = DateTime.MinValue;

    public PowerFlyoutWindow()
    {
        InitializeComponent();

        Deactivated += (_, _) =>
        {
            FlyoutAnimationHelper.HandleDeactivated(this, () => LastDeactivatedTime = DateTime.UtcNow);
        };

        BuildActions();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int ncrp = 1;
        Win32Api.DwmSetWindowAttribute(hwnd, 2, ref ncrp, sizeof(int));
        int borderColor = unchecked((int)0xFFFFFFFE);
        Win32Api.DwmSetWindowAttribute(hwnd, 34, ref borderColor, sizeof(int));
        int cornerPref = 1;
        Win32Api.DwmSetWindowAttribute(hwnd, 33, ref cornerPref, sizeof(int));
    }

    private void BuildActions()
    {
        PowerActionsPanel.Children.Clear();

        PowerActionsPanel.Children.Add(CreateActionButton(
            "Sleep",
            "M12 3a9 9 0 1 0 9 9c0-.46-.04-.92-.1-1.36a5.389 5.389 0 0 1-4.4 2.26 5.403 5.403 0 0 1-3.14-9.8c-.44-.06-.9-.1-1.36-.1z",
            isDanger: false,
            onClick: ExecuteSleep));

        PowerActionsPanel.Children.Add(CreateActionButton(
            "Lock",
            "M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1 1.71 0 3.1 1.39 3.1 3.1v2z",
            isDanger: false,
            onClick: ExecuteLock));

        PowerActionsPanel.Children.Add(CreateActionButton(
            "Restart",
            "M17.65 6.35A7.958 7.958 0 0 0 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08A5.99 5.99 0 0 1 12 18c-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z",
            isDanger: false,
            onClick: ExecuteRestart));

        PowerActionsPanel.Children.Add(CreateActionButton(
            "Shutdown",
            "M13 3h-2v10h2V3zm4.83 2.17l-1.42 1.42A6.92 6.92 0 0 1 19 12c0 3.87-3.13 7-7 7s-7-3.13-7-7c0-2.27 1.09-4.29 2.77-5.58L6.35 5.01A8.932 8.932 0 0 0 3 12c0 4.97 4.03 9 9 9s9-4.03 9-9c0-2.74-1.23-5.18-3.17-6.83z",
            isDanger: true,
            onClick: ExecuteShutdown));
    }

    private Button CreateActionButton(string label, string iconPathData, bool isDanger, Action onClick)
    {
        Color hoverBg = _isLightTheme ? Color.FromArgb(0x0F, 0, 0, 0) : Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF);
        Brush fg = isDanger
            ? (Brush)Resources["DangerFg"]
            : new SolidColorBrush(_isLightTheme ? Color.FromRgb(0x1B, 0x1B, 0x1B) : Color.FromRgb(0xF3, 0xF3, 0xF3));

        var row = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var icon = new Path
        {
            Data = Geometry.Parse(iconPathData),
            Fill = fg,
            Width = 15,
            Height = 15,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var text = new TextBlock
        {
            Text = label,
            Foreground = fg,
            FontSize = 13,
            FontWeight = FontWeights.Medium,
            VerticalAlignment = VerticalAlignment.Center
        };

        row.Children.Add(icon);
        row.Children.Add(text);

        var template = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "RowBorder";
        borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(10, 9, 10, 9));

        var presenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        presenterFactory.SetValue(HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Stretch);
        presenterFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(presenterFactory);
        template.VisualTree = borderFactory;

        var hoverTrigger = new Trigger { Property = IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverBg), "RowBorder"));
        template.Triggers.Add(hoverTrigger);

        var btn = new Button
        {
            Content = row,
            Template = template,
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 1, 0, 1)
        };
        btn.Click += (_, _) =>
        {
            Hide();
            onClick();
        };

        return btn;
    }

    private static void ExecuteSleep()
    {
        try
        {
            Win32Api.SetSuspendState(false, false, false);
        }
        catch { }
    }

    private static void ExecuteLock()
    {
        try
        {
            Win32Api.LockWorkStation();
        }
        catch { }
    }

    private static void ExecuteRestart()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = "/r /t 0",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch { }
    }

    private static void ExecuteShutdown()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = "/s /t 0",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch { }
    }

    public void ShowAt(double anchorScreenLeft, double anchorWidth, double taskbarTop, double taskbarHeight, string dockEdge, string? theme = null, string? accent = null)
    {
        ApplyAccentColor(accent);
        ApplyTheme(theme);
        BuildActions();
        UpdateLayout();

        double flyoutWidth = Width;
        double flyoutHeight = ActualHeight > 10 ? ActualHeight : 0;
        if (flyoutHeight <= 10)
        {
            OuterBorder.Measure(new System.Windows.Size(flyoutWidth, double.PositiveInfinity));
            flyoutHeight = OuterBorder.DesiredSize.Height > 10 ? OuterBorder.DesiredSize.Height : 204;
        }

        double left = anchorScreenLeft + (anchorWidth / 2.0) - (flyoutWidth / 2.0);
        const double floatingGap = 10.0;
        double top = string.Equals(dockEdge, "Top", StringComparison.OrdinalIgnoreCase)
            ? taskbarTop + taskbarHeight + floatingGap
            : taskbarTop - flyoutHeight - floatingGap;

        var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;
        double dpiX = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (dpiX <= 0) dpiX = 1.0;
        double monitorLeft = mon.Left / dpiX;
        double monitorRight = mon.Right / dpiX;
        const double sideMargin = 12.0;

        if (left + flyoutWidth > monitorRight - sideMargin)
            left = monitorRight - flyoutWidth - sideMargin;
        if (left < monitorLeft + sideMargin)
            left = monitorLeft + sideMargin;

        Left = left;
        FlyoutAnimationHelper.AnimateShow(this, top, dockEdge);
    }

    public void ShowAtExact(double exactLeft, double exactTop, string dockEdge, string? theme = null, string? accent = null)
    {
        ApplyAccentColor(accent);
        ApplyTheme(theme);
        BuildActions();
        UpdateLayout();

        Left = exactLeft;

        double finalTop = exactTop;
        if (!string.Equals(dockEdge, "Top", StringComparison.OrdinalIgnoreCase))
        {
            double flyoutH = ActualHeight > 10 ? ActualHeight : 190;
            var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;
            double dpiY = VisualTreeHelper.GetDpi(this).DpiScaleY;
            if (dpiY <= 0) dpiY = 1.0;
            double monitorBottom = mon.Bottom / dpiY;
            finalTop = monitorBottom - flyoutH - 10.0;
        }

        Top = finalTop;
        FlyoutAnimationHelper.AnimateShow(this, finalTop, dockEdge);
    }

    public void ApplyAccentColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            _accentBrush = new SolidColorBrush(color);
            Resources["AccentBrush"] = _accentBrush;
            if (HeaderIcon != null) HeaderIcon.Fill = _accentBrush;
        }
        catch { }
    }

    public void ApplyTheme(string? theme)
    {
        _isLightTheme = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
        if (_isLightTheme)
        {
            Resources["BtnFg"] = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));
            Resources["BtnHover"] = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
            Resources["MutedFg"] = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
            Resources["DangerFg"] = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));

            OuterBorder.Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
            OuterBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x28, 0, 0, 0));
            TxtHeaderTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));
        }
        else
        {
            Resources["BtnFg"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            Resources["BtnHover"] = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x2E));
            Resources["MutedFg"] = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0));
            Resources["DangerFg"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x95));

            OuterBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            OuterBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x3E));
            TxtHeaderTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        }

        if (HeaderIcon != null) HeaderIcon.Fill = _accentBrush;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
