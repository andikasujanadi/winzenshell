using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using WinZenShell.Native;
using WinZenShell.Services;
using Forms = System.Windows.Forms;

namespace WinZenShell.Windows;

public partial class TaskbarWebWindow : Window
{
    private readonly ShellStateService _shellState;
    private readonly SystemMetricsService _metricsService;
    private readonly BrightnessService _brightnessService;
    private readonly AudioService _audioService;
    private readonly VirtualDesktopService _virtualDesktopService;
    private readonly AppLauncherService _appLauncherService;
    private readonly Action _openSettingsCallback;
    private readonly DispatcherTimer _metricsTimer;
    private readonly DispatcherTimer _fullscreenTimer;
    private BrightnessFlyoutWindow? _brightnessFlyout;
    private VolumeFlyoutWindow? _volumeFlyout;
    private ControlCenterFlyoutWindow? _controlCenterFlyout;
    private ClockFlyoutWindow? _clockFlyout;
    private AppLauncherFlyoutWindow? _appLauncherFlyout;
    private SystemMonitorFlyoutWindow? _systemMonitorFlyout;
    private FileSystemWatcher? _triggerWatcher;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _webViewReady;
    private bool _isApplyingWorkArea;
    private bool _isFullscreenActive;

    public IntPtr Handle => _hwnd;

    public TaskbarWebWindow(
        ShellStateService shellState,
        SystemMetricsService metricsService,
        BrightnessService brightnessService,
        AppLauncherService appLauncherService,
        Action openSettingsCallback)
    {
        InitializeComponent();
        _shellState = shellState;
        _metricsService = metricsService;
        _brightnessService = brightnessService;
        _audioService = new AudioService();
        _virtualDesktopService = new VirtualDesktopService();
        _metricsService.Audio = _audioService;
        _metricsService.VirtualDesktops = _virtualDesktopService;
        _appLauncherService = appLauncherService;
        _openSettingsCallback = openSettingsCallback;

        _shellState.StateChanged += OnShellStateChanged;
        _shellState.TaskbarVisibilityRequested += OnTaskbarVisibilityRequested;
        _shellState.TaskbarBoundsUpdated += OnTaskbarBoundsUpdated;
        _brightnessService.BrightnessChanged += val => Dispatcher.Invoke(() =>
        {
            if (_brightnessFlyout != null && _brightnessFlyout.IsVisible)
            {
                _brightnessFlyout.UpdateBrightness(val);
            }
            BroadcastMetrics();
        });
        _audioService.VolumeChanged += (_, _) => Dispatcher.Invoke(BroadcastMetrics);

        _metricsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.2)
        };
        _metricsTimer.Tick += (_, _) => BroadcastMetrics();

        _fullscreenTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _fullscreenTimer.Tick += (_, _) => CheckFullscreenApp();

        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            _metricsTimer.Stop();
            _fullscreenTimer.Stop();
        };
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

        try
        {
            _triggerWatcher = new FileSystemWatcher(AppDomain.CurrentDomain.BaseDirectory, "cmd_trigger.txt")
            {
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };
            _triggerWatcher.Changed += (_, _) => HandleExternalTrigger();
            _triggerWatcher.Created += (_, _) => HandleExternalTrigger();
            HandleExternalTrigger();
        }
        catch { }
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
                _fullscreenTimer.Start();
            };

            core.Navigate("https://app.winzenshell.local/taskbar.html");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Taskbar WebView2 init error: {ex.Message}");
        }
    }

    private void HandleExternalTrigger()
    {
        try
        {
            string triggerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cmd_trigger.txt");
            if (!File.Exists(triggerPath)) return;
            System.Threading.Thread.Sleep(50);
            string cmd = File.ReadAllText(triggerPath).Trim();
            try { File.Delete(triggerPath); } catch { }

            if (cmd.StartsWith("capture_taskbar", StringComparison.OrdinalIgnoreCase))
            {
                var parts = cmd.Split(' ');
                string outPath = parts.Length > 1 ? parts[1] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "taskbar_capture.png");
                Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        if (TaskbarWebView != null && TaskbarWebView.CoreWebView2 != null)
                        {
                            using (var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                await TaskbarWebView.CoreWebView2.CapturePreviewAsync(
                                    Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,
                                    fs);
                                await fs.FlushAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "capture_err.txt"), ex.ToString());
                    }
                });
                return;
            }

            Dispatcher.Invoke(() =>
            {
                try
                {
                    var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;

                    if (cmd.StartsWith("open_cc", StringComparison.OrdinalIgnoreCase))
                    {
                        _controlCenterFlyout ??= new ControlCenterFlyoutWindow(_shellState, _metricsService, _brightnessService, _openSettingsCallback, _audioService);
                        _controlCenterFlyout.ShowAt(mon.Right - 380, 34, Top, Height, _shellState.Settings.DockEdge);
                    }
                    else if (cmd.StartsWith("close_cc", StringComparison.OrdinalIgnoreCase))
                    {
                        _controlCenterFlyout?.Hide();
                    }
                    else if (cmd.StartsWith("open_brightness", StringComparison.OrdinalIgnoreCase))
                    {
                        _brightnessFlyout ??= new BrightnessFlyoutWindow(_brightnessService);
                        _brightnessFlyout.ShowAt(mon.Right - 420, 34, Top, Height, _shellState.Settings.DockEdge, _shellState.Settings.Theme);
                    }
                    else if (cmd.StartsWith("open_volume", StringComparison.OrdinalIgnoreCase))
                    {
                        _volumeFlyout ??= new VolumeFlyoutWindow(_audioService);
                        _volumeFlyout.ShowAt(mon.Right - 420, 34, Top, Height, _shellState.Settings.DockEdge, _shellState.Settings.Theme);
                    }
                    else if (cmd.StartsWith("open_clock", StringComparison.OrdinalIgnoreCase))
                    {
                        _clockFlyout ??= new ClockFlyoutWindow();
                        _clockFlyout.ShowAt(mon.Right - 320, 34, Top, Height, _shellState.Settings.DockEdge, _shellState.Settings.Theme);
                    }
                    else if (cmd.StartsWith("open_sys_monitor", StringComparison.OrdinalIgnoreCase))
                    {
                        _systemMonitorFlyout ??= new SystemMonitorFlyoutWindow(_metricsService);
                        _systemMonitorFlyout.ShowAt(mon.Right - 460, 34, Top, Height, _shellState.Settings.DockEdge, _shellState.Settings.Theme);
                    }
                    else if (cmd.StartsWith("open_launcher", StringComparison.OrdinalIgnoreCase))
                    {
                        _appLauncherFlyout ??= new AppLauncherFlyoutWindow(_appLauncherService, _openSettingsCallback, _shellState);
                        string reg = "left";
                        if (cmd.Contains("center", StringComparison.OrdinalIgnoreCase)) reg = "center";
                        else if (cmd.Contains("right", StringComparison.OrdinalIgnoreCase)) reg = "right";
                        else if (cmd.Contains("left", StringComparison.OrdinalIgnoreCase)) reg = "left";
                        else if (_shellState.Settings.WidgetOverrides.TryGetValue("community.zen-start-button", out var ov) && !string.IsNullOrWhiteSpace(ov.Slot))
                            reg = ov.Slot;

                        _appLauncherFlyout.ShowAt(mon.Left + 40, 34, Top, Height, _shellState.Settings.DockEdge, reg);
                    }
                    else if (cmd.StartsWith("open_settings", StringComparison.OrdinalIgnoreCase))
                    {
                        _openSettingsCallback.Invoke();
                    }
                    else if (cmd.StartsWith("set_theme", StringComparison.OrdinalIgnoreCase))
                    {
                        string targetTheme = cmd.Contains("light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark";
                        _shellState.UpdateTheme(targetTheme);
                    }
                    else if (cmd.StartsWith("close_all", StringComparison.OrdinalIgnoreCase))
                    {
                        CloseAllFlyouts();
                    }
                    else if (cmd.StartsWith("toggle_volume", StringComparison.OrdinalIgnoreCase))
                    {
                        _volumeFlyout ??= new VolumeFlyoutWindow(_audioService);
                        bool wasJustOpen = _volumeFlyout.IsVisible ||
                            (DateTime.UtcNow - _volumeFlyout.LastDeactivatedTime).TotalMilliseconds < 350;
                        CloseAllFlyouts();
                        if (!wasJustOpen)
                        {
                            _volumeFlyout.ShowAt(mon.Right - 420, 34, Top, Height, _shellState.Settings.DockEdge, _shellState.Settings.Theme);
                        }
                    }
                    else if (cmd.StartsWith("toggle_brightness", StringComparison.OrdinalIgnoreCase))
                    {
                        _brightnessFlyout ??= new BrightnessFlyoutWindow(_brightnessService);
                        bool wasJustOpen = _brightnessFlyout.IsVisible ||
                            (DateTime.UtcNow - _brightnessFlyout.LastDeactivatedTime).TotalMilliseconds < 350;
                        CloseAllFlyouts();
                        if (!wasJustOpen)
                        {
                            _brightnessFlyout.ShowAt(mon.Right - 420, 34, Top, Height, _shellState.Settings.DockEdge, _shellState.Settings.Theme);
                        }
                    }
                    else if (cmd.StartsWith("status_flyout", StringComparison.OrdinalIgnoreCase))
                    {
                        string status = $"Volume={_volumeFlyout?.IsVisible == true};Brightness={_brightnessFlyout?.IsVisible == true};Clock={_clockFlyout?.IsVisible == true};Telemetry={_systemMonitorFlyout?.IsVisible == true};CC={_controlCenterFlyout?.IsVisible == true};Launcher={_appLauncherFlyout?.IsVisible == true}";
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flyout_status.txt"), status);
                    }
                    else if (cmd.StartsWith("volume_click_0", StringComparison.OrdinalIgnoreCase))
                    {
                        if (_volumeFlyout != null && _volumeFlyout.VolumeSlider != null)
                        {
                            _volumeFlyout.UpdateSliderValueFromMouse(_volumeFlyout.VolumeSlider, new System.Windows.Point(0, 10));
                        }
                    }
                    else if (cmd.StartsWith("brightness_click_0", StringComparison.OrdinalIgnoreCase))
                    {
                        if (_brightnessFlyout != null && _brightnessFlyout.BrightnessSlider != null)
                        {
                            _brightnessFlyout.UpdateSliderValueFromMouse(_brightnessFlyout.BrightnessSlider, new System.Windows.Point(0, 10));
                        }
                    }
                    else if (cmd.StartsWith("set_brightness_val", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = cmd.Split(' ');
                        if (parts.Length > 1 && int.TryParse(parts[1], out int val))
                        {
                            _brightnessService.SetBrightness(val);
                            if (_brightnessFlyout != null && _brightnessFlyout.IsVisible)
                            {
                                _brightnessFlyout.UpdateBrightness(val);
                            }
                            BroadcastMetrics();
                        }
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "trigger_error.txt"), $"[{DateTime.Now}] Dispatcher {ex}\n");
                    }
                    catch { }
                }
            });
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "trigger_error.txt"), $"[{DateTime.Now}] {ex}\n");
            }
            catch { }
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
        if (_controlCenterFlyout != null && _controlCenterFlyout.IsVisible)
        {
            _controlCenterFlyout.BroadcastMetrics();
        }
        if (_systemMonitorFlyout != null && _systemMonitorFlyout.IsVisible)
        {
            _systemMonitorFlyout.UpdateTelemetry(snapshot);
        }
        if (_volumeFlyout != null && _volumeFlyout.IsVisible)
        {
            _volumeFlyout.UpdateVolume(snapshot.VolumePercent, snapshot.IsMuted);
        }
        if (_brightnessFlyout != null && _brightnessFlyout.IsVisible)
        {
            _brightnessFlyout.UpdateBrightness(snapshot.BrightnessPercent);
        }
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

                case "show_window_context_menu":
                    ShowNativeWindowContextMenu(root);
                    break;

                case "toggle_brightness_flyout":
                    double bx = root.TryGetProperty("x", out var bxProp) && bxProp.TryGetDouble(out double bxd) ? bxd : 0;
                    double bw = root.TryGetProperty("width", out var bwProp) && bwProp.TryGetDouble(out double bwd) ? bwd : 34;

                    Dispatcher.Invoke(() =>
                    {
                        _brightnessFlyout ??= new BrightnessFlyoutWindow(_brightnessService);
                        bool wasJustOpen = _brightnessFlyout.IsVisible ||
                            (DateTime.UtcNow - _brightnessFlyout.LastDeactivatedTime).TotalMilliseconds < 350;

                        CloseAllFlyouts();

                        if (!wasJustOpen)
                        {
                            _brightnessFlyout.ShowAt(Left + bx, bw, Top, Height, _shellState.Settings.DockEdge, _shellState.Settings.Theme);
                        }
                    });
                    break;

                case "toggle_volume_flyout":
                    double vx = root.TryGetProperty("x", out var vxProp) && vxProp.TryGetDouble(out double vxd) ? vxd : 0;
                    double vw = root.TryGetProperty("width", out var vwProp) && vwProp.TryGetDouble(out double vwd) ? vwd : 34;

                    Dispatcher.Invoke(() =>
                    {
                        _volumeFlyout ??= new VolumeFlyoutWindow(_audioService);
                        bool wasJustOpen = _volumeFlyout.IsVisible ||
                            (DateTime.UtcNow - _volumeFlyout.LastDeactivatedTime).TotalMilliseconds < 350;

                        CloseAllFlyouts();

                        if (!wasJustOpen)
                        {
                            _volumeFlyout.ShowAt(Left + vx, vw, Top, Height, _shellState.Settings.DockEdge, _shellState.Settings.Theme);
                        }
                    });
                    break;

                case "toggle_control_center":
                    double ccX = root.TryGetProperty("x", out var ccXProp) && ccXProp.TryGetDouble(out double ccXd) ? ccXd : 0;
                    double ccW = root.TryGetProperty("width", out var ccWProp) && ccWProp.TryGetDouble(out double ccWd) ? ccWd : 34;

                    Dispatcher.Invoke(() =>
                    {
                        _controlCenterFlyout ??= new ControlCenterFlyoutWindow(_shellState, _metricsService, _brightnessService, _openSettingsCallback, _audioService);
                        bool wasJustOpen = _controlCenterFlyout.IsVisible ||
                            (DateTime.UtcNow - _controlCenterFlyout.LastDeactivatedTime).TotalMilliseconds < 350;

                        CloseAllFlyouts();

                        if (!wasJustOpen)
                        {
                            _controlCenterFlyout.ShowAt(Left + ccX, ccW, Top, Height, _shellState.Settings.DockEdge);
                        }
                    });
                    break;

                case "toggle_clock_flyout":
                    double clkX = root.TryGetProperty("x", out var clkXProp) && clkXProp.TryGetDouble(out double clkXd) ? clkXd : 0;
                    double clkW = root.TryGetProperty("width", out var clkWProp) && clkWProp.TryGetDouble(out double clkWd) ? clkWd : 34;

                    Dispatcher.Invoke(() =>
                    {
                        _clockFlyout ??= new ClockFlyoutWindow();
                        bool wasJustOpen = _clockFlyout.IsVisible ||
                            (DateTime.UtcNow - _clockFlyout.LastDeactivatedTime).TotalMilliseconds < 350;

                        CloseAllFlyouts();

                        if (!wasJustOpen)
                        {
                            _clockFlyout.ShowAt(Left + clkX, clkW, Top, Height, _shellState.Settings.DockEdge, _shellState.Settings.Theme);
                        }
                    });
                    break;

                case "toggle_system_monitor_flyout":
                    double smX = root.TryGetProperty("x", out var smXProp) && smXProp.TryGetDouble(out double smXd) ? smXd : 0;
                    double smW = root.TryGetProperty("width", out var smWProp) && smWProp.TryGetDouble(out double smWd) ? smWd : 34;

                    Dispatcher.Invoke(() =>
                    {
                        _systemMonitorFlyout ??= new SystemMonitorFlyoutWindow(_metricsService);
                        bool wasJustOpen = _systemMonitorFlyout.IsVisible ||
                            (DateTime.UtcNow - _systemMonitorFlyout.LastDeactivatedTime).TotalMilliseconds < 350;

                        CloseAllFlyouts();

                        if (!wasJustOpen)
                        {
                            _systemMonitorFlyout.ShowAt(Left + smX, smW, Top, Height, _shellState.Settings.DockEdge, _shellState.Settings.Theme);
                        }
                    });
                    break;

                case "toggle_app_launcher":
                    double alX = root.TryGetProperty("x", out var alXProp) && alXProp.TryGetDouble(out double alXd) ? alXd : 0;
                    double alW = root.TryGetProperty("width", out var alWProp) && alWProp.TryGetDouble(out double alWd) ? alWd : 34;
                    string alRegion = root.TryGetProperty("region", out var alRegProp) ? alRegProp.GetString() ?? "" : "";

                    Dispatcher.Invoke(() =>
                    {
                        _appLauncherFlyout ??= new AppLauncherFlyoutWindow(_appLauncherService, _openSettingsCallback, _shellState);
                        bool wasJustOpen = _appLauncherFlyout.IsVisible ||
                            (DateTime.UtcNow - _appLauncherFlyout.LastDeactivatedTime).TotalMilliseconds < 350;

                        CloseAllFlyouts();

                        if (!wasJustOpen)
                        {
                            double screenX = Left + alX;

                            if (string.IsNullOrWhiteSpace(alRegion))
                            {
                                if (_shellState.Settings.WidgetOverrides.TryGetValue("community.zen-start-button", out var ov) && !string.IsNullOrWhiteSpace(ov.Slot))
                                {
                                    alRegion = ov.Slot;
                                }
                                else
                                {
                                    double anchorCenter = screenX + (alW / 2.0);
                                    if (anchorCenter < Left + (Width * 0.35))
                                        alRegion = "left";
                                    else if (anchorCenter > Left + (Width * 0.65))
                                        alRegion = "right";
                                    else
                                        alRegion = "center";
                                }
                            }

                            _appLauncherFlyout.ShowAt(screenX, alW, Top, Height, _shellState.Settings.DockEdge, alRegion);
                        }
                    });
                    break;

                case "set_brightness":
                    if (root.TryGetProperty("value", out var bValProp) && bValProp.TryGetInt32(out int bVal))
                    {
                        _brightnessService.SetBrightness(bVal);
                        if (_brightnessFlyout != null && _brightnessFlyout.IsVisible)
                        {
                            _brightnessFlyout.UpdateBrightness(bVal);
                        }
                        BroadcastMetrics();
                    }
                    break;

                case "set_volume":
                    if (root.TryGetProperty("value", out var vValProp) && vValProp.TryGetInt32(out int vVal))
                    {
                        _audioService.SetVolume(vVal);
                        BroadcastMetrics();
                    }
                    break;

                case "toggle_mute":
                    _audioService.ToggleMute();
                    BroadcastMetrics();
                    break;

                case "switch_desktop":
                    if (root.TryGetProperty("index", out var dIdxProp) && dIdxProp.TryGetInt32(out int dIdx))
                    {
                        _virtualDesktopService.SwitchToDesktop(dIdx);
                        System.Threading.Tasks.Task.Delay(350).ContinueWith(_ => Dispatcher.Invoke(BroadcastMetrics));
                    }
                    break;

                case "next_desktop":
                    _virtualDesktopService.NextDesktop();
                    System.Threading.Tasks.Task.Delay(350).ContinueWith(_ => Dispatcher.Invoke(BroadcastMetrics));
                    break;

                case "previous_desktop":
                    _virtualDesktopService.PreviousDesktop();
                    System.Threading.Tasks.Task.Delay(350).ContinueWith(_ => Dispatcher.Invoke(BroadcastMetrics));
                    break;

                case "create_desktop":
                    _virtualDesktopService.CreateNewDesktop();
                    System.Threading.Tasks.Task.Delay(600).ContinueWith(_ => Dispatcher.Invoke(BroadcastMetrics));
                    break;

                case "open_task_view":
                    _virtualDesktopService.OpenTaskView();
                    break;

                case "reorder_widgets":
                    if (root.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                    {
                        var updates = JsonSerializer.Deserialize<List<WidgetOrderUpdate>>(
                            itemsProp.GetRawText(),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (updates != null)
                        {
                            _shellState.ReorderWidgets(updates);
                        }
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

    private void ShowNativeWindowContextMenu(JsonElement root)
    {
        string appId = root.TryGetProperty("appId", out var appIdProp) ? appIdProp.GetString() ?? "" : "";
        string processName = root.TryGetProperty("processName", out var pn) ? pn.GetString() ?? "App" : "App";
        string title = root.TryGetProperty("title", out var tt) ? tt.GetString() ?? processName : processName;

        var windows = new List<(long Hwnd, string Title, bool IsActive, bool IsMinimized)>();
        if (root.TryGetProperty("windows", out var winArr) && winArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var w in winArr.EnumerateArray())
            {
                long h = w.TryGetProperty("hwnd", out var hp) && hp.TryGetInt64(out long hv) ? hv : 0;
                string wt = w.TryGetProperty("title", out var tp) ? tp.GetString() ?? title : title;
                bool active = w.TryGetProperty("isActive", out var ap) && ap.GetBoolean();
                bool min = w.TryGetProperty("isMinimized", out var mp) && mp.GetBoolean();
                if (h != 0)
                {
                    windows.Add((h, wt, active, min));
                }
            }
        }

        if (windows.Count == 0) return;

        bool isMulti = windows.Count > 1;
        var menu = new Forms.ContextMenuStrip
        {
            Renderer = new Win11DarkContextMenuRenderer(),
            ForeColor = Color.FromArgb(245, 245, 245),
            BackColor = Color.FromArgb(44, 44, 44),
            ShowImageMargin = true,
            ShowCheckMargin = false,
            ImageScalingSize = new System.Drawing.Size(16, 16),
            Font = new Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Regular),
            Padding = new Forms.Padding(4, 4, 4, 4)
        };

        menu.HandleCreated += (_, _) =>
        {
            int corner = 2; // DWMWCP_ROUND (Windows 11 round corners)
            Win32Api.DwmSetWindowAttribute(menu.Handle, 33, ref corner, sizeof(int));
            int dark = 1;
            Win32Api.DwmSetWindowAttribute(menu.Handle, 20, ref dark, sizeof(int));
        };

        string headerText = isMulti
            ? $"{processName} ({windows.Count} windows)"
            : (!string.IsNullOrWhiteSpace(title) ? title : processName);

        var headerItem = new Forms.ToolStripMenuItem(headerText)
        {
            Enabled = false,
            ForeColor = Color.FromArgb(170, 170, 170),
            Font = new Font("Segoe UI", 9.0f, System.Drawing.FontStyle.Bold),
            Padding = new Forms.Padding(6, 6, 10, 5),
            Image = CreateFluentIcon("window")
        };
        menu.Items.Add(headerItem);
        menu.Items.Add(new Forms.ToolStripSeparator());

        if (isMulti)
        {
            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                string itemLabel = string.IsNullOrWhiteSpace(w.Title) ? $"Window {i + 1}" : w.Title;
                var winItem = new Forms.ToolStripMenuItem(itemLabel)
                {
                    ForeColor = w.IsActive ? Color.FromArgb(96, 205, 255) : Color.FromArgb(245, 245, 245),
                    Padding = new Forms.Padding(6, 5, 10, 5),
                    Image = CreateFluentIcon("window", w.IsActive)
                };
                long targetHwnd = w.Hwnd;
                winItem.Click += (_, _) =>
                {
                    _metricsService.ActivateOrToggleWindow(targetHwnd);
                    BroadcastMetrics();
                };
                menu.Items.Add(winItem);
            }
            menu.Items.Add(new Forms.ToolStripSeparator());
        }
        else
        {
            long targetHwnd = windows[0].Hwnd;
            var switchItem = new Forms.ToolStripMenuItem("Switch to window")
            {
                ForeColor = Color.FromArgb(245, 245, 245),
                Padding = new Forms.Padding(6, 5, 10, 5),
                Image = CreateFluentIcon("focus")
            };
            switchItem.Click += (_, _) =>
            {
                _metricsService.ActivateOrToggleWindow(targetHwnd);
                BroadcastMetrics();
            };
            menu.Items.Add(switchItem);
        }

        var minimizeItem = new Forms.ToolStripMenuItem(isMulti ? "Minimize all" : "Minimize")
        {
            ForeColor = Color.FromArgb(245, 245, 245),
            Padding = new Forms.Padding(6, 5, 10, 5),
            Image = CreateFluentIcon("minimize")
        };
        minimizeItem.Click += (_, _) =>
        {
            foreach (var w in windows)
            {
                if (!w.IsMinimized)
                {
                    Win32Api.ShowWindow(new IntPtr(w.Hwnd), Win32Api.SW_MINIMIZE);
                }
            }
            BroadcastMetrics();
        };
        menu.Items.Add(minimizeItem);

        menu.Items.Add(new Forms.ToolStripSeparator());

        var hideItem = new Forms.ToolStripMenuItem("Hide app from taskbar")
        {
            ForeColor = Color.FromArgb(220, 220, 220),
            Padding = new Forms.Padding(6, 5, 10, 5),
            Image = CreateFluentIcon("hide")
        };
        hideItem.Click += (_, _) =>
        {
            _shellState.HideApp(windows[0].Hwnd, appId, processName, title);
            BroadcastMetrics();
        };
        menu.Items.Add(hideItem);

        var closeItem = new Forms.ToolStripMenuItem(isMulti ? "Close all windows" : "Close window")
        {
            ForeColor = Color.FromArgb(255, 153, 164),
            Padding = new Forms.Padding(6, 5, 10, 5),
            Image = CreateFluentIcon("close"),
            Tag = "danger"
        };
        closeItem.Click += (_, _) =>
        {
            foreach (var w in windows)
            {
                _shellState.CloseWindow(w.Hwnd);
            }
            BroadcastMetrics();
        };
        menu.Items.Add(closeItem);

        var cursorPos = Forms.Cursor.Position;
        menu.Show(cursorPos, Forms.ToolStripDropDownDirection.AboveRight);
    }

    public static Bitmap CreateFluentIcon(string type, bool isActive = false)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

        switch (type.ToLowerInvariant())
        {
            case "window":
                Color winColor = isActive ? Color.FromArgb(96, 205, 255) : Color.FromArgb(200, 200, 200);
                using (var pen = new Pen(winColor, 1.25f))
                {
                    g.DrawRectangle(pen, 2, 2, 11, 10);
                    g.DrawLine(pen, 2, 5, 13, 5);
                }
                if (isActive)
                {
                    using var dotBrush = new SolidBrush(Color.FromArgb(96, 205, 255));
                    g.FillEllipse(dotBrush, 5, 7, 3, 3);
                }
                break;

            case "focus":
                using (var pen = new Pen(Color.FromArgb(96, 205, 255), 1.35f))
                {
                    g.DrawRectangle(pen, 2, 2, 11, 11);
                    g.DrawLine(pen, 2, 5, 13, 5);
                }
                using (var fillBrush = new SolidBrush(Color.FromArgb(96, 205, 255)))
                {
                    g.FillRectangle(fillBrush, 5, 7, 5, 4);
                }
                break;

            case "minimize":
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1.8f))
                {
                    g.DrawLine(pen, 2, 11, 13, 11);
                }
                break;

            case "hide":
                using (var pen = new Pen(Color.FromArgb(210, 210, 210), 1.25f))
                {
                    g.DrawArc(pen, 2, 3, 11, 8, 0, 180);
                    g.DrawArc(pen, 2, 4, 11, 8, 180, 180);
                    g.DrawLine(pen, 2, 13, 13, 2);
                }
                break;

            case "close":
                using (var pen = new Pen(Color.FromArgb(255, 128, 140), 1.5f))
                {
                    g.DrawLine(pen, 3, 3, 12, 12);
                    g.DrawLine(pen, 12, 3, 3, 12);
                }
                break;

            case "settings":
                using (var pen = new Pen(Color.FromArgb(96, 205, 255), 1.25f))
                {
                    g.DrawEllipse(pen, 4, 4, 7, 7);
                    g.DrawLine(pen, 8, 1, 8, 4);
                    g.DrawLine(pen, 8, 11, 8, 14);
                    g.DrawLine(pen, 1, 8, 4, 8);
                    g.DrawLine(pen, 11, 8, 14, 8);
                }
                break;

            case "restore":
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1.25f))
                {
                    g.DrawRectangle(pen, 2, 3, 11, 8);
                    g.DrawLine(pen, 2, 9, 13, 9);
                }
                break;

            case "quit":
                using (var pen = new Pen(Color.FromArgb(255, 128, 140), 1.35f))
                {
                    g.DrawArc(pen, 3, 3, 10, 10, -50, 280);
                    g.DrawLine(pen, 8, 1, 8, 6);
                }
                break;
        }

        return bmp;
    }

    public sealed class Win11DarkContextMenuRenderer : Forms.ToolStripProfessionalRenderer
    {
        public Win11DarkContextMenuRenderer() : base(new Win11DarkMenuColorTable())
        {
        }

        protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Color.FromArgb(44, 44, 44));
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderImageMargin(Forms.ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Color.FromArgb(44, 44, 44));
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(58, 58, 58), 1);
            var r = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            e.Graphics.DrawRectangle(pen, r);
        }

        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Enabled) return;

            if (e.Item.Selected || e.Item.Pressed)
            {
                var bounds = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
                bool isDanger = string.Equals(e.Item.Tag as string, "danger", StringComparison.OrdinalIgnoreCase);
                Color bgColor = isDanger ? Color.FromArgb(68, 32, 38) : Color.FromArgb(60, 60, 60);
                using var brush = new SolidBrush(bgColor);
                using var path = CreateRoundedRect(bounds, 4);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
            }
        }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            bool isDanger = string.Equals(e.Item.Tag as string, "danger", StringComparison.OrdinalIgnoreCase);
            Color textColor = isDanger
                ? Color.FromArgb(255, 153, 164)
                : (e.Item.Selected ? Color.White : e.TextColor);

            e.TextColor = textColor;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using var pen = new Pen(Color.FromArgb(58, 58, 58), 1);
            e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
        }

        private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public sealed class Win11DarkMenuColorTable : Forms.ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(44, 44, 44);
        public override Color MenuBorder => Color.FromArgb(58, 58, 58);
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Color.FromArgb(60, 60, 60);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(60, 60, 60);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(60, 60, 60);
        public override Color SeparatorDark => Color.FromArgb(58, 58, 58);
        public override Color SeparatorLight => Color.FromArgb(58, 58, 58);
        public override Color ImageMarginGradientBegin => Color.FromArgb(44, 44, 44);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(44, 44, 44);
        public override Color ImageMarginGradientEnd => Color.FromArgb(44, 44, 44);
    }

    private void CloseAllFlyouts(Window? except = null)
    {
        if (_brightnessFlyout != null && _brightnessFlyout != except && _brightnessFlyout.IsVisible)
            _brightnessFlyout.Hide();
        if (_volumeFlyout != null && _volumeFlyout != except && _volumeFlyout.IsVisible)
            _volumeFlyout.Hide();
        if (_controlCenterFlyout != null && _controlCenterFlyout != except && _controlCenterFlyout.IsVisible)
            _controlCenterFlyout.Hide();
        if (_clockFlyout != null && _clockFlyout != except && _clockFlyout.IsVisible)
            _clockFlyout.Hide();
        if (_systemMonitorFlyout != null && _systemMonitorFlyout != except && _systemMonitorFlyout.IsVisible)
            _systemMonitorFlyout.Hide();
        if (_appLauncherFlyout != null && _appLauncherFlyout != except && _appLauncherFlyout.IsVisible)
            _appLauncherFlyout.Hide();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_MOUSEACTIVATE = 0x0021;
        const int MA_ACTIVATE = 1;
        if (msg == WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(MA_ACTIVATE);
        }

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
        else if (msg == (Win32Api.WM_USER + 101))
        {
            if (wParam.ToInt32() == Win32Api.ABN_FULLSCREENAPP)
            {
                bool isFsOpening = lParam != IntPtr.Zero;
                if (isFsOpening)
                {
                    SetFullscreenState(true);
                }
                else
                {
                    CheckFullscreenApp();
                }
            }
        }
        return IntPtr.Zero;
    }

    private void CheckFullscreenApp()
    {
        bool isFs = DetectFullscreenWindow();
        SetFullscreenState(isFs);
    }

    private void SetFullscreenState(bool isFullscreen)
    {
        if (_isFullscreenActive == isFullscreen) return;
        _isFullscreenActive = isFullscreen;

        Dispatcher.Invoke(() =>
        {
            if (_isFullscreenActive)
            {
                _brightnessFlyout?.Hide();
                _controlCenterFlyout?.Hide();
                Visibility = Visibility.Hidden;
            }
            else
            {
                if (_shellState.Settings.EnableWinZenTaskbar)
                {
                    Visibility = Visibility.Visible;
                    _shellState.SyncTaskbarAndWorkArea();
                }
            }
        });
    }

    private bool DetectFullscreenWindow()
    {
        IntPtr fgWnd = Win32Api.GetForegroundWindow();
        if (fgWnd == IntPtr.Zero) return false;

        // Ignore our own taskbar and flyouts
        if (fgWnd == _hwnd) return _isFullscreenActive;
        if (_controlCenterFlyout != null && _controlCenterFlyout.IsVisible)
        {
            try
            {
                if (new WindowInteropHelper(_controlCenterFlyout).Handle == fgWnd)
                    return false;
            }
            catch { }
        }
        if (_brightnessFlyout != null && _brightnessFlyout.IsVisible)
        {
            try
            {
                if (new WindowInteropHelper(_brightnessFlyout).Handle == fgWnd)
                    return false;
            }
            catch { }
        }

        // Ignore shell desktop window (Progman, WorkerW, native taskbar)
        IntPtr shellWnd = Win32Api.GetShellWindow();
        if (fgWnd == shellWnd) return false;

        var sbClass = new StringBuilder(256);
        Win32Api.GetClassName(fgWnd, sbClass, sbClass.Capacity);
        string className = sbClass.ToString();
        if (className == "Progman" || className == "WorkerW" || className == "Shell_TrayWnd" || className == "Shell_SecondaryTrayWnd")
            return false;

        if (className.Contains("WinZenShell") || className.StartsWith("HwndWrapper[WinZenShell", StringComparison.OrdinalIgnoreCase))
            return false;

        // Must be visible, non-minimized, and non-cloaked
        if (!Win32Api.IsWindowVisible(fgWnd) || Win32Api.IsIconic(fgWnd))
            return false;

        if (Win32Api.DwmGetWindowAttribute(fgWnd, Win32Api.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            return false;

        // Get window bounds
        if (!Win32Api.GetWindowRect(fgWnd, out Win32Api.RECT fgRect))
            return false;

        // Get monitor info of foreground window
        IntPtr hMon = Win32Api.MonitorFromWindow(fgWnd, Win32Api.MONITOR_DEFAULTTONEAREST);
        if (hMon == IntPtr.Zero) return false;

        var mi = new Win32Api.MONITORINFO { cbSize = Marshal.SizeOf<Win32Api.MONITORINFO>() };
        if (!Win32Api.GetMonitorInfo(hMon, ref mi)) return false;

        // If multi-monitor, verify window is on the same monitor as the taskbar
        IntPtr tbMon = Win32Api.MonitorFromWindow(_hwnd, Win32Api.MONITOR_DEFAULTTONEAREST);
        if (tbMon != IntPtr.Zero && hMon != tbMon) return false;

        // Check if foreground window completely covers or exceeds the monitor
        bool coversMonitor = fgRect.Left <= mi.rcMonitor.Left &&
                             fgRect.Top <= mi.rcMonitor.Top &&
                             fgRect.Right >= mi.rcMonitor.Right &&
                             fgRect.Bottom >= mi.rcMonitor.Bottom;

        if (!coversMonitor) return false;

        // If RespectDesktopWorkArea is enabled, a standard maximized window's rect is bounded by rcWork.
        // It cannot reach or exceed rcMonitor.Bottom.
        // Therefore, if it covers the entire rcMonitor, it is running in true fullscreen (video, game, F11, etc.)!
        if (_shellState.Settings.RespectDesktopWorkArea)
            return true;

        // If work area is not respected, check whether window style removes WS_CAPTION
        int style = Win32Api.GetWindowLong(fgWnd, Win32Api.GWL_STYLE);
        bool hasCaption = (style & Win32Api.WS_CAPTION) == Win32Api.WS_CAPTION;
        return !hasCaption;
    }
}
