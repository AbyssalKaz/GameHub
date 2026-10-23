'use strict';

const host = window.chrome && window.chrome.webview;

const GB = 1024 ** 3;

const PLATFORMS = [
  { key: 'steam', label: 'Steam', sources: ['Steam'], launcher: 'Steam' },
  { key: 'epic', label: 'Epic Games', sources: ['Epic'], launcher: 'Epic Games' },
  { key: 'hydra', label: 'Hydra', sources: ['Hydra'], launcher: 'Hydra' },
  { key: 'riot', label: 'Riot Games', sources: ['Riot'], launcher: 'Riot Client' },
  { key: 'xbox', label: 'Xbox', sources: ['Xbox'], launcher: 'Xbox' },
  { key: 'hoyoplay', label: 'HoYoPlay', sources: ['HoYoPlay'], launcher: 'HoYoPlay' },
  { key: 'ea', label: 'EA', sources: ['EA'], launcher: 'EA app' },
  { key: 'rockstar', label: 'Rockstar Games', sources: ['Rockstar'], launcher: 'Rockstar Launcher' },
  { key: 'local', label: 'Local', sources: ['Folder', 'Extra'] },
  { key: 'downloads', label: 'Downloads', sources: ['Downloads'] },
];

const SORTS = [
  { key: 'name', label: 'Name' },
  { key: 'size', label: 'Size on disk' },
  { key: 'recent', label: 'Recently played' },
  { key: 'playtime', label: 'Most played' },
  { key: 'installed', label: 'Recently installed' },
  { key: 'platform', label: 'Platform' },
];

const VIEWS = {
  all: 'All games',
  recent: 'Recently played',
  favorites: 'Favorites',
  leftovers: 'Leftover files',
  free: 'Free games',
};

const SIZE_BUCKETS = [
  { key: 'any', label: 'Any size', test: () => true },
  { key: 'small', label: 'Under 1 GB', test: bytes => bytes < GB },
  { key: 'medium', label: '1–20 GB', test: bytes => bytes >= GB && bytes < 20 * GB },
  { key: 'large', label: '20–60 GB', test: bytes => bytes >= 20 * GB && bytes < 60 * GB },
  { key: 'huge', label: '60 GB+', test: bytes => bytes >= 60 * GB },
];

const DEMO_PATTERN = /\b(demo|playtest|open beta|beta|free trial|prologue|test server)\b/i;
const PART_COLORS = ['#7b8cff', '#a46bff', '#5fd39a', '#f2b25c', '#5aa9e6', '#ff8fa3'];

const extensions = {
  views: {},
  sidebarSections: [],
  heroButtons: [],
  drawerSections: [],
  drawerActions: {},
  menuItems: [],
  menuActions: {},
  messageHandlers: [],
  sidebarTop: [],
  afterRender: [],
  cardBadges: [],
  filters: [],
  filterChips: [],
  sortComparers: {},
  groupers: {},
  drawerStats: [],
  keyHandlers: [],
  libraryHandlers: [],
  settingsSections: [],
  launchGuards: [],
  listColumns: [],
  cardMounted: [],
  sidebarFooter: [],
};

const ICON = {
  gamepad: '<svg viewBox="0 0 24 24" class="glyph"><path d="M6 11h4M8 9v4M15 12h.01M18 10h.01"/><path d="M17.3 5H6.7a4 4 0 0 0-3.96 3.43l-.9 6.3A3 3 0 0 0 4.8 18.2c.86 0 1.68-.37 2.25-1.02L9 15h6l1.95 2.18a3 3 0 0 0 5.21-2.48l-.9-6.27A4 4 0 0 0 17.3 5z"/></svg>',
  clock: '<svg viewBox="0 0 24 24" class="glyph"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></svg>',
  star: '<svg viewBox="0 0 24 24" class="glyph"><path d="m12 3.5 2.6 5.3 5.9.9-4.25 4.15 1 5.85L12 16.95 6.75 19.7l1-5.85L3.5 9.7l5.9-.9z"/></svg>',
  starFilled: '<svg viewBox="0 0 24 24" class="glyph filled"><path d="m12 3.5 2.6 5.3 5.9.9-4.25 4.15 1 5.85L12 16.95 6.75 19.7l1-5.85L3.5 9.7l5.9-.9z"/></svg>',
  box: '<svg viewBox="0 0 24 24" class="glyph"><rect x="3" y="4" width="18" height="5" rx="1.2"/><path d="M5 9v9a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V9M10 13h4"/></svg>',
  folder: '<svg viewBox="0 0 24 24" class="glyph"><path d="M3.5 7.5a2 2 0 0 1 2-2h3.6l2 2h7.4a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2z"/></svg>',
  download: '<svg viewBox="0 0 24 24" class="glyph"><path d="M12 4v11m0 0-4.5-4.5M12 15l4.5-4.5M4.5 16.5v1.5a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2v-1.5"/></svg>',
  play: '<svg viewBox="0 0 24 24" class="glyph filled"><path d="M8 5.6v12.8a1 1 0 0 0 1.52.85l10.2-6.4a1 1 0 0 0 0-1.7L9.52 4.75A1 1 0 0 0 8 5.6z"/></svg>',
  external: '<svg viewBox="0 0 24 24" class="glyph"><path d="M14 4h6v6M20 4l-8.5 8.5M18.5 14v4a2 2 0 0 1-2 2h-10a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4"/></svg>',
  close: '<svg viewBox="0 0 24 24" class="glyph"><path d="M6 6l12 12M18 6 6 18"/></svg>',
  refresh: '<svg viewBox="0 0 24 24" class="glyph"><path d="M20 12a8 8 0 1 1-2.34-5.66"/><path d="M20 4v5h-5"/></svg>',
  ruler: '<svg viewBox="0 0 24 24" class="glyph"><path d="M4 16.5 16.5 4l3.5 3.5L7.5 20z"/><path d="m8 13 2 2M11 10l2 2M14 7l2 2"/></svg>',
  check: '<svg viewBox="0 0 24 24" class="glyph check"><path d="m5 12.5 4.5 4.5L19 7.5"/></svg>',
  eyeOff: '<svg viewBox="0 0 24 24" class="glyph"><path d="M3 3l18 18M10.6 6.1A9.8 9.8 0 0 1 12 6c5 0 8.5 4.5 9.5 6-.45.7-1.4 2-2.8 3.2M6.5 7.6C4.6 8.9 3.2 10.8 2.5 12c1 1.5 4.5 6 9.5 6 1.7 0 3.2-.5 4.5-1.3"/><path d="M9.9 10a3 3 0 0 0 4.1 4.1"/></svg>',
  info: '<svg viewBox="0 0 24 24" class="glyph"><circle cx="12" cy="12" r="9"/><path d="M12 11v5M12 8h.01"/></svg>',
  alert: '<svg viewBox="0 0 24 24" class="glyph"><path d="M12 4 2.8 19.5h18.4z"/><path d="M12 10v4.5M12 17h.01"/></svg>',
  drive: '<svg viewBox="0 0 24 24" class="glyph"><rect x="3" y="13" width="18" height="7" rx="2"/><path d="M5 13 7.5 5h9L19 13M7 16.5h.01M10.5 16.5h.01"/></svg>',
  search: '<svg viewBox="0 0 24 24" class="glyph"><circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/></svg>',
  arrowDown: '<svg viewBox="0 0 24 24" class="glyph"><path d="M12 5v14M6 13l6 6 6-6"/></svg>',
  arrowUp: '<svg viewBox="0 0 24 24" class="glyph"><path d="M12 19V5M6 11l6-6 6 6"/></svg>',
  gear: '<svg viewBox="0 0 24 24" class="glyph"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>',
  columns: '<svg viewBox="0 0 24 24" class="glyph"><rect x="3.5" y="4.5" width="17" height="15" rx="2.5"/><path d="M9.5 4.5v15M14.5 4.5v15"/></svg>',
  brand: '<svg viewBox="0 0 24 24"><rect x="3.5" y="3.5" width="7" height="7" rx="2" fill="#fff"/><rect x="13.5" y="3.5" width="7" height="7" rx="2" fill="#fff" opacity=".55"/><rect x="3.5" y="13.5" width="7" height="7" rx="2" fill="#fff" opacity=".55"/><path d="M14.6 13.9v6.2a.6.6 0 0 0 .9.5l4.9-3.1a.6.6 0 0 0 0-1l-4.9-3.1a.6.6 0 0 0-.9.5z" fill="#fff"/></svg>',
};

const $ = selector => document.querySelector(selector);

const elements = {
  sidebar: $('#sidebar'),
  search: $('#search'),
  searchClear: $('#searchClear'),
  sortDropdown: $('#sortDropdown'),
  sortButton: $('#sortButton'),
  sortLabel: $('#sortLabel'),
  sortMenu: $('#sortMenu'),
  viewToggle: $('#viewToggle'),
  zoomControl: $('#zoomControl'),
  cardSize: $('#cardSize'),
  pageTitle: $('#pageTitle'),
  summary: $('#summary'),
  filters: $('#filters'),
  scroller: $('#scroller'),
  results: $('#results'),
  backdrop: $('#backdrop'),
  drawer: $('#drawer'),
  contextMenu: $('#contextMenu'),
  toasts: $('#toasts'),
};

const state = {
  loaded: false,
  games: [],
  byId: new Map(),
  platformIcons: {},
  drives: [],
  sizes: {},
  generatedAt: null,
  scan: { running: false, done: 0, total: 0 },
  syncing: false,
  view: 'all',
  platform: null,
  drive: null,
  search: '',
  sizeBucket: 'any',
  selectedId: null,
  visible: [],
  failedCovers: new Set(),
  prefs: Object.assign({ layout: 'grid', sort: 'name', cardSize: 190, hideDemos: false, sortDescending: null }, readStorage('hub.prefs', {})),
  favorites: new Set(readStorage('hub.favorites', [])),
  userData: {},
  settings: {},
  meta: {},
  running: {},
  extras: {},
};

if (state.prefs.startupView === 'home' || state.prefs.startupView === 'library') state.prefs.lastMode = state.prefs.startupView;

function readStorage(key, fallback) {
  try {
    const raw = localStorage.getItem(key);
    return raw ? JSON.parse(raw) : fallback;
  } catch {
    return fallback;
  }
}

function writeStorage(key, value) {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch {
  }
}

function savePrefs() {
  writeStorage('hub.prefs', state.prefs);
}

function send(message) {
  if (host) host.postMessage(message);
}

function gameData(id) {
  return state.userData[id] || {};
}

function setGameData(id, patch) {
  const next = Object.assign({}, state.userData[id] || {});
  for (const [key, value] of Object.entries(patch)) {
    if (value == null) delete next[key];
    else next[key] = value;
  }
  state.userData[id] = next;
  send({ type: 'setGameData', id, patch });
}

function setting(key, fallback) {
  return Object.prototype.hasOwnProperty.call(state.settings, key) ? state.settings[key] : fallback;
}

function setSetting(key, value) {
  if (value == null) delete state.settings[key];
  else state.settings[key] = value;
  send({ type: 'setSetting', key, value });
}

function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[character]));
}

function formatBytes(bytes) {
  if (bytes == null) return '—';
  if (bytes < 1024) return `${bytes} B`;
  const units = ['KB', 'MB', 'GB', 'TB'];
  let value = bytes / 1024;
  let unit = 0;
  while (value >= 1000 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  return `${value >= 100 ? value.toFixed(0) : value.toFixed(1)} ${units[unit]}`;
}

function formatCount(count) {
  return count.toLocaleString('en-GB');
}

function relativeTime(iso) {
  if (!iso) return null;
  const seconds = (Date.now() - new Date(iso).getTime()) / 1000;
  if (seconds < 90) return 'Just now';
  const minutes = seconds / 60;
  if (minutes < 60) return `${Math.round(minutes)} min ago`;
  const hours = minutes / 60;
  if (hours < 24) return `${Math.round(hours)} h ago`;
  const days = hours / 24;
  if (days < 2) return 'Yesterday';
  if (days < 7) return `${Math.floor(days)} days ago`;
  if (days < 30) return `${Math.floor(days / 7)} wk ago`;
  if (days < 365) return `${Math.floor(days / 30.4)} mo ago`;
  const years = Math.floor(days / 365);
  return `${years} yr${years > 1 ? 's' : ''} ago`;
}

function formatDate(iso) {
  if (!iso) return null;
  return new Date(iso).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
}

function formatPlaytime(minutes) {
  if (!minutes) return null;
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (hours >= 100 || !rest) return `${formatCount(hours)} h`;
  return `${hours} h ${rest} m`;
}

function hueOf(text) {
  let hash = 0;
  for (const character of text) hash = (hash * 31 + character.charCodeAt(0)) | 0;
  return Math.abs(hash) % 360;
}

function normalize(text) {
  return String(text ?? '')
    .normalize('NFD')
    .replace(/[\u0300-\u036f™®©]/g, '')
    .toLowerCase();
}

function pathKey(path) {
  return String(path).replace(/\\+$/, '').toLowerCase();
}

function platformOf(game) {
  return PLATFORMS.find(platform => platform.sources.includes(game.Source)) || PLATFORMS.find(platform => platform.key === 'local');
}

function platformIcon(key) {
  if (state.platformIcons[key]) return `<img src="${escapeHtml(state.platformIcons[key])}" alt="">`;
  if (key === 'downloads') return ICON.download;
  return ICON.folder;
}

function isFavorite(game) {
  return state.favorites.has(game.Id);
}

function isDemo(game) {
  return DEMO_PATTERN.test(game.Name);
}

function displayFolder(game) {
  const path = game.InstallPath || (game.Parts[0] && game.Parts[0].Path) || '';
  return path;
}

function locationLabel(path) {
  if (!path) return '';
  const drive = (driveOf(path) || '').replace('\\', '');
  const lower = path.toLowerCase();
  if (lower.includes('\\steamapps\\common\\')) return `${drive} Steam library`;
  if (lower.includes('\\epic games\\')) return `${drive} Epic Games`;
  const segments = path.split('\\').filter(Boolean).slice(1, -1);
  if (!segments.length) return drive;
  return `${drive} ${segments.length > 2 ? '…\\' : ''}${segments.slice(-2).join('\\')}`;
}

function sharingWith(game) {
  const keys = new Set(partsOf(game).map(part => pathKey(part.path)));
  return state.games.filter(other => other !== game && partsOf(other).some(part => keys.has(pathKey(part.path))));
}

function partsOf(game) {
  const parts = [];
  if (game.InstallPath) {
    parts.push({ label: game.Status === 'leftover' ? 'Leftover files' : 'Game files', path: game.InstallPath });
  }
  for (const part of game.Parts || []) parts.push({ label: part.Label, path: part.Path });
  return parts;
}

function sizeOf(game) {
  let bytes = 0;
  let files = 0;
  let pending = false;
  const parts = partsOf(game).map(part => {
    const record = state.sizes[pathKey(part.path)];
    if (!record) {
      pending = true;
      return { ...part, pending: true };
    }
    bytes += record.Bytes;
    files += record.Files;
    return { ...part, bytes: record.Bytes, files: record.Files, missing: record.Missing };
  });
  const visibleParts = parts.filter((part, index) => index === 0 || part.pending || part.missing || part.bytes > 0);
  return { bytes, files, pending: pending && bytes === 0, partial: pending, parts: visibleParts };
}

function sharedSize(games) {
  const seen = new Set();
  let bytes = 0;
  let pending = false;
  for (const game of games) {
    for (const part of partsOf(game)) {
      const key = pathKey(part.path);
      if (seen.has(key)) continue;
      seen.add(key);
      const record = state.sizes[key];
      if (record) bytes += record.Bytes;
      else pending = true;
    }
  }
  return { bytes, pending };
}

function driveOf(path) {
  const match = /^([a-z]):/i.exec(path || '');
  return match ? `${match[1].toUpperCase()}:\\` : null;
}

function gameDrives(game) {
  return new Set(partsOf(game).map(part => driveOf(part.path)).filter(Boolean));
}

function searchScore(game, query) {
  const name = normalize(game.Name);
  const words = name.split(/[^\p{L}\p{N}]+/u).filter(Boolean);
  const initials = words.map(word => word[0]).join('');
  const compactName = name.replace(/[^\p{L}\p{N}]/gu, '');
  const compactQuery = query.replace(/[^\p{L}\p{N}]/gu, '');

  if (name === query) return 1000;
  if (name.startsWith(query)) return 800;
  if (words.some(word => word.startsWith(query))) return 600;
  if (initials.startsWith(compactQuery) && compactQuery.length >= 2) return 500;
  if (compactName.includes(compactQuery) && compactQuery.length >= 2) return 400;

  const tokens = query.split(/\s+/).filter(Boolean);
  const haystack = `${name} ${normalize(platformOf(game).label)} ${normalize(displayFolder(game))}`;
  if (tokens.length && tokens.every(token => haystack.includes(token))) return 200;
  return fuzzyScore(words, compactName, query);
}

function baseList() {
  let list = state.games;
  if (state.view === 'leftovers') list = list.filter(game => game.Status !== 'installed' && game.Status !== 'downloading');
  else list = list.filter(game => game.Status === 'installed' || game.Status === 'downloading');

  if (state.view === 'recent') list = list.filter(game => game.LastPlayed);
  if (state.view === 'favorites') list = list.filter(isFavorite);
  return list;
}

function filteredGames() {
  let list = baseList();

  if (state.platform) {
    const platform = PLATFORMS.find(entry => entry.key === state.platform);
    list = list.filter(game => platform.sources.includes(game.Source));
  }
  if (state.drive) list = list.filter(game => gameDrives(game).has(state.drive));
  if (state.prefs.hideDemos) list = list.filter(game => !isDemo(game));
  for (const filter of extensions.filters) list = filter(list);
  const search = parseSearch(state.search);
  for (const token of search.filters) list = list.filter(token.test);
  if (state.sizeBucket !== 'any') {
    const bucket = SIZE_BUCKETS.find(entry => entry.key === state.sizeBucket);
    list = list.filter(game => {
      const size = sizeOf(game);
      return !size.pending && bucket.test(size.bytes);
    });
  }

  const query = normalize(search.text.trim());
  if (query) {
    const scored = list.map(game => ({ game, score: searchScore(game, query) })).filter(entry => entry.score > 0);
    scored.sort((first, second) => second.score - first.score || compareGames(first.game, second.game));
    return scored.map(entry => entry.game);
  }

  const sortKey = state.view === 'recent' ? 'recent' : state.prefs.sort;
  return [...list].sort((first, second) => compareGames(first, second, sortKey));
}

function compareGames(first, second, sortKey = state.prefs.sort) {
  const byName = first.Name.localeCompare(second.Name, undefined, { numeric: true, sensitivity: 'base' });
  const descending = (a, b) => (b ?? -Infinity) - (a ?? -Infinity);
  let result = 0;

  switch (sortKey) {
    case 'size': {
      const a = sizeOf(first);
      const b = sizeOf(second);
      result = descending(a.pending ? null : a.bytes, b.pending ? null : b.bytes);
      break;
    }
    case 'recent':
      result = descending(first.LastPlayed ? Date.parse(first.LastPlayed) : null, second.LastPlayed ? Date.parse(second.LastPlayed) : null);
      break;
    case 'playtime':
      result = descending(first.PlaytimeMinutes, second.PlaytimeMinutes);
      break;
    case 'installed':
      result = descending(first.InstalledAt ? Date.parse(first.InstalledAt) : null, second.InstalledAt ? Date.parse(second.InstalledAt) : null);
      break;
    case 'platform':
      result = PLATFORMS.indexOf(platformOf(first)) - PLATFORMS.indexOf(platformOf(second));
      break;
    default:
      if (extensions.sortComparers[sortKey]) result = extensions.sortComparers[sortKey](first, second);
  }
  return result || byName;
}

function renderSidebar() {
  const installed = state.games.filter(game => game.Status === 'installed' || game.Status === 'downloading');
  const leftovers = state.games.filter(game => game.Status !== 'installed' && game.Status !== 'downloading');
  const counts = {
    all: installed.length,
    recent: installed.filter(game => game.LastPlayed).length,
    favorites: installed.filter(isFavorite).length,
    leftovers: leftovers.length,
  };

  const navItem = (key, icon, label) => `
    <button class="nav-item ${state.view === key ? 'active' : ''}" data-view="${key}" title="${label} · ${counts[key]}">
      <span class="nav-icon">${icon}</span>
      <span class="nav-label">${label}</span>
      <span class="nav-count">${counts[key]}</span>
    </button>`;

  const platformItems = PLATFORMS.map(platform => {
    const games = installed.filter(game => platform.sources.includes(game.Source));
    if (!games.length) return '';
    const size = sharedSize(games);
    return `
      <button class="nav-item ${state.platform === platform.key ? 'active' : ''}" data-platform="${platform.key}" title="${platform.label} · ${games.length} games · ${formatBytes(size.bytes)} on disk">
        <span class="nav-icon">${platformIcon(platform.key)}</span>
        <span class="nav-label">${platform.label}</span>
        <span class="nav-count">${games.length}</span>
      </button>`;
  }).join('');

  const driveItems = state.drives.map(drive => {
    const onDrive = state.games.filter(game => gameDrives(game).has(drive.root));
    const games = sharedSize(onDrive);
    const used = drive.total - drive.free;
    const gamesPercent = Math.min(100, (games.bytes / drive.total) * 100);
    const otherPercent = Math.max(0, Math.min(100 - gamesPercent, ((used - games.bytes) / drive.total) * 100));
    return `
      <button class="drive ${state.drive === drive.root ? 'active' : ''}" data-drive="${escapeHtml(drive.root)}" title="${escapeHtml(drive.root.replace('\\', ''))} ${escapeHtml(drive.label || '')} · games use ${formatBytes(games.bytes)} · ${formatBytes(drive.free)} free of ${formatBytes(drive.total)}">
        <div class="drive-top">
          <span class="drive-letter">${escapeHtml(drive.root.replace('\\', ''))}</span>
          <span class="drive-label">${escapeHtml(drive.label || 'Local Disk')}</span>
          <span class="drive-games">${games.pending && !games.bytes ? '<span class="measuring"></span>' : formatBytes(games.bytes)}</span>
        </div>
        <div class="drive-bar"><i class="games" style="width:${gamesPercent}%"></i><i class="other" style="width:${otherPercent}%"></i></div>
        <div class="drive-foot"><span>${formatBytes(drive.free)} free</span><span>${formatBytes(drive.total)}</span></div>
      </button>`;
  }).join('');

  elements.sidebar.innerHTML = `
    <div class="brand" data-drag>
      <div class="brand-mark">${ICON.brand}</div>
      <div class="brand-name">Game Hub</div>
      <button class="sidebar-toggle" data-sidebar-toggle title="${state.prefs.sidebarCollapsed ? 'Expand' : 'Collapse'} sidebar (Ctrl+B)">
        <svg viewBox="0 0 24 24" class="glyph"><rect x="3.5" y="4.5" width="17" height="15" rx="2.5"/><path d="M9.5 4.5v15"/><path d="${state.prefs.sidebarCollapsed ? 'm13.5 10 2 2-2 2' : 'm15.5 10-2 2 2 2'}"/></svg>
      </button>
    </div>
    <div class="sidebar-scroll">
      ${extensions.sidebarTop.map(item => item()).join('')}
      <div class="nav-section">
        <div class="nav-heading">Library</div>
        ${navItem('all', ICON.gamepad, VIEWS.all)}
        ${navItem('recent', ICON.clock, VIEWS.recent)}
        ${navItem('favorites', ICON.star, VIEWS.favorites)}
        ${counts.leftovers ? navItem('leftovers', ICON.box, VIEWS.leftovers) : ''}
      </div>
      ${extensions.sidebarSections.map(section => section()).join('')}
      <div class="nav-section">
        <div class="nav-heading">Platforms</div>
        ${platformItems}
      </div>
      <div class="nav-section">
        <div class="nav-heading">Storage</div>
        ${driveItems}
      </div>
    </div>
    <div class="sidebar-footer" id="sidebarFooter">${footerHtml()}</div>`;
}

function footerHtml() {
  const lastScan = Object.values(state.sizes).reduce((latest, record) => (record.ScannedAt > latest ? record.ScannedAt : latest), '');
  const lower = text => (text ? text.charAt(0).toLowerCase() + text.slice(1) : 'never');
  const syncLine = state.syncing
    ? `<span class="grow">Scanning launchers…</span>`
    : `<span class="grow" title="Library last refreshed ${escapeHtml(formatDate(state.generatedAt) || '')}">Library · ${escapeHtml(lower(relativeTime(state.generatedAt)))}</span>`;
  const scanLine = state.scan.running
    ? `<span class="grow">Measuring ${state.scan.done}/${state.scan.total}</span>`
    : `<span class="grow" title="Sizes last measured ${escapeHtml(formatDate(lastScan) || '')}">Sizes · ${escapeHtml(lower(relativeTime(lastScan)))}</span>`;
  const progress = state.scan.running && state.scan.total
    ? `<div class="status-progress"><i style="width:${(state.scan.done / state.scan.total) * 100}%"></i></div>`
    : '';

  return `
    <div class="footer-row">
      <button class="nav-item footer-settings ${state.view === 'settings' ? 'active' : ''}" data-view="settings" title="Settings (Ctrl+,)">
        <span class="nav-icon">${ICON.gear}</span>
        <span class="nav-label">Settings</span>
      </button>
      ${extensions.sidebarFooter.map(item => item()).join('')}
    </div>
    <div class="status-line">
      ${syncLine}
      <button class="text-button ${state.syncing ? 'busy' : ''}" data-action="refresh" title="Rescan launchers and folders, and update Start Menu shortcuts">${ICON.refresh}<span class="button-text">Refresh</span></button>
    </div>
    <div class="status-line">
      ${scanLine}
      <button class="text-button ${state.scan.running ? 'busy' : ''}" data-action="rescan" title="Measure every game folder again">${state.scan.running ? ICON.refresh : ICON.ruler}<span class="button-text">Measure</span></button>
    </div>
    ${progress}`;
}

function renderFooter() {
  const footer = document.getElementById('sidebarFooter');
  if (footer) footer.innerHTML = footerHtml();
}

function renderHeadline(list) {
  const platform = PLATFORMS.find(entry => entry.key === state.platform);
  let title = VIEWS[state.view];
  if (state.view === 'all' && platform) title = platform.label;
  const searchText = parseSearch(state.search).text.trim();
  if (searchText) title = `Results for “${searchText}”`;
  elements.pageTitle.textContent = title;

  const size = sharedSize(list);
  const noun = state.view === 'leftovers' ? (list.length === 1 ? 'item' : 'items') : (list.length === 1 ? 'game' : 'games');
  const sizeText = size.pending && !size.bytes ? 'measuring sizes…' : `<strong>${formatBytes(size.bytes)}</strong> on disk${size.pending ? ' so far' : ''}`;
  const topMatch = state.search.trim() ? list.find(game => game.CanLaunch) : null;
  const hint = topMatch ? ` · <span class="hint">Enter to play <strong>${escapeHtml(topMatch.Name)}</strong></span>` : '';
  elements.summary.innerHTML = `<strong>${formatCount(list.length)}</strong> ${noun} · ${sizeText}${hint}`;

  const chips = [];
  if (platform) {
    chips.push(`<button class="chip removable active" data-clear="platform">${platformIcon(platform.key)}${platform.label}${ICON.close}</button>`);
  }
  if (state.drive) {
    chips.push(`<button class="chip removable active" data-clear="drive">${ICON.drive}${escapeHtml(state.drive.replace('\\', ''))} drive${ICON.close}</button>`);
  }
  if (state.view !== 'leftovers') {
    chips.push(`<button class="chip ${state.prefs.hideDemos ? 'active' : ''}" data-action="toggle-demos">${ICON.eyeOff}Hide demos &amp; playtests</button>`);
  }
  for (const chip of extensions.filterChips) chips.push(chip());
  chips.push(`<div class="chip-group">${SIZE_BUCKETS.map(bucket => `<button class="${state.sizeBucket === bucket.key ? 'active' : ''}" data-bucket="${bucket.key}">${bucket.label}</button>`).join('')}</div>`);
  elements.filters.innerHTML = chips.join('');
}

function coverHtml(game, className = 'cover-image') {
  if (game.CoverUrl && !state.failedCovers.has(game.Id)) {
    return `<img class="${className}" src="${escapeHtml(game.CoverUrl)}" data-cover-for="${escapeHtml(game.Id)}" loading="lazy" decoding="async" alt="">`;
  }

  const hue = hueOf(game.Name);
  const background = `radial-gradient(130% 85% at 50% 0%, hsl(${hue} 42% 30%) 0%, hsl(${(hue + 30) % 360} 38% 13%) 62%, #0b0d12 100%)`;
  if (game.HeaderUrl) {
    return `
      <div class="cover-fallback" style="background:${background}">
        <img class="backdrop-image" src="${escapeHtml(game.HeaderUrl)}" alt="">
        <img class="header-image" src="${escapeHtml(game.HeaderUrl)}" alt="">
        <div class="fallback-title">${escapeHtml(game.Name)}</div>
      </div>`;
  }

  const emblem = game.IconUrl
    ? `<img class="fallback-icon" src="${escapeHtml(game.IconUrl)}" alt="">`
    : `<div class="fallback-letter">${escapeHtml((game.Name.match(/[\p{L}\p{N}]/u) || ['?'])[0].toUpperCase())}</div>`;
  return `
    <div class="cover-fallback" style="background:${background}">
      ${emblem}
      <div class="fallback-title">${escapeHtml(game.Name)}</div>
    </div>`;
}

function statusLabel(game) {
  if (game.Status === 'downloading') return game.Download && game.Download.State === 'paused' ? 'Paused' : game.Download && game.Download.State === 'queued' ? 'Queued' : 'Downloading';
  return game.Status === 'installer' ? 'Installer only' : game.Status === 'leftover' ? 'Leftover' : '';
}

function secondaryMeta(game) {
  for (const hook of extensions.cardMeta || []) {
    const value = hook(game);
    if (value !== undefined) return value;
  }
  switch (state.prefs.sort) {
    case 'playtime':
      return formatPlaytime(game.PlaytimeMinutes);
    case 'installed':
      return game.InstalledAt ? `Installed ${relativeTime(game.InstalledAt)}` : null;
    case 'platform':
      return platformOf(game).label;
    default:
      return relativeTime(game.LastPlayed) || platformOf(game).label;
  }
}

function sizeMarkup(size) {
  if (size.pending) return '<span class="measuring"></span>';
  return formatBytes(size.bytes);
}

function cardHtml(game, index, maxBytes) {
  const platform = platformOf(game);
  const size = sizeOf(game);
  const percent = size.pending || !maxBytes ? 0 : Math.max(2, (size.bytes / maxBytes) * 100);
  const secondary = secondaryMeta(game);
  const flag = game.Status !== 'installed'
    ? `<span class="status-tag">${statusLabel(game)}</span>`
    : isFavorite(game) ? `<span class="favorite-mark">${ICON.starFilled}</span>` : '';

  return `
    <div class="card" tabindex="0" data-id="${escapeHtml(game.Id)}" style="animation-delay:${Math.min(index, 24) * 14}ms">
      <div class="cover">
        ${coverHtml(game)}
        <div class="cover-shade"></div>
        <span class="badge" title="${platform.label}">${platformIcon(platform.key)}</span>
        ${flag}
        ${extensions.cardBadges.map(badge => badge(game)).join('')}
        ${game.CanLaunch ? `<button class="play-button" data-action="play" title="Play">${ICON.play}</button>` : ''}
        <div class="size-meter"><i data-meter style="width:${percent}%"></i></div>
      </div>
      <div class="card-info">
        <div class="card-title" title="${escapeHtml(game.Name)}">${escapeHtml(game.Name)}</div>
        <div class="card-meta">
          <span class="size" data-size>${sizeMarkup(size)}</span>
          ${secondary ? `<span class="sep"></span><span>${escapeHtml(secondary)}</span>` : ''}
        </div>
      </div>
    </div>`;
}

function recentShelfHtml() {
  const recent = state.games
    .filter(game => game.Status === 'installed' && game.LastPlayed && (game.HeroUrl || game.HeaderUrl || game.CoverUrl))
    .sort((first, second) => Date.parse(second.LastPlayed) - Date.parse(first.LastPlayed))
    .slice(0, 4);
  if (recent.length < 2) return '';

  const cards = recent.map((game, index) => {
    const platform = platformOf(game);
    const image = game.HeroUrl || game.HeaderUrl || game.CoverUrl;
    const label = game.LogoUrl
      ? `<img class="recent-logo" src="${escapeHtml(game.LogoUrl)}" alt="${escapeHtml(game.Name)}">`
      : `<div class="recent-name">${escapeHtml(game.Name)}</div>`;
    const playtime = formatPlaytime(game.PlaytimeMinutes);
    return `
      <div class="recent-card" data-id="${escapeHtml(game.Id)}" style="animation-delay:${index * 40}ms">
        <img class="hero" src="${escapeHtml(image)}" alt="">
        <div class="recent-body">
          ${label}
          <div class="recent-meta">${platformIcon(platform.key)}<span>${escapeHtml(relativeTime(game.LastPlayed))}</span>${playtime ? `<span>·</span><span>${escapeHtml(playtime)}</span>` : ''}</div>
        </div>
        ${game.CanLaunch ? `<button class="recent-play" data-action="play">${ICON.play}Play</button>` : ''}
      </div>`;
  }).join('');

  return `
    <section class="shelf">
      <h2 class="shelf-title">Continue playing</h2>
      <div class="recent-row">${cards}</div>
    </section>`;
}

function listHeadHtml(columns) {
  const sortKey = state.view === 'recent' ? 'recent' : state.prefs.sort;
  const header = (key, label) => key
    ? `<button data-sort="${key}" class="${sortKey === key ? 'sorted' : ''}">${escapeHtml(label)}${sortKey === key ? (key === 'name' || key === 'platform' ? ICON.arrowUp : ICON.arrowDown) : ''}</button>`
    : `<span>${escapeHtml(label)}</span>`;
  return `
    <div class="list-row list-head">
      <div>${header('name', 'Name')}</div>
      ${columns.map(column => `<div class="list-head-${escapeHtml(column.key)}">${header(column.sortKey, column.label)}</div>`).join('')}
      <button class="list-columns-button" data-list-columns title="Choose columns">${ICON.columns}</button>
    </div>`;
}

function listRowHtml(game, columns, maxBytes) {
  const thumb = game.IconUrl
    ? `<img class="list-thumb icon" src="${escapeHtml(game.IconUrl)}" alt="">`
    : game.HeaderUrl || game.CoverUrl
      ? `<img class="list-thumb" src="${escapeHtml(game.HeaderUrl || game.CoverUrl)}" alt="">`
      : `<div class="list-thumb"></div>`;
  const folder = displayFolder(game);
  const tag = game.Status !== 'installed' ? `<span class="mini-tag">${statusLabel(game)}</span>` : '';
  return `
    <div class="list-row" data-id="${escapeHtml(game.Id)}" tabindex="0">
      <div class="list-name">
        ${thumb}
        <div style="min-width:0">
          <div class="list-title">${escapeHtml(game.Name)}${tag}</div>
          <div class="list-sub">${escapeHtml(folder.split('\\').pop() || '')}</div>
        </div>
      </div>
      ${columns.map(column => column.render(game, { maxBytes })).join('')}
    </div>`;
}

function listHtml(list, maxBytes, virtualBody = false) {
  const columns = activeListColumns();
  return `
    <div class="list" style="--list-columns:${escapeHtml(listColumnTemplate(columns))}">
      ${listHeadHtml(columns)}
      ${virtualBody ? '<div class="list-body" data-virtual-list></div>' : list.map(game => listRowHtml(game, columns, maxBytes)).join('')}
    </div>`;
}

function emptyHtml() {
  if (!state.loaded) {
    return `<div class="empty">${ICON.gamepad}<strong>Loading your library…</strong></div>`;
  }
  const filtered = state.search || state.platform || state.drive || state.sizeBucket !== 'any' || state.prefs.hideDemos;
  return `
    <div class="empty">
      ${filtered ? ICON.search : ICON.gamepad}
      <strong>${filtered ? 'No games match' : state.view === 'favorites' ? 'No favorites yet' : 'Nothing here yet'}</strong>
      <span>${filtered ? 'Try a different search or clear the filters.' : state.view === 'favorites' ? 'Star a game from its details panel or right-click menu.' : ''}</span>
      ${filtered ? '<button class="text-button" data-action="clear-filters">Clear filters</button>' : ''}
    </div>`;
}

function renderResults() {
  virtualResults.reset();
  const customView = extensions.views[state.view];
  if (customView) {
    customView.render();
    return;
  }

  const list = filteredGames();
  state.visible = list;
  renderHeadline(list);

  const maxBytes = Math.max(0, ...list.map(game => sizeOf(game).bytes));
  const layout = state.prefs.layout;
  elements.zoomControl.classList.toggle('hidden', layout !== 'grid');
  document.documentElement.style.setProperty('--card-width', `${state.prefs.cardSize}px`);

  if (!list.length) {
    elements.results.innerHTML = emptyHtml();
    return;
  }

  if (layout === 'list') {
    const virtual = list.length > VIRTUAL_THRESHOLD;
    elements.results.innerHTML = listHtml(list, maxBytes, virtual);
    if (virtual) virtualResults.mount(elements.results.querySelector('[data-virtual-list]'), list, maxBytes, 'list');
    else announceCards(elements.results);
    return;
  }

  const showShelf = state.view === 'all' && !state.search.trim() && !state.platform && !state.drive && state.sizeBucket === 'any';
  const shelf = showShelf ? recentShelfHtml() : '';
  const grouper = extensions.groupers[state.prefs.groupBy];
  if (grouper) {
    const groups = new Map();
    for (const game of list) {
      const group = grouper.groupOf(game);
      if (!groups.has(group.key)) groups.set(group.key, { ...group, games: [] });
      groups.get(group.key).games.push(game);
    }
    const ordered = [...groups.values()].sort(grouper.compare || ((first, second) => String(first.label).localeCompare(String(second.label))));
    let index = 0;
    elements.results.innerHTML = ordered.map(group => `
      <section class="group">
        <h2 class="shelf-title">${group.label}<span class="count">${formatCount(group.games.length)}</span>${group.note ? `<span class="group-note">${group.note}</span>` : ''}</h2>
        <div class="grid">${group.games.map(game => cardHtml(game, index++, maxBytes)).join('')}</div>
      </section>`).join('');
    announceCards(elements.results);
    return;
  }
  const virtual = list.length > VIRTUAL_THRESHOLD;
  const grid = virtual
    ? '<div class="grid" data-virtual-grid></div>'
    : `<div class="grid">${list.map((game, index) => cardHtml(game, index, maxBytes)).join('')}</div>`;
  elements.results.innerHTML = shelf
    ? `${shelf}<h2 class="shelf-title">Library <span class="count">${formatCount(list.length)}</span></h2>${grid}`
    : grid;
  if (virtual) virtualResults.mount(elements.results.querySelector('[data-virtual-grid]'), list, maxBytes, 'grid');
  else announceCards(elements.results);
}

function renderControls() {
  const sort = SORTS.find(entry => entry.key === state.prefs.sort) || SORTS[0];
  elements.sortLabel.textContent = sort.label;
  elements.sortMenu.innerHTML = SORTS.map(entry => `
    <button class="menu-item ${entry.key === state.prefs.sort ? 'selected' : ''}" data-sort="${entry.key}">${ICON.check}${entry.label}</button>`).join('');
  for (const button of elements.viewToggle.querySelectorAll('button')) {
    button.classList.toggle('active', button.dataset.view === state.prefs.layout);
  }
  elements.cardSize.value = state.prefs.cardSize;
  elements.searchClear.hidden = !state.search;
}

function renderAll() {
  renderSidebar();
  renderControls();
  renderResults();
  if (state.selectedId) renderDrawer();
  for (const hook of extensions.afterRender) hook();
}

let sizeTimer = null;
let renderTimer = null;

function scheduleSizeUpdate() {
  if (sizeTimer) return;
  sizeTimer = setTimeout(() => {
    sizeTimer = null;
    const needsReorder = state.prefs.sort === 'size' || state.sizeBucket !== 'any';
    if (needsReorder) {
      scheduleRender();
    } else {
      updateSizesInPlace();
    }
    renderSidebar();
    if (state.selectedId) renderDrawer();
  }, 250);
}

function updateSizesInPlace() {
  const maxBytes = Math.max(0, ...state.visible.map(game => sizeOf(game).bytes));
  virtualResults.maxBytes = maxBytes;
  for (const element of elements.results.querySelectorAll('[data-id]')) {
    const game = state.byId.get(element.dataset.id);
    if (!game) continue;
    const size = sizeOf(game);
    const label = element.querySelector('[data-size]');
    const meter = element.querySelector('[data-meter]');
    if (label) label.innerHTML = sizeMarkup(size);
    if (meter) meter.style.width = `${size.pending || !maxBytes ? 0 : Math.max(2, (size.bytes / maxBytes) * 100)}%`;
  }
  renderHeadline(state.visible);
}

function scheduleRender(delay = 300) {
  clearTimeout(renderTimer);
  renderTimer = setTimeout(() => {
    const scroll = elements.scroller.scrollTop;
    renderAll();
    elements.scroller.scrollTop = scroll;
  }, delay);
}

function openDrawer(id) {
  state.selectedId = id;
  renderDrawer();
  elements.drawer.classList.add('open');
  elements.drawer.setAttribute('aria-hidden', 'false');
  elements.backdrop.classList.add('open');
  elements.drawer.scrollTop = 0;
}

function closeDrawer() {
  state.selectedId = null;
  elements.drawer.classList.remove('open');
  elements.drawer.setAttribute('aria-hidden', 'true');
  elements.backdrop.classList.remove('open');
}

function renderDrawer() {
  const game = state.byId.get(state.selectedId);
  if (!game) {
    closeDrawer();
    return;
  }

  const platform = platformOf(game);
  const size = sizeOf(game);
  const heroImage = game.HeroUrl || game.HeaderUrl || game.CoverUrl;
  const heroBlurred = !game.HeroUrl && heroImage;
  const hue = hueOf(game.Name);
  const heroBackground = `background: radial-gradient(120% 120% at 20% 0%, hsl(${hue} 45% 28%), #0d1016 70%)`;

  const heroLabel = game.LogoUrl
    ? `<img class="hero-logo" src="${escapeHtml(game.LogoUrl)}" alt="${escapeHtml(game.Name)}">`
    : `<h2 class="hero-title">${escapeHtml(game.Name)}</h2>`;

  const folder = displayFolder(game);
  const launcherButton = platform.launcher && game.Status === 'installed'
    ? `<button class="secondary-button" data-action="open-launcher">${platformIcon(platform.key)}${escapeHtml(platform.launcher)}</button>`
    : '';
  const favorite = isFavorite(game);

  const parts = size.parts;
  const totalKnown = parts.reduce((sum, part) => sum + (part.bytes || 0), 0);
  const usageBar = parts.map((part, index) => part.bytes
    ? `<i style="width:${(part.bytes / totalKnown) * 100}%;background:${PART_COLORS[index % PART_COLORS.length]}" title="${escapeHtml(part.label)}"></i>`
    : '').join('');
  const usageRows = parts.map((part, index) => `
    <div class="usage-row">
      <span class="usage-swatch" style="background:${PART_COLORS[index % PART_COLORS.length]}"></span>
      <div style="min-width:0">
        <div class="usage-label">${escapeHtml(part.label)}</div>
        <div class="usage-path" title="${escapeHtml(part.path)}">${escapeHtml(part.path)}</div>
      </div>
      <span class="usage-size">${part.pending ? '<span class="measuring"></span>' : part.missing ? 'Missing' : formatBytes(part.bytes)}</span>
      <button class="icon-button" data-action="open-folder" data-path="${escapeHtml(part.path)}" title="Open folder">${ICON.folder}</button>
    </div>`).join('');

  let comparison = '';
  if (game.ReportedBytes && !size.partial && size.bytes) {
    const difference = size.bytes - game.ReportedBytes;
    const ratio = difference / game.ReportedBytes;
    const source = game.Source === 'Hydra' ? 'Hydra' : platform.launcher || platform.label;
    if (Math.abs(ratio) >= 0.1 && Math.abs(difference) >= 200 * 1024 ** 2) {
      comparison = `
        <div class="callout">${ICON.alert}<div>${escapeHtml(source)} reports <strong>${formatBytes(game.ReportedBytes)}</strong>, but this game actually takes <strong>${formatBytes(size.bytes)}</strong> on disk (${difference > 0 ? '+' : '−'}${formatBytes(Math.abs(difference))}).</div></div>`;
    } else {
      comparison = `
        <div class="callout neutral">${ICON.info}<div>${escapeHtml(source)} reports ${formatBytes(game.ReportedBytes)}, which matches what's on disk.</div></div>`;
    }
  }

  const sharing = sharingWith(game);
  const sharedNote = sharing.length
    ? `<div class="callout neutral">${ICON.info}<div>Shares its folder with <strong>${sharing.map(other => escapeHtml(other.Name)).join(', ')}</strong>. Totals count that folder once.</div></div>`
    : '';

  let statusCallout = '';
  if (game.Status === 'installer') {
    statusCallout = `<div class="callout">${ICON.alert}<div><strong>Not installed.</strong> Only the downloaded installer for this game is on disk.</div></div>`;
  } else if (game.Status === 'leftover') {
    statusCallout = `<div class="callout">${ICON.alert}<div><strong>No playable game found.</strong> This folder is in your game library but has no game executable, so it's probably an unfinished download or extraction.</div></div>`;
  }

  const launchTarget = game.Url || (game.Exe && game.Arguments ? `"${game.Exe}" ${game.Arguments}` : game.Exe);
  elements.drawer.innerHTML = `
    <div class="drawer-hero" style="${heroImage ? '' : heroBackground}">
      ${heroImage ? `<img class="hero ${heroBlurred ? 'blurred' : ''}" src="${escapeHtml(heroImage)}" alt="">` : ''}
      ${heroLabel}
      <button class="drawer-close" data-action="close-drawer" title="Close">${ICON.close}</button>
      ${extensions.heroButtons.map(button => button(game)).join('')}
    </div>
    <div class="drawer-body">
      <div class="drawer-heading">
        <h2>${game.LogoUrl ? escapeHtml(game.Name) : ''}</h2>
        <span class="platform-pill">${platformIcon(platform.key)}${platform.label}</span>
      </div>

      <div class="actions">
        <button class="primary-button" data-action="play" ${game.CanLaunch ? '' : 'disabled'}>${ICON.play}${game.CanLaunch ? 'Play' : 'Not playable'}</button>
        ${folder ? `<button class="secondary-button" data-action="open-folder" data-path="${escapeHtml(folder)}">${ICON.folder}Open folder</button>` : ''}
        ${launcherButton}
        ${game.Status === 'installed' ? `<button class="secondary-button square ${favorite ? 'on' : ''}" data-action="favorite" title="${favorite ? 'Remove from favorites' : 'Add to favorites'}">${favorite ? ICON.starFilled : ICON.star}</button>` : ''}
      </div>

      ${statusCallout}

      <div class="stats">
        <div class="stat"><div class="stat-label">Size on disk</div><div class="stat-value">${size.pending ? '<span class="measuring"></span>' : formatBytes(size.bytes)}</div></div>
        <div class="stat"><div class="stat-label">Last played</div><div class="stat-value ${game.LastPlayed ? '' : 'muted'}">${escapeHtml(relativeTime(game.LastPlayed) || 'Never')}</div></div>
        <div class="stat"><div class="stat-label">Playtime</div><div class="stat-value ${game.PlaytimeMinutes ? '' : 'muted'}">${escapeHtml(formatPlaytime(game.PlaytimeMinutes) || 'Unknown')}</div></div>
        <div class="stat"><div class="stat-label">Installed</div><div class="stat-value ${game.InstalledAt ? '' : 'muted'}">${escapeHtml(formatDate(game.InstalledAt) || 'Unknown')}</div></div>
        ${extensions.drawerStats.map(stat => stat(game)).join('')}
      </div>

      <h3 class="section-title">Disk usage</h3>
      <div class="usage">
        <div class="usage-total">
          <span class="value">${size.pending ? '<span class="measuring"></span>' : formatBytes(size.bytes)}</span>
          <span class="files">${size.files ? `${formatCount(size.files)} files` : ''}${size.partial && !size.pending ? ' · still measuring' : ''}</span>
        </div>
        ${totalKnown ? `<div class="usage-bar">${usageBar}</div>` : ''}
        ${usageRows}
      </div>

      ${comparison}
      ${sharedNote}

      <dl class="details-list">
        <dt>Platform</dt><dd>${escapeHtml(platform.label)}${game.Source === 'Extra' ? ' (added in config)' : ''}</dd>
        ${launchTarget ? `<dt>Launches</dt><dd class="mono">${escapeHtml(launchTarget)}</dd>` : ''}
        ${game.SteamAppId ? `<dt>Steam app ID</dt><dd>${escapeHtml(game.SteamAppId)}</dd>` : ''}
      </dl>
      ${extensions.drawerSections.map(section => section(game)).join('')}
    </div>`;
}

function launch(game, origin = 'hub') {
  if (!game || !game.CanLaunch) return;
  if (extensions.launchGuards.some(guard => guard(game) === false)) return;
  send({ type: 'launch', id: game.Id, origin });
  toast(`Launching ${game.Name}…`, 'info', ICON.play);
}

function toggleFavorite(game) {
  if (state.favorites.has(game.Id)) state.favorites.delete(game.Id);
  else state.favorites.add(game.Id);
  writeStorage('hub.favorites', [...state.favorites]);
  renderAll();
}

function setView(view) {
  const changed = state.view !== view;
  state.view = view;
  elements.scroller.scrollTop = 0;
  renderAll();
  if (changed) playViewTransition();
}

function toast(message, kind = 'info', icon = ICON.info) {
  const element = document.createElement('div');
  element.className = `toast ${kind}`;
  element.innerHTML = `${kind === 'error' ? ICON.alert : icon}<span>${escapeHtml(message)}</span>`;
  elements.toasts.appendChild(element);
  setTimeout(() => {
    element.classList.add('leaving');
    setTimeout(() => element.remove(), 260);
  }, 3200);
}

function openContextMenu(game, x, y) {
  const platform = platformOf(game);
  const folder = displayFolder(game);
  const favorite = isFavorite(game);
  elements.contextMenu.innerHTML = `
    <button class="menu-item" data-menu="play" ${game.CanLaunch ? '' : 'disabled'}>${ICON.play}Play</button>
    <button class="menu-item" data-menu="details">${ICON.info}Details</button>
    <div class="menu-separator"></div>
    <button class="menu-item" data-menu="folder" ${folder ? '' : 'disabled'}>${ICON.folder}Open folder</button>
    ${platform.launcher && game.Status === 'installed' ? `<button class="menu-item" data-menu="launcher">${ICON.external}Open in ${escapeHtml(platform.launcher)}</button>` : ''}
    ${game.Status === 'installed' ? `<button class="menu-item" data-menu="favorite">${favorite ? ICON.starFilled : ICON.star}${favorite ? 'Remove from favorites' : 'Add to favorites'}</button>` : ''}`;
  elements.contextMenu.insertAdjacentHTML('beforeend', extensions.menuItems.map(item => item(game)).join(''));
  elements.contextMenu.dataset.id = game.Id;

  const menu = elements.contextMenu;
  menu.style.left = '0px';
  menu.style.top = '0px';
  menu.classList.add('open');
  const bounds = menu.getBoundingClientRect();
  menu.style.left = `${Math.min(x, window.innerWidth - bounds.width - 8)}px`;
  menu.style.top = `${Math.min(y, window.innerHeight - bounds.height - 8)}px`;
}

function closeMenus() {
  elements.contextMenu.classList.remove('open');
  elements.sortDropdown.classList.remove('open');
}

elements.sidebar.addEventListener('click', event => {
  const view = event.target.closest('[data-view]');
  const platform = event.target.closest('[data-platform]');
  const drive = event.target.closest('[data-drive]');
  const action = event.target.closest('[data-action]');

  if (view) {
    setView(view.dataset.view);
  } else if (platform) {
    state.platform = state.platform === platform.dataset.platform ? null : platform.dataset.platform;
    elements.scroller.scrollTop = 0;
    renderAll();
  } else if (drive) {
    state.drive = state.drive === drive.dataset.drive ? null : drive.dataset.drive;
    elements.scroller.scrollTop = 0;
    renderAll();
  } else if (action && action.dataset.action === 'refresh') {
    if (!state.syncing) {
      state.syncing = true;
      renderFooter();
      send({ type: 'refresh' });
    }
  } else if (action && action.dataset.action === 'rescan') {
    if (!state.scan.running) {
      state.scan = { running: true, done: 0, total: 0 };
      renderFooter();
      send({ type: 'rescan' });
    }
  }
});

elements.filters.addEventListener('click', event => {
  const bucket = event.target.closest('[data-bucket]');
  const clear = event.target.closest('[data-clear]');
  const action = event.target.closest('[data-action]');

  if (bucket) state.sizeBucket = bucket.dataset.bucket;
  else if (clear) state[clear.dataset.clear] = null;
  else if (action && action.dataset.action === 'toggle-demos') {
    state.prefs.hideDemos = !state.prefs.hideDemos;
    savePrefs();
  } else return;
  renderAll();
});

elements.results.addEventListener('click', event => {
  const sortHeader = event.target.closest('[data-sort]');
  if (sortHeader) {
    state.prefs.sort = sortHeader.dataset.sort;
    savePrefs();
    renderAll();
    return;
  }

  const action = event.target.closest('[data-action]');
  if (action && action.dataset.action === 'clear-filters') {
    Object.assign(state, { search: '', platform: null, drive: null, sizeBucket: 'any' });
    state.prefs.hideDemos = false;
    savePrefs();
    elements.search.value = '';
    renderAll();
    return;
  }

  const item = event.target.closest('[data-id]');
  if (!item) return;
  const game = state.byId.get(item.dataset.id);
  if (action && action.dataset.action === 'play') {
    event.stopPropagation();
    launch(game);
    return;
  }
  openDrawer(game.Id);
});

elements.results.addEventListener('dblclick', event => {
  const item = event.target.closest('[data-id]');
  if (!item || event.target.closest('[data-action]')) return;
  const game = state.byId.get(item.dataset.id);
  if (game && game.CanLaunch) {
    closeDrawer();
    launch(game);
  }
});

elements.results.addEventListener('contextmenu', event => {
  const item = event.target.closest('[data-id]');
  if (!item) return;
  event.preventDefault();
  openContextMenu(state.byId.get(item.dataset.id), event.clientX, event.clientY);
});

elements.results.addEventListener('keydown', event => {
  if (event.key !== 'Enter') return;
  const item = event.target.closest('[data-id]');
  if (item) openDrawer(item.dataset.id);
});

elements.contextMenu.addEventListener('click', event => {
  const item = event.target.closest('[data-menu]');
  if (!item) return;
  const game = state.byId.get(elements.contextMenu.dataset.id);
  closeMenus();
  if (!game) return;

  switch (item.dataset.menu) {
    case 'play': launch(game); break;
    case 'details': openDrawer(game.Id); break;
    case 'folder': send({ type: 'openFolder', path: displayFolder(game) }); break;
    case 'launcher': send({ type: 'openLauncher', id: game.Id }); break;
    case 'favorite': toggleFavorite(game); break;
    default:
      if (extensions.menuActions[item.dataset.menu]) extensions.menuActions[item.dataset.menu](game);
  }
});

elements.drawer.addEventListener('click', event => {
  const action = event.target.closest('[data-action]');
  if (!action) return;
  const game = state.byId.get(state.selectedId);
  if (!game) return;

  switch (action.dataset.action) {
    case 'close-drawer': closeDrawer(); break;
    case 'play': launch(game); break;
    case 'open-folder': send({ type: 'openFolder', path: action.dataset.path }); break;
    case 'open-launcher': send({ type: 'openLauncher', id: game.Id }); break;
    case 'favorite': toggleFavorite(game); break;
    default:
      if (extensions.drawerActions[action.dataset.action]) extensions.drawerActions[action.dataset.action](game, action);
  }
});

elements.backdrop.addEventListener('click', closeDrawer);

elements.sortButton.addEventListener('click', event => {
  event.stopPropagation();
  elements.contextMenu.classList.remove('open');
  elements.sortDropdown.classList.toggle('open');
});

elements.sortMenu.addEventListener('click', event => {
  const item = event.target.closest('[data-sort]');
  if (!item) return;
  state.prefs.sort = item.dataset.sort;
  savePrefs();
  closeMenus();
  renderAll();
});

elements.viewToggle.addEventListener('click', event => {
  const button = event.target.closest('[data-view]');
  if (!button) return;
  state.prefs.layout = button.dataset.view;
  savePrefs();
  renderAll();
});

elements.cardSize.addEventListener('input', () => {
  state.prefs.cardSize = Number(elements.cardSize.value);
  document.documentElement.style.setProperty('--card-width', `${state.prefs.cardSize}px`);
  savePrefs();
});

elements.search.addEventListener('input', () => {
  state.search = elements.search.value;
  elements.searchClear.hidden = !state.search;
  elements.scroller.scrollTop = 0;
  renderResults();
});

elements.searchClear.addEventListener('click', event => {
  event.preventDefault();
  elements.search.value = '';
  state.search = '';
  elements.searchClear.hidden = true;
  renderResults();
  elements.search.focus();
});

elements.search.addEventListener('keydown', event => {
  if (event.key === 'Enter') {
    const first = state.visible.find(game => game.CanLaunch);
    if (state.search.trim() && first) launch(first);
  } else if (event.key === 'ArrowDown') {
    event.preventDefault();
    const first = elements.results.querySelector('[data-id][tabindex]');
    if (first) first.focus();
  }
});

document.addEventListener('click', event => {
  if (!event.target.closest('.dropdown') && !event.target.closest('.context-menu')) closeMenus();
});

document.addEventListener('contextmenu', event => {
  if (!event.target.closest('input')) event.preventDefault();
});

document.addEventListener('keydown', event => {
  if (extensions.keyHandlers.some(handler => handler(event) === true)) return;
  const inInput = event.target.tagName === 'INPUT' || event.target.tagName === 'TEXTAREA' || event.target.isContentEditable;

  if ((event.ctrlKey && (event.key === 'k' || event.key === 'f')) || (event.key === '/' && !inInput)) {
    event.preventDefault();
    elements.search.focus();
    elements.search.select();
    return;
  }

  if (event.key === 'Escape') {
    if (elements.contextMenu.classList.contains('open') || elements.sortDropdown.classList.contains('open')) closeMenus();
    else if (state.selectedId) closeDrawer();
    else if (state.search) {
      elements.search.value = '';
      state.search = '';
      renderResults();
    } else elements.search.blur();
    return;
  }

  if (!inInput && !state.selectedId && event.key.length === 1 && !event.ctrlKey && !event.altKey && !event.metaKey && event.key.trim()) {
    elements.search.focus();
  }
});

document.addEventListener('load', event => {
  if (event.target.classList && event.target.classList.contains('cover-image')) event.target.classList.add('loaded');
}, true);

document.addEventListener('error', event => {
  const image = event.target;
  if (image.tagName !== 'IMG') return;
  const id = image.dataset.coverFor;
  if (id) {
    state.failedCovers.add(id);
    const game = state.byId.get(id);
    if (game) image.outerHTML = coverHtml(game);
  } else {
    image.style.visibility = 'hidden';
  }
}, true);

setInterval(renderFooter, 60 * 1000);

function applyLibrary(message) {
  state.games = message.games || [];
  state.byId = new Map(state.games.map(game => [game.Id, game]));
  state.platformIcons = message.platforms || {};
  state.drives = message.drives || [];
  state.generatedAt = message.generatedAt;
  state.sizes = {};
  for (const [key, record] of Object.entries(message.sizes || {})) state.sizes[pathKey(key)] = record;
  state.scan.running = !!message.scanning;
  state.syncing = !!message.syncing;
  state.hasGridKey = !!message.hasGridKey;
  state.userData = message.userData || {};
  state.settings = message.settings || {};
  state.extras = message.extras || {};
  state.loaded = true;
  for (const handler of extensions.libraryHandlers) handler(message);
  state.failedCovers.clear();
  if (state.drive && !state.drives.some(drive => drive.root === state.drive)) state.drive = null;
  renderAll();
}

function handleMessage(message) {
  switch (message.type) {
    case 'library':
      applyLibrary(message);
      break;

    case 'size':
      state.sizes[pathKey(message.record.Path)] = message.record;
      scheduleSizeUpdate();
      break;

    case 'scan':
      state.scan = { running: message.running, done: message.done, total: message.total };
      renderFooter();
      break;

    case 'art': {
      const game = state.byId.get(message.id);
      if (!game) break;
      Object.assign(game, { CoverUrl: message.cover, HeroUrl: message.hero, LogoUrl: message.logo, HeaderUrl: message.header, IconUrl: message.icon || game.IconUrl, CustomArt: message.custom || game.CustomArt });
      state.failedCovers.delete(game.Id);
      scheduleRender(400);
      break;
    }

    case 'sync':
      state.syncing = message.running;
      renderFooter();
      if (!message.running) {
        if (message.succeeded) toast('Library refreshed and Start Menu shortcuts updated', 'info', ICON.check);
        else toast('Refreshing the library failed. Check last-run.log.', 'error');
      }
      break;

    case 'launched': {
      const game = state.byId.get(message.id);
      if (game) {
        game.LastPlayed = message.lastPlayed;
        scheduleRender(800);
      }
      break;
    }

    case 'toast':
      toast(message.message, message.kind || 'info');
      break;

    case 'gameData':
      if (message.data && Object.keys(message.data).length) state.userData[message.id] = message.data;
      else delete state.userData[message.id];
      for (const handler of extensions.messageHandlers) handler(message);
      break;

    case 'settings':
      state.settings = message.settings || {};
      for (const handler of extensions.messageHandlers) handler(message);
      break;

    default:
      for (const handler of extensions.messageHandlers) handler(message);
  }
}

window.addEventListener('DOMContentLoaded', () => {
  renderAll();

  if (host) {
    host.addEventListener('message', event => handleMessage(event.data));
    send({ type: 'ready' });
  } else {
    state.loaded = true;
    elements.results.innerHTML = `<div class="empty">${ICON.gamepad}<strong>Open Game Hub from GameHub.exe</strong><span>This page needs the desktop app to read your library.</span></div>`;
  }
});
