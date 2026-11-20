function initTaskbar() {
  const slotLeft = document.getElementById('slot-left');
  const slotCenter = document.getElementById('slot-center');
  const slotRight = document.getElementById('slot-right');
  const btnOpenStudio = document.getElementById('btnOpenStudio');
  const taskbarBar = document.getElementById('taskbarBar');
  const taskbarBlurCanvas = document.getElementById('taskbarBlurCanvas');
  const taskbarWallpaperLayer = document.getElementById('taskbarWallpaperLayer');
  const taskbarSurfaceLayer = document.getElementById('taskbarSurfaceLayer');

  // --- WebGL2 GPU Dual Kawase Blur Engine ---
  let blurSharedBytes = null;
  let gl = null;
  let kawaseDownProg = null;
  let kawaseUpProg = null;
  let quadVao = null;
  let srcTex = null;
  let srcTexW = 0;
  let srcTexH = 0;
  let fboPyramid = [];
  let fboBaseW = 0;
  let fboBaseH = 0;

  function initKawaseGl() {
    if (gl || !taskbarBlurCanvas) return !!gl;
    gl = taskbarBlurCanvas.getContext('webgl2', {
      alpha: false,
      antialias: false,
      depth: false,
      stencil: false,
      preserveDrawingBuffer: true,
      powerPreference: 'high-performance'
    });
    if (!gl) return false;

    const vsSrc = `#version 300 es
      layout(location = 0) in vec2 aPos;
      uniform float uFlipY;
      out vec2 vUv;
      void main() {
        vec2 uv = aPos * 0.5 + 0.5;
        vUv = vec2(uv.x, mix(uv.y, 1.0 - uv.y, uFlipY));
        gl_Position = vec4(aPos, 0.0, 1.0);
      }`;

    const downFsSrc = `#version 300 es
      precision highp float;
      in vec2 vUv;
      out vec4 fragColor;
      uniform sampler2D uTex;
      uniform vec2 uHalfPixel;
      void main() {
        vec4 sum = texture(uTex, vUv) * 4.0;
        sum += texture(uTex, vUv - uHalfPixel);
        sum += texture(uTex, vUv + uHalfPixel);
        sum += texture(uTex, vUv + vec2(uHalfPixel.x, -uHalfPixel.y));
        sum += texture(uTex, vUv - vec2(uHalfPixel.x, -uHalfPixel.y));
        fragColor = vec4((sum * 0.125).rgb, 1.0);
      }`;

    const upFsSrc = `#version 300 es
      precision highp float;
      in vec2 vUv;
      out vec4 fragColor;
      uniform sampler2D uTex;
      uniform vec2 uHalfPixel;
      uniform float uSaturate;
      void main() {
        vec4 sum = texture(uTex, vUv + vec2(-uHalfPixel.x * 2.0, 0.0));
        sum += texture(uTex, vUv + vec2(-uHalfPixel.x, uHalfPixel.y)) * 2.0;
        sum += texture(uTex, vUv + vec2(0.0, uHalfPixel.y * 2.0));
        sum += texture(uTex, vUv + vec2(uHalfPixel.x, uHalfPixel.y)) * 2.0;
        sum += texture(uTex, vUv + vec2(uHalfPixel.x * 2.0, 0.0));
        sum += texture(uTex, vUv + vec2(uHalfPixel.x, -uHalfPixel.y)) * 2.0;
        sum += texture(uTex, vUv + vec2(0.0, -uHalfPixel.y * 2.0));
        sum += texture(uTex, vUv + vec2(-uHalfPixel.x, -uHalfPixel.y)) * 2.0;
        vec3 rgb = (sum / 12.0).rgb;
        float luma = dot(rgb, vec3(0.2126, 0.7152, 0.0722));
        rgb = clamp(mix(vec3(luma), rgb, uSaturate), 0.0, 1.0);
        fragColor = vec4(rgb, 1.0);
      }`;

    function compileProgram(vsText, fsText) {
      const vs = gl.createShader(gl.VERTEX_SHADER);
      gl.shaderSource(vs, vsText);
      gl.compileShader(vs);
      const fs = gl.createShader(gl.FRAGMENT_SHADER);
      gl.shaderSource(fs, fsText);
      gl.compileShader(fs);
      const prog = gl.createProgram();
      gl.attachShader(prog, vs);
      gl.attachShader(prog, fs);
      gl.linkProgram(prog);
      gl.deleteShader(vs);
      gl.deleteShader(fs);
      return {
        prog,
        uFlipY: gl.getUniformLocation(prog, 'uFlipY'),
        uTex: gl.getUniformLocation(prog, 'uTex'),
        uHalfPixel: gl.getUniformLocation(prog, 'uHalfPixel'),
        uSaturate: gl.getUniformLocation(prog, 'uSaturate')
      };
    }

    kawaseDownProg = compileProgram(vsSrc, downFsSrc);
    kawaseUpProg = compileProgram(vsSrc, upFsSrc);

    quadVao = gl.createVertexArray();
    gl.bindVertexArray(quadVao);
    const vbo = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 1, -1, -1, 1, 1, 1]), gl.STATIC_DRAW);
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
    gl.bindVertexArray(null);

    srcTex = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, srcTex);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    return true;
  }

  function ensureKawasePyramid(baseW, baseH) {
    if (fboBaseW === baseW && fboBaseH === baseH && fboPyramid.length === 4) return;
    fboPyramid.forEach((lvl) => {
      gl.deleteFramebuffer(lvl.fbo);
      gl.deleteTexture(lvl.tex);
    });
    fboPyramid = [];
    fboBaseW = baseW;
    fboBaseH = baseH;

    let w = baseW;
    let h = baseH;
    for (let i = 0; i < 4; i++) {
      w = Math.max(2, Math.floor(w / 2));
      h = Math.max(2, Math.floor(h / 2));
      const tex = gl.createTexture();
      gl.bindTexture(gl.TEXTURE_2D, tex);
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, w, h, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

      const fbo = gl.createFramebuffer();
      gl.bindFramebuffer(gl.FRAMEBUFFER, fbo);
      gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, tex, 0);
      fboPyramid.push({ fbo, tex, w, h });
    }
    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
  }

  function renderKawaseBlurFrame(w, h, blurStrength) {
    if (!blurSharedBytes || w <= 0 || h <= 0 || blurStrength <= 0) return;
    const byteLen = w * h * 4;
    if (blurSharedBytes.byteLength < byteLen) return;
    if (!initKawaseGl()) return;

    if (taskbarBlurCanvas.width !== w || taskbarBlurCanvas.height !== h) {
      taskbarBlurCanvas.width = w;
      taskbarBlurCanvas.height = h;
    }

    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, srcTex);
    const view = blurSharedBytes.subarray(0, byteLen);
    if (srcTexW !== w || srcTexH !== h) {
      srcTexW = w;
      srcTexH = h;
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, w, h, 0, gl.RGBA, gl.UNSIGNED_BYTE, view);
    } else {
      gl.texSubImage2D(gl.TEXTURE_2D, 0, 0, 0, w, h, gl.RGBA, gl.UNSIGNED_BYTE, view);
    }

    ensureKawasePyramid(w, h);

    const passes = blurStrength <= 45 ? 1 : 2;
    const offset = 0.85 + (blurStrength / 100.0) * 1.9;

    gl.bindVertexArray(quadVao);

    // 1. Downsample passes
    gl.useProgram(kawaseDownProg.prog);
    gl.uniform1i(kawaseDownProg.uTex, 0);
    let prevTex = srcTex;
    let prevW = w;
    let prevH = h;
    for (let i = 0; i < passes; i++) {
      const lvl = fboPyramid[i];
      gl.bindFramebuffer(gl.FRAMEBUFFER, lvl.fbo);
      gl.viewport(0, 0, lvl.w, lvl.h);
      gl.bindTexture(gl.TEXTURE_2D, prevTex);
      gl.uniform1f(kawaseDownProg.uFlipY, i === 0 ? 1.0 : 0.0);
      gl.uniform2f(kawaseDownProg.uHalfPixel, (0.5 / prevW) * offset, (0.5 / prevH) * offset);
      gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
      prevTex = lvl.tex;
      prevW = lvl.w;
      prevH = lvl.h;
    }

    // 2. Upsample passes
    gl.useProgram(kawaseUpProg.prog);
    gl.uniform1i(kawaseUpProg.uTex, 0);
    gl.uniform1f(kawaseUpProg.uFlipY, 0.0);
    for (let i = passes - 2; i >= 0; i--) {
      const dstLvl = fboPyramid[i];
      gl.bindFramebuffer(gl.FRAMEBUFFER, dstLvl.fbo);
      gl.viewport(0, 0, dstLvl.w, dstLvl.h);
      gl.bindTexture(gl.TEXTURE_2D, prevTex);
      gl.uniform1f(kawaseUpProg.uSaturate, 1.0);
      gl.uniform2f(kawaseUpProg.uHalfPixel, (0.5 / prevW) * offset, (0.5 / prevH) * offset);
      gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
      prevTex = dstLvl.tex;
      prevW = dstLvl.w;
      prevH = dstLvl.h;
    }

    // Final upsample pass directly to canvas with saturation boost
    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    gl.viewport(0, 0, w, h);
    gl.bindTexture(gl.TEXTURE_2D, prevTex);
    gl.uniform1f(kawaseUpProg.uSaturate, 1.15);
    gl.uniform2f(kawaseUpProg.uHalfPixel, (0.5 / prevW) * offset, (0.5 / prevH) * offset);
    gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
    gl.bindVertexArray(null);
  }

  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('sharedbufferreceived', (e) => {
      try {
        blurSharedBytes = new Uint8Array(e.getBuffer());
      } catch (err) {
        console.error('SharedBuffer error:', err);
      }
    });
    window.chrome.webview.addEventListener('message', (e) => {
      const msg = e.data;
      if (msg && msg.type === 'blur_frame') {
        renderKawaseBlurFrame(msg.w, msg.h, msg.blur);
      }
    });
  }

  let lastWidgetsSignature = '';
  let currentTheme = 'dark';
  let currentAccent = '#3684E3';
  let currentRadius = 12;
  let currentDockEdge = 'bottom';
  let currentAnimations = true;

  let currentDynamicIslandConfig = {
    enabled: false,
    placement: 'taskbar',
    taskbarSlot: 'center',
    timeFormat: '24h',
    showSeconds: false,
    showDate: true,
    enableAnimation: true,
    enableMusicIsland: true
  };
  let dynamicIslandEl = null;
  let dynamicIslandClockTimer = null;
  let isDynamicIslandExpanded = false;
  let lastMediaSnapshot = null;
  let lastMediaTrackKey = '';
  let isMusicWidePillMode = false;
  let isMusicLyricsPinnedMode = false;
  let globalDateFormat = 'DD-MM-YYYY';

  function formatDateWithTemplate(date, template) {
    const tpl = (template && String(template).trim()) ? String(template) : 'DD-MM-YYYY';
    const d = date instanceof Date ? date : new Date();
    const year = String(d.getFullYear());
    const monthIdx = d.getMonth();
    const dayNum = d.getDate();
    const map = {
      YYYY: year,
      YY: year.slice(-2),
      MMMM: d.toLocaleDateString('en-US', { month: 'long' }),
      MMM: d.toLocaleDateString('en-US', { month: 'short' }),
      MM: String(monthIdx + 1).padStart(2, '0'),
      M: String(monthIdx + 1),
      dddd: d.toLocaleDateString('en-US', { weekday: 'long' }),
      ddd: d.toLocaleDateString('en-US', { weekday: 'short' }),
      DD: String(dayNum).padStart(2, '0'),
      D: String(dayNum)
    };
    return tpl.replace(/YYYY|YY|MMMM|MMM|MM|M|dddd|ddd|DD|D/g, (m) => map[m] ?? m);
  }

  function updateDynamicIslandMedia(media) {
    const musicEl = document.getElementById('shell-dynamic-island-music');
    if (!musicEl) return;
    const isEnabled = currentDynamicIslandConfig.enabled &&
                      currentDynamicIslandConfig.enableMusicIsland !== false &&
                      currentDynamicIslandConfig.placement !== 'top-center';
    const noAnim = currentDynamicIslandConfig.enableAnimation === false || !currentAnimations;

    if (!isEnabled || !media || !media.hasMedia) {
      lastMediaTrackKey = '';
      if (isMusicLyricsPinnedMode) {
        isMusicLyricsPinnedMode = false;
        musicEl.classList.remove('is-lyrics-pill');
        musicEl.style.width = isMusicWidePillMode ? '210px' : '';
      }
      musicEl.classList.remove('is-hovered');
      if (noAnim || !isEnabled) {
        musicEl.classList.add('is-music-absent');
        musicEl.style.display = 'none';
      } else if (musicEl.style.display !== 'none') {
        musicEl.classList.add('is-music-absent');
      }
      return;
    }

    const trackKey = `${(media.title || '').toLowerCase()}|${(media.artist || '').toLowerCase()}`;
    const isFinished = (media.durationSeconds > 0 && media.positionSeconds >= media.durationSeconds - 0.3 && !media.isPlaying);
    if ((lastMediaTrackKey && trackKey !== lastMediaTrackKey) || isFinished) {
      if (isMusicLyricsPinnedMode) {
        isMusicLyricsPinnedMode = false;
        musicEl.classList.remove('is-lyrics-pill');
        if (isMusicWidePillMode) {
          musicEl.classList.add('is-wide-pill');
          musicEl.classList.add('is-hovered');
          musicEl.style.width = '210px';
        } else {
          musicEl.classList.remove('is-wide-pill');
          musicEl.classList.remove('is-hovered');
          musicEl.style.width = '';
        }
      }
    }
    lastMediaTrackKey = trackKey;

    if (musicEl.style.display === 'none') {
      if (noAnim) {
        musicEl.classList.remove('is-music-absent');
        musicEl.style.display = 'inline-flex';
      } else {
        musicEl.classList.add('is-music-absent');
        musicEl.style.display = 'inline-flex';
        void musicEl.offsetWidth;
        requestAnimationFrame(() => {
          musicEl.classList.remove('is-music-absent');
        });
      }
    } else {
      musicEl.classList.remove('is-music-absent');
    }

    if (isMusicWidePillMode && !isMusicLyricsPinnedMode) {
      musicEl.classList.add('is-wide-pill');
      musicEl.classList.add('is-hovered');
      musicEl.style.width = '210px';
    }

    const thumbImg = document.getElementById('tbDiMusicThumb');
    const thumbPlaceholder = document.getElementById('tbDiMusicThumbPlaceholder');
    if (thumbImg && thumbPlaceholder) {
      if (media.thumbnailBase64 && media.thumbnailBase64.length > 30) {
        thumbImg.src = media.thumbnailBase64;
        thumbImg.style.display = 'block';
        thumbPlaceholder.style.display = 'none';
      } else {
        thumbImg.style.display = 'none';
        thumbPlaceholder.style.display = 'block';
      }
    }

    const titleEl = document.getElementById('tbDiMusicTitle');
    if (titleEl && !isMusicLyricsPinnedMode) {
      titleEl.textContent = media.title || 'Playing';
    }

    const waveEl = document.getElementById('tbDiMusicWaveform');
    if (waveEl) {
      if (media.isPlaying) {
        waveEl.classList.add('playing');
      } else {
        waveEl.classList.remove('playing');
      }
    }
  }

  function createDynamicIslandElement() {
    const wrapper = document.createElement('div');
    wrapper.id = 'shell-dynamic-island-wrapper';
    wrapper.className = 'shell-dynamic-island-wrapper';

    // 1. Clock Pill
    const el = document.createElement('div');
    el.id = 'shell-dynamic-island';
    el.className = 'shell-dynamic-island pitch-black';
    if (currentDynamicIslandConfig.enableAnimation === false) {
      el.classList.add('no-anim');
    }
    el.setAttribute('role', 'button');
    el.setAttribute('tabindex', '0');
    el.setAttribute('aria-label', 'Dynamic Island Clock');

    const colDiv = document.createElement('div');
    colDiv.className = 'di-collapsed-content';

    const timeSpan = document.createElement('span');
    timeSpan.className = 'di-time';
    timeSpan.id = 'tbDiTime';

    const secSpan = document.createElement('span');
    secSpan.className = 'di-seconds';
    secSpan.id = 'tbDiSeconds';

    const sepSpan = document.createElement('span');
    sepSpan.className = 'di-sep';
    sepSpan.id = 'tbDiSep';

    const dateSpan = document.createElement('span');
    dateSpan.className = 'di-date';
    dateSpan.id = 'tbDiDate';

    colDiv.appendChild(timeSpan);
    colDiv.appendChild(secSpan);
    colDiv.appendChild(sepSpan);
    colDiv.appendChild(dateSpan);
    el.appendChild(colDiv);

    let musicHoverTimer = null;

    function triggerExpand(target) {
      clearTimeout(musicHoverTimer);
      if (!isMusicWidePillMode) {
        musicEl.style.transition = 'none';
        musicEl.classList.remove('is-hovered');
        void musicEl.offsetWidth;
        musicEl.style.transition = '';
      }

      const hasMusicPill = musicEl.style.display !== 'none' && !musicEl.classList.contains('is-music-absent');
      const clockRect = el.getBoundingClientRect();
      const wrapRect = wrapper.getBoundingClientRect();
      const clockW = Math.round(clockRect.width) || 78;
      const musicRect = musicEl.getBoundingClientRect();
      const musicW = Math.round(musicRect.width) || (isMusicWidePillMode ? 210 : 68);
      const effectiveW = hasMusicPill ? (clockW + 8 + musicW) : clockW;

      if (window.winzen && typeof window.winzen.send === 'function') {
        window.winzen.send('toggle_dynamic_island_expand', {
          target,
          x: wrapRect.left,
          y: clockRect.top,
          width: effectiveW,
          height: clockRect.height,
          clockWidth: clockW,
          musicWidth: hasMusicPill ? musicW : 0,
          hasMusicPill: hasMusicPill,
          isMusicWide: isMusicWidePillMode,
          slot: currentDynamicIslandConfig.taskbarSlot || 'center'
        });
      }
    }

    window.winzenUnmergeDynamicIsland = () => {
      wrapper.classList.remove('merging');
    };

    el.addEventListener('click', (ev) => {
      ev.stopPropagation();
      triggerExpand('clock');
    });

    el.addEventListener('keydown', (ev) => {
      if (ev.key === 'Enter' || ev.key === ' ') {
        ev.preventDefault();
        ev.stopPropagation();
        triggerExpand('clock');
      }
    });

    wrapper.appendChild(el);

    // 2. Music Pill
    const musicEl = document.createElement('div');
    musicEl.id = 'shell-dynamic-island-music';
    musicEl.className = 'shell-dynamic-island-music pitch-black';
    musicEl.setAttribute('role', 'button');
    musicEl.setAttribute('tabindex', '0');
    musicEl.setAttribute('aria-label', 'Now Playing');
    musicEl.style.display = 'none';

    const thumbContainer = document.createElement('div');
    thumbContainer.className = 'di-music-thumb-container';

    const thumbImg = document.createElement('img');
    thumbImg.id = 'tbDiMusicThumb';
    thumbImg.className = 'di-music-thumb';
    thumbImg.alt = 'Album Art';
    thumbImg.style.display = 'none';

    const thumbPlaceholder = document.createElement('div');
    thumbPlaceholder.id = 'tbDiMusicThumbPlaceholder';
    thumbPlaceholder.className = 'di-music-thumb-placeholder';

    thumbContainer.appendChild(thumbImg);
    thumbContainer.appendChild(thumbPlaceholder);

    const titleSpan = document.createElement('span');
    titleSpan.id = 'tbDiMusicTitle';
    titleSpan.className = 'di-music-title';
    titleSpan.textContent = 'Song Title';

    const waveDiv = document.createElement('div');
    waveDiv.id = 'tbDiMusicWaveform';
    waveDiv.className = 'di-music-waveform';
    for (let i = 0; i < 5; i++) {
      const bar = document.createElement('span');
      bar.className = 'di-wave-bar';
      waveDiv.appendChild(bar);
    }

    musicEl.appendChild(thumbContainer);
    musicEl.appendChild(titleSpan);
    musicEl.appendChild(waveDiv);

    window.winzenSetMusicLyrics = (lyricText, isPinned, dynamicW) => {
      isMusicLyricsPinnedMode = !!isPinned;
      if (!musicEl) return;

      if (isMusicLyricsPinnedMode && lyricText && lyricText.trim().length > 0) {
        musicEl.classList.add('is-lyrics-pill');
        if (titleSpan) {
          titleSpan.textContent = lyricText;
        }
        const targetW = (dynamicW && dynamicW > 70) ? Math.min(340, Math.max(96, Math.round(dynamicW))) : calculateTextWidth(lyricText);
        musicEl.style.width = `${targetW}px`;
      } else {
        musicEl.classList.remove('is-lyrics-pill');
        if (isMusicWidePillMode) {
          musicEl.classList.add('is-wide-pill');
          musicEl.classList.add('is-hovered');
          musicEl.style.width = '210px';
        } else {
          musicEl.classList.remove('is-wide-pill');
          musicEl.classList.remove('is-hovered');
          musicEl.style.width = '';
        }
        if (titleSpan) {
          const fallback = lyricText || (lastMediaSnapshot && lastMediaSnapshot.title) || 'Playing';
          titleSpan.textContent = fallback;
        }
      }

      if (lastMediaSnapshot) {
        updateDynamicIslandMedia(lastMediaSnapshot);
      }
    };

    let _lyricMeasureCanvas = null;
    function calculateTextWidth(text) {
      if (!text) return 100;
      if (!_lyricMeasureCanvas) {
        _lyricMeasureCanvas = document.createElement('canvas');
      }
      const ctx = _lyricMeasureCanvas.getContext('2d');
      if (ctx) {
        ctx.font = '600 12px -apple-system, BlinkMacSystemFont, "Segoe UI Variable Display", "Segoe UI", system-ui, sans-serif';
        const textW = Math.ceil(ctx.measureText(text).width);
        return Math.min(340, Math.max(96, textW + 78));
      }
      const estimated = Math.round(text.length * 6.5 + 78);
      return Math.min(340, Math.max(96, estimated));
    }

    window.winzenSetMusicWideMode = (wide) => {
      isMusicWidePillMode = !!wide;
      if (musicEl) {
        if (isMusicWidePillMode) {
          musicEl.classList.add('is-wide-pill');
          musicEl.classList.add('is-hovered');
          if (!isMusicLyricsPinnedMode) {
            musicEl.style.width = '210px';
          }
          if (lastMediaSnapshot) {
            updateDynamicIslandMedia(lastMediaSnapshot);
          }
        } else {
          musicEl.classList.remove('is-wide-pill');
          musicEl.classList.remove('is-hovered');
          if (!isMusicLyricsPinnedMode) {
            musicEl.style.width = '';
          }
        }
      }
    };

    window.winzenIsMusicWideMode = () => isMusicWidePillMode;

    if (isMusicWidePillMode) {
      window.winzenSetMusicWideMode(true);
    }

    musicEl.addEventListener('mouseenter', () => {
      if (wrapper.classList.contains('island-hidden') || isMusicWidePillMode || isMusicLyricsPinnedMode) return;
      clearTimeout(musicHoverTimer);
      musicHoverTimer = setTimeout(() => {
        if (!wrapper.classList.contains('island-hidden') && !isMusicWidePillMode && !isMusicLyricsPinnedMode) {
          musicEl.classList.add('is-hovered');
        }
      }, 260);
    });

    musicEl.addEventListener('mouseleave', () => {
      if (isMusicWidePillMode || isMusicLyricsPinnedMode) return;
      clearTimeout(musicHoverTimer);
      musicEl.classList.remove('is-hovered');
    });

    musicEl.addEventListener('click', (ev) => {
      ev.stopPropagation();
      triggerExpand('music');
    });

    musicEl.addEventListener('keydown', (ev) => {
      if (ev.key === 'Enter' || ev.key === ' ') {
        ev.preventDefault();
        ev.stopPropagation();
        triggerExpand('music');
      }
    });

    wrapper.appendChild(musicEl);

    if (lastMediaSnapshot) {
      updateDynamicIslandMedia(lastMediaSnapshot);
    }

    return wrapper;
  }

  function toggleDynamicIslandExpand(force) {
    if (!dynamicIslandEl) return;
    const shouldExpand = (typeof force === 'boolean') ? force : !isDynamicIslandExpanded;
    if (isDynamicIslandExpanded === shouldExpand) return;
    isDynamicIslandExpanded = shouldExpand;

    if (currentDynamicIslandConfig.enableAnimation === false) {
      dynamicIslandEl.classList.add('no-anim');
    } else {
      dynamicIslandEl.classList.remove('no-anim');
    }

    if (shouldExpand) {
      dynamicIslandEl.classList.add('expanded');
      updateDynamicIslandClock();
    } else {
      dynamicIslandEl.classList.remove('expanded');
    }
  }

  // Collapse overlay on click outside or escape
  document.addEventListener('click', (ev) => {
    if (window.winzen && typeof window.winzen.send === 'function') {
      window.winzen.send('taskbar_clicked_outside');
    }
  });

  document.addEventListener('keydown', (ev) => {
    if (ev.key === 'Escape') {
      if (window.winzen && typeof window.winzen.send === 'function') {
        window.winzen.send('taskbar_clicked_outside');
      }
    }
  });

  function updateDynamicIslandClock() {
    if (!dynamicIslandEl) return;
    const timeSpan = dynamicIslandEl.querySelector('#tbDiTime');
    const secSpan = dynamicIslandEl.querySelector('#tbDiSeconds');
    const sepSpan = dynamicIslandEl.querySelector('#tbDiSep');
    const dateSpan = dynamicIslandEl.querySelector('#tbDiDate');

    const expTime = dynamicIslandEl.querySelector('#tbDiExpTime');
    const expSec = dynamicIslandEl.querySelector('#tbDiExpSeconds');
    const expDate = dynamicIslandEl.querySelector('#tbDiExpDate');
    const expBadge = dynamicIslandEl.querySelector('#tbDiExpBadge');

    const now = new Date();
    const is12h = currentDynamicIslandConfig.timeFormat === '12h';

    let hours = now.getHours();
    let ampm = '';
    if (is12h) {
      ampm = hours >= 12 ? ' PM' : ' AM';
      hours = hours % 12;
      if (hours === 0) hours = 12;
    }

    const minStr = String(now.getMinutes()).padStart(2, '0');
    const hrStr = is12h ? String(hours) : String(hours).padStart(2, '0');
    const secStr = `.${String(now.getSeconds()).padStart(2, '0')}`;

    const formattedDate = formatDateWithTemplate(now, globalDateFormat);

    // Collapsed view clock
    if (timeSpan) timeSpan.textContent = `${hrStr}:${minStr}${ampm}`;

    if (secSpan) {
      if (currentDynamicIslandConfig.showSeconds) {
        secSpan.style.display = 'inline';
        secSpan.textContent = secStr;
      } else {
        secSpan.style.display = 'none';
      }
    }

    if (dateSpan) {
      if (currentDynamicIslandConfig.showDate) {
        dateSpan.style.display = 'inline';
        dateSpan.textContent = formattedDate;
      } else {
        dateSpan.style.display = 'none';
      }
    }
    if (sepSpan) {
      sepSpan.style.display = 'none';
    }

    // Expanded view clock and date
    if (expTime) expTime.textContent = `${hrStr}:${minStr}${ampm}`;
    if (expSec) expSec.textContent = secStr;
    if (expDate) expDate.textContent = now.toLocaleDateString([], { month: 'short', day: 'numeric', year: 'numeric' });
    if (expBadge) expBadge.textContent = now.toLocaleDateString([], { weekday: 'long' }).toUpperCase();
  }

  if (btnOpenStudio) {
    btnOpenStudio.addEventListener('click', () => {
      window.winzen.shell.openSettings();
    });
  }

  window.addEventListener('winzen:state', (e) => {
    const state = e.detail;
    if (!state || !Array.isArray(state.widgets)) return;

    const settings = state.settings || {};
    currentTheme = settings.theme || 'dark';
    currentAccent = settings.accentColor || '#3684E3';
    currentRadius = settings.taskbarCornerRadiusPx ?? 12;

    document.documentElement.setAttribute('data-theme', currentTheme);
    document.body.className = currentTheme === 'light' ? 'theme-light' : 'theme-dark';
    document.documentElement.style.setProperty('--winzen-accent', currentAccent);
    document.documentElement.style.setProperty('--winzen-border-accent', currentAccent);
    document.documentElement.style.setProperty('--win11-accent', currentAccent);

    const isFloating = (settings.floatingMarginPx || 0) > 0;
    const isSeparated = (settings.taskbarStyle || settings.TaskbarStyle || 'unified').toLowerCase() === 'separated';
    const isFloatingRounded = isFloating && currentRadius > 0;
    const bgOpacityPct = Math.max(0, Math.min(100, settings.backgroundOpacity ?? 100));
    const bgBlurPct = Math.max(0, Math.min(100, settings.backgroundBlurStrength ?? 60));
    const bgAlpha = (bgOpacityPct / 100).toFixed(2);
    const isLightTheme = currentTheme === 'light';
    const surfaceBg = isLightTheme ? `rgba(243, 243, 243, ${bgAlpha})` : `rgba(28, 28, 28, ${bgAlpha})`;
    const htmlBg = (bgOpacityPct >= 100 && !isFloatingRounded && !isSeparated) ? (isLightTheme ? '#f3f3f3' : '#1c1c1c') : 'transparent';
    const activeWinWidget = state.widgets.find((w) => w.id === 'community.zen-active-windows');
    const appIconSizePx = activeWinWidget && typeof activeWinWidget.iconSizePx === 'number'
      ? Math.max(12, Math.min(32, activeWinWidget.iconSizePx))
      : 18;
    document.documentElement.style.setProperty('--winzen-app-icon-size', `${appIconSizePx}px`);

    const tbHeight = settings.taskbarHeightPx ?? 48;
    const effectiveRadius = Math.min(currentRadius, tbHeight / 2);
    const activeEffRadius = isSeparated ? effectiveRadius : (isFloating ? effectiveRadius : 0);
    const btnH = Math.max(36, appIconSizePx + 18);
    const vGap = Math.max(2, (tbHeight - btnH) / 2);
    const dynamicPadX = Math.round(6 + Math.max(0, activeEffRadius - 10) * 0.18);
    const innerBtnRadius = activeEffRadius > 6
      ? Math.min(Math.floor(btnH / 2), Math.max(4, Math.round(activeEffRadius - vGap)))
      : 4;

    document.documentElement.style.setProperty('--taskbar-surface-bg', surfaceBg);
    document.documentElement.style.setProperty('--taskbar-html-bg', htmlBg);
    document.documentElement.style.setProperty('--taskbar-sep-radius', `${effectiveRadius}px`);
    document.documentElement.style.setProperty('--taskbar-pad-x', `${dynamicPadX}px`);
    document.documentElement.style.setProperty('--taskbar-inner-radius', `${innerBtnRadius}px`);

    const borderAlpha = bgOpacityPct === 0 ? 0 : +(0.08 * (bgOpacityPct / 100)).toFixed(4);
    const borderTopColor = borderAlpha <= 0.0001
      ? 'transparent'
      : (isLightTheme ? `rgba(0, 0, 0, ${borderAlpha})` : `rgba(255, 255, 255, ${borderAlpha})`);
    const borderBottomColor = borderAlpha <= 0.0001
      ? 'transparent'
      : (isLightTheme ? `rgba(0, 0, 0, ${borderAlpha})` : `rgba(255, 255, 255, ${borderAlpha})`);
    document.documentElement.style.setProperty('--taskbar-border-top', borderTopColor);
    document.documentElement.style.setProperty('--taskbar-border-bottom', borderBottomColor);
    document.documentElement.style.setProperty('--taskbar-sep-border', borderTopColor);

    const isTransparentWithBlur = bgOpacityPct < 100 && bgBlurPct > 0;
    const useRealtimeBlur = isTransparentWithBlur && (settings.blurEngine === 'realtime' || settings.BlurEngine === 'realtime');
    if (taskbarBlurCanvas) {
      taskbarBlurCanvas.style.display = useRealtimeBlur ? 'block' : 'none';
      if (useRealtimeBlur && blurSharedBytes && srcTexW > 0 && srcTexH > 0) {
        renderKawaseBlurFrame(srcTexW, srcTexH, bgBlurPct);
      }
    }
    const backdropUrl = state.backdropImageUrl || '';
    if (taskbarWallpaperLayer) {
      if (isTransparentWithBlur && backdropUrl) {
        taskbarWallpaperLayer.style.display = 'block';
        taskbarWallpaperLayer.style.backgroundImage = `url("${backdropUrl}")`;
        taskbarWallpaperLayer.style.setProperty('--taskbar-wallpaper-blur', `${(bgBlurPct * 0.24).toFixed(1)}px`);
      } else {
        taskbarWallpaperLayer.style.display = 'none';
        taskbarWallpaperLayer.style.backgroundImage = '';
      }
    }

    const isTop = (settings.dockEdge || 'Bottom').toLowerCase() === 'top';
    currentDockEdge = isTop ? 'top' : 'bottom';
    currentAnimations = settings.enableAnimations !== false;
    globalDateFormat = settings.dateFormat || settings.DateFormat || globalDateFormat;

    const diConfig = settings.dynamicIsland || settings.DynamicIsland || {
      enabled: false,
      placement: 'taskbar',
      taskbarSlot: 'center',
      timeFormat: '24h',
      showSeconds: false,
      showDate: true,
      enableAnimation: true,
      enableMusicIsland: true
    };
    currentDynamicIslandConfig = diConfig;
    const wantWide = !!diConfig.widePill;
    isMusicWidePillMode = wantWide;
    if (typeof window.winzenSetMusicWideMode === 'function') {
      window.winzenSetMusicWideMode(wantWide);
    }
    if (!diConfig.pinLyrics && isMusicLyricsPinnedMode && typeof window.winzenSetMusicLyrics === 'function') {
      window.winzenSetMusicLyrics('', false, 0);
    }
    if (dynamicIslandEl) {
      if (currentDynamicIslandConfig.enableAnimation === false) {
        dynamicIslandEl.classList.add('no-anim');
      } else {
        dynamicIslandEl.classList.remove('no-anim');
      }
    }
    if (lastMediaSnapshot) {
      updateDynamicIslandMedia(lastMediaSnapshot);
    }

    document.body.classList.toggle('winzen-no-anim', !currentAnimations);
    if (taskbarBar) {
      taskbarBar.classList.toggle('floating', isFloating);
      taskbarBar.classList.toggle('separated', isSeparated);
      taskbarBar.classList.toggle('dock-top', isTop);
      scheduleSeparatedBlurClip();
      taskbarBar.style.setProperty('--taskbar-radius', `${isFloating ? effectiveRadius : 0}px`);
      if (bgOpacityPct === 0) {
        taskbarBar.style.borderTopColor = 'transparent';
        taskbarBar.style.borderBottomColor = 'transparent';
      } else {
        taskbarBar.style.borderTopColor = '';
        taskbarBar.style.borderBottomColor = '';
      }
    }

    // Update accent, dock edge, and animations flag on existing widget hosts
    document.querySelectorAll('.widget-host').forEach(host => {
      host.setAttribute('data-dock', currentDockEdge);
      host.setAttribute('data-animations', currentAnimations ? 'on' : 'off');
      host.style.setProperty('--winzen-accent', currentAccent);
      host.style.setProperty('--winzen-border-accent', currentAccent);
      host.style.setProperty('--win11-accent', currentAccent);
      host.style.setProperty('--winzen-radius-xs', `${innerBtnRadius}px`);
    });

    const enabledWidgets = state.widgets
      .filter((w) => w.enabled && (w.slot || '').toLowerCase() !== 'unused' && (w.slot || '').toLowerCase() !== 'disabled')
      .sort((a, b) => (a.order ?? 10) - (b.order ?? 10));
    const sig = JSON.stringify({
      theme: currentTheme,
      accent: currentAccent,
      animations: currentAnimations,
      dock: currentDockEdge,
      di: currentDynamicIslandConfig,
      widgets: enabledWidgets.map((w) => ({
        id: w.id,
        slot: w.slot,
        order: w.order ?? 10,
        width: w.width,
        autoWidth: w.autoWidth !== false,
        displayMode: w.displayMode || 'widget',
        aspectRatio: w.aspectRatio || '1:1',
        startIcon: w.startIcon || 'windows',
        startIconColorMode: w.startIconColorMode || 'text',
        startText: w.startText || 'Start',
        showStartText: !!w.showStartText,
        batteryIconStyle: w.batteryIconStyle || 'win11',
        batteryPercentMode: w.batteryPercentMode || 'outside-right',
        showSeconds: w.showSeconds !== false,
        showDate: w.showDate !== false,
        showCpu: w.showCpu !== false,
        showRam: w.showRam !== false,
        showGpu: w.showGpu !== false,
        html: w.htmlContent,
        css: w.cssContent,
        js: w.jsContent
      }))
    });

    if (sig !== lastWidgetsSignature) {
      lastWidgetsSignature = sig;
      mountWidgets(enabledWidgets);
    }
  });

  function mountWidgets(widgets) {
    slotLeft.innerHTML = '';
    slotCenter.innerHTML = '';
    slotRight.innerHTML = '';

    const isDiActiveInTaskbar = currentDynamicIslandConfig &&
      currentDynamicIslandConfig.enabled &&
      (currentDynamicIslandConfig.placement === 'taskbar' || currentDockEdge === 'top');
    const diSlot = ((currentDynamicIslandConfig && currentDynamicIslandConfig.taskbarSlot) || 'center').toLowerCase();

    widgets.forEach((w) => {
      const slot = (w.slot || 'right').toLowerCase();
      // Skip widgets placed in the control center or not in use from the taskbar zones
      if (slot === 'control-center' || slot === 'unused' || slot === 'disabled') return;

      // RULE 1: If Dynamic Island is active in the center, all widgets on the center become inactive (suppressed from taskbar)!
      if (isDiActiveInTaskbar && diSlot === 'center' && slot === 'center') {
        return;
      }

      const isAutoWidth = w.autoWidth !== false;
      const isShrinkableStrip = w.id === 'community.zen-active-windows';
      const isIconMode = (w.displayMode || 'widget') === 'icon';

      const host = document.createElement('div');
      host.className = `widget-host theme-${currentTheme}`;
      host.dataset.mode = w.displayMode || 'widget';
      host.dataset.inControlCenter = 'false';
      host.dataset.theme = currentTheme;
      host.setAttribute('data-theme', currentTheme);
      host.setAttribute('data-dock', currentDockEdge);
      host.setAttribute('data-animations', currentAnimations ? 'on' : 'off');
      host.style.width = isAutoWidth ? 'auto' : (isIconMode ? '34px' : `${w.width || 160}px`);
      host.dataset.widgetId = w.id;
      host.style.setProperty('--winzen-accent', currentAccent);
      host.style.setProperty('--winzen-border-accent', currentAccent);
      host.style.setProperty('--win11-accent', currentAccent);
      const curInnerR = document.documentElement.style.getPropertyValue('--taskbar-inner-radius') || '4px';
      host.style.setProperty('--winzen-radius-xs', curInnerR);

      const shadow = host.attachShadow({ mode: 'open' });

      const isLightHost = currentTheme === 'light';
      const defText = isLightHost ? '#1b1b1b' : '#ffffff';
      const defSecondary = isLightHost ? '#5c5c5c' : '#c5c5c5';
      const defSurface = isLightHost ? '#ffffff' : '#252525';
      const defHover = isLightHost ? 'rgba(0, 0, 0, 0.05)' : 'rgba(255, 255, 255, 0.0605)';
      const defSubtle = isLightHost ? '#f9f9f9' : '#202020';
      const defBorder = isLightHost ? 'rgba(0, 0, 0, 0.082)' : 'rgba(255, 255, 255, 0.08)';

      const styleEl = document.createElement('style');
      styleEl.textContent = `
        :host {
          --winzen-bg: ${isLightHost ? '#f3f3f3' : '#1c1c1c'};
          --winzen-surface: ${defSurface};
          --winzen-surface-hover: ${defHover};
          --winzen-surface-active: rgba(255, 255, 255, 0.04);
          --winzen-surface-subtle: ${defSubtle};
          --winzen-border: ${defBorder};
          --winzen-border-subtle: rgba(255, 255, 255, 0.05);
          --winzen-border-accent: ${currentAccent};
          --winzen-text: ${defText};
          --winzen-text-secondary: ${defSecondary};
          --winzen-text-muted: #888888;
          --winzen-accent: ${currentAccent};
          --winzen-accent-hover: ${currentAccent};
          --winzen-accent-pressed: ${currentAccent};
          --winzen-purple: #c084fc;
          --winzen-danger: #f87171;
          --winzen-radius-xs: var(--taskbar-inner-radius, 4px);
          --winzen-radius-sm: 6px;
          --winzen-radius: 10px;
          --winzen-radius-lg: 14px;
          --winzen-radius-pill: 9999px;
          --winzen-font: 'Segoe UI Variable Text', 'Segoe UI', system-ui, -apple-system, sans-serif;

          display: inline-flex;
          align-items: center;
          width: ${isAutoWidth ? (isShrinkableStrip ? 'auto' : 'max-content') : '100%'};
          max-width: ${isShrinkableStrip ? '100%' : 'none'};
          min-width: ${isShrinkableStrip ? '0' : 'auto'};
          height: max(36px, calc(var(--winzen-app-icon-size, 18px) + 18px));
          max-height: 100%;
          overflow: visible;
          font-family: var(--winzen-font);
          color: var(--winzen-text);
        }

        :host([data-theme="light"]), :host(.theme-light) {
          --winzen-bg: #f3f3f3;
          --winzen-surface: #ffffff;
          --winzen-surface-hover: rgba(0, 0, 0, 0.045);
          --winzen-surface-active: rgba(0, 0, 0, 0.075);
          --winzen-surface-subtle: #f9f9f9;
          --winzen-border: rgba(0, 0, 0, 0.082);
          --winzen-border-subtle: rgba(0, 0, 0, 0.05);
          --winzen-border-accent: ${currentAccent};
          --winzen-text: #1b1b1b;
          --winzen-text-secondary: #5c5c5c;
          --winzen-text-muted: #8a8a8a;
          --winzen-accent: ${currentAccent};
          --winzen-accent-hover: ${currentAccent};
          --winzen-accent-pressed: ${currentAccent};
          --winzen-purple: #7c3aed;
          --winzen-danger: #dc2626;
          color: var(--winzen-text);
        }
        ${w.cssContent || ''}

        :host([data-animations="off"]) *,
        :host([data-animations="off"]) *::before,
        :host([data-animations="off"]) *::after {
          animation: none !important;
          transition: none !important;
        }
        :host([data-animations="off"]) *:active {
          transform: none !important;
        }
      `;
      shadow.appendChild(styleEl);

      const wrapper = document.createElement('div');
      wrapper.className = `widget-taskbar-wrapper theme-${currentTheme}`;
      wrapper.dataset.inControlCenter = 'false';
      wrapper.dataset.theme = currentTheme;
      wrapper.setAttribute('data-theme', currentTheme);
      wrapper.style.cssText = `width:${isAutoWidth ? (isShrinkableStrip ? 'auto' : 'max-content') : '100%'};${isShrinkableStrip ? 'max-width:100%;min-width:0;' : ''}height:max(36px, calc(var(--winzen-app-icon-size, 18px) + 18px));max-height:100%;display:inline-flex;align-items:center;`;
      wrapper.innerHTML = w.htmlContent || '';
      shadow.appendChild(wrapper);

      if (slot === 'left') {
        slotLeft.appendChild(host);
      } else if (slot === 'center') {
        slotCenter.appendChild(host);
      } else {
        slotRight.appendChild(host);
      }

      if (w.jsContent) {
        try {
          shadow.createElement = (...args) => window.document.createElement(...args);
          const taskbarConfig = {
            ...w,
            inControlCenter: false,
            aspectRatio: 'taskbar',
            displayMode: w.displayMode || 'widget',
            dockEdge: currentDockEdge,
            animations: currentAnimations
          };
          const fn = new Function('root', 'document', 'winzen', 'widgetConfig', w.jsContent);
          fn(shadow, shadow, window.winzen, taskbarConfig);
        } catch (err) {
          console.error(`Widget script error (${w.id}):`, err);
        }
      }
    });

    if (isDiActiveInTaskbar) {
      if (!dynamicIslandEl) {
        dynamicIslandEl = createDynamicIslandElement();
      }
      dynamicIslandEl.classList.remove('island-hidden');
      const innerClock = dynamicIslandEl.querySelector('#shell-dynamic-island');
      if (innerClock) innerClock.classList.remove('island-hidden');

      if (diSlot === 'center') {
        // Exclusively occupies center
        slotCenter.innerHTML = '';
        slotCenter.appendChild(dynamicIslandEl);
      } else if (diSlot === 'left') {
        // RULE 2: If on the left, Dynamic Island should be the FIRST item!
        slotLeft.prepend(dynamicIslandEl);
      } else if (diSlot === 'right') {
        // RULE 3: If on the right, Dynamic Island should be the LAST item!
        slotRight.appendChild(dynamicIslandEl);
      }

      updateDynamicIslandClock();
      if (lastMediaSnapshot) {
        updateDynamicIslandMedia(lastMediaSnapshot);
      }
      if (!dynamicIslandClockTimer) {
        dynamicIslandClockTimer = setInterval(updateDynamicIslandClock, 1000);
      }
    } else {
      if (dynamicIslandClockTimer) {
        clearInterval(dynamicIslandClockTimer);
        dynamicIslandClockTimer = null;
      }
      dynamicIslandEl = null;
    }

    const leftEmpty = slotLeft.children.length === 0;
    const centerEmpty = slotCenter.children.length === 0;
    const centerNoBg = !!(isDiActiveInTaskbar && diSlot === 'center');
    const rightEmpty = slotRight.children.length === 0;
    slotLeft.classList.toggle('is-empty', leftEmpty);
    slotCenter.classList.toggle('is-empty', centerEmpty);
    slotCenter.classList.toggle('no-island-bg', centerNoBg);
    slotRight.classList.toggle('is-empty', rightEmpty);
    const rightWrapperEl = document.querySelector('.right-wrapper');
    if (rightWrapperEl) {
      rightWrapperEl.classList.toggle('is-empty', rightEmpty);
    }

    scheduleSeparatedBlurClip();

    if (window.winzen.metrics) {
      window.dispatchEvent(new CustomEvent('winzen:metrics', { detail: window.winzen.metrics }));
    }
  }

  let _sepClipRaf = 0;
  function updateSeparatedBlurClip() {
    const clipEl = document.getElementById('separatedBlurClip');
    if (!clipEl || !taskbarBar) return;
    if (!taskbarBar.classList.contains('separated')) {
      clipEl.innerHTML = '';
      return;
    }

    const barRect = taskbarBar.getBoundingClientRect();
    const rawR = parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--taskbar-sep-radius'));
    const r = Number.isFinite(rawR) ? Math.max(0, rawR) : 12;

    const islands = [];
    if (slotLeft && !slotLeft.classList.contains('is-empty')) islands.push(slotLeft);
    if (slotCenter && !slotCenter.classList.contains('is-empty') && !slotCenter.classList.contains('no-island-bg')) islands.push(slotCenter);
    const rw = document.querySelector('.right-wrapper');
    if (rw && (!rw.classList.contains('is-empty') || rw.matches(':hover') || rw.matches(':focus-within'))) {
      islands.push(rw);
    }

    const rectsHtml = islands.map((el) => {
      const rect = el.getBoundingClientRect();
      if (rect.width <= 0 || rect.height <= 0) return '';
      const x = (rect.left - barRect.left).toFixed(2);
      const y = (rect.top - barRect.top).toFixed(2);
      const w = rect.width.toFixed(2);
      const h = rect.height.toFixed(2);
      const rx = Math.min(r, rect.width / 2, rect.height / 2).toFixed(2);
      return `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${rx}" ry="${rx}"/>`;
    }).join('');
    clipEl.innerHTML = rectsHtml;
  }

  function scheduleSeparatedBlurClip() {
    if (_sepClipRaf) cancelAnimationFrame(_sepClipRaf);
    _sepClipRaf = requestAnimationFrame(() => {
      _sepClipRaf = 0;
      updateSeparatedBlurClip();
    });
  }

  if (typeof ResizeObserver !== 'undefined') {
    const sepRo = new ResizeObserver(() => scheduleSeparatedBlurClip());
    if (slotLeft) sepRo.observe(slotLeft);
    if (slotCenter) sepRo.observe(slotCenter);
    const rw = document.querySelector('.right-wrapper');
    if (rw) {
      sepRo.observe(rw);
      rw.addEventListener('mouseenter', scheduleSeparatedBlurClip);
      rw.addEventListener('mouseleave', scheduleSeparatedBlurClip);
    }
  }
  window.addEventListener('resize', scheduleSeparatedBlurClip);

  window.addEventListener('winzen:metrics', (e) => {
    if (e.detail && e.detail.media) {
      lastMediaSnapshot = e.detail.media;
      updateDynamicIslandMedia(e.detail.media);
    }
    scheduleSeparatedBlurClip();
  });

  window.winzen.send('request_state');
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initTaskbar);
} else {
  initTaskbar();
}
