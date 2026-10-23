'use strict';

ICON.home = '<svg viewBox="0 0 24 24" class="glyph"><path d="M3.5 11 12 4l8.5 7"/><path d="M6 9.5V19a1.5 1.5 0 0 0 1.5 1.5h9A1.5 1.5 0 0 0 18 19V9.5"/><path d="M10 20.5v-5h4v5"/></svg>';
ICON.tiles = '<svg viewBox="0 0 24 24" class="glyph"><rect x="3" y="5" width="8" height="5.5" rx="1.3"/><rect x="13" y="5" width="8" height="5.5" rx="1.3"/><rect x="3" y="13.5" width="8" height="5.5" rx="1.3"/><rect x="13" y="13.5" width="8" height="5.5" rx="1.3"/></svg>';
ICON.covers = '<svg viewBox="0 0 24 24" class="glyph"><rect x="3" y="4" width="5" height="16" rx="1.3"/><rect x="9.5" y="4" width="5" height="16" rx="1.3"/><rect x="16" y="4" width="5" height="16" rx="1.3"/></svg>';
ICON.desktop = '<svg viewBox="0 0 24 24" class="glyph"><rect x="3" y="4.5" width="18" height="12" rx="2"/><path d="M8.5 20h7M12 16.5V20"/></svg>';
ICON.power = '<svg viewBox="0 0 24 24" class="glyph"><path d="M12 3.5v8"/><path d="M7 6.3a7.5 7.5 0 1 0 10 0"/></svg>';
ICON.music = '<svg viewBox="0 0 24 24" class="glyph"><path d="M9 18V5.5l11-2V16"/><circle cx="6.5" cy="18" r="2.5"/><circle cx="17.5" cy="16" r="2.5"/></svg>';
ICON.sleep = '<svg viewBox="0 0 24 24" class="glyph"><path d="M20 14.5A8 8 0 1 1 9.5 4a6.5 6.5 0 0 0 10.5 10.5z"/></svg>';
ICON.restart = '<svg viewBox="0 0 24 24" class="glyph"><path d="M4.5 12a7.5 7.5 0 1 0 2.2-5.3"/><path d="M4.5 4.5v4h4"/></svg>';
ICON.signOut = '<svg viewBox="0 0 24 24" class="glyph"><path d="M14 4.5h4a1.5 1.5 0 0 1 1.5 1.5v12a1.5 1.5 0 0 1-1.5 1.5h-4"/><path d="m10 8-4 4 4 4M6 12h9"/></svg>';
ICON.exitApp = '<svg viewBox="0 0 24 24" class="glyph"><rect x="4" y="4" width="16" height="16" rx="3"/><path d="m9 9 6 6M15 9l-6 6"/></svg>';
ICON.pad = '<svg viewBox="0 0 24 24" class="glyph"><path d="M7.5 7h9a4.5 4.5 0 0 1 4.4 3.6l.9 4.6a2.6 2.6 0 0 1-4.5 2.2l-1.8-1.9h-7l-1.8 1.9a2.6 2.6 0 0 1-4.5-2.2l.9-4.6A4.5 4.5 0 0 1 7.5 7z"/><path d="M8 10v3M6.5 11.5h3M15.5 11h.01M17.5 12.5h.01"/></svg>';
ICON.wifi = '<svg viewBox="0 0 24 24" class="glyph wifi"><path class="arc3" d="M2.5 9a14 14 0 0 1 19 0"/><path class="arc2" d="M5.5 12.2a9.5 9.5 0 0 1 13 0"/><path class="arc1" d="M8.5 15.4a5 5 0 0 1 7 0"/><path d="M12 18.8h.01"/></svg>';
ICON.wifiOff = '<svg viewBox="0 0 24 24" class="glyph"><path d="M2.5 9a14 14 0 0 1 19 0M5.5 12.2a9.5 9.5 0 0 1 13 0M8.5 15.4a5 5 0 0 1 7 0M12 18.8h.01" opacity=".35"/><path d="M4 4l16 16"/></svg>';
ICON.ethernet = '<svg viewBox="0 0 24 24" class="glyph"><rect x="4.5" y="4" width="15" height="11" rx="1.6"/><path d="M9 15v4.5h6V15M9 8v3M12 8v3M15 8v3"/></svg>';
ICON.volume = '<svg viewBox="0 0 24 24" class="glyph volume"><path d="M4 9.5h3.5L12 5.5v13l-4.5-4H4z"/><path class="wave1" d="M15.5 9.5a3.5 3.5 0 0 1 0 5"/><path class="wave2" d="M18 7a7 7 0 0 1 0 10"/></svg>';
ICON.volumeMute = '<svg viewBox="0 0 24 24" class="glyph"><path d="M4 9.5h3.5L12 5.5v13l-4.5-4H4z"/><path d="m16 9.5 5 5M21 9.5l-5 5"/></svg>';
ICON.padMenu = '<svg viewBox="0 0 24 24" class="glyph"><path d="M6 8h12M6 12h12M6 16h12"/></svg>';
ICON.padView = '<svg viewBox="0 0 24 24" class="glyph"><rect x="4" y="5" width="10" height="9" rx="1.5"/><rect x="10" y="10" width="10" height="9" rx="1.5"/></svg>';

const HOME_LAYOUTS = ['thumbnails', 'covers'];
const HOME_VIDEO_DELAY = 2200;
const STATUS_INTERVAL = 15 * 1000;

const controller = { mode: 'mouse', kind: 'xbox', model: '' };

const home = {
  element: document.getElementById('home'),
  visible: false,
  started: false,
  sections: [],
  row: 0,
  columns: {},
  signature: '',
  zone: 'tiles',
  query: '',
  status: null,
  background: 0,
  backgroundTimer: null,
  backgroundUrl: null,
  videoTimer: null,
  videoUrl: null,
};

function homeLayout() {
  return HOME_LAYOUTS.includes(state.prefs.homeLayout) ? state.prefs.homeLayout : 'thumbnails';
}

function byName(first, second) {
  return first.Name.localeCompare(second.Name, undefined, { numeric: true, sensitivity: 'base' });
}

function runningEntry(id) {
  const running = state.running || {};
  return running[id] || null;
}

function homeSections() {
  const installed = state.games.filter(game => game.Status === 'installed' && !(state.prefs.hideDemos && isDemo(game)));
  const gameItem = game => ({ kind: 'game', id: game.Id, game });
  const offerItem = offer => ({ kind: 'offer', id: offer.Key, offer });

  if (home.query) {
    const query = normalize(home.query);
    const results = installed
      .map(game => ({ game, score: typeof searchScore === 'function' ? searchScore(game, query) : (normalize(game.Name).includes(query) ? 1 : 0) }))
      .filter(entry => entry.score > 0)
      .sort((first, second) => second.score - first.score || byName(first.game, second.game))
      .map(entry => gameItem(entry.game));
    return [{ key: 'results', title: 'Search results', label: 'Results', items: results }];
  }

  const playing = Object.entries(state.running || {})
    .map(([id, entry]) => ({ game: state.byId.get(id), entry }))
    .filter(pair => pair.game)
    .sort((first, second) => Date.parse(second.entry.startedAt || 0) - Date.parse(first.entry.startedAt || 0))
    .map(pair => pair.game);
  const recent = installed
    .filter(game => game.LastPlayed)
    .sort((first, second) => Date.parse(second.LastPlayed) - Date.parse(first.LastPlayed))
    .slice(0, 18);
  const favorites = installed.filter(isFavorite).sort(byName);
  const fresh = installed
    .filter(game => game.InstalledAt)
    .sort((first, second) => Date.parse(second.InstalledAt) - Date.parse(first.InstalledAt))
    .slice(0, 14);
  const offers = (typeof free !== 'undefined' && free.snapshot ? free.snapshot.Offers : [])
    .filter(offer => offer.Section === 'now' || offer.Section === 'upcoming');

  const sections = [];
  if (playing.length) sections.push({ key: 'playing', title: 'Now playing', label: 'Playing', items: playing.map(gameItem) });
  if (recent.length) sections.push({ key: 'recent', title: 'Continue playing', label: 'Recent', items: recent.map(gameItem) });
  if (favorites.length) sections.push({ key: 'favorites', title: 'Favorites', label: 'Favorites', items: favorites.map(gameItem) });
  if (fresh.length) sections.push({ key: 'new', title: 'Recently installed', label: 'New', items: fresh.map(gameItem) });
  if (offers.length) sections.push({ key: 'free', title: 'Free to claim', label: 'Free games', items: offers.map(offerItem) });
  sections.push({ key: 'all', title: 'All games', label: 'All games', items: [...installed].sort(byName).map(gameItem) });
  return sections;
}

function homeSignature(sections) {
  return homeLayout() + '|' + home.query + '|' + sections.map(section => section.key + ':' + section.items.map(item =>
    item.kind === 'game' ? `${item.id}~${item.game.CoverUrl || ''}~${item.game.HeaderUrl || ''}` : `${item.id}~${item.offer.Image || ''}`).join(',')).join('|');
}

function currentItem() {
  const section = home.sections[home.row];
  if (!section) return null;
  return section.items[home.columns[section.key] || 0] || null;
}

function clockText() {
  return new Date().toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
}

function homeImageFor(item) {
  if (!item) return null;
  if (item.kind === 'offer') return item.offer.Image ? epicImage(item.offer.Image) : null;
  return item.game.HeroUrl || item.game.HeaderUrl || item.game.CoverUrl || null;
}

const PS_SHAPES = {
  a: '<svg viewBox="0 0 24 24"><path d="M7 7l10 10M17 7 7 17"/></svg>',
  b: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="5.6"/></svg>',
  x: '<svg viewBox="0 0 24 24"><rect x="6.6" y="6.6" width="10.8" height="10.8" rx=".6"/></svg>',
  y: '<svg viewBox="0 0 24 24"><path d="M12 5.8 18.2 16.6H5.8z"/></svg>',
};

const GENERIC_FACE = { a: 'bottom', b: 'right', x: 'left', y: 'top' };

function padGlyph(name, kind = controller.kind) {
  if (['a', 'b', 'x', 'y'].includes(name)) {
    if (kind === 'playstation') return `<kbd class="pad-glyph face ps ps-${name}">${PS_SHAPES[name]}</kbd>`;
    if (kind === 'generic') {
      const lit = GENERIC_FACE[name];
      return `<kbd class="pad-glyph face generic">${['top', 'right', 'bottom', 'left'].map(spot => `<i class="${spot} ${spot === lit ? 'lit' : ''}"></i>`).join('')}</kbd>`;
    }
    return `<kbd class="pad-glyph face xbox xbox-${name}">${name.toUpperCase()}</kbd>`;
  }

  const labels = {
    xbox: { lb: 'LB', rb: 'RB', lt: 'LT', rt: 'RT', ls: 'LS', rs: 'RS', guide: 'Guide' },
    playstation: { lb: 'L1', rb: 'R1', lt: 'L2', rt: 'R2', ls: 'L3', rs: 'R3', guide: 'PS', start: 'Options', view: controller.model === 'dualshock' ? 'Share' : 'Create' },
    generic: { lb: 'L1', rb: 'R1', lt: 'L2', rt: 'R2', ls: 'L3', rs: 'R3', guide: 'Home', start: 'Start', view: 'Select' },
  }[kind] || {};

  if (kind === 'xbox' && name === 'start') return `<kbd class="pad-glyph round">${ICON.padMenu}</kbd>`;
  if (kind === 'xbox' && name === 'view') return `<kbd class="pad-glyph round">${ICON.padView}</kbd>`;
  return `<kbd class="pad-glyph pill">${escapeHtml(labels[name] || name.toUpperCase())}</kbd>`;
}

function thumbArt(game) {
  const image = game.HeaderUrl || game.HeroUrl;
  if (image) return `<img src="${escapeHtml(image)}" alt="" decoding="async">`;

  const hue = hueOf(game.Name);
  const emblem = game.IconUrl ? `<img class="home-fallback-icon" src="${escapeHtml(game.IconUrl)}" alt="">` : '';
  const cover = game.CoverUrl ? `<img class="home-fallback-cover" src="${escapeHtml(game.CoverUrl)}" alt="">` : '';
  return `
    <div class="home-fallback" style="background:radial-gradient(120% 140% at 0% 0%, hsl(${hue} 45% 30%), hsl(${(hue + 40) % 360} 40% 12%) 70%)">
      ${cover}
      ${cover ? '' : emblem}
      <span>${escapeHtml(game.Name)}</span>
    </div>`;
}

function tileHtml(item, row, column, shape) {
  const art = item.kind === 'offer'
    ? `<img src="${escapeHtml(epicImage(item.offer.Image) || '')}" alt="" decoding="async">`
    : shape === 'tall' ? coverHtml(item.game) : thumbArt(item.game);
  const badge = item.kind === 'offer' ? platformIcon(item.offer.Store) : platformIcon(platformOf(item.game).key);
  let tag = '';
  if (item.kind === 'offer') tag = `<span class="home-free-tag">${item.offer.Section === 'now' ? 'FREE' : 'SOON'}</span>`;
  else if (runningEntry(item.id)) tag = '<span class="home-live-tag"><i></i>Playing</span>';
  else if (item.game.Status === 'installed' && isFavorite(item.game)) tag = `<span class="home-fav-tag">${ICON.starFilled}</span>`;
  const title = item.kind === 'offer' ? item.offer.Title : item.game.Name;

  return `
    <button class="home-tile ${shape}" tabindex="-1" data-row="${row}" data-col="${column}" title="${escapeHtml(title)}">
      <div class="home-tile-art">${art}</div>
      <span class="home-tile-badge">${badge}</span>
      ${tag}
    </button>`;
}

function batteryHtml(pad) {
  if (pad.battery === 'wired') return '<span class="home-battery wired"></span>';
  const level = { empty: 0, low: 1, medium: 2, full: 3 }[pad.battery];
  if (level == null) return '';
  return `<span class="home-battery level-${level}"><i></i><i></i><i></i></span>`;
}

function statusHtml() {
  const status = home.status || {};
  const parts = [];

  const pads = status.pads || [];
  if (pads.length) {
    for (const pad of pads) {
      const label = pad.battery === 'wired' ? 'Wired controller' : pad.battery && pad.battery !== 'unknown' ? `Controller battery ${pad.battery}` : 'Controller';
      const warn = pad.battery === 'empty' || pad.battery === 'low';
      parts.push(`<span class="home-status-item pad ${warn ? 'warn' : ''}" title="${escapeHtml(label)}">${ICON.pad}${batteryHtml(pad)}</span>`);
    }
  } else if (navigator.getGamepads && [...navigator.getGamepads()].some(Boolean)) {
    parts.push(`<span class="home-status-item pad" title="Controller">${ICON.pad}</span>`);
  }

  const network = status.network;
  if (network) {
    if (network.kind === 'wifi') {
      const bars = network.signal == null ? 3 : network.signal >= 70 ? 3 : network.signal >= 40 ? 2 : network.signal > 0 ? 1 : 0;
      const title = network.ssid ? `${network.ssid}${network.signal == null ? '' : ` · ${network.signal}%`}` : 'Wi-Fi';
      parts.push(`<span class="home-status-item network signal-${bars}" title="${escapeHtml(title)}">${ICON.wifi}</span>`);
    } else if (network.kind === 'ethernet') {
      parts.push(`<span class="home-status-item network" title="Wired network">${ICON.ethernet}</span>`);
    } else if (network.kind === 'none') {
      parts.push(`<span class="home-status-item network warn" title="Not connected">${ICON.wifiOff}</span>`);
    }
  }

  const volume = status.volume;
  if (volume && volume.level != null) {
    const percent = Math.round(volume.level * 100);
    const waves = volume.muted || percent === 0 ? 0 : percent < 34 ? 1 : 2;
    parts.push(`<button class="home-status-item volume waves-${waves}" data-home-action="mute" tabindex="-1" title="${volume.muted ? 'Muted' : `Volume ${percent}%`}">${volume.muted ? ICON.volumeMute : ICON.volume}</button>`);
  }

  const profile = status.profile;
  if (profile && (profile.picture || profile.name)) {
    const initials = String(profile.name || '?').split(/[\s._-]+/).filter(Boolean).slice(0, 2).map(word => word[0].toUpperCase()).join('');
    const picture = profile.picture ? `<img src="${escapeHtml(profile.picture)}" alt="" onerror="this.remove()">` : '';
    parts.push(`<span class="home-avatar" title="${escapeHtml(profile.name || '')}"><span>${escapeHtml(initials)}</span>${picture}</span>`);
  }

  return parts.join('');
}

function renderStatus() {
  const target = home.element.querySelector('[data-home-status]');
  if (target) target.innerHTML = statusHtml();
}

function requestStatus() {
  if (home.visible && !document.hidden) send({ type: 'consoleStatus' });
}

function homeBarHtml(layout) {
  const showTabs = layout === 'covers' && !home.query;
  const tabs = showTabs
    ? `<nav class="home-tabs">${home.sections.map((section, index) => `
        <button class="home-tab ${index === home.row ? 'active' : ''}" data-home-tab="${index}" ${index === home.row ? 'data-pad-default' : ''}>${escapeHtml(section.label)}</button>`).join('')}
      </nav>`
    : '<div class="home-heading">Home</div>';

  const query = home.query
    ? `<button class="home-query" data-home-action="clear-search" title="Clear search">${ICON.search}<span>${escapeHtml(home.query)}</span>${ICON.close}</button>`
    : '';
  const musicOn = !!setting('console.music', false) && !!setting('console.musicPath', '');
  const musicTitle = musicOn ? (music.current ? `Music: ${music.current.name}` : 'Music on') : 'Background music';

  return `
    <header class="home-bar" data-drag>
      <div class="home-brand"><span class="brand-mark">${ICON.brand}</span></div>
      ${tabs}
      <div class="home-system">
        ${query}
        <button class="home-icon-button" data-home-action="search" title="Search your library (S)" ${showTabs ? '' : 'data-pad-default'}>${ICON.search}</button>
        <button class="home-icon-button ${musicOn ? 'on' : ''}" data-home-action="music" title="${escapeHtml(musicTitle)}">${ICON.music}</button>
        <div class="home-switch" title="Layout">
          <button class="${layout === 'thumbnails' ? 'active' : ''}" data-home-layout="thumbnails" title="Thumbnails">${ICON.tiles}</button>
          <button class="${layout === 'covers' ? 'active' : ''}" data-home-layout="covers" title="Covers">${ICON.covers}</button>
        </div>
        <button class="home-icon-button" data-home-action="exit" title="Desktop library (Home key)">${ICON.desktop}</button>
        <button class="home-icon-button" data-home-action="power" title="Power">${ICON.power}</button>
        <span class="home-divider"></span>
        <div class="home-status" data-home-status>${statusHtml()}</div>
        <div class="home-clock" data-home-clock>${clockText()}</div>
      </div>
    </header>`;
}

function homeStageHtml(layout) {
  if (!state.loaded) {
    return `<div class="home-loading"><span class="measuring"></span></div>`;
  }

  if (home.query && !home.sections[0]?.items.length) {
    return `
      <div class="home-empty">
        ${ICON.search}
        <strong>No games match “${escapeHtml(home.query)}”</strong>
        <span>${controller.mode === 'controller' ? `${padGlyph('b')} Clear search` : 'Press Esc to clear the search'}</span>
      </div>`;
  }

  if (layout === 'covers') {
    const section = home.sections[home.row];
    return `
      <div class="home-carousel">
        <div class="home-track-viewport">
          <div class="home-track" data-track="${home.row}">${section.items.map((item, column) => tileHtml(item, home.row, column, 'tall')).join('')}</div>
        </div>
      </div>
      <section class="home-focus" data-home-focus></section>`;
  }

  return `
    <section class="home-focus" data-home-focus></section>
    <div class="home-rows-viewport">
      <div class="home-rows" data-home-rows>
        ${home.sections.map((section, row) => `
          <section class="home-row" data-home-row="${row}">
            <h2>${escapeHtml(section.title)} <span>${section.items.length}</span></h2>
            <div class="home-track-viewport">
              <div class="home-track" data-track="${row}">${section.items.map((item, column) => tileHtml(item, row, column, 'thumb')).join('')}</div>
            </div>
          </section>`).join('')}
      </div>
    </div>`;
}

function hintsHtml() {
  const covers = homeLayout() === 'covers';
  if (controller.mode === 'controller') {
    const item = currentItem();
    const primary = item && item.kind === 'game' && runningEntry(item.id) ? 'Resume' : item && item.kind === 'offer' ? 'Open' : 'Play';
    const hints = home.zone === 'bar'
      ? [[padGlyph('a'), 'Select'], [padGlyph('b'), 'Back']]
      : [[padGlyph('a'), primary], [padGlyph('x'), 'Details'], [padGlyph('y'), 'Favorite'], [padGlyph('lb') + padGlyph('rb'), covers ? 'Switch tab' : 'Jump row'], [padGlyph('ls'), 'Search']];
    hints.push([padGlyph('view'), 'Library'], [padGlyph('start'), 'Power']);
    return hints.map(([keys, label]) => `<span class="home-hint">${keys}<span>${label}</span></span>`).join('');
  }

  const key = label => `<kbd>${label}</kbd>`;
  const hints = [[key('←') + key('→') + (covers ? '' : key('↑') + key('↓')), 'Move'], [key('Enter'), 'Play'], [key('Space'), 'Details'], [key('F'), 'Favorite'], [key('Tab'), covers ? 'Switch tab' : 'Next row'], [key('S'), 'Search']];
  return hints.map(([keys, label]) => `<span class="home-hint">${keys}<span>${label}</span></span>`).join('');
}

function renderHome(keepFocusId) {
  if (!home.visible) return;
  const layout = homeLayout();
  const focusedId = keepFocusId === undefined ? currentItem()?.id : keepFocusId;
  const focusedSection = home.sections[home.row]?.key;

  home.sections = state.loaded ? homeSections() : [];
  home.signature = homeSignature(home.sections);

  const sectionIndex = home.sections.findIndex(section => section.key === focusedSection);
  home.row = sectionIndex >= 0 ? sectionIndex : Math.min(home.row, Math.max(0, home.sections.length - 1));
  for (const section of home.sections) {
    const index = focusedId ? section.items.findIndex(item => item.id === focusedId) : -1;
    if (index >= 0 && section.key === home.sections[home.row]?.key) home.columns[section.key] = index;
    home.columns[section.key] = Math.min(home.columns[section.key] || 0, Math.max(0, section.items.length - 1));
  }

  home.element.className = `home layout-${layout} visible${home.zone === 'bar' ? ' bar-zone' : ''}`;
  home.element.innerHTML = `
    <div class="home-bg">
      <img class="home-bg-image" data-bg="0" alt="">
      <img class="home-bg-image" data-bg="1" alt="">
      <video class="home-bg-video" data-bg-video muted loop playsinline preload="none"></video>
      <div class="home-bg-shade"></div>
    </div>
    ${homeBarHtml(layout)}
    <main class="home-stage">${homeStageHtml(layout)}</main>
    <footer class="home-hints" data-home-hints>${hintsHtml()}</footer>`;
  home.backgroundUrl = null;
  home.videoUrl = null;
  clearTimeout(home.videoTimer);

  requestAnimationFrame(() => {
    updateHomeFocus(true);
    if (home.zone === 'bar' && controller.mode === 'controller' && typeof padEnsureFocus === 'function') {
      const bar = home.element.querySelector('.home-bar');
      if (bar) padEnsureFocus(bar, 'home-bar');
    }
  });
}

function refreshHome() {
  if (!home.visible) return;
  const sections = state.loaded ? homeSections() : [];
  if (homeSignature(sections) !== home.signature || !home.element.querySelector('.home-stage')) {
    renderHome();
  } else {
    home.sections = sections;
    renderFocusPanel();
  }
}

function renderFocusPanel() {
  const panel = home.element.querySelector('[data-home-focus]');
  if (!panel) return;
  const item = currentItem();
  if (!item) {
    panel.innerHTML = '';
    return;
  }

  if (item.kind === 'offer') {
    const offer = item.offer;
    const store = offer.Store === 'epic' ? 'Epic Games' : 'Steam';
    const when = offer.Section === 'now'
      ? (offer.EndsAt ? `Free until ${longDate(offer.EndsAt)}` : 'Free for a limited time')
      : `Free from ${longDate(offer.StartsAt)}`;
    panel.innerHTML = `
      <div class="home-title"><h1>${escapeHtml(offer.Title)}</h1></div>
      <div class="home-meta">${platformIcon(offer.Store)}<span>${store}</span><i></i><span>${escapeHtml(when)}</span>${offer.OriginalPrice ? `<i></i><s>${escapeHtml(offer.OriginalPrice)}</s>` : ''}</div>
      <div class="home-actions">
        <button class="home-play" data-home-action="activate">${ICON.gift}${offer.Section === 'now' ? `Claim on ${store}` : `View on ${store}`}</button>
      </div>`;
    return;
  }

  const game = item.game;
  const platform = platformOf(game);
  const size = sizeOf(game);
  const running = runningEntry(game.Id);
  const runningFor = running && running.startedAt ? Math.max(1, Math.round((Date.now() - Date.parse(running.startedAt)) / 60000)) : null;
  const played = running
    ? `<span class="home-live">${runningFor ? `Playing for ${escapeHtml(formatPlaytime(runningFor))}` : 'Playing now'}</span>`
    : game.LastPlayed ? `<span>Played ${escapeHtml(relativeTime(game.LastPlayed).replace(/^./, character => character.toLowerCase()))}</span>` : '<span>Not played yet</span>';
  const facts = [
    `${platformIcon(platform.key)}<span>${escapeHtml(platform.label)}</span>`,
    played,
    game.PlaytimeMinutes ? `<span>${escapeHtml(formatPlaytime(game.PlaytimeMinutes))}</span>` : '',
    size.pending ? '' : `<span>${formatBytes(size.bytes)}</span>`,
  ].filter(Boolean).join('<i></i>');

  const title = game.LogoUrl
    ? `<img class="home-logo" src="${escapeHtml(game.LogoUrl)}" alt="${escapeHtml(game.Name)}">`
    : `<h1>${escapeHtml(game.Name)}</h1>`;
  const favorite = isFavorite(game);
  const primary = running
    ? `<button class="home-play" data-home-action="resume">${ICON.play}Resume</button>`
    : `<button class="home-play" data-home-action="activate" ${game.CanLaunch ? '' : 'disabled'}>${ICON.play}${game.CanLaunch ? 'Play' : 'Not playable'}</button>`;

  panel.innerHTML = `
    <div class="home-title">${title}</div>
    <div class="home-meta">${facts}</div>
    <div class="home-actions">
      ${primary}
      <button class="home-secondary" data-home-action="details">${ICON.info}Details</button>
      <button class="home-secondary square ${favorite ? 'on' : ''}" data-home-action="favorite" title="${favorite ? 'Remove from favorites' : 'Add to favorites'}">${favorite ? ICON.starFilled : ICON.star}</button>
    </div>`;
}

function setHomeBackground(url) {
  if (url === home.backgroundUrl) return;
  home.backgroundUrl = url;
  clearTimeout(home.backgroundTimer);
  home.backgroundTimer = setTimeout(() => {
    const layers = home.element.querySelectorAll('[data-bg]');
    if (layers.length < 2) return;
    const next = layers[1 - home.background];
    const current = layers[home.background];
    if (!url) {
      current.classList.remove('active');
      return;
    }
    next.onload = () => {
      next.classList.add('active');
      current.classList.remove('active');
      home.background = 1 - home.background;
    };
    next.src = url;
  }, 140);
}

function microTrailerFor(item) {
  if (!item || item.kind !== 'game') return null;
  if (!setting('meta.animatedBackgrounds', true)) return null;
  if (matchMedia('(prefers-reduced-motion: reduce)').matches) return null;
  const meta = state.meta && state.meta[item.id];
  const movie = meta && Array.isArray(meta.Movies) ? meta.Movies[0] : null;
  return movie && movie.Micro || null;
}

function stopHomeVideo() {
  clearTimeout(home.videoTimer);
  home.videoUrl = null;
  const video = home.element.querySelector('[data-bg-video]');
  if (!video) return;
  video.classList.remove('active');
  setTimeout(() => {
    if (!video.classList.contains('active')) video.pause();
  }, 600);
}

function scheduleHomeVideo(item) {
  const url = microTrailerFor(item);
  if (url && url === home.videoUrl) return;
  stopHomeVideo();
  if (!url || !home.visible) return;
  home.videoUrl = url;
  home.videoTimer = setTimeout(() => {
    const video = home.element.querySelector('[data-bg-video]');
    if (!video || home.videoUrl !== url || !home.visible) return;
    video.onplaying = () => {
      if (home.videoUrl === url) video.classList.add('active');
    };
    video.onerror = () => video.classList.remove('active');
    if (video.getAttribute('src') !== url) video.src = url;
    video.muted = true;
    const playing = video.play();
    if (playing && playing.catch) playing.catch(() => {});
  }, HOME_VIDEO_DELAY);
}

function updateHomeFocus(instant = false) {
  if (!home.visible) return;
  const layout = homeLayout();
  const section = home.sections[home.row];

  for (const tile of home.element.querySelectorAll('.home-tile.focused')) tile.classList.remove('focused');
  for (const tab of home.element.querySelectorAll('[data-home-tab]')) tab.classList.toggle('active', Number(tab.dataset.homeTab) === home.row);

  if (section) {
    const column = home.columns[section.key] || 0;
    const tile = home.element.querySelector(`.home-tile[data-row="${home.row}"][data-col="${column}"]`);
    if (tile) tile.classList.add('focused');

    if (layout === 'thumbnails') {
      for (const rowElement of home.element.querySelectorAll('[data-home-row]')) {
        rowElement.classList.toggle('active', Number(rowElement.dataset.homeRow) === home.row);
      }
      const rows = home.element.querySelector('[data-home-rows]');
      const rowElement = home.element.querySelector(`[data-home-row="${home.row}"]`);
      if (rows && rowElement) {
        rows.style.transition = instant ? 'none' : '';
        rows.style.transform = `translateY(${-rowElement.offsetTop}px)`;
      }
    }

    const track = home.element.querySelector(`[data-track="${home.row}"]`);
    if (track && tile) {
      const viewport = track.parentElement;
      const maximum = Math.max(0, track.scrollWidth - viewport.clientWidth + 8);
      const offset = Math.min(maximum, Math.max(0, tile.offsetLeft - 4));
      track.style.transition = instant ? 'none' : '';
      track.style.transform = `translateX(${-offset}px)`;
    }
  }

  renderFocusPanel();
  const item = currentItem();
  setHomeBackground(homeImageFor(item));
  scheduleHomeVideo(item);
  updateHints();
}

function homeMove(dx, dy) {
  const layout = homeLayout();
  if (!home.sections.length) return false;
  const beforeRow = home.row;
  const beforeSection = home.sections[home.row];
  const beforeColumn = home.columns[beforeSection.key] || 0;

  if (dy) {
    if (layout === 'thumbnails') home.row = Math.max(0, Math.min(home.sections.length - 1, home.row + dy));
    else return false;
  }
  if (dx) {
    const section = home.sections[home.row];
    const column = home.columns[section.key] || 0;
    home.columns[section.key] = Math.max(0, Math.min(section.items.length - 1, column + dx));
  }

  const moved = home.row !== beforeRow || (home.columns[beforeSection.key] || 0) !== beforeColumn;
  if (moved) {
    consoleSound('move');
    updateHomeFocus();
  }
  return moved;
}

function homeSwitch(delta) {
  if (!home.sections.length) return;
  const count = home.sections.length;
  home.row = (home.row + delta + count) % count;
  consoleSound('move');
  if (homeLayout() === 'covers') renderHome(null);
  else updateHomeFocus();
}

function homeResume(item = currentItem()) {
  if (!item || item.kind !== 'game') return;
  const running = runningEntry(item.id);
  send({ type: 'consoleResume', id: item.id, pid: running ? running.pid : null });
  padFeedback('select');
}

function homeActivate(item = currentItem()) {
  if (!item) {
    padFeedback('error');
    return;
  }
  if (item.kind === 'offer') {
    const url = item.offer.Store === 'steam' ? (item.offer.ClientUrl || item.offer.StoreUrl) : item.offer.StoreUrl;
    padFeedback('select');
    send({ type: 'openStore', url });
    return;
  }
  if (runningEntry(item.id)) {
    homeResume(item);
    return;
  }
  if (item.game.CanLaunch) {
    launch(item.game);
  } else {
    padFeedback('select');
    openDrawer(item.game.Id);
  }
}

function homeDetails(item = currentItem()) {
  if (!item) return;
  if (item.kind === 'offer') {
    homeActivate(item);
    return;
  }
  padFeedback('select');
  openDrawer(item.game.Id);
}

function homeFavorite(item = currentItem()) {
  if (!item || item.kind !== 'game' || item.game.Status !== 'installed') {
    padFeedback('error');
    return;
  }
  toggleFavorite(item.game);
  padFeedback('select');
  toast(isFavorite(item.game) ? `Added ${item.game.Name} to favorites` : `Removed ${item.game.Name} from favorites`, 'info', ICON.star);
}

function setHomeLayout(layout) {
  if (!HOME_LAYOUTS.includes(layout) || layout === homeLayout()) return;
  state.prefs.homeLayout = layout;
  savePrefs();
  renderHome();
}

function setHomeQuery(query) {
  const next = query.trim() ? query : '';
  if (next === home.query) return;
  home.query = next;
  home.row = 0;
  home.columns.results = 0;
  renderHome(null);
}

function openHomeSearch() {
  leaveHomeBar(true);
  openKeyboard({
    value: home.query,
    label: 'Search your library',
    onInput: value => setHomeQuery(value),
    onDone: value => setHomeQuery(value),
    count: () => (home.query && home.sections[0] ? home.sections[0].items.length : null),
  });
}

function clearHomeSearch() {
  if (!home.query) return false;
  setHomeQuery('');
  padFeedback('back');
  return true;
}

function enterHome() {
  if (home.visible) return;
  home.visible = true;
  home.zone = 'tiles';
  state.prefs.lastMode = 'home';
  savePrefs();
  closeMenus();
  document.body.classList.add('home-active');
  renderHome();
  requestStatus();
  music.held = false;
  syncMusic();
}

function exitHome(focusSearch = false) {
  if (!home.visible) return;
  home.visible = false;
  home.zone = 'tiles';
  state.prefs.lastMode = 'library';
  savePrefs();
  stopHomeVideo();
  syncMusic();
  document.body.classList.remove('home-active');
  home.element.classList.remove('visible');
  setTimeout(() => {
    if (!home.visible) home.element.innerHTML = '';
  }, 400);
  if (focusSearch) {
    elements.search.focus();
    elements.search.select();
  }
}

let lastOpenHome = 0;

function openHome() {
  const now = performance.now();
  if (now - lastOpenHome < 700) return;
  lastOpenHome = now;
  if (typeof closeKeyboard === 'function') closeKeyboard(true);
  if (modal.isOpen) modal.close();
  closeMenus();
  if (state.selectedId) closeDrawer();
  if (home.visible) {
    leaveHomeBar(true);
    home.query = '';
    home.row = 0;
    renderHome(null);
    if (home.sections[0]) {
      home.columns[home.sections[0].key] = 0;
      updateHomeFocus();
    }
  } else {
    enterHome();
  }
  consoleSound('open');
}

window.openHome = openHome;

function exitConsoleMode() {
  if (typeof windowControl !== 'undefined' && windowControl.mode === 'fullscreen') windowAction('fullscreen');
  exitHome();
}

function enterHomeBar() {
  const bar = home.element.querySelector('.home-bar');
  if (!bar) return;
  home.zone = 'bar';
  home.element.classList.add('bar-zone');
  padInput.current = null;
  padEnsureFocus(bar, 'home-bar');
  consoleSound('move');
  updateHints();
}

function leaveHomeBar(silent = false) {
  if (home.zone !== 'bar') return;
  home.zone = 'tiles';
  home.element.classList.remove('bar-zone');
  if (typeof padInput !== 'undefined' && padInput.current) {
    padInput.current.classList.remove('pad-focus');
    if (home.element.contains(padInput.current)) padInput.current.blur();
    padInput.current = null;
  }
  if (!silent) consoleSound('move');
  updateHints();
}

function homeBarPad(event) {
  const bar = home.element.querySelector('.home-bar');
  if (!bar) {
    leaveHomeBar(true);
    return;
  }
  switch (event.name) {
    case 'left': case 'right': case 'up':
      padMove(bar, 'home-bar', event.name);
      break;
    case 'down':
      leaveHomeBar();
      break;
    case 'a':
      padActivate(bar, 'home-bar');
      break;
    case 'b':
      leaveHomeBar(true);
      consoleSound('back');
      break;
    case 'start':
      openPowerMenu();
      break;
    case 'view':
      exitHome();
      break;
  }
}

function homePad(event) {
  if (home.zone === 'bar') {
    homeBarPad(event);
    return;
  }
  switch (event.name) {
    case 'left': homeMove(-1, 0); break;
    case 'right': homeMove(1, 0); break;
    case 'down': homeMove(0, 1); break;
    case 'up':
      if (homeLayout() === 'covers' || home.row === 0 || !home.sections.length) enterHomeBar();
      else homeMove(0, -1);
      break;
    case 'lt': homeMove(-6, 0); break;
    case 'rt': homeMove(6, 0); break;
    case 'lb': homeSwitch(-1); break;
    case 'rb': homeSwitch(1); break;
    case 'a': homeActivate(); break;
    case 'x': homeDetails(); break;
    case 'y': homeFavorite(); break;
    case 'b':
      if (!clearHomeSearch()) padFeedback('error');
      break;
    case 'ls': openHomeSearch(); break;
    case 'rs':
      setHomeLayout(homeLayout() === 'thumbnails' ? 'covers' : 'thumbnails');
      consoleSound('select');
      break;
    case 'start': openPowerMenu(); break;
    case 'view':
      exitHome();
      consoleSound('back');
      break;
  }
}

const POWER_ACTIONS = [
  { key: 'sleep', label: 'Sleep', icon: 'sleep', confirm: 'Put this PC to sleep?', button: 'Sleep' },
  { key: 'restart', label: 'Restart', icon: 'restart', confirm: 'Restart this PC now?', note: 'Unsaved work in other apps will be lost.', button: 'Restart', danger: true },
  { key: 'shutdown', label: 'Shut down', icon: 'power', confirm: 'Shut down this PC now?', note: 'Unsaved work in other apps will be lost.', button: 'Shut down', danger: true },
  { key: 'signout', label: 'Sign out', icon: 'signOut', confirm: 'Sign out of Windows?', note: 'Every open app will close.', button: 'Sign out', danger: true },
  { key: 'console', label: 'Exit console mode', icon: 'desktop', confirm: 'Leave console mode?', note: 'Game Hub goes back to the desktop library.', button: 'Exit console mode', local: true, group: true },
  { key: 'exit', label: 'Exit Game Hub', icon: 'exitApp', confirm: 'Close Game Hub?', button: 'Exit' },
];

function powerListHtml() {
  return `
    <header class="modal-head">
      <h2>Power</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="modal-body power-list">
      ${POWER_ACTIONS.map(action => `
        ${action.group ? '<span class="power-divider"></span>' : ''}
        <button class="power-option ${action.danger ? 'danger' : ''}" data-power="${action.key}">
          <span class="power-icon">${ICON[action.icon]}</span>
          <span>${escapeHtml(action.label)}</span>
        </button>`).join('')}
    </div>`;
}

function powerConfirmHtml(action) {
  return `
    <div class="power-confirm">
      <span class="power-confirm-icon ${action.danger ? 'danger' : ''}">${ICON[action.icon]}</span>
      <h2>${escapeHtml(action.confirm)}</h2>
      ${action.note ? `<p>${escapeHtml(action.note)}</p>` : ''}
      <div class="power-confirm-actions">
        <button class="secondary-button" data-power="cancel" data-pad-default>Cancel</button>
        <button class="primary-button ${action.danger ? 'danger' : ''}" data-power-confirm="${action.key}">${escapeHtml(action.button)}</button>
      </div>
    </div>`;
}

function openPowerMenu() {
  leaveHomeBar(true);
  if (typeof padInput !== 'undefined') padInput.remembered.modal = null;
  modal.open(powerListHtml(), 'power-modal');
  consoleSound('open');
}

function showPowerStep(html) {
  const panel = modal.panel;
  if (!panel) return;
  panel.innerHTML = html;
  if (typeof padInput !== 'undefined') {
    padInput.current = null;
    padInput.remembered.modal = null;
    if (controller.mode === 'controller') padEnsureFocus(panel, 'modal');
  }
}

modal.layer.addEventListener('click', event => {
  const panel = modal.panel;
  if (!panel || !panel.classList.contains('power-modal')) return;
  const choice = event.target.closest('[data-power]');
  const confirm = event.target.closest('[data-power-confirm]');

  if (choice && choice.dataset.power === 'cancel') {
    showPowerStep(powerListHtml());
    consoleSound('back');
  } else if (choice) {
    const action = POWER_ACTIONS.find(entry => entry.key === choice.dataset.power);
    if (action) showPowerStep(powerConfirmHtml(action));
  } else if (confirm) {
    const action = POWER_ACTIONS.find(entry => entry.key === confirm.dataset.powerConfirm);
    modal.close();
    if (!action) return;
    if (action.local) {
      exitConsoleMode();
      return;
    }
    fadeMusicOut();
    send({ type: 'consolePower', action: action.key });
  }
});

let volumeOsdTimer = null;

function consoleVolumeStep(delta) {
  send({ type: 'consoleVolume', delta });
}

function showVolumeOsd(level, muted) {
  let osd = document.getElementById('volumeOsd');
  if (!osd) {
    osd = document.createElement('div');
    osd.id = 'volumeOsd';
    osd.className = 'volume-osd';
    document.body.appendChild(osd);
  }
  const percent = Math.round(level * 100);
  osd.innerHTML = `${muted ? ICON.volumeMute : ICON.volume}<div class="volume-osd-track"><i style="width:${muted ? 0 : percent}%"></i></div><span>${muted ? 'Muted' : percent}</span>`;
  osd.classList.add('visible');
  clearTimeout(volumeOsdTimer);
  volumeOsdTimer = setTimeout(() => osd.classList.remove('visible'), 1600);
}

const music = {
  tracks: [],
  order: [],
  position: 0,
  audio: null,
  current: null,
  fade: null,
  blocked: false,
  held: false,
  requested: '',
  failures: 0,
};

function musicVolume() {
  const volume = Number(setting('console.musicVolume', 0.3));
  return Math.max(0, Math.min(1, Number.isFinite(volume) ? volume : 0.3)) * 0.6;
}

function musicWanted() {
  return home.visible && !music.held && !document.hidden &&
    !!setting('console.music', false) && !!setting('console.musicPath', '') &&
    music.tracks.length > 0 && !Object.keys(state.running || {}).length;
}

function shuffleTracks() {
  const order = music.tracks.map((_, index) => index);
  for (let index = order.length - 1; index > 0; index--) {
    const swap = Math.floor(Math.random() * (index + 1));
    [order[index], order[swap]] = [order[swap], order[index]];
  }
  music.order = order;
  music.position = 0;
}

function musicAudio() {
  if (music.audio) return music.audio;
  const audio = new Audio();
  audio.preload = 'auto';
  audio.volume = 0;
  audio.addEventListener('ended', () => nextTrack());
  audio.addEventListener('playing', () => {
    music.failures = 0;
  });
  audio.addEventListener('error', () => {
    if (!audio.getAttribute('src')) return;
    music.failures++;
    if (music.failures < Math.min(5, music.tracks.length)) nextTrack();
  });
  music.audio = audio;
  return audio;
}

function rampMusic(target, duration, then) {
  const audio = music.audio;
  if (!audio) return;
  clearInterval(music.fade);
  const start = audio.volume;
  const began = performance.now();
  music.fade = setInterval(() => {
    const progress = Math.min(1, (performance.now() - began) / duration);
    audio.volume = Math.max(0, Math.min(1, start + (target - start) * progress));
    if (progress >= 1) {
      clearInterval(music.fade);
      music.fade = null;
      if (then) then();
    }
  }, 40);
}

function loadTrack() {
  if (!music.tracks.length) return;
  if (!music.order.length || music.position >= music.order.length) shuffleTracks();
  music.current = music.tracks[music.order[music.position]];
  musicAudio().src = music.current.url;
  const button = home.element.querySelector('[data-home-action="music"]');
  if (button) button.title = `Music: ${music.current.name}`;
}

function nextTrack() {
  music.position++;
  loadTrack();
  if (musicWanted()) playMusic();
}

function playMusic() {
  const audio = musicAudio();
  if (!music.current || !audio.getAttribute('src')) loadTrack();
  const playing = audio.play();
  if (playing && playing.then) {
    playing.then(() => {
      music.blocked = false;
      if (musicWanted()) rampMusic(musicVolume(), 1600);
      else fadeMusicOut();
    }).catch(() => {
      music.blocked = true;
    });
  } else {
    rampMusic(musicVolume(), 1600);
  }
}

function fadeMusicOut() {
  const audio = music.audio;
  if (!audio || audio.paused) return;
  rampMusic(0, 900, () => audio.pause());
}

function syncMusic() {
  const path = setting('console.musicPath', '');
  if (setting('console.music', false) && path && music.requested !== path) {
    music.requested = path;
    send({ type: 'consoleMusicList' });
  }
  if (musicWanted()) {
    if (!music.audio || music.audio.paused) playMusic();
    else rampMusic(musicVolume(), 600);
  } else {
    fadeMusicOut();
  }
}

function retryHomeMusic() {
  if (music.blocked && musicWanted()) playMusic();
}

function toggleMusic() {
  if (!setting('console.musicPath', '')) {
    send({ type: 'consoleMusicPick', mode: 'folder' });
    return;
  }
  setSetting('console.music', !setting('console.music', false));
  music.held = false;
  syncMusic();
  padFeedback('select');
  const button = home.element.querySelector('[data-home-action="music"]');
  if (button) button.classList.toggle('on', !!setting('console.music', false));
}

window.addEventListener('pointerdown', retryHomeMusic, true);
window.addEventListener('keydown', retryHomeMusic, true);

window.addEventListener('focus', () => {
  if (music.held) {
    music.held = false;
    syncMusic();
  }
  requestStatus();
});

document.addEventListener('visibilitychange', () => {
  syncMusic();
  if (!document.hidden) requestStatus();
});

extensions.launchGuards.push(() => {
  music.held = true;
  fadeMusicOut();
  stopHomeVideo();
  padFeedback('launch');
  setTimeout(() => {
    if (music.held && document.hasFocus()) {
      music.held = false;
      syncMusic();
    }
  }, 20 * 1000);
  return undefined;
});

home.element.addEventListener('click', event => {
  const tile = event.target.closest('.home-tile');
  const tab = event.target.closest('[data-home-tab]');
  const layout = event.target.closest('[data-home-layout]');
  const action = event.target.closest('[data-home-action]');
  if (controller.mode !== 'controller') leaveHomeBar(true);

  if (tile) {
    leaveHomeBar(true);
    const row = Number(tile.dataset.row);
    const column = Number(tile.dataset.col);
    const section = home.sections[row];
    const alreadyFocused = row === home.row && (home.columns[section.key] || 0) === column;
    home.row = row;
    home.columns[section.key] = column;
    if (alreadyFocused) {
      homeActivate();
    } else {
      consoleSound('move');
      updateHomeFocus();
    }
  } else if (tab) {
    home.row = Number(tab.dataset.homeTab);
    consoleSound('move');
    renderHome(null);
  } else if (layout) {
    setHomeLayout(layout.dataset.homeLayout);
  } else if (action) {
    switch (action.dataset.homeAction) {
      case 'activate': homeActivate(); break;
      case 'resume': homeResume(); break;
      case 'details': homeDetails(); break;
      case 'favorite': homeFavorite(); break;
      case 'search': openHomeSearch(); break;
      case 'clear-search': clearHomeSearch(); break;
      case 'music': toggleMusic(); break;
      case 'power': openPowerMenu(); break;
      case 'mute': send({ type: 'consoleVolume', mute: !(home.status && home.status.volume && home.status.volume.muted) }); break;
      case 'exit': exitHome(); break;
    }
  }
});

home.element.addEventListener('contextmenu', event => {
  const tile = event.target.closest('.home-tile');
  if (!tile) return;
  event.preventDefault();
  const item = home.sections[Number(tile.dataset.row)]?.items[Number(tile.dataset.col)];
  if (item && item.kind === 'game') openContextMenu(item.game, event.clientX, event.clientY);
});

let wheelLock = 0;
home.element.addEventListener('wheel', event => {
  if (!home.visible || performance.now() < wheelLock) return;
  const horizontal = Math.abs(event.deltaX) > Math.abs(event.deltaY) || event.shiftKey;
  const delta = horizontal ? (event.deltaX || event.deltaY) : event.deltaY;
  if (Math.abs(delta) < 4) return;
  wheelLock = performance.now() + 140;
  if (horizontal || homeLayout() === 'covers') homeMove(delta > 0 ? 1 : -1, 0);
  else homeMove(0, delta > 0 ? 1 : -1);
}, { passive: true });

window.addEventListener('keydown', event => {
  if (typeof osk !== 'undefined' && osk.open) return;
  if (!home.visible) {
    if (event.key === 'Home' && event.target.tagName !== 'INPUT' && !modal.isOpen) {
      event.preventDefault();
      enterHome();
    }
    return;
  }

  if (modal.isOpen || state.selectedId || elements.contextMenu.classList.contains('open') || event.target.tagName === 'INPUT') return;

  if (event.ctrlKey && (event.key === 'k' || event.key === 'f')) {
    event.preventDefault();
    event.stopImmediatePropagation();
    openHomeSearch();
    return;
  }
  if (event.ctrlKey || event.altKey || event.metaKey) return;

  leaveHomeBar(true);
  let handled = true;
  switch (event.key) {
    case 'ArrowLeft': homeMove(-1, 0); break;
    case 'ArrowRight': homeMove(1, 0); break;
    case 'ArrowUp': homeMove(0, -1); break;
    case 'ArrowDown': homeMove(0, 1); break;
    case 'Enter': homeActivate(); break;
    case ' ': homeDetails(); break;
    case 'f': case 'F': homeFavorite(); break;
    case 'Tab': homeSwitch(event.shiftKey ? -1 : 1); break;
    case 's': case 'S': case '/': openHomeSearch(); break;
    case 'Home': exitHome(); break;
    case 'Escape': handled = clearHomeSearch(); break;
    default: handled = event.key.length === 1;
  }

  if (handled) {
    event.preventDefault();
    event.stopImmediatePropagation();
  }
}, true);

function updateHints() {
  const hints = home.element.querySelector('[data-home-hints]');
  if (hints) hints.innerHTML = hintsHtml();
}

const GUIDE_MODES = [
  { key: 'off', label: 'Off' },
  { key: 'smart', label: 'When no game is in front' },
  { key: 'always', label: 'Always' },
];

function guideMode() {
  const value = setting('console.guideButton', 'smart');
  if (value === true) return 'smart';
  if (value === false) return 'off';
  return GUIDE_MODES.some(mode => mode.key === value) ? value : 'smart';
}

function consoleToggleHtml(key, fallback, label, note) {
  const on = !!setting(key, fallback);
  return `
    <div class="console-setting">
      <div class="console-setting-text"><strong>${escapeHtml(label)}</strong>${note ? `<span>${escapeHtml(note)}</span>` : ''}</div>
      <button class="console-switch ${on ? 'on' : ''}" role="switch" aria-checked="${on}" data-console-setting="${escapeHtml(key)}" data-fallback="${fallback}"><i></i></button>
    </div>`;
}

function consoleSettingsInner() {
  const path = setting('console.musicPath', '');
  const volume = Math.round(Number(setting('console.musicVolume', 0.3)) * 100);
  const mode = guideMode();
  return `
    ${consoleToggleHtml('console.sounds', true, 'Navigation sounds', 'Plays on the Home screen and while using a controller.')}
    ${consoleToggleHtml('console.rumble', true, 'Controller rumble', 'A short buzz when you select or launch something.')}
    <div class="console-setting">
      <div class="console-setting-text"><strong>Guide button opens Home</strong><span>Brings Game Hub forward from the tray or background. With a game in front, hold the button for a second.</span></div>
      <div class="console-choices">
        ${GUIDE_MODES.map(choice => `<button class="secondary-button compact ${choice.key === mode ? 'on' : ''}" data-console-guide="${choice.key}">${escapeHtml(choice.label)}</button>`).join('')}
      </div>
    </div>
    ${consoleToggleHtml('console.music', false, 'Home background music', 'Shuffles your own music quietly while the Home screen is open.')}
    <div class="console-setting">
      <div class="console-setting-text"><strong>Music source</strong><span class="console-path">${path ? escapeHtml(path) : 'Nothing chosen yet'}</span></div>
      <div class="console-choices">
        <button class="secondary-button compact" data-console-music="folder">${ICON.folder || ''}Choose folder</button>
        <button class="secondary-button compact" data-console-music="file">${ICON.music}Choose file</button>
      </div>
    </div>
    <div class="console-setting">
      <div class="console-setting-text"><strong>Music volume</strong></div>
      <div class="console-volume">
        <input type="range" min="0" max="100" step="5" value="${volume}" data-console-volume>
        <span data-console-volume-label>${volume}%</span>
      </div>
    </div>`;
}

function refreshConsoleSettings() {
  for (const wrapper of document.querySelectorAll('[data-console-settings]')) wrapper.innerHTML = consoleSettingsInner();
}

extensions.settingsSections.push({
  id: 'console',
  title: 'Console mode',
  render: () => `<div class="console-settings" data-console-settings>${consoleSettingsInner()}</div>`,
  bind(root) {
    const wrapper = root.matches && root.matches('[data-console-settings]') ? root : root.querySelector('[data-console-settings]');
    if (!wrapper || wrapper.dataset.bound) return;
    wrapper.dataset.bound = '1';

    wrapper.addEventListener('click', event => {
      const toggle = event.target.closest('[data-console-setting]');
      const guide = event.target.closest('[data-console-guide]');
      const source = event.target.closest('[data-console-music]');
      if (toggle) {
        const key = toggle.dataset.consoleSetting;
        setSetting(key, !setting(key, toggle.dataset.fallback === 'true'));
        if (key === 'console.music') {
          music.held = false;
          syncMusic();
        }
        refreshConsoleSettings();
      } else if (guide) {
        setSetting('console.guideButton', guide.dataset.consoleGuide);
        refreshConsoleSettings();
      } else if (source) {
        send({ type: 'consoleMusicPick', mode: source.dataset.consoleMusic });
      }
    });

    wrapper.addEventListener('input', event => {
      const slider = event.target.closest('[data-console-volume]');
      if (!slider) return;
      state.settings['console.musicVolume'] = Number(slider.value) / 100;
      const label = wrapper.querySelector('[data-console-volume-label]');
      if (label) label.textContent = `${slider.value}%`;
      if (music.audio && !music.audio.paused && !music.fade) music.audio.volume = musicVolume();
    });

    wrapper.addEventListener('change', event => {
      const slider = event.target.closest('[data-console-volume]');
      if (slider) setSetting('console.musicVolume', Number(slider.value) / 100);
    });
  },
});

setInterval(() => {
  const clock = home.element.querySelector('[data-home-clock]');
  if (clock) clock.textContent = clockText();
}, 10 * 1000);

setInterval(requestStatus, STATUS_INTERVAL);

let homeRefreshTimer = null;
extensions.afterRender.push(() => {
  if (!home.started && state.loaded) {
    home.started = true;
    if (home.visible) {
      renderHome();
      requestStatus();
      syncMusic();
    }
    return;
  }
  clearTimeout(homeRefreshTimer);
  homeRefreshTimer = setTimeout(refreshHome, 120);
});

extensions.messageHandlers.push(message => {
  switch (message.type) {
    case 'freeGames':
    case 'art':
    case 'running':
    case 'meta':
      clearTimeout(homeRefreshTimer);
      homeRefreshTimer = setTimeout(() => {
        refreshHome();
        if (message.type === 'meta' && home.visible) scheduleHomeVideo(currentItem());
      }, 300);
      if (message.type === 'running') setTimeout(syncMusic, 400);
      break;

    case 'openHome':
      openHome();
      break;

    case 'consoleStatus':
      home.status = message;
      renderStatus();
      break;

    case 'consoleVolume':
      home.status = home.status || {};
      home.status.volume = { level: message.level, muted: message.muted };
      renderStatus();
      if (message.show !== false) showVolumeOsd(message.level, message.muted);
      break;

    case 'consoleMusic':
      music.tracks = Array.isArray(message.tracks) ? message.tracks : [];
      music.requested = message.path || setting('console.musicPath', '');
      music.order = [];
      music.current = null;
      music.failures = 0;
      if (music.audio) {
        music.audio.pause();
        music.audio.removeAttribute('src');
      }
      if (message.picked && !music.tracks.length) toast('No music files were found there', 'error');
      syncMusic();
      if (home.visible) renderHome();
      break;

    case 'settings':
      refreshConsoleSettings();
      syncMusic();
      break;
  }
});

extensions.sidebarTop.push(() => `
  <div class="nav-section home-nav">
    <button class="nav-item home-entry" data-home-enter title="Home screen (Home key)">
      <span class="nav-icon">${ICON.home}</span>
      <span class="nav-label">Home screen</span>
      <span class="nav-count"></span>
    </button>
  </div>`);

elements.sidebar.addEventListener('click', event => {
  if (event.target.closest('[data-home-enter]')) enterHome();
});

window.addEventListener('resize', () => {
  if (home.visible) updateHomeFocus(true);
});

if (state.prefs.lastMode !== 'library') {
  home.visible = true;
  document.body.classList.add('home-active');
  renderHome();
}
