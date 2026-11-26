using System;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using WinZenShell.Native;
using WinZenShell.Services;

namespace WinZenShell.Windows;

public partial class MediaFlyoutWindow : Window
{
    private readonly ShellStateService _shellState;
    private readonly SystemMetricsService _metricsService;
    private readonly MediaService _mediaService;
    private bool _webViewReady;
    private IntPtr _hwnd = IntPtr.Zero;
    private int _lastClipWidth = -1;
    private int _lastClipHeight = -1;
    private double _lastClipDpi = -1;
    private readonly System.Windows.Threading.DispatcherTimer _metricsTimer;

    public DateTime LastDeactivatedTime { get; private set; } = DateTime.MinValue;

    public MediaFlyoutWindow(
        ShellStateService shellState,
        SystemMetricsService metricsService,
        MediaService mediaService)
    {
        InitializeComponent();
        _shellState = shellState;
        _metricsService = metricsService;
        _mediaService = mediaService;

        _metricsTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _metricsTimer.Tick += (_, _) =>
        {
            if (IsVisible) BroadcastMediaState();
        };

        Deactivated += (_, _) =>
        {
            _metricsTimer.Stop();
            FlyoutAnimationHelper.HandleDeactivated(this, () => LastDeactivatedTime = DateTime.UtcNow);
        };
        Loaded += OnLoaded;

        _shellState.StateChanged += OnShellStateChanged;
        _mediaService.MediaChanged += OnMediaChanged;
        Closed += (_, _) =>
        {
            _shellState.StateChanged -= OnShellStateChanged;
            _mediaService.MediaChanged -= OnMediaChanged;
        };
    }

    private void OnShellStateChanged(ShellFullStatePayload payload)
    {
        Dispatcher.InvokeAsync(() =>
        {
            ApplyTheme(payload.Settings.Theme, payload.Settings.AccentColor);
        });
    }

    private void OnMediaChanged(MediaPlaybackSnapshot snapshot)
    {
        if (IsVisible)
        {
            Dispatcher.InvokeAsync(BroadcastMediaState);
        }
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
        if (_hwnd == IntPtr.Zero || MediaWebView == null || MediaWebView.Handle == IntPtr.Zero) return;
        if (Win32Api.GetClientRect(MediaWebView.Handle, out var clientRect) && clientRect.Width > 0 && clientRect.Height > 0)
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
            if (Win32Api.SetWindowRgn(MediaWebView.Handle, childRgn, false) == 0)
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
            await MediaWebView.EnsureCoreWebView2Async(env);
            bool initLight = string.Equals(_shellState.Settings.Theme, "light", StringComparison.OrdinalIgnoreCase);
            MediaWebView.DefaultBackgroundColor = FlyoutAnimationHelper.FlyoutBackgroundOpacity < 100
                ? System.Drawing.Color.Transparent
                : (initLight ? System.Drawing.Color.FromArgb(0xF3, 0xF3, 0xF3) : System.Drawing.Color.FromArgb(0x1E, 0x1E, 0x1E));

            var core = MediaWebView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = true;

            string webUiDir = TaskbarWebWindow.ResolveWebUiDirectory();
            core.SetVirtualHostNameToFolderMapping(
                "app.winzenshell.local",
                webUiDir,
                CoreWebView2HostResourceAccessKind.Allow);

            core.WebMessageReceived += OnWebMessageReceived;
            core.NavigationCompleted += (_, _) =>
            {
                _webViewReady = true;
                ApplyCornerClipping();
                ApplyTheme(_shellState.Settings.Theme, _shellState.Settings.AccentColor);
                BroadcastMediaState();
            };

            core.Navigate("https://app.winzenshell.local/media-flyout.html");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MediaFlyout init error: {ex.Message}");
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            string action = root.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";
            switch (action)
            {
                case "media_play":
                    _ = _mediaService.TryPlayAsync();
                    break;
                case "media_pause":
                    _ = _mediaService.TryPauseAsync();
                    break;
                case "media_play_pause":
                    _ = _mediaService.TryTogglePlayPauseAsync();
                    break;
                case "media_next":
                    _ = _mediaService.TrySkipNextAsync();
                    break;
                case "media_previous":
                    _ = _mediaService.TrySkipPreviousAsync();
                    break;
                case "media_seek":
                    if (root.TryGetProperty("position", out var posProp) && posProp.TryGetDouble(out double pos))
                    {
                        _ = _mediaService.TryChangePlaybackPositionAsync(pos);
                    }
                    break;
                case "media_select_session":
                    if (root.TryGetProperty("sourceAppId", out var appProp))
                    {
                        _ = _mediaService.SelectSessionAsync(appProp.GetString());
                    }
                    break;
                case "close_flyout":
                    FlyoutAnimationHelper.AnimateHide(this);
                    break;
            }
        }
        catch { }
    }

    private void BroadcastMediaState()
    {
        if (!_webViewReady || MediaWebView.CoreWebView2 == null) return;
        try
        {
            var snap = _mediaService.CurrentSnapshot;
            string json = JsonSerializer.Serialize(new
            {
                type = "media_update",
                media = new
                {
                    hasMedia = snap.HasMedia,
                    isPlaying = snap.IsPlaying,
                    title = snap.Title,
                    artist = snap.Artist,
                    albumTitle = snap.AlbumTitle,
                    thumbnailBase64 = snap.ThumbnailBase64,
                    sourceAppId = snap.SourceAppId,
                    positionSeconds = snap.PositionSeconds,
                    durationSeconds = snap.DurationSeconds,
                    canPlay = snap.CanPlay,
                    canPause = snap.CanPause,
                    canSkipNext = snap.CanSkipNext,
                    canSkipPrevious = snap.CanSkipPrevious,
                    sessions = snap.Sessions.ConvertAll(s => new
                    {
                        sourceAppId = s.SourceAppId,
                        title = s.Title,
                        artist = s.Artist,
                        isPlaying = s.IsPlaying,
                        isActive = s.IsActive
                    })
                }
            });
            MediaWebView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch { }
    }

    public void ApplyTheme(string? theme, string? accentColor = null)
    {
        bool isLight = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
        if (isLight)
        {
            OuterBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF3, 0xF3, 0xF3));
            OuterBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x28, 0x00, 0x00, 0x00));
        }
        else
        {
            OuterBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
            OuterBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0x3E, 0x3E));
        }

        if (IsVisible)
            FlyoutBackdropHelper.ApplyTo(this, Top);

        if (MediaWebView != null)
        {
            var bgCol = FlyoutAnimationHelper.FlyoutBackgroundOpacity < 100
                ? System.Drawing.Color.Transparent
                : (isLight ? System.Drawing.Color.FromArgb(0xF3, 0xF3, 0xF3) : System.Drawing.Color.FromArgb(0x1E, 0x1E, 0x1E));
            if (MediaWebView.DefaultBackgroundColor != bgCol)
                MediaWebView.DefaultBackgroundColor = bgCol;
        }

        if (_webViewReady && MediaWebView?.CoreWebView2 != null)
        {
            MediaWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
            {
                type = "theme_update",
                theme = isLight ? "light" : "dark",
                accentColor = accentColor ?? _shellState.Settings.AccentColor ?? "#3684E3"
            }));
        }
    }

    public void ShowAt(
        double anchorScreenX,
        double anchorWidth,
        double taskbarTop,
        double taskbarHeight,
        string dockEdge,
        string? theme = null,
        string? accentColor = null)
    {
        ApplyTheme(theme ?? _shellState.Settings.Theme, accentColor ?? _shellState.Settings.AccentColor);

        var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;
        double dpiScale = System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (dpiScale <= 0) dpiScale = 1.0;

        double monitorLeft = mon.Left / dpiScale;
        double monitorRight = mon.Right / dpiScale;
        double monitorTop = mon.Top / dpiScale;
        double monitorBottom = mon.Bottom / dpiScale;

        double left = anchorScreenX + (anchorWidth / 2.0) - (Width / 2.0);
        if (left + Width > monitorRight - 12)
            left = monitorRight - Width - 12;
        if (left < monitorLeft + 12)
            left = monitorLeft + 12;

        const double floatingGap = 10.0;
        double top = string.Equals(dockEdge, "Top", StringComparison.OrdinalIgnoreCase)
            ? taskbarTop + taskbarHeight + floatingGap
            : taskbarTop - Height - floatingGap;

        if (top < monitorTop + 12) top = monitorTop + 12;
        if (top + Height > monitorBottom - 12) top = monitorBottom - Height - 12;

        Left = left;
        ApplyCornerClipping();
        FlyoutAnimationHelper.AnimateShow(this, top, dockEdge, activate: true, useOpacityFade: true);

        BroadcastMediaState();
        _metricsTimer.Start();
    }
}
