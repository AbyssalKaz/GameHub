'use strict';

const RESIZE_HANDLES = [
  { edge: 'top', hit: 12 },
  { edge: 'bottom', hit: 15 },
  { edge: 'left', hit: 10 },
  { edge: 'right', hit: 11 },
  { edge: 'top-left', hit: 13 },
  { edge: 'top-right', hit: 14 },
  { edge: 'bottom-left', hit: 16 },
  { edge: 'bottom-right', hit: 17 },
];

const NOT_DRAGGABLE = 'input, textarea, select, button, a, label, .search, .dropdown, .segmented, .zoom, .chip, .chip-group, .filters, .nav-item, .drive, [data-no-drag]';

const windowControl = {
  mode: 'normal',
  lastDown: 0,
  lastX: 0,
  lastY: 0,
};

function windowAction(action, extra = {}) {
  send({ type: 'window', action, ...extra });
}

function applyWindowMode(mode) {
  windowControl.mode = mode;
  document.body.classList.toggle('window-maximized', mode === 'maximized');
  document.body.classList.toggle('window-fullscreen', mode === 'fullscreen');
}

function applySidebarState() {
  document.querySelector('.app').classList.toggle('sidebar-collapsed', !!state.prefs.sidebarCollapsed);
}

function toggleSidebar() {
  state.prefs.sidebarCollapsed = !state.prefs.sidebarCollapsed;
  savePrefs();
  applySidebarState();
  renderSidebar();
}

document.body.insertAdjacentHTML('beforeend', `<div class="resize-handles">${RESIZE_HANDLES.map(handle => `<div class="resize-handle ${handle.edge}" data-hit="${handle.hit}"></div>`).join('')}</div>`);

document.addEventListener('mousedown', event => {
  if (event.button !== 0) return;

  const handle = event.target.closest('[data-hit]');
  if (handle) {
    event.preventDefault();
    windowAction('resize', { hit: Number(handle.dataset.hit) });
    return;
  }

  if (!event.target.closest('[data-drag]') || event.target.closest(NOT_DRAGGABLE)) return;
  event.preventDefault();

  const now = performance.now();
  const isDoubleClick = now - windowControl.lastDown < 420 &&
    Math.abs(event.screenX - windowControl.lastX) < 6 &&
    Math.abs(event.screenY - windowControl.lastY) < 6;

  if (isDoubleClick) {
    windowControl.lastDown = 0;
    windowAction('maximize');
    return;
  }

  windowControl.lastDown = now;
  windowControl.lastX = event.screenX;
  windowControl.lastY = event.screenY;
  if (windowControl.mode !== 'fullscreen') windowAction('drag');
});

window.addEventListener('keydown', event => {
  if (event.key === 'F11') {
    event.preventDefault();
    windowAction('fullscreen');
    return;
  }

  if (event.ctrlKey && !event.shiftKey && !event.altKey && event.key.toLowerCase() === 'b') {
    event.preventDefault();
    event.stopImmediatePropagation();
    toggleSidebar();
    return;
  }

  if (event.key === 'Escape' && windowControl.mode === 'fullscreen') {
    const busy = state.selectedId || state.search ||
      document.querySelector('.context-menu.open, .dropdown.open, .modal-layer.open, .osk-layer.open');
    if (!busy) {
      event.preventDefault();
      event.stopImmediatePropagation();
      windowAction('fullscreen');
    }
  }
}, true);

elements.sidebar.addEventListener('click', event => {
  if (event.target.closest('[data-sidebar-toggle]')) toggleSidebar();
});

extensions.messageHandlers.push(message => {
  if (message.type === 'windowMode') applyWindowMode(message.mode);
});

applySidebarState();
