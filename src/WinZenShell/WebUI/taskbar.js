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

    widgets.forEach((w) => {
      const slot = (w.slot || 'right').toLowerCase();
      // Skip widgets placed in the control center or not in use from the taskbar zones
      if (slot === 'control-center' || slot === 'unused' || slot === 'disabled') return;

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

    if (window.winzen.metrics) {
      window.dispatchEvent(new CustomEvent('winzen:metrics', { detail: window.winzen.metrics }));
    }
  }

  window.winzen.send('request_state');
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initTaskbar);
} else {
  initTaskbar();
}
