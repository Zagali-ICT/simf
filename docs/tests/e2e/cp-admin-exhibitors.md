# E2E test catalogue — Exhibitors CRUD (`/admin/exhibitors`)

| | |
|--|--|
| **Page** | [`cp/admin-exhibitors.md`](../../pages/cp/admin-exhibitors.md) |
| **Route** | `/admin/exhibitors` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5) |

> **Page permission:** the page is gated by `@attribute [RequirePermission(PermissionCatalog.Exhibitors.View)]`.
> The grid CRUD action buttons (Add / Edit / Details / Delete) are surfaced by `SimfDataGrid`
> itself and are **not** individually wrapped in `<AuthorizedAction>`, so any admin who can
> open the page sees them — but the API enforces the finer-grained `Exhibitors.Create` /
> `Exhibitors.Edit` / `Exhibitors.Delete` policies on the underlying endpoints (`POST
> /admin/exhibitors`, `PUT /admin/exhibitors/{id}`, `DELETE /admin/exhibitors/{id}`). The
> **only** individually `<AuthorizedAction>`-gated affordance is the per-row "Accounts"
> (account-provisioning) icon, wrapped in `<AuthorizedAction Permission="Exhibitors.Edit">`.
> E2E-EXH-009 covers the per-action API gate.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-EXH-001 | Full CRUD round-trip — Add → Edit → View/Delete (deactivate) via CrudShell | happy | P0 | _to author_ |
| E2E-EXH-002 | Add an exhibitor (create-only, all fields incl. contact + website + ContactPicker) | happy | P1 | _to author_ |
| E2E-EXH-003 | Edit an exhibitor (change contact details + toggle Active off) | happy | P1 | _to author_ |
| E2E-EXH-004 | Delete (soft-deactivate) confirmed via the SimfConfirm gate (D-353) | happy | P1 | _to author_ |
| E2E-EXH-005 | Cancel delete from the SimfConfirm gate (no-op) (D-353) | happy | P2 | _to author_ |
| E2E-EXH-006 | Read-only Details view (ViewDelete with IsDelete=false, no Deactivate button) | happy | P2 | _to author_ |
| E2E-EXH-007 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-EXH-008 | Auth gate (page) — admin lacking `Exhibitors.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-EXH-009 | Auth gate (action) — admin with View but not Create → POST 403 | auth | P1 | _to author_ |
| E2E-EXH-010 | Validation — blank name(s) → client-side bilingual toast, no POST | error | P1 | _to author_ |
| E2E-EXH-011 | Validation — server length rejection (400 `EXHIBITOR_INVALID`) | error | P1 | _to author_ |
| E2E-EXH-012 | Conflict — inactive exhibitor blocks account provisioning (409 `EXHIBITOR_INACTIVE`) | error | P1 | _to author_ |
| E2E-EXH-013 | Server 500 on `/list` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-EXH-014 | Form Cancel discards edits (no PUT) | happy | P2 | _to author_ |
| E2E-EXH-015 | RTL/Arabic render — page + form mirror | i18n | P1 | _to author_ |
| E2E-EXH-016 | Per-column filter narrows the grid (Name (English) / Name (Arabic)) | happy | P1 | _to author_ |
| E2E-EXH-017 | Column sort toggles (Name (English) asc → desc) | happy | P2 | _to author_ |
| E2E-EXH-018 | Presentation toggle persists across reload (Page ↔ Popup) (D-353) | happy | P1 | _to author_ |
| E2E-EXH-019 | Full-page mode round-trip — Add/Edit/View take over the content area (D-353) | happy | P1 | _to author_ |
| E2E-EXH-020 | Account provisioning sub-flow — list + provision a Visitor account | happy | P1 | _to author_ |
| E2E-EXH-021 | Excel export — toolbar Export → POST /export (whole grid vs selected rows + header row) (D-356) | happy | P1 | _to author_ |
| E2E-EXH-022 | Excel import — Import → workbook → result modal "N created…" + per-row error (D-356) | happy | P1 | _to author_ |
| E2E-EXH-023 | Excel import rejection — non-.xlsx / wrong-sheet → 400 + bilingual toast, nothing created (D-356) | error | P1 | _to author_ |

## Scenarios

### E2E-EXH-001 — Full CRUD round-trip

```gherkin
Feature: Exhibitors CRUD round-trip
  As an Administrator
  I want to manage the exhibitor directory (CP-only exhibitor records, D-199 #3)
  So that exhibitor onboarding and per-exhibitor account provisioning stay accurate

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with the Exhibitors.View/Create/Edit/Delete permissions has
      signed in via /login + /login/totp (TOTP from the Get-Totp helper)
  And they have landed on /admin/exhibitors
  And the grid is rendered (or the SimfEmptyState if there are no exhibitors)

Scenario: Create, edit, then deactivate one exhibitor
  Given the grid currently shows {N} rows
  When the administrator clicks the grid toolbar "Add" action
  Then the CrudShell opens hosting ExhibitorsAddEdit with the title "Add exhibitor"
       and fields: Name (English), Name (Arabic), Contact email, Contact phone,
       Website, and a Contact picker (the "Active" checkbox is hidden on Add)
  When they fill Name (English) = "Naval Defence Systems"
  And they fill Name (Arabic) = "أنظمة الدفاع البحري"
  And they fill Contact email = "info@navaldefence.example"
  And they fill Contact phone = "+966112223333"
  And they fill Website = "https://navaldefence.example"
  And they click "Save"
  Then the BFF fires POST /account/api/admin/exhibitors and the API returns HTTP 200
  And the CrudShell closes
  And a green toast reads "Exhibitor saved." / "تم حفظ العارض."
  And a row exists with Name (English) = "Naval Defence Systems",
      Name (Arabic) = "أنظمة الدفاع البحري", Accounts = 0, Active = (on pill)
  And the grid summary reads "Showing 1–{N+1} of {N+1}"

  When the administrator clicks the row's Edit (pencil) action
  Then the page first GETs /account/api/admin/exhibitors/{id} to load the full detail
  And the CrudShell opens hosting ExhibitorsAddEdit with the title "Edit exhibitor"
      and the row's values pre-filled, and the "Active" checkbox visible and ticked
  When they change Contact phone to "+966114445555"
  And they click "Save"
  Then the BFF fires PUT /account/api/admin/exhibitors/{id} and the API returns HTTP 200
  And the CrudShell closes
  And a green toast reads "Exhibitor saved." / "تم حفظ العارض."

  When the administrator clicks the row's Delete (trash) action
  Then the page GETs /account/api/admin/exhibitors/{id} and the CrudShell opens hosting
       ExhibitorsViewDelete (IsDelete=true) with the read-only details and a red
       "Deactivate" button (D-353: there is no inline native window.confirm)
  When they click the red "Deactivate" button
  Then a SimfConfirm dialog appears (Danger=true) titled "Deactivate exhibitor" with the
       message Admin.Exhibitors.Delete.Message formatted with the exhibitor's English name
  When they click the confirm "Deactivate"
  Then the BFF fires DELETE /account/api/admin/exhibitors/{id} and the API returns HTTP 200
  And the CrudShell closes
  And a green toast reads "Exhibitor deleted." / "تم حذف العارض."
  And the grid reloads (the soft-deactivated row shows the "off" pill until an isActive
      filter excludes inactive rows)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-exhibitors-001-before.png`
- Screenshot after create: `docs/screenshots/cp-admin-exhibitors-001-after-create.png`
- Screenshot after edit: `docs/screenshots/cp-admin-exhibitors-001-after-edit.png`
- Screenshot after delete: `docs/screenshots/cp-admin-exhibitors-001-after-delete.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/exhibitors/*` call returns 200
- Audit rows: `OperationLog` rows with `Event = 'Exhibitor.Created'`, `'Exhibitor.Updated'`,
  and `'Exhibitor.Deactivated'`, each carrying the actor's id (the
  `superadmin@zagali-ict.com` user id)

### E2E-EXH-002 — Add an exhibitor (create-only, all fields)

```gherkin
Scenario: Create an exhibitor with every field populated, including a Contact link
  Given the administrator is on /admin/exhibitors
  And at least one active Contact exists in the shared Contact directory
  When they click the grid toolbar "Add" action
  And they fill Name (English) = "Maritime Robotics Co"
  And they fill Name (Arabic) = "شركة الروبوتات البحرية"
  And they fill Contact email = "hello@maritimerobotics.example"
  And they fill Contact phone = "+966500000001"
  And they fill Website = "https://maritimerobotics.example"
  And they pick an existing contact from the ContactPicker (sets ContactId)
  And they click "Save"
  Then POST /account/api/admin/exhibitors returns HTTP 200
  And the CrudShell closes
  And a green toast reads "Exhibitor saved." / "تم حفظ العارض."
  And a new grid row shows Name (English) = "Maritime Robotics Co", Accounts = 0,
      Active = (on pill)
  And note: the create endpoint sets IsActive = true server-side; the "Active"
      checkbox is only rendered in Edit, not in Add (the AddEdit form hides it when !IsEdit)
```

### E2E-EXH-003 — Edit an exhibitor (change contact + deactivate)

```gherkin
Scenario: Update contact details and deactivate via the Edit form
  Given an exhibitor "Maritime Robotics Co" exists and is Active
  When the administrator clicks the "Maritime Robotics Co" row's Edit (pencil) action
  Then the CrudShell opens hosting ExhibitorsAddEdit titled "Edit exhibitor" with fields pre-filled
  When they change Contact email = "ops@maritimerobotics.example"
  And they untick the "Active" checkbox
  And they click "Save"
  Then PUT /account/api/admin/exhibitors/{id} returns HTTP 200
  And a green toast reads "Exhibitor saved." / "تم حفظ العارض."
  And the row's Active column now shows the "off" pill
  And an OperationLog row with Event='Exhibitor.Updated' records the actor id
```

### E2E-EXH-004 — Delete (soft-deactivate) confirmed

```gherkin
Scenario: Delete an exhibitor and confirm via SimfConfirm
  Given an exhibitor "Maritime Robotics Co" exists and is Active
  When the administrator clicks the row's Delete (trash) action
  Then the CrudShell opens hosting ExhibitorsViewDelete (IsDelete=true) with a red
       "Deactivate" button (D-353)
  When they click "Deactivate" and then confirm in the SimfConfirm dialog
  Then DELETE /account/api/admin/exhibitors/{id} returns HTTP 200
  And a green toast reads "Exhibitor deleted." / "تم حذف العارض."
  And the grid reloads with the row's Active column now the "off" pill
  And an OperationLog row with Event='Exhibitor.Deactivated' records the actor id
  And note: DeactivateAsync is idempotent — deactivating an already-inactive exhibitor
      returns early and writes no second audit row
```

### E2E-EXH-005 — Cancel delete (no-op)

```gherkin
Scenario: Dismiss the SimfConfirm delete gate
  Given an exhibitor "Maritime Robotics Co" exists and is Active
  When the administrator clicks the row's Delete (trash) action
  And the ExhibitorsViewDelete form opens and they click "Deactivate"
  And they dismiss (Cancel) the SimfConfirm dialog (D-353; no native window.confirm)
  Then no DELETE request fires
  And no toast appears
  And the "Maritime Robotics Co" row is unchanged (still the "on" pill)
```

### E2E-EXH-006 — Read-only Details view

```gherkin
Scenario: Details opens the ViewDelete form read-only (no Deactivate button)
  Given an exhibitor "Naval Defence Systems" exists
  When the administrator clicks the row's Details action
  Then the page GETs /account/api/admin/exhibitors/{id}
  And the CrudShell opens hosting ExhibitorsViewDelete (IsDelete=false) titled "Exhibitor details"
  And a definition list shows Name (English), Name (Arabic), Contact email, Contact phone,
      Website and Active (empty optional fields render as "—")
  And there is NO red "Deactivate" button (it only renders when IsDelete=true)
  When they click "Close"
  Then the form closes and the grid re-appears unchanged
```

### E2E-EXH-007 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no Exhibitor rows
  When the administrator opens /admin/exhibitors
  Then the grid body renders the SimfEmptyState component
  And the empty state title reads "No exhibitors yet." / "لا يوجد عارضون بعد."
  And the grid toolbar still shows the "Add" action
  And no error toast appears
```

### E2E-EXH-008 — Auth gate (page level)

```gherkin
Scenario: Admin lacking Exhibitors.View is denied the page
  Given a signed-in admin whose role does NOT include the Exhibitors.View permission
        (and is not the Administrator wildcard "*")
  When they navigate to /admin/exhibitors
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/exhibitors/list request fires
```

### E2E-EXH-009 — Auth gate (action level)

```gherkin
Scenario: Admin with View but without Create cannot create
  Given a signed-in admin whose role includes Exhibitors.View but NOT Exhibitors.Create
  And they have opened /admin/exhibitors (the page renders, "Add" is visible
      because the toolbar action is not individually gated in the CP UI)
  When they fill the Add form and click "Save"
  Then the BFF forwards POST /admin/exhibitors
  And the API rejects it with HTTP 403 (the Exhibitors.Create policy is not satisfied)
  And the form stays open with the bilingual error surfaced from the envelope
  And note: the integration test Non_admin_caller_is_forbidden_from_export
      (ExhibitorsExcelTests) covers the same per-action gate on the export endpoint
```

### E2E-EXH-010 — Client-side name validation

```gherkin
Scenario: Blank name shows a bilingual error and suppresses the POST
  Given the Add form (ExhibitorsAddEdit) is open
  When the administrator leaves Name (English) and/or Name (Arabic) blank
  And clicks "Save"
  Then a SimfAlert error appears inside the form reading
       "Both the English and Arabic names are required."
       / "الاسم بالإنجليزية والعربية كلاهما مطلوب."
       (the Admin.Exhibitors.NameRequired key)
  And the form stays open
  And NO POST /account/api/admin/exhibitors request fires (guarded client-side in SaveAsync)
```

### E2E-EXH-011 — Server-side validation rejection

```gherkin
Scenario: Over-length value is rejected by the API with 400 EXHIBITOR_INVALID
  Given the Add form is open with a valid Name (English) and Name (Arabic)
  When the administrator submits a value the API rejects, e.g.:
       a Name (English) or Name (Arabic) longer than 256 characters, or
       a Contact email longer than 320 characters, or
       a Contact phone longer than 32 characters, or
       a Website longer than 512 characters
  And clicks "Save"
  Then POST /account/api/admin/exhibitors returns HTTP 400
  And ApiResult.Error.Code = "EXHIBITOR_INVALID"
  And the form stays open
  And the error surfaces the bilingual MessageForCurrentCulture(), e.g.
      "Exhibitor name (EN + AR) must be between 1 and 256 characters." /
      "يجب أن يتراوح طول اسم العارض (إنجليزي + عربي) بين 1 و 256 حرفاً."
  And note: linking a non-existent / inactive Contact also returns 400 EXHIBITOR_INVALID
      ("Contact id '…' does not exist or is inactive.")
```

### E2E-EXH-012 — Conflict (inactive exhibitor blocks provisioning)

```gherkin
Scenario: Provisioning an account under an inactive exhibitor returns 409
  Given an exhibitor "Dormant Exhibitor" exists but has been deactivated (IsActive = false)
  When the administrator opens its per-row "Accounts" sub-flow
  And fills Contact name = "Jane Doe" and Email = "jane@dormant.example"
  And clicks "Provision account"
  Then the BFF forwards POST /admin/exhibitors/{id}/accounts
  And the API returns HTTP 409 with ApiResult.Error.Code = "EXHIBITOR_INACTIVE"
  And the error surfaces the bilingual message
      "The exhibitor is not active; reactivate it before adding accounts." /
      "العارض غير نشط؛ يرجى إعادة تفعيله قبل إضافة الحسابات."
  And note: a duplicate / already-registered account email surfaces from the reused admin
      provisioning pipeline (CreateVisitorAsync) as its own ApiException, and an invalid
      contact name/email length returns 400 EXHIBITOR_ACCOUNT_INVALID
```

### E2E-EXH-013 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/exhibitors/list (e.g. DB down)
  When the administrator opens /admin/exhibitors
  Then the page first shows "Loading exhibitors…" / (the Admin.Exhibitors.Loading key)
  And then a red toast appears reading
       "Could not load exhibitors. Please try again." /
       "تعذر تحميل العارضين. حاول مرة أخرى."
       (the Admin.Exhibitors.LoadFailed key)
  And no rows render
```

### E2E-EXH-014 — Form Cancel discards edits

```gherkin
Scenario: Cancel closes the form without persisting
  Given the administrator has opened the Edit form for "Naval Defence Systems"
  When they change Contact phone to "+966119998888"
  And they click "Cancel"
  Then the CrudShell closes
  And NO PUT request fires
  And the "Naval Defence Systems" row is unchanged in the grid
```

### E2E-EXH-015 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add form
  Given the administrator is on /admin/exhibitors in English
  When they switch the UI language to Arabic ("العربية")
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "العارضون"
  And the grid column headers read
      "الاسم (بالإنجليزية)", "الاسم (بالعربية)", "الحسابات", "نشط"
  And the grid toolbar "Add" action is shown (label from the shared Grid.Add resx key)

  When they click the grid toolbar "Add" action
  Then the CrudShell opens in RTL with title "إضافة عارض"
  And the field labels read "الاسم (بالإنجليزية)", "الاسم (بالعربية)",
      "البريد الإلكتروني للتواصل", "هاتف التواصل", "الموقع الإلكتروني"
  And the Save / Cancel buttons read "حفظ" (Save) and "إلغاء" (Cancel)
```

### E2E-EXH-016 — Per-column filter narrows the grid

```gherkin
Scenario: Typing into a per-column grid filter narrows the rows
  Given exhibitors exist with varied names, e.g. "Naval Defence Systems",
        "Maritime Robotics Co" and "Coastal Sensors Ltd"
  And the administrator is on /admin/exhibitors with the grid rendered
  When they open the column-filter control for the "Name (English)" column
  And they type "Naval" into the "Filter column Name (English)" input
  Then a POST /account/api/admin/exhibitors/list fires carrying
       GridQuery.Filters["nameEn"] = "Naval"
  And GridQuery.Skip is reset to 0 (paging returns to the first page)
  And the grid re-renders showing only rows whose Name (English) contains "Naval"
  And the grid summary updates to the narrowed total

  When they clear the "Name (English)" filter
  And instead type "بحري" into the "Filter column Name (Arabic)" input
  Then a POST /account/api/admin/exhibitors/list fires carrying
       GridQuery.Filters["nameAr"] = "بحري"
  And GridQuery.Skip is reset to 0
  And the grid shows only rows whose Name (Arabic) contains "بحري"
  And only the "Name (English)" and "Name (Arabic)" columns expose a per-column
      filter input (Accounts and Active are not Filterable; AccountCount is a computed
      sub-query and is not server-filterable)
```

### E2E-EXH-017 — Column sort toggles

```gherkin
Scenario: Clicking a sortable column header toggles ascending/descending
  Given exhibitors exist with varied names
  And the administrator is on /admin/exhibitors with the grid rendered
       (default order: Name (Arabic) ascending)
  When they click the "Name (English)" column header
  Then a POST /account/api/admin/exhibitors/list fires carrying
       GridQuery.Sort = "nameEn" and GridQuery.SortDescending = false
  And the grid re-renders ordered by Name (English) ascending

  When they click the "Name (English)" column header again
  Then a POST /account/api/admin/exhibitors/list fires carrying
       GridQuery.Sort = "nameEn" and GridQuery.SortDescending = true
  And the grid re-renders ordered by Name (English) descending
  And the sortable columns are exactly: Name (English), Name (Arabic) and Active
      (Accounts is not Sortable)
```

### E2E-EXH-018 — Presentation toggle persists across reload (D-353)

```gherkin
Scenario: Switch between Popup and full Page and the choice persists
  Given the administrator is on /admin/exhibitors with the default "dialog" (popup) presentation
  And the grid toolbar shows the CrudPresentationToggle (PageKey="exhibitors")
  When they click the toggle to choose "Open as full page"
  Then localStorage key "simf.cp.prefs.exhibitors" holds {"v":1,"presentation":"page"}
  When they reload /admin/exhibitors
  Then OnInitializedAsync calls Prefs.GetPresentationAsync("exhibitors") and reads back "page"
  And opening "Add" now renders the full-page CrudShell frame (not a popup with a backdrop)
  When they switch the toggle back to "Open as dialog"
  Then localStorage key "simf.cp.prefs.exhibitors" holds {"v":1,"presentation":"dialog"}
  And after a reload opening "Add" renders the popup dialog again
```

### E2E-EXH-019 — Full-page mode round-trip (D-353)

```gherkin
Scenario: In full-page mode the Add/Edit/View forms take over the content area
  Given the presentation for /admin/exhibitors is set to "page"
  When the administrator clicks the grid toolbar "Add" action
  Then the grid + SimfBanner are hidden (GridHidden = FormOpen && presentation == Page)
  And the CrudShell renders full-page with the title "Add exhibitor" and a "Close" header
  And there is no modal backdrop
  When they fill Name (English) = "Subsea Systems", Name (Arabic) = "أنظمة تحت البحر",
      and click "Save"
  Then POST /account/api/admin/exhibitors returns HTTP 200
  And the CrudShell closes and the grid + banner re-appear with the new row
  And a green toast reads "Exhibitor saved." / "تم حفظ العارض."
  When they click the row's Details action
  Then the ExhibitorsViewDelete form opens full-page in read-only mode (no Deactivate button)
      showing Name (English/Arabic), Contact email, Contact phone, Website and Active
  When they click the CrudShell close ("Close")
  Then the form closes and the grid re-appears unchanged
```

### E2E-EXH-020 — Account provisioning sub-flow

```gherkin
Scenario: List and provision a per-exhibitor login account
  Given an exhibitor "Naval Defence Systems" exists and is Active
  And the signed-in admin holds Exhibitors.Edit (the per-row "Accounts" icon is rendered
      because it is wrapped in <AuthorizedAction Permission="Exhibitors.Edit">)
  When the administrator clicks the row's "Accounts" (user) icon
  Then a SimfModal opens titled "Accounts — Naval Defence Systems"
       (this is a separate SimfModal overlay, independent of the CrudShell)
  And it GETs /account/api/admin/exhibitors/{id}/accounts
  And an info alert reads "A provisioned account is a pending-approval app login tagged to
      this exhibitor."
  And if no accounts exist yet the SimfEmptyState reads "No accounts provisioned yet."
  When they fill Contact name = "Captain Khalid", Email = "khalid@navaldefence.example",
      and Role label = "Booth lead"
  And they click "Provision account"
  Then POST /account/api/admin/exhibitors/{id}/accounts returns HTTP 200
  And a green toast reads "Account provisioned. It is pending approval." /
      (the Admin.Exhibitors.Provision.Done key)
  And the accounts table now lists the new row (Contact name / Email / Role / Active ✓)
  And the grid's "Accounts" column for that exhibitor increments by one (LoadAsync is re-run)
  And an OperationLog row with Event='Exhibitor.AccountProvisioned' records the actor id and
      the new SubjectUserId / SubjectEmail
  And note: the provisioned account is a least-privilege Visitor created in the
      pending-approval state through the existing admin provisioning pipeline
      (CreateVisitorAsync), linked by an ExhibitorMembership row (Data↔Identity stays
      separated: UserId is a logical FK resolved cross-context on read)

  When they click "Provision account" with the Contact name OR Email blank
  Then no POST fires and a red toast reads "The contact name and email are both required."
      (the Admin.Exhibitors.Provision.Required client guard)
```

### E2E-EXH-021 — Excel export (D-356)

```gherkin
Scenario: Export the filtered grid (or just selected rows) to an XLSX workbook
  Given the administrator is on /admin/exhibitors with at least two exhibitors
  When they click the toolbar "Export" action with no rows selected
  Then OnExportAsync calls _excel.ExportAsync(empty Ids, current Query)
  And a POST /account/api/admin/exhibitors/export fires carrying
      AdminGridExportRequest { Ids: [], Query: <current GridQuery> }
      (the Query is sent only because no rows are selected; with a selection Query is null)
  And the API caps the set at 5000 rows and returns an XLSX
      (Content-Type application/vnd.openxmlformats-officedocument.spreadsheetml.sheet)
  And the browser saves a file named simf-exhibitors-{yyyyMMddHHmmss}.xlsx
  And the workbook's "Exhibitors" sheet header row reads
      NameEn | NameAr | ContactEmail | ContactPhone | Website | AccountCount | IsActive
  When they instead select two rows then click "Export"
  Then OnExportAsync calls _excel.ExportAsync([id1, id2], current Query)
  And the export request carries those two Ids in AdminGridExportRequest.Ids and Query = null
  And the API lists the rows then filters to the wanted ids, so the workbook contains
      exactly those two rows
```

### E2E-EXH-022 — Excel import (D-356)

```gherkin
Scenario: Import exhibitors from a workbook and see the per-row outcome
  Given the administrator is on /admin/exhibitors
  When they click the toolbar "Import" action
  Then OnImportAsync calls _excel.TriggerImportAsync(), opening the file picker
      on the hidden <input id="exhibitors-import-input" accept=".xlsx">
  When they choose an .xlsx whose "Exhibitors" sheet has the required headers
      NameEn, NameAr and rows for two new exhibitors
      (e.g. "Thales Maritime"/"تاليس البحرية" and "Leonardo Sea"/"ليوناردو البحرية")
  Then a POST /account/api/admin/exhibitors/import fires as multipart form data (field "file")
  And the import-result modal (title "Import results") shows
      "2 created, 0 updated, 0 skipped." with an empty error list
  And a green toast reads the shared Grid.Import.Done key ("Import complete." / "اكتمل الاستيراد.")
  And OnImportedAsync reloads the grid (LoadAsync) so both new exhibitors appear
  When they import a workbook with one row missing NameEn (or NameAr)
  Then that row is reported in the modal's error list, formatted by the Grid.Import.RowError
      key "Row {n} ({NameEn}): Both the English and Arabic names are required." /
      "… الاسمان بالإنجليزية والعربية كلاهما مطلوبان." while the valid rows still create
      (one bad row never aborts the batch — the base catches the per-row DataValidationException)
  And note: import is insert-only (every applied row → Created) — AccountCount (read-only
      derived count), IsActive (a created exhibitor is always active) and the ContactId
      directory FK are intentionally NOT settable by import; the RowKey echoed in errors is
      the NameEn cell
```

### E2E-EXH-023 — Excel import rejection (D-356)

```gherkin
Scenario: A bad / wrong-sheet upload is rejected and nothing is created
  Given the administrator is on /admin/exhibitors
  When they import a file that is not a valid .xlsx (fails the ZIP-magic check 50 4B 03 04)
  Then POST /account/api/admin/exhibitors/import returns HTTP 400 (DataValidationException)
  And OnExcelError surfaces a red toast reading
       "The file is not a valid Excel workbook." / "الملف ليس مصنف Excel صالحًا."
  And no exhibitor is created
  When they import an .xlsx larger than 5 MB
  Then the request is rejected (HTTP 413, AdminImportEmpty) with
       "The Excel file is too large. The maximum is 5 MB." /
       "ملف Excel كبير جدًا. الحد الأقصى 5 ميغابايت."
  When they import a workbook whose worksheet is not named "Exhibitors" (or is missing one of
       the required headers NameEn / NameAr)
  Then the parse fails and the request returns 400 with the bilingual worksheet/header message
  And no exhibitor is created
```

---

## Implementation notes

- **Manual smoke is canonical today.** Until Playwright is adopted, the canonical
  "run" of these scenarios is a Chrome DevTools MCP session — sign in via the
  Background steps, walk each scenario, and capture screenshots into
  `docs/screenshots/cp-admin-exhibitors-*.png`.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin scenario
  into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) plus
  a step-definition class. The Gherkin shape is already runner-agnostic.
- **API integration tests** cover the same surface at a lower layer (no browser):
  - `tests/SIMF.Api.Tests/ExhibitorsTests.cs` — the CRUD + account-provisioning endpoints.
  - `tests/SIMF.Api.Tests/ExhibitorsExcelTests.cs` (D-356 Excel engine):
    - `Export_returns_an_xlsx_workbook` (E2E-EXH-021 at API layer; asserts ZIP magic)
    - `Import_creates_each_row_and_reports_the_outcome` (E2E-EXH-022; created rows then list)
    - `Import_reports_a_per_row_error_for_a_blank_name_without_aborting` (E2E-EXH-022 error path)
    - `Non_admin_caller_is_forbidden_from_export` (E2E-EXH-009 at API layer)
  When an E2E scenario reliably covers one of these, the matching `Api.Tests` case
  can usually be retired — but keep both during the transition.
- **Backing endpoints / error codes** (grounded in
  `src/Backend/SIMF.Api/Endpoints/Exhibitors/ExhibitorEndpoints.cs`,
  `src/Backend/SIMF.Api/Endpoints/Admin/ExhibitorsExcelEndpoints.cs` and
  `src/Backend/SIMF.Infrastructure/Exhibitors/AdminExhibitorService.cs`):
  - `POST /admin/exhibitors/list` — policy `Exhibitors.View`
  - `GET /admin/exhibitors/{id}` — policy `Exhibitors.View`
  - `POST /admin/exhibitors` — policy `Exhibitors.Create`, rate-limited "auth"
  - `PUT /admin/exhibitors/{id}` — policy `Exhibitors.Edit`, rate-limited "auth"
  - `DELETE /admin/exhibitors/{id}` — policy `Exhibitors.Delete` (soft-deactivate), rate-limited "auth"
  - `GET /admin/exhibitors/{id}/accounts` — policy `Exhibitors.View`
  - `POST /admin/exhibitors/{id}/accounts` — policy `Exhibitors.Create`, rate-limited "auth"
  - `POST /admin/exhibitors/export` — policy `Exhibitors.Export`, rate-limited "auth"; columns
    NameEn, NameAr, ContactEmail, ContactPhone, Website, AccountCount, IsActive; sheet
    "Exhibitors"; file `simf-exhibitors-{ts}.xlsx`; 5000-row cap
    (`ExportExhibitorsEndpoint` over `AdminGridExportEndpoint<AdminExhibitorSummary>`)
  - `POST /admin/exhibitors/import` — policy `Exhibitors.Import`, rate-limited "auth"; multipart
    "file"; required headers NameEn/NameAr; insert-only; AccountCount/IsActive/ContactId omitted;
    5 MB + ZIP-magic upload gate (400/`AdminImportEmpty` for >5 MB → 413), 5000-row cap; blank
    name = per-row error (`ImportExhibitorsEndpoint` over `AdminGridImportEndpoint`)
  - Error codes: `EXHIBITOR_INVALID` (400), `EXHIBITOR_NOT_FOUND` (404),
    `EXHIBITOR_INACTIVE` (409 — provisioning under an inactive exhibitor),
    `EXHIBITOR_ACCOUNT_INVALID` (400 — bad contact name/email/role length)
  - Field limits: NameEn/NameAr 1–256, ContactEmail ≤320, ContactPhone ≤32, Website ≤512;
    provisioning ContactName 1–256, Email 1–320, RoleLabel ≤128
  - Audit events: `Exhibitor.Created`, `Exhibitor.Updated`, `Exhibitor.Deactivated`,
    `Exhibitor.AccountProvisioned`
- **CP page note (D-353/D-356).** The page is on the uniform CRUD shell: Add/Edit
  (`ExhibitorsAddEdit`), View/Delete and the read-only Details view (`ExhibitorsViewDelete`)
  are all hosted by `CrudShell`, framed as a popup or a full page per the toolbar
  `CrudPresentationToggle` (PageKey `"exhibitors"`, persisted in localStorage
  `simf.cp.prefs.exhibitors`). Delete is gated by an in-form `SimfConfirm` (no native
  `window.confirm`, so no `handle_dialog` step) — see E2E-EXH-004/005. A read-only **Details**
  view exists (E2E-EXH-006) via the same ViewDelete form with `IsDelete=false`. D-356 added
  Excel **export + import** through `CrudGridExcel` (`Resource="exhibitors"`): export
  `POST /admin/exhibitors/export`, import `POST /admin/exhibitors/import` (insert-only, 5 MB +
  ZIP-magic gate, 5000-row cap) — see E2E-EXH-021..023. The per-row **Accounts** provisioning
  icon is the only `<AuthorizedAction>`-gated UI affordance (`Exhibitors.Edit`) and opens its
  own `SimfModal`, independent of the CrudShell (E2E-EXH-020); the CRUD action buttons are not
  individually gated in the CP — per-action enforcement is API-side only (E2E-EXH-009).

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5).
