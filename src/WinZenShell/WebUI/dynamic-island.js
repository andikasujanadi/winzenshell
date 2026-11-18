(() => {
  const capsule = document.getElementById('islandCapsule');
  const collapsedContent = document.getElementById('islandCollapsedContent');
  const collapsedInner = document.getElementById('islandCollapsedInner');
  const collapsedMusicInner = document.getElementById('islandCollapsedMusicInner');
  const expandedContent = document.getElementById('islandExpandedContent');
  const expandedMusicContent = document.getElementById('islandExpandedMusicContent');

  const timeEl = document.getElementById('diTime');
  const secEl = document.getElementById('diSeconds');
  const dateEl = document.getElementById('diDate');

  const colMusicThumb = document.getElementById('diColMusicThumb');
  const colMusicThumbPlaceholder = document.getElementById('diColMusicThumbPlaceholder');
  const colMusicTitle = document.getElementById('diColMusicTitle');
  const colMusicWaveform = document.getElementById('diColMusicWaveform');

  // Background merge/unmerge pill elements
  const bgPill = document.getElementById('islandBgPill');
  const bgClockInner = document.getElementById('islandBgClockInner');
  const bgMusicInner = document.getElementById('islandBgMusicInner');
  const bgTimeEl = document.getElementById('diBgTime');
  const bgSecEl = document.getElementById('diBgSeconds');
  const bgDateEl = document.getElementById('diBgDate');
  const bgThumb = document.getElementById('diBgThumb');
  const bgThumbPlaceholder = document.getElementById('diBgThumbPlaceholder');
  const bgTitle = document.getElementById('diBgTitle');
  const bgWaveform = document.getElementById('diBgWaveform');

  const cardTime = document.getElementById('diCardTime');
  const cardSec = document.getElementById('diCardSeconds');
  const cardBatteryGroup = document.getElementById('diCardBattery');
  const cardBatteryPct = document.getElementById('diCardBatteryPct');
  const cardBatteryIconWrap = document.getElementById('diCardBatteryIconWrap');
  let currentBatteryStyle = '';
  let lastBatteryMetrics = null;
  const cardWeekday = document.getElementById('diCardWeekday');
  const cardDate = document.getElementById('diCardDate');

  // Music card elements
  const musicCoverImg = document.getElementById('diMusicCoverImg');
  const musicCoverPlaceholder = document.getElementById('diMusicCoverPlaceholder');
  const musicTitle = document.getElementById('diMusicTitle');
  const musicArtist = document.getElementById('diMusicArtist');
  const musicCardWaveform = document.getElementById('diMusicCardWaveform');
  const musicPosition = document.getElementById('diMusicPosition');
  const musicDuration = document.getElementById('diMusicDuration');
  const musicSeekTrack = document.getElementById('diMusicSeekTrack');
  const musicSeekFill = document.getElementById('diMusicSeekFill');
  const btnMusicPrev = document.getElementById('btnDiMusicPrev');
  const btnMusicPlayPause = document.getElementById('btnDiMusicPlayPause');
  const btnMusicNext = document.getElementById('btnDiMusicNext');
  const btnPinLyrics = document.getElementById('btnDiPinLyrics');
  const btnPillMode = document.getElementById('btnDiPillMode');
  const lyricsView = document.getElementById('diMusicLyricsView');
  const lyricsScroller = document.getElementById('diLyricsScroller');
  const lyricsPlaceholder = document.getElementById('diLyricsPlaceholder');
  const iconPlay = document.getElementById('iconDiPlay');
  const iconPause = document.getElementById('iconDiPause');

  // Top-Center collapsed music pill
  const pillsRow = document.getElementById('islandPillsRow');
  const islandMusicPill = document.getElementById('islandMusicPill');
  const diTcThumb = document.getElementById('diTcThumb');
  const diTcThumbPlaceholder = document.getElementById('diTcThumbPlaceholder');
  const diTcTitle = document.getElementById('diTcTitle');
  const diTcWaveform = document.getElementById('diTcWaveform');

  let isExpanded = false;
  let isCollapsing = false;
  let currentTarget = 'clock';
  let currentSlot = 'center';
  let currentAnchorWidth = 78;
  let currentClockWidth = 78;
  let currentDockEdge = 'top';
  let lastMediaSnapshot = null;
  let isScrubbingSeek = false;
  let musicHoverTimer = null;
  let closeTimeout = null;

  let diConfig = {
    enabled: true,
    placement: 'taskbar',
    taskbarSlot: 'center',
    timeFormat: '24h',
    showSeconds: false,
    showDate: false,
    enableAnimation: true,
    enableMusicIsland: true
  };

  let globalDateFormat = 'DD-MM-YYYY';

  function formatDateWithTemplate(date, template) {
    const tpl = (template && String(template).trim()) ? String(template) : 'DD-MM-YYYY';
    const d = date instanceof Date ? date : new Date();
    const year = String(d.getFullYear());
    const monthIdx = d.getMonth();
    const dayNum = d.getDate();
    const map = {
      YYYY: year,
      YY: year.slice(-2),
      MMMM: d.toLocaleDateString('en-US', { month: 'long' }),
      MMM: d.toLocaleDateString('en-US', { month: 'short' }),
      MM: String(monthIdx + 1).padStart(2, '0'),
      M: String(monthIdx + 1),
      dddd: d.toLocaleDateString('en-US', { weekday: 'long' }),
      ddd: d.toLocaleDateString('en-US', { weekday: 'short' }),
      DD: String(dayNum).padStart(2, '0'),
      D: String(dayNum)
    };
    return tpl.replace(/YYYY|YY|MMMM|MMM|MM|M|dddd|ddd|DD|D/g, (m) => map[m] ?? m);
  }

  function formatTime(sec) {
    if (typeof sec !== 'number' || isNaN(sec) || sec < 0) sec = 0;
    const m = Math.floor(sec / 60);
    const s = Math.floor(sec % 60);
    return `${m}:${String(s).padStart(2, '0')}`;
  }

  function getStandardClockWidth() {
    if (!diConfig.showDate) {
      return diConfig.showSeconds ? 100 : 78;
    }
    if (!globalDateFormat || globalDateFormat === 'DD-MM-YYYY') {
      return 196;
    }
    const dateLen = (dateEl && dateEl.textContent) ? dateEl.textContent.length : 10;
    const secAdd = diConfig.showSeconds ? 22 : 0;
    return Math.max(140, 114 + secAdd + Math.round(dateLen * 8));
  }

  function updateClock() {
    const now = new Date();
    const is12h = diConfig.timeFormat === '12h';

    let hours = now.getHours();
    let ampm = '';
    if (is12h) {
      ampm = hours >= 12 ? ' PM' : ' AM';
      hours = hours % 12;
      if (hours === 0) hours = 12;
    }

    const minStr = String(now.getMinutes()).padStart(2, '0');
    const hrStr = is12h ? String(hours) : String(hours).padStart(2, '0');
    const rawSec = String(now.getSeconds()).padStart(2, '0');
    const secStr = `.${rawSec}`;

    const formattedDate = formatDateWithTemplate(now, globalDateFormat);
    const weekdayStr = now.toLocaleDateString('en-US', { weekday: 'long' });

    const timeText = `${hrStr}:${minStr}${ampm}`;

    // 1. Collapsed Clock & Background Clock Pill
    if (timeEl) timeEl.textContent = timeText;
    if (bgTimeEl) bgTimeEl.textContent = timeText;

    if (secEl) {
      secEl.style.display = diConfig.showSeconds ? 'inline' : 'none';
      secEl.textContent = secStr;
    }
    if (bgSecEl) {
      bgSecEl.style.display = diConfig.showSeconds ? 'inline' : 'none';
      bgSecEl.textContent = secStr;
    }

    if (dateEl) {
      dateEl.style.display = diConfig.showDate ? 'inline' : 'none';
      dateEl.textContent = formattedDate;
    }
    if (bgDateEl) {
      bgDateEl.style.display = diConfig.showDate ? 'inline' : 'none';
      bgDateEl.textContent = formattedDate;
    }

    // 2. Expanded Card Clock & Date
    if (cardTime) cardTime.textContent = timeText;
    if (cardSec) {
      cardSec.style.display = 'inline';
      cardSec.textContent = secStr;
    }

    if (cardWeekday) cardWeekday.textContent = weekdayStr;
    if (cardDate) cardDate.textContent = formattedDate;

    updateCollapsedWidth();
  }

  function buildBatteryIconHtml(style) {
    switch (style) {
      case 'win10':
        return `
          <svg class="batt-svg batt-win10" viewBox="0 0 28 16" fill="none" xmlns="http://www.w3.org/2000/svg">
            <rect class="batt-housing" x="1.5" y="2.5" width="22" height="11" rx="0" stroke="var(--batt-border)" stroke-width="1.2"/>
            <rect class="batt-cap" x="23.5" y="5.5" width="2" height="5" rx="0" fill="var(--batt-border)"/>
            <rect class="batt-fill-bar" x="3" y="4" width="0" height="8" rx="0" fill="var(--batt-fill)"/>
            <path class="batt-bolt" d="M12.5 3.5 L9 8 H12 L11 12.5 L15 7.5 H12 Z" fill="currentColor"/>
          </svg>
        `;

      case 'macos':
        return `
          <svg class="batt-svg batt-macos" viewBox="0 0 29 16" fill="none" xmlns="http://www.w3.org/2000/svg">
            <defs>
              <clipPath id="diMacosPillClip">
                <rect x="1.5" y="2" width="23" height="12" rx="4.5"/>
              </clipPath>
            </defs>
            <rect class="batt-track" x="1.5" y="2" width="23" height="12" rx="4.5" fill="var(--batt-track)"/>
            <path class="batt-cap" d="M25.7 5.7 C27.2 5.7 27.5 6.6 27.5 8 C27.5 9.4 27.2 10.3 25.7 10.3 Z" fill="var(--batt-track)"/>
            <g clip-path="url(#diMacosPillClip)">
              <rect class="batt-fill-bar" x="1.5" y="2" width="0" height="12" fill="var(--batt-fill)"/>
            </g>
            <g class="batt-bolt">
              <path d="M12.5 2.5 L8.5 7.5 H11.5 L10.5 13.5 L16.5 6.5 H12.5 Z" stroke="rgba(0,0,0,0.35)" stroke-width="1.2" stroke-linejoin="round" fill="none"/>
              <path d="M12.5 2.5 L8.5 7.5 H11.5 L10.5 13.5 L16.5 6.5 H12.5 Z" fill="#ffffff"/>
            </g>
          </svg>
        `;

      case 'win11':
      default:
        return `
          <svg class="batt-svg batt-win11" viewBox="0 0 28 16" fill="none" xmlns="http://www.w3.org/2000/svg">
            <defs>
              <clipPath id="diWin11CavityClip">
                <rect x="3.5" y="4.5" width="18" height="7" rx="1.5"/>
              </clipPath>
            </defs>
            <rect class="batt-housing" x="1.5" y="2.5" width="22" height="11" rx="3.5" stroke="var(--batt-border)" stroke-width="1.5"/>
            <rect class="batt-cap" x="23.5" y="6" width="2" height="4" rx="1" fill="var(--batt-border)"/>
            <g clip-path="url(#diWin11CavityClip)">
              <rect class="batt-fill-bar" x="3.5" y="4.5" width="0" height="7" fill="var(--batt-fill)"/>
            </g>
            <g class="batt-bolt">
              <path class="bolt-halo" d="M10.5 0.5 L7.5 6.5 H10 L9 11.5 L13.5 5.5 H10.5 Z" stroke="var(--batt-bolt-halo)" stroke-width="1.8" stroke-linejoin="round" stroke-linecap="round"/>
              <path class="bolt-core" d="M10.5 0.5 L7.5 6.5 H10 L9 11.5 L13.5 5.5 H10.5 Z" fill="var(--batt-bolt-fill)"/>
            </g>
          </svg>
        `;
    }
  }

  function applyBatteryStyle(style) {
    const resolved = (style === 'win10' || style === 'macos' || style === 'win11') ? style : 'win11';
    if (resolved === currentBatteryStyle && cardBatteryIconWrap && cardBatteryIconWrap.children.length > 0) return;
    currentBatteryStyle = resolved;
    if (cardBatteryGroup) {
      cardBatteryGroup.classList.remove('style-win11', 'style-win10', 'style-macos');
      cardBatteryGroup.classList.add(`style-${resolved}`);
    }
    if (cardBatteryIconWrap) {
      cardBatteryIconWrap.innerHTML = buildBatteryIconHtml(resolved);
    }
    if (lastBatteryMetrics) {
      updateBattery(lastBatteryMetrics);
    }
  }

  function updateBattery(metrics) {
    if (!metrics) return;
    lastBatteryMetrics = metrics;
    let pct = 100;
    if (typeof metrics.batteryPercent === 'number' && metrics.batteryPercent >= 0) {
      pct = Math.max(0, Math.min(100, metrics.batteryPercent));
    }
    const isCharging = !!metrics.isCharging;
    const isLow = pct <= 5 && !isCharging;
    const isEnergySaver = pct > 5 && pct <= 20 && !isCharging;

    if (cardBatteryPct) cardBatteryPct.textContent = `${pct}%`;
    if (cardBatteryGroup) {
      cardBatteryGroup.classList.toggle('is-charging', isCharging);
      cardBatteryGroup.classList.toggle('is-low', isLow);
      cardBatteryGroup.classList.toggle('is-energy-saver', isEnergySaver);
    }
    if (cardBatteryIconWrap) {
      const fillBar = cardBatteryIconWrap.querySelector('.batt-fill-bar');
      if (fillBar) {
        const maxW = currentBatteryStyle === 'macos' ? 23 : (currentBatteryStyle === 'win10' ? 19 : 18);
        const fillW = Math.max(0, Math.min(maxW, (maxW * pct) / 100));
        fillBar.setAttribute('width', fillW.toFixed(2));
      }
    }
  }

  applyBatteryStyle('win11');

  function updateCollapsedWidth() {
    if (!capsule || isExpanded || isCollapsing) return;
    if (diConfig.placement === 'top-center') {
      const targetW = getStandardClockWidth();
      capsule.style.setProperty('--di-collapsed-width', `${targetW}px`);
    }
  }

  function syncSmallMusicElements(media, thumbImg, thumbPl, waveEl) {
    if (thumbImg && thumbPl) {
      if (media && media.thumbnailBase64 && media.thumbnailBase64.length > 30) {
        thumbImg.src = media.thumbnailBase64;
        thumbImg.style.display = 'block';
        thumbPl.style.display = 'none';
      } else {
        thumbImg.style.display = 'none';
        thumbPl.style.display = 'block';
      }
    }
    if (waveEl) {
      if (media && media.isPlaying) {
        waveEl.classList.add('playing');
      } else {
        waveEl.classList.remove('playing');
      }
    }
  }

  let isMusicPillWideMode = false;
  let isLyricsPinned = false;

  // Synced Lyrics System (LRCLIB)
  let parsedLyrics = []; // [{ time: number, text: string }]
  let lyricsTrackKey = '';
  let activeLyricIndex = -1;
  let lyricsFetchAbort = null;

  function parseLrc(lrcText) {
    if (!lrcText || typeof lrcText !== 'string') return [];
    const lines = lrcText.split('\n');
    const result = [];
    const tagRegex = /\[(\d{1,2}):(\d{2})(?:\.(\d{2,3}))?\]/g;
    for (const rawLine of lines) {
      const line = rawLine.trim();
      if (!line) continue;
      tagRegex.lastIndex = 0;
      let match;
      const timestamps = [];
      let lastMatchEnd = 0;
      while ((match = tagRegex.exec(line)) !== null) {
        const min = parseInt(match[1], 10);
        const sec = parseInt(match[2], 10);
        const msFrac = match[3] ? (match[3].length === 2 ? parseInt(match[3], 10) * 10 : parseInt(match[3], 10)) : 0;
        const totalSec = min * 60 + sec + msFrac / 1000;
        timestamps.push(totalSec);
        lastMatchEnd = tagRegex.lastIndex;
      }
      const text = line.slice(lastMatchEnd).trim();
      if (text && timestamps.length > 0) {
        for (const t of timestamps) {
          result.push({ time: t, text });
        }
      }
    }
    result.sort((a, b) => a.time - b.time);
    return result;
  }

  function flushSmallPillLyrics(fallbackTitle) {
    currentActiveLyricText = '';
    const title = fallbackTitle || (lastMediaSnapshot && lastMediaSnapshot.title) || 'Playing';
    if (colMusicTitle) colMusicTitle.textContent = title;
    if (bgTitle) bgTitle.textContent = title;
    if (diTcTitle) diTcTitle.textContent = title;
    if (window.winzen && typeof window.winzen.send === 'function') {
      window.winzen.send('di_set_lyrics', {
        isPinned: false,
        text: '',
        width: 0
      });
    }
  }

  function cleanLyricsQuery(rawTitle, rawArtist) {
    let t = (rawTitle || '').trim();
    let a = (rawArtist || '').trim();

    // Strip common YouTube / streaming noise in parentheses or brackets
    t = t.replace(/\s*[\(\[](?:official\s*(?:music\s*)?(?:video|audio|lyric\s*video|visualizer)|lyrics?|audio|hd|4k|mv|m\/v|live|remaster(?:ed)?|explicit)[\)\]]/gi, '');
    t = t.replace(/\s*[\(\[](?:feat\.?|ft\.?)\s+[^\)\]]+[\)\]]/gi, '');
    t = t.replace(/\s*\|\s*.*$/, '').trim();

    // Clean VEVO / Topic / Official channel suffixes from artist
    a = a.replace(/vevo$/i, '').replace(/\s*-\s*topic$/i, '').replace(/\s*official$/i, '').trim();

    // If title has "Artist - Track" format, split it
    let splitArtist = '';
    let splitTrack = '';
    const dashMatch = t.match(/^(.+?)\s+[-–—]\s+(.+)$/);
    if (dashMatch) {
      splitArtist = dashMatch[1].trim();
      splitTrack = dashMatch[2].trim();
    }

    return { cleanTitle: t, cleanArtist: a, splitArtist, splitTrack };
  }

  async function searchLrclib(query, signal, duration) {
    if (!query) return null;
    const searchUrl = `https://lrclib.net/api/search?q=${encodeURIComponent(query)}`;
    const res = await fetch(searchUrl, {
      signal,
      headers: { 'Lrclib-Client': 'WinZenShell DynamicIsland v1.0' }
    });
    if (!res.ok) return null;
    const list = await res.json();
    if (!Array.isArray(list) || list.length === 0) return null;

    // Prefer items with syncedLyrics, and closest duration if available
    const withSynced = list.filter((item) => !!item.syncedLyrics);
    const pool = withSynced.length > 0 ? withSynced : list.filter((item) => !!item.plainLyrics);
    if (pool.length === 0) return null;

    if (typeof duration === 'number' && duration > 0) {
      pool.sort((x, y) => Math.abs((x.duration || 0) - duration) - Math.abs((y.duration || 0) - duration));
    }
    return pool[0];
  }

  async function fetchLyricsForTrack(title, artist, duration) {
    const key = `${(title || '').toLowerCase()}|${(artist || '').toLowerCase()}`;
    if (key === lyricsTrackKey) return;
    lyricsTrackKey = key;
    parsedLyrics = [];
    activeLyricIndex = -1;
    flushSmallPillLyrics(title);

    if (lyricsFetchAbort) {
      lyricsFetchAbort.abort();
    }
    lyricsFetchAbort = new AbortController();
    const { signal } = lyricsFetchAbort;

    if (!title || title === 'No Media is Playing' || title === 'No Media') {
      renderLyricsPlaceholder('No media playing');
      return;
    }

    renderLyricsPlaceholder('Searching lyrics...');

    try {
      const { cleanTitle, cleanArtist, splitArtist, splitTrack } = cleanLyricsQuery(title, artist);
      const primaryTrack = splitTrack || cleanTitle;
      const primaryArtist = splitArtist || cleanArtist;

      // 1. Direct match query
      let url = `https://lrclib.net/api/get?track_name=${encodeURIComponent(primaryTrack)}&artist_name=${encodeURIComponent(primaryArtist)}`;
      if (typeof duration === 'number' && duration > 0) {
        url += `&duration=${Math.round(duration)}`;
      }

      let res = await fetch(url, {
        signal,
        headers: { 'Lrclib-Client': 'WinZenShell DynamicIsland v1.0' }
      });

      let data = null;
      if (res.ok) {
        const candidate = await res.json();
        if (candidate && (candidate.syncedLyrics || candidate.plainLyrics)) {
          data = candidate;
        }
      }

      // 2. Fallback search queries (handles YouTube channel names, "Artist - Song", etc.)
      if (!data) {
        const queries = [];
        if (splitTrack && splitArtist) {
          queries.push(`${splitTrack} ${splitArtist}`);
        }
        if (cleanTitle && cleanArtist) {
          queries.push(`${cleanTitle} ${cleanArtist}`);
        }
        if (splitTrack) {
          queries.push(splitTrack);
        }
        if (cleanTitle && cleanTitle !== splitTrack) {
          queries.push(cleanTitle);
        }

        const tried = new Set();
        for (const q of queries) {
          const norm = q.trim().toLowerCase();
          if (!norm || tried.has(norm)) continue;
          tried.add(norm);
          data = await searchLrclib(q.trim(), signal, duration);
          if (data) break;
        }
      }

      if (!data || (!data.syncedLyrics && !data.plainLyrics)) {
        renderLyricsPlaceholder('No lyrics found');
        return;
      }

      if (data.syncedLyrics) {
        parsedLyrics = parseLrc(data.syncedLyrics);
        renderLyricsList(parsedLyrics);
        syncLyricsWithPosition(lastMediaSnapshot ? (lastMediaSnapshot.positionSeconds || 0) : 0, true);
      } else if (data.plainLyrics) {
        const plainLines = data.plainLyrics.split('\n').map((l) => l.trim()).filter(Boolean);
        parsedLyrics = plainLines.map((text, idx) => ({ time: idx * 4, text }));
        renderLyricsList(parsedLyrics);
      }
    } catch (err) {
      if (err.name === 'AbortError') return;
      console.warn('[DynamicIsland] Failed to fetch lyrics:', err);
      renderLyricsPlaceholder('Lyrics unavailable');
    }
  }

  function renderLyricsPlaceholder(msg) {
    if (!lyricsScroller) return;
    lyricsScroller.innerHTML = `<div class="di-lyrics-placeholder">${msg}</div>`;
  }

  function renderLyricsList(lyrics) {
    if (!lyricsScroller) return;
    if (!lyrics || lyrics.length === 0) {
      renderLyricsPlaceholder('No lyrics available');
      return;
    }
    lyricsScroller.innerHTML = lyrics
      .map((item, idx) => `<div class="di-lyric-line" data-idx="${idx}">${escapeHtml(item.text)}</div>`)
      .join('');

    lyricsScroller.querySelectorAll('.di-lyric-line').forEach((el) => {
      el.addEventListener('click', (ev) => {
        ev.stopPropagation();
        const idx = parseInt(el.getAttribute('data-idx'), 10);
        if (!Number.isNaN(idx) && parsedLyrics[idx]) {
          const targetPos = parsedLyrics[idx].time;
          if (window.winzen && typeof window.winzen.send === 'function') {
            window.winzen.send('media_seek', { position: targetPos });
          }
        }
      });
    });
  }

  function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  let currentActiveLyricText = '';
  let _lyricMeasureCanvas = null;

  function estimateLyricWidth(text) {
    if (!text) return 100;
    if (!_lyricMeasureCanvas) {
      _lyricMeasureCanvas = document.createElement('canvas');
    }
    const ctx = _lyricMeasureCanvas.getContext('2d');
    if (ctx) {
      ctx.font = '600 12px -apple-system, BlinkMacSystemFont, "Segoe UI Variable Display", "Segoe UI", system-ui, sans-serif';
      const textW = Math.ceil(ctx.measureText(text).width);
      return Math.min(340, Math.max(96, textW + 78));
    }
    const est = Math.round(text.length * 6.5 + 78);
    return Math.min(340, Math.max(96, est));
  }

  function syncLyricsWithPosition(posSeconds, forceScroll = false) {
    if (!lyricsScroller || parsedLyrics.length === 0) return;
    let targetIdx = -1;
    for (let i = 0; i < parsedLyrics.length; i++) {
      if (posSeconds >= parsedLyrics[i].time - 0.25) {
        targetIdx = i;
      } else {
        break;
      }
    }

    if (targetIdx !== activeLyricIndex || forceScroll) {
      activeLyricIndex = targetIdx;
      const activeLine = (targetIdx >= 0 && parsedLyrics[targetIdx]) ? parsedLyrics[targetIdx].text : '';
      currentActiveLyricText = activeLine;

      if (isLyricsPinned && activeLine) {
        const dynW = estimateLyricWidth(activeLine);
        if (colMusicTitle) colMusicTitle.textContent = activeLine;
        if (bgTitle) bgTitle.textContent = activeLine;
        if (diTcTitle) diTcTitle.textContent = activeLine;

        if (window.winzen && typeof window.winzen.send === 'function') {
          window.winzen.send('di_set_lyrics', {
            isPinned: true,
            text: activeLine,
            width: dynW
          });
        }
      }

      const lines = lyricsScroller.querySelectorAll('.di-lyric-line');
      lines.forEach((line, idx) => {
        if (idx === targetIdx) {
          line.classList.add('active');
          line.classList.remove('passed');
          const scrollerH = lyricsScroller.clientHeight;
          const lineTop = line.offsetTop;
          const lineH = line.clientHeight;
          lyricsScroller.scrollTo({
            top: Math.max(0, lineTop - scrollerH / 2 + lineH / 2),
            behavior: forceScroll ? 'auto' : 'smooth'
          });
        } else if (idx < targetIdx) {
          line.classList.remove('active');
          line.classList.add('passed');
        } else {
          line.classList.remove('active', 'passed');
        }
      });
    }
  }

  function updatePillModeUI() {
    if (btnPillMode) {
      btnPillMode.textContent = 'Wide Pill';
      btnPillMode.classList.toggle('active', isMusicPillWideMode);
    }
    if (islandMusicPill) {
      if (isMusicPillWideMode) {
        islandMusicPill.classList.add('is-hovered');
      } else {
        islandMusicPill.classList.remove('is-hovered');
      }
    }
    if (capsule) {
      capsule.classList.toggle('is-wide-pill', isMusicPillWideMode);
    }
    if (window.winzen && typeof window.winzen.send === 'function') {
      window.winzen.send('di_set_music_wide', { isWide: isMusicPillWideMode });
    }
  }

  let musicPillHideTimer = null;

  function setMusicPillRestingVisible(showPill, instant = false) {
    if (!islandMusicPill) return;
    if (musicPillHideTimer) {
      clearTimeout(musicPillHideTimer);
      musicPillHideTimer = null;
    }
    if (diConfig.placement !== 'top-center') {
      islandMusicPill.classList.remove('is-hovered');
      islandMusicPill.classList.add('is-music-absent');
      islandMusicPill.style.display = 'none';
      return;
    }

    const noAnim = instant || diConfig.enableAnimation === false;
    if (showPill) {
      if (islandMusicPill.style.display === 'none') {
        if (noAnim) {
          islandMusicPill.style.transition = 'none';
          islandMusicPill.classList.remove('is-music-absent');
          islandMusicPill.style.display = 'inline-flex';
          void islandMusicPill.offsetWidth;
          islandMusicPill.style.transition = '';
        } else {
          islandMusicPill.classList.add('is-music-absent');
          islandMusicPill.style.display = 'inline-flex';
          void islandMusicPill.offsetWidth;
          requestAnimationFrame(() => {
            islandMusicPill.classList.remove('is-music-absent');
          });
        }
      } else {
        if (noAnim) {
          islandMusicPill.style.transition = 'none';
          islandMusicPill.classList.remove('is-music-absent');
          void islandMusicPill.offsetWidth;
          islandMusicPill.style.transition = '';
        } else {
          islandMusicPill.classList.remove('is-music-absent');
        }
      }
      if (isMusicPillWideMode) {
        islandMusicPill.classList.add('is-hovered');
      }
    } else {
      if (!isMusicPillWideMode) {
        islandMusicPill.classList.remove('is-hovered');
      }
      if (noAnim) {
        islandMusicPill.style.transition = 'none';
        islandMusicPill.classList.add('is-music-absent');
        islandMusicPill.style.display = 'none';
        void islandMusicPill.offsetWidth;
        islandMusicPill.style.transition = '';
      } else if (islandMusicPill.style.display !== 'none') {
        islandMusicPill.classList.add('is-music-absent');
        musicPillHideTimer = setTimeout(() => {
          musicPillHideTimer = null;
          if (islandMusicPill && islandMusicPill.classList.contains('is-music-absent')) {
            islandMusicPill.style.display = 'none';
          }
        }, 320);
      }
    }
  }

  function updateMusicView(media) {
    if (!media) return;
    lastMediaSnapshot = media;

    // 1. Update expanded music card
    if (musicCoverImg && musicCoverPlaceholder) {
      if (media.thumbnailBase64 && media.thumbnailBase64.length > 30) {
        musicCoverImg.src = media.thumbnailBase64;
        musicCoverImg.style.display = 'block';
        musicCoverPlaceholder.style.display = 'none';
      } else {
        musicCoverImg.style.display = 'none';
        musicCoverPlaceholder.style.display = 'block';
      }
    }

    if (musicTitle) musicTitle.textContent = media.title || 'No Media';
    if (musicArtist) musicArtist.textContent = media.artist || 'Unknown Artist';

    if (musicCardWaveform) {
      if (media.isPlaying) {
        musicCardWaveform.classList.add('playing');
      } else {
        musicCardWaveform.classList.remove('playing');
      }
    }

    const pos = media.positionSeconds || 0;
    const dur = media.durationSeconds || 0;
    if (!isScrubbingSeek) {
      if (musicPosition) musicPosition.textContent = formatTime(pos);
      if (musicDuration) musicDuration.textContent = formatTime(dur);

      if (musicSeekFill) {
        const pct = dur > 0 ? Math.min(100, Math.max(0, (pos / dur) * 100)) : 0;
        musicSeekFill.style.width = `${pct}%`;
      }
    }

    if (iconPlay && iconPause) {
      if (media.isPlaying) {
        iconPlay.style.display = 'none';
        iconPause.style.display = 'block';
      } else {
        iconPlay.style.display = 'block';
        iconPause.style.display = 'none';
      }
    }

    // 2. Sync small music pill inner elements (for morphing & background pills)
    syncSmallMusicElements(media, colMusicThumb, colMusicThumbPlaceholder, colMusicWaveform);
    syncSmallMusicElements(media, bgThumb, bgThumbPlaceholder, bgWaveform);
    syncSmallMusicElements(media, diTcThumb, diTcThumbPlaceholder, diTcWaveform);
    if (!isLyricsPinned || !currentActiveLyricText) {
      if (colMusicTitle) colMusicTitle.textContent = media.title || 'Playing';
      if (bgTitle) bgTitle.textContent = media.title || 'Playing';
      if (diTcTitle) diTcTitle.textContent = media.title || 'Playing';
    } else {
      if (colMusicTitle) colMusicTitle.textContent = currentActiveLyricText;
      if (bgTitle) bgTitle.textContent = currentActiveLyricText;
      if (diTcTitle) diTcTitle.textContent = currentActiveLyricText;
    }

    // 3. Lyrics sync / auto-fetch if pinned
    if (!media.hasMedia || (dur > 0 && pos >= dur - 0.3 && !media.isPlaying)) {
      if (currentActiveLyricText || parsedLyrics.length > 0) {
        lyricsTrackKey = '';
        parsedLyrics = [];
        activeLyricIndex = -1;
        flushSmallPillLyrics(media.title || '');
      }
    } else if (isLyricsPinned && media.title) {
      fetchLyricsForTrack(media.title, media.artist, media.durationSeconds);
      syncLyricsWithPosition(pos);
    }

    // 4. Update top-center resting music pill visibility
    if (islandMusicPill && !isExpanded && !isCollapsing) {
      const showPill = diConfig.placement === 'top-center' &&
                       diConfig.enableMusicIsland !== false &&
                       media.hasMedia;
      setMusicPillRestingVisible(showPill, false);
    }
  }

  // Seekbar click & pointer drag handler
  let lastScrubRatio = 0;

  function applySeekFromClientX(clientX, sendToHost) {
    if (!musicSeekTrack) return;
    const rect = musicSeekTrack.getBoundingClientRect();
    if (rect.width <= 0) return;
    const ratio = Math.max(0, Math.min(1, (clientX - rect.left) / rect.width));
    lastScrubRatio = ratio;
    const dur = (lastMediaSnapshot && lastMediaSnapshot.durationSeconds) || 0;
    const targetPos = dur > 0 ? ratio * dur : 0;

    if (musicSeekFill) musicSeekFill.style.width = `${ratio * 100}%`;
    if (musicPosition && dur > 0) musicPosition.textContent = formatTime(targetPos);
    if (lastMediaSnapshot && dur > 0) lastMediaSnapshot.positionSeconds = targetPos;

    if (sendToHost && dur > 0 && window.winzen && typeof window.winzen.send === 'function') {
      window.winzen.send('media_seek', { position: targetPos });
    }
  }

  if (musicSeekTrack) {
    musicSeekTrack.addEventListener('pointerdown', (ev) => {
      ev.stopPropagation();
      ev.preventDefault();
      isScrubbingSeek = true;
      musicSeekTrack.classList.add('scrubbing');
      try { musicSeekTrack.setPointerCapture(ev.pointerId); } catch { }
      applySeekFromClientX(ev.clientX, false);
    });

    musicSeekTrack.addEventListener('pointermove', (ev) => {
      if (!isScrubbingSeek) return;
      ev.stopPropagation();
      applySeekFromClientX(ev.clientX, false);
    });

    const endScrub = (ev) => {
      if (!isScrubbingSeek) return;
      ev.stopPropagation();
      isScrubbingSeek = false;
      musicSeekTrack.classList.remove('scrubbing');
      try { musicSeekTrack.releasePointerCapture(ev.pointerId); } catch { }
      applySeekFromClientX(ev.clientX, true);
    };

    musicSeekTrack.addEventListener('pointerup', endScrub);
    musicSeekTrack.addEventListener('pointercancel', endScrub);
    musicSeekTrack.addEventListener('click', (ev) => {
      ev.stopPropagation();
    });
  }

  // Media playback button events
  if (btnMusicPrev) {
    btnMusicPrev.addEventListener('click', (ev) => {
      ev.stopPropagation();
      if (window.winzen && typeof window.winzen.send === 'function') {
        window.winzen.send('media_previous');
      }
    });
  }

  if (btnMusicPlayPause) {
    btnMusicPlayPause.addEventListener('click', (ev) => {
      ev.stopPropagation();
      if (window.winzen && typeof window.winzen.send === 'function') {
        window.winzen.send('media_play_pause');
      }
    });
  }

  if (btnMusicNext) {
    btnMusicNext.addEventListener('click', (ev) => {
      ev.stopPropagation();
      if (window.winzen && typeof window.winzen.send === 'function') {
        window.winzen.send('media_next');
      }
    });
  }

  // Pill mode toggle (Wide pill <-> Compact pill)
  if (btnPillMode) {
    btnPillMode.addEventListener('click', (ev) => {
      ev.stopPropagation();
      isMusicPillWideMode = !isMusicPillWideMode;
      updatePillModeUI();
    });
  }

  // Pin lyrics toggle (In-Island synchronized lyrics view)
  if (btnPinLyrics) {
    btnPinLyrics.addEventListener('click', (ev) => {
      ev.stopPropagation();
      isLyricsPinned = !isLyricsPinned;
      btnPinLyrics.classList.toggle('active', isLyricsPinned);
      btnPinLyrics.textContent = isLyricsPinned ? 'lyrics pinned' : 'pin lyrics';
      if (capsule) {
        capsule.classList.toggle('lyrics-pinned', isLyricsPinned);
      }
      if (lyricsView) {
        lyricsView.style.display = isLyricsPinned ? 'flex' : 'none';
      }
      if (isLyricsPinned) {
        if (lastMediaSnapshot) {
          fetchLyricsForTrack(lastMediaSnapshot.title, lastMediaSnapshot.artist, lastMediaSnapshot.durationSeconds);
          syncLyricsWithPosition(lastMediaSnapshot.positionSeconds || 0, true);
        }
      } else {
        // Revert pill lyrics to title
        if (colMusicTitle && lastMediaSnapshot) colMusicTitle.textContent = lastMediaSnapshot.title || 'Playing';
        if (bgTitle && lastMediaSnapshot) bgTitle.textContent = lastMediaSnapshot.title || 'Playing';
        if (diTcTitle && lastMediaSnapshot) diTcTitle.textContent = lastMediaSnapshot.title || 'Playing';
        if (window.winzen && typeof window.winzen.send === 'function') {
          window.winzen.send('di_set_lyrics', {
            isPinned: false,
            text: '',
            width: 0
          });
        }
      }
    });
  }

  function restoreTopCenterRestingState() {
    if (!capsule) return;
    capsule.style.transition = 'none';
    capsule.classList.add('instant-setup');
    if (bgPill) {
      bgPill.style.transition = 'none';
      bgPill.classList.add('instant-setup');
      bgPill.style.display = 'none';
    }
    if (pillsRow) pillsRow.classList.remove('stage-expanded');

    if (collapsedInner) collapsedInner.style.display = 'inline-flex';
    if (collapsedMusicInner) collapsedMusicInner.style.display = 'none';

    const clockW = getStandardClockWidth();
    capsule.style.setProperty('--di-collapsed-width', `${clockW}px`);
    capsule.style.setProperty('--di-pill-offset-x', '0px');

    if (islandMusicPill) {
      const showPill = diConfig.placement === 'top-center' &&
                       diConfig.enableMusicIsland !== false &&
                       !!(lastMediaSnapshot && lastMediaSnapshot.hasMedia);
      setMusicPillRestingVisible(showPill, true);
    }

    void capsule.offsetWidth;
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        capsule.style.transition = '';
        capsule.classList.remove('instant-setup');
        if (bgPill) {
          bgPill.style.transition = '';
          bgPill.classList.remove('instant-setup');
        }
      });
    });
  }

  function toggleExpand(force, options = {}) {
    if (!capsule) return;
    const shouldExpand = (typeof force === 'boolean') ? force : !isExpanded;
    if (shouldExpand === isExpanded && !isCollapsing) return;

    if (closeTimeout) {
      clearTimeout(closeTimeout);
      closeTimeout = null;
    }
    clearTimeout(musicHoverTimer);
    if (islandMusicPill) islandMusicPill.classList.remove('is-hovered');

    const hasAnim = diConfig.enableAnimation !== false;
    if (!hasAnim) {
      capsule.classList.add('no-anim');
    } else {
      capsule.classList.remove('no-anim');
    }

    if (shouldExpand) {
      isExpanded = true;
      isCollapsing = false;

      if (options.target) {
        currentTarget = options.target.toLowerCase();
      }

      if (diConfig.placement === 'top-center') {
        currentSlot = 'center';
      } else if (options.slot) {
        currentSlot = options.slot.toLowerCase();
      } else if (diConfig.taskbarSlot) {
        currentSlot = diConfig.taskbarSlot.toLowerCase();
      }

      if (options.dockEdge) {
        currentDockEdge = options.dockEdge.toLowerCase();
      }

      const collapsedH = (options.anchorHeight && options.anchorHeight > 10) ? Math.round(options.anchorHeight) : 30;
      capsule.style.setProperty('--di-collapsed-height', `${collapsedH}px`);
      capsule.style.setProperty('--di-collapsed-radius', `${Math.round(collapsedH / 2)}px`);

      const container = document.getElementById('islandContainer') || document.querySelector('.island-container');
      if (options.taskbarHeight && options.anchorHeight) {
        const topPad = (typeof options.anchorY === 'number' && options.anchorY >= 0)
          ? Math.round(options.anchorY)
          : Math.max(0, Math.round((options.taskbarHeight - collapsedH) / 2));
        const bottomPad = Math.max(0, Math.round(options.taskbarHeight - topPad - collapsedH));
        document.documentElement.style.setProperty('--di-dock-top-padding', `${topPad}px`);
        document.documentElement.style.setProperty('--di-dock-bottom-padding', `${bottomPad}px`);
        if (container) {
          container.style.setProperty('--di-dock-top-padding', `${topPad}px`);
          container.style.setProperty('--di-dock-bottom-padding', `${bottomPad}px`);
        }
      } else if (diConfig.placement === 'top-center') {
        document.documentElement.style.setProperty('--di-dock-top-padding', '8px');
        if (container) container.style.setProperty('--di-dock-top-padding', '8px');
      }

      if (container) {
        if (currentDockEdge === 'bottom' && diConfig.placement !== 'top-center') {
          container.classList.add('dock-bottom');
        } else {
          container.classList.remove('dock-bottom');
        }
        container.classList.remove('slot-left', 'slot-center', 'slot-right');
        container.classList.add(`slot-${currentSlot}`);
      }

      // Determine whether 2 pills (Clock + Music) are active
      const hasSecondIsland = (typeof options.hasMusicPill === 'boolean')
        ? options.hasMusicPill
        : (diConfig.enableMusicIsland !== false && !!(lastMediaSnapshot && lastMediaSnapshot.hasMedia));

      const clockW = (options.clockWidth && options.clockWidth > 40)
        ? Math.round(options.clockWidth)
        : getStandardClockWidth();
      currentClockWidth = clockW;
      const isWidePill = typeof options.isMusicWide === 'boolean' ? options.isMusicWide : isMusicPillWideMode;
      const musicW = (options.musicWidth && options.musicWidth > 50)
        ? Math.round(options.musicWidth)
        : (isWidePill ? 210 : 68);

      let clockPos = 0;
      let musicPos = 0;
      if (hasSecondIsland) {
        if (currentSlot === 'left') {
          clockPos = 0;
          musicPos = clockW + 8;
        } else if (currentSlot === 'right') {
          clockPos = -(musicW + 8);
          musicPos = 0;
        } else {
          clockPos = -Math.round((musicW + 8) / 2);
          musicPos = Math.round((clockW + 8) / 2);
        }
      }

      if (window.winzen && typeof window.winzen.send === 'function') {
        window.winzen.send('di_expand', { expanded: true });
      }

      // 1. Snap capsule and background pill to their exact starting pill positions on Frame 0
      capsule.style.transition = 'none';
      capsule.classList.add('instant-setup');
      capsule.classList.remove('expanded');
      if (pillsRow) pillsRow.classList.remove('stage-expanded');
      if (islandMusicPill) islandMusicPill.style.display = 'none';

      if (currentTarget === 'music') {
        if (collapsedInner) collapsedInner.style.display = 'none';
        if (collapsedMusicInner) collapsedMusicInner.style.display = 'flex';
        if (expandedContent) expandedContent.style.display = 'none';
        if (expandedMusicContent) expandedMusicContent.style.display = 'flex';
        updateMusicView(lastMediaSnapshot);

        capsule.style.setProperty('--di-collapsed-width', `${musicW}px`);
        capsule.style.setProperty('--di-pill-offset-x', `${musicPos}px`);
        capsule.classList.toggle('is-wide-pill', isWidePill);

        if (bgPill) {
          if (hasSecondIsland) {
            bgPill.style.transition = 'none';
            bgPill.classList.add('instant-setup');
            bgPill.style.display = 'inline-flex';
            bgPill.classList.remove('is-wide-pill');
            if (bgClockInner) bgClockInner.style.display = 'inline-flex';
            if (bgMusicInner) bgMusicInner.style.display = 'none';
            bgPill.style.setProperty('--di-bg-pill-width', `${clockW}px`);
            bgPill.style.setProperty('--di-bg-pill-offset-x', `${clockPos}px`);
          } else {
            bgPill.style.display = 'none';
          }
        }
      } else {
        if (collapsedInner) collapsedInner.style.display = 'inline-flex';
        if (collapsedMusicInner) collapsedMusicInner.style.display = 'none';
        if (expandedContent) expandedContent.style.display = 'flex';
        if (expandedMusicContent) expandedMusicContent.style.display = 'none';
        updateClock();

        capsule.style.setProperty('--di-collapsed-width', `${clockW}px`);
        capsule.style.setProperty('--di-pill-offset-x', `${clockPos}px`);
        capsule.classList.remove('is-wide-pill');

        if (bgPill) {
          if (hasSecondIsland) {
            bgPill.style.transition = 'none';
            bgPill.classList.add('instant-setup');
            bgPill.style.display = 'inline-flex';
            bgPill.classList.toggle('is-wide-pill', isWidePill);
            if (bgClockInner) bgClockInner.style.display = 'none';
            if (bgMusicInner) bgMusicInner.style.display = 'flex';
            bgPill.style.setProperty('--di-bg-pill-width', `${musicW}px`);
            bgPill.style.setProperty('--di-bg-pill-offset-x', `${musicPos}px`);
          } else {
            bgPill.style.display = 'none';
          }
        }
      }

      // Force browser layout at starting pill positions
      void capsule.offsetWidth;
      if (bgPill) void bgPill.offsetWidth;

      // 2. Re-enable transitions and animate from pill position into centered grow position while background pill merges
      capsule.style.transition = '';
      capsule.classList.remove('instant-setup');
      if (bgPill) {
        bgPill.style.transition = '';
        bgPill.classList.remove('instant-setup');
      }

      capsule.classList.add('expanded');
      if (pillsRow) pillsRow.classList.add('stage-expanded');
    } else {
      isExpanded = false;
      isCollapsing = true;

      // Ensure dimensions and positions reflect wide pill mode during shrink
      const isWidePill = typeof options.isMusicWide === 'boolean' ? options.isMusicWide : isMusicPillWideMode;
      const musicW = (options.musicWidth && options.musicWidth > 50)
        ? Math.round(options.musicWidth)
        : (isWidePill ? 210 : 68);
      const clockW = (options.clockWidth && options.clockWidth > 40)
        ? Math.round(options.clockWidth)
        : currentClockWidth;

      const hasSecondIsland = (typeof options.hasMusicPill === 'boolean')
        ? options.hasMusicPill
        : (diConfig.enableMusicIsland !== false && !!(lastMediaSnapshot && lastMediaSnapshot.hasMedia));

      let clockPos = 0;
      let musicPos = 0;
      if (hasSecondIsland) {
        if (currentSlot === 'left') {
          clockPos = 0;
          musicPos = clockW + 8;
        } else if (currentSlot === 'right') {
          clockPos = -(musicW + 8);
          musicPos = 0;
        } else {
          clockPos = -Math.round((musicW + 8) / 2);
          musicPos = Math.round((clockW + 8) / 2);
        }
      }

      if (currentTarget === 'music') {
        capsule.style.setProperty('--di-collapsed-width', `${musicW}px`);
        capsule.style.setProperty('--di-pill-offset-x', `${musicPos}px`);
        capsule.classList.toggle('is-wide-pill', isWidePill);
        if (bgPill && hasSecondIsland) {
          bgPill.style.setProperty('--di-bg-pill-width', `${clockW}px`);
          bgPill.style.setProperty('--di-bg-pill-offset-x', `${clockPos}px`);
          bgPill.classList.remove('is-wide-pill');
        }
      } else {
        capsule.style.setProperty('--di-collapsed-width', `${clockW}px`);
        capsule.style.setProperty('--di-pill-offset-x', `${clockPos}px`);
        capsule.classList.remove('is-wide-pill');
        if (bgPill && hasSecondIsland) {
          bgPill.style.setProperty('--di-bg-pill-width', `${musicW}px`);
          bgPill.style.setProperty('--di-bg-pill-offset-x', `${musicPos}px`);
          bgPill.classList.toggle('is-wide-pill', isWidePill);
        }
      }

      // Animate shrink back to the active pill's standby position while background pill unmerges
      capsule.classList.remove('expanded');
      if (pillsRow) pillsRow.classList.remove('stage-expanded');

      if (window.winzen && typeof window.winzen.send === 'function') {
        window.winzen.send('di_expand', { expanded: false });
      }

      if (hasAnim && !options.instant) {
        closeTimeout = setTimeout(() => {
          if (!isExpanded) {
            isCollapsing = false;
            if (diConfig.placement === 'top-center') {
              restoreTopCenterRestingState();
            }
            if (window.winzen && typeof window.winzen.send === 'function') {
              window.winzen.send('di_close_complete');
            }
          }
        }, 440);
      } else {
        isCollapsing = false;
        restoreTopCenterRestingState();
        if (window.winzen && typeof window.winzen.send === 'function') {
          window.winzen.send('di_close_complete');
        }
      }
    }
  }

  setInterval(() => {
    updateClock();
    if (lastMediaSnapshot && lastMediaSnapshot.hasMedia &&
        lastMediaSnapshot.isPlaying && lastMediaSnapshot.durationSeconds > 0) {
      if (!isScrubbingSeek) {
        const nextPos = Math.min(lastMediaSnapshot.durationSeconds, (lastMediaSnapshot.positionSeconds || 0) + 1);
        lastMediaSnapshot.positionSeconds = nextPos;
        if (isExpanded && currentTarget === 'music') {
          if (musicPosition) musicPosition.textContent = formatTime(nextPos);
          if (musicSeekFill) {
            const pct = Math.min(100, Math.max(0, (nextPos / lastMediaSnapshot.durationSeconds) * 100));
            musicSeekFill.style.width = `${pct}%`;
          }
        }
        if (isLyricsPinned) {
          syncLyricsWithPosition(nextPos);
        }
      }
    }
  }, 1000);
  updateClock();

  function applyState(state) {
    if (!state) return;
    const s = state.Settings || state.settings || state;
    const di = s.DynamicIsland || s.dynamicIsland;
    const edge = (s.DockEdge || s.dockEdge || 'bottom').toLowerCase();
    globalDateFormat = s.DateFormat || s.dateFormat || globalDateFormat;

    const widgets = state.Widgets || state.widgets || [];
    const overrides = s.WidgetOverrides || s.widgetOverrides || {};
    const battWidget = Array.isArray(widgets)
      ? widgets.find((w) => w && (w.id === 'community.zen-battery' || w.id === 'zen-battery' || w.Id === 'community.zen-battery' || w.Id === 'zen-battery'))
      : null;
    const battOverride = overrides['community.zen-battery'] || overrides['zen-battery'];
    const battStyle = (battWidget && (battWidget.batteryIconStyle || battWidget.BatteryIconStyle))
      || (battOverride && (battOverride.batteryIconStyle || battOverride.BatteryIconStyle))
      || 'win11';
    applyBatteryStyle(battStyle);

    if (di) {
      diConfig = {
        ...diConfig,
        ...di
      };
      if (diConfig.taskbarSlot) {
        currentSlot = diConfig.taskbarSlot.toLowerCase();
      }
      if (diConfig.placement === 'top-center') {
        currentDockEdge = 'top';
        const container = document.getElementById('islandContainer') || document.querySelector('.island-container');
        if (container) {
          container.classList.remove('dock-bottom', 'slot-left', 'slot-right');
          container.classList.add('slot-center');
          container.style.setProperty('--di-dock-top-padding', '8px');
        }
        document.documentElement.style.setProperty('--di-dock-top-padding', '8px');
        if (!isExpanded && !isCollapsing) {
          restoreTopCenterRestingState();
        }
      } else {
        currentDockEdge = edge;
      }
      if (capsule) {
        if (diConfig.enableAnimation === false) {
          capsule.classList.add('no-anim');
        } else {
          capsule.classList.remove('no-anim');
        }
      }
      updateClock();
      updateCollapsedWidth();
      if (lastMediaSnapshot) {
        updateMusicView(lastMediaSnapshot);
      }
    }
  }

  window.addEventListener('winzen:state', (e) => {
    applyState(e.detail);
  });

  window.addEventListener('winzen:metrics', (e) => {
    updateBattery(e.detail);
    if (e.detail && e.detail.media) {
      updateMusicView(e.detail.media);
    }
  });

  if (window.winzen && typeof window.winzen.onState === 'function') {
    window.winzen.onState((state) => applyState(state));
  }

  function handleHostMessage(msg) {
    if (!msg) return;
    if (msg.type === 'state_update' || msg.type === 'state') {
      applyState(msg.payload || msg.data);
      return;
    }
    if (msg.type === 'metrics_update' || msg.type === 'metrics') {
      const payload = msg.payload || msg.data;
      updateBattery(payload);
      if (payload && payload.media) {
        updateMusicView(payload.media);
      }
      return;
    }
    if (msg.type === 'set_mode') {
      toggleExpand(!!msg.expanded, {
        target: msg.target || 'clock',
        instant: !!msg.instant,
        anchorWidth: msg.anchorWidth,
        clockWidth: msg.clockWidth,
        musicWidth: msg.musicWidth,
        hasMusicPill: msg.hasMusicPill,
        isMusicWide: msg.isMusicWide,
        anchorY: msg.anchorY,
        anchorHeight: msg.anchorHeight,
        dockEdge: msg.dockEdge,
        slot: msg.slot,
        taskbarHeight: msg.taskbarHeight
      });
    }
  }

  window.addEventListener('winzen:message', (e) => {
    handleHostMessage(e.detail);
  });
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (e) => {
      handleHostMessage(e.data);
    });
  }

  if (capsule) {
    capsule.addEventListener('click', (ev) => {
      ev.stopPropagation();
      if (isExpanded && ev.target && ev.target.closest('.di-music-timeline-row, .di-music-controls-row, .di-music-actions-row, .di-music-lyrics-view')) {
        return;
      }
      if (isExpanded) {
        toggleExpand(false);
      } else {
        toggleExpand(true, { target: 'clock' });
      }
    });
  }

  if (islandMusicPill) {
    islandMusicPill.addEventListener('mouseenter', () => {
      if (isExpanded || isCollapsing || isMusicPillWideMode) return;
      clearTimeout(musicHoverTimer);
      musicHoverTimer = setTimeout(() => {
        if (!isExpanded && !isCollapsing && !isMusicPillWideMode) {
          islandMusicPill.classList.add('is-hovered');
        }
      }, 260);
    });

    islandMusicPill.addEventListener('mouseleave', () => {
      if (isMusicPillWideMode) return;
      clearTimeout(musicHoverTimer);
      islandMusicPill.classList.remove('is-hovered');
    });

    islandMusicPill.addEventListener('click', (ev) => {
      ev.stopPropagation();
      clearTimeout(musicHoverTimer);
      if (!isMusicPillWideMode) {
        islandMusicPill.classList.remove('is-hovered');
      }
      toggleExpand(true, { target: 'music' });
    });
  }

  document.addEventListener('click', (ev) => {
    if (isExpanded) {
      if (capsule && !capsule.contains(ev.target)) {
        toggleExpand(false);
      }
    }
  });

  document.addEventListener('keydown', (ev) => {
    if (ev.key === 'Escape' && isExpanded) {
      toggleExpand(false);
    }
  });
})();
