using System;
using System.Management;
using System.Threading.Tasks;

namespace WinZenShell.Services;

public class BrightnessService
{
    private int _cachedBrightness = 100;
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

        // Apply via WMI asynchronously so UI dragging remains buttery smooth
        Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
                foreach (ManagementObject obj in searcher.Get())
                {
                    obj.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)percent });
                }
            }
            catch
            {
                // Fallback or ignore if unsupported on secondary display
            }
        });
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
