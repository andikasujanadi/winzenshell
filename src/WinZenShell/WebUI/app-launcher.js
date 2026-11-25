(function () {
  let allApps = [];
  let recentApps = [];
  let pinnedApps = [];
  let visibleApps = [];
  let selectedIndex = -1;
  let lastAppsSig = '';
  let layoutVariant = 'default';
  let win81TileColorMode = 'multicolor';
  let win81SplitView = true;
  let win81FullScreen = false;
  let win81ProfileName = '';
  let win81ProfilePicture = '';
  let win81ActivePane = 'start';

  const WIN81_PALETTE = [
    '#0078D7', '#008299', '#5133AB', '#AC193D',
    '#D24726', '#008A00', '#2672EC', '#8C0095',
    '#094AB2', '#B81B6C', '#0099BC', '#107C10'
  ];

  function getWin81TileColor(key) {
    const str = String(key || '');
    let hash = 0;
    for (let i = 0; i < str.length; i++) {
      hash = ((hash << 5) - hash + str.charCodeAt(i)) | 0;
    }
    return WIN81_PALETTE[Math.abs(hash) % WIN81_PALETTE.length];
  }

  const searchInput = document.getElementById('searchInput');
  const searchBox = document.getElementById('searchBox');
  const btnClearSearch = document.getElementById('btnClearSearch');
  const win81HeaderTitle = document.getElementById('win81HeaderTitle');
  const win81ProfileBox = document.getElementById('win81ProfileBox');
  const win81ProfileNameEl = document.getElementById('win81ProfileName');
  const win81ProfileAvatarEl = document.getElementById('win81ProfileAvatar');
  const win81NavBar = document.getElementById('win81NavBar');
  const btnWin81ToggleView = document.getElementById('btnWin81ToggleView');
  const win81NavArrow = document.getElementById('win81NavArrow');
  const win81NavLabel = document.getElementById('win81NavLabel');
  const sectionPinned = document.getElementById('sectionPinned');
  const pinnedGrid = document.getElementById('pinnedGrid');
  const launcherContent = document.getElementById('launcherContent');
  const syncPinnedCellSize = () => {
    if (layoutVariant === 'win81' && !(searchInput.value || '').trim()) {
      const availH = (launcherContent ? launcherContent.clientHeight : 600) - 58;
      const cell = Math.max(112, Math.min(185, Math.floor((availH - 3 * 8) / 4)));
      document.documentElement.style.setProperty('--pinned-cell-size', `${cell}px`);
      const appsRows = Math.max(6, Math.floor((availH + 8) / 62));
      document.documentElement.style.setProperty('--win81-apps-rows', String(appsRows));
      return;
    }
    if (!pinnedGrid) return;
    const style = getComputedStyle(pinnedGrid);
    const padL = parseFloat(style.paddingLeft) || 8;
    const padR = parseFloat(style.paddingRight) || 8;
    const gap = parseFloat(style.columnGap) || 8;
    const innerWidth = pinnedGrid.clientWidth - padL - padR - gap * 5;
    if (innerWidth > 60) {
      const cell = Math.round(innerWidth / 6);
      document.documentElement.style.setProperty('--pinned-cell-size', `${cell}px`);
    }
  };
  if (launcherContent && typeof ResizeObserver !== 'undefined') {
    new ResizeObserver(syncPinnedCellSize).observe(launcherContent);
  }
  if (launcherContent) {
    launcherContent.addEventListener('wheel', (e) => {
      if (layoutVariant === 'win81' && !(searchInput.value || '').trim()) {
        if (Math.abs(e.deltaY) > Math.abs(e.deltaX) && launcherContent.scrollWidth > launcherContent.clientWidth) {
          launcherContent.scrollLeft += e.deltaY;
          e.preventDefault();
        }
      }
    }, { passive: false });
  }
  const sectionRecent = document.getElementById('sectionRecent');
  const recentGrid = document.getElementById('recentGrid');
  const appsGrid = document.getElementById('appsGrid');
  const allAppsTitle = document.getElementById('allAppsTitle');
  const appCountBadge = document.getElementById('appCountBadge');

  const folderOverlay = document.getElementById('folderOverlay');
  const folderBackdrop = document.getElementById('folderBackdrop');
  const folderDialog = document.getElementById('folderDialog');
  const folderTitleInput = document.getElementById('folderTitleInput');
  const btnUngroupFolder = document.getElementById('btnUngroupFolder');
  const btnFolderClose = document.getElementById('btnFolderClose');
  const folderGrid = document.getElementById('folderGrid');

  let activeOpenFolder = null;

  // Prevent browser native image/text drag from interrupting custom pointer drag
  document.addEventListener('dragstart', (e) => {
    e.preventDefault();
    return false;
  });

  function sendHost(action, payload = {}) {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage({ action, ...payload });
    }
  }

  function isAppPinned(app) {
    if (!app) return false;
    const target = (app.targetPath || '').toLowerCase();
    const name = (app.name || '').toLowerCase();
    return pinnedApps.some(p => {
      if (p.isFolder && Array.isArray(p.folderItems)) {
        return p.folderItems.some(sub =>
          (sub.targetPath && sub.targetPath.toLowerCase() === target) ||
          (sub.name && sub.name.toLowerCase() === name)
        );
      }
      return (p.targetPath && p.targetPath.toLowerCase() === target) ||
             (p.name && p.name.toLowerCase() === name);
    });
  }

  function resolveLiveIcon(item) {
    if (!item) return '';
    const tp = (item.targetPath || '').toLowerCase();
    const nm = (item.name || '').toLowerCase();
    const live = allApps.find(a =>
      (tp && (a.targetPath || '').toLowerCase() === tp) ||
      (nm && (a.name || '').toLowerCase() === nm)
    );
    return (live && live.iconDataUrl) ? live.iconDataUrl : (item.iconDataUrl || '');
  }

  function getFallbackIconSvg() {
    return `<svg class="app-icon-fallback" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
      <rect x="3" y="3" width="7" height="7"></rect>
      <rect x="14" y="3" width="7" height="7"></rect>
      <rect x="14" y="14" width="7" height="7"></rect>
      <rect x="3" y="14" width="7" height="7"></rect>
    </svg>`;
  }

  function getFolderFallbackSvg() {
    return `<svg class="app-icon-fallback" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
      <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"></path>
    </svg>`;
  }

  function renderAppItem(app, isSelected, isPinnedExplicit, pinnedIndex, section) {
    const item = document.createElement('div');
    item.className = 'app-item' + (isSelected ? ' selected' : '');
    item.tabIndex = 0;
    item.addEventListener('dragstart', (e) => e.preventDefault());

    const iconWrap = document.createElement('div');
    iconWrap.className = 'app-icon-wrap';

    if (app.isFolder) {
      const fSize = ['1x1', '2x1', '1x2', '2x2'].includes(app.folderSize) ? app.folderSize : '1x1';
      const items = app.folderItems || [];
      item.classList.add('pinned-folder-item', `size-${fSize}`);
      iconWrap.classList.add('folder-icon-wrap');

      if (fSize === '1x1') {
        const miniGrid = document.createElement('div');
        const count = items.length;
        miniGrid.className = 'folder-mini-grid' + (count === 1 ? ' count-1' : count === 2 ? ' count-2' : '');

        const previewItems = items.slice(0, 4);
        if (previewItems.length === 0) {
          iconWrap.innerHTML = getFolderFallbackSvg();
        } else {
          previewItems.forEach(sub => {
            const subIcon = resolveLiveIcon(sub);
            if (subIcon) {
              const mImg = document.createElement('img');
              mImg.className = 'folder-mini-icon';
              mImg.draggable = false;
              mImg.src = subIcon;
              miniGrid.appendChild(mImg);
            } else {
              const mSpan = document.createElement('span');
              mSpan.className = 'folder-mini-icon';
              mSpan.innerHTML = getFallbackIconSvg();
              miniGrid.appendChild(mSpan);
            }
          });
          iconWrap.appendChild(miniGrid);
        }
      } else {
        item.classList.add('is-large-folder');
        iconWrap.classList.add('large-folder-wrap');

        const largeGrid = document.createElement('div');
        largeGrid.className = `large-folder-grid grid-${fSize}`;

        const capacity = fSize === '2x2' ? 4 : 3;
        const hasOverflow = items.length > capacity;
        const directCount = hasOverflow ? (capacity - 1) : items.length;

        if (items.length === 0) {
          iconWrap.innerHTML = getFolderFallbackSvg();
        } else {
          items.slice(0, directCount).forEach(sub => {
            const appBtn = document.createElement('div');
            appBtn.className = 'large-folder-app-btn';
            appBtn.tabIndex = 0;

            const subIcon = resolveLiveIcon(sub);
            if (subIcon) {
              const img = document.createElement('img');
              img.className = 'large-folder-app-icon';
              img.draggable = false;
              img.decoding = 'async';
              img.src = subIcon;
              img.alt = sub.name || '';
              img.onerror = () => {
                img.remove();
                appBtn.innerHTML = getFallbackIconSvg();
              };
              appBtn.appendChild(img);
            } else {
              appBtn.innerHTML = getFallbackIconSvg();
            }

            appBtn.addEventListener('click', (e) => {
              e.stopPropagation();
              if (item._dragJustEnded) {
                delete item._dragJustEnded;
                return;
              }
              launchApp(sub);
            });

            appBtn.addEventListener('contextmenu', (e) => {
              e.preventDefault();
              e.stopPropagation();
              sendHost('show_app_context_menu', {
                name: sub.name,
                target: sub.targetPath,
                resolvedExePath: sub.resolvedExePath || '',
                iconDataUrl: sub.iconDataUrl || '',
                isPinned: true,
                section: 'folder',
                isFolder: false,
                folderId: app.id || '',
                clientX: e.clientX,
                clientY: e.clientY
              });
            });

            largeGrid.appendChild(appBtn);
          });

          if (hasOverflow) {
            const moreBtn = document.createElement('div');
            moreBtn.className = 'large-folder-more-btn';
            const moreMini = document.createElement('div');
            const restItems = items.slice(directCount, directCount + 4);
            moreMini.className = 'folder-mini-grid' + (restItems.length === 1 ? ' count-1' : restItems.length === 2 ? ' count-2' : '');

            restItems.forEach(sub => {
              const subIcon = resolveLiveIcon(sub);
              if (subIcon) {
                const mImg = document.createElement('img');
                mImg.className = 'folder-mini-icon';
                mImg.draggable = false;
                mImg.src = subIcon;
                moreMini.appendChild(mImg);
              } else {
                const mSpan = document.createElement('span');
                mSpan.className = 'folder-mini-icon';
                mSpan.innerHTML = getFallbackIconSvg();
                moreMini.appendChild(mSpan);
              }
            });

            moreBtn.appendChild(moreMini);
            moreBtn.addEventListener('click', (e) => {
              e.stopPropagation();
              if (item._dragJustEnded) {
                delete item._dragJustEnded;
                return;
              }
              openFolder(app);
            });
            largeGrid.appendChild(moreBtn);
          }

          iconWrap.appendChild(largeGrid);
        }
      }
    } else {
      const appIcon = resolveLiveIcon(app);
      if (appIcon) {
        const img = document.createElement('img');
        img.className = 'app-icon-img';
        img.draggable = false;
        img.decoding = 'async';
        img.src = appIcon;
        img.alt = app.name || '';
        img.onerror = () => {
          img.remove();
          iconWrap.innerHTML = getFallbackIconSvg();
        };
        iconWrap.appendChild(img);
      } else {
        iconWrap.innerHTML = getFallbackIconSvg();
      }
    }

    if (!app.isFolder && section === 'pinned') {
      const tSize = ['1x1', '2x1', '1x2', '2x2'].includes(app.tileSize) ? app.tileSize : '1x1';
      item.classList.add('pinned-app-item', `size-${tSize}`);
    }

    const nameLabel = document.createElement('div');
    nameLabel.className = 'app-name';
    nameLabel.textContent = app.name || '';

    item.appendChild(iconWrap);
    item.appendChild(nameLabel);

    if (app.isFolder) {
      item.addEventListener('click', (e) => {
        if (item._dragJustEnded) {
          delete item._dragJustEnded;
          return;
        }
        openFolder(app);
      });

      item.addEventListener('contextmenu', (e) => {
        e.preventDefault();
        e.stopPropagation();
        sendHost('show_app_context_menu', {
          name: app.name || 'Folder',
          target: app.id || 'folder',
          isPinned: true,
          isFolder: true,
          folderId: app.id || '',
          folderSize: app.folderSize || '1x1'
        });
      });
    } else {
      item.addEventListener('click', () => {
        if (item._dragJustEnded) {
          delete item._dragJustEnded;
          return;
        }
        launchApp(app);
      });

      item.addEventListener('contextmenu', (e) => {
        e.preventDefault();
        e.stopPropagation();
        const isFromPinned = section === 'pinned' || section === 'folder';
        const pinned = isPinnedExplicit !== undefined ? isPinnedExplicit : isAppPinned(app);
        sendHost('show_app_context_menu', {
          name: app.name,
          target: app.targetPath,
          resolvedExePath: app.resolvedExePath || '',
          iconDataUrl: app.iconDataUrl || '',
          isPinned: isFromPinned ? true : pinned,
          section: section || 'all',
          isFolder: false,
          folderId: app._parentFolderId || '',
          tileSize: app.tileSize || '1x1',
          clientX: e.clientX,
          clientY: e.clientY
        });
      });
    }

    item._appData = app;
    if (layoutVariant === 'win81') {
      item.style.setProperty('--win81-tile-bg', getWin81TileColor(app.name || app.id || app.targetPath));
    }

    // Pointer-driven fluid real-time reordering & 500ms hover folder creation
    if (typeof pinnedIndex === 'number' && pinnedIndex >= 0) {
      item.dataset.pinnedIndex = String(pinnedIndex);
      setupPinnedPointerDrag(item, app, pinnedIndex);
    } else if (section === 'recent' || section === 'all') {
      setupCatalogPointerDrag(item, app);
    }

    return item;
  }

  function setupPinnedPointerDrag(item, app, pinnedIndex) {
    item.addEventListener('dragstart', (e) => e.preventDefault());
    item.addEventListener('pointerdown', (e) => {
      if (e.button !== 0) return; // Only primary mouse button

      const startX = e.clientX;
      const startY = e.clientY;
      let isDragging = false;
      let ghostEl = null;
      let hoverFolderTarget = null;
      let hoverFolderTimer = null;
      let isFolderCreationArmed = false;
      let reorderTimer = null;
      let pendingReorderSlot = -1;
      let currentSlotIndex = pinnedIndex;

      // Purge any lingering ghost clones from previous interrupted drags
      document.querySelectorAll('.pinned-drag-ghost').forEach(el => el.remove());

      const removeGhost = () => {
        if (ghostEl) {
          try { ghostEl.remove(); } catch {}
          ghostEl = null;
        }
        document.querySelectorAll('.pinned-drag-ghost').forEach(el => el.remove());
      };

      const cleanupDrag = () => {
        try {
          if (item && e.pointerId) item.releasePointerCapture(e.pointerId);
        } catch {}
        if (reorderTimer) {
          clearTimeout(reorderTimer);
          reorderTimer = null;
          pendingReorderSlot = -1;
        }
        if (hoverFolderTimer) {
          clearTimeout(hoverFolderTimer);
          hoverFolderTimer = null;
        }
        if (hoverFolderTarget) {
          hoverFolderTarget.classList.remove('folder-hover-target', 'folder-hover-armed');
          hoverFolderTarget = null;
        }
        if (item) {
          item.classList.remove('is-drag-source');
        }
        if (pinnedGrid) {
          pinnedGrid.classList.remove('grid-drag-active');
        }
        window.removeEventListener('pointermove', onPointerMove);
        window.removeEventListener('pointerup', onPointerUp);
        window.removeEventListener('pointercancel', onPointerCancel);
      };

      const onPointerCancel = () => {
        cleanupDrag();
        removeGhost();
      };

      const onPointerMove = (moveEvt) => {
        const dist = Math.hypot(moveEvt.clientX - startX, moveEvt.clientY - startY);
        if (!isDragging && dist > 5) {
          isDragging = true;
          try {
            item.setPointerCapture(e.pointerId);
          } catch {}
          removeGhost();

          const rect = item.getBoundingClientRect();
          ghostEl = item.cloneNode(true);
          const fSize = app.folderSize || '1x1';
          const tSize = app.tileSize || '1x1';
          ghostEl.className = 'pinned-drag-ghost' +
            (app.isFolder
              ? ` pinned-folder-item size-${fSize}` + (fSize !== '1x1' ? ' is-large-folder' : '')
              : ` pinned-app-item size-${tSize}`);
          ghostEl.style.background = 'transparent';
          ghostEl.style.border = 'none';
          ghostEl.style.boxShadow = 'none';
          ghostEl.style.width = rect.width + 'px';
          ghostEl.style.height = rect.height + 'px';
          ghostEl.style.left = (moveEvt.clientX - rect.width / 2) + 'px';
          ghostEl.style.top = (moveEvt.clientY - rect.height / 2) + 'px';
          document.body.appendChild(ghostEl);

          item.classList.add('is-drag-source');
          if (pinnedGrid) pinnedGrid.classList.add('grid-drag-active');
        }

        if (!isDragging || !ghostEl) return;
        moveEvt.preventDefault();

        const rect = item.getBoundingClientRect();
        ghostEl.style.left = (moveEvt.clientX - rect.width / 2) + 'px';
        ghostEl.style.top = (moveEvt.clientY - rect.height / 2) + 'px';

        // Folders cannot be dropped into an app or another folder
        let detectedFolderTarget = null;
        if (!app.isFolder) {
          const allItems = Array.from(pinnedGrid.querySelectorAll('.app-item'));
          for (const targetEl of allItems) {
            if (targetEl === item) continue;
            const targetRect = targetEl.getBoundingClientRect();
            const cx = targetRect.left + targetRect.width / 2;
            const cy = targetRect.top + targetRect.height / 2;
            const distToCenter = Math.hypot(moveEvt.clientX - cx, moveEvt.clientY - cy);
            const aimZoneRadius = Math.max(targetRect.width, targetRect.height) * 0.45;

            if (distToCenter <= aimZoneRadius) {
              detectedFolderTarget = targetEl;
              break;
            }
          }
        }

        if (detectedFolderTarget) {
          // If aiming at an icon, cancel any pending fluid slot reorder
          if (reorderTimer) {
            clearTimeout(reorderTimer);
            reorderTimer = null;
            pendingReorderSlot = -1;
          }

          if (hoverFolderTarget !== detectedFolderTarget) {
            if (hoverFolderTimer) clearTimeout(hoverFolderTimer);
            if (hoverFolderTarget) {
              hoverFolderTarget.classList.remove('folder-hover-target', 'folder-hover-armed');
            }
            isFolderCreationArmed = false;
            hoverFolderTarget = detectedFolderTarget;
            hoverFolderTarget.classList.add('folder-hover-target');

            // Arm folder creation after 500ms of holding over target (only creates on mouse release)
            hoverFolderTimer = setTimeout(() => {
              isFolderCreationArmed = true;
              if (hoverFolderTarget) {
                hoverFolderTarget.classList.add('folder-hover-armed');
              }
            }, 500);
          }
          return; // While in folder hover zone, don't reorder slots
        } else {
          // Moved away from target center: disarm folder creation
          if (hoverFolderTimer) {
            clearTimeout(hoverFolderTimer);
            hoverFolderTimer = null;
          }
          if (hoverFolderTarget) {
            hoverFolderTarget.classList.remove('folder-hover-target', 'folder-hover-armed');
            hoverFolderTarget = null;
          }
          isFolderCreationArmed = false;

          // Delay for fluid move: allows the user to aim at icons without them jumping away immediately
          const allItems = Array.from(pinnedGrid.querySelectorAll('.app-item'));
          let bestSlot = currentSlotIndex;
          let minDistance = Infinity;

          allItems.forEach((el, idx) => {
            const r = el.getBoundingClientRect();
            const cx = r.left + r.width / 2;
            const cy = r.top + r.height / 2;
            const d = Math.hypot(moveEvt.clientX - cx, moveEvt.clientY - cy);
            if (d < minDistance) {
              minDistance = d;
              bestSlot = idx;
            }
          });

          if (bestSlot !== currentSlotIndex) {
            if (pendingReorderSlot !== bestSlot) {
              if (reorderTimer) clearTimeout(reorderTimer);
              pendingReorderSlot = bestSlot;
              // 280ms delay before fluid FLIP reordering shifts the tiles
              reorderTimer = setTimeout(() => {
                reorderTimer = null;
                pendingReorderSlot = -1;

                const currentItems = Array.from(pinnedGrid.querySelectorAll('.app-item'));
                if (bestSlot >= currentItems.length) return;

                const rectsBefore = new Map();
                currentItems.forEach(el => rectsBefore.set(el, el.getBoundingClientRect()));

                const refEl = currentItems[bestSlot];
                if (bestSlot > currentSlotIndex) {
                  pinnedGrid.insertBefore(item, refEl.nextSibling);
                } else {
                  pinnedGrid.insertBefore(item, refEl);
                }
                currentSlotIndex = bestSlot;

                const refreshedItems = Array.from(pinnedGrid.querySelectorAll('.app-item'));
                refreshedItems.forEach(el => {
                  if (el === item) return;
                  const b = rectsBefore.get(el);
                  if (!b) return;
                  const a = el.getBoundingClientRect();
                  const dx = b.left - a.left;
                  const dy = b.top - a.top;
                  if (dx !== 0 || dy !== 0) {
                    el.style.transform = `translate(${dx}px, ${dy}px)`;
                    el.style.transition = 'none';
                    requestAnimationFrame(() => {
                      el.style.transition = 'transform 0.22s cubic-bezier(0.2, 0, 0, 1)';
                      el.style.transform = 'translate(0, 0)';
                    });
                  }
                });
              }, 280);
            }
          } else {
            if (reorderTimer) {
              clearTimeout(reorderTimer);
              reorderTimer = null;
              pendingReorderSlot = -1;
            }
          }
        }
      };

      const onPointerUp = () => {
        const wasArmed = isFolderCreationArmed;
        const targetFolderEl = hoverFolderTarget;

        cleanupDrag();

        if (isDragging) {
          item._dragJustEnded = true;
          setTimeout(() => { delete item._dragJustEnded; }, 120);

          if (wasArmed && targetFolderEl) {
            // Folder creation executed ONLY upon mouse release
            const targetIdx = parseInt(targetFolderEl.dataset.pinnedIndex, 10);
            const tgtApp = (!Number.isNaN(targetIdx) && targetIdx >= 0) ? pinnedApps[targetIdx] : null;
            const srcApp = app;

            removeGhost();

            if (tgtApp && srcApp && tgtApp !== srcApp) {
              const actualSrcIdx = pinnedApps.indexOf(srcApp);
              const actualTgtIdx = pinnedApps.indexOf(tgtApp);
              if (actualSrcIdx >= 0 && actualTgtIdx >= 0) {
                delete srcApp.tileSize;
                if (tgtApp.isFolder) {
                  tgtApp.folderItems = tgtApp.folderItems || [];
                  tgtApp.folderItems.push(srcApp);
                  pinnedApps.splice(actualSrcIdx, 1);
                } else {
                  delete tgtApp.tileSize;
                  const folder = {
                    id: 'folder_' + Date.now(),
                    name: 'Folder',
                    isFolder: true,
                    folderSize: '1x1',
                    folderItems: [tgtApp, srcApp]
                  };
                  pinnedApps[actualTgtIdx] = folder;
                  pinnedApps.splice(actualSrcIdx, 1);
                }

                persistAndRenderPinned();
              }
            }
          } else if (ghostEl) {
            removeGhost();
            item.classList.remove('is-drag-source');
            if (pinnedGrid) pinnedGrid.classList.remove('grid-drag-active');

            const reordered = [];
            pinnedGrid.querySelectorAll('.app-item').forEach(el => {
              const oldIdx = parseInt(el.dataset.pinnedIndex, 10);
              if (!Number.isNaN(oldIdx) && pinnedApps[oldIdx]) {
                reordered.push(pinnedApps[oldIdx]);
              }
            });
            if (reordered.length === pinnedApps.length) {
              pinnedApps = reordered;
              persistAndRenderPinned();
            } else {
              updateView();
            }
          } else {
            removeGhost();
            updateView();
          }
        } else {
          removeGhost();
        }
      };

      window.addEventListener('pointermove', onPointerMove);
      window.addEventListener('pointerup', onPointerUp);
      window.addEventListener('pointercancel', onPointerCancel);
    });
  }

  function setupCatalogPointerDrag(item, app) {
    item.addEventListener('dragstart', (e) => e.preventDefault());
    item.addEventListener('pointerdown', (e) => {
      if (e.button !== 0 || !pinnedGrid) return;

      const startX = e.clientX;
      const startY = e.clientY;
      let isDragging = false;
      let ghostEl = null;
      let placeholderEl = null;
      let hoverFolderTarget = null;
      let hoverFolderTimer = null;
      let isFolderCreationArmed = false;
      let autoScrollRaf = null;
      let lastClientY = startY;
      let didRevealPinned = false;
      const contentEl = document.getElementById('launcherContent');

      const removePlaceholder = () => {
        if (placeholderEl) {
          try { placeholderEl.remove(); } catch {}
          placeholderEl = null;
        }
        pinnedGrid.querySelectorAll('.bento-drop-placeholder').forEach(el => el.remove());
      };

      const removeGhost = () => {
        if (ghostEl) {
          try { ghostEl.remove(); } catch {}
          ghostEl = null;
        }
        document.querySelectorAll('.pinned-drag-ghost').forEach(el => el.remove());
      };

      const stopAutoScroll = () => {
        if (autoScrollRaf) {
          cancelAnimationFrame(autoScrollRaf);
          autoScrollRaf = null;
        }
      };

      const tickAutoScroll = () => {
        if (!isDragging || !contentEl) {
          autoScrollRaf = null;
          return;
        }
        if (lastClientY < 120 && contentEl.scrollTop > 0) {
          contentEl.scrollTop = Math.max(0, contentEl.scrollTop - 12);
          autoScrollRaf = requestAnimationFrame(tickAutoScroll);
        } else {
          autoScrollRaf = null;
        }
      };

      const cleanupDrag = () => {
        stopAutoScroll();
        try {
          if (item && e.pointerId) item.releasePointerCapture(e.pointerId);
        } catch {}
        if (hoverFolderTimer) {
          clearTimeout(hoverFolderTimer);
          hoverFolderTimer = null;
        }
        if (hoverFolderTarget) {
          hoverFolderTarget.classList.remove('folder-hover-target', 'folder-hover-armed');
          hoverFolderTarget = null;
        }
        if (item) item.classList.remove('is-drag-source');
        pinnedGrid.classList.remove('grid-drag-active');
        if (didRevealPinned && pinnedApps.length === 0 && sectionPinned) {
          sectionPinned.style.display = 'none';
        }
        window.removeEventListener('pointermove', onPointerMove);
        window.removeEventListener('pointerup', onPointerUp);
        window.removeEventListener('pointercancel', onPointerCancel);
      };

      const onPointerCancel = () => {
        cleanupDrag();
        removePlaceholder();
        removeGhost();
      };

      const onPointerMove = (moveEvt) => {
        lastClientY = moveEvt.clientY;
        const dist = Math.hypot(moveEvt.clientX - startX, moveEvt.clientY - startY);
        if (!isDragging && dist > 6) {
          isDragging = true;
          try { item.setPointerCapture(e.pointerId); } catch {}
          removeGhost();

          const rect = item.getBoundingClientRect();
          ghostEl = item.cloneNode(true);
          ghostEl.className = 'pinned-drag-ghost';
          ghostEl.style.background = 'transparent';
          ghostEl.style.border = 'none';
          ghostEl.style.boxShadow = 'none';
          ghostEl.style.width = rect.width + 'px';
          ghostEl.style.height = rect.height + 'px';
          ghostEl.style.left = (moveEvt.clientX - rect.width / 2) + 'px';
          ghostEl.style.top = (moveEvt.clientY - rect.height / 2) + 'px';
          document.body.appendChild(ghostEl);

          item.classList.add('is-drag-source');
          if (sectionPinned && sectionPinned.style.display === 'none') {
            sectionPinned.style.display = '';
            didRevealPinned = true;
            syncPinnedCellSize();
          }
          pinnedGrid.classList.add('grid-drag-active');
        }

        if (!isDragging || !ghostEl) return;
        moveEvt.preventDefault();

        const rect = item.getBoundingClientRect();
        ghostEl.style.left = (moveEvt.clientX - rect.width / 2) + 'px';
        ghostEl.style.top = (moveEvt.clientY - rect.height / 2) + 'px';

        if (moveEvt.clientY < 120 && contentEl && contentEl.scrollTop > 0 && !autoScrollRaf) {
          autoScrollRaf = requestAnimationFrame(tickAutoScroll);
        }

        const pRect = pinnedGrid.getBoundingClientRect();
        const overPinned = (
          moveEvt.clientX >= pRect.left - 16 &&
          moveEvt.clientX <= pRect.right + 16 &&
          moveEvt.clientY >= pRect.top - 16 &&
          moveEvt.clientY <= pRect.bottom + 24
        );

        if (!overPinned) {
          if (hoverFolderTimer) {
            clearTimeout(hoverFolderTimer);
            hoverFolderTimer = null;
          }
          if (hoverFolderTarget) {
            hoverFolderTarget.classList.remove('folder-hover-target', 'folder-hover-armed');
            hoverFolderTarget = null;
          }
          isFolderCreationArmed = false;
          removePlaceholder();
          return;
        }

        // Check 500ms hover over an existing pinned app or folder (if not already pinned)
        let detectedFolderTarget = null;
        if (!isAppPinned(app)) {
          const allPinned = Array.from(pinnedGrid.querySelectorAll('.app-item'));
          for (const targetEl of allPinned) {
            const tr = targetEl.getBoundingClientRect();
            const cx = tr.left + tr.width / 2;
            const cy = tr.top + tr.height / 2;
            const d = Math.hypot(moveEvt.clientX - cx, moveEvt.clientY - cy);
            const aimRadius = Math.min(tr.width, tr.height) * 0.42;
            if (d <= aimRadius) {
              detectedFolderTarget = targetEl;
              break;
            }
          }
        }

        if (detectedFolderTarget) {
          removePlaceholder();
          if (hoverFolderTarget !== detectedFolderTarget) {
            if (hoverFolderTimer) clearTimeout(hoverFolderTimer);
            if (hoverFolderTarget) {
              hoverFolderTarget.classList.remove('folder-hover-target', 'folder-hover-armed');
            }
            isFolderCreationArmed = false;
            hoverFolderTarget = detectedFolderTarget;
            hoverFolderTarget.classList.add('folder-hover-target');
            hoverFolderTimer = setTimeout(() => {
              isFolderCreationArmed = true;
              if (hoverFolderTarget) hoverFolderTarget.classList.add('folder-hover-armed');
            }, 500);
          }
          return;
        }

        if (hoverFolderTimer) {
          clearTimeout(hoverFolderTimer);
          hoverFolderTimer = null;
        }
        if (hoverFolderTarget) {
          hoverFolderTarget.classList.remove('folder-hover-target', 'folder-hover-armed');
          hoverFolderTarget = null;
        }
        isFolderCreationArmed = false;

        // Place/move Bento 1x1 drop placeholder at nearest slot
        if (!placeholderEl) {
          placeholderEl = document.createElement('div');
          placeholderEl.className = 'bento-drop-placeholder';
        }
        const pinnedItems = Array.from(pinnedGrid.querySelectorAll('.app-item'));
        if (pinnedItems.length === 0) {
          if (placeholderEl.parentNode !== pinnedGrid) {
            pinnedGrid.appendChild(placeholderEl);
          }
          return;
        }

        let bestEl = null;
        let insertAfter = false;
        let minD = Infinity;
        pinnedItems.forEach((el) => {
          const r = el.getBoundingClientRect();
          const cx = r.left + r.width / 2;
          const cy = r.top + r.height / 2;
          const d = Math.hypot(moveEvt.clientX - cx, moveEvt.clientY - cy);
          if (d < minD) {
            minD = d;
            bestEl = el;
            insertAfter = moveEvt.clientX > cx;
          }
        });

        if (bestEl) {
          const refNode = insertAfter ? bestEl.nextSibling : bestEl;
          if (refNode !== placeholderEl && placeholderEl.nextSibling !== refNode) {
            pinnedGrid.insertBefore(placeholderEl, refNode);
          }
        }
      };

      const onPointerUp = () => {
        const wasArmed = isFolderCreationArmed;
        const targetFolderEl = hoverFolderTarget;
        let dropSlotIndex = -1;
        if (placeholderEl && placeholderEl.parentNode === pinnedGrid) {
          const children = Array.from(pinnedGrid.children).filter(c => c === placeholderEl || c.classList.contains('app-item'));
          dropSlotIndex = children.indexOf(placeholderEl);
        }

        removePlaceholder();
        cleanupDrag();
        removeGhost();

        if (!isDragging) return;
        item._dragJustEnded = true;
        setTimeout(() => { delete item._dragJustEnded; }, 120);

        const newPinned = {
          id: app.targetPath || app.id || '',
          targetPath: app.targetPath || '',
          name: app.name || '',
          iconDataUrl: app.iconDataUrl || '',
          resolvedExePath: app.resolvedExePath || '',
          isFolder: false,
          tileSize: '1x1',
          folderItems: []
        };

        if (wasArmed && targetFolderEl && !isAppPinned(app)) {
          const targetIdx = parseInt(targetFolderEl.dataset.pinnedIndex, 10);
          const tgtApp = (!Number.isNaN(targetIdx) && targetIdx >= 0) ? pinnedApps[targetIdx] : null;
          if (tgtApp) {
            if (tgtApp.isFolder) {
              tgtApp.folderItems = tgtApp.folderItems || [];
              tgtApp.folderItems.push(newPinned);
            } else {
              delete tgtApp.tileSize;
              pinnedApps[targetIdx] = {
                id: 'folder_' + Date.now(),
                name: 'Folder',
                isFolder: true,
                folderSize: '1x1',
                folderItems: [tgtApp, newPinned]
              };
            }
            persistAndRenderPinned();
          }
        } else if (dropSlotIndex >= 0) {
          const targetLower = (app.targetPath || '').toLowerCase();
          const nameLower = (app.name || '').toLowerCase();
          const existingRootIdx = pinnedApps.findIndex(p => !p.isFolder && (
            (targetLower && (p.targetPath || '').toLowerCase() === targetLower) ||
            (nameLower && (p.name || '').toLowerCase() === nameLower)
          ));
          if (existingRootIdx >= 0) {
            const [moved] = pinnedApps.splice(existingRootIdx, 1);
            const clampedIdx = Math.min(Math.max(0, existingRootIdx < dropSlotIndex ? dropSlotIndex - 1 : dropSlotIndex), pinnedApps.length);
            pinnedApps.splice(clampedIdx, 0, moved);
            persistAndRenderPinned();
          } else if (!isAppPinned(app)) {
            const clampedIdx = Math.min(Math.max(0, dropSlotIndex), pinnedApps.length);
            pinnedApps.splice(clampedIdx, 0, newPinned);
            persistAndRenderPinned();
          }
        }
      };

      window.addEventListener('pointermove', onPointerMove);
      window.addEventListener('pointerup', onPointerUp);
      window.addEventListener('pointercancel', onPointerCancel);
    });
  }

  function openFolder(folder) {
    if (!folder) return;
    activeOpenFolder = folder;
    folderTitleInput.value = folder.name || 'Folder';
    renderFolderGrid(folder);
    folderOverlay.style.display = 'flex';
    folderTitleInput.blur();
  }

  function closeFolder() {
    folderOverlay.style.display = 'none';
    activeOpenFolder = null;
  }

  function checkFolderAutoUngroup(folder) {
    if (!folder || !folder.isFolder) return false;
    const items = folder.folderItems || [];
    if (items.length <= 1) {
      const fIdx = pinnedApps.indexOf(folder);
      if (fIdx >= 0) {
        items.forEach((i) => delete i._parentFolderId);
        pinnedApps.splice(fIdx, 1, ...items);
      }
      if (activeOpenFolder === folder) {
        closeFolder();
      }
      persistAndRenderPinned();
      return true;
    }
    return false;
  }

  function renderFolderGrid(folder) {
    folderGrid.innerHTML = '';
    const items = folder.folderItems || [];
    if (items.length === 0) {
      const empty = document.createElement('div');
      empty.className = 'apps-empty';
      empty.textContent = 'Empty folder';
      folderGrid.appendChild(empty);
      return;
    }

    items.forEach((app, idx) => {
      app._parentFolderId = folder.id;
      const el = renderAppItem(app, false, true, undefined, 'folder');
      el.dataset.folderSubIndex = String(idx);
      setupFolderItemPointerDrag(el, app, idx, folder);
      folderGrid.appendChild(el);
    });
  }

  function setupFolderItemPointerDrag(item, app, subIdx, folder) {
    item.addEventListener('dragstart', (e) => e.preventDefault());
    item.addEventListener('pointerdown', (e) => {
      if (e.button !== 0) return;

      const startX = e.clientX;
      const startY = e.clientY;
      let isDragging = false;
      let ghostEl = null;
      let reorderTimer = null;
      let pendingReorderSlot = -1;
      let currentSlotIndex = subIdx;

      try {
        item.setPointerCapture(e.pointerId);
      } catch {}

      document.querySelectorAll('.pinned-drag-ghost').forEach(el => el.remove());

      const removeGhost = () => {
        if (ghostEl) {
          try { ghostEl.remove(); } catch {}
          ghostEl = null;
        }
        document.querySelectorAll('.pinned-drag-ghost').forEach(el => el.remove());
      };

      const cleanupDrag = () => {
        try {
          if (item && e.pointerId) item.releasePointerCapture(e.pointerId);
        } catch {}
        if (reorderTimer) {
          clearTimeout(reorderTimer);
          reorderTimer = null;
          pendingReorderSlot = -1;
        }
        if (item) {
          item.classList.remove('is-drag-source');
        }
        window.removeEventListener('pointermove', onPointerMove);
        window.removeEventListener('pointerup', onPointerUp);
        window.removeEventListener('pointercancel', onPointerCancel);
      };

      const onPointerCancel = () => {
        cleanupDrag();
        removeGhost();
      };

      const onPointerMove = (moveEvt) => {
        const dist = Math.hypot(moveEvt.clientX - startX, moveEvt.clientY - startY);
        if (!isDragging && dist > 5) {
          isDragging = true;
          removeGhost();

          const rect = item.getBoundingClientRect();
          ghostEl = item.cloneNode(true);
          ghostEl.className = 'pinned-drag-ghost';
          ghostEl.style.background = 'transparent';
          ghostEl.style.border = 'none';
          ghostEl.style.boxShadow = 'none';
          ghostEl.style.width = rect.width + 'px';
          ghostEl.style.height = rect.height + 'px';
          ghostEl.style.left = (moveEvt.clientX - rect.width / 2) + 'px';
          ghostEl.style.top = (moveEvt.clientY - rect.height / 2) + 'px';
          document.body.appendChild(ghostEl);

          item.classList.add('is-drag-source');
        }

        if (!isDragging || !ghostEl) return;
        moveEvt.preventDefault();

        const rect = item.getBoundingClientRect();
        ghostEl.style.left = (moveEvt.clientX - rect.width / 2) + 'px';
        ghostEl.style.top = (moveEvt.clientY - rect.height / 2) + 'px';

        // Check if cursor is dragged outside the folder dialog container
        const dlgRect = folderDialog.getBoundingClientRect();
        const isOutside = (
          moveEvt.clientX < dlgRect.left - 10 ||
          moveEvt.clientX > dlgRect.right + 10 ||
          moveEvt.clientY < dlgRect.top - 10 ||
          moveEvt.clientY > dlgRect.bottom + 10
        );

        if (isOutside) {
          ghostEl.classList.add('drag-out-folder');
          if (reorderTimer) {
            clearTimeout(reorderTimer);
            reorderTimer = null;
            pendingReorderSlot = -1;
          }
        } else {
          ghostEl.classList.remove('drag-out-folder');

          // Fluid slot reordering within folderGrid
          const currentItems = Array.from(folderGrid.querySelectorAll('.app-item'));
          let bestSlot = currentSlotIndex;
          let minDistance = Infinity;

          currentItems.forEach((el, idx) => {
            const r = el.getBoundingClientRect();
            const cx = r.left + r.width / 2;
            const cy = r.top + r.height / 2;
            const d = Math.hypot(moveEvt.clientX - cx, moveEvt.clientY - cy);
            if (d < minDistance) {
              minDistance = d;
              bestSlot = idx;
            }
          });

          if (bestSlot !== currentSlotIndex) {
            if (pendingReorderSlot !== bestSlot) {
              if (reorderTimer) clearTimeout(reorderTimer);
              pendingReorderSlot = bestSlot;
              reorderTimer = setTimeout(() => {
                reorderTimer = null;
                pendingReorderSlot = -1;

                const freshItems = Array.from(folderGrid.querySelectorAll('.app-item'));
                if (bestSlot >= freshItems.length) return;

                const rectsBefore = new Map();
                freshItems.forEach(el => rectsBefore.set(el, el.getBoundingClientRect()));

                const refEl = freshItems[bestSlot];
                if (bestSlot > currentSlotIndex) {
                  folderGrid.insertBefore(item, refEl.nextSibling);
                } else {
                  folderGrid.insertBefore(item, refEl);
                }
                currentSlotIndex = bestSlot;

                const refreshed = Array.from(folderGrid.querySelectorAll('.app-item'));
                refreshed.forEach(el => {
                  if (el === item) return;
                  const b = rectsBefore.get(el);
                  if (!b) return;
                  const a = el.getBoundingClientRect();
                  const dx = b.left - a.left;
                  const dy = b.top - a.top;
                  if (dx !== 0 || dy !== 0) {
                    el.style.transform = `translate(${dx}px, ${dy}px)`;
                    el.style.transition = 'none';
                    requestAnimationFrame(() => {
                      el.style.transition = 'transform 0.22s cubic-bezier(0.2, 0, 0, 1)';
                      el.style.transform = 'translate(0, 0)';
                    });
                  }
                });
              }, 200);
            }
          } else {
            if (reorderTimer) {
              clearTimeout(reorderTimer);
              reorderTimer = null;
              pendingReorderSlot = -1;
            }
          }
        }
      };

      const onPointerUp = (upEvt) => {
        const dlgRect = folderDialog.getBoundingClientRect();
        const isOutside = (
          upEvt.clientX < dlgRect.left - 10 ||
          upEvt.clientX > dlgRect.right + 10 ||
          upEvt.clientY < dlgRect.top - 10 ||
          upEvt.clientY > dlgRect.bottom + 10
        );

        cleanupDrag();

        if (isDragging) {
          item._dragJustEnded = true;
          setTimeout(() => { delete item._dragJustEnded; }, 120);

          if (isOutside) {
            // Dragged outside: remove from folder and add to root pinned apps!
            removeGhost();

            const appIdx = (folder.folderItems || []).indexOf(app);
            if (appIdx >= 0) {
              folder.folderItems.splice(appIdx, 1);
            }
            delete app._parentFolderId;

            // Place app in root pinned apps right after the folder
            const fIdx = pinnedApps.indexOf(folder);
            if (fIdx >= 0) {
              pinnedApps.splice(fIdx + 1, 0, app);
            } else {
              pinnedApps.push(app);
            }

            const ungrouped = checkFolderAutoUngroup(folder);
            if (!ungrouped) {
              renderFolderGrid(folder);
            }
            persistAndRenderPinned();
          } else {
            // Dropped inside folder dialog: sync order and remove ghost cleanly
            removeGhost();
            item.classList.remove('is-drag-source');

            const reordered = [];
            folderGrid.querySelectorAll('.app-item').forEach(el => {
              const oldSub = parseInt(el.dataset.folderSubIndex, 10);
              if (!Number.isNaN(oldSub) && folder.folderItems[oldSub]) {
                reordered.push(folder.folderItems[oldSub]);
              }
            });
            if (reordered.length === (folder.folderItems || []).length) {
              folder.folderItems = reordered;
              persistAndRenderPinned();
              renderFolderGrid(folder);
            } else {
              renderFolderGrid(folder);
            }
          }
        } else {
          removeGhost();
          renderFolderGrid(folder);
        }
      };

      window.addEventListener('pointermove', onPointerMove);
      window.addEventListener('pointerup', onPointerUp);
      window.addEventListener('pointercancel', onPointerCancel);
    });
  }

  if (folderTitleInput) {
    const commitFolderRename = () => {
      if (activeOpenFolder) {
        const newName = folderTitleInput.value.trim() || 'Folder';
        if (activeOpenFolder.name !== newName) {
          activeOpenFolder.name = newName;
          persistAndRenderPinned();
        }
      }
    };
    folderTitleInput.addEventListener('blur', commitFolderRename);
    folderTitleInput.addEventListener('keydown', (e) => {
      e.stopPropagation();
      if (e.key === 'Enter') {
        commitFolderRename();
        folderTitleInput.blur();
      } else if (e.key === 'Escape') {
        if (activeOpenFolder) {
          folderTitleInput.value = activeOpenFolder.name || 'Folder';
        }
        folderTitleInput.blur();
      }
    });
  }

  if (btnFolderClose) btnFolderClose.addEventListener('click', closeFolder);
  if (folderBackdrop) folderBackdrop.addEventListener('click', closeFolder);

  if (btnUngroupFolder) {
    btnUngroupFolder.addEventListener('click', () => {
      if (!activeOpenFolder) return;
      const f = activeOpenFolder;
      const fIdx = pinnedApps.indexOf(f);
      if (fIdx >= 0) {
        const unpacked = f.folderItems || [];
        unpacked.forEach((i) => delete i._parentFolderId);
        pinnedApps.splice(fIdx, 1, ...unpacked);
        closeFolder();
        persistAndRenderPinned();
      }
    });
  }

  function persistAndRenderPinned() {
    updateView();
    sendHost('save_pinned_launcher_apps', {
      items: pinnedApps.map((p) => ({
        id: p.id || p.targetPath || '',
        targetPath: p.targetPath || '',
        name: p.name || '',
        iconDataUrl: p.iconDataUrl || '',
        isFolder: !!p.isFolder,
        folderSize: p.folderSize || '1x1',
        tileSize: p.tileSize || '1x1',
        folderItems: (p.folderItems || []).map((sub) => ({
          id: sub.id || sub.targetPath || '',
          targetPath: sub.targetPath || '',
          name: sub.name || '',
          iconDataUrl: sub.iconDataUrl || '',
          isFolder: false,
          folderItems: []
        }))
      }))
    });
  }

  function launchApp(app) {
    if (!app) return;
    closeFolder();
    sendHost('launch_app', { target: app.targetPath, name: app.name });
  }

  if (btnWin81ToggleView) {
    btnWin81ToggleView.addEventListener('click', () => {
      const goingToApps = win81ActivePane === 'start';
      win81ActivePane = goingToApps ? 'apps' : 'start';
      const contentEl = document.getElementById('launcherContent');
      if (contentEl) {
        contentEl.scrollTop = 0;
        contentEl.scrollLeft = 0;
        contentEl.classList.remove('win81-anim-to-apps', 'win81-anim-to-start');
        void contentEl.offsetWidth;
        contentEl.classList.add(goingToApps ? 'win81-anim-to-apps' : 'win81-anim-to-start');
      }
      updateView();
    });
  }

  function updateView() {
    const query = (searchInput.value || '').trim().toLowerCase();
    const isWin81 = layoutVariant === 'win81';
    document.body.classList.toggle('variant-win81', isWin81);
    document.body.classList.toggle('win81-fullscreen', isWin81 && win81FullScreen);
    document.body.classList.toggle('win81-searching', isWin81 && !!query);
    document.body.classList.toggle('win81-accent-mode', isWin81 && win81TileColorMode === 'accent');
    document.body.classList.toggle('win81-multicolor-mode', isWin81 && win81TileColorMode !== 'accent');

    const isWin81AppsOrSearch = isWin81 && ((win81SplitView && win81ActivePane === 'apps') || !!query);
    document.body.classList.toggle('win81-apps-page', isWin81AppsOrSearch);

    if (win81HeaderTitle) {
      win81HeaderTitle.style.display = isWin81 ? 'block' : 'none';
      win81HeaderTitle.textContent = isWin81AppsOrSearch ? 'Apps' : 'Start';
    }
    if (win81ProfileBox) {
      win81ProfileBox.style.display = (isWin81 && !isWin81AppsOrSearch) ? 'flex' : 'none';
      if (win81ProfileNameEl) {
        const parts = (win81ProfileName || 'User').trim().split(/\s+/);
        if (parts.length >= 2) {
          win81ProfileNameEl.innerHTML = `<span class="win81-profile-first">${parts[0].replace(/</g, '&lt;')}</span><span class="win81-profile-last">${parts.slice(1).join(' ').replace(/</g, '&lt;')}</span>`;
        } else {
          win81ProfileNameEl.innerHTML = `<span class="win81-profile-first">${(parts[0] || 'User').replace(/</g, '&lt;')}</span>`;
        }
      }
      if (win81ProfileAvatarEl) {
        if (win81ProfilePicture) {
          win81ProfileAvatarEl.innerHTML = `<img src="${win81ProfilePicture}" alt="" />`;
        } else {
          win81ProfileAvatarEl.innerHTML = `<svg viewBox="0 0 24 24" fill="currentColor" width="22" height="22"><path d="M12 12c2.7 0 5-2.3 5-5s-2.3-5-5-5-5 2.3-5 5 2.3 5 5 5zm0 2c-3.3 0-10 1.7-10 5v3h20v-3c0-3.3-6.7-5-10-5z"/></svg>`;
        }
      }
    }
    if (searchBox) {
      searchBox.style.display = (!isWin81 || isWin81AppsOrSearch) ? 'flex' : 'none';
    }
    if (searchInput) {
      searchInput.placeholder = isWin81 ? 'Search' : 'Search your apps...';
    }

    if (!query) {
      btnClearSearch.style.display = 'none';
      const useSplit = isWin81 && win81SplitView;
      if (win81NavBar) {
        win81NavBar.style.display = useSplit ? 'flex' : 'none';
        if (win81NavArrow) {
          win81NavArrow.innerHTML = win81ActivePane === 'start'
            ? `<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="5" x2="12" y2="19"></line><polyline points="19 12 12 19 5 12"></polyline></svg>`
            : `<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="19" x2="12" y2="5"></line><polyline points="5 12 12 5 19 12"></polyline></svg>`;
        }
        if (win81NavLabel) win81NavLabel.textContent = win81ActivePane === 'start' ? 'Apps' : 'Start';
      }

      if (useSplit) {
        const showStart = win81ActivePane === 'start';
        if (sectionPinned) sectionPinned.style.display = (showStart && pinnedApps.length > 0) ? 'block' : 'none';
        sectionRecent.style.display = (showStart && recentApps.length > 0) ? 'block' : 'none';
        const sectionAllEl = document.getElementById('sectionAll');
        if (sectionAllEl) sectionAllEl.style.display = showStart ? 'none' : 'block';
      } else {
        if (sectionPinned) sectionPinned.style.display = pinnedApps.length > 0 ? 'block' : 'none';
        sectionRecent.style.display = recentApps.length > 0 ? 'block' : 'none';
        const sectionAllEl = document.getElementById('sectionAll');
        if (sectionAllEl) sectionAllEl.style.display = 'block';
      }

      allAppsTitle.textContent = isWin81 ? 'Apps' : 'All Apps';
      visibleApps = allApps;
      selectedIndex = -1;
    } else {
      // Active query: Hide Pinned & Recent, show filtered All Apps
      btnClearSearch.style.display = 'flex';
      if (win81NavBar) win81NavBar.style.display = 'none';
      if (sectionPinned) sectionPinned.style.display = 'none';
      sectionRecent.style.display = 'none';
      const sectionAllEl = document.getElementById('sectionAll');
      if (sectionAllEl) sectionAllEl.style.display = 'block';

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

    // Purge any lingering ghost drag clones
    document.querySelectorAll('.pinned-drag-ghost').forEach(el => el.remove());

    // Render Pinned row
    if (pinnedGrid) {
      pinnedGrid.innerHTML = '';
      pinnedApps.forEach((app, pIdx) => {
        pinnedGrid.appendChild(renderAppItem(app, false, true, pIdx, 'pinned'));
      });
      requestAnimationFrame(syncPinnedCellSize);
    }

    // Render Recent row
    recentGrid.innerHTML = '';
    recentApps.slice(0, 6).forEach(app => {
      recentGrid.appendChild(renderAppItem(app, false, false, undefined, 'recent'));
    });

    // Render Apps grid
    appsGrid.innerHTML = '';
    if (visibleApps.length === 0) {
      const empty = document.createElement('div');
      empty.className = 'apps-empty';
      empty.textContent = query ? `No apps matching "${query}"` : 'No applications found';
      appsGrid.appendChild(empty);
    } else if (isWin81 && !query) {
      let lastLetter = null;
      visibleApps.forEach((app, idx) => {
        const firstChar = ((app.name || '').trim().charAt(0) || '#').toUpperCase();
        const letter = /[A-Z]/.test(firstChar) ? firstChar : '#';
        if (letter !== lastLetter) {
          lastLetter = letter;
          const header = document.createElement('div');
          header.className = 'win81-alpha-header';
          header.textContent = letter;
          appsGrid.appendChild(header);
        }
        appsGrid.appendChild(renderAppItem(app, idx === selectedIndex, undefined, undefined, 'all'));
      });
    } else {
      visibleApps.forEach((app, idx) => {
        appsGrid.appendChild(renderAppItem(app, idx === selectedIndex, undefined, undefined, 'all'));
      });
    }

    scrollSelectedIntoView();
  }

  let selectedElement = null;

  function getNavigableItems() {
    if (folderOverlay && folderOverlay.style.display === 'flex') {
      return Array.from(folderGrid.querySelectorAll('.app-item'));
    }
    const items = [];
    if (sectionPinned && sectionPinned.style.display !== 'none' && pinnedGrid) {
      items.push(...pinnedGrid.querySelectorAll('.app-item'));
    }
    if (sectionRecent && sectionRecent.style.display !== 'none' && recentGrid) {
      items.push(...recentGrid.querySelectorAll('.app-item'));
    }
    if (appsGrid) {
      items.push(...appsGrid.querySelectorAll('.app-item'));
    }
    return items;
  }

  function setSelectedElement(el) {
    document.querySelectorAll('.app-item.selected').forEach(node => node.classList.remove('selected'));
    selectedElement = el || null;
    if (selectedElement) {
      selectedElement.classList.add('selected');
      selectedElement.scrollIntoView({ block: 'nearest', inline: 'nearest', behavior: 'smooth' });
    }
  }

  function navigateSpatial(key) {
    const items = getNavigableItems();
    if (items.length === 0) return false;

    const current = (selectedElement && document.contains(selectedElement))
      ? selectedElement
      : (appsGrid.querySelector('.app-item.selected') || null);

    if (!current) {
      setSelectedElement(items[0]);
      return true;
    }

    const isHorizontalMode = layoutVariant === 'win81' && !(searchInput.value || '').trim() &&
      !(folderOverlay && folderOverlay.style.display === 'flex');

    if (!isHorizontalMode && (key === 'ArrowRight' || key === 'ArrowLeft')) {
      const idx = items.indexOf(current);
      if (idx < 0) {
        setSelectedElement(items[0]);
      } else {
        const nextIdx = key === 'ArrowRight'
          ? Math.min(idx + 1, items.length - 1)
          : Math.max(idx - 1, 0);
        setSelectedElement(items[nextIdx]);
      }
      return true;
    }

    const curRect = current.getBoundingClientRect();
    const curCx = curRect.left + curRect.width / 2;
    const curCy = curRect.top + curRect.height / 2;

    if (key === 'ArrowRight' || key === 'ArrowLeft') {
      const isRight = key === 'ArrowRight';
      let best = null;
      let bestColDist = Infinity;
      let bestRowDist = Infinity;
      items.forEach(cand => {
        if (cand === current) return;
        const r = cand.getBoundingClientRect();
        const cx = r.left + r.width / 2;
        const cy = r.top + r.height / 2;
        if (isRight ? (r.left < curRect.right - 8 && cx <= curCx + 4) : (r.right > curRect.left + 8 && cx >= curCx - 4)) {
          return;
        }
        const colDist = Math.abs(cx - curCx);
        const rowDist = Math.abs(cy - curCy);
        if (colDist < bestColDist - 12 || (Math.abs(colDist - bestColDist) <= 12 && rowDist < bestRowDist)) {
          bestColDist = colDist;
          bestRowDist = rowDist;
          best = cand;
        }
      });
      if (best) setSelectedElement(best);
      return true;
    }

    const isDown = key === 'ArrowDown';

    let best = null;
    let bestRowDist = Infinity;
    let bestColDist = Infinity;

    items.forEach(cand => {
      if (cand === current) return;
      const r = cand.getBoundingClientRect();
      const cx = r.left + r.width / 2;
      const cy = r.top + r.height / 2;
      if (isDown ? (r.top < curRect.bottom - 8 && cy <= curCy + 4) : (r.bottom > curRect.top + 8 && cy >= curCy - 4)) {
        return;
      }
      const rowDist = Math.abs(cy - curCy);
      const colDist = Math.abs(cx - curCx);
      if (rowDist < bestRowDist - 12 || (Math.abs(rowDist - bestRowDist) <= 12 && colDist < bestColDist)) {
        bestRowDist = rowDist;
        bestColDist = colDist;
        best = cand;
      }
    });

    if (best) {
      setSelectedElement(best);
    }
    return true;
  }

  function scrollSelectedIntoView() {
    if (selectedIndex >= 0) {
      const items = appsGrid.querySelectorAll('.app-item');
      if (items[selectedIndex]) {
        setSelectedElement(items[selectedIndex]);
        return;
      }
    }
    setSelectedElement(null);
  }

  // Keyboard navigation
  searchInput.addEventListener('input', () => {
    updateView();
  });

  searchInput.addEventListener('keydown', (e) => {
    handleNavigationOrEscape(e);
  });

  // Redirect typing anywhere in the launcher into the searchbar
  document.addEventListener('keydown', (e) => {
    // If folder modal is open, let the modal handle navigation/escape (unless renaming title)
    if (folderOverlay && folderOverlay.style.display === 'flex') {
      if (document.activeElement === folderTitleInput) {
        if (e.key === 'Escape') {
          e.preventDefault();
          closeFolder();
        }
        return;
      }
      handleNavigationOrEscape(e);
      return;
    }

    if (
      document.activeElement === searchInput ||
      document.activeElement === folderTitleInput ||
      document.activeElement?.tagName === 'INPUT' ||
      document.activeElement?.tagName === 'TEXTAREA'
    ) {
      return;
    }

    if (handleNavigationOrEscape(e)) return;

    if (e.key === 'Backspace') {
      e.preventDefault();
      searchInput.focus();
      if (searchInput.value.length > 0) {
        searchInput.value = searchInput.value.slice(0, -1);
        updateView();
      }
      return;
    }

    // Printable character without Ctrl/Alt/Meta modifiers
    if (e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey) {
      e.preventDefault();
      searchInput.value += e.key;
      updateView();
      searchInput.focus();
    }
  });

  function handleNavigationOrEscape(e) {
    if (folderOverlay && folderOverlay.style.display === 'flex') {
      if (e.key === 'Escape') {
        e.preventDefault();
        closeFolder();
        return true;
      }
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp' || e.key === 'ArrowLeft' || e.key === 'ArrowRight') {
        e.preventDefault();
        return navigateSpatial(e.key);
      }
      if (e.key === 'Enter' && selectedElement && folderGrid.contains(selectedElement) && selectedElement._appData) {
        e.preventDefault();
        launchApp(selectedElement._appData);
        return true;
      }
      return false;
    }

    if (e.key === 'ArrowDown' || e.key === 'ArrowUp' || e.key === 'ArrowRight' || e.key === 'ArrowLeft') {
      e.preventDefault();
      return navigateSpatial(e.key);
    } else if (e.key === 'Enter') {
      e.preventDefault();
      if (selectedElement && document.contains(selectedElement) && selectedElement._appData) {
        if (selectedElement._appData.isFolder) {
          openFolder(selectedElement._appData);
        } else {
          launchApp(selectedElement._appData);
        }
      } else if (selectedIndex >= 0 && visibleApps[selectedIndex]) {
        launchApp(visibleApps[selectedIndex]);
      } else if (visibleApps.length > 0) {
        launchApp(visibleApps[0]);
      }
      return true;
    } else if (e.key === 'Escape') {
      e.preventDefault();
      if (searchInput.value) {
        searchInput.value = '';
        searchInput.focus();
        updateView();
      } else {
        sendHost('close_launcher');
      }
      return true;
    }
    return false;
  }

  function refreshSelected() {
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
      const nextVariant = data.layoutVariant === 'win81' ? 'win81' : 'default';
      const nextColorMode = data.win81TileColorMode === 'accent' ? 'accent' : 'multicolor';
      const nextSplitView = typeof data.win81SplitView === 'boolean' ? data.win81SplitView : true;
      const nextFullScreen = typeof data.win81FullScreen === 'boolean' ? data.win81FullScreen : false;
      const nextProfileName = typeof data.win81ProfileName === 'string' ? data.win81ProfileName : '';
      const nextProfilePic = typeof data.win81ProfilePicture === 'string' ? data.win81ProfilePicture : '';
      const nextAll = Array.isArray(payload.all) ? payload.all : [];
      const nextRecent = Array.isArray(payload.recent) ? payload.recent : [];
      const nextPinned = Array.isArray(payload.pinned) ? payload.pinned : [];
      const sig = JSON.stringify({
        v: `${nextVariant}|${nextColorMode}|${nextSplitView}|${nextFullScreen}|${nextProfileName}|${nextProfilePic.length}`,
        p: nextPinned.map((a) => `${a.isFolder ? 'f' : 'a'}|${a.id || a.targetPath}|${a.name}|${a.folderSize || '1x1'}|${a.tileSize || '1x1'}|${(a.folderItems || []).map(s => s.targetPath || s.name).join(',')}`),
        r: nextRecent.map((a) => `${a.targetPath}|${a.name}|${a.iconDataUrl ? `${a.iconDataUrl.length}:${a.iconDataUrl.slice(-24)}` : 0}`),
        a: nextAll.map((a) => `${a.targetPath}|${a.name}|${a.iconDataUrl ? `${a.iconDataUrl.length}:${a.iconDataUrl.slice(-24)}` : 0}`)
      });
      if (sig !== lastAppsSig) {
        lastAppsSig = sig;
        layoutVariant = nextVariant;
        win81TileColorMode = nextColorMode;
        win81SplitView = nextSplitView;
        win81FullScreen = nextFullScreen;
        win81ProfileName = nextProfileName;
        win81ProfilePicture = nextProfilePic;
        allApps = nextAll;
        recentApps = nextRecent;
        pinnedApps = nextPinned;
        updateView();
        if (activeOpenFolder) {
          const freshFolder = pinnedApps.find(p => p.isFolder && (p.id === activeOpenFolder.id || p.name === activeOpenFolder.name));
          if (freshFolder) {
            activeOpenFolder = freshFolder;
            renderFolderGrid(freshFolder);
          } else {
            closeFolder();
          }
        }
      }
    } else if (data.type === 'open_folder' || data.action === 'open_folder') {
      const fId = data.folderId;
      const match = pinnedApps.find(p => p.isFolder && (p.id === fId || p.targetPath === fId || p.name === fId));
      if (match) openFolder(match);
    } else if (data.type === 'resize_folder' || data.action === 'resize_folder') {
      const fId = data.folderId;
      const newSize = ['1x1', '2x1', '1x2', '2x2'].includes(data.size) ? data.size : '1x1';
      const match = pinnedApps.find(p => p.isFolder && (p.id === fId || p.targetPath === fId || p.name === fId));
      if (match) {
        match.folderSize = newSize;
        persistAndRenderPinned();
      }
    } else if (data.type === 'resize_pinned_app' || data.action === 'resize_pinned_app') {
      const target = (data.target || '').toLowerCase();
      const name = (data.name || '').toLowerCase();
      const newSize = ['1x1', '2x1', '1x2', '2x2'].includes(data.size) ? data.size : '1x1';
      const match = pinnedApps.find(p => !p.isFolder && (
        (target && (p.targetPath || '').toLowerCase() === target) ||
        (name && (p.name || '').toLowerCase() === name)
      ));
      if (match) {
        match.tileSize = newSize;
        persistAndRenderPinned();
      }
    } else if (data.type === 'ungroup_folder' || data.action === 'ungroup_folder') {
      const fId = data.folderId;
      const fIdx = pinnedApps.findIndex(p => p.isFolder && (p.id === fId || p.targetPath === fId || p.name === fId));
      if (fIdx >= 0) {
        const unpacked = pinnedApps[fIdx].folderItems || [];
        pinnedApps.splice(fIdx, 1, ...unpacked);
        if (activeOpenFolder && (activeOpenFolder.id === fId || activeOpenFolder.targetPath === fId || activeOpenFolder.name === fId)) {
          closeFolder();
        }
        persistAndRenderPinned();
      }
    } else if (data.type === 'remove_from_folder' || data.action === 'remove_from_folder') {
      const fId = data.folderId;
      const target = data.target;
      const f = pinnedApps.find(p => p.isFolder && (p.id === fId || p.targetPath === fId || p.name === fId));
      if (f && Array.isArray(f.folderItems)) {
        const removed = f.folderItems.find(sub => sub.targetPath === target || sub.name === target);
        f.folderItems = f.folderItems.filter(sub => sub.targetPath !== target && sub.name !== target);
        if (removed) {
          delete removed._parentFolderId;
          const fIdx = pinnedApps.indexOf(f);
          if (fIdx >= 0) {
            pinnedApps.splice(fIdx + 1, 0, removed);
          } else {
            pinnedApps.push(removed);
          }
        }
        const ungrouped = checkFolderAutoUngroup(f);
        if (!ungrouped && activeOpenFolder === f) {
          renderFolderGrid(f);
        }
        persistAndRenderPinned();
      }
    } else if (data.type === 'launcher_closed' || data.action === 'launcher_closed') {
      closeFolder();
      if (win81OpenAnimTimer) {
        clearTimeout(win81OpenAnimTimer);
        win81OpenAnimTimer = null;
      }
      document.body.classList.remove('win81-opening');
      if (document.body.classList.contains('win81-fullscreen') &&
          !document.body.classList.contains('winzen-no-anim')) {
        document.body.classList.add('win81-preopen');
      }
    } else if (data.type === 'focus_search' || data.action === 'focus_search') {
      closeFolder();
      win81ActivePane = 'start';
      if (searchInput.value) {
        searchInput.value = '';
        updateView();
      } else {
        if (layoutVariant === 'win81' && win81SplitView) {
          updateView();
        } else {
          selectedIndex = -1;
          refreshSelected();
        }
      }
      const contentEl = document.querySelector('.launcher-content');
      if (contentEl) contentEl.scrollTop = 0;
      triggerWin81TileOpenAnimation();
      setTimeout(() => {
        searchInput.focus();
        searchInput.select();
      }, 30);
    }
  }

  let win81OpenAnimTimer = null;
  function triggerWin81TileOpenAnimation() {
    if (win81OpenAnimTimer) {
      clearTimeout(win81OpenAnimTimer);
      win81OpenAnimTimer = null;
    }
    document.body.classList.remove('win81-opening');
    if (!document.body.classList.contains('win81-fullscreen') ||
        document.body.classList.contains('winzen-no-anim')) {
      document.body.classList.remove('win81-preopen');
      return;
    }
    const tiles = document.querySelectorAll('.pinned-row .app-item, .recent-row .app-item, .apps-grid .app-item');
    tiles.forEach((tile, idx) => {
      tile.style.setProperty('--tile-idx', String(Math.min(idx, 15)));
    });
    void document.body.offsetWidth;
    document.body.classList.remove('win81-preopen');
    document.body.classList.add('win81-opening');
    win81OpenAnimTimer = setTimeout(() => {
      document.body.classList.remove('win81-opening');
      win81OpenAnimTimer = null;
    }, 500);
  }

  // Dismiss native context menu when scrolling inside app launcher or folder grid
  let lastScrollDismissAt = 0;
  const onLauncherScroll = () => {
    const now = Date.now();
    if (now - lastScrollDismissAt < 100) return;
    lastScrollDismissAt = now;
    sendHost('dismiss_context_menu');
  };
  document.addEventListener('scroll', onLauncherScroll, { capture: true, passive: true });
  document.addEventListener('wheel', onLauncherScroll, { passive: true });

  // Close folder dialog when launcher window loses focus or hides
  window.addEventListener('blur', () => {
    closeFolder();
  });
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) {
      closeFolder();
    }
  });

  // Request initial app catalog
  sendHost('request_apps');
})();
