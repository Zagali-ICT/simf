# CP page — Function (العارضون · Exhibitors)

What the administrator does on `/admin/exhibitors` — elements, actions,
navigation and acceptance criteria. Grounded in `ExhibitorsList.razor`,
`ExhibitorsAddEdit.razor` and `ExhibitorsViewDelete.razor`.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## Purpose
Maintain the **exhibitor directory** — the CP-only company records behind
exhibition booths (D-199 #3 / D-202 Track-2) — and provision per-exhibitor login
accounts. An exhibitor is a bilingual name + optional contact details + an
optional shared-Contact link; accounts are created afterwards under the
exhibitor.

## Elements (top → bottom)
1. **`SimfBanner`** titled "Exhibitors" / "العارضون" (`Admin.Exhibitors.Title`).
   Hidden when a CRUD form has taken over the page in full-page mode
   (`GridHidden = FormOpen && _presentation == Page`).
2. **Toast** — an inline `SimfAlert` shown after a save/delete/provision or on a
   load failure (variant `success` / `error`).
3. **`SimfDataGrid`** of `AdminExhibitorSummary` rows with:
   - **Custom toolbar:** `CrudPresentationToggle` (PageKey `"exhibitors"`) — the
     Page↔Popup choice (D-353).
   - **Toolbar actions** (from `SimfDataGrid`): **Add**, **Export**, **Import**,
     plus per-row **Edit** / **Details** / **Delete** quiet icons.
   - **Columns:** Name (English) `nameEn`, Name (Arabic) `nameAr`, Accounts
     `accountCount`, Active `isActive` (an on/off `SimfPill`).
   - **Per-row quiet action — "Accounts" (user icon):** opens the
     account-provisioning modal. It is the **only** affordance wrapped in
     `<AuthorizedAction Permission="Exhibitors.Edit">`.
   - **Empty state:** `SimfEmptyState` titled `Admin.Exhibitors.None`.
4. **`CrudGridExcel`** (`Resource="exhibitors"`) — the hidden Excel export/import
   engine wired to the toolbar Export/Import.
5. **`CrudShell`** (conditional) — hosts `ExhibitorsAddEdit` or
   `ExhibitorsViewDelete` as a popup or full page per `_presentation`.
6. **Account-provisioning `SimfModal`** (conditional) — a separate overlay,
   independent of `CrudShell`.

## User actions

### A. Browse / filter / sort / page
- The grid loads on `OnInitializedAsync` and re-loads on every query change
  (`OnQueryChangedAsync`).
- **Search** — a free-text term matches Name (EN) **or** Name (AR).
- **Per-column filter** — only **Name (English)** (`nameEn`) and **Name
  (Arabic)** (`nameAr`) are `Filterable`. Accounts and Active are not.
- **Sort** — **Name (English)**, **Name (Arabic)** and **Active** are
  `Sortable`; default order is **Name (Arabic) ascending**. Accounts is **not**
  sortable (it is a computed sub-query).
- **Paging** — default page size 20 (`Top = 20`); the server clamps `Top` to
  1–200 (default 25 when unset).

### B. Add an exhibitor
- Toolbar **Add** → `CrudShell` opens `ExhibitorsAddEdit` (`IsEdit=false`) titled
  "Add exhibitor" (`Admin.Exhibitors.Add.Title`).
- Fields: **Name (English)**, **Name (Arabic)**, **Contact email**, **Contact
  phone**, **Website**, **Contact** (a `ContactPicker`). The **Active** checkbox
  is **hidden on Add** (a created exhibitor is always active server-side).
- **Save** → `POST /account/api/admin/exhibitors`; on success the shell closes, a
  green toast "Exhibitor saved." (`Admin.Exhibitors.Saved`) shows and the grid
  reloads.

### C. Edit an exhibitor
- Row **Edit** (pencil) → first `GET /account/api/admin/exhibitors/{id}` for the
  full `AdminExhibitorDetail`, then `ExhibitorsAddEdit` (`IsEdit=true`) titled
  "Edit exhibitor". The **Active** checkbox **is** shown here.
- **Save** → `PUT /account/api/admin/exhibitors/{id}`; same success toast + grid
  reload.

### D. View details (read-only)
- Row **Details** → `GET {id}` then `ExhibitorsViewDelete` (`IsDelete=false`)
  titled "Exhibitor details" — a `<dl>` of Name (EN), Name (AR), Contact email,
  Contact phone, Website, Active (empty optional fields render as "—"). **No**
  Deactivate button. **Close** dismisses.

### E. Delete (soft-deactivate)
- Row **Delete** (trash) → `GET {id}` then `ExhibitorsViewDelete`
  (`IsDelete=true`) with a red **Deactivate** button.
- Deactivate → a **`SimfConfirm`** dialog (Danger) titled "Deactivate exhibitor"
  with `Admin.Exhibitors.Delete.Message` formatted with the English name (there
  is **no** native `window.confirm`, D-353).
- Confirm → `DELETE /account/api/admin/exhibitors/{id}`; green toast "Exhibitor
  deleted." (`Admin.Exhibitors.Deleted`) + grid reload. Cancel is a no-op.

### F. Provision a per-exhibitor account
- Row **Accounts** (user icon, gated by `Exhibitors.Edit`) → a `SimfModal`
  titled "Accounts — {NameEn}" (`Admin.Exhibitors.Accounts.Title`) opens and
  `GET /account/api/admin/exhibitors/{id}/accounts` lists existing accounts
  (table: Contact name / Email / Role / Active; empty → `SimfEmptyState`
  `Admin.Exhibitors.Accounts.None`). An info alert explains a provisioned account
  is a pending-approval app login tagged to the exhibitor.
- The provision form has **Contact name** (≤256), **Email** (≤320) and **Role
  label** (≤128, optional). **Provision account** → a client guard requires
  Contact name + Email (else the red toast `Admin.Exhibitors.Provision.Required`,
  no POST), then `POST /account/api/admin/exhibitors/{id}/accounts`.
- On success: the form resets, a green toast "Account provisioned…"
  (`Admin.Exhibitors.Provision.Done`) shows, the accounts list reloads **and**
  the grid reloads so the **Accounts** count increments.

### G. Excel export / import (D-356)
- **Export** → `OnExportAsync(selected)` → `_excel.ExportAsync(ids, _query)` →
  `POST /account/api/admin/exhibitors/export`. With **no rows selected** the
  current grid `Query` is sent (whole filtered grid); with a **selection** only
  the selected `Ids` are sent. Downloads `simf-exhibitors-{timestamp}.xlsx`,
  sheet "Exhibitors", header `NameEn | NameAr | ContactEmail | ContactPhone |
  Website | AccountCount | IsActive`; 5000-row cap.
- **Import** → `_excel.TriggerImportAsync()` opens the hidden file picker
  (`accept=".xlsx"`) → `POST /account/api/admin/exhibitors/import` (multipart
  "file"). A result modal reports "N created, N updated, N skipped" + per-row
  errors; on completion a green toast (`Grid.Import.Done`) shows and the grid
  reloads. Import is **insert-only** (required headers `NameEn | NameAr`).

### H. Presentation toggle (D-353)
- `CrudPresentationToggle` switches Add/Edit/View/Delete between **dialog**
  (popup) and **full page**. The choice persists in `localStorage`
  (`simf.cp.prefs.exhibitors`) and is restored on load via
  `Prefs.GetPresentationAsync("exhibitors")`. Default is **Dialog**.

## Navigation
- **Into** the page: the CP nav item **Exhibitors** (`Module.Exhibitors`,
  icon `briefcase`) under the **Exhibition** group; gated by
  `PermissionCatalog.Exhibitors.View`.
- **Within** the page: Add/Edit/Details/Delete open `CrudShell`; Accounts opens a
  modal. There is no further drill-down route.
- An admin lacking `Exhibitors.View` (and not the `Administrator` wildcard `*`)
  is sent to `/not-permitted`.

## Acceptance criteria
- The grid lists exhibitors with NameEn / NameAr / Accounts / Active and pages,
  sorts (NameEn, NameAr, Active) and filters (NameEn, NameAr).
- Add creates an active exhibitor (no Active checkbox on Add); Edit can toggle
  Active; Delete soft-deactivates behind a `SimfConfirm` gate.
- Account provisioning creates a pending-approval Visitor login and increments
  the row's Accounts count.
- Excel export honours selection-else-grid; import is insert-only with per-row
  errors.
- All CRUD/provision/export/import enforcement is **API-side** (per-action
  policies); the page gate is `Exhibitors.View`.

## E2E
See [`docs/tests/e2e/cp-admin-exhibitors.md`](../../tests/e2e/cp-admin-exhibitors.md)
— E2E-EXH-001 (CRUD round-trip) … 020 (account provisioning) … 023 (Excel import
rejection).
</content>
