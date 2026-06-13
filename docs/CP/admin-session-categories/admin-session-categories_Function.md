# CP Session categories — Function (`/admin/session-categories`)

What the administrator does on this page. Grounded in the as-built CP page
(`SessionCategoriesList.razor` + `SessionCategoriesAddEdit.razor` +
`SessionCategoriesViewDelete.razor`) and the backing endpoints.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> **Page purpose.** Manage the dynamic session-category lookup (D-226) that
> backs the CP session form's category picker and rides the app agenda payload
> as the "is-main-session / type" tag. The list **ships empty** (OI-2), so the
> first render is the empty state until the team seeds rows.

## Privilege / auth gate

**Administrator only.** The page carries
`@attribute [RequirePermission(PermissionCatalog.SessionCategories.View)]`; a
signed-in admin whose role lacks `SessionCategories.View` is routed to
`/not-permitted` and the `Module.SessionCategories` nav item is hidden for that
user. Each mutating action is independently gated (Create / Edit / Delete /
Export / Import) at both the CP (`AuthorizedAction` via the grid) and the API
(`Policies(...)`) layer. All API mutations also require
`RequireApprovedAccount` and are rate-limited under the `auth` limiter.

## Elements (top → bottom, as built)

1. **Banner** (`SimfBanner`) — title `Admin.SessionCategories.Title`. Hidden
   when a form is open in full-page presentation (`GridHidden`).
2. **Inline alert** (`SimfAlert`) — a transient success / error toast
   (`_toast`) rendered above the grid (e.g. "Category saved." / "Category
   deleted." / a load-failure message).
3. **Data grid** (`SimfDataGrid`, the owner-mandated list-page standard) with:
   - **Toolbar actions:** **Add** (`OnAdd`), **Export** (`OnExport`),
     **Import** (`OnImport`), plus a **presentation toggle**
     (`CrudPresentationToggle`, `PageKey="session-categories"`) in
     `CustomToolbar`.
   - **Columns:** Name (English) · Name (Arabic) · Order · Active. Active
     renders a `SimfPill` (`on` = `Grid.Active`, `off` = `Grid.Inactive`).
   - **Per-row actions** (quiet grid affordances): **Edit** (pencil,
     `OnEditOne`) · **Details** (eye, `OnDetailsOne`) · **Delete** (trash,
     `OnDeleteOne`).
   - **Multiselect:** select-all / per-row checkboxes render, but there is **no
     bulk-action toolbar button** (selection feeds Export only — no bulk
     delete/update endpoint).
   - **Empty template:** `SimfEmptyState` (`Admin.SessionCategories.None`).
   - **Numbered pager** with first/prev/next/last + page-size + summary
     (`GridQuery { Top = 20 }`).
4. **CrudShell** (rendered when a form is open) framing one of the two reusable
   forms as a **dialog** or a **full page** per the presentation toggle.

## What the administrator does

1. **Browse + page** the lookup. `OnInitializedAsync` reads the persisted
   presentation preference then loads page 1 via
   `POST /account/api/admin/session-categories/list`.
2. **Filter per column.** A filter input is exposed on **Name (English)**
   (`name`) and **Name (Arabic)** (`namearabic`) only (the only `Filterable`
   columns). Typing re-queries the list (Skip reset). The grid's global
   `Search` also matches either name.
3. **Sort columns.** All four columns are `Sortable` (`name` / `namearabic` /
   `order` / `isActive`); clicking a header toggles asc/desc. Default
   (unsorted) order is **DisplayOrder, then Name**.
4. **Add a category.** Toolbar **Add** opens `SessionCategoriesAddEdit`
   (`IsEdit=false`) titled `Admin.SessionCategories.Add.Title`. Four inputs:
   Name (English), Name (Arabic), Display order (number, defaults `"0"`), and —
   **only in Edit** — an Active checkbox. **Save** → `POST` → on success the
   form closes, a success toast shows, the grid reloads.
5. **Edit a category.** Per-row pencil first fetches the full row via
   `GET …/{id}` (buttons disabled while in flight), then opens the same form
   `IsEdit=true` pre-filled, with the **Active** checkbox visible. **Save** →
   `PUT …/{id}`.
6. **View details (read-only).** Per-row eye fetches `GET …/{id}` then opens
   `SessionCategoriesViewDelete` (`IsDelete=false`) — a read-only `<dl>` of the
   four fields plus a **Close** button (no Deactivate button).
7. **Delete (soft).** Per-row trash fetches `GET …/{id}` then opens
   `SessionCategoriesViewDelete` (`IsDelete=true`): a red **Deactivate** button
   raises a `SimfConfirm` dialog naming the row
   (`Admin.SessionCategories.Delete.Message`). Confirming fires `DELETE …/{id}`
   (soft-delete / `Deactivate`); the row **stays visible** with its Active pill
   flipped to **Inactive** (the list applies no active filter). Cancelling the
   confirm makes **no** request.
8. **Export to Excel.** Toolbar **Export** posts
   `AdminGridExportRequest { Ids, Query }` to `…/export`: with no rows selected
   it exports the **whole filtered grid** (empty `Ids` + current `Query`); with
   rows ticked it exports **just those ids**. Workbook sheet
   **"SessionCategories"**, header `Name | NameArabic | DisplayOrder | IsActive`.
9. **Import from Excel.** Toolbar **Import** triggers the hidden file input
   (accept `.xlsx`) and posts the workbook (multipart) to `…/import`
   (insert-only). On return: the shared result outcome + `Grid.Import.Done`
   success toast, then the grid reloads. A non-`.xlsx` / oversized / wrong-sheet
   upload returns HTTP 400 surfaced via `OnExcelError`.
10. **Toggle presentation.** The `CrudPresentationToggle` switches
    Add/Edit/View/Delete between a **dialog** and a **full page**; the choice
    persists in `localStorage` under `simf.cp.prefs.session-categories` (via
    `CpPreferences`) and is restored on next open. In full-page mode the grid +
    banner are hidden while a form is open.

## Toasts (via `_toast` / `SimfAlert`)

| Trigger | Variant | Resx key |
|---------|---------|----------|
| Save (create/update) success | success | `Admin.SessionCategories.Saved` |
| Delete (deactivate) success | success | `Admin.SessionCategories.Deleted` |
| Import completed | success | `Grid.Import.Done` |
| List load failed | error | `envelope.Error.MessageForCurrentCulture()` else `Admin.SessionCategories.LoadFailed` |
| Detail load failed | error | `envelope.Error.MessageForCurrentCulture()` else `Admin.SessionCategories.LoadFailed` |
| Excel error (bad upload) | error | message from `OnExcelError` |

(Exact bilingual phrasing lives in the `Strings` resx files — descriptive here,
not quoted verbatim. In-form validation errors are covered in the Logic doc.)

## Cross-links

- Contract / DTOs: [admin-session-categories_API.md](admin-session-categories_API.md)
- Field mapping + validation + audit: [admin-session-categories_Logic.md](admin-session-categories_Logic.md)
- Screen layout + states + RTL: [admin-session-categories_Design.md](admin-session-categories_Design.md)
- CP reference: [`docs/pages/cp/admin-session-categories.md`](../../pages/cp/admin-session-categories.md)
- E2E: [`docs/tests/e2e/cp-admin-session-categories.md`](../../tests/e2e/cp-admin-session-categories.md)
- Consumer (app agenda): [`docs/App/Page_016/`](../../App/Page_016/README.md)
