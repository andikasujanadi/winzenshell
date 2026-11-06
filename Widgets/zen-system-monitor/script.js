const cpuVal = document.getElementById('cpuVal');
const cpuBar = document.getElementById('cpuBar');
const ramVal = document.getElementById('ramVal');
const ramBar = document.getElementById('ramBar');
const sysPill = document.getElementById('sysPill');

window.addEventListener('winzen:metrics', (e) => {
  const m = e.detail;
  if (!m) return;

  const cpu = m.cpuPercent ?? 0;
  const ram = m.memoryPercent ?? 0;

  cpuVal.textContent = `${cpu}%`;
  cpuBar.style.width = `${cpu}%`;

  ramVal.textContent = `${ram}%`;
  ramBar.style.width = `${ram}%`;

  if (m.memoryUsedGb && m.memoryTotalGb) {
    sysPill.title = `CPU: ${cpu}% | RAM: ${m.memoryUsedGb} / ${m.memoryTotalGb} GB (${ram}%)`;
  }
});
