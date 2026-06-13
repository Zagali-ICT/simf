# CP — Interests — Logic (`/admin/interests`)

Business rules behind the Interests lookup. Verified against `UserInterest.cs`,
`InterestConfiguration.cs`, `InterestService.cs`, `InterestRepository.cs`, the
validators, and the app consuming endpoint.

Last updated: 2026-06-13 — CP config-page documentation (D-380)

## L-1 The entity + storage
`UserInterest : BaseAuditEntity` (table **`dbo.Interests`**, `SimfAppDbContext` — D-167):

| Field | Type | Rule |
|-------|------|------|
| `Id` | `Guid` | PK; `Guid.NewGuid()` on create |
| `Name` | `string(128)`, required | English label; **unique** (DB index) |
| `NameArabic` | `string(128)`, required | Arabic label |
| `DisplayOrder` | `int` | sort key in the picker (`≥ 0`); tie-broken by `Name` |
| `IsActive` | `bool` (from `BaseEntity`) | soft-delete flag; `true` on create |
| `CreatedAt` / `UpdatedAt` | audit stamps | from `BaseAuditEntity` |

EF config: 128-char caps on both names (line up with the validators), a **unique
index on `Name`**, and a composite index on **`(IsActive, DisplayOrder)`** matching
the visitor-picker read shape.

## L-2 Validation (server is canonical; client mirrors)
`AdminCreateInterestRequestValidator` / `AdminUpdateInterestRequestValidator`
(FluentValidation, bilingual messages):
- `Name` — **NotEmpty**, **MaximumLength(128)**.
- `NameArabic` — **NotEmpty**, **MaximumLength(128)**.
- `DisplayOrder` — **GreaterThanOrEqualTo(0)**.

The Add/Edit form (`InterestAddEdit.HandleSubmitAsync`) guards the same three rules
client-side for fast UX (1–128 chars each; `DisplayOrder` parses to a non-negative
`int`), then trims `Name`/`NameArabic` before sending. The `MaxLength="128"` on the
two text fields caps input; the EF `HasMaxLength(128)` is the storage backstop — the
three layers are aligned (validation alignment rule).

> Implementation note: the DisplayOrder field uses `Value`/`ValueChanged` +
> `ValueExpression` (not `@bind-Value`) because it parses to `int` only at submit time
> — the in-flight string lives in `_displayOrderInput`. (The D-132 mid-flight fix.)

## L-3 Uniqueness / duplicate handling
`Name` is unique. The service checks `NameExistsAsync` before insert and (on rename
only) before update, throwing **409 `INTEREST_NAME_DUPLICATE`** when it collides; the
DB unique index is the final backstop. `NameArabic` is **not** unique — two interests
may share an Arabic name. Uniqueness is **ordinal/exact** on the stored (trimmed)
value; there is no case-folding, so "Naval" and "naval" are distinct rows.

## L-4 Create / update / deactivate semantics
- **Create** — new `Guid` id, trimmed names, `IsActive = true`, `CreatedAt = now`;
  audit `InterestCreated`.
- **Update** — find-or-404, collision check on rename, overwrite the four fields
  (`Name`, `NameArabic`, `DisplayOrder`, `IsActive`), `UpdatedAt = now`; audit
  `InterestUpdated`. `IsActive` here is the **re-activate** lever.
- **Deactivate** — find-or-404, set `IsActive = false`, `UpdatedAt = now`; audit
  `InterestDeactivated`. **Idempotent** — already-inactive returns early with no
  write. **Soft only**: the row is never hard-deleted, so existing visitor links
  (`UserProfileInterests`) survive.

## L-5 Ordering + paging (the grid read)
`ListAllAsync` clamps `Top` to `[1,200]` (default 25), `Skip` to `≥0`, then
`InterestRepository.ListPageAsync`:
- **Search** — substring (`LIKE %term%`) over `Name` **and** `NameArabic`.
- **Column filters** — `name`, `nameArabic` (substring), `isActive` (bool).
- **Sort** — `name` / `nameArabic` / `displayOrder` / `createdAt` (asc/desc);
  **default** order is `DisplayOrder` then `Name` (the picker's natural order).
- Returns `GridPage<AdminInterestSummary>` (items + total).

## L-6 The app-picker consumption contract (why this page matters)
The app interests step (Page 007‑01) reads `GET /app/account/interests`
(`InterestsListEndpoint` → `InterestRepository.ListActiveAsync`):
- **Active only** — `Where(IsActive)`. A deactivated interest disappears from the
  picker; existing visitor links are untouched.
- **Order** — `OrderBy(DisplayOrder).ThenBy(Name)` — the admin's display order, then
  English name as a stable tiebreaker.
- **Shape** — `InterestDto(Id, Name, NameArabic, DisplayOrder)` (no `IsActive` /
  `CreatedAt` on the app wire — the list is pre-filtered to active).
- The visitor picks **1–10**; the selected `Id`s ride the single profile upsert
  (`POST /app/account/user-profile`) and become `UserProfileInterests` join rows.

So the CP grid and the app picker read the **same `Interests` table**: the CP writes
it (all rows, audited), the app reads the **active** slice. There is no duplicated
copy and no cross-database relation (D-157) — the visitor↔interest link is an
App-side EF join.

## L-7 Edge cases + known limitations
- **Empty list** → `SimfEmptyState` ("No interests yet." / Arabic). Toolbar Add stays.
- **Duplicate Name** → 409 `INTEREST_NAME_DUPLICATE`, alert surfaces the bilingual
  server message; the form stays open.
- **Deactivating an in-use interest** → allowed; visitors who linked it keep the link;
  the picker stops offering it to new visitors; editing → tick Active restores it.
- **DisplayOrder collision** → allowed; equal-order rows fall back to `Name` order;
  an admin can re-bump one to disambiguate.
- **Missing id** on Get/Update/Deactivate → 404 `INTEREST_NOT_FOUND`.
- **Import** → insert-only; blank/duplicate name = a per-row error (not a batch
  abort); bad/wrong-sheet upload = 400, nothing created.
- **Concurrent edits** → no `RowVersion` on `Interest` (no optimistic concurrency
  configured) → last-write-wins. Acceptable for a 5–30-row lookup; adding a
  `RowVersion` would touch the D-110 schema freeze and needs owner approval.

## L-8 Localization / RTL
All visible strings come from `Strings.resx` (EN) + `Strings.ar.resx` (AR) via
`IStringLocalizer<Strings>`. Bilingual data is paired (`Name`/`NameArabic`). The CP
header `العربية` / `English` toggle round-trips the page with `culture=ar|en`; in
Arabic the layout sets `dir="rtl"` and the grid/toolbar/forms mirror. The stored
interest names are author-supplied (not localized resx) — the picker shows the
locale's side of the `Name`/`NameArabic` pair.
