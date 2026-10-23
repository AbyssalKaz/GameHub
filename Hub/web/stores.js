'use strict';

ICON.store = '<svg viewBox="0 0 24 24" class="glyph"><path d="M4 8.5 5.6 4.5h12.8L20 8.5"/><path d="M4 8.5h16v1a2.75 2.75 0 0 1-5.33.95 2.75 2.75 0 0 1-5.34 0A2.75 2.75 0 0 1 4 9.5z"/><path d="M5.5 12.2v7.3a1 1 0 0 0 1 1h11a1 1 0 0 0 1-1v-7.3M10 20.5v-4.5h4v4.5"/></svg>';
ICON.back = '<svg viewBox="0 0 24 24" class="glyph"><path d="M19 12H5m0 0 6-6m-6 6 6 6"/></svg>';
ICON.forward = '<svg viewBox="0 0 24 24" class="glyph"><path d="M5 12h14m0 0-6-6m6 6-6 6"/></svg>';
ICON.stop = '<svg viewBox="0 0 24 24" class="glyph"><path d="M6.5 6.5l11 11M17.5 6.5l-11 11"/></svg>';
ICON.plus = '<svg viewBox="0 0 24 24" class="glyph"><path d="M12 5v14M5 12h14"/></svg>';
ICON.globe = '<svg viewBox="0 0 24 24" class="glyph"><circle cx="12" cy="12" r="8.5"/><path d="M3.5 12h17M12 3.5c2.4 2.6 3.6 5.4 3.6 8.5s-1.2 5.9-3.6 8.5c-2.4-2.6-3.6-5.4-3.6-8.5S9.6 6.1 12 3.5z"/></svg>';
ICON.storeLock = '<svg viewBox="0 0 24 24" class="glyph"><rect x="5" y="10.5" width="14" height="10" rx="2.2"/><path d="M8.5 10.5V8a3.5 3.5 0 0 1 7 0v2.5"/></svg>';
ICON.storeShield = '<svg viewBox="0 0 24 24" class="glyph"><path d="M12 3.5 5 6.3v5.2c0 4.3 2.9 7.6 7 9 4.1-1.4 7-4.7 7-9V6.3z"/><path d="m9 12 2.2 2.2L15.5 10"/></svg>';
ICON.storeShieldOff = '<svg viewBox="0 0 24 24" class="glyph"><path d="M12 3.5 5 6.3v5.2c0 4.3 2.9 7.6 7 9 4.1-1.4 7-4.7 7-9V6.3z"/><path d="M4 4l16 16"/></svg>';
ICON.storeLibrary = '<svg viewBox="0 0 24 24" class="glyph"><path d="M6 4.5h12a1 1 0 0 1 1 1v15l-7-4-7 4v-15a1 1 0 0 1 1-1z"/></svg>';
ICON.storePause = '<svg viewBox="0 0 24 24" class="glyph"><path d="M9 6v12M15 6v12"/></svg>';
ICON.storeResume = '<svg viewBox="0 0 24 24" class="glyph"><path d="M8 5.8v12.4L18 12z"/></svg>';
ICON.storeRetry = '<svg viewBox="0 0 24 24" class="glyph"><path d="M4 12a8 8 0 1 0 2.34-5.66"/><path d="M4 4v5h5"/></svg>';
ICON.storeTrash = '<svg viewBox="0 0 24 24" class="glyph"><path d="M4.5 7h15M10 11v6M14 11v6M6.5 7l1 12a2 2 0 0 0 2 1.8h5a2 2 0 0 0 2-1.8l1-12M9 7V4.5h6V7"/></svg>';
ICON.storeFile = '<svg viewBox="0 0 24 24" class="glyph"><path d="M14 3.5H7a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8.5z"/><path d="M14 3.5v5h5"/></svg>';
ICON.storeArchive = '<svg viewBox="0 0 24 24" class="glyph"><rect x="4" y="4" width="16" height="16" rx="2.5"/><path d="M12 4v3m0 2v2m0 2v2"/><rect x="10.5" y="15" width="3" height="3" rx=".8"/></svg>';
ICON.storeApp = '<svg viewBox="0 0 24 24" class="glyph"><rect x="3.5" y="4.5" width="17" height="15" rx="2.5"/><path d="M3.5 9h17M7 6.8h.01M9.5 6.8h.01"/></svg>';
ICON.storeSpeaker = '<svg viewBox="0 0 24 24" class="glyph"><path d="M4.5 9.5h3l4.5-4v13l-4.5-4h-3z"/><path d="M15.5 9a4 4 0 0 1 0 6M18 6.5a7.5 7.5 0 0 1 0 11"/></svg>';
ICON.storeMuted = '<svg viewBox="0 0 24 24" class="glyph"><path d="M4.5 9.5h3l4.5-4v13l-4.5-4h-3z"/><path d="m16 9.5 5 5M21 9.5l-5 5"/></svg>';
ICON.storePerson = '<svg viewBox="0 0 24 24" class="glyph"><circle cx="12" cy="8.5" r="3.5"/><path d="M5 19.5c1.2-3.2 3.8-5 7-5s5.8 1.8 7 5"/></svg>';
ICON.storeMinus = '<svg viewBox="0 0 24 24" class="glyph"><path d="M5 12h14"/></svg>';

const OFFICIAL_STORES = [
  { key: 'steam', name: 'Steam', url: 'https://store.steampowered.com/', icon: 'steam' },
  { key: 'epic', name: 'Epic Games', url: 'https://store.epicgames.com/', icon: 'epic' },
  { key: 'gog', name: 'GOG', url: 'https://www.gog.com/' },
  { key: 'xbox', name: 'Xbox', url: 'https://www.xbox.com/games/browse', icon: 'xbox' },
  { key: 'ea', name: 'EA', url: 'https://www.ea.com/games', icon: 'ea' },
  { key: 'ubisoft', name: 'Ubisoft', url: 'https://store.ubisoft.com/' },
  { key: 'humble', name: 'Humble', url: 'https://www.humblebundle.com/store' },
  { key: 'itch', name: 'itch.io', url: 'https://itch.io/games' },
];

VIEWS.stores = 'Stores';

const storeView = {
  shown: false,
  started: false,
  adding: false,
  tabs: [],
  activeId: 0,
  maxTabs: 8,
  url: '',
  title: '',
  blank: true,
  canGoBack: false,
  canGoForward: false,
  loading: false,
  zoom: 1,
  host: '',
  profile: '',
  blockAds: true,
  siteAllowed: false,
  official: false,
  blocked: 0,
  popups: 0,
  filters: { builtIn: 0, user: 0, status: '' },
  downloadsActive: 0,
  custom: readStorage('hub.customStores', []),
  panel: null,
  libraryTab: 'bookmarks',
  downloads: [],
  downloadsLoaded: false,
  downloadFolder: '',
  scanner: true,
  history: [],
  historyQuery: '',
  confirmQueue: [],
  confirming: null,
  boundsKey: '',
  syncQueued: false,
  snapshotTimer: null,
  resizeObserver: null,
};

function allStores() {
  return [...OFFICIAL_STORES, ...storeView.custom];
}

function currentStore() {
  return allStores().find(store => store.key === state.prefs.storeKey) || OFFICIAL_STORES[0];
}

function storeHost(url) {
  try {
    return new URL(url).host;
  } catch {
    return '';
  }
}

function siteOf(host) {
  return String(host || '').toLowerCase().replace(/^www\./, '');
}

function storeMatchesHost(store, host) {
  const site = siteOf(storeHost(store.url));
  const current = siteOf(host);
  if (!site || !current) return false;
  const root = site.split('.').slice(-2).join('.');
  return current === site || current.endsWith('.' + root) || current === root;
}

function activeStoreKey() {
  const tab = activeTab();
  if (!tab || storeView.blank) return null;
  const custom = storeView.custom.find(store => store.profile && store.profile === tab.profile);
  if (custom) return custom.key;
  const match = allStores().find(store => !store.profile && storeMatchesHost(store, storeView.host));
  return match ? match.key : null;
}

function letterIcon(label, image, extraClass = '') {
  const letter = (String(label || '?').replace(/^www\./, '').charAt(0) || '?').toUpperCase();
  return `<span class="store-icon letter ${extraClass}" data-letter="${escapeHtml(letter)}">${image ? `<img class="store-fav" src="${escapeHtml(image)}" alt="">` : ''}</span>`;
}

function hostFavicon(host) {
  return host ? `https://${host}/favicon.ico` : '';
}

function storeIcon(store) {
  if (store.icon && state.platformIcons[store.icon]) return `<span class="store-icon"><img src="${escapeHtml(state.platformIcons[store.icon])}" alt=""></span>`;
  return letterIcon(store.name, hostFavicon(storeHost(store.url)));
}

function storeNavItem() {
  const busy = storeView.downloadsActive > 0;
  return `
    <button class="nav-item ${state.view === 'stores' ? 'active' : ''}" data-view="stores" title="Browse stores">
      <span class="nav-icon">${ICON.store}</span>
      <span class="nav-label">Browse stores</span>
      <span class="nav-count">${busy ? `<span class="store-nav-downloads" title="${storeView.downloadsActive} downloading">${ICON.download}${storeView.downloadsActive}</span>` : ''}</span>
    </button>`;
}

function storeTabsHtml() {
  const active = activeStoreKey();
  const chips = allStores().map(store => `
    <button class="store-tab ${store.key === active ? 'active' : ''}" data-store="${escapeHtml(store.key)}" title="${escapeHtml(store.url)}${store.profile ? ' · separate login' : ''}">
      ${storeIcon(store)}<span>${escapeHtml(store.name)}</span>
      ${store.profile ? `<span class="store-profile-mark" title="Uses its own login">${ICON.storePerson}</span>` : ''}
      ${store.custom ? `<span class="store-remove" data-remove-store="${escapeHtml(store.key)}" title="Remove ${escapeHtml(store.name)}">${ICON.close}</span>` : ''}
    </button>`).join('');

  const add = storeView.adding
    ? `<form class="store-add-form" data-store-add>
         <input name="name" type="text" placeholder="Name" autocomplete="off" spellcheck="false">
         <input name="url" type="text" placeholder="Address, e.g. example.com" autocomplete="off" spellcheck="false">
         <label class="store-add-profile" title="Keeps this site's logins and cookies apart from the other stores">
           <input name="profile" type="checkbox"><span class="store-check"></span>Separate login
         </label>
         <button class="text-button" type="submit">Add</button>
         <button class="icon-button" type="button" data-store-add-cancel title="Cancel">${ICON.close}</button>
       </form>`
    : `<button class="store-tab add" data-store-add-open title="Pin another site">${ICON.plus}<span>Add site</span></button>`;

  return chips + add;
}

function activeTab() {
  return storeView.tabs.find(tab => tab.id === storeView.activeId) || null;
}

function tabIconHtml(tab) {
  if (tab.loading && !tab.blank) return '<span class="tab-spinner"></span>';
  if (tab.blank) return `<span class="store-icon tab-blank">${ICON.store}</span>`;
  return letterIcon(storeHost(tab.url) || tab.title, tab.favicon || hostFavicon(storeHost(tab.url)));
}

function browserStripHtml() {
  const tabs = storeView.tabs.map(tab => {
    const profile = tab.profile ? storeView.custom.find(store => store.profile === tab.profile) : null;
    const sound = tab.audible || tab.muted
      ? `<button class="tab-sound ${tab.muted ? 'muted' : ''}" data-tab-mute="${tab.id}" title="${tab.muted ? 'Unmute tab' : 'Mute tab'}">${tab.muted ? ICON.storeMuted : ICON.storeSpeaker}</button>`
      : '';
    return `
      <div class="browser-tab ${tab.id === storeView.activeId ? 'active' : ''} ${tab.suspended ? 'sleeping' : ''}" data-tab="${tab.id}" title="${escapeHtml(tab.title)}${tab.url ? '\n' + escapeHtml(tab.url) : ''}${profile ? '\nLogin: ' + escapeHtml(profile.name) : ''}">
        <span class="tab-icon">${tabIconHtml(tab)}</span>
        <span class="tab-title">${escapeHtml(tab.blank && !tab.loading ? 'New tab' : tab.title)}</span>
        ${profile ? `<span class="tab-profile" title="Separate login for ${escapeHtml(profile.name)}">${ICON.storePerson}</span>` : ''}
        ${sound}
        <button class="tab-close" data-tab-close="${tab.id}" title="Close tab (Ctrl+W)">${ICON.close}</button>
      </div>`;
  }).join('');
  const full = storeView.tabs.length >= storeView.maxTabs;
  return `${tabs}<button class="browser-newtab" data-browser="newTab" title="${full ? `Up to ${storeView.maxTabs} tabs` : 'New tab (Ctrl+T)'}" ${full ? 'disabled' : ''}>${ICON.plus}</button>`;
}

function bookmarks() {
  const list = setting('store.bookmarks', []);
  return Array.isArray(list) ? list.filter(entry => entry && entry.url) : [];
}

function isBookmarked(url) {
  return !!url && bookmarks().some(entry => entry.url === url);
}

function shieldHtml() {
  if (!storeView.host) return '';
  if (!storeView.blockAds) {
    return `<button class="store-nav-button store-shield off" data-store-settings title="Ad blocking is off. Turn it on in Settings.">${ICON.storeShieldOff}</button>`;
  }
  if (storeView.siteAllowed) {
    return `<button class="store-nav-button store-shield off" data-browser="toggleAds" title="Ads and pop-ups are allowed on ${escapeHtml(siteOf(storeView.host))}. Click to block them again.">${ICON.storeShieldOff}</button>`;
  }
  const count = storeView.blocked;
  const popups = storeView.popups ? `, including ${storeView.popups} pop-up${storeView.popups === 1 ? '' : 's'}` : '';
  const title = count
    ? `${count} ad and tracker request${count === 1 ? '' : 's'} blocked on this page${popups}. Click to allow ads on ${siteOf(storeView.host)}.`
    : `Blocking ads, trackers and pop-ups. Click to allow ads on ${siteOf(storeView.host)}.`;
  return `<button class="store-nav-button store-shield ${count ? 'active' : ''}" data-browser="toggleAds" title="${escapeHtml(title)}">${ICON.storeShield}${count ? `<span class="store-badge">${count > 99 ? '99+' : count}</span>` : ''}</button>`;
}

function downloadsButtonHtml() {
  const active = storeView.downloads.filter(item => item.State === 'downloading');
  let ring = '';
  if (active.length) {
    const known = active.filter(item => item.Total > 0);
    const progress = known.length ? known.reduce((sum, item) => sum + item.Received, 0) / known.reduce((sum, item) => sum + item.Total, 0) : 0;
    const circumference = 2 * Math.PI * 10;
    ring = `<svg class="store-ring" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10"/><circle class="value" cx="12" cy="12" r="10" stroke-dasharray="${circumference.toFixed(2)}" stroke-dashoffset="${(circumference * (1 - Math.min(1, progress))).toFixed(2)}"/></svg>`;
  }
  const threat = storeView.downloads.some(item => item.Scan === 'threat' && item.State === 'completed');
  return `<button class="store-nav-button store-downloads-button ${storeView.panel === 'downloads' ? 'pressed' : ''} ${active.length ? 'busy' : ''} ${threat ? 'threat' : ''}" data-store-panel="downloads" title="Downloads (Ctrl+J)">${ring}${ICON.download}${active.length ? `<span class="store-badge">${active.length}</span>` : ''}</button>`;
}

function toolbarHtml() {
  const secure = /^https:/i.test(storeView.url);
  const showUrl = storeView.blank ? '' : storeView.url;
  const starred = isBookmarked(storeView.url);
  const zoom = Math.round((storeView.zoom || 1) * 100);
  return `
    <button class="store-nav-button" data-browser="back" title="Back (Alt+Left)" ${storeView.canGoBack ? '' : 'disabled'}>${ICON.back}</button>
    <button class="store-nav-button" data-browser="forward" title="Forward (Alt+Right)" ${storeView.canGoForward ? '' : 'disabled'}>${ICON.forward}</button>
    <button class="store-nav-button" data-browser="reload" title="${storeView.loading ? 'Stop' : 'Reload (F5)'}" ${activeTab() && !storeView.blank ? '' : 'disabled'}>${storeView.loading ? ICON.stop : ICON.refresh}</button>
    <form class="store-address ${storeView.loading ? 'loading' : ''}" data-browser-address>
      <span class="store-address-icon ${secure ? 'secure' : ''}" title="${secure ? 'Secure connection' : ''}">${secure ? ICON.storeLock : ICON.globe}</span>
      <input name="address" type="text" value="${escapeHtml(showUrl)}" placeholder="Search or enter an address" autocomplete="off" spellcheck="false">
      ${showUrl ? `<button type="button" class="store-star ${starred ? 'on' : ''}" data-store-bookmark title="${starred ? 'Remove bookmark' : 'Bookmark this page'} (Ctrl+D)">${starred ? ICON.starFilled : ICON.star}</button>` : ''}
    </form>
    ${zoom !== 100 ? `<div class="store-zoom" title="Page zoom (Ctrl + mouse wheel)">
        <button data-browser-zoom="-1" title="Zoom out (Ctrl+-)">${ICON.storeMinus}</button>
        <button class="store-zoom-value" data-browser-zoom="0" title="Reset zoom (Ctrl+0)">${zoom}%</button>
        <button data-browser-zoom="1" title="Zoom in (Ctrl++)">${ICON.plus}</button>
      </div>` : ''}
    ${shieldHtml()}
    ${downloadsButtonHtml()}
    <button class="store-nav-button ${storeView.panel === 'bookmarks' || storeView.panel === 'history' ? 'pressed' : ''}" data-store-panel="library" title="Bookmarks and history (Ctrl+H)">${ICON.storeLibrary}</button>
    <button class="store-nav-button" data-browser="external" title="Open this page in your web browser" ${storeView.blank ? 'disabled' : ''}>${ICON.external}</button>`;
}

function bookmarkBarHtml() {
  const list = bookmarks();
  if (!list.length || !setting('store.showBookmarksBar', true)) return '';
  return list.map((entry, index) => `
    <button class="bookmark-chip" data-bookmark="${index}" title="${escapeHtml(entry.title || entry.url)}\n${escapeHtml(entry.url)}">
      ${letterIcon(entry.host || storeHost(entry.url), entry.favicon || hostFavicon(entry.host || storeHost(entry.url)))}
      <span>${escapeHtml(entry.title || entry.host || entry.url)}</span>
    </button>`).join('');
}

function startPageHtml() {
  const tab = activeTab();
  if (tab && tab.loading) {
    return `<div class="store-frame-placeholder"><span class="tab-spinner large"></span><span>Loading ${escapeHtml(storeHost(tab.url) || 'page')}…</span></div>`;
  }
  const marks = bookmarks().slice(0, 12);
  const recent = [];
  const seen = new Set();
  for (const visit of storeView.history) {
    if (seen.has(visit.Host)) continue;
    seen.add(visit.Host);
    recent.push(visit);
    if (recent.length >= 8) break;
  }
  const tile = (attributes, icon, label, note) => `
    <button class="start-tile" ${attributes} title="${escapeHtml(note || label)}">
      ${icon}
      <span class="start-tile-label">${escapeHtml(label)}</span>
    </button>`;
  return `
    <div class="store-start">
      <section>
        <h3 class="store-start-title">Stores</h3>
        <div class="start-grid">${allStores().map(store => tile(`data-store="${escapeHtml(store.key)}"`, storeIcon(store), store.name, store.url)).join('')}</div>
      </section>
      ${marks.length ? `<section>
        <h3 class="store-start-title">Bookmarks</h3>
        <div class="start-grid">${marks.map((entry, index) => tile(`data-bookmark="${index}"`, letterIcon(entry.host, entry.favicon || hostFavicon(entry.host)), entry.title || entry.host, entry.url)).join('')}</div>
      </section>` : ''}
      ${recent.length ? `<section>
        <h3 class="store-start-title">Recently visited</h3>
        <div class="start-grid">${recent.map(visit => tile(`data-history-url="${escapeHtml(visit.Url)}"`, letterIcon(visit.Host, hostFavicon(visit.Host)), siteOf(visit.Host), visit.Title)).join('')}</div>
      </section>` : ''}
    </div>`;
}

function frameFillHtml() {
  if (!storeView.tabs.length || storeView.blank) return startPageHtml();
  return `<div class="store-frame-placeholder">${ICON.store}<span>${escapeHtml(storeView.title || 'Loading…')}</span></div>`;
}

function fileIcon(name) {
  const extension = String(name || '').split('.').pop().toLowerCase();
  if (['zip', 'rar', '7z', 'iso', 'tar', 'gz'].includes(extension)) return ICON.storeArchive;
  if (['exe', 'msi', 'msix', 'appx', 'bat', 'cmd'].includes(extension)) return ICON.storeApp;
  return ICON.storeFile;
}

function formatSpeed(bytesPerSecond) {
  return bytesPerSecond > 0 ? `${formatBytes(Math.round(bytesPerSecond))}/s` : '';
}

function formatRemaining(item) {
  if (!(item.Speed > 0) || !(item.Total > 0)) return '';
  const seconds = (item.Total - item.Received) / item.Speed;
  if (seconds < 60) return `${Math.max(1, Math.round(seconds))} s left`;
  if (seconds < 3600) return `${Math.round(seconds / 60)} min left`;
  return `${(seconds / 3600).toFixed(1)} h left`;
}

function downloadStatus(item) {
  const sizes = item.Total > 0 ? `${formatBytes(item.Received)} of ${formatBytes(item.Total)}` : formatBytes(item.Received);
  switch (item.State) {
    case 'downloading': return [sizes, formatSpeed(item.Speed), formatRemaining(item)].filter(Boolean).join(' · ');
    case 'pending': return 'Waiting for you to confirm';
    case 'paused': return `Paused · ${sizes}`;
    case 'interrupted': return `${item.Error || 'Interrupted'} · ${sizes}`;
    case 'failed': return item.Error || 'Failed';
    case 'cancelled': return 'Cancelled';
    case 'recycled': return 'Moved to the Recycle Bin';
    case 'completed': return [formatBytes(item.Total > 0 ? item.Total : item.Received), item.SourceHost ? siteOf(item.SourceHost) : '', item.FinishedAt ? relativeTime(new Date(item.FinishedAt).toISOString()) : ''].filter(Boolean).join(' · ');
    default: return '';
  }
}

function scanHtml(item) {
  if (item.State !== 'completed' || !item.Scan) return '';
  const labels = {
    scanning: ['scanning', '<span class="tab-spinner small"></span>', 'Scanning with Microsoft Defender…'],
    clean: ['clean', ICON.check, 'Microsoft Defender found no threats'],
    threat: ['threat', ICON.alert, `Threat found: ${item.ScanDetail || 'unknown'}`],
    unavailable: ['muted', ICON.info, item.ScanDetail || 'Microsoft Defender isn’t available'],
    error: ['muted', ICON.info, item.ScanDetail || 'The scan failed'],
  };
  const [kind, icon, text] = labels[item.Scan] || labels.error;
  return `<div class="download-scan ${kind}">${icon}<span>${escapeHtml(text)}</span></div>`;
}

function downloadActions(item) {
  const button = (command, icon, title, extra = '') => `<button class="icon-button ${extra}" data-download="${command}" title="${escapeHtml(title)}">${icon}</button>`;
  const actions = [];
  if (item.State === 'downloading') actions.push(button('pause', ICON.storePause, 'Pause'), button('cancel', ICON.close, 'Cancel'));
  else if (item.State === 'paused' || item.State === 'interrupted') actions.push(button('resume', ICON.storeResume, 'Resume'), button('cancel', ICON.close, 'Cancel'));
  else if (item.State === 'failed' || item.State === 'cancelled') actions.push(button('retry', ICON.storeRetry, 'Download again'), button('remove', ICON.close, 'Remove from list'));
  else if (item.State === 'completed') {
    if (item.Scan === 'threat') actions.push(button('recycle', ICON.storeTrash, 'Move to the Recycle Bin', 'danger'));
    else actions.push(button('open', ICON.external, 'Open'));
    actions.push(button('folder', ICON.folder, 'Show in folder'), button('remove', ICON.close, 'Remove from list'));
  } else if (item.State === 'recycled') actions.push(button('remove', ICON.close, 'Remove from list'));
  return actions.join('');
}

function downloadsPanelHtml() {
  const items = storeView.downloads;
  const rows = items.map(item => {
    const progress = item.Total > 0 ? Math.min(100, (item.Received / item.Total) * 100) : 0;
    const active = item.State === 'downloading' || item.State === 'paused' || item.State === 'interrupted';
    return `
      <div class="download-row ${escapeHtml(item.State)} ${item.Scan === 'threat' ? 'threat' : ''}" data-download-id="${escapeHtml(item.Id)}">
        <span class="download-icon">${fileIcon(item.Name)}</span>
        <div class="download-main">
          <div class="download-name" title="${escapeHtml(item.Path || item.Name)}">${escapeHtml(item.Name)}</div>
          <div class="download-status">${escapeHtml(downloadStatus(item))}</div>
          ${active ? `<div class="download-progress ${item.Total > 0 ? '' : 'indeterminate'} ${item.State !== 'downloading' ? 'paused' : ''}"><i style="width:${progress.toFixed(1)}%"></i></div>` : ''}
          ${scanHtml(item)}
        </div>
        <div class="download-actions">${downloadActions(item)}</div>
      </div>`;
  }).join('');

  return `
    <header class="store-panel-head">
      <h3>Downloads</h3>
      ${items.some(item => !['downloading', 'paused', 'interrupted', 'pending'].includes(item.State)) ? '<button class="text-button" data-download-clear>Clear list</button>' : ''}
      <button class="icon-button" data-store-panel-close title="Close">${ICON.close}</button>
    </header>
    <div class="store-panel-body">
      ${rows || `<div class="store-panel-empty">${ICON.download}<span>Files you download from the stores show up here.</span></div>`}
    </div>
    <footer class="store-panel-foot">
      <span class="store-panel-path" title="${escapeHtml(storeView.downloadFolder)}">${ICON.folder}<span>${escapeHtml(storeView.downloadFolder || 'Downloads')}</span></span>
      <button class="text-button" data-store-pick-folder>Change</button>
      ${storeView.scanner ? '' : `<div class="store-panel-note">${ICON.info}<span>Microsoft Defender isn’t available, so downloads aren’t scanned.</span></div>`}
    </footer>`;
}

function dayLabel(time) {
  const date = new Date(time);
  const today = new Date();
  const start = new Date(today.getFullYear(), today.getMonth(), today.getDate()).getTime();
  if (time >= start) return 'Today';
  if (time >= start - 86400000) return 'Yesterday';
  return date.toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' });
}

function historyListHtml() {
  const query = storeView.historyQuery.trim().toLowerCase();
  const visits = storeView.history.filter(visit => !query || (visit.Title || '').toLowerCase().includes(query) || (visit.Url || '').toLowerCase().includes(query));
  if (!visits.length) {
    return `<div class="store-panel-empty">${ICON.clock}<span>${query ? 'Nothing in your history matches that.' : 'Pages you visit in the store browser show up here.'}</span></div>`;
  }
  let lastDay = '';
  return visits.slice(0, 300).map(visit => {
    const day = dayLabel(visit.Visited);
    const heading = day !== lastDay ? `<div class="history-day">${escapeHtml(day)}</div>` : '';
    lastDay = day;
    const time = new Date(visit.Visited).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
    return `${heading}
      <div class="history-row" data-history-url="${escapeHtml(visit.Url)}" title="${escapeHtml(visit.Url)}">
        ${letterIcon(visit.Host, hostFavicon(visit.Host))}
        <div class="history-text"><span class="history-title">${escapeHtml(visit.Title || visit.Host)}</span><span class="history-host">${escapeHtml(siteOf(visit.Host))}</span></div>
        <span class="history-time">${escapeHtml(time)}</span>
        <button class="icon-button" data-history-remove="${escapeHtml(visit.Url)}" title="Remove from history">${ICON.close}</button>
      </div>`;
  }).join('');
}

function bookmarkListHtml() {
  const list = bookmarks();
  if (!list.length) {
    return `<div class="store-panel-empty">${ICON.star}<span>Press the star in the address bar or Ctrl+D to bookmark a page.</span></div>`;
  }
  return list.map((entry, index) => `
    <div class="history-row" data-bookmark="${index}" title="${escapeHtml(entry.url)}">
      ${letterIcon(entry.host, entry.favicon || hostFavicon(entry.host))}
      <div class="history-text"><span class="history-title">${escapeHtml(entry.title || entry.host)}</span><span class="history-host">${escapeHtml(siteOf(entry.host))}</span></div>
      <button class="icon-button" data-bookmark-remove="${index}" title="Remove bookmark">${ICON.close}</button>
    </div>`).join('');
}

function libraryPanelHtml() {
  const tab = storeView.libraryTab;
  return `
    <header class="store-panel-head">
      <div class="segmented store-panel-switch">
        <button class="${tab === 'bookmarks' ? 'active' : ''}" data-library-tab="bookmarks">Bookmarks</button>
        <button class="${tab === 'history' ? 'active' : ''}" data-library-tab="history">History</button>
      </div>
      <button class="icon-button" data-store-panel-close title="Close">${ICON.close}</button>
    </header>
    ${tab === 'history' ? `
      <label class="store-panel-search">${ICON.search}<input type="text" data-history-search placeholder="Search history" value="${escapeHtml(storeView.historyQuery)}" autocomplete="off" spellcheck="false"></label>
      <div class="store-panel-body" data-history-list>${historyListHtml()}</div>
      <footer class="store-panel-foot">${storeView.history.length ? '<button class="text-button" data-history-clear>Clear history</button>' : ''}</footer>`
    : `
      <div class="store-panel-body">${bookmarkListHtml()}</div>
      <footer class="store-panel-foot">
        <label class="store-inline-toggle"><input type="checkbox" data-bookmark-bar-toggle ${setting('store.showBookmarksBar', true) ? 'checked' : ''}><span class="store-check"></span>Show bookmarks bar</label>
      </footer>`}`;
}

function panelHtml() {
  if (storeView.panel === 'downloads') return downloadsPanelHtml();
  if (storeView.panel === 'bookmarks' || storeView.panel === 'history') return libraryPanelHtml();
  return '';
}

function storeRoot() {
  return elements.results.querySelector('.stores-view');
}

function renderPart(selector, html) {
  const root = storeRoot();
  const element = root && root.querySelector(selector);
  if (element && element.innerHTML !== html) element.innerHTML = html;
  return element;
}

function renderStoresView() {
  elements.pageTitle.textContent = 'Stores';
  elements.summary.innerHTML = '';
  elements.filters.innerHTML = '';
  elements.zoomControl.classList.add('hidden');

  if (storeRoot()) {
    renderStoreChrome();
    scheduleBrowserSync();
    return;
  }

  elements.results.innerHTML = `
    <div class="stores-view">
      <div class="store-tabs" data-store-tabs>${storeTabsHtml()}</div>
      <div class="store-shell">
        <div class="browser-strip" data-browser-strip>${browserStripHtml()}</div>
        <div class="store-toolbar" data-store-toolbar>${toolbarHtml()}</div>
        <div class="bookmark-bar" data-bookmark-bar>${bookmarkBarHtml()}</div>
        <div class="store-body">
          <div class="store-frame" data-store-frame>
            <div class="store-frame-fill" data-frame-fill>${frameFillHtml()}</div>
            <img class="store-snapshot" data-store-snapshot alt="" hidden>
          </div>
          <aside class="store-panel" data-store-panel-host>${panelHtml()}</aside>
        </div>
      </div>
    </div>`;

  const frame = elements.results.querySelector('[data-store-frame]');
  if (storeView.resizeObserver) storeView.resizeObserver.disconnect();
  storeView.resizeObserver = new ResizeObserver(() => scheduleBrowserSync());
  storeView.resizeObserver.observe(frame);
  updatePanelVisibility();
  scheduleBrowserSync();
}

function renderStoreChrome() {
  if (!storeRoot()) return;
  if (!document.activeElement || !document.activeElement.closest('[data-store-add]')) renderPart('[data-store-tabs]', storeTabsHtml());
  renderPart('[data-browser-strip]', browserStripHtml());
  updateToolbar();
  renderPart('[data-bookmark-bar]', bookmarkBarHtml());
  renderPart('[data-frame-fill]', frameFillHtml());
  renderPanel();
}

function renderPanel() {
  const host = storeRoot() && storeRoot().querySelector('[data-store-panel-host]');
  if (!host) return;
  const search = host.querySelector('[data-history-search]');
  const typing = search && document.activeElement === search;
  if (typing) {
    renderPart('[data-history-list]', historyListHtml());
  } else {
    const scroller = host.querySelector('.store-panel-body');
    const scroll = scroller ? scroller.scrollTop : 0;
    renderPart('[data-store-panel-host]', panelHtml());
    const fresh = host.querySelector('.store-panel-body');
    if (fresh) fresh.scrollTop = scroll;
  }
  updatePanelVisibility();
}

function updatePanelVisibility() {
  const host = storeRoot() && storeRoot().querySelector('[data-store-panel-host]');
  if (host) host.classList.toggle('open', !!storeView.panel);
}

function updateToolbar() {
  const toolbar = storeRoot() && storeRoot().querySelector('[data-store-toolbar]');
  if (!toolbar) return;
  const input = toolbar.querySelector('input[name="address"]');
  const typing = input && document.activeElement === input;
  const draft = input ? input.value : '';
  const selection = typing ? [input.selectionStart, input.selectionEnd] : null;
  const html = toolbarHtml();
  if (toolbar.dataset.html === html) return;
  toolbar.dataset.html = html;
  toolbar.innerHTML = html;
  if (typing) {
    const fresh = toolbar.querySelector('input[name="address"]');
    fresh.value = draft;
    fresh.focus();
    if (selection) fresh.setSelectionRange(selection[0], selection[1]);
  }
}

function setPanel(panel) {
  storeView.panel = panel;
  if (panel === 'history') {
    storeView.libraryTab = 'history';
    send({ type: 'browser', action: 'history' });
  } else if (panel === 'bookmarks') {
    storeView.libraryTab = 'bookmarks';
  } else if (panel === 'downloads') {
    send({ type: 'browser', action: 'download', command: 'list' });
  }
  renderPanel();
  updateToolbar();
  scheduleBrowserSync();
}

function togglePanel(panel) {
  if (panel === 'library') {
    const open = storeView.panel === 'bookmarks' || storeView.panel === 'history';
    setPanel(open ? null : storeView.libraryTab);
  } else {
    setPanel(storeView.panel === panel ? null : panel);
  }
}

function frameElement() {
  return storeRoot() && storeRoot().querySelector('[data-store-frame]');
}

function rectsOverlap(a, b) {
  return a.left < b.right && a.right > b.left && a.top < b.bottom && a.bottom > b.top;
}

function browserCovered(box) {
  if (document.body.classList.contains('home-active')) return true;
  const home = document.getElementById('home');
  if (home && home.classList.contains('visible')) return true;
  if (document.getElementById('modalLayer').classList.contains('open')) return true;
  if (elements.drawer.classList.contains('open') || elements.backdrop.classList.contains('open')) return true;

  for (const element of document.querySelectorAll('.context-menu.open, .dropdown.open .dropdown-menu, [data-covers-browser]')) {
    if (element.closest('[data-store-frame]')) continue;
    if (element.matches('[data-covers-browser]')) {
      const style = getComputedStyle(element);
      if (style.display === 'none' || style.visibility === 'hidden' || Number(style.opacity) === 0) continue;
    }
    const rect = element.getBoundingClientRect();
    if (rect.width > 0 && rect.height > 0 && rectsOverlap(rect, box)) return true;
  }
  return false;
}

function browserBounds(box) {
  let bottom = box.bottom;
  if (elements.toasts.childElementCount) {
    const rect = elements.toasts.getBoundingClientRect();
    if (rect.height > 0 && rect.left < box.right && rect.right > box.left && rect.top < box.bottom) bottom = Math.min(bottom, rect.top - 10);
  }
  return {
    x: Math.round(box.left * 100) / 100,
    y: Math.round(box.top * 100) / 100,
    width: Math.round(box.width * 100) / 100,
    height: Math.round(Math.max(80, bottom - box.top) * 100) / 100,
  };
}

function scheduleBrowserSync() {
  if (storeView.syncQueued) return;
  if (state.view !== 'stores' && !storeView.shown) return;
  storeView.syncQueued = true;
  requestAnimationFrame(() => {
    storeView.syncQueued = false;
    syncBrowser();
  });
}

function syncBrowser() {
  const frame = state.view === 'stores' ? frameElement() : null;
  const box = frame ? frame.getBoundingClientRect() : null;
  const onPage = !!box && box.width > 40 && box.height > 40;
  const covered = onPage && browserCovered(box);

  if (onPage && !covered) {
    const bounds = browserBounds(box);
    const scale = window.devicePixelRatio || 1;
    const key = `${bounds.x},${bounds.y},${bounds.width},${bounds.height}@${scale}`;
    if (storeView.shown && key === storeView.boundsKey) return;

    const message = { type: 'browser', action: storeView.shown ? 'bounds' : 'show', bounds, scale };
    if (!storeView.started) {
      storeView.started = true;
      if (!storeView.tabs.length) {
        const store = currentStore();
        message.url = store.url;
        if (store.profile) message.profile = store.profile;
      }
    }
    const wasShown = storeView.shown;
    storeView.shown = true;
    storeView.boundsKey = key;
    send(message);
    if (!wasShown) hideSnapshotSoon();
  } else if (storeView.shown) {
    storeView.shown = false;
    storeView.boundsKey = '';
    send({ type: 'browser', action: 'hide', snapshot: onPage && covered && !storeView.blank });
  }
}

function hideSnapshotSoon() {
  clearTimeout(storeView.snapshotTimer);
  storeView.snapshotTimer = setTimeout(() => {
    const image = storeRoot() && storeRoot().querySelector('[data-store-snapshot]');
    if (image && storeView.shown) {
      image.hidden = true;
      image.removeAttribute('src');
    }
  }, 160);
}

function showSnapshot(message) {
  if (storeView.shown || message.id !== storeView.activeId) return;
  const image = storeRoot() && storeRoot().querySelector('[data-store-snapshot]');
  if (!image) return;
  clearTimeout(storeView.snapshotTimer);
  image.src = message.image;
  image.hidden = false;
}

function openStore(key, newTab = false) {
  const store = allStores().find(entry => entry.key === key);
  if (!store) return;
  state.prefs.storeKey = store.key;
  savePrefs();
  storeView.started = true;
  send({ type: 'browser', action: 'open', url: store.url, profile: store.profile || '', newTab });
  renderPart('[data-store-tabs]', storeTabsHtml());
}

function openInStoreBrowser(url, newTab = false) {
  if (!url) return;
  storeView.started = true;
  if (state.view !== 'stores') setView('stores');
  send({ type: 'browser', action: 'open', url, newTab });
}

function openAddress(url, newTab, profile) {
  if (!url) return;
  storeView.started = true;
  send({ type: 'browser', action: 'open', url, profile: profile || '', newTab: !!newTab });
}

function profileFor(store) {
  return 'site-' + store.key.replace(/[^a-z0-9_-]/gi, '').slice(0, 50);
}

function addStore(name, url, separate) {
  let address = url.trim();
  if (!/^https?:\/\//i.test(address)) address = 'https://' + address;
  if (!storeHost(address)) {
    toast('That address doesn’t look right.', 'error');
    return false;
  }
  const store = { key: 'custom-' + Date.now(), name: name.trim() || siteOf(storeHost(address)), url: address, custom: true };
  if (separate) store.profile = profileFor(store);
  storeView.custom.push(store);
  writeStorage('hub.customStores', storeView.custom);
  storeView.adding = false;
  renderPart('[data-store-tabs]', storeTabsHtml());
  openStore(store.key);
  return true;
}

function removeStore(key) {
  const store = storeView.custom.find(entry => entry.key === key);
  storeView.custom = storeView.custom.filter(entry => entry.key !== key);
  writeStorage('hub.customStores', storeView.custom);
  if (state.prefs.storeKey === key) {
    state.prefs.storeKey = OFFICIAL_STORES[0].key;
    savePrefs();
  }
  renderPart('[data-store-tabs]', storeTabsHtml());
  if (store) toast(`Removed ${store.name}`, 'info');
}

function toggleBookmark() {
  if (storeView.blank || !storeView.url) return;
  const list = bookmarks();
  const index = list.findIndex(entry => entry.url === storeView.url);
  if (index >= 0) {
    list.splice(index, 1);
    toast('Bookmark removed', 'info', ICON.star);
  } else {
    const tab = activeTab();
    list.push({ url: storeView.url, title: storeView.title || storeView.host, host: storeView.host, favicon: tab && tab.favicon ? tab.favicon : '', profile: storeView.profile || '' });
    toast('Bookmarked', 'info', ICON.starFilled);
  }
  setSetting('store.bookmarks', list);
  renderStoreChrome();
}

function removeBookmark(index) {
  const list = bookmarks();
  if (index < 0 || index >= list.length) return;
  list.splice(index, 1);
  setSetting('store.bookmarks', list);
  renderStoreChrome();
}

function focusAddress() {
  const input = storeRoot() && storeRoot().querySelector('input[name="address"]');
  if (!input) return;
  input.focus();
  input.select();
}

function openBookmark(index, newTab) {
  const entry = bookmarks()[index];
  if (entry) openAddress(entry.url, newTab, entry.profile);
}

function wantsNewTab(event) {
  return event.ctrlKey || event.metaKey || event.button === 1;
}

function confirmNextDownload() {
  if (storeView.confirming || !storeView.confirmQueue.length) return;
  const request = storeView.confirmQueue.shift();
  storeView.confirming = request;
  let decided = false;
  const decide = folder => {
    if (decided) return;
    decided = true;
    send({ type: 'browser', action: 'downloadDecision', id: request.id, folder });
  };

  const folders = request.folders || [];
  const choices = folders.map((folder, index) => `
    <label class="download-target">
      <input type="radio" name="downloadTarget" value="${index}" ${folder.path === request.folder ? 'checked' : ''}>
      <span class="download-target-mark"></span>
      <span class="download-target-text"><strong>${escapeHtml(folder.label)}</strong><span>${escapeHtml(folder.path)}</span></span>
    </label>`).join('');

  modal.open(`
    <header class="modal-head">
      <h2>${request.risky ? 'Download this file?' : 'Save download'}</h2>
      <button class="icon-button large" data-modal-close title="Cancel">${ICON.close}</button>
    </header>
    <div class="modal-body">
      <div class="download-card">
        <span class="download-icon large">${fileIcon(request.name)}</span>
        <div class="download-main">
          <div class="download-name">${escapeHtml(request.name)}</div>
          <div class="download-status">${escapeHtml([request.size > 0 ? formatBytes(request.size) : '', request.host ? 'from ' + siteOf(request.host) : ''].filter(Boolean).join(' · '))}</div>
        </div>
      </div>
      ${request.risky ? `<div class="callout">${ICON.alert}<span><strong>${escapeHtml(siteOf(request.host) || 'This site')}</strong> isn’t an official store, and this is a program or archive. Only save it if you trust the site. ${storeView.scanner ? 'Microsoft Defender scans it when the download finishes.' : 'Microsoft Defender isn’t available to scan it.'}</span></div>` : ''}
      <div class="download-targets">${choices}</div>
    </div>
    <footer class="modal-foot">
      <span class="grow"></span>
      <button class="secondary-button compact" data-modal-close>Cancel</button>
      <button class="primary-button compact" data-download-save>${ICON.download}Save</button>
    </footer>`, 'download-modal');

  modal.onClose = () => {
    decide(null);
    storeView.confirming = null;
    setTimeout(confirmNextDownload, 260);
  };

  const panel = modal.panel;
  panel.querySelector('[data-download-save]').addEventListener('click', () => {
    const picked = panel.querySelector('input[name="downloadTarget"]:checked');
    const folder = folders[picked ? Number(picked.value) : 0];
    decide(folder ? folder.path : request.folder);
    modal.close();
  });
  requestAnimationFrame(() => {
    const save = panel.querySelector('[data-download-save]');
    if (save) save.focus();
  });
}

function applyDownloads(message) {
  const previous = new Set(storeView.downloads.map(item => item.Id));
  const firstLoad = !storeView.downloadsLoaded;
  storeView.downloads = message.items || [];
  storeView.downloadFolder = message.folder || '';
  storeView.scanner = message.scanner !== false;
  storeView.downloadsLoaded = true;
  storeView.downloadsActive = storeView.downloads.filter(item => item.State === 'downloading').length;

  const started = !firstLoad && storeView.downloads.some(item => !previous.has(item.Id) && item.State === 'downloading');
  if (started && state.view === 'stores' && storeView.panel !== 'downloads') {
    setPanel('downloads');
    return;
  }
  if (state.view === 'stores') {
    updateToolbar();
    if (storeView.panel === 'downloads') renderPanel();
  }
  updateNavCount();
}

function updateNavCount() {
  const count = document.querySelector('.nav-item[data-view="stores"] .nav-count');
  if (!count) return;
  const html = storeView.downloadsActive > 0 ? `<span class="store-nav-downloads" title="${storeView.downloadsActive} downloading">${ICON.download}${storeView.downloadsActive}</span>` : '';
  if (count.innerHTML !== html) count.innerHTML = html;
}

function applyBrowserState(message) {
  const previousPopups = storeView.popups;
  const previousHost = storeView.host;
  const wasBlank = storeView.blank;
  Object.assign(storeView, {
    tabs: message.tabs || [],
    activeId: message.activeId || 0,
    maxTabs: message.maxTabs || 8,
    url: message.url || '',
    title: message.title || '',
    blank: message.blank !== false,
    canGoBack: !!message.canGoBack,
    canGoForward: !!message.canGoForward,
    loading: !!message.loading,
    zoom: Number(message.zoom) || 1,
    host: message.host || '',
    profile: message.profile || '',
    blockAds: message.blockAds !== false,
    siteAllowed: !!message.siteAllowed,
    official: !!message.official,
    blocked: message.blocked || 0,
    popups: message.popups || 0,
    filters: message.filters || storeView.filters,
    downloadsActive: message.downloadsActive || 0,
  });
  if (storeView.tabs.length) storeView.started = true;

  if (storeView.popups > previousPopups && previousPopups === 0 && previousHost === storeView.host && state.view === 'stores') {
    toast(`Blocked a pop-up on ${siteOf(storeView.host)}`, 'info', ICON.storeShield);
  }
  if (storeView.blank && !wasBlank && !storeView.history.length) send({ type: 'browser', action: 'history' });

  if (state.view === 'stores') renderStoreChrome();
  updateNavCount();
}

elements.results.addEventListener('mousedown', event => {
  if (state.view === 'stores' && event.button === 1 && event.target.closest('[data-tab], [data-store], [data-bookmark], [data-history-url]')) event.preventDefault();
});

elements.results.addEventListener('auxclick', event => {
  if (state.view !== 'stores' || event.button !== 1) return;
  const tab = event.target.closest('[data-tab]');
  const store = event.target.closest('[data-store]');
  const bookmark = event.target.closest('[data-bookmark]');
  const visit = event.target.closest('[data-history-url]');
  if (tab) send({ type: 'browser', action: 'closeTab', id: Number(tab.dataset.tab) });
  else if (store) openStore(store.dataset.store, true);
  else if (bookmark) openBookmark(Number(bookmark.dataset.bookmark), true);
  else if (visit) openAddress(visit.dataset.historyUrl, true);
  else return;
  event.preventDefault();
});

elements.results.addEventListener('click', event => {
  if (state.view !== 'stores') return;
  const target = event.target;
  const closest = selector => target.closest(selector);
  let element;

  if ((element = closest('[data-remove-store]'))) {
    event.stopPropagation();
    removeStore(element.dataset.removeStore);
  } else if ((element = closest('[data-tab-close]'))) {
    send({ type: 'browser', action: 'closeTab', id: Number(element.dataset.tabClose) });
  } else if ((element = closest('[data-tab-mute]'))) {
    send({ type: 'browser', action: 'mute', id: Number(element.dataset.tabMute) });
  } else if ((element = closest('[data-tab]'))) {
    send({ type: 'browser', action: 'activate', id: Number(element.dataset.tab) });
  } else if ((element = closest('[data-store]'))) {
    openStore(element.dataset.store, wantsNewTab(event));
  } else if (closest('[data-store-add-open]')) {
    storeView.adding = true;
    renderPart('[data-store-tabs]', storeTabsHtml());
    storeRoot().querySelector('[data-store-add] input[name="name"]').focus();
  } else if (closest('[data-store-add-cancel]')) {
    storeView.adding = false;
    renderPart('[data-store-tabs]', storeTabsHtml());
  } else if (closest('[data-store-bookmark]')) {
    toggleBookmark();
  } else if ((element = closest('[data-bookmark-remove]'))) {
    removeBookmark(Number(element.dataset.bookmarkRemove));
  } else if ((element = closest('[data-bookmark]'))) {
    openBookmark(Number(element.dataset.bookmark), wantsNewTab(event));
  } else if ((element = closest('[data-history-remove]'))) {
    const url = element.dataset.historyRemove;
    storeView.history = storeView.history.filter(visit => visit.Url !== url);
    send({ type: 'browser', action: 'historyRemove', url });
    renderPanel();
  } else if ((element = closest('[data-history-url]'))) {
    openAddress(element.dataset.historyUrl, wantsNewTab(event), storeView.profile);
  } else if (closest('[data-history-clear]')) {
    confirmClearHistory();
  } else if ((element = closest('[data-library-tab]'))) {
    setPanel(element.dataset.libraryTab);
  } else if (closest('[data-store-panel-close]')) {
    setPanel(null);
  } else if ((element = closest('[data-store-panel]'))) {
    togglePanel(element.dataset.storePanel);
  } else if ((element = closest('[data-download]'))) {
    const row = element.closest('[data-download-id]');
    if (row) send({ type: 'browser', action: 'download', command: element.dataset.download, id: row.dataset.downloadId });
  } else if (closest('[data-download-clear]')) {
    send({ type: 'browser', action: 'download', command: 'clear' });
  } else if (closest('[data-store-pick-folder]')) {
    send({ type: 'browser', action: 'pickDownloadFolder' });
  } else if ((element = closest('[data-browser-zoom]'))) {
    send({ type: 'browser', action: 'zoom', delta: Number(element.dataset.browserZoom) });
  } else if (closest('[data-store-settings]')) {
    if (VIEWS.settings) setView('settings');
  } else if ((element = closest('[data-browser]'))) {
    if (element.dataset.browser === 'newTab') send({ type: 'browser', action: 'newTab' });
    else send({ type: 'browser', action: element.dataset.browser });
  }
});

elements.results.addEventListener('dblclick', event => {
  if (state.view === 'stores' && event.target.matches('[data-browser-strip]')) send({ type: 'browser', action: 'newTab' });
});

elements.results.addEventListener('change', event => {
  if (state.view !== 'stores') return;
  if (event.target.matches('[data-bookmark-bar-toggle]')) {
    setSetting('store.showBookmarksBar', event.target.checked);
    renderPart('[data-bookmark-bar]', bookmarkBarHtml());
  }
});

elements.results.addEventListener('input', event => {
  if (state.view !== 'stores') return;
  if (event.target.matches('[data-history-search]')) {
    storeView.historyQuery = event.target.value;
    renderPart('[data-history-list]', historyListHtml());
  }
});

elements.results.addEventListener('submit', event => {
  if (state.view !== 'stores') return;
  event.preventDefault();
  const form = event.target;
  if (form.matches('[data-browser-address]')) {
    const value = form.elements.address.value.trim();
    if (value) {
      if (storeView.tabs.length) send({ type: 'browser', action: 'navigate', url: value });
      else openAddress(value, true);
    }
    form.elements.address.blur();
  } else if (form.matches('[data-store-add]')) {
    const url = form.elements.url.value.trim();
    if (!url) {
      form.elements.url.focus();
      return;
    }
    addStore(form.elements.name.value, url, form.elements.profile.checked);
  }
});

elements.results.addEventListener('keydown', event => {
  if (state.view !== 'stores' || event.key !== 'Escape') return;
  const input = event.target.closest('input[name="address"]');
  if (input) {
    input.value = storeView.blank ? '' : storeView.url;
    input.blur();
    event.stopPropagation();
  } else if (event.target.closest('[data-store-add]')) {
    storeView.adding = false;
    renderPart('[data-store-tabs]', storeTabsHtml());
    event.stopPropagation();
  } else if (event.target.closest('[data-history-search]') && storeView.historyQuery) {
    storeView.historyQuery = '';
    event.target.value = '';
    renderPart('[data-history-list]', historyListHtml());
    event.stopPropagation();
  }
});

document.addEventListener('load', event => {
  const image = event.target;
  if (image.classList && image.classList.contains('store-fav') && image.parentElement) image.parentElement.classList.add('loaded');
}, true);

function confirmClearHistory() {
  modal.open(`
    <header class="modal-head"><h2>Clear browsing history?</h2><button class="icon-button large" data-modal-close title="Cancel">${ICON.close}</button></header>
    <div class="modal-body"><p class="store-modal-text">This removes the ${storeView.history.length} page${storeView.history.length === 1 ? '' : 's'} in the store browser’s history. Bookmarks, logins and downloads stay.</p></div>
    <footer class="modal-foot"><span class="grow"></span><button class="secondary-button compact" data-modal-close>Cancel</button><button class="primary-button compact" data-history-clear-confirm>Clear history</button></footer>`);
  modal.panel.querySelector('[data-history-clear-confirm]').addEventListener('click', () => {
    storeView.history = [];
    send({ type: 'browser', action: 'clearHistory' });
    modal.close();
    renderPanel();
  });
}

extensions.keyHandlers.push(event => {
  if (state.view !== 'stores' || document.body.classList.contains('home-active')) return false;
  const control = event.ctrlKey || event.metaKey;
  const key = event.key.toLowerCase();
  const inField = event.target.tagName === 'INPUT' || event.target.tagName === 'TEXTAREA';
  const run = message => {
    event.preventDefault();
    send(Object.assign({ type: 'browser' }, message));
    return true;
  };

  if (control && !event.altKey) {
    if (key === 't') return run(event.shiftKey ? { action: 'reopen' } : { action: 'newTab' });
    if (key === 'w') return run({ action: 'closeTab', id: storeView.activeId });
    if (key === 'tab' || key === 'pagedown' || key === 'pageup') return run({ action: 'cycle', delta: event.shiftKey || key === 'pageup' ? -1 : 1 });
    if (key === 'l') {
      event.preventDefault();
      focusAddress();
      return true;
    }
    if (key === 'd') {
      event.preventDefault();
      toggleBookmark();
      return true;
    }
    if (key === 'h') {
      event.preventDefault();
      setPanel(storeView.panel === 'history' ? null : 'history');
      return true;
    }
    if (key === 'j') {
      event.preventDefault();
      togglePanel('downloads');
      return true;
    }
    if (key === 'r' && !inField) return run({ action: 'reload' });
    if ((key === '=' || key === '+') && !inField) return run({ action: 'zoom', delta: 1 });
    if (key === '-' && !inField) return run({ action: 'zoom', delta: -1 });
    if (key === '0' && !inField) return run({ action: 'zoom', delta: 0 });
    if (/^[1-9]$/.test(key) && !inField) {
      const index = key === '9' ? storeView.tabs.length - 1 : Number(key) - 1;
      const tab = storeView.tabs[index];
      if (tab) return run({ action: 'activate', id: tab.id });
      event.preventDefault();
      return true;
    }
  }
  if (event.altKey && !control && !inField) {
    if (event.key === 'ArrowLeft') return run({ action: 'back' });
    if (event.key === 'ArrowRight') return run({ action: 'forward' });
  }
  if (event.altKey && !control && key === 'd') {
    event.preventDefault();
    focusAddress();
    return true;
  }
  if (event.key === 'F5' && !control) return run({ action: 'reload' });
  if (event.key === 'F6') {
    event.preventDefault();
    focusAddress();
    return true;
  }
  return false;
});

function storeSettingsHtml() {
  const allowed = (setting('store.adAllowSites', []) || []).filter(Boolean);
  const filterStatus = storeView.filters.status || '';
  const prompt = setting('store.downloadPrompt', 'risky');
  const switchRow = (key, fallback, label, note) => `
    <label class="store-setting">
      <span class="store-setting-text"><strong>${escapeHtml(label)}</strong>${note ? `<span>${escapeHtml(note)}</span>` : ''}</span>
      <input type="checkbox" data-store-setting="${key}" ${setting(key, fallback) ? 'checked' : ''}><span class="store-switch"></span>
    </label>`;

  return `
    <div class="store-settings">
      ${switchRow('store.blockAds', true, 'Block ads, trackers and pop-ups', `${storeView.filters.builtIn || 'Several hundred'} known ad and tracker domains. Official store sites are never blocked.`)}
      <div class="store-setting column">
        <span class="store-setting-text"><strong>Extra filter list</strong><span>A hosts file or a list of ||domain^ rules. Refreshed every 3 days.</span></span>
        <form class="store-setting-inline" data-store-filter-form>
          <input type="text" name="filterUrl" value="${escapeHtml(setting('store.filterListUrl', '') || '')}" placeholder="https://example.com/hosts.txt" autocomplete="off" spellcheck="false">
          <button class="text-button" type="submit">Save</button>
          ${setting('store.filterListUrl', '') ? '<button class="text-button" type="button" data-store-filter-refresh>Update now</button>' : ''}
        </form>
        ${filterStatus ? `<span class="store-setting-status">${escapeHtml(filterStatus)}</span>` : ''}
      </div>
      ${allowed.length ? `<div class="store-setting column">
        <span class="store-setting-text"><strong>Sites allowed to show ads</strong></span>
        <div class="store-setting-chips">${allowed.map(site => `<span class="chip removable">${escapeHtml(site)}<button type="button" data-store-unallow="${escapeHtml(site)}" title="Block ads on ${escapeHtml(site)} again">${ICON.close}</button></span>`).join('')}</div>
      </div>` : ''}
      <div class="store-setting">
        <span class="store-setting-text"><strong>Download folder</strong><span class="mono">${escapeHtml(storeView.downloadFolder || 'Downloads')}</span></span>
        <span class="store-setting-actions">
          <button class="text-button" type="button" data-store-setting-folder>Change</button>
          ${setting('store.downloadFolder', '') ? '<button class="text-button" type="button" data-store-setting-folder-reset>Use Downloads</button>' : ''}
        </span>
      </div>
      <label class="store-setting">
        <span class="store-setting-text"><strong>Ask where to save</strong><span>Programs and archives from unofficial sites always ask first.</span></span>
        <select data-store-setting-select="store.downloadPrompt">
          <option value="risky" ${prompt !== 'always' ? 'selected' : ''}>Only for risky files</option>
          <option value="always" ${prompt === 'always' ? 'selected' : ''}>For every download</option>
        </select>
      </label>
      ${switchRow('store.scanDownloads', true, 'Scan downloads with Microsoft Defender', storeView.scanner ? 'Runs a quick scan of each finished file.' : 'Microsoft Defender isn’t available on this PC.')}
      ${switchRow('store.showBookmarksBar', true, 'Show bookmarks bar', '')}
      <div class="store-setting">
        <span class="store-setting-text"><strong>Browsing history</strong><span>${storeView.history.length ? `${storeView.history.length} pages` : 'Up to 500 recent pages'}</span></span>
        <button class="text-button" type="button" data-store-setting-clear-history>Clear history</button>
      </div>
    </div>`;
}

function bindStoreSettings(root) {
  send({ type: 'browser', action: 'history' });
  root.addEventListener('change', event => {
    const toggle = event.target.closest('[data-store-setting]');
    const select = event.target.closest('[data-store-setting-select]');
    if (toggle) setSetting(toggle.dataset.storeSetting, toggle.checked);
    if (select) setSetting(select.dataset.storeSettingSelect, select.value);
  });
  root.addEventListener('submit', event => {
    if (!event.target.matches('[data-store-filter-form]')) return;
    event.preventDefault();
    const value = event.target.elements.filterUrl.value.trim();
    if (value && !/^https?:\/\//i.test(value)) {
      toast('Enter a full web address, starting with https://', 'error');
      return;
    }
    setSetting('store.filterListUrl', value || null);
    toast(value ? 'Downloading the filter list…' : 'Extra filter list removed', 'info');
  });
  root.addEventListener('click', event => {
    const unallow = event.target.closest('[data-store-unallow]');
    if (unallow) {
      const site = unallow.dataset.storeUnallow;
      const next = (setting('store.adAllowSites', []) || []).filter(entry => entry !== site);
      setSetting('store.adAllowSites', next.length ? next : null);
      refreshStoreSettings(root);
    } else if (event.target.closest('[data-store-filter-refresh]')) {
      send({ type: 'browser', action: 'refreshFilters' });
      toast('Updating the filter list…', 'info');
    } else if (event.target.closest('[data-store-setting-folder]')) {
      send({ type: 'browser', action: 'pickDownloadFolder' });
    } else if (event.target.closest('[data-store-setting-folder-reset]')) {
      setSetting('store.downloadFolder', null);
      send({ type: 'browser', action: 'download', command: 'list' });
    } else if (event.target.closest('[data-store-setting-clear-history]')) {
      confirmClearHistory();
    }
  });
}

function refreshStoreSettings(root) {
  const container = root || document.querySelector('[data-store-settings-root]');
  if (!container) return;
  const focused = document.activeElement && container.contains(document.activeElement) && document.activeElement.tagName === 'INPUT' && document.activeElement.type === 'text';
  if (focused) return;
  container.innerHTML = storeSettingsHtml();
}

extensions.settingsSections.push({
  id: 'store',
  title: 'Store browser',
  render: () => `<div data-store-settings-root>${storeSettingsHtml()}</div>`,
  bind: root => bindStoreSettings(root),
});

extensions.views.stores = { render: renderStoresView };

extensions.afterRender.push(() => {
  updateNavCount();
  if (state.view !== 'stores') scheduleBrowserSync();
});

extensions.messageHandlers.push(message => {
  switch (message.type) {
    case 'browserState':
      applyBrowserState(message);
      refreshStoreSettings();
      break;
    case 'browserSnapshot':
      showSnapshot(message);
      break;
    case 'browserHistory':
      storeView.history = message.items || [];
      if (state.view === 'stores') {
        if (storeView.panel === 'history') renderPanel();
        if (storeView.blank) renderPart('[data-frame-fill]', frameFillHtml());
      }
      refreshStoreSettings();
      break;
    case 'browserCommand':
      if (state.view !== 'stores') break;
      if (message.command === 'focusAddress') focusAddress();
      else if (message.command === 'bookmark') toggleBookmark();
      else if (message.command === 'history') setPanel(storeView.panel === 'history' ? null : 'history');
      else if (message.command === 'downloads') togglePanel('downloads');
      break;
    case 'downloads':
      applyDownloads(message);
      refreshStoreSettings();
      break;
    case 'downloadConfirm':
      storeView.confirmQueue.push(message);
      confirmNextDownload();
      break;
    case 'settings':
      if (state.view === 'stores') renderStoreChrome();
      refreshStoreSettings();
      break;
  }
});

new MutationObserver(scheduleBrowserSync).observe(document.body, {
  subtree: true,
  childList: true,
  attributes: true,
  attributeFilter: ['class', 'style', 'hidden', 'data-covers-browser'],
});
document.querySelector('.app').addEventListener('transitionend', event => {
  if (event.propertyName === 'grid-template-columns') scheduleBrowserSync();
});
window.addEventListener('resize', scheduleBrowserSync);

(function watchPixelRatio() {
  const query = matchMedia(`(resolution: ${window.devicePixelRatio}dppx)`);
  const changed = () => {
    query.removeEventListener('change', changed);
    scheduleBrowserSync();
    watchPixelRatio();
  };
  query.addEventListener('change', changed);
})();

window.addEventListener('DOMContentLoaded', () => {
  send({ type: 'browser', action: 'state' });
  send({ type: 'browser', action: 'download', command: 'list' });
});
