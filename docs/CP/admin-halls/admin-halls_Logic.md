# Halls — Logic (`/admin/halls`)

State + data model behind the Halls page. Verified against `Hall.cs`,
`HallConfiguration.cs`, `AdminHallService.cs`, `Halls.cs` (contracts),
`ProgrammeSessionService.cs` and `VenueMapNode(.Configuration).cs` this session.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> Companion docs: [Design](admin-halls_Design.md) · [API](admin-halls_API.md) ·
> [Function](admin-halls_Function.md).

## L-1 The entity
`Hall` (`SIMF.Domain.Programme`, `: BaseAuditEntity`) on `SimfAppDbContext`
(`dbo.Halls`, migration `AddHalls`, 2026-05-28). Fields:

| Field | Type | Rule (server) | EF |
|-------|------|---------------|----|
| `Code` | string | 2–16 chars, trimmed + **uppercased**, **unique** | `HasMaxLength(16)`, `IsRequired`, unique index |
| `Name` | string | 1–128 chars | `HasMaxLength(128)`, `IsRequired` |
| `NameArabic` | string | 1–128 chars | `HasMaxLength(128)`, `IsRequired` |
| `Capacity` | int | ≥ 0 | — |
| `Floor` | string? | ≤ 32, null when blank | `HasMaxLength(32)` |
| `EquipmentNotes` | string? | ≤ 1024, null when blank | `HasMaxLength(1024)` |
| `Purpose` | `HallPurpose` | default `General` (0) — D-248 | enum int |
| `GeofenceCenterLat` | double? | −90..90; all-three-or-none — D-240 | — |
| `GeofenceCenterLon` | double? | −180..180 | — |
| `GeofenceRadiusMeters` | double? | > 0 and ≤ 100000 | — |
| `IsActive` (base) | bool | soft-delete flag | composite index `(IsActive, Name)` |
| `CreatedAt` / `UpdatedAt` / `CreatedBy` / `UpdatedBy` (base) | audit | stamped on write | — |

The UI `MaxLength` on every text field (`HallsAddEdit`) matches the EF
`HasMaxLength` which matches the service length-guard — the SES-001 three-way
alignment rule holds (16 / 128 / 128 / 32 / 1024).

## L-2 Code uniqueness (case-insensitive)
`Code` is normalised `.Trim().ToUpperInvariant()` in `Validate` before any
persistence, and the DB has a **unique index** on `Code`. Create checks
`AnyAsync(h => h.Code == code)`; Update re-checks only when the code changed
(`!string.Equals(old, new, OrdinalIgnoreCase)`), excluding the row's own id. A
clash → `ApiException(HALL_CODE_DUPLICATE, 409)` (bilingual). The DB index is the
hard backstop behind the application check.

## L-3 Soft-delete (never hard-delete)
Deactivate sets `IsActive = false` + `UpdatedAt = now` and writes
`Hall.Deactivated`; it is **idempotent** (an already-inactive hall returns with
no second write). Rows are never removed — the grid keeps showing a deactivated
hall with a grey **Inactive** pill, and Edit can re-activate it (`IsActive =
true`). There is no list-level "hide inactive" default; the grid shows all rows
unless the `isActive` filter is set.

## L-4 List query (server, `ListAllAsync`)
`AsNoTracking` over `dbContext.Halls`:
- **Search** → `EF.Functions.Like` `%term%` on `Code`, `Name`, `NameArabic`.
- **`isActive` filter** → parsed bool equality.
- **Per-column filters** (D-255): `code` / `name` / `namearabic` /
  `floor` substring `Contains` (floor guards null). Unknown columns ignored.
- **Sort**: `code` (default), `name`, `namearabic`, `capacity` (asc/desc) — any
  other key falls back to `OrderBy(Code)`.
- **Page**: `Skip = max(0, Skip)`, `Top = clamp(Top>0 ? Top : 25, 1, 200)`.
- Projects to `AdminHallSummary` (adds `(int)Purpose`); returns `GridPage.Of`.

The summary deliberately omits `EquipmentNotes` and the geofence triple — those
load only via `GetAsync` (`AdminHallDetail`) before Edit / Details / Deactivate.

## L-5 Audit + actor
Every mutation reads the actor from the `sub` JWT claim (401 if unparseable) and
writes an `AuditEntry` via `IAuditLog`: `Hall.Created`, `Hall.Updated`,
`Hall.Deactivated`, each `Outcome = Success`, `ActorUserId = actor`, with a
detail string (`id=…; code=…; …`). `BaseAuditEntity` also stamps
`CreatedBy`/`UpdatedBy` through the central save-changes interceptor. The audit
captures the actor id only (no cross-DB name snapshot needed here).

## L-6 Geofence rule (D-240)
The three geofence values are validated together in `ValidateGeofence`:
- all three null → no geofence (the hall records arrivals by QR door-scan only);
- any one null while others set → `HALL_GEOFENCE_INVALID` (partial is rejected);
- when set: lat ∈ [−90,90], lon ∈ [−180,180], radius > 0 and ≤ 100000 m (a
  venue-scale cap), else `HALL_GEOFENCE_INVALID`.

The CP form mirrors the same rule client-side (`TryParseGeofence`,
invariant-culture parse) before sending. The geofence feeds the mobile
arrival/attendance chain (`HallAttendance*`), not this CP page's render — it is a
write-only configuration field here.

## L-7 `HALL_IN_USE` is reserved, not enforced
`ErrorCodes.HallInUse` exists but no code path throws it: Deactivate is currently
**unconditional**. The intended future guard (refuse to deactivate a hall an
active Session uses) is not wired — recorded here so the doc matches the code, not
the aspiration. A `VenueMapNode` pointing at the hall is a separate matter (L-9):
the FK is **Restrict**, so a hall referenced by a map node cannot be *hard*-deleted
at the DB level, but soft-delete (the only delete this page does) is unaffected.

## L-8 How the hall reaches the app session (Page 016)
`Session.HallId` is a **real FK** to `Hall` (`Session.Hall` nav). The public
read `ProgrammeSessionService` `.Include(row => row.Hall)` and projects onto
every `PublicSessionListItem`:
- `hallId = session.HallId`
- `hallName = session.Hall!.Name`
- `hallNameArabic = session.Hall!.NameArabic`

so the app agenda (Page 016) shows the hall name in the active locale without a
second call. The hall's `Capacity` also drives the session booking cap
(`CapacityOverride ?? Hall.Capacity ?? 0`). This is an **intra-`SIMF_App`** FK
(both `Session` and `Hall` live in `SimfAppDbContext`) — it does **not** cross
the Data↔Identity boundary (D-157), so a normal EF relation is correct here.

## L-9 How the hall reaches the venue map (Page 015)
`VenueMapNode` (D-230) carries an **optional real FK** `HallId` (`Hall?` nav)
with `OnDelete(DeleteBehavior.Restrict)`; a node with `Kind = Hall` (enum value
0) marks that hall on the 2D map. The relation is **one-directional**: the map
node points at the hall; `Hall` has no back-collection. Halls and map nodes are
curated on **separate** CP pages (`/admin/halls` vs `/admin/venue-map`) — this
page never edits a node. Restrict-delete means a hall in use by a node can't be
hard-deleted, which is consistent with this page's soft-delete-only model.

## L-10 Localisation
Bilingual data is paired (`Name`/`NameArabic`); the Arabic name is a required
field. The app and CP render the active-locale name (the public projection ships
both). All CP UI strings come from `Admin.Halls.*` resx (EN + AR); all server
messages are bilingual `ApiException` pairs surfaced via
`MessageForCurrentCulture()`.

## L-11 Seeding
No dedicated hall seeder was found this session — `dbo.Halls` ships empty and the
event/venue team enters the real rooms through this CP page (consistent with the
"ship empty, team seeds" posture used for the geofence coordinates, G-OI-2).

## Drift / code concern (report only — no code changed)
The **legacy** reference doc
[`docs/pages/cp/admin-halls.md`](../../pages/cp/admin-halls.md) is **stale vs
code** in two ways, reconciled here from the source:
1. It lists the backend list route as `POST /account/api/admin/halls/list` and
   the others as bare `GET/POST/PUT/DELETE /admin/halls...`. The **API** routes
   are uniformly under `/api/v1/admin/halls*`; `/account/api/admin/halls/*` is
   the **CP BFF proxy**, not the API route. (This set documents both hops.)
2. Its "Form fields" table and §6 omit the **D-240 geofence triple** and the
   **D-248 `Purpose`** column, both of which are present in the live
   contracts/entity and the Add/Edit form. (The E2E catalogue
   [`cp-admin-halls.md`](../../tests/e2e/cp-admin-halls.md) already covers the
   geofence — E2E-HAL-004/008.)

Neither is a code defect — they are documentation drift in the older per-page
reference. No code was changed; flagged for a future reconcile of the legacy doc.
