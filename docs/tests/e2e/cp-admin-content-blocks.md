# E2E test catalogue — Content blocks (`/admin/content-blocks`)

| | |
|--|--|
| **Page** | [`cp/admin-content-blocks.md`](../../pages/cp/admin-content-blocks.md) |
| **Route** | `/admin/content-blocks` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-07-31 (`FR-1203-markdown-render` — content is plain text) |

> **Page summary.** The Content blocks page (D-173, gap doc G8, PDF §1, §2.1)
> is the dynamic-CMS admin surface: editable key/value text blocks (welcome
> message, page copy, labels, the `cyber.*` policy text the Flutter app reads)
> surfaced on the public Website + mobile app. The page is the **standard
> `SimfDataGrid`** (D-255/D-256 — migrated from a raw table) with columns
> Key, English (preview), Last updated and Active, a toolbar **Add ("New
> block")** action, **one shared Add/Edit modal** carrying four fields
> (`Key`, `Content (English)`, `Content (Arabic)` and an `Active` checkbox),
> and quiet per-row **icon** actions: **Edit** (pencil), **Details** (eye) and
> **Delete** (trash). _(D-353/D-356, 2026-06-10) — the page now frames Add / Edit
> / View / Delete through `CrudShell` (popup or full page per the toolbar toggle),
> not the old inline `SimfModal`. Delete no longer one-clicks: it opens the
> `ContentBlockViewDelete` form and a `SimfConfirm` gates the soft-delete-by-Key
> (see E2E-CNT-017). The toolbar also carries **Export** + **Import** (Excel)._
> The grid loads `new GridQuery { Top = 20 }`, so it shows up to **20 rows per
> page** with the standard prev/next/first/last pager.
>
> The grid carries the standard **per-column filter inputs** on the
> `Filterable` columns — **Key** (`key`) and **English** (`contentEn`) — and is
> **sortable** on Key (`key`), English (`contentEn`) and Last updated
> (`lastUpdatedAt`). The list endpoint honours these as `GridQuery.Filters`
> entries and `GridQuery.Sort` (see `AdminCmsService.ListContentBlocksAsync`).
> The grid shows select-all / per-row checkboxes (`Multiselect="true"`), but
> there is **no `CustomToolbar` bulk action** wired — selection is cosmetic, so
> there is no bulk scenario here. There is **no separate read-only "Details"
> view** — "Edit" re-opens the same modal pre-filled and is the read-back path.
>
> **Upsert is keyed, not id-based.** The same `PUT /admin/content-blocks`
> serves both create and edit; the server normalises the key (`Trim()` +
> `ToLowerInvariant()`) and creates the row if absent or **updates it in
> place** if present. In the modal the **`Key` field is disabled while
> editing** (`Disabled="_busy || _isEdit"`), so a duplicate-key collision
> can only be reached from the **New block** path — and it does **not** error,
> it silently upserts onto the existing row (see E2E-CNT-005).
>
> `RequiredPermission` = `PermissionCatalog.ContentBlocks.View`. The upsert is
> additionally gated by `ContentBlocks.Edit` and delete by
> `ContentBlocks.Delete` at the API layer.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-CNT-001 | Golden path — New block → grid → Edit (read-back + change) → Delete | happy | P0 | _to author_ |
| E2E-CNT-002 | New block: create one block, toast + grid row + audit | happy | P0 | _to author_ |
| E2E-CNT-003 | Edit content block: Key field disabled, content updated in place | happy | P1 | _to author_ |
| E2E-CNT-004 | Delete (idempotent deactivate): row Active flips to "—" | happy | P1 | _to author_ |
| E2E-CNT-005 | Re-using an existing key from "New block" upserts in place (no duplicate) | happy | P1 | _to author_ |
| E2E-CNT-006 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-CNT-007 | Auth gate: signed-in admin lacking `ContentBlocks.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-CNT-008 | Validation: too-short key (`< 2` chars) → `CONTENT_BLOCK_INVALID` 400 | error | P1 | _to author_ |
| E2E-CNT-009 | Validation: content over 8000 chars → `CONTENT_BLOCK_INVALID` 400 | error | P1 | _to author_ |
| E2E-CNT-010 | Delete a missing/already-removed key → `CONTENT_BLOCK_NOT_FOUND` / idempotent | error | P2 | _to author_ |
| E2E-CNT-011 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-CNT-012 | RTL / Arabic render: page + Add modal mirror | i18n | P1 | _to author_ |
| E2E-CNT-013 | Per-column filter narrows the grid (Key / English) | happy | P1 | _to author_ |
| E2E-CNT-014 | Column sort toggles (Key ascending ⇄ descending) | happy | P2 | _to author_ |
| E2E-CNT-015 | Presentation toggle persists across reload (`simf.cp.prefs.content-blocks`) (D-353) | happy | P1 | _to author_ |
| E2E-CNT-016 | Full-page mode round-trip: Add/Edit/View take over the content area, Save returns to grid (D-353) | happy | P1 | _to author_ |
| E2E-CNT-017 | Delete confirmation gate (CrudShell + SimfConfirm; Cancel = no DELETE, confirm = one DELETE) (D-353) | error | P0 | _to author_ |
| E2E-CNT-018 | Excel export — whole filtered grid vs selected rows (D-356) | happy | P1 | _to author_ |
| E2E-CNT-019 | Excel import — upload workbook → result modal "N created…" + per-row error (D-356) | happy | P1 | _to author_ |
| E2E-CNT-020 | Excel import rejection — non-.xlsx / wrong-sheet upload → 400 + bilingual toast, nothing created (D-356) | error | P1 | _to author_ |
| E2E-CNT-021 | Content is **plain text, not markdown or HTML** — a `<script>` payload round-trips as visible text on the CP pane, the Website and the app | security | P0 | authored ✓ (`ContentBlockPlainTextContractTests`) |
| E2E-CNT-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-CNT-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-CNT-001 — Golden path (New → grid → Edit → Delete)

```gherkin
Feature: Content blocks CRUD round-trip
  As an Administrator
  I want to manage the dynamic CMS content blocks
  So that the Website + Flutter copy stays editable without a redeploy

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator (superadmin@zagali-ict.com) has signed in via /login + /login/totp
    using a fresh code from the Get-Totp helper
  And they have landed on /admin/content-blocks
  And the grid has finished loading (no "Loading content blocks…" text)

Scenario: Create, read back via Edit, change in place, then delete one block
  Given the grid currently shows {N} rows
  When the administrator clicks "New block"
  Then the Edit modal opens titled "Edit content block"
  And it shows four inputs: "Key (e.g. home.welcome.title)", "Content (English)",
      "Content (Arabic)", and an "Active" checkbox (ticked)
  And the "Key" field is enabled (this is the create path)
  When they fill Key="home.welcome.title"
  And they fill Content (English)="Welcome to SIMF 2027"
  And they fill Content (Arabic)="مرحباً بكم في سيمف 2027"
  And the Active checkbox stays ticked
  And they click "Save"
  Then the modal closes
  And a green SimfAlert reads "Content block saved." at the top of the surface
  And the grid shows {N + 1} rows
  And a row exists with Key="home.welcome.title", an English preview starting "Welcome to SIMF 2027",
      a "Last updated" timestamp of "now" in "yyyy-MM-dd HH:mm UTC" format, and Active = "✓"

  When the administrator clicks the row's Edit (pencil) action
  Then the Edit modal re-opens with the row's values pre-filled
  And the "Key" field is DISABLED and reads "home.welcome.title"
  And "Content (English)" reads "Welcome to SIMF 2027"
  And "Content (Arabic)" reads "مرحباً بكم في سيمف 2027"
  And the "Active" checkbox is ticked
  When they change Content (English) to "Welcome back to SIMF 2027"
  And they click "Save"
  Then the modal closes
  And a green SimfAlert reads "Content block saved."
  And the same row (no new row added) now previews "Welcome back to SIMF 2027"
  And the row id is unchanged (in-place update, not a second row)

  When the administrator clicks the row's Delete (trash) action
  Then a green SimfAlert reads "Content block deleted."
  And the row's Active column flips from "✓" to "—"
  And the row remains visible (delete is a soft deactivate, not a hard remove)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-content-blocks-golden-before.png`
- Screenshots: `docs/screenshots/cp-admin-content-blocks-{add-modal,edit-modal,after-delete}.png`
- Console errors: 0 expected
- Network: `POST /account/api/admin/content-blocks/list` → 200; `PUT /account/api/admin/content-blocks` (create) → 200; `PUT /account/api/admin/content-blocks` (edit) → 200; `DELETE /account/api/admin/content-blocks/home.welcome.title` → 200
- Audit rows: two `OperationLog`/audit rows with `Event = 'ContentBlock.Upserted'` (create + edit, `Detail = "key=home.welcome.title"`) and one with `Event = 'ContentBlock.Deactivated'`, all carrying the actor's user id

### E2E-CNT-002 — New block (single create)

```gherkin
Scenario: Create one content block from the New block button
  Given the administrator is on /admin/content-blocks
  When they click "New block"
  And they fill Key="footer.copyright"
  And they fill Content (English)="© 2027 Royal Saudi Naval Forces"
  And they fill Content (Arabic)="© 2027 القوات البحرية الملكية السعودية"
  And they click "Save"
  Then the BFF forwards PUT /account/api/admin/content-blocks with the UpsertContentBlockRequest
  And the API returns HTTP 200 with ApiResult.Data.Key="footer.copyright"
  And the modal closes
  And a green SimfAlert reads "Content block saved."
  And a new grid row appears with Key="footer.copyright" and Active="✓"
```

### E2E-CNT-003 — Edit content block (key locked, content updated)

```gherkin
Scenario: Edit pre-fills the row and disables the key
  Given a content block with Key="home.welcome.title" exists
  When the administrator clicks the row's Edit (pencil) action
  Then the Edit modal opens with Key, Content (English), Content (Arabic) and Active pre-filled
  And the "Key" input is rendered disabled (cannot be changed on the edit path)
  When they untick the "Active" checkbox
  And they click "Save"
  Then the API upserts the row in place (same id) with IsActive=false
  And a green SimfAlert reads "Content block saved."
  And the row's Active column reads "—"
```

### E2E-CNT-004 — Delete (idempotent deactivate)

```gherkin
Scenario: Delete deactivates the row rather than hard-deleting it
  Given an active content block with Key="promo.banner" exists (Active = "✓")
  When the administrator clicks the row's Delete (trash) action
  Then the BFF forwards DELETE /account/api/admin/content-blocks/promo.banner
  And the API returns HTTP 200 with ApiResult.Data = true
  And a green SimfAlert reads "Content block deleted."
  And the row stays in the grid with its Active column now "—"
  And a subsequent public read GET /api/v1/content/promo.banner returns 404 (inactive blocks are hidden publicly)
```

### E2E-CNT-005 — Re-using an existing key upserts in place (no duplicate)

```gherkin
Scenario: New block with a key that already exists updates the existing row
  Given a content block with Key="home.welcome.title" and Content (English)="Welcome to SIMF 2027" exists
  When the administrator clicks "New block"
  And they fill Key="HOME.WELCOME.TITLE"   # different case — the server normalises to lower-case
  And they fill Content (English)="Overwritten copy"
  And they fill Content (Arabic)="نسخة محدثة"
  And they click "Save"
  Then the API normalises the key to "home.welcome.title" and updates the EXISTING row in place
  And NO second row is created (the grid row count is unchanged)
  And a green SimfAlert reads "Content block saved." (there is no duplicate/conflict error for this page)
  And the existing row's English preview now reads "Overwritten copy"
```

### E2E-CNT-006 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no ContentBlock rows
  When the administrator opens /admin/content-blocks
  Then once loading finishes the grid body is replaced by the SimfEmptyState component
  And the empty state title reads "No content blocks yet." / "لا توجد كتل بعد."
  And the toolbar still shows the "New block" button
  And no error SimfAlert appears
```

### E2E-CNT-007 — Auth gate (missing ContentBlocks.View permission)

```gherkin
Scenario: A signed-in admin lacking the ContentBlocks.View permission is denied
  Given a signed-in Control-Panel user whose role does NOT include "ContentBlocks.View"
    and who is not the Administrator wildcard ("*")
  When they navigate to /admin/content-blocks
  Then the [RequirePermission(PermissionCatalog.ContentBlocks.View)] gate redirects them to /not-permitted with HTTP 200
  And no POST /account/api/admin/content-blocks/list request fires
  And the "Content blocks" item is hidden in the nav rail (CpNavigation RequiredPermission = ContentBlocks.View)
```

### E2E-CNT-008 — Validation: too-short key

```gherkin
Scenario: A key shorter than 2 characters is rejected by the API
  Given the Add modal is open from "New block"
  When the administrator fills Key="a"   # 1 character, below the 2-128 bound
  And fills Content (English)="x"
  And clicks "Save"
  Then the BFF forwards PUT /account/api/admin/content-blocks
  And the API returns HTTP 400 with ApiResult.Error.Code = "CONTENT_BLOCK_INVALID"
  And the modal STAYS open (env.Success is false)
  And a red SimfAlert surfaces the bilingual MessageForCurrentCulture():
      "Content block key must be between 2 and 128 characters." /
      "يجب أن يتراوح طول مفتاح المحتوى بين 2 و 128 حرفاً."
```

### E2E-CNT-009 — Validation: content over 8000 characters

```gherkin
Scenario: Content longer than 8000 characters is rejected
  Given the Add modal is open from "New block"
  When the administrator fills Key="long.block"
  And fills Content (English) with a string of 8001 characters
  And clicks "Save"
  Then the API returns HTTP 400 with ApiResult.Error.Code = "CONTENT_BLOCK_INVALID"
  And the modal stays open
  And a red SimfAlert reads "Content cannot exceed 8000 characters." /
      "لا يمكن أن يتجاوز المحتوى 8000 حرف."
```

### E2E-CNT-010 — Delete a missing / already-removed key

```gherkin
Scenario: Deleting a key that no longer exists returns CONTENT_BLOCK_NOT_FOUND
  Given a content block with Key="stale.key" was already hard-removed from the DB out of band
  When the administrator clicks the row's Delete (trash) action on a row whose Key="stale.key"
  Then the BFF forwards DELETE /account/api/admin/content-blocks/stale.key
  And the API returns HTTP 404 with ApiResult.Error.Code = "CONTENT_BLOCK_NOT_FOUND"
  And a red SimfAlert surfaces "Content block not found." / "لم يتم العثور على المحتوى."

Scenario: Deleting an already-inactive block is idempotent (no error)
  Given a content block with Key="promo.banner" exists but is already inactive (Active = "—")
  When the administrator clicks the row's Delete (trash) action
  Then the API returns HTTP 200 with ApiResult.Data = true (the deactivate is a no-op)
  And a green SimfAlert reads "Content block deleted."
```

### E2E-CNT-011 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the bilingual fallback toast
  Given the API is configured to return 500 on POST /admin/content-blocks/list (e.g. DB down)
  When the administrator opens /admin/content-blocks
  Then the page first shows "Loading content blocks…"
  And then a red SimfAlert appears reading
      "The content blocks could not be loaded." / "تعذّر تحميل كتل المحتوى."
  And no grid rows render
  And the "New block" button is still present
```

### E2E-CNT-012 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/content-blocks in English
  When they switch the culture to Arabic (العربية)
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "كتل المحتوى"
  And the toolbar button reads "كتلة جديدة"
  And the grid headers read "المفتاح", "الإنجليزية", "آخر تحديث", "مفعّل"
  And the nav rail mirrors with Arabic labels

  When they click "كتلة جديدة"
  Then the Edit modal opens in RTL titled "تعديل كتلة المحتوى"
  And the field labels read "المفتاح (مثلاً home.welcome.title)", "المحتوى (الإنجليزية)",
      "المحتوى (العربية)" and the checkbox label "مفعّل"
  And the footer actions read "إلغاء" and "حفظ" in reverse order
```

### E2E-CNT-013 — Per-column filter narrows the grid

```gherkin
Scenario: Typing in a column filter narrows the grid and resets paging
  Given the grid on /admin/content-blocks has loaded its first page (GridQuery { Top = 20 })
  And it currently shows more than 20 rows across multiple pages
  And rows exist with Key="home.welcome.title" and Key="footer.copyright"
  When the administrator types "home" into the "Filter column Key" input on the Key column
  Then a POST /account/api/admin/content-blocks/list fires with
      GridQuery.Filters["key"]="home" and GridQuery.Skip reset to 0 (back to page 1)
  And the grid re-renders showing only rows whose Key contains "home"
      (e.g. "home.welcome.title" stays, "footer.copyright" is gone)
  And the summary line counts only the narrowed total

  When the administrator clears the Key filter
  And types "Welcome" into the "Filter column English" input on the English column
  Then a POST .../list fires with GridQuery.Filters["contentEn"]="Welcome" and Skip=0
  And the grid shows only rows whose English content contains "Welcome"
  And clearing the filter restores the full first page

  # Grounding: AdminCmsService.ListContentBlocksAsync honours Filters["key"] and
  # Filters["contentEn"] (case-insensitive Contains); unknown columns are ignored.
```

### E2E-CNT-014 — Column sort toggles

```gherkin
Scenario: Clicking a sortable header toggles ascending / descending
  Given the grid on /admin/content-blocks has loaded (default order is Key ascending)
  When the administrator clicks the "Key" column header
  Then a POST /account/api/admin/content-blocks/list fires with
      GridQuery.Sort="key" and GridQuery.SortDescending=false
  And the rows render in ascending Key order
  When they click the "Key" header again
  Then a POST .../list fires with GridQuery.Sort="key" and GridQuery.SortDescending=true
  And the rows render in descending Key order
  And sorting the "Last updated" column instead posts GridQuery.Sort="lastUpdatedAt"

  # Grounding: only Key (key), English (contentEn) and Last updated (lastUpdatedAt)
  # are Sortable; the Active column is not sortable.
```

### E2E-CNT-015 — Presentation toggle persists across reload (D-353)

```gherkin
Scenario: Switch to full-page mode and it persists across reload
  Given the administrator is on /admin/content-blocks with the default "dialog" presentation
  And the grid toolbar shows the CrudPresentationToggle ("Open as full page", maximize icon)
  When they click the toggle
  Then the toggle label changes to "Open as dialog" (window icon)
  And localStorage key "simf.cp.prefs.content-blocks" holds {"v":1,"presentation":"page"}
  When they reload /admin/content-blocks
  Then OnInitializedAsync calls Prefs.GetPresentationAsync("content-blocks")
  And the toggle still reads "Open as dialog"
  And opening "New block" now renders the full-page CrudShell frame (not a popup)

  # Grounding: ContentBlocksList sets PageKey="content-blocks"; the toggle binds
  # _presentation and CpPreferences persists it to localStorage as
  # "simf.cp.prefs.{PageKey}". Default is CrudPresentation.Dialog.
```

### E2E-CNT-016 — Full-page mode round-trip (D-353)

```gherkin
Scenario: Add/Edit/View take over the content area; Save returns to the grid
  Given the presentation is set to "full page" (simf.cp.prefs.content-blocks = "page")
  When the administrator clicks "New block"
  Then the grid + SimfBanner are hidden (GridHidden) and the CrudShell renders the
      ContentBlockAddEdit form full-page titled "Add content block"
  And there is no modal backdrop
  When they fill Key="home.hero.subtitle"
  And they fill Content (English)="A maritime forum for the region"
  And they fill Content (Arabic)="منتدى بحري للمنطقة"
  And they click "Save"
  Then the CrudShell closes and the grid re-appears
  And a green SimfAlert reads "Content block saved."
  And the new row is present

  When they click the row's Details (eye) action
  Then the ContentBlockViewDelete form opens full-page in read-only mode
      (no "Delete" button, IsDelete=false) showing Key, Content (English),
      Content (Arabic), Last updated and Active in a description list
  When they click the CrudShell "Close" (X) header / "Close" button
  Then the form closes and the grid re-appears unchanged
```

### E2E-CNT-017 — Delete confirmation gate (CrudShell + SimfConfirm) (D-353)

```gherkin
Scenario: Delete requires explicit confirmation; Cancel fires no DELETE
  Given the administrator is on /admin/content-blocks
  And an active content block with Key="promo.banner" exists (Active = "✓")
  When they click the row's Delete (trash) action
  Then a CrudShell opens hosting the ContentBlockViewDelete form (IsDelete=true)
      titled "Delete content block"
  And it shows the row's read-only details and a red "Delete" button
  When they click the red "Delete" button
  Then a SimfConfirm dialog appears (Danger=true) titled "Delete content block"
  And its message reads "Delete the content block \"promo.banner\"?" with the Key interpolated
      (string.Format Admin.ContentBlocks.Delete.Message)
  When they click "Cancel"
  Then the SimfConfirm closes (_confirming=false)
  And NO DELETE request fires and the row is unchanged

  When they re-open Delete, click the red "Delete", then click the confirm "Delete" button
  Then exactly one DELETE /account/api/admin/content-blocks/promo.banner fires
      (the Key is Uri.EscapeDataString-encoded into the path — NOT a row id)
  And the API returns HTTP 200 with ApiResult.Data = true
  And the CrudShell closes
  And a green SimfAlert reads "Content block deleted."
  And the row's Active column flips from "✓" to "—" (soft deactivate, row stays visible)

  # Grounding: delete is gated by SimfConfirm inside ContentBlockViewDelete, NOT a
  # native window.confirm; the old one-click trash delete is gone (D-353).
```

### E2E-CNT-018 — Excel export (D-356)

```gherkin
Scenario: Export the filtered grid, then just the selected rows, to an XLSX workbook
  Given the administrator is on /admin/content-blocks with at least two content blocks
  When they click the toolbar "Export" action with NO rows selected
  Then OnExportAsync calls _excel.ExportAsync with an empty Ids list and the current _query
  And a POST /account/api/admin/content-blocks/export fires with
      AdminGridExportRequest { Ids: [], Query: <the current GridQuery> }
  And the browser saves an .xlsx workbook (the whole filtered grid, capped at 5000 rows)
  And the workbook's header row carries the content-block columns (Key, Content, ContentArabic, IsActive)

  When they instead tick two row checkboxes then click "Export"
  Then OnExportAsync passes those two rows' Ids (and Query is omitted when rows are selected)
  And a POST .../export fires with AdminGridExportRequest.Ids = [those two ids]
  And the workbook contains exactly those two rows

  # Grounding: ContentBlocksList wires OnExport=OnExportAsync and renders
  # <CrudGridExcel Resource="content-blocks">; OnExportAsync =>
  # _excel.ExportAsync(selected.Select(r => r.Id), _query).
```

### E2E-CNT-019 — Excel import (D-356)

```gherkin
Scenario: Import content blocks from a workbook and see the per-row outcome
  Given the administrator is on /admin/content-blocks
  When they click the toolbar "Import" action
  Then OnImportAsync calls _excel.TriggerImportAsync(), opening the hidden
      file <input id="content-blocks-import-input" accept=".xlsx">
  When they choose an .xlsx whose sheet has Key / Content / ContentArabic rows for two new blocks
  Then a POST /account/api/admin/content-blocks/import fires as multipart form data
  And the import-result modal shows "2 created, 0 updated, 0 skipped."
  And OnImportedAsync raises the shared "Grid.Import.Done" success toast and reloads the grid
  And the grid now lists both new blocks

  When they import a workbook containing one row whose Key already exists and one new Key
  Then the modal reports 1 created and 1 updated (upsert-by-Key) — or surfaces a per-row
      error list for any malformed row
  And the import is capped at 5000 rows by the API

  # Grounding: ContentBlocksList wires OnImport=OnImportAsync (=> _excel.TriggerImportAsync())
  # AND OnImported=OnImportedAsync (success toast = Grid.Import.Done + LoadAsync()).
```

### E2E-CNT-020 — Excel import rejection (bad / wrong-sheet upload) (D-356)

```gherkin
Scenario: A bad upload is rejected without creating anything
  Given the administrator is on /admin/content-blocks
  When they import a file that is not a valid .xlsx (fails the ZIP-magic check, or exceeds the 5MB gate)
  Then the API returns HTTP 400
  And CrudGridExcel raises OnError, so OnExcelError surfaces a red bilingual SimfAlert
  And no content block is created or updated

  When they import a workbook whose worksheet is not the expected content-blocks sheet
  Then the request returns HTTP 400 with the bilingual "expected worksheet" message
  And nothing is created

  # Grounding: the API caps export+import at 5000 rows and rejects a non-.xlsx
  # upload (ZIP-magic + 5MB gate) with HTTP 400; OnExcelError sets an error Toast.
```

### E2E-CNT-021 — Content is plain text, not markdown or HTML (FR-1203)

```gherkin
Feature: A content block is text, and nothing renders it as markup
  ContentBlock's XML doc said "markdown allowed" while no surface rendered
  markdown. The ruling (2026-07-30) was to correct the CONTRACT, not to build a
  renderer: every key in use is a short plain-text field (an eyebrow, a heading,
  a counter, a button label), rendering markdown would change what already-seeded
  production copy looks like, and it would mean injecting HTML built from an
  admin-editable field into a public page that today has no such path.

Background:
  Given the administrator is on /admin/content-blocks

Scenario: an HTML payload is stored and shown back as text
  When they create the block "qa.plaintext.probe" with English content:
    """
    <script>alert('xss')</script> **bold** # heading
    """
  Then the save succeeds
  And the grid's English preview shows that text literally, including the angle brackets
  And the Details pane shows it literally
  And the page's DOM contains NO <script> element
  And the browser console reports zero errors

Scenario: the public Website shows the same text, still as text
  Given the block is bound to a landing key the hydrator reads
  When an anonymous visitor loads "/"
  Then the rendered section shows the markup as visible characters
  And the page's DOM contains NO <script> element originating from that value
  And no '#' or '**' has been interpreted as formatting

Scenario: the Flutter app shows the same text, still as text
  When the app reads the block through GET /api/v1/content/{key}
  Then the string is rendered in a Text widget exactly as stored

  # Cleanup: soft-delete "qa.plaintext.probe" when the sweep finishes.
```

> **Unit backing.** `ContentBlockPlainTextContractTests` proves the CP half by
> rendering `ContentBlockViewDelete` with the payload above (no `<script>`
> element, `&lt;script&gt;` in the markup, the exact text in `textContent`), and
> ratchets the other two halves at source: no `(MarkupString)` cast anywhere in
> the CP, the Website or the shared components wraps anything but a
> server-generated SVG, and `site-content.js` keeps its five-replacement `esc()`,
> its `textContent` single-value path and exactly four `innerHTML` sinks.

---

## Implementation notes

- **API integration tests** at `tests/SIMF.Api.Tests/CmsTests.cs` cover the
  same surface at a lower layer (no browser) and are the lower-tier safety net
  for these scenarios:
  - `Admin_upsert_creates_then_updates_in_place` — backs E2E-CNT-002 / -003 /
    -005 (proves the same id is reused on a second upsert of the same key).
  - `Delete_content_block_makes_subsequent_public_read_404` — backs
    E2E-CNT-004 / -010 (delete = deactivate; public read then 404s).
  - `Non_admin_caller_is_forbidden_on_content_block_upsert` — backs the API
    half of the auth gate E2E-CNT-007 (a Visitor token → HTTP 403 on upsert).
  - `Public_read_returns_active_block` / `Public_read_of_inactive_block_returns_404`
    / `If_modified_since_returns_304_when_unchanged` / `Public_batch_returns_only_existing_active_keys`
    — the public read side that consumes what this admin page writes.
  - `Cybersecurity_policy_blocks_are_seeded_by_IdentitySeeder` — guards the
    well-known `cyber.*` keys (a Flutter wire contract) that an admin must not
    break from this page.
  Note these tests hit the API directly at `/api/v1/admin/content-blocks`; the
  CP page reaches the same endpoints through the BFF passthrough at
  `/account/api/admin/content-blocks*` (see
  `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs`).

- **No client-side validation.** The razor performs no length checks before
  the PUT — every validation path (E2E-CNT-008 / -009) is enforced server-side
  in `AdminCmsService.UpsertContentBlockAsync` and surfaces back through
  `env.Error.MessageForCurrentCulture()`. Drive these scenarios by actually
  submitting the out-of-bound value, not by expecting an inline field error.

- **Manual smoke is the canonical run today.** Until Playwright is adopted,
  walk each scenario in a Chrome DevTools MCP session (sign in per the Auth
  setup, capture screenshots into `docs/screenshots/cp-admin-content-blocks-*.png`).

- **Convert to Playwright** when the runner lands: copy each Gherkin scenario
  into a `.feature` file under `tests/SIMF.E2E.Tests/` plus a step-definition
  class. The Gherkin is already runner-agnostic.

---

_Last reviewed:_ 2026-07-31 by Claude (`FR-1203-markdown-render` — added E2E-CNT-021, content is plain text on every surface); prior review 2026-06-10 (D-356 Phase 5 — Excel + toggle): added E2E-CNT-015..020 (presentation toggle, full-page round-trip, CrudShell+SimfConfirm delete gate, Excel export, Excel import, Excel import rejection); prior review 2026-06-03 (D-256/D-257 grid affordances reconciled).
