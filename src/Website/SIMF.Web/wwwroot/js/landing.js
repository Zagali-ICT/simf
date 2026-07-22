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
    nodes.forEach(function (n) { io.observe(n); });
  }

  /* ---- 3. search drop-panel toggle -------------------------------------- */
  function initSearch() {
    var toggle = document.getElementById('ln-search-toggle');
    var panel = document.getElementById('ln-search-panel');
    if (!toggle || !panel) { return; }
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
    nums.forEach(function (n) { io.observe(n); });
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

  function run() { initReveal(); initSearch(); initCountUp(); initThemeTabs(); initAgenda(); }
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', run);
  } else {
    run();
  }
})();
