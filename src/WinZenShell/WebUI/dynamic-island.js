(() => {
  const capsule = document.getElementById('islandCapsule');
  const collapsedContent = document.getElementById('islandCollapsedContent');
  const collapsedInner = document.getElementById('islandCollapsedInner');
  const expandedContent = document.getElementById('islandExpandedContent');

  const timeEl = document.getElementById('diTime');
  const secEl = document.getElementById('diSeconds');
  const sepEl = document.getElementById('diSep');
  const dateEl = document.getElementById('diDate');

  const cardTime = document.getElementById('diCardTime');
  const cardSec = document.getElementById('diCardSeconds');
  const cardWeekday = document.getElementById('diCardWeekday');
  const cardDate = document.getElementById('diCardDate');

  let isExpanded = false;
  let currentSlot = 'center';
  let currentAnchorWidth = 80;
  let currentDockEdge = 'top';

  let diConfig = {
    enabled: true,
    placement: 'taskbar',
    taskbarSlot: 'center',
    timeFormat: '24h',
    showSeconds: false,
    showDate: false,
    enableAnimation: true
  };

  function updateClock() {
    const now = new Date();
    const is12h = diConfig.timeFormat === '12h';

    let hours = now.getHours();
    let ampm = '';
    if (is12h) {
      ampm = hours >= 12 ? ' PM' : ' AM';
      hours = hours % 12;
      if (hours === 0) hours = 12;
    }

    const minStr = String(now.getMinutes()).padStart(2, '0');
    const hrStr = is12h ? String(hours) : String(hours).padStart(2, '0');
    const secStr = `:${String(now.getSeconds()).padStart(2, '0')}`;

    // 1. Collapsed Clock
    if (timeEl) timeEl.textContent = `${hrStr}:${minStr}${ampm}`;

    if (secEl) {
      if (diConfig.showSeconds) {
        secEl.style.display = 'inline';
        secEl.textContent = secStr;
      } else {
        secEl.style.display = 'none';
      }
    }

    if (dateEl && sepEl) {
      if (diConfig.showDate) {
        dateEl.style.display = 'inline';
        sepEl.style.display = 'inline-block';
        dateEl.textContent = now.toLocaleDateString([], { weekday: 'short', month: 'short', day: 'numeric' });
      } else {
        dateEl.style.display = 'none';
        sepEl.style.display = 'none';
      }
    }

    // 2. Expanded Card Clock & Date
    if (cardTime) cardTime.textContent = `${hrStr}:${minStr}${ampm}`;
    if (cardSec) cardSec.textContent = secStr;

    const cardBadge = document.getElementById('diCardWeekday');
    const cardDateShort = document.getElementById('diCardDateShort');

    if (cardBadge) cardBadge.textContent = now.toLocaleDateString([], { weekday: 'long' }).toUpperCase();
    if (cardDateShort) cardDateShort.textContent = now.toLocaleDateString([], { month: 'short', day: 'numeric', year: 'numeric' }).toUpperCase();

    updateCollapsedWidth();
  }

  function updateCollapsedWidth() {
    if (!capsule || isExpanded) return;
    if (diConfig.placement === 'top-center') {
      const innerW = (collapsedInner && collapsedInner.getBoundingClientRect().width > 0)
        ? collapsedInner.getBoundingClientRect().width
        : (timeEl && timeEl.getBoundingClientRect().width > 0 ? timeEl.getBoundingClientRect().width : 45);
      const targetW = Math.max(76, Math.min(220, Math.ceil(innerW + 28))); // 14px padding each side
      capsule.style.setProperty('--di-collapsed-width', `${targetW}px`);
    }
  }

  let closeTimeout = null;

  function toggleExpand(force, options = {}) {
    if (!capsule) return;
    const shouldExpand = (typeof force === 'boolean') ? force : !isExpanded;
    if (shouldExpand === isExpanded) return;
    isExpanded = shouldExpand;

    if (closeTimeout) {
      clearTimeout(closeTimeout);
      closeTimeout = null;
    }

    if (options.slot) {
      currentSlot = options.slot.toLowerCase();
    } else if (diConfig.taskbarSlot) {
      currentSlot = diConfig.taskbarSlot.toLowerCase();
    }

    if (options.dockEdge) {
      currentDockEdge = options.dockEdge.toLowerCase();
    }

    if (options.anchorWidth && options.anchorWidth > 40) {
      currentAnchorWidth = Math.round(options.anchorWidth);
      capsule.style.setProperty('--di-collapsed-width', `${currentAnchorWidth}px`);
    } else if (diConfig.placement === 'top-center') {
      updateCollapsedWidth();
    } else {
      capsule.style.setProperty('--di-collapsed-width', `${currentAnchorWidth}px`);
    }

    const collapsedH = (options.anchorHeight && options.anchorHeight > 10) ? Math.round(options.anchorHeight) : 30;
    capsule.style.setProperty('--di-collapsed-height', `${collapsedH}px`);
    // Small border radius is exactly half the height (15px) so the corner radius animates smoothly from the start
    capsule.style.setProperty('--di-collapsed-radius', `${Math.round(collapsedH / 2)}px`);

    const container = document.getElementById('islandContainer') || document.querySelector('.island-container');
    if (container) {
      if (currentDockEdge === 'bottom') {
        container.classList.add('dock-bottom');
      } else {
        container.classList.remove('dock-bottom');
      }
      container.classList.remove('slot-left', 'slot-center', 'slot-right');
      container.classList.add(`slot-${currentSlot}`);
    }

    const hasAnim = diConfig.enableAnimation !== false;
    if (!hasAnim) {
      capsule.classList.add('no-anim');
    } else {
      capsule.classList.remove('no-anim');
    }

    if (shouldExpand) {
      updateClock();
      // Animate corner radius, width, and height immediately from the start of the click
      capsule.classList.add('expanded');

      if (window.winzen && typeof window.winzen.send === 'function') {
        window.winzen.send('di_expand', { expanded: true });
      }
    } else {
      capsule.classList.remove('expanded');

      if (window.winzen && typeof window.winzen.send === 'function') {
        window.winzen.send('di_expand', { expanded: false });
      }

      if (hasAnim) {
        // Wait for contraction transition and small content fade-in to complete before telling host to hide window
        closeTimeout = setTimeout(() => {
          if (!isExpanded && window.winzen && typeof window.winzen.send === 'function') {
            window.winzen.send('di_close_complete');
          }
        }, 500);
      } else {
        if (window.winzen && typeof window.winzen.send === 'function') {
          window.winzen.send('di_close_complete');
        }
      }
    }
  }

  setInterval(updateClock, 1000);
  updateClock();

  function applyState(state) {
    if (!state) return;
    const s = state.Settings || state.settings || state;
    const di = s.DynamicIsland || s.dynamicIsland;
    if (di) {
      diConfig = {
        ...diConfig,
        ...di
      };
      if (diConfig.taskbarSlot) {
        currentSlot = diConfig.taskbarSlot.toLowerCase();
      }
      if (capsule) {
        if (diConfig.enableAnimation === false) {
          capsule.classList.add('no-anim');
        } else {
          capsule.classList.remove('no-anim');
        }
      }
      updateClock();
      updateCollapsedWidth();
    }
  }

  window.addEventListener('winzen:state', (e) => {
    applyState(e.detail);
  });

  if (window.winzen && typeof window.winzen.onState === 'function') {
    window.winzen.onState((state) => applyState(state));
  }

  function handleHostMessage(msg) {
    if (!msg) return;
    if (msg.type === 'state_update' || msg.type === 'state') {
      applyState(msg.payload || msg.data);
      return;
    }
    if (msg.type === 'set_mode') {
      toggleExpand(!!msg.expanded, {
        anchorWidth: msg.anchorWidth,
        anchorHeight: msg.anchorHeight,
        dockEdge: msg.dockEdge,
        slot: msg.slot
      });
    }
  }

  window.addEventListener('winzen:message', (e) => {
    handleHostMessage(e.detail);
  });
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (e) => {
      handleHostMessage(e.data);
    });
  }

  if (capsule) {
    capsule.addEventListener('click', (ev) => {
      ev.stopPropagation();
      toggleExpand(!isExpanded);
    });
  }

  document.addEventListener('click', (ev) => {
    if (isExpanded) {
      if (capsule && !capsule.contains(ev.target)) {
        toggleExpand(false);
      }
    } else {
      toggleExpand(true);
    }
  });

  document.addEventListener('keydown', (ev) => {
    if (ev.key === 'Escape' && isExpanded) {
      toggleExpand(false);
    }
  });
})();
