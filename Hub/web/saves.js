'use strict';

ICON.save = '<svg viewBox="0 0 24 24" class="glyph"><path d="M5 4.5h11l3.5 3.5v10a2 2 0 0 1-2 2h-11a2 2 0 0 1-2-2V6.5a2 2 0 0 1 .5-2z"/><path d="M8 4.5V9h7V4.5M8 20v-5.5h8V20"/></svg>';
ICON.history = '<svg viewBox="0 0 24 24" class="glyph"><path d="M3.5 12a8.5 8.5 0 1 0 2.5-6"/><path d="M3.5 4v4.5H8M12 8v4.5l3 2"/></svg>';
ICON.registry = '<svg viewBox="0 0 24 24" class="glyph"><rect x="4" y="4" width="7" height="7" rx="1.5"/><rect x="13" y="4" width="7" height="7" rx="1.5"/><rect x="4" y="13" width="7" height="7" rx="1.5"/><path d="M16.5 13.5v6M13.5 16.5h6"/></svg>';

const saves = {
  scans: new Map(),
  requested: new Map(),
  busy: new Map(),
  info: null,
  pendingBackup: null,
};

function savesScanFor(id) {
  return saves.scans.get(id) || null;
}

function requestSavesScan(id, force = false) {
  const last = saves.requested.get(id) || 0;
  if (!force && Date.now() - last < 60 * 1000) return;
  saves.requested.set(id, Date.now());
  send({ type: 'savesScan', id });
}

function savesSupported(game) {
  return game && game.Status === 'installed';
}

function backupDateLabel(iso) {
  const relative = relativeTime(iso);
  const date = formatDate(iso);
  return relative && date ? `${date} · ${relative.charAt(0).toLowerCase()}${relative.slice(1)}` : date || relative || 'Unknown date';
}

function savesSectionHtml(game) {
  if (!savesSupported(game)) return '';
  const scan = savesScanFor(game.Id);
  const busy = saves.busy.get(game.Id);
  const head = `<h3 class="section-title">Saves</h3>`;

  if (!scan || scan.loading) {
    return `
      <section class="saves" data-saves-section="${escapeHtml(game.Id)}">
        ${head}
        <div class="saves-loading"><span class="measuring"></span><span>${scan && scan.loading ? 'Downloading the save locations list…' : 'Looking for saves…'}</span></div>
      </section>`;
  }

  const locations = scan.locations || [];
  const registry = scan.registry || [];
  const backups = scan.backups || [];
  const found = locations.length || registry.length;

  const locationRows = locations.map(location => `
    <button class="saves-location" data-action="saves-reveal" data-path="${escapeHtml(location.path)}" title="Show in File Explorer">
      <span class="saves-location-icon">${location.isFolder ? ICON.folder : ICON.save}</span>
      <span class="saves-location-path">${escapeHtml(location.path)}</span>
      <span class="saves-location-size">${location.tooLarge ? 'Too large to back up' : formatBytes(location.bytes)}</span>
    </button>`).join('');
  const registryRows = registry.map(key => `
    <div class="saves-location registry" title="${escapeHtml(key)}">
      <span class="saves-location-icon">${ICON.registry}</span>
      <span class="saves-location-path">${escapeHtml(key)}</span>
      <span class="saves-location-size">Registry</span>
    </div>`).join('');

  const backupRows = backups.slice(0, 6).map(backup => `
    <div class="saves-backup">
      <span class="saves-backup-icon">${ICON.history}</span>
      <span class="saves-backup-text">
        <span class="saves-backup-date">${escapeHtml(backupDateLabel(backup.createdAt))}</span>
        <span class="saves-backup-meta">${escapeHtml(backup.reason || 'Backup')} · ${formatBytes(backup.bytes)}</span>
      </span>
      <button class="text-button" data-action="saves-restore" data-path="${escapeHtml(backup.path)}" ${busy ? 'disabled' : ''}>${ICON.undo}Restore</button>
    </div>`).join('');

  let empty = '';
  if (!found) {
    empty = scan.available === false
      ? `<div class="callout neutral">${ICON.info}<div>The save locations list couldn't be downloaded. Game Hub tries again later.</div></div>`
      : scan.manifestName
        ? `<div class="saves-empty">No save files were found on this PC yet.</div>`
        : `<div class="saves-empty">Game Hub doesn't know where this game keeps its saves.</div>`;
  }

  return `
    <section class="saves" data-saves-section="${escapeHtml(game.Id)}">
      ${head}
      ${scan.error ? `<div class="callout">${ICON.alert}<div>${escapeHtml(scan.error)}</div></div>` : ''}
      ${found ? `<div class="saves-locations">${locationRows}${registryRows}</div>` : empty}
      <div class="saves-actions">
        <button class="secondary-button compact ${busy === 'backup' ? 'busy' : ''}" data-action="saves-backup" ${found && !busy ? '' : 'disabled'}>${busy === 'backup' ? ICON.refresh : ICON.save}${busy === 'backup' ? 'Backing up…' : 'Back up now'}</button>
        <button class="secondary-button compact" data-action="saves-open">${ICON.folder}Open backups</button>
        ${found && scan.totalBytes ? `<span class="saves-total">${formatBytes(scan.totalBytes)} of saves</span>` : ''}
      </div>
      ${backups.length ? `
        <div class="saves-backups">
          <div class="saves-subhead">Backups <span class="count">${backups.length}</span></div>
          ${backupRows}
          ${backups.length > 6 ? `<button class="link-button" data-action="saves-open">${ICON.folder}${backups.length - 6} older in the backups folder</button>` : ''}
        </div>` : ''}
    </section>`;
}

function refreshSavesSection(id) {
  if (state.selectedId !== id) return;
  const section = elements.drawer.querySelector('[data-saves-section]');
  const game = state.byId.get(id);
  if (section && game) section.outerHTML = savesSectionHtml(game);
}

function openRestore(game, path) {
  const scan = savesScanFor(game.Id);
  const backup = scan && (scan.backups || []).find(entry => entry.path === path);
  if (!backup) return;
  const targets = (scan.locations || []).map(location => `
    <div class="remove-row blocked saves-target">
      <span class="remove-check">${location.isFolder ? ICON.folder : ICON.save}</span>
      <span class="remove-text">
        <span class="remove-path" title="${escapeHtml(location.path)}">${escapeHtml(location.path)}</span>
      </span>
      <span class="remove-size">${formatBytes(location.bytes)}</span>
    </div>`).join('');

  modal.open(`
    <header class="modal-head">
      <h2>Restore ${escapeHtml(game.Name)} saves</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="modal-body">
      <div class="remove-method">
        <span class="remove-method-icon">${ICON.history}</span>
        <div>
          <strong>${escapeHtml(backupDateLabel(backup.createdAt))}</strong>
          <p>${escapeHtml(backup.reason || 'Backup')} · ${formatBytes(backup.bytes)}. Files in the backup replace the ones on this PC. Your current saves are backed up first, so you can undo this.</p>
        </div>
      </div>
      ${targets ? `<h3 class="section-title">Locations that get overwritten</h3><div class="remove-list">${targets}</div>` : ''}
    </div>
    <footer class="modal-foot">
      <span class="grow remove-total">Close the game before restoring.</span>
      <button class="secondary-button compact" data-modal-close>Cancel</button>
      <button class="primary-button compact" data-saves-restore-confirm="${escapeHtml(path)}" data-game="${escapeHtml(game.Id)}">${ICON.undo}Restore</button>
    </footer>`, 'uninstall-modal saves-modal');
}

modal.layer.addEventListener('click', event => {
  const confirm = event.target.closest('[data-saves-restore-confirm]');
  if (!confirm) return;
  const id = confirm.dataset.game;
  saves.busy.set(id, 'restore');
  send({ type: 'savesRestore', id, path: confirm.dataset.savesRestoreConfirm });
  modal.close();
  refreshSavesSection(id);
  toast('Restoring saves…', 'info', ICON.history);
});

extensions.drawerSections.push(game => {
  if (!savesSupported(game)) return '';
  requestSavesScan(game.Id);
  return savesSectionHtml(game);
});

extensions.drawerActions['saves-backup'] = game => {
  saves.busy.set(game.Id, 'backup');
  refreshSavesSection(game.Id);
  send({ type: 'savesBackup', id: game.Id, reason: 'Manual backup' });
};
extensions.drawerActions['saves-open'] = game => send({ type: 'savesOpenFolder', id: game.Id });
extensions.drawerActions['saves-reveal'] = (game, element) => send({ type: 'savesReveal', path: element.dataset.path });
extensions.drawerActions['saves-restore'] = (game, element) => openRestore(game, element.dataset.path);

function backupBeforeUninstall(id) {
  return new Promise(resolve => {
    saves.pendingBackup = { id, resolve };
    send({ type: 'savesBackup', id, reason: 'Before uninstall' });
  });
}

extensions.messageHandlers.push(message => {
  if (message.type === 'savesScan') {
    saves.scans.set(message.id, message);
    if (!message.loading) saves.busy.delete(message.id);
    refreshSavesSection(message.id);
    if (typeof onUninstallSavesScan === 'function') onUninstallSavesScan(message);
  } else if (message.type === 'savesBackupResult') {
    saves.busy.delete(message.id);
    const pending = saves.pendingBackup;
    if (pending && pending.id === message.id) {
      saves.pendingBackup = null;
      pending.resolve(message);
    } else {
      toast(message.message, message.ok ? 'info' : message.empty ? 'info' : 'error', message.ok ? ICON.check : ICON.info);
    }
    refreshSavesSection(message.id);
  } else if (message.type === 'savesRestoreResult') {
    saves.busy.delete(message.id);
    toast(message.message, message.ok ? 'info' : 'error', message.ok ? ICON.check : ICON.alert);
    refreshSavesSection(message.id);
  } else if (message.type === 'savesInfo') {
    saves.info = message;
    if (typeof refreshCleanupSettings === 'function') refreshCleanupSettings();
  }
});
