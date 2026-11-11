const trayRoot = root.getElementById('trayRoot');
const trayChevronBtn = root.getElementById('trayChevronBtn');
const trayInlineIcons = root.getElementById('trayInlineIcons');
const trayCcView = root.getElementById('trayCcView');
const trayCcGrid = root.getElementById('trayCcGrid');
const trayCcEmpty = root.getElementById('trayCcEmpty');

const inControlCenter = typeof widgetConfig !== 'undefined' && widgetConfig && !!widgetConfig.inControlCenter;

if (inControlCenter) {
  if (trayRoot) trayRoot.style.display = 'none';
  if (trayCcView) trayCcView.style.display = 'flex';
} else {
  if (trayCcView) trayCcView.style.display = 'none';
  if (trayRoot) trayRoot.style.display = 'inline-flex';
}

let lastSignature = '';

function buildTrayButton(item, className) {
  const btn = document.createElement('button');
  btn.type = 'button';
  btn.className = className;
  const appTitle = item.tooltip || item.name || 'Tray App';
  btn.title = inControlCenter
    ? `${appTitle}\nClick: Open • Right-click: ${item.isPromoted ? 'Unpin from taskbar' : 'Pin to taskbar'}`
    : appTitle;

  if (item.iconDataUrl) {
    const img = document.createElement('img');
    img.className = 'tray-icon-img';
    img.src = item.iconDataUrl;
    img.alt = item.name || '';
    btn.appendChild(img);
  } else {
    const fb = document.createElement('span');
    fb.className = 'tray-icon-fallback';
    fb.textContent = (item.name || '?').slice(0, 1).toUpperCase();
    btn.appendChild(fb);
  }

  btn.addEventListener('click', (ev) => {
    ev.stopPropagation();
    if (window.winzen && typeof window.winzen.send === 'function') {
      window.winzen.send('activate_tray_icon', { id: item.id, rightClick: false });
    }
  });

  btn.addEventListener('contextmenu', (ev) => {
    ev.preventDefault();
    ev.stopPropagation();
    if (window.winzen && typeof window.winzen.send === 'function') {
      window.winzen.send('activate_tray_icon', { id: item.id, rightClick: true });
    }
  });

  return btn;
}

function renderTrayIcons(trayIcons) {
  if (!Array.isArray(trayIcons)) return;

  if (inControlCenter) {
    if (!trayCcGrid) return;
    const sig = 'cc:' + trayIcons.map((p) => `${p.id}|${p.isPromoted ? 1 : 0}|${p.tooltip}|${p.iconDataUrl ? p.iconDataUrl.length : 0}`).join(';;');
    if (sig === lastSignature) return;
    lastSignature = sig;

    trayCcGrid.innerHTML = '';
    if (trayIcons.length === 0) {
      if (trayCcEmpty) trayCcEmpty.style.display = 'flex';
      trayCcGrid.style.display = 'none';
      return;
    }

    if (trayCcEmpty) trayCcEmpty.style.display = 'none';
    trayCcGrid.style.display = 'grid';
    for (const item of trayIcons) {
      const cls = `tray-cc-tile${item.isPromoted ? ' is-promoted' : ''}`;
      trayCcGrid.appendChild(buildTrayButton(item, cls));
    }
    return;
  }

  if (!trayInlineIcons) return;

  const promoted = trayIcons.filter((item) => item && item.isPromoted);
  const hiddenCount = trayIcons.length - promoted.length;

  if (trayChevronBtn) {
    trayChevronBtn.title = hiddenCount > 0
      ? `Show hidden icons (${hiddenCount})`
      : `System tray (${trayIcons.length} active)`;
  }

  const sig = promoted.map((p) => `${p.id}|${p.tooltip}|${p.iconDataUrl ? p.iconDataUrl.length : 0}`).join(';;') + `::${hiddenCount}`;
  if (sig === lastSignature) return;
  lastSignature = sig;

  trayInlineIcons.innerHTML = '';
  for (const item of promoted) {
    trayInlineIcons.appendChild(buildTrayButton(item, 'tray-btn tray-item-btn'));
  }
}

if (trayChevronBtn) {
  trayChevronBtn.addEventListener('click', (ev) => {
    ev.stopPropagation();
    if (window.winzen && typeof window.winzen.send === 'function') {
      const rect = trayChevronBtn.getBoundingClientRect();
      window.winzen.send('toggle_tray_overflow', {
        x: rect.left,
        y: rect.top,
        width: rect.width,
        height: rect.height
      });
    }
  });
}

function updateDockOrientation(dockEdge) {
  if (!trayRoot) return;
  const isTop = String(dockEdge || '').toLowerCase() === 'top';
  trayRoot.classList.toggle('dock-top', isTop);
}

if (typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.dockEdge) {
  updateDockOrientation(widgetConfig.dockEdge);
}

window.addEventListener('winzen:state', (e) => {
  const edge = e.detail && e.detail.settings && e.detail.settings.dockEdge;
  if (edge) updateDockOrientation(edge);
});

window.addEventListener('winzen:metrics', (e) => {
  if (e.detail && Array.isArray(e.detail.trayIcons)) {
    renderTrayIcons(e.detail.trayIcons);
  }
});
