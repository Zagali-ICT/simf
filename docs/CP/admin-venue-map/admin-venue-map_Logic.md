# Venue map nodes — Logic (`/admin/venue-map`)

The state, data model and server rules behind the page. Grounded in
`VenueMapService.cs`, the `VenueMapNode` entity, the `VenueMapNodeKind` enum, the
`VenueMap.cs` / `PublicVenueMap.cs` contracts and the `VenueMapEndpoints` /
`VenueMapExcelEndpoints`. Verified against code this session.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> Companion docs: [Design](admin-venue-map_Design.md) ·
> [API](admin-venue-map_API.md) · [Function](admin-venue-map_Function.md).

## Data model

The node lives on `SimfAppDbContext` as `dbo.VenueMapNodes` (migration `App/D230`,
D-230). The entity (`SIMF.Domain.Venue.VenueMapNode`) carries:

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | PK, `Guid.NewGuid()` at create |
| `Label` | `string` | EN label, 1–128 |
| `LabelArabic` | `string` | AR label, 1–128 |
| `Kind` | `VenueMapNodeKind` | `Hall=0 · Zone=1 · Booth=2 · PointOfInterest=3` |
| `X` | `double` | map design-space X |
| `Y` | `double` | map design-space Y |
| `HallId` | `Guid?` | **optional, bare logical FK** to a Hall (D-157) |
| `BoothId` | `Guid?` | **optional, bare logical FK** to a Booth (D-157) |
| `IsActive` | `bool` | soft-delete flag (`Deactivate()` sets it false) |
| `CreatedAt` | `DateTimeOffset` | set from `TimeProvider.GetUtcNow()` at create |
| `UpdatedAt` | `DateTimeOffset?` | set on every update and on deactivate |

### `VenueMapNodeKind` (frozen-name enum, `SIMF.Common.Enums`)
```
Hall = 0
Zone = 1
Booth = 2
PointOfInterest = 3
```
A `Hall` / `Booth` node typically references the Hall / Booth it represents via
`HallId` / `BoothId`; `Zone` / `PointOfInterest` nodes are free-standing labels
with a position. Nothing in the service *enforces* that pairing — any Kind may have
or omit a link; the only constraint is that a present link must resolve to an
**active** record. The enum is **additive-only** per the freeze (no rename/reorder).

## Bare-Guid links — resolve-on-read (D-157)

`HallId` and `BoothId` are **bare guids**, not EF navigations and not DB foreign
keys. Hall and Booth are App entities (same `SimfAppDbContext`), but the node never
stores or returns their **name** — D-157 forbids duplicating one entity's data
inside another's row. The link name is resolved on demand:

- **CP View form** resolves the name client-side by fetching the hall/booth list
  (only when a link is present) and matching the id.
- **Excel export/import** resolves Hall/Booth by their human-readable **code**
  (loaded once per request).
- **The app** receives only `hallId` / `boothId` and resolves a booth via its own
  `GET /app/booths` read; **no hall name and no booth name ships on the node**.

## List query rules (`VenueMapService.ListAsync`)

- `Skip` floored at 0; `Top` clamped to `[1, 500]` (50 when ≤ 0).
- Per-column filter `Filters["label"]` → `Label.Contains(v) || LabelArabic.Contains(v)`;
  unknown filter columns are ignored.
- Grid-wide `Search` → `LIKE %term%` on `Label` **or** `LabelArabic`.
- Sort switch on `(Sort?.ToLowerInvariant(), SortDescending)`: `label`
  asc/desc, `kind` asc/desc; **default = `Label` ascending**.
- Returns **all** rows (active + inactive); `AsNoTracking()`; projects straight to
  `AdminVenueMapNodeSummary`.

## Write rules

### Create (`CreateAsync`)
1. `ValidateLabels` trims both labels and requires each `Length ∈ [1, 128]`, else
   `400 VENUE_MAP_NODE_INVALID` ("Both labels are required and must be 1–128
   characters.").
2. `EnsureReferencesAsync` — if `HallId` is set it must match an **active** Hall
   (else `400` "The referenced hall was not found."); same for `BoothId` ("…booth…").
3. New `VenueMapNode` with `IsActive = true`, `CreatedAt = now`; saved.
4. Audit `OperationLog` row `VenueMapNode.Created` (`AuditEvents.VenueMapNodeCreated`,
   `Outcome.Success`, `ActorUserId`, detail `id=…; label=…; kind=…`); info log.

### Update (`UpdateAsync`)
1. Load the node by id or `404 VENUE_MAP_NODE_NOT_FOUND`.
2. Re-run `ValidateLabels` + `EnsureReferencesAsync`.
3. Overwrite Label/LabelArabic/Kind/X/Y/HallId/BoothId/**IsActive**;
   `UpdatedAt = now`; saved.
4. Audit `VenueMapNode.Updated` (detail `id=…; label=…; active=…`).

### Delete (`DeactivateAsync`) — soft + idempotent
1. Load by id or `404`.
2. If already `!IsActive` → **return early** (no write, no audit) — idempotent.
3. Else `Deactivate()` (`IsActive = false`), `UpdatedAt = now`; saved; audit
   `VenueMapNode.Deactivated` (detail `id=…; label=…`).

There is **no hard delete** and no cascade — a deactivated node simply stops
appearing in the public read.

## Public projection (`ListPublicAsync`)

```
db.VenueMapNodes
  .Where(n => n.IsActive)
  .OrderBy(n => n.Label)
  .Select(n => new PublicVenueMapNode(Id, Label, LabelArabic, Kind, X, Y, HallId, BoothId))
```

- **Active-only**, ordered by `Label`.
- Drops `CreatedAt` / `UpdatedAt` — `PublicVenueMapNode` is the smaller
  append-only wire shape the shipped app decodes (D-219 wire-contract preservation).
- `AllowAnonymous()` — guest-and-above; no token, no permission code.

This is the single contract the app's venue-map screen (App Page 015 §E1) renders:
each node at `(x, y)`; a `kind=Booth` node's `boothId` matches
`PublicBoothSummary.Id` (App Page 015 §E2/§E3) for the booth deep-link. The app
performs its own coordinate normalisation over the design-space `(x, y)` values —
the server stores and returns them raw (no normalisation, no viewport query, no
paging).

## CP-side state (`VenueMapList`)

- `_query : GridQuery` (default `Top = 20`), `_page`, `_loading`, `_toast`.
- `_presentation : CrudPresentation` (Dialog default; read from
  `Prefs.GetPresentationAsync("venue-map")` at init, persisted to
  `localStorage["simf.cp.prefs.venue-map"]` — pure browser storage, no schema, no
  server state, respects the D-110 freeze).
- `_form : FormKind { None, AddEdit, ViewDelete }` + `_isEdit` / `_isDelete` /
  `_target : AdminVenueMapNodeDetail?` drive which form `CrudShell` hosts.
- `FormOpen = _form != None`; `GridHidden = FormOpen && _presentation == Page`.
- Edit / Details / Delete always `LoadDetailAsync` the **full** detail first
  (the summary omits the links + timestamps, so editing from it would be lossy);
  a failed detail load returns null + an error toast and does not open the form.

## Audit & ownership

Every write goes through `IAuditLog.WriteAsync` with the actor's `sub`-claim id —
`VenueMapNode.Created` / `.Updated` / `.Deactivated`. The audit is the standard
App-entity stamping (no cross-DB write; Identity stays separate — D-157). There is
no per-tenant scoping (single-tenant system).

## Seeding / lifecycle

The table **ships empty** — there is no seeder; the Logistics team populates it
through the CP (or Excel import). So the empty state is the default first render
and the public `GET /app/venue-map` returns `[]` until nodes are added.

## Edge cases

- **Stale link on save** — a Hall/Booth deactivated after the picker loaded fails
  `EnsureReferencesAsync` → `400 VENUE_MAP_NODE_INVALID`. On **export** the same
  unresolved id renders an empty Hall/Booth cell rather than a dangling guid.
- **Idempotent delete** — second delete on an inactive node returns 200 without a
  new audit row.
- **Mixed Kind/link** — the model allows e.g. a `PointOfInterest` with a `HallId`
  or a `Booth` Kind with no `BoothId`; the service does not couple Kind to the
  link. Coherence is an editorial responsibility.
- **Import insert-only** — every imported row is a new node; there is no dedup
  against existing labels and no way to update/deactivate via import.

## Related decisions
- **D-230** — venue-map node CRUD + `VenueMapNodeKind` + `App/D230` migration (FR-605).
- **D-157** — Data ↔ Identity separation / bare-Guid links resolved on read.
- **D-219** — shipped mobile wire-contract preserved (the public node shape is append-only).
- **D-255** — `SimfDataGrid` list-page standard.
- **D-353** — `CrudShell` form split + presentation toggle + `SimfConfirm` delete.
- **D-356** — generic-grid Excel export + import (5000-row caps).
