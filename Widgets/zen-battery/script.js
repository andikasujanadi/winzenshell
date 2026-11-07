const batteryRoot = root.getElementById('batteryRoot');
const iconWrap = root.getElementById('iconWrap');
const pctLeft = root.getElementById('pctLeft');
const pctRight = root.getElementById('pctRight');

let currentStyle = (typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.batteryIconStyle) || 'win11';
let currentMode = (typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.batteryPercentMode) || 'outside-right';

let lastMetrics = null;

const BOLT_SVG = `<svg class="batt-bolt" viewBox="0 0 12 16" fill="none" xmlns="http://www.w3.org/2000/svg">
  <path class="bolt-halo" d="M7 1L1 8.5H5.5L4 14.5L11 7H6.5L7 1Z" stroke="rgba(0,0,0,0.7)" stroke-width="2" stroke-linejoin="round" stroke-linecap="round"/>
  <path class="bolt-core" d="M7 1L1 8.5H5.5L4 14.5L11 7H6.5L7 1Z" fill="#ffffff"/>
</svg>`;

function buildIconHtml(style) {
  switch (style) {
    case 'win10':
      return `
        <div class="batt-row">
          <div class="batt-housing">
            <div class="batt-fill-bar" id="fillBar"></div>
            ${BOLT_SVG}
            <span class="pct-inside" id="pctInside">--</span>
          </div>
          <div class="batt-cap"></div>
        </div>
      `;

    case 'macos':
      return `
        <div class="batt-row">
          <div class="batt-housing">
            <div class="batt-fill-bar" id="fillBar"></div>
            <svg class="batt-bolt batt-bolt-macos" viewBox="0 0 9 12" fill="none">
              <path d="M5.5 0.5L0.5 6.5H4L3 11.5L8 5.5H4.5L5.5 0.5Z" fill="#ffffff"/>
            </svg>
            <span class="pct-inside" id="pctInside">--</span>
          </div>
          <div class="batt-cap"></div>
        </div>
      `;

    case 'win11':
    default:
      return `
        <div class="batt-row">
          <div class="batt-housing">
            <div class="batt-fill-bar" id="fillBar"></div>
            <span class="pct-inside" id="pctInside">--</span>
          </div>
          <div class="batt-cap"></div>
          ${BOLT_SVG}
        </div>
      `;
  }
}

function applyStyleAndMode(style, mode) {
  if (!batteryRoot || !iconWrap) return;

  // Clear previous classes
  batteryRoot.classList.remove(
    'style-win11', 'style-win10', 'style-macos',
    'mode-outside-right', 'mode-outside-left', 'mode-inside', 'mode-hide'
  );

  batteryRoot.classList.add(`style-${style}`);
  batteryRoot.classList.add(`mode-${mode}`);

  iconWrap.innerHTML = buildIconHtml(style);

  if (lastMetrics) {
    updateBatteryDisplay(lastMetrics);
  }
}

function updateBatteryDisplay(m) {
  if (!m) return;
  lastMetrics = m;

  const pct = typeof m.batteryPercent === 'number' ? Math.max(0, Math.min(100, m.batteryPercent)) : 100;
  const isCharging = !!m.isCharging;
  const hasBattery = m.hasBattery !== false;
  const isLow = pct <= 5 && !isCharging;
  const isEnergySaver = pct > 5 && pct <= 20 && !isCharging;
  const statusText = m.batteryStatusText || (isCharging ? 'Charging' : (isEnergySaver ? 'Energy Saver' : 'On Battery'));

  const fillBar = root.getElementById('fillBar');
  const pctInside = root.getElementById('pctInside');

  if (pctLeft) pctLeft.textContent = `${pct}%`;
  if (pctRight) pctRight.textContent = `${pct}%`;
  if (pctInside) {
    if (isCharging) {
      pctInside.innerHTML = `<span style="font-size:7px;margin-right:1px;line-height:1;">⚡</span>${pct}`;
      pctInside.style.flexDirection = 'row';
    } else {
      pctInside.textContent = `${pct}`;
      pctInside.style.flexDirection = 'row';
    }
  }

  if (fillBar) {
    fillBar.style.width = `${pct}%`;
    fillBar.style.height = '100%';
  }

  if (batteryRoot) {
    batteryRoot.classList.toggle('is-charging', isCharging);
    batteryRoot.classList.toggle('is-low', isLow);
    batteryRoot.classList.toggle('is-energy-saver', isEnergySaver);
    batteryRoot.title = `Battery: ${pct}% • ${statusText} (Click to open Battery Settings)`;
  }
}

applyStyleAndMode(currentStyle, currentMode);

if (batteryRoot) {
  batteryRoot.addEventListener('click', (ev) => {
    ev.stopPropagation();
    if (window.winzen && typeof window.winzen.send === 'function') {
      window.winzen.send('open_uri', { uri: 'ms-settings:powersleep' });
    }
  });
}

window.addEventListener('winzen:metrics', (e) => {
  updateBatteryDisplay(e.detail);
});

window.addEventListener('winzen:state', (e) => {
  const state = e.detail;
  if (!state || !Array.isArray(state.widgets)) return;
  const cfg = state.widgets.find((w) => w.id === 'community.zen-battery');
  if (!cfg) return;

  const newStyle = cfg.batteryIconStyle || 'win11';
  const newMode = cfg.batteryPercentMode || 'outside-right';

  if (newStyle !== currentStyle || newMode !== currentMode) {
    currentStyle = newStyle;
    currentMode = newMode;
    applyStyleAndMode(currentStyle, currentMode);
  }
});
