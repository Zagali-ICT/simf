# E2E test catalogue — Website "Partners & sponsors" (`/partners`)

| | |
|--|--|
| **Page** | [`web/partners.md`](../../pages/web/partners.md) |
| **Route** | `/partners` |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Test runner** | Chrome DevTools MCP (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | **None — anonymous and static.** No API call, no bearer token, no seeding. |
| **Figma** | KSA Maritime Forum — Companies / Partners & Sponsors (Desktop AR), node `5866-40017` |
| **Last reviewed** | 2026-07-19 |

> **What this page is.** `/partners` (`Partners.razor`) is the Website's public,
> anonymous **partners & sponsors** page (Figma `5866-40017`) — the first
> Partners-cluster page. Static SSR on the shared `LandingShell` chrome, no CRUD.
> Three sections:
> 1. **Interior hero** (`ln-pghero`, via `LandingPageHero`) — photo + gradient, the
>    single `<h1>`, a subtitle and the venue + date pills. **No breadcrumb.**
> 2. **Government partners** (`ln-pband` → `ln-pcard` × 4) — a title + description,
>    then four government-entity cards (logo + gray label) reused from
>    `Landing.PartnerLogos`, plus a gold progress rail.
> 3. **Sponsors** (`ln-spon` → `ln-scard2`) — a title + description, then a
>    horizontally-scrolled strip of sponsor cards (external-link icon + logo + tier
>    tag) reused from `Landing.Sponsors`, with prev/next arrows.
>
> **Reuse / content note.** Both bands reuse the landing's shared CSS + JS + data
> (single-sourced). The sponsors "View all" CTA is intentionally omitted (this page
> IS the full listing) and the sponsor logos are branded placeholders until a public
> logo route exists — see `web/partners.md` §7.
>
> **Auth model (Website, anonymous).** No `RequirePermission`, no `/not-permitted`,
> no `/login` redirect.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WPT-001 | Golden path — hero + partners grid + sponsors carousel render + shared chrome, one `<h1>` | happy | P0 | _to author_ |
| E2E-WPT-002 | Hero has NO breadcrumb (single-page cluster); the venue + date pills render | happy | P1 | _to author_ |
| E2E-WPT-003 | Government partners — four `ln-pcard` cards (logo + label) + the gold progress rail | happy | P1 | _to author_ |
| E2E-WPT-004 | Sponsors carousel — sponsor cards (icon + logo + tier tag) + prev/next arrows; NO "View all" CTA | happy | P1 | _to author_ |
| E2E-WPT-005 | Static/anonymous — no `/api/...` request, no Authorization header, no `/login` or `/not-permitted` redirect | auth | P0 | _to author_ |
| E2E-WPT-006 | RTL / Arabic ⇄ LTR / English — hero + bands mirror; Arabic content in AR, English in EN | i18n | P1 | _to author_ |
| E2E-WPT-007 | Responsive — the two band strips scroll internally; no page horizontal overflow at 1440/1280/1024/768/390 both languages | responsive | P1 | _to author_ |
| E2E-WPT-008 | Inbound wiring — the About-cluster nav "Partnerships" item and the About page CTA open `/partners` (not the old `#partners` anchor) | nav | P1 | _to author_ |
| E2E-WPT-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-WPT-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-WPT-001 — Golden path

```gherkin
Feature: Website Partners & sponsors page showcases the forum's partners and sponsors
  As any visitor (anonymous or signed in)
  I want to see the forum's government partners and its sponsors
  So that I understand who backs and supports the forum

Background:
  Given the Website is reachable
  And the browser is a fresh anonymous session (no auth cookie, no bearer token)

Scenario: The page renders the hero + both showcase bands
  When the browser opens /partners
  Then NO request to /api/... is made (the page is static)
  And the shared header + footer render (LandingShell chrome)
  And an interior hero (section.ln-pghero) renders with exactly one <h1>
  And a partners band (section.ln-pband) renders four .ln-pcard cards
  And a sponsors band (section.ln-spon) renders a .ln-spon__carousel
  And the page title is "Partners & sponsors — Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-partners-ar-1440.png` (AR) + `web-partners-en-1440.png` (EN)
- Console errors: 0 expected (a benign shared-chrome font-preload warning is allowed)
- Network: no `/api/v1/...` request; the hero photo + partner logos return 200
- Audit row: none

### E2E-WPT-002 — Hero without breadcrumb

```gherkin
Scenario: The single-page-cluster hero omits the breadcrumb
  When the browser opens /partners
  Then NO .ln-pghero__crumbs breadcrumb element is present
  And the hero renders its <h1> title, subtitle and two .ln-pghero__pill pills
```

### E2E-WPT-003 — Government partners grid

```gherkin
Scenario: The partners band shows the four government-entity cards + rail
  When the browser opens /partners
  Then section.ln-pband renders its title (Landing.Partners.Title) + description
  And exactly four .ln-pcard cards render, each with a .ln-pcard__logo image and a .ln-pcard__label
  And under Arabic the first card label reads "رئاسة أمن الدولة"
  And a .ln-pband__bar progress rail with a gold .ln-pband__thumb renders below the cards
```

### E2E-WPT-004 — Sponsors carousel (no "View all")

```gherkin
Scenario: The sponsors carousel renders cards + arrows and omits the self-referential "View all"
  When the browser opens /partners
  Then section.ln-spon renders its title (Landing.Sponsors.Title) + description
  And the .ln-spon__track renders one or more .ln-scard2 cards
  And each .ln-scard2 has an external-link icon, a logo image and a .ln-scard2__tag (the tier tag)
  And two .ln-spon__arrow prev/next buttons render
  And NO .ln-btn--outline "View all" button is present in the sponsors band
  And clicking the next arrow scrolls the .ln-spon__viewport rightwards (landing.js initSponsors)
```

### E2E-WPT-005 — Static / anonymous

```gherkin
Scenario: The page loads anonymously and fires no API request
  Given a fresh browser with no auth cookie and no bearer token
  When the user opens /partners directly
  Then the page renders WITHOUT redirecting to /login or /not-permitted
  And NO request to /api/... is made and no Authorization header is sent

Scenario: A signed-in session changes nothing
  Given an Approved Visitor is signed in on the Website
  When they open /partners
  Then the rendered page is identical to the anonymous view
```

### E2E-WPT-006 — RTL / LTR

```gherkin
Scenario: The page mirrors between Arabic and English
  When the browser opens /partners under the Arabic UI culture (<html dir="rtl" lang="ar">)
  Then the page mirrors right-to-left, the hero photo sits on the LEFT, content renders Arabic
  And the partners band title reads "قسم الشركاء" and the sponsors tier tag reads "مستضيف"

  When the browser opens /partners under the English UI culture (<html dir="ltr" lang="en">)
  Then the hero photo flips to the RIGHT and the content renders English
  And the partners band title reads "Partners" and the sponsors tier tag reads "Host"
```

### E2E-WPT-007 — Responsive

```gherkin
Scenario: The band strips scroll internally with no page horizontal overflow
  When the browser opens /partners and the viewport width is set to each of 1440, 1280, 1024, 768, 390
  Then the four partner cards scroll within .ln-pband__strip (they do not push the page wide)
  And the sponsor cards scroll within .ln-spon__viewport
  And at every width in {1440, 1280, 1024, 768, 390} document.scrollWidth == document.clientWidth (no page overflow)
  And no element outside the two scroll strips extends past the viewport
  And this holds in BOTH the EN (LTR) and AR (RTL) cultures
```

### E2E-WPT-008 — Inbound nav wiring

```gherkin
Scenario: The "Partnerships" nav item opens the dedicated page
  Given the browser is on any Website page with the shared nav header
  When the user opens the About mega-menu and clicks "Partnerships"
  Then the browser navigates to /partners (not an on-page #partners anchor)

Scenario: The About page CTA opens the dedicated page
  Given the browser is on /about
  When the user clicks the "Partnerships" CTA button in the intro block
  Then the browser navigates to /partners
```

---

## Implementation notes

- **Read-only, anonymous, static, no CRUD.** The only interactions are the carousel
  arrows (client JS scroll) and the external-link sponsor cards. The matrix above is
  exhaustive.
- **Reuse contract.** The two bands are single-sourced off the landing
  (`Landing.PartnerLogos` / `Landing.Sponsors` + `Landing.Partners.*` /
  `Landing.Sponsors.*`); assertions on card counts / labels double as drift guards.
- **Placeholder sponsors.** Until a public sponsor-logo route exists, the sponsors
  band renders the shipped placeholder set (`web/partners.md` §7). When wired live,
  add scenarios for a populated tier group + the empty-state.
- **Lower-layer coverage:** component (bUnit, no browser)
  `tests/SIMF.Web.Tests/PartnersPageTests.cs` pins the hero, the partners grid and
  the sponsors carousel (incl. the omitted "View all").
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a `.feature`
  under `tests/SIMF.E2E.Tests/`.

---

_Last reviewed:_ 2026-07-19 by Claude (Partners & sponsors page — `ln-` Bootstrap SSR, Figma 5866-40017).
