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
const STICKY_NOTES_SVG = `<svg class="win-icon" viewBox="0 0 24 24" fill="none"><rect x="3" y="3" width="18" height="18" rx="3" fill="#FFC83B"/><path d="M15 15l6 6h-6v-6z" fill="#E5A823"/><line x1="7" y1="8" x2="17" y2="8" stroke="#7A5200" stroke-width="1.8" stroke-linecap="round"/><line x1="7" y1="12" x2="14" y2="12" stroke="#7A5200" stroke-width="1.8" stroke-linecap="round"/></svg>`;

function getAppIconHtml(item) {
  const id = (item.appId || '').toLowerCase();
  const proc = (item.processName || '').toLowerCase();
  const tit = (item.title || '').toLowerCase();
  if (id.includes('notes') || proc.includes('notes') || proc.includes('sticky') || tit.includes('sticky')) {
    return STICKY_NOTES_SVG;
  }
  if (item.iconDataUrl) {
    return `<img class="win-icon" src="${item.iconDataUrl}" alt="" />`;
  }
  return FALLBACK_SVG;
}

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
  if (!m || !strip) return;

  const rawWindows = Array.isArray(m.windows) ? m.windows : [];
  const pinnedList = Array.isArray(m.pinnedApps) ? m.pinnedApps : [];

  const currentMap = new Map();
  rawWindows.forEach((w) => {
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

  // Group running windows by appId / processName
  const appGroups = new Map();
  rawOrdered.forEach((w) => {
    const key = (w.appId || w.processName).toLowerCase();
    if (!appGroups.has(key)) {
      appGroups.set(key, {
        key,
        appId: w.appId || key,
        processName: w.processName,
        title: w.title,
        exePath: w.exePath || '',
        iconDataUrl: w.iconDataUrl || '',
        isPinned: false,
        isClosedPinned: false,
        windows: []
      });
    }
    const grp = appGroups.get(key);
    grp.windows.push(w);
    if (!grp.iconDataUrl && w.iconDataUrl) grp.iconDataUrl = w.iconDataUrl;
    if (!grp.exePath && w.exePath) grp.exePath = w.exePath;
  });

  const orderedItems = [];
  const usedRunningKeys = new Set();

  // 1. Process Pinned Apps first (Windows 11 pinned sequence)
  pinnedList.forEach((pin) => {
    const pKey = (pin.appId || pin.processName).toLowerCase();
    const runningMatch = appGroups.get(pKey);

    if (runningMatch) {
      runningMatch.isPinned = true;
      if (!runningMatch.exePath && pin.exePath) runningMatch.exePath = pin.exePath;
      if (!runningMatch.iconDataUrl && pin.iconDataUrl) runningMatch.iconDataUrl = pin.iconDataUrl;
      orderedItems.push(runningMatch);
      usedRunningKeys.add(pKey);
    } else {
      orderedItems.push({
        key: `pinned:${pKey}`,
        appId: pin.appId,
        processName: pin.processName,
        title: pin.title || pin.processName,
        exePath: pin.exePath || '',
        iconDataUrl: pin.iconDataUrl || '',
        isPinned: true,
        isClosedPinned: true,
        windows: []
      });
    }
  });

  // 2. Append unpinned running apps in discovery order
  for (const [key, grp] of appGroups.entries()) {
    if (!usedRunningKeys.has(key)) {
      orderedItems.push(grp);
    }
  }

  const itemsToRender = orderedItems.slice(0, 10);

  const sig = JSON.stringify({
    showAppName,
    combineApps,
    items: itemsToRender.map((it) => ({
      k: it.key,
      count: it.windows.length,
      isClosed: !!it.isClosedPinned,
      isPinned: !!it.isPinned,
      hasActive: it.windows.some((w) => w.isActive),
      allMin: it.windows.length > 0 && it.windows.every((w) => w.isMinimized),
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
    const allMinimized = item.windows.length > 0 && item.windows.every((w) => w.isMinimized);

    const btn = document.createElement('button');
    const classes = ['win-pill'];
    if (item.isClosedPinned) classes.push('pinned');
    if (hasActive) classes.push('active');
    if (allMinimized) classes.push('minimized');
    if (isMulti) classes.push('combined');
    if (!showAppName) classes.push('icon-only');

    btn.className = classes.join(' ');

    const tooltip = item.isClosedPinned
      ? `${item.processName}\nLeft-click: Open\nRight-click: Options`
      : (isMulti
          ? `${item.processName} (${item.windows.length} windows)\nLeft-click: Cycle windows\nRight-click: Options`
          : `${item.processName} — ${item.title}\nLeft-click: Switch / Minimize\nRight-click: Options`);
    btn.title = tooltip;

    const iconHtml = getAppIconHtml(item);

    let labelHtml = '';
    if (showAppName) {
      const displayTitle = isMulti
        ? `${escapeHtml(item.processName)}`
        : escapeHtml(item.title || item.processName);
      const badgeHtml = isMulti ? `<span class="win-badge">${item.windows.length}</span>` : '';
      labelHtml = `<span class="win-title">${displayTitle}</span>${badgeHtml}`;
    } else if (isMulti) {
      labelHtml = `<span class="win-badge">${item.windows.length}</span>`;
    }

    btn.innerHTML = `${iconHtml}${labelHtml}`;

    // Left-click: Launch if closed pinned, or Activate / Cycle
    btn.addEventListener('click', (ev) => {
      ev.stopPropagation();
      closeContextMenu();

      if (item.isClosedPinned) {
        if (window.winzen && window.winzen.tasks && typeof window.winzen.tasks.launchApp === 'function') {
          window.winzen.tasks.launchApp(item.exePath, item.processName);
        } else if (window.winzen && typeof window.winzen.send === 'function') {
          window.winzen.send('launch_app', { exePath: item.exePath, processName: item.processName });
        }
        return;
      }

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

  if (window.winzen && window.winzen.tasks && typeof window.winzen.tasks.showContextMenu === 'function') {
    window.winzen.tasks.showContextMenu({
      appId: item.appId || '',
      processName: item.processName || '',
      title: item.title || '',
      exePath: item.exePath || '',
      iconDataUrl: item.iconDataUrl || '',
      isPinned: !!item.isPinned,
      windows: (item.windows || []).map((w) => ({
        hwnd: Number(w.hwnd),
        title: String(w.title || ''),
        isActive: !!w.isActive,
        isMinimized: !!w.isMinimized
      }))
    });
    return;
  }

  const menu = document.createElement('div');
  menu.className = 'win11-menu';

  const isMulti = item.windows.length > 1;
  const headerTitle = isMulti
    ? `${item.processName} (${item.windows.length} windows)`
    : item.title || item.processName;

  if (item.isClosedPinned) {
    // Closed pinned app context menu
    menu.innerHTML = `
      <div class="menu-header">
        ${item.iconDataUrl ? `<img class="win-icon" src="${item.iconDataUrl}" style="width:16px;height:16px;"/>` : ''}
        <span class="menu-header-title">${escapeHtml(headerTitle)}</span>
      </div>
      <button class="menu-item btn-launch">
        <svg viewBox="0 0 16 16" fill="none" stroke="#60cdff" stroke-width="1.25" stroke-linecap="round"><path d="M7.5 8.5L13 3M9.5 3H13V6.5M6.5 3.5H3V13H12.5V9.5"/></svg>
        <span>Open</span>
      </button>
      <div class="menu-divider"></div>
      <button class="menu-item btn-unpin">
        <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.25" stroke-linecap="round"><path d="M6 10L2.5 13.5M4.5 8.5L7.5 11.5M5.5 7.5L8.5 10.5L11 8L8 5Z"/><line x1="2.5" y1="2.5" x2="13.5" y2="13.5" stroke="#ff7887" stroke-width="1.4"/></svg>
        <span>Unpin from taskbar</span>
      </button>
    `;

    const btnLaunch = menu.querySelector('.btn-launch');
    if (btnLaunch) {
      btnLaunch.addEventListener('click', (e) => {
        e.stopPropagation();
        if (window.winzen && window.winzen.tasks) {
          window.winzen.tasks.launchApp(item.exePath, item.processName);
        }
        closeContextMenu();
      });
    }

    const btnUnpin = menu.querySelector('.btn-unpin');
    if (btnUnpin) {
      btnUnpin.addEventListener('click', (e) => {
        e.stopPropagation();
        if (window.winzen && window.winzen.tasks) {
          window.winzen.tasks.unpinApp(item.appId, item.processName);
        }
        closeContextMenu();
      });
    }
  } else {
    // Running app context menu
    let windowEntriesHtml = '';
    if (isMulti) {
      windowEntriesHtml = item.windows
        .map(
          (w, i) => `
          <button class="menu-item btn-switch-win" data-hwnd="${w.hwnd}">
            <svg viewBox="0 0 16 16" fill="none" stroke="${w.isActive ? '#60cdff' : '#c5c5c5'}" stroke-width="1.2"><rect x="2" y="2.5" width="12" height="10.5" rx="1.5"/><line x1="2" y1="5.5" x2="14" y2="5.5"/></svg>
            <span class="menu-item-text">${w.isActive ? '● ' : ''}${escapeHtml(w.title || `Window ${i + 1}`)}</span>
          </button>
        `
        )
        .join('') + `<div class="menu-divider"></div>`;
    }

    menu.innerHTML = `
      <div class="menu-header">
        ${item.iconDataUrl ? `<img class="win-icon" src="${item.iconDataUrl}" style="width:16px;height:16px;"/>` : ''}
        <span class="menu-header-title">${escapeHtml(headerTitle)}</span>
      </div>
      ${windowEntriesHtml}
      ${
        !isMulti
          ? `<button class="menu-item btn-focus">
              <svg viewBox="0 0 16 16" fill="none" stroke="#60cdff" stroke-width="1.3"><rect x="2" y="2" width="9" height="8" stroke="#8ca0b4" stroke-width="1.1"/><rect x="5" y="5" width="9" height="8" fill="#2c2c2c"/><line x1="5" y1="7.5" x2="14" y2="7.5"/></svg>
              <span>Switch to window</span>
            </button>`
          : ''
      }
      <button class="menu-item btn-minimize">
        <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="3.5" y1="8" x2="12.5" y2="8"/></svg>
        <span>${isMulti ? 'Minimize all' : 'Minimize'}</span>
      </button>
      <div class="menu-divider"></div>
      ${
        item.isPinned
          ? `<button class="menu-item btn-unpin">
              <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.25" stroke-linecap="round"><path d="M6 10L2.5 13.5M4.5 8.5L7.5 11.5M5.5 7.5L8.5 10.5L11 8L8 5Z"/><line x1="2.5" y1="2.5" x2="13.5" y2="13.5" stroke="#ff7887" stroke-width="1.4"/></svg>
              <span>Unpin from taskbar</span>
            </button>`
          : `<button class="menu-item btn-pin">
              <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.25" stroke-linecap="round"><path d="M6 10L2.5 13.5M4.5 8.5L7.5 11.5M5.5 7.5L8.5 10.5L11 8L8 5Z"/><line x1="10" y1="3.5" x2="12.5" y2="6"/></svg>
              <span>Pin to taskbar</span>
            </button>`
      }
      <button class="menu-item btn-hide-app">
        <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.25" stroke-linecap="round"><path d="M2.5 8C4.5 4.5 11.5 4.5 13.5 8M2.5 8C4.5 11.5 11.5 11.5 13.5 8"/><circle cx="8" cy="8" r="1.3" fill="currentColor"/><line x1="3" y1="3" x2="13" y2="13"/></svg>
        <span>Hide app from taskbar</span>
      </button>
      <button class="menu-item danger btn-close-win">
        <svg viewBox="0 0 16 16" fill="none" stroke="#ff7382" stroke-width="1.5" stroke-linecap="round"><line x1="4" y1="4" x2="12" y2="12"/><line x1="12" y1="4" x2="4" y2="12"/></svg>
        <span>${isMulti ? 'Close all windows' : 'Close window'}</span>
      </button>
    `;

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

    const btnPin = menu.querySelector('.btn-pin');
    if (btnPin) {
      btnPin.addEventListener('click', (e) => {
        e.stopPropagation();
        if (window.winzen && window.winzen.tasks) {
          window.winzen.tasks.pinApp(item.appId, item.processName, item.title, item.exePath, item.iconDataUrl);
        }
        closeContextMenu();
      });
    }

    const btnUnpin = menu.querySelector('.btn-unpin');
    if (btnUnpin) {
      btnUnpin.addEventListener('click', (e) => {
        e.stopPropagation();
        if (window.winzen && window.winzen.tasks) {
          window.winzen.tasks.unpinApp(item.appId, item.processName);
        }
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
