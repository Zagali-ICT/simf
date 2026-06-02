# E2E test catalogue — Visit & entry (`/visit`)

| | |
|--|--|
| **Page** | [`web/visit.md`](../../pages/web/visit.md) |
| **Route** | `/visit` |
| **Surface** | Website |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | **None required.** `/visit` is a public, anonymous, read-only page (no `@attribute [Authorize]`, no `@rendermode`, no API client). It renders for a fresh browser with no auth cookie. (The `Get-Totp` helper / `superadmin@zagali-ict.com` setup is irrelevant here and is only carried in the table for catalogue consistency.) |
| **Last reviewed** | 2026-06-02 |

> **What this page is.** `/visit` (`Visit.razor`) is the Website's public
> **Visit & entry** information page — static SSR, **informational only**. By
> deliberate design there is **no public gate-data endpoint** (gate / check-in
> data is operator-only), so the page renders clean bilingual static copy via
> `IStringLocalizer<Strings>` `L["Visit.*"]` and calls **no API client** — it
> never fabricates gate data. It composes a `SimfBanner` (which renders the
> page `<h1>`) plus **four** `simf-card simf-page-card` sections, each an
> `<h2>` + supporting paragraph(s):
>
> 1. **Getting here** — two paragraphs (`Visit.GettingHere.Body`, `Visit.GettingHere.Transport`)
> 2. **Entry & badges** — two paragraphs (`Visit.Entry.Badge`, `Visit.Entry.SignUp`)
> 3. **Opening hours** — one paragraph (`Visit.Hours.Body`)
> 4. **Accessibility** — one paragraph (`Visit.Accessibility.Body`)
>
> **Auth model (Website, not CP).** This is a public Website page reachable by
> anyone, signed in or not. There is **no** `RequirePermission` /
> `/not-permitted` gate (that is the Control-Panel pattern) and **no**
> unauthenticated → `/login` redirect (that is the signed-in Website pattern).
> The "auth" scenario here is the opposite assertion: an anonymous visitor
> **can** open `/visit` and read it without ever signing in.
>
> **No CRUD, no forms, no buttons.** The page has zero interactive controls —
> no grid, modal, filter, toggle, form field, or submit. The only navigation
> affordance reaching this route is the `MainLayout` public-nav anchor
> `<a href="/visit">@L["Nav.Visit"]</a>`. Do **not** author grid / modal /
> validation / duplicate scenarios that the page does not have.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WVS-001 | Golden path — anonymous browser opens `/visit`, banner + all four cards render, no API/console error | happy | P0 | _to author_ |
| E2E-WVS-002 | Banner — page `<h1>` title + subtitle render from `Visit.Banner.*` | happy | P1 | _to author_ |
| E2E-WVS-003 | "Getting here" card — `<h2>` + both supporting paragraphs render | happy | P1 | _to author_ |
| E2E-WVS-004 | "Entry & badges" card — `<h2>` + QR-badge + sign-up paragraphs render | happy | P1 | _to author_ |
| E2E-WVS-005 | "Opening hours" card — `<h2>` + body paragraph render | happy | P2 | _to author_ |
| E2E-WVS-006 | "Accessibility" card — `<h2>` + body paragraph render | happy | P2 | _to author_ |
| E2E-WVS-007 | Public nav — `MainLayout` "Visit" link routes here from any public page | happy | P1 | _to author_ |
| E2E-WVS-008 | Anonymous access — no auth cookie, no `/login` redirect, no `/not-permitted`, no auth API call | auth | P0 | _to author_ |
| E2E-WVS-009 | No fabricated gate data — page issues **zero** `/account/api/*` or `/api/v1/*` requests | resilience | P1 | _to author_ |
| E2E-WVS-010 | Accessibility wiring — `aria-labelledby` links each card to its `<h2>` id; single `<main>` landmark | a11y | P1 | _to author_ |
| E2E-WVS-011 | RTL / Arabic render — `/culture?culture=ar` mirrors the page, all copy in Arabic | i18n | P1 | _to author_ |

## Scenarios

### E2E-WVS-001 — Golden path

```gherkin
Feature: Visit & entry public information page
  As a prospective attendee (no account, or signed in — it does not matter)
  I want to read how to get to the forum, how entry works, hours, and accessibility
  So that I am prepared before I travel to the event

Background:
  Given the Website is reachable on http://localhost:5115
  And the browser has no SIMF auth cookie (a fresh / anonymous session)

Scenario: An anonymous visitor opens /visit and reads the whole page
  When the visitor navigates to /visit
  Then the response is HTTP 200 (no redirect to /login and no /not-permitted)
  And the document title is "Visit & entry · Saudi International Maritime Forum"
  And the SimfBanner renders the page <h1> "Visit & entry"
  And the banner subtitle reads "Everything you need to know before you arrive at the forum."
  And exactly four simf-page-card sections render in order:
    | order | h2 title        |
    | 1     | Getting here    |
    | 2     | Entry & badges  |
    | 3     | Opening hours   |
    | 4     | Accessibility   |
  And no /account/api/... or /api/v1/... network request fires (the page calls no API client)
  And the browser console logs 0 errors
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-visit-golden.png` (banner + all four cards in one viewport / full-page capture)
- Console errors: 0 expected
- Network: **0** application API calls — only the static SSR document + CSS/JS assets; assert no request path contains `/account/api/` or `/api/v1/`
- Audit row: **none** — `/visit` is read-only and does not write to `OperationLog` / `RowAudit`

### E2E-WVS-002 — Banner

```gherkin
Scenario: The SimfBanner renders the page title and subtitle
  Given the visitor is on /visit
  Then there is exactly one <h1> on the page (the SimfBanner title) reading "Visit & entry"
  And a banner subtitle paragraph reads "Everything you need to know before you arrive at the forum."
  And the four card titles are <h2> elements (the banner owns the only <h1>)
```

### E2E-WVS-003 — "Getting here" card

```gherkin
Scenario: Getting here card shows its heading and both supporting paragraphs
  Given the visitor is on /visit
  Then a section with aria-labelledby="visit-getting-here-title" is present
  And its <h2 id="visit-getting-here-title"> reads "Getting here"
  And it contains a paragraph reading "The forum is held at the official event venue. The full address and a venue map will be published here closer to the event. Please follow on-site signage and the directions of stewards on arrival."
  And it contains a second paragraph reading "Parking and transport details, including the nearest access routes, will be announced ahead of the opening day."
```

### E2E-WVS-004 — "Entry & badges" card

```gherkin
Scenario: Entry & badges card explains the QR-badge flow and sign-up nudge
  Given the visitor is on /visit
  Then a section with aria-labelledby="visit-entry-title" is present
  And its <h2 id="visit-entry-title"> reads "Entry & badges"
  And it contains a paragraph reading "Entry is by QR badge. After your account is approved, open the SIMF mobile app to display your personal QR badge, which staff scan at the entrance to check you in."
  And it contains a second paragraph reading "Do not have an account yet? Sign up in the SIMF app before you travel so your badge is ready in time for entry."
```

### E2E-WVS-005 — "Opening hours" card

```gherkin
Scenario: Opening hours card shows its heading and body
  Given the visitor is on /visit
  Then a section with aria-labelledby="visit-hours-title" is present
  And its <h2 id="visit-hours-title"> reads "Opening hours"
  And it contains a paragraph reading "The forum opens daily during the event dates. Exact opening and closing times for each day will be confirmed here before the event begins."
```

### E2E-WVS-006 — "Accessibility" card

```gherkin
Scenario: Accessibility card shows its heading and body
  Given the visitor is on /visit
  Then a section with aria-labelledby="visit-accessibility-title" is present
  And its <h2 id="visit-accessibility-title"> reads "Accessibility"
  And it contains a paragraph reading "The venue is designed to be accessible to visitors with reduced mobility. If you have specific access requirements, please contact your event coordinator in advance so we can assist you on the day."
```

### E2E-WVS-007 — Public nav link

```gherkin
Scenario: The MainLayout public-nav "Visit" link routes to /visit
  Given the visitor is on any public Website page that uses MainLayout (e.g. /programme)
  Then the layout <nav> shows two secondary-button anchors: "Programme" and "Visit"
  When the visitor clicks the "Visit" link (href="/visit")
  Then the browser lands on /visit
  And the "Visit & entry" banner <h1> renders
```

### E2E-WVS-008 — Anonymous access (no auth gate)

```gherkin
Scenario: An unauthenticated visitor can read /visit without signing in
  Given there is no signed-in session (fresh browser, no auth cookie)
  When the visitor opens /visit directly
  Then the page renders with HTTP 200
  And the visitor is NOT redirected to /login
  And the visitor is NOT redirected to /not-permitted
  And no /api/v1/auth/... request fires (the page performs no authentication)
  And the full Visit & entry content is visible
```

> **Note (public Website page).** Unlike a Control-Panel page, `/visit` has
> **no** `RequirePermission` and never routes to `/not-permitted`; unlike a
> signed-in Website page (e.g. `/account`), it has **no** unauthenticated →
> `/login` redirect. It is intentionally readable by anyone. There is no
> negative auth case to author — the assertion is that access succeeds.

### E2E-WVS-009 — No fabricated gate data

```gherkin
Scenario: The page never requests operator-only gate / check-in data
  Given the visitor is on /visit
  When the page has fully rendered (SSR document + assets settled)
  Then the network panel shows zero requests whose path contains "/account/api/"
  And zero requests whose path contains "/api/v1/"
  And no gate, check-in, or attendance data is shown — only the static bilingual copy
```

> Gate / check-in data is operator-only by design (D-comment in `Visit.razor`).
> This page deliberately ships static placeholder copy rather than calling a
> public gate endpoint that does not exist, so the correct production-readiness
> assertion is the **absence** of any data API call.

### E2E-WVS-010 — Accessibility wiring

```gherkin
Scenario: Each card is labelled by its heading and the landmark is singular
  Given the visitor is on /visit
  Then each of the four <section class="simf-card simf-page-card"> elements carries an aria-labelledby
  And each aria-labelledby points to the id of that section's <h2>:
    | section            | aria-labelledby            |
    | Getting here       | visit-getting-here-title   |
    | Entry & badges     | visit-entry-title          |
    | Opening hours      | visit-hours-title          |
    | Accessibility      | visit-accessibility-title  |
  And the page content sits inside the single MainLayout <main id="main-content"> landmark
  And there is exactly one <h1> (the banner) and the four card titles are <h2>
```

### E2E-WVS-011 — RTL / Arabic render

```gherkin
Scenario: The Arabic culture mirrors the page and shows Arabic copy
  Given the visitor is on /visit in English
  When the visitor switches culture via GET /culture?culture=ar&redirectUri=%2Fvisit
  Then the document renders with <html lang="ar" dir="rtl">
  And the document title reads "الزيارة والدخول · الملتقى البحري السعودي الدولي"
  And the banner <h1> reads "الزيارة والدخول"
  And the banner subtitle reads "كل ما تحتاج معرفته قبل وصولك إلى الملتقى."
  And the four card titles read, in order: "الوصول إلى الموقع", "الدخول والبطاقات", "ساعات العمل", "إمكانية الوصول"
  And the layout is mirrored right-to-left (cards and nav buttons reverse)
  And no Latin body copy leaks into the Arabic layout
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-visit-rtl.png` (Arabic, `dir="rtl"`)
- Console errors: 0 expected
- Network: still **0** application API calls under Arabic culture (the `/culture` redirect is the only extra request, and it is an infra endpoint, not `/api/v1/...`)

---

## Implementation notes

- **Static SSR, informational only.** `Visit.razor` has no `@rendermode`, no
  `@attribute [Authorize]`, no API client and no interactive controls. It
  renders four bilingual `simf-page-card` sections via `L["Visit.*"]` over a
  `SimfBanner`. The catalogue is authored against that real composition — there
  are no CRUD, validation, conflict/duplicate, or server-500-from-the-page
  cases to write, because the page issues no request that could fail. The
  resilience angle for a page like this is the **absence** of API calls
  (E2E-WVS-009), not a 500 fallback.
- **Public, not gated.** Reachable anonymously; no `/not-permitted` (CP
  pattern) and no `/login` redirect (signed-in Website pattern). E2E-WVS-008
  asserts the positive: anonymous access succeeds.
- **Strings are the contract.** All copy is verified against
  `src/Website/SIMF.Web/Resources/Strings.resx` and `Strings.ar.resx`
  (`Visit.PageTitle`, `Visit.Banner.Title/Subtitle`,
  `Visit.GettingHere.Title/Body/Transport`, `Visit.Entry.Title/Badge/SignUp`,
  `Visit.Hours.Title/Body`, `Visit.Accessibility.Title/Body`). If a string
  changes, update the matching scenario assertion in the same changeset.
- **Culture switch.** Arabic is selected via the shared
  `GET /culture?culture=ar&redirectUri=...` endpoint (same mechanism the auth
  pages use through `SimfLanguageSwitch`); `App.razor` then emits
  `<html lang dir>` from `CultureInfo.CurrentUICulture`. `/visit` itself has no
  language-switch control on the page — the switch is exercised via the
  endpoint / a page that hosts the switch.
- **Lower-layer coverage.** No API integration test under
  `tests/SIMF.Api.Tests/` backs this page, because it has no backing API by
  design. Coverage is purely the rendered SSR markup; the appropriate
  lower-layer test (if added later) would be a `bUnit` render test asserting
  the banner + four cards + `aria-labelledby` wiring, not an `Api.Tests` case.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin
  scenario into a `.feature` under `tests/SIMF.E2E.Tests/` (project TBD) with a
  step-definition class. The steps are already runner-agnostic.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
