document.addEventListener('DOMContentLoaded', () => {
  const slotLeft = document.getElementById('slot-left');
  const slotCenter = document.getElementById('slot-center');
  const slotRight = document.getElementById('slot-right');
  const btnOpenStudio = document.getElementById('btnOpenStudio');

  let lastWidgetsSignature = '';

  btnOpenStudio.addEventListener('click', () => {
    window.winzen.shell.openSettings();
  });

  window.addEventListener('winzen:state', (e) => {
    const state = e.detail;
    if (!state || !Array.isArray(state.widgets)) return;

    const enabledWidgets = state.widgets
      .filter((w) => w.enabled)
      .sort((a, b) => (a.order ?? 10) - (b.order ?? 10));
    const sig = JSON.stringify(
      enabledWidgets.map((w) => ({
        id: w.id,
        slot: w.slot,
        order: w.order ?? 10,
        width: w.width,
        autoWidth: w.autoWidth !== false,
        showAppName: w.showAppName !== false,
        combineApps: !!w.combineApps,
        startIcon: w.startIcon || 'windows',
        startText: w.startText || 'Start',
        showStartText: !!w.showStartText,
        html: w.htmlContent,
        css: w.cssContent,
        js: w.jsContent
      }))
    );

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
      const isAutoWidth = w.autoWidth !== false;

      const host = document.createElement('div');
      host.className = 'widget-host';
      host.style.width = isAutoWidth ? 'auto' : `${w.width || 160}px`;
      host.dataset.widgetId = w.id;

      const shadow = host.attachShadow({ mode: 'open' });

      const styleEl = document.createElement('style');
      styleEl.textContent = `
        :host {
          display: inline-flex;
          align-items: center;
          width: ${isAutoWidth ? 'max-content' : '100%'};
          height: 36px;
          max-height: 36px;
          overflow: visible;
          font-family: 'Segoe UI Variable Text', 'Segoe UI', system-ui, sans-serif;
          color: #ffffff;
        }
        ${w.cssContent || ''}
      `;
      shadow.appendChild(styleEl);

      const wrapper = document.createElement('div');
      wrapper.style.cssText = `width:${isAutoWidth ? 'max-content' : '100%'};height:36px;max-height:36px;display:inline-flex;align-items:center;`;
      wrapper.innerHTML = w.htmlContent || '';
      shadow.appendChild(wrapper);

      const slot = (w.slot || 'right').toLowerCase();
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
          const fn = new Function('root', 'document', 'winzen', 'widgetConfig', w.jsContent);
          fn(shadow, shadow, window.winzen, w);
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
});
