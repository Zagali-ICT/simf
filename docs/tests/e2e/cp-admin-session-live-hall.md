# E2E test catalogue — Live hall (per-session) (`/admin/sessions/live-hall`)

| | |
|--|--|
| **Page** | — (read-only live view; this catalogue is the authored proof) |
| **Route** | `/admin/sessions/live-hall` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-07-18 (page created) |

> **What this page does.** The **live per-session hall view** (2026-07-18, CP page
> 2e). The admin picks one **active** session from a `SimfSelect` dropdown and then
> sees, live, two read-only panels for that session's hall:
>
> 1. **Seat map** — the hall layout rendered as a 4-state grid. Each seat is
>    coloured by its live state: **available** (متاح, no reservation) ·
>    **unavailable** (غير متاح, an admin/VIP block — `Kind = AdminReservedRow`) ·
>    **reserved** (محجوز, a holder who has **not** checked in — `CheckedIn = false`)
>    · **confirmed** (تم التأكيد, a holder who **has** scanned in at the hall gate —
>    an open `HallAttendance` row, `CheckedIn = true`). A hall with no seat layout
>    shows the "no seat map" empty state instead.
> 2. **In the hall now** — a table of everyone currently inside the hall (the open
>    `HallAttendance` rows), each with their **name, organisation, profile type,
>    job title, seat, entry time (Saudi time) and method** (QR scan / geofence), ordered
>    by arrival. Open-seating attendees show "General admission / دخول عام".
>
> A **Refresh** button re-pulls both panels, and — **QA B17** — while a session is
> selected both reads also re-run automatically every **15 seconds**
> (`SessionLiveHall.RefreshInterval`, a `PeriodicTimer` loop mirroring the CP's
> other live monitor, `ServicesMonitor`). Before B17 the page only ever pulled on
> session select and on a manual Refresh click, so a door scan stayed invisible
> until an admin happened to click — misleading for a "live" monitor during an
> event. The timer starts on selection, is cancelled + disposed when the selection
> changes or clears and when the component is disposed (`IDisposable`), so a Blazor
> Server circuit never leaks one. The background tick does **not** disable the
> session picker or spin the Refresh button, and a response that arrives after the
> admin switched sessions is dropped. There is **no** create / edit / delete
> and **no** grid actions — this is a monitor. Seat cells are not clickable (unlike
> the seat-plan editor at `/admin/sessions/seat-plans`).
>
> **Data sources (read-only).** `GET /api/v1/admin/sessions/{id}/seat-map`
> (`GetSessionSeatMapAsync` with a null actor — no "my seat" cell) and
> `GET /api/v1/admin/sessions/{id}/present` (`GetPresentAttendeesAsync`). The
> present list is resolved **App-DB only** — name/org/type/job-title come from
> `UserProfile`, never a cross-DB Identity join (D-157). An admin-typed present
> user (no profile) resolves to blank profile fields, never an error.
>
> **Permission gate.** Page `@attribute [RequirePermission(PermissionCatalog.Attendance.View)]`
> (`Attendance.View`). Both API endpoints are gated
> `Policies(PolicyFor(Attendance.View), RequireApprovedAccount)`. Nav item
> `Module.SessionLiveHall` → `RequiredPermission = Attendance.View`.
> `Attendance.View` seeds as `AdminOnly`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-SLH-001 | Golden path — pick a session → seat map + present list render for its hall | happy | P0 | _to author_ |
| E2E-SLH-002 | Seat map shows all four states with the right colour + tooltip (available / unavailable / reserved / confirmed) | happy | P0 | _to author_ |
| E2E-SLH-003 | "In the hall now" table lists every open-attendance row with name / org / type / job / seat / entered / method, ordered by entry | happy | P0 | _to author_ |
| E2E-SLH-004 | Refresh re-pulls both `/seat-map` and `/present` (a new gate scan appears as confirmed) | happy | P1 | _to author_ |
| E2E-SLH-005 | Hall with no seat layout → seat-map `SimfEmptyState` ("no seat map"); present table still renders | edge | P1 | _to author_ |
| E2E-SLH-006 | Nobody inside → present `SimfEmptyState` ("No one is inside the hall yet.") | happy | P1 | _to author_ |
| E2E-SLH-007 | No active sessions → page-level `SimfEmptyState` ("No sessions available."), no picker | happy | P1 | _to author_ |
| E2E-SLH-008 | Auth gate — admin lacking `Attendance.View` → `/not-permitted`; nav item hidden; API 403 | auth | P0 | _to author_ |
| E2E-SLH-009 | Server 500 on `/seat-map` → bilingual fallback error toast | resilience | P2 | _to author_ |
| E2E-SLH-010 | RTL render — Arabic mirrors page; 4-state labels متاح / غير متاح / محجوز / تم التأكيد | i18n | P1 | _to author_ |
| E2E-SLH-011 | Open-seating attendee (no specific seat) → seat cell reads "General admission / دخول عام" | edge | P1 | _to author_ |
| E2E-SLH-012 | Switching session A → B clears the prior hall (no data bleed) | edge | P1 | _to author_ |
| E2E-SLH-013 | Cross-DB safety — an admin-typed present user with no `UserProfile` → blank profile cells, no error, no Identity join | data | P1 | authored ✓ (API `Present_attendees_are_resolved_from_app_profiles_only`) |
| E2E-SLH-014 | QA B17 — a door scan appears within one 15 s auto-refresh tick, with no manual Refresh click | happy | P0 | _to author_ |
| E2E-SLH-015 | QA B17 — the poll starts on selection, stops on clear/switch and is disposed with the page (no leaked timer) | resilience | P0 | authored ✓ (`SessionLiveHallAutoRefreshTests`) |
| E2E-SLH-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-SLH-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-SLH-001 — Golden path

```gherkin
Feature: Live per-session hall view
  As an Administrator monitoring an event
  I want to pick a session and see its hall live
  So that I can see who is inside and the seat occupancy at a glance

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp (TOTP from Get-Totp)
  And they have landed on /admin/sessions/live-hall
  And an active session "Opening Plenary · SES-OPEN01" runs in hall "Majlis A" (layout rows A–C × 10)
  And attendee "Faisal Al-Harbi" holds seat B3 and has scanned in at the hall gate
  And attendee "Sara Al-Otaibi" holds seat B4 but has NOT scanned in

Scenario: Pick a session and see its hall live
  Given the page has loaded and the session dropdown is populated with active sessions only
  When the administrator selects "SES-OPEN01 — Opening Plenary"
  Then the BFF forwards GET /account/api/admin/sessions/{id}/seat-map and GET /account/api/admin/sessions/{id}/present
  And both return HTTP 200 with ApiResult.Success = true
  And the "Seat map" panel renders rows A–C with 10 seats each
  And seat B3 is coloured "confirmed" (تم التأكيد) and its tooltip reads "Seat B3 — Confirmed (checked in)"
  And seat B4 is coloured "reserved" (محجوز) and its tooltip reads "Seat B4 — Reserved"
  And the "In the hall now" panel lists "Faisal Al-Harbi" (present) but NOT "Sara Al-Otaibi" (not scanned in)
  And the present summary reads "1 present"
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-session-live-hall-golden-before.png` (session picked, panels loading)
- Screenshot after: `docs/screenshots/cp-admin-session-live-hall-golden-after.png` (seat map + present table)
- Console errors: 0 expected
- Network: `/account/api/admin/sessions/list` 200 on load; `/seat-map` + `/present` 200 on select
- DOM: `scrollWidth == clientWidth` (no horizontal overflow); the seat grid scrolls inside `.seatmap` (`overflow-x: auto`), never the page body

### E2E-SLH-002 — Four seat states

```gherkin
Scenario: The seat map paints all four states distinctly
  Given the selected session's hall has: seat A1 free, seat A2 admin-blocked (VIP),
        seat A3 reserved by a visitor who has not checked in, seat A4 reserved by a
        visitor who has checked in at the gate
  When the seat map renders
  Then A1 uses the "available" swatch (--color-seat-free) with tooltip "Seat A1 — Available"
  And A2 uses the "unavailable" swatch (--color-seat-admin) with tooltip "Seat A2 — Unavailable"
  And A3 uses the "reserved" swatch (--color-seat-user) with tooltip "Seat A3 — Reserved"
  And A4 uses the "confirmed" swatch (--color-seat-confirmed / success) with tooltip "Seat A4 — Confirmed (checked in)"
  And the legend shows all four states with matching swatches
```

### E2E-SLH-003 — Present table columns and order

```gherkin
Scenario: Everyone inside is listed with full profile + seat, ordered by entry
  Given three attendees are currently inside the hall, entering at 09:01, 09:03 and 09:05
  When the "In the hall now" table renders
  Then it has columns Name, Organisation, Type, Job title, Seat, Entered (Saudi time), Method
  And rows appear in entry order (09:01 first)
  And each row shows the attendee's UserProfile name (Arabic when the Name field is blank),
      organisation name, profile-type name, job title, their held seat (e.g. "B3"), the
      UTC entry time (yyyy-MM-dd HH:mm) and the method ("QR scan" / "Geofence")
  And the summary line reads "3 present"
```

### E2E-SLH-004 — Refresh re-pulls both panels

```gherkin
Scenario: Refresh reflects a new gate scan
  Given a session is selected and one attendee is shown "reserved" on seat C7
  When that attendee scans in at the hall gate (a new open HallAttendance row)
  And the administrator clicks "Refresh"
  Then GET /seat-map and GET /present are re-issued
  And seat C7 flips from "reserved" to "confirmed"
  And the attendee now appears in the "In the hall now" table
```

### E2E-SLH-005 — Hall with no seat layout

```gherkin
Scenario: A hall with no seat layout still shows the present list
  Given the selected session's hall has no seat layout (RowLabels empty)
  When the panels render
  Then the "Seat map" panel shows the SimfEmptyState "This hall has no seat layout, so there is no seat map to show."
  And the "In the hall now" table still renders normally (attendance does not need a layout)
```

### E2E-SLH-006 — Nobody inside

```gherkin
Scenario: An empty hall shows the present empty state
  Given a session is selected and no one has an open attendance row for it
  When the "In the hall now" panel renders
  Then it shows the SimfEmptyState "No one is inside the hall yet."
  And no present table renders
  And the seat map still renders (reservations can exist without anyone present)
```

### E2E-SLH-007 — No active sessions

```gherkin
Scenario: No active sessions renders the page empty state
  Given the database has no active sessions
  When the administrator opens /admin/sessions/live-hall
  Then the page renders the SimfEmptyState "No sessions available."
  And no session dropdown, seat map, present table, or Refresh button render
  And no error toast appears
```

### E2E-SLH-008 — Auth gate (Attendance.View)

```gherkin
Scenario: Admin lacking Attendance.View is denied the page
  Given a signed-in admin whose role does NOT include Attendance.View (and is not Administrator "*")
  When they navigate to /admin/sessions/live-hall
  Then they land on /not-permitted with HTTP 200
  And the "Module.SessionLiveHall" nav item is hidden for them
  And if GET /api/v1/admin/sessions/{id}/present or /seat-map is forged directly, the API returns HTTP 403
```

### E2E-SLH-009 — Server 500 on the seat map

```gherkin
Scenario: API 500 on /seat-map shows the fallback toast
  Given a session is selected
  And the API is configured to return 500 on /admin/sessions/{id}/seat-map
  When the panels load
  Then a red toast appears reading the server message, or the fallback
      "Could not load the live hall. Please try again." / "تعذّر تحميل القاعة المباشرة. حاول مرة أخرى."
  And no seat map renders
```

### E2E-SLH-010 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the live-hall view
  Given the administrator is on /admin/sessions/live-hall in English
  When they switch the UI to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "القاعة المباشرة"
  And the seat-map heading reads "خريطة المقاعد" and the present heading "داخل القاعة الآن"
  And the four legend labels read متاح / غير متاح / محجوز / تم التأكيد
  And the present table headers read الاسم / الجهة / النوع / المسمى الوظيفي / المقعد / وقت الدخول (بتوقيت السعودية) / الطريقة
```

### E2E-SLH-011 — Open-seating attendee

```gherkin
Scenario: An open-seating (general admission) attendee has no specific seat
  Given the selected session uses OpenSeating and an attendee is present via a one-tap join
  When the "In the hall now" table renders that attendee's row
  Then the Seat cell reads "General admission" / "دخول عام"
  And that attendee is NOT painted on the seat map (they hold no specific seat)
```

### E2E-SLH-012 — Session switch clears the prior hall

```gherkin
Scenario: Switching sessions never bleeds data
  Given session A is selected and its seat map + present list are shown
  When the administrator selects session B from the dropdown
  Then A's seat map and present list are cleared before B's data arrives
  And any stale error toast from A is cleared
  And only B's hall is shown once its /seat-map and /present return
```

### E2E-SLH-013 — Cross-DB safety (App-DB-only resolution)

```gherkin
Scenario: An admin-typed present user with no profile resolves to blank fields
  Given a user with no UserProfile row (an admin-typed account) has an open attendance row
  When the "In the hall now" table renders their row
  Then the Name/Organisation/Type/Job-title cells are blank (not an error)
  And no query is made against the Identity database (D-157 — App-DB only)
  # Covered at the endpoint layer by SessionAttendanceTests.
```

### E2E-SLH-014 — a door scan appears without a manual Refresh (QA B17)

```gherkin
Scenario: The live monitor refreshes itself while a session is selected
  # QA B17: the page used to issue its two GETs only on session select and on a
  # manual Refresh click — no timer, no polling, no push — so a door scan was
  # invisible until an admin happened to click Refresh.
  Given the administrator has selected session "SES-OPEN01" on /admin/sessions/live-hall
  And attendee "Sara Al-Otaibi" is shown "reserved" on seat B4
  When she scans in at the hall door and the administrator touches nothing
  Then within 15 seconds a GET /account/api/admin/sessions/{id}/seat-map and a
      GET /account/api/admin/sessions/{id}/present fire on their own
  And seat B4 flips from "reserved" to "confirmed"
  And she appears in the "In the hall now" table
  And the session picker was never disabled and the Refresh button never showed a
      spinner (the background tick is silent)
```

### E2E-SLH-015 — the poll's lifetime (QA B17)

```gherkin
Scenario: The poll exists only while a session is selected and never outlives the page
  Given the administrator opens /admin/sessions/live-hall with nothing selected
  Then no poll runs

  When they select a session
  Then the poll starts

  When they switch to another session
  Then the first session's poll is cancelled and disposed before the new one starts
  And a response from the previous session that arrives late is discarded (no bleed)

  When they clear the selection (the placeholder option)
  Then the poll stops

  When they navigate away from the page
  Then the component is disposed and the timer + cancellation source are disposed with it
  # A PeriodicTimer left running on a Blazor Server circuit is a real leak.
```

**Evidence:** `tests/SIMF.ControlPanel.Tests/SessionLiveHallAutoRefreshTests.cs` →
`B17_no_session_selected_means_no_poll_timer`,
`B17_selecting_a_session_starts_the_poll_and_disposing_stops_it`,
`B17_clearing_the_selection_stops_the_poll`.

---

## Implementation notes

- **API integration tests at a lower layer.** `tests/SIMF.Api.Tests/SessionAttendanceTests.cs`
  covers the two endpoints this page consumes (no browser):
  - present list returns the open-attendance attendees with their App-DB profile + seat,
    ordered by entry → E2E-SLH-001 / -003.
  - admin seat-map returns the 4-state grid with a null "my seat" cell → E2E-SLH-002.
  - profile fields resolve from `UserProfile` only, never the Identity DB → E2E-SLH-013.
  - both endpoints require `Attendance.View` (`PermissionEnforcementTests`) → E2E-SLH-008.
  Keep both layers during the transition; this catalogue is the browser-level proof
  that the CP page drives those same outcomes.
- **The four seat states** come from `SessionSeatCell`: no cell = available; `Kind ==
  AdminReservedRow` = unavailable; a holder with `CheckedIn == false` = reserved; a
  holder with `CheckedIn == true` (open `HallAttendance` row) = confirmed. `CheckedIn`
  is the append-only wire field added Wave 2 slice 1 (`c1a50687`); the open row is what
  the hall-door check-in (`/admin/hall-arrivals` → `POST .../arrivals`) opens and the
  staff check-out (`POST .../departures`) closes.
- **No grid / no CRUD.** Like `/admin/attendance` and `/admin/hall-arrivals`, this is a
  read-only monitor over loaded data. There is no Add/Edit/Details/Deactivate surface;
  the present list is a live snapshot table (not a `SimfDataGrid` — it is unpaged and
  refreshes atomically with the seat map).
- **Permission gate** (HARD RULE, CLAUDE.md §Access control): page
  `RequirePermission(Attendance.View)`; nav `Module.SessionLiveHall` →
  `RequiredPermission = Attendance.View`; API `Policies(PolicyFor(Attendance.View),
  RequireApprovedAccount)` on both `GET /admin/sessions/{id}/seat-map` and
  `GET /admin/sessions/{id}/present`. No new permission code is introduced — the view
  reuses the existing `Attendance.View`.
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a `.feature`
  file under `tests/SIMF.E2E.Tests/` + step-definition class. The Gherkin shape is
  already runner-agnostic.

---

_Last reviewed:_ 2026-07-26 by Claude (QA B17 — 15 s auto-refresh + disposal; E2E-SLH-014/015). Prior: 2026-07-18 by Claude (page created — live per-session hall view, CP page 2e).
