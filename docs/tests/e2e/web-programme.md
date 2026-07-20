# E2E test catalogue — Website "Programme" agenda (`/programme`)

| | |
|--|--|
| **Page** | [`web/programme.md`](../../pages/web/programme.md) |
| **Route** | `/programme` |
| **Surface** | Website (public marketing site - `ln-` Bootstrap SSR) |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later - keep steps tool-agnostic) |
| **Auth setup** | **None - the page is anonymous.** `Programme.razor` calls `SimfPublicClient`, which carries **no bearer token**; both `GET /api/v1/app/programme/sessions` and `GET /api/v1/app/speakers` are `AllowAnonymous()`. A signed-in session is neither required nor read. (Seeding the agenda for the golden path uses the Control Panel admin - `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper - to create halls / themes / speakers / sessions over the admin API, then the public page is driven anonymously.) |
| **Figma** | No dedicated website frame - this is an `ln-`-idiom rebuild reusing the kit (see `web/programme.md` §7). |
| **Last reviewed** | 2026-07-20 |

> **What this page is.** `/programme` (`Programme.razor`) is the Website's public,
> anonymous **full agenda**, re-skinned from the legacy `Simf*` / `MainLayout`
> page onto the shared `ln-` marketing kit (`LandingShell` + `LandingPageHero` +
> a new `ln-agenda` band). The data flow is unchanged. On load
> `OnInitializedAsync`:
> 1. `await Api.GetProgrammeSessionsAsync()` -> `GET /api/v1/app/programme/sessions`.
>    A `null` result (failed envelope / unreachable service) sets `_error` and the
>    page renders a single `ln-agenda__msg` block (`role="alert"`).
> 2. otherwise `BuildDays(...)` groups `Items` by the **local calendar date** of
>    `StartUtc` (`StartUtc.ToLocalTime().Date`), ordered by day then by start time
>    within each day. Zero days renders the `ln-fsection` empty state.
> 3. `await Api.GetSpeakersAsync()` -> `GET /api/v1/app/speakers` is **best-effort**:
>    a `null` result leaves the speakers strip empty and does **not** flip the
>    page into the error state.
>
> **Layout of the agenda.** One `ln-agenda__day` group per date, each with an
> `<h2>` `ln-agenda__dayhead` and a `ul.ln-agenda__list` of `li.ln-agenda__row`.
> Per row: `ln-agenda__time` (the `HH:mm - HH:mm` local window), an
> `ln-agenda__main` block holding the `<h3>` `ln-agenda__title` and the
> `ln-agenda__hall`, and - only when the session carries a primary theme name
> (resolved for the current culture) - a neutral `ln-agenda__pill`.
>
> **Speakers strip** (best-effort, on a navy `ln-fsection--dark` band): the
> `Programme.Speakers.Title` heading + `li.ln-agenda__spk` chips, each an
> `ln-agenda__spkname` and - when present - an `ln-agenda__spkrank` (`lang="en"`).
>
> **Bilingual fallback.** `Title` / `Hall` / `ThemeName` / `SpeakerName` use the
> Arabic-preferred-then-English `Pick(...)` helper: in an Arabic UI use the
> `*Arabic` value when present, else the base value; a null base renders empty.
> The theme pill is gated on the **resolved** name, so it never paints an empty
> chip. The day heading uses `dddd, d MMMM yyyy` in `CurrentUICulture`.
>
> **Single `<h1>`.** The page's only `<h1>` is the `LandingPageHero` title
> (`Programme.Banner.Title`); day headings are `<h2>`, session titles `<h3>`.
>
> **Auth model (Website, anonymous).** This is **not** a Control-Panel page: there
> is **no** `RequirePermission`, no `/not-permitted` gate, and **no**
> unauthenticated -> `/login` redirect. The page is reachable by anyone. The "auth"
> row asserts the *anonymous-by-design* contract, not a redirect.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WPG-001 | Golden path - `ln-` hero + day-grouped agenda rows + best-effort speakers strip + shared chrome, exactly one `<h1>` | happy | P0 | _to author_ |
| E2E-WPG-002 | Day grouping & ordering - sessions group by local calendar date and sort by start time within each day | happy | P1 | _to author_ |
| E2E-WPG-003 | Theme pill is conditional - no theme name renders no `ln-agenda__pill`; a theme paints the neutral pill; a theme named only in the *other* language renders no empty chip | happy | P1 | _to author_ |
| E2E-WPG-004 | Speakers strip is best-effort - 0 rows or a failed `/speakers` read omits the strip; the agenda still renders (no error state) | resilience | P1 | _to author_ |
| E2E-WPG-005 | Empty state - `GET /programme/sessions` returns 0 items => the `ln-fsection` empty state (`Programme.Empty.Title`), no error block | happy | P1 | _to author_ |
| E2E-WPG-006 | Auth: anonymous-by-design - page loads with no Authorization header, no `/login` and no `/not-permitted` redirect | auth | P0 | _to author_ |
| E2E-WPG-007 | Error state - `GET /programme/sessions` fails (null envelope / unreachable) => the `ln-agenda__msg` alert, no day sections | resilience | P1 | _to author_ |
| E2E-WPG-008 | Server 500 on `/programme/sessions` => client maps to null => error block, no unhandled exception | resilience | P2 | _to author_ |
| E2E-WPG-009 | Speakers-only resilience - sessions 200 but `/speakers` 500 => agenda renders, strip absent, no error state | resilience | P2 | _to author_ |
| E2E-WPG-010 | RTL / Arabic render - `ln-` mirror: hero photo to the left, right-aligned headings, Arabic day headings / titles / halls / theme pills / speaker names | i18n | P1 | _to author_ |
| E2E-WPG-011 | Responsive - agenda rows wrap and speakers chips reflow; no horizontal overflow at 1440/1280/1024/768/390 in both languages | responsive | P1 | _to author_ |
| E2E-WPG-012 | Reachability - the Programs mega-menu "Full agenda" item opens `/programme` | nav | P1 | _to author_ |

## Scenarios

### E2E-WPG-001 — Golden path

```gherkin
Feature: Website programme agenda renders the published forum schedule on the ln- kit
  As any visitor (anonymous or signed in)
  I want to see the full forum agenda grouped by day
  So that I can plan which sessions and halls to attend

Background:
  Given the API is reachable on http://localhost:5175
  And the Website is reachable on http://localhost:5115
  And - to seed the agenda - an Administrator has signed into the Control Panel
      (superadmin@zagali-ict.com + TOTP via the Get-Totp helper)
  And via the admin API a Hall "Main Hall" / "القاعة الرئيسية" exists
  And a Theme "Keynote" / "الكلمة الرئيسية" exists
  And a Speaker "Dr. Sarah Al-Otaibi" / "د. سارة العتيبي" (rank "Chief Scientist") exists and is active
  And an active published Session "Opening Keynote" / "الكلمة الافتتاحية" is scheduled
      in "Main Hall", tagged with the "Keynote" theme, from 09:00 to 10:30 local on day D
  And the browser is a fresh anonymous session (no auth cookie, no bearer token)

Scenario: The published agenda renders on the shared ln- chrome
  When the browser opens /programme
  Then the shared header + footer render (LandingShell chrome)
  And an interior hero (section.ln-pghero) renders with exactly one <h1> reading "Programme"
  And a GET /api/v1/app/programme/sessions request fires with NO Authorization header and returns 200
  And a GET /api/v1/app/speakers request fires (anonymous) and returns 200
  And a day group (.ln-agenda__day) renders with an <h2> .ln-agenda__dayhead formatted "dddd, d MMMM yyyy" for day D
  And inside it a li.ln-agenda__row shows .ln-agenda__time "09:00 – 10:30", an <h3> .ln-agenda__title "Opening Keynote", and a .ln-agenda__hall "Main Hall"
  And that row shows a neutral .ln-agenda__pill reading "Keynote"
  And a navy speakers band (section.ln-fsection--dark) renders a li.ln-agenda__spk with .ln-agenda__spkname "Dr. Sarah Al-Otaibi" and a .ln-agenda__spkrank "Chief Scientist"
  And neither the empty state nor the .ln-agenda__msg error is present
  And the page title is "Programme · Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-programme-en-1440.png` (EN) + `web-programme-ar-1440.png` (AR)
- Console errors: 0 expected (the site-wide `favicon.ico` 404 and a benign shared-chrome font-preload warning are allowed)
- Network: `GET /api/v1/app/programme/sessions` and `GET /api/v1/app/speakers` each 200 (ApiResult envelope, `Success = true`); no Authorization header on either; the hero + footer assets return 200
- Audit row: none - `/programme` is a read-only anonymous page and writes no `OperationLog` / `RowAudit` row

### E2E-WPG-002 — Day grouping & ordering

```gherkin
Scenario: Sessions group by local calendar date and sort by start time within each day
  Given three active sessions exist:
    | code | title            | start (local)  | end (local)    |
    | S1   | Afternoon Panel  | day D 14:00     | day D 15:00     |
    | S2   | Opening Keynote  | day D 09:00     | day D 10:30     |
    | S3   | Day-2 Workshop   | day D+1 09:00   | day D+1 11:00   |
  When the browser opens /programme
  Then exactly two .ln-agenda__day groups render, in ascending day order: day D then day D+1
  And the day-D group lists "Opening Keynote" (09:00 – 10:30) before "Afternoon Panel" (14:00 – 15:00)
  And the day-D+1 group lists "Day-2 Workshop" (09:00 – 11:00)
  And the grouping key is the LOCAL calendar date of StartUtc (StartUtc.ToLocalTime().Date), not the UTC date
```

> **Note.** `BuildDays` orders by `StartUtc`, groups by `StartUtc.ToLocalTime().Date`,
> then orders the groups by date. When asserting the heading text, compute it from
> the test machine's local timezone, not from UTC. (Server-local grouping is the
> pre-existing behaviour; see `web/programme.md` §7.4 for the event-offset note.)

### E2E-WPG-003 — Conditional theme pill

```gherkin
Scenario: The neutral theme pill renders only when a resolved theme name is present
  Given an active session "Networking Break" exists with NO theme tagged
      (PrimaryThemeName and PrimaryThemeNameArabic both null/blank)
  And an active session "Opening Keynote" exists tagged with the "Keynote" theme (both languages)
  When the browser opens /programme
  Then the "Opening Keynote" row renders a neutral .ln-agenda__pill reading "Keynote"
  And the "Networking Break" row renders NO .ln-agenda__pill at all
  And both rows still render their .ln-agenda__time and .ln-agenda__hall

Scenario: A theme named only in the other language does not paint an empty chip
  Given an active session "Tech Demo" is tagged with a theme whose Arabic name is set but English name is blank
  When the browser opens /programme under the English UI culture
  Then the "Tech Demo" row renders NO .ln-agenda__pill (the pill is gated on the resolved, culture-picked name)
  When the browser opens /programme under the Arabic UI culture
  Then the "Tech Demo" row renders a .ln-agenda__pill with the Arabic theme name
```

### E2E-WPG-004 — Speakers strip is best-effort

```gherkin
Scenario: No speakers -> the speakers strip is omitted, the agenda still renders
  Given at least one active session exists
  And GET /api/v1/app/speakers returns 200 with Data.Items = []
  When the browser opens /programme
  Then the .ln-agenda__day groups render normally
  And NO section.ln-fsection--dark speakers band is present (the strip renders only when _speakers.Count > 0)
  And no .ln-agenda__msg error appears

Scenario: A speakers fetch failure does not break the agenda
  Given at least one active session exists (GET /programme/sessions returns 200)
  And GET /api/v1/app/speakers returns a failed/unreachable result (the client maps it to null)
  When the browser opens /programme
  Then the .ln-agenda__day groups still render
  And the speakers strip is omitted
  And _error stays false - the page does NOT show the .ln-agenda__msg error
```

### E2E-WPG-005 — Empty state

```gherkin
Scenario: Zero published sessions renders the ln- empty state
  Given GET /api/v1/app/programme/sessions returns 200 with Data.Items = []
  When the browser opens /programme
  Then BuildDays produces zero day groups (_days.Count == 0)
  And a section.ln-fsection renders a .ln-fsection__head with an <h2> reading "No sessions yet" / "لا توجد جلسات بعد"
  And a .ln-fsection__sub reads "The agenda has not been published yet. Please check back soon." / "لم يتم نشر الأجندة بعد. يُرجى التحقق لاحقًا."
  And no .ln-agenda__msg error appears
  And the ln-pghero hero still renders the "Programme" title + subtitle
```

### E2E-WPG-006 — Auth gate (anonymous by design)

```gherkin
Scenario: The page is reachable anonymously and never redirects to a login or not-permitted page
  Given a fresh browser with no auth cookie and no bearer token
  When the user opens /programme directly
  Then the page renders (agenda or empty state) WITHOUT redirecting to /login
  And the page does NOT redirect to /not-permitted
  And the GET /api/v1/app/programme/sessions request carries NO Authorization header
  And the API does not return 401/403 for the public reads

Scenario: A signed-in session changes nothing on this page
  Given an Approved Visitor is signed in on the Website
  When they open /programme
  Then the same anonymous public reads fire (no bearer token is attached by SimfPublicClient)
  And the rendered agenda is identical to the anonymous view
```

### E2E-WPG-007 — Error state (sessions fetch returns null)

```gherkin
Scenario: A failed sessions envelope shows the ln- error block
  Given GET /api/v1/app/programme/sessions returns a body whose ApiResult envelope has Success = false
      (so SimfPublicClient.GetProgrammeSessionsAsync returns null)
  When the browser opens /programme
  Then OnInitializedAsync sets _error = true and returns early
  And the page renders a single p.ln-agenda__msg with role="alert"
  And the alert reads "The programme could not be loaded. Please try again." / "تعذّر تحميل البرنامج. حاول مرة أخرى."
  And NO .ln-agenda__day group, NO empty state, and NO speakers strip render
  And no GET /api/v1/app/speakers request fires (the method returned before the speakers call)
```

### E2E-WPG-008 — Server 500 on /programme/sessions

```gherkin
Scenario: API 500 on the sessions list degrades to the error block with no unhandled exception
  Given GET /api/v1/app/programme/sessions returns HTTP 500
  When the browser opens /programme
  Then SimfPublicClient reads the body and, on a failed/non-JSON envelope, returns null (it never throws)
  And the page sets _error = true and renders the .ln-agenda__msg error
  And no unhandled exception reaches the browser console
  And the speakers call is not reached
```

### E2E-WPG-009 — Speakers-only resilience (sessions OK, speakers 500)

```gherkin
Scenario: A 500 on /speakers leaves the agenda intact
  Given GET /api/v1/app/programme/sessions returns 200 with at least one session
  And GET /api/v1/app/speakers returns HTTP 500
  When the browser opens /programme
  Then GetSpeakersAsync returns null (no throw)
  And the speakers strip is omitted
  And the .ln-agenda__day groups render normally
  And _error stays false - NO .ln-agenda__msg error renders
  And no unhandled exception reaches the browser console
```

### E2E-WPG-010 — RTL / Arabic render

```gherkin
Scenario: The agenda mirrors and renders Arabic content under an Arabic UI culture
  Given the seeded session has TitleArabic="الكلمة الافتتاحية", HallNameArabic="القاعة الرئيسية", PrimaryThemeNameArabic="الكلمة الرئيسية"
  And the seeded speaker has NameArabic="د. سارة العتيبي"
  When the browser opens /programme under the Arabic UI culture (<html dir="rtl" lang="ar">)
  Then the page mirrors right-to-left and the ln-pghero hero photo sits on the LEFT
  And the <h1> reads "البرنامج"
  And the .ln-agenda__dayhead renders in the Arabic culture's "dddd, d MMMM yyyy" form
  And the .ln-agenda__title shows "الكلمة الافتتاحية" (Pick prefers the *Arabic value)
  And the .ln-agenda__hall shows "القاعة الرئيسية" and the .ln-agenda__pill shows "الكلمة الرئيسية"
  And the speakers band heading reads "المتحدثون" and a .ln-agenda__spkname shows "د. سارة العتيبي"
  And the .ln-agenda__spkrank keeps its English value but is tagged lang="en"
  And no Latin text leaks where an Arabic value is present

Scenario: Arabic fallback when an *Arabic field is blank
  Given a session has Title="Tech Demo" but TitleArabic is blank
  When the page renders under the Arabic culture
  Then the .ln-agenda__title falls back to the English "Tech Demo" (Pick returns the base when arabic is blank)
```

### E2E-WPG-011 — Responsive / no horizontal overflow

```gherkin
Scenario: The agenda + speakers reflow with no horizontal overflow
  When the browser opens /programme and the viewport width is set to each of 1440, 1280, 1024, 768, 390
  Then at every width document.scrollWidth == document.clientWidth (no horizontal overflow)
  And at <= 640px each .ln-agenda__row wraps so the .ln-agenda__time takes its own full-width line and the .ln-agenda__pill drops to the start
  And the speakers chips (.ln-agenda__spk) wrap onto multiple rows
  And this holds in BOTH the EN (LTR) and AR (RTL) cultures
```

### E2E-WPG-012 — Reachability via the nav

```gherkin
Scenario: The "Full agenda" nav item opens the page
  Given the browser is on any Website page with the shared nav header
  When the user opens the "Programmes" mega-menu and clicks "Full agenda" / "الأجندة الكاملة"
  Then the browser navigates to /programme
```

---

## Implementation notes

- **Read-only, anonymous, no CRUD.** `/programme` has no button, modal, form,
  filter, toggle or grid action - the matrix above is exhaustive for the page's
  *actual* behaviour (load -> group -> render, plus the best-effort speakers strip
  and the three terminal states: agenda / empty / error).
- **ln- re-skin.** The 2026-07-20 changeset replaced the legacy `SimfBanner` /
  `SimfEmptyState` / `SimfAlert` / `SimfPill` / `simf-card` DOM with the shared
  `ln-` kit (`ln-pghero` hero, `ln-fsection` sections, the new `ln-agenda*` band,
  the `ln-fsection--dark` speakers strip). Assert the `ln-` classes above, not the
  retired `Simf*` ones.
- **Day-filter parameter not used by this page.** The API supports
  `GET /api/v1/app/programme/sessions?day=yyyy-MM-dd`, but `Programme.razor` calls
  the unfiltered `GetProgrammeSessionsAsync()` and groups client-side. The
  malformed-`day` -> 400 path is exercised at the API layer instead.
- **Lower-layer coverage:**
  - component (bUnit, no browser) `tests/SIMF.Web.Tests/ProgrammePageTests.cs` pins
    the three render branches + the `ln-` DOM (`ln-pghero`, `ln-agenda__row`,
    `ln-agenda__spk`, `ln-fsection`, `ln-agenda__msg`) via a stub `SimfPublicClient`.
  - API integration `tests/SIMF.Api.Tests/ProgrammeSessionsTests.cs` +
    `PublicSpeakersTests.cs` prove the wire contract (anonymous, ordering,
    soft-delete drop-off, 400/404) at a lower layer.
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a
  `.feature` under `tests/SIMF.E2E.Tests/`. The steps are already runner-agnostic.

---

_Last reviewed:_ 2026-07-20 by Claude (Programme agenda page - `ln-` Bootstrap SSR re-skin; live agenda + speakers strip).
