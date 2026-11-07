using System;
using System.Windows;
using WinZenShell.Native;
using WinZenShell.Services;

namespace WinZenShell.Windows;

public partial class VolumeFlyoutWindow : Window
{
    private readonly AudioService _audioService;
    private bool _isUpdatingUi = true;
    private bool _isLightTheme = false;
    public DateTime LastDeactivatedTime { get; private set; } = DateTime.MinValue;

    public VolumeFlyoutWindow(AudioService audioService)
    {
        _audioService = audioService;
        InitializeComponent();
        _isUpdatingUi = false;

        Deactivated += (_, _) =>
        {
            LastDeactivatedTime = DateTime.UtcNow;
            Hide();
        };
    }

    public void ShowAt(double anchorScreenX, double anchorWidth, double taskbarTop, double taskbarHeight, string dockEdge, string? theme = null, string? accent = null)
    {
        ApplyTheme(theme);
        ApplyAccentColor(accent);
        UpdateVolumeDisplay();

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
            var brush = new System.Windows.Media.SolidColorBrush(color);
            Resources["AccentBrush"] = brush;
            if (SpeakerPath != null && _audioService != null && !_audioService.GetMute() && _audioService.GetVolume() > 0)
            {
                SpeakerPath.Fill = brush;
            }
        }
        catch { }
    }

    private const string PathHigh = "M11.536 14.01A8.473 8.473 0 0 0 14.026 8a8.47 8.47 0 0 0-2.49-6.01l-.708.707A7.476 7.476 0 0 1 13.025 8c0 2.071-.84 3.946-2.197 5.303l.708.707zM10.121 12.6A6.48 6.48 0 0 0 12.025 8a6.48 6.48 0 0 0-1.904-4.596l-.707.707A5.482 5.482 0 0 1 11.025 8a5.483 5.483 0 0 1-1.61 3.89l.706.71zM8.707 11.185A4.486 4.486 0 0 0 10.025 8a4.486 4.486 0 0 0-1.318-3.185l-.707.707A3.49 3.49 0 0 1 9.025 8a3.49 3.49 0 0 1-1.025 2.478l.707.707zM6.717 3.55A.5.5 0 0 1 7 4v8a.5.5 0 0 1-.812.39L3.825 10.5H1.5A.5.5 0 0 1 1 10V6a.5.5 0 0 1 .5-.5h2.325l2.363-1.89a.5.5 0 0 1 .529-.06z";
    private const string PathMed = "M10.121 12.6A6.48 6.48 0 0 0 12.025 8a6.48 6.48 0 0 0-1.904-4.596l-.707.707A5.482 5.482 0 0 1 11.025 8a5.483 5.483 0 0 1-1.61 3.89l.706.71zM8.707 11.185A4.486 4.486 0 0 0 10.025 8a4.486 4.486 0 0 0-1.318-3.185l-.707.707A3.49 3.49 0 0 1 9.025 8a3.49 3.49 0 0 1-1.025 2.478l.707.707zM6.717 3.55A.5.5 0 0 1 7 4v8a.5.5 0 0 1-.812.39L3.825 10.5H1.5A.5.5 0 0 1 1 10V6a.5.5 0 0 1 .5-.5h2.325l2.363-1.89a.5.5 0 0 1 .529-.06z";
    private const string PathLow = "M8.707 11.185A4.486 4.486 0 0 0 10.025 8a4.486 4.486 0 0 0-1.318-3.185l-.707.707A3.49 3.49 0 0 1 9.025 8a3.49 3.49 0 0 1-1.025 2.478l.707.707zM6.717 3.55A.5.5 0 0 1 7 4v8a.5.5 0 0 1-.812.39L3.825 10.5H1.5A.5.5 0 0 1 1 10V6a.5.5 0 0 1 .5-.5h2.325l2.363-1.89a.5.5 0 0 1 .529-.06z";
    private const string PathMute = "M6.717 3.55A.5.5 0 0 1 7 4v8a.5.5 0 0 1-.812.39L3.825 10.5H1.5A.5.5 0 0 1 1 10V6a.5.5 0 0 1 .5-.5h2.325l2.363-1.89a.5.5 0 0 1 .529-.06zm7.137 2.096a.5.5 0 0 1 0 .708L12.207 8l1.647 1.646a.5.5 0 0 1-.708.708L11.5 8.707l-1.646 1.647a.5.5 0 0 1-.708-.708L10.793 8 9.146 6.354a.5.5 0 1 1 .708-.708L11.5 7.293l1.646-1.647a.5.5 0 0 1 .708 0z";

    public void UpdateVolume(int volumePercent, bool isMuted)
    {
        _isUpdatingUi = true;
        if (VolumeSlider != null)
        {
            VolumeSlider.Value = volumePercent;
        }

        if (PercentLabel != null)
        {
            if (isMuted || volumePercent == 0)
            {
                PercentLabel.Text = isMuted ? "Mute" : "0%";
                PercentLabel.Foreground = System.Windows.Media.Brushes.Gray;
            }
            else
            {
                PercentLabel.Text = $"{volumePercent}%";
                PercentLabel.Foreground = _isLightTheme
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1B, 0x1B, 0x1B))
                    : System.Windows.Media.Brushes.White;
            }
        }

        if (SpeakerPath != null)
        {
            if (isMuted || volumePercent == 0)
            {
                SpeakerPath.Data = System.Windows.Media.Geometry.Parse(PathMute);
                SpeakerPath.Fill = System.Windows.Media.Brushes.Gray;
            }
            else
            {
                string p = volumePercent > 66 ? PathHigh : (volumePercent > 33 ? PathMed : PathLow);
                SpeakerPath.Data = System.Windows.Media.Geometry.Parse(p);
                SpeakerPath.Fill = Resources["AccentBrush"] as System.Windows.Media.Brush
                    ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x36, 0x84, 0xE3));
            }
        }

        _isUpdatingUi = false;
    }

    private void UpdateVolumeDisplay()
    {
        if (_audioService == null) return;
        int current = _audioService.GetVolume();
        bool muted = _audioService.GetMute();
        UpdateVolume(current, muted);
    }

    private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi || _audioService == null || PercentLabel == null) return;
        int val = (int)Math.Round(e.NewValue);
        _audioService.SetVolume(val);
        PercentLabel.Text = $"{val}%";
        PercentLabel.Foreground = _isLightTheme
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1B, 0x1B, 0x1B))
            : System.Windows.Media.Brushes.White;
    }

    private void OnMuteClicked(object sender, RoutedEventArgs e)
    {
        _audioService.ToggleMute();
        UpdateVolumeDisplay();
    }

    private void OnPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        e.Handled = true;
        int step = e.Delta > 0 ? 3 : -3;
        int current = _audioService.GetVolume();
        int next = Math.Clamp(current + step, 0, 100);
        _audioService.SetVolume(next);
        UpdateVolume(next, false);
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
