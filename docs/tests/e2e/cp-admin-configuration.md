# E2E test catalogue — System Configuration (`/admin/configuration`)

| | |
|--|--|
| **Page** | [`cp/admin-configuration.md`](../../pages/cp/admin-configuration.md) |
| **Route** | `/admin/configuration` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-07-10 (D-736 — app-update policy keys → version-policy endpoint) |

> **Page in one line:** admin CRUD over the platform system-settings key/value
> store (P2.4 / D-229, FDS-012 §5.5). Permission family `Configuration.*`
> (`View` gates the page, `Create` / `Edit` / `Delete` gate the actions). The
> store **ships empty** — the team seeds keys once the client confirms the list
> (FDS-012 OI-2), so the empty state is the default first-run experience.
> **Update (D-736):** `DefaultContentSeeder` now pre-creates the six mobile
> app-update policy keys `appUpdate.{android|ios}.{minVersion|latestVersion|storeUrl}`
> with EMPTY values and format-documenting Descriptions, so a fresh grid lists
> them ready to edit — their values drive the anonymous
> `GET /api/v1/app/version-policy` read (see E2E-CFG-024). **How to configure +
> the release runbook** (semver rules, raise `min` only after 100% store rollout):
> [`docs/manuals/SIMF-App-Update-Dev-Guide.md`](../../manuals/SIMF-App-Update-Dev-Guide.md).
>
> **Surface specifics that differ from the gold-standard Interests page:**
> - Flat key/value/description model — **no** Arabic-name field, **no** display
>   order, **no** read-only "Details" modal.
> - The **Key** field is locked on edit (`Disabled="@(_busy || _isEdit)"`) — the
>   key is immutable once created.
> - The **Active** checkbox is rendered **only in the Edit modal** (`@if (_isEdit)`).
> - **Delete** is a soft deactivate. (D-353 — corrected) It is no longer a
>   native browser `confirm()`: Delete now opens the `ConfigurationViewDelete`
>   form inside `CrudShell` (dialog or full page per the toggle), and the
>   actual call is gated by an in-page `SimfConfirm` dialog. See E2E-CFG-020.
> - Empty-key validation is **client-side** (toast, no network call); length /
>   duplicate validation is **server-side**.
> - **Grid affordances (D-256 — raw table → `SimfDataGrid`):** the page now
>   renders through `SimfDataGrid` (page size `Top=20`). Per-column filter inputs
>   exist on **Key**, **Value** and **Description** (the **Active** column is not
>   filterable); only **Key** is sortable. Row actions are **quiet icon buttons**
>   in the grid's RowActions (pencil = edit, trash = delete) — not filled text
>   buttons. `Multiselect` is on (select-all + per-row checkboxes) but there is
>   **no bulk-action toolbar button**, so the checkboxes are cosmetic here.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-CFG-001 | Full CRUD round-trip — Create → Edit (value + deactivate via checkbox) → Delete | happy | P0 | _to author_ |
| E2E-CFG-002 | Empty store renders `SimfEmptyState` ("No system settings yet.") | happy | P1 | _to author_ |
| E2E-CFG-003 | "New setting" opens the Add modal (Key editable, no Active checkbox) | happy | P1 | _to author_ |
| E2E-CFG-004 | "Edit" opens the Edit modal (Key locked, Active checkbox visible, values pre-filled) | happy | P1 | _to author_ |
| E2E-CFG-005 | "Delete" → confirm dialog → soft deactivate + reload | happy | P1 | _to author_ |
| E2E-CFG-006 | "Delete" → cancel dialog → no network call, row unchanged | happy | P2 | _to author_ |
| E2E-CFG-007 | Cancel button closes the modal with no save | happy | P2 | _to author_ |
| E2E-CFG-008 | Auth gate — signed-in admin lacking `Configuration.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-CFG-009 | Action gate — admin with `View` but not `Create`/`Edit`/`Delete` sees no action buttons | auth | P1 | _to author_ |
| E2E-CFG-010 | Validation — blank Key → client-side toast, no POST | error | P1 | _to author_ |
| E2E-CFG-011 | Validation — Key/Value over max length → server 400 `SYSTEM_SETTING_INVALID` | error | P1 | _to author_ |
| E2E-CFG-012 | Conflict — duplicate active Key → server 409 `SYSTEM_SETTING_KEY_DUPLICATE` | error | P1 | _to author_ |
| E2E-CFG-013 | Not found — edit/delete a deleted id → server 404 `SYSTEM_SETTING_NOT_FOUND` | error | P2 | _to author_ |
| E2E-CFG-014 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-CFG-015 | RTL / Arabic render mirrors page + Add/Edit modal | i18n | P1 | _to author_ |
| E2E-CFG-016 | Per-column filter (Key / Value) narrows the grid, resets Skip to 0 | grid | P1 | _to author_ |
| E2E-CFG-017 | Column sort on Key toggles asc ⇄ desc | grid | P2 | _to author_ |
| E2E-CFG-018 | Presentation toggle persists across reload (Page ⇄ Popup) (D-353) | happy | P1 | _to author_ |
| E2E-CFG-019 | Full-page mode round-trip — Add/Edit/View take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-CFG-020 | Delete confirmation gate — CrudShell ViewDelete + SimfConfirm (Cancel = no DELETE, confirm = one DELETE) (D-353) | error | P0 | _to author_ |
| E2E-CFG-021 | Excel export — toolbar Export downloads an .xlsx of the filtered grid / selected rows (D-356) | happy | P1 | _to author_ |
| E2E-CFG-022 | Excel import — upload a workbook → rows created + result modal with per-row outcome (D-356) | happy | P1 | _to author_ |
| E2E-CFG-023 | Excel import rejection — non-.xlsx / wrong-sheet upload → 400 + bilingual toast, nothing created (D-356) | error | P1 | _to author_ |
| E2E-CFG-024 | App-update policy keys — edited `appUpdate.android.*` values flow through the anonymous `GET /api/v1/app/version-policy`; blank → null; `javascript:` store URL → null (D-467/D-736) | happy | P1 | _to author_ (API layer covered — see Implementation notes) |

## Scenarios

### E2E-CFG-001 — Full CRUD round-trip (golden path)

```gherkin
Feature: System Configuration CRUD round-trip
  As an Administrator with the Configuration.* permissions
  I want to create, edit and remove platform system settings
  So that the empty-by-default settings store reflects the client's confirmed keys

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator holding Configuration.View/Create/Edit/Delete has signed in
        via /login + /login/totp (TOTP from the Get-Totp helper)
  And they have navigated to /admin/configuration
  And the page issued POST /account/api/admin/system-settings/list and returned 200

Scenario: Create, edit value, deactivate via the Active checkbox, then delete one setting
  Given the grid currently shows {N} rows (or the SimfEmptyState when N = 0)

  # --- Create ---
  When the administrator clicks "New setting"
  Then the Add modal opens titled "New setting"
  And it shows three editable fields: Key, Value, Description (optional)
  And the "Active" checkbox is NOT rendered (it only exists in Edit mode)
  When they fill Key="event.contactEmail"
  And they fill Value="info@simf.test"
  And they fill Description="Public contact email shown on the website footer"
  And they click "Save"
  Then the BFF forwards POST /account/api/admin/system-settings and returns 200
  And the modal closes
  And a green SimfAlert reads "Setting saved." / "تم حفظ الإعداد."
  And the grid reloads via POST /account/api/admin/system-settings/list
  And a row exists with Key="event.contactEmail", Value="info@simf.test" and Active="✓"

  # --- Edit value + deactivate via the checkbox ---
  When the administrator clicks the row's Edit (pencil) icon action
  Then the BFF forwards GET /account/api/admin/system-settings/{id} and returns 200
  And the Edit modal opens titled "Edit setting" with the row's values pre-filled
  And the Key field is disabled (the key is immutable once created)
  And the "Active" checkbox is visible and ticked
  When they change Value to "events@simf.test"
  And they untick the "Active" checkbox
  And they click "Save"
  Then the BFF forwards PUT /account/api/admin/system-settings/{id} and returns 200
  And the modal closes
  And a green SimfAlert reads "Setting saved." / "تم حفظ الإعداد."
  And the row now shows Value="events@simf.test" and Active="—"

  # --- Delete (soft deactivate behind confirm) ---
  When the administrator clicks the row's Delete (trash) icon action
  Then a native browser confirm dialog asks "Remove this setting?" / "هل تريد إزالة هذا الإعداد؟"
  When they accept the dialog
  Then the BFF forwards DELETE /account/api/admin/system-settings/{id} and returns 200
  And a green SimfAlert reads "Setting removed." / "تمت إزالة الإعداد."
  And the grid reloads (the deactivated row no longer shows as active)
```

**Evidence captured:**
- Screenshots: `docs/screenshots/cp-admin-configuration-add-modal-before.png`,
  `docs/screenshots/cp-admin-configuration-grid-after-create.png`,
  `docs/screenshots/cp-admin-configuration-edit-modal.png`,
  `docs/screenshots/cp-admin-configuration-grid-after-delete.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/system-settings/...` call returns 200
  (POST `/list`, POST create, GET `{id}`, PUT `{id}`, DELETE `{id}`)
- Audit rows: `OperationLog` rows with `Event = 'SystemSetting.Created'`,
  `'SystemSetting.Updated'`, `'SystemSetting.Deactivated'`, each carrying the
  actor's id and `Detail` of the form `id=...; key=event.contactEmail`.

### E2E-CFG-002 — Empty store

```gherkin
Scenario: Empty store renders SimfEmptyState
  Given the SystemSettings table has no rows (the default first-run state)
  When the administrator opens /admin/configuration
  Then POST /account/api/admin/system-settings/list returns 200 with Total = 0
  And the grid body renders the SimfEmptyState component
  And the empty state title reads "No system settings yet." / "لا توجد إعدادات نظام بعد."
  And the "New setting" button is still visible in the toolbar
  And no table summary line is shown
```

### E2E-CFG-003 — Add modal shape

```gherkin
Scenario: New setting opens an Add modal with an editable Key and no Active checkbox
  Given the administrator is on /admin/configuration
  When they click "New setting"
  Then a SimfModal opens titled "New setting" / "إعداد جديد"
  And the Key field is editable (max length 128)
  And the Value field is editable (max length 2048)
  And the Description (optional) field is editable (max length 512)
  And there is NO "Active" checkbox in the Add modal
  And the footer shows "Cancel" and "Save"
```

### E2E-CFG-004 — Edit modal shape

```gherkin
Scenario: Edit opens an Edit modal with a locked Key and a visible Active checkbox
  Given a setting Key="feature.networking.enabled", Value="true" exists
  When the administrator clicks the row's Edit (pencil) icon action
  Then GET /account/api/admin/system-settings/{id} returns 200
  And a SimfModal opens titled "Edit setting" / "تعديل الإعداد"
  And the Key field reads "feature.networking.enabled" and is disabled
  And the Value field reads "true" and is editable
  And the "Active" checkbox is rendered and reflects the row's current IsActive
```

### E2E-CFG-005 — Delete confirm → soft deactivate

```gherkin
Scenario: Delete soft-deactivates after the confirm dialog is accepted
  Given a setting Key="archive.editions.visible", Value="true", Active="✓" exists
  When the administrator clicks the row's Delete (trash) icon action
  Then a native confirm dialog asks "Remove this setting?" / "هل تريد إزالة هذا الإعداد؟"
  When they accept the dialog
  Then DELETE /account/api/admin/system-settings/{id} returns 200
  And a green toast reads "Setting removed." / "تمت إزالة الإعداد."
  And the grid reloads
  And an OperationLog row with Event = 'SystemSetting.Deactivated' is written
```

### E2E-CFG-006 — Delete cancel → no-op

```gherkin
Scenario: Cancelling the confirm dialog leaves the row untouched
  Given a setting row exists
  When the administrator clicks the row's Delete (trash) icon action
  And they dismiss / cancel the native confirm dialog
  Then no DELETE /account/api/admin/system-settings/{id} request fires
  And no toast appears
  And the row remains active and unchanged
```

### E2E-CFG-007 — Cancel the editor

```gherkin
Scenario: Cancel closes the modal without saving
  Given the Add modal is open with Key="some.key" typed
  When the administrator clicks "Cancel"
  Then the modal closes
  And no POST /account/api/admin/system-settings request fires
  And the grid is unchanged
```

### E2E-CFG-008 — Auth gate (page)

```gherkin
Scenario: An admin lacking Configuration.View is denied the page
  Given a signed-in admin user whose role does NOT include the Configuration.View permission
  When they navigate to /admin/configuration
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/system-settings/list request fires
  # The page is decorated [RequirePermission(PermissionCatalog.Configuration.View)]
  # and the nav item Module.Configuration carries RequiredPermission = Configuration.View.
```

### E2E-CFG-009 — Action gate (buttons)

```gherkin
Scenario: View-only admin sees the grid but no Create/Edit/Delete buttons
  Given a signed-in admin holding Configuration.View but NOT Create/Edit/Delete
  When they open /admin/configuration
  Then the grid and its rows render
  But the "New setting" (grid Add) action is hidden (gated by Configuration.Create)
  And the per-row Edit (pencil) icon action is hidden (gated by Configuration.Edit)
  And the per-row Delete (trash) icon action is hidden (gated by Configuration.Delete)
  # Belt-and-braces: even if a button were forced, the API endpoint is gated by the
  # matching policy and would return 403 Forbidden.
```

### E2E-CFG-010 — Validation: blank Key (client-side)

```gherkin
Scenario: Submitting a blank Key shows a client-side toast and fires no request
  Given the Add modal is open
  When the administrator leaves Key blank
  And clicks "Save"
  Then a red SimfAlert reads "A key is required." / "المفتاح مطلوب."
  And the modal stays open
  And NO POST /account/api/admin/system-settings request fires
  # SaveAsync() guards string.IsNullOrWhiteSpace(_form.Key) before any JS interop.
```

### E2E-CFG-011 — Validation: over max length (server-side)

```gherkin
Scenario: A Key longer than 128 characters returns a 400 SYSTEM_SETTING_INVALID
  Given the Add modal is open
  When the administrator fills Key with a 129-character string
  And fills Value="x"
  And clicks "Save"
  Then the BFF forwards POST /account/api/admin/system-settings
  And the API returns HTTP 400 with ApiResult.Error.Code = "SYSTEM_SETTING_INVALID"
  And the modal stays open
  And the error toast surfaces the bilingual MessageForCurrentCulture()
        "The setting key must be between 1 and 128 characters." /
        "يجب أن يتراوح طول مفتاح الإعداد بين 1 و 128 حرفاً."
  # The same code/400 covers a Value over 2048 characters.
```

### E2E-CFG-012 — Conflict: duplicate active Key

```gherkin
Scenario: A duplicate active Key returns a 409 SYSTEM_SETTING_KEY_DUPLICATE
  Given an active setting Key="event.contactEmail" already exists
  When the administrator opens the Add modal
  And fills Key="event.contactEmail" + Value="other@simf.test"
  And clicks "Save"
  Then the BFF forwards POST /account/api/admin/system-settings
  And the API returns HTTP 409 with ApiResult.Error.Code = "SYSTEM_SETTING_KEY_DUPLICATE"
  And the modal stays open
  And the error toast surfaces the bilingual message
        "A setting with the key 'event.contactEmail' already exists." /
        "يوجد إعداد بالمفتاح 'event.contactEmail' بالفعل."
```

### E2E-CFG-013 — Not found on stale id

```gherkin
Scenario: Editing or deleting a removed id returns a 404 SYSTEM_SETTING_NOT_FOUND
  Given a setting row was deleted in another session after the grid loaded
  When the administrator clicks "Edit" on the now-stale row
  Then GET /account/api/admin/system-settings/{id} returns HTTP 404
        with ApiResult.Error.Code = "SYSTEM_SETTING_NOT_FOUND"
  And the page surfaces the bilingual message
        "The system setting was not found." / "لم يتم العثور على الإعداد."
  # The same 404 applies to PUT / DELETE on a missing id.
```

### E2E-CFG-014 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the bilingual fallback toast
  Given the API is configured to return 500 on /admin/system-settings/list (e.g. DB down)
  When the administrator opens /admin/configuration
  Then the page briefly shows "Loading settings…" / "جارٍ تحميل الإعدادات…"
  And then a red SimfAlert reads the fallback
        "The action could not be completed. Please try again." /
        "تعذّر إكمال العملية. يُرجى المحاولة مرة أخرى."
  And no rows render
```

### E2E-CFG-015 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add/Edit modal
  Given the administrator is on /admin/configuration in English
  When they switch the language to "العربية" from the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "إعدادات النظام"
  And the toolbar button reads "إعداد جديد"
  And the table headers read Key/Value/Description/Active in Arabic
  And the nav rail mirrors to the right

  When they click "إعداد جديد"
  Then the Add modal opens in RTL with Arabic field labels
        (Key / Value / Description (optional) → المفتاح / القيمة / الوصف)
  And the footer actions "Cancel" / "Save" appear in reverse order
```

### E2E-CFG-016 — Per-column filter narrows the grid

```gherkin
Scenario: Typing in a column filter input narrows the grid and resets paging
  Given several settings exist, e.g. Key="event.contactEmail", Key="event.startDate",
        Key="feature.networking.enabled"
  And the administrator is on /admin/configuration with the grid loaded (Top = 20, Skip = 0)

  # --- Filter on the Key column ---
  When the administrator types "event." into the "Filter column Key" input
  Then after the 300 ms debounce the page issues
        POST /account/api/admin/system-settings/list
        with GridQuery.Filters["key"] = "event." and Skip reset to 0
  And the grid narrows to the rows whose Key contains "event."
        (the service applies s.Key.Contains("event."))
  And any prior row selection is cleared

  # --- Filter on the Value column instead ---
  When the administrator clears the Key filter and types "true" into the "Filter column Value" input
  Then a new POST /account/api/admin/system-settings/list fires
        with GridQuery.Filters["value"] = "true" (and no "key" entry) and Skip = 0
  And the grid narrows to rows whose Value contains "true"
  # The Description column is filterable on key "description"; the Active column is NOT filterable.
```

### E2E-CFG-017 — Column sort toggles

```gherkin
Scenario: Clicking the Key header toggles ascending ⇄ descending
  Given the administrator is on /admin/configuration with several settings loaded
  And the grid defaults to Key ascending (the service default is rows.OrderBy(s => s.Key))

  When the administrator clicks the "Key" column header
  Then the page issues POST /account/api/admin/system-settings/list
        with GridQuery.Sort = "key", SortDescending = false and Skip reset to 0
  And the rows render in ascending Key order
  And the Key header carries aria-sort="ascending"

  When the administrator clicks the "Key" column header again
  Then a new POST fires with GridQuery.Sort = "key" and SortDescending = true
  And the rows render in descending Key order (the service maps ("key", true) → OrderByDescending)
  And the Key header carries aria-sort="descending"
  # Only the Key column is Sortable; Value / Description / Active headers do not sort.
```

### E2E-CFG-018 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch the form presentation and it persists across reload
  Given the administrator is on /admin/configuration with the default "dialog" presentation
  And the grid toolbar shows the CrudPresentationToggle (PageKey = "configuration")
  When they click the toggle to "Open as full page"
  Then the toggle label flips to "Open as dialog"
  And localStorage key "simf.cp.prefs.configuration" holds {"v":1,"presentation":"page"}
  When they reload /admin/configuration
  Then the page re-reads the preference via CpPreferences.GetPresentationAsync("configuration")
  And the toggle still reads "Open as dialog"
  And opening "New setting" now renders the full-page CrudShell frame (not a popup)
  When they flip the toggle back to "Open as dialog"
  Then localStorage "simf.cp.prefs.configuration" holds {"v":1,"presentation":"dialog"}
  # PageKey = "configuration" (const PageKey in ConfigurationList); the toggle binds _presentation.
```

### E2E-CFG-019 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/View take over the content area; Save returns to the grid
  Given the presentation is set to "full page" (localStorage presentation = "page")
  When the administrator clicks "New setting"
  Then the grid + SimfBanner are hidden (GridHidden) and the CrudShell renders the
        ConfigurationAddEdit form full-page with a title + close header
  And there is no modal backdrop
  When they fill Key="event.startDate", Value="2026-11-01" and click "Save"
  Then POST /account/api/admin/system-settings returns 200
  And the full-page frame closes
  And the grid re-appears with the new row and a green "Setting saved." / "تم حفظ الإعداد." toast
  When they click the row's Edit (pencil) icon and then the frame's Close header button
  Then the form closes and the grid re-appears unchanged (no PUT fired)
```

### E2E-CFG-020 — Delete confirmation gate (CrudShell + SimfConfirm) (D-353)

```gherkin
Scenario: Delete requires explicit SimfConfirm confirmation (no native confirm)
  Given a setting Key="archive.editions.visible", Value="true", Active="✓" exists
  When the administrator clicks the row's Delete (trash) icon action
  Then GET /account/api/admin/system-settings/{id} returns 200
  And the ConfigurationViewDelete form opens inside CrudShell showing the read-only
        details (Key / Value / Description / Active) and a red "Delete" / "حذف" button
  And NO native browser confirm() dialog is used
  When they click the red "Delete" button
  Then a SimfConfirm dialog titled "Remove setting" / "حذف الإعداد" appears
  And its message reads 'Remove the setting "archive.editions.visible"? It will no longer apply.' /
        'هل تريد حذف الإعداد ”archive.editions.visible“؟ سيؤدي ذلك إلى تعطيله.'
  When they click "Cancel" / "إلغاء"
  Then no DELETE /account/api/admin/system-settings/{id} request fires and the row is unchanged
  When they re-open the form, click "Delete" then confirm "Delete" / "حذف"
  Then exactly one DELETE /account/api/admin/system-settings/{id} fires and returns 200
  And the form closes and a green "Setting removed." / "تمت إزالة الإعداد." toast appears
  And the grid reloads (the deactivated row no longer shows as active)
  # Supersedes the native confirm() flow described in E2E-CFG-001/005/006 (pre-D-353).
```

### E2E-CFG-021 — Excel export (D-356)

```gherkin
Scenario: Export the filtered grid to an XLSX workbook
  Given the administrator is on /admin/configuration with at least two settings
  When they click the toolbar "Export" / "تصدير" action with no rows selected
  Then a POST /account/api/admin/system-settings/export fires with an
        AdminGridExportRequest carrying an empty Ids list and the current Query
  And the browser saves a file named simf-configuration-{timestamp}.xlsx
  And the workbook's "Configuration" sheet header row reads Key | Value | Description | IsActive
  When they instead select two rows and click "Export"
  Then the request carries those two Ids and an omitted Query
  And the workbook contains exactly those two rows
  # Export is gated by Configuration.Export; the API caps the export at 5000 rows.
```

### E2E-CFG-022 — Excel import (D-356)

```gherkin
Scenario: Import settings from a workbook and see the per-row outcome
  Given the administrator is on /admin/configuration
  When they click the toolbar "Import" / "استيراد" action
  And the hidden file input id="system-settings-import-input" (accept=".xlsx") opens
  And they choose an .xlsx whose "Configuration" sheet has Key/Value rows for two new settings
  Then a POST /account/api/admin/system-settings/import fires as multipart form data
  And the import-result modal shows "2 created, 0 updated, 0 skipped"
  And a green "Import complete." / "اكتمل الاستيراد." (Grid.Import.Done) toast appears
  And the grid reloads and lists both new settings
  When they import a workbook containing one duplicate active Key and one new Key
  Then the modal shows 1 created, 0 updated, and one per-row error naming the duplicate
        (the service returns 409 SYSTEM_SETTING_KEY_DUPLICATE for that row, not a batch abort)
  # Required headers are Key + Value; import is insert-only and gated by Configuration.Import.
```

### E2E-CFG-023 — Excel import rejection (D-356)

```gherkin
Scenario: A bad / wrong-sheet upload is rejected without creating anything
  Given the administrator is on /admin/configuration
  When they import a file that is not a valid .xlsx (fails the ZIP-magic check) or exceeds 5MB
  Then the request returns HTTP 400 and the page shows a bilingual error toast
  And no system setting is created
  When they import a workbook whose sheet is not named "Configuration"
        (or is missing the required Key / Value headers)
  Then the request returns HTTP 400 with the bilingual worksheet/header error message
  And nothing is created
  # The upload defence (ZIP-magic + 5MB gate) and the 5000-row cap live in the shared import base.
```

### E2E-CFG-024 — App-update policy keys feed the public version-policy endpoint (D-736)

```gherkin
Scenario: Editing the seeded appUpdate keys changes the anonymous version policy
  Given the six app-update keys are pre-seeded EMPTY by DefaultContentSeeder
        (appUpdate.android.minVersion / appUpdate.android.latestVersion /
        appUpdate.android.storeUrl + the matching appUpdate.ios.* trio),
        each with a Description documenting its format
  And GET /api/v1/app/version-policy (anonymous — no auth header) returns 200
        with android/ios objects whose minVersion, latestVersion and storeUrl
        are all null
  When the administrator edits appUpdate.android.latestVersion to "1.1.0"
  And edits appUpdate.android.storeUrl to a valid absolute https Google Play
        listing URL
  Then GET /api/v1/app/version-policy returns android.latestVersion = "1.1.0"
        and android.storeUrl = the saved Play URL (the ios fields stay null)
  When they blank appUpdate.android.latestVersion again
  Then the endpoint returns android.latestVersion = null (that rule is off)
  When they set appUpdate.android.storeUrl to "javascript:alert(1)"
  Then the endpoint returns android.storeUrl = null
  # The store URL is sanitised server-side to absolute http(s)-or-null (D-467) —
  # the value becomes a launched link on-device. Version strings pass through
  # as-is: the app owns semver parsing and ignores values it cannot parse.
```

---

## Implementation notes

- **Manual smoke is the canonical run today.** Until Playwright is adopted, the
  canonical execution is a Chrome DevTools MCP session: sign in per the Auth
  setup, walk each scenario, and capture screenshots into
  `docs/screenshots/cp-admin-configuration-*.png`.
- **API integration tests cover the same surface at a lower layer** —
  [`tests/SIMF.Api.Tests/SystemSettingsTests.cs`](../../../tests/SIMF.Api.Tests/SystemSettingsTests.cs):
  - `Create_then_get_then_list_contains_the_setting` → mirrors E2E-CFG-001 create/list.
  - `Get_returns_404_for_unknown_id` → mirrors E2E-CFG-013.
  - `Create_with_empty_key_is_400_SYSTEM_SETTING_INVALID` → mirrors E2E-CFG-011.
  - `Duplicate_active_key_is_409` → mirrors E2E-CFG-012.
  - `Deactivate_marks_the_setting_inactive` → mirrors E2E-CFG-005.
  - `Non_admin_caller_is_forbidden_on_create` → mirrors the API side of E2E-CFG-009.
- **D-736 version-policy read is covered at the API layer** —
  [`tests/SIMF.Api.Tests/AppVersionPolicyPublicTests.cs`](../../../tests/SIMF.Api.Tests/AppVersionPolicyPublicTests.cs):
  - `GET_is_anonymous_and_returns_null_for_unset_keys`,
    `GET_returns_the_admin_configured_values`,
    `GET_returns_null_for_a_blank_value`, `GET_drops_a_non_http_store_url`,
    `GET_ignores_a_deactivated_key` → mirror E2E-CFG-024.
  - Endpoint: `src/Backend/SIMF.Api/Endpoints/Public/AppVersionPolicyEndpoint.cs`
    (`GET /app/version-policy`, `AllowAnonymous`); read/sanitise:
    `src/Backend/SIMF.Infrastructure/Configuration/AppVersionPolicyService.cs`;
    key whitelist: `src/Shared/SIMF.Common/AppUpdateSettingKeys.cs`; seeding:
    `src/Backend/SIMF.Infrastructure/Seeding/DefaultContentSeeder.cs`.
- **Endpoints & gates (ground truth):**
  - API: `src/Backend/SIMF.Api/Endpoints/Admin/SystemSettingEndpoints.cs`
    (`POST /admin/system-settings/list`, `GET /admin/system-settings/{id}`,
    `POST /admin/system-settings`, `PUT /admin/system-settings/{id}`,
    `DELETE /admin/system-settings/{id}`), each `Policies(PermissionCatalog.PolicyFor(...))`.
  - BFF passthroughs: `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs`
    (`/account/api/admin/system-settings/*`).
  - Service / validation / audit: `src/Backend/SIMF.Infrastructure/Configuration/AdminSystemSettingService.cs`.
  - Permissions: `PermissionCatalog.Configuration.{View,Create,Edit,Delete}`.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin
  scenario into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be
  created) + step definitions. The Gherkin is already runner-agnostic.

---

_Last reviewed:_ 2026-07-10 by Claude (D-736 — appended E2E-CFG-024: the six seeded `appUpdate.*` keys flow through the anonymous `GET /api/v1/app/version-policy` with D-467 store-URL sanitisation. Prior: 2026-06-10, D-356 Phase 5 — Excel + toggle; appended E2E-CFG-018..023, corrected the stale native-confirm delete note to CrudShell + SimfConfirm).
