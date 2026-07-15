# E2E test catalogue — Website session detail (`/sessions/{id}`)

| | |
|--|--|
| **Page** | [`web/session-detail.md`](../../pages/web/session-detail.md) |
| **Route** | `/sessions/{id:guid}` |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | **None — the page is anonymous.** `SessionDetail.razor` calls `SimfPublicClient` (no bearer); `GET /api/v1/app/programme/sessions/{id}` + the agenda list are `AllowAnonymous()`. Seeding a session (with themes / speakers / language / outcomes / presentation files) uses the Control Panel admin — `superadmin@zagali-ict.com` + TOTP via `Get-Totp` — over the admin API, then the page is driven anonymously. |
| **Figma** | KSA Maritime Forum — Session Detail (Desktop AR), node `5991-85840` |
| **Last reviewed** | 2026-07-15 |

> **What this page is.** `/sessions/{id}` (`SessionDetail.razor` + `.razor.cs`) is
> the Website's public, anonymous **session detail** (Figma `5991-85840`) on the
> shared `LandingShell`. On load `OnInitializedAsync` does
> `Session = await Api.GetSessionAsync(Id)` (`GET /programme/sessions/{id}`); a
> `null` result (unknown / unpublished id, or unreachable service) renders the
> **not-found** state (`SessionDetail.NotFound.*`) and the page returns. Otherwise
> it also fetches the agenda once for a **best-effort related strip**
> (`GetProgrammeSessionsAsync`, other sessions, this one filtered out, first 3 by
> start) — a null agenda just leaves the strip empty and never errors.
>
> **Seven sections** (each data-driven; the optional ones omit gracefully when empty):
> 1. **Hero** (`ln-sesshero`) — navy gradient band: breadcrumb, a gold day chip
>    (the session's weekday), the single `<h1>` title, the description lead, and
>    four gold-tinted meta pills (time window · date · hall · category) with gold
>    `ln-ico` glyphs. Times render in event-local (Riyadh +03:00).
> 2. **At-a-glance** (`ln-glance`) — a sidebar card of label/value rows: track
>    (category), language, hall, capacity (`{n} seats`), live broadcast (Yes/No).
>    A row omits when its data is absent (track / language are optional).
> 3. **Overview** (`ln-sessabout`) — "Why this session matters" + the description.
> 4. **Key themes** (`ln-tcard` grid) — one gold-badged navy card per tagged theme,
>    name + the theme **description** (sourced from `Theme.Description`). Section
>    omits when the session has no themes.
> 5. **Speakers** (`ln-spkcard` grid, **reused** from the Speakers page) — ringed
>    photo (via the `/content/assets/SpeakerPhoto/{id}/image` proxy when the
>    speaker has an asset), name, gold role pill, gray country pin.
> 6. **Related** (`ln-rcard` grid) — up to 3 other sessions, each a link to its
>    own `/sessions/{id}`. Omits when empty.
> 7. **Downloads + outcomes** (`ln-docrow` / `ln-outcomes`) — the session's
>    presentation files (public download links) and the "key outcomes" bullets.
>    Each omits when empty.
>
> **Public downloads (owner decision 2026-07-15).** Each download links to the
> same-origin `/content/sessions/{sessionId}/downloads/{presentationId}` proxy →
> the anonymous API route `GET /app/sessions/{sessionId}/downloads/{presentationId}`,
> which validates the presentation belongs to that session (the session scope is
> the authorisation in place of a signed-in account) and streams the file as an
> attachment. Distinct from the signed-in `/app/presentations/{id}/file` route.
>
> **Bilingual fallback.** Title / description / hall / theme name+desc / speaker
> name / outcome / language all use the Arabic-preferred-in-RTL `Pick(...)` helper.
> Chrome + section labels come from `IStringLocalizer<Strings>` (`SessionDetail.*`).
>
> **Auth model.** Anonymous by design — no `RequirePermission`, no `/login`
> redirect, no `/not-permitted`. A signed-in session changes nothing.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WSDT-001 | Golden path — a fully-seeded session renders all seven sections (hero, at-a-glance, overview, themes, speakers, related, downloads + outcomes) | happy | P0 | _to author_ |
| E2E-WSDT-002 | Hero — breadcrumb + gold day chip + title + description + four gold-tinted meta pills (time/date/hall/category) with visible gold icons; times in Riyadh +03:00 | happy | P1 | _to author_ |
| E2E-WSDT-003 | At-a-glance — track/language rows render only when present; hall/capacity/live always; capacity reads "{n} seats"; live reads Yes when LiveStreamUrl set else No | happy | P1 | _to author_ |
| E2E-WSDT-004 | Key themes — one `ln-tcard` per tagged theme with the theme description; the whole section omits when the session has no themes | happy | P1 | _to author_ |
| E2E-WSDT-005 | Speakers — the `ln-spkcard` grid (photo via the asset proxy, name, role pill, country pin); empty-state text when the session has no speakers | happy | P1 | _to author_ |
| E2E-WSDT-006 | Related strip — up to 3 other sessions, this one excluded, each linking to its own `/sessions/{id}`; strip omits when the agenda is empty / unreachable | resilience | P1 | _to author_ |
| E2E-WSDT-007 | Downloads — each file links to `/content/sessions/{sessionId}/downloads/{presentationId}`; clicking downloads the file (attachment, original name); section omits when the session has none | happy | P0 | _to author_ |
| E2E-WSDT-008 | Public download authorisation — the anonymous route streams a file that belongs to the session; a presentationId from a DIFFERENT session 404s (session scope is the authz) | auth | P0 | _to author_ |
| E2E-WSDT-009 | Outcomes — one checklist item per outcome (gold check), in display order; section omits when the session has none | happy | P1 | _to author_ |
| E2E-WSDT-010 | Not found — an unknown / unpublished id renders the not-found state (title + body + "Back to the programme" link), no hero, no error crash | resilience | P0 | _to author_ |
| E2E-WSDT-011 | Auth: anonymous-by-design — page + downloads load with no Authorization header, no `/login` redirect; a signed-in session changes nothing | auth | P0 | _to author_ |
| E2E-WSDT-012 | RTL / Arabic render — all sections render Arabic (Arabic-preferred), the page mirrors right-to-left, hero + card icons render in their token colour (never invisible) | i18n | P1 | _to author_ |
| E2E-WSDT-013 | Responsive — themes/related grids step 3→2→1 (1100/860), the overview + downloads/outcomes two-column layouts stack (900), no horizontal overflow at 1440/1024/768/390 in both languages | responsive | P1 | _to author_ |

## Scenarios

### E2E-WSDT-001 — Golden path

```gherkin
Feature: Website session detail renders one published session in full
  As any visitor (anonymous or signed in)
  I want to see a session's overview, themes, speakers, related sessions and materials
  So that I can decide to attend and prepare

Background:
  Given the API is reachable on http://localhost:5175
  And the Website is reachable on http://localhost:5115
  And — to seed — an Administrator has signed into the Control Panel (superadmin@zagali-ict.com + TOTP via Get-Totp)
  And via the admin API a Hall "Main Hall" / "القاعة الرئيسية" (capacity 400) exists
  And a Theme "Maritime Security" / "الأمن البحري" with a description exists and is tagged on the session
  And a Speaker "Dr. Sarah Al-Otaibi" / "د. سارة العتيبي" (rank "Chief Scientist", country Saudi Arabia, a photo) is on the session
  And the session has Language "English & Arabic" / "الإنجليزية والعربية", two key outcomes, and one uploaded presentation file "agenda.pdf"
  And an active published Session "Securing Maritime Trade Routes" / "تأمين طرق التجارة البحرية" is scheduled in "Main Hall" 09:00–10:30 (Riyadh) on a Friday
  And the browser is a fresh anonymous session

Scenario: A fully-seeded session renders all seven sections
  When the browser opens /sessions/{id}
  Then a GET /api/v1/app/programme/sessions/{id} request fires with NO Authorization header and returns 200
  And the hero (section.ln-sesshero) shows the breadcrumb, a gold chip "Friday", the h1 "Securing Maritime Trade Routes", the lead, and pills for 09:00–10:30, the date, "Main Hall" and the category
  And the at-a-glance card (.ln-glance) shows Track, Language "English & Arabic", Hall "Main Hall", Capacity "400 seats" and Live broadcast
  And a key-themes card (.ln-tcard) shows "Maritime Security" with its description
  And a speaker card (.ln-spkcard) shows "Dr. Sarah Al-Otaibi", the "Chief Scientist" pill and "Saudi Arabia"
  And an outcomes checklist (.ln-outcomes__item) lists the two key outcomes
  And a download row (.ln-docrow) links "agenda.pdf" to /content/sessions/{id}/downloads/{presentationId}
  And a "Continue your agenda" strip lists up to 3 other sessions, each linking to /sessions/{otherId}
  And the not-found state is NOT present
  And the page title is "Securing Maritime Trade Routes — Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshots: `docs/screenshots/web-session-detail-hero-*.png`, `…-glance-*.png` (AR + EN @1440)
- Console errors: 0 expected
- Network: `GET /programme/sessions/{id}` + `GET /programme/sessions` each 200, no Authorization header; the speaker photo + each download proxy 200
- Audit row: none — read-only anonymous page

### E2E-WSDT-007 / 008 — Public downloads + session-scoped authorisation

```gherkin
Scenario: A session presentation downloads anonymously from the website
  Given the session has an active presentation "agenda.pdf"
  When the browser opens /sessions/{id} and clicks the "agenda.pdf" download row
  Then a GET /content/sessions/{id}/downloads/{presentationId} request returns 200 with Content-Disposition: attachment; filename="agenda.pdf"
  And the file bytes download (no sign-in required)
  And the request carries NO Authorization header

Scenario: A presentation from another session is not reachable via this session's route
  Given presentation P belongs to session OTHER (not this one)
  When a client requests GET /api/v1/app/sessions/{thisSessionId}/downloads/{P} (anonymous)
  Then the API returns 404 (the session scope is the authorisation — P is not in thisSession)
  And no bytes are served
```

### E2E-WSDT-010 — Not found

```gherkin
Scenario: An unknown or unpublished session id renders the not-found state
  Given no active session with id {id} exists (or GetSessionAsync returns null)
  When the browser opens /sessions/{id}
  Then the page renders the .ln-sessmissing block with the title "Session not found" / "الجلسة غير موجودة"
  And a body line and a "Back to the programme" link to /programme
  And NO hero / at-a-glance / themes / speakers render
  And no unhandled exception reaches the console
```

### E2E-WSDT-012 — RTL / Arabic render

```gherkin
Scenario: The detail mirrors and renders Arabic under an Arabic UI culture
  When the browser opens /sessions/{id} under the Arabic UI culture (<html dir="rtl" lang="ar">)
  Then the page mirrors right-to-left; the at-a-glance card sits on the right, the overview text on the left
  And the hero title shows "تأمين طرق التجارة البحرية" and the pills / chip render Arabic
  And the hero pill icons + card icons render in their token colour (gold on the hero, gold-dark on the badges) — never invisible
  And the theme card shows the Arabic name + Arabic description; the speaker shows "د. سارة العتيبي"; outcomes render Arabic
  And no Latin text leaks where an Arabic value is present
```

### E2E-WSDT-013 — Responsive

```gherkin
Scenario: The layout reflows with no horizontal overflow
  When the viewport is set to each of 1440, 1100, 900, 860, 390
  Then the themes + related grids render 3 columns at ≥1101px, 2 at ≤1100px and 1 at ≤860px
  And the overview and downloads/outcomes two-column grids stack to one column at ≤900px
  And at every width in {1440, 1024, 768, 390} document.scrollWidth == document.clientWidth (no horizontal overflow), in BOTH languages
```

---

## Implementation notes

- **Read-only, anonymous.** No CRUD, no form, no toggle. The matrix is exhaustive
  for the page's behaviour: load → render (or not-found), the best-effort related
  strip, and the seven data-driven sections that each omit when empty.
- **Public downloads are a deliberate policy (owner, 2026-07-15).** The website
  download route is `AllowAnonymous()`; the session-scope check
  (`presentation.SessionId == sessionId`) replaces the signed-in gate the app's
  `/app/presentations` read uses. Scenario WSDT-008 pins that scope.
- **Lower-layer coverage:**
  - Component (bUnit): `tests/SIMF.Web.Tests/SessionDetailPageTests.cs` —
    `Renders_all_sections_when_the_session_loads` (all 7 sections + the download
    proxy URL), `Renders_the_not_found_state_for_an_unknown_id`,
    `Omits_the_data_sections_that_are_empty`.
  - API integration: `tests/SIMF.Api.Tests/PublicSessionDetailTests.cs` /
    `ProgrammeSessionsTests.cs` cover the anonymous detail read; the new
    session-scoped download route is exercised at the API layer (belongs-to-session
    404). CP editing of language + outcomes is covered by `cp-admin-sessions.md`
    (E2E-SES-*) and the admin session integration tests.
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a
  `.feature`; the steps are already runner-agnostic.

---

_Last reviewed:_ 2026-07-15 by Claude (Session Detail — `ln-` Bootstrap SSR, Figma 5991-85840).
