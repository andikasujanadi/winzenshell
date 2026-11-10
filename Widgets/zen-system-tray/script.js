const trayRoot = document.getElementById('trayRoot');
const trayChevronBtn = document.getElementById('trayChevronBtn');
const trayInlineIcons = document.getElementById('trayInlineIcons');

let lastSignature = '';

function renderTrayIcons(trayIcons) {
  if (!trayInlineIcons || !Array.isArray(trayIcons)) return;

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
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'tray-btn tray-item-btn';
    btn.title = item.tooltip || item.name || 'Tray App';

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

    trayInlineIcons.appendChild(btn);
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
