const startBtn = root.getElementById('startBtn');
const iconSlot = root.getElementById('startIconSlot');
const textSlot = root.getElementById('startTextSlot');

const ICONS = {
  chromeos: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="8.5"/><circle cx="12" cy="12" r="3.6" fill="currentColor"/></svg>`,
  windows: `<span class="win-grid"><i></i><i></i><i></i><i></i></span>`,
  win10: `<svg viewBox="0 0 16 16" fill="currentColor"><path d="M0 2.22 6.47 1.34v6.29H0V2.22zm0 6.07h6.47v6.37L0 13.78V8.29zm7.25-7.07L16 0v7.63H7.25V1.22zm8.75 7.07v7.71l-8.75-1.2v-6.51H16z"/></svg>`,
  apple: `<svg viewBox="0 0 24 24" fill="currentColor"><path d="M18.71 19.5c-.83 1.24-1.71 2.45-3.05 2.47-1.34.03-1.77-.79-3.29-.79-1.53 0-2 .77-3.27.82-1.31.05-2.3-1.32-3.14-2.53C4.25 17 2.94 12.45 4.7 9.39c.87-1.52 2.43-2.48 4.12-2.51 1.28-.02 2.5.87 3.29.87.78 0 2.26-1.07 3.81-.91.65.03 2.47.26 3.64 1.98-.09.06-2.17 1.28-2.15 3.81.03 3.02 2.65 4.03 2.68 4.04-.03.07-.42 1.44-1.38 2.83M15.97 4.54c.64-.78 1.08-1.86.96-2.95-.93.04-2.06.62-2.73 1.4-.58.68-1.1 1.77-.96 2.83 1.04.08 2.09-.5 2.73-1.28z"/></svg>`
};

function renderStartButton(iconKey, colorMode, customText, showText) {
  if (iconSlot) {
    iconSlot.innerHTML = ICONS[iconKey] || ICONS.chromeos;
  }

  if (textSlot) {
    textSlot.textContent = customText;
    textSlot.style.display = showText ? 'inline' : 'none';
  }

  if (startBtn) {
    startBtn.classList.toggle('icon-only', !showText);
    startBtn.classList.toggle('color-accent', colorMode === 'accent');
    startBtn.classList.toggle('color-text', colorMode !== 'accent');
  }
}

let iconKey = (typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.startIcon) || 'chromeos';
let colorMode = (typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.startIconColorMode) || 'text';
let customText = (typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.startText) || 'Launcher';
let showText = typeof widgetConfig !== 'undefined' && widgetConfig ? !!widgetConfig.showStartText : false;

renderStartButton(iconKey, colorMode, customText, showText);

if (startBtn) {
  startBtn.title = 'App Launcher';

  startBtn.addEventListener('click', (ev) => {
    ev.stopPropagation();
    const rect = startBtn.getBoundingClientRect();
    const currentRegion = (typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.slot) || 'left';
    if (window.winzen && typeof window.winzen.send === 'function') {
      window.winzen.send('toggle_app_launcher', {
        x: rect.left,
        y: rect.top,
        width: rect.width,
        height: rect.height,
        region: currentRegion
      });
    } else if (window.winzen && window.winzen.shell && typeof window.winzen.shell.openStartMenu === 'function') {
      window.winzen.shell.openStartMenu();
    }
  });
}

window.addEventListener('winzen:state', (e) => {
  const state = e.detail;
  if (!state || !Array.isArray(state.widgets)) return;
  const cfg = state.widgets.find((w) => w.id === 'community.zen-start-button' || w.id === 'zen-start-button');
  if (!cfg) return;

  const newIconKey = cfg.startIcon || 'chromeos';
  const newColorMode = cfg.startIconColorMode || 'text';
  const newCustomText = cfg.startText || 'Launcher';
  const newShowText = !!cfg.showStartText;

  if (newIconKey !== iconKey || newColorMode !== colorMode || newCustomText !== customText || newShowText !== showText) {
    iconKey = newIconKey;
    colorMode = newColorMode;
    customText = newCustomText;
    showText = newShowText;
    renderStartButton(iconKey, colorMode, customText, showText);
  }
});
