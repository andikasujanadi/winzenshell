using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Management.Infrastructure;

namespace WinZenShell.Services;

public class BrightnessService : IDisposable
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwNewBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetMonitorBrightness(IntPtr hMonitor, out uint pdwMinimumBrightness, out uint pdwCurrentBrightness, out uint pdwMaximumBrightness);

    private int _cachedBrightness = 100;
    private int _targetBrightness = 100;
    private volatile bool _isApplyingHardware;
    private readonly object _wmiLock = new();
    private bool _initialized;
    private readonly object _lock = new();

    private CimSession? _cimSession;
    private readonly object _sessionLock = new();

    public int CurrentBrightness => _cachedBrightness;
    public event Action<int>? BrightnessChanged;

    public BrightnessService()
    {
        Task.Run(InitializeBrightness);
        StartKeyboardBrightnessMonitor();
    }

    private CimSession? GetCimSession()
    {
        lock (_sessionLock)
        {
            if (_cimSession != null) return _cimSession;
            try
            {
                _cimSession = CimSession.Create(null);
                return _cimSession;
            }
            catch
            {
                return null;
            }
        }
    }

    private void InitializeBrightness()
    {
        try
        {
            int val = QueryHardwareBrightness();
            lock (_lock)
            {
                _cachedBrightness = val;
                _targetBrightness = val;
                _initialized = true;
            }
            BrightnessChanged?.Invoke(_cachedBrightness);
        }
        catch
        {
            lock (_lock)
            {
                _initialized = true;
            }
        }
    }

    private void StartKeyboardBrightnessMonitor()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await Task.Delay(200);
                    if (_isApplyingHardware) continue;

                    int hw = QueryHardwareBrightness();
                    if (hw >= 0 && hw != _cachedBrightness && !_isApplyingHardware)
                    {
                        lock (_lock)
                        {
                            _cachedBrightness = hw;
                            _targetBrightness = hw;
                        }
                        BrightnessChanged?.Invoke(hw);
                    }
                }
                catch { }
            }
        });
    }

    public int GetBrightness()
    {
        lock (_lock)
        {
            if (!_initialized)
            {
                _cachedBrightness = QueryHardwareBrightness();
                _targetBrightness = _cachedBrightness;
                _initialized = true;
            }
            return _cachedBrightness;
        }
    }

    public void SetBrightness(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        lock (_lock)
        {
            if (_cachedBrightness == percent) return;
            _cachedBrightness = percent;
        }

        // Immediately notify UI listeners with zero delay or smoothing
        BrightnessChanged?.Invoke(percent);

        // Queue hardware write sequentially
        lock (_wmiLock)
        {
            _targetBrightness = percent;
            if (!_isApplyingHardware)
            {
                _isApplyingHardware = true;
                Task.Run(ProcessHardwareQueue);
            }
        }
    }

    private void ProcessHardwareQueue()
    {
        while (true)
        {
            int target;
            lock (_wmiLock)
            {
                target = _targetBrightness;
            }

            ApplyHardwareBrightness(target);

            lock (_wmiLock)
            {
                if (_targetBrightness == target)
                {
                    _isApplyingHardware = false;
                    break;
                }
            }
        }
    }

    private void ApplyHardwareBrightness(int target)
    {
        bool applied = false;

        // 1. CIM / WMI WmiSetBrightness on reused CimSession - executes in ~5-10ms with zero delay
        try
        {
            var session = GetCimSession();
            if (session != null)
            {
                var instances = session.QueryInstances(@"root\wmi", "WQL", "SELECT * FROM WmiMonitorBrightnessMethods");
                foreach (var instance in instances)
                {
                    using var parameters = new CimMethodParametersCollection
                    {
                        CimMethodParameter.Create("Timeout", (uint)1, CimType.UInt32, CimFlags.In),
                        CimMethodParameter.Create("Brightness", (byte)target, CimType.UInt8, CimFlags.In)
                    };
                    session.InvokeMethod(instance, "WmiSetBrightness", parameters);
                    applied = true;
                }
            }
        }
        catch { }

        // 2. DXVA2 DDC/CI (Desktop monitors & external displays)
        if (!applied)
        {
            try
            {
                IntPtr hMon = MonitorFromWindow(IntPtr.Zero, 1); // MONITOR_DEFAULTTOPRIMARY
                if (hMon != IntPtr.Zero)
                {
                    if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint num) && num > 0)
                    {
                        var monitors = new PHYSICAL_MONITOR[num];
                        if (GetPhysicalMonitorsFromHMONITOR(hMon, num, monitors))
                        {
                            for (int i = 0; i < num; i++)
                            {
                                if (SetMonitorBrightness(monitors[i].hPhysicalMonitor, (uint)target))
                                {
                                    applied = true;
                                }
                            }
                            DestroyPhysicalMonitors(num, monitors);
                        }
                    }
                }
            }
            catch { }
        }
    }

    private int QueryHardwareBrightness()
    {
        // 1. Try CIM / WMI on reused session (~0ms)
        try
        {
            var session = GetCimSession();
            if (session != null)
            {
                var instances = session.QueryInstances(@"root\wmi", "WQL", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
                foreach (var instance in instances)
                {
                    var prop = instance.CimInstanceProperties["CurrentBrightness"];
                    if (prop != null && prop.Value != null)
                    {
                        return Convert.ToInt32(prop.Value);
                    }
                }
            }
        }
        catch { }

        // 2. Try DXVA2
        try
        {
            IntPtr hMon = MonitorFromWindow(IntPtr.Zero, 1);
            if (hMon != IntPtr.Zero)
            {
                if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint num) && num > 0)
                {
                    var monitors = new PHYSICAL_MONITOR[num];
                    if (GetPhysicalMonitorsFromHMONITOR(hMon, num, monitors))
                    {
                        if (num > 0)
                        {
                            if (GetMonitorBrightness(monitors[0].hPhysicalMonitor, out _, out uint cur, out _))
                            {
                                DestroyPhysicalMonitors(num, monitors);
                                return (int)cur;
                            }
                        }
                        DestroyPhysicalMonitors(num, monitors);
                    }
                }
            }
        }
        catch { }

        return _cachedBrightness;
    }

    public void Dispose()
    {
        lock (_sessionLock)
        {
            _cimSession?.Dispose();
            _cimSession = null;
        }
    }
}
