# E2E test catalogue — Website marketing landing, Bootstrap rebuild (`/landing`)

| | |
|--|--|
| **Page** | [`web/landing-rebuild.md`](../../pages/web/landing-rebuild.md) |
| **Route** | `/landing` (Blazor SSR Razor page; slated to take over `/` at cutover) |
| **Surface** | Website (public, anonymous) |
| **Test runner** | Chrome DevTools MCP + PowerShell driver (tool-agnostic steps) |
| **Auth setup** | None — anonymous. No API needed: the page is static SSR with in-page content models (no `/content/site` dependency in this build). |
| **Last reviewed** | 2026-07-13 |

> **What this page is.** A from-Figma **Bootstrap 5** rebuild of the marketing
> landing, delivered as a **Blazor SSR** Razor page (server-rendered HTML, not
> MudBlazor). Bilingual Arabic RTL / English LTR via the existing `/culture`
> switch; dynamic sections are server-side `@foreach` loops over content models.
> It coexists with the static `/` landing until cutover.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WLB-001 | Golden — `/landing` returns 200 and renders all sections (hero, intro, stats, about, milestones, hero2, themes, sessions, speakers, partners, sponsors, news, discover, footer) | happy | P0 | _to author_ |
| E2E-WLB-002 | Arabic RTL — `/culture?culture=ar` sets `<html dir="rtl" lang="ar">`, loads `bootstrap.rtl.min.css`, and renders Arabic strings | i18n | P0 | _to author_ |
| E2E-WLB-003 | English LTR — `/culture?culture=en` sets `dir="ltr"`, loads `bootstrap.min.css`, and renders English strings | i18n | P0 | _to author_ |
| E2E-WLB-004 | No horizontal overflow — `scrollWidth == clientWidth` at 1440 / 1280 / 1024 / 768 / 390 in both languages | responsive | P0 | _to author_ |
| E2E-WLB-005 | Mobile nav — below 1100px the desktop menu is hidden, the hamburger shows, and it opens the Bootstrap offcanvas listing all nav groups | responsive | P0 | _to author_ |
| E2E-WLB-006 | Data loops — participation stats (4), milestones (4), themes (5), sessions (3), partner logos (4), sponsors (8), news (3), discover (6) each render their model rows | happy | P1 | _to author_ |
| E2E-WLB-007 | Themes interaction — hovering a pillar activates it (white bg, gold badge) and crossfades its background; auto-rotate advances when the section is on-screen | happy | P1 | _to author_ |
| E2E-WLB-008 | Search panel — the nav search toggle opens the drop-panel, focuses the input, and Escape / close button dismiss it | happy | P1 | _to author_ |
| E2E-WLB-009 | Reduced motion — with `prefers-reduced-motion: reduce`, reveal blocks are visible immediately, marquees and theme auto-rotate do not animate | a11y | P1 | _to author_ |
| E2E-WLB-010 | Clean console + assets — no console errors and no failed/404 asset requests on load in either language | resilience | P0 | _to author_ |
| E2E-WLB-011 | Culture persistence — after switching language the choice survives a reload (culture cookie) | i18n | P2 | _to author_ |

## Scenarios

### E2E-WLB-001 — Golden: the landing renders every section

```gherkin
Feature: Bootstrap landing renders end-to-end
  As a public visitor
  I want the full marketing landing to render
  So that I can read about the forum before signing up

Scenario: All sections are present
  When a visitor opens "/landing"
  Then the response is 200 text/html
  And the page shows the hero title "Saudi International Maritime Forum"
  And the participation stats show 4 counters (+500, +40, +100, +220)
  And the themes row shows 5 pillar cards
  And the sessions row shows 3 session cards
  And the news row shows 3 article cards
  And the discover grid shows 6 destination cards
  And the footer shows the important-links, contact, social and legal blocks
```

### E2E-WLB-002 — Arabic RTL

```gherkin
Scenario: Switching to Arabic renders RTL
  Given a visitor is on "/landing"
  When they open "/culture?culture=ar&redirectUri=/landing"
  Then the document has lang="ar" and dir="rtl"
  And the page loads "bootstrap.rtl.min.css"
  And the hero title reads "المُلتقى البحري السعودي الدولي"
  And the nav logo sits on the right and the language toggle ("English") on the left
  And scrollWidth equals clientWidth (no horizontal overflow)
```

### E2E-WLB-005 — Mobile offcanvas nav

```gherkin
Scenario: The mobile hamburger opens the offcanvas
  Given the viewport width is 390px
  And a visitor is on "/landing"
  Then the desktop nav menu is not displayed
  And the hamburger toggler is displayed
  When the visitor activates the hamburger
  Then the offcanvas panel becomes visible
  And it lists the About, Programmes, Speakers, Discover and Archive groups
  When the visitor activates the close button
  Then the offcanvas is dismissed
```

### E2E-WLB-010 — Clean console and assets

```gherkin
Scenario: No console errors or broken assets
  When a visitor opens "/landing" in English and then in Arabic
  Then there are no console errors
  And every image, stylesheet and script request returns a non-error status
  And no <img> is broken (naturalWidth > 0)
```

_Last authored:_ 2026-07-13 by Claude (Bootstrap rebuild E2E catalogue).
