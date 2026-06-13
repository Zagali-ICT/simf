# CP Content blocks — Function (`/admin/content-blocks`)

What an Administrator can **do** on this page. Grounded in
`ContentBlocksList.razor` + the two reusable forms. The rules behind each
action are in [admin-content-blocks_Logic.md](admin-content-blocks_Logic.md);
the contract is in [admin-content-blocks_API.md](admin-content-blocks_API.md).

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## Who can use it
A signed-in admin whose role carries `ContentBlocks.View` (or `Administrator`
wildcard `"*"`). Without it the page redirects to `/not-permitted` and the nav
item is hidden (`CpNavigation` `RequiredPermission = ContentBlocks.View`).
Each mutation is additionally gated at the API: Edit→`ContentBlocks.Edit`,
Delete→`ContentBlocks.Delete`, Export→`ContentBlocks.Export`,
Import→`ContentBlocks.Import`.

## F1 — Browse the blocks (the grid)
- On load the page calls `/account/api/admin/content-blocks/list` with
  `GridQuery { Top = 20 }` and shows up to 20 rows/page.
- Columns: **Key** (in `<code>`), **English** (first 80 chars of `Content` +
  "…"), **Last updated** (`yyyy-MM-dd HH:mm UTC`), **Active** (on/off pill).
- **Filter** per column on **Key** and **English** (the inputs post
  `Filters["key"]` / `Filters["content"]` and reset paging to page 1).
- **Sort** on **Key**, **English**, **Last updated** (click header to toggle
  asc/desc; default Key ascending). Active is not sortable.
- **Select-all / per-row checkboxes** exist but only narrow the Excel export;
  there is **no bulk delete**.
- Empty list → `SimfEmptyState` ("No content blocks…"); the New-block button
  stays.

## F2 — Create a block ("New block")
- Click the toolbar **Add** ("New block"). A `CrudShell` opens the
  `ContentBlockAddEdit` form titled **Add content block**, with the **Key field
  enabled**.
- Fill **Key** (e.g. `home.welcome.title`), **Content (English)**, **Content
  (Arabic)**, leave **Active** ticked, then **Save**.
- On success the form closes, a green toast reads **"Content block saved."**
  (`Admin.ContentBlocks.Saved`), and the grid reloads with the new row.

## F3 — Edit a block (key locked)
- Click a row's **Edit** (pencil). The same form re-opens pre-filled from the
  grid row (**no detail fetch**); the **Key field is disabled** (`IsEdit`).
- Change the English/Arabic body and/or the Active checkbox → **Save**.
- The upsert updates the **same row in place** (same id); toast "Content block
  saved."; grid reloads.

## F4 — View details
- Click a row's **Details** (eye). The `ContentBlockViewDelete` form opens
  **read-only** (`IsDelete=false`) showing Key, Content (English), Content
  (Arabic), Last updated, Active as a description list. No Delete button. Close
  with the secondary **Close** button (or the CrudShell close).

## F5 — Delete a block (soft, with a confirm gate)
- Click a row's **Delete** (trash). The `ContentBlockViewDelete` form opens with
  `IsDelete=true` and a red **Delete** button.
- Clicking red **Delete** raises a `SimfConfirm` (Danger) whose message
  interpolates the **Key** (`Delete the content block "<key>"?`). **Cancel** =
  no DELETE; the row is unchanged.
- **Confirm** fires exactly one `DELETE /account/api/admin/content-blocks/<key>`
  (the Key is `Uri.EscapeDataString`-encoded — it is the path, not a row id).
  On success: toast **"Content block deleted."** (`Admin.ContentBlocks.Deleted`);
  the row's Active pill flips to **off**; the row **stays visible** (soft
  deactivate). Deleting an already-inactive block is an idempotent no-op (still
  succeeds).

## F6 — Re-use an existing key (upsert-in-place)
- From **New block**, entering a Key that already exists (any case — it is
  normalised to lower-case) does **not** create a duplicate and does **not**
  error: it overwrites the existing row in place. The grid row count is
  unchanged.

## F7 — Excel export (D-356)
- Toolbar **Export**. With **no rows ticked** it exports the **whole filtered
  grid**; with rows ticked it exports **only those rows' ids**. The browser
  downloads an `.xlsx`. (Columns + caps per `docs/pages/cp/admin-content-blocks.md` §5.)

## F8 — Excel import (D-356)
- Toolbar **Import** opens the hidden file input (`accept=".xlsx"`). Choosing a
  workbook posts it multipart; on success an import-result modal reports
  "N created, N updated, N skipped" with a per-row error list, then the shared
  **`Grid.Import.Done`** toast fires and the grid reloads. A non-`.xlsx` /
  wrong-sheet / over-cap upload is rejected (400) and surfaces a red toast;
  nothing is created.

## F9 — Presentation toggle (dialog ⇄ full page) (D-353)
- The toolbar `CrudPresentationToggle` switches whether Add/Edit/View/Delete are
  hosted as a **dialog** (default) or **full page**. The choice persists in
  `localStorage` (`simf.cp.prefs.content-blocks`) and is restored on reload. In
  full-page mode the grid + banner are hidden while a form is open.

## Errors the admin sees
- **List load failed / server 500** → a red toast
  (`env.Error.MessageForCurrentCulture()` ?? `Admin.ContentBlocks.LoadFailed`);
  no rows.
- **Save validation** (short key, > 8000-char body) → the form **stays open**
  with a red `SimfAlert` carrying the API's bilingual message (400
  `CONTENT_BLOCK_INVALID`). There is **no client-side length check** beyond a
  present, ≤ 128-char Key guard (`Admin.ContentBlocks.Required`).
- **Delete of a missing key** → 404 `CONTENT_BLOCK_NOT_FOUND`, red alert in the
  form.

## Downstream effect (why it matters)
Editing/deactivating a block changes what the **public app + Website** read live
(no redeploy). The well-known keys are a wire contract: `terms` (app Page 009),
`about` (app Home/About), and `cyber.*` (app policy screen). Renaming a Key
breaks the client that codes against the slug.
