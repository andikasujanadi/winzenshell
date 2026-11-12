const colCpu = root.getElementById('colCpu');
const colRam = root.getElementById('colRam');
const colGpu = root.getElementById('colGpu');
const colNone = root.getElementById('colNone');
const cpuVal = root.getElementById('cpuVal');
const cpuBar = root.getElementById('cpuBar');
const ramVal = root.getElementById('ramVal');
const ramBar = root.getElementById('ramBar');
const gpuVal = root.getElementById('gpuVal');
const gpuBar = root.getElementById('gpuBar');
const sysPill = root.getElementById('sysPill');
const btnIcon = root.getElementById('btnSysIcon');
const viewIcon = root.getElementById('viewIcon');
const viewWidget = root.getElementById('viewWidget');

const inControlCenter = typeof widgetConfig !== 'undefined' && widgetConfig && !!widgetConfig.inControlCenter;
const isWidgetMode = typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.displayMode === 'widget';

let showCpu = typeof widgetConfig !== 'undefined' && widgetConfig ? widgetConfig.showCpu !== false : true;
let showRam = typeof widgetConfig !== 'undefined' && widgetConfig ? widgetConfig.showRam !== false : true;
let showGpu = typeof widgetConfig !== 'undefined' && widgetConfig ? widgetConfig.showGpu !== false : true;

if (isWidgetMode || inControlCenter) {
  if (viewIcon) viewIcon.style.display = 'none';
  if (viewWidget) viewWidget.style.display = 'flex';
} else {
  if (viewWidget) viewWidget.style.display = 'none';
  if (viewIcon) viewIcon.style.display = 'inline-flex';
}

function applyStatVisibility() {
  if (colCpu) colCpu.style.display = showCpu ? '' : 'none';
  if (colRam) colRam.style.display = showRam ? '' : 'none';
  if (colGpu) colGpu.style.display = showGpu ? '' : 'none';
  if (colNone) colNone.style.display = (!showCpu && !showRam && !showGpu) ? 'block' : 'none';
}

applyStatVisibility();

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

window.addEventListener('winzen:state', (e) => {
  const state = e.detail;
  if (!state) return;
  const list = inControlCenter && Array.isArray(state.controlCenterWidgets)
    ? state.controlCenterWidgets
    : (Array.isArray(state.widgets) ? state.widgets : []);
  const cfg = list.find((w) => w.id === 'community.zen-system-monitor');
  if (!cfg) return;

  showCpu = cfg.showCpu !== false;
  showRam = cfg.showRam !== false;
  showGpu = cfg.showGpu !== false;
  applyStatVisibility();
});

window.addEventListener('winzen:metrics', (e) => {
  const m = e.detail;
  if (!m) return;

  const cpu = m.cpuPercent ?? 0;
  const ram = m.memoryPercent ?? 0;
  const gpu = m.gpuPercent ?? 0;

  if (cpuVal) cpuVal.textContent = `${cpu}%`;
  if (cpuBar) cpuBar.style.width = `${cpu}%`;

  if (ramVal) ramVal.textContent = `${ram}%`;
  if (ramBar) ramBar.style.width = `${ram}%`;

  if (gpuVal) gpuVal.textContent = `${gpu}%`;
  if (gpuBar) gpuBar.style.width = `${gpu}%`;
});
