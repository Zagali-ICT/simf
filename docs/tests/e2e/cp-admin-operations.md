# E2E test catalogue — Operations toggles (`/admin/operations`)

| | |
|--|--|
| **Page** | [`cp/admin-operations.md`](../../pages/cp/admin-operations.md) |
| **Route** | `/admin/operations` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page shape (read from `OperationsToggles.razor`).** This is **not** a CRUD
> grid — it is two **singleton** toggle sections on one surface (D-166, gap
> doc G4; PDF §2.3 + §2.4). There is no list, no add/edit/delete, no pager,
> no empty state in the grid sense (the two rows are EF-seeded and the service
> self-heals a missing row). The page exposes exactly **five** interactive
> controls:
>
> 1. **Registration gate → "Registration is open"** checkbox (`_gateIsOpen`).
> 2. **Registration gate → "Auto-close (UTC)"** `datetime-local` field (`_gateAutoCloseInput`).
> 3. **Registration gate → "Save"** button → `SaveGateAsync` → `PUT /account/api/admin/registration-gate`.
> 4. **Archive visibility → "Archive is visible to the public"** checkbox (`_archiveIsVisible`).
> 5. **Archive visibility → "Save"** button → `SaveArchiveAsync` → `PUT /account/api/admin/archive/visibility`.
>
> Each section also renders a read-only **"Last changed"** `<dl>` (UTC string).
> The page is gated by `PermissionCatalog.Operations.View`; the two **PUT**
> endpoints require `PermissionCatalog.Operations.Edit` — so a View-only admin
> can open the page and read both states but cannot save (the BFF forwards the
> API 403). This split is the page's most important non-obvious behaviour and
> is exercised in E2E-OPS-005.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-OPS-001 | Golden round-trip — toggle registration gate closed → save → reload → reopen → save | happy | P0 | _to author_ |
| E2E-OPS-002 | Schedule auto-close — set "Auto-close (UTC)" → save → field round-trips | happy | P1 | _to author_ |
| E2E-OPS-003 | Toggle archive visibility — hide → save → public `GET /archive/visibility` reflects `IsVisible=false` | happy | P0 | _to author_ |
| E2E-OPS-004 | Auth gate — admin lacking `Operations.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-OPS-005 | Edit gate — View-only admin can load but Save is rejected (403 → fallback toast) | auth | P0 | _to author_ |
| E2E-OPS-006 | Validation — malformed "Auto-close (UTC)" → client bilingual error, no PUT fires | error | P1 | _to author_ |
| E2E-OPS-007 | Idempotent no-op — Save with no change writes no audit row + still shows success toast | edge | P2 | _to author_ |
| E2E-OPS-008 | Singleton self-heal — missing seed row → page loads a default (open / visible), no error | edge | P2 | _to author_ |
| E2E-OPS-009 | Server 500 on load — `GET registration-gate` 500 → bilingual load-failed toast | resilience | P2 | _to author_ |
| E2E-OPS-010 | Server 500 on save — `PUT registration-gate` 500 → bilingual save-failed toast, state unchanged | resilience | P2 | _to author_ |
| E2E-OPS-011 | RTL render — Arabic toggle mirrors page, headings, checkboxes, Save buttons | i18n | P1 | _to author_ |

## Scenarios

### E2E-OPS-001 — Golden round-trip (registration gate close → reopen)

```gherkin
Feature: Operations toggles — registration gate round-trip
  As an Administrator with Operations.Edit
  I want to close and reopen public sign-up
  So that I control whether new accounts can be created for the event

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator (Operations.View + Operations.Edit; superadmin is wildcard "*")
    has signed in via /login + /login/totp using the Get-Totp helper
  And they have landed on /admin/operations
  And both singleton sections have finished loading (no "Loading…" placeholder remains)

Scenario: Close registration, persist across reload, then reopen
  Given the "Registration gate" section shows the "Registration is open" checkbox TICKED
  And the "Last changed" value shows a UTC timestamp
  When the administrator UNTICKS "Registration is open"
  And clicks the "Save" button in the Registration gate section
  Then the BFF forwards PUT /account/api/admin/registration-gate
    with body { "isOpen": false, "autoCloseUtc": null }
  And the API returns HTTP 200 with ApiResult.Success = true
  And a green SimfAlert appears at the top of the surface reading
    "Registration gate updated." / "تم تحديث بوّابة التسجيل."
  And the "Last changed" timestamp advances to the current minute

  When the administrator reloads /admin/operations
  Then the "Registration is open" checkbox is UNTICKED (state persisted)

  And a closed gate is observable on sign-up: a public POST /api/v1/auth/sign-up
    returns HTTP 403 with ApiResult.Error.Code = "REGISTRATION_CLOSED"

  When the administrator TICKS "Registration is open" again
  And clicks "Save"
  Then the API returns HTTP 200
  And the green "Registration gate updated." toast appears
  And a subsequent public POST /api/v1/auth/sign-up returns HTTP 201 Created
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-operations-golden-before.png` (gate open)
- Screenshot after: `docs/screenshots/cp-admin-operations-golden-after.png` (gate closed + green toast)
- Console errors: 0 expected
- Network: every `/account/api/admin/registration-gate` call returns 200; the
  public sign-up probe returns 403 while closed and 201 after reopen
- Audit row: `OperationLog` / audit entry with `EventType = RegistrationGateUpdated`,
  `Outcome = Success`, the actor's user id, and `Detail = "isOpen=False; autoCloseUtc=null"`
  (then a second row for the reopen)

### E2E-OPS-002 — Schedule auto-close

```gherkin
Scenario: Set a future auto-close moment and confirm it round-trips
  Given the "Registration is open" checkbox is TICKED
  And the "Auto-close (UTC)" field is empty
  When the administrator types a future UTC datetime into "Auto-close (UTC)"
    (e.g. "2026-12-31T23:59")
  And clicks "Save" in the Registration gate section
  Then the BFF forwards PUT /account/api/admin/registration-gate
    with body { "isOpen": true, "autoCloseUtc": "2026-12-31T23:59:00+00:00" }
  And the API returns HTTP 200
  And the green "Registration gate updated." toast appears

  When the administrator reloads /admin/operations
  Then the "Auto-close (UTC)" field is pre-filled with "2026-12-31T23:59"
    (the page formats AutoCloseUtc as "yyyy-MM-ddTHH:mm")

  # Past auto-close behaviour (verified at the API layer, see OperationsTogglesTests):
  # an auto-close moment <= now makes the gate behave closed even when IsOpen=true
  And the value the page sent is interpreted as UTC (the input has no zone; the
    page wraps it via DateTime.SpecifyKind(..., Utc) before sending)
```

**Evidence captured:**
- Screenshot after: `docs/screenshots/cp-admin-operations-autoclose-after.png`
- Network: `PUT /account/api/admin/registration-gate` returns 200; body shows the
  UTC offset `+00:00`
- Audit row: `RegistrationGateUpdated` with `Detail` containing the ISO-8601
  `autoCloseUtc=2026-12-31T23:59:00.0000000+00:00`

### E2E-OPS-003 — Toggle archive visibility (public endpoint reflects it)

```gherkin
Scenario: Hide the past-events archive and confirm the public endpoint follows
  Given the "Archive visibility" section shows the
    "Archive is visible to the public" checkbox TICKED
  And the public GET http://localhost:5175/api/v1/archive/visibility
    (no auth header) returns ApiResult.Data.IsVisible = true
  When the administrator UNTICKS "Archive is visible to the public"
  And clicks the "Save" button in the Archive visibility section
  Then the BFF forwards PUT /account/api/admin/archive/visibility
    with body { "isVisible": false }
  And the API returns HTTP 200
  And a green SimfAlert reads "Archive visibility updated." / "تم تحديث إظهار الأرشيف."
  And the section's "Last changed" timestamp advances

  When an unauthenticated client calls GET /api/v1/archive/visibility again
  Then it returns HTTP 200 with ApiResult.Data.IsVisible = false (no auth needed)

  # Restore so later runs start from the visible state
  When the administrator TICKS the checkbox and clicks "Save"
  Then the public endpoint reports IsVisible = true again
```

**Evidence captured:**
- Screenshot before/after: `docs/screenshots/cp-admin-operations-archive-before.png`,
  `docs/screenshots/cp-admin-operations-archive-after.png`
- Network: `PUT /account/api/admin/archive/visibility` returns 200; the anonymous
  `GET /api/v1/archive/visibility` returns 200 and mirrors the saved value
- Audit row: `ArchiveVisibilityUpdated`, `Outcome = Success`, actor id,
  `Detail = "isVisible=False"`

### E2E-OPS-004 — Auth gate (no `Operations.View`)

```gherkin
Scenario: A signed-in admin lacking Operations.View is denied the page
  Given a signed-in Control Panel user whose role grants NO Operations.View
    permission (e.g. a Gate Operator with only Gates.* permissions)
  When they navigate to /admin/operations
  Then they land on /not-permitted with HTTP 200
  And the "Operations toggles" nav item is hidden for them
    (CpNavigation entry RequiredPermission = PermissionCatalog.Operations.View)
  And no /account/api/admin/registration-gate request fires
  And no /account/api/admin/archive/visibility request fires
```

### E2E-OPS-005 — Edit gate (View-only admin cannot Save)

```gherkin
Scenario: A View-only admin loads both sections but Save is rejected
  Given a signed-in admin whose role grants Operations.View but NOT Operations.Edit
  When they navigate to /admin/operations
  Then the page renders normally and BOTH sections load their current state
    (GET registration-gate and GET archive/visibility return 200)
  When they change "Registration is open" and click "Save"
  Then the BFF forwards PUT /account/api/admin/registration-gate
  And the API returns HTTP 403 (the PUT endpoint requires Operations.Edit)
  And the page shows a red SimfAlert with the fallback message
    "The change could not be saved." / "تعذّر حفظ التغيير."
    (or the server's bilingual MessageForCurrentCulture() if one is returned)
  And the persisted gate state is unchanged on reload
```

> **Grounding:** `RegistrationGateEndpoints.cs` / `ArchiveVisibilityEndpoints.cs`
> gate the GET with `Operations.View` and the PUT with `Operations.Edit`; the
> integration test `OperationsTogglesTests.Non_admin_caller_cannot_toggle_either_gate`
> asserts the PUT 403 for a non-admin. The wildcard `superadmin` holds both, so to
> exercise this row use a purpose-made role with `Operations.View` only.

### E2E-OPS-006 — Validation (malformed auto-close)

```gherkin
Scenario: A malformed Auto-close value is rejected client-side before any PUT
  Given the Registration gate section is loaded
  When the administrator enters a value that DateTime.TryParse cannot read
    into "Auto-close (UTC)" (e.g. via a forced non-date string)
  And clicks "Save" in the Registration gate section
  Then a red SimfAlert reads
    "Auto-close must be a valid UTC datetime." / "يجب أن يكون الإغلاق التلقائي وقتاً UTC صحيحاً."
  And NO PUT /account/api/admin/registration-gate request fires
  And the Save button returns from its Loading state
```

> Note: the field is a native `datetime-local` input so most malformed values are
> blocked by the browser; this scenario targets the explicit
> `DateTime.TryParse` guard + `AutoCloseInvalid` toast in `SaveGateAsync`.
> Leaving the field BLANK is valid (sends `autoCloseUtc: null`) and is covered by
> E2E-OPS-001 / E2E-OPS-003 — not a validation failure.

### E2E-OPS-007 — Idempotent no-op save

```gherkin
Scenario: Saving without changing anything is a no-op but still confirms
  Given both sections are loaded with their current values
  When the administrator clicks "Save" in the Registration gate section
    WITHOUT changing the checkbox or the auto-close field
  Then the API returns HTTP 200 and the green "Registration gate updated." toast appears
  But NO new audit row is written (the service only audits when
    IsOpen or AutoCloseUtc actually changed — see UpdateRegistrationGateAsync `changed` guard)
  And the "Last changed" timestamp does NOT advance
```

**Evidence captured:**
- Audit table query before/after the no-op save shows the same row count for
  `RegistrationGateUpdated`

### E2E-OPS-008 — Singleton self-heal (missing seed row)

```gherkin
Scenario: A missing operations singleton row self-heals to a safe default
  Given the RegistrationGate singleton row has been removed from the database
    (simulating lost seed data)
  When the administrator opens /admin/operations
  Then the page loads without an error toast
  And the "Registration is open" checkbox is TICKED (default open)
  And the "Last changed" timestamp shows the recreation moment
    (LoadRegistrationGateAsync inserts a default row when none exists)

  # The public IsRegistrationOpenAsync path also fails OPEN if the row is missing
  And a public POST /api/v1/auth/sign-up still succeeds (HTTP 201) while the
    gate row is absent
```

> Grounding: `OperationsToggleService.LoadRegistrationGateAsync` /
> `LoadArchiveVisibilityAsync` insert a default singleton when none is found, and
> `IsRegistrationOpenAsync` returns `true` ("fail-open") when the row is missing.
> This is why there is no classic "empty state" — the page can never show zero rows.

### E2E-OPS-009 — Server 500 on load

```gherkin
Scenario: A 500 while loading shows the bilingual load-failed toast
  Given the API is configured to return HTTP 500 on GET /admin/registration-gate
    (e.g. the database is down)
  When the administrator opens /admin/operations
  Then a red SimfAlert reads
    "The operations toggles could not be loaded." / "تعذّر تحميل مفاتيح التشغيل."
  And the toggle controls do not render (the section stays at "Loading…" / hidden
    because _gate / _archive remain null after the catch)
```

> Grounding: `LoadAsync` wraps both GETs in a single try/catch and sets
> `Admin.Operations.LoadFailed` on any exception.

### E2E-OPS-010 — Server 500 on save

```gherkin
Scenario: A 500 while saving shows the bilingual save-failed toast and leaves state intact
  Given the Registration gate section is loaded with "Registration is open" TICKED
  And the API is configured to return HTTP 500 on PUT /admin/registration-gate
  When the administrator UNTICKS the checkbox and clicks "Save"
  Then the BFF forwards the PUT and the response is not Success
  And a red SimfAlert reads "The change could not be saved." / "تعذّر حفظ التغيير."
    (or the server's MessageForCurrentCulture() when present)
  And the Save button leaves its Loading state (the finally block clears _busy)
  And reloading /admin/operations shows the gate still OPEN (no partial write)
```

### E2E-OPS-011 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the whole page
  Given the administrator is on /admin/operations in English
  When they switch the UI to Arabic (العربية) from the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "مفاتيح التشغيل"
  And the Registration gate heading reads "بوّابة التسجيل"
  And the gate checkbox label reads "التسجيل مفتوح"
  And the "Auto-close (UTC)" label reads "الإغلاق التلقائي (UTC)"
  And the Archive visibility heading reads "إظهار الأرشيف"
  And the archive checkbox label reads "الأرشيف ظاهر للعموم"
  And both "Save" buttons read "حفظ"
  And the nav rail mirrors with the "مفاتيح التشغيل" item
  And the checkboxes + form actions appear right-aligned (RTL flow)

  When the administrator saves a section in Arabic
  Then the success toast reads "تم تحديث بوّابة التسجيل." (gate)
    or "تم تحديث إظهار الأرشيف." (archive)
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-operations-rtl.png`
- Console errors: 0 expected

---

## Implementation notes

- **API integration tests cover this surface at a lower layer** —
  [`tests/SIMF.Api.Tests/OperationsTogglesTests.cs`](../../../tests/SIMF.Api.Tests/OperationsTogglesTests.cs)
  asserts: admin GET returns the seeded open state; closing the gate (and a
  past auto-close) makes `POST /auth/sign-up` return `403 REGISTRATION_CLOSED`;
  sign-up succeeds when open; the public `GET /archive/visibility` needs no auth;
  admin toggling archive visibility is reflected on the public endpoint; and a
  non-admin caller gets `403` on both PUTs (the `Operations.Edit` gate). The
  related public archive read is also touched by
  [`tests/SIMF.Api.Tests/ArchiveTests.cs`](../../../tests/SIMF.Api.Tests/ArchiveTests.cs).
  The `// Tests:` headers on the endpoint/service files still name the legacy
  filenames `RegistrationGateTests.cs` / `ArchiveVisibilityTests.cs`, but the
  live tests are consolidated in `OperationsTogglesTests.cs`.
- **No CRUD / no empty state.** Treat E2E-OPS-008 as the "empty" equivalent: the
  two rows are singletons (`RegistrationGate.SingletonId` /
  `ArchiveVisibility.SingletonId`), EF-seeded, and self-healed by the service —
  the page never renders a list or an add/delete affordance.
- **Two-permission page.** `Operations.View` gates the page + the GETs;
  `Operations.Edit` gates the two PUTs. Both are `AdminOnly` baseline in
  `PermissionCatalog.All`. The most valuable non-happy assertions are the auth
  pair E2E-OPS-004 (no View → `/not-permitted`) and E2E-OPS-005 (View-only →
  Save 403).
- **Convert to Playwright** when the runner is adopted: each Gherkin block maps to
  a `.feature` scenario under `tests/SIMF.E2E.Tests/` with a step-definition class;
  the steps are already runner-agnostic.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
