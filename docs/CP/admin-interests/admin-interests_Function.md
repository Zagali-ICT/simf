# CP — Interests — Function (`/admin/interests`)

What the admin does on this page. Grounded in `InterestsList.razor`,
`InterestAddEdit.razor`, `InterestViewDelete.razor` and `CrudGridExcel.razor`.

Last updated: 2026-06-13 — CP config-page documentation (D-380)

## Privilege / auth gate
**Administrator (or any role granted `Interests.View`).** Reaching the page requires
`@attribute [RequirePermission(PermissionCatalog.Interests.View)]`; an admin without
that permission does not see the nav item and is bounced from the route. Each write
needs its own per-action permission (`Interests.Create` / `.Edit` / `.Delete` /
`.Export` / `.Import`). Administrator holds the wildcard `*` and so has all of them.
An unauthenticated visitor is challenged to `/login`; an authenticated non-admin
lands on `/not-permitted`.

## Why the page exists
Interests is the small admin-managed lookup the **visitor profile interests step**
(app Page 007‑01) picks from (1–10 selections). The page lets an admin keep that
picker accurate without a code change: add a topic when a new stream is announced,
deactivate one that stopped being relevant, reorder so the most popular sit at the
top. It is **lookup-table CRUD** — no workflow, no approvals; every change takes
effect on the visitor picker the next time it loads.

## Elements (top → bottom, as built)
1. **Banner** — `<SimfBanner Title="@L["Admin.Interests.Title"]" />`; title only
   (AR **الاهتمامات** / EN **Interests**), no subtitle, no Actions slot.
2. **Inline alert** — a `SimfAlert` surfaces the success/error toast (load failure,
   created/updated/deactivated, import result/error) above the grid.
3. **`SimfDataGrid`** with:
   - **Toolbar** — Select-all checkbox; a **presentation toggle**
     (`CrudPresentationToggle`, dialog ↔ full page); **Add**; **Export**; **Import**.
     Per-row: **Edit**, **Details**, **Deactivate** icon actions.
   - **Columns** — Name · Name (Arabic) · Order · Status (`SimfPill`
     Active/Inactive). (Details on the columns are in the Design doc.)
   - **Pager** — First / Prev / numbered / Next / Last + page-size + summary.
   - **Empty template** — `SimfEmptyState` with `Admin.Interests.None`
     ("No interests yet.").
4. **`CrudShell`** — hosts the Add/Edit and View/Delete forms as a centred dialog or
   a full-page panel (per the toggle), same route, no navigation.
5. **`CrudGridExcel`** — the hidden file input + import-result modal behind the
   toolbar Export/Import actions.

## What the admin does
1. **Browse / search / filter / sort** — the grid loads page 1
   (`POST /account/api/admin/interests/list`, `Top=20`); typing in the filter,
   changing a column filter, sorting a column or paging re-issues the query.
2. **Add an interest** — toolbar **Add** opens `InterestAddEdit` (IsEdit=false). Fill
   **Name (English)**, **Name (Arabic)**, **Display order** → **Create interest**.
   On success: form closes, grid reloads, green toast
   `Interest "{name}" was created.`
3. **Edit an interest** — per-row **Edit** opens `InterestAddEdit` (IsEdit=true) with
   the row pre-filled **and** an extra **Active** checkbox. Change any field → **Save
   changes** → green toast `Interest "{name}" was updated.` This is also how a
   deactivated interest is **re-activated** (tick Active).
4. **View details** — per-row **Details** opens `InterestViewDelete` (IsDelete=false):
   a read-only `<dl>` of Name / Name (Arabic) / Order / Status. **Close** dismisses it.
5. **Deactivate** — per-row **Deactivate** opens `InterestViewDelete` (IsDelete=true):
   the same read-only details plus a red **Deactivate** button. Clicking it raises a
   `SimfConfirm` naming the interest; confirming fires
   `DELETE /account/api/admin/interests/{id}` → green toast
   `Interest "{name}" was deactivated.` and the row's pill flips to grey **Inactive**.
   (Cancel fires no request.)
6. **Export** — toolbar **Export** downloads an `.xlsx` (sheet `Interests`, file
   `simf-interests-…`) of the **selected rows**, or the **whole filtered grid** when
   none are selected. Columns: Name · NameArabic · DisplayOrder · IsActive.
7. **Import** — toolbar **Import** opens the OS file picker; the chosen `.xlsx` (sheet
   `Interests`, headers `Name`, `NameArabic`) is uploaded
   (`POST /account/api/admin/interests/import`). A result modal reports
   created / updated / skipped + per-row errors; the grid reloads.
8. **Switch presentation** — the toolbar **dialog ↔ full page** toggle decides whether
   the four forms open as a popup or a full-width in-place panel; the choice persists
   per browser (`localStorage` key `simf.cp.prefs.interests`, via `CpPreferences`).

## Acceptance criteria
- The page is reachable only with `Interests.View`; each write needs its own per-action
  permission; an ungated reach is impossible (`[RequirePermission]` + endpoint policies).
- Add / Edit / Deactivate round-trip through the BFF and reload the grid; success and
  error both surface as a bilingual alert/toast.
- Deactivate is **confirmed** (D-353) and **soft** — the row stays, the pill turns
  grey, and the app picker stops offering it on its next fetch.
- A duplicate English name is rejected (409) and surfaced; the unique index on
  `Interest.Name` is the backstop.
- Export reflects the current selection/filter; import is insert-only with per-row
  error reporting and rejects a bad/wrong-sheet upload without creating anything.
- Whatever is created/edited/ordered/deactivated here is exactly what the app's
  Page 007‑01 picker shows on its next `GET /app/account/interests`.

## Where it fits
Reference-data group (`Nav.ReferenceData`) alongside Countries, Organisations,
Contacts and Profile types. It is the **canonical `SimfDataGrid` list-page exemplar**
the other CP list pages follow.
