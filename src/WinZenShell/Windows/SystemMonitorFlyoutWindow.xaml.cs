using System;
using System.Diagnostics;
using System.Windows;
using WinZenShell.Native;
using WinZenShell.Services;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace WinZenShell.Windows;

public partial class SystemMonitorFlyoutWindow : Window
{
    private readonly SystemMetricsService _metricsService;
    private readonly ShellStateService? _shellState;
    private SolidColorBrush _accentBrush = new(MediaColor.FromRgb(0x36, 0x84, 0xE3));
    private bool _isUpdatingCheckboxes;
    public DateTime LastDeactivatedTime { get; private set; } = DateTime.MinValue;

    public SystemMonitorFlyoutWindow(SystemMetricsService metricsService, ShellStateService? shellState = null)
    {
        InitializeComponent();
        _metricsService = metricsService;
        _shellState = shellState;

        if (_shellState != null)
        {
            _shellState.StateChanged += _ => Dispatcher.BeginInvoke(new Action(SyncStatCheckboxesFromState));
        }

        Deactivated += (_, _) =>
        {
            FlyoutAnimationHelper.HandleDeactivated(this, () => LastDeactivatedTime = DateTime.UtcNow);
        };
        Loaded += (_, _) =>
        {
            SyncStatCheckboxesFromState();
            UpdateTelemetry();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int ncrp = 1; // DWMNCRP_DISABLED
        Win32Api.DwmSetWindowAttribute(hwnd, 2, ref ncrp, sizeof(int));
        int borderColor = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
        Win32Api.DwmSetWindowAttribute(hwnd, 34, ref borderColor, sizeof(int));
        int cornerPref = 1; // DWMWCP_DONOTROUND
        Win32Api.DwmSetWindowAttribute(hwnd, 33, ref cornerPref, sizeof(int));
    }

    public void SyncStatCheckboxesFromState()
    {
        if (_shellState == null || ChkCpu == null || ChkRam == null || ChkGpu == null) return;
        var (showCpu, showRam, showGpu) = _shellState.GetSystemMonitorStats();
        _isUpdatingCheckboxes = true;
        try
        {
            ChkCpu.IsChecked = showCpu;
            ChkRam.IsChecked = showRam;
            ChkGpu.IsChecked = showGpu;
            UpdateCardActiveOpacity(showCpu, showRam, showGpu);
        }
        finally
        {
            _isUpdatingCheckboxes = false;
        }
    }

    private void UpdateCardActiveOpacity(bool showCpu, bool showRam, bool showGpu)
    {
        if (BarCpuTrack != null) BarCpuTrack.Opacity = showCpu ? 1.0 : 0.35;
        if (TxtCpuVal != null) TxtCpuVal.Opacity = showCpu ? 1.0 : 0.45;
        if (BarRamTrack != null) BarRamTrack.Opacity = showRam ? 1.0 : 0.35;
        if (TxtRamVal != null) TxtRamVal.Opacity = showRam ? 1.0 : 0.45;
        if (BarGpuTrack != null) BarGpuTrack.Opacity = showGpu ? 1.0 : 0.35;
        if (TxtGpuVal != null) TxtGpuVal.Opacity = showGpu ? 1.0 : 0.45;
    }

    private void OnStatCheckboxChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingCheckboxes || !IsLoaded) return;
        bool showCpu = ChkCpu?.IsChecked == true;
        bool showRam = ChkRam?.IsChecked == true;
        bool showGpu = ChkGpu?.IsChecked == true;
        UpdateCardActiveOpacity(showCpu, showRam, showGpu);
        _shellState?.UpdateSystemMonitorStats(showCpu, showRam, showGpu);
    }

    public void UpdateTelemetry(SystemMetricsSnapshot? snapshot = null)
    {
        snapshot ??= _metricsService.GetSnapshot(IntPtr.Zero, IntPtr.Zero);

        int cpu = Math.Clamp(snapshot.CpuPercent, 0, 100);
        int ram = Math.Clamp(snapshot.MemoryPercent, 0, 100);
        int gpu = Math.Clamp(snapshot.GpuPercent, 0, 100);

        TxtCpuVal.Text = $"{cpu}%";
        TxtRamVal.Text = $"{ram}%";
        TxtGpuVal.Text = $"{gpu}%";

        double barMaxWidth = BarCpuTrack != null && BarCpuTrack.ActualWidth > 40 ? BarCpuTrack.ActualWidth : 244.0;
        BarCpu.Width = Math.Round((cpu / 100.0) * barMaxWidth);
        BarRam.Width = Math.Round((ram / 100.0) * barMaxWidth);
        BarGpu.Width = Math.Round((gpu / 100.0) * barMaxWidth);

        TxtRamDetails.Text = $"{snapshot.MemoryUsedGb:0.0} GB of {snapshot.MemoryTotalGb:0.0} GB in use";
    }

    public void ShowAt(double anchorScreenX, double anchorWidth, double taskbarTop, double taskbarHeight, string dockEdge, string? theme = null, string? accent = null)
    {
        ApplyAccentColor(accent);
        ApplyTheme(theme);
        SyncStatCheckboxesFromState();
        UpdateTelemetry();

        double flyoutWidth = Width;
        double flyoutHeight = Height;

        double left = anchorScreenX + (anchorWidth / 2.0) - (flyoutWidth / 2.0);
        const double floatingGap = 10.0;
        double top = string.Equals(dockEdge, "Top", StringComparison.OrdinalIgnoreCase)
            ? taskbarTop + taskbarHeight + floatingGap
            : taskbarTop - flyoutHeight - floatingGap;

        var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;
        double monitorLeft = mon.Left;
        double monitorRight = mon.Right;
        const double sideMargin = 12.0;

        if (left + flyoutWidth > monitorRight - sideMargin)
            left = monitorRight - flyoutWidth - sideMargin;
        if (left < monitorLeft + sideMargin)
            left = monitorLeft + sideMargin;

        Left = left;
        FlyoutAnimationHelper.AnimateShow(this, top, dockEdge);
    }

    public void ApplyAccentColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        try
        {
            var color = (MediaColor)MediaColorConverter.ConvertFromString(hex);
            _accentBrush = new SolidColorBrush(color);
            Resources["AccentBrush"] = _accentBrush;
            if (HeaderChipIcon != null) HeaderChipIcon.Fill = _accentBrush;
            if (TxtCpuVal != null) TxtCpuVal.Foreground = _accentBrush;
            if (BarCpu != null) BarCpu.Background = _accentBrush;
        }
        catch { }
    }

    public void ApplyTheme(string? theme)
    {
        bool isLight = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
        if (isLight)
        {
            Resources["ProgressTrackBg"] = new SolidColorBrush(MediaColor.FromRgb(0xE5, 0xE5, 0xE5));
            Resources["TaskMgrBtnBg"] = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
            Resources["TaskMgrBtnBorder"] = new SolidColorBrush(MediaColor.FromArgb(0x28, 0, 0, 0));
            Resources["TaskMgrBtnFg"] = new SolidColorBrush(MediaColor.FromRgb(0x1B, 0x1B, 0x1B));
            Resources["TaskMgrBtnHover"] = new SolidColorBrush(MediaColor.FromRgb(0xF0, 0xF0, 0xF0));
            Resources["CheckBoxBg"] = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
            Resources["CheckBoxBorder"] = new SolidColorBrush(MediaColor.FromArgb(0x48, 0, 0, 0));

            OuterBorder.Background = new SolidColorBrush(MediaColor.FromRgb(0xF3, 0xF3, 0xF3));
            OuterBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(0x28, 0x00, 0x00, 0x00));
            CpuCard.Background = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
            CpuCard.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(0x18, 0x00, 0x00, 0x00));
            RamCard.Background = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
            RamCard.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(0x18, 0x00, 0x00, 0x00));
            GpuCard.Background = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
            GpuCard.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(0x18, 0x00, 0x00, 0x00));
            TxtHeaderTitle.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x1B, 0x1B, 0x1B));
            LblCpu.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x1B, 0x1B, 0x1B));
            LblRam.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x1B, 0x1B, 0x1B));
            LblGpu.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x1B, 0x1B, 0x1B));
            TxtRamDetails.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x66, 0x66, 0x66));
            TxtGpuVal.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x05, 0x96, 0x69));
            BarGpu.Background = new SolidColorBrush(MediaColor.FromRgb(0x05, 0x96, 0x69));
            TxtCpuVal.Foreground = _accentBrush;
            BarCpu.Background = _accentBrush;
        }
        else
        {
            Resources["ProgressTrackBg"] = new SolidColorBrush(MediaColor.FromRgb(0x38, 0x38, 0x38));
            Resources["TaskMgrBtnBg"] = new SolidColorBrush(MediaColor.FromRgb(0x26, 0x26, 0x26));
            Resources["TaskMgrBtnBorder"] = new SolidColorBrush(MediaColor.FromRgb(0x38, 0x38, 0x38));
            Resources["TaskMgrBtnFg"] = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
            Resources["TaskMgrBtnHover"] = new SolidColorBrush(MediaColor.FromRgb(0x33, 0x33, 0x33));
            Resources["CheckBoxBg"] = new SolidColorBrush(MediaColor.FromRgb(0x2A, 0x2A, 0x2A));
            Resources["CheckBoxBorder"] = new SolidColorBrush(MediaColor.FromRgb(0x58, 0x58, 0x58));

            OuterBorder.Background = new SolidColorBrush(MediaColor.FromRgb(0x1E, 0x1E, 0x1E));
            OuterBorder.BorderBrush = new SolidColorBrush(MediaColor.FromRgb(0x3E, 0x3E, 0x3E));
            CpuCard.Background = new SolidColorBrush(MediaColor.FromRgb(0x25, 0x25, 0x25));
            CpuCard.BorderBrush = new SolidColorBrush(MediaColor.FromRgb(0x38, 0x38, 0x38));
            RamCard.Background = new SolidColorBrush(MediaColor.FromRgb(0x25, 0x25, 0x25));
            RamCard.BorderBrush = new SolidColorBrush(MediaColor.FromRgb(0x38, 0x38, 0x38));
            GpuCard.Background = new SolidColorBrush(MediaColor.FromRgb(0x25, 0x25, 0x25));
            GpuCard.BorderBrush = new SolidColorBrush(MediaColor.FromRgb(0x38, 0x38, 0x38));
            TxtHeaderTitle.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
            LblCpu.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
            LblRam.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
            LblGpu.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
            TxtRamDetails.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xA0, 0xA0, 0xA0));
            TxtGpuVal.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x34, 0xD3, 0x99));
            BarGpu.Background = new SolidColorBrush(MediaColor.FromRgb(0x34, 0xD3, 0x99));
            TxtCpuVal.Foreground = _accentBrush;
            BarCpu.Background = _accentBrush;
        }
        if (HeaderChipIcon != null) HeaderChipIcon.Fill = _accentBrush;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        FlyoutAnimationHelper.AnimateHide(this);
    }

    private void OnOpenTaskMgrClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "taskmgr.exe",
                UseShellExecute = true
            });
            FlyoutAnimationHelper.AnimateHide(this);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open Task Manager: {ex.Message}");
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
