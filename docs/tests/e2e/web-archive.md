# E2E test catalogue — Website "Forum archive" (`/archive`)

| | |
|--|--|
| **Page** | [`web/archive.md`](../../pages/web/archive.md) |
| **Route** | `/archive` |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Test runner** | Chrome DevTools MCP (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | **None — anonymous.** No bearer token; one anonymous GET `/api/v1/app/archive`. |
| **Figma** | KSA Maritime Forum — Archive (Desktop AR), node `5840-27997` |
| **Last reviewed** | 2026-07-22 |

> **What this page is.** `/archive` (`Archive.razor`) is the Website's public,
> anonymous **forum archive** page (Figma `5840-27997`). SSR on the shared
> `LandingShell` chrome. It reads the **live** archive edition list
> (`SimfPublicClient.GetArchiveAsync`) server-side; empty/hidden/unreachable falls
> back to the landing's static past editions. Six sections:
> 1. **Interior hero** (`ln-pghero`) — photo, the single `<h1>`, subtitle + pills. **No breadcrumb.**
> 2. **Headline counters** (`ln-stats`, navy) — Speakers / Attendees / Sessions from the latest edition.
> 3. **Photos & video** (`ln-gallery`) — a static highlights grid (§7 of `web/archive.md`).
> 4. **Session titles** (`ln-sessions`, navy) — reused `Landing.Sessions` cards.
> 5. **Past speakers** (`ln-speakers`, navy) — the collage + a link to `/speakers`.
> 6. **Past editions** (`ln-miles ln-miles--wrap`) — **live** edition cards (static Milestones fallback).
>
> **Auth model (Website, anonymous).** No `RequirePermission`, no `/login` redirect.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WAR-001 | Golden path — hero + all six sections render + shared chrome, one `<h1>` | happy | P0 | _to author_ |
| E2E-WAR-002 | Live data — the past-edition cards + the headline counters come from GET /archive (newest-first) | data | P0 | _to author_ |
| E2E-WAR-003 | Fallback — a hidden / empty / unreachable archive renders the static past editions, never blank | data | P0 | _to author_ |
| E2E-WAR-004 | Hero has NO breadcrumb; the venue + date pills render | happy | P1 | _to author_ |
| E2E-WAR-005 | Photos grid (6 items) + reused session cards (3) + the speakers collage render | happy | P1 | _to author_ |
| E2E-WAR-006 | The live edition band wraps (ln-miles--wrap) — many editions reflow, no horizontal overflow | responsive | P0 | _to author_ |
| E2E-WAR-007 | RTL / Arabic ⇄ LTR / English — hero + bands mirror; Arabic content in AR, English in EN | i18n | P1 | _to author_ |
| E2E-WAR-008 | Responsive — grid 3→2→1, cards wrap; no horizontal overflow at 1440/1280/1024/768/390 both languages | responsive | P1 | _to author_ |
| E2E-WAR-009 | Nav — the "Archive" top-menu dropdown lists the real past editions (title + year, newest-first), same source as the page | nav | P1 | _to author_ |
| E2E-WAR-010 | Nav → anchor — clicking an edition in the dropdown opens `/archive` and scrolls to that edition's card (`#ed-{year}`) | nav | P1 | _to author_ |

## Scenarios

### E2E-WAR-001 — Golden path

```gherkin
Feature: Website Forum-archive page shows past editions, numbers and highlights
  As any visitor (anonymous or signed in)
  I want to see the forum's past editions, headline numbers and highlights
  So that I understand the forum's track record

Background:
  Given the Website is reachable
  And the browser is a fresh anonymous session (no auth cookie, no bearer token)

Scenario: The page renders the hero + all six sections
  When the browser opens /archive
  Then the shared header + footer render (LandingShell chrome)
  And an interior hero (section.ln-pghero) renders with exactly one <h1>
  And a navy counters band (section.ln-stats) renders three .ln-stat counters
  And a photos grid (section.ln-gallery) renders six .ln-gallery__item cells
  And a session-titles band (section.ln-sessions) renders three .ln-scard cards
  And a past-speakers band (section.ln-speakers) renders the collage
  And a past-editions band (section.ln-miles.ln-miles--wrap) renders one or more .ln-mcard cards
  And the page title is "Forum archive — Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-archive-ar-1440.png` (AR) + `web-archive-en-1440.png` (EN)
- Console errors: 0 expected (a benign shared-chrome font-preload warning is allowed)
- Network: exactly one GET `/api/v1/app/archive`; the hero + gallery + edition photos return 200
- Audit row: none

### E2E-WAR-002 — Live data

```gherkin
Scenario: The counters + edition cards come from the live archive list
  Given the archive-visibility toggle is on and at least one edition exists
  When the browser opens /archive
  Then GET /api/v1/app/archive returns the edition list
  And the past-edition cards render in newest-first order (highest Year first)
  And the headline counters (.ln-stat__num) show the latest edition's Speakers / Attendees / Sessions
```

### E2E-WAR-003 — Fallback

```gherkin
Scenario: A hidden or unreachable archive still renders past editions
  Given the archive-visibility toggle is OFF (GET /archive returns an empty list) OR the API is unreachable
  When the browser opens /archive
  Then the page does NOT blank or error
  And the past-editions band renders the landing's static past editions (newest-first)
  And the headline counters show the default figures
```

### E2E-WAR-004 — Hero without breadcrumb

```gherkin
Scenario: The hero omits the breadcrumb
  When the browser opens /archive
  Then NO .ln-pghero__crumbs breadcrumb element is present
  And the hero renders its <h1> title, subtitle and two .ln-pghero__pill pills
```

### E2E-WAR-005 — Static highlights + reused bands

```gherkin
Scenario: The gallery, session cards and speakers collage render
  When the browser opens /archive
  Then section.ln-gallery renders its title (Archive.Gallery.Title) + six .ln-gallery__item images
  And section.ln-sessions renders three .ln-scard cards, each a "programme details" link to /programme/sessions
  And section.ln-speakers renders the collage image and a "view all" link to /speakers
```

### E2E-WAR-006 — Wrapping live band

```gherkin
Scenario: The variable-length edition band wraps without overflow
  Given the archive returns five or more editions
  When the browser opens /archive at 1280px width
  Then the .ln-miles.ln-miles--wrap row wraps the .ln-mcard cards onto multiple rows
  And document.scrollWidth == document.clientWidth (no horizontal overflow)
```

### E2E-WAR-007 — RTL / LTR

```gherkin
Scenario: The page mirrors between Arabic and English
  When the browser opens /archive under the Arabic UI culture (<html dir="rtl" lang="ar">)
  Then the page mirrors right-to-left, the hero photo sits on the LEFT, content renders Arabic
  And the counters read "المتحدثون / الحضور / الجلسات" and the editions band title reads "استعرض الإصدارات السابقة"

  When the browser opens /archive under the English UI culture (<html dir="ltr" lang="en">)
  Then the hero photo flips to the RIGHT and the content renders English
  And the counters read "Speakers / Attendees / Sessions"
```

### E2E-WAR-008 — Responsive

```gherkin
Scenario: The bands reflow with no horizontal overflow
  When the browser opens /archive and the viewport width is set to each of 1440, 1280, 1024, 768, 390
  Then the .ln-gallery__grid shows 3 columns ≥900px, 2 columns ≤900px and 1 column ≤560px
  And the session cards and the edition cards wrap to multiple rows on narrow widths
  And at every width in {1440, 1280, 1024, 768, 390} document.scrollWidth == document.clientWidth (no overflow)
  And this holds in BOTH the EN (LTR) and AR (RTL) cultures
```

### E2E-WAR-009 — Nav editions dropdown

```gherkin
Scenario: The "Archive" top-menu is a dropdown of the real editions
  Given the browser is on any Website page with the shared nav header
  When the user opens the "Archive" mega-menu
  Then the dropdown lists one item per past edition, newest-first
  And each item label is the edition title followed by its year (e.g. "SIMF 2025" / "سيمف 2025")
  And the items match the cards on /archive (same source, same order)
  And each item href is "/archive#ed-N" (a real in-page anchor, never a dead "#")
```

### E2E-WAR-010 — Nav item scrolls to the edition card

```gherkin
Scenario: Clicking an edition opens /archive and scrolls to its card
  Given the browser is on the Website home page
  When the user opens the "Archive" mega-menu and clicks the second edition (e.g. 2024)
  Then the browser navigates to /archive#ed-2024 (the edition's year, not an index)
  And the first-paint splash does NOT cover the page (enhanced navigation)
  And the #ed-2024 edition card is scrolled into view near the top of the viewport
  And the scrolled-to card's name matches the clicked dropdown label
```

---

## Implementation notes

- **Read-only, anonymous.** One anonymous GET `/archive`; the only interactions are the
  session-card + speaker + edition links (navigations). The matrix above is exhaustive.
- **Live-data + fallback** is the key behaviour: WAR-002 (populated) and WAR-003
  (empty/unreachable → static Milestones) are both P0.
- **Deferred scope.** The live media gallery + video, and the per-edition session
  titles / past speakers, await a public archive-detail endpoint + a video provider
  (`web/archive.md` §7). Add scenarios when they land.
- **Lower-layer coverage:** component (bUnit, no browser)
  `tests/SIMF.Web.Tests/ArchivePageTests.cs` pins the live-render, the fallback and the
  reused bands via a stub `SimfPublicClient`; `tests/SIMF.Web.Tests/PublicEditionsTests.cs`
  pins the shared editions source (ordering, `ed-N` anchor + `/archive#ed-N` href, the
  "title year" label, latest-edition stats, static fallback) that feeds both the page
  cards and the nav dropdown.
- **Nav editions dropdown.** The top-menu "Archive" is a data-driven dropdown built from
  `PublicEditions` (the same source as the page). Each card renders `id="ed-{year}"` and
  each dropdown item links to `/archive#ed-{year}` (year-based so a link built from an
  older cache snapshot survives a re-order); enhanced navigation does not honour the URL
  fragment, so `landing.js` re-asserts the scroll to the target on `enhancedload` (a short
  retry that stops once the card lands, tolerating a late morph / Blazor scroll reset).
- **Dismiss on click.** The mega-menu opens on hover / keyboard focus. A pointer click on
  the toggle no longer latches it open (`mousedown` `preventDefault` suppresses the click
  focus), and clicking an item dismisses the panel immediately (`is-dismissed`, cleared on
  `mouseleave` OR `focusin` so keyboard users can re-open it). `PublicEditionsTests` also
  pins that a transient API failure is NOT cached (retries the live source next request).
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a `.feature`
  under `tests/SIMF.E2E.Tests/`.

---

_Last reviewed:_ 2026-07-22 by Claude (Forum archive page — `ln-` Bootstrap SSR, Figma 5840-27997; live archive data + static fallback; top-menu editions dropdown + `#ed-N` anchors).
