/* landing.js — progressive enhancement for the SIMF public landing.
 * Vanilla JS only (no framework). Everything here is non-essential polish:
 * the page is fully readable and navigable with JS disabled.
 *
 *   1. Page-loader fade-out on window load.
 *   2. Reveal-on-scroll for .ln-reveal blocks (IntersectionObserver).
 *   3. Search drop-panel toggle.
 *   4. Sponsors carousel: prev/next arrows scroll the (static) sponsor strip.
 *   5. Hero video paused while off-screen (saves decode CPU/battery).
 *   6. Theme explorer: vertical tabs switch the visible theme panel.
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

  /* ---- 4. sponsors carousel: prev/next arrows scroll the strip ---------- */
  function initSponsors() {
    var vp = document.querySelector('.ln-spon__viewport');
    var carousel = document.querySelector('.ln-spon__carousel');
    if (!vp || !carousel) { return; }
    var arrows = carousel.querySelectorAll('.ln-spon__arrow');
    if (arrows.length < 2) { return; }
    var step = 259; // one sponsor card (243px) + gap (16px)
    // carousel is forced LTR (see landing.css): standard scrollLeft, so prev = -step (left/back), next = +step (right/advance).
    arrows[0].addEventListener('click', function () { vp.scrollBy({ left: -step, behavior: 'smooth' }); });
    arrows[1].addEventListener('click', function () { vp.scrollBy({ left: step, behavior: 'smooth' }); });
  }

  /* ---- 5. hero video: pause while off-screen ---------------------------- */
  function initHeroVideo() {
    var video = document.querySelector('.ln-hero__video');
    if (!video || !('IntersectionObserver' in window)) { return; }
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (e.isIntersecting) { var p = video.play(); if (p && p.catch) { p.catch(function () {}); } }
        else { video.pause(); }
      });
    }, { threshold: 0.05 });
    io.observe(video);
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

  function run() { initReveal(); initSearch(); initSponsors(); initHeroVideo(); initThemeTabs(); }
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', run);
  } else {
    run();
  }
})();
