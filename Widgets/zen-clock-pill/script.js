const doc = (typeof root !== 'undefined' && root) ? root : document;
const timeText = doc.getElementById('timeText');
const dateText = doc.getElementById('dateText');
const clockBtn = doc.getElementById('clockBtn');

let showSeconds = typeof widgetConfig !== 'undefined' && widgetConfig
  ? widgetConfig.showSeconds !== false
  : true;
let showDate = typeof widgetConfig !== 'undefined' && widgetConfig
  ? widgetConfig.showDate !== false
  : true;

function applyClockLayout() {
  if (clockBtn) {
    clockBtn.classList.toggle('no-date', !showDate);
  }
  if (dateText) {
    dateText.style.display = showDate ? '' : 'none';
  }
}

function tickLocalClock() {
  const now = new Date();
  if (timeText) {
    const opts = showSeconds
      ? { hour12: false, hour: '2-digit', minute: '2-digit', second: '2-digit' }
      : { hour12: false, hour: '2-digit', minute: '2-digit' };
    timeText.textContent = now.toLocaleTimeString([], opts);
  }
  if (dateText && showDate) {
    dateText.textContent = now.toLocaleDateString([], { weekday: 'short', month: 'short', day: 'numeric' });
  }
}

applyClockLayout();
tickLocalClock();
setInterval(tickLocalClock, 1000);

window.addEventListener('winzen:state', (e) => {
  const state = e.detail;
  if (!state) return;
  let cfg = null;
  if (Array.isArray(state.widgets)) {
    cfg = state.widgets.find((w) => w.id === 'community.zen-clock-pill' || w.id === 'zen-clock-pill');
  }
  if (!cfg && Array.isArray(state.controlCenterWidgets)) {
    cfg = state.controlCenterWidgets.find((w) => w.id === 'community.zen-clock-pill' || w.id === 'zen-clock-pill');
  }
  if (!cfg) return;

  const nextSec = cfg.showSeconds !== false;
  const nextDate = cfg.showDate !== false;
  if (nextSec !== showSeconds || nextDate !== showDate) {
    showSeconds = nextSec;
    showDate = nextDate;
    applyClockLayout();
    tickLocalClock();
  }
});

if (clockBtn) {
  clockBtn.addEventListener('click', (ev) => {
    ev.stopPropagation();
    const rect = clockBtn.getBoundingClientRect();
    if (window.winzen && typeof window.winzen.send === 'function') {
      window.winzen.send('toggle_clock_flyout', {
        x: rect.left,
        y: rect.top,
        width: rect.width,
        height: rect.height
      });
    }
  });
}
