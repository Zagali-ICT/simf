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

---

_Last reviewed:_ `2026-06-27` by `SIMF Team`.
