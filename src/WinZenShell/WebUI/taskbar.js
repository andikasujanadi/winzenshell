function initTaskbar() {
  const slotLeft = document.getElementById('slot-left');
  const slotCenter = document.getElementById('slot-center');
  const slotRight = document.getElementById('slot-right');
  const btnOpenStudio = document.getElementById('btnOpenStudio');
  const taskbarBar = document.getElementById('taskbarBar');

  let lastWidgetsSignature = '';
  let currentTheme = 'dark';
  let currentAccent = '#3684E3';
  let currentRadius = 12;

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
    const isTop = (settings.dockEdge || 'Bottom').toLowerCase() === 'top';
    if (taskbarBar) {
      taskbarBar.classList.toggle('floating', isFloating);
      taskbarBar.classList.toggle('dock-top', isTop);
      taskbarBar.style.setProperty('--taskbar-radius', `${isFloating ? currentRadius : 0}px`);
    }

    // Update accent and radius on existing widget hosts
    document.querySelectorAll('.widget-host').forEach(host => {
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
        startText: w.startText || 'Start',
        showStartText: !!w.showStartText,
        batteryIconStyle: w.batteryIconStyle || 'win11',
        batteryPercentMode: w.batteryPercentMode || 'outside-right',
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
      const isIconMode = (w.displayMode || 'widget') === 'icon';

      const host = document.createElement('div');
      host.className = `widget-host theme-${currentTheme}`;
      host.dataset.mode = w.displayMode || 'widget';
      host.dataset.inControlCenter = 'false';
      host.dataset.theme = currentTheme;
      host.setAttribute('data-theme', currentTheme);
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
          width: ${isAutoWidth ? 'max-content' : '100%'};
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
      `;
      shadow.appendChild(styleEl);

      const wrapper = document.createElement('div');
      wrapper.className = `widget-taskbar-wrapper theme-${currentTheme}`;
      wrapper.dataset.inControlCenter = 'false';
      wrapper.dataset.theme = currentTheme;
      wrapper.setAttribute('data-theme', currentTheme);
      wrapper.style.cssText = `width:${isAutoWidth ? 'max-content' : '100%'};height:36px;max-height:36px;display:inline-flex;align-items:center;`;
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
            displayMode: w.displayMode || 'widget'
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
