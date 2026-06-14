# E2E test catalogue — `Gate scanner` (`gateScanner`)

> **Authority:** SIMF E2E test catalogue (D-133). Mobile catalogue — the staff
> gate-operator console (D-406, Figma 758:4380/4651/4735/4819/4886). Reached
> from the staff-only drawer entry. Backend already shipped
> (`/app/gates/my-assignments`, `/app/gates/{id}/scans`); backend tests in
> `tests/SIMF.Api.Tests/GateScanTests.cs`. App tests:
> `src/Mobile/simf_app/test/features/gates/`.

| | |
|--|--|
| **Page** | mobile gate console (Figma 758:4380/4651/4735/4819/4886) |
| **Route** | app screen #105 `/gates/scan` |
| **Surface** | Mobile (Flutter) |
| **Role/gate** | App: `AppRole.staff` (router role-gate, D-405/406). Server: `Gates.Operate` permission (`GateOperator`) + gate assignment (403 otherwise) |
| **Test runner** | Flutter widget/unit test + device manual (live camera) |

---

### E2E-MOBGATE-001 — Golden allowed scan (مسموح)

```gherkin
Scenario: A valid badge is allowed in
  Given a staff operator opens the gate scanner from the drawer
  And they are assigned to a gate (GET /app/gates/my-assignments)
  When they scan (or enter) a valid badge code
  Then POST /app/gates/{gateId}/scans returns outcome=Allowed (HTTP 200)
  And the green "مسموح / Allowed" card shows the holder name, type, gate, direction
  And "سكان مرة أخرى" returns to the scanner for the next person
```

### E2E-MOBGATE-002 — Denied scan (ممنوع)

```gherkin
Scenario: An invalid/ineligible badge is denied
  When the scan returns outcome=Denied (HTTP 200) with a denialReasonCode + message
  Then the red "ممنوع / Denied" card shows the server's denial message
  And the reference shows "لا يوجد / None" and the type a dash when unknown
```

### E2E-MOBGATE-003 — Manual entry + Hold

```gherkin
Scenario: Manual entry when the camera can't read the code
  When the operator types the code and taps "تحقّق / Check"
  Then the same scan flow runs

Scenario: Hold pauses auto-scanning (client-only)
  When the operator taps "إيقاف مؤقت / Hold"
  Then auto-detection pauses and the button becomes "استئناف / Resume"
  # There is no server "hold" outcome (D-406) — this is a local pause only.
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

_Last reviewed:_ `2026-06-14` by `SIMF Team`.
