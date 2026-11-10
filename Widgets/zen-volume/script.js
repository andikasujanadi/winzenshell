const btn = root.getElementById('btnVolume');
const viewIcon = root.getElementById('viewIcon');
const viewWidget = root.getElementById('viewWidget');
const slider = root.getElementById('volumeInput');
const valLabel = root.getElementById('sliderVal');
const btnSliderMute = root.getElementById('btnSliderMute');
const speakerPath = root.getElementById('speakerPath');
const sliderSpeakerPath = root.getElementById('sliderSpeakerPath');

const inControlCenter = typeof widgetConfig !== 'undefined' && widgetConfig && !!widgetConfig.inControlCenter;
const isWidgetMode = typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.displayMode === 'widget';

if (inControlCenter || isWidgetMode) {
  if (viewIcon) viewIcon.style.display = 'none';
  if (viewWidget) viewWidget.style.display = 'flex';
} else {
  if (viewWidget) viewWidget.style.display = 'none';
  if (viewIcon) viewIcon.style.display = 'inline-flex';
}

const PATH_HIGH = 'M11.536 14.01A8.473 8.473 0 0 0 14.026 8a8.47 8.47 0 0 0-2.49-6.01l-.708.707A7.476 7.476 0 0 1 13.025 8c0 2.071-.84 3.946-2.197 5.303l.708.707zM10.121 12.6A6.48 6.48 0 0 0 12.025 8a6.48 6.48 0 0 0-1.904-4.596l-.707.707A5.482 5.482 0 0 1 11.025 8a5.483 5.483 0 0 1-1.61 3.89l.706.71zM8.707 11.185A4.486 4.486 0 0 0 10.025 8a4.486 4.486 0 0 0-1.318-3.185l-.707.707A3.49 3.49 0 0 1 9.025 8a3.49 3.49 0 0 1-1.025 2.478l.707.707zM6.717 3.55A.5.5 0 0 1 7 4v8a.5.5 0 0 1-.812.39L3.825 10.5H1.5A.5.5 0 0 1 1 10V6a.5.5 0 0 1 .5-.5h2.325l2.363-1.89a.5.5 0 0 1 .529-.06z';
const PATH_MED = 'M10.121 12.6A6.48 6.48 0 0 0 12.025 8a6.48 6.48 0 0 0-1.904-4.596l-.707.707A5.482 5.482 0 0 1 11.025 8a5.483 5.483 0 0 1-1.61 3.89l.706.71zM8.707 11.185A4.486 4.486 0 0 0 10.025 8a4.486 4.486 0 0 0-1.318-3.185l-.707.707A3.49 3.49 0 0 1 9.025 8a3.49 3.49 0 0 1-1.025 2.478l.707.707zM6.717 3.55A.5.5 0 0 1 7 4v8a.5.5 0 0 1-.812.39L3.825 10.5H1.5A.5.5 0 0 1 1 10V6a.5.5 0 0 1 .5-.5h2.325l2.363-1.89a.5.5 0 0 1 .529-.06z';
const PATH_LOW = 'M8.707 11.185A4.486 4.486 0 0 0 10.025 8a4.486 4.486 0 0 0-1.318-3.185l-.707.707A3.49 3.49 0 0 1 9.025 8a3.49 3.49 0 0 1-1.025 2.478l.707.707zM6.717 3.55A.5.5 0 0 1 7 4v8a.5.5 0 0 1-.812.39L3.825 10.5H1.5A.5.5 0 0 1 1 10V6a.5.5 0 0 1 .5-.5h2.325l2.363-1.89a.5.5 0 0 1 .529-.06z';
const PATH_MUTE = 'M6.717 3.55A.5.5 0 0 1 7 4v8a.5.5 0 0 1-.812.39L3.825 10.5H1.5A.5.5 0 0 1 1 10V6a.5.5 0 0 1 .5-.5h2.325l2.363-1.89a.5.5 0 0 1 .529-.06zm7.137 2.096a.5.5 0 0 1 0 .708L12.207 8l1.647 1.646a.5.5 0 0 1-.708.708L11.5 8.707l-1.646 1.647a.5.5 0 0 1-.708-.708L10.793 8 9.146 6.354a.5.5 0 1 1 .708-.708L11.5 7.293l1.646-1.647a.5.5 0 0 1 .708 0z';

let currentVolume = 50;
let isCurrentlyMuted = false;

function updateVisuals(val, isMuted) {
  currentVolume = val;
  isCurrentlyMuted = !!isMuted;
  const effectiveVal = isMuted ? 0 : val;
  const pathData = isMuted || val === 0 ? PATH_MUTE : (val > 66 ? PATH_HIGH : (val > 33 ? PATH_MED : PATH_LOW));

  if (speakerPath) speakerPath.setAttribute('d', pathData);
  if (sliderSpeakerPath) sliderSpeakerPath.setAttribute('d', pathData);

  if (valLabel) {
    valLabel.textContent = isMuted ? 'Mute' : `${val}%`;
    valLabel.classList.toggle('is-muted', !!isMuted);
  }

  if (slider) {
    slider.value = effectiveVal;
    slider.style.background = `linear-gradient(to right, var(--winzen-accent, #60cdff) 0%, var(--winzen-accent, #60cdff) ${effectiveVal}%, var(--vol-track-bg, rgba(255, 255, 255, 0.42)) ${effectiveVal}%, var(--vol-track-bg, rgba(255, 255, 255, 0.42)) 100%)`;
  }

  if (btn) {
    btn.title = isMuted ? 'Volume: Muted (Scroll to adjust)' : `Volume: ${val}% (Scroll to adjust)`;
    btn.classList.toggle('is-muted', !!isMuted);
  }
}

function onVolumeWheel(ev) {
  ev.preventDefault();
  ev.stopPropagation();
  const step = ev.deltaY < 0 ? 3 : -3;
  let next = Math.max(0, Math.min(100, currentVolume + step));
  currentVolume = next;
  isCurrentlyMuted = false;
  updateVisuals(next, false);
  if (window.winzen && window.winzen.volume && typeof window.winzen.volume.set === 'function') {
    window.winzen.volume.set(next);
  } else if (window.winzen) {
    window.winzen.send('set_volume', { value: next });
  }
}

if (btn) {
  btn.addEventListener('wheel', onVolumeWheel, { passive: false });
}
if (viewWidget) {
  viewWidget.addEventListener('wheel', onVolumeWheel, { passive: false });
}

if (slider) {
  updateVisuals(Number(slider.value || 50), false);

  slider.addEventListener('input', (ev) => {
    ev.stopPropagation();
    const val = Number(slider.value);
    updateVisuals(val, false);
    if (window.winzen && window.winzen.volume && typeof window.winzen.volume.set === 'function') {
      window.winzen.volume.set(val);
    } else if (window.winzen) {
      window.winzen.send('set_volume', { value: val });
    }
  });
}

if (btnSliderMute) {
  btnSliderMute.addEventListener('click', (ev) => {
    ev.stopPropagation();
    if (window.winzen && window.winzen.volume && typeof window.winzen.volume.toggleMute === 'function') {
      window.winzen.volume.toggleMute();
    } else if (window.winzen) {
      window.winzen.send('toggle_mute', {});
    }
  });
}

if (btn) {
  btn.addEventListener('click', (ev) => {
    ev.stopPropagation();
    const rect = btn.getBoundingClientRect();
    if (window.winzen && window.winzen.volume && typeof window.winzen.volume.toggle === 'function') {
      window.winzen.volume.toggle(rect.left, rect.top, rect.width, rect.height);
    } else if (window.winzen) {
      window.winzen.send('toggle_volume_flyout', {
        x: rect.left,
        y: rect.top,
        width: rect.width,
        height: rect.height
      });
    }
  });
}

window.addEventListener('winzen:metrics', (e) => {
  const m = e.detail;
  if (!m || typeof m.volumePercent !== 'number') return;
  const current = m.volumePercent;
  const muted = !!m.isMuted;
  if (document.activeElement !== slider) {
    updateVisuals(current, muted);
  }
});
