# E2E test catalogue — Website "Key themes" (`/about/themes`)

| | |
|--|--|
| **Page** | [`web/themes.md`](../../pages/web/themes.md) |
| **Route** | `/about/themes` |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Test runner** | Chrome DevTools MCP (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | **None — anonymous and static.** No API call, no bearer token, no seeding. |
| **Figma** | KSA Maritime Forum — Key Themes (Desktop AR), node `5865-35289` (hero `5865:35291`; explorer `5963:39940`) |
| **Last reviewed** | 2026-07-15 |

> **What this page is.** `/about/themes` (`Themes.razor` + `.razor.cs`) is the
> Website's public, anonymous **key-themes overview** (Figma `5865-35289`) — the
> third About-cluster page. Static SSR on the shared `LandingShell` chrome, with
> ONE interactive widget: a **theme explorer**. Two sections:
> 1. **Interior hero** (`ln-pghero`, via `LandingPageHero`) — photo + gradient,
>    a **3-level breadcrumb** (Home / About / Key themes), the single `<h1>`, a
>    subtitle and the venue + date pills.
> 2. **Theme explorer** (`ln-fsection` → `ln-themex`) — a title + subtitle, then an
>    image beside five theme panels and a vertical tab list (Theme 1–5). The five
>    themes' title + description **reuse `Landing.Themes`**; the narrow tabs use
>    ordinal labels. Selecting a tab shows that theme's panel (gold selector bar on
>    the active tab).
>
> **Progressive enhancement.** WITHOUT JavaScript, every theme panel renders
> stacked (content never hidden) and the tab list is hidden. WITH JavaScript
> (`landing.js` → `initThemeTabs`, gated by the `.ln-js` class), the tabs show and
> drive which single panel is visible; the first is active by default.
>
> **Auth model (Website, anonymous).** No `RequirePermission`, no `/not-permitted`,
> no `/login` redirect.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WTHM-001 | Golden path — hero + explorer render (image, 5 panels, 5 tabs, first active) + shared chrome, one `<h1>` | happy | P0 | _to author_ |
| E2E-WTHM-002 | Hero 3-level breadcrumb — Home / About / Key themes, the "About" level links to `/about` | happy | P1 | _to author_ |
| E2E-WTHM-003 | Tab interaction (JS) — clicking tab N shows panel N (its `Landing.Themes[N]` title + desc), moves the gold selector bar, updates `aria-selected` | interaction | P0 | _to author_ |
| E2E-WTHM-004 | No-JS fallback — with JS disabled all five panels are visible (stacked) and the tab list is hidden; content is never lost | resilience | P0 | _to author_ |
| E2E-WTHM-005 | Content reuse — the five panels render the landing's `Landing.Themes` wording; the tabs render ordinal labels (Theme 1 … Theme 5) | happy | P1 | _to author_ |
| E2E-WTHM-006 | Static/anonymous — no `/api/...` request, no Authorization header, no `/login` or `/not-permitted` redirect | auth | P0 | _to author_ |
| E2E-WTHM-007 | RTL / Arabic — mirrors right-to-left, image on the LEFT, tabs on the RIGHT with the gold bar on their inline-start (right) edge, Arabic theme text | i18n | P0 | _to author_ |
| E2E-WTHM-008 | LTR / English mirror — image on the RIGHT, tabs on the LEFT with the gold bar on their inline-start (left) edge; breadcrumb "Home / About / Key themes" | i18n | P1 | _to author_ |
| E2E-WTHM-009 | Responsive — below 860px the explorer stacks + tabs wrap horizontally; no horizontal overflow at 1440/1024/768/390 in both languages | responsive | P1 | _to author_ |

## Scenarios

### E2E-WTHM-001 — Golden path

```gherkin
Feature: Website Key-themes page renders the forum's five strategic themes
  As any visitor (anonymous or signed in)
  I want to browse the forum's key themes
  So that I understand what the forum will discuss

Background:
  Given the Website is reachable
  And the browser is a fresh anonymous session (no auth cookie, no bearer token)

Scenario: The page renders the hero + the theme explorer
  When the browser opens /about/themes
  Then NO request to /api/... is made (the page is static)
  And the shared header + footer render (LandingShell chrome)
  And an interior hero (section.ln-pghero) renders with exactly one <h1>
  And a theme explorer (.ln-themex) renders one image, five .ln-themex__panel and five .ln-themex__tab
  And (with JS enabled) exactly one .ln-themex__tab.is-active and one .ln-themex__panel.is-active — the first
  And the page title is "Key themes — Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-themes-ar-1440.png` (AR) + `web-themes-en-1440.png` (EN)
- Console errors: 0 expected
- Network: no `/api/v1/...` request; the explorer image (assets/figma/themes/theme-explorer.jpg) returns 200
- Audit row: none

### E2E-WTHM-002 — Hero 3-level breadcrumb

```gherkin
Scenario: The hero breadcrumb has three levels with a working parent link
  When the browser opens /about/themes
  Then the .ln-pghero__crumbs breadcrumb reads "Home / About / Key themes"
  And the "About" crumb links to "/about" and "Home" links to "/"
  And the current "Key themes" crumb is plain text
```

### E2E-WTHM-003 — Tab interaction (JS on)

```gherkin
Scenario: Selecting a tab switches the visible theme panel
  Given JavaScript is enabled (the .ln-js class is present)
  And the explorer shows Theme 1's panel active by default
  When the user clicks the third tab ("Theme 3" / "المحور الثالث")
  Then the third tab becomes .is-active and its aria-selected becomes "true"
  And the first tab's aria-selected becomes "false"
  And exactly one .ln-themex__panel is visible — the third
  And that panel shows Landing.Themes[2]'s title and description
  And the gold selector bar (.ln-themex__tab.is-active::before) sits on the active tab's inline-start edge
```

### E2E-WTHM-004 — No-JS fallback

```gherkin
Scenario: With JavaScript disabled the explorer degrades to a readable stacked list
  Given the browser has JavaScript disabled (initThemeTabs never runs, so the
      explorer never gains the .is-enhanced class)
  When the user opens /about/themes
  Then ALL five .ln-themex__panel elements are visible (getComputedStyle display != none)
  And the tab list (.ln-themex__tabs) is hidden (display: none)
  And no theme content is lost — every theme's title + description is readable

Scenario: If landing.js fails to load, content is still fully reachable
  Given the inline head script sets .ln-js but landing.js does not execute (e.g. CSP/network)
  When the user opens /about/themes
  Then the explorer never gains .is-enhanced, so all five panels stay visible
  And no themes become unreachable (the single-panel view is keyed on .is-enhanced, not .ln-js)
```

### E2E-WTHM-005 — Content reuse

```gherkin
Scenario: Panels reuse the landing themes; tabs use ordinal labels
  When the browser opens /about/themes under EN
  Then the five .ln-themex__panel titles equal the five Landing.Themes English titles, in order
  And the five .ln-themex__tab labels read "Theme 1", "Theme 2", "Theme 3", "Theme 4", "Theme 5"
  And the "On this page" label (Themes.OnThisPage) renders above the tabs
```

### E2E-WTHM-006 — Static / anonymous

```gherkin
Scenario: The page loads anonymously and fires no API request
  Given a fresh browser with no auth cookie and no bearer token
  When the user opens /about/themes directly
  Then the page renders WITHOUT redirecting to /login or /not-permitted
  And NO request to /api/... is made and no Authorization header is sent

Scenario: A signed-in session changes nothing
  Given an Approved Visitor is signed in on the Website
  When they open /about/themes
  Then the rendered page is identical to the anonymous view
```

### E2E-WTHM-007 — RTL / Arabic render

```gherkin
Scenario: The explorer mirrors right-to-left and renders Arabic
  When the browser opens /about/themes under the Arabic UI culture (<html dir="rtl" lang="ar">)
  Then the page mirrors right-to-left
  And in the explorer the image sits on the LEFT and the tab list on the RIGHT
  And the active tab's gold selector bar sits on its RIGHT (inline-start) edge
  And the breadcrumb reads "الرئيسية / عن الملتقى / المحاور"
  And the active panel renders Arabic theme text (e.g. "التقنيات الحديثة وتأمين قاع البحار وسلاسل الإمداد")
```

### E2E-WTHM-008 — LTR / English mirror

```gherkin
Scenario: The explorer mirrors correctly under English
  When the browser opens /about/themes under the English UI culture (<html dir="ltr" lang="en">)
  Then the image sits on the RIGHT and the tab list on the LEFT
  And the active tab's gold selector bar sits on its LEFT (inline-start) edge
  And the breadcrumb reads "Home / About / Key themes"
```

### E2E-WTHM-009 — Responsive

```gherkin
Scenario: The explorer stacks on narrow viewports with no horizontal overflow
  When the browser opens /about/themes and the viewport width is set to each of 1440, 1024, 768, 390
  Then below 860px the .ln-themex stacks to a single column (image, then panels, then tabs)
  And below 860px the tab list wraps horizontally with the selector bar under the active tab
  And the hero block goes full-width below 720px
  And at every width in {1440, 1024, 768, 390} document.scrollWidth == document.clientWidth (no horizontal overflow)
  And this holds in BOTH the EN (LTR) and AR (RTL) cultures
```

---

## Implementation notes

- **One interactive widget, no CRUD.** The theme explorer's tab switch is the only
  interaction — no form, modal, filter, or API. The matrix above is exhaustive.
- **Progressive enhancement is the contract.** WTHM-004 (no-JS) is a P0 — the page
  must remain fully readable without JavaScript (all panels shown, tabs hidden).
- **Content is single-sourced.** The five theme title/desc pairs come from
  `Landing.Themes`; do not duplicate the theme wording here.
- **Lower-layer coverage:** component (bUnit, no browser)
  `tests/SIMF.Web.Tests/ThemesPageTests.cs` pins the render + the default-active tab +
  the ARIA wiring + the `Landing.Themes` reuse. (bUnit renders without the `.ln-js`
  head script, so it sees the no-JS DOM — all panels present.)
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a `.feature`
  under `tests/SIMF.E2E.Tests/`. The tab-switch + no-JS scenarios need a real browser.

---

_Last reviewed:_ 2026-07-15 by Claude (Key themes page — `ln-` Bootstrap SSR, Figma 5865-35289).
