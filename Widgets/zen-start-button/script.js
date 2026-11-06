const startBtn = root.getElementById('startBtn');
const iconSlot = root.getElementById('startIconSlot');
const textSlot = root.getElementById('startTextSlot');

const ICONS = {
  windows: `<span class="win-grid"><i></i><i></i><i></i><i></i></span>`,
  win10: `<svg viewBox="0 0 16 16" fill="#60cdff"><path d="M0 2.22 6.47 1.34v6.29H0V2.22zm0 6.07h6.47v6.37L0 13.78V8.29zm7.25-7.07L16 0v7.63H7.25V1.22zm8.75 7.07v7.71l-8.75-1.2v-6.51H16z"/></svg>`,
  arch: `<svg viewBox="0 0 24 24" fill="#1793d1"><path d="M12 2.5C11.5 3.5 6 15 2 21.5c4-2 7-2 10-2s6 0 10 2c-4-6.5-9.5-18-10-19zm0 6.2c1.7 3.5 3.5 7.2 4.6 9.8-1.5-.4-3.1-.6-4.6-.6s-3.1.2-4.6.6c1.1-2.6 2.9-6.3 4.6-9.8z"/></svg>`,
  ubuntu: `<svg viewBox="0 0 24 24" fill="#e95420"><circle cx="12" cy="12" r="10"/><circle cx="12" cy="4" r="1.8" fill="#ffffff"/><circle cx="5" cy="16" r="1.8" fill="#ffffff"/><circle cx="19" cy="16" r="1.8" fill="#ffffff"/><path d="M12 6.5a5.5 5.5 0 0 0-4.8 2.8l2.2 1.3a3 3 0 0 1 5.2 0l2.2-1.3A5.5 5.5 0 0 0 12 6.5zm-5.8 4.2a5.5 5.5 0 0 0 0 2.6l2.3-.8a3 3 0 0 1 0-1l-2.3-.8zm11.6 0-2.3.8a3 3 0 0 1 0 1l2.3.8a5.5 5.5 0 0 0 0-2.6zM8.5 17.5a5.5 5.5 0 0 0 7 0l-1.6-2a3 3 0 0 1-3.8 0l-1.6 2z" fill="#ffffff"/></svg>`,
  debian: `<svg viewBox="0 0 24 24" fill="#d70a53"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1.2 4.2c2.8.5 4.8 2.8 4.6 5.6-.2 2.2-1.8 4-4 4.3-1.8.2-3.4-.8-4-2.4-.6-1.5-.2-3.2.9-4.3 1-.9 2.5-1.2 3.8-.7.8.3 1.5.9 1.8 1.7.3.7.2 1.5-.2 2.1-.4.6-1.1 1-1.8 1-.5 0-1-.3-1.3-.7-.2-.3-.3-.7-.1-1.1.1-.3.4-.5.7-.6.2 0 .4.1.5.3.1.2.1.4 0 .5-.1.1-.2.2-.4.2"/></svg>`,
  fedora: `<svg viewBox="0 0 24 24" fill="#294172"><circle cx="12" cy="12" r="10"/><path d="M14.5 7.5a3.5 3.5 0 0 0-3.5 3.5V13H8v2h3v4h2v-4h3.5v-2H13v-2a1.5 1.5 0 0 1 1.5-1.5H16V7.5h-1.5z" fill="#3c6eb4"/><circle cx="15.5" cy="10" r="1.2" fill="#ffffff"/></svg>`,
  tux: `<svg viewBox="0 0 24 24" fill="#f5c211"><path d="M12 2c-3 0-4.5 2.5-4.5 6 0 2 .5 3.5 1.5 4.5-.5 1-2 2-3 4-1.2 2.4-.5 4.5 1.5 4.5 1.5 0 2.5-.8 3.5-2 1 .5 2 .8 3 .8s2-.3 3-.8c1 1.2 2 2 3.5 2 2 0 2.7-2.1 1.5-4.5-1-2-2.5-3-3-4 1-1 1.5-2.5 1.5-4.5 0-3.5-1.5-6-4.5-6z" fill="#ffffff"/><path d="M12 2c-2.5 0-4 2-4 5.5 0 2 .5 3.5 1.5 4.5C9 13 8 14 7 16c-.8 1.6-.3 3 1 3s2.5-.8 3-1.5c.7.3 1.3.5 2 .5s1.3-.2 2-.5c.5.7 1.7 1.5 3 1.5 1.3 0 1.8-1.4 1-3-1-2-2-3-2.5-4 1-1 1.5-2.5 1.5-4.5C18 4 16.5 2 12 2z" fill="#222222"/><circle cx="10.5" cy="6" r="1" fill="#ffffff"/><circle cx="10.5" cy="6" r=".5" fill="#000000"/><circle cx="13.5" cy="6" r="1" fill="#ffffff"/><circle cx="13.5" cy="6" r=".5" fill="#000000"/><path d="M10 8c0 1.5 1 2.5 2 2.5s2-1 2-2.5l-2-.5-2 .5z" fill="#f5c211"/></svg>`,
  apple: `<svg viewBox="0 0 24 24" fill="#ffffff"><path d="M18.71 19.5c-.83 1.24-1.71 2.45-3.05 2.47-1.34.03-1.77-.79-3.29-.79-1.53 0-2 .77-3.27.82-1.31.05-2.3-1.32-3.14-2.53C4.25 17 2.94 12.45 4.7 9.39c.87-1.52 2.43-2.48 4.12-2.51 1.28-.02 2.5.87 3.29.87.78 0 2.26-1.07 3.81-.91.65.03 2.47.26 3.64 1.98-.09.06-2.17 1.28-2.15 3.81.03 3.02 2.65 4.03 2.68 4.04-.03.07-.42 1.44-1.38 2.83M15.97 4.54c.64-.78 1.08-1.86.96-2.95-.93.04-2.06.62-2.73 1.4-.58.68-1.1 1.77-.96 2.83 1.04.08 2.09-.5 2.73-1.28z"/></svg>`,
  zen: `<svg viewBox="0 0 24 24" fill="#60cdff"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 2c4.41 0 8 3.59 8 8 0 2.1-.81 4.02-2.15 5.46L12 12.83V4zm-1 9.17l-5.85 5.85C4.31 16.58 3.5 14.39 3.5 12c0-4.41 3.59-8 8-8v9.17h-.5zM12 14.24l4.95 4.95C15.65 19.69 13.9 20.2 12 20.2c-1.9 0-3.65-.51-4.95-1.01L12 14.24z"/></svg>`
};

const iconKey = (typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.startIcon) || 'windows';
const customText = (typeof widgetConfig !== 'undefined' && widgetConfig && widgetConfig.startText) || 'Start';
const showText = typeof widgetConfig !== 'undefined' && widgetConfig ? !!widgetConfig.showStartText : false;

if (iconSlot) {
  iconSlot.innerHTML = ICONS[iconKey] || ICONS.windows;
}

if (textSlot) {
  textSlot.textContent = customText;
  textSlot.style.display = showText ? 'inline' : 'none';
}

if (startBtn) {
  if (!showText) {
    startBtn.classList.add('icon-only');
  } else {
    startBtn.classList.remove('icon-only');
  }
  startBtn.title = customText || 'Start';

  startBtn.addEventListener('click', () => {
    window.winzen.shell.openStartMenu();
  });
}
