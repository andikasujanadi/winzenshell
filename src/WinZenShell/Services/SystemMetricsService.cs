using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using WinZenShell.Native;

namespace WinZenShell.Services;

public class RunningWindowItem
{
    public long Hwnd { get; set; }
    public string Title { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string AppId { get; set; } = "";
    public bool IsMinimized { get; set; }
    public bool IsActive { get; set; }
    public string IconDataUrl { get; set; } = "";
    public long OrderIndex { get; set; }
    public string ExePath { get; set; } = "";
}

public class HiddenAppEntry
{
    public string AppId { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int WindowCount { get; set; } = 1;
}

public class SystemMetricsSnapshot
{
    public int CpuPercent { get; set; }
    public int MemoryPercent { get; set; }
    public double MemoryUsedGb { get; set; }
    public double MemoryTotalGb { get; set; }
    public int BrightnessPercent { get; set; } = 100;
    public int VolumePercent { get; set; } = 50;
    public bool IsMuted { get; set; }
    public List<VirtualDesktopInfo> VirtualDesktops { get; set; } = new();
    public int CurrentVirtualDesktopIndex { get; set; }
    public string CurrentVirtualDesktopName { get; set; } = "";
    public string TimeFormatted { get; set; } = "";
    public string DateFormatted { get; set; } = "";
    public List<RunningWindowItem> Windows { get; set; } = new();
    public List<HiddenAppEntry> HiddenApps { get; set; } = new();
    public List<PinnedAppEntry> PinnedApps { get; set; } = new();
    public int BatteryPercent { get; set; } = 100;
    public bool IsCharging { get; set; } = false;
    public bool HasBattery { get; set; } = true;
    public string BatteryStatusText { get; set; } = "Fully Charged";
}

public class SystemMetricsService
{
    public BrightnessService? Brightness { get; set; }
    public AudioService? Audio { get; set; }
    public VirtualDesktopService? VirtualDesktops { get; set; }

    private DateTime _lastCpuCheck = DateTime.UtcNow;
    private TimeSpan _lastTotalProcTime = TimeSpan.Zero;
    private int _cachedCpuPercent = 8;

    // Preserve fixed window order so active windows never jump to the first slot
    private readonly Dictionary<long, long> _windowOrderMap = new();
    private long _nextWindowOrder = 1;

    // Track hidden apps by identity so all windows of the same application are hidden together
    private readonly HashSet<string> _hiddenAppIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _hiddenProcessNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _hiddenTitles = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<long> _hiddenHwnds = new();
    private readonly Dictionary<string, HiddenAppEntry> _hiddenAppCatalog = new(StringComparer.OrdinalIgnoreCase);

    // Cache extracted PNG base64 icons by HWND and by executable path
    private readonly Dictionary<long, string> _hwndIconCache = new();
    private readonly Dictionary<string, string> _exeIconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _appExePathMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _appIconMap = new(StringComparer.OrdinalIgnoreCase);
    private IntPtr _lastForegroundAppHwnd = IntPtr.Zero;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    public SystemMetricsSnapshot GetSnapshot(IntPtr ignoreTaskbarHwnd, IntPtr ignoreSettingsHwnd)
    {
        var mem = new MEMORYSTATUSEX();
        GlobalMemoryStatusEx(mem);

        double totalGb = Math.Round(mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0), 1);
        double availGb = mem.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
        double usedGb = Math.Round(totalGb - availGb, 1);

        int cpu = SampleApproximateCpu();
        var now = DateTime.Now;

        var windows = GetTopLevelAppWindows(ignoreTaskbarHwnd, ignoreSettingsHwnd);
        var desktops = VirtualDesktops?.GetDesktops() ?? new List<VirtualDesktopInfo>();
        int curIdx = desktops.FindIndex(d => d.IsCurrent);
        if (curIdx < 0 && desktops.Count > 0) curIdx = 0;
        string curName = curIdx >= 0 && curIdx < desktops.Count ? desktops[curIdx].Name : "Desktop 1";

        bool hasBattery = false;
        int batteryPercent = 100;
        bool isCharging = false;
        string batteryStatusText = "AC Connected";

        if (Win32Api.GetSystemPowerStatus(out var powerStatus))
        {
            bool noBattery = (powerStatus.BatteryFlag & 128) != 0 || powerStatus.BatteryLifePercent == 255;
            hasBattery = !noBattery;
            batteryPercent = noBattery ? 100 : Math.Clamp((int)powerStatus.BatteryLifePercent, 0, 100);
            isCharging = powerStatus.ACLineStatus == 1;

            if (noBattery)
            {
                batteryStatusText = "Desktop (AC Power)";
            }
            else if (isCharging)
            {
                batteryStatusText = batteryPercent >= 100 ? "Fully Charged" : $"Charging ({batteryPercent}%)";
            }
            else
            {
                batteryStatusText = $"On Battery ({batteryPercent}%)";
            }
        }

        return new SystemMetricsSnapshot
        {
            CpuPercent = cpu,
            MemoryPercent = (int)mem.dwMemoryLoad,
            MemoryUsedGb = usedGb,
            MemoryTotalGb = totalGb,
            BrightnessPercent = Brightness?.CurrentBrightness ?? 100,
            VolumePercent = Audio?.GetVolume() ?? 50,
            IsMuted = Audio?.GetMute() ?? false,
            VirtualDesktops = desktops,
            CurrentVirtualDesktopIndex = curIdx,
            CurrentVirtualDesktopName = curName,
            TimeFormatted = now.ToString("HH:mm:ss"),
            DateFormatted = now.ToString("ddd, MMM d"),
            Windows = windows,
            HiddenApps = GetHiddenApps(),
            BatteryPercent = batteryPercent,
            IsCharging = isCharging,
            HasBattery = hasBattery,
            BatteryStatusText = batteryStatusText
        };
    }

    private static readonly Dictionary<string, string> _knownAppNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Microsoft.Notes", "Sticky Notes" },
        { "MicrosoftStickyNotes", "Sticky Notes" },
        { "Microsoft.WindowsCalculator", "Calculator" },
        { "CalculatorApp", "Calculator" },
        { "Microsoft.WindowsTerminal", "Terminal" },
        { "WindowsTerminal", "Terminal" },
        { "Microsoft.Photos", "Photos" },
        { "Microsoft.Paint", "Paint" },
        { "mspaint", "Paint" },
        { "Microsoft.WindowsStore", "Microsoft Store" },
        { "explorer", "File Explorer" },
        { "chrome", "Google Chrome" },
        { "msedge", "Microsoft Edge" },
        { "code", "Visual Studio Code" },
        { "devenv", "Visual Studio" },
        { "notepad", "Notepad" }
    };

    public static string NormalizeAppIdentity(string processName, string title)
    {
        string p = (processName ?? "").Trim().ToLowerInvariant();
        string t = (title ?? "").Trim().ToLowerInvariant();

        if (p.Contains("notes") || p.Contains("sticky") || t.Contains("sticky notes") || t.Contains("stickynotes"))
            return "microsoft.notes";

        if (p.Equals("applicationframehost", StringComparison.OrdinalIgnoreCase))
        {
            if (t.Contains("sticky") || t.Contains("note")) return "microsoft.notes";
            if (t.Contains("calc")) return "calculatorapp";
            if (t.Contains("terminal")) return "windowsterminal";
            if (t.Contains("setting")) return "windowssettings";
            return string.IsNullOrWhiteSpace(t) ? "uwp-app" : "uwp:" + t;
        }

        if (p.Contains("explorer")) return "explorer";
        if (p.Contains("chrome")) return "chrome";
        if (p.Contains("firefox")) return "firefox";
        if (p.Contains("msedge") || p.Contains("edge")) return "msedge";

        return string.IsNullOrWhiteSpace(p) ? "app" : p;
    }

    public static string GetFriendlyAppName(string processName, string title)
    {
        string p = (processName ?? "").Trim();
        if (p.Equals("Microsoft.Notes", StringComparison.OrdinalIgnoreCase) ||
            p.IndexOf("Sticky", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (!string.IsNullOrWhiteSpace(title) && title.IndexOf("Sticky Notes", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return "Sticky Notes";
        }

        if (_knownAppNames.TryGetValue(p, out var friendly))
            return friendly;

        if (string.Equals(p, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                if (title.Contains("Sticky Notes", StringComparison.OrdinalIgnoreCase)) return "Sticky Notes";
                return title;
            }
            return "Windows App";
        }

        if (p.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
        {
            string stripped = p.Substring("Microsoft.".Length);
            return string.IsNullOrWhiteSpace(stripped) ? p : stripped;
        }

        return p;
    }

    public void HideAppByIdentity(long hwndVal, string? explicitAppId = null, string? processName = null, string? title = null)
    {
        string resolvedTitle = (title ?? "").Trim();
        string resolvedProc = (processName ?? "").Trim();

        try
        {
            if (hwndVal != 0 && (string.IsNullOrWhiteSpace(resolvedTitle) || string.IsNullOrWhiteSpace(resolvedProc) || string.Equals(resolvedProc, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase)))
            {
                IntPtr hWnd = new IntPtr(hwndVal);
                if (string.IsNullOrWhiteSpace(resolvedTitle))
                {
                    int len = Win32Api.GetWindowTextLength(hWnd);
                    if (len > 0)
                    {
                        var sb = new StringBuilder(len + 1);
                        Win32Api.GetWindowText(hWnd, sb, sb.Capacity);
                        resolvedTitle = sb.ToString().Trim();
                    }
                }

                Win32Api.GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid > 0)
                {
                    try
                    {
                        using var proc = Process.GetProcessById((int)pid);
                        resolvedProc = proc.ProcessName;
                    }
                    catch { }
                }

                if (string.Equals(resolvedProc, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
                {
                    uint childPid = 0;
                    Win32Api.EnumChildWindows(hWnd, (cWnd, _) =>
                    {
                        Win32Api.GetWindowThreadProcessId(cWnd, out uint cPid);
                        if (cPid > 0 && cPid != pid)
                        {
                            childPid = cPid;
                            return false;
                        }
                        return true;
                    }, IntPtr.Zero);

                    if (childPid > 0)
                    {
                        try
                        {
                            using var cp = Process.GetProcessById((int)childPid);
                            resolvedProc = cp.ProcessName;
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }

        string appId = !string.IsNullOrWhiteSpace(explicitAppId)
            ? explicitAppId.Trim().ToLowerInvariant()
            : NormalizeAppIdentity(resolvedProc, resolvedTitle);

        if (appId == "microsoft.notes" || resolvedProc.Equals("Microsoft.Notes", StringComparison.OrdinalIgnoreCase) || resolvedTitle.Contains("Sticky Notes"))
        {
            appId = "microsoft.notes";
            resolvedProc = "Microsoft.Notes";
        }

        string displayName = GetFriendlyAppName(resolvedProc, resolvedTitle);

        if (hwndVal != 0) _hiddenHwnds.Add(hwndVal);
        _hiddenAppIds.Add(appId);
        _hiddenProcessNames.Add(resolvedProc);

        if (appId == "microsoft.notes")
        {
            _hiddenAppIds.Add("microsoft.notes");
            _hiddenProcessNames.Add("Microsoft.Notes");
            _hiddenProcessNames.Add("ApplicationFrameHost");
            _hiddenTitles.Add("Sticky Notes");
            displayName = "Sticky Notes";
            resolvedProc = "Microsoft.Notes";
        }

        _hiddenAppCatalog[appId] = new HiddenAppEntry
        {
            AppId = appId,
            ProcessName = resolvedProc,
            DisplayName = displayName,
            WindowCount = 1
        };
    }

    public void UnhideAppByIdentity(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return;
        string normId = appId.Trim().ToLowerInvariant();

        if (normId == "microsoft.notes")
        {
            _hiddenAppIds.Remove("microsoft.notes");
            _hiddenProcessNames.Remove("Microsoft.Notes");
            _hiddenProcessNames.Remove("ApplicationFrameHost");
            _hiddenTitles.Remove("Sticky Notes");
            _hiddenAppCatalog.Remove("microsoft.notes");
            return;
        }

        if (_hiddenAppCatalog.TryGetValue(normId, out var entry))
        {
            _hiddenProcessNames.Remove(entry.ProcessName);
            _hiddenTitles.Remove(entry.DisplayName);
        }

        _hiddenAppIds.Remove(normId);
        _hiddenAppCatalog.Remove(normId);
    }

    public void UnhideAllApps()
    {
        _hiddenAppIds.Clear();
        _hiddenProcessNames.Clear();
        _hiddenTitles.Clear();
        _hiddenHwnds.Clear();
        _hiddenAppCatalog.Clear();
    }

    public List<HiddenAppEntry> GetHiddenApps()
    {
        return _hiddenAppCatalog.Values.ToList();
    }

    public string? GetExePathForApp(string appId, string? processName = null)
    {
        if (!string.IsNullOrWhiteSpace(appId) && _appExePathMap.TryGetValue(appId, out var ep))
            return ep;
        if (!string.IsNullOrWhiteSpace(processName) && _appExePathMap.TryGetValue(processName, out ep))
            return ep;
        return null;
    }

    public string? GetIconForApp(string appId, string? processName = null)
    {
        if (!string.IsNullOrWhiteSpace(appId) && _appIconMap.TryGetValue(appId, out var ic))
            return ic;
        if (!string.IsNullOrWhiteSpace(processName) && _appIconMap.TryGetValue(processName, out ic))
            return ic;
        return null;
    }

    public void CloseWindow(long hwndVal)
    {
        IntPtr hWnd = new IntPtr(hwndVal);
        if (hWnd != IntPtr.Zero)
        {
            Win32Api.PostMessage(hWnd, Win32Api.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private int SampleApproximateCpu()
    {
        try
        {
            var now = DateTime.UtcNow;
            double elapsedMs = (now - _lastCpuCheck).TotalMilliseconds;
            if (elapsedMs < 800)
                return _cachedCpuPercent;

            TimeSpan sum = TimeSpan.Zero;
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id > 4)
                        sum += p.TotalProcessorTime;
                }
                catch
                {
                    // Ignore protected processes
                }
                finally
                {
                    p.Dispose();
                }
            }

            if (_lastTotalProcTime != TimeSpan.Zero && elapsedMs > 0)
            {
                double cpuDeltaMs = (sum - _lastTotalProcTime).TotalMilliseconds;
                double raw = (cpuDeltaMs / (elapsedMs * Environment.ProcessorCount)) * 100.0;
                _cachedCpuPercent = Math.Clamp((int)Math.Round(raw), 1, 100);
            }

            _lastTotalProcTime = sum;
            _lastCpuCheck = now;
        }
        catch
        {
            // Keep cached
        }
        return _cachedCpuPercent;
    }

    public List<RunningWindowItem> GetTopLevelAppWindows(IntPtr ignoreTaskbarHwnd, IntPtr ignoreSettingsHwnd)
    {
        var discovered = new List<RunningWindowItem>();
        var activeHwnds = new HashSet<long>();
        IntPtr shellWnd = Win32Api.GetShellWindow();
        IntPtr foregroundWnd = Win32Api.GetForegroundWindow();

        uint fgPid = 0;
        if (foregroundWnd != IntPtr.Zero)
        {
            Win32Api.GetWindowThreadProcessId(foregroundWnd, out fgPid);
        }

        if (foregroundWnd != IntPtr.Zero &&
            foregroundWnd != shellWnd &&
            foregroundWnd != ignoreTaskbarHwnd &&
            fgPid != (uint)Environment.ProcessId &&
            !Win32Api.IsIconic(foregroundWnd))
        {
            _lastForegroundAppHwnd = foregroundWnd;
        }
        else if (_lastForegroundAppHwnd != IntPtr.Zero &&
                 (!Win32Api.IsWindowVisible(_lastForegroundAppHwnd) || Win32Api.IsIconic(_lastForegroundAppHwnd)))
        {
            _lastForegroundAppHwnd = IntPtr.Zero;
        }

        IntPtr effectiveForeground = (fgPid == (uint)Environment.ProcessId)
            ? _lastForegroundAppHwnd
            : foregroundWnd;

        Win32Api.EnumWindows((hWnd, _) =>
        {
            if (hWnd == shellWnd || hWnd == ignoreTaskbarHwnd)
                return true;
            if (!Win32Api.IsWindowVisible(hWnd))
                return true;

            if (Win32Api.DwmGetWindowAttribute(hWnd, Win32Api.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;

            int exStyle = Win32Api.GetWindowLong(hWnd, Win32Api.GWL_EXSTYLE);
            if ((exStyle & Win32Api.WS_EX_TOOLWINDOW) != 0 && (exStyle & Win32Api.WS_EX_APPWINDOW) == 0)
                return true;

            int len = Win32Api.GetWindowTextLength(hWnd);
            if (len <= 0)
                return true;

            var sb = new StringBuilder(len + 1);
            Win32Api.GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString().Trim();
            if (string.IsNullOrWhiteSpace(title) ||
                title == "Program Manager" ||
                title == "Windows Input Experience" ||
                title == "Settings")
                return true;

            long hwndKey = hWnd.ToInt64();
            activeHwnds.Add(hwndKey);

            if (!_windowOrderMap.TryGetValue(hwndKey, out long orderIdx))
            {
                orderIdx = _nextWindowOrder++;
                _windowOrderMap[hwndKey] = orderIdx;
            }

            string procName = "App";
            uint pid = 0;
            try
            {
                Win32Api.GetWindowThreadProcessId(hWnd, out pid);
                if (pid > 0)
                {
                    using var p = Process.GetProcessById((int)pid);
                    procName = p.ProcessName;
                }
            }
            catch
            {
                // Ignore
            }

            if (string.Equals(procName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    uint childPid = 0;
                    Win32Api.EnumChildWindows(hWnd, (cWnd, _) =>
                    {
                        Win32Api.GetWindowThreadProcessId(cWnd, out uint cPid);
                        if (cPid > 0 && cPid != pid)
                        {
                            childPid = cPid;
                            return false;
                        }
                        return true;
                    }, IntPtr.Zero);

                    if (childPid > 0)
                    {
                        using var cp = Process.GetProcessById((int)childPid);
                        procName = cp.ProcessName;
                        pid = childPid;
                    }
                }
                catch { }
            }

            string appId = NormalizeAppIdentity(procName, title);
            if (appId == "microsoft.notes" || procName.Equals("Microsoft.Notes", StringComparison.OrdinalIgnoreCase) || title.Contains("Sticky Notes"))
            {
                appId = "microsoft.notes";
                procName = "Microsoft.Notes";
            }

            if (_hiddenHwnds.Contains(hwndKey) ||
                _hiddenAppIds.Contains(appId) ||
                _hiddenProcessNames.Contains(procName) ||
                _hiddenTitles.Contains(title) ||
                (appId == "microsoft.notes" && _hiddenAppIds.Contains("microsoft.notes")))
            {
                if (_hiddenAppCatalog.TryGetValue(appId, out var entry))
                {
                    entry.WindowCount++;
                }
                return true;
            }

            string iconDataUrl = "";
            if (appId != "microsoft.notes" && !procName.Equals("Microsoft.Notes", StringComparison.OrdinalIgnoreCase) && !procName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
            {
                iconDataUrl = GetOrExtractWindowIcon(hWnd, pid);
            }

            string? exePath = pid > 0 ? TryGetProcessExePath(pid) : null;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                _appExePathMap[appId] = exePath;
                _appExePathMap[procName] = exePath;
            }
            if (!string.IsNullOrWhiteSpace(iconDataUrl))
            {
                _appIconMap[appId] = iconDataUrl;
                _appIconMap[procName] = iconDataUrl;
            }

            string friendlyName = GetFriendlyAppName(procName, title);

            bool isMin = Win32Api.IsIconic(hWnd);

            discovered.Add(new RunningWindowItem
            {
                Hwnd = hwndKey,
                Title = title.Length > 28 ? title[..28] + "…" : title,
                ProcessName = friendlyName,
                AppId = appId,
                IsMinimized = isMin,
                IsActive = !isMin && (hWnd == effectiveForeground),
                IconDataUrl = iconDataUrl,
                OrderIndex = orderIdx,
                ExePath = exePath ?? ""
            });

            return discovered.Count < 14;
        }, IntPtr.Zero);

        // Prune closed windows from order & icon caches
        var staleKeys = _windowOrderMap.Keys.Where(k => !activeHwnds.Contains(k)).ToList();
        foreach (long k in staleKeys)
        {
            _windowOrderMap.Remove(k);
            _hwndIconCache.Remove(k);
        }

        // Always return sorted by stable creation/discovery OrderIndex so active windows never reorder!
        return discovered.OrderBy(w => w.OrderIndex).Take(8).ToList();
    }

    private string GetOrExtractWindowIcon(IntPtr hWnd, uint pid)
    {
        long key = hWnd.ToInt64();
        if (_hwndIconCache.TryGetValue(key, out string? cached) && !string.IsNullOrEmpty(cached))
            return cached;

        try
        {
            // 1. Try window HICON via WM_GETICON or ClassLongPtr
            IntPtr hIcon = IntPtr.Zero;
            Win32Api.SendMessageTimeout(
                hWnd,
                Win32Api.WM_GETICON,
                new IntPtr(Win32Api.ICON_BIG),
                IntPtr.Zero,
                Win32Api.SMTO_ABORTIFHUNG,
                40,
                out hIcon);

            if (hIcon == IntPtr.Zero)
            {
                Win32Api.SendMessageTimeout(
                    hWnd,
                    Win32Api.WM_GETICON,
                    new IntPtr(Win32Api.ICON_SMALL2),
                    IntPtr.Zero,
                    Win32Api.SMTO_ABORTIFHUNG,
                    40,
                    out hIcon);
            }

            if (hIcon != IntPtr.Zero)
            {
                using var ico = Icon.FromHandle(hIcon);
                string dataUrl = IconToPngDataUrl(ico);
                if (!string.IsNullOrEmpty(dataUrl))
                {
                    _hwndIconCache[key] = dataUrl;
                    return dataUrl;
                }
            }

            // 2. Extract associated icon from process executable path (gives real yellow folder icon for explorer.exe, etc.)
            if (pid > 0)
            {
                string? exePath = TryGetProcessExePath(pid);
                if (!string.IsNullOrWhiteSpace(exePath) &&
                    !exePath.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (_exeIconCache.TryGetValue(exePath, out string? exeCached))
                    {
                        _hwndIconCache[key] = exeCached;
                        return exeCached;
                    }

                    if (File.Exists(exePath))
                    {
                        using var assocIcon = Icon.ExtractAssociatedIcon(exePath);
                        if (assocIcon != null)
                        {
                            string dataUrl = IconToPngDataUrl(assocIcon);
                            _exeIconCache[exePath] = dataUrl;
                            _hwndIconCache[key] = dataUrl;
                            return dataUrl;
                        }
                    }
                }
            }

            // 3. Fallback to window class icon
            hIcon = Win32Api.GetClassLongPtr(hWnd, Win32Api.GCLP_HICON);
            if (hIcon == IntPtr.Zero)
            {
                hIcon = Win32Api.GetClassLongPtr(hWnd, Win32Api.GCLP_HICONSM);
            }

            if (hIcon != IntPtr.Zero)
            {
                using var ico = Icon.FromHandle(hIcon);
                string dataUrl = IconToPngDataUrl(ico);
                if (!string.IsNullOrEmpty(dataUrl))
                {
                    _hwndIconCache[key] = dataUrl;
                    return dataUrl;
                }
            }
        }
        catch
        {
            // Ignore icon extraction errors
        }

        return "";
    }

    private static string? TryGetProcessExePath(uint pid)
    {
        IntPtr hProc = Win32Api.OpenProcess(Win32Api.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProc == IntPtr.Zero)
            return null;

        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            if (Win32Api.QueryFullProcessImageName(hProc, 0, sb, ref size))
            {
                return sb.ToString();
            }
        }
        finally
        {
            Win32Api.CloseHandle(hProc);
        }
        return null;
    }

    private static string IconToPngDataUrl(Icon icon)
    {
        try
        {
            using var bmp = icon.ToBitmap();
            using var resized = new Bitmap(bmp, new Size(20, 20));
            using var ms = new MemoryStream();
            resized.Save(ms, ImageFormat.Png);
            return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return "";
        }
    }

    public void ActivateOrToggleWindow(long hwndVal, bool? wasActiveHint = null)
    {
        IntPtr hWnd = new IntPtr(hwndVal);
        if (hWnd == IntPtr.Zero) return;

        if (Win32Api.IsIconic(hWnd))
        {
            Win32Api.ShowWindow(hWnd, Win32Api.SW_RESTORE);
            Win32Api.SetForegroundWindow(hWnd);
            _lastForegroundAppHwnd = hWnd;
        }
        else if (wasActiveHint == true || Win32Api.GetForegroundWindow() == hWnd || _lastForegroundAppHwnd == hWnd)
        {
            Win32Api.ShowWindow(hWnd, Win32Api.SW_MINIMIZE);
            if (_lastForegroundAppHwnd == hWnd)
                _lastForegroundAppHwnd = IntPtr.Zero;
        }
        else
        {
            Win32Api.ShowWindow(hWnd, Win32Api.SW_SHOW);
            Win32Api.SetForegroundWindow(hWnd);
            _lastForegroundAppHwnd = hWnd;
        }
    }

    public void ActivateWindowOnly(long hwndVal)
    {
        IntPtr hWnd = new IntPtr(hwndVal);
        if (hWnd == IntPtr.Zero) return;

        if (Win32Api.IsIconic(hWnd))
        {
            Win32Api.ShowWindow(hWnd, Win32Api.SW_RESTORE);
        }
        else
        {
            Win32Api.ShowWindow(hWnd, Win32Api.SW_SHOW);
        }
        Win32Api.SetForegroundWindow(hWnd);
        _lastForegroundAppHwnd = hWnd;
    }

    public void MinimizeWindow(long hwndVal)
    {
        IntPtr hWnd = new IntPtr(hwndVal);
        if (hWnd == IntPtr.Zero) return;
        Win32Api.ShowWindow(hWnd, Win32Api.SW_MINIMIZE);
        if (_lastForegroundAppHwnd == hWnd)
            _lastForegroundAppHwnd = IntPtr.Zero;
    }

    public void TriggerWindowsStartMenu()
    {
        Win32Api.keybd_event(Win32Api.VK_LWIN, 0, Win32Api.KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
        Win32Api.keybd_event(Win32Api.VK_LWIN, 0, Win32Api.KEYEVENTF_EXTENDEDKEY | Win32Api.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}
