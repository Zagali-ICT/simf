# E2E test catalogue — Sponsors CRUD (`/admin/sponsors`)

| | |
|--|--|
| **Page** | [`cp/admin-sponsors.md`](../../pages/cp/admin-sponsors.md) |
| **Route** | `/admin/sponsors` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **Page permission:** the page is gated by `@attribute [RequirePermission(PermissionCatalog.Sponsors.View)]`.
> The action buttons (Add / Edit / Delete) are **not** wrapped in `<AuthorizedAction>` on
> this page, so any admin who can open it sees all three buttons — but the BFF/API enforce
> the finer-grained `Sponsors.Create` / `Sponsors.Edit` / `Sponsors.Delete` policies on the
> underlying endpoints (`POST /admin/sponsors`, `PUT /admin/sponsors/{id}`, `DELETE
> /admin/sponsors/{id}`). E2E-SPN-009 covers the per-action API gate.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-SPN-001 | Full CRUD round-trip — Add → Edit → Delete (deactivate) | happy | P0 | _to author_ |
| E2E-SPN-002 | Add a sponsor (create-only, all fields incl. logo/url/order) | happy | P1 | _to author_ |
| E2E-SPN-003 | Edit a sponsor (change tier + toggle Active off) | happy | P1 | _to author_ |
| E2E-SPN-004 | Delete (soft-deactivate) confirmed via the SimfConfirm gate (D-353; superseded by E2E-SPN-020) | happy | P1 | _to author_ |
| E2E-SPN-005 | Cancel delete from the SimfConfirm gate (no-op) (D-353; superseded by E2E-SPN-020) | happy | P2 | _to author_ |
| E2E-SPN-006 | Tier dropdown carries all four tiers + grid ordering | happy | P2 | _to author_ |
| E2E-SPN-007 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-SPN-008 | Auth gate (page) — admin lacking `Sponsors.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-SPN-009 | Auth gate (action) — admin with View but not Create → POST 403 | auth | P1 | _to author_ |
| E2E-SPN-010 | Validation — blank name(s) → client-side bilingual toast, no POST | error | P1 | _to author_ |
| E2E-SPN-011 | Validation — server length/tier/order rejection (400 `SponsorInvalid`) | error | P1 | _to author_ |
| E2E-SPN-012 | Conflict — duplicate active NameAr in same tier → 409 `SponsorDuplicate` | error | P1 | _to author_ |
| E2E-SPN-013 | Server 500 on `/list` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-SPN-014 | Modal Cancel discards edits | happy | P2 | _to author_ |
| E2E-SPN-015 | RTL/Arabic render — page + modal mirror | i18n | P1 | _to author_ |
| E2E-SPN-016 | Per-column filter narrows the grid (Name (English) / Name (Arabic)) | happy | P1 | _to author_ |
| E2E-SPN-017 | Column sort toggles (Tier asc → desc) | happy | P2 | _to author_ |
| E2E-SPN-018 | Presentation toggle persists across reload (Page ↔ Popup) (D-353) | happy | P1 | _to author_ |
| E2E-SPN-019 | Full-page mode round-trip — Add/Edit/View take over the content area (D-353) | happy | P1 | _to author_ |
| E2E-SPN-020 | Delete confirmation gate — ViewDelete + SimfConfirm (not native confirm) (D-353) | error | P0 | _to author_ |
| E2E-SPN-021 | Excel export — toolbar Export → POST /export (whole grid vs selected rows) (D-356) | happy | P1 | _to author_ |
| E2E-SPN-022 | Excel import — Import → workbook → result modal "N created…" + per-row error (D-356) | happy | P1 | _to author_ |
| E2E-SPN-023 | Excel import rejection — non-.xlsx / wrong-sheet → 400 + bilingual toast, nothing created (D-356) | error | P1 | _to author_ |
| E2E-SPN-024 | Logo via the unified media-asset pipeline — upload then external link (D-357) | happy | P1 | _to author_ |
| E2E-SPN-025 | Edit preserves the bilingual tagline — regression (D-501) | error | P0 | _to author_ |
| E2E-SPN-026 | Excel export/import round-trips Tagline/TaglineArabic + About/AboutArabic (D-502) | happy | P1 | _to author_ |

## Scenarios

### E2E-SPN-001 — Full CRUD round-trip

```gherkin
Feature: Sponsors CRUD round-trip
  As an Administrator
  I want to manage the public sponsors list (logos grouped by tier)
  So that the website sponsors screen (Mockup page 23) stays accurate

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with the Sponsors.View/Create/Edit/Delete permissions has
      signed in via /login + /login/totp (TOTP from the Get-Totp helper)
  And they have landed on /admin/sponsors
  And the grid is rendered (or the SimfEmptyState if there are no sponsors)

Scenario: Create, edit, then delete one sponsor
  Given the grid currently shows {N} rows
  When the administrator clicks the grid toolbar "Add" action
  Then the "Add sponsor" modal opens with fields:
       Name (English), Name (Arabic), Tier (select), Link, Logo path,
       Display order (number), and an "Active" checkbox (ticked)
  When they fill Name (English) = "Lockheed Martin"
  And they fill Name (Arabic) = "لوكهيد مارتن"
  And they select Tier = "Platinum"
  And they fill Link = "https://www.lockheedmartin.com"
  And they fill Logo path = "sponsors/lockheed.png"
  And they fill Display order = "10"
  And they click "Save"
  Then the BFF fires POST /account/api/admin/sponsors and the API returns HTTP 200
  And the modal closes
  And a green toast reads "Sponsor saved." / "تم حفظ الراعي."
  And a row exists with Name (English) = "Lockheed Martin",
      Tier = "Platinum", Logo path = "sponsors/lockheed.png",
      Link = "https://www.lockheedmartin.com", Display order = 10, Active = "✓"
  And the grid summary reads "Showing 1–{N+1} of {N+1}"

  When the administrator clicks the row's Edit (pencil) action
  Then the "Edit sponsor" modal opens with the row's values pre-filled
  And the "Active" checkbox is visible and ticked
  When they change Display order to "20"
  And they click "Save"
  Then the BFF fires PUT /account/api/admin/sponsors/{id} and the API returns HTTP 200
  And the modal closes
  And a green toast reads "Sponsor saved." / "تم حفظ الراعي."
  And the row's Display order column now reads "20"

  When the administrator clicks the row's Delete (trash) action
  Then the SponsorsViewDelete form opens (CrudShell popup or full page per the toggle) with a
       red "Delete" button (D-353: the inline native window.confirm was removed)
  When they click the red "Delete" button
  Then a SimfConfirm dialog appears naming the sponsor (Admin.Sponsors.Delete.Message)
  When they click the confirm "Delete"
  Then the BFF fires DELETE /account/api/admin/sponsors/{id} and the API returns HTTP 200
  And a green toast reads "Sponsor deleted." / "تم حذف الراعي."
  And the grid reloads
  And the "Lockheed Martin" row no longer appears (it was soft-deactivated; the
      list query orders by Tier, DisplayOrder, NameAr and re-renders without it
      only when an isActive filter excludes inactive rows — without that filter the
      row still shows with Active = "—")
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-sponsors-001-before.png`
- Screenshot after create: `docs/screenshots/cp-admin-sponsors-001-after-create.png`
- Screenshot after edit: `docs/screenshots/cp-admin-sponsors-001-after-edit.png`
- Screenshot after delete: `docs/screenshots/cp-admin-sponsors-001-after-delete.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/sponsors/*` call returns 200
- Audit rows: `OperationLog` rows with `Event = 'Sponsor.Created'`, `'Sponsor.Updated'`,
  and `'Sponsor.Deactivated'`, each carrying the actor's id (the
  `superadmin@zagali-ict.com` user id)

### E2E-SPN-002 — Add a sponsor (create-only)

```gherkin
Scenario: Create a sponsor with every field populated
  Given the administrator is on /admin/sponsors
  When they click "Add sponsor"
  And they fill Name (English) = "Saab"
  And they fill Name (Arabic) = "ساب"
  And they select Tier = "Gold"
  And they fill Link = "https://www.saab.com"
  And they fill Logo path = "sponsors/saab.svg"
  And they fill Display order = "5"
  And they leave the "Active" checkbox ticked
  And they click "Save"
  Then POST /account/api/admin/sponsors returns HTTP 200
  And the modal closes
  And a green toast reads "Sponsor saved." / "تم حفظ الراعي."
  And a new grid row shows NameEn="Saab", Tier="Gold", Display order=5, Active="✓"
```

### E2E-SPN-003 — Edit a sponsor (change tier + deactivate)

```gherkin
Scenario: Re-tier and deactivate via the Edit modal
  Given a sponsor "Saab" exists in tier "Gold" and is Active
  When the administrator clicks the "Saab" row's Edit (pencil) action
  Then the modal is titled "Edit sponsor" and the fields are pre-filled
  When they change Tier = "Silver"
  And they untick the "Active" checkbox
  And they click "Save"
  Then PUT /account/api/admin/sponsors/{id} returns HTTP 200
  And a green toast reads "Sponsor saved." / "تم حفظ الراعي."
  And the row now shows Tier="Silver" and Active="—"
  And an OperationLog row with Event='Sponsor.Updated' records the actor id
```

### E2E-SPN-004 — Delete (soft-deactivate) confirmed

```gherkin
Scenario: Delete a sponsor and confirm via SimfConfirm
  Given a sponsor "Saab" exists and is Active
  When the administrator clicks the "Saab" row's Delete (trash) action
  Then the SponsorsViewDelete form opens with a red "Delete" button (D-353)
  When they click "Delete" and then confirm in the SimfConfirm dialog
  Then DELETE /account/api/admin/sponsors/{id} returns HTTP 200
  And a green toast reads "Sponsor deleted." / "تم حذف الراعي."
  And the grid reloads with the row's Active column now "—"
  And an OperationLog row with Event='Sponsor.Deactivated' records the actor id
```

### E2E-SPN-005 — Cancel delete (no-op)

```gherkin
Scenario: Dismiss the SimfConfirm delete gate
  Given a sponsor "Saab" exists and is Active
  When the administrator clicks the "Saab" row's Delete (trash) action
  And the SponsorsViewDelete form opens and they click "Delete"
  And they dismiss (Cancel) the SimfConfirm dialog (D-353; no native window.confirm)
  Then no DELETE request fires
  And no toast appears
  And the "Saab" row is unchanged (still Active="✓")
```

### E2E-SPN-006 — Tier dropdown + grid ordering

```gherkin
Scenario: Tier picker offers all four tiers and the grid orders by tier
  Given the administrator opens the "Add sponsor" modal
  Then the Tier select lists exactly: "Platinum", "Gold", "Silver", "Bronze"
  And "Platinum" is the default selection
  When sponsors exist across more than one tier
  Then the grid renders them ordered by Tier (Platinum→Gold→Silver→Bronze),
       then Display order ascending, then Name (Arabic) ascending
       (the API ListAllAsync OrderBy Tier, DisplayOrder, NameAr)
```

### E2E-SPN-007 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no Sponsor rows
  When the administrator opens /admin/sponsors
  Then the grid body renders the SimfEmptyState component
  And the empty state title reads "No sponsors yet." / "لا يوجد رعاة بعد."
  And the grid toolbar still shows the "Add" action
  And no error toast appears
```

### E2E-SPN-008 — Auth gate (page level)

```gherkin
Scenario: Admin lacking Sponsors.View is denied the page
  Given a signed-in admin whose role does NOT include the Sponsors.View permission
        (and is not the Administrator wildcard "*")
  When they navigate to /admin/sponsors
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/sponsors/list request fires
```

### E2E-SPN-009 — Auth gate (action level)

```gherkin
Scenario: Admin with View but without Create cannot create
  Given a signed-in admin whose role includes Sponsors.View but NOT Sponsors.Create
  And they have opened /admin/sponsors (the page renders, "Add sponsor" is visible
      because the button is not individually gated in the CP UI)
  When they fill the Add modal and click "Save"
  Then the BFF forwards POST /admin/sponsors
  And the API rejects it with HTTP 403 (the Sponsors.Create policy is not satisfied)
  And the modal stays open with the bilingual error toast surfaced from the envelope
```

### E2E-SPN-010 — Client-side name validation

```gherkin
Scenario: Blank name shows a bilingual toast and suppresses the POST
  Given the "Add sponsor" modal is open
  When the administrator leaves Name (English) and/or Name (Arabic) blank
  And clicks "Save"
  Then a SimfAlert error toast appears reading
       "Both the English and Arabic names are required."
       / "الاسم بالإنجليزية والعربية مطلوبان."
  And the modal stays open
  And NO POST /account/api/admin/sponsors request fires (guarded client-side in SaveAsync)
```

### E2E-SPN-011 — Server-side validation rejection

```gherkin
Scenario: Over-length / bad-tier / negative-order is rejected by the API with 400
  Given the "Add sponsor" modal is open with a valid Name (English) and Name (Arabic)
  When the administrator submits a value the API rejects, e.g.:
       a Logo path longer than 256 characters, or
       a Link longer than 512 characters, or
       a Display order below 0
  And clicks "Save"
  Then POST /account/api/admin/sponsors returns HTTP 400
  And ApiResult.Error.Code = "SponsorInvalid"
  And the modal stays open
  And the error toast surfaces the bilingual MessageForCurrentCulture(), e.g.
      "Logo path must be 256 characters or fewer." /
      "يجب أن يكون مسار الشعار 256 حرفاً أو أقل."
```

### E2E-SPN-012 — Duplicate (conflict)

```gherkin
Scenario: Duplicate active Arabic name in the same tier returns 409
  Given an active sponsor with Name (Arabic) = "ساب" exists in tier "Gold"
  When the administrator opens "Add sponsor"
  And fills Name (English) = "Saab Duplicate"
  And fills Name (Arabic) = "ساب"
  And selects Tier = "Gold"
  And clicks "Save"
  Then the BFF forwards POST /admin/sponsors
  And the API returns HTTP 409 with ApiResult.Error.Code = "SponsorDuplicate"
  And the modal stays open
  And the error toast surfaces the bilingual message, e.g.
      "An active sponsor named 'ساب' already exists in this tier." /
      "يوجد راعٍ نشط بالاسم 'ساب' في هذه الفئة بالفعل."
  And the same Arabic name in a DIFFERENT tier (e.g. "Silver") would NOT conflict
```

### E2E-SPN-013 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/sponsors/list (e.g. DB down)
  When the administrator opens /admin/sponsors
  Then the page first shows "Loading sponsors…" / "جارٍ تحميل الرعاة…"
  And then a red toast appears reading
       "Could not load sponsors. Please try again." /
       "تعذّر تحميل الرعاة. يُرجى المحاولة مرة أخرى."
  And no rows render
```

### E2E-SPN-014 — Modal Cancel discards edits

```gherkin
Scenario: Cancel closes the modal without persisting
  Given the administrator has opened the "Edit sponsor" modal for "Saab"
  When they change Display order to "99"
  And they click "Cancel"
  Then the modal closes
  And NO PUT request fires
  And the "Saab" row's Display order is unchanged in the grid
```

### E2E-SPN-015 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/sponsors in English
  When they switch the UI language to Arabic ("العربية")
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "الرعاة"
  And the grid column headers read
      "الاسم (إنجليزي)", "الاسم (عربي)", "الفئة", "مسار الشعار",
      "الرابط", "ترتيب العرض", "نشط"
  And the grid toolbar "Add" action is shown (label from the shared Grid.Add resx key)

  When they click the grid toolbar "Add" action
  Then the Add modal opens in RTL with title "إضافة راعٍ"
  And the field labels read "الاسم (إنجليزي)", "الاسم (عربي)", "الفئة",
      "الرابط", "مسار الشعار", "ترتيب العرض", "نشط"
  And the footer buttons read "إلغاء" (Cancel) and "حفظ" (Save)
```

### E2E-SPN-016 — Per-column filter narrows the grid

```gherkin
Scenario: Typing into a per-column grid filter narrows the rows
  Given sponsors exist with varied names, e.g. "Saab", "Lockheed Martin" and "Naval Group"
  And the administrator is on /admin/sponsors with the grid rendered
  When they open the column-filter control for the "Name (English)" column
  And they type "Saab" into the "Filter column Name (English)" input
  Then a POST /account/api/admin/sponsors/list fires carrying
       GridQuery.Filters["nameEn"] = "Saab"
  And GridQuery.Skip is reset to 0 (paging returns to the first page)
  And the grid re-renders showing only rows whose Name (English) contains "Saab"
  And the grid summary updates to the narrowed total

  When they clear the "Name (English)" filter
  And instead type "ساب" into the "Filter column Name (Arabic)" input
  Then a POST /account/api/admin/sponsors/list fires carrying
       GridQuery.Filters["nameAr"] = "ساب"
  And GridQuery.Skip is reset to 0
  And the grid shows only rows whose Name (Arabic) contains "ساب"
  And only the "Name (English)" and "Name (Arabic)" columns expose a per-column
      filter input (Tier, Logo path, Link, Display order and Active are not Filterable)
```

### E2E-SPN-017 — Column sort toggles

```gherkin
Scenario: Clicking a sortable column header toggles ascending/descending
  Given sponsors exist across more than one tier
  And the administrator is on /admin/sponsors with the grid rendered
       (default order: Tier asc, then Display order asc, then Name (Arabic) asc)
  When they click the "Tier" column header
  Then a POST /account/api/admin/sponsors/list fires carrying
       GridQuery.Sort = "tier" and GridQuery.SortDescending = false
  And the grid re-renders ordered by Tier ascending (Platinum→Gold→Silver→Bronze)

  When they click the "Tier" column header again
  Then a POST /account/api/admin/sponsors/list fires carrying
       GridQuery.Sort = "tier" and GridQuery.SortDescending = true
  And the grid re-renders ordered by Tier descending (Bronze→Silver→Gold→Platinum)
  And the sortable columns are exactly: Name (English), Name (Arabic), Tier,
      Display order and Active (Logo path and Link are not Sortable)
```

### E2E-SPN-018 — Presentation toggle persists across reload (D-353)

```gherkin
Scenario: Switch between Popup and full Page and the choice persists
  Given the administrator is on /admin/sponsors with the default "dialog" (popup) presentation
  And the grid toolbar shows the CrudPresentationToggle (PageKey="sponsors")
  When they click the toggle to choose "Open as full page"
  Then localStorage key "simf.cp.prefs.sponsors" holds {"v":1,"presentation":"page"}
  When they reload /admin/sponsors
  Then OnInitializedAsync calls Prefs.GetPresentationAsync("sponsors") and reads back "page"
  And opening "Add" now renders the full-page CrudShell frame (not a popup with a backdrop)
  When they switch the toggle back to "Open as dialog"
  Then localStorage key "simf.cp.prefs.sponsors" holds {"v":1,"presentation":"dialog"}
  And after a reload opening "Add" renders the popup dialog again
```

### E2E-SPN-019 — Full-page mode round-trip (D-353)

```gherkin
Scenario: In full-page mode the Add/Edit/View forms take over the content area
  Given the presentation for /admin/sponsors is set to "page"
  When the administrator clicks the grid toolbar "Add" action
  Then the grid + SimfBanner are hidden (GridHidden = FormOpen && presentation == Page)
  And the CrudShell renders full-page with the title "Add sponsor" and a Close ("Close") header
  And there is no modal backdrop
  When they fill Name (English) = "Naval Group", Name (Arabic) = "نافال جروب",
      select Tier = "Platinum", and click "Add sponsor"
  Then POST /account/api/admin/sponsors returns HTTP 200
  And the CrudShell closes and the grid + banner re-appear with the new row
  And a green toast reads "Sponsor saved." / "تم حفظ الراعي."
  When they click the row's Details action
  Then the SponsorsViewDelete form opens full-page in read-only mode (no Delete button)
      showing Name (English/Arabic), Tier, Logo path, Link, Display order and Active
  When they click the CrudShell close (X) / "Close"
  Then the form closes and the grid re-appears unchanged
```

### E2E-SPN-020 — Delete confirmation gate (CrudShell + SimfConfirm) (D-353)

```gherkin
Scenario: Delete is gated by SimfConfirm inside the ViewDelete form, not window.confirm
  Given a sponsor "Naval Group" exists and is Active
  And the administrator is on /admin/sponsors
  When they click the "Naval Group" row's Delete (trash) action
  Then the page first GETs /account/api/admin/sponsors/{id} to load the full detail
  And the SponsorsViewDelete form opens (in a CrudShell popup or full page per the toggle)
      showing the read-only details and a red "Delete" button
  When they click the red "Delete" button
  Then a SimfConfirm dialog appears (Danger=true) with the message
       Admin.Sponsors.Delete.Message formatted with the sponsor's English name
       (NOT a native browser window.confirm)
  When they click the confirm "Cancel"
  Then no DELETE request fires and the row is unchanged (still Active = "✓")
  When they re-open Delete and click the confirm "Delete"
  Then exactly one DELETE /account/api/admin/sponsors/{id} fires and returns HTTP 200
  And the CrudShell closes
  And a green toast reads "Sponsor deleted." / "تم حذف الراعي."
  And the grid reloads (the soft-deactivated row shows Active = "—" until an isActive
      filter excludes inactive rows)
```

**Evidence captured:**
- The delete now flows through `SponsorsViewDelete.razor` → `SimfConfirm` → `simfAccount.deleteJson`;
  there is **no** `window.confirm` / `handle_dialog` step any more (the inline list `confirm()` was
  removed in D-353).
- Network: exactly one `DELETE /account/api/admin/sponsors/{id}` on confirm, zero on Cancel.

### E2E-SPN-021 — Excel export (D-356)

```gherkin
Scenario: Export the filtered grid (or just selected rows) to an XLSX workbook
  Given the administrator is on /admin/sponsors with at least two sponsors
  When they click the toolbar "Export" action with no rows selected
  Then OnExportAsync calls _excel.ExportAsync(empty Ids, current Query)
  And a POST /account/api/admin/sponsors/export fires carrying
      AdminGridExportRequest { Ids: [], Query: <current GridQuery> }
  And the API caps the set at 5000 rows and returns an XLSX
      (Content-Type application/vnd.openxmlformats-officedocument.spreadsheetml.sheet)
  And the browser saves a file named simf-sponsors-{yyyyMMddHHmmss}.xlsx
  And the workbook's "Sponsors" sheet header row reads
      NameEn | NameAr | Tier | LogoRelativePath | Url | DisplayOrder | IsActive
      | Tagline | TaglineArabic | About | AboutArabic   (D-502)
  And the Tier column is written by display name (Platinum/Gold/Silver/Bronze) so it
      round-trips back through import
  When they instead select two rows then click "Export"
  Then the export request carries those two Ids in AdminGridExportRequest.Ids
  And the workbook contains exactly those two rows
```

### E2E-SPN-022 — Excel import (D-356)

```gherkin
Scenario: Import sponsors from a workbook and see the per-row outcome
  Given the administrator is on /admin/sponsors
  When they click the toolbar "Import" action
  Then OnImportAsync calls _excel.TriggerImportAsync(), opening the file picker
      on the hidden <input id="sponsors-import-input" accept=".xlsx">
  When they choose an .xlsx whose "Sponsors" sheet has the required headers
      NameEn, NameAr, Tier and rows for two new sponsors
      (e.g. "Thales"/"تاليس"/"Gold" and "Leonardo"/"ليوناردو"/"Silver")
  Then a POST /account/api/admin/sponsors/import fires as multipart form data
      (field "file")
  And the import-result modal shows "2 created, 0 updated, 0 skipped." with an empty error list
  And a green toast reads the shared Grid.Import.Done key ("Import complete.")
  And OnImportedAsync reloads the grid (LoadAsync) so both new sponsors appear
  When they import a workbook with one row whose Tier cell is "Diamond" (unknown)
  Then ParseTier raises a per-row DataValidationException and that row is reported in the
      modal's error list ("The tier must be one of Platinum, Gold, Silver or Bronze."
      / "يجب أن تكون الفئة إحدى: بلاتيني أو ذهبي أو فضي أو برونزي.") while the valid rows
      still create (one bad row never aborts the batch)
  And a row missing NameEn or NameAr is reported with
      "Both the English and Arabic names are required." /
      "الاسم بالإنجليزية والعربية مطلوبان."
  And note: import is insert-only — ContactId (the optional shared-Contact link) is never set
      by import; an admin links a contact afterwards via Edit
  And the optional Tagline/TaglineArabic/About/AboutArabic columns ARE imported when present
      (trimmed + length-guarded by CreateAsync); absent columns simply stay null (D-502)
```

### E2E-SPN-023 — Excel import rejection (D-356)

```gherkin
Scenario: A bad / wrong-sheet upload is rejected and nothing is created
  Given the administrator is on /admin/sponsors
  When they import a file that is not a valid .xlsx (fails the ZIP-magic check 50 4B 03 04)
  Then POST /account/api/admin/sponsors/import returns HTTP 400 (DataValidationException)
  And OnExcelError surfaces a red toast reading
       "The file is not a valid Excel workbook." / "الملف ليس مصنف Excel صالحًا."
  And no sponsor is created
  When they import an .xlsx larger than 5 MB
  Then the request is rejected (HTTP 413, AdminImportEmpty) with
       "The Excel file is too large. The maximum is 5 MB." /
       "ملف Excel كبير جدًا. الحد الأقصى 5 ميغابايت."
  When they import a workbook whose worksheet is not named "Sponsors" (or is missing one of
       the required headers NameEn / NameAr / Tier)
  Then the parse fails and the request returns 400 with the bilingual worksheet/header message
  And no sponsor is created
```

---

## Implementation notes

- **Manual smoke is canonical today.** Until Playwright is adopted, the canonical
  "run" of these scenarios is a Chrome DevTools MCP session — sign in via the
  Background steps, walk each scenario, and capture screenshots into
  `docs/screenshots/cp-admin-sponsors-*.png`.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin scenario
  into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) plus
  a step-definition class. The Gherkin shape is already runner-agnostic.
- **API integration tests** at `tests/SIMF.Api.Tests/SponsorsTests.cs` cover the
  same surface at a lower layer (no browser):
  - `Admin_creates_sponsor_and_public_endpoint_returns_it_grouped`
  - `Public_list_groups_highest_tier_first`
  - `Deactivated_sponsor_drops_off_public_list`
  - `Duplicate_active_name_in_same_tier_returns_409` (E2E-SPN-012 at API layer)
  - `Non_admin_caller_is_forbidden_on_create` (E2E-SPN-009 at API layer)
  When an E2E scenario reliably covers one of these, the matching `Api.Tests` case
  can usually be retired — but keep both during the transition.
- **Backing endpoints / error codes** (grounded in
  `src/Backend/SIMF.Api/Endpoints/Sponsors/SponsorEndpoints.cs` +
  `src/Backend/SIMF.Infrastructure/Sponsors/AdminSponsorService.cs`):
  - `POST /admin/sponsors/list` — policy `Sponsors.View`
  - `POST /admin/sponsors` — policy `Sponsors.Create`, rate-limited "auth"
  - `PUT /admin/sponsors/{id}` — policy `Sponsors.Edit`, rate-limited "auth"
  - `DELETE /admin/sponsors/{id}` — policy `Sponsors.Delete` (soft-deactivate), rate-limited "auth"
  - `POST /admin/sponsors/export` — policy `Sponsors.Export`, rate-limited "auth"; columns
    NameEn, NameAr, Tier (display name), LogoRelativePath, Url, DisplayOrder, IsActive,
    Tagline, TaglineArabic, About, AboutArabic (D-502); sheet
    "Sponsors"; file `simf-sponsors-{ts}.xlsx`; 5000-row cap
    (`src/Backend/SIMF.Api/Endpoints/Admin/SponsorsExcelEndpoints.cs` →
    `ExportSponsorsEndpoint` over `AdminGridExportEndpoint<AdminSponsorSummary>`)
  - `POST /admin/sponsors/import` — policy `Sponsors.Import`, rate-limited "auth"; multipart
    "file"; required headers NameEn/NameAr/Tier; insert-only; ContactId omitted; 5 MB +
    ZIP-magic upload gate (400/`AdminImportEmpty`), 5000-row cap; unknown tier / blank name =
    per-row error (`ImportSponsorsEndpoint` over `AdminGridImportEndpoint`)
  - Error codes: `SponsorInvalid` (400), `SponsorDuplicate` (409), `SponsorNotFound` (404)
  - Tier values: 10=Platinum, 20=Gold, 30=Silver, 40=Bronze
  - Field limits: NameEn/NameAr 1–256, LogoRelativePath ≤256, Url ≤512, DisplayOrder ≥0
  - Audit events: `Sponsor.Created`, `Sponsor.Updated`, `Sponsor.Deactivated`
- **CP page note (updated D-353/D-356).** The page is now fully on the uniform CRUD
  shell: Add/Edit (`SponsorsAddEdit`), View/Delete (`SponsorsViewDelete`) and the
  read-only Details view are all hosted by `CrudShell`, framed as a popup or a full
  page per the toolbar `CrudPresentationToggle` (PageKey `"sponsors"`, persisted in
  localStorage `simf.cp.prefs.sponsors`). Delete is gated by an in-form `SimfConfirm`
  (no native `window.confirm`, so no `handle_dialog` step) — see E2E-SPN-020. A
  read-only **Details** view now exists (E2E-SPN-019) via the same ViewDelete form
  with `IsDelete=false`. D-356 added Excel **export + import** through `CrudGridExcel`
  (`Resource="sponsors"`): export `POST /admin/sponsors/export`, import
  `POST /admin/sponsors/import` (insert-only, 5 MB + ZIP-magic gate, 5000-row cap) —
  see E2E-SPN-021..023. Action buttons are not individually `<AuthorizedAction>`-gated;
  per-action enforcement is API-side only (see E2E-SPN-009), and Export/Import are
  gated by the `Sponsors.Export` / `Sponsors.Import` policies on the API.

### E2E-SPN-024 — Logo via the unified media-asset pipeline (D-357)

```gherkin
Scenario: Upload logo, then switch it to an external link
  Given an Administrator is editing a sponsor
  When they open the "Image" control, choose "Upload file", pick a PNG and click Upload
  Then a success message shows and the preview thumbnail refreshes
  And GET /account/api/admin/assets/SponsorLogo/{ownerId}/image returns the bytes (200)
  And /admin/media-library lists it as SponsorLogo - this entity - Image - Uploaded file - active
  When they switch to "External link", enter https://cdn.example/x.jpg and click Save link
  Then the asset Source becomes "External link" and GET /app/assets/SponsorLogo/{ownerId}/image 302s to that URL
  And the same-origin /content/assets/SponsorLogo/{ownerId}/image proxy serves it for any public page that renders this entity
```

**Evidence:** the Asset DB row + the out-of-row file (or stored link); the Media Library row;
0 console errors; audit `AssetUploaded` then `AssetLinked`. Validation: a non-image / over-5 MB /
video upload is 400; deactivate->restore round-trips; restoring when a live (category,owner) asset
already exists is 409 (covered by `tests/SIMF.Api.Tests/AssetEndpointsTests.cs`).

### E2E-SPN-025 — Edit preserves the bilingual tagline (regression D-501)

```gherkin
Scenario: Editing a sponsor does not wipe its tagline
  # Regression for D-501: the inline UpdateSponsorRequest bind model had no
  # Tagline/TaglineArabic property, so FastEndpoints silently dropped them on PUT
  # and UpdateAsync overwrote the stored tagline with null — every edit lost it.
  Given an active sponsor "Tagline Co" / "شركة الشعار" exists in tier "Gold"
        with Tagline = "Strategic Partner" / "الشريك الاستراتيجي"
  And the administrator is on /admin/sponsors
  When they click the "Tagline Co" row's Edit (pencil) action
  Then the modal pre-fills the Tagline (English) and Tagline (Arabic) fields
       with "Strategic Partner" and "الشريك الاستراتيجي"
  When they change Display order to "5" and leave both tagline fields unchanged
  And they click "Save"
  Then PUT /account/api/admin/sponsors/{id} returns HTTP 200
  And the returned AdminSponsorDetail still has
       Tagline = "Strategic Partner" and TaglineArabic = "الشريك الاستراتيجي"
  When they re-open the "Tagline Co" row's Edit action (or the public sponsor detail)
  Then both tagline fields are still populated (the tagline was NOT lost on save)
```

**Evidence captured:**
- API-layer proof: `tests/SIMF.Api.Tests/SponsorsTests.cs` →
  `Admin_update_preserves_the_tagline` (fails before the fix — the tagline returns
  `null`; passes after the bind model carries `Tagline`/`TaglineArabic`).
- Network: the PUT request body carries `tagline`/`taglineArabic`, and the 200
  response echoes them back unchanged.

### E2E-SPN-026 — Excel round-trips Tagline/TaglineArabic + About/AboutArabic (D-502)

```gherkin
Scenario: Export then re-import carries the bilingual tagline + about
  # Regression for D-502: the sponsor Excel export columns + import ApplyRowAsync
  # omitted Tagline/TaglineArabic/About/AboutArabic, so those fields could not
  # round-trip through Excel (export hid them; import always left them null).
  Given an active sponsor exists with Tagline = "Strategic Partner" / "الشريك الاستراتيجي"
        and About = "A global energy leader." / "شركة طاقة عالمية."
  When the administrator clicks the toolbar "Export" action
  Then the "Sponsors" sheet header row includes the columns
       Tagline, TaglineArabic, About, AboutArabic
  And that sponsor's row carries "Strategic Partner" in the Tagline cell
  When they import a workbook whose row sets
       Tagline = "Strategic Partner", TaglineArabic = "الشريك الاستراتيجي",
       About = "A global energy leader.", AboutArabic = "شركة طاقة عالمية."
  Then POST /account/api/admin/sponsors/import returns HTTP 200 with the row Created
  And the created sponsor's detail (and the grid list summary) carries all four
       bilingual values — they were NOT dropped at the Excel IO boundary
```

**Evidence captured:**
- API-layer proof: `tests/SIMF.Api.Tests/SponsorsExcelTests.cs` →
  `Export_includes_the_tagline_and_about_columns` (parses the workbook, asserts the
  four headers + the Tagline value) and `Import_round_trips_the_tagline_and_about`
  (imports a workbook with the four columns, asserts the listed summary carries them).

---

_Last reviewed:_ 2026-06-26 by Claude (D-502 — Excel tagline/about round-trip; D-501 edit-preserves-tagline).
Prior: 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle). Added E2E-SPN-018..023
(D-353 toggle + full-page round-trip + SimfConfirm delete gate; D-356 Excel export/import +
import rejection) and corrected the stale native-`window.confirm` delete copy in
E2E-SPN-001/004/005 + the CP page note to the shipped CrudShell + SimfConfirm flow.
Prior: 2026-06-03 by Claude (E2E catalogue rebuild) (D-256/D-257 grid affordances reconciled).
