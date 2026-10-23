'use strict';

const PadLogic = (() => {
  const STANDARD_BUTTONS = ['a', 'b', 'x', 'y', 'lb', 'rb', 'lt', 'rt', 'view', 'start', 'ls', 'rs', 'up', 'down', 'left', 'right', 'guide'];
  const PRESS_BUTTONS = ['a', 'b', 'x', 'y', 'lb', 'rb', 'lt', 'rt', 'start', 'ls', 'rs', 'guide'];
  const DIRECTIONS = ['up', 'down', 'left', 'right'];
  const STICK_ON = 0.55;
  const STICK_OFF = 0.35;
  const TRIGGER_ON = 0.5;
  const SCROLL_DEADZONE = 0.22;
  const FIRST_REPEAT = 380;
  const NEXT_REPEAT = 95;

  function emptyState() {
    return { pressed: {}, direction: null, repeatAt: 0, viewChord: false };
  }

  function readButtons(buttons) {
    const pressed = {};
    STANDARD_BUTTONS.forEach((name, index) => {
      const button = buttons[index];
      if (button == null) return;
      if (typeof button === 'object') {
        pressed[name] = name === 'lt' || name === 'rt' ? button.pressed || button.value > TRIGGER_ON : !!button.pressed;
      } else {
        pressed[name] = typeof button === 'number' ? button > TRIGGER_ON : !!button;
      }
    });
    return pressed;
  }

  function stickDirection(x, y, previous) {
    const threshold = previous ? STICK_OFF : STICK_ON;
    if (Math.max(Math.abs(x), Math.abs(y)) < threshold) return null;
    if (Math.abs(x) > Math.abs(y)) return x < 0 ? 'left' : 'right';
    return y < 0 ? 'up' : 'down';
  }

  function padStep(previous, buttons, axes, now) {
    const pressed = readButtons(buttons || []);
    const was = previous.pressed || {};
    const events = [];

    const dpad = DIRECTIONS.find(direction => pressed[direction]) || null;
    const direction = dpad || stickDirection(Number(axes[0]) || 0, Number(axes[1]) || 0, previous.direction);
    let repeatAt = previous.repeatAt;
    if (direction && direction !== previous.direction) {
      events.push({ name: direction });
      repeatAt = now + FIRST_REPEAT;
    } else if (direction && now >= repeatAt) {
      events.push({ name: direction, repeat: true });
      repeatAt = now + NEXT_REPEAT;
    }

    let viewChord = previous.viewChord;
    if (pressed.view && !was.view) viewChord = false;
    for (const name of PRESS_BUTTONS) {
      if (!pressed[name] || was[name]) continue;
      if (pressed.view && (name === 'lb' || name === 'rb')) {
        viewChord = true;
        events.push({ name: name === 'lb' ? 'volumeDown' : 'volumeUp' });
        continue;
      }
      events.push({ name });
    }
    if (!pressed.view && was.view && !viewChord) events.push({ name: 'view' });

    const scroll = Number(axes[3]) || 0;
    if (Math.abs(scroll) > SCROLL_DEADZONE) {
      events.push({ name: 'scroll', amount: Math.sign(scroll) * (Math.abs(scroll) - SCROLL_DEADZONE) / (1 - SCROLL_DEADZONE) });
    }

    const active = !!direction || Object.values(pressed).some(Boolean) || Math.abs(scroll) > SCROLL_DEADZONE;
    return { state: { pressed, direction, repeatAt, viewChord }, events, active };
  }

  function padKind(id) {
    const text = String(id || '').toLowerCase();
    if (/054c|dualsense|dualshock|playstation|wireless controller/.test(text)) return 'playstation';
    if (/045e|xbox|xinput/.test(text)) return 'xbox';
    return 'generic';
  }

  function padModel(id) {
    const text = String(id || '').toLowerCase();
    if (/dualsense|0ce6|0df2/.test(text)) return 'dualsense';
    if (/dualshock|05c4|09cc|wireless controller/.test(text)) return 'dualshock';
    return '';
  }

  function center(rect) {
    return { x: (rect.left + rect.right) / 2, y: (rect.top + rect.bottom) / 2 };
  }

  function gap(firstStart, firstEnd, secondStart, secondEnd) {
    if (secondEnd < firstStart) return firstStart - secondEnd;
    if (secondStart > firstEnd) return secondStart - firstEnd;
    return 0;
  }

  function navScore(from, to, direction) {
    const horizontal = direction === 'left' || direction === 'right';
    const forward = direction === 'right' || direction === 'down';
    const start = center(from);
    const end = center(to);
    const along = horizontal ? end.x - start.x : end.y - start.y;
    if ((forward ? along : -along) <= 1) return null;

    const beyond = horizontal
      ? (forward ? to.right > from.right + 1 : to.left < from.left - 1)
      : (forward ? to.bottom > from.bottom + 1 : to.top < from.top - 1);
    if (!beyond) return null;

    const edge = horizontal
      ? (forward ? to.left - from.right : from.left - to.right)
      : (forward ? to.top - from.bottom : from.top - to.bottom);
    const cross = horizontal ? gap(from.top, from.bottom, to.top, to.bottom) : gap(from.left, from.right, to.left, to.right);
    const crossCenter = horizontal ? Math.abs(end.y - start.y) : Math.abs(end.x - start.x);
    return Math.max(0, edge) + cross * 4 + (cross > 0 ? 40 : 0) + crossCenter * 0.05;
  }

  function pickNext(from, candidates, direction) {
    let best = null;
    let bestScore = Infinity;
    for (const candidate of candidates) {
      const score = navScore(from, candidate.rect, direction);
      if (score != null && score < bestScore) {
        best = candidate;
        bestScore = score;
      }
    }
    return best;
  }

  function pickWrap(from, candidates, direction) {
    if (direction !== 'left' && direction !== 'right') return null;
    const sameRow = candidates.filter(candidate => gap(from.top, from.bottom, candidate.rect.top, candidate.rect.bottom) === 0);
    let best = null;
    for (const candidate of sameRow) {
      const x = center(candidate.rect).x;
      if (!best || (direction === 'right' ? x < center(best.rect).x : x > center(best.rect).x)) best = candidate;
    }
    return best && center(best.rect).x !== center(from).x ? best : null;
  }

  function pickNearest(point, candidates) {
    let best = null;
    let bestDistance = Infinity;
    for (const candidate of candidates) {
      const middle = center(candidate.rect);
      const distance = Math.hypot(middle.x - point.x, (middle.y - point.y) * 1.5);
      if (distance < bestDistance) {
        best = candidate;
        bestDistance = distance;
      }
    }
    return best;
  }

  return { emptyState, readButtons, padStep, padKind, padModel, navScore, pickNext, pickWrap, pickNearest, center };
})();

const padInput = {
  state: PadLogic.emptyState(),
  running: false,
  index: null,
  current: null,
  scopeName: null,
  remembered: {},
  history: [],
  lastView: state.view,
  restoringView: false,
  pendingDefault: false,
  browserScroll: 0,
  browserScrollAt: 0,
  lastMouse: null,
  barSignature: '',
};

const PAD_FOCUSABLE = '[data-id], .nav-item, .drive, button, input:not([type="hidden"]), select, textarea, a[href], [tabindex]:not([tabindex="-1"])';
const PAD_KEY_ATTRIBUTES = ['id', 'data-id', 'data-view', 'data-platform', 'data-drive', 'data-action', 'data-menu', 'data-sort', 'data-bucket', 'data-clear', 'data-store', 'data-browser', 'data-home-action', 'data-home-tab', 'data-home-layout', 'data-power', 'data-osk', 'data-key', 'data-console-setting'];
const PAD_HIDDEN_CONTAINERS = '.context-menu:not(.open), .dropdown:not(.open) .dropdown-menu, .drawer:not(.open), .modal-layer:not(.open), .home:not(.visible), [aria-hidden="true"], [hidden], .hidden';

function activeGamepad() {
  const pads = navigator.getGamepads ? [...navigator.getGamepads()].filter(pad => pad && pad.connected) : [];
  return pads.find(pad => pad.index === padInput.index) || pads[0] || null;
}

function setControllerMode(on) {
  const mode = on ? 'controller' : 'mouse';
  if (controller.mode === mode) return;
  controller.mode = mode;
  document.body.classList.toggle('controller-mode', on);
  if (!on && padInput.current) padInput.current.classList.remove('pad-focus');
  if (on && padInput.current && padInput.current.isConnected) padInput.current.classList.add('pad-focus');
  if (typeof updateHints === 'function') updateHints();
  renderPadBar();
}

function pollPads(now) {
  const pads = navigator.getGamepads ? [...navigator.getGamepads()].filter(pad => pad && pad.connected) : [];
  if (!pads.length) {
    padInput.running = false;
    padInput.state = PadLogic.emptyState();
    return;
  }
  requestAnimationFrame(pollPads);

  const latest = pads.reduce((first, second) => (second.timestamp > first.timestamp ? second : first));
  if (latest.index !== padInput.index) {
    const switching = padInput.index == null || pads.every(pad => pad.index !== padInput.index) || PadLogic.padStep(PadLogic.emptyState(), latest.buttons, latest.axes, now).active;
    if (switching) {
      padInput.index = latest.index;
      padInput.state = PadLogic.emptyState();
    }
  }
  const gamepad = pads.find(pad => pad.index === padInput.index) || latest;

  const kind = PadLogic.padKind(gamepad.id);
  const model = PadLogic.padModel(gamepad.id);
  if (kind !== controller.kind || model !== controller.model) {
    controller.kind = kind;
    controller.model = model;
    if (typeof updateHints === 'function') updateHints();
    padInput.barSignature = '';
    renderPadBar();
  }

  const step = PadLogic.padStep(padInput.state, gamepad.buttons, gamepad.axes, now);
  padInput.state = step.state;
  if (!step.events.length || document.hidden || !document.hasFocus()) return;

  if (controller.mode !== 'controller') {
    setControllerMode(true);
    if (step.events.every(event => ['up', 'down', 'left', 'right', 'scroll'].includes(event.name))) {
      showPadFocus();
      return;
    }
  }
  resumeConsoleAudio();
  if (typeof retryHomeMusic === 'function') retryHomeMusic();
  for (const event of step.events) dispatchPad(event);
}

function startPadPolling() {
  if (padInput.running) return;
  padInput.running = true;
  requestAnimationFrame(pollPads);
}

const consoleAudio = { context: null, lastMove: 0 };

function resumeConsoleAudio() {
  if (consoleAudio.context && consoleAudio.context.state === 'suspended') consoleAudio.context.resume().catch(() => {});
}

function consoleSound(kind) {
  if (!setting('console.sounds', true)) return;
  if (!(home.visible || controller.mode === 'controller')) return;

  const nowMs = performance.now();
  if (kind === 'move') {
    if (nowMs - consoleAudio.lastMove < 45) return;
    consoleAudio.lastMove = nowMs;
  }

  let context = consoleAudio.context;
  try {
    if (!context) context = consoleAudio.context = new AudioContext();
    if (context.state === 'suspended') context.resume().catch(() => {});
  } catch {
    return;
  }

  const start = context.currentTime + 0.005;
  const tone = (frequency, offset, duration, { type = 'sine', gain = 0.05, to = null } = {}) => {
    const oscillator = context.createOscillator();
    const envelope = context.createGain();
    const at = start + offset;
    oscillator.type = type;
    oscillator.frequency.setValueAtTime(frequency, at);
    if (to) oscillator.frequency.exponentialRampToValueAtTime(to, at + duration);
    envelope.gain.setValueAtTime(0.0001, at);
    envelope.gain.exponentialRampToValueAtTime(gain, at + 0.006);
    envelope.gain.exponentialRampToValueAtTime(0.0001, at + duration);
    oscillator.connect(envelope).connect(context.destination);
    oscillator.start(at);
    oscillator.stop(at + duration + 0.03);
  };

  switch (kind) {
    case 'move':
      tone(1750, 0, 0.035, { type: 'triangle', gain: 0.03, to: 1450 });
      break;
    case 'select':
      tone(880, 0, 0.07, { gain: 0.05, to: 1100 });
      tone(1320, 0.05, 0.09, { gain: 0.04 });
      break;
    case 'back':
      tone(990, 0, 0.07, { gain: 0.045, to: 760 });
      tone(620, 0.05, 0.09, { gain: 0.035 });
      break;
    case 'error':
      tone(200, 0, 0.08, { type: 'square', gain: 0.018 });
      tone(180, 0.1, 0.1, { type: 'square', gain: 0.018 });
      break;
    case 'launch':
      tone(660, 0, 0.1, { gain: 0.045 });
      tone(990, 0.08, 0.1, { gain: 0.045 });
      tone(1480, 0.16, 0.22, { gain: 0.04 });
      break;
    case 'open':
      tone(520, 0, 0.09, { gain: 0.04, to: 780 });
      break;
  }
}

const RUMBLE_EFFECTS = {
  select: { duration: 26, strongMagnitude: 0, weakMagnitude: 0.32 },
  launch: { duration: 150, strongMagnitude: 0.45, weakMagnitude: 0.6 },
  error: { duration: 55, strongMagnitude: 0.28, weakMagnitude: 0 },
};

function consoleRumble(kind) {
  if (!setting('console.rumble', true) || controller.mode !== 'controller') return;
  const effect = RUMBLE_EFFECTS[kind];
  const actuator = activeGamepad()?.vibrationActuator;
  if (!effect || !actuator || typeof actuator.playEffect !== 'function') return;
  try {
    const result = actuator.playEffect('dual-rumble', { startDelay: 0, ...effect });
    if (result && result.catch) result.catch(() => {});
  } catch {
  }
}

function padFeedback(kind) {
  consoleSound(kind);
  if (kind === 'select' || kind === 'launch' || kind === 'error') consoleRumble(kind);
}

function isPadVisible(element) {
  if (!element.isConnected || element.closest(PAD_HIDDEN_CONTAINERS)) return false;
  if (element.disabled) return false;
  const rect = element.getBoundingClientRect();
  if (rect.width < 2 || rect.height < 2) return false;
  const style = getComputedStyle(element);
  return style.visibility !== 'hidden' && style.display !== 'none';
}

function padFocusables(root) {
  if (!root) return [];
  return [...root.querySelectorAll(PAD_FOCUSABLE)].filter(element => {
    const owner = element.parentElement && element.parentElement.closest('[data-id]');
    if (owner && root.contains(owner) && owner !== root) return false;
    return isPadVisible(element);
  });
}

function padKeyOf(element) {
  for (const attribute of PAD_KEY_ATTRIBUTES) {
    const value = element.getAttribute(attribute);
    if (value == null) continue;
    if (attribute === 'id') return `#${CSS.escape(value)}`;
    return `[${attribute}="${CSS.escape(value)}"]`;
  }
  return null;
}

function padRemember(scopeName, element) {
  padInput.remembered[scopeName] = { element, key: padKeyOf(element) };
}

function padDefaultFor(root, scopeName) {
  const list = padFocusables(root);
  const preferred = root.querySelector('[data-pad-default]');
  if (preferred && list.includes(preferred)) return preferred;

  if (scopeName === 'library') {
    const first = list.find(element => element.matches('[data-id]'));
    if (first) return first;
    const active = list.find(element => element.matches('.nav-item.active'));
    if (active) return active;
  }
  const primary = list.find(element => element.matches('.primary-button, .power-option, .menu-item'));
  if (primary) return primary;
  return list.find(element => !element.matches('[data-modal-close], [data-action="close-drawer"]')) || list[0] || null;
}

function padRecover(root, scopeName) {
  const memory = padInput.remembered[scopeName];
  if (memory) {
    if (memory.element && root.contains(memory.element) && isPadVisible(memory.element)) return memory.element;
    if (memory.key) {
      const match = [...root.querySelectorAll(memory.key)].find(isPadVisible);
      if (match) return match;
    }
  }
  return null;
}

function padFocus(element, scopeName, { scroll = true } = {}) {
  if (padInput.current && padInput.current !== element) padInput.current.classList.remove('pad-focus');
  if (!element.matches('button, input, select, textarea, a[href], [tabindex]')) element.tabIndex = -1;
  try {
    element.focus({ preventScroll: true });
  } catch {
  }
  if (controller.mode === 'controller') element.classList.add('pad-focus');
  if (scroll) element.scrollIntoView({ block: 'nearest', inline: 'nearest', behavior: 'smooth' });
  padInput.current = element;
  padInput.scopeName = scopeName;
  padRemember(scopeName, element);
}

function padEnsureFocus(root, scopeName) {
  const current = padInput.current;
  if (current && padInput.scopeName === scopeName && root.contains(current) && isPadVisible(current)) {
    current.classList.add('pad-focus');
    return { element: current, fresh: false };
  }
  const element = padRecover(root, scopeName) || padDefaultFor(root, scopeName);
  if (!element) return { element: null, fresh: true };
  padFocus(element, scopeName);
  return { element, fresh: true };
}

function padMove(root, scopeName, direction, { wrap = false } = {}) {
  const { element: current, fresh } = padEnsureFocus(root, scopeName);
  if (!current) {
    padFeedback('error');
    return false;
  }
  if (fresh) return true;

  if (current.matches('input[type="range"]') && (direction === 'left' || direction === 'right')) {
    const step = Number(current.step) || 1;
    const value = Number(current.value) + (direction === 'right' ? step : -step);
    current.value = String(Math.max(Number(current.min || 0), Math.min(Number(current.max || 100), value)));
    current.dispatchEvent(new Event('input', { bubbles: true }));
    current.dispatchEvent(new Event('change', { bubbles: true }));
    padFeedback('move');
    return true;
  }

  const from = current.getBoundingClientRect();
  const candidates = padFocusables(root).filter(element => element !== current).map(element => ({ element, rect: element.getBoundingClientRect() }));
  const next = PadLogic.pickNext(from, candidates, direction) || (wrap ? PadLogic.pickWrap(from, candidates, direction) : null);
  if (!next) return false;
  padFocus(next.element, scopeName);
  padFeedback('move');
  return true;
}

function padActivate(root, scopeName) {
  const { element, fresh } = padEnsureFocus(root, scopeName);
  if (!element || fresh) return;

  if (element.matches('input[type="text"], input[type="search"], input:not([type]), textarea')) {
    padFeedback('open');
    openKeyboardFor(element);
    return;
  }
  if (element.matches('select')) {
    const count = element.options.length;
    if (count) {
      element.selectedIndex = (element.selectedIndex + 1) % count;
      element.dispatchEvent(new Event('change', { bubbles: true }));
    }
    padFeedback('select');
    return;
  }
  if (element.matches('input[type="range"]')) return;
  padFeedback('select');
  element.click();
}

function showPadFocus() {
  const scope = currentPadScope();
  if (scope && scope.root) padEnsureFocus(scope.root, scope.name);
}

function currentPadScope() {
  if (osk.open) return null;
  if (modal.isOpen && modal.panel) return { name: 'modal', root: modal.panel };
  if (elements.contextMenu.classList.contains('open')) return { name: 'menu', root: elements.contextMenu };
  if (elements.sortDropdown.classList.contains('open')) return { name: 'sort', root: elements.sortMenu };
  if (state.selectedId) return { name: 'drawer', root: elements.drawer };
  if (home.visible) return home.zone === 'bar' ? { name: 'home-bar', root: home.element.querySelector('.home-bar') } : null;
  return { name: 'library', root: document.querySelector('.app') };
}

function scrollableAncestor(element) {
  for (let node = element && element.parentElement; node && node !== document.body; node = node.parentElement) {
    const style = getComputedStyle(node);
    if (/(auto|scroll)/.test(style.overflowY) && node.scrollHeight > node.clientHeight + 4) return node;
  }
  return null;
}

function padPage(root, scopeName, delta) {
  const { element: current } = padEnsureFocus(root, scopeName);
  const container = (current && scrollableAncestor(current)) || (scopeName === 'drawer' ? elements.drawer : elements.scroller);
  if (!container) return;
  const before = current ? current.getBoundingClientRect() : container.getBoundingClientRect();
  const point = PadLogic.center(before);
  container.scrollBy({ top: delta * container.clientHeight * 0.85, behavior: 'auto' });

  const box = container.getBoundingClientRect();
  const candidates = padFocusables(container)
    .map(element => ({ element, rect: element.getBoundingClientRect() }))
    .filter(candidate => candidate.rect.top >= box.top - 2 && candidate.rect.bottom <= box.bottom + 2);
  const target = PadLogic.pickNearest(point, candidates);
  if (target) padFocus(target.element, scopeName, { scroll: false });
  padFeedback('move');
}

function padScroll(root, scopeName, amount) {
  const current = padInput.current && root.contains(padInput.current) ? padInput.current : null;
  const container = (current && scrollableAncestor(current)) || (scopeName === 'drawer' ? elements.drawer : elements.scroller);
  if (container) container.scrollBy({ top: amount * 22, behavior: 'auto' });
}

function dispatchPad(event) {
  if (event.name === 'guide') {
    openHome();
    return;
  }
  if (event.name === 'volumeUp' || event.name === 'volumeDown') {
    if (typeof consoleVolumeStep === 'function') consoleVolumeStep(event.name === 'volumeUp' ? 0.05 : -0.05);
    return;
  }
  if (osk.open) {
    oskPad(event);
  } else if (modal.isOpen && modal.panel) {
    overlayPad(event, modal.panel, 'modal', () => modal.close());
  } else if (elements.contextMenu.classList.contains('open')) {
    overlayPad(event, elements.contextMenu, 'menu', closeMenus);
  } else if (elements.sortDropdown.classList.contains('open')) {
    overlayPad(event, elements.sortMenu, 'sort', closeMenus);
  } else if (state.selectedId) {
    drawerPad(event);
  } else if (home.visible) {
    homePad(event);
  } else {
    libraryPad(event);
  }
  renderPadBar();
}

function overlayPad(event, root, scopeName, close) {
  switch (event.name) {
    case 'up': case 'down': case 'left': case 'right':
      padMove(root, scopeName, event.name);
      break;
    case 'a':
      padActivate(root, scopeName);
      break;
    case 'b':
      close();
      padFeedback('back');
      break;
    case 'lt': case 'rt':
      padPage(root, scopeName, event.name === 'lt' ? -1 : 1);
      break;
    case 'scroll':
      padScroll(root, scopeName, event.amount);
      break;
    case 'start':
      if (scopeName !== 'modal') {
        close();
        openHome();
      }
      break;
  }
}

function drawerPad(event) {
  const game = state.byId.get(state.selectedId);
  switch (event.name) {
    case 'b':
      closeDrawer();
      padFeedback('back');
      break;
    case 'y':
      if (game && game.Status === 'installed') {
        toggleFavorite(game);
        padFeedback('select');
      }
      break;
    case 'x':
      if (game) {
        const box = elements.drawer.getBoundingClientRect();
        openContextMenu(game, box.left + 24, box.top + 80);
        padInput.remembered.menu = null;
        padFeedback('open');
      }
      break;
    case 'start':
      closeDrawer();
      openHome();
      break;
    default:
      overlayPad(event, elements.drawer, 'drawer', closeDrawer);
  }
}

function focusedGame() {
  const element = padInput.current;
  if (!element || !element.isConnected) return null;
  const card = element.closest('[data-id]');
  return card ? state.byId.get(card.dataset.id) : null;
}

function inStores() {
  return state.view === 'stores' && !!elements.results.querySelector('[data-store-frame], .stores-view');
}

function storeCanGoBack() {
  return typeof storeView !== 'undefined' && !!storeView.canGoBack;
}

function browserScroll(dy) {
  padInput.browserScroll += dy;
  const now = performance.now();
  if (now - padInput.browserScrollAt < 60) return;
  padInput.browserScrollAt = now;
  send({ type: 'browser', action: 'scroll', dy: Math.round(padInput.browserScroll) });
  padInput.browserScroll = 0;
}

function switchSidebarView(delta) {
  const items = [...elements.sidebar.querySelectorAll('.nav-item[data-view]')];
  if (!items.length) return;
  const index = items.findIndex(item => item.dataset.view === state.view);
  const next = items[((index < 0 ? 0 : index) + delta + items.length) % items.length];
  padInput.current = null;
  padInput.remembered.library = null;
  padInput.pendingDefault = true;
  padFeedback('move');
  next.click();
}

function switchStoreTab(delta) {
  const tabs = [...elements.results.querySelectorAll('[data-store]')];
  if (!tabs.length) {
    switchSidebarView(delta);
    return;
  }
  const index = tabs.findIndex(tab => tab.classList.contains('active'));
  const next = tabs[((index < 0 ? 0 : index) + delta + tabs.length) % tabs.length];
  next.click();
  padFeedback('move');
}

function libraryBack() {
  if (inStores() && storeCanGoBack()) {
    send({ type: 'browser', action: 'back' });
    padFeedback('back');
  } else if (state.search) {
    elements.search.value = '';
    state.search = '';
    renderAll();
    padFeedback('back');
  } else if (state.platform || state.drive) {
    state.platform = null;
    state.drive = null;
    renderAll();
    padFeedback('back');
  } else if (padInput.history.length) {
    padInput.restoringView = true;
    padInput.current = null;
    padInput.remembered.library = null;
    padInput.pendingDefault = true;
    setView(padInput.history.pop());
    padFeedback('back');
  } else {
    padFeedback('error');
  }
}

function libraryPad(event) {
  const root = document.querySelector('.app');
  switch (event.name) {
    case 'up': case 'down': case 'left': case 'right':
      padMove(root, 'library', event.name);
      break;
    case 'a':
      padActivate(root, 'library');
      break;
    case 'b':
      libraryBack();
      break;
    case 'x': {
      const game = focusedGame();
      if (game) {
        const box = padInput.current.getBoundingClientRect();
        openContextMenu(game, box.left + box.width / 2, box.top + Math.min(box.height / 2, 120));
        padInput.remembered.menu = null;
        padFeedback('open');
      } else if (inStores()) {
        send({ type: 'browser', action: 'reload' });
        padFeedback('select');
      }
      break;
    }
    case 'y': {
      const game = focusedGame();
      if (game && game.Status === 'installed') {
        toggleFavorite(game);
        toast(isFavorite(game) ? `Added ${game.Name} to favorites` : `Removed ${game.Name} from favorites`, 'info', ICON.star);
        padFeedback('select');
      } else if (inStores()) {
        send({ type: 'browser', action: 'forward' });
        padFeedback('move');
      }
      break;
    }
    case 'lb': case 'rb':
      if (inStores()) switchStoreTab(event.name === 'lb' ? -1 : 1);
      else switchSidebarView(event.name === 'lb' ? -1 : 1);
      break;
    case 'lt': case 'rt':
      if (inStores()) {
        send({ type: 'browser', action: 'scroll', page: event.name === 'lt' ? -1 : 1 });
        padFeedback('move');
      } else {
        padPage(root, 'library', event.name === 'lt' ? -1 : 1);
      }
      break;
    case 'scroll':
      if (inStores()) browserScroll(event.amount * 40);
      else padScroll(root, 'library', event.amount);
      break;
    case 'start':
      openHome();
      break;
    case 'view':
      toggleSidebar();
      padFeedback('select');
      break;
  }
}

const OSK_ROWS = ['1234567890', 'qwertyuiop', "asdfghjkl'", 'zxcvbnm,.-'];

const osk = {
  open: false,
  element: null,
  value: '',
  shift: false,
  options: null,
  current: null,
};

function oskKeysHtml() {
  const rows = OSK_ROWS.map(row => [...row].map(character => {
    const label = osk.shift ? character.toUpperCase() : character;
    return `<button class="osk-key" data-key="${escapeHtml(character)}" tabindex="-1">${escapeHtml(label)}</button>`;
  }).join('')).join('');
  return `${rows}
    <button class="osk-key action ${osk.shift ? 'on' : ''}" data-osk="shift" tabindex="-1">${ICON.oskShift}<span>Shift</span></button>
    <button class="osk-key action space" data-osk="space" tabindex="-1"><span>Space</span></button>
    <button class="osk-key action" data-osk="backspace" tabindex="-1">${ICON.oskBackspace}</button>
    <button class="osk-key action done" data-osk="done" tabindex="-1">${ICON.check}<span>Done</span></button>`;
}

function oskHintsHtml() {
  return [
    [padGlyph('x'), 'Backspace'],
    [padGlyph('y'), 'Space'],
    [padGlyph('lt'), 'Shift'],
    [padGlyph('b'), 'Close'],
    [padGlyph('start'), 'Done'],
  ].map(([glyph, label]) => `<span class="pad-hint">${glyph}<span>${label}</span></span>`).join('');
}

function renderKeyboard() {
  if (!osk.element) return;
  osk.element.querySelector('[data-osk-text]').textContent = osk.value;
  osk.element.querySelector('[data-osk-placeholder]').hidden = !!osk.value;
  const keys = osk.element.querySelector('[data-osk-keys]');
  const focusedKey = osk.current ? padKeyOf(osk.current) : null;
  keys.innerHTML = oskKeysHtml();
  osk.current = (focusedKey && keys.querySelector(focusedKey)) || keys.querySelector('[data-key="q"]');
  osk.current.classList.add('focused');
  osk.element.querySelector('[data-osk-hints]').innerHTML = oskHintsHtml();
}

function openKeyboard(options) {
  closeKeyboard(true);
  osk.open = true;
  osk.options = options;
  osk.value = options.value || '';
  osk.shift = false;
  osk.current = null;

  const element = document.createElement('div');
  element.className = 'osk-layer';
  element.innerHTML = `
    <div class="osk-backdrop" data-osk-close></div>
    <div class="osk-panel" role="dialog">
      <div class="osk-field">
        ${ICON.search}
        <div class="osk-text"><span data-osk-text></span><i class="osk-caret"></i><span class="osk-placeholder" data-osk-placeholder>${escapeHtml(options.label || 'Type to search')}</span></div>
        <span class="osk-count" data-osk-count></span>
      </div>
      <div class="osk-keys" data-osk-keys></div>
      <div class="osk-hints" data-osk-hints></div>
    </div>`;
  document.body.appendChild(element);
  osk.element = element;
  renderKeyboard();
  updateKeyboardCount();
  requestAnimationFrame(() => element.classList.add('open'));
}

function openKeyboardFor(input) {
  const isLibrarySearch = input === elements.search;
  openKeyboard({
    value: input.value,
    label: input.placeholder || 'Type here',
    onInput(value) {
      input.value = value;
      input.dispatchEvent(new Event('input', { bubbles: true }));
    },
    onDone(value) {
      input.value = value;
      input.dispatchEvent(new Event('input', { bubbles: true }));
      if (isLibrarySearch) {
        requestAnimationFrame(() => {
          const first = elements.results.querySelector('[data-id]');
          if (first) padFocus(first, 'library');
        });
        return;
      }
      const form = input.closest('form');
      if (form) {
        if (typeof form.requestSubmit === 'function') form.requestSubmit();
        else form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
      }
    },
    count: isLibrarySearch ? () => state.visible.length : null,
  });
}

function closeKeyboard(silent = false) {
  if (!osk.open) return;
  osk.open = false;
  const element = osk.element;
  osk.element = null;
  element.classList.remove('open');
  setTimeout(() => element.remove(), 200);
  if (!silent && osk.options && osk.options.onClose) osk.options.onClose(osk.value);
}

function updateKeyboardCount() {
  const counter = osk.element && osk.element.querySelector('[data-osk-count]');
  if (!counter) return;
  const count = osk.options && osk.options.count ? osk.options.count() : null;
  counter.textContent = count == null || !osk.value ? '' : `${formatCount(count)} ${count === 1 ? 'match' : 'matches'}`;
}

function oskSetValue(value) {
  osk.value = value;
  if (osk.element) {
    osk.element.querySelector('[data-osk-text]').textContent = value;
    osk.element.querySelector('[data-osk-placeholder]').hidden = !!value;
  }
  if (osk.options && osk.options.onInput) osk.options.onInput(value);
  requestAnimationFrame(updateKeyboardCount);
}

function oskPress(key) {
  if (!key) return;
  if (key.dataset.key) {
    const character = osk.shift ? key.dataset.key.toUpperCase() : key.dataset.key;
    oskSetValue(osk.value + character);
    if (osk.shift) {
      osk.shift = false;
      renderKeyboard();
    }
    consoleSound('move');
    return;
  }
  switch (key.dataset.osk) {
    case 'shift':
      osk.shift = !osk.shift;
      renderKeyboard();
      consoleSound('move');
      break;
    case 'space':
      oskSetValue(osk.value + ' ');
      consoleSound('move');
      break;
    case 'backspace':
      oskBackspace();
      break;
    case 'done':
      oskDone();
      break;
  }
}

function oskBackspace() {
  if (!osk.value) {
    padFeedback('error');
    return;
  }
  oskSetValue(osk.value.slice(0, -1));
  consoleSound('back');
}

function oskDone() {
  const options = osk.options;
  const value = osk.value;
  closeKeyboard(true);
  padFeedback('select');
  if (options && options.onDone) options.onDone(value);
}

function oskFocusKey(key) {
  if (osk.current) osk.current.classList.remove('focused');
  osk.current = key;
  key.classList.add('focused');
}

function oskPad(event) {
  switch (event.name) {
    case 'up': case 'down': case 'left': case 'right': {
      const keys = [...osk.element.querySelectorAll('.osk-key')];
      const from = osk.current.getBoundingClientRect();
      const candidates = keys.filter(key => key !== osk.current).map(element => ({ element, rect: element.getBoundingClientRect() }));
      const next = PadLogic.pickNext(from, candidates, event.name) || PadLogic.pickWrap(from, candidates, event.name);
      if (next) {
        oskFocusKey(next.element);
        consoleSound('move');
      }
      break;
    }
    case 'a':
      oskPress(osk.current);
      consoleRumble('select');
      break;
    case 'x':
      oskBackspace();
      break;
    case 'y':
      oskSetValue(osk.value + ' ');
      consoleSound('move');
      break;
    case 'lt': case 'ls':
      osk.shift = !osk.shift;
      renderKeyboard();
      consoleSound('move');
      break;
    case 'b':
      closeKeyboard();
      padFeedback('back');
      break;
    case 'start':
      oskDone();
      break;
  }
}

document.addEventListener('click', event => {
  if (!osk.open || !osk.element || !osk.element.contains(event.target)) return;
  if (event.target.closest('[data-osk-close]')) {
    closeKeyboard();
    return;
  }
  const key = event.target.closest('.osk-key');
  if (key) {
    oskFocusKey(key);
    oskPress(key);
  }
});

window.addEventListener('keydown', event => {
  if (!osk.open) return;
  event.stopImmediatePropagation();
  if (event.key === 'Escape') {
    event.preventDefault();
    closeKeyboard();
  } else if (event.key === 'Enter') {
    event.preventDefault();
    oskDone();
  } else if (event.key === 'Backspace') {
    event.preventDefault();
    oskBackspace();
  } else if (event.key.length === 1 && !event.ctrlKey && !event.altKey && !event.metaKey) {
    event.preventDefault();
    oskSetValue(osk.value + event.key);
  }
}, true);

function padBarHints() {
  if (osk.open || home.visible && !state.selectedId && !modal.isOpen && !elements.contextMenu.classList.contains('open')) return [];
  if (modal.isOpen || elements.contextMenu.classList.contains('open') || elements.sortDropdown.classList.contains('open')) {
    return [['a', 'Select'], ['b', 'Back']];
  }
  if (state.selectedId) return [['a', 'Select'], ['b', 'Close'], ['x', 'Options'], ['y', 'Favorite']];
  if (inStores()) return [['a', 'Select'], ['b', 'Back'], ['lb', 'rb', 'Stores'], ['lt', 'rt', 'Page'], ['y', 'Forward'], ['start', 'Home']];
  return [['a', 'Select'], ['b', 'Back'], ['x', 'Options'], ['y', 'Favorite'], ['lb', 'rb', 'Views'], ['start', 'Home'], ['view', 'Sidebar']];
}

function renderPadBar() {
  let bar = document.getElementById('padBar');
  const hints = controller.mode === 'controller' ? padBarHints() : [];
  const signature = controller.kind + controller.model + hints.map(hint => hint.join('+')).join('|');
  if (signature === padInput.barSignature && bar) return;
  padInput.barSignature = signature;
  if (!bar) {
    bar = document.createElement('div');
    bar.id = 'padBar';
    bar.className = 'pad-bar';
    document.body.appendChild(bar);
  }
  bar.classList.toggle('visible', hints.length > 0);
  if (!hints.length) return;
  bar.innerHTML = hints.map(hint => {
    const label = hint[hint.length - 1];
    const glyphs = hint.slice(0, -1).map(name => padGlyph(name)).join('');
    return `<span class="pad-hint">${glyphs}<span>${escapeHtml(label)}</span></span>`;
  }).join('');
}

ICON.oskShift = '<svg viewBox="0 0 24 24" class="glyph"><path d="M12 4 4.5 12H9v7h6v-7h4.5z"/></svg>';
ICON.oskBackspace = '<svg viewBox="0 0 24 24" class="glyph"><path d="M8.5 5.5H20a1 1 0 0 1 1 1v11a1 1 0 0 1-1 1H8.5L3 12z"/><path d="m11.5 9.5 5 5M16.5 9.5l-5 5"/></svg>';

window.addEventListener('gamepadconnected', () => startPadPolling());
window.addEventListener('gamepaddisconnected', () => {
  if (!activeGamepad()) padInput.state = PadLogic.emptyState();
});

window.addEventListener('mousemove', event => {
  if (controller.mode !== 'controller') return;
  const last = padInput.lastMouse;
  padInput.lastMouse = { x: event.screenX, y: event.screenY };
  if (!last) return;
  if (Math.abs(event.screenX - last.x) + Math.abs(event.screenY - last.y) > 6) {
    padInput.lastMouse = null;
    setControllerMode(false);
  }
}, { passive: true });

window.addEventListener('mousedown', () => {
  padInput.lastMouse = null;
  if (controller.mode === 'controller') setControllerMode(false);
}, true);

window.addEventListener('wheel', () => {
  if (controller.mode === 'controller') setControllerMode(false);
}, { passive: true });

extensions.afterRender.push(() => {
  if (state.view !== padInput.lastView) {
    if (!padInput.restoringView) {
      padInput.history.push(padInput.lastView);
      if (padInput.history.length > 20) padInput.history.shift();
    }
    padInput.restoringView = false;
    padInput.lastView = state.view;
  }

  if (controller.mode !== 'controller') return;
  requestAnimationFrame(() => {
    const scope = currentPadScope();
    if (!scope || !scope.root) return;
    if (padInput.pendingDefault && scope.name === 'library') {
      padInput.pendingDefault = false;
      const element = padDefaultFor(scope.root, 'library');
      if (element) padFocus(element, 'library', { scroll: false });
      return;
    }
    if (padInput.current && !padInput.current.isConnected && padInput.scopeName === scope.name) {
      const element = padRecover(scope.root, scope.name);
      if (element) padFocus(element, scope.name, { scroll: false });
    }
    renderPadBar();
  });
});

new MutationObserver(() => {
  if (controller.mode === 'controller') renderPadBar();
}).observe(document.body, { attributes: true, attributeFilter: ['class'], subtree: false });

for (const target of [elements.drawer, elements.contextMenu, elements.sortDropdown, modal.layer]) {
  new MutationObserver(() => {
    if (controller.mode !== 'controller') return;
    renderPadBar();
    requestAnimationFrame(() => {
      const scope = currentPadScope();
      if (scope && scope.root && scope.name !== 'library' && scope.name !== 'home-bar') padEnsureFocus(scope.root, scope.name);
      else if (scope && scope.name === 'library' && padInput.current && !scope.root.contains(padInput.current)) padEnsureFocus(scope.root, 'library');
    });
  }).observe(target, { attributes: true, attributeFilter: ['class'] });
}

if (navigator.getGamepads && [...navigator.getGamepads()].some(Boolean)) startPadPolling();
renderPadBar();
