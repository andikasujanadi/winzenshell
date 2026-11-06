using System;
using System.Diagnostics;
using System.Windows;
using WinZenShell.Native;
using WinZenShell.Services;

namespace WinZenShell.Windows;

public partial class SystemMonitorFlyoutWindow : Window
{
    private readonly SystemMetricsService _metricsService;
    public DateTime LastDeactivatedTime { get; private set; } = DateTime.MinValue;

    public SystemMonitorFlyoutWindow(SystemMetricsService metricsService)
    {
        InitializeComponent();
        _metricsService = metricsService;

        Deactivated += (_, _) =>
        {
            LastDeactivatedTime = DateTime.UtcNow;
            Hide();
        };
        Loaded += (_, _) => UpdateTelemetry();
    }

    public void UpdateTelemetry(SystemMetricsSnapshot? snapshot = null)
    {
        snapshot ??= _metricsService.GetSnapshot(IntPtr.Zero, IntPtr.Zero);

        int cpu = Math.Clamp(snapshot.CpuPercent, 0, 100);
        int ram = Math.Clamp(snapshot.MemoryPercent, 0, 100);

        TxtCpuVal.Text = $"{cpu}%";
        TxtRamVal.Text = $"{ram}%";

        // Progress bar width inside a 238px wide content container
        const double barMaxWidth = 238.0;
        BarCpu.Width = Math.Round((cpu / 100.0) * barMaxWidth);
        BarRam.Width = Math.Round((ram / 100.0) * barMaxWidth);

        TxtRamDetails.Text = $"{snapshot.MemoryUsedGb:0.0} GB of {snapshot.MemoryTotalGb:0.0} GB in use";
    }

    public void ShowAt(double anchorScreenX, double anchorWidth, double taskbarTop, double taskbarHeight, string dockEdge, string? theme = null)
    {
        ApplyTheme(theme);
        UpdateTelemetry();

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
        bool isLight = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
        if (isLight)
        {
            OuterBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF3, 0xF3, 0xF3));
            OuterBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x28, 0x00, 0x00, 0x00));
            CpuCard.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
            CpuCard.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x18, 0x00, 0x00, 0x00));
            RamCard.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
            RamCard.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x18, 0x00, 0x00, 0x00));
            TxtHeaderTitle.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1B, 0x1B, 0x1B));
            LblCpu.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1B, 0x1B, 0x1B));
            LblRam.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1B, 0x1B, 0x1B));
            TxtCpuVal.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x67, 0xC0));
            BarCpu.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x67, 0xC0));
        }
        else
        {
            OuterBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
            OuterBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0x3E, 0x3E));
            CpuCard.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x25, 0x25, 0x25));
            CpuCard.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x38, 0x38, 0x38));
            RamCard.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x25, 0x25, 0x25));
            RamCard.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x38, 0x38, 0x38));
            TxtHeaderTitle.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
            LblCpu.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
            LblRam.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
            TxtCpuVal.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x60, 0xCD, 0xFF));
            BarCpu.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x60, 0xCD, 0xFF));
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Hide();
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
            Hide();
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
