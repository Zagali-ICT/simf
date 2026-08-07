# E2E test catalogue — Website "Programme" agenda (`/programme`)

| | |
|--|--|
| **Page** | [`web/programme.md`](../../pages/web/programme.md) |
| **Route** | `/programme` |
| **Surface** | Website (public marketing site - `ln-` Bootstrap SSR) |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later - keep steps tool-agnostic) |
| **Auth setup** | **None - anonymous.** `Programme.razor` calls `SimfPublicClient` (no bearer token); `GET /api/v1/app/programme/sessions` + `GET /api/v1/app/speakers` are `AllowAnonymous()`. (Seed the agenda for the golden path via the Control Panel admin - `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper - creating halls / categories / speakers / typed sessions over the admin API, then drive the public page anonymously.) |
| **Design** | Adapted from the app "Programme schedule" (Figma `883-2308`); **no dedicated website frame** (`web/programme.md` §7). |
| **Last reviewed** | 2026-07-20 |

> **What this page is.** `/programme` (`Programme.razor`) is the Website's public,
> anonymous **agenda**, presented app-style: a **day strip** switches the visible
> day, an optional **type filter** narrows by session kind, and each session is a
> **timeline card**. On load `OnInitializedAsync`:
> 1. `await Api.GetProgrammeSessionsAsync()`. A `null` result (failed envelope /
>    unreachable) sets `_error` and renders a single `ln-agenda__msg` (`role="alert"`).
> 2. otherwise `BuildDays(...)` groups `Items` by the **event-local (+03:00 Riyadh)
>    date** of `Start` (`EventTime.Local(Start).Date`), ordered by day then
>    start time; `_types` = the distinct non-null `SessionType`s. Zero days renders
>    the empty state.
> 3. `await Api.GetSpeakersAsync()` is **best-effort**: a `null` result just leaves
>    the speakers strip empty (never flips to error).
>
> **Structure.** A dark `ln-fsection--dark ln-agenda` band contains: a
> `ln-agenda__daystrip` (`role="group"`) of `ln-agenda__daypill` buttons
> (`aria-pressed`, `data-agenda-day=id`, weekday + date number); a
> `ln-agenda__tabs` type filter (rendered only when `_types` is non-empty) of
> `ln-agenda__tab` buttons (`aria-pressed`, `data-agenda-type`; "All" =
> `data-agenda-type=""`); an `ln-agenda__label` `<h2>` ("Schedule"); and per-day
> `<section class="ln-agenda__day" data-agenda-daypanel=id>` with an `<h3>` date
> heading + a `<ul>` of `li.ln-agenda__card` (`data-agenda-cardtype=<SessionType|"">`).
> Each card = an **aria-hidden** time column (`ln-agenda__when`: `t1` / gold
> `line` / `t2`) + content (optional gold `ln-agenda__cat` chip, `<h4>`
> `ln-agenda__title`, `ln-agenda__hall`, a visually-hidden `ln-agenda__time-sr`
> window for AT, optional `ln-agenda__desc`). A `ln-agenda__none` note replaces a
> day the filter empties.
>
> **Progressive enhancement.** `landing.js` `initAgenda` toggles
> `is-active`/`is-hidden`/`is-empty` + `aria-pressed`, then adds `is-enhanced` to
> the band. The single-day view + the filter are gated on `.is-enhanced`, so with
> **no JS every day + card stays visible** (nothing is hidden, no filtering).
>
> **Single `<h1>`** = the `LandingPageHero` title; day headings are `<h3>`, session
> titles `<h4>`.
>
> **Auth model (Website, anonymous).** No `RequirePermission`, no `/login` or
> `/not-permitted` redirect. The "auth" row asserts the anonymous-by-design contract.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WPG-001 | Golden path - hero + day strip + type filter + timeline cards + speakers; one `<h1>` | happy | P0 | _to author_ |
| E2E-WPG-002 | Day grouping & ordering - group by event-local (+03:00) date, sort by start | happy | P1 | _to author_ |
| E2E-WPG-003 | Day strip switches the visible day (JS) - only the active day shows; `aria-pressed` moves | happy | P0 | _to author_ |
| E2E-WPG-004 | Type filter (data-driven) - tabs render only when types exist; a tab hides non-matching cards; an emptied day shows the note | happy | P0 | _to author_ |
| E2E-WPG-005 | Card content is data-driven - chip (category else theme) + description render when present; both omit when null; Pick fallback | happy | P1 | _to author_ |
| E2E-WPG-006 | Speakers strip is best-effort - 0 rows or a failed `/speakers` omits the strip; agenda still renders | resilience | P1 | _to author_ |
| E2E-WPG-007 | Empty state - 0 sessions => the `ln-fsection` empty state, no error | happy | P1 | _to author_ |
| E2E-WPG-008 | Error state - a failed sessions envelope => `ln-agenda__msg`, no day sections | resilience | P1 | _to author_ |
| E2E-WPG-009 | Server 500 on `/programme/sessions` => null => error block, no unhandled exception | resilience | P2 | _to author_ |
| E2E-WPG-010 | Auth: anonymous-by-design - no Authorization header, no `/login` / `/not-permitted` | auth | P0 | _to author_ |
| E2E-WPG-011 | No-JS fallback - every day + card visible, no filtering, content reachable | resilience | P0 | _to author_ |
| E2E-WPG-012 | RTL / Arabic - day strip + tabs + time column mirror; Arabic content | i18n | P1 | _to author_ |
| E2E-WPG-013 | Responsive - day strip scrolls, tabs wrap, cards stack; no overflow at 1440/1280/1024/768/390 both languages | responsive | P1 | _to author_ |
| E2E-WPG-014 | Reachability - the Programs mega-menu "Full agenda" item opens `/programme` | nav | P1 | _to author_ |
| E2E-WPG-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-WPG-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | 2026-07-29 PASS (LTR+RTL) |

## Scenarios

### E2E-WPG-001 — Golden path

```gherkin
Feature: Website programme agenda shows an app-style schedule
  As any visitor (anonymous or signed in)
  I want a day strip, a type filter and timeline cards
  So that I can browse the forum schedule by day and kind

Background:
  Given the API is reachable on http://localhost:5175 and the Website on http://localhost:5115
  And via the admin API a Hall "Main Hall" / "القاعة الرئيسية" exists
  And a Category "Opening & Welcome" / "الافتتاح والترحيب" exists
  And a Speaker "Dr. Sarah Al-Otaibi" / "د. سارة العتيبي" (rank "Chief Scientist") exists and is active
  And a published Session "Opening Session" / "الجلسة الافتتاحية" in "Main Hall", category "Opening & Welcome",
      type Event, 09:00-10:00 Riyadh on day D, with an Arabic + English description
  And the browser is a fresh anonymous session

Scenario: The agenda renders the day strip, timeline card and speakers
  When the browser opens /programme
  Then the shared header + footer render (LandingShell chrome)
  And an interior hero (section.ln-pghero) renders exactly one <h1> "Programme"
  And GET /api/v1/app/programme/sessions fires with NO Authorization header and returns 200
  And GET /api/v1/app/speakers fires (anonymous) and returns 200
  And a section.ln-fsection--dark.ln-agenda renders
  And a ln-agenda__daystrip renders a ln-agenda__daypill for day D (weekday + date number), aria-pressed="true" once JS runs
  And a ln-agenda__tab type filter renders "All" + "Events" (types present)
  And a ln-agenda__label <h2> reads "Schedule"
  And a ln-agenda__day for day D renders an <h3> date heading and a li.ln-agenda__card
  And the card shows a gold ln-agenda__cat "Opening & Welcome", an <h4> "Opening Session", the hall "Main Hall",
      a ln-agenda__when time column 09:00 / 10:00, and the description
  And a ln-agenda__time-sr window "09:00 – 10:00" is present for assistive tech
  And a navy speakers band (section.ln-fsection--dark) renders a li.ln-agenda__spk "Dr. Sarah Al-Otaibi" + rank "Chief Scientist"
  And the page title is "Programme · Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-programme-en.png` (EN) + `web-programme-ar.png` (AR) + `web-programme-mobile.png`
- Console errors: 0 (the site-wide `favicon.ico` 404 + a benign shared-chrome font-preload warning are allowed)
- Network: `GET /api/v1/app/programme/sessions` + `/speakers` each 200, no Authorization header; hero + footer assets 200
- Audit row: none (read-only anonymous page)

### E2E-WPG-002 — Day grouping & ordering (event-local +03:00)

```gherkin
Scenario: Sessions group by the event-local (+03:00) date and sort by start
  Given sessions: S1 "Afternoon" day D 14:00; S2 "Opening" day D 09:00; S3 "Day-2" day D+1 09:00 (Riyadh)
  When the browser opens /programme
  Then two ln-agenda__day panels render (data-agenda-daypanel "0" then "1"), ascending
  And day 0 lists "Opening" (09:00 – 10:00) before "Afternoon" (14:00 – ...)
  And the grouping key is EventTime.Local(Start).Date (+03:00), not the server-local or UTC date
```

### E2E-WPG-003 — Day strip switches the day (JS)

```gherkin
Scenario: Clicking a day pill shows that day only
  Given the agenda has 3 days and JS is enabled
  When the browser opens /programme
  Then only the first day's ln-agenda__day is visible (is-enhanced hides the others); its pill has aria-pressed="true"
  When the user clicks the 2nd ln-agenda__daypill (data-agenda-day="1")
  Then only day 1's panel is visible; its pill has aria-pressed="true" and the others "false"
```

### E2E-WPG-004 — Type filter (data-driven + empty-day note)

```gherkin
Scenario: The type filter hides non-matching cards and notes an emptied day
  Given day 0 has an Event and a Session, and at least one Workshop exists on another day
  When the browser opens /programme
  Then a ln-agenda__tabs renders "All" (aria-pressed="true") + "Workshops" + "Sessions" + "Events"
  When the user clicks the "Workshops" tab (data-agenda-type="Workshop")
  Then every visible li.ln-agenda__card has data-agenda-cardtype="Workshop" (others get .is-hidden)
  And a day with no Workshop shows the ln-agenda__none note "No sessions match this filter." (its list hidden)
  When the sessions carry NO type at all
  Then the ln-agenda__tabs row is NOT rendered (data-driven)
```

### E2E-WPG-005 — Card content is data-driven

```gherkin
Scenario: The chip + description render only when present, with the shared fallback
  Given session A has category "Maritime Security" (EN+AR) and a description; session B has neither
  When the browser opens /programme
  Then A's card shows a gold ln-agenda__cat chip and a ln-agenda__desc
  And B's card shows neither (Chip = PickOrNull(category) ?? PickOrNull(theme) = null; Description = null)
  And under the English culture, a category set only in Arabic still shows (Pick falls back to the non-blank language)
```

### E2E-WPG-006 — Speakers strip is best-effort

```gherkin
Scenario: No speakers -> the strip is omitted, the agenda still renders
  Given at least one session exists and GET /speakers returns 200 with Items = [] (or fails -> null)
  When the browser opens /programme
  Then the agenda renders and NO section.ln-fsection--dark speakers band is present; no error appears
```

### E2E-WPG-007 — Empty state

```gherkin
Scenario: Zero sessions renders the ln- empty state
  Given GET /programme/sessions returns 200 with Items = []
  When the browser opens /programme
  Then a section.ln-fsection renders <h2> "No sessions yet" + the sub text; no ln-agenda__msg error; the hero still renders
```

### E2E-WPG-008 — Error state

```gherkin
Scenario: A failed sessions envelope shows the ln- error block
  Given GET /programme/sessions returns Success=false (client -> null)
  When the browser opens /programme
  Then a single p.ln-agenda__msg (role="alert") renders "The programme could not be loaded. Please try again."
  And NO day strip / cards / speakers render; no /speakers request fires
```

### E2E-WPG-009 — Server 500

```gherkin
Scenario: API 500 degrades to the error block with no unhandled exception
  Given GET /programme/sessions returns HTTP 500
  When the browser opens /programme
  Then SimfPublicClient returns null (never throws); the ln-agenda__msg error renders; no console exception; the speakers call is not reached
```

### E2E-WPG-010 — Auth (anonymous by design)

```gherkin
Scenario: Reachable anonymously; never redirects to login / not-permitted
  Given a fresh browser with no auth cookie / bearer token
  When the user opens /programme
  Then the page renders WITHOUT redirecting to /login or /not-permitted
  And the sessions request carries NO Authorization header; the API does not 401/403
  And a signed-in session changes nothing (SimfPublicClient attaches no bearer)
```

### E2E-WPG-011 — No-JS fallback

```gherkin
Scenario: With JS disabled every day + card is visible and reachable
  Given the browser has JavaScript disabled (initAgenda never runs)
  When the user opens /programme
  Then the ln-agenda band has NO .is-enhanced class
  And EVERY ln-agenda__day panel is visible (no day is display:none) with its <h3> date heading
  And NO ln-agenda__card is hidden and NO ln-agenda__none note shows
  And the day pills + type tabs render but are inert (no filtering) - all content is reachable by scrolling
```

### E2E-WPG-012 — RTL / Arabic

```gherkin
Scenario: The agenda mirrors and renders Arabic under the Arabic culture
  When the browser opens /programme under <html dir="rtl" lang="ar">
  Then the <h1> reads "البرنامج"; the ln-agenda__label reads "المواعيد"
  And the ln-agenda__daystrip + ln-agenda__tabs sit inline-start (right); the day pills read e.g. "الجمعة 20"
  And the tabs read "الكل / ورش العمل / جلسات / الأحداث"
  And each card's ln-agenda__when time column sits on the RIGHT; the chip/title/hall/description render Arabic
  And the ln-agenda__spkrank keeps its English value but is tagged lang="en"
  And no Latin text leaks where an Arabic value exists (Pick prefers the *Arabic value)
```

### E2E-WPG-013 — Responsive

```gherkin
Scenario: The agenda reflows with no horizontal overflow
  When the viewport width is set to each of 1440, 1280, 1024, 768, 390
  Then at every width document.scrollWidth == document.clientWidth (no horizontal overflow)
  And the ln-agenda__daystrip scrolls horizontally if the days overflow; the ln-agenda__tabs wrap
  And at <= 640px each ln-agenda__card stacks (flex-direction: column) and its ln-agenda__when goes horizontal
  And this holds in BOTH the EN (LTR) and AR (RTL) cultures
```

### E2E-WPG-014 — Reachability via the nav

```gherkin
Scenario: The "Full agenda" nav item opens the page
  Given the browser is on any Website page with the shared nav header
  When the user opens the "Programmes" mega-menu and clicks "Full agenda" / "الأجندة الكاملة"
  Then the browser navigates to /programme
```

---

## Implementation notes

- **Read-only, anonymous.** The only interactions are the day strip + type filter
  (client-side JS) and navigations; no CRUD, no server round-trip on filter.
- **Filter contract.** The tab's `data-agenda-type` and the card's
  `data-agenda-cardtype` both come from `SessionType.ToString()`
  (`Workshop`/`Session`/`Event`); the JS filter is string-equality, so the two
  must not drift (pinned by the bUnit multi-day/typed test).
- **Progressive enhancement.** The single-day view + filter are gated on
  `.is-enhanced` (JS-added), so WPG-011 (no-JS) is the safety contract: all
  content reachable without JS.
- **Lower-layer coverage:** component (bUnit) `tests/SIMF.Web.Tests/ProgrammePageTests.cs`
  pins the render branches + the day-id / filter / chip contract via a stub
  `SimfPublicClient`; API integration `ProgrammeSessionsTests` + `PublicSpeakersTests`
  prove the wire contract. The JS toggle path (WPG-003/004/011) is browser-only.
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a
  `.feature` under `tests/SIMF.E2E.Tests/`.

---

_Last reviewed:_ 2026-07-20 by Claude (Programme agenda - app-style day strip + type filter + timeline cards, `ln-` SSR, live data).
