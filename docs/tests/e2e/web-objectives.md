# E2E test catalogue — Website "Objectives" (`/about/objectives`)

| | |
|--|--|
| **Page** | [`web/objectives.md`](../../pages/web/objectives.md) |
| **Route** | `/about/objectives` |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Test runner** | Chrome DevTools MCP (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | **None — the page is anonymous and static.** It makes no API call; no bearer token, no seeding, no signed-in session. |
| **Figma** | KSA Maritime Forum — Objectives (Desktop AR), node `5865-34626` (hero `5865:34628`; section `5865:38988`) |
| **Last reviewed** | 2026-07-15 |

> **What this page is.** `/about/objectives` (`Objectives.razor` + `.razor.cs`) is
> the Website's public, anonymous **Objectives overview** (Figma `5865-34626`) — the
> second About-cluster page. A **static** SSR page on the shared `LandingShell`
> chrome, no CRUD/form/button. Two sections:
> 1. **Interior hero** (`ln-pghero`, via the reusable `LandingPageHero`) — the same
>    photo + blue gradient as `/about`, but with a **3-level breadcrumb** (Home /
>    About / Objectives; the middle "About" links to `/about`), the page's single
>    `<h1>` (`Objectives.Hero.Title`), a subtitle and the venue + date pills.
> 2. **Six objectives** (`ln-fsection` → six `ln-fcard ln-fcard--raised`) — a title
>    (`Objectives.Section.Title`) + subtitle over a 3×2 grid of feature cards
>    (maritime security / supply-chain resilience / energy security / infrastructure
>    protection / digital transformation / international cooperation), each a
>    gold-tint square icon chip + navy title + gray description, with a soft shadow.
>
> **Bilingual.** Hero + section headers from `IStringLocalizer<Strings>`
> (`Objectives.*` + reused `PageHero.Home` / `About.Breadcrumb`), following the
> `/culture` switch; the six cards are `Bilingual` content resolved `.For(rtl)`.
>
> **Auth model (Website, anonymous).** No `RequirePermission`, no `/not-permitted`,
> no `/login` redirect. The "auth" row asserts the *anonymous-and-static* contract.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WOBJ-001 | Golden path — hero + six-objectives section render + shared chrome, one `<h1>` | happy | P0 | _to author_ |
| E2E-WOBJ-002 | Hero 3-level breadcrumb — Home / About / Objectives, with the "About" level linking to `/about` | happy | P1 | _to author_ |
| E2E-WOBJ-003 | Objectives grid — exactly six `ln-fcard` (`--raised`), each with a distinct icon + title + description | happy | P1 | _to author_ |
| E2E-WOBJ-004 | Static/anonymous — the page fires **no** `/api/...` request, no Authorization header, never redirects to `/login` or `/not-permitted` | auth | P0 | _to author_ |
| E2E-WOBJ-005 | RTL / Arabic — under `dir="rtl"` the page mirrors, hero photo on the LEFT, Arabic card content renders | i18n | P0 | _to author_ |
| E2E-WOBJ-006 | LTR / English mirror — hero photo flips RIGHT; breadcrumb "Home / About / Objectives"; English card content | i18n | P1 | _to author_ |
| E2E-WOBJ-007 | Responsive — the `ln-fsection` grid steps 3→2→1 at ≤900/≤560; no horizontal overflow at 1440/1024/768/390 in both languages | responsive | P1 | _to author_ |

## Scenarios

### E2E-WOBJ-001 — Golden path

```gherkin
Feature: Website Objectives page renders the forum's six strategic objectives
  As any visitor (anonymous or signed in)
  I want to see what the forum aims to achieve
  So that I understand its strategic goals

Background:
  Given the Website is reachable
  And the browser is a fresh anonymous session (no auth cookie, no bearer token)

Scenario: The Objectives page renders the hero + the six-objectives section
  When the browser opens /about/objectives
  Then NO request to /api/... is made (the page is static)
  And the shared header + footer render (LandingShell chrome)
  And an interior hero (section.ln-pghero) renders with exactly one <h1>
  And a feature section (section.ln-fsection) renders a header + six .ln-fcard cards
  And the page title is "Objectives — Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-objectives-ar-1440.png` (AR) + `web-objectives-en-1440.png` (EN)
- Console errors: 0 expected
- Network: no `/api/v1/...` request; all six card icons + the hero photo return 200
- Audit row: none — read-only anonymous static page

### E2E-WOBJ-002 — Hero 3-level breadcrumb

```gherkin
Scenario: The hero renders a three-level breadcrumb with a working parent link
  When the browser opens /about/objectives
  Then the .ln-pghero__crumbs breadcrumb reads "Home / About / Objectives"
  And the "About" crumb is an <a> whose href is "/about"
  And the "Home" crumb is an <a> whose href is "/"
  And the current "Objectives" crumb is plain text (not a link)
  And the hero <h1> reads the Objectives hero title
  And two .ln-pghero__pill pills render (venue + date)
```

### E2E-WOBJ-003 — Objectives grid

```gherkin
Scenario: The six objectives render as raised feature cards with distinct icons
  When the browser opens /about/objectives
  Then section.ln-fsection renders its title (Objectives.Section.Title) + subtitle
  And exactly six .ln-fcard cards render, all carrying .ln-fcard--raised
  And each card has a .ln-fcard__icon, a .ln-fcard__title and a .ln-fcard__desc
  And under EN the titles read: Strengthening maritime security, Supply-chain resilience,
      Energy security, Protecting infrastructure, Digital transformation, International cooperation
  And the six icons are distinct assets under assets/figma/objectives/ and each returns 200
```

### E2E-WOBJ-004 — Static / anonymous by design

```gherkin
Scenario: The page loads anonymously and fires no API request
  Given a fresh browser with no auth cookie and no bearer token
  When the user opens /about/objectives directly
  Then the page renders WITHOUT redirecting to /login or /not-permitted
  And NO request to /api/... is made and no Authorization header is sent
  And no 401/403 occurs

Scenario: A signed-in session changes nothing
  Given an Approved Visitor is signed in on the Website
  When they open /about/objectives
  Then the rendered page is identical to the anonymous view (still no API call)
```

### E2E-WOBJ-005 — RTL / Arabic render

```gherkin
Scenario: The page mirrors right-to-left and renders Arabic content
  When the browser opens /about/objectives under the Arabic UI culture (<html dir="rtl" lang="ar">)
  Then the page mirrors right-to-left
  And in the hero the background photo sits on the LEFT and the text block on the RIGHT
  And the breadcrumb reads "الرئيسية / عن الملتقى / الأهداف"
  And the section title reads "ستة أهداف رئيسية"
  And the card titles render Arabic (تعزيز الأمن البحري / أمن الطاقة / التعاون الدولي …)
```

### E2E-WOBJ-006 — LTR / English mirror

```gherkin
Scenario: The hero mirrors correctly under English
  When the browser opens /about/objectives under the English UI culture (<html dir="ltr" lang="en">)
  Then in the hero the background photo flips to the RIGHT and the text block to the LEFT
  And the breadcrumb reads "Home / About / Objectives"
  And the section + cards render their English content
```

### E2E-WOBJ-007 — Responsive

```gherkin
Scenario: The objectives grid reflows and nothing overflows horizontally
  When the browser opens /about/objectives and the viewport width is set to each of 1440, 1024, 768, 390
  Then the .ln-fsection__grid renders 3 columns at ≥901px, 2 at ≤900px and 1 at ≤560px
  And the hero block goes full-width below 720px
  And at every width in {1440, 1024, 768, 390} document.scrollWidth == document.clientWidth (no horizontal overflow)
  And this holds in BOTH the EN (LTR) and AR (RTL) cultures
```

---

## Implementation notes

- **Read-only, anonymous, static, no CRUD.** No form, modal, filter or action —
  the matrix above is exhaustive. Do not invent Add/Edit/Delete, search, API or
  permission scenarios the page does not have.
- **No API layer to cover.** The content is static resx + `Bilingual` records; there
  is no API-integration test.
- **Lower-layer coverage:** component (bUnit, no browser)
  `tests/SIMF.Web.Tests/ObjectivesPageTests.cs` pins the render —
  `Renders_one_h1_hero_with_a_three_level_breadcrumb`,
  `Renders_the_six_objectives_as_raised_feature_cards`,
  `Each_objective_card_has_its_own_icon`.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin scenario
  into a `.feature` under `tests/SIMF.E2E.Tests/`. The steps are runner-agnostic.

---

_Last reviewed:_ 2026-07-15 by Claude (Objectives page — `ln-` Bootstrap SSR, Figma 5865-34626).
