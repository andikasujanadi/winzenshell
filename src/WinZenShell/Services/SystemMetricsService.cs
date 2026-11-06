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
    public string TimeFormatted { get; set; } = "";
    public string DateFormatted { get; set; } = "";
    public List<RunningWindowItem> Windows { get; set; } = new();
    public List<HiddenAppEntry> HiddenApps { get; set; } = new();
}

public class SystemMetricsService
{
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

        return new SystemMetricsSnapshot
        {
            CpuPercent = cpu,
            MemoryPercent = (int)mem.dwMemoryLoad,
            MemoryUsedGb = usedGb,
            MemoryTotalGb = totalGb,
            TimeFormatted = now.ToString("HH:mm:ss"),
            DateFormatted = now.ToString("ddd, MMM d"),
            Windows = windows,
            HiddenApps = GetHiddenApps()
        };
    }

    public static string NormalizeAppIdentity(string processName, string title)
    {
        string p = (processName ?? "").Trim();
        string t = (title ?? "").Trim();

        if (string.Equals(p, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(t))
        {
            return "uwp:" + t.ToLowerInvariant();
        }

        if (p.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(t))
        {
            return "uwp:" + t.ToLowerInvariant();
        }

        return string.IsNullOrWhiteSpace(p) ? (string.IsNullOrWhiteSpace(t) ? "unknown" : t.ToLowerInvariant()) : p.ToLowerInvariant();
    }

    public void HideAppByIdentity(long hwndVal, string? processName = null, string? title = null)
    {
        string resolvedTitle = (title ?? "").Trim();
        string resolvedProc = (processName ?? "").Trim();

        try
        {
            if (hwndVal != 0 && (string.IsNullOrWhiteSpace(resolvedTitle) || string.IsNullOrWhiteSpace(resolvedProc)))
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
                if (string.IsNullOrWhiteSpace(resolvedProc))
                {
                    Win32Api.GetWindowThreadProcessId(hWnd, out uint pid);
                    if (pid > 0)
                    {
                        using var proc = Process.GetProcessById((int)pid);
                        resolvedProc = proc.ProcessName;
                    }
                }
            }
        }
        catch { }

        if (string.IsNullOrWhiteSpace(resolvedProc)) resolvedProc = "App";
        string appId = NormalizeAppIdentity(resolvedProc, resolvedTitle);

        if (hwndVal != 0) _hiddenHwnds.Add(hwndVal);
        _hiddenAppIds.Add(appId);
        _hiddenProcessNames.Add(resolvedProc);
        if (!string.IsNullOrWhiteSpace(resolvedTitle))
        {
            _hiddenTitles.Add(resolvedTitle);
        }

        string displayName = !string.IsNullOrWhiteSpace(resolvedTitle) ? resolvedTitle : resolvedProc;
        if (resolvedProc.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(resolvedTitle))
        {
            displayName = resolvedTitle;
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

        if (_hiddenAppCatalog.TryGetValue(appId, out var entry))
        {
            _hiddenProcessNames.Remove(entry.ProcessName);
            _hiddenTitles.Remove(entry.DisplayName);
        }

        _hiddenAppIds.Remove(appId);
        _hiddenAppCatalog.Remove(appId);
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

        Win32Api.EnumWindows((hWnd, _) =>
        {
            if (hWnd == shellWnd || hWnd == ignoreTaskbarHwnd)
                return true;
            if (!Win32Api.IsWindowVisible(hWnd))
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

            string appId = NormalizeAppIdentity(procName, title);

            if (_hiddenHwnds.Contains(hwndKey) ||
                _hiddenAppIds.Contains(appId) ||
                _hiddenProcessNames.Contains(procName) ||
                _hiddenTitles.Contains(title))
            {
                if (_hiddenAppCatalog.TryGetValue(appId, out var entry))
                {
                    entry.WindowCount++;
                }
                return true;
            }

            string iconDataUrl = GetOrExtractWindowIcon(hWnd, pid);

            discovered.Add(new RunningWindowItem
            {
                Hwnd = hwndKey,
                Title = title.Length > 28 ? title[..28] + "…" : title,
                ProcessName = procName,
                AppId = appId,
                IsMinimized = Win32Api.IsIconic(hWnd),
                IsActive = hWnd == foregroundWnd,
                IconDataUrl = iconDataUrl,
                OrderIndex = orderIdx
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

            if (hIcon == IntPtr.Zero)
            {
                hIcon = Win32Api.GetClassLongPtr(hWnd, Win32Api.GCLP_HICON);
            }

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

            // 2. Fallback: Extract associated icon from process executable path
            if (pid > 0)
            {
                string? exePath = TryGetProcessExePath(pid);
                if (!string.IsNullOrWhiteSpace(exePath))
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
        using var bmp = icon.ToBitmap();
        using var resized = new Bitmap(bmp, new Size(20, 20));
        using var ms = new MemoryStream();
        resized.Save(ms, ImageFormat.Png);
        return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
    }

    public void ActivateOrToggleWindow(long hwndVal)
    {
        IntPtr hWnd = new IntPtr(hwndVal);
        if (hWnd == IntPtr.Zero) return;

        if (Win32Api.IsIconic(hWnd))
        {
            Win32Api.ShowWindow(hWnd, Win32Api.SW_RESTORE);
            Win32Api.SetForegroundWindow(hWnd);
        }
        else if (Win32Api.GetForegroundWindow() == hWnd)
        {
            Win32Api.ShowWindow(hWnd, Win32Api.SW_MINIMIZE);
        }
        else
        {
            Win32Api.SetForegroundWindow(hWnd);
        }
    }

    public void TriggerWindowsStartMenu()
    {
        Win32Api.keybd_event(Win32Api.VK_LWIN, 0, Win32Api.KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
        Win32Api.keybd_event(Win32Api.VK_LWIN, 0, Win32Api.KEYEVENTF_EXTENDEDKEY | Win32Api.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}
