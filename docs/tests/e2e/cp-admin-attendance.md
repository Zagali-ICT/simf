# E2E test catalogue — Session-attendance dashboard (`/admin/attendance`)

| | |
|--|--|
| **Page** | [`cp/admin-attendance.md`](../../pages/cp/admin-attendance.md) |
| **Route** | `/admin/attendance` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-05 |

> **What this page is.** The session-attendance dashboard
> (`AttendanceDashboard.razor`, FR-506) is a **read-only** view of the
> live event's hall-arrival data (D-241), derived entirely from the existing
> `HallAttendance` records — **no schema, no writes**. On load it fires **two**
> calls: `GET /account/api/admin/attendance/summary` →
> `ApiResult<SessionAttendanceSummary>` (the live top-line) and
> `POST /account/api/admin/attendance/sessions/list` (a `GridQuery`) →
> `ApiResult<GridPage<SessionAttendanceRow>>` (the per-session grid). It renders
> **3 `SimfStatCard` tiles** (Live attendees now / Sessions with attendance /
> Total arrivals) above a **`SimfDataGrid`** of active sessions. The grid is
> **read-only** — no Add / Edit / Delete / select / bulk actions — only
> per-column filter (Code, Session), column sort (Code, Session, Start) and the
> pager. The page is gated by
> `@attribute [RequirePermission(PermissionCatalog.Attendance.View)]` (code
> `"Attendance.View"`, baseline `AdminOnly`); `Administrator = "*"` satisfies it.
> The nav item `Module.Attendance` (under "Overview") carries the same
> `RequiredPermission`.

> **Counts, defined.** A session's **Total attendees** is the **distinct** count
> of people who arrived at its hall — any `HallAttendance` enter record (GPS
> geofence D-241 **or** operator QR door-scan D-244), so a person who re-entered
> (a closed row + a new open row) counts **once**. **Live now** is the count
> currently inside (open rows, `LeaveUtc` null). The top-line **Live attendees
> now** is the distinct count of people inside **any** hall; **Total arrivals**
> sums each active session's distinct-attendee count. The attendee identity is
> never resolved (D-157) — `UserId` is counted as an opaque Guid.

## The 3 stat tiles + the 6 grid columns

| Tile | Contract field | en | ar |
|---|---|---|---|
| 1 | `LiveAttendeesNow` | Live attendees now | الحاضرون الآن |
| 2 | `SessionsWithAttendance` | Sessions with attendance | جلسات بها حضور |
| 3 | `TotalArrivals` | Total arrivals | إجمالي الوصول |

| Col (key) | en | sort | filter |
|---|---|---|---|
| `code` | Code | ✓ | ✓ |
| `title` | Session | ✓ | ✓ |
| `hall` | Hall | — | — |
| `startUtc` | Start (UTC) | ✓ | — |
| `total` | Total attendees | — | — |
| `live` | Live now (`SimfPill` when > 0) | — | — |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-ATT-001 | Golden path — admin loads `/admin/attendance`; 3 tiles + per-session grid render | happy | P0 | authored ✓ (`List_returns_distinct_attendee_and_live_now_per_session`) |
| E2E-ATT-002 | Distinct attendees dedupe re-entry; live-now counts only open rows | function | P0 | authored ✓ (`List_returns_distinct_attendee_and_live_now_per_session`) |
| E2E-ATT-003 | A session with no arrivals shows 0 / 0 | happy | P1 | authored ✓ (`List_empty_session_has_zero_counts`) |
| E2E-ATT-004 | Top-line summary reflects live + total arrivals | happy | P0 | authored ✓ (`Summary_lower_bounds_reflect_seeded_arrivals`) |
| E2E-ATT-005 | Empty grid (no active sessions) renders `SimfEmptyState` (`Admin.Attendance.None`) | happy | P1 | _to author_ |
| E2E-ATT-006 | Auth gate (API) — caller lacking `Attendance.View` → 403 on summary + list | auth | P0 | authored ✓ (`Summary_is_forbidden_for_a_non_admin`, `List_is_forbidden_for_a_non_admin`) |
| E2E-ATT-007 | Auth gate (CP) — admin lacking `Attendance.View` → `/not-permitted`; nav item hidden | auth | P0 | _to author_ |
| E2E-ATT-008 | Per-column filter (code / title) narrows the grid | happy | P1 | _to author_ |
| E2E-ATT-009 | Column sort toggles (code / title / startUtc ascending↔descending) | happy | P2 | _to author_ |
| E2E-ATT-010 | Live-now `SimfPill` shows for sessions with people inside, plain "0" otherwise | function | P1 | _to author_ |
| E2E-ATT-011 | Server 500 on summary or list → red `SimfAlert` (`Admin.Attendance.LoadFailed`) | resilience | P2 | _to author_ |
| E2E-ATT-012 | Read-only surface — no Add/Edit/Delete/select; no POST/PUT/DELETE beyond the list call | function | P2 | _to author_ |
| E2E-ATT-013 | RTL / Arabic render — banner, tiles, grid headers and nav mirror | i18n | P1 | _to author_ |
| E2E-ATT-014 | Counts reflect live state — record an arrival, reload, the session's Live-now + the top-line move | happy | P1 | _to author_ |

## Scenarios

### E2E-ATT-001 — Golden path (load + render)

```gherkin
Feature: Session-attendance dashboard
  As an Administrator
  I want a live view of who has arrived at each session's hall
  So that I can read attendance and current room occupancy at a glance

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And the account superadmin@zagali-ict.com is Approved with a paired TOTP authenticator
  And an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And they have landed on /admin/attendance

Scenario: The dashboard renders the live top-line and the per-session grid
  Given an active session "Opening Plenary" in hall "Main Hall" has 3 distinct
        arrivals, of which 2 are currently inside
  When the page initialises
  Then it fires GET /account/api/admin/attendance/summary
  And it fires POST /account/api/admin/attendance/sessions/list with the default GridQuery (Sort="startUtc")
  And the BFF forwards them to GET /api/v1/admin/attendance/summary and
      POST /api/v1/admin/attendance/sessions/list on the API (both HTTP 200)
  And the browser tab title is "Session attendance · SIMF"
  And the SimfBanner title reads "Session attendance"
  And 3 SimfStatCard tiles render: "Live attendees now", "Sessions with attendance", "Total arrivals"
  And the grid shows a row for "Opening Plenary" with Hall="Main Hall", Total attendees=3, Live now=2
  And no error SimfAlert is shown
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-attendance-001-after.png`
- Console errors: 0 expected
- Network: one `GET …/attendance/summary` + one `POST …/attendance/sessions/list`, both 200
- Audit row: none — reading the dashboard is a pure aggregate read (no `OperationLog` row)

**Lower-layer evidence:** `SessionAttendanceTests.List_returns_distinct_attendee_and_live_now_per_session` (green).

### E2E-ATT-002 — Distinct attendees dedupe re-entry; live counts open rows only

```gherkin
Scenario: A re-entered attendee counts once; live-now counts only open rows
  Given attendee A entered, left (closed row), then entered again (open row)
  And attendee B is currently inside (open row)
  And attendee C entered and left (closed row)
  When the administrator views the session's row on /admin/attendance
  Then "Total attendees" reads 3 (A, B, C — A's two rows dedupe to one)
  And "Live now" reads 2 (A and B — the open rows; C already left)
```

**Lower-layer evidence:** `SessionAttendanceTests.List_returns_distinct_attendee_and_live_now_per_session` (green).

### E2E-ATT-003 — Session with no arrivals

```gherkin
Scenario: An active session that nobody has arrived at shows zero counts
  Given an active session exists with no HallAttendance rows
  When the administrator views /admin/attendance
  Then that session's row shows Total attendees=0 and Live now="0" (plain text, no pill)
```

**Lower-layer evidence:** `SessionAttendanceTests.List_empty_session_has_zero_counts` (green).

### E2E-ATT-004 — Top-line summary

```gherkin
Scenario: The top-line tiles aggregate across the event
  Given there are people currently inside one or more halls
  When the administrator opens /admin/attendance
  Then "Live attendees now" equals the distinct count of people with an open attendance row
  And "Sessions with attendance" equals the number of active sessions with at least one arrival
  And "Total arrivals" equals the sum over active sessions of each session's distinct-attendee count
```

**Lower-layer evidence:** `SessionAttendanceTests.Summary_lower_bounds_reflect_seeded_arrivals` (green).

### E2E-ATT-005 — Empty grid (no active sessions)

```gherkin
Scenario: With no active sessions the grid shows the empty state
  Given there are no active sessions
  When the administrator opens /admin/attendance
  Then the per-session grid body renders the SimfEmptyState titled
      "No attendance has been recorded yet." (Arabic: "لم يُسجَّل أي حضور بعد.")
  And the 3 stat tiles still render (with 0 values)
```

### E2E-ATT-006 — Auth gate (API 403)

```gherkin
Scenario: A signed-in non-admin without Attendance.View is forbidden by the API
  Given a signed-in Approved account that is not an Administrator and lacks "Attendance.View"
  When it calls GET /api/v1/admin/attendance/summary
  Then the API returns HTTP 403
  And the same account calling POST /api/v1/admin/attendance/sessions/list also returns HTTP 403
```

**Lower-layer evidence:** `SessionAttendanceTests.Summary_is_forbidden_for_a_non_admin` +
`SessionAttendanceTests.List_is_forbidden_for_a_non_admin` (green).

### E2E-ATT-007 — Auth gate (CP redirect + nav hide)

```gherkin
Scenario: A signed-in admin without Attendance.View is denied in the CP
  Given a signed-in Approved admin whose role does NOT include "Attendance.View" (and no "*")
  When they navigate to /admin/attendance
  Then the RequirePermission(PermissionCatalog.Attendance.View) attribute redirects them to /not-permitted (HTTP 200)
  And no attendance summary / list request fires
  And the "Attendance" item is hidden from their side nav rail (RequiredPermission = Attendance.View)
```

### E2E-ATT-008 — Per-column filter

```gherkin
Scenario: Typing into the Code / Session column filters narrows the grid
  Given the grid shows several active sessions
  When the administrator types a session code fragment into the "Code" column filter
  Then the grid issues POST /account/api/admin/attendance/sessions/list with
      GridQuery.Filters["code"] = the fragment and Skip reset to 0
  And only sessions whose Code contains the fragment (case-insensitive) render
  When they instead type into the "Session" column filter
  Then the list call carries Filters["title"] and the grid narrows by English title
```

### E2E-ATT-009 — Column sort

```gherkin
Scenario: Sorting by Start, Code, then Session toggles ascending/descending
  Given the grid is sorted by Start (UTC) ascending by default (Sort="startUtc")
  When the administrator clicks the "Code" column header
  Then the list call carries Sort="code", SortDescending=false and the rows reorder by code A→Z
  When they click "Code" again
  Then the list call carries Sort="code", SortDescending=true (Z→A)
  And the Hall, Total attendees and Live now columns stay unsortable
```

### E2E-ATT-010 — Live-now pill

```gherkin
Scenario: Live-now renders a pill only when people are inside
  Given session X has 4 people currently inside and session Y has 0
  When the administrator views /admin/attendance
  Then session X's "Live now" cell shows a SimfPill (Variant="on") reading "4"
  And session Y's "Live now" cell shows plain text "0" (no pill)
```

### E2E-ATT-011 — Server 500 fallback

```gherkin
Scenario: A 500 on either call shows the bilingual fallback alert
  Given the API is configured to return HTTP 500 on /api/v1/admin/attendance/summary (or …/sessions/list)
  When the administrator opens /admin/attendance
  Then a red SimfAlert renders the server message if present,
      else "Could not load attendance. Please try again."
      (Arabic: "تعذّر تحميل الحضور. حاول مرة أخرى.")
  And the page does not crash to the unhandled-exception page
```

### E2E-ATT-012 — Read-only surface

```gherkin
Scenario: The page exposes no write actions
  Given an administrator has landed on /admin/attendance with the grid rendered
  Then there are no Add / Edit / Delete / Save buttons, no row checkboxes, no bulk toolbar
  And the page makes no PUT / DELETE request, and no POST other than the sessions/list query
```

### E2E-ATT-013 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the dashboard
  Given an administrator is on /admin/attendance in English
  When they switch the UI language to "العربية"
  Then the page reloads with <html dir="rtl" lang="ar">
  And the tab title is "حضور الجلسات · SIMF" and the banner reads "حضور الجلسات"
  And the tiles read "الحاضرون الآن", "جلسات بها حضور", "إجمالي الوصول"
  And the grid headers read "الرمز", "الجلسة", "القاعة", "البداية (UTC)", "إجمالي الحضور", "الآن"
  And the grid + nav rail mirror right-to-left
```

### E2E-ATT-014 — Counts reflect live state (round-trip)

```gherkin
Scenario: Recording an arrival moves the session's live count and the top-line
  Given session X currently shows Live now = N and Total attendees = M on /admin/attendance
  When a new attendee arrives at hall X (POST /app/sessions/{X}/arrival inside the geofence,
      or an operator QR door-scan POST /admin/sessions/{X}/arrivals)
  And the administrator reloads /admin/attendance
  Then session X's "Live now" reads N + 1 and "Total attendees" reads M + 1 (a first-time arrival)
  And the top-line "Live attendees now" and "Total arrivals" each increase by 1
```

---

## Implementation notes

- **Read-only, two calls.** `AttendanceDashboard.razor` calls
  `GET /account/api/admin/attendance/summary` (`simfAccount.getJson`) and
  `POST /account/api/admin/attendance/sessions/list` (`simfAccount.postJson`)
  and renders `SIMF.Contracts.Attendance.SessionAttendanceSummary` into 3
  `SimfStatCard` tiles plus `SessionAttendanceRow` into a `SimfDataGrid`. No
  CRUD surface.
- **BFF → API chain.** CP routes (`AccountEndpoints.cs`, group `/account/api`)
  forward to `SimfAdminClient.GetSessionAttendanceSummaryAsync` /
  `ListSessionAttendanceAsync` → API `GetSessionAttendanceSummaryEndpoint`
  (`GET /api/v1/admin/attendance/summary`) and `ListSessionAttendanceEndpoint`
  (`POST /api/v1/admin/attendance/sessions/list`), both gated
  `Policies(PolicyFor(Attendance.View), RequireApprovedAccount)`.
- **Permission.** `PermissionCatalog.Attendance.View` (`"Attendance.View"`,
  baseline `AdminOnly`) gates **both** endpoints and the CP page
  (`@attribute [RequirePermission(...)]`), and is the nav item's
  `RequiredPermission`. `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`
  and `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` fail the build if a
  gate is missing.
- **Lower-layer coverage.** `tests/SIMF.Api.Tests/SessionAttendanceTests.cs`
  covers the aggregate maths (distinct-attendee dedupe, live-now, empty
  session, summary lower bounds) and the API permission gate (403 for a
  non-admin on both endpoints).
- **Convert to Playwright** when the runner is adopted: each Gherkin scenario
  maps to a `.feature` file under `tests/SIMF.E2E.Tests/` plus a step-definition
  class. The shape is already runner-agnostic.

---

_Last reviewed:_ 2026-06-05 by SIMF Team (FR-506 session-attendance dashboard added).
