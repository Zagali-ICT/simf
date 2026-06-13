# Exhibition booths — Function (`/admin/booths`)

What the operator does on this page and what each action triggers. Grounded in
`BoothsList.razor` + `BoothsAddEdit.razor` + `BoothsViewDelete.razor` +
`AdminBoothService.cs`. The contract is in [admin-booths_API.md](admin-booths_API.md);
the screen is in [admin-booths_Design.md](admin-booths_Design.md).

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## Who can reach it

- **Page gate.** `@attribute [RequirePermission(PermissionCatalog.Booths.View)]`
  — an admin without `Booths.View` (and not the wildcard `Administrator = "*"`)
  is redirected to `/not-permitted` and the nav item `Module.Booths` is hidden.
- **Per-action gates** are enforced **at the API** (the CP buttons are not
  `<AuthorizedAction>`-wrapped): Create / Edit / Delete / Export / Import each
  require their own permission, so an admin with only `Booths.View` sees the
  buttons but the call returns `403`.

## On load

1. `OnInitializedAsync` reads the saved presentation
   (`Prefs.GetPresentationAsync("booths")`), sets `_loading = true`, then loads
   the two cached lookups — `POST /account/api/admin/halls/list` and
   `.../exhibitors/list` (`Top=500`, active rows only) — so the grid's Hall +
   Exhibitor columns can resolve names.
2. `LoadAsync` calls `POST /account/api/admin/booths/list` with `_query`
   (`Top=20`) and binds `_page`.
3. The grid renders the page, or `SimfEmptyState` (`Admin.Booths.None`) when
   empty, or a red toast (`Admin.Booths.LoadFailed` fallback) on failure.

## Actions

### Add a booth
- Toolbar **Add** → `OnAddAsync` opens an empty `BoothsAddEdit` in CrudShell
  (title `Admin.Booths.Add.Title`). The form loads its own active exhibitor +
  hall picker lists.
- Fill the fields (see Design §"Add / Edit form"); **Code, Name (English), Name
  (Arabic)** are required.
- **Save** → `HandleSubmitAsync` runs the client guard, then
  `POST /account/api/admin/booths` (`AdminCreateBoothRequest`, blank strings sent
  as `null`). On success: the shell closes, a green `Admin.Booths.Saved` toast
  shows, and the grid reloads. Created booths are always active.

### Edit a booth
- Row **Edit** (pencil) → `OnEditAsync` first GETs
  `GET /account/api/admin/booths/{id}` for the full detail, then opens
  `BoothsAddEdit` pre-filled (title `Admin.Booths.Edit.Title`). Only Edit shows
  the **Active** checkbox.
- **Save** → `PUT /account/api/admin/booths/{id}` (`AdminUpdateBoothRequest`,
  carries `IsActive`). Same success path. Unticking **Active** soft-deactivates
  on save (a deactivate path independent of the Delete button).

### View details
- Row **Details** → `OnDetailsAsync` GETs the detail and opens `BoothsViewDelete`
  read-only (`IsDelete=false`, no Delete button, title
  `Admin.Booths.Details.Title`). Close with `Admin.Booths.Details.Close`.

### Delete (soft-delete) a booth
- Row **Delete** (trash) → `OnDeleteAsync` GETs the detail and opens
  `BoothsViewDelete` in delete mode (`IsDelete=true`, red Delete button, title
  `Admin.Booths.Delete.Title`).
- Clicking the red **Delete** opens a `SimfConfirm` (Danger) whose message is
  `Admin.Booths.Delete.Message` formatted with the booth's **English name**.
- Confirm → `DELETE /account/api/admin/booths/{id}` (`simfAccount.deleteJson`).
  Success → green `Admin.Booths.Deleted` toast + grid reload; the row drops from
  the grid and from the public exhibition list + venue map. Cancel fires no
  request. (This replaced the old native `window.confirm`, D-353.)

### Excel export
- Toolbar **Export** → `OnExportAsync` → `_excel.ExportAsync(selectedIds, _query)`
  → `POST /account/api/admin/booths/export`. With rows selected those `Ids` win;
  otherwise the whole filtered `Query`. Downloads
  `simf-booths-{timestamp}.xlsx` (sheet **Booths**: Code | Name | NameArabic |
  Exhibitor [English name] | Sector | Hall [Code] | IsActive). Capped 5000 rows.

### Excel import
- Toolbar **Import** → `OnImportAsync` → `_excel.TriggerImportAsync()` opens the
  hidden `<input id="booths-import-input" accept=".xlsx">`. The chosen workbook
  posts to `POST /account/api/admin/booths/import`; the result modal shows
  "N created, N updated, N skipped" + per-row errors, then `OnImportedAsync`
  shows the shared `Grid.Import.Done` toast and reloads the grid.
- **Insert-only.** Required headers `Code`, `Name`, `NameArabic`; optional
  `Exhibitor` resolves by English name and `Hall` by Code (active, case-insensitive).
  Officer fields, the Contact link and Map X/Y are **never** imported — set them
  afterwards via Edit. A bad row (e.g. duplicate/short Code, unresolved FK) is a
  per-row error and never aborts the batch.

### Page ↔ Popup toggle
- The `CrudPresentationToggle` (in the grid `<CustomToolbar>`) flips Add/Edit/
  View between a popup dialog and a full page; the choice persists in
  `localStorage` (`simf.cp.prefs.booths`) and is restored on the next load. In
  full-page mode the banner + grid hide while a form is open.

### Search / filter / sort
- The grid search box matches Code / Name / NameArabic. Per-column filters apply
  to Code, Name, NameArabic, Sector; sortable columns are Code (default), Name,
  Sector, Active. The **Exhibitor** and **Hall** columns expose no filter/sort
  (client-resolved). Each change reissues `POST /account/api/admin/booths/list`.

## Validation (server is the source of truth)

`AdminBoothService.ValidateAndNormalise` (+ the FK guards):
- **Code** — trimmed, **upper-cased**, 2–16 chars; case-insensitive uniqueness.
- **Name** 1–128; **NameArabic** 1–128.
- **Officer** name ≤256, phone ≤32, email ≤320 **and must contain `@`**.
- **Sector** EN/AR ≤128; **Description** EN/AR ≤2048.
- **HallId** must be an **active Hall**; **ExhibitorId** an **active Exhibitor**;
  **ContactId** an existing **active Contact** — each else `400 BOOTH_INVALID`.
- Every failure throws `ApiException(ErrorCodes.BoothInvalid, 400, …)`; a
  duplicate Code throws `BoothCodeDuplicate` (409); a missing id throws
  `BoothNotFound` (404).
- **Client-side guard** (`BoothsAddEdit.HandleSubmitAsync`): blocks the request
  when Code / Name (English) / Name (Arabic) is blank → `Admin.Booths.Required`
  toast, no POST.

## Toast strategy

| Event | Key | Variant |
|-------|-----|---------|
| Create / update saved | `Admin.Booths.Saved` | success |
| Deleted | `Admin.Booths.Deleted` | success |
| Import complete | `Grid.Import.Done` | success |
| List load failed | `Admin.Booths.LoadFailed` (or server message) | error |
| Halls lookup failed | `Admin.Booths.HallsLoadFailed` | error |
| Exhibitors lookup failed | `Admin.Booths.ExhibitorsLoadFailed` | error |
| Required fields blank (client) | `Admin.Booths.Required` | error (in-form) |
| Server validation / generic | server `MessageForCurrentCulture()` or `Admin.Booths.Fallback` | error (in-form) |

## Golden path

Add (`A-12`, both names, an active exhibitor, officer + map position) → row
appears active → Edit (change Sector / untick Active) → reopen shows the change
→ Delete (red button → SimfConfirm → confirm) → row drops from the grid and the
public exhibition list + venue map. Audit: one `Booth.Created`, one
`Booth.Updated`, one `Booth.Deactivated`. Full Gherkin: E2E-BTH-001 in
[`docs/tests/e2e/cp-admin-booths.md`](../../tests/e2e/cp-admin-booths.md).
