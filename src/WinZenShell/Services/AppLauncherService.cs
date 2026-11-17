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
            ("Settings", "ms-settings:", Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\ImmersiveControlPanel\SystemSettings.exe"),
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
                    if (string.IsNullOrEmpty(iconUrl))
                    {
                        iconUrl = GetBuiltinFallbackIconDataUrl(name);
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
                        string lowerFullPath = file.ToLowerInvariant();
                        if (ignoredFolderSegments.Any(seg => lowerFullPath.Contains(seg))) continue;

                        string fileName = Path.GetFileNameWithoutExtension(file);
                        if (string.IsNullOrWhiteSpace(fileName)) continue;

                        string lowerName = fileName.ToLowerInvariant();
                        if (ignoredExactNames.Contains(fileName)) continue;
                        if (ignoredKeywords.Any(k => lowerName.Contains(k))) continue;
                        if (lowerName.Contains("(x86)") || lowerName.Contains("(32-bit)")) continue;

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

    private static string GetBuiltinFallbackIconDataUrl(string appName)
    {
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
                    if (iconIndex != 0)
                    {
                        string? indexedDataUrl = TryExtractIndexedIconDataUrl(resolvedPath, iconIndex);
                        if (!string.IsNullOrEmpty(indexedDataUrl))
                        {
                            return indexedDataUrl;
                        }
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

    private static string? TryExtractIndexedIconDataUrl(string filePath, int iconIndex)
    {
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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

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
            if (!string.IsNullOrWhiteSpace(targetPath) && File.Exists(targetPath))
            {
                return (targetPath, 0);
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

    public object GetAppsPayload()
    {
        lock (_lock)
        {
            // Recent apps: highest launch count, or defaults
            var recent = _apps
                .Where(a => a.LaunchCount > 0)
                .OrderByDescending(a => a.LaunchCount)
                .Take(6)
                .ToList();

            if (recent.Count < 6)
            {
                // Fallback top 6 popular apps
                var fallback = _apps
                    .Where(a => !recent.Contains(a))
                    .Take(6 - recent.Count);
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
