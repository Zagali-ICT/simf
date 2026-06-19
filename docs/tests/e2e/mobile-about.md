# E2E test catalogue — `About the forum` (`about`)

> **Authority:** SIMF E2E template (D-133). The content read is built + anonymous
> (D-173). Pixel-parity to KSA Figma frame `1082:15307` (D-448): the navy
> `KsaPage` shell, an **intro card** (`SIMF · 2026` kicker + gold heading + the
> forum paragraph, frame `1082:15566`) and the **"المحاور الرئيسية"** list of the
> **four fixed forum themes** (frames `1082:15578`…`15620`). The paragraph is
> hydrated from the CMS content layer (`GET /app/content/about`) when present and
> **falls back to static bilingual copy** otherwise; the heading + the four themes
> are static (no structured CMS block exists for them — the page always shows the
> forum content). Widget-tested in
> `src/Mobile/simf_app/test/features/about/about_screen_test.dart`.

| | |
|--|--|
| **Page** | [`Page_037`](../../App/Page_037/README.md) |
| **Route** | `GET /api/v1/app/content/about` · app screen #37 `/about` |
| **Auth setup** | **None** — `AllowAnonymous`. |
| **Last reviewed** | 2026-06-19 (D-448 — Figma `1082:15307` parity) |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB037-001 | Heading + CMS-hydrated paragraph + the four numbered themes (01–04) | happy | P0 | authored ✓ (screen `renders the heading, the CMS paragraph and the four themes`) |
| E2E-MOB037-002 | Unseeded key (404) → static fallback paragraph; themes still render | edge | P1 | authored ✓ (screen `a 404 … falls back to the static paragraph`) |
| E2E-MOB037-003 | Server error → degrades to static content (no error screen) | edge | P1 | authored ✓ (screen `a server error also degrades to the static content`) |
| E2E-MOB037-004 | RTL: the theme number sits to the right (inline start) of its title | rtl | P1 | authored ✓ (screen `Arabic: the theme number sits to the right of its title`) |

## Scenarios

### E2E-MOB037-001 — About content + themes

```gherkin
Feature: About the forum
  As a guest
  I want the forum intro and its main themes
  So that I understand what SIMF is about

Scenario: The intro card and the four themes render
  Given the CMS "about" block returns a body
  When the /about screen renders
  Then the intro card shows "SIMF · 2026", the gold heading and the CMS paragraph
  And the "Main themes" section lists the four numbered themes 01–04 with their titles
```

**Evidence:** screen test `renders the heading, the CMS paragraph and the four themes`.

### E2E-MOB037-002 / 003 / 004 — Fallback, error degrade, RTL

```gherkin
Scenario: An unseeded CMS key falls back to the static paragraph
  Given GET /app/content/about returns 404
  Then the static forum paragraph is shown and the four themes still render

Scenario: A server error degrades to static content
  Given GET /app/content/about returns 500
  Then the static paragraph + themes are shown (no error screen — the page is content-complete)

Scenario: RTL theme layout
  Given the device locale is Arabic
  Then each theme's number (e.g. "01") sits to the right of its title (inline start under RTL)
```

**Evidence:** screen tests `a 404 (unseeded key) falls back to the static paragraph`,
`a server error also degrades to the static content`,
`Arabic: the theme number sits to the right of its title`.

---

_Last reviewed:_ `2026-06-19` by `SIMF Team`.
