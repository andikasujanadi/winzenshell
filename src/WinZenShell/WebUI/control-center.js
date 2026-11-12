function initControlCenter() {
  const ccContainer = document.getElementById('ccContainer');
  const ccGrid = document.getElementById('ccGrid');
  const btnEditControls = document.getElementById('btnEditControls');
  const btnDoneControls = document.getElementById('btnDoneControls');
  const ccAddDrawer = document.getElementById('ccAddDrawer');
  const ccDrawerList = document.getElementById('ccDrawerList');
  const btnCcCloseDrawer = document.getElementById('btnCcCloseDrawer');

  let isEditing = false;
  let currentCcWidgets = [];
  let availableWidgets = [];
  let draggedCardId = null;
  let lastSentHeight = 0;

  let currentTheme = 'dark';
  let currentAccent = '#3684E3';
  let currentAnimations = true;
  let lastCcRenderSig = '';

  const SVG_MINUS = '<svg viewBox="0 0 16 16" width="8" height="8" fill="currentColor"><rect x="3" y="7" width="10" height="2" rx="1"/></svg>';
  const SVG_RESIZE = '<svg viewBox="0 0 16 16" width="8" height="8" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M10 2h4v4M14 2L9 7M6 14H2v-4M2 14l5-5"/></svg>';

  function scheduleResize() {
    const measureAndSend = () => {
      if (!ccGrid || !window.winzen || typeof window.winzen.send !== 'function') return;
      const bottomBar = document.getElementById('ccBottomBar');
      const bottomBarH = bottomBar ? bottomBar.offsetHeight : 44;

      let maxBottom = 0;
      const children = ccGrid.children;
      if (children.length === 0) {
        maxBottom = 96;
      } else {
        for (let i = 0; i < children.length; i++) {
          const ch = children[i];
          // Ignore placeholder slots when computing tight content height in normal mode
          if (!isEditing && ch.classList.contains('cc-slot-placeholder')) continue;
          const bottom = ch.offsetTop + ch.offsetHeight;
          if (bottom > maxBottom) maxBottom = bottom;
        }
      }

      const gridContentH = maxBottom + 12; // bottom padding
      const total = Math.ceil(gridContentH + bottomBarH + 4);

      if (total > 80 && Math.abs(total - lastSentHeight) >= 2) {
        lastSentHeight = total;
        window.winzen.send('resize_control_center', { height: total });
      }
    };

    requestAnimationFrame(() => {
      measureAndSend();
      setTimeout(measureAndSend, 60);
      setTimeout(measureAndSend, 160);
    });
  }

  if (btnEditControls) {
    btnEditControls.addEventListener('click', () => toggleEditMode(true));
  }
  if (btnDoneControls) {
    btnDoneControls.addEventListener('click', () => toggleEditMode(false));
  }

  if (btnCcCloseDrawer && ccAddDrawer) {
    btnCcCloseDrawer.addEventListener('click', () => {
      ccAddDrawer.style.display = 'none';
    });
  }

  function toggleEditMode(enable) {
    isEditing = enable;
    if (isEditing) {
      ccContainer.classList.add('is-editing');
      if (btnEditControls) btnEditControls.style.display = 'none';
      if (btnDoneControls) btnDoneControls.style.display = 'inline-flex';
    } else {
      ccContainer.classList.remove('is-editing');
      if (btnEditControls) btnEditControls.style.display = 'inline-flex';
      if (btnDoneControls) btnDoneControls.style.display = 'none';
      if (ccAddDrawer) ccAddDrawer.style.display = 'none';
    }
    renderControlCenter(currentCcWidgets);
    scheduleResize();
  }

  function openAddDrawer() {
    if (!ccAddDrawer || !ccDrawerList) return;
    ccDrawerList.innerHTML = '';

    const currentIds = new Set(currentCcWidgets.map((w) => w.id));
    const available = availableWidgets.filter((w) => !currentIds.has(w.id) && w.id !== 'zen-control-center');

    if (available.length === 0) {
      ccDrawerList.innerHTML = '<div style="padding:16px;text-align:center;color:#888;font-size:12px;">All available widgets are already in Control Center.</div>';
    } else {
      available.forEach((w) => {
        const item = document.createElement('div');
        item.className = 'cc-drawer-item';
        item.innerHTML = `
          <div>
            <div class="cc-drawer-item-title">${escapeHtml(w.name || w.id)}</div>
            <div class="cc-drawer-item-sub">${escapeHtml(w.description || 'Widget')}</div>
          </div>
          <span class="cc-drawer-add-pill">+ Add</span>
        `;
        item.addEventListener('click', () => {
          addWidgetToCc(w.id);
          ccAddDrawer.style.display = 'none';
        });
        ccDrawerList.appendChild(item);
      });
    }

    ccAddDrawer.style.display = 'flex';
  }

  function addWidgetToCc(widgetId) {
    const isSlider = widgetId.includes('volume') || widgetId.includes('brightness') || widgetId.includes('battery');
    const defaultSpan = isSlider ? '4x1' : '2x2';
    const newOrder = (currentCcWidgets.length + 1) * 10;

    const updated = currentCcWidgets.map((w, idx) => ({
      id: w.id,
      order: (idx + 1) * 10,
      aspectRatio: normalizeSpan(w.id, w.aspectRatio),
      enabled: w.enabled !== false
    }));

    updated.push({
      id: widgetId,
      order: newOrder,
      aspectRatio: defaultSpan,
      enabled: true
    });

    window.winzen.send('update_control_center_widgets', { items: updated });
  }

  function normalizeSpan(widgetId, span) {
    if (!span || span === '1:2') {
      const isSlider = widgetId.includes('volume') || widgetId.includes('brightness') || widgetId.includes('battery');
      return isSlider ? '4x1' : '2x1';
    }
    if (span === '1:1') return '2x2';
    return span;
  }

  function getNextSpan(widgetId, currentSpan) {
    const norm = normalizeSpan(widgetId, currentSpan);
    const isSlider = widgetId.includes('volume') || widgetId.includes('brightness');
    if (isSlider) {
      const sliderCycle = ['4x1', '2x1', '1x1'];
      const idx = sliderCycle.indexOf(norm);
      return sliderCycle[(idx + 1) % sliderCycle.length];
    }
    const generalCycle = ['2x2', '2x1', '1x1', '4x1', '3x1'];
    const idx = generalCycle.indexOf(norm);
    if (idx === -1) return '2x2';
    return generalCycle[(idx + 1) % generalCycle.length];
  }

  function buildCcSignature(widgets, settings) {
    return JSON.stringify({
      theme: (settings && settings.theme) || 'dark',
      accent: (settings && settings.accentColor) || '#3684E3',
      isEditing: isEditing,
      widgets: (widgets || []).map((w) => ({
        id: w.id,
        order: w.order ?? 10,
        aspectRatio: normalizeSpan(w.id, w.aspectRatio),
        enabled: w.enabled !== false,
        batteryIconStyle: w.batteryIconStyle || 'win11',
        batteryPercentMode: w.batteryPercentMode || 'outside-right',
        startIcon: w.startIcon || 'chromeos',
        startIconColorMode: w.startIconColorMode || 'text',
        showSeconds: w.showSeconds !== false,
        showDate: w.showDate !== false,
        showCpu: w.showCpu !== false,
        showRam: w.showRam !== false,
        showGpu: w.showGpu !== false,
        html: w.htmlContent || '',
        css: w.cssContent || '',
        js: w.jsContent || ''
      }))
    });
  }

  function applyStateUpdate(state) {
    if (!state) return;
    currentTheme = (state.settings && state.settings.theme) || 'dark';
    currentAccent = (state.settings && state.settings.accentColor) || '#3684E3';
    currentAnimations = !(state.settings && state.settings.enableAnimations === false);
    document.documentElement.setAttribute('data-theme', currentTheme);
    document.body.className = `${currentTheme === 'light' ? 'theme-light' : 'theme-dark'}${currentAnimations ? '' : ' winzen-no-anim'}`;
    document.querySelectorAll('.cc-card-content').forEach((el) => {
      el.setAttribute('data-animations', currentAnimations ? 'on' : 'off');
    });
    document.documentElement.style.setProperty('--winzen-accent', currentAccent);
    document.documentElement.style.setProperty('--winzen-border-accent', currentAccent);
    document.documentElement.style.setProperty('--win11-accent', currentAccent);

    availableWidgets = (state.widgets || []).slice();
    const ccList = state.controlCenterWidgets || [];
    currentCcWidgets = ccList.slice().sort((a, b) => (a.order ?? 10) - (b.order ?? 10));

    const sig = buildCcSignature(currentCcWidgets, state.settings);
    if (sig !== lastCcRenderSig) {
      lastCcRenderSig = sig;
      renderControlCenter(currentCcWidgets);
    } else if (window.winzen && window.winzen.metrics) {
      window.dispatchEvent(new CustomEvent('winzen:metrics', { detail: window.winzen.metrics }));
    }
  }

  window.addEventListener('winzen:state', (e) => {
    applyStateUpdate(e.detail);
  });

  function renderControlCenter(widgets) {
    ccGrid.innerHTML = '';

    if (widgets.length === 0 && !isEditing) {
      ccGrid.innerHTML = `
        <div class="cc-card" data-span="4x1" style="text-align:center;color:#9ca3af;font-size:12px;padding:24px 12px;grid-column:span 4;display:flex;align-items:center;justify-content:center;">
          No widgets configured. Click Edit Controls below.
        </div>
      `;
      scheduleResize();
      return;
    }

    // Render placed widgets
    let totalColsOccupied = 0;
    widgets.forEach((w) => {
      const span = normalizeSpan(w.id, w.aspectRatio);

      // Track columns occupied roughly to balance grid
      const parts = span.split('x');
      const cols = parseInt(parts[0], 10) || 2;
      const rows = parseInt(parts[1], 10) || 1;
      totalColsOccupied += cols * rows;

      const card = document.createElement('div');
      card.className = 'cc-card';
      card.dataset.span = span;
      card.dataset.aspectRatio = span;
      card.dataset.widgetId = w.id;
      card.draggable = isEditing;

      // Badges for edit mode (Picture 4)
      const btnRemove = document.createElement('button');
      btnRemove.className = 'cc-card-badge-remove';
      btnRemove.title = 'Remove widget';
      btnRemove.setAttribute('aria-label', 'Remove widget');
      btnRemove.innerHTML = SVG_MINUS;
      btnRemove.addEventListener('click', (ev) => {
        ev.stopPropagation();
        const filtered = currentCcWidgets.filter((item) => item.id !== w.id);
        const payload = filtered.map((item, idx) => ({
          id: item.id,
          order: (idx + 1) * 10,
          aspectRatio: normalizeSpan(item.id, item.aspectRatio),
          enabled: item.enabled !== false
        }));
        window.winzen.send('update_control_center_widgets', { items: payload });
      });
      card.appendChild(btnRemove);

      const btnResize = document.createElement('button');
      btnResize.className = 'cc-card-badge-resize';
      btnResize.title = 'Resize widget';
      btnResize.setAttribute('aria-label', 'Resize widget');
      btnResize.innerHTML = SVG_RESIZE;
      btnResize.addEventListener('click', (ev) => {
        ev.stopPropagation();
        const nextSpan = getNextSpan(w.id, span);
        const payload = currentCcWidgets.map((item) => ({
          id: item.id,
          order: item.order,
          aspectRatio: item.id === w.id ? nextSpan : normalizeSpan(item.id, item.aspectRatio),
          enabled: item.enabled !== false
        }));
        window.winzen.send('update_control_center_widgets', { items: payload });
      });
      card.appendChild(btnResize);

      // Drag and drop reordering
      card.addEventListener('dragstart', (ev) => {
        if (!isEditing) {
          ev.preventDefault();
          return;
        }
        draggedCardId = w.id;
        card.classList.add('dragging');
        if (ev.dataTransfer) {
          ev.dataTransfer.effectAllowed = 'move';
          ev.dataTransfer.setData('text/plain', w.id);
        }
      });

      card.addEventListener('dragend', () => {
        card.classList.remove('dragging');
        document.querySelectorAll('.cc-card').forEach((c) => c.classList.remove('drag-over'));
      });

      card.addEventListener('dragover', (ev) => {
        if (!isEditing || !draggedCardId || draggedCardId === w.id) return;
        ev.preventDefault();
        card.classList.add('drag-over');
      });

      card.addEventListener('dragleave', () => {
        card.classList.remove('drag-over');
      });

      card.addEventListener('drop', (ev) => {
        if (!isEditing || !draggedCardId || draggedCardId === w.id) return;
        ev.preventDefault();
        card.classList.remove('drag-over');

        const fromIdx = currentCcWidgets.findIndex((item) => item.id === draggedCardId);
        const toIdx = currentCcWidgets.findIndex((item) => item.id === w.id);
        if (fromIdx !== -1 && toIdx !== -1) {
          const reordered = currentCcWidgets.slice();
          const [moved] = reordered.splice(fromIdx, 1);
          reordered.splice(toIdx, 0, moved);

          const payload = reordered.map((item, idx) => ({
            id: item.id,
            order: (idx + 1) * 10,
            aspectRatio: normalizeSpan(item.id, item.aspectRatio),
            enabled: item.enabled !== false
          }));

          window.winzen.send('update_control_center_widgets', { items: payload });
        }
        draggedCardId = null;
      });

      // Card Content with Shadow DOM
      const content = document.createElement('div');
      content.className = 'cc-card-content';
      content.dataset.inControlCenter = 'true';
      content.dataset.aspectRatio = span;
      content.dataset.span = span;
      content.setAttribute('data-in-control-center', 'true');
      content.setAttribute('data-aspect-ratio', span);
      content.setAttribute('data-span', span);

      content.dataset.theme = currentTheme;
      content.setAttribute('data-theme', currentTheme);
      content.setAttribute('data-animations', currentAnimations ? 'on' : 'off');
      content.style.setProperty('--winzen-accent', currentAccent);
      content.style.setProperty('--winzen-border-accent', currentAccent);
      content.style.setProperty('--win11-accent', currentAccent);

      const isLightHost = currentTheme === 'light';
      const defText = isLightHost ? '#1b1b1b' : '#ffffff';
      const defSecondary = isLightHost ? '#5c5c5c' : '#c5c5c5';
      const defSurface = isLightHost ? '#ffffff' : '#252525';
      const defHover = isLightHost ? 'rgba(0, 0, 0, 0.045)' : 'rgba(255, 255, 255, 0.0605)';
      const defSubtle = isLightHost ? '#f9f9f9' : '#202020';
      const defBorder = isLightHost ? 'rgba(0, 0, 0, 0.082)' : 'rgba(255, 255, 255, 0.08)';

      const shadow = content.attachShadow({ mode: 'open' });
      const styleEl = document.createElement('style');
      styleEl.textContent = `
        :host {
          --winzen-bg: ${isLightHost ? '#f3f3f3' : '#1c1c1c'};
          --winzen-surface: ${defSurface};
          --winzen-surface-hover: ${defHover};
          --winzen-surface-active: rgba(255, 255, 255, 0.04);
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

          display: flex;
          flex-direction: column;
          align-items: stretch;
          justify-content: center;
          width: 100%;
          height: 100%;
          min-height: 0;
          font-family: var(--winzen-font);
          color: var(--winzen-text);
          box-sizing: border-box;
          padding: 8px 10px;
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
        }

        /* Full width slider styling */
        .slider-row {
          display: flex;
          align-items: center;
          gap: 10px;
          width: 100%;
          padding: 0 4px;
        }
        .slider-row .slider-val {
          font-size: 12px;
          font-weight: 600;
          min-width: 36px;
          text-align: right;
          font-variant-numeric: tabular-nums;
        }
        .slider-row input[type="range"] {
          flex: 1 1 auto;
          height: 4px;
          border-radius: 2px;
          outline: none;
          accent-color: var(--winzen-accent, #3684e3);
          cursor: pointer;
        }

        /* Clock styling */
        .clock-pill {
          display: flex;
          flex-direction: column;
          align-items: center;
          justify-content: center;
          width: 100%;
          height: 100%;
          text-align: center;
          background: transparent;
          border: none;
        }
        .clock-pill .time {
          font-size: 22px;
          font-weight: 600;
          letter-spacing: -0.3px;
        }
        .clock-pill .date {
          font-size: 11px;
          color: var(--winzen-text-secondary);
          margin-top: 2px;
        }

        /* Telemetry styling */
        .telemetry-card {
          display: flex;
          flex-direction: column;
          gap: 6px;
          width: 100%;
          justify-content: center;
        }

        /* Battery styling */
        .battery-widget {
          display: flex;
          align-items: center;
          justify-content: space-between;
          width: 100%;
          gap: 12px;
        }

        ${w.cssContent || ''}
      `;
      shadow.appendChild(styleEl);

      const htmlWrapper = document.createElement('div');
      htmlWrapper.style.width = '100%';
      htmlWrapper.style.height = '100%';
      htmlWrapper.style.display = 'flex';
      htmlWrapper.style.alignItems = 'center';
      htmlWrapper.style.justifyContent = 'center';
      htmlWrapper.innerHTML = w.htmlContent || '';
      shadow.appendChild(htmlWrapper);

      card.appendChild(content);
      ccGrid.appendChild(card);

      // Execute widget script
      if (w.jsContent) {
        try {
          const configCopy = {
            id: w.id,
            name: w.name,
            aspectRatio: span,
            colSpan: cols,
            rowSpan: rows,
            inControlCenter: true,
            theme: currentTheme,
            accentColor: currentAccent,
            batteryIconStyle: w.batteryIconStyle,
            batteryPercentMode: w.batteryPercentMode,
            startIcon: w.startIcon,
            startIconColorMode: w.startIconColorMode,
            showSeconds: w.showSeconds,
            showDate: w.showDate,
            showCpu: w.showCpu,
            showRam: w.showRam,
            showGpu: w.showGpu
          };
          const fn = new Function('root', 'document', 'winzen', 'widgetConfig', w.jsContent);
          fn(shadow, shadow, window.winzen, configCopy);
        } catch (err) {
          console.error(`Control Center widget script error (${w.id}):`, err);
        }
      }
    });

    // In Edit Mode, render empty slot placeholders (as in Picture 2)
    if (isEditing) {
      // Complete current row and add at least 4 more empty slots
      const remainder = totalColsOccupied % 4;
      const neededToFillRow = remainder === 0 ? 0 : (4 - remainder);
      const totalPlaceholders = Math.max(4, neededToFillRow + 4);

      for (let i = 0; i < totalPlaceholders; i++) {
        const slot = document.createElement('div');
        slot.className = 'cc-slot-placeholder';
        slot.title = 'Add Widget';
        slot.innerHTML = '<span class="cc-slot-plus">+</span>';
        slot.addEventListener('click', () => openAddDrawer());
        ccGrid.appendChild(slot);
      }
    }

    if (window.winzen && window.winzen.metrics) {
      window.dispatchEvent(new CustomEvent('winzen:metrics', { detail: window.winzen.metrics }));
    }
    scheduleResize();
  }

  window.addEventListener('winzen:metrics', (e) => {
    const m = e.detail;
    if (!m) return;
  });

  function escapeHtml(s) {
    return String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  }

  if (window.winzen && window.winzen.state) {
    applyStateUpdate(window.winzen.state);
  }
  if (window.winzen && typeof window.winzen.send === 'function') {
    window.winzen.send('request_state');
  }
}

document.addEventListener('DOMContentLoaded', initControlCenter);
