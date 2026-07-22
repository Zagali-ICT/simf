# E2E test catalogue — Visitors (`/admin/visitors`)

| | |
|--|--|
| **Page** | [`cp/admin-visitors.md`](../../pages/cp/admin-visitors.md) |
| **Route** | `/admin/visitors` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **Page permission:** `[RequirePermission(PermissionCatalog.Visitors.View)]` (`Visitors.View`).
> Per-action gates on the backing API: `Visitors.Create` (Duplicate),
> `Visitors.RegisterOnsite` (the register-onsite call the Add wizard fires),
> `Visitors.Edit` (Edit), `Visitors.Delete` (row + bulk delete),
> `Visitors.Export`, `Visitors.Import`. `Administrator = "*"` satisfies all.
> The Add modal hosts the **D-127 walk-in registration wizard** (`CreateVisitorForm` →
> `WalkInRegistrationForm`), **not** a slim 2-field create form — register-onsite mints
> the QR badge and auto-approves the account in one transaction.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-VIS-001 | Golden round-trip — walk-in Add (Saudi) → Details (ID image) → Edit → row Delete | happy | P0 | _to author_ |
| E2E-VIS-002 | Walk-in non-Saudi branch (Passport) → Approved + QR minted | happy | P1 | _to author_ |
| E2E-VIS-003 | Empty list renders `SimfEmptyState` ("No visitors yet.") | happy | P1 | _to author_ |
| E2E-VIS-004 | Auth gate — admin lacking `Visitors.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-VIS-005 | List filter + sort + paging round-trip | happy | P1 | _to author_ |
| E2E-VIS-006 | Details modal — full profile + inline ID-document image (D-129) | happy | P1 | _to author_ |
| E2E-VIS-007 | Walk-in validation — Saudi ID typo → server 400 + form error | error | P1 | _to author_ |
| E2E-VIS-008 | Duplicate visitor with a new email → toast + reload | happy | P1 | _to author_ |
| E2E-VIS-009 | Duplicate conflict — reused email → 409 `AdminEmailAlreadyRegistered` | error | P1 | _to author_ |
| E2E-VIS-010 | Bulk delete (multiselect) with reason ≥10 chars → toast + reload | happy | P1 | _to author_ |
| E2E-VIS-011 | Bulk delete with no selection → "Select at least one row first." | error | P2 | _to author_ |
| E2E-VIS-012 | Row delete with reason < 10 chars → Delete button stays disabled | error | P2 | _to author_ |
| E2E-VIS-013 | Export selected → XLSX download (`simf-visitors-*.xlsx`) | happy | P1 | _to author_ |
| E2E-VIS-014 | Import XLSX → result modal (created/skipped + per-row errors) | happy | P1 | _to author_ |
| E2E-VIS-015 | Import non-XLSX file → bilingual validation error | error | P2 | _to author_ |
| E2E-VIS-016 | Copy one / copy selected → info toast | happy | P2 | _to author_ |
| E2E-VIS-017 | Paste → "Paste-to-add will land with the User Management module." | happy | P2 | _to author_ |
| E2E-VIS-018 | Cross-kind id on `/admin/visitors/{otherId}/profile` → 404 (D-124) | error | P1 | _to author_ |
| E2E-VIS-019 | Server 500 on `/list` → empty grid, no crash (resilient fallback) | resilience | P2 | _to author_ |
| E2E-VIS-020 | RTL / Arabic render — page + Add wizard + Details modal mirror | i18n | P1 | _to author_ |
| E2E-VIS-021 | Organisation is required (D-354) | error | P1 | _to author_ |
| E2E-VIS-022 | Numeric ID fields reject letters + inline field validation (D-354) | error | P1 | _to author_ |
| E2E-VIS-023 | Presentation toggle: switch Add/Edit/Details to full-page + persists across reload (D-353) | happy | P1 | _to author_ |
| E2E-VIS-024 | Full-page mode: Add/Edit/Details take over the content area, save/close returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-VIS-025 | Walk-in birth location (D-469) — Saudi → region `<select>` over the 13 official regions (code-keyed, cross-locale preselect); non-Saudi → free-text "as in passport" | validation | P1 | _to author_ |
| E2E-VIS-030 | Edit login email (D-214 + #24) — golden change → 200 + save toast, stamp roll + old-session revoke + new address unverified (re-verify at next sign-in); duplicate → 409 `ADMIN_EMAIL_ALREADY_REGISTERED` inline SimfAlert; name-only edit keeps the session; bad format → 400 | happy | P1 | _to author_ |
| E2E-VIS-031 | Bulk add (#10 batch-builder) — gated toolbar "Bulk add" opens the `BulkBadgeGenerator` dialog; build a batch (type + count → Add), Generate → confirm → `bulk-generate` 200; hidden without `Visitors.BulkGenerate` | happy | P1 | authored ✓ (BulkBadgeGeneratorTests; gate by CpNavigationPermission/PermissionEnforcement) |

## Scenarios

### E2E-VIS-001 — Golden round-trip (walk-in Add → Details → Edit → Delete)

```gherkin
Feature: Visitors page golden round-trip
  As an Administrator on the registration desk
  I want to register a walk-in, inspect the profile, edit it, then remove it
  So that the visitor roster stays accurate end-to-end

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And they have landed on /admin/visitors
  And the grid has loaded via POST /account/api/admin/visitors/list

Scenario: Register a Saudi walk-in, view, edit and delete them
  Given the grid currently shows {N} rows
  When the administrator clicks the toolbar "Add visitor" action
  Then the SimfModal "Add visitor" opens hosting the D-127 walk-in wizard
  And section 1 "Badge type" shows the colour-coded ProfileType tile picker

  When they pick a Visitor profile-type tile
  And in section 2 "Identity" they fill Name on badge="Faisal Al-Otaibi", Date of birth="1990-04-12", English name="Faisal Al-Otaibi", Arabic name="فيصل العتيبي", Place of birth="Riyadh"
  And in section 3 "Nationality and ID" they keep the "Saudi" toggle and fill National ID="1099887766"
  And in section 4 "Contact" they fill Saudi mobile="+966500112233" and leave Email blank
  And they leave section 5 "ID document" empty and pick two interests in section 6
  And they submit the wizard
  Then the BFF forwards POST /account/api/admin/visitors/register-onsite (AdminWalkInRegistrationRequest)
  And the API returns 200 with AdminWalkInRegistrationResponse { UserId, QrId, Email }
  And the WalkInSuccessModal renders the badge with an SVG QR and Done / Print / Register another
  When they click "Done"
  Then the Add modal closes
  And a green toast reads "Invitation sent to {email}." (Admin.CreateVisitor.Success)
  And the grid reloads and shows {N + 1} rows including the new Approved visitor

  When the administrator clicks the "Details" action on that row
  Then a read-only modal "Visitor details — {email}" opens
  And GET /account/api/admin/visitors/{id}/profile returns 200
  And the description lists render Email, Display name, User type, State=Approved, Created, English/Arabic name, Nationality, Identity type="National ID", Identity number="1099887766", Saudi mobile, Interest count and QR id
  When they click "Close"
  Then the modal closes

  When the administrator clicks the "Edit" action on that row
  Then the SimfModal "Edit visitor" opens with EditAccountForm (Scope=visitors) pre-filled
  When they change the Display name and click Save
  Then PUT /account/api/admin/visitors/{id} returns 200
  And the modal closes and a green toast reads "The account was updated." (Admin.Edit.Saved)
  And the grid reloads with the new display name
  # Build #24: if the Edit also CHANGES the Email, the account is signed out (the
  # security stamp is rolled) AND the new address is marked unverified
  # (EmailConfirmed=false), so it is re-verified at the user's next sign-in via the
  # email-OTP 2FA. Sign-in gates on AccountState, not EmailConfirmed, so this is
  # NOT a lockout - it just re-proves the corrected address is deliverable.

  When the administrator clicks the "Delete" action on that row
  Then the "Delete visitors" modal opens reading "This will disable 1 visitor account(s)..."
  And the Delete button is disabled until the Reason has 10–500 chars
  When they type Reason="Test fixture cleanup for E2E run" and click "Delete"
  Then POST /account/api/admin/visitors/bulk-delete returns 200
  And a green toast reads "{deleted} deleted, {skipped} skipped." (Admin.Users.BulkDelete.Success)
  And the grid reloads back to {N} rows
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-visitors-golden-before.png`
- Screenshot after: `docs/screenshots/cp-admin-visitors-golden-after.png` (+ `-walkin-wizard.png`, `-success-modal.png`, `-details-modal.png`, `-edit-modal.png`, `-bulkdelete-modal.png`)
- Console errors: 0 expected
- Network: every `/account/api/admin/visitors/*` call returns 200; the register-onsite, profile, PUT and bulk-delete calls are all 200
- Audit rows: `OperationLog` / `RowAudit` rows for `Admin.WalkInRegistered`, the profile read, the update, and the soft-delete, each with the actor id

### E2E-VIS-002 — Walk-in non-Saudi branch (Passport)

```gherkin
Scenario: Register a non-Saudi visitor by passport
  Given the Add walk-in wizard is open
  When in section 3 the administrator flips the toggle to "Non-Saudi"
  Then a country picker and an Iqama/Passport sub-picker appear
  When they choose country="GBR", pick the "Passport" sub-option and fill Passport="P1234567"
  And in section 4 they fill International mobile="+447700900123"
  And they complete the required identity fields and submit
  Then POST /account/api/admin/visitors/register-onsite returns 200
  And the account lands in Approved state with a minted QR id
  And the WalkInSuccessModal renders the badge
```

### E2E-VIS-003 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no Visitor accounts
  When the administrator opens /admin/visitors
  Then POST /account/api/admin/visitors/list returns 200 with an empty page
  And the grid body renders the SimfEmptyState component titled "No visitors yet." / "لا يوجد زوار بعد." (Admin.Visitors.None)
  And the toolbar still shows the "Add visitor" action
  And no error toast appears
```

### E2E-VIS-004 — Auth gate

```gherkin
Scenario: Signed-in admin lacking Visitors.View is denied
  Given a signed-in Control Panel user whose roles do NOT grant Visitors.View
  When they navigate to /admin/visitors
  Then they land on /not-permitted with HTTP 200
  And no POST /account/api/admin/visitors/list request fires
  And the "Visitors" nav-rail item is hidden (its RequiredPermission is Visitors.View)
```

### E2E-VIS-005 — Filter + sort + paging

```gherkin
Scenario: Filter, sort and page through the grid
  Given the grid shows more than one page of visitors at PageSize=20
  When the administrator types "alotaibi" into the filter for the Email column
  Then POST /account/api/admin/visitors/list fires with the filter in the GridQuery
  And only matching rows render and the pager summary updates
  When they clear the filter and click the "Email" header to sort
  Then the list reloads sorted by email and the sort indicator shows on that column
  When they click "Next" / "Last" in the pager
  Then the list reloads with the new Skip/Top and the page formatter updates ("Page X of Y")
```

### E2E-VIS-006 — Details modal with ID-document image

```gherkin
Scenario: Details modal renders the inline ID-document image
  Given a visitor exists who has an ID document on file (HasIdImage = true)
  When the administrator clicks "Details" on that row
  Then GET /account/api/admin/visitors/{id}/profile returns 200 with HasIdImage = true
  And the modal shows the "ID document" heading
  And an <img> is rendered with src "/account/api/admin/visitors/{id}/id-document?v={ticks}"
  And that GET streams the decrypted image and returns 200 (image content-type)
  And no Console error is logged for the image request
```

### E2E-VIS-007 — Walk-in validation failure

```gherkin
Scenario: A malformed Saudi national ID is rejected
  Given the Add walk-in wizard is open on the Saudi branch
  When the administrator fills National ID="0123456789" (not matching ^1\d{9}$)
  And completes the other required fields and submits
  Then the server-side AdminWalkInRegistrationRequestValidator rejects it
  And POST /account/api/admin/visitors/register-onsite returns HTTP 400 (DataValidationException)
  And the wizard stays open showing the field-level validation error
  And no visitor row is added to the grid
```

### E2E-VIS-008 — Duplicate visitor (happy)

```gherkin
Scenario: Duplicate an existing visitor under a new email
  Given a visitor row exists
  When the administrator clicks the "Duplicate" action on that row
  Then the "Duplicate visitor" modal opens reading "Create a copy of {email} under a new email address."
  And the Duplicate button is disabled until the new email looks valid (contains '@', ≤256 chars)
  When they fill New email address="faisal.copy@example.com" and click "Duplicate"
  Then POST /account/api/admin/visitors/duplicate (AdminDuplicateUserRequest) returns 200
  And the modal closes and a green toast reads "Created faisal.copy@example.com." (Admin.Users.Duplicate.Success)
  And the grid reloads with the new row
```

### E2E-VIS-009 — Duplicate conflict (reused email)

```gherkin
Scenario: Duplicating onto an email that already exists returns 409
  Given a visitor with email "existing@example.com" already exists
  When the administrator opens the Duplicate modal on any visitor row
  And fills New email address="existing@example.com" and clicks "Duplicate"
  Then POST /account/api/admin/visitors/duplicate returns HTTP 409
  And ApiResult.Error.Code = "AdminEmailAlreadyRegistered"
  And the modal stays open
  And a red toast surfaces the bilingual MessageForCurrentCulture()
      "An account with this email address already exists." / "يوجد حساب مسجّل بهذا البريد الإلكتروني بالفعل."
```

### E2E-VIS-010 — Bulk delete with reason

```gherkin
Scenario: Bulk-delete two selected visitors with an audited reason
  Given the administrator ticks the multiselect checkboxes on two visitor rows
  When they click the toolbar "Delete" (bulk) action
  Then the "Delete visitors" modal opens reading "This will disable 2 visitor account(s)..."
  And the Reason textarea shows the helper "10–500 characters, audited."
  And the Delete button is disabled while the reason length is < 10 or > 500
  When they type Reason="Duplicate registrations removed after audit" and click "Delete"
  Then POST /account/api/admin/visitors/bulk-delete returns 200
  And a green toast reads "{deleted} deleted, {skipped} skipped." (Admin.Users.BulkDelete.Success)
  And the grid reloads without the deleted rows
```

### E2E-VIS-011 — Bulk delete with no selection

```gherkin
Scenario: Bulk delete with nothing selected shows a guard toast
  Given no rows are selected
  When the administrator triggers the bulk "Delete" action with an empty selection
  Then no modal opens
  And a red toast reads "Select at least one row first." / "اختر صفًا واحدًا على الأقل." (Admin.Users.NoSelection)
  And no POST /account/api/admin/visitors/bulk-delete request fires
```

### E2E-VIS-012 — Delete reason too short

```gherkin
Scenario: A short reason keeps the Delete button disabled
  Given the row "Delete" action has opened the "Delete visitors" modal for one row
  When the administrator types Reason="too short" (9 chars, < 10)
  Then the "Delete" button stays disabled
  And no POST /account/api/admin/visitors/bulk-delete request fires
  When they extend the reason to "removed per audit" (≥ 10 chars)
  Then the "Delete" button becomes enabled
```

### E2E-VIS-013 — Export selected to XLSX

```gherkin
Scenario: Export selected visitors downloads an XLSX
  Given the administrator has ticked one or more visitor rows
  When they click the toolbar "Export" action
  Then POST /account/api/admin/visitors/export (AdminExportUsersRequest with the selected Ids) fires
  And the response is application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
  And the browser downloads a file named "simf-visitors-{yyyyMMddHHmmss}.xlsx"
  When no rows are selected and Export is clicked
  Then the request carries Query=_query instead of Ids and exports the current filtered view
```

### E2E-VIS-014 — Import XLSX

```gherkin
Scenario: Import a visitors workbook and read the result modal
  Given the administrator has a valid .xlsx workbook with visitor rows
  When they click the toolbar "Import" action
  Then the hidden #visitors-import-input file picker opens (accept=".xlsx")
  When they choose the workbook
  Then POST /account/api/admin/visitors/import (multipart) returns 200 with AdminImportUsersResponse
  And the "Import result" modal opens reading "{created} created, {skipped} skipped." (Admin.Users.Import.ResultBody)
  And any per-row failures render as "Row {n} ({email}): {reason}" list items
  And the grid reloads to include the imported visitors
```

### E2E-VIS-015 — Import non-XLSX file

```gherkin
Scenario: Importing a non-workbook file is rejected
  Given the Import picker is open
  When the administrator selects a file that is not a valid .xlsx (no ZIP magic 50 4B 03 04)
  Then POST /account/api/admin/visitors/import returns a 400 DataValidationException
  And a red toast surfaces the bilingual message
      "The file is not a valid Excel workbook." / "الملف ليس مصنف Excel صالحًا."
      (or the Admin.Users.Import.Fallback "Import failed." / "فشل الاستيراد." when no server message is present)
  And no Import result modal opens
```

### E2E-VIS-016 — Copy actions

```gherkin
Scenario: Copy one and copy selected raise info toasts
  When the administrator clicks "Copy" on a single row
  Then an info toast reads "Copied {email} to the clipboard." (Admin.Users.Copy.One)
  When they tick two rows and use the bulk "Copy" action
  Then an info toast reads "Copied 2 rows to the clipboard." (Admin.Users.Copy.Count)
  And neither action fires a network request
```

### E2E-VIS-017 — Paste action

```gherkin
Scenario: Paste surfaces the deferred-feature notice
  When the administrator triggers the "Paste" action with clipboard content
  Then an info toast reads "Paste-to-add will land with the User Management module." (Admin.Users.Paste.NotImplemented)
  When they trigger Paste with an empty clipboard
  Then a red toast reads "The clipboard is empty." (Admin.Users.Paste.Empty)
```

### E2E-VIS-018 — Cross-kind id 404 (D-124 security)

```gherkin
Scenario: Requesting a non-Visitor id on the visitor profile route returns 404
  Given the id of an Other/partner-typed account (not a Visitor)
  When a GET /account/api/admin/visitors/{otherId}/profile is issued (e.g. via a hand-crafted Details deep-link)
  Then the API returns HTTP 404 with ApiResult.Error.Code = "NotFound"
      message "No visitor was found for this id." / "لم يتم العثور على زائر بهذا المعرّف."
  And the Details modal shows the SimfAlert error (no profile fields leak)
  And the response never reveals "exists but wrong type"
```

### E2E-VIS-019 — Server 500 on list

```gherkin
Scenario: API 500 on /list degrades to an empty grid without crashing
  Given the API is configured to fail POST /admin/visitors/list (e.g. DB down)
  When the administrator opens /admin/visitors
  Then the BFF returns a non-success envelope
  And the page falls back to GridPage.Of(empty) — the grid renders the SimfEmptyState
  And the page does not throw; no unhandled Console error blocks interaction
```

### E2E-VIS-020 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page, the Add wizard and the Details modal
  Given the administrator is on /admin/visitors in English
  When they switch culture to Arabic
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "الزوار" (Admin.Visitors.Title)
  And the nav rail and toolbar mirror (Arabic labels, reversed order)
  And the empty state, if shown, reads "لا يوجد زوار بعد."

  When they open the "Add visitor" modal
  Then the walk-in wizard renders RTL with Arabic section labels and reversed actions
  When they open a Details modal
  Then the description list labels are Arabic and the modal title reads "تفاصيل الزائر — {email}"
```

### E2E-VIS-021 — Organisation is required (D-354)

```gherkin
Scenario: The walk-in cannot be registered without an organisation
  Given I am signed in as an Administrator and open the Add-visitor modal
  And I fill a valid badge type, English + Arabic name, badge name, nationality/ID and a mobile
  But I leave the Organisation (الجهة) field unpicked
  When I press Register
  Then the form does not submit
  And an inline error under the Organisation field reads "Pick an organisation." ("اختر الجهة." in Arabic)

Scenario: Picking an organisation from the typeahead unblocks the submit
  Given I am in the Add-visitor modal with every other field valid
  When I type "Aramco" into the Organisation search box
  And I pick "Saudi Aramco" from the results list
  Then the field shows "Selected: Saudi Aramco"
  And pressing Register creates the visitor and shows the WalkInSuccessModal QR badge
```

### E2E-VIS-022 — Numeric ID fields reject letters + inline field validation (D-354)

```gherkin
Scenario: The National ID / Iqama field accepts digits only
  Given I am in the Add-visitor modal
  When I type "12ab34" into the Saudi National ID field
  Then the field shows "1234" (non-digits are stripped as I type)

Scenario: An invalid Iqama shows an inline error on the field, not just a top banner
  Given I switch the visitor to Non-Saudi and pick the Iqama document type
  And I enter "1000000000" (does not start with 2) into the Iqama field
  When I press Register
  Then an inline error renders directly under the Iqama field
  And the form does not submit
```

### E2E-VIS-023 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch the Add/Edit/Details framing to full-page and it persists across reload
  Given the administrator is on /admin/visitors with the default "dialog" presentation
  And the grid toolbar shows the CrudPresentationToggle "Open as full page" control (maximize icon)
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.visitors" holds {"v":1,"presentation":"page"}
  When they reload /admin/visitors
  Then OnInitializedAsync re-reads the preference via Prefs.GetPresentationAsync("visitors")
  And the toggle still reads "Open as dialog"
  And opening "Add visitor" now renders the full-page CrudShell frame (not a popup)
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-visitors-toggle-page.png`
- Console errors: 0 expected
- Storage: `simf.cp.prefs.visitors` = `{"v":1,"presentation":"page"}` after the toggle, and still present after reload
- Network: toggling fires no `/account/api/admin/visitors/*` request (the preference is client-side only)

### E2E-VIS-024 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/Details take over the content area; save or close returns to the grid
  Given the presentation is set to "full page" (Presentation = Page)
  When the administrator clicks the toolbar "Add visitor" action
  Then the SimfBanner + grid are hidden (GridHidden) and the CrudShell renders the
       full-page frame titled "Add visitor" (Admin.Visitors.Add.Title) hosting the
       D-127 walk-in CreateVisitorForm wizard
  And there is no modal backdrop
  When they complete the wizard and it reports success (register-onsite 200)
  Then the CrudShell closes (CloseForm) and the banner + grid re-appear
  And a green toast reads "Invitation sent to {email}." (Admin.CreateVisitor.Success)
  And the grid reloads via POST /account/api/admin/visitors/list

  When the administrator clicks the "Edit" action on a row
  Then the full-page frame titled "Edit visitor" (Admin.Visitors.Edit.Title) hosts EditAccountForm (Scope=visitors)
  When they change the Display name and Save
  Then PUT /account/api/admin/visitors/{id} returns 200
  And the frame closes, the grid re-appears, and a green toast reads "The account was updated." (Admin.Edit.Saved)

  When the administrator clicks the "Details" action on a row
  Then the full-page frame titled "Visitor details — {email}" (Admin.Visitors.Details.Title) hosts the
       details-only VisitorsViewDelete form (no Delete button — visitor delete is the reason-gated bulk dialog)
  And GET /account/api/admin/visitors/{id}/profile returns 200 and the description lists render
  When they click the frame "Close" (Admin.Visitors.Details.Close)
  Then the frame closes and the grid re-appears unchanged
```

**Evidence captured:**
- Screenshots: `docs/screenshots/cp-admin-visitors-fullpage-add.png`, `-fullpage-edit.png`, `-fullpage-details.png`
- Console errors: 0 expected
- Network: register-onsite / PUT / profile / list calls all return 200; no modal backdrop element is present in the DOM while a full-page frame is open

### E2E-VIS-025 — Walk-in birth location: Saudi region dropdown / non-Saudi free text (D-469)

```gherkin
Scenario: A Saudi walk-in's place of birth is a region dropdown
  Given the administrator is on the walk-in Add wizard with the "Saudi" toggle on
  Then section 2 "Identity" renders "Place of birth" as a <select> over the 13
       official Saudi regions, defaulting to the "Select region" placeholder
  When they pick a region
  Then the localized region name is submitted in AdminWalkInRegistrationRequest.PlaceOfBirth
       (the existing free-text column — no schema change)

Scenario: A previously-stored region preselects regardless of UI language
  Given a record whose stored place of birth is "Riyadh" (saved in English)
  When the form is rendered with the CP UI culture set to Arabic
  Then the <select> still preselects the Riyadh option (it is keyed on the region
       code via SaudiRegions.ByName, not on the stored name string)

Scenario: A non-Saudi walk-in types the place of birth
  Given the administrator turns the "Saudi" toggle off
  Then "Place of birth" becomes a free-text field with the "As in the passport." helper
```

**Evidence:** shared-constant lookups covered by `tests/SIMF.Api.Tests/SaudiRegionsTests.cs`
(ByName maps either language → code; ByCode → localized name). Live browser drive of
the auth-gated walk-in wizard is pending the broader E2E-VIS authoring pass (all
E2E-VIS rows are `_to author_`).

### E2E-VIS-026 — Change type: flip a Visitor to a partner (Other) type (D-728)

```gherkin
Scenario: An admin converts a visitor into a partner (Other) account
  Given an approved visitor account
  And at least one active partner profile type (IsVisitor=false, e.g. Sponsor/Staff)
  When the administrator opens the visitor's Details view
  Then the "Change account type" block renders (gated by Accounts.ChangeType)
  And its "New type" dropdown lists ONLY active partner-scope types
      (no visitor types, no inactive types — the opposite scope only)
  When they pick a partner type and click "Change type"
  Then POST /account/api/admin/accounts/{id}/change-type returns 200
  And the block shows the green "The account type was changed..." success alert
  And the account's ProfileTypeId is now the picked partner type
  And the account leaves /admin/visitors and appears in /admin/others
  And the security stamp was rolled + sessions revoked (a partner type may grant
      Staff/Moderator app perms), while the approval state is unchanged

Scenario: A same-scope target is rejected
  Given an approved visitor account
  When a change-type request targets ANOTHER visitor-scope profile type
  Then POST /account/api/admin/accounts/{id}/change-type returns 400
      (ADMIN_PROFILE_TYPE_INVALID — a same-scope change is an edit, not a type change)
```

**Evidence:** `tests/SIMF.Api.Tests/AdminChangeAccountTypeTests.cs` (flip both
directions, same-scope 400, inactive 400, empty 400, 404, non-admin 403, stamp
roll, approval-state preserved); CP block behaviour in
`tests/SIMF.ControlPanel.Tests/ChangeAccountTypeBlockTests.cs` (opposite-scope
filter + POST wiring). Live browser drive pending the E2E-VIS authoring pass.

---

## Implementation notes

- **Manual smoke is canonical today.** Until Playwright is adopted, the canonical
  "run" of these scenarios is a Chrome DevTools MCP session: sign in per the
  Auth setup, walk each scenario, and capture screenshots into
  `docs/screenshots/cp-admin-visitors-{scenario}.png`. Each Gherkin block is
  written runner-agnostic so it ports straight into a `.feature` file under
  `tests/SIMF.E2E.Tests/` later.
- **The Add modal is the D-127 walk-in wizard, not a 2-field create.** Drive
  `WalkInRegistrationForm` (badge type → identity → nationality/ID → contact →
  ID document → interests) and assert the `WalkInSuccessModal` QR badge. The
  account is created already-Approved (no pending queue) and a QR id is minted
  in the same transaction. `/admin/visitors/new` (`CreateVisitor.razor`) is a
  preserved deep-link fallback to the same flow.
- **2026-07-22 redesign (behaviour-preserving).** The wizard is now grouped into
  numbered `SimfFormSection` cards (SpeakersAddEdit parity) on the responsive
  `simf-form__grid`; the DOB / gender / preferred-language / nationality /
  birth-region controls use `SimfDatePicker` / `SimfSelect`, and the ID-document /
  photo inputs use `SimfFileUpload`. All still render native `<input>` / `<select>`
  under the field shell, so the field-level scenarios above are unchanged. A single
  wrapping `<fieldset disabled>` preserves the submit-time lockout. Structure pinned
  by `tests/SIMF.ControlPanel.Tests/WalkInRegistrationFormTests.cs`.
- **API integration tests cover the same surface at a lower layer** (no browser):
  - `tests/SIMF.Api.Tests/AdminGridVisitorsTests.cs` — list/grid + bulk-delete /
    duplicate / export / import type-scoping (incl. the `AdminUserNotFound` 404
    on cross-kind ids).
  - `tests/SIMF.Api.Tests/WalkInRegistrationTests.cs` — register-onsite golden +
    Saudi/Iqama/Passport validation branches.
  - `tests/SIMF.Api.Tests/AdminUpdateUserTests.cs` — the visitor Edit (PUT) path.
  - `tests/SIMF.Api.Tests/PendingProfileReadTests.cs` — the profile read + the
    D-124 404-for-mismatch rule.
  - `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` — the per-action
    `Visitors.*` policy gates that back the auth-gate scenario.
  Where an E2E scenario fully covers one of these, the lower-layer case may be
  retired later — keep both during the transition.

## On-site remediation (W4 — H-1 duplicate-identity guard)

| Id | Scenario | Category | Priority | Status |
|----|----------|----------|----------|--------|
| E2E-VIS-027 | Walk-in with an already-registered National ID / Iqama / passport → `DUPLICATE_IDENTITY` (409) | conflict | P0 | _to author_ |

### E2E-VIS-027 — duplicate identity is rejected at the desk

```gherkin
Scenario: a National ID that already belongs to a profile cannot be walked in again
  Given a visitor with National ID 1101798278 is already registered
  When staff submit a walk-in with the same National ID (different email)
  Then the API responds 409 with error code DUPLICATE_IDENTITY
  And the desk shows the bilingual message "An account is already registered with
      this national ID, Iqama, or passport number." /
      "يوجد حساب مسجّل بالفعل بهذه الهوية الوطنية أو رقم الإقامة أو جواز السفر."
  And no new account is created
  # Iqama and passport are matched the same way (via the identity blind index);
  # a distinct identity still registers cleanly. The same guard covers the staff
  # app twin POST /app/staff/visitors/register-onsite.
```

### E2E-VIS-028 — the list shows the visitor's profile-photo thumbnail (D-568)

```gherkin
Scenario: the name column renders a photo thumbnail when the visitor has an avatar
  Given an Administrator is on /admin/visitors
  And visitor "A" has a profile photo (avatar) set and visitor "B" has none
  When the grid loads a page of visitors
  Then visitor A's name cell shows a circular photo thumbnail beside the display name
  And visitor B's name cell shows a tinted initials tile (never a broken image)
  And no avatar request is issued for visitor B (the URL is only built when HasAvatar)
  And sorting / filtering by the Name column still works (the column key is unchanged)
```

**Covered (lower layer):** `tests/SIMF.Api.Tests/PendingProfileReadTests.cs` →
`Others_pending_list_row_reports_HasAvatar_once_a_photo_is_set` asserts the list
row's `HasAvatar` flips with the `AvatarRelativePath` sentinel (the same
projection backs the visitors list). The thumbnail render itself is the shared
`SimfIdentityCell` proven on the Speakers/Sponsors lists; confirm visually in the
Chrome DevTools MCP smoke.

### E2E-VIS-029 — Edit visitor: change the profile photo + ID image (VIP edit)

```gherkin
Scenario: the visitor Edit form can replace the profile photo and ID image
  Given an Administrator holding Visitors.Edit is on /admin/visitors
  When they open the Edit form for a visitor (SimfModal, EditAccountForm, Scope=visitors)
  Then below the email / display-name / tier fields a "Photo & ID" section shows:
    """
    Profile photo | ID document
    """
  And each shows the current image (when one is on file) plus a file input to replace it
  And the "VIP welcome photo" input is NOT shown here (ShowVipPhoto is only set on the VIP page)
  When they pick a new PNG (< 2 MB) for "Profile photo" and click "Save"
  Then PUT /account/api/admin/visitors/{id} fires first (email + name + tier)
  And then POST /account/api/admin/visitors/{id}/avatar (multipart "file") returns 200
  And the form closes and the grid's name-cell thumbnail reflects the new photo
  # Leaving both inputs empty saves only the core fields and uploads nothing.
  # An ID image with no human face returns 400 VISITOR_ID_IMAGE_NO_FACE and the form stays open.
```

**Covered (lower layer):** `tests/SIMF.Api.Tests/AdminAvatarEndpoints`-backed cases in
`WalkInRegistrationTests.cs` (`Admin_uploads_visitor_avatar_sets_path`) and
`AdminIdDocumentAuditTests.cs` cover the upload endpoints the Edit form reuses.

### E2E-VIS-030 — Edit visitor: change the login email (D-214 + #24)

```gherkin
Feature: Edit a visitor's login email
  As an Administrator on the Visitors desk
  I want to correct a visitor's login email from the Edit form
  So that a mistyped address is fixed, stale sessions die, and the new address is re-proven

Background:
  Given an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And they are on /admin/visitors with the grid loaded
  And an Approved visitor "faisal@example.com" exists
  And a separate account "taken@example.com" already exists

Scenario: Golden — change the login email
  When the administrator clicks the "Edit" action on the "faisal@example.com" row
  Then the SimfModal "Edit visitor" opens hosting EditAccountForm (Scope=visitors)
  And GET /account/api/admin/visitors/{id}/profile returns 200 and pre-fills Email, Display name and tier
  When they change Email to "faisal.new@example.com" (leaving the display name and tier unchanged) and click "Save"
  Then PUT /account/api/admin/visitors/{id} (AdminUpdateVisitorRequest) returns 200
  And the modal closes and the host list raises a green toast
      "The account was updated." / "تم تحديث الحساب." (Admin.Edit.Saved)
  And the grid reloads showing the new email

Scenario: The email change rolls the security stamp, revokes sessions and re-verifies the new address (#24)
  Given the golden change above returned 200
  Then AdminAccountService.UpdateAccountAsync rolled the security stamp and revoked the
       visitor's refresh tokens (emailChanged=true), so the visitor's live app / Website
       sessions are signed out at their next request and a refresh with the old refresh
       token is rejected
  And the new address is marked unverified (EmailConfirmed=false), so the visitor's next
      sign-in email-OTP 2FA is sent to "faisal.new@example.com" to re-prove deliverability
  And this is NOT a lockout — sign-in gates on AccountState (still Approved), not EmailConfirmed
  And an AdminUserUpdated audit row is written with Detail containing "emailChanged=True"

Scenario: Duplicate email is rejected inline and the form stays open
  When the administrator opens Edit on "faisal@example.com", changes Email to
      "taken@example.com" (already registered to another account) and clicks "Save"
  Then PUT /account/api/admin/visitors/{id} returns HTTP 409
  And ApiResult.Error.Code = "ADMIN_EMAIL_ALREADY_REGISTERED" (ErrorCodes.AdminEmailAlreadyRegistered)
  And the Edit form stays open showing the inline SimfAlert (Variant="error") with the bilingual
      MessageForCurrentCulture() "An account with this email address already exists." /
      "يوجد حساب مسجّل بهذا البريد الإلكتروني بالفعل."
  And no success toast is raised and the grid is not reloaded

Scenario: A name-only edit keeps the visitor signed in
  When the administrator opens Edit and changes ONLY the Display name (Email and tier unchanged) and clicks "Save"
  Then PUT /account/api/admin/visitors/{id} returns 200 and the "The account was updated." toast shows
  And because the email did not change the security stamp is NOT rolled and no refresh token is
      revoked (emailChanged=false) — the visitor's existing sessions stay valid and EmailConfirmed is unchanged

Scenario: A malformed email is rejected with a 400 field error
  Given the client CanSave gate only requires the email to be non-blank, so "not-an-email" enables Save
  When the administrator changes Email to "not-an-email" and clicks "Save"
  Then PUT /account/api/admin/visitors/{id} returns HTTP 400
      (UpdateVisitorRouteRequestValidator — RuleFor(Email).EmailAddress())
  And the inline SimfAlert shows "A valid email address is required." / "يجب إدخال بريد إلكتروني صالح."
  And the form stays open and no row changes
```

**Covered (lower layer):** `tests/SIMF.Api.Tests/AdminUpdateUserTests.cs` —
`Update_visitor_changes_email_and_display_name` (golden),
`Update_visitor_email_change_rolls_security_stamp` (stamp roll + session revoke),
`Update_visitor_duplicate_email_is_409` (the 409), and
`Update_visitor_short_display_name_is_400` (the validator). Live browser drive of the
auth-gated Edit form is pending the broader E2E-VIS authoring pass (all E2E-VIS rows
are `_to author_`). The email-change re-verify note also anchors the E2E-VIS-001
golden Edit step (Build #24).

### E2E-VIS-031 — Bulk add badges from the visitors list (#10 batch-builder)

```gherkin
Feature: Bulk badge generation is reachable from the Visitors list
  As an Administrator holding Visitors.BulkGenerate
  I want to generate placeholder badges without leaving /admin/visitors

Scenario: Build a batch and generate from the toolbar dialog
  Given the administrator is on /admin/visitors with Visitors.BulkGenerate
  And the grid toolbar shows a "Bulk add" button (AuthorizedAction, Visitors.BulkGenerate)
  When they click "Bulk add"
  Then a SimfModal opens hosting the shared BulkBadgeGenerator (ShowHeader=false)
  When they choose the "VIP" profile type, enter count "5", and click "Add"
  Then a batch row "VIP × 5" appears with a running total of 5
  When they click "Generate badges" and confirm the popup (no organiser email)
  Then POST /account/api/admin/visitors/bulk-generate returns 200 with Created = 5
  And a success toast reads "5 badge(s) generated."
  # IsDelegate is off by default here (the delegates desk defaults it on).

Scenario: The Bulk add button is hidden without the permission
  Given a signed-in admin whose roles do NOT grant Visitors.BulkGenerate
  When they open /admin/visitors
  Then the "Bulk add" toolbar button is not rendered
  And a hand-crafted bulk-generate POST is rejected by the endpoint policy
```

**Covered (lower layer):** `tests/SIMF.ControlPanel.Tests/BulkBadgeGeneratorTests.cs`
(add / merge / pick-type / post the built batch); the endpoint gate by
`PermissionEnforcementTests` (`Visitors.BulkGenerate`) and the toolbar `AuthorizedAction`.
The same generator + request contract is exercised on `/admin/delegates`
(`cp-admin-delegates.md`, E2E-DLG-004/013/014).

---

_Last reviewed:_ 2026-07-22 by Claude (#10 front-end redesign - the walk-in wizard regrouped into SimfFormSection cards + SimfSelect/SimfDatePicker/SimfFileUpload (behaviour-preserving, E2E-VIS structure note + WalkInRegistrationFormTests); added E2E-VIS-031 - the gated "Bulk add" toolbar dialog hosting the shared BulkBadgeGenerator batch-builder). Prior: 2026-07-22 by Claude (#24 DoD - added E2E-VIS-030, the dedicated edit-email scenario for PUT /admin/visitors/{id}: golden change, stamp roll + old-session revoke + EmailConfirmed=false re-verify, duplicate 409 ADMIN_EMAIL_ALREADY_REGISTERED inline, name-only keeps the session, bad-format 400). Prior: 2026-07-22 by SIMF Team (Build #24 - noted on E2E-VIS-001 that an Edit which changes the email now marks it unverified (EmailConfirmed=false) for re-verification at next sign-in; not a lockout). Prior: 2026-07-21 by Claude (VIP edit - the shared EditAccountForm gained a Photo & ID section; E2E-VIS-029). Earlier: 2026-07-11 by Claude (W4 on-site remediation - H-1 duplicate-identity guard; E2E-VIS-027). Earlier: 2026-07-09 by SIMF Team (D-728 - E2E-VIS-026 change-account-type); 2026-06-20 (D-469 - E2E-VIS-025 Saudi birth-location region dropdown); 2026-06-10 (D-356 Phase 5 - Excel + toggle; E2E-VIS-023/024).
