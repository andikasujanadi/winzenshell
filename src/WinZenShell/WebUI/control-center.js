function initControlCenter() {
  const ccContainer = document.getElementById('ccContainer');
  const ccGrid = document.getElementById('ccGrid');
  const btnEditControls = document.getElementById('btnEditControls');
  const btnAddWidget = document.getElementById('btnAddWidget');
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
      const bottomBarH = bottomBar ? bottomBar.offsetHeight : 52;

      let maxBottom = 0;
      const children = ccGrid.children;
      if (children.length === 0) {
        maxBottom = 96;
      } else {
        for (let i = 0; i < children.length; i++) {
          const ch = children[i];
          if (!isEditing && ch.classList.contains('cc-slot-placeholder')) continue;
          const bottom = ch.offsetTop + ch.offsetHeight;
          if (bottom > maxBottom) maxBottom = bottom;
        }
      }

      const gridContentH = maxBottom + 6; // padding below last card
      const total = Math.ceil(gridContentH + bottomBarH + 16); // extra bottom clearance for rounded corners

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
  if (btnAddWidget) {
    btnAddWidget.addEventListener('click', () => openAddDrawer());
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
      if (btnAddWidget) btnAddWidget.style.display = 'inline-flex';
      if (btnDoneControls) btnDoneControls.style.display = 'inline-flex';
    } else {
      ccContainer.classList.remove('is-editing');
      if (btnEditControls) btnEditControls.style.display = 'inline-flex';
      if (btnAddWidget) btnAddWidget.style.display = 'none';
      if (btnDoneControls) btnDoneControls.style.display = 'none';
      if (ccAddDrawer) ccAddDrawer.style.display = 'none';
    }
    renderControlCenter(currentCcWidgets);
    scheduleResize();
  }

  window.exitEditMode = () => {
    if (isEditing) toggleEditMode(false);
  };
  window.addEventListener('blur', () => {
    if (isEditing) toggleEditMode(false);
  });

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
      const sliderCycle = ['4x1', '2x1', '1x2', '1x1'];
      const idx = sliderCycle.indexOf(norm);
      return sliderCycle[(idx + 1) % sliderCycle.length];
    }
    const generalCycle = ['2x2', '2x1', '1x2', '1x1', '4x1', '3x1'];
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

    // 2D grid matrix simulation for 4 columns
    const gridMap = {};
    const getCell = (r, c) => !!(gridMap[r] && gridMap[r][c]);
    const setCell = (r, c, val) => {
      if (!gridMap[r]) gridMap[r] = {};
      gridMap[r][c] = val;
    };

    const placedList = [];
    widgets.forEach((w) => {
      const span = normalizeSpan(w.id, w.aspectRatio);
      const parts = span.split('x');
      const colSpan = Math.max(1, Math.min(4, parseInt(parts[0], 10) || 2));
      const rowSpan = Math.max(1, Math.min(2, parseInt(parts[1], 10) || 1));

      // Standard CSS grid auto-placement search (no dense packing, respects order)
      let r = 1;
      let placed = false;
      while (!placed) {
        for (let c = 1; c <= 5 - colSpan; c++) {
          let canFit = true;
          for (let dr = 0; dr < rowSpan; dr++) {
            for (let dc = 0; dc < colSpan; dc++) {
              if (getCell(r + dr, c + dc)) {
                canFit = false;
                break;
              }
            }
            if (!canFit) break;
          }
          if (canFit) {
            for (let dr = 0; dr < rowSpan; dr++) {
              for (let dc = 0; dc < colSpan; dc++) {
                setCell(r + dr, c + dc, true);
              }
            }
            placedList.push({
              widget: w,
              span,
              colSpan,
              rowSpan,
              row: r,
              col: c
            });
            placed = true;
            break;
          }
        }
        if (!placed) r++;
      }
    });

    let maxRow = 0;
    placedList.forEach((p) => {
      const endRow = p.row + p.rowSpan - 1;
      if (endRow > maxRow) maxRow = endRow;
    });

    // In edit mode, add at least 1 extra empty row of placeholders at the bottom (as in Picture 2 & 4)
    const extraRows = isEditing ? 1 : 0;
    const totalRows = Math.max(isEditing ? 4 : 1, maxRow + extraRows);

    // 1. Render empty placeholder slots in edit mode for unoccupied cells
    if (isEditing) {
      for (let r = 1; r <= totalRows; r++) {
        for (let c = 1; c <= 4; c++) {
          if (!getCell(r, c)) {
            const slot = document.createElement('div');
            slot.className = 'cc-slot-placeholder';
            slot.style.gridRow = `${r}`;
            slot.style.gridColumn = `${c}`;
            if (r > maxRow) slot.dataset.extraRow = 'true';
            ccGrid.appendChild(slot);
          }
        }
      }
    }

    // 2. Render cards for placed widgets
    placedList.forEach((pw) => {
      const w = pw.widget;
      const span = pw.span;
      const cols = pw.colSpan;
      const rows = pw.rowSpan;

      const card = document.createElement('div');
      card.className = 'cc-card';
      card.dataset.span = span;
      card.dataset.aspectRatio = span;
      card.dataset.widgetId = w.id;
      card.style.gridColumn = `${pw.col} / span ${cols}`;
      card.style.gridRow = `${pw.row} / span ${rows}`;
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

      // Resize handle badge with interactive drag-to-resize AND click-to-cycle
      const btnResize = document.createElement('div');
      btnResize.className = 'cc-card-badge-resize';
      btnResize.title = 'Drag or click to resize';
      btnResize.setAttribute('aria-label', 'Resize widget');
      btnResize.innerHTML = SVG_RESIZE;

      let isDraggingResize = false;
      let startX = 0, startY = 0;
      let origCols = cols, origRows = rows;
      let curCols = cols, curRows = rows;
      let hasMoved = false;

      btnResize.addEventListener('pointerdown', (ev) => {
        ev.stopPropagation();
        ev.preventDefault();
        try { btnResize.setPointerCapture(ev.pointerId); } catch {}
        isDraggingResize = true;
        hasMoved = false;
        startX = ev.clientX;
        startY = ev.clientY;
        origCols = curCols = cols;
        origRows = curRows = rows;
        card.classList.add('is-resizing');
      });

      btnResize.addEventListener('pointermove', (ev) => {
        if (!isDraggingResize) return;
        const dx = ev.clientX - startX;
        const dy = ev.clientY - startY;
        if (Math.hypot(dx, dy) > 6) {
          hasMoved = true;
        }

        const colDelta = Math.round(dx / 60);
        const rowDelta = Math.round(dy / 55);

        const newCols = Math.max(1, Math.min(4, origCols + colDelta));
        const newRows = Math.max(1, Math.min(2, origRows + rowDelta));

        if (newCols !== curCols || newRows !== curRows) {
          curCols = newCols;
          curRows = newRows;
          const previewSpan = `${newCols}x${newRows}`;
          card.dataset.span = previewSpan;
          card.dataset.aspectRatio = previewSpan;
          card.style.gridColumn = `${pw.col} / span ${newCols}`;
          card.style.gridRow = `${pw.row} / span ${newRows}`;
          if (content) {
            content.dataset.span = previewSpan;
            content.dataset.aspectRatio = previewSpan;
          }
        }
      });

      const onPointerEnd = (ev) => {
        if (!isDraggingResize) return;
        try { btnResize.releasePointerCapture(ev.pointerId); } catch {}
        isDraggingResize = false;
        card.classList.remove('is-resizing');

        if (!hasMoved) {
          // Clicked without dragging: cycle span
          const nextSpan = getNextSpan(w.id, span);
          const payload = currentCcWidgets.map((item) => ({
            id: item.id,
            order: item.order,
            aspectRatio: item.id === w.id ? nextSpan : normalizeSpan(item.id, item.aspectRatio),
            enabled: item.enabled !== false
          }));
          window.winzen.send('update_control_center_widgets', { items: payload });
        } else {
          // Dragged: commit new size
          const finalSpan = `${curCols}x${curRows}`;
          const payload = currentCcWidgets.map((item) => ({
            id: item.id,
            order: item.order,
            aspectRatio: item.id === w.id ? finalSpan : normalizeSpan(item.id, item.aspectRatio),
            enabled: item.enabled !== false
          }));
          window.winzen.send('update_control_center_widgets', { items: payload });
        }
      };

      btnResize.addEventListener('pointerup', onPointerEnd);
      btnResize.addEventListener('pointercancel', onPointerEnd);
      card.appendChild(btnResize);

      // Drag and drop reordering
      card.addEventListener('dragstart', (ev) => {
        if (!isEditing || isDraggingResize) {
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
      content.className = `cc-card-content in-control-center cc-span-${span} cc-aspect-${span.replace('x', '-')}`;
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
          --slider-thumb-outer: ${isLightHost ? '#ffffff' : '#454545'};
          --bright-thumb-outer: ${isLightHost ? '#ffffff' : '#454545'};
          --vol-thumb-outer: ${isLightHost ? '#ffffff' : '#454545'};
          --bright-track-bg: ${isLightHost ? 'rgba(0, 0, 0, 0.3)' : 'rgba(255, 255, 255, 0.42)'};
          --vol-track-bg: ${isLightHost ? 'rgba(0, 0, 0, 0.3)' : 'rgba(255, 255, 255, 0.42)'};
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

        /* Clock styling - ensure date is visible across ALL sizes in Control Center */
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
        .clock-pill .date {
          display: block !important;
        }
        :host([data-span="1x1"]) .clock-pill,
        [data-span="1x1"] .clock-pill {
          flex-direction: column !important;
          align-items: center !important;
          justify-content: center !important;
          gap: 2px !important;
          padding: 4px 2px !important;
        }
        :host([data-span="1x1"]) .clock-pill .time,
        [data-span="1x1"] .clock-pill .time {
          font-size: 15px !important;
          font-weight: 700 !important;
          line-height: 1.1 !important;
        }
        :host([data-span="1x1"]) .clock-pill .date,
        [data-span="1x1"] .clock-pill .date {
          display: block !important;
          font-size: 9.5px !important;
          font-weight: 500 !important;
          opacity: 0.8 !important;
          white-space: nowrap !important;
          overflow: hidden !important;
          text-overflow: ellipsis !important;
          max-width: 68px !important;
          line-height: 1.1 !important;
        }
        :host([data-span="2x1"]) .clock-pill,
        [data-span="2x1"] .clock-pill {
          flex-direction: column !important;
          align-items: center !important;
          justify-content: center !important;
          gap: 4px !important;
          padding: 6px !important;
        }
        :host([data-span="2x1"]) .clock-pill .time,
        [data-span="2x1"] .clock-pill .time {
          font-size: 22px !important;
          font-weight: 700 !important;
          line-height: 1.1 !important;
        }
        :host([data-span="2x1"]) .clock-pill .date,
        [data-span="2x1"] .clock-pill .date {
          display: block !important;
          font-size: 11.5px !important;
          font-weight: 500 !important;
          opacity: 0.8 !important;
          line-height: 1.1 !important;
        }
        :host([data-span="1x2"]) .clock-pill,
        [data-span="1x2"] .clock-pill {
          display: flex !important;
          flex-direction: column !important;
          align-items: center !important;
          justify-content: center !important;
          gap: 10px !important;
          width: 100% !important;
          height: 100% !important;
          padding: 8px 4px !important;
          box-sizing: border-box !important;
        }
        :host([data-span="1x2"]) .clock-pill .time,
        [data-span="1x2"] .clock-pill .time {
          font-size: 22px !important;
          font-weight: 700 !important;
          line-height: 1.1 !important;
        }
        :host([data-span="1x2"]) .clock-pill .date,
        [data-span="1x2"] .clock-pill .date {
          display: block !important;
          font-size: 11px !important;
          font-weight: 500 !important;
          opacity: 0.75 !important;
          text-align: center !important;
          line-height: 1.3 !important;
          max-width: 64px !important;
        }
        :host([data-span="2x2"]) .clock-pill,
        [data-span="2x2"] .clock-pill {
          flex-direction: column !important;
          align-items: center !important;
          justify-content: center !important;
          gap: 8px !important;
          padding: 12px !important;
        }
        :host([data-span="2x2"]) .clock-pill .time,
        [data-span="2x2"] .clock-pill .time {
          font-size: 32px !important;
          font-weight: 700 !important;
          line-height: 1.1 !important;
        }
        :host([data-span="2x2"]) .clock-pill .date,
        [data-span="2x2"] .clock-pill .date {
          display: block !important;
          font-size: 13.5px !important;
          font-weight: 500 !important;
          opacity: 0.8 !important;
          line-height: 1.2 !important;
        }

        /* Telemetry styling (CPU, RAM, GPU) */
        .telemetry-card {
          display: flex;
          flex-direction: column;
          gap: 6px;
          width: 100%;
          justify-content: center;
        }
        .stat-col {
          min-width: 0 !important;
        }
        :host([data-span="1x1"]) .sys-pill,
        [data-span="1x1"] .sys-pill {
          flex-direction: column !important;
          align-items: stretch !important;
          justify-content: space-evenly !important;
          gap: 3px !important;
          padding: 4px 6px !important;
          box-sizing: border-box !important;
        }
        :host([data-span="1x1"]) .stat-head,
        [data-span="1x1"] .stat-head {
          font-size: 9px !important;
          display: flex !important;
          align-items: center !important;
          justify-content: space-between !important;
        }
        :host([data-span="1x1"]) .stat-head strong,
        [data-span="1x1"] .stat-head strong {
          font-size: 10px !important;
        }
        :host([data-span="1x1"]) .bar-track,
        [data-span="1x1"] .bar-track {
          height: 3px !important;
        }
        :host([data-span="2x1"]) .sys-pill,
        [data-span="2x1"] .sys-pill {
          flex-direction: row !important;
          align-items: center !important;
          justify-content: space-between !important;
          gap: 8px !important;
          padding: 6px 8px !important;
          box-sizing: border-box !important;
        }
        :host([data-span="2x1"]) .stat-col,
        [data-span="2x1"] .stat-col {
          flex: 1 1 0 !important;
          width: 0 !important;
          min-width: 0 !important;
          display: flex !important;
          flex-direction: column !important;
          gap: 3px !important;
        }
        :host([data-span="2x1"]) .stat-head,
        [data-span="2x1"] .stat-head {
          font-size: 9.5px !important;
          display: flex !important;
          justify-content: space-between !important;
        }
        :host([data-span="2x1"]) .stat-head strong,
        [data-span="2x1"] .stat-head strong {
          font-size: 11px !important;
        }
        :host([data-span="2x1"]) .bar-track,
        [data-span="2x1"] .bar-track {
          height: 4px !important;
        }
        :host([data-span="2x2"]) .sys-pill,
        [data-span="2x2"] .sys-pill {
          display: flex !important;
          flex-direction: column !important;
          align-items: stretch !important;
          justify-content: space-evenly !important;
          gap: 12px !important;
          padding: 12px !important;
          box-sizing: border-box !important;
        }
        :host([data-span="2x2"]) .stat-col,
        [data-span="2x2"] .stat-col {
          flex: 0 0 auto !important;
          width: 100% !important;
          min-width: 0 !important;
          display: flex !important;
          flex-direction: column !important;
          gap: 4px !important;
        }
        :host([data-span="2x2"]) .stat-head,
        [data-span="2x2"] .stat-head {
          display: flex !important;
          align-items: center !important;
          justify-content: space-between !important;
          font-size: 11px !important;
        }
        :host([data-span="2x2"]) .stat-head strong,
        [data-span="2x2"] .stat-head strong {
          font-size: 13px !important;
        }
        :host([data-span="2x2"]) .bar-track,
        [data-span="2x2"] .bar-track {
          height: 5px !important;
        }
        :host([data-span="1x2"]) .view-widget,
        [data-span="1x2"] .view-widget {
          width: 100% !important;
          height: 100% !important;
        }
        :host([data-span="1x2"]) .sys-pill,
        [data-span="1x2"] .sys-pill {
          display: flex !important;
          flex-direction: column !important;
          align-items: stretch !important;
          justify-content: space-evenly !important;
          height: 100% !important;
          width: 100% !important;
          gap: 10px !important;
          padding: 22px 8px 10px 8px !important;
          box-sizing: border-box !important;
        }
        :host([data-span="1x2"]) .stat-col,
        [data-span="1x2"] .stat-col {
          display: flex !important;
          flex-direction: column !important;
          gap: 3px !important;
          width: 100% !important;
          min-width: 0 !important;
        }
        :host([data-span="1x2"]) .stat-head,
        [data-span="1x2"] .stat-head {
          display: flex !important;
          align-items: center !important;
          justify-content: space-between !important;
          width: 100% !important;
          font-size: 10px !important;
        }
        :host([data-span="1x2"]) .stat-head span,
        [data-span="1x2"] .stat-head span {
          font-size: 9px !important;
          opacity: 0.75 !important;
        }
        :host([data-span="1x2"]) .stat-head strong,
        [data-span="1x2"] .stat-head strong {
          font-size: 10.5px !important;
          font-weight: 600 !important;
        }
        :host([data-span="1x2"]) .bar-track,
        [data-span="1x2"] .bar-track {
          width: 100% !important;
          height: 4px !important;
          max-height: 4px !important;
        }

        /* Network & Bluetooth Widget responsive rules */
        :host([data-span="1x1"]) .net-text-col,
        [data-span="1x1"] .net-text-col,
        :host([data-span="1x1"]) .bt-text-col,
        [data-span="1x1"] .bt-text-col {
          display: none !important;
        }
        :host([data-span="1x1"]) .network-pill,
        [data-span="1x1"] .network-pill,
        :host([data-span="1x1"]) .bluetooth-pill,
        [data-span="1x1"] .bluetooth-pill {
          flex-direction: column !important;
          align-items: center !important;
          justify-content: center !important;
          padding: 0 !important;
          width: 100% !important;
          height: 100% !important;
        }
        :host([data-span="1x1"]) .network-icon-wrap,
        [data-span="1x1"] .network-icon-wrap,
        :host([data-span="1x1"]) .bluetooth-icon-wrap,
        [data-span="1x1"] .bluetooth-icon-wrap {
          width: 38px !important;
          height: 38px !important;
          border-radius: 50% !important;
        }
        :host([data-span="1x2"]) .network-pill,
        [data-span="1x2"] .network-pill,
        :host([data-span="1x2"]) .bluetooth-pill,
        [data-span="1x2"] .bluetooth-pill {
          display: flex !important;
          flex-direction: column !important;
          align-items: center !important;
          justify-content: center !important;
          text-align: center !important;
          width: 100% !important;
          height: 100% !important;
          padding: 10px 4px !important;
          gap: 8px !important;
          box-sizing: border-box !important;
        }
        :host([data-span="1x2"]) .network-icon-wrap,
        [data-span="1x2"] .network-icon-wrap,
        :host([data-span="1x2"]) .bluetooth-icon-wrap,
        [data-span="1x2"] .bluetooth-icon-wrap {
          width: 44px !important;
          height: 44px !important;
          border-radius: 50% !important;
          display: flex !important;
          align-items: center !important;
          justify-content: center !important;
          flex-shrink: 0 !important;
        }
        :host([data-span="1x2"]) .network-icon-wrap .net-icon,
        [data-span="1x2"] .network-icon-wrap .net-icon,
        :host([data-span="1x2"]) .bluetooth-icon-wrap .bt-icon,
        [data-span="1x2"] .bluetooth-icon-wrap .bt-icon {
          width: 22px !important;
          height: 22px !important;
        }
        :host([data-span="1x2"]) .net-text-col,
        [data-span="1x2"] .net-text-col,
        :host([data-span="1x2"]) .bt-text-col,
        [data-span="1x2"] .bt-text-col {
          display: flex !important;
          flex-direction: column !important;
          align-items: center !important;
          justify-content: center !important;
          text-align: center !important;
          width: 100% !important;
          gap: 2px !important;
          overflow: hidden !important;
        }
        :host([data-span="1x2"]) .net-label,
        [data-span="1x2"] .net-label,
        :host([data-span="1x2"]) .bt-label,
        [data-span="1x2"] .bt-label {
          font-size: 12px !important;
          font-weight: 600 !important;
          white-space: nowrap !important;
          overflow: hidden !important;
          text-overflow: ellipsis !important;
          max-width: 66px !important;
          display: block !important;
        }
        :host([data-span="1x2"]) .net-sublabel,
        [data-span="1x2"] .net-sublabel,
        :host([data-span="1x2"]) .bt-sublabel,
        [data-span="1x2"] .bt-sublabel {
          font-size: 10px !important;
          opacity: 0.75 !important;
          white-space: nowrap !important;
          overflow: hidden !important;
          text-overflow: ellipsis !important;
          max-width: 66px !important;
          display: block !important;
        }
        :host([data-span="2x1"]) .network-pill,
        [data-span="2x1"] .network-pill,
        :host([data-span="2x1"]) .bluetooth-pill,
        [data-span="2x1"] .bluetooth-pill {
          flex-direction: row !important;
          align-items: center !important;
          padding: 0 10px !important;
          gap: 10px !important;
        }

        /* Brightness & Volume responsive rules */
        :host([data-span="1x1"]) .slider-row,
        [data-span="1x1"] .slider-row {
          flex-direction: column !important;
          align-items: center !important;
          justify-content: center !important;
          gap: 4px !important;
          padding: 4px !important;
        }
        :host([data-span="1x1"]) input[type="range"],
        [data-span="1x1"] input[type="range"] {
          display: none !important;
        }
        :host([data-span="1x1"]) .slider-icon,
        [data-span="1x1"] .slider-icon {
          width: 22px !important;
          height: 22px !important;
          order: 1 !important;
        }
        :host([data-span="1x1"]) .slider-val,
        [data-span="1x1"] .slider-val {
          font-size: 13px !important;
          font-weight: 600 !important;
          order: 2 !important;
          min-width: 0 !important;
          text-align: center !important;
        }
        :host([data-span="1x2"]) .view-widget,
        [data-span="1x2"] .view-widget {
          width: 100% !important;
          height: 100% !important;
          display: flex !important;
          align-items: center !important;
          justify-content: center !important;
        }
        :host([data-span="1x2"]) .slider-row,
        [data-span="1x2"] .slider-row {
          display: flex !important;
          flex-direction: column !important;
          align-items: center !important;
          justify-content: space-between !important;
          height: 100% !important;
          width: 100% !important;
          padding: 8px 4px !important;
          gap: 4px !important;
          box-sizing: border-box !important;
        }
        :host([data-span="1x2"]) .slider-val,
        [data-span="1x2"] .slider-val {
          font-size: 13px !important;
          font-weight: 600 !important;
          order: 1 !important;
          text-align: center !important;
          min-width: 0 !important;
        }
        :host([data-span="1x2"]) input[type="range"],
        [data-span="1x2"] input[type="range"] {
          display: block !important;
          order: 2 !important;
          -webkit-appearance: none !important;
          appearance: none !important;
          width: 82px !important;
          height: 6px !important;
          max-height: 6px !important;
          border-radius: 3px !important;
          outline: none !important;
          cursor: pointer !important;
          margin: 38px 0 !important;
          transform: rotate(-90deg) !important;
          touch-action: none !important;
        }
        :host([data-span="1x2"]) input[type="range"]::-webkit-slider-runnable-track,
        [data-span="1x2"] input[type="range"]::-webkit-slider-runnable-track {
          height: 6px !important;
          max-height: 6px !important;
          border-radius: 3px !important;
          background: transparent !important;
        }
        :host([data-span="1x2"]) input[type="range"]::-webkit-slider-thumb,
        [data-span="1x2"] input[type="range"]::-webkit-slider-thumb {
          -webkit-appearance: none !important;
          appearance: none !important;
          width: 18px !important;
          height: 18px !important;
          border-radius: 50% !important;
          background: var(--winzen-accent, #3684e3) !important;
          background-clip: padding-box !important;
          border: 4px solid var(--slider-thumb-outer, var(--bright-thumb-outer, var(--vol-thumb-outer, #454545))) !important;
          cursor: pointer !important;
          margin-top: -6px !important;
          box-shadow: 0 1px 4px rgba(0, 0, 0, 0.4) !important;
        }
        :host([data-span="1x2"]) .slider-icon,
        [data-span="1x2"] .slider-icon,
        :host([data-span="1x2"]) .slider-mute-btn,
        [data-span="1x2"] .slider-mute-btn {
          width: 20px !important;
          height: 20px !important;
          order: 3 !important;
          opacity: 0.85 !important;
          display: flex !important;
          align-items: center !important;
          justify-content: center !important;
        }

        :host([data-span="2x1"]) .slider-row,
        [data-span="2x1"] .slider-row {
          display: flex !important;
          flex-direction: row !important;
          align-items: center !important;
          gap: 6px !important;
          padding: 0 8px !important;
          width: 100% !important;
        }
        :host([data-span="2x1"]) input[type="range"],
        [data-span="2x1"] input[type="range"] {
          flex: 1 1 auto !important;
          min-width: 0 !important;
        }

        /* System Tray in 1x1, 2x1, 2x2 */
        :host([data-span="1x1"]) .tray-cc-grid,
        [data-span="1x1"] .tray-cc-grid {
          grid-template-columns: repeat(2, 1fr) !important;
          gap: 4px !important;
          padding: 4px !important;
        }
        :host([data-span="1x1"]) .tray-cc-tile,
        [data-span="1x1"] .tray-cc-tile {
          height: 26px !important;
        }
        :host([data-span="2x1"]) .tray-cc-grid,
        [data-span="2x1"] .tray-cc-grid {
          grid-template-columns: repeat(4, 1fr) !important;
          gap: 4px !important;
          padding: 4px 6px !important;
        }
        :host([data-span="1x2"]) .tray-cc-grid,
        [data-span="1x2"] .tray-cc-grid {
          grid-template-columns: repeat(2, 1fr) !important;
          grid-auto-rows: 28px !important;
          gap: 6px !important;
          padding: 6px 4px !important;
          width: 100% !important;
          height: 100% !important;
          align-content: center !important;
          justify-content: center !important;
        }

        /* Battery styling */
        .battery-widget {
          display: flex;
          align-items: center;
          justify-content: space-between;
          width: 100%;
          gap: 12px;
        }
        /* Battery 1x1 Compact Layout */
        :host([data-span="1x1"]) .battery-pill,
        [data-span="1x1"] .battery-pill {
          width: 100% !important;
          height: 100% !important;
          padding: 4px !important;
          box-sizing: border-box !important;
        }
        :host([data-span="1x1"]) .battery-pill > .battery-pct,
        [data-span="1x1"] .battery-pct,
        :host([data-span="1x1"]) .battery-pill > .battery-icon-wrap,
        [data-span="1x1"] .battery-icon-wrap {
          display: none !important;
        }
        :host([data-span="1x1"]) .cc-battery-body,
        [data-span="1x1"] .cc-battery-body {
          display: flex !important;
          flex-direction: column !important;
          align-items: center !important;
          justify-content: center !important;
          gap: 3px !important;
          width: 100% !important;
          height: 100% !important;
          padding: 2px !important;
          box-sizing: border-box !important;
          text-align: center !important;
        }
        :host([data-span="1x1"]) .cc-batt-top,
        [data-span="1x1"] .cc-batt-top {
          display: flex !important;
          flex-direction: column !important;
          align-items: center !important;
          justify-content: center !important;
          gap: 3px !important;
          padding-right: 0 !important;
          width: 100% !important;
        }
        :host([data-span="1x1"]) .cc-batt-icon-slot,
        [data-span="1x1"] .cc-batt-icon-slot {
          display: inline-flex !important;
          align-items: center !important;
          justify-content: center !important;
          transform: scale(1.25) !important;
          transform-origin: center center !important;
        }
        :host([data-span="1x1"]) .cc-batt-pct,
        [data-span="1x1"] .cc-batt-pct {
          font-size: 15px !important;
          font-weight: 700 !important;
          line-height: 1.1 !important;
          text-align: center !important;
        }
        :host([data-span="1x1"]) .cc-batt-status,
        [data-span="1x1"] .cc-batt-status {
          display: none !important;
        }
        :host([data-span="1x1"]) .cc-batt-track,
        [data-span="1x1"] .cc-batt-track {
          flex: none !important;
          width: 46px !important;
          max-width: 46px !important;
          height: 4px !important;
          max-height: 4px !important;
          margin: 2px auto 0 auto !important;
          border-radius: 2px !important;
        }
        :host([data-span="1x1"]) .cc-batt-fill,
        [data-span="1x1"] .cc-batt-fill {
          height: 4px !important;
          max-height: 4px !important;
          border-radius: 2px !important;
        }

        /* Battery Horizontal Banners (2x1, 3x1, 4x1) */
        :host([data-span="2x1"]) .cc-battery-body,
        [data-span="2x1"] .cc-battery-body,
        :host([data-span="3x1"]) .cc-battery-body,
        [data-span="3x1"] .cc-battery-body,
        :host([data-span="4x1"]) .cc-battery-body,
        [data-span="4x1"] .cc-battery-body {
          display: grid !important;
          grid-template-columns: auto 1fr !important;
          grid-template-rows: auto auto !important;
          row-gap: 8px !important;
          column-gap: 12px !important;
          width: 100% !important;
          height: 100% !important;
          align-content: center !important;
          padding: 6px 12px !important;
          box-sizing: border-box !important;
        }
        :host([data-span="2x1"]) .cc-batt-top,
        [data-span="2x1"] .cc-batt-top,
        :host([data-span="3x1"]) .cc-batt-top,
        [data-span="3x1"] .cc-batt-top,
        :host([data-span="4x1"]) .cc-batt-top,
        [data-span="4x1"] .cc-batt-top {
          grid-column: 1 !important;
          grid-row: 1 !important;
          display: flex !important;
          align-items: center !important;
          gap: 8px !important;
          padding-right: 0 !important;
        }
        :host([data-span="2x1"]) .cc-batt-pct,
        [data-span="2x1"] .cc-batt-pct,
        :host([data-span="3x1"]) .cc-batt-pct,
        [data-span="3x1"] .cc-batt-pct,
        :host([data-span="4x1"]) .cc-batt-pct,
        [data-span="4x1"] .cc-batt-pct {
          font-size: 18px !important;
          font-weight: 700 !important;
        }
        :host([data-span="2x1"]) .cc-batt-status,
        [data-span="2x1"] .cc-batt-status,
        :host([data-span="3x1"]) .cc-batt-status,
        [data-span="3x1"] .cc-batt-status,
        :host([data-span="4x1"]) .cc-batt-status,
        [data-span="4x1"] .cc-batt-status {
          grid-column: 2 !important;
          grid-row: 1 !important;
          text-align: right !important;
          align-self: center !important;
          font-size: 11.5px !important;
          font-weight: 500 !important;
          white-space: nowrap !important;
          overflow: hidden !important;
          text-overflow: ellipsis !important;
          display: block !important;
        }
        :host([data-span="2x1"]) .cc-batt-track,
        [data-span="2x1"] .cc-batt-track,
        :host([data-span="3x1"]) .cc-batt-track,
        [data-span="3x1"] .cc-batt-track,
        :host([data-span="4x1"]) .cc-batt-track,
        [data-span="4x1"] .cc-batt-track {
          grid-column: 1 / -1 !important;
          grid-row: 2 !important;
          width: 100% !important;
          height: 5px !important;
          border-radius: 2.5px !important;
          flex: none !important;
        }
        :host([data-span="2x1"]) .cc-batt-fill,
        [data-span="2x1"] .cc-batt-fill,
        :host([data-span="3x1"]) .cc-batt-fill,
        [data-span="3x1"] .cc-batt-fill,
        :host([data-span="4x1"]) .cc-batt-fill,
        [data-span="4x1"] .cc-batt-fill {
          height: 5px !important;
          border-radius: 2.5px !important;
        }

        /* Battery 2x2 Layout */
        :host([data-span="2x2"]) .cc-battery-body,
        [data-span="2x2"] .cc-battery-body {
          display: flex !important;
          flex-direction: column !important;
          align-items: center !important;
          justify-content: space-evenly !important;
          padding: 12px !important;
          text-align: center !important;
        }
        :host([data-span="2x2"]) .cc-batt-top,
        [data-span="2x2"] .cc-batt-top {
          display: flex !important;
          flex-direction: column !important;
          align-items: center !important;
          justify-content: center !important;
          gap: 8px !important;
          padding-right: 0 !important;
        }
        :host([data-span="2x2"]) .cc-batt-icon-slot,
        [data-span="2x2"] .cc-batt-icon-slot {
          transform: scale(1.6) !important;
        }
        :host([data-span="2x2"]) .cc-batt-pct,
        [data-span="2x2"] .cc-batt-pct {
          font-size: 28px !important;
          font-weight: 700 !important;
        }
        :host([data-span="2x2"]) .cc-batt-status,
        [data-span="2x2"] .cc-batt-status {
          font-size: 12px !important;
          display: block !important;
        }
        :host([data-span="2x2"]) .cc-batt-track,
        [data-span="2x2"] .cc-batt-track {
          width: 100% !important;
          max-width: 120px !important;
          height: 6px !important;
          margin: 0 auto !important;
        }
        :host([data-span="1x2"]) .battery-pill,
        [data-span="1x2"] .battery-pill {
          width: 100% !important;
          height: 100% !important;
          padding: 8px 4px !important;
          box-sizing: border-box !important;
        }
        :host([data-span="1x2"]) .battery-pill > .battery-pct,
        [data-span="1x2"] .battery-pill > .battery-pct,
        :host([data-span="1x2"]) .battery-pill > .battery-icon-wrap,
        [data-span="1x2"] .battery-pill > .battery-icon-wrap {
          display: none !important;
        }
        :host([data-span="1x2"]) .cc-battery-body,
        [data-span="1x2"] .cc-battery-body {
          display: flex !important;
          flex-direction: column !important;
          align-items: center !important;
          justify-content: space-around !important;
          text-align: center !important;
          width: 100% !important;
          height: 100% !important;
          padding: 8px 2px !important;
          gap: 6px !important;
          box-sizing: border-box !important;
        }
        :host([data-span="1x2"]) .cc-batt-top,
        [data-span="1x2"] .cc-batt-top {
          display: flex !important;
          flex-direction: column !important;
          align-items: center !important;
          justify-content: center !important;
          gap: 6px !important;
          padding-right: 0 !important;
          width: 100% !important;
        }
        :host([data-span="1x2"]) .cc-batt-icon-slot,
        [data-span="1x2"] .cc-batt-icon-slot {
          transform: scale(1.15) !important;
        }
        :host([data-span="1x2"]) .cc-batt-pct,
        [data-span="1x2"] .cc-batt-pct {
          font-size: 16px !important;
          font-weight: 700 !important;
        }
        :host([data-span="1x2"]) .cc-batt-status,
        [data-span="1x2"] .cc-batt-status {
          font-size: 9.5px !important;
          white-space: nowrap !important;
          overflow: hidden !important;
          text-overflow: ellipsis !important;
          max-width: 66px !important;
          text-align: center !important;
          opacity: 0.8 !important;
          display: block !important;
        }
        :host([data-span="1x2"]) .cc-batt-track,
        [data-span="1x2"] .cc-batt-track {
          flex: none !important;
          width: 100% !important;
          max-width: 58px !important;
          height: 5px !important;
          max-height: 5px !important;
          margin: 4px auto !important;
          border-radius: 3px !important;
        }
        :host([data-span="1x2"]) .cc-batt-fill,
        [data-span="1x2"] .cc-batt-fill {
          height: 5px !important;
          max-height: 5px !important;
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
      htmlWrapper.className = `cc-widget-wrapper in-control-center cc-span-${span} cc-aspect-${span.replace('x', '-')}`;
      htmlWrapper.dataset.inControlCenter = 'true';
      htmlWrapper.dataset.span = span;
      htmlWrapper.dataset.aspectRatio = span;
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
