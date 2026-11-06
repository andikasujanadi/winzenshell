using System;
using System.Runtime.InteropServices;

namespace WinZenShell.Native;

public class WorkAreaTelemetry
{
    public int MonitorLeft { get; set; }
    public int MonitorTop { get; set; }
    public int MonitorRight { get; set; }
    public int MonitorBottom { get; set; }
    public int MonitorWidth => MonitorRight - MonitorLeft;
    public int MonitorHeight => MonitorBottom - MonitorTop;

    public int WorkAreaLeft { get; set; }
    public int WorkAreaTop { get; set; }
    public int WorkAreaRight { get; set; }
    public int WorkAreaBottom { get; set; }
    public int WorkAreaWidth => WorkAreaRight - WorkAreaLeft;
    public int WorkAreaHeight => WorkAreaBottom - WorkAreaTop;

    public int ReservedTopPx => WorkAreaTop - MonitorTop;
    public int ReservedBottomPx => MonitorBottom - WorkAreaBottom;
    public bool IsAppBarRegistered { get; set; }
    public string DockEdge { get; set; } = "Bottom";
}

public class WorkAreaAppBarService
{
    private bool _isRegistered;
    private int _callbackMessageId;
    private IntPtr _registeredHwnd = IntPtr.Zero;

    public bool IsRegistered => _isRegistered;

    public void RegisterAppBar(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        if (_isRegistered && _registeredHwnd == hwnd) return;

        if (_isRegistered && _registeredHwnd != IntPtr.Zero)
        {
            UnregisterAppBar(_registeredHwnd);
        }

        _callbackMessageId = Win32Api.WM_USER + 101;
        var abd = new Win32Api.APPBARDATA
        {
            cbSize = Marshal.SizeOf<Win32Api.APPBARDATA>(),
            hWnd = hwnd,
            uCallbackMessage = (uint)_callbackMessageId
        };

        Win32Api.SHAppBarMessage(Win32Api.ABM_NEW, ref abd);
        _isRegistered = true;
        _registeredHwnd = hwnd;
    }

    public void UnregisterAppBar(IntPtr hwnd)
    {
        IntPtr target = hwnd != IntPtr.Zero ? hwnd : _registeredHwnd;
        if (_isRegistered && target != IntPtr.Zero)
        {
            var abd = new Win32Api.APPBARDATA
            {
                cbSize = Marshal.SizeOf<Win32Api.APPBARDATA>(),
                hWnd = target
            };
            Win32Api.SHAppBarMessage(Win32Api.ABM_REMOVE, ref abd);
        }
        _isRegistered = false;
        _registeredHwnd = IntPtr.Zero;
    }

    /// <summary>
    /// Positions the custom taskbar window and enforces the Respected Desktop Working Area.
    /// Uses both SHAppBarMessage(ABM_QUERYPOS / ABM_SETPOS) and direct SPI_SETWORKAREA so
    /// maximized windows on Windows 11 always respect the custom taskbar even when Shell_TrayWnd is hidden.
    /// </summary>
    public Win32Api.RECT ApplyTaskbarBoundsAndWorkArea(
        IntPtr taskbarHwnd,
        bool taskbarVisible,
        bool respectWorkArea,
        string dockEdge,
        int taskbarHeightPx,
        int floatingMarginPx,
        bool nativeTaskbarHidden)
    {
        var monitorInfo = GetPrimaryMonitorInfo();
        var monRect = monitorInfo.rcMonitor;

        bool isTop = string.Equals(dockEdge, "Top", StringComparison.OrdinalIgnoreCase);
        int totalBarFootprint = taskbarHeightPx + (floatingMarginPx * 2);

        // Compute physical window bounds for the custom taskbar
        var barRect = new Win32Api.RECT
        {
            Left = monRect.Left + floatingMarginPx,
            Right = monRect.Right - floatingMarginPx,
            Top = isTop ? (monRect.Top + floatingMarginPx) : (monRect.Bottom - taskbarHeightPx - floatingMarginPx),
            Bottom = isTop ? (monRect.Top + taskbarHeightPx + floatingMarginPx) : (monRect.Bottom - floatingMarginPx)
        };

        if (taskbarVisible && taskbarHwnd != IntPtr.Zero)
        {
            if (respectWorkArea)
            {
                RegisterAppBar(taskbarHwnd);

                var abd = new Win32Api.APPBARDATA
                {
                    cbSize = Marshal.SizeOf<Win32Api.APPBARDATA>(),
                    hWnd = taskbarHwnd,
                    uEdge = isTop ? Win32Api.ABE_TOP : Win32Api.ABE_BOTTOM,
                    rc = new Win32Api.RECT(
                        monRect.Left,
                        isTop ? monRect.Top : monRect.Bottom - totalBarFootprint,
                        monRect.Right,
                        isTop ? monRect.Top + totalBarFootprint : monRect.Bottom)
                };

                Win32Api.SHAppBarMessage(Win32Api.ABM_QUERYPOS, ref abd);
                // Maintain exact requested height on the chosen edge
                if (isTop)
                {
                    abd.rc.Top = monRect.Top;
                    abd.rc.Bottom = monRect.Top + totalBarFootprint;
                }
                else
                {
                    abd.rc.Bottom = monRect.Bottom;
                    abd.rc.Top = monRect.Bottom - totalBarFootprint;
                }
                Win32Api.SHAppBarMessage(Win32Api.ABM_SETPOS, ref abd);

                // Also explicitly enforce SPI_SETWORKAREA so Windows 11 respects it even when Shell_TrayWnd is SW_HIDE
                var desiredWorkArea = new Win32Api.RECT
                {
                    Left = monRect.Left,
                    Right = monRect.Right,
                    Top = isTop ? (monRect.Top + totalBarFootprint) : monRect.Top,
                    Bottom = isTop ? monRect.Bottom : (monRect.Bottom - totalBarFootprint)
                };
                Win32Api.SystemParametersInfo(
                    Win32Api.SPI_SETWORKAREA,
                    0,
                    ref desiredWorkArea,
                    Win32Api.SPIF_SENDCHANGE);
            }
            else
            {
                // Taskbar is visible as overlay, do not reserve Desktop Working Area
                UnregisterAppBar(taskbarHwnd);
                if (nativeTaskbarHidden)
                {
                    var fullArea = monRect;
                    Win32Api.SystemParametersInfo(
                        Win32Api.SPI_SETWORKAREA,
                        0,
                        ref fullArea,
                        Win32Api.SPIF_SENDCHANGE);
                }
            }

            // Position the custom taskbar window at top-most z-order
            Win32Api.SetWindowPos(
                taskbarHwnd,
                Win32Api.HWND_TOPMOST,
                barRect.Left,
                barRect.Top,
                barRect.Width,
                barRect.Height,
                Win32Api.SWP_NOACTIVATE | Win32Api.SWP_SHOWWINDOW);
        }
        else
        {
            // Custom taskbar is disabled/hidden
            if (taskbarHwnd != IntPtr.Zero)
            {
                UnregisterAppBar(taskbarHwnd);
            }

            if (nativeTaskbarHidden)
            {
                // Native taskbar is hidden (and set to autohide), and custom taskbar is off -> full monitor working area
                var fullArea = monRect;
                Win32Api.SystemParametersInfo(
                    Win32Api.SPI_SETWORKAREA,
                    0,
                    ref fullArea,
                    Win32Api.SPIF_SENDCHANGE);
            }
        }

        return barRect;
    }

    public WorkAreaTelemetry GetTelemetry(string dockEdge)
    {
        var mi = GetPrimaryMonitorInfo();
        var spiWork = new Win32Api.RECT();
        Win32Api.SystemParametersInfo(Win32Api.SPI_GETWORKAREA, 0, ref spiWork, 0);

        return new WorkAreaTelemetry
        {
            MonitorLeft = mi.rcMonitor.Left,
            MonitorTop = mi.rcMonitor.Top,
            MonitorRight = mi.rcMonitor.Right,
            MonitorBottom = mi.rcMonitor.Bottom,
            WorkAreaLeft = spiWork.Left,
            WorkAreaTop = spiWork.Top,
            WorkAreaRight = spiWork.Right,
            WorkAreaBottom = spiWork.Bottom,
            IsAppBarRegistered = _isRegistered,
            DockEdge = dockEdge
        };
    }

    public static Win32Api.MONITORINFO GetPrimaryMonitorInfo()
    {
        var pt = new Win32Api.POINT { X = 0, Y = 0 };
        IntPtr hMon = Win32Api.MonitorFromPoint(pt, Win32Api.MONITOR_DEFAULTTOPRIMARY);
        var mi = new Win32Api.MONITORINFO
        {
            cbSize = Marshal.SizeOf<Win32Api.MONITORINFO>()
        };
        Win32Api.GetMonitorInfo(hMon, ref mi);
        return mi;
    }
}
