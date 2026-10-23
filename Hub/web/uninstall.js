'use strict';

ICON.trash = '<svg viewBox="0 0 24 24" class="glyph"><path d="M4.5 6.5h15M9.5 6.5V4.8a1.3 1.3 0 0 1 1.3-1.3h2.4a1.3 1.3 0 0 1 1.3 1.3v1.7M6.5 6.5l.8 12.1a2 2 0 0 0 2 1.9h5.4a2 2 0 0 0 2-1.9l.8-12.1M10 10.5v6M14 10.5v6"/></svg>';
ICON.shield = '<svg viewBox="0 0 24 24" class="glyph"><path d="M12 3.5 5 6v5.5c0 4.3 2.9 7.8 7 9 4.1-1.2 7-4.7 7-9V6z"/><path d="m9 12 2.2 2.2L15.5 10"/></svg>';
ICON.lock = '<svg viewBox="0 0 24 24" class="glyph"><rect x="5" y="10.5" width="14" height="10" rx="2"/><path d="M8.5 10.5V8a3.5 3.5 0 0 1 7 0v2.5"/></svg>';
ICON.verify = '<svg viewBox="0 0 24 24" class="glyph"><path d="M12 3.5 5 6v5.5c0 4.3 2.9 7.8 7 9 4.1-1.2 7-4.7 7-9V6z"/><path d="m9 12 2.2 2.2L15.5 10"/></svg>';
ICON.move = '<svg viewBox="0 0 24 24" class="glyph"><rect x="3" y="13" width="18" height="7" rx="2"/><path d="M7 16.5h.01M12 3.5v6.5m0 0-3-3m3 3 3-3"/></svg>';

const remover = {
  game: null,
  plan: null,
  selected: new Set(),
  busy: false,
  savesScan: null,
  backupSaves: true,
  backupTouched: false,
  stage: '',
  error: '',
};

const CONFIRM_LABELS = {
  steam: 'Open Steam uninstall',
  launcher: 'Open Epic library',
  uninstaller: 'Run uninstaller',
  recycle: 'Move to Recycle Bin',
};

function uninstallBackupDefault() {
  return setting('saves.backupBeforeUninstall', true) !== false;
}

function openUninstall(game) {
  const cached = typeof savesScanFor === 'function' ? savesScanFor(game.Id) : null;
  Object.assign(remover, { game, plan: null, selected: new Set(), busy: false, savesScan: cached, backupSaves: uninstallBackupDefault(), backupTouched: false, stage: '', error: '' });
  modal.open(`
    <header class="modal-head">
      <h2>${game.Status === 'installed' ? 'Uninstall' : 'Remove'} ${escapeHtml(game.Name)}</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="modal-body"><div class="uninstall-loading"><span class="measuring"></span></div></div>`, 'uninstall-modal');
  send({ type: 'uninstallPlan', id: game.Id });
  if (game.Status === 'installed' && typeof requestSavesScan === 'function') requestSavesScan(game.Id, true);
}

function onUninstallSavesScan(message) {
  if (!remover.game || remover.game.Id !== message.id || remover.busy) return;
  remover.savesScan = message;
  if (remover.plan) renderUninstall();
}

function uninstallHasSaves() {
  const scan = remover.savesScan;
  if (!scan || scan.loading) return false;
  return (scan.locations || []).some(location => !location.tooLarge) || (scan.registry || []).length > 0;
}

function folderSize(path) {
  const record = state.sizes[pathKey(path)];
  return record ? record.Bytes : null;
}

function plannedBytes() {
  const plan = remover.plan;
  let bytes = 0;
  if (plan.Method === 'steam' || plan.Method === 'uninstaller' || plan.Method === 'launcher') {
    const install = remover.game.InstallPath;
    if (install) bytes += folderSize(install) || 0;
    if (plan.Method === 'steam') {
      for (const part of remover.game.Parts || []) bytes += folderSize(part.Path) || 0;
    }
  }
  for (const path of remover.selected) bytes += folderSize(path) || 0;
  return bytes;
}

function uninstallSavesHtml() {
  const { game, plan } = remover;
  if (game.Status !== 'installed' || plan.Method === 'launcher') return '';
  const scan = remover.savesScan;
  if (!scan || scan.loading) {
    return `<div class="uninstall-saves pending"><span class="measuring"></span><span>Checking for saves…</span></div>`;
  }
  if (!uninstallHasSaves()) return '';
  const count = (scan.locations || []).length + (scan.registry || []).length;
  return `
    <label class="uninstall-saves">
      <input type="checkbox" class="cleanup-switch" data-uninstall-backup ${remover.backupSaves ? 'checked' : ''} ${remover.busy ? 'disabled' : ''}>
      <span class="uninstall-saves-text">
        <strong>Back up saves first</strong>
        <span>${count} ${count === 1 ? 'location' : 'locations'}${scan.totalBytes ? ` · ${formatBytes(scan.totalBytes)}` : ''} saved to your backups folder</span>
      </span>
    </label>`;
}

function renderUninstall() {
  const panel = modal.panel;
  if (!panel || !remover.plan) return;
  const { game, plan } = remover;

  if (plan.Method === 'none') {
    panel.innerHTML = `
      <header class="modal-head">
        <h2>Can't remove ${escapeHtml(game.Name)} safely</h2>
        <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
      </header>
      <div class="modal-body">
        <div class="callout">${ICON.lock}<div>${escapeHtml(plan.Blocked)}</div></div>
      </div>
      <footer class="modal-foot"><span class="grow"></span><button class="secondary-button compact" data-modal-close>Close</button></footer>`;
    return;
  }

  const folders = plan.Folders || [];
  const folderRows = folders.map(folder => {
    const bytes = folderSize(folder.Path);
    const checked = remover.selected.has(folder.Path);
    return `
      <label class="remove-row ${folder.Allowed ? '' : 'blocked'}">
        <input type="checkbox" data-remove-path="${escapeHtml(folder.Path)}" ${checked ? 'checked' : ''} ${folder.Allowed && !remover.busy ? '' : 'disabled'}>
        <span class="remove-check">${ICON.check}</span>
        <span class="remove-text">
          <span class="remove-label">${escapeHtml(folder.Label)}</span>
          <span class="remove-path" title="${escapeHtml(folder.Path)}">${escapeHtml(folder.Path)}</span>
          ${folder.Allowed ? '' : `<span class="remove-reason">${ICON.lock}${escapeHtml(folder.Reason)}</span>`}
        </span>
        <span class="remove-size">${bytes == null ? '' : formatBytes(bytes)}</span>
      </label>`;
  }).join('');

  const folderTitle = plan.Method === 'recycle' ? 'Folders to move to the Recycle Bin' : 'Also clean up (optional)';
  const bytes = plannedBytes();
  const canConfirm = !remover.busy && (plan.Method !== 'recycle' || remover.selected.size > 0);
  const busyLabel = remover.stage === 'backup' ? 'Backing up saves…' : 'Working…';

  panel.innerHTML = `
    <header class="modal-head">
      <h2>${game.Status === 'installed' ? 'Uninstall' : 'Remove'} ${escapeHtml(game.Name)}</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="modal-body">
      <div class="remove-method">
        <span class="remove-method-icon">${plan.Method === 'recycle' ? ICON.trash : ICON.shield}</span>
        <div>
          <strong>${escapeHtml(plan.Title)}</strong>
          <p>${escapeHtml(plan.Description)}</p>
          ${plan.Command && plan.Method === 'uninstaller' ? `<code>${escapeHtml(plan.Command)}</code>` : ''}
        </div>
      </div>
      ${plan.Warning ? `<div class="callout">${ICON.alert}<div>${escapeHtml(plan.Warning)}</div></div>` : ''}
      ${remover.error ? `<div class="callout">${ICON.alert}<div>${escapeHtml(remover.error)}</div></div>` : ''}
      ${uninstallSavesHtml()}
      ${folders.length ? `<h3 class="section-title">${folderTitle}</h3><div class="remove-list">${folderRows}</div>` : ''}
    </div>
    <footer class="modal-foot">
      <span class="grow remove-total">${bytes ? `Frees about <strong>${formatBytes(bytes)}</strong>` : ''}</span>
      <button class="secondary-button compact" data-modal-close>Cancel</button>
      <button class="danger-button ${remover.busy ? 'busy' : ''}" data-remove-confirm ${canConfirm ? '' : 'disabled'}>${remover.busy ? ICON.refresh : ICON.trash}${remover.busy ? busyLabel : plan.ActionLabel || CONFIRM_LABELS[plan.Method]}</button>
    </footer>`;
}

modal.layer.addEventListener('change', event => {
  if (!remover.plan) return;
  const backup = event.target.closest('[data-uninstall-backup]');
  if (backup) {
    remover.backupSaves = backup.checked;
    remover.backupTouched = true;
    return;
  }
  const box = event.target.closest('[data-remove-path]');
  if (!box) return;
  if (box.checked) remover.selected.add(box.dataset.removePath);
  else remover.selected.delete(box.dataset.removePath);
  renderUninstall();
});

async function confirmUninstall() {
  const game = remover.game;
  remover.busy = true;
  remover.error = '';
  if (remover.backupSaves && uninstallHasSaves() && remover.plan.Method !== 'launcher' && typeof backupBeforeUninstall === 'function') {
    remover.stage = 'backup';
    renderUninstall();
    const result = await backupBeforeUninstall(game.Id);
    if (remover.game !== game) return;
    if (!result.ok && !result.empty) {
      remover.busy = false;
      remover.stage = '';
      remover.error = `${result.message} Nothing was uninstalled. Turn off the backup to uninstall anyway.`;
      renderUninstall();
      return;
    }
    if (result.ok) toast(result.message, 'info', ICON.check);
  }
  remover.stage = 'uninstall';
  renderUninstall();
  send({ type: 'uninstall', id: game.Id, folders: [...remover.selected] });
}

modal.layer.addEventListener('click', event => {
  if (!event.target.closest('[data-remove-confirm]') || remover.busy || !remover.plan) return;
  confirmUninstall();
});

function removalSnapshot(game) {
  const meta = state.meta && state.meta[game.Id];
  return {
    Id: game.Id,
    Name: game.Name,
    Source: game.Source,
    InstallPath: game.InstallPath,
    Exe: game.Exe,
    SteamAppId: game.SteamAppId,
    Developers: (meta && meta.Developers) || [],
  };
}

extensions.messageHandlers.push(message => {
  if (message.type === 'uninstallPlan') {
    if (!remover.game || remover.game.Id !== message.id) return;
    remover.plan = message.plan;
    remover.selected = new Set((message.plan.Folders || []).filter(folder => folder.Selected).map(folder => folder.Path));
    renderUninstall();
  } else if (message.type === 'uninstallResult') {
    if (!remover.game || remover.game.Id !== message.id) return;
    remover.busy = false;
    remover.stage = '';
    toast(message.message, message.ok ? 'info' : 'error', message.ok ? ICON.check : ICON.alert);
    if (message.ok) {
      const game = remover.game;
      const method = remover.plan && remover.plan.Method;
      const snapshot = removalSnapshot(game);
      modal.close();
      if (method === 'recycle') {
        closeDrawer();
        if (game.Status === 'installed' && typeof openLeftovers === 'function') setTimeout(() => openLeftovers(snapshot), 380);
      } else if (method === 'steam' || method === 'launcher' || method === 'uninstaller') {
        send({ type: 'cleanupTrackRemoval', id: game.Id, developers: snapshot.Developers });
      }
    } else {
      renderUninstall();
    }
  }
});

function canVerify(game) {
  return game.Status === 'installed' && (game.Source === 'Steam' || game.Source === 'Epic');
}

function canMove(game) {
  return game.Status === 'installed' && !!game.InstallPath;
}

extensions.drawerSections.push(game => `
  <div class="manage">
    <h3 class="section-title">Manage</h3>
    <div class="manage-actions">
      <button class="secondary-button compact" data-action="change-art">${ICON.image}Change artwork</button>
      ${canVerify(game) ? `<button class="secondary-button compact" data-action="verify-files" title="Ask ${game.Source === 'Steam' ? 'Steam' : 'Epic Games'} to check the game's files and repair any that are missing or damaged">${ICON.verify}Verify files</button>` : ''}
      ${canMove(game) ? `<button class="secondary-button compact" data-action="move-game">${ICON.move}Move to another drive</button>` : ''}
      <button class="danger-outline-button" data-action="uninstall">${ICON.trash}${game.Status === 'installed' ? 'Uninstall' : 'Remove files'}</button>
    </div>
  </div>`);
extensions.drawerActions.uninstall = game => openUninstall(game);
extensions.drawerActions['verify-files'] = game => send({ type: 'verifyFiles', id: game.Id });
extensions.drawerActions['move-game'] = game => {
  if (typeof openMove === 'function') openMove(game);
};
extensions.menuItems.push(game => `<div class="menu-separator"></div><button class="menu-item danger" data-menu="uninstall">${ICON.trash}${game.Status === 'installed' ? 'Uninstall…' : 'Remove files…'}</button>`);
extensions.menuActions.uninstall = game => openUninstall(game);
