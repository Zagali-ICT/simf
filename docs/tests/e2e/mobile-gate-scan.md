# E2E test catalogue — `Gate scanner` (`gateScanner`)

> **Authority:** SIMF E2E test catalogue (D-133). Mobile catalogue — the staff
> gate-operator console (D-406 / D-509, Figma 758:4651/4819/4886). Reached
> from the staff-only drawer entry. Backend already shipped
> (`/app/gates/my-assignments`, `/app/gates/{id}/scans`); backend tests in
> `tests/SIMF.Api.Tests/GateScanTests.cs`. App tests:
> `src/Mobile/simf_app/test/features/gates/`.
>
> **D-509 flow (Figma 758:4651):** the console now opens on a **setup** card —
> a QR glyph, the gate picker, and the **نوع الحركة (دخول/خروج)** movement
> toggle — then **سكان الرمز** opens the live camera. The operator's direction
> choice is sent on the scan and **honoured for a Both-mode gate**; a fixed
> In/Out gate locks the toggle to its one direction (no CP round-trip).
>
> **D-737 (unified scanner):** the camera stage now uses the shared
> `SimfScannerBody` (`lib/app/widgets/simf_scanner_body.dart`) — the old bespoke
> `gate_scanner_view.dart` is **deleted**. Same behaviour, plus one shared
> `ScanGate` dedupe/single-flight policy and a visible camera-permission-denied
> error card (بدل الشاشة السوداء) with the manual field always usable below it.

| | |
|--|--|
| **Page** | mobile gate console (Figma 758:4380/4651/4735/4819/4886) |
| **Route** | app screen #105 `/gates/scan` |
| **Surface** | Mobile (Flutter) |
| **Role/gate** | App: `AppRole.staff` (router role-gate, D-405/406). Server: `Gates.Operate` permission (`GateOperator`) + gate assignment (403 otherwise) |
| **Test runner** | Flutter widget/unit test + device manual (live camera) |

---

### E2E-MOBGATE-000 — Setup: choose gate + movement type (Figma 758:4651)

```gherkin
Scenario: A Both-mode gate requires a movement choice before scanning
  Given a staff operator opens the gate scanner from the drawer
  And they are assigned to a Both-mode gate (GET /app/gates/my-assignments)
  Then the setup card shows the QR glyph, the gate picker and the دخول/خروج toggle
  And "سكان الرمز / Scan code" is disabled with the "choose the movement type first" hint
  When they tap "دخول / Entry" (or "خروج / Exit")
  Then "سكان الرمز" enables and opens the live camera

Scenario: A fixed In/Out gate locks the toggle and can scan immediately
  Given the assigned gate's DirectionMode is In (or Out)
  Then the toggle is locked to that direction and "سكان الرمز" is enabled at once
```

### E2E-MOBGATE-001 — Golden allowed scan (مسموح) honours the direction

```gherkin
Scenario: A valid badge is allowed in
  Given the operator picked دخول/خروج on the setup card and opened the camera
  When they scan (or enter) a valid badge code
  Then POST /app/gates/{gateId}/scans is sent with the chosen direction
  And it returns outcome=Allowed (HTTP 200)
  And the green "مسموح / Allowed" card shows the holder name, type, gate, direction
  And on a Both-mode gate the recorded الحركة matches the operator's choice
  And "سكان مرة أخرى" returns to the scanner for the next person
```

### E2E-MOBGATE-006 — Allowed, but no session attendance recorded (DEF-CHK-004)

```gherkin
Scenario: A hall-door gate is scanned outside every session window
  Given the operator's gate is a HALL-DOOR gate (Gate.HallId is set)
  And no session is running in that hall right now (nor within the 15 min grace)
  When they scan a valid badge code
  Then the scan returns outcome=Allowed (HTTP 200) — the holder is still admitted
  And the response carries noticeMessage (already localized by Accept-Language)
  And the green "مسموح / Allowed" card shows that advisory in amber under the subtitle
      "تم السماح بالدخول، ولكن لم يتم تسجيل حضور الجلسة لهذا المسح."
  And a GateScan row is written; NO HallAttendance row is written

Scenario: A fixed Out gate closes nothing
  Given the operator's gate has DirectionMode = Out and HallId set
  And a session IS live in that hall
  And the badge holder has no open HallAttendance row for it
  When they scan that badge code
  Then the scan returns outcome=Allowed and carries the SAME advisory noticeMessage
  # The check-out closed nothing, so nothing was recorded. The advisory wording
  # names no single cause because the server reports all cases identically.

Scenario: A check-IN whose attendance insert never lands
  Given the operator's gate has DirectionMode = In and HallId set
  And a session IS live in that hall
  And the store rejects the HallAttendance insert (deadlock / timeout / lost race)
  When they scan that badge code
  Then the scan returns outcome=Allowed and carries the SAME advisory noticeMessage
  And NO HallAttendance row is written
  # The arrival branch used to report success unconditionally, so the operator
  # read a plain "Allowed" as "counted" while the attendance was lost.

Scenario: An ordinary scan shows no advisory
  Given a session IS live in that hall (or the gate is a perimeter gate)
  When they scan a valid badge code
  Then noticeMessage is null and the allowed card renders exactly as before
```

**Evidence:** API `GateHallDoorChainTests.Hall_door_gate_with_no_live_session_returns_an_allowed_scan_carrying_a_notice`
(+ `..._bound_to_a_live_session_carries_no_notice`, `Perimeter_gate_carries_no_notice`,
`Fixed_out_gate_with_no_open_row_carries_the_advisory_notice`,
`Fixed_out_gate_that_closes_an_open_row_carries_no_notice`,
`Gate_door_arrival_that_persisted_no_row_does_not_report_attendance_recorded`);
app decode `test/features/gates/gate_models_test.dart` — "an allowed scan can
carry an advisory notice". `noticeMessage` is an **additive** field on the
shipped wire contract; an older app build simply ignores it.

### E2E-MOBGATE-002 — Denied scan (ممنوع)

```gherkin
Scenario: An invalid/ineligible badge is denied
  When the scan returns outcome=Denied (HTTP 200) with a denialReasonCode + message
  Then the red "ممنوع / Denied" card shows the server's denial message
  And the reference shows "لا يوجد / None" and the type a dash when unknown
```

### E2E-MOBGATE-003 — Manual entry + back-to-setup

```gherkin
Scenario: Manual entry when the camera can't read the code
  When the operator types the code on the scanner and taps "تحقّق / Check"
  Then the same scan flow runs (with the chosen direction)

Scenario: Back returns to setup, not out of the console (D-509)
  Given the operator is on the camera/scanner stage
  When they tap the AppBar back (or system back)
  Then they return to the setup card (where they can change the gate/direction)
  And tapping back again from setup leaves the console
```

### E2E-MOBGATE-004 — Role / authority / failures

```gherkin
Scenario: Non-staff cannot reach the scanner
  Given a signed-in visitor/moderator
  Then the drawer shows no "مسح البوابة" entry
  And navigation to /gates/scan redirects home (D-406 role gate)

Scenario: Staff without the GateOperator grant (403)
  Given the user is AppRole.staff but lacks Gates.Operate
  When GET /app/gates/my-assignments returns 403
  Then the "not authorised to operate gates" state shows

Scenario: No assignments
  When my-assignments returns an empty list
  Then the "not assigned to any gate" state shows

Scenario: Infra failures
  When a scan returns 429 (circuit/rate)
  Then the "too many attempts" toast shows and no result card appears
  When my-assignments returns 500
  Then the error + Retry surface shows

Scenario: RTL
  Given the app language is Arabic
  Then the scanner, result card and fields render right-to-left
```

### E2E-MOBGATE-005 — Unified scanner: camera-first + camera-denied error card (D-737)

```gherkin
Scenario: The camera stage is the shared SimfScannerBody
  Given the operator picked دخول/خروج and tapped "سكان الرمز"
  Then the gold-bracket viewfinder (SimfScannerFrame) opens over the bounded live camera
  And the "إيقاف الكاميرا / Stop camera" control sits OUTSIDE the camera surface (EMUI-safe)
  And the manual field + "تحقّق / Check" stay usable below

Scenario: A denied / missing camera shows the error card, not a black box
  Given the console opens the camera stage
  When the OS denies the camera permission (or the device has no camera)
  Then the shared error card shows
       "تعذّر تشغيل الكاميرا. فعّل إذن الكاميرا من إعدادات النظام، أو أدخل الرمز يدويًا بالأسفل." /
       "Camera unavailable. Enable camera permission in system settings, or type the code below."
  And a "إعادة المحاولة / Try again" retry control is offered
  And the operator can still type the badge code and run the same scan flow

Scenario: A steady badge under the camera fires one scan
  Given the camera is live and reading ~1 frame/second
  When a badge stays in the viewfinder
  Then POST /app/gates/{gateId}/scans runs ONCE (ScanGate single-flight + dedupe)
  And re-presenting the badge after the result lets the next person be scanned (onNoCode reset)

Scenario: The viewfinder is sized for the device it runs on (BUG-019 / 19e)
  Given the gate console runs on a phone
  Then the viewfinder card keeps the Figma 343px width
  Given the gate console runs on a wide tablet panel
  Then the viewfinder card scales up (clamped, not stretched edge-to-edge)
    instead of rendering as a small phone-sized card
  And on a very narrow window it never exceeds the screen width
```

**Evidence:** source-verified — `simf_scanner_body.dart` renders `_CameraErrorCard`
on a controller error / the 8 s watchdog (device-only render). `simf_scanner_body_test`
covers the always-mounted manual field with the camera off; `scan_gate_test`
(single-flight + same-code dedupe + `onNoCode` reset). Gate scan outcomes remain
covered by `GateScanTests` + `test/features/gates/`.

---

_Last reviewed:_ `2026-07-27` by `SIMF Team` — DEF-CHK-004 advisory
`noticeMessage` now also covers a check-IN whose attendance insert never lands
(E2E-MOBGATE-006). Earlier: `2026-07-27` fixed-Out scan that closes nothing;
`2026-07-26` DEF-CHK-004 advisory `noticeMessage`;
`2026-07-11` D-737 unified scanner (SimfScannerBody; `gate_scanner_view.dart`
deleted); `2026-06-27`.
_Last reviewed:_ `2026-07-26` by `SIMF Team` — BUG-019 / 19d + 19e: the shared
viewfinder's raw `Color(0x…)` / `Colors.black|white` moved to `SimfTokens`, and the
card width is now responsive (`WindowSize`), locked by
`test/app/widgets/simf_scanner_frame_test.dart`. Earlier: `2026-07-11` (D-737
unified scanner; `gate_scanner_view.dart` deleted), `2026-06-27`.
