const btn = root.getElementById('btnBrightness');
const viewIcon = root.getElementById('viewIcon');
const viewWidget = root.getElementById('viewWidget');
const slider = root.getElementById('brightnessInput');
const valLabel = root.getElementById('sliderVal');

const inControlCenter = typeof widgetConfig !== 'undefined' && widgetConfig && !!widgetConfig.inControlCenter;
const isWidgetMode = typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.displayMode === 'widget';

// In Control Center, always use widget mode (slider). On taskbar, respect displayMode ('icon' by default).
if (inControlCenter || isWidgetMode) {
  if (viewIcon) viewIcon.style.display = 'none';
  if (viewWidget) viewWidget.style.display = 'flex';
} else {
  if (viewWidget) viewWidget.style.display = 'none';
  if (viewIcon) viewIcon.style.display = 'inline-flex';
}

let currentBrightness = 100;

function updateVisuals(val) {
  currentBrightness = val;
  if (valLabel) valLabel.textContent = `${val}%`;
  if (slider) {
    slider.value = val;
    slider.style.background = `linear-gradient(to right, var(--winzen-accent, #60cdff) 0%, var(--winzen-accent, #60cdff) ${val}%, var(--bright-track-bg, rgba(255, 255, 255, 0.42)) ${val}%, var(--bright-track-bg, rgba(255, 255, 255, 0.42)) 100%)`;
  }
}

function onBrightnessWheel(ev) {
  ev.preventDefault();
  ev.stopPropagation();
  const step = ev.deltaY < 0 ? 3 : -3;
  let next = Math.max(0, Math.min(100, currentBrightness + step));
  currentBrightness = next;
  updateVisuals(next);
  if (window.winzen && window.winzen.brightness && typeof window.winzen.brightness.set === 'function') {
    window.winzen.brightness.set(next);
  } else if (window.winzen) {
    window.winzen.send('set_brightness', { value: next });
  }
}

if (btn) {
  btn.addEventListener('wheel', onBrightnessWheel, { passive: false });
}
if (viewWidget) {
  viewWidget.addEventListener('wheel', onBrightnessWheel, { passive: false });
}

if (slider) {
  updateVisuals(Number(slider.value || 100));

  slider.addEventListener('input', (ev) => {
    ev.stopPropagation();
    const val = Number(slider.value);
    updateVisuals(val);
    if (window.winzen && window.winzen.brightness && typeof window.winzen.brightness.set === 'function') {
      window.winzen.brightness.set(val);
    } else if (window.winzen) {
      window.winzen.send('set_brightness', { value: val });
    }
  });

  // Vertical drag support for 1x2 mode
  let isPointerDragging = false;
  const handleVerticalPointer = (ev) => {
    const isVertical = (typeof widgetConfig !== 'undefined' && widgetConfig && (widgetConfig.aspectRatio === '1x2' || (widgetConfig.colSpan === 1 && widgetConfig.rowSpan === 2)));
    if (!isVertical) return;
    const rect = slider.getBoundingClientRect();
    const clampedY = Math.max(rect.top, Math.min(rect.bottom, ev.clientY));
    const pct = Math.round(((rect.bottom - clampedY) / (rect.height || 1)) * 100);
    const val = Math.max(0, Math.min(100, pct));
    slider.value = val;
    updateVisuals(val);
    if (window.winzen && window.winzen.brightness && typeof window.winzen.brightness.set === 'function') {
      window.winzen.brightness.set(val);
    } else if (window.winzen) {
      window.winzen.send('set_brightness', { value: val });
    }
  };

  slider.addEventListener('pointerdown', (ev) => {
    const isVertical = (typeof widgetConfig !== 'undefined' && widgetConfig && (widgetConfig.aspectRatio === '1x2' || (widgetConfig.colSpan === 1 && widgetConfig.rowSpan === 2)));
    if (isVertical) {
      isPointerDragging = true;
      try { slider.setPointerCapture(ev.pointerId); } catch {}
      handleVerticalPointer(ev);
      ev.stopPropagation();
    }
  });

  slider.addEventListener('pointermove', (ev) => {
    if (isPointerDragging) {
      handleVerticalPointer(ev);
      ev.stopPropagation();
    }
  });

  const stopPointer = (ev) => {
    if (isPointerDragging) {
      isPointerDragging = false;
      try { slider.releasePointerCapture(ev.pointerId); } catch {}
      ev.stopPropagation();
    }
  };
  slider.addEventListener('pointerup', stopPointer);
  slider.addEventListener('pointercancel', stopPointer);
}

if (btn) {
  btn.addEventListener('click', (ev) => {
    ev.stopPropagation();
    const rect = btn.getBoundingClientRect();
    if (window.winzen && window.winzen.brightness && typeof window.winzen.brightness.toggle === 'function') {
      window.winzen.brightness.toggle(rect.left, rect.top, rect.width, rect.height);
    } else if (window.winzen) {
      window.winzen.send('toggle_brightness_flyout', {
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
  if (!m || typeof m.brightnessPercent !== 'number') return;
  const current = m.brightnessPercent;
  if (document.activeElement !== slider) {
    updateVisuals(current);
  }
});
