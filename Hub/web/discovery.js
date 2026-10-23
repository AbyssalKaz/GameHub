'use strict';

ICON.discPlus = '<svg viewBox="0 0 24 24" class="glyph"><rect x="3.5" y="3.5" width="17" height="17" rx="4"/><path d="M12 8v8M8 12h8"/></svg>';
ICON.discPencil = '<svg viewBox="0 0 24 24" class="glyph"><path d="M4 20h4L19 9a2.83 2.83 0 0 0-4-4L4 16z"/><path d="m13.5 6.5 4 4"/></svg>';
ICON.discMerge = '<svg viewBox="0 0 24 24" class="glyph"><path d="M6 4v4a4 4 0 0 0 4 4h4a4 4 0 0 1 4 4v4"/><path d="M18 4v4a4 4 0 0 1-4 4"/><path d="m15 17 3 3 3-3"/></svg>';
ICON.discSplit = '<svg viewBox="0 0 24 24" class="glyph"><path d="M12 20v-6a4 4 0 0 0-4-4H5"/><path d="M12 14a4 4 0 0 1 4-4h3"/><path d="m8 7-3 3 3 3M16 7l3 3-3 3"/></svg>';
ICON.discEye = '<svg viewBox="0 0 24 24" class="glyph"><path d="M2.5 12C4 9.5 7.5 6 12 6s8 3.5 9.5 6c-1.5 2.5-5 6-9.5 6s-8-3.5-9.5-6z"/><circle cx="12" cy="12" r="2.8"/></svg>';
ICON.discBag = '<svg viewBox="0 0 24 24" class="glyph"><path d="M5 8h14l-1.2 11a2 2 0 0 1-2 1.8H8.2a2 2 0 0 1-2-1.8z"/><path d="M9 10V7a3 3 0 0 1 6 0v3"/></svg>';
ICON.discChip = '<svg viewBox="0 0 24 24" class="glyph"><rect x="5" y="3.5" width="14" height="17" rx="2"/><path d="M8 3.5v4h8v-4M9 14h6M9 17h4"/></svg>';
ICON.discUpdate = '<svg viewBox="0 0 24 24" class="glyph"><path d="M12 3.5v10m0 0-4-4m4 4 4-4"/><path d="M4.5 14.5V18a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2v-3.5"/></svg>';
ICON.discShortcut = '<svg viewBox="0 0 24 24" class="glyph"><rect x="3.5" y="3.5" width="17" height="17" rx="3"/><path d="M9.5 14.5 15 9M10.5 9H15v4.5"/></svg>';

const DISCOVERY_PLATFORMS = [
  { key: 'gog', label: 'GOG', sources: ['GOG'], launcher: 'GOG Galaxy', icon: 'discBag' },
  { key: 'ubisoft', label: 'Ubisoft Connect', sources: ['Ubisoft'], launcher: 'Ubisoft Connect', icon: 'discBag' },
  { key: 'battlenet', label: 'Battle.net', sources: ['BattleNet'], launcher: 'Battle.net', icon: 'discBag' },
  { key: 'itch', label: 'itch.io', sources: ['Itch'], launcher: 'itch', icon: 'discBag' },
  { key: 'amazon', label: 'Amazon Games', sources: ['Amazon'], launcher: 'Amazon Games', icon: 'discBag' },
  { key: 'manual', label: 'Added by you', sources: ['Manual'], icon: 'discPlus' },
];

const DOWNLOAD_STATES = { downloading: 'Downloading', installing: 'Installing', paused: 'Paused', queued: 'Queued', extracting: 'Extracting', error: 'Failed' };

const discovery = {
  data: {},
  requests: new Map(),
  requestCount: 0,
  shortcuts: null,
  steam: null,
  emulators: null,
  emulatorsDirty: false,
  merge: null,
  editor: null,
};

(function installPlatforms() {
  const steam = PLATFORMS.find(platform => platform.key === 'steam');
  if (steam && !steam.sources.includes('SteamShortcut')) steam.sources.push('SteamShortcut');
  const localIndex = PLATFORMS.findIndex(platform => platform.key === 'local');
  const additions = DISCOVERY_PLATFORMS.filter(entry => !PLATFORMS.some(platform => platform.key === entry.key));
  PLATFORMS.splice(localIndex < 0 ? PLATFORMS.length : localIndex, 0, ...additions);
})();

const basePlatformIcon = platformIcon;
platformIcon = key => {
  if (state.platformIcons[key]) return basePlatformIcon(key);
  const entry = PLATFORMS.find(platform => platform.key === key);
  if (entry && entry.icon && ICON[entry.icon]) return ICON[entry.icon];
  return basePlatformIcon(key);
};

function nameKey(name) {
  return String(name ?? '').toLowerCase().replace(/[^\p{L}\p{Nd}]/gu, '');
}

function discRequest(message, callback) {
  const requestId = `discovery-${Date.now()}-${++discovery.requestCount}`;
  discovery.requests.set(requestId, callback);
  send(Object.assign({}, message, { requestId }));
}

function resolveRequest(message) {
  const callback = discovery.requests.get(message.requestId);
  if (!callback) return false;
  discovery.requests.delete(message.requestId);
  callback(message);
  return true;
}

function syncEmulatorPlatforms() {
  const sources = new Set(state.games.map(game => game.Source).filter(source => source && source.startsWith('Emulator:')));
  for (let index = PLATFORMS.length - 1; index >= 0; index--) {
    const platform = PLATFORMS[index];
    if (platform.emulator && !sources.has(platform.sources[0])) PLATFORMS.splice(index, 1);
  }
  const localIndex = PLATFORMS.findIndex(platform => platform.key === 'local');
  for (const source of [...sources].sort()) {
    if (PLATFORMS.some(platform => platform.sources.includes(source))) continue;
    const name = source.slice('Emulator:'.length);
    const entry = { key: `emulator-${nameKey(name)}`, label: name, sources: [source], launcher: name, icon: 'discChip', emulator: true };
    PLATFORMS.splice(localIndex < 0 ? PLATFORMS.length : localIndex, 0, entry);
  }
}

function originalOf(game) {
  return (discovery.data.originals || {})[game.Id] || null;
}

function isManual(game) {
  return game.Source === 'Manual' || String(game.Id || '').startsWith('manual:');
}

function mergesInto(game) {
  return (discovery.data.merges || []).filter(merge => merge.keep === game.Id);
}

function percentOf(download) {
  return download && download.Progress != null ? Math.round(download.Progress * 1000) / 10 : null;
}

function downloadLabel(game) {
  const download = game.Download;
  if (!download) return '';
  const label = DOWNLOAD_STATES[download.State] || 'Downloading';
  if (game.Status === 'installed' && (download.State === 'downloading' || download.State === 'queued')) return download.State === 'queued' ? 'Update queued' : 'Updating';
  return label;
}

function downloadBadgeHtml(game) {
  const download = game.Download;
  if (download) {
    const percent = percentOf(download);
    const paused = download.State === 'paused' || download.State === 'queued';
    return `
      <span class="disc-card-download ${paused ? 'paused' : ''}" data-disc-badge title="${escapeHtml(downloadLabel(game))}${percent != null ? ` · ${percent}%` : ''}">${percent != null ? `${Math.floor(percent)}%` : escapeHtml(downloadLabel(game))}</span>
      <span class="disc-card-bar ${paused ? 'paused' : ''}" data-disc-badge><i style="width:${percent ?? 0}%"></i></span>`;
  }
  if (game.NeedsUpdate && game.Status === 'installed') {
    return `<span class="disc-card-update" data-disc-badge title="An update is waiting">${ICON.discUpdate}Update</span>`;
  }
  return '';
}

extensions.cardBadges.push(game => downloadBadgeHtml(game));

function downloadBlockHtml(game) {
  const download = game.Download;
  if (download) {
    const percent = percentOf(download);
    const paused = download.State === 'paused' || download.State === 'queued';
    const bytes = download.BytesTotal ? `${formatBytes(download.BytesDone || 0)} of ${formatBytes(download.BytesTotal)}` : '';
    return `
      <div class="disc-download ${paused ? 'paused' : ''}">
        <div class="disc-download-top">
          <span class="disc-download-state">${ICON.download}${escapeHtml(downloadLabel(game))}</span>
          <span class="disc-download-bytes">${escapeHtml(bytes)}</span>
          <span class="disc-download-percent">${percent != null ? `${percent}%` : ''}</span>
        </div>
        <div class="disc-download-bar"><i style="width:${percent ?? 0}%"></i></div>
        ${game.LauncherPage ? `<button class="link-button" data-action="open-launcher">${ICON.external}Open in ${escapeHtml(platformOf(game).launcher || platformOf(game).label)}</button>` : ''}
      </div>`;
  }
  if (game.NeedsUpdate && game.Status === 'installed') {
    return `
      <div class="callout disc-update-callout">${ICON.discUpdate}<div><strong>Update waiting.</strong> ${escapeHtml(platformOf(game).launcher || platformOf(game).label)} has an update for this game that hasn't been installed yet.</div>
        ${game.LauncherPage ? `<button class="link-button" data-action="open-launcher">${ICON.external}Open</button>` : ''}
      </div>`;
  }
  return '';
}

extensions.drawerSections.push(game => {
  const merges = mergesInto(game);
  const mergedRows = merges.map(merge => `
    <div class="disc-merged-row">
      <span class="disc-merged-icon">${platformIcon((PLATFORMS.find(platform => platform.sources.includes(merge.absorbSource)) || { key: 'local' }).key)}</span>
      <span class="disc-merged-text">
        <span class="disc-merged-name">${escapeHtml(merge.absorbName || merge.absorb)}</span>
        <span class="disc-merged-path mono">${escapeHtml(merge.absorbPath || (merge.missing ? 'Not found right now' : ''))}</span>
      </span>
      <button class="secondary-button compact" data-action="discovery-split" data-absorb="${escapeHtml(merge.absorb)}">${ICON.discSplit}Split</button>
    </div>`).join('');

  return `
    <div data-disc-download>${downloadBlockHtml(game)}</div>
    ${merges.length ? `
      <div class="disc-merged">
        <h3 class="section-title">Merged entries</h3>
        ${mergedRows}
      </div>` : ''}
    <div class="disc-entry">
      <h3 class="section-title">Library entry</h3>
      <div class="disc-entry-actions">
        <button class="secondary-button compact" data-action="discovery-edit">${ICON.discPencil}Edit details</button>
        <button class="secondary-button compact" data-action="discovery-merge">${ICON.discMerge}Merge with…</button>
        ${isManual(game)
          ? `<button class="secondary-button compact" data-action="discovery-remove-manual">${ICON.close}Remove from library</button>`
          : `<button class="secondary-button compact" data-action="discovery-hide">${ICON.eyeOff}Hide</button>`}
      </div>
    </div>`;
});

extensions.drawerActions['discovery-edit'] = game => openEditor(game);
extensions.drawerActions['discovery-merge'] = game => openMerge(game);
extensions.drawerActions['discovery-hide'] = game => hideGame(game);
extensions.drawerActions['discovery-remove-manual'] = game => removeManual(game);
extensions.drawerActions['discovery-split'] = (game, element) => {
  send({ type: 'discovery.unmerge', keep: game.Id, absorb: element.dataset.absorb });
};

extensions.menuItems.push(game => `
  <div class="menu-separator"></div>
  <button class="menu-item" data-menu="discovery-edit">${ICON.discPencil}Edit details…</button>
  <button class="menu-item" data-menu="discovery-merge">${ICON.discMerge}Merge with…</button>
  ${isManual(game)
    ? `<button class="menu-item" data-menu="discovery-remove-manual">${ICON.close}Remove from library</button>`
    : `<button class="menu-item" data-menu="discovery-hide">${ICON.eyeOff}Hide from library</button>`}`);

extensions.menuActions['discovery-edit'] = game => openEditor(game);
extensions.menuActions['discovery-merge'] = game => openMerge(game);
extensions.menuActions['discovery-hide'] = game => hideGame(game);
extensions.menuActions['discovery-remove-manual'] = game => removeManual(game);

function hideGame(game) {
  if (state.selectedId === game.Id) closeDrawer();
  send({ type: 'discovery.hide', id: game.Id });
}

function removeManual(game) {
  modal.open(`
    <header class="modal-head">
      <h2>Remove ${escapeHtml(game.Name)}?</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="modal-body">
      <p class="disc-text">The game leaves your library and its Start Menu shortcut is removed on the next refresh. Its files stay where they are.</p>
    </div>
    <footer class="modal-foot">
      <span class="grow"></span>
      <button class="secondary-button compact" data-modal-close>Cancel</button>
      <button class="primary-button compact" data-disc-confirm-remove>Remove</button>
    </footer>`, 'disc-modal small');
  modal.panel.querySelector('[data-disc-confirm-remove]').addEventListener('click', () => {
    if (state.selectedId === game.Id) closeDrawer();
    send({ type: 'discovery.removeManual', id: game.Id });
    modal.close();
  });
}

extensions.sidebarSections.unshift(() => `
  <div class="nav-section disc-nav">
    <button class="nav-item" data-disc-add title="Add a game the library didn't find">
      <span class="nav-icon">${ICON.discPlus}</span>
      <span class="nav-label">Add a game</span>
      <span class="nav-count"></span>
    </button>
  </div>`);

elements.sidebar.addEventListener('click', event => {
  if (event.target.closest('[data-disc-add]')) openEditor(null);
});

function openEditor(game) {
  const manual = !game || isManual(game);
  const original = game ? originalOf(game) : null;
  const override = game ? (discovery.data.overrides || {})[game.Id] || null : null;
  discovery.editor = {
    game,
    manual,
    path: game ? (game.Url && !isManual(game) ? '' : game.Exe || '') : '',
    name: game ? game.Name : '',
    arguments: game ? game.Arguments || '' : '',
    suggestedName: '',
    known: null,
    original,
    override,
  };
  modal.open('', 'disc-modal');
  modal.onClose = () => {
    discovery.editor = null;
  };
  renderEditor();
  const first = modal.panel.querySelector(game ? '[data-disc-field="name"]' : '[data-disc-browse]');
  if (first) first.focus();
}

function renderEditor() {
  const panel = modal.panel;
  const editor = discovery.editor;
  if (!panel || !editor) return;
  const game = editor.game;
  const original = editor.original || (game ? { Name: game.Name, Exe: game.Exe, Arguments: game.Arguments, Url: game.Url } : null);
  const usesUrl = game && !editor.manual && (original ? original.Url : game.Url);
  const title = game ? `Edit ${game.Name}` : 'Add a game';

  panel.innerHTML = `
    <header class="modal-head">
      <h2>${escapeHtml(title)}</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <form class="modal-body disc-form" data-disc-editor autocomplete="off">
      <label class="disc-field">
        <span class="disc-label">Program</span>
        <span class="disc-path-row">
          <input class="disc-input mono" data-disc-field="path" type="text" spellcheck="false" value="${escapeHtml(editor.path)}" placeholder="${escapeHtml(usesUrl ? `Launches through ${platformOf(game).launcher || platformOf(game).label}` : 'C:\\Games\\Example\\Game.exe')}">
          <button class="secondary-button compact" type="button" data-disc-browse>${ICON.folder}Browse…</button>
        </span>
        ${editor.known ? `<span class="disc-note warn">${ICON.alert}Already in your library as ${escapeHtml(editor.known)}.</span>` : ''}
        ${usesUrl ? `<span class="disc-hint">Pick a program only if you want to skip the launcher. Leave it empty to keep launching through ${escapeHtml(platformOf(game).launcher || platformOf(game).label)}.</span>` : ''}
      </label>
      <label class="disc-field">
        <span class="disc-label">Name</span>
        <input class="disc-input" data-disc-field="name" type="text" spellcheck="false" value="${escapeHtml(editor.name)}" placeholder="${escapeHtml(editor.suggestedName || (original && original.Name) || 'Game name')}">
      </label>
      <label class="disc-field">
        <span class="disc-label">Launch options</span>
        <input class="disc-input mono" data-disc-field="arguments" type="text" spellcheck="false" value="${escapeHtml(editor.arguments)}" placeholder="-windowed -nointro">
      </label>
      <button type="submit" hidden></button>
    </form>
    <footer class="modal-foot">
      ${game && !editor.manual && editor.override ? `<button class="link-button" data-disc-reset>${ICON.undo || ICON.refresh}Undo my changes</button>` : ''}
      <span class="grow"></span>
      <button class="secondary-button compact" data-modal-close>Cancel</button>
      <button class="primary-button compact" data-disc-save>${game ? ICON.check : ICON.discPlus}${game ? 'Save' : 'Add to library'}</button>
    </footer>`;

  const form = panel.querySelector('[data-disc-editor]');
  form.addEventListener('input', event => {
    const field = event.target.dataset.discField;
    if (field) editor[field] = event.target.value;
  });
  form.addEventListener('submit', event => {
    event.preventDefault();
    saveEditor();
  });
  panel.querySelector('[data-disc-save]').addEventListener('click', saveEditor);
  panel.querySelector('[data-disc-browse]').addEventListener('click', () => {
    discRequest({ type: 'discovery.pickExe', initial: editor.path || (game && displayFolder(game)) || '' }, message => {
      if (discovery.editor !== editor || !message.path) return;
      editor.path = message.path;
      editor.known = game && message.known === game.Name ? null : message.known;
      if (!editor.name || editor.name === editor.suggestedName) editor.name = game ? editor.name : message.name || '';
      editor.suggestedName = message.name || '';
      renderEditor();
    });
  });
  const reset = panel.querySelector('[data-disc-reset]');
  if (reset) {
    reset.addEventListener('click', () => {
      send({ type: 'discovery.setOverride', id: game.Id });
      discovery.editor = null;
      modal.close();
    });
  }
}

function saveEditor() {
  const editor = discovery.editor;
  if (!editor) return;
  const path = editor.path.trim().replace(/^"(.*)"$/, '$1');
  const name = editor.name.trim();
  const args = editor.arguments.trim();
  const game = editor.game;

  if (!game || editor.manual) {
    if (!path) {
      toast('Choose the game’s program first.', 'error');
      return;
    }
    discRequest({ type: 'discovery.saveManual', id: game ? game.Id : null, exe: path, name: name || editor.suggestedName, arguments: args }, () => {
      if (discovery.editor === editor) {
        discovery.editor = null;
        modal.close();
      }
    });
    return;
  }

  const original = editor.original || { Name: game.Name, Exe: game.Exe, Arguments: game.Arguments, Url: game.Url };
  const message = { type: 'discovery.setOverride', id: game.Id };
  if (name && name !== original.Name) message.name = name;
  if (path && (original.Url || path.toLowerCase() !== String(original.Exe || '').toLowerCase())) message.exe = path;
  if (args !== (original.Arguments || '')) message.arguments = args;
  send(message);
  discovery.editor = null;
  modal.close();
}

function openMerge(game) {
  discovery.merge = { game, query: '', confirm: null };
  modal.open('', 'disc-modal wide tall');
  modal.onClose = () => {
    discovery.merge = null;
  };
  renderMerge();
  const input = modal.panel.querySelector('[data-disc-merge-search]');
  if (input) input.focus();
}

function mergeCandidates(game, query) {
  const key = nameKey(game.Name);
  const text = normalize(query.trim());
  return state.games
    .filter(other => other !== game)
    .map(other => {
      const otherKey = nameKey(other.Name);
      let score = otherKey === key ? 3 : otherKey.startsWith(key) || key.startsWith(otherKey) ? 2 : 0;
      if (text) {
        if (!normalize(other.Name).includes(text)) return null;
        score += 1;
      }
      return { other, score };
    })
    .filter(Boolean)
    .sort((first, second) => second.score - first.score || first.other.Name.localeCompare(second.other.Name))
    .slice(0, 60)
    .map(entry => entry.other);
}

function renderMerge() {
  const panel = modal.panel;
  const merge = discovery.merge;
  if (!panel || !merge) return;
  const { game } = merge;
  const candidates = mergeCandidates(game, merge.query);

  if (merge.confirm) {
    const other = merge.confirm;
    panel.innerHTML = `
      <header class="modal-head">
        <h2>Merge ${escapeHtml(other.Name)}?</h2>
        <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
      </header>
      <div class="modal-body">
        <p class="disc-text"><strong>${escapeHtml(other.Name)}</strong> (${escapeHtml(platformOf(other).label)}) becomes part of <strong>${escapeHtml(game.Name)}</strong>. Its folder counts toward the size, its playtime and last played date carry over, and it no longer shows as a separate game.</p>
        <p class="disc-text muted">Nothing on disk changes. You can split them again from the game’s details.</p>
      </div>
      <footer class="modal-foot">
        <button class="link-button" data-disc-merge-back>${ICON.chevronLeft || ICON.close}Back</button>
        <span class="grow"></span>
        <button class="secondary-button compact" data-modal-close>Cancel</button>
        <button class="primary-button compact" data-disc-merge-confirm>${ICON.discMerge}Merge</button>
      </footer>`;
    panel.querySelector('[data-disc-merge-back]').addEventListener('click', () => {
      merge.confirm = null;
      renderMerge();
    });
    panel.querySelector('[data-disc-merge-confirm]').addEventListener('click', () => {
      send({ type: 'discovery.merge', keep: game.Id, absorb: other.Id });
      discovery.merge = null;
      modal.close();
    });
    return;
  }

  const rows = candidates.map(other => `
    <button class="disc-pick-row" data-disc-merge-id="${escapeHtml(other.Id)}">
      <span class="disc-pick-icon">${platformIcon(platformOf(other).key)}</span>
      <span class="disc-pick-text">
        <span class="disc-pick-name">${escapeHtml(other.Name)}</span>
        <span class="disc-pick-path mono">${escapeHtml(displayFolder(other) || other.Exe || other.Url || '')}</span>
      </span>
      <span class="disc-pick-platform">${escapeHtml(platformOf(other).label)}</span>
    </button>`).join('');

  const hadFocus = document.activeElement && document.activeElement.matches('[data-disc-merge-search]');
  panel.innerHTML = `
    <header class="modal-head">
      <h2>Merge with ${escapeHtml(game.Name)}</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="disc-search">
      ${ICON.search}
      <input data-disc-merge-search type="text" spellcheck="false" placeholder="Search your library" value="${escapeHtml(merge.query)}">
    </div>
    <div class="modal-body disc-pick-list">
      ${rows || `<div class="disc-empty">${merge.query ? 'No games match.' : 'There are no other games to merge.'}</div>`}
    </div>`;

  const input = panel.querySelector('[data-disc-merge-search]');
  input.addEventListener('input', () => {
    merge.query = input.value;
    renderMerge();
  });
  if (hadFocus) {
    input.focus();
    input.setSelectionRange(input.value.length, input.value.length);
  }
  panel.querySelector('.disc-pick-list').addEventListener('click', event => {
    const row = event.target.closest('[data-disc-merge-id]');
    if (!row) return;
    merge.confirm = state.byId.get(row.dataset.discMergeId) || null;
    renderMerge();
  });
}

const SHORTCUT_ACTIONS = [
  { key: 'add', label: 'New' },
  { key: 'update', label: 'Updated' },
  { key: 'remove', label: 'Removed' },
  { key: 'keep', label: 'Unchanged' },
];

function openShortcutPreview() {
  discovery.shortcuts = { running: true, items: [], skipped: [], error: null, applying: false, showKeep: false, showSkipped: false };
  modal.open('', 'disc-modal wide');
  modal.onClose = () => {
    discovery.shortcuts = null;
  };
  renderShortcutPreview();
  send({ type: 'discovery.shortcutsPreview' });
}

function renderShortcutPreview() {
  const panel = modal.panel;
  const plan = discovery.shortcuts;
  if (!panel || !plan) return;

  let body;
  if (plan.running) {
    body = `<div class="disc-loading"><span class="measuring"></span>Checking every launcher and folder. This takes up to a minute.</div>`;
  } else if (plan.error) {
    body = `<div class="callout">${ICON.alert}<div>${escapeHtml(plan.error)}</div></div>`;
  } else {
    const groups = SHORTCUT_ACTIONS.map(action => {
      const items = plan.items.filter(item => item.Action === action.key);
      if (!items.length) return '';
      const collapsed = action.key === 'keep' && !plan.showKeep;
      return `
        <section class="disc-plan-group">
          <h3 class="section-title">${action.label} <span class="disc-count">${items.length}</span>
            ${action.key === 'keep' ? `<button class="link-button" data-disc-toggle="showKeep">${plan.showKeep ? 'Hide' : 'Show'}</button>` : ''}
          </h3>
          ${collapsed ? '' : items.map(item => `
            <div class="disc-plan-row ${action.key}">
              <span class="disc-plan-mark">${action.key === 'remove' ? '−' : action.key === 'keep' ? '·' : '+'}</span>
              <span class="disc-plan-name">${escapeHtml(item.Name)}</span>
              <span class="disc-plan-target mono" title="${escapeHtml(item.Target || item.File)}">${escapeHtml(item.Target || item.File)}</span>
            </div>`).join('')}
        </section>`;
    }).join('');

    const skipped = plan.skipped || [];
    const skippedHtml = skipped.length ? `
      <section class="disc-plan-group">
        <h3 class="section-title">Skipped <span class="disc-count">${skipped.length}</span>
          <button class="link-button" data-disc-toggle="showSkipped">${plan.showSkipped ? 'Hide' : 'Show'}</button>
        </h3>
        ${plan.showSkipped ? skipped.map(item => `
          <div class="disc-plan-row skipped">
            <span class="disc-plan-mark">×</span>
            <span class="disc-plan-name">${escapeHtml(item.Name)}</span>
            <span class="disc-plan-target">${escapeHtml(item.Reason)}</span>
          </div>`).join('') : ''}
      </section>` : '';

    const changes = plan.items.filter(item => item.Action !== 'keep').length;
    body = `
      <p class="disc-text">${changes ? `${changes} change${changes === 1 ? '' : 's'} in` : 'Nothing to change in'} <span class="mono">${escapeHtml(plan.folder || '')}</span></p>
      ${groups}
      ${skippedHtml}`;
  }

  const changes = plan.items.filter(item => item.Action !== 'keep').length;
  panel.innerHTML = `
    <header class="modal-head">
      <h2>Start Menu shortcuts</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="modal-body disc-plan">${body}</div>
    <footer class="modal-foot">
      <span class="grow"></span>
      <button class="secondary-button compact" data-modal-close>Close</button>
      <button class="primary-button compact" data-disc-apply-shortcuts ${plan.running || plan.error || plan.applying || !changes ? 'disabled' : ''}>${ICON.check}${plan.applying ? 'Applying…' : 'Apply changes'}</button>
    </footer>`;

  panel.querySelectorAll('[data-disc-toggle]').forEach(button => button.addEventListener('click', () => {
    plan[button.dataset.discToggle] = !plan[button.dataset.discToggle];
    renderShortcutPreview();
  }));
  panel.querySelector('[data-disc-apply-shortcuts]').addEventListener('click', () => {
    plan.applying = true;
    renderShortcutPreview();
    send({ type: 'discovery.shortcutsApply' });
  });
}

function openSteamExport() {
  discovery.steam = { running: true, items: [], skipped: [], error: null, applying: false, steamRunning: false, showSkipped: false };
  modal.open('', 'disc-modal wide');
  modal.onClose = () => {
    discovery.steam = null;
  };
  renderSteamExport();
  send({ type: 'discovery.steamPreview' });
}

function renderSteamExport() {
  const panel = modal.panel;
  const plan = discovery.steam;
  if (!panel || !plan) return;

  let body;
  if (plan.running) {
    body = `<div class="disc-loading"><span class="measuring"></span>Reading your Steam shortcuts…</div>`;
  } else if (plan.error) {
    body = `<div class="callout">${ICON.alert}<div>${escapeHtml(plan.error)}</div></div>`;
  } else {
    const rows = plan.items.map(item => {
      const game = state.byId.get(item.id);
      return `
        <div class="disc-plan-row ${item.action}">
          <span class="disc-plan-mark">+</span>
          <span class="disc-plan-icon">${game ? platformIcon(platformOf(game).key) : ''}</span>
          <span class="disc-plan-name">${escapeHtml(item.name)}</span>
          <span class="disc-plan-tag">${item.action === 'add' ? 'New' : 'Update'}${item.art ? ` · ${item.art} image${item.art === 1 ? '' : 's'}` : ''}</span>
        </div>`;
    }).join('');
    const skipped = plan.skipped || [];
    body = `
      ${plan.steamRunning ? `<div class="callout">${ICON.alert}<div><strong>Steam is open.</strong> Close Steam completely (including the tray icon), then apply. Steam overwrites its shortcut list while it runs.</div></div>` : ''}
      <p class="disc-text">Games from other launchers show up in Steam as non-Steam games, with their cover art. Your existing Steam shortcuts are kept, and a backup is saved first.</p>
      ${plan.items.length ? `<section class="disc-plan-group"><h3 class="section-title">Games <span class="disc-count">${plan.items.length}</span></h3>${rows}</section>` : `<div class="disc-empty">Every game is already in Steam.</div>`}
      ${skipped.length ? `
        <section class="disc-plan-group">
          <h3 class="section-title">Already in Steam <span class="disc-count">${skipped.length}</span>
            <button class="link-button" data-disc-toggle="showSkipped">${plan.showSkipped ? 'Hide' : 'Show'}</button>
          </h3>
          ${plan.showSkipped ? skipped.map(item => `
            <div class="disc-plan-row skipped"><span class="disc-plan-mark">·</span><span class="disc-plan-name">${escapeHtml(item.name)}</span><span class="disc-plan-target">${escapeHtml(item.reason)}</span></div>`).join('') : ''}
        </section>` : ''}`;
  }

  const blocked = plan.running || plan.error || plan.applying || plan.steamRunning || !plan.items.length;
  panel.innerHTML = `
    <header class="modal-head">
      <h2>Add games to Steam</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="modal-body disc-plan">${body}</div>
    <footer class="modal-foot">
      ${!plan.running && !plan.error ? `<button class="link-button" data-disc-steam-recheck>${ICON.refresh}Check again</button>` : ''}
      <span class="grow"></span>
      <button class="secondary-button compact" data-modal-close>Close</button>
      <button class="primary-button compact" data-disc-steam-apply ${blocked ? 'disabled' : ''}>${ICON.check}${plan.applying ? 'Adding…' : 'Add to Steam'}</button>
    </footer>`;

  panel.querySelectorAll('[data-disc-toggle]').forEach(button => button.addEventListener('click', () => {
    plan[button.dataset.discToggle] = !plan[button.dataset.discToggle];
    renderSteamExport();
  }));
  const recheck = panel.querySelector('[data-disc-steam-recheck]');
  if (recheck) {
    recheck.addEventListener('click', () => {
      plan.running = true;
      renderSteamExport();
      send({ type: 'discovery.steamPreview' });
    });
  }
  panel.querySelector('[data-disc-steam-apply]').addEventListener('click', () => {
    plan.applying = true;
    renderSteamExport();
    send({ type: 'discovery.steamApply' });
  });
}

function discToggle(key, fallback, label, hint) {
  const on = setting(key, fallback) === true;
  return `
    <label class="disc-setting">
      <span class="disc-setting-text">
        <span class="disc-setting-label">${label}</span>
        ${hint ? `<span class="disc-setting-hint">${hint}</span>` : ''}
      </span>
      <input type="checkbox" class="play-switch" data-disc-setting="${key}" data-disc-default="${fallback}" ${on ? 'checked' : ''}>
    </label>`;
}

function refreshSection(id) {
  const section = extensions.settingsSections.find(entry => entry.id === id);
  const container = document.querySelector(`[data-disc-section="${id}"]`);
  if (!section || !container) return;
  const template = document.createElement('template');
  template.innerHTML = section.render().trim();
  const fresh = template.content.firstElementChild;
  if (fresh) container.innerHTML = fresh.innerHTML;
}

function bindOnce(root, id, handlers) {
  if (!root || root.dataset[`disc${id}`]) return;
  root.dataset[`disc${id}`] = 'true';
  for (const [eventName, handler] of Object.entries(handlers)) root.addEventListener(eventName, handler);
}

function settingToggleHandler(event) {
  const input = event.target.closest('[data-disc-setting]');
  if (!input) return false;
  const fallback = input.dataset.discDefault === 'true';
  setSetting(input.dataset.discSetting, input.checked === fallback ? null : input.checked);
  return true;
}

extensions.settingsSections.push({
  id: 'discovery-shortcuts',
  title: 'Start Menu shortcuts',
  render: () => `
    <div class="disc-settings" data-disc-section="discovery-shortcuts">
      <div class="disc-setting">
        <span class="disc-setting-text">
          <span class="disc-setting-label">Shortcut folder</span>
          <span class="disc-setting-hint mono">Start Menu\\Programs\\${escapeHtml(discovery.data.shortcutFolder || 'Games')}</span>
        </span>
        <button class="secondary-button compact" data-disc-shortcuts-preview>${ICON.discShortcut}Preview changes…</button>
      </div>
      ${discToggle('discovery.coverIcons', false, 'Use cover art as shortcut icons', 'Shortcuts get a small version of each game’s cover instead of the program icon. Applies on the next refresh.')}
    </div>`,
  bind(root) {
    bindOnce(root, 'Shortcuts', {
      click: event => {
        if (event.target.closest('[data-disc-shortcuts-preview]')) openShortcutPreview();
      },
      change: event => {
        if (settingToggleHandler(event)) toast('Shortcut icons update on the next refresh', 'info', ICON.check);
      },
    });
  },
});

extensions.settingsSections.push({
  id: 'discovery-steam',
  title: 'Steam',
  render: () => `
    <div class="disc-settings" data-disc-section="discovery-steam">
      <div class="disc-setting">
        <span class="disc-setting-text">
          <span class="disc-setting-label">Add games from other launchers to Steam</span>
          <span class="disc-setting-hint">Shows them in your Steam library and Big Picture with their artwork. Steam must be closed to apply.</span>
        </span>
        <button class="secondary-button compact" data-disc-steam>${ICON.discShortcut}Review…</button>
      </div>
    </div>`,
  bind(root) {
    bindOnce(root, 'Steam', {
      click: event => {
        if (event.target.closest('[data-disc-steam]')) openSteamExport();
      },
    });
  },
});

function emulatorDraft() {
  if (!discovery.emulators) {
    discovery.emulators = (discovery.data.emulators || []).map(entry => ({
      Name: entry.Name || '',
      Exe: entry.Exe || '',
      Arguments: entry.Arguments || '',
      RomFolders: [...(entry.RomFolders || [])],
      Extensions: [...(entry.Extensions || [])],
    }));
    discovery.emulatorsDirty = false;
  }
  return discovery.emulators;
}

function emulatorCardHtml(entry, index) {
  const folders = entry.RomFolders.map((folder, folderIndex) => `
    <span class="disc-folder-chip mono" title="${escapeHtml(folder)}">${escapeHtml(folder)}<button class="disc-chip-remove" data-disc-emu-folder-remove="${index}:${folderIndex}" title="Remove">${ICON.close}</button></span>`).join('');
  return `
    <div class="disc-emu-card">
      <div class="disc-emu-head">
        <span class="disc-emu-icon">${ICON.discChip}</span>
        <input class="disc-input disc-emu-name" data-disc-emu="${index}" data-disc-emu-field="Name" type="text" spellcheck="false" value="${escapeHtml(entry.Name)}" placeholder="Emulator name">
        <button class="icon-button" data-disc-emu-remove="${index}" title="Remove emulator">${ICON.close}</button>
      </div>
      <div class="disc-emu-grid">
        <span class="disc-label">Program</span>
        <span class="disc-path-row">
          <input class="disc-input mono" data-disc-emu="${index}" data-disc-emu-field="Exe" type="text" spellcheck="false" value="${escapeHtml(entry.Exe)}" placeholder="C:\\Emulators\\Emulator.exe">
          <button class="secondary-button compact" data-disc-emu-exe="${index}">${ICON.folder}Browse…</button>
        </span>
        <span class="disc-label">Launch options</span>
        <input class="disc-input mono" data-disc-emu="${index}" data-disc-emu-field="Arguments" type="text" spellcheck="false" value="${escapeHtml(entry.Arguments)}" placeholder="&quot;{rom}&quot;">
        <span class="disc-label">Game folders</span>
        <span class="disc-folders">${folders}<button class="link-button" data-disc-emu-folder-add="${index}">${ICON.discPlus}Add folder</button></span>
        <span class="disc-label">File types</span>
        <input class="disc-input mono" data-disc-emu="${index}" data-disc-emu-field="Extensions" type="text" spellcheck="false" value="${escapeHtml(entry.Extensions.join(', '))}" placeholder="iso, rvz, wbfs">
      </div>
    </div>`;
}

extensions.settingsSections.push({
  id: 'discovery-emulators',
  title: 'Emulators',
  render: () => {
    const draft = emulatorDraft();
    const configured = new Set(draft.map(entry => nameKey(entry.Name)));
    const suggestions = (discovery.data.detectedEmulators || []).filter(entry => !configured.has(nameKey(entry.Name)));
    return `
      <div class="disc-settings" data-disc-section="discovery-emulators">
        <p class="disc-setting-hint disc-intro">Each game file in an emulator’s folders becomes its own game. Use <span class="mono">{rom}</span> in the launch options where the file path goes.</p>
        ${suggestions.map(entry => `
          <div class="disc-suggestion">
            <span class="disc-emu-icon">${ICON.discChip}</span>
            <span class="disc-setting-text">
              <span class="disc-setting-label">${escapeHtml(entry.Name)} found</span>
              <span class="disc-setting-hint mono">${escapeHtml(entry.Exe)}</span>
            </span>
            <button class="secondary-button compact" data-disc-emu-suggest="${escapeHtml(entry.Name)}">${ICON.discPlus}Set up</button>
          </div>`).join('')}
        ${draft.map(emulatorCardHtml).join('')}
        <div class="disc-emu-foot">
          <button class="secondary-button compact" data-disc-emu-add>${ICON.discPlus}Add emulator</button>
          <span class="grow"></span>
          ${discovery.emulatorsDirty ? `<button class="link-button" data-disc-emu-discard>Discard changes</button>` : ''}
          <button class="primary-button compact" data-disc-emu-save ${discovery.emulatorsDirty ? '' : 'disabled'}>${ICON.check}Save emulators</button>
        </div>
      </div>`;
  },
  bind(root) {
    bindOnce(root, 'Emulators', {
      input: event => {
        const input = event.target.closest('[data-disc-emu-field]');
        if (!input) return;
        const entry = emulatorDraft()[Number(input.dataset.discEmu)];
        if (!entry) return;
        const field = input.dataset.discEmuField;
        entry[field] = field === 'Extensions' ? input.value.split(/[,;\s]+/).map(item => item.trim()).filter(Boolean) : input.value;
        if (!discovery.emulatorsDirty) {
          discovery.emulatorsDirty = true;
          const save = root.querySelector('[data-disc-emu-save]');
          if (save) save.disabled = false;
        }
      },
      click: event => {
        const target = event.target.closest('button');
        if (!target) return;
        const draft = emulatorDraft();
        const changed = () => {
          discovery.emulatorsDirty = true;
          refreshSection('discovery-emulators');
        };

        if (target.matches('[data-disc-emu-add]')) {
          draft.push({ Name: '', Exe: '', Arguments: '"{rom}"', RomFolders: [], Extensions: [] });
          changed();
        } else if (target.matches('[data-disc-emu-remove]')) {
          draft.splice(Number(target.dataset.discEmuRemove), 1);
          changed();
        } else if (target.matches('[data-disc-emu-suggest]')) {
          const found = (discovery.data.detectedEmulators || []).find(entry => entry.Name === target.dataset.discEmuSuggest);
          if (found) {
            draft.push({ Name: found.Name, Exe: found.Exe, Arguments: found.Arguments, RomFolders: [...(found.RomFolders || [])], Extensions: [...(found.Extensions || [])] });
            changed();
          }
        } else if (target.matches('[data-disc-emu-exe]')) {
          const index = Number(target.dataset.discEmuExe);
          discRequest({ type: 'pickFile', title: 'Choose the emulator', filter: 'Programs|*.exe|All files|*.*', initial: '' }, message => {
            if (!message.path || !draft[index]) return;
            draft[index].Exe = message.path;
            if (!draft[index].Name) draft[index].Name = message.path.split('\\').pop().replace(/\.exe$/i, '');
            changed();
          });
        } else if (target.matches('[data-disc-emu-folder-add]')) {
          const index = Number(target.dataset.discEmuFolderAdd);
          discRequest({ type: 'pickFolder', title: 'Choose a folder with games for this emulator' }, message => {
            if (!message.path || !draft[index]) return;
            if (!draft[index].RomFolders.some(folder => folder.toLowerCase() === message.path.toLowerCase())) draft[index].RomFolders.push(message.path);
            changed();
          });
        } else if (target.matches('[data-disc-emu-folder-remove]')) {
          const [index, folderIndex] = target.dataset.discEmuFolderRemove.split(':').map(Number);
          if (draft[index]) draft[index].RomFolders.splice(folderIndex, 1);
          changed();
        } else if (target.matches('[data-disc-emu-discard]')) {
          discovery.emulators = null;
          refreshSection('discovery-emulators');
        } else if (target.matches('[data-disc-emu-save]')) {
          const incomplete = draft.find(entry => !entry.Name.trim() || !entry.Exe.trim());
          if (incomplete) {
            toast('Every emulator needs a name and a program.', 'error');
            return;
          }
          send({ type: 'discovery.saveEmulators', emulators: draft });
          discovery.emulatorsDirty = false;
          refreshSection('discovery-emulators');
        }
      },
    });
  },
});

extensions.settingsSections.push({
  id: 'discovery-library',
  title: 'Finding games',
  render: () => {
    const manual = discovery.data.manual || [];
    const merges = discovery.data.merges || [];
    return `
      <div class="disc-settings" data-disc-section="discovery-library">
        ${discToggle('discovery.watchFolders', true, 'Watch game folders', 'Refreshes the library by itself when a new game appears in your game folders.')}
        <div class="disc-setting">
          <span class="disc-setting-text">
            <span class="disc-setting-label">Games you added</span>
            ${manual.length ? '' : '<span class="disc-setting-hint">Use “Add a game” in the sidebar for games no launcher knows about.</span>'}
          </span>
          <button class="secondary-button compact" data-disc-add>${ICON.discPlus}Add a game…</button>
        </div>
        ${manual.map(entry => {
          const id = entry.Id || `manual:${String(entry.Exe || '').toLowerCase()}`;
          return `
            <div class="disc-list-row">
              <span class="disc-list-text">
                <span class="disc-list-name">${escapeHtml(entry.Name || entry.Exe)}</span>
                <span class="disc-list-path mono">${escapeHtml(entry.Exe)}${entry.Arguments ? ` ${escapeHtml(entry.Arguments)}` : ''}</span>
              </span>
              <button class="icon-button" data-disc-manual-edit="${escapeHtml(id)}" title="Edit">${ICON.discPencil}</button>
              <button class="icon-button" data-disc-manual-remove="${escapeHtml(id)}" title="Remove from library">${ICON.close}</button>
            </div>`;
        }).join('')}
        ${merges.length ? `
          <div class="disc-setting"><span class="disc-setting-text"><span class="disc-setting-label">Merged games</span></span></div>
          ${merges.map(merge => `
            <div class="disc-list-row">
              <span class="disc-list-text">
                <span class="disc-list-name">${escapeHtml(merge.absorbName || merge.absorb)} <span class="disc-arrow">→</span> ${escapeHtml(merge.keepName || merge.keep)}</span>
                ${merge.missing ? '<span class="disc-list-path">One of these isn’t in the library right now</span>' : ''}
              </span>
              <button class="secondary-button compact" data-disc-unmerge-keep="${escapeHtml(merge.keep)}" data-disc-unmerge-absorb="${escapeHtml(merge.absorb)}">${ICON.discSplit}Split</button>
            </div>`).join('')}` : ''}
      </div>`;
  },
  bind(root) {
    bindOnce(root, 'Library', {
      change: event => settingToggleHandler(event),
      click: event => {
        const target = event.target.closest('button');
        if (!target) return;
        if (target.matches('[data-disc-add]')) openEditor(null);
        else if (target.matches('[data-disc-manual-edit]')) {
          const game = state.byId.get(target.dataset.discManualEdit);
          if (game) openEditor(game);
          else toast('That game isn’t in the library right now. Refresh first.', 'error');
        } else if (target.matches('[data-disc-manual-remove]')) {
          const game = state.byId.get(target.dataset.discManualRemove) || { Id: target.dataset.discManualRemove, Name: 'this game' };
          removeManual(game);
        } else if (target.matches('[data-disc-unmerge-absorb]')) {
          send({ type: 'discovery.unmerge', keep: target.dataset.discUnmergeKeep, absorb: target.dataset.discUnmergeAbsorb });
        }
      },
    });
  },
});

extensions.settingsSections.push({
  id: 'discovery-hidden',
  title: 'Hidden games',
  render: () => {
    const hidden = discovery.data.hidden || [];
    return `
      <div class="disc-settings" data-disc-section="discovery-hidden">
        ${hidden.length ? hidden.map((entry, index) => {
          const platform = entry.source ? PLATFORMS.find(item => item.sources.includes(entry.source)) : null;
          return `
            <div class="disc-list-row">
              <span class="disc-list-icon">${entry.iconUrl ? `<img src="${escapeHtml(entry.iconUrl)}" alt="">` : ICON.eyeOff}</span>
              <span class="disc-list-text">
                <span class="disc-list-name">${escapeHtml(entry.name)}</span>
                <span class="disc-list-path">${escapeHtml([platform ? platform.label : null, entry.byName ? 'Hidden by name' : null].filter(Boolean).join(' · ') || 'Not found right now')}</span>
              </span>
              <button class="secondary-button compact" data-disc-unhide="${index}">${ICON.discEye}Show</button>
            </div>`;
        }).join('') : '<div class="disc-empty">No hidden games. Right-click a game and choose “Hide from library” to hide it.</div>'}
      </div>`;
  },
  bind(root) {
    bindOnce(root, 'Hidden', {
      click: event => {
        const button = event.target.closest('[data-disc-unhide]');
        if (!button) return;
        const entry = (discovery.data.hidden || [])[Number(button.dataset.discUnhide)];
        if (!entry) return;
        button.disabled = true;
        if (entry.byName) send({ type: 'discovery.unhideName', name: entry.name });
        else send({ type: 'discovery.unhide', id: entry.id });
      },
    });
  },
});

extensions.libraryHandlers.push(message => {
  discovery.data = (message.extras && message.extras.discovery) || {};
  if (!discovery.emulatorsDirty) discovery.emulators = null;
  syncEmulatorPlatforms();
  if (state.platform && !PLATFORMS.some(platform => platform.key === state.platform)) state.platform = null;
});

function applyDiscoveryDownloads(updates) {
  let needsRender = false;
  for (const update of updates || []) {
    const game = state.byId.get(update.id);
    if (!game) continue;
    const statusChanged = update.status && update.status !== game.Status;
    if (update.status) game.Status = update.status;
    game.Download = update.download || null;
    game.NeedsUpdate = update.needsUpdate || null;
    if (statusChanged) {
      needsRender = true;
      continue;
    }

    document.querySelectorAll(`.card[data-id="${CSS.escape(game.Id)}"] .cover`).forEach(cover => {
      cover.querySelectorAll('[data-disc-badge]').forEach(element => element.remove());
      const shade = cover.querySelector('.cover-shade');
      const html = downloadBadgeHtml(game);
      if (shade && html) shade.insertAdjacentHTML('afterend', html);
    });
    if (state.selectedId === game.Id) {
      const block = elements.drawer.querySelector('[data-disc-download]');
      if (block) block.innerHTML = downloadBlockHtml(game);
    }
  }
  if (needsRender) scheduleRender(400);
}

extensions.messageHandlers.push(message => {
  switch (message.type) {
    case 'pickedPath':
    case 'discovery.pickedExe':
    case 'discovery.manualSaved':
      resolveRequest(message);
      break;

    case 'discovery.downloads':
      applyDiscoveryDownloads(message.games);
      break;

    case 'discovery.shortcutsPreview':
      if (!discovery.shortcuts) break;
      Object.assign(discovery.shortcuts, {
        running: !!message.running,
        error: message.error || null,
        folder: message.folder || discovery.shortcuts.folder,
        items: message.running ? discovery.shortcuts.items : message.items || [],
        skipped: message.running ? discovery.shortcuts.skipped : message.skipped || [],
      });
      renderShortcutPreview();
      break;

    case 'discovery.shortcutsApplied':
      if (discovery.shortcuts && discovery.shortcuts.applying) {
        discovery.shortcuts = null;
        modal.close();
      }
      break;

    case 'discovery.steamPreview':
      if (!discovery.steam) break;
      Object.assign(discovery.steam, {
        running: false,
        error: message.error || null,
        items: message.items || [],
        skipped: message.skipped || [],
        steamRunning: !!message.steamRunning,
      });
      renderSteamExport();
      break;

    case 'discovery.steamApplied':
      if (!discovery.steam) break;
      if (message.error) {
        Object.assign(discovery.steam, { applying: false, error: null, steamRunning: /close steam/i.test(message.error) || discovery.steam.steamRunning });
        toast(message.error, 'error');
        renderSteamExport();
      } else {
        const total = (message.added || 0) + (message.updated || 0);
        toast(total ? `Added ${message.added || 0} and updated ${message.updated || 0} games in Steam` : 'Steam already had every game', 'info', ICON.check);
        discovery.steam = null;
        modal.close();
      }
      break;
  }
});
