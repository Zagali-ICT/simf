# E2E test catalogue — Website "The organizer" (`/about/organizer`)

| | |
|--|--|
| **Page** | [`web/organizer.md`](../../pages/web/organizer.md) |
| **Route** | `/about/organizer` |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Test runner** | Chrome DevTools MCP (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | **None — anonymous and static.** No API call, no bearer token, no seeding. |
| **Figma** | KSA Maritime Forum — The Organizer (Desktop AR), node `5865-38003` (hero `5865:38005`; cards `5866:39706`) |
| **Last reviewed** | 2026-07-15 |

> **What this page is.** `/about/organizer` (`Organizer.razor` + `.razor.cs`) is the
> Website's public, anonymous **organising-bodies overview** (Figma `5865-38003`) —
> the fourth About-cluster page. Static SSR on the shared `LandingShell` chrome, no
> CRUD. Two sections:
> 1. **Interior hero** (`ln-pghero`, via `LandingPageHero`) — photo + gradient, a
>    **3-level breadcrumb** (Home / About / The organizer), the single `<h1>`, a
>    subtitle and the venue + date pills.
> 2. **Organising bodies** (`ln-fsection` → two `ln-orgcard`) — a title + subtitle
>    over two centred cards: Ministry of Defense (colour emblem `<img>`) and Royal
>    Saudi Naval Forces (the forum mark recoloured navy via a CSS mask), each with a
>    name + description.
>
> **Content note.** The Figma frame was a placeholder (dev-logo, duplicated text);
> this page fills it with the real MOD + RSNF bodies (see `web/organizer.md` §7).
>
> **Auth model (Website, anonymous).** No `RequirePermission`, no `/not-permitted`,
> no `/login` redirect.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WORG-001 | Golden path — hero + two organiser cards render + shared chrome, one `<h1>` | happy | P0 | _to author_ |
| E2E-WORG-002 | Hero 3-level breadcrumb — Home / About / The organizer, the "About" level links to `/about` | happy | P1 | _to author_ |
| E2E-WORG-003 | Cards — exactly two `ln-orgcard`, each with a logo + name + description; card 1 a colour `<img>` emblem, card 2 the navy-masked forum mark (visible, not blank/black) | happy | P1 | _to author_ |
| E2E-WORG-004 | Static/anonymous — no `/api/...` request, no Authorization header, no `/login` or `/not-permitted` redirect | auth | P0 | _to author_ |
| E2E-WORG-005 | RTL / Arabic — mirrors right-to-left, hero photo on the LEFT, Arabic card content (وزارة الدفاع / القوات البحرية الملكية السعودية) | i18n | P0 | _to author_ |
| E2E-WORG-006 | LTR / English mirror — hero photo flips RIGHT; breadcrumb "Home / About / The organizer"; English card content | i18n | P1 | _to author_ |
| E2E-WORG-007 | Responsive — the two cards stack to one column below 720px; no horizontal overflow at 1440/1024/768/390 in both languages | responsive | P1 | _to author_ |
| E2E-WORG-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-WORG-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-WORG-001 — Golden path

```gherkin
Feature: Website Organizer page presents the forum's organising bodies
  As any visitor (anonymous or signed in)
  I want to know who organises the forum
  So that I understand its official backing

Background:
  Given the Website is reachable
  And the browser is a fresh anonymous session (no auth cookie, no bearer token)

Scenario: The page renders the hero + the two organiser cards
  When the browser opens /about/organizer
  Then NO request to /api/... is made (the page is static)
  And the shared header + footer render (LandingShell chrome)
  And an interior hero (section.ln-pghero) renders with exactly one <h1>
  And a section (section.ln-fsection) renders two .ln-orgcard cards
  And the page title is "The organizer — Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-organizer-ar-1440.png` (AR) + `web-organizer-en-1440.png` (EN)
- Console errors: 0 expected
- Network: no `/api/v1/...` request; the MOD emblem + the masked mark's logo-fill.svg return 200
- Audit row: none

### E2E-WORG-002 — Hero 3-level breadcrumb

```gherkin
Scenario: The hero breadcrumb has three levels with a working parent link
  When the browser opens /about/organizer
  Then the .ln-pghero__crumbs breadcrumb reads "Home / About / The organizer"
  And the "About" crumb links to "/about" and "Home" links to "/"
  And the current "The organizer" crumb is plain text
```

### E2E-WORG-003 — Cards + logo treatments

```gherkin
Scenario: Two organiser cards render with distinct logo treatments
  When the browser opens /about/organizer
  Then section.ln-fsection renders its title (Organizer.Section.Title) + subtitle
  And exactly two .ln-orgcard cards render
  And under EN card 1 shows "Ministry of Defense — Saudi Arabia" with a colour <img> emblem (assets/figma/organizer/mod-emblem.svg, 200)
  And under EN card 2 shows "Royal Saudi Naval Forces" with a .ln-orgcard__logo--masked element
  And that .ln-orgcard__logo--masked computes background-color rgb(0, 22, 64) (navy) and a non-none mask-image (the mark is visible, not a blank/black box)
  And each card has a .ln-orgcard__name and a .ln-orgcard__desc
```

### E2E-WORG-004 — Static / anonymous

```gherkin
Scenario: The page loads anonymously and fires no API request
  Given a fresh browser with no auth cookie and no bearer token
  When the user opens /about/organizer directly
  Then the page renders WITHOUT redirecting to /login or /not-permitted
  And NO request to /api/... is made and no Authorization header is sent

Scenario: A signed-in session changes nothing
  Given an Approved Visitor is signed in on the Website
  When they open /about/organizer
  Then the rendered page is identical to the anonymous view
```

### E2E-WORG-005 — RTL / Arabic render

```gherkin
Scenario: The page mirrors right-to-left and renders Arabic
  When the browser opens /about/organizer under the Arabic UI culture (<html dir="rtl" lang="ar">)
  Then the page mirrors right-to-left
  And in the hero the background photo sits on the LEFT and the text block on the RIGHT
  And the breadcrumb reads "الرئيسية / عن الملتقى / المنظم"
  And card 1 reads "وزارة الدفاع — المملكة العربية السعودية" and card 2 "القوات البحرية الملكية السعودية"
```

### E2E-WORG-006 — LTR / English mirror

```gherkin
Scenario: The hero mirrors correctly under English
  When the browser opens /about/organizer under the English UI culture (<html dir="ltr" lang="en">)
  Then in the hero the background photo flips to the RIGHT and the text block to the LEFT
  And the breadcrumb reads "Home / About / The organizer"
  And the cards render their English names + descriptions
```

### E2E-WORG-007 — Responsive

```gherkin
Scenario: The cards stack on narrow viewports with no horizontal overflow
  When the browser opens /about/organizer and the viewport width is set to each of 1440, 1024, 768, 390
  Then the two .ln-orgcard cards sit side by side at ≥721px and stack to one column at ≤720px
  And the hero block goes full-width below 720px
  And at every width in {1440, 1024, 768, 390} document.scrollWidth == document.clientWidth (no horizontal overflow)
  And this holds in BOTH the EN (LTR) and AR (RTL) cultures
```

---

## Implementation notes

- **Read-only, anonymous, static, no CRUD.** No form, modal, filter or action — the
  matrix above is exhaustive.
- **Placeholder Figma frame filled with real content.** The frame shipped a dev-logo
  + duplicated text; this page uses the real MOD + RSNF bodies. The RSNF card's logo
  is a navy-recoloured forum-mark stand-in pending the real RSNF emblem asset
  (`web/organizer.md` §7).
- **Lower-layer coverage:** component (bUnit, no browser)
  `tests/SIMF.Web.Tests/OrganizerPageTests.cs` pins the render + the two logo
  treatments.
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a `.feature`
  under `tests/SIMF.E2E.Tests/`. WORG-003's mask-visibility check needs a real browser.

---

_Last reviewed:_ 2026-07-15 by Claude (The organizer page — `ln-` Bootstrap SSR, Figma 5865-38003).
