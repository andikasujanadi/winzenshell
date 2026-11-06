using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace WinZenShell.Services;

public class AppLauncherItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public string IconDataUrl { get; set; } = "";
    public bool IsRecent { get; set; }
    public int LaunchCount { get; set; }
}

public class AppLauncherService
{
    private readonly string _recentsFilePath;
    private readonly List<AppLauncherItem> _apps = new();
    private readonly HashSet<string> _seenNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private Dictionary<string, int> _launchStats = new(StringComparer.OrdinalIgnoreCase);

    public bool IsLoaded { get; private set; }
    public event Action? AppsLoaded;

    public AppLauncherService()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinZenShell");
        Directory.CreateDirectory(dir);
        _recentsFilePath = Path.Combine(dir, "recent_apps.json");

        LoadRecentStats();

        // Load built-ins immediately so launcher is instantly usable
        LoadBuiltinApps();

        // Scan shortcuts asynchronously
        Task.Run(ScanInstalledShortcuts);
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

    private void LoadBuiltinApps()
    {
        var builtins = new[]
        {
            ("File Explorer", "explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\explorer.exe"),
            ("Settings", "ms-settings:", ""),
            ("Windows Terminal", "wt.exe", ""),
            ("Notepad", "notepad.exe", Environment.GetFolderPath(Environment.SpecialFolder.System) + @"\notepad.exe"),
            ("Calculator", "calc.exe", Environment.GetFolderPath(Environment.SpecialFolder.System) + @"\calc.exe"),
            ("Task Manager", "taskmgr.exe", Environment.GetFolderPath(Environment.SpecialFolder.System) + @"\Taskmgr.exe"),
            ("Snipping Tool", "snippingtool.exe", "")
        };

        lock (_lock)
        {
            foreach (var (name, target, iconSource) in builtins)
            {
                if (_seenNames.Add(name))
                {
                    string iconUrl = "";
                    if (!string.IsNullOrEmpty(iconSource) && File.Exists(iconSource))
                    {
                        iconUrl = ExtractIconDataUrl(iconSource);
                    }

                    int count = _launchStats.TryGetValue(name, out int c) ? c : 0;
                    _apps.Add(new AppLauncherItem
                    {
                        Id = name.ToLowerInvariant().Replace(" ", "-"),
                        Name = name,
                        TargetPath = target,
                        IconDataUrl = iconUrl,
                        LaunchCount = count,
                        IsRecent = count > 0
                    });
                }
            }
        }
    }

    private void ScanInstalledShortcuts()
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
            "license", "licence", "website", "manual", "eula", "feedback"
        };

        var foundItems = new List<AppLauncherItem>();

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
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        if (string.IsNullOrWhiteSpace(fileName)) continue;

                        string lowerName = fileName.ToLowerInvariant();
                        if (ignoredKeywords.Any(k => lowerName.Contains(k))) continue;

                        lock (_lock)
                        {
                            if (_seenNames.Contains(fileName)) continue;
                        }

                        string iconUrl = ExtractIconDataUrl(file);
                        int count = _launchStats.TryGetValue(fileName, out int c) ? c : 0;

                        foundItems.Add(new AppLauncherItem
                        {
                            Id = Guid.NewGuid().ToString("N")[..8],
                            Name = fileName,
                            TargetPath = file,
                            IconDataUrl = iconUrl,
                            LaunchCount = count,
                            IsRecent = count > 0
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }

        lock (_lock)
        {
            foreach (var item in foundItems)
            {
                if (_seenNames.Add(item.Name))
                {
                    _apps.Add(item);
                }
            }

            _apps.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            IsLoaded = true;
        }

        AppsLoaded?.Invoke();
    }

    private static string ExtractIconDataUrl(string path)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon == null) return "";

            using var bmp = icon.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            byte[] bytes = ms.ToArray();
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }
        catch
        {
            return "";
        }
    }

    public object GetAppsPayload()
    {
        lock (_lock)
        {
            // Recent apps: highest launch count, or defaults
            var recent = _apps
                .Where(a => a.LaunchCount > 0)
                .OrderByDescending(a => a.LaunchCount)
                .Take(5)
                .ToList();

            if (recent.Count < 5)
            {
                // Fallback top 5 popular apps
                var fallback = _apps
                    .Where(a => !recent.Contains(a))
                    .Take(5 - recent.Count);
                recent.AddRange(fallback);
            }

            return new
            {
                recent,
                all = _apps
            };
        }
    }

    public bool Launch(string targetPath, string appName)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = true
            };
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to launch '{targetPath}': {ex.Message}");
            return false;
        }
    }
}
