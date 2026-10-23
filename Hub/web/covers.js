'use strict';

ICON.image = '<svg viewBox="0 0 24 24" class="glyph"><rect x="3.5" y="4.5" width="17" height="15" rx="2.5"/><circle cx="9" cy="10" r="1.8"/><path d="m4 17.5 5-4.5 3.5 3 3-2.5 4.5 4"/></svg>';
ICON.upload = '<svg viewBox="0 0 24 24" class="glyph"><path d="M12 15V4m0 0L7.5 8.5M12 4l4.5 4.5M4.5 15.5V18a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2v-2.5"/></svg>';
ICON.link = '<svg viewBox="0 0 24 24" class="glyph"><path d="M10 14a4 4 0 0 0 5.66 0l3-3a4 4 0 0 0-5.66-5.66l-1 1"/><path d="M14 10a4 4 0 0 0-5.66 0l-3 3a4 4 0 0 0 5.66 5.66l1-1"/></svg>';
ICON.undo = '<svg viewBox="0 0 24 24" class="glyph"><path d="M9 14 4 9l5-5"/><path d="M4 9h10.5a5.5 5.5 0 0 1 0 11H11"/></svg>';
ICON.key = '<svg viewBox="0 0 24 24" class="glyph"><circle cx="8" cy="15" r="4"/><path d="m10.85 12.15 8.65-8.65M17 6l2.5 2.5M14.5 8.5 17 11"/></svg>';

const modal = {
  layer: document.getElementById('modalLayer'),
  onClose: null,

  open(html, className = '') {
    this.layer.innerHTML = `<div class="modal-backdrop" data-modal-close></div><div class="modal ${className}" role="dialog">${html}</div>`;
    requestAnimationFrame(() => this.layer.classList.add('open'));
  },

  close() {
    if (!this.isOpen) return;
    this.layer.classList.remove('open');
    const callback = this.onClose;
    this.onClose = null;
    setTimeout(() => {
      if (!this.layer.classList.contains('open')) this.layer.innerHTML = '';
    }, 220);
    if (callback) callback();
  },

  get isOpen() {
    return this.layer.classList.contains('open');
  },

  get panel() {
    return this.layer.querySelector('.modal');
  },
};

modal.layer.addEventListener('click', event => {
  if (event.target.closest('[data-modal-close]')) modal.close();
});

document.addEventListener('keydown', event => {
  if (!modal.layer.classList.contains('open')) return;
  if (event.key === 'Escape') {
    event.preventDefault();
    event.stopImmediatePropagation();
    modal.close();
  } else if (event.target.tagName !== 'INPUT') {
    event.stopImmediatePropagation();
  }
}, true);

const ART_KINDS = [
  { key: 'cover', label: 'Cover', field: 'CoverUrl' },
  { key: 'hero', label: 'Background', field: 'HeroUrl' },
  { key: 'logo', label: 'Logo', field: 'LogoUrl' },
  { key: 'icon', label: 'Icon', field: 'IconUrl' },
];

function sendArt(type, payload) {
  if (payload.kind === 'icon') send({ ...payload, type: 'iconArt', action: type });
  else send({ ...payload, type });
}

const artPicker = {
  game: null,
  kind: 'cover',
  query: '',
  requestId: null,
  loading: false,
  candidates: [],
  errors: [],
  hasGridKey: false,
  showKeyForm: false,
  busyUrl: null,
};

function openArtPicker(game, kind = 'cover') {
  Object.assign(artPicker, {
    game,
    kind,
    query: game.Name,
    candidates: [],
    errors: [],
    loading: true,
    showKeyForm: false,
    busyUrl: null,
    hasGridKey: !!state.hasGridKey,
  });
  modal.open('', 'art-modal');
  renderArtPicker();
  searchArt();
}

function searchArt() {
  artPicker.requestId = String(Date.now());
  artPicker.loading = true;
  artPicker.candidates = [];
  artPicker.errors = [];
  renderArtGrid();
  send({ type: 'findArt', id: artPicker.game.Id, query: artPicker.query, requestId: artPicker.requestId });
}

function currentArt(game, kind) {
  const entry = ART_KINDS.find(item => item.key === kind);
  return game[entry.field] || null;
}

function renderArtPicker() {
  const panel = modal.panel;
  if (!panel) return;
  const game = artPicker.game;
  const custom = (game.CustomArt || []).includes(artPicker.kind);

  panel.innerHTML = `
    <header class="modal-head">
      <h2>Artwork for ${escapeHtml(game.Name)}</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>

    <div class="art-toolbar">
      <div class="segmented art-kinds">
        ${ART_KINDS.map(kind => `<button class="${artPicker.kind === kind.key ? 'active' : ''}" data-art-kind="${kind.key}">${kind.label}</button>`).join('')}
      </div>
      <form class="art-search" data-art-search>
        ${ICON.search}
        <input name="query" type="text" value="${escapeHtml(artPicker.query)}" placeholder="Search by game name" autocomplete="off" spellcheck="false">
        <button class="text-button" type="submit">Search</button>
      </form>
    </div>

    <div class="art-scroll">
      <div class="art-grid kind-${artPicker.kind}" id="artGrid"></div>
    </div>

    <footer class="modal-foot art-foot">
      <button class="secondary-button compact" data-art-file>${ICON.upload}Image file…</button>
      <form class="art-link" data-art-link>
        ${ICON.link}
        <input name="url" type="text" placeholder="Paste an image link" autocomplete="off" spellcheck="false">
        <button class="text-button" type="submit">Use</button>
      </form>
      ${custom ? `<button class="secondary-button compact" data-art-reset>${ICON.undo}Use automatic art</button>` : ''}
      <div class="grid-key">${gridKeyHtml()}</div>
    </footer>`;

  renderArtGrid();
}

function gridKeyHtml() {
  if (artPicker.showKeyForm) {
    return `
      <form class="grid-key-form" data-grid-key>
        <input name="key" type="password" placeholder="SteamGridDB API key" autocomplete="off">
        <button class="text-button" type="submit">Save</button>
      </form>`;
  }
  return artPicker.hasGridKey
    ? `<button class="link-button" data-grid-toggle title="Change or remove the SteamGridDB key">${ICON.key}SteamGridDB on</button>`
    : `<button class="link-button" data-grid-toggle title="Free key from steamgriddb.com → Preferences → API">${ICON.key}Add SteamGridDB for more art</button>`;
}

function renderArtGrid() {
  const grid = document.getElementById('artGrid');
  if (!grid) return;
  grid.className = `art-grid kind-${artPicker.kind}`;

  const current = currentArt(artPicker.game, artPicker.kind);
  const currentTile = current
    ? `<div class="art-tile current" title="Current ${artPicker.kind === 'hero' ? 'background' : artPicker.kind}">
         <div class="art-image"><img src="${escapeHtml(current)}" alt=""></div>
         <div class="art-caption"><span class="art-source">Current</span><span class="art-label">${(artPicker.game.CustomArt || []).includes(artPicker.kind) ? 'Your choice' : 'Automatic'}</span></div>
       </div>`
    : '';

  if (artPicker.loading) {
    grid.innerHTML = currentTile + Array.from({ length: 8 }, () => '<div class="art-tile skeleton"><div class="art-image"></div><div class="art-caption"><span class="measuring"></span></div></div>').join('');
    return;
  }

  const candidates = artPicker.candidates.filter(candidate => candidate.Kind === artPicker.kind);
  const tiles = candidates.map((candidate, index) => `
    <button class="art-tile ${artPicker.busyUrl === candidate.Url ? 'busy' : ''}" data-art-index="${artPicker.candidates.indexOf(candidate)}" style="animation-delay:${Math.min(index, 20) * 18}ms">
      <div class="art-image"><img src="${escapeHtml(candidate.Thumb)}" loading="lazy" decoding="async" alt="" data-art-thumb></div>
      <div class="art-caption">
        <span class="art-source">${escapeHtml(candidate.Source)}</span>
        <span class="art-label" title="${escapeHtml(candidate.Label || '')}">${escapeHtml(candidate.Label || '')}</span>
      </div>
      <span class="art-spinner">${ICON.refresh}</span>
    </button>`).join('');

  const errors = artPicker.errors.length
    ? `<div class="art-errors">${ICON.alert}<span>${artPicker.errors.map(escapeHtml).join(' · ')}</span></div>`
    : '';
  const empty = candidates.length
    ? ''
    : `<div class="art-empty">${ICON.search}<strong>No ${artPicker.kind === 'hero' ? 'backgrounds' : artPicker.kind + 's'} found</strong><span>Try a shorter or different name${artPicker.hasGridKey ? '' : ', or add a SteamGridDB key for community art'}.</span></div>`;

  grid.innerHTML = errors + currentTile + tiles + empty;
}

function chooseArt(candidate) {
  artPicker.busyUrl = candidate.Url;
  renderArtGrid();
  sendArt('setArt', { id: artPicker.game.Id, kind: artPicker.kind, url: candidate.Url });
}

modal.layer.addEventListener('click', event => {
  if (!modal.panel || !modal.panel.classList.contains('art-modal')) return;

  const kind = event.target.closest('[data-art-kind]');
  const tile = event.target.closest('[data-art-index]');
  if (kind) {
    artPicker.kind = kind.dataset.artKind;
    renderArtPicker();
  } else if (tile && !artPicker.busyUrl) {
    chooseArt(artPicker.candidates[Number(tile.dataset.artIndex)]);
  } else if (event.target.closest('[data-art-file]')) {
    sendArt('setArtFile', { id: artPicker.game.Id, kind: artPicker.kind });
  } else if (event.target.closest('[data-art-reset]')) {
    sendArt('resetArt', { id: artPicker.game.Id, kind: artPicker.kind });
  } else if (event.target.closest('[data-grid-toggle]')) {
    artPicker.showKeyForm = true;
    modal.panel.querySelector('.grid-key').innerHTML = gridKeyHtml();
    modal.panel.querySelector('.grid-key input').focus();
  }
});

modal.layer.addEventListener('submit', event => {
  event.preventDefault();
  const form = event.target;
  if (form.matches('[data-art-search]')) {
    artPicker.query = form.elements.query.value.trim() || artPicker.game.Name;
    searchArt();
  } else if (form.matches('[data-art-link]')) {
    const url = form.elements.url.value.trim();
    if (!/^https?:\/\//i.test(url)) {
      toast('Paste a link that starts with http:// or https://', 'error');
      return;
    }
    artPicker.busyUrl = url;
    sendArt('setArt', { id: artPicker.game.Id, kind: artPicker.kind, url });
    toast('Downloading image…', 'info', ICON.image);
  } else if (form.matches('[data-grid-key]')) {
    send({ type: 'setGridKey', value: form.elements.key.value });
  }
});

modal.layer.addEventListener('error', event => {
  if (event.target.matches && event.target.matches('[data-art-thumb]')) {
    const tile = event.target.closest('.art-tile');
    if (tile) tile.remove();
  }
}, true);

extensions.messageHandlers.push(message => {
  switch (message.type) {
    case 'artCandidates':
      if (!artPicker.game || message.requestId !== artPicker.requestId) return;
      artPicker.loading = false;
      artPicker.candidates = message.candidates || [];
      artPicker.errors = message.errors || [];
      artPicker.hasGridKey = !!message.hasGridKey;
      artPicker.game.CustomArt = message.custom || [];
      renderArtGrid();
      break;

    case 'artSet': {
      const wasBusy = artPicker.busyUrl;
      artPicker.busyUrl = null;
      if (message.cancelled) break;
      if (message.ok) {
        const label = (ART_KINDS.find(kind => kind.key === message.kind) || ART_KINDS[0]).label;
        toast(message.kind ? `${label} updated` : 'Artwork reset', 'info', ICON.check);
      } else {
        toast(message.error || 'That image could not be used.', 'error');
      }
      if (artPicker.game && artPicker.game.Id === message.id && modal.panel && modal.panel.classList.contains('art-modal')) {
        renderArtPicker();
      } else if (wasBusy) {
        renderArtGrid();
      }
      break;
    }

    case 'gridKey':
      state.hasGridKey = message.hasKey;
      artPicker.hasGridKey = message.hasKey;
      artPicker.showKeyForm = false;
      toast(message.hasKey ? 'SteamGridDB key saved' : 'SteamGridDB key removed', 'info', ICON.key);
      if (artPicker.game && modal.panel) {
        renderArtPicker();
        searchArt();
      }
      break;
  }
});

extensions.messageHandlers.push(message => {
  if (message.type === 'art' && artPicker.game && artPicker.game.Id === message.id && modal.panel && modal.panel.classList.contains('art-modal')) {
    renderArtGrid();
  }
});

extensions.heroButtons.push(game => `<button class="hero-art-button" data-action="change-art" title="Change cover, background, logo or icon">${ICON.image}Change art</button>`);
extensions.drawerActions['change-art'] = game => openArtPicker(game);
extensions.menuItems.push(game => `<button class="menu-item" data-menu="change-art">${ICON.image}Change artwork…</button>`);
extensions.menuActions['change-art'] = game => openArtPicker(game);
