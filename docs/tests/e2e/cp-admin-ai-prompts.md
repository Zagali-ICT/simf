# E2E test catalogue — AI prompts catalogue (`/admin/ai/prompts`)

| | |
|--|--|
| **Page** | [`cp/admin-ai-prompts.md`](../../pages/cp/admin-ai-prompts.md) _(reference doc not yet authored — grounded directly in `AiPromptsList.razor`)_ |
| **Route** | `/admin/ai/prompts` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **Page permission:** `@attribute [RequirePermission(PermissionCatalog.AiPrompts.View)]`.
> Each row action is gated by its own permission at the API:
> `AiPrompts.Create` (POST), `AiPrompts.Edit` (PUT), `AiPrompts.Delete`
> (DELETE / "Deactivate"), `AiPrompts.Test` (POST `/test`). All are
> `AdminOnly` baseline (`Administrator = "*"` satisfies every one).
> The Create / Update / Delete / Test endpoints also require the
> `RequireApprovedAccount` policy, and Create/Update/Delete sit behind the
> per-IP `auth` rate-limit while Test sits behind the per-admin `ai-test`
> limiter (D-179).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-AIP-001 | Golden round-trip — New → Edit → Test → Deactivate one prompt | happy | P0 | _to author_ |
| E2E-AIP-002 | Empty list renders `SimfEmptyState` ("No AI prompts yet.") | happy | P1 | _to author_ |
| E2E-AIP-003 | Auth gate: signed-in admin lacking `AiPrompts.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-AIP-004 | Create: invalid `Key` (not kebab-case) → 400 `AI_PROMPT_INVALID` bilingual toast | error | P1 | _to author_ |
| E2E-AIP-005 | Create: blank `Display name` → 400 `AI_PROMPT_INVALID` bilingual toast | error | P1 | _to author_ |
| E2E-AIP-006 | Create: `Temperature` out of range (3.0) → 400 `AI_PROMPT_INVALID` | error | P2 | _to author_ |
| E2E-AIP-007 | Duplicate `Key` → 409 `AI_PROMPT_KEY_DUPLICATE` bilingual toast | error | P1 | _to author_ |
| E2E-AIP-008 | Edit: `Key` field disabled (immutable), pre-filled values, toggle `Active` off | happy | P1 | _to author_ |
| E2E-AIP-009 | Test (Echo provider): `key=value` inputs → output + latency + tokens | happy | P0 | _to author_ |
| E2E-AIP-010 | Test (OpenAi provider, no key) → 503 `AI_PROVIDER_NOT_CONFIGURED` toast | resilience | P2 | _to author_ |
| E2E-AIP-011 | Delete (trash) row action → soft-deactivate, row pill flips to "—" | happy | P1 | _to author_ |
| E2E-AIP-012 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-AIP-013 | RTL render: Arabic toggle mirrors page + Add/Test modals | i18n | P1 | _to author_ |
| E2E-AIP-014 | Pager summary line reads "Showing X–Y of Z" | happy | P2 | _to author_ |
| E2E-AIP-015 | Per-column filter (Key / Name) narrows the grid via `Filters[key]` | grid | P1 | _to author_ |
| E2E-AIP-016 | Column sort toggles ascending → descending → off (`Sort` + `SortDescending`) | grid | P2 | _to author_ |
| E2E-AIP-017 | Presentation toggle: switch to full-page + persists across reload (D-353) | happy | P1 | _to author_ |
| E2E-AIP-018 | Full-page mode: Add/Edit/View take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-AIP-019 | Delete confirmation: Deactivate opens View/Delete → SimfConfirm gates the call (D-353) | error | P0 | _to author_ |
| E2E-AIP-020 | Excel export: toolbar Export downloads an .xlsx of the filtered grid (D-356) | happy | P1 | _to author_ |
| E2E-AIP-021 | Excel import: upload a workbook → rows created/updated + result modal with per-row outcome (D-356) | happy | P1 | _to author_ |
| E2E-AIP-022 | Excel import: a non-workbook / wrong-sheet upload → bilingual rejection, nothing created (D-356) | error | P1 | _to author_ |
| E2E-AIP-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-AIP-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-AIP-001 — Golden round-trip (New → Edit → Test → Deactivate)

```gherkin
Feature: AI prompt catalogue round-trip
  As an Administrator
  I want to create, edit, dry-run and deactivate an AI prompt from one place
  So that all AI behaviour on SIMF is managed dynamically without a redeploy

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp (Get-Totp helper)
  And they have landed on /admin/ai/prompts
  And the page issued POST /account/api/admin/ai/prompts/list and rendered the grid

Scenario: Create, edit, test and deactivate one prompt
  Given the grid currently shows {N} rows
  When the administrator clicks "New prompt"
  Then the Create modal opens titled "Create AI prompt"
  And the modal shows: Key, Feature (select), Display name (English),
      Display name (Arabic), Provider (select), Model, System prompt (textarea),
      User prompt template (textarea), Temperature (number), Max output tokens
      (number), and an "Active" checkbox
  And the "Key" field is enabled (it is only disabled in edit mode)
  And the Provider select defaults to "Echo" and Model defaults to "echo"
  And Temperature defaults to "0.2" and Max output tokens defaults to "512"

  When they fill Key="welcome-greeting"
  And they select Feature="Assistance"
  And they fill Display name (English)="Welcome greeting"
  And they fill Display name (Arabic)="رسالة الترحيب"
  And they leave Provider="Echo" and Model="echo"
  And they fill System prompt="You are a friendly SIMF concierge."
  And they fill User prompt template="Greet the visitor named {name}."
  And they leave Temperature="0.2" and Max output tokens="512"
  And they click "Save"
  Then the BFF forwards POST /account/api/admin/ai/prompts and the API returns 200
  And the modal closes
  And a green SimfAlert reads "Prompt saved."
  And the grid reloads (POST /account/api/admin/ai/prompts/list) showing {N + 1} rows
  And a row exists with Key="welcome-greeting", Feature="Assistance",
      Name="Welcome greeting", Provider="Echo", Model="echo", Version="v1",
      and the Active column shows "✓"

  When the administrator clicks the row's Edit (pencil) action in the grid RowActions
  Then GET /account/api/admin/ai/prompts/{id} returns 200
  And the Edit modal opens titled "Edit AI prompt" with values pre-filled
  And the "Key" field is now disabled (Key is immutable once written)
  When they change System prompt="You are a concise SIMF concierge."
  And they click "Save"
  Then PUT /account/api/admin/ai/prompts/{id} returns 200
  And a green SimfAlert reads "Prompt saved."
  And the row's Version column now reads "v2"

  When the administrator clicks the row's Test (flask) action in the grid RowActions
  Then the Test modal opens titled "Test prompt" showing the key "welcome-greeting"
  When they type inputs (one per line) "name=Captain Ahmad"
  And they click "Run test"
  Then POST /account/api/admin/ai/prompts/{id}/test returns 200
  And a description list shows Output, Latency (e.g. "3ms") and Tokens
  And because Provider="Echo" the Output deterministically echoes the rendered prompt
  When they click "Cancel"
  Then the Test modal closes

  When the administrator clicks the row's Delete (trash) action in the grid RowActions
      (the soft-deactivate action — DELETE on the API)
  Then DELETE /account/api/admin/ai/prompts/{id} returns 200
  And a green SimfAlert reads "Prompt deactivated."
  And the grid reloads and the row's Active column now shows "—"
```

**Evidence captured:**
- Screenshots: `docs/screenshots/cp-admin-ai-prompts-{grid,create-modal,edit-modal,test-modal,after-deactivate}.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/ai/prompts/*` call returns 200
  (`/list` POST, create POST, `{id}` GET, `{id}` PUT, `{id}/test` POST, `{id}` DELETE)
- Audit rows: `OperationLog` (or audit sink) rows with `Event = 'AiPrompt.Created'`,
  `'AiPrompt.Updated'` (carrying `contentHashOld`/`contentHashNew`/`contentChanged`),
  and `'AiPrompt.Deactivated'`, each with the actor's id. The edit also writes an
  `AiPromptHistory` snapshot of the pre-mutation v1 content.

### E2E-AIP-002 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no active AiPrompt rows
  When the administrator opens /admin/ai/prompts
  Then POST /account/api/admin/ai/prompts/list returns 200 with Total = 0
  And the grid body renders the SimfEmptyState component
  And the empty state title reads "No AI prompts yet." / "لا توجد محفّزات بعد."
  And the "New prompt" button is still visible in the toolbar
  And no summary line is rendered
```

### E2E-AIP-003 — Auth gate

```gherkin
Scenario: Signed-in admin lacking AiPrompts.View is denied
  Given a signed-in Control Panel user whose role does NOT grant AiPrompts.View
      (i.e. not Administrator and without that permission baked into the JWT)
  When they navigate to /admin/ai/prompts
  Then the [RequirePermission(PermissionCatalog.AiPrompts.View)] gate redirects
      them to /not-permitted with HTTP 200
  And no POST /account/api/admin/ai/prompts/list request fires
  And the "AI prompts" nav item is hidden for them (CpNavigation RequiredPermission
      = AiPrompts.View)
```

### E2E-AIP-004 — Invalid Key (not kebab-case)

```gherkin
Scenario: Non-kebab Key returns 400 AI_PROMPT_INVALID
  Given the Create modal is open
  When the administrator fills Key="Welcome Greeting" (spaces + capitals)
  And fills the remaining required fields with valid values
  And clicks "Save"
  Then the BFF forwards POST /account/api/admin/ai/prompts
  And the API returns HTTP 400 with ApiResult.Error.Code = "AI_PROMPT_INVALID"
  And the error toast surfaces the bilingual MessageForCurrentCulture()
      "Key must be 2–64 chars, kebab-case (a-z, 0-9, -)." /
      "يجب أن يكون المفتاح بين 2 و 64 محرفاً، بصيغة kebab."
  And the modal stays open with the field values intact
```

### E2E-AIP-005 — Blank Display name

```gherkin
Scenario: Blank Display name returns 400 AI_PROMPT_INVALID
  Given the Create modal is open
  When the administrator fills Key="blank-name-test"
  And leaves Display name (English) blank
  And fills the other required fields
  And clicks "Save"
  Then POST /account/api/admin/ai/prompts returns HTTP 400
  And ApiResult.Error.Code = "AI_PROMPT_INVALID"
  And the error toast reads
      "DisplayName must be between 1 and 128 characters." /
      "يجب أن يتراوح طول DisplayName بين 1 و 128 محرفاً."
  And the modal stays open
```

### E2E-AIP-006 — Temperature out of range

```gherkin
Scenario: Temperature above 2.0 returns 400 AI_PROMPT_INVALID
  Given the Create modal is open
  And all text fields hold valid values (Key="temp-range-test")
  When the administrator sets Temperature="3.0"
  And clicks "Save"
  Then POST /account/api/admin/ai/prompts returns HTTP 400
  And ApiResult.Error.Code = "AI_PROMPT_INVALID"
  And the error toast reads
      "Temperature must be between 0 and 2." /
      "يجب أن تكون درجة الحرارة بين 0 و 2."
  And the modal stays open
```

### E2E-AIP-007 — Duplicate Key

```gherkin
Scenario: Duplicate Key returns 409 AI_PROMPT_KEY_DUPLICATE
  Given an active AiPrompt with Key="welcome-greeting" already exists
  When the administrator opens the Create modal
  And fills Key="welcome-greeting" plus all other valid fields
  And clicks "Save"
  Then POST /account/api/admin/ai/prompts returns HTTP 409
  And ApiResult.Error.Code = "AI_PROMPT_KEY_DUPLICATE"
  And the error toast surfaces the bilingual server message
      "AI prompt key 'welcome-greeting' is already in use." /
      "مفتاح المحفّز 'welcome-greeting' مستخدم بالفعل."
  And the modal stays open
```

### E2E-AIP-008 — Edit modal: Key immutable + Active toggle

```gherkin
Scenario: Edit pre-fills values, disables Key, and toggles Active off
  Given an active prompt "welcome-greeting" exists in the grid
  When the administrator clicks the row's Edit (pencil) action in the grid RowActions
  Then GET /account/api/admin/ai/prompts/{id} returns 200
  And the Edit modal opens with every field pre-filled from the detail payload
  And the "Key" field is disabled (immutable; the update request has no Key field)
  And the "Active" checkbox is ticked
  When they untick "Active"
  And click "Save"
  Then PUT /account/api/admin/ai/prompts/{id} returns 200 with IsActive=false
  And a green SimfAlert reads "Prompt saved."
  And on reload the row's Active column shows "—" and Version increments by 1
```

### E2E-AIP-009 — Test prompt against Echo provider

```gherkin
Scenario: Dry-run an Echo prompt returns a deterministic output
  Given an active prompt "welcome-greeting" with Provider="Echo" exists
  When the administrator clicks the row's Test (flask) action in the grid RowActions
  Then the Test modal opens titled "Test prompt" and shows the key "welcome-greeting"
  And the inputs textarea label reads "Inputs (one per line: key=value)"
  When they type "name=Captain Ahmad"
  And click "Run test"
  Then POST /account/api/admin/ai/prompts/{id}/test returns 200 (ApiResult success)
  And a description list renders Output, Latency ("{N}ms") and Tokens ("in / out")
  And the Echo provider's Output deterministically reflects the rendered template
  And one AiInvocation row is recorded (CallerKind="Admin") visible later under
      /admin/ai/invocations
```

### E2E-AIP-010 — Test against an unconfigured provider

```gherkin
Scenario: Testing an OpenAi prompt with no API key returns 503
  Given an active prompt "live-assist" with Provider="OpenAi" exists
  And the API has no OpenAi key configured (default dev posture)
  When the administrator clicks the row's Test (flask) action, enters any inputs, and clicks "Run test"
  Then POST /account/api/admin/ai/prompts/{id}/test returns HTTP 503
  And ApiResult.Error.Code = "AI_PROVIDER_NOT_CONFIGURED"
  And a red error toast surfaces the bilingual MessageForCurrentCulture()
  And the Test modal stays open with no Output description list
```

### E2E-AIP-011 — Deactivate is a soft delete

```gherkin
Scenario: Delete (trash) soft-deactivates and is idempotent
  Given an active prompt "welcome-greeting" exists in the grid
  When the administrator clicks the row's Delete (trash) action in the grid RowActions
  Then DELETE /account/api/admin/ai/prompts/{id} returns 200 (ApiResult<bool> = true)
  And a green SimfAlert reads "Prompt deactivated." / "تمّ تعطيل المحفّز."
  And the grid reloads and the row's Active column shows "—" (row not removed)
  And an audit row 'AiPrompt.Deactivated' is written with the actor id
  When the administrator clicks the Delete (trash) action again on the now-inactive row
  Then the API returns 200 with no further audit row (the service early-returns
      when IsActive is already false)
```

### E2E-AIP-012 — Server 500 on /list

```gherkin
Scenario: API 500 on /list shows the bilingual fallback toast
  Given the API is configured to return 500 on /admin/ai/prompts/list (e.g. DB down)
  When the administrator opens /admin/ai/prompts
  Then the page first shows the "Loading…" text
  And then a red SimfAlert appears reading
      "Could not load AI prompts." / "تعذّر تحميل قائمة المحفّزات."
  And no rows render and no summary line appears
```

### E2E-AIP-013 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the page, Create modal and Test modal
  Given the administrator is on /admin/ai/prompts in English
  When they switch the UI culture to العربية from the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "محفّزات الذكاء الاصطناعي"
  And the toolbar button reads "محفّز جديد"
  And the table column headers read المفتاح / الميزة / الاسم / الموفّر / النموذج /
      الإصدار / مفعّل
  And the nav rail mirrors with Arabic labels

  When they click "محفّز جديد"
  Then the Create modal opens in RTL titled "إنشاء محفّز"
  And the field labels render in Arabic (e.g. "المفتاح (kebab-case، غير قابل للتعديل)",
      "محفّز النظام", "درجة الحرارة (0.0–2.0)")
  And the footer buttons read "حفظ" and "إلغاء" in reverse order

  When they cancel and click "اختبار" on a row
  Then the Test modal opens in RTL titled "اختبار المحفّز"
  And the inputs label reads "المدخلات (سطر لكلٍّ: key=value)"
  And the action button reads "تشغيل الاختبار"
```

### E2E-AIP-014 — Pager summary line

```gherkin
Scenario: Summary line reflects the current page window
  Given the database has more than 20 active prompts (Top defaults to 20)
  When the administrator opens /admin/ai/prompts
  Then POST /account/api/admin/ai/prompts/list returns Skip=0, the first 20 items, Total=Z
  And the summary line under the table reads "Showing 1–20 of {Z}" /
      "عرض 1–20 من {Z}"
```

### E2E-AIP-015 — Per-column filter narrows the grid

```gherkin
Scenario: Typing into a column filter input reloads the grid with Filters[key]
  Given the grid is rendered with rows whose Key/Feature/Name span several values
  And the grid shows a filter row under the headers with a search input under the
      Key, Feature and Name columns (only those three columns are Filterable;
      Provider, Model, Version and Active have no filter input)
  When the administrator types "welcome" into the filter input under the "Key" column
      (aria-label "Filter column Key")
  Then after the 300ms debounce the page issues POST
      /account/api/admin/ai/prompts/list with GridQuery.Filters["key"]="welcome",
      Skip reset to 0, and any prior row selection cleared
  And the grid narrows to only the rows whose Key contains "welcome"
  And the summary line recomputes against the filtered Total

  When the administrator also types "Assistance" into the "Feature" column filter
      (aria-label "Filter column Feature")
  Then the next POST /list carries BOTH GridQuery.Filters["key"]="welcome" AND
      GridQuery.Filters["feature"]="Assistance" with Skip=0

  When the administrator clears the "Key" filter input
  Then the next POST /list drops the "key" entry from GridQuery.Filters
      (only Filters["feature"]="Assistance" remains) and the grid widens accordingly
```

### E2E-AIP-016 — Column sort toggles

```gherkin
Scenario: Clicking a sortable header cycles ascending → descending → ascending
  Given the grid is rendered and the Key, Feature, Name, Provider and Version
      column headers are sortable buttons (Model and Active are not sortable)
  When the administrator clicks the "Key" column header
  Then the page issues POST /account/api/admin/ai/prompts/list with
      GridQuery.Sort="key", SortDescending=false and Skip reset to 0
  And the header shows the ascending (▲) arrow and aria-sort="ascending"
  When the administrator clicks the "Key" header again
  Then the next POST /list carries GridQuery.Sort="key", SortDescending=true
  And the header shows the descending (▼) arrow and aria-sort="descending"
  When the administrator clicks the "Version" column header
  Then the next POST /list carries GridQuery.Sort="version", SortDescending=false
      (switching columns starts a fresh ascending sort) and the "Key" header
      returns to its neutral (↕) state
```

### E2E-AIP-017 — Presentation toggle persists (D-353)

```gherkin
Scenario: Switch to full-page mode and it persists across reload
  Given the administrator is on /admin/ai/prompts with the default "dialog" presentation
  And the grid toolbar's CustomToolbar slot shows the CrudPresentationToggle
      ("Open as full page", maximize icon)
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And the bound _presentation flips to CrudPresentation.Page
  And CpPreferences persists localStorage key "simf.cp.prefs.ai-prompts"
      = {"v":1,"presentation":"page"}
  When they reload /admin/ai/prompts
  Then OnInitializedAsync calls Prefs.GetPresentationAsync("ai-prompts")
      and the toggle restores to "Open as dialog"
  And opening "New prompt" now renders the full-page CrudShell frame (not a popup)
```

### E2E-AIP-018 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/View take over the content area; Save returns to the grid
  Given the presentation is set to "full page" (_presentation = CrudPresentation.Page)
  When the administrator clicks "New prompt"
  Then GridHidden becomes true so the SimfBanner + grid are removed from the DOM
  And the CrudShell renders the AiPromptsAddEdit form full-page (Presentation="Page")
      titled "Create AI prompt", with no modal backdrop
  When they fill Key="welcome-greeting", Feature, Display name (English/Arabic),
      leave Provider="Echo"/Model="echo", and click "Save"
  Then POST /account/api/admin/ai/prompts returns 200
  And CloseForm runs: the full-page frame closes and the grid + banner re-appear
  And a green SimfAlert reads "Prompt saved." with the new row showing Version "v1"
  When they click the row's Edit (pencil) action then the CrudShell close (X) button
      (CloseLabel "Close")
  Then GET /account/api/admin/ai/prompts/{id} had returned 200, the form closes,
      and the grid re-appears unchanged (no PUT fired)
```

### E2E-AIP-019 — Delete confirmation gate (CrudShell + SimfConfirm) (D-353)

```gherkin
Scenario: Deactivate requires explicit confirmation through SimfConfirm
  Given an active prompt "welcome-greeting" exists in the grid
  When the administrator clicks the row's Delete (trash) action in the grid RowActions
  Then GET /account/api/admin/ai/prompts/{id} returns 200
  And a CrudShell opens hosting AiPromptsViewDelete (IsDelete=true), titled
      "Deactivate AI prompt", showing the prompt's read-only description list
      (Key, Feature, Display name, Provider, Model, System prompt, User prompt
      template, Temperature, Max output tokens, Version, Active) and a red
      "Deactivate" button
  When they click the red "Deactivate" button
  Then a SimfConfirm dialog opens (Danger=true) whose message is
      Admin.AiPrompts.Delete.Message formatted with the Key
      ("welcome-greeting"), with confirm "Deactivate" / cancel "Cancel"
  And no DELETE request has fired yet
  When they click "Cancel"
  Then the SimfConfirm closes, the form stays open, and the row is unchanged
  When they click "Deactivate" again and then confirm "Deactivate"
  Then exactly one DELETE /account/api/admin/ai/prompts/{id} fires and returns
      200 (ApiResult<bool> success)
  And the CrudShell closes
  And a green SimfAlert reads "Prompt deactivated." / "تمّ تعطيل المحفّز."
  And the grid reloads and the row's Active column now shows the "off" pill
```

**Note:** delete is **no longer** a one-click trash action that fires DELETE
directly — D-353 routes it through CrudShell + AiPromptsViewDelete and a
SimfConfirm gate (not the native `window.confirm`). E2E-AIP-001 and E2E-AIP-011
above describe the older one-click path and remain for the API-outcome
assertions, but the in-browser flow is the gated one proven here.

### E2E-AIP-020 — Excel export (D-356)

```gherkin
Scenario: Export the AI-prompt grid to an XLSX workbook
  Given the administrator is on /admin/ai/prompts with at least two prompts
  And the toolbar shows the "Export" action (SimfDataGrid OnExport is wired and a
      CrudGridExcel Resource="ai/prompts" is rendered)
  When they click "Export" with no rows selected
  Then OnExportAsync calls _excel.ExportAsync with an empty Ids list and the
      current GridQuery
  And a POST /account/api/admin/ai/prompts/export fires carrying
      AdminGridExportRequest { Ids = [], Query = _query } (Query sent because no
      rows are selected = whole filtered grid)
  And the browser saves an .xlsx workbook whose sheet header row carries the
      prompt columns (Key, Feature, Display name, Provider, Model, Version, Active)
  When they instead tick two rows then click "Export"
  Then the POST carries AdminGridExportRequest { Ids = [those two ids] } and the
      workbook contains exactly those two rows
  And the API caps any export at 5000 rows
```

### E2E-AIP-021 — Excel import (D-356)

```gherkin
Scenario: Import AI prompts from a workbook and see the per-row outcome
  Given the administrator is on /admin/ai/prompts
  And the toolbar shows the "Import" action (SimfDataGrid OnImport is wired)
  When they click "Import"
  Then OnImportAsync calls _excel.TriggerImportAsync which clicks the hidden
      file input id "ai/prompts-import-input" (accept=".xlsx")
  When they choose a valid .xlsx describing two new prompts
  Then a POST /account/api/admin/ai/prompts/import fires as multipart form data
  And the import-result modal shows "2 created, 0 updated, 0 skipped."
  And OnImportedAsync surfaces the shared "Grid.Import.Done" success toast and
      reloads the grid (POST /account/api/admin/ai/prompts/list) showing both rows
  When they import a workbook with one new Key and one already-existing Key
  Then the result modal shows 1 created / 1 updated (or skipped) plus a per-row
      error list naming the affected row
  And the API caps any import at 5000 rows
```

### E2E-AIP-022 — Excel import rejection (bad / wrong-sheet upload) (D-356)

```gherkin
Scenario: A bad upload is rejected without creating anything
  Given the administrator is on /admin/ai/prompts
  When they click "Import" and choose a file that is not a valid .xlsx
      (fails the ZIP-magic check) or exceeds the 5MB gate
  Then POST /account/api/admin/ai/prompts/import returns HTTP 400
  And CrudGridExcel raises OnError so OnExcelError shows a red bilingual toast
  And no AiPrompt row is created and the grid is unchanged
  When they instead upload a workbook whose worksheet is not the expected
      prompts sheet
  Then the request returns HTTP 400 with the bilingual wrong-sheet message
  And again nothing is created
```

---

## Implementation notes

- **Manual smoke is canonical today.** Until a Playwright project exists, the
  canonical run is a Chrome DevTools MCP session: sign in via the Auth setup,
  walk each scenario, capture screenshots into
  `docs/screenshots/cp-admin-ai-prompts-{scenario}.png`. The Gherkin is
  runner-agnostic and converts 1:1 into `.feature` files under a future
  `tests/SIMF.E2E.Tests/`.
- **Lower-layer API integration tests** that cover this same surface without a
  browser live at `tests/SIMF.Api.Tests/AiModuleTests.cs` (CRUD + Echo dry-run +
  invocations log) and `tests/SIMF.Api.Tests/AiHardeningTests.cs` (D-179 input
  caps, the per-admin `ai-test` rate-limit, audit-detail redaction, and the
  provider-not-configured path). The `// Tests:` header on
  `AiPromptAdminEndpoints.cs` still references a file named `AiAdminTests.cs`,
  which does not exist on disk under that name — the equivalent coverage is in
  `AiModuleTests.cs` + `AiHardeningTests.cs`. Worth reconciling that header in a
  separate change.
- **Permission gates** are enforced twice: the API endpoint `Policies(...)` and
  the CP page `[RequirePermission]`. `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`
  and `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` fail the build if a
  gate is missing, so E2E-AIP-003 has a build-time backstop for the page gate.
- **Sibling page:** the AI invocations log lives at `/admin/ai/invocations`
  (`AiInvocations.View`); it is out of scope here and has its own catalogue.

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle): added E2E-AIP-017..022 for the CrudPresentationToggle, full-page CrudShell round-trip, the SimfConfirm delete gate, and Excel export/import (+ rejection). Prior review 2026-06-03 (E2E catalogue rebuild, D-256/D-257 grid affordances reconciled).
