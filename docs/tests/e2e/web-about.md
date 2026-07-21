# E2E test catalogue — Website "About US" (`/about`)

| | |
|--|--|
| **Page** | [`web/about.md`](../../pages/web/about.md) |
| **Route** | `/about` |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Test runner** | Chrome DevTools MCP (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | **None — the page is anonymous and static.** `/about` makes **no** API call; it renders marketing prose + reused landing content. No bearer token, no seeding, no signed-in session is involved. |
| **Figma** | KSA Maritime Forum — About US (Desktop AR), node `5865-33963` (hero `5865:33965`; values `5963:8168`; stats `6226:8513`; pillars `5865:37439`) |
| **Last reviewed** | 2026-07-15 |

> **What this page is.** `/about` (`About.razor` + `About.razor.cs`) is the
> Website's public, anonymous **About-the-forum overview** (Figma `5865-33963`) —
> the first page of the About cluster. It is a **static** SSR page (no API, no
> CRUD, no form, no button beyond the "Partnerships" link) on the shared
> `LandingShell` chrome. Five sections:
> 1. **Interior hero** (`ln-pghero`, via the reusable `LandingPageHero`) — a
>    photo on the inline-END side under a blue brand gradient, with a breadcrumb
>    (Home / About), the page's single `<h1>` (`About.Hero.Title`), a subtitle,
>    and two gold-tint event pills (venue + date).
> 2. **Intro** (`ln-about`, reused from the landing) — eyebrow/title/lead from
>    `Landing.About.*` + a "Partnerships" CTA linking to `/partners`, and the
>    forum-hall photo.
> 3. **Values** (`ln-values` → four `ln-vcard`) — Innovation / Integration &
>    communication / Sustainability / Responsibility, each a gold-tint icon
>    circle + centred label.
> 4. **Participation stats** (`ln-stats`, reused) — the four `Landing.Stats`
>    counters in a navy 2×2 band under the shared `Landing.Stats.*` headers.
> 5. **Pillars** (`ln-pillars` → three `ln-fcard`) — Strategic dialogue /
>    Global partnerships / Foreseeing the future, each a gold-tint **square** icon
>    chip (a distinct globe / anchor / chip SVG) + navy title + gray description.
>
> **Bilingual.** Hero + section headers come from `IStringLocalizer<Strings>`
> (`About.*` + shared `PageHero.Home`) and follow the `/culture` switch; the
> repeated card content is `Bilingual` in `About.razor.cs` resolved `.For(rtl)`
> (Arabic-preferred in RTL). The intro + stats reuse the landing's own content so
> the shared event facts are single-sourced.
>
> **Auth model (Website, anonymous).** This is **not** a Control-Panel page: no
> `RequirePermission`, no `/not-permitted`, no unauthenticated → `/login`
> redirect. The page is reachable by anyone; the "auth" row asserts the
> *anonymous-and-static* contract (loads with no Authorization header, fires no
> `/api/...` request), not a redirect.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WABT-001 | Golden path — all five sections render (hero, intro, values, stats, pillars) + shared chrome, one `<h1>` | happy | P0 | _to author_ |
| E2E-WABT-002 | Hero — breadcrumb (Home / About), title, subtitle, two event pills (venue + date) with icons | happy | P1 | _to author_ |
| E2E-WABT-003 | Values — exactly four `ln-vcard`, each with a gold-tint icon circle + a label | happy | P1 | _to author_ |
| E2E-WABT-004 | Stats — exactly four `ln-stat` counters (2×2) under the reused `Landing.Stats.*` headers; each "+N" keeps the leading "+" LTR | happy | P1 | _to author_ |
| E2E-WABT-005 | Pillars — exactly three `ln-fcard`, each with a **distinct** icon (globe / anchor / chip), a title and a description | happy | P1 | _to author_ |
| E2E-WABT-006 | Intro reuse — the `ln-about` block renders the landing's `Landing.About.*` content and the CTA links to the landing partners band (`/#partners`) | happy | P2 | _to author_ |
| E2E-WABT-007 | Static/anonymous — the page fires **no** `/api/...` request, carries no Authorization header, never redirects to `/login` or `/not-permitted` | auth | P0 | _to author_ |
| E2E-WABT-008 | RTL / Arabic render — under `<html dir="rtl" lang="ar">` the page mirrors right-to-left, the hero photo sits on the LEFT with the text block on the RIGHT, and Arabic card content renders | i18n | P0 | _to author_ |
| E2E-WABT-009 | LTR / English mirror — under EN the hero photo flips to the RIGHT with the text on the LEFT; values/pillars/stats read English | i18n | P1 | _to author_ |
| E2E-WABT-010 | Responsive — values 4→2→1 and pillars 3→2→1 at ≤900/≤520; intro stacks below 980; hero full-width below 720; no horizontal overflow at 1440/1024/768/390 in both languages | responsive | P1 | _to author_ |
| E2E-WABT-011 | No-JS graceful — with JavaScript disabled every `ln-reveal` section renders at full opacity (content never hidden) | resilience | P2 | _to author_ |

## Scenarios

### E2E-WABT-001 — Golden path

```gherkin
Feature: Website About-the-forum overview renders its five marketing sections
  As any visitor (anonymous or signed in)
  I want an overview of the forum, its values, reach and pillars
  So that I understand what the Saudi International Maritime Forum is

Background:
  Given the Website is reachable
  And the browser is a fresh anonymous session (no auth cookie, no bearer token)

Scenario: The About page renders all five sections on the shared chrome
  When the browser opens /about
  Then NO request to /api/... is made (the page is static)
  And the shared header (nav) and footer render (LandingShell chrome)
  And an interior hero (section.ln-pghero) renders with exactly one <h1>
  And a two-column intro (section.ln-about) renders below the hero
  And a values strip (section.ln-values) renders four .ln-vcard cards
  And a participation-stats band (section.ln-stats) renders four .ln-stat counters
  And a pillars row (section.ln-pillars) renders three .ln-fcard cards
  And the page title is "About the Forum — Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-about-ar-1440.png` (full page, AR) + `web-about-en-1440.png` (EN)
- Console errors: 0 expected (only the shared hero-font preload hint)
- Network: no `/api/v1/...` request fires; all assets (hero photo, 3 pillar icons, intro photo) return 200
- Audit row: none — `/about` is a read-only anonymous static page and writes nothing

### E2E-WABT-002 — Hero

```gherkin
Scenario: The interior hero shows breadcrumb, title, subtitle and two event pills
  When the browser opens /about
  Then section.ln-pghero renders a breadcrumb "Home / About" (PageHero.Home + About.Breadcrumb)
  And the <h1> reads the About hero title (About.Hero.Title)
  And a subtitle paragraph (About.Hero.Subtitle) renders under the title
  And exactly two .ln-pghero__pill pills render
  And one pill shows the venue (Landing.Hero.Venue) with a location icon
  And one pill shows the date (Landing.Subnav.Date) with a calendar icon
  And the hero background photo (assets/figma/about/about-hero.jpg) returns 200 (not a broken image)
```

### E2E-WABT-003 — Values

```gherkin
Scenario: The values strip renders four labelled cards
  When the browser opens /about
  Then section.ln-values renders its title (About.Values.Title)
  And exactly four .ln-vcard cards render
  And each card contains a .ln-vcard__icon circle and a .ln-vcard__label
  And under EN the labels read: Innovation, Integration & communication, Sustainability, Responsibility
```

### E2E-WABT-004 — Stats

```gherkin
Scenario: The participation band renders the four canonical counters
  When the browser opens /about
  Then section.ln-stats renders the reused headers (Landing.Stats.Eyebrow / .Title / .Lead)
  And exactly four .ln-stat counters render (two rows of two)
  And each .ln-stat__num keeps its leading "+" on the left (direction: ltr) — e.g. "+500", not "500+"
  And under EN one counter reads "+500 Officials & delegates" and one reads "+40 Participating countries"
```

> **Note.** The Figma frame shows six counter slots with placeholder duplicates;
> the page renders the four canonical `Landing.Stats` figures (matching the
> homepage) rather than invent duplicate numbers — see `web/about.md` §7.

### E2E-WABT-005 — Pillars

```gherkin
Scenario: The pillars row renders three feature cards with distinct icons
  When the browser opens /about
  Then section.ln-pillars renders its title (About.Pillars.Title) and sub (About.Pillars.Sub)
  And exactly three .ln-fcard cards render
  And card 1 shows the globe icon (assets/figma/about/icon-globe.svg) + "Strategic dialogue"
  And card 2 shows the anchor icon (assets/figma/about/icon-anchor.svg) + "Global partnerships"
  And card 3 shows the chip icon (assets/figma/about/icon-chip.svg) + "Foreseeing the future"
  And each card has a .ln-fcard__title and a .ln-fcard__desc
  And all three icon SVGs return 200
```

### E2E-WABT-006 — Intro reuse

```gherkin
Scenario: The intro block reuses the landing's About content and links to partners
  When the browser opens /about
  Then exactly one section.ln-about renders
  And it renders the landing eyebrow / title / lead (Landing.About.Eyebrow / .Title / .Lead)
  And its primary CTA (a.ln-btn--primary) links to /#partners (the landing partners band; the dedicated /partners page lands in Wave 4)
  And the intro photo (assets/figma/about/about-card-1.jpg) returns 200
```

### E2E-WABT-007 — Static / anonymous by design

```gherkin
Scenario: The page loads anonymously and fires no API request
  Given a fresh browser with no auth cookie and no bearer token
  When the user opens /about directly
  Then the page renders WITHOUT redirecting to /login
  And the page does NOT redirect to /not-permitted (a Control-Panel concept, absent here)
  And NO request to /api/... is made and no Authorization header is sent
  And no 401/403 occurs

Scenario: A signed-in session changes nothing on this page
  Given an Approved Visitor is signed in on the Website
  When they open /about
  Then the rendered page is identical to the anonymous view (still no API call)
```

### E2E-WABT-008 — RTL / Arabic render

```gherkin
Scenario: The page mirrors right-to-left and renders Arabic content under the Arabic culture
  When the browser opens /about under the Arabic UI culture (<html dir="rtl" lang="ar">)
  Then the page mirrors right-to-left
  And in the hero the background photo sits on the LEFT and the text block (title + pills) on the RIGHT
  And the <h1> renders the Arabic hero title and the breadcrumb reads "الرئيسية / عن الملتقى"
  And the value labels render Arabic (الابتكار / التكامل والتواصل / الاستدامة / المسؤولية)
  And the pillar titles render Arabic (حوار استراتيجي / شراكات عالمية / استشراف المستقبل)
```

### E2E-WABT-009 — LTR / English mirror

```gherkin
Scenario: The hero mirrors correctly under English
  When the browser opens /about under the English UI culture (<html dir="ltr" lang="en">)
  Then in the hero the background photo flips to the RIGHT and the text block to the LEFT
  And the breadcrumb reads "Home / About"
  And the values, stats and pillars render their English content
```

### E2E-WABT-010 — Responsive

```gherkin
Scenario: The grids reflow and nothing overflows horizontally
  When the browser opens /about and the viewport width is set to each of 1440, 1024, 768, 390
  Then the values grid renders 4 columns at ≥901px, 2 at ≤900px and 1 at ≤520px
  And the pillars grid renders 3 columns at ≥901px, 2 at ≤900px and 1 at ≤520px
  And the intro (.ln-about__inner) stacks to a single column below 980px
  And the hero block goes full-width below 720px
  And at every width in {1440, 1024, 768, 390} document.scrollWidth == document.clientWidth (no horizontal overflow)
  And this holds in BOTH the EN (LTR) and AR (RTL) cultures
```

### E2E-WABT-011 — No-JS graceful degradation

```gherkin
Scenario: With JavaScript disabled every section is fully visible
  Given the browser has JavaScript disabled
  When the user opens /about
  Then every .ln-reveal section renders at full opacity (the reveal-on-scroll never hides content without JS)
  And all five sections and the chrome are readable
```

**Evidence captured (E2E-WABT-011):**
- DOM check: with JS off, `getComputedStyle('.ln-values .ln-reveal').opacity === '1'` (the `.ln-js`-gated reveal rule only hides when the head script has added `.ln-js`)

---

## Implementation notes

- **Read-only, anonymous, static, no CRUD.** `/about` has no form, modal, filter
  or grid action — its only interactive affordance is the "Partnerships" link to
  `/partners`. The matrix above is exhaustive for the page's actual behaviour
  (load → render five sections). Do not invent Add/Edit/Delete, search, API or
  permission scenarios the page does not have.
- **No API layer to cover.** Unlike `/speakers` and `/sessions/{id}`, this page
  makes no server call, so there is no API-integration test — the content is
  static resx + `Bilingual` records + the reused `Landing.Stats` list.
- **Lower-layer coverage:**
  - Component (bUnit, no browser): `tests/SIMF.Web.Tests/AboutPageTests.cs` pins
    the render — `Renders_exactly_one_h1_hero_with_breadcrumb_and_info_pills`,
    `Renders_the_four_forum_values`, `Reuses_the_landing_participation_stats_band`,
    `Renders_the_three_pillars_with_their_distinct_icons`,
    `Reuses_the_landing_about_intro_block`.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin scenario
  into a `.feature` under `tests/SIMF.E2E.Tests/`. The steps are runner-agnostic.

---

_Last reviewed:_ 2026-07-15 by Claude (About US page — `ln-` Bootstrap SSR, Figma 5865-33963).
