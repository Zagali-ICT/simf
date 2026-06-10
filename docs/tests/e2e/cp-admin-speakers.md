# E2E test catalogue — Speakers CRUD (`/admin/speakers`)

| | |
|--|--|
| **Page** | [`cp/admin-speakers.md`](../../pages/cp/admin-speakers.md) |
| **Route** | `/admin/speakers` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **Page surface (read from `SpeakersList.razor` + `SpeakersAddEdit.razor` +
> `SpeakersViewDelete.razor`):** a
> `SimfDataGrid` of speakers with columns Code, Name, Name (Arabic), Rank,
> Country, Display order, Active; toolbar **Add speaker**; per-row **Edit**,
> **Details**, **Deactivate** icons; multiselect checkboxes; the column filter
> (Name), sort (Code, Name, Display order), and the pager (First / Prev / Next /
> Last / page size). The toolbar also carries the **D-353 presentation toggle**
> (`CrudPresentationToggle PageKey="speakers"` — "Open as full page" / "Open as
> dialog") and the **D-356** Excel **Export** + **Import** actions wired through
> `CrudGridExcel Resource="speakers"`. Add / Edit / Details / Deactivate are now
> framed by a **`CrudShell`** (popup or full page per the toggle) hosting the
> reusable `SpeakersAddEdit` and `SpeakersViewDelete` forms — Deactivate opens the
> View/Delete form whose red **Deactivate** button is gated by a **`SimfConfirm`**
> dialog (no longer a one-click list delete). The Add/Edit form has these fields:
> **Code** (2–16, required, upper-cased), **Name (English)** (1–128, required),
> **Name (Arabic)** (1–128, required), **Rank / title** (≤64), **Country**
> (picker, optional — loaded from `/account/api/admin/countries/list`), bilingual
> **Bio** (≤2048), **Qualifications** (≤1024), **Training & experience** (≤1024),
> **Awards** (≤1024) textareas, **Allows meeting requests** + **Allows data
> sharing** checkboxes, **Facebook / LinkedIn / X URL** (≤256 each), **Display
> order** (≥0 integer), and — Edit only — an **Active** checkbox.
>
> **Permission gate:** `@attribute [RequirePermission(PermissionCatalog.Speakers.View)]`
> (`Speakers.View`). The four CRUD actions map to `Speakers.View` /
> `Speakers.Create` / `Speakers.Edit` / `Speakers.Delete`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-SPK-001 | Full CRUD round-trip — Add → Edit → Details → Deactivate | happy | P0 | _to author_ |
| E2E-SPK-002 | Add with country + social URLs + all bilingual fields persists & round-trips | happy | P1 | _to author_ |
| E2E-SPK-003 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-SPK-004 | Auth gate — signed-in admin lacking `Speakers.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-SPK-005 | Filter by Name narrows the grid | function | P1 | _to author_ |
| E2E-SPK-006 | Sort by Code / Name / Display order reorders the grid | function | P2 | _to author_ |
| E2E-SPK-007 | Pager (page size + Next/Prev/First/Last) walks pages | function | P2 | _to author_ |
| E2E-SPK-008 | Details modal renders all read-only fields then closes | function | P1 | _to author_ |
| E2E-SPK-009 | Edit "Active" checkbox toggles the public visibility pill | function | P1 | _to author_ |
| E2E-SPK-010 | Validation — empty Code / Name / Arabic name → bilingual modal error | error | P1 | _to author_ |
| E2E-SPK-011 | Validation — Code length 1 or 17, Display order negative → bilingual error | error | P1 | _to author_ |
| E2E-SPK-012 | Conflict — duplicate Code → 409 `SPEAKER_CODE_DUPLICATE` | error | P1 | _to author_ |
| E2E-SPK-013 | Country picker — invalid / inactive country id → 400 `SPEAKER_INVALID` | error | P2 | _to author_ |
| E2E-SPK-014 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-SPK-015 | Deactivate is idempotent — re-deactivate inactive speaker still succeeds | resilience | P2 | _to author_ |
| E2E-SPK-016 | RTL / Arabic render mirrors page, grid + Add modal | i18n | P1 | _to author_ |
| E2E-SPK-017 | Presentation toggle persists across reload (localStorage `simf.cp.prefs.speakers`) (D-353) | happy | P1 | _to author_ |
| E2E-SPK-018 | Full-page mode round-trip — Add/Edit/View take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-SPK-019 | Delete confirmation gate — Deactivate opens View/Delete → SimfConfirm gates the call (D-353) | error | P0 | _to author_ |
| E2E-SPK-020 | Excel export — toolbar Export downloads an .xlsx of the filtered grid / selected rows (D-356) | happy | P1 | _to author_ |
| E2E-SPK-021 | Excel import — upload a workbook → rows created + result modal with per-row outcome (D-356) | happy | P1 | _to author_ |
| E2E-SPK-022 | Excel import rejection — non-.xlsx / wrong-sheet upload → bilingual rejection, nothing created (D-356) | error | P1 | _to author_ |

## Scenarios

### E2E-SPK-001 — Full CRUD round-trip

```gherkin
Feature: Speakers CRUD round-trip
  As an Administrator with the Speakers permissions
  I want to add, edit, view and deactivate a programme speaker
  So that the public speaker list stays accurate to the event programme

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in as superadmin@zagali-ict.com via /login + /login/totp
    using a fresh code from the PowerShell Get-Totp helper
  And they have landed on /admin/speakers
  And the grid has finished loading (no "Loading speakers…" indicator)

Scenario: Create, edit, view, deactivate one speaker
  Given the grid currently shows {N} rows
  When the administrator clicks "Add speaker"
  Then the Add modal opens titled "Add speaker"
  And it shows the Code, Name (English), Name (Arabic), Rank / title, Country,
    Bio, Qualifications, Training & experience, Awards (each bilingual),
    Allows meeting requests, Allows data sharing, Facebook/LinkedIn/X URL,
    and Display order fields
  And no "Active" checkbox is shown (Add mode hides it)

  When they fill Code="SPK-001"
  And they fill Name (English)="Rear Admiral John Carter"
  And they fill Name (Arabic)="العميد البحري جون كارتر"
  And they fill Display order="10"
  And they click "Create speaker"
  Then the BFF forwards POST /account/api/admin/speakers and returns HTTP 200
  And the API stores Code upper-cased as "SPK-001"
  And the modal closes
  And the grid shows {N + 1} rows
  And a green toast reads 'Speaker "Rear Admiral John Carter" was created.'
  And a row exists with Code="SPK-001", Name="Rear Admiral John Carter",
    Display order=10 and the green "Active" pill

  When the administrator clicks the "Edit" icon on that row
  Then a GET /account/api/admin/speakers/{id} returns HTTP 200
  And the Edit modal opens titled "Edit speaker" with the row's values pre-filled
  And an additional "Active — show in the public speaker list" checkbox is visible (ticked)
  When they change Rank / title to "Vice Admiral"
  And they change Display order to "0"
  And they click "Save changes"
  Then a PUT /account/api/admin/speakers/{id} returns HTTP 200
  And the modal closes
  And a green toast reads 'Speaker "Rear Admiral John Carter" was updated.'
  And the row's Rank column reads "Vice Admiral" and Display order reads "0"

  When the administrator clicks the "Details" icon on that row
  Then a read-only modal opens titled "Speaker details"
  And it renders Code, Name, Name (Arabic), Rank="Vice Admiral", Country="—",
    and the bilingual Bio/Qualifications/Training/Awards rows ("—" where blank)
  When they click "Close"
  Then the modal closes

  When the administrator clicks the "Deactivate" icon on that row
  Then the View/Delete form opens (dialog by default) showing the row's read-only details
  And a red "Deactivate" button is visible (D-353 — no longer a one-click list delete)
  When they click "Deactivate" and confirm the SimfConfirm dialog
  Then a DELETE /account/api/admin/speakers/{id} returns HTTP 200
  And a green toast reads 'Speaker "Rear Admiral John Carter" was deactivated.'
  And the row remains visible but its pill changes to the grey "Inactive" pill
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-speakers-001-before.png` (grid baseline)
- Screenshots: `docs/screenshots/cp-admin-speakers-001-{add-modal,edit-modal,details-modal,after}.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/speakers/*` call returns 200
- Audit rows: `Speaker.Created`, `Speaker.Updated`, `Speaker.Deactivated`
  (`AuditEvents`) each with the actor's user id in `ActorUserId`

### E2E-SPK-002 — Add with full payload (country + social URLs + bilingual)

```gherkin
Scenario: A fully-populated speaker round-trips through Details intact
  Given at least one active Country exists (the picker is loaded from
    /account/api/admin/countries/list)
  When the administrator opens the Add modal
  And fills Code="SPK-002"
  And fills Name (English)="Dr Sarah Lin"
  And fills Name (Arabic)="د. سارة لين"
  And fills Rank / title="Chief Scientist"
  And selects a Country from the picker (e.g. "Saudi Arabia (SA)")
  And fills Bio (English)="Marine robotics researcher."
  And fills Bio (Arabic)="باحثة في الروبوتات البحرية."
  And fills Qualifications (English)="PhD, Naval Architecture"
  And fills Facebook URL="https://facebook.com/sarah.lin"
  And fills LinkedIn URL="https://linkedin.com/in/sarahlin"
  And fills X URL="https://x.com/sarahlin"
  And ticks "Allows meeting requests"
  And ticks "Allows data sharing"
  And fills Display order="5"
  And clicks "Create speaker"
  Then the POST returns HTTP 200 and the modal closes
  And the grid row shows Country resolved to the selected country's localized name
  When the administrator opens the Details modal for that row
  Then Country, Bio, Qualifications, Facebook/LinkedIn/X URL, Allows meeting
    requests = "Yes", Allows data sharing = "Yes" all render the saved values
```

### E2E-SPK-003 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no Speaker rows
  When the administrator opens /admin/speakers
  Then the grid body renders the SimfEmptyState component
  And it shows the bilingual copy "No speakers yet." / "لا يوجد متحدّثون بعد."
  And the toolbar still shows the "Add speaker" button
  And no error toast appears
```

### E2E-SPK-004 — Auth gate

```gherkin
Scenario: Signed-in admin lacking Speakers.View is denied
  Given a signed-in Control-Panel user whose role does NOT include the
    Speakers.View permission (and is not the Administrator wildcard "*")
  When they navigate to /admin/speakers
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/speakers/list request fires
  And the "Speakers" item is absent from the nav rail
    (CpNavigation RequiredPermission = Speakers.View)
```

### E2E-SPK-005 — Filter by Name

```gherkin
Scenario: Column filter narrows the grid to matching names
  Given the grid shows speakers including "Rear Admiral John Carter" and "Dr Sarah Lin"
  When the administrator types "Sarah" into the Name column filter
  Then a POST /account/api/admin/speakers/list fires with the search term
  And the grid shows only rows whose Code, Name or Arabic name contains "Sarah"
  And the summary footer reflects the reduced total
  When they clear the filter
  Then the grid reloads the full list
```

### E2E-SPK-006 — Sort

```gherkin
Scenario: Sortable columns reorder the grid server-side
  Given the grid shows several speakers
  When the administrator clicks the "Code" column header
  Then a POST /account/api/admin/speakers/list fires with Sort="code"
  And rows are ordered ascending by Code
  When they click "Code" again
  Then rows are ordered descending by Code (SortDescending=true)
  When they click the "Display order" header
  Then rows are ordered by Display order (the default secondary sort is Name)
  And the "Name (Arabic)", "Rank", "Country" and "Active" columns are NOT sortable
```

### E2E-SPK-007 — Pager

```gherkin
Scenario: Pager walks pages and respects page size
  Given more than 20 active speakers exist (default Top = 20)
  When the administrator opens /admin/speakers
  Then the grid shows the first 20 rows and the page indicator reads page 1
  When they click "Next"
  Then a POST /account/api/admin/speakers/list fires with Skip=20
  And page 2 rows render
  When they click "First"
  Then page 1 rows render again (Skip=0)
  When they change the page size to a larger value
  Then a list call fires with the new Top and more rows render
```

### E2E-SPK-008 — Details modal

```gherkin
Scenario: Details modal is read-only and closes cleanly
  Given a speaker row exists
  When the administrator clicks its "Details" icon
  Then GET /account/api/admin/speakers/{id} returns HTTP 200
  And a modal titled "Speaker details" opens
  And every field is rendered in a description list (dl/dt/dd) with no inputs:
    Code, Name, Name (Arabic), Rank, Country, Bio (En/Ar), Qualifications (En/Ar),
    Training & experience (En/Ar), Awards (En/Ar), Allows meeting requests,
    Allows data sharing, Facebook/LinkedIn/X URL, Display order, Active
  And blank optional fields render the em dash "—"
  When they click "Close"
  Then the modal closes and no save/network call fires
```

### E2E-SPK-009 — Active toggle controls public visibility

```gherkin
Scenario: Un-ticking Active in Edit deactivates the speaker
  Given an active speaker row showing the green "Active" pill
  When the administrator opens its Edit modal
  And un-ticks "Active — show in the public speaker list"
  And clicks "Save changes"
  Then the PUT returns HTTP 200 with IsActive=false
  And a green toast reads 'Speaker "{name}" was updated.'
  And the row's pill changes to the grey "Inactive" pill
  When they re-open Edit and re-tick "Active" and save
  Then the pill returns to the green "Active" pill
```

### E2E-SPK-010 — Required-field validation (client-side)

```gherkin
Scenario: Blank required fields show a bilingual error in the modal, no POST
  Given the Add modal is open
  When the administrator leaves Code blank and clicks "Create speaker"
  Then a SimfAlert error appears at the top of the modal
  And reads "Code must be between 2 and 16 characters." /
    "يجب أن يتراوح طول الرمز بين 2 و 16 حرفاً."
  And the modal stays open and no POST /account/api/admin/speakers request fires

  When they fill Code="SPK-010" but leave Name (English) blank and submit
  Then the error reads "English name is required (1–128 characters)." /
    "الاسم بالإنجليزية مطلوب (1–128 حرفاً)."

  When they fill Name (English) but leave Name (Arabic) blank and submit
  Then the error reads "Arabic name is required (1–128 characters)."
```

### E2E-SPK-011 — Bounds validation

```gherkin
Scenario: Out-of-range Code length and negative Display order are rejected
  Given the Add modal is open with Name + Arabic name filled
  When the administrator fills Code="A" (1 char) and submits
  Then the bilingual "Code must be between 2 and 16 characters." error shows
    and no POST fires (client guard: length is < 2 or > 16)
  When they fill Code="SPK-011" and Display order="-1" and submit
  Then the error reads "Display order must be zero or a positive integer." /
    "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً."
  And no POST fires
```

### E2E-SPK-012 — Duplicate Code conflict

```gherkin
Scenario: Duplicate Code returns 409 with the bilingual server message
  Given a speaker with Code="SPK-001" already exists
  When the administrator opens the Add modal
  And fills Code="spk-001" (the API upper-cases to "SPK-001")
  And fills Name (English)="Duplicate Test" + Name (Arabic)="اختبار مكرر"
  And fills Display order="0"
  And clicks "Create speaker"
  Then the BFF forwards POST /admin/speakers
  And the API returns HTTP 409 with ApiResult.Error.Code = "SPEAKER_CODE_DUPLICATE"
  And the modal stays open
  And the SimfAlert surfaces the bilingual MessageForCurrentCulture():
    "A speaker with code 'SPK-001' already exists." /
    "يوجد متحدّث بالرمز 'SPK-001' بالفعل."
```

### E2E-SPK-013 — Invalid / inactive country

```gherkin
Scenario: A country id that is missing or inactive is rejected server-side
  Given the administrator submits Add or Edit with a CountryId that does not
    exist or whose Country.IsActive = false (e.g. via a stale picker option)
  When the request reaches the API
  Then it returns HTTP 400 with ApiResult.Error.Code = "SPEAKER_INVALID"
  And the SimfAlert surfaces "Country id '{id}' does not exist or is inactive." /
    "رقم البلد '{id}' غير موجود أو غير مفعّل."
  And the modal stays open
```

### E2E-SPK-014 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is made to return 500 on /admin/speakers/list (e.g. DB down)
  When the administrator opens /admin/speakers
  Then the grid first shows the "Loading speakers…" indicator
  And then a red toast appears reading "The speakers could not be loaded." /
    "تعذّر تحميل المتحدّثين."
  And no rows render
```

### E2E-SPK-015 — Idempotent deactivate

```gherkin
Scenario: Deactivating an already-inactive speaker still succeeds
  Given a speaker row whose pill is the grey "Inactive" pill
  When the administrator clicks its "Deactivate" icon, then "Deactivate" in the
    View/Delete form, then confirms the SimfConfirm dialog (D-353)
  Then DELETE /account/api/admin/speakers/{id} returns HTTP 200 (service is idempotent;
    it early-returns when IsActive is already false and writes no second audit row)
  And a green toast reads 'Speaker "{name}" was deactivated.'
  And the row stays inactive
```

### E2E-SPK-016 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors page, grid and Add modal
  Given the administrator is on /admin/speakers in English
  When they switch the language to "العربية" in the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "المتحدّثون"
  And the grid column headers, toolbar buttons and pager arrows mirror to RTL
  And the Country column renders the Arabic country name
  When they click "إضافة متحدّث"
  Then the Add modal opens in RTL with Arabic field labels
    (الرمز, الاسم بالإنجليزية, الاسم بالعربية, …)
  And the form actions appear in reverse order
  And submitting a blank Code shows the Arabic error
    "يجب أن يتراوح طول الرمز بين 2 و 16 حرفاً."
```

### E2E-SPK-017 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch to full-page mode and it persists across reload
  Given the administrator is on /admin/speakers with the default "dialog" presentation
  And the grid toolbar shows the "Open as full page" toggle (maximize icon)
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.speakers" holds {"v":1,"presentation":"page"}
  When they reload /admin/speakers
  Then OnInitializedAsync re-reads the preference via Prefs.GetPresentationAsync("speakers")
  And the toggle still reads "Open as dialog"
  And opening "Add speaker" now renders the full-page frame (not a popup)
```

### E2E-SPK-018 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/View take over the content area; Save returns to the grid
  Given the presentation is set to "full page" (toggle reads "Open as dialog")
  When the administrator clicks "Add speaker"
  Then the grid + SimfBanner are hidden (GridHidden) and the CrudShell renders the
    full-page frame titled "Add speaker" hosting SpeakersAddEdit
  And there is no modal backdrop
  When they fill Code="SPK-018", Name (English)="Captain Ada Reyes",
    Name (Arabic)="النقيب آدا رييس", Display order="3" and click "Create speaker"
  Then a POST /account/api/admin/speakers returns HTTP 200
  And the page frame closes (CloseForm) and the grid re-appears with the new row
    and the green toast 'Speaker "Captain Ada Reyes" was created.'
  When they click the "Edit" icon and then the frame's "Close" (X) header button
  Then the form closes and the grid re-appears unchanged (no PUT fires)
```

### E2E-SPK-019 — Delete confirmation gate (D-353)

```gherkin
Scenario: Deactivate requires explicit confirmation via SimfConfirm
  Given the administrator is on /admin/speakers
  When they click the "Deactivate" icon on a speaker row
  Then GET /account/api/admin/speakers/{id} returns HTTP 200
  And the SpeakersViewDelete form opens (dialog by default) showing the read-only
    description list and a red "Deactivate" button
  When they click "Deactivate"
  Then a SimfConfirm dialog appears titled "Deactivate speaker" naming the speaker
    (Admin.Speakers.Delete.Message — "Deactivate speaker \"{name}\"? …")
  And it is a Danger confirm that cannot be dismissed by a backdrop click
  When they click "Cancel"
  Then no DELETE request fires and the row is unchanged
  When they re-open and click "Deactivate" then the confirm "Deactivate" button
  Then exactly one DELETE /account/api/admin/speakers/{id} fires and returns HTTP 200
  And the form closes and a green toast reads 'Speaker "{name}" was deactivated.'
  And the row's pill turns grey "Inactive"
```

### E2E-SPK-020 — Excel export (D-356)

```gherkin
Scenario: Export the filtered grid (or selected rows) to an XLSX workbook
  Given the administrator is on /admin/speakers with at least two speakers
  And they hold the Speakers.Export permission
  When they click the toolbar "Export" action with no rows selected
  Then OnExportAsync calls _excel.ExportAsync with an empty ids list
  And a POST /account/api/admin/speakers/export fires carrying
    AdminGridExportRequest { Ids: [], Query: <current GridQuery> }
  And the browser saves a file named simf-speakers-{yyyyMMddHHmmss}.xlsx
  And the workbook's "Speakers" sheet header row is
    Code | Name | NameArabic | Rank | Country | DisplayOrder | IsActive
  When they instead select two rows then click "Export"
  Then the request carries those two Ids and a null Query
  And the workbook contains exactly those two speaker rows
  And the whole-grid export is capped at 5000 rows server-side
```

### E2E-SPK-021 — Excel import (D-356)

```gherkin
Scenario: Import speakers from a workbook and see the per-row outcome
  Given the administrator is on /admin/speakers and holds Speakers.Import
  When they click the toolbar "Import" action
  Then TriggerImportAsync clicks the hidden file input id "speakers-import-input"
    (accept=".xlsx")
  When they choose an .xlsx whose "Speakers" sheet has the required headers
    Code, Name, NameArabic (Rank + DisplayOrder optional) for two new speakers
  Then a POST /account/api/admin/speakers/import fires as multipart form data
  And each row is created insert-only via AdminCreateSpeakerRequest (Code upper-cased;
    Country, bilingual rich-text, social URLs + consent flags are NOT imported)
  And the import-result modal titled "Import results" shows "2 created, 0 updated, 0 skipped."
  And the shared green "Import complete." toast (Grid.Import.Done) appears
  And the grid reloads and lists both new speakers
  When they import a workbook containing one duplicate Code and one new speaker
  Then the modal shows "1 created, 0 updated, 0 skipped." with one row error
    "Row {n} ({CODE}): A speaker with code '{CODE}' already exists."
    (SPEAKER_CODE_DUPLICATE — one bad row never aborts the batch)
```

### E2E-SPK-022 — Excel import rejection (D-356)

```gherkin
Scenario: A bad or wrong-sheet upload is rejected without creating anything
  Given the administrator is on /admin/speakers
  When they import a file that is not a valid .xlsx (fails the ZIP-magic check)
  Then the API returns HTTP 400 with the bilingual DataValidationException
    "The file is not a valid Excel workbook." / "الملف ليس مصنف Excel صالحًا."
  And CrudGridExcel raises OnError and the page shows that red error toast
  And no speaker is created
  When they import a workbook whose sheet is not named "Speakers"
    (or is missing a required header Code/Name/NameArabic)
  Then the parse fails with a 400 bilingual message and nothing is created
  When they import an .xlsx larger than 5 MB
  Then the API returns HTTP 413 "The Excel file is too large. The maximum is 5 MB."
    and nothing is created
```

---

## Implementation notes

- **Manual smoke as canonical-source-of-truth today.** Until Playwright is
  adopted, the canonical "run" of these scenarios is a Chrome DevTools MCP
  session — sign in as `superadmin@zagali-ict.com` with a fresh `Get-Totp`
  code, walk each scenario, and capture screenshots into
  `docs/screenshots/cp-admin-speakers-{scenario}.png`.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin
  scenario into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be
  created) plus a step-definition class. The Gherkin shape is already
  runner-agnostic.
- **API integration tests** at
  [`tests/SIMF.Api.Tests/AdminSpeakersTests.cs`](../../../tests/SIMF.Api.Tests/AdminSpeakersTests.cs)
  cover the same surface at a lower layer (no browser): create / get / list /
  update / deactivate, the `SPEAKER_INVALID` bounds checks (code 2–16, names
  1–128, display order ≥ 0, social URL ≤ 256), the `SPEAKER_CODE_DUPLICATE`
  409 on create + update, the `SPEAKER_NOT_FOUND` 404, the country-validity
  check, and the idempotent deactivate. Keep both layers during the
  transition; an E2E scenario that fully covers a case may later retire its
  matching `Api.Tests` case.
- **Permission/auth.** The page is gated by `PermissionCatalog.Speakers.View`;
  the four CRUD endpoints are additionally gated by `Speakers.Create` /
  `Speakers.Edit` / `Speakers.Delete` plus
  `AuthorizationPolicies.RequireApprovedAccount`. `PermissionEnforcementTests`
  and `CpNavigationPermissionTests` fail the build if a gate is missing.
- **Error codes** (from `ErrorCodes.cs`): `SPEAKER_INVALID` (400),
  `SPEAKER_NOT_FOUND` (404), `SPEAKER_CODE_DUPLICATE` (409). Create / update /
  deactivate are rate-limited via the `"auth"` limiter.
- **D-353 framing + D-356 Excel (Phase 5).** Add / Edit / Details / Deactivate
  are hosted by `CrudShell` (popup or full page, `CrudPresentationToggle
  PageKey="speakers"`, persisted in localStorage `simf.cp.prefs.speakers`).
  Deactivate now runs through `SpeakersViewDelete` + a `SimfConfirm` Danger
  dialog (no native `window.confirm`). Excel export
  (`POST /account/api/admin/speakers/export`, gated by `Speakers.Export`) and
  import (`POST /account/api/admin/speakers/import`, gated by `Speakers.Import`)
  go through the shared `CrudGridExcel Resource="speakers"`. Export columns:
  Code, Name, NameArabic, Rank, Country, DisplayOrder, IsActive (sheet
  "Speakers"); import is **insert-only** with required headers Code / Name /
  NameArabic (Rank + DisplayOrder optional; Country, rich-text, social URLs and
  consent flags are deliberately not imported). Both cap at 5000 rows; import
  rejects a non-`.xlsx` (ZIP-magic, 400) and an over-5 MB upload (413). API
  integration coverage: `tests/SIMF.Api.Tests/SpeakersExcelTests.cs`.
- **Known gap (out of scope here).** The English resx is **missing**
  `Admin.Speakers.Delete.Title` and `Admin.Speakers.Delete.Message` (both exist
  only in `Strings.ar.resx`), so the EN SimfConfirm title/body fall back to the
  resource keys until added. Flagged for a separate resx fix.

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle).
