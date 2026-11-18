using System;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using WinZenShell.Native;
using WinZenShell.Services;

namespace WinZenShell.Windows;

public partial class DynamicIslandWindow : Window
{
    private readonly ShellStateService _shellState;
    private readonly MediaService? _mediaService;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _webViewReady;
    private bool _isTaskbarOverlay;
    private bool _isExpanded;
    private bool _isClosing;
    private double _lastAnchorW = 80;
    private double _lastClockW = 78;
    private double _lastMusicW = 0;
    private bool _lastHasMusic = false;
    private double _lastAnchorY = 8.0;
    private double _lastAnchorH = 30.0;
    private double _lastTaskbarH = 46.0;
    private string _lastDockEdge = "Top";
    private string _lastSlot = "center";
    private string _lastTarget = "clock";
    private bool _lastIsMusicWide = false;

    private DateTime _lastExpandTime = DateTime.MinValue;

    public IntPtr Handle => _hwnd != IntPtr.Zero ? _hwnd : (_hwnd = new WindowInteropHelper(this).EnsureHandle());
    public bool IsOverlayOpen => _isTaskbarOverlay && IsVisible && !_isClosing;
    public bool IsExpanded => _isExpanded;
    public TaskbarWebWindow? TaskbarWindow { get; set; }

    public DynamicIslandWindow(ShellStateService shellState, MediaService? mediaService = null)
    {
        InitializeComponent();
        _shellState = shellState;
        _mediaService = mediaService;

        Loaded += OnLoaded;
        Deactivated += OnDeactivated;
        SizeChanged += OnWindowSizeChanged;
        _shellState.StateChanged += OnShellStateChanged;
        if (_mediaService != null)
        {
            _mediaService.MediaChanged += OnMediaChanged;
        }
        _ = InitializeWebViewAsync();
    }

    private void OnMediaChanged(MediaPlaybackSnapshot snapshot)
    {
        Dispatcher.Invoke(() =>
        {
            PushMediaToWeb(snapshot);
        });
    }

    public void PushMediaToWeb(MediaPlaybackSnapshot snapshot)
    {
        if (!_webViewReady || DiWebView?.CoreWebView2 == null) return;
        try
        {
            string json = JsonSerializer.Serialize(new
            {
                type = "metrics_update",
                payload = new
                {
                    media = snapshot
                }
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            DiWebView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch { }
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Invariant guard: overlay or expanded island must never be clipped below 440x330
        if ((_isTaskbarOverlay || _isExpanded) && (e.NewSize.Width < 439 || e.NewSize.Height < 329))
        {
            Dispatcher.BeginInvoke(() => EnsureWindowSize(440, 330));
        }
    }

    public void EnsureWindowSize(double targetW, double targetH)
    {
        bool changed = Math.Abs(Width - targetW) > 0.5 ||
                       Math.Abs(Height - targetH) > 0.5 ||
                       Math.Abs(DiWebView.ActualWidth - targetW) > 0.5 ||
                       Math.Abs(DiWebView.ActualHeight - targetH) > 0.5;
        if (!changed) return;

        Width = targetW;
        Height = targetH;
        DiWebView.Width = targetW;
        DiWebView.Height = targetH;
        DiWebView.InvalidateMeasure();
        UpdateLayout();

        if (_hwnd != IntPtr.Zero)
        {
            double dpi = 1.0;
            try { dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX; } catch { }
            if (dpi <= 0) dpi = 1.0;
            int pxW = (int)Math.Round(targetW * dpi);
            int pxH = (int)Math.Round(targetH * dpi);
            Win32Api.SetWindowPos(_hwnd, Win32Api.HWND_TOPMOST, 0, 0, pxW, pxH,
                Win32Api.SWP_NOMOVE | Win32Api.SWP_NOACTIVATE);
        }
    }

    public void EnsureWindowBounds(double targetLeft, double targetTop, double targetW, double targetH)
    {
        Left = targetLeft;
        Top = targetTop;
        Width = targetW;
        Height = targetH;
        DiWebView.Width = targetW;
        DiWebView.Height = targetH;
        DiWebView.InvalidateMeasure();
        UpdateLayout();

        if (_hwnd != IntPtr.Zero)
        {
            double dpiX = 1.0;
            double dpiY = 1.0;
            try
            {
                var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
                if (dpi.DpiScaleX > 0) dpiX = dpi.DpiScaleX;
                if (dpi.DpiScaleY > 0) dpiY = dpi.DpiScaleY;
            }
            catch { }
            int pxLeft = (int)Math.Round(targetLeft * dpiX);
            int pxTop = (int)Math.Round(targetTop * dpiY);
            int pxW = (int)Math.Round(targetW * dpiX);
            int pxH = (int)Math.Round(targetH * dpiY);
            Win32Api.SetWindowPos(_hwnd, Win32Api.HWND_TOPMOST, pxLeft, pxTop, pxW, pxH,
                Win32Api.SWP_NOACTIVATE);
        }
    }

    public void ResetOverlayImmediate()
    {
        _isClosing = false;
        if (_isTaskbarOverlay)
        {
            _isTaskbarOverlay = false;
            _isExpanded = false;
            if (_webViewReady && DiWebView?.CoreWebView2 != null)
            {
                DiWebView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"set_mode\",\"expanded\":false,\"instant\":true}");
            }
            var tb = TaskbarWindow ?? Owner as TaskbarWebWindow ?? System.Windows.Application.Current.Windows.OfType<TaskbarWebWindow>().FirstOrDefault();
            if (tb?.TaskbarWebView?.CoreWebView2 != null)
            {
                tb.TaskbarWebView.CoreWebView2.ExecuteScriptAsync(
                    "document.getElementById('shell-dynamic-island-wrapper')?.classList.remove('island-hidden'); " +
                    "document.getElementById('shell-dynamic-island')?.classList.remove('island-hidden');"
                );
            }
            Hide();
        }
    }

    private bool _isInitializingWebView;
    public async Task InitializeWebViewAsync()
    {
        if (_isInitializingWebView || _webViewReady) return;
        _isInitializingWebView = true;
        try
        {
            _hwnd = new WindowInteropHelper(this).EnsureHandle();
            var env = await WebViewEnvironmentManager.GetSharedEnvironmentAsync();
            await DiWebView.EnsureCoreWebView2Async(env);
            DiWebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

            var core = DiWebView.CoreWebView2;
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
                PushStateToWeb(_shellState.BuildPayload());
                if (_mediaService != null)
                {
                    PushMediaToWeb(_mediaService.CurrentSnapshot);
                }
                if (_isTaskbarOverlay)
                {
                    var msg = JsonSerializer.Serialize(new
                    {
                        type = "set_mode",
                        expanded = true,
                        target = _lastTarget,
                        anchorWidth = _lastAnchorW > 0 ? _lastAnchorW : 80,
                        clockWidth = _lastClockW > 0 ? _lastClockW : 78,
                        musicWidth = _lastMusicW,
                        hasMusicPill = _lastHasMusic,
                        anchorY = _lastAnchorY,
                        anchorHeight = _lastAnchorH > 0 ? _lastAnchorH : 30.0,
                        taskbarHeight = _lastTaskbarH > 0 ? _lastTaskbarH : 46.0,
                        dockEdge = _lastDockEdge,
                        slot = _lastSlot
                    });
                    core.PostWebMessageAsJson(msg);
                }
            };

            core.Navigate($"https://app.winzenshell.local/dynamic-island.html?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
        }
        catch (Exception ex)
        {
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "di_init_err.txt"), ex.ToString()); } catch { }
            System.Diagnostics.Debug.WriteLine($"DynamicIsland init error: {ex.Message}");
        }
        finally
        {
            _isInitializingWebView = false;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ = InitializeWebViewAsync();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        // Don't close immediately if expanded within the last 600ms (prevents click activation glitch)
        if ((DateTime.UtcNow - _lastExpandTime).TotalMilliseconds < 600)
        {
            return;
        }

        if (_isTaskbarOverlay)
        {
            CloseOverlay();
        }
        else if (_isExpanded)
        {
            CloseOverlay();
        }
    }

    private void OnGridMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isExpanded)
        {
            _lastExpandTime = DateTime.UtcNow;
            if (_webViewReady && DiWebView?.CoreWebView2 != null)
            {
                DiWebView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"set_mode\",\"expanded\":true}");
            }
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;

        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);

        int exStyle = Win32Api.GetWindowLong(_hwnd, Win32Api.GWL_EXSTYLE);
        exStyle |= Win32Api.WS_EX_TOOLWINDOW;
        exStyle |= Win32Api.WS_EX_TOPMOST;
        Win32Api.SetWindowLong(_hwnd, Win32Api.GWL_EXSTYLE, exStyle);

        int ncrp = 1; // DWMNCRP_DISABLED
        Win32Api.DwmSetWindowAttribute(_hwnd, 2, ref ncrp, sizeof(int));
        int borderColor = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
        Win32Api.DwmSetWindowAttribute(_hwnd, 34, ref borderColor, sizeof(int));

        Win32Api.SetWindowPos(_hwnd, Win32Api.HWND_TOPMOST, 0, 0, 0, 0,
            Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_NOACTIVATE);

        UpdatePosition();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == (Win32Api.WM_USER + 102))
        {
            handled = true;
            return IntPtr.Zero;
        }
        return IntPtr.Zero;
    }

    public void UpdatePosition()
    {
        if (_isTaskbarOverlay) return;

        var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;
        double dpi = 1.0;
        try
        {
            dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX;
        }
        catch { }
        if (dpi <= 0) dpi = 1.0;

        double monLeft = mon.Left / dpi;
        double monWidth = mon.Width / dpi;
        double monTop = mon.Top / dpi;

        double targetLeft = monLeft + (monWidth - Width) / 2.0;
        double targetTop = monTop + 2;
        EnsureWindowBounds(targetLeft, targetTop, Width, Height);
    }

    public void ShowAsOverlay(double anchorScreenX, double anchorW, double taskbarScreenY, double taskbarH, string dockEdge, string slot = "center", string target = "clock", double clockW = 0, double musicW = 0, bool hasMusic = false, double anchorY = 8.0, double anchorH = 30.0, bool isMusicWide = false)
    {
        _isClosing = false;
        _isTaskbarOverlay = true;
        _isExpanded = true;
        _lastAnchorW = anchorW;
        _lastClockW = clockW > 0 ? clockW : anchorW;
        _lastMusicW = musicW;
        _lastHasMusic = hasMusic;
        _lastAnchorY = anchorY >= 0 ? anchorY : 8.0;
        _lastAnchorH = anchorH > 10 ? anchorH : 30.0;
        _lastTaskbarH = taskbarH > 10 ? taskbarH : 46.0;
        _lastDockEdge = dockEdge;
        _lastSlot = slot;
        _lastTarget = target;
        _lastIsMusicWide = isMusicWide;
        if (isMusicWide && _lastMusicW < 200)
        {
            _lastMusicW = 210;
        }

        var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;
        double dpi = 1.0;
        try
        {
            dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX;
        }
        catch { }
        if (dpi <= 0) dpi = 1.0;
        double monLeft = mon.Left / dpi;
        double monW = mon.Width / dpi;

        const double overlayW = 440.0;
        const double overlayH = 330.0;

        double targetLeft;

        if (string.Equals(slot, "left", StringComparison.OrdinalIgnoreCase))
        {
            targetLeft = anchorScreenX;
        }
        else if (string.Equals(slot, "right", StringComparison.OrdinalIgnoreCase))
        {
            targetLeft = (anchorScreenX + anchorW) - overlayW;
        }
        else
        {
            targetLeft = anchorScreenX + (anchorW - overlayW) / 2.0;
        }

        // Clamp within monitor bounds
        targetLeft = Math.Max(monLeft, Math.Min(monLeft + monW - overlayW, targetLeft));

        double targetTop;
        if (string.Equals(dockEdge, "Top", StringComparison.OrdinalIgnoreCase))
        {
            targetTop = taskbarScreenY;
        }
        else
        {
            targetTop = taskbarScreenY + taskbarH - overlayH;
        }

        EnsureWindowBounds(targetLeft, targetTop, overlayW, overlayH);

        if (_webViewReady && DiWebView?.CoreWebView2 != null)
        {
            var msg = JsonSerializer.Serialize(new
            {
                type = "set_mode",
                expanded = true,
                target = target,
                anchorWidth = anchorW,
                clockWidth = _lastClockW,
                musicWidth = _lastMusicW,
                hasMusicPill = _lastHasMusic,
                isMusicWide = isMusicWide,
                anchorY = _lastAnchorY,
                anchorHeight = _lastAnchorH,
                dockEdge = dockEdge,
                slot = slot,
                taskbarHeight = _lastTaskbarH
            });
            DiWebView.CoreWebView2.PostWebMessageAsJson(msg);
        }

        Show();
        Topmost = true;
        EnsureWindowBounds(targetLeft, targetTop, overlayW, overlayH);
        Activate();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
        {
            if (_isTaskbarOverlay && IsVisible && !_isClosing)
            {
                EnsureWindowBounds(targetLeft, targetTop, overlayW, overlayH);
            }
        });

        var tb = TaskbarWindow ?? Owner as TaskbarWebWindow ?? System.Windows.Application.Current.Windows.OfType<TaskbarWebWindow>().FirstOrDefault();
        if (tb?.TaskbarWebView?.CoreWebView2 != null)
        {
            tb.TaskbarWebView.CoreWebView2.ExecuteScriptAsync(
                "document.getElementById('shell-dynamic-island-wrapper')?.classList.add('island-hidden'); " +
                "document.getElementById('shell-dynamic-island')?.classList.add('island-hidden');"
            );
        }
    }

    public void CloseOverlay()
    {
        if (_isClosing) return;

        if (_isTaskbarOverlay)
        {
            bool anim = _shellState.Settings.DynamicIsland?.EnableAnimation != false;
            if (anim && _webViewReady && DiWebView?.CoreWebView2 != null)
            {
                _isClosing = true;
                var msg = JsonSerializer.Serialize(new
                {
                    type = "set_mode",
                    expanded = false,
                    target = _lastTarget,
                    anchorWidth = _lastAnchorW > 0 ? _lastAnchorW : 80,
                    clockWidth = _lastClockW > 0 ? _lastClockW : 78,
                    musicWidth = _lastMusicW,
                    hasMusicPill = _lastHasMusic,
                    isMusicWide = _lastIsMusicWide,
                    anchorY = _lastAnchorY,
                    anchorHeight = _lastAnchorH > 0 ? _lastAnchorH : 30.0,
                    taskbarHeight = _lastTaskbarH > 0 ? _lastTaskbarH : 46.0,
                    dockEdge = _lastDockEdge,
                    slot = _lastSlot
                });
                DiWebView.CoreWebView2.PostWebMessageAsJson(msg);
                // Safety fallback to ensure overlay is hidden even if web message is dropped
                _ = System.Threading.Tasks.Task.Delay(650).ContinueWith(_ => Dispatcher.Invoke(() =>
                {
                    if (_isClosing)
                    {
                        FinishClose();
                    }
                }));
            }
            else
            {
                FinishClose();
            }
        }
        else
        {
            if (_isExpanded && !_isClosing)
            {
                _isClosing = true;
                if (_webViewReady && DiWebView?.CoreWebView2 != null)
                {
                    DiWebView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"set_mode\",\"expanded\":false}");
                }
                _ = System.Threading.Tasks.Task.Delay(650).ContinueWith(_ => Dispatcher.Invoke(() =>
                {
                    if (_isClosing)
                    {
                        FinishClose();
                    }
                }));
            }
        }
    }

    public async void FinishClose()
    {
        _isClosing = false;
        bool wasOverlay = _isTaskbarOverlay;
        _isTaskbarOverlay = false;
        _isExpanded = false;

        if (wasOverlay)
        {
            TaskbarWebWindow? tbWindow = TaskbarWindow ?? Owner as TaskbarWebWindow;
            if (tbWindow == null)
            {
                foreach (Window win in System.Windows.Application.Current.Windows)
                {
                    if (win is TaskbarWebWindow mainTb)
                    {
                        tbWindow = mainTb;
                        break;
                    }
                }
            }

            if (tbWindow?.TaskbarWebView?.CoreWebView2 != null)
            {
                try
                {
                    // Unhide the taskbar pill FIRST with smooth natural transition
                    await tbWindow.TaskbarWebView.CoreWebView2.ExecuteScriptAsync(
                        "(() => { " +
                        "  const w = document.getElementById('shell-dynamic-island-wrapper'); " +
                        "  const el = document.getElementById('shell-dynamic-island'); " +
                        "  const m = document.getElementById('shell-dynamic-island-music'); " +
                        "  if (w) { w.classList.remove('island-hidden'); } " +
                        "  if (el) { el.classList.remove('island-hidden'); } " +
                        "  if (m && window.winzenIsMusicWideMode && window.winzenIsMusicWideMode()) { m.classList.add('is-hovered'); } " +
                        "  if (window.winzenUnmergeDynamicIsland) window.winzenUnmergeDynamicIsland(); " +
                        "})();"
                    );
                    // Wait 2 compositor frames (~32ms) so DWM composites the 100% opaque taskbar pill before hiding the overlay
                    await System.Threading.Tasks.Task.Delay(32);
                }
                catch { }
            }

            Hide();
        }
        else
        {
            await System.Threading.Tasks.Task.Delay(50);
            if (!_isExpanded && !_isTaskbarOverlay)
            {
                EnsureWindowSize(440, 44);
                UpdatePosition();
            }
        }
    }



    private void OnShellStateChanged(ShellFullStatePayload payload)
    {
        Dispatcher.Invoke(() =>
        {
            if (!_isTaskbarOverlay)
            {
                UpdatePosition();
            }
            PushStateToWeb(payload);
        });
    }

    private void PushStateToWeb(ShellFullStatePayload payload)
    {
        if (!_webViewReady || DiWebView?.CoreWebView2 == null) return;
        try
        {
            string json = JsonSerializer.Serialize(new
            {
                type = "state_update",
                payload = payload
            });
            DiWebView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch { }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            string type = root.TryGetProperty("action", out var aProp) ? aProp.GetString() ?? "" :
                          root.TryGetProperty("type", out var tProp) ? tProp.GetString() ?? "" : "";

            if (type == "di_close_complete" || type == "di_closed_anim_complete")
            {
                Dispatcher.Invoke(() =>
                {
                    FinishClose();
                });
            }
            else if (type == "di_close")
            {
                Dispatcher.Invoke(() =>
                {
                    CloseOverlay();
                });
            }
            else if (type == "di_set_music_wide")
            {
                bool isWide = root.TryGetProperty("isWide", out var wProp) && wProp.GetBoolean();
                _lastIsMusicWide = isWide;
                if (isWide)
                {
                    _lastMusicW = 210;
                }
                else
                {
                    _lastMusicW = 68;
                }
                Dispatcher.Invoke(() =>
                {
                    TaskbarWebWindow? tbWindow = TaskbarWindow ?? Owner as TaskbarWebWindow;
                    if (tbWindow == null)
                    {
                        foreach (Window win in System.Windows.Application.Current.Windows)
                        {
                            if (win is TaskbarWebWindow mainTb)
                            {
                                tbWindow = mainTb;
                                break;
                            }
                        }
                    }
                    tbWindow?.TaskbarWebView?.CoreWebView2?.ExecuteScriptAsync(
                        $"window.winzenSetMusicWideMode && window.winzenSetMusicWideMode({(isWide ? "true" : "false")});"
                    );
                });
            }
            else if (type == "di_set_lyrics")
            {
                bool isPinned = root.TryGetProperty("isPinned", out var pinProp) && pinProp.GetBoolean();
                string lyricText = root.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "" : "";
                double lyricW = root.TryGetProperty("width", out var widthProp) && widthProp.TryGetDouble(out var lwVal) ? lwVal : 0;
                if (isPinned && lyricW > 40)
                {
                    _lastMusicW = lyricW;
                }
                else if (_lastIsMusicWide)
                {
                    _lastMusicW = 210;
                }
                else
                {
                    _lastMusicW = 68;
                }

                Dispatcher.Invoke(() =>
                {
                    TaskbarWebWindow? tbWindow = TaskbarWindow ?? Owner as TaskbarWebWindow;
                    if (tbWindow == null)
                    {
                        foreach (Window win in System.Windows.Application.Current.Windows)
                        {
                            if (win is TaskbarWebWindow mainTb)
                            {
                                tbWindow = mainTb;
                                break;
                            }
                        }
                    }

                    string safeText = lyricText.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
                    tbWindow?.TaskbarWebView?.CoreWebView2?.ExecuteScriptAsync(
                        $"window.winzenSetMusicLyrics && window.winzenSetMusicLyrics(\"{safeText}\", {(isPinned ? "true" : "false")}, {lyricW});"
                    );
                });
            }
            else if (type == "di_expand")
            {
                bool isExp = !root.TryGetProperty("expanded", out var expProp) || expProp.GetBoolean();
                _isExpanded = isExp;
                if (isExp)
                {
                    _lastExpandTime = DateTime.UtcNow;
                    Dispatcher.Invoke(() =>
                    {
                        EnsureWindowSize(440, 330);
                        if (!_isTaskbarOverlay)
                        {
                            UpdatePosition();
                        }
                    });
                }
            }
            else if (type == "media_play_pause")
            {
                _ = _mediaService?.TryTogglePlayPauseAsync();
            }
            else if (type == "media_play")
            {
                _ = _mediaService?.TryPlayAsync();
            }
            else if (type == "media_pause")
            {
                _ = _mediaService?.TryPauseAsync();
            }
            else if (type == "media_next")
            {
                _ = _mediaService?.TrySkipNextAsync();
            }
            else if (type == "media_previous")
            {
                _ = _mediaService?.TrySkipPreviousAsync();
            }
            else if (type == "media_seek")
            {
                double posVal = root.TryGetProperty("position", out var posProp) && posProp.TryGetDouble(out var pVal) ? pVal : 0;
                _ = _mediaService?.TryChangePlaybackPositionAsync(posVal);
            }
        }
        catch { }
    }
}
