'use strict';

ICON.tag = '<svg viewBox="0 0 24 24" class="glyph"><path d="M3.5 12.6V4.5a1 1 0 0 1 1-1h8.1a1 1 0 0 1 .7.3l7.4 7.4a1 1 0 0 1 0 1.4l-8.1 8.1a1 1 0 0 1-1.4 0l-7.4-7.4a1 1 0 0 1-.3-.7z"/><circle cx="8.5" cy="8.5" r="1.6"/></svg>';
ICON.trendDown = '<svg viewBox="0 0 24 24" class="glyph"><path d="m3.5 7 6.5 6.5 4-4 6.5 6.5"/><path d="M20.5 11v5h-5"/></svg>';
ICON.bell = '<svg viewBox="0 0 24 24" class="glyph"><path d="M6 16.5V11a6 6 0 1 1 12 0v5.5l1.5 2h-15z"/><path d="M10 20.5a2 2 0 0 0 4 0"/></svg>';

VIEWS.deals = 'Wishlist & deals';

const DEAL_SORTS = [
  { key: 'discount', label: 'Discount' },
  { key: 'price', label: 'Price' },
  { key: 'name', label: 'Name' },
  { key: 'rank', label: 'Wishlist order' },
  { key: 'added', label: 'Recently added' },
];

const SALE_RULES = [
  { key: 'any', label: 'Any discount' },
  { key: 'nearLow', label: 'Within 10% of the historical low' },
  { key: 'lowest', label: 'Only at the historical low' },
];

const deals = {
  data: null,
  accounts: [],
  loading: false,
  requested: false,
  search: { query: '', requestId: null, loading: false, result: null },
  searchTimer: null,
};

function money(cents, currency) {
  if (cents == null) return '';
  if (cents === 0) return 'Free';
  try {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency: currency || 'USD' }).format(cents / 100);
  } catch {
    return (cents / 100).toFixed(2);
  }
}

function dealTargets() {
  const targets = setting('deals.targets', {});
  return targets && typeof targets === 'object' ? targets : {};
}

function targetFor(appId) {
  const value = dealTargets()[appId];
  return typeof value === 'number' && value > 0 ? value : null;
}

function wishItems() {
  return (deals.data && deals.data.Items) || [];
}

function lowOf(item) {
  if (item.Low) return { cents: item.Low.Cents, currency: item.Low.Currency, shop: item.Low.Shop, date: item.Low.Date, url: item.Low.Url, source: 'itad' };
  if (item.SeenLowCents != null) return { cents: item.SeenLowCents, currency: deals.data && deals.data.Currency, source: 'seen' };
  return null;
}

function targetHit(item) {
  const target = targetFor(item.AppId);
  return target != null && item.PriceCents != null && item.PriceCents <= target;
}

function requestWishlist(force = false) {
  deals.requested = true;
  deals.loading = true;
  send({ type: 'dealsWishlist', force });
}

function dealsNavItem() {
  const sale = wishItems().filter(item => item.Discount > 0);
  const hot = sale.filter(item => item.IsLowest || targetHit(item)).length;
  const badge = hot
    ? `<span class="nav-badge" title="${hot} at their lowest price or below your target">${hot}</span>`
    : `<span class="nav-count">${sale.length || ''}</span>`;
  return `
    <button class="nav-item ${state.view === 'deals' ? 'active' : ''}" data-view="deals" title="Wishlist & deals">
      <span class="nav-icon">${ICON.tag}</span>
      <span class="nav-label">Wishlist &amp; deals</span>
      ${badge}
    </button>`;
}

function sortedWishItems() {
  const query = normalize(state.search.trim());
  const sort = state.prefs.dealsSort || 'discount';
  let list = wishItems().filter(item => !query || normalize(item.Name).includes(query));
  if (state.prefs.dealsOnSale) list = list.filter(item => item.Discount > 0 || targetHit(item));

  const name = (first, second) => first.Name.localeCompare(second.Name, 'en', { sensitivity: 'base' });
  const price = item => item.PriceCents == null ? Number.MAX_SAFE_INTEGER : item.PriceCents;
  const comparers = {
    discount: (first, second) => second.Discount - first.Discount || Number(second.IsLowest) - Number(first.IsLowest) || price(first) - price(second) || name(first, second),
    price: (first, second) => price(first) - price(second) || name(first, second),
    name,
    rank: (first, second) => (first.Priority || 9999) - (second.Priority || 9999) || name(first, second),
    added: (first, second) => (second.AddedAt || '').localeCompare(first.AddedAt || ''),
  };
  return list.slice().sort(comparers[sort] || comparers.discount);
}

function wishPriceHtml(item, currency) {
  if (item.FreeToPlay && !item.Purchasable) return '<span class="wish-now free">Free to play</span>';
  if (item.PriceCents == null) {
    const label = item.ComingSoon ? (item.ReleaseText || 'Coming soon') : 'Not for sale';
    return `<span class="wish-muted">${escapeHtml(label)}</span>`;
  }
  const ends = item.Discount > 0 ? countdown(item.DiscountEndsAt) : null;
  return `
    <div class="wish-price-line">
      ${item.Discount > 0 ? `<span class="wish-discount">−${item.Discount}%</span>` : ''}
      <span class="wish-now ${item.Discount > 0 ? 'sale' : ''}">${escapeHtml(money(item.PriceCents, currency))}</span>
      ${item.Discount > 0 && item.OriginalCents ? `<s>${escapeHtml(money(item.OriginalCents, currency))}</s>` : ''}
    </div>
    ${ends ? `<div class="wish-sub">Sale ends in ${escapeHtml(ends)}</div>` : ''}`;
}

function wishLowHtml(item, currency) {
  const low = lowOf(item);
  if (!low) return '<span class="wish-muted">—</span>';
  const when = low.date ? formatDate(low.date) : null;
  const detail = low.source === 'itad'
    ? [low.shop, when].filter(Boolean).join(' · ')
    : 'Lowest seen here';
  const badge = item.IsLowest
    ? `<span class="wish-badge lowest">${ICON.trendDown}Lowest ever</span>`
    : '';
  return `
    <div class="wish-price-line"><span class="wish-low-price">${escapeHtml(money(low.cents, low.currency || currency))}</span>${badge}</div>
    <div class="wish-sub">${escapeHtml(detail)}</div>`;
}

function wishRow(item, index, currency) {
  const target = targetFor(item.AppId);
  const hit = targetHit(item);
  const meta = [
    item.ComingSoon ? 'Unreleased' : item.ReleasedAt ? `Released ${formatDate(item.ReleasedAt)}` : '',
    item.AddedAt ? `Wishlisted ${relativeTime(item.AddedAt).toLowerCase()}` : '',
  ].filter(Boolean).join(' · ');

  return `
    <div class="wish-row ${item.Discount > 0 ? 'on-sale' : ''} ${hit ? 'target-hit' : ''}" data-wish="${escapeHtml(item.AppId)}" style="animation-delay:${Math.min(index, 20) * 18}ms">
      <div class="wish-art"><img src="${escapeHtml(item.Image)}" loading="lazy" decoding="async" alt=""></div>
      <div class="wish-info">
        <div class="wish-name" title="${escapeHtml(item.Name)}">${escapeHtml(item.Name)}</div>
        <div class="wish-sub">${escapeHtml(meta)}</div>
      </div>
      <div class="wish-cell">${wishPriceHtml(item, currency)}</div>
      <div class="wish-cell">${wishLowHtml(item, currency)}</div>
      <label class="wish-target ${hit ? 'hit' : ''}" title="Get a notification when the price drops to this or lower">
        ${ICON.bell}
        <input type="text" inputmode="decimal" data-target="${escapeHtml(item.AppId)}" value="${target != null ? (target / 100).toFixed(2) : ''}" placeholder="Alert at" spellcheck="false" autocomplete="off">
      </label>
    </div>`;
}

function wishCalloutsHtml() {
  const data = deals.data;
  if (!data) return '';
  const parts = [];
  if (!data.SteamId) {
    parts.push(`<div class="callout neutral">${ICON.info}<div>No Steam account was found on this PC. Sign in to Steam once, then refresh this page.</div></div>`);
  } else if (data.Empty) {
    parts.push(`
      <div class="callout neutral">${ICON.info}<div>
        <strong>Your Steam wishlist looks empty.</strong> If you do have wishlisted games, the wishlist is probably private: in Steam, open your profile’s privacy settings and set <strong>Game details</strong> to <strong>Public</strong>, then refresh.
        <div class="deals-callout-actions"><button class="secondary-button compact" data-open-url="https://steamcommunity.com/my/edit/settings">${ICON.external}Open privacy settings</button></div>
      </div></div>`);
  }
  if ((data.Errors || []).length) {
    parts.push(`<div class="callout">${ICON.alert}<div>Some prices couldn't be loaded: ${data.Errors.map(escapeHtml).join(' · ')}</div></div>`);
  }
  if (data.SteamId && !data.Empty && !data.HasItad && !state.prefs.dealsItadHintHidden) {
    parts.push(`
      <div class="callout neutral deals-itad-hint">${ICON.trendDown}<div>
        <strong>See historical lows.</strong> Add a free IsThereAnyDeal API key to compare against the lowest price each game has ever had.
        <form class="deals-key-form" data-itad-form>
          <input name="key" type="text" placeholder="Paste your API key" autocomplete="off" spellcheck="false">
          <button class="secondary-button compact" type="submit">Save key</button>
          <button class="text-button" type="button" data-open-url="https://isthereanydeal.com/apps/my/">Get a key</button>
        </form>
      </div>
      <button class="icon-button" data-itad-hide title="Hide">${ICON.close}</button></div>`);
  }
  return parts.join('');
}

function wishListHtml() {
  if (!deals.data) {
    return `<div class="wish-list">${Array.from({ length: 6 }, () => '<div class="wish-row skeleton"><div class="wish-art"></div><div class="wish-info"><span class="measuring"></span></div></div>').join('')}</div>`;
  }
  const list = sortedWishItems();
  const currency = deals.data.Currency;
  if (!list.length) {
    if (!wishItems().length) return '';
    return `<div class="deal-empty">${state.prefs.dealsOnSale ? 'None of your wishlisted games are on sale right now.' : 'No wishlisted games match your search.'}</div>`;
  }
  return `
    <div class="wish-list">
      <div class="wish-head"><span></span><span>Game</span><span>Price</span><span>${deals.data.HasItad ? 'Historical low' : 'Lowest seen'}</span><span>Price alert</span></div>
      ${list.map((item, index) => wishRow(item, index, currency)).join('')}
    </div>`;
}

function storeName(key, fallback) {
  return (typeof STORE_LABELS !== 'undefined' && STORE_LABELS[key]) || fallback || key;
}

function priceOfferHtml(price, best) {
  const now = price.PriceText || money(price.Cents, price.Currency);
  const was = price.Discount > 0 ? (price.RegularText || money(price.RegularCents, price.Currency)) : '';
  const url = price.Url || '';
  return `
    <button class="price-offer ${best ? 'best' : ''}" data-open-url="${escapeHtml(url)}" ${price.ClientUrl ? `data-client-url="${escapeHtml(price.ClientUrl)}"` : ''} title="Open on ${escapeHtml(price.StoreName)}${price.ClientUrl ? ' · Shift-click to open in the launcher' : ''}">
      <span class="price-store">${dealStoreIcon(price.Store)}<span>${escapeHtml(storeName(price.Store, price.StoreName))}</span></span>
      <span class="price-value">
        <strong>${escapeHtml(price.Free ? 'Free' : now)}</strong>
        ${was ? `<s>${escapeHtml(was)}</s>` : ''}
        ${price.Discount > 0 ? `<span class="wish-discount small">−${price.Discount}%</span>` : ''}
      </span>
    </button>`;
}

function searchResultsHtml() {
  const search = deals.search;
  if (!search.query) return '';
  if (search.loading && !search.result) {
    return `<div class="price-results loading"><span class="measuring"></span>Searching every store…</div>`;
  }
  const result = search.result;
  if (!result) return '';
  const errors = (result.Errors || []).length
    ? `<div class="price-errors">${ICON.alert}<span>${result.Errors.map(escapeHtml).join(' · ')}</span></div>`
    : '';
  if (!(result.Games || []).length) {
    return `<div class="price-results">${errors}<div class="deal-empty">No store has a game called “${escapeHtml(result.Query)}”.</div></div>`;
  }
  const rows = result.Games.map(game => {
    const priced = game.Prices.filter(price => price.Cents != null);
    const bestCents = priced.length ? Math.min(...priced.map(price => price.Cents)) : null;
    const low = game.Low ? `<div class="wish-sub">Historical low ${escapeHtml(money(Math.round(game.Low.Cents), game.Low.Currency))}</div>` : '';
    return `
      <div class="price-row">
        <div class="price-art">${game.Image ? `<img src="${escapeHtml(game.Image)}" loading="lazy" decoding="async" alt="">` : ''}</div>
        <div class="price-title"><div class="wish-name" title="${escapeHtml(game.Title)}">${escapeHtml(game.Title)}</div>${low}</div>
        <div class="price-offers">${game.Prices.map(price => priceOfferHtml(price, priced.length > 1 && price.Cents === bestCents)).join('')}</div>
      </div>`;
  }).join('');
  const note = result.HasItad ? '' : '<div class="price-note">Add an IsThereAnyDeal key in Settings to include more shops and historical lows.</div>';
  return `<div class="price-results ${search.loading ? 'refreshing' : ''}">${errors}${rows}${note}</div>`;
}

function dealsFiltersHtml() {
  const sort = state.prefs.dealsSort || 'discount';
  const accounts = deals.accounts || [];
  const chosen = setting('deals.steamId', '') || '';
  const accountPicker = accounts.length > 1
    ? `<label class="deals-account" title="Steam account whose wishlist is shown">${platformIcon('steam')}
         <select data-deals-account>
           <option value="" ${!chosen ? 'selected' : ''}>Signed-in account</option>
           ${accounts.map(account => `<option value="${escapeHtml(account.SteamId)}" ${chosen === account.SteamId ? 'selected' : ''}>${escapeHtml(account.Name)}</option>`).join('')}
         </select></label>`
    : '';
  return `
    <div class="chip-group" data-deals-sort>${DEAL_SORTS.map(entry => `<button class="${entry.key === sort ? 'active' : ''}" data-sort-key="${entry.key}">${entry.label}</button>`).join('')}</div>
    <button class="chip ${state.prefs.dealsOnSale ? 'active' : ''}" data-deals-onsale>${ICON.tag}On sale only</button>
    <button class="chip ${deals.loading ? 'active' : ''}" data-deals-refresh>${ICON.refresh}${deals.loading ? 'Checking…' : 'Check prices'}</button>
    ${accountPicker}`;
}

function dealsSummaryHtml() {
  const data = deals.data;
  if (!data) return 'Loading your Steam wishlist…';
  const items = wishItems();
  if (!items.length) return 'Search every store, or wishlist games on Steam to track their prices.';
  const sale = items.filter(item => item.Discount > 0).length;
  const lowest = items.filter(item => item.IsLowest).length;
  const updated = relativeTime(data.UpdatedAt);
  return `<strong>${items.length}</strong> wishlisted · <strong>${sale}</strong> on sale${lowest ? ` · <strong>${lowest}</strong> at their lowest ever` : ''} · checked ${escapeHtml(updated ? updated.charAt(0).toLowerCase() + updated.slice(1) : 'just now')}`;
}

function renderDealsView() {
  elements.pageTitle.textContent = 'Wishlist & deals';
  elements.zoomControl.classList.add('hidden');
  elements.summary.innerHTML = dealsSummaryHtml();
  elements.filters.innerHTML = dealsFiltersHtml();
  if (!deals.requested) requestWishlist(false);

  const existing = elements.results.querySelector('.deals-view');
  if (existing) {
    existing.querySelector('[data-deals-callouts]').innerHTML = wishCalloutsHtml();
    existing.querySelector('[data-deals-list]').innerHTML = wishListHtml();
    existing.querySelector('[data-deals-search-results]').innerHTML = searchResultsHtml();
    return;
  }

  elements.results.innerHTML = `
    <div class="deals-view">
      <section class="deals-search">
        <form class="deals-search-box" data-deals-search>
          ${ICON.search}
          <input name="query" type="text" value="${escapeHtml(deals.search.query)}" placeholder="Compare prices on Steam, Epic, GOG and more" autocomplete="off" spellcheck="false">
          ${deals.search.query ? `<button class="icon-button" type="button" data-deals-search-clear title="Clear">${ICON.close}</button>` : ''}
        </form>
        <div data-deals-search-results>${searchResultsHtml()}</div>
      </section>
      <div data-deals-callouts>${wishCalloutsHtml()}</div>
      <section class="shelf">
        <h2 class="shelf-title">Steam wishlist${wishItems().length ? ` <span class="count">${wishItems().length}</span>` : ''}</h2>
        <div data-deals-list>${wishListHtml()}</div>
      </section>
    </div>`;
}

function updateDealsParts() {
  if (state.view !== 'deals') return;
  renderDealsView();
  const title = elements.results.querySelector('.deals-view .shelf-title');
  if (title) title.innerHTML = `Steam wishlist${wishItems().length ? ` <span class="count">${wishItems().length}</span>` : ''}`;
}

function runSearch(query) {
  query = query.trim();
  deals.search.query = query;
  if (query.length < 2) {
    deals.search.result = null;
    deals.search.loading = false;
    deals.search.requestId = null;
    const box = elements.results.querySelector('[data-deals-search-results]');
    if (box) box.innerHTML = searchResultsHtml();
    return;
  }
  deals.search.loading = true;
  deals.search.requestId = String(Date.now());
  send({ type: 'dealsSearch', query, requestId: deals.search.requestId });
  const box = elements.results.querySelector('[data-deals-search-results]');
  if (box) box.innerHTML = searchResultsHtml();
}

function saveTarget(input) {
  const appId = input.dataset.target;
  const text = input.value.trim().replace(/[^\d.,]/g, '').replace(',', '.');
  const targets = Object.assign({}, dealTargets());
  const value = parseFloat(text);
  if (!text || !(value > 0)) {
    delete targets[appId];
    input.value = '';
  } else {
    targets[appId] = Math.round(value * 100);
    input.value = value.toFixed(2);
  }
  setSetting('deals.targets', targets);
  const row = input.closest('.wish-row');
  const item = wishItems().find(entry => entry.AppId === appId);
  if (row && item) {
    row.classList.toggle('target-hit', targetHit(item));
    input.closest('.wish-target').classList.toggle('hit', targetHit(item));
  }
  renderSidebar();
}

elements.results.addEventListener('click', event => {
  if (state.view !== 'deals') return;

  const open = event.target.closest('[data-open-url]');
  if (open) {
    event.preventDefault();
    event.stopPropagation();
    const client = open.dataset.clientUrl;
    openDealUrl(event.shiftKey && client ? client : open.dataset.openUrl, event.shiftKey);
    return;
  }
  if (event.target.closest('[data-itad-hide]')) {
    state.prefs.dealsItadHintHidden = true;
    savePrefs();
    updateDealsParts();
    return;
  }
  if (event.target.closest('[data-deals-search-clear]')) {
    const input = elements.results.querySelector('[data-deals-search] input');
    input.value = '';
    runSearch('');
    event.target.closest('[data-deals-search-clear]').remove();
    input.focus();
    return;
  }
  if (event.target.closest('.wish-target')) return;
  const row = event.target.closest('[data-wish]');
  if (row) openDealUrl(event.shiftKey ? `steam://store/${row.dataset.wish}` : `https://store.steampowered.com/app/${row.dataset.wish}`, event.shiftKey);
});

elements.results.addEventListener('submit', event => {
  if (state.view !== 'deals') return;
  event.preventDefault();
  const form = event.target;
  if (form.matches('[data-deals-search]')) {
    clearTimeout(deals.searchTimer);
    runSearch(form.elements.query.value);
  } else if (form.matches('[data-itad-form]')) {
    const key = form.elements.key.value.trim();
    if (!key) {
      form.elements.key.focus();
      return;
    }
    setSetting('deals.itadKey', key);
    deals.loading = true;
    toast('IsThereAnyDeal key saved. Loading historical lows…', 'info', ICON.check);
  }
});

elements.results.addEventListener('input', event => {
  if (state.view !== 'deals') return;
  const input = event.target.closest('[data-deals-search] input');
  if (!input) return;
  const form = input.closest('form');
  if (input.value && !form.querySelector('[data-deals-search-clear]')) {
    form.insertAdjacentHTML('beforeend', `<button class="icon-button" type="button" data-deals-search-clear title="Clear">${ICON.close}</button>`);
  }
  clearTimeout(deals.searchTimer);
  deals.searchTimer = setTimeout(() => runSearch(input.value), 650);
});

elements.results.addEventListener('change', event => {
  if (state.view !== 'deals') return;
  const input = event.target.closest('[data-target]');
  if (input) saveTarget(input);
});

elements.results.addEventListener('keydown', event => {
  if (state.view !== 'deals') return;
  const target = event.target.closest('[data-target]');
  if (target && event.key === 'Enter') {
    target.blur();
    event.stopPropagation();
  } else if (event.target.closest('[data-deals-search] input') && event.key === 'Escape') {
    event.target.value = '';
    runSearch('');
    event.stopPropagation();
  }
});

elements.filters.addEventListener('click', event => {
  if (state.view !== 'deals') return;
  const sort = event.target.closest('[data-sort-key]');
  if (sort) {
    state.prefs.dealsSort = sort.dataset.sortKey;
    savePrefs();
    updateDealsParts();
  } else if (event.target.closest('[data-deals-onsale]')) {
    state.prefs.dealsOnSale = !state.prefs.dealsOnSale;
    savePrefs();
    updateDealsParts();
  } else if (event.target.closest('[data-deals-refresh]') && !deals.loading) {
    requestWishlist(true);
    updateDealsParts();
  }
});

elements.filters.addEventListener('change', event => {
  if (state.view !== 'deals') return;
  const select = event.target.closest('[data-deals-account]');
  if (!select) return;
  setSetting('deals.steamId', select.value || null);
  deals.data = null;
  deals.loading = true;
  updateDealsParts();
});

extensions.views.deals = { render: renderDealsView };

extensions.libraryHandlers.push(() => {
  if (!deals.requested) requestWishlist(false);
});

extensions.messageHandlers.push(message => {
  if (message.type === 'dealsWishlistLoading') {
    deals.loading = true;
  } else if (message.type === 'dealsWishlist') {
    deals.loading = false;
    deals.data = message.data;
    if (message.accounts) deals.accounts = message.accounts;
    renderSidebar();
    updateDealsParts();
    const panel = document.querySelector('[data-deals-settings]');
    if (panel && !panel.contains(document.activeElement)) panel.outerHTML = dealsSettingsHtml();
  } else if (message.type === 'dealsSearch') {
    if (message.requestId !== deals.search.requestId) return;
    deals.search.loading = false;
    deals.search.result = message.result;
    const box = elements.results.querySelector('[data-deals-search-results]');
    if (state.view === 'deals' && box) box.innerHTML = searchResultsHtml();
  }
});

function dealsToggle(key, fallback, title, description) {
  const on = !!setting(key, fallback);
  return `
    <label class="deals-setting">
      <span class="deals-setting-text"><strong>${title}</strong><span>${description}</span></span>
      <input type="checkbox" class="deals-switch" data-deals-setting="${key}" ${on ? 'checked' : ''}>
    </label>`;
}

function dealsSettingsHtml() {
  const rule = setting('deals.saleRule', 'any');
  const chosen = setting('deals.steamId', '') || '';
  const key = setting('deals.itadKey', '') || '';
  const accounts = deals.accounts || [];
  return `
    <div class="deals-settings" data-deals-settings>
      ${dealsToggle('deals.notifyFree', true, 'Notify me about new free games', 'A Windows notification when a game becomes free to keep on Epic, Steam, GOG or Ubisoft. Checked every 3 hours.')}
      ${dealsToggle('deals.notifyItch', false, 'Include itch.io giveaways', 'itch.io has many small 100% off sales, so these are off by default.')}
      ${dealsToggle('deals.hideOwned', false, 'Hide owned and claimed games', 'Leave games you already have out of the Free games page.')}
      ${dealsToggle('deals.notifySales', true, 'Notify me when wishlisted games go on sale', 'Uses your Steam wishlist. Games with a price alert notify only at or below that price.')}
      <label class="deals-setting">
        <span class="deals-setting-text"><strong>Sale alerts</strong><span>Which discounts are worth a notification.</span></span>
        <select class="deals-select" data-deals-setting-select="deals.saleRule">
          ${SALE_RULES.map(entry => `<option value="${entry.key}" ${entry.key === rule ? 'selected' : ''}>${entry.label}</option>`).join('')}
        </select>
      </label>
      <label class="deals-setting">
        <span class="deals-setting-text"><strong>Steam account</strong><span>Whose wishlist to track.</span></span>
        <select class="deals-select" data-deals-setting-select="deals.steamId">
          <option value="" ${!chosen ? 'selected' : ''}>Signed-in account</option>
          ${accounts.map(account => `<option value="${escapeHtml(account.SteamId)}" ${chosen === account.SteamId ? 'selected' : ''}>${escapeHtml(account.Name)}</option>`).join('')}
        </select>
      </label>
      <form class="deals-setting" data-deals-key-form>
        <span class="deals-setting-text"><strong>IsThereAnyDeal API key</strong><span>Optional. Adds historical lows and more shops to price comparisons. <button class="link-button" type="button" data-deals-settings-open="https://isthereanydeal.com/apps/my/">Get a free key</button></span></span>
        <span class="deals-key-field">
          <input type="password" name="key" value="${escapeHtml(key)}" placeholder="Not set" autocomplete="off" spellcheck="false">
          <button class="secondary-button compact" type="submit">Save</button>
        </span>
      </form>
    </div>`;
}

extensions.settingsSections.push({
  id: 'deals',
  title: 'Deals & free games',
  render: () => {
    if (!deals.requested) requestWishlist(false);
    return dealsSettingsHtml();
  },
  bind: root => {
    root.addEventListener('change', event => {
      const toggle = event.target.closest('[data-deals-setting]');
      const select = event.target.closest('[data-deals-setting-select]');
      if (toggle) setSetting(toggle.dataset.dealsSetting, toggle.checked);
      if (select) {
        setSetting(select.dataset.dealsSettingSelect, select.value || null);
        if (select.dataset.dealsSettingSelect === 'deals.steamId') deals.data = null;
      }
    });
    root.addEventListener('submit', event => {
      const form = event.target.closest('[data-deals-key-form]');
      if (!form) return;
      event.preventDefault();
      setSetting('deals.itadKey', form.elements.key.value.trim() || null);
      toast(form.elements.key.value.trim() ? 'IsThereAnyDeal key saved' : 'IsThereAnyDeal key removed', 'info', ICON.check);
    });
    root.addEventListener('click', event => {
      const link = event.target.closest('[data-deals-settings-open]');
      if (!link) return;
      event.preventDefault();
      openDealUrl(link.dataset.dealsSettingsOpen, true);
    });
  },
});
