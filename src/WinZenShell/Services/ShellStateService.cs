using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WinZenShell.Native;

namespace WinZenShell.Services;

public class ControlCenterItemConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("order")]
    public int Order { get; set; } = 10;

    [JsonPropertyName("aspectRatio")]
    public string AspectRatio { get; set; } = "2x1"; // "1x1", "2x1", "2x2", "3x1", "4x1"

    [JsonPropertyName("colSpan")]
    public int ColSpan { get; set; } = 2;

    [JsonPropertyName("rowSpan")]
    public int RowSpan { get; set; } = 1;

    [JsonPropertyName("row")]
    public int? Row { get; set; }

    [JsonPropertyName("col")]
    public int? Col { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

public class ControlCenterItemPayload
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("order")]
    public int Order { get; set; } = 10;

    [JsonPropertyName("aspectRatio")]
    public string AspectRatio { get; set; } = "2x1";

    [JsonPropertyName("colSpan")]
    public int ColSpan { get; set; } = 2;

    [JsonPropertyName("rowSpan")]
    public int RowSpan { get; set; } = 1;

    [JsonPropertyName("row")]
    public int? Row { get; set; }

    [JsonPropertyName("col")]
    public int? Col { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("htmlContent")]
    public string HtmlContent { get; set; } = "";

    [JsonPropertyName("cssContent")]
    public string CssContent { get; set; } = "";

    [JsonPropertyName("jsContent")]
    public string JsContent { get; set; } = "";

    [JsonPropertyName("batteryIconStyle")]
    public string? BatteryIconStyle { get; set; }

    [JsonPropertyName("batteryPercentMode")]
    public string? BatteryPercentMode { get; set; }

    [JsonPropertyName("startIcon")]
    public string? StartIcon { get; set; }

    [JsonPropertyName("startIconColorMode")]
    public string? StartIconColorMode { get; set; }

    [JsonPropertyName("showSeconds")]
    public bool ShowSeconds { get; set; } = true;

    [JsonPropertyName("showDate")]
    public bool ShowDate { get; set; } = true;

    [JsonPropertyName("showCpu")]
    public bool ShowCpu { get; set; } = true;

    [JsonPropertyName("showRam")]
    public bool ShowRam { get; set; } = true;

    [JsonPropertyName("showGpu")]
    public bool ShowGpu { get; set; } = true;
}

public class PinnedAppEntry
{
    [JsonPropertyName("appId")]
    public string AppId { get; set; } = "";

    [JsonPropertyName("processName")]
    public string ProcessName { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("exePath")]
    public string ExePath { get; set; } = "";

    [JsonPropertyName("iconDataUrl")]
    public string IconDataUrl { get; set; } = "";

    [JsonPropertyName("nativeIconDataUrl")]
    public string NativeIconDataUrl { get; set; } = "";
}

public class DynamicIslandConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;

    [JsonPropertyName("placement")]
    public string Placement { get; set; } = "taskbar"; // "taskbar" or "top-center"

    [JsonPropertyName("taskbarSlot")]
    public string TaskbarSlot { get; set; } = "center"; // "center", "left", "right"

    [JsonPropertyName("timeFormat")]
    public string TimeFormat { get; set; } = "24h"; // "24h" or "12h"

    [JsonPropertyName("showSeconds")]
    public bool ShowSeconds { get; set; } = false;

    [JsonPropertyName("showDate")]
    public bool ShowDate { get; set; } = true;

    [JsonPropertyName("enableAnimation")]
    public bool EnableAnimation { get; set; } = true;

    [JsonPropertyName("reserveWorkArea")]
    public bool ReserveWorkArea { get; set; } = true;

    [JsonPropertyName("enableMusicIsland")]
    public bool EnableMusicIsland { get; set; } = true;

    [JsonPropertyName("pinLyrics")]
    public bool PinLyrics { get; set; } = false;

    [JsonPropertyName("widePill")]
    public bool WidePill { get; set; } = false;
}

public class LayoutPreset
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public Dictionary<string, WidgetStateOverride> WidgetOverrides { get; set; } = new();
    public List<ControlCenterItemConfig> ControlCenterWidgets { get; set; } = new();

    // Shell layout & appearance settings
    public bool? HideNativeTaskbar { get; set; }
    public string? DockEdge { get; set; }
    public int? TaskbarHeightPx { get; set; }
    public int? FloatingMarginPx { get; set; }
    public int? TaskbarCornerRadiusPx { get; set; }
    public string? TaskbarStyle { get; set; }
    public int? BackgroundOpacity { get; set; }
    public int? BackgroundBlurStrength { get; set; }
    public int? FlyoutBackgroundOpacity { get; set; }
    public int? FlyoutBackgroundBlurStrength { get; set; }

    // Dynamic Island placement settings
    public bool? DynamicIslandEnabled { get; set; }
    public string? DynamicIslandPlacement { get; set; }
    public string? DynamicIslandTaskbarSlot { get; set; }

    // App Launcher settings
    public AppLauncherConfig? AppLauncher { get; set; }
    public bool? OverrideWindowsKey { get; set; }
}

public class AppLauncherPinnedItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("targetPath")]
    public string TargetPath { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("iconDataUrl")]
    public string IconDataUrl { get; set; } = "";

    [JsonPropertyName("isFolder")]
    public bool IsFolder { get; set; } = false;

    [JsonPropertyName("folderSize")]
    public string FolderSize { get; set; } = "1x1";

    [JsonPropertyName("tileSize")]
    public string TileSize { get; set; } = "1x1";

    [JsonPropertyName("folderItems")]
    public List<AppLauncherPinnedItem> FolderItems { get; set; } = new();
}

public class AppLauncherConfig
{
    [JsonPropertyName("showPinnedApps")]
    public bool ShowPinnedApps { get; set; } = true;

    [JsonPropertyName("showRecentApps")]
    public bool ShowRecentApps { get; set; } = true;

    [JsonPropertyName("showSteamGames")]
    public bool ShowSteamGames { get; set; } = true;

    [JsonPropertyName("layoutVariant")]
    public string LayoutVariant { get; set; } = "default";

    [JsonPropertyName("win81TileColorMode")]
    public string Win81TileColorMode { get; set; } = "multicolor";

    [JsonPropertyName("win81SplitView")]
    public bool Win81SplitView { get; set; } = true;

    [JsonPropertyName("win81FullScreen")]
    public bool Win81FullScreen { get; set; } = false;

    [JsonPropertyName("win81ProfileName")]
    public string Win81ProfileName { get; set; } = "";

    [JsonPropertyName("win81ProfilePicture")]
    public string Win81ProfilePicture { get; set; } = "";

    [JsonPropertyName("pinnedApps")]
    public List<AppLauncherPinnedItem> PinnedApps { get; set; } = new();

    [JsonPropertyName("hiddenApps")]
    public List<string> HiddenApps { get; set; } = new();
}

public class ShellSettings
{
    public bool HideNativeTaskbar { get; set; } = false;
    public bool EnableWinZenTaskbar { get; set; } = true;
    public bool RespectDesktopWorkArea { get; set; } = true;
    public string DockEdge { get; set; } = "Bottom"; // "Bottom" or "Top"
    public int TaskbarHeightPx { get; set; } = 48;
    public int FloatingMarginPx { get; set; } = 0;
    public int TaskbarCornerRadiusPx { get; set; } = 12;
    public string TaskbarStyle { get; set; } = "unified"; // "unified" or "separated"
    public int BackgroundOpacity { get; set; } = 100;
    public int BackgroundBlurStrength { get; set; } = 60;
    public int FlyoutBackgroundOpacity { get; set; } = 100;
    public int FlyoutBackgroundBlurStrength { get; set; } = 60;
    public string BlurEngine { get; set; } = "wallpaper"; // "wallpaper" or "realtime"
    public string AccentColor { get; set; } = "#3684E3";
    public string Theme { get; set; } = "dark"; // "dark" or "light"
    public string IconPack { get; set; } = "native";
    public string DateFormat { get; set; } = "DD-MM-YYYY";
    public bool EnableAnimations { get; set; } = true;
    public bool HighlightActiveAppIcon { get; set; } = true;
    public bool OverrideWindowsKey { get; set; } = false;
    public bool AutoStartOnLogon { get; set; } = false;
    public Dictionary<string, WidgetStateOverride> WidgetOverrides { get; set; } = new();
    public List<ControlCenterItemConfig> ControlCenterWidgets { get; set; } = new();
    public List<PinnedAppEntry> PinnedApps { get; set; } = new();
    public DynamicIslandConfig DynamicIsland { get; set; } = new();
    public List<LayoutPreset> LayoutPresets { get; set; } = new();
    public string? ActiveLayoutPresetId { get; set; }
    public AppLauncherConfig AppLauncher { get; set; } = new();
}

public class ShellFullStatePayload
{
    public ShellSettings Settings { get; set; } = new();
    public NativeTaskbarStatus NativeTaskbar { get; set; } = new();
    public string BackdropImageUrl { get; set; } = "";
    public WorkAreaTelemetry WorkArea { get; set; } = new();
    public List<WidgetManifest> Widgets { get; set; } = new();
    public List<ControlCenterItemPayload> ControlCenterWidgets { get; set; } = new();
    public List<HiddenAppEntry> HiddenApps { get; set; } = new();
    public List<IconPackManifest> IconPacks { get; set; } = new();
    public string IconPacksFolderPath { get; set; } = "";
    public string WidgetsFolderPath { get; set; } = "";
    public string StatusBanner { get; set; } = "Ready";
}

public class ShellStateService
{
    private readonly NativeTaskbarService _nativeTaskbarService;
    private readonly WorkAreaAppBarService _workAreaService;
    private readonly WidgetManagerService _widgetManager;
    private readonly SystemMetricsService? _systemMetricsService;
    private readonly IconPackService? _iconPackService;
    private readonly string _settingsFilePath;
    private readonly string _portableSettingsFilePath;

    private ShellSettings _settings = new();
    private string _statusBanner = "WinZenShell Active — Ready";

    public event Action<ShellFullStatePayload>? StateChanged;
    public event Action<bool>? TaskbarVisibilityRequested;
    public event Action<Win32Api.RECT>? TaskbarBoundsUpdated;
    public event Action? DynamicIslandVisibilityChanged;
    public event Action<bool>? WinKeyOverrideChanged;

    public Func<IntPtr>? GetTaskbarHwnd { get; set; }
    public Func<IntPtr>? GetDynamicIslandHwnd { get; set; }

    public ShellSettings Settings => _settings;
    public NativeTaskbarService NativeTaskbar => _nativeTaskbarService;
    public WorkAreaAppBarService WorkArea => _workAreaService;
    public WidgetManagerService WidgetManager => _widgetManager;
    public IconPackService? IconPacks => _iconPackService;

    public ShellStateService(
        NativeTaskbarService nativeTaskbarService,
        WorkAreaAppBarService workAreaService,
        WidgetManagerService widgetManager,
        SystemMetricsService? systemMetricsService = null,
        IconPackService? iconPackService = null)
    {
        _nativeTaskbarService = nativeTaskbarService;
        _workAreaService = workAreaService;
        _widgetManager = widgetManager;
        _systemMetricsService = systemMetricsService;
        _iconPackService = iconPackService;

        string configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinZenShell");
        Directory.CreateDirectory(configDir);
        _settingsFilePath = Path.Combine(configDir, "settings.json");
        _portableSettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        LoadSettings();
    }

    public void LoadSettings()
    {
        try
        {
            string? pathToLoad = File.Exists(_settingsFilePath)
                ? _settingsFilePath
                : (File.Exists(_portableSettingsFilePath) ? _portableSettingsFilePath : null);

            if (pathToLoad != null)
            {
                string json = File.ReadAllText(pathToLoad);
                var loaded = JsonSerializer.Deserialize<ShellSettings>(json);
                if (loaded != null)
                {
                    _settings = loaded;
                }
            }
        }
        catch
        {
            _settings = new ShellSettings();
        }

        if (_settings.ControlCenterWidgets == null || _settings.ControlCenterWidgets.Count == 0)
        {
            _settings.ControlCenterWidgets = new List<ControlCenterItemConfig>
            {
                new() { Id = "community.zen-volume", Order = 10, AspectRatio = "1:2", Enabled = true },
                new() { Id = "community.zen-brightness", Order = 20, AspectRatio = "1:2", Enabled = true },
                new() { Id = "community.zen-system-monitor", Order = 30, AspectRatio = "1:1", Enabled = true },
                new() { Id = "community.zen-bluetooth", Order = 40, AspectRatio = "2x1", Enabled = true }
            };
        }

        if (_settings.DynamicIsland == null)
        {
            _settings.DynamicIsland = new();
        }
        if (string.IsNullOrWhiteSpace(_settings.DateFormat))
        {
            _settings.DateFormat = "DD-MM-YYYY";
        }
        if (string.IsNullOrWhiteSpace(_settings.IconPack))
        {
            _settings.IconPack = "native";
        }
        if (_settings.PinnedApps != null)
        {
            foreach (var pin in _settings.PinnedApps)
            {
                if (string.IsNullOrWhiteSpace(pin.NativeIconDataUrl) && !string.IsNullOrWhiteSpace(pin.IconDataUrl))
                {
                    pin.NativeIconDataUrl = pin.IconDataUrl;
                }
            }
        }
        _iconPackService?.SetActivePack(_settings.IconPack, notify: true);
        ApplyIconPackToPinnedApps();
        _settings.LayoutPresets ??= new();
        if (string.Equals(_settings.DockEdge, "Top", StringComparison.OrdinalIgnoreCase) && _settings.DynamicIsland.Placement == "top-center")
        {
            _settings.DynamicIsland.Placement = "taskbar";
        }

        FlyoutAnimationHelper.AnimationsEnabled = _settings.EnableAnimations;
        SyncAutoStartRegistry(_settings.AutoStartOnLogon);
    }

    public void SaveSettings()
    {
        try
        {
            string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
            try
            {
                File.WriteAllText(_portableSettingsFilePath, json);
            }
            catch { }
        }
        catch
        {
            // Ignore write errors
        }
    }

    public async Task ToggleHideNativeTaskbarAsync(bool hide)
    {
        _settings.HideNativeTaskbar = hide;
        SaveSettings();

        if (hide)
        {
            await _nativeTaskbarService.HideNativeTaskbarAsync(stepMsg =>
            {
                _statusBanner = stepMsg;
                BroadcastState();
            });
            _statusBanner = "Native Taskbar Hidden (Auto-Hide applied first -> Working Area cleared -> Concealed)";
        }
        else
        {
            _nativeTaskbarService.RestoreNativeTaskbar();
            _statusBanner = "Native taskbar shown";
        }

        // Re-apply custom taskbar bounds and Respected Desktop Working Area
        SyncTaskbarAndWorkArea();
        BroadcastState();
    }

    public void ToggleWinZenTaskbar(bool enable)
    {
        _settings.EnableWinZenTaskbar = enable;
        SaveSettings();

        TaskbarVisibilityRequested?.Invoke(enable);
        SyncTaskbarAndWorkArea();

        _statusBanner = enable ? "Taskbar enabled" : "Taskbar disabled";
        BroadcastState();
    }

    public void ToggleAnimations(bool enable)
    {
        _settings.EnableAnimations = enable;
        FlyoutAnimationHelper.AnimationsEnabled = enable;
        SaveSettings();
        _statusBanner = enable ? "Animations enabled" : "Animations disabled";
        BroadcastState();
    }

    public void UpdateHighlightActiveAppIcon(bool enable)
    {
        _settings.HighlightActiveAppIcon = enable;
        SaveSettings();
        _statusBanner = enable ? "Active app highlight enabled" : "Active app highlight disabled";
        BroadcastState();
    }

    public void UpdateWorkAreaConfig(
        bool respectWorkArea,
        string dockEdge,
        int heightPx,
        int marginPx,
        string? theme = null,
        int? cornerRadiusPx = null,
        string? accentColor = null,
        int? backgroundOpacity = null,
        int? backgroundBlurStrength = null,
        string? blurEngine = null,
        int? flyoutBackgroundOpacity = null,
        int? flyoutBackgroundBlurStrength = null,
        string? taskbarStyle = null)
    {
        _settings.RespectDesktopWorkArea = respectWorkArea;
        if (!string.IsNullOrWhiteSpace(dockEdge))
            _settings.DockEdge = dockEdge;
        if (string.Equals(_settings.DockEdge, "Top", StringComparison.OrdinalIgnoreCase) && _settings.DynamicIsland.Placement == "top-center")
        {
            _settings.DynamicIsland.Placement = "taskbar";
        }
        _settings.TaskbarHeightPx = Math.Clamp(heightPx, 32, 96);
        _settings.FloatingMarginPx = Math.Clamp(marginPx, 0, 24);
        if (cornerRadiusPx.HasValue)
            _settings.TaskbarCornerRadiusPx = Math.Clamp(cornerRadiusPx.Value, 0, 120);
        if (backgroundOpacity.HasValue)
            _settings.BackgroundOpacity = Math.Clamp(backgroundOpacity.Value, 0, 100);
        if (backgroundBlurStrength.HasValue)
            _settings.BackgroundBlurStrength = Math.Clamp(backgroundBlurStrength.Value, 0, 100);
        if (flyoutBackgroundOpacity.HasValue)
            _settings.FlyoutBackgroundOpacity = Math.Clamp(flyoutBackgroundOpacity.Value, 0, 100);
        if (flyoutBackgroundBlurStrength.HasValue)
            _settings.FlyoutBackgroundBlurStrength = Math.Clamp(flyoutBackgroundBlurStrength.Value, 0, 100);
        if (!string.IsNullOrWhiteSpace(blurEngine))
            _settings.BlurEngine = string.Equals(blurEngine, "realtime", StringComparison.OrdinalIgnoreCase) ? "realtime" : "wallpaper";
        if (!string.IsNullOrWhiteSpace(accentColor))
            _settings.AccentColor = accentColor;
        if (!string.IsNullOrWhiteSpace(theme))
            _settings.Theme = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark";
        if (!string.IsNullOrWhiteSpace(taskbarStyle))
            _settings.TaskbarStyle = string.Equals(taskbarStyle, "separated", StringComparison.OrdinalIgnoreCase) ? "separated" : "unified";
        SaveSettings();

        SyncTaskbarAndWorkArea();

        _statusBanner = respectWorkArea ? "Work area saved" : "Overlay mode";
        BroadcastState();
    }

    public void UpdateAccentColor(string accentColor)
    {
        if (string.IsNullOrWhiteSpace(accentColor)) return;
        _settings.AccentColor = accentColor;
        SaveSettings();
        _statusBanner = $"Accent color set to {accentColor}";
        BroadcastState();
    }

    public void UpdateCornerRadius(int radiusPx)
    {
        _settings.TaskbarCornerRadiusPx = Math.Clamp(radiusPx, 0, 120);
        SaveSettings();
        _statusBanner = $"Corner radius set to {_settings.TaskbarCornerRadiusPx}px";
        BroadcastState();
    }

    public void UpdateTheme(string theme)
    {
        _settings.Theme = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark";
        SaveSettings();
        _statusBanner = $"Theme changed to {_settings.Theme}";
        BroadcastState();
    }

    public void UpdateDateFormat(string? dateFormat)
    {
        _settings.DateFormat = string.IsNullOrWhiteSpace(dateFormat) ? "DD-MM-YYYY" : dateFormat.Trim();
        SaveSettings();
        _statusBanner = "Date format saved";
        BroadcastState();
    }

    public void UpdateOverrideWindowsKey(bool enabled)
    {
        _settings.OverrideWindowsKey = enabled;
        SaveSettings();
        _statusBanner = enabled ? "Windows key opens App Launcher" : "Windows key restored";
        WinKeyOverrideChanged?.Invoke(enabled);
        BroadcastState();
    }

    public void UpdateAutoStartOnLogon(bool enable)
    {
        _settings.AutoStartOnLogon = enable;
        SyncAutoStartRegistry(enable);
        SaveSettings();
        _statusBanner = enable ? "Auto-start on logon enabled" : "Auto-start on logon disabled";
        BroadcastState();
    }

    private static void SyncAutoStartRegistry(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key == null) return;

            const string appName = "WinZenShell";
            if (enable)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
                {
                    key.SetValue(appName, $"\"{exePath}\" --autostart");
                }
            }
            else
            {
                if (key.GetValue(appName) != null)
                {
                    key.DeleteValue(appName, throwOnMissingValue: false);
                }
            }
        }
        catch { }
    }

    public void UpdateWidgetState(
        string widgetId,
        bool enabled,
        string slot,
        int width,
        bool autoWidth = true,
        bool showAppName = true,
        int? order = null,
        bool combineApps = false,
        string? startIcon = null,
        string? startText = null,
        bool showStartText = false,
        string? displayMode = null,
        string? aspectRatio = null,
        bool? showThumbnails = null,
        string? batteryIconStyle = null,
        string? batteryPercentMode = null,
        string? startIconColorMode = null,
        bool? showSeconds = null,
        bool? showDate = null,
        bool? showCpu = null,
        bool? showRam = null,
        bool? showGpu = null,
        int? iconSizePx = null,
        int? iconPaddingPx = null,
        int? startIconScalePercent = null)
    {
        int? existingOrder = _settings.WidgetOverrides.TryGetValue(widgetId, out var prev) ? prev.Order : null;
        var existing = _settings.WidgetOverrides.TryGetValue(widgetId, out var existingOv) ? existingOv : null;

        _settings.WidgetOverrides[widgetId] = new WidgetStateOverride
        {
            Enabled = enabled,
            Slot = slot,
            Order = order ?? existingOrder,
            Width = Math.Clamp(width, 34, 720),
            AutoWidth = autoWidth,
            ShowAppName = showAppName,
            CombineApps = combineApps,
            ShowThumbnails = showThumbnails ?? (existing?.ShowThumbnails ?? true),
            IconSizePx = Math.Clamp(iconSizePx ?? (existing?.IconSizePx ?? 18), 12, 56),
            IconPaddingPx = Math.Clamp(iconPaddingPx ?? (existing?.IconPaddingPx ?? 9), 0, 18),
            StartIcon = !string.IsNullOrWhiteSpace(startIcon) ? startIcon : (existing?.StartIcon ?? "windows"),
            StartIconColorMode = !string.IsNullOrWhiteSpace(startIconColorMode) ? startIconColorMode : (existing?.StartIconColorMode ?? "text"),
            StartIconScalePercent = Math.Clamp(startIconScalePercent ?? (existing != null && existing.StartIconScalePercent > 0 ? existing.StartIconScalePercent : 100), 40, 100),
            StartText = startText != null ? startText : (existing?.StartText ?? "Start"),
            ShowStartText = showStartText,
            DisplayMode = !string.IsNullOrWhiteSpace(displayMode) ? displayMode : (existing?.DisplayMode ?? "widget"),
            AspectRatio = !string.IsNullOrWhiteSpace(aspectRatio) ? aspectRatio : (existing?.AspectRatio ?? "1:1"),
            BatteryIconStyle = !string.IsNullOrWhiteSpace(batteryIconStyle) ? batteryIconStyle : (existing?.BatteryIconStyle ?? "win11"),
            BatteryPercentMode = !string.IsNullOrWhiteSpace(batteryPercentMode) ? batteryPercentMode : (existing?.BatteryPercentMode ?? "outside-right"),
            ShowSeconds = showSeconds ?? (existing?.ShowSeconds ?? true),
            ShowDate = showDate ?? (existing?.ShowDate ?? true),
            ShowCpu = showCpu ?? (existing?.ShowCpu ?? true),
            ShowRam = showRam ?? (existing?.ShowRam ?? true),
            ShowGpu = showGpu ?? (existing?.ShowGpu ?? true)
        };
        SaveSettings();
        _statusBanner = "Widget updated";
        BroadcastState();
    }

    public (bool ShowCpu, bool ShowRam, bool ShowGpu) GetSystemMonitorStats()
    {
        var discovered = _widgetManager.DiscoverWidgets(_settings.WidgetOverrides);
        var match = discovered.Find(w => w.Id == "community.zen-system-monitor");
        if (match == null) return (true, true, true);
        return (match.ShowCpu, match.ShowRam, match.ShowGpu);
    }

    public void UpdateSystemMonitorStats(bool showCpu, bool showRam, bool showGpu)
    {
        const string widgetId = "community.zen-system-monitor";
        var discovered = _widgetManager.DiscoverWidgets(_settings.WidgetOverrides);
        var current = discovered.Find(w => w.Id == widgetId);
        UpdateWidgetState(
            widgetId,
            current?.Enabled ?? true,
            current?.Slot ?? "right",
            current?.Width ?? 160,
            current?.AutoWidth ?? true,
            current?.ShowAppName ?? true,
            current?.Order,
            current?.CombineApps ?? false,
            current?.StartIcon,
            current?.StartText,
            current?.ShowStartText ?? false,
            current?.DisplayMode,
            current?.AspectRatio,
            current?.ShowThumbnails,
            current?.BatteryIconStyle,
            current?.BatteryPercentMode,
            current?.StartIconColorMode,
            current?.ShowSeconds,
            current?.ShowDate,
            showCpu,
            showRam,
            showGpu);
    }

    public void ReorderWidgets(List<WidgetOrderUpdate> updates)
    {
        if (updates == null || updates.Count == 0) return;

        var discovered = _widgetManager.DiscoverWidgets(_settings.WidgetOverrides);
        foreach (var item in updates)
        {
            if (string.IsNullOrWhiteSpace(item.Id)) continue;
            var current = discovered.Find(w => w.Id == item.Id);
            if (!_settings.WidgetOverrides.TryGetValue(item.Id, out var ov))
            {
                ov = new WidgetStateOverride
                {
                    Enabled = item.Enabled ?? current?.Enabled ?? true,
                    Slot = current?.Slot ?? "right",
                    Width = current?.Width ?? 160,
                    AutoWidth = current?.AutoWidth ?? true,
                    ShowAppName = current?.ShowAppName ?? true,
                    CombineApps = current?.CombineApps ?? false,
                    ShowThumbnails = current?.ShowThumbnails ?? true,
                    StartIcon = current?.StartIcon ?? "windows",
                    StartText = current?.StartText ?? "Start",
                    ShowStartText = current?.ShowStartText ?? false,
                    DisplayMode = current?.DisplayMode ?? "widget",
                    AspectRatio = current?.AspectRatio ?? "1:1",
                    ShowSeconds = current?.ShowSeconds ?? true,
                    ShowDate = current?.ShowDate ?? true,
                    ShowCpu = current?.ShowCpu ?? true,
                    ShowRam = current?.ShowRam ?? true,
                    ShowGpu = current?.ShowGpu ?? true
                };
                _settings.WidgetOverrides[item.Id] = ov;
            }
            if (item.Enabled.HasValue)
            {
                ov.Enabled = item.Enabled.Value;
            }
            else if (string.Equals(item.Slot, "unused", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(item.Slot, "disabled", StringComparison.OrdinalIgnoreCase))
            {
                ov.Enabled = false;
            }
            if (!string.IsNullOrWhiteSpace(item.Slot))
            {
                ov.Slot = item.Slot.ToLowerInvariant();
            }
            ov.Order = item.Order;
        }

        SaveSettings();
        _statusBanner = "Arrangement saved";
        BroadcastState();
    }

    public void HideApp(long hwnd, string? appId, string? processName, string? title)
    {
        _systemMetricsService?.HideAppByIdentity(hwnd, appId, processName, title);
        _statusBanner = "App hidden";
        BroadcastState();
    }

    public void HideApp(long hwnd, string? processName, string? title)
    {
        HideApp(hwnd, null, processName, title);
    }

    public void UnhideApp(string appId)
    {
        _systemMetricsService?.UnhideAppByIdentity(appId);
        _statusBanner = "App restored";
        BroadcastState();
    }

    public void UnhideAllApps()
    {
        _systemMetricsService?.UnhideAllApps();
        _statusBanner = "Apps restored";
        BroadcastState();
    }

    public void CloseWindow(long hwnd)
    {
        _systemMetricsService?.CloseWindow(hwnd);
    }

    public bool IsAppPinned(string appId, string? processName = null)
    {
        if (_settings.PinnedApps == null) return false;
        return _settings.PinnedApps.Any(p =>
            (!string.IsNullOrWhiteSpace(appId) && string.Equals(p.AppId, appId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(processName) && string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase)));
    }

    public void PinApp(string appId, string processName, string title, string? exePath = null, string? iconDataUrl = null)
    {
        if (_settings.PinnedApps == null) _settings.PinnedApps = new();
        if (IsAppPinned(appId, processName)) return;

        if (string.IsNullOrWhiteSpace(exePath) && _systemMetricsService != null)
        {
            exePath = _systemMetricsService.GetExePathForApp(appId, processName);
        }
        if (string.IsNullOrWhiteSpace(iconDataUrl) && _systemMetricsService != null)
        {
            iconDataUrl = _systemMetricsService.GetIconForApp(appId, processName);
        }

        string nativeIcon = iconDataUrl ?? "";
        string packIcon = _iconPackService?.ResolveAppIconDataUrl(appId, processName, title, exePath) ?? "";

        _settings.PinnedApps.Add(new PinnedAppEntry
        {
            AppId = appId,
            ProcessName = processName,
            Title = string.IsNullOrWhiteSpace(title) ? processName : title,
            ExePath = exePath ?? "",
            IconDataUrl = !string.IsNullOrWhiteSpace(packIcon) ? packIcon : nativeIcon,
            NativeIconDataUrl = nativeIcon
        });

        SaveSettings();
        _statusBanner = $"Pinned {processName} to taskbar";
        BroadcastState();
    }

    public void UnpinApp(string appId, string? processName = null)
    {
        if (_settings.PinnedApps == null) return;
        int removed = _settings.PinnedApps.RemoveAll(p =>
            (!string.IsNullOrWhiteSpace(appId) && string.Equals(p.AppId, appId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(processName) && string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase)));

        if (removed > 0)
        {
            SaveSettings();
            _statusBanner = "Unpinned app from taskbar";
            BroadcastState();
        }
    }

    public void ReorderPinnedApps(List<string> orderedAppKeys)
    {
        if (_settings.PinnedApps == null || _settings.PinnedApps.Count <= 1 || orderedAppKeys == null || orderedAppKeys.Count == 0)
            return;

        var reordered = new List<PinnedAppEntry>(_settings.PinnedApps.Count);
        var remaining = new List<PinnedAppEntry>(_settings.PinnedApps);

        foreach (string rawKey in orderedAppKeys)
        {
            if (string.IsNullOrWhiteSpace(rawKey)) continue;
            string key = rawKey.Trim();
            int idx = remaining.FindIndex(p =>
                string.Equals(p.AppId, key, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.ProcessName, key, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                reordered.Add(remaining[idx]);
                remaining.RemoveAt(idx);
            }
        }

        if (remaining.Count > 0)
            reordered.AddRange(remaining);

        // Only save & broadcast if order actually changed
        bool changed = false;
        for (int i = 0; i < reordered.Count; i++)
        {
            if (!ReferenceEquals(reordered[i], _settings.PinnedApps[i]))
            {
                changed = true;
                break;
            }
        }

        if (!changed) return;

        _settings.PinnedApps = reordered;
        SaveSettings();
        BroadcastState();
    }

    public bool IsLauncherAppPinned(string targetPath, string? name = null)
    {
        if (_settings.AppLauncher?.PinnedApps == null) return false;
        foreach (var p in _settings.AppLauncher.PinnedApps)
        {
            if (p.IsFolder)
            {
                if (p.FolderItems != null && p.FolderItems.Any(sub =>
                    (!string.IsNullOrWhiteSpace(targetPath) && string.Equals(sub.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(name) && string.Equals(sub.Name, name, StringComparison.OrdinalIgnoreCase))))
                {
                    return true;
                }
            }
            else
            {
                if ((!string.IsNullOrWhiteSpace(targetPath) && string.Equals(p.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(name) && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public void PinLauncherApp(string targetPath, string name, string? iconDataUrl = null)
    {
        if (_settings.AppLauncher == null) _settings.AppLauncher = new();
        if (_settings.AppLauncher.PinnedApps == null) _settings.AppLauncher.PinnedApps = new();

        if (IsLauncherAppPinned(targetPath, name)) return;

        string packIcon = _iconPackService?.ResolveAppIconDataUrl(name, Path.GetFileNameWithoutExtension(targetPath), name, targetPath) ?? "";

        _settings.AppLauncher.PinnedApps.Add(new AppLauncherPinnedItem
        {
            Id = targetPath,
            TargetPath = targetPath,
            Name = name,
            IconDataUrl = !string.IsNullOrWhiteSpace(packIcon) ? packIcon : (iconDataUrl ?? "")
        });
        SaveSettings();
        _statusBanner = $"Pinned {name} to launcher";
        BroadcastState();
    }

    public void UnpinLauncherApp(string targetPath)
    {
        if (_settings.AppLauncher?.PinnedApps == null) return;
        int removed = _settings.AppLauncher.PinnedApps.RemoveAll(p =>
            !p.IsFolder && (
                string.Equals(p.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name, targetPath, StringComparison.OrdinalIgnoreCase)));

        foreach (var f in _settings.AppLauncher.PinnedApps.Where(p => p.IsFolder).ToList())
        {
            if (f.FolderItems != null)
            {
                int fRemoved = f.FolderItems.RemoveAll(sub =>
                    string.Equals(sub.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sub.Name, targetPath, StringComparison.OrdinalIgnoreCase));
                if (fRemoved > 0)
                {
                    removed += fRemoved;
                    if (f.FolderItems.Count == 1)
                    {
                        int fIdx = _settings.AppLauncher.PinnedApps.IndexOf(f);
                        if (fIdx >= 0)
                        {
                            _settings.AppLauncher.PinnedApps[fIdx] = f.FolderItems[0];
                        }
                    }
                    else if (f.FolderItems.Count == 0)
                    {
                        _settings.AppLauncher.PinnedApps.Remove(f);
                    }
                }
            }
        }

        if (removed > 0)
        {
            SaveSettings();
            _statusBanner = "Unpinned app from launcher";
            BroadcastState();
        }
    }

    public void UnpinLauncherFolder(string folderId)
    {
        if (_settings.AppLauncher?.PinnedApps == null) return;
        int removed = _settings.AppLauncher.PinnedApps.RemoveAll(p => p.IsFolder && (string.Equals(p.Id, folderId, StringComparison.OrdinalIgnoreCase) || string.Equals(p.Name, folderId, StringComparison.OrdinalIgnoreCase)));
        if (removed > 0)
        {
            SaveSettings();
            _statusBanner = "Removed folder from launcher";
            BroadcastState();
        }
    }

    public void SavePinnedLauncherApps(List<AppLauncherPinnedItem> items)
    {
        if (_settings.AppLauncher == null) _settings.AppLauncher = new();
        _settings.AppLauncher.PinnedApps = items ?? new();
        SaveSettings();
        _statusBanner = "Pinned apps updated";
        BroadcastState();
    }

    public void ResetPinnedLauncherApps()
    {
        if (_settings.AppLauncher?.PinnedApps == null) return;
        _settings.AppLauncher.PinnedApps.Clear();
        SaveSettings();
        _statusBanner = "Launcher pinned apps reset";
        BroadcastState();
    }

    public void ReorderPinnedLauncherApps(List<string> orderedTargets)
    {
        if (_settings.AppLauncher?.PinnedApps == null || _settings.AppLauncher.PinnedApps.Count <= 1 || orderedTargets == null || orderedTargets.Count == 0)
            return;

        var reordered = new List<AppLauncherPinnedItem>(_settings.AppLauncher.PinnedApps.Count);
        var remaining = new List<AppLauncherPinnedItem>(_settings.AppLauncher.PinnedApps);

        foreach (string rawTarget in orderedTargets)
        {
            if (string.IsNullOrWhiteSpace(rawTarget)) continue;
            string target = rawTarget.Trim();
            int idx = remaining.FindIndex(p =>
                string.Equals(p.TargetPath, target, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name, target, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                reordered.Add(remaining[idx]);
                remaining.RemoveAt(idx);
            }
        }

        if (remaining.Count > 0)
            reordered.AddRange(remaining);

        bool changed = false;
        for (int i = 0; i < reordered.Count; i++)
        {
            if (!ReferenceEquals(reordered[i], _settings.AppLauncher.PinnedApps[i]))
            {
                changed = true;
                break;
            }
        }

        if (!changed) return;

        _settings.AppLauncher.PinnedApps = reordered;
        SaveSettings();
        _statusBanner = "Launcher pinned apps reordered";
        BroadcastState();
    }

    public void MovePinnedLauncherAppToFront(string targetPath)
    {
        if (_settings.AppLauncher?.PinnedApps == null || _settings.AppLauncher.PinnedApps.Count <= 1) return;
        int idx = _settings.AppLauncher.PinnedApps.FindIndex(p =>
            string.Equals(p.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Name, targetPath, StringComparison.OrdinalIgnoreCase));
        if (idx > 0)
        {
            var item = _settings.AppLauncher.PinnedApps[idx];
            _settings.AppLauncher.PinnedApps.RemoveAt(idx);
            _settings.AppLauncher.PinnedApps.Insert(0, item);
            SaveSettings();
            _statusBanner = $"Moved {item.Name} to front";
            BroadcastState();
        }
    }

    public void MovePinnedLauncherApp(string targetPath, int offset)
    {
        if (_settings.AppLauncher?.PinnedApps == null || _settings.AppLauncher.PinnedApps.Count <= 1 || offset == 0) return;
        int idx = _settings.AppLauncher.PinnedApps.FindIndex(p =>
            string.Equals(p.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Name, targetPath, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
        {
            int newIdx = Math.Clamp(idx + offset, 0, _settings.AppLauncher.PinnedApps.Count - 1);
            if (newIdx != idx)
            {
                var item = _settings.AppLauncher.PinnedApps[idx];
                _settings.AppLauncher.PinnedApps.RemoveAt(idx);
                _settings.AppLauncher.PinnedApps.Insert(newIdx, item);
                SaveSettings();
                _statusBanner = $"Moved {item.Name}";
                BroadcastState();
            }
        }
    }

    public bool IsLauncherAppHidden(string targetPath, string? name = null)
    {
        if (_settings.AppLauncher?.HiddenApps == null) return false;
        return _settings.AppLauncher.HiddenApps.Any(h =>
            (!string.IsNullOrWhiteSpace(targetPath) && string.Equals(h, targetPath, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(name) && string.Equals(h, name, StringComparison.OrdinalIgnoreCase)));
    }

    public void HideLauncherApp(string targetPath, string name)
    {
        if (_settings.AppLauncher == null) _settings.AppLauncher = new();
        if (_settings.AppLauncher.HiddenApps == null) _settings.AppLauncher.HiddenApps = new();

        string key = !string.IsNullOrWhiteSpace(name) ? name : targetPath;
        if (!_settings.AppLauncher.HiddenApps.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            _settings.AppLauncher.HiddenApps.Add(key);
        }
        if (!string.IsNullOrWhiteSpace(targetPath) && !_settings.AppLauncher.HiddenApps.Contains(targetPath, StringComparer.OrdinalIgnoreCase))
        {
            _settings.AppLauncher.HiddenApps.Add(targetPath);
        }

        // Also remove from pinned if it was pinned
        _settings.AppLauncher.PinnedApps?.RemoveAll(p =>
            string.Equals(p.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        SaveSettings();
        _statusBanner = $"Hidden {name} from launcher";
        BroadcastState();
    }

    public void UnhideLauncherApp(string targetPathOrName)
    {
        if (_settings.AppLauncher?.HiddenApps == null) return;
        int removed = _settings.AppLauncher.HiddenApps.RemoveAll(h =>
            string.Equals(h, targetPathOrName, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            SaveSettings();
            _statusBanner = "App restored in launcher";
            BroadcastState();
        }
    }

    public void ResetHiddenLauncherApps()
    {
        if (_settings.AppLauncher?.HiddenApps == null) return;
        _settings.AppLauncher.HiddenApps.Clear();
        SaveSettings();
        _statusBanner = "Launcher hidden apps reset";
        BroadcastState();
    }

    public void UpdateAppLauncherConfig(
        bool? showPinned,
        bool? showRecent,
        bool? showSteam,
        bool? overrideWinKey,
        string? layoutVariant = null,
        string? win81TileColorMode = null,
        bool? win81SplitView = null,
        bool? win81FullScreen = null,
        string? win81ProfileName = null,
        string? win81ProfilePicture = null)
    {
        if (_settings.AppLauncher == null) _settings.AppLauncher = new();
        if (showPinned.HasValue) _settings.AppLauncher.ShowPinnedApps = showPinned.Value;
        if (showRecent.HasValue) _settings.AppLauncher.ShowRecentApps = showRecent.Value;
        if (showSteam.HasValue) _settings.AppLauncher.ShowSteamGames = showSteam.Value;
        if (!string.IsNullOrWhiteSpace(layoutVariant))
            _settings.AppLauncher.LayoutVariant = layoutVariant == "win81" ? "win81" : "default";
        if (!string.IsNullOrWhiteSpace(win81TileColorMode))
            _settings.AppLauncher.Win81TileColorMode = win81TileColorMode == "accent" ? "accent" : "multicolor";
        if (win81SplitView.HasValue)
            _settings.AppLauncher.Win81SplitView = win81SplitView.Value;
        if (win81FullScreen.HasValue)
            _settings.AppLauncher.Win81FullScreen = win81FullScreen.Value;
        if (win81ProfileName != null)
            _settings.AppLauncher.Win81ProfileName = win81ProfileName.Trim();
        if (win81ProfilePicture != null)
            _settings.AppLauncher.Win81ProfilePicture = win81ProfilePicture;
        if (overrideWinKey.HasValue)
        {
            _settings.OverrideWindowsKey = overrideWinKey.Value;
            WinKeyOverrideChanged?.Invoke(_settings.OverrideWindowsKey);
        }
        SaveSettings();
        _statusBanner = "App launcher settings updated";
        BroadcastState();
    }

    public void CreateNewCommunityWidget(string name, string author, string slot, int width)
    {
        var manifest = _widgetManager.CreateCommunityWidgetScaffold(name, author, slot, width);
        _statusBanner = $"Created community widget '{manifest.Name}' in Widgets/{manifest.FolderName}";
        BroadcastState();
    }

    public void UpdateDynamicIslandConfig(bool enabled, string placement, string slot, string timeFormat, bool showSeconds, bool showDate, bool enableAnimation = true, bool reserveWorkArea = true, bool enableMusicIsland = true, bool? pinLyrics = null, bool? widePill = null)
    {
        if (string.Equals(_settings.DockEdge, "Top", StringComparison.OrdinalIgnoreCase) && placement == "top-center")
        {
            placement = "taskbar";
        }

        _settings.DynamicIsland.Enabled = enabled;
        _settings.DynamicIsland.Placement = placement;
        _settings.DynamicIsland.TaskbarSlot = slot;
        _settings.DynamicIsland.TimeFormat = timeFormat;
        _settings.DynamicIsland.ShowSeconds = showSeconds;
        _settings.DynamicIsland.ShowDate = showDate;
        _settings.DynamicIsland.EnableAnimation = enableAnimation;
        _settings.DynamicIsland.ReserveWorkArea = reserveWorkArea;
        _settings.DynamicIsland.EnableMusicIsland = enableMusicIsland;
        if (pinLyrics.HasValue)
            _settings.DynamicIsland.PinLyrics = pinLyrics.Value;
        if (widePill.HasValue)
            _settings.DynamicIsland.WidePill = widePill.Value;

        SaveSettings();
        SyncTaskbarAndWorkArea();
        _statusBanner = enabled ? $"Dynamic Island ({placement}) enabled" : "Dynamic Island disabled";
        BroadcastState();
        DynamicIslandVisibilityChanged?.Invoke();
    }

    public void SetDynamicIslandPinLyrics(bool isPinned)
    {
        if (_settings.DynamicIsland.PinLyrics == isPinned) return;
        _settings.DynamicIsland.PinLyrics = isPinned;
        SaveSettings();
        BroadcastState();
    }

    public void SetDynamicIslandWidePill(bool isWide)
    {
        if (_settings.DynamicIsland.WidePill == isWide) return;
        _settings.DynamicIsland.WidePill = isWide;
        SaveSettings();
        BroadcastState();
    }

    public void SyncTaskbarAndWorkArea()
    {
        IntPtr taskbarHwnd = GetTaskbarHwnd?.Invoke() ?? IntPtr.Zero;
        IntPtr dynamicIslandHwnd = GetDynamicIslandHwnd?.Invoke() ?? IntPtr.Zero;
        int topReservedWorkAreaPx = (_settings.DynamicIsland.Enabled &&
                                     _settings.DynamicIsland.ReserveWorkArea &&
                                     string.Equals(_settings.DynamicIsland.Placement, "top-center", StringComparison.OrdinalIgnoreCase) &&
                                     !string.Equals(_settings.DockEdge, "Top", StringComparison.OrdinalIgnoreCase))
            ? 44
            : 0;

        var rect = _workAreaService.ApplyTaskbarBoundsAndWorkArea(
            taskbarHwnd,
            _settings.EnableWinZenTaskbar,
            _settings.RespectDesktopWorkArea,
            _settings.DockEdge,
            _settings.TaskbarHeightPx,
            _settings.FloatingMarginPx,
            _settings.HideNativeTaskbar,
            topReservedWorkAreaPx,
            dynamicIslandHwnd);
        TaskbarBoundsUpdated?.Invoke(rect);
        DynamicIslandVisibilityChanged?.Invoke();
    }

    public void EmergencyRestoreAll()
    {
        IntPtr taskbarHwnd = GetTaskbarHwnd?.Invoke() ?? IntPtr.Zero;
        _workAreaService.UnregisterAppBar(taskbarHwnd);
        _nativeTaskbarService.RestoreNativeTaskbar();
        _settings.HideNativeTaskbar = false;
        SaveSettings();
        _statusBanner = "Native restored";
        BroadcastState();
    }

    public void UpdateControlCenterWidgets(List<ControlCenterItemConfig> items)
    {
        if (items == null) return;
        foreach (var it in items)
        {
            if (it.AspectRatio != null && it.AspectRatio.Contains('x'))
            {
                var parts = it.AspectRatio.Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out int c) && int.TryParse(parts[1], out int r))
                {
                    it.ColSpan = c;
                    it.RowSpan = r;
                }
            }
        }
        _settings.ControlCenterWidgets = items;
        SaveSettings();
        _statusBanner = "Control Center saved";
        BroadcastState();
    }

    private static T DeepClone<T>(T value) where T : new()
    {
        try
        {
            string json = JsonSerializer.Serialize(value);
            return JsonSerializer.Deserialize<T>(json) ?? new T();
        }
        catch
        {
            return new T();
        }
    }

    private void CaptureCurrentStateIntoPreset(LayoutPreset preset)
    {
        preset.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");
        preset.WidgetOverrides = DeepClone(_settings.WidgetOverrides ?? new());
        preset.ControlCenterWidgets = DeepClone(_settings.ControlCenterWidgets ?? new());
        preset.HideNativeTaskbar = _settings.HideNativeTaskbar;
        preset.DockEdge = _settings.DockEdge;
        preset.TaskbarHeightPx = _settings.TaskbarHeightPx;
        preset.FloatingMarginPx = _settings.FloatingMarginPx;
        preset.TaskbarCornerRadiusPx = _settings.TaskbarCornerRadiusPx;
        preset.TaskbarStyle = _settings.TaskbarStyle;
        preset.BackgroundOpacity = _settings.BackgroundOpacity;
        preset.BackgroundBlurStrength = _settings.BackgroundBlurStrength;
        preset.FlyoutBackgroundOpacity = _settings.FlyoutBackgroundOpacity;
        preset.FlyoutBackgroundBlurStrength = _settings.FlyoutBackgroundBlurStrength;
        preset.DynamicIslandEnabled = _settings.DynamicIsland?.Enabled;
        preset.DynamicIslandPlacement = _settings.DynamicIsland?.Placement;
        preset.DynamicIslandTaskbarSlot = _settings.DynamicIsland?.TaskbarSlot;
        preset.AppLauncher = DeepClone(_settings.AppLauncher ?? new());
        preset.OverrideWindowsKey = _settings.OverrideWindowsKey;
    }

    public void SaveLayoutPreset(string? name)
    {
        _settings.LayoutPresets ??= new();
        string cleanName = string.IsNullOrWhiteSpace(name)
            ? $"Layout {_settings.LayoutPresets.Count + 1}"
            : name.Trim();

        var preset = new LayoutPreset
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = cleanName
        };
        CaptureCurrentStateIntoPreset(preset);

        _settings.LayoutPresets.Add(preset);
        _settings.ActiveLayoutPresetId = preset.Id;
        SaveSettings();
        _statusBanner = $"Saved layout '{preset.Name}'";
        BroadcastState();
    }

    public void ApplyLayoutPreset(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || _settings.LayoutPresets == null) return;
        var preset = _settings.LayoutPresets.Find(p => p.Id == id);
        if (preset == null) return;

        _settings.WidgetOverrides = DeepClone(preset.WidgetOverrides ?? new());
        _settings.ControlCenterWidgets = DeepClone(preset.ControlCenterWidgets ?? new());

        if (preset.HideNativeTaskbar.HasValue && preset.HideNativeTaskbar.Value != _settings.HideNativeTaskbar)
        {
            _ = ToggleHideNativeTaskbarAsync(preset.HideNativeTaskbar.Value);
        }
        if (!string.IsNullOrWhiteSpace(preset.DockEdge))
            _settings.DockEdge = preset.DockEdge;
        if (preset.TaskbarHeightPx.HasValue)
            _settings.TaskbarHeightPx = Math.Clamp(preset.TaskbarHeightPx.Value, 36, 96);
        if (preset.FloatingMarginPx.HasValue)
            _settings.FloatingMarginPx = Math.Clamp(preset.FloatingMarginPx.Value, 0, 32);
        if (preset.TaskbarCornerRadiusPx.HasValue)
            _settings.TaskbarCornerRadiusPx = Math.Clamp(preset.TaskbarCornerRadiusPx.Value, 0, 120);
        if (!string.IsNullOrWhiteSpace(preset.TaskbarStyle))
            _settings.TaskbarStyle = string.Equals(preset.TaskbarStyle, "separated", StringComparison.OrdinalIgnoreCase) ? "separated" : "unified";
        if (preset.BackgroundOpacity.HasValue)
            _settings.BackgroundOpacity = Math.Clamp(preset.BackgroundOpacity.Value, 0, 100);
        if (preset.BackgroundBlurStrength.HasValue)
            _settings.BackgroundBlurStrength = Math.Clamp(preset.BackgroundBlurStrength.Value, 0, 100);
        if (preset.FlyoutBackgroundOpacity.HasValue)
            _settings.FlyoutBackgroundOpacity = Math.Clamp(preset.FlyoutBackgroundOpacity.Value, 0, 100);
        if (preset.FlyoutBackgroundBlurStrength.HasValue)
            _settings.FlyoutBackgroundBlurStrength = Math.Clamp(preset.FlyoutBackgroundBlurStrength.Value, 0, 100);

        _settings.DynamicIsland ??= new();
        if (preset.DynamicIslandEnabled.HasValue)
            _settings.DynamicIsland.Enabled = preset.DynamicIslandEnabled.Value;
        if (!string.IsNullOrWhiteSpace(preset.DynamicIslandPlacement))
            _settings.DynamicIsland.Placement = preset.DynamicIslandPlacement;
        if (!string.IsNullOrWhiteSpace(preset.DynamicIslandTaskbarSlot))
            _settings.DynamicIsland.TaskbarSlot = preset.DynamicIslandTaskbarSlot;

        if (string.Equals(_settings.DockEdge, "Top", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_settings.DynamicIsland.Placement, "top-center", StringComparison.OrdinalIgnoreCase))
        {
            _settings.DynamicIsland.Placement = "taskbar";
        }

        if (preset.AppLauncher != null)
            _settings.AppLauncher = DeepClone(preset.AppLauncher);
        if (preset.OverrideWindowsKey.HasValue && preset.OverrideWindowsKey.Value != _settings.OverrideWindowsKey)
        {
            _settings.OverrideWindowsKey = preset.OverrideWindowsKey.Value;
            WinKeyOverrideChanged?.Invoke(preset.OverrideWindowsKey.Value);
        }

        _settings.ActiveLayoutPresetId = preset.Id;
        SaveSettings();
        SyncTaskbarAndWorkArea();
        _statusBanner = $"Applied layout '{preset.Name}'";
        BroadcastState();
        DynamicIslandVisibilityChanged?.Invoke();
    }

    public void UpdateLayoutPreset(string? id, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(id) || _settings.LayoutPresets == null) return;
        var preset = _settings.LayoutPresets.Find(p => p.Id == id);
        if (preset == null) return;

        if (!string.IsNullOrWhiteSpace(name))
        {
            preset.Name = name.Trim();
        }
        CaptureCurrentStateIntoPreset(preset);
        _settings.ActiveLayoutPresetId = preset.Id;
        SaveSettings();
        _statusBanner = $"Updated layout '{preset.Name}'";
        BroadcastState();
    }

    public void RenameLayoutPreset(string? id, string? name)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || _settings.LayoutPresets == null) return;
        var preset = _settings.LayoutPresets.Find(p => p.Id == id);
        if (preset == null) return;

        preset.Name = name.Trim();
        SaveSettings();
        _statusBanner = $"Renamed layout to '{preset.Name}'";
        BroadcastState();
    }

    public void DeleteLayoutPreset(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || _settings.LayoutPresets == null) return;
        int removed = _settings.LayoutPresets.RemoveAll(p => p.Id == id);
        if (removed == 0) return;

        if (_settings.ActiveLayoutPresetId == id)
        {
            _settings.ActiveLayoutPresetId = null;
        }
        SaveSettings();
        _statusBanner = "Layout preset deleted";
        BroadcastState();
    }

    public void UpdateIconPack(string? packId)
    {
        string normalized = string.IsNullOrWhiteSpace(packId) ? "native" : packId.Trim();
        _settings.IconPack = normalized;
        _iconPackService?.SetActivePack(normalized, notify: true);
        _settings.IconPack = _iconPackService?.ActivePackId ?? normalized;
        ApplyIconPackToPinnedApps();
        _systemMetricsService?.ClearIconCaches();
        SaveSettings();
        _statusBanner = string.Equals(_settings.IconPack, "native", StringComparison.OrdinalIgnoreCase)
            ? "Using native Windows icons"
            : $"Icon pack set to '{_settings.IconPack}'";
        BroadcastState();
    }

    public void RefreshIconPacks()
    {
        _iconPackService?.RefreshPacks();
        _settings.IconPack = _iconPackService?.ActivePackId ?? _settings.IconPack;
        ApplyIconPackToPinnedApps();
        _systemMetricsService?.ClearIconCaches();
        _statusBanner = "Icon packs refreshed";
        BroadcastState();
    }

    public void DeleteIconPack(string? packId)
    {
        if (_iconPackService == null || string.IsNullOrWhiteSpace(packId)) return;
        if (_iconPackService.DeletePack(packId))
        {
            _settings.IconPack = _iconPackService.ActivePackId;
            ApplyIconPackToPinnedApps();
            _systemMetricsService?.ClearIconCaches();
            SaveSettings();
            _statusBanner = "Icon pack deleted";
            BroadcastState();
        }
    }

    public bool ImportIconPackFromPath(string sourcePath, out string message, Action<int, string>? onProgress = null)
    {
        if (_iconPackService == null)
        {
            message = "Icon pack service unavailable.";
            return false;
        }

        bool ok = _iconPackService.ImportIconPack(sourcePath, out string importedId, out message, onProgress);
        if (ok && !string.IsNullOrWhiteSpace(importedId))
        {
            _settings.IconPack = importedId;
            ApplyIconPackToPinnedApps();
            _systemMetricsService?.ClearIconCaches();
            SaveSettings();
        }
        _statusBanner = message;
        BroadcastState();
        return ok;
    }

    private void ApplyIconPackToPinnedApps()
    {
        if (_settings.PinnedApps == null) return;
        foreach (var pin in _settings.PinnedApps)
        {
            if (string.IsNullOrWhiteSpace(pin.NativeIconDataUrl) && !string.IsNullOrWhiteSpace(pin.IconDataUrl))
            {
                pin.NativeIconDataUrl = pin.IconDataUrl;
            }
            string packIcon = _iconPackService?.ResolveAppIconDataUrl(pin.AppId, pin.ProcessName, pin.Title, pin.ExePath) ?? "";
            pin.IconDataUrl = !string.IsNullOrWhiteSpace(packIcon) ? packIcon : pin.NativeIconDataUrl;
        }
    }

    public ShellFullStatePayload BuildPayload()
    {
        var allWidgets = _widgetManager.DiscoverWidgets(_settings.WidgetOverrides);

        var ccPayload = new List<ControlCenterItemPayload>();
        foreach (var cc in _settings.ControlCenterWidgets)
        {
            var match = allWidgets.Find(w => w.Id == cc.Id || w.FolderName == cc.Id || $"community.{w.FolderName}" == cc.Id);
            if (match != null)
            {
                int colSpan = cc.ColSpan;
                int rowSpan = cc.RowSpan;
                string ratio = cc.AspectRatio ?? "";
                if (ratio.Contains('x'))
                {
                    var parts = ratio.Split('x');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int c) && int.TryParse(parts[1], out int r))
                    {
                        colSpan = Math.Clamp(c, 1, 4);
                        rowSpan = Math.Clamp(r, 1, 2);
                    }
                }
                else if (colSpan <= 0 || rowSpan <= 0)
                {
                    if (ratio == "1:2")
                    {
                        colSpan = (match.Id.Contains("volume") || match.Id.Contains("brightness") || match.Id.Contains("battery")) ? 4 : 2;
                        rowSpan = 1;
                    }
                    else if (ratio == "1:1")
                    {
                        colSpan = 2;
                        rowSpan = 2;
                    }
                    else
                    {
                        colSpan = 2;
                        rowSpan = 1;
                    }
                }
                string finalRatio = $"{colSpan}x{rowSpan}";

                ccPayload.Add(new ControlCenterItemPayload
                {
                    Id = match.Id,
                    Name = match.Name,
                    Order = cc.Order,
                    AspectRatio = finalRatio,
                    ColSpan = colSpan,
                    RowSpan = rowSpan,
                    Row = cc.Row,
                    Col = cc.Col,
                    Enabled = cc.Enabled,
                    HtmlContent = match.HtmlContent,
                    CssContent = match.CssContent,
                    JsContent = match.JsContent,
                    BatteryIconStyle = match.BatteryIconStyle,
                    BatteryPercentMode = match.BatteryPercentMode,
                    StartIcon = match.StartIcon,
                    StartIconColorMode = match.StartIconColorMode,
                    ShowSeconds = match.ShowSeconds,
                    ShowDate = match.ShowDate,
                    ShowCpu = match.ShowCpu,
                    ShowRam = match.ShowRam,
                    ShowGpu = match.ShowGpu
                });
            }
        }

        return new ShellFullStatePayload
        {
            Settings = _settings,
            NativeTaskbar = _nativeTaskbarService.GetCurrentStatus(),
            WorkArea = _workAreaService.GetTelemetry(_settings.DockEdge),
            Widgets = allWidgets,
            ControlCenterWidgets = ccPayload.OrderBy(c => c.Order).ToList(),
            HiddenApps = _systemMetricsService?.GetHiddenApps() ?? new(),
            IconPacks = _iconPackService?.GetInstalledPacks() ?? new(),
            IconPacksFolderPath = _iconPackService?.UserPacksDirectory ?? "",
            WidgetsFolderPath = _widgetManager.WidgetsRootPath,
            StatusBanner = _statusBanner
        };
    }

    public void BroadcastState()
    {
        StateChanged?.Invoke(BuildPayload());
    }
}
