using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WinZenShell.Native;

public class NativeTaskbarStatus
{
    public bool IsHidden { get; set; }
    public bool IsAutoHideEnabled { get; set; }
    public string PrimaryHandleHex { get; set; } = "0x0";
    public int SecondaryCount { get; set; }
    public string LastActionLog { get; set; } = "Initialized";
}

public class NativeTaskbarService
{
    private const string PrimaryTrayClass = "Shell_TrayWnd";
    private const string SecondaryTrayClass = "Shell_SecondaryTrayWnd";
    private const uint EVENT_OBJECT_SHOW = 0x8002;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    private const int OBJID_WINDOW = 0;

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    private bool _isHiddenByUs;
    private string _lastActionLog = "Ready";
    private IntPtr _showHook = IntPtr.Zero;
    private WinEventDelegate? _winEventDelegate;
    private DispatcherTimer? _guardTimer;

    public bool IsHiddenByUs => _isHiddenByUs;

    private void StartHideGuard()
    {
        if (_showHook == IntPtr.Zero)
        {
            _winEventDelegate ??= OnWinEvent;
            _showHook = SetWinEventHook(
                EVENT_OBJECT_SHOW, EVENT_OBJECT_SHOW, IntPtr.Zero,
                _winEventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        }

        if (_guardTimer == null)
        {
            _guardTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _guardTimer.Tick += (_, _) => EnforceHiddenIfNeeded();
        }
        if (!_guardTimer.IsEnabled)
        {
            _guardTimer.Start();
        }
    }

    private void StopHideGuard()
    {
        if (_guardTimer != null && _guardTimer.IsEnabled)
        {
            _guardTimer.Stop();
        }
        if (_showHook != IntPtr.Zero)
        {
            UnhookWinEvent(_showHook);
            _showHook = IntPtr.Zero;
        }
    }

    private void OnWinEvent(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (!_isHiddenByUs || idObject != OBJID_WINDOW || hwnd == IntPtr.Zero) return;

        var sb = new StringBuilder(64);
        if (GetClassName(hwnd, sb, sb.Capacity) > 0)
        {
            string cls = sb.ToString();
            if (cls == PrimaryTrayClass || cls == SecondaryTrayClass)
            {
                Win32Api.ShowWindow(hwnd, Win32Api.SW_HIDE);
            }
        }
    }

    private void EnforceHiddenIfNeeded()
    {
        if (!_isHiddenByUs) return;

        IntPtr trayHwnd = Win32Api.FindWindow(PrimaryTrayClass, null);
        if (trayHwnd != IntPtr.Zero && Win32Api.IsWindowVisible(trayHwnd))
        {
            Win32Api.ShowWindow(trayHwnd, Win32Api.SW_HIDE);
        }

        foreach (IntPtr secHwnd in GetSecondaryTaskbarHandles())
        {
            if (Win32Api.IsWindowVisible(secHwnd))
            {
                Win32Api.ShowWindow(secHwnd, Win32Api.SW_HIDE);
            }
        }
    }

    /// <summary>
    /// Hides the Windows 11 native taskbar using the Autohide-First sequence:
    /// 1. Sets ABM_SETSTATE -> ABS_AUTOHIDE so Windows 11 removes the native taskbar's reserved working area.
    /// 2. Waits briefly for Explorer to release the working area bounds.
    /// 3. Hides Shell_TrayWnd and all Shell_SecondaryTrayWnd windows via ShowWindow(SW_HIDE).
    /// </summary>
    public async Task<NativeTaskbarStatus> HideNativeTaskbarAsync(Action<string>? onStepProgress = null)
    {
        IntPtr trayHwnd = Win32Api.FindWindow(PrimaryTrayClass, null);
        if (trayHwnd == IntPtr.Zero)
        {
            _lastActionLog = "Shell_TrayWnd not found.";
            return GetCurrentStatus();
        }

        // Step 1: Set native taskbar to Auto-Hide first so its Desktop Working Area is reclaimed by Windows
        onStepProgress?.Invoke("Step 1/2: Setting Native Taskbar to Auto-Hide (releasing native working area)...");
        SetNativeTaskbarAutoHide(trayHwnd, enableAutoHide: true);

        // Give explorer.exe time to process ABM_SETSTATE and expand SPI_GETWORKAREA to full monitor bounds
        await Task.Delay(180);

        // Step 2: Hide primary and secondary native taskbar windows
        onStepProgress?.Invoke("Step 2/2: Concealing Shell_TrayWnd & secondary taskbar windows (SW_HIDE)...");
        Win32Api.ShowWindow(trayHwnd, Win32Api.SW_HIDE);

        foreach (IntPtr secHwnd in GetSecondaryTaskbarHandles())
        {
            Win32Api.ShowWindow(secHwnd, Win32Api.SW_HIDE);
        }

        _isHiddenByUs = true;
        StartHideGuard();
        _lastActionLog = "Hidden (Auto-Hide applied first -> Working Area cleared -> SW_HIDE applied)";
        onStepProgress?.Invoke(_lastActionLog);

        return GetCurrentStatus();
    }

    /// <summary>
    /// Restores the Windows 11 native taskbar safely:
    /// 1. Shows Shell_TrayWnd and all Shell_SecondaryTrayWnd windows (SW_SHOW).
    /// 2. Restores ABM_SETSTATE -> ABS_ALWAYSONTOP so native taskbar docks normally again.
    /// </summary>
    public NativeTaskbarStatus RestoreNativeTaskbar()
    {
        _isHiddenByUs = false;
        StopHideGuard();

        IntPtr trayHwnd = Win32Api.FindWindow(PrimaryTrayClass, null);
        if (trayHwnd != IntPtr.Zero)
        {
            // Step 1: Make native taskbar windows visible again
            Win32Api.ShowWindow(trayHwnd, Win32Api.SW_SHOW);

            foreach (IntPtr secHwnd in GetSecondaryTaskbarHandles())
            {
                Win32Api.ShowWindow(secHwnd, Win32Api.SW_SHOW);
            }

            // Step 2: Restore standard docked state (disable autohide so native working area is restored)
            SetNativeTaskbarAutoHide(trayHwnd, enableAutoHide: false);
        }

        _lastActionLog = "Restored (SW_SHOW -> Standard Docked WorkArea restored)";
        return GetCurrentStatus();
    }

    public void SetNativeTaskbarAutoHide(IntPtr trayHwnd, bool enableAutoHide)
    {
        var abd = new Win32Api.APPBARDATA
        {
            cbSize = Marshal.SizeOf<Win32Api.APPBARDATA>(),
            hWnd = trayHwnd,
            lParam = new IntPtr(enableAutoHide ? Win32Api.ABS_AUTOHIDE : Win32Api.ABS_ALWAYSONTOP)
        };
        Win32Api.SHAppBarMessage(Win32Api.ABM_SETSTATE, ref abd);
    }

    public bool GetNativeTaskbarAutoHideState(IntPtr trayHwnd)
    {
        var abd = new Win32Api.APPBARDATA
        {
            cbSize = Marshal.SizeOf<Win32Api.APPBARDATA>(),
            hWnd = trayHwnd
        };
        UIntPtr state = Win32Api.SHAppBarMessage(Win32Api.ABM_GETSTATE, ref abd);
        return ((int)state.ToUInt32() & Win32Api.ABS_AUTOHIDE) != 0;
    }

    public List<IntPtr> GetSecondaryTaskbarHandles()
    {
        var list = new List<IntPtr>();
        IntPtr current = IntPtr.Zero;
        while (true)
        {
            current = Win32Api.FindWindowEx(IntPtr.Zero, current, SecondaryTrayClass, null);
            if (current == IntPtr.Zero)
                break;
            list.Add(current);
        }
        return list;
    }

    public NativeTaskbarStatus GetCurrentStatus()
    {
        IntPtr trayHwnd = Win32Api.FindWindow(PrimaryTrayClass, null);
        bool visible = trayHwnd != IntPtr.Zero && Win32Api.IsWindowVisible(trayHwnd);
        bool autoHide = trayHwnd != IntPtr.Zero && GetNativeTaskbarAutoHideState(trayHwnd);
        var secondaries = GetSecondaryTaskbarHandles();

        return new NativeTaskbarStatus
        {
            IsHidden = !visible,
            IsAutoHideEnabled = autoHide,
            PrimaryHandleHex = $"0x{trayHwnd.ToInt64():X}",
            SecondaryCount = secondaries.Count,
            LastActionLog = _lastActionLog
        };
    }
}
