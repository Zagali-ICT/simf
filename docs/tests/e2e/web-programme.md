# E2E test catalogue — Website programme / agenda (`/programme`)

| | |
|--|--|
| **Page** | [`web/programme.md`](../../pages/web/programme.md) _(reference doc not yet authored — this catalogue is grounded directly in `Programme.razor`)_ |
| **Route** | `/programme` |
| **Surface** | Website |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | **None — the page is anonymous.** `Programme.razor` calls `SimfPublicClient`, which carries **no bearer token**; both `GET /api/v1/programme/sessions` and `GET /api/v1/speakers` are `AllowAnonymous()`. A signed-in session is neither required nor read. (Seeding the agenda for the golden path uses the Control Panel admin — `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper — to create halls / themes / speakers / sessions over the admin API, then the public page is driven anonymously.) |
| **Last reviewed** | 2026-06-02 |

> **What this page is.** `/programme` (`Programme.razor`) is the Website's
> public, anonymous **agenda** (D-199, Mockup page 16 "Agenda"). It is a
> read-only SSR page with no CRUD, no modal, no form, no toggle and no
> button. On load `OnInitializedAsync`:
> 1. `await Api.GetProgrammeSessionsAsync()` → `GET /api/v1/programme/sessions`.
>    A `null` result (failed envelope / unreachable service) sets `_error = true`
>    and the page renders a single `SimfAlert` error.
> 2. otherwise `BuildDays(...)` groups `Items` by the **local calendar date** of
>    `StartUtc` (`StartUtc.ToLocalTime().Date`), ordered by day then by start time
>    within each day. Zero days renders `SimfEmptyState`.
> 3. `await Api.GetSpeakersAsync()` → `GET /api/v1/speakers` is **best-effort**:
>    a `null` result leaves the speakers strip empty and does **not** flip the
>    page into the error state.
>
> **Per row** (within a day `<section class="simf-card">`): a `simf-card__supporting`
> line `"{HH:mm} – {HH:mm} — {Title}"` (local time window via `TimeWindow`, current
> culture), a second supporting line for the hall (`Hall`), and — only when the
> session carries a primary theme name (EN **or** AR) — a neutral `SimfPill` with
> the theme name.
>
> **Bilingual fallback.** `Title` / `Hall` / `ThemeName` / `SpeakerName` use the
> Arabic-preferred-then-English `Pick(...)` helper: in an Arabic UI use the `*Arabic`
> value when present, else the base value; a null base renders empty. The day
> heading uses `dddd, d MMMM yyyy` in `CurrentUICulture`.
>
> **Auth model (Website, anonymous).** This is **not** a Control-Panel page: there
> is **no** `RequirePermission`, no `/not-permitted` gate, and **no** unauthenticated
> → `/login` redirect. The page is reachable by anyone, signed in or not. The
> "auth" row below therefore asserts the *anonymous-by-design* contract (the page
> loads with no Authorization header and never 401s), not a redirect.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WPG-001 | Golden path — published agenda renders day sections, each session row (time window — title, hall, theme pill) + the speakers strip | happy | P0 | _to author_ |
| E2E-WPG-002 | Day grouping & ordering — sessions group by local calendar date and sort by start time within each day | happy | P1 | _to author_ |
| E2E-WPG-003 | Theme pill is conditional — a session with no primary theme name renders no `SimfPill`; one with a theme name renders the neutral pill | happy | P1 | _to author_ |
| E2E-WPG-004 | Speakers strip is best-effort — `GET /speakers` returns 0 rows ⇒ no speakers section; speakers fetch fails ⇒ agenda still renders (no error state) | resilience | P1 | _to author_ |
| E2E-WPG-005 | Empty state — `GET /programme/sessions` returns 0 items ⇒ `SimfEmptyState` ("No sessions yet") and no error alert | happy | P1 | _to author_ |
| E2E-WPG-006 | Auth: anonymous-by-design — page loads with no Authorization header, no `/login` redirect, no `/not-permitted` | auth | P0 | _to author_ |
| E2E-WPG-007 | Error state — `GET /programme/sessions` fails (null envelope / unreachable) ⇒ `SimfAlert` error, no day sections | resilience | P1 | _to author_ |
| E2E-WPG-008 | Server 500 on `/programme/sessions` ⇒ client maps to null ⇒ error alert, no unhandled exception | resilience | P2 | _to author_ |
| E2E-WPG-009 | Speakers-only resilience — sessions 200 but `/speakers` returns 500 ⇒ agenda renders, speakers strip absent, no error alert | resilience | P2 | _to author_ |
| E2E-WPG-010 | RTL / Arabic render — day headings, titles, halls, theme pills and speaker names render Arabic; page mirrors right-to-left | i18n | P1 | _to author_ |

## Scenarios

### E2E-WPG-001 — Golden path

```gherkin
Feature: Website programme agenda renders the published forum schedule
  As any visitor (anonymous or signed in)
  I want to see the full forum agenda grouped by day
  So that I can plan which sessions and halls to attend

Background:
  Given the API is reachable on http://localhost:5175
  And the Website is reachable on http://localhost:5115
  And — to seed the agenda — an Administrator has signed into the Control Panel
      (superadmin@zagali-ict.com + TOTP via the Get-Totp helper)
  And via the admin API a Hall "Main Hall" / "القاعة الرئيسية" (capacity 120) exists
  And a Theme "Keynote" / "الكلمة الرئيسية" (a colour) exists
  And a Speaker "Dr. Sarah Al-Otaibi" / "د. سارة العتيبي" (rank "Chief Scientist") exists and is active
  And an active published Session "Opening Keynote" / "الكلمة الافتتاحية" is scheduled
      in "Main Hall", tagged with the "Keynote" theme, from 09:00 to 10:30 local on day D
  And the browser is a fresh anonymous session (no auth cookie, no bearer token)

Scenario: The published agenda renders one day section with the seeded session and the speakers strip
  When the browser opens /programme
  Then a GET /api/v1/programme/sessions request fires with NO Authorization header and returns 200
  And the ApiResult envelope is Success = true with Data.Items containing the "Opening Keynote" session
  And a GET /api/v1/speakers request fires (anonymous) and returns 200
  And the SimfBanner title reads "Programme" and the subtitle reads "The full forum agenda — sessions, halls and times."
  And a day <section class="simf-card"> renders with an h2 heading formatted "dddd, d MMMM yyyy" for day D (e.g. "Wednesday, 3 June 2026")
  And inside that section a list item shows the supporting line "09:00 – 10:30 — Opening Keynote"
  And a second supporting line shows the hall "Main Hall"
  And a neutral SimfPill shows the theme name "Keynote"
  And a "Speakers" section renders with a list item "Dr. Sarah Al-Otaibi" and a neutral pill "Chief Scientist"
  And neither the SimfEmptyState nor the SimfAlert error is present
  And the page title is "Programme · Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/web-programme-agenda-before.png` (full agenda with the day section + speakers strip)
- Screenshot after: `docs/screenshots/web-programme-agenda-after.png` (session row + theme pill in focus)
- Console errors: 0 expected
- Network: `GET /api/v1/programme/sessions` and `GET /api/v1/speakers` each return 200 (ApiResult envelope, `Success = true`); no other `/api/v1/...` call fires from this page; neither call carries an Authorization header
- Audit row: none — `/programme` is a read-only anonymous page and writes no `OperationLog` / `RowAudit` row

### E2E-WPG-002 — Day grouping & ordering

```gherkin
Scenario: Sessions group by local calendar date and sort by start time within each day
  Given three active sessions exist:
    | code | title            | start (local)        | end (local)          |
    | S1   | Afternoon Panel  | day D 14:00          | day D 15:00          |
    | S2   | Opening Keynote  | day D 09:00          | day D 10:30          |
    | S3   | Day-2 Workshop   | day D+1 09:00        | day D+1 11:00        |
  When the browser opens /programme
  Then exactly two day <section> cards render, in ascending day order: day D then day D+1
  And the day-D section lists "Opening Keynote" (09:00 – 10:30) before "Afternoon Panel" (14:00 – 15:00)
  And the day-D+1 section lists "Day-2 Workshop" (09:00 – 11:00)
  And the grouping key is the LOCAL calendar date of StartUtc (StartUtc.ToLocalTime().Date), not the UTC date
```

> **Note.** `BuildDays` orders by `StartUtc`, then groups by
> `StartUtc.ToLocalTime().Date`, then orders the groups by date — so a session
> whose UTC instant falls on a different local day appears under the **local**
> day. When asserting the heading text, compute it from the test machine's local
> timezone, not from UTC.

### E2E-WPG-003 — Conditional theme pill

```gherkin
Scenario: The neutral theme pill renders only when a primary theme name is present
  Given an active session "Networking Break" / "استراحة تواصل" exists in a hall with NO theme tagged
      (PrimaryThemeName and PrimaryThemeNameArabic both null/blank)
  And an active session "Opening Keynote" exists tagged with the "Keynote" theme
  When the browser opens /programme
  Then the "Opening Keynote" row renders a neutral SimfPill reading "Keynote"
  And the "Networking Break" row renders NO SimfPill at all
  And both rows still render their time-window line and their hall line
```

### E2E-WPG-004 — Speakers strip is best-effort

```gherkin
Scenario: No speakers ⇒ the speakers section is omitted, the agenda still renders
  Given at least one active session exists
  And GET /api/v1/speakers returns 200 with Data.Items = [] (no active speakers)
  When the browser opens /programme
  Then the day sections render normally
  And NO "Speakers" section is present (the strip renders only when _speakers.Count > 0)
  And no error alert appears

Scenario: A speakers fetch failure does not break the agenda
  Given at least one active session exists (GET /programme/sessions returns 200)
  And GET /api/v1/speakers returns a failed/unreachable result (the client maps it to null)
  When the browser opens /programme
  Then the day sections still render
  And the speakers strip is empty (omitted)
  And _error stays false — the page does NOT show the SimfAlert error
```

> The page comment is explicit: "a failure to load [speakers] must not turn the
> whole agenda into an error state, so a null result just leaves the strip empty."

### E2E-WPG-005 — Empty state

```gherkin
Scenario: Zero published sessions renders SimfEmptyState
  Given the database has no active published Session rows
  And GET /api/v1/programme/sessions returns 200 with Data.Items = []
  When the browser opens /programme
  Then BuildDays produces zero day sections (_days.Count == 0)
  And the page renders the SimfEmptyState component
  And the empty state title reads "No sessions yet" / "لا توجد جلسات بعد"
  And the empty state description reads "The agenda has not been published yet. Please check back soon." / "لم يتم نشر الأجندة بعد. يُرجى التحقق لاحقًا."
  And no SimfAlert error appears
  And the SimfBanner still renders the "Programme" title + subtitle
```

### E2E-WPG-006 — Auth gate (anonymous by design)

```gherkin
Scenario: The page is reachable anonymously and never redirects to a login or not-permitted page
  Given a fresh browser with no auth cookie and no bearer token
  When the user opens /programme directly
  Then the page renders (agenda or empty state) WITHOUT redirecting to /login
  And the page does NOT redirect to /not-permitted (that is a Control-Panel concept, not present here)
  And the GET /api/v1/programme/sessions request carries NO Authorization header
  And the API does not return 401/403 for the public reads

Scenario: A signed-in session changes nothing on this page
  Given an Approved Visitor is signed in on the Website
  When they open /programme
  Then the same anonymous public reads fire (no bearer token is attached by SimfPublicClient)
  And the rendered agenda is identical to the anonymous view
```

> **Note (Website, not CP).** Unlike a Control-Panel page, `/programme` has **no**
> `RequirePermission` attribute and never routes to `/not-permitted`. `SimfPublicClient`
> deliberately attaches no bearer token; both backing endpoints are `AllowAnonymous()`.
> The "auth gate" for this page is the *absence* of any gate — assert that the
> anonymous read succeeds and that no redirect occurs.

### E2E-WPG-007 — Error state (sessions fetch returns null)

```gherkin
Scenario: A failed sessions envelope shows the SimfAlert error
  Given GET /api/v1/programme/sessions returns a body whose ApiResult envelope has Success = false
      (so SimfPublicClient.GetProgrammeSessionsAsync returns null)
  When the browser opens /programme
  Then OnInitializedAsync sets _error = true and returns early
  And the page renders a single SimfAlert with Variant="error"
  And the alert reads "The programme could not be loaded. Please try again." / "تعذّر تحميل البرنامج. حاول مرة أخرى."
  And NO day sections, NO empty state, and NO speakers strip render
  And no GET /api/v1/speakers request fires (the method returned before reaching the speakers call)
```

### E2E-WPG-008 — Server 500 on /programme/sessions

```gherkin
Scenario: API 500 on the sessions list degrades to the error alert with no unhandled exception
  Given GET /api/v1/programme/sessions returns HTTP 500 (e.g. the DB is down)
  When the browser opens /programme
  Then SimfPublicClient reads the body and, on a failed/non-JSON envelope, returns null (it never throws for HttpRequestException / JsonException / timeout)
  And the page sets _error = true
  And the SimfAlert error renders ("The programme could not be loaded. Please try again.")
  And no unhandled exception reaches the browser console
  And the speakers call is not reached
```

### E2E-WPG-009 — Speakers-only resilience (sessions OK, speakers 500)

```gherkin
Scenario: A 500 on /speakers leaves the agenda intact
  Given GET /api/v1/programme/sessions returns 200 with at least one session
  And GET /api/v1/speakers returns HTTP 500
  When the browser opens /programme
  Then GetSpeakersAsync returns null (no throw)
  And the speakers strip is omitted
  And the day sections render normally
  And _error stays false — NO SimfAlert error renders
  And no unhandled exception reaches the browser console
```

### E2E-WPG-010 — RTL / Arabic render

```gherkin
Scenario: The agenda mirrors and renders Arabic content under an Arabic UI culture
  Given the seeded session has TitleArabic="الكلمة الافتتاحية", HallNameArabic="القاعة الرئيسية", PrimaryThemeNameArabic="الكلمة الرئيسية"
  And the seeded speaker has NameArabic="د. سارة العتيبي"
  When the browser opens /programme under the Arabic UI culture (<html dir="rtl" lang="ar">)
  Then the page mirrors right-to-left
  And the SimfBanner title reads "البرنامج" and the subtitle reads "الأجندة الكاملة للملتقى — الجلسات والقاعات والأوقات."
  And the day heading renders in the Arabic culture's "dddd, d MMMM yyyy" form
  And the session line shows the Arabic title "الكلمة الافتتاحية" (Pick prefers the *Arabic value)
  And the hall line shows "القاعة الرئيسية"
  And the theme pill shows "الكلمة الرئيسية"
  And the "Speakers" heading reads "المتحدثون" and the speaker name shows "د. سارة العتيبي"
  And no Latin text leaks where an Arabic value is present (the English base is used only when the *Arabic field is blank)

Scenario: Arabic fallback when an *Arabic field is blank
  Given a session has Title="Tech Demo" but TitleArabic is blank
  When the page renders under the Arabic culture
  Then the session line falls back to the English "Tech Demo" (Pick returns the base when arabic is blank)
```

---

## Implementation notes

- **Read-only, anonymous, no CRUD.** `/programme` has no button, modal, form,
  filter, toggle or grid action — the matrix above is exhaustive for the page's
  *actual* behaviour (load → group → render, plus the best-effort speakers strip
  and the three terminal states: agenda / empty / error). Do not invent Add/Edit/
  Delete or permission scenarios that the page does not have.
- **Anonymous by design.** `SimfPublicClient` carries no bearer token and both
  backing endpoints are `AllowAnonymous()` (`ListProgrammeSessionsEndpoint` and
  `ListPublicSpeakersEndpoint`). The "auth" scenario (E2E-WPG-006) asserts the
  anonymous contract, not a `/login` or `/not-permitted` redirect (that is the
  Control-Panel pattern, absent here).
- **Day-filter parameter not used by this page.** The API supports
  `GET /api/v1/programme/sessions?day=yyyy-MM-dd` (the Day 1/2/3 segmented control
  for the Flutter agenda), but `Programme.razor` calls the unfiltered
  `GetProgrammeSessionsAsync()` and groups client-side. The malformed-`day` →
  400 path (`ErrorCodes.SessionInvalid`) is therefore **out of scope for this
  Website page** and is exercised at the API layer instead (see below).
- **Lower-layer coverage (API integration tests, no browser):**
  - `tests/SIMF.Api.Tests/ProgrammeSessionsTests.cs` covers the backing
    `GET /api/v1/programme/sessions` (+ `{id}`): `Public_list_is_anonymous_and_returns_active_session_with_hall_and_theme`,
    `Public_list_is_ordered_by_start_time`, `Day_filter_restricts_to_that_utc_calendar_day`,
    `Malformed_day_filter_is_rejected_with_400`, `Detail_returns_speakers_themes_and_seat_summary`,
    `Capacity_override_drives_the_seat_summary_capacity`,
    `Soft_deleted_session_drops_off_list_and_detail_404s`, `Unknown_session_id_returns_404`.
  - `tests/SIMF.Api.Tests/PublicSpeakersTests.cs` covers the backing
    `GET /api/v1/speakers` (+ `{id}`): `Public_list_is_anonymous_and_succeeds`,
    `Created_active_speaker_appears_in_public_list_and_detail`,
    `Public_list_is_ordered_by_display_order`, `Public_detail_unknown_id_returns_404`,
    `Deactivated_speaker_is_absent_from_list_and_detail_404s`,
    `Detail_returns_the_speakers_active_sessions`,
    `Social_urls_are_hidden_unless_speaker_consents_to_data_sharing`.
  These prove the wire contract (anonymous, ordering, soft-delete drop-off, 400/404)
  at a lower layer; the E2E scenarios above prove the *rendered* agenda — day
  grouping by local time, the conditional theme pill, the best-effort speakers
  strip, and the three terminal UI states.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin scenario
  into a `.feature` under `tests/SIMF.E2E.Tests/` (project TBD) with a
  step-definition class. The steps are already runner-agnostic.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
