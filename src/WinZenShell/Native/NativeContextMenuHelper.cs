using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace WinZenShell.Native;

public static class NativeContextMenuHelper
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_MOUSEHWHEEL = 0x020E;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int WM_NCRBUTTONDOWN = 0x00A4;
    private const int WM_NCMBUTTONDOWN = 0x00A7;

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private static IntPtr _hookId = IntPtr.Zero;
    private static Forms.ContextMenuStrip? _activeMenu;
    private static LowLevelMouseProc? _hookDelegate;

    public static void AttachAutoDismiss(Forms.ContextMenuStrip menu)
    {
        if (menu == null) return;

        DetachAutoDismiss();

        _activeMenu = menu;
        _hookDelegate = HookCallback;

        IntPtr modHandle = GetModuleHandle(null);
        _hookId = SetWindowsHookEx(WH_MOUSE_LL, _hookDelegate, modHandle, 0);

        menu.Closed += (_, _) =>
        {
            if (_activeMenu == menu)
            {
                DetachAutoDismiss();
            }
        };

        menu.Disposed += (_, _) =>
        {
            if (_activeMenu == menu)
            {
                DetachAutoDismiss();
            }
        };
    }

    public static void DetachAutoDismiss()
    {
        if (_hookId != IntPtr.Zero)
        {
            try { UnhookWindowsHookEx(_hookId); } catch { }
            _hookId = IntPtr.Zero;
        }
        _activeMenu = null;
        _hookDelegate = null;
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _activeMenu != null && _activeMenu.Visible)
        {
            int msg = wParam.ToInt32();
            if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN ||
                msg == WM_MOUSEWHEEL || msg == WM_MOUSEHWHEEL ||
                msg == WM_NCLBUTTONDOWN || msg == WM_NCRBUTTONDOWN || msg == WM_NCMBUTTONDOWN)
            {
                var curPos = Forms.Cursor.Position;
                bool isInside = IsPointInMenuOrSubmenus(_activeMenu, curPos);

                if (!isInside)
                {
                    var menuToClose = _activeMenu;
                    DetachAutoDismiss();
                    menuToClose.BeginInvoke(() =>
                    {
                        try { menuToClose.Close(); } catch { }
                    });
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static bool IsPointInMenuOrSubmenus(Forms.ToolStripDropDown menu, Point pt)
    {
        if (menu == null) return false;
        if (menu.Visible && menu.Bounds.Contains(pt))
            return true;

        foreach (Forms.ToolStripItem item in menu.Items)
        {
            if (item is Forms.ToolStripDropDownItem dropDownItem && dropDownItem.HasDropDownItems && dropDownItem.DropDown.Visible)
            {
                if (IsPointInMenuOrSubmenus(dropDownItem.DropDown, pt))
                    return true;
            }
        }

        return false;
    }
}
