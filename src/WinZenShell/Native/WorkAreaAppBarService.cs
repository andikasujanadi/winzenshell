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
    private bool _isTaskbarAppBarRegistered;
    private IntPtr _taskbarAppBarHwnd = IntPtr.Zero;

    private bool _isTopAppBarRegistered;
    private IntPtr _topAppBarHwnd = IntPtr.Zero;

    public bool IsRegistered => _isTaskbarAppBarRegistered || _isTopAppBarRegistered;

    public void RegisterTaskbarAppBar(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        if (_isTaskbarAppBarRegistered && _taskbarAppBarHwnd == hwnd) return;

        if (_isTaskbarAppBarRegistered && _taskbarAppBarHwnd != IntPtr.Zero)
        {
            UnregisterTaskbarAppBar();
        }

        var abd = new Win32Api.APPBARDATA
        {
            cbSize = Marshal.SizeOf<Win32Api.APPBARDATA>(),
            hWnd = hwnd,
            uCallbackMessage = (uint)(Win32Api.WM_USER + 101)
        };

        Win32Api.SHAppBarMessage(Win32Api.ABM_NEW, ref abd);
        _isTaskbarAppBarRegistered = true;
        _taskbarAppBarHwnd = hwnd;
    }

    public void UnregisterTaskbarAppBar()
    {
        if (_isTaskbarAppBarRegistered && _taskbarAppBarHwnd != IntPtr.Zero)
        {
            var abd = new Win32Api.APPBARDATA
            {
                cbSize = Marshal.SizeOf<Win32Api.APPBARDATA>(),
                hWnd = _taskbarAppBarHwnd
            };
            Win32Api.SHAppBarMessage(Win32Api.ABM_REMOVE, ref abd);
        }
        _isTaskbarAppBarRegistered = false;
        _taskbarAppBarHwnd = IntPtr.Zero;
    }

    public void RegisterTopAppBar(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        if (_isTopAppBarRegistered && _topAppBarHwnd == hwnd) return;

        if (_isTopAppBarRegistered && _topAppBarHwnd != IntPtr.Zero)
        {
            UnregisterTopAppBar();
        }

        var abd = new Win32Api.APPBARDATA
        {
            cbSize = Marshal.SizeOf<Win32Api.APPBARDATA>(),
            hWnd = hwnd,
            uCallbackMessage = (uint)(Win32Api.WM_USER + 102)
        };

        Win32Api.SHAppBarMessage(Win32Api.ABM_NEW, ref abd);
        _isTopAppBarRegistered = true;
        _topAppBarHwnd = hwnd;
    }

    public void UnregisterTopAppBar()
    {
        if (_isTopAppBarRegistered && _topAppBarHwnd != IntPtr.Zero)
        {
            var abd = new Win32Api.APPBARDATA
            {
                cbSize = Marshal.SizeOf<Win32Api.APPBARDATA>(),
                hWnd = _topAppBarHwnd
            };
            Win32Api.SHAppBarMessage(Win32Api.ABM_REMOVE, ref abd);
        }
        _isTopAppBarRegistered = false;
        _topAppBarHwnd = IntPtr.Zero;
    }

    public void RegisterAppBar(IntPtr hwnd)
    {
        RegisterTaskbarAppBar(hwnd);
    }

    public void UnregisterAppBar(IntPtr hwnd)
    {
        if (hwnd == _taskbarAppBarHwnd || hwnd == IntPtr.Zero)
        {
            UnregisterTaskbarAppBar();
        }
        if (hwnd == _topAppBarHwnd || hwnd == IntPtr.Zero)
        {
            UnregisterTopAppBar();
        }
    }

    public void UnregisterAllAppBars()
    {
        UnregisterTaskbarAppBar();
        UnregisterTopAppBar();
    }

    /// <summary>
    /// Positions the custom taskbar window and enforces the Respected Desktop Working Area.
    /// Uses both SHAppBarMessage(ABM_QUERYPOS / ABM_SETPOS) and direct SPI_SETWORKAREA so
    /// maximized windows on Windows 11 always respect the custom taskbar and Dynamic Island work area reservation.
    /// </summary>
    private static bool RectsEqual(Win32Api.RECT a, Win32Api.RECT b)
    {
        return a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
    }

    public Win32Api.RECT ApplyTaskbarBoundsAndWorkArea(
        IntPtr taskbarHwnd,
        bool taskbarVisible,
        bool respectWorkArea,
        string dockEdge,
        int taskbarHeightPx,
        int floatingMarginPx,
        bool nativeTaskbarHidden,
        int topReservedWorkAreaPx = 0,
        IntPtr topAppBarHwnd = default)
    {
        var monitorInfo = GetPrimaryMonitorInfo();
        var monRect = monitorInfo.rcMonitor;

        bool isTop = string.Equals(dockEdge, "Top", StringComparison.OrdinalIgnoreCase);
        int totalBarFootprint = taskbarHeightPx + (floatingMarginPx * 2);
        // When docked flush (floatingMarginPx == 0), inset WorkArea by 1px less than the topmost bar
        // so maximized windows' 1px DWM top/bottom frame sits underneath the bar with zero wallpaper gap.
        int workAreaFootprint = floatingMarginPx == 0 ? Math.Max(1, totalBarFootprint - 1) : totalBarFootprint;

        // Compute physical window bounds for the custom taskbar
        var barRect = new Win32Api.RECT
        {
            Left = monRect.Left + floatingMarginPx,
            Right = monRect.Right - floatingMarginPx,
            Top = isTop ? (monRect.Top + floatingMarginPx) : (monRect.Bottom - taskbarHeightPx - floatingMarginPx),
            Bottom = isTop ? (monRect.Top + taskbarHeightPx + floatingMarginPx) : (monRect.Bottom - floatingMarginPx)
        };

        // 1. TOP APPBAR (DYNAMIC ISLAND TOP-CENTER WORK AREA CUT)
        if (topReservedWorkAreaPx > 0 && topAppBarHwnd != IntPtr.Zero)
        {
            RegisterTopAppBar(topAppBarHwnd);
            var abdTop = new Win32Api.APPBARDATA
            {
                cbSize = Marshal.SizeOf<Win32Api.APPBARDATA>(),
                hWnd = topAppBarHwnd,
                uEdge = Win32Api.ABE_TOP,
                rc = new Win32Api.RECT(monRect.Left, monRect.Top, monRect.Right, monRect.Top + topReservedWorkAreaPx)
            };
            Win32Api.SHAppBarMessage(Win32Api.ABM_QUERYPOS, ref abdTop);
            abdTop.rc.Top = monRect.Top;
            abdTop.rc.Bottom = monRect.Top + topReservedWorkAreaPx;
            Win32Api.SHAppBarMessage(Win32Api.ABM_SETPOS, ref abdTop);
        }
        else
        {
            UnregisterTopAppBar();
        }

        // 2. BOTTOM / TOP TASKBAR APPBAR
        if (taskbarVisible && taskbarHwnd != IntPtr.Zero)
        {
            if (respectWorkArea)
            {
                RegisterTaskbarAppBar(taskbarHwnd);
                var abd = new Win32Api.APPBARDATA
                {
                    cbSize = Marshal.SizeOf<Win32Api.APPBARDATA>(),
                    hWnd = taskbarHwnd,
                    uEdge = isTop ? Win32Api.ABE_TOP : Win32Api.ABE_BOTTOM,
                    rc = new Win32Api.RECT(
                        monRect.Left,
                        isTop ? monRect.Top : monRect.Bottom - workAreaFootprint,
                        monRect.Right,
                        isTop ? monRect.Top + workAreaFootprint : monRect.Bottom)
                };
                Win32Api.SHAppBarMessage(Win32Api.ABM_QUERYPOS, ref abd);
                if (isTop)
                {
                    abd.rc.Top = monRect.Top;
                    abd.rc.Bottom = monRect.Top + workAreaFootprint;
                }
                else
                {
                    abd.rc.Bottom = monRect.Bottom;
                    abd.rc.Top = monRect.Bottom - workAreaFootprint;
                }
                Win32Api.SHAppBarMessage(Win32Api.ABM_SETPOS, ref abd);
            }
            else
            {
                UnregisterTaskbarAppBar();
            }

            // Position the custom taskbar window at top-most z-order (only move/resize if bounds changed)
            uint swpFlags = Win32Api.SWP_NOACTIVATE | Win32Api.SWP_SHOWWINDOW;
            if (Win32Api.GetWindowRect(taskbarHwnd, out var curWndRect) && RectsEqual(curWndRect, barRect))
            {
                swpFlags |= Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE;
            }
            Win32Api.SetWindowPos(
                taskbarHwnd,
                Win32Api.HWND_TOPMOST,
                barRect.Left,
                barRect.Top,
                barRect.Width,
                barRect.Height,
                swpFlags);
        }
        else
        {
            UnregisterTaskbarAppBar();
        }

        // 3. FINAL DESKTOP WORK AREA ENFORCEMENT
        int targetTop = topReservedWorkAreaPx > 0
            ? (monRect.Top + topReservedWorkAreaPx)
            : (isTop && respectWorkArea && taskbarVisible ? (monRect.Top + workAreaFootprint) : monRect.Top);

        int targetBottom = (!isTop && respectWorkArea && taskbarVisible)
            ? (monRect.Bottom - workAreaFootprint)
            : monRect.Bottom;

        var currentWorkArea = new Win32Api.RECT();
        Win32Api.SystemParametersInfo(Win32Api.SPI_GETWORKAREA, 0, ref currentWorkArea, 0);

        if (currentWorkArea.Top != targetTop || currentWorkArea.Bottom != targetBottom)
        {
            var desiredWorkArea = new Win32Api.RECT(monRect.Left, targetTop, monRect.Right, targetBottom);
            Win32Api.SystemParametersInfo(
                Win32Api.SPI_SETWORKAREA,
                0,
                ref desiredWorkArea,
                Win32Api.SPIF_SENDCHANGE);
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
            IsAppBarRegistered = IsRegistered,
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
