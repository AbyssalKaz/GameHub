'use strict';

const VIRTUAL_THRESHOLD = 150;
const VIRTUAL_OVERSCAN = 2;

const SIZE_UNITS = { b: 1, kb: 1024, mb: 1024 ** 2, gb: GB, tb: 1024 ** 4 };
const AGE_UNITS = { h: 3600e3, d: 86400e3, w: 7 * 86400e3, m: 30.44 * 86400e3, mo: 30.44 * 86400e3, y: 365.25 * 86400e3 };
const AGE_WORDS = { h: 'hour', d: 'day', w: 'week', m: 'month', mo: 'month', y: 'year' };
const FAVORITE_WORDS = new Set(['fav', 'favs', 'favorite', 'favorites', 'favourite', 'favourites', 'starred']);

const searchCache = { text: null, result: null };

function parseSearch(text) {
  const source = String(text ?? '');
  if (searchCache.text === source) return searchCache.result;

  const filters = [];
  const words = [];
  const pattern = /(?:[^\s"]+:"[^"]*"|"[^"]*"|\S+)/g;
  let match;
  while ((match = pattern.exec(source))) {
    const raw = match[0];
    const filter = searchFilter(raw);
    if (filter) filters.push({ ...filter, raw, start: match.index, end: match.index + raw.length });
    else words.push(raw.replace(/^"|"$/g, ''));
  }

  const result = { text: words.join(' '), filters };
  searchCache.text = source;
  searchCache.result = result;
  return result;
}

function searchFilter(raw) {
  let token = raw;
  let negated = false;
  if (/^[-!]./.test(token)) {
    negated = true;
    token = token.slice(1);
  }

  const filter = positiveFilter(token);
  if (!filter) return null;
  if (!negated) return filter;
  return { label: `Not ${filter.label.charAt(0).toLowerCase()}${filter.label.slice(1)}`, test: game => !filter.test(game), negated: true };
}

function positiveFilter(token) {
  const lower = token.toLowerCase();
  if (FAVORITE_WORDS.has(lower)) return { label: 'Favorites', test: isFavorite };
  if (lower === 'unplayed' || lower === 'never-played') return { label: 'Never played', test: game => !game.LastPlayed && !game.PlaytimeMinutes };
  if (lower === 'played') return { label: 'Played', test: game => !!(game.LastPlayed || game.PlaytimeMinutes) };
  if (lower === 'installed') return null;

  const separator = token.indexOf(':');
  if (separator <= 0) return null;
  const key = lower.slice(0, separator);
  const value = token.slice(separator + 1).replace(/^"|"$/g, '').trim();
  if (!value) return null;

  switch (key) {
    case 'platform':
    case 'p':
    case 'source':
      return platformFilter(value);
    case 'size':
      return sizeFilter(value);
    case 'played':
    case 'lastplayed':
      return ageFilter(value, 'LastPlayed', 'Played');
    case 'installed':
    case 'added':
      return ageFilter(value, 'InstalledAt', 'Installed');
    case 'status':
      return statusFilter(value);
    case 'tag':
    case 'collection':
      return collectionFilter(value);
    case 'genre':
      return genreFilter(value);
    case 'drive':
      return driveFilter(value);
    case 'is':
      return positiveFilter(value);
    default:
      return null;
  }
}

function platformFilter(value) {
  const wanted = normalize(value).replace(/\s+/g, '');
  const platform = PLATFORMS.find(entry => entry.key === wanted)
    || PLATFORMS.find(entry => entry.key.startsWith(wanted) || normalize(entry.label).replace(/\s+/g, '').startsWith(wanted));
  if (!platform) return null;
  return { label: platform.label, icon: platformIcon(platform.key), test: game => platform.sources.includes(game.Source) };
}

function sizeFilter(value) {
  const match = /^(<=|>=|<|>|=)?(\d+(?:[.,]\d+)?)\s*(b|kb|mb|gb|tb)?$/i.exec(value);
  if (!match) return null;
  const operator = match[1] || '>=';
  const unit = (match[3] || 'gb').toLowerCase();
  const bytes = Number(match[2].replace(',', '.')) * SIZE_UNITS[unit];
  const compare = {
    '<': size => size < bytes,
    '<=': size => size <= bytes,
    '>': size => size > bytes,
    '>=': size => size >= bytes,
    '=': size => Math.abs(size - bytes) <= bytes * 0.05,
  }[operator];
  const amount = `${match[2]} ${unit.toUpperCase()}`;
  const label = operator.startsWith('<') ? `Under ${amount}` : operator === '=' ? `About ${amount}` : `Over ${amount}`;
  return {
    label,
    icon: ICON.ruler,
    test: game => {
      const size = sizeOf(game);
      return !size.pending && compare(size.bytes);
    },
  };
}

function ageFilter(value, field, verb) {
  const match = /^(<=|>=|<|>)?(\d+(?:\.\d+)?)\s*(h|d|w|mo|m|y)$/i.exec(value);
  if (!match) return null;
  const older = (match[1] || '<').startsWith('>');
  const unit = match[3].toLowerCase();
  const amount = Number(match[2]);
  const span = amount * AGE_UNITS[unit];
  const unitWord = `${AGE_WORDS[unit]}${amount === 1 ? '' : 's'}`;
  const label = older ? `${verb} over ${amount} ${unitWord} ago` : `${verb} in the last ${amount} ${unitWord}`;
  return {
    label,
    icon: ICON.clock,
    test: game => {
      if (!game[field]) return false;
      const age = Date.now() - Date.parse(game[field]);
      return older ? age > span : age <= span;
    },
  };
}

function statusFilter(value) {
  const wanted = normalize(value);
  const statuses = typeof PLAY_STATUSES !== 'undefined' ? PLAY_STATUSES : [];
  const status = statuses.find(entry => entry.key === wanted) || statuses.find(entry => entry.key.startsWith(wanted) || normalize(entry.label).startsWith(wanted));
  const key = status ? status.key : wanted;
  return { label: `Status: ${status ? status.label : value}`, test: game => normalize(gameData(game.Id).status || '') === key };
}

function collectionFilter(value) {
  const wanted = normalize(value);
  const names = game => (Array.isArray(gameData(game.Id).collections) ? gameData(game.Id).collections : []).map(normalize);
  return {
    label: `Collection: ${value}`,
    test: game => {
      const list = names(game);
      return list.includes(wanted) || list.some(name => name.startsWith(wanted));
    },
  };
}

function genreFilter(value) {
  const wanted = normalize(value);
  return {
    label: `Genre: ${value}`,
    test: game => {
      const genres = state.meta[game.Id] && state.meta[game.Id].Genres;
      return Array.isArray(genres) && genres.some(genre => normalize(genre).includes(wanted));
    },
  };
}

function driveFilter(value) {
  const letter = /^([a-z])(:\\?)?$/i.exec(value);
  if (!letter) return null;
  const root = `${letter[1].toUpperCase()}:\\`;
  return { label: `${letter[1].toUpperCase()}: drive`, icon: ICON.drive, test: game => gameDrives(game).has(root) };
}

function searchChipsHtml() {
  const { filters } = parseSearch(state.search);
  return filters.map((filter, index) => `
    <button class="chip removable active search-chip ${filter.negated ? 'negated' : ''}" data-search-token="${index}" title="Remove ${escapeHtml(filter.raw)} from the search">
      ${filter.icon || ICON.search}${escapeHtml(filter.label)}${ICON.close}
    </button>`).join('');
}

function removeSearchToken(index) {
  const filter = parseSearch(state.search).filters[index];
  if (!filter) return;
  const next = `${state.search.slice(0, filter.start)} ${state.search.slice(filter.end)}`.replace(/\s{2,}/g, ' ').trim();
  state.search = next;
  elements.search.value = next;
  elements.searchClear.hidden = !next;
  renderResults();
  for (const hook of extensions.afterRender) hook();
}

extensions.filterChips.unshift(searchChipsHtml);

elements.filters.addEventListener('click', event => {
  const chip = event.target.closest('[data-search-token]');
  if (chip) removeSearchToken(Number(chip.dataset.searchToken));
});

function editDistance(first, second, limit) {
  const rows = first.length;
  const columns = second.length;
  if (Math.abs(rows - columns) > limit) return limit + 1;
  let previousPrevious = null;
  let previous = Array.from({ length: columns + 1 }, (_, index) => index);
  for (let row = 1; row <= rows; row++) {
    const current = [row];
    let best = row;
    for (let column = 1; column <= columns; column++) {
      const cost = first[row - 1] === second[column - 1] ? 0 : 1;
      let value = Math.min(previous[column] + 1, current[column - 1] + 1, previous[column - 1] + cost);
      if (previousPrevious && row > 1 && column > 1 && first[row - 1] === second[column - 2] && first[row - 2] === second[column - 1]) {
        value = Math.min(value, previousPrevious[column - 2] + 1);
      }
      current.push(value);
      if (value < best) best = value;
    }
    if (best > limit) return limit + 1;
    previousPrevious = previous;
    previous = current;
  }
  return previous[columns];
}

function trigrams(text) {
  const padded = `  ${text} `;
  const set = new Set();
  for (let index = 0; index < padded.length - 2; index++) set.add(padded.slice(index, index + 3));
  return set;
}

function trigramSimilarity(first, second) {
  const a = trigrams(first);
  const b = trigrams(second);
  let shared = 0;
  for (const gram of a) if (b.has(gram)) shared++;
  return (2 * shared) / (a.size + b.size);
}

function fuzzyScore(words, compactName, query) {
  const compactQuery = query.replace(/[^\p{L}\p{N}]/gu, '');
  if (compactQuery.length < 3) return 0;

  const tokens = query.split(/[^\p{L}\p{N}]+/u).filter(Boolean);
  let distance = 0;
  let matched = tokens.length > 0;
  for (const token of tokens) {
    const allowed = token.length >= 8 ? 2 : token.length >= 4 ? 1 : 0;
    let best = Infinity;
    for (const word of words) {
      if (word.startsWith(token)) {
        best = 0;
        break;
      }
      if (!allowed) continue;
      best = Math.min(
        best,
        editDistance(token, word, allowed),
        editDistance(token, word.slice(0, token.length), allowed),
        editDistance(token, word.slice(0, token.length + 1), allowed),
      );
    }
    if (best > allowed) {
      matched = false;
      break;
    }
    distance += best;
  }
  if (matched) return Math.max(110, 180 - distance * 25);

  if (compactQuery.length < 4) return 0;
  const similarity = trigramSimilarity(compactQuery, compactName.slice(0, compactQuery.length + 6));
  return similarity >= 0.45 ? Math.round(40 + similarity * 60) : 0;
}

const DEFAULT_LIST_COLUMNS = ['platform', 'size', 'location', 'recent', 'playtime'];

extensions.listColumns.push(
  {
    key: 'platform',
    label: 'Platform',
    sortKey: 'platform',
    width: '140px',
    compactWidth: '120px',
    render: game => {
      const platform = platformOf(game);
      return `<div class="list-platform">${platformIcon(platform.key)}${escapeHtml(platform.label)}</div>`;
    },
  },
  {
    key: 'size',
    label: 'Size on disk',
    sortKey: 'size',
    width: '220px',
    compactWidth: '150px',
    render: (game, context) => {
      const size = sizeOf(game);
      const percent = size.pending || !context.maxBytes ? 0 : Math.max(1.5, (size.bytes / context.maxBytes) * 100);
      return `<div class="list-size"><div class="bar"><i data-meter style="width:${percent}%"></i></div><span class="value" data-size>${sizeMarkup(size)}</span></div>`;
    },
  },
  {
    key: 'location',
    label: 'Location',
    width: 'minmax(130px, 200px)',
    compactWidth: 'minmax(100px, 150px)',
    render: game => {
      const folder = displayFolder(game);
      return `<div class="list-cell" title="${escapeHtml(folder)}">${escapeHtml(locationLabel(folder))}</div>`;
    },
  },
  {
    key: 'recent',
    label: 'Last played',
    sortKey: 'recent',
    width: '120px',
    compactWidth: '104px',
    render: game => `<div class="list-cell">${escapeHtml(relativeTime(game.LastPlayed) || '—')}</div>`,
  },
  {
    key: 'playtime',
    label: 'Playtime',
    sortKey: 'playtime',
    width: '100px',
    compactWidth: '90px',
    render: game => `<div class="list-cell">${escapeHtml(formatPlaytime(game.PlaytimeMinutes) || '—')}</div>`,
  },
  {
    key: 'installed',
    label: 'Installed',
    sortKey: 'installed',
    width: '120px',
    compactWidth: '104px',
    render: game => `<div class="list-cell">${escapeHtml(game.InstalledAt ? formatDate(game.InstalledAt) : '—')}</div>`,
  },
);

const compactListQuery = matchMedia('(max-width: 1320px)');

function selectedListColumnKeys() {
  const saved = state.prefs.listColumns;
  if (Array.isArray(saved)) return saved;
  return extensions.listColumns.filter(column => DEFAULT_LIST_COLUMNS.includes(column.key) || column.defaultOn).map(column => column.key);
}

function activeListColumns() {
  const keys = new Set(selectedListColumnKeys());
  return extensions.listColumns.filter(column => keys.has(column.key));
}

function listColumnTemplate(columns) {
  const compact = compactListQuery.matches;
  const name = compact ? 'minmax(200px, 1fr)' : 'minmax(240px, 1fr)';
  return [name, ...columns.map(column => (compact && column.compactWidth) || column.width || 'minmax(100px, 160px)'), '28px'].join(' ');
}

compactListQuery.addEventListener('change', () => {
  if (state.prefs.layout === 'list' && !extensions.views[state.view]) scheduleRender(0);
});

const columnMenu = { element: null, anchor: null };

function columnMenuHtml() {
  const selected = new Set(selectedListColumnKeys());
  return `
    <div class="column-menu-title">Columns</div>
    ${extensions.listColumns.map(column => `<button class="menu-item ${selected.has(column.key) ? 'selected' : ''}" data-column-toggle="${escapeHtml(column.key)}">${ICON.check}${escapeHtml(column.label)}</button>`).join('')}
    <div class="menu-separator"></div>
    <button class="menu-item" data-column-reset>${ICON.refresh}Reset to default</button>`;
}

function openColumnMenu(anchor) {
  closeColumnMenu();
  const menu = document.createElement('div');
  menu.className = 'column-menu';
  menu.innerHTML = columnMenuHtml();
  document.body.appendChild(menu);
  const bounds = anchor.getBoundingClientRect();
  const size = menu.getBoundingClientRect();
  menu.style.left = `${Math.max(8, Math.min(bounds.right - size.width, window.innerWidth - size.width - 8))}px`;
  menu.style.top = `${Math.min(bounds.bottom + 6, window.innerHeight - size.height - 8)}px`;
  requestAnimationFrame(() => menu.classList.add('open'));
  columnMenu.element = menu;
  columnMenu.anchor = anchor;

  menu.addEventListener('click', event => {
    const toggle = event.target.closest('[data-column-toggle]');
    if (toggle) {
      const keys = new Set(selectedListColumnKeys());
      if (keys.has(toggle.dataset.columnToggle)) keys.delete(toggle.dataset.columnToggle);
      else keys.add(toggle.dataset.columnToggle);
      state.prefs.listColumns = extensions.listColumns.map(column => column.key).filter(key => keys.has(key));
    } else if (event.target.closest('[data-column-reset]')) {
      delete state.prefs.listColumns;
    } else return;
    savePrefs();
    menu.innerHTML = columnMenuHtml();
    const scroll = elements.scroller.scrollTop;
    renderResults();
    elements.scroller.scrollTop = scroll;
  });
}

function closeColumnMenu() {
  if (!columnMenu.element) return;
  const menu = columnMenu.element;
  columnMenu.element = null;
  menu.classList.remove('open');
  setTimeout(() => menu.remove(), 160);
}

elements.results.addEventListener('click', event => {
  const button = event.target.closest('[data-list-columns]');
  if (!button) return;
  event.stopPropagation();
  if (columnMenu.element) closeColumnMenu();
  else openColumnMenu(button);
});

document.addEventListener('mousedown', event => {
  if (columnMenu.element && !event.target.closest('.column-menu') && !event.target.closest('[data-list-columns]')) closeColumnMenu();
});

extensions.keyHandlers.push(event => {
  if (event.key !== 'Escape' || !columnMenu.element) return false;
  closeColumnMenu();
  return true;
});

const virtualResults = {
  container: null,
  items: [],
  indexOf: new Map(),
  maxBytes: 0,
  kind: 'grid',
  columns: 1,
  listColumns: [],
  rowHeight: 0,
  rowGap: 0,
  from: -1,
  to: -1,
  mounted: new Map(),
  frame: 0,
  settleTimer: 0,
  lastPitch: { grid: 0, list: 0 },
  lastColumns: 1,

  get active() {
    return !!(this.container && this.container.isConnected);
  },

  reset() {
    cancelAnimationFrame(this.frame);
    clearTimeout(this.settleTimer);
    this.frame = 0;
    this.container = null;
    this.items = [];
    this.indexOf = new Map();
    this.mounted = new Map();
    this.from = -1;
    this.to = -1;
  },

  mount(container, items, maxBytes, kind) {
    this.reset();
    if (!container) return;
    this.container = container;
    this.items = items;
    this.indexOf = new Map(items.map((game, index) => [game.Id, index]));
    this.maxBytes = maxBytes;
    this.kind = kind;
    this.listColumns = kind === 'list' ? activeListColumns() : [];
    container.classList.add('virtual');

    const estimate = this.lastPitch[kind];
    if (estimate) container.style.paddingBottom = `${Math.ceil(items.length / (kind === 'grid' ? this.lastColumns : 1)) * estimate}px`;

    this.rowHeight = 0;
    this.update();
    this.settleTimer = setTimeout(() => {
      if (container.isConnected) container.classList.add('settled');
    }, 700);
  },

  schedule() {
    if (this.frame || !this.container) return;
    this.frame = requestAnimationFrame(() => {
      this.frame = 0;
      this.update();
    });
  },

  measureColumns() {
    if (this.kind !== 'grid') {
      this.columns = 1;
      this.rowGap = 0;
      return;
    }
    const style = getComputedStyle(this.container);
    const gap = parseFloat(style.columnGap) || 0;
    const minimum = parseFloat(style.getPropertyValue('--card-width')) || state.prefs.cardSize || 190;
    const width = this.container.clientWidth;
    this.columns = Math.max(1, Math.floor((width + gap) / (minimum + gap)));
    this.rowGap = parseFloat(style.rowGap) || 0;
    this.lastColumns = this.columns;
  },

  measureRow() {
    const first = this.container.firstElementChild;
    return first ? first.offsetHeight : 0;
  },

  update() {
    if (!this.active) return;
    this.measureColumns();
    const totalRows = Math.ceil(this.items.length / this.columns);
    if (!this.rowHeight) {
      this.renderRange(0, Math.min(totalRows, 1), 0);
      this.rowHeight = this.measureRow();
      if (!this.rowHeight) return;
    }

    const pitch = this.rowHeight + this.rowGap;
    const scroller = elements.scroller;
    const offset = this.container.getBoundingClientRect().top - scroller.getBoundingClientRect().top;
    const viewTop = -offset;
    const viewBottom = viewTop + scroller.clientHeight;
    const first = Math.max(0, Math.min(totalRows, Math.floor(viewTop / pitch) - VIRTUAL_OVERSCAN));
    const last = Math.max(first, Math.min(totalRows, Math.ceil(viewBottom / pitch) + VIRTUAL_OVERSCAN));
    this.renderRange(first, last, pitch, totalRows);
    this.lastPitch[this.kind] = pitch;

    const measured = this.measureRow();
    if (measured && Math.abs(measured - this.rowHeight) > 0.5) {
      this.rowHeight = measured;
      this.schedule();
    }
  },

  renderRange(firstRow, lastRow, pitch, totalRows = lastRow) {
    const from = firstRow * this.columns;
    const to = Math.min(this.items.length, lastRow * this.columns);
    const container = this.container;
    container.style.paddingTop = `${firstRow * pitch}px`;
    container.style.paddingBottom = `${Math.max(0, totalRows - lastRow) * pitch}px`;
    if (from === this.from && to === this.to) return;
    this.from = from;
    this.to = to;

    const wanted = this.items.slice(from, to);
    const keep = new Set(wanted.map(game => game.Id));
    for (const [id, element] of this.mounted) {
      if (keep.has(id)) continue;
      element.remove();
      this.mounted.delete(id);
    }

    const missing = wanted.filter(game => !this.mounted.has(game.Id));
    const created = [];
    if (missing.length) {
      const template = document.createElement('template');
      template.innerHTML = missing.map(game => (this.kind === 'grid'
        ? cardHtml(game, this.indexOf.get(game.Id), this.maxBytes)
        : listRowHtml(game, this.listColumns, this.maxBytes))).join('');
      [...template.content.children].forEach((element, index) => {
        this.mounted.set(missing[index].Id, element);
        created.push(element);
      });
    }

    let cursor = container.firstElementChild;
    for (const game of wanted) {
      const element = this.mounted.get(game.Id);
      if (cursor === element) cursor = cursor.nextElementSibling;
      else container.insertBefore(element, cursor);
    }
    if (created.length) announceCards(created);
  },

  reveal(id) {
    const index = this.indexOf.get(id);
    if (index == null || !this.active || !this.rowHeight) return elements.results.querySelector(`[data-id="${CSS.escape(id)}"]`);
    const scroller = elements.scroller;
    const pitch = this.rowHeight + this.rowGap;
    const containerTop = this.container.getBoundingClientRect().top - scroller.getBoundingClientRect().top + scroller.scrollTop;
    const rowTop = containerTop + Math.floor(index / this.columns) * pitch;
    if (rowTop < scroller.scrollTop || rowTop + this.rowHeight > scroller.scrollTop + scroller.clientHeight) {
      scroller.scrollTo({ top: Math.max(0, rowTop - (scroller.clientHeight - this.rowHeight) / 2), behavior: 'instant' });
    }
    this.update();
    return this.mounted.get(id) || null;
  },
};

function announceCards(source) {
  if (!extensions.cardMounted.length) return;
  const list = source instanceof Element ? source.querySelectorAll('[data-id]') : source;
  for (const element of list) {
    const game = state.byId.get(element.dataset.id);
    if (!game) continue;
    for (const hook of extensions.cardMounted) {
      try {
        hook(element, game);
      } catch (error) {
        console.error(error);
      }
    }
  }
}

elements.scroller.addEventListener('scroll', () => {
  if (virtualResults.container) virtualResults.schedule();
  if (columnMenu.element) closeColumnMenu();
}, { passive: true });

new ResizeObserver(() => {
  if (virtualResults.container) virtualResults.schedule();
}).observe(elements.scroller);

elements.cardSize.addEventListener('input', () => {
  if (virtualResults.container) virtualResults.schedule();
});

function motionReduced() {
  return document.documentElement.dataset.motion === 'reduced';
}

function playViewTransition() {
  if (motionReduced() || document.documentElement.dataset.transition === 'none') return;
  for (const element of [elements.results, document.querySelector('.headline')]) {
    if (!element) continue;
    element.classList.remove('view-enter');
    void element.offsetWidth;
    element.classList.add('view-enter');
  }
}

document.addEventListener('animationend', event => {
  if (event.target.classList && event.target.classList.contains('view-enter') && event.animationName.startsWith('view-')) {
    event.target.classList.remove('view-enter');
  }
});
