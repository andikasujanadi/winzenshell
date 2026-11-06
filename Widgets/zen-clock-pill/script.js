const timeText = document.getElementById('timeText');
const dateText = document.getElementById('dateText');

function tickLocalClock() {
  const now = new Date();
  timeText.textContent = now.toLocaleTimeString([], { hour12: false });
  dateText.textContent = now.toLocaleDateString([], { weekday: 'short', month: 'short', day: 'numeric' });
}

tickLocalClock();
setInterval(tickLocalClock, 1000);

document.getElementById('clockBtn').addEventListener('click', () => {
  window.winzen.shell.openSettings();
});
