# E2E test catalogue — Website "The exhibition" (`/programme/exhibition`)

| | |
|--|--|
| **Page** | [`web/exhibition.md`](../../pages/web/exhibition.md) |
| **Route** | `/programme/exhibition` |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Test runner** | Chrome DevTools MCP (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | **None — anonymous and static.** No API call, no bearer token, no seeding. |
| **Figma** | KSA Maritime Forum — Exhibition (Desktop AR), node `5867-23560` |
| **Last reviewed** | 2026-07-18 |

> **What this page is.** `/programme/exhibition` (`Exhibition.razor`) is the Website's
> public, anonymous **exhibition floor-plan** page (Figma `5867-23560`) — the third
> Programme-cluster page. Static SSR on the shared `LandingShell` chrome, no CRUD. Two
> sections:
> 1. **Interior hero** (`ln-pghero`, via `LandingPageHero`) — photo + gradient, the
>    single `<h1>`, a subtitle and the venue + date pills. **No breadcrumb.**
> 2. **Floor plan** (`ln-fsection` → `ln-exhibit`) — the exhibition map (a dense booth
>    grid + zones), rendered as the exported diagram image inside a bordered card that
>    scrolls horizontally on narrow viewports.
>
> **Auth model (Website, anonymous).** No `RequirePermission`, no `/not-permitted`,
> no `/login` redirect.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WEXH-001 | Golden path — hero + floor-plan map render + shared chrome, one `<h1>` | happy | P0 | _to author_ |
| E2E-WEXH-002 | Hero has NO breadcrumb (Programme cluster); the venue + date pills render | happy | P1 | _to author_ |
| E2E-WEXH-003 | Map — the `.ln-exhibit__map` image loads (200, not broken) with accessible `alt`, inside the `.ln-exhibit__scroll` card | happy | P1 | _to author_ |
| E2E-WEXH-004 | Static/anonymous — no `/api/...` request, no Authorization header, no `/login` or `/not-permitted` redirect | auth | P0 | _to author_ |
| E2E-WEXH-005 | RTL / Arabic ⇄ LTR / English — hero + headers mirror; the map image is the same diagram in both | i18n | P1 | _to author_ |
| E2E-WEXH-006 | Responsive — the map card scrolls horizontally below 720px while the PAGE never overflows (scrollWidth==clientWidth) at 1440/1024/768/390 | responsive | P0 | _to author_ |
| E2E-WEXH-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-WEXH-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | 2026-07-29 PASS (LTR+RTL) |

## Scenarios

### E2E-WEXH-001 — Golden path

```gherkin
Feature: Website Exhibition page shows the accompanying floor plan
  As any visitor (anonymous or signed in)
  I want to see the exhibition floor plan
  So that I can find the stands and zones

Background:
  Given the Website is reachable
  And the browser is a fresh anonymous session (no auth cookie, no bearer token)

Scenario: The page renders the hero + the floor-plan map
  When the browser opens /programme/exhibition
  Then NO request to /api/... is made (the page is static)
  And the shared header + footer render (LandingShell chrome)
  And an interior hero (section.ln-pghero) renders with exactly one <h1>
  And a floor-plan section (section.ln-fsection) renders a .ln-exhibit card with the map image
  And the page title is "The exhibition — Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-exhibition-ar-1440.png` (AR) + `web-exhibition-en-1440.png` (EN)
- Console errors: 0 expected
- Network: no `/api/v1/...` request; the map image (assets/figma/exhibition/exhibition-map.png) returns 200
- Audit row: none

### E2E-WEXH-002 — Hero without breadcrumb

```gherkin
Scenario: The Programme-cluster hero omits the breadcrumb
  When the browser opens /programme/exhibition
  Then NO .ln-pghero__crumbs breadcrumb element is present
  And the hero renders its <h1> title, subtitle and two .ln-pghero__pill pills
```

### E2E-WEXH-003 — Floor-plan map

```gherkin
Scenario: The map image loads with accessible alt inside the scroll card
  When the browser opens /programme/exhibition
  Then section.ln-fsection renders its title (Exhibition.Section.Title) + subtitle
  And a .ln-exhibit card contains a .ln-exhibit__scroll wrapper
  And the .ln-exhibit__map <img> src is "assets/figma/exhibition/exhibition-map.png" and returns 200 (not a broken image)
  And the image has a non-empty alt (Exhibition.Map.Alt)
```

### E2E-WEXH-004 — Static / anonymous

```gherkin
Scenario: The page loads anonymously and fires no API request
  Given a fresh browser with no auth cookie and no bearer token
  When the user opens /programme/exhibition directly
  Then the page renders WITHOUT redirecting to /login or /not-permitted
  And NO request to /api/... is made and no Authorization header is sent

Scenario: A signed-in session changes nothing
  Given an Approved Visitor is signed in on the Website
  When they open /programme/exhibition
  Then the rendered page is identical to the anonymous view
```

### E2E-WEXH-005 — RTL / LTR

```gherkin
Scenario: The hero + headers mirror; the map is the same diagram in both languages
  When the browser opens /programme/exhibition under the Arabic UI culture (<html dir="rtl" lang="ar">)
  Then the page mirrors right-to-left, the hero photo sits on the LEFT, headers render Arabic

  When the browser opens /programme/exhibition under the English UI culture (<html dir="ltr" lang="en">)
  Then the hero photo flips to the RIGHT and the headers render English
  And the floor-plan image is the same diagram (its zone labels are baked-in — see web/exhibition.md §7)
```

### E2E-WEXH-006 — Responsive

```gherkin
Scenario: The map card scrolls while the page never overflows
  When the browser opens /programme/exhibition and the viewport width is set to each of 1440, 1024, 768, 390
  Then the .ln-exhibit__scroll card may scroll horizontally below 720px (the map keeps a legible width)
  And at every width in {1440, 1024, 768, 390} document.scrollWidth == document.clientWidth (no horizontal PAGE overflow)
  And this holds in BOTH the EN (LTR) and AR (RTL) cultures
```

---

## Implementation notes

- **Read-only, anonymous, static, no CRUD.** No form, modal, filter or action — the
  only affordance is scrolling the map card. The matrix above is exhaustive.
- **Map as image.** The dense booth grid is the exported diagram image, not rebuilt in
  HTML (impractical + non-interactive). An EN/SVG or app-wired interactive variant is a
  follow-up (`web/exhibition.md` §7).
- **Page-overflow guard.** WEXH-006 is P0: the horizontal scroll must stay inside
  `.ln-exhibit__scroll`; the page body must not gain a horizontal scrollbar.
- **Lower-layer coverage:** component (bUnit, no browser)
  `tests/SIMF.Web.Tests/ExhibitionPageTests.cs` pins the render + the map image + alt.
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a `.feature`
  under `tests/SIMF.E2E.Tests/`.

---

_Last reviewed:_ 2026-07-18 by Claude (Exhibition page — `ln-` Bootstrap SSR, Figma 5867-23560).
