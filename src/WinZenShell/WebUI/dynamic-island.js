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
  const bgWaveform = document.getElementById('diBgWaveform');

  const cardTime = document.getElementById('diCardTime');
  const cardSec = document.getElementById('diCardSeconds');
  const cardBatteryPct = document.getElementById('diCardBatteryPct');
  const cardBatteryFill = document.getElementById('diCardBatteryFill');
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

  function formatTime(sec) {
    if (typeof sec !== 'number' || isNaN(sec) || sec < 0) sec = 0;
    const m = Math.floor(sec / 60);
    const s = Math.floor(sec % 60);
    return `${m}:${String(s).padStart(2, '0')}`;
  }

  function getStandardClockWidth() {
    return diConfig.showDate ? 196 : (diConfig.showSeconds ? 100 : 78);
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

    const dayStr = String(now.getDate()).padStart(2, '0');
    const monthStr = String(now.getMonth() + 1).padStart(2, '0');
    const yearStr = String(now.getFullYear());
    const formattedDate = `${dayStr}-${monthStr}-${yearStr}`;
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

  function updateBattery(metrics) {
    if (!metrics) return;
    let pct = 100;
    if (typeof metrics.batteryPercent === 'number' && metrics.batteryPercent >= 0) {
      pct = metrics.batteryPercent;
    }
    if (cardBatteryPct) cardBatteryPct.textContent = `${pct}%`;
    if (cardBatteryFill) cardBatteryFill.style.width = `${Math.max(5, Math.min(100, pct))}%`;
  }

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
    if (musicPosition) musicPosition.textContent = formatTime(pos);
    if (musicDuration) musicDuration.textContent = formatTime(dur);

    if (musicSeekFill) {
      const pct = dur > 0 ? Math.min(100, Math.max(0, (pos / dur) * 100)) : 0;
      musicSeekFill.style.width = `${pct}%`;
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
    if (diTcTitle) diTcTitle.textContent = media.title || 'Playing';

    // 3. Update top-center resting music pill visibility
    if (islandMusicPill && !isExpanded && !isCollapsing) {
      const showPill = diConfig.placement === 'top-center' &&
                       diConfig.enableMusicIsland !== false &&
                       media.hasMedia;
      islandMusicPill.style.display = showPill ? 'inline-flex' : 'none';
    }
  }

  // Seekbar click handler
  if (musicSeekTrack) {
    musicSeekTrack.addEventListener('click', (ev) => {
      ev.stopPropagation();
      const rect = musicSeekTrack.getBoundingClientRect();
      const clickX = ev.clientX - rect.left;
      const ratio = Math.max(0, Math.min(1, clickX / rect.width));
      const dur = (lastMediaSnapshot && lastMediaSnapshot.durationSeconds) || 0;
      if (dur > 0 && window.winzen && typeof window.winzen.send === 'function') {
        window.winzen.send('media_seek', { position: ratio * dur });
      }
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
      islandMusicPill.style.display = showPill ? 'inline-flex' : 'none';
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

      if (options.slot) {
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
      const musicW = 68;

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

        if (bgPill) {
          if (hasSecondIsland) {
            bgPill.style.transition = 'none';
            bgPill.classList.add('instant-setup');
            bgPill.style.display = 'inline-flex';
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

        if (bgPill) {
          if (hasSecondIsland) {
            bgPill.style.transition = 'none';
            bgPill.classList.add('instant-setup');
            bgPill.style.display = 'inline-flex';
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
        }, 425);
      } else {
        isCollapsing = false;
        restoreTopCenterRestingState();
        if (window.winzen && typeof window.winzen.send === 'function') {
          window.winzen.send('di_close_complete');
        }
      }
    }
  }

  setInterval(updateClock, 1000);
  updateClock();

  function applyState(state) {
    if (!state) return;
    const s = state.Settings || state.settings || state;
    const di = s.DynamicIsland || s.dynamicIsland;
    const edge = (s.DockEdge || s.dockEdge || 'bottom').toLowerCase();

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
      if (isExpanded) {
        toggleExpand(false);
      } else {
        toggleExpand(true, { target: 'clock' });
      }
    });
  }

  if (islandMusicPill) {
    islandMusicPill.addEventListener('mouseenter', () => {
      if (isExpanded || isCollapsing) return;
      clearTimeout(musicHoverTimer);
      musicHoverTimer = setTimeout(() => {
        if (!isExpanded && !isCollapsing) {
          islandMusicPill.classList.add('is-hovered');
        }
      }, 260);
    });

    islandMusicPill.addEventListener('mouseleave', () => {
      clearTimeout(musicHoverTimer);
      islandMusicPill.classList.remove('is-hovered');
    });

    islandMusicPill.addEventListener('click', (ev) => {
      ev.stopPropagation();
      clearTimeout(musicHoverTimer);
      islandMusicPill.classList.remove('is-hovered');
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
