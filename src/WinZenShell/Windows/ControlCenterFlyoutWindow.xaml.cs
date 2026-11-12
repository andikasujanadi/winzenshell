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
    private readonly Action<string, string>? _forwardActionCallback;
    private bool _webViewReady;
    private IntPtr _hwnd = IntPtr.Zero;
    private int _lastClipWidth = -1;
    private int _lastClipHeight = -1;
    private double _lastClipDpi = -1;
    private double _contentHeight = 520;
    private double _lastTaskbarTop;
    private double _lastTaskbarHeight = 50;
    private string _lastDockEdge = "Bottom";
    public DateTime LastDeactivatedTime { get; private set; } = DateTime.MinValue;

    public ControlCenterFlyoutWindow(
        ShellStateService shellState,
        SystemMetricsService metricsService,
        BrightnessService brightnessService,
        Action openSettingsCallback,
        AudioService? audioService = null,
        Action<string, string>? forwardActionCallback = null)
    {
        InitializeComponent();
        _shellState = shellState;
        _metricsService = metricsService;
        _brightnessService = brightnessService;
        _openSettingsCallback = openSettingsCallback;
        _audioService = audioService;
        _forwardActionCallback = forwardActionCallback;

        Deactivated += (_, _) =>
        {
            ExitEditMode();
            FlyoutAnimationHelper.HandleDeactivated(this, () => LastDeactivatedTime = DateTime.UtcNow);
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
            if (clientRect.Width == _lastClipWidth &&
                clientRect.Height == _lastClipHeight &&
                Math.Abs(dpiScale - _lastClipDpi) < 0.01)
            {
                return;
            }
            _lastClipWidth = clientRect.Width;
            _lastClipHeight = clientRect.Height;
            _lastClipDpi = dpiScale;

            int radiusPhysical = (int)Math.Round(15 * dpiScale);
            int diameter = radiusPhysical * 2;

            IntPtr childRgn = Win32Api.CreateRoundRectRgn(0, 0, clientRect.Width + 1, clientRect.Height + 1, diameter, diameter);
            if (Win32Api.SetWindowRgn(CcWebView.Handle, childRgn, false) == 0)
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
            bool initLight = string.Equals(_shellState.Settings.Theme, "light", StringComparison.OrdinalIgnoreCase);
            CcWebView.DefaultBackgroundColor = initLight
                ? System.Drawing.Color.FromArgb(0xF9, 0xF9, 0xF9)
                : System.Drawing.Color.FromArgb(0x1C, 0x1C, 0x1C);

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
        if (CcWebView != null)
        {
            var bgCol = isLight
                ? System.Drawing.Color.FromArgb(0xF9, 0xF9, 0xF9)
                : System.Drawing.Color.FromArgb(0x1C, 0x1C, 0x1C);
            if (CcWebView.DefaultBackgroundColor != bgCol)
                CcWebView.DefaultBackgroundColor = bgCol;
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
                    ExitEditMode();
                    FlyoutAnimationHelper.AnimateHide(this);
                    break;

                case "resize_control_center":
                    if (root.TryGetProperty("height", out var hProp) && hProp.TryGetDouble(out double reqH) && reqH > 50)
                    {
                        _contentHeight = reqH;
                        Dispatcher.Invoke(ApplyDynamicBounds);
                    }
                    break;

                case "activate_tray_icon":
                case "toggle_network_flyout":
                case "toggle_battery_flyout":
                    _forwardActionCallback?.Invoke(action, e.WebMessageAsJson);
                    break;
            }
        }
        catch
        {
            // Ignore malformed message
        }
    }

    private double GetAvailableMonitorHeight()
    {
        var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;
        double dpiY = System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleY;
        if (dpiY <= 0) dpiY = 1.0;

        double monitorTop = mon.Top / dpiY;
        double monitorBottom = mon.Bottom / dpiY;
        double monitorHeight = mon.Height / dpiY;

        const double floatingGap = 10.0;
        double taskbarH = Math.Max(_lastTaskbarHeight, 40.0);
        double tbTop = _lastTaskbarTop;
        if (tbTop <= 0)
        {
            tbTop = string.Equals(_lastDockEdge, "Top", StringComparison.OrdinalIgnoreCase)
                ? monitorTop
                : monitorBottom - taskbarH;
        }

        double availableH;
        if (string.Equals(_lastDockEdge, "Top", StringComparison.OrdinalIgnoreCase))
        {
            double top = tbTop + taskbarH + floatingGap;
            double bottom = monitorBottom - floatingGap;
            availableH = bottom - top;
        }
        else
        {
            double top = monitorTop + floatingGap;
            double bottom = tbTop - floatingGap;
            availableH = bottom - top;
        }

        return Math.Max(240.0, Math.Min(monitorHeight - (floatingGap * 2), availableH));
    }

    private double ClampToAvailableHeight(double requestedContentHeight)
    {
        double maxAvailable = GetAvailableMonitorHeight();
        if (requestedContentHeight <= 0) requestedContentHeight = 360;
        // Clip to requested content height, but never exceed available monitor height
        return Math.Clamp(Math.Round(requestedContentHeight), 160.0, maxAvailable);
    }

    private void ApplyDynamicBounds()
    {
        double targetHeight = ClampToAvailableHeight(_contentHeight);
        if (Math.Abs(Height - targetHeight) < 2.0) return;
        const double floatingGap = 10.0;

        Height = targetHeight;
        Top = string.Equals(_lastDockEdge, "Top", StringComparison.OrdinalIgnoreCase)
            ? _lastTaskbarTop + _lastTaskbarHeight + floatingGap
            : _lastTaskbarTop - targetHeight - floatingGap;

        Dispatcher.BeginInvoke(new Action(ApplyCornerClipping), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void ShowAt(double anchorScreenX, double anchorWidth, double taskbarTop, double taskbarHeight, string dockEdge)
    {
        _lastTaskbarTop = taskbarTop;
        _lastTaskbarHeight = taskbarHeight;
        _lastDockEdge = dockEdge;

        double dpiX = System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX;
        double dpiY = System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleY;
        if (dpiX <= 0) dpiX = 1.0;
        if (dpiY <= 0) dpiY = 1.0;

        var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;
        double monitorLeft = mon.Left / dpiX;
        double monitorRight = mon.Right / dpiX;
        double monitorTop = mon.Top / dpiY;
        double monitorBottom = mon.Bottom / dpiY;

        double flyoutWidth = Width;
        double flyoutHeight = ClampToAvailableHeight(_contentHeight);
        Height = flyoutHeight;

        double left = anchorScreenX + (anchorWidth / 2.0) - (flyoutWidth / 2.0);
        const double floatingGap = 10.0;
        const double sideMargin = 12.0;

        if (left + flyoutWidth > monitorRight - sideMargin)
            left = monitorRight - flyoutWidth - sideMargin;
        if (left < monitorLeft + sideMargin)
            left = monitorLeft + sideMargin;

        Left = left;

        double top;
        if (string.Equals(dockEdge, "Top", StringComparison.OrdinalIgnoreCase))
        {
            top = taskbarTop + taskbarHeight + floatingGap;
        }
        else
        {
            top = taskbarTop - flyoutHeight - floatingGap;
            if (top < monitorTop + floatingGap)
                top = monitorTop + floatingGap;
        }

        ApplyCornerClipping();
        ApplyTheme(_shellState.Settings.Theme);
        ExitEditMode();
        FlyoutAnimationHelper.AnimateShow(this, top, dockEdge, activate: true, useOpacityFade: false);

        PushStateToWeb(_shellState.BuildPayload());
        BroadcastMetrics();
    }

    public void ExitEditMode()
    {
        if (_webViewReady && CcWebView?.CoreWebView2 != null)
        {
            _ = CcWebView.CoreWebView2.ExecuteScriptAsync("window.exitEditMode && window.exitEditMode();");
        }
    }
}
