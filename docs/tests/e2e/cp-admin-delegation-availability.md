# E2E test catalogue — Delegation availability (`/admin/delegation-availability`)

| | |
|--|--|
| **Page** | [`cp/admin-delegation-availability.md`](../../pages/cp/admin-delegation-availability.md) |
| **Route** | `/admin/delegation-availability` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-07-22 (bi-meeting rework — new page) |

> **What this page does (grounded in `DelegationAvailabilityPage.razor(.cs)`, bi-meeting rework).**
> The team defines an **invited country's delegation** availability windows (Start/End in Saudi time
> + slot length); the delegation-meeting flow chops each window into free slots an entitled
> app user reads via `GET /app/countries/{id}/available-slots`. It is the delegation twin of
> [`cp-admin-speaker-availability.md`](cp-admin-speaker-availability.md): a country `<select>`
> (invited countries only), an add-window form (Start `Admin.DelegationAvailability.Start`
> "Start (Saudi time)" / "البداية (بتوقيت السعودية)"; End; slot minutes `Admin.DelegationAvailability.SlotMinutes`
> "Slot length (minutes)" / "مدة الفترة (دقائق)"), and the selected country's window list with a
> quiet trash **Delete** action. The window list renders each start/end on the Saudi wall clock in
> 12-hour form (e.g. `2026-11-20 10:00 AM – 10:30 AM`). The page + delete are gated by `DelegationMeetings.Manage`
> (page attribute, nav item, and the `<AuthorizedAction>` on Delete); the backend LIST endpoint
> needs only `DelegationMeetings.View`. API:
> `GET`/`POST /admin/countries/{countryId:int}/availability-windows`,
> `DELETE /admin/delegation-availability-windows/{id:guid}`,
> `GET /app/countries/{countryId:int}/available-slots` — covered by
> `tests/SIMF.Api.Tests/DelegationAvailabilityTests.cs` (5/5).

> **BFF proxy.** The page calls the Control Panel proxy, never the API directly:
> country picker `POST /account/api/admin/countries/list` (`GridQuery { Top = 500 }`);
> forum-day bounds `GET /account/api/admin/programme/forum-window`; list
> `GET /account/api/admin/countries/{id}/availability-windows`; create
> `POST /account/api/admin/countries/{id}/availability-windows`
> (`CreateDelegationAvailabilityWindowRequest`); delete
> `DELETE /account/api/admin/delegation-availability-windows/{windowId}`.

> **Slot + window rules.** Slot length is clamped to **5–480 minutes**
> (`VALIDATION_FAILED`). A window must end after it starts and fit at least one whole slot
> (`VALIDATION_FAILED`). When the programme has authored days, a window is bounded to the
> **forum days** (converted to KSA local, UTC+3) — `VALIDATION_FAILED` otherwise; with no
> programme days seeded the bound is skipped and the pickers carry no min/max (the server
> still enforces on submit). A non-invited country target is rejected
> `DELEGATE_COUNTRY_NOT_INVITED` (400). Deleting an unknown window is
> `DELEGATION_AVAILABILITY_WINDOW_NOT_FOUND` (404).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-DAV-001 | Pick an invited country, add a 60-min window @ 30-min slots → it lists; the free-slots read yields 2 slots | happy | P0 | authored ✓ (`Create_window_then_it_lists_and_yields_slots`, API) |
| E2E-DAV-002 | Delete a window → it leaves the list and its slots disappear | happy | P1 | authored ✓ (`Delete_window_removes_its_slots`, API) |
| E2E-DAV-003 | Invalid window (end ≤ start, or shorter than one slot) → 400 `VALIDATION_FAILED`; a non-invited country target → 400 `DELEGATE_COUNTRY_NOT_INVITED`; no row added | error | P0 | authored ✓ (`An_invalid_window_is_400_and_a_non_invited_country_is_400`, API) |
| E2E-DAV-004 | A slot already held by a live delegation meeting (Accepted / AwaitingSpeaker / Done, either country) is not offered | edge | P0 | authored ✓ (`A_live_delegation_meeting_slot_is_excluded_from_the_free_slots`, API) |
| E2E-DAV-005 | Forum-day bound — a window outside the event days is rejected 400 `VALIDATION_FAILED`; pickers carry the forum min/max | error | P0 | authored ✓ (`Create_window_outside_the_forum_window_is_400`, API) |
| E2E-DAV-006 | Auth gate — admin lacking `DelegationMeetings.Manage` → `/not-permitted`; nav item hidden; a `View`-only admin can list but sees no Delete action | auth | P0 | _to author_ (gate verified by `CpNavigationPermissionTests` / `PermissionEnforcementTests`) |
| E2E-DAV-007 | RTL / Arabic render — page + add form mirror; title "أوقات إتاحة الوفود" | i18n | P1 | _to author_ |
| E2E-DAV-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-DAV-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-DAV-001/002 — Define + remove a delegation window

```gherkin
Feature: Delegation availability windows
Background:
  Given an Administrator with DelegationMeetings.Manage has signed in to the Control Panel
  And a country is marked invited (Country.IsInvited)
  And they are on /admin/delegation-availability with that country selected

Scenario: Add a window and see its free slots
  When they add a window 2026-11-20 10:00-11:00 (Saudi time) with 30-minute slots
  Then POST /account/api/admin/countries/{id}/availability-windows returns 200
  And a green toast reads "Window added." / "تمت إضافة الفترة."
  And the window appears under "Windows" / "الفترات" (row shows "30 min slots" / "30 دقيقة لكل فترة")
  And GET /app/countries/{id}/available-slots returns two 30-minute slots

Scenario: Delete a window
  When they click the quiet trash Delete action on the window
  Then DELETE /account/api/admin/delegation-availability-windows/{id} returns 200
  And the window leaves the list and the country has no free slots
```

**Evidence:** `DelegationAvailabilityTests.Create_window_then_it_lists_and_yields_slots`,
`DelegationAvailabilityTests.Delete_window_removes_its_slots` (both green).

### E2E-DAV-003 — Window + country validation

```gherkin
Scenario: An out-of-order or sub-slot window is rejected
  When they add a window whose End is on/before its Start (or a window shorter than one slot)
  Then POST .../availability-windows returns 400 VALIDATION_FAILED
    (bilingual toast: "The window must end after it starts and fit at least one slot." /
    "يجب أن تنتهي الفترة بعد بدايتها وأن تتّسع لفترة واحدة على الأقل.")
  And no window is added
  # The client also blocks obviously-bad input before the POST:
  # Admin.DelegationAvailability.BadDates = "Enter a valid start and end." / "أدخل بداية ونهاية صحيحتين.";
  # Admin.DelegationAvailability.BadSlot = "Enter a positive slot length." / "أدخل مدة فترة موجبة."

Scenario: A slot length outside 5-480 minutes is rejected
  When they add a window with a slot length of 3 minutes (or 600 minutes)
  Then POST .../availability-windows returns 400 VALIDATION_FAILED
    ("Slot length must be between 5 and 480 minutes." /
    "يجب أن تتراوح مدة الفترة بين 5 و 480 دقيقة.")

Scenario: A non-invited country cannot hold delegation availability
  When a window is POSTed for a countryId whose Country.IsInvited is false
  Then the API returns 400 DELEGATE_COUNTRY_NOT_INVITED
    ("The delegation is not an invited country." / "الوفد ليس من الدول المدعوّة.")
  # The picker only lists invited countries, so this is reachable via a scripted client.
```

**Evidence:** `DelegationAvailabilityTests.An_invalid_window_is_400_and_a_non_invited_country_is_400` (green).

### E2E-DAV-004 — A live delegation meeting slot is excluded (double-book guard)

```gherkin
Scenario: A slot held by a live meeting is not offered as free
  Given country SA has an availability window covering 10:00-11:00 (two 30-min slots)
  And a delegation meeting involving SA (as requester OR target) is Accepted / AwaitingSpeaker / Done
      on the 10:00-10:30 slot
  When GET /app/countries/{SA}/available-slots is read
  Then the 10:00-10:30 slot is absent and only the 10:30-11:00 slot is offered
  # SlotHolding = { Accepted, AwaitingSpeaker, Done }; the overlap is half-open
  # (t.Start < slotEnd && slotStart < t.End) and matches either the requesting or target country.
```

**Evidence:** `DelegationAvailabilityTests.A_live_delegation_meeting_slot_is_excluded_from_the_free_slots` (green).

### E2E-DAV-005 — Forum-day bound

```gherkin
Feature: Delegation availability windows are bounded to the forum days
Background:
  Given the programme has authored days (the forum window, e.g. 2026-11-20..22)
  And an Administrator is on /admin/delegation-availability with an invited country selected

Scenario: A window outside the event days is rejected
  When they add a window on a day AFTER the last forum day
  Then POST .../availability-windows returns 400 VALIDATION_FAILED
    (bilingual toast: "Availability windows can only be set within the forum days
    (2026-11-20 to 2026-11-22)." /
    "لا يمكن تحديد فترات التوفّر إلا خلال أيام الملتقى (2026-11-20 إلى 2026-11-22).")
  And no window is added

Scenario: The Start / End pickers advertise the forum-day min/max
  When the add-window form renders
  Then GET /account/api/admin/programme/forum-window returns the MinDate/MaxDate
  And the Start and End datetime-local fields carry those bounds
  # When no programme days exist the bound is skipped and the pickers carry no min/max;
  # the server still enforces on submit. The client out-of-range toast is
  # Admin.DelegationAvailability.BadDateRange = "Dates must be within {0}." /
  # "يجب أن تكون التواريخ ضمن {0}." with {0} built from the live forum window.
```

**Evidence:** `DelegationAvailabilityTests.Create_window_outside_the_forum_window_is_400` (green).

### E2E-DAV-006 — Auth gate

```gherkin
Scenario: An admin lacking DelegationMeetings.Manage is denied the page
  Given a signed-in admin whose role does NOT grant DelegationMeetings.Manage
  When they navigate to /admin/delegation-availability
  Then they land on /not-permitted with HTTP 200
  And the nav item (RequiredPermission = DelegationMeetings.Manage) is hidden from the rail

Scenario: A View-only admin can read windows but not delete
  Given a signed-in admin whose role grants DelegationMeetings.View but NOT .Manage
  # The page attribute requires .Manage, so a pure View-only admin does not reach the page;
  # the LIST endpoint itself needs only .View (used by the app slots read + any future viewer).
  When the DELETE /admin/delegation-availability-windows/{id} is issued directly with only View
  Then the API returns HTTP 403 and the window is not removed
```

### E2E-DAV-007 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the page + add form
  Given the administrator is on /admin/delegation-availability in English
  When they switch the language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the title reads "أوقات إتاحة الوفود"
  And the country picker label reads "الوفد (الدولة)" with the empty option "اختر دولة مدعوّة…"
  And the add-window heading reads "إضافة فترة إتاحة", the fields "البداية (بتوقيت السعودية)" / "النهاية (بتوقيت السعودية)" /
      "مدة الفترة (دقائق)", and the button "إضافة فترة"
  And the windows heading reads "الفترات" (empty state "لا توجد فترات إتاحة بعد.")
  And no element overflows horizontally (scrollWidth == clientWidth)
```

---

## Implementation notes

- **API integration tests** — [`tests/SIMF.Api.Tests/DelegationAvailabilityTests.cs`](../../../tests/SIMF.Api.Tests/DelegationAvailabilityTests.cs) (5/5, all green): create→list→2 slots; a live delegation-meeting slot excluded; invalid window + non-invited country both 400; delete clears slots; window outside the forum window 400.
- **Backing surface:**
  - Admin — `POST /admin/countries/{countryId:int}/availability-windows` (`DelegationMeetings.Manage`),
    `GET /admin/countries/{countryId:int}/availability-windows` (`DelegationMeetings.View`),
    `DELETE /admin/delegation-availability-windows/{id:guid}` (`DelegationMeetings.Manage`) — all `RequireApprovedAccount`
  - App — `GET /app/countries/{countryId:int}/available-slots` (`RequireApprovedAccount`, no permission gate)
  - Permissions — `PermissionCatalog.DelegationMeetings.View` / `.Manage` (reused — **no new permission code, no seeder / migration**)
  - Error codes — `DELEGATE_COUNTRY_NOT_INVITED` (400), `VALIDATION_FAILED` (400: slot 5–480, window ordering, forum-day bound), `DELEGATION_AVAILABILITY_WINDOW_NOT_FOUND` (404)
- **Nav / gate tests** — `CpNavigationPermissionTests` (the nav item's `RequiredPermission`) and `PermissionEnforcementTests` fail the build if a gate is missing.
- **Free-slot derivation** subtracts delegation `MeetingRequestStatuses.SlotHolding` rows (`Accepted` + `AwaitingSpeaker` + `Done`) where the country is the requester or the target — the same shape as the hall / speaker slot exclusion — so a booked delegation slot is never re-offered.

---

_Last reviewed:_ 2026-07-22 by Claude — bi-meeting rework: new delegation-availability admin page (clone of the speaker-availability stack), gated `DelegationMeetings.Manage`; backed by `DelegationAvailabilityTests` (5/5). E2E-DAV-006 (auth gate) + E2E-DAV-007 (RTL) remain browser-authored.
