# E2E test catalogue — Countries CRUD (`/admin/countries`)

| | |
|--|--|
| **Page** | [`cp/admin-countries.md`](../../pages/cp/admin-countries.md) |
| **Route** | `/admin/countries` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (canonical SIMF browser smoke). Convertible to Playwright later — keep scenario steps tool-agnostic. |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Required permission** | `Countries.View` (page gate). Create/Edit/Deactivate actions hit endpoints gated by `Countries.Create` / `Countries.Edit` / `Countries.Delete`. |
| **Last reviewed** | 2026-06-02 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-CTY-001 | Full CRUD round-trip — Add → Edit → Details → Deactivate | happy | P0 | _to author_ |
| E2E-CTY-002 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-CTY-003 | Auth gate: signed-in admin lacking `Countries.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-CTY-004 | Add — search/filter by code or name | function | P1 | _to author_ |
| E2E-CTY-005 | Sort columns (ISO id, Code, Name (English), Order) | function | P2 | _to author_ |
| E2E-CTY-006 | Pager — page size + first/last/prev/next | function | P2 | _to author_ |
| E2E-CTY-007 | Details modal — read-only field render + Close | function | P1 | _to author_ |
| E2E-CTY-008 | Edit — toggle `Active` checkbox reactivates a deactivated row | function | P1 | _to author_ |
| E2E-CTY-009 | Validation: invalid ISO id (blank / 0 / >999) | error | P1 | _to author_ |
| E2E-CTY-010 | Validation: code not exactly 2 letters | error | P1 | _to author_ |
| E2E-CTY-011 | Validation: empty English / Arabic name | error | P1 | _to author_ |
| E2E-CTY-012 | Validation: phone prefix > 8 chars / negative display order | error | P2 | _to author_ |
| E2E-CTY-013 | Conflict: duplicate ISO id → 409 `COUNTRY_ID_DUPLICATE` | error | P1 | _to author_ |
| E2E-CTY-014 | Conflict: duplicate alpha-2 code → 409 `COUNTRY_CODE_DUPLICATE` | error | P1 | _to author_ |
| E2E-CTY-015 | Not found: edit/details of a missing id → 404 `COUNTRY_NOT_FOUND` | error | P2 | _to author_ |
| E2E-CTY-016 | Server 500 on `/list` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-CTY-017 | RTL render: Arabic toggle mirrors page + Add modal | i18n | P1 | _to author_ |

## Scenarios

### E2E-CTY-001 — Full CRUD round-trip

```gherkin
Feature: Countries CRUD round-trip
  As an Administrator
  I want to manage the country lookup that backs the nationality picker
  So that visitor + speaker country data joins against an accurate list

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with the Countries.View/Create/Edit/Delete permissions
    has signed in via /login then /login/totp using the Get-Totp helper
  And they have navigated to /admin/countries
  And the grid issues POST /account/api/admin/countries/list and returns 200

Scenario: Create, edit, view, then deactivate one country
  Given the grid currently shows {N} rows
  When the administrator clicks "Add country"
  Then the Add modal opens titled "Add country"
  And it shows six fields: ISO 3166-1 numeric id, ISO alpha-2 code,
    Name (English), Name (Arabic), Dial code, Display order
  And no "Active" checkbox is shown (Add mode only)
  And the id field helper reads "Manually assigned (e.g. 682 = SA, 784 = AE). Once set it cannot be changed."

  When they fill ISO 3166-1 numeric id="116"
  And they fill ISO alpha-2 code="KH"
  And they fill Name (English)="Cambodia"
  And they fill Name (Arabic)="كمبوديا"
  And they fill Dial code="+855"
  And they fill Display order="50"
  And they click "Create country"
  Then the BFF forwards POST /account/api/admin/countries and the API returns 200
  And the request body normalises Code to upper-case "KH" and trims the names
  And the modal closes
  And the grid reloads showing {N + 1} rows
  And a green toast reads 'Country "Cambodia" was created.'
  And a row exists with ISO id=116, Code="KH", Name (English)="Cambodia",
    Dial code="+855", Order=50 and the green "Active" pill

  When the administrator clicks the "Edit" icon on the Cambodia row
  Then the BFF issues GET /account/api/admin/countries/116 (200)
  And the Edit modal opens titled "Edit country" with the row's values pre-filled
  And the ISO id field is disabled (read-only) with helper "The numeric id is fixed for an existing row."
  And an additional "Active — show in the nationality picker" checkbox is visible and ticked
  When they change Display order to "5"
  And they click "Save changes"
  Then the BFF forwards PUT /account/api/admin/countries/116 (200)
  And the modal closes
  And a green toast reads 'Country "Cambodia" was updated.'
  And the Cambodia row's Order column reads "5"

  When the administrator clicks the "Details" icon on the Cambodia row
  Then the BFF issues GET /account/api/admin/countries/116 (200)
  And a read-only modal titled "Country details" opens with a description list
    showing ISO id, Code, Name (English), Name (Arabic), Dial code, Order, Status
  When they click "Close"
  Then the modal closes

  When the administrator clicks the "Deactivate" icon on the Cambodia row
  Then the BFF forwards DELETE /account/api/admin/countries/116 (200)
  And a green toast reads 'Country "Cambodia" was deactivated.'
  And the Cambodia row remains visible but its Status pill changes to the grey "Inactive" pill
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-countries-001-before.png` (grid at {N} rows)
- Screenshots: `docs/screenshots/cp-admin-countries-{add-modal,edit-modal,details-modal,after-deactivate}.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/countries/*` call returns 200 (POST `/list`, POST create, GET `/{id}`, PUT `/{id}`, DELETE `/{id}`)
- Audit rows: `OperationLog`/`AuditEntry` rows with `EventType = 'Country.Created'`, `'Country.Updated'`, `'Country.Deactivated'`, each carrying the actor's user id and `Detail` (e.g. `id=116; code=KH; nameEn=Cambodia`)

### E2E-CTY-002 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no Country rows (or the active filter excludes all)
  When the administrator opens /admin/countries
  Then the grid body renders the SimfEmptyState component
  And the empty state shows the bilingual copy "No countries yet." / "لا توجد بلدان بعد."
  And the toolbar still shows the "Add country" button
  And no error toast appears
```

### E2E-CTY-003 — Auth gate

```gherkin
Scenario: Signed-in admin lacking Countries.View is denied
  Given a signed-in Control Panel user whose role does NOT include the
    Countries.View permission (Administrator wildcard "*" is NOT held)
  When they navigate to /admin/countries
  Then the [RequirePermission(PermissionCatalog.Countries.View)] gate denies them
  And they land on /not-permitted with HTTP 200
  And no POST /account/api/admin/countries/list request fires
```

### E2E-CTY-004 — Search / filter

```gherkin
Scenario: Filter the grid by code or name
  Given the grid shows several countries including "Saudi Arabia" (Code SA)
  When the administrator types "SA" into the filter search box
  Then a POST /account/api/admin/countries/list fires with Search="SA"
  And the grid shows only rows whose Code, Name (English) or Name (Arabic) contain "SA"
  When they clear the search box
  Then the grid reloads the full list
```

### E2E-CTY-005 — Sort columns

```gherkin
Scenario: Sortable columns re-query with Sort + SortDescending
  Given the grid is loaded
  When the administrator clicks the "ISO id" column header
  Then a POST /account/api/admin/countries/list fires with Sort="id", SortDescending=false
  And the rows order ascending by ISO id
  When they click the "ISO id" header again
  Then SortDescending=true and the order reverses
  And the same applies to the "Code", "Name (English)" and "Order" headers
  And the "Name (Arabic)", "Dial code" and "Status" columns are NOT sortable
```

### E2E-CTY-006 — Pager

```gherkin
Scenario: Page size and pager navigation
  Given the database has more than one page of countries
  When the administrator changes the "Show" page size
  Then a POST /account/api/admin/countries/list fires with the new Top value
  And the summary reads "Showing 1–{pageSize} of {total}"
  When they click "Next" / "Last page" / "First page" / "Previous"
  Then the grid re-queries with the matching Skip and the page indicator
    reads "Page {current} of {total}"
```

### E2E-CTY-007 — Details modal

```gherkin
Scenario: Details modal is read-only and closes cleanly
  Given a Country row "Saudi Arabia" exists
  When the administrator clicks the "Details" icon on that row
  Then the BFF issues GET /account/api/admin/countries/{id} (200)
  And a modal titled "Country details" opens with a description list (dl.simf-dl)
  And every value is read-only (no inputs, no Save button)
  And a missing Dial code renders as the em dash "—"
  When they click "Close"
  Then the modal closes and no write request fired
```

### E2E-CTY-008 — Reactivate via Edit

```gherkin
Scenario: Edit re-activates a deactivated country
  Given a Country "Cambodia" exists with the grey "Inactive" pill
  When the administrator clicks "Edit" on that row
  Then the "Active — show in the nationality picker" checkbox is unticked
  When they tick the "Active" checkbox
  And they click "Save changes"
  Then PUT /account/api/admin/countries/{id} fires with IsActive=true (200)
  And a green toast reads 'Country "Cambodia" was updated.'
  And the row's Status pill changes back to the green "Active" pill
```

### E2E-CTY-009 — Validation: invalid ISO id

```gherkin
Scenario: Bad ISO numeric id is blocked client-side
  Given the Add modal is open
  When the administrator leaves ISO 3166-1 numeric id blank (or "0" or "1000")
  And fills the remaining fields with valid data
  And clicks "Create country"
  Then a SimfAlert error appears at the top of the modal reading
    "Id must be a positive integer (1–999, ISO 3166-1 numeric)."
  And the modal stays open
  And no POST /account/api/admin/countries request fires
```

### E2E-CTY-010 — Validation: code length

```gherkin
Scenario: Alpha-2 code must be exactly 2 letters
  Given the Add modal is open with a valid id and names
  When the administrator fills ISO alpha-2 code="S" (or leaves it blank)
  And clicks "Create country"
  Then a SimfAlert error reads "Code must be exactly 2 letters."
  And the modal stays open
  And no POST request fires
```

### E2E-CTY-011 — Validation: empty names

```gherkin
Scenario: English and Arabic names are required
  Given the Add modal is open with a valid id and code
  When the administrator leaves Name (English) blank and clicks "Create country"
  Then a SimfAlert error reads "English name is required (1–128 characters)." /
    "الاسم بالإنجليزية مطلوب (1–128 حرفاً)."
  And the modal stays open
  When they fill a valid English name but leave Name (Arabic) blank and submit
  Then a SimfAlert error reads "Arabic name is required (1–128 characters)."
  And no POST request fires in either case
```

### E2E-CTY-012 — Validation: dial code / display order

```gherkin
Scenario: Server-side guards for dial code length and negative order
  Given the Add modal is open with otherwise-valid data
  When the administrator fills Display order="-1" and clicks "Create country"
  Then the client guard blocks it with "Display order must be zero or a positive integer."
  And no POST request fires
  # Phone prefix > 8 chars is capped client-side by MaxLength="8"; if bypassed,
  # the API returns 400 COUNTRY_INVALID "Phone prefix must be 8 characters or fewer."
  # surfaced via the modal's bilingual error alert.
```

### E2E-CTY-013 — Conflict: duplicate ISO id

```gherkin
Scenario: Duplicate ISO numeric id returns 409
  Given a Country with id=682 (Saudi Arabia) already exists
  When the administrator opens the Add modal
  And fills ISO id="682" + code="ZZ" + valid names + Display order="0"
  And clicks "Create country"
  Then the BFF forwards POST /account/api/admin/countries
  And the API returns HTTP 409 with ApiResult.Error.Code = "COUNTRY_ID_DUPLICATE"
  And the modal stays open
  And the modal error alert surfaces the bilingual MessageForCurrentCulture()
    "A country with id 682 already exists." / "يوجد بلد بالمعرّف 682 بالفعل."
```

### E2E-CTY-014 — Conflict: duplicate alpha-2 code

```gherkin
Scenario: Duplicate ISO alpha-2 code returns 409
  Given a Country with code "SA" already exists
  When the administrator opens the Add modal
  And fills a free ISO id + code="sa" (lower-case) + valid names + order="0"
  And clicks "Create country"
  Then the request normalises the code to upper-case "SA"
  And the API returns HTTP 409 with ApiResult.Error.Code = "COUNTRY_CODE_DUPLICATE"
  And the modal error alert reads "A country with code 'SA' already exists." /
    "يوجد بلد بالرمز 'SA' بالفعل."
  And the same 409 fires from Edit when changing one row's code to another's code
```

### E2E-CTY-015 — Not found

```gherkin
Scenario: Editing or viewing a deleted/missing country surfaces 404
  Given the administrator triggers an edit/details for an id that no longer exists
    (e.g. the row was removed by another admin between load and click)
  When the BFF issues GET /account/api/admin/countries/{missingId}
  Then the API returns HTTP 404 with ApiResult.Error.Code = "COUNTRY_NOT_FOUND"
  And the page shows a red toast surfacing
    "The country was not found." / "لم يتم العثور على البلد."
  And no modal opens
```

### E2E-CTY-016 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows fallback bilingual toast
  Given the API is configured to return 500 on /admin/countries/list (e.g. DB down)
  When the administrator opens /admin/countries
  Then the grid shows the "Loading countries…" indicator
  And then a red toast appears reading
    "The countries could not be loaded." / "تعذّر تحميل البلدان."
  And no rows render
```

### E2E-CTY-017 — RTL render

```gherkin
Scenario: Arabic toggle mirrors page + Add modal
  Given the administrator is on /admin/countries in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "البلدان"
  And the grid column headers + toolbar mirror to the right
  And the pager arrows reverse direction

  When they click "إضافة بلد"
  Then the Add modal opens in RTL with Arabic field labels
  And the form actions ("إنشاء البلد" / "إلغاء") appear in reverse order
```

---

## Implementation notes

- **Manual smoke as canonical source of truth today.** Until Playwright is
  adopted, the canonical "run" of these scenarios is a Chrome DevTools MCP
  session — sign in via the Background steps, walk each scenario, and capture
  screenshots into `docs/screenshots/cp-admin-countries-{scenario}.png`.
- **Rate limiting.** Create / Update / Deactivate endpoints carry
  `RequireRateLimiting("auth")`. A fast-fire CRUD loop can trip a 429; pace the
  write scenarios or expect the limiter to be widened in the test environment.
- **Field model (from `CountryForm.razor`).** ISO numeric id (1–999, create-only,
  read-only on edit), ISO alpha-2 code (exactly 2 letters, upper-cased + unique),
  Name (English) 1–128, Name (Arabic) 1–128, Dial code optional ≤ 8 chars,
  Display order ≥ 0, and an `Active` checkbox shown only in Edit mode. Client
  guards mirror the server `Validate(...)` in `AdminCountryService`.
- **Error codes (from `ErrorCodes.cs` / `AdminCountryService`):**
  `COUNTRY_INVALID` (400), `COUNTRY_NOT_FOUND` (404),
  `COUNTRY_ID_DUPLICATE` (409), `COUNTRY_CODE_DUPLICATE` (409).
- **Convert to Playwright** when the runner is adopted: copy each Gherkin
  scenario into a `.feature` file under `tests/SIMF.E2E.Tests/` + step
  definitions. The Gherkin shape is already runner-agnostic.
- **API integration tests** at `tests/SIMF.Api.Tests/AdminCountriesTests.cs`
  cover the same surface at a lower layer (no browser) — list/get/create/update/
  deactivate plus the duplicate-id, duplicate-code, not-found and validation
  paths. During the transition, keep both.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
