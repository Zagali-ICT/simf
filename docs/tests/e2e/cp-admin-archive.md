# E2E test catalogue — Previous Editions (Archive) CRUD (`/admin/archive`)

| | |
|--|--|
| **Page** | [`cp/admin-archive.md`](../../pages/cp/admin-archive.md) |
| **Route** | `/admin/archive` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (canonical SIMF browser smoke). Convertible to Playwright later — keep scenario steps tool-agnostic. |
| **Auth setup** | `superadmin@simrsnf.com` / `[REDACTED - supply via SIMF_API_SuperAdmin__TempPassword]` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-16 (D-440 — gallery/past-speaker image fields now expect a full https URL) |

> **P6 (D-440):** the gallery `url` and past-speaker `photo` fields are now
> rendered as **real images in the app** when they hold an **absolute https URL**
> (the textarea placeholders were updated to a full-URL example). The pipe format
> is unchanged (`url | image|video | caption`, `nameAr | nameEn | photo-url`); a
> relative path still saves fine but the app shows a glyph/initials fallback for
> it. No behaviour/validation change in the CP — authoring stays replace-all.

> **Permission gate.** The page carries `@attribute [RequirePermission(PermissionCatalog.Archive.View)]`
> (`"Archive.View"`). The backing API endpoints are gated per action:
> `Archive.View` (list + get), `Archive.Create` (POST), `Archive.Edit` (PUT),
> `Archive.Delete` (DELETE), `Archive.Snapshot` (POST `snapshot-current` — the
> "make this year history" action, D-275). `Administrator = "*"` satisfies all
> five. The CP nav item `Module.PreviousEditions` is gated by `Archive.View`.
>
> **"Make this year history" (D-275).** A toolbar button above the grid (gated by
> `Archive.Snapshot`, wrapped in `<AuthorizedAction>`) opens a confirm dialog with
> a single "Show in the archive now" checkbox and POSTs
> `/account/api/admin/archive/snapshot-current`. The API **generates** the year
> (current UTC year) + bilingual title ("SIMF {year}" / "سيمف {year}") and
> **computes** the three counters from live data — **attendees = distinct
> gate-scan arrivals** (allowed `CheckIn` scans with a resolved profile),
> sessions = active sessions, speakers = active speakers — then reuses the create
> path, so a second snapshot of the same year returns
> `archive_edition_year_duplicate` 409. The optional checkbox flips the
> `ArchiveVisibility` toggle (D-166) on.
>
> **"Delete" is a soft-delete.** The grid row's Delete (trash) action calls the
> BFF `DELETE /account/api/admin/archive/{id}` which maps to the API
> `DeactivateAsync` — it flips `IsActive = false` (pulls the edition from the
> public `/archive` list) but never hard-deletes the row. The row therefore
> stays in the admin grid afterwards with the `Active` pill flipped to
> "Inactive".
>
> **Grid affordances (D-256/D-257).** The page renders a `SimfDataGrid`
> (`Top = 20` page size, `Multiselect` select-all checkboxes — cosmetic, there
> is no bulk toolbar action). Row actions are quiet **icon** buttons in the
> grid's `RowActions` (Edit = pencil, Delete = trash), not filled text buttons.
> Per-column filter inputs exist on **Title (English)** (`titleEn`) and
> **Title (Arabic)** (`titleAr`); column sort is available on **Year** (`year`)
> and **Title (English)** (`titleEn`). The backend honours `Filters["titleEn"]`
> / `Filters["titleAr"]` (case-insensitive `Contains`) and `Sort` =
> `year` | `titleEn`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-ARC-001 | Full round-trip — Add → list → Edit → Deactivate (Delete) | happy | P0 | _to author_ |
| E2E-ARC-002 | Empty list renders `SimfEmptyState` ("No archive editions yet.") | happy | P1 | _to author_ |
| E2E-ARC-003 | Auth gate: signed-in user lacking `Archive.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-ARC-004 | Add modal opens with defaults (Year = current UTC year, IsActive ticked) | happy | P1 | _to author_ |
| E2E-ARC-005 | Client validation: blank English/Arabic title → bilingual modal toast | error | P1 | _to author_ |
| E2E-ARC-006 | Server validation: Year out of 2000–2100 → `archive_edition_invalid` 400 | error | P1 | _to author_ |
| E2E-ARC-007 | Conflict: duplicate Year → `archive_edition_year_duplicate` 409 | error | P1 | _to author_ |
| E2E-ARC-008 | Edit: change IsActive + counts; modal pre-fills the row values | happy | P1 | _to author_ |
| E2E-ARC-009 | Delete confirm dialog: cancel keeps the edition active | happy | P1 | _to author_ |
| E2E-ARC-010 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-ARC-011 | RTL / Arabic render mirrors page + Add modal | i18n | P1 | _to author_ |
| E2E-ARC-012 | Per-column filter narrows the grid (titleEn / titleAr) | happy | P1 | _to author_ |
| E2E-ARC-013 | Column sort toggles (year / titleEn ascending↔descending) | happy | P2 | _to author_ |
| E2E-ARC-014 | Make this year history → snapshot creates "SIMF {year}" with computed counts | happy | P0 | authored ✓ (`Snapshot_creates_current_year_edition_and_duplicate_409`) |
| E2E-ARC-015 | Second snapshot of the same year → `archive_edition_year_duplicate` 409 | error | P1 | authored ✓ (`Snapshot_creates_current_year_edition_and_duplicate_409`) |
| E2E-ARC-016 | Snapshot forbidden without `Archive.Snapshot` (non-admin → 403) | auth | P0 | authored ✓ (`Snapshot_is_forbidden_for_a_non_admin`) |
| E2E-ARC-017 | "Show in archive now" checkbox flips `ArchiveVisibility` on | happy | P1 | authored (screen) |
| E2E-ARC-018 | Presentation toggle persists across reload (`simf.cp.prefs.archive`) (D-353) | happy | P1 | _to author_ |
| E2E-ARC-019 | Full-page mode round-trip — Add/Edit/View take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-ARC-020 | Delete confirmation gate — ViewDelete + SimfConfirm (Cancel = no DELETE; confirm = one DELETE) (D-353) | error | P0 | _to author_ |
| E2E-ARC-021 | Excel export — whole filtered grid vs selected rows, real header row (D-356) | happy | P1 | _to author_ |
| E2E-ARC-022 | Excel import — workbook upload → rows created + per-row outcome modal (D-356) | happy | P1 | _to author_ |
| E2E-ARC-023 | Excel import rejection — non-.xlsx / wrong-sheet upload → bilingual 400, nothing created (D-356) | error | P1 | _to author_ |
| E2E-ARC-024 | Cover Image via the unified media-asset pipeline — upload then external link (D-357) | happy | P1 | _to author_ |
| E2E-ARC-025 | Excel round-trip of the dropped edition fields — summary / location / date label / cover path survive export + import (D-506) | happy | P1 | authored ✓ (`Export_includes_the_dropped_edition_columns` + `Import_round_trips_the_dropped_edition_fields`) |
| E2E-ARC-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-ARC-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-ARC-001 — Full round-trip (golden path)

```gherkin
Feature: Previous Editions (Archive) CRUD round-trip
  As an Administrator
  I want to manage the public Archive / Past Editions list
  So that the website's "Previous Editions" page stays accurate

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And they have navigated to /admin/archive
  And the page has issued POST /account/api/admin/archive/list and rendered the grid

Scenario: Create, list, edit, then deactivate one archive edition
  Given the grid currently shows {N} rows
  When the administrator clicks "Add edition"
  Then the "Add edition" modal opens
  And the Year field defaults to the current UTC year (e.g. 2026)
  And the "Active (visible in public archive)" checkbox is ticked
  When they set Year="2019"
  And they fill Title (English)="SIMF 2019 — Inaugural Forum"
  And they fill Title (Arabic)="منتدى سيمف 2019 — النسخة الأولى"
  And they fill Summary (English)="The first Saudi International Maritime Forum."
  And they fill Summary (Arabic)="منتدى السعودية الدولي البحري الأول."
  And they set Attendees="1200"
  And they set Sessions="18"
  And they set Speakers="42"
  Then no cover-image field is offered, only the hint
      "Save the record first, then add an image."
  When they click "Save"
  Then the BFF forwards POST /account/api/admin/archive and the API returns HTTP 200
  And the modal closes
  And a green SimfAlert toast reads "Edition saved."
  And the grid reloads (POST /account/api/admin/archive/list) and shows {N + 1} rows
  And a row exists with Year=2019, Title (English)="SIMF 2019 — Inaugural Forum",
      Attendees=1200, Sessions=18, Speakers=42 and an "Active" pill

  When the administrator clicks the 2019 row's Edit (pencil) action
  Then the "Edit edition" modal opens with every field pre-filled from the row
  And the IsActive checkbox is ticked
  When they change Attendees to "1350"
  And they click "Save"
  Then the BFF forwards PUT /account/api/admin/archive/{id} and the API returns HTTP 200
  And the modal closes
  And a green toast reads "Edition saved."
  And the 2019 row's Attendees column now reads "1350"

  When the administrator clicks the 2019 row's Delete (trash) action
  Then the View/Delete form opens (CrudShell, dialog by default) showing the row's read-only details
  And a red "Deactivate" button is visible
  When they click "Deactivate"
  Then a SimfConfirm dialog asks to confirm, naming the edition
  When they click the confirm "Deactivate" button
  Then the BFF forwards DELETE /account/api/admin/archive/{id} and the API returns HTTP 200
  And a green toast reads "Edition deleted."
  And the grid reloads
  And the 2019 row is still present but its Active pill now reads "Inactive"
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-archive-001-before.png` (grid baseline)
- Screenshot add modal: `docs/screenshots/cp-admin-archive-001-add-modal.png`
- Screenshot after create: `docs/screenshots/cp-admin-archive-001-after-create.png`
- Screenshot after deactivate: `docs/screenshots/cp-admin-archive-001-after-delete.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/archive/*` call returns 200
- Audit rows: `OperationLog` rows `archive_edition.created`, `archive_edition.updated`,
  `archive_edition.deactivated` — each with the signed-in admin's `ActorUserId`

### E2E-ARC-002 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no ArchiveEdition rows
  When the administrator opens /admin/archive
  Then POST /account/api/admin/archive/list returns 200 with an empty page (Total = 0)
  And the grid body renders the SimfEmptyState component titled
      "No archive editions yet." (Arabic: "لا توجد نسخ مؤرشفة بعد.")
  And the "Add edition" button is still visible above the empty state
  And no error toast appears
```

### E2E-ARC-003 — Auth gate

```gherkin
Scenario: Signed-in user without Archive.View is denied
  Given a user is signed in to the Control Panel
  And their role does not grant the "Archive.View" permission (and is not Administrator "*")
  When they navigate to /admin/archive
  Then the RequirePermission attribute redirects them to /not-permitted with HTTP 200
  And no POST /account/api/admin/archive/list request fires
  And the "Module.PreviousEditions" nav item is not rendered for them
```

### E2E-ARC-004 — Add modal defaults

```gherkin
Scenario: Add modal opens with sensible defaults
  Given the administrator is on /admin/archive
  When they click "Add edition"
  Then the "Add edition" modal opens (title "Add edition")
  And the Year number input is pre-set to the current UTC year (DateTime.UtcNow.Year)
  And Title (English), Title (Arabic) and both Summary fields are blank
  And there is no cover-image field, only the save-first hint
  And Attendees, Sessions and Speakers default to "0"
  And the "Active (visible in public archive)" checkbox is ticked
  And the footer shows "Cancel" and "Save"
  When they click "Cancel"
  Then the modal closes with no network request fired
```

### E2E-ARC-005 — Client-side title validation

```gherkin
Scenario: Blank English or Arabic title is blocked before any request
  Given the "Add edition" modal is open
  When the administrator leaves Title (English) blank
  And leaves Title (Arabic) blank
  And clicks "Save"
  Then a red SimfAlert toast appears reading
      "The English and Arabic titles are both required."
      (Arabic: "العنوان بالإنجليزية والعربية مطلوبان.")
  And the modal stays open
  And NO POST /account/api/admin/archive request fires (client guard short-circuits)
```

### E2E-ARC-006 — Server validation (year range)

```gherkin
Scenario: Year outside 2000–2100 is rejected by the API
  Given the "Add edition" modal is open
  When the administrator sets Year="1999"
  And fills Title (English)="Too old" and Title (Arabic)="قديم جدًا"
  And clicks "Save"
  Then the BFF forwards POST /account/api/admin/archive
  And the API returns HTTP 400 with ApiResult.Error.Code = "archive_edition_invalid"
  And the modal stays open
  And the error toast surfaces the bilingual MessageForCurrentCulture()
      "Year must be between 2000 and 2100." (Arabic: "يجب أن يكون العام بين 2000 و 2100.")
```

### E2E-ARC-007 — Duplicate year conflict

```gherkin
Scenario: A second edition for an existing year returns 409
  Given an ArchiveEdition for Year=2019 already exists
  When the administrator opens "Add edition"
  And sets Year="2019"
  And fills Title (English)="Duplicate 2019" and Title (Arabic)="مكرر 2019"
  And clicks "Save"
  Then the BFF forwards POST /account/api/admin/archive
  And the API returns HTTP 409 with ApiResult.Error.Code = "archive_edition_year_duplicate"
  And the modal stays open
  And the error toast surfaces the bilingual server message
      "An archive edition for year 2019 already exists."
      (Arabic: "توجد نسخة أرشيف للعام 2019 بالفعل.")
```

### E2E-ARC-008 — Edit pre-fill + IsActive toggle

```gherkin
Scenario: Edit pre-fills the row and can toggle public visibility off
  Given an active ArchiveEdition for Year=2021 exists with Attendees=900
  When the administrator clicks the 2021 row's Edit (pencil) action
  Then the "Edit edition" modal opens (title "Edit edition")
  And every field is pre-filled from the row (Year=2021, both titles, both summaries,
      Attendees=900, Sessions, Speakers)
  And an "Image" section hosts SimfImageUpload Category="ArchiveCover", showing
      the edition's current cover if one is attached
  And the "Active (visible in public archive)" checkbox reflects the row's IsActive=true
  When they untick the IsActive checkbox
  And change Attendees to "950"
  And click "Save"
  Then the BFF forwards PUT /account/api/admin/archive/{id} and the API returns HTTP 200
  And a green toast reads "Edition saved."
  And the 2021 row's Attendees column reads "950"
  And the 2021 row's Active pill reads "Inactive"
```

### E2E-ARC-009 — Delete confirm dialog cancel path

```gherkin
Scenario: Dismissing the delete confirm keeps the edition active
  Given an active ArchiveEdition for Year=2019 exists (Active pill = "Active")
  When the administrator clicks the 2019 row's Delete (trash) action
  Then the View/Delete form (CrudShell) opens showing the row's read-only details and a red "Deactivate" button
  When they click "Deactivate"
  Then a SimfConfirm dialog appears naming the edition
  When they dismiss / cancel the confirm dialog
  Then NO DELETE /account/api/admin/archive/{id} request fires
  And the 2019 row stays in the grid with its Active pill still reading "Active"
  And no toast appears
```

### E2E-ARC-010 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows fallback bilingual toast
  Given the API is configured to return 500 on POST /admin/archive/list (e.g. DB down)
  When the administrator opens /admin/archive
  Then the page shows the "Loading editions…" indicator first
  And then a red SimfAlert toast appears reading
      "Could not load archive editions. Please try again."
      (Arabic: "تعذّر تحميل النسخ المؤرشفة. يرجى المحاولة مرة أخرى.")
  And no grid rows render
  And the "Add edition" button is still available
```

### E2E-ARC-011 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/archive in English
  When they switch the UI language to Arabic ("العربية")
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "النسخ السابقة"
  And the "Add edition" button reads "إضافة نسخة"
  And the table headers render in Arabic (Year/Title/Attendees/Sessions/Speakers/Active)
  And the nav rail mirrors to the right edge

  When they click "إضافة نسخة"
  Then the "Add edition" modal opens in RTL
  And the field labels are Arabic (e.g. "العنوان (الإنجليزية)", "مُفعّل (ظاهر في الأرشيف العام)")
  And the footer "Cancel"/"Save" buttons appear in reverse order
```

### E2E-ARC-012 — Per-column filter narrows the grid

```gherkin
Scenario: Typing into the Title (English) column filter narrows the grid
  Given the grid shows editions for 2019, 2020 and 2021
  And the 2019 edition's Title (English) is "SIMF 2019 — Inaugural Forum"
  When the administrator opens the filter for column "Filter column Title (English)"
  And types "Inaugural"
  Then the grid issues POST /account/api/admin/archive/list with
      GridQuery.Filters["titleEn"]="Inaugural" and GridQuery.Skip reset to 0
  And the API returns only the 2019 row (case-insensitive Contains match on TitleEn)
  And the grid renders just that one row
  And the pager summary reflects the narrowed Total

  When they additionally type "2020" into "Filter column Title (Arabic)"
  Then the list call now carries both Filters["titleEn"]="Inaugural"
      and Filters["titleAr"]="2020"
  And the two per-column filters AND together (no matching row) so the grid
      renders the SimfEmptyState

  When they clear both column filters
  Then a fresh list call fires with empty Filters and the full grid returns
```

### E2E-ARC-013 — Column sort toggles

```gherkin
Scenario: Sorting by Year, then by Title (English), toggles ascending/descending
  Given the grid shows several editions across multiple years
  And the default order is Year descending (newest first)
  When the administrator clicks the "Year" column header
  Then the grid issues POST /account/api/admin/archive/list with
      GridQuery.Sort="year" and GridQuery.SortDescending=false
  And the rows reorder oldest-year-first
  When they click the "Year" header again
  Then the list call carries Sort="year" and SortDescending=true
  And the rows reorder newest-year-first

  When the administrator clicks the "Title (English)" column header
  Then the list call carries Sort="titleEn" and SortDescending=false
  And the rows reorder A→Z by English title
  When they click the "Title (English)" header again
  Then the list call carries Sort="titleEn" and SortDescending=true
  And the rows reorder Z→A
  And the "Title (Arabic)", Attendees, Sessions, Speakers and Active columns
      stay unsortable (no sort affordance, no Sort key sent for them)
```

### E2E-ARC-014 — Make this year history (snapshot, golden path)

```gherkin
Scenario: One-click snapshot creates the current-year edition with computed counts
  Given the administrator is on /admin/archive
  And the live event has some active sessions, active speakers, and gate CheckIn scans
  And no archive edition exists yet for the current UTC year
  When the administrator clicks "Make this year history"
  Then a confirm dialog opens titled "Archive the current event"
  And it shows the intro about auto-counted attendees/sessions/speakers
  And a single "Show in the archive now" checkbox (ticked by default)
  When they click "Create snapshot"
  Then the BFF forwards POST /account/api/admin/archive/snapshot-current and the API returns HTTP 200
  And the new edition's Year equals the current UTC year
  And its Title (English) is "SIMF {year}" and Title (Arabic) is "سيمف {year}"
  And its Attendees equals the distinct count of allowed CheckIn gate scans
  And its Sessions equals the active-session count and Speakers the active-speaker count
  And the dialog closes
  And a green toast reads "Archived the current event as {year}."
  And the grid reloads and shows the new "SIMF {year}" row with an "Active" pill
```

**Evidence:** `AdminArchiveTests.Snapshot_creates_current_year_edition_and_duplicate_409` (green).

### E2E-ARC-015 — Snapshot duplicate-year conflict

```gherkin
Scenario: A second snapshot of the same year is rejected
  Given an archive edition already exists for the current UTC year (a prior snapshot)
  When the administrator clicks "Make this year history" and confirms
  Then the API returns HTTP 409 with ApiResult.Error.Code = "archive_edition_year_duplicate"
  And the dialog stays open
  And the error toast surfaces "An archive edition for year {year} already exists."
      (Arabic: "توجد نسخة أرشيف للعام {year} بالفعل.")
```

**Evidence:** `AdminArchiveTests.Snapshot_creates_current_year_edition_and_duplicate_409` (the second call asserts 409) (green).

### E2E-ARC-016 — Snapshot permission gate

```gherkin
Scenario: Snapshot is forbidden without Archive.Snapshot
  Given a signed-in account that is not an Administrator and lacks "Archive.Snapshot"
  When it calls POST /api/v1/admin/archive/snapshot-current
  Then the API returns HTTP 403
  And in the Control Panel the "Make this year history" button is not rendered for that account
      (the <AuthorizedAction> hides it)
```

**Evidence:** `AdminArchiveTests.Snapshot_is_forbidden_for_a_non_admin` (green).

### E2E-ARC-017 — "Show in archive now" flips visibility

```gherkin
Scenario: The optional checkbox reveals the archive after the snapshot
  Given the archive-visibility toggle is currently off
  When the administrator runs "Make this year history" with "Show in the archive now" ticked
  Then after the 200 the archive-visibility toggle (D-166) is on
  And the public GET /api/v1/app/archive list now includes the new edition
  When instead the checkbox is unticked
  Then the snapshot still creates the edition but the visibility toggle is left unchanged
```

### E2E-ARC-018 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch to full-page mode and it persists across reload
  Given the administrator is on /admin/archive with the default "dialog" presentation
  And the grid toolbar's CustomToolbar shows the "Open as full page" toggle (maximize icon)
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.archive" holds {"v":1,"presentation":"page"}
  When they reload /admin/archive
  Then OnInitializedAsync calls Prefs.GetPresentationAsync("archive") and restores "page"
  And the toggle still reads "Open as dialog"
  And opening "Add edition" now renders the full-page frame (not a popup)
```

### E2E-ARC-019 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/View take over the content area; Save returns to the grid
  Given the presentation is set to "full page"
  When the administrator clicks "Add edition"
  Then the grid + SimfBanner are replaced by the CrudShell full-page frame
      (title "Add edition" + close header + the ArchiveAddEdit form)
  And there is no modal backdrop
  When they set Year="2018", Title (English)="SIMF 2018", Title (Arabic)="سيمف 2018"
  And they click "Save"
  Then the BFF forwards POST /account/api/admin/archive and the API returns HTTP 200
  And the page frame closes
  And the grid re-appears with the new row and a green "Edition saved." toast
  When they click the Edit (pencil) action and then the frame's close (X) button
  Then the ArchiveAddEdit form closes and the grid re-appears unchanged
```

### E2E-ARC-020 — Delete confirmation gate (CrudShell + SimfConfirm) (D-353)

```gherkin
Scenario: Deactivate requires explicit SimfConfirm confirmation
  Given the administrator is on /admin/archive
  And an active edition for Year=2019 (Title (English)="SIMF 2019 — Inaugural Forum") exists
  When they click the 2019 row's Delete (trash) action
  Then the ArchiveViewDelete form opens inside CrudShell showing the row's read-only details
  And a red "Deactivate" button is visible
  When they click "Deactivate"
  Then a SimfConfirm dialog appears titled "Delete edition" naming the edition
      ("Delete \"SIMF 2019 — Inaugural Forum\"? It will be hidden from the public archive.")
  When they click "Cancel" on the confirm
  Then NO DELETE /account/api/admin/archive/{id} request fires and the row is unchanged
  When they re-open Deactivate and click the confirm "Deactivate" button
  Then exactly one DELETE /account/api/admin/archive/{id} fires and the API returns HTTP 200
  And the form closes, a green "Edition deleted." toast appears
  And the 2019 row stays in the grid with its Active pill flipped to "Inactive"
```

### E2E-ARC-021 — Excel export (D-356)

```gherkin
Scenario: Export the filtered grid to an XLSX workbook
  Given the administrator is on /admin/archive with at least two editions
  When they click the toolbar "Export" action with no rows selected
  Then a POST /account/api/admin/archive/export fires carrying AdminGridExportRequest
      with an empty Ids list and the current Query (whole filtered grid)
  And the browser saves a file named simf-archive-{timestamp}.xlsx
  And the workbook's "Archive" sheet header row is
      Year | TitleEn | TitleAr | Attendees | Sessions | Speakers | IsActive
      | SummaryEn | SummaryAr | LocationEn | LocationAr
      | DateLabelEn | DateLabelAr
      (the trailing summary/location/date-label columns were appended in D-506 so
       the full edition round-trips through export + import; the cover column left
       the workbook in D-889, the cover being a StoredFile and not a typed string)
  When they instead select two rows then click "Export"
  Then the request carries those two Ids and a null Query
  And the workbook contains exactly those two editions
  And the API caps the export at 5000 rows
```

### E2E-ARC-022 — Excel import (D-356)

```gherkin
Scenario: Import editions from a workbook and see the per-row outcome
  Given the administrator is on /admin/archive
  When they click the toolbar "Import" action
  Then the hidden file input "archive-import-input" (accept=".xlsx") opens the OS picker
  When they choose an .xlsx whose "Archive" sheet has Year/TitleEn/TitleAr rows
      for two new years (e.g. 2016 and 2017)
  Then a POST /account/api/admin/archive/import fires as multipart form data
  And the import-result modal shows "2 created, 0 updated, 0 skipped."
  And a green toast reads the shared Grid.Import.Done message
  And the grid reloads and lists both new editions
  When they import a workbook containing one duplicate year (an existing edition)
      and one new year
  Then the modal shows 1 created and one per-row error naming the duplicate year
      (the service's archive_edition_year_duplicate 409 recorded as a row error,
       the batch is not aborted)
  And the import is insert-only (no existing edition is updated)
```

### E2E-ARC-023 — Excel import rejection (D-356)

```gherkin
Scenario: A bad or wrong-sheet upload is rejected without creating anything
  Given the administrator is on /admin/archive
  When they import a file that is not a valid .xlsx (fails the ZIP-magic / 5MB gate)
  Then the request returns HTTP 400 and the page shows a bilingual error toast
  And no archive edition is created
  When they import a workbook whose sheet is not named "Archive"
      (or is missing a required header: Year, TitleEn or TitleAr)
  Then the request returns HTTP 400 with the bilingual "worksheet named 'Archive'" /
      required-header message
  And no archive edition is created
```

---

### E2E-ARC-026 — the list shows the edition's cover thumbnail (D-357)

```gherkin
Scenario: the English-title column renders a thumbnail when the edition has a cover
  Given an Administrator is on /admin/archive
  And edition "A" has an ArchiveCover asset and edition "B" has none
  When the grid loads a page
  Then A's title cell shows the cover thumbnail beside the title
  And B's title cell shows a tinted initials tile (never a broken image)
  And sorting / filtering by the title column still works (column key unchanged)
```

**Covered (lower layer):** the flag-population path is proven by
`tests/SIMF.Api.Tests/ContactsTests.cs` →
`Admin_list_flips_HasLogo_once_a_CompanyLogo_asset_is_attached`; Archive uses the
identical owner=row.Id `WhichOwnersHaveActiveAssetAsync(ArchiveCover, ...)`
restructure. Confirm the render visually in the Chrome DevTools MCP smoke.

---

## Implementation notes

- **Manual smoke is canonical today.** Until a Playwright project exists, the
  canonical "run" is a Chrome DevTools MCP session: sign in per the Background,
  drive each scenario, and capture screenshots into
  `docs/screenshots/cp-admin-archive-*.png`.
- **Convert to Playwright** when the runner is adopted: each Gherkin scenario
  maps to a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be
  created) plus a step-definition class. The shape is already runner-agnostic.
- **API integration tests** at `tests/SIMF.Api.Tests/AdminArchiveTests.cs`
  cover the same surface at a lower layer (no browser):
  `Admin_create_then_get_roundtrips`, `Admin_create_and_list_contains_edition`,
  `Create_duplicate_year_returns_409`, `Non_admin_caller_is_forbidden_on_create`,
  `Admin_create_roundtrips_location_and_date_label` (D-273), and — for the
  "make this year history" action (D-275) —
  `Snapshot_creates_current_year_edition_and_duplicate_409` +
  `Snapshot_is_forbidden_for_a_non_admin`.
  `tests/SIMF.Api.Tests/ArchiveTests.cs` covers the public anonymous
  `GET /archive` projection. When an E2E scenario reliably covers one of these,
  the lower-layer case can be retired — but keep both during the transition.
- **Permission/seed gate.** `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`
  and `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` already assert the
  `Archive.*` codes gate both nav + endpoints; E2E-ARC-003 is the browser-level
  proof of the same gate.
- **Excel export/import (D-356).** The grid wires `OnExport`/`OnImport` to the
  shared `CrudGridExcel` (`Resource="archive"`), which posts the generic
  `/account/api/admin/archive/export|import` BFF routes. The API endpoints live in
  `src/Backend/SIMF.Api/Endpoints/Admin/ArchiveExcelEndpoints.cs`
  (`ExportArchiveEndpoint` gated by `Archive.Export`; `ImportArchiveEndpoint`
  gated by `Archive.Import`, insert-only, required headers `Year/TitleEn/TitleAr`,
  duplicate-year 409 recorded as a per-row error). **D-506** appended the dropped
  edition columns (`SummaryEn/SummaryAr/LocationEn/LocationAr/DateLabelEn/DateLabelAr`;
  the cover column was removed again by D-889)
  to both the export `_columns` (after `IsActive`) and the import `ApplyRowAsync`
  read set, so the full edition now survives a round-trip; required headers are
  unchanged (the new columns are optional). Lower-layer coverage:
  `tests/SIMF.Api.Tests/ArchiveExcelTests.cs` (`Export_includes_the_dropped_edition_columns`,
  `Import_round_trips_the_dropped_edition_fields`). E2E-ARC-021..023 + E2E-ARC-025
  are the browser-level proof.
- **Delete is CrudShell + SimfConfirm (D-353), not a native confirm().** The
  earlier description of a native `confirm()` dialog on the row's Delete action is
  superseded — Delete now opens the `ArchiveViewDelete` form inside `CrudShell`
  and the destructive call is gated by a `SimfConfirm` dialog. E2E-ARC-001,
  E2E-ARC-009 and E2E-ARC-020 reflect the shipped behaviour.

### E2E-ARC-024 — Cover Image via the unified media-asset pipeline (D-357)

```gherkin
Scenario: Upload cover image, then switch it to an external link
  Given an Administrator is editing an archive edition
  When they open the "Image" control, choose "Upload file", pick a PNG and click Upload
  Then a success message shows and the preview thumbnail refreshes
  And GET /account/api/admin/assets/ArchiveCover/{ownerId}/image returns the bytes (200)
  And /admin/media-library lists it as ArchiveCover - this entity - Image - Uploaded file - active
  When they switch to "External link", enter https://cdn.example/x.jpg and click Save link
  Then the asset Source becomes "External link" and GET /app/assets/ArchiveCover/{ownerId}/image 302s to that URL
  And the same-origin /content/assets/ArchiveCover/{ownerId}/image proxy serves it for any public page that renders this edition
```

**Evidence:** the Asset DB row + the out-of-row file (or stored link); the Media Library row;
0 console errors; audit `AssetUploaded` then `AssetLinked`. Validation: a non-image / over-5 MB /
video upload is 400; deactivate->restore round-trips; restoring when a live (category,owner) asset
already exists is 409 (covered by `tests/SIMF.Api.Tests/AssetEndpointsTests.cs`).

### E2E-ARC-025 — Excel round-trip of the dropped edition fields (D-506)

```gherkin
Scenario: Summary, location and date label survive export + import
  Given the administrator is on /admin/archive
  And an edition exists with SummaryEn/SummaryAr, LocationEn/LocationAr and
      DateLabelEn/DateLabelAr all set
  When they click the toolbar "Export" action
  Then the "Archive" sheet header row carries the appended columns
      SummaryEn | SummaryAr | LocationEn | LocationAr
      | DateLabelEn | DateLabelAr (after Year..IsActive)
  And that edition's data row holds the real summary / location / date-label
      values (not blanks)
  And the workbook carries no cover column at all — a stale workbook naming one
      fails loudly rather than silently dropping the image

  When they import a workbook whose "Archive" sheet carries those same columns
      for a brand-new year
  Then a POST /account/api/admin/archive/import creates the row (0 errors)
  And the created edition's grid summary (and GET detail) carries every one of
      SummaryEn, SummaryAr, LocationEn, LocationAr,
      DateLabelEn and DateLabelAr — none are dropped at the IO boundary
  And an absent (omitted) optional column simply stays null
```

**Evidence:** `tests/SIMF.Api.Tests/ArchiveExcelTests.cs` —
`Export_includes_the_dropped_edition_columns` +
`Import_round_trips_the_dropped_edition_fields` (green).

---

_Last reviewed:_ 2026-06-26 by SIMF Team (D-506 — Excel export/import now round-trips the dropped edition fields: E2E-ARC-025; export header list in ARC-021 + the Excel implementation note refreshed). Prior: 2026-06-10 (D-356 Phase 5 — Excel export/import + D-353 Page↔Popup toggle: E2E-ARC-018..023; corrected the stale native-confirm() delete copy in ARC-001/ARC-009).
