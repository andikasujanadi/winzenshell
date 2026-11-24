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
    private readonly Action? _restartCallback;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _webViewReady;

    public bool AllowClose { get; set; }

    public SettingsWebWindow(ShellStateService shellState, Action? quitCallback = null, Action? restartCallback = null)
    {
        InitializeComponent();
        _shellState = shellState;
        _quitCallback = quitCallback;
        _restartCallback = restartCallback;
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

        ApplyTheme(_shellState.Settings.Theme);

        // Register Emergency Hotkey: Ctrl + Shift + Alt + Z to restore native taskbar from anywhere
        Win32Api.RegisterHotKey(
            _hwnd,
            HOTKEY_ID_EMERGENCY_RESTORE,
            Win32Api.MOD_CONTROL | Win32Api.MOD_SHIFT | Win32Api.MOD_ALT | Win32Api.MOD_NOREPEAT,
            VK_Z);

        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);
    }

    public void ApplyTheme(string? theme)
    {
        bool isLight = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
        if (_hwnd != IntPtr.Zero)
        {
            int dark = isLight ? 0 : 1;
            Win32Api.DwmSetWindowAttribute(_hwnd, Win32Api.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        }

        var bg = isLight
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF3, 0xF3, 0xF3))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x20, 0x20, 0x20));
        Background = bg;
        if (Content is System.Windows.Controls.Grid grid)
        {
            grid.Background = bg;
        }

        if (SettingsWebView != null)
        {
            SettingsWebView.DefaultBackgroundColor = isLight
                ? System.Drawing.Color.FromArgb(243, 243, 243)
                : System.Drawing.Color.FromArgb(32, 32, 32);
        }
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
                ApplyTheme(_shellState.Settings.Theme);
                PushStateToWeb(_shellState.BuildPayload());
            };

            core.Navigate("https://app.winzenshell.local/settings.html");
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings_init_err.txt"), ex.ToString());
            }
            catch { }
        }
    }

    private void OnShellStateChanged(ShellFullStatePayload payload)
    {
        Dispatcher.Invoke(() =>
        {
            ApplyTheme(payload.Settings.Theme);
            PushStateToWeb(payload);
        });
    }

    private void PostIconPackImportStatus(bool busy, bool ok, string message, int percent = 0)
    {
        if (!_webViewReady || SettingsWebView.CoreWebView2 == null) return;
        try
        {
            string json = JsonSerializer.Serialize(new
            {
                type = "icon_pack_import_status",
                busy,
                ok,
                percent,
                message
            });
            SettingsWebView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch { }
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

                case "toggle_animations":
                    bool animEnable = !root.TryGetProperty("enable", out var animProp) || animProp.GetBoolean();
                    _shellState.ToggleAnimations(animEnable);
                    break;

                case "toggle_highlight_active_app":
                    bool highlightActive = !root.TryGetProperty("enable", out var haProp) || haProp.GetBoolean();
                    _shellState.UpdateHighlightActiveAppIcon(highlightActive);
                    break;

                case "toggle_override_windows_key":
                    bool ovWinKey = root.TryGetProperty("enable", out var owkProp) && owkProp.GetBoolean();
                    _shellState.UpdateOverrideWindowsKey(ovWinKey);
                    break;

                case "toggle_auto_start":
                    bool autoStart = root.TryGetProperty("enable", out var asProp) && asProp.GetBoolean();
                    _shellState.UpdateAutoStartOnLogon(autoStart);
                    break;

                case "update_workarea_config":
                    bool respect = !root.TryGetProperty("respectWorkArea", out var rProp) || rProp.GetBoolean();
                    string dockEdge = root.TryGetProperty("dockEdge", out var dProp) ? dProp.GetString() ?? "Bottom" : "Bottom";
                    int heightPx = root.TryGetProperty("taskbarHeightPx", out var htProp) ? htProp.GetInt32() : 48;
                    int marginPx = root.TryGetProperty("floatingMarginPx", out var mgProp) ? mgProp.GetInt32() : 0;
                    string? theme = root.TryGetProperty("theme", out var tmProp) ? tmProp.GetString() : null;
                    int? cornerRadius = root.TryGetProperty("taskbarCornerRadiusPx", out var crProp) ? crProp.GetInt32() : null;
                    string? accent = root.TryGetProperty("accentColor", out var acProp) ? acProp.GetString() : null;
                    int? bgOpacity = root.TryGetProperty("backgroundOpacity", out var boProp) && boProp.ValueKind == JsonValueKind.Number ? boProp.GetInt32() : null;
                    int? bgBlur = root.TryGetProperty("backgroundBlurStrength", out var bbProp) && bbProp.ValueKind == JsonValueKind.Number ? bbProp.GetInt32() : null;
                    int? flyoutBgOpacity = root.TryGetProperty("flyoutBackgroundOpacity", out var fboProp) && fboProp.ValueKind == JsonValueKind.Number ? fboProp.GetInt32() : null;
                    int? flyoutBgBlur = root.TryGetProperty("flyoutBackgroundBlurStrength", out var fbbProp) && fbbProp.ValueKind == JsonValueKind.Number ? fbbProp.GetInt32() : null;
                    string? blurEngine = root.TryGetProperty("blurEngine", out var beProp) && beProp.ValueKind == JsonValueKind.String ? beProp.GetString() : null;
                    string? taskbarStyle = root.TryGetProperty("taskbarStyle", out var tsProp) && tsProp.ValueKind == JsonValueKind.String ? tsProp.GetString() : null;
                    _shellState.UpdateWorkAreaConfig(respect, dockEdge, heightPx, marginPx, theme, cornerRadius, accent, bgOpacity, bgBlur, blurEngine, flyoutBgOpacity, flyoutBgBlur, taskbarStyle);
                    break;

                case "set_accent_color":
                    string newAccent = root.TryGetProperty("accentColor", out var nacProp) ? nacProp.GetString() ?? "#3684E3" : "#3684E3";
                    _shellState.UpdateAccentColor(newAccent);
                    break;

                case "set_corner_radius":
                    int newRadius = root.TryGetProperty("taskbarCornerRadiusPx", out var ncrProp) ? ncrProp.GetInt32() : 12;
                    _shellState.UpdateCornerRadius(newRadius);
                    break;

                case "set_theme":
                    string newTheme = root.TryGetProperty("theme", out var nthProp) ? nthProp.GetString() ?? "dark" : "dark";
                    _shellState.UpdateTheme(newTheme);
                    break;

                case "set_date_format":
                    string? newDateFormat = root.TryGetProperty("dateFormat", out var ndfProp) ? ndfProp.GetString() : null;
                    _shellState.UpdateDateFormat(newDateFormat);
                    break;

                case "update_widget_state":
                    string widgetId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                    bool wEnabled = !root.TryGetProperty("enabled", out var weProp) || weProp.GetBoolean();
                    string wSlot = root.TryGetProperty("slot", out var wsProp) ? wsProp.GetString() ?? "right" : "right";
                    int wWidth = root.TryGetProperty("width", out var wwProp) ? wwProp.GetInt32() : 160;
                    bool wAutoWidth = !root.TryGetProperty("autoWidth", out var waProp) || waProp.GetBoolean();
                    bool wShowAppName = !root.TryGetProperty("showAppName", out var wsnProp) || wsnProp.GetBoolean();
                    bool wCombineApps = root.TryGetProperty("combineApps", out var wcaProp) && wcaProp.GetBoolean();
                    bool? wShowThumbnails = root.TryGetProperty("showThumbnails", out var wstbProp) &&
                                            (wstbProp.ValueKind == JsonValueKind.True || wstbProp.ValueKind == JsonValueKind.False)
                        ? wstbProp.GetBoolean()
                        : null;
                    string? wStartIcon = root.TryGetProperty("startIcon", out var wsiProp) ? wsiProp.GetString() : null;
                    string? wStartIconColorMode = root.TryGetProperty("startIconColorMode", out var wsicmProp) ? wsicmProp.GetString() : null;
                    string? wStartText = root.TryGetProperty("startText", out var wstProp) ? wstProp.GetString() : null;
                    bool wShowStartText = root.TryGetProperty("showStartText", out var wsstProp) && wsstProp.GetBoolean();
                    string? wDisplayMode = root.TryGetProperty("displayMode", out var wdmProp) ? wdmProp.GetString() : null;
                    string? wAspectRatio = root.TryGetProperty("aspectRatio", out var warProp) ? warProp.GetString() : null;
                    string? wBatteryIconStyle = root.TryGetProperty("batteryIconStyle", out var wbisProp) ? wbisProp.GetString() : null;
                    string? wBatteryPercentMode = root.TryGetProperty("batteryPercentMode", out var wbpmProp) ? wbpmProp.GetString() : null;
                    bool? wShowSeconds = root.TryGetProperty("showSeconds", out var wssProp) &&
                                         (wssProp.ValueKind == JsonValueKind.True || wssProp.ValueKind == JsonValueKind.False)
                        ? wssProp.GetBoolean()
                        : null;
                    bool? wShowDate = root.TryGetProperty("showDate", out var wsdProp) &&
                                      (wsdProp.ValueKind == JsonValueKind.True || wsdProp.ValueKind == JsonValueKind.False)
                        ? wsdProp.GetBoolean()
                        : null;
                    bool? wShowCpu = root.TryGetProperty("showCpu", out var wscProp) &&
                                     (wscProp.ValueKind == JsonValueKind.True || wscProp.ValueKind == JsonValueKind.False)
                        ? wscProp.GetBoolean()
                        : null;
                    bool? wShowRam = root.TryGetProperty("showRam", out var wsrProp) &&
                                     (wsrProp.ValueKind == JsonValueKind.True || wsrProp.ValueKind == JsonValueKind.False)
                        ? wsrProp.GetBoolean()
                        : null;
                    bool? wShowGpu = root.TryGetProperty("showGpu", out var wsgProp) &&
                                     (wsgProp.ValueKind == JsonValueKind.True || wsgProp.ValueKind == JsonValueKind.False)
                        ? wsgProp.GetBoolean()
                        : null;
                    int? wIconSizePx = root.TryGetProperty("iconSizePx", out var wispProp) && wispProp.ValueKind == JsonValueKind.Number
                        ? wispProp.GetInt32()
                        : null;
                    int? wIconPaddingPx = root.TryGetProperty("iconPaddingPx", out var wippProp) && wippProp.ValueKind == JsonValueKind.Number
                        ? wippProp.GetInt32()
                        : null;
                    int? wStartIconScalePercent = root.TryGetProperty("startIconScalePercent", out var wsiscProp) && wsiscProp.ValueKind == JsonValueKind.Number
                        ? wsiscProp.GetInt32()
                        : null;
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
                            wAspectRatio,
                            wShowThumbnails,
                            wBatteryIconStyle,
                            wBatteryPercentMode,
                            wStartIconColorMode,
                            wShowSeconds,
                            wShowDate,
                            wShowCpu,
                            wShowRam,
                            wShowGpu,
                            wIconSizePx,
                            wIconPaddingPx,
                            wStartIconScalePercent);
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

                case "update_dynamic_island":
                    bool diEnabled = root.TryGetProperty("enabled", out var dieProp) && dieProp.GetBoolean();
                    string diPlacement = root.TryGetProperty("placement", out var dipProp) ? dipProp.GetString() ?? "taskbar" : "taskbar";
                    string diSlot = root.TryGetProperty("taskbarSlot", out var disProp) ? disProp.GetString() ?? "center" : "center";
                    string diTimeFormat = root.TryGetProperty("timeFormat", out var ditfProp) ? ditfProp.GetString() ?? "24h" : "24h";
                    bool diShowSec = root.TryGetProperty("showSeconds", out var dissProp) && dissProp.GetBoolean();
                    bool diShowDate = !root.TryGetProperty("showDate", out var disdProp) || disdProp.GetBoolean();
                    bool diAnim = !root.TryGetProperty("enableAnimation", out var diaProp) || diaProp.GetBoolean();
                    bool diReserve = !root.TryGetProperty("reserveWorkArea", out var dirProp) || dirProp.GetBoolean();
                    bool diMusic = !root.TryGetProperty("enableMusicIsland", out var dimProp) || dimProp.GetBoolean();
                    bool? diPinLyrics = root.TryGetProperty("pinLyrics", out var diplProp) ? diplProp.GetBoolean() : null;
                    bool? diWidePill = root.TryGetProperty("widePill", out var diwpProp) ? diwpProp.GetBoolean() : null;
                    _shellState.UpdateDynamicIslandConfig(diEnabled, diPlacement, diSlot, diTimeFormat, diShowSec, diShowDate, diAnim, diReserve, diMusic, diPinLyrics, diWidePill);
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

                case "save_layout_preset":
                    {
                        string? presetName = root.TryGetProperty("name", out var lpNameProp) ? lpNameProp.GetString() : null;
                        _shellState.SaveLayoutPreset(presetName);
                    }
                    break;

                case "apply_layout_preset":
                    if (root.TryGetProperty("id", out var lpApplyId) && !string.IsNullOrWhiteSpace(lpApplyId.GetString()))
                    {
                        _shellState.ApplyLayoutPreset(lpApplyId.GetString());
                    }
                    break;

                case "update_layout_preset":
                    if (root.TryGetProperty("id", out var lpUpdateId) && !string.IsNullOrWhiteSpace(lpUpdateId.GetString()))
                    {
                        string? newName = root.TryGetProperty("name", out var lpNewName) ? lpNewName.GetString() : null;
                        _shellState.UpdateLayoutPreset(lpUpdateId.GetString(), newName);
                    }
                    break;

                case "rename_layout_preset":
                    if (root.TryGetProperty("id", out var lpRenameId) && !string.IsNullOrWhiteSpace(lpRenameId.GetString()))
                    {
                        string? renamed = root.TryGetProperty("name", out var lpRenameName) ? lpRenameName.GetString() : null;
                        _shellState.RenameLayoutPreset(lpRenameId.GetString(), renamed);
                    }
                    break;

                case "delete_layout_preset":
                    if (root.TryGetProperty("id", out var lpDeleteId) && !string.IsNullOrWhiteSpace(lpDeleteId.GetString()))
                    {
                        _shellState.DeleteLayoutPreset(lpDeleteId.GetString());
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

                case "update_app_launcher_config":
                    {
                        bool? showPinned = root.TryGetProperty("showPinnedApps", out var spProp) ? spProp.GetBoolean() : null;
                        bool? showRecent = root.TryGetProperty("showRecentApps", out var srProp) ? srProp.GetBoolean() : null;
                        bool? showSteam = root.TryGetProperty("showSteamGames", out var ssProp) ? ssProp.GetBoolean() : null;
                        bool? overrideWinKey = root.TryGetProperty("overrideWindowsKey", out var owProp) ? owProp.GetBoolean() : null;
                        string? layoutVariant = root.TryGetProperty("layoutVariant", out var lvProp) ? lvProp.GetString() : null;
                        string? win81TileColorMode = root.TryGetProperty("win81TileColorMode", out var wcProp) ? wcProp.GetString() : null;
                        bool? win81SplitView = root.TryGetProperty("win81SplitView", out var wsvProp) ? wsvProp.GetBoolean() : null;
                        bool? win81FullScreen = root.TryGetProperty("win81FullScreen", out var wfsProp) ? wfsProp.GetBoolean() : null;
                        string? win81ProfileName = root.TryGetProperty("win81ProfileName", out var wpnProp) ? wpnProp.GetString() : null;
                        string? win81ProfilePicture = root.TryGetProperty("win81ProfilePicture", out var wppProp) ? wppProp.GetString() : null;

                        _shellState.UpdateAppLauncherConfig(showPinned, showRecent, showSteam, overrideWinKey, layoutVariant, win81TileColorMode, win81SplitView, win81FullScreen, win81ProfileName, win81ProfilePicture);
                        break;
                    }

                case "pick_win81_profile_picture":
                    {
                        var dlg = new Microsoft.Win32.OpenFileDialog
                        {
                            Title = "Choose Profile Picture",
                            Filter = "Image Files (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp"
                        };
                        if (dlg.ShowDialog(this) == true && File.Exists(dlg.FileName))
                        {
                            try
                            {
                                byte[] bytes = File.ReadAllBytes(dlg.FileName);
                                string ext = Path.GetExtension(dlg.FileName).TrimStart('.').ToLowerInvariant();
                                if (ext == "jpg") ext = "jpeg";
                                string dataUrl = $"data:image/{ext};base64,{Convert.ToBase64String(bytes)}";
                                _shellState.UpdateAppLauncherConfig(null, null, null, null, win81ProfilePicture: dataUrl);
                            }
                            catch { }
                        }
                        break;
                    }

                case "unpin_launcher_app":
                    if (root.TryGetProperty("targetPath", out var ulProp) && !string.IsNullOrWhiteSpace(ulProp.GetString()))
                    {
                        _shellState.UnpinLauncherApp(ulProp.GetString()!);
                    }
                    break;

                case "unpin_launcher_folder":
                    if (root.TryGetProperty("folderId", out var ufProp) && !string.IsNullOrWhiteSpace(ufProp.GetString()))
                    {
                        _shellState.UnpinLauncherFolder(ufProp.GetString()!);
                    }
                    break;

                case "reset_pinned_launcher_apps":
                    _shellState.ResetPinnedLauncherApps();
                    break;

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
                            _shellState.ReorderPinnedLauncherApps(list);
                        }
                        break;
                    }

                case "unhide_launcher_app":
                    if (root.TryGetProperty("targetPath", out var uhlProp) && !string.IsNullOrWhiteSpace(uhlProp.GetString()))
                    {
                        _shellState.UnhideLauncherApp(uhlProp.GetString()!);
                    }
                    break;

                case "reset_hidden_launcher_apps":
                    _shellState.ResetHiddenLauncherApps();
                    break;

                case "quit_app":
                    _quitCallback?.Invoke();
                    break;

                case "restart_app":
                case "restart_shell":
                    _restartCallback?.Invoke();
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

                case "update_icon_pack":
                    {
                        string? packId = root.TryGetProperty("iconPack", out var ipProp) ? ipProp.GetString() : "native";
                        _shellState.UpdateIconPack(packId);
                    }
                    break;

                case "refresh_icon_packs":
                    _shellState.RefreshIconPacks();
                    break;

                case "delete_icon_pack":
                    if (root.TryGetProperty("id", out var dipIdProp) && !string.IsNullOrWhiteSpace(dipIdProp.GetString()))
                    {
                        _shellState.DeleteIconPack(dipIdProp.GetString());
                    }
                    break;

                case "open_gnome_look":
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "https://www.gnome-look.org/browse?cat=132&ord=rating",
                            UseShellExecute = true
                        });
                    }
                    catch { }
                    break;

                case "open_icon_packs_folder":
                    if (_shellState.IconPacks != null)
                    {
                        try
                        {
                            Directory.CreateDirectory(_shellState.IconPacks.UserPacksDirectory);
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = _shellState.IconPacks.UserPacksDirectory,
                                UseShellExecute = true
                            });
                        }
                        catch { }
                    }
                    break;

                case "import_icon_pack_archive":
                    {
                        var dlg = new Microsoft.Win32.OpenFileDialog
                        {
                            Title = "Import Icon Pack Archive (GNOME-Look / ZIP / TAR)",
                            Filter = "Icon Pack Archives (*.zip;*.tar.xz;*.tar.gz;*.tgz;*.tar)|*.zip;*.tar.xz;*.tar.gz;*.tgz;*.tar|All Files (*.*)|*.*"
                        };
                        if (dlg.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dlg.FileName))
                        {
                            string selectedPath = dlg.FileName;
                            PostIconPackImportStatus(true, true, $"Extracting '{Path.GetFileName(selectedPath)}'...", 8);
                            _ = System.Threading.Tasks.Task.Run(() =>
                            {
                                bool ok = _shellState.ImportIconPackFromPath(
                                    selectedPath,
                                    out string msg,
                                    (pct, stage) => Dispatcher.BeginInvoke(() => PostIconPackImportStatus(true, true, stage, pct)));
                                Dispatcher.Invoke(() => PostIconPackImportStatus(false, ok, msg, ok ? 100 : 0));
                            });
                        }
                    }
                    break;

                case "import_icon_pack_folder":
                    {
                        using var fbd = new System.Windows.Forms.FolderBrowserDialog
                        {
                            Description = "Select an extracted Icon Pack folder (containing index.theme or apps/)",
                            UseDescriptionForTitle = true
                        };
                        if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                        {
                            string selectedDir = fbd.SelectedPath;
                            PostIconPackImportStatus(true, true, $"Scanning folder '{Path.GetFileName(selectedDir)}'...", 10);
                            _ = System.Threading.Tasks.Task.Run(() =>
                            {
                                bool ok = _shellState.ImportIconPackFromPath(
                                    selectedDir,
                                    out string msg,
                                    (pct, stage) => Dispatcher.BeginInvoke(() => PostIconPackImportStatus(true, true, stage, pct)));
                                Dispatcher.Invoke(() => PostIconPackImportStatus(false, ok, msg, ok ? 100 : 0));
                            });
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
