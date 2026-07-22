# E2E test catalogue — Website marketing landing, Bootstrap rebuild (`/landing`)

| | |
|--|--|
| **Page** | [`web/landing-rebuild.md`](../../pages/web/landing-rebuild.md) |
| **Route** | `/` (primary, public homepage) + `/landing` (kept) — Blazor SSR Razor page; cutover done 2026-07-14 |
| **Surface** | Website (public, anonymous) |
| **Test runner** | Chrome DevTools MCP + PowerShell driver (tool-agnostic steps) |
| **Auth setup** | None — anonymous. No API needed: the page is static SSR with in-page content models (no `/content/site` dependency in this build). |
| **Last reviewed** | 2026-07-13 |

> **What this page is.** A from-Figma **Bootstrap 5** rebuild of the marketing
> landing, delivered as a **Blazor SSR** Razor page (server-rendered HTML, not
> MudBlazor). Bilingual Arabic RTL / English LTR via the existing `/culture`
> switch; dynamic sections are server-side `@foreach` loops over content models.
> Since the 2026-07-14 cutover it **is** the `/` homepage (the old static
> landing was deleted); `/landing` is kept as an alias — both routes serve the
> same page.

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
| E2E-WLB-012 | Route cutover — `/` returns 200 and serves this landing via the Blazor endpoint (not static-file middleware); `/landing` still returns 200 (same page); the deleted `/index.html` returns 404 | routing | P0 | _to author_ |
| E2E-WLB-013 | Hero background video (D-756) — with `OrganizationProfile.BackgroundVideoUrl` set to a YouTube link the hero renders a covering muted/loop/no-controls `youtube-nocookie` `<iframe.ln-hero__video--yt>`; a direct MP4/HLS link renders `<video.ln-hero__video src=...>`; unset keeps `assets/hero-video.mp4`; the CSP `frame-src` permits the YouTube host | happy | P1 | authored ✓ (`HeroMediaTests` classification) |

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
  And the hero shows the subtitle, a description paragraph, and two info pills (venue + dates), all aligned to the reading-start side
  And the sub-nav strip shows Venue, Time, Date and Weather (Venue at the reading-start side) and no preview/eye button
  And the participation stats show 4 counters (+500, +40, +100, +220)
  And the themes row shows 5 pillar cards
  And the "importance" section (hero2) shows the "The importance of the forum" heading and a "Learn more" button
  And the programme section ("The Forum Programme") shows 3 day cards tagged "Day One", "Day Two", "Day Three"
  And the speakers section shows a filled gold "view all" button
  And the news row shows 3 article cards
  And the discover grid shows 6 destination cards
  And the footer shows the logo-only brand, important-links, contact, social and legal blocks
  And the footer legal block shows a "Last modified" line
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

### E2E-WLB-012 — Route cutover (`/` is the homepage)

```gherkin
Scenario: The rebuild is served at the site root
  When a visitor opens "/"
  Then the response status is 200
  And the page has the ".landing" root and the "ln-hero__title" heading
  And the request is served by the Blazor endpoint, not the static-file middleware

Scenario: The /landing alias still resolves
  When a visitor opens "/landing"
  Then the response status is 200
  And it renders the same landing page as "/"

Scenario: The old static index.html is gone
  When a visitor requests "/index.html"
  Then the response status is 404
```

### E2E-WLB-013 — Hero background video is config-driven (D-756)

```gherkin
Scenario: A configured YouTube link becomes a covering background iframe
  Given OrganizationProfile.BackgroundVideoUrl is "https://youtu.be/rmW5sJTp-Zo"
  When a visitor opens "/"
  Then the hero contains an "iframe.ln-hero__video--yt" whose src targets youtube-nocookie.com/embed/rmW5sJTp-Zo
  And the embed params include autoplay=1, mute=1, loop=1, controls=0
  And the Content-Security-Policy report-only frame-src permits www.youtube-nocookie.com

Scenario: A configured direct file becomes the hero <video> source
  Given OrganizationProfile.BackgroundVideoUrl is "https://cdn.example.com/hero.mp4"
  When a visitor opens "/"
  Then the hero contains a "video.ln-hero__video" with src "https://cdn.example.com/hero.mp4"

Scenario: No configured video keeps the bundled hero asset
  Given OrganizationProfile.BackgroundVideoUrl is empty
  When a visitor opens "/"
  Then the hero contains a "video.ln-hero__video" with src "assets/hero-video.mp4"
```

_Last authored:_ 2026-07-22 by Claude (added E2E-WLB-013 hero background video; D-756).
_Last authored:_ 2026-07-14 by Claude (added E2E-WLB-012 route cutover; `/` cutover).
