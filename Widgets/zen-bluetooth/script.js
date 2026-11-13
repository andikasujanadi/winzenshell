const btnBluetooth = root.getElementById('btnBluetooth');
const iconWrap = root.getElementById('iconWrap');
const textCol = root.getElementById('textCol');
const btSvg = root.getElementById('btSvg');
const btPath = root.getElementById('btPath');
const btLabel = root.getElementById('btLabel');
const btSubLabel = root.getElementById('btSubLabel');

const inControlCenter = typeof widgetConfig !== 'undefined' && widgetConfig && !!widgetConfig.inControlCenter;
let currentDisplayMode = (typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.displayMode) || 'icon';

function applyDisplayMode(mode) {
  currentDisplayMode = mode;
  if (btLabel && !inControlCenter) {
    btLabel.style.display = mode === 'widget' ? 'inline-block' : 'none';
  }
}

applyDisplayMode(currentDisplayMode);

function updateBluetoothVisuals(m) {
  if (!btnBluetooth) return;

  const isEnabled = !!(m && m.bluetoothEnabled);
  const isConnected = !!(m && m.bluetoothConnected);
  const deviceName = (m && m.bluetoothDeviceName) || '';
  const statusText = (m && m.bluetoothStatusText) || (isEnabled ? 'On' : 'Off');

  btnBluetooth.classList.remove('state-connected', 'state-on', 'state-off');

  if (isConnected) {
    btnBluetooth.classList.add('state-connected');
    if (btLabel) btLabel.textContent = inControlCenter ? (deviceName || 'Bluetooth') : (deviceName || 'Bluetooth');
    if (btSubLabel) btSubLabel.textContent = 'Connected';
    btnBluetooth.setAttribute('title', deviceName ? `Bluetooth: Connected to ${deviceName}` : 'Bluetooth: Connected');
  } else if (isEnabled) {
    btnBluetooth.classList.add('state-on');
    if (btLabel) btLabel.textContent = 'Bluetooth';
    if (btSubLabel) btSubLabel.textContent = 'Not connected';
    btnBluetooth.setAttribute('title', 'Bluetooth: On (Not connected)');
  } else {
    btnBluetooth.classList.add('state-off');
    if (btLabel) btLabel.textContent = 'Bluetooth';
    if (btSubLabel) btSubLabel.textContent = 'Off';
    btnBluetooth.setAttribute('title', 'Bluetooth: Off');
  }
}

if (iconWrap && inControlCenter) {
  iconWrap.addEventListener('click', (ev) => {
    ev.stopPropagation();
    if (window.winzen && typeof window.winzen.send === 'function') {
      window.winzen.send('toggle_bluetooth', {});
    }
  });
}

if (textCol && inControlCenter) {
  textCol.addEventListener('click', (ev) => {
    ev.stopPropagation();
    if (window.winzen && typeof window.winzen.send === 'function') {
      window.winzen.send('open_bluetooth_settings', {});
    }
  });
}

if (btnBluetooth) {
  btnBluetooth.addEventListener('click', (ev) => {
    ev.stopPropagation();
    if (window.winzen && typeof window.winzen.send === 'function') {
      if (inControlCenter) {
        window.winzen.send('toggle_bluetooth', {});
      } else {
        window.winzen.send('open_bluetooth_settings', {});
      }
    }
  });

  btnBluetooth.addEventListener('contextmenu', (ev) => {
    ev.preventDefault();
    ev.stopPropagation();
    if (window.winzen && typeof window.winzen.send === 'function') {
      window.winzen.send('open_bluetooth_settings', {});
    }
  });
}

window.addEventListener('winzen:metrics', (e) => {
  updateBluetoothVisuals(e.detail);
});

window.addEventListener('winzen:state', (e) => {
  const state = e.detail;
  if (!state || !Array.isArray(state.widgets)) return;
  const cfg = state.widgets.find((w) => w.id === 'community.zen-bluetooth');
  if (!cfg) return;
  const newMode = cfg.displayMode || 'icon';
  if (newMode !== currentDisplayMode) {
    applyDisplayMode(newMode);
  }
});
