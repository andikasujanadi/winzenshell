const widgetRoot = typeof root !== 'undefined' ? root : document;
const widgetView = widgetRoot.getElementById('vdWidgetView');
const compactView = widgetRoot.getElementById('vdCompactView');
const iconView = widgetRoot.getElementById('vdIconView');
const pillsContainer = widgetRoot.getElementById('vdPillsContainer');
const compactNumbers = widgetRoot.getElementById('vdCompactNumbers');
const btnTaskView = widgetRoot.getElementById('btnTaskView');
const btnAddDesktop = widgetRoot.getElementById('btnAddDesktop');
const btnIconMode = widgetRoot.getElementById('btnIconMode');
const iconBadge = widgetRoot.getElementById('iconBadge');
const iconLabel = widgetRoot.getElementById('iconLabel');

const currentMode = (typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.displayMode)
  ? widgetConfig.displayMode
  : 'widget';

function applyDisplayMode(mode) {
  if (mode === 'compact') {
    if (widgetView) widgetView.style.display = 'none';
    if (compactView) compactView.style.display = 'inline-flex';
    if (iconView) iconView.style.display = 'none';
  } else if (mode === 'icon') {
    if (widgetView) widgetView.style.display = 'none';
    if (compactView) compactView.style.display = 'none';
    if (iconView) iconView.style.display = 'inline-flex';
  } else {
    // 'widget' (large full mode)
    if (widgetView) widgetView.style.display = 'flex';
    if (compactView) compactView.style.display = 'none';
    if (iconView) iconView.style.display = 'none';
  }
}

applyDisplayMode(currentMode);

function switchDesktop(idx) {
  if (window.winzen && window.winzen.desktops) {
    window.winzen.desktops.switch(idx);
  } else if (window.winzen) {
    window.winzen.send('switch_desktop', { index: idx });
  }
}

function renderDesktops(desktops, currentIndex) {
  if (!desktops || desktops.length === 0) {
    desktops = [{ index: 0, name: 'Desktop 1', isCurrent: true }];
  }

  // 1. Render Large Mode (pills with numbers + names)
  if (pillsContainer) {
    pillsContainer.innerHTML = '';
    desktops.forEach((d) => {
      const pill = document.createElement('button');
      pill.className = `vd-pill ${d.isCurrent ? 'active' : ''}`;
      pill.setAttribute('aria-label', d.name);

      const numSpan = document.createElement('span');
      numSpan.className = 'vd-pill-num';
      numSpan.textContent = String(d.index + 1);

      const nameSpan = document.createElement('span');
      nameSpan.className = 'vd-pill-name';
      nameSpan.textContent = d.name || `Desktop ${d.index + 1}`;

      pill.appendChild(numSpan);
      pill.appendChild(nameSpan);

      pill.addEventListener('click', (ev) => {
        ev.stopPropagation();
        switchDesktop(d.index);
      });

      pillsContainer.appendChild(pill);
    });
  }

  // 2. Render Compact Mode (clean numbers 1  2  3)
  if (compactNumbers) {
    compactNumbers.innerHTML = '';
    desktops.forEach((d) => {
      const numBtn = document.createElement('button');
      numBtn.className = `vd-compact-num ${d.isCurrent ? 'active' : ''}`;
      numBtn.textContent = String(d.index + 1);
      numBtn.setAttribute('aria-label', d.name || `Desktop ${d.index + 1}`);

      numBtn.addEventListener('click', (ev) => {
        ev.stopPropagation();
        switchDesktop(d.index);
      });

      compactNumbers.appendChild(numBtn);
    });
  }

  // 3. Update Icon Mode (badge and label)
  const cur = desktops.find((d) => d.isCurrent) || desktops[0];
  const idx = cur ? cur.index : currentIndex;
  if (iconBadge) iconBadge.textContent = String(idx + 1);
  if (iconLabel) iconLabel.textContent = cur ? cur.name : `Desktop ${idx + 1}`;
}

if (btnTaskView) {
  btnTaskView.addEventListener('click', (ev) => {
    ev.stopPropagation();
    if (window.winzen && window.winzen.desktops) {
      window.winzen.desktops.openTaskView();
    } else if (window.winzen) {
      window.winzen.send('open_task_view', {});
    }
  });
}

if (btnAddDesktop) {
  btnAddDesktop.addEventListener('click', (ev) => {
    ev.stopPropagation();
    if (window.winzen && window.winzen.desktops) {
      window.winzen.desktops.create();
    } else if (window.winzen) {
      window.winzen.send('create_desktop', {});
    }
  });
}

if (btnIconMode) {
  btnIconMode.addEventListener('click', (ev) => {
    ev.stopPropagation();
    if (window.winzen && window.winzen.desktops) {
      window.winzen.desktops.openTaskView();
    } else if (window.winzen) {
      window.winzen.send('open_task_view', {});
    }
  });
}

// Mouse wheel capture: scroll wheel over any view cycles desktops smoothly
function onDesktopWheel(ev) {
  ev.preventDefault();
  ev.stopPropagation();
  if (ev.deltaY < 0) {
    if (window.winzen && window.winzen.desktops) {
      window.winzen.desktops.previous();
    } else if (window.winzen) {
      window.winzen.send('previous_desktop', {});
    }
  } else {
    if (window.winzen && window.winzen.desktops) {
      window.winzen.desktops.next();
    } else if (window.winzen) {
      window.winzen.send('next_desktop', {});
    }
  }
}

widgetRoot.addEventListener('wheel', onDesktopWheel, { passive: false });

window.addEventListener('winzen:metrics', (e) => {
  const m = e.detail;
  if (!m) return;
  if (Array.isArray(m.virtualDesktops)) {
    renderDesktops(m.virtualDesktops, m.currentVirtualDesktopIndex || 0);
  }
});
