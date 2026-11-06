using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using WinZenShell.Native;
using WinZenShell.Services;

namespace WinZenShell.Windows;

public partial class TaskbarWebWindow : Window
{
    private readonly ShellStateService _shellState;
    private readonly SystemMetricsService _metricsService;
    private readonly Action _openSettingsCallback;
    private readonly DispatcherTimer _metricsTimer;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _webViewReady;
    private bool _isApplyingWorkArea;

    public IntPtr Handle => _hwnd;

    public TaskbarWebWindow(
        ShellStateService shellState,
        SystemMetricsService metricsService,
        Action openSettingsCallback)
    {
        InitializeComponent();
        _shellState = shellState;
        _metricsService = metricsService;
        _openSettingsCallback = openSettingsCallback;

        _shellState.StateChanged += OnShellStateChanged;
        _shellState.TaskbarVisibilityRequested += OnTaskbarVisibilityRequested;
        _shellState.TaskbarBoundsUpdated += OnTaskbarBoundsUpdated;

        _metricsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.2)
        };
        _metricsTimer.Tick += (_, _) => BroadcastMetrics();

        Loaded += OnLoaded;
        Closed += (_, _) => _metricsTimer.Stop();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;

        // Set WS_EX_TOOLWINDOW | WS_EX_TOPMOST so taskbar does not appear in Alt+Tab
        int exStyle = Win32Api.GetWindowLong(_hwnd, Win32Api.GWL_EXSTYLE);
        exStyle |= Win32Api.WS_EX_TOOLWINDOW | Win32Api.WS_EX_TOPMOST;
        exStyle &= ~Win32Api.WS_EX_APPWINDOW;
        Win32Api.SetWindowLong(_hwnd, Win32Api.GWL_EXSTYLE, exStyle);

        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);

        _shellState.SyncTaskbarAndWorkArea();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var env = await WebViewEnvironmentManager.GetSharedEnvironmentAsync();
            await TaskbarWebView.EnsureCoreWebView2Async(env);

            var core = TaskbarWebView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = true;

            string webUiDir = ResolveWebUiDirectory();
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
                PushStateToWeb(_shellState.BuildPayload());
                BroadcastMetrics();
                _metricsTimer.Start();
            };

            core.Navigate("https://app.winzenshell.local/taskbar.html");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Taskbar WebView2 init error: {ex.Message}");
        }
    }

    public static string ResolveWebUiDirectory()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "src", "WinZenShell", "WebUI");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return Path.Combine(baseDir, "WebUI");
    }

    private void OnTaskbarBoundsUpdated(Win32Api.RECT rect)
    {
        Dispatcher.Invoke(() =>
        {
            Left = rect.Left;
            Top = rect.Top;
            Width = rect.Width;
            Height = rect.Height;
            TaskbarWebView.Width = rect.Width;
            TaskbarWebView.Height = rect.Height;
        });
    }

    private void OnTaskbarVisibilityRequested(bool visible)
    {
        Dispatcher.Invoke(() =>
        {
            if (visible)
            {
                Show();
                _shellState.SyncTaskbarAndWorkArea();
            }
            else
            {
                Hide();
            }
        });
    }

    private void OnShellStateChanged(ShellFullStatePayload payload)
    {
        Dispatcher.Invoke(() => PushStateToWeb(payload));
    }

    private void PushStateToWeb(ShellFullStatePayload payload)
    {
        if (!_webViewReady || TaskbarWebView.CoreWebView2 == null) return;

        string json = JsonSerializer.Serialize(new
        {
            type = "state_update",
            payload
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        TaskbarWebView.CoreWebView2.PostWebMessageAsJson(json);
    }

    private void BroadcastMetrics()
    {
        if (!_webViewReady || TaskbarWebView.CoreWebView2 == null || !IsVisible) return;

        var snapshot = _metricsService.GetSnapshot(_hwnd, IntPtr.Zero);
        string json = JsonSerializer.Serialize(new
        {
            type = "metrics_update",
            payload = snapshot
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        TaskbarWebView.CoreWebView2.PostWebMessageAsJson(json);
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
                    _openSettingsCallback.Invoke();
                    break;

                case "open_start_menu":
                    _metricsService.TriggerWindowsStartMenu();
                    break;

                case "activate_window":
                    if (root.TryGetProperty("hwnd", out var hwndProp) && hwndProp.TryGetInt64(out long hwndVal))
                    {
                        _metricsService.ActivateOrToggleWindow(hwndVal);
                        BroadcastMetrics();
                    }
                    break;

                case "hide_app":
                case "hide_window":
                    long hideHwnd = root.TryGetProperty("hwnd", out var hwProp) && hwProp.TryGetInt64(out long hVal) ? hVal : 0;
                    string? pName = root.TryGetProperty("processName", out var pnProp) ? pnProp.GetString() : null;
                    string? wTitle = root.TryGetProperty("title", out var wtProp) ? wtProp.GetString() : null;
                    _shellState.HideApp(hideHwnd, pName, wTitle);
                    BroadcastMetrics();
                    break;

                case "unhide_app":
                    if (root.TryGetProperty("appId", out var uhaProp) && !string.IsNullOrWhiteSpace(uhaProp.GetString()))
                    {
                        _shellState.UnhideApp(uhaProp.GetString()!);
                        BroadcastMetrics();
                    }
                    break;

                case "unhide_all_apps":
                case "unhide_all_windows":
                    _shellState.UnhideAllApps();
                    BroadcastMetrics();
                    break;

                case "close_window":
                    if (root.TryGetProperty("hwnd", out var cwProp) && cwProp.TryGetInt64(out long cwHwnd))
                    {
                        _shellState.CloseWindow(cwHwnd);
                        BroadcastMetrics();
                    }
                    break;

                case "emergency_restore":
                    _shellState.EmergencyRestoreAll();
                    break;
            }
        }
        catch
        {
            // Ignore malformed message
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Api.WM_DISPLAYCHANGE || msg == Win32Api.WM_DPICHANGED)
        {
            if (!_isApplyingWorkArea)
            {
                _isApplyingWorkArea = true;
                try
                {
                    _shellState.SyncTaskbarAndWorkArea();
                    _shellState.BroadcastState();
                }
                finally
                {
                    _isApplyingWorkArea = false;
                }
            }
        }
        return IntPtr.Zero;
    }
}
