(function () {
  function postToNative(payload) {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(payload);
    } else if (window.parent && window.parent !== window) {
      window.parent.postMessage({ __winzenBridge: true, payload }, '*');
    }
  }

  window.winzen = {
    state: null,
    metrics: null,
    send(action, data = {}) {
      postToNative({ action, ...data });
    },
    shell: {
      openStartMenu() {
        postToNative({ action: 'open_start_menu' });
      },
      openSettings() {
        postToNative({ action: 'open_settings' });
      },
      emergencyRestore() {
        postToNative({ action: 'emergency_restore' });
      },
      quit() {
        postToNative({ action: 'quit_app' });
      }
    },
    tasks: {
      activateWindow(hwnd) {
        postToNative({ action: 'activate_window', hwnd: Number(hwnd) });
      },
      closeWindow(hwnd) {
        postToNative({ action: 'close_window', hwnd: Number(hwnd) });
      },
      hideApp(hwnd, processName = '', title = '') {
        postToNative({
          action: 'hide_app',
          hwnd: Number(hwnd),
          processName: String(processName || ''),
          title: String(title || '')
        });
      },
      hideWindow(hwnd, processName = '', title = '') {
        postToNative({
          action: 'hide_app',
          hwnd: Number(hwnd),
          processName: String(processName || ''),
          title: String(title || '')
        });
      },
      unhideApp(appId) {
        postToNative({ action: 'unhide_app', appId: String(appId || '') });
      },
      unhideWindow(appId) {
        postToNative({ action: 'unhide_app', appId: String(appId || '') });
      },
      unhideAllWindows() {
        postToNative({ action: 'unhide_all_apps' });
      },
      unhideAllApps() {
        postToNative({ action: 'unhide_all_apps' });
      }
    }
  };

  function handleIncomingMessage(msg) {
    if (!msg || typeof msg !== 'object') return;
    if (msg.type === 'state_update' && msg.payload) {
      window.winzen.state = msg.payload;
      window.dispatchEvent(new CustomEvent('winzen:state', { detail: msg.payload }));
    } else if (msg.type === 'metrics_update' && msg.payload) {
      window.winzen.metrics = msg.payload;
      window.dispatchEvent(new CustomEvent('winzen:metrics', { detail: msg.payload }));
    }
  }

  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (event) => {
      handleIncomingMessage(event.data);
    });
  }

  window.addEventListener('message', (event) => {
    const data = event.data;
    if (!data) return;
    if (data.__winzenHostEvent) {
      handleIncomingMessage(data.message);
    } else if (data.__winzenBridge && window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(data.payload);
    }
  });
})();
