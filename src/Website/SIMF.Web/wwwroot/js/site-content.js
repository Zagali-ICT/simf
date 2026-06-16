/* site-content.js — live-data hydration for the public pages.
 *
 * This is the new design's equivalent of the old landing's remote-content hook
 * (content.js -> loadSiteContentRemote). It fetches the SAME same-origin feed,
 * GET /content/site, which the Website server builds from the backend API via
 * SimfPublicClient (see Endpoints/SiteContentEndpoints.cs):
 *
 *     browser  ──fetch('/content/site')──►  Website (BFF proxy)
 *                                              └─ SimfPublicClient ─► SIMF.Api (anonymous public reads)
 *
 * Progressive enhancement: the hand-authored static markup is the fallback. A
 * field/section is replaced ONLY when the feed actually carries it, so an empty
 * or unreachable API (the feed yields {} or a 500) leaves the designed content
 * exactly as shipped. The page is Arabic-first, so we read the bare
 * (Arabic-preferred) fields; the feed also emits `<field>_en` for later i18n.
 *
 * Sections WITHOUT an API source stay static by design: the 5 "المحاور" theme
 * cards (themes are admin-auth only — no public endpoint), the "أكتشف السعودية"
 * discover grid (no backend concept), the milestones timeline, the threat-stat
 * marquee, and the partner-band government logos.
 */
(function () {
  'use strict';

  function esc(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }
  function pick(o, k) { var v = o && o[k]; return (v == null) ? '' : String(v); }
  function nonEmpty(a) { return Array.isArray(a) && a.length > 0; }

  // Set an element's text only when both the element and a non-empty value exist,
  // so a missing CMS key never blanks a designed field.
  function setText(selOrEl, value) {
    var el = (typeof selOrEl === 'string') ? document.querySelector(selOrEl) : selOrEl;
    var v = (value == null) ? '' : String(value).trim();
    if (el && v) { el.textContent = v; }
  }

  function fetchSite() {
    return fetch('/content/site', { headers: { Accept: 'application/json' } })
      .then(function (r) { return r.ok ? r.json() : null; })
      .catch(function () { return null; });
  }

  /* ---- Hero (CMS) -------------------------------------------------------- */
  // The brand <h1> has no CMS key, so it stays static; the subtitle carries the
  // edition theme (titleStart + titleHighlight + titleEnd).
  function hydrateHero(hero) {
    if (!hero) { return; }
    var theme = (pick(hero, 'titleStart') + ' ' + pick(hero, 'titleHighlight')
      + ' ' + pick(hero, 'titleEnd')).replace(/\s+/g, ' ').trim();
    setText('.hero__subtitle', theme);
  }

  /* ---- Sub-nav strip (date + venue from the hero CMS) -------------------- */
  function hydrateSubnav(hero) {
    if (!hero) { return; }
    var items = document.querySelectorAll('.subnav__item');
    // order: [0] weather, [1] date, [2] time, [3] venue
    if (items[1]) { setText(items[1].querySelector('span'), pick(hero, 'metaDate')); }
    if (items[3]) { setText(items[3].querySelector('span'), pick(hero, 'metaVenue')); }
  }

  /* ---- Speakers-page hero banner (date + venue from the hero CMS) -------- */
  function hydrateSpeakerHero(hero) {
    if (!hero) { return; }
    var items = document.querySelectorAll('.spkhero__meta .shi');
    // order: [0] date, [1] time, [2] venue
    if (items[0]) { setText(items[0].querySelector('span'), pick(hero, 'metaDate')); }
    if (items[2]) { setText(items[2].querySelector('span'), pick(hero, 'metaVenue')); }
  }

  /* ---- Intro lead (global-landscape narrative from the stats CMS) -------- */
  function hydrateIntro(stats) {
    if (!stats) { return; }
    setText('.intro__lead', pick(stats, 'intro'));
  }

  /* ---- Participation counters (stats CMS count1..4 / label1..4) --------- */
  function hydrateStats(stats) {
    if (!stats) { return; }
    var cells = document.querySelectorAll('.stats__band .stat');
    for (var i = 0; i < cells.length && i < 4; i++) {
      var count = pick(stats, 'count' + (i + 1));
      var label = pick(stats, 'label' + (i + 1));
      if (count) { setText(cells[i].querySelector('.stat__num'), '+' + count); }
      setText(cells[i].querySelector('.stat__label'), label);
    }
  }

  /* ---- About (CMS) ------------------------------------------------------- */
  function hydrateAbout(about) {
    if (!about) { return; }
    setText('.about__eyebrow', pick(about, 'eyebrow'));
    setText('.about__title', pick(about, 'h2'));
    var lead = [pick(about, 'p1'), pick(about, 'p2')].filter(Boolean).join(' ');
    setText('.about__lead', lead);
  }

  /* ---- Themes / pillars section header (CMS — cards have no public source) */
  function hydratePillars(pillars) {
    if (!pillars) { return; }
    setText('.themes__title', pick(pillars, 'h2'));
    setText('.themes__sub', pick(pillars, 'p'));
  }

  /* ---- Main sessions ----------------------------------------------------- */
  // Keep the designed session photos (the feed only carries a neutral
  // placeholder); cycle the local images by index and bind only the text.
  var SESSION_IMGS = [
    'assets/figma/sessions/session-card-1.jpg',
    'assets/figma/sessions/session-card-2.jpg',
    'assets/figma/sessions/session-card-3.jpg'
  ];
  function hydrateSessions(rows) {
    var host = document.querySelector('.sessions__row');
    if (!host || !nonEmpty(rows)) { return; }
    host.innerHTML = rows.map(function (s, i) {
      var img = SESSION_IMGS[i % SESSION_IMGS.length];
      var num = (pick(s, 'n').replace(/^0+/, '')) || String(i + 1);
      var tag = pick(s, 'tag') || ('الجلسة ' + (i + 1));
      return '<article class="scard">'
        + '<div class="scard__img" style="background-image:url(\'' + img + '\')"></div>'
        + '<div class="scard__body">'
        + '<div class="scard__badgerow"><span class="scard__num"><span>' + esc(num) + '</span></span>'
        + '<span class="scard__tag">' + esc(tag) + '</span></div>'
        + '<div class="scard__content">'
        + '<h3 class="scard__title">' + esc(pick(s, 'title')) + '</h3>'
        + '<p class="scard__text">' + esc(pick(s, 'desc')) + '</p></div>'
        + '<div class="scard__actions"><button type="button" class="scard__btn">قراءة المزيد عن الجلسة</button></div>'
        + '</div></article>';
    }).join('');
  }

  /* ---- News & articles --------------------------------------------------- */
  var NEWS_IMGS = [
    'assets/figma/news/news-1.jpg',
    'assets/figma/news/news-2.jpg',
    'assets/figma/news/news-3.jpg'
  ];
  function hydrateNews(rows) {
    var host = document.querySelector('.news__cards');
    if (!host || !nonEmpty(rows)) { return; }
    host.innerHTML = rows.slice(0, 3).map(function (n, i) {
      var img = NEWS_IMGS[i % NEWS_IMGS.length];
      return '<article class="ncard">'
        + '<div class="ncard__img" style="background-image:url(\'' + img + '\')"></div>'
        + '<div class="ncard__body"><div class="ncard__content">'
        + '<span class="ncard__date">' + esc(pick(n, 'date')) + '</span>'
        + '<h3 class="ncard__title">' + esc(pick(n, 'title')) + '</h3>'
        + '<p class="ncard__excerpt">' + esc(pick(n, 'excerpt')) + '</p></div>'
        + '<div class="ncard__actions"><button type="button" class="ncard__btn">قراءة المزيد</button></div>'
        + '</div></article>';
    }).join('');
  }

  /* ---- Partners / sponsors (marquee) ------------------------------------- */
  function hydratePartners(rows) {
    var track = document.getElementById('spon-track');
    if (!track || !nonEmpty(rows)) { return; }
    function card(p) {
      var logo = pick(p, 'logo') || 'assets/figma/sponsors/sponsor-1.svg';
      var name = pick(p, 'name') || 'شريك';
      var type = pick(p, 'type') || 'شريك';
      return '<div class="scard2"><img class="scard2__icon" src="assets/figma/sponsors/icon-external-link.svg" alt="">'
        + '<div class="scard2__col"><img class="scard2__logo" src="' + esc(logo) + '" alt="' + esc(name) + '">'
        + '<span class="scard2__tag">' + esc(type) + '</span></div></div>';
    }
    var one = rows.map(card).join('');
    track.innerHTML = one + one; // duplicate for the seamless CSS marquee loop
  }

  /* ---- Speakers (speakers.html grids) ------------------------------------ */
  var PIN_SVG = '<svg viewBox="0 0 17.5 21.5" fill="none" aria-hidden="true">'
    + '<circle cx="8.75" cy="8.75" r="3" stroke="currentColor" stroke-width="1.5"/>'
    + '<path d="M8.75 20.75C12.75 16.75 16.75 13.1683 16.75 8.75C16.75 4.33172 13.1683 0.75 8.75 0.75C4.33172 0.75 0.75 4.33172 0.75 8.75C0.75 13.1683 4.75 16.75 8.75 20.75Z" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>';
  function speakerPhoto(p, i) {
    return pick(p, 'photo') || ('assets/figma/speakers-page/speaker-' + ((i % 12) + 1) + '.jpg');
  }
  function speakerCard(p, i) {
    var loc = pick(p, 'org') || 'المملكة العربية السعودية';
    return '<article class="spkcard"><div class="spkcard__photo"><img src="'
      + esc(speakerPhoto(p, i)) + '" alt="' + esc(pick(p, 'name')) + '"></div>'
      + '<h3 class="spkcard__name">' + esc(pick(p, 'name')) + '</h3>'
      + '<div class="spkcard__role">' + esc(pick(p, 'role')) + '</div>'
      + '<div class="spkcard__loc">' + PIN_SVG + '<span>' + esc(loc) + '</span></div></article>';
  }
  function hydrateSpeakers(rows) {
    if (!nonEmpty(rows)) { return; }
    var html = rows.map(speakerCard).join('');
    ['spk-grid-1', 'spk-grid-2'].forEach(function (id) {
      var g = document.getElementById(id);
      if (g) { g.innerHTML = html; }
    });
  }

  /* ---- run --------------------------------------------------------------- */
  function run() {
    fetchSite().then(function (data) {
      if (!data) { return; } // unreachable/empty feed -> keep the static design
      hydrateHero(data.hero);
      hydrateSubnav(data.hero);
      hydrateSpeakerHero(data.hero);
      hydrateIntro(data.stats);
      hydrateStats(data.stats);
      hydrateAbout(data.about);
      hydratePillars(data.pillars);
      hydrateSessions(data.sessions);
      hydrateNews(data.news);
      hydratePartners(data.partners);
      hydrateSpeakers(data.speakers);
    });
  }
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', run);
  } else {
    run();
  }
})();
