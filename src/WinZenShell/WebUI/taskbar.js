function initTaskbar() {
  const slotLeft = document.getElementById('slot-left');
  const slotCenter = document.getElementById('slot-center');
  const slotRight = document.getElementById('slot-right');
  const btnOpenStudio = document.getElementById('btnOpenStudio');
  const taskbarBar = document.getElementById('taskbarBar');
  const taskbarWallpaperLayer = document.getElementById('taskbarWallpaperLayer');
  const taskbarSurfaceLayer = document.getElementById('taskbarSurfaceLayer');

  let lastWidgetsSignature = '';
  let currentTheme = 'dark';
  let currentAccent = '#3684E3';
  let currentRadius = 12;
  let currentDockEdge = 'bottom';
  let currentAnimations = true;

  let currentDynamicIslandConfig = {
    enabled: false,
    placement: 'taskbar',
    taskbarSlot: 'center',
    timeFormat: '24h',
    showSeconds: false,
    showDate: true,
    enableAnimation: true,
    enableMusicIsland: true
  };
  let dynamicIslandEl = null;
  let dynamicIslandClockTimer = null;
  let isDynamicIslandExpanded = false;
  let lastMediaSnapshot = null;
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

  function updateDynamicIslandMedia(media) {
    const musicEl = document.getElementById('shell-dynamic-island-music');
    if (!musicEl) return;
    const isEnabled = currentDynamicIslandConfig.enabled &&
                      currentDynamicIslandConfig.enableMusicIsland !== false &&
                      currentDynamicIslandConfig.placement !== 'top-center';
    const noAnim = currentDynamicIslandConfig.enableAnimation === false || !currentAnimations;

    if (!isEnabled || !media || !media.hasMedia) {
      musicEl.classList.remove('is-hovered');
      if (noAnim || !isEnabled) {
        musicEl.classList.add('is-music-absent');
        musicEl.style.display = 'none';
      } else if (musicEl.style.display !== 'none') {
        musicEl.classList.add('is-music-absent');
      }
      return;
    }

    if (musicEl.style.display === 'none') {
      if (noAnim) {
        musicEl.classList.remove('is-music-absent');
        musicEl.style.display = 'inline-flex';
      } else {
        musicEl.classList.add('is-music-absent');
        musicEl.style.display = 'inline-flex';
        void musicEl.offsetWidth;
        requestAnimationFrame(() => {
          musicEl.classList.remove('is-music-absent');
        });
      }
    } else {
      musicEl.classList.remove('is-music-absent');
    }

    const thumbImg = document.getElementById('tbDiMusicThumb');
    const thumbPlaceholder = document.getElementById('tbDiMusicThumbPlaceholder');
    if (thumbImg && thumbPlaceholder) {
      if (media.thumbnailBase64 && media.thumbnailBase64.length > 30) {
        thumbImg.src = media.thumbnailBase64;
        thumbImg.style.display = 'block';
        thumbPlaceholder.style.display = 'none';
      } else {
        thumbImg.style.display = 'none';
        thumbPlaceholder.style.display = 'block';
      }
    }

    const titleEl = document.getElementById('tbDiMusicTitle');
    if (titleEl) {
      titleEl.textContent = media.title || 'Playing';
    }

    const waveEl = document.getElementById('tbDiMusicWaveform');
    if (waveEl) {
      if (media.isPlaying) {
        waveEl.classList.add('playing');
      } else {
        waveEl.classList.remove('playing');
      }
    }
  }

  function createDynamicIslandElement() {
    const wrapper = document.createElement('div');
    wrapper.id = 'shell-dynamic-island-wrapper';
    wrapper.className = 'shell-dynamic-island-wrapper';

    // 1. Clock Pill
    const el = document.createElement('div');
    el.id = 'shell-dynamic-island';
    el.className = 'shell-dynamic-island pitch-black';
    if (currentDynamicIslandConfig.enableAnimation === false) {
      el.classList.add('no-anim');
    }
    el.setAttribute('role', 'button');
    el.setAttribute('tabindex', '0');
    el.setAttribute('aria-label', 'Dynamic Island Clock');

    const colDiv = document.createElement('div');
    colDiv.className = 'di-collapsed-content';

    const timeSpan = document.createElement('span');
    timeSpan.className = 'di-time';
    timeSpan.id = 'tbDiTime';

    const secSpan = document.createElement('span');
    secSpan.className = 'di-seconds';
    secSpan.id = 'tbDiSeconds';

    const sepSpan = document.createElement('span');
    sepSpan.className = 'di-sep';
    sepSpan.id = 'tbDiSep';

    const dateSpan = document.createElement('span');
    dateSpan.className = 'di-date';
    dateSpan.id = 'tbDiDate';

    colDiv.appendChild(timeSpan);
    colDiv.appendChild(secSpan);
    colDiv.appendChild(sepSpan);
    colDiv.appendChild(dateSpan);
    el.appendChild(colDiv);

    let musicHoverTimer = null;

    function triggerExpand(target) {
      clearTimeout(musicHoverTimer);
      musicEl.style.transition = 'none';
      musicEl.classList.remove('is-hovered');
      void musicEl.offsetWidth;
      musicEl.style.transition = '';

      const hasMusicPill = musicEl.style.display !== 'none' && !musicEl.classList.contains('is-music-absent');
      const clockRect = el.getBoundingClientRect();
      const wrapRect = wrapper.getBoundingClientRect();
      const clockW = Math.round(clockRect.width) || 78;
      const musicW = 68;
      const effectiveW = hasMusicPill ? (clockW + 8 + musicW) : clockW;

      if (window.winzen && typeof window.winzen.send === 'function') {
        window.winzen.send('toggle_dynamic_island_expand', {
          target,
          x: wrapRect.left,
          y: clockRect.top,
          width: effectiveW,
          height: clockRect.height,
          clockWidth: clockW,
          musicWidth: hasMusicPill ? musicW : 0,
          hasMusicPill: hasMusicPill,
          slot: currentDynamicIslandConfig.taskbarSlot || 'center'
        });
      }
    }

    window.winzenUnmergeDynamicIsland = () => {
      wrapper.classList.remove('merging');
    };

    el.addEventListener('click', (ev) => {
      ev.stopPropagation();
      triggerExpand('clock');
    });

    el.addEventListener('keydown', (ev) => {
      if (ev.key === 'Enter' || ev.key === ' ') {
        ev.preventDefault();
        ev.stopPropagation();
        triggerExpand('clock');
      }
    });

    wrapper.appendChild(el);

    // 2. Music Pill
    const musicEl = document.createElement('div');
    musicEl.id = 'shell-dynamic-island-music';
    musicEl.className = 'shell-dynamic-island-music pitch-black';
    musicEl.setAttribute('role', 'button');
    musicEl.setAttribute('tabindex', '0');
    musicEl.setAttribute('aria-label', 'Now Playing');
    musicEl.style.display = 'none';

    const thumbContainer = document.createElement('div');
    thumbContainer.className = 'di-music-thumb-container';

    const thumbImg = document.createElement('img');
    thumbImg.id = 'tbDiMusicThumb';
    thumbImg.className = 'di-music-thumb';
    thumbImg.alt = 'Album Art';
    thumbImg.style.display = 'none';

    const thumbPlaceholder = document.createElement('div');
    thumbPlaceholder.id = 'tbDiMusicThumbPlaceholder';
    thumbPlaceholder.className = 'di-music-thumb-placeholder';

    thumbContainer.appendChild(thumbImg);
    thumbContainer.appendChild(thumbPlaceholder);

    const titleSpan = document.createElement('span');
    titleSpan.id = 'tbDiMusicTitle';
    titleSpan.className = 'di-music-title';
    titleSpan.textContent = 'Song Title';

    const waveDiv = document.createElement('div');
    waveDiv.id = 'tbDiMusicWaveform';
    waveDiv.className = 'di-music-waveform';
    for (let i = 0; i < 5; i++) {
      const bar = document.createElement('span');
      bar.className = 'di-wave-bar';
      waveDiv.appendChild(bar);
    }

    musicEl.appendChild(thumbContainer);
    musicEl.appendChild(titleSpan);
    musicEl.appendChild(waveDiv);

    musicEl.addEventListener('mouseenter', () => {
      if (wrapper.classList.contains('island-hidden')) return;
      clearTimeout(musicHoverTimer);
      musicHoverTimer = setTimeout(() => {
        if (!wrapper.classList.contains('island-hidden')) {
          musicEl.classList.add('is-hovered');
        }
      }, 260);
    });

    musicEl.addEventListener('mouseleave', () => {
      clearTimeout(musicHoverTimer);
      musicEl.classList.remove('is-hovered');
    });

    musicEl.addEventListener('click', (ev) => {
      ev.stopPropagation();
      triggerExpand('music');
    });

    musicEl.addEventListener('keydown', (ev) => {
      if (ev.key === 'Enter' || ev.key === ' ') {
        ev.preventDefault();
        ev.stopPropagation();
        triggerExpand('music');
      }
    });

    wrapper.appendChild(musicEl);

    if (lastMediaSnapshot) {
      updateDynamicIslandMedia(lastMediaSnapshot);
    }

    return wrapper;
  }

  function toggleDynamicIslandExpand(force) {
    if (!dynamicIslandEl) return;
    const shouldExpand = (typeof force === 'boolean') ? force : !isDynamicIslandExpanded;
    if (isDynamicIslandExpanded === shouldExpand) return;
    isDynamicIslandExpanded = shouldExpand;

    if (currentDynamicIslandConfig.enableAnimation === false) {
      dynamicIslandEl.classList.add('no-anim');
    } else {
      dynamicIslandEl.classList.remove('no-anim');
    }

    if (shouldExpand) {
      dynamicIslandEl.classList.add('expanded');
      updateDynamicIslandClock();
    } else {
      dynamicIslandEl.classList.remove('expanded');
    }
  }

  // Collapse overlay on click outside or escape
  document.addEventListener('click', (ev) => {
    if (window.winzen && typeof window.winzen.send === 'function') {
      window.winzen.send('taskbar_clicked_outside');
    }
  });

  document.addEventListener('keydown', (ev) => {
    if (ev.key === 'Escape') {
      if (window.winzen && typeof window.winzen.send === 'function') {
        window.winzen.send('taskbar_clicked_outside');
      }
    }
  });

  function updateDynamicIslandClock() {
    if (!dynamicIslandEl) return;
    const timeSpan = dynamicIslandEl.querySelector('#tbDiTime');
    const secSpan = dynamicIslandEl.querySelector('#tbDiSeconds');
    const sepSpan = dynamicIslandEl.querySelector('#tbDiSep');
    const dateSpan = dynamicIslandEl.querySelector('#tbDiDate');

    const expTime = dynamicIslandEl.querySelector('#tbDiExpTime');
    const expSec = dynamicIslandEl.querySelector('#tbDiExpSeconds');
    const expDate = dynamicIslandEl.querySelector('#tbDiExpDate');
    const expBadge = dynamicIslandEl.querySelector('#tbDiExpBadge');

    const now = new Date();
    const is12h = currentDynamicIslandConfig.timeFormat === '12h';

    let hours = now.getHours();
    let ampm = '';
    if (is12h) {
      ampm = hours >= 12 ? ' PM' : ' AM';
      hours = hours % 12;
      if (hours === 0) hours = 12;
    }

    const minStr = String(now.getMinutes()).padStart(2, '0');
    const hrStr = is12h ? String(hours) : String(hours).padStart(2, '0');
    const secStr = `.${String(now.getSeconds()).padStart(2, '0')}`;

    const formattedDate = formatDateWithTemplate(now, globalDateFormat);

    // Collapsed view clock
    if (timeSpan) timeSpan.textContent = `${hrStr}:${minStr}${ampm}`;

    if (secSpan) {
      if (currentDynamicIslandConfig.showSeconds) {
        secSpan.style.display = 'inline';
        secSpan.textContent = secStr;
      } else {
        secSpan.style.display = 'none';
      }
    }

    if (dateSpan) {
      if (currentDynamicIslandConfig.showDate) {
        dateSpan.style.display = 'inline';
        dateSpan.textContent = formattedDate;
      } else {
        dateSpan.style.display = 'none';
      }
    }
    if (sepSpan) {
      sepSpan.style.display = 'none';
    }

    // Expanded view clock and date
    if (expTime) expTime.textContent = `${hrStr}:${minStr}${ampm}`;
    if (expSec) expSec.textContent = secStr;
    if (expDate) expDate.textContent = now.toLocaleDateString([], { month: 'short', day: 'numeric', year: 'numeric' });
    if (expBadge) expBadge.textContent = now.toLocaleDateString([], { weekday: 'long' }).toUpperCase();
  }

  if (btnOpenStudio) {
    btnOpenStudio.addEventListener('click', () => {
      window.winzen.shell.openSettings();
    });
  }

  window.addEventListener('winzen:state', (e) => {
    const state = e.detail;
    if (!state || !Array.isArray(state.widgets)) return;

    const settings = state.settings || {};
    currentTheme = settings.theme || 'dark';
    currentAccent = settings.accentColor || '#3684E3';
    currentRadius = settings.taskbarCornerRadiusPx ?? 12;

    document.documentElement.setAttribute('data-theme', currentTheme);
    document.body.className = currentTheme === 'light' ? 'theme-light' : 'theme-dark';
    document.documentElement.style.setProperty('--winzen-accent', currentAccent);
    document.documentElement.style.setProperty('--winzen-border-accent', currentAccent);
    document.documentElement.style.setProperty('--win11-accent', currentAccent);

    const isFloating = (settings.floatingMarginPx || 0) > 0;
    const isFloatingRounded = isFloating && currentRadius > 0;
    const bgOpacityPct = Math.max(0, Math.min(100, settings.backgroundOpacity ?? 100));
    const bgBlurPct = Math.max(0, Math.min(100, settings.backgroundBlurStrength ?? 60));
    const bgAlpha = (bgOpacityPct / 100).toFixed(2);
    const isLightTheme = currentTheme === 'light';
    const surfaceBg = isLightTheme ? `rgba(243, 243, 243, ${bgAlpha})` : `rgba(28, 28, 28, ${bgAlpha})`;
    const htmlBg = (bgOpacityPct >= 100 && !isFloatingRounded) ? (isLightTheme ? '#f3f3f3' : '#1c1c1c') : 'transparent';
    document.documentElement.style.setProperty('--taskbar-surface-bg', surfaceBg);
    document.documentElement.style.setProperty('--taskbar-html-bg', htmlBg);

    const borderAlpha = bgOpacityPct === 0 ? 0 : +(0.08 * (bgOpacityPct / 100)).toFixed(4);
    const borderTopColor = borderAlpha <= 0.0001
      ? 'transparent'
      : (isLightTheme ? `rgba(0, 0, 0, ${borderAlpha})` : `rgba(255, 255, 255, ${borderAlpha})`);
    const borderBottomColor = borderAlpha <= 0.0001
      ? 'transparent'
      : (isLightTheme ? `rgba(0, 0, 0, ${borderAlpha})` : `rgba(255, 255, 255, ${borderAlpha})`);
    document.documentElement.style.setProperty('--taskbar-border-top', borderTopColor);
    document.documentElement.style.setProperty('--taskbar-border-bottom', borderBottomColor);

    const isTransparentWithBlur = bgOpacityPct < 100 && bgBlurPct > 0;
    const backdropUrl = state.backdropImageUrl || '';
    if (taskbarWallpaperLayer) {
      if (isTransparentWithBlur && backdropUrl) {
        taskbarWallpaperLayer.style.display = 'block';
        taskbarWallpaperLayer.style.backgroundImage = `url("${backdropUrl}")`;
        taskbarWallpaperLayer.style.setProperty('--taskbar-wallpaper-blur', `${(bgBlurPct * 0.24).toFixed(1)}px`);
      } else {
        taskbarWallpaperLayer.style.display = 'none';
        taskbarWallpaperLayer.style.backgroundImage = '';
      }
    }

    const activeWinWidget = state.widgets.find((w) => w.id === 'community.zen-active-windows');
    const appIconSizePx = activeWinWidget && typeof activeWinWidget.iconSizePx === 'number'
      ? Math.max(12, Math.min(32, activeWinWidget.iconSizePx))
      : 18;
    document.documentElement.style.setProperty('--winzen-app-icon-size', `${appIconSizePx}px`);

    const isTop = (settings.dockEdge || 'Bottom').toLowerCase() === 'top';
    currentDockEdge = isTop ? 'top' : 'bottom';
    currentAnimations = settings.enableAnimations !== false;
    globalDateFormat = settings.dateFormat || settings.DateFormat || globalDateFormat;

    const diConfig = settings.dynamicIsland || settings.DynamicIsland || {
      enabled: false,
      placement: 'taskbar',
      taskbarSlot: 'center',
      timeFormat: '24h',
      showSeconds: false,
      showDate: true,
      enableAnimation: true,
      enableMusicIsland: true
    };
    currentDynamicIslandConfig = diConfig;
    if (dynamicIslandEl) {
      if (currentDynamicIslandConfig.enableAnimation === false) {
        dynamicIslandEl.classList.add('no-anim');
      } else {
        dynamicIslandEl.classList.remove('no-anim');
      }
    }
    if (lastMediaSnapshot) {
      updateDynamicIslandMedia(lastMediaSnapshot);
    }

    document.body.classList.toggle('winzen-no-anim', !currentAnimations);
    if (taskbarBar) {
      taskbarBar.classList.toggle('floating', isFloating);
      taskbarBar.classList.toggle('dock-top', isTop);
      taskbarBar.style.setProperty('--taskbar-radius', `${isFloating ? currentRadius : 0}px`);
      if (bgOpacityPct === 0) {
        taskbarBar.style.borderTopColor = 'transparent';
        taskbarBar.style.borderBottomColor = 'transparent';
      } else {
        taskbarBar.style.borderTopColor = '';
        taskbarBar.style.borderBottomColor = '';
      }
    }

    // Update accent, dock edge, and animations flag on existing widget hosts
    document.querySelectorAll('.widget-host').forEach(host => {
      host.setAttribute('data-dock', currentDockEdge);
      host.setAttribute('data-animations', currentAnimations ? 'on' : 'off');
      host.style.setProperty('--winzen-accent', currentAccent);
      host.style.setProperty('--winzen-border-accent', currentAccent);
      host.style.setProperty('--win11-accent', currentAccent);
    });

    const enabledWidgets = state.widgets
      .filter((w) => w.enabled && (w.slot || '').toLowerCase() !== 'unused' && (w.slot || '').toLowerCase() !== 'disabled')
      .sort((a, b) => (a.order ?? 10) - (b.order ?? 10));
    const sig = JSON.stringify({
      theme: currentTheme,
      accent: currentAccent,
      animations: currentAnimations,
      dock: currentDockEdge,
      di: currentDynamicIslandConfig,
      widgets: enabledWidgets.map((w) => ({
        id: w.id,
        slot: w.slot,
        order: w.order ?? 10,
        width: w.width,
        autoWidth: w.autoWidth !== false,
        displayMode: w.displayMode || 'widget',
        aspectRatio: w.aspectRatio || '1:1',
        startIcon: w.startIcon || 'windows',
        startIconColorMode: w.startIconColorMode || 'text',
        startText: w.startText || 'Start',
        showStartText: !!w.showStartText,
        batteryIconStyle: w.batteryIconStyle || 'win11',
        batteryPercentMode: w.batteryPercentMode || 'outside-right',
        showSeconds: w.showSeconds !== false,
        showDate: w.showDate !== false,
        showCpu: w.showCpu !== false,
        showRam: w.showRam !== false,
        showGpu: w.showGpu !== false,
        html: w.htmlContent,
        css: w.cssContent,
        js: w.jsContent
      }))
    });

    if (sig !== lastWidgetsSignature) {
      lastWidgetsSignature = sig;
      mountWidgets(enabledWidgets);
    }
  });

  function mountWidgets(widgets) {
    slotLeft.innerHTML = '';
    slotCenter.innerHTML = '';
    slotRight.innerHTML = '';

    const isDiActiveInTaskbar = currentDynamicIslandConfig &&
      currentDynamicIslandConfig.enabled &&
      (currentDynamicIslandConfig.placement === 'taskbar' || currentDockEdge === 'top');
    const diSlot = ((currentDynamicIslandConfig && currentDynamicIslandConfig.taskbarSlot) || 'center').toLowerCase();

    widgets.forEach((w) => {
      const slot = (w.slot || 'right').toLowerCase();
      // Skip widgets placed in the control center or not in use from the taskbar zones
      if (slot === 'control-center' || slot === 'unused' || slot === 'disabled') return;

      // RULE 1: If Dynamic Island is active in the center, all widgets on the center become inactive (suppressed from taskbar)!
      if (isDiActiveInTaskbar && diSlot === 'center' && slot === 'center') {
        return;
      }

      const isAutoWidth = w.autoWidth !== false;
      const isShrinkableStrip = w.id === 'community.zen-active-windows';
      const isIconMode = (w.displayMode || 'widget') === 'icon';

      const host = document.createElement('div');
      host.className = `widget-host theme-${currentTheme}`;
      host.dataset.mode = w.displayMode || 'widget';
      host.dataset.inControlCenter = 'false';
      host.dataset.theme = currentTheme;
      host.setAttribute('data-theme', currentTheme);
      host.setAttribute('data-dock', currentDockEdge);
      host.setAttribute('data-animations', currentAnimations ? 'on' : 'off');
      host.style.width = isAutoWidth ? 'auto' : (isIconMode ? '34px' : `${w.width || 160}px`);
      host.dataset.widgetId = w.id;
      host.style.setProperty('--winzen-accent', currentAccent);
      host.style.setProperty('--winzen-border-accent', currentAccent);
      host.style.setProperty('--win11-accent', currentAccent);

      const shadow = host.attachShadow({ mode: 'open' });

      const isLightHost = currentTheme === 'light';
      const defText = isLightHost ? '#1b1b1b' : '#ffffff';
      const defSecondary = isLightHost ? '#5c5c5c' : '#c5c5c5';
      const defSurface = isLightHost ? '#ffffff' : '#252525';
      const defHover = isLightHost ? 'rgba(0, 0, 0, 0.05)' : 'rgba(255, 255, 255, 0.0605)';
      const defSubtle = isLightHost ? '#f9f9f9' : '#202020';
      const defBorder = isLightHost ? 'rgba(0, 0, 0, 0.082)' : 'rgba(255, 255, 255, 0.08)';

      const styleEl = document.createElement('style');
      styleEl.textContent = `
        :host {
          --winzen-bg: ${isLightHost ? '#f3f3f3' : '#1c1c1c'};
          --winzen-surface: ${defSurface};
          --winzen-surface-hover: ${defHover};
          --winzen-surface-active: rgba(255, 255, 255, 0.04);
          --winzen-surface-subtle: ${defSubtle};
          --winzen-border: ${defBorder};
          --winzen-border-subtle: rgba(255, 255, 255, 0.05);
          --winzen-border-accent: ${currentAccent};
          --winzen-text: ${defText};
          --winzen-text-secondary: ${defSecondary};
          --winzen-text-muted: #888888;
          --winzen-accent: ${currentAccent};
          --winzen-accent-hover: ${currentAccent};
          --winzen-accent-pressed: ${currentAccent};
          --winzen-purple: #c084fc;
          --winzen-danger: #f87171;
          --winzen-radius-xs: 4px;
          --winzen-radius-sm: 6px;
          --winzen-radius: 10px;
          --winzen-radius-lg: 14px;
          --winzen-radius-pill: 9999px;
          --winzen-font: 'Segoe UI Variable Text', 'Segoe UI', system-ui, -apple-system, sans-serif;

          display: inline-flex;
          align-items: center;
          width: ${isAutoWidth ? (isShrinkableStrip ? 'auto' : 'max-content') : '100%'};
          max-width: ${isShrinkableStrip ? '100%' : 'none'};
          min-width: ${isShrinkableStrip ? '0' : 'auto'};
          height: 36px;
          max-height: 36px;
          overflow: visible;
          font-family: var(--winzen-font);
          color: var(--winzen-text);
        }

        :host([data-theme="light"]), :host(.theme-light) {
          --winzen-bg: #f3f3f3;
          --winzen-surface: #ffffff;
          --winzen-surface-hover: rgba(0, 0, 0, 0.045);
          --winzen-surface-active: rgba(0, 0, 0, 0.075);
          --winzen-surface-subtle: #f9f9f9;
          --winzen-border: rgba(0, 0, 0, 0.082);
          --winzen-border-subtle: rgba(0, 0, 0, 0.05);
          --winzen-border-accent: ${currentAccent};
          --winzen-text: #1b1b1b;
          --winzen-text-secondary: #5c5c5c;
          --winzen-text-muted: #8a8a8a;
          --winzen-accent: ${currentAccent};
          --winzen-accent-hover: ${currentAccent};
          --winzen-accent-pressed: ${currentAccent};
          --winzen-purple: #7c3aed;
          --winzen-danger: #dc2626;
          color: var(--winzen-text);
        }
        ${w.cssContent || ''}

        :host([data-animations="off"]) *,
        :host([data-animations="off"]) *::before,
        :host([data-animations="off"]) *::after {
          animation: none !important;
          transition: none !important;
        }
        :host([data-animations="off"]) *:active {
          transform: none !important;
        }
      `;
      shadow.appendChild(styleEl);

      const wrapper = document.createElement('div');
      wrapper.className = `widget-taskbar-wrapper theme-${currentTheme}`;
      wrapper.dataset.inControlCenter = 'false';
      wrapper.dataset.theme = currentTheme;
      wrapper.setAttribute('data-theme', currentTheme);
      wrapper.style.cssText = `width:${isAutoWidth ? (isShrinkableStrip ? 'auto' : 'max-content') : '100%'};${isShrinkableStrip ? 'max-width:100%;min-width:0;' : ''}height:36px;max-height:36px;display:inline-flex;align-items:center;`;
      wrapper.innerHTML = w.htmlContent || '';
      shadow.appendChild(wrapper);

      if (slot === 'left') {
        slotLeft.appendChild(host);
      } else if (slot === 'center') {
        slotCenter.appendChild(host);
      } else {
        slotRight.appendChild(host);
      }

      if (w.jsContent) {
        try {
          shadow.createElement = (...args) => window.document.createElement(...args);
          const taskbarConfig = {
            ...w,
            inControlCenter: false,
            aspectRatio: 'taskbar',
            displayMode: w.displayMode || 'widget',
            dockEdge: currentDockEdge,
            animations: currentAnimations
          };
          const fn = new Function('root', 'document', 'winzen', 'widgetConfig', w.jsContent);
          fn(shadow, shadow, window.winzen, taskbarConfig);
        } catch (err) {
          console.error(`Widget script error (${w.id}):`, err);
        }
      }
    });

    if (isDiActiveInTaskbar) {
      if (!dynamicIslandEl) {
        dynamicIslandEl = createDynamicIslandElement();
      }
      dynamicIslandEl.classList.remove('island-hidden');
      const innerClock = dynamicIslandEl.querySelector('#shell-dynamic-island');
      if (innerClock) innerClock.classList.remove('island-hidden');

      if (diSlot === 'center') {
        // Exclusively occupies center
        slotCenter.innerHTML = '';
        slotCenter.appendChild(dynamicIslandEl);
      } else if (diSlot === 'left') {
        // RULE 2: If on the left, Dynamic Island should be the FIRST item!
        slotLeft.prepend(dynamicIslandEl);
      } else if (diSlot === 'right') {
        // RULE 3: If on the right, Dynamic Island should be the LAST item!
        slotRight.appendChild(dynamicIslandEl);
      }

      updateDynamicIslandClock();
      if (lastMediaSnapshot) {
        updateDynamicIslandMedia(lastMediaSnapshot);
      }
      if (!dynamicIslandClockTimer) {
        dynamicIslandClockTimer = setInterval(updateDynamicIslandClock, 1000);
      }
    } else {
      if (dynamicIslandClockTimer) {
        clearInterval(dynamicIslandClockTimer);
        dynamicIslandClockTimer = null;
      }
      dynamicIslandEl = null;
    }

    if (window.winzen.metrics) {
      window.dispatchEvent(new CustomEvent('winzen:metrics', { detail: window.winzen.metrics }));
    }
  }

  window.addEventListener('winzen:metrics', (e) => {
    if (e.detail && e.detail.media) {
      lastMediaSnapshot = e.detail.media;
      updateDynamicIslandMedia(e.detail.media);
    }
  });

  window.winzen.send('request_state');
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initTaskbar);
} else {
  initTaskbar();
}
