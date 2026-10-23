'use strict';

ICON.plus = '<svg viewBox="0 0 24 24" class="glyph"><path d="M12 5v14M5 12h14"/></svg>';
ICON.bookmark = '<svg viewBox="0 0 24 24" class="glyph"><path d="M7 3.5h10a1 1 0 0 1 1 1v16l-6-4-6 4v-16a1 1 0 0 1 1-1z"/></svg>';
ICON.playCircle = '<svg viewBox="0 0 24 24" class="glyph"><circle cx="12" cy="12" r="9"/><path d="m10.2 8.6 5 3.4-5 3.4z"/></svg>';
ICON.trophy = '<svg viewBox="0 0 24 24" class="glyph"><path d="M8 4h8v5.5a4 4 0 0 1-8 0z"/><path d="M8 6H5v1.5a3 3 0 0 0 3 3M16 6h3v1.5a3 3 0 0 1-3 3M12 13.5V17M8.5 20.5h7M9.5 17h5"/></svg>';
ICON.dropped = '<svg viewBox="0 0 24 24" class="glyph"><circle cx="12" cy="12" r="9"/><path d="M9.5 9.5v5M14.5 9.5v5"/></svg>';
ICON.layers = '<svg viewBox="0 0 24 24" class="glyph"><path d="m12 3.5 8.5 4.5-8.5 4.5L3.5 8z"/><path d="m3.5 12 8.5 4.5 8.5-4.5M3.5 16l8.5 4.5 8.5-4.5"/></svg>';
ICON.pie = '<svg viewBox="0 0 24 24" class="glyph"><path d="M12 3.5a8.5 8.5 0 1 0 8.5 8.5H12z"/><path d="M15 3.8A8.5 8.5 0 0 1 20.2 9H15z"/></svg>';
ICON.copy = '<svg viewBox="0 0 24 24" class="glyph"><rect x="8.5" y="8.5" width="12" height="12" rx="2"/><path d="M15.5 8.5V6a2 2 0 0 0-2-2h-8a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h3"/></svg>';
ICON.pencil = '<svg viewBox="0 0 24 24" class="glyph"><path d="M4 20h4L19 9a2.8 2.8 0 0 0-4-4L4 16z"/><path d="m13.5 6.5 4 4"/></svg>';
ICON.file = '<svg viewBox="0 0 24 24" class="glyph"><path d="M6.5 3.5h7l4 4v12a1 1 0 0 1-1 1h-10a1 1 0 0 1-1-1v-15a1 1 0 0 1 1-1z"/><path d="M13.5 3.5v4h4"/></svg>';
ICON.chevronRight = '<svg viewBox="0 0 24 24" class="glyph"><path d="m9.5 6 6 6-6 6"/></svg>';
ICON.chevronDown = '<svg viewBox="0 0 24 24" class="glyph chevron"><path d="m6 9 6 6 6-6"/></svg>';

const PLAY_STATUSES = [
  { key: 'backlog', label: 'Backlog', color: '#8b9bff', icon: ICON.bookmark, rank: 1 },
  { key: 'playing', label: 'Playing', color: '#5fd39a', icon: ICON.playCircle, rank: 0 },
  { key: 'completed', label: 'Completed', color: '#f2c45c', icon: ICON.trophy, rank: 2 },
  { key: 'dropped', label: 'Dropped', color: '#ff8a8a', icon: ICON.dropped, rank: 3 },
];
const STATUS_BY_KEY = Object.fromEntries(PLAY_STATUSES.map(status => [status.key, status]));

const COLLECTION_COLORS = ['#7b8cff', '#a46bff', '#ff8fa3', '#ff8f5a', '#f2c45c', '#5fd39a', '#4fc3c9', '#5aa9e6'];

const PLATFORM_COLORS = {
  steam: '#5aa9e6',
  epic: '#c9ced8',
  hydra: '#a98bff',
  riot: '#ff6b6b',
  xbox: '#5fd39a',
  hoyoplay: '#6fd0ff',
  ea: '#ff8f5a',
  rockstar: '#ffcf5c',
  local: '#f2a65c',
  downloads: '#57d394',
  leftover: '#8a8f9c',
};

const CARD_META_OPTIONS = [
  { key: 'auto', label: 'Match the sort' },
  { key: 'lastPlayed', label: 'Last played' },
  { key: 'installed', label: 'Install date' },
  { key: 'added', label: 'Date added' },
  { key: 'playtime', label: 'Playtime' },
  { key: 'rating', label: 'Your rating' },
  { key: 'status', label: 'Play status' },
];

const GROUP_OPTIONS = [
  { key: null, label: 'No grouping' },
  { key: 'drive', label: 'Drive' },
  { key: 'platform', label: 'Platform' },
  { key: 'status', label: 'Play status' },
  { key: 'collection', label: 'Collection' },
];

const DAY = 24 * 60 * 60 * 1000;
const NEW_WINDOW = 7 * DAY;
const STALE_AFTER = 182 * DAY;
const STORAGE_PREVIEW_ROWS = 8;

const organize = {
  statusMenuOpen: false,
  groupMenuOpen: false,
  dragId: null,
  expanded: new Set(),
  memory: readStorage('organize.viewMemory', {}),
  currentKey: null,
  restoring: false,
  restoredView: false,
  editor: null,
  driveCache: null,
};

function parseTime(value) {
  if (!value) return 0;
  const time = Date.parse(value);
  return Number.isFinite(time) ? time : 0;
}

function statusOf(game) {
  const status = gameData(game.Id).status;
  return STATUS_BY_KEY[status] ? status : null;
}

function ratingOf(game) {
  const rating = Number(gameData(game.Id).rating);
  return rating >= 1 && rating <= 5 ? Math.round(rating) : 0;
}

function collectionList() {
  const list = setting('organize.collections', []);
  return Array.isArray(list) ? list.filter(entry => entry && typeof entry.name === 'string' && entry.name) : [];
}

function collectionsOf(game) {
  const names = gameData(game.Id).collections;
  return Array.isArray(names) ? names.filter(name => typeof name === 'string') : [];
}

function inCollection(game, name) {
  return collectionsOf(game).includes(name);
}

function collectionColor(name) {
  const entry = collectionList().find(collection => collection.name === name);
  return (entry && entry.color) || COLLECTION_COLORS[0];
}

function collectionViewKey(name) {
  return `col:${name}`;
}

function activeCollection() {
  return state.view.startsWith('col:') ? state.view.slice(4) : null;
}

function firstSeenOf(game) {
  const map = setting('organize.firstSeen', null);
  return map && typeof map === 'object' ? parseTime(map[game.Id]) : 0;
}

function addedAt(game) {
  return firstSeenOf(game) || parseTime(game.InstalledAt);
}

function isNew(game) {
  if (game.Status !== 'installed' || !setting('organize.newBadges', true)) return false;
  const since = Math.max(parseTime(game.InstalledAt), firstSeenOf(game));
  if (!since || Date.now() - since > NEW_WINDOW) return false;
  return parseTime(game.LastPlayed) < since && parseTime(gameData(game.Id).openedAt) < since;
}

function reviewScoreOf(game) {
  const meta = state.meta && state.meta[game.Id];
  if (!meta) return null;
  const score = Number(meta.ReviewScore ?? meta.Metacritic);
  return Number.isFinite(score) && score > 0 ? score : null;
}

function releaseTimeOf(game) {
  const meta = state.meta && state.meta[game.Id];
  const text = meta && meta.ReleaseDate;
  if (!text) return null;
  const parsed = Date.parse(text);
  if (Number.isFinite(parsed)) return parsed;
  const year = /\b(19|20)\d{2}\b/.exec(String(text));
  return year ? Date.UTC(Number(year[0]), 0, 1) : null;
}

function mainFolder(game) {
  return displayFolder(game);
}

function measuredBytes(game) {
  const size = sizeOf(game);
  return size.pending ? null : size.bytes;
}

function saveCollections(list) {
  setSetting('organize.collections', list.map(entry => ({ name: entry.name, color: entry.color })));
}

function setGameCollections(game, names) {
  setGameData(game.Id, { collections: names.length ? names : null });
}

function toggleCollection(game, name, force) {
  const names = collectionsOf(game);
  const member = names.includes(name);
  const next = force ?? !member;
  if (next === member) return false;
  setGameCollections(game, next ? [...names, name] : names.filter(entry => entry !== name));
  return true;
}

function nextCollectionColor() {
  const used = new Set(collectionList().map(entry => entry.color));
  return COLLECTION_COLORS.find(color => !used.has(color)) || COLLECTION_COLORS[collectionList().length % COLLECTION_COLORS.length];
}

function openCollectionEditor(originalName = null, addGameIds = []) {
  const existing = collectionList().find(entry => entry.name === originalName) || null;
  organize.editor = { original: existing ? existing.name : null, color: existing ? existing.color : nextCollectionColor(), addGameIds };

  modal.open(`
    <header class="modal-head">
      <h2>${existing ? 'Edit collection' : 'New collection'}</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="modal-body organize-editor">
      <label class="organize-field">
        <span class="organize-field-label">Name</span>
        <input class="organize-input" data-collection-name maxlength="40" spellcheck="false" autocomplete="off" value="${escapeHtml(existing ? existing.name : '')}" placeholder="Co-op nights">
      </label>
      <div class="organize-field">
        <span class="organize-field-label">Color</span>
        <div class="organize-swatches">${COLLECTION_COLORS.map(color => `<button class="organize-swatch ${color === organize.editor.color ? 'active' : ''}" data-collection-color="${color}" style="--swatch:${color}" title="${color}"></button>`).join('')}</div>
      </div>
      <div class="organize-error" data-collection-error></div>
    </div>
    <footer class="modal-foot">
      ${existing ? `<button class="danger-outline-button" data-collection-delete="${escapeHtml(existing.name)}">${ICON.trash || ICON.close}Delete</button>` : ''}
      <span class="grow"></span>
      <button class="secondary-button compact" data-modal-close>Cancel</button>
      <button class="primary-button compact" data-collection-save>${ICON.check}${existing ? 'Save' : 'Create'}</button>
    </footer>`, 'organize-modal');

  setTimeout(() => {
    const input = modal.layer.querySelector('[data-collection-name]');
    if (input) {
      input.focus();
      input.select();
    }
  }, 60);
}

function saveCollectionEditor() {
  const editor = organize.editor;
  const input = modal.layer.querySelector('[data-collection-name]');
  const error = modal.layer.querySelector('[data-collection-error]');
  if (!editor || !input) return;

  const name = input.value.trim().replace(/\s+/g, ' ');
  const list = collectionList();
  const clash = list.find(entry => entry.name.toLowerCase() === name.toLowerCase() && entry.name !== editor.original);
  if (!name || clash) {
    error.textContent = name ? 'You already have a collection with that name.' : 'Give the collection a name.';
    input.focus();
    return;
  }

  if (editor.original) {
    const renamed = editor.original !== name;
    saveCollections(list.map(entry => (entry.name === editor.original ? { name, color: editor.color } : entry)));
    if (renamed) {
      for (const game of state.games) {
        const names = collectionsOf(game);
        if (names.includes(editor.original)) setGameCollections(game, names.map(entry => (entry === editor.original ? name : entry)));
      }
      if (state.view === collectionViewKey(editor.original)) state.view = collectionViewKey(name);
      moveViewMemory(collectionViewKey(editor.original), collectionViewKey(name));
    }
  } else {
    saveCollections([...list, { name, color: editor.color }]);
    const added = editor.addGameIds.map(id => state.byId.get(id)).filter(Boolean);
    for (const game of added) toggleCollection(game, name, true);
    toast(added.length === 1 ? `Added ${added[0].Name} to ${name}` : `Created ${name}`, 'info', ICON.check);
  }

  organize.editor = null;
  modal.close();
  renderAll();
}

function confirmDeleteCollection(name) {
  const members = state.games.filter(game => inCollection(game, name)).length;
  modal.open(`
    <header class="modal-head">
      <h2>Delete ${escapeHtml(name)}?</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="modal-body">
      <p class="organize-confirm-text">${members ? `${formatCount(members)} ${members === 1 ? 'game stays' : 'games stay'} in your library; only the collection is removed.` : 'The collection is empty.'}</p>
    </div>
    <footer class="modal-foot">
      <span class="grow"></span>
      <button class="secondary-button compact" data-modal-close>Cancel</button>
      <button class="danger-button" data-collection-confirm-delete="${escapeHtml(name)}">${ICON.trash || ICON.close}Delete collection</button>
    </footer>`, 'organize-modal');
}

function deleteCollection(name) {
  saveCollections(collectionList().filter(entry => entry.name !== name));
  for (const game of state.games) {
    if (inCollection(game, name)) setGameCollections(game, collectionsOf(game).filter(entry => entry !== name));
  }
  delete organize.memory[collectionViewKey(name)];
  if (state.view === collectionViewKey(name)) state.view = 'all';
  modal.close();
  toast(`Deleted ${name}`, 'info', ICON.check);
  renderAll();
}

modal.layer.addEventListener('click', event => {
  const swatch = event.target.closest('[data-collection-color]');
  if (swatch && organize.editor) {
    organize.editor.color = swatch.dataset.collectionColor;
    for (const element of modal.layer.querySelectorAll('[data-collection-color]')) element.classList.toggle('active', element === swatch);
    return;
  }
  if (event.target.closest('[data-collection-save]')) {
    saveCollectionEditor();
    return;
  }
  const remove = event.target.closest('[data-collection-delete]');
  if (remove) {
    confirmDeleteCollection(remove.dataset.collectionDelete);
    return;
  }
  const confirm = event.target.closest('[data-collection-confirm-delete]');
  if (confirm) deleteCollection(confirm.dataset.collectionConfirmDelete);
});

modal.layer.addEventListener('keydown', event => {
  if (event.key === 'Enter' && event.target.matches('[data-collection-name]')) {
    event.preventDefault();
    saveCollectionEditor();
  }
});

function syncCollectionViews() {
  const list = collectionList();
  for (const key of Object.keys(VIEWS)) {
    if (key.startsWith('col:')) delete VIEWS[key];
  }
  for (const entry of list) VIEWS[collectionViewKey(entry.name)] = entry.name;
  const active = activeCollection();
  if (active !== null && !list.some(entry => entry.name === active)) state.view = 'all';
}

extensions.sidebarSections.unshift(() => {
  syncCollectionViews();
  const list = collectionList();
  const counts = new Map();
  for (const game of state.games) {
    if (game.Status !== 'installed') continue;
    for (const name of collectionsOf(game)) counts.set(name, (counts.get(name) || 0) + 1);
  }

  const items = list.map(entry => {
    const view = collectionViewKey(entry.name);
    const count = counts.get(entry.name) || 0;
    return `
      <button class="nav-item organize-collection ${state.view === view ? 'active' : ''}" data-view="${escapeHtml(view)}" data-collection="${escapeHtml(entry.name)}" data-collection-drop="${escapeHtml(entry.name)}" title="${escapeHtml(entry.name)} · ${count} ${count === 1 ? 'game' : 'games'}">
        <span class="nav-icon"><span class="organize-dot" style="--dot:${escapeHtml(entry.color)}"></span></span>
        <span class="nav-label">${escapeHtml(entry.name)}</span>
        <span class="nav-count">${count}</span>
      </button>`;
  }).join('');

  return `
    <div class="nav-section organize-collections ${list.length ? '' : 'empty'}">
      <div class="nav-heading organize-heading"><span>Collections</span><button class="organize-heading-add" data-collection-create title="New collection">${ICON.plus}</button></div>
      ${items}
      <button class="nav-item organize-new-collection" data-collection-create data-collection-drop="" title="New collection">
        <span class="nav-icon">${ICON.plus}</span>
        <span class="nav-label">New collection</span>
        <span class="nav-count"></span>
      </button>
    </div>`;
});

elements.sidebar.addEventListener('click', event => {
  if (event.target.closest('[data-collection-create]')) {
    event.stopPropagation();
    openCollectionEditor();
  }
}, true);

const collectionMenu = document.createElement('div');
collectionMenu.className = 'context-menu organize-collection-menu';
document.body.appendChild(collectionMenu);

function closeCollectionMenu() {
  collectionMenu.classList.remove('open');
}

elements.sidebar.addEventListener('contextmenu', event => {
  const item = event.target.closest('[data-collection]');
  if (!item) return;
  event.preventDefault();
  const name = item.dataset.collection;
  collectionMenu.dataset.collection = name;
  collectionMenu.innerHTML = `
    <button class="menu-item" data-collection-menu="open">${ICON.layers}Show games</button>
    <button class="menu-item" data-collection-menu="edit">${ICON.pencil}Rename or recolor…</button>
    <div class="menu-separator"></div>
    <button class="menu-item danger" data-collection-menu="delete">${ICON.trash || ICON.close}Delete collection…</button>`;
  collectionMenu.style.left = '0px';
  collectionMenu.style.top = '0px';
  collectionMenu.classList.add('open');
  const bounds = collectionMenu.getBoundingClientRect();
  collectionMenu.style.left = `${Math.min(event.clientX, window.innerWidth - bounds.width - 8)}px`;
  collectionMenu.style.top = `${Math.min(event.clientY, window.innerHeight - bounds.height - 8)}px`;
});

collectionMenu.addEventListener('click', event => {
  const item = event.target.closest('[data-collection-menu]');
  if (!item) return;
  const name = collectionMenu.dataset.collection;
  closeCollectionMenu();
  if (item.dataset.collectionMenu === 'open') setView(collectionViewKey(name));
  else if (item.dataset.collectionMenu === 'edit') openCollectionEditor(name);
  else if (item.dataset.collectionMenu === 'delete') confirmDeleteCollection(name);
});

function draggableItem(target) {
  const item = target.closest('[data-id]');
  if (!item || item.classList.contains('list-head') || !elements.results.contains(item)) return null;
  return item.classList.contains('card') || item.classList.contains('list-row') || item.classList.contains('organize-row') ? item : null;
}

elements.results.addEventListener('mousedown', event => {
  if (event.button !== 0) return;
  const item = draggableItem(event.target);
  if (item && !item.draggable) item.draggable = true;
});

elements.results.addEventListener('dragstart', event => {
  const item = draggableItem(event.target);
  const game = item && state.byId.get(item.dataset.id);
  if (!game) return;
  organize.dragId = game.Id;
  event.dataTransfer.effectAllowed = 'copy';
  event.dataTransfer.setData('application/x-game-hub-id', game.Id);
  const cover = item.querySelector('.cover');
  if (cover) event.dataTransfer.setDragImage(cover, Math.min(60, cover.offsetWidth / 2), 40);
  document.body.classList.add('organize-dragging');
});

function clearDropTargets() {
  for (const element of elements.sidebar.querySelectorAll('.drop-over')) element.classList.remove('drop-over');
}

document.addEventListener('dragend', () => {
  organize.dragId = null;
  document.body.classList.remove('organize-dragging');
  clearDropTargets();
});

elements.sidebar.addEventListener('dragover', event => {
  const target = event.target.closest('[data-collection-drop]');
  if (!target || !organize.dragId) return;
  event.preventDefault();
  event.dataTransfer.dropEffect = 'copy';
  if (!target.classList.contains('drop-over')) {
    clearDropTargets();
    target.classList.add('drop-over');
  }
});

elements.sidebar.addEventListener('dragleave', event => {
  const target = event.target.closest('[data-collection-drop]');
  if (target && !target.contains(event.relatedTarget)) target.classList.remove('drop-over');
});

elements.sidebar.addEventListener('drop', event => {
  const target = event.target.closest('[data-collection-drop]');
  const game = organize.dragId && state.byId.get(organize.dragId);
  if (!target || !game) return;
  event.preventDefault();
  clearDropTargets();
  document.body.classList.remove('organize-dragging');
  organize.dragId = null;

  const name = target.dataset.collectionDrop;
  if (!name) {
    openCollectionEditor(null, [game.Id]);
    return;
  }
  if (toggleCollection(game, name, true)) {
    toast(`Added ${game.Name} to ${name}`, 'info', ICON.check);
    renderSidebar();
    if (activeCollection() === name) scheduleRender(100);
  } else {
    toast(`${game.Name} is already in ${name}`, 'info');
  }
});

extensions.filters.push(list => {
  const collection = activeCollection();
  if (collection !== null) list = list.filter(game => inCollection(game, collection));
  const status = state.prefs.statusFilter;
  if (status && status !== 'any' && state.view !== 'leftovers') list = list.filter(game => (statusOf(game) || 'none') === status);
  return list;
});

extensions.filterChips.push(() => {
  const collection = activeCollection();
  if (collection === null) return '';
  return `<button class="chip" data-organize-edit-collection="${escapeHtml(collection)}"><span class="organize-dot" style="--dot:${escapeHtml(collectionColor(collection))}"></span>Edit collection</button>`;
});

extensions.filterChips.push(() => {
  if (state.view === 'leftovers') return '';
  const current = state.prefs.statusFilter || 'any';
  const counts = { any: 0, none: 0 };
  const collection = activeCollection();
  const pool = collection === null ? baseList() : baseList().filter(game => inCollection(game, collection));
  for (const game of pool) {
    counts.any++;
    const status = statusOf(game) || 'none';
    counts[status] = (counts[status] || 0) + 1;
  }

  const options = [
    { key: 'any', label: 'Any status', icon: '' },
    ...PLAY_STATUSES.map(status => ({ key: status.key, label: status.label, icon: `<span class="organize-status-icon" style="--status:${status.color}">${status.icon}</span>` })),
    { key: 'none', label: 'No status', icon: '' },
  ];
  const selected = STATUS_BY_KEY[current];
  const label = selected ? selected.label : current === 'none' ? 'No status' : 'Status';
  const icon = selected ? `<span class="organize-status-icon" style="--status:${selected.color}">${selected.icon}</span>` : ICON.bookmark;

  return `
    <div class="dropdown organize-status-filter ${organize.statusMenuOpen ? 'open' : ''}">
      <button class="chip ${current !== 'any' ? 'active' : ''}" data-organize-status-toggle>${icon}${escapeHtml(label)}${ICON.chevronDown}</button>
      <div class="dropdown-menu">
        ${options.map(option => `
          <button class="menu-item ${option.key === current ? 'selected' : ''}" data-organize-status-filter="${option.key}">
            ${ICON.check}${option.icon}<span class="grow">${option.label}</span><span class="organize-menu-count">${formatCount(counts[option.key] || 0)}</span>
          </button>`).join('')}
      </div>
    </div>`;
});

elements.filters.addEventListener('click', event => {
  const toggle = event.target.closest('[data-organize-status-toggle]');
  if (toggle) {
    organize.statusMenuOpen = !organize.statusMenuOpen;
    toggle.closest('.organize-status-filter').classList.toggle('open', organize.statusMenuOpen);
    return;
  }
  const option = event.target.closest('[data-organize-status-filter]');
  if (option) {
    organize.statusMenuOpen = false;
    state.prefs.statusFilter = option.dataset.organizeStatusFilter === 'any' ? null : option.dataset.organizeStatusFilter;
    savePrefs();
    elements.scroller.scrollTop = 0;
    renderAll();
    return;
  }
  const editCollection = event.target.closest('[data-organize-edit-collection]');
  if (editCollection) {
    openCollectionEditor(editCollection.dataset.organizeEditCollection);
    return;
  }
  if (event.target.closest('[data-organize-rescan]') && !state.scan.running) {
    state.scan = { running: true, done: 0, total: 0 };
    renderFooter();
    send({ type: 'rescan' });
  }
});

document.addEventListener('click', event => {
  if (organize.statusMenuOpen && !event.target.closest('.organize-status-filter')) {
    organize.statusMenuOpen = false;
    for (const element of document.querySelectorAll('.organize-status-filter.open')) element.classList.remove('open');
  }
  if (organize.groupMenuOpen && !event.target.closest('#organizeGroup')) setGroupMenu(false);
  if (!event.target.closest('.organize-collection-menu')) closeCollectionMenu();
}, true);

document.addEventListener('keydown', event => {
  if (event.key !== 'Escape') return;
  if (collectionMenu.classList.contains('open') || organize.statusMenuOpen || organize.groupMenuOpen) {
    closeCollectionMenu();
    organize.statusMenuOpen = false;
    for (const element of document.querySelectorAll('.organize-status-filter.open')) element.classList.remove('open');
    setGroupMenu(false);
    event.stopImmediatePropagation();
  }
}, true);

extensions.cardBadges.push(game => {
  let html = '';
  if (isNew(game)) html += '<span class="organize-new-badge">New</span>';
  const status = STATUS_BY_KEY[statusOf(game)];
  if (status && setting('organize.statusBadges', true)) {
    html += `<span class="organize-status-badge" style="--status:${status.color}" title="${status.label}">${status.icon}</span>`;
  }
  return html;
});

extensions.cardMeta = extensions.cardMeta || [];
extensions.cardMeta.push(game => {
  const mode = setting('organize.cardMeta', 'auto');
  const key = mode === 'auto' ? state.prefs.sort : mode;
  const own = ['rating', 'review', 'release', 'status', 'added'];
  if (mode === 'auto' && !own.includes(key)) return undefined;

  switch (key) {
    case 'rating': {
      const rating = ratingOf(game);
      return rating ? `${'★'.repeat(rating)}${'☆'.repeat(5 - rating)}` : 'Not rated';
    }
    case 'review': {
      const meta = state.meta && state.meta[game.Id];
      if (meta && meta.ReviewScore) return `${meta.ReviewScore}% positive`;
      if (meta && meta.Metacritic) return `Metacritic ${meta.Metacritic}`;
      return 'No reviews yet';
    }
    case 'release': {
      const time = releaseTimeOf(game);
      return time ? `Released ${new Date(time).getFullYear()}` : 'Release date unknown';
    }
    case 'status': {
      const status = STATUS_BY_KEY[statusOf(game)];
      return status ? status.label : 'No status';
    }
    case 'added': {
      const time = addedAt(game);
      return time ? `Added ${relativeTime(new Date(time).toISOString())}` : 'Added before tracking';
    }
    case 'installed':
      return game.InstalledAt ? `Installed ${relativeTime(game.InstalledAt)}` : 'Install date unknown';
    case 'lastPlayed':
      return relativeTime(game.LastPlayed) || 'Never played';
    case 'playtime':
      return formatPlaytime(game.PlaytimeMinutes) || 'No playtime yet';
    default:
      return undefined;
  }
});

const artworkMenuIndex = extensions.menuItems.findIndex(item => String(item).includes('change-art'));

extensions.menuItems.splice(artworkMenuIndex + 1, 0, game => {
  if (game.Status !== 'installed') return '';
  const current = statusOf(game);
  for (const status of PLAY_STATUSES) {
    extensions.menuActions[`organize-status-${status.key}`] = target => setPlayStatus(target, statusOf(target) === status.key ? null : status.key);
  }

  const list = collectionList();
  const shown = list.slice(0, 8);
  const collectionItems = shown.map((entry, index) => {
    extensions.menuActions[`organize-collection-${index}`] = target => {
      const added = !inCollection(target, entry.name);
      toggleCollection(target, entry.name);
      toast(added ? `Added ${target.Name} to ${entry.name}` : `Removed ${target.Name} from ${entry.name}`, 'info', ICON.check);
      afterOrganizeChange();
    };
    return `<button class="menu-item organize-menu-collection ${inCollection(game, entry.name) ? 'selected' : ''}" data-menu="organize-collection-${index}">${ICON.check}<span class="organize-dot" style="--dot:${escapeHtml(entry.color)}"></span><span class="grow">${escapeHtml(entry.name)}</span></button>`;
  }).join('');

  return `
    <div class="menu-separator"></div>
    <div class="organize-menu-label">Status</div>
    <div class="organize-menu-status">
      ${PLAY_STATUSES.map(status => `<button class="${current === status.key ? 'active' : ''}" data-menu="organize-status-${status.key}" style="--status:${status.color}" title="${current === status.key ? 'Clear status' : status.label}">${status.icon}<span>${status.label}</span></button>`).join('')}
    </div>
    <div class="menu-separator"></div>
    <div class="organize-menu-label">Collections</div>
    ${collectionItems}
    ${list.length > shown.length ? `<button class="menu-item" data-menu="organize-collection-more">${ICON.layers}All collections…</button>` : ''}
    <button class="menu-item" data-menu="organize-collection-new">${ICON.plus}New collection…</button>`;
});

extensions.menuActions['organize-collection-new'] = game => openCollectionEditor(null, [game.Id]);
extensions.menuActions['organize-collection-more'] = game => openDrawer(game.Id);

function setPlayStatus(game, status) {
  setGameData(game.Id, { status: status || null });
  afterOrganizeChange();
}

function afterOrganizeChange() {
  renderSidebar();
  if (state.selectedId) renderDrawer();
  scheduleRender(120);
}

extensions.drawerSections.unshift(game => {
  if (game.Status !== 'installed') return '';
  const current = statusOf(game);
  const rating = ratingOf(game);
  const list = collectionList();

  const statusButtons = PLAY_STATUSES.map(status => `
    <button class="${current === status.key ? 'active' : ''}" data-action="organize-status" data-value="${status.key}" style="--status:${status.color}" title="${current === status.key ? 'Clear status' : `Mark as ${status.label.toLowerCase()}`}">${status.icon}<span>${status.label}</span></button>`).join('');

  const stars = [1, 2, 3, 4, 5].map(value => `
    <button class="${value <= rating ? 'on' : ''}" data-action="organize-rate" data-value="${value === rating ? 0 : value}" title="${value === rating ? 'Clear rating' : `${value} star${value > 1 ? 's' : ''}`}">${ICON.starFilled}</button>`).join('');

  const chips = list.map((entry, index) => `
    <button class="organize-chip ${inCollection(game, entry.name) ? 'on' : ''}" data-action="organize-collection" data-value="${index}" style="--dot:${escapeHtml(entry.color)}">
      <span class="organize-dot"></span>${escapeHtml(entry.name)}
    </button>`).join('');

  return `
    <section class="organize-drawer">
      <h3 class="section-title">Your library</h3>
      <div class="organize-panel">
        <div class="organize-line">
          <span class="organize-line-label">Status</span>
          <div class="organize-segmented">${statusButtons}</div>
        </div>
        <div class="organize-line">
          <span class="organize-line-label">Rating</span>
          <div class="organize-stars">${stars}</div>
          <span class="organize-rating-text">${rating ? `${rating} / 5` : 'Not rated'}</span>
        </div>
        <div class="organize-line top">
          <span class="organize-line-label">Collections</span>
          <div class="organize-chips">
            ${chips}
            <button class="organize-chip add" data-action="organize-new-collection">${ICON.plus}New collection</button>
          </div>
        </div>
      </div>
      <div class="organize-notes" data-organize-notes></div>
    </section>`;
});

extensions.drawerActions['organize-status'] = (game, element) => {
  setPlayStatus(game, statusOf(game) === element.dataset.value ? null : element.dataset.value);
};

extensions.drawerActions['organize-rate'] = (game, element) => {
  const rating = Number(element.dataset.value);
  setGameData(game.Id, { rating: rating || null });
  afterOrganizeChange();
};

extensions.drawerActions['organize-collection'] = (game, element) => {
  const entry = collectionList()[Number(element.dataset.value)];
  if (!entry) return;
  toggleCollection(game, entry.name);
  afterOrganizeChange();
};

extensions.drawerActions['organize-new-collection'] = game => openCollectionEditor(null, [game.Id]);

const notesEditor = {
  element: null,
  textarea: null,
  status: null,
  id: null,
  timer: null,
  focused: false,
  dirty: false,
};

function buildNotesEditor() {
  const wrapper = document.createElement('div');
  wrapper.className = 'organize-notes-box';
  wrapper.innerHTML = `
    <div class="organize-notes-head"><h3 class="section-title">Notes</h3><span class="organize-notes-status"></span></div>
    <textarea class="organize-notes-input" rows="3" spellcheck="true" placeholder="Private notes: where you left off, codes, mods to reinstall…"></textarea>`;
  notesEditor.element = wrapper;
  notesEditor.textarea = wrapper.querySelector('textarea');
  notesEditor.status = wrapper.querySelector('.organize-notes-status');

  const { textarea } = notesEditor;
  textarea.addEventListener('input', () => {
    notesEditor.dirty = true;
    notesEditor.status.textContent = '';
    clearTimeout(notesEditor.timer);
    notesEditor.timer = setTimeout(saveNotes, 700);
    growNotes();
  });
  textarea.addEventListener('focus', () => {
    notesEditor.focused = true;
  });
  textarea.addEventListener('blur', () => {
    if (!textarea.isConnected) return;
    notesEditor.focused = false;
    saveNotes();
  });
}

function growNotes() {
  const { textarea } = notesEditor;
  textarea.style.height = 'auto';
  textarea.style.height = `${Math.min(420, Math.max(88, textarea.scrollHeight + 2))}px`;
}

function saveNotes() {
  clearTimeout(notesEditor.timer);
  if (!notesEditor.dirty || !notesEditor.id) return;
  const text = notesEditor.textarea.value;
  setGameData(notesEditor.id, { notes: text.trim() ? text : null });
  notesEditor.dirty = false;
  notesEditor.status.textContent = 'Saved';
}

function attachNotes(game) {
  const slot = elements.drawer.querySelector('[data-organize-notes]');
  if (!slot || slot.firstChild) return;
  if (!notesEditor.element) buildNotesEditor();

  const { textarea } = notesEditor;
  if (notesEditor.id !== game.Id) {
    saveNotes();
    notesEditor.id = game.Id;
    notesEditor.focused = false;
    notesEditor.dirty = false;
    notesEditor.status.textContent = '';
    textarea.value = gameData(game.Id).notes || '';
  }

  const start = textarea.selectionStart;
  const end = textarea.selectionEnd;
  const refocus = notesEditor.focused;
  slot.appendChild(notesEditor.element);
  growNotes();
  if (refocus) {
    textarea.focus({ preventScroll: true });
    textarea.setSelectionRange(start, end);
  }
}

function decorateDrawer() {
  const game = state.byId.get(state.selectedId);
  if (!game || !elements.drawer.classList.contains('open') && !elements.drawer.firstChild) return;

  attachNotes(game);
  rememberSelection(game.Id);

  if (isNew(game)) {
    setGameData(game.Id, { openedAt: new Date().toISOString() });
    for (const badge of elements.results.querySelectorAll(`[data-id="${CSS.escape(game.Id)}"] .organize-new-badge`)) badge.remove();
  }

  const markLink = (element, path) => {
    if (!element || !path) return;
    element.classList.add('organize-size-link');
    element.dataset.organizePath = path;
    element.title = 'See which folders take up the space';
  };
  markLink(elements.drawer.querySelector('.usage-total .value'), mainFolder(game));
  for (const row of elements.drawer.querySelectorAll('.usage-row')) {
    const folderButton = row.querySelector('[data-path]');
    markLink(row.querySelector('.usage-size'), folderButton && folderButton.dataset.path);
  }
  for (const stat of elements.drawer.querySelectorAll('.stat')) {
    const label = stat.querySelector('.stat-label');
    if (label && label.textContent.trim() === 'Size on disk') markLink(stat.querySelector('.stat-value'), mainFolder(game));
  }
}

new MutationObserver(records => {
  const classChanged = records.some(record => record.type === 'attributes');
  if (classChanged && !elements.drawer.classList.contains('open')) {
    saveNotes();
    if (notesEditor.focused && notesEditor.textarea) {
      notesEditor.focused = false;
      notesEditor.textarea.blur();
    }
    return;
  }
  if (records.some(record => record.type === 'childList')) decorateDrawer();
}).observe(elements.drawer, { childList: true, attributes: true, attributeFilter: ['class'] });

elements.drawer.addEventListener('click', event => {
  const link = event.target.closest('[data-organize-path]');
  if (!link) return;
  event.stopPropagation();
  openBreakdown(link.dataset.organizePath, state.byId.get(state.selectedId));
});

SORTS.push(
  { key: 'added', label: 'Date added' },
  { key: 'rating', label: 'Your rating' },
  { key: 'review', label: 'Review score' },
  { key: 'release', label: 'Release date' },
  { key: 'status', label: 'Play status' },
);

function descendingNullsLast(first, second) {
  if (first == null && second == null) return 0;
  if (first == null) return 1;
  if (second == null) return -1;
  return second - first;
}

extensions.sortComparers.added = (first, second) => descendingNullsLast(addedAt(first) || null, addedAt(second) || null);
extensions.sortComparers.rating = (first, second) => descendingNullsLast(ratingOf(first) || null, ratingOf(second) || null);
extensions.sortComparers.review = (first, second) => descendingNullsLast(reviewScoreOf(first), reviewScoreOf(second));
extensions.sortComparers.release = (first, second) => descendingNullsLast(releaseTimeOf(first), releaseTimeOf(second));
extensions.sortComparers.status = (first, second) => {
  const rank = game => (STATUS_BY_KEY[statusOf(game)] ? STATUS_BY_KEY[statusOf(game)].rank : 9);
  return rank(first) - rank(second);
};

try {
  if (typeof compareGames === 'function') {
    const baseCompare = compareGames;
    compareGames = function (first, second, sortKey = state.prefs.sort) {
      if (state.view === 'all' && !state.search.trim() && setting('organize.pinFavorites', false)) {
        const pinned = Number(isFavorite(second)) - Number(isFavorite(first));
        if (pinned) return pinned;
      }
      return baseCompare(first, second, sortKey);
    };
  }
} catch (error) {
  console.warn('Pinning favorites is unavailable', error);
}

function driveUsage() {
  const cache = organize.driveCache;
  if (cache && cache.games === state.games && Date.now() - cache.at < 400) return cache.drives;

  const seen = new Set();
  const drives = new Map();
  for (const game of state.games) {
    for (const part of partsOf(game)) {
      const key = pathKey(part.path);
      if (seen.has(key)) continue;
      seen.add(key);
      const root = driveOf(part.path);
      if (!root) continue;
      if (!drives.has(root)) drives.set(root, { bytes: 0, pending: false, platforms: new Map() });
      const entry = drives.get(root);
      const record = state.sizes[key];
      if (!record) {
        entry.pending = true;
        continue;
      }
      const platform = game.Status === 'installed' ? platformOf(game).key : 'leftover';
      entry.bytes += record.Bytes;
      entry.platforms.set(platform, (entry.platforms.get(platform) || 0) + record.Bytes);
    }
  }
  organize.driveCache = { games: state.games, at: Date.now(), drives };
  return drives;
}

function driveMeterHtml(drive) {
  const usage = driveUsage().get(drive.root) || { bytes: 0 };
  const used = drive.total - drive.free;
  const gamesPercent = Math.min(100, (usage.bytes / drive.total) * 100);
  const otherPercent = Math.max(0, Math.min(100 - gamesPercent, ((used - usage.bytes) / drive.total) * 100));
  const low = drive.free / drive.total < 0.1;
  return `
    <span class="organize-drive-meter ${low ? 'low' : ''}" title="Games ${formatBytes(usage.bytes)} · other files ${formatBytes(Math.max(0, used - usage.bytes))} · ${formatBytes(drive.free)} free">
      <span class="organize-drive-bar"><i class="games" style="width:${gamesPercent}%"></i><i class="other" style="width:${otherPercent}%"></i></span>
      <span class="organize-drive-free">${formatBytes(drive.free)} free of ${formatBytes(drive.total)}</span>
    </span>`;
}

extensions.groupers.drive = {
  label: 'Drive',
  groupOf(game) {
    const root = driveOf(mainFolder(game));
    if (!root) return { key: '~', label: 'Unknown location', order: 99 };
    const drive = state.drives.find(entry => entry.root === root);
    return {
      key: root,
      label: `${escapeHtml(root.replace('\\', ''))} <span class="organize-group-sub">${escapeHtml((drive && drive.label) || 'Local Disk')}</span>`,
      note: drive ? driveMeterHtml(drive) : '',
      order: 0,
    };
  },
  compare: (first, second) => first.order - second.order || first.key.localeCompare(second.key),
};

extensions.groupers.platform = {
  label: 'Platform',
  groupOf(game) {
    const platform = platformOf(game);
    return { key: platform.key, label: `<span class="organize-group-icon">${platformIcon(platform.key)}</span>${escapeHtml(platform.label)}`, order: PLATFORMS.indexOf(platform) };
  },
  compare: (first, second) => first.order - second.order,
};

extensions.groupers.status = {
  label: 'Play status',
  groupOf(game) {
    const status = STATUS_BY_KEY[statusOf(game)];
    if (!status) return { key: 'none', label: 'No status', order: 9 };
    return { key: status.key, label: `<span class="organize-group-icon organize-status-icon" style="--status:${status.color}">${status.icon}</span>${status.label}`, order: status.rank };
  },
  compare: (first, second) => first.order - second.order,
};

extensions.groupers.collection = {
  label: 'Collection',
  groupOf(game) {
    const list = collectionList();
    const names = collectionsOf(game);
    const index = list.findIndex(entry => names.includes(entry.name));
    if (index < 0) return { key: '~', label: 'Not in a collection', order: 9999 };
    const entry = list[index];
    return { key: `c${index}`, label: `<span class="organize-dot" style="--dot:${escapeHtml(entry.color)}"></span>${escapeHtml(entry.name)}`, order: index };
  },
  compare: (first, second) => first.order - second.order,
};

function mountGroupControl() {
  const sort = document.getElementById('sortDropdown');
  if (!sort || document.getElementById('organizeGroup')) return;
  sort.insertAdjacentHTML('beforebegin', `
    <div class="dropdown organize-group" id="organizeGroup">
      <button class="dropdown-button" id="organizeGroupButton" title="Group and arrange games">
        ${ICON.layers}<span id="organizeGroupLabel">Group</span>${ICON.chevronDown}
      </button>
      <div class="dropdown-menu" id="organizeGroupMenu"></div>
    </div>`);

  document.getElementById('organizeGroupButton').addEventListener('click', () => {
    elements.sortDropdown.classList.remove('open');
    setGroupMenu(!organize.groupMenuOpen);
  });

  document.getElementById('organizeGroupMenu').addEventListener('click', event => {
    const group = event.target.closest('[data-organize-group]');
    const meta = event.target.closest('[data-organize-card-meta]');
    const pin = event.target.closest('[data-organize-pin]');
    if (group) {
      state.prefs.groupBy = group.dataset.organizeGroup || null;
      if (state.prefs.groupBy && state.prefs.layout === 'list') state.prefs.layout = 'grid';
      savePrefs();
      setGroupMenu(false);
    } else if (meta) {
      setSetting('organize.cardMeta', meta.dataset.organizeCardMeta === 'auto' ? null : meta.dataset.organizeCardMeta);
      setGroupMenu(false);
    } else if (pin) {
      setSetting('organize.pinFavorites', !setting('organize.pinFavorites', false) || null);
    } else {
      return;
    }
    renderAll();
  });
}

function setGroupMenu(open) {
  organize.groupMenuOpen = open;
  const element = document.getElementById('organizeGroup');
  if (element) element.classList.toggle('open', open);
}

function renderGroupControl() {
  mountGroupControl();
  const element = document.getElementById('organizeGroup');
  if (!element) return;
  element.classList.toggle('hidden', !!extensions.views[state.view]);

  const current = extensions.groupers[state.prefs.groupBy] ? state.prefs.groupBy : null;
  const option = GROUP_OPTIONS.find(entry => entry.key === current);
  document.getElementById('organizeGroupLabel').textContent = current ? `By ${option ? option.label.toLowerCase() : current}` : 'Group';
  element.querySelector('.dropdown-button').classList.toggle('on', !!current);

  const cardMeta = setting('organize.cardMeta', 'auto');
  const pinned = setting('organize.pinFavorites', false);
  document.getElementById('organizeGroupMenu').innerHTML = `
    <div class="organize-menu-label">Group by</div>
    ${GROUP_OPTIONS.map(entry => `<button class="menu-item ${entry.key === current ? 'selected' : ''}" data-organize-group="${entry.key || ''}">${ICON.check}${entry.label}</button>`).join('')}
    <div class="menu-separator"></div>
    <div class="organize-menu-label">Card details</div>
    ${CARD_META_OPTIONS.map(entry => `<button class="menu-item ${entry.key === cardMeta ? 'selected' : ''}" data-organize-card-meta="${entry.key}">${ICON.check}${entry.label}</button>`).join('')}
    <div class="menu-separator"></div>
    <button class="menu-item ${pinned ? 'selected' : ''}" data-organize-pin>${ICON.check}Pin favorites first</button>`;
}

function storageGameRow(game, maxBytes, extra = '') {
  const platform = platformOf(game);
  const size = sizeOf(game);
  const folder = mainFolder(game);
  const percent = size.pending || !maxBytes ? 0 : Math.max(1.5, (size.bytes / maxBytes) * 100);
  const thumb = game.IconUrl
    ? `<img class="list-thumb icon" src="${escapeHtml(game.IconUrl)}" alt="">`
    : game.HeaderUrl || game.CoverUrl
      ? `<img class="list-thumb" src="${escapeHtml(game.HeaderUrl || game.CoverUrl)}" alt="">`
      : '<div class="list-thumb"></div>';
  const played = game.Status === 'installed'
    ? (game.LastPlayed ? `Played ${relativeTime(game.LastPlayed).toLowerCase()}` : 'Never played')
    : statusLabel(game);
  const sub = [played, locationLabel(folder)].filter(Boolean).map(escapeHtml).join(' · ');
  const removeLabel = game.Status === 'installed' ? 'Uninstall' : 'Remove files';

  return `
    <div class="organize-row" data-id="${escapeHtml(game.Id)}">
      ${thumb}
      <div class="organize-row-text">
        <div class="organize-row-title">${escapeHtml(game.Name)}${extra}</div>
        <div class="organize-row-sub"><span class="organize-row-platform">${platformIcon(platform.key)}${escapeHtml(platform.label)}</span>${sub ? `<span class="sep"></span><span class="organize-row-where" title="${escapeHtml(folder)}">${sub}</span>` : ''}</div>
      </div>
      <button class="organize-row-size" ${folder ? `data-organize-path="${escapeHtml(folder)}"` : 'disabled'} title="See which folders take up the space">
        <span class="bar"><i style="width:${percent}%"></i></span>
        <span class="value">${sizeMarkup(size)}</span>
      </button>
      <div class="organize-row-actions">
        <button class="icon-button" data-organize-folder="${escapeHtml(folder)}" ${folder ? '' : 'disabled'} title="Open folder">${ICON.folder}</button>
        <button class="icon-button organize-danger" data-organize-uninstall title="${removeLabel}">${ICON.trash || ICON.close}</button>
      </div>
    </div>`;
}

function storageSection(key, title, icon, games, emptyText) {
  const query = normalize(state.search.trim());
  const list = query ? games.filter(game => normalize(game.Name).includes(query)) : games;
  const expanded = organize.expanded.has(key);
  const shown = expanded ? list : list.slice(0, STORAGE_PREVIEW_ROWS);
  const total = sharedSize(list);
  const maxBytes = Math.max(0, ...list.map(game => sizeOf(game).bytes));

  return `
    <section class="shelf organize-storage-section">
      <h2 class="shelf-title"><span class="organize-section-icon">${icon}</span>${title}<span class="count">${formatCount(list.length)}</span>${list.length ? `<span class="group-note"><span class="organize-section-total">${total.pending && !total.bytes ? '<span class="measuring"></span>' : formatBytes(total.bytes)}</span></span>` : ''}</h2>
      ${list.length ? `
        <div class="organize-rows">${shown.map(game => storageGameRow(game, maxBytes)).join('')}</div>
        ${list.length > STORAGE_PREVIEW_ROWS ? `<button class="link-button organize-more" data-organize-more="${key}">${expanded ? 'Show fewer' : `Show all ${formatCount(list.length)}`}</button>` : ''}`
        : `<div class="organize-empty-note">${emptyText}</div>`}
    </section>`;
}

function staleLabel(game) {
  const last = parseTime(game.LastPlayed);
  if (last) return Date.now() - last > STALE_AFTER ? `Played ${relativeTime(game.LastPlayed).toLowerCase()}` : null;
  const added = addedAt(game);
  if (added && Date.now() - added < STALE_AFTER) return null;
  return 'Never played';
}

function duplicateKey(name) {
  return normalize(name).replace(/[^\p{L}\p{N}]/gu, '');
}

function duplicateGroups(games) {
  const parent = new Map(games.map(game => [game.Id, game.Id]));
  const find = id => {
    while (parent.get(id) !== id) {
      parent.set(id, parent.get(parent.get(id)));
      id = parent.get(id);
    }
    return id;
  };
  const union = (first, second) => parent.set(find(first), find(second));

  const byName = new Map();
  const byApp = new Map();
  for (const game of games) {
    const key = duplicateKey(game.Name);
    if (key.length >= 2) {
      if (byName.has(key)) union(game.Id, byName.get(key));
      else byName.set(key, game.Id);
    }
    if (game.SteamAppId) {
      if (byApp.has(game.SteamAppId)) union(game.Id, byApp.get(game.SteamAppId));
      else byApp.set(game.SteamAppId, game.Id);
    }
  }

  const clusters = new Map();
  for (const game of games) {
    const root = find(game.Id);
    if (!clusters.has(root)) clusters.set(root, []);
    clusters.get(root).push(game);
  }

  const groups = [];
  for (const members of clusters.values()) {
    const byFolder = new Map();
    for (const game of members) {
      const folder = mainFolder(game);
      if (folder && !byFolder.has(pathKey(folder))) byFolder.set(pathKey(folder), game);
    }
    if (byFolder.size < 2) continue;
    const copies = [...byFolder.values()].sort((first, second) => parseTime(second.LastPlayed) - parseTime(first.LastPlayed) || (measuredBytes(second) || 0) - (measuredBytes(first) || 0));
    groups.push({ name: copies[0].Name, copies, keep: copies[0], size: sharedSize(copies) });
  }
  return groups.sort((first, second) => second.size.bytes - first.size.bytes);
}

function storageData() {
  const installed = state.games.filter(game => game.Status === 'installed');
  const leftovers = state.games.filter(game => game.Status !== 'installed');
  const bySize = list => [...list].sort((first, second) => (measuredBytes(second) ?? -1) - (measuredBytes(first) ?? -1));
  const stale = bySize(installed.filter(game => staleLabel(game)));
  const duplicates = duplicateGroups(installed);
  const extraCopies = duplicates.flatMap(group => group.copies.filter(game => game !== group.keep));
  const reclaimable = new Set([...leftovers, ...stale, ...extraCopies]);

  return {
    installed,
    biggest: bySize(installed).slice(0, 25),
    stale,
    leftovers: bySize(leftovers),
    duplicates,
    total: sharedSize(state.games),
    leftoverSize: sharedSize(leftovers),
    staleSize: sharedSize(stale),
    duplicateSize: sharedSize(extraCopies),
    reclaim: sharedSize([...reclaimable]),
  };
}

function driveCardsHtml() {
  const usage = driveUsage();
  if (!state.drives.length) return '';
  const cards = state.drives.map(drive => {
    const entry = usage.get(drive.root) || { bytes: 0, platforms: new Map(), pending: false };
    const used = drive.total - drive.free;
    const segments = [...entry.platforms.entries()].sort((first, second) => second[1] - first[1]);
    const other = Math.max(0, used - entry.bytes);
    const percent = bytes => (bytes / drive.total) * 100;
    const nameOf = key => (key === 'leftover' ? 'Leftovers' : (PLATFORMS.find(platform => platform.key === key) || { label: key }).label);
    const low = drive.free / drive.total < 0.1;

    return `
      <article class="organize-drive-card ${low ? 'low' : ''}">
        <header class="organize-drive-head">
          <span class="organize-drive-icon">${ICON.drive}</span>
          <div class="organize-drive-name">
            <strong>${escapeHtml(drive.root.replace('\\', ''))}</strong>
            <span>${escapeHtml(drive.label || 'Local Disk')}</span>
          </div>
          <div class="organize-drive-free-text"><strong>${formatBytes(drive.free)}</strong> free of ${formatBytes(drive.total)}</div>
        </header>
        <div class="organize-drive-stack">
          ${segments.map(([key, bytes]) => `<i style="width:${percent(bytes)}%;background:${PLATFORM_COLORS[key] || '#7b8cff'}" title="${escapeHtml(nameOf(key))} · ${formatBytes(bytes)}"></i>`).join('')}
          <i class="other" style="width:${percent(other)}%" title="Other files · ${formatBytes(other)}"></i>
        </div>
        <div class="organize-legend">
          ${segments.map(([key, bytes]) => `<span><i style="background:${PLATFORM_COLORS[key] || '#7b8cff'}"></i>${escapeHtml(nameOf(key))}<b>${formatBytes(bytes)}</b></span>`).join('')}
          <span><i class="other"></i>Other files<b>${formatBytes(other)}</b></span>
          ${entry.pending ? '<span class="organize-legend-pending"><span class="measuring"></span></span>' : ''}
        </div>
        <button class="link-button organize-drive-link" data-organize-drive="${escapeHtml(drive.root)}">${ICON.gamepad}Show games on ${escapeHtml(drive.root.replace('\\', ''))}</button>
      </article>`;
  }).join('');
  return `
    <section class="shelf">
      <h2 class="shelf-title"><span class="organize-section-icon">${ICON.drive}</span>Drives</h2>
      <div class="organize-drive-grid">${cards}</div>
    </section>`;
}

function duplicatesHtml(groups) {
  const query = normalize(state.search.trim());
  const list = query ? groups.filter(group => group.copies.some(game => normalize(game.Name).includes(query))) : groups;
  const body = list.length
    ? list.map(group => {
      const maxBytes = Math.max(0, ...group.copies.map(game => sizeOf(game).bytes));
      return `
        <div class="organize-duplicate">
          <div class="organize-duplicate-head">
            <strong>${escapeHtml(group.name)}</strong>
            <span>${group.copies.length} copies · ${group.size.pending && !group.size.bytes ? '<span class="measuring"></span>' : formatBytes(group.size.bytes)}</span>
          </div>
          <div class="organize-rows">${group.copies.map(game => storageGameRow(game, maxBytes, game === group.keep && game.LastPlayed ? '<span class="organize-tag">Played most recently</span>' : '')).join('')}</div>
        </div>`;
    }).join('')
    : '<div class="organize-empty-note">No game is installed in more than one place.</div>';

  return `
    <section class="shelf organize-storage-section">
      <h2 class="shelf-title"><span class="organize-section-icon">${ICON.copy}</span>Duplicates<span class="count">${formatCount(list.length)}</span></h2>
      ${body}
    </section>`;
}

function renderStorageView() {
  elements.pageTitle.textContent = 'Storage';
  elements.zoomControl.classList.add('hidden');
  state.visible = [];

  const data = storageData();
  const sizeText = size => (size.pending && !size.bytes ? '<span class="measuring"></span>' : formatBytes(size.bytes));
  elements.summary.innerHTML = `<strong>${sizeText(data.total)}</strong> of games on ${formatCount(state.drives.length)} ${state.drives.length === 1 ? 'drive' : 'drives'} · about <strong>${sizeText(data.reclaim)}</strong> could be freed`;
  elements.filters.innerHTML = `<button class="chip ${state.scan.running ? 'active' : ''}" data-organize-rescan>${ICON.ruler}${state.scan.running ? `Measuring ${state.scan.done}/${state.scan.total}` : 'Measure again'}</button>`;

  elements.results.innerHTML = `
    <div class="storage-view">
      <div class="stats organize-storage-stats">
        <div class="stat"><div class="stat-label">Could be freed</div><div class="stat-value">${sizeText(data.reclaim)}</div></div>
        <div class="stat"><div class="stat-label">Leftovers &amp; installers</div><div class="stat-value ${data.leftovers.length ? '' : 'muted'}">${data.leftovers.length ? sizeText(data.leftoverSize) : 'None'}</div></div>
        <div class="stat"><div class="stat-label">Unplayed 6+ months</div><div class="stat-value ${data.stale.length ? '' : 'muted'}">${data.stale.length ? sizeText(data.staleSize) : 'None'}</div></div>
        <div class="stat"><div class="stat-label">Extra copies</div><div class="stat-value ${data.duplicates.length ? '' : 'muted'}">${data.duplicates.length ? sizeText(data.duplicateSize) : 'None'}</div></div>
      </div>
      ${driveCardsHtml()}
      ${storageSection('biggest', 'Biggest games', ICON.ruler, data.biggest, 'No installed games yet.')}
      ${storageSection('stale', 'Not played in 6 months', ICON.clock, data.stale, 'Everything installed has been played recently.')}
      ${storageSection('leftovers', 'Installers &amp; leftovers', ICON.box, data.leftovers, 'No leftover folders or installers found.')}
      ${duplicatesHtml(data.duplicates)}
    </div>`;
}

VIEWS.storage = 'Storage';
extensions.views.storage = { render: renderStorageView };

elements.results.addEventListener('click', event => {
  const own = event.target.closest('[data-organize-uninstall], [data-organize-folder], [data-organize-path], [data-organize-more], [data-organize-drive]');
  if (!own || !elements.results.contains(own)) return;
  event.stopPropagation();

  const row = own.closest('[data-id]');
  const game = row && state.byId.get(row.dataset.id);
  if (own.dataset.organizeMore) {
    const key = own.dataset.organizeMore;
    if (organize.expanded.has(key)) organize.expanded.delete(key);
    else organize.expanded.add(key);
    renderStorageView();
  } else if (own.dataset.organizeDrive) {
    state.drive = own.dataset.organizeDrive;
    setView('all');
  } else if (own.hasAttribute('data-organize-uninstall')) {
    if (game && typeof openUninstall === 'function') openUninstall(game);
  } else if (own.dataset.organizeFolder) {
    send({ type: 'organizeOpenFolder', path: own.dataset.organizeFolder });
  } else if (own.dataset.organizePath) {
    openBreakdown(own.dataset.organizePath, game);
  }
}, true);

const breakdown = {
  stack: [],
  game: null,
  results: new Map(),
};

function requestBreakdown(path, force = false) {
  const key = pathKey(path);
  const existing = breakdown.results.get(key);
  if (existing && !force && !existing.error) return;
  breakdown.results.set(key, { loading: true, items: existing && !existing.error ? existing.items : [], done: 0, total: 0 });
  send({ type: 'organizeBreakdown', path, force });
}

function openBreakdown(path, game) {
  if (!path) return;
  breakdown.stack = [path];
  breakdown.game = game || null;
  modal.open('<div class="modal-body"><span class="measuring"></span></div>', 'organize-modal organize-breakdown-modal');
  modal.onClose = () => {
    breakdown.stack = [];
  };
  requestBreakdown(path);
  renderBreakdown();
}

function folderName(path) {
  const trimmed = String(path).replace(/\\+$/, '');
  return trimmed.split('\\').pop() || trimmed;
}

function renderBreakdown() {
  const panel = modal.panel;
  if (!panel || !breakdown.stack.length) return;
  const path = breakdown.stack[breakdown.stack.length - 1];
  const entry = breakdown.results.get(pathKey(path)) || { loading: true, items: [] };
  const items = entry.items || [];
  const total = items.reduce((sum, item) => sum + (item.pending ? 0 : item.bytes), 0);
  const maxBytes = Math.max(0, ...items.map(item => item.bytes));
  const folders = items.filter(item => !item.isFiles).length;

  const crumbs = breakdown.stack.map((crumb, index) => `
    ${index ? ICON.chevronRight : ''}
    <button class="organize-crumb ${index === breakdown.stack.length - 1 ? 'current' : ''}" data-breakdown-crumb="${index}">${escapeHtml(folderName(crumb))}</button>`).join('');

  let status = '';
  if (entry.error) status = `<span>${escapeHtml(entry.error)}</span>`;
  else if (!entry.complete) status = `<span>Measuring${entry.total ? ` ${entry.done} of ${entry.total} folders` : '…'}</span>`;
  else status = `<span>${formatCount(folders)} ${folders === 1 ? 'folder' : 'folders'}${entry.scannedAt ? ` · measured ${escapeHtml((relativeTime(entry.scannedAt) || '').toLowerCase())}` : ''}</span>`;

  const rows = items.map(item => {
    const percent = item.pending || !maxBytes ? 0 : Math.max(1, (item.bytes / maxBytes) * 100);
    const share = total && !item.pending ? Math.round((item.bytes / total) * 100) : null;
    return `
      <div class="organize-breakdown-row ${item.isFiles ? 'files' : ''}" ${item.isFiles ? '' : `data-breakdown-open="${escapeHtml(item.path)}"`}>
        <span class="organize-breakdown-icon">${item.isFiles ? ICON.file : ICON.folder}</span>
        <div class="organize-breakdown-name">
          <span title="${escapeHtml(item.name)}">${escapeHtml(item.name)}</span>
          <span class="bar"><i style="width:${percent}%"></i></span>
        </div>
        <span class="organize-breakdown-share">${share == null ? '' : `${share}%`}</span>
        <span class="organize-breakdown-size">${item.pending ? '<span class="measuring"></span>' : formatBytes(item.bytes)}</span>
        <button class="icon-button" data-breakdown-explorer="${escapeHtml(item.path)}" title="Open in Explorer">${ICON.external}</button>
        <span class="organize-breakdown-chevron">${item.isFiles ? '' : ICON.chevronRight}</span>
      </div>`;
  }).join('');

  const previousBody = panel.querySelector('.organize-breakdown-list');
  const previousScroll = previousBody && previousBody.dataset.path === path ? previousBody.scrollTop : 0;
  panel.innerHTML = `
    <header class="modal-head">
      <h2>${breakdown.game ? escapeHtml(breakdown.game.Name) : 'Folder breakdown'}</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="modal-body">
      <div class="organize-crumbs">${crumbs}</div>
      <div class="organize-breakdown-summary">
        <span class="organize-breakdown-total">${entry.complete || total ? formatBytes(total) : '<span class="measuring"></span>'}</span>
        ${status}
      </div>
      ${!entry.complete && entry.total ? `<div class="status-progress"><i style="width:${(entry.done / entry.total) * 100}%"></i></div>` : ''}
      <div class="organize-breakdown-list" data-path="${escapeHtml(path)}">${rows || (entry.complete ? '<div class="organize-empty-note">This folder is empty.</div>' : '')}</div>
    </div>
    <footer class="modal-foot">
      <span class="grow organize-breakdown-path" title="${escapeHtml(path)}">${escapeHtml(path)}</span>
      <button class="secondary-button compact" data-breakdown-refresh ${entry.complete || entry.error ? '' : 'disabled'}>${ICON.refresh}Measure again</button>
      <button class="secondary-button compact" data-breakdown-explorer="${escapeHtml(path)}">${ICON.folder}Open in Explorer</button>
    </footer>`;
  const list = panel.querySelector('.organize-breakdown-list');
  if (list && previousScroll) list.scrollTop = previousScroll;
}

modal.layer.addEventListener('click', event => {
  if (!breakdown.stack.length || !modal.layer.querySelector('.organize-breakdown-modal')) return;
  const explorer = event.target.closest('[data-breakdown-explorer]');
  if (explorer) {
    send({ type: 'organizeOpenFolder', path: explorer.dataset.breakdownExplorer });
    return;
  }
  const crumb = event.target.closest('[data-breakdown-crumb]');
  if (crumb) {
    breakdown.stack = breakdown.stack.slice(0, Number(crumb.dataset.breakdownCrumb) + 1);
    renderBreakdown();
    return;
  }
  if (event.target.closest('[data-breakdown-refresh]')) {
    requestBreakdown(breakdown.stack[breakdown.stack.length - 1], true);
    renderBreakdown();
    return;
  }
  const open = event.target.closest('[data-breakdown-open]');
  if (open) {
    breakdown.stack.push(open.dataset.breakdownOpen);
    requestBreakdown(open.dataset.breakdownOpen);
    renderBreakdown();
  }
});

function decorateSidebar() {
  const sections = [...elements.sidebar.querySelectorAll('.nav-section')];
  const storageSection = sections.find(section => {
    const heading = section.querySelector('.nav-heading');
    return heading && heading.textContent.trim() === 'Storage';
  }) || (elements.sidebar.querySelector('.drive') || { closest: () => null }).closest('.nav-section');

  if (storageSection && !storageSection.querySelector('[data-view="storage"]')) {
    const heading = storageSection.querySelector('.nav-heading');
    const html = `
      <button class="nav-item organize-storage-nav ${state.view === 'storage' ? 'active' : ''}" data-view="storage" title="Storage overview">
        <span class="nav-icon">${ICON.pie}</span>
        <span class="nav-label">Storage overview</span>
        <span class="nav-count"></span>
      </button>`;
    if (heading) heading.insertAdjacentHTML('afterend', html);
    else storageSection.insertAdjacentHTML('afterbegin', html);
  }

  for (const button of elements.sidebar.querySelectorAll('[data-platform]')) {
    if (button.querySelector('.organize-nav-size')) continue;
    const platform = PLATFORMS.find(entry => entry.key === button.dataset.platform);
    const label = button.querySelector('.nav-label');
    if (!platform || !label) continue;
    const games = state.games.filter(game => game.Status === 'installed' && platform.sources.includes(game.Source));
    const size = sharedSize(games);
    if (!size.bytes) continue;
    label.classList.add('organize-two-line');
    label.innerHTML = `<span class="organize-nav-name">${escapeHtml(platform.label)}</span><span class="organize-nav-size">${formatBytes(size.bytes)}${size.pending ? '+' : ''}</span>`;
  }
}

new MutationObserver(decorateSidebar).observe(elements.sidebar, { childList: true });

function viewKey() {
  return [state.view, state.platform || '', state.drive || ''].join('|');
}

function memoryFor(key) {
  if (!organize.memory[key]) organize.memory[key] = {};
  return organize.memory[key];
}

function saveMemory() {
  const keys = Object.keys(organize.memory);
  if (keys.length > 60) {
    for (const key of keys.slice(0, keys.length - 60)) delete organize.memory[key];
  }
  writeStorage('organize.viewMemory', organize.memory);
}

function moveViewMemory(fromView, toView) {
  for (const key of Object.keys(organize.memory)) {
    if (key.split('|')[0] === fromView) {
      organize.memory[[toView, ...key.split('|').slice(1)].join('|')] = organize.memory[key];
      delete organize.memory[key];
    }
  }
  saveMemory();
}

function rememberSelection(id) {
  if (!organize.currentKey) return;
  const entry = memoryFor(organize.currentKey);
  if (entry.selected === id) return;
  entry.selected = id;
  saveMemory();
}

let scrollSaveTimer = null;
elements.scroller.addEventListener('scroll', () => {
  if (organize.restoring || !organize.currentKey) return;
  clearTimeout(scrollSaveTimer);
  const key = organize.currentKey;
  scrollSaveTimer = setTimeout(() => {
    if (key !== organize.currentKey || organize.restoring) return;
    memoryFor(key).scroll = Math.round(elements.scroller.scrollTop);
    saveMemory();
  }, 180);
}, { passive: true });

elements.results.addEventListener('focusin', event => {
  const item = event.target.closest('[data-id]');
  if (item && state.byId.has(item.dataset.id)) rememberSelection(item.dataset.id);
});

function restoreViewMemory(key) {
  const saved = organize.memory[key] || {};
  organize.restoring = true;
  clearTimeout(scrollSaveTimer);
  requestAnimationFrame(() => {
    if (key !== organize.currentKey) return;
    elements.scroller.scrollTo({ top: saved.scroll || 0, behavior: 'instant' });
    if (saved.selected && !state.selectedId) {
      const item = elements.results.querySelector(`[data-id="${CSS.escape(saved.selected)}"]`);
      if (item) {
        item.classList.add('organize-remembered');
        if (item.tabIndex >= 0) item.focus({ preventScroll: true });
        setTimeout(() => item.classList.remove('organize-remembered'), 1600);
      }
    }
    setTimeout(() => {
      organize.restoring = false;
    }, 120);
  });
}

function knownView(view) {
  if (!view) return false;
  if (view.startsWith('col:')) return collectionList().some(entry => collectionViewKey(entry.name) === view);
  return Object.prototype.hasOwnProperty.call(VIEWS, view) || !!extensions.views[view];
}

extensions.libraryHandlers.push(() => {
  if (organize.restoredView) return;
  organize.restoredView = true;
  const last = readStorage('organize.lastView', null);
  if (!last || !knownView(last.view)) return;
  state.view = last.view;
  if (last.platform && PLATFORMS.some(platform => platform.key === last.platform)) state.platform = last.platform;
  if (last.drive) state.drive = last.drive;
});

extensions.afterRender.push(() => {
  renderGroupControl();
  if (!state.loaded) return;
  const key = viewKey();
  if (key === organize.currentKey) return;
  organize.currentKey = key;
  writeStorage('organize.lastView', { view: state.view, platform: state.platform, drive: state.drive });
  restoreViewMemory(key);
});

function organizeToggle(key, label, fallback) {
  const on = setting(key, fallback);
  return `
    <label class="organize-setting">
      <span class="organize-setting-text">${label}</span>
      <input type="checkbox" class="organize-switch" data-organize-setting="${key}" data-default="${fallback}" ${on ? 'checked' : ''}>
    </label>`;
}

extensions.settingsSections.push({
  id: 'organize',
  title: 'Library organizing',
  render: () => `
    <div class="organize-settings">
      ${organizeToggle('organize.pinFavorites', 'Pin favorites to the top of All games', false)}
      ${organizeToggle('organize.statusBadges', 'Show play status on game covers', true)}
      ${organizeToggle('organize.newBadges', 'Mark games added in the last 7 days as New', true)}
      <label class="organize-setting">
        <span class="organize-setting-text">Second line under game titles</span>
        <select class="organize-select" data-organize-setting="organize.cardMeta">
          ${CARD_META_OPTIONS.map(option => `<option value="${option.key}" ${setting('organize.cardMeta', 'auto') === option.key ? 'selected' : ''}>${option.label}</option>`).join('')}
        </select>
      </label>
    </div>`,
  bind(root) {
    if (!root || root.dataset.organizeBound) return;
    root.dataset.organizeBound = 'true';
    root.addEventListener('change', event => {
      const input = event.target.closest('[data-organize-setting]');
      if (!input) return;
      const key = input.dataset.organizeSetting;
      if (input.type === 'checkbox') {
        const fallback = input.dataset.default === 'true';
        setSetting(key, input.checked === fallback ? null : input.checked);
      } else {
        setSetting(key, input.value === 'auto' ? null : input.value);
      }
      scheduleRender(60);
    });
  },
});

extensions.messageHandlers.push(message => {
  if (message.type === 'organizeBreakdown') {
    const key = pathKey(message.path || '');
    if (message.error) breakdown.results.set(key, { error: message.error, items: [], complete: true });
    else breakdown.results.set(key, { items: message.items || [], done: message.done, total: message.total, complete: !!message.complete, scannedAt: message.scannedAt });
    const current = breakdown.stack[breakdown.stack.length - 1];
    if (current && pathKey(current) === key) renderBreakdown();
  } else if (message.type === 'scan' && state.view === 'storage') {
    const chip = elements.filters.querySelector('[data-organize-rescan]');
    if (chip) chip.innerHTML = `${ICON.ruler}${state.scan.running ? `Measuring ${state.scan.done}/${state.scan.total}` : 'Measure again'}`;
  }
});

mountGroupControl();
