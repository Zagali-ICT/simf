# E2E test catalogue — Gates operations dashboard (`/admin/gates/dashboard`)

| | |
|--|--|
| **Page** | [`cp/admin-gates-dashboard.md`](../../pages/cp/admin-gates-dashboard.md) |
| **Route** | `/admin/gates/dashboard` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page nature.** This is a **read-only** operations dashboard (D-199). It has
> NO create / edit / delete — gate CRUD lives at `/admin/gates` and check-in /
> check-out happen at the operator console `/admin/gates/operator`. The whole
> surface is: one **Refresh** button, two **stat cards** (Currently inside,
> Gates), and two read-only tables (the *Currently inside* roster and the
> *Gates* roster). It consumes exactly two BFF passthroughs (D-148), both
> fired once on first interactive render:
> - `GET  /account/api/admin/gates/reports/currently-inside`
>   → `ApiResult<IReadOnlyList<AdminCurrentlyInsideRow>>`
> - `POST /account/api/admin/gates/list` with `{ "Top": 200 }`
>   → `ApiResult<GridPage<AdminGateSummary>>`
>
> Both API endpoints are gated by `PermissionCatalog.Gates.Manage` +
> `RequireApprovedAccount`; the page itself carries
> `@attribute [RequirePermission(PermissionCatalog.Gates.Manage)]`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-GDS-001 | Golden path — page loads, both tables + both stat cards render, counts agree | happy | P0 | _to author_ |
| E2E-GDS-002 | Refresh button re-fires both calls and updates the *Currently inside* count | happy | P0 | _to author_ |
| E2E-GDS-003 | Stat cards reflect the row counts of their tables | happy | P1 | _to author_ |
| E2E-GDS-004 | Empty *Currently inside* roster renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-GDS-005 | Empty *Gates* roster renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-GDS-006 | Gate `Active` / `Inactive` pill renders the correct `SimfPill` variant | happy | P1 | _to author_ |
| E2E-GDS-007 | Auth gate — signed-in admin lacking `Gates.Manage` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-GDS-008 | Server 500 on `/currently-inside` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-GDS-009 | Server 500 on `/gates/list` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-GDS-010 | Loading state — "Loading…" placeholder shows while both calls are in flight | happy | P2 | _to author_ |
| E2E-GDS-011 | RTL / Arabic render mirrors the page, headings, columns and pills | i18n | P1 | _to author_ |
| E2E-GDS-012 | Regression (D-794) — `/currently-inside` returns 200 against the REAL database, not a stubbed one | regression | P0 | 2026-07-29 PASS |
| E2E-GDS-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-GDS-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-GDS-012 — Regression: the report is translatable (D-794)

> **Why this scenario exists.** E2E-GDS-008 already covered "the server returns
> 500 on `/currently-inside`" — as a *simulated* fault, with a stubbed response.
> Meanwhile the real endpoint returned 500 on **every** request, because its EF
> query could not be translated to SQL, and this dashboard had never worked.
> A resilience scenario that fakes a failure will never notice that the failure
> is permanent. This one calls the live endpoint and asserts success.

```gherkin
Feature: The currently-inside report can actually be produced
  As an Administrator with the Gates.Manage permission
  I want the dashboard's roster call to succeed against the real database
  So that the page shows occupancy instead of an error toast

Background:
  Given the API is reachable and backed by a REAL SQL Server database
  And an Administrator with the Gates.Manage permission has signed in

Scenario: The report succeeds on an empty scan log
  Given the GateScans table contains no rows at all
  When I GET /api/v1/admin/gates/reports/currently-inside
  Then the response status is 200
  And the ApiResult envelope reports Success = true
  And Data is an empty list
  # Not a data assertion by accident: the defect was at query-TRANSLATION time,
  # so it reproduced on an empty table. Seeding could never have masked it.

Scenario: A visitor whose latest allowed scan is a check-in appears
  Given a gate "GCI-1" and an approved visitor with a QR
  And the visitor has one allowed CheckIn scan 10 minutes ago
  When I GET /api/v1/admin/gates/reports/currently-inside
  Then the response status is 200
  And the visitor appears exactly once
  And their LastCheckInGateCode is "GCI-1"

Scenario: A later check-out removes the visitor
  Given the visitor has an allowed CheckIn 10 minutes ago
  And the visitor has an allowed CheckOut 5 minutes ago
  When I GET /api/v1/admin/gates/reports/currently-inside
  Then the visitor does not appear
  # Seed these rows directly. Posting two scans through the scan endpoint does
  # NOT work: GateOperatorService absorbs a repeat allowed scan inside a
  # 5-second DuplicateWindow (G-5), so the second call writes no row.

Scenario: A check-in older than the presence window is treated as departed
  Given the visitor has one allowed CheckIn 20 hours ago and no later scan
  When I GET /api/v1/admin/gates/reports/currently-inside
  Then the visitor does not appear
  # StalePresenceWindow is 16 hours: an in-only gate never emits a CheckOut.

Scenario: Two scans at the same instant still yield one row
  Given the visitor has two allowed CheckIn scans with an identical ScannedAt
  When I GET /api/v1/admin/gates/reports/currently-inside
  Then the visitor appears exactly once
```

**Automated by** `tests/SIMF.Api.Tests/AdminGateCurrentlyInsideTests.cs` (5 cases,
all passing 2026-07-29).

### E2E-GDS-001 — Golden path

```gherkin
Feature: Gates operations dashboard golden path
  As an Administrator with the Gates.Manage permission
  I want a live read-only overview of who is inside the venue and the gate roster
  So that I can monitor gate operations without leaving the Control Panel

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with the Gates.Manage permission has signed in via /login + /login/totp
  And they have landed on /admin/gates/dashboard

Scenario: Dashboard loads, both rosters and both stat cards render
  Given at least one gate "GATE-A" / "بوابة عامة" exists and is active
  And at least one visitor "Sara Al-Otaibi" is currently checked in at "GATE-A"
  When the page completes its first interactive render
  Then a GET /account/api/admin/gates/reports/currently-inside fires and returns 200
  And a POST /account/api/admin/gates/list with body {"Top":200} fires and returns 200
  And the SimfBanner title reads "Gates operations dashboard"
  And a "Currently inside" SimfStatCard shows the count of the inside roster
  And a "Gates" SimfStatCard shows the count of the gates roster
  And the "Currently inside" table shows a row with
      Name="Sara Al-Otaibi", Profile type (the profile-type name or "—"),
      Gate="GATE-A", Entered at formatted as "yyyy-MM-dd HH:mm:ss UTC"
  And the table summary reads "{N} currently inside"
  And the "Gates" table shows a row with Code="GATE-A", Name="GATE-A" and a green "Active" pill
  And the gates table summary reads "{M} gates"
  And no error SimfAlert is shown
```

**Evidence captured:**
- Screenshot before (loading): `docs/screenshots/cp-admin-gates-dashboard-loading.png`
- Screenshot after (both tables populated): `docs/screenshots/cp-admin-gates-dashboard-golden.png`
- Console errors: 0 expected
- Network: `GET /account/api/admin/gates/reports/currently-inside` returns 200 and `POST /account/api/admin/gates/list` returns 200
- Audit: none — this is a read-only dashboard (no `RowAudit` / `OperationLog` write).

### E2E-GDS-002 — Refresh re-fires both calls

```gherkin
Scenario: Refresh button reloads both rosters
  Given the dashboard has finished its initial load
  And the "Currently inside" stat card shows {N}
  When a new visitor checks in at the operator console in a separate session
  And the administrator clicks the "Refresh" button
  Then the button shows its loading label "Refreshing…" while the calls are in flight
  And a fresh GET /account/api/admin/gates/reports/currently-inside fires and returns 200
  And a fresh POST /account/api/admin/gates/list fires and returns 200
  And the "Currently inside" stat card now shows {N + 1}
  And the new visitor appears as a new row in the "Currently inside" table
  And no error SimfAlert is shown
```

**Evidence captured:**
- Screenshot after refresh: `docs/screenshots/cp-admin-gates-dashboard-refresh.png`
- Network: two new 200s (the second `/currently-inside` + `/gates/list` pair)
- Console errors: 0 expected

### E2E-GDS-003 — Stat cards agree with table counts

```gherkin
Scenario: Stat card values equal the rendered row counts
  Given the dashboard has finished loading
  When the administrator reads the two SimfStatCard values
  Then the "Currently inside" stat card value equals the number of <tr> rows in the inside table
  And the "Gates" stat card value equals the number of <tr> rows in the gates table
  And both counts are rendered with the invariant culture (no thousands separator drift)
```

### E2E-GDS-004 — Empty *Currently inside* roster

```gherkin
Scenario: No one inside renders the inside empty state
  Given the database has no visitor currently checked in (every check-in has a matching check-out)
  And at least one gate exists
  When the administrator opens /admin/gates/dashboard
  Then the "Currently inside" stat card shows "0"
  And under the "Currently inside" heading a SimfEmptyState renders
  And it shows the bilingual copy "No one is currently inside the venue." / "لا يوجد أحد داخل المعرض حاليًا."
  And the "Gates" table still renders its rows normally
  And no error SimfAlert is shown
```

### E2E-GDS-005 — Empty *Gates* roster

```gherkin
Scenario: No gates configured renders the gates empty state
  Given the database has no gate rows
  When the administrator opens /admin/gates/dashboard
  Then the "Gates" stat card shows "0"
  And under the "Gates" heading a SimfEmptyState renders
  And it shows the bilingual copy "No gates have been configured." / "لم يتم إعداد أي بوابات."
  And no error SimfAlert is shown
```

### E2E-GDS-006 — Active / Inactive pill rendering

```gherkin
Scenario: Gate active state maps to the correct SimfPill variant
  Given a gate "GATE-ON" exists with IsActive = true
  And a gate "GATE-OFF" exists with IsActive = false
  When the administrator opens /admin/gates/dashboard
  Then the "GATE-ON" row shows the SimfPill variant="on" reading "Active"
  And the "GATE-OFF" row shows the SimfPill variant="off" reading "Inactive"
```

### E2E-GDS-007 — Auth gate

```gherkin
Scenario: Admin without the Gates.Manage permission is denied
  Given a signed-in Control Panel user whose role does NOT include PermissionCatalog.Gates.Manage
  And who is NOT the Administrator wildcard ("*")
  When they navigate to /admin/gates/dashboard
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/gates/reports/currently-inside request fires
  And no /account/api/admin/gates/list request fires
```

### E2E-GDS-008 — Server 500 on `/currently-inside`

```gherkin
Scenario: API 500 on the inside report shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/gates/reports/currently-inside (e.g. DB down)
  And /admin/gates/list still returns 200
  When the administrator opens /admin/gates/dashboard
  Then the inside call envelope is not {Success:true, Data:not null}
  And a red SimfAlert appears reading "Could not load the gates dashboard." / "تعذّر تحميل لوحة البوابات."
  And (the unfilled inside roster shows its empty state)
```

### E2E-GDS-009 — Server 500 on `/gates/list`

```gherkin
Scenario: API 500 on the gates list shows the fallback bilingual toast
  Given /admin/gates/reports/currently-inside returns 200
  And the API is configured to return 500 on /admin/gates/list
  When the administrator opens /admin/gates/dashboard
  Then the gates-list call envelope is not {Success:true, Data:not null}
  And a red SimfAlert appears reading "Could not load the gates dashboard." / "تعذّر تحميل لوحة البوابات."
  And the inside roster still renders its rows from the successful call
```

### E2E-GDS-010 — Loading state

```gherkin
Scenario: Loading placeholder shows while both calls are in flight
  Given the two BFF calls are artificially delayed (throttled network)
  When the administrator opens /admin/gates/dashboard
  Then while _loading is true the page shows the "Loading…" / "جارٍ التحميل…" paragraph
  And neither stat card nor either table is rendered yet
  When both calls resolve
  Then the loading paragraph disappears and the stat cards + tables render
```

### E2E-GDS-011 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the dashboard
  Given the administrator is on /admin/gates/dashboard in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "لوحة عمليات البوابات"
  And the Refresh button reads "تحديث"
  And the stat cards read "داخل المعرض حاليًا" and "البوابات"
  And the "Currently inside" heading reads "الموجودون بالداخل حاليًا"
  And the inside table columns read "الاسم" / "نوع الملف" / "البوابة" / "وقت الدخول"
  And the gates table columns read "الرمز" / "الاسم" / "نشطة"
  And the active pill reads "نشطة" and the inactive pill reads "غير نشطة"
  And the Arabic display name (DisplayNameArabic / NameArabic) is used where present, else the English name
  And the layout mirrors right-to-left
```

---

## Implementation notes

- **API integration tests** at `tests/SIMF.Api.Tests/AdminGatesTests.cs` cover the
  two backing endpoints at a lower layer (no browser): the gates `POST /api/v1/admin/gates/list`
  round-trip (creates a gate, lists it, asserts it is present) and the standard
  CRUD/conflict cases (`GATE_CODE_DUPLICATE` 409, direction-mode update,
  deactivate). The `currently-inside` report is exercised indirectly via the
  scan-flow tests under `tests/SIMF.Api.Tests/GateScanTests.cs` and
  `GateVisitorsListTests.cs`. There is no dedicated browser-level test yet — this
  catalogue is the source of truth for the CP dashboard surface.
- **Permission gate.** The page and both endpoints share one permission,
  `PermissionCatalog.Gates.Manage`. `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`
  asserts the `Module.GatesDashboard` nav item carries `RequiredPermission =
  PermissionCatalog.Gates.Manage`, and `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs`
  asserts the admin gate endpoints reject a caller lacking it — those two cover
  E2E-GDS-007 at the unit/integration layer.
- **No mutation surface.** Because the page is read-only, there are no
  validation, conflict/duplicate, or write-audit scenarios to author here (the
  template's "validation failure" and "conflict / duplicate" rows do not apply);
  those live in the gate-CRUD catalogue for `/admin/gates`. The resilience
  scenarios (E2E-GDS-008 / -009) replace them.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin
  scenario into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be
  created) + step-definition class. The Gherkin shape is already runner-agnostic.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
