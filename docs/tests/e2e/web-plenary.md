# E2E test catalogue — Website "Plenary sessions" (`/programme/sessions`)

| | |
|--|--|
| **Page** | [`web/plenary.md`](../../pages/web/plenary.md) |
| **Route** | `/programme/sessions` |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Test runner** | Chrome DevTools MCP (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | **None — anonymous and static.** No API call, no bearer token, no seeding. |
| **Figma** | KSA Maritime Forum — Plenary Sessions (Desktop AR), node `5867-22842` |
| **Last reviewed** | 2026-07-18 |

> **What this page is.** `/programme/sessions` (`Plenary.razor`) is the Website's
> public, anonymous **plenary-sessions overview** (Figma `5867-22842`) — the second
> Programme-cluster page and the ln-styled successor to the old MudBlazor `/programme`.
> Static SSR on the shared `LandingShell` chrome, no CRUD. Two sections:
> 1. **Interior hero** (`ln-pghero`, via `LandingPageHero`) — photo + gradient, the
>    single `<h1>`, a subtitle and the venue + date pills. **No breadcrumb.**
> 2. **Plenary sessions** (`ln-sessions` → 3 `ln-scard`) — the three day cards
>    (Day 1–3), reusing the shared `Landing.Sessions`; each with an "Explore the
>    sessions" CTA linking to the live agenda at `/programme`.
>
> **Auth model (Website, anonymous).** No `RequirePermission`, no `/not-permitted`,
> no `/login` redirect.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WPLN-001 | Golden path — hero + three session cards render + shared chrome, one `<h1>` | happy | P0 | _to author_ |
| E2E-WPLN-002 | Hero has NO breadcrumb (Programme cluster); the venue + date pills render | happy | P1 | _to author_ |
| E2E-WPLN-003 | Session cards — three `ln-scard` reusing `Landing.Sessions` (day badge 1/2/3 + title + text), RTL order Day 1 (right) → Day 3 (left) | happy | P1 | _to author_ |
| E2E-WPLN-004 | CTA — each card's "Explore the sessions" `.ln-scard__btn` is an `<a href="/programme">` (not underlined-blue; hover turns gold) | happy | P1 | _to author_ |
| E2E-WPLN-005 | Static/anonymous — no `/api/...` request, no Authorization header, no `/login` or `/not-permitted` redirect | auth | P0 | _to author_ |
| E2E-WPLN-006 | RTL / Arabic ⇄ LTR / English — mirrors (hero photo side flips); Arabic content in AR, English in EN | i18n | P0 | _to author_ |
| E2E-WPLN-007 | Responsive — the three cards reflow; no horizontal overflow at 1440/1024/768/390 both languages | responsive | P1 | _to author_ |
| E2E-WPLN-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-WPLN-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-WPLN-001 — Golden path

```gherkin
Feature: Website Plenary-sessions page shows the three forum days
  As any visitor (anonymous or signed in)
  I want to see the plenary sessions across the three forum days
  So that I know the programme's main themes

Background:
  Given the Website is reachable
  And the browser is a fresh anonymous session (no auth cookie, no bearer token)

Scenario: The page renders the hero + the three session cards
  When the browser opens /programme/sessions
  Then NO request to /api/... is made (the page is static)
  And the shared header + footer render (LandingShell chrome)
  And an interior hero (section.ln-pghero) renders with exactly one <h1>
  And a sessions section (section.ln-sessions) renders three .ln-scard cards
  And the page title is "Plenary sessions — Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-plenary-ar-1440.png` (AR) + `web-plenary-en-1440.png` (EN)
- Console errors: 0 expected
- Network: no `/api/v1/...` request; the three session-card images return 200
- Audit row: none

### E2E-WPLN-002 — Hero without breadcrumb

```gherkin
Scenario: The Programme-cluster hero omits the breadcrumb
  When the browser opens /programme/sessions
  Then NO .ln-pghero__crumbs breadcrumb element is present
  And the hero renders its <h1> title, subtitle and two .ln-pghero__pill pills
```

### E2E-WPLN-003 — Session cards (reused Landing.Sessions)

```gherkin
Scenario: The three plenary day cards render from the shared session data
  When the browser opens /programme/sessions
  Then section.ln-sessions renders its title (Plenary.Section.Title) + subtitle
  And exactly three .ln-scard cards render
  And each card shows an image, a gold day badge (.ln-scard__num with 1/2/3 + .ln-scard__tag), a title and text
  And under EN the three titles read the Landing.Sessions English titles
      (Securing maritime energy supply chains / Maritime supply chains and logistics infrastructure / Seabed security and digital infrastructure)
  And in AR the cards read right-to-left Day 1 (rightmost) → Day 3 (leftmost)
```

### E2E-WPLN-004 — CTA link

```gherkin
Scenario: Each card CTA links to the live agenda
  When the browser opens /programme/sessions
  Then each .ln-scard__btn "Explore the sessions" element is an <a> whose href is "/programme"
  And it renders as a button (no default underlined-blue link styling) and turns gold on hover
```

### E2E-WPLN-005 — Static / anonymous

```gherkin
Scenario: The page loads anonymously and fires no API request
  Given a fresh browser with no auth cookie and no bearer token
  When the user opens /programme/sessions directly
  Then the page renders WITHOUT redirecting to /login or /not-permitted
  And NO request to /api/... is made and no Authorization header is sent

Scenario: A signed-in session changes nothing
  Given an Approved Visitor is signed in on the Website
  When they open /programme/sessions
  Then the rendered page is identical to the anonymous view
```

### E2E-WPLN-006 — RTL / LTR

```gherkin
Scenario: The page mirrors between Arabic and English
  When the browser opens /programme/sessions under the Arabic UI culture (<html dir="rtl" lang="ar">)
  Then the page mirrors right-to-left, the hero photo sits on the LEFT
  And the cards render Arabic titles/text and RTL card order

  When the browser opens /programme/sessions under the English UI culture (<html dir="ltr" lang="en">)
  Then the hero photo flips to the RIGHT and the cards read English
```

### E2E-WPLN-007 — Responsive

```gherkin
Scenario: The cards reflow with no horizontal overflow
  When the browser opens /programme/sessions and the viewport width is set to each of 1440, 1024, 768, 390
  Then the three .ln-scard cards reflow per the shared ln-sessions rules
  And the hero block goes full-width below 720px
  And at every width in {1440, 1024, 768, 390} document.scrollWidth == document.clientWidth (no horizontal overflow)
  And this holds in BOTH the EN (LTR) and AR (RTL) cultures
```

---

## Implementation notes

- **Read-only, anonymous, static, no CRUD.** The only interaction is the CTA link to
  `/programme`. The matrix above is exhaustive.
- **Pure reuse.** The page adds no CSS/assets/code-behind — it reuses `ln-sessions` /
  `ln-scard` and the shared `Landing.Sessions`. Do not duplicate the session content here.
- **Supersession deferred.** The old `/programme` live agenda + `ProgrammePageTests`
  remain; retiring them is the owner's call (`web/plenary.md` §7).
- **Lower-layer coverage:** component (bUnit, no browser)
  `tests/SIMF.Web.Tests/PlenaryPageTests.cs` pins the render + the reuse + the CTA target.
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a `.feature`
  under `tests/SIMF.E2E.Tests/`.

---

_Last reviewed:_ 2026-07-18 by Claude (Plenary sessions page — `ln-` Bootstrap SSR, Figma 5867-22842).
