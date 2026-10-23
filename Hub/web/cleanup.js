'use strict';

ICON.broom = '<svg viewBox="0 0 24 24" class="glyph"><path d="M14.5 3.5 10 11"/><path d="M6.5 11.5h7l1.5 2.5-1 6.5H6l-1-6.5z"/><path d="M8.5 15.5v5M11.5 15.5v5"/></svg>';
ICON.select = '<svg viewBox="0 0 24 24" class="glyph"><rect x="4" y="4" width="16" height="16" rx="4"/><path d="m8.5 12.2 2.4 2.4 4.6-5"/></svg>';
ICON.skip = '<svg viewBox="0 0 24 24" class="glyph"><path d="M6 5.5v13l9-6.5zM18 5.5v13"/></svg>';

VIEWS.cleanup = 'Cleanup';

state.selection = new Set();
state.selectMode = false;

const cleanup = {
  scan: null,
  scanning: false,
  selected: new Set(),
  busy: false,
  anchor: null,
  batch: null,
  leftovers: null,
  backupAll: null,
};

const BATCH_METHODS = {
  recycle: { label: 'Recycle Bin', icon: () => ICON.trash },
  steam: { label: 'Steam', icon: () => ICON.shield },
  uninstaller: { label: 'Uninstaller', icon: () => ICON.shield },
  launcher: { label: 'Launcher', icon: () => ICON.external },
  none: { label: 'Can’t remove', icon: () => ICON.lock },
};

const BATCH_STATUS = {
  pending: 'Waiting',
  backup: 'Backing up saves',
  working: 'Removing',
  waiting: 'Waiting for you',
  removed: 'Removed',
  opened: 'Launcher opened',
  skipped: 'Skipped',
  partial: 'Partly removed',
  failed: 'Failed',
  blocked: 'Not removed',
  cancelled: 'Cancelled',
};

function toggleSelection(id, force) {
  if (!id) return;
  const on = force === undefined ? !state.selection.has(id) : force;
  if (on) state.selection.add(id);
  else state.selection.delete(id);
  applySelection();
}

function clearSelection() {
  state.selection.clear();
  state.selectMode = false;
  cleanup.anchor = null;
  applySelection();
  if (!extensions.views[state.view]) renderHeadline(state.visible);
}

function selectRange(toId) {
  const order = state.visible.map(game => game.Id);
  const from = order.indexOf(cleanup.anchor);
  const to = order.indexOf(toId);
  if (from < 0 || to < 0) {
    toggleSelection(toId, true);
    return;
  }
  const [start, end] = from < to ? [from, to] : [to, from];
  for (let index = start; index <= end; index++) state.selection.add(order[index]);
  applySelection();
}

function selectionGames() {
  return [...state.selection].map(id => state.byId.get(id)).filter(Boolean);
}

function applySelection() {
  const active = state.selection.size > 0 || state.selectMode;
  document.body.classList.toggle('batch-selecting', active && !extensions.views[state.view]);
  for (const item of elements.results.querySelectorAll('.card[data-id], .list-row[data-id]')) {
    item.classList.toggle('batch-selected', state.selection.has(item.dataset.id));
  }
  renderBatchBar();
}

function selectionAllowed() {
  return !extensions.views[state.view];
}

elements.results.addEventListener('click', event => {
  if (!selectionAllowed()) return;
  const item = event.target.closest('.card[data-id], .list-row[data-id]');
  if (!item) return;
  const modifier = event.ctrlKey || event.metaKey || event.shiftKey;
  if (!modifier && !state.selectMode) return;
  event.preventDefault();
  event.stopImmediatePropagation();
  const id = item.dataset.id;
  if (event.shiftKey && cleanup.anchor) selectRange(id);
  else toggleSelection(id);
  cleanup.anchor = id;
}, true);

elements.results.addEventListener('dblclick', event => {
  if (state.selectMode && event.target.closest('[data-id]')) {
    event.preventDefault();
    event.stopImmediatePropagation();
  }
}, true);

new MutationObserver(() => {
  if (state.selection.size || state.selectMode) applySelection();
}).observe(elements.results, { childList: true, subtree: true });

extensions.filterChips.push(() => `<button class="chip ${state.selectMode ? 'active' : ''}" data-batch-select title="Select several games. You can also Ctrl-click or Shift-click covers.">${ICON.select}${state.selectMode ? 'Done selecting' : 'Select'}</button>`);

elements.filters.addEventListener('click', event => {
  if (!event.target.closest('[data-batch-select]') || extensions.views[state.view]) return;
  if (state.selectMode) {
    clearSelection();
  } else {
    state.selectMode = true;
    applySelection();
    renderHeadline(state.visible);
  }
});

extensions.keyHandlers.push(event => {
  if (event.key !== 'Escape' || modal.isOpen || state.selectedId) return false;
  if (!state.selection.size && !state.selectMode) return false;
  clearSelection();
  return true;
});

extensions.libraryHandlers.push(() => {
  for (const id of [...state.selection]) {
    if (!state.byId.has(id)) state.selection.delete(id);
  }
});

extensions.afterRender.push(() => applySelection());

function batchBar() {
  let bar = document.getElementById('batchBar');
  if (!bar) {
    bar = document.createElement('div');
    bar.id = 'batchBar';
    bar.className = 'batch-bar';
    document.body.appendChild(bar);
    bar.addEventListener('click', event => {
      if (event.target.closest('[data-batch-clear]')) clearSelection();
      else if (event.target.closest('[data-batch-all]')) {
        for (const game of state.visible) state.selection.add(game.Id);
        applySelection();
      } else if (event.target.closest('[data-batch-uninstall]') && state.selection.size) openBatch([...state.selection]);
    });
  }
  return bar;
}

function renderBatchBar() {
  const bar = batchBar();
  const show = (state.selection.size > 0 || state.selectMode) && selectionAllowed();
  bar.classList.toggle('open', show);
  document.body.classList.toggle('batch-bar-open', show);
  if (!show) return;
  const games = selectionGames();
  const size = sharedSize(games);
  const count = games.length;
  const allVisible = state.visible.length > 0 && state.visible.every(game => state.selection.has(game.Id));
  bar.innerHTML = `
    <span class="batch-count"><strong>${formatCount(count)}</strong> ${count === 1 ? 'game' : 'games'} selected</span>
    ${count ? `<span class="batch-size">${size.pending && !size.bytes ? '<span class="measuring"></span>' : formatBytes(size.bytes)}</span>` : '<span class="batch-size muted">Click covers to select them</span>'}
    <span class="batch-spacer"></span>
    ${allVisible ? '' : `<button class="text-button" data-batch-all>Select all ${formatCount(state.visible.length)}</button>`}
    <button class="text-button" data-batch-clear>${count ? 'Clear' : 'Cancel'}</button>
    <button class="danger-button" data-batch-uninstall ${count ? '' : 'disabled'}>${ICON.trash}Uninstall selected</button>`;
}

function openBatch(ids) {
  cleanup.batch = { ids, items: null, backup: setting('saves.backupBeforeUninstall', true) !== false, running: false, done: false, status: {}, current: null };
  modal.open(`
    <header class="modal-head">
      <h2>Uninstall ${ids.length} ${ids.length === 1 ? 'game' : 'games'}</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="modal-body"><div class="uninstall-loading"><span class="measuring"></span></div></div>`, 'uninstall-modal batch-modal');
  send({ type: 'batchUninstallPlan', ids });
}

function batchItemBytes(item) {
  const game = state.byId.get(item.id);
  if (item.method === 'recycle') return (item.folders || []).reduce((sum, folder) => sum + (folderSize(folder.path) || 0), 0);
  return game ? sizeOf(game).bytes || 0 : 0;
}

function renderBatch() {
  const batch = cleanup.batch;
  const panel = modal.panel;
  if (!batch || !batch.items || !panel || !panel.classList.contains('batch-modal')) return;

  const runnable = batch.items.filter(item => item.method !== 'none');
  const blocked = batch.items.length - runnable.length;
  const launchers = runnable.filter(item => item.method === 'launcher').length;
  const bytes = runnable.reduce((sum, item) => sum + batchItemBytes(item), 0);

  const rows = batch.items.map(item => {
    const method = BATCH_METHODS[item.method] || BATCH_METHODS.none;
    const status = batch.status[item.id];
    const statusClass = status ? status.status : item.method === 'none' ? 'blocked' : 'pending';
    const note = status && status.message ? status.message : item.blocked || (item.method === 'launcher' ? `Opens ${item.launcher || 'its launcher'} so you can remove it there.` : item.method === 'steam' ? 'Steam asks you to confirm.' : item.method === 'uninstaller' ? 'Its own uninstaller asks you to confirm.' : `${(item.folders || []).length} ${(item.folders || []).length === 1 ? 'folder' : 'folders'} to the Recycle Bin`);
    const isCurrent = batch.current === item.id && batch.running;
    const leftoverButton = batch.done && status && status.status === 'removed' && item.method === 'recycle'
      ? `<button class="text-button" data-batch-leftovers="${escapeHtml(item.id)}">${ICON.search}Leftovers</button>`
      : '<span></span>';
    return `
      <div class="batch-row ${statusClass} ${isCurrent ? 'current' : ''}">
        <span class="batch-row-icon">${isCurrent ? '<span class="measuring"></span>' : method.icon()}</span>
        <span class="remove-text">
          <span class="remove-label">${escapeHtml(item.name)}</span>
          <span class="batch-row-note">${escapeHtml(note)}</span>
        </span>
        ${leftoverButton}
        <span class="batch-state ${statusClass}">${escapeHtml(status ? BATCH_STATUS[status.status] || status.status : method.label)}</span>
        <span class="remove-size">${item.method === 'none' || !batchItemBytes(item) ? '' : formatBytes(batchItemBytes(item))}</span>
      </div>`;
  }).join('');

  const waiting = batch.running && batch.current && batch.status[batch.current] && batch.status[batch.current].status === 'waiting';
  let foot;
  if (batch.done) {
    foot = `<span class="grow remove-total">${escapeHtml(batch.summary || '')}</span><button class="primary-button compact" data-modal-close>Done</button>`;
  } else if (batch.running) {
    foot = `
      <span class="grow remove-total">Removing one game at a time.</span>
      ${waiting ? `<button class="secondary-button compact" data-batch-skip>${ICON.skip}Don’t wait</button>` : ''}
      <button class="secondary-button compact" data-batch-stop ${batch.stopping ? 'disabled' : ''}>${batch.stopping ? 'Stopping…' : 'Stop after this game'}</button>`;
  } else {
    foot = `
      <span class="grow remove-total">${bytes ? `Frees about <strong>${formatBytes(bytes)}</strong>` : ''}</span>
      <button class="secondary-button compact" data-modal-close>Cancel</button>
      <button class="danger-button" data-batch-confirm ${runnable.length ? '' : 'disabled'}>${ICON.trash}Uninstall ${runnable.length} ${runnable.length === 1 ? 'game' : 'games'}</button>`;
  }

  panel.innerHTML = `
    <header class="modal-head">
      <h2>Uninstall ${batch.items.length} ${batch.items.length === 1 ? 'game' : 'games'}</h2>
      ${batch.running ? '' : `<button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>`}
    </header>
    <div class="modal-body">
      ${!batch.running && !batch.done && (blocked || launchers) ? `<div class="callout neutral">${ICON.info}<div>${[blocked ? `${blocked} can’t be removed from here and will be skipped.` : '', launchers ? `${launchers} ${launchers === 1 ? 'is' : 'are'} managed by a launcher, which opens so you can finish there.` : ''].filter(Boolean).join(' ')}</div></div>` : ''}
      <div class="batch-list">${rows}</div>
      ${!batch.running && !batch.done ? `
        <label class="uninstall-saves">
          <input type="checkbox" class="cleanup-switch" data-batch-backup ${batch.backup ? 'checked' : ''}>
          <span class="uninstall-saves-text">
            <strong>Back up saves first</strong>
            <span>Each game’s saves are zipped to your backups folder before it’s removed</span>
          </span>
        </label>` : ''}
    </div>
    <footer class="modal-foot">${foot}</footer>`;
}

modal.layer.addEventListener('change', event => {
  const box = event.target.closest('[data-batch-backup]');
  if (box && cleanup.batch) cleanup.batch.backup = box.checked;
});

modal.layer.addEventListener('click', event => {
  const batch = cleanup.batch;
  if (!batch || !modal.panel || !modal.panel.classList.contains('batch-modal')) return;
  if (event.target.closest('[data-batch-confirm]') && !batch.running) {
    const ids = batch.items.filter(item => item.method !== 'none').map(item => item.id);
    batch.running = true;
    for (const id of ids) batch.status[id] = { status: 'pending' };
    renderBatch();
    send({ type: 'batchUninstall', ids, backup: batch.backup });
  } else if (event.target.closest('[data-batch-skip]')) {
    send({ type: 'batchSkip' });
  } else if (event.target.closest('[data-batch-stop]')) {
    batch.stopping = true;
    send({ type: 'batchCancel' });
    renderBatch();
  } else if (event.target.closest('[data-batch-leftovers]')) {
    const id = event.target.closest('[data-batch-leftovers]').dataset.batchLeftovers;
    const snapshot = batch.snapshots && batch.snapshots[id];
    if (snapshot) openLeftovers(snapshot);
  }
});

modal.layer.addEventListener('click', event => {
  const batch = cleanup.batch;
  if (batch && batch.running && event.target.closest('.modal-backdrop') && modal.panel && modal.panel.classList.contains('batch-modal')) {
    event.stopImmediatePropagation();
  }
}, true);

window.addEventListener('keydown', event => {
  const batch = cleanup.batch;
  if (event.key !== 'Escape' || !batch || !batch.running || !modal.panel || !modal.panel.classList.contains('batch-modal')) return;
  event.preventDefault();
  event.stopImmediatePropagation();
}, true);

function openLeftovers(snapshot) {
  cleanup.leftovers = { game: snapshot, token: null, folders: null, registry: [], selectedFolders: new Set(), selectedKeys: new Set(), backup: true, busy: false, error: null };
  modal.open(`
    <header class="modal-head">
      <h2>Leftovers of ${escapeHtml(snapshot.Name)}</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="modal-body"><div class="uninstall-loading"><span class="measuring"></span><span class="move-muted">Searching AppData, Documents and Saved Games…</span></div></div>`, 'uninstall-modal leftovers-modal');
  send({ type: 'leftoversScan', id: snapshot.Id, requestId: snapshot.Id, game: snapshot, developers: snapshot.Developers || [] });
}

function leftoverBytes() {
  const scan = cleanup.leftovers;
  return (scan.folders || []).filter(item => scan.selectedFolders.has(item.Path)).reduce((sum, item) => sum + (item.Bytes || 0), 0);
}

function renderLeftovers() {
  const scan = cleanup.leftovers;
  const panel = modal.panel;
  if (!scan || !scan.folders || !panel || !panel.classList.contains('leftovers-modal')) return;
  const folders = scan.folders;
  const registry = scan.registry;

  const head = `
    <header class="modal-head">
      <h2>Leftovers of ${escapeHtml(scan.game.Name)}</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>`;

  if (!folders.length && !registry.length) {
    panel.innerHTML = `
      ${head}
      <div class="modal-body">
        ${scan.error ? `<div class="callout">${ICON.alert}<div>${escapeHtml(scan.error)}</div></div>` : `<div class="cleanup-empty">${ICON.check}<strong>Nothing left behind</strong><span>No folders or settings named after ${escapeHtml(scan.game.Name)} were found.</span></div>`}
      </div>
      <footer class="modal-foot"><span class="grow"></span><button class="primary-button compact" data-modal-close>Done</button></footer>`;
    return;
  }

  const folderRows = folders.map(item => `
    <label class="remove-row ${item.Reason ? 'blocked' : ''}">
      <input type="checkbox" data-leftover-path="${escapeHtml(item.Path)}" ${scan.selectedFolders.has(item.Path) ? 'checked' : ''} ${item.Reason || scan.busy ? 'disabled' : ''}>
      <span class="remove-check">${ICON.check}</span>
      <span class="remove-text">
        <span class="remove-label">${escapeHtml(item.Label)}${item.Saves ? '<span class="cleanup-tag save-data">Save data</span>' : ''}</span>
        <span class="remove-path" title="${escapeHtml(item.Path)}">${escapeHtml(item.Path)}</span>
        ${item.Reason ? `<span class="remove-reason">${ICON.lock}${escapeHtml(item.Reason)}</span>` : ''}
      </span>
      <span class="remove-size">${formatBytes(item.Bytes)}</span>
    </label>`).join('');

  const registryRows = registry.map(item => `
    <label class="remove-row">
      <input type="checkbox" data-leftover-key="${escapeHtml(item.Key)}" ${scan.selectedKeys.has(item.Key) ? 'checked' : ''} ${scan.busy ? 'disabled' : ''}>
      <span class="remove-check">${ICON.check}</span>
      <span class="remove-text">
        <span class="remove-label">${escapeHtml(item.Reason)}</span>
        <span class="remove-path" title="HKEY_CURRENT_USER\\${escapeHtml(item.Key)}">HKEY_CURRENT_USER\\${escapeHtml(item.Key)}</span>
      </span>
      <span class="remove-size">${item.Values} ${item.Values === 1 ? 'value' : 'values'}${item.SubKeys ? ` · ${item.SubKeys} ${item.SubKeys === 1 ? 'key' : 'keys'}` : ''}</span>
    </label>`).join('');

  const savesTicked = folders.some(item => item.Saves && scan.selectedFolders.has(item.Path)) || scan.selectedKeys.size > 0;
  const count = scan.selectedFolders.size + scan.selectedKeys.size;
  const bytes = leftoverBytes();
  const total = [
    scan.selectedFolders.size ? `<strong>${formatBytes(bytes)}</strong> to the Recycle Bin` : '',
    scan.selectedKeys.size ? `${scan.selectedKeys.size} registry ${scan.selectedKeys.size === 1 ? 'key' : 'keys'} after a .reg backup` : '',
  ].filter(Boolean).join(' · ');

  panel.innerHTML = `
    ${head}
    <div class="modal-body">
      ${scan.error ? `<div class="callout">${ICON.alert}<div>${escapeHtml(scan.error)}</div></div>` : ''}
      ${folders.length ? `<h3 class="section-title">Folders and files</h3><div class="remove-list">${folderRows}</div>` : ''}
      ${registry.length ? `<h3 class="section-title">Registry settings</h3><div class="remove-list">${registryRows}</div>` : ''}
      ${savesTicked ? `
        <label class="uninstall-saves">
          <input type="checkbox" class="cleanup-switch" data-leftover-backup ${scan.backup ? 'checked' : ''} ${scan.busy ? 'disabled' : ''}>
          <span class="uninstall-saves-text">
            <strong>Back up the save data first</strong>
            <span>Ticked saves and registry keys are zipped to your backups folder</span>
          </span>
        </label>` : ''}
    </div>
    <footer class="modal-foot">
      <span class="grow remove-total">${total}</span>
      <button class="secondary-button compact" data-modal-close>Keep all</button>
      <button class="danger-button ${scan.busy ? 'busy' : ''}" data-leftover-confirm ${count && !scan.busy ? '' : 'disabled'}>${scan.busy ? ICON.refresh : ICON.trash}${scan.busy ? 'Cleaning up…' : `Remove ${count || ''} ${count === 1 ? 'item' : 'items'}`}</button>
    </footer>`;
}

modal.layer.addEventListener('change', event => {
  const scan = cleanup.leftovers;
  if (!scan || !modal.panel || !modal.panel.classList.contains('leftovers-modal')) return;
  const path = event.target.closest('[data-leftover-path]');
  const key = event.target.closest('[data-leftover-key]');
  const backup = event.target.closest('[data-leftover-backup]');
  if (path) {
    if (path.checked) scan.selectedFolders.add(path.dataset.leftoverPath);
    else scan.selectedFolders.delete(path.dataset.leftoverPath);
  } else if (key) {
    if (key.checked) scan.selectedKeys.add(key.dataset.leftoverKey);
    else scan.selectedKeys.delete(key.dataset.leftoverKey);
  } else if (backup) {
    scan.backup = backup.checked;
    return;
  } else {
    return;
  }
  renderLeftovers();
});

modal.layer.addEventListener('click', event => {
  const scan = cleanup.leftovers;
  if (!scan || scan.busy || !event.target.closest('[data-leftover-confirm]')) return;
  scan.busy = true;
  renderLeftovers();
  send({ type: 'leftoversRecycle', token: scan.token, paths: [...scan.selectedFolders], registry: [...scan.selectedKeys], backup: scan.backup });
});

function showLeftoversPrompt(game) {
  const card = document.createElement('div');
  card.className = 'cleanup-prompt';
  card.innerHTML = `
    <span class="cleanup-prompt-icon">${ICON.broom}</span>
    <span class="cleanup-prompt-text"><strong>${escapeHtml(game.Name)} was uninstalled</strong><span>Look for settings and files it left behind?</span></span>
    <button class="secondary-button compact" data-prompt-open>Look now</button>
    <button class="icon-button" data-prompt-close title="Dismiss">${ICON.close}</button>`;
  document.body.appendChild(card);
  requestAnimationFrame(() => card.classList.add('open'));
  const dismiss = () => {
    card.classList.remove('open');
    setTimeout(() => card.remove(), 260);
  };
  const timer = setTimeout(dismiss, 30 * 1000);
  card.addEventListener('click', event => {
    if (event.target.closest('[data-prompt-open]')) {
      clearTimeout(timer);
      dismiss();
      openLeftovers(game);
    } else if (event.target.closest('[data-prompt-close]')) {
      clearTimeout(timer);
      dismiss();
      send({ type: 'leftoversDismiss', id: game.Id });
    }
  });
}

extensions.menuItems.push(game => game.Status !== 'installed' ? `<button class="menu-item" data-menu="find-leftovers">${ICON.broom}Find leftover settings…</button>` : '');
extensions.menuActions['find-leftovers'] = game => openLeftovers(removalSnapshot(game));

function cleanupItems() {
  return (cleanup.scan && cleanup.scan.items) || [];
}

function cleanupSelectedItems() {
  return cleanupItems().filter(item => cleanup.selected.has(item.Path));
}

function cleanupRowHtml(item) {
  const selected = cleanup.selected.has(item.Path);
  const owner = item.Game ? `For ${item.Game}` : 'No matching game in your library';
  return `
    <label class="remove-row cleanup-row ${item.Reason ? 'blocked' : ''}">
      <input type="checkbox" data-cleanup-path="${escapeHtml(item.Path)}" ${selected ? 'checked' : ''} ${item.Reason || cleanup.busy ? 'disabled' : ''}>
      <span class="remove-check">${ICON.check}</span>
      <span class="remove-text">
        <span class="remove-label">${escapeHtml(item.Path.split('\\').pop())}<span class="cleanup-tag">${escapeHtml(item.Label)}</span></span>
        <span class="remove-path" title="${escapeHtml(item.Path)}">${escapeHtml(owner)} · ${escapeHtml(item.Path)}</span>
        ${item.Reason ? `<span class="remove-reason">${ICON.lock}${escapeHtml(item.Reason)}</span>` : ''}
      </span>
      <span class="cleanup-row-meta">
        <span class="remove-size">${formatBytes(item.Bytes)}</span>
        <span class="cleanup-row-date">${escapeHtml(relativeTime(item.Modified) || '')}</span>
      </span>
      <button class="icon-button" data-cleanup-reveal="${escapeHtml(item.Path)}" title="Show in File Explorer">${ICON.folder}</button>
    </label>`;
}

function cleanupSectionHtml(title, items, empty, note) {
  const total = items.reduce((sum, item) => sum + (item.Bytes || 0), 0);
  const allSelected = items.filter(item => !item.Reason).every(item => cleanup.selected.has(item.Path));
  return `
    <section class="cleanup-section">
      <h2 class="shelf-title">${title}<span class="count">${items.length ? `${formatCount(items.length)} · ${formatBytes(total)}` : ''}</span>
        ${items.some(item => !item.Reason) ? `<button class="text-button cleanup-select-all" data-cleanup-all="${escapeHtml(title)}">${allSelected ? 'Select none' : 'Select all'}</button>` : ''}
      </h2>
      ${note ? `<div class="cleanup-note">${note}</div>` : ''}
      ${items.length ? `<div class="remove-list">${items.map(cleanupRowHtml).join('')}</div>` : `<div class="cleanup-section-empty">${empty}</div>`}
    </section>`;
}

function renderCleanupView() {
  elements.pageTitle.textContent = 'Cleanup';
  elements.zoomControl.classList.add('hidden');
  const items = cleanupItems();
  const selected = cleanupSelectedItems();
  const selectedBytes = selected.reduce((sum, item) => sum + (item.Bytes || 0), 0);
  const total = items.reduce((sum, item) => sum + (item.Bytes || 0), 0);

  elements.filters.innerHTML = `<button class="chip ${cleanup.scanning ? 'active' : ''}" data-cleanup-rescan>${ICON.refresh}${cleanup.scanning ? 'Scanning…' : 'Scan again'}</button>`;

  if (!cleanup.scan) {
    elements.summary.innerHTML = 'Looking for installers and downloads you no longer need…';
    elements.results.innerHTML = `<div class="cleanup-view"><div class="uninstall-loading"><span class="measuring"></span></div></div>`;
    if (!cleanup.scanning) {
      cleanup.scanning = true;
      send({ type: 'cleanupScan' });
    }
    return;
  }

  elements.summary.innerHTML = items.length
    ? `<strong>${formatBytes(total)}</strong> in ${formatCount(items.length)} ${items.length === 1 ? 'item' : 'items'} · checked ${escapeHtml((relativeTime(cleanup.scan.scannedAt) || 'just now').toLowerCase())}`
    : `Nothing to clean up · checked ${escapeHtml((relativeTime(cleanup.scan.scannedAt) || 'just now').toLowerCase())}`;

  const hydra = items.filter(item => item.Kind === 'hydra');
  const downloads = items.filter(item => item.Kind === 'downloads');
  const roots = (cleanup.scan.hydraRoots || []).map(root => `<code>${escapeHtml(root)}</code>`).join(' ');

  elements.results.innerHTML = `
    <div class="cleanup-view">
      ${cleanup.scan.error ? `<div class="callout">${ICON.alert}<div>${escapeHtml(cleanup.scan.error)}</div></div>` : ''}
      <div class="callout neutral">${ICON.shield}<div>Only installers for games that are already installed are ticked. Everything goes to the Recycle Bin, so you can restore it.</div></div>
      ${cleanupSectionHtml('Hydra downloads', hydra, 'No leftover Hydra downloads.', roots ? `Hydra download folders: ${roots}` : '')}
      ${cleanupSectionHtml('Installers in Downloads', downloads, 'No installers for games you already have.', cleanup.scan.downloads ? `<code>${escapeHtml(cleanup.scan.downloads)}</code>` : '')}
      <div class="cleanup-dock ${selected.length ? 'open' : ''}">
        <span class="batch-count"><strong>${formatCount(selected.length)}</strong> selected</span>
        <span class="batch-size">${formatBytes(selectedBytes)}</span>
        <span class="batch-spacer"></span>
        <button class="danger-button ${cleanup.busy ? 'busy' : ''}" data-cleanup-recycle ${selected.length && !cleanup.busy ? '' : 'disabled'}>${cleanup.busy ? ICON.refresh : ICON.trash}${cleanup.busy ? 'Working…' : 'Move to Recycle Bin…'}</button>
      </div>
    </div>`;
}

function openCleanupConfirm() {
  const items = cleanupSelectedItems();
  if (!items.length) return;
  const bytes = items.reduce((sum, item) => sum + (item.Bytes || 0), 0);
  modal.open(`
    <header class="modal-head">
      <h2>Move ${items.length} ${items.length === 1 ? 'item' : 'items'} to the Recycle Bin</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="modal-body">
      <div class="remove-list">
        ${items.map(item => `
          <div class="remove-row blocked cleanup-confirm-row">
            <span class="remove-check">${item.IsFolder ? ICON.folder : ICON.download}</span>
            <span class="remove-text">
              <span class="remove-label">${escapeHtml(item.Game || item.Label)}</span>
              <span class="remove-path" title="${escapeHtml(item.Path)}">${escapeHtml(item.Path)}</span>
            </span>
            <span class="remove-size">${formatBytes(item.Bytes)}</span>
          </div>`).join('')}
      </div>
    </div>
    <footer class="modal-foot">
      <span class="grow remove-total">Frees about <strong>${formatBytes(bytes)}</strong></span>
      <button class="secondary-button compact" data-modal-close>Cancel</button>
      <button class="danger-button" data-cleanup-confirm>${ICON.trash}Move to Recycle Bin</button>
    </footer>`, 'uninstall-modal cleanup-modal');
}

modal.layer.addEventListener('click', event => {
  if (!event.target.closest('[data-cleanup-confirm]') || cleanup.busy) return;
  cleanup.busy = true;
  send({ type: 'cleanupRecycle', paths: cleanupSelectedItems().map(item => item.Path) });
  modal.close();
  if (state.view === 'cleanup') renderCleanupView();
});

elements.results.addEventListener('change', event => {
  if (state.view !== 'cleanup') return;
  const box = event.target.closest('[data-cleanup-path]');
  if (!box) return;
  if (box.checked) cleanup.selected.add(box.dataset.cleanupPath);
  else cleanup.selected.delete(box.dataset.cleanupPath);
  renderCleanupView();
});

elements.results.addEventListener('click', event => {
  if (state.view !== 'cleanup') return;
  const reveal = event.target.closest('[data-cleanup-reveal]');
  if (reveal) {
    event.preventDefault();
    send({ type: 'cleanupReveal', path: reveal.dataset.cleanupReveal });
    return;
  }
  const all = event.target.closest('[data-cleanup-all]');
  if (all) {
    const kind = all.dataset.cleanupAll === 'Hydra downloads' ? 'hydra' : 'downloads';
    const items = cleanupItems().filter(item => item.Kind === kind && !item.Reason);
    const select = !items.every(item => cleanup.selected.has(item.Path));
    for (const item of items) {
      if (select) cleanup.selected.add(item.Path);
      else cleanup.selected.delete(item.Path);
    }
    renderCleanupView();
    return;
  }
  if (event.target.closest('[data-cleanup-recycle]')) openCleanupConfirm();
});

elements.filters.addEventListener('click', event => {
  if (state.view !== 'cleanup' || !event.target.closest('[data-cleanup-rescan]') || cleanup.scanning) return;
  cleanup.scanning = true;
  send({ type: 'cleanupScan' });
  renderCleanupView();
});

extensions.views.cleanup = { render: renderCleanupView };

function decorateCleanupNav() {
  const sections = [...elements.sidebar.querySelectorAll('.nav-section')];
  const storage = sections.find(section => {
    const heading = section.querySelector('.nav-heading');
    return heading && heading.textContent.trim() === 'Storage';
  });
  if (!storage || storage.querySelector('[data-view="cleanup"]')) return;
  const count = cleanup.scan ? cleanupItems().filter(item => item.Selected && !item.Reason).length : 0;
  const html = `
    <button class="nav-item cleanup-nav ${state.view === 'cleanup' ? 'active' : ''}" data-view="cleanup" title="Installers and downloads you no longer need">
      <span class="nav-icon">${ICON.broom}</span>
      <span class="nav-label">Cleanup</span>
      ${count ? `<span class="nav-count">${count}</span>` : '<span class="nav-count"></span>'}
    </button>`;
  const anchor = storage.querySelector('[data-view="storage"]') || storage.querySelector('.nav-heading');
  if (anchor) anchor.insertAdjacentHTML('afterend', html);
  else storage.insertAdjacentHTML('afterbegin', html);
}

new MutationObserver(decorateCleanupNav).observe(elements.sidebar, { childList: true });
decorateCleanupNav();

function cleanupSettingsHtml() {
  const info = saves.info;
  if (!info) {
    send({ type: 'savesInfo' });
    return `<div class="cleanup-settings" data-cleanup-settings><div class="uninstall-loading"><span class="measuring"></span></div></div>`;
  }
  const keep = Number(info.keep) || 10;
  const custom = info.backupFolder && info.defaultBackupFolder && info.backupFolder.toLowerCase() !== info.defaultBackupFolder.toLowerCase();
  const progress = cleanup.backupAll;
  const running = progress && progress.running;
  const percent = running && progress.total ? (progress.done / progress.total) * 100 : 0;
  const oneDriveSuggestion = !info.syncFolder && info.oneDrive;
  const manifestLine = info.manifestAvailable
    ? `Updated ${escapeHtml((relativeTime(info.manifestUpdatedAt) || 'recently').toLowerCase())}`
    : 'Not downloaded yet';

  return `
    <div class="cleanup-settings" data-cleanup-settings>
      <div class="cleanup-setting">
        <span class="cleanup-setting-text">
          <span class="cleanup-setting-label">Backup folder</span>
          <span class="cleanup-setting-path" title="${escapeHtml(info.backupFolder)}">${escapeHtml(info.backupFolder)}</span>
        </span>
        <span class="cleanup-setting-actions">
          <button class="text-button" data-saves-open-root>${ICON.folder}Open</button>
          ${custom ? '<button class="text-button" data-saves-reset-folder>Reset</button>' : ''}
          <button class="secondary-button compact" data-saves-pick="saves.backupFolder">Change…</button>
        </span>
      </div>
      <div class="cleanup-setting">
        <span class="cleanup-setting-text">
          <span class="cleanup-setting-label">Backups to keep per game</span>
          <span class="cleanup-setting-hint">Older backups go to the Recycle Bin.</span>
        </span>
        <div class="chip-group">
          ${[3, 5, 10, 20, 50].map(value => `<button class="${keep === value ? 'active' : ''}" data-saves-keep="${value}">${value}</button>`).join('')}
        </div>
      </div>
      <div class="cleanup-setting">
        <span class="cleanup-setting-text">
          <span class="cleanup-setting-label">Copy backups to</span>
          ${info.syncFolder
            ? `<span class="cleanup-setting-path" title="${escapeHtml(info.syncFolder)}">${escapeHtml(info.syncFolder)}</span>`
            : `<span class="cleanup-setting-hint">${oneDriveSuggestion ? 'OneDrive is set up on this PC. Copying there keeps saves safe if this PC fails.' : 'Pick a cloud-synced folder to keep a second copy.'}</span>`}
        </span>
        <span class="cleanup-setting-actions">
          ${info.syncFolder ? '<button class="text-button" data-saves-sync-off>Turn off</button>' : ''}
          ${oneDriveSuggestion ? '<button class="secondary-button compact" data-saves-onedrive>Use OneDrive</button>' : ''}
          <button class="secondary-button compact" data-saves-pick="saves.syncFolder">${info.syncFolder ? 'Change…' : 'Choose…'}</button>
        </span>
      </div>
      <label class="cleanup-setting">
        <span class="cleanup-setting-text">
          <span class="cleanup-setting-label">Back up saves before uninstalling</span>
          <span class="cleanup-setting-hint">Ticked by default in the uninstall dialog when saves are found.</span>
        </span>
        <input type="checkbox" class="cleanup-switch" data-saves-before-uninstall ${setting('saves.backupBeforeUninstall', true) !== false ? 'checked' : ''}>
      </label>
      <div class="cleanup-setting">
        <span class="cleanup-setting-text">
          <span class="cleanup-setting-label">Back up all games</span>
          <span class="cleanup-setting-hint">${running
            ? `${escapeHtml(progress.name || '')} · ${progress.done} of ${progress.total}`
            : progress && progress.total
              ? `Last run: ${progress.backedUp} backed up${progress.failed ? `, ${progress.failed} failed` : ''}${progress.cancelled ? ', stopped early' : ''}.`
              : 'Zips the saves of every installed game that has them.'}</span>
          ${running ? `<span class="move-progress-bar"><i style="width:${percent}%"></i></span>` : ''}
        </span>
        <span class="cleanup-setting-actions">
          ${running
            ? '<button class="secondary-button compact" data-saves-cancel-all>Stop</button>'
            : '<button class="secondary-button compact" data-saves-backup-all>' + ICON.save + 'Back up all</button>'}
        </span>
      </div>
      <div class="cleanup-setting">
        <span class="cleanup-setting-text">
          <span class="cleanup-setting-label">Save locations list</span>
          <span class="cleanup-setting-hint">${manifestLine}. From the Ludusavi project, checked weekly.</span>
        </span>
        <span class="cleanup-setting-actions">
          <button class="text-button ${cleanup.manifestLoading ? 'busy' : ''}" data-saves-manifest>${ICON.refresh}${cleanup.manifestLoading ? 'Updating…' : 'Update now'}</button>
        </span>
      </div>
    </div>`;
}

function refreshCleanupSettings() {
  const container = document.querySelector('[data-cleanup-settings]');
  if (container) container.outerHTML = cleanupSettingsHtml();
}

extensions.settingsSections.push({
  id: 'saves',
  title: 'Saves & cleanup',
  render: cleanupSettingsHtml,
  bind() {},
});

document.addEventListener('click', event => {
  if (!event.target.closest('[data-cleanup-settings]')) return;
  const target = event.target;
  const pick = target.closest('[data-saves-pick]');
  const keep = target.closest('[data-saves-keep]');
  if (pick) send({ type: 'savesPickFolder', key: pick.dataset.savesPick });
  else if (keep) {
    setSetting('saves.keep', Number(keep.dataset.savesKeep) === 10 ? null : Number(keep.dataset.savesKeep));
    if (saves.info) saves.info.keep = Number(keep.dataset.savesKeep);
    refreshCleanupSettings();
  } else if (target.closest('[data-saves-open-root]')) send({ type: 'savesOpenFolder' });
  else if (target.closest('[data-saves-reset-folder]')) {
    setSetting('saves.backupFolder', null);
    send({ type: 'savesInfo' });
  } else if (target.closest('[data-saves-sync-off]')) {
    setSetting('saves.syncFolder', null);
    send({ type: 'savesInfo' });
  } else if (target.closest('[data-saves-onedrive]') && saves.info && saves.info.oneDrive) {
    setSetting('saves.syncFolder', `${saves.info.oneDrive.replace(/\\+$/, '')}\\Game Hub Saves`);
    send({ type: 'savesInfo' });
  } else if (target.closest('[data-saves-backup-all]')) {
    cleanup.backupAll = { running: true, done: 0, total: 0, name: 'Starting' };
    send({ type: 'savesBackupAll' });
    refreshCleanupSettings();
  } else if (target.closest('[data-saves-cancel-all]')) send({ type: 'savesCancelAll' });
  else if (target.closest('[data-saves-manifest]') && !cleanup.manifestLoading) {
    cleanup.manifestLoading = true;
    send({ type: 'savesRefreshManifest' });
    refreshCleanupSettings();
  }
});

document.addEventListener('change', event => {
  const box = event.target.closest('[data-saves-before-uninstall]');
  if (box) setSetting('saves.backupBeforeUninstall', box.checked ? null : false);
});

extensions.messageHandlers.push(message => {
  switch (message.type) {
    case 'cleanupScanning':
      cleanup.scanning = true;
      if (state.view === 'cleanup') renderCleanupView();
      break;
    case 'cleanupScan': {
      cleanup.scanning = false;
      cleanup.busy = false;
      cleanup.scan = message;
      const paths = new Set((message.items || []).map(item => item.Path));
      const keep = [...cleanup.selected].filter(path => paths.has(path));
      cleanup.selected = new Set(keep.length ? keep : (message.items || []).filter(item => item.Selected && !item.Reason).map(item => item.Path));
      if (state.view === 'cleanup') renderCleanupView();
      const nav = elements.sidebar.querySelector('[data-view="cleanup"]');
      if (nav) {
        nav.remove();
        decorateCleanupNav();
      }
      break;
    }
    case 'cleanupResult':
      cleanup.busy = false;
      toast(message.message, message.ok ? 'info' : 'error', message.ok ? ICON.check : ICON.alert);
      if (state.view === 'cleanup') renderCleanupView();
      break;
    case 'batchUninstallPlan':
      if (!cleanup.batch) return;
      cleanup.batch.items = message.items || [];
      cleanup.batch.snapshots = Object.fromEntries(cleanup.batch.items.map(item => {
        const game = state.byId.get(item.id);
        return [item.id, game ? removalSnapshot(game) : null];
      }));
      renderBatch();
      break;
    case 'batchUninstallProgress':
      if (!cleanup.batch) return;
      cleanup.batch.current = message.id;
      cleanup.batch.status[message.id] = { status: message.status, message: message.message };
      renderBatch();
      break;
    case 'batchUninstallDone': {
      const batch = cleanup.batch;
      if (!batch) return;
      batch.running = false;
      batch.done = true;
      batch.current = null;
      for (const result of message.results || []) batch.status[result.id] = { status: result.status, message: result.message };
      const results = message.results || [];
      const removed = results.filter(result => result.status === 'removed').length;
      const opened = results.filter(result => result.status === 'opened' || result.status === 'skipped').length;
      const failed = results.filter(result => ['failed', 'blocked', 'partial'].includes(result.status)).length;
      batch.summary = [removed ? `${removed} removed` : '', opened ? `${opened} to finish in ${opened === 1 ? 'its launcher' : 'their launchers'}` : '', failed ? `${failed} not removed` : ''].filter(Boolean).join(' · ') || message.message || '';
      for (const result of results) {
        if (result.status === 'removed') state.selection.delete(result.id);
      }
      applySelection();
      renderBatch();
      toast(`Batch uninstall finished${batch.summary ? `: ${batch.summary}` : ''}`, failed ? 'error' : 'info', failed ? ICON.alert : ICON.check);
      break;
    }
    case 'leftoversScan': {
      const scan = cleanup.leftovers;
      if (!scan || scan.game.Id !== message.requestId) return;
      scan.token = message.token;
      scan.folders = message.folders || [];
      scan.registry = message.registry || [];
      scan.error = message.error || null;
      scan.selectedFolders = new Set(scan.folders.filter(item => item.Selected && !item.Reason).map(item => item.Path));
      scan.selectedKeys = new Set(scan.registry.filter(item => item.Selected).map(item => item.Key));
      renderLeftovers();
      break;
    }
    case 'leftoversResult': {
      const scan = cleanup.leftovers;
      toast(message.message, message.ok ? 'info' : 'error', message.ok ? ICON.check : ICON.alert);
      if (scan) scan.busy = false;
      if (message.ok && modal.panel && modal.panel.classList.contains('leftovers-modal')) modal.close();
      else if (scan && message.message && message.message.includes('out of date')) openLeftovers(scan.game);
      else renderLeftovers();
      break;
    }
    case 'leftoversPrompt':
      if (message.game) showLeftoversPrompt(message.game);
      break;
    case 'savesManifestLoading':
      cleanup.manifestLoading = true;
      refreshCleanupSettings();
      break;
    case 'savesInfo':
      cleanup.manifestLoading = false;
      if (message.running && !(cleanup.backupAll && cleanup.backupAll.running)) cleanup.backupAll = { running: true, done: 0, total: 0 };
      break;
    case 'savesBackupAllProgress':
      cleanup.backupAll = message;
      refreshCleanupSettings();
      if (!message.running) {
        const text = message.cancelled
          ? `Stopped. Backed up ${message.backedUp} ${message.backedUp === 1 ? 'game' : 'games'}.`
          : `Backed up saves for ${message.backedUp} ${message.backedUp === 1 ? 'game' : 'games'}${message.bytes ? ` (${formatBytes(message.bytes)})` : ''}${message.failed ? `. ${message.failed} failed.` : '.'}`;
        toast(text, message.failed ? 'error' : 'info', message.failed ? ICON.alert : ICON.check);
      }
      break;
  }
});
