'use strict';

ICON.palette = ICON.palette || '<svg viewBox="0 0 24 24" class="glyph"><path d="M12 3.5a8.5 8.5 0 1 0 0 17c1.1 0 1.75-.8 1.75-1.7 0-.45-.17-.83-.45-1.15-.27-.31-.43-.68-.43-1.12 0-.95.77-1.7 1.72-1.7h2.06A3.85 3.85 0 0 0 20.5 11 7.9 7.9 0 0 0 12 3.5z"/><circle cx="7.6" cy="11" r="1.1"/><circle cx="10" cy="7.4" r="1.1"/><circle cx="14.6" cy="7.6" r="1.1"/></svg>';
ICON.keyboard = ICON.keyboard || '<svg viewBox="0 0 24 24" class="glyph"><rect x="2.5" y="6" width="19" height="12" rx="2.5"/><path d="M6.5 10h.01M10 10h.01M13.5 10h.01M17 10h.01M6.5 14h.01M17 14h.01M9.5 14h5"/></svg>';
ICON.pulse = ICON.pulse || '<svg viewBox="0 0 24 24" class="glyph"><path d="M3 12h4l2.5-6 4 12 2.5-6H21"/></svg>';
ICON.archive = ICON.archive || '<svg viewBox="0 0 24 24" class="glyph"><rect x="3" y="4" width="18" height="5" rx="1.5"/><path d="M5 9v9a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V9M10 13h4"/></svg>';
ICON.folders = ICON.folders || '<svg viewBox="0 0 24 24" class="glyph"><path d="M3.5 8V6.5a2 2 0 0 1 2-2h3.6l2 2h7.4a2 2 0 0 1 2 2V16a2 2 0 0 1-2 2H5.5a2 2 0 0 1-2-2z"/><path d="M3.5 10.5h17"/></svg>';
ICON.offline = ICON.offline || '<svg viewBox="0 0 24 24" class="glyph"><path d="M3 3l18 18M8.5 16.4a5 5 0 0 1 6.2-.6M5 12.9a10 10 0 0 1 4-2.4M12.5 10a10 10 0 0 1 6.5 2.9M2 9.5a15 15 0 0 1 3.7-2.6M10.6 5.6A15 15 0 0 1 22 9.5M12 20h.01"/></svg>';

const THEME_DEFAULTS = {
  mode: 'dark',
  accent: 'indigo',
  background: '',
  backgroundBlur: 18,
  backgroundOpacity: 35,
  transition: 'fade',
  motion: 'system',
};

const ACCENTS = [
  { key: 'indigo', label: 'Indigo', color: '#7b8cff', second: '#a46bff' },
  { key: 'blue', label: 'Blue', color: '#4f9dff', second: '#45d0ff' },
  { key: 'teal', label: 'Teal', color: '#2ec4b6', second: '#5fd39a' },
  { key: 'green', label: 'Green', color: '#5fcf6f', second: '#b7e35a' },
  { key: 'amber', label: 'Amber', color: '#f2a93b', second: '#ff7a59' },
  { key: 'rose', label: 'Rose', color: '#ff6b8b', second: '#c86bff' },
  { key: 'violet', label: 'Violet', color: '#a46bff', second: '#ff6bd6' },
];

const ACCENT_PROPERTIES = ['--accent', '--accent-2', '--accent-gradient', '--accent-soft'];
const HEX_COLOR = /^#[0-9a-f]{6}$/i;
const WALLPAPER_URL = /^https:\/\/cache\.hub\/[^"'()\\\s]+$/;

const SYNC_INTERVALS = [
  { value: 'PT15M', label: 'Every 15 minutes' },
  { value: 'PT30M', label: 'Every 30 minutes' },
  { value: 'PT1H', label: 'Every hour' },
  { value: 'PT3H', label: 'Every 3 hours' },
  { value: 'PT6H', label: 'Every 6 hours' },
  { value: 'logon', label: 'Only when I sign in to Windows' },
];

const CONFIG_LISTS = [
  { key: 'LibraryFolders', title: 'Game library folders', hint: 'Every folder inside these is added as a game.', kind: 'folder' },
  { key: 'SearchFolders', title: 'Folders to search', hint: 'Searched deeply for games, such as a Downloads folder. Found games are listed under Downloads.', kind: 'folder' },
  { key: 'ExtraGames', title: 'Single games', hint: 'One game folder or program each.', kind: 'both' },
  { key: 'IgnoreNames', title: 'Hidden names', hint: 'Games with these exact names are left out of the library.', kind: 'text' },
];

const SECTION_ICONS = {
  general: () => ICON.sliders || ICON.gear,
  folders: () => ICON.folders,
  appearance: () => ICON.palette,
  playing: () => ICON.play,
  console: () => ICON.home || ICON.gamepad,
  store: () => ICON.globe || ICON.external,
  deals: () => ICON.tag || ICON.download,
  meta: () => ICON.info,
  organize: () => ICON.layers || ICON.box,
  'discovery-shortcuts': () => ICON.tiles || ICON.external,
  'discovery-steam': () => ICON.store || ICON.gamepad,
  'discovery-emulators': () => ICON.pad || ICON.gamepad,
  'discovery-library': () => ICON.search,
  'discovery-hidden': () => ICON.eyeOff,
  saves: () => ICON.save || ICON.broom || ICON.box,
  backup: () => ICON.archive,
  diagnostics: () => ICON.pulse,
  about: () => ICON.info,
};

const SHORTCUT_GROUPS = [
  {
    title: 'Library',
    keys: [
      [['Ctrl', 'K'], 'Search your games'],
      [['/'], 'Search your games'],
      [['Enter'], 'Play the first match while searching'],
      [['↓'], 'Move from search into the results'],
      [['Enter'], 'Open the focused game'],
      [['Esc'], 'Close a panel or clear the search'],
      [['Ctrl', 'B'], 'Show or hide the sidebar'],
      [['Ctrl', ','], 'Settings'],
      [['F1'], 'This list of shortcuts'],
    ],
  },
  {
    title: 'Home',
    keys: [
      [['Home'], 'Open or leave the home screen'],
      [['←', '→', '↑', '↓'], 'Move between games and rows'],
      [['Enter'], 'Play or open'],
      [['Space'], 'Game details'],
      [['F'], 'Add or remove favorite'],
      [['Tab'], 'Switch shelf'],
      [['S'], 'Search'],
      [['Esc'], 'Clear the search'],
    ],
  },
  {
    title: 'Window',
    keys: [
      [['F11'], 'Full screen'],
      [['Esc'], 'Leave full screen'],
      [['Win', '↑'], 'Maximize'],
      [['Win', '←', '→'], 'Snap to a side'],
      [['Win', 'Z'], 'Snap layouts'],
    ],
  },
  {
    title: 'Store browser',
    keys: [
      [['Ctrl', 'T'], 'New tab'],
      [['Ctrl', 'Shift', 'T'], 'Reopen closed tab'],
      [['Ctrl', 'W'], 'Close tab'],
      [['Ctrl', 'Tab'], 'Next tab'],
      [['Ctrl', '1–9'], 'Go to tab'],
      [['Ctrl', 'L'], 'Address bar'],
      [['Alt', '←', '→'], 'Back and forward'],
      [['Ctrl', 'R'], 'Reload'],
      [['Ctrl', 'D'], 'Bookmark'],
      [['Ctrl', 'H'], 'History'],
      [['Ctrl', 'J'], 'Downloads'],
      [['Ctrl', '+', '−', '0'], 'Zoom'],
    ],
  },
];

const SEARCH_SYNTAX = [
  ['platform:steam', 'Only one platform'],
  ['size:>20gb', 'Larger than 20 GB'],
  ['played:<30d', 'Played in the last 30 days'],
  ['installed:<1w', 'Installed this week'],
  ['unplayed', 'Never played'],
  ['fav', 'Favorites'],
  ['status:playing', 'Play status'],
  ['tag:"co-op"', 'In a collection'],
  ['genre:rpg', 'Genre'],
  ['drive:d', 'On a drive'],
  ['-platform:epic', 'Leave out matches'],
];

const settingsUi = {
  section: state.prefs.settingsSection || 'general',
  info: null,
  infoRequested: false,
  syncBusy: false,
  listeners: new Map(),
  picks: new Map(),
  nextPick: 1,
  snapshot: {},
  pendingImport: null,
  online: navigator.onLine !== false,
  onlineTimer: null,
  errorsSent: new Set(),
};

function settingValue(key, fallback) {
  if (key.startsWith('prefs.')) {
    const value = state.prefs[key.slice(6)];
    return value === undefined || value === null ? fallback : value;
  }
  return setting(key, fallback);
}

function setSettingValue(key, value, options = {}) {
  const persist = options.persist !== false;
  const empty = value === undefined || value === null || value === '';
  if (key.startsWith('prefs.')) {
    const name = key.slice(6);
    if (empty) delete state.prefs[name];
    else state.prefs[name] = value;
    if (persist) savePrefs();
  } else if (persist) {
    setSetting(key, empty ? null : value);
  } else if (empty) {
    delete state.settings[key];
  } else {
    state.settings[key] = value;
  }
  if (persist && !key.startsWith('prefs.')) settingsUi.snapshot[key] = JSON.stringify(empty ? null : value);
  notifySetting(key, empty ? null : value);
}

function onSetting(key, callback) {
  if (!settingsUi.listeners.has(key)) settingsUi.listeners.set(key, new Set());
  settingsUi.listeners.get(key).add(callback);
  return () => settingsUi.listeners.get(key).delete(callback);
}

function notifySetting(key, value) {
  for (const [pattern, callbacks] of settingsUi.listeners) {
    const matches = pattern === key || pattern === '*' || (pattern.endsWith('*') && key.startsWith(pattern.slice(0, -1)));
    if (!matches) continue;
    for (const callback of callbacks) {
      try {
        callback(value, key);
      } catch (error) {
        reportClientError(error);
      }
    }
  }
}

function diffSettings() {
  const next = {};
  for (const [key, value] of Object.entries(state.settings)) next[key] = JSON.stringify(value);
  const keys = new Set([...Object.keys(next), ...Object.keys(settingsUi.snapshot)]);
  const changed = [...keys].filter(key => next[key] !== settingsUi.snapshot[key]);
  settingsUi.snapshot = next;
  for (const key of changed) notifySetting(key, setting(key, null));
}

function settingAttributes(key, type, fallback) {
  return `data-setting="${escapeHtml(key)}" data-setting-type="${type}" data-setting-default="${escapeHtml(JSON.stringify(fallback === undefined ? null : fallback))}"`;
}

function valueType(value) {
  if (typeof value === 'boolean') return 'bool';
  if (typeof value === 'number') return 'number';
  return 'string';
}

function normalizeChoices(choices) {
  return choices.map(choice => (choice !== null && typeof choice === 'object' ? choice : { value: choice, label: String(choice) }));
}

function encodeChoice(value) {
  return value === null || value === undefined ? '' : String(value);
}

function decodeSettingValue(type, raw) {
  if (raw === '' || raw === undefined || raw === null) return null;
  if (type === 'bool') return raw === true || raw === 'true';
  if (type === 'number') {
    const number = Number(raw);
    return Number.isFinite(number) ? number : null;
  }
  return String(raw);
}

function settingRow(label, control, options = {}) {
  const tag = options.tag || 'div';
  const classes = ['settings-row', options.disabled ? 'disabled' : '', options.stacked ? 'stacked' : '', options.className || ''].filter(Boolean).join(' ');
  return `
    <${tag} class="${classes}"${options.id ? ` data-settings-row="${escapeHtml(options.id)}"` : ''}>
      <span class="settings-row-text">
        <span class="settings-row-label">${escapeHtml(label)}</span>
        ${options.hint ? `<span class="settings-row-hint">${escapeHtml(options.hint)}</span>` : ''}
      </span>
      <span class="settings-control">${control}</span>
    </${tag}>`;
}

function settingGroup(title, html) {
  return `
    <div class="settings-group">
      ${title ? `<h3 class="settings-group-title">${escapeHtml(title)}</h3>` : ''}
      <div class="settings-group-body">${html}</div>
    </div>`;
}

function settingToggle(key, label, options = {}) {
  const fallback = options.fallback === true;
  const checked = settingValue(key, fallback) === true;
  const control = `<input type="checkbox" class="settings-switch" ${settingAttributes(key, 'bool', fallback)} ${checked ? 'checked' : ''} ${options.disabled ? 'disabled' : ''}>`;
  return settingRow(label, control, Object.assign({}, options, { tag: 'label' }));
}

function settingSelect(key, label, choices, options = {}) {
  const list = normalizeChoices(choices);
  const type = options.type || valueType(list.length ? list[0].value : '');
  const current = encodeChoice(settingValue(key, options.fallback));
  const control = `
    <select class="settings-select" ${settingAttributes(key, type, options.fallback)} ${options.disabled ? 'disabled' : ''}>
      ${list.map(choice => `<option value="${escapeHtml(encodeChoice(choice.value))}" ${encodeChoice(choice.value) === current ? 'selected' : ''}>${escapeHtml(choice.label)}</option>`).join('')}
    </select>`;
  return settingRow(label, control, options);
}

function settingSegmented(key, label, choices, options = {}) {
  const list = normalizeChoices(choices);
  const type = options.type || valueType(list.find(choice => choice.value !== null && choice.value !== undefined)?.value ?? '');
  const current = encodeChoice(settingValue(key, options.fallback));
  const control = `
    <span class="settings-segmented" role="radiogroup">
      ${list.map(choice => {
        const encoded = encodeChoice(choice.value);
        return `<button type="button" role="radio" aria-checked="${encoded === current}" class="${encoded === current ? 'active' : ''}" ${settingAttributes(key, type, options.fallback)} data-setting-value="${escapeHtml(encoded)}" ${options.disabled ? 'disabled' : ''}>${escapeHtml(choice.label)}</button>`;
      }).join('')}
    </span>`;
  return settingRow(label, control, options);
}

function settingText(key, label, options = {}) {
  const value = settingValue(key, options.fallback ?? '');
  const control = `<input type="text" class="settings-input ${options.mono ? 'mono' : ''}" ${settingAttributes(key, 'string', options.fallback ?? '')} value="${escapeHtml(value ?? '')}" placeholder="${escapeHtml(options.placeholder || '')}" spellcheck="false" autocomplete="off" ${options.disabled ? 'disabled' : ''}>`;
  return settingRow(label, control, options);
}

function settingNumber(key, label, options = {}) {
  const value = settingValue(key, options.fallback ?? '');
  const limits = `${options.min !== undefined ? `min="${options.min}"` : ''} ${options.max !== undefined ? `max="${options.max}"` : ''} step="${options.step || 1}"`;
  const control = `
    <span class="settings-number">
      <input type="number" class="settings-input" ${settingAttributes(key, 'number', options.fallback ?? null)} ${limits} value="${escapeHtml(value ?? '')}" ${options.disabled ? 'disabled' : ''}>
      ${options.unit ? `<span class="settings-unit">${escapeHtml(options.unit)}</span>` : ''}
    </span>`;
  return settingRow(label, control, options);
}

function settingSlider(key, label, options = {}) {
  const min = options.min ?? 0;
  const max = options.max ?? 100;
  const value = Number(settingValue(key, options.fallback ?? min));
  const control = `
    <span class="settings-slider-wrap">
      <input type="range" class="settings-slider" ${settingAttributes(key, 'number', options.fallback ?? min)} min="${min}" max="${max}" step="${options.step || 1}" value="${value}" data-setting-unit="${escapeHtml(options.unit || '')}" ${options.disabled ? 'disabled' : ''}>
      <output class="settings-slider-value">${escapeHtml(`${value}${options.unit || ''}`)}</output>
    </span>`;
  return settingRow(label, control, options);
}

function settingPath(kind, key, label, options = {}) {
  const value = settingValue(key, '');
  const control = `
    <span class="settings-path">
      <span class="settings-path-value mono ${value ? '' : 'empty'}" data-setting-path-for="${escapeHtml(key)}" title="${escapeHtml(value || '')}">${escapeHtml(value || options.placeholder || 'Not set')}</span>
      <button type="button" class="secondary-button compact" data-setting-pick="${kind}" ${settingAttributes(key, 'string', '')} data-setting-title="${escapeHtml(options.title || label)}" data-setting-filter="${escapeHtml(options.filter || '')}" ${options.disabled ? 'disabled' : ''}>Browse…</button>
      <button type="button" class="icon-button" data-setting-clear="${escapeHtml(key)}" title="Clear" ${value ? '' : 'hidden'}>${ICON.close}</button>
    </span>`;
  return settingRow(label, control, Object.assign({ stacked: true }, options));
}

function settingFolder(key, label, options = {}) {
  return settingPath('folder', key, label, options);
}

function settingFile(key, label, options = {}) {
  return settingPath('file', key, label, options);
}

function pickPath(options = {}) {
  const requestId = `pick${settingsUi.nextPick++}`;
  return new Promise(resolve => {
    if (!host) {
      resolve(null);
      return;
    }
    settingsUi.picks.set(requestId, resolve);
    send({
      type: options.kind === 'file' ? 'pickFile' : 'pickFolder',
      requestId,
      title: options.title || '',
      filter: options.filter || '',
      initial: options.initial || '',
    });
  });
}

function controlDefault(control) {
  try {
    return JSON.parse(control.dataset.settingDefault || 'null');
  } catch {
    return null;
  }
}

function bindSettings(root = document) {
  for (const control of root.querySelectorAll('[data-setting]')) {
    const key = control.dataset.setting;
    const value = settingValue(key, controlDefault(control));
    if (control.matches('[data-setting-pick]')) continue;
    if (control.matches('[data-setting-value]')) {
      const active = control.dataset.settingValue === encodeChoice(value);
      control.classList.toggle('active', active);
      control.setAttribute('aria-checked', String(active));
    } else if (control.type === 'checkbox') {
      control.checked = value === true;
    } else if (document.activeElement !== control) {
      control.value = encodeChoice(value);
      if (control.type === 'range') updateSliderOutput(control);
    }
  }
  for (const label of root.querySelectorAll('[data-setting-path-for]')) {
    const value = settingValue(label.dataset.settingPathFor, '');
    label.textContent = value || 'Not set';
    label.title = value || '';
    label.classList.toggle('empty', !value);
    const clear = label.parentElement.querySelector('[data-setting-clear]');
    if (clear) clear.hidden = !value;
  }
}

function updateSliderOutput(control) {
  const output = control.parentElement.querySelector('.settings-slider-value');
  if (output) output.textContent = `${control.value}${control.dataset.settingUnit || ''}`;
}

function commitControl(control, persist) {
  const key = control.dataset.setting;
  const type = control.dataset.settingType || 'string';
  let value;
  if (control.type === 'checkbox') {
    value = control.checked;
  } else {
    value = decodeSettingValue(type, control.value);
    if (type === 'number' && value !== null) {
      if (control.min !== '') value = Math.max(Number(control.min), value);
      if (control.max !== '') value = Math.min(Number(control.max), value);
    }
    if (type === 'string' && value !== null) value = value.trim() || null;
  }
  setSettingValue(key, value, { persist });
}

document.addEventListener('change', event => {
  const control = event.target.closest && event.target.closest('[data-setting]');
  if (!control || control.tagName === 'BUTTON') return;
  commitControl(control, true);
  if (control.type === 'range') updateSliderOutput(control);
});

document.addEventListener('input', event => {
  const control = event.target.closest && event.target.closest('input[type="range"][data-setting]');
  if (!control) return;
  updateSliderOutput(control);
  commitControl(control, false);
});

document.addEventListener('click', event => {
  const target = event.target.closest && event.target.closest('[data-setting-value], [data-setting-pick], [data-setting-clear]');
  if (!target || target.disabled) return;

  if (target.matches('[data-setting-pick]')) {
    const key = target.dataset.setting;
    pickPath({ kind: target.dataset.settingPick, title: target.dataset.settingTitle, filter: target.dataset.settingFilter, initial: settingValue(key, '') }).then(path => {
      if (!path) return;
      setSettingValue(key, path);
      bindSettings(target.closest('.settings-row') || document);
    });
    return;
  }

  if (target.matches('[data-setting-clear]')) {
    setSettingValue(target.dataset.settingClear, null);
    bindSettings(target.closest('.settings-row') || document);
    return;
  }

  const value = decodeSettingValue(target.dataset.settingType, target.dataset.settingValue);
  setSettingValue(target.dataset.setting, value);
  const group = target.closest('.settings-segmented');
  for (const button of (group || target.parentElement).querySelectorAll('[data-setting-value]')) {
    const active = button === target;
    button.classList.toggle('active', active);
    button.setAttribute('aria-checked', String(active));
  }
});

const lightSchemeQuery = matchMedia('(prefers-color-scheme: light)');
const reducedMotionQuery = matchMedia('(prefers-reduced-motion: reduce)');
const themeCache = readStorage('hub.theme', {});
let themeSignature = '';

function themeValue(name) {
  const fallback = state.loaded ? THEME_DEFAULTS[name] : (themeCache[name] ?? THEME_DEFAULTS[name]);
  return setting(`theme.${name}`, fallback);
}

function currentTheme() {
  const theme = {};
  for (const name of Object.keys(THEME_DEFAULTS)) theme[name] = themeValue(name);
  return theme;
}

function hexToRgb(hex) {
  const number = parseInt(hex.slice(1), 16);
  return [(number >> 16) & 255, (number >> 8) & 255, number & 255];
}

function rgbToHex(rgb) {
  return `#${rgb.map(channel => Math.round(Math.max(0, Math.min(255, channel))).toString(16).padStart(2, '0')).join('')}`;
}

function mixHex(first, second, amount) {
  const from = hexToRgb(first);
  const to = hexToRgb(second);
  return rgbToHex(from.map((channel, index) => channel + (to[index] - channel) * amount));
}

function shiftHue(hex, degrees) {
  const [red, green, blue] = hexToRgb(hex).map(channel => channel / 255);
  const max = Math.max(red, green, blue);
  const min = Math.min(red, green, blue);
  const lightness = (max + min) / 2;
  const delta = max - min;
  let hue = 0;
  let saturation = 0;
  if (delta) {
    saturation = delta / (1 - Math.abs(2 * lightness - 1));
    if (max === red) hue = ((green - blue) / delta) % 6;
    else if (max === green) hue = (blue - red) / delta + 2;
    else hue = (red - green) / delta + 4;
    hue *= 60;
  }
  hue = (hue + degrees + 360) % 360;
  const chroma = (1 - Math.abs(2 * lightness - 1)) * saturation;
  const x = chroma * (1 - Math.abs(((hue / 60) % 2) - 1));
  const m = lightness - chroma / 2;
  const sector = Math.floor(hue / 60);
  const parts = [[chroma, x, 0], [x, chroma, 0], [0, chroma, x], [0, x, chroma], [x, 0, chroma], [chroma, 0, x]][sector] || [0, 0, 0];
  return rgbToHex(parts.map(part => (part + m) * 255));
}

function accentColors(accent) {
  const preset = ACCENTS.find(entry => entry.key === accent);
  if (preset) return preset;
  if (HEX_COLOR.test(accent || '')) return { key: 'custom', color: accent.toLowerCase(), second: shiftHue(accent, 34) };
  return ACCENTS[0];
}

function isLightTheme(theme = currentTheme()) {
  return theme.mode === 'light' || (theme.mode === 'system' && lightSchemeQuery.matches);
}

function wallpaperElement() {
  let element = document.getElementById('wallpaper');
  if (!element) {
    element = document.createElement('div');
    element.id = 'wallpaper';
    element.setAttribute('aria-hidden', 'true');
    document.body.prepend(element);
  }
  return element;
}

function applyTheme() {
  const theme = currentTheme();
  const root = document.documentElement;
  const light = isLightTheme(theme);
  root.dataset.theme = light ? 'light' : 'dark';

  const accent = accentColors(theme.accent);
  if (accent === ACCENTS[0] && !light) {
    for (const property of ACCENT_PROPERTIES) root.style.removeProperty(property);
  } else {
    const color = light ? mixHex(accent.color, '#0b0e16', 0.2) : accent.color;
    const second = light ? mixHex(accent.second, '#0b0e16', 0.2) : accent.second;
    const [red, green, blue] = hexToRgb(color);
    root.style.setProperty('--accent', color);
    root.style.setProperty('--accent-2', second);
    root.style.setProperty('--accent-gradient', `linear-gradient(135deg, ${color} 0%, ${second} 100%)`);
    root.style.setProperty('--accent-soft', `rgba(${red}, ${green}, ${blue}, ${light ? 0.12 : 0.14})`);
  }

  root.dataset.transition = ['fade', 'slide', 'none'].includes(theme.transition) ? theme.transition : 'fade';
  const reduced = theme.motion === 'reduced' || (theme.motion !== 'full' && reducedMotionQuery.matches);
  root.dataset.motion = reduced ? 'reduced' : 'full';

  const wallpaper = WALLPAPER_URL.test(theme.background || '') ? theme.background : '';
  root.classList.toggle('has-wallpaper', !!wallpaper);
  if (wallpaper || document.getElementById('wallpaper')) {
    const element = wallpaperElement();
    element.style.backgroundImage = wallpaper ? `url("${wallpaper}")` : '';
  }
  const blur = Math.max(0, Math.min(60, Number(theme.backgroundBlur) || 0));
  const opacity = Math.max(5, Math.min(100, Number(theme.backgroundOpacity) || THEME_DEFAULTS.backgroundOpacity));
  root.style.setProperty('--wallpaper-blur', `${blur}px`);
  root.style.setProperty('--wallpaper-opacity', String(opacity / 100));

  writeStorage('hub.theme', theme);

  const background = getComputedStyle(root).getPropertyValue('--bg').trim();
  const signature = `${background}|${light}`;
  if (signature !== themeSignature) {
    themeSignature = signature;
    if (HEX_COLOR.test(background)) send({ type: 'setTheme', background, light });
  }
}

onSetting('theme.*', applyTheme);
lightSchemeQuery.addEventListener('change', applyTheme);
reducedMotionQuery.addEventListener('change', applyTheme);
applyTheme();

function builtinSections() {
  return {
    before: [
      { id: 'general', title: 'General', keywords: 'startup start home library sync schedule refresh interval', builtin: true, render: generalSectionHtml },
      { id: 'folders', title: 'Library folders', keywords: 'folders paths scan extra games programs exe hidden ignore names config', builtin: true, render: foldersSectionHtml },
      { id: 'appearance', title: 'Appearance', keywords: 'theme light dark accent colour color background wallpaper image blur transitions motion animation', builtin: true, render: appearanceSectionHtml },
    ],
    after: [
      { id: 'backup', title: 'Backup & transfer', keywords: 'export import backup restore move another pc', builtin: true, render: backupSectionHtml },
      { id: 'diagnostics', title: 'Diagnostics', keywords: 'log errors cache clear disk space scan troubleshooting', builtin: true, render: diagnosticsSectionHtml },
      { id: 'about', title: 'About', keywords: 'version install uninstall start menu paths shortcuts keyboard', builtin: true, render: aboutSectionHtml },
    ],
  };
}

function allSettingsSections() {
  const builtins = builtinSections();
  const extra = extensions.settingsSections.filter(section => section && section.id && typeof section.render === 'function');
  return [...builtins.before, ...extra, ...builtins.after];
}

function openSettings(sectionId) {
  if (sectionId) {
    settingsUi.section = sectionId;
    state.prefs.settingsSection = sectionId;
    savePrefs();
  }
  if (typeof home !== 'undefined' && home.visible && typeof exitHome === 'function') exitHome();
  if (state.view === 'settings') {
    elements.scroller.scrollTop = 0;
    renderSettingsView();
  } else {
    setView('settings');
  }
}

function requestSettingsInfo() {
  settingsUi.infoRequested = true;
  send({ type: 'settingsInfo' });
}

function sectionIcon(section) {
  const icon = SECTION_ICONS[section.id];
  return (icon && icon()) || ICON.gear;
}

function renderSettingsSection(section, body) {
  try {
    body.innerHTML = section.render();
  } catch (error) {
    reportClientError(error);
    body.innerHTML = `<div class="settings-note error">${ICON.alert}<span>This section couldn't be shown.</span></div>`;
    return;
  }
  try {
    if (typeof section.bind === 'function') section.bind(body);
  } catch (error) {
    reportClientError(error);
  }
  bindSettings(body);
}

function renderSettingsView() {
  elements.pageTitle.textContent = 'Settings';
  elements.summary.textContent = '';
  elements.filters.innerHTML = '';
  elements.zoomControl.classList.add('hidden');

  const sections = allSettingsSections();
  const query = normalize(state.search.trim());
  const matching = query ? sections.filter(section => normalize(`${section.title} ${section.keywords || ''}`).includes(query)) : sections;
  const selected = sections.find(section => section.id === settingsUi.section) || sections[0];
  const shown = query ? matching : [selected];
  const key = `${query ? 'search' : 'single'}:${shown.map(section => section.id).join(',')}`;

  const existing = elements.results.querySelector('.settings-view');
  if (existing && existing.dataset.key === key) {
    for (const button of existing.querySelectorAll('[data-settings-nav]')) {
      button.classList.toggle('active', !query && button.dataset.settingsNav === selected.id);
      button.classList.toggle('dim', !!query && !matching.includes(sections.find(section => section.id === button.dataset.settingsNav)));
    }
    return;
  }

  if (!settingsUi.info || !existing) requestSettingsInfo();
  const matchingIds = new Set(matching.map(section => section.id));
  elements.results.innerHTML = `
    <div class="settings-view" data-key="${escapeHtml(key)}">
      <nav class="settings-nav">
        ${sections.map(section => `
          <button class="settings-nav-item ${!query && section.id === selected.id ? 'active' : ''} ${query && !matchingIds.has(section.id) ? 'dim' : ''}" data-settings-nav="${escapeHtml(section.id)}">
            <span class="nav-icon">${sectionIcon(section)}</span>
            <span class="nav-label">${escapeHtml(section.title)}</span>
          </button>`).join('')}
      </nav>
      <div class="settings-pane">
        ${shown.length ? shown.map(section => `
          <section class="settings-section ${section.builtin ? 'builtin' : 'external'}" data-settings-section="${escapeHtml(section.id)}">
            <h2 class="settings-section-title">${escapeHtml(section.title)}</h2>
            <div class="settings-section-body"></div>
          </section>`).join('') : `<div class="settings-empty">${ICON.search}<span>No settings match “${escapeHtml(state.search.trim())}”</span></div>`}
      </div>
    </div>`;

  for (const section of shown) {
    const body = elements.results.querySelector(`[data-settings-section="${CSS.escape(section.id)}"] .settings-section-body`);
    if (body) renderSettingsSection(section, body);
  }
}

function refreshBuiltinSections(ids) {
  if (state.view !== 'settings') return;
  const sections = allSettingsSections();
  for (const body of elements.results.querySelectorAll('.settings-section')) {
    const section = sections.find(entry => entry.id === body.dataset.settingsSection);
    if (!section || !section.builtin || (ids && !ids.includes(section.id))) continue;
    const target = body.querySelector('.settings-section-body');
    const focusKey = document.activeElement && target.contains(document.activeElement) ? document.activeElement.dataset.settingsFocus : null;
    renderSettingsSection(section, target);
    if (focusKey) {
      const field = target.querySelector(`[data-settings-focus="${CSS.escape(focusKey)}"]`);
      if (field) field.focus();
    }
  }
}

VIEWS.settings = 'Settings';
extensions.views.settings = { render: renderSettingsView };

function loadingRows(count = 3) {
  return `<div class="settings-loading">${Array.from({ length: count }, () => '<span class="settings-skeleton"></span>').join('')}</div>`;
}

function infoValue(path, fallback = null) {
  let value = settingsUi.info;
  for (const part of path.split('.')) {
    if (value === null || value === undefined) return fallback;
    value = value[part];
  }
  return value === undefined || value === null ? fallback : value;
}

function generalSectionHtml() {
  const sync = infoValue('sync');
  let syncControl;
  if (!sync) {
    syncControl = settingRow('Refresh the library automatically', '<span class="settings-skeleton inline"></span>');
  } else if (!sync.available) {
    syncControl = settingRow('Refresh the library automatically', '<span class="settings-muted">Not scheduled</span>', { hint: 'The scheduled sync task wasn’t found. Run the shortcut sync setup once to create it.', disabled: true });
  } else {
    const known = SYNC_INTERVALS.some(option => option.value === sync.interval);
    const options = known ? SYNC_INTERVALS : [{ value: sync.interval, label: `Custom (${sync.interval})` }, ...SYNC_INTERVALS];
    syncControl = settingRow('Refresh the library automatically', `
      <select class="settings-select" data-settings-sync ${settingsUi.syncBusy ? 'disabled' : ''}>
        ${options.map(option => `<option value="${escapeHtml(option.value)}" ${option.value === sync.interval ? 'selected' : ''}>${escapeHtml(option.label)}</option>`).join('')}
      </select>`, { hint: 'Also runs when you sign in to Windows. Updates Start Menu shortcuts.' });
  }

  return `
    ${settingGroup('Start up', settingSegmented('prefs.startupView', 'Open Game Hub on', [
      { value: null, label: 'Where I left off' },
      { value: 'home', label: 'Home screen' },
      { value: 'library', label: 'Library' },
    ], { type: 'string' }))}
    ${settingGroup('Library', `
      ${syncControl}
      ${settingRow('Refresh now', `<button type="button" class="secondary-button compact" data-settings-action="refresh" ${state.syncing ? 'disabled' : ''}>${ICON.refresh}<span>${state.syncing ? 'Refreshing…' : 'Refresh'}</span></button>`, { hint: state.generatedAt ? `Last refreshed ${(relativeTime(state.generatedAt) || '').toLowerCase()}` : '' })}
    `)}`;
}

function foldersSectionHtml() {
  const config = infoValue('config');
  if (!config) return loadingRows(4);
  return `
    ${CONFIG_LISTS.map(list => {
      const items = config[list.key] || [];
      const rows = items.length
        ? items.map((item, index) => `
          <li class="settings-list-item">
            <span class="settings-list-text mono" title="${escapeHtml(item)}">${escapeHtml(item)}</span>
            ${list.kind !== 'text' ? `<button type="button" class="icon-button" data-settings-open-path="${escapeHtml(item)}" title="Show in File Explorer">${ICON.folder}</button>` : ''}
            <button type="button" class="icon-button" data-settings-list-remove="${list.key}" data-index="${index}" title="Remove from this list">${ICON.close}</button>
          </li>`).join('')
        : '<li class="settings-list-empty">Nothing added</li>';
      const adders = list.kind === 'text'
        ? `<form class="settings-list-add" data-settings-list-form="${list.key}">
             <input type="text" class="settings-input" name="value" placeholder="Exact game name" spellcheck="false" autocomplete="off" data-settings-focus="${list.key}">
             <button type="submit" class="secondary-button compact">${ICON.plus || '+'}<span>Add</span></button>
           </form>`
        : `<div class="settings-list-add">
             <button type="button" class="secondary-button compact" data-settings-list-add="${list.key}" data-kind="folder">${ICON.folder}<span>Add folder</span></button>
             ${list.kind === 'both' ? `<button type="button" class="secondary-button compact" data-settings-list-add="${list.key}" data-kind="file">${ICON.gamepad}<span>Add program</span></button>` : ''}
           </div>`;
      return `
        <div class="settings-group">
          <h3 class="settings-group-title">${escapeHtml(list.title)}<span class="count">${items.length}</span></h3>
          <p class="settings-group-hint">${escapeHtml(list.hint)}</p>
          <ul class="settings-list">${rows}</ul>
          ${adders}
        </div>`;
    }).join('')}
    <div class="settings-footnote">
      <span>Changes rescan the library. Removing an entry never deletes files.</span>
      <button type="button" class="link-button" data-settings-open-file="config">Open config.json</button>
    </div>`;
}

function appearanceSectionHtml() {
  const theme = currentTheme();
  const accent = accentColors(theme.accent);
  const custom = accent.key === 'custom';
  const wallpaper = WALLPAPER_URL.test(theme.background || '') ? theme.background : '';
  const swatches = `
    <span class="accent-swatches">
      ${ACCENTS.map(entry => `<button type="button" class="accent-swatch ${entry.key === accent.key ? 'active' : ''}" style="--swatch:${entry.color};--swatch-2:${entry.second}" data-settings-accent="${entry.key}" title="${escapeHtml(entry.label)}" aria-label="${escapeHtml(entry.label)}"></button>`).join('')}
      <label class="accent-swatch custom ${custom ? 'active' : ''}" title="Custom colour" style="${custom ? `--swatch:${accent.color};--swatch-2:${accent.second}` : ''}">
        <input type="color" value="${escapeHtml(custom ? accent.color : '#7b8cff')}" data-settings-accent-custom aria-label="Custom colour">
      </label>
    </span>`;
  const preview = wallpaper
    ? `<span class="wallpaper-preview" style="background-image:url(&quot;${escapeHtml(wallpaper)}&quot;)"></span>`
    : `<span class="wallpaper-preview no-image">${ICON.image || ICON.palette}</span>`;

  return `
    ${settingGroup('Theme', `
      ${settingSegmented('theme.mode', 'Colour mode', [
        { value: 'dark', label: 'Dark' },
        { value: 'light', label: 'Light' },
        { value: 'system', label: 'Match Windows' },
      ], { fallback: THEME_DEFAULTS.mode, hint: 'The home screen always stays dark.' })}
      ${settingRow('Accent colour', swatches)}
    `)}
    ${settingGroup('Background', `
      ${settingRow('Background image', `
        <span class="wallpaper-control">
          ${preview}
          <button type="button" class="secondary-button compact" data-settings-action="pick-wallpaper">${ICON.image || ICON.folder}<span>${wallpaper ? 'Change' : 'Choose image'}</span></button>
          ${wallpaper ? '<button type="button" class="link-button" data-settings-action="clear-wallpaper">Remove</button>' : ''}
        </span>`, { hint: wallpaper ? setting('theme.backgroundName', '') : 'Shown behind the library.' })}
      ${settingSlider('theme.backgroundBlur', 'Blur', { min: 0, max: 60, step: 1, unit: ' px', fallback: THEME_DEFAULTS.backgroundBlur, disabled: !wallpaper })}
      ${settingSlider('theme.backgroundOpacity', 'Image strength', { min: 5, max: 100, step: 5, unit: '%', fallback: THEME_DEFAULTS.backgroundOpacity, disabled: !wallpaper })}
    `)}
    ${settingGroup('Motion', `
      ${settingSegmented('theme.transition', 'Page transitions', [
        { value: 'fade', label: 'Fade' },
        { value: 'slide', label: 'Slide' },
        { value: 'none', label: 'None' },
      ], { fallback: THEME_DEFAULTS.transition })}
      ${settingSegmented('theme.motion', 'Animations', [
        { value: 'system', label: 'Match Windows' },
        { value: 'full', label: 'Full' },
        { value: 'reduced', label: 'Reduced' },
      ], { fallback: THEME_DEFAULTS.motion })}
    `)}`;
}

function backupSectionHtml() {
  return `
    ${settingGroup('Export', settingRow('Save a backup', `
      <button type="button" class="primary-button compact" data-settings-action="export">${ICON.download || ICON.save}<span>Export…</span></button>`,
      { hint: 'Favorites, play status, collections, notes, custom artwork, launch options and settings. Choose .zip to include artwork, or .json for data only.' }))}
    ${settingGroup('Import', settingRow('Restore from a backup', `
      <button type="button" class="secondary-button compact" data-settings-action="import">${ICON.upload || ICON.folder}<span>Import…</span></button>`,
      { hint: 'You see what’s inside before anything changes. Current data is backed up first.' }))}`;
}

function cacheRowsHtml() {
  const cache = infoValue('diagnostics.cache', []);
  const total = cache.reduce((sum, entry) => sum + (entry.bytes || 0), 0);
  return `
    <div class="cache-bar">${cache.filter(entry => entry.bytes > 0).map((entry, index) => `<i style="flex:${entry.bytes};background:${PART_COLORS[index % PART_COLORS.length]}" title="${escapeHtml(entry.label)}"></i>`).join('')}</div>
    <ul class="cache-list">
      ${cache.map((entry, index) => `
        <li class="cache-item">
          <span class="cache-dot" style="background:${PART_COLORS[index % PART_COLORS.length]}"></span>
          <span class="cache-label">${escapeHtml(entry.label)}</span>
          <span class="cache-size">${escapeHtml(formatBytes(entry.bytes || 0))}</span>
          ${entry.clearable ? `<button type="button" class="link-button" data-settings-clear-cache="${escapeHtml(entry.key)}" data-label="${escapeHtml(entry.label)}" ${entry.bytes ? '' : 'disabled'}>Clear</button>` : '<span class="cache-keep"></span>'}
        </li>`).join('')}
      <li class="cache-item total"><span class="cache-dot"></span><span class="cache-label">Total</span><span class="cache-size">${escapeHtml(formatBytes(total))}</span><span class="cache-keep"></span></li>
    </ul>`;
}

function diagnosticsSectionHtml() {
  const diagnostics = infoValue('diagnostics');
  if (!diagnostics) return loadingRows(5);
  const when = iso => (iso ? `${relativeTime(iso) || ''} · ${formatDate(iso) || ''}` : 'Never');
  const errors = diagnostics.errors || [];
  return `
    ${settingGroup('Status', `
      <dl class="settings-facts">
        <dt>Library refreshed</dt><dd>${escapeHtml(when(diagnostics.libraryGeneratedAt || state.generatedAt))}</dd>
        <dt>Sync log written</dt><dd>${escapeHtml(when(diagnostics.syncLogTime))}</dd>
        <dt>Sizes measured</dt><dd>${escapeHtml(when(diagnostics.lastMeasured))}</dd>
        <dt>Last full size scan</dt><dd>${escapeHtml(when(diagnostics.lastFullScan))}</dd>
        <dt>Connection</dt><dd>${settingsUi.online ? 'Online' : 'Offline'}</dd>
      </dl>
      <div class="settings-actions">
        <button type="button" class="secondary-button compact" data-settings-action="reload-info">${ICON.refresh}<span>Update</span></button>
        <button type="button" class="secondary-button compact" data-settings-action="copy-diagnostics">${ICON.copy || ICON.file || ICON.info}<span>Copy details</span></button>
      </div>`)}
    ${settingGroup('Disk space', cacheRowsHtml())}
    <div class="settings-group">
      <h3 class="settings-group-title">Recent problems<span class="count">${errors.length}</span></h3>
      ${errors.length ? `
        <ul class="error-list">
          ${errors.slice().reverse().slice(0, 30).map(entry => `
            <li class="error-item">
              <span class="error-meta"><span class="error-area">${escapeHtml(entry.Area || 'app')}</span><span class="error-time" title="${escapeHtml(formatDate(entry.Time) || '')}">${escapeHtml(relativeTime(entry.Time) || '')}</span></span>
              <span class="error-message">${escapeHtml(String(entry.Message || '').split('\n')[0])}</span>
            </li>`).join('')}
        </ul>` : '<p class="settings-group-hint">Nothing has gone wrong recently.</p>'}
      <div class="settings-actions">
        <button type="button" class="secondary-button compact" data-settings-open-file="errors">${ICON.file || ICON.external}<span>Open error log</span></button>
        ${errors.length ? '<button type="button" class="link-button" data-settings-clear-cache="log" data-label="Error log">Clear</button>' : ''}
      </div>
    </div>
    <div class="settings-group">
      <h3 class="settings-group-title">Last library sync</h3>
      ${diagnostics.syncLog ? `<details class="sync-log"><summary>Show log</summary><pre>${escapeHtml(diagnostics.syncLog)}</pre></details>` : '<p class="settings-group-hint">No log yet.</p>'}
      <div class="settings-actions">
        <button type="button" class="secondary-button compact" data-settings-open-file="syncLog">${ICON.file || ICON.external}<span>Open last-run.log</span></button>
      </div>
    </div>`;
}

function aboutSectionHtml() {
  const about = infoValue('about');
  const install = infoValue('install');
  if (!about) return loadingRows(4);
  const pathRow = (label, path, folder) => `
    <dt>${escapeHtml(label)}</dt>
    <dd class="path-fact"><span class="mono" title="${escapeHtml(path || '')}">${escapeHtml(path || '—')}</span>${path ? `<button type="button" class="icon-button" data-settings-open-path="${escapeHtml(folder || path)}" title="Show in File Explorer">${ICON.folder}</button>` : ''}</dd>`;
  const parentOf = path => (path ? path.replace(/\\[^\\]+$/, '') : path);

  let installRow = '';
  if (install) {
    const status = install.installed
      ? 'In the Start Menu and Installed apps'
      : install.shortcut || install.registered ? 'Partly installed' : 'Not installed';
    installRow = settingRow('Start Menu and Installed apps', `
      <span class="settings-actions inline">
        <span class="settings-status ${install.installed ? 'good' : ''}">${escapeHtml(status)}</span>
        ${install.installed ? '' : `<button type="button" class="primary-button compact" data-settings-action="install">${install.shortcut || install.registered ? 'Repair' : 'Install'}</button>`}
        ${install.shortcut || install.registered ? '<button type="button" class="secondary-button compact" data-settings-action="uninstall">Remove</button>' : ''}
      </span>`, { hint: 'Adds a Start Menu shortcut and an entry in Settings › Apps. Your games and data are not touched.' });
  }

  return `
    <div class="about-head">
      <span class="about-mark">${ICON.gamepad}</span>
      <span class="about-name">Game Hub</span>
      <span class="about-version">${escapeHtml(about.version || '')}</span>
    </div>
    ${settingGroup('Credits', `
      <p class="about-credit">Created by Claude, an AI model made by Anthropic. Every line of Game Hub's code was written by AI, and all of it has been proofread by people.</p>`)}
    ${settingGroup('Versions', `
      <dl class="settings-facts">
        <dt>Built</dt><dd>${escapeHtml(formatDate(about.built) || '—')}</dd>
        <dt>WebView2</dt><dd>${escapeHtml(about.webView2 || '—')}</dd>
        <dt>.NET runtime</dt><dd>${escapeHtml(about.framework || '—')}</dd>
        <dt>Windows</dt><dd>${escapeHtml(about.os || '—')}</dd>
      </dl>`)}
    ${installRow ? settingGroup('Install', installRow) : ''}
    ${settingGroup('Locations', `
      <dl class="settings-facts paths">
        ${pathRow('App', about.exe, parentOf(about.exe))}
        ${pathRow('Library data', about.cacheDir)}
        ${pathRow('Sync folder', about.syncDir)}
        ${pathRow('Config file', infoValue('configFile'), parentOf(infoValue('configFile')))}
      </dl>`)}
    ${settingGroup('Help', settingRow('Keyboard shortcuts', `<button type="button" class="secondary-button compact" data-settings-action="shortcuts">${ICON.keyboard}<span>Show</span></button>`, { hint: 'Press F1 or ? anywhere.' }))}`;
}

function saveConfigList(key, items) {
  const config = infoValue('config');
  if (!config) return;
  config[key] = items;
  send({ type: 'saveConfigList', key, items });
  refreshBuiltinSections(['folders']);
}

function addConfigItem(key, value) {
  const config = infoValue('config');
  const text = String(value || '').trim();
  if (!config || !text) return;
  const items = config[key] || [];
  if (items.some(item => item.toLowerCase() === text.toLowerCase())) {
    toast('That’s already in the list');
    return;
  }
  saveConfigList(key, [...items, text]);
}

function collectLocalData() {
  const data = {};
  try {
    for (let index = 0; index < localStorage.length; index++) {
      const key = localStorage.key(index);
      if (!key || !key.startsWith('hub.')) continue;
      data[key] = readStorage(key, null);
    }
  } catch {
  }
  return data;
}

function restoreLocalData(local) {
  if (!local || typeof local !== 'object') return;
  for (const [key, value] of Object.entries(local)) {
    if (key.startsWith('hub.') && value !== null && value !== undefined) writeStorage(key, value);
  }
  if (local['hub.prefs']) state.prefs = Object.assign({ layout: 'grid', sort: 'name', cardSize: 190, hideDemos: false, sortDescending: null }, local['hub.prefs']);
  if (Array.isArray(local['hub.favorites'])) state.favorites = new Set(local['hub.favorites']);
}

function confirmModal(options) {
  modal.open(`
    <div class="modal-head">
      <h2>${escapeHtml(options.title)}</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </div>
    <div class="modal-body">${options.body}</div>
    <div class="modal-foot">
      <button class="secondary-button compact" data-modal-close>${escapeHtml(options.cancel || 'Cancel')}</button>
      <button class="primary-button compact ${options.danger ? 'danger' : ''}" data-settings-confirm>${escapeHtml(options.confirm)}</button>
    </div>`, 'settings-modal');
  modal.onClose = options.onCancel || null;
  requestAnimationFrame(() => {
    const button = modal.panel && modal.panel.querySelector('[data-settings-confirm]');
    if (!button) return;
    button.focus();
    button.addEventListener('click', () => {
      modal.onClose = null;
      modal.close();
      options.onConfirm();
    }, { once: true });
  });
}

function showImportSummary(summary, local) {
  settingsUi.pendingImport = local || null;
  const exported = summary.exportedAt ? formatDate(summary.exportedAt) : 'Unknown date';
  confirmModal({
    title: 'Import this backup?',
    body: `
      <dl class="settings-facts">
        <dt>File</dt><dd class="mono">${escapeHtml(summary.file || '')}</dd>
        <dt>Saved</dt><dd>${escapeHtml(exported)}</dd>
        <dt>Games with your data</dt><dd>${escapeHtml(formatCount(summary.games || 0))}</dd>
        <dt>Settings</dt><dd>${escapeHtml(formatCount(summary.settings || 0))}</dd>
        <dt>Custom artwork</dt><dd>${summary.art ? escapeHtml(`${formatCount(summary.artFiles || 0)} files`) : 'Not included'}</dd>
      </dl>
      <p class="settings-group-hint">Matching games get the backup’s data. Your current data is saved first so nothing is lost.</p>`,
    confirm: 'Import',
    onConfirm: () => send({ type: 'applyImport' }),
    onCancel: () => {
      settingsUi.pendingImport = null;
      send({ type: 'cancelImport' });
    },
  });
}

function confirmClearCache(kind, label) {
  const isLog = kind === 'log';
  confirmModal({
    title: isLog ? 'Clear the error log?' : `Clear ${label.toLowerCase()}?`,
    body: `<p class="settings-modal-text">${isLog
      ? 'The list of recent problems is emptied.'
      : 'The files go to the Recycle Bin. Game Hub downloads them again as you browse. Your custom artwork is kept.'}</p>`,
    confirm: 'Clear',
    danger: true,
    onConfirm: () => send({ type: 'clearCache', kind }),
  });
}

function confirmHubUninstall() {
  confirmModal({
    title: 'Remove from the Start Menu?',
    body: '<p class="settings-modal-text">The Start Menu shortcut goes to the Recycle Bin and Game Hub leaves Installed apps. The app keeps running and your library data stays.</p>',
    confirm: 'Remove',
    danger: true,
    onConfirm: () => send({ type: 'uninstallHub' }),
  });
}

function diagnosticsText() {
  const about = infoValue('about', {});
  const diagnostics = infoValue('diagnostics', {});
  const lines = [
    `Game Hub ${about.version || ''}`,
    `WebView2 ${about.webView2 || ''}`,
    `.NET ${about.framework || ''}`,
    `${about.os || ''}`,
    `Games ${state.games.length}`,
    `Library refreshed ${diagnostics.libraryGeneratedAt || state.generatedAt || 'never'}`,
    `Sizes measured ${diagnostics.lastMeasured || 'never'}`,
    `Online ${settingsUi.online}`,
    '',
    ...(diagnostics.errors || []).slice(-20).map(entry => `${entry.Time} [${entry.Area}] ${String(entry.Message || '').split('\n')[0]}`),
  ];
  return lines.join('\r\n');
}

function copyText(text) {
  const done = () => toast('Copied', 'info', ICON.check);
  if (navigator.clipboard && navigator.clipboard.writeText) {
    navigator.clipboard.writeText(text).then(done, () => toast('Couldn’t copy', 'error'));
  }
}

elements.results.addEventListener('click', event => {
  if (state.view !== 'settings') return;
  const closest = selector => event.target.closest(selector);
  let element;

  if ((element = closest('[data-settings-nav]'))) {
    settingsUi.section = element.dataset.settingsNav;
    state.prefs.settingsSection = settingsUi.section;
    savePrefs();
    if (state.search) {
      state.search = '';
      elements.search.value = '';
      elements.searchClear.hidden = true;
    }
    elements.scroller.scrollTop = 0;
    renderSettingsView();
  } else if ((element = closest('[data-settings-accent]'))) {
    setSettingValue('theme.accent', element.dataset.settingsAccent === 'indigo' ? null : element.dataset.settingsAccent);
    refreshBuiltinSections(['appearance']);
  } else if ((element = closest('[data-settings-list-remove]'))) {
    const key = element.dataset.settingsListRemove;
    const items = (infoValue('config', {})[key] || []).slice();
    items.splice(Number(element.dataset.index), 1);
    saveConfigList(key, items);
  } else if ((element = closest('[data-settings-list-add]'))) {
    const key = element.dataset.settingsListAdd;
    const kind = element.dataset.kind;
    pickPath({
      kind,
      title: kind === 'file' ? 'Choose a game program' : 'Choose a folder',
      filter: kind === 'file' ? 'Programs (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|All files|*.*' : '',
    }).then(path => {
      if (path) addConfigItem(key, path);
    });
  } else if ((element = closest('[data-settings-open-path]'))) {
    send({ type: 'openFolder', path: element.dataset.settingsOpenPath });
  } else if ((element = closest('[data-settings-open-file]'))) {
    send({ type: 'openDiagFile', file: element.dataset.settingsOpenFile });
  } else if ((element = closest('[data-settings-clear-cache]'))) {
    confirmClearCache(element.dataset.settingsClearCache, element.dataset.label || 'cache');
  } else if ((element = closest('[data-settings-action]'))) {
    runSettingsAction(element.dataset.settingsAction, element);
  }
});

elements.results.addEventListener('submit', event => {
  const form = event.target.closest('[data-settings-list-form]');
  if (!form) return;
  event.preventDefault();
  addConfigItem(form.dataset.settingsListForm, form.elements.value.value);
});

elements.results.addEventListener('change', event => {
  if (event.target.matches('[data-settings-sync]')) {
    settingsUi.syncBusy = true;
    event.target.disabled = true;
    send({ type: 'setSyncInterval', value: event.target.value });
  } else if (event.target.matches('[data-settings-accent-custom]')) {
    setSettingValue('theme.accent', event.target.value);
    refreshBuiltinSections(['appearance']);
  }
});

elements.results.addEventListener('input', event => {
  if (!event.target.matches('[data-settings-accent-custom]')) return;
  setSettingValue('theme.accent', event.target.value, { persist: false });
  const swatch = event.target.closest('.accent-swatch');
  if (swatch) {
    swatch.style.setProperty('--swatch', event.target.value);
    swatch.style.setProperty('--swatch-2', shiftHue(event.target.value, 34));
  }
});

function runSettingsAction(action, element) {
  switch (action) {
    case 'refresh':
      if (!state.syncing) {
        state.syncing = true;
        renderFooter();
        send({ type: 'refresh' });
        element.disabled = true;
      }
      break;
    case 'pick-wallpaper':
      send({ type: 'pickThemeBackground' });
      break;
    case 'clear-wallpaper':
      setSettingValue('theme.background', null);
      setSettingValue('theme.backgroundName', null);
      refreshBuiltinSections(['appearance']);
      break;
    case 'export':
      send({ type: 'exportData', local: collectLocalData() });
      break;
    case 'import':
      send({ type: 'importData' });
      break;
    case 'reload-info':
      requestSettingsInfo();
      break;
    case 'copy-diagnostics':
      copyText(diagnosticsText());
      break;
    case 'install':
      send({ type: 'installHub' });
      break;
    case 'uninstall':
      confirmHubUninstall();
      break;
    case 'shortcuts':
      openShortcutSheet();
      break;
    case 'retry-online':
      send({ type: 'checkOnline' });
      break;
  }
}

function keysHtml(keys) {
  return keys.map(key => `<kbd>${escapeHtml(key)}</kbd>`).join('<span class="kbd-plus"></span>');
}

function openShortcutSheet() {
  modal.open(`
    <div class="modal-head">
      <h2>Keyboard shortcuts</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </div>
    <div class="modal-body shortcut-sheet">
      ${SHORTCUT_GROUPS.map(group => `
        <section class="shortcut-group">
          <h3>${escapeHtml(group.title)}</h3>
          <dl>${group.keys.map(([keys, label]) => `<dt>${keysHtml(keys)}</dt><dd>${escapeHtml(label)}</dd>`).join('')}</dl>
        </section>`).join('')}
      <section class="shortcut-group wide">
        <h3>Search filters</h3>
        <dl class="syntax">${SEARCH_SYNTAX.map(([example, label]) => `<dt><code>${escapeHtml(example)}</code></dt><dd>${escapeHtml(label)}</dd>`).join('')}</dl>
      </section>
    </div>`, 'shortcut-modal');
}

extensions.keyHandlers.push(event => {
  const inField = event.target.tagName === 'INPUT' || event.target.tagName === 'TEXTAREA' || event.target.tagName === 'SELECT' || event.target.isContentEditable;
  if (event.ctrlKey && !event.altKey && !event.shiftKey && event.key === ',') {
    event.preventDefault();
    if (modal.isOpen) modal.close();
    openSettings();
    return true;
  }
  if (event.key === 'F1' || (event.key === '?' && !inField && !event.ctrlKey && !event.altKey)) {
    event.preventDefault();
    if (modal.isOpen && modal.panel && modal.panel.classList.contains('shortcut-modal')) modal.close();
    else if (!modal.isOpen) openShortcutSheet();
    return true;
  }
  return false;
});

function reportClientError(error, source, line) {
  const message = error && error.message ? error.message : String(error);
  const signature = `${message}|${source || ''}|${line || ''}`;
  if (settingsUi.errorsSent.has(signature) || settingsUi.errorsSent.size > 40) return;
  settingsUi.errorsSent.add(signature);
  send({ type: 'clientError', message, source: source || '', line: line || 0, stack: error && error.stack ? String(error.stack) : '' });
}

function logDiagnostic(area, message) {
  send({ type: 'diagLog', area: String(area || 'page'), message: String(message || '') });
}

window.addEventListener('error', event => {
  if (event.target && event.target !== window) return;
  reportClientError(event.error || event.message, event.filename, event.lineno);
});

window.addEventListener('unhandledrejection', event => {
  reportClientError(event.reason || 'Unhandled promise rejection');
});

const ONLINE_VIEWS = ['free', 'deals', 'stores'];

function offlineCallout() {
  let element = document.getElementById('offlineCallout');
  if (!element) {
    element = document.createElement('div');
    element.id = 'offlineCallout';
    element.className = 'offline-callout';
    element.innerHTML = `${ICON.offline}<span>You’re offline. Showing what was saved last time. This page updates by itself when you’re back online.</span><button type="button" class="secondary-button compact" data-offline-retry>Try again</button>`;
    elements.results.before(element);
    element.querySelector('[data-offline-retry]').addEventListener('click', () => send({ type: 'checkOnline' }));
  }
  return element;
}

function updateOfflineCallout() {
  const show = !settingsUi.online && ONLINE_VIEWS.includes(state.view) && !document.body.classList.contains('home-active');
  if (show) offlineCallout().hidden = false;
  else {
    const element = document.getElementById('offlineCallout');
    if (element) element.hidden = true;
  }
}

function setOnline(online) {
  clearTimeout(settingsUi.onlineTimer);
  if (!online) settingsUi.onlineTimer = setTimeout(() => send({ type: 'checkOnline' }), 60 * 1000);
  if (settingsUi.online === online) return;
  settingsUi.online = online;
  document.documentElement.classList.toggle('is-offline', !online);
  renderFooter();
  updateOfflineCallout();
  if (online) {
    toast('Back online', 'info', ICON.wifi || ICON.check);
    if (typeof free !== 'undefined' && free && !free.loading) send({ type: 'freeGames', force: true });
    if (typeof requestWishlist === 'function') requestWishlist(true);
    if (ONLINE_VIEWS.includes(state.view)) renderAll();
  }
  if (state.view === 'settings') refreshBuiltinSections(['diagnostics']);
}

extensions.sidebarFooter.push(() => (settingsUi.online
  ? ''
  : `<button type="button" class="offline-pill" data-offline-check title="No internet connection. Click to check again.">${ICON.offline}<span>Offline</span></button>`));

elements.sidebar.addEventListener('click', event => {
  if (event.target.closest('[data-offline-check]')) send({ type: 'checkOnline' });
});

window.addEventListener('offline', () => setOnline(false));
window.addEventListener('online', () => send({ type: 'checkOnline' }));
if (!settingsUi.online) document.documentElement.classList.add('is-offline');

extensions.afterRender.push(updateOfflineCallout);

const LIBRARY_CACHE = { database: 'gamehub', store: 'cache', key: 'library' };
let libraryCacheTimer = null;

function openLibraryCache() {
  return new Promise((resolve, reject) => {
    if (!window.indexedDB) {
      reject(new Error('IndexedDB unavailable'));
      return;
    }
    const request = indexedDB.open(LIBRARY_CACHE.database, 1);
    request.onupgradeneeded = () => request.result.createObjectStore(LIBRARY_CACHE.store);
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

function writeLibraryCache(message) {
  openLibraryCache().then(database => {
    const transaction = database.transaction(LIBRARY_CACHE.store, 'readwrite');
    transaction.objectStore(LIBRARY_CACHE.store).put(Object.assign({}, message, { cachedAt: new Date().toISOString() }), LIBRARY_CACHE.key);
    transaction.oncomplete = () => database.close();
    transaction.onerror = () => database.close();
  }).catch(() => {});
}

function readLibraryCache() {
  return openLibraryCache().then(database => new Promise(resolve => {
    const transaction = database.transaction(LIBRARY_CACHE.store, 'readonly');
    const request = transaction.objectStore(LIBRARY_CACHE.store).get(LIBRARY_CACHE.key);
    request.onsuccess = () => resolve(request.result || null);
    request.onerror = () => resolve(null);
    transaction.oncomplete = () => database.close();
  })).catch(() => null);
}

extensions.libraryHandlers.push(message => {
  diffSettings();
  applyTheme();
  if (message.cached) return;
  clearTimeout(libraryCacheTimer);
  libraryCacheTimer = setTimeout(() => {
    const snapshot = Object.assign({}, message, { sizes: {} });
    for (const record of Object.values(state.sizes)) snapshot.sizes[record.Path] = record;
    writeLibraryCache(snapshot);
  }, 1500);
});

window.addEventListener('DOMContentLoaded', () => {
  if (!host) return;
  readLibraryCache().then(cached => {
    if (!cached || state.loaded || !Array.isArray(cached.games)) return;
    handleMessage(Object.assign({}, cached, { type: 'library', cached: true, scanning: false, syncing: false }));
  });
  if (!settingsUi.online) send({ type: 'checkOnline' });
});

extensions.messageHandlers.push(message => {
  switch (message.type) {
    case 'settings':
      diffSettings();
      applyTheme();
      break;

    case 'settingsInfo':
      settingsUi.info = message;
      settingsUi.infoRequested = false;
      refreshBuiltinSections();
      break;

    case 'pickedPath': {
      const resolve = settingsUi.picks.get(message.requestId);
      if (!resolve) break;
      settingsUi.picks.delete(message.requestId);
      resolve(message.path || null);
      break;
    }

    case 'syncInterval':
      settingsUi.syncBusy = false;
      if (message.ok) {
        if (settingsUi.info && settingsUi.info.sync) settingsUi.info.sync.interval = message.interval;
        const label = SYNC_INTERVALS.find(option => option.value === message.interval);
        toast(label ? `Library refresh: ${label.label.toLowerCase()}` : 'Sync schedule updated', 'info', ICON.check);
      } else {
        toast(`Couldn’t change the schedule: ${message.error || 'unknown error'}`, 'error');
      }
      refreshBuiltinSections(['general']);
      break;

    case 'themeBackground':
      if (WALLPAPER_URL.test(message.url || '')) {
        setSettingValue('theme.background', message.url);
        setSettingValue('theme.backgroundName', message.name || null);
        refreshBuiltinSections(['appearance']);
      }
      break;

    case 'importLoaded':
      showImportSummary(message.summary || {}, message.local);
      break;

    case 'importApplied':
      if (message.ok) {
        restoreLocalData(settingsUi.pendingImport);
        settingsUi.pendingImport = null;
        applyTheme();
        renderAll();
        toast(message.message || 'Backup imported', 'info', ICON.check);
      } else {
        settingsUi.pendingImport = null;
        toast(`Import failed: ${message.message || 'unknown error'}`, 'error');
      }
      break;

    case 'installState':
      if (settingsUi.info) settingsUi.info.install = message.install;
      refreshBuiltinSections(['about']);
      break;

    case 'online':
      setOnline(!!message.online);
      break;

    case 'sync':
      if (!message.running) refreshBuiltinSections(['general']);
      break;
  }
});
