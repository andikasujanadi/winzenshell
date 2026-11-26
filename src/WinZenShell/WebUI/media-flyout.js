(() => {
  const artBackdrop = document.getElementById('artBackdrop');
  const sourceBadge = document.getElementById('sourceBadge');
  const sourceName = document.getElementById('sourceName');
  const btnLyricsToggle = document.getElementById('btnLyricsToggle');
  const sessionTabs = document.getElementById('sessionTabs');

  const playerView = document.getElementById('playerView');
  const coverImg = document.getElementById('coverImg');
  const trackTitle = document.getElementById('trackTitle');
  const trackArtist = document.getElementById('trackArtist');
  const trackAlbum = document.getElementById('trackAlbum');

  const lyricsView = document.getElementById('lyricsView');
  const lyricsMiniArt = document.getElementById('lyricsMiniArt');
  const lyricsMiniTitle = document.getElementById('lyricsMiniTitle');
  const lyricsMiniArtist = document.getElementById('lyricsMiniArtist');
  const lyricsModeTag = document.getElementById('lyricsModeTag');
  const lyricsScroll = document.getElementById('lyricsScroll');

  const timeElapsed = document.getElementById('timeElapsed');
  const timeTotal = document.getElementById('timeTotal');
  const seekTrackWrap = document.getElementById('seekTrackWrap');
  const seekFill = document.getElementById('seekFill');
  const seekThumb = document.getElementById('seekThumb');

  const btnPrev = document.getElementById('btnPrev');
  const btnPlayPause = document.getElementById('btnPlayPause');
  const btnNext = document.getElementById('btnNext');
  const iconPlay = document.getElementById('iconPlay');
  const iconPause = document.getElementById('iconPause');

  let currentMedia = null;
  let showLyrics = localStorage.getItem('winzen_media_flyout_lyrics') === 'true';
  let isDraggingSeek = false;
  let lastTrackKey = '';
  let lyricsAbort = null;
  let parsedLyrics = [];
  let plainLyricsLines = [];
  let activeLyricIndex = -1;
  let lastSyncTs = performance.now();

  function sendNative(action, payload = {}) {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage({ action, ...payload });
    }
  }

  function formatTime(sec) {
    if (!Number.isFinite(sec) || sec < 0) return '0:00';
    const total = Math.floor(sec);
    const m = Math.floor(total / 60);
    const s = total % 60;
    return `${m}:${s < 10 ? '0' : ''}${s}`;
  }

  function friendlyAppName(appId) {
    if (!appId) return 'Media Player';
    const lower = appId.toLowerCase();
    if (lower.includes('spotify')) return 'Spotify';
    if (lower.includes('applemusic')) return 'Apple Music';
    if (lower.includes('chrome')) return 'Google Chrome';
    if (lower.includes('msedge')) return 'Microsoft Edge';
    if (lower.includes('firefox')) return 'Firefox';
    if (lower.includes('zunemusic')) return 'Media Player';
    if (lower.includes('vlc')) return 'VLC';
    const base = appId.split('!')[0].split('_')[0];
    const clean = base.replace(/\.exe$/i, '').split('.').pop() || base;
    return clean.charAt(0).toUpperCase() + clean.slice(1);
  }

  function updateLyricsModeUI() {
    btnLyricsToggle.classList.toggle('active', showLyrics);
    if (showLyrics) {
      playerView.style.display = 'none';
      lyricsView.style.display = 'flex';
      syncLyricsToTime(currentMedia?.positionSeconds || 0, true);
    } else {
      lyricsView.style.display = 'none';
      playerView.style.display = 'flex';
    }
  }

  btnLyricsToggle.addEventListener('click', () => {
    showLyrics = !showLyrics;
    localStorage.setItem('winzen_media_flyout_lyrics', String(showLyrics));
    updateLyricsModeUI();
  });

  btnPrev.addEventListener('click', () => sendNative('media_previous'));
  btnPlayPause.addEventListener('click', () => sendNative('media_play_pause'));
  btnNext.addEventListener('click', () => sendNative('media_next'));

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') sendNative('close_flyout');
  });

  // Drag-to-seek
  function seekFromPointer(clientX) {
    if (!currentMedia || !currentMedia.hasMedia || !currentMedia.durationSeconds) return 0;
    const rect = seekTrackWrap.getBoundingClientRect();
    if (rect.width <= 0) return 0;
    const pct = Math.max(0, Math.min(1, (clientX - rect.left) / rect.width));
    const sec = pct * currentMedia.durationSeconds;
    seekFill.style.width = `${pct * 100}%`;
    seekThumb.style.left = `${pct * 100}%`;
    timeElapsed.textContent = formatTime(sec);
    return sec;
  }

  seekTrackWrap.addEventListener('pointerdown', (e) => {
    if (!currentMedia || !currentMedia.hasMedia || !currentMedia.durationSeconds) return;
    isDraggingSeek = true;
    seekTrackWrap.classList.add('dragging');
    seekTrackWrap.setPointerCapture(e.pointerId);
    seekFromPointer(e.clientX);
  });

  seekTrackWrap.addEventListener('pointermove', (e) => {
    if (!isDraggingSeek) return;
    seekFromPointer(e.clientX);
  });

  seekTrackWrap.addEventListener('pointerup', (e) => {
    if (!isDraggingSeek) return;
    isDraggingSeek = false;
    seekTrackWrap.classList.remove('dragging');
    try { seekTrackWrap.releasePointerCapture(e.pointerId); } catch (_) {}
    const targetSec = seekFromPointer(e.clientX);
    if (currentMedia) currentMedia.positionSeconds = targetSec;
    sendNative('media_seek', { position: targetSec });
    syncLyricsToTime(targetSec, true);
  });

  // LRCLIB Synced Lyrics Engine
  function parseLrc(lrcText) {
    const lines = [];
    const regex = /\[(\d{1,2}):(\d{2})(?:\.(\d{1,3}))?\]\s*(.*)/;
    for (const raw of (lrcText || '').split('\n')) {
      const m = raw.match(regex);
      if (!m) continue;
      const min = parseInt(m[1], 10);
      const sec = parseInt(m[2], 10);
      const msStr = (m[3] || '0').padEnd(3, '0').slice(0, 3);
      const ms = parseInt(msStr, 10);
      const text = (m[4] || '').trim();
      if (!text) continue;
      lines.push({ time: min * 60 + sec + ms / 1000, text });
    }
    lines.sort((a, b) => a.time - b.time);
    return lines;
  }

  function renderLyricsStatus(msg) {
    lyricsScroll.innerHTML = `<div class="lyrics-status">${msg}</div>`;
  }

  function renderLyricsLines() {
    lyricsScroll.innerHTML = '';
    activeLyricIndex = -1;
    if (parsedLyrics.length > 0) {
      lyricsModeTag.textContent = 'SYNCED';
      parsedLyrics.forEach((line, idx) => {
        const el = document.createElement('div');
        el.className = 'lyrics-line';
        el.textContent = line.text;
        el.dataset.idx = String(idx);
        el.addEventListener('click', () => {
          if (currentMedia && currentMedia.hasMedia) {
            currentMedia.positionSeconds = line.time;
            sendNative('media_seek', { position: line.time });
            syncLyricsToTime(line.time, true);
          }
        });
        lyricsScroll.appendChild(el);
      });
      syncLyricsToTime(currentMedia?.positionSeconds || 0, true);
    } else if (plainLyricsLines.length > 0) {
      lyricsModeTag.textContent = 'PLAIN';
      plainLyricsLines.forEach((text) => {
        const el = document.createElement('div');
        el.className = 'lyrics-line plain';
        el.textContent = text;
        lyricsScroll.appendChild(el);
      });
    } else {
      lyricsModeTag.textContent = 'NONE';
      renderLyricsStatus('No lyrics found for this track');
    }
  }

  function syncLyricsToTime(posSec, forceScroll = false) {
    if (!showLyrics || parsedLyrics.length === 0) return;
    let nextIdx = -1;
    for (let i = 0; i < parsedLyrics.length; i++) {
      if (parsedLyrics[i].time <= posSec + 0.25) nextIdx = i;
      else break;
    }
    if (nextIdx === activeLyricIndex && !forceScroll) return;
    const prev = lyricsScroll.querySelector('.lyrics-line.active');
    if (prev) prev.classList.remove('active');
    activeLyricIndex = nextIdx;
    if (nextIdx >= 0) {
      const el = lyricsScroll.querySelector(`.lyrics-line[data-idx="${nextIdx}"]`);
      if (el) {
        el.classList.add('active');
        const targetTop = Math.max(0, el.offsetTop - (lyricsScroll.clientHeight / 2) + (el.clientHeight / 2));
        lyricsScroll.scrollTo({
          top: targetTop,
          behavior: forceScroll ? 'auto' : 'smooth'
        });
      }
    }
  }

  async function fetchLyrics(title, artist, duration) {
    if (lyricsAbort) lyricsAbort.abort();
    lyricsAbort = new AbortController();
    const signal = lyricsAbort.signal;
    parsedLyrics = [];
    plainLyricsLines = [];
    renderLyricsStatus('Searching lyrics...');

    const cleanTitle = (title || '').replace(/\s*[-([].*?(official|video|audio|lyrics|remaster).*?[)\]]/gi, '').trim();
    const cleanArtist = (artist || '').split(/,|&|feat\.|ft\./i)[0].trim();

    try {
      let data = null;
      if (cleanTitle && cleanArtist) {
        let url = `https://lrclib.net/api/get?track_name=${encodeURIComponent(cleanTitle)}&artist_name=${encodeURIComponent(cleanArtist)}`;
        if (duration > 0) url += `&duration=${Math.round(duration)}`;
        const res = await fetch(url, { signal, headers: { 'Lrclib-Client': 'WinZenShell MediaFlyout v1.0' } });
        if (res.ok) data = await res.json();
      }
      if (!data || (!data.syncedLyrics && !data.plainLyrics)) {
        const q = `${cleanTitle} ${cleanArtist}`.trim();
        if (q) {
          const sRes = await fetch(`https://lrclib.net/api/search?q=${encodeURIComponent(q)}`, {
            signal,
            headers: { 'Lrclib-Client': 'WinZenShell MediaFlyout v1.0' }
          });
          if (sRes.ok) {
            const list = await sRes.json();
            if (Array.isArray(list) && list.length > 0) {
              data = list.find((x) => x.syncedLyrics) || list.find((x) => x.plainLyrics) || null;
            }
          }
        }
      }
      if (signal.aborted) return;
      if (data && data.syncedLyrics) {
        parsedLyrics = parseLrc(data.syncedLyrics);
      } else if (data && data.plainLyrics) {
        plainLyricsLines = data.plainLyrics.split('\n').map((l) => l.trim()).filter(Boolean);
      }
      renderLyricsLines();
    } catch (err) {
      if (err && err.name === 'AbortError') return;
      renderLyricsStatus('Unable to load lyrics');
    }
  }

  function renderSessions(sessions) {
    if (!Array.isArray(sessions) || sessions.length <= 1) {
      sessionTabs.style.display = 'none';
      sessionTabs.innerHTML = '';
      return;
    }
    sessionTabs.style.display = 'flex';
    sessionTabs.innerHTML = '';
    sessions.forEach((s) => {
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'session-pill' + (s.isActive ? ' active' : '');
      btn.textContent = friendlyAppName(s.sourceAppId);
      btn.title = s.title || s.sourceAppId;
      btn.addEventListener('click', () => {
        sendNative('media_select_session', { sourceAppId: s.sourceAppId });
      });
      sessionTabs.appendChild(btn);
    });
  }

  function renderMedia(media) {
    currentMedia = media;
    lastSyncTs = performance.now();
    const hasMedia = !!(media && media.hasMedia);

    sourceName.textContent = hasMedia ? friendlyAppName(media.sourceAppId) : 'Media Player';
    sourceBadge.classList.toggle('is-playing', !!(hasMedia && media.isPlaying));
    renderSessions(media?.sessions);

    if (!hasMedia) {
      trackTitle.textContent = 'No Media is Playing';
      trackArtist.textContent = '';
      trackAlbum.textContent = '';
      lyricsMiniTitle.textContent = 'No Media is Playing';
      lyricsMiniArtist.textContent = '';
      coverImg.src = '';
      coverImg.classList.remove('has-img');
      lyricsMiniArt.src = '';
      artBackdrop.style.backgroundImage = '';
      artBackdrop.classList.remove('has-art');
      seekFill.style.width = '0%';
      seekThumb.style.left = '0%';
      timeElapsed.textContent = '0:00';
      timeTotal.textContent = '0:00';
      btnPrev.disabled = true;
      btnPlayPause.disabled = true;
      btnNext.disabled = true;
      iconPlay.style.display = 'block';
      iconPause.style.display = 'none';
      lastTrackKey = '';
      parsedLyrics = [];
      plainLyricsLines = [];
      renderLyricsStatus('Play a song to view synced lyrics');
      return;
    }

    trackTitle.textContent = media.title || 'Unknown Title';
    trackArtist.textContent = media.artist || '';
    trackAlbum.textContent = media.albumTitle || '';
    lyricsMiniTitle.textContent = media.title || 'Unknown Title';
    lyricsMiniArtist.textContent = media.artist || '';

    if (media.thumbnailBase64 && media.thumbnailBase64.length > 20) {
      coverImg.src = media.thumbnailBase64;
      coverImg.classList.add('has-img');
      lyricsMiniArt.src = media.thumbnailBase64;
      artBackdrop.style.backgroundImage = `url("${media.thumbnailBase64}")`;
      artBackdrop.classList.add('has-art');
    } else {
      coverImg.src = '';
      coverImg.classList.remove('has-img');
      lyricsMiniArt.src = '';
      artBackdrop.style.backgroundImage = '';
      artBackdrop.classList.remove('has-art');
    }

    btnPrev.disabled = false;
    btnPlayPause.disabled = false;
    btnNext.disabled = false;
    iconPlay.style.display = media.isPlaying ? 'none' : 'block';
    iconPause.style.display = media.isPlaying ? 'block' : 'none';

    if (!isDraggingSeek) {
      const dur = media.durationSeconds || 0;
      const pos = media.positionSeconds || 0;
      const pct = dur > 0 ? Math.min(100, Math.max(0, (pos / dur) * 100)) : 0;
      seekFill.style.width = `${pct}%`;
      seekThumb.style.left = `${pct}%`;
      timeElapsed.textContent = formatTime(pos);
      timeTotal.textContent = formatTime(dur);
    }

    const trackKey = `${media.title || ''}||${media.artist || ''}`;
    if (trackKey !== lastTrackKey) {
      lastTrackKey = trackKey;
      fetchLyrics(media.title, media.artist, media.durationSeconds);
    } else {
      syncLyricsToTime(media.positionSeconds || 0);
    }
  }

  // Smooth local timeline interpolation between host pushes
  setInterval(() => {
    if (!currentMedia || !currentMedia.hasMedia || !currentMedia.isPlaying || isDraggingSeek) return;
    const now = performance.now();
    const dt = (now - lastSyncTs) / 1000;
    lastSyncTs = now;
    const dur = currentMedia.durationSeconds || 0;
    if (dur > 0) {
      currentMedia.positionSeconds = Math.min(dur, (currentMedia.positionSeconds || 0) + dt);
      const pct = Math.min(100, Math.max(0, (currentMedia.positionSeconds / dur) * 100));
      seekFill.style.width = `${pct}%`;
      seekThumb.style.left = `${pct}%`;
      timeElapsed.textContent = formatTime(currentMedia.positionSeconds);
      syncLyricsToTime(currentMedia.positionSeconds);
    }
  }, 250);

  function handleMessage(data) {
    if (!data) return;
    if (data.type === 'theme_update') {
      const th = data.theme || 'dark';
      document.documentElement.setAttribute('data-theme', th);
      document.body.classList.toggle('theme-light', th === 'light');
      if (data.accentColor) {
        document.documentElement.style.setProperty('--winzen-accent', data.accentColor);
      }
    } else if (data.type === 'media_update' && data.media) {
      renderMedia(data.media);
    }
  }

  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (ev) => handleMessage(ev.data));
  }
  updateLyricsModeUI();
})();
