/* landing.js — progressive enhancement for the SIMF public landing.
 * Vanilla JS only (no framework). Everything here is non-essential polish:
 * the page is fully readable and navigable with JS disabled.
 *
 *   1. Page-loader fade-out on window load.
 *   2. Reveal-on-scroll for .ln-reveal blocks (IntersectionObserver).
 *   3. Search drop-panel toggle.
 *   4. Themes crossfade + active card (timer gated to the section's viewport).
 *   5. Hero video paused while off-screen (saves decode CPU/battery).
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

  /* ---- 4. themes crossfade + active card -------------------------------- */
  // The auto-rotate timer only runs while the Themes section is on-screen, so
  // it never wakes to recalc off-screen crossfades for the rest of the page.
  function initThemes() {
    var section = document.querySelector('.ln-themes');
    var cards = document.querySelectorAll('.ln-tcard');
    var bgs = document.querySelectorAll('.ln-themes__bgimg');
    var bgWrap = document.querySelector('.ln-themes__bg');
    if (!section || !cards.length) { return; }
    var idx = 0, timer = null, hovered = false;
    function activate(i) {
      idx = i;
      for (var j = 0; j < cards.length; j++) { cards[j].classList.toggle('is-active', j === i); }
      for (var k = 0; k < bgs.length; k++) { bgs[k].classList.toggle('is-active', k === i); }
      if (bgWrap) { bgWrap.classList.add('is-on'); }
    }
    function stop() { if (timer) { window.clearInterval(timer); timer = null; } }
    function start() {
      if (prefersReducedMotion || hovered || timer) { return; }
      timer = window.setInterval(function () { activate((idx + 1) % cards.length); }, 4000);
    }
    for (var i = 0; i < cards.length; i++) {
      (function (n) {
        cards[n].addEventListener('mouseenter', function () { hovered = true; activate(n); stop(); });
        cards[n].addEventListener('mouseleave', function () { hovered = false; start(); });
        cards[n].addEventListener('focusin', function () { activate(n); stop(); });
        cards[n].addEventListener('focusout', function () { start(); });
      })(i);
    }
    activate(0);
    if ('IntersectionObserver' in window) {
      var io = new IntersectionObserver(function (entries) {
        entries.forEach(function (e) { if (e.isIntersecting) { start(); } else { stop(); } });
      }, { threshold: 0.2 });
      io.observe(section);
    } else {
      start();
    }
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

  function run() { initReveal(); initSearch(); initThemes(); initHeroVideo(); }
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', run);
  } else {
    run();
  }
})();
