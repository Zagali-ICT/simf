# Exhibition booths — Logic (`/admin/booths`)

State, data model and business rules behind the page. Grounded in `Booth.cs`,
`AdminBoothService.cs`, `BoothContracts.cs`. The screen is in
[admin-booths_Design.md](admin-booths_Design.md); the contract in
[admin-booths_API.md](admin-booths_API.md).

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## L-1 — The `Booth` entity

`Booth : BaseAuditEntity` on `SimfAppDbContext` (table `dbo.Booths`, D-199
additive migration; D-222 added the Exhibitor FK + officer fields). Fields:

| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `Code` | string | Short code (e.g. `A-12`); 2–16; unique across active **and** inactive rows; stored upper-cased |
| `Name` / `NameArabic` | string | Bilingual, 1–128 each |
| `ExhibitorId` | Guid? | **D-222** real FK → `Exhibitor.Id` (same App DB); source of truth for the exhibitor |
| `OfficerName` / `OfficerPhone` / `OfficerEmail` | string? | **D-222** booth-officer contact (≤256 / ≤32 / ≤320) |
| `ContactId` | Guid? | **SIMF-FDS-014 / D-281** optional link to a shared `Contact` (the officer as a person) |
| `ExhibitorName` / `ExhibitorNameArabic` | string? | **Legacy free-text fallback** (pre-D-222 + public wire contract D-219); **not settable from the admin write surface any more** (see L-6) |
| `Sector` / `SectorArabic` | string? | Bilingual sector tag (≤128) |
| `Description` / `DescriptionArabic` | string? | Bilingual paragraph (≤2048) |
| `HallId` | Guid? | Optional real FK → `Hall.Id` (same App DB) |
| `MapX` / `MapY` | double? | Normalised 2D venue-map position; optional until placed |
| `IsActive` | bool | Soft-delete flag (from `BaseAuditEntity`) |
| `CreatedAt` / `UpdatedAt` / audit stamps | — | From `BaseAuditEntity` |

## L-2 — Cross-DB / logical-FK discipline (D-157)

`ExhibitorId`, `HallId` are **real FKs within `SimfAppDbContext`** (Exhibitor,
Hall, Booth all live in `SIMF_App`). `ContactId` is also same-context. None of
these cross the App↔Identity boundary, so no cross-database constraint is
involved. The **actor** on each write is the JWT `sub` Guid (an Identity user),
captured only in the audit `Detail` string — a logical reference, never a FK.

## L-3 — Soft-delete (`IsActive`)

`DELETE` → `booth.Deactivate()` sets `IsActive=false` and is **idempotent** (an
already-inactive booth returns success with no second audit row). There is **no
hard delete**. Deactivation also happens implicitly when an admin unticks the
**Active** checkbox on Edit and saves (`PUT` with `IsActive=false`). An inactive
booth still appears in the admin grid (with an "off" pill) but is filtered out of
the three public reads (L-6).

## L-4 — Validation + normalisation (`ValidateAndNormalise` + FK guards)

- **Code** — `Trim().ToUpperInvariant()`, length 2–16, else `400 BOOTH_INVALID`.
  Uniqueness is case-insensitive; on create any clash → `409
  BOOTH_CODE_DUPLICATE`; on update the clash check runs **only when the Code
  actually changed**.
- **Name / NameArabic** — trimmed, 1–128 each, else `400`.
- **Optional text** (`OptionalText`) — blank → `null`; over max → `400`. Max
  lengths mirror `BoothConfiguration.HasMaxLength`: Officer name 256 / phone 32 /
  email 320; Sector* 128; Description* 2048.
- **Officer email** — when present must contain `@`, else `400`.
- **FK guards** — `EnsureHallIsValidAsync` / `EnsureExhibitorIsValidAsync` /
  `EnsureContactIsValidAsync`: a supplied id must be an **existing active** row in
  `Halls` / `Exhibitors` / `Contacts`; an inactive or unknown id → `400
  BOOTH_INVALID` (so a booth never points at a soft-deleted parent). `null` is
  allowed (each is optional).
- **Server is the source of truth.** The CP client guard only short-circuits a
  blank Code / Name / NameArabic; everything else is enforced server-side.

## L-5 — List query (`ListAllAsync`)

`AsNoTracking`. `Top` clamped 1–200 (default 25; CP sends 20), `Skip ≥ 0`.
`Search` → `LIKE` over Code / Name / NameArabic. Per-column `Filters` honoured
for `code` / `name` / `namearabic` / `sector` (`Contains`); unknown keys ignored.
Sort keys `code` (default) / `name` / `sector` / `isactive`. The projection
returns `AdminBoothSummary` (Id, Code, Name, NameArabic, ExhibitorId, Sector,
HallId, IsActive) — **the summary deliberately omits** the officer fields, the
bilingual sector-Arabic/description, the Contact link and the map position, which
is why the CP re-fetches the full `AdminBoothDetail` before every Edit / View /
Delete form (L-7).

## L-6 — How the data reaches the app (resolve-on-read)

The CP curates `dbo.Booths`; the app reads three **public** projections
(`AllowAnonymous`), all filtered to `IsActive=true`:

- `GET /app/booths` → `PublicBoothSummary` — Code, Name/NameArabic,
  **ExhibitorName/ExhibitorNameArabic** (the resolved company name; the public
  projection fills these from the linked `Exhibitor` when `ExhibitorId` is set,
  else the legacy free-text fallback), Sector/SectorArabic, `HallId` (bare Guid,
  **no hall name** — D11), MapX/MapY.
- `GET /app/booths/{id}` → `PublicBoothDetail` — the summary fields **plus**
  Description/DescriptionArabic (the lazy paragraph the detail sheet shows).
- `GET /app/venue-map` → positioned nodes; booth nodes carry `boothId` matching
  `PublicBoothSummary.Id`, letting the app join a node to its booth card.

**Not exposed to the app:** the booth **officer** fields, the `ContactId` link
and the `IsActive` flag are CP-internal. There is **no `LogoUrl`** field, so a
booth logo in the app is **decoration only** (D11). The admin summary ships the
bare `ExhibitorId`; the public summary ships the resolved `ExhibitorName` — the
resolution happens server-side in the public projection, not on the wire.

## L-7 — Why Edit/View/Delete re-fetch the detail

The grid binds the summary only. Editing from a summary-only model would wipe the
officer fields, the Arabic sector/description, the Contact link and the map
position on save. So `OnEditAsync` / `OnDetailsAsync` / `OnDeleteAsync` each call
`GET /account/api/admin/booths/{id}` first and open the form against the full
`AdminBoothDetail`; a GET failure surfaces a toast and the form does not open.

## L-8 — Client-side FK name resolution

The summary carries only `ExhibitorId` / `HallId`. `BoothsList` loads two cached
lookups at mount (`exhibitors/list`, `halls/list`, `Top=500`, active only) and
resolves a display name via `ExhibitorName(id)` / `HallName(id)` (unknown → `—`).
This is why the Exhibitor + Hall grid columns are neither server-sortable nor
server-filterable. `BoothsViewDelete` loads its own copies (un-filtered) for the
read-only name display.

## L-9 — Audit + stamping

`AdminBoothService` writes `Booth.Created` / `Booth.Updated` /
`Booth.Deactivated` via `IAuditLog`, each with the actor user id and a `Detail`
string (id / code / name / active). `CreatedAt` is set on create; `UpdatedAt` on
every update + on deactivate (`timeProvider.GetUtcNow()`).

## L-10 — Excel round-trip (D-356)

- **Export** writes the Exhibitor as its **English name** and the Hall as its
  **Code** (human-readable natural keys), so the workbook can be re-imported.
- **Import** is **insert-only** (Created is the only success kind): it resolves
  `Exhibitor` by English name and `Hall` by Code (active, case-insensitive); a
  non-blank value that resolves to nothing, a short/duplicate Code, etc. is a
  **per-row** error that never aborts the batch. Officer fields, `ContactId` and
  Map X/Y **cannot** be expressed safely as plain text and are **always left
  unset** by import — an admin sets them later via Edit.

## L-11 — Edge cases / known limitations

- **Map X / Map Y are free doubles** consumed by the public 2D venue map; the
  server does **not** range-check them.
- **Action buttons are not `<AuthorizedAction>`-gated** — an admin with
  `Booths.View` but not Create/Edit/Delete/Export/Import sees the buttons; the
  API returns `403` (the per-action enforcement point).
- **Legacy `ExhibitorName`/`ExhibitorNameArabic` columns** still exist on the
  entity for the public wire contract / pre-D-222 rows but are **not writable**
  from the admin surface (the admin write path sets `ExhibitorId` only).
- A booth can be created **before** its exhibitor, hall or map placement exists
  (all four are optional).

## L-12 — Frozen surface

`dbo.Booths` is an additive table under the D-199 / D-219 / D-222 freeze-lifts;
the schema is otherwise frozen (D-110). The **shipped public wire contract**
(the JSON field names the app decodes — `code`, `name`, `nameArabic`,
`exhibitorName`, `sector`, `hallId`, `mapX`, `mapY`, `description`) must be
preserved append-only. Adding a public field (e.g. a booth `logoUrl` or a hall
**name**) is a new DTO field requiring **owner approval**.
