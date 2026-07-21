# E2E test catalogue — Website "Discover Saudi Arabia" (`/discover`)

| | |
|--|--|
| **Page** | [`web/discover.md`](../../pages/web/discover.md) |
| **Route** | `/discover` |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Test runner** | Chrome DevTools MCP (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | **None — anonymous and static.** No API call, no bearer token, no seeding. |
| **Figma** | KSA Maritime Forum — Discover Saudi (Desktop AR), node `5867-29747` — **placeholder hero + cards** |
| **Last reviewed** | 2026-07-19 |

> **What this page is.** `/discover` (`Discover.razor`) is the Website's public,
> anonymous **Discover Saudi Arabia** page (Figma `5867-29747`). Static SSR on the
> shared `LandingShell` chrome, no CRUD. Two sections:
> 1. **Interior hero** (`ln-pghero`, via `LandingPageHero`) — photo + gradient, the
>    single `<h1>`, a subtitle and the venue + date pills. **No breadcrumb.**
> 2. **Destinations** (`ln-discover` → `ln-dcard` × 6) — a title + description, then
>    six destination cards (photo + name `<h3>` + driving distance + region) reused
>    from `Landing.DiscoverCards`.
>
> **Reuse / content note.** The destinations band reuses the landing's shared CSS +
> data (single-sourced). The Figma hero + cards are placeholders; this page shows a
> real hero + the landing's six real destinations, and the self-referential "Explore
> Saudi Arabia" CTA is omitted — see `web/discover.md` §7.
>
> **Auth model (Website, anonymous).** No `RequirePermission`, no `/not-permitted`,
> no `/login` redirect.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WDS-001 | Golden path — hero + destinations grid render + shared chrome, one `<h1>` | happy | P0 | _to author_ |
| E2E-WDS-002 | Hero has NO breadcrumb (single-page cluster); the venue + date pills render | happy | P1 | _to author_ |
| E2E-WDS-003 | Destinations — six `ln-dcard` cards (photo + name + distance + region); clean h1→h2→h3 order | happy | P1 | _to author_ |
| E2E-WDS-004 | The self-referential "Explore Saudi Arabia" CTA is NOT rendered | happy | P2 | _to author_ |
| E2E-WDS-005 | Static/anonymous — no `/api/...` request, no Authorization header, no `/login` or `/not-permitted` redirect | auth | P0 | _to author_ |
| E2E-WDS-006 | RTL / Arabic ⇄ LTR / English — hero + cards mirror; Arabic content in AR, English in EN | i18n | P1 | _to author_ |
| E2E-WDS-007 | Responsive — the grid collapses 3→2→1 columns; no horizontal overflow at 1440/1280/1024/768/390 both languages | responsive | P1 | _to author_ |

## Scenarios

### E2E-WDS-001 — Golden path

```gherkin
Feature: Website Discover-Saudi-Arabia page showcases destinations to explore
  As any visitor (anonymous or signed in)
  I want to see the destinations across Saudi Arabia near the forum
  So that I can plan what to explore around my trip

Background:
  Given the Website is reachable
  And the browser is a fresh anonymous session (no auth cookie, no bearer token)

Scenario: The page renders the hero + the destinations grid
  When the browser opens /discover
  Then NO request to /api/... is made (the page is static)
  And the shared header + footer render (LandingShell chrome)
  And an interior hero (section.ln-pghero) renders with exactly one <h1>
  And a destinations band (section.ln-discover) renders six .ln-dcard cards
  And the page title is "Discover Saudi Arabia — Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-discover-ar-1440.png` (AR) + `web-discover-en-1440.png` (EN)
- Console errors: 0 expected (a benign shared-chrome font-preload warning is allowed)
- Network: no `/api/v1/...` request; the hero photo + six card photos return 200
- Audit row: none

### E2E-WDS-002 — Hero without breadcrumb

```gherkin
Scenario: The single-page-cluster hero omits the breadcrumb
  When the browser opens /discover
  Then NO .ln-pghero__crumbs breadcrumb element is present
  And the hero renders its <h1> title, subtitle and two .ln-pghero__pill pills
```

### E2E-WDS-003 — Destinations grid

```gherkin
Scenario: The destinations band shows the six place cards
  When the browser opens /discover
  Then section.ln-discover renders its title (Landing.Discover.Title, an <h2>) + description
  And exactly six .ln-dcard cards render, each with a .ln-dcard__img photo, an <h3> .ln-dcard__title,
    a distance .ln-dcard__meta and a region .ln-dcard__meta
  And under English the first card reads "AlUla", "1,100 km", "Madinah Region"
  And the heading order is exactly one <h1> (hero), one <h2> (section), six <h3> (cards)
```

### E2E-WDS-004 — No self-referential CTA

```gherkin
Scenario: The landing's "Explore Saudi Arabia" CTA is not on the dedicated page
  When the browser opens /discover
  Then NO .ln-btn--outline "Explore Saudi Arabia" button is present in section.ln-discover
```

### E2E-WDS-005 — Static / anonymous

```gherkin
Scenario: The page loads anonymously and fires no API request
  Given a fresh browser with no auth cookie and no bearer token
  When the user opens /discover directly
  Then the page renders WITHOUT redirecting to /login or /not-permitted
  And NO request to /api/... is made and no Authorization header is sent

Scenario: A signed-in session changes nothing
  Given an Approved Visitor is signed in on the Website
  When they open /discover
  Then the rendered page is identical to the anonymous view
```

### E2E-WDS-006 — RTL / LTR

```gherkin
Scenario: The page mirrors between Arabic and English
  When the browser opens /discover under the Arabic UI culture (<html dir="rtl" lang="ar">)
  Then the page mirrors right-to-left, the hero photo sits on the LEFT, content renders Arabic
  And the first destination card reads "العُلا" with region "منطقة المدينة المنورة"

  When the browser opens /discover under the English UI culture (<html dir="ltr" lang="en">)
  Then the hero photo flips to the RIGHT and the content renders English
  And the first destination card reads "AlUla" with region "Madinah Region"
```

### E2E-WDS-007 — Responsive

```gherkin
Scenario: The grid collapses cleanly with no horizontal overflow
  When the browser opens /discover and the viewport width is set to each of 1440, 1280, 1024, 768, 390
  Then the .ln-discover__grid shows 3 columns ≥1000px, 2 columns ≤1000px and 1 column ≤640px
  And at every width in {1440, 1280, 1024, 768, 390} document.scrollWidth == document.clientWidth (no overflow)
  And no element extends past the viewport
  And this holds in BOTH the EN (LTR) and AR (RTL) cultures
```

---

## Implementation notes

- **Read-only, anonymous, static, no CRUD.** The only interaction is opening a
  destination card (currently a non-navigating hover card, as on the landing). The
  matrix above is exhaustive.
- **Reuse contract.** The destinations band is single-sourced off the landing
  (`Landing.DiscoverCards` + `Landing.Discover.Title` / `Landing.Discover.Desc`);
  assertions on card counts / labels double as drift guards.
- **Placeholder Figma frame → real content.** The Figma hero + cards are placeholders;
  this page shows a real hero + the landing's six real destinations
  (`web/discover.md` §7).
- **Lower-layer coverage:** component (bUnit, no browser)
  `tests/SIMF.Web.Tests/DiscoverPageTests.cs` pins the hero, the six-card band and the
  omitted "Explore" CTA.
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a `.feature`
  under `tests/SIMF.E2E.Tests/`.

---

_Last reviewed:_ 2026-07-19 by Claude (Discover Saudi Arabia page — `ln-` Bootstrap SSR, Figma 5867-29747).
