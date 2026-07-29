// SIMF element sweep — the CP/Website half of the QA programme's WS1 element layer.
//
// Run against the CURRENTLY OPEN page via chrome-devtools MCP `evaluate_script`
// (or as the body of a Playwright `page.evaluate` — it is deliberately plain DOM
// with no imports, so the same source serves the manual pass and the CI runner).
//
// It enumerates every interactive control and image on the page and reports:
//   * accessible name — a control a screen reader announces as a bare "button"
//     or "edit text" is unusable, and this is the cheapest place to catch it;
//   * broken images — SIMF images are served through a BFF proxy that 404s when
//     the asset row exists but its bytes do not (the D-687 / BUG-001 class), and
//     SimfImageThumb degrades so gracefully that a broken image is invisible to
//     the eye while firing a failed request per row;
//   * dead / erroring same-origin links and assets.
//
// It reports counts and inventory too, so the caller can diff against the
// predicted inventory (tools/qa/predicted_inventory.py) rather than eyeballing.
//
// ---------------------------------------------------------------------------
// Two defects in the 2026-07-26 version were fixed on 2026-07-28, and both mean
// earlier sweep results must be read with care:
//
//   1. INPUTS WERE NEVER CHECKED. The old loop computed an accessible name for
//      inputs / selects / textareas and then only ever tested buttons and links
//      against it. Its headline "0 unnamed controls" across 79 CP + 29 Website
//      routes therefore says nothing about form fields — the largest class of
//      control on a CRUD admin. Fixed below, with real name resolution
//      (aria-label, aria-labelledby, <label for>, wrapping <label>, title) —
//      NOT `textContent`, which is always empty for an input, and not
//      `placeholder`, which is a value hint and not a name.
//   2. THE LINK CHECK SILENTLY CAPPED AT 80 URLs. A page with more simply had
//      the remainder skipped, and the report still said `pass: true`. The cap is
//      now explicit, configurable, and reported in `capped` so a truncated run
//      can never read as a clean one.
// ---------------------------------------------------------------------------
async (options) => {
  // `options ?? {}` rather than a default parameter: Playwright's evaluate
  // passes NULL when the caller supplies no argument, and a default parameter
  // only applies to `undefined`. With `options = {}` the very first line threw
  // "Cannot read properties of null (reading 'maxUrlChecks')" on every page.
  const settings = options ?? {};
  const MAX_URL_CHECKS = settings.maxUrlChecks ?? 250;

  // --- accessible name -------------------------------------------------------
  // Approximates the accname algorithm well enough for a defect sweep. Order
  // matters: an explicit label wins over a title, and a title over content.
  const labelText = (el) => {
    if (el.id) {
      const forLabel = document.querySelector(`label[for="${CSS.escape(el.id)}"]`);
      if (forLabel) return (forLabel.textContent || '').trim();
    }
    const wrapping = el.closest('label');
    return wrapping ? (wrapping.textContent || '').trim() : '';
  };

  const labelledBy = (el) => {
    const ids = (el.getAttribute('aria-labelledby') || '').split(/\s+/).filter(Boolean);
    return ids
      .map((id) => document.getElementById(id))
      .filter(Boolean)
      .map((node) => (node.textContent || '').trim())
      .join(' ')
      .trim();
  };

  const accessibleName = (el) => {
    const aria = (el.getAttribute('aria-label') || '').trim();
    if (aria) return aria;
    const by = labelledBy(el);
    if (by) return by;
    const label = labelText(el);
    if (label) return label;
    const title = (el.getAttribute('title') || '').trim();
    if (title) return title;
    const alt = (el.getAttribute('alt') || '').trim();
    if (alt) return alt;
    // Content is a valid name source for buttons and links only; for a form
    // field it is always empty, which is exactly how the old bug hid.
    const tag = el.tagName.toLowerCase();
    if (tag === 'button' || tag === 'a' || el.getAttribute('role') === 'button'
      || el.getAttribute('role') === 'link') {
      return (el.textContent || '').trim();
    }
    return '';
  };

  // A control the user cannot reach does not need a name.
  const isHidden = (el) => {
    if (el.getAttribute('aria-hidden') === 'true') return true;
    if (el.hasAttribute('hidden')) return true;
    const style = getComputedStyle(el);
    if (style.display === 'none' || style.visibility === 'hidden') return true;
    return el.getClientRects().length === 0;
  };

  const region = (el) =>
    el.closest('nav') ? 'nav' : (el.closest('main') ? 'main' : 'chrome');

  // A stable-ish handle for reporting, so a violation can be found again.
  const describe = (el) => {
    const id = el.id ? `#${el.id}` : '';
    const cls = (el.className && typeof el.className === 'string')
      ? '.' + el.className.trim().split(/\s+/).slice(0, 3).join('.')
      : '';
    return `${el.tagName.toLowerCase()}${id}${cls}`;
  };

  const report = {
    route: location.pathname + location.search,
    title: document.title,
    dir: document.documentElement.getAttribute('dir') || 'ltr',
    lang: document.documentElement.getAttribute('lang') || '',
    // No horizontal overflow — the responsive gate from the delivery checklist.
    overflowX: document.documentElement.scrollWidth > document.documentElement.clientWidth,
    counts: {
      buttons: 0, links: 0, inputs: 0, selects: 0, textareas: 0, images: 0,
      // How many SimfDataGrids actually rendered. The predicted inventory is
      // derived from the grids in a page's .razor source, but a master-detail
      // page (e.g. /admin/meeting-tables) keeps its grids behind an `@if` on a
      // parent selection, so opening the URL alone renders none of them. Without
      // this count "the grid is behind a precondition" and "the grid regressed
      // away" look identical, and the caller cannot tell a false failure from a
      // real one.
      grids: document.querySelectorAll('.simf-grid').length,
    },
    disabled: { buttons: 0, inputs: 0 },
    inventory: { buttons: [], links: [] },
    problems: { unnamed: [], brokenImages: [], badLinks: [] },
    capped: null,
  };

  // Resolved path -> the raw href/src that produced it. A bad link reported as
  // just "/admin/ 404" sends the reader hunting through the markup; reported as
  // "/admin/ 404 (from href=".")" it names the defect.
  const sameOrigin = new Map();
  const remember = (raw) => {
    try {
      const url = new URL(raw, location.href);
      if (url.origin !== location.origin) return;
      const key = url.pathname + url.search;
      if (!sameOrigin.has(key)) sameOrigin.set(key, raw);
    } catch { /* not a resolvable URL — nothing to check */ }
  };

  const nodes = [...document.querySelectorAll(
    'button, a[href], input, select, textarea, img, [role=button], [role=link]')];

  for (const el of nodes) {
    const tag = el.tagName.toLowerCase();
    const role = el.getAttribute('role');
    const where = region(el);
    const hidden = isHidden(el);

    if (tag === 'img') {
      report.counts.images++;
      const wrapper = el.closest('.simf-img-thumb');
      const broken =
        (wrapper && wrapper.classList.contains('simf-img-thumb--broken'))
        || (el.complete && el.naturalWidth === 0);
      if (broken) {
        report.problems.brokenImages.push({ src: el.getAttribute('src'), region: where });
      }
      if (el.getAttribute('src')) remember(el.getAttribute('src'));
      continue;
    }

    if (tag === 'a' || role === 'link') {
      report.counts.links++;
      const href = el.getAttribute('href');
      if (href && !/^(#|javascript:|mailto:|tel:)/i.test(href)) remember(href);
      const name = accessibleName(el);
      report.inventory.links.push({ name, href });
      if (!name && !hidden && where === 'main') {
        report.problems.unnamed.push({ kind: 'link', el: describe(el), href, region: where });
      }
      continue;
    }

    const type = (el.getAttribute('type') || '').toLowerCase();
    if (tag === 'input' && (type === 'hidden' || type === 'submit' && !el.value)) {
      if (type === 'hidden') continue;
    }

    const isButton = tag === 'button' || role === 'button'
      || (tag === 'input' && ['button', 'submit', 'reset'].includes(type));
    const disabled = el.disabled || el.getAttribute('aria-disabled') === 'true';
    const name = accessibleName(el)
      || (tag === 'input' && ['button', 'submit', 'reset'].includes(type) ? (el.value || '') : '');

    if (isButton) {
      report.counts.buttons++;
      if (disabled) report.disabled.buttons++;
      report.inventory.buttons.push({ name, disabled });
    } else if (tag === 'input') {
      report.counts.inputs++;
      if (disabled) report.disabled.inputs++;
    } else if (tag === 'select') {
      report.counts.selects++;
    } else if (tag === 'textarea') {
      report.counts.textareas++;
    }

    // THE FIX: every control gets the name check, not just buttons. A file
    // input, a search box or a select with no label is exactly as unusable to a
    // screen-reader user as a nameless icon button.
    if (!name && !hidden && where === 'main') {
      report.problems.unnamed.push({
        kind: isButton ? 'button' : tag,
        el: describe(el),
        placeholder: el.getAttribute('placeholder') || undefined,
        region: where,
      });
    }
  }

  // --- same-origin link + asset status --------------------------------------
  const urls = [...sameOrigin.keys()];
  const checked = urls.slice(0, MAX_URL_CHECKS);
  if (urls.length > checked.length) {
    // Explicit, and it fails the sweep: a partial check must never read as clean.
    report.capped = { found: urls.length, checked: checked.length };
  }
  for (const path of checked) {
    const from = sameOrigin.get(path);
    try {
      const response = await fetch(path, { credentials: 'include' });
      if (response.status >= 400) {
        report.problems.badLinks.push({ path, status: response.status, from });
      }
    } catch {
      report.problems.badLinks.push({ path, status: 'ERR', from });
    }
  }

  report.pass =
    report.problems.unnamed.length === 0
    && report.problems.brokenImages.length === 0
    && report.problems.badLinks.length === 0
    && report.capped === null
    && !report.overflowX;

  return report;
}
