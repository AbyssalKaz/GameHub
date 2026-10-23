'use strict';

ICON.film = '<svg viewBox="0 0 24 24" class="glyph"><rect x="3.5" y="5" width="17" height="14" rx="2.5"/><path d="M8 5v14M16 5v14M3.5 9.5H8M3.5 14.5H8M16 9.5h4.5M16 14.5h4.5"/></svg>';
ICON.news = '<svg viewBox="0 0 24 24" class="glyph"><path d="M5 5.5h11.5v13a1.5 1.5 0 0 0 1.5 1.5H6.5A1.5 1.5 0 0 1 5 18.5z"/><path d="M16.5 9H19a1 1 0 0 1 1 1v8.5a1.5 1.5 0 0 1-3 0M8 9h5.5M8 12.5h5.5M8 16h3.5"/></svg>';
ICON.thumbUp = '<svg viewBox="0 0 24 24" class="glyph"><path d="M7.5 10.5v9h-3v-9zM7.5 10.5l3.8-6.2a1.6 1.6 0 0 1 2.9 1.1l-.7 3.6h5a2 2 0 0 1 2 2.4l-1.3 6.5a2 2 0 0 1-2 1.6H7.5"/></svg>';
ICON.thumbDown = '<svg viewBox="0 0 24 24" class="glyph"><path d="M7.5 13.5v-9h-3v9zM7.5 13.5l3.8 6.2a1.6 1.6 0 0 0 2.9-1.1l-.7-3.6h5a2 2 0 0 0 2-2.4l-1.3-6.5a2 2 0 0 0-2-1.6H7.5"/></svg>';
ICON.hourglass = '<svg viewBox="0 0 24 24" class="glyph"><path d="M7 3.5h10M7 20.5h10M8 3.5c0 4.5 8 4.5 8 8.5s-8 4-8 8.5M16 3.5c0 4.5-8 4.5-8 8.5s8 4 8 8.5"/></svg>';
ICON.tag = '<svg viewBox="0 0 24 24" class="glyph"><path d="M3.5 12.2V5a1.5 1.5 0 0 1 1.5-1.5h7.2a1.5 1.5 0 0 1 1.06.44l7.3 7.3a1.5 1.5 0 0 1 0 2.12l-7.2 7.2a1.5 1.5 0 0 1-2.12 0l-7.3-7.3a1.5 1.5 0 0 1-.44-1.06z"/><circle cx="8.5" cy="8.5" r="1.4"/></svg>';
ICON.globe = '<svg viewBox="0 0 24 24" class="glyph"><circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c2.5 2.6 3.7 5.6 3.7 9s-1.2 6.4-3.7 9c-2.5-2.6-3.7-5.6-3.7-9S9.5 5.6 12 3z"/></svg>';
ICON.chevronLeft = '<svg viewBox="0 0 24 24" class="glyph"><path d="m15 5-7 7 7 7"/></svg>';
ICON.chevronRight = '<svg viewBox="0 0 24 24" class="glyph"><path d="m9 5 7 7-7 7"/></svg>';
ICON.chevronDown = '<svg viewBox="0 0 24 24" class="glyph"><path d="m6 9 6 6 6-6"/></svg>';
ICON.wand = '<svg viewBox="0 0 24 24" class="glyph"><path d="m4 20 10.5-10.5M13 5.5V3M18.5 11H21M16.8 7.2l1.7-1.7M15.5 3.8l.9.9M19.3 7.6l.9.9"/><path d="m12.5 7.5 4 4"/></svg>';

const META_SETTINGS = {
  animated: 'meta.animatedBackgrounds',
  hover: 'meta.hoverPreviews',
  autoArt: 'meta.autoGridArt',
};

const META_PHASES = {
  matching: 'Matching games to Steam',
  media: 'Loading trailers and screenshots',
  details: 'Loading descriptions and genres',
  artwork: 'Finding missing artwork',
  playtime: 'Checking how long games take',
};

const metaUi = {
  requested: new Map(),
  loading: new Set(),
  status: null,
  fill: null,
  fillStartedHere: false,
  showMissed: false,
  search: null,
  genreOpen: false,
  refreshTimer: null,
  lightbox: null,
  hover: { timer: null, card: null, video: null },
  drawer: {
    id: null,
    about: null,
    media: null,
    news: null,
    aboutHtml: '',
    newsHtml: '',
    mediaKey: '',
    selected: 0,
    player: null,
    heroVideo: null,
    heroTimer: null,
  },
};

const reducedMotion = window.matchMedia ? window.matchMedia('(prefers-reduced-motion: reduce)') : { matches: false };

function metaOf(id) {
  return state.meta[id] || null;
}

function metaMicro(meta) {
  const movie = meta && (meta.Movies || []).find(entry => entry.Micro || entry.MicroMp4);
  return movie ? { webm: movie.Micro, mp4: movie.MicroMp4 } : null;
}

function mergeMeta(previous, next) {
  if (!next) return previous || null;
  if (next.News == null && previous && previous.News) next.News = previous.News;
  return next;
}

function requestMeta(id, force = false) {
  const last = metaUi.requested.get(id) || 0;
  if (!force && Date.now() - last < 10 * 60 * 1000) return;
  metaUi.requested.set(id, Date.now());
  metaUi.loading.add(id);
  send({ type: 'metaRequest', id, force });
}

function formatHours(hours) {
  if (hours == null) return null;
  return `${Number.isInteger(hours) ? hours : hours.toFixed(1)} h`;
}

function reviewTone(score) {
  if (score == null) return 'neutral';
  if (score >= 80) return 'good';
  if (score >= 70) return 'fair';
  if (score >= 40) return 'mixed';
  return 'poor';
}

function criticTone(score) {
  if (score >= 75) return 'good';
  if (score >= 50) return 'mixed';
  return 'poor';
}

function compactCount(count) {
  if (count >= 1e6) return `${(count / 1e6).toFixed(count >= 1e7 ? 0 : 1)}M`;
  if (count >= 1e4) return `${Math.round(count / 1e3)}K`;
  return formatCount(count);
}

function releaseText(meta) {
  if (meta.ComingSoon) return meta.ReleaseText ? `Coming ${meta.ReleaseText}` : 'Coming soon';
  if (meta.ReleaseDate) return new Date(`${meta.ReleaseDate}T12:00:00`).toLocaleDateString('en-GB', { day: 'numeric', month: 'long', year: 'numeric' });
  return meta.ReleaseText || null;
}

function openExternal(url) {
  if (url && /^https?:\/\//i.test(url)) send({ type: 'metaOpenUrl', url });
}

function metaOpenInStore(url) {
  if (!url || !/^https?:\/\//i.test(url)) return;
  if (typeof openInStoreBrowser !== 'function' || !extensions.views.stores) {
    openExternal(url);
    return;
  }
  closeDrawer();
  openInStoreBrowser(url, true);
}

function badgesHtml(meta) {
  const badges = [];
  if (meta.ReviewScore != null && meta.ReviewCount) {
    const tone = reviewTone(meta.ReviewScore);
    badges.push(`
      <div class="meta-badge tone-${tone}" title="${escapeHtml(`${meta.ReviewScore}% of ${formatCount(meta.ReviewCount)} Steam reviews are positive`)}">
        <span class="meta-badge-icon">${meta.ReviewScore >= 40 ? ICON.thumbUp : ICON.thumbDown}</span>
        <div class="meta-badge-text">
          <strong>${escapeHtml(meta.ReviewSummary || `${meta.ReviewScore}% positive`)}</strong>
          <span>${meta.ReviewScore}% of ${compactCount(meta.ReviewCount)} reviews</span>
        </div>
      </div>`);
  }
  if (meta.Metacritic) {
    const tone = criticTone(meta.Metacritic);
    const tag = meta.MetacriticUrl ? 'button' : 'div';
    badges.push(`
      <${tag} class="meta-badge critic" ${meta.MetacriticUrl ? `data-meta-open="${escapeHtml(meta.MetacriticUrl)}" title="Open on Metacritic"` : ''}>
        <span class="meta-score tone-${tone}">${escapeHtml(meta.Metacritic)}</span>
        <div class="meta-badge-text"><strong>Metascore</strong><span>Critic reviews</span></div>
      </${tag}>`);
  }
  return badges.length ? `<div class="meta-badges">${badges.join('')}</div>` : '';
}

function hltbHtml(meta) {
  const times = meta.Hltb;
  if (!times) return '';
  const cell = (label, value) => `
    <div class="meta-hltb-cell ${value == null ? 'empty' : ''}">
      <span class="meta-hltb-value">${value == null ? '—' : escapeHtml(formatHours(value))}</span>
      <span class="meta-hltb-label">${label}</span>
    </div>`;
  const tag = times.Url ? 'button' : 'div';
  return `
    <${tag} class="meta-hltb" ${times.Url ? `data-meta-open="${escapeHtml(times.Url)}" title="Open on HowLongToBeat"` : ''}>
      <span class="meta-hltb-icon">${ICON.hourglass}</span>
      ${cell('Main story', times.Main)}
      ${cell('Main + extras', times.Extra)}
      ${cell('Completionist', times.Complete)}
    </${tag}>`;
}

function factsHtml(meta) {
  const rows = [];
  const list = values => values.map(escapeHtml).join(', ');
  if (meta.Developers && meta.Developers.length) rows.push(`<dt>Developer</dt><dd>${list(meta.Developers)}</dd>`);
  if (meta.Publishers && meta.Publishers.length && list(meta.Publishers) !== list(meta.Developers || [])) rows.push(`<dt>Publisher</dt><dd>${list(meta.Publishers)}</dd>`);
  const released = releaseText(meta);
  if (released) rows.push(`<dt>${meta.ComingSoon ? 'Release' : 'Released'}</dt><dd>${escapeHtml(released)}</dd>`);
  const links = [];
  if (meta.StoreUrl) links.push(`<button class="meta-link" data-meta-store="${escapeHtml(meta.StoreUrl)}">${platformIcon('steam')}Store page</button>`);
  if (meta.Website) links.push(`<button class="meta-link" data-meta-open="${escapeHtml(meta.Website)}">${ICON.globe}Website</button>`);
  if (links.length) rows.push(`<dt>Links</dt><dd class="meta-links">${links.join('')}</dd>`);
  return rows.length ? `<dl class="details-list meta-facts">${rows.join('')}</dl>` : '';
}

function matchHtml(game, meta) {
  const kind = meta ? meta.Match : 'pending';
  if (game.Source === 'Steam' && kind !== 'manual' && kind !== 'none') return '';
  if (metaUi.search && metaUi.search.id === game.Id) return searchHtml(game);

  if (kind === 'none' || (!meta || !meta.SteamAppId) && !metaUi.loading.has(game.Id)) {
    if (kind === 'pending' && metaUi.loading.has(game.Id)) return '';
    return `
      <div class="meta-match">
        <span>${kind === 'none' && gameData(game.Id)['meta.steamAppId'] === 'none' ? 'Store info turned off for this game.' : 'No Steam page matched this game.'}</span>
        <button class="link-button" data-meta-action="search">${ICON.search}Find it on Steam</button>
      </div>`;
  }
  if (!meta || !meta.SteamAppId) return '';
  return `
    <div class="meta-match">
      <span>Info from Steam: <strong>${escapeHtml(meta.Name || `App ${meta.SteamAppId}`)}</strong>${kind === 'manual' ? ' (chosen by you)' : ''}</span>
      <button class="link-button" data-meta-action="search">Wrong game?</button>
    </div>`;
}

function searchHtml(game) {
  const search = metaUi.search;
  const manual = gameData(game.Id)['meta.steamAppId'];
  let results;
  if (search.loading) {
    results = Array.from({ length: 3 }, () => '<div class="meta-result skeleton"><span class="meta-result-image"></span><span class="meta-skeleton-line"></span></div>').join('');
  } else if (search.error) {
    results = `<div class="meta-search-note">${ICON.alert}${escapeHtml(search.error)}</div>`;
  } else if (!search.results.length) {
    results = `<div class="meta-search-note">${ICON.search}No Steam games found. Try a shorter name.</div>`;
  } else {
    results = search.results.map(result => `
      <button class="meta-result" data-meta-pick="${escapeHtml(result.AppId)}">
        <img class="meta-result-image" src="${escapeHtml(result.Image)}" alt="" loading="lazy">
        <span class="meta-result-name">${escapeHtml(result.Name)}</span>
        <span class="meta-result-id">${escapeHtml(result.AppId)}</span>
      </button>`).join('');
  }

  return `
    <div class="meta-search">
      <form class="meta-search-form" data-meta-search-form>
        ${ICON.search}
        <input name="query" type="text" value="${escapeHtml(search.query)}" placeholder="Search Steam by name" autocomplete="off" spellcheck="false">
        <button class="text-button" type="submit">Search</button>
      </form>
      <div class="meta-results">${results}</div>
      <div class="meta-search-foot">
        <button class="link-button" data-meta-pick="none">${ICON.eyeOff}Not on Steam</button>
        ${manual ? `<button class="link-button" data-meta-pick="auto">${ICON.refresh}Match automatically</button>` : ''}
        <span class="grow"></span>
        <button class="text-button" data-meta-action="cancel-search">Cancel</button>
      </div>
    </div>`;
}

function aboutHtml(game) {
  const meta = metaOf(game.Id);
  const loading = metaUi.loading.has(game.Id);
  const match = matchHtml(game, meta);
  const hasInfo = meta && (meta.Description || (meta.Genres && meta.Genres.length) || meta.ReviewScore != null || meta.Metacritic || meta.Hltb);

  if (!hasInfo) {
    if (loading && !match) {
      return `
        <div class="meta-about loading">
          <div class="meta-badges"><span class="meta-badge skeleton"></span><span class="meta-badge skeleton"></span></div>
          <span class="meta-skeleton-line"></span><span class="meta-skeleton-line"></span><span class="meta-skeleton-line short"></span>
        </div>`;
    }
    return match ? `<div class="meta-about">${match}</div>` : '';
  }

  const genres = (meta.Genres || []).map(genre => `<button class="meta-genre ${state.prefs.genre === genre ? 'active' : ''}" data-meta-genre="${escapeHtml(genre)}" title="Show ${escapeHtml(genre)} games">${escapeHtml(genre)}</button>`).join('');
  return `
    <div class="meta-about">
      ${badgesHtml(meta)}
      ${hltbHtml(meta)}
      ${meta.Description ? `<p class="meta-description">${escapeHtml(meta.Description)}</p>` : ''}
      ${genres ? `<div class="meta-genres">${genres}</div>` : ''}
      ${factsHtml(meta)}
      ${match}
    </div>`;
}

function mediaItems(meta) {
  if (!meta) return [];
  const movies = (meta.Movies || []).filter(movie => movie.Dash || movie.Mp4 || movie.Webm || movie.Micro || movie.MicroMp4).slice(0, 8);
  const thumbs = meta.ScreenshotThumbs || [];
  return [
    ...movies.map(movie => ({ kind: 'movie', movie, thumb: movie.Thumb || movie.Poster })),
    ...(meta.Screenshots || []).map((url, index) => ({ kind: 'shot', url, index, thumb: thumbs[index] || url })),
  ];
}

function mediaKeyOf(meta) {
  return mediaItems(meta).map(item => item.kind === 'movie' ? item.movie.Dash || item.movie.Mp4 || item.movie.Micro : item.url).join('|');
}

function stageHtml(item) {
  if (!item) return '';
  if (item.kind === 'movie') {
    const poster = item.movie.Poster || item.movie.Thumb;
    return `
      <button class="meta-stage-poster" data-meta-play title="Play trailer">
        ${poster ? `<img src="${escapeHtml(poster)}" alt="">` : ''}
        <span class="meta-stage-play">${ICON.play}</span>
        ${item.movie.Name ? `<span class="meta-stage-name">${escapeHtml(item.movie.Name)}</span>` : ''}
      </button>`;
  }
  return `<button class="meta-stage-shot" data-meta-lightbox="${item.index}" title="View full size"><img src="${escapeHtml(item.url)}" alt=""></button>`;
}

function mediaHtml(meta) {
  const items = mediaItems(meta);
  if (!items.length) return '';
  const selected = Math.min(metaUi.drawer.selected, items.length - 1);
  const thumbs = items.map((item, index) => `
    <button class="meta-thumb ${index === selected ? 'active' : ''} ${item.kind}" data-meta-item="${index}" title="${escapeHtml(item.kind === 'movie' ? item.movie.Name || 'Trailer' : `Screenshot ${item.index + 1}`)}">
      ${item.thumb ? `<img src="${escapeHtml(item.thumb)}" alt="" loading="lazy" decoding="async">` : ''}
      ${item.kind === 'movie' ? `<span class="meta-thumb-play">${ICON.play}</span>` : ''}
    </button>`).join('');
  return `
    <h3 class="section-title">Media</h3>
    <div class="meta-stage" data-meta-stage>${stageHtml(items[selected])}</div>
    ${items.length > 1 ? `<div class="meta-strip" data-meta-strip>${thumbs}</div>` : ''}`;
}

function destroyPlayer() {
  const drawer = metaUi.drawer;
  if (drawer.player) {
    drawer.player.destroy();
    drawer.player = null;
  }
  const video = drawer.media && drawer.media.querySelector('.meta-stage-video');
  if (video) {
    video.pause();
    video.removeAttribute('src');
    video.load();
  }
}

function selectMediaItem(index) {
  const drawer = metaUi.drawer;
  const meta = metaOf(drawer.id);
  const items = mediaItems(meta);
  if (!items[index] || !drawer.media) return;
  destroyPlayer();
  drawer.selected = index;
  const stage = drawer.media.querySelector('[data-meta-stage]');
  if (stage) stage.innerHTML = stageHtml(items[index]);
  for (const thumb of drawer.media.querySelectorAll('[data-meta-item]')) thumb.classList.toggle('active', Number(thumb.dataset.metaItem) === index);
  const active = drawer.media.querySelector('.meta-thumb.active');
  if (active) active.scrollIntoView({ block: 'nearest', inline: 'nearest', behavior: 'smooth' });
}

function playSelectedMovie() {
  const drawer = metaUi.drawer;
  const item = mediaItems(metaOf(drawer.id))[drawer.selected];
  const stage = drawer.media && drawer.media.querySelector('[data-meta-stage]');
  if (!item || item.kind !== 'movie' || !stage) return;
  destroyPlayer();

  const movie = item.movie;
  stage.innerHTML = `<video class="meta-stage-video" controls playsinline ${movie.Poster ? `poster="${escapeHtml(movie.Poster)}"` : ''}></video>`;
  const video = stage.querySelector('video');
  const volume = Number(readStorage('hub.metaVolume', 0.7));
  video.volume = Number.isFinite(volume) ? Math.min(1, Math.max(0, volume)) : 0.7;
  video.addEventListener('volumechange', () => writeStorage('hub.metaVolume', video.volume));
  video.addEventListener('play', () => {
    if (drawer.heroVideo) {
      drawer.heroVideo.dataset.wanted = '';
      drawer.heroVideo.pause();
    }
  });
  video.addEventListener('pause', () => {
    if (!video.isConnected && elements.drawer.classList.contains('open')) video.dataset.resume = '1';
  });

  const fallback = () => {
    if (drawer.player) {
      drawer.player.destroy();
      drawer.player = null;
    }
    const micro = movie.Micro || movie.MicroMp4;
    if (micro && video.isConnected) {
      video.loop = true;
      video.src = micro;
      video.play().catch(() => {});
      toast('Showing a short preview. The full trailer couldn’t be loaded.', 'info', ICON.film);
    } else if (stage.isConnected) {
      stage.innerHTML = `
        <div class="meta-stage-error">${ICON.alert}<span>This trailer couldn’t be played here.</span>
          ${metaOf(drawer.id) && metaOf(drawer.id).StoreUrl ? `<button class="text-button" data-meta-store="${escapeHtml(metaOf(drawer.id).StoreUrl)}">Watch on Steam</button>` : ''}
        </div>`;
    }
  };

  const direct = movie.Webm || movie.Mp4;
  if (direct) {
    video.src = direct;
    video.addEventListener('error', fallback, { once: true });
    video.play().catch(() => {});
    return;
  }
  if (movie.Dash && MetaDashPlayer.supported()) {
    const player = new MetaDashPlayer(video, movie.Dash, fallback);
    drawer.player = player;
    player.start().then(() => {
      if (drawer.player === player) video.play().catch(() => {});
    }).catch(() => {
      if (drawer.player === player) fallback();
    });
    return;
  }
  fallback();
}

function newsHtml(meta) {
  const news = meta && meta.News;
  if (!news || !news.length) return '';
  return `
    <h3 class="section-title">News</h3>
    <div class="meta-news-list">
      ${news.map(item => `
        <button class="meta-news-item" ${item.Url ? `data-meta-news="${escapeHtml(item.Url)}"` : 'disabled'}>
          <div class="meta-news-top">
            <span class="meta-news-feed ${item.Patch ? 'patch' : ''}">${escapeHtml(item.Patch ? 'Patch notes' : item.Feed || 'News')}</span>
            <span class="meta-news-date" title="${escapeHtml(formatDate(item.Date) || '')}">${escapeHtml(relativeTime(item.Date) || '')}</span>
          </div>
          <div class="meta-news-title">${escapeHtml(item.Title)}</div>
          ${item.Excerpt ? `<div class="meta-news-excerpt">${escapeHtml(item.Excerpt)}</div>` : ''}
        </button>`).join('')}
    </div>`;
}

function removeHeroVideo() {
  const drawer = metaUi.drawer;
  clearTimeout(drawer.heroTimer);
  drawer.heroTimer = null;
  if (drawer.heroVideo) {
    drawer.heroVideo.pause();
    drawer.heroVideo.removeAttribute('src');
    drawer.heroVideo.load();
    drawer.heroVideo.remove();
    drawer.heroVideo = null;
  }
}

function placeHeroVideo(hero, video) {
  const image = hero.querySelector(':scope > img.hero');
  if (image) image.insertAdjacentElement('afterend', video);
  else hero.insertAdjacentElement('afterbegin', video);
}

function mountHeroVideo(game) {
  const drawer = metaUi.drawer;
  const hero = elements.drawer.querySelector('.drawer-hero');
  if (!hero) return;

  if (drawer.heroVideo) {
    if (drawer.heroVideo.parentElement !== hero) {
      placeHeroVideo(hero, drawer.heroVideo);
      if (drawer.heroVideo.dataset.wanted === '1') drawer.heroVideo.play().catch(() => {});
    }
    return;
  }
  if (drawer.heroTimer || !setting(META_SETTINGS.animated, true) || reducedMotion.matches) return;
  const micro = metaMicro(metaOf(game.Id));
  if (!micro) return;

  drawer.heroTimer = setTimeout(() => {
    drawer.heroTimer = null;
    const currentHero = elements.drawer.querySelector('.drawer-hero');
    if (!currentHero || drawer.id !== game.Id || !elements.drawer.classList.contains('open')) return;
    const video = document.createElement('video');
    video.className = 'meta-hero-video';
    video.muted = true;
    video.loop = true;
    video.playsInline = true;
    video.preload = 'auto';
    video.dataset.wanted = '1';
    video.addEventListener('playing', () => video.classList.add('visible'));
    video.addEventListener('error', () => {
      if (micro.mp4 && video.src !== micro.mp4) {
        video.src = micro.mp4;
        video.play().catch(() => {});
      } else {
        video.remove();
      }
    });
    video.src = micro.webm || micro.mp4;
    drawer.heroVideo = video;
    placeHeroVideo(currentHero, video);
    video.play().catch(() => {});
  }, 1800);
}

function resetDrawerMeta() {
  destroyPlayer();
  removeHeroVideo();
  const drawer = metaUi.drawer;
  for (const node of [drawer.about, drawer.media, drawer.news]) if (node) node.remove();
  Object.assign(drawer, { id: null, about: null, media: null, news: null, aboutHtml: '', newsHtml: '', mediaKey: '', selected: 0 });
}

function suspendDrawerMeta() {
  removeHeroVideo();
  const video = metaUi.drawer.media && metaUi.drawer.media.querySelector('.meta-stage-video');
  if (video) video.pause();
}

function updateDrawerMeta(game) {
  const drawer = metaUi.drawer;
  const meta = metaOf(game.Id);

  const about = aboutHtml(game);
  if (about !== drawer.aboutHtml) {
    const input = drawer.about.querySelector('input[name="query"]');
    const focused = input && document.activeElement === input;
    const caret = focused ? input.selectionStart : 0;
    drawer.aboutHtml = about;
    drawer.about.innerHTML = about;
    drawer.about.hidden = !about;
    const next = drawer.about.querySelector('input[name="query"]');
    if (next && (focused || (metaUi.search && metaUi.search.focus))) {
      next.focus();
      next.setSelectionRange(focused ? caret : next.value.length, focused ? caret : next.value.length);
      if (metaUi.search) metaUi.search.focus = false;
    }
  }

  const key = mediaKeyOf(meta);
  if (key !== drawer.mediaKey) {
    destroyPlayer();
    drawer.mediaKey = key;
    drawer.selected = 0;
    drawer.media.innerHTML = mediaHtml(meta);
    drawer.media.hidden = !key;
  }

  const news = newsHtml(meta);
  if (news !== drawer.newsHtml) {
    drawer.newsHtml = news;
    drawer.news.innerHTML = news;
    drawer.news.hidden = !news;
  }
}

function mountDrawerMeta() {
  const open = elements.drawer.classList.contains('open');
  const game = state.selectedId ? state.byId.get(state.selectedId) : null;
  const drawer = metaUi.drawer;

  if (!game) {
    resetDrawerMeta();
    return;
  }
  if (drawer.id !== game.Id) {
    resetDrawerMeta();
    drawer.id = game.Id;
    if (metaUi.search && metaUi.search.id !== game.Id) metaUi.search = null;
  }
  if (!open) {
    suspendDrawerMeta();
    return;
  }

  const body = elements.drawer.querySelector('.drawer-body');
  if (!body) return;

  if (!drawer.about) {
    drawer.about = Object.assign(document.createElement('section'), { className: 'meta-block meta-about-block' });
    drawer.media = Object.assign(document.createElement('section'), { className: 'meta-block meta-media' });
    drawer.news = Object.assign(document.createElement('section'), { className: 'meta-block meta-news' });
    drawer.about.hidden = drawer.media.hidden = drawer.news.hidden = true;
  }
  updateDrawerMeta(game);

  const anchor = body.querySelector(':scope > .stats') || body.querySelector(':scope > .actions');
  if (anchor) {
    if (anchor.nextElementSibling !== drawer.about) anchor.insertAdjacentElement('afterend', drawer.about);
  } else if (drawer.about.parentElement !== body) {
    body.prepend(drawer.about);
  }
  if (drawer.about.nextElementSibling !== drawer.media) drawer.about.insertAdjacentElement('afterend', drawer.media);

  const slot = body.querySelector('[data-meta-slot]');
  if (slot && drawer.news.parentElement !== slot) slot.appendChild(drawer.news);
  else if (!slot && drawer.news.parentElement !== body) body.appendChild(drawer.news);

  const stageVideo = drawer.media.querySelector('.meta-stage-video');
  if (stageVideo && stageVideo.dataset.resume === '1') {
    stageVideo.dataset.resume = '';
    stageVideo.play().catch(() => {});
  }

  mountHeroVideo(game);
}

new MutationObserver(mountDrawerMeta).observe(elements.drawer, { childList: true, attributes: true, attributeFilter: ['class'] });

extensions.drawerSections.push(game => {
  requestMeta(game.Id);
  return '<div class="meta-slot" data-meta-slot></div>';
});

function startMetaSearch(game, query) {
  const requestId = String(Date.now());
  metaUi.search = { id: game.Id, query: query || game.Name, requestId, results: [], loading: true, error: null, focus: true };
  send({ type: 'metaSearch', id: game.Id, query: metaUi.search.query, requestId });
  updateDrawerMeta(game);
}

function pickMetaMatch(game, value) {
  const appId = value === 'auto' ? null : value;
  setGameData(game.Id, { 'meta.steamAppId': appId });
  metaUi.search = null;
  metaUi.loading.add(game.Id);
  if (value === 'none') delete state.meta[game.Id];
  send({ type: 'metaRequest', id: game.Id, force: true });
  metaUi.requested.set(game.Id, Date.now());
  updateDrawerMeta(game);
  toast(value === 'none' ? 'Store info turned off for this game' : value === 'auto' ? 'Matching automatically again' : 'Loading info for the chosen game', 'info', ICON.check);
}

function setGenreFilter(genre) {
  state.prefs.genre = genre || null;
  if (!state.prefs.genre) delete state.prefs.genre;
  savePrefs();
  metaUi.genreOpen = false;
  renderAll();
}

elements.drawer.addEventListener('click', event => {
  const game = state.byId.get(state.selectedId);
  if (!game || !event.target.closest('.meta-block')) return;

  const open = event.target.closest('[data-meta-open]');
  const store = event.target.closest('[data-meta-store]');
  const news = event.target.closest('[data-meta-news]');
  const genre = event.target.closest('[data-meta-genre]');
  const action = event.target.closest('[data-meta-action]');
  const pick = event.target.closest('[data-meta-pick]');
  const item = event.target.closest('[data-meta-item]');
  const shot = event.target.closest('[data-meta-lightbox]');

  if (open) openExternal(open.dataset.metaOpen);
  else if (store) metaOpenInStore(store.dataset.metaStore);
  else if (news) metaOpenInStore(news.dataset.metaNews);
  else if (genre) {
    const value = genre.dataset.metaGenre;
    setGenreFilter(state.prefs.genre === value ? null : value);
    if (state.prefs.genre) {
      closeDrawer();
      toast(`Showing ${value} games`, 'info', ICON.tag);
    }
  } else if (pick) pickMetaMatch(game, pick.dataset.metaPick);
  else if (action && action.dataset.metaAction === 'search') {
    const meta = metaOf(game.Id);
    startMetaSearch(game, game.Name || (meta && meta.Name));
  } else if (action && action.dataset.metaAction === 'cancel-search') {
    metaUi.search = null;
    updateDrawerMeta(game);
  } else if (item) selectMediaItem(Number(item.dataset.metaItem));
  else if (event.target.closest('[data-meta-play]')) playSelectedMovie();
  else if (shot) openLightbox(Number(shot.dataset.metaLightbox));
});

elements.drawer.addEventListener('submit', event => {
  const form = event.target.closest('[data-meta-search-form]');
  if (!form) return;
  event.preventDefault();
  const game = state.byId.get(state.selectedId);
  if (game) startMetaSearch(game, form.elements.query.value.trim() || game.Name);
});

elements.drawer.addEventListener('input', event => {
  if (event.target.name === 'query' && event.target.closest('.meta-search') && metaUi.search) metaUi.search.query = event.target.value;
});

elements.drawer.addEventListener('wheel', event => {
  const strip = event.target.closest('[data-meta-strip]');
  if (!strip || Math.abs(event.deltaY) <= Math.abs(event.deltaX)) return;
  if (strip.scrollWidth <= strip.clientWidth) return;
  event.preventDefault();
  strip.scrollLeft += event.deltaY;
}, { passive: false });

function openLightbox(index) {
  const meta = metaOf(metaUi.drawer.id);
  const shots = (meta && meta.Screenshots) || [];
  if (!shots.length) return;
  metaUi.lightbox = { shots, index: Math.max(0, Math.min(index, shots.length - 1)) };

  let layer = document.querySelector('.meta-lightbox');
  if (!layer) {
    layer = document.createElement('div');
    layer.className = 'meta-lightbox';
    layer.innerHTML = `
      <div class="meta-lightbox-backdrop" data-meta-lightbox-close></div>
      <img class="meta-lightbox-image" alt="">
      <button class="meta-lightbox-nav prev" data-meta-lightbox-step="-1" title="Previous">${ICON.chevronLeft}</button>
      <button class="meta-lightbox-nav next" data-meta-lightbox-step="1" title="Next">${ICON.chevronRight}</button>
      <div class="meta-lightbox-count"></div>
      <button class="meta-lightbox-close" data-meta-lightbox-close title="Close">${ICON.close}</button>`;
    layer.addEventListener('click', event => {
      const step = event.target.closest('[data-meta-lightbox-step]');
      if (step) stepLightbox(Number(step.dataset.metaLightboxStep));
      else if (event.target.closest('[data-meta-lightbox-close]')) closeLightbox();
    });
    document.body.appendChild(layer);
  }
  renderLightbox();
  requestAnimationFrame(() => layer.classList.add('open'));
}

function renderLightbox() {
  const layer = document.querySelector('.meta-lightbox');
  const box = metaUi.lightbox;
  if (!layer || !box) return;
  const image = layer.querySelector('.meta-lightbox-image');
  image.classList.remove('loaded');
  image.onload = () => image.classList.add('loaded');
  image.src = box.shots[box.index];
  layer.querySelector('.meta-lightbox-count').textContent = `${box.index + 1} / ${box.shots.length}`;
  layer.classList.toggle('single', box.shots.length < 2);
  for (const offset of [1, -1]) {
    const neighbour = box.shots[(box.index + offset + box.shots.length) % box.shots.length];
    if (neighbour) new Image().src = neighbour;
  }
}

function stepLightbox(step) {
  const box = metaUi.lightbox;
  if (!box) return;
  box.index = (box.index + step + box.shots.length) % box.shots.length;
  renderLightbox();
}

function closeLightbox() {
  const layer = document.querySelector('.meta-lightbox');
  metaUi.lightbox = null;
  if (!layer) return;
  layer.classList.remove('open');
  setTimeout(() => {
    if (!metaUi.lightbox) layer.remove();
  }, 220);
}

extensions.keyHandlers.push(event => {
  if (!metaUi.lightbox) return false;
  if (event.key === 'Escape') closeLightbox();
  else if (event.key === 'ArrowLeft') stepLightbox(-1);
  else if (event.key === 'ArrowRight') stepLightbox(1);
  else return true;
  event.preventDefault();
  return true;
});

function stopHoverPreview() {
  const hover = metaUi.hover;
  clearTimeout(hover.timer);
  hover.timer = null;
  hover.card = null;
  if (hover.video) {
    const video = hover.video;
    hover.video = null;
    video.classList.remove('visible');
    video.pause();
    setTimeout(() => {
      video.removeAttribute('src');
      video.load();
      video.remove();
    }, 200);
  }
}

function startHoverPreview(card, micro) {
  const hover = metaUi.hover;
  const cover = card.querySelector('.cover');
  if (!cover || hover.card !== card || !card.isConnected) return;
  const video = document.createElement('video');
  video.className = 'meta-card-preview';
  video.muted = true;
  video.loop = true;
  video.playsInline = true;
  video.addEventListener('playing', () => {
    if (hover.video === video) video.classList.add('visible');
  });
  video.addEventListener('error', () => {
    if (micro.mp4 && video.src !== micro.mp4) {
      video.src = micro.mp4;
      video.play().catch(() => {});
    }
  });
  video.src = micro.webm || micro.mp4;
  const shade = cover.querySelector('.cover-shade');
  if (shade) cover.insertBefore(video, shade);
  else cover.appendChild(video);
  hover.video = video;
  video.play().catch(() => {});
}

elements.results.addEventListener('mouseover', event => {
  const card = event.target.closest('.card[data-id]');
  if (!card || card === metaUi.hover.card) return;
  stopHoverPreview();
  if (!setting(META_SETTINGS.hover, true) || reducedMotion.matches) return;
  const micro = metaMicro(metaOf(card.dataset.id));
  if (!micro) return;
  metaUi.hover.card = card;
  metaUi.hover.timer = setTimeout(() => startHoverPreview(card, micro), 600);
});

elements.results.addEventListener('mouseout', event => {
  const card = metaUi.hover.card;
  if (!card) return;
  if (event.relatedTarget && card.contains(event.relatedTarget)) return;
  if (!card.contains(event.target)) return;
  stopHoverPreview();
});

elements.results.addEventListener('mousedown', () => stopHoverPreview());
elements.scroller.addEventListener('scroll', () => {
  if (metaUi.hover.card) stopHoverPreview();
}, { passive: true });
window.addEventListener('blur', stopHoverPreview);

function genreCounts() {
  const counts = new Map();
  const list = typeof baseList === 'function' ? baseList() : state.games;
  for (const game of list) {
    const meta = state.meta[game.Id];
    for (const genre of (meta && meta.Genres) || []) counts.set(genre, (counts.get(genre) || 0) + 1);
  }
  return [...counts.entries()].sort((first, second) => second[1] - first[1] || first[0].localeCompare(second[0]));
}

extensions.filters.push(list => {
  const genre = state.prefs.genre;
  if (!genre) return list;
  return list.filter(game => {
    const meta = state.meta[game.Id];
    return !!meta && (meta.Genres || []).includes(genre);
  });
});

extensions.filterChips.push(() => {
  const genres = genreCounts();
  const active = state.prefs.genre;
  if (!genres.length && !active) return '';
  const items = genres.map(([genre, count]) => `
    <button class="menu-item ${active === genre ? 'selected' : ''}" data-meta-genre-pick="${escapeHtml(genre)}">
      ${ICON.check}<span class="meta-genre-name">${escapeHtml(genre)}</span><span class="meta-genre-count">${formatCount(count)}</span>
    </button>`).join('');
  return `
    <div class="meta-genre-filter ${metaUi.genreOpen ? 'open' : ''}" data-meta-genre-filter>
      <button class="chip ${active ? 'active removable' : ''}" data-meta-genre-toggle>
        ${ICON.tag}${escapeHtml(active || 'Genre')}${active ? `<span class="meta-genre-clear" data-meta-genre-clear title="Show all genres">${ICON.close}</span>` : ICON.chevronDown}
      </button>
      <div class="meta-genre-menu">
        <button class="menu-item ${active ? '' : 'selected'}" data-meta-genre-pick="">${ICON.check}<span class="meta-genre-name">All genres</span></button>
        <div class="menu-separator"></div>
        <div class="meta-genre-items">${items}</div>
      </div>
    </div>`;
});

elements.filters.addEventListener('click', event => {
  const wrapper = event.target.closest('[data-meta-genre-filter]');
  if (!wrapper) return;
  event.stopPropagation();
  const pick = event.target.closest('[data-meta-genre-pick]');
  if (event.target.closest('[data-meta-genre-clear]')) setGenreFilter(null);
  else if (pick) setGenreFilter(pick.dataset.metaGenrePick);
  else if (event.target.closest('[data-meta-genre-toggle]')) {
    metaUi.genreOpen = !metaUi.genreOpen;
    wrapper.classList.toggle('open', metaUi.genreOpen);
  }
});

document.addEventListener('click', event => {
  if (!metaUi.genreOpen || event.target.closest('[data-meta-genre-filter]')) return;
  metaUi.genreOpen = false;
  const wrapper = document.querySelector('[data-meta-genre-filter]');
  if (wrapper) wrapper.classList.remove('open');
});

extensions.keyHandlers.push(event => {
  if (event.key !== 'Escape' || !metaUi.genreOpen) return false;
  metaUi.genreOpen = false;
  const wrapper = document.querySelector('[data-meta-genre-filter]');
  if (wrapper) wrapper.classList.remove('open');
  return true;
});

function metaStatusText() {
  const status = metaUi.status;
  if (status && status.phase && status.phase !== 'idle' && META_PHASES[status.phase]) {
    return `${META_PHASES[status.phase]}${status.total ? ` · ${formatCount(Math.min(status.done + 1, status.total))} of ${formatCount(status.total)}` : ''}`;
  }
  const known = Object.values(state.meta).filter(meta => meta && (meta.Description || (meta.Genres && meta.Genres.length))).length;
  return known ? `Store info saved for ${formatCount(known)} ${known === 1 ? 'game' : 'games'}. It refreshes every two weeks.` : 'Store info loads in the background when the library opens.';
}

function switchHtml(key, fallback) {
  return `<label class="meta-switch"><input type="checkbox" data-meta-setting="${key}" ${setting(key, fallback) ? 'checked' : ''}><span class="meta-switch-track"><span class="meta-switch-thumb"></span></span></label>`;
}

function fillHtml() {
  const fill = metaUi.fill;
  if (fill && fill.running) {
    const percent = fill.total ? Math.round((fill.done / fill.total) * 100) : 0;
    return `
      <div class="meta-fill-progress">
        <div class="meta-fill-bar"><i style="width:${percent}%"></i></div>
        <div class="meta-fill-line">
          <span>${formatCount(fill.done)} of ${formatCount(fill.total)}${fill.current ? ` · <strong>${escapeHtml(fill.current)}</strong>` : ''}</span>
          <button class="text-button" data-meta-fill-cancel>Cancel</button>
        </div>
      </div>`;
  }
  if (!fill) return '';
  const missed = fill.missed || [];
  const summary = fill.total
    ? `Added art for ${formatCount(fill.applied)} of ${formatCount(fill.total)} ${fill.total === 1 ? 'game' : 'games'}${fill.cancelled ? ' before you cancelled' : ''}.`
    : 'Every game already has artwork.';
  return `
    <div class="meta-fill-result">
      <span>${summary}</span>
      ${missed.length ? `<button class="link-button" data-meta-fill-missed>${metaUi.showMissed ? 'Hide' : 'Show'} ${formatCount(missed.length)} without a match</button>` : ''}
    </div>
    ${missed.length && metaUi.showMissed ? `<ul class="meta-missed">${missed.map(name => `<li>${escapeHtml(name)}</li>`).join('')}</ul>` : ''}`;
}

function metaSettingsInner() {
  const running = metaUi.fill && metaUi.fill.running;
  const busy = metaUi.status && metaUi.status.phase && metaUi.status.phase !== 'idle';
  return `
    <div class="meta-setting">
      <div class="meta-setting-text"><strong>Animated backgrounds</strong><span>Play a muted trailer loop behind the game page header.</span></div>
      ${switchHtml(META_SETTINGS.animated, true)}
    </div>
    <div class="meta-setting">
      <div class="meta-setting-text"><strong>Trailer previews on hover</strong><span>Rest the pointer on a cover to play a short muted clip.</span></div>
      ${switchHtml(META_SETTINGS.hover, true)}
    </div>
    <div class="meta-setting">
      <div class="meta-setting-text"><strong>Store info</strong><span data-meta-status>${escapeHtml(metaStatusText())}</span></div>
      <button class="secondary-button compact ${busy ? 'busy' : ''}" data-meta-refresh>${ICON.refresh}Refresh metadata</button>
    </div>
    <div class="meta-setting stacked">
      <div class="meta-setting-row">
        <div class="meta-setting-text"><strong>Missing artwork</strong><span>Find covers, backgrounds and logos for games without them. Only exact name matches are used.</span></div>
        <button class="secondary-button compact" data-meta-fill ${running ? 'disabled' : ''}>${ICON.wand}Fill missing art</button>
      </div>
      ${fillHtml()}
    </div>
    <div class="meta-setting">
      <div class="meta-setting-text"><strong>Fill backgrounds and logos automatically</strong><span>${state.hasGridKey ? 'Uses SteamGridDB for installed games, exact name matches only.' : 'Needs a SteamGridDB key. Add one from Change art on any game.'}</span></div>
      ${switchHtml(META_SETTINGS.autoArt, true)}
    </div>`;
}

function renderMetaSettings() {
  for (const root of document.querySelectorAll('[data-meta-settings]')) root.innerHTML = metaSettingsInner();
}

extensions.settingsSections.push({
  id: 'meta',
  title: 'Game info',
  render: () => `<div class="meta-settings" data-meta-settings>${metaSettingsInner()}</div>`,
  bind: () => {},
});

document.addEventListener('change', event => {
  const input = event.target.closest && event.target.closest('[data-meta-setting]');
  if (!input) return;
  setSetting(input.dataset.metaSetting, input.checked);
  if (input.dataset.metaSetting === META_SETTINGS.animated && !input.checked) removeHeroVideo();
  if (input.dataset.metaSetting === META_SETTINGS.hover && !input.checked) stopHoverPreview();
});

document.addEventListener('click', event => {
  if (!event.target.closest || !event.target.closest('[data-meta-settings]')) return;
  if (event.target.closest('[data-meta-refresh]')) {
    metaUi.requested.clear();
    send({ type: 'metaRefresh' });
  } else if (event.target.closest('[data-meta-fill]')) {
    metaUi.fillStartedHere = true;
    metaUi.showMissed = false;
    metaUi.fill = { running: true, done: 0, total: 0, missed: [] };
    send({ type: 'metaFillArt', kinds: ['cover', 'hero', 'logo'] });
    renderMetaSettings();
  } else if (event.target.closest('[data-meta-fill-cancel]')) {
    send({ type: 'metaFillCancel' });
  } else if (event.target.closest('[data-meta-fill-missed]')) {
    metaUi.showMissed = !metaUi.showMissed;
    renderMetaSettings();
  }
});

function showFillSummary(fill) {
  if (typeof modal === 'undefined') return;
  const missed = fill.missed || [];
  modal.open(`
    <header class="modal-head">
      <h2>Missing artwork</h2>
      <button class="icon-button large" data-modal-close title="Close">${ICON.close}</button>
    </header>
    <div class="meta-fill-summary">
      <div class="meta-fill-numbers">
        <div class="stat"><div class="stat-label">Games checked</div><div class="stat-value">${formatCount(fill.total)}</div></div>
        <div class="stat"><div class="stat-label">Art added</div><div class="stat-value">${formatCount(fill.applied)}</div></div>
        <div class="stat"><div class="stat-label">No exact match</div><div class="stat-value ${missed.length ? '' : 'muted'}">${formatCount(missed.length)}</div></div>
      </div>
      ${missed.length ? `
        <h3 class="section-title">No exact match</h3>
        <ul class="meta-missed">${missed.map(name => `<li>${escapeHtml(name)}</li>`).join('')}</ul>
        <div class="callout neutral">${ICON.info}<div>Open one of these games and use <strong>Change art</strong> to search by a different name.</div></div>` : ''}
    </div>
    <footer class="modal-foot">
      <span class="grow"></span>
      <button class="primary-button compact" data-modal-close>Done</button>
    </footer>`, 'meta-fill-modal');
}

function scheduleMetaRefresh() {
  if (metaUi.refreshTimer) return;
  metaUi.refreshTimer = setTimeout(() => {
    metaUi.refreshTimer = null;
    const dependsOnMeta = state.prefs.genre || (extensions.sortComparers && extensions.sortComparers[state.prefs.sort]) || (state.prefs.groupBy && extensions.groupers && extensions.groupers[state.prefs.groupBy]);
    if (dependsOnMeta && !metaUi.hover.video) scheduleRender(0);
    else if (typeof renderHeadline === 'function' && state.view !== 'stores' && !extensions.views[state.view]) renderHeadline(state.visible);
    const status = document.querySelector('[data-meta-status]');
    if (status) status.textContent = metaStatusText();
  }, 1500);
}

extensions.libraryHandlers.push(message => {
  const extras = message.extras || {};
  const incoming = extras.meta || {};
  const next = {};
  for (const [id, meta] of Object.entries(incoming)) next[id] = mergeMeta(state.meta[id], meta);
  state.meta = next;
  if (extras.metaStatus) metaUi.status = extras.metaStatus;
});

extensions.messageHandlers.push(message => {
  switch (message.type) {
    case 'meta': {
      state.meta[message.id] = mergeMeta(state.meta[message.id], message.meta);
      if (message.meta && message.meta.Complete) metaUi.loading.delete(message.id);
      if (message.id === metaUi.drawer.id && metaUi.drawer.about) {
        const game = state.byId.get(message.id);
        if (game) {
          updateDrawerMeta(game);
          mountHeroVideo(game);
        }
      }
      scheduleMetaRefresh();
      break;
    }

    case 'metaLoaded': {
      metaUi.loading.delete(message.id);
      const game = state.byId.get(message.id);
      if (game && message.id === metaUi.drawer.id && metaUi.drawer.about) updateDrawerMeta(game);
      break;
    }

    case 'metaSearchResults': {
      const search = metaUi.search;
      if (!search || search.requestId !== message.requestId) break;
      search.loading = false;
      search.results = message.results || [];
      search.error = message.error || null;
      const game = state.byId.get(search.id);
      if (game && search.id === metaUi.drawer.id && metaUi.drawer.about) updateDrawerMeta(game);
      break;
    }

    case 'metaStatus': {
      metaUi.status = message;
      const status = document.querySelector('[data-meta-status]');
      if (status) status.textContent = metaStatusText();
      const button = document.querySelector('[data-meta-refresh]');
      if (button) button.classList.toggle('busy', message.phase !== 'idle');
      break;
    }

    case 'metaFill': {
      const wasRunning = metaUi.fill && metaUi.fill.running;
      metaUi.fill = message;
      renderMetaSettings();
      if (wasRunning && !message.running && metaUi.fillStartedHere) {
        metaUi.fillStartedHere = false;
        if (message.cancelled) toast(`Stopped. Added art for ${formatCount(message.applied)} ${message.applied === 1 ? 'game' : 'games'}.`, 'info', ICON.check);
        else if (!message.total) toast('Every game already has artwork', 'info', ICON.check);
        else showFillSummary(message);
      }
      break;
    }

    case 'settings':
      renderMetaSettings();
      if (!setting(META_SETTINGS.animated, true)) removeHeroVideo();
      if (!setting(META_SETTINGS.hover, true)) stopHoverPreview();
      break;

    case 'gridKey':
      renderMetaSettings();
      break;
  }
});

class MetaDashPlayer {
  static supported() {
    return typeof MediaSource !== 'undefined' && typeof DOMParser !== 'undefined';
  }

  constructor(video, url, onFailure) {
    this.video = video;
    this.url = url;
    this.onFailure = onFailure;
    this.tracks = [];
    this.destroyed = false;
    this.failed = false;
    this.source = null;
    this.objectUrl = null;
    this.base = url;
    this.onSeeking = () => this.seek();
  }

  static parseDuration(text) {
    const match = /^P(?:(\d+(?:\.\d+)?)D)?(?:T(?:(\d+(?:\.\d+)?)H)?(?:(\d+(?:\.\d+)?)M)?(?:(\d+(?:\.\d+)?)S)?)?$/.exec(text || '');
    if (!match) return 0;
    const [, days, hours, minutes, seconds] = match.map(value => Number(value || 0));
    return days * 86400 + hours * 3600 + minutes * 60 + seconds;
  }

  static child(element, name) {
    return element ? [...element.children].find(node => node.localName === name) || null : null;
  }

  representations(manifest) {
    const found = [];
    for (const set of manifest.getElementsByTagName('AdaptationSet')) {
      const setMime = set.getAttribute('mimeType') || '';
      const setType = set.getAttribute('contentType') || '';
      const setTemplate = MetaDashPlayer.child(set, 'SegmentTemplate');
      for (const representation of set.getElementsByTagName('Representation')) {
        const mime = representation.getAttribute('mimeType') || setMime;
        const codecs = representation.getAttribute('codecs') || set.getAttribute('codecs');
        const kind = mime.startsWith('video/') || setType === 'video' ? 'video' : mime.startsWith('audio/') || setType === 'audio' ? 'audio' : null;
        const template = MetaDashPlayer.child(representation, 'SegmentTemplate') || setTemplate;
        if (!kind || !codecs || !template || !template.getAttribute('duration') || !template.getAttribute('media')) continue;
        const type = `${mime}; codecs="${codecs}"`;
        if (!MediaSource.isTypeSupported(type)) continue;
        found.push({
          kind,
          type,
          id: representation.getAttribute('id') || '',
          bandwidth: Number(representation.getAttribute('bandwidth') || 0),
          height: Number(representation.getAttribute('height') || 0),
          timescale: Number(template.getAttribute('timescale') || 1),
          segmentDuration: Number(template.getAttribute('duration')),
          initialization: template.getAttribute('initialization'),
          media: template.getAttribute('media'),
          startNumber: Number(template.getAttribute('startNumber') || 1),
        });
      }
    }
    return found;
  }

  choose(representations) {
    const target = (this.video.clientWidth || 640) * (window.devicePixelRatio || 1) > 1000 ? 1080 : 720;
    const videos = representations.filter(entry => entry.kind === 'video').sort((first, second) => first.height - second.height || first.bandwidth - second.bandwidth);
    const fitting = videos.filter(entry => entry.height <= target);
    const video = fitting.length ? fitting[fitting.length - 1] : videos[0];
    const audio = representations.filter(entry => entry.kind === 'audio').sort((first, second) => second.bandwidth - first.bandwidth)[0];
    return [video, audio].filter(Boolean);
  }

  resolve(template, representation, number) {
    const path = template
      .replace(/\$RepresentationID\$/g, representation.id)
      .replace(/\$Bandwidth\$/g, String(representation.bandwidth))
      .replace(/\$Number(?:%0(\d+)d)?\$/g, (_, width) => String(number).padStart(Number(width || 0), '0'))
      .replace(/\$\$/g, '$');
    return new URL(path, this.base).href;
  }

  async start() {
    const response = await fetch(this.url);
    if (!response.ok) throw new Error(`Manifest ${response.status}`);
    const manifest = new DOMParser().parseFromString(await response.text(), 'application/xml');
    const root = manifest.documentElement;
    if (!root || root.localName !== 'MPD') throw new Error('Not a DASH manifest');

    const baseUrl = MetaDashPlayer.child(root, 'BaseURL');
    if (baseUrl && baseUrl.textContent.trim()) this.base = new URL(baseUrl.textContent.trim(), this.url).href;
    this.duration = MetaDashPlayer.parseDuration(root.getAttribute('mediaPresentationDuration'));

    const chosen = this.choose(this.representations(manifest));
    if (!chosen.some(entry => entry.kind === 'video')) throw new Error('No playable video');
    if (this.destroyed) return;

    this.source = new MediaSource();
    this.objectUrl = URL.createObjectURL(this.source);
    this.video.src = this.objectUrl;
    await new Promise(resolve => this.source.addEventListener('sourceopen', resolve, { once: true }));
    if (this.destroyed) return;
    if (this.duration) this.source.duration = this.duration;

    for (const representation of chosen) {
      const buffer = this.source.addSourceBuffer(representation.type);
      const count = this.duration ? Math.ceil((this.duration * representation.timescale) / representation.segmentDuration) : Infinity;
      this.tracks.push({ representation, buffer, index: representation.startNumber, last: representation.startNumber + count - 1, running: false });
    }
    await Promise.all(this.tracks.map(async track => this.append(track, await this.download(this.resolve(track.representation.initialization, track.representation, 0)))));
    if (this.destroyed) return;

    this.video.addEventListener('seeking', this.onSeeking);
    for (const track of this.tracks) this.run(track);
  }

  async download(url) {
    for (let attempt = 0; ; attempt++) {
      try {
        const response = await fetch(url);
        if (!response.ok) throw Object.assign(new Error(`HTTP ${response.status}`), { status: response.status });
        return await response.arrayBuffer();
      } catch (error) {
        if (this.destroyed || attempt >= 2 || error.status === 404) throw error;
        await new Promise(resolve => setTimeout(resolve, 700 * (attempt + 1)));
      }
    }
  }

  idle(track) {
    if (!track.buffer.updating) return Promise.resolve();
    return new Promise(resolve => track.buffer.addEventListener('updateend', resolve, { once: true }));
  }

  async append(track, data) {
    await this.idle(track);
    if (this.destroyed) return;
    const attempt = () => new Promise((resolve, reject) => {
      const buffer = track.buffer;
      const finish = () => {
        buffer.removeEventListener('updateend', finish);
        buffer.removeEventListener('error', fail);
        resolve();
      };
      const fail = () => {
        buffer.removeEventListener('updateend', finish);
        buffer.removeEventListener('error', fail);
        reject(new Error('Append failed'));
      };
      buffer.addEventListener('updateend', finish);
      buffer.addEventListener('error', fail);
      try {
        buffer.appendBuffer(data);
      } catch (error) {
        buffer.removeEventListener('updateend', finish);
        buffer.removeEventListener('error', fail);
        reject(error);
      }
    });

    try {
      await attempt();
    } catch (error) {
      if (error.name !== 'QuotaExceededError') throw error;
      const keepFrom = this.video.currentTime - 8;
      if (keepFrom <= 0) throw error;
      track.buffer.remove(0, keepFrom);
      await this.idle(track);
      await attempt();
    }
  }

  ahead(track) {
    const time = this.video.currentTime;
    const ranges = track.buffer.buffered;
    for (let index = 0; index < ranges.length; index++) {
      if (ranges.start(index) <= time + 0.3 && ranges.end(index) > time) return ranges.end(index) - time;
    }
    return 0;
  }

  async run(track) {
    if (track.running) return;
    track.running = true;
    try {
      while (!this.destroyed && track.index <= track.last) {
        if (this.ahead(track) > 30) {
          await new Promise(resolve => setTimeout(resolve, 400));
          continue;
        }
        const index = track.index;
        let data;
        try {
          data = await this.download(this.resolve(track.representation.media, track.representation, index));
        } catch (error) {
          if (error.status === 404 && track.last === Infinity) {
            track.last = index - 1;
            break;
          }
          throw error;
        }
        if (this.destroyed) return;
        await this.append(track, data);
        if (track.index === index) track.index = index + 1;
      }
    } catch (error) {
      if (!this.destroyed && !this.failed) {
        this.failed = true;
        if (this.video.currentTime < 1 && this.onFailure) this.onFailure(error);
      }
      return;
    } finally {
      track.running = false;
    }
    this.finish();
  }

  seek() {
    const time = this.video.currentTime;
    for (const track of this.tracks) {
      let end = time;
      const ranges = track.buffer.buffered;
      for (let index = 0; index < ranges.length; index++) {
        if (ranges.start(index) <= time + 0.3 && ranges.end(index) > time) end = ranges.end(index);
      }
      const segment = Math.floor((end * track.representation.timescale) / track.representation.segmentDuration);
      track.index = Math.min(track.representation.startNumber + segment, track.last + 1);
      if (!track.running && track.index <= track.last) this.run(track);
    }
  }

  finish() {
    if (this.destroyed || !this.source || this.source.readyState !== 'open') return;
    if (this.tracks.some(track => track.running || track.index <= track.last || track.buffer.updating)) return;
    try {
      this.source.endOfStream();
    } catch {
    }
  }

  destroy() {
    this.destroyed = true;
    this.video.removeEventListener('seeking', this.onSeeking);
    if (this.objectUrl) URL.revokeObjectURL(this.objectUrl);
    this.objectUrl = null;
  }
}
