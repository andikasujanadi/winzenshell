using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinZenShell.Native;

public sealed class WinKeyHookService : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const uint VK_LWIN = 0x5B;
    private const uint VK_RWIN = 0x5C;
    private const ushort VK_DUMMY = 0xFF;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private static readonly IntPtr InjectedMarker = new(0x575A4E53);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _winKeyDown;
    private bool _otherKeyPressed;

    public event Action? WinKeySoloPressed;

    public WinKeyHookService()
    {
        _proc = HookCallback;
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled && _hookId == IntPtr.Zero)
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            IntPtr hMod = curModule != null ? GetModuleHandle(curModule.ModuleName) : IntPtr.Zero;
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, hMod, 0);
            _winKeyDown = false;
            _otherKeyPressed = false;
        }
        else if (!enabled && _hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _winKeyDown = false;
            _otherKeyPressed = false;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (hookStruct.dwExtraInfo == InjectedMarker)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            int msg = wParam.ToInt32();
            bool isKeyDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            bool isKeyUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;
            bool isWinKey = hookStruct.vkCode == VK_LWIN || hookStruct.vkCode == VK_RWIN;

            if (isWinKey)
            {
                if (isKeyDown)
                {
                    if (!_winKeyDown)
                    {
                        _winKeyDown = true;
                        _otherKeyPressed = false;
                    }
                }
                else if (isKeyUp)
                {
                    bool trigger = _winKeyDown && !_otherKeyPressed;
                    _winKeyDown = false;
                    _otherKeyPressed = false;

                    if (trigger)
                    {
                        // Send a harmless unassigned keystroke so Windows Explorer suppresses the native Start Menu
                        // while allowing the Windows key itself to release cleanly.
                        InjectDummyKey();
                        WinKeySoloPressed?.Invoke();
                    }
                }
            }
            else if (_winKeyDown && isKeyDown)
            {
                _otherKeyPressed = true;
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static void InjectDummyKey()
    {
        var inputs = new INPUT[2];
        inputs[0] = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = VK_DUMMY,
                    dwFlags = 0,
                    dwExtraInfo = InjectedMarker
                }
            }
        };
        inputs[1] = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = VK_DUMMY,
                    dwFlags = KEYEVENTF_KEYUP,
                    dwExtraInfo = InjectedMarker
                }
            }
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    public void Dispose()
    {
        SetEnabled(false);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}
