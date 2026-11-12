(function () {
  let allApps = [];
  let recentApps = [];
  let visibleApps = [];
  let selectedIndex = -1;
  let lastAppsSig = '';

  const searchInput = document.getElementById('searchInput');
  const btnClearSearch = document.getElementById('btnClearSearch');
  const sectionRecent = document.getElementById('sectionRecent');
  const recentGrid = document.getElementById('recentGrid');
  const appsGrid = document.getElementById('appsGrid');
  const allAppsTitle = document.getElementById('allAppsTitle');
  const appCountBadge = document.getElementById('appCountBadge');

  function sendHost(action, payload = {}) {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage({ action, ...payload });
    }
  }

  function renderAppItem(app, isSelected) {
    const item = document.createElement('div');
    item.className = 'app-item' + (isSelected ? ' selected' : '');
    item.tabIndex = 0;
    item.title = app.name;

    const iconWrap = document.createElement('div');
    iconWrap.className = 'app-icon-wrap';

    if (app.iconDataUrl) {
      const img = document.createElement('img');
      img.className = 'app-icon-img';
      img.decoding = 'async';
      img.src = app.iconDataUrl;
      img.alt = app.name;
      img.onerror = () => {
        img.remove();
        iconWrap.innerHTML = getFallbackIconSvg();
      };
      iconWrap.appendChild(img);
    } else {
      iconWrap.innerHTML = getFallbackIconSvg();
    }

    const nameLabel = document.createElement('div');
    nameLabel.className = 'app-name';
    nameLabel.textContent = app.name;

    item.appendChild(iconWrap);
    item.appendChild(nameLabel);

    item.addEventListener('click', () => {
      launchApp(app);
    });

    return item;
  }

  function getFallbackIconSvg() {
    return `<svg class="app-icon-fallback" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
      <rect x="3" y="3" width="7" height="7"></rect>
      <rect x="14" y="3" width="7" height="7"></rect>
      <rect x="14" y="14" width="7" height="7"></rect>
      <rect x="3" y="14" width="7" height="7"></rect>
    </svg>`;
  }

  function launchApp(app) {
    if (!app) return;
    sendHost('launch_app', { target: app.targetPath, name: app.name });
  }

  function updateView() {
    const query = (searchInput.value || '').trim().toLowerCase();

    if (!query) {
      // Empty search: Show Recent & All Apps
      btnClearSearch.style.display = 'none';
      sectionRecent.style.display = recentApps.length > 0 ? 'block' : 'none';
      allAppsTitle.textContent = 'All Apps';
      visibleApps = allApps;
      selectedIndex = -1;
    } else {
      // Active query: Hide Recent, filter All Apps
      btnClearSearch.style.display = 'flex';
      sectionRecent.style.display = 'none';

      visibleApps = allApps.filter(a => {
        const name = (a.name || '').toLowerCase();
        return name.includes(query);
      });

      // Sort by prefix match first
      visibleApps.sort((a, b) => {
        const aStarts = (a.name || '').toLowerCase().startsWith(query);
        const bStarts = (b.name || '').toLowerCase().startsWith(query);
        if (aStarts && !bStarts) return -1;
        if (!aStarts && bStarts) return 1;
        return (a.name || '').localeCompare(b.name || '');
      });

      allAppsTitle.textContent = `Results (${visibleApps.length})`;
      selectedIndex = visibleApps.length > 0 ? 0 : -1;
    }

    appCountBadge.textContent = visibleApps.length;

    // Render Recent row
    recentGrid.innerHTML = '';
    recentApps.slice(0, 5).forEach(app => {
      recentGrid.appendChild(renderAppItem(app, false));
    });

    // Render Apps grid
    appsGrid.innerHTML = '';
    if (visibleApps.length === 0) {
      const empty = document.createElement('div');
      empty.className = 'apps-empty';
      empty.textContent = query ? `No apps matching "${query}"` : 'No applications found';
      appsGrid.appendChild(empty);
    } else {
      visibleApps.forEach((app, idx) => {
        appsGrid.appendChild(renderAppItem(app, idx === selectedIndex));
      });
    }

    scrollSelectedIntoView();
  }

  function scrollSelectedIntoView() {
    if (selectedIndex < 0) return;
    const items = appsGrid.querySelectorAll('.app-item');
    if (items[selectedIndex]) {
      items[selectedIndex].scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    }
  }

  // Keyboard navigation
  searchInput.addEventListener('input', () => {
    updateView();
  });

  searchInput.addEventListener('keydown', (e) => {
    const cols = 5;
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      if (visibleApps.length > 0) {
        selectedIndex = Math.min(selectedIndex + cols, visibleApps.length - 1);
        refreshSelected();
      }
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      if (selectedIndex >= 0) {
        selectedIndex = Math.max(selectedIndex - cols, 0);
        refreshSelected();
      }
    } else if (e.key === 'ArrowRight') {
      e.preventDefault();
      if (visibleApps.length > 0) {
        selectedIndex = Math.min(selectedIndex + 1, visibleApps.length - 1);
        refreshSelected();
      }
    } else if (e.key === 'ArrowLeft') {
      e.preventDefault();
      if (selectedIndex > 0) {
        selectedIndex = Math.max(selectedIndex - 1, 0);
        refreshSelected();
      }
    } else if (e.key === 'Enter') {
      e.preventDefault();
      if (selectedIndex >= 0 && visibleApps[selectedIndex]) {
        launchApp(visibleApps[selectedIndex]);
      } else if (visibleApps.length > 0) {
        launchApp(visibleApps[0]);
      }
    } else if (e.key === 'Escape') {
      e.preventDefault();
      if (searchInput.value) {
        searchInput.value = '';
        updateView();
      } else {
        sendHost('close_launcher');
      }
    }
  });

  function refreshSelected() {
    const items = appsGrid.querySelectorAll('.app-item');
    items.forEach((item, idx) => {
      if (idx === selectedIndex) {
        item.classList.add('selected');
      } else {
        item.classList.remove('selected');
      }
    });
    scrollSelectedIntoView();
  }

  btnClearSearch.addEventListener('click', () => {
    searchInput.value = '';
    searchInput.focus();
    updateView();
  });

  // Host WebMessage listener
  window.addEventListener('message', (event) => {
    handleHostData(event.data);
  });

  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (event) => {
      handleHostData(event.data);
    });
  }

  function handleHostData(data) {
    if (!data) return;
    if (data.type === 'theme_update' || data.action === 'theme_update') {
      const theme = data.theme || 'dark';
      document.documentElement.setAttribute('data-theme', theme);
      document.body.classList.toggle('theme-light', theme === 'light');
      if (typeof data.enableAnimations === 'boolean') {
        document.body.classList.toggle('winzen-no-anim', !data.enableAnimations);
      }
      if (data.accentColor) {
        document.documentElement.style.setProperty('--winzen-accent', data.accentColor);
      }
    } else if (data.type === 'apps_data' || data.action === 'apps_data') {
      const payload = data.payload || data;
      const theme = data.theme || payload.theme;
      if (theme) {
        document.documentElement.setAttribute('data-theme', theme);
        document.body.classList.toggle('theme-light', theme === 'light');
      }
      if (typeof data.enableAnimations === 'boolean') {
        document.body.classList.toggle('winzen-no-anim', !data.enableAnimations);
      }
      const accent = data.accentColor || payload.accentColor;
      if (accent) {
        document.documentElement.style.setProperty('--winzen-accent', accent);
      }
      const nextAll = Array.isArray(payload.all) ? payload.all : [];
      const nextRecent = Array.isArray(payload.recent) ? payload.recent : [];
      const sig = JSON.stringify({
        r: nextRecent.map((a) => `${a.targetPath}|${a.name}|${a.iconDataUrl ? a.iconDataUrl.length : 0}`),
        a: nextAll.map((a) => `${a.targetPath}|${a.name}|${a.iconDataUrl ? a.iconDataUrl.length : 0}`)
      });
      if (sig !== lastAppsSig) {
        lastAppsSig = sig;
        allApps = nextAll;
        recentApps = nextRecent;
        updateView();
      }
    } else if (data.type === 'focus_search' || data.action === 'focus_search') {
      if (searchInput.value) {
        searchInput.value = '';
        updateView();
      } else {
        selectedIndex = -1;
        refreshSelected();
      }
      const contentEl = document.querySelector('.launcher-content');
      if (contentEl) contentEl.scrollTop = 0;
      setTimeout(() => {
        searchInput.focus();
        searchInput.select();
      }, 30);
    }
  }

  // Request initial app catalog
  sendHost('request_apps');
})();
