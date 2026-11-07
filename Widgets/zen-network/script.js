const btnNetwork = root.getElementById('btnNetwork');
const netSvg = root.getElementById('netSvg');
const netPath = root.getElementById('netPath');
const netLabel = root.getElementById('netLabel');

let currentDisplayMode = (typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.displayMode) || 'icon';

const PATH_WIFI_FULL = 'M12 3a9 9 0 0 1 6.36 2.64.75.75 0 0 1-1.06 1.06A7.5 7.5 0 0 0 12 4.5a7.5 7.5 0 0 0-5.3 2.2.75.75 0 0 1-1.06-1.06A9 9 0 0 1 12 3zm0 4a5 5 0 0 1 3.54 1.46.75.75 0 1 1-1.06 1.06 3.5 3.5 0 0 0-4.95 0 .75.75 0 0 1-1.06-1.06A5 5 0 0 1 12 7zm0 4a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3z';
const PATH_WIFI_MED = 'M12 7a5 5 0 0 1 3.54 1.46.75.75 0 1 1-1.06 1.06 3.5 3.5 0 0 0-4.95 0 .75.75 0 0 1-1.06-1.06A5 5 0 0 1 12 7zm0 4a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3z';
const PATH_WIFI_LOW = 'M12 8.5a3 3 0 0 1 2.12.88.75.75 0 1 1-1.06 1.06 1.5 1.5 0 0 0-2.12 0 .75.75 0 0 1-1.06-1.06A3 3 0 0 1 12 8.5zm0 2.5a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3z';
const PATH_WIFI_MIN = 'M12 11a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3z';
const PATH_ETHERNET = 'M3 4a1 1 0 0 1 1-1h16a1 1 0 0 1 1 1v11a1 1 0 0 1-1 1h-6v2h2a.75.75 0 0 1 0 1.5H8a.75.75 0 0 1 0-1.5h2v-2H4a1 1 0 0 1-1-1V4zm2 1v9h14V5H5z';
const PATH_OFFLINE = 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.42 0-8-3.58-8-8 0-1.85.63-3.55 1.69-4.9L16.9 18.31C15.55 19.37 13.85 20 12 20zm6.31-3.1L7.1 5.69C8.45 4.63 10.15 4 12 4c4.42 0 8 3.58 8 8 0 1.85-.63 3.55-1.69 4.9z';

function getWifiSignalPath(signal) {
  if (signal >= 70) return PATH_WIFI_FULL;
  if (signal >= 40) return PATH_WIFI_MED;
  if (signal >= 15) return PATH_WIFI_LOW;
  return PATH_WIFI_MIN;
}

function applyDisplayMode(mode) {
  currentDisplayMode = mode;
  if (netLabel) {
    netLabel.style.display = mode === 'widget' ? 'inline-block' : 'none';
  }
}

applyDisplayMode(currentDisplayMode);

function updateNetworkVisuals(m) {
  if (!btnNetwork || !netPath) return;

  const type = (m && m.networkType) || 'offline';
  const signal = typeof (m && m.wifiSignalPercent) === 'number' ? m.wifiSignalPercent : 0;
  const ssid = (m && m.networkName) || '';
  const ip = (m && m.localIpAddress) || '';
  const isConnected = !!(m && m.isNetworkConnected);

  btnNetwork.classList.remove('state-wifi', 'state-ethernet', 'state-offline');
  btnNetwork.classList.add(`state-${type}`);

  if (type === 'wifi') {
    netPath.setAttribute('d', getWifiSignalPath(signal));
    btnNetwork.title = `Wi-Fi: ${ssid || 'Connected'} (${signal}%) • ${ip || 'Active'} (Click to view networks)`;
    if (netLabel) {
      netLabel.textContent = ssid || 'Wi-Fi';
    }
  } else if (type === 'ethernet') {
    netPath.setAttribute('d', PATH_ETHERNET);
    btnNetwork.title = `Ethernet: Connected • ${ip || 'Active'} (Click to view networks)`;
    if (netLabel) {
      netLabel.textContent = 'LAN';
    }
  } else {
    netPath.setAttribute('d', PATH_OFFLINE);
    btnNetwork.title = 'Network: Disconnected (Click to view available networks)';
    if (netLabel) {
      netLabel.textContent = 'Offline';
    }
  }
}

if (btnNetwork) {
  btnNetwork.addEventListener('click', (ev) => {
    ev.stopPropagation();
    const rect = btnNetwork.getBoundingClientRect();
    if (window.winzen && typeof window.winzen.send === 'function') {
      window.winzen.send('toggle_network_flyout', {
        x: rect.left,
        y: rect.top,
        width: rect.width,
        height: rect.height
      });
    }
  });
}

window.addEventListener('winzen:metrics', (e) => {
  updateNetworkVisuals(e.detail);
});

window.addEventListener('winzen:state', (e) => {
  const state = e.detail;
  if (!state || !Array.isArray(state.widgets)) return;
  const cfg = state.widgets.find((w) => w.id === 'community.zen-network');
  if (!cfg) return;
  const newMode = cfg.displayMode || 'icon';
  if (newMode !== currentDisplayMode) {
    applyDisplayMode(newMode);
  }
});
