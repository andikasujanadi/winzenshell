using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using WinZenShell.Native;
using WinZenShell.Services;

namespace WinZenShell.Windows;

public partial class ControlCenterFlyoutWindow : Window
{
    private readonly ShellStateService _shellState;
    private readonly SystemMetricsService _metricsService;
    private readonly BrightnessService _brightnessService;
    private readonly AudioService? _audioService;
    private readonly Action _openSettingsCallback;
    private bool _webViewReady;
    private IntPtr _hwnd = IntPtr.Zero;
    public DateTime LastDeactivatedTime { get; private set; } = DateTime.MinValue;

    public ControlCenterFlyoutWindow(
        ShellStateService shellState,
        SystemMetricsService metricsService,
        BrightnessService brightnessService,
        Action openSettingsCallback,
        AudioService? audioService = null)
    {
        InitializeComponent();
        _shellState = shellState;
        _metricsService = metricsService;
        _brightnessService = brightnessService;
        _openSettingsCallback = openSettingsCallback;
        _audioService = audioService;

        Deactivated += (_, _) =>
        {
            LastDeactivatedTime = DateTime.UtcNow;
            Hide();
        };
        Loaded += OnLoaded;

        _shellState.StateChanged += OnShellStateChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int ncrp = 1; // DWMNCRP_DISABLED
        Win32Api.DwmSetWindowAttribute(_hwnd, 2, ref ncrp, sizeof(int));
        int borderColor = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
        Win32Api.DwmSetWindowAttribute(_hwnd, 34, ref borderColor, sizeof(int));
        int cornerPref = 1; // DWMWCP_DONOTROUND
        Win32Api.DwmSetWindowAttribute(_hwnd, 33, ref cornerPref, sizeof(int));

        ApplyCornerClipping();
    }

    public void ApplyCornerClipping()
    {
        if (_hwnd == IntPtr.Zero || CcWebView == null || CcWebView.Handle == IntPtr.Zero) return;
        if (Win32Api.GetClientRect(CcWebView.Handle, out var clientRect) && clientRect.Width > 0 && clientRect.Height > 0)
        {
            double dpiScale = System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX;
            if (dpiScale <= 0) dpiScale = 1.0;
            int radiusPhysical = (int)Math.Round(11 * dpiScale);
            int diameter = radiusPhysical * 2;

            IntPtr childRgn = Win32Api.CreateRoundRectRgn(0, 0, clientRect.Width + 1, clientRect.Height + 1, diameter, diameter);
            if (Win32Api.SetWindowRgn(CcWebView.Handle, childRgn, true) == 0)
            {
                Win32Api.DeleteObject(childRgn);
            }
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var env = await WebViewEnvironmentManager.GetSharedEnvironmentAsync();
            await CcWebView.EnsureCoreWebView2Async(env);
            CcWebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

            var core = CcWebView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = true;

            string webUiDir = TaskbarWebWindow.ResolveWebUiDirectory();
            core.SetVirtualHostNameToFolderMapping(
                "app.winzenshell.local",
                webUiDir,
                CoreWebView2HostResourceAccessKind.Allow);

            core.SetVirtualHostNameToFolderMapping(
                "widgets.winzenshell.local",
                _shellState.WidgetManager.WidgetsRootPath,
                CoreWebView2HostResourceAccessKind.Allow);

            core.WebMessageReceived += OnWebMessageReceived;
            core.NavigationCompleted += (_, _) =>
            {
                _webViewReady = true;
                ApplyCornerClipping();
                PushStateToWeb(_shellState.BuildPayload());
                BroadcastMetrics();
            };

            core.Navigate("https://app.winzenshell.local/control-center.html");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ControlCenter init error: {ex.Message}");
        }
    }

    private void OnShellStateChanged(ShellFullStatePayload payload)
    {
        Dispatcher.Invoke(() =>
        {
            ApplyTheme(payload.Settings.Theme);
            PushStateToWeb(payload);
        });
    }

    private void ApplyTheme(string? theme)
    {
        bool isLight = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
        if (isLight)
        {
            OuterBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF9, 0xF9, 0xF9));
            OuterBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x38, 0x00, 0x00, 0x00));
        }
        else
        {
            OuterBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C, 0x1C, 0x1C));
            OuterBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
        }
    }

    public void PushStateToWeb(ShellFullStatePayload payload)
    {
        if (!_webViewReady || CcWebView.CoreWebView2 == null) return;

        string json = JsonSerializer.Serialize(new
        {
            type = "state_update",
            payload
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        CcWebView.CoreWebView2.PostWebMessageAsJson(json);
    }

    public void BroadcastMetrics()
    {
        if (!_webViewReady || CcWebView.CoreWebView2 == null || !IsVisible) return;

        var snapshot = _metricsService.GetSnapshot(IntPtr.Zero, IntPtr.Zero);
        string json = JsonSerializer.Serialize(new
        {
            type = "metrics_update",
            payload = snapshot
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        CcWebView.CoreWebView2.PostWebMessageAsJson(json);
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            string action = root.TryGetProperty("action", out var actProp) ? actProp.GetString() ?? "" : "";

            switch (action)
            {
                case "request_state":
                    PushStateToWeb(_shellState.BuildPayload());
                    BroadcastMetrics();
                    break;

                case "open_settings":
                    Hide();
                    _openSettingsCallback.Invoke();
                    break;

                case "set_brightness":
                    if (root.TryGetProperty("value", out var bValProp) && bValProp.TryGetInt32(out int bVal))
                    {
                        _brightnessService.SetBrightness(bVal);
                        BroadcastMetrics();
                    }
                    break;

                case "set_volume":
                    if (root.TryGetProperty("value", out var vValProp) && vValProp.TryGetInt32(out int vVal))
                    {
                        _audioService?.SetVolume(vVal);
                        BroadcastMetrics();
                    }
                    break;

                case "toggle_mute":
                    _audioService?.ToggleMute();
                    BroadcastMetrics();
                    break;

                case "update_control_center_widgets":
                    if (root.TryGetProperty("items", out var ccItemsProp) && ccItemsProp.ValueKind == JsonValueKind.Array)
                    {
                        var ccUpdates = JsonSerializer.Deserialize<List<ControlCenterItemConfig>>(
                            ccItemsProp.GetRawText(),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (ccUpdates != null)
                        {
                            _shellState.UpdateControlCenterWidgets(ccUpdates);
                        }
                    }
                    break;

                case "close_control_center":
                    Hide();
                    break;
            }
        }
        catch
        {
            // Ignore malformed message
        }
    }

    public void ShowAt(double anchorScreenX, double anchorWidth, double taskbarTop, double taskbarHeight, string dockEdge)
    {
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

        ApplyCornerClipping();
        ApplyTheme(_shellState.Settings.Theme);
        Show();
        Activate();
        Focus();

        PushStateToWeb(_shellState.BuildPayload());
        BroadcastMetrics();
    }
}
