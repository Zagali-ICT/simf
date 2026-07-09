# E2E test catalogue — Others CRUD (`/admin/others`)

| | |
|--|--|
| **Page** | [`cp/admin-others.md`](../../pages/cp/admin-others.md) |
| **Route** | `/admin/others` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (canonical SIMF browser smoke). Convertible to Playwright later — keep scenario steps tool-agnostic. |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Required permission** | `PermissionCatalog.Others.View` (`Others.View`) on the page; row/action endpoints additionally gated by `Others.Create` / `Others.Edit` / `Others.Delete` / `Others.Export` / `Others.Import` / `Others.RegisterOnsite` |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **What this page is.** Type-scoped management grid for **Other-typed accounts**
> (exhibitor reps, sponsor staff, press, contractors — every non-visitor,
> non-admin attendee). It is the sibling of `/admin/visitors`: same `SimfDataGrid`
> toolbar, same modals, the only structural differences are that the walk-in
> wizard runs with `Kind="Other"` (no Interests section) and the profile-type
> pool comes from `/account/api/admin/profile-types?userType=Other`. All grid
> traffic goes through the CP BFF under `/account/api/admin/others/*`, which
> forwards to the API `/api/v1/admin/others/*` D-113 routes with the admin token.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-OTH-001 | Golden round-trip — walk-in Add (Other) → Details → Edit → row Delete | happy | P0 | _to author_ |
| E2E-OTH-002 | Walk-in register-onsite mints approved account + QR badge | happy | P0 | _to author_ |
| E2E-OTH-003 | List loads + paging/sort/filter on the grid | happy | P1 | _to author_ |
| E2E-OTH-004 | Details modal renders full profile + inline ID-document image | happy | P1 | _to author_ |
| E2E-OTH-005 | Edit modal saves email + display name + profile type | happy | P1 | _to author_ |
| E2E-OTH-006 | Duplicate one row to a new email | happy | P1 | _to author_ |
| E2E-OTH-007 | Bulk-delete selected rows with audited reason | happy | P1 | _to author_ |
| E2E-OTH-008 | Single-row Delete reuses the reason modal | happy | P2 | _to author_ |
| E2E-OTH-009 | Export to Excel downloads `simf-others-*.xlsx` | happy | P1 | _to author_ |
| E2E-OTH-010 | Import from Excel returns created/skipped result | happy | P1 | _to author_ |
| E2E-OTH-011 | Copy (one + selected) and Paste toasts | happy | P2 | _to author_ |
| E2E-OTH-012 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-OTH-013 | Auth gate: signed-in admin lacking `Others.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-OTH-014 | Walk-in validation: missing profile type / names / ID → inline error | error | P1 | _to author_ |
| E2E-OTH-015 | Bulk-delete reason guard: < 10 or > 500 chars keeps Delete disabled | error | P2 | _to author_ |
| E2E-OTH-016 | Duplicate conflict: existing email → 409 `ADMIN_EMAIL_ALREADY_REGISTERED` | error | P1 | _to author_ |
| E2E-OTH-017 | Cross-kind guard: a Visitor id on `/admin/others/{id}/profile` → 404 `NOT_FOUND` | error | P1 | _to author_ |
| E2E-OTH-018 | Cross-kind ProfileTypeId in walk-in → 400 `ADMIN_PROFILE_TYPE_INVALID` | error | P1 | _to author_ |
| E2E-OTH-019 | No Other profile-type seeded → walk-in wizard shows the seed prompt | error | P2 | _to author_ |
| E2E-OTH-020 | Server 500 on `/list` → empty page renders, no rows, no crash | resilience | P2 | _to author_ |
| E2E-OTH-021 | RTL/Arabic render mirrors page + walk-in wizard + modals | i18n | P1 | _to author_ |
| E2E-OTH-022 | Organisation required + digit-only IDs on the Others walk-in (D-354) | error | P1 | _to author_ |
| E2E-OTH-023 | Presentation toggle: switch to full-page + persists across reload (D-353) | happy | P1 | _to author_ |
| E2E-OTH-024 | Full-page mode: Add (walk-in) / Edit / Details take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |

## Scenarios

### E2E-OTH-001 — Golden round-trip (Add → Details → Edit → Delete)

```gherkin
Feature: Others management round-trip
  As an Administrator
  I want to register, inspect, edit and remove an Other-typed account
  So that exhibitor / press / contractor records stay accurate before the event

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp using
      "superadmin@zagali-ict.com" and a fresh code from the Get-Totp helper
  And at least one Other profile-type is seeded (via /admin/profile-types/other)
  And they have landed on /admin/others
  And the grid title reads "Others" and a POST /account/api/admin/others/list returned 200

Scenario: Register a walk-in Other, view, edit, then delete it
  Given the grid currently shows {N} rows
  When the administrator clicks "Add" on the toolbar
  Then the "Add Other user" modal opens hosting the walk-in wizard
  And the wizard has NO "Interests" section (Other kind)
  When they pick a profile-type tile
  And they fill Badge name (DisplayName)="Khalid Press"
  And they fill English name="Khalid Al-Sahafi"
  And they fill Arabic name="خالد الصحفي"
  And they keep "Saudi = Yes" and fill National ID="1098765432"
  And they fill Saudi mobile="0551234567"
  And they fill Email="khalid.press@example.com"
  And they click "Register"
  Then POST /account/api/admin/others/register-onsite returns 200
  And the "Registered — badge ready" success modal appears with a QR badge
  When they click "Done"
  Then the modal closes and a green toast reads
      "Account created for khalid.press@example.com. The invitation email has been queued."
  And the grid reloads and shows {N + 1} rows
  And a row exists with Email="khalid.press@example.com" and the Approved state

  When the administrator clicks the "Details" icon on that row
  Then GET /account/api/admin/others/{id}/profile returns 200
  And a read-only modal "Other user details — khalid.press@example.com" lists
      Email, Display name, User type, State, Profile type, Created, names,
      nationality, identity type/number and mobile
  When they click "Close"
  Then the modal closes

  When the administrator clicks the "Edit" icon on that row
  Then the "Edit Other user" modal opens pre-filled with email, display name and profile type
  When they change Display name to "Khalid Media"
  And they click "Save"
  Then PUT /account/api/admin/others/{id} returns 200
  And the modal closes and a green toast reads "The account was updated."

  When the administrator clicks the "Delete" icon on that row
  Then the "Delete Other accounts" modal opens with the count confirmation
  When they type Reason="Test fixture cleanup after E2E round-trip" (>= 10 chars)
  And they click "Delete"
  Then POST /account/api/admin/others/bulk-delete returns 200
  And a green toast reads "1 deleted, 0 skipped."
  And the grid reloads with {N} rows again
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-others-golden-before.png`
- Screenshot after each modal: `docs/screenshots/cp-admin-others-{add-wizard,success-qr,details-modal,edit-modal,delete-modal}.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/others/*` call returns 200 (list, register-onsite,
  profile, PUT, bulk-delete); the post-register id-document upload is fire-and-forget
- Audit rows: `RowAudit` Insert (create) + Update (edit) + soft-delete Update with the
  actor id; `OperationLog` walk-in register + bulk-delete entries

### E2E-OTH-002 — Walk-in register-onsite mints approved account + QR

```gherkin
Scenario: Non-Saudi walk-in with passport mints an approved badge
  Given the Add modal is open on the walk-in wizard
  When the administrator picks a profile-type tile
  And fills Badge name="Maria Vendor", English name="Maria Rossi", Arabic name="ماريا روسي"
  And toggles "Saudi = No"
  And selects Nationality="IT" from the country picker
  And keeps the ID kind toggle on "Passport" and fills Passport number="YA1234567"
  And fills International mobile="+393331234567"
  And optionally attaches an ID image (image/png|jpeg|webp) via the file input
  And clicks "Register"
  Then POST /account/api/admin/others/register-onsite returns 200
  And the response carries UserId + Email + the QR badge
  And the "Registered — badge ready" modal shows the QR as the access key
  And if an ID image was attached, a follow-up POST
      /account/api/admin/others/{id}/id-document fires (fire-and-forget)
  When they click "Register another"
  Then the wizard resets but keeps the selected profile-type tile and Saudi/nationality choice
```

### E2E-OTH-003 — List loads + paging / sort / filter

```gherkin
Scenario: Grid paging, sorting and column filtering hit the list endpoint
  Given the grid has more than one page of Other accounts (page size 20)
  When the administrator clicks the "Next" / "Last" pager controls
  Then a POST /account/api/admin/others/list fires with the updated Skip/Top
  And the pager summary updates (e.g. "Showing 21–40 of 57")
  When they sort the "Email" column header
  Then a list request fires with the email sort and the rows reorder
  When they type "press" into the column filter on "Email"
  Then a list request fires with the filter and only matching rows remain
```

### E2E-OTH-004 — Details modal renders full profile + ID image

```gherkin
Scenario: Details modal streams the encrypted ID-document image inline
  Given an Other account that has an uploaded ID image (HasIdImage = true)
  When the administrator clicks the "Details" icon on that row
  Then GET /account/api/admin/others/{id}/profile returns 200
  And the description list renders identity type ("National ID" / "Iqama" / "Passport")
      derived from whichever number is present
  And an <img> loads from /account/api/admin/others/{id}/id-document?v={ticks}
  And that image request returns 200 with Cache-Control "private, max-age=60"
  And when the account has a profile photo (HasAvatar = true) a "Profile photo" block
      renders an <img> from /account/api/admin/others/{id}/avatar?v={ticks} (D-727, owner item 5)
  When the profile read fails (envelope.Success = false)
  Then a SimfAlert error shows the bilingual fallback instead of the description list
```

### E2E-OTH-005 — Edit modal saves the account

```gherkin
Scenario: Edit changes email, display name and profile type
  Given the Edit modal is open for an existing Other account
  And the profile-type dropdown lists only active Other-side tiers (IsVisitor = false)
  When the administrator changes Email to "renamed.other@example.com"
  And selects a different profile type
  And clicks "Save"
  Then PUT /account/api/admin/others/{id} returns 200
  And a green toast reads "The account was updated."
  And the hint warns that changing the email signs the account out
  And the "Save" button is disabled while Display name is under 2 chars or email is blank
```

### E2E-OTH-006 — Duplicate one row to a new email

```gherkin
Scenario: Duplicate copies an Other account under a new email
  Given the grid shows a row for "khalid.press@example.com"
  When the administrator clicks the "Duplicate" icon on that row
  Then the "Duplicate Other user" modal opens with the source email in the prompt
  And the "Duplicate" button stays disabled until the New email looks like an email
  When they fill New email="khalid.copy@example.com"
  And click "Duplicate"
  Then POST /account/api/admin/others/duplicate returns 200
  And a green toast reads "Created khalid.copy@example.com."
  And the grid reloads with the new row
```

### E2E-OTH-007 — Bulk-delete selected rows with audited reason

```gherkin
Scenario: Bulk-delete multiple selected rows with a reason
  Given the administrator ticks the checkboxes on two Other rows
  When they click "Delete" on the toolbar
  Then the "Delete Other accounts" modal opens reading
      "This will disable 2 Other account(s). Sessions are revoked and the users are notified by email."
  When they type Reason="Removed at exhibitor request before the show" (>= 10 chars)
  And click "Delete"
  Then POST /account/api/admin/others/bulk-delete returns 200
  And a green toast reads "2 deleted, 0 skipped."
  And the deleted rows drop out on reload

Scenario: Bulk-delete with an empty selection is rejected client-side
  Given no rows are selected
  When the administrator clicks "Delete" on the toolbar
  Then a red toast reads "Select at least one row first."
  And no bulk-delete modal opens and no request fires
```

### E2E-OTH-008 — Single-row Delete reuses the reason modal

```gherkin
Scenario: Per-row Delete opens the same reason modal with one target
  When the administrator clicks the "Delete" icon on a single row
  Then the "Delete Other accounts" modal opens for that one account
  When they enter a >= 10 char reason and click "Delete"
  Then POST /account/api/admin/others/bulk-delete returns 200 with that single id
  And a green toast reads "1 deleted, 0 skipped."
```

### E2E-OTH-009 — Export to Excel

```gherkin
Scenario: Export downloads the Others workbook
  When the administrator clicks "Export to Excel" on the toolbar with no selection
  Then POST /account/api/admin/others/export fires with the current Query
  And the browser downloads a file named "simf-others-{yyyyMMddHHmmss}.xlsx"
      of content-type application/vnd.openxmlformats-officedocument.spreadsheetml.sheet

Scenario: Export of a selection sends ids only
  Given two rows are selected
  When the administrator clicks "Export to Excel"
  Then the export request carries the selected Ids and Query is null
```

### E2E-OTH-010 — Import from Excel

```gherkin
Scenario: Import an .xlsx of Other accounts
  When the administrator clicks "Import from Excel" on the toolbar
  Then the hidden file input (#others-import-input, accept=".xlsx") is triggered
  When they choose a valid .xlsx of Other rows
  Then POST /account/api/admin/others/import returns 200
  And the "Import result" modal reads "{created} created, {skipped} skipped."
  And any per-row errors list as "Row {n} ({email}): {reason}"
  And the grid reloads

Scenario: Import with no file selected returns the empty-file error
  Given the import upload carries no file
  Then the BFF returns 400 with ApiResult.Error.Code = "AdminImportEmpty"
  And a red toast surfaces the bilingual "An Excel file is required." / "ملف Excel مطلوب."
```

### E2E-OTH-011 — Copy and Paste toasts

```gherkin
Scenario: Copy one row, copy a selection, and paste
  When the administrator clicks the "Copy" icon on one row for "khalid.press@example.com"
  Then an info toast reads "Copied khalid.press@example.com to the clipboard."
  When they select three rows and click "Copy" on the toolbar
  Then an info toast reads "Copied 3 rows to the clipboard."
  When they click "Paste" with an empty clipboard
  Then a red toast reads "The clipboard is empty."
  When they click "Paste" with clipboard content
  Then an info toast reads "Paste-to-add will land with the User Management module."
```

### E2E-OTH-012 — Empty list

```gherkin
Scenario: No Other accounts renders SimfEmptyState
  Given the database has no Other-typed accounts
  When the administrator opens /admin/others
  Then POST /account/api/admin/others/list returns 200 with 0 rows
  And the grid body renders the SimfEmptyState reading
      "No Other accounts yet." / "لا يوجد مستخدمون من نوع آخر بعد."
  And the toolbar still shows the "Add" button
  And no error toast appears
```

### E2E-OTH-013 — Auth gate

```gherkin
Scenario: A signed-in admin lacking Others.View is denied
  Given a signed-in Control-Panel user whose role does NOT grant the
      "Others.View" permission (the page carries [RequirePermission(PermissionCatalog.Others.View)])
  When they navigate to /admin/others
  Then they land on /not-permitted with HTTP 200
  And no POST /account/api/admin/others/list request fires
  And the "Others" nav item is hidden for that user (CpNavigation RequiredPermission = Others.View)
```

### E2E-OTH-014 — Walk-in validation failures

```gherkin
Scenario: The walk-in wizard blocks incomplete submissions client-side
  Given the Add modal is open on the walk-in wizard
  When the administrator clicks "Register" with no profile-type tile selected
  Then a SimfAlert error appears reading the "A profile type is required." message
  And no register-onsite request fires
  When they pick a profile type but leave the English name blank and click "Register"
  Then the inline error switches to the English-name-required message
  And the same gating applies in order to Arabic name, Badge name,
      National ID (Saudi: 10 digits starting with 1),
      Nationality + Iqama (10 digits starting with 2) or Passport (non-Saudi),
      and "at least one mobile number"
  And no register-onsite request fires until every client rule passes
```

### E2E-OTH-015 — Bulk-delete reason guard

```gherkin
Scenario: The reason field guards the Delete button length
  Given the "Delete Other accounts" modal is open
  When the Reason has fewer than 10 characters
  Then the "Delete" button is disabled
  When the Reason exceeds 500 characters
  Then the "Delete" button is disabled
  When the Reason is between 10 and 500 characters
  Then the "Delete" button enables and the request can be sent
```

### E2E-OTH-016 — Duplicate conflict (existing email)

```gherkin
Scenario: Duplicate to an already-registered email returns 409
  Given an account already exists for "taken.other@example.com"
  When the administrator duplicates a row to New email="taken.other@example.com"
  And clicks "Duplicate"
  Then POST /account/api/admin/others/duplicate forwards to the API
  And the API returns HTTP 409 with ApiResult.Error.Code = "ADMIN_EMAIL_ALREADY_REGISTERED"
  And the Duplicate modal stays open
  And the error toast surfaces the bilingual MessageForCurrentCulture()
```

### E2E-OTH-017 — Cross-kind profile read (type-smuggling guard)

```gherkin
Scenario: A Visitor id on the Others profile route returns 404
  Given a known Visitor account id
  When a GET /account/api/admin/others/{visitorId}/profile is issued
  Then the API returns HTTP 404 with ApiResult.Error.Code = "NOT_FOUND"
  And the Details modal shows the bilingual fallback, never the visitor's data
```

### E2E-OTH-018 — Cross-kind ProfileTypeId in walk-in

```gherkin
Scenario: A Visitor-side profile type on the Other walk-in is rejected
  Given the walk-in wizard somehow carries a Visitor-side ProfileTypeId
  When the administrator submits the Other register-onsite
  Then the API returns HTTP 400 with ApiResult.Error.Code = "ADMIN_PROFILE_TYPE_INVALID"
  And the inline SimfAlert surfaces the bilingual server message
  And no account is created
```

### E2E-OTH-019 — No Other profile-type seeded

```gherkin
Scenario: Walk-in wizard prompts to seed an Other profile-type first
  Given the database has no active Other profile-types
      (none returned by /account/api/admin/profile-types?userType=Other)
  When the administrator opens the Add modal
  Then the Profile type section shows the info alert "No profile types are seeded"
      (Admin.WalkIn.ProfileType.NoneSeeded) instead of tiles
  And submitting still blocks on the "A profile type is required." client rule
```

### E2E-OTH-020 — Server 500 on /list

```gherkin
Scenario: API 500 on /list degrades to an empty grid without crashing
  Given the API is configured to return 500 on /admin/others/list (e.g. DB down)
  When the administrator opens /admin/others
  Then the grid shows the loading indicator and then renders an empty page
      (LoadAsync falls back to GridPage.Of(empty) on a non-success envelope)
  And no rows render and the page does not throw
  And the console shows no unhandled Blazor circuit error
```

### E2E-OTH-021 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page, wizard and modals
  Given the administrator is on /admin/others in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "مستخدمون آخرون"
  And the nav rail and toolbar mirror (Arabic labels, reversed order)
  When they open the Add modal
  Then the walk-in wizard renders RTL with Arabic section legends and field labels
  And the Saudi/Iqama/Passport toggles render right-to-left
  When they open the Delete modal
  Then the title reads "حذف الحسابات الأخرى" and the reason field + actions mirror
```

### E2E-OTH-022 — Organisation required + digit-only IDs on the Others walk-in (D-354)

```gherkin
Scenario: The Others walk-in requires an organisation too (shared form)
  Given the administrator opens the Add modal on /admin/others (Kind=Other)
  And fills a valid partner badge type, names, nationality/ID and a mobile
  But leaves the Organisation (الجهة) field unpicked
  When they press Register
  Then the form does not submit and an inline "Pick an organisation." error shows
  And the National ID / Iqama field strips any non-digit typed into it
```

### E2E-OTH-023 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch to full-page mode and it persists across reload
  Given the administrator is on /admin/others with the default "dialog" presentation
  And the grid toolbar shows the CrudPresentationToggle ("Open as full page", maximize icon)
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.others" holds {"v":1,"presentation":"page"}
  When they reload /admin/others
  Then OnInitializedAsync rehydrates the choice via Prefs.GetPresentationAsync("others")
  And the toggle still reads "Open as dialog"
  And opening "Add" now renders the full-page CrudShell frame (not a popup)
```

### E2E-OTH-024 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add (walk-in) / Edit / Details take over the content area; Save returns to the grid
  Given the presentation is set to "full page" (toggle in the page state)
  When the administrator clicks "Add" on the toolbar
  Then the SimfBanner + grid are replaced by the CrudShell full-page frame
      (title "Add Other user" + close header + the OthersAddEdit body)
  And there is no modal backdrop (GridHidden = FormOpen && presentation == Page)
  And the framed body hosts the walk-in CreateOtherForm wizard (Kind=Other, no Interests section)
  When they complete the wizard and the register-onsite succeeds
  Then the full-page frame closes
  And the grid re-appears with the new row and the green "Account created for {email}..." toast

  When the administrator clicks the "Edit" icon on a row
  Then GET /account/api/admin/others/{id}/profile returns 200
  And the full-page frame opens hosting OthersAddEdit → the shared EditAccountForm (Scope="others")
  When they change Display name and click "Save"
  Then PUT /account/api/admin/others/{id} returns 200
  And the frame closes and the grid re-appears with the "The account was updated." toast

  When the administrator clicks the "Details" icon on a row
  Then the full-page frame opens hosting the read-only OthersViewDelete profile body
      (Details-only — it renders NO Delete button; single-row delete stays the reason-gated
       /bulk-delete dialog, not a CrudShell+SimfConfirm gate)
  When they click "Close"
  Then the frame closes and the grid re-appears unchanged
```

---

## Implementation notes

- **Manual smoke as canonical source of truth today.** Until Playwright is
  adopted, the canonical "run" of these scenarios is a Chrome DevTools MCP
  session: sign in per the Background, walk each scenario, and capture
  screenshots into `docs/screenshots/cp-admin-others-{scenario}.png`.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin
  scenario into a `.feature` under `tests/SIMF.E2E.Tests/` (project to be
  created) plus a step-definition class. The Gherkin shape is runner-agnostic.
- **API integration tests** at
  [`tests/SIMF.Api.Tests/AdminGridOthersTests.cs`](../../../tests/SIMF.Api.Tests/AdminGridOthersTests.cs)
  cover the same surface at a lower layer (no browser): bulk-delete that
  skips cross-kind Visitor ids, duplicate, export, and the mandatory-ProfileTypeId
  import path on `/api/v1/admin/others/*`. The walk-in register-onsite,
  approve/reject queues, and ID-document upload/stream are covered by the
  broader admin-account API suites. When an E2E scenario is automated
  end-to-end, the matching `Api.Tests` case can usually be retired — keep both
  during the transition.
- **Permission contract.** The page is gated by
  `[RequirePermission(PermissionCatalog.Others.View)]`; the destructive and bulk
  actions are gated server-side by the matching `Others.*` permissions
  (`Create`, `Edit`, `Delete`, `Export`, `Import`, `RegisterOnsite`). An
  ungated path here is a security defect — `CpNavigationPermissionTests` and
  `PermissionEnforcementTests` fail the build if a gate is missing.

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle; D-353 presentation toggle scenarios added).
