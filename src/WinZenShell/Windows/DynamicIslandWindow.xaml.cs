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
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _webViewReady;
    private bool _isTaskbarOverlay;
    private bool _isExpanded;
    private bool _isClosing;
    private double _lastAnchorW = 80;
    private string _lastDockEdge = "Top";
    private string _lastSlot = "center";

    private DateTime _lastExpandTime = DateTime.MinValue;

    public IntPtr Handle => _hwnd != IntPtr.Zero ? _hwnd : (_hwnd = new WindowInteropHelper(this).EnsureHandle());
    public bool IsOverlayOpen => _isTaskbarOverlay && IsVisible && !_isClosing;
    public bool IsExpanded => _isExpanded;

    public DynamicIslandWindow(ShellStateService shellState)
    {
        InitializeComponent();
        _shellState = shellState;

        Loaded += OnLoaded;
        Deactivated += OnDeactivated;
        _shellState.StateChanged += OnShellStateChanged;
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

        Left = monLeft + (monWidth - Width) / 2.0;
        Top = monTop + 2;
    }

    public void ShowAsOverlay(double anchorScreenX, double anchorW, double taskbarScreenY, double taskbarH, string dockEdge, string slot = "center")
    {
        _isClosing = false;
        _isTaskbarOverlay = true;
        _isExpanded = true;
        _lastAnchorW = anchorW;
        _lastDockEdge = dockEdge;
        _lastSlot = slot;
        Width = 560;
        Height = 260;

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

        double targetLeft;
        const double xOffset = 24.0; // Matches --di-slot-offset in dynamic-island.css

        if (string.Equals(slot, "left", StringComparison.OrdinalIgnoreCase))
        {
            targetLeft = anchorScreenX - xOffset;
        }
        else if (string.Equals(slot, "right", StringComparison.OrdinalIgnoreCase))
        {
            targetLeft = (anchorScreenX + anchorW + xOffset) - Width;
        }
        else
        {
            targetLeft = anchorScreenX + (anchorW - Width) / 2.0;
        }

        // Clamp within monitor bounds with 12px margin
        targetLeft = Math.Max(monLeft + 12, Math.Min(monLeft + monW - Width - 12, targetLeft));
        Left = targetLeft;

        if (string.Equals(dockEdge, "Top", StringComparison.OrdinalIgnoreCase))
        {
            Top = taskbarScreenY + taskbarH; // Floats directly below top taskbar
        }
        else
        {
            Top = taskbarScreenY - Height; // Floats directly above bottom taskbar
        }

        if (_webViewReady && DiWebView?.CoreWebView2 != null)
        {
            var msg = JsonSerializer.Serialize(new
            {
                type = "set_mode",
                expanded = true,
                anchorWidth = anchorW,
                anchorHeight = 30.0,
                dockEdge = dockEdge,
                slot = slot
            });
            DiWebView.CoreWebView2.PostWebMessageAsJson(msg);
        }

        Show();
        Topmost = true;
        if (_hwnd != IntPtr.Zero)
        {
            Win32Api.SetWindowPos(_hwnd, Win32Api.HWND_TOPMOST, 0, 0, 0, 0,
                Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_NOACTIVATE);
        }

        var tb = Owner as TaskbarWebWindow ?? System.Windows.Application.Current.Windows.OfType<TaskbarWebWindow>().FirstOrDefault();
        if (tb?.TaskbarWebView?.CoreWebView2 != null)
        {
            tb.TaskbarWebView.CoreWebView2.ExecuteScriptAsync("document.getElementById('shell-dynamic-island')?.classList.add('island-hidden');");
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
                    anchorWidth = _lastAnchorW > 0 ? _lastAnchorW : 80,
                    anchorHeight = 30.0,
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
            TaskbarWebWindow? tbWindow = Owner as TaskbarWebWindow;
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
                    // Unhide the taskbar pill FIRST and await the IPC execution
                    await tbWindow.TaskbarWebView.CoreWebView2.ExecuteScriptAsync(
                        "document.getElementById('shell-dynamic-island')?.classList.remove('island-hidden');"
                    );
                    // Wait 1 compositor frame (25ms) so DWM paints the visible taskbar pill behind the overlay
                    await System.Threading.Tasks.Task.Delay(25);
                }
                catch { }
            }

            Hide();
        }
        else
        {
            Width = 260;
            Height = 44;
            UpdatePosition();
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
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
                if (_isTaskbarOverlay)
                {
                    var msg = JsonSerializer.Serialize(new
                    {
                        type = "set_mode",
                        expanded = true,
                        anchorWidth = _lastAnchorW > 0 ? _lastAnchorW : 80,
                        anchorHeight = 30.0,
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
            System.Diagnostics.Debug.WriteLine($"DynamicIsland init error: {ex.Message}");
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
            else if (type == "di_expand")
            {
                bool isExp = !root.TryGetProperty("expanded", out var expProp) || expProp.GetBoolean();
                _isExpanded = isExp;
                if (isExp)
                {
                    _lastExpandTime = DateTime.UtcNow;
                    Dispatcher.Invoke(() =>
                    {
                        Width = 560;
                        Height = 260;
                        if (!_isTaskbarOverlay)
                        {
                            UpdatePosition();
                        }
                    });
                }
            }
        }
        catch { }
    }
}
