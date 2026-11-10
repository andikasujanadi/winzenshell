using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

public partial class BatteryFlyoutWindow : Window
{
    private record PowerPlanItem(string Guid, string Name, bool IsActive);

    private SolidColorBrush _accentBrush = new(Color.FromRgb(0x36, 0x84, 0xE3));
    private bool _isLightTheme;
    public DateTime LastDeactivatedTime { get; private set; } = DateTime.MinValue;

    private static readonly Regex PowerPlanRegex = new(
        @"Power Scheme GUID:\s*([0-9a-fA-F\-]{36})\s*\(([^)]+)\)\s*(\*)?",
        RegexOptions.Compiled);

    public BatteryFlyoutWindow()
    {
        InitializeComponent();

        Deactivated += (_, _) =>
        {
            FlyoutAnimationHelper.HandleDeactivated(this, () => LastDeactivatedTime = DateTime.UtcNow);
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

    public void UpdateStatus()
    {
        bool ok = Win32Api.GetSystemPowerStatus(out var sps);
        if (!ok)
        {
            TxtPercent.Text = "--%";
            TxtStatus.Text = "Unavailable";
            TxtDetails.Text = "Unable to query system power status.";
            BarBattery.Width = 0;
            EnergySaverBadge.Visibility = Visibility.Collapsed;
            return;
        }

        bool noBattery = (sps.BatteryFlag & 128) != 0 || sps.BatteryLifePercent == 255;
        bool isCharging = sps.ACLineStatus == 1 && (sps.BatteryFlag & 8) != 0;
        bool isPluggedIn = sps.ACLineStatus == 1;
        bool isEnergySaver = sps.SystemStatusFlag == 1;

        int pct = noBattery ? 100 : Math.Clamp((int)sps.BatteryLifePercent, 0, 100);
        TxtPercent.Text = noBattery ? "AC" : $"{pct}%";

        const double barMaxWidth = 248.0;
        BarBattery.Width = noBattery ? barMaxWidth : Math.Round((pct / 100.0) * barMaxWidth);

        EnergySaverBadge.Visibility = isEnergySaver ? Visibility.Visible : Visibility.Collapsed;

        Brush statusBrush = _accentBrush;
        if (noBattery)
        {
            TxtStatus.Text = "AC Power";
            TxtDetails.Text = "Plugged in (no battery detected)";
        }
        else if (isCharging)
        {
            TxtStatus.Text = "Charging";
            statusBrush = new SolidColorBrush(Color.FromRgb(0x34, 0xC7, 0x59));
            if (sps.BatteryFullLifeTime > 0 && sps.BatteryLifeTime > 0)
            {
                int remainingToFull = Math.Max(60, sps.BatteryFullLifeTime - sps.BatteryLifeTime);
                TxtDetails.Text = $"Plugged in • ~{FormatDuration(remainingToFull)} until full";
            }
            else
            {
                TxtDetails.Text = pct >= 99 ? "Plugged in • Nearly full" : "Plugged in and charging";
            }
        }
        else if (isPluggedIn)
        {
            TxtStatus.Text = pct >= 99 ? "Fully Charged" : "Plugged In";
            statusBrush = new SolidColorBrush(Color.FromRgb(0x34, 0xC7, 0x59));
            TxtDetails.Text = pct >= 99 ? "Battery is fully charged" : "Plugged in, not charging";
        }
        else
        {
            TxtStatus.Text = pct <= 15 ? "Low Battery" : "On Battery";
            if (pct <= 15)
            {
                statusBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            }
            if (sps.BatteryLifeTime > 0)
            {
                TxtDetails.Text = $"{FormatDuration(sps.BatteryLifeTime)} estimated remaining";
            }
            else
            {
                TxtDetails.Text = "Calculating remaining battery time...";
            }
        }

        TxtStatus.Foreground = statusBrush;
        BarBattery.Background = statusBrush;
    }

    private static string FormatDuration(int seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        int hours = (int)ts.TotalHours;
        int mins = ts.Minutes;
        if (hours > 0)
            return $"{hours}h {mins}m";
        return $"{Math.Max(1, mins)}m";
    }

    private async Task RefreshPowerPlansAsync()
    {
        var plans = await Task.Run(ReadPowerPlans);
        PowerPlansPanel.Children.Clear();

        if (plans.Count == 0)
        {
            PowerPlansPanel.Children.Add(new TextBlock
            {
                Text = "No power plans found",
                Foreground = (Brush)Resources["MutedFg"],
                FontSize = 11.5
            });
            return;
        }

        foreach (var plan in plans)
        {
            PowerPlansPanel.Children.Add(BuildPowerPlanRow(plan));
        }
    }

    private static List<PowerPlanItem> ReadPowerPlans()
    {
        var list = new List<PowerPlanItem>();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = "/list",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return list;
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(2000);

            foreach (Match m in PowerPlanRegex.Matches(output))
            {
                string guid = m.Groups[1].Value.Trim();
                string name = m.Groups[2].Value.Trim();
                bool isActive = m.Groups[3].Success;
                list.Add(new PowerPlanItem(guid, name, isActive));
            }
        }
        catch { }
        return list;
    }

    private Button BuildPowerPlanRow(PowerPlanItem plan)
    {
        Color rowBg = plan.IsActive
            ? (_isLightTheme ? Color.FromArgb(0x18, _accentBrush.Color.R, _accentBrush.Color.G, _accentBrush.Color.B)
                             : Color.FromArgb(0x28, _accentBrush.Color.R, _accentBrush.Color.G, _accentBrush.Color.B))
            : Colors.Transparent;
        Color hoverBg = _isLightTheme ? Color.FromArgb(0x0F, 0, 0, 0) : Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF);
        Brush textFg = new SolidColorBrush(_isLightTheme ? Color.FromRgb(0x1B, 0x1B, 0x1B) : Color.FromRgb(0xFF, 0xFF, 0xFF));
        Brush borderBrush = plan.IsActive ? _accentBrush : Brushes.Transparent;

        var contentGrid = new Grid();
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var txt = new TextBlock
        {
            Text = plan.Name,
            Foreground = textFg,
            FontSize = 12,
            FontWeight = plan.IsActive ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(txt, 0);
        contentGrid.Children.Add(txt);

        var dotOuter = new Ellipse
        {
            Width = 12,
            Height = 12,
            Stroke = plan.IsActive ? _accentBrush : new SolidColorBrush(_isLightTheme ? Color.FromRgb(0x88, 0x88, 0x88) : Color.FromRgb(0x78, 0x78, 0x78)),
            StrokeThickness = plan.IsActive ? 4 : 1.5,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(dotOuter, 1);
        contentGrid.Children.Add(dotOuter);

        var borderTemplate = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "RowBorder";
        borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(rowBg));
        borderFactory.SetValue(Border.BorderBrushProperty, borderBrush);
        borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(10, 7, 10, 7));

        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        borderFactory.AppendChild(cp);
        borderTemplate.VisualTree = borderFactory;

        if (!plan.IsActive)
        {
            var hoverTrigger = new Trigger { Property = IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverBg), "RowBorder"));
            borderTemplate.Triggers.Add(hoverTrigger);
        }

        var btn = new Button
        {
            Content = contentGrid,
            Template = borderTemplate,
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(0, 0, 0, 4)
        };
        btn.Click += async (_, _) =>
        {
            await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powercfg",
                        Arguments = $"/setactive {plan.Guid}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(2000);
                }
                catch { }
            });
            await RefreshPowerPlansAsync();
            UpdateStatus();
        };

        return btn;
    }

    public void ShowAt(double anchorScreenX, double anchorWidth, double taskbarTop, double taskbarHeight, string dockEdge, string? theme = null, string? accent = null)
    {
        ApplyAccentColor(accent);
        ApplyTheme(theme);
        UpdateStatus();
        UpdateLayout();

        double flyoutWidth = Width;
        double flyoutHeight = ActualHeight > 10 ? ActualHeight : 320;

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
        _ = RefreshPowerPlansAsync();
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
            if (IsVisible) UpdateStatus();
        }
        catch { }
    }

    public void ApplyTheme(string? theme)
    {
        _isLightTheme = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
        if (_isLightTheme)
        {
            Resources["CardBg"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            Resources["CardBorder"] = new SolidColorBrush(Color.FromArgb(0x18, 0, 0, 0));
            Resources["ProgressTrackBg"] = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xE5));
            Resources["BtnBg"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            Resources["BtnBorder"] = new SolidColorBrush(Color.FromArgb(0x28, 0, 0, 0));
            Resources["BtnFg"] = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));
            Resources["BtnHover"] = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
            Resources["MutedFg"] = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));

            OuterBorder.Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
            OuterBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x28, 0, 0, 0));
            TxtHeaderTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));
            TxtPercent.Foreground = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));
            LblPowerPlan.Foreground = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));
        }
        else
        {
            Resources["CardBg"] = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25));
            Resources["CardBorder"] = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38));
            Resources["ProgressTrackBg"] = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38));
            Resources["BtnBg"] = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26));
            Resources["BtnBorder"] = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38));
            Resources["BtnFg"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            Resources["BtnHover"] = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
            Resources["MutedFg"] = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0));

            OuterBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            OuterBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x3E));
            TxtHeaderTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            TxtPercent.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            LblPowerPlan.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        }

        if (HeaderIcon != null) HeaderIcon.Fill = _accentBrush;
        if (IsVisible) UpdateStatus();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        FlyoutAnimationHelper.AnimateHide(this);
    }

    private void OnOpenPowerSettingsClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:powersleep",
                UseShellExecute = true
            });
            Hide();
        }
        catch { }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
