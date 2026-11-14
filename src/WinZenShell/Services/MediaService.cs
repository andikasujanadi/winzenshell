using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace WinZenShell.Services;

public class MediaPlaybackSnapshot
{
    public bool HasMedia { get; set; } = false;
    public bool IsPlaying { get; set; } = false;
    public string Title { get; set; } = "No Media is Playing";
    public string Artist { get; set; } = "";
    public string AlbumTitle { get; set; } = "";
    public string ThumbnailBase64 { get; set; } = "";
    public string SourceAppId { get; set; } = "";
    public double PositionSeconds { get; set; } = 0;
    public double DurationSeconds { get; set; } = 0;
    public bool CanPlay { get; set; } = false;
    public bool CanPause { get; set; } = false;
    public bool CanSkipNext { get; set; } = false;
    public bool CanSkipPrevious { get; set; } = false;
}

public class MediaService
{
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private MediaPlaybackSnapshot _cachedSnapshot = new();
    private readonly object _lock = new();

    public event Action<MediaPlaybackSnapshot>? MediaChanged;

    public MediaPlaybackSnapshot CurrentSnapshot
    {
        get
        {
            lock (_lock)
            {
                return _cachedSnapshot;
            }
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_sessionManager != null)
            {
                _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
                HookCurrentSession(_sessionManager.GetCurrentSession());
                await RefreshMediaAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaService] Initialize failed: {ex.Message}");
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        try
        {
            HookCurrentSession(sender.GetCurrentSession());
            _ = RefreshMediaAsync();
        }
        catch { }
    }

    private void HookCurrentSession(GlobalSystemMediaTransportControlsSession? session)
    {
        lock (_lock)
        {
            if (_currentSession != null)
            {
                try
                {
                    _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                    _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                    _currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
                }
                catch { }
            }

            _currentSession = session;

            if (_currentSession != null)
            {
                try
                {
                    _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                    _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                    _currentSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
                }
                catch { }
            }
        }
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = RefreshMediaAsync();
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        _ = RefreshMediaAsync();
    }

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        _ = RefreshTimelineAsync();
    }

    public async Task RefreshMediaAsync()
    {
        try
        {
            GlobalSystemMediaTransportControlsSession? session;
            lock (_lock)
            {
                session = _currentSession ?? _sessionManager?.GetCurrentSession();
            }

            if (session == null)
            {
                SetNoMedia();
                return;
            }

            var playbackInfo = session.GetPlaybackInfo();
            var mediaProps = await session.TryGetMediaPropertiesAsync();

            if (mediaProps == null || (string.IsNullOrWhiteSpace(mediaProps.Title) && string.IsNullOrWhiteSpace(mediaProps.Artist)))
            {
                SetNoMedia();
                return;
            }

            var snapshot = new MediaPlaybackSnapshot
            {
                HasMedia = true,
                Title = !string.IsNullOrWhiteSpace(mediaProps.Title) ? mediaProps.Title : "Unknown Title",
                Artist = mediaProps.Artist ?? "",
                AlbumTitle = mediaProps.AlbumTitle ?? "",
                SourceAppId = session.SourceAppUserModelId ?? "",
                IsPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                CanPlay = playbackInfo?.Controls?.IsPlayEnabled ?? true,
                CanPause = playbackInfo?.Controls?.IsPauseEnabled ?? true,
                CanSkipNext = playbackInfo?.Controls?.IsNextEnabled ?? true,
                CanSkipPrevious = playbackInfo?.Controls?.IsPreviousEnabled ?? true
            };

            // Timeline
            try
            {
                var timeline = session.GetTimelineProperties();
                if (timeline != null)
                {
                    snapshot.PositionSeconds = timeline.Position.TotalSeconds;
                    snapshot.DurationSeconds = timeline.EndTime.TotalSeconds;
                }
            }
            catch { }

            // Thumbnail extraction
            if (mediaProps.Thumbnail != null)
            {
                try
                {
                    using var stream = await mediaProps.Thumbnail.OpenReadAsync();
                    if (stream != null && stream.Size > 0)
                    {
                        using var reader = new DataReader(stream.GetInputStreamAt(0));
                        await reader.LoadAsync((uint)stream.Size);
                        byte[] bytes = new byte[stream.Size];
                        reader.ReadBytes(bytes);

                        string mime = stream.ContentType;
                        if (string.IsNullOrWhiteSpace(mime)) mime = "image/png";
                        snapshot.ThumbnailBase64 = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
                    }
                }
                catch { }
            }

            lock (_lock)
            {
                _cachedSnapshot = snapshot;
            }

            MediaChanged?.Invoke(snapshot);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaService] RefreshMediaAsync error: {ex.Message}");
            SetNoMedia();
        }
    }

    private async Task RefreshTimelineAsync()
    {
        try
        {
            GlobalSystemMediaTransportControlsSession? session;
            lock (_lock)
            {
                session = _currentSession;
            }
            if (session != null)
            {
                var timeline = session.GetTimelineProperties();
                if (timeline != null)
                {
                    lock (_lock)
                    {
                        if (_cachedSnapshot.HasMedia)
                        {
                            _cachedSnapshot.PositionSeconds = timeline.Position.TotalSeconds;
                            _cachedSnapshot.DurationSeconds = timeline.EndTime.TotalSeconds;
                        }
                    }
                    MediaChanged?.Invoke(CurrentSnapshot);
                }
            }
        }
        catch { }
        await Task.CompletedTask;
    }

    private void SetNoMedia()
    {
        var empty = new MediaPlaybackSnapshot
        {
            HasMedia = false,
            IsPlaying = false,
            Title = "No Media is Playing",
            Artist = "",
            AlbumTitle = "",
            ThumbnailBase64 = "",
            CanPlay = false,
            CanPause = false,
            CanSkipNext = false,
            CanSkipPrevious = false
        };

        lock (_lock)
        {
            _cachedSnapshot = empty;
        }

        MediaChanged?.Invoke(empty);
    }

    public async Task<bool> TryPlayAsync()
    {
        try
        {
            GlobalSystemMediaTransportControlsSession? session;
            lock (_lock)
            {
                session = _currentSession ?? _sessionManager?.GetCurrentSession();
            }
            if (session != null)
            {
                bool res = await session.TryPlayAsync();
                await Task.Delay(80);
                await RefreshMediaAsync();
                return res;
            }
        }
        catch { }
        return false;
    }

    public async Task<bool> TryPauseAsync()
    {
        try
        {
            GlobalSystemMediaTransportControlsSession? session;
            lock (_lock)
            {
                session = _currentSession ?? _sessionManager?.GetCurrentSession();
            }
            if (session != null)
            {
                bool res = await session.TryPauseAsync();
                await Task.Delay(80);
                await RefreshMediaAsync();
                return res;
            }
        }
        catch { }
        return false;
    }

    public async Task<bool> TryTogglePlayPauseAsync()
    {
        try
        {
            GlobalSystemMediaTransportControlsSession? session;
            lock (_lock)
            {
                session = _currentSession ?? _sessionManager?.GetCurrentSession();
            }
            if (session != null)
            {
                var info = session.GetPlaybackInfo();
                bool res;
                if (info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    res = await session.TryPauseAsync();
                }
                else
                {
                    res = await session.TryPlayAsync();
                }
                await Task.Delay(80);
                await RefreshMediaAsync();
                return res;
            }
        }
        catch { }
        return false;
    }

    public async Task<bool> TrySkipNextAsync()
    {
        try
        {
            GlobalSystemMediaTransportControlsSession? session;
            lock (_lock)
            {
                session = _currentSession ?? _sessionManager?.GetCurrentSession();
            }
            if (session != null)
            {
                bool res = await session.TrySkipNextAsync();
                await Task.Delay(120);
                await RefreshMediaAsync();
                return res;
            }
        }
        catch { }
        return false;
    }

    public async Task<bool> TrySkipPreviousAsync()
    {
        try
        {
            GlobalSystemMediaTransportControlsSession? session;
            lock (_lock)
            {
                session = _currentSession ?? _sessionManager?.GetCurrentSession();
            }
            if (session != null)
            {
                bool res = await session.TrySkipPreviousAsync();
                await Task.Delay(120);
                await RefreshMediaAsync();
                return res;
            }
        }
        catch { }
        return false;
    }

    public async Task<bool> TryChangePlaybackPositionAsync(double positionSeconds)
    {
        try
        {
            GlobalSystemMediaTransportControlsSession? session;
            lock (_lock)
            {
                session = _currentSession ?? _sessionManager?.GetCurrentSession();
            }
            if (session != null)
            {
                long ticks = (long)(positionSeconds * TimeSpan.TicksPerSecond);
                bool res = await session.TryChangePlaybackPositionAsync(ticks);
                await RefreshTimelineAsync();
                return res;
            }
        }
        catch { }
        return false;
    }
}
