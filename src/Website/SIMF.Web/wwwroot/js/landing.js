/* landing.js — progressive enhancement for the SIMF public landing.
 * Vanilla JS only (no framework). Everything here is non-essential polish:
 * the page is fully readable and navigable with JS disabled.
 *
 *   1. Page-loader fade-out on window load.
 *   2. Reveal-on-scroll for .ln-reveal blocks (IntersectionObserver).
 *   3. Search drop-panel toggle.
 *   5. Stat count-up: participation counters tick from 0 when scrolled in.
 *   6. Theme explorer: vertical tabs switch the visible theme panel.
 *   7. Programme agenda: day strip + type filter.
 * (The hero drift + sponsors marquee are pure CSS now — see landing.css.)
 */
(function () {
  'use strict';

  var prefersReducedMotion = window.matchMedia
    && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  /* Bind-once guard. run() re-fires on every Blazor enhanced navigation (see the
     enhancedload hook at the bottom), so this marks an element as wired: the shared
     header (preserved across enhanced nav) is not wired twice, while swapped page
     content (fresh elements with no mark) still gets wired each time. */
  function firstInit(el, key) {
    if (!el) { return false; }
    // Mark via a JS property, NOT a data-* attribute. Blazor's enhanced-navigation
    // DOM morph re-syncs a PRESERVED element's attributes back to the server-rendered
    // state (which has no data-lnInit*), so a dataset flag would be stripped and
    // run() would re-wire the shared header on every navigation — stacking duplicate
    // listeners. A JS property survives the morph, so the bind-once guard holds.
    var prop = '_lnInit' + key;
    if (el[prop]) { return false; }
    el[prop] = true;
    return true;
  }

  /* ---- 1. page loader ---------------------------------------------------- */
  function hideLoader() {
    var el = document.getElementById('ln-loader');
    if (el) { el.classList.add('is-gone'); }
  }
  window.addEventListener('load', function () { window.setTimeout(hideLoader, 300); });
  // Safety net: never trap the user behind the splash if 'load' is slow.
  window.setTimeout(hideLoader, 4000);

  /* ---- 2. reveal-on-scroll ---------------------------------------------- */
  function initReveal() {
    var nodes = document.querySelectorAll('.ln-reveal');
    if (!nodes.length) { return; }
    if (prefersReducedMotion || !('IntersectionObserver' in window)) {
      nodes.forEach(function (n) { n.classList.add('is-visible'); });
      return;
    }
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (e.isIntersecting) { e.target.classList.add('is-visible'); io.unobserve(e.target); }
      });
    }, { threshold: 0.12, rootMargin: '0px 0px -8% 0px' });
    nodes.forEach(function (n) { if (firstInit(n, 'Reveal')) { io.observe(n); } });
  }

  /* ---- 3. search drop-panel toggle -------------------------------------- */
  function initSearch() {
    var toggle = document.getElementById('ln-search-toggle');
    var panel = document.getElementById('ln-search-panel');
    if (!toggle || !panel) { return; }
    if (!firstInit(toggle, 'Search')) { return; }
    var closeBtn = panel.querySelector('.ln-search__close');
    function setOpen(open) {
      panel.classList.toggle('is-open', open);
      toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
      if (open) {
        var input = panel.querySelector('input');
        if (input) { input.focus(); }
      }
    }
    toggle.addEventListener('click', function () { setOpen(!panel.classList.contains('is-open')); });
    if (closeBtn) { closeBtn.addEventListener('click', function () { setOpen(false); toggle.focus(); }); }
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && panel.classList.contains('is-open')) { setOpen(false); toggle.focus(); }
    });
  }

  /* ---- 5. stat count-up: numbers tick from 0 when scrolled into view ---- */
  function initCountUp() {
    var nums = document.querySelectorAll('.ln-stat__num');
    if (!nums.length) { return; }
    // Reduced-motion / no-IO: leave the final value rendered as-is.
    if (prefersReducedMotion || !('IntersectionObserver' in window)) { return; }
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (!e.isIntersecting) { return; }
        io.unobserve(e.target);
        animateCount(e.target);
      });
    }, { threshold: 0.6 });
    nums.forEach(function (n) { if (firstInit(n, 'Count')) { io.observe(n); } });
  }

  // Tick el's numeric part from 0 to its target over ~1.3s, preserving any
  // non-digit prefix/suffix ("+500" -> "+0".."+500"); restores the exact
  // original text at the end so formatting is never altered.
  function animateCount(el) {
    var raw = el.textContent.trim();
    var parts = raw.match(/^(\D*)([\d,]+)(.*)$/);
    if (!parts) { return; }
    var prefix = parts[1], suffix = parts[3];
    var target = parseInt(parts[2].replace(/,/g, ''), 10);
    if (!isFinite(target)) { return; }
    var startTs = null, duration = 1300;
    function step(ts) {
      if (startTs === null) { startTs = ts; }
      var p = Math.min((ts - startTs) / duration, 1);
      var eased = 1 - Math.pow(1 - p, 3); // easeOutCubic
      if (p < 1) {
        el.textContent = prefix + Math.round(eased * target).toLocaleString('en-US') + suffix;
        window.requestAnimationFrame(step);
      } else {
        el.textContent = raw; // restore the exact authored string
      }
    }
    window.requestAnimationFrame(step);
  }

  /* ---- 6. theme explorer: vertical tabs switch the visible panel -------- */
  function initThemeTabs() {
    var ex = document.querySelector('.ln-themex');
    if (!ex) { return; }
    if (!firstInit(ex, 'Tabs')) { return; }
    var tabs = ex.querySelectorAll('.ln-themex__tab');
    var panels = ex.querySelectorAll('.ln-themex__panel');
    if (!tabs.length || tabs.length !== panels.length) { return; }
    function activate(i) {
      tabs.forEach(function (t, j) {
        var on = j === i;
        t.classList.toggle('is-active', on);
        t.setAttribute('aria-selected', on ? 'true' : 'false');
      });
      panels.forEach(function (p, j) { p.classList.toggle('is-active', j === i); });
    }
    tabs.forEach(function (t, i) { t.addEventListener('click', function () { activate(i); }); });
    activate(0);
    // Commit to the single-panel view only now the tabs are wired — so if this
    // never runs (JS disabled / failed to load) every panel stays reachable.
    ex.classList.add('is-enhanced');
  }

  /* ---- 7. programme agenda: day strip + type filter (progressive) ------- */
  function initAgenda() {
    var root = document.querySelector('.ln-agenda');
    if (!root) { return; }
    if (!firstInit(root, 'Agenda')) { return; }
    var dayPills = root.querySelectorAll('[data-agenda-day]');
    var dayPanels = root.querySelectorAll('[data-agenda-daypanel]');
    var typeTabs = root.querySelectorAll('[data-agenda-type]');
    var cards = root.querySelectorAll('[data-agenda-cardtype]');
    if (!dayPanels.length) { return; }
    var activeDay = dayPills.length ? dayPills[0].getAttribute('data-agenda-day') : null;
    var activeType = '';
    function apply() {
      dayPills.forEach(function (p) {
        var on = p.getAttribute('data-agenda-day') === activeDay;
        p.classList.toggle('is-active', on);
        p.setAttribute('aria-pressed', on ? 'true' : 'false');
      });
      typeTabs.forEach(function (t) {
        var on = (t.getAttribute('data-agenda-type') || '') === activeType;
        t.classList.toggle('is-active', on);
        t.setAttribute('aria-pressed', on ? 'true' : 'false');
      });
      cards.forEach(function (c) {
        var t = c.getAttribute('data-agenda-cardtype') || '';
        c.classList.toggle('is-hidden', activeType !== '' && t !== activeType);
      });
      // Reflect the active day + flag any day the filter emptied (shows a note).
      dayPanels.forEach(function (p) {
        p.classList.toggle('is-active', p.getAttribute('data-agenda-daypanel') === activeDay);
        var inDay = p.querySelectorAll('[data-agenda-cardtype]');
        var anyVisible = Array.prototype.some.call(inDay, function (c) { return !c.classList.contains('is-hidden'); });
        p.classList.toggle('is-empty', !anyVisible);
      });
    }
    dayPills.forEach(function (p) {
      p.addEventListener('click', function () { activeDay = p.getAttribute('data-agenda-day'); apply(); });
    });
    typeTabs.forEach(function (t) {
      t.addEventListener('click', function () { activeType = t.getAttribute('data-agenda-type') || ''; apply(); });
    });
    apply();
    // Commit to the single-day view only now the controls are wired — so if this
    // never runs (JS disabled / failed), every day + card stays visible.
    root.classList.add('is-enhanced');
  }

  /* ---- 8. floating chat assistant --------------------------------------- */
  function initChatbot() {
    var root = document.getElementById('ln-chat');
    if (!root) { return; }
    if (!firstInit(root, 'Chat')) { return; }
    var launcher = document.getElementById('ln-chat-launcher');
    var panel = document.getElementById('ln-chat-panel');
    var closeBtn = document.getElementById('ln-chat-close');
    var form = document.getElementById('ln-chat-form');
    var input = document.getElementById('ln-chat-input');
    var log = document.getElementById('ln-chat-log');
    if (!launcher || !panel || !form || !input || !log) { return; }

    function setOpen(open) {
      panel.hidden = !open;
      root.classList.toggle('is-open', open);
      launcher.setAttribute('aria-expanded', open ? 'true' : 'false');
      if (open) { input.focus(); }
    }
    launcher.addEventListener('click', function () { setOpen(panel.hidden); });
    if (closeBtn) { closeBtn.addEventListener('click', function () { setOpen(false); launcher.focus(); }); }
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && !panel.hidden) { setOpen(false); launcher.focus(); }
    });

    function addMsg(text, who) {
      var el = document.createElement('div');
      el.className = 'ln-chat__msg ln-chat__msg--' + who;
      el.textContent = text;
      log.appendChild(el);
      log.scrollTop = log.scrollHeight;
      return el;
    }
    function unavailable() {
      return document.documentElement.lang === 'ar'
        ? 'تعذّر الوصول إلى المساعد الآن. حاول لاحقاً.'
        : 'The assistant is unavailable right now. Please try again.';
    }

    var busy = false;
    form.addEventListener('submit', function (e) {
      e.preventDefault();
      var q = input.value.trim();
      if (!q || busy) { return; }
      addMsg(q, 'user');
      input.value = '';
      busy = true;
      var typing = addMsg('…', 'ai');
      typing.classList.add('ln-chat__msg--typing');
      window.fetch('/chat/ask', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ question: q })
      }).then(function (r) { return r.ok ? r.json() : null; })
        .then(function (data) {
          typing.remove();
          addMsg((data && data.answer) || unavailable(), 'ai');
        })
        .catch(function () { typing.remove(); addMsg(unavailable(), 'ai'); })
        .finally(function () { busy = false; input.focus(); });
    });
  }

  /* ---- 9. nav mega-menu: dismiss on click ------------------------------- */
  function initDropdowns() {
    var dds = document.querySelectorAll('.ln-dd');
    if (!dds.length) { return; }
    dds.forEach(function (dd) {
      if (!firstInit(dd, 'Dd')) { return; }
      var toggle = dd.querySelector('.ln-nav__item');
      // A pointer press must not latch the panel open via :focus-within (that
      // leaves it stuck open until you click elsewhere). preventDefault on
      // mousedown stops the click focusing the toggle; hover and keyboard focus
      // still open the panel.
      if (toggle) {
        toggle.addEventListener('mousedown', function (e) { e.preventDefault(); });
      }
      // Clicking a menu item navigates — dismiss the panel (drop focus + a class
      // that overrides :hover) so it does not linger open on the destination page.
      dd.addEventListener('click', function (e) {
        if (e.target.closest('.ln-dd__link')) {
          dd.classList.add('is-dismissed');
          if (document.activeElement && document.activeElement.blur) { document.activeElement.blur(); }
        }
      });
      // Re-enable opening once the pointer leaves OR focus (re-)enters the menu.
      // The focusin path matters for keyboard users: an item is a same-page anchor,
      // so mouseleave may never fire — without this, tabbing back could not re-open
      // the panel (is-dismissed would override :focus-within indefinitely).
      dd.addEventListener('mouseleave', function () { dd.classList.remove('is-dismissed'); });
      dd.addEventListener('focusin', function () { dd.classList.remove('is-dismissed'); });
    });
  }

  function run() { initReveal(); initSearch(); initCountUp(); initThemeTabs(); initAgenda(); initChatbot(); initDropdowns(); }
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', run);
  } else {
    run();
  }

  /* Blazor enhanced navigation swaps the page DOM WITHOUT a full reload, so
     DOMContentLoaded never fires again. Without re-running, a navigated-to page
     keeps its .ln-reveal blocks hidden (opacity:0) and its interactions unwired
     until a manual reload. Re-run after every enhanced load; firstInit() keeps the
     preserved shared header from being wired twice. */
  // Scroll to the element named by location.hash. A full page load scrolls to a
  // #fragment natively, but enhanced navigation (below) does NOT — so a
  // "/archive#ed-1" click from the nav lands at the page top without this.
  function scrollToHash() {
    if (!location.hash) { return; }
    var el = document.getElementById(decodeURIComponent(location.hash.slice(1)));
    if (el) { el.scrollIntoView(); }
  }

  function onEnhancedLoad() {
    // Enhanced nav also morphs a fresh #ln-loader (without is-gone) back into the
    // DOM, and window 'load' never refires — so without this the splash covers the
    // navigated-to page at z-index 99999 and blocks all interaction. Hide it
    // instantly (no fade on a client-side nav), then re-wire the page interactions.
    var loader = document.getElementById('ln-loader');
    if (loader) { loader.style.transition = 'none'; loader.classList.add('is-gone'); }
    run();
    // Enhanced nav does not honour the URL #fragment; do it after the morph (a
    // short delay so it wins over Blazor's own scroll reset).
    if (location.hash) { window.setTimeout(scrollToHash, 60); }
  }

  var enhancedHooked = false;
  function hookEnhancedNav() {
    if (enhancedHooked) { return true; }
    if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
      window.Blazor.addEventListener('enhancedload', onEnhancedLoad);
      enhancedHooked = true;
      return true;
    }
    return false;
  }
  // blazor.web.js may load after this script, so try now and again once it is up.
  if (!hookEnhancedNav()) {
    document.addEventListener('DOMContentLoaded', hookEnhancedNav);
    window.addEventListener('load', hookEnhancedNav);
  }
})();
