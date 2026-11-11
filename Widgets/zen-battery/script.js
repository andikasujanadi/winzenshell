const batteryRoot = root.getElementById('batteryRoot');
const iconWrap = root.getElementById('iconWrap');
const pctLeft = root.getElementById('pctLeft');
const pctRight = root.getElementById('pctRight');
const ccBatteryBody = root.getElementById('ccBatteryBody');
const ccIconWrap = root.getElementById('ccIconWrap');
const ccBattPct = root.getElementById('ccBattPct');
const ccBattStatus = root.getElementById('ccBattStatus');
const ccBattBar = root.getElementById('ccBattBar');

const inControlCenter = typeof widgetConfig !== 'undefined' && widgetConfig && !!widgetConfig.inControlCenter;

let currentStyle = (typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.batteryIconStyle) || 'win11';
let currentMode = (typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.batteryPercentMode) || 'outside-right';

let lastMetrics = null;

function buildIconHtml(style) {
  switch (style) {
    case 'win10':
      return `
        <div class="batt-row">
          <svg class="batt-svg batt-win10" viewBox="0 0 28 16" fill="none" xmlns="http://www.w3.org/2000/svg">
            <rect class="batt-housing" x="1.5" y="2.5" width="22" height="11" rx="0" stroke="var(--batt-border)" stroke-width="1.2"/>
            <rect class="batt-cap" x="23.5" y="5.5" width="2" height="5" rx="0" fill="var(--batt-border)"/>
            <rect class="batt-fill-bar" id="fillBar" x="3" y="4" width="0" height="8" rx="0" fill="var(--batt-fill)"/>
            <path class="batt-bolt" d="M12.5 3.5 L9 8 H12 L11 12.5 L15 7.5 H12 Z" fill="currentColor"/>
          </svg>
          <span class="pct-inside" id="pctInside">--</span>
        </div>
      `;

    case 'macos':
      return `
        <div class="batt-row">
          <svg class="batt-svg batt-macos" viewBox="0 0 29 16" fill="none" xmlns="http://www.w3.org/2000/svg">
            <defs>
              <clipPath id="macosPillClip">
                <rect x="1.5" y="2" width="23" height="12" rx="4.5"/>
              </clipPath>
            </defs>
            <rect class="batt-track" x="1.5" y="2" width="23" height="12" rx="4.5" fill="var(--batt-track)"/>
            <path class="batt-cap" d="M25.7 5.7 C27.2 5.7 27.5 6.6 27.5 8 C27.5 9.4 27.2 10.3 25.7 10.3 Z" fill="var(--batt-track)"/>
            <g clip-path="url(#macosPillClip)">
              <rect class="batt-fill-bar" id="fillBar" x="1.5" y="2" width="0" height="12" fill="var(--batt-fill)"/>
            </g>
            <g class="batt-bolt">
              <path d="M12.5 2.5 L8.5 7.5 H11.5 L10.5 13.5 L16.5 6.5 H12.5 Z" stroke="rgba(0,0,0,0.35)" stroke-width="1.2" stroke-linejoin="round" fill="none"/>
              <path d="M12.5 2.5 L8.5 7.5 H11.5 L10.5 13.5 L16.5 6.5 H12.5 Z" fill="#ffffff"/>
            </g>
          </svg>
          <span class="pct-inside" id="pctInside">--</span>
        </div>
      `;

    case 'win11':
    default:
      return `
        <div class="batt-row">
          <svg class="batt-svg batt-win11" viewBox="0 0 28 16" fill="none" xmlns="http://www.w3.org/2000/svg">
            <defs>
              <clipPath id="win11CavityClip">
                <rect x="3.5" y="4.5" width="18" height="7" rx="1.5"/>
              </clipPath>
            </defs>
            <rect class="batt-housing" x="1.5" y="2.5" width="22" height="11" rx="3.5" stroke="var(--batt-border)" stroke-width="1.5"/>
            <rect class="batt-cap" x="23.5" y="6" width="2" height="4" rx="1" fill="var(--batt-border)"/>
            <g clip-path="url(#win11CavityClip)">
              <rect class="batt-fill-bar" id="fillBar" x="3.5" y="4.5" width="0" height="7" fill="var(--batt-fill)"/>
            </g>
            <g class="batt-bolt">
              <path class="bolt-halo" d="M10.5 0.5 L7.5 6.5 H10 L9 11.5 L13.5 5.5 H10.5 Z" stroke="var(--batt-bolt-halo)" stroke-width="1.8" stroke-linejoin="round" stroke-linecap="round"/>
              <path class="bolt-core" d="M10.5 0.5 L7.5 6.5 H10 L9 11.5 L13.5 5.5 H10.5 Z" fill="var(--batt-bolt-fill)"/>
            </g>
          </svg>
          <span class="pct-inside" id="pctInside">--</span>
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
  if (ccIconWrap) {
    ccIconWrap.innerHTML = buildIconHtml(style);
    ccIconWrap.className = 'cc-batt-icon-slot battery-icon-wrap';
  }

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

  const fillBars = [root.getElementById('fillBar'), ...(ccIconWrap ? Array.from(ccIconWrap.querySelectorAll('.batt-fill-bar')) : [])].filter(Boolean);
  const pctInsideEls = Array.from(root.querySelectorAll('#pctInside'));

  if (pctLeft) pctLeft.textContent = `${pct}%`;
  if (pctRight) pctRight.textContent = `${pct}%`;
  if (ccBattPct) ccBattPct.textContent = `${pct}%`;
  if (ccBattStatus) {
    if (!hasBattery) ccBattStatus.textContent = 'AC Power';
    else if (isCharging) ccBattStatus.textContent = pct >= 100 ? 'Fully Charged' : 'Plugged in • Charging';
    else if (isLow) ccBattStatus.textContent = 'Critical Battery';
    else if (isEnergySaver) ccBattStatus.textContent = 'Battery Saver';
    else ccBattStatus.textContent = 'On Battery Power';
  }
  if (ccBattBar) ccBattBar.style.width = `${pct}%`;

  pctInsideEls.forEach((pctInside) => {
    if (isCharging) {
      pctInside.innerHTML = `<span style="font-size:7px;margin-right:1px;line-height:1;">⚡</span>${pct}`;
      pctInside.style.flexDirection = 'row';
    } else {
      pctInside.textContent = `${pct}`;
      pctInside.style.flexDirection = 'row';
    }
  });

  const maxW = currentStyle === 'macos' ? 23 : (currentStyle === 'win10' ? 19 : 18);
  const fillW = Math.max(0, Math.min(maxW, (maxW * pct) / 100));
  fillBars.forEach((fb) => fb.setAttribute('width', fillW.toFixed(2)));

  if (batteryRoot) {
    batteryRoot.classList.toggle('is-charging', isCharging);
    batteryRoot.classList.toggle('is-low', isLow);
    batteryRoot.classList.toggle('is-energy-saver', isEnergySaver);
    batteryRoot.title = `Battery: ${pct}% • ${statusText} (Click for Power & Battery)`;
  }
}

applyStyleAndMode(currentStyle, currentMode);

if (batteryRoot) {
  batteryRoot.addEventListener('click', (ev) => {
    ev.stopPropagation();
    if (window.winzen && typeof window.winzen.send === 'function') {
      const rect = batteryRoot.getBoundingClientRect();
      window.winzen.send('toggle_battery_flyout', {
        x: rect.left,
        y: rect.top,
        width: rect.width,
        height: rect.height
      });
    }
  });
}

window.addEventListener('winzen:metrics', (e) => {
  updateBatteryDisplay(e.detail);
});

if (typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.inControlCenter) {
  if (batteryRoot) batteryRoot.classList.add('in-control-center');
}

window.addEventListener('winzen:state', (e) => {
  const state = e.detail;
  if (!state) return;
  let cfg = null;
  if (Array.isArray(state.widgets)) {
    cfg = state.widgets.find((w) => w.id === 'community.zen-battery' || w.id === 'zen-battery');
  }
  if (!cfg && Array.isArray(state.controlCenterWidgets)) {
    cfg = state.controlCenterWidgets.find((w) => w.id === 'community.zen-battery' || w.id === 'zen-battery');
  }
  if (!cfg) return;

  const newStyle = cfg.batteryIconStyle || 'win11';
  const newMode = cfg.batteryPercentMode || 'outside-right';

  if (newStyle !== currentStyle || newMode !== currentMode) {
    currentStyle = newStyle;
    currentMode = newMode;
    applyStyleAndMode(currentStyle, currentMode);
  }
});
