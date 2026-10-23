'use strict';

ICON.gift = '<svg viewBox="0 0 24 24" class="glyph"><rect x="3.5" y="8" width="17" height="4.5" rx="1.2"/><path d="M5 12.5v6a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-6M12 8v12.5"/><path d="M12 8C10.5 4.5 7 4.2 7 6.2 7 7.6 9 8 12 8zm0 0c1.5-3.5 5-3.8 5-1.8C17 7.6 15 8 12 8z"/></svg>';
ICON.timer = '<svg viewBox="0 0 24 24" class="glyph"><circle cx="12" cy="13" r="7.5"/><path d="M12 9v4l2.5 1.5M9.5 2.5h5"/></svg>';
ICON.calendar = '<svg viewBox="0 0 24 24" class="glyph"><rect x="3.5" y="5" width="17" height="15.5" rx="2.5"/><path d="M3.5 10h17M8 3v4M16 3v4"/></svg>';
ICON.claimed = '<svg viewBox="0 0 24 24" class="glyph"><path d="M20 12a8 8 0 1 1-8-8"/><path d="m8.5 11.5 3 3L21 5"/></svg>';
ICON.eyeOff = '<svg viewBox="0 0 24 24" class="glyph"><path d="M10.6 5.1A10.4 10.4 0 0 1 12 5c5 0 8.5 4.5 9.5 7a13 13 0 0 1-2.6 3.7M6.6 6.6C4.5 8 3.1 10.2 2.5 12c1 2.5 4.5 7 9.5 7 1.9 0 3.6-.6 5-1.6M3.5 3.5l17 17"/><path d="M9.9 9.9a3 3 0 0 0 4.2 4.2"/></svg>';

const free = {
  snapshot: null,
  loading: false,
  pendingUrl: null,
};

const STORE_LABELS = { steam: 'Steam', epic: 'Epic Games', gog: 'GOG', ubisoft: 'Ubisoft', itch: 'itch.io' };
const DEAL_HOSTS = /^https:\/\/([a-z0-9-]+\.)*(steampowered\.com|steamcommunity\.com|epicgames\.com|gog\.com|itch\.io|ubisoft\.com|isthereanydeal\.com|itad\.link)(\/|$)/i;

function freeOffers() {
  return (free.snapshot && free.snapshot.Offers) || [];
}

function freeNameKey(text) {
  return normalize(text).replace(/[^\p{L}\p{N}]/gu, '');
}

function ownedGame(offer) {
  const key = freeNameKey(offer.Title);
  return state.games.find(game =>
    (offer.SteamAppId && game.SteamAppId === offer.SteamAppId) || (key && freeNameKey(game.Name) === key));
}

function claimedOffers() {
  const claimed = setting('deals.claimed', {});
  return claimed && typeof claimed === 'object' ? claimed : {};
}

function isClaimed(offer) {
  return Object.prototype.hasOwnProperty.call(claimedOffers(), offer.Key);
}

function toggleClaimed(key) {
  const claimed = Object.assign({}, claimedOffers());
  if (claimed[key]) delete claimed[key];
  else claimed[key] = new Date().toISOString();
  for (const [entry, at] of Object.entries(claimed)) {
    if (Date.now() - Date.parse(at) > 400 * 86400000) delete claimed[entry];
  }
  setSetting('deals.claimed', claimed);
}

function dealStoreIcon(key) {
  if (state.platformIcons[key]) return `<img src="${escapeHtml(state.platformIcons[key])}" alt="">`;
  if (typeof OFFICIAL_STORES !== 'undefined' && typeof storeIcon === 'function') {
    const store = OFFICIAL_STORES.find(entry => entry.key === key);
    if (store) return storeIcon(store);
  }
  return ICON.gift;
}

function storeKeyForUrl(url) {
  const host = (url.match(/^https:\/\/([^/]+)/i) || [])[1] || '';
  if (/steampowered\.com|steamcommunity\.com$/i.test(host)) return 'steam';
  if (/epicgames\.com$/i.test(host)) return 'epic';
  if (/gog\.com$/i.test(host)) return 'gog';
  if (/itch\.io$/i.test(host)) return 'itch';
  if (/ubisoft\.com$/i.test(host)) return 'ubisoft';
  return null;
}

function openDealUrl(url, external = false) {
  if (!url) return;
  const inApp = !external && DEAL_HOSTS.test(url) && typeof storeView !== 'undefined';
  if (!inApp) {
    send({ type: 'dealsOpen', url });
    return;
  }
  const key = storeKeyForUrl(url);
  if (key && typeof OFFICIAL_STORES !== 'undefined' && OFFICIAL_STORES.some(store => store.key === key)) {
    state.prefs.storeKey = key;
    savePrefs();
  }
  if (storeView.started) {
    send({ type: 'browser', action: 'navigate', url });
  } else {
    free.pendingUrl = url;
  }
  setView('stores');
}

function countdown(iso) {
  const milliseconds = Date.parse(iso) - Date.now();
  if (!(milliseconds > 0)) return null;
  const minutes = Math.floor(milliseconds / 60000);
  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);
  if (days >= 1) return `${days} d ${hours} h`;
  if (hours >= 1) return `${hours} h ${minutes % 60} m`;
  return `${Math.max(1, minutes)} min`;
}

function longDate(iso) {
  if (!iso) return '';
  return new Date(iso).toLocaleString('en-GB', { weekday: 'short', day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' });
}

function epicImage(url) {
  if (!url || !/epicgames\.com/i.test(url)) return url;
  return url + (url.includes('?') ? '&' : '?') + 'w=860&h=484&resize=1&quality=medium';
}

function claimAction(offer) {
  const store = STORE_LABELS[offer.Store] || offer.Store;
  switch (offer.Store) {
    case 'epic': return { url: offer.StoreUrl, label: 'Claim on Epic' };
    case 'steam': return { url: offer.ClientUrl || offer.StoreUrl, label: 'Get it on Steam' };
    case 'gog': return { url: offer.ClientUrl || offer.StoreUrl, label: 'Claim on GOG' };
    default: return { url: offer.StoreUrl, label: `Get it on ${store}` };
  }
}

function offerCard(offer, index) {
  const owned = ownedGame(offer);
  const claimed = isClaimed(offer);
  const store = STORE_LABELS[offer.Store] || offer.Store;
  let pill = '';
  let when = '';
  let primary = '';
  let claimButton = '';

  if (offer.Section === 'now') {
    const left = countdown(offer.EndsAt);
    const action = claimAction(offer);
    pill = left ? `<span class="deal-pill urgent" data-ends="${escapeHtml(offer.EndsAt)}">${ICON.timer}<span>Ends in ${left}</span></span>` : '';
    when = offer.EndsAt ? `Free to keep until ${escapeHtml(longDate(offer.EndsAt))}` : 'Free to keep for a limited time';
    primary = claimed
      ? `<button class="secondary-button compact" data-open="${escapeHtml(offer.StoreUrl)}">${ICON.external}View on ${escapeHtml(store)}</button>`
      : `<button class="primary-button compact" data-open="${escapeHtml(action.url)}">${ICON.gift}${escapeHtml(action.label)}</button>`;
    claimButton = `<button class="secondary-button compact deal-claim ${claimed ? 'on' : ''}" data-claim="${escapeHtml(offer.Key)}" title="${claimed ? 'Mark as not claimed' : 'Mark as claimed'}">${ICON.claimed}${claimed ? 'Claimed' : 'Mark claimed'}</button>`;
  } else if (offer.Section === 'upcoming') {
    const starts = countdown(offer.StartsAt);
    pill = starts ? `<span class="deal-pill" data-starts="${escapeHtml(offer.StartsAt)}">${ICON.calendar}<span>Free in ${starts}</span></span>` : '';
    when = `Free from ${escapeHtml(longDate(offer.StartsAt))} until ${escapeHtml(longDate(offer.EndsAt))}`;
    primary = `<button class="secondary-button compact" data-open="${escapeHtml(offer.StoreUrl)}">${ICON.external}View on Epic</button>`;
  } else if (offer.Section === 'weekend') {
    const left = countdown(offer.EndsAt);
    pill = left ? `<span class="deal-pill" data-ends="${escapeHtml(offer.EndsAt)}">${ICON.timer}<span>Ends in ${left}</span></span>` : '';
    when = offer.EndsAt ? `Play free until ${escapeHtml(longDate(offer.EndsAt))}` : 'Play free for a limited time';
    primary = `<button class="primary-button compact" data-open="${escapeHtml(offer.ClientUrl || offer.StoreUrl)}">${ICON.play || ICON.gift}Play free on ${escapeHtml(store)}</button>`;
  } else {
    when = [offer.ReleasedAt ? `Released ${escapeHtml(formatDate(offer.ReleasedAt))}` : '', offer.Reviews ? escapeHtml(offer.Reviews) : ''].filter(Boolean).join(' · ');
    primary = `<button class="secondary-button compact" data-open="${escapeHtml(offer.ClientUrl || offer.StoreUrl)}">${ICON.external}View on Steam</button>`;
  }

  const price = offer.OriginalPrice && offer.Section !== 'freeToPlay' && !/^\D*0([.,]00)?\D*$/.test(offer.OriginalPrice)
    ? `<s>${escapeHtml(offer.OriginalPrice)}</s>`
    : '';
  const freeLabel = { freeToPlay: 'Free to play', upcoming: 'Free soon', weekend: 'Free weekend' }[offer.Section] || 'Free';

  return `
    <article class="deal deal-${offer.Section} ${claimed ? 'claimed' : ''}" data-open="${escapeHtml(offer.StoreUrl)}" style="animation-delay:${Math.min(index, 16) * 30}ms">
      <div class="deal-media ${offer.ImageFit === 'contain' ? 'contain' : ''}">
        ${offer.Image ? `<img src="${escapeHtml(epicImage(offer.Image))}" loading="lazy" decoding="async" alt="">` : ''}
        <span class="deal-store">${dealStoreIcon(offer.Store)}${escapeHtml(store)}</span>
        ${pill}
      </div>
      <div class="deal-body">
        <h3 title="${escapeHtml(offer.Title)}">${escapeHtml(offer.Title)}</h3>
        <div class="deal-price">${price}<span class="deal-free">${freeLabel}</span>${owned ? `<span class="deal-owned">${ICON.check}In your library</span>` : claimed ? `<span class="deal-owned claimed">${ICON.claimed}Claimed</span>` : ''}</div>
        <div class="deal-when">${when}</div>
        <div class="deal-actions">${primary}${claimButton}</div>
      </div>
    </article>`;
}

function freeSection(title, offers, emptyText, className = '', hiddenCount = 0) {
  if (!offers.length && emptyText == null) return '';
  const hiddenNote = hiddenCount ? `<span class="deal-hidden-note">${hiddenCount} hidden</span>` : '';
  const body = offers.length
    ? `<div class="deal-grid ${className}">${offers.map(offerCard).join('')}</div>`
    : `<div class="deal-empty">${emptyText}</div>`;
  return `
    <section class="shelf">
      <h2 class="shelf-title">${title}${offers.length ? ` <span class="count">${offers.length}</span>` : ''}${hiddenNote}</h2>
      ${body}
    </section>`;
}

function hideOwnedAndClaimed() {
  return !!setting('deals.hideOwned', false);
}

function renderFreeView() {
  const query = normalize(state.search.trim());
  const hiding = hideOwnedAndClaimed();
  const matching = freeOffers().filter(offer => !query || normalize(offer.Title).includes(query));
  const hidden = new Set(hiding ? matching.filter(offer => ownedGame(offer) || isClaimed(offer)).map(offer => offer.Key) : []);
  const pick = predicate => {
    const all = matching.filter(predicate);
    return { shown: all.filter(offer => !hidden.has(offer.Key)), hidden: all.filter(offer => hidden.has(offer.Key)).length };
  };

  const now = pick(offer => offer.Section === 'now' && offer.Store !== 'itch');
  now.shown.sort((first, second) => (first.EndsAt || '9').localeCompare(second.EndsAt || '9'));
  const upcoming = pick(offer => offer.Section === 'upcoming');
  const weekend = pick(offer => offer.Section === 'weekend');
  const itch = pick(offer => offer.Section === 'now' && offer.Store === 'itch');
  const freeToPlay = pick(offer => offer.Section === 'freeToPlay');

  elements.pageTitle.textContent = query ? `Free games matching “${state.search.trim()}”` : 'Free games';
  elements.zoomControl.classList.add('hidden');

  if (!free.snapshot) {
    elements.summary.innerHTML = 'Checking Steam, Epic Games, GOG, Ubisoft and itch.io…';
    elements.filters.innerHTML = '';
    elements.results.innerHTML = `<div class="free-view"><div class="deal-grid">${Array.from({ length: 3 }, () => '<article class="deal skeleton"><div class="deal-media"></div><div class="deal-body"><span class="measuring"></span></div></article>').join('')}</div></div>`;
    return;
  }

  const updated = relativeTime(free.snapshot.UpdatedAt);
  const unclaimed = now.shown.filter(offer => !isClaimed(offer)).length;
  elements.summary.innerHTML = `<strong>${now.shown.length}</strong> free to keep right now${unclaimed && unclaimed !== now.shown.length ? ` · <strong>${unclaimed}</strong> not claimed yet` : ''}${upcoming.shown.length ? ` · <strong>${upcoming.shown.length}</strong> coming next` : ''} · checked ${escapeHtml(updated ? updated.charAt(0).toLowerCase() + updated.slice(1) : 'just now')}`;
  elements.filters.innerHTML = `
    <button class="chip ${free.loading ? 'active' : ''}" data-free-refresh>${ICON.refresh}${free.loading ? 'Checking…' : 'Check again'}</button>
    <button class="chip ${hiding ? 'active' : ''}" data-free-hide title="Hide games that are already in your library or that you marked as claimed">${ICON.eyeOff}Hide owned &amp; claimed</button>`;

  const errors = (free.snapshot.Errors || []).length
    ? `<div class="callout">${ICON.alert}<div>Some stores couldn't be reached: ${free.snapshot.Errors.map(escapeHtml).join(' · ')}</div></div>`
    : '';

  elements.results.innerHTML = `
    <div class="free-view">
      ${errors}
      ${freeSection('Free to keep now', now.shown, now.hidden ? 'Everything free right now is already yours.' : 'Nothing is free to keep on Steam, Epic, GOG or Ubisoft right now.', 'large', now.hidden)}
      ${freeSection('Coming next on Epic', upcoming.shown, 'Epic hasn’t announced the next free games yet.', '', upcoming.hidden)}
      ${freeSection('Free weekends', weekend.shown, null, '', weekend.hidden)}
      ${freeSection('100% off on itch.io', itch.shown, null, 'compact', itch.hidden)}
      ${freeSection('Popular new free-to-play on Steam', freeToPlay.shown, 'No new free-to-play releases found.', 'compact', freeToPlay.hidden)}
    </div>`;
}

function refreshCountdowns() {
  if (state.view !== 'free') return;
  for (const element of elements.results.querySelectorAll('[data-ends]')) {
    const left = countdown(element.dataset.ends);
    element.querySelector('span').textContent = left ? `Ends in ${left}` : 'Ended';
  }
  for (const element of elements.results.querySelectorAll('[data-starts]')) {
    const starts = countdown(element.dataset.starts);
    element.querySelector('span').textContent = starts ? `Free in ${starts}` : 'Free now';
  }
}

setInterval(refreshCountdowns, 30 * 1000);
setInterval(() => send({ type: 'freeGames', force: false }), 30 * 60 * 1000);

elements.results.addEventListener('click', event => {
  if (state.view !== 'free') return;
  const claim = event.target.closest('[data-claim]');
  if (claim) {
    event.stopPropagation();
    toggleClaimed(claim.dataset.claim);
    renderFreeView();
    renderSidebar();
    return;
  }
  const target = event.target.closest('[data-open]');
  if (!target) return;
  event.stopPropagation();
  openDealUrl(target.dataset.open, event.shiftKey);
});

elements.filters.addEventListener('click', event => {
  if (state.view !== 'free') return;
  if (event.target.closest('[data-free-hide]')) {
    setSetting('deals.hideOwned', !hideOwnedAndClaimed());
    renderFreeView();
    return;
  }
  if (!event.target.closest('[data-free-refresh]') || free.loading) return;
  free.loading = true;
  renderFreeView();
  send({ type: 'freeGames', force: true });
});

extensions.views.free = { render: renderFreeView };

extensions.sidebarSections.push(() => {
  const count = freeOffers().filter(offer => offer.Section === 'now' && offer.Store !== 'itch' && !isClaimed(offer) && !ownedGame(offer)).length;
  return `
    <div class="nav-section">
      <div class="nav-heading">Store</div>
      <button class="nav-item ${state.view === 'free' ? 'active' : ''}" data-view="free">
        <span class="nav-icon">${ICON.gift}</span>
        <span class="nav-label">Free games</span>
        ${count ? `<span class="nav-badge" title="${count} free to keep right now">${count}</span>` : '<span class="nav-count"></span>'}
      </button>
      ${typeof dealsNavItem === 'function' ? dealsNavItem() : ''}
      ${typeof storeNavItem === 'function' ? storeNavItem() : ''}
    </div>`;
});

extensions.messageHandlers.push(message => {
  if (message.type === 'freeGamesLoading') {
    free.loading = true;
  } else if (message.type === 'freeGames') {
    free.loading = false;
    free.snapshot = message.snapshot;
    renderSidebar();
    if (state.view === 'free') renderFreeView();
  } else if (message.type === 'browserState' && free.pendingUrl) {
    const url = free.pendingUrl;
    free.pendingUrl = null;
    send({ type: 'browser', action: 'navigate', url });
  } else if (message.type === 'dealsShow' && extensions.views[message.view]) {
    setView(message.view);
  }
});
