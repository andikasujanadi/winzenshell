using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace WinZenShell.Services;

public class BluetoothDeviceItem
{
    public string Name { get; set; } = "";
    public ulong Address { get; set; } = 0;
    public bool IsConnected { get; set; } = false;
    public bool IsRemembered { get; set; } = false;
    public uint ClassOfDevice { get; set; } = 0;
    public string DeviceType { get; set; } = "device"; // "audio", "phone", "computer", "peripheral", "device"
}

public class BluetoothStatusInfo
{
    public bool IsAvailable { get; set; } = false;
    public bool IsEnabled { get; set; } = false;
    public bool IsConnected { get; set; } = false;
    public string ConnectedDeviceName { get; set; } = "";
    public string StatusText { get; set; } = "Off";
    public string ComputerName { get; set; } = Environment.MachineName;
    public List<string> ConnectedDevices { get; set; } = new();
    public List<string> PairedDevices { get; set; } = new();
    public List<BluetoothDeviceItem> DeviceItems { get; set; } = new();
}

public class BluetoothService
{
    private readonly object _lock = new();
    private BluetoothStatusInfo _cachedStatus = new();
    private DateTime _lastStatusFetch = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(1.5);

    public event EventHandler? BluetoothChanged;

    [StructLayout(LayoutKind.Sequential)]
    private struct BLUETOOTH_FIND_RADIO_PARAMS
    {
        public uint dwSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BLUETOOTH_RADIO_INFO
    {
        public uint dwSize;
        public ulong address;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
        public string szName;
        public uint ulClassofDevice;
        public ushort lmpSubversion;
        public ushort manufacturer;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BLUETOOTH_DEVICE_INFO
    {
        public uint dwSize;
        public ulong Address;
        public uint ulClassofDevice;
        [MarshalAs(UnmanagedType.Bool)] public bool fConnected;
        [MarshalAs(UnmanagedType.Bool)] public bool fRemembered;
        [MarshalAs(UnmanagedType.Bool)] public bool fAuthenticated;
        public SYSTEMTIME stLastSeen;
        public SYSTEMTIME stLastUsed;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
        public string szName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEMTIME
    {
        public ushort wYear, wMonth, wDayOfWeek, wDay, wHour, wMinute, wSecond, wMilliseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BLUETOOTH_DEVICE_SEARCH_PARAMS
    {
        public uint dwSize;
        [MarshalAs(UnmanagedType.Bool)] public bool fReturnAuthenticated;
        [MarshalAs(UnmanagedType.Bool)] public bool fReturnRemembered;
        [MarshalAs(UnmanagedType.Bool)] public bool fReturnUnknown;
        [MarshalAs(UnmanagedType.Bool)] public bool fReturnConnected;
        [MarshalAs(UnmanagedType.Bool)] public bool fIssueInquiry;
        public byte cTimeoutMultiplier;
        public IntPtr hRadio;
    }

    [DllImport("bthprops.cpl", SetLastError = true)]
    private static extern IntPtr BluetoothFindFirstRadio(ref BLUETOOTH_FIND_RADIO_PARAMS pbtfrp, out IntPtr phRadio);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindRadioClose(IntPtr hFind);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("bthprops.cpl", SetLastError = true)]
    private static extern uint BluetoothGetRadioInfo(IntPtr hRadio, ref BLUETOOTH_RADIO_INFO pRadioInfo);

    [DllImport("bthprops.cpl", SetLastError = true)]
    private static extern IntPtr BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS pbtsp, ref BLUETOOTH_DEVICE_INFO pbtdi);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindNextDevice(IntPtr hFind, ref BLUETOOTH_DEVICE_INFO pbtdi);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindDeviceClose(IntPtr hFind);

    public BluetoothStatusInfo GetBluetoothStatus()
    {
        lock (_lock)
        {
            if (DateTime.UtcNow - _lastStatusFetch < CacheDuration && _cachedStatus != null)
            {
                return _cachedStatus;
            }

            var info = new BluetoothStatusInfo();

            try
            {
                var radioParams = new BLUETOOTH_FIND_RADIO_PARAMS();
                radioParams.dwSize = (uint)Marshal.SizeOf(radioParams);

                IntPtr hRadio = IntPtr.Zero;
                IntPtr hFindRadio = BluetoothFindFirstRadio(ref radioParams, out hRadio);

                if (hFindRadio != IntPtr.Zero)
                {
                    info.IsAvailable = true;
                    info.IsEnabled = true;

                    try
                    {
                        var radioInfo = new BLUETOOTH_RADIO_INFO();
                        radioInfo.dwSize = (uint)Marshal.SizeOf(radioInfo);
                        if (BluetoothGetRadioInfo(hRadio, ref radioInfo) == 0 && !string.IsNullOrWhiteSpace(radioInfo.szName))
                        {
                            info.ComputerName = radioInfo.szName;
                        }

                        var searchParams = new BLUETOOTH_DEVICE_SEARCH_PARAMS();
                        searchParams.dwSize = (uint)Marshal.SizeOf(searchParams);
                        searchParams.fReturnAuthenticated = true;
                        searchParams.fReturnRemembered = true;
                        searchParams.fReturnConnected = true;
                        searchParams.fReturnUnknown = false;
                        searchParams.fIssueInquiry = false;
                        searchParams.cTimeoutMultiplier = 1;
                        searchParams.hRadio = hRadio;

                        var devInfo = new BLUETOOTH_DEVICE_INFO();
                        devInfo.dwSize = (uint)Marshal.SizeOf(devInfo);

                        IntPtr hFindDev = BluetoothFindFirstDevice(ref searchParams, ref devInfo);
                        if (hFindDev != IntPtr.Zero)
                        {
                            try
                            {
                                do
                                {
                                    string name = devInfo.szName?.Trim() ?? "";
                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        uint majorClass = (devInfo.ulClassofDevice & 0x1F00);
                                        string devType = majorClass switch
                                        {
                                            0x0400 => "audio",
                                            0x0200 => "phone",
                                            0x0100 => "computer",
                                            0x0500 => "peripheral",
                                            0x0600 => "imaging",
                                            0x0700 => "wearable",
                                            _ => "device"
                                        };

                                        var item = new BluetoothDeviceItem
                                        {
                                            Name = name,
                                            Address = devInfo.Address,
                                            IsConnected = devInfo.fConnected,
                                            IsRemembered = devInfo.fRemembered,
                                            ClassOfDevice = devInfo.ulClassofDevice,
                                            DeviceType = devType
                                        };
                                        info.DeviceItems.Add(item);
                                        info.PairedDevices.Add(name);

                                        if (devInfo.fConnected)
                                        {
                                            info.IsConnected = true;
                                            info.ConnectedDevices.Add(name);
                                            if (string.IsNullOrEmpty(info.ConnectedDeviceName))
                                            {
                                                info.ConnectedDeviceName = name;
                                            }
                                        }
                                    }
                                } while (BluetoothFindNextDevice(hFindDev, ref devInfo));
                            }
                            finally
                            {
                                BluetoothFindDeviceClose(hFindDev);
                            }
                        }
                    }
                    finally
                    {
                        BluetoothFindRadioClose(hFindRadio);
                        if (hRadio != IntPtr.Zero)
                        {
                            CloseHandle(hRadio);
                        }
                    }

                    if (info.IsConnected)
                    {
                        info.StatusText = !string.IsNullOrEmpty(info.ConnectedDeviceName)
                            ? info.ConnectedDeviceName
                            : "Connected";
                    }
                    else
                    {
                        info.StatusText = "On";
                    }
                }
                else
                {
                    info.IsAvailable = CheckBluetoothHardwarePresent();
                    info.IsEnabled = false;
                    info.IsConnected = false;
                    info.StatusText = info.IsAvailable ? "Off" : "Not available";
                }
            }
            catch
            {
                info.IsAvailable = false;
                info.IsEnabled = false;
                info.StatusText = "Off";
            }

            _cachedStatus = info;
            _lastStatusFetch = DateTime.UtcNow;
            return info;
        }
    }

    private static bool CheckBluetoothHardwarePresent()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Devices");
            if (key != null) return true;
            using var enumKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\BTH");
            if (enumKey != null && enumKey.SubKeyCount > 0) return true;
        }
        catch { }
        return false;
    }

    public static void OpenWindowsBluetoothSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        }
        catch { }
    }

    public static void OpenWindowsAddBluetoothDevice()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:bluetooth-add-device") { UseShellExecute = true });
        }
        catch { }
    }

    public static void ConnectDevice(string name)
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        }
        catch { }
    }

    public async Task<bool> ToggleBluetoothAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                string script = @"
try {
    Add-Type -AssemblyName System.Runtime.WindowsRuntime
    $asTaskGeneric = [System.WindowsRuntimeSystemExtensions].GetMethods() | ? { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' }
    [Windows.Devices.Radios.Radio, Windows.System.Devices, ContentType = WindowsRuntime] | Out-Null
    $task = $asTaskGeneric.MakeGenericMethod([System.Collections.Generic.IReadOnlyList[Windows.Devices.Radios.Radio]]).Invoke($null, @([Windows.Devices.Radios.Radio]::GetRadiosAsync()))
    $task.Wait(2000)
    $bt = $task.Result | ? { $_.Kind -eq 'Bluetooth' }
    if ($bt) {
        $targetState = if ($bt.State -eq 'On') { [Windows.Devices.Radios.RadioState]::Off } else { [Windows.Devices.Radios.RadioState]::On }
        $setTask = $asTaskGeneric.MakeGenericMethod([Windows.Devices.Radios.RadioAccessStatus]).Invoke($null, @($bt.SetStateAsync($targetState)))
        $setTask.Wait(2000)
    }
} catch { }";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(3500);
            }
            catch { }

            lock (_lock)
            {
                _lastStatusFetch = DateTime.MinValue;
            }

            BluetoothChanged?.Invoke(this, EventArgs.Empty);
            return GetBluetoothStatus().IsEnabled;
        });
    }
}
