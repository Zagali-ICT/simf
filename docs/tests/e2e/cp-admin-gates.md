# E2E test catalogue — Gates CRUD (`/admin/gates`)

| | |
|--|--|
| **Page** | [`cp/admin-gates.md`](../../pages/cp/admin-gates.md) _(reference doc not yet authored)_ |
| **Route** | `/admin/gates` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page facts (read from source, do not invent):**
> - Page: `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/GatesList.razor`
>   (D-148 — Gate Module CRUD list, mirrors `HallsList`).
> - Form: `GateForm.razor` (shared Add/Edit; the **Active** checkbox only renders in Edit).
> - Required permission: **`Gates.Manage`** (`@attribute [RequirePermission(PermissionCatalog.Gates.Manage)]`).
> - Nav item: `Module.Gates` → `/admin/gates`, `RequiredPermission = PermissionCatalog.Gates.Manage` (`CpNavigation.cs`).
> - BFF passthroughs (`AccountEndpoints.cs`): `POST /account/api/admin/gates/list`,
>   `GET /account/api/admin/gates/{id}`, `POST /account/api/admin/gates`,
>   `PUT /account/api/admin/gates/{id}`, `DELETE /account/api/admin/gates/{id}`.
>   The form also primes its pickers from `POST /account/api/admin/profile-types/list`
>   and `POST /account/api/admin/admins/list`.
> - API endpoints (`src/Backend/SIMF.Api/Endpoints/Admin/GateEndpoints.cs`), all gated by
>   `Gates.Manage` + `RequireApprovedAccount`; create/update/delete are rate-limited (`"auth"` limiter).
> - Grid columns: **Code, Name, Name (Arabic), Direction, Allowed types** (`0 → "All"`),
>   **Operators** (active-assignment count), **Status** (Active/Inactive pill).
> - Form fields: **Code** (2–16, uppercased+unique), **Name (English)** (1–128),
>   **Name (Arabic)** (1–128), **Description (English)** (≤1024, optional),
>   **Description (Arabic)** (≤1024, optional), **Direction policy** (In / Out / Both),
>   **Allowed profile types** (multi-select; empty = all), **Assigned operators** (multi-select),
>   **Active** (Edit only).
> - Error codes (`ErrorCodes.cs` / `AdminGateService.cs`): `GATE_INVALID` (400),
>   `GATE_NOT_FOUND` (404), `GATE_CODE_DUPLICATE` (409), `GATE_PROFILE_TYPE_INVALID` (400),
>   `GATE_ASSIGNMENT_INVALID` (400).
> - Audit events (`AuditEvents.cs`): `Gate.Created`, `Gate.Updated`, `Gate.Deactivated`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-GAT-001 | Full CRUD round-trip — Add → Edit → Details → Deactivate | happy | P0 | _to author_ |
| E2E-GAT-002 | Add a restricted gate with allowed profile types + assigned operators | happy | P1 | _to author_ |
| E2E-GAT-003 | Direction policy column reflects In / Out / Both selection | happy | P2 | _to author_ |
| E2E-GAT-004 | Search / filter the grid by code or name | happy | P2 | _to author_ |
| E2E-GAT-005 | Sort grid by Code / Name / Direction column | happy | P2 | _to author_ |
| E2E-GAT-006 | Paging — Next / Prev / First / Last + page size | happy | P2 | _to author_ |
| E2E-GAT-007 | Details modal renders all fields read-only and Close dismisses it | happy | P2 | _to author_ |
| E2E-GAT-008 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-GAT-009 | Auth gate — signed-in admin lacking `Gates.Manage` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-GAT-010 | Client validation — short code / blank names blocked in the modal | error | P1 | _to author_ |
| E2E-GAT-011 | Server validation — `GATE_INVALID` (400) surfaced bilingually | error | P1 | _to author_ |
| E2E-GAT-012 | Duplicate code — `GATE_CODE_DUPLICATE` (409) surfaced bilingually | error | P1 | _to author_ |
| E2E-GAT-013 | Server 500 / list failure → bilingual load-failed toast | resilience | P2 | _to author_ |
| E2E-GAT-014 | RTL / Arabic render mirrors page + modal | i18n | P1 | _to author_ |

## Scenarios

### E2E-GAT-001 — Full CRUD round-trip

```gherkin
Feature: Gates CRUD round-trip
  As an Administrator with Gates.Manage
  I want to create, edit, view and deactivate a gate
  So that the venue access-control gates stay accurate for the event

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with the Gates.Manage permission has signed in
    via /login + /login/totp using superadmin@zagali-ict.com and a Get-Totp code
  And they have landed on /admin/gates

Scenario: Create, edit, view, deactivate one gate
  Given the grid currently shows {N} rows
  When the administrator clicks "Add gate"
  Then the Add modal opens titled "Add gate"
  And it shows the fields: Code, Name (English), Name (Arabic),
    Description (English), Description (Arabic), Direction policy,
    Allowed profile types, Assigned operators
  And the "Active" checkbox is NOT shown (Add mode)
  And the Direction policy defaults to "Both (inferred direction)"
  When they fill Code="G-MAIN-1"
  And they fill Name (English)="Main Entrance"
  And they fill Name (Arabic)="المدخل الرئيسي"
  And they leave Allowed profile types empty
  And they click "Create gate"
  Then the BFF forwards POST /account/api/admin/gates and the API returns 200
  And the modal closes
  And the grid shows {N + 1} rows
  And a green toast reads 'Gate "Main Entrance" was created.'
  And a row exists with Code="G-MAIN-1", Name="Main Entrance",
    Direction="Both", Allowed types="All", Operators="0",
    and the green "Active" pill

  When the administrator clicks the "Edit" icon on that row
  Then a GET /account/api/admin/gates/{id} fires
  And the Edit modal opens titled "Edit gate" with the row's values pre-filled
  And the "Active" checkbox is visible and ticked
  When they change Name (English) to "Main Gate" and Direction policy to "In (check-in only)"
  And they click "Save changes"
  Then the BFF forwards PUT /account/api/admin/gates/{id} and the API returns 200
  And the modal closes
  And a green toast reads 'Gate "Main Gate" was updated.'
  And the row's Name reads "Main Gate" and Direction reads "In"

  When the administrator clicks the "Details" icon on that row
  Then a read-only modal titled "Gate details" opens
  And it renders Code, Name, Name (Arabic), Direction, Allowed types,
    Operators and Status in a description list
  When they click "Close"
  Then the modal closes

  When the administrator clicks the "Deactivate" icon on that row
  Then a DELETE /account/api/admin/gates/{id} fires and the API returns 200
  And a green toast reads 'Gate "Main Gate" was deactivated.'
  And the row's Status pill changes to the grey "Inactive" pill
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-gates-crud-before.png`
- Screenshot add modal: `docs/screenshots/cp-admin-gates-add-modal.png`
- Screenshot edit modal: `docs/screenshots/cp-admin-gates-edit-modal.png`
- Screenshot details modal: `docs/screenshots/cp-admin-gates-details-modal.png`
- Screenshot after deactivate: `docs/screenshots/cp-admin-gates-crud-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/gates/*` call returns 200
- Audit rows: `Gate.Created`, then `Gate.Updated`, then `Gate.Deactivated`
  (`AuditEvents`) each with the actor's id and `Detail` carrying `code=G-MAIN-1`.

### E2E-GAT-002 — Add a restricted gate with allowed types + operators

```gherkin
Scenario: Create a gate restricted to chosen profile types and assigned operators
  Given at least one active profile type and one active admin exist
  And the administrator opens the "Add gate" modal
  Then the "Allowed profile types" list is primed from
    POST /account/api/admin/profile-types/list (isActive=true)
  And the "Assigned operators" list is primed from
    POST /account/api/admin/admins/list (shows "{email} — {display name}")
  When they fill Code="VIP-1", Name (English)="VIP Lounge", Name (Arabic)="صالة كبار الزوار"
  And they select one or more profile types in "Allowed profile types"
  And they select one operator in "Assigned operators"
  And they click "Create gate"
  Then the API returns 200
  And a green toast reads 'Gate "VIP Lounge" was created.'
  And the new row shows the count of selected types under "Allowed types" (not "All")
  And the "Operators" column shows the count of assigned operators
```

**Evidence captured:**
- Screenshot of the restricted-gate row → `docs/screenshots/cp-admin-gates-restricted-row.png`
- Network: `profile-types/list` + `admins/list` + `POST gates` all return 200
- Audit row: `Gate.Created` with `Detail` carrying `code=VIP-1`.

### E2E-GAT-003 — Direction policy reflected in the grid

```gherkin
Scenario Outline: The Direction column reflects the chosen policy
  Given the administrator opens the "Add gate" modal
  When they fill Code="<code>" + Name (English)="<name>" + Name (Arabic)="<ar>"
  And they pick Direction policy "<choice>"
  And they click "Create gate"
  Then the new row's Direction column reads "<column>"

Examples:
  | code  | name        | ar          | choice                    | column |
  | DIR-IN  | In Gate   | بوابة دخول   | In (check-in only)        | In     |
  | DIR-OUT | Out Gate  | بوابة خروج   | Out (check-out only)      | Out    |
  | DIR-BO  | Both Gate | بوابة مزدوجة | Both (inferred direction) | Both   |
```

### E2E-GAT-004 — Search / filter the grid

```gherkin
Scenario: Filtering by code or name narrows the grid
  Given gates "G-MAIN-1" (Main Gate) and "VIP-1" (VIP Lounge) exist
  When the administrator types "VIP" into the grid search box
  Then a POST /account/api/admin/gates/list fires with Search="VIP"
  And the grid shows only the "VIP-1" row
  When they clear the search box
  Then the grid shows all rows again (server matches Code, Name and Name (Arabic), case-insensitive LIKE)
```

### E2E-GAT-005 — Sort the grid

```gherkin
Scenario: Sorting toggles ascending / descending on a sortable column
  Given the grid shows several gates
  When the administrator clicks the "Code" column header
  Then a POST /account/api/admin/gates/list fires with Sort="code", SortDescending=false
  And the rows render in ascending Code order
  When they click the "Code" header again
  Then the request flips to SortDescending=true and rows render in descending order
  And the same toggle works for the "Name" and "Direction" headers
```

### E2E-GAT-006 — Paging

```gherkin
Scenario: Pager controls move through pages and change page size
  Given more than one page of gates exist (page size 20)
  Then the summary reads "Showing 1–20 of {total}"
  When the administrator clicks "Next"
  Then the grid loads the next page and the summary updates
  When they click "Previous" / "First page" / "Last page"
  Then the grid loads the matching page each time
  When they change the "Show" page size
  Then a new /list request fires with the new Top and the grid re-renders
```

### E2E-GAT-007 — Details modal read-only

```gherkin
Scenario: Details modal is read-only and Close dismisses it
  Given a gate row exists
  When the administrator clicks the "Details" icon on it
  Then a GET /account/api/admin/gates/{id} fires
  And a modal titled "Gate details" opens with a description list of
    Code, Name, Name (Arabic), Direction, Allowed types (or "All"),
    Operators count and Status (Active / Inactive)
  And the modal has no editable inputs — only a "Close" button
  When they click "Close"
  Then the modal closes and the grid is unchanged
```

### E2E-GAT-008 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no Gate rows (or all are filtered out)
  When the administrator opens /admin/gates
  Then the grid body renders the SimfEmptyState component titled "No gates yet."
    (Arabic: "لا توجد بوابات بعد.")
  And no error toast appears
  And the toolbar still shows the "Add gate" button
```

### E2E-GAT-009 — Auth gate

```gherkin
Scenario: Signed-in admin lacking Gates.Manage is denied
  Given a signed-in Control Panel user whose roles do NOT grant Gates.Manage
    (and who is not the wildcard Administrator)
  When they navigate to /admin/gates
  Then the [RequirePermission(PermissionCatalog.Gates.Manage)] gate redirects
    them to /not-permitted with HTTP 200
  And no POST /account/api/admin/gates/list request fires
  And the "Gates" nav item is hidden for them (RequiredPermission = Gates.Manage)
```

### E2E-GAT-010 — Client-side validation

```gherkin
Scenario: Short code and blank names are blocked before any POST
  Given the "Add gate" modal is open
  When the administrator fills Code="G" (1 character) and clicks "Create gate"
  Then a SimfAlert error appears in the modal reading
    "Code must be between 2 and 16 characters." (Arabic: "يجب أن يتراوح الرمز بين 2 و 16 حرفاً.")
  And no POST /account/api/admin/gates request fires

  When they fix Code="G-OK" but leave Name (English) blank and click "Create gate"
  Then the alert reads "English name is required (1–128 characters)."
    (Arabic: "الاسم الإنجليزي مطلوب (1–128 حرفاً).")
  When they fill Name (English) but leave Name (Arabic) blank and click "Create gate"
  Then the alert reads "Arabic name is required (1–128 characters)."
  And the modal stays open throughout and no POST fires
```

### E2E-GAT-011 — Server-side validation (GATE_INVALID)

```gherkin
Scenario: Server rejects an invalid gate with GATE_INVALID 400 surfaced bilingually
  Given a payload that passes the client guard but fails the server guard
    (e.g. a description over 1024 characters, or a code the server rejects)
  When the administrator submits the "Add gate" modal
  Then the BFF forwards POST /account/api/admin/gates
  And the API returns HTTP 400 with ApiResult.Error.Code = "GATE_INVALID"
  And the modal stays open
  And the SimfAlert surfaces Error.MessageForCurrentCulture()
    (English e.g. "Gate code must be between 2 and 16 characters.";
     Arabic e.g. "يجب أن يتراوح رمز البوابة بين 2 و 16 حرفاً.")
```

### E2E-GAT-012 — Duplicate code (GATE_CODE_DUPLICATE)

```gherkin
Scenario: Duplicate code returns 409 with the bilingual server message
  Given a gate with Code="G-MAIN-1" already exists
  When the administrator opens the "Add gate" modal
  And fills Code="g-main-1" (the server upper-cases to "G-MAIN-1")
    + Name (English)="Duplicate Gate" + Name (Arabic)="بوابة مكررة"
  And clicks "Create gate"
  Then the BFF forwards POST /account/api/admin/gates
  And the API returns HTTP 409 with ApiResult.Error.Code = "GATE_CODE_DUPLICATE"
  And the modal stays open
  And the SimfAlert surfaces MessageForCurrentCulture()
    (English: "A gate with code 'G-MAIN-1' already exists.";
     Arabic: "توجد بوابة بالرمز 'G-MAIN-1' بالفعل.")
  And the same 409 path is exercised on Edit when changing a code to a value
    already held by another gate
```

### E2E-GAT-013 — Server 500 / list failure

```gherkin
Scenario: API failure on /list shows the bilingual load-failed toast
  Given the API is configured to fail /admin/gates/list (e.g. DB down → 500,
    or the envelope returns Success=false)
  When the administrator opens /admin/gates
  Then the grid shows the loading indicator first
  And then a red SimfAlert toast appears reading "The gates could not be loaded."
    (Arabic: "تعذّر تحميل البوابات.")
  And no rows render
```

### E2E-GAT-014 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/gates in English
  When they switch the UI to Arabic from the header language switch
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "البوابات"
  And the grid column headers and action labels render in Arabic
  And the toolbar buttons and pager arrows mirror

  When they click "إضافة بوابة" (Add gate)
  Then the Add modal opens in RTL with Arabic field labels
    (Direction options read In / Out / Both in Arabic, "Both (inferred direction)" first)
  And the form actions appear in reverse order
```

---

## Implementation notes

- **Manual smoke as canonical-source-of-truth today.** Until a Playwright project
  exists, the canonical "run" of these scenarios is a Chrome DevTools MCP session:
  sign in via the Background steps, walk each scenario, capture screenshots into
  `docs/screenshots/cp-admin-gates-*.png`.
- **Convert to Playwright** when the runner is adopted: each Gherkin block maps to a
  `.feature` scenario under `tests/SIMF.E2E.Tests/` (project to be created) plus a
  step-definition class. The steps are already runner-agnostic.
- **API integration tests** at `tests/SIMF.Api.Tests/AdminGatesTests.cs` cover the
  same admin CRUD surface at a lower layer (no browser) — create / duplicate-code /
  validation / not-found / deactivate / assignments. Related lower-layer suites:
  `GateScanTests.cs`, `GateVisitorsListTests.cs`, `GateFailureCircuitTests.cs`
  (operator-scan + reports paths, not driven by this page). When an E2E scenario
  reliably covers a case, the matching `Api.Tests` case can be trimmed during the
  transition — keep both until then.
- **Permission gate** is enforced in two places that the build guards:
  `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs` (nav item carries
  `Gates.Manage`) and `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` (every
  gate endpoint is policy-gated). E2E-GAT-009 is the browser-level proof of the same.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
