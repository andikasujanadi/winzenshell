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
    public string AspectRatio { get; set; } = "1:1"; // "1:1" or "1:2"

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
    public string AspectRatio { get; set; } = "1:1";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("htmlContent")]
    public string HtmlContent { get; set; } = "";

    [JsonPropertyName("cssContent")]
    public string CssContent { get; set; } = "";

    [JsonPropertyName("jsContent")]
    public string JsContent { get; set; } = "";
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
    public string AccentColor { get; set; } = "#3684E3";
    public string Theme { get; set; } = "dark"; // "dark" or "light"
    public Dictionary<string, WidgetStateOverride> WidgetOverrides { get; set; } = new();
    public List<ControlCenterItemConfig> ControlCenterWidgets { get; set; } = new();
    public List<PinnedAppEntry> PinnedApps { get; set; } = new();
}

public class ShellFullStatePayload
{
    public ShellSettings Settings { get; set; } = new();
    public NativeTaskbarStatus NativeTaskbar { get; set; } = new();
    public WorkAreaTelemetry WorkArea { get; set; } = new();
    public List<WidgetManifest> Widgets { get; set; } = new();
    public List<ControlCenterItemPayload> ControlCenterWidgets { get; set; } = new();
    public List<HiddenAppEntry> HiddenApps { get; set; } = new();
    public string WidgetsFolderPath { get; set; } = "";
    public string StatusBanner { get; set; } = "Ready";
}

public class ShellStateService
{
    private readonly NativeTaskbarService _nativeTaskbarService;
    private readonly WorkAreaAppBarService _workAreaService;
    private readonly WidgetManagerService _widgetManager;
    private readonly SystemMetricsService? _systemMetricsService;
    private readonly string _settingsFilePath;

    private ShellSettings _settings = new();
    private string _statusBanner = "WinZenShell Active — Ready";

    public event Action<ShellFullStatePayload>? StateChanged;
    public event Action<bool>? TaskbarVisibilityRequested;
    public event Action<Win32Api.RECT>? TaskbarBoundsUpdated;

    public Func<IntPtr>? GetTaskbarHwnd { get; set; }

    public ShellSettings Settings => _settings;
    public NativeTaskbarService NativeTaskbar => _nativeTaskbarService;
    public WorkAreaAppBarService WorkArea => _workAreaService;
    public WidgetManagerService WidgetManager => _widgetManager;

    public ShellStateService(
        NativeTaskbarService nativeTaskbarService,
        WorkAreaAppBarService workAreaService,
        WidgetManagerService widgetManager,
        SystemMetricsService? systemMetricsService = null)
    {
        _nativeTaskbarService = nativeTaskbarService;
        _workAreaService = workAreaService;
        _widgetManager = widgetManager;
        _systemMetricsService = systemMetricsService;

        string configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinZenShell");
        Directory.CreateDirectory(configDir);
        _settingsFilePath = Path.Combine(configDir, "settings.json");

        LoadSettings();
    }

    public void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<ShellSettings>(json);
                if (loaded != null)
                {
                    _settings = loaded;
                    // On fresh app start, always start with native taskbar visible until user toggles or we apply safely
                    _settings.HideNativeTaskbar = false;
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
                new() { Id = "community.zen-system-monitor", Order = 30, AspectRatio = "1:1", Enabled = true }
            };
        }
    }

    public void SaveSettings()
    {
        try
        {
            string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
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

    public void UpdateWorkAreaConfig(
        bool respectWorkArea,
        string dockEdge,
        int heightPx,
        int marginPx,
        string? theme = null,
        int? cornerRadiusPx = null,
        string? accentColor = null)
    {
        _settings.RespectDesktopWorkArea = respectWorkArea;
        if (!string.IsNullOrWhiteSpace(dockEdge))
            _settings.DockEdge = dockEdge;
        _settings.TaskbarHeightPx = Math.Clamp(heightPx, 32, 96);
        _settings.FloatingMarginPx = Math.Clamp(marginPx, 0, 24);
        if (cornerRadiusPx.HasValue)
            _settings.TaskbarCornerRadiusPx = Math.Clamp(cornerRadiusPx.Value, 0, 32);
        if (!string.IsNullOrWhiteSpace(accentColor))
            _settings.AccentColor = accentColor;
        if (!string.IsNullOrWhiteSpace(theme))
            _settings.Theme = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark";
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
        _settings.TaskbarCornerRadiusPx = Math.Clamp(radiusPx, 0, 32);
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
        bool? showThumbnails = null)
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
            StartIcon = !string.IsNullOrWhiteSpace(startIcon) ? startIcon : (existing?.StartIcon ?? "windows"),
            StartText = startText != null ? startText : (existing?.StartText ?? "Start"),
            ShowStartText = showStartText,
            DisplayMode = !string.IsNullOrWhiteSpace(displayMode) ? displayMode : (existing?.DisplayMode ?? "widget"),
            AspectRatio = !string.IsNullOrWhiteSpace(aspectRatio) ? aspectRatio : (existing?.AspectRatio ?? "1:1")
        };
        SaveSettings();
        _statusBanner = "Widget updated";
        BroadcastState();
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
                    AspectRatio = current?.AspectRatio ?? "1:1"
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

        _settings.PinnedApps.Add(new PinnedAppEntry
        {
            AppId = appId,
            ProcessName = processName,
            Title = string.IsNullOrWhiteSpace(title) ? processName : title,
            ExePath = exePath ?? "",
            IconDataUrl = iconDataUrl ?? ""
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

    public void CreateNewCommunityWidget(string name, string author, string slot, int width)
    {
        var manifest = _widgetManager.CreateCommunityWidgetScaffold(name, author, slot, width);
        _statusBanner = $"Created community widget '{manifest.Name}' in Widgets/{manifest.FolderName}";
        BroadcastState();
    }

    public void SyncTaskbarAndWorkArea()
    {
        IntPtr taskbarHwnd = GetTaskbarHwnd?.Invoke() ?? IntPtr.Zero;
        var rect = _workAreaService.ApplyTaskbarBoundsAndWorkArea(
            taskbarHwnd,
            _settings.EnableWinZenTaskbar,
            _settings.RespectDesktopWorkArea,
            _settings.DockEdge,
            _settings.TaskbarHeightPx,
            _settings.FloatingMarginPx,
            _settings.HideNativeTaskbar);
        TaskbarBoundsUpdated?.Invoke(rect);
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
        _settings.ControlCenterWidgets = items;
        SaveSettings();
        _statusBanner = "Control Center saved";
        BroadcastState();
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
                ccPayload.Add(new ControlCenterItemPayload
                {
                    Id = match.Id,
                    Name = match.Name,
                    Order = cc.Order,
                    AspectRatio = !string.IsNullOrWhiteSpace(cc.AspectRatio) ? cc.AspectRatio : "1:1",
                    Enabled = cc.Enabled,
                    HtmlContent = match.HtmlContent,
                    CssContent = match.CssContent,
                    JsContent = match.JsContent
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
            WidgetsFolderPath = _widgetManager.WidgetsRootPath,
            StatusBanner = _statusBanner
        };
    }

    public void BroadcastState()
    {
        StateChanged?.Invoke(BuildPayload());
    }
}
