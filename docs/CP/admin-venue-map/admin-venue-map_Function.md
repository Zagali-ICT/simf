# Venue map nodes — Function (`/admin/venue-map`)

What the operator does on this page. Grounded in `VenueMapList.razor` +
`VenueMapAddEdit.razor` + `VenueMapViewDelete.razor` and the
`VenueMapEndpoints` / `VenueMapExcelEndpoints` / `VenueMapService` they call.
Verified against code this session.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> Companion docs: [Design](admin-venue-map_Design.md) ·
> [API](admin-venue-map_API.md) · [Logic](admin-venue-map_Logic.md).

## Who can use it

- **Reach the page:** an admin whose role carries `VenueMap.View` (or the wildcard
  `Administrator = "*"`). The page is `@attribute [RequirePermission(PermissionCatalog.VenueMap.View)]`;
  the nav-rail item (`Module.VenueMap`) is hidden without it; a signed-in admin
  lacking it is routed to `/not-permitted`.
- **Act on the page:** the toolbar/row affordances are individually gated —
  `VenueMap.Create` (New node / Import), `VenueMap.Edit` (Edit), `VenueMap.Delete`
  (Delete), `VenueMap.Export` (Export), `VenueMap.Import` (Import). A view-only
  admin sees the grid but none of the gated affordances.

## What it is for

The Logistics team places the **2D venue-map nodes** the Flutter app renders
(SIMF-FDS-006 §5.3/§7, FR-605). Each node is a marker on the venue's 2D map: a
bilingual label, a `Kind` (Hall / Zone / Booth / PointOfInterest), an `(X, Y)`
position, and an **optional** link to a Hall **or** a Booth. The table **ships
empty** — so the first thing the operator sees is the empty state, and the first
job is to add nodes.

## Actions

### Browse / search / sort
- The grid lists every node (active and inactive). The **Label** column has a
  per-column filter that matches the English **or** Arabic label server-side
  (resets paging to the first page). The grid-wide search matches the same two
  fields.
- **Label** and **Kind** are sortable (click the header; default is Label
  ascending). **Position** and **Active** are not sortable.

### Add a node ("New node")  — gated `VenueMap.Create`
1. Click **New node** → the `VenueMapAddEdit` form opens in `CrudShell` (dialog or
   full page per the toggle). The Hall + Booth pickers load at mount.
2. Fill **Label (English)** and **Label (Arabic)** (both required, ≤ 128 chars),
   choose a **Kind**, optionally set **X** / **Y** (step 0.1, default 0), and
   optionally pick a **Linked hall** or **Linked booth** ("— None —" leaves it
   unset). The **Active** checkbox is **not** shown when adding.
3. Click **Save** → `POST /account/api/admin/venue-map`. On success the form
   closes, a green toast `Admin.VenueMap.Saved` shows, and the grid reloads with
   the new row.

### Edit / move / re-link a node  — gated `VenueMap.Edit`
1. Click the row **Edit** (pencil) → `GET /account/api/admin/venue-map/{id}` fetches
   the full detail, then `VenueMapAddEdit` opens pre-filled (every field, **plus**
   the now-visible **Active** checkbox reflecting `IsActive`).
2. Change the labels / Kind / X / Y / links, and/or tick or untick **Active**.
3. Click **Save** → `PUT /account/api/admin/venue-map/{id}`. Same success toast +
   grid reload. Unticking **Active** here is the in-form way to take a node off the
   app map without deleting it.

### View details  — read-only (needs only `VenueMap.View`)
- Click the row **Details** (eye) → `VenueMapViewDelete` opens read-only, listing
  Label, Label (Arabic), Kind, Position, **Hall** (resolved name), **Booth**
  (resolved name) and Active. The Hall/Booth name is fetched only when the node
  actually links one. Only a **Close** button.

### Delete (soft)  — gated `VenueMap.Delete`
1. Click the row **Delete** (trash) → `VenueMapViewDelete` opens showing the
   read-only details and a red **Delete** button.
2. Click **Delete** → a **`SimfConfirm`** (danger) appears, titled
   `Admin.VenueMap.Delete.Title`, its message naming the node by its **English**
   label.
   - **Cancel** → no request fires; the node is untouched.
   - **Delete (confirm)** → `DELETE /account/api/admin/venue-map/{id}` (soft delete:
     `IsActive = false`). Green toast `Admin.VenueMap.Deleted`, the grid reloads and
     the row drops out of the public app map. A second delete on an already-inactive
     node still returns success (idempotent).

### Export to Excel  — gated `VenueMap.Export`
- Click **Export** with no rows selected → the whole filtered grid is exported;
  select rows first → only those. `POST /export` returns
  `simf-venue-map-{timestamp}.xlsx`, sheet `VenueMap`, header
  `Label | LabelArabic | Kind | X | Y | Hall | Booth | IsActive`. Hall/Booth are
  written as the linked record's human-readable code (empty if deactivated).
  Capped at 5000 rows.

### Import from Excel  — gated `VenueMap.Import`
- Click **Import** → the file picker opens (`accept=".xlsx"`). Choose a workbook
  whose **VenueMap** sheet has the required headers `Label | LabelArabic | Kind`
  (plus optional `X`, `Y`, `Hall`, `Booth`). `POST /import` (multipart) inserts
  each row as a **new** node (insert-only — it cannot update or deactivate).
  The result modal shows `{Created}/{Updated}/{Skipped}` and a per-row error list;
  a green `Grid.Import.Done` toast shows and the grid reloads. Hall/Booth resolve
  by code; a blank cell leaves the link unset; an unknown code or Kind makes that
  one row an error without aborting the rest.

## Golden path (happy-flow)

Add → Edit (move + deactivate) → Delete, mirroring **E2E-VMP-001**:

1. **New node** → Label (English) `Main Entrance`, Label (Arabic) `المدخل الرئيسي`,
   Kind `PointOfInterest`, X `120.5`, Y `88`, no links → **Save** → 200 →
   green "saved" toast → row shows `Main Entrance`, `PointOfInterest`,
   `120.5, 88`, Active ✓.
2. **Edit** that row → X `200`, Y `150.4`, untick **Active** → **Save** → 200 →
   Position reads `200, 150.4`, Active reads inactive.
3. **Delete** that row → **Delete** → `SimfConfirm` names "Main Entrance" →
   confirm **Delete** → 200 → green "removed" toast → row gone from the grid and
   from `GET /app/venue-map`.

## Validation the operator hits

| Rule | Where | Effect |
|------|-------|--------|
| Both labels required | client guard (`HandleSubmitAsync`) | inline `Admin.VenueMap.Required` error; **no POST** |
| Label 1–128 chars | server (`ValidateLabels`) | `400 VENUE_MAP_NODE_INVALID` "Both labels are required and must be 1–128 characters." |
| Linked hall must be active | server (`EnsureReferencesAsync`) | `400 VENUE_MAP_NODE_INVALID` "The referenced hall was not found." |
| Linked booth must be active | server (`EnsureReferencesAsync`) | `400 VENUE_MAP_NODE_INVALID` "The referenced booth was not found." |
| Node still exists | server (`GET`/`PUT`/`DELETE`) | `404 VENUE_MAP_NODE_NOT_FOUND` |

A stale picker (a Hall/Booth deactivated after the form loaded) surfaces as the
`400` on Save; a node another admin just deleted surfaces as the `404` when you
open Edit. Server errors render via `MessageForCurrentCulture()`, falling back to
`Admin.VenueMap.LoadFailed` (list) / `Admin.VenueMap.Fallback` (form).

## Bilingual / RTL

All visible text is resx (`Admin.VenueMap.*` + shared `Grid.*`); the
`العربية` / `English` header link toggles culture and mirrors the page, grid, both
CRUD forms and the Excel modal under `dir="rtl"`. See the EN resx-gap caveat in
[Design](admin-venue-map_Design.md).

## Cross-references
- The data this page writes is read by **[App Page 015](../../App/Page_015/README.md)**
  (venue map) via `GET /app/venue-map` — see [API §A6](admin-venue-map_API.md).
- E2E scenarios: [`docs/tests/e2e/cp-admin-venue-map.md`](../../tests/e2e/cp-admin-venue-map.md)
  (E2E-VMP-001 … 024).
