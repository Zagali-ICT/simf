# Organisations — Logic (`/admin/organisations`)

The state/data model behind the page: the entity, soft-delete, audit stamping,
list filtering, uniqueness/ordering, how the lookup reaches the app, and seeding.
Verified against `Organisation.cs`, `AdminOrganisationService.cs`,
`PublicOrganisationService.cs`, `OrganisationSeeder.cs` this session.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> Companion docs: [Design](admin-organisations_Design.md) ·
> [API](admin-organisations_API.md) · [Function](admin-organisations_Function.md).

## Entity — `Organisation : BaseAuditEntity`

Lives in `SIMF.Domain.Organisations.Organisation`, persisted as `dbo.Organisations`
on **`SimfAppDbContext`** (D-220 additive migration, B3). It is a plain bilingual
reference row — no navigation properties, no cross-context FK.

| Property | Type | Notes |
|----------|------|-------|
| `NameArabic` | `string` (required, default `""`) | **Primary** display name, 1–150 chars; the default sort key and the picker's display |
| `Name` | `string?` | English name, ≤ 150; optional |
| `CommercialRegistration` | `string?` | سجل تجاري, ≤ 700 stored; optional, **unique when present** |
| `Sector` | `string?` | ≤ 128; optional |
| `City` | `string?` | ≤ 128; optional |
| `Phone` | `string?` | ≤ 32; optional |
| `Email` | `string?` | ≤ 320; optional |
| `Website` | `string?` | ≤ 512; optional |
| (inherited) `Id` | `Guid` | PK |
| (inherited) `IsActive` | `bool` | soft-delete flag (from `BaseEntity`/`BaseAuditEntity`) |
| (inherited) `CreatedAt` / `UpdatedAt` | `DateTimeOffset` / `?` | audit timestamps |
| (inherited) `CreatedBy` / `UpdatedBy` | actor stamps | set by the central audit-stamping interceptor |

> The field length caps are enforced in the **service** (`ValidateAndNormalise`
> + the import `Clamp`), not via EF attributes on the entity. They are now named
> constants on `AdminOrganisationService` carrying the widths declared in
> `OrganisationConfiguration`: **150 / 150 / 700 / 128 / 128 / 32 / 320 / 512**.
> The two name caps read 256 until 2026-08-18 while the column held 150, so a
> 151-to-256-character name passed the 400 validator and then failed
> `SaveChangesAsync` as an unhandled `SqlException` — a 500 on legitimate input,
> and mid-import an abort after earlier batches had already committed. The
> commercial registration reads 700 to match the widened column (the CP form
> still caps its input at 32, which is stricter than the server, not looser).

## Soft-delete (`IsActive`)

- Deactivate calls `org.Deactivate()` → `IsActive = false`, stamps `UpdatedAt`,
  writes an `organisation.deactivated` audit entry. The row is **never hard
  deleted**.
- **Idempotent:** if the row is already inactive, `DeactivateAsync` returns early
  with no second `SaveChanges` and no second audit write.
- There is **no reactivate endpoint**; a deactivated row can be brought back only
  by editing it with `IsActive = true` (the Edit form's Active checkbox sets
  `UpdateOrganisationRequest.IsActive`).

## List filtering, ordering, paging (`AdminOrganisationService.ListAsync`)

- Base query: `db.Organisations.AsNoTracking()` — **the admin grid shows all
  rows, active and inactive** (the Active column / pill distinguishes them).
  (Contrast the public picker below, which filters `.Where(o => o.IsActive)`.)
- **Search** (`GridQuery.Search`): a single term LIKE across `NameArabic`,
  `Name`, `CommercialRegistration`, `City`.
- **Per-column filters** (`GridQuery.Filters`, `Contains`): keys `name`,
  `nameen`, `commercialregistration`, `sector`, `city`, `isactive`
  (boolean-parsed); unknown columns ignored; filters accumulate.
- **Sort** (`GridQuery.Sort` + `SortDescending`): `name` (→ `NameArabic`), `city`,
  `isactive`; **default** = `OrderBy(NameArabic)` ascending.
- **Paging**: `Skip = max(0, Skip)`, `Top = clamp(Top>0 ? Top : 25, 1, 200)`.
  Projects to `AdminOrganisationSummary` (no contact columns) and returns
  `GridPage<AdminOrganisationSummary>.Of(page, total, …)`.

## Uniqueness rule (commercial registration)

- `CommercialRegistration` is **unique when present** (it may be null).
- **Create**: if a CR is supplied and any existing row already has it → 409
  `ORGANISATION_INVALID` (`DuplicateCommercialRegistration`).
- **Update**: the clash check runs only when the CR **changes**
  (case-insensitive compare of old vs new), excluding the row itself.
- This is what makes the gov-Excel re-import an **upsert** rather than a
  duplicate-insert.

## Audit stamping

Every write logs through `IAuditLog.WriteAsync` with `AuditOutcome.Success`, the
actor's user id (from the `sub` claim), and a `Detail` string:

| Operation | `AuditEvents` event | Detail |
|-----------|---------------------|--------|
| Create | `OrganisationCreated` (`organisation.created`) | `id=…; nameAr=…; cr=…` |
| Update | `OrganisationUpdated` (`organisation.updated`) | `id=…; nameAr=…; active=…` |
| Deactivate | `OrganisationDeactivated` (`organisation.deactivated`) | `id=…; nameAr=…` |
| Import | `OrganisationImported` (`organisation.imported`) | `read=…; inserted=…; updated=…; skipped=…` |

The actor display-name/email snapshotting (the audit-trail self-containment) is
handled by the shared audit layer; the org row itself stores no actor copy beyond
the inherited `CreatedBy`/`UpdatedBy` stamps.

## Import upsert logic (`ImportAsync`)

- Parses the workbook via `IOrganisationExcelReader.Read(stream)` →
  `OrganisationImportRow[]`. A parse failure → 400 `ORGANISATION_IMPORT_FAILED`.
- Per row: `NullIfBlank(NameAr)`; a missing Arabic name → **skipped** + error
  `"Row {n}: Arabic name is required."`. All fields are `Clamp`-ed to the column
  lengths (150 / 700 / 150 / 128 / 128 / 32 / 320 / 512) rather than rejected.
- **Match key:** by `CommercialRegistration` when present; otherwise by the exact
  **active** Arabic name. Match → update; no match → insert (new
  `IsActive = true`).
- **Lookup is pre-loaded, not per row.** The whole sheet is normalised first,
  then two chunked `IN (...)` queries (≤ 500 keys each) load the candidate rows
  into two case-insensitive maps — one keyed on commercial registration, one on
  the active Arabic name. Before 2026-08-18 this was one `SingleOrDefaultAsync`
  per spreadsheet row (N+1), and on the name path `Single` threw permanently
  once two organisations shared a name; the name map now takes the oldest match
  (`CreatedAt`, then `Id`) instead.
- **A row inserted by the sheet is registered in both maps**, so a key repeated
  within one workbook updates the pending row. An unsaved insert is invisible to
  a query, so two rows sharing a commercial registration previously both
  inserted and hit the filtered unique index on `SaveChanges`.
- **Update fills, it does not clear.** Every optional column coalesces
  (`existing.X = value ?? existing.X`): a blank cell in a bulk sheet means "not
  supplied". Before 2026-08-18 the update branch assigned unconditionally, so a
  partial-update sheet carrying only the Arabic name erased the English name,
  the commercial registration and the contact columns while reporting
  `updated=1` — and the next sheet carrying the real CR then matched nothing and
  inserted a duplicate the filtered unique index could not catch. Clearing a
  field deliberately is what the explicit Edit form is for.
- Flushes every `ImportBatchSize = 500` rows; error list capped at
  `ImportErrorCap = 50`. Returns `OrganisationImportResult(rowsRead, inserted,
  updated, skipped, errors)`.

## How the lookup reaches the app (resolve-on-read, cross-DB safe)

- The app reads the **same `dbo.Organisations` table** through
  `PublicOrganisationService.SearchAsync` (`GET /app/organisations`):
  `AsNoTracking().Where(o => o.IsActive)`, LIKE over `NameArabic` / `Name` /
  `City`, ordered by `NameArabic`, `top` clamped 1–50 (default 20). It projects
  to `OrganisationPickerItem(Id, NameAr, NameEn, City)`.
- Only **active** rows reach the picker → a CP Deactivate immediately drops the
  row from the app الجهة list (no extra step).
- The visitor's chosen organisation is stored on the profile as a **bare
  `Guid` `organisationId`** (D-221), **not** an EF FK — consistent with the
  D-157 Data↔Identity separation rule. The profile (App) and the organisations
  lookup (App) both live on `SimfAppDbContext`; the picker resolves the display
  name on read, the profile keeps only the id. No cross-DB relation is involved
  here (both are App-side), and no Identity data is duplicated.

## Seeding

- **Dev only:** `OrganisationSeeder.SeedFakeAsync` inserts a small realistic
  sample of 12 Saudi maritime / defence / energy bodies (Royal Saudi Naval
  Forces, SAMI, Saudi Aramco, Mawani, Bahri, …), idempotent on the
  commercial-registration number (re-running inserts only the missing rows).
- The seeder's own XML doc states it is **NOT run in production** — in production
  the lookup is populated by the **real government Excel import** (the CP "Import
  Excel" affordance, B3 / D-220).
- I did **not** find a D-377 "baseline organisations" production-seed in the code
  read this session; the production population path is the gov-Excel import, not a
  code seed. (The task brief raised D-377 as a possibility — recorded here as
  *not verified in code*; do not assert a D-377 org baseline.)

## Invariants (summary)

1. Arabic name required (1–150, the stored width) and is the primary display +
   default sort key.
2. Commercial registration unique when present; the upsert key for re-import.
3. Soft-delete only (`IsActive`); deactivate is idempotent; no hard delete.
4. Admin grid shows all rows; the **public picker shows active only**.
5. Org↔profile link is a bare Guid (`organisationId`, D-221), resolve-on-read.
6. Every mutation is audited; field caps enforced in the service from one set of
   constants, mirrored by the import `Clamp` and never looser than the stored
   column.
7. A bulk import fills columns; only the explicit Edit form clears them.
