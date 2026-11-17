const doc = (typeof root !== 'undefined' && root) ? root : document;
const btn = doc.getElementById('btnLockScreen');

if (btn) {
  btn.addEventListener('click', (ev) => {
    ev.stopPropagation();
    if (window.winzen && typeof window.winzen.send === 'function') {
      window.winzen.send('lock_workstation');
    }
  });
}
