using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinZenShell.Services;

public sealed class HookedTrayIconInfo
{
    public uint ProcessId { get; set; }
    public ulong HWnd { get; set; }
    public uint UId { get; set; }
    public uint CallbackMessage { get; set; }
    public uint Version { get; set; }
    public string ExecutablePath { get; set; } = "";
    public string Tooltip { get; set; } = "";
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
}

public sealed class TrayHookBridge : IDisposable
{
    private const string PipeName = "WinZenTrayHookPipe";
    private const string StopEventName = "Local\\WinZenTrayHookStopEvent";
    private const int PacketSize = 288;

    private readonly ConcurrentDictionary<string, HookedTrayIconInfo> _icons = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _cts = new();
    private readonly EventWaitHandle _stopEvent;
    private bool _started;
    private bool _disposed;

    public event Action? IconsUpdated;

    public TrayHookBridge()
    {
        _stopEvent = new EventWaitHandle(false, EventResetMode.ManualReset, StopEventName);
    }

    public void Start()
    {
        if (_started || _disposed) return;
        _started = true;
        _stopEvent.Reset();

        _ = Task.Run(() => PipeServerLoop(_cts.Token));
        _ = Task.Run(() => InjectIntoExplorerAsync(_cts.Token));
    }

    public HookedTrayIconInfo? FindMatch(uint pid, string? exePath, string? tooltip)
    {
        HookedTrayIconInfo? byPid = null;
        HookedTrayIconInfo? byExe = null;
        string targetExe = (exePath ?? "").Trim();
        string targetTip = (tooltip ?? "").Split('\n')[0].Trim();

        foreach (var kv in _icons.Values)
        {
            if (kv.CallbackMessage == 0 || kv.HWnd == 0) continue;

            if (pid > 0 && kv.ProcessId == pid)
            {
                if (!string.IsNullOrEmpty(targetTip) &&
                    !string.IsNullOrEmpty(kv.Tooltip) &&
                    kv.Tooltip.Contains(targetTip, StringComparison.OrdinalIgnoreCase))
                {
                    return kv;
                }
                byPid ??= kv;
            }

            if (!string.IsNullOrEmpty(targetExe) &&
                !string.IsNullOrEmpty(kv.ExecutablePath) &&
                string.Equals(kv.ExecutablePath, targetExe, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(targetTip) &&
                    !string.IsNullOrEmpty(kv.Tooltip) &&
                    kv.Tooltip.Contains(targetTip, StringComparison.OrdinalIgnoreCase))
                {
                    return kv;
                }
                byExe ??= kv;
            }
        }

        return byPid ?? byExe;
    }

    private async Task PipeServerLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                byte[] buf = new byte[PacketSize];
                while (server.IsConnected && !ct.IsCancellationRequested)
                {
                    int read = 0;
                    while (read < PacketSize)
                    {
                        int n = await server.ReadAsync(buf.AsMemory(read, PacketSize - read), ct).ConfigureAwait(false);
                        if (n <= 0) break;
                        read += n;
                    }
                    if (read < PacketSize) break;

                    ProcessPacket(buf);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(250, ct).ConfigureAwait(false);
            }
        }
    }

    private void ProcessPacket(byte[] buf)
    {
        uint dwMessage = BitConverter.ToUInt32(buf, 0);
        uint pid = BitConverter.ToUInt32(buf, 4);
        ulong hwnd = BitConverter.ToUInt64(buf, 8);
        uint uid = BitConverter.ToUInt32(buf, 16);
        uint uFlags = BitConverter.ToUInt32(buf, 20);
        uint callbackMsg = BitConverter.ToUInt32(buf, 24);
        uint uVersion = BitConverter.ToUInt32(buf, 28);
        string tip = Encoding.Unicode.GetString(buf, 32, 256).TrimEnd('\0');

        if (hwnd == 0) return;
        string key = $"{hwnd}:{uid}";

        if (dwMessage == 2 /* NIM_DELETE */)
        {
            if (_icons.TryRemove(key, out _))
            {
                IconsUpdated?.Invoke();
            }
            return;
        }

        var existing = _icons.GetOrAdd(key, _ => new HookedTrayIconInfo
        {
            HWnd = hwnd,
            UId = uid
        });

        if (pid > 0)
        {
            existing.ProcessId = pid;
            if (string.IsNullOrEmpty(existing.ExecutablePath))
            {
                existing.ExecutablePath = TryGetProcessPath((int)pid);
            }
        }
        if ((uFlags & 0x01 /* NIF_MESSAGE */) != 0 && callbackMsg != 0)
        {
            existing.CallbackMessage = callbackMsg;
        }
        if ((uFlags & 0x04 /* NIF_TIP */) != 0 && !string.IsNullOrWhiteSpace(tip))
        {
            existing.Tooltip = tip;
        }
        if (dwMessage == 4 /* NIM_SETVERSION */)
        {
            existing.Version = uVersion;
        }
        existing.LastSeenUtc = DateTime.UtcNow;

        IconsUpdated?.Invoke();
    }

    private static string TryGetProcessPath(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            return proc.MainModule?.FileName ?? "";
        }
        catch
        {
            return "";
        }
    }

    private async Task InjectIntoExplorerAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(300, ct).ConfigureAwait(false);
            string? dllPath = EnsureHookDllExtracted();
            if (string.IsNullOrEmpty(dllPath) || !File.Exists(dllPath)) return;

            var explorers = Process.GetProcessesByName("explorer");
            if (explorers.Length == 0) return;

            // Find the explorer process that owns Shell_TrayWnd
            IntPtr hTray = FindWindowW("Shell_TrayWnd", null);
            uint targetPid = 0;
            if (hTray != IntPtr.Zero)
            {
                GetWindowThreadProcessId(hTray, out targetPid);
            }
            if (targetPid == 0)
            {
                targetPid = (uint)explorers[0].Id;
            }

            bool injected = InjectDll(targetPid, dllPath);
            if (injected)
            {
                await Task.Delay(250, ct).ConfigureAwait(false);
                // Broadcast TaskbarCreated so all running tray apps re-register their icon metadata
                uint msgTaskbarCreated = RegisterWindowMessageW("TaskbarCreated");
                if (msgTaskbarCreated != 0)
                {
                    SendNotifyMessageW(new IntPtr(0xFFFF /* HWND_BROADCAST */), msgTaskbarCreated, IntPtr.Zero, IntPtr.Zero);
                }
            }
        }
        catch { }
    }

    private static string? EnsureHookDllExtracted()
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinZenShell");
            Directory.CreateDirectory(dir);
            string targetPath = Path.Combine(dir, "WinZenTrayHook.dll");

            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WinZenShell.Native.WinZenTrayHook.dll");
            if (stream != null)
            {
                bool shouldWrite = !File.Exists(targetPath) || new FileInfo(targetPath).Length != stream.Length;
                if (shouldWrite)
                {
                    try
                    {
                        using var fs = File.Create(targetPath);
                        stream.CopyTo(fs);
                    }
                    catch
                    {
                        // If explorer still holds the old DLL, write a versioned copy
                        string fallback = Path.Combine(dir, $"WinZenTrayHook_{Process.GetCurrentProcess().Id}.dll");
                        stream.Position = 0;
                        using var fs = File.Create(fallback);
                        stream.CopyTo(fs);
                        return fallback;
                    }
                }
                return targetPath;
            }

            string localPath = Path.Combine(AppContext.BaseDirectory, "WinZenTrayHook.dll");
            if (File.Exists(localPath)) return localPath;
        }
        catch { }
        return null;
    }

    private static bool InjectDll(uint pid, string dllPath)
    {
        const uint PROCESS_CREATE_THREAD = 0x0002;
        const uint PROCESS_QUERY_INFORMATION = 0x0400;
        const uint PROCESS_VM_OPERATION = 0x0008;
        const uint PROCESS_VM_WRITE = 0x0020;
        const uint PROCESS_VM_READ = 0x0010;
        const uint MEM_COMMIT = 0x1000;
        const uint MEM_RESERVE = 0x2000;
        const uint MEM_RELEASE = 0x8000;
        const uint PAGE_READWRITE = 0x04;

        IntPtr hProc = OpenProcess(
            PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
            false,
            pid);
        if (hProc == IntPtr.Zero) return false;

        IntPtr remoteMem = IntPtr.Zero;
        IntPtr hThread = IntPtr.Zero;
        try
        {
            byte[] pathBytes = Encoding.Unicode.GetBytes(dllPath + "\0");
            remoteMem = VirtualAllocEx(hProc, IntPtr.Zero, (UIntPtr)pathBytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (remoteMem == IntPtr.Zero) return false;

            if (!WriteProcessMemory(hProc, remoteMem, pathBytes, (UIntPtr)pathBytes.Length, out _))
                return false;

            IntPtr hKernel32 = GetModuleHandleW("kernel32.dll");
            IntPtr pLoadLibraryW = GetProcAddress(hKernel32, "LoadLibraryW");
            if (pLoadLibraryW == IntPtr.Zero) return false;

            hThread = CreateRemoteThread(hProc, IntPtr.Zero, UIntPtr.Zero, pLoadLibraryW, remoteMem, 0, out _);
            if (hThread == IntPtr.Zero) return false;

            WaitForSingleObject(hThread, 3000);
            return true;
        }
        finally
        {
            if (hThread != IntPtr.Zero) CloseHandle(hThread);
            if (remoteMem != IntPtr.Zero) VirtualFreeEx(hProc, remoteMem, UIntPtr.Zero, MEM_RELEASE);
            CloseHandle(hProc);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        try { _stopEvent.Set(); } catch { }
        try { _stopEvent.Dispose(); } catch { }
        _cts.Dispose();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowW(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessageW(string lpString);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SendNotifyMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, UIntPtr nSize, out UIntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, UIntPtr dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);
}
