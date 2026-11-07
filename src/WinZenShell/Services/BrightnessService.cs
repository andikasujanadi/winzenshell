using System;
using System.Management;
using System.Threading.Tasks;

namespace WinZenShell.Services;

public class BrightnessService
{
    private int _cachedBrightness = 100;
    private int _targetBrightness = 100;
    private bool _isApplyingWmi;
    private readonly object _wmiLock = new();
    private bool _initialized;
    private readonly object _lock = new();

    public int CurrentBrightness => _cachedBrightness;
    public event Action<int>? BrightnessChanged;

    public BrightnessService()
    {
        Task.Run(InitializeBrightness);
    }

    private void InitializeBrightness()
    {
        try
        {
            int val = QueryWmiBrightness();
            lock (_lock)
            {
                _cachedBrightness = val;
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

    public int GetBrightness()
    {
        lock (_lock)
        {
            if (!_initialized)
            {
                _cachedBrightness = QueryWmiBrightness();
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

        BrightnessChanged?.Invoke(percent);

        // Queue WMI write sequentially so UI dragging and scrolling is 100% instant and never blocks WMI
        lock (_wmiLock)
        {
            _targetBrightness = percent;
            if (!_isApplyingWmi)
            {
                _isApplyingWmi = true;
                Task.Run(ProcessWmiQueue);
            }
        }
    }

    private void ProcessWmiQueue()
    {
        while (true)
        {
            int target;
            lock (_wmiLock)
            {
                target = _targetBrightness;
            }

            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
                foreach (ManagementObject obj in searcher.Get())
                {
                    obj.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)target });
                }
            }
            catch
            {
                // Fallback or ignore if unsupported on secondary display
            }

            lock (_wmiLock)
            {
                if (_targetBrightness == target)
                {
                    _isApplyingWmi = false;
                    break;
                }
            }
        }
    }

    private int QueryWmiBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            foreach (ManagementObject obj in searcher.Get())
            {
                var val = obj["CurrentBrightness"];
                if (val != null)
                {
                    return Convert.ToInt32(val);
                }
            }
        }
        catch
        {
            // Fallback default
        }
        return _cachedBrightness;
    }
}
