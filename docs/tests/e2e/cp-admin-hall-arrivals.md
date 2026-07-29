# E2E test catalogue — Hall arrivals (door scan) (`/admin/hall-arrivals`)

| | |
|--|--|
| **Page** | [`cp/admin-hall-arrivals.md`](../../pages/cp/admin-hall-arrivals.md) |
| **Route** | `/admin/hall-arrivals` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-07-26 (DEF-CHK-003 picker + FR-CHK-003/004) |

> **What this page does.** The hall-door arrival console (P5.1d — D-244,
> FDS-003 §5.4). An operator picks one **active** session from a dropdown, then
> scans or types an attendee's **badge QR** (`MaxLength="64"`) and clicks either
> **Record arrival** (check-in) or **Record departure** (check-out, 2026-07-18).
> The server resolves the QR to an attendee; **arrival** opens (or merges into)
> the one open `HallAttendance` row with `Method = QrScan`, **departure** closes
> that open row (idempotent — a no-op when the attendee is not checked in). Both
> return the resolved attendee so the console can confirm **who** was recorded.
> The open row is exactly what the seat map renders as the "confirmed / تم التأكيد"
> state, so a check-out clears it. There is no grid, no edit, no delete. Raw
> coordinates are never involved; this is the door-scan means (the geofence means,
> and the attendee's own self check-out, are the attendee's own device).
>
> **Which sessions the picker offers (DEF-CHK-003, 2026-07-26).** The picker used
> to apply the **arrival** window (`EnsureSessionLiveNow`, ± 15 min) to BOTH
> buttons, so a session that had ended dropped out of the list and its hall could
> never be checked OUT — exactly when an operator needs to. The departures
> endpoint deliberately has **no** window (an attendee already inside must always
> be able to leave), so the picker now offers every active session that has
> already opened for arrivals (`now >= Start - 15 min`), with the currently-live
> ones listed first and the most recently ended next. A session that has not
> started yet is still hidden (no attendance row can exist for it). Pressing
> **Record arrival** on an ended session is answered by the server's existing
> bilingual `SESSION_NOT_LIVE` (409) message in the error toast.
>
> **Permission gate.** Page `@attribute [RequirePermission(PermissionCatalog.HallArrivals.View)]`
> (`HallArrivals.View`); the QR field + both buttons are wrapped in
> `<AuthorizedAction Permission="PermissionCatalog.HallArrivals.Record">`
> (`HallArrivals.Record`). Both default to `AdminOnly`. The API endpoints
> `POST /api/v1/admin/sessions/{sessionId}/arrivals` **and**
> `POST /api/v1/admin/sessions/{sessionId}/departures` are both gated by
> `HallArrivals.Record` + `RequireApprovedAccount` — one code covers BOTH
> directions, which is why its catalogue text reads "Record a hall arrival **or
> departure** by badge scan" (FR-CHK-003; the operator population is identical for
> check-in and check-out, so the codes are deliberately not split).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-HAR-001 | Golden path — select session → scan badge QR → arrival recorded with the resolved attendee name | happy | P0 | _to author_ |
| E2E-HAR-002 | Session dropdown lists only **active** sessions (`{Title} · {Code}`), live ones first | happy | P1 | _to author_ |
| E2E-HAR-003 | "Record arrival" button is the only action; clears the QR field on success (ready for the next scan) | happy | P1 | _to author_ |
| E2E-HAR-004 | Client guard: Record with no session selected → "Select a session first." (no network call) | error | P1 | _to author_ |
| E2E-HAR-005 | Client guard: Record with a blank QR field → no-op, no toast, no network call | error | P2 | _to author_ |
| E2E-HAR-006 | Re-scanning the same attendee merges into the one open row (idempotent, still success) | happy | P1 | _to author_ |
| E2E-HAR-007 | Empty state — no active sessions → `SimfEmptyState` ("No active sessions…") and no QR field | happy | P1 | _to author_ |
| E2E-HAR-008 | Auth gate (View) — signed-in admin lacking `HallArrivals.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-HAR-009 | Auth gate (Record) — admin with View but not `HallArrivals.Record` → QR field + button hidden, API 403 | auth | P0 | _to author_ |
| E2E-HAR-010 | Unknown / unrecognised badge QR → API 400 `ATTENDEE_QR_UNKNOWN` → bilingual error toast | error | P1 | _to author_ |
| E2E-HAR-011 | Attendee not approved / locked out → API 403 `ATTENDEE_NOT_APPROVED` → bilingual error toast | error | P1 | _to author_ |
| E2E-HAR-012 | QR field length cap — input limited to 64 chars (`MaxLength="64"`) | error | P2 | _to author_ |
| E2E-HAR-013 | Server 500 on `/sessions/list` → bilingual fallback toast, no session dropdown | resilience | P2 | _to author_ |
| E2E-HAR-014 | RTL render — Arabic toggle mirrors page, labels, select + button | i18n | P1 | _to author_ |
| E2E-HAR-018 | Staff check-OUT — select session → scan badge → **Record departure** closes the open row; seat-map confirmed state clears | happy | P0 | authored ✓ (API `Operator_scan_records_a_departure`) |
| E2E-HAR-019 | Check-out with no prior arrival → 200 idempotent no-op (`Arrived=false`), no error | edge | P1 | authored ✓ (API `Departure_without_a_prior_arrival_is_an_idempotent_noop`) |
| E2E-HAR-020 | Unknown badge QR on departure → 400 `ATTENDEE_QR_UNKNOWN` → bilingual error toast | error | P1 | authored ✓ (API `Unknown_qr_departure_is_400`) |
| E2E-HAR-021 | Auth gate (Record) — a non-operator cannot record a departure → API 403 | auth | P0 | authored ✓ (API `A_non_operator_cannot_record_a_departure`) |
| E2E-HAR-022 | DEF-CHK-003 — a session that ENDED hours ago is still selectable, so the hall can be checked out | happy | P0 | authored ✓ (CP `HallArrivalsConsoleSessionPickerTests`) |
| E2E-HAR-023 | DEF-CHK-003 — a session that has not started yet (and an inactive one) stay out of the picker | edge | P1 | authored ✓ (CP `HallArrivalsConsoleSessionPickerTests`) |
| E2E-HAR-024 | FR-CHK-004 — concurrent arrivals against a full hall fill it exactly, never past capacity | validation | P1 | authored ✓ (API `Concurrent_arrivals_never_exceed_the_hall_capacity`) |
| E2E-HAR-025 | DEF-CHK-002 — an operator door scan alone unlocks that session's rating (no geofence needed) | happy | P1 | authored ✓ (API `Operator_scan_makes_the_per_session_rating_submittable`) |

## Scenarios

### E2E-HAR-001 — Golden path

```gherkin
Feature: Hall-door QR arrival recording
  As an operator at a hall door
  I want to scan an attendee's badge QR against the active session
  So that their hall arrival is recorded (Method = QrScan)

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp (TOTP from Get-Totp)
  And they have landed on /admin/hall-arrivals
  And at least one active session exists with an approved attendee whose badge QR is known

Scenario: Select a session, scan a badge QR, record the arrival
  Given the page has loaded and the "Session" dropdown is populated
  When the operator selects the session labelled "Opening Plenary · SES-OPEN01"
  Then the "Attendee badge QR" field and the "Record arrival" button become available
  And the field helper reads "Scan or type the badge code, then record the arrival."
  When the operator types the badge code "A1B2C3D4E5F6" into "Attendee badge QR"
  And clicks "Record arrival"
  Then the BFF forwards POST /account/api/admin/sessions/{sessionId}/arrivals
  And the API returns HTTP 200 with ApiResult.Success = true
  And ApiResult.Data carries { UserId, DisplayName, DisplayNameArabic, Status }
  And Status.Arrived = true and Status.Method = "QrScan"
  And a green toast reads "Arrival recorded: <DisplayName>" (e.g. "Arrival recorded: Faisal Al-Harbi")
  And the "Attendee badge QR" field is cleared, ready for the next scan
  And the selected session stays selected
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-hall-arrivals-golden-before.png` (session selected, QR field empty)
- Screenshot after: `docs/screenshots/cp-admin-hall-arrivals-golden-after.png` (green toast with the resolved name, QR field cleared)
- Console errors: 0 expected
- Network: `/account/api/admin/sessions/list` returns 200 on load; `/account/api/admin/sessions/{id}/arrivals` returns 200 on record
- Audit row: `OperationLog` row with `Event = 'HallAttendance.ArrivalRecorded'`, `Outcome = Success`, the operator's id as `ActorUserId`, and `Detail` containing `method=QrScan; attendee=<userId>; operator=<operatorId>`

### E2E-HAR-002 — Session dropdown lists only active sessions

```gherkin
Scenario: The dropdown shows active sessions only, labelled and sorted
  Given three sessions exist: two active, one inactive (IsActive = false)
  When the operator opens /admin/hall-arrivals
  Then GET-equivalent POST /account/api/admin/sessions/list fires with { Top: 200, Sort: "start" }
  And the "Session" dropdown lists exactly the two active sessions
  And each option label reads "{Title} · {Code}"
  And the inactive session does not appear
  And the placeholder reads "Session" until one is chosen
```

### E2E-HAR-003 — Record is the only action and clears the QR on success

```gherkin
Scenario: The page exposes exactly one record action
  Given a session is selected
  Then the only interactive controls are: the "Session" dropdown, the "Attendee badge QR" field, and the "Record arrival" button
  And there is no grid, no Add/Edit/Delete, no Details modal, no filters, and no pager
  When the operator scans a valid badge and clicks "Record arrival"
  Then on success the QR field resets to empty
  And the success toast names the resolved attendee
  And the operator can immediately type the next badge code without reselecting the session
```

### E2E-HAR-004 — Client guard: no session selected

```gherkin
Scenario: Record with no session selected shows a guard toast
  Given the page has loaded but no session is selected
  And a badge code is typed into "Attendee badge QR"
  When the operator clicks "Record arrival"
  Then a red toast reads "Select a session first." / "اختر جلسة أولاً."
  And NO POST /account/api/admin/sessions/{id}/arrivals request fires
  And the QR field keeps its typed value
```

### E2E-HAR-005 — Client guard: blank QR field

```gherkin
Scenario: Record with a blank QR field is a no-op
  Given a session is selected
  And the "Attendee badge QR" field is empty (or whitespace only)
  When the operator clicks "Record arrival"
  Then nothing happens — no toast appears
  And NO POST /account/api/admin/sessions/{id}/arrivals request fires
```

### E2E-HAR-006 — Merge with a prior arrival (idempotent)

```gherkin
Scenario: Re-scanning the same attendee merges into the one open row
  Given a session is selected
  And the attendee with badge "A1B2C3D4E5F6" already has an open arrival for that session
  When the operator scans "A1B2C3D4E5F6" again and clicks "Record arrival"
  Then the API returns HTTP 200 with Status.Arrived = true
  And a green toast reads "Arrival recorded: <DisplayName>"
  And exactly one HallAttendance row exists for that (session, attendee) pair
  And no second audit row is written (the row was not newly created)
```

### E2E-HAR-007 — Empty state (no active sessions)

```gherkin
Scenario: No active sessions renders SimfEmptyState
  Given the database has no active sessions
  When the operator opens /admin/hall-arrivals
  Then the page renders the SimfEmptyState component
  And its title reads "No active sessions to record arrivals for." / "لا توجد جلسات نشطة لتسجيل الوصول إليها."
  And neither the "Session" dropdown, the "Attendee badge QR" field, nor the "Record arrival" button render
  And no error toast appears
```

### E2E-HAR-008 — Auth gate (View permission)

```gherkin
Scenario: Admin lacking HallArrivals.View is denied the page
  Given a signed-in admin whose role does NOT include HallArrivals.View (and is not Administrator "*")
  When they navigate to /admin/hall-arrivals
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/sessions/list request fires
  And the "Module.HallArrivals" nav item is hidden for them (RequiredPermission = HallArrivals.View)
```

### E2E-HAR-009 — Auth gate (Record permission)

```gherkin
Scenario: Admin with View but not Record cannot record
  Given a signed-in admin whose role includes HallArrivals.View but NOT HallArrivals.Record
  When they open /admin/hall-arrivals
  Then the page loads and the "Session" dropdown is shown
  And the <AuthorizedAction> block is hidden — neither the "Attendee badge QR" field nor "Record arrival" button render
  And if a record request is forged directly, POST /api/v1/admin/sessions/{id}/arrivals returns HTTP 403
```

### E2E-HAR-010 — Unknown badge QR

```gherkin
Scenario: An unrecognised badge QR returns 400 with a bilingual message
  Given a session is selected
  When the operator types an unknown badge code "ZZZZZZZZZZZZ" and clicks "Record arrival"
  Then the BFF forwards POST /account/api/admin/sessions/{id}/arrivals
  And the API returns HTTP 400 with ApiResult.Error.Code = "ATTENDEE_QR_UNKNOWN"
  And a red toast surfaces the bilingual MessageForCurrentCulture():
      "That badge QR was not recognised." / "لم يتم التعرّف على رمز الشارة."
  And the QR field keeps its value (no clear on failure)
```

### E2E-HAR-011 — Attendee not approved

```gherkin
Scenario: A non-approved / locked-out attendee returns 403 with a bilingual message
  Given a session is selected
  And the badge "B9X8C7V6N5M4" resolves to an attendee whose AccountState is not Approved (or is locked out)
  When the operator scans that badge and clicks "Record arrival"
  Then the API returns HTTP 403 with ApiResult.Error.Code = "ATTENDEE_NOT_APPROVED"
  And a red toast reads "This attendee's account is not approved for entry." / "حساب هذا الحاضر غير معتمد للدخول."
  And no HallAttendance row is created
```

### E2E-HAR-012 — QR field length cap

```gherkin
Scenario: The QR field caps input at 64 characters
  Given a session is selected
  When the operator pastes a 100-character string into "Attendee badge QR"
  Then the field accepts at most 64 characters (MaxLength="64")
  And the value sent on Record is the operator's input trimmed (RecordQrArrivalRequest.QrId, server .Trim())
```

### E2E-HAR-013 — Server 500 on the session list

```gherkin
Scenario: API 500 on /sessions/list shows the fallback toast
  Given the API is configured to return 500 on /admin/sessions/list (e.g. DB down)
  When the operator opens /admin/hall-arrivals
  Then the loading line "Loading sessions…" shows briefly
  And then a red toast appears reading the server message, or the fallback
      "Something went wrong. Please try again." / "حدث خطأ ما. يرجى المحاولة مرة أخرى."
  And neither the "Session" dropdown nor the QR field render
```

### E2E-HAR-014 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the console
  Given the operator is on /admin/hall-arrivals in English
  When they switch the UI to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "الوصول إلى القاعات (مسح الباب)"
  And the select label reads "الجلسة"
  And the QR field label reads "رمز شارة الحاضر" with helper "امسح أو أدخل رمز الشارة ثم سجّل الوصول."
  And the "Record arrival" button reads "تسجيل الوصول"
  And on a successful scan the success toast renders the Arabic prefix "تم تسجيل الوصول: <DisplayNameArabic>"
```

### E2E-HAR-018 — Staff check-OUT (Record departure)

```gherkin
Feature: Hall-door QR departure recording (2026-07-18)
  As an operator at a hall door
  I want to scan an attendee's badge QR to check them OUT of the session
  So that the hall occupancy and their "confirmed" seat state clear when they leave

Scenario: Select a session, scan a checked-in attendee, record the departure
  Given a session is selected and the attendee "Faisal Al-Harbi" is currently checked IN (open HallAttendance row)
  When the operator types that attendee's badge code and clicks "Record departure"
  Then the BFF forwards POST /account/api/admin/sessions/{sessionId}/departures
  And the API returns HTTP 200 with ApiResult.Data.Status.Arrived = false and a non-null Status.Leave
  And a green toast reads "Departure recorded: Faisal Al-Harbi" / "تم تسجيل الخروج: <DisplayNameArabic>"
  And the "Attendee badge QR" field clears, ready for the next scan
  And the attendee's open HallAttendance row for that session is now closed (Leave set)
  And re-reading the seat map, that attendee's seat is no longer "confirmed / تم التأكيد"
```

### E2E-HAR-019 — Check-out with no prior arrival (idempotent no-op)

```gherkin
Scenario: Recording a departure for an attendee who is not checked in is a harmless no-op
  Given a session is selected and the attendee has NO open attendance row for it
  When the operator scans that attendee's badge and clicks "Record departure"
  Then the API returns HTTP 200 with Status.Arrived = false
  And a green toast still reads "Departure recorded: <DisplayName>" (nothing to close, no error)
  And no HallAttendance row is created or changed
```

### E2E-HAR-020 — Unknown badge QR on departure

```gherkin
Scenario: An unrecognised badge QR on check-out returns 400
  Given a session is selected
  When the operator types an unknown badge code and clicks "Record departure"
  Then the API returns HTTP 400 with ApiResult.Error.Code = "ATTENDEE_QR_UNKNOWN"
  And a red toast reads "That badge QR was not recognised." / "لم يتم التعرّف على رمز الشارة."
  And the QR field keeps its value (no clear on failure)
```

### E2E-HAR-021 — Auth gate (Record) on departure

```gherkin
Scenario: A caller without HallArrivals.Record cannot record a departure
  Given an approved visitor token (no HallArrivals.Record permission)
  When a departure is posted directly to POST /api/v1/admin/sessions/{id}/departures
  Then the API returns HTTP 403 Forbidden
  # In the CP the whole QR field + both buttons are hidden for a View-only admin.
```

---

## Implementation notes

- **API integration tests at a lower layer.** `tests/SIMF.Api.Tests/HallArrivalScanTests.cs`
  already covers this surface at the endpoint level (no browser):
  - `Operator_scan_records_a_qr_arrival` → E2E-HAR-001 (happy path, `Method = QrScan`).
  - `Scan_merges_with_a_prior_geofence_arrival_one_open_row` → E2E-HAR-006 (one open row).
  - `Unknown_qr_is_400` → E2E-HAR-010 (`ATTENDEE_QR_UNKNOWN`).
  - `Non_approved_attendee_is_403` → E2E-HAR-011 (`ATTENDEE_NOT_APPROVED`).
  - `A_non_operator_account_is_forbidden` → E2E-HAR-009 (Record gate, 403).
  - `Operator_scan_records_a_departure` → E2E-HAR-018 (staff check-OUT closes the open row).
  - `Departure_without_a_prior_arrival_is_an_idempotent_noop` → E2E-HAR-019.
  - `Unknown_qr_departure_is_400` → E2E-HAR-020.
  - `A_non_operator_cannot_record_a_departure` → E2E-HAR-021 (Record gate on departures, 403).
  - `Operator_scan_makes_the_per_session_rating_submittable` → E2E-HAR-025
    (DEF-CHK-002 refutation — the door scan alone unlocks the session rating).
  Keep both layers during the transition; the E2E catalogue is the browser-level
  proof that the CP console drives those same outcomes.
- **Picker coverage at the component layer.**
  `tests/SIMF.ControlPanel.Tests/HallArrivalsConsoleSessionPickerTests.cs`
  (bUnit) pins the DEF-CHK-003 rule: an ended session is still offered
  (E2E-HAR-022), a not-yet-started / inactive one is not (E2E-HAR-023), and a
  live session sorts ahead of an ended one.
- **Capacity under concurrency.**
  `tests/SIMF.Api.Tests/HallAttendanceTests.cs`
  → `Concurrent_arrivals_never_exceed_the_hall_capacity` (E2E-HAR-024).
- **End-of-day auto check-out.** Any attendee still checked in when their session
  ends is auto-closed by `HallAttendanceCloseoutWorker.CloseEndedSessionsAsync`
  (`Leave = Session.End`); covered by
  `tests/SIMF.Api.Tests/Operations/HallAttendanceCloseoutWorkerTests.cs`.
- **No grid / no CRUD.** Unlike the lookup-table pages (e.g. `/admin/interests`),
  this is a single record action over a loaded session list. There is no
  Add/Edit/Details/Deactivate surface to cover — the matrix instead enumerates
  the dropdown, the QR field, the Record button, the two client guards, and the
  server error/auth paths.
- **Permission gates** (HARD RULE, CLAUDE.md §Access control): page
  `RequirePermission(HallArrivals.View)`; action `AuthorizedAction(HallArrivals.Record)`;
  nav `Module.HallArrivals` → `RequiredPermission = HallArrivals.View`; API
  `Policies(PolicyFor(HallArrivals.Record), RequireApprovedAccount)`. Both codes
  seed as `AdminOnly`.
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a
  `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) +
  step-definition class. The Gherkin shape is already runner-agnostic.

## On-site remediation (W4 — X-2 / X-3 / X-4)

| Id | Scenario | Category | Priority | Status |
|----|----------|----------|----------|--------|
| E2E-HAR-015 | Session picker only offers currently-live sessions; a stale/future session is rejected with `SESSION_NOT_LIVE` | validation | P0 | _to author_ |
| E2E-HAR-016 | Recording an arrival when the hall is at its physical capacity → `HALL_AT_CAPACITY` | validation | P1 | _to author_ |
| E2E-HAR-017 | Door QR of an approved attendee whose profile-type is inactive → `ATTENDEE_NOT_APPROVED` | validation | P1 | _to author_ |
| E2E-HAR-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-HAR-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

### E2E-HAR-015 — arrival bound to a live session window (± 15 min grace)

```gherkin
Scenario: the API rejects an arrival on a stale session
  Given a session "Opening Keynote" runs 09:00–10:00 and it is now 14:00
  When a client posts an arrival for that session id via /admin/sessions/{id}/arrivals
  Then the API responds 409 with error code SESSION_NOT_LIVE
  And no HallAttendance row is written for that session
  # A session live now (± 15 min grace of its window) is offered and accepted.
  # DEF-CHK-003 (2026-07-26): the session is STILL listed in the picker — it has
  # to be, so the hall can be checked out — but pressing "Record arrival" on it
  # surfaces that same SESSION_NOT_LIVE message in the red toast. See E2E-HAR-022.
```

### E2E-HAR-016 — hall at capacity

```gherkin
Scenario: a full hall rejects a further distinct arrival
  Given the hall "Majlis A" has Capacity 1 and one attendee is currently present
      for the live session
  When the operator scans a SECOND, distinct approved attendee's badge QR
  Then the API responds 409 with error code HALL_AT_CAPACITY
  And a red toast shows the server message "This hall is at capacity." /
      "بلغت هذه القاعة سعتها القصوى."
  # A re-scan by the attendee already present merges (no capacity denial).
```

### E2E-HAR-022 — DEF-CHK-003: an ended session can still be checked out

```gherkin
Scenario: the operator closes out a hall after the session has finished
  Given the session "Closed Plenary" ran 09:00–10:00 and it is now 13:00
  And attendees were checked in at its door
  When the operator opens /admin/hall-arrivals
  Then "Closed Plenary · SES-ENDED" IS listed in the Session dropdown
      (below any session that is live right now)
  When the operator selects it, scans a badge and clicks "Record departure"
  Then POST /account/api/admin/sessions/{sessionId}/departures returns HTTP 200
  And a green toast reads "Departure recorded: <DisplayName>"
  # Before DEF-CHK-003 the session was absent from the picker entirely, so the
  # hall could never be closed out once its window had passed.
```

### E2E-HAR-023 — DEF-CHK-003: sessions the picker still hides

```gherkin
Scenario: a not-yet-started or inactive session is not offered
  Given a session "Tomorrow Keynote" starts in 60 minutes
  And a session "Cancelled Panel" is live-by-clock but IsActive = false
  When the operator opens /admin/hall-arrivals
  Then neither appears in the Session dropdown
  And with no other candidate the page renders SimfEmptyState
      "No active sessions to record arrivals for."
  # No attendance row can exist for a session that has not opened for arrivals,
  # so there is nothing to check in or out.
```

### E2E-HAR-024 — FR-CHK-004: concurrent arrivals fill the hall exactly

```gherkin
Scenario: five arrivals race a hall of capacity two
  Given the live session's hall has Capacity 2 and nobody is present
  When five distinct approved attendees post an arrival at the same instant
  Then exactly 2 arrivals return HTTP 200
  And the other 3 return HTTP 409 with error code HALL_AT_CAPACITY
  And the hall holds exactly 2 open HallAttendance rows
  # Capacity was count-then-decide with no DB constraint, so all five used to be
  # admitted. The count and the insert now share one Serializable transaction
  # (the pattern SeatReservationService already uses), so there is no oversell
  # and no over-reject. The passive hall-door gate path stays deliberately
  # ADVISORY — a person who physically passed a turnstile is always counted.
```

### E2E-HAR-025 — DEF-CHK-002: a door scan alone unlocks the session rating

```gherkin
Scenario: the operator's door scan makes the per-session rating submittable
  Given an approved attendee has no HallAttendance row for the live session
  When they POST /api/v1/app/feedback/submit { code: "Session", targetId: <sessionId> }
  Then the API responds 403 with error code RATING_NOT_ATTENDED
  When the operator scans that attendee's badge at the hall door
  And the attendee retries the same submission
  Then the API responds HTTP 200 and the rating is stored against the session
  # The audit claimed the per-session rating was unreachable because nothing
  # creates HallAttendance automatically. It is reachable: the operator QR scan
  # and the hall-door gate both write the row, keyed on the SAME Identity
  # SimfUser.Id the rating gate reads from the `sub` claim. Only the attendee's
  # self-service geofence arrival is missing, and that is deferred (D-211).
```

### E2E-HAR-017 — inactive profile-type is not admitted at the door

```gherkin
Scenario: an approved holder with a deactivated profile-type is denied
  Given an attendee is Approved and unlocked but their profile-type is IsActive=false
  When the operator scans the attendee's badge QR at the hall door
  Then the API responds 403 with error code ATTENDEE_NOT_APPROVED
  And no HallAttendance row is written
  # Mirrors the perimeter gate's ProfileTypeInactive denial (X-4 unifies them).
```

---

_Last reviewed:_ 2026-07-26 by Claude (DEF-CHK-003 picker keeps ended sessions selectable for check-out; FR-CHK-003 permission wording; FR-CHK-004 capacity race closed; DEF-CHK-002 refuted). Prior: 2026-07-18 (staff check-OUT — Record departure + departures endpoint); 2026-07-11 (W4 on-site remediation — X-2/X-3/X-4 hall-arrival guards); 2026-06-02 (E2E catalogue rebuild).
