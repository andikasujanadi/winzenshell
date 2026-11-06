document.addEventListener('DOMContentLoaded', () => {
  const navItems = document.querySelectorAll('.pane-item');
  const tabViews = document.querySelectorAll('.tab-view');
  const breadcrumbCurrent = document.getElementById('breadcrumbCurrent');

  const tabTitles = {
    'tab-shell': 'Taskbar',
    'tab-widgets': 'Widgets & Layout',
    'tab-sdk': 'Developer SDK'
  };

  navItems.forEach((btn) => {
    btn.addEventListener('click', () => {
      navItems.forEach((b) => b.classList.remove('active'));
      tabViews.forEach((p) => p.classList.remove('active'));
      btn.classList.add('active');
      const targetId = btn.dataset.tab;
      const target = document.getElementById(targetId);
      if (target) target.classList.add('active');
      if (breadcrumbCurrent) breadcrumbCurrent.textContent = tabTitles[targetId] || 'Taskbar';
    });
  });

  // UI Controls
  const statusBannerText = document.getElementById('statusBannerText');
  const toggleHideNative = document.getElementById('toggleHideNative');
  const stateHideNative = document.getElementById('stateHideNative');

  const toggleWinZenTaskbar = document.getElementById('toggleWinZenTaskbar');
  const stateWinZenTaskbar = document.getElementById('stateWinZenTaskbar');

  const toggleShowAppNames = document.getElementById('toggleShowAppNames');
  const stateShowAppNames = document.getElementById('stateShowAppNames');

  const toggleCombineApps = document.getElementById('toggleCombineApps');
  const stateCombineApps = document.getElementById('stateCombineApps');

  const hiddenAppsContainer = document.getElementById('hiddenAppsContainer');
  const hiddenAppsSubtitle = document.getElementById('hiddenAppsSubtitle');
  const btnUnhideAllApps = document.getElementById('btnUnhideAllApps');

  const toggleRespectWorkArea = document.getElementById('toggleRespectWorkArea');
  const stateRespectWorkArea = document.getElementById('stateRespectWorkArea');

  const selectDockEdge = document.getElementById('selectDockEdge');
  const inputTaskbarHeight = document.getElementById('inputTaskbarHeight');
  const labelHeightVal = document.getElementById('labelHeightVal');
  const inputFloatingMargin = document.getElementById('inputFloatingMargin');
  const labelMarginVal = document.getElementById('labelMarginVal');

  // Native Taskbar Telemetry
  const valTrayHwnd = document.getElementById('valTrayHwnd');
  const valNativeVisible = document.getElementById('valNativeVisible');
  const valNativeAutoHide = document.getElementById('valNativeAutoHide');
  const valSecondaryCount = document.getElementById('valSecondaryCount');

  // WorkArea Telemetry
  const previewTopBar = document.getElementById('previewTopBar');
  const previewBottomBar = document.getElementById('previewBottomBar');
  const previewWorkAreaDims = document.getElementById('previewWorkAreaDims');
  const previewWorkAreaCoords = document.getElementById('previewWorkAreaCoords');
  const valMonitorBounds = document.getElementById('valMonitorBounds');
  const valWorkAreaBounds = document.getElementById('valWorkAreaBounds');
  const valReservedSpace = document.getElementById('valReservedSpace');
  const valAppBarStatus = document.getElementById('valAppBarStatus');

  // Region Containers & Preview Chips
  const previewChipsLeft = document.getElementById('previewChipsLeft');
  const previewChipsCenter = document.getElementById('previewChipsCenter');
  const previewChipsRight = document.getElementById('previewChipsRight');

  const widgetsRegionLeft = document.getElementById('widgetsRegionLeft');
  const widgetsRegionCenter = document.getElementById('widgetsRegionCenter');
  const widgetsRegionRight = document.getElementById('widgetsRegionRight');

  const countRegionLeft = document.getElementById('countRegionLeft');
  const countRegionCenter = document.getElementById('countRegionCenter');
  const countRegionRight = document.getElementById('countRegionRight');

  let isUpdatingFromHost = false;
  let latestWidgets = [];
  let draggedWidgetId = null;

  function sendWorkAreaConfig() {
    if (isUpdatingFromHost) return;
    stateRespectWorkArea.textContent = toggleRespectWorkArea.checked ? 'On' : 'Off';
    window.winzen.send('update_workarea_config', {
      respectWorkArea: toggleRespectWorkArea.checked,
      dockEdge: selectDockEdge.value,
      taskbarHeightPx: parseInt(inputTaskbarHeight.value, 10) || 48,
      floatingMarginPx: parseInt(inputFloatingMargin.value, 10) || 0
    });
  }

  toggleHideNative.addEventListener('change', () => {
    if (isUpdatingFromHost) return;
    stateHideNative.textContent = toggleHideNative.checked ? 'On' : 'Off';
    window.winzen.send('toggle_native_taskbar', {
      hide: toggleHideNative.checked
    });
  });

  toggleWinZenTaskbar.addEventListener('change', () => {
    if (isUpdatingFromHost) return;
    stateWinZenTaskbar.textContent = toggleWinZenTaskbar.checked ? 'On' : 'Off';
    window.winzen.send('toggle_winzen_taskbar', {
      enable: toggleWinZenTaskbar.checked
    });
  });

  function updateActiveWindowsWidgetConfig(patch) {
    const activeWinWidget = latestWidgets.find((w) => w.id === 'community.zen-active-windows') || {
      id: 'community.zen-active-windows',
      enabled: true,
      slot: 'center',
      order: 10,
      width: 420,
      autoWidth: true,
      showAppName: true,
      combineApps: false
    };

    window.winzen.send('update_widget_state', {
      id: activeWinWidget.id,
      enabled: activeWinWidget.enabled !== false,
      slot: activeWinWidget.slot || 'center',
      order: activeWinWidget.order ?? 10,
      width: activeWinWidget.width || 420,
      autoWidth: activeWinWidget.autoWidth !== false,
      showAppName: patch.showAppName !== undefined ? patch.showAppName : (activeWinWidget.showAppName !== false),
      combineApps: patch.combineApps !== undefined ? patch.combineApps : !!activeWinWidget.combineApps
    });
  }

  toggleShowAppNames.addEventListener('change', () => {
    if (isUpdatingFromHost) return;
    const show = toggleShowAppNames.checked;
    stateShowAppNames.textContent = show ? 'On' : 'Off';
    updateActiveWindowsWidgetConfig({ showAppName: show });
  });

  toggleCombineApps.addEventListener('change', () => {
    if (isUpdatingFromHost) return;
    const combine = toggleCombineApps.checked;
    stateCombineApps.textContent = combine ? 'On' : 'Off';
    updateActiveWindowsWidgetConfig({ combineApps: combine });
  });

  btnUnhideAllApps.addEventListener('click', () => {
    window.winzen.tasks.unhideAllApps();
  });

  toggleRespectWorkArea.addEventListener('change', sendWorkAreaConfig);
  selectDockEdge.addEventListener('change', sendWorkAreaConfig);

  inputTaskbarHeight.addEventListener('input', () => {
    labelHeightVal.textContent = `${inputTaskbarHeight.value}px`;
  });
  inputTaskbarHeight.addEventListener('change', sendWorkAreaConfig);

  inputFloatingMargin.addEventListener('input', () => {
    labelMarginVal.textContent = `${inputFloatingMargin.value}px`;
  });
  inputFloatingMargin.addEventListener('change', sendWorkAreaConfig);

  document.getElementById('btnEmergencySidebar').addEventListener('click', () => {
    window.winzen.shell.emergencyRestore();
  });

  document.getElementById('btnQuitSidebar').addEventListener('click', () => {
    window.winzen.shell.quit();
  });

  document.getElementById('btnQuitTop').addEventListener('click', () => {
    window.winzen.shell.quit();
  });

  document.getElementById('btnMaximizeTest').addEventListener('click', () => {
    window.winzen.send('toggle_maximize_test');
  });

  document.getElementById('btnRefreshState').addEventListener('click', () => {
    window.winzen.send('request_state');
  });

  document.getElementById('btnOpenWidgetsFolder').addEventListener('click', () => {
    window.winzen.send('open_widgets_folder');
  });

  document.getElementById('btnReloadWidgets').addEventListener('click', () => {
    window.winzen.send('reload_widgets');
  });

  document.getElementById('btnCreateWidget').addEventListener('click', () => {
    const nameInput = document.getElementById('newWidgetName');
    const authorInput = document.getElementById('newWidgetAuthor');
    const slotInput = document.getElementById('newWidgetSlot');
    const widthInput = document.getElementById('newWidgetWidth');

    const name = nameInput.value.trim() || 'My Zen Widget';
    window.winzen.send('create_community_widget', {
      name,
      author: authorInput.value.trim() || 'Community',
      slot: slotInput.value,
      width: parseInt(widthInput.value, 10) || 160
    });
    nameInput.value = '';
  });

  // Setup Drag & Drop on Region Dropzones
  [widgetsRegionLeft, widgetsRegionCenter, widgetsRegionRight].forEach((zone) => {
    zone.addEventListener('dragover', (e) => {
      e.preventDefault();
      zone.classList.add('drag-over');
    });

    zone.addEventListener('dragleave', () => {
      zone.classList.remove('drag-over');
    });

    zone.addEventListener('drop', (e) => {
      e.preventDefault();
      zone.classList.remove('drag-over');
      if (!draggedWidgetId) return;

      const targetRegion = zone.dataset.region;
      moveWidgetToRegion(draggedWidgetId, targetRegion);
      draggedWidgetId = null;
    });
  });

  window.addEventListener('winzen:state', (e) => {
    const data = e.detail;
    if (!data) return;

    isUpdatingFromHost = true;
    try {
      const bannerText = data.statusBanner || 'Ready';
      statusBannerText.innerHTML = `<span class="status-dot"></span> <span>${escapeHtml(bannerText)}</span>`;

      const s = data.settings || {};
      toggleHideNative.checked = !!s.hideNativeTaskbar;
      stateHideNative.textContent = s.hideNativeTaskbar ? 'On' : 'Off';

      toggleWinZenTaskbar.checked = !!s.enableWinZenTaskbar;
      stateWinZenTaskbar.textContent = s.enableWinZenTaskbar ? 'On' : 'Off';

      toggleRespectWorkArea.checked = !!s.respectDesktopWorkArea;
      stateRespectWorkArea.textContent = s.respectDesktopWorkArea ? 'On' : 'Off';

      selectDockEdge.value = s.dockEdge || 'Bottom';
      inputTaskbarHeight.value = s.taskbarHeightPx || 48;
      labelHeightVal.textContent = `${s.taskbarHeightPx || 48}px`;
      inputFloatingMargin.value = s.floatingMarginPx || 0;
      labelMarginVal.textContent = `${s.floatingMarginPx || 0}px`;

      latestWidgets = (data.widgets || []).slice().sort((a, b) => (a.order ?? 10) - (b.order ?? 10));
      const activeWinWidget = latestWidgets.find((w) => w.id === 'community.zen-active-windows');
      const showLabels = activeWinWidget ? activeWinWidget.showAppName !== false : true;
      const combine = activeWinWidget ? !!activeWinWidget.combineApps : false;

      toggleShowAppNames.checked = showLabels;
      stateShowAppNames.textContent = showLabels ? 'On' : 'Off';

      toggleCombineApps.checked = combine;
      stateCombineApps.textContent = combine ? 'On' : 'Off';

      renderHiddenApps(data.hiddenApps || []);

      const nt = data.nativeTaskbar || {};
      valTrayHwnd.textContent = nt.primaryHandleHex || '0x0';
      valNativeVisible.textContent = nt.isHidden ? 'Hidden' : 'Visible';
      valNativeAutoHide.textContent = nt.isAutoHideEnabled ? 'On' : 'Off';
      valSecondaryCount.textContent = String(nt.secondaryCount ?? 0);

      const wa = data.workArea || {};
      const monW = (wa.monitorRight || 1920) - (wa.monitorLeft || 0);
      const monH = (wa.monitorBottom || 1080) - (wa.monitorTop || 0);
      const workW = (wa.workAreaRight || 1920) - (wa.workAreaLeft || 0);
      const workH = (wa.workAreaBottom || 1080) - (wa.workAreaTop || 0);

      valMonitorBounds.textContent = `${monW} × ${monH}`;
      valWorkAreaBounds.textContent = `${workW} × ${workH}`;
      valReservedSpace.textContent = `Top: ${wa.reservedTopPx || 0}px · Bottom: ${wa.reservedBottomPx || 0}px`;
      valAppBarStatus.textContent = wa.isAppBarRegistered
        ? 'Registered (ABM_SETPOS)'
        : 'Overlay (Unregistered)';

      previewWorkAreaDims.textContent = `${workW} × ${workH}`;
      previewWorkAreaCoords.textContent = `(${wa.workAreaLeft}, ${wa.workAreaTop}) → (${wa.workAreaRight}, ${wa.workAreaBottom})`;

      const isTop = (s.dockEdge || 'Bottom').toLowerCase() === 'top';
      if (!s.enableWinZenTaskbar) {
        previewTopBar.classList.add('hidden');
        previewBottomBar.classList.add('hidden');
      } else if (isTop) {
        previewTopBar.classList.remove('hidden');
        previewTopBar.textContent = `WinZenShell (Top · ${s.taskbarHeightPx}px)`;
        previewBottomBar.classList.add('hidden');
      } else {
        previewTopBar.classList.add('hidden');
        previewBottomBar.classList.remove('hidden');
        previewBottomBar.textContent = `WinZenShell (Bottom · ${s.taskbarHeightPx}px)`;
      }

      renderWidgetsByRegion(latestWidgets);
    } finally {
      isUpdatingFromHost = false;
    }
  });

  function renderHiddenApps(hiddenList) {
    hiddenAppsContainer.innerHTML = '';
    if (!Array.isArray(hiddenList) || hiddenList.length === 0) {
      hiddenAppsSubtitle.textContent =
        'No hidden apps. Right-click any app in the Active Window Switcher on the taskbar and select "Hide app".';
      btnUnhideAllApps.style.display = 'none';
      return;
    }

    hiddenAppsSubtitle.textContent = `${hiddenList.length} application(s) hidden. Click below to unhide all windows of that application:`;
    btnUnhideAllApps.style.display = 'inline-flex';

    hiddenList.forEach((item) => {
      const chip = document.createElement('button');
      chip.className = 'hidden-app-chip';
      chip.title = `Click to unhide ${item.displayName || item.processName}`;
      chip.innerHTML = `
        <span>${escapeHtml(item.displayName || item.processName)}</span>
        <span class="unhide-tag">Unhide</span>
      `;
      chip.addEventListener('click', () => {
        window.winzen.tasks.unhideApp(item.appId);
      });
      hiddenAppsContainer.appendChild(chip);
    });
  }

  function renderWidgetsByRegion(widgets) {
    const groups = {
      left: [],
      center: [],
      right: []
    };

    widgets.forEach((w) => {
      const slot = (w.slot || 'right').toLowerCase();
      if (slot === 'left') groups.left.push(w);
      else if (slot === 'center') groups.center.push(w);
      else groups.right.push(w);
    });

    ['left', 'center', 'right'].forEach((r) => {
      groups[r].sort((a, b) => (a.order ?? 10) - (b.order ?? 10));
    });

    // Update top visual preview bar
    renderPreviewChips(previewChipsLeft, groups.left);
    renderPreviewChips(previewChipsCenter, groups.center);
    renderPreviewChips(previewChipsRight, groups.right);

    // Update region counts
    countRegionLeft.textContent = `${groups.left.length} widget${groups.left.length === 1 ? '' : 's'}`;
    countRegionCenter.textContent = `${groups.center.length} widget${groups.center.length === 1 ? '' : 's'}`;
    countRegionRight.textContent = `${groups.right.length} widget${groups.right.length === 1 ? '' : 's'}`;

    // Render separated region lists
    renderRegionStack(widgetsRegionLeft, groups.left, 'left');
    renderRegionStack(widgetsRegionCenter, groups.center, 'center');
    renderRegionStack(widgetsRegionRight, groups.right, 'right');
  }

  function renderPreviewChips(container, list) {
    container.innerHTML = '';
    if (list.length === 0) {
      container.innerHTML = `<span class="rp-chip disabled">Empty</span>`;
      return;
    }
    list.forEach((w, idx) => {
      const chip = document.createElement('span');
      chip.className = `rp-chip ${w.enabled ? '' : 'disabled'}`;
      chip.innerHTML = `<span class="rp-chip-order">#${idx + 1}</span><span>${escapeHtml(w.name)}</span>`;
      container.appendChild(chip);
    });
  }

  function renderRegionStack(container, regionWidgets, regionKey) {
    container.innerHTML = '';
    if (regionWidgets.length === 0) {
      container.innerHTML = `<div class="region-empty">No widgets in ${regionKey} region — drag a widget here or change its region selector</div>`;
      return;
    }

    regionWidgets.forEach((w, index) => {
      const isAutoWidth = w.autoWidth !== false;
      const isWindowSwitcher = w.id === 'community.zen-active-windows';
      const isStartLauncher = w.id === 'community.zen-start-button';

      const expander = document.createElement('div');
      expander.className = 'win11-expander widget-expander';
      expander.draggable = true;
      expander.dataset.widgetId = w.id;

      expander.addEventListener('dragstart', (e) => {
        draggedWidgetId = w.id;
        expander.classList.add('dragging');
        if (e.dataTransfer) {
          e.dataTransfer.effectAllowed = 'move';
          e.dataTransfer.setData('text/plain', w.id);
        }
      });

      expander.addEventListener('dragend', () => {
        expander.classList.remove('dragging');
        draggedWidgetId = null;
      });

      let specializedOptionsHtml = '';

      if (isStartLauncher) {
        const curIcon = w.startIcon || 'windows';
        const curText = w.startText || 'Start';
        const showStartText = !!w.showStartText;

        specializedOptionsHtml = `
          <div class="subrow-field">
            <label>Launcher icon</label>
            <select class="win11-select w-starticon">
              <option value="windows" ${curIcon === 'windows' ? 'selected' : ''}>Windows 11 (Fluent)</option>
              <option value="win10" ${curIcon === 'win10' ? 'selected' : ''}>Windows 10</option>
              <option value="arch" ${curIcon === 'arch' ? 'selected' : ''}>Arch Linux</option>
              <option value="ubuntu" ${curIcon === 'ubuntu' ? 'selected' : ''}>Ubuntu</option>
              <option value="debian" ${curIcon === 'debian' ? 'selected' : ''}>Debian</option>
              <option value="fedora" ${curIcon === 'fedora' ? 'selected' : ''}>Fedora</option>
              <option value="tux" ${curIcon === 'tux' ? 'selected' : ''}>Tux (Linux Penguin)</option>
              <option value="apple" ${curIcon === 'apple' ? 'selected' : ''}>Apple</option>
              <option value="zen" ${curIcon === 'zen' ? 'selected' : ''}>Zen Lotus</option>
            </select>
          </div>
          <label class="inline-check">
            <input type="checkbox" class="w-showstarttext" ${showStartText ? 'checked' : ''} />
            <span>Show text label</span>
          </label>
          <div class="subrow-field" style="${!showStartText ? 'opacity:0.4;' : ''}">
            <label>Label text</label>
            <input type="text" class="win11-input w-starttext" value="${escapeHtml(curText)}" placeholder="Start" style="width: 100px;" ${!showStartText ? 'disabled' : ''} />
          </div>
        `;
      } else if (isWindowSwitcher) {
        specializedOptionsHtml = `
          <label class="inline-check">
            <input type="checkbox" class="w-showappname" ${w.showAppName !== false ? 'checked' : ''} />
            <span>Show application name</span>
          </label>
          <label class="inline-check">
            <input type="checkbox" class="w-combineapps" ${w.combineApps ? 'checked' : ''} />
            <span>Combine same apps</span>
          </label>
        `;
      }

      expander.innerHTML = `
        <div class="win11-card">
          <div class="card-left">
            <span class="drag-grip" title="Drag to move widget between regions">⋮⋮</span>
            <div class="card-titles">
              <span class="card-title">${escapeHtml(w.name)}</span>
              <span class="card-subtitle">${escapeHtml(w.description || 'Community Widget')} · Widgets/${escapeHtml(w.folderName)}</span>
            </div>
          </div>
          <div class="card-right">
            <div class="arrange-btns">
              <button class="arrange-btn btn-move-up" title="Move earlier in ${regionKey} region" ${index === 0 ? 'disabled' : ''}>↑</button>
              <button class="arrange-btn btn-move-down" title="Move later in ${regionKey} region" ${index === regionWidgets.length - 1 ? 'disabled' : ''}>↓</button>
            </div>
            <span class="toggle-state-text w-state-lbl">${w.enabled ? 'On' : 'Off'}</span>
            <label class="win11-toggle">
              <input type="checkbox" class="w-enable" ${w.enabled ? 'checked' : ''} />
              <span class="toggle-track"><span class="toggle-thumb"></span></span>
            </label>
          </div>
        </div>
        <div class="expander-subrow widget-row-options">
          <div class="subrow-field">
            <label>Region</label>
            <select class="win11-select w-slot">
              <option value="left" ${regionKey === 'left' ? 'selected' : ''}>Left region</option>
              <option value="center" ${regionKey === 'center' ? 'selected' : ''}>Center region</option>
              <option value="right" ${regionKey === 'right' ? 'selected' : ''}>Right region</option>
            </select>
          </div>
          <label class="inline-check">
            <input type="checkbox" class="w-autowidth" ${isAutoWidth ? 'checked' : ''} />
            <span>Dynamic width</span>
          </label>
          <div class="subrow-field" style="${isAutoWidth ? 'opacity:0.4;' : ''}">
            <label>Fixed width</label>
            <input type="number" class="win11-input w-width" min="36" max="640" value="${w.width || 160}" ${isAutoWidth ? 'disabled' : ''} style="width: 80px;" />
          </div>
          ${specializedOptionsHtml}
        </div>
      `;

      const btnMoveUp = expander.querySelector('.btn-move-up');
      const btnMoveDown = expander.querySelector('.btn-move-down');
      const enableInput = expander.querySelector('.w-enable');
      const stateLbl = expander.querySelector('.w-state-lbl');
      const slotSelect = expander.querySelector('.w-slot');
      const autoWidthInput = expander.querySelector('.w-autowidth');
      const widthInput = expander.querySelector('.w-width');

      const showAppNameInput = expander.querySelector('.w-showappname');
      const combineAppsInput = expander.querySelector('.w-combineapps');
      const startIconSelect = expander.querySelector('.w-starticon');
      const showStartTextInput = expander.querySelector('.w-showstarttext');
      const startTextInput = expander.querySelector('.w-starttext');

      btnMoveUp.addEventListener('click', () => {
        swapWidgetOrderInRegion(regionWidgets, index, index - 1, regionKey);
      });

      btnMoveDown.addEventListener('click', () => {
        swapWidgetOrderInRegion(regionWidgets, index, index + 1, regionKey);
      });

      const pushUpdate = () => {
        if (stateLbl) stateLbl.textContent = enableInput.checked ? 'On' : 'Off';
        const targetSlot = slotSelect.value;
        let newOrder = w.order ?? (index + 1) * 10;
        if (targetSlot !== regionKey) {
          const targetCount = latestWidgets.filter((item) => (item.slot || 'right').toLowerCase() === targetSlot).length;
          newOrder = (targetCount + 1) * 10;
        }

        if (showStartTextInput && startTextInput) {
          startTextInput.disabled = !showStartTextInput.checked;
          startTextInput.parentElement.style.opacity = showStartTextInput.checked ? '1' : '0.4';
        }

        window.winzen.send('update_widget_state', {
          id: w.id,
          enabled: enableInput.checked,
          slot: targetSlot,
          order: newOrder,
          width: parseInt(widthInput.value, 10) || 160,
          autoWidth: autoWidthInput.checked,
          showAppName: showAppNameInput ? showAppNameInput.checked : (w.showAppName !== false),
          combineApps: combineAppsInput ? combineAppsInput.checked : !!w.combineApps,
          startIcon: startIconSelect ? startIconSelect.value : (w.startIcon || 'windows'),
          startText: startTextInput ? startTextInput.value : (w.startText || 'Start'),
          showStartText: showStartTextInput ? showStartTextInput.checked : !!w.showStartText
        });
      };

      enableInput.addEventListener('change', pushUpdate);
      slotSelect.addEventListener('change', pushUpdate);
      autoWidthInput.addEventListener('change', pushUpdate);
      widthInput.addEventListener('change', pushUpdate);

      if (showAppNameInput) showAppNameInput.addEventListener('change', pushUpdate);
      if (combineAppsInput) combineAppsInput.addEventListener('change', pushUpdate);
      if (startIconSelect) startIconSelect.addEventListener('change', pushUpdate);
      if (showStartTextInput) showStartTextInput.addEventListener('change', pushUpdate);
      if (startTextInput) startTextInput.addEventListener('change', pushUpdate);

      container.appendChild(expander);
    });
  }

  function swapWidgetOrderInRegion(regionList, fromIdx, toIdx, regionKey) {
    if (toIdx < 0 || toIdx >= regionList.length) return;
    const copy = regionList.slice();
    const [moved] = copy.splice(fromIdx, 1);
    copy.splice(toIdx, 0, moved);

    const updates = copy.map((item, idx) => ({
      id: item.id,
      slot: regionKey,
      order: (idx + 1) * 10
    }));

    window.winzen.send('reorder_widgets', { items: updates });
  }

  function moveWidgetToRegion(widgetId, targetRegion) {
    const widget = latestWidgets.find((w) => w.id === widgetId);
    if (!widget) return;

    const targetList = latestWidgets
      .filter((w) => (w.slot || 'right').toLowerCase() === targetRegion && w.id !== widgetId)
      .sort((a, b) => (a.order ?? 10) - (b.order ?? 10));

    targetList.push(widget);

    const updates = targetList.map((item, idx) => ({
      id: item.id,
      slot: targetRegion,
      order: (idx + 1) * 10
    }));

    window.winzen.send('reorder_widgets', { items: updates });
  }

  function escapeHtml(str) {
    return String(str ?? '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  window.winzen.send('request_state');
});
