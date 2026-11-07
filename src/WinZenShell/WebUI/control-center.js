function initControlCenter() {
  const ccContainer = document.getElementById('ccContainer');
  const ccGrid = document.getElementById('ccGrid');
  const ccTime = document.getElementById('ccTime');
  const btnEdit = document.getElementById('btnCcEdit');
  const btnEditText = document.getElementById('btnCcEditText');
  const btnClose = document.getElementById('btnCcClose');
  const ccEditFooter = document.getElementById('ccEditFooter');
  const ccAddSelect = document.getElementById('ccAddSelect');
  const btnCcAdd = document.getElementById('btnCcAdd');

  let isEditing = false;
  let currentCcWidgets = [];
  let availableWidgets = [];
  let draggedCardId = null;

  if (btnClose) {
    btnClose.addEventListener('click', () => {
      if (isEditing) toggleEditMode(false);
      window.winzen.controlCenter.close();
    });
  }

  if (btnEdit) {
    btnEdit.addEventListener('click', () => {
      toggleEditMode(!isEditing);
    });
  }

  function toggleEditMode(enable) {
    isEditing = enable;
    if (isEditing) {
      ccContainer.classList.add('is-editing');
      btnEdit.classList.add('is-active');
      btnEditText.textContent = 'Done';
      ccEditFooter.style.display = 'flex';
      populateAddSelect();
    } else {
      ccContainer.classList.remove('is-editing');
      btnEdit.classList.remove('is-active');
      btnEditText.textContent = 'Edit';
      ccEditFooter.style.display = 'none';
    }
  }

  function populateAddSelect() {
    ccAddSelect.innerHTML = '<option value="">+ Add widget to Control Center...</option>';
    const currentIds = new Set(currentCcWidgets.map((w) => w.id));
    availableWidgets.forEach((w) => {
      if (!currentIds.has(w.id)) {
        const opt = document.createElement('option');
        opt.value = w.id;
        opt.textContent = w.name || w.id;
        ccAddSelect.appendChild(opt);
      }
    });
  }

  if (btnCcAdd) {
    btnCcAdd.addEventListener('click', () => {
      const selectedId = ccAddSelect.value;
      if (!selectedId) return;

      const widgetMeta = availableWidgets.find((w) => w.id === selectedId);
      const newOrder = (currentCcWidgets.length + 1) * 10;
      const newAspectRatio = selectedId.includes('brightness') ? '1:2' : '1:1';

      const updated = currentCcWidgets.map((w, idx) => ({
        id: w.id,
        order: (idx + 1) * 10,
        aspectRatio: w.aspectRatio || '1:1',
        enabled: w.enabled !== false
      }));

      updated.push({
        id: selectedId,
        order: newOrder,
        aspectRatio: newAspectRatio,
        enabled: true
      });

      window.winzen.send('update_control_center_widgets', { items: updated });
    });
  }

  let currentTheme = 'dark';
  let currentAccent = '#3684E3';

  function applyStateUpdate(state) {
    if (!state) return;
    currentTheme = (state.settings && state.settings.theme) || 'dark';
    currentAccent = (state.settings && state.settings.accentColor) || '#3684E3';
    document.documentElement.setAttribute('data-theme', currentTheme);
    document.body.className = currentTheme === 'light' ? 'theme-light' : 'theme-dark';
    document.documentElement.style.setProperty('--winzen-accent', currentAccent);
    document.documentElement.style.setProperty('--winzen-border-accent', currentAccent);
    document.documentElement.style.setProperty('--win11-accent', currentAccent);

    availableWidgets = (state.widgets || []).slice();
    const ccList = state.controlCenterWidgets || [];
    currentCcWidgets = ccList.slice().sort((a, b) => (a.order ?? 10) - (b.order ?? 10));

    renderControlCenter(currentCcWidgets);
    if (isEditing) populateAddSelect();
  }

  window.addEventListener('winzen:state', (e) => {
    applyStateUpdate(e.detail);
  });

  function renderControlCenter(widgets) {
    ccGrid.innerHTML = '';

    if (widgets.length === 0) {
      ccGrid.innerHTML = `
        <div class="cc-card" data-aspect-ratio="1:2" style="text-align:center;color:#9ca3af;font-size:12px;padding:24px 12px;">
          No widgets in Control Center.<br />Click <strong>Edit</strong> above to add widgets.
        </div>
      `;
      return;
    }

    widgets.forEach((w) => {
      const ratio = w.aspectRatio || (w.id.includes('brightness') ? '1:2' : '1:1');

      const card = document.createElement('div');
      card.className = 'cc-card';
      card.dataset.aspectRatio = ratio;
      card.dataset.widgetId = w.id;
      card.draggable = true;

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
            aspectRatio: item.aspectRatio || '1:1',
            enabled: item.enabled !== false
          }));

          window.winzen.send('update_control_center_widgets', { items: payload });
        }
        draggedCardId = null;
      });

      // Card Header
      const header = document.createElement('div');
      header.className = 'cc-card-header';
      header.innerHTML = `
        <span class="cc-card-label">${escapeHtml(w.name || 'Widget')}</span>
        <div class="cc-card-actions">
          <button class="cc-btn-resize" title="Toggle Aspect Ratio">${ratio === '1:2' ? '1:1 Square' : '1:2 Wide'}</button>
          <button class="cc-btn-delete" title="Remove module">✕</button>
        </div>
      `;

      const btnResize = header.querySelector('.cc-btn-resize');
      btnResize.addEventListener('click', (ev) => {
        ev.stopPropagation();
        const nextRatio = ratio === '1:2' ? '1:1' : '1:2';
        const payload = currentCcWidgets.map((item) => ({
          id: item.id,
          order: item.order,
          aspectRatio: item.id === w.id ? nextRatio : (item.aspectRatio || '1:1'),
          enabled: item.enabled !== false
        }));
        window.winzen.send('update_control_center_widgets', { items: payload });
      });

      const btnDel = header.querySelector('.cc-btn-delete');
      btnDel.addEventListener('click', (ev) => {
        ev.stopPropagation();
        const filtered = currentCcWidgets.filter((item) => item.id !== w.id);
        const payload = filtered.map((item, idx) => ({
          id: item.id,
          order: (idx + 1) * 10,
          aspectRatio: item.aspectRatio || '1:1',
          enabled: item.enabled !== false
        }));
        window.winzen.send('update_control_center_widgets', { items: payload });
      });

      card.appendChild(header);

      // Card Content with Shadow DOM
      const content = document.createElement('div');
      content.className = 'cc-card-content';
      content.dataset.inControlCenter = 'true';
      content.dataset.aspectRatio = ratio;
      content.setAttribute('data-in-control-center', 'true');
      content.setAttribute('data-aspect-ratio', ratio);

      content.dataset.theme = currentTheme;
      content.setAttribute('data-theme', currentTheme);
      content.style.setProperty('--winzen-accent', currentAccent);
      content.style.setProperty('--winzen-border-accent', currentAccent);
      content.style.setProperty('--win11-accent', currentAccent);

      const shadow = content.attachShadow({ mode: 'open' });
      const styleEl = document.createElement('style');
      styleEl.textContent = `
        :host {
          --winzen-bg: #1c1c1c;
          --winzen-surface: #252525;
          --winzen-surface-hover: rgba(255, 255, 255, 0.0605);
          --winzen-surface-active: rgba(255, 255, 255, 0.04);
          --winzen-border: rgba(255, 255, 255, 0.08);
          --winzen-border-subtle: rgba(255, 255, 255, 0.05);
          --winzen-border-accent: ${currentAccent};
          --winzen-text: #ffffff;
          --winzen-text-secondary: #c5c5c5;
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
      wrapper.className = `widget-root-container in-control-center cc-aspect-${ratio.replace(':', '-')} theme-${currentTheme}`;
      wrapper.dataset.inControlCenter = 'true';
      wrapper.dataset.aspectRatio = ratio;
      wrapper.dataset.theme = currentTheme;
      wrapper.setAttribute('data-in-control-center', 'true');
      wrapper.setAttribute('data-theme', currentTheme);
      wrapper.setAttribute('data-aspect-ratio', ratio);
      wrapper.style.cssText = 'width:100%; height:100%; flex:1; display:flex; flex-direction:column; justify-content:center; align-items:stretch; min-height:0;';
      wrapper.innerHTML = w.htmlContent || '';
      shadow.appendChild(wrapper);

      card.appendChild(content);
      ccGrid.appendChild(card);

      if (w.jsContent) {
        try {
          shadow.createElement = (...args) => window.document.createElement(...args);
          const configCopy = {
            ...w,
            displayMode: 'widget',
            inControlCenter: true,
            aspectRatio: ratio
          };
          const fn = new Function('root', 'document', 'winzen', 'widgetConfig', w.jsContent);
          fn(shadow, shadow, window.winzen, configCopy);
        } catch (err) {
          console.error(`Control Center widget script error (${w.id}):`, err);
        }
      }
    });

    if (window.winzen.metrics) {
      window.dispatchEvent(new CustomEvent('winzen:metrics', { detail: window.winzen.metrics }));
    }
  }

  window.addEventListener('winzen:metrics', (e) => {
    const m = e.detail;
    if (!m) return;
    if (m.timeFormatted && ccTime) {
      ccTime.textContent = m.timeFormatted;
    }
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

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initControlCenter);
} else {
  initControlCenter();
}
