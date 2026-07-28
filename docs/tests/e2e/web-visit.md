# E2E test catalogue — Website "Visiting & travel" (`/visit`)

| | |
|--|--|
| **Page** | [`web/visit.md`](../../pages/web/visit.md) |
| **Route** | `/visit` |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Test runner** | Chrome DevTools MCP (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | **None — anonymous and static.** No API call, no bearer token, no seeding. |
| **Figma** | KSA Maritime Forum — Visits (Desktop AR), node `5867-24636` |
| **Last reviewed** | 2026-07-19 |

> **What this page is.** `/visit` (`Visit.razor`) is the Website's public, anonymous
> **Visiting & travel** page (Figma `5867-24636`). It **supersedes** the old MudBlazor
> visit-entry page (SimfBanner + four logistics cards) at the same route. Static SSR on
> the shared `LandingShell` chrome, no CRUD. Three sections:
> 1. **Interior hero** (`ln-pghero`, via `LandingPageHero`) — photo + gradient, the
>    single `<h1>`, a subtitle and the venue + date pills. **No breadcrumb.**
> 2. **Why visit** (`ln-discover ln-discover--dark` → `ln-dcard` × 6) — a title +
>    description on a navy background, then six destination cards reused from
>    `Landing.DiscoverCards` (the same six as `/discover`).
> 3. **Travel & visa** (`ln-visa` → reused `ln-about` 2-col) — a band title + sub,
>    then a photo + the tourist-visa heading, two paragraphs of Saudi eVisa copy and an
>    eligible-countries callout with a **placeholder** CTA (see `web/visit.md` §7).
>
> **Auth model (Website, anonymous).** No `RequirePermission`, no `/not-permitted`,
> no `/login` redirect.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WVS-001 | Golden path — hero + why-visit band + travel-&-visa section render + shared chrome, one `<h1>` | happy | P0 | _to author_ |
| E2E-WVS-002 | Hero has NO breadcrumb (single-page cluster); the venue + date pills render | happy | P1 | _to author_ |
| E2E-WVS-003 | Why visit — navy `ln-discover--dark` band with six `ln-dcard` destination cards | happy | P1 | _to author_ |
| E2E-WVS-004 | Travel & visa — 2-col (photo + heading + two paragraphs) + the eligible-countries callout | happy | P1 | _to author_ |
| E2E-WVS-005 | The visa CTA is a placeholder `<button>` (no navigation) described by the countries-list label | happy | P2 | _to author_ |
| E2E-WVS-006 | Static/anonymous — no `/api/...` request, no Authorization header, no `/login` or `/not-permitted` redirect | auth | P0 | _to author_ |
| E2E-WVS-007 | RTL / Arabic ⇄ LTR / English — hero + both bands mirror; Arabic content in AR, English in EN | i18n | P1 | _to author_ |
| E2E-WVS-008 | Responsive — the destination grid collapses 3→2→1 and the visa 2-col stacks; no horizontal overflow at 1440/1280/1024/768/390 both languages | responsive | P1 | _to author_ |
| E2E-WVS-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-WVS-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-WVS-001 — Golden path

```gherkin
Feature: Website Visiting-&-travel page explains why to visit and how to get a visa
  As any visitor (anonymous or signed in)
  I want to know why to visit the Kingdom and how to travel and enter it
  So that I can plan my trip to the forum

Background:
  Given the Website is reachable
  And the browser is a fresh anonymous session (no auth cookie, no bearer token)

Scenario: The page renders the hero + both bands
  When the browser opens /visit
  Then NO request to /api/... is made (the page is static)
  And the shared header + footer render (LandingShell chrome)
  And an interior hero (section.ln-pghero) renders with exactly one <h1>
  And a navy why-visit band (section.ln-discover.ln-discover--dark) renders six .ln-dcard cards
  And a travel-&-visa band (section.ln-visa) renders a reused .ln-about__inner 2-column layout
  And the page title is "Visiting & travel — Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-visit-ar-1440.png` (AR) + `web-visit-en-1440.png` (EN)
- Console errors: 0 expected (a benign shared-chrome font-preload warning is allowed)
- Network: no `/api/v1/...` request; the hero + card + visa photos return 200
- Audit row: none

### E2E-WVS-002 — Hero without breadcrumb

```gherkin
Scenario: The single-page-cluster hero omits the breadcrumb
  When the browser opens /visit
  Then NO .ln-pghero__crumbs breadcrumb element is present
  And the hero renders its <h1> title, subtitle and two .ln-pghero__pill pills
```

### E2E-WVS-003 — Why-visit navy band

```gherkin
Scenario: The why-visit band is the dark destinations grid
  When the browser opens /visit
  Then section.ln-discover.ln-discover--dark renders on a navy (#001640) background
  And its title (Visit.Why.Title, an <h2>) + description render in light text
  And exactly six .ln-dcard cards render (photo + <h3> name + distance + region)
  And under English the first card reads "AlUla", "1,100 km", "Madinah Region"
```

### E2E-WVS-004 — Travel & visa section

```gherkin
Scenario: The travel-&-visa section shows the eVisa summary
  When the browser opens /visit
  Then section.ln-visa renders its band title (Visit.Visa.Title, an <h2>) + subtitle
  And a reused .ln-about__inner renders a .ln-about__media photo and a .ln-about__content column
  And the content shows the tourist-visa heading (Visit.Visa.Heading, an <h3>) and two paragraphs
  And an .ln-visa-cta callout renders with a button and a countries-list title + subtitle
```

### E2E-WVS-005 — Placeholder CTA

```gherkin
Scenario: The eligible-countries CTA is a documented placeholder
  When the browser opens /visit
  Then the .ln-visa-cta button is a <button type="button"> (not an <a> link)
  And it does NOT navigate or open a new tab when activated (no target yet — web/visit.md §7)
  And it carries aria-describedby="visa-countries-label" so it announces its context
```

### E2E-WVS-006 — Static / anonymous

```gherkin
Scenario: The page loads anonymously and fires no API request
  Given a fresh browser with no auth cookie and no bearer token
  When the user opens /visit directly
  Then the page renders WITHOUT redirecting to /login or /not-permitted
  And NO request to /api/... is made and no Authorization header is sent

Scenario: A signed-in session changes nothing
  Given an Approved Visitor is signed in on the Website
  When they open /visit
  Then the rendered page is identical to the anonymous view
```

### E2E-WVS-007 — RTL / LTR

```gherkin
Scenario: The page mirrors between Arabic and English
  When the browser opens /visit under the Arabic UI culture (<html dir="rtl" lang="ar">)
  Then the page mirrors right-to-left, the hero photo sits on the LEFT, content renders Arabic
  And the why-visit title reads "لماذا الزيارة" and the visa title reads "السفر والتأشيرة"

  When the browser opens /visit under the English UI culture (<html dir="ltr" lang="en">)
  Then the hero photo flips to the RIGHT and the content renders English
  And the why-visit title reads "Why visit" and the visa title reads "Travel & visa"
```

### E2E-WVS-008 — Responsive

```gherkin
Scenario: The bands reflow with no horizontal overflow
  When the browser opens /visit and the viewport width is set to each of 1440, 1280, 1024, 768, 390
  Then the .ln-discover__grid shows 3 columns ≥1000px, 2 columns ≤1000px and 1 column ≤640px
  And the visa .ln-about__inner stacks to one column ≤980px and the .ln-visa-cta callout wraps
  And at every width in {1440, 1280, 1024, 768, 390} document.scrollWidth == document.clientWidth (no overflow)
  And this holds in BOTH the EN (LTR) and AR (RTL) cultures
```

---

## Implementation notes

- **Read-only, anonymous, static, no CRUD.** The only "interaction" is the placeholder
  visa CTA (which does nothing yet) and the non-navigating destination cards. The matrix
  above is exhaustive.
- **Supersede.** This catalogue replaced the old MudBlazor visit-entry scenarios
  (SimfBanner + four logistics cards). If the retired attendee-logistics info returns as
  a later section, add scenarios then.
- **Reuse contract.** The why-visit band is single-sourced off `Landing.DiscoverCards`;
  assertions on card counts / labels double as drift guards.
- **Placeholder CTA + image.** The visa CTA has no target and the visa photo is a reused
  placeholder until real assets/URLs are provided (`web/visit.md` §7).
- **Lower-layer coverage:** component (bUnit, no browser)
  `tests/SIMF.Web.Tests/VisitPageTests.cs` pins the hero, the navy band and the visa
  section incl. the placeholder CTA.
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a `.feature`
  under `tests/SIMF.E2E.Tests/`.

---

_Last reviewed:_ 2026-07-19 by Claude (Visiting & travel page — `ln-` Bootstrap SSR, Figma 5867-24636; supersedes the old MudBlazor visit-entry page).
