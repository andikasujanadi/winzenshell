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
        bool nativeTaskbarHidden)
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

        if (taskbarVisible && taskbarHwnd != IntPtr.Zero)
        {
            if (respectWorkArea)
            {
                var currentWorkArea = new Win32Api.RECT();
                Win32Api.SystemParametersInfo(Win32Api.SPI_GETWORKAREA, 0, ref currentWorkArea, 0);

                int targetEdgePos = isTop ? (monRect.Top + workAreaFootprint) : (monRect.Bottom - workAreaFootprint);
                bool dockedEdgeMatches = isTop
                    ? currentWorkArea.Top == targetEdgePos
                    : currentWorkArea.Bottom == targetEdgePos;

                // Preserve opposite monitor edge if another taskbar (e.g. native bottom taskbar) reserves it
                int preservedTop = !isTop && !nativeTaskbarHidden && currentWorkArea.Top > monRect.Top
                    ? currentWorkArea.Top
                    : monRect.Top;
                int preservedBottom = isTop && !nativeTaskbarHidden && currentWorkArea.Bottom < monRect.Bottom
                    ? currentWorkArea.Bottom
                    : monRect.Bottom;

                var desiredWorkArea = new Win32Api.RECT
                {
                    Left = monRect.Left,
                    Right = monRect.Right,
                    Top = isTop ? targetEdgePos : preservedTop,
                    Bottom = isTop ? preservedBottom : targetEdgePos
                };

                // Skip redundant AppBar / SPI_SETWORKAREA broadcasts when docked edge already matches
                if (!_isRegistered || _registeredHwnd != taskbarHwnd || !dockedEdgeMatches)
                {
                    RegisterAppBar(taskbarHwnd);

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
                    // Maintain exact requested height on the chosen edge
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

                    Win32Api.SystemParametersInfo(Win32Api.SPI_GETWORKAREA, 0, ref currentWorkArea, 0);
                    bool edgeAfterAppBar = isTop
                        ? currentWorkArea.Top == targetEdgePos
                        : currentWorkArea.Bottom == targetEdgePos;
                    if (!edgeAfterAppBar)
                    {
                        desiredWorkArea.Top = isTop ? targetEdgePos : Math.Max(monRect.Top, currentWorkArea.Top);
                        desiredWorkArea.Bottom = isTop ? Math.Min(monRect.Bottom, currentWorkArea.Bottom) : targetEdgePos;
                        Win32Api.SystemParametersInfo(
                            Win32Api.SPI_SETWORKAREA,
                            0,
                            ref desiredWorkArea,
                            Win32Api.SPIF_SENDCHANGE);
                    }
                }
            }
            else
            {
                // Taskbar is visible as overlay, do not reserve Desktop Working Area
                UnregisterAppBar(taskbarHwnd);
                if (nativeTaskbarHidden)
                {
                    var currentWorkArea = new Win32Api.RECT();
                    Win32Api.SystemParametersInfo(Win32Api.SPI_GETWORKAREA, 0, ref currentWorkArea, 0);
                    if (!RectsEqual(currentWorkArea, monRect))
                    {
                        var fullArea = monRect;
                        Win32Api.SystemParametersInfo(
                            Win32Api.SPI_SETWORKAREA,
                            0,
                            ref fullArea,
                            Win32Api.SPIF_SENDCHANGE);
                    }
                }
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
