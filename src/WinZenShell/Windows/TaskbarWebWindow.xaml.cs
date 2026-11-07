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
    private WindowSelectorFlyoutWindow? _windowSelectorFlyout;
    private FileSystemWatcher? _triggerWatcher;
    private Forms.ContextMenuStrip? _activeNativeMenu;
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
        TaskbarWebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 28, 28, 28);
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

            if (cmd.StartsWith("capture_settings", StringComparison.OrdinalIgnoreCase))
            {
                var parts = cmd.Split(' ');
                string outPath = parts.Length > 1 ? parts[1] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings_capture.png");
                string targetTab = parts.Length > 2 ? parts[2] : "tab-shell";
                Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        var settingsWin = System.Windows.Application.Current.Windows.OfType<SettingsWebWindow>().FirstOrDefault();
                        if (settingsWin != null && settingsWin.SettingsWebView?.CoreWebView2 != null)
                        {
                            if (!string.IsNullOrWhiteSpace(targetTab))
                            {
                                await settingsWin.SettingsWebView.CoreWebView2.ExecuteScriptAsync($"window.switchTab && window.switchTab('{targetTab}');");
                                await System.Threading.Tasks.Task.Delay(400);
                            }
                            if (parts.Length > 3)
                            {
                                string script = string.Join(" ", System.Linq.Enumerable.Skip(parts, 3));
                                await settingsWin.SettingsWebView.CoreWebView2.ExecuteScriptAsync(script);
                                await System.Threading.Tasks.Task.Delay(300);
                            }
                            using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None);
                            await settingsWin.SettingsWebView.CoreWebView2.CapturePreviewAsync(
                                Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,
                                fs);
                            await fs.FlushAsync();
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
                        _brightnessFlyout.ShowAt(mon.Right - 420, 34, Top, Height, _shellState.Settings.DockEdge, _shellState.Settings.Theme, _shellState.Settings.AccentColor);
                    }
                    else if (cmd.StartsWith("open_volume", StringComparison.OrdinalIgnoreCase))
                    {
                        _volumeFlyout ??= new VolumeFlyoutWindow(_audioService);
                        _volumeFlyout.ShowAt(mon.Right - 420, 34, Top, Height, _shellState.Settings.DockEdge, _shellState.Settings.Theme, _shellState.Settings.AccentColor);
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
                    else if (cmd.StartsWith("open_window_selector", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = cmd.Split(' ');
                        string outPath = parts.Length > 1 ? parts[1] : "";
                        bool showThumbMode = !cmd.Contains("list_mode", StringComparison.OrdinalIgnoreCase);
                        var snap = _metricsService.GetSnapshot(_hwnd, IntPtr.Zero);
                        var multiGroup = System.Linq.Enumerable.FirstOrDefault(
                            System.Linq.Enumerable.GroupBy(snap.Windows, w => (w.AppId ?? w.ProcessName).ToLowerInvariant()),
                            g => System.Linq.Enumerable.Count(g) > 1);
                        var targetWins = multiGroup != null
                            ? System.Linq.Enumerable.ToList(multiGroup)
                            : System.Linq.Enumerable.Take(snap.Windows, 2).ToList();
                        if (targetWins.Count > 0)
                        {
                            var first = targetWins[0];
                            var entries = System.Linq.Enumerable.Select(targetWins, w => new SelectorWindowEntry
                            {
                                Hwnd = w.Hwnd,
                                Title = w.Title,
                                IsActive = w.IsActive,
                                IsMinimized = w.IsMinimized
                            }).ToList();

                            _windowSelectorFlyout ??= new WindowSelectorFlyoutWindow(_metricsService, BroadcastMetrics);
                            _windowSelectorFlyout.ShowAt(
                                first.AppId,
                                first.ProcessName,
                                first.IconDataUrl,
                                entries,
                                mon.Left + (mon.Width / 2.0) - 20,
                                38,
                                Top,
                                Height,
                                _shellState.Settings.DockEdge,
                                _shellState.Settings.Theme,
                                showThumbMode,
                                false,
                                _shellState.Settings.AccentColor);

                            if (!string.IsNullOrWhiteSpace(outPath))
                            {
                                _windowSelectorFlyout.CaptureToFile(outPath);
                            }
                        }
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
                    else if (cmd.StartsWith("trigger_menu", StringComparison.OrdinalIgnoreCase))
                    {
                        var sampleDoc = JsonDocument.Parse("{\"appId\":\"firefox\",\"processName\":\"Firefox\",\"title\":\"Mozilla Firefox\",\"windows\":[{\"hwnd\":12345,\"title\":\"Mozilla Firefox\",\"isActive\":true,\"isMinimized\":false}]}");
                        ShowNativeWindowContextMenu(sampleDoc.RootElement);
                    }
                    else if (cmd.StartsWith("reload_settings", StringComparison.OrdinalIgnoreCase))
                    {
                        _shellState.LoadSettings();
                        BroadcastMetrics();
                    }
                    else if (cmd.StartsWith("capture_menu", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = cmd.Split(' ');
                        string outPath = parts.Length > 1 ? parts[1] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "menu_capture.png");
                        if (_activeNativeMenu != null)
                        {
                            using var bmp = new Bitmap(_activeNativeMenu.Width, _activeNativeMenu.Height);
                            _activeNativeMenu.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                            bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
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
            ApplyTaskbarCornerRegion();
        });
    }

    private void ApplyTaskbarCornerRegion()
    {
        int margin = _shellState.Settings.FloatingMarginPx;
        int radius = _shellState.Settings.TaskbarCornerRadiusPx;
        bool isLight = string.Equals(_shellState.Settings.Theme, "light", StringComparison.OrdinalIgnoreCase);

        OuterBorder.Background = isLight
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF3, 0xF3, 0xF3))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C, 0x1C, 0x1C));

        TaskbarWebView.DefaultBackgroundColor = isLight
            ? System.Drawing.Color.FromArgb(255, 0xF3, 0xF3, 0xF3)
            : System.Drawing.Color.FromArgb(255, 0x1C, 0x1C, 0x1C);

        if (_hwnd == IntPtr.Zero) return;

        // Clear any legacy 1-bit GDI region so DWM hardware anti-aliased corners are active
        Win32Api.SetWindowRgn(_hwnd, IntPtr.Zero, true);

        int darkAttr = isLight ? 0 : 1;
        Win32Api.DwmSetWindowAttribute(_hwnd, Win32Api.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkAttr, sizeof(int));

        if (margin > 0 && radius > 0)
        {
            // DWMWCP_ROUNDSMALL (3) for small radii, DWMWCP_ROUND (2) for standard/large radii
            int cornerPref = radius <= 5 ? 3 : 2;
            Win32Api.DwmSetWindowAttribute(_hwnd, Win32Api.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

            // DWMWA_BORDER_COLOR (34): subtle hardware anti-aliased border (COLORREF 0x00BBGGRR)
            int borderColor = isLight ? 0x00D6D6D6 : 0x00303030;
            Win32Api.DwmSetWindowAttribute(_hwnd, 34, ref borderColor, sizeof(int));
        }
        else
        {
            // DWMWCP_DONOTROUND (1)
            int cornerPref = 1;
            Win32Api.DwmSetWindowAttribute(_hwnd, Win32Api.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

            // DWMWA_COLOR_NONE (0xFFFFFFFE)
            int borderColor = unchecked((int)0xFFFFFFFE);
            Win32Api.DwmSetWindowAttribute(_hwnd, 34, ref borderColor, sizeof(int));
        }
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
        Dispatcher.Invoke(() =>
        {
            ApplyTaskbarCornerRegion();
            PushStateToWeb(payload);
        });
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
        snapshot.PinnedApps = _shellState.Settings.PinnedApps ?? new();
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
                        bool? wasActive = root.TryGetProperty("wasActive", out var waProp) &&
                                          (waProp.ValueKind == JsonValueKind.True || waProp.ValueKind == JsonValueKind.False)
                            ? waProp.GetBoolean()
                            : null;
                        Dispatcher.Invoke(() => CloseAllFlyouts());
                        _metricsService.ActivateOrToggleWindow(hwndVal, wasActive);
                        System.Threading.Tasks.Task.Delay(60).ContinueWith(_ => Dispatcher.Invoke(BroadcastMetrics));
                    }
                    break;

                case "hover_window_selector":
                case "toggle_window_selector":
                    bool isHoverReq = string.Equals(action, "hover_window_selector", StringComparison.OrdinalIgnoreCase);
                    string wsAppId = root.TryGetProperty("appId", out var wsAidProp) ? wsAidProp.GetString() ?? "" : "";
                    string wsProc = root.TryGetProperty("processName", out var wsProcProp) ? wsProcProp.GetString() ?? "App" : "App";
                    string wsIcon = root.TryGetProperty("iconDataUrl", out var wsIcoProp) ? wsIcoProp.GetString() ?? "" : "";
                    double wsX = root.TryGetProperty("x", out var wsXProp) && wsXProp.TryGetDouble(out double wsXd) ? wsXd : 0;
                    double wsW = root.TryGetProperty("width", out var wsWProp) && wsWProp.TryGetDouble(out double wsWd) ? wsWd : 38;
                    bool wsShowThumbs = !root.TryGetProperty("showThumbnails", out var wsStProp) ||
                                        wsStProp.ValueKind != JsonValueKind.False;
                    var wsList = new List<SelectorWindowEntry>();
                    if (root.TryGetProperty("windows", out var wsWins) && wsWins.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in wsWins.EnumerateArray())
                        {
                            wsList.Add(new SelectorWindowEntry
                            {
                                Hwnd = item.TryGetProperty("hwnd", out var hP) && hP.TryGetInt64(out long hv) ? hv : 0,
                                Title = item.TryGetProperty("title", out var tP) ? tP.GetString() ?? "" : "",
                                IsActive = item.TryGetProperty("isActive", out var aP) && aP.GetBoolean(),
                                IsMinimized = item.TryGetProperty("isMinimized", out var mP) && mP.GetBoolean()
                            });
                        }
                    }

                    Dispatcher.Invoke(() =>
                    {
                        _windowSelectorFlyout ??= new WindowSelectorFlyoutWindow(_metricsService, BroadcastMetrics);
                        bool sameApp = string.Equals(_windowSelectorFlyout.CurrentAppId, wsAppId, StringComparison.OrdinalIgnoreCase);

                        if (!isHoverReq)
                        {
                            bool wasAlreadyPinned = sameApp && _windowSelectorFlyout.IsVisible && _windowSelectorFlyout.IsPinnedByClick;
                            CloseAllFlyouts();
                            if (wasAlreadyPinned || wsList.Count == 0)
                            {
                                return;
                            }
                        }
                        else
                        {
                            CloseAllFlyouts(_windowSelectorFlyout);
                            if (wsList.Count == 0)
                            {
                                _windowSelectorFlyout.HideSelector();
                                return;
                            }
                        }

                        _windowSelectorFlyout.ShowAt(
                            wsAppId,
                            wsProc,
                            wsIcon,
                            wsList,
                            Left + wsX,
                            wsW,
                            Top,
                            Height,
                            _shellState.Settings.DockEdge,
                            _shellState.Settings.Theme,
                            wsShowThumbs,
                            isHoverReq,
                            _shellState.Settings.AccentColor);
                    });
                    break;

                case "leave_window_selector":
                    Dispatcher.Invoke(() =>
                    {
                        if (_windowSelectorFlyout != null && _windowSelectorFlyout.IsVisible)
                        {
                            _windowSelectorFlyout.NotifyTaskbarHoverLeave();
                        }
                    });
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

                case "pin_app":
                    string pAppId = root.TryGetProperty("appId", out var pai) ? pai.GetString() ?? "" : "";
                    string pProc = root.TryGetProperty("processName", out var ppn) ? ppn.GetString() ?? "" : "";
                    string pTitle = root.TryGetProperty("title", out var ppt) ? ppt.GetString() ?? "" : "";
                    string pExe = root.TryGetProperty("exePath", out var ppe) ? ppe.GetString() ?? "" : "";
                    string pIcon = root.TryGetProperty("iconDataUrl", out var ppi) ? ppi.GetString() ?? "" : "";
                    _shellState.PinApp(pAppId, pProc, pTitle, pExe, pIcon);
                    BroadcastMetrics();
                    break;

                case "unpin_app":
                    string uAppId = root.TryGetProperty("appId", out var uai) ? uai.GetString() ?? "" : "";
                    string uProc = root.TryGetProperty("processName", out var upn) ? upn.GetString() ?? "" : "";
                    _shellState.UnpinApp(uAppId, uProc);
                    BroadcastMetrics();
                    break;

                case "launch_app":
                    string lExe = root.TryGetProperty("exePath", out var lei) ? lei.GetString() ?? "" : "";
                    string lName = root.TryGetProperty("processName", out var lpn) ? lpn.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(lExe))
                    {
                        _appLauncherService.Launch(lExe, lName);
                    }
                    else if (!string.IsNullOrWhiteSpace(lName))
                    {
                        _appLauncherService.Launch(lName, lName);
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
                            _brightnessFlyout.ShowAt(Left + bx, bw, Top, Height, _shellState.Settings.DockEdge, _shellState.Settings.Theme, _shellState.Settings.AccentColor);
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
                            _volumeFlyout.ShowAt(Left + vx, vw, Top, Height, _shellState.Settings.DockEdge, _shellState.Settings.Theme, _shellState.Settings.AccentColor);
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

    public static Bitmap? GetBitmapFromDataUrl(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;
        try
        {
            int commaIdx = dataUrl.IndexOf(',');
            string base64 = commaIdx >= 0 ? dataUrl[(commaIdx + 1)..] : dataUrl;
            byte[] bytes = Convert.FromBase64String(base64);
            using var ms = new MemoryStream(bytes);
            using var orig = Image.FromStream(ms);
            return new Bitmap(orig, new System.Drawing.Size(16, 16));
        }
        catch
        {
            return null;
        }
    }

    private void ShowNativeWindowContextMenu(JsonElement root)
    {
        string appId = root.TryGetProperty("appId", out var appIdProp) ? appIdProp.GetString() ?? "" : "";
        string processName = root.TryGetProperty("processName", out var pn) ? pn.GetString() ?? "App" : "App";
        string title = root.TryGetProperty("title", out var tt) ? tt.GetString() ?? processName : processName;
        string iconDataUrl = root.TryGetProperty("iconDataUrl", out var ic) ? ic.GetString() ?? "" : "";
        string exePath = root.TryGetProperty("exePath", out var ep) ? ep.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(iconDataUrl))
        {
            iconDataUrl = _metricsService.GetIconForApp(appId, processName) ?? "";
        }
        if (string.IsNullOrWhiteSpace(exePath))
        {
            exePath = _metricsService.GetExePathForApp(appId, processName) ?? "";
        }

        bool isPinned = _shellState.IsAppPinned(appId, processName);

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

        bool isMulti = windows.Count > 1;
        string headerText = isMulti
            ? $"{processName} ({windows.Count} windows)"
            : (!string.IsNullOrWhiteSpace(title) ? title : processName);

        Bitmap? appIcon = GetBitmapFromDataUrl(iconDataUrl);
        var headerItem = new Forms.ToolStripMenuItem(headerText)
        {
            Enabled = false,
            ForeColor = Color.FromArgb(170, 170, 170),
            Font = new Font("Segoe UI", 9.0f, System.Drawing.FontStyle.Bold),
            Padding = new Forms.Padding(6, 6, 10, 5),
            Image = appIcon ?? CreateFluentIcon("window")
        };
        menu.Items.Add(headerItem);
        menu.Items.Add(new Forms.ToolStripSeparator());

        if (windows.Count == 0)
        {
            // Closed pinned app context menu
            var launchItem = new Forms.ToolStripMenuItem("Open")
            {
                ForeColor = Color.FromArgb(245, 245, 245),
                Padding = new Forms.Padding(6, 5, 10, 5),
                Image = CreateFluentIcon("launch")
            };
            launchItem.Click += (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(exePath))
                    _appLauncherService.Launch(exePath, processName);
                else
                    _appLauncherService.Launch(processName, processName);
            };
            menu.Items.Add(launchItem);

            menu.Items.Add(new Forms.ToolStripSeparator());

            var unpinClosedItem = new Forms.ToolStripMenuItem("Unpin from taskbar")
            {
                ForeColor = Color.FromArgb(245, 245, 245),
                Padding = new Forms.Padding(6, 5, 10, 5),
                Image = CreateFluentIcon("unpin")
            };
            unpinClosedItem.Click += (_, _) =>
            {
                _shellState.UnpinApp(appId, processName);
                BroadcastMetrics();
            };
            menu.Items.Add(unpinClosedItem);

            var cPos = Forms.Cursor.Position;
            _activeNativeMenu = menu;
            menu.Show(cPos, Forms.ToolStripDropDownDirection.AboveRight);
            return;
        }

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

        // Pin to taskbar / Unpin from taskbar
        if (isPinned)
        {
            var unpinItem = new Forms.ToolStripMenuItem("Unpin from taskbar")
            {
                ForeColor = Color.FromArgb(245, 245, 245),
                Padding = new Forms.Padding(6, 5, 10, 5),
                Image = CreateFluentIcon("unpin")
            };
            unpinItem.Click += (_, _) =>
            {
                _shellState.UnpinApp(appId, processName);
                BroadcastMetrics();
            };
            menu.Items.Add(unpinItem);
        }
        else
        {
            var pinItem = new Forms.ToolStripMenuItem("Pin to taskbar")
            {
                ForeColor = Color.FromArgb(245, 245, 245),
                Padding = new Forms.Padding(6, 5, 10, 5),
                Image = CreateFluentIcon("pin")
            };
            pinItem.Click += (_, _) =>
            {
                _shellState.PinApp(appId, processName, title, exePath, iconDataUrl);
                BroadcastMetrics();
            };
            menu.Items.Add(pinItem);
        }

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
        _activeNativeMenu = menu;
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
                    g.DrawRectangle(pen, 2f, 2.5f, 12f, 10.5f);
                    g.DrawLine(pen, 2f, 5.5f, 14f, 5.5f);
                }
                if (isActive)
                {
                    using var dotBrush = new SolidBrush(Color.FromArgb(96, 205, 255));
                    g.FillEllipse(dotBrush, 4f, 7.5f, 2.5f, 2.5f);
                }
                break;

            case "focus":
                // Layered windows indicating "Switch to window"
                using (var backPen = new Pen(Color.FromArgb(140, 160, 180), 1.1f))
                {
                    g.DrawRectangle(backPen, 2f, 2f, 9f, 8f);
                }
                using (var frontBrush = new SolidBrush(Color.FromArgb(44, 44, 44)))
                {
                    g.FillRectangle(frontBrush, 5f, 5f, 9f, 8f);
                }
                using (var frontPen = new Pen(Color.FromArgb(96, 205, 255), 1.3f))
                {
                    g.DrawRectangle(frontPen, 5f, 5f, 9f, 8f);
                    g.DrawLine(frontPen, 5f, 7.5f, 14f, 7.5f);
                }
                break;

            case "minimize":
                // Centered rounded bar
                using (var pen = new Pen(Color.FromArgb(230, 230, 230), 2.0f))
                {
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    g.DrawLine(pen, 3.5f, 8.0f, 12.5f, 8.0f);
                }
                break;

            case "pin":
                // Windows 11 Fluent angled pushpin
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1.25f))
                using (var brush = new SolidBrush(Color.FromArgb(220, 220, 220)))
                using (var needlePen = new Pen(Color.FromArgb(180, 180, 180), 1.25f))
                {
                    g.DrawLine(needlePen, 6.0f, 10.0f, 2.5f, 13.5f);
                    g.DrawLine(pen, 4.5f, 8.5f, 7.5f, 11.5f);
                    var pts = new PointF[]
                    {
                        new(5.5f, 7.5f),
                        new(8.5f, 10.5f),
                        new(11.0f, 8.0f),
                        new(8.0f, 5.0f)
                    };
                    g.FillPolygon(brush, pts);
                    g.DrawLine(pen, 10.0f, 3.5f, 12.5f, 6.0f);
                }
                break;

            case "unpin":
                // Windows 11 Fluent pushpin with slash
                using (var pen = new Pen(Color.FromArgb(180, 180, 180), 1.25f))
                using (var brush = new SolidBrush(Color.FromArgb(180, 180, 180)))
                using (var needlePen = new Pen(Color.FromArgb(150, 150, 150), 1.25f))
                using (var slashPen = new Pen(Color.FromArgb(255, 120, 135), 1.4f))
                {
                    g.DrawLine(needlePen, 6.0f, 10.0f, 2.5f, 13.5f);
                    g.DrawLine(pen, 4.5f, 8.5f, 7.5f, 11.5f);
                    var pts = new PointF[]
                    {
                        new(5.5f, 7.5f),
                        new(8.5f, 10.5f),
                        new(11.0f, 8.0f),
                        new(8.0f, 5.0f)
                    };
                    g.FillPolygon(brush, pts);
                    g.DrawLine(pen, 10.0f, 3.5f, 12.5f, 6.0f);

                    slashPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    slashPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    g.DrawLine(slashPen, 2.5f, 2.5f, 13.5f, 13.5f);
                }
                break;

            case "hide":
                // Fluent Eye-off
                using (var pen = new Pen(Color.FromArgb(210, 210, 210), 1.25f))
                using (var brush = new SolidBrush(Color.FromArgb(210, 210, 210)))
                {
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;

                    var ptsTop = new PointF[] { new(2.5f, 8.0f), new(8.0f, 4.5f), new(13.5f, 8.0f) };
                    var ptsBot = new PointF[] { new(2.5f, 8.0f), new(8.0f, 11.5f), new(13.5f, 8.0f) };
                    g.DrawCurve(pen, ptsTop, 0.5f);
                    g.DrawCurve(pen, ptsBot, 0.5f);

                    g.FillEllipse(brush, 6.75f, 6.75f, 2.5f, 2.5f);
                    g.DrawLine(pen, 3.0f, 3.0f, 13.0f, 13.0f);
                }
                break;

            case "close":
                // Fluent soft dismiss cross
                using (var pen = new Pen(Color.FromArgb(255, 115, 130), 1.5f))
                {
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    g.DrawLine(pen, 4.0f, 4.0f, 12.0f, 12.0f);
                    g.DrawLine(pen, 12.0f, 4.0f, 4.0f, 12.0f);
                }
                break;

            case "launch":
                // Fluent Open in new window / Launch
                using (var pen = new Pen(Color.FromArgb(96, 205, 255), 1.25f))
                {
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;

                    g.DrawLine(pen, 7.5f, 8.5f, 13.0f, 3.0f);
                    g.DrawLine(pen, 9.5f, 3.0f, 13.0f, 3.0f);
                    g.DrawLine(pen, 13.0f, 3.0f, 13.0f, 6.5f);

                    g.DrawLine(pen, 6.5f, 3.5f, 3.0f, 3.5f);
                    g.DrawLine(pen, 3.0f, 3.5f, 3.0f, 13.0f);
                    g.DrawLine(pen, 3.0f, 13.0f, 12.5f, 13.0f);
                    g.DrawLine(pen, 12.5f, 13.0f, 12.5f, 9.5f);
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
        if (_windowSelectorFlyout != null && _windowSelectorFlyout != except && _windowSelectorFlyout.IsVisible)
            _windowSelectorFlyout.HideSelector();
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
