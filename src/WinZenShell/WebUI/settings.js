function initSettingsPage() {
  const navItems = document.querySelectorAll('.pane-item');
  const tabViews = document.querySelectorAll('.tab-view');
  const breadcrumbCurrent = document.getElementById('breadcrumbCurrent');

  const tabTitles = {
    'tab-shell': 'Taskbar',
    'tab-widgets': 'Widgets & Layout',
    'tab-control-center': 'Zen Control Center',
    'tab-sdk': 'Developer SDK'
  };

  function switchTab(targetId) {
    if (!tabTitles[targetId]) return;
    navItems.forEach((b) => b.classList.toggle('active', b.dataset.tab === targetId));
    tabViews.forEach((p) => p.classList.toggle('active', p.id === targetId));
    if (breadcrumbCurrent) breadcrumbCurrent.textContent = tabTitles[targetId] || 'Taskbar';
    try {
      localStorage.setItem('winzen_active_tab', targetId);
    } catch { }
  }
  window.switchTab = switchTab;

  navItems.forEach((btn) => {
    btn.addEventListener('click', () => {
      switchTab(btn.dataset.tab);
    });
  });

  const savedTab = localStorage.getItem('winzen_active_tab') || 'tab-widgets';
  if (window.location.hash) {
    const hashTab = window.location.hash.replace('#', '');
    if (tabTitles[hashTab]) switchTab(hashTab);
    else switchTab(savedTab);
  } else {
    switchTab(savedTab);
  }

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

  const toggleShowThumbnails = document.getElementById('toggleShowThumbnails');
  const stateShowThumbnails = document.getElementById('stateShowThumbnails');

  const toggleEnableAnimations = document.getElementById('toggleEnableAnimations');
  const stateEnableAnimations = document.getElementById('stateEnableAnimations');

  const hiddenAppsContainer = document.getElementById('hiddenAppsContainer');
  const hiddenAppsSubtitle = document.getElementById('hiddenAppsSubtitle');
  const btnUnhideAllApps = document.getElementById('btnUnhideAllApps');

  const toggleRespectWorkArea = document.getElementById('toggleRespectWorkArea');
  const stateRespectWorkArea = document.getElementById('stateRespectWorkArea');

  const selectDockEdge = document.getElementById('selectDockEdge');
  const selectTheme = document.getElementById('selectTheme');
  const inputTaskbarHeight = document.getElementById('inputTaskbarHeight');
  const labelHeightVal = document.getElementById('labelHeightVal');
  const inputFloatingMargin = document.getElementById('inputFloatingMargin');
  const labelMarginVal = document.getElementById('labelMarginVal');
  const inputCornerRadius = document.getElementById('inputCornerRadius');
  const labelRadiusVal = document.getElementById('labelRadiusVal');
  const accentSwatchesContainer = document.getElementById('accentSwatchesContainer');
  let currentAccentColor = '#3684E3';

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
  const previewChipsUnused = document.getElementById('previewChipsUnused');

  const badgePreviewLeft = document.getElementById('badgePreviewLeft');
  const badgePreviewCenter = document.getElementById('badgePreviewCenter');
  const badgePreviewRight = document.getElementById('badgePreviewRight');
  const badgePreviewUnused = document.getElementById('badgePreviewUnused');

  const widgetsRegionLeft = document.getElementById('widgetsRegionLeft');
  const widgetsRegionCenter = document.getElementById('widgetsRegionCenter');
  const widgetsRegionRight = document.getElementById('widgetsRegionRight');
  const widgetsRegionUnused = document.getElementById('widgetsRegionUnused');

  const countRegionLeft = document.getElementById('countRegionLeft');
  const countRegionCenter = document.getElementById('countRegionCenter');
  const countRegionRight = document.getElementById('countRegionRight');
  const countRegionUnused = document.getElementById('countRegionUnused');

  // Control Center Settings Elements
  const ccSettingsPreviewGrid = document.getElementById('ccSettingsPreviewGrid');
  const ccSettingsModulesList = document.getElementById('ccSettingsModulesList');
  const selectAddCcWidget = document.getElementById('selectAddCcWidget');
  const selectAddCcAspect = document.getElementById('selectAddCcAspect');
  const btnAddModuleToCc = document.getElementById('btnAddModuleToCc');
  let latestCcWidgets = [];

  let isUpdatingFromHost = false;
  let latestWidgets = [];

  function sendWorkAreaConfig() {
    if (isUpdatingFromHost) return;
    stateRespectWorkArea.textContent = toggleRespectWorkArea.checked ? 'On' : 'Off';
    const parsedHeight = parseInt(inputTaskbarHeight.value, 10);
    const parsedMargin = parseInt(inputFloatingMargin.value, 10);
    const parsedRadius = inputCornerRadius ? parseInt(inputCornerRadius.value, 10) : 12;
    window.winzen.send('update_workarea_config', {
      respectWorkArea: toggleRespectWorkArea.checked,
      dockEdge: selectDockEdge.value,
      taskbarHeightPx: Number.isNaN(parsedHeight) ? 48 : parsedHeight,
      floatingMarginPx: Number.isNaN(parsedMargin) ? 0 : parsedMargin,
      taskbarCornerRadiusPx: Number.isNaN(parsedRadius) ? 12 : parsedRadius,
      accentColor: currentAccentColor,
      theme: selectTheme ? selectTheme.value : 'dark'
    });
  }

  if (selectTheme) {
    selectTheme.addEventListener('change', () => {
      if (isUpdatingFromHost) return;
      window.winzen.shell.setTheme(selectTheme.value);
      document.documentElement.setAttribute('data-theme', selectTheme.value);
      document.body.className = selectTheme.value === 'light' ? 'theme-light' : 'theme-dark';
      updateAccentColorUI(currentAccentColor);
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
      combineApps: false,
      showThumbnails: true
    };

    window.winzen.send('update_widget_state', {
      id: activeWinWidget.id,
      enabled: activeWinWidget.enabled !== false,
      slot: activeWinWidget.slot || 'center',
      order: activeWinWidget.order ?? 10,
      width: activeWinWidget.width || 420,
      autoWidth: activeWinWidget.autoWidth !== false,
      showAppName: patch.showAppName !== undefined ? patch.showAppName : (activeWinWidget.showAppName !== false),
      combineApps: patch.combineApps !== undefined ? patch.combineApps : !!activeWinWidget.combineApps,
      showThumbnails: patch.showThumbnails !== undefined ? patch.showThumbnails : (activeWinWidget.showThumbnails !== false)
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

  if (toggleShowThumbnails) {
    toggleShowThumbnails.addEventListener('change', () => {
      if (isUpdatingFromHost) return;
      const showThumb = toggleShowThumbnails.checked;
      if (stateShowThumbnails) stateShowThumbnails.textContent = showThumb ? 'On' : 'Off';
      updateActiveWindowsWidgetConfig({ showThumbnails: showThumb });
    });
  }

  if (toggleEnableAnimations) {
    toggleEnableAnimations.addEventListener('change', () => {
      if (isUpdatingFromHost) return;
      const anim = toggleEnableAnimations.checked;
      if (stateEnableAnimations) stateEnableAnimations.textContent = anim ? 'On' : 'Off';
      window.winzen.send('toggle_animations', { enable: anim });
    });
  }

  btnUnhideAllApps.addEventListener('click', () => {
    window.winzen.tasks.unhideAllApps();
  });

  toggleRespectWorkArea.addEventListener('change', sendWorkAreaConfig);
  selectDockEdge.addEventListener('change', sendWorkAreaConfig);

  function updateSliderTrackFill(sliderEl) {
    if (!sliderEl) return;
    const min = parseFloat(sliderEl.min || '0');
    const max = parseFloat(sliderEl.max || '100');
    const val = parseFloat(sliderEl.value || '0');
    const pct = max > min ? Math.max(0, Math.min(100, ((val - min) / (max - min)) * 100)) : 0;
    sliderEl.style.background = `linear-gradient(to right, var(--win11-accent, #60cdff) 0%, var(--win11-accent, #60cdff) ${pct}%, var(--win11-slider-track, rgba(255, 255, 255, 0.42)) ${pct}%, var(--win11-slider-track, rgba(255, 255, 255, 0.42)) 100%)`;
  }

  function bindRangeSlider(sliderEl, labelEl) {
    if (!sliderEl) return;
    updateSliderTrackFill(sliderEl);
    sliderEl.addEventListener('input', () => {
      if (labelEl) labelEl.textContent = `${sliderEl.value}px`;
      updateSliderTrackFill(sliderEl);
    });
    sliderEl.addEventListener('change', sendWorkAreaConfig);
  }

  bindRangeSlider(inputTaskbarHeight, labelHeightVal);
  bindRangeSlider(inputFloatingMargin, labelMarginVal);
  bindRangeSlider(inputCornerRadius, labelRadiusVal);

  function updateAccentColorUI(color) {
    currentAccentColor = color || '#3684E3';
    document.documentElement.style.setProperty('--win11-accent', currentAccentColor);
    document.documentElement.style.setProperty('--winzen-accent', currentAccentColor);
    document.documentElement.style.setProperty('--winzen-border-accent', currentAccentColor);
    updateSliderTrackFill(inputTaskbarHeight);
    updateSliderTrackFill(inputFloatingMargin);
    updateSliderTrackFill(inputCornerRadius);
    if (accentSwatchesContainer) {
      accentSwatchesContainer.querySelectorAll('.accent-swatch').forEach(btn => {
        const btnColor = btn.dataset.color;
        if (btnColor && btnColor.toLowerCase() === currentAccentColor.toLowerCase()) {
          btn.classList.add('active');
        } else {
          btn.classList.remove('active');
        }
      });
    }
  }

  if (accentSwatchesContainer) {
    accentSwatchesContainer.querySelectorAll('.accent-swatch').forEach(btn => {
      btn.addEventListener('click', () => {
        const color = btn.dataset.color;
        if (!color) return;
        updateAccentColorUI(color);
        window.winzen.shell.setAccentColor(color);
      });
    });
  }

  document.getElementById('btnEmergencySidebar').addEventListener('click', () => {
    window.winzen.shell.emergencyRestore();
  });

  document.getElementById('btnQuitSidebar').addEventListener('click', () => {
    window.winzen.shell.quit();
  });

  document.getElementById('btnQuitTop').addEventListener('click', () => {
    window.winzen.shell.quit();
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


  function applyStateUpdate(data) {
    if (!data) return;

    isUpdatingFromHost = true;
    try {
      let bannerText = data.statusBanner || 'Ready';
      if (bannerText.length > 22) {
        bannerText = 'Saved';
      }
      statusBannerText.innerHTML = `<span class="status-dot"></span> <span>${escapeHtml(bannerText)}</span>`;
      if (window._statusResetTimer) clearTimeout(window._statusResetTimer);
      if (bannerText !== 'Ready') {
        window._statusResetTimer = setTimeout(() => {
          statusBannerText.innerHTML = `<span class="status-dot"></span> <span>Ready</span>`;
        }, 3000);
      }

      const s = data.settings || {};
      toggleHideNative.checked = !!s.hideNativeTaskbar;
      stateHideNative.textContent = s.hideNativeTaskbar ? 'On' : 'Off';

      toggleWinZenTaskbar.checked = !!s.enableWinZenTaskbar;
      stateWinZenTaskbar.textContent = s.enableWinZenTaskbar ? 'On' : 'Off';

      toggleRespectWorkArea.checked = !!s.respectDesktopWorkArea;
      stateRespectWorkArea.textContent = s.respectDesktopWorkArea ? 'On' : 'Off';

      if (toggleEnableAnimations) {
        const animOn = s.enableAnimations !== false;
        toggleEnableAnimations.checked = animOn;
        if (stateEnableAnimations) stateEnableAnimations.textContent = animOn ? 'On' : 'Off';
      }

      selectDockEdge.value = s.dockEdge || 'Bottom';
      if (selectTheme) {
        selectTheme.value = s.theme || 'dark';
        document.documentElement.setAttribute('data-theme', selectTheme.value);
        document.body.className = selectTheme.value === 'light' ? 'theme-light' : 'theme-dark';
      }
      inputTaskbarHeight.value = s.taskbarHeightPx || 48;
      labelHeightVal.textContent = `${s.taskbarHeightPx || 48}px`;
      updateSliderTrackFill(inputTaskbarHeight);
      inputFloatingMargin.value = s.floatingMarginPx || 0;
      labelMarginVal.textContent = `${s.floatingMarginPx || 0}px`;
      updateSliderTrackFill(inputFloatingMargin);
      if (inputCornerRadius) {
        inputCornerRadius.value = s.taskbarCornerRadiusPx ?? 12;
        if (labelRadiusVal) labelRadiusVal.textContent = `${s.taskbarCornerRadiusPx ?? 12}px`;
        updateSliderTrackFill(inputCornerRadius);
      }
      if (s.accentColor) {
        updateAccentColorUI(s.accentColor);
      }

      latestWidgets = (data.widgets || []).slice().sort((a, b) => (a.order ?? 10) - (b.order ?? 10));
      const activeWinWidget = latestWidgets.find((w) => w.id === 'community.zen-active-windows');
      const showLabels = activeWinWidget ? activeWinWidget.showAppName !== false : true;
      const combine = activeWinWidget ? !!activeWinWidget.combineApps : false;
      const showThumbs = activeWinWidget ? activeWinWidget.showThumbnails !== false : true;

      toggleShowAppNames.checked = showLabels;
      stateShowAppNames.textContent = showLabels ? 'On' : 'Off';

      toggleCombineApps.checked = combine;
      stateCombineApps.textContent = combine ? 'On' : 'Off';

      if (toggleShowThumbnails) {
        toggleShowThumbnails.checked = showThumbs;
        if (stateShowThumbnails) stateShowThumbnails.textContent = showThumbs ? 'On' : 'Off';
      }

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
      latestCcWidgets = (data.controlCenterWidgets || []).slice().sort((a, b) => (a.order ?? 10) - (b.order ?? 10));
      renderControlCenterSettings(latestCcWidgets, latestWidgets);
    } finally {
      isUpdatingFromHost = false;
    }
  }

  window.addEventListener('winzen:state', (e) => {
    applyStateUpdate(e.detail);
  });

  function renderHiddenApps(hiddenList) {
    hiddenAppsContainer.innerHTML = '';
    if (!Array.isArray(hiddenList) || hiddenList.length === 0) {
      hiddenAppsSubtitle.textContent =
        'No hidden apps. Right-click any app in the Active Window Switcher on the taskbar and select "Hide app".';
      btnUnhideAllApps.style.display = 'none';
      return;
    }

    // Deduplicate by appId or displayName
    const unique = [];
    const seen = new Set();
    hiddenList.forEach((item) => {
      let id = (item.appId || item.processName || '').toLowerCase();
      const label = (item.displayName || item.processName || '').trim();
      if (id.includes('notes') || id.includes('sticky') || label.toLowerCase().includes('sticky')) {
        id = 'microsoft.notes';
      }
      if (!seen.has(id)) {
        seen.add(id);
        unique.push({
          ...item,
          appId: id,
          displayName: id === 'microsoft.notes' ? 'Sticky Notes' : label
        });
      }
    });

    hiddenAppsSubtitle.textContent = `${unique.length} application(s) hidden. Click below to unhide all windows of that application:`;
    btnUnhideAllApps.style.display = 'inline-flex';

    unique.forEach((item) => {
      const chip = document.createElement('button');
      chip.className = 'hidden-app-chip';
      const label = item.displayName || item.processName;
      chip.title = `Click to unhide ${label}`;
      chip.innerHTML = `
        <span>${escapeHtml(label)}</span>
        <span class="unhide-tag">Unhide</span>
      `;
      chip.addEventListener('click', () => {
        window.winzen.tasks.unhideApp(item.appId);
      });
      hiddenAppsContainer.appendChild(chip);
    });
  }

  let isPreviewDragging = false;

  function renderWidgetsByRegion(widgets) {
    if (isPreviewDragging) return;
    const groups = {
      left: [],
      center: [],
      right: [],
      unused: []
    };

    widgets.forEach((w) => {
      const isUnused = !w.enabled || (w.slot || '').toLowerCase() === 'unused' || (w.slot || '').toLowerCase() === 'disabled';
      if (isUnused) {
        groups.unused.push(w);
      } else {
        const slot = (w.slot || 'right').toLowerCase();
        if (slot === 'left') groups.left.push(w);
        else if (slot === 'center') groups.center.push(w);
        else groups.right.push(w);
      }
    });

    ['left', 'center', 'right', 'unused'].forEach((r) => {
      groups[r].sort((a, b) => (a.order ?? 10) - (b.order ?? 10));
    });

    // Update top visual preview bar
    if (previewChipsLeft) renderPreviewChips(previewChipsLeft, groups.left);
    if (previewChipsCenter) renderPreviewChips(previewChipsCenter, groups.center);
    if (previewChipsRight) renderPreviewChips(previewChipsRight, groups.right);
    if (previewChipsUnused) renderPreviewChips(previewChipsUnused, groups.unused);

    // Update badges
    if (badgePreviewLeft) badgePreviewLeft.textContent = groups.left.length;
    if (badgePreviewCenter) badgePreviewCenter.textContent = groups.center.length;
    if (badgePreviewRight) badgePreviewRight.textContent = groups.right.length;
    if (badgePreviewUnused) badgePreviewUnused.textContent = groups.unused.length;

    // Update region counts
    if (countRegionLeft) countRegionLeft.textContent = `${groups.left.length} widget${groups.left.length === 1 ? '' : 's'}`;
    if (countRegionCenter) countRegionCenter.textContent = `${groups.center.length} widget${groups.center.length === 1 ? '' : 's'}`;
    if (countRegionRight) countRegionRight.textContent = `${groups.right.length} widget${groups.right.length === 1 ? '' : 's'}`;
    if (countRegionUnused) countRegionUnused.textContent = `${groups.unused.length} widget${groups.unused.length === 1 ? '' : 's'}`;

    // Render separated region lists
    if (widgetsRegionLeft) renderRegionStack(widgetsRegionLeft, groups.left, 'left');
    if (widgetsRegionCenter) renderRegionStack(widgetsRegionCenter, groups.center, 'center');
    if (widgetsRegionRight) renderRegionStack(widgetsRegionRight, groups.right, 'right');
    if (widgetsRegionUnused) renderRegionStack(widgetsRegionUnused, groups.unused, 'unused');
  }

  function renderPreviewChips(container, list) {
    container.innerHTML = '';
    const regionKey = container.dataset.region || 'center';

    if (list.length === 0) {
      container.innerHTML = `<span class="rp-chip empty-placeholder">Empty</span>`;
      return;
    }

    list.forEach((w, idx) => {
      const isUnused = regionKey === 'unused' || !w.enabled;
      const chip = document.createElement('div');
      chip.className = `rp-chip ${isUnused ? 'disabled' : ''}`;
      chip.dataset.widgetId = w.id;
      chip.dataset.widgetName = w.name;
      chip.dataset.region = regionKey;

      const orderText = isUnused ? '—' : `#${idx + 1}`;
      const badgeText = isUnused ? 'Inactive' : (w.displayMode === 'icon' ? 'Icon' : (w.displayMode === 'compact' ? 'Compact' : 'Active'));

      chip.innerHTML = `
        <div class="rp-chip-left">
          <span class="rp-chip-grip">⋮⋮</span>
          <span class="rp-chip-order">${orderText}</span>
          <span class="rp-chip-name" title="${escapeHtml(w.name)}">${escapeHtml(w.name)}</span>
        </div>
        <div class="rp-chip-right">
          <span class="rp-chip-badge">${badgeText}</span>
        </div>
      `;
      attachPreviewChipDrag(chip, w);
      container.appendChild(chip);
    });
  }

  function attachPreviewChipDrag(chip, w) {
    chip.addEventListener('pointerdown', (e) => {
      if (e.button !== 0) return;
      e.preventDefault();

      const chipRect = chip.getBoundingClientRect();
      const startX = e.clientX;
      const startY = e.clientY;
      const ghostOffsetX = e.clientX - chipRect.left;
      const ghostOffsetY = e.clientY - chipRect.top;

      let dragStarted = false;
      let dragGhost = null;
      let dragPlaceholder = null;

      const zones = [
        { key: 'left', container: previewChipsLeft, zoneEl: previewChipsLeft ? previewChipsLeft.closest('.rp-zone') : null },
        { key: 'center', container: previewChipsCenter, zoneEl: previewChipsCenter ? previewChipsCenter.closest('.rp-zone') : null },
        { key: 'right', container: previewChipsRight, zoneEl: previewChipsRight ? previewChipsRight.closest('.rp-zone') : null },
        { key: 'unused', container: previewChipsUnused, zoneEl: previewChipsUnused ? previewChipsUnused.closest('.rp-zone') : null }
      ];

      const onPointerMove = (moveEv) => {
        const dx = moveEv.clientX - startX;
        const dy = moveEv.clientY - startY;

        if (!dragStarted) {
          if (Math.hypot(dx, dy) < 4) return;
          dragStarted = true;
          isPreviewDragging = true;
          document.body.classList.add('is-dragging-preview-chip');

          dragGhost = document.createElement('div');
          dragGhost.className = 'rp-chip rp-chip-ghost';
          dragGhost.innerHTML = chip.innerHTML;
          dragGhost.style.width = `${chipRect.width}px`;
          dragGhost.style.height = `${chipRect.height}px`;
          dragGhost.style.left = `${moveEv.clientX - ghostOffsetX}px`;
          dragGhost.style.top = `${moveEv.clientY - ghostOffsetY}px`;
          document.body.appendChild(dragGhost);

          dragPlaceholder = document.createElement('div');
          dragPlaceholder.className = 'rp-chip-placeholder';
          dragPlaceholder.dataset.widgetId = w.id;
          dragPlaceholder.dataset.widgetName = w.name;
          dragPlaceholder.innerHTML = `<span>Drop here</span>`;
          dragPlaceholder.style.width = '100%';
          dragPlaceholder.style.height = `${chipRect.height}px`;

          chip.classList.add('is-dragging-source');
          chip.parentNode.insertBefore(dragPlaceholder, chip);
          chip.style.display = 'none';
        }

        if (!dragStarted || !dragGhost) return;

        dragGhost.style.left = `${moveEv.clientX - ghostOffsetX}px`;
        dragGhost.style.top = `${moveEv.clientY - ghostOffsetY}px`;

        let targetZone = null;
        for (const z of zones) {
          if (!z.zoneEl) continue;
          const zRect = z.zoneEl.getBoundingClientRect();
          if (
            moveEv.clientX >= zRect.left &&
            moveEv.clientX <= zRect.right &&
            moveEv.clientY >= zRect.top - 40 &&
            moveEv.clientY <= zRect.bottom + 40
          ) {
            targetZone = z;
            break;
          }
        }

        if (!targetZone) {
          let minDist = Infinity;
          for (const z of zones) {
            if (!z.zoneEl) continue;
            const zRect = z.zoneEl.getBoundingClientRect();
            const centerX = (zRect.left + zRect.right) / 2;
            const centerY = (zRect.top + zRect.bottom) / 2;
            const dist = Math.hypot(moveEv.clientX - centerX, moveEv.clientY - centerY);
            if (dist < minDist) {
              minDist = dist;
              targetZone = z;
            }
          }
        }

        if (!targetZone) return;

        zones.forEach((z) => z.zoneEl && z.zoneEl.classList.remove('drag-active'));
        if (targetZone.zoneEl) targetZone.zoneEl.classList.add('drag-active');

        const targetContainer = targetZone.container;
        if (!targetContainer) return;

        const emptyPl = targetContainer.querySelector('.empty-placeholder');
        if (emptyPl) emptyPl.style.display = 'none';

        // Fluid midpoint calculation (vertical for columns, horizontal/vertical for grid)
        const siblingChips = Array.from(targetContainer.querySelectorAll('.rp-chip:not(.is-dragging-source):not(.empty-placeholder)'));
        let insertBeforeNode = null;

        for (const sib of siblingChips) {
          const sRect = sib.getBoundingClientRect();
          if (targetZone.key === 'unused') {
            if (moveEv.clientY < sRect.top + sRect.height * 0.4 ||
               (moveEv.clientY <= sRect.bottom && moveEv.clientX < sRect.left + sRect.width / 2)) {
              insertBeforeNode = sib;
              break;
            }
          } else {
            if (moveEv.clientY < sRect.top + sRect.height / 2) {
              insertBeforeNode = sib;
              break;
            }
          }
        }

        if (insertBeforeNode) {
          targetContainer.insertBefore(dragPlaceholder, insertBeforeNode);
        } else {
          targetContainer.appendChild(dragPlaceholder);
        }

        zones.forEach((z) => {
          if (z.container && z.container !== targetContainer) {
            const remaining = z.container.querySelectorAll('.rp-chip:not(.is-dragging-source):not(.empty-placeholder), .rp-chip-placeholder');
            const ep = z.container.querySelector('.empty-placeholder');
            if (remaining.length === 0) {
              if (ep) ep.style.display = '';
              else z.container.innerHTML = `<span class="rp-chip empty-placeholder">Empty</span>`;
            }
          }
        });

        updateLiveOrderLabels();
      };

      const onPointerUp = () => {
        window.removeEventListener('pointermove', onPointerMove);
        window.removeEventListener('pointerup', onPointerUp);
        window.removeEventListener('pointercancel', onPointerUp);

        document.body.classList.remove('is-dragging-preview-chip');
        zones.forEach((z) => z.zoneEl && z.zoneEl.classList.remove('drag-active'));

        if (!dragStarted) return;

        if (dragGhost && dragGhost.parentNode) {
          dragGhost.parentNode.removeChild(dragGhost);
          dragGhost = null;
        }

        if (dragPlaceholder && dragPlaceholder.parentNode) {
          const parent = dragPlaceholder.parentNode;
          chip.style.display = '';
          chip.classList.remove('is-dragging-source');
          const targetRegion = parent.dataset.region || 'center';
          chip.dataset.region = targetRegion;
          if (targetRegion === 'unused') {
            chip.classList.add('disabled');
          } else {
            chip.classList.remove('disabled');
          }
          parent.insertBefore(chip, dragPlaceholder);
          dragPlaceholder.parentNode.removeChild(dragPlaceholder);
          dragPlaceholder = null;
        } else {
          chip.style.display = '';
          chip.classList.remove('is-dragging-source');
        }

        isPreviewDragging = false;

        zones.forEach((z) => {
          if (!z.container) return;
          const remaining = z.container.querySelectorAll('.rp-chip:not(.empty-placeholder)');
          const ep = z.container.querySelector('.empty-placeholder');
          if (remaining.length === 0) {
            if (ep) ep.style.display = '';
            else z.container.innerHTML = `<span class="rp-chip empty-placeholder">Empty</span>`;
          } else if (ep) {
            ep.style.display = 'none';
          }
        });

        updateLiveOrderLabels();
        commitTaskbarWidgetOrder();
      };

      window.addEventListener('pointermove', onPointerMove);
      window.addEventListener('pointerup', onPointerUp);
      window.addEventListener('pointercancel', onPointerUp);
    });
  }

  function updateLiveOrderLabels() {
    const zones = [
      { key: 'left', container: previewChipsLeft, badge: badgePreviewLeft },
      { key: 'center', container: previewChipsCenter, badge: badgePreviewCenter },
      { key: 'right', container: previewChipsRight, badge: badgePreviewRight },
      { key: 'unused', container: previewChipsUnused, badge: badgePreviewUnused }
    ];

    zones.forEach((z) => {
      if (!z.container) return;
      const items = z.container.querySelectorAll('.rp-chip:not(.empty-placeholder), .rp-chip-placeholder');
      if (z.badge) z.badge.textContent = items.length;

      items.forEach((item, idx) => {
        const orderSpan = item.querySelector('.rp-chip-order');
        if (orderSpan) {
          orderSpan.textContent = z.key === 'unused' ? '—' : `#${idx + 1}`;
        }
        const badgeSpan = item.querySelector('.rp-chip-badge');
        if (badgeSpan && !item.classList.contains('rp-chip-placeholder')) {
          badgeSpan.textContent = z.key === 'unused' ? 'Inactive' : 'Active';
        }
      });
    });
  }

  function commitTaskbarWidgetOrder() {
    const updates = [];
    const zones = [
      { key: 'left', container: previewChipsLeft, enabled: true },
      { key: 'center', container: previewChipsCenter, enabled: true },
      { key: 'right', container: previewChipsRight, enabled: true },
      { key: 'unused', container: previewChipsUnused, enabled: false }
    ];

    zones.forEach((z) => {
      if (!z.container) return;
      const chips = Array.from(z.container.querySelectorAll('.rp-chip:not(.empty-placeholder)'));
      chips.forEach((c, idx) => {
        const wid = c.dataset.widgetId;
        if (!wid) return;
        const newOrder = (idx + 1) * 10;
        updates.push({
          id: wid,
          slot: z.key,
          order: newOrder,
          enabled: z.enabled
        });

        const match = latestWidgets.find((w) => w.id === wid);
        if (match) {
          match.slot = z.key;
          match.order = newOrder;
          match.enabled = z.enabled;
        }
      });
    });

    window.winzen.send('reorder_widgets', { items: updates });

    const leftCount = previewChipsLeft ? previewChipsLeft.querySelectorAll('.rp-chip:not(.empty-placeholder)').length : 0;
    const centerCount = previewChipsCenter ? previewChipsCenter.querySelectorAll('.rp-chip:not(.empty-placeholder)').length : 0;
    const rightCount = previewChipsRight ? previewChipsRight.querySelectorAll('.rp-chip:not(.empty-placeholder)').length : 0;
    const unusedCount = previewChipsUnused ? previewChipsUnused.querySelectorAll('.rp-chip:not(.empty-placeholder)').length : 0;

    if (countRegionLeft) countRegionLeft.textContent = `${leftCount} widget${leftCount === 1 ? '' : 's'}`;
    if (countRegionCenter) countRegionCenter.textContent = `${centerCount} widget${centerCount === 1 ? '' : 's'}`;
    if (countRegionRight) countRegionRight.textContent = `${rightCount} widget${rightCount === 1 ? '' : 's'}`;
    if (countRegionUnused) countRegionUnused.textContent = `${unusedCount} widget${unusedCount === 1 ? '' : 's'}`;

    if (badgePreviewLeft) badgePreviewLeft.textContent = leftCount;
    if (badgePreviewCenter) badgePreviewCenter.textContent = centerCount;
    if (badgePreviewRight) badgePreviewRight.textContent = rightCount;
    if (badgePreviewUnused) badgePreviewUnused.textContent = unusedCount;

    const groups = { left: [], center: [], right: [], unused: [] };
    latestWidgets.forEach((w) => {
      const isUnused = !w.enabled || (w.slot || '').toLowerCase() === 'unused' || (w.slot || '').toLowerCase() === 'disabled';
      if (isUnused) {
        groups.unused.push(w);
      } else {
        const s = (w.slot || 'right').toLowerCase();
        if (s === 'left') groups.left.push(w);
        else if (s === 'center') groups.center.push(w);
        else groups.right.push(w);
      }
    });
    ['left', 'center', 'right', 'unused'].forEach((r) => groups[r].sort((a, b) => (a.order ?? 10) - (b.order ?? 10)));
    if (widgetsRegionLeft) renderRegionStack(widgetsRegionLeft, groups.left, 'left');
    if (widgetsRegionCenter) renderRegionStack(widgetsRegionCenter, groups.center, 'center');
    if (widgetsRegionRight) renderRegionStack(widgetsRegionRight, groups.right, 'right');
    if (widgetsRegionUnused) renderRegionStack(widgetsRegionUnused, groups.unused, 'unused');
  }

  function renderRegionStack(container, regionWidgets, regionKey) {
    container.innerHTML = '';
    const regionName = regionKey === 'unused' ? 'Inactive' : `${regionKey.charAt(0).toUpperCase() + regionKey.slice(1)} region`;

    if (regionWidgets.length === 0) {
      container.innerHTML = `<div class="region-empty">No widgets in ${regionName} — drag a widget here in the preview above or select this region below</div>`;
      return;
    }

    regionWidgets.forEach((w, index) => {
      const isAutoWidth = w.autoWidth !== false;
      const isWindowSwitcher = w.id === 'community.zen-active-windows';
      const isStartLauncher = w.id === 'community.zen-start-button';
      const isBattery = w.id === 'community.zen-battery';
      const isClock = w.id === 'community.zen-clock-pill';
      const isSystemMonitor = w.id === 'community.zen-system-monitor';

      const expander = document.createElement('div');
      expander.className = 'win11-expander widget-expander';
      expander.dataset.widgetId = w.id;

      let specializedOptionsHtml = '';

      if (isStartLauncher) {
        const curIcon = w.startIcon || 'chromeos';
        const curColorMode = w.startIconColorMode || 'text';
        const curText = w.startText || 'Launcher';
        const showStartText = !!w.showStartText;

        specializedOptionsHtml = `
          <div class="subrow-field">
            <label>Launcher icon</label>
            <select class="win11-select w-starticon">
              <option value="chromeos" ${curIcon === 'chromeos' ? 'selected' : ''}>ChromeOS</option>
              <option value="windows" ${curIcon === 'windows' ? 'selected' : ''}>Windows 11</option>
              <option value="win10" ${curIcon === 'win10' ? 'selected' : ''}>Windows 10</option>
              <option value="apple" ${curIcon === 'apple' ? 'selected' : ''}>Apple</option>
            </select>
          </div>
          <div class="subrow-field">
            <label>Icon color</label>
            <select class="win11-select w-starticoncolor">
              <option value="text" ${curColorMode === 'text' ? 'selected' : ''}>Text color</option>
              <option value="accent" ${curColorMode === 'accent' ? 'selected' : ''}>Accent color</option>
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
          <label class="inline-check">
            <input type="checkbox" class="w-showthumbnails" ${w.showThumbnails !== false ? 'checked' : ''} />
            <span>Show window thumbnails</span>
          </label>
        `;
      } else if (isBattery) {
        const curStyle = w.batteryIconStyle || 'win11';
        const curMode = w.batteryPercentMode || 'outside-right';

        specializedOptionsHtml = `
          <div class="subrow-field">
            <label>Battery icon style</label>
            <select class="win11-select w-batteryicon">
              <option value="win11" ${curStyle === 'win11' ? 'selected' : ''}>W11</option>
              <option value="win10" ${curStyle === 'win10' ? 'selected' : ''}>W10</option>
              <option value="macos" ${curStyle === 'macos' ? 'selected' : ''}>M27</option>
            </select>
          </div>
          <div class="subrow-field">
            <label>Percentage position</label>
            <select class="win11-select w-batterypercent">
              <option value="hide" ${curMode === 'hide' || curMode === 'none' ? 'selected' : ''}>None</option>
              <option value="outside-left" ${curMode === 'outside-left' || curMode === 'left' ? 'selected' : ''}>Left</option>
              <option value="outside-right" ${curMode === 'outside-right' || curMode === 'right' ? 'selected' : ''}>Right</option>
            </select>
          </div>
        `;
      } else if (isClock) {
        specializedOptionsHtml = `
          <label class="inline-check">
            <input type="checkbox" class="w-showseconds" ${w.showSeconds !== false ? 'checked' : ''} />
            <span>Show seconds</span>
          </label>
          <label class="inline-check">
            <input type="checkbox" class="w-showdate" ${w.showDate !== false ? 'checked' : ''} />
            <span>Show date</span>
          </label>
        `;
      } else if (isSystemMonitor) {
        specializedOptionsHtml = `
          <label class="inline-check">
            <input type="checkbox" class="w-showcpu" ${w.showCpu !== false ? 'checked' : ''} />
            <span>Show CPU</span>
          </label>
          <label class="inline-check">
            <input type="checkbox" class="w-showram" ${w.showRam !== false ? 'checked' : ''} />
            <span>Show RAM</span>
          </label>
          <label class="inline-check">
            <input type="checkbox" class="w-showgpu" ${w.showGpu !== false ? 'checked' : ''} />
            <span>Show GPU</span>
          </label>
        `;
      }

      const hasMultipleModes = !w.supportedModes || w.supportedModes.length > 1;
      let displayModeHtml = '';
      if (hasMultipleModes) {
        const modes = w.supportedModes && w.supportedModes.length > 0 ? w.supportedModes : ['widget', 'icon'];
        const modeLabels = {
          widget: 'Full Mode',
          compact: 'Compact Mode (1 2 3)',
          icon: 'Icon Mode'
        };
        const options = modes
          .map((m) => {
            const label = modeLabels[m] || (m.charAt(0).toUpperCase() + m.slice(1) + ' Mode');
            const selected = (w.displayMode || 'widget') === m ? 'selected' : '';
            return `<option value="${m}" ${selected}>${label}</option>`;
          })
          .join('');
        displayModeHtml = `
          <div class="subrow-field">
            <label>Display mode</label>
            <select class="win11-select w-displaymode">
              ${options}
            </select>
          </div>
        `;
      }

      const authorName = (w.author || 'Community').trim();
      const isOfficial = authorName.toLowerCase() === 'official';
      const authorTagHtml = `<span class="widget-author-tag ${isOfficial ? 'is-official' : ''}">${escapeHtml(isOfficial ? 'Official' : authorName)}</span>`;

      expander.innerHTML = `
        <div class="win11-card">
          <div class="card-left">
            <div class="card-titles">
              <div class="card-title-row">
                <span class="card-title">${escapeHtml(w.name)}</span>
                ${authorTagHtml}
              </div>
              <span class="card-subtitle">${escapeHtml(w.description || 'Widget')} · Widgets/${escapeHtml(w.folderName)}</span>
            </div>
          </div>
          <div class="card-right">
            <div class="arrange-btns">
              <button class="arrange-btn btn-move-up" title="Move earlier in ${regionName}" ${index === 0 ? 'disabled' : ''}>↑</button>
              <button class="arrange-btn btn-move-down" title="Move later in ${regionName}" ${index === regionWidgets.length - 1 ? 'disabled' : ''}>↓</button>
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
              <option value="unused" ${regionKey === 'unused' ? 'selected' : ''}>Inactive</option>
            </select>
          </div>
          ${displayModeHtml}
          <label class="inline-check">
            <input type="checkbox" class="w-autowidth" ${isAutoWidth ? 'checked' : ''} />
            <span>Dynamic width</span>
          </label>
          <div class="subrow-field" style="${isAutoWidth ? 'opacity:0.4;' : ''}">
            <label>Fixed width</label>
            <input type="number" class="win11-input w-width" min="34" max="640" value="${w.width || 160}" ${isAutoWidth ? 'disabled' : ''} style="width: 80px;" />
          </div>
          ${specializedOptionsHtml}
        </div>
      `;

      const btnMoveUp = expander.querySelector('.btn-move-up');
      const btnMoveDown = expander.querySelector('.btn-move-down');
      const enableInput = expander.querySelector('.w-enable');
      const stateLbl = expander.querySelector('.w-state-lbl');
      const slotSelect = expander.querySelector('.w-slot');
      const displayModeSelect = expander.querySelector('.w-displaymode');
      const autoWidthInput = expander.querySelector('.w-autowidth');
      const widthInput = expander.querySelector('.w-width');

      const showAppNameInput = expander.querySelector('.w-showappname');
      const combineAppsInput = expander.querySelector('.w-combineapps');
      const showThumbnailsInput = expander.querySelector('.w-showthumbnails');
      const startIconSelect = expander.querySelector('.w-starticon');
      const startIconColorSelect = expander.querySelector('.w-starticoncolor');
      const showStartTextInput = expander.querySelector('.w-showstarttext');
      const startTextInput = expander.querySelector('.w-starttext');
      const batteryIconSelect = expander.querySelector('.w-batteryicon');
      const batteryPercentSelect = expander.querySelector('.w-batterypercent');
      const showSecondsInput = expander.querySelector('.w-showseconds');
      const showDateInput = expander.querySelector('.w-showdate');
      const showCpuInput = expander.querySelector('.w-showcpu');
      const showRamInput = expander.querySelector('.w-showram');
      const showGpuInput = expander.querySelector('.w-showgpu');

      btnMoveUp.addEventListener('click', () => {
        swapWidgetOrderInRegion(regionWidgets, index, index - 1, regionKey);
      });

      btnMoveDown.addEventListener('click', () => {
        swapWidgetOrderInRegion(regionWidgets, index, index + 1, regionKey);
      });

      const pushUpdate = () => {
        let isEnabled = enableInput.checked;
        let targetSlot = slotSelect.value;

        if (targetSlot === 'unused') {
          isEnabled = false;
          enableInput.checked = false;
        } else if (!isEnabled && targetSlot !== 'unused' && regionKey === 'unused') {
          isEnabled = true;
          enableInput.checked = true;
        }

        if (stateLbl) stateLbl.textContent = isEnabled ? 'On' : 'Off';

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
          enabled: isEnabled,
          slot: targetSlot,
          order: newOrder,
          width: parseInt(widthInput.value, 10) || 160,
          autoWidth: autoWidthInput.checked,
          showAppName: showAppNameInput ? showAppNameInput.checked : (w.showAppName !== false),
          combineApps: combineAppsInput ? combineAppsInput.checked : !!w.combineApps,
          showThumbnails: showThumbnailsInput ? showThumbnailsInput.checked : (w.showThumbnails !== false),
          startIcon: startIconSelect ? startIconSelect.value : (w.startIcon || 'chromeos'),
          startIconColorMode: startIconColorSelect ? startIconColorSelect.value : (w.startIconColorMode || 'text'),
          startText: startTextInput ? startTextInput.value : (w.startText || 'Start'),
          showStartText: showStartTextInput ? showStartTextInput.checked : !!w.showStartText,
          batteryIconStyle: batteryIconSelect ? batteryIconSelect.value : (w.batteryIconStyle || 'win11'),
          batteryPercentMode: batteryPercentSelect ? batteryPercentSelect.value : (w.batteryPercentMode || 'outside-right'),
          showSeconds: showSecondsInput ? showSecondsInput.checked : (w.showSeconds !== false),
          showDate: showDateInput ? showDateInput.checked : (w.showDate !== false),
          showCpu: showCpuInput ? showCpuInput.checked : (w.showCpu !== false),
          showRam: showRamInput ? showRamInput.checked : (w.showRam !== false),
          showGpu: showGpuInput ? showGpuInput.checked : (w.showGpu !== false),
          displayMode: displayModeSelect ? displayModeSelect.value : (w.displayMode || (w.supportedModes && w.supportedModes[0] ? w.supportedModes[0] : 'widget')),
          aspectRatio: w.aspectRatio || '1:1'
        });
      };

      enableInput.addEventListener('change', pushUpdate);
      slotSelect.addEventListener('change', pushUpdate);
      if (displayModeSelect) displayModeSelect.addEventListener('change', pushUpdate);
      autoWidthInput.addEventListener('change', pushUpdate);
      widthInput.addEventListener('change', pushUpdate);

      if (showAppNameInput) showAppNameInput.addEventListener('change', pushUpdate);
      if (combineAppsInput) combineAppsInput.addEventListener('change', pushUpdate);
      if (showThumbnailsInput) showThumbnailsInput.addEventListener('change', pushUpdate);
      if (startIconSelect) startIconSelect.addEventListener('change', pushUpdate);
      if (startIconColorSelect) startIconColorSelect.addEventListener('change', pushUpdate);
      if (showStartTextInput) showStartTextInput.addEventListener('change', pushUpdate);
      if (startTextInput) startTextInput.addEventListener('change', pushUpdate);
      if (batteryIconSelect) batteryIconSelect.addEventListener('change', pushUpdate);
      if (batteryPercentSelect) batteryPercentSelect.addEventListener('change', pushUpdate);
      if (showSecondsInput) showSecondsInput.addEventListener('change', pushUpdate);
      if (showDateInput) showDateInput.addEventListener('change', pushUpdate);
      if (showCpuInput) showCpuInput.addEventListener('change', pushUpdate);
      if (showRamInput) showRamInput.addEventListener('change', pushUpdate);
      if (showGpuInput) showGpuInput.addEventListener('change', pushUpdate);

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
      order: (idx + 1) * 10,
      enabled: regionKey !== 'unused'
    }));

    window.winzen.send('reorder_widgets', { items: updates });
  }

  function renderControlCenterSettings(ccWidgets, allWidgets) {
    if (!ccSettingsPreviewGrid || !ccSettingsModulesList) return;

    // 1. Grid Preview
    ccSettingsPreviewGrid.innerHTML = '';
    if (ccWidgets.length === 0) {
      ccSettingsPreviewGrid.innerHTML = '<div style="color:#888;font-size:12px;grid-column:span 2;padding:12px;text-align:center;">No active modules in Control Center</div>';
    } else {
      ccWidgets.forEach((m) => {
        const tile = document.createElement('div');
        tile.className = 'cc-preview-tile';
        tile.dataset.aspect = m.aspectRatio || '1:1';
        tile.innerHTML = `
          <span class="cc-preview-title">${escapeHtml(m.name || m.id)}</span>
          <span class="cc-preview-ratio">${m.aspectRatio || '1:1'}</span>
        `;
        ccSettingsPreviewGrid.appendChild(tile);
      });
    }

    // 2. Active Modules list
    ccSettingsModulesList.innerHTML = '';
    if (ccWidgets.length === 0) {
      ccSettingsModulesList.innerHTML = '<div class="region-empty">No modules configured in Control Center</div>';
    } else {
      ccWidgets.forEach((m, idx) => {
        const row = document.createElement('div');
        row.className = 'cc-module-row';
        row.dataset.id = m.id;
        row.innerHTML = `
          <div class="cc-module-info">
            <span class="cc-module-name">${escapeHtml(m.name || m.id)}</span>
          </div>
          <div class="cc-module-controls">
            <select class="win11-select cc-mod-aspect">
              <option value="1:1" ${(m.aspectRatio || '1:1') === '1:1' ? 'selected' : ''}>1:1 (Square)</option>
              <option value="1:2" ${m.aspectRatio === '1:2' ? 'selected' : ''}>1:2 (Wide Slider)</option>
            </select>
            <div class="arrange-btns">
              <button class="arrange-btn btn-cc-up" ${idx === 0 ? 'disabled' : ''}>↑</button>
              <button class="arrange-btn btn-cc-down" ${idx === ccWidgets.length - 1 ? 'disabled' : ''}>↓</button>
            </div>
            <button class="arrange-btn btn-cc-del" title="Remove module" style="color: #ff99a4;">✕</button>
          </div>
        `;

        const aspectSelect = row.querySelector('.cc-mod-aspect');
        aspectSelect.addEventListener('change', () => {
          const updated = ccWidgets.map((item) => ({
            id: item.id,
            order: item.order,
            aspectRatio: item.id === m.id ? aspectSelect.value : (item.aspectRatio || '1:1'),
            enabled: item.enabled !== false
          }));
          window.winzen.send('update_control_center_widgets', { items: updated });
        });

        const btnUp = row.querySelector('.btn-cc-up');
        btnUp.addEventListener('click', () => {
          if (idx > 0) {
            const listCopy = ccWidgets.slice();
            const temp = listCopy[idx - 1];
            listCopy[idx - 1] = listCopy[idx];
            listCopy[idx] = temp;
            const updated = listCopy.map((item, i) => ({
              id: item.id,
              order: (i + 1) * 10,
              aspectRatio: item.aspectRatio || '1:1',
              enabled: item.enabled !== false
            }));
            window.winzen.send('update_control_center_widgets', { items: updated });
          }
        });

        const btnDown = row.querySelector('.btn-cc-down');
        btnDown.addEventListener('click', () => {
          if (idx < ccWidgets.length - 1) {
            const listCopy = ccWidgets.slice();
            const temp = listCopy[idx + 1];
            listCopy[idx + 1] = listCopy[idx];
            listCopy[idx] = temp;
            const updated = listCopy.map((item, i) => ({
              id: item.id,
              order: (i + 1) * 10,
              aspectRatio: item.aspectRatio || '1:1',
              enabled: item.enabled !== false
            }));
            window.winzen.send('update_control_center_widgets', { items: updated });
          }
        });

        const btnDel = row.querySelector('.btn-cc-del');
        btnDel.addEventListener('click', () => {
          const filtered = ccWidgets.filter((item) => item.id !== m.id);
          const updated = filtered.map((item, i) => ({
            id: item.id,
            order: (i + 1) * 10,
            aspectRatio: item.aspectRatio || '1:1',
            enabled: item.enabled !== false
          }));
          window.winzen.send('update_control_center_widgets', { items: updated });
        });

        ccSettingsModulesList.appendChild(row);
      });
    }

    // 3. Populate Add Select
    if (selectAddCcWidget) {
      selectAddCcWidget.innerHTML = '<option value="">Select widget to add...</option>';
      const currentIds = new Set(ccWidgets.map((w) => w.id));
      (allWidgets || []).forEach((w) => {
        if (!currentIds.has(w.id) && !w.id.includes('zen-control-center')) {
          const opt = document.createElement('option');
          opt.value = w.id;
          opt.textContent = w.name || w.id;
          selectAddCcWidget.appendChild(opt);
        }
      });
    }
  }

  if (btnAddModuleToCc) {
    btnAddModuleToCc.addEventListener('click', () => {
      const selectedId = selectAddCcWidget.value;
      if (!selectedId) return;
      const aspect = selectAddCcAspect ? selectAddCcAspect.value : '1:2';
      const updated = latestCcWidgets.map((item, i) => ({
        id: item.id,
        order: (i + 1) * 10,
        aspectRatio: item.aspectRatio || '1:1',
        enabled: item.enabled !== false
      }));
      updated.push({
        id: selectedId,
        order: (updated.length + 1) * 10,
        aspectRatio: aspect,
        enabled: true
      });
      window.winzen.send('update_control_center_widgets', { items: updated });
    });
  }

  function escapeHtml(str) {
    return String(str ?? '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  if (window.winzen && window.winzen.state) {
    applyStateUpdate(window.winzen.state);
  }
  if (window.winzen && typeof window.winzen.send === 'function') {
    window.winzen.send('request_state');
  }
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initSettingsPage);
} else {
  initSettingsPage();
}
