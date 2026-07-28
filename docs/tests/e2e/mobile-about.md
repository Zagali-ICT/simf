# E2E test catalogue — `About the forum` (`about`)

> **Authority:** SIMF E2E template (D-133). The content read is built + anonymous
> (D-173). **Re-skinned to the restructured KSA Figma frame `1116:16448` (D-465):**
> the navy `KsaPage` shell, an anchor-mark header (`الملتقى الدولي البحري`), the
> **الرسالة** (mission) card, the **الرؤية** (vision) card, the **تفاصيل الملتقى**
> details card (السنة / الزمن / المكان) and the **"المحاور الرئيسية"** list of the
> **four fixed forum themes**. The vision paragraph is hydrated from the CMS
> content layer (`GET /app/content/about`) when present and **falls back to static
> bilingual copy** otherwise; the mission line, the details and the four themes are
> static (no structured CMS block — the page always shows the forum content). The
> تفاصيل date value mirrors the mock (the real event date is an OI). Widget-tested
> in `src/Mobile/simf_app/test/features/about/about_screen_test.dart`.

| | |
|--|--|
| **Page** | [`Page_037`](../../App/Page_037/README.md) |
| **Route** | `GET /api/v1/app/content/about` · app screen #37 `/about` |
| **Figma** | `1116:16448` |
| **Auth setup** | **None** — `AllowAnonymous`. |
| **Last reviewed** | 2026-06-20 (D-465 — Figma `1116:16448` restructure) |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB037-001 | Heading + CMS-hydrated paragraph + the four numbered themes (01–04) | happy | P0 | authored ✓ (screen `renders the heading, the CMS paragraph and the four themes`) |
| E2E-MOB037-002 | Unseeded key (404) → static fallback paragraph; themes still render | edge | P1 | authored ✓ (screen `a 404 … falls back to the static paragraph`) |
| E2E-MOB037-003 | Server error → degrades to static content (no error screen) | edge | P1 | authored ✓ (screen `a server error also degrades to the static content`) |
| E2E-MOB037-004 | RTL: the theme number sits to the right (inline start) of its title | rtl | P1 | authored ✓ (screen `Arabic: the theme number sits to the right of its title`) |
| E2E-MOB037-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB037-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-MOB037-001 — About content + themes

```gherkin
Feature: About the forum
  As a guest
  I want the forum intro and its main themes
  So that I understand what SIMF is about

Scenario: The mission/vision cards and the four themes render
  Given the CMS "about" block returns a body
  When the /about screen renders
  Then the الرسالة card shows the mission line and the الرؤية card shows the CMS paragraph
  And the تفاصيل الملتقى card shows the year / date / location rows
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
