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
    private ShellStateService? _shellStateService;

    private TaskbarWebWindow? _taskbarWindow;
    private SettingsWebWindow? _settingsWindow;
    private Forms.NotifyIcon? _trayIcon;
    private static System.Threading.Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Automatically terminate any previous instance of WinZenShell so the newest launch always takes over cleanly
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

        _singleInstanceMutex = new System.Threading.Mutex(true, "WinZenShell_SingleInstance_Mutex", out _);

        base.OnStartup(e);

        // Keep running in system tray when Settings window is closed; quit only via explicit Quit action
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Register crash / unhandled exception failsafes so user is never left without a taskbar
        AppDomain.CurrentDomain.UnhandledException += (_, _) => SafeCleanupAndRestore();
        DispatcherUnhandledException += (_, args) =>
        {
            SafeCleanupAndRestore();
            args.Handled = true;
        };

        _nativeTaskbarService = new NativeTaskbarService();
        _workAreaService = new WorkAreaAppBarService();
        _widgetManagerService = new WidgetManagerService();
        _systemMetricsService = new SystemMetricsService();

        _shellStateService = new ShellStateService(
            _nativeTaskbarService,
            _workAreaService,
            _widgetManagerService,
            _systemMetricsService);

        _taskbarWindow = new TaskbarWebWindow(
            _shellStateService,
            _systemMetricsService,
            ShowAndFocusSettingsWindow);

        _shellStateService.GetTaskbarHwnd = () => _taskbarWindow.Handle;

        _settingsWindow = new SettingsWebWindow(_shellStateService, QuitApplication);
        MainWindow = _settingsWindow;

        InitializeSystemTray();

        if (_shellStateService.Settings.EnableWinZenTaskbar)
        {
            _taskbarWindow.Show();
        }

        _settingsWindow.Show();
        _shellStateService.SyncTaskbarAndWorkArea();
    }

    public void QuitApplication()
    {
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

    private void ShowAndFocusSettingsWindow()
    {
        Dispatcher.Invoke(() =>
        {
            if (_settingsWindow == null) return;
            if (!_settingsWindow.IsVisible)
                _settingsWindow.Show();
            if (_settingsWindow.WindowState == WindowState.Minimized)
                _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
        });
    }

    private void InitializeSystemTray()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "WinZenShell — Click to Open Settings or Quit",
            Visible = true
        };

        var menu = new Forms.ContextMenuStrip
        {
            Renderer = new Forms.ToolStripProfessionalRenderer(new Win11DarkTrayColorTable()),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(43, 43, 43),
            ShowImageMargin = false,
            Font = new Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Regular)
        };

        var openItem = new Forms.ToolStripMenuItem("Open Settings")
        {
            ForeColor = Color.White,
            Padding = new Forms.Padding(6, 4, 6, 4)
        };
        openItem.Click += (_, _) => ShowAndFocusSettingsWindow();

        var restoreItem = new Forms.ToolStripMenuItem("Restore Native Taskbar")
        {
            ForeColor = Color.FromArgb(197, 197, 197),
            Padding = new Forms.Padding(6, 4, 6, 4)
        };
        restoreItem.Click += (_, _) => _shellStateService?.EmergencyRestoreAll();

        var quitItem = new Forms.ToolStripMenuItem("Quit WinZen")
        {
            ForeColor = Color.FromArgb(255, 153, 164),
            Padding = new Forms.Padding(6, 4, 6, 4)
        };
        quitItem.Click += (_, _) => QuitApplication();

        menu.Items.Add(openItem);
        menu.Items.Add(restoreItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
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

    private sealed class Win11DarkTrayColorTable : Forms.ProfessionalColorTable
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
        SafeCleanupAndRestore();
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

    private void SafeCleanupAndRestore()
    {
        try
        {
            if (_taskbarWindow != null && _taskbarWindow.Handle != IntPtr.Zero)
            {
                _workAreaService?.UnregisterAppBar(_taskbarWindow.Handle);
            }
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
