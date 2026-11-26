using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinZenShell.Native;

namespace WinZenShell.Services;

public class AppLauncherItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public string ResolvedExePath { get; set; } = "";
    public string IconDataUrl { get; set; } = "";
    public string NativeIconDataUrl { get; set; } = "";
    public bool IsRecent { get; set; }
    public int LaunchCount { get; set; }
    public bool IsFolder { get; set; }
    public string FolderSize { get; set; } = "1x1";
    public string TileSize { get; set; } = "1x1";
    public string GroupName { get; set; } = "";
    public List<AppLauncherItem> FolderItems { get; set; } = new();
}

public class AppLauncherService
{
    private readonly string _recentsFilePath;
    private readonly List<AppLauncherItem> _apps = new();
    private readonly HashSet<string> _seenNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _shortcutExeCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly object _lock = new();
    private IconPackService? _iconPackService;
    private System.Threading.Timer? _debounceTimer;
    private int _isScanning;
    private DateTime _lastScanUtc = DateTime.MinValue;
    private Dictionary<string, int> _launchStats = new(StringComparer.OrdinalIgnoreCase);

    public bool IsLoaded { get; private set; }
    public event Action? AppsLoaded;

    public AppLauncherService(IconPackService? iconPackService = null)
    {
        _iconPackService = iconPackService;
        if (_iconPackService != null)
        {
            _iconPackService.ActivePackChanged += ApplyActiveIconPack;
        }

        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinZenShell");
        Directory.CreateDirectory(dir);
        _recentsFilePath = Path.Combine(dir, "recent_apps.json");

        LoadRecentStats();

        // Load built-ins immediately so launcher is instantly usable
        var builtins = BuildBuiltinApps();
        lock (_lock)
        {
            foreach (var item in builtins)
            {
                if (_seenNames.Add(item.Name))
                {
                    _apps.Add(item);
                }
            }
        }

        StartDirectoryWatchers();

        // Scan shortcuts asynchronously
        Task.Run(ScanInstalledShortcuts);
    }

    public void ApplyActiveIconPack()
    {
        bool anyChanged = false;
        lock (_lock)
        {
            foreach (var item in _apps)
            {
                string exeStem = !string.IsNullOrWhiteSpace(item.ResolvedExePath)
                    ? Path.GetFileNameWithoutExtension(item.ResolvedExePath)
                    : item.Name;
                string exeOrTarget = !string.IsNullOrWhiteSpace(item.ResolvedExePath)
                    ? item.ResolvedExePath
                    : item.TargetPath;

                string packIcon = _iconPackService?.ResolveAppIconDataUrl(item.Name, exeStem, item.Name, exeOrTarget) ?? "";
                string nextIcon = !string.IsNullOrEmpty(packIcon) ? packIcon : item.NativeIconDataUrl;
                if (!string.Equals(item.IconDataUrl, nextIcon, StringComparison.Ordinal))
                {
                    item.IconDataUrl = nextIcon;
                    anyChanged = true;
                }
            }
        }

        if (anyChanged)
        {
            AppsLoaded?.Invoke();
        }
    }

    public void ForceRescan()
    {
        ApplyActiveIconPack();
        _lastScanUtc = DateTime.MinValue;
        _ = Task.Run(ScanInstalledShortcuts);
    }

    public void RefreshIfStale(TimeSpan? maxAge = null)
    {
        var threshold = maxAge ?? TimeSpan.FromSeconds(5);
        if (DateTime.UtcNow - _lastScanUtc < threshold)
            return;

        _ = Task.Run(ScanInstalledShortcuts);
    }

    private void StartDirectoryWatchers()
    {
        var dirs = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var filter in new[] { "*.lnk", "*.url" })
            {
                try
                {
                    var watcher = new FileSystemWatcher(dir, filter)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                        EnableRaisingEvents = true
                    };
                    watcher.Created += OnShortcutDirectoryChanged;
                    watcher.Deleted += OnShortcutDirectoryChanged;
                    watcher.Renamed += OnShortcutDirectoryChanged;
                    _watchers.Add(watcher);
                }
                catch { }
            }
        }

        string? steamPath = TryGetSteamRootPath();
        if (!string.IsNullOrWhiteSpace(steamPath))
        {
            foreach (var libPath in GetSteamLibraryFolders(steamPath))
            {
                string steamAppsDir = Path.Combine(libPath, "steamapps");
                if (!Directory.Exists(steamAppsDir)) continue;
                try
                {
                    var watcher = new FileSystemWatcher(steamAppsDir, "appmanifest_*.acf")
                    {
                        IncludeSubdirectories = false,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                        EnableRaisingEvents = true
                    };
                    watcher.Created += OnShortcutDirectoryChanged;
                    watcher.Deleted += OnShortcutDirectoryChanged;
                    watcher.Changed += OnShortcutDirectoryChanged;
                    watcher.Renamed += OnShortcutDirectoryChanged;
                    _watchers.Add(watcher);
                }
                catch { }
            }
        }
    }

    private void OnShortcutDirectoryChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new System.Threading.Timer(_ =>
            {
                _ = Task.Run(ScanInstalledShortcuts);
            }, null, 1200, System.Threading.Timeout.Infinite);
        }
        catch { }
    }

    private void LoadRecentStats()
    {
        try
        {
            if (File.Exists(_recentsFilePath))
            {
                string json = File.ReadAllText(_recentsFilePath);
                var stats = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                if (stats != null)
                {
                    _launchStats = new Dictionary<string, int>(stats, StringComparer.OrdinalIgnoreCase);
                }
            }
        }
        catch
        {
            _launchStats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveRecentStats()
    {
        try
        {
            string json = JsonSerializer.Serialize(_launchStats, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_recentsFilePath, json);
        }
        catch { }
    }

    private List<AppLauncherItem> BuildBuiltinApps()
    {
        var builtins = new[]
        {
            ("File Explorer", "explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\explorer.exe"),
            ("Microsoft Store", "ms-windows-store:", ""),
            ("Settings", "ms-settings:", Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\ImmersiveControlPanel\SystemSettings.exe"),
            ("Windows Terminal", "wt.exe", ""),
            ("Notepad", "notepad.exe", Environment.GetFolderPath(Environment.SpecialFolder.System) + @"\notepad.exe"),
            ("Calculator", "calc.exe", Environment.GetFolderPath(Environment.SpecialFolder.System) + @"\calc.exe"),
            ("Task Manager", "taskmgr.exe", Environment.GetFolderPath(Environment.SpecialFolder.System) + @"\Taskmgr.exe"),
            ("Snipping Tool", "snippingtool.exe", "")
        };

        var list = new List<AppLauncherItem>();
        foreach (var (name, target, iconSource) in builtins)
        {
            string cacheKey = $"builtin:{name}";
            if (!_iconCache.TryGetValue(cacheKey, out string? iconUrl) || string.IsNullOrEmpty(iconUrl))
            {
                iconUrl = "";
                if (!string.IsNullOrEmpty(iconSource) && File.Exists(iconSource))
                {
                    iconUrl = ExtractIconDataUrl(iconSource);
                }
                if (string.IsNullOrEmpty(iconUrl) && string.Equals(name, "Microsoft Store", StringComparison.OrdinalIgnoreCase))
                {
                    iconUrl = UwpAppIconHelper.ExtractIconDataUrlByAumid(UwpAppIconHelper.MicrosoftStoreAumid, 128);
                }
                if (string.IsNullOrEmpty(iconUrl))
                {
                    iconUrl = GetBuiltinFallbackIconDataUrl(name);
                }
                if (!string.IsNullOrEmpty(iconUrl))
                {
                    _iconCache[cacheKey] = iconUrl;
                }
            }

            string nativeIcon = iconUrl ?? "";
            string packIcon = _iconPackService?.ResolveAppIconDataUrl(name, name, name, !string.IsNullOrEmpty(iconSource) ? iconSource : target) ?? "";
            string finalIcon = !string.IsNullOrEmpty(packIcon) ? packIcon : nativeIcon;

            int count = _launchStats.TryGetValue(name, out int c) ? c : 0;
            list.Add(new AppLauncherItem
            {
                Id = name.ToLowerInvariant().Replace(" ", "-"),
                Name = name,
                TargetPath = target,
                ResolvedExePath = !string.IsNullOrEmpty(iconSource) ? iconSource : target,
                IconDataUrl = finalIcon,
                NativeIconDataUrl = nativeIcon,
                LaunchCount = count,
                IsRecent = count > 0
            });
        }
        return list;
    }

    private void ScanInstalledShortcuts()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _isScanning, 1, 0) != 0)
            return;

        try
        {
            var dirsToScan = new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            };

            var ignoredKeywords = new[]
            {
                "uninstall", "help", "readme", "documentation", "release notes",
                "license", "licence", "website", "manual", "eula", "feedback",
                "administrative tools", "application verifier", "windows app cert",
                "software development kit", "sample desktop apps", "sample uwp apps",
                "tools for desktop apps", "tools for uwp apps", "reset preferences",
                "install additional tools", "module docs", "reload configuration",
                "steps recorder", "telemetry log", "recording manager",
                "language preferences", "debuggable package manager",
                "native tools command prompt", "cross tools command prompt",
                "developer command prompt", "developer powershell",
                "recycle bin", "dfrgui", "recoverydrive"
            };

            var ignoredExactNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "services", "component services", "computer management", "disk cleanup",
                "event viewer", "iscsi initiator", "memory diagnostics tool",
                "performance monitor", "registry editor", "resource monitor",
                "system configuration", "system information", "task scheduler",
                "windows defender firewall with advanced security",
                "character map", "vlc media player skinned", "gpuview",
                "windows performance analyzer", "windows performance recorder",
                "livecaptions", "magnify", "narrator", "on-screen keyboard", "voiceaccess",
                "run", "command prompt"
            };

            var ignoredFolderSegments = new[]
            {
                "\\administrative tools\\",
                "\\windows kits\\",
                "\\visual studio tools\\",
                "\\accessibility\\",
                "\\startup\\",
                "\\system tools\\",
                "\\microsoft office tools\\"
            };

            var localSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rebuilt = BuildBuiltinApps();
            foreach (var b in rebuilt)
            {
                localSeen.Add(b.Name);
            }

            foreach (var dir in dirsToScan)
            {
                if (!Directory.Exists(dir)) continue;

                try
                {
                    var files = Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        try
                        {
                            string lowerFullPath = file.ToLowerInvariant();
                            if (ignoredFolderSegments.Any(seg => lowerFullPath.Contains(seg))) continue;

                            string fileName = Path.GetFileNameWithoutExtension(file);
                            if (string.IsNullOrWhiteSpace(fileName)) continue;

                            string lowerName = fileName.ToLowerInvariant();
                            if (ignoredExactNames.Contains(fileName)) continue;
                            if (ignoredKeywords.Any(k => lowerName.Contains(k))) continue;
                            if (lowerName.Contains("(x86)") || lowerName.Contains("(32-bit)")) continue;

                            if (!localSeen.Add(fileName)) continue;

                            if (!_iconCache.TryGetValue(file, out string? iconUrl) || string.IsNullOrEmpty(iconUrl))
                            {
                                iconUrl = ExtractIconDataUrl(file);
                                if (string.IsNullOrEmpty(iconUrl))
                                {
                                    iconUrl = GetBuiltinFallbackIconDataUrl(fileName);
                                }
                                if (!string.IsNullOrEmpty(iconUrl))
                                {
                                    _iconCache[file] = iconUrl;
                                }
                            }

                            if (!_shortcutExeCache.TryGetValue(file, out string? resolvedExe))
                            {
                                resolvedExe = TryResolveShortcutTargetExe(file) ?? "";
                                _shortcutExeCache[file] = resolvedExe;
                            }

                            string exeStem = !string.IsNullOrWhiteSpace(resolvedExe)
                                ? Path.GetFileNameWithoutExtension(resolvedExe)
                                : fileName;
                            string exeOrTarget = !string.IsNullOrWhiteSpace(resolvedExe) ? resolvedExe : file;

                            string nativeIcon = iconUrl ?? "";
                            string packIcon = _iconPackService?.ResolveAppIconDataUrl(fileName, exeStem, fileName, exeOrTarget) ?? "";
                            string finalIcon = !string.IsNullOrEmpty(packIcon) ? packIcon : nativeIcon;

                            int count = _launchStats.TryGetValue(fileName, out int c) ? c : 0;

                            rebuilt.Add(new AppLauncherItem
                            {
                                Id = Guid.NewGuid().ToString("N")[..8],
                                Name = fileName,
                                TargetPath = file,
                                ResolvedExePath = resolvedExe ?? "",
                                IconDataUrl = finalIcon,
                                NativeIconDataUrl = nativeIcon,
                                LaunchCount = count,
                                IsRecent = count > 0
                            });
                        }
                        catch { }
                    }
                }
                catch { }
            }

            // Scan installed Steam games from libraryfolders.vdf + appmanifest_*.acf and game .url shortcuts
            ScanSteamAndUrlGameShortcuts(dirsToScan, rebuilt, localSeen, ignoredExactNames, ignoredKeywords, ignoredFolderSegments);

            // Also scan Microsoft Store / UWP packaged apps from shell:AppsFolder
            var ignoredUwpAumidPrefixes = new[]
            {
                "Microsoft.Windows.ShellExperienceHost",
                "Microsoft.Windows.StartMenuExperienceHost",
                "Microsoft.Windows.Search",
                "Microsoft.Windows.Apprep.ChxApp",
                "Microsoft.Windows.AssignedAccessLockApp",
                "Microsoft.Windows.CapturePicker",
                "Microsoft.Windows.CloudExperienceHost",
                "Microsoft.Windows.ContentDeliveryManager",
                "Microsoft.Windows.NarratorQuickStart",
                "Microsoft.Windows.OOBENetwork",
                "Microsoft.Windows.ParentalControls",
                "Microsoft.Windows.PeopleExperienceHost",
                "Microsoft.Windows.PinningConfirmationDialog",
                "Microsoft.Windows.PrintQueueActionCenter",
                "Microsoft.Windows.SecHealthUI",
                "Microsoft.Windows.XGpuEjectDialog",
                "MicrosoftWindows.Client.",
                "MicrosoftWindows.UndockedDevKit",
                "Windows.CBSPreview",
                "Windows.PrintDialog",
                "windows.immersivecontrolpanel",
                "Microsoft.AAD.BrokerPlugin",
                "Microsoft.AccountsControl",
                "Microsoft.AsyncTextService",
                "Microsoft.BioEnrollment",
                "Microsoft.CredDialogHost",
                "Microsoft.ECApp",
                "Microsoft.LockApp",
                "Microsoft.MicrosoftEdgeDevToolsClient",
                "Microsoft.Win32WebViewHost",
                "Microsoft.XboxGameCallableUI"
            };

            foreach (var (uwpName, aumid) in UwpAppIconHelper.EnumerateAppsFolderUwpApps())
            {
                try
                {
                    if (ignoredUwpAumidPrefixes.Any(p => aumid.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    string lowerUwpName = uwpName.ToLowerInvariant();
                    if (ignoredExactNames.Contains(uwpName)) continue;
                    if (ignoredKeywords.Any(k => lowerUwpName.Contains(k))) continue;
                    if (!localSeen.Add(uwpName)) continue;

                    string target = $"shell:AppsFolder\\{aumid}";
                    if (!_iconCache.TryGetValue(target, out string? iconUrl) || string.IsNullOrEmpty(iconUrl))
                    {
                        iconUrl = UwpAppIconHelper.ExtractIconDataUrlByAumid(aumid, 128);
                        if (!string.IsNullOrEmpty(iconUrl))
                        {
                            _iconCache[target] = iconUrl;
                        }
                    }

                    string nativeIcon = iconUrl ?? "";
                    string packIcon = _iconPackService?.ResolveAppIconDataUrl(uwpName, uwpName, uwpName, target) ?? "";
                    string finalIcon = !string.IsNullOrEmpty(packIcon) ? packIcon : nativeIcon;

                    int count = _launchStats.TryGetValue(uwpName, out int c) ? c : 0;

                    rebuilt.Add(new AppLauncherItem
                    {
                        Id = Guid.NewGuid().ToString("N")[..8],
                        Name = uwpName,
                        TargetPath = target,
                        ResolvedExePath = target,
                        IconDataUrl = finalIcon,
                        NativeIconDataUrl = nativeIcon,
                        LaunchCount = count,
                        IsRecent = count > 0
                    });
                }
                catch { }
            }

            rebuilt.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            // Re-evaluate against active icon pack right before committing in case the active pack changed during the scan
            foreach (var item in rebuilt)
            {
                string exeStem = !string.IsNullOrWhiteSpace(item.ResolvedExePath)
                    ? Path.GetFileNameWithoutExtension(item.ResolvedExePath)
                    : item.Name;
                string exeOrTarget = !string.IsNullOrWhiteSpace(item.ResolvedExePath)
                    ? item.ResolvedExePath
                    : item.TargetPath;
                string packIcon = _iconPackService?.ResolveAppIconDataUrl(item.Name, exeStem, item.Name, exeOrTarget) ?? "";
                item.IconDataUrl = !string.IsNullOrEmpty(packIcon) ? packIcon : item.NativeIconDataUrl;
            }

            bool changed;
            lock (_lock)
            {
                changed = !IsLoaded || _apps.Count != rebuilt.Count;
                if (!changed)
                {
                    for (int i = 0; i < rebuilt.Count; i++)
                    {
                        if (!string.Equals(_apps[i].Name, rebuilt[i].Name, StringComparison.OrdinalIgnoreCase) ||
                            !string.Equals(_apps[i].TargetPath, rebuilt[i].TargetPath, StringComparison.OrdinalIgnoreCase) ||
                            !string.Equals(_apps[i].IconDataUrl, rebuilt[i].IconDataUrl, StringComparison.Ordinal))
                        {
                            changed = true;
                            break;
                        }
                    }
                }

                if (changed)
                {
                    _apps.Clear();
                    _apps.AddRange(rebuilt);
                    _seenNames.Clear();
                    foreach (var item in rebuilt)
                    {
                        _seenNames.Add(item.Name);
                    }
                }

                IsLoaded = true;
                _lastScanUtc = DateTime.UtcNow;
            }

            if (changed)
            {
                AppsLoaded?.Invoke();
            }
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _isScanning, 0);
        }
    }

    private static string GetBuiltinFallbackIconDataUrl(string appName)
    {
        if (string.Equals(appName, "Microsoft Store", StringComparison.OrdinalIgnoreCase))
        {
            const string svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 48 48'><path d='M16 14v-2a8 8 0 0 1 16 0v2h6a2 2 0 0 1 2 2l2 24a4 4 0 0 1-4 4H10a4 4 0 0 1-4-4l2-24a2 2 0 0 1 2-2h6zm3 0h10v-2a5 5 0 0 0-10 0v2z' fill='#2b88d8'/><rect x='17' y='21' width='6' height='6' rx='1' fill='#f25022'/><rect x='25' y='21' width='6' height='6' rx='1' fill='#7fba00'/><rect x='17' y='29' width='6' height='6' rx='1' fill='#00a4ef'/><rect x='25' y='29' width='6' height='6' rx='1' fill='#ffb900'/></svg>";
            return "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
        }

        if (string.Equals(appName, "Windows Terminal", StringComparison.OrdinalIgnoreCase))
        {
            const string svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 48 48'><rect x='4' y='8' width='40' height='32' rx='6' fill='#2b2b2b' stroke='#484848' stroke-width='1.5'/><rect x='4' y='8' width='40' height='8' rx='4' fill='#3a3a3a'/><path d='M13 22l6 5-6 5' fill='none' stroke='#ffffff' stroke-width='2.6' stroke-linecap='round' stroke-linejoin='round'/><line x1='22' y1='32' x2='32' y2='32' stroke='#ffffff' stroke-width='2.6' stroke-linecap='round'/></svg>";
            return "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
        }

        if (string.Equals(appName, "Snipping Tool", StringComparison.OrdinalIgnoreCase))
        {
            const string svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 48 48'><rect x='8' y='10' width='32' height='26' rx='4' fill='none' stroke='#60a5fa' stroke-width='2.4' stroke-dasharray='5 3'/><circle cx='16' cy='34' r='4.5' fill='none' stroke='#e5e7eb' stroke-width='2.4'/><circle cx='32' cy='34' r='4.5' fill='none' stroke='#e5e7eb' stroke-width='2.4'/><path d='M19.5 30.5L31 15M28.5 30.5L17 15' stroke='#e5e7eb' stroke-width='2.4' stroke-linecap='round'/></svg>";
            return "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
        }

        if (appName.StartsWith("Git", StringComparison.OrdinalIgnoreCase))
        {
            const string svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 48 48'><path d='M44.4 21.8L26.2 3.6a3.1 3.1 0 0 0-4.4 0l-3.8 3.8 4.8 4.8a3.7 3.7 0 0 1 4.7 4.7l4.6 4.6a3.7 3.7 0 1 1-2.2 2.1l-4.3-4.3v11.3a3.7 3.7 0 1 1-3.1-.1V19a3.7 3.7 0 0 1-2-4.9L15.8 9.4 3.6 21.6a3.1 3.1 0 0 0 0 4.4l18.2 18.2a3.1 3.1 0 0 0 4.4 0l18.2-18.2a3.1 3.1 0 0 0 0-4.2z' fill='#F05032'/></svg>";
            return "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
        }

        return "";
    }

    private static string ExtractIconDataUrl(string path)
    {
        try
        {
            string iconSourcePath = path;
            if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var (resolvedPath, iconIndex) = TryResolveShortcutIconSource(path);
                if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
                {
                    string? highResDataUrl = TryExtractIndexedIconDataUrl(resolvedPath, iconIndex);
                    if (!string.IsNullOrEmpty(highResDataUrl))
                    {
                        return highResDataUrl;
                    }
                    iconSourcePath = resolvedPath;
                }
                else
                {
                    // Use SHGFI_SYSICONINDEX + ImageList_GetIcon so Windows does NOT composite the shortcut arrow overlay
                    var shfi = new SHFILEINFO();
                    IntPtr himl = SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_SYSICONINDEX | SHGFI_LARGEICON);
                    if (himl != IntPtr.Zero && shfi.iIcon >= 0)
                    {
                        IntPtr hIcon = ImageList_GetIcon(himl, shfi.iIcon, ILD_TRANSPARENT);
                        if (hIcon != IntPtr.Zero)
                        {
                            try
                            {
                                using var shellIcon = Icon.FromHandle(hIcon);
                                using var bmp = shellIcon.ToBitmap();
                                using var ms = new MemoryStream();
                                bmp.Save(ms, ImageFormat.Png);
                                return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
                            }
                            finally
                            {
                                DestroyIcon(hIcon);
                            }
                        }
                    }
                }
            }

            string? directHighRes = TryExtractIndexedIconDataUrl(iconSourcePath, 0);
            if (!string.IsNullOrEmpty(directHighRes))
            {
                return directHighRes;
            }

            using var icon = Icon.ExtractAssociatedIcon(iconSourcePath);
            if (icon == null) return "";

            using var fallbackBmp = icon.ToBitmap();
            using var fallbackMs = new MemoryStream();
            fallbackBmp.Save(fallbackMs, ImageFormat.Png);
            byte[] bytes = fallbackMs.ToArray();
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }
        catch
        {
            return "";
        }
    }

    private void ScanSteamAndUrlGameShortcuts(
        List<string> dirsToScan,
        List<AppLauncherItem> rebuilt,
        HashSet<string> localSeen,
        HashSet<string> ignoredExactNames,
        string[] ignoredKeywords,
        string[] ignoredFolderSegments)
    {
        var seenSteamAppIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Scan installed Steam games via Steam library manifests (appmanifest_*.acf) + Uninstall registry
        try
        {
            string? steamRoot = TryGetSteamRootPath();
            if (!string.IsNullOrWhiteSpace(steamRoot))
            {
                var registryIcons = GetSteamUninstallRegistryIcons();
                var urlShortcutIcons = GetSteamUrlShortcutIcons(dirsToScan);
                var nonGameAppIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "228980",  // Steamworks Common Redistributables
                    "250820",  // SteamVR
                    "1070560", // Steam Linux Runtime
                    "1391110", // Steam Linux Runtime - Soldier
                    "1628350"  // Steam Linux Runtime - Sniper
                };

                foreach (var libFolder in GetSteamLibraryFolders(steamRoot))
                {
                    string steamAppsDir = Path.Combine(libFolder, "steamapps");
                    if (!Directory.Exists(steamAppsDir)) continue;

                    foreach (var acfFile in Directory.EnumerateFiles(steamAppsDir, "appmanifest_*.acf", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            var (appId, gameName, installDir) = ParseSteamAppManifest(acfFile);
                            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(gameName))
                                continue;

                            if (nonGameAppIds.Contains(appId))
                                continue;

                            string lowerGameName = gameName.ToLowerInvariant();
                            if (lowerGameName.Contains("steamworks") ||
                                lowerGameName.StartsWith("proton ") ||
                                lowerGameName.StartsWith("steam linux runtime") ||
                                lowerGameName.EndsWith(" dedicated server"))
                            {
                                continue;
                            }

                            if (ignoredExactNames.Contains(gameName)) continue;
                            if (ignoredKeywords.Any(k => lowerGameName.Contains(k))) continue;

                            seenSteamAppIds.Add(appId);
                            if (!localSeen.Add(gameName)) continue;

                            string targetUrl = $"steam://rungameid/{appId}";
                            string? fullInstallDir = !string.IsNullOrWhiteSpace(installDir)
                                ? Path.Combine(steamAppsDir, "common", installDir)
                                : null;
                            string? gameExePath = FindPrimaryExecutableInDir(fullInstallDir);

                            if (!_iconCache.TryGetValue(targetUrl, out string? iconUrl) || string.IsNullOrEmpty(iconUrl))
                            {
                                iconUrl = ResolveSteamGameIconDataUrl(
                                    steamRoot,
                                    appId,
                                    registryIcons.TryGetValue(appId, out var regIcon) ? regIcon : null,
                                    urlShortcutIcons.TryGetValue(appId, out var urlIcon) ? urlIcon : null,
                                    gameExePath);

                                if (!string.IsNullOrEmpty(iconUrl))
                                {
                                    _iconCache[targetUrl] = iconUrl;
                                }
                            }

                            string exeStem = !string.IsNullOrWhiteSpace(gameExePath)
                                ? Path.GetFileNameWithoutExtension(gameExePath)
                                : gameName;
                            string exeOrTarget = !string.IsNullOrWhiteSpace(gameExePath) ? gameExePath : targetUrl;

                            string nativeIcon = iconUrl ?? "";
                            string packIcon = _iconPackService?.ResolveAppIconDataUrl(gameName, exeStem, gameName, exeOrTarget) ?? "";
                            string finalIcon = !string.IsNullOrEmpty(packIcon) ? packIcon : nativeIcon;

                            int count = _launchStats.TryGetValue(gameName, out int c) ? c : 0;

                            rebuilt.Add(new AppLauncherItem
                            {
                                Id = $"steam-{appId}",
                                Name = gameName,
                                TargetPath = targetUrl,
                                ResolvedExePath = gameExePath ?? targetUrl,
                                IconDataUrl = finalIcon,
                                NativeIconDataUrl = nativeIcon,
                                LaunchCount = count,
                                IsRecent = count > 0
                            });
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }

        // 2. Scan *.url game launcher shortcuts (Steam, Epic Games, GOG, Riot, Xbox, etc.) in Start Menu / Desktop
        foreach (var dir in dirsToScan)
        {
            if (!Directory.Exists(dir)) continue;

            try
            {
                foreach (var urlFile in Directory.EnumerateFiles(dir, "*.url", SearchOption.AllDirectories))
                {
                    try
                    {
                        string lowerFullPath = urlFile.ToLowerInvariant();
                        if (ignoredFolderSegments.Any(seg => lowerFullPath.Contains(seg))) continue;

                        string fileName = Path.GetFileNameWithoutExtension(urlFile);
                        if (string.IsNullOrWhiteSpace(fileName)) continue;

                        string lowerName = fileName.ToLowerInvariant();
                        if (ignoredExactNames.Contains(fileName)) continue;
                        if (ignoredKeywords.Any(k => lowerName.Contains(k))) continue;

                        var (urlTarget, iconFile, iconIndex) = ParseInternetShortcutFile(urlFile);
                        if (string.IsNullOrWhiteSpace(urlTarget)) continue;

                        // Skip generic HTTP/HTTPS web links (e.g. documentation, FAQ, support pages)
                        if (urlTarget.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            urlTarget.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // If it's a Steam rungameid shortcut, only include it if it's currently installed (or if no Steam library was found)
                        if (urlTarget.StartsWith("steam://rungameid/", StringComparison.OrdinalIgnoreCase))
                        {
                            string steamAppId = urlTarget["steam://rungameid/".Length..].Trim();
                            if (seenSteamAppIds.Contains(steamAppId))
                                continue;

                            // Skip uninstalled leftover Steam .url shortcuts if we know the installed Steam library
                            if (seenSteamAppIds.Count > 0 && !IsSteamAppInstalledInRegistry(steamAppId))
                                continue;
                        }

                        if (!localSeen.Add(fileName)) continue;

                        if (!_iconCache.TryGetValue(urlFile, out string? iconUrl) || string.IsNullOrEmpty(iconUrl))
                        {
                            iconUrl = ExtractUrlShortcutIconDataUrl(iconFile, iconIndex);
                            if (!string.IsNullOrEmpty(iconUrl))
                            {
                                _iconCache[urlFile] = iconUrl;
                            }
                        }

                        string nativeIcon = iconUrl ?? "";
                        string packIcon = _iconPackService?.ResolveAppIconDataUrl(fileName, fileName, fileName, urlTarget) ?? "";
                        string finalIcon = !string.IsNullOrEmpty(packIcon) ? packIcon : nativeIcon;

                        int count = _launchStats.TryGetValue(fileName, out int c) ? c : 0;

                        rebuilt.Add(new AppLauncherItem
                        {
                            Id = Guid.NewGuid().ToString("N")[..8],
                            Name = fileName,
                            TargetPath = urlFile,
                            ResolvedExePath = !string.IsNullOrWhiteSpace(iconFile) ? iconFile : urlTarget,
                            IconDataUrl = finalIcon,
                            NativeIconDataUrl = nativeIcon,
                            LaunchCount = count,
                            IsRecent = count > 0
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }
    }

    private static string? TryGetSteamRootPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            string? path = key?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(path))
            {
                string normalized = path.Replace('/', '\\');
                if (Directory.Exists(normalized))
                    return normalized;
            }
        }
        catch { }

        string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        return Directory.Exists(defaultPath) ? defaultPath : null;
    }

    private static List<string> GetSteamLibraryFolders(string steamRoot)
    {
        var folders = new List<string> { steamRoot };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(steamRoot) };

        try
        {
            string vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdfPath))
            {
                string content = File.ReadAllText(vdfPath);
                foreach (Match match in Regex.Matches(content, "\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase))
                {
                    string rawPath = match.Groups[1].Value.Replace("\\\\", "\\").Trim();
                    if (!string.IsNullOrWhiteSpace(rawPath) && Directory.Exists(rawPath))
                    {
                        string full = Path.GetFullPath(rawPath);
                        if (seen.Add(full))
                        {
                            folders.Add(full);
                        }
                    }
                }
            }
        }
        catch { }

        return folders;
    }

    private static (string AppId, string Name, string InstallDir) ParseSteamAppManifest(string acfFilePath)
    {
        string appId = "";
        string name = "";
        string installDir = "";

        foreach (var rawLine in File.ReadLines(acfFilePath))
        {
            string line = rawLine.Trim();
            if (!line.StartsWith('"')) continue;

            var match = Regex.Match(line, "^\"([^\"]+)\"\\s+\"([^\"]*)\"");
            if (!match.Success) continue;

            string k = match.Groups[1].Value;
            string v = match.Groups[2].Value;

            if (string.Equals(k, "appid", StringComparison.OrdinalIgnoreCase))
                appId = v.Trim();
            else if (string.Equals(k, "name", StringComparison.OrdinalIgnoreCase))
                name = v.Trim();
            else if (string.Equals(k, "installdir", StringComparison.OrdinalIgnoreCase))
                installDir = v.Trim();

            if (!string.IsNullOrEmpty(appId) && !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(installDir))
                break;
        }

        return (appId, name, installDir);
    }

    private static Dictionary<string, string> GetSteamUninstallRegistryIcons()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] uninstallPaths =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var regPath in uninstallPaths)
        {
            try
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(regPath);
                if (baseKey == null) continue;

                foreach (var subName in baseKey.GetSubKeyNames())
                {
                    if (!subName.StartsWith("Steam App ", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string appId = subName["Steam App ".Length..].Trim();
                    if (string.IsNullOrWhiteSpace(appId)) continue;

                    using var appKey = baseKey.OpenSubKey(subName);
                    string? displayIcon = (appKey?.GetValue("DisplayIcon") as string)?.Trim().Trim('"');
                    if (!string.IsNullOrWhiteSpace(displayIcon) && File.Exists(displayIcon))
                    {
                        map[appId] = displayIcon;
                    }
                }
            }
            catch { }
        }

        return map;
    }

    private static bool IsSteamAppInstalledInRegistry(string appId)
    {
        string[] uninstallPaths =
        {
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {appId}",
            $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {appId}"
        };

        foreach (var path in uninstallPaths)
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(path);
                if (k != null) return true;
            }
            catch { }
        }
        return false;
    }

    private static Dictionary<string, string> GetSteamUrlShortcutIcons(List<string> dirsToScan)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in dirsToScan)
        {
            if (!Directory.Exists(dir)) continue;
            try
            {
                foreach (var urlFile in Directory.EnumerateFiles(dir, "*.url", SearchOption.AllDirectories))
                {
                    try
                    {
                        var (urlTarget, iconFile, _) = ParseInternetShortcutFile(urlFile);
                        if (urlTarget != null &&
                            urlTarget.StartsWith("steam://rungameid/", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(iconFile) &&
                            File.Exists(iconFile))
                        {
                            string appId = urlTarget["steam://rungameid/".Length..].Trim();
                            if (!string.IsNullOrWhiteSpace(appId))
                            {
                                map[appId] = iconFile;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
        return map;
    }

    private static (string? Url, string? IconFile, int IconIndex) ParseInternetShortcutFile(string urlFilePath)
    {
        string? url = null;
        string? iconFile = null;
        int iconIndex = 0;

        foreach (var rawLine in File.ReadLines(urlFilePath))
        {
            string line = rawLine.Trim();
            if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
            {
                url = line[4..].Trim();
            }
            else if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
            {
                iconFile = Environment.ExpandEnvironmentVariables(line["IconFile=".Length..].Trim().Trim('"'));
            }
            else if (line.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(line["IconIndex=".Length..].Trim(), out iconIndex);
            }
        }

        return (url, iconFile, iconIndex);
    }

    private static string ResolveSteamGameIconDataUrl(
        string steamRoot,
        string appId,
        string? registryIconPath,
        string? urlShortcutIconPath,
        string? gameExePath)
    {
        // 1. DisplayIcon from Uninstall\Steam App <AppId>
        if (!string.IsNullOrWhiteSpace(registryIconPath) && File.Exists(registryIconPath))
        {
            string dataUrl = ExtractUrlShortcutIconDataUrl(registryIconPath, 0);
            if (!string.IsNullOrEmpty(dataUrl)) return dataUrl;
        }

        // 2. IconFile from existing Steam .url shortcut
        if (!string.IsNullOrWhiteSpace(urlShortcutIconPath) && File.Exists(urlShortcutIconPath))
        {
            string dataUrl = ExtractUrlShortcutIconDataUrl(urlShortcutIconPath, 0);
            if (!string.IsNullOrEmpty(dataUrl)) return dataUrl;
        }

        // 3. Steam librarycache/<AppId>/<40-hex-hash>.jpg matched against steam/games/<40-hex-hash>.ico
        try
        {
            string appCacheDir = Path.Combine(steamRoot, "appcache", "librarycache", appId);
            string steamGamesDir = Path.Combine(steamRoot, "steam", "games");
            if (Directory.Exists(appCacheDir))
            {
                foreach (var file in Directory.EnumerateFiles(appCacheDir, "*.*", SearchOption.TopDirectoryOnly))
                {
                    string stem = Path.GetFileNameWithoutExtension(file);
                    if (stem.Length == 40)
                    {
                        string icoCandidate = Path.Combine(steamGamesDir, stem + ".ico");
                        if (File.Exists(icoCandidate))
                        {
                            string dataUrl = ExtractUrlShortcutIconDataUrl(icoCandidate, 0);
                            if (!string.IsNullOrEmpty(dataUrl)) return dataUrl;
                        }

                        string dataUrlFromJpg = TryImageFileToDataUrl(file);
                        if (!string.IsNullOrEmpty(dataUrlFromJpg)) return dataUrlFromJpg;
                    }
                }
            }
        }
        catch { }

        // 4. Primary executable in common/<installdir>
        if (!string.IsNullOrWhiteSpace(gameExePath) && File.Exists(gameExePath))
        {
            string dataUrl = ExtractIconDataUrl(gameExePath);
            if (!string.IsNullOrEmpty(dataUrl)) return dataUrl;
        }

        // 5. Fallback to Steam client icon
        string steamExe = Path.Combine(steamRoot, "steam.exe");
        if (File.Exists(steamExe))
        {
            return ExtractIconDataUrl(steamExe);
        }

        return "";
    }

    private static string ExtractUrlShortcutIconDataUrl(string? iconFile, int iconIndex)
    {
        if (string.IsNullOrWhiteSpace(iconFile) || !File.Exists(iconFile))
            return "";

        try
        {
            string ext = Path.GetExtension(iconFile);
            if (ext.Equals(".ico", StringComparison.OrdinalIgnoreCase))
            {
                using var ico = new Icon(iconFile, 48, 48);
                using var bmp = ico.ToBitmap();
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
            }

            if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return TryImageFileToDataUrl(iconFile);
            }

            if (iconIndex != 0)
            {
                string? indexed = TryExtractIndexedIconDataUrl(iconFile, iconIndex);
                if (!string.IsNullOrEmpty(indexed)) return indexed;
            }

            return ExtractIconDataUrl(iconFile);
        }
        catch
        {
            return ExtractIconDataUrl(iconFile);
        }
    }

    private static string TryImageFileToDataUrl(string imagePath)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(imagePath);
            string ext = Path.GetExtension(imagePath).ToLowerInvariant();
            string mime = ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".svg" => "image/svg+xml",
                _ => "image/png"
            };
            return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        }
        catch
        {
            return "";
        }
    }

    private static string? FindPrimaryExecutableInDir(string? installDir)
    {
        if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir))
            return null;

        try
        {
            var ignoredExeSubstrings = new[]
            {
                "unins", "crash", "redist", "setup", "vcredist", "dxsetup",
                "unitycrashhandler", "easyanticheat", "battleye", "cefprocess",
                "zfgamebrowser", "elevationservice"
            };

            var candidates = Directory.EnumerateFiles(installDir, "*.exe", SearchOption.TopDirectoryOnly)
                .Where(f =>
                {
                    string lower = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    return !ignoredExeSubstrings.Any(ig => lower.Contains(ig));
                })
                .OrderByDescending(f =>
                {
                    try { return new FileInfo(f).Length; } catch { return 0L; }
                })
                .ToList();

            if (candidates.Count > 0)
                return candidates[0];
        }
        catch { }

        return null;
    }

    private static string? TryExtractIndexedIconDataUrl(string filePath, int iconIndex)
    {
        IntPtr[] hiResIcons = new IntPtr[1];
        uint[] iconIds = new uint[1];
        uint hiResExtracted = PrivateExtractIcons(filePath, iconIndex, 128, 128, hiResIcons, iconIds, 1, 0);
        if (hiResExtracted > 0 && hiResExtracted != uint.MaxValue && hiResIcons[0] != IntPtr.Zero)
        {
            try
            {
                using var icon = Icon.FromHandle(hiResIcons[0]);
                using var bmp = icon.ToBitmap();
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
            }
            finally
            {
                DestroyIcon(hiResIcons[0]);
            }
        }

        IntPtr[] largeIcons = new IntPtr[1];
        uint extracted = ExtractIconEx(filePath, iconIndex, largeIcons, null, 1);
        if (extracted > 0 && largeIcons[0] != IntPtr.Zero)
        {
            try
            {
                using var icon = Icon.FromHandle(largeIcons[0]);
                using var bmp = icon.ToBitmap();
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
            }
            finally
            {
                DestroyIcon(largeIcons[0]);
            }
        }
        return null;
    }

    private const uint SHGFI_SYSICONINDEX = 0x000004000;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint ILD_TRANSPARENT = 0x00000001;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, uint flags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint PrivateExtractIcons(
        string szFileName,
        int nIconIndex,
        int cxIcon,
        int cyIcon,
        IntPtr[] phicon,
        uint[] piconid,
        uint nIcons,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static string? TryResolveShortcutTargetExe(string lnkPath)
    {
        try
        {
            var link = (IShellLinkW)new ShellLink();
            ((IPersistFile)link).Load(lnkPath, 0);
            var targetSb = new StringBuilder(260);
            link.GetPath(targetSb, targetSb.Capacity, IntPtr.Zero, 0);
            string targetPath = Environment.ExpandEnvironmentVariables(targetSb.ToString().Trim().Trim('"'));
            if (!string.IsNullOrWhiteSpace(targetPath))
            {
                return targetPath;
            }
        }
        catch { }
        return null;
    }

    private static (string? Path, int IconIndex) TryResolveShortcutIconSource(string lnkPath)
    {
        try
        {
            var link = (IShellLinkW)new ShellLink();
            ((IPersistFile)link).Load(lnkPath, 0);

            var iconSb = new StringBuilder(260);
            link.GetIconLocation(iconSb, iconSb.Capacity, out int iconIdx);
            string iconLoc = Environment.ExpandEnvironmentVariables(iconSb.ToString().Trim().Trim('"'));
            if (!string.IsNullOrWhiteSpace(iconLoc) && File.Exists(iconLoc))
            {
                return (iconLoc, iconIdx);
            }

            var targetSb = new StringBuilder(260);
            link.GetPath(targetSb, targetSb.Capacity, IntPtr.Zero, 0);
            string targetPath = Environment.ExpandEnvironmentVariables(targetSb.ToString().Trim().Trim('"'));
            if (!string.IsNullOrWhiteSpace(targetPath))
            {
                // Git for Windows.lnk targets mingw64\bin\wintoast.exe which has no embedded icon;
                // redirect icon extraction to git-for-windows.ico or git-bash.exe in the Git root
                if (targetPath.EndsWith("wintoast.exe", StringComparison.OrdinalIgnoreCase))
                {
                    string gitIco = @"C:\Program Files\Git\mingw64\share\git\git-for-windows.ico";
                    if (File.Exists(gitIco))
                        return (gitIco, 0);

                    string gitBash = @"C:\Program Files\Git\git-bash.exe";
                    if (File.Exists(gitBash))
                        return (gitBash, 0);
                }

                if (File.Exists(targetPath))
                {
                    return (targetPath, 0);
                }
            }
        }
        catch { }
        return (null, 0);
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    public object GetAppsPayload(AppLauncherConfig? config = null)
    {
        lock (_lock)
        {
            var hiddenSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (config?.HiddenApps != null)
            {
                foreach (var h in config.HiddenApps)
                {
                    if (!string.IsNullOrWhiteSpace(h))
                        hiddenSet.Add(h);
                }
            }

            bool showSteam = config?.ShowSteamGames ?? true;
            bool showPinned = config?.ShowPinnedApps ?? true;
            bool showRecent = config?.ShowRecentApps ?? true;

            // Filter all apps
            var filteredAll = _apps.Where(a =>
            {
                if (hiddenSet.Contains(a.TargetPath) || hiddenSet.Contains(a.Name))
                    return false;
                if (!showSteam && a.TargetPath.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
                    return false;
                return true;
            }).ToList();

            // Pinned apps
            var pinned = new List<AppLauncherItem>();
            if (showPinned && config?.PinnedApps != null)
            {
                foreach (var p in config.PinnedApps)
                {
                    if (p.IsFolder)
                    {
                        var folderItem = new AppLauncherItem
                        {
                            Id = string.IsNullOrWhiteSpace(p.Id) ? $"folder_{Guid.NewGuid():N}" : p.Id,
                            Name = string.IsNullOrWhiteSpace(p.Name) ? "Folder" : p.Name,
                            IsFolder = true,
                            FolderSize = string.IsNullOrWhiteSpace(p.FolderSize) ? "1x1" : p.FolderSize,
                            GroupName = p.GroupName ?? "",
                            FolderItems = new List<AppLauncherItem>()
                        };

                        if (p.FolderItems != null)
                        {
                            foreach (var sub in p.FolderItems)
                            {
                                if (string.IsNullOrWhiteSpace(sub.TargetPath)) continue;
                                if (hiddenSet.Contains(sub.TargetPath) || hiddenSet.Contains(sub.Name)) continue;
                                if (!showSteam && sub.TargetPath.StartsWith("steam://", StringComparison.OrdinalIgnoreCase)) continue;

                                var subMatch = _apps.FirstOrDefault(a =>
                                    string.Equals(a.TargetPath, sub.TargetPath, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(a.Name, sub.Name, StringComparison.OrdinalIgnoreCase));

                                if (subMatch != null)
                                {
                                    folderItem.FolderItems.Add(subMatch);
                                }
                                else
                                {
                                    folderItem.FolderItems.Add(new AppLauncherItem
                                    {
                                        Id = sub.TargetPath,
                                        Name = sub.Name,
                                        TargetPath = sub.TargetPath,
                                        IconDataUrl = !string.IsNullOrEmpty(sub.IconDataUrl) ? sub.IconDataUrl : "",
                                        NativeIconDataUrl = !string.IsNullOrEmpty(sub.IconDataUrl) ? sub.IconDataUrl : ""
                                    });
                                }
                            }
                        }

                        pinned.Add(folderItem);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(p.TargetPath)) continue;
                    if (hiddenSet.Contains(p.TargetPath) || hiddenSet.Contains(p.Name))
                        continue;
                    if (!showSteam && p.TargetPath.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var matched = _apps.FirstOrDefault(a =>
                        string.Equals(a.TargetPath, p.TargetPath, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a.Name, p.Name, StringComparison.OrdinalIgnoreCase));

                    string tileSize = string.IsNullOrWhiteSpace(p.TileSize) ? "1x1" : p.TileSize;
                    if (matched != null)
                    {
                        pinned.Add(new AppLauncherItem
                        {
                            Id = matched.Id,
                            Name = matched.Name,
                            TargetPath = matched.TargetPath,
                            ResolvedExePath = matched.ResolvedExePath,
                            IconDataUrl = matched.IconDataUrl,
                            NativeIconDataUrl = matched.NativeIconDataUrl,
                            IsRecent = matched.IsRecent,
                            LaunchCount = matched.LaunchCount,
                            TileSize = tileSize,
                            GroupName = p.GroupName ?? ""
                        });
                    }
                    else
                    {
                        pinned.Add(new AppLauncherItem
                        {
                            Id = p.TargetPath,
                            Name = p.Name,
                            TargetPath = p.TargetPath,
                            IconDataUrl = !string.IsNullOrEmpty(p.IconDataUrl) ? p.IconDataUrl : "",
                            NativeIconDataUrl = !string.IsNullOrEmpty(p.IconDataUrl) ? p.IconDataUrl : "",
                            TileSize = tileSize,
                            GroupName = p.GroupName ?? ""
                        });
                    }
                }
            }

            // Recent apps
            var recent = new List<AppLauncherItem>();
            if (showRecent)
            {
                recent = filteredAll
                    .Where(a => a.LaunchCount > 0)
                    .OrderByDescending(a => a.LaunchCount)
                    .Take(6)
                    .ToList();

                if (recent.Count < 6)
                {
                    var fallback = filteredAll
                        .Where(a => !recent.Contains(a))
                        .Take(6 - recent.Count);
                    recent.AddRange(fallback);
                }
            }

            return new
            {
                pinned,
                recent,
                all = filteredAll,
                config = config ?? new AppLauncherConfig()
            };
        }
    }

    public bool Launch(string targetPath, string appName, bool runAsAdmin = false, string? resolvedExe = null)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return false;

        try
        {
            ProcessStartInfo psi;
            if (targetPath.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
            {
                psi = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = targetPath,
                    UseShellExecute = true
                };
            }
            else
            {
                string fileToLaunch = targetPath;
                if (runAsAdmin && !string.IsNullOrWhiteSpace(resolvedExe) && File.Exists(resolvedExe))
                {
                    fileToLaunch = resolvedExe;
                }

                psi = new ProcessStartInfo
                {
                    FileName = fileToLaunch,
                    UseShellExecute = true
                };

                if (runAsAdmin)
                {
                    psi.Verb = "runas";
                }
            }

            Process.Start(psi);

            if (!string.IsNullOrWhiteSpace(appName))
            {
                lock (_lock)
                {
                    _launchStats[appName] = (_launchStats.TryGetValue(appName, out int c) ? c : 0) + 1;
                    var app = _apps.FirstOrDefault(a => string.Equals(a.Name, appName, StringComparison.OrdinalIgnoreCase));
                    if (app != null)
                    {
                        app.LaunchCount = _launchStats[appName];
                        app.IsRecent = true;
                    }
                }
                SaveRecentStats();
            }

            return true;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // User cancelled UAC prompt
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to launch '{targetPath}': {ex.Message}");
            return false;
        }
    }

    public bool OpenFileLocation(string targetPath, string? resolvedExe = null)
    {
        try
        {
            string? path = null;
            if (File.Exists(targetPath))
            {
                path = targetPath;
            }
            else if (!string.IsNullOrWhiteSpace(resolvedExe) && File.Exists(resolvedExe))
            {
                path = resolvedExe;
            }

            if (!string.IsNullOrEmpty(path))
            {
                Process.Start("explorer.exe", $"/select,\"{path}\"");
                return true;
            }
            else if (targetPath.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:appsfeatures",
                    UseShellExecute = true
                });
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open file location for '{targetPath}': {ex.Message}");
            return false;
        }
    }
}
