const strip = root.getElementById('winStrip');
const showAppName = typeof widgetConfig !== 'undefined' && widgetConfig
  ? widgetConfig.showAppName !== false
  : true;
const combineApps = typeof widgetConfig !== 'undefined' && widgetConfig
  ? !!widgetConfig.combineApps
  : false;

// Client-side stable order tracker
const seenOrder = [];
let lastSig = '';
let activeMenuEl = null;

const FALLBACK_SVG = `<svg class="win-fallback-icon" viewBox="0 0 16 16" fill="currentColor"><path d="M2.5 2A1.5 1.5 0 0 0 1 3.5v9A1.5 1.5 0 0 0 2.5 14h11a1.5 1.5 0 0 0 1.5-1.5v-9A1.5 1.5 0 0 0 13.5 2h-11zM2 3.5a.5.5 0 0 1 .5-.5h11a.5.5 0 0 1 .5.5V5H2V3.5zm0 2.5h12v6.5a.5.5 0 0 1-.5.5h-11a.5.5 0 0 1-.5-.5V6z"/></svg>`;

// Dismiss context menu on click outside or Escape
window.addEventListener('click', closeContextMenu);
window.addEventListener('keydown', (e) => {
  if (e.key === 'Escape') closeContextMenu();
});

function closeContextMenu() {
  if (activeMenuEl) {
    activeMenuEl.remove();
    activeMenuEl = null;
  }
}

window.addEventListener('winzen:metrics', (e) => {
  const m = e.detail;
  if (!m || !Array.isArray(m.windows) || !strip) return;

  const currentMap = new Map();
  m.windows.forEach((w) => {
    currentMap.set(w.hwnd, w);
    if (!seenOrder.includes(w.hwnd)) {
      seenOrder.push(w.hwnd);
    }
  });

  for (let i = seenOrder.length - 1; i >= 0; i--) {
    if (!currentMap.has(seenOrder[i])) {
      seenOrder.splice(i, 1);
    }
  }

  const rawOrdered = seenOrder
    .map((hwnd) => currentMap.get(hwnd))
    .filter(Boolean);

  let itemsToRender = [];

  if (combineApps) {
    // Group windows by appId or processName while maintaining discovery order
    const appGroups = new Map();
    rawOrdered.forEach((w) => {
      const key = w.appId || w.processName.toLowerCase();
      if (!appGroups.has(key)) {
        appGroups.set(key, {
          key,
          appId: w.appId || key,
          processName: w.processName,
          title: w.title,
          iconDataUrl: w.iconDataUrl,
          windows: []
        });
      }
      const grp = appGroups.get(key);
      grp.windows.push(w);
      if (!grp.iconDataUrl && w.iconDataUrl) grp.iconDataUrl = w.iconDataUrl;
    });

    itemsToRender = Array.from(appGroups.values()).slice(0, 8);
  } else {
    itemsToRender = rawOrdered.slice(0, 8).map((w) => ({
      key: String(w.hwnd),
      appId: w.appId,
      processName: w.processName,
      title: w.title,
      iconDataUrl: w.iconDataUrl,
      windows: [w]
    }));
  }

  const sig = JSON.stringify({
    showAppName,
    combineApps,
    items: itemsToRender.map((it) => ({
      k: it.key,
      count: it.windows.length,
      hasActive: it.windows.some((w) => w.isActive),
      allMin: it.windows.every((w) => w.isMinimized),
      t: it.title,
      i: !!it.iconDataUrl
    }))
  });

  if (sig === lastSig) return;
  lastSig = sig;

  strip.innerHTML = '';
  if (itemsToRender.length === 0) {
    strip.innerHTML = `<div class="win-pill"><span class="win-title">No open windows</span></div>`;
    return;
  }

  itemsToRender.forEach((item) => {
    const isMulti = item.windows.length > 1;
    const hasActive = item.windows.some((w) => w.isActive);
    const allMinimized = item.windows.every((w) => w.isMinimized);

    const btn = document.createElement('button');
    const classes = ['win-pill'];
    if (hasActive) classes.push('active');
    if (allMinimized) classes.push('minimized');
    if (isMulti) classes.push('combined');
    if (!showAppName) classes.push('icon-only');

    btn.className = classes.join(' ');

    const tooltip = isMulti
      ? `${item.processName} (${item.windows.length} windows)\nLeft-click: Cycle windows\nRight-click: Menu`
      : `${item.processName} — ${item.title}\nLeft-click: Switch / Minimize\nRight-click: Menu`;
    btn.title = tooltip;

    const iconHtml = item.iconDataUrl
      ? `<img class="win-icon" src="${item.iconDataUrl}" alt="" />`
      : FALLBACK_SVG;

    let labelHtml = '';
    if (showAppName) {
      const displayTitle = isMulti
        ? `${escapeHtml(item.processName)}`
        : escapeHtml(item.title);
      const badgeHtml = isMulti ? `<span class="win-badge">${item.windows.length}</span>` : '';
      labelHtml = `<span class="win-title">${displayTitle}</span>${badgeHtml}`;
    } else if (isMulti) {
      labelHtml = `<span class="win-badge">${item.windows.length}</span>`;
    }

    btn.innerHTML = `${iconHtml}${labelHtml}`;

    // Left-click: Activate or Cycle
    btn.addEventListener('click', (ev) => {
      ev.stopPropagation();
      closeContextMenu();

      if (!isMulti) {
        window.winzen.tasks.activateWindow(item.windows[0].hwnd);
        return;
      }

      // Multi-window cycling
      const activeIdx = item.windows.findIndex((w) => w.isActive);
      if (activeIdx === -1) {
        window.winzen.tasks.activateWindow(item.windows[0].hwnd);
      } else {
        const nextIdx = (activeIdx + 1) % item.windows.length;
        window.winzen.tasks.activateWindow(item.windows[nextIdx].hwnd);
      }
    });

    // Right-click: Authentic Windows 11 Dark Context Menu
    btn.addEventListener('contextmenu', (ev) => {
      ev.preventDefault();
      ev.stopPropagation();
      openContextMenu(ev, item, btn);
    });

    strip.appendChild(btn);
  });
});

function openContextMenu(ev, item, anchorBtn) {
  closeContextMenu();

  const menu = document.createElement('div');
  menu.className = 'win11-menu';

  const isMulti = item.windows.length > 1;
  const headerTitle = isMulti
    ? `${item.processName} (${item.windows.length} windows)`
    : item.title || item.processName;

  let windowEntriesHtml = '';
  if (isMulti) {
    windowEntriesHtml = item.windows
      .map(
        (w, i) => `
        <button class="menu-item btn-switch-win" data-hwnd="${w.hwnd}">
          <svg viewBox="0 0 16 16" fill="currentColor"><path d="M2 3a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V3zm1 1v8h10V4H3z"/></svg>
          <span class="menu-item-text">${w.isActive ? '● ' : ''}${escapeHtml(w.title || `Window ${i + 1}`)}</span>
        </button>
      `
      )
      .join('') + `<div class="menu-divider"></div>`;
  }

  menu.innerHTML = `
    <div class="menu-header">
      <span class="menu-header-title">${escapeHtml(headerTitle)}</span>
    </div>
    ${windowEntriesHtml}
    ${
      !isMulti
        ? `<button class="menu-item btn-focus">
            <svg viewBox="0 0 16 16" fill="currentColor"><path d="M1.5 1a.5.5 0 0 0-.5.5v4a.5.5 0 0 1-1 0v-4A1.5 1.5 0 0 1 1.5 0h4a.5.5 0 0 1 0 1h-4zM10 .5a.5.5 0 0 1 .5-.5h4A1.5 1.5 0 0 1 16 1.5v4a.5.5 0 0 1-1 0v-4a.5.5 0 0 0-.5-.5h-4a.5.5 0 0 1-.5-.5zM.5 10a.5.5 0 0 1 .5.5v4a.5.5 0 0 0 .5.5h4a.5.5 0 0 1 0 1h-4A1.5 1.5 0 0 1 0 14.5v-4a.5.5 0 0 1 .5-.5zm15 0a.5.5 0 0 1 .5.5v4a1.5 1.5 0 0 1-1.5 1.5h-4a.5.5 0 0 1 0-1h4a.5.5 0 0 0 .5-.5v-4a.5.5 0 0 1 .5-.5z"/></svg>
            <span>Switch to window</span>
          </button>`
        : ''
    }
    <button class="menu-item btn-minimize">
      <svg viewBox="0 0 16 16" fill="currentColor"><path d="M2 8a.5.5 0 0 1 .5-.5h11a.5.5 0 0 1 0 1h-11A.5.5 0 0 1 2 8z"/></svg>
      <span>${isMulti ? 'Minimize all' : 'Minimize'}</span>
    </button>
    <div class="menu-divider"></div>
    <button class="menu-item btn-hide-app">
      <svg viewBox="0 0 16 16" fill="currentColor"><path d="M13.359 11.238C15.06 9.72 16 8 16 8s-3-5.5-8-5.5a7.028 7.028 0 0 0-2.79.588l.77.771A5.944 5.944 0 0 1 8 3.5c2.12 0 3.879 1.168 5.168 2.457A13.134 13.134 0 0 1 14.828 8c-.058.087-.122.183-.195.288-.335.48-.83 1.12-1.465 1.755-.165.165-.337.328-.517.486l.708.709zM11.297 9.176a3.5 3.5 0 0 0-4.474-4.474l.823.823a2.5 2.5 0 0 1 2.829 2.829l.822.822zm-2.943 1.299.822.822a3.5 3.5 0 0 1-4.474-4.474l.823.823a2.5 2.5 0 0 0 2.829 2.829zM3.35 5.47c-.18.16-.353.322-.518.487A13.134 13.134 0 0 0 1.172 8l.195.288c.335.48.83 1.12 1.465 1.755C4.121 11.332 5.881 12.5 8 12.5c.716 0 1.39-.133 2.02-.36l.77.772A7.029 7.029 0 0 1 8 13.5C3 13.5 0 8 0 8s.939-1.721 2.641-3.238l.708.709zm10.296 8.884-12-12 .708-.708 12 12-.708.708z"/></svg>
      <span>Hide app from taskbar</span>
    </button>
    <button class="menu-item danger btn-close-win">
      <svg viewBox="0 0 16 16" fill="currentColor"><path d="M4.646 4.646a.5.5 0 0 1 .708 0L8 7.293l2.646-2.647a.5.5 0 0 1 .708.708L8.707 8l2.647 2.646a.5.5 0 0 1-.708.708L8 8.707l-2.646 2.647a.5.5 0 0 1-.708-.708L7.293 8 4.646 5.354a.5.5 0 0 1 0-.708z"/></svg>
      <span>${isMulti ? 'Close all windows' : 'Close window'}</span>
    </button>
  `;

  // Attach actions
  menu.querySelectorAll('.btn-switch-win').forEach((btn) => {
    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      const hwnd = Number(btn.dataset.hwnd);
      window.winzen.tasks.activateWindow(hwnd);
      closeContextMenu();
    });
  });

  const btnFocus = menu.querySelector('.btn-focus');
  if (btnFocus) {
    btnFocus.addEventListener('click', (e) => {
      e.stopPropagation();
      window.winzen.tasks.activateWindow(item.windows[0].hwnd);
      closeContextMenu();
    });
  }

  const btnMin = menu.querySelector('.btn-minimize');
  if (btnMin) {
    btnMin.addEventListener('click', (e) => {
      e.stopPropagation();
      item.windows.forEach((w) => {
        if (!w.isMinimized) window.winzen.tasks.activateWindow(w.hwnd);
      });
      closeContextMenu();
    });
  }

  const btnHide = menu.querySelector('.btn-hide-app');
  if (btnHide) {
    btnHide.addEventListener('click', (e) => {
      e.stopPropagation();
      const first = item.windows[0];
      window.winzen.tasks.hideApp(first.hwnd, item.processName, item.title);
      closeContextMenu();
    });
  }

  const btnClose = menu.querySelector('.btn-close-win');
  if (btnClose) {
    btnClose.addEventListener('click', (e) => {
      e.stopPropagation();
      item.windows.forEach((w) => {
        window.winzen.tasks.closeWindow(w.hwnd);
      });
      closeContextMenu();
    });
  }

  // Position above the anchor button on taskbar
  const rect = anchorBtn.getBoundingClientRect();
  menu.style.bottom = `${window.innerHeight - rect.top + 6}px`;
  menu.style.left = `${Math.max(8, rect.left)}px`;

  root.appendChild(menu);
  activeMenuEl = menu;
}

function escapeHtml(s) {
  return String(s ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}
