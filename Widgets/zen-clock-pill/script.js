const doc = (typeof root !== 'undefined' && root) ? root : document;
const timeText = doc.getElementById('timeText');
const dateText = doc.getElementById('dateText');
const clockBtn = doc.getElementById('clockBtn');

function tickLocalClock() {
  const now = new Date();
  if (timeText) timeText.textContent = now.toLocaleTimeString([], { hour12: false });
  if (dateText) dateText.textContent = now.toLocaleDateString([], { weekday: 'short', month: 'short', day: 'numeric' });
}

tickLocalClock();
setInterval(tickLocalClock, 1000);

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
