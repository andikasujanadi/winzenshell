(function () {
  let allApps = [];
  let recentApps = [];
  let pinnedApps = [];
  let visibleApps = [];
  let selectedIndex = -1;
  let lastAppsSig = '';

  const searchInput = document.getElementById('searchInput');
  const btnClearSearch = document.getElementById('btnClearSearch');
  const sectionPinned = document.getElementById('sectionPinned');
  const pinnedGrid = document.getElementById('pinnedGrid');
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
    item.title = app.name || '';
    item.addEventListener('dragstart', (e) => e.preventDefault());

    const iconWrap = document.createElement('div');
    iconWrap.className = 'app-icon-wrap';

    if (app.isFolder) {
      item.classList.add('pinned-folder-item');
      item.title = `${app.name || 'Folder'} (${(app.folderItems || []).length} apps)`;
      iconWrap.classList.add('folder-icon-wrap');

      const miniGrid = document.createElement('div');
      const count = (app.folderItems || []).length;
      miniGrid.className = 'folder-mini-grid' + (count === 1 ? ' count-1' : count === 2 ? ' count-2' : '');

      const previewItems = (app.folderItems || []).slice(0, 4);
      if (previewItems.length === 0) {
        iconWrap.innerHTML = getFolderFallbackSvg();
      } else {
        previewItems.forEach(sub => {
          if (sub.iconDataUrl) {
            const mImg = document.createElement('img');
            mImg.className = 'folder-mini-icon';
            mImg.draggable = false;
            mImg.src = sub.iconDataUrl;
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
    } else if (app.iconDataUrl) {
      const img = document.createElement('img');
      img.className = 'app-icon-img';
      img.draggable = false;
      img.decoding = 'async';
      img.src = app.iconDataUrl;
      img.alt = app.name || '';
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
          folderId: app.id || ''
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
          clientX: e.clientX,
          clientY: e.clientY
        });
      });
    }

    // Pointer-driven fluid real-time reordering & 1-second hover folder creation
    if (typeof pinnedIndex === 'number' && pinnedIndex >= 0) {
      item.dataset.pinnedIndex = String(pinnedIndex);
      setupPinnedPointerDrag(item, app, pinnedIndex);
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

      try {
        item.setPointerCapture(e.pointerId);
      } catch {}

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
                if (tgtApp.isFolder) {
                  tgtApp.folderItems = tgtApp.folderItems || [];
                  tgtApp.folderItems.push(srcApp);
                  pinnedApps.splice(actualSrcIdx, 1);
                } else {
                  const folder = {
                    id: 'folder_' + Date.now(),
                    name: 'Folder',
                    isFolder: true,
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

  function updateView() {
    const query = (searchInput.value || '').trim().toLowerCase();

    if (!query) {
      // Empty search: Show Pinned, Recent & All Apps
      btnClearSearch.style.display = 'none';
      if (sectionPinned) sectionPinned.style.display = pinnedApps.length > 0 ? 'block' : 'none';
      sectionRecent.style.display = recentApps.length > 0 ? 'block' : 'none';
      allAppsTitle.textContent = 'All Apps';
      visibleApps = allApps;
      selectedIndex = -1;
    } else {
      // Active query: Hide Pinned & Recent, filter All Apps
      btnClearSearch.style.display = 'flex';
      if (sectionPinned) sectionPinned.style.display = 'none';
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

    // Purge any lingering ghost drag clones
    document.querySelectorAll('.pinned-drag-ghost').forEach(el => el.remove());

    // Render Pinned row
    if (pinnedGrid) {
      pinnedGrid.innerHTML = '';
      pinnedApps.forEach((app, pIdx) => {
        pinnedGrid.appendChild(renderAppItem(app, false, true, pIdx, 'pinned'));
      });
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
    } else {
      visibleApps.forEach((app, idx) => {
        appsGrid.appendChild(renderAppItem(app, idx === selectedIndex, undefined, undefined, 'all'));
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
    handleNavigationOrEscape(e);
  });

  // Redirect typing anywhere in the launcher into the searchbar
  document.addEventListener('keydown', (e) => {
    // If folder modal is open, let the modal handle escape or title input
    if (folderOverlay && folderOverlay.style.display === 'flex') {
      if (e.key === 'Escape') {
        e.preventDefault();
        closeFolder();
      }
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
      searchInput.focus();
      searchInput.value += e.key;
      updateView();
    }
  });

  function handleNavigationOrEscape(e) {
    if (folderOverlay && folderOverlay.style.display === 'flex') {
      if (e.key === 'Escape') {
        e.preventDefault();
        closeFolder();
        return true;
      }
      return false;
    }

    const cols = 6;
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      if (visibleApps.length > 0) {
        selectedIndex = Math.min(selectedIndex + cols, visibleApps.length - 1);
        refreshSelected();
      }
      return true;
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      if (selectedIndex >= 0) {
        selectedIndex = Math.max(selectedIndex - cols, 0);
        refreshSelected();
      }
      return true;
    } else if (e.key === 'ArrowRight') {
      e.preventDefault();
      if (visibleApps.length > 0) {
        selectedIndex = Math.min(selectedIndex + 1, visibleApps.length - 1);
        refreshSelected();
      }
      return true;
    } else if (e.key === 'ArrowLeft') {
      e.preventDefault();
      if (selectedIndex > 0) {
        selectedIndex = Math.max(selectedIndex - 1, 0);
        refreshSelected();
      }
      return true;
    } else if (e.key === 'Enter') {
      e.preventDefault();
      if (selectedIndex >= 0 && visibleApps[selectedIndex]) {
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
      const nextPinned = Array.isArray(payload.pinned) ? payload.pinned : [];
      const sig = JSON.stringify({
        p: nextPinned.map((a) => `${a.isFolder ? 'f' : 'a'}|${a.id || a.targetPath}|${a.name}|${(a.folderItems || []).map(s => s.targetPath || s.name).join(',')}`),
        r: nextRecent.map((a) => `${a.targetPath}|${a.name}|${a.iconDataUrl ? `${a.iconDataUrl.length}:${a.iconDataUrl.slice(-24)}` : 0}`),
        a: nextAll.map((a) => `${a.targetPath}|${a.name}|${a.iconDataUrl ? `${a.iconDataUrl.length}:${a.iconDataUrl.slice(-24)}` : 0}`)
      });
      if (sig !== lastAppsSig) {
        lastAppsSig = sig;
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
    } else if (data.type === 'focus_search' || data.action === 'focus_search') {
      closeFolder();
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
