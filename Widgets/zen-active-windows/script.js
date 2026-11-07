const strip = root.getElementById('winStrip');
let showAppName = typeof widgetConfig !== 'undefined' && widgetConfig
  ? widgetConfig.showAppName !== false
  : true;
let combineApps = typeof widgetConfig !== 'undefined' && widgetConfig
  ? !!widgetConfig.combineApps
  : false;
let showThumbnails = typeof widgetConfig !== 'undefined' && widgetConfig
  ? widgetConfig.showThumbnails !== false
  : true;

let lastCachedMetrics = (typeof window !== 'undefined' && window.winzen && window.winzen.metrics)
  ? window.winzen.metrics
  : null;

// Client-side stable order & drag-rearrange trackers
const seenOrder = [];
let customOrder = [];
let customAppOrder = [];
let isDraggingItem = false;
let suppressClickUntil = 0;
let lastSig = '';
let activeMenuEl = null;
let hoverTimer = null;

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

window.addEventListener('winzen:state', (e) => {
  const state = e.detail;
  if (!state || !Array.isArray(state.widgets)) return;
  const cfg = state.widgets.find((w) => w.id === 'community.zen-active-windows');
  if (!cfg) return;

  const newShowAppName = cfg.showAppName !== false;
  const newCombineApps = !!cfg.combineApps;
  const newShowThumbnails = cfg.showThumbnails !== false;

  if (newShowAppName !== showAppName || newCombineApps !== combineApps || newShowThumbnails !== showThumbnails) {
    const combineChanged = newCombineApps !== combineApps;
    showAppName = newShowAppName;
    combineApps = newCombineApps;
    showThumbnails = newShowThumbnails;
    if (combineChanged) {
      customOrder = [];
    }
    lastSig = '';
    const metricsToRender = lastCachedMetrics || (window.winzen && window.winzen.metrics) || null;
    if (metricsToRender) {
      lastCachedMetrics = metricsToRender;
      renderWindows(metricsToRender);
    }
  }
});

window.addEventListener('winzen:metrics', (e) => {
  lastCachedMetrics = e.detail;
  if (!isDraggingItem) {
    renderWindows(lastCachedMetrics);
  }
});

function syncOrderFromDom() {
  if (!strip) return;
  const pills = Array.from(strip.querySelectorAll('.win-pill[data-item-key]'));
  if (pills.length === 0) return;

  customOrder = pills.map((el) => el.dataset.itemKey).filter(Boolean);

  const newAppOrder = [];
  const newHwndOrder = [];
  pills.forEach((el) => {
    const ak = el.dataset.appKey;
    if (ak && !newAppOrder.includes(ak)) {
      newAppOrder.push(ak);
    }
    const hwndsStr = el.dataset.hwnds || '';
    hwndsStr.split(',').forEach((hStr) => {
      const h = Number(hStr);
      if (h && !newHwndOrder.includes(h)) {
        newHwndOrder.push(h);
      }
    });
  });

  if (newAppOrder.length > 0) {
    customAppOrder = newAppOrder;
  }
  if (newHwndOrder.length > 0) {
    seenOrder.forEach((h) => {
      if (!newHwndOrder.includes(h)) newHwndOrder.push(h);
    });
    seenOrder.length = 0;
    newHwndOrder.forEach((h) => seenOrder.push(h));
  }
}

function renderWindows(m) {
  if (!m || !strip || isDraggingItem) return;

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

  const pinnedByKey = new Map();
  pinnedList.forEach((pin) => {
    const pKey = (pin.appId || pin.processName || '').toLowerCase();
    if (pKey) pinnedByKey.set(pKey, pin);
  });

  const baseItems = [];

  if (combineApps) {
    // Combine mode ON: group running windows of the same app into a single button
    const appGroups = new Map();
    rawOrdered.forEach((w) => {
      const appKey = (w.appId || w.processName || '').toLowerCase();
      if (!appGroups.has(appKey)) {
        appGroups.set(appKey, {
          key: `app:${appKey}`,
          appKey,
          appId: w.appId || appKey,
          processName: w.processName,
          title: w.title,
          exePath: w.exePath || '',
          iconDataUrl: w.iconDataUrl || '',
          isPinned: false,
          isClosedPinned: false,
          windows: []
        });
      }
      const grp = appGroups.get(appKey);
      grp.windows.push(w);
      if (!grp.iconDataUrl && w.iconDataUrl) grp.iconDataUrl = w.iconDataUrl;
      if (!grp.exePath && w.exePath) grp.exePath = w.exePath;
    });

    const usedRunningKeys = new Set();

    // 1. Pinned apps first
    pinnedList.forEach((pin) => {
      const pKey = (pin.appId || pin.processName || '').toLowerCase();
      const runningMatch = appGroups.get(pKey);
      if (runningMatch) {
        runningMatch.isPinned = true;
        if (!runningMatch.exePath && pin.exePath) runningMatch.exePath = pin.exePath;
        if (!runningMatch.iconDataUrl && pin.iconDataUrl) runningMatch.iconDataUrl = pin.iconDataUrl;
        baseItems.push(runningMatch);
        usedRunningKeys.add(pKey);
      } else {
        baseItems.push({
          key: `app:${pKey}`,
          appKey: pKey,
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

    // 2. Unpinned running app groups
    for (const [appKey, grp] of appGroups.entries()) {
      if (!usedRunningKeys.has(appKey)) {
        baseItems.push(grp);
      }
    }
  } else {
    // Combine mode OFF: every open window is its own separate button
    const runningByAppKey = new Map();
    rawOrdered.forEach((w) => {
      const appKey = (w.appId || w.processName || '').toLowerCase();
      const pin = pinnedByKey.get(appKey);
      const item = {
        key: `win:${w.hwnd}`,
        appKey,
        appId: w.appId || appKey,
        processName: w.processName,
        title: w.title || w.processName,
        exePath: w.exePath || (pin ? pin.exePath : '') || '',
        iconDataUrl: w.iconDataUrl || (pin ? pin.iconDataUrl : '') || '',
        isPinned: !!pin,
        isClosedPinned: false,
        windows: [w]
      };
      if (!runningByAppKey.has(appKey)) {
        runningByAppKey.set(appKey, []);
      }
      runningByAppKey.get(appKey).push(item);
    });

    const usedAppKeys = new Set();

    // 1. Pinned apps first (if running, insert each individual window; if closed, insert closed pinned button)
    pinnedList.forEach((pin) => {
      const pKey = (pin.appId || pin.processName || '').toLowerCase();
      const winItems = runningByAppKey.get(pKey);
      if (winItems && winItems.length > 0) {
        winItems.forEach((wi) => baseItems.push(wi));
        usedAppKeys.add(pKey);
      } else {
        baseItems.push({
          key: `pin:${pKey}`,
          appKey: pKey,
          appId: pin.appId,
          processName: pin.processName,
          title: pin.title || pin.processName,
          exePath: pin.exePath || '',
          iconDataUrl: pin.iconDataUrl || '',
          isPinned: true,
          isClosedPinned: true,
          windows: []
        });
        usedAppKeys.add(pKey);
      }
    });

    // 2. Unpinned individual running windows in seenOrder
    rawOrdered.forEach((w) => {
      const appKey = (w.appId || w.processName || '').toLowerCase();
      if (!usedAppKeys.has(appKey)) {
        baseItems.push({
          key: `win:${w.hwnd}`,
          appKey,
          appId: w.appId || appKey,
          processName: w.processName,
          title: w.title || w.processName,
          exePath: w.exePath || '',
          iconDataUrl: w.iconDataUrl || '',
          isPinned: false,
          isClosedPinned: false,
          windows: [w]
        });
      }
    });
  }

  // Apply user's drag-rearranged order (customOrder -> fallback customAppOrder -> discovery order)
  if (customOrder.length === 0 && customAppOrder.length > 0) {
    baseItems.sort((a, b) => {
      const ia = customAppOrder.indexOf(a.appKey);
      const ib = customAppOrder.indexOf(b.appKey);
      const oa = ia === -1 ? 9999 : ia;
      const ob = ib === -1 ? 9999 : ib;
      return oa - ob;
    });
  }

  const validKeys = new Set(baseItems.map((it) => it.key));
  customOrder = customOrder.filter((k) => validKeys.has(k));
  baseItems.forEach((it) => {
    if (!customOrder.includes(it.key)) {
      customOrder.push(it.key);
    }
  });

  const itemByKey = new Map(baseItems.map((it) => [it.key, it]));
  const orderedItems = customOrder.map((k) => itemByKey.get(k)).filter(Boolean);

  const itemsToRender = orderedItems.slice(0, 24);

  const sig = JSON.stringify({
    showAppName,
    combineApps,
    showThumbnails,
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
    btn.dataset.itemKey = item.key;
    btn.dataset.appKey = item.appKey || '';
    btn.dataset.hwnds = item.windows.map((w) => w.hwnd).join(',');

    const iconHtml = getAppIconHtml(item);

    let labelHtml = '';
    if (showAppName) {
      const displayTitle = isMulti
        ? `${escapeHtml(item.processName)}`
        : escapeHtml(item.title || item.processName);
      const badgeHtml = isMulti ? `<span class="win-badge">${item.windows.length}</span>` : '';
      labelHtml = `<span class="win-title">${displayTitle}</span>${badgeHtml}`;
    }

    btn.innerHTML = `${iconHtml}${labelHtml}`;

    const buildSelectorPayload = () => {
      const rect = btn.getBoundingClientRect();
      return {
        appId: item.appId || '',
        processName: item.processName || '',
        iconDataUrl: item.iconDataUrl || '',
        x: rect.left,
        width: rect.width,
        showThumbnails,
        windows: item.windows.map((w) => ({
          hwnd: Number(w.hwnd),
          title: String(w.title || item.processName || ''),
          isActive: !!w.isActive,
          isMinimized: !!w.isMinimized
        }))
      };
    };

    // Drag-to-Rearrange taskbar buttons horizontally
    btn.addEventListener('pointerdown', (e) => {
      if (e.button !== 0) return;

      const startX = e.clientX;
      const pointerId = e.pointerId;
      let draggedMoved = false;

      const onPointerMove = (moveEv) => {
        if (!draggedMoved && Math.abs(moveEv.clientX - startX) > 4) {
          draggedMoved = true;
          isDraggingItem = true;
          try {
            btn.setPointerCapture(pointerId);
          } catch (_) {}
          btn.classList.add('dragging');
          if (hoverTimer) {
            clearTimeout(hoverTimer);
            hoverTimer = null;
          }
          closeContextMenu();
          if (window.winzen && window.winzen.tasks && typeof window.winzen.tasks.leaveWindowSelector === 'function') {
            window.winzen.tasks.leaveWindowSelector();
          } else if (window.winzen && typeof window.winzen.send === 'function') {
            window.winzen.send('leave_window_selector');
          }
        }

        if (!draggedMoved) return;

        const pills = Array.from(strip.querySelectorAll('.win-pill[data-item-key]'));
        const idx = pills.indexOf(btn);
        if (idx === -1) return;

        if (idx > 0) {
          const prev = pills[idx - 1];
          const prevRect = prev.getBoundingClientRect();
          if (moveEv.clientX < prevRect.left + prevRect.width / 2) {
            strip.insertBefore(btn, prev);
            syncOrderFromDom();
            return;
          }
        }

        if (idx < pills.length - 1) {
          const next = pills[idx + 1];
          const nextRect = next.getBoundingClientRect();
          if (moveEv.clientX > nextRect.left + nextRect.width / 2) {
            strip.insertBefore(btn, next.nextSibling);
            syncOrderFromDom();
          }
        }
      };

      const onPointerEnd = () => {
        btn.removeEventListener('pointermove', onPointerMove);
        btn.removeEventListener('pointerup', onPointerEnd);
        btn.removeEventListener('pointercancel', onPointerEnd);

        if (draggedMoved) {
          try {
            btn.releasePointerCapture(pointerId);
          } catch (_) {}
          btn.classList.remove('dragging');
          syncOrderFromDom();
          isDraggingItem = false;
          suppressClickUntil = Date.now() + 140;
          lastSig = '';
          if (lastCachedMetrics) {
            renderWindows(lastCachedMetrics);
          }
        }
      };

      btn.addEventListener('pointermove', onPointerMove);
      btn.addEventListener('pointerup', onPointerEnd);
      btn.addEventListener('pointercancel', onPointerEnd);
    });

    // Hover:
    // - If showThumbnails is ON: show thumbnail preview for ANY running app (1 or more windows)
    // - If showThumbnails is OFF: show compact window list ONLY when multi-window (if 1 window, show nothing on hover)
    btn.addEventListener('mouseenter', () => {
      if (isDraggingItem) return;
      if (hoverTimer) {
        clearTimeout(hoverTimer);
        hoverTimer = null;
      }

      if (item.isClosedPinned || (!showThumbnails && !isMulti)) {
        if (window.winzen && window.winzen.tasks && typeof window.winzen.tasks.leaveWindowSelector === 'function') {
          window.winzen.tasks.leaveWindowSelector();
        } else if (window.winzen && typeof window.winzen.send === 'function') {
          window.winzen.send('leave_window_selector');
        }
        return;
      }

      hoverTimer = setTimeout(() => {
        if (isDraggingItem) return;
        const payload = buildSelectorPayload();
        if (window.winzen && window.winzen.tasks && typeof window.winzen.tasks.hoverWindowSelector === 'function') {
          window.winzen.tasks.hoverWindowSelector(payload);
        } else if (window.winzen && typeof window.winzen.send === 'function') {
          window.winzen.send('hover_window_selector', payload);
        }
      }, 110);
    });

    btn.addEventListener('mouseleave', () => {
      if (hoverTimer) {
        clearTimeout(hoverTimer);
        hoverTimer = null;
      }
      if (window.winzen && window.winzen.tasks && typeof window.winzen.tasks.leaveWindowSelector === 'function') {
        window.winzen.tasks.leaveWindowSelector();
      } else if (window.winzen && typeof window.winzen.send === 'function') {
        window.winzen.send('leave_window_selector');
      }
    });

    // Left-click:
    // - Closed pinned app -> Launch
    // - Single running window -> Activate if inactive/minimized, Minimize if active
    // - Multiple running windows -> Open Windows 11 Popup Window Selector (thumbnails or list)
    btn.addEventListener('click', (ev) => {
      ev.stopPropagation();
      if (isDraggingItem || Date.now() < suppressClickUntil) {
        return;
      }
      if (hoverTimer) {
        clearTimeout(hoverTimer);
        hoverTimer = null;
      }
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
        const win0 = item.windows[0];
        const isCurrentlyActive = !!(win0.isActive && !win0.isMinimized);
        if (window.winzen && window.winzen.tasks && typeof window.winzen.tasks.activateWindow === 'function') {
          window.winzen.tasks.activateWindow(win0.hwnd, isCurrentlyActive);
        } else {
          window.winzen.send('activate_window', { hwnd: Number(win0.hwnd), wasActive: isCurrentlyActive });
        }
        return;
      }

      const payload = buildSelectorPayload();
      if (window.winzen && window.winzen.tasks && typeof window.winzen.tasks.toggleWindowSelector === 'function') {
        window.winzen.tasks.toggleWindowSelector(payload);
      } else {
        window.winzen.send('toggle_window_selector', payload);
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
}

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

  // Position relative to anchor button on taskbar (support both bottom and top dock)
  const rect = anchorBtn.getBoundingClientRect();
  const isDockTop = window.winzen && window.winzen.state && window.winzen.state.settings &&
    String(window.winzen.state.settings.dockEdge || '').toLowerCase() === 'top';

  if (isDockTop) {
    menu.style.top = `${rect.bottom + 6}px`;
    menu.style.bottom = 'auto';
  } else {
    menu.style.bottom = `${window.innerHeight - rect.top + 6}px`;
    menu.style.top = 'auto';
  }
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
