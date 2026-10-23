'use strict';

ICON.stopSquare = '<svg viewBox="0 0 24 24" class="glyph filled"><rect x="6" y="6" width="12" height="12" rx="2.5"/></svg>';
ICON.chart = '<svg viewBox="0 0 24 24" class="glyph"><path d="M4 20h16"/><rect x="5.5" y="11" width="3" height="6.5" rx="1"/><rect x="10.5" y="6.5" width="3" height="11" rx="1"/><rect x="15.5" y="13.5" width="3" height="4" rx="1"/></svg>';
ICON.dice = '<svg viewBox="0 0 24 24" class="glyph"><rect x="4" y="4" width="16" height="16" rx="3.5"/><circle cx="9" cy="9" r="1.1" class="dot"/><circle cx="15" cy="9" r="1.1" class="dot"/><circle cx="12" cy="12" r="1.1" class="dot"/><circle cx="9" cy="15" r="1.1" class="dot"/><circle cx="15" cy="15" r="1.1" class="dot"/></svg>';
ICON.trophy = '<svg viewBox="0 0 24 24" class="glyph"><path d="M8 4.5h8v5a4 4 0 0 1-8 0z"/><path d="M8 6.5H5.5a2.5 2.5 0 0 0 2.6 3.5M16 6.5h2.5a2.5 2.5 0 0 1-2.6 3.5M12 13.5v3M8.5 19.5h7M9.5 19.5l.5-3h4l.5 3"/></svg>';
ICON.sliders = '<svg viewBox="0 0 24 24" class="glyph"><path d="M4 7h9M17 7h3M4 17h3M11 17h9"/><circle cx="15" cy="7" r="2"/><circle cx="9" cy="17" r="2"/></svg>';

VIEWS.stats = 'Stats';

const PLAY_COLORS = {
  steam: 'var(--steam)',
  epic: 'var(--epic)',
  hydra: 'var(--hydra)',
  riot: '#ff6b74',
  xbox: '#6cc04a',
  hoyoplay: '#7fc8ff',
  ea: '#ff8a5c',
  rockstar: '#f5c84c',
  local: 'var(--local)',
  downloads: 'var(--downloads)',
};

const ON_LAUNCH_OPTIONS = [
  { key: 'none', label: 'Stay open' },
  { key: 'minimize', label: 'Minimize' },
  { key: 'tray', label: 'Hide in the tray' },
];

const UNDER_HOURS = [0, 2, 5, 10, 20, 50];

const play = {
  sessions: [],
  tracked: {},
  reported: {},
  achievements: {},
  statsRange: 'weekly',
  stopping: null,
  candidates: {},
  picker: { platform: '', unplayed: false, underHours: 0, favorites: false, installed: true, spinning: false, pick: null },
  ticker: null,
  pill: null,
};

function playParse(iso) {
  const time = iso ? new Date(iso).getTime() : NaN;
  return Number.isFinite(time) ? time : null;
}

function playClock(startedAt) {
  const start = playParse(startedAt);
  if (start == null) return '';
  const seconds = Math.max(0, Math.floor((Date.now() - start) / 1000));
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const rest = String(seconds % 60).padStart(2, '0');
  return hours ? `${hours}:${String(minutes).padStart(2, '0')}:${rest}` : `${minutes}:${rest}`;
}

function playHours(minutes) {
  if (!minutes) return '0 h';
  if (minutes < 60) return `${minutes} min`;
  const hours = minutes / 60;
  return `${hours >= 100 ? formatCount(Math.round(hours)) : hours.toFixed(1).replace(/\.0$/, '')} h`;
}

function playSessionWhen(session) {
  const start = new Date(session.Start);
  const day = start.toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' });
  const time = start.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
  return `${day}, ${time}`;
}

function playThumb(game, className = 'play-thumb') {
  const image = game.IconUrl || game.CoverUrl;
  if (image) return `<span class="${className}"><img src="${escapeHtml(image)}" alt="" loading="lazy"></span>`;
  return `<span class="${className} empty" style="--hue:${hueOf(game.Name)}">${escapeHtml(game.Name.charAt(0).toUpperCase())}</span>`;
}

function runningIds() {
  return Object.keys(state.running).filter(id => state.byId.has(id));
}

function isRunning(game) {
  return !!(game && state.running[game.Id]);
}

function setRunning(games) {
  const previous = JSON.stringify(state.running);
  state.running = {};
  for (const entry of games || []) {
    if (entry && entry.id) state.running[entry.id] = { startedAt: entry.startedAt, pid: entry.pid };
  }
  if (JSON.stringify(state.running) === previous) return;
  renderPill();
  scheduleRender(60);
}

function ensureTicker() {
  const active = runningIds().length > 0;
  if (active && !play.ticker) {
    play.ticker = setInterval(tick, 1000);
  } else if (!active && play.ticker) {
    clearInterval(play.ticker);
    play.ticker = null;
  }
}

function tick() {
  for (const element of document.querySelectorAll('[data-play-elapsed]')) {
    element.textContent = playClock(element.dataset.playElapsed);
  }
}

function renderPill() {
  if (!play.pill) {
    play.pill = document.createElement('div');
    play.pill.className = 'play-pill';
    play.pill.setAttribute('role', 'status');
    document.body.appendChild(play.pill);
    play.pill.addEventListener('click', event => {
      const stop = event.target.closest('[data-play-stop]');
      const open = event.target.closest('[data-play-open]');
      if (stop) {
        confirmStop(state.byId.get(stop.dataset.playStop));
      } else if (open) {
        openDrawer(open.dataset.playOpen);
      }
    });
  }

  const ids = runningIds().sort((first, second) => (playParse(state.running[second].startedAt) || 0) - (playParse(state.running[first].startedAt) || 0));
  document.body.classList.toggle('play-running', ids.length > 0);
  ensureTicker();
  if (!ids.length) {
    play.pill.classList.remove('visible');
    return;
  }

  const game = state.byId.get(ids[0]);
  const entry = state.running[game.Id];
  const others = ids.length - 1;
  play.pill.innerHTML = `
    <button class="play-pill-main" data-play-open="${escapeHtml(game.Id)}" title="Show ${escapeHtml(game.Name)}">
      ${playThumb(game, 'play-pill-art')}
      <span class="play-pill-text">
        <span class="play-pill-label"><i class="play-live"></i>Now playing${others ? ` · +${others} more` : ''}</span>
        <span class="play-pill-name">${escapeHtml(game.Name)}</span>
      </span>
      <span class="play-pill-time" data-play-elapsed="${escapeHtml(entry.startedAt)}">${playClock(entry.startedAt)}</span>
    </button>
    <button class="play-pill-stop" data-play-stop="${escapeHtml(game.Id)}" title="Stop ${escapeHtml(game.Name)}">${ICON.stopSquare}</button>`;
  play.pill.classList.add('visible');
}

function confirmStop(game) {
  if (!game) return;
  if (!isRunning(game)) {
    toast(`${game.Name} isn't running any more.`);
    return;
  }
  play.stopping = null;
  const entry = state.running[game.Id];
  modal.open(`
    <header class="modal-head">
      <h2>Stop ${escapeHtml(game.Name)}?</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="modal-body">
      <div class="play-stop-summary">
        ${playThumb(game, 'play-stop-art')}
        <div>
          <div class="play-stop-name">${escapeHtml(game.Name)}</div>
          <div class="play-stop-meta">Running for <span data-play-elapsed="${escapeHtml(entry.startedAt)}">${playClock(entry.startedAt)}</span></div>
        </div>
      </div>
      <div class="callout">${ICON.alert}<div>This force-closes the game and everything it started. <strong>Unsaved progress will be lost.</strong> Quitting from inside the game is safer when you can.</div></div>
    </div>
    <footer class="modal-foot">
      <span class="grow"></span>
      <button class="secondary-button compact" data-modal-close>Cancel</button>
      <button class="danger-button" data-play-confirm-stop="${escapeHtml(game.Id)}">${ICON.stopSquare}Stop game</button>
    </footer>`, 'play-stop-modal');
}

document.getElementById('modalLayer').addEventListener('click', event => {
  const confirm = event.target.closest('[data-play-confirm-stop]');
  if (!confirm || confirm.disabled) return;
  play.stopping = confirm.dataset.playConfirmStop;
  confirm.disabled = true;
  confirm.classList.add('busy');
  confirm.innerHTML = `${ICON.refresh}Stopping…`;
  send({ type: 'stopGame', id: play.stopping });
});

extensions.launchGuards.push(game => {
  if (!isRunning(game)) return true;
  toast(`${game.Name} is already running.`, 'info', ICON.play);
  return false;
});

extensions.cardBadges.push(game => (isRunning(game) ? '<span class="play-card-running"><i class="play-live"></i>Running</span>' : ''));

function applyDrawerRunning() {
  const game = state.byId.get(state.selectedId);
  if (!game || !isRunning(game)) return;
  const button = elements.drawer.querySelector('.actions [data-action="play"]');
  if (!button) return;
  const entry = state.running[game.Id];
  button.dataset.action = 'stop-game';
  button.disabled = false;
  button.classList.add('play-stop-button');
  button.title = `Stop ${game.Name}`;
  button.innerHTML = `${ICON.stopSquare}Stop<span class="play-stop-elapsed" data-play-elapsed="${escapeHtml(entry.startedAt)}">${playClock(entry.startedAt)}</span>`;
}

new MutationObserver(applyDrawerRunning).observe(elements.drawer, { childList: true });

extensions.drawerActions['stop-game'] = game => confirmStop(game);

extensions.drawerStats.push(game => {
  const progress = play.achievements[game.Id];
  if (!progress || !progress.Total) return '';
  const percent = Math.round((progress.Unlocked / progress.Total) * 100);
  const title = progress.LastUnlocked ? `Last unlocked ${relativeTime(progress.LastUnlocked).toLowerCase()}` : `${percent}% unlocked`;
  return `
    <div class="stat play-achievements" title="${escapeHtml(title)}">
      <div class="stat-label">Achievements</div>
      <div class="stat-value">${formatCount(progress.Unlocked)}<span class="play-of"> / ${formatCount(progress.Total)}</span></div>
      <div class="play-meter ${percent === 100 ? 'complete' : ''}"><i style="width:${percent}%"></i></div>
    </div>`;
});

extensions.drawerStats.push(game => {
  const tracked = play.tracked[game.Id] || 0;
  if (!tracked) return '';
  const reported = play.reported[game.Id] || 0;
  const title = reported ? `${platformOf(game).launcher || 'The launcher'} reports ${formatPlaytime(reported)}. Game Hub shows whichever is higher.` : 'Time Game Hub saw this game running';
  return `
    <div class="stat" title="${escapeHtml(title)}">
      <div class="stat-label">Tracked here</div>
      <div class="stat-value">${escapeHtml(formatPlaytime(tracked))}</div>
    </div>`;
});

function sessionsHtml(game) {
  const sessions = play.sessions.filter(session => session.Id === game.Id).sort((first, second) => (playParse(second.Start) || 0) - (playParse(first.Start) || 0));
  if (!sessions.length && game.Status !== 'installed') return '';
  const longest = sessions.reduce((max, session) => Math.max(max, session.Minutes || 0), 0);
  const rows = sessions.slice(0, 5).map(session => `
    <div class="play-session">
      <span class="play-session-when">${escapeHtml(playSessionWhen(session))}</span>
      <span class="play-session-bar"><i style="width:${longest ? Math.max(4, (session.Minutes / longest) * 100) : 0}%"></i></span>
      <span class="play-session-length">${escapeHtml(formatPlaytime(session.Minutes))}</span>
    </div>`).join('');
  return `
    <h3 class="section-title play-section-title">Sessions${sessions.length ? `<span class="count">${formatCount(sessions.length)}</span>` : ''}</h3>
    <div class="play-sessions">
      ${rows || '<div class="play-empty-line">No sessions yet. Game Hub records them while the game is running.</div>'}
      ${sessions.length > 5 ? `<button class="text-button play-more" data-play-view-stats>See all in Stats</button>` : ''}
    </div>`;
}

function launchOptionsHtml(game) {
  if (game.Status !== 'installed' || !game.CanLaunch) return '';
  const data = gameData(game.Id);
  const platform = platformOf(game);
  const url = game.Url || '';
  const viaLauncher = !!url && !url.toLowerCase().startsWith('shell:');
  const canDirect = viaLauncher && (!!game.InstallPath || !!game.Exe);
  const direct = canDirect && data['launch.direct'] === true;
  const isSteam = game.Source === 'Steam';
  const programLaunch = !url || direct;
  const argsApply = programLaunch || isSteam;
  const adminApplies = programLaunch;
  const launcher = platform.launcher || platform.label;
  const candidates = play.candidates[game.Id];
  const chosen = data['launch.exe'];
  const program = chosen || (candidates && candidates.current) || '';

  if (direct && !candidates) send({ type: 'playExeCandidates', id: game.Id });

  const programRow = direct ? `
    <div class="play-option play-program">
      <div class="play-option-text">
        <span class="play-option-label">Program</span>
        <span class="play-option-path mono" title="${escapeHtml(program)}">${program ? escapeHtml(program) : candidates ? 'Not found. Choose the game’s program.' : '<span class="measuring"></span>'}</span>
      </div>
      <div class="play-program-actions">
        ${candidates && candidates.items.length > 1 ? `
          <select class="play-select" data-play-exe>
            ${candidates.items.map(item => `<option value="${escapeHtml(item.path)}" ${item.path === program ? 'selected' : ''}>${escapeHtml(item.folder ? `${item.folder}\\${item.name}` : item.name)}</option>`).join('')}
            ${program && !candidates.items.some(item => item.path === program) ? `<option value="${escapeHtml(program)}" selected>${escapeHtml(program.split('\\').pop())}</option>` : ''}
          </select>` : ''}
        <button class="secondary-button compact" data-play-browse>${ICON.folder}Browse</button>
        ${chosen ? `<button class="icon-button" data-play-reset-exe title="Use the detected program">${ICON.undo || ICON.refresh}</button>` : ''}
      </div>
    </div>` : '';

  return `
    <h3 class="section-title play-section-title">Launch options</h3>
    <div class="play-options" data-play-options="${escapeHtml(game.Id)}">
      ${canDirect ? `
        <div class="play-option">
          <div class="play-option-text"><span class="play-option-label">Start</span></div>
          <div class="chip-group play-start-mode">
            <button class="${direct ? '' : 'active'}" data-play-direct="false">Through ${escapeHtml(launcher)}</button>
            <button class="${direct ? 'active' : ''}" data-play-direct="true">Directly</button>
          </div>
        </div>
        ${direct ? `<div class="play-option-note">${isSteam ? 'Starting directly skips Steam. Some games need Steam running for saves and achievements.' : `Starting directly skips ${escapeHtml(launcher)}. Some games won't start without it.`}</div>` : ''}
        ${programRow}` : ''}
      <label class="play-option">
        <div class="play-option-text">
          <span class="play-option-label">Extra arguments</span>
          ${argsApply ? '' : `<span class="play-option-hint">${escapeHtml(launcher)} doesn't pass these on. Choose Directly to use them.</span>`}
        </div>
        <input class="play-input mono" type="text" spellcheck="false" placeholder="-windowed -novid" value="${escapeHtml(data['launch.args'] || '')}" data-play-args ${argsApply ? '' : 'disabled'}>
      </label>
      <label class="play-option">
        <div class="play-option-text">
          <span class="play-option-label">Run as administrator</span>
          ${adminApplies ? '' : `<span class="play-option-hint">Only possible when the game starts directly.</span>`}
        </div>
        <input type="checkbox" class="play-switch" data-play-admin ${data['launch.admin'] === true ? 'checked' : ''} ${adminApplies ? '' : 'disabled'}>
      </label>
      <label class="play-option">
        <div class="play-option-text">
          <span class="play-option-label">Track playtime</span>
          <span class="play-option-hint">Turn off for apps that stay open in the background.</span>
        </div>
        <input type="checkbox" class="play-switch" data-play-tracked ${isTracked(game, data) ? 'checked' : ''}>
      </label>
    </div>`;
}

const UNTRACKED_BY_DEFAULT = new Set(['431960', '1070560', '228980']);

function isTracked(game, data) {
  const value = data['play.tracked'];
  return typeof value === 'boolean' ? value : !UNTRACKED_BY_DEFAULT.has(String(game.SteamAppId || ''));
}

extensions.drawerSections.push(game => `${sessionsHtml(game)}${launchOptionsHtml(game)}`);

function selectedGame() {
  return state.byId.get(state.selectedId);
}

elements.drawer.addEventListener('click', event => {
  const game = selectedGame();
  if (!game) return;
  const directButton = event.target.closest('[data-play-direct]');
  if (directButton) {
    const direct = directButton.dataset.playDirect === 'true';
    setGameData(game.Id, { 'launch.direct': direct || null });
    renderDrawer();
  } else if (event.target.closest('[data-play-browse]')) {
    send({ type: 'playBrowseExe', id: game.Id });
  } else if (event.target.closest('[data-play-reset-exe]')) {
    setGameData(game.Id, { 'launch.exe': null });
    renderDrawer();
  } else if (event.target.closest('[data-play-view-stats]')) {
    closeDrawer();
    setView('stats');
  }
});

elements.drawer.addEventListener('change', event => {
  const game = selectedGame();
  if (!game) return;
  const target = event.target;
  if (target.matches('[data-play-args]')) {
    const value = target.value.trim();
    setGameData(game.Id, { 'launch.args': value || null });
    toast(value ? `Extra arguments saved for ${game.Name}` : `Extra arguments cleared for ${game.Name}`, 'info', ICON.check);
  } else if (target.matches('[data-play-admin]')) {
    setGameData(game.Id, { 'launch.admin': target.checked || null });
  } else if (target.matches('[data-play-tracked]')) {
    state.userData[game.Id] = { ...(state.userData[game.Id] || {}), 'play.tracked': target.checked };
    send({ type: 'playTracked', id: game.Id, tracked: target.checked });
  } else if (target.matches('[data-play-exe]')) {
    setGameData(game.Id, { 'launch.exe': target.value || null });
    renderDrawer();
  }
});

elements.drawer.addEventListener('keydown', event => {
  if (event.key === 'Enter' && event.target.matches('[data-play-args]')) event.target.blur();
});

function startOfDay(time) {
  const date = new Date(time);
  date.setHours(0, 0, 0, 0);
  return date.getTime();
}

function startOfWeek(time) {
  const date = new Date(startOfDay(time));
  const offset = (date.getDay() + 6) % 7;
  date.setDate(date.getDate() - offset);
  return date.getTime();
}

function chartBuckets(range) {
  const daily = range === 'daily';
  const count = daily ? 30 : 12;
  const buckets = [];
  let cursor = daily ? startOfDay(Date.now()) : startOfWeek(Date.now());
  for (let index = 0; index < count; index++) {
    buckets.unshift({ start: cursor, minutes: 0, sessions: 0 });
    const previous = new Date(cursor);
    previous.setDate(previous.getDate() - (daily ? 1 : 7));
    cursor = previous.getTime();
  }
  const first = buckets[0].start;
  for (const session of play.sessions) {
    const start = playParse(session.Start);
    if (start == null || start < first) continue;
    const key = daily ? startOfDay(start) : startOfWeek(start);
    const bucket = buckets.find(item => item.start === key);
    if (!bucket) continue;
    bucket.minutes += session.Minutes || 0;
    bucket.sessions++;
  }
  return buckets;
}

function niceMax(minutes) {
  const hours = minutes / 60;
  if (hours <= 1) return 60;
  const steps = [2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 25, 30, 40, 50, 60, 80, 100, 150, 200];
  const step = steps.find(value => value >= hours) || Math.ceil(hours / 50) * 50;
  return step * 60;
}

function chartHtml() {
  const range = play.statsRange;
  const buckets = chartBuckets(range);
  const peak = Math.max(...buckets.map(bucket => bucket.minutes));
  const max = niceMax(peak);
  const total = buckets.reduce((sum, bucket) => sum + bucket.minutes, 0);
  const daily = range === 'daily';
  const label = bucket => {
    const date = new Date(bucket.start);
    return daily
      ? date.toLocaleDateString('en-GB', { day: 'numeric' })
      : date.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' });
  };
  const tip = bucket => {
    const date = new Date(bucket.start);
    const when = daily
      ? date.toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'long' })
      : `Week of ${date.toLocaleDateString('en-GB', { day: 'numeric', month: 'long' })}`;
    return `${when}: ${bucket.minutes ? formatPlaytime(bucket.minutes) : 'nothing'}${bucket.sessions ? ` in ${bucket.sessions} session${bucket.sessions === 1 ? '' : 's'}` : ''}`;
  };
  const current = buckets.length - 1;
  const showLabel = index => !daily || index === current || index % 5 === current % 5;

  return `
    <section class="play-card play-chart-card">
      <header class="play-card-head">
        <h2>${daily ? 'Last 30 days' : 'Last 12 weeks'}</h2>
        <span class="play-card-figure">${escapeHtml(playHours(total))}</span>
      </header>
      <div class="play-chart ${daily ? 'daily' : 'weekly'}" style="--bars:${buckets.length}">
        <div class="play-chart-grid">
          ${[1, 0.5, 0].map(fraction => `<div class="play-gridline"><span>${fraction ? escapeHtml(playHours(Math.round(max * fraction))) : ''}</span></div>`).join('')}
        </div>
        <div class="play-bars">
          ${buckets.map((bucket, index) => `
            <div class="play-bar ${index === current ? 'current' : ''} ${bucket.minutes ? '' : 'empty'}" data-tip="${escapeHtml(tip(bucket))}">
              <div class="play-bar-track"><i style="height:${bucket.minutes ? Math.max(3, (bucket.minutes / max) * 100) : 0}%"></i></div>
              <span class="play-bar-label">${showLabel(index) ? escapeHtml(label(bucket)) : ''}</span>
            </div>`).join('')}
        </div>
      </div>
      ${play.sessions.length ? '' : '<div class="play-empty-line">Nothing tracked yet. Sessions appear here once Game Hub sees a game running.</div>'}
    </section>`;
}

function platformBreakdownHtml(games) {
  const totals = new Map();
  for (const game of games) {
    if (!game.PlaytimeMinutes) continue;
    const platform = platformOf(game);
    const entry = totals.get(platform.key) || { platform, minutes: 0, games: 0 };
    entry.minutes += game.PlaytimeMinutes;
    entry.games++;
    totals.set(platform.key, entry);
  }
  const rows = [...totals.values()].sort((first, second) => second.minutes - first.minutes);
  const sum = rows.reduce((total, row) => total + row.minutes, 0);
  if (!rows.length) return '';
  return `
    <section class="play-card">
      <header class="play-card-head"><h2>By platform</h2></header>
      <div class="play-split">
        ${rows.map(row => `<i style="flex:${row.minutes};background:${PLAY_COLORS[row.platform.key] || 'var(--accent)'}" title="${escapeHtml(row.platform.label)}"></i>`).join('')}
      </div>
      <div class="play-platforms">
        ${rows.map(row => `
          <div class="play-platform">
            <span class="play-platform-dot" style="background:${PLAY_COLORS[row.platform.key] || 'var(--accent)'}"></span>
            <span class="play-platform-icon">${platformIcon(row.platform.key)}</span>
            <span class="play-platform-name">${escapeHtml(row.platform.label)}<span class="play-platform-games">${formatCount(row.games)} game${row.games === 1 ? '' : 's'}</span></span>
            <span class="play-platform-hours">${escapeHtml(playHours(row.minutes))}</span>
            <span class="play-platform-share">${Math.round((row.minutes / sum) * 100)}%</span>
          </div>`).join('')}
      </div>
    </section>`;
}

function mostPlayedHtml(games) {
  const ranked = games.filter(game => game.PlaytimeMinutes).sort((first, second) => second.PlaytimeMinutes - first.PlaytimeMinutes).slice(0, 10);
  if (!ranked.length) return '';
  const top = ranked[0].PlaytimeMinutes;
  return `
    <section class="play-card">
      <header class="play-card-head"><h2>Most played</h2></header>
      <ol class="play-ranking">
        ${ranked.map((game, index) => `
          <li>
            <button class="play-rank" data-play-open-game="${escapeHtml(game.Id)}">
              <span class="play-rank-number">${index + 1}</span>
              ${playThumb(game)}
              <span class="play-rank-main">
                <span class="play-rank-name">${escapeHtml(game.Name)}</span>
                <span class="play-rank-bar"><i style="width:${Math.max(2, (game.PlaytimeMinutes / top) * 100)}%;background:${PLAY_COLORS[platformOf(game).key] || 'var(--accent)'}"></i></span>
              </span>
              <span class="play-rank-hours">${escapeHtml(playHours(game.PlaytimeMinutes))}</span>
            </button>
          </li>`).join('')}
      </ol>
    </section>`;
}

function recentSessionsHtml() {
  const sessions = [...play.sessions]
    .filter(session => state.byId.has(session.Id))
    .sort((first, second) => (playParse(second.Start) || 0) - (playParse(first.Start) || 0))
    .slice(0, 12);
  if (!sessions.length) return '';
  return `
    <section class="play-card">
      <header class="play-card-head"><h2>Recent sessions</h2></header>
      <div class="play-recent">
        ${sessions.map(session => {
          const game = state.byId.get(session.Id);
          return `
            <button class="play-recent-row" data-play-open-game="${escapeHtml(game.Id)}">
              ${playThumb(game)}
              <span class="play-recent-main">
                <span class="play-recent-name">${escapeHtml(game.Name)}</span>
                <span class="play-recent-when">${escapeHtml(playSessionWhen(session))}</span>
              </span>
              <span class="play-recent-length">${escapeHtml(formatPlaytime(session.Minutes))}</span>
            </button>`;
        }).join('')}
      </div>
    </section>`;
}

function renderStatsView() {
  const games = state.games.filter(game => game.Status !== 'leftover');
  const totalMinutes = games.reduce((sum, game) => sum + (game.PlaytimeMinutes || 0), 0);
  const played = games.filter(game => game.PlaytimeMinutes).length;
  const sessions = play.sessions;
  const longest = sessions.reduce((best, session) => (!best || session.Minutes > best.Minutes ? session : best), null);
  const trackedMinutes = sessions.reduce((sum, session) => sum + (session.Minutes || 0), 0);
  const average = sessions.length ? Math.round(trackedMinutes / sessions.length) : 0;
  const longestGame = longest && state.byId.get(longest.Id);
  const achievements = Object.values(play.achievements);
  const unlocked = achievements.reduce((sum, item) => sum + item.Unlocked, 0);
  const possible = achievements.reduce((sum, item) => sum + item.Total, 0);

  elements.pageTitle.textContent = 'Stats';
  elements.zoomControl.classList.add('hidden');
  elements.summary.innerHTML = `<strong>${escapeHtml(playHours(totalMinutes))}</strong> played across <strong>${formatCount(played)}</strong> game${played === 1 ? '' : 's'}`;
  elements.filters.innerHTML = `
    <div class="chip-group play-range">
      <button class="${play.statsRange === 'weekly' ? 'active' : ''}" data-play-range="weekly">Weekly</button>
      <button class="${play.statsRange === 'daily' ? 'active' : ''}" data-play-range="daily">Daily</button>
    </div>
    <button class="chip" data-play-picker>${ICON.dice}Pick a game</button>`;

  const tile = (label, value, note, muted) => `
    <div class="play-tile">
      <div class="stat-label">${label}</div>
      <div class="play-tile-value ${muted ? 'muted' : ''}">${value}</div>
      ${note ? `<div class="play-tile-note" title="${escapeHtml(note)}">${escapeHtml(note)}</div>` : ''}
    </div>`;

  elements.results.innerHTML = `
    <div class="play-stats">
      <div class="play-tiles">
        ${tile('Total playtime', escapeHtml(playHours(totalMinutes)), played ? `${formatCount(played)} games with playtime` : '', !totalMinutes)}
        ${tile('Sessions tracked', formatCount(sessions.length), sessions.length ? `${escapeHtml(playHours(trackedMinutes))} tracked by Game Hub` : 'Starts with your next game', !sessions.length)}
        ${tile('Longest session', longest ? escapeHtml(formatPlaytime(longest.Minutes)) : '—', longestGame ? longestGame.Name : '', !longest)}
        ${tile('Average session', average ? escapeHtml(formatPlaytime(average)) : '—', '', !average)}
        ${possible ? tile('Achievements', `${formatCount(unlocked)}<span class="play-of"> / ${formatCount(possible)}</span>`, `Across ${formatCount(achievements.length)} Steam games`) : ''}
      </div>
      ${chartHtml()}
      <div class="play-columns">
        <div class="play-column">
          ${mostPlayedHtml(games)}
        </div>
        <div class="play-column">
          ${platformBreakdownHtml(games)}
          ${recentSessionsHtml()}
        </div>
      </div>
    </div>`;
}

extensions.views.stats = { render: renderStatsView };

elements.results.addEventListener('click', event => {
  const open = event.target.closest('[data-play-open-game]');
  if (open && state.view === 'stats') openDrawer(open.dataset.playOpenGame);
});

elements.filters.addEventListener('click', event => {
  const range = event.target.closest('[data-play-range]');
  if (range && state.view === 'stats') {
    play.statsRange = range.dataset.playRange;
    renderStatsView();
    return;
  }
  if (event.target.closest('[data-play-picker]')) openPicker();
});

extensions.sidebarSections.push(() => `
  <div class="nav-section">
    <div class="nav-heading">Play</div>
    <button class="nav-item ${state.view === 'stats' ? 'active' : ''}" data-view="stats">
      <span class="nav-icon">${ICON.chart}</span>
      <span class="nav-label">${VIEWS.stats}</span>
      <span class="nav-count"></span>
    </button>
    <button class="nav-item" data-play-picker>
      <span class="nav-icon">${ICON.dice}</span>
      <span class="nav-label">Pick a game</span>
      <span class="nav-count"></span>
    </button>
  </div>`);

elements.sidebar.addEventListener('click', event => {
  if (event.target.closest('[data-play-picker]')) openPicker();
});

extensions.filterChips.push(() => (state.view === 'all' || state.view === 'favorites' || state.view === 'recent'
  ? `<button class="chip" data-play-picker title="Pick a random game to play">${ICON.dice}Pick for me</button>`
  : ''));

function pickerPool() {
  const options = play.picker;
  return state.games.filter(game => {
    if (game.Status === 'leftover') return false;
    if (options.installed && !game.CanLaunch) return false;
    if (options.platform && platformOf(game).key !== options.platform) return false;
    if (options.unplayed && (game.PlaytimeMinutes || game.LastPlayed)) return false;
    if (options.underHours && (game.PlaytimeMinutes || 0) >= options.underHours * 60) return false;
    if (options.favorites && !isFavorite(game)) return false;
    return !isDemo(game);
  });
}

function pickerPlatforms() {
  const keys = new Set(state.games.filter(game => game.Status !== 'leftover').map(game => platformOf(game).key));
  return PLATFORMS.filter(platform => keys.has(platform.key));
}

function pickerFiltersHtml(pool) {
  const options = play.picker;
  const toggle = (key, label) => `<button class="chip ${options[key] ? 'active' : ''}" data-picker-toggle="${key}">${options[key] ? ICON.check : ''}${label}</button>`;
  return `
    <div class="picker-filters">
      <select class="play-select" data-picker-platform>
        <option value="">Every platform</option>
        ${pickerPlatforms().map(platform => `<option value="${platform.key}" ${options.platform === platform.key ? 'selected' : ''}>${escapeHtml(platform.label)}</option>`).join('')}
      </select>
      <select class="play-select" data-picker-hours>
        ${UNDER_HOURS.map(hours => `<option value="${hours}" ${options.underHours === hours ? 'selected' : ''}>${hours ? `Under ${hours} hours` : 'Any playtime'}</option>`).join('')}
      </select>
      ${toggle('unplayed', 'Never played')}
      ${toggle('favorites', 'Favorites')}
      ${toggle('installed', 'Installed')}
      <span class="picker-count">${formatCount(pool.length)} game${pool.length === 1 ? '' : 's'}</span>
    </div>`;
}

function pickerResultHtml(game) {
  if (!game) return '';
  const meta = [
    platformOf(game).label,
    game.PlaytimeMinutes ? `${formatPlaytime(game.PlaytimeMinutes)} played` : 'Never played',
    game.LastPlayed ? `last played ${relativeTime(game.LastPlayed).toLowerCase()}` : null,
  ].filter(Boolean).join(' · ');
  return `
    <div class="picker-name">${escapeHtml(game.Name)}</div>
    <div class="picker-meta">${escapeHtml(meta)}</div>`;
}

function renderPicker() {
  const pool = pickerPool();
  const options = play.picker;
  const game = options.pick && state.byId.get(options.pick);
  const html = `
    <header class="modal-head">
      <h2>What should I play?</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="modal-body">
      ${pickerFiltersHtml(pool)}
      <div class="picker-stage">
        <div class="picker-window ${game ? 'landed' : ''}">
          <div class="picker-reel">${game ? `<div class="picker-slot">${coverHtml(game)}</div>` : `<div class="picker-slot picker-idle">${ICON.dice}</div>`}</div>
        </div>
        <div class="picker-result">${game ? pickerResultHtml(game) : `<div class="picker-meta">${pool.length ? 'Shuffle to pick something from your library.' : 'No games match these filters.'}</div>`}</div>
      </div>
    </div>
    <footer class="modal-foot">
      <span class="grow"></span>
      ${game ? `<button class="secondary-button compact" data-picker-details>${ICON.info}Details</button>` : ''}
      <button class="secondary-button compact" data-picker-spin ${pool.length ? '' : 'disabled'}>${ICON.dice}${game ? 'Reroll' : 'Shuffle'}</button>
      ${game && game.CanLaunch ? `<button class="primary-button compact" data-picker-play>${ICON.play}Play</button>` : ''}
    </footer>`;

  if (modal.isOpen && modal.panel && modal.panel.classList.contains('picker-modal')) modal.panel.innerHTML = html;
  else modal.open(html, 'picker-modal');
}

function openPicker() {
  if (!state.loaded) return;
  play.picker.pick = null;
  play.picker.spinning = false;
  renderPicker();
}

function spinPicker() {
  const options = play.picker;
  if (options.spinning) return;
  const pool = pickerPool();
  if (!pool.length) return;
  const candidates = pool.length > 1 && options.pick ? pool.filter(game => game.Id !== options.pick) : pool;
  const winner = candidates[Math.floor(Math.random() * candidates.length)];
  const panel = modal.panel;
  const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  if (!panel || reduced || pool.length < 2) {
    options.pick = winner.Id;
    renderPicker();
    return;
  }

  options.spinning = true;
  const fillers = [];
  const count = 22;
  for (let index = 0; index < count; index++) {
    let game = pool[Math.floor(Math.random() * pool.length)];
    if (pool.length > 2 && fillers.length && game.Id === fillers[fillers.length - 1].Id) game = pool[(pool.indexOf(game) + 1) % pool.length];
    fillers.push(game);
  }
  const current = options.pick && state.byId.get(options.pick);
  const strip = [...(current ? [current] : []), ...fillers, winner];

  const windowElement = panel.querySelector('.picker-window');
  const reel = panel.querySelector('.picker-reel');
  const result = panel.querySelector('.picker-result');
  windowElement.classList.remove('landed');
  windowElement.classList.add('spinning');
  result.classList.add('hidden');
  for (const button of panel.querySelectorAll('.modal-foot button')) button.disabled = true;
  reel.style.transition = 'none';
  reel.style.transform = 'translateY(0)';
  reel.innerHTML = strip.map(game => `<div class="picker-slot">${coverHtml(game)}</div>`).join('');

  requestAnimationFrame(() => {
    requestAnimationFrame(() => {
      const slot = reel.firstElementChild.getBoundingClientRect().height;
      reel.style.transition = 'transform 2.4s cubic-bezier(0.12, 0.72, 0.16, 1)';
      reel.style.transform = `translateY(${-slot * (strip.length - 1)}px)`;
    });
  });

  const finish = () => {
    if (!options.spinning) return;
    options.spinning = false;
    options.pick = winner.Id;
    if (modal.isOpen && modal.panel && modal.panel.classList.contains('picker-modal')) renderPicker();
  };
  reel.addEventListener('transitionend', finish, { once: true });
  setTimeout(finish, 2800);
}

document.getElementById('modalLayer').addEventListener('click', event => {
  const panel = modal.panel;
  if (!panel || !panel.classList.contains('picker-modal')) return;
  const options = play.picker;
  const toggle = event.target.closest('[data-picker-toggle]');
  if (toggle && !options.spinning) {
    options[toggle.dataset.pickerToggle] = !options[toggle.dataset.pickerToggle];
    options.pick = null;
    renderPicker();
  } else if (event.target.closest('[data-picker-spin]')) {
    spinPicker();
  } else if (event.target.closest('[data-picker-play]')) {
    const game = state.byId.get(options.pick);
    modal.close();
    if (game) launch(game, 'picker');
  } else if (event.target.closest('[data-picker-details]')) {
    const id = options.pick;
    modal.close();
    if (id) openDrawer(id);
  }
});

document.getElementById('modalLayer').addEventListener('change', event => {
  const panel = modal.panel;
  if (!panel || !panel.classList.contains('picker-modal') || play.picker.spinning) return;
  if (event.target.matches('[data-picker-platform]')) play.picker.platform = event.target.value;
  else if (event.target.matches('[data-picker-hours]')) play.picker.underHours = Number(event.target.value) || 0;
  else return;
  play.picker.pick = null;
  renderPicker();
});

function playToggle(key, label, hint, disabled = false) {
  return `
    <label class="play-setting ${disabled ? 'disabled' : ''}">
      <span class="play-setting-text">
        <span class="play-setting-label">${label}</span>
        ${hint ? `<span class="play-setting-hint">${hint}</span>` : ''}
      </span>
      <input type="checkbox" class="play-switch" data-play-setting="${key}" ${setting(key, false) === true ? 'checked' : ''} ${disabled ? 'disabled' : ''}>
    </label>`;
}

extensions.settingsSections.push({
  id: 'playing',
  title: 'Playing',
  render: () => {
    const onLaunch = setting('play.onLaunch', 'none');
    const startup = setting('play.startWithWindows', false) === true;
    const discord = setting('play.discord', false) === true;
    return `
      <div class="play-settings">
        <div class="play-setting">
          <span class="play-setting-text"><span class="play-setting-label">When a game starts</span></span>
          <div class="chip-group">
            ${ON_LAUNCH_OPTIONS.map(option => `<button class="${onLaunch === option.key ? 'active' : ''}" data-play-on-launch="${option.key}">${option.label}</button>`).join('')}
          </div>
        </div>
        ${playToggle('play.restoreOnExit', 'Bring Game Hub back when the game closes')}
        ${playToggle('play.closeToTray', 'Keep running in the tray', 'Closing or minimizing the window hides it next to the clock.')}
        ${playToggle('play.startWithWindows', 'Start with Windows')}
        ${playToggle('play.startMinimized', 'Start hidden in the tray', '', !startup || setting('play.startConsole', false) === true)}
        ${playToggle('play.startConsole', 'Start on the home screen in full screen', '', !startup)}
        ${playToggle('play.discord', 'Show what I’m playing on Discord', 'Needs the Discord desktop app running.')}
        <label class="play-setting ${discord ? '' : 'disabled'}">
          <span class="play-setting-text">
            <span class="play-setting-label">Discord application ID</span>
            <span class="play-setting-hint">Create a free application at discord.com/developers and paste its Application ID. Discord shows the application’s name above the game.</span>
          </span>
          <input class="play-input mono play-app-id" type="text" inputmode="numeric" spellcheck="false" placeholder="123456789012345678" value="${escapeHtml(setting('play.discordAppId', ''))}" data-play-app-id ${discord ? '' : 'disabled'}>
        </label>
      </div>`;
  },
  bind(root) {
    if (!root || root.dataset.playBound) return;
    root.dataset.playBound = 'true';
    const refresh = () => {
      const section = extensions.settingsSections.find(entry => entry.id === 'playing');
      const container = root.querySelector('.play-settings');
      if (section && container) container.outerHTML = section.render();
    };
    root.addEventListener('click', event => {
      const option = event.target.closest('[data-play-on-launch]');
      if (!option) return;
      setSetting('play.onLaunch', option.dataset.playOnLaunch === 'none' ? null : option.dataset.playOnLaunch);
      refresh();
    });
    root.addEventListener('change', event => {
      const input = event.target;
      if (input.matches('[data-play-setting]')) {
        setSetting(input.dataset.playSetting, input.checked || null);
        refresh();
      } else if (input.matches('[data-play-app-id]')) {
        const value = input.value.replace(/\s+/g, '');
        if (value && !/^\d{15,22}$/.test(value)) {
          toast('That doesn’t look like a Discord application ID. It’s a long number.', 'error');
          return;
        }
        setSetting('play.discordAppId', value || null);
        toast(value ? 'Discord application ID saved' : 'Discord application ID cleared', 'info', ICON.check);
      }
    });
  },
});

function applyPlayExtras(extras) {
  const data = (extras && extras.play) || {};
  play.sessions = data.sessions || [];
  play.tracked = data.tracked || {};
  play.reported = data.reported || {};
  play.achievements = data.achievements || {};
  play.candidates = {};
  setRunning(data.running || []);
}

extensions.libraryHandlers.push(message => applyPlayExtras(message.extras));

extensions.messageHandlers.push(message => {
  switch (message.type) {
    case 'running':
      setRunning(message.games);
      break;

    case 'playSession': {
      if (message.session) play.sessions.push(message.session);
      play.tracked[message.id] = message.tracked || play.tracked[message.id] || 0;
      const game = state.byId.get(message.id);
      if (game) {
        if (message.minutes) game.PlaytimeMinutes = message.minutes;
        if (message.lastPlayed) game.LastPlayed = message.lastPlayed;
      }
      scheduleRender(200);
      break;
    }

    case 'stopResult': {
      const pending = play.stopping === message.id;
      play.stopping = null;
      if (pending && modal.isOpen && modal.panel && modal.panel.classList.contains('play-stop-modal')) modal.close();
      toast(message.message, message.ok ? 'info' : 'error', ICON.stopSquare);
      break;
    }

    case 'achievements':
      if (message.progress) play.achievements[message.id] = message.progress;
      if (state.selectedId === message.id) renderDrawer();
      break;

    case 'playExeCandidates':
      play.candidates[message.id] = { items: message.items || [], current: message.current || null };
      if (state.selectedId === message.id) renderDrawer();
      break;

    case 'openHome':
      if (typeof window.openHome === 'function') window.openHome();
      break;

    case 'gameData':
      if (state.selectedId === message.id && elements.drawer.querySelector('[data-play-options]')) renderDrawer();
      break;
  }
});

renderPill();
