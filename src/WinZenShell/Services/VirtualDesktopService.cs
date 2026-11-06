using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace WinZenShell.Services;

public class VirtualDesktopInfo
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsCurrent { get; set; }
    public string? Wallpaper { get; set; }
}

public class VirtualDesktopService
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const byte VK_LWIN = 0x5B;
    private const byte VK_CONTROL = 0x11;
    private const byte VK_TAB = 0x09;
    private const byte VK_LEFT = 0x25;
    private const byte VK_RIGHT = 0x27;
    private const byte VK_D = 0x44;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public List<VirtualDesktopInfo> GetDesktops()
    {
        var list = new List<VirtualDesktopInfo>();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops");
            if (key != null)
            {
                byte[]? ids = key.GetValue("VirtualDesktopIDs") as byte[];
                byte[]? cur = key.GetValue("CurrentVirtualDesktop") as byte[];
                Guid currentGuid = (cur != null && cur.Length == 16) ? new Guid(cur) : Guid.Empty;

                if (ids != null && ids.Length >= 16)
                {
                    int count = ids.Length / 16;
                    for (int i = 0; i < count; i++)
                    {
                        byte[] guidBytes = new byte[16];
                        Buffer.BlockCopy(ids, i * 16, guidBytes, 0, 16);
                        var g = new Guid(guidBytes);
                        string gStr = g.ToString("B").ToUpperInvariant();

                        string name = "";
                        string? wallpaper = null;
                        using (var dKey = key.OpenSubKey(@"Desktops\" + gStr))
                        {
                            if (dKey != null)
                            {
                                name = dKey.GetValue("Name") as string ?? "";
                                wallpaper = dKey.GetValue("Wallpaper") as string;
                            }
                        }
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            name = $"Desktop {i + 1}";
                        }

                        list.Add(new VirtualDesktopInfo
                        {
                            Index = i,
                            Id = g.ToString(),
                            Name = name,
                            IsCurrent = (g == currentGuid),
                            Wallpaper = wallpaper
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading VirtualDesktops registry: {ex.Message}");
        }

        if (list.Count == 0)
        {
            list.Add(new VirtualDesktopInfo
            {
                Index = 0,
                Id = Guid.Empty.ToString(),
                Name = "Desktop 1",
                IsCurrent = true
            });
        }

        if (!list.Any(d => d.IsCurrent))
        {
            list[0].IsCurrent = true;
        }

        return list;
    }

    public void SwitchToDesktop(int targetIndex)
    {
        var list = GetDesktops();
        int currentIdx = list.FindIndex(d => d.IsCurrent);
        if (currentIdx < 0 || targetIndex < 0 || targetIndex >= list.Count || currentIdx == targetIndex)
            return;

        int diff = targetIndex - currentIdx;
        Task.Run(() =>
        {
            if (diff > 0)
            {
                for (int i = 0; i < diff; i++)
                {
                    SendKeyCombo(VK_RIGHT);
                    Thread.Sleep(30);
                }
            }
            else
            {
                int steps = Math.Abs(diff);
                for (int i = 0; i < steps; i++)
                {
                    SendKeyCombo(VK_LEFT);
                    Thread.Sleep(30);
                }
            }
        });
    }

    public void NextDesktop()
    {
        Task.Run(() => SendKeyCombo(VK_RIGHT));
    }

    public void PreviousDesktop()
    {
        Task.Run(() => SendKeyCombo(VK_LEFT));
    }

    public void CreateNewDesktop()
    {
        Task.Run(() =>
        {
            keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_D, 0, 0, UIntPtr.Zero);

            keybd_event(VK_D, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        });
    }

    public void OpenTaskView()
    {
        Task.Run(() =>
        {
            keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event(VK_TAB, 0, 0, UIntPtr.Zero);

            keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        });
    }

    private static void SendKeyCombo(byte arrowKey)
    {
        keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(arrowKey, 0, 0, UIntPtr.Zero);

        keybd_event(arrowKey, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}
