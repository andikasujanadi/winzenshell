using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using WinZenShell.Native;
using WinZenShell.Services;
using Forms = System.Windows.Forms;

namespace WinZenShell.Windows;

public partial class AppLauncherFlyoutWindow : Window
{
    private readonly AppLauncherService _appLauncherService;
    private readonly Action _openSettingsCallback;
    private readonly ShellStateService? _shellState;
    private bool _webViewReady;
    private IntPtr _hwnd = IntPtr.Zero;
    private int _lastClipWidth = -1;
    private int _lastClipHeight = -1;
    private double _lastClipDpi = -1;
    private Forms.ContextMenuStrip? _activeNativeMenu;
    private bool _isContextMenuOpen = false;
    public DateTime LastDeactivatedTime { get; private set; } = DateTime.MinValue;

    public AppLauncherFlyoutWindow(
        AppLauncherService appLauncherService,
        Action openSettingsCallback,
        ShellStateService? shellState = null)
    {
        InitializeComponent();
        _appLauncherService = appLauncherService;
        _openSettingsCallback = openSettingsCallback;
        _shellState = shellState;

        Deactivated += (_, _) =>
        {
            if (_isContextMenuOpen) return;
            try
            {
                if (_webViewReady && LauncherWebView.CoreWebView2 != null)
                {
                    LauncherWebView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"launcher_closed\"}");
                }
            }
            catch { }
            FlyoutAnimationHelper.HandleDeactivated(this, () => LastDeactivatedTime = DateTime.UtcNow);
        };
        Activated += (_, _) => FocusSearchInput();
        Loaded += OnLoaded;

        _appLauncherService.AppsLoaded += OnAppsLoaded;
        if (_shellState != null)
        {
            _shellState.StateChanged += payload => Dispatcher.Invoke(() =>
            {
                ApplyTheme(payload.Settings.Theme, payload.Settings.AccentColor);
                if (IsVisible)
                {
                    PushAppsData();
                }
            });
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
        if (_hwnd == IntPtr.Zero || LauncherWebView == null || LauncherWebView.Handle == IntPtr.Zero) return;
        if (Win32Api.GetClientRect(LauncherWebView.Handle, out var clientRect) && clientRect.Width > 0 && clientRect.Height > 0)
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
            if (Win32Api.SetWindowRgn(LauncherWebView.Handle, childRgn, false) == 0)
            {
                Win32Api.DeleteObject(childRgn);
            }
        }
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

        if (LauncherWebView != null)
        {
            var bgCol = FlyoutAnimationHelper.FlyoutBackgroundOpacity < 100
                ? System.Drawing.Color.Transparent
                : (isLight ? System.Drawing.Color.FromArgb(0xF3, 0xF3, 0xF3) : System.Drawing.Color.FromArgb(0x1E, 0x1E, 0x1E));
            if (LauncherWebView.DefaultBackgroundColor != bgCol)
                LauncherWebView.DefaultBackgroundColor = bgCol;
        }

        if (_webViewReady && LauncherWebView?.CoreWebView2 != null)
        {
            string th = isLight ? "light" : "dark";
            string acc = accentColor ?? _shellState?.Settings.AccentColor ?? "#3684E3";
            LauncherWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
            {
                type = "theme_update",
                theme = th,
                accentColor = acc,
                enableAnimations = _shellState?.Settings.EnableAnimations ?? true
            }));
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var env = await WebViewEnvironmentManager.GetSharedEnvironmentAsync();
            await LauncherWebView.EnsureCoreWebView2Async(env);
            bool initLight = string.Equals(_shellState?.Settings.Theme, "light", StringComparison.OrdinalIgnoreCase);
            LauncherWebView.DefaultBackgroundColor = FlyoutAnimationHelper.FlyoutBackgroundOpacity < 100
                ? System.Drawing.Color.Transparent
                : (initLight ? System.Drawing.Color.FromArgb(0xF3, 0xF3, 0xF3) : System.Drawing.Color.FromArgb(0x1E, 0x1E, 0x1E));

            var core = LauncherWebView.CoreWebView2;
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
                ApplyTheme(_shellState?.Settings.Theme, _shellState?.Settings.AccentColor);
                PushAppsData();
            };

            core.Navigate("https://app.winzenshell.local/app-launcher.html");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppLauncher WebView2 init error: {ex.Message}");
        }
    }

    private void OnAppsLoaded()
    {
        Dispatcher.Invoke(() =>
        {
            if (_webViewReady)
            {
                PushAppsData();
            }
        });
    }

    public void PushAppsData()
    {
        if (!_webViewReady || LauncherWebView.CoreWebView2 == null) return;

        try
        {
            var payload = _appLauncherService.GetAppsPayload(_shellState?.Settings.AppLauncher);
            string theme = _shellState?.Settings.Theme ?? "dark";
            string accentColor = _shellState?.Settings.AccentColor ?? "#3684E3";
            string json = JsonSerializer.Serialize(new
            {
                type = "apps_data",
                payload,
                theme,
                accentColor,
                enableAnimations = _shellState?.Settings.EnableAnimations ?? true
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            LauncherWebView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch { }
    }

    private void PostWebMessage(object obj)
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                if (_webViewReady && LauncherWebView.CoreWebView2 != null)
                {
                    string json = JsonSerializer.Serialize(obj, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    LauncherWebView.CoreWebView2.PostWebMessageAsJson(json);
                }
            });
        }
        catch { }
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
                case "request_apps":
                    PushAppsData();
                    break;

                case "launch_app":
                    string target = root.TryGetProperty("target", out var tProp) ? tProp.GetString() ?? "" : "";
                    string name = root.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(target))
                    {
                        _appLauncherService.Launch(target, name);
                        FlyoutAnimationHelper.AnimateHide(this);
                    }
                    break;

                case "show_app_context_menu":
                    {
                        string appName = root.TryGetProperty("name", out var anProp) ? anProp.GetString() ?? "" : "";
                        string targetPath = root.TryGetProperty("target", out var tpProp) ? tpProp.GetString() ?? "" : "";
                        string resolvedExe = root.TryGetProperty("resolvedExePath", out var reProp) ? reProp.GetString() ?? "" : "";
                        string iconDataUrl = root.TryGetProperty("iconDataUrl", out var iduProp) ? iduProp.GetString() ?? "" : "";
                        bool isFolder = root.TryGetProperty("isFolder", out var ifProp) && ifProp.GetBoolean();
                        string folderId = root.TryGetProperty("folderId", out var fidProp) ? fidProp.GetString() ?? "" : "";
                        string section = root.TryGetProperty("section", out var secProp) ? secProp.GetString() ?? "" : "";
                        bool isPinned = root.TryGetProperty("isPinned", out var ipProp)
                            ? ipProp.GetBoolean()
                            : (_shellState?.IsLauncherAppPinned(targetPath, appName) ?? false);

                        ShowAppContextMenu(appName, targetPath, resolvedExe, iconDataUrl, isPinned, isFolder, folderId, section);
                        break;
                    }

                case "dismiss_context_menu":
                    if (_activeNativeMenu != null && _activeNativeMenu.Visible)
                    {
                        _activeNativeMenu.Close();
                    }
                    break;

                case "save_pinned_launcher_apps":
                    {
                        if (root.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                        {
                            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            var list = JsonSerializer.Deserialize<List<AppLauncherPinnedItem>>(itemsProp.GetRawText(), options);
                            if (list != null)
                            {
                                _shellState?.SavePinnedLauncherApps(list);
                            }
                        }
                        break;
                    }

                case "reorder_pinned_launcher_apps":
                    {
                        if (root.TryGetProperty("orderedTargets", out var otProp) && otProp.ValueKind == JsonValueKind.Array)
                        {
                            var list = new System.Collections.Generic.List<string>();
                            foreach (var el in otProp.EnumerateArray())
                            {
                                string? s = el.GetString();
                                if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
                            }
                            _shellState?.ReorderPinnedLauncherApps(list);
                        }
                        break;
                    }

                case "close_launcher":
                    FlyoutAnimationHelper.AnimateHide(this);
                    break;

                case "open_settings":
                    Hide();
                    _openSettingsCallback.Invoke();
                    break;
            }
        }
        catch { }
    }

    private void ShowAppContextMenu(
        string appName,
        string targetPath,
        string resolvedExe,
        string iconDataUrl,
        bool isPinned,
        bool isFolder = false,
        string folderId = "",
        string section = "")
    {
        if (string.IsNullOrWhiteSpace(appName) && string.IsNullOrWhiteSpace(targetPath)) return;

        _activeNativeMenu?.Close();
        _activeNativeMenu?.Dispose();
        _activeNativeMenu = null;

        var menu = new Forms.ContextMenuStrip
        {
            Renderer = new TaskbarWebWindow.Win11DarkContextMenuRenderer(),
            ForeColor = Color.FromArgb(245, 245, 245),
            BackColor = Color.FromArgb(44, 44, 44),
            ShowImageMargin = false,
            ShowCheckMargin = false,
            Font = new Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Regular),
            Padding = new Forms.Padding(4, 4, 4, 4)
        };

        menu.HandleCreated += (_, _) =>
        {
            int corner = 2; // DWMWCP_ROUND
            Win32Api.DwmSetWindowAttribute(menu.Handle, 33, ref corner, sizeof(int));
            int dark = 1;
            Win32Api.DwmSetWindowAttribute(menu.Handle, 20, ref dark, sizeof(int));
        };

        _isContextMenuOpen = true;
        menu.Closed += (_, _) =>
        {
            _isContextMenuOpen = false;
            Dispatcher.BeginInvoke(() =>
            {
                if (_activeNativeMenu == menu)
                    _activeNativeMenu = null;
                menu.Dispose();
            });
        };

        var itemPadding = new Forms.Padding(0, 6, 0, 6);

        // Header with App Name
        var headerItem = new Forms.ToolStripMenuItem(appName)
        {
            Enabled = false,
            ForeColor = Color.FromArgb(170, 170, 170),
            Font = new Font("Segoe UI", 9.0f, System.Drawing.FontStyle.Bold),
            Padding = itemPadding
        };
        menu.Items.Add(headerItem);
        menu.Items.Add(new Forms.ToolStripSeparator());

        if (isFolder)
        {
            var openFolderItem = new Forms.ToolStripMenuItem("Open folder")
            {
                ForeColor = Color.FromArgb(245, 245, 245),
                Padding = itemPadding
            };
            openFolderItem.Click += (_, _) =>
            {
                PostWebMessage(new { type = "open_folder", folderId = string.IsNullOrEmpty(folderId) ? targetPath : folderId });
            };
            menu.Items.Add(openFolderItem);

            var ungroupItem = new Forms.ToolStripMenuItem("Ungroup folder")
            {
                ForeColor = Color.FromArgb(245, 245, 245),
                Padding = itemPadding
            };
            ungroupItem.Click += (_, _) =>
            {
                PostWebMessage(new { type = "ungroup_folder", folderId = string.IsNullOrEmpty(folderId) ? targetPath : folderId });
            };
            menu.Items.Add(ungroupItem);

            menu.Items.Add(new Forms.ToolStripSeparator());

            var unpinFolderItem = new Forms.ToolStripMenuItem("Remove")
            {
                ForeColor = Color.FromArgb(255, 153, 164),
                Padding = itemPadding,
                Tag = "danger"
            };
            unpinFolderItem.Click += (_, _) =>
            {
                _shellState?.UnpinLauncherFolder(string.IsNullOrEmpty(folderId) ? targetPath : folderId);
                PushAppsData();
            };
            menu.Items.Add(unpinFolderItem);

            _activeNativeMenu = menu;
            NativeContextMenuHelper.AttachAutoDismiss(menu);
            menu.Show(Forms.Cursor.Position);
            return;
        }

        // 1. Open
        var openItem = new Forms.ToolStripMenuItem("Open")
        {
            ForeColor = Color.FromArgb(245, 245, 245),
            Padding = itemPadding
        };
        openItem.Click += (_, _) =>
        {
            _appLauncherService.Launch(targetPath, appName);
            FlyoutAnimationHelper.AnimateHide(this);
        };
        menu.Items.Add(openItem);

        // 2. Run as administrator (for Win32 / desktop apps)
        bool isUwpOrProtocol = targetPath.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase) ||
                               targetPath.StartsWith("ms-", StringComparison.OrdinalIgnoreCase) ||
                               targetPath.StartsWith("steam://", StringComparison.OrdinalIgnoreCase);

        if (!isUwpOrProtocol)
        {
            var adminItem = new Forms.ToolStripMenuItem("Run as administrator")
            {
                ForeColor = Color.FromArgb(245, 245, 245),
                Padding = itemPadding
            };
            adminItem.Click += (_, _) =>
            {
                _appLauncherService.Launch(targetPath, appName, runAsAdmin: true, resolvedExe: resolvedExe);
                FlyoutAnimationHelper.AnimateHide(this);
            };
            menu.Items.Add(adminItem);
        }

        // 3. Open file location / App settings
        bool hasFileLocation = File.Exists(targetPath) || (!string.IsNullOrWhiteSpace(resolvedExe) && File.Exists(resolvedExe));
        if (hasFileLocation)
        {
            var fileLocItem = new Forms.ToolStripMenuItem("Open file location")
            {
                ForeColor = Color.FromArgb(245, 245, 245),
                Padding = itemPadding
            };
            fileLocItem.Click += (_, _) =>
            {
                _appLauncherService.OpenFileLocation(targetPath, resolvedExe);
                FlyoutAnimationHelper.AnimateHide(this);
            };
            menu.Items.Add(fileLocItem);
        }
        else if (targetPath.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
        {
            var appSettingsItem = new Forms.ToolStripMenuItem("App settings")
            {
                ForeColor = Color.FromArgb(245, 245, 245),
                Padding = itemPadding
            };
            appSettingsItem.Click += (_, _) =>
            {
                _appLauncherService.OpenFileLocation(targetPath, resolvedExe);
                FlyoutAnimationHelper.AnimateHide(this);
            };
            menu.Items.Add(appSettingsItem);
        }

        if (!string.IsNullOrEmpty(folderId))
        {
            var removeFromFolderItem = new Forms.ToolStripMenuItem("Remove from folder")
            {
                ForeColor = Color.FromArgb(255, 153, 164),
                Padding = itemPadding,
                Tag = "danger"
            };
            removeFromFolderItem.Click += (_, _) =>
            {
                PostWebMessage(new { type = "remove_from_folder", folderId, target = targetPath });
            };
            menu.Items.Add(removeFromFolderItem);
        }

        menu.Items.Add(new Forms.ToolStripSeparator());

        bool isFromPinned = section == "pinned" || section == "folder";
        bool isActuallyPinned = _shellState?.IsLauncherAppPinned(targetPath, appName) ?? isPinned;

        if (isFromPinned)
        {
            if ((_shellState?.Settings.AppLauncher?.PinnedApps?.Count ?? 0) > 1)
            {
                // Submenu: Reorder pinned item
                var reorderMenu = new Forms.ToolStripMenuItem("Reorder")
                {
                    ForeColor = Color.FromArgb(245, 245, 245),
                    Padding = itemPadding,
                    DropDown = new Forms.ToolStripDropDownMenu
                    {
                        Renderer = new TaskbarWebWindow.Win11DarkContextMenuRenderer(),
                        ForeColor = Color.FromArgb(245, 245, 245),
                        BackColor = Color.FromArgb(44, 44, 44),
                        ShowImageMargin = false,
                        ShowCheckMargin = false,
                        Font = new Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Regular),
                        Padding = new Forms.Padding(4, 4, 4, 4)
                    }
                };

                var toFrontItem = new Forms.ToolStripMenuItem("Move to front")
                {
                    ForeColor = Color.FromArgb(245, 245, 245),
                    Padding = itemPadding
                };
                toFrontItem.Click += (_, _) =>
                {
                    _shellState?.MovePinnedLauncherAppToFront(targetPath);
                    PushAppsData();
                };
                reorderMenu.DropDownItems.Add(toFrontItem);

                var moveLeftItem = new Forms.ToolStripMenuItem("Move left")
                {
                    ForeColor = Color.FromArgb(245, 245, 245),
                    Padding = itemPadding
                };
                moveLeftItem.Click += (_, _) =>
                {
                    _shellState?.MovePinnedLauncherApp(targetPath, -1);
                    PushAppsData();
                };
                reorderMenu.DropDownItems.Add(moveLeftItem);

                var moveRightItem = new Forms.ToolStripMenuItem("Move right")
                {
                    ForeColor = Color.FromArgb(245, 245, 245),
                    Padding = itemPadding
                };
                moveRightItem.Click += (_, _) =>
                {
                    _shellState?.MovePinnedLauncherApp(targetPath, 1);
                    PushAppsData();
                };
                reorderMenu.DropDownItems.Add(moveRightItem);

                menu.Items.Add(reorderMenu);
            }
        }
        else
        {
            if (!isActuallyPinned)
            {
                var pinLauncherItem = new Forms.ToolStripMenuItem("Pin to launcher")
                {
                    ForeColor = Color.FromArgb(245, 245, 245),
                    Padding = itemPadding
                };
                pinLauncherItem.Click += (_, _) =>
                {
                    _shellState?.PinLauncherApp(targetPath, appName, iconDataUrl);
                    PushAppsData();
                };
                menu.Items.Add(pinLauncherItem);
            }
            else
            {
                var unpinLauncherItem = new Forms.ToolStripMenuItem("Unpin from launcher")
                {
                    ForeColor = Color.FromArgb(255, 153, 164),
                    Padding = itemPadding,
                    Tag = "danger"
                };
                unpinLauncherItem.Click += (_, _) =>
                {
                    _shellState?.UnpinLauncherApp(targetPath);
                    PushAppsData();
                };
                menu.Items.Add(unpinLauncherItem);
            }
        }

        // Pin to taskbar / Unpin from taskbar
        string proc = Path.GetFileNameWithoutExtension(targetPath);
        bool isTaskbarPinned = _shellState?.IsAppPinned("", proc) ?? false;
        string pinTaskbarText = isTaskbarPinned ? "Unpin from taskbar" : "Pin to taskbar";
        var pinTaskbarItem = new Forms.ToolStripMenuItem(pinTaskbarText)
        {
            ForeColor = isTaskbarPinned ? Color.FromArgb(255, 153, 164) : Color.FromArgb(245, 245, 245),
            Padding = itemPadding,
            Tag = isTaskbarPinned ? "danger" : null
        };
        pinTaskbarItem.Click += (_, _) =>
        {
            if (isTaskbarPinned)
            {
                _shellState?.UnpinApp("", proc);
            }
            else
            {
                _shellState?.PinApp("", proc, appName, targetPath, iconDataUrl);
            }
        };
        menu.Items.Add(pinTaskbarItem);

        menu.Items.Add(new Forms.ToolStripSeparator());

        if (isFromPinned)
        {
            // Remove pinned app from launcher
            var removeItem = new Forms.ToolStripMenuItem("Remove")
            {
                ForeColor = Color.FromArgb(255, 153, 164),
                Padding = itemPadding,
                Tag = "danger"
            };
            removeItem.Click += (_, _) =>
            {
                _shellState?.UnpinLauncherApp(targetPath);
                PushAppsData();
            };
            menu.Items.Add(removeItem);
        }
        else
        {
            // Hide unpinned app
            var hideItem = new Forms.ToolStripMenuItem("Hide")
            {
                ForeColor = Color.FromArgb(255, 153, 164),
                Padding = itemPadding,
                Tag = "danger"
            };
            hideItem.Click += (_, _) =>
            {
                _shellState?.HideLauncherApp(targetPath, appName);
                PushAppsData();
            };
            menu.Items.Add(hideItem);
        }

        _activeNativeMenu = menu;
        NativeContextMenuHelper.AttachAutoDismiss(menu);
        menu.Show(Forms.Cursor.Position);
    }

    public void ShowAt(
        double anchorScreenX,
        double anchorWidth,
        double taskbarTop,
        double taskbarHeight,
        string dockEdge,
        string region = "left")
    {
        double flyoutWidth = Width;
        double flyoutHeight = Height;

        var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;
        double monitorLeft = mon.Left;
        double monitorRight = mon.Right;
        double monitorTop = mon.Top;
        double monitorBottom = mon.Bottom;
        double monitorWidth = monitorRight - monitorLeft;

        const double sideMargin = 12.0;
        double left;

        switch (region.ToLowerInvariant())
        {
            case "center":
                // Really center: perfectly in the middle of the display
                left = monitorLeft + ((monitorWidth - flyoutWidth) / 2.0);
                break;

            case "right":
                // Float right: docked to the right edge with clean margin
                left = monitorRight - flyoutWidth - sideMargin;
                break;

            case "left":
            default:
                // Float left: docked to the left edge with clean margin
                left = monitorLeft + sideMargin;
                break;
        }

        // Keep strictly within screen horizontal bounds
        if (left + flyoutWidth > monitorRight - sideMargin)
            left = monitorRight - flyoutWidth - sideMargin;
        if (left < monitorLeft + sideMargin)
            left = monitorLeft + sideMargin;

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

        // Keep within screen vertical bounds
        if (top < monitorTop + 12)
            top = monitorTop + 12;
        if (top + flyoutHeight > monitorBottom - 12)
            top = monitorBottom - flyoutHeight - 12;

        Left = left;

        ApplyCornerClipping();
        ApplyTheme(_shellState?.Settings.Theme, _shellState?.Settings.AccentColor);
        FlyoutAnimationHelper.AnimateShow(this, top, dockEdge, activate: true, useOpacityFade: false);

        if (_webViewReady && LauncherWebView.CoreWebView2 != null)
        {
            PushAppsData();
        }
        _appLauncherService.RefreshIfStale();
        FocusSearchInput();
    }

    private void FocusSearchInput()
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            try
            {
                if (!IsVisible) return;
                LauncherWebView.Focus();
                System.Windows.Input.Keyboard.Focus(LauncherWebView);
                if (_webViewReady && LauncherWebView.CoreWebView2 != null)
                {
                    LauncherWebView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"focus_search\"}");
                }
            }
            catch { }
        });
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
