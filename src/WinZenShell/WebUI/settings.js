function initSettingsPage() {
  const navItems = document.querySelectorAll('.pane-item');
  const tabViews = document.querySelectorAll('.tab-view');
  const breadcrumbCurrent = document.getElementById('breadcrumbCurrent');

  const tabTitles = {
    'tab-shell': 'Shell',
    'tab-widgets': 'Layouts'
  };

  function switchTab(targetId) {
    if (targetId === 'tab-control-center') targetId = 'tab-widgets';
    if (!tabTitles[targetId]) return;
    navItems.forEach((b) => b.classList.toggle('active', b.dataset.tab === targetId));
    tabViews.forEach((p) => p.classList.toggle('active', p.id === targetId));
    if (breadcrumbCurrent) breadcrumbCurrent.textContent = tabTitles[targetId] || 'Shell';
    const sp = document.querySelector('.settings-page');
    if (sp) sp.scrollTop = 0;
    window.scrollTo(0, 0);
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

  const inputAppIconSize = document.getElementById('inputAppIconSize');
  const labelAppIconSizeVal = document.getElementById('labelAppIconSizeVal');
  const btnResetAppIconSize = document.getElementById('btnResetAppIconSize');

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
  const inputBgOpacity = document.getElementById('inputBgOpacity');
  const labelBgOpacityVal = document.getElementById('labelBgOpacityVal');
  const inputBgBlur = document.getElementById('inputBgBlur');
  const labelBgBlurVal = document.getElementById('labelBgBlurVal');
  const bgBlurField = document.getElementById('bgBlurField');
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
  const ccAvailableWidgetsGrid = document.getElementById('ccAvailableWidgetsGrid');
  let latestCcWidgets = [];

  let isUpdatingFromHost = false;
  let latestWidgets = [];

  function sendWorkAreaConfig() {
    if (isUpdatingFromHost) return;
    stateRespectWorkArea.textContent = toggleRespectWorkArea.checked ? 'On' : 'Off';
    const parsedHeight = parseInt(inputTaskbarHeight.value, 10);
    const parsedMargin = parseInt(inputFloatingMargin.value, 10);
    const parsedRadius = inputCornerRadius ? parseInt(inputCornerRadius.value, 10) : 12;
    const parsedOpacity = inputBgOpacity ? parseInt(inputBgOpacity.value, 10) : 100;
    const parsedBlur = inputBgBlur ? parseInt(inputBgBlur.value, 10) : 60;
    updateBlurFieldEnabledState(Number.isNaN(parsedOpacity) ? 100 : parsedOpacity);
    window.winzen.send('update_workarea_config', {
      respectWorkArea: toggleRespectWorkArea.checked,
      dockEdge: selectDockEdge.value,
      taskbarHeightPx: Number.isNaN(parsedHeight) ? 48 : parsedHeight,
      floatingMarginPx: Number.isNaN(parsedMargin) ? 0 : parsedMargin,
      taskbarCornerRadiusPx: Number.isNaN(parsedRadius) ? 12 : parsedRadius,
      backgroundOpacity: Number.isNaN(parsedOpacity) ? 100 : parsedOpacity,
      backgroundBlurStrength: Number.isNaN(parsedBlur) ? 60 : parsedBlur,
      accentColor: currentAccentColor,
      theme: selectTheme ? selectTheme.value : 'dark'
    });
  }

  function updateBlurFieldEnabledState(opacityVal) {
    if (!inputBgBlur || !bgBlurField) return;
    const isOpaque = opacityVal >= 100;
    inputBgBlur.disabled = isOpaque;
    bgBlurField.style.opacity = isOpaque ? '0.42' : '1';
    bgBlurField.title = isOpaque ? 'Blur is disabled at 100% opacity for efficiency' : '';
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
      showThumbnails: true,
      iconSizePx: 18
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
      showThumbnails: patch.showThumbnails !== undefined ? patch.showThumbnails : (activeWinWidget.showThumbnails !== false),
      iconSizePx: patch.iconSizePx !== undefined ? patch.iconSizePx : (activeWinWidget.iconSizePx ?? 18)
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

  let appIconSizeTimer = null;
  if (inputAppIconSize) {
    inputAppIconSize.addEventListener('input', () => {
      const sz = parseInt(inputAppIconSize.value, 10) || 18;
      if (labelAppIconSizeVal) labelAppIconSizeVal.textContent = `${sz}px`;
      updateSliderTrackFill(inputAppIconSize);
      if (isUpdatingFromHost) return;
      if (appIconSizeTimer) clearTimeout(appIconSizeTimer);
      appIconSizeTimer = setTimeout(() => updateActiveWindowsWidgetConfig({ iconSizePx: sz }), 40);
    });
    inputAppIconSize.addEventListener('change', () => {
      if (isUpdatingFromHost) return;
      const sz = parseInt(inputAppIconSize.value, 10) || 18;
      updateActiveWindowsWidgetConfig({ iconSizePx: sz });
    });
  }

  if (btnResetAppIconSize) {
    btnResetAppIconSize.addEventListener('click', () => {
      if (inputAppIconSize) {
        inputAppIconSize.value = 18;
        updateSliderTrackFill(inputAppIconSize);
      }
      if (labelAppIconSizeVal) labelAppIconSizeVal.textContent = '18px';
      if (!isUpdatingFromHost) {
        updateActiveWindowsWidgetConfig({ iconSizePx: 18 });
      }
    });
  }

  if (toggleEnableAnimations) {
    toggleEnableAnimations.addEventListener('change', () => {
      if (isUpdatingFromHost) return;
      const anim = toggleEnableAnimations.checked;
      if (stateEnableAnimations) stateEnableAnimations.textContent = anim ? 'On' : 'Off';
      document.body.classList.toggle('winzen-no-anim', !anim);
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

  let liveConfigTimer = null;
  function bindRangeSlider(sliderEl, labelEl, suffix = 'px') {
    if (!sliderEl) return;
    updateSliderTrackFill(sliderEl);
    sliderEl.addEventListener('input', () => {
      if (labelEl) labelEl.textContent = `${sliderEl.value}${suffix}`;
      updateSliderTrackFill(sliderEl);
      if (sliderEl === inputBgOpacity) {
        updateBlurFieldEnabledState(parseInt(sliderEl.value, 10));
      }
      if (sliderEl === inputBgOpacity || sliderEl === inputBgBlur) {
        if (liveConfigTimer) clearTimeout(liveConfigTimer);
        liveConfigTimer = setTimeout(sendWorkAreaConfig, 50);
      }
    });
    sliderEl.addEventListener('change', sendWorkAreaConfig);
  }

  bindRangeSlider(inputTaskbarHeight, labelHeightVal, 'px');
  bindRangeSlider(inputFloatingMargin, labelMarginVal, 'px');
  bindRangeSlider(inputCornerRadius, labelRadiusVal, 'px');
  bindRangeSlider(inputBgOpacity, labelBgOpacityVal, '%');
  bindRangeSlider(inputBgBlur, labelBgBlurVal, '%');

  function updateAccentColorUI(color) {
    currentAccentColor = color || '#3684E3';
    document.documentElement.style.setProperty('--win11-accent', currentAccentColor);
    document.documentElement.style.setProperty('--winzen-accent', currentAccentColor);
    document.documentElement.style.setProperty('--winzen-border-accent', currentAccentColor);
    updateSliderTrackFill(inputTaskbarHeight);
    updateSliderTrackFill(inputFloatingMargin);
    updateSliderTrackFill(inputCornerRadius);
    updateSliderTrackFill(inputBgOpacity);
    updateSliderTrackFill(inputBgBlur);
    updateSliderTrackFill(inputAppIconSize);
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

  document.getElementById('btnQuitTop')?.addEventListener('click', () => {
    window.winzen.shell.quit();
  });

  const btnRestart = document.getElementById('btnRestartShell') || document.getElementById('btnRefreshState');
  btnRestart?.addEventListener('click', () => {
    window.winzen.shell.restart();
  });

  document.getElementById('btnOpenWidgetsFolder').addEventListener('click', () => {
    window.winzen.send('open_widgets_folder');
  });

  document.getElementById('btnReloadWidgets').addEventListener('click', () => {
    window.winzen.send('reload_widgets');
  });




  function applyStateUpdate(data) {
    if (!data) return;

    isUpdatingFromHost = true;
    try {
      const s = data.settings || {};
      toggleHideNative.checked = !!s.hideNativeTaskbar;
      stateHideNative.textContent = s.hideNativeTaskbar ? 'On' : 'Off';

      toggleWinZenTaskbar.checked = !!s.enableWinZenTaskbar;
      stateWinZenTaskbar.textContent = s.enableWinZenTaskbar ? 'On' : 'Off';

      toggleRespectWorkArea.checked = !!s.respectDesktopWorkArea;
      stateRespectWorkArea.textContent = s.respectDesktopWorkArea ? 'On' : 'Off';

      const animOn = s.enableAnimations !== false;
      document.body.classList.toggle('winzen-no-anim', !animOn);
      if (toggleEnableAnimations) {
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
      if (inputBgOpacity) {
        const op = s.backgroundOpacity ?? 100;
        inputBgOpacity.value = op;
        if (labelBgOpacityVal) labelBgOpacityVal.textContent = `${op}%`;
        updateSliderTrackFill(inputBgOpacity);
        updateBlurFieldEnabledState(op);
      }
      if (inputBgBlur) {
        const bl = s.backgroundBlurStrength ?? 60;
        inputBgBlur.value = bl;
        if (labelBgBlurVal) labelBgBlurVal.textContent = `${bl}%`;
        updateSliderTrackFill(inputBgBlur);
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
      if (inputAppIconSize) {
        const iconSz = activeWinWidget ? (activeWinWidget.iconSizePx ?? 18) : 18;
        inputAppIconSize.value = iconSz;
        if (labelAppIconSizeVal) labelAppIconSizeVal.textContent = `${iconSz}px`;
        updateSliderTrackFill(inputAppIconSize);
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
        ? 'Reserved (Docked)'
        : 'Floating overlay';

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
  const expandedWidgetIds = new Set();

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

  function renderPreviewChips(container, list) {
    container.innerHTML = '';
    const regionKey = container.dataset.region || 'center';
    const regionName = regionKey === 'unused' ? 'Inactive' : `${regionKey.charAt(0).toUpperCase() + regionKey.slice(1)} region`;

    if (list.length === 0) {
      container.innerHTML = `<span class="rp-chip empty-placeholder">Empty</span>`;
      return;
    }

    list.forEach((w, idx) => {
      const isUnused = regionKey === 'unused' || !w.enabled;
      const isAutoWidth = w.autoWidth !== false;
      const isWindowSwitcher = w.id === 'community.zen-active-windows';
      const isStartLauncher = w.id === 'community.zen-start-button';
      const isBattery = w.id === 'community.zen-battery';
      const isClock = w.id === 'community.zen-clock-pill';
      const isSystemMonitor = w.id === 'community.zen-system-monitor';

      const isExpanded = expandedWidgetIds.has(w.id);

      const chip = document.createElement('div');
      chip.className = `rp-chip ${isUnused ? 'disabled' : ''} ${isExpanded ? 'is-expanded' : ''}`;
      chip.dataset.widgetId = w.id;
      chip.dataset.widgetName = w.name;
      chip.dataset.region = regionKey;

      const authorName = (w.author || 'Community').trim();
      const isOfficial = authorName.toLowerCase() === 'official';
      const authorTagHtml = `<span class="widget-author-tag ${isOfficial ? 'is-official' : ''}">${escapeHtml(isOfficial ? 'Official' : authorName)}</span>`;

      let specializedOptionsHtml = '';

      if (isStartLauncher) {
        const curIcon = w.startIcon || 'chromeos';
        const curColorMode = w.startIconColorMode || 'text';
        const curText = w.startText || 'Launcher';
        const showStartText = !!w.showStartText;

        specializedOptionsHtml = `
          <div class="tc-opt-row tc-opt-row-wrap">
            <div class="tc-field">
              <label>Launcher icon</label>
              <select class="win11-select w-starticon">
                <option value="chromeos" ${curIcon === 'chromeos' ? 'selected' : ''}>ChromeOS</option>
                <option value="windows" ${curIcon === 'windows' ? 'selected' : ''}>Windows 11</option>
                <option value="win10" ${curIcon === 'win10' ? 'selected' : ''}>Windows 10</option>
                <option value="apple" ${curIcon === 'apple' ? 'selected' : ''}>Apple</option>
              </select>
            </div>
            <div class="tc-field">
              <label>Icon color</label>
              <select class="win11-select w-starticoncolor">
                <option value="text" ${curColorMode === 'text' ? 'selected' : ''}>Text color</option>
                <option value="accent" ${curColorMode === 'accent' ? 'selected' : ''}>Accent color</option>
              </select>
            </div>
          </div>
          <div class="tc-width-section">
            <label class="inline-check">
              <input type="checkbox" class="w-showstarttext" ${showStartText ? 'checked' : ''} />
              <span>Show text label</span>
            </label>
            <div class="tc-fixed-width-row tc-start-text-row" style="${!showStartText ? 'opacity:0.4;' : ''}">
              <span class="tc-fixed-width-label">Label text</span>
              <div class="tc-fixed-width-input-wrap">
                <input type="text" class="win11-input w-starttext" value="${escapeHtml(curText)}" placeholder="Start" style="width: 80px;" ${!showStartText ? 'disabled' : ''} />
              </div>
            </div>
          </div>
        `;
      } else if (isWindowSwitcher) {
        specializedOptionsHtml = `
          <div class="tc-opt-checks">
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
          </div>
        `;
      } else if (isBattery) {
        const curStyle = w.batteryIconStyle || 'win11';
        const curMode = w.batteryPercentMode || 'outside-right';

        specializedOptionsHtml = `
          <div class="tc-opt-row tc-opt-row-wrap">
            <div class="tc-field">
              <label>Battery icon style</label>
              <select class="win11-select w-batteryicon">
                <option value="win11" ${curStyle === 'win11' ? 'selected' : ''}>W11</option>
                <option value="win10" ${curStyle === 'win10' ? 'selected' : ''}>W10</option>
                <option value="macos" ${curStyle === 'macos' ? 'selected' : ''}>M27</option>
              </select>
            </div>
            <div class="tc-field">
              <label>Percentage position</label>
              <select class="win11-select w-batterypercent">
                <option value="hide" ${curMode === 'hide' || curMode === 'none' ? 'selected' : ''}>None</option>
                <option value="outside-left" ${curMode === 'outside-left' || curMode === 'left' ? 'selected' : ''}>Left</option>
                <option value="outside-right" ${curMode === 'outside-right' || curMode === 'right' ? 'selected' : ''}>Right</option>
              </select>
            </div>
          </div>
        `;
      } else if (isClock) {
        specializedOptionsHtml = `
          <div class="tc-opt-checks">
            <label class="inline-check">
              <input type="checkbox" class="w-showseconds" ${w.showSeconds !== false ? 'checked' : ''} />
              <span>Show seconds</span>
            </label>
            <label class="inline-check">
              <input type="checkbox" class="w-showdate" ${w.showDate !== false ? 'checked' : ''} />
              <span>Show date</span>
            </label>
          </div>
        `;
      } else if (isSystemMonitor) {
        specializedOptionsHtml = `
          <div class="tc-opt-checks">
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
          </div>
        `;
      }

      const hasMultipleModes = !w.supportedModes || w.supportedModes.length > 1;
      let displayModeHtml = '';
      if (hasMultipleModes) {
        const modes = w.supportedModes && w.supportedModes.length > 0 ? w.supportedModes : ['widget', 'icon'];
        const modeLabels = {
          widget: 'Full Mode',
          compact: 'Compact Mode',
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
          <div class="tc-field">
            <label>Display mode</label>
            <select class="win11-select w-displaymode">
              ${options}
            </select>
          </div>
        `;
      }

      chip.innerHTML = `
        <div class="tc-card-header">
          <div class="tc-header-info">
            <span class="tc-widget-name" title="${escapeHtml(w.name)}">${escapeHtml(w.name)}</span>
            ${authorTagHtml ? `<div class="tc-tag-row">${authorTagHtml}</div>` : ''}
          </div>
          <button type="button" class="tc-collapse-btn" title="${isExpanded ? 'Collapse options' : 'Expand options'}" aria-label="Toggle options">
            <svg class="tc-chevron" viewBox="0 0 16 16" width="12" height="12" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <polyline points="4 6 8 10 12 6"></polyline>
            </svg>
          </button>
        </div>

        <div class="tc-card-body">
          <div class="tc-card-sub" title="${escapeHtml(w.description || '')}">
            ${escapeHtml(w.description || 'Widget')} · Widgets/${escapeHtml(w.folderName || '')}
          </div>

          <div class="tc-card-options tc-no-drag">
            ${displayModeHtml ? `<div class="tc-opt-row">${displayModeHtml}</div>` : ''}

            <div class="tc-width-section">
              <label class="inline-check">
                <input type="checkbox" class="w-autowidth" ${isAutoWidth ? 'checked' : ''} />
                <span>Dynamic width</span>
              </label>
              <div class="tc-fixed-width-row" style="${isAutoWidth ? 'opacity:0.4;' : ''}">
                <span class="tc-fixed-width-label">Fixed width</span>
                <div class="tc-fixed-width-input-wrap">
                  <input type="number" class="win11-input w-width" min="34" max="640" value="${w.width || 160}" ${isAutoWidth ? 'disabled' : ''} />
                  <span class="tc-fixed-width-unit">px</span>
                </div>
              </div>
            </div>

            ${specializedOptionsHtml}
          </div>
        </div>
      `;

      const displayModeSelect = chip.querySelector('.w-displaymode');
      const autoWidthInput = chip.querySelector('.w-autowidth');
      const widthInput = chip.querySelector('.w-width');

      const showAppNameInput = chip.querySelector('.w-showappname');
      const combineAppsInput = chip.querySelector('.w-combineapps');
      const showThumbnailsInput = chip.querySelector('.w-showthumbnails');
      const startIconSelect = chip.querySelector('.w-starticon');
      const startIconColorSelect = chip.querySelector('.w-starticoncolor');
      const showStartTextInput = chip.querySelector('.w-showstarttext');
      const startTextInput = chip.querySelector('.w-starttext');
      const batteryIconSelect = chip.querySelector('.w-batteryicon');
      const batteryPercentSelect = chip.querySelector('.w-batterypercent');
      const showSecondsInput = chip.querySelector('.w-showseconds');
      const showDateInput = chip.querySelector('.w-showdate');
      const showCpuInput = chip.querySelector('.w-showcpu');
      const showRamInput = chip.querySelector('.w-showram');
      const showGpuInput = chip.querySelector('.w-showgpu');

      const pushUpdate = () => {
        let targetSlot = chip.dataset.region || regionKey;
        let isEnabled = targetSlot !== 'unused';

        let newOrder = w.order ?? (idx + 1) * 10;

        if (showStartTextInput && startTextInput) {
          startTextInput.disabled = !showStartTextInput.checked;
          const stWrap = chip.querySelector('.tc-start-text-row') || startTextInput.parentElement;
          if (stWrap) {
            stWrap.style.opacity = showStartTextInput.checked ? '1' : '0.4';
          }
        }

        if (autoWidthInput && widthInput) {
          widthInput.disabled = autoWidthInput.checked;
          const fwWrap = chip.querySelector('.tc-fixed-width-row') || widthInput.parentElement;
          if (fwWrap) {
            fwWrap.style.opacity = autoWidthInput.checked ? '0.4' : '1';
          }
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

      const collapseBtn = chip.querySelector('.tc-collapse-btn');
      const toggleCollapse = () => {
        const willExpand = !chip.classList.contains('is-expanded');
        chip.classList.toggle('is-expanded', willExpand);
        if (collapseBtn) {
          collapseBtn.title = willExpand ? 'Collapse options' : 'Expand options';
        }
        if (willExpand) {
          expandedWidgetIds.add(w.id);
        } else {
          expandedWidgetIds.delete(w.id);
        }
      };

      if (collapseBtn) {
        collapseBtn.addEventListener('click', (e) => {
          e.stopPropagation();
          toggleCollapse();
        });
      }

      attachPreviewChipDrag(chip, w, toggleCollapse);
      container.appendChild(chip);
    });
  }

  function attachPreviewChipDrag(chip, w, toggleCollapse) {
    chip.addEventListener('pointerdown', (e) => {
      if (e.button !== 0) return;
      if (e.target.closest('input, select, button, label, .tc-no-drag, .arrange-btn')) return;
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
          dragGhost.className = 'rp-chip-ghost';
          dragGhost.innerHTML = `
            <div style="display:flex;align-items:center;gap:8px;padding:8px 12px;font-size:12px;font-weight:600;color:#fff;">
              <span>${escapeHtml(w.name)}</span>
            </div>
          `;
          dragGhost.style.width = `${chipRect.width}px`;
          dragGhost.style.height = `38px`;
          dragGhost.style.left = `${moveEv.clientX - ghostOffsetX}px`;
          dragGhost.style.top = `${moveEv.clientY - 19}px`;
          document.body.appendChild(dragGhost);

          dragPlaceholder = document.createElement('div');
          dragPlaceholder.className = 'rp-chip-placeholder';
          dragPlaceholder.dataset.widgetId = w.id;
          dragPlaceholder.dataset.widgetName = w.name;
          dragPlaceholder.innerHTML = `<span>Drop here</span>`;
          dragPlaceholder.style.width = '100%';
          dragPlaceholder.style.height = `${Math.min(chipRect.height, 120)}px`;

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

        if (!dragStarted) {
          if (e.target.closest('.tc-card-header') && !e.target.closest('button, input, select, label')) {
            if (typeof toggleCollapse === 'function') {
              toggleCollapse();
            }
          }
          return;
        }

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

      items.forEach((item) => {
        const orderSpan = item.querySelector('.rp-chip-order');
        if (orderSpan) orderSpan.remove();
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
  }

  let draggedTileId = null;

  function normalizeSpan(widgetId, span) {
    if (!span || span === '1:2') {
      const isSlider = widgetId.includes('volume') || widgetId.includes('brightness') || widgetId.includes('battery');
      return isSlider ? '4x1' : '2x1';
    }
    if (span === '1:1') return '2x2';
    return span;
  }

  function getNextSpan(widgetId, currentSpan) {
    const norm = normalizeSpan(widgetId, currentSpan);
    const isSlider = widgetId.includes('volume') || widgetId.includes('brightness');
    if (isSlider) {
      const sliderCycle = ['4x1', '2x1', '1x2', '1x1'];
      const idx = sliderCycle.indexOf(norm);
      return sliderCycle[(idx + 1) % sliderCycle.length];
    }
    const generalCycle = ['2x2', '2x1', '1x2', '1x1', '4x1', '3x1'];
    const idx = generalCycle.indexOf(norm);
    if (idx === -1) return '2x2';
    return generalCycle[(idx + 1) % generalCycle.length];
  }

  function renderControlCenterSettings(ccWidgets, allWidgets) {
    if (!ccSettingsPreviewGrid) return;

    latestCcWidgets = (ccWidgets || []).slice().sort((a, b) => (a.order ?? 10) - (b.order ?? 10));

    // 1. Interactive 4-Column Grid Preview
    ccSettingsPreviewGrid.innerHTML = '';
    if (latestCcWidgets.length === 0) {
      ccSettingsPreviewGrid.innerHTML = '<div style="color:var(--win11-text-tertiary);font-size:12px;grid-column:span 4;padding:24px 12px;text-align:center;">No widgets in Control Center. Add one below.</div>';
    } else {
      // 2D grid matrix simulation for 4 columns
      const gridMap = {};
      const getCell = (r, c) => !!(gridMap[r] && gridMap[r][c]);
      const setCell = (r, c, val) => {
        if (!gridMap[r]) gridMap[r] = {};
        gridMap[r][c] = val;
      };

      const placedList = [];
      latestCcWidgets.forEach((m) => {
        const span = normalizeSpan(m.id, m.aspectRatio);
        const parts = span.split('x');
        const colSpan = Math.max(1, Math.min(4, parseInt(parts[0], 10) || 2));
        const rowSpan = Math.max(1, Math.min(2, parseInt(parts[1], 10) || 1));

        let r = 1;
        let placed = false;
        while (!placed) {
          for (let c = 1; c <= 5 - colSpan; c++) {
            let canFit = true;
            for (let dr = 0; dr < rowSpan; dr++) {
              for (let dc = 0; dc < colSpan; dc++) {
                if (getCell(r + dr, c + dc)) {
                  canFit = false;
                  break;
                }
              }
              if (!canFit) break;
            }
            if (canFit) {
              for (let dr = 0; dr < rowSpan; dr++) {
                for (let dc = 0; dc < colSpan; dc++) {
                  setCell(r + dr, c + dc, true);
                }
              }
              placedList.push({
                widget: m,
                span,
                colSpan,
                rowSpan,
                row: r,
                col: c
              });
              placed = true;
              break;
            }
          }
          if (!placed) r++;
        }
      });

      let maxRow = 0;
      placedList.forEach((p) => {
        const endRow = p.row + p.rowSpan - 1;
        if (endRow > maxRow) maxRow = endRow;
      });

      const totalRows = Math.max(4, maxRow + 1);

      // Render placeholders for all unoccupied cells (gaps + trailing empty rows)
      for (let r = 1; r <= totalRows; r++) {
        for (let c = 1; c <= 4; c++) {
          if (!getCell(r, c)) {
            const slot = document.createElement('div');
            slot.className = 'cc-preview-placeholder';
            slot.style.gridRow = `${r}`;
            slot.style.gridColumn = `${c}`;
            
            // Allow dropping dragged widgets onto empty placeholders
            slot.addEventListener('dragover', (ev) => {
              if (!draggedTileId) return;
              ev.preventDefault();
              ev.dataTransfer.dropEffect = 'move';
              slot.classList.add('drag-over');
            });
            slot.addEventListener('dragleave', () => {
              slot.classList.remove('drag-over');
            });
            slot.addEventListener('drop', (ev) => {
              if (!draggedTileId) return;
              ev.preventDefault();
              slot.classList.remove('drag-over');
              // Move dragged widget to the end
              const fromIdx = latestCcWidgets.findIndex((item) => item.id === draggedTileId);
              if (fromIdx !== -1) {
                const reordered = latestCcWidgets.slice();
                const [moved] = reordered.splice(fromIdx, 1);
                reordered.push(moved);
                const payload = reordered.map((item, idx) => ({
                  id: item.id,
                  order: (idx + 1) * 10,
                  aspectRatio: normalizeSpan(item.id, item.aspectRatio),
                  enabled: item.enabled !== false
                }));
                window.winzen.send('update_control_center_widgets', { items: payload });
              }
              draggedTileId = null;
            });

            ccSettingsPreviewGrid.appendChild(slot);
          }
        }
      }

      placedList.forEach((pw) => {
        const m = pw.widget;
        const span = pw.span;
        const tile = document.createElement('div');
        tile.className = 'cc-preview-tile';
        tile.dataset.span = span;
        tile.dataset.aspect = span;
        tile.dataset.id = m.id;
        tile.style.gridColumn = `${pw.col} / span ${pw.colSpan}`;
        tile.style.gridRow = `${pw.row} / span ${pw.rowSpan}`;
        tile.setAttribute('draggable', 'true');

        tile.innerHTML = `
          <div class="cc-tile-header">
            <span class="cc-tile-title" title="${escapeHtml(m.name || m.id)}">${escapeHtml(m.name || m.id)}</span>
            <div class="cc-tile-actions">
              <button type="button" class="cc-tile-btn cc-tile-btn-aspect" title="Click to cycle size (1x1, 2x1, 2x2, 3x1, 4x1)">${span}</button>
              <button type="button" class="cc-tile-btn cc-tile-btn-del" title="Remove widget from Control Center">✕</button>
            </div>
          </div>
          <div class="cc-tile-body">
            <span class="cc-tile-tag">${span} Tile</span>
          </div>
        `;

        // Cycle size on click
        const btnAspect = tile.querySelector('.cc-tile-btn-aspect');
        btnAspect.addEventListener('click', (ev) => {
          ev.stopPropagation();
          const nextSpan = getNextSpan(m.id, span);
          const payload = latestCcWidgets.map((item) => ({
            id: item.id,
            order: item.order,
            aspectRatio: item.id === m.id ? nextSpan : normalizeSpan(item.id, item.aspectRatio),
            enabled: item.enabled !== false
          }));
          window.winzen.send('update_control_center_widgets', { items: payload });
        });

        // Delete widget
        const btnDel = tile.querySelector('.cc-tile-btn-del');
        btnDel.addEventListener('click', (ev) => {
          ev.stopPropagation();
          const filtered = latestCcWidgets.filter((item) => item.id !== m.id);
          const payload = filtered.map((item, idx) => ({
            id: item.id,
            order: (idx + 1) * 10,
            aspectRatio: normalizeSpan(item.id, item.aspectRatio),
            enabled: item.enabled !== false
          }));
          window.winzen.send('update_control_center_widgets', { items: payload });
        });

        // Drag & Drop reordering
        tile.addEventListener('dragstart', (ev) => {
          draggedTileId = m.id;
          tile.classList.add('is-dragging');
          ev.dataTransfer.effectAllowed = 'move';
          try { ev.dataTransfer.setData('text/plain', m.id); } catch { }
        });

        tile.addEventListener('dragend', () => {
          tile.classList.remove('is-dragging');
          draggedTileId = null;
          ccSettingsPreviewGrid.querySelectorAll('.cc-preview-tile').forEach((t) => t.classList.remove('drag-over'));
        });

        tile.addEventListener('dragover', (ev) => {
          if (!draggedTileId || draggedTileId === m.id) return;
          ev.preventDefault();
          ev.dataTransfer.dropEffect = 'move';
          tile.classList.add('drag-over');
        });

        tile.addEventListener('dragleave', () => {
          tile.classList.remove('drag-over');
        });

        tile.addEventListener('drop', (ev) => {
          if (!draggedTileId || draggedTileId === m.id) return;
          ev.preventDefault();
          tile.classList.remove('drag-over');

          const fromIdx = latestCcWidgets.findIndex((item) => item.id === draggedTileId);
          const toIdx = latestCcWidgets.findIndex((item) => item.id === m.id);
          if (fromIdx !== -1 && toIdx !== -1) {
            const reordered = latestCcWidgets.slice();
            const [moved] = reordered.splice(fromIdx, 1);
            reordered.splice(toIdx, 0, moved);

            const payload = reordered.map((item, idx) => ({
              id: item.id,
              order: (idx + 1) * 10,
              aspectRatio: normalizeSpan(item.id, item.aspectRatio),
              enabled: item.enabled !== false
            }));

            window.winzen.send('update_control_center_widgets', { items: payload });
          }
          draggedTileId = null;
        });

        ccSettingsPreviewGrid.appendChild(tile);
      });
    }

    // 2. Available Widgets to Add (Only widgets NOT currently inside Control Center)
    if (ccAvailableWidgetsGrid) {
      ccAvailableWidgetsGrid.innerHTML = '';
      const currentIds = new Set(latestCcWidgets.map((w) => w.id));
      const availableList = (allWidgets || []).filter((w) => !currentIds.has(w.id) && !w.id.includes('zen-control-center'));

      if (availableList.length === 0) {
        ccAvailableWidgetsGrid.innerHTML = '<div class="cc-empty-available">All available widgets have been added to Control Center.</div>';
      } else {
        availableList.forEach((w) => {
          const tile = document.createElement('div');
          tile.className = 'cc-available-tile';
          tile.title = `Click to add ${escapeHtml(w.name)} to Control Center`;
          tile.innerHTML = `
            <div class="cc-avail-info">
              <span class="cc-avail-name">${escapeHtml(w.name)}</span>
              <span class="cc-avail-desc">${escapeHtml(w.description || 'Widget')}</span>
            </div>
            <button type="button" class="cc-btn-add-tile" aria-label="Add to Control Center">
              <svg viewBox="0 0 16 16" width="11" height="11" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><line x1="8" y1="2.5" x2="8" y2="13.5"></line><line x1="2.5" y1="8" x2="13.5" y2="8"></line></svg>
              <span>Add</span>
            </button>
          `;

          tile.addEventListener('click', () => {
            const isSlider = w.id.includes('volume') || w.id.includes('brightness') || w.id.includes('battery');
            const defaultAspect = isSlider ? '4x1' : '2x2';
            const updated = latestCcWidgets.map((item, i) => ({
              id: item.id,
              order: (i + 1) * 10,
              aspectRatio: normalizeSpan(item.id, item.aspectRatio),
              enabled: item.enabled !== false
            }));
            updated.push({
              id: w.id,
              order: (updated.length + 1) * 10,
              aspectRatio: defaultAspect,
              enabled: true
            });
            window.winzen.send('update_control_center_widgets', { items: updated });
          });

          ccAvailableWidgetsGrid.appendChild(tile);
        });
      }
    }
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
