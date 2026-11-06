const widgetRoot = typeof root !== 'undefined' ? root : document;
const widgetView = widgetRoot.getElementById('vdWidgetView');
const iconView = widgetRoot.getElementById('vdIconView');
const pillsContainer = widgetRoot.getElementById('vdPillsContainer');
const btnTaskView = widgetRoot.getElementById('btnTaskView');
const btnAddDesktop = widgetRoot.getElementById('btnAddDesktop');
const btnIconMode = widgetRoot.getElementById('btnIconMode');
const iconBadge = widgetRoot.getElementById('iconBadge');
const iconLabel = widgetRoot.getElementById('iconLabel');

const isWidgetMode = typeof widgetConfig === 'undefined' || !widgetConfig || widgetConfig.displayMode !== 'icon';

if (isWidgetMode) {
  if (widgetView) widgetView.style.display = 'flex';
  if (iconView) iconView.style.display = 'none';
} else {
  if (widgetView) widgetView.style.display = 'none';
  if (iconView) iconView.style.display = 'inline-flex';
}

function renderDesktops(desktops, currentIndex) {
  if (!pillsContainer) return;
  pillsContainer.innerHTML = '';

  if (!desktops || desktops.length === 0) {
    desktops = [{ index: 0, name: 'Desktop 1', isCurrent: true }];
  }

  desktops.forEach((d) => {
    const pill = document.createElement('button');
    pill.className = `vd-pill ${d.isCurrent ? 'active' : ''}`;
    pill.title = `${d.name} (Click to switch)`;
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
      if (window.winzen && window.winzen.desktops) {
        window.winzen.desktops.switch(d.index);
      } else if (window.winzen) {
        window.winzen.send('switch_desktop', { index: d.index });
      }
    });

    pillsContainer.appendChild(pill);
  });

  const cur = desktops.find((d) => d.isCurrent) || desktops[0];
  if (iconBadge) iconBadge.textContent = String((cur ? cur.index : currentIndex) + 1);
  if (iconLabel) iconLabel.textContent = cur ? cur.name : 'Desktop 1';
  if (btnIconMode) {
    btnIconMode.title = `Virtual Desktop: ${cur ? cur.name : 'Desktop 1'} (Scroll to switch, click for Task View)`;
  }
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

// Mouse wheel capture: scroll wheel over widget switches desktops smoothly!
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
