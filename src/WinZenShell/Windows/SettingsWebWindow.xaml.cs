using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using WinZenShell.Native;
using WinZenShell.Services;

namespace WinZenShell.Windows;

public partial class SettingsWebWindow : Window
{
    private const int HOTKEY_ID_EMERGENCY_RESTORE = 9001;
    private const uint VK_Z = 0x5A;

    private readonly ShellStateService _shellState;
    private readonly Action? _quitCallback;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _webViewReady;

    public bool AllowClose { get; set; }

    public SettingsWebWindow(ShellStateService shellState, Action? quitCallback = null)
    {
        InitializeComponent();
        _shellState = shellState;
        _quitCallback = quitCallback;
        _shellState.StateChanged += OnShellStateChanged;

        Loaded += OnLoaded;
        Closed += OnWindowClosed;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            // Minimize/hide to system tray instead of closing the application
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;

        // Enable Windows 11 Immersive Dark Mode titlebar
        int dark = 1;
        Win32Api.DwmSetWindowAttribute(_hwnd, Win32Api.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

        // Register Emergency Hotkey: Ctrl + Shift + Alt + Z to restore native taskbar from anywhere
        Win32Api.RegisterHotKey(
            _hwnd,
            HOTKEY_ID_EMERGENCY_RESTORE,
            Win32Api.MOD_CONTROL | Win32Api.MOD_SHIFT | Win32Api.MOD_ALT | Win32Api.MOD_NOREPEAT,
            VK_Z);

        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_hwnd != IntPtr.Zero)
        {
            Win32Api.UnregisterHotKey(_hwnd, HOTKEY_ID_EMERGENCY_RESTORE);
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var env = await WebViewEnvironmentManager.GetSharedEnvironmentAsync();
            await SettingsWebView.EnsureCoreWebView2Async(env);

            var core = SettingsWebView.CoreWebView2;
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
                PushStateToWeb(_shellState.BuildPayload());
            };

            core.Navigate("https://app.winzenshell.local/settings.html");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to initialize Settings WebView2: {ex.Message}",
                "WinZenShell Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnShellStateChanged(ShellFullStatePayload payload)
    {
        Dispatcher.Invoke(() => PushStateToWeb(payload));
    }

    private void PushStateToWeb(ShellFullStatePayload payload)
    {
        if (!_webViewReady || SettingsWebView.CoreWebView2 == null) return;

        string json = JsonSerializer.Serialize(new
        {
            type = "state_update",
            payload
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        SettingsWebView.CoreWebView2.PostWebMessageAsJson(json);
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            string action = root.TryGetProperty("action", out var actProp) ? actProp.GetString() ?? "" : "";

            switch (action)
            {
                case "request_state":
                    _shellState.SyncTaskbarAndWorkArea();
                    PushStateToWeb(_shellState.BuildPayload());
                    break;

                case "toggle_native_taskbar":
                    bool hide = root.TryGetProperty("hide", out var hProp) && hProp.GetBoolean();
                    await _shellState.ToggleHideNativeTaskbarAsync(hide);
                    break;

                case "toggle_winzen_taskbar":
                    bool enable = root.TryGetProperty("enable", out var eProp) && eProp.GetBoolean();
                    _shellState.ToggleWinZenTaskbar(enable);
                    break;

                case "update_workarea_config":
                    bool respect = !root.TryGetProperty("respectWorkArea", out var rProp) || rProp.GetBoolean();
                    string dockEdge = root.TryGetProperty("dockEdge", out var dProp) ? dProp.GetString() ?? "Bottom" : "Bottom";
                    int heightPx = root.TryGetProperty("taskbarHeightPx", out var htProp) ? htProp.GetInt32() : 48;
                    int marginPx = root.TryGetProperty("floatingMarginPx", out var mgProp) ? mgProp.GetInt32() : 0;
                    string? theme = root.TryGetProperty("theme", out var tmProp) ? tmProp.GetString() : null;
                    _shellState.UpdateWorkAreaConfig(respect, dockEdge, heightPx, marginPx, theme);
                    break;

                case "set_theme":
                    string newTheme = root.TryGetProperty("theme", out var nthProp) ? nthProp.GetString() ?? "dark" : "dark";
                    _shellState.UpdateTheme(newTheme);
                    break;

                case "update_widget_state":
                    string widgetId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                    bool wEnabled = !root.TryGetProperty("enabled", out var weProp) || weProp.GetBoolean();
                    string wSlot = root.TryGetProperty("slot", out var wsProp) ? wsProp.GetString() ?? "right" : "right";
                    int wWidth = root.TryGetProperty("width", out var wwProp) ? wwProp.GetInt32() : 160;
                    bool wAutoWidth = !root.TryGetProperty("autoWidth", out var waProp) || waProp.GetBoolean();
                    bool wShowAppName = !root.TryGetProperty("showAppName", out var wsnProp) || wsnProp.GetBoolean();
                    bool wCombineApps = root.TryGetProperty("combineApps", out var wcaProp) && wcaProp.GetBoolean();
                    string? wStartIcon = root.TryGetProperty("startIcon", out var wsiProp) ? wsiProp.GetString() : null;
                    string? wStartText = root.TryGetProperty("startText", out var wstProp) ? wstProp.GetString() : null;
                    bool wShowStartText = root.TryGetProperty("showStartText", out var wsstProp) && wsstProp.GetBoolean();
                    string? wDisplayMode = root.TryGetProperty("displayMode", out var wdmProp) ? wdmProp.GetString() : null;
                    string? wAspectRatio = root.TryGetProperty("aspectRatio", out var warProp) ? warProp.GetString() : null;
                    int? wOrder = root.TryGetProperty("order", out var woProp) && woProp.ValueKind == JsonValueKind.Number
                        ? woProp.GetInt32()
                        : null;
                    if (!string.IsNullOrWhiteSpace(widgetId))
                    {
                        _shellState.UpdateWidgetState(
                            widgetId,
                            wEnabled,
                            wSlot,
                            wWidth,
                            wAutoWidth,
                            wShowAppName,
                            wOrder,
                            wCombineApps,
                            wStartIcon,
                            wStartText,
                            wShowStartText,
                            wDisplayMode,
                            wAspectRatio);
                    }
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

                case "unhide_app":
                case "unhide_window":
                    if (root.TryGetProperty("appId", out var uhAppProp) && !string.IsNullOrWhiteSpace(uhAppProp.GetString()))
                    {
                        _shellState.UnhideApp(uhAppProp.GetString()!);
                    }
                    break;

                case "unhide_all_apps":
                case "unhide_all_windows":
                    _shellState.UnhideAllApps();
                    break;

                case "quit_app":
                    _quitCallback?.Invoke();
                    break;

                case "create_community_widget":
                    string name = root.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "My Widget" : "My Widget";
                    string author = root.TryGetProperty("author", out var aProp) ? aProp.GetString() ?? "Community" : "Community";
                    string slot = root.TryGetProperty("slot", out var sProp) ? sProp.GetString() ?? "right" : "right";
                    int width = root.TryGetProperty("width", out var wProp) ? wProp.GetInt32() : 170;
                    _shellState.CreateNewCommunityWidget(name, author, slot, width);
                    break;

                case "open_widgets_folder":
                    _shellState.WidgetManager.OpenWidgetsFolderInExplorer();
                    break;

                case "reload_widgets":
                    _shellState.BroadcastState();
                    break;

                case "emergency_restore":
                    _shellState.EmergencyRestoreAll();
                    break;
            }
        }
        catch
        {
            // Ignore malformed JSON
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Api.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID_EMERGENCY_RESTORE)
        {
            _shellState.EmergencyRestoreAll();
            handled = true;
        }
        return IntPtr.Zero;
    }
}
