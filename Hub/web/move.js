'use strict';

const mover = {
  game: null,
  plan: null,
  picked: null,
  progress: null,
  running: false,
};

const MOVE_PHASES = {
  checking: 'Checking the game isn’t running…',
  moving: 'Moving…',
  copying: 'Copying files',
  verifying: 'Checking every file arrived…',
  recycling: 'Moving the old copy to the Recycle Bin…',
};

function openMove(game) {
  Object.assign(mover, { game, plan: null, picked: null, progress: null, running: false });
  modal.open(`
    <header class="modal-head">
      <h2>Move ${escapeHtml(game.Name)}</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="modal-body"><div class="uninstall-loading"><span class="measuring"></span></div></div>`, 'uninstall-modal move-modal');
  send({ type: 'movePlan', id: game.Id });
}

function moveHead() {
  return `
    <header class="modal-head">
      <h2>Move ${escapeHtml(mover.game.Name)}</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>`;
}

function moveSteps(steps) {
  return `<ol class="move-steps">${steps.map(step => `<li>${step}</li>`).join('')}</ol>`;
}

function driveLetter(path) {
  return String(path || '').slice(0, 2).toUpperCase();
}

function moveDrivesHtml(plan) {
  const roots = (plan.roots || []).filter(root => !root.current);
  if (!roots.length) return `<div class="callout neutral">${ICON.info}<div>No other drives were found. You can still pick a folder on this drive.</div></div>`;
  return `
    <div class="move-drives">
      ${roots.map(root => {
        const used = root.total ? Math.min(100, ((root.total - root.free) / root.total) * 100) : 0;
        const fits = root.free > (plan.bytes || 0);
        return `
          <button class="move-drive ${fits ? '' : 'tight'}" data-move-drive="${escapeHtml(root.path)}" title="Choose a folder on ${escapeHtml(driveLetter(root.path))}">
            <span class="move-drive-icon">${ICON.drive}</span>
            <span class="move-drive-text">
              <span class="move-drive-name">${escapeHtml(root.label)} (${escapeHtml(driveLetter(root.path))})</span>
              <span class="move-drive-bar"><i style="width:${used}%"></i></span>
              <span class="move-drive-free">${formatBytes(root.free)} free${fits ? '' : ' · not enough space'}</span>
            </span>
          </button>`;
      }).join('')}
    </div>`;
}

function renderMove() {
  const panel = modal.panel;
  const plan = mover.plan;
  if (!panel || !plan || !mover.game || !panel.classList.contains('move-modal')) return;
  const game = mover.game;

  if (plan.kind === 'measuring') {
    panel.innerHTML = `${moveHead()}<div class="modal-body"><div class="uninstall-loading"><span class="measuring"></span><span class="move-muted">Measuring the game folder…</span></div></div>`;
    return;
  }

  if (plan.kind === 'steam') {
    panel.innerHTML = `
      ${moveHead()}
      <div class="modal-body">
        <div class="remove-method">
          <span class="remove-method-icon">${ICON.move}</span>
          <div>
            <strong>Steam moves its own games</strong>
            <p>Steam copies the files, checks them and updates its records, so the game keeps its updates, cloud saves and workshop items.</p>
          </div>
        </div>
        ${moveSteps([
          'Click <strong>Open in Steam</strong> below.',
          'In the properties window, choose <strong>Installed Files</strong>.',
          'Click <strong>Move install folder</strong>, pick the drive and confirm.',
          'If the drive isn’t listed, add a library folder on it in Steam under <strong>Settings › Storage</strong> first.',
        ])}
      </div>
      <footer class="modal-foot">
        <span class="grow"></span>
        <button class="secondary-button compact" data-modal-close>Close</button>
        <button class="primary-button compact" data-move-steam>${ICON.external}Open in Steam</button>
      </footer>`;
    return;
  }

  if (plan.kind === 'epic') {
    panel.innerHTML = `
      ${moveHead()}
      <div class="modal-body">
        <div class="remove-method">
          <span class="remove-method-icon">${ICON.move}</span>
          <div>
            <strong>Epic Games has no move button</strong>
            <p>You can still move the game without downloading it again. Epic finds the files at the new place and only checks them.</p>
          </div>
        </div>
        ${moveSteps([
          'Close the Epic Games Launcher completely, including from the tray.',
          `Move the game folder${game.InstallPath ? ` <code>${escapeHtml(game.InstallPath)}</code>` : ''} to the new drive in File Explorer.`,
          'Open Epic, find the game in your library and click <strong>Install</strong>.',
          'Choose the folder that now <em>contains</em> the game folder. Epic verifies the files instead of downloading them.',
        ])}
      </div>
      <footer class="modal-foot">
        <span class="grow"></span>
        <button class="secondary-button compact" data-modal-close>Close</button>
        <button class="primary-button compact" data-move-epic>${ICON.external}Open Epic library</button>
      </footer>`;
    return;
  }

  if (plan.kind !== 'copy') {
    panel.innerHTML = `
      ${moveHead()}
      <div class="modal-body"><div class="callout">${ICON.lock}<div>${escapeHtml(plan.reason || 'This game can’t be moved from here.')}</div></div></div>
      <footer class="modal-foot"><span class="grow"></span><button class="secondary-button compact" data-modal-close>Close</button></footer>`;
    return;
  }

  if (mover.running) {
    const progress = mover.progress || { phase: 'checking' };
    const percent = progress.total ? Math.min(100, (progress.done / progress.total) * 100) : 0;
    const detail = progress.phase === 'copying'
      ? `${formatBytes(progress.done)} of ${formatBytes(progress.total)} · ${formatCount(progress.files || 0)} of ${formatCount(progress.totalFiles || 0)} files`
      : '';
    panel.innerHTML = `
      ${moveHead()}
      <div class="modal-body">
        <div class="move-progress">
          <div class="move-progress-label"><strong>${escapeHtml(MOVE_PHASES[progress.phase] || 'Working…')}</strong><span>${progress.phase === 'copying' ? `${Math.floor(percent)}%` : ''}</span></div>
          <div class="move-progress-bar ${progress.phase === 'copying' ? '' : 'indeterminate'}"><i style="width:${progress.phase === 'copying' ? percent : 100}%"></i></div>
          <div class="move-progress-detail">${detail}</div>
        </div>
        <div class="move-route">
          <span class="remove-path" title="${escapeHtml(plan.source)}">${escapeHtml(plan.source)}</span>
          ${ICON.arrowDown}
          <span class="remove-path" title="${escapeHtml(mover.picked && mover.picked.target)}">${escapeHtml(mover.picked && mover.picked.target)}</span>
        </div>
      </div>
      <footer class="modal-foot">
        <span class="grow remove-total">You can keep using Game Hub while this runs.</span>
        <button class="secondary-button compact" data-move-cancel ${progress.phase === 'recycling' ? 'disabled' : ''}>Cancel move</button>
      </footer>`;
    return;
  }

  const picked = mover.picked;
  const sameDrive = picked && driveLetter(picked.target) === driveLetter(plan.source);
  const tooSmall = picked && !sameDrive && picked.free >= 0 && picked.free < (plan.bytes || 0);
  const problem = plan.running
    ? `Close ${game.Name} first. ${plan.running} is running from its folder.`
    : picked && picked.reason
      ? picked.reason
      : tooSmall
        ? `Not enough space. The game needs ${formatBytes(plan.bytes)} and ${driveLetter(picked.target)} has ${formatBytes(picked.free)} free.`
        : '';
  const explain = !picked
    ? ''
    : sameDrive
      ? 'Same drive, so the folder is moved in one step without copying.'
      : `Game Hub copies ${formatBytes(plan.bytes)} (${formatCount(plan.files || 0)} files), checks that every file and byte arrived, then moves the original folder to the Recycle Bin.`;

  panel.innerHTML = `
    ${moveHead()}
    <div class="modal-body">
      <h3 class="section-title">From</h3>
      <div class="remove-row blocked move-source">
        <span class="remove-check">${ICON.folder}</span>
        <span class="remove-text"><span class="remove-path" title="${escapeHtml(plan.source)}">${escapeHtml(plan.source)}</span></span>
        <span class="remove-size">${formatBytes(plan.bytes)}</span>
      </div>
      ${plan.warning ? `<div class="callout">${ICON.alert}<div>${escapeHtml(plan.warning)}</div></div>` : ''}
      <h3 class="section-title">To</h3>
      ${moveDrivesHtml(plan)}
      <div class="move-target ${picked ? '' : 'empty'}">
        <span class="move-target-text">
          ${picked ? `<span class="remove-label">New folder</span><span class="remove-path" title="${escapeHtml(picked.target)}">${escapeHtml(picked.target)}</span>` : '<span class="move-muted">Pick a drive above or choose any folder.</span>'}
        </span>
        <button class="secondary-button compact" data-move-pick>${ICON.folder}${picked ? 'Change…' : 'Choose folder…'}</button>
      </div>
      ${problem ? `<div class="callout">${ICON.alert}<div>${escapeHtml(problem)}</div></div>` : explain ? `<div class="callout neutral">${ICON.info}<div>${escapeHtml(explain)}</div></div>` : ''}
    </div>
    <footer class="modal-foot">
      <span class="grow"></span>
      <button class="secondary-button compact" data-modal-close>Cancel</button>
      <button class="primary-button compact" data-move-start ${picked && !problem ? '' : 'disabled'}>${ICON.move}Move game</button>
    </footer>`;
}

modal.layer.addEventListener('click', event => {
  if (!mover.game || !modal.panel || !modal.panel.classList.contains('move-modal')) return;
  const id = mover.game.Id;
  if (event.target.closest('[data-move-steam]')) {
    send({ type: 'moveOpenSteam', id });
    modal.close();
  } else if (event.target.closest('[data-move-epic]')) {
    send({ type: 'openLauncher', id });
    modal.close();
  } else if (event.target.closest('[data-move-pick]')) {
    send({ type: 'movePickFolder', id });
  } else if (event.target.closest('[data-move-drive]')) {
    send({ type: 'movePickFolder', id, start: event.target.closest('[data-move-drive]').dataset.moveDrive });
  } else if (event.target.closest('[data-move-start]') && mover.picked && !mover.running) {
    mover.running = true;
    mover.progress = { phase: 'checking' };
    renderMove();
    send({ type: 'moveStart', id, destination: mover.picked.path });
  } else if (event.target.closest('[data-move-cancel]')) {
    send({ type: 'moveCancel' });
  }
});

function migrateGameId(from, to) {
  if (!from || !to || from === to) return;
  if (state.favorites.has(from)) {
    state.favorites.delete(from);
    state.favorites.add(to);
    writeStorage('hub.favorites', [...state.favorites]);
  }
  if (state.userData[from]) {
    state.userData[to] = Object.assign({}, state.userData[from], state.userData[to] || {});
    delete state.userData[from];
  }
  if (state.selectedId === from) closeDrawer();
}

extensions.messageHandlers.push(message => {
  if (message.type === 'movePlan') {
    if (!mover.game || mover.game.Id !== message.id) return;
    mover.plan = message;
    renderMove();
  } else if (message.type === 'movePicked') {
    if (!mover.game || mover.game.Id !== message.id) return;
    mover.picked = message;
    renderMove();
  } else if (message.type === 'moveProgress') {
    if (!mover.running) return;
    mover.progress = message;
    renderMove();
  } else if (message.type === 'moveDone') {
    const wasOpen = mover.game && mover.game.Id === message.id;
    mover.running = false;
    toast(message.message, message.ok ? 'info' : message.cancelled ? 'info' : 'error', message.ok ? ICON.check : ICON.info);
    if (!wasOpen) return;
    if (message.ok) modal.close();
    else renderMove();
  } else if (message.type === 'gameMoved') {
    migrateGameId(message.from, message.to);
  }
});
