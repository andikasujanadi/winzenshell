function initTaskbar() {
  const slotLeft = document.getElementById('slot-left');
  const slotCenter = document.getElementById('slot-center');
  const slotRight = document.getElementById('slot-right');
  const btnOpenStudio = document.getElementById('btnOpenStudio');

  let lastWidgetsSignature = '';
  let currentTheme = 'dark';

  if (btnOpenStudio) {
    btnOpenStudio.addEventListener('click', () => {
      window.winzen.shell.openSettings();
    });
  }

  window.addEventListener('winzen:state', (e) => {
    const state = e.detail;
    if (!state || !Array.isArray(state.widgets)) return;

    currentTheme = (state.settings && state.settings.theme) || 'dark';
    document.documentElement.setAttribute('data-theme', currentTheme);
    document.body.className = currentTheme === 'light' ? 'theme-light' : 'theme-dark';

    const enabledWidgets = state.widgets
      .filter((w) => w.enabled && (w.slot || '').toLowerCase() !== 'unused' && (w.slot || '').toLowerCase() !== 'disabled')
      .sort((a, b) => (a.order ?? 10) - (b.order ?? 10));
    const sig = JSON.stringify({
      theme: currentTheme,
      widgets: enabledWidgets.map((w) => ({
        id: w.id,
        slot: w.slot,
        order: w.order ?? 10,
        width: w.width,
        autoWidth: w.autoWidth !== false,
        displayMode: w.displayMode || 'widget',
        aspectRatio: w.aspectRatio || '1:1',
        showAppName: w.showAppName !== false,
        combineApps: !!w.combineApps,
        startIcon: w.startIcon || 'windows',
        startText: w.startText || 'Start',
        showStartText: !!w.showStartText,
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

      const shadow = host.attachShadow({ mode: 'open' });

      const styleEl = document.createElement('style');
      styleEl.textContent = `
        :host {
          --winzen-bg: #1c1c1c;
          --winzen-surface: #252525;
          --winzen-surface-hover: rgba(255, 255, 255, 0.0605);
          --winzen-surface-active: rgba(255, 255, 255, 0.04);
          --winzen-surface-subtle: #202020;
          --winzen-border: rgba(255, 255, 255, 0.08);
          --winzen-border-subtle: rgba(255, 255, 255, 0.05);
          --winzen-border-accent: #60cdff;
          --winzen-text: #ffffff;
          --winzen-text-secondary: #c5c5c5;
          --winzen-text-muted: #888888;
          --winzen-accent: #60cdff;
          --winzen-accent-hover: #78d4ff;
          --winzen-accent-pressed: #0098e6;
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
          --winzen-border-accent: #0067c0;
          --winzen-text: #1b1b1b;
          --winzen-text-secondary: #5c5c5c;
          --winzen-text-muted: #8a8a8a;
          --winzen-accent: #0067c0;
          --winzen-accent-hover: #1879cd;
          --winzen-accent-pressed: #005aa3;
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
