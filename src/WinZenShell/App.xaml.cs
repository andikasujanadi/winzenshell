using System;
using System.Drawing;
using System.Reflection;
using System.Windows;
using WinZenShell.Native;
using WinZenShell.Services;
using WinZenShell.Windows;
using Forms = System.Windows.Forms;

namespace WinZenShell;

public partial class App : System.Windows.Application
{
    private NativeTaskbarService? _nativeTaskbarService;
    private WorkAreaAppBarService? _workAreaService;
    private WidgetManagerService? _widgetManagerService;
    private SystemMetricsService? _systemMetricsService;
    private BrightnessService? _brightnessService;
    private IconPackService? _iconPackService;
    private AppLauncherService? _appLauncherService;
    private ShellStateService? _shellStateService;

    private TaskbarWebWindow? _taskbarWindow;
    private SettingsWebWindow? _settingsWindow;
    private DynamicIslandWindow? _dynamicIslandWindow;
    private WinKeyHookService? _winKeyHookService;
    private Forms.NotifyIcon? _trayIcon;
    private static System.Threading.Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            string configDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinZenShell");
            System.IO.Directory.CreateDirectory(configDir);
            string logPath = System.IO.Path.Combine(configDir, "startup.log");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Starting PID {Environment.ProcessId}\n");
            if (!Win32Api.EnsureInteractiveDefaultDesktop())
            {
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] EnsureInteractiveDefaultDesktop returned false, exiting initial process\n");
                Environment.Exit(0);
                return;
            }
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] EnsureInteractiveDefaultDesktop returned true\n");

            try
            {
                int currentPid = Environment.ProcessId;
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("WinZenShell"))
                {
                    if (proc.Id != currentPid)
                    {
                        try
                        {
                            proc.Kill();
                            proc.WaitForExit(1500);
                        }
                        catch { }
                    }
                    proc.Dispose();
                }
            }
            catch { }
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Kill previous instances complete\n");

            try
            {
                _singleInstanceMutex = new System.Threading.Mutex(true, "WinZenShell_SingleInstance_Mutex", out _);
            }
            catch { }

            base.OnStartup(e);
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] base.OnStartup complete\n");

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

        AppDomain.CurrentDomain.UnhandledException += (_, eArgs) =>
        {
            try
            {
                string cLog = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinZenShell", "crash.log");
                System.IO.File.AppendAllText(cLog, $"[{DateTime.Now}] AppDomain Exception: {eArgs.ExceptionObject}\n");
            }
            catch { }
            SafeCleanupAndRestore();
        };
        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                string cLog = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinZenShell", "crash.log");
                System.IO.File.AppendAllText(cLog, $"[{DateTime.Now}] Dispatcher Exception: {args.Exception}\n");
            }
            catch { }
            SafeCleanupAndRestore();
            args.Handled = true;
        };

            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Creating services...\n");
            _nativeTaskbarService = new NativeTaskbarService();
            _workAreaService = new WorkAreaAppBarService();
            _widgetManagerService = new WidgetManagerService();
            _brightnessService = new BrightnessService();
            _iconPackService = new IconPackService();
            _appLauncherService = new AppLauncherService(_iconPackService);
            _systemMetricsService = new SystemMetricsService
            {
                Brightness = _brightnessService,
                IconPacks = _iconPackService
            };
            _iconPackService.ActivePackChanged += () => _systemMetricsService.ClearIconCaches();

            _shellStateService = new ShellStateService(
                _nativeTaskbarService,
                _workAreaService,
                _widgetManagerService,
                _systemMetricsService,
                _iconPackService);

            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Creating TaskbarWebWindow...\n");
            _taskbarWindow = new TaskbarWebWindow(
                _shellStateService,
                _systemMetricsService,
                _brightnessService,
                _appLauncherService,
                ShowAndFocusSettingsWindow);

            _shellStateService.GetTaskbarHwnd = () => _taskbarWindow != null ? _taskbarWindow.Handle : IntPtr.Zero;
            _shellStateService.GetDynamicIslandHwnd = () => _dynamicIslandWindow != null ? _dynamicIslandWindow.Handle : IntPtr.Zero;

            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Creating SettingsWebWindow...\n");
            try
            {
                _settingsWindow = new SettingsWebWindow(_shellStateService, QuitApplication, RestartApplication);
                MainWindow = _settingsWindow;
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] SettingsWebWindow created successfully\n");
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] new SettingsWebWindow exception: {ex}\n");
                throw;
            }

            try
            {
                InitializeSystemTray();
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] InitializeSystemTray complete\n");
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] InitializeSystemTray exception: {ex}\n");
            }

            if (_shellStateService.Settings.EnableWinZenTaskbar)
            {
                _taskbarWindow.Show();
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] _taskbarWindow.Show() complete\n");
            }

            bool isAutoStart = Array.Exists(e.Args, a => string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase));
            if (!isAutoStart)
            {
                try
                {
                    _settingsWindow.Show();
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] _settingsWindow.Show() complete\n");
                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] _settingsWindow.Show() exception: {ex}\n");
                }
            }
            try
            {
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Wiring DynamicIslandVisibilityChanged...\n");
                _shellStateService.DynamicIslandVisibilityChanged += UpdateDynamicIslandVisibility;
                _winKeyHookService = new WinKeyHookService();
                _winKeyHookService.WinKeySoloPressed += () => Dispatcher.BeginInvoke(() => _taskbarWindow?.ToggleAppLauncherFromWinKey());
                _shellStateService.WinKeyOverrideChanged += enabled => Dispatcher.BeginInvoke(() => _winKeyHookService?.SetEnabled(enabled));
                _winKeyHookService.SetEnabled(_shellStateService.Settings.OverrideWindowsKey);
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] SyncTaskbarAndWorkArea...\n");
                _shellStateService.SyncTaskbarAndWorkArea();
                if (_shellStateService.Settings.HideNativeTaskbar)
                {
                    _ = _shellStateService.ToggleHideNativeTaskbarAsync(true);
                }
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] UpdateDynamicIslandVisibility...\n");
                UpdateDynamicIslandVisibility();
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Shell started successfully!\n");
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Exception during shell startup finalization: {ex}\n");
                throw;
            }
        }
        catch (Exception ex)
        {
            try
            {
                string logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinZenShell", "startup.log");
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] OnStartup Exception: {ex}\n");
                string cLog = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinZenShell", "crash.log");
                System.IO.File.AppendAllText(cLog, $"[{DateTime.Now}] OnStartup Exception: {ex}\n");
            }
            catch { }
            SafeCleanupAndRestore();
        }
    }

    public void QuitApplication()
    {
        try { System.IO.File.WriteAllText("quit_reason.txt", "QuitApplication called at:\n" + Environment.StackTrace); } catch { }
        Dispatcher.Invoke(() =>
        {
            SafeCleanupAndRestore();
            if (_settingsWindow != null)
            {
                _settingsWindow.AllowClose = true;
                _settingsWindow.Close();
            }
            if (_taskbarWindow != null)
            {
                _taskbarWindow.Close();
            }
            Shutdown();
        });
    }

    public void RestartApplication()
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c timeout /t 1 /nobreak >nul & start \"\" \"{exePath}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                    };
                    System.Diagnostics.Process.Start(psi);
                }
            }
            catch { }

            SafeCleanupAndRestore();
            if (_settingsWindow != null)
            {
                _settingsWindow.AllowClose = true;
                _settingsWindow.Close();
            }
            if (_taskbarWindow != null)
            {
                _taskbarWindow.Close();
            }

            try
            {
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
                _singleInstanceMutex = null;
            }
            catch { }

            Shutdown();
            Environment.Exit(0);
        });
    }

    private void ShowAndFocusSettingsWindow()
    {
        Dispatcher.Invoke(() =>
        {
            if (_settingsWindow == null) return;
            if (!_settingsWindow.IsVisible)
                _settingsWindow.Show();
            if (_settingsWindow.WindowState == WindowState.Minimized)
                _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Topmost = true;
            _settingsWindow.Activate();
            _settingsWindow.Focus();
            _settingsWindow.Topmost = false;
        });
    }

    private void InitializeSystemTray()
    {
        Icon trayIcon = SystemIcons.Application;
        try
        {
            var res = GetResourceStream(new Uri("pack://application:,,,/Assets/winzen.ico"));
            if (res?.Stream != null)
            {
                trayIcon = new Icon(res.Stream);
            }
        }
        catch { }

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = trayIcon,
            Text = "WinZenShell — Click to Open Settings or Quit",
            Visible = true
        };

        var menu = new Forms.ContextMenuStrip
        {
            Renderer = new TaskbarWebWindow.Win11DarkContextMenuRenderer(),
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

        var openItem = new Forms.ToolStripMenuItem("Open Settings")
        {
            ForeColor = Color.FromArgb(245, 245, 245),
            Padding = new Forms.Padding(6, 5, 10, 5),
            Image = TaskbarWebWindow.CreateFluentIcon("settings", false, TaskbarWebWindow.ParseDrawingAccentColor(_shellStateService?.Settings.AccentColor))
        };
        openItem.Click += (_, _) => ShowAndFocusSettingsWindow();

        var restoreItem = new Forms.ToolStripMenuItem("Restore Native Taskbar")
        {
            ForeColor = Color.FromArgb(220, 220, 220),
            Padding = new Forms.Padding(6, 5, 10, 5),
            Image = TaskbarWebWindow.CreateFluentIcon("restore")
        };
        restoreItem.Click += (_, _) => _shellStateService?.EmergencyRestoreAll();

        var restartItem = new Forms.ToolStripMenuItem("Restart WinZen")
        {
            ForeColor = Color.FromArgb(220, 220, 220),
            Padding = new Forms.Padding(6, 5, 10, 5),
            Image = TaskbarWebWindow.CreateFluentIcon("restart")
        };
        restartItem.Click += (_, _) => RestartApplication();

        var quitItem = new Forms.ToolStripMenuItem("Quit WinZen")
        {
            ForeColor = Color.FromArgb(255, 153, 164),
            Padding = new Forms.Padding(6, 5, 10, 5),
            Image = TaskbarWebWindow.CreateFluentIcon("quit"),
            Tag = "danger"
        };
        quitItem.Click += (_, _) => QuitApplication();

        menu.Items.Add(openItem);
        menu.Items.Add(restoreItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(restartItem);
        menu.Items.Add(quitItem);

        _trayIcon.ContextMenuStrip = menu;

        // Open tray menu on LEFT click (as well as right click) so user can choose "Open Settings" or "Quit WinZen"
        _trayIcon.MouseUp += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                try
                {
                    MethodInfo? mi = typeof(Forms.NotifyIcon).GetMethod(
                        "ShowContextMenu",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (mi != null)
                    {
                        mi.Invoke(_trayIcon, null);
                    }
                    else
                    {
                        menu.Show(Forms.Cursor.Position);
                    }
                }
                catch
                {
                    ShowAndFocusSettingsWindow();
                }
            }
        };
    }

    public sealed class Win11DarkTrayColorTable : Forms.ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(43, 43, 43);
        public override Color MenuBorder => Color.FromArgb(64, 64, 64);
        public override Color MenuItemBorder => Color.FromArgb(80, 80, 80);
        public override Color MenuItemSelected => Color.FromArgb(58, 58, 58);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(58, 58, 58);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(58, 58, 58);
        public override Color SeparatorDark => Color.FromArgb(64, 64, 64);
        public override Color SeparatorLight => Color.FromArgb(64, 64, 64);
        public override Color ImageMarginGradientBegin => Color.FromArgb(43, 43, 43);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(43, 43, 43);
        public override Color ImageMarginGradientEnd => Color.FromArgb(43, 43, 43);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            string logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinZenShell", "startup.log");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] OnExit called: ExitCode={e.ApplicationExitCode}\n{Environment.StackTrace}\n");
        }
        catch { }
        SafeCleanupAndRestore();
        _winKeyHookService?.Dispose();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        if (_singleInstanceMutex != null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch { }
            _singleInstanceMutex.Dispose();
        }
        base.OnExit(e);
    }

    private void UpdateDynamicIslandVisibility()
    {
        if (_shellStateService == null) return;
        Dispatcher.Invoke(() =>
        {
            string logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinZenShell", "startup.log");
            try
            {
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] UpdateDynamicIslandVisibility called\n");
                var di = _shellStateService.Settings.DynamicIsland;
                bool isDiEnabled = di != null && di.Enabled;
                bool shouldShowStandalone = isDiEnabled && di != null &&
                    string.Equals(di.Placement, "top-center", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(_shellStateService.Settings.DockEdge, "Top", StringComparison.OrdinalIgnoreCase);

                if (isDiEnabled)
                {
                    if (_dynamicIslandWindow == null)
                    {
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Creating DynamicIslandWindow...\n");
                        _dynamicIslandWindow = new DynamicIslandWindow(_shellStateService, _taskbarWindow?.MediaService);
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] DynamicIslandWindow created\n");
                    }
                    if (_taskbarWindow != null)
                    {
                        _taskbarWindow.DynamicIslandOverlayWindow = _dynamicIslandWindow;
                    }
                }

                if (shouldShowStandalone)
                {
                    if (_dynamicIslandWindow != null)
                    {
                        _dynamicIslandWindow.Owner = null;
                        _dynamicIslandWindow.Topmost = true;
                        if (!_dynamicIslandWindow.IsOverlayOpen && !_dynamicIslandWindow.IsExpanded)
                        {
                            _dynamicIslandWindow.EnsureWindowSize(440, 44);
                        }
                        _dynamicIslandWindow.Show();
                        _dynamicIslandWindow.UpdatePosition();
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] DynamicIslandWindow shown standalone\n");
                    }
                }
                else
                {
                    if (_dynamicIslandWindow != null && !_dynamicIslandWindow.IsOverlayOpen)
                    {
                        _dynamicIslandWindow.Hide();
                    }
                }
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] UpdateDynamicIslandVisibility EXCEPTION: {ex}\n");
            }
        });
    }

    private void SafeCleanupAndRestore()
    {
        try
        {
            if (_dynamicIslandWindow != null)
            {
                _dynamicIslandWindow.Close();
                _dynamicIslandWindow = null;
            }
            _workAreaService?.UnregisterAllAppBars();
            if (_nativeTaskbarService != null && _nativeTaskbarService.IsHiddenByUs)
            {
                _nativeTaskbarService.RestoreNativeTaskbar();
            }
        }
        catch
        {
            // Ignore cleanup exceptions
        }
    }
}
