const btn = root.getElementById('btnControlCenter');

if (btn) {
  btn.addEventListener('click', (ev) => {
    ev.stopPropagation();
    const rect = btn.getBoundingClientRect();
    if (window.winzen && window.winzen.controlCenter && typeof window.winzen.controlCenter.toggle === 'function') {
      window.winzen.controlCenter.toggle(rect.left, rect.top, rect.width, rect.height);
    } else if (window.winzen) {
      window.winzen.send('toggle_control_center', {
        x: rect.left,
        y: rect.top,
        width: rect.width,
        height: rect.height
      });
    }
  });
}
