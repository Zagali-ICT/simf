# E2E test catalogue — Programme run of show (`/admin/programme/timeline`)

| | |
|--|--|
| **Page** | [`cp/admin-programme-timeline.md`](../../pages/cp/admin-programme-timeline.md) |
| **Route** | `/admin/programme/timeline` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page shape (grounding).** This is a **read-only** at-a-glance overview — there is
> **no create / edit / delete** here (that grid lives at `/admin/sessions`). The page
> reads the existing BFF route `POST /account/api/admin/sessions/list` once with
> `GridQuery { Top = 500 }`, then groups the returned `AdminSessionSummary` items by the
> **local calendar day** of `Start.LocalDateTime`, days ascending, sessions within a
> day ascending by start time. Every scenario below is grounded in the real elements of
> `ProgrammeTimeline.razor`: the two `SimfStatCard`s (Days / Sessions), the day-filter
> `<select>`, the per-day `<h2>` heading + `simf-table` (columns Time / Code / Session /
> Hall) + per-day count line, the `SimfEmptyState`, and the `SimfAlert` error toast.
>
> **Permission asymmetry to verify.** The CP page is gated by
> `PermissionCatalog.ProgrammeTimeline.View`, but the BFF/API list route it calls
> (`/admin/sessions/list`) is gated by `PermissionCatalog.Sessions.View`. A role granted
> `ProgrammeTimeline.View` but **not** `Sessions.View` reaches the page yet gets a 403
> from the list call → the page must surface the load-failure toast (see E2E-PTL-009).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-PTL-001 | Golden path — page loads, stats + day sections + tables render from the live `/sessions/list` round-trip | happy | P0 | _to author_ |
| E2E-PTL-002 | Stat cards reflect the data — Days = distinct local days, Sessions = total item count | happy | P1 | _to author_ |
| E2E-PTL-003 | Day filter — "All days" → single day → back to all days | function | P0 | _to author_ |
| E2E-PTL-004 | Day grouping + ordering — days ascending, rows ascending by start, multi-hall same slot | function | P1 | _to author_ |
| E2E-PTL-005 | Time-window + per-day count rendering (`HH:mm – HH:mm`, "{N} session(s) on this day") | function | P1 | _to author_ |
| E2E-PTL-006 | Empty state — no sessions → `SimfEmptyState` with bilingual copy, no filter/stats | happy | P1 | _to author_ |
| E2E-PTL-007 | Auth gate — signed-in admin lacking `ProgrammeTimeline.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-PTL-008 | Read-only guarantee — no Add/Edit/Delete/row actions anywhere on the page | function | P1 | _to author_ |
| E2E-PTL-009 | Permission asymmetry — has `ProgrammeTimeline.View` but not `Sessions.View` → list 403 → error toast | error | P1 | _to author_ |
| E2E-PTL-010 | Server 500 on `/sessions/list` → bilingual fallback toast, no tables | resilience | P2 | _to author_ |
| E2E-PTL-011 | RTL / Arabic render — page + headings + table mirror, Arabic titles/halls, Arabic day headings | i18n | P1 | _to author_ |
| E2E-PTL-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-PTL-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-PTL-001 — Golden path

```gherkin
Feature: Programme run-of-show overview
  As an Administrator with ProgrammeTimeline.View
  I want the whole agenda on one screen grouped by day
  So that I can read the run of show at a glance without editing anything

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And at least two sessions on two different calendar days exist via /admin/sessions
  And they have navigated to /admin/programme/timeline

Scenario: Timeline loads and renders day sections from the live round-trip
  When the page initialises
  Then exactly one POST /account/api/admin/sessions/list fires with body {"Top":500}
  And it returns HTTP 200 with an ApiResult envelope where Success=true
  And the loading line "Loading the programme…" is no longer shown
  And the SimfBanner title reads "Programme run of show"
  And the subtitle reads "The full agenda on one screen, grouped by day."
  And two SimfStatCard tiles appear titled "Days" and "Sessions"
  And a "Day" filter <select> appears with a first option "All days"
  And for each distinct day a <h2> heading appears formatted "dddd, d MMMM yyyy" (e.g. "Monday, 8 December 2025")
  And under each heading a simf-table renders with column headers "Time", "Code", "Session", "Hall"
  And each row shows the session's time window, code, English title and English hall name
  And below each table a muted line reads "{N} session(s) on this day" matching that day's row count
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-programme-timeline-golden-before.png` (loading)
- Screenshot after: `docs/screenshots/cp-admin-programme-timeline-golden-after.png` (day sections rendered)
- Console errors: 0 expected
- Network: the single `/account/api/admin/sessions/list` call returns 200; no other `/account/api/admin/...` write calls fire (read-only page)
- Audit row: none expected — this page performs no mutations, so no `OperationLog` / `RowAudit` row is written on view.

### E2E-PTL-002 — Stat cards reflect the data

```gherkin
Scenario: The two stat cards count days and total sessions
  Given the list call returned 5 sessions spread across 3 distinct local calendar days
  When the page renders
  Then the "Days" SimfStatCard value reads "3"
  And the "Sessions" SimfStatCard value reads "5"
  And the count equals the number of <h2> day sections rendered (3)
  And the sum of the per-day "{N} session(s) on this day" lines equals 5
```

### E2E-PTL-003 — Day filter

```gherkin
Scenario: The day filter narrows to one day and restores all days
  Given the timeline shows 3 day sections ("All days" selected)
  When the administrator selects the second day's heading from the "Day" <select>
  Then only that one day's <h2> heading and table remain visible
  And the other two day sections are hidden
  And no new /account/api/admin/sessions/list request fires (filtering is client-side)
  When the administrator re-selects the first option "All days"
  Then all 3 day sections are visible again
  And the stat cards still read "Days"=3 and "Sessions"=total (filtering does not change the stats)
```

### E2E-PTL-004 — Day grouping and ordering

```gherkin
Scenario: Days sort ascending and rows sort ascending by start within a day
  Given sessions exist with these Start local times:
    | code   | local start day | local start time |
    | OPN-01 | 2025-12-08      | 09:00            |
    | KEY-01 | 2025-12-08      | 10:30            |
    | PAR-A  | 2025-12-08      | 10:30            |
    | CLO-01 | 2025-12-09      | 16:00            |
  When the timeline renders
  Then the first <h2> is the 8 December section and the second is the 9 December section (days ascending)
  And within the 8 December table the rows appear in start-time order: OPN-01 (09:00) then the two 10:30 rows
  And the two 10:30 sessions in different halls (PAR-A, KEY-01) both appear as separate rows under the same day
  And the 9 December section shows exactly one row (CLO-01)
```

### E2E-PTL-005 — Time-window and per-day count rendering

```gherkin
Scenario: Time window formats as HH:mm – HH:mm and the count line pluralises
  Given a session OPN-01 with Start local 09:00 and End local 10:15
  When the timeline renders
  Then the OPN-01 row Time cell reads "09:00 – 10:15"
  And the times reflect the operator's local wall clock (Start.LocalDateTime projection)
  And a day with 1 session shows "1 session(s) on this day"
  And a day with 4 sessions shows "4 session(s) on this day"
```

### E2E-PTL-006 — Empty state

```gherkin
Scenario: No sessions renders the SimfEmptyState
  Given the database has no active Session rows (or all sessions are deactivated)
  When the administrator opens /admin/programme/timeline
  Then the POST /account/api/admin/sessions/list call returns 200 with an empty Items array
  And the page renders the SimfEmptyState component
  And the empty state title reads "No sessions have been scheduled yet." / "لم تتم جدولة أي جلسات بعد."
  And neither stat card, nor the "Day" filter, nor any day table is shown
  And no error toast appears
```

### E2E-PTL-007 — Auth gate

```gherkin
Scenario: Admin lacking ProgrammeTimeline.View is denied
  Given a signed-in admin whose role does NOT include the ProgrammeTimeline.View permission
    (and is not the Administrator wildcard "*")
  When they navigate to /admin/programme/timeline
  Then the [RequirePermission(PermissionCatalog.ProgrammeTimeline.View)] attribute denies access
  And they land on /not-permitted with HTTP 200
  And no /account/api/admin/sessions/list request fires
  And the "Programme run of show" nav item is hidden for this role (RequiredPermission gating)
```

### E2E-PTL-008 — Read-only guarantee

```gherkin
Scenario: The page exposes no mutation affordances
  Given the timeline has loaded with several sessions
  When the administrator inspects the page
  Then there is NO "Add" / "Create" button
  And there is NO per-row Edit / Details / Deactivate / Delete action
  And there is NO bulk action or status toggle
  And the only interactive control is the read-only "Day" filter <select>
  And no write request (POST/PUT/DELETE to /account/api/admin/sessions/...) can be triggered from this page
```

### E2E-PTL-009 — Permission asymmetry (page allowed, list forbidden)

```gherkin
Scenario: Has ProgrammeTimeline.View but not Sessions.View → list 403 → error toast
  Given a signed-in admin granted ProgrammeTimeline.View but NOT Sessions.View
  When they navigate to /admin/programme/timeline
  Then the page itself loads (the RequirePermission gate passes)
  And the POST /account/api/admin/sessions/list call returns HTTP 403 (API gated by Sessions.View)
  And the page shows a SimfAlert error toast
  And the toast text is the API error MessageForCurrentCulture() or the fallback
    "Could not load the programme. Please try again." / "تعذّر تحميل البرنامج. يرجى المحاولة مرة أخرى."
  And no day tables or stat cards render
```

### E2E-PTL-010 — Server 500 on list

```gherkin
Scenario: API 500 on /sessions/list shows the bilingual fallback toast
  Given the API is made to return HTTP 500 on /admin/sessions/list (e.g. DB down)
  When the administrator opens /admin/programme/timeline
  Then the "Loading the programme…" line shows first
  And then a red SimfAlert error toast appears reading
    "Could not load the programme. Please try again." / "تعذّر تحميل البرنامج. يرجى المحاولة مرة أخرى."
  And no day sections, stat cards, or filter render
  And the page does not throw an unhandled JS exception (console errors: 0 from the page itself)
```

### E2E-PTL-011 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and shows Arabic titles, halls and day headings
  Given the administrator is on /admin/programme/timeline in English with sessions loaded
  When they switch the UI culture to Arabic (the "العربية" header link)
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "جدول سير الفعاليات"
  And the subtitle reads "كامل جدول الأعمال على شاشة واحدة، مجمّعًا حسب اليوم."
  And the stat cards are titled "الأيام" and "الجلسات"
  And the "Day" filter label reads "اليوم" with first option "كل الأيام"
  And the table headers read "الوقت", "الرمز", "الجلسة", "القاعة"
  And each <h2> day heading renders in Arabic (CurrentUICulture "dddd, d MMMM yyyy")
  And each row shows the session's Arabic title (TitleArabic) and Arabic hall name (HallNameArabic)
  And the per-day count line reads "{N} جلسة في هذا اليوم"
  And the nav rail and table columns mirror right-to-left
```

---

## Implementation notes

- **Read-only page — no mutation surface.** Unlike the CRUD catalogues
  (e.g. `cp-admin-interests.md`), there is no Add/Edit/Delete here. The
  "every distinct function" rows therefore cover the load round-trip, the two
  stat cards, the client-side day filter, the grouping/ordering rules, the
  time-window + count formatting, and the bilingual title/hall projection.
- **Permission asymmetry is the most important non-happy case.** The CP page
  gate is `ProgrammeTimeline.View`; the BFF list route forwards to the API's
  `ListSessionsEndpoint` which is gated by `Sessions.View`
  (`src/Backend/SIMF.Api/Endpoints/Admin/SessionEndpoints.cs`). E2E-PTL-009
  exercises the gap a per-page permission model creates.
- **API integration tests (lower layer).** The same `/admin/sessions/list`
  surface is covered without a browser in
  `tests/SIMF.Api.Tests/AdminSessionsTests.cs` — notably
  `Non_admin_caller_is_forbidden_on_create` (auth gating on the sessions
  surface) plus the create/round-trip/deactivate cases that seed the rows this
  timeline reads. There is no dedicated list-endpoint test today; the list is
  exercised indirectly via the round-trip fixtures.
- **Manual smoke as canonical run today.** Until Playwright is adopted, the
  canonical run is a Chrome DevTools MCP session: sign in per the Auth setup,
  walk each scenario, capture screenshots into
  `docs/screenshots/cp-admin-programme-timeline-{scenario}.png`.
- **Convert to Playwright** later by copying each Gherkin block into a
  `.feature` file under `tests/SIMF.E2E.Tests/` with step definitions — the
  steps are already runner-agnostic.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
