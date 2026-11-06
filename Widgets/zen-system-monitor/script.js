const cpuVal = root.getElementById('cpuVal');
const cpuBar = root.getElementById('cpuBar');
const ramVal = root.getElementById('ramVal');
const ramBar = root.getElementById('ramBar');
const sysPill = root.getElementById('sysPill');
const btnIcon = root.getElementById('btnSysIcon');
const viewIcon = root.getElementById('viewIcon');
const viewWidget = root.getElementById('viewWidget');

const inControlCenter = typeof widgetConfig !== 'undefined' && widgetConfig && !!widgetConfig.inControlCenter;
const isWidgetMode = typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.displayMode === 'widget';

if (isWidgetMode || inControlCenter) {
  if (viewIcon) viewIcon.style.display = 'none';
  if (viewWidget) viewWidget.style.display = 'flex';
} else {
  if (viewWidget) viewWidget.style.display = 'none';
  if (viewIcon) viewIcon.style.display = 'inline-flex';
}

function toggleTelemetryFlyout(ev) {
  ev.stopPropagation();
  // Don't open separate popup inside Control Center
  if (inControlCenter) return;

  const targetEl = (viewIcon && viewIcon.style.display !== 'none' && btnIcon) ? btnIcon : sysPill;
  if (!targetEl) return;

  const rect = targetEl.getBoundingClientRect();
  if (window.winzen && window.winzen.systemMonitor && typeof window.winzen.systemMonitor.toggle === 'function') {
    window.winzen.systemMonitor.toggle(rect.left, rect.top, rect.width, rect.height);
  } else if (window.winzen && typeof window.winzen.send === 'function') {
    window.winzen.send('toggle_system_monitor_flyout', {
      x: rect.left,
      y: rect.top,
      width: rect.width,
      height: rect.height
    });
  }
}

if (btnIcon) {
  btnIcon.addEventListener('click', toggleTelemetryFlyout);
}

if (sysPill) {
  sysPill.style.cursor = 'pointer';
  sysPill.addEventListener('click', toggleTelemetryFlyout);
}

window.addEventListener('winzen:metrics', (e) => {
  const m = e.detail;
  if (!m) return;

  const cpu = m.cpuPercent ?? 0;
  const ram = m.memoryPercent ?? 0;

  if (cpuVal) cpuVal.textContent = `${cpu}%`;
  if (cpuBar) cpuBar.style.width = `${cpu}%`;

  if (ramVal) ramVal.textContent = `${ram}%`;
  if (ramBar) ramBar.style.width = `${ram}%`;

  const tooltip = `CPU: ${cpu}% | RAM: ${m.memoryUsedGb ?? 0} / ${m.memoryTotalGb ?? 0} GB (${ram}%) - Click for telemetry details`;
  if (sysPill) sysPill.title = tooltip;
  if (btnIcon) btnIcon.title = tooltip;
});
