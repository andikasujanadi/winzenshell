using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace WinZenShell.Services;

[Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    int RegisterControlChangeNotify(IntPtr pNotify);
    int UnregisterControlChangeNotify(IntPtr pNotify);
    int GetChannelCount(out uint pnChannelCount);
    int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
    int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
    int GetMasterVolumeLevel(out float pfLevelDB);
    int GetMasterVolumeLevelScalar(out float pfLevel);
    int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
    int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
    int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
    int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
    int GetMute(out bool pbMute);
    int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
    int VolumeStepUp(ref Guid pguidEventContext);
    int VolumeStepDown(ref Guid pguidEventContext);
    int QueryHardwareSupport(out uint pdwHardwareSupportMask);
    int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
}

[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
    int GetState(out int pdwState);
}

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
    int RegisterEndpointNotificationCallback(IntPtr pClient);
    int UnregisterEndpointNotificationCallback(IntPtr pClient);
}

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject { }

public class AudioService
{
    private int _cachedVolume = 50;
    private bool _cachedMute = false;
    private IAudioEndpointVolume? _cachedEndpoint;
    private readonly object _lock = new();

    public event Action<int, bool>? VolumeChanged;

    public AudioService()
    {
        GetVolume();
        StartKeyboardVolumeMonitor();
    }

    private IAudioEndpointVolume? GetEndpointVolume()
    {
        lock (_lock)
        {
            if (_cachedEndpoint != null) return _cachedEndpoint;
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out var dev);
                if (dev == null) return null;

                var iid = typeof(IAudioEndpointVolume).GUID;
                dev.Activate(ref iid, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out var o);
                _cachedEndpoint = o as IAudioEndpointVolume;
                return _cachedEndpoint;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AudioService GetEndpointVolume failed: {ex.Message}");
                return null;
            }
        }
    }

    private void StartKeyboardVolumeMonitor()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await Task.Delay(150);
                    var ep = GetEndpointVolume();
                    if (ep != null)
                    {
                        ep.GetMasterVolumeLevelScalar(out float level);
                        ep.GetMute(out bool mute);
                        int vol = (int)Math.Clamp(Math.Round(level * 100.0), 0, 100);

                        if (vol != _cachedVolume || mute != _cachedMute)
                        {
                            lock (_lock)
                            {
                                _cachedVolume = vol;
                                _cachedMute = mute;
                            }
                            VolumeChanged?.Invoke(vol, mute);
                        }
                    }
                }
                catch { }
            }
        });
    }

    public int GetVolume()
    {
        try
        {
            var ep = GetEndpointVolume();
            if (ep != null)
            {
                ep.GetMasterVolumeLevelScalar(out float level);
                lock (_lock)
                {
                    _cachedVolume = (int)Math.Clamp(Math.Round(level * 100.0), 0, 100);
                }
            }
        }
        catch { }
        return _cachedVolume;
    }

    public bool GetMute()
    {
        try
        {
            var ep = GetEndpointVolume();
            if (ep != null)
            {
                ep.GetMute(out bool mute);
                lock (_lock)
                {
                    _cachedMute = mute;
                }
            }
        }
        catch { }
        return _cachedMute;
    }

    public void SetVolume(int volumePercent)
    {
        try
        {
            volumePercent = Math.Clamp(volumePercent, 0, 100);
            var ep = GetEndpointVolume();
            if (ep != null)
            {
                var guid = Guid.Empty;
                float scalar = volumePercent / 100.0f;
                // Instantaneous hardware volume write (< 0.1ms) with zero smoothing or delay
                ep.SetMasterVolumeLevelScalar(scalar, ref guid);
                if (volumePercent > 0 && _cachedMute)
                {
                    ep.SetMute(false, ref guid);
                    _cachedMute = false;
                }
                lock (_lock)
                {
                    _cachedVolume = volumePercent;
                }
                VolumeChanged?.Invoke(_cachedVolume, _cachedMute);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AudioService SetVolume failed: {ex.Message}");
            // Reset cached endpoint in case default device changed
            lock (_lock) { _cachedEndpoint = null; }
        }
    }

    public void ToggleMute()
    {
        try
        {
            var ep = GetEndpointVolume();
            if (ep != null)
            {
                var guid = Guid.Empty;
                bool newMute = !_cachedMute;
                ep.SetMute(newMute, ref guid);
                lock (_lock)
                {
                    _cachedMute = newMute;
                }
                VolumeChanged?.Invoke(_cachedVolume, _cachedMute);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AudioService ToggleMute failed: {ex.Message}");
            lock (_lock) { _cachedEndpoint = null; }
        }
    }

    public void SetMute(bool mute)
    {
        try
        {
            var ep = GetEndpointVolume();
            if (ep != null)
            {
                var guid = Guid.Empty;
                ep.SetMute(mute, ref guid);
                lock (_lock)
                {
                    _cachedMute = mute;
                }
                VolumeChanged?.Invoke(_cachedVolume, _cachedMute);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AudioService SetMute failed: {ex.Message}");
            lock (_lock) { _cachedEndpoint = null; }
        }
    }
}
