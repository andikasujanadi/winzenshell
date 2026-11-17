const doc = (typeof root !== 'undefined' && root) ? root : document;
const btn = doc.getElementById('btnPowerMenu');

if (btn) {
  btn.addEventListener('click', (ev) => {
    ev.stopPropagation();
    const rect = btn.getBoundingClientRect();
    if (window.winzen && typeof window.winzen.send === 'function') {
      window.winzen.send('toggle_power_flyout', {
        x: rect.left,
        y: rect.top,
        width: rect.width,
        height: rect.height
      });
    }
  });
}
