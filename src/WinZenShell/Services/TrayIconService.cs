using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinZenShell.Native;

namespace WinZenShell.Services;

public class TrayIconItem
{
    public string Id { get; set; } = "";
    public string RegistryKeyName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Tooltip { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public int ProcessId { get; set; }
    public long TargetHwnd { get; set; }
    public uint CallbackMessage { get; set; }
    public uint Uid { get; set; }
    public bool IsPromoted { get; set; }
    public bool IsClassicToolbarItem { get; set; }
    public string IconDataUrl { get; set; } = "";
}

public class TrayIconService
{
    private readonly ConcurrentDictionary<string, string> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _cacheLock = new();
    private List<TrayIconItem> _cachedItems = new();
    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private volatile bool _isRefreshing;
    private bool _hasInitialCache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMilliseconds(2500);

    public TrayIconService()
    {
        // Pre-warm cache off the UI thread at startup so the first flyout open is instantaneous
        _ = RefreshTrayIconsAsync();
    }

    private static readonly Regex KnownFolderPrefixRegex = new(
        @"^\{([0-9a-fA-F\-]{36})\}\\(.*)$",
        RegexOptions.Compiled);

    private static readonly HashSet<string> IgnoredProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "svchost", "conhost", "runtimebroker", "backgroundtaskhost",
        "searchhost", "startmenuexperiencehost", "shellhost", "shellexperiencehost",
        "dwm", "csrss", "winlogon", "lsass", "services", "smss", "fontdrvhost",
        "textinputhost", "sihost", "ctfmon", "dllhost", "taskhostw",
        "applicationframehost", "wmiprvse", "msedgewebview2", "audiodg",
        "lockapp", "searchindexer", "securityhealthservice", "smartscreen"
    };

    #region P/Invoke

    private const uint PROCESS_VM_OPERATION = 0x0008;
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_VM_WRITE = 0x0020;
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RELEASE = 0x8000;
    private const uint PAGE_READWRITE = 0x04;

    private const uint TB_BUTTONCOUNT = 0x0418;
    private const uint TB_GETBUTTON = 0x0417;
    private const uint TB_GETBUTTONTEXTW = 0x044B;

    private const uint WM_MOUSEMOVE = 0x0200;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_RBUTTONDOWN = 0x0204;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_CONTEXTMENU = 0x007B;

    [StructLayout(LayoutKind.Sequential)]
    private struct TBBUTTON64
    {
        public int iBitmap;
        public int idCommand;
        public byte fsState;
        public byte fsStyle;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] bReserved;
        public ulong dwData;
        public IntPtr iString;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TRAYDATA
    {
        public IntPtr hWnd;
        public uint uID;
        public uint uCallbackMessage;
        public uint dwReserved1;
        public uint dwReserved2;
        public IntPtr hIcon;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(ref Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

    #endregion

    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _lastRefreshUtc = DateTime.MinValue;
        }
    }

    public List<TrayIconItem> GetActiveTrayIcons(bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            bool needsAsyncRefresh;
            List<TrayIconItem> snapshot;
            lock (_cacheLock)
            {
                snapshot = _cachedItems.ToList();
                needsAsyncRefresh = !_hasInitialCache || (DateTime.UtcNow - _lastRefreshUtc >= CacheTtl);
            }

            if (needsAsyncRefresh && !_isRefreshing)
            {
                _ = RefreshTrayIconsAsync();
            }

            return snapshot;
        }

        var items = BuildTrayIconList();
        lock (_cacheLock)
        {
            _cachedItems = items;
            _hasInitialCache = true;
            _lastRefreshUtc = DateTime.UtcNow;
            return items.ToList();
        }
    }

    public async Task<List<TrayIconItem>> RefreshTrayIconsAsync()
    {
        if (_isRefreshing)
        {
            lock (_cacheLock) return _cachedItems.ToList();
        }

        _isRefreshing = true;
        try
        {
            var items = await Task.Run(BuildTrayIconList);
            lock (_cacheLock)
            {
                _cachedItems = items;
                _hasInitialCache = true;
                _lastRefreshUtc = DateTime.UtcNow;
                return items.ToList();
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private List<TrayIconItem> BuildTrayIconList()
    {
        var results = new List<TrayIconItem>();
        var seenSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Try classic Explorer ToolbarWindow32 if populated (Windows 10 / ExplorerPatcher / StartAllBack)
        var classicItems = ReadAllToolbarTrayIcons();
        foreach (var item in classicItems)
        {
            string sig = $"{item.TargetHwnd}:{item.Uid}";
            if (seenSignatures.Add(sig))
            {
                results.Add(item);
            }
        }

        // 2. Windows 11 NotifyIconSettings registry merged with currently running processes
        var regItems = ReadNotifyIconRegistryItems();
        foreach (var item in regItems)
        {
            string exeKey = Path.GetFileName(item.ExecutablePath);
            // Avoid duplicating an icon already read live from ToolbarWindow32 for the same executable/tooltip
            bool alreadyLive = results.Any(r =>
                (!string.IsNullOrWhiteSpace(exeKey) &&
                 !string.IsNullOrWhiteSpace(r.ExecutablePath) &&
                 string.Equals(Path.GetFileName(r.ExecutablePath), exeKey, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(item.Tooltip) &&
                 string.Equals(r.Tooltip, item.Tooltip, StringComparison.OrdinalIgnoreCase)));

            if (!alreadyLive)
            {
                results.Add(item);
            }
        }

        return results
            .OrderByDescending(i => i.IsPromoted)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<TrayIconItem> ReadAllToolbarTrayIcons()
    {
        var items = new List<TrayIconItem>();
        try
        {
            IntPtr shellTray = FindWindow("Shell_TrayWnd", null);
            if (shellTray != IntPtr.Zero)
            {
                IntPtr notifyWnd = FindWindowEx(shellTray, IntPtr.Zero, "TrayNotifyWnd", null);
                if (notifyWnd != IntPtr.Zero)
                {
                    IntPtr sysPager = FindWindowEx(notifyWnd, IntPtr.Zero, "SysPager", null);
                    IntPtr toolbar = sysPager != IntPtr.Zero
                        ? FindWindowEx(sysPager, IntPtr.Zero, "ToolbarWindow32", null)
                        : FindWindowEx(notifyWnd, IntPtr.Zero, "ToolbarWindow32", null);

                    if (toolbar != IntPtr.Zero)
                    {
                        items.AddRange(ReadToolbarWindow(toolbar, isPromoted: true));
                    }
                }
            }

            IntPtr overflowWnd = FindWindow("NotifyIconOverflowWindow", null);
            if (overflowWnd != IntPtr.Zero)
            {
                IntPtr overflowToolbar = FindWindowEx(overflowWnd, IntPtr.Zero, "ToolbarWindow32", null);
                if (overflowToolbar != IntPtr.Zero)
                {
                    items.AddRange(ReadToolbarWindow(overflowToolbar, isPromoted: false));
                }
            }
        }
        catch { }

        return items;
    }

    private List<TrayIconItem> ReadToolbarWindow(IntPtr toolbarHwnd, bool isPromoted)
    {
        var list = new List<TrayIconItem>();
        if (toolbarHwnd == IntPtr.Zero) return list;

        int count = (int)SendMessage(toolbarHwnd, TB_BUTTONCOUNT, IntPtr.Zero, IntPtr.Zero);
        if (count <= 0 || count > 200) return list;

        GetWindowThreadProcessId(toolbarHwnd, out uint explorerPid);
        if (explorerPid == 0) return list;

        IntPtr hProc = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE, false, explorerPid);
        if (hProc == IntPtr.Zero) return list;

        IntPtr remoteBuf = IntPtr.Zero;
        try
        {
            int tbSize = Marshal.SizeOf<TBBUTTON64>();
            int traySize = Marshal.SizeOf<TRAYDATA>();
            int bufSize = Math.Max(tbSize, 512);
            remoteBuf = VirtualAllocEx(hProc, IntPtr.Zero, (uint)bufSize, MEM_COMMIT, PAGE_READWRITE);
            if (remoteBuf == IntPtr.Zero) return list;

            IntPtr localTb = Marshal.AllocHGlobal(tbSize);
            IntPtr localTray = Marshal.AllocHGlobal(traySize);
            IntPtr localText = Marshal.AllocHGlobal(512);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    IntPtr ok = SendMessage(toolbarHwnd, TB_GETBUTTON, (IntPtr)i, remoteBuf);
                    if (ok == IntPtr.Zero) continue;

                    if (!ReadProcessMemory(hProc, remoteBuf, localTb, tbSize, out _))
                        continue;

                    var tb = Marshal.PtrToStructure<TBBUTTON64>(localTb);
                    // Hidden buttons have fsState bit 0x08 (TBSTATE_HIDDEN)
                    if ((tb.fsState & 0x08) != 0) continue;
                    if (tb.dwData == 0) continue;

                    if (!ReadProcessMemory(hProc, (IntPtr)tb.dwData, localTray, traySize, out _))
                        continue;

                    var td = Marshal.PtrToStructure<TRAYDATA>(localTray);
                    if (td.hWnd == IntPtr.Zero) continue;

                    GetWindowThreadProcessId(td.hWnd, out uint ownerPid);
                    if (ownerPid == 0) continue;

                    string tooltip = "";
                    int textLen = (int)SendMessage(toolbarHwnd, TB_GETBUTTONTEXTW, (IntPtr)tb.idCommand, remoteBuf);
                    if (textLen > 0 && textLen < 255)
                    {
                        if (ReadProcessMemory(hProc, remoteBuf, localText, (textLen + 1) * 2, out _))
                        {
                            tooltip = Marshal.PtrToStringUni(localText, textLen) ?? "";
                        }
                    }

                    string exePath = TryGetProcessExePath((int)ownerPid);
                    string iconDataUrl = "";
                    if (td.hIcon != IntPtr.Zero)
                    {
                        iconDataUrl = IconHandleToDataUrl(td.hIcon);
                    }
                    if (string.IsNullOrEmpty(iconDataUrl) && !string.IsNullOrEmpty(exePath))
                    {
                        iconDataUrl = GetFileIconDataUrl(exePath);
                    }

                    string displayName = !string.IsNullOrWhiteSpace(tooltip)
                        ? tooltip.Split('\n')[0].Trim()
                        : Path.GetFileNameWithoutExtension(exePath);
                    if (string.IsNullOrWhiteSpace(displayName))
                        displayName = $"App ({ownerPid})";

                    list.Add(new TrayIconItem
                    {
                        Id = $"tb:{td.hWnd.ToInt64()}:{td.uID}",
                        Name = displayName,
                        Tooltip = string.IsNullOrWhiteSpace(tooltip) ? displayName : tooltip,
                        ExecutablePath = exePath,
                        ProcessId = (int)ownerPid,
                        TargetHwnd = td.hWnd.ToInt64(),
                        CallbackMessage = td.uCallbackMessage,
                        Uid = td.uID,
                        IsPromoted = isPromoted,
                        IsClassicToolbarItem = true,
                        IconDataUrl = iconDataUrl
                    });
                }
            }
            finally
            {
                Marshal.FreeHGlobal(localTb);
                Marshal.FreeHGlobal(localTray);
                Marshal.FreeHGlobal(localText);
            }
        }
        finally
        {
            if (remoteBuf != IntPtr.Zero)
                VirtualFreeEx(hProc, remoteBuf, 0, MEM_RELEASE);
            CloseHandle(hProc);
        }

        return list;
    }

    private List<TrayIconItem> ReadNotifyIconRegistryItems()
    {
        var list = new List<TrayIconItem>();
        var runningByPath = new Dictionary<string, (int Pid, string ExePath)>(StringComparer.OrdinalIgnoreCase);
        var runningByFile = new Dictionary<string, (int Pid, string ExePath)>(StringComparer.OrdinalIgnoreCase);

        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                if (IgnoredProcessNames.Contains(proc.ProcessName)) continue;

                string exePath = TryGetProcessExePath(proc);
                if (!string.IsNullOrWhiteSpace(exePath))
                {
                    runningByPath.TryAdd(exePath, (proc.Id, exePath));
                    string file = Path.GetFileName(exePath);
                    if (!string.IsNullOrWhiteSpace(file))
                        runningByFile.TryAdd(file, (proc.Id, exePath));
                }
                else
                {
                    runningByFile.TryAdd(proc.ProcessName + ".exe", (proc.Id, ""));
                }
            }
            catch { }
            finally
            {
                proc.Dispose();
            }
        }

        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings");
            if (root == null) return list;

            var matchedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string subKeyName in root.GetSubKeyNames())
            {
                using var sub = root.OpenSubKey(subKeyName);
                if (sub == null) continue;

                string rawExe = sub.GetValue("ExecutablePath") as string ?? "";
                if (string.IsNullOrWhiteSpace(rawExe)) continue;

                string resolvedExe = ResolveKnownFolderPath(rawExe);
                string fileName = Path.GetFileName(resolvedExe);
                string baseName = Path.GetFileNameWithoutExtension(resolvedExe);

                if (IgnoredProcessNames.Contains(baseName)) continue;

                int pid = 0;
                string actualExePath = resolvedExe;

                if (runningByPath.TryGetValue(resolvedExe, out var pathMatch))
                {
                    pid = pathMatch.Pid;
                    actualExePath = pathMatch.ExePath;
                }
                else if (!string.IsNullOrWhiteSpace(fileName) && runningByFile.TryGetValue(fileName, out var fileMatch))
                {
                    pid = fileMatch.Pid;
                    if (!string.IsNullOrWhiteSpace(fileMatch.ExePath))
                        actualExePath = fileMatch.ExePath;
                }
                else
                {
                    continue;
                }

                string tooltip = (sub.GetValue("InitialTooltip") as string ?? "").Trim();
                int promotedVal = sub.GetValue("IsPromoted") is int iv ? iv : 0;
                bool isPromoted = promotedVal == 1;

                // Deduplicate same app + tooltip across duplicate registry hashes
                string dedupKey = $"{actualExePath}|{tooltip}".ToLowerInvariant();
                if (!matchedKeys.Add(dedupKey)) continue;

                string iconDataUrl = "";
                if (!_iconCache.TryGetValue($"reg:{subKeyName}", out iconDataUrl!))
                {
                    if (sub.GetValue("IconSnapshot") is byte[] pngBytes && pngBytes.Length > 8)
                    {
                        iconDataUrl = "data:image/png;base64," + Convert.ToBase64String(pngBytes);
                    }
                    else if (!string.IsNullOrWhiteSpace(actualExePath) && File.Exists(actualExePath))
                    {
                        iconDataUrl = GetFileIconDataUrl(actualExePath);
                    }
                    if (!string.IsNullOrEmpty(iconDataUrl))
                    {
                        _iconCache[$"reg:{subKeyName}"] = iconDataUrl;
                    }
                }

                string displayName = !string.IsNullOrWhiteSpace(tooltip)
                    ? tooltip.Split('\n')[0].Trim()
                    : Path.GetFileNameWithoutExtension(actualExePath);
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = baseName;

                list.Add(new TrayIconItem
                {
                    Id = $"reg:{subKeyName}",
                    RegistryKeyName = subKeyName,
                    Name = displayName,
                    Tooltip = string.IsNullOrWhiteSpace(tooltip) ? displayName : tooltip,
                    ExecutablePath = actualExePath,
                    ProcessId = pid,
                    IsPromoted = isPromoted,
                    IsClassicToolbarItem = false,
                    IconDataUrl = iconDataUrl
                });
            }
        }
        catch { }

        return list;
    }

    public bool TogglePromoted(string registryKeyName)
    {
        if (string.IsNullOrWhiteSpace(registryKeyName)) return false;
        try
        {
            using var sub = Registry.CurrentUser.OpenSubKey($@"Control Panel\NotifyIconSettings\{registryKeyName}", writable: true);
            if (sub == null) return false;
            int current = sub.GetValue("IsPromoted") is int iv ? iv : 0;
            int next = current == 1 ? 0 : 1;
            sub.SetValue("IsPromoted", next, RegistryValueKind.DWord);
            lock (_cacheLock)
            {
                bool nextPromoted = next == 1;
                foreach (var cached in _cachedItems)
                {
                    if (string.Equals(cached.RegistryKeyName, registryKeyName, StringComparison.OrdinalIgnoreCase))
                        cached.IsPromoted = nextPromoted;
                }
                _cachedItems = _cachedItems
                    .OrderByDescending(i => i.IsPromoted)
                    .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                _lastRefreshUtc = DateTime.UtcNow;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool ActivateTrayIcon(string id, bool rightClick = false, Action? onWinZenSelfClick = null)
    {
        var item = GetActiveTrayIcons().FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.Ordinal));
        if (item == null) return false;

        if (!string.IsNullOrWhiteSpace(item.ExecutablePath) &&
            item.ExecutablePath.EndsWith("WinZenShell.exe", StringComparison.OrdinalIgnoreCase))
        {
            onWinZenSelfClick?.Invoke();
            return true;
        }

        if (item.IsClassicToolbarItem && item.TargetHwnd != 0 && item.CallbackMessage != 0)
        {
            IntPtr target = (IntPtr)item.TargetHwnd;
            if (item.ProcessId > 0) AllowSetForegroundWindow(item.ProcessId);

            IntPtr wParam = (IntPtr)item.Uid;
            PostMessage(target, item.CallbackMessage, wParam, (IntPtr)WM_MOUSEMOVE);
            if (rightClick)
            {
                PostMessage(target, item.CallbackMessage, wParam, (IntPtr)WM_RBUTTONDOWN);
                PostMessage(target, item.CallbackMessage, wParam, (IntPtr)WM_RBUTTONUP);
                PostMessage(target, item.CallbackMessage, wParam, (IntPtr)WM_CONTEXTMENU);
            }
            else
            {
                PostMessage(target, item.CallbackMessage, wParam, (IntPtr)WM_LBUTTONDOWN);
                PostMessage(target, item.CallbackMessage, wParam, (IntPtr)WM_LBUTTONUP);
            }
            return true;
        }

        // Modern Windows 11 registry-matched running tray app: locate its main or hidden window and restore/activate
        if (item.ProcessId > 0)
        {
            IntPtr windowToShow = FindBestWindowForProcess(item.ProcessId);
            if (windowToShow != IntPtr.Zero)
            {
                AllowSetForegroundWindow(item.ProcessId);
                ShowWindow(windowToShow, IsIconic(windowToShow) ? 9 : 5); // SW_RESTORE : SW_SHOW
                SetForegroundWindow(windowToShow);
                return true;
            }
        }

        return false;
    }

    private static IntPtr FindBestWindowForProcess(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (p.MainWindowHandle != IntPtr.Zero)
                return p.MainWindowHandle;
        }
        catch { }

        IntPtr bestWithTitle = IntPtr.Zero;
        IntPtr fallbackWindow = IntPtr.Zero;

        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out uint winPid);
            if (winPid != (uint)pid) return true;

            var clsSb = new StringBuilder(128);
            GetClassName(hWnd, clsSb, clsSb.Capacity);
            string cls = clsSb.ToString();

            if (cls == "IME" || cls.StartsWith("MSCTFIME", StringComparison.OrdinalIgnoreCase) ||
                cls.Contains("GDI+ Hook", StringComparison.OrdinalIgnoreCase) ||
                cls == "tooltips_class32")
            {
                return true;
            }

            var titleSb = new StringBuilder(256);
            GetWindowText(hWnd, titleSb, titleSb.Capacity);
            if (titleSb.Length > 0 && bestWithTitle == IntPtr.Zero)
            {
                bestWithTitle = hWnd;
            }
            else if (fallbackWindow == IntPtr.Zero)
            {
                fallbackWindow = hWnd;
            }
            return true;
        }, IntPtr.Zero);

        return bestWithTitle != IntPtr.Zero ? bestWithTitle : fallbackWindow;
    }

    private static string ResolveKnownFolderPath(string rawPath)
    {
        var m = KnownFolderPrefixRegex.Match(rawPath.Trim());
        if (!m.Success) return Environment.ExpandEnvironmentVariables(rawPath);

        if (Guid.TryParse(m.Groups[1].Value, out Guid folderGuid))
        {
            int hr = SHGetKnownFolderPath(ref folderGuid, 0, IntPtr.Zero, out IntPtr pPath);
            if (hr == 0 && pPath != IntPtr.Zero)
            {
                try
                {
                    string? baseDir = Marshal.PtrToStringUni(pPath);
                    if (!string.IsNullOrWhiteSpace(baseDir))
                    {
                        return Path.Combine(baseDir, m.Groups[2].Value);
                    }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pPath);
                }
            }
        }
        return rawPath;
    }

    private static string TryGetProcessExePath(Process proc)
    {
        try
        {
            return proc.MainModule?.FileName ?? "";
        }
        catch
        {
            return TryGetProcessExePath(proc.Id);
        }
    }

    private static string TryGetProcessExePath(int pid)
    {
        IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (hProc == IntPtr.Zero) return "";
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            if (QueryFullProcessImageName(hProc, 0, sb, ref size))
                return sb.ToString();
        }
        catch { }
        finally
        {
            CloseHandle(hProc);
        }
        return "";
    }

    private string GetFileIconDataUrl(string exePath)
    {
        if (_iconCache.TryGetValue(exePath, out string? cached))
            return cached;

        try
        {
            using Icon? icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon != null)
            {
                string url = BitmapToDataUrl(icon.ToBitmap());
                if (!string.IsNullOrEmpty(url))
                    _iconCache[exePath] = url;
                return url;
            }
        }
        catch { }
        return "";
    }

    private static string IconHandleToDataUrl(IntPtr hIcon)
    {
        try
        {
            using Icon icon = Icon.FromHandle(hIcon);
            using Bitmap bmp = icon.ToBitmap();
            return BitmapToDataUrl(bmp);
        }
        catch
        {
            return "";
        }
    }

    private static string BitmapToDataUrl(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
    }
}
