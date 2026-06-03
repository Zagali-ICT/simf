# E2E test catalogue — Previous Editions (Archive) CRUD (`/admin/archive`)

| | |
|--|--|
| **Page** | [`cp/admin-archive.md`](../../pages/cp/admin-archive.md) |
| **Route** | `/admin/archive` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (canonical SIMF browser smoke). Convertible to Playwright later — keep scenario steps tool-agnostic. |
| **Auth setup** | `superadmin@zagali-ict.com` / `Aa@123456789` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-03 |

> **Permission gate.** The page carries `@attribute [RequirePermission(PermissionCatalog.Archive.View)]`
> (`"Archive.View"`). The backing API endpoints are gated per action:
> `Archive.View` (list + get), `Archive.Create` (POST), `Archive.Edit` (PUT),
> `Archive.Delete` (DELETE). `Administrator = "*"` satisfies all four. The CP
> nav item `Module.PreviousEditions` is gated by `Archive.View`.
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
  And they fill Cover image path="archive/2019/cover.jpg"
  And they click "Save"
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
  Then a native confirm() dialog appears reading
      "Delete this edition? It will be removed from the public archive immediately."
  When they accept the dialog
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
  And Title (English), Title (Arabic), both Summary fields and Cover image path are blank
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
      Attendees=900, Sessions, Speakers, Cover image path)
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
  Then a native confirm() dialog appears reading
      "Delete this edition? It will be removed from the public archive immediately."
  When they dismiss / cancel the dialog
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
  `Create_duplicate_year_returns_409`, and `Non_admin_caller_is_forbidden_on_create`.
  `tests/SIMF.Api.Tests/ArchiveTests.cs` covers the public anonymous
  `GET /archive` projection. When an E2E scenario reliably covers one of these,
  the lower-layer case can be retired — but keep both during the transition.
- **Permission/seed gate.** `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`
  and `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` already assert the
  `Archive.*` codes gate both nav + endpoints; E2E-ARC-003 is the browser-level
  proof of the same gate.

---

_Last reviewed:_ 2026-06-03 by Claude (E2E catalogue rebuild) (D-256/D-257 grid affordances reconciled).
