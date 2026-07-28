# E2E test catalogue — Website "Opening ceremony" (`/programme/opening`)

| | |
|--|--|
| **Page** | [`web/opening.md`](../../pages/web/opening.md) |
| **Route** | `/programme/opening` |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Test runner** | Chrome DevTools MCP (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | **None — anonymous and static.** No API call, no bearer token, no seeding. |
| **Figma** | KSA Maritime Forum — Opening Ceremony (Desktop AR), node `5867-22242` |
| **Last reviewed** | 2026-07-18 |

> **What this page is.** `/programme/opening` (`Opening.razor` + `.razor.cs`) is the
> Website's public, anonymous **opening-ceremony / programme overview** (Figma
> `5867-22242`) — the first Programme-cluster page. Static SSR on the shared
> `LandingShell` chrome, no CRUD. Three sections:
> 1. **Interior hero** (`ln-pghero`, via `LandingPageHero`) — photo + gradient, the
>    single `<h1>`, a subtitle and the venue + date pills. **No breadcrumb** (the
>    Programme cluster omits it).
> 2. **Overview** (`ln-fsection ln-fsection--dark`, a dark navy section → 8 `ln-vcard ln-vcard--dark`)
>    — an 8-card grid of the forum's activity highlights (blue icon circle + gold label).
> 3. **Target participants** (`ln-fsection` → `ln-numlist`) — a numbered `<ol>` of the
>    nine participant segments, two columns, each with a gold number badge.
>
> **Auth model (Website, anonymous).** No `RequirePermission`, no `/not-permitted`,
> no `/login` redirect.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WOPN-001 | Golden path — hero + overview + participants render + shared chrome, one `<h1>` | happy | P0 | _to author_ |
| E2E-WOPN-002 | Hero has NO breadcrumb (Programme cluster) — `.ln-pghero__crumbs` is absent; the venue + date pills still render | happy | P1 | _to author_ |
| E2E-WOPN-003 | Overview — exactly eight `.ln-vcard--dark` cards on the dark section, each an icon + gold label | happy | P1 | _to author_ |
| E2E-WOPN-004 | Participants — a numbered `<ol>` of nine `.ln-numitem`, badges 1..9, `list-style: none` (no double numbering) | happy | P1 | _to author_ |
| E2E-WOPN-005 | Static/anonymous — no `/api/...` request, no Authorization header, no `/login` or `/not-permitted` redirect | auth | P0 | _to author_ |
| E2E-WOPN-006 | RTL / Arabic ⇄ LTR / English — mirrors (hero photo side flips); Arabic content in AR, English in EN | i18n | P0 | _to author_ |
| E2E-WOPN-007 | Responsive — overview 4→2→1 (≤900/≤520), participants 2→1 (≤720); no horizontal overflow at 1440/1024/768/390 both languages | responsive | P1 | _to author_ |
| E2E-WOPN-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-WOPN-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-WOPN-001 — Golden path

```gherkin
Feature: Website Opening-ceremony page shows the programme overview
  As any visitor (anonymous or signed in)
  I want an overview of the forum's activities and who it is for
  So that I can decide whether and how to take part

Background:
  Given the Website is reachable
  And the browser is a fresh anonymous session (no auth cookie, no bearer token)

Scenario: The page renders the hero + overview + participants
  When the browser opens /programme/opening
  Then NO request to /api/... is made (the page is static)
  And the shared header + footer render (LandingShell chrome)
  And an interior hero (section.ln-pghero) renders with exactly one <h1>
  And a dark overview (section.ln-fsection.ln-fsection--dark) renders eight .ln-vcard--dark cards
  And a participants section (section.ln-fsection) renders one ol.ln-numlist of nine .ln-numitem
  And the page title is "Opening ceremony — Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-opening-ar-1440.png` (AR) + `web-opening-en-1440.png` (EN)
- Console errors: 0 expected
- Network: no `/api/v1/...` request; the hero photo returns 200
- Audit row: none

### E2E-WOPN-002 — Hero without breadcrumb

```gherkin
Scenario: The Programme-cluster hero omits the breadcrumb
  When the browser opens /programme/opening
  Then NO .ln-pghero__crumbs breadcrumb element is present
  And the hero still renders its <h1> title, subtitle and two .ln-pghero__pill pills
```

### E2E-WOPN-003 — Overview (dark cards)

```gherkin
Scenario: The overview renders eight dark highlight cards
  When the browser opens /programme/opening
  Then section.ln-fsection.ln-fsection--dark renders its title (Opening.Overview.Title) + subtitle on a dark (navy) background
  And exactly eight .ln-vcard.ln-vcard--dark cards render
  And each card has a .ln-vcard__icon (blue-tint circle) and a .ln-vcard__label (gold)
  And under EN the labels include "Public & private workshops" and "B2B bilateral meetings"
```

### E2E-WOPN-004 — Participants (numbered list)

```gherkin
Scenario: The participants list renders nine numbered items
  When the browser opens /programme/opening
  Then section.ln-fsection renders its title (Opening.Participants.Title) + subtitle
  And an ol.ln-numlist renders exactly nine li.ln-numitem
  And the .ln-numitem__num badges read 1, 2, 3, … 9 in order
  And ol.ln-numlist has list-style: none (the visual badge is the only number — no browser double-numbering)
  And under EN item 1 reads "Government bodies & royal commissions across the Kingdom's regions."
```

### E2E-WOPN-005 — Static / anonymous

```gherkin
Scenario: The page loads anonymously and fires no API request
  Given a fresh browser with no auth cookie and no bearer token
  When the user opens /programme/opening directly
  Then the page renders WITHOUT redirecting to /login or /not-permitted
  And NO request to /api/... is made and no Authorization header is sent

Scenario: A signed-in session changes nothing
  Given an Approved Visitor is signed in on the Website
  When they open /programme/opening
  Then the rendered page is identical to the anonymous view
```

### E2E-WOPN-006 — RTL / LTR

```gherkin
Scenario: The page mirrors between Arabic and English
  When the browser opens /programme/opening under the Arabic UI culture (<html dir="rtl" lang="ar">)
  Then the page mirrors right-to-left, the hero photo sits on the LEFT
  And the overview labels + participant items render Arabic
  And each .ln-numitem shows its text on the RIGHT and the number badge on the LEFT

  When the browser opens /programme/opening under the English UI culture (<html dir="ltr" lang="en">)
  Then the hero photo flips to the RIGHT
  And the number badge sits on the RIGHT of each participant item
```

### E2E-WOPN-007 — Responsive

```gherkin
Scenario: The grids reflow and nothing overflows horizontally
  When the browser opens /programme/opening and the viewport width is set to each of 1440, 1024, 768, 390
  Then the overview grid renders 4 columns at ≥901px, 2 at ≤900px and 1 at ≤520px
  And the participants list renders 2 columns at ≥721px and 1 at ≤720px
  And the hero block goes full-width below 720px
  And at every width in {1440, 1024, 768, 390} document.scrollWidth == document.clientWidth (no horizontal overflow)
  And this holds in BOTH the EN (LTR) and AR (RTL) cultures
```

---

## Implementation notes

- **Read-only, anonymous, static, no CRUD.** No form, modal, filter or action — the
  matrix above is exhaustive.
- **No breadcrumb (Programme cluster).** WOPN-002 pins the cluster's no-breadcrumb hero,
  in contrast to the About cluster which renders `Home / About / …`.
- **Lower-layer coverage:** component (bUnit, no browser)
  `tests/SIMF.Web.Tests/OpeningPageTests.cs` pins the render + the no-breadcrumb + the
  eight dark cards + the nine numbered items.
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a `.feature`
  under `tests/SIMF.E2E.Tests/`.

---

_Last reviewed:_ 2026-07-18 by Claude (Opening ceremony page — `ln-` Bootstrap SSR, Figma 5867-22242).
