# E2E test catalogue — Session categories (`/admin/session-categories`)

| | |
|--|--|
| **Page** | [`cp/session-categories.md`](../../pages/cp/session-categories.md) |
| **Route** | `/admin/session-categories` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **Page background.** B9b (D-226) — CP management for the dynamic session-category
> lookup (SIMF-FDS-004 §5.4). A small bilingual lookup (NameEn / NameAr / display
> order / active) that backs the category picker on the session form. The table
> **ships empty** and is seeded by the team once the client confirms the list
> (open item OI-2), so the empty-state path is the default first render. Mirrors
> `BoothsList` / the Organisation lookup.
>
> **Centralized framing (D-353) + Excel (D-356).** Add / Edit / View / Delete are
> no longer inline `SimfModal` forms — they are hosted by `CrudShell`, which frames
> the reusable `SessionCategoriesAddEdit` and `SessionCategoriesViewDelete` forms as
> a **popup or a full page** per the admin's toolbar choice (a
> `<CrudPresentationToggle PageKey="session-categories">` persisted in localStorage
> via `CpPreferences`). Delete now runs through `SessionCategoriesViewDelete` + a
> `SimfConfirm` gate — the old native `confirm()` is gone. The toolbar also wires
> **Excel Export + Import** through a shared `<CrudGridExcel Resource="session-categories">`
> (`OnExport` / `OnImport`).
>
> **RequiredPermission:** the page is gated
> by `PermissionCatalog.SessionCategories.View`; the toolbar/row actions are gated
> by `.Create` / `.Edit` / `.Delete` (all `AdminOnly` baseline).
>
> **Grid (D-256).** The page now renders the canonical `SimfDataGrid` (raw-table →
> grid migration, owner-mandated standard) — server-paged with a numbered pager
> (`GridQuery { Top = 20 }`), per-column filter inputs on **Name (English)** and
> **Name (Arabic)** (`nameEn` / `nameAr`), and column sort on all four columns
> (`nameEn` / `nameAr` / `order` / `isActive`). The toolbar **Add** action and the
> per-row **Edit** (pencil) / **Delete** (trash) actions are quiet grid affordances
> (`OnAdd` / `OnEditOne` / `OnDeleteOne`), not filled text buttons. `Multiselect`
> renders select-all / per-row checkboxes, but there is **no bulk-action toolbar
> button** on this page (selection is cosmetic here — no bulk endpoint).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-SCT-001 | Full CRUD round-trip — Add → Edit (toggle Active off) → Delete | happy | P0 | _to author_ |
| E2E-SCT-002 | Empty list renders `SimfEmptyState` ("No session categories yet.") | happy | P1 | _to author_ |
| E2E-SCT-003 | Auth: signed-in admin lacking `SessionCategories.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-SCT-004 | New-category button: opens Add modal with 4 fields | function | P1 | _to author_ |
| E2E-SCT-005 | Edit button: pre-fills modal from GET detail + shows Active checkbox | function | P1 | _to author_ |
| E2E-SCT-006 | Delete button: ViewDelete form + SimfConfirm → soft-delete (pill flips to Inactive) (D-353) | function | P1 | _to author_ |
| E2E-SCT-007 | Delete cancelled at confirm dialog → no request, no change | function | P2 | _to author_ |
| E2E-SCT-008 | Cancel button in modal closes it without saving | function | P2 | _to author_ |
| E2E-SCT-009 | Validation: blank NameEn or NameAr → client "Both names are required." | error | P1 | _to author_ |
| E2E-SCT-010 | Validation: name > 128 chars → API 400 `SESSION_CATEGORY_INVALID` | error | P1 | _to author_ |
| E2E-SCT-011 | Display-order field accepts integers; non-numeric coerces to 0 | function | P2 | _to author_ |
| E2E-SCT-012 | Action-level permission gating (Create/Edit/Delete buttons hidden) | auth | P1 | _to author_ |
| E2E-SCT-013 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-SCT-014 | RTL render: Arabic toggle mirrors page + Add modal | i18n | P1 | _to author_ |
| E2E-SCT-015 | Per-column filter narrows the grid (Name (English) / Name (Arabic)) | function | P1 | _to author_ |
| E2E-SCT-016 | Column sort toggles (Name (English) / Order) | function | P2 | _to author_ |
| E2E-SCT-017 | Presentation toggle: switch to full-page + persists across reload (D-353) | happy | P1 | _to author_ |
| E2E-SCT-018 | Full-page mode: Add/Edit/View take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-SCT-019 | Excel export: toolbar Export downloads an .xlsx of the filtered grid / selected rows (D-356) | happy | P1 | _to author_ |
| E2E-SCT-020 | Excel import: upload a workbook → rows created + result modal with per-row outcome (D-356) | happy | P1 | _to author_ |
| E2E-SCT-021 | Excel import: a non-workbook / wrong-sheet upload → bilingual rejection, nothing created (D-356) | error | P1 | _to author_ |
| E2E-SCT-022 | Add a category reusing an ACTIVE category's English name → 409, not 500 | conflict | P1 | _to author_ |
| E2E-SCT-023 | Rename a category onto an ACTIVE category's English name → 409 | conflict | P1 | _to author_ |
| E2E-SCT-024 | Re-activate a retired category whose English name a live one now holds → 409 | conflict | P2 | _to author_ |
| E2E-SCT-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-SCT-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-SCT-001 — Full CRUD round-trip

```gherkin
Feature: Session categories CRUD round-trip
  As an Administrator
  I want to manage the dynamic session-category lookup
  So that the session form's category picker reflects the event programme

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And they have navigated to /admin/session-categories
  And the page has finished loading (no "Loading categories…" text)

Scenario: Create, edit (toggle Active off), then delete one category
  Given the grid currently shows {N} rows (or the SimfEmptyState when N = 0)
  When the administrator clicks the grid toolbar's "Add" action
  Then the Add modal opens titled "Add category"
  And it shows four fields: Name (English), Name (Arabic), Display order, and an "Active" checkbox
  When they fill Name (English)="Keynote"
  And they fill Name (Arabic)="الكلمة الرئيسية"
  And they set Display order="10"
  And they click "Save"
  Then a POST /account/api/admin/session-categories fires and returns 200
  And the modal closes
  And a green toast reads "Category saved." / "تم حفظ التصنيف."
  And the grid shows {N + 1} rows
  And a row exists with Name (English)="Keynote", Name (Arabic)="الكلمة الرئيسية", Order=10, Active="✓"

  When the administrator clicks the "Keynote" row's Edit (pencil) action
  Then a GET /account/api/admin/session-categories/{id} fires and returns 200
  And the Edit modal opens titled "Edit category" with the row's values pre-filled
  And the "Active" checkbox is ticked
  When they change Display order to "5"
  And they untick the "Active" checkbox
  And they click "Save"
  Then a PUT /account/api/admin/session-categories/{id} fires and returns 200
  And the modal closes
  And a green toast reads "Category saved." / "تم حفظ التصنيف."
  And the "Keynote" row now reads Order=5 and Active="—"

  When the administrator clicks the "Keynote" row's Delete (trash) action
  Then the View/Delete form (SessionCategoriesViewDelete) opens showing the read-only details and a red "Deactivate" button
  When they click "Deactivate" and confirm in the SimfConfirm dialog (which names "Keynote")
  Then a DELETE /account/api/admin/session-categories/{id} fires and returns 200
  And a green toast reads "Category deleted." / "تم حذف التصنيف."
  And the "Keynote" row remains visible with the grey "Inactive" pill (soft-delete; the list has no active filter)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-session-categories-001-before.png`
- Screenshot after add: `docs/screenshots/cp-admin-session-categories-001-add.png`
- Screenshot after edit: `docs/screenshots/cp-admin-session-categories-001-edit.png`
- Screenshot after delete: `docs/screenshots/cp-admin-session-categories-001-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/session-categories/*` call returns 200
- Audit rows: `SessionCategory.Created`, `SessionCategory.Updated`, `SessionCategory.Deactivated` rows in the audit log with the actor's id (`Detail` carries `id=…; nameEn=Keynote`).

### E2E-SCT-002 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the SessionCategories table has no rows (the seed-empty default — OI-2)
  When the administrator opens /admin/session-categories
  Then the POST /account/api/admin/session-categories/list returns 200 with Total = 0
  And the grid body renders the SimfEmptyState component
  And the empty state title reads "No session categories yet." / "لا توجد تصنيفات جلسات بعد."
  And the grid toolbar's "Add" action is still visible above the empty state
  And no error toast appears
```

### E2E-SCT-003 — Auth gate

```gherkin
Scenario: Signed-in admin without SessionCategories.View is denied
  Given a user is signed in whose role does NOT grant PermissionCatalog.SessionCategories.View
  When they navigate to /admin/session-categories
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/session-categories/list request fires
  And the "Module.SessionCategories" nav item is not shown in the rail for that user
```

### E2E-SCT-004 — New-category button opens Add modal

```gherkin
Scenario: New category opens an empty Add modal
  Given the administrator is on /admin/session-categories
  When they click the grid toolbar's "Add" action
  Then the modal opens titled "Add category"
  And Name (English) and Name (Arabic) are empty
  And Display order shows 0
  And the "Active" checkbox is ticked by default
  And no request has fired yet (the POST only fires on Save)
```

### E2E-SCT-005 — Edit button pre-fills from GET detail

```gherkin
Scenario: Edit fetches the row detail and pre-fills the modal
  Given at least one session category "Workshop" exists
  When the administrator clicks the "Workshop" row's Edit (pencil) action
  Then a GET /account/api/admin/session-categories/{id} fires and returns 200
  And the modal opens titled "Edit category"
  And Name (English), Name (Arabic), Display order are pre-filled from the detail response
  And the "Active" checkbox reflects the row's current IsActive value
  And the buttons are disabled while the GET is in flight (_busy guard)
```

### E2E-SCT-006 — Delete soft-deletes via the ViewDelete form + SimfConfirm

```gherkin
Scenario: Delete confirms then soft-deletes the row
  Given an active session category "Panel" exists with the green "Active" pill
  When the administrator clicks the "Panel" row's Delete (trash) action
  Then the View/Delete form (SessionCategoriesViewDelete) opens showing the read-only details and a red "Deactivate" button
  When they click "Deactivate"
  Then a SimfConfirm dialog appears naming "Panel" (D-353, replaces the old native confirm())
  When they confirm via the dialog's "Deactivate" button
  Then a DELETE /account/api/admin/session-categories/{id} fires and returns 200
  And a green toast reads "Category deleted." / "تم حذف التصنيف."
  And the "Panel" row remains in the grid but its Active column shows the grey "Inactive" pill
  And re-deleting the same already-inactive row is idempotent (no error)
```

### E2E-SCT-007 — Delete cancelled at the confirm dialog

```gherkin
Scenario: Cancelling the SimfConfirm dialog makes no change
  Given an active session category "Roundtable" exists
  When the administrator clicks the "Roundtable" row's Delete (trash) action
  And the View/Delete form opens and they click "Deactivate"
  And they click "Cancel" in the SimfConfirm dialog
  Then no DELETE request fires
  And the "Roundtable" row is unchanged (still the green "Active" pill)
  And no toast appears
```

### E2E-SCT-008 — Cancel closes the modal without saving

```gherkin
Scenario: Cancel discards the in-progress form
  Given the Add modal is open with Name (English)="Discarded"
  When the administrator clicks "Cancel"
  Then the modal closes
  And no POST request fires
  And no new row appears in the grid
```

### E2E-SCT-009 — Client validation: blank names

```gherkin
Scenario: Blank English or Arabic name is blocked client-side
  Given the Add modal is open
  When the administrator leaves Name (English) blank (or Name (Arabic) blank)
  And clicks "Save"
  Then a SimfAlert error appears reading "Both names are required." / "كلا الاسمين مطلوبان."
  And the modal stays open
  And no POST /account/api/admin/session-categories request fires (guarded before the call)
```

### E2E-SCT-010 — Server validation: name over 128 chars

```gherkin
Scenario: Over-long name returns API 400 with bilingual server message
  Given the Add modal is open
  When the administrator fills Name (English) with a 129-character string
  And fills Name (Arabic)="صالح"
  And clicks "Save"
  Then the BFF forwards POST /admin/session-categories
  And the API returns HTTP 400 with ApiResult.Error.Code = "SESSION_CATEGORY_INVALID"
  And the modal stays open
  And the error toast surfaces the bilingual MessageForCurrentCulture():
      "Session category English name must be between 1 and 128 characters." /
      "يجب أن يتراوح طول الاسم الإنجليزي للتصنيف بين 1 و 128 حرفاً."
  And the Name (Arabic) over-128 path returns the matching Arabic-name message
```

### E2E-SCT-011 — Display-order field coercion

```gherkin
Scenario: Display order accepts integers and coerces invalid input to 0
  Given the Add modal is open
  When the administrator types "25" into Display order and saves a valid row
  Then the created row shows Order=25
  When they open the Add modal and clear Display order / type a non-numeric value
  Then on change the field resolves to 0 (int.TryParse fallback)
  And a row saved that way shows Order=0
```

### E2E-SCT-012 — Action-level permission gating

```gherkin
Scenario: View-only admin sees the grid but no mutating actions
  Given a user signed in with PermissionCatalog.SessionCategories.View but NOT Create/Edit/Delete
  When they open /admin/session-categories
  Then the grid and rows render
  And the grid toolbar's "Add" action is hidden (AuthorizedAction Create)
  And the per-row Edit (pencil) action is hidden (AuthorizedAction Edit)
  And the per-row Delete (trash) action is hidden (AuthorizedAction Delete)
  And a direct POST /account/api/admin/session-categories from that user is rejected by the API policy
```

### E2E-SCT-013 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the bilingual fallback toast
  Given the API is configured to return 500 on /admin/session-categories/list (e.g. DB down)
  When the administrator opens /admin/session-categories
  Then the page shows "Loading categories…" briefly
  And then a red toast appears reading "Could not load categories." / "تعذّر تحميل التصنيفات."
  And no grid rows render
```

### E2E-SCT-014 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/session-categories in English
  When they switch the UI language to العربية from the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "تصنيفات الجلسات"
  And the column headers read "الاسم (إنجليزي)", "الاسم (عربي)", "الترتيب", "نشط"
  And the nav rail and toolbar mirror (Arabic labels, reversed order)

  When they click the grid toolbar's "إضافة" (Add) action
  Then the Add modal opens in RTL titled "إضافة تصنيف"
  And the field labels read "الاسم (إنجليزي)", "الاسم (عربي)", "ترتيب العرض", "نشط"
  And the footer buttons read "إلغاء" and "حفظ" in reversed order
```

### E2E-SCT-015 — Per-column filter narrows the grid

```gherkin
Scenario: Typing into a per-column filter input narrows the grid server-side
  Given the administrator is on /admin/session-categories
  And the grid shows several categories including "Keynote" and "Workshop"
  When they type "key" into the filter input under the "Name (English)" column
  Then a POST /account/api/admin/session-categories/list fires
  And its GridQuery carries Filters["nameEn"]="key" with Skip reset to 0
  And the grid narrows to rows whose English name contains "key" (e.g. "Keynote")
  And the pager summary updates to the filtered Total

  When they clear the "Name (English)" filter
  And they type "ورشة" into the filter input under the "Name (Arabic)" column
  Then a POST /account/api/admin/session-categories/list fires
  And its GridQuery carries Filters["nameAr"]="ورشة" with Skip reset to 0
  And the grid narrows to rows whose Arabic name contains "ورشة"
  And only the "Name (English)" and "Name (Arabic)" columns expose a filter input
      (the "Order" and "Active" columns are not Filterable)
```

### E2E-SCT-016 — Column sort toggles

```gherkin
Scenario: Clicking a sortable column header toggles ascending/descending
  Given the administrator is on /admin/session-categories
  And the grid shows several categories
  When they click the "Name (English)" column header
  Then a POST /account/api/admin/session-categories/list fires
  And its GridQuery carries Sort="nameEn" with SortDescending=false (A→Z)
  And the rows reorder ascending by English name
  When they click the "Name (English)" column header again
  Then a POST .../list fires with Sort="nameEn" and SortDescending=true (Z→A)
  And the rows reorder descending by English name

  When they click the "Order" column header
  Then a POST .../list fires with Sort="order" and SortDescending=false
  And the rows reorder ascending by Display order
  And the default (unsorted) order is DisplayOrder then NameEn
```

### E2E-SCT-017 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch to full-page mode and it persists across reload
  Given the administrator is on /admin/session-categories with the default "dialog" presentation
  And the grid toolbar shows the CrudPresentationToggle "Open as full page" control (maximize icon)
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.session-categories" holds {"v":1,"presentation":"page"}
  When they reload /admin/session-categories
  Then OnInitializedAsync reads the preference back (Prefs.GetPresentationAsync("session-categories"))
  And the toggle still reads "Open as dialog"
  And opening Add now renders the full-page CrudShell frame (not a popup)
```

### E2E-SCT-018 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/View take over the content area; Save returns to the grid
  Given the presentation is set to "full page" (_presentation = Page)
  When the administrator clicks the grid toolbar's "Add" action
  Then the grid + SimfBanner are hidden (GridHidden) and the CrudShell renders the
       full-page frame titled "Add category" with a close header and the SessionCategoriesAddEdit form
  And there is no modal backdrop
  When they fill Name (English)="Plenary", Name (Arabic)="جلسة عامة", Display order="3" and click "Save"
  Then the page frame closes (CloseForm)
  And the grid re-appears with the new "Plenary" row and the green "Category saved." toast
  When they click the "Plenary" row's Edit (pencil) action and then the frame's close (X) button
  Then the form closes and the grid re-appears unchanged
  When they click the "Plenary" row's Details (eye) action
  Then the full-page frame renders SessionCategoriesViewDelete read-only (no Deactivate button)
  And clicking "Close" returns to the grid
```

### E2E-SCT-019 — Excel export (D-356)

```gherkin
Scenario: Export the grid to an XLSX workbook (whole grid vs selected rows)
  Given the administrator is on /admin/session-categories with at least two categories
  When they click the toolbar "Export" action with no rows selected
  Then a POST /account/api/admin/session-categories/export fires carrying
       AdminGridExportRequest { Ids = [], Query = <current GridQuery> }
  And the browser saves an .xlsx workbook of the whole filtered grid
  And the workbook header row reads Name | NameArabic | DisplayOrder | IsActive
  When they instead tick two row checkboxes then click "Export"
  Then the POST carries those two ids in Ids (Query omitted) and the workbook contains exactly those two rows
  And the API caps the export at 5000 rows
```

### E2E-SCT-020 — Excel import (D-356)

```gherkin
Scenario: Import categories from a workbook and see the per-row outcome
  Given the administrator is on /admin/session-categories
  When they click the toolbar "Import" action (OnImport → _excel.TriggerImportAsync())
  And the hidden file input "session-categories-import-input" (accept=".xlsx") opens
  And they choose an .xlsx whose sheet has Name/NameArabic rows for two new categories
  Then a POST /account/api/admin/session-categories/import fires as multipart form data
  And the import-result modal shows "2 created, 0 updated, 0 skipped." with an empty error list
  And the shared "Grid.Import.Done" success toast appears and the grid reloads listing both new categories
  When they import a workbook containing one row matching an existing name and one new name
  Then the modal shows the created/updated/skipped tally plus a per-row error/skip line for the matched row
```

### E2E-SCT-021 — Excel import rejection (D-356)

```gherkin
Scenario: A bad upload is rejected without creating anything
  Given the administrator is on /admin/session-categories
  When they import a file that is not a valid .xlsx (fails the ZIP-magic check) or exceeds the 5MB cap
  Then the request returns HTTP 400 and OnExcelError surfaces a bilingual error toast
  And no session category is created
  When they import a workbook whose worksheet is not the expected categories sheet
  Then the request returns HTTP 400 with the bilingual wrong-sheet message
  And the grid is unchanged
```

### E2E-SCT-022 — Adding a category on a taken active name is a conflict

```gherkin
Scenario: A duplicate English name is refused with a conflict, not a server error
  Given the administrator is on /admin/session-categories
  And an ACTIVE category already exists with English name "Keynote"
  When they open Add and save English name "Keynote" with Arabic name "كلمة رئيسية"
  Then the POST /account/api/admin/session-categories returns HTTP 409
  And the error code is SESSION_CATEGORY_INVALID
  And a bilingual conflict toast names the clashing category
      (EN "A session category named 'Keynote' already exists."
       / AR "يوجد تصنيف جلسة بالاسم 'Keynote' بالفعل.")
  And the response is NOT a 500 — the unique index is pre-checked, never left to
      surface as a raw DbUpdateException
  And no second category is created
```

### E2E-SCT-023 — Renaming onto a taken active name is a conflict

```gherkin
Scenario: An edit that moves a category onto a live name is refused
  Given an ACTIVE category "Keynote" and a second ACTIVE category "Panel" exist
  When the administrator edits "Panel" and changes its English name to "Keynote"
  Then the PUT /account/api/admin/session-categories/{id} returns HTTP 409
  And the error code is SESSION_CATEGORY_INVALID
  And "Panel" keeps its original name in the grid after the modal reports the error

  When they instead edit "Panel" and change only its display order
  Then the save succeeds — an edit that does not move the name is never blocked
      by the category's own row
```

### E2E-SCT-024 — Re-activating onto a name a live row now holds is a conflict

```gherkin
Scenario: Reviving a retired category whose name was reused is refused
  Given a category "Forum" was created and then deleted (Active pill reads "Inactive")
  And a NEW active category has since been created with the English name "Forum"
  When the administrator edits the retired "Forum" and ticks Active back on,
       changing no name
  Then the PUT /account/api/admin/session-categories/{id} returns HTTP 409
  And the error code is SESSION_CATEGORY_INVALID
  And the retired row stays Inactive
  # The unique index is filtered to [IsActive] = 1, so re-activation contends for
  # the name with no rename involved — a "check only when the name changed" guard
  # would miss this path and hand the admin a 500.
```

---

## Implementation notes

- **Add / Edit / View / Delete are CrudShell-hosted (D-353).** The page no longer
  carries inline `SimfModal` forms — `CrudShell` frames the reusable
  `SessionCategoriesAddEdit` (Add/Edit) and `SessionCategoriesViewDelete`
  (View/Delete) forms as a **popup or a full page** per the
  `CrudPresentationToggle` choice (persisted in localStorage key
  `simf.cp.prefs.session-categories`). Unlike Interests there is no separate
  Details glyph — the Details (eye) action opens the same `SessionCategoriesViewDelete`
  form in read-only mode (`IsDelete=false`, no Deactivate button). Edit re-fetches
  the row via `GET /account/api/admin/session-categories/{id}` to pre-fill.
- **Delete is a soft-delete (Deactivate) behind a `SimfConfirm` gate (D-353).** The
  Delete (trash) action opens `SessionCategoriesViewDelete` (`IsDelete=true`); its
  red "Deactivate" button raises a `SimfConfirm` dialog (titled "Delete category" /
  message naming the row) — the old native `confirm()` on the list is **gone**.
  Only the dialog's confirm button fires `DELETE`; the service runs
  `category.Deactivate()` (sets `IsActive = false`) and is idempotent on
  already-inactive rows. The list endpoint applies **no default active filter** (the
  page sends `GridQuery { Top = 20 }`), so a deleted row stays visible — its Active
  column flips from the on pill ("Active") to the off pill ("Inactive") rather than
  disappearing — assert that, not row removal. (The older "✓"/"—" glyphs in the
  scenarios above are the pre-grid representation; the post-D-256 grid renders a
  `SimfPill` on/off badge.) Driving via Chrome DevTools MCP no longer needs
  `handle_dialog` pre-arming — the confirm is an in-page Blazor component, not a
  browser dialog.
- **Two distinct validation layers.** "Both names are required." is a **client**
  guard in `SaveAsync` (no request fires). The 1–128 length bound is enforced
  **server-side** and returns `SESSION_CATEGORY_INVALID` (HTTP 400) with the
  bilingual message; the EF column and the `MaxLength="128"` field cap also bound
  the input. **Corrected:** this note previously said the lookup carried no
  uniqueness constraint and that a 409 scenario did not apply. It does —
  `SessionCategoryConfiguration` declares a unique index on `Name` **filtered to
  `[IsActive] = 1`**, so the English name is contended among the ACTIVE rows only.
  `AdminSessionCategoryService` now pre-checks it and answers `409` with
  `SESSION_CATEGORY_INVALID`; without the pre-check the index raised a raw
  `DbUpdateException` and the admin saw a 500. Because the index is **filtered**,
  three paths can collide — create, rename, and re-activating a retired row whose
  name a live row has since taken (no rename involved) — which is why
  E2E-SCT-022..024 are catalogued separately.
- **API integration tests** at `tests/SIMF.Api.Tests/SessionCategoriesTests.cs`
  cover the create → get → list, update, deactivate, validation (400) and the
  permission policy at the API layer (no browser). The E2E catalogue layers the
  CP-driven UI behaviour (modals, toasts, confirm dialog, action-button gating,
  RTL) on top of that lower-layer coverage.
- **Grid filter / sort keys (D-255/D-256).** The UI exposes a per-column filter
  input only on **Name (English)** (`nameEn`) and **Name (Arabic)** (`nameAr`) —
  the only columns marked `Filterable="true"`. The backend
  (`AdminSessionCategoryService.ListAsync`) honours those two filter keys (plus an
  `isActive` key the UI does not surface) and the sort keys `nameEn` / `nameAr` /
  `order` / `isActive`; all four columns are `Sortable="true"`. Unknown filter
  columns are ignored. Default order is `DisplayOrder` then `NameEn`. `Multiselect`
  shows select-all / per-row checkboxes, but there is **no `CustomToolbar`
  bulk-action button** on this page, so no bulk scenario is catalogued.
- **Audit keys:** `SessionCategory.Created`, `SessionCategory.Updated`,
  `SessionCategory.Deactivated` (`AuditEvents`), one row per mutation with the
  actor id.
- **Excel export + import (D-356).** The toolbar wires both `OnExport` and
  `OnImport` through a shared `<CrudGridExcel @ref="_excel" Resource="session-categories">`.
  Export POSTs `AdminGridExportRequest { Ids, Query }` to
  `/account/api/admin/session-categories/export` (empty `Ids` + current `Query` =
  whole filtered grid; selected row ids = just those, `Query` omitted). Import opens
  the hidden `session-categories-import-input` (accept `.xlsx`) and POSTs multipart to
  `/account/api/admin/session-categories/import`, then shows the
  "{Created} created, {Updated} updated, {Skipped} skipped" result modal + a per-row
  error list, the shared `Grid.Import.Done` success toast, and reloads the grid. The
  API caps both export and import at 5000 rows and rejects a non-`.xlsx` upload
  (ZIP-magic + 5MB gate) with HTTP 400 surfaced via `OnExcelError`.
- **Presentation toggle (D-353).** `<CrudPresentationToggle PageKey="session-categories">`
  in the grid `CustomToolbar` switches Add/Edit/View/Delete between a popup and a
  full-page CrudShell frame; the choice is persisted in localStorage
  `simf.cp.prefs.session-categories` and read back by
  `Prefs.GetPresentationAsync("session-categories")` in `OnInitializedAsync`.

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle; D-353 CrudShell/SimfConfirm reconciled). Earlier: 2026-06-03 (D-256/D-257 grid affordances).
