# E2E test catalogue — Programme days (`/admin/programme-days`)

| | |
|--|--|
| **Page** | [`cp/admin-programme-days.md`](../../pages/cp/admin-programme-days.md) |
| **Route** | `/admin/programme-days` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-19 (D-452 — programme days + logo) |

> **Page background.** D-452 (Figma 883:2308 "تفاصيل اليوم") — CP management for the
> **programme days** that head the app's Sessions screen. Each day carries a
> **date**, a **bilingual title** (EN / AR), a **display order**, and an optional
> **logo**. The logo rides the unified D-357 media-asset pipeline
> (`AssetCategory.ProgrammeDayImage` owned by the day's `Id`) — there is **no logo
> column** and the upload only appears once the row exists (Edit, not Add). The
> table backs the day-strip + day banner on the app's `/app/programme/days`
> read; while it is empty the app **synthesises one day per distinct session
> date** so the agenda never blanks (a strict superset of the old sessions
> screen). Mirrors the session-category lookup CRUD shape, plus a date and a
> one-active-day-per-date uniqueness guard.
>
> **Centralized framing (D-353).** Add / Edit / View / Delete are hosted by
> `CrudShell`, which frames the reusable `ProgrammeDaysAddEdit` and
> `ProgrammeDaysViewDelete` forms as a **popup or a full page** per the admin's
> toolbar choice (`<CrudPresentationToggle PageKey="programme-days">` persisted in
> localStorage via `CpPreferences`). Delete runs through `ProgrammeDaysViewDelete`
> + a `SimfConfirm` gate.
>
> **No Excel.** Unlike the session-category lookup this page carries **no Excel
> export/import** — it is a tiny date-keyed lookup (a handful of rows) whose logo
> rides the asset pipeline, so a workbook round-trip does not apply.
>
> **RequiredPermission:** the page is gated by `PermissionCatalog.ProgrammeDays.View`;
> the toolbar/row actions by `.Create` / `.Edit` / `.Delete` (all `AdminOnly`
> baseline). The nav item `Module.ProgrammeDays` is gated on `.View`.
>
> **Grid.** Canonical `SimfDataGrid` — server-paged (`GridQuery { Top = 20 }`), a
> numbered pager, per-column filter inputs on **Title (EN)** (`title`) and
> **Title (AR)** (`titlearabic`), column sort on **Date** / **Title (EN)** /
> **Order** / **Active**. Columns: **Date**, **Title (EN)**, **Title (AR)**,
> **Order**, **Logo** (Set/None pill), **Active** (on/off pill). `Multiselect`
> renders select-all / per-row checkboxes (cosmetic — no bulk endpoint).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-PGD-001 | Full CRUD round-trip — Add → Edit (toggle Active off) → Delete | happy | P0 | _to author_ |
| E2E-PGD-002 | Empty list renders `SimfEmptyState` ("No programme days have been added yet.") | happy | P1 | _to author_ |
| E2E-PGD-003 | Auth: signed-in admin lacking `ProgrammeDays.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-PGD-004 | Add button: opens Add form (Date + Title EN/AR + Display order; no logo, no Active) | function | P1 | _to author_ |
| E2E-PGD-005 | Edit button: pre-fills from GET detail + shows Active checkbox **and** the logo upload | function | P1 | _to author_ |
| E2E-PGD-006 | Logo upload (Edit only): attach an image → `ProgrammeDayImage` asset; grid Logo pill flips Set | function | P1 | _to author_ |
| E2E-PGD-007 | Delete button: ViewDelete form + SimfConfirm → soft-delete (Active pill flips Inactive) | function | P1 | _to author_ |
| E2E-PGD-008 | Cancel button in form closes it without saving | function | P2 | _to author_ |
| E2E-PGD-009 | Validation: blank Title (EN) or (AR) → client "An English and Arabic title… are required." | error | P1 | _to author_ |
| E2E-PGD-010 | Validation: missing Date → client "A date is required." | error | P1 | _to author_ |
| E2E-PGD-011 | Uniqueness: a second **active** day on the same date → API 400 `PROGRAMME_DAY_INVALID` | error | P0 | _to author_ |
| E2E-PGD-012 | Title > 128 chars → API 400 `PROGRAMME_DAY_INVALID` (bilingual message) | error | P1 | _to author_ |
| E2E-PGD-013 | Action-level permission gating (Add/Edit/Delete hidden for View-only admin) | auth | P1 | _to author_ |
| E2E-PGD-014 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-PGD-015 | RTL render: Arabic toggle mirrors page + Add form (date / titles / order) | i18n | P1 | _to author_ |
| E2E-PGD-016 | Per-column filter narrows the grid (Title EN / Title AR) | function | P2 | _to author_ |
| E2E-PGD-017 | Column sort toggles (Date / Order) | function | P2 | _to author_ |
| E2E-PGD-018 | App parity: an authored day + its logo appear on `/app/programme/days` (day-strip + banner) | happy | P0 | _to author_ |

## Scenarios

### E2E-PGD-001 — Full CRUD round-trip

```gherkin
Feature: Programme days CRUD round-trip
  As an Administrator
  I want to manage the programme days that head the app's Sessions screen
  So that visitors see the right day title, logo and grouped sessions

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp using the Get-Totp helper
  And they have navigated to /admin/programme-days
  And the page has finished loading (no "Loading programme days…" text)

Scenario: Create, edit (toggle Active off), then delete one day
  Given the grid currently shows {N} rows (or the SimfEmptyState when N = 0)
  When the administrator clicks the grid toolbar's "Add" action
  Then the Add form opens titled "Add programme day"
  And it shows Date, Title (English), Title (Arabic) and Display order
  And it shows neither the "Active" checkbox nor the logo upload (Add cannot attach bytes yet)
  When they set Date="2026-09-15"
  And they fill Title (English)="Opening Day"
  And they fill Title (Arabic)="يوم الافتتاح"
  And they set Display order="1"
  And they click "Save"
  Then a POST /account/api/admin/programme-days fires and returns 200
  And the form closes
  And a green toast reads "Programme day saved." / "تم حفظ يوم البرنامج."
  And the grid shows {N + 1} rows
  And a row exists with Date=2026-09-15, Title (EN)="Opening Day", Order=1, Logo="None", Active="Active"

  When the administrator clicks the "Opening Day" row's Edit (pencil) action
  Then a GET /account/api/admin/programme-days/{id} fires and returns 200
  And the Edit form opens titled "Edit programme day" with the row's values pre-filled
  And the "Active" checkbox is ticked and the "Day logo" upload is shown
  When they change Display order to "2"
  And they untick the "Active" checkbox
  And they click "Save"
  Then a PUT /account/api/admin/programme-days/{id} fires and returns 200
  And a green toast reads "Programme day saved." / "تم حفظ يوم البرنامج."
  And the "Opening Day" row now reads Order=2 and Active="Inactive"

  When the administrator clicks the "Opening Day" row's Delete (trash) action
  Then the ProgrammeDaysViewDelete form opens with the read-only details and a red "Deactivate" button
  When they click "Deactivate" and confirm in the SimfConfirm dialog (which names "Opening Day")
  Then a DELETE /account/api/admin/programme-days/{id} fires and returns 200
  And a green toast reads "Programme day deactivated." / "تم تعطيل يوم البرنامج."
  And the "Opening Day" row remains visible with the grey "Inactive" pill (soft-delete; the list has no active filter)
```

**Evidence captured:**
- Screenshots: `docs/screenshots/cp-admin-programme-days-001-{before,add,edit,after}.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/programme-days/*` call returns 200
- Audit rows: `ProgrammeDay.Created`, `ProgrammeDay.Updated`, `ProgrammeDay.Deactivated` with the actor id (`Detail` carries `id=…; date=2026-09-15; title=Opening Day`).

### E2E-PGD-002 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the ProgrammeDays table has no rows
  When the administrator opens /admin/programme-days
  Then the POST /account/api/admin/programme-days/list returns 200 with Total = 0
  And the grid body renders the SimfEmptyState titled "No programme days have been added yet." / "لم تتم إضافة أي أيام للبرنامج بعد."
  And the toolbar's "Add" action is still visible above the empty state
  And no error toast appears
```

### E2E-PGD-003 — Auth gate

```gherkin
Scenario: Signed-in admin without ProgrammeDays.View is denied
  Given a user is signed in whose role does NOT grant PermissionCatalog.ProgrammeDays.View
  When they navigate to /admin/programme-days
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/programme-days/list request fires
  And the "Module.ProgrammeDays" nav item is not shown in the rail for that user
```

### E2E-PGD-004 — Add form fields

```gherkin
Scenario: New programme day opens an empty Add form without logo or Active
  Given the administrator is on /admin/programme-days
  When they click the grid toolbar's "Add" action
  Then the form opens titled "Add programme day"
  And Date is empty, Title (English) and Title (Arabic) are empty, Display order shows 0
  And there is NO "Active" checkbox and NO "Day logo" upload (IsEdit=false)
  And no request has fired yet (the POST only fires on Save)
```

### E2E-PGD-005 — Edit pre-fills + exposes logo

```gherkin
Scenario: Edit fetches the row detail, pre-fills, and shows the logo upload
  Given at least one programme day "Day Two" exists
  When the administrator clicks the "Day Two" row's Edit (pencil) action
  Then a GET /account/api/admin/programme-days/{id} fires and returns 200
  And the form opens titled "Edit programme day" with Date, Title (EN/AR) and Display order pre-filled
  And the "Active" checkbox reflects the row's IsActive value
  And a "Day logo" section renders the SimfImageUpload (Category="ProgrammeDayImage", OwnerId=the row id)
```

### E2E-PGD-006 — Logo upload attaches a ProgrammeDayImage asset

```gherkin
Scenario: Attaching a logo flips the grid Logo pill to Set
  Given the administrator is editing the programme day "Day Two" which currently has Logo="None"
  When they choose an image in the "Day logo" SimfImageUpload and it uploads
  Then a POST to the generic asset upload endpoint for AssetCategory.ProgrammeDayImage / OwnerId={id} returns 200
  And re-opening the grid (or its next /list) shows the "Day Two" row Logo column as the on pill "Set"
  And GET /api/v1/app/assets/ProgrammeDayImage/{id}/image serves the bytes anonymously for the app
```

### E2E-PGD-007 — Delete soft-deletes via ViewDelete + SimfConfirm

```gherkin
Scenario: Delete confirms then soft-deletes the day
  Given an active programme day "Day Three" exists with the green "Active" pill
  When the administrator clicks the "Day Three" row's Delete (trash) action
  Then the ProgrammeDaysViewDelete form opens with the read-only details and a red "Deactivate" button
  When they click "Deactivate" and confirm in the SimfConfirm dialog naming "Day Three"
  Then a DELETE /account/api/admin/programme-days/{id} fires and returns 200
  And a green toast reads "Programme day deactivated." / "تم تعطيل يوم البرنامج."
  And the "Day Three" row stays in the grid but its Active column shows the grey "Inactive" pill
  And re-deleting the already-inactive row is idempotent (no error)
  And the freed date can now host a new active day (the uniqueness guard only counts active rows)
```

### E2E-PGD-008 — Cancel closes the form

```gherkin
Scenario: Cancel discards the in-progress form
  Given the Add form is open with Title (English)="Discarded"
  When the administrator clicks "Cancel"
  Then the form closes and no POST request fires
  And no new row appears in the grid
```

### E2E-PGD-009 — Client validation: blank titles

```gherkin
Scenario: Blank English or Arabic title is blocked client-side
  Given the Add form is open with a valid Date set
  When the administrator leaves Title (English) blank (or Title (Arabic) blank) and clicks "Save"
  Then a SimfAlert error reads "An English and Arabic title (1–128 characters) are required." /
       "العنوان بالإنجليزية والعربية (من 1 إلى 128 حرفاً) مطلوب."
  And the form stays open and no POST request fires (guarded before the call)
```

### E2E-PGD-010 — Client validation: missing date

```gherkin
Scenario: Missing date is blocked client-side
  Given the Add form is open with both titles filled but no Date chosen
  When the administrator clicks "Save"
  Then a SimfAlert error reads "A date is required." / "التاريخ مطلوب."
  And the form stays open and no POST request fires
```

### E2E-PGD-011 — Uniqueness: one active day per date

```gherkin
Scenario: A second active day on the same date is rejected
  Given an active programme day already exists for Date=2026-09-15
  When the administrator adds another day with Date=2026-09-15 and a valid title and clicks "Save"
  Then the BFF forwards POST /admin/programme-days
  And the API returns HTTP 400 with ApiResult.Error.Code = "PROGRAMME_DAY_INVALID"
  And the error toast surfaces the bilingual message:
      "A programme day already exists for that date." /
      "يوجد يوم برنامج مسجّل بالفعل لهذا التاريخ."
  And the form stays open and nothing is created
```

### E2E-PGD-012 — Server validation: title over 128 chars

```gherkin
Scenario: Over-long title returns API 400 with the bilingual server message
  Given the Add form is open with a valid Date
  When the administrator fills Title (English) with a 129-character string and Title (Arabic)="يوم" and clicks "Save"
  Then the API returns HTTP 400 with ApiResult.Error.Code = "PROGRAMME_DAY_INVALID"
  And the error toast surfaces "Programme-day English title must be between 1 and 128 characters." /
      "يجب أن يتراوح طول العنوان الإنجليزي لليوم بين 1 و 128 حرفاً."
  And the Title (Arabic) over-128 path returns the matching Arabic-title message
```

### E2E-PGD-013 — Action-level permission gating

```gherkin
Scenario: View-only admin sees the grid but no mutating actions
  Given a user signed in with PermissionCatalog.ProgrammeDays.View but NOT Create/Edit/Delete
  When they open /admin/programme-days
  Then the grid and rows render
  And the toolbar's "Add" action is hidden, and the per-row Edit/Delete actions are hidden
  And a direct POST /account/api/admin/programme-days from that user is rejected by the API policy
```

### E2E-PGD-014 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the bilingual fallback toast
  Given the API is configured to return 500 on /admin/programme-days/list
  When the administrator opens /admin/programme-days
  Then a red toast reads "The action could not be completed. Please try again." /
      "تعذّر إكمال العملية. يرجى المحاولة مرة أخرى."
  And no grid rows render
```

### E2E-PGD-015 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add form
  Given the administrator is on /admin/programme-days in English
  When they switch the UI language to العربية from the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "أيام البرنامج"
  And the column headers read "التاريخ", "العنوان (إنجليزي)", "العنوان (عربي)", "الترتيب", "الشعار", "نشط"
  When they click the toolbar's "إضافة" (Add) action
  Then the Add form opens in RTL titled "إضافة يوم برنامج"
  And the field labels read "التاريخ", "العنوان (بالإنجليزية)", "العنوان (بالعربية)", "ترتيب العرض"
  And the footer buttons read "إلغاء" and "حفظ" in reversed order
```

### E2E-PGD-016 — Per-column filter narrows the grid

```gherkin
Scenario: Typing into a per-column filter input narrows the grid server-side
  Given the administrator is on /admin/programme-days with several days
  When they type "open" into the filter input under the "Title (EN)" column
  Then a POST /account/api/admin/programme-days/list fires
  And its GridQuery carries Filters["title"]="open" with Skip reset to 0
  And the grid narrows to rows whose English title contains "open"
  When they clear it and type "افتتاح" under the "Title (AR)" column
  Then the GridQuery carries Filters["titlearabic"]="افتتاح" and the grid narrows accordingly
  And only the "Title (EN)" and "Title (AR)" columns expose a filter input
```

### E2E-PGD-017 — Column sort toggles

```gherkin
Scenario: Clicking a sortable header toggles ascending/descending
  Given the administrator is on /admin/programme-days with several days
  When they click the "Date" column header
  Then a POST .../list fires with Sort="date", SortDescending=false and rows reorder by date ascending
  When they click it again
  Then the POST carries SortDescending=true and rows reorder descending
  When they click the "Order" column header
  Then the POST carries Sort="order", SortDescending=false
  And the default (unsorted) order is DisplayOrder then Date
```

### E2E-PGD-018 — App parity (the reason the page exists)

```gherkin
Scenario: An authored day + logo drive the app's Sessions screen
  Given an active programme day Date=2026-09-15, Title (EN)="Opening Day", with a logo attached
  And at least one active session whose start (in KSA, UTC+3) falls on 2026-09-15
  When the app (or a client) GETs /api/v1/app/programme/days anonymously
  Then the response Days contains an entry with that day's Id and Title "Opening Day"
  And HasImage = true (the ProgrammeDayImage asset is linked)
  And its Sessions list contains the session(s) bucketed onto that KSA date
  And the app renders the day in the day-strip, the day banner (logo), and groups those sessions under it
  And while NO ProgrammeDay rows exist the app instead synthesises one day per distinct session date (HasImage=false) so the agenda never blanks
```

---

## Implementation notes

- **Add / Edit / View / Delete are CrudShell-hosted (D-353).** `CrudShell` frames
  `ProgrammeDaysAddEdit` (Add/Edit) and `ProgrammeDaysViewDelete` (View/Delete) as a
  popup or full page per `<CrudPresentationToggle PageKey="programme-days">`
  (localStorage `simf.cp.prefs.programme-days`). Edit re-fetches via
  `GET /account/api/admin/programme-days/{id}` to pre-fill.
- **Logo is Edit-only and rides the asset pipeline (D-357).** The Add form has no
  upload — the row must exist before bytes can be attached. The Edit form renders
  `<SimfImageUpload Category="ProgrammeDayImage" OwnerId="@Initial.Id">`; the grid /
  detail `HasImage` flag is resolved from the `Asset` table
  (`AssetCategory.ProgrammeDayImage`, `OwnerId = day.Id`). The app reads the bytes
  from the generic anonymous serve route. There is **no logo column**.
- **One active day per date.** `AdminProgrammeDayService.EnsureUniqueDateAsync`
  rejects a second active row for the same date with `PROGRAMME_DAY_INVALID` (HTTP
  400); the guard ignores inactive rows so a deactivated date can be re-used. This
  invariant is what lets the public day-grouping attach each KSA date's sessions to
  exactly one day card.
- **Two validation layers.** "An English and Arabic title… are required." and "A
  date is required." are **client** guards in `HandleSubmitAsync` (no request
  fires). The 1–128 length bound + the date-uniqueness check are enforced
  **server-side** and return `PROGRAMME_DAY_INVALID` with bilingual messages.
- **No Excel.** This page deliberately omits `OnExport` / `OnImport` /
  `CrudGridExcel` (tiny date-keyed lookup; the logo is not workbook data), so the
  SCT-style Excel scenarios do not apply here.
- **API integration tests** at `tests/SIMF.Api.Tests/ProgrammeDaysTests.cs` cover
  create → get → list, 404, blank-title 400, the same-date 400, deactivate, the
  non-admin 403, the `Session.Type` echo, and the public day-grouping
  (`/app/programme/days`). The E2E catalogue layers the CP-driven UI behaviour
  (forms, toasts, confirm, logo upload, action gating, RTL) on top.
- **Audit keys:** `ProgrammeDay.Created`, `ProgrammeDay.Updated`,
  `ProgrammeDay.Deactivated` (`AuditEvents`), one row per mutation with the actor id.

---

_Last reviewed:_ 2026-06-19 by Claude (D-452 — programme days + logo + session-type wiring).
