const doc = (typeof root !== 'undefined' && root) ? root : document;
const timeText = doc.getElementById('timeText');
const dateText = doc.getElementById('dateText');
const clockBtn = doc.getElementById('clockBtn');

const inControlCenter = typeof widgetConfig !== 'undefined' && widgetConfig && !!widgetConfig.inControlCenter;

let showSeconds = typeof widgetConfig !== 'undefined' && widgetConfig
  ? widgetConfig.showSeconds !== false
  : true;
let showDate = inControlCenter
  ? true
  : (typeof widgetConfig !== 'undefined' && widgetConfig ? widgetConfig.showDate !== false : true);
let dateFormat = (window.winzen && window.winzen.state && window.winzen.state.settings && window.winzen.state.settings.dateFormat) || 'DD-MM-YYYY';

function formatDateWithTemplate(date, template) {
  const tpl = (template && String(template).trim()) ? String(template) : 'DD-MM-YYYY';
  const d = date instanceof Date ? date : new Date();
  const year = String(d.getFullYear());
  const monthIdx = d.getMonth();
  const dayNum = d.getDate();
  const map = {
    YYYY: year,
    YY: year.slice(-2),
    MMMM: d.toLocaleDateString('en-US', { month: 'long' }),
    MMM: d.toLocaleDateString('en-US', { month: 'short' }),
    MM: String(monthIdx + 1).padStart(2, '0'),
    M: String(monthIdx + 1),
    dddd: d.toLocaleDateString('en-US', { weekday: 'long' }),
    ddd: d.toLocaleDateString('en-US', { weekday: 'short' }),
    DD: String(dayNum).padStart(2, '0'),
    D: String(dayNum)
  };
  return tpl.replace(/YYYY|YY|MMMM|MMM|MM|M|dddd|ddd|DD|D/g, (m) => map[m] ?? m);
}

function applyClockLayout() {
  if (inControlCenter) {
    showDate = true;
  }
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
  if (dateText) {
    dateText.textContent = formatDateWithTemplate(now, dateFormat);
  }
}

applyClockLayout();
tickLocalClock();
setInterval(tickLocalClock, 1000);

window.addEventListener('winzen:state', (e) => {
  const state = e.detail;
  if (!state) return;
  const nextDateFmt = (state.settings && state.settings.dateFormat) || dateFormat;
  const dateFmtChanged = nextDateFmt !== dateFormat;
  dateFormat = nextDateFmt;

  if (inControlCenter) {
    showDate = true;
    applyClockLayout();
    tickLocalClock();
    return;
  }
  let cfg = null;
  if (Array.isArray(state.widgets)) {
    cfg = state.widgets.find((w) => w.id === 'community.zen-clock-pill' || w.id === 'zen-clock-pill');
  }
  if (!cfg) {
    if (dateFmtChanged) tickLocalClock();
    return;
  }

  const nextSec = cfg.showSeconds !== false;
  const nextDate = cfg.showDate !== false;
  if (nextSec !== showSeconds || nextDate !== showDate || dateFmtChanged) {
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
