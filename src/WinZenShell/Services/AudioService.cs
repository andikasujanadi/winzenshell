using System;
using System.Runtime.InteropServices;

namespace WinZenShell.Services;

[StructLayout(LayoutKind.Sequential)]
internal struct AUDIO_VOLUME_NOTIFICATION_DATA
{
    public Guid guidEventContext;
    [MarshalAs(UnmanagedType.Bool)]
    public bool bMuted;
    public float fMasterVolume;
    public uint nChannels;
}

[Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolumeCallback
{
    [PreserveSig]
    int OnNotify(IntPtr pNotify);
}

[Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig]
    int RegisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
    [PreserveSig]
    int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
    [PreserveSig]
    int GetChannelCount(out uint pnChannelCount);
    [PreserveSig]
    int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
    [PreserveSig]
    int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
    [PreserveSig]
    int GetMasterVolumeLevel(out float pfLevelDB);
    [PreserveSig]
    int GetMasterVolumeLevelScalar(out float pfLevel);
    [PreserveSig]
    int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
    [PreserveSig]
    int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
    [PreserveSig]
    int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
    [PreserveSig]
    int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
    [PreserveSig]
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
    [PreserveSig]
    int GetMute(out bool pbMute);
    [PreserveSig]
    int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
    [PreserveSig]
    int VolumeStepUp(ref Guid pguidEventContext);
    [PreserveSig]
    int VolumeStepDown(ref Guid pguidEventContext);
    [PreserveSig]
    int QueryHardwareSupport(out uint pdwHardwareSupportMask);
    [PreserveSig]
    int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
}

[Guid("79912236-000E-486A-8730-45433DD6782C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMNotificationClient
{
    [PreserveSig]
    int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, int dwNewState);
    [PreserveSig]
    int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
    [PreserveSig]
    int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
    [PreserveSig]
    int OnDefaultDeviceChanged(int dataFlow, int role, [MarshalAs(UnmanagedType.LPWStr)] string pwstrDefaultDeviceId);
    [PreserveSig]
    int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, IntPtr key);
}

[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    [PreserveSig]
    int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
    [PreserveSig]
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
    [PreserveSig]
    int GetState(out int pdwState);
}

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
    [PreserveSig]
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
    [PreserveSig]
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
    [PreserveSig]
    int RegisterEndpointNotificationCallback(IMMNotificationClient pClient);
    [PreserveSig]
    int UnregisterEndpointNotificationCallback(IMMNotificationClient pClient);
}

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject { }

internal class AudioEndpointVolumeCallback : IAudioEndpointVolumeCallback
{
    private readonly AudioService _service;

    public AudioEndpointVolumeCallback(AudioService service)
    {
        _service = service;
    }

    public int OnNotify(IntPtr pNotify)
    {
        try
        {
            if (pNotify != IntPtr.Zero)
            {
                var data = Marshal.PtrToStructure<AUDIO_VOLUME_NOTIFICATION_DATA>(pNotify);
                _service.HandleVolumeNotification(data.fMasterVolume, data.bMuted);
            }
        }
        catch { }
        return 0;
    }
}

internal class AudioDeviceNotificationClient : IMMNotificationClient
{
    private readonly AudioService _service;

    public AudioDeviceNotificationClient(AudioService service)
    {
        _service = service;
    }

    public int OnDeviceStateChanged(string pwstrDeviceId, int dwNewState) => 0;
    public int OnDeviceAdded(string pwstrDeviceId) => 0;
    public int OnDeviceRemoved(string pwstrDeviceId) => 0;

    public int OnDefaultDeviceChanged(int dataFlow, int role, string pwstrDefaultDeviceId)
    {
        if (dataFlow == 0 && role == 1) // eRender = 0, eMultimedia = 1
        {
            _service.HandleDefaultDeviceChanged();
        }
        return 0;
    }

    public int OnPropertyValueChanged(string pwstrDeviceId, IntPtr key) => 0;
}

public class AudioService : IDisposable
{
    private int _cachedVolume = 50;
    private bool _cachedMute = false;
    private IMMDeviceEnumerator? _enumerator;
    private IAudioEndpointVolume? _cachedEndpoint;
    private readonly AudioEndpointVolumeCallback _volumeCallback;
    private readonly AudioDeviceNotificationClient _deviceClient;
    private System.Windows.Threading.DispatcherTimer? _pollTimer;
    private readonly object _lock = new();
    private bool _isDisposed;

    public event Action<int, bool>? VolumeChanged;

    public AudioService()
    {
        _volumeCallback = new AudioEndpointVolumeCallback(this);
        _deviceClient = new AudioDeviceNotificationClient(this);

        try
        {
            _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            _enumerator.RegisterEndpointNotificationCallback(_deviceClient);
        }
        catch { }

        GetVolume();
        GetMute();
        StartPollingTimer();
    }

    private IAudioEndpointVolume? GetEndpointVolume()
    {
        lock (_lock)
        {
            if (_cachedEndpoint != null) return _cachedEndpoint;
            try
            {
                _enumerator ??= (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                _enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out var dev);
                if (dev == null) return null;

                var iid = typeof(IAudioEndpointVolume).GUID;
                dev.Activate(ref iid, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out var o);
                _cachedEndpoint = o as IAudioEndpointVolume;
                if (_cachedEndpoint != null)
                {
                    try
                    {
                        _cachedEndpoint.RegisterControlChangeNotify(_volumeCallback);
                    }
                    catch { }
                }
                return _cachedEndpoint;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AudioService GetEndpointVolume failed: {ex.Message}");
                return null;
            }
        }
    }

    internal void HandleVolumeNotification(float scalar, bool muted)
    {
        int vol = (int)Math.Clamp(Math.Round(scalar * 100.0), 0, 100);
        bool changed = false;
        lock (_lock)
        {
            if (vol != _cachedVolume || muted != _cachedMute)
            {
                _cachedVolume = vol;
                _cachedMute = muted;
                changed = true;
            }
        }
        if (changed)
        {
            VolumeChanged?.Invoke(vol, muted);
        }
    }

    internal void HandleDefaultDeviceChanged()
    {
        lock (_lock)
        {
            if (_cachedEndpoint != null)
            {
                try { _cachedEndpoint.UnregisterControlChangeNotify(_volumeCallback); } catch { }
                _cachedEndpoint = null;
            }
        }
        GetVolume();
        GetMute();
        VolumeChanged?.Invoke(_cachedVolume, _cachedMute);
    }

    private void StartPollingTimer()
    {
        try
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
            dispatcher.InvokeAsync(() =>
            {
                _pollTimer = new System.Windows.Threading.DispatcherTimer(
                    TimeSpan.FromMilliseconds(200),
                    System.Windows.Threading.DispatcherPriority.Background,
                    (_, _) => CheckVolumeChange(),
                    dispatcher);
                _pollTimer.Start();
            });
        }
        catch { }
    }

    private void CheckVolumeChange()
    {
        try
        {
            var ep = GetEndpointVolume();
            if (ep != null)
            {
                ep.GetMasterVolumeLevelScalar(out float level);
                ep.GetMute(out bool mute);
                int vol = (int)Math.Clamp(Math.Round(level * 100.0), 0, 100);

                bool changed = false;
                lock (_lock)
                {
                    if (vol != _cachedVolume || mute != _cachedMute)
                    {
                        _cachedVolume = vol;
                        _cachedMute = mute;
                        changed = true;
                    }
                }
                if (changed)
                {
                    VolumeChanged?.Invoke(vol, mute);
                }
            }
        }
        catch { }
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

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _pollTimer?.Stop();
        lock (_lock)
        {
            if (_cachedEndpoint != null)
            {
                try { _cachedEndpoint.UnregisterControlChangeNotify(_volumeCallback); } catch { }
                _cachedEndpoint = null;
            }
            if (_enumerator != null)
            {
                try { _enumerator.UnregisterEndpointNotificationCallback(_deviceClient); } catch { }
                _enumerator = null;
            }
        }
    }
}
