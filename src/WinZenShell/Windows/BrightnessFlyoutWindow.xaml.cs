using System;
using System.Windows;
using WinZenShell.Native;
using WinZenShell.Services;

namespace WinZenShell.Windows;

public partial class BrightnessFlyoutWindow : Window
{
    private readonly BrightnessService _brightnessService;
    private bool _isUpdatingUi;
    private bool _isLightTheme;
    public DateTime LastDeactivatedTime { get; private set; } = DateTime.MinValue;

    public BrightnessFlyoutWindow(BrightnessService brightnessService)
    {
        InitializeComponent();
        _brightnessService = brightnessService;

        Deactivated += (_, _) =>
        {
            LastDeactivatedTime = DateTime.UtcNow;
            Hide();
        };
        BrightnessSlider.ValueChanged += OnSliderValueChanged;
    }

    public void ShowAt(double anchorScreenX, double anchorWidth, double taskbarTop, double taskbarHeight, string dockEdge, string? theme = null, string? accent = null)
    {
        ApplyTheme(theme);
        ApplyAccentColor(accent);
        _isUpdatingUi = true;
        int current = _brightnessService.GetBrightness();
        BrightnessSlider.Value = current;
        PercentLabel.Text = $"{current}%";
        _isUpdatingUi = false;

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

    public void ApplyTheme(string? theme)
    {
        _isLightTheme = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
        if (_isLightTheme)
        {
            OuterBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF3, 0xF3, 0xF3));
            OuterBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x28, 0x00, 0x00, 0x00));
            InnerBorder.Background = System.Windows.Media.Brushes.Transparent;
            InnerBorder.BorderThickness = new Thickness(0);
            if (PercentLabel != null)
                PercentLabel.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1B, 0x1B, 0x1B));
        }
        else
        {
            OuterBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
            OuterBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0x3E, 0x3E));
            InnerBorder.Background = System.Windows.Media.Brushes.Transparent;
            InnerBorder.BorderThickness = new Thickness(0);
            if (PercentLabel != null)
                PercentLabel.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
        }
    }

    public void ApplyAccentColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            Resources["AccentBrush"] = new System.Windows.Media.SolidColorBrush(color);
        }
        catch { }
    }

    public void UpdateBrightness(int percent)
    {
        _isUpdatingUi = true;
        if (BrightnessSlider != null && !BrightnessSlider.IsMouseCaptured)
        {
            BrightnessSlider.Value = percent;
        }
        if (PercentLabel != null && (BrightnessSlider == null || !BrightnessSlider.IsMouseCaptured))
        {
            PercentLabel.Text = $"{percent}%";
            PercentLabel.Foreground = _isLightTheme
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1B, 0x1B, 0x1B))
                : System.Windows.Media.Brushes.White;
        }
        _isUpdatingUi = false;
    }

    private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi) return;
        int val = (int)Math.Round(e.NewValue);
        PercentLabel.Text = $"{val}%";
        PercentLabel.Foreground = _isLightTheme
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1B, 0x1B, 0x1B))
            : System.Windows.Media.Brushes.White;
        _brightnessService.SetBrightness(val);
    }

    private void OnPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        e.Handled = true;
        int step = e.Delta > 0 ? 3 : -3;
        int current = _brightnessService.GetBrightness();
        int next = Math.Clamp(current + step, 0, 100);
        _brightnessService.SetBrightness(next);
        UpdateBrightness(next);
    }

    internal void UpdateSliderValueFromMouse(System.Windows.Controls.Slider slider, System.Windows.Point pt)
    {
        double thumbOffset = 11.0;
        double trackWidth = slider.ActualWidth - (thumbOffset * 2);
        if (trackWidth > 0)
        {
            double ratio = Math.Clamp((pt.X - thumbOffset) / trackWidth, 0.0, 1.0);
            int val = (int)Math.Round(slider.Minimum + (ratio * (slider.Maximum - slider.Minimum)));
            slider.Value = val;
        }
    }

    private void OnSliderPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Slider slider)
        {
            var pt = e.GetPosition(slider);
            UpdateSliderValueFromMouse(slider, pt);
            slider.CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnSliderPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.Slider slider && slider.IsMouseCaptured && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var pt = e.GetPosition(slider);
            UpdateSliderValueFromMouse(slider, pt);
            e.Handled = true;
        }
    }

    private void OnSliderPreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Slider slider && slider.IsMouseCaptured)
        {
            slider.ReleaseMouseCapture();
            e.Handled = true;
        }
    }
}
