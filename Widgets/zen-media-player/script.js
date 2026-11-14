(function () {
  const root = typeof document !== 'undefined' ? document : null;
  if (!root) return;

  const viewIcon = root.getElementById('viewIcon');
  const viewWidget = root.getElementById('viewWidget');
  const mediaContainer = root.getElementById('mediaContainer');
  const btnMediaIcon = root.getElementById('btnMediaIcon');
  const iconPlayingDot = root.getElementById('iconPlayingDot');

  const mediaArtWrap = root.getElementById('mediaArtWrap');
  const mediaArtImg = root.getElementById('mediaArtImg');
  const mediaArtPlaceholder = root.getElementById('mediaArtPlaceholder');
  const mediaTitle = root.getElementById('mediaTitle');
  const mediaArtist = root.getElementById('mediaArtist');
  const mediaProgressBar = root.getElementById('mediaProgressBar');
  const mediaProgressFill = root.getElementById('mediaProgressFill');
  const mediaProgressThumb = root.getElementById('mediaProgressThumb');

  const btnPrev = root.getElementById('btnPrev');
  const btnPlayPause = root.getElementById('btnPlayPause');
  const btnNext = root.getElementById('btnNext');
  const iconPlay = root.getElementById('iconPlay');
  const iconPause = root.getElementById('iconPause');

  let currentMedia = null;

  // Mode check
  const inControlCenter = typeof widgetConfig !== 'undefined' && widgetConfig && !!widgetConfig.inControlCenter;
  const isWidgetMode = typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.displayMode === 'widget';
  const isIconMode = typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.displayMode === 'icon';

  if (isIconMode && !inControlCenter) {
    if (viewIcon) viewIcon.style.display = 'flex';
    if (viewWidget) viewWidget.style.display = 'none';
  } else {
    if (viewIcon) viewIcon.style.display = 'none';
    if (viewWidget) viewWidget.style.display = 'flex';
  }

  // Thumbnail show/hide in taskbar option
  const showThumbInTaskbar = (typeof widgetConfig === 'undefined' || !widgetConfig || widgetConfig.showThumbnail !== false);
  if (!inControlCenter && !showThumbInTaskbar) {
    if (mediaArtWrap) mediaArtWrap.style.display = 'none';
  } else {
    if (mediaArtWrap) mediaArtWrap.style.display = 'flex';
  }

  // Aspect ratio adaptation
  const currentSpan = (typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.aspectRatio) ||
                      (viewWidget && (viewWidget.dataset.aspectRatio || viewWidget.dataset.span)) ||
                      (root.body && root.body.dataset.aspectRatio) || (inControlCenter ? '2x1' : 'taskbar');

  if (mediaContainer) {
    mediaContainer.setAttribute('data-aspect-ratio', currentSpan);
    mediaContainer.setAttribute('data-span', currentSpan);
  }
  if (viewWidget) {
    viewWidget.setAttribute('data-aspect-ratio', currentSpan);
    viewWidget.setAttribute('data-span', currentSpan);
  }

  function sendAction(action, data = {}) {
    if (window.winzen && typeof window.winzen.send === 'function') {
      window.winzen.send(action, data);
    } else if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage({ action, ...data });
    }
  }

  // Interactive controls
  if (btnPlayPause) {
    btnPlayPause.addEventListener('click', (ev) => {
      ev.stopPropagation();
      sendAction('media_play_pause');
    });
  }

  if (btnPrev) {
    btnPrev.addEventListener('click', (ev) => {
      ev.stopPropagation();
      sendAction('media_previous');
    });
  }

  if (btnNext) {
    btnNext.addEventListener('click', (ev) => {
      ev.stopPropagation();
      sendAction('media_next');
    });
  }

  if (btnMediaIcon) {
    btnMediaIcon.addEventListener('click', (ev) => {
      ev.stopPropagation();
      sendAction('media_play_pause');
    });
  }

  // Interactive Seekbar
  if (mediaProgressBar) {
    mediaProgressBar.addEventListener('click', (ev) => {
      ev.stopPropagation();
      if (!currentMedia || !currentMedia.hasMedia || !currentMedia.durationSeconds || currentMedia.durationSeconds <= 0) return;
      const rect = mediaProgressBar.getBoundingClientRect();
      if (rect.width <= 0) return;
      const clickX = Math.max(0, Math.min(rect.width, ev.clientX - rect.left));
      const pct = clickX / rect.width;
      const targetSeconds = pct * currentMedia.durationSeconds;
      if (mediaProgressFill) mediaProgressFill.style.width = (pct * 100) + '%';
      if (mediaProgressThumb) mediaProgressThumb.style.left = (pct * 100) + '%';
      if (window.winzen && window.winzen.media && typeof window.winzen.media.seek === 'function') {
        window.winzen.media.seek(targetSeconds);
      } else {
        sendAction('media_seek', { position: targetSeconds });
      }
    });
  }

  // State rendering
  function renderMediaState(metrics) {
    const media = (metrics && (metrics.media || metrics.Media)) || null;
    currentMedia = media;
    const hasMedia = !!(media && media.hasMedia);
    const isPlaying = !!(media && media.isPlaying);

    // Dynamic thumbnail show/hide update check
    const showThumb = (typeof widgetConfig === 'undefined' || !widgetConfig || widgetConfig.showThumbnail !== false);
    if (!inControlCenter && !showThumb) {
      if (mediaArtWrap) mediaArtWrap.style.display = 'none';
    } else {
      if (mediaArtWrap) mediaArtWrap.style.display = 'flex';
    }

    if (!hasMedia) {
      // Inactive / No Media state
      if (viewWidget) viewWidget.classList.add('no-media');
      if (mediaTitle) mediaTitle.textContent = 'No Media is Playing';
      if (mediaArtist) mediaArtist.textContent = '';
      if (mediaArtImg) {
        mediaArtImg.src = '';
        mediaArtImg.classList.remove('has-img');
      }
      if (mediaProgressFill) mediaProgressFill.style.width = '0%';
      if (mediaProgressThumb) mediaProgressThumb.style.left = '0%';

      // All media controls inactive, disabled, visible but cannot be clicked
      if (btnPrev) {
        btnPrev.disabled = true;
        btnPrev.style.pointerEvents = 'none';
      }
      if (btnPlayPause) {
        btnPlayPause.disabled = true;
        btnPlayPause.style.pointerEvents = 'none';
      }
      if (btnNext) {
        btnNext.disabled = true;
        btnNext.style.pointerEvents = 'none';
      }

      if (iconPlay) iconPlay.style.display = 'block';
      if (iconPause) iconPause.style.display = 'none';

      if (btnMediaIcon) {
        btnMediaIcon.classList.remove('is-playing');
        btnMediaIcon.title = 'No Media is Playing';
      }
      return;
    }

    // Active media state
    if (viewWidget) viewWidget.classList.remove('no-media');
    if (mediaTitle) mediaTitle.textContent = media.title || 'Unknown Title';
    if (mediaArtist) mediaArtist.textContent = media.artist || (media.albumTitle || '');

    if (media.thumbnailBase64 && media.thumbnailBase64.length > 20) {
      if (mediaArtImg) {
        mediaArtImg.src = media.thumbnailBase64;
        mediaArtImg.classList.add('has-img');
      }
    } else {
      if (mediaArtImg) {
        mediaArtImg.src = '';
        mediaArtImg.classList.remove('has-img');
      }
    }

    // Enable controls
    if (btnPrev) {
      btnPrev.disabled = false;
      btnPrev.style.pointerEvents = 'auto';
    }
    if (btnPlayPause) {
      btnPlayPause.disabled = false;
      btnPlayPause.style.pointerEvents = 'auto';
    }
    if (btnNext) {
      btnNext.disabled = false;
      btnNext.style.pointerEvents = 'auto';
    }

    // Play/Pause icon toggle
    if (isPlaying) {
      if (iconPlay) iconPlay.style.display = 'none';
      if (iconPause) iconPause.style.display = 'block';
      if (btnMediaIcon) btnMediaIcon.classList.add('is-playing');
    } else {
      if (iconPlay) iconPlay.style.display = 'block';
      if (iconPause) iconPause.style.display = 'none';
      if (btnMediaIcon) btnMediaIcon.classList.remove('is-playing');
    }

    if (btnMediaIcon) {
      btnMediaIcon.title = `${media.title || 'Playing'} - ${media.artist || ''}`;
    }

    // Progress bar
    if (media.durationSeconds > 0 && mediaProgressFill) {
      const pct = Math.min(100, Math.max(0, (media.positionSeconds / media.durationSeconds) * 100));
      mediaProgressFill.style.width = pct + '%';
      if (mediaProgressThumb) mediaProgressThumb.style.left = pct + '%';
    }
  }

  // Initial render with cached metrics
  if (window.winzen && window.winzen.metrics) {
    renderMediaState(window.winzen.metrics);
  } else {
    renderMediaState(null);
  }

  // Event listener for live updates
  window.addEventListener('winzen:metrics', (ev) => {
    if (ev && ev.detail) {
      renderMediaState(ev.detail);
    }
  });
})();
