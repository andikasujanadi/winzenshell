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
      },
      setTheme(theme) {
        postToNative({ action: 'set_theme', theme: String(theme || 'dark') });
      },
      setAccentColor(color) {
        postToNative({ action: 'set_accent_color', accentColor: String(color || '#3684E3') });
      },
      setCornerRadius(radiusPx) {
        postToNative({ action: 'set_corner_radius', taskbarCornerRadiusPx: Number(radiusPx) });
      }
    },
    tasks: {
      activateWindow(hwnd, wasActive = undefined) {
        const msg = { action: 'activate_window', hwnd: Number(hwnd) };
        if (typeof wasActive === 'boolean') msg.wasActive = wasActive;
        postToNative(msg);
      },
      toggleWindowSelector(itemPayload) {
        postToNative({
          action: 'toggle_window_selector',
          appId: String((itemPayload && itemPayload.appId) || ''),
          processName: String((itemPayload && itemPayload.processName) || ''),
          iconDataUrl: String((itemPayload && itemPayload.iconDataUrl) || ''),
          x: Number((itemPayload && itemPayload.x) || 0),
          width: Number((itemPayload && itemPayload.width) || 38),
          windows: Array.isArray(itemPayload && itemPayload.windows) ? itemPayload.windows : []
        });
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
      },
      showContextMenu(itemPayload) {
        postToNative({
          action: 'show_window_context_menu',
          ...itemPayload
        });
      },
      pinApp(appId, processName, title, exePath, iconDataUrl) {
        postToNative({
          action: 'pin_app',
          appId: String(appId || ''),
          processName: String(processName || ''),
          title: String(title || ''),
          exePath: String(exePath || ''),
          iconDataUrl: String(iconDataUrl || '')
        });
      },
      unpinApp(appId, processName) {
        postToNative({
          action: 'unpin_app',
          appId: String(appId || ''),
          processName: String(processName || '')
        });
      },
      launchApp(exePath, processName) {
        postToNative({
          action: 'launch_app',
          exePath: String(exePath || ''),
          processName: String(processName || '')
        });
      }
    },
    brightness: {
      get() {
        return window.winzen.metrics && typeof window.winzen.metrics.brightnessPercent === 'number'
          ? window.winzen.metrics.brightnessPercent
          : 100;
      },
      set(val) {
        postToNative({ action: 'set_brightness', value: Number(val) });
      },
      toggle(x, y, width, height) {
        postToNative({
          action: 'toggle_brightness_flyout',
          x: Number(x || 0),
          y: Number(y || 0),
          width: Number(width || 34),
          height: Number(height || 34)
        });
      }
    },
    volume: {
      get() {
        return window.winzen.metrics && typeof window.winzen.metrics.volumePercent === 'number'
          ? window.winzen.metrics.volumePercent
          : 50;
      },
      isMuted() {
        return window.winzen.metrics ? !!window.winzen.metrics.isMuted : false;
      },
      set(val) {
        postToNative({ action: 'set_volume', value: Number(val) });
      },
      toggleMute() {
        postToNative({ action: 'toggle_mute' });
      },
      toggle(x, y, width, height) {
        postToNative({
          action: 'toggle_volume_flyout',
          x: Number(x || 0),
          y: Number(y || 0),
          width: Number(width || 34),
          height: Number(height || 34)
        });
      }
    },
    desktops: {
      get() {
        return (window.winzen.metrics && window.winzen.metrics.virtualDesktops) || [];
      },
      getCurrentIndex() {
        return window.winzen.metrics ? (window.winzen.metrics.currentVirtualDesktopIndex || 0) : 0;
      },
      getCurrentName() {
        return window.winzen.metrics ? (window.winzen.metrics.currentVirtualDesktopName || 'Desktop 1') : 'Desktop 1';
      },
      switch(index) {
        postToNative({ action: 'switch_desktop', index: Number(index) });
      },
      next() {
        postToNative({ action: 'next_desktop' });
      },
      previous() {
        postToNative({ action: 'previous_desktop' });
      },
      create() {
        postToNative({ action: 'create_desktop' });
      },
      openTaskView() {
        postToNative({ action: 'open_task_view' });
      }
    },
    controlCenter: {
      toggle(x, y, width, height) {
        postToNative({
          action: 'toggle_control_center',
          x: Number(x || 0),
          y: Number(y || 0),
          width: Number(width || 34),
          height: Number(height || 34)
        });
      },
      close() {
        postToNative({ action: 'close_control_center' });
      }
    },
    clock: {
      toggle(x, y, width, height) {
        postToNative({
          action: 'toggle_clock_flyout',
          x: Number(x || 0),
          y: Number(y || 0),
          width: Number(width || 34),
          height: Number(height || 34)
        });
      }
    },
    appLauncher: {
      toggle(x, y, width, height) {
        postToNative({
          action: 'toggle_app_launcher',
          x: Number(x || 0),
          y: Number(y || 0),
          width: Number(width || 34),
          height: Number(height || 34)
        });
      }
    },
    systemMonitor: {
      toggle(x, y, width, height) {
        postToNative({
          action: 'toggle_system_monitor_flyout',
          x: Number(x || 0),
          y: Number(y || 0),
          width: Number(width || 34),
          height: Number(height || 34)
        });
      }
    }
  };

  function handleIncomingMessage(msg) {
    if (!msg || typeof msg !== 'object') return;
    if (msg.type === 'state_update' && msg.payload) {
      window.winzen.state = msg.payload;
      const theme = (msg.payload.settings && msg.payload.settings.theme) || 'dark';
      document.documentElement.setAttribute('data-theme', theme);
      if (document.body) {
        if (theme === 'light') {
          document.body.classList.add('theme-light');
          document.body.classList.remove('theme-dark');
        } else {
          document.body.classList.add('theme-dark');
          document.body.classList.remove('theme-light');
        }
      }
      window.dispatchEvent(new CustomEvent('winzen:state', { detail: msg.payload }));
      window.dispatchEvent(new CustomEvent('winzen:theme', { detail: { theme } }));
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
