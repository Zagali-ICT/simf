# E2E test catalogue — `Guest seating desk` (`staffSeating`)

> **Authority:** SIMF E2E test catalogue (D-133 / D-245). Mobile catalogue — the
> **staff seating desk** (D-771, owner 2026-07-26). Derived from the visitor seat
> picker (#109 `seat_picker_screen.dart`): the same shared `HallSeatMapCard` hall
> plan, but a tap ASKS WHO SITS THERE instead of selecting a seat, and a badge
> scanner above the plan answers the opposite question — where does this guest
> sit.
>
> Reached from the **session detail header** when the signed-in user resolves to
> `AppRole.staff` (the same trailing slot the Moderator uses for the Q&A desk;
> the two roles are disjoint, D-519).
>
> Backend: `POST /app/staff/sessions/{id}/seating/by-badge`,
> `GET /app/staff/sessions/{id}/seating/seat`,
> `GET /app/staff/sessions/{id}/seating/occupant/{userId}/photo`
> (`src/Backend/SIMF.Api/Endpoints/Staff/StaffSeatingEndpoints.cs`). Backend
> tests: `tests/SIMF.Api.Tests/SeatTierEligibilityTests.cs`. App tests:
> `src/Mobile/simf_app/test/features/staff/staff_seating_screen_test.dart`.
>
> **Seat tiers (D-771):** the hall layout carries a per-row tier — `Normal`,
> `Vip`, `Vvip`. A **VVIP** seat has **no registration**: an administrator blocks
> it from the Control Panel seat plan and types a manual bilingual guest note
> ("هذا المقعد محجوز لمعالي الوزير"), and that note IS the occupant record the
> desk displays.

| | |
|--|--|
| **Page** | mobile staff seating desk |
| **Route** | app screen #118 `/staff/seating/:sessionId` |
| **Surface** | Mobile (Flutter, tablet-first) |
| **Role/gate** | App: `AppRole.staff` (router role-gate). Server: `Seating.Assist` permission, granted to the Staff + Moderator app roles via `PermissionCatalog.OperationalPermissionsForAppRole` — **no admin RBAC role required**; any other account is 403 |
| **Test runner** | Flutter widget test + device manual (live camera) |

## Coverage matrix

| Function | Scenario ids |
|---|---|
| Golden: tap a seat → occupant (reference, name, photo) | E2E-MOBSEATDESK-001 |
| Golden: scan a badge → the guest's seat | E2E-MOBSEATDESK-002 |
| VVIP seat → the administrator's manual guest note | E2E-MOBSEATDESK-003 |
| Empty seat / badge with no seat in this session | E2E-MOBSEATDESK-004 |
| Unknown badge (validation / 404) | E2E-MOBSEATDESK-005 |
| Auth gate — a visitor is 403'd | E2E-MOBSEATDESK-006 |
| Server 500 / network failure | E2E-MOBSEATDESK-007 |
| Tablet layout + RTL + accessible names | E2E-MOBSEATDESK-008 |

---

### E2E-MOBSEATDESK-001 — Golden: tap a seat → who sits there

```gherkin
Scenario: Staff taps an occupied seat and reads the reservation back to the guest
  Given a staff account (ProfileType.MobileAppRole = Staff, Approved) is signed in
  And session "Opening" is in hall H-1 whose layout is A(VVIP), B(VIP), C(Normal)
  And visitor "Sara Al Otaibi" holds seat C2 in that session
  When staff opens the session detail and taps the seat icon in the header
  Then the seating desk opens at /staff/seating/{sessionId}
  And the hall plan renders every row, including the VVIP and VIP rows
  When they tap seat C2
  Then GET /app/staff/sessions/{id}/seating/seat?rowLabel=C&seatNumber=2 returns 200
  And the result card shows the reference (reservation id), "Row C · Seat 2",
      the guest name "Sara Al Otaibi" / "سارة العتيبي" and the tier "Normal / عادي"
  And the guest photo is fetched through the AUTHENTICATED bytes path
      (GET …/seating/occupant/{userId}/photo with the bearer token — D-422),
      never a raw Image.network
  And the check-in line reads "Checked in / سجّل الدخول" when a HallAttendance exists
```

### E2E-MOBSEATDESK-002 — Golden: scan a badge → where the guest sits

```gherkin
Scenario: Staff scans a guest badge and directs them to their seat
  Given the same signed-in staff account and session
  And visitor "Sara Al Otaibi" has badge QR "ABC123XYZ789" and holds seat C2
  When staff scans (or types) "ABC123XYZ789" in the shared scanner and taps "بحث / Look up"
  Then POST /app/staff/sessions/{id}/seating/by-badge returns 200 with found=true
  And the result card shows "Row C · Seat 2", the reference id and the guest's name + photo
```

### E2E-MOBSEATDESK-003 — A VVIP seat shows the administrator's manual guest note

```gherkin
Scenario: A protocol seat has no registration, only the admin's note
  Given an administrator blocked seat A1 from the Control Panel seat plan
  And typed the guest note "هذا المقعد محجوز لمعالي الوزير" / "Reserved for the Minister"
  When staff taps seat A1 on the desk
  Then the result card shows the guest NOTE where a name would be
  And the tier line reads "شخصيات بالغة الأهمية / VVIP"
  And no user id / photo is returned (userId is null, hasPhoto is false)
```

### E2E-MOBSEATDESK-004 — Empty seat and badge-with-no-seat

```gherkin
Scenario: An empty seat is a valid answer, not an error
  When staff taps a free seat C4
  Then the endpoint returns 200 with found=false
  And the card reads "هذا المقعد شاغر / This seat is empty"

Scenario: A valid badge that holds no seat in this session
  Given visitor "Walk In" has a badge but no reservation in this session
  When staff looks that badge up
  Then the endpoint returns 200 with found=false and the guest's identity filled in
  And the card reads "لا يوجد مقعد لهذا الضيف في هذه الجلسة /
      This guest has no seat in this session"
```

### E2E-MOBSEATDESK-005 — Unknown / empty badge

```gherkin
Scenario: An unrecognised badge code
  When staff looks up "NOTABADGE00"
  Then the endpoint returns 404 ATTENDEE_QR_UNKNOWN
  And the card reads "لم يتم التعرف على هذه البطاقة. / That badge was not recognised."

Scenario: An empty badge code
  When staff submits an empty code
  Then the endpoint returns 400 VALIDATION_FAILED
      ("امسح رمز البطاقة أو اكتبه." / "Scan or type a badge code.")

Scenario: A seat outside the hall layout
  When a seat lookup names a row or seat number the layout does not contain
  Then the endpoint returns 400 SEAT_OUT_OF_BOUNDS
```

### E2E-MOBSEATDESK-006 — Auth gate

```gherkin
Scenario: An ordinary visitor cannot use the desk
  Given an approved VISITOR account is signed in
  Then /staff/seating/:sessionId is not reachable from the app (no header action;
      the router role-gate blocks a deep link)
  And calling GET /app/staff/sessions/{id}/seating/seat with that token returns 403

Scenario: The photo endpoint cannot be used to enumerate attendees
  Given a staff account with Seating.Assist
  When it requests …/seating/occupant/{userId}/photo for a user who holds NO active
      reservation in that session
  Then the endpoint returns 404 (never the bytes)
```

### E2E-MOBSEATDESK-007 — Server failure

```gherkin
Scenario: A 500 or network error surfaces a readable message, not a blank card
  Given the seat lookup fails with HTTP 500
  Then the result card reads "تعذّر تنفيذ البحث. / The lookup failed."
  And the desk stays usable (the plan is still tappable; "مسح النتيجة / Clear result" resets it)

Scenario: The seat map itself fails to load
  Then the shared SeatMapAsyncView error + retry state is shown, and retry re-fetches
```

### E2E-MOBSEATDESK-008 — Tablet layout, RTL and accessibility

```gherkin
Scenario: The desk adapts to a tablet without hardcoded breakpoints
  Given the device width is >= the medium WindowSize bucket
  Then the scanner card and the result card sit side by side
  And on a compact phone they stack, scanner first
  And the body is capped and centred by MaxWidthBody (no edge-to-edge stretch)

Scenario: Arabic renders right-to-left with the hall plan unmirrored
  Given the app locale is ar
  Then the page body is RTL and every label reads in Arabic
  And the hall plan itself stays LTR (venue order — L-7), as on the visitor picker

Scenario: Every control has an accessible name
  Then the badge field, the "بحث / Look up" button, "مسح النتيجة / Clear result",
      the guest photo ("صورة الضيف / Guest photo") and every seat cell
      (row+seat, plus its state word) expose a Semantics label
```

---

## Seat-tier eligibility (visitor side — the rule this desk exists alongside)

These belong to the visitor seat picker but are recorded here because the tier is
the same data:

```gherkin
Scenario: VIP visitor reserves a VIP seat            -> 200
Scenario: Normal visitor reserves a VIP seat         -> 409 SEAT_TIER_NOT_ELIGIBLE
Scenario: Anyone reserves a VVIP seat                -> 409 SEAT_TIER_RESERVED
Scenario: VIP visitor reserves a Normal seat         -> 200
Scenario: Auto-pick for a Normal visitor             -> skips the VVIP + VIP rows
Scenario: A layout saved before D-771 (no tiers)     -> every row books as Normal
```

Backend coverage: `tests/SIMF.Api.Tests/SeatTierEligibilityTests.cs`.
App coverage: `src/Mobile/simf_app/test/features/sessions/seat_tier_test.dart`.
