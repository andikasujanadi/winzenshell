using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace WinZenShell.Services;

public class MediaSessionSummary
{
    public string SourceAppId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public bool IsPlaying { get; set; }
    public bool IsActive { get; set; }
}

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
    public System.Collections.Generic.List<MediaSessionSummary> Sessions { get; set; } = new();
}

public class MediaService
{
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private string? _pinnedSessionAppId;
    private MediaPlaybackSnapshot _cachedSnapshot = new();
    private readonly object _lock = new();
    private double _timelineBasePositionSeconds;
    private DateTimeOffset _timelineBaseUtc = DateTimeOffset.UtcNow;
    private DateTimeOffset _lastManualSeekUtc = DateTimeOffset.MinValue;

    public event Action<MediaPlaybackSnapshot>? MediaChanged;

    public MediaPlaybackSnapshot CurrentSnapshot
    {
        get
        {
            lock (_lock)
            {
                if (_cachedSnapshot.HasMedia && _cachedSnapshot.IsPlaying && _cachedSnapshot.DurationSeconds > 0)
                {
                    double elapsed = Math.Max(0, (DateTimeOffset.UtcNow - _timelineBaseUtc).TotalSeconds);
                    _cachedSnapshot.PositionSeconds = Math.Clamp(_timelineBasePositionSeconds + elapsed, 0, _cachedSnapshot.DurationSeconds);
                }
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
                _sessionManager.SessionsChanged += OnSessionsChanged;
                HookCurrentSession(_sessionManager.GetCurrentSession());
                await RefreshMediaAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaService] Initialize failed: {ex.Message}");
        }
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        try
        {
            var pinned = ResolvePinnedOrCurrentSession(sender);
            if (pinned != _currentSession)
            {
                HookCurrentSession(pinned);
            }
            _ = RefreshMediaAsync();
        }
        catch { }
    }

    private GlobalSystemMediaTransportControlsSession? ResolvePinnedOrCurrentSession(GlobalSystemMediaTransportControlsSessionManager manager)
    {
        if (!string.IsNullOrWhiteSpace(_pinnedSessionAppId))
        {
            try
            {
                foreach (var s in manager.GetSessions())
                {
                    if (string.Equals(s.SourceAppUserModelId, _pinnedSessionAppId, StringComparison.OrdinalIgnoreCase))
                        return s;
                }
            }
            catch { }
            _pinnedSessionAppId = null;
        }
        return manager.GetCurrentSession();
    }

    public async Task SelectSessionAsync(string? sourceAppId)
    {
        try
        {
            _pinnedSessionAppId = string.IsNullOrWhiteSpace(sourceAppId) ? null : sourceAppId.Trim();
            if (_sessionManager != null)
            {
                HookCurrentSession(ResolvePinnedOrCurrentSession(_sessionManager));
                await RefreshMediaAsync();
            }
        }
        catch { }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        try
        {
            HookCurrentSession(ResolvePinnedOrCurrentSession(sender));
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
            DateTimeOffset timelineUpdatedUtc = DateTimeOffset.UtcNow;
            try
            {
                var timeline = session.GetTimelineProperties();
                if (timeline != null)
                {
                    double dur = Math.Max(0, (timeline.EndTime - timeline.StartTime).TotalSeconds);
                    double pos = Math.Max(0, (timeline.Position - timeline.StartTime).TotalSeconds);
                    if (dur > 0) pos = Math.Clamp(pos, 0, dur);
                    snapshot.DurationSeconds = dur;
                    snapshot.PositionSeconds = pos;
                    if (timeline.LastUpdatedTime != default && timeline.LastUpdatedTime != DateTimeOffset.MinValue)
                    {
                        timelineUpdatedUtc = timeline.LastUpdatedTime.ToUniversalTime();
                    }
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

            // Collect available sessions for multi-session switching
            try
            {
                if (_sessionManager != null)
                {
                    foreach (var s in _sessionManager.GetSessions())
                    {
                        try
                        {
                            var sInfo = s.GetPlaybackInfo();
                            var sProps = await s.TryGetMediaPropertiesAsync();
                            string sApp = s.SourceAppUserModelId ?? "";
                            if (string.IsNullOrWhiteSpace(sApp) && string.IsNullOrWhiteSpace(sProps?.Title)) continue;
                            snapshot.Sessions.Add(new MediaSessionSummary
                            {
                                SourceAppId = sApp,
                                Title = !string.IsNullOrWhiteSpace(sProps?.Title) ? sProps.Title : sApp,
                                Artist = sProps?.Artist ?? "",
                                IsPlaying = sInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                                IsActive = string.Equals(sApp, snapshot.SourceAppId, StringComparison.OrdinalIgnoreCase)
                            });
                        }
                        catch { }
                    }
                }
            }
            catch { }

            lock (_lock)
            {
                _cachedSnapshot = snapshot;
                _timelineBasePositionSeconds = snapshot.PositionSeconds;
                _timelineBaseUtc = timelineUpdatedUtc;
            }

            MediaChanged?.Invoke(CurrentSnapshot);
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
                    DateTimeOffset updatedUtc = (timeline.LastUpdatedTime != default && timeline.LastUpdatedTime != DateTimeOffset.MinValue)
                        ? timeline.LastUpdatedTime.ToUniversalTime()
                        : DateTimeOffset.UtcNow;
                    double dur = Math.Max(0, (timeline.EndTime - timeline.StartTime).TotalSeconds);
                    double pos = Math.Max(0, (timeline.Position - timeline.StartTime).TotalSeconds);
                    if (dur > 0) pos = Math.Clamp(pos, 0, dur);

                    bool ignored = false;
                    lock (_lock)
                    {
                        if (_cachedSnapshot.HasMedia)
                        {
                            // Ignore stale timeline updates right after a manual seek
                            if ((DateTimeOffset.UtcNow - _lastManualSeekUtc).TotalMilliseconds < 600 && updatedUtc < _lastManualSeekUtc)
                            {
                                ignored = true;
                            }
                            else
                            {
                                _cachedSnapshot.PositionSeconds = pos;
                                _cachedSnapshot.DurationSeconds = dur;
                                _timelineBasePositionSeconds = pos;
                                _timelineBaseUtc = updatedUtc;
                            }
                        }
                    }
                    if (!ignored)
                    {
                        MediaChanged?.Invoke(CurrentSnapshot);
                    }
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
                var timeline = session.GetTimelineProperties();
                long baseTicks = timeline != null ? timeline.StartTime.Ticks : 0;
                long ticks = baseTicks + (long)(Math.Max(0, positionSeconds) * TimeSpan.TicksPerSecond);
                if (timeline != null && timeline.MaxSeekTime > timeline.MinSeekTime)
                {
                    ticks = Math.Clamp(ticks, timeline.MinSeekTime.Ticks, timeline.MaxSeekTime.Ticks);
                }
                double clampedSeconds = Math.Max(0, (ticks - baseTicks) / (double)TimeSpan.TicksPerSecond);
                bool res = await session.TryChangePlaybackPositionAsync(ticks);
                lock (_lock)
                {
                    if (_cachedSnapshot.HasMedia)
                    {
                        _cachedSnapshot.PositionSeconds = clampedSeconds;
                        _timelineBasePositionSeconds = clampedSeconds;
                        _timelineBaseUtc = DateTimeOffset.UtcNow;
                        _lastManualSeekUtc = _timelineBaseUtc;
                    }
                }
                MediaChanged?.Invoke(CurrentSnapshot);
                await Task.Delay(250);
                await RefreshTimelineAsync();
                return res;
            }
        }
        catch { }
        return false;
    }
}
