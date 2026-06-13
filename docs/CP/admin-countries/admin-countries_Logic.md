# CP — Logic (البلدان · Countries `/admin/countries`)

Business rules, validation, edge cases and the app-consumption contract. Grounded
in `CountryEndpoints.cs`, `CountriesExcelEndpoints.cs`, `CountryAddEdit.razor`
(client guards), `Countries.cs`, and `ProfileCountriesEndpoint.cs`. Server-side
rules cited from `AdminCountryService.Validate` as documented in
`docs/pages/cp/admin-countries.md` (the service file was not re-read this session —
see the verification note at the end).

Last updated: 2026-06-13 — CP config-page documentation (D-380)

## What this page governs
The `dbo.Countries` table (`SimfAppDbContext`) — the single source of truth for
the country / nationality reference list. Each row:

| Field | Type | Rule |
|-------|------|------|
| `Id` | `int` | **ISO 3166-1 numeric**, the **primary key**, **manually assigned** (NOT IDENTITY). Immutable after create. |
| `Code` | `string` | ISO alpha-2, exactly 2 chars, stored upper-cased, **unique** |
| `Name` | `string` | English name, 1–128 chars |
| `NameArabic` | `string` | Arabic name, 1–128 chars |
| `PhonePrefix` | `string?` | optional dial code, ≤ 8 chars, blank → `null` |
| `DisplayOrder` | `int` | `≥ 0`; controls the app picker order |
| `IsActive` | `bool` | soft-delete flag; only active rows reach the app picker |

## Auth + permission gates
- **Page**: `@attribute [RequirePermission(PermissionCatalog.Countries.View)]` —
  an admin without `Countries.View` lands on `/not-permitted` and the list never
  fires. `Administrator = "*"` (wildcard) holds every code.
- **API**: each endpoint is policy-gated by its own code (View / Create / Edit /
  Delete / Export / Import) **and** `RequireApprovedAccount`. Create / Update /
  Deactivate also carry the `auth` rate-limit bucket.

## Primary-key model (the defining trait)
Unlike the sibling lookups (Themes, Halls — `Guid` auto-key), `Country`'s key is
the **manually assigned ISO 3166-1 numeric** `int`:
- **Add** — the admin types the id; client guard requires an integer `1–999`
  (`int.TryParse(_idInput) && countryId is > 0 and <= 999`); server requires `> 0`.
- **Edit** — the id field is **disabled** (`Disabled="@(_busy || IsEdit)"`) and the
  update contract (`AdminUpdateCountryRequest`) has **no `Id`** — the route id is
  authoritative. To "change" an id you must create a new row.

## Validation (client guards in `CountryAddEdit.HandleSubmitAsync`)
In order; the first failure sets `_error` (a resx string) and returns **before any
request fires**:
1. **Id** (Add only) — `int.TryParse` and `1–999`, else `Admin.Countries.Field.IdInvalid`.
2. **Code** — non-blank and `Trim().Length == 2`, else `…Field.CodeInvalid`.
   Submitted as `Trim().ToUpperInvariant()`.
3. **Name (English)** — non-blank and `≤ 128`, else `…Field.NameEnInvalid`. Submitted trimmed.
4. **Name (Arabic)** — non-blank and `≤ 128`, else `…Field.NameArInvalid`. Submitted trimmed.
5. **Display order** — `int.TryParse` and `≥ 0`, else `…Field.DisplayOrderInvalid`.
6. **Phone prefix** — capped to 8 by `MaxLength="8"`; blank → `null` (`NullIfBlank`).

Server validation (`AdminCountryService.Validate`, per the CP reference doc):
id `> 0`; code exactly 2 chars; names 1–128; phone prefix ≤ 8; display order `≥ 0`
— any breach → `400 COUNTRY_INVALID` (bilingual).

## Uniqueness / conflict rules
- **Duplicate id** (create) → `409 COUNTRY_ID_DUPLICATE` (message names the id).
- **Duplicate code** (create; or update changing the code to one another row holds)
  → `409 COUNTRY_CODE_DUPLICATE` (message names the code). Code is **case-insensitive**
  — stored upper-cased, so `sa` and `SA` collide.
- **Not found** (get / update / deactivate of a missing id) → `404 COUNTRY_NOT_FOUND`.

## Soft-delete + reactivation
- **Deactivate** = `DeactivateAsync` → `IsActive = false`, returns `ApiResult<bool>.Ok(true)`.
  A second deactivate of an already-inactive row is a no-op. It is **unconditional**:
  there is **no in-use guard** (`COUNTRY_IN_USE` is reserved but not enforced), so a
  country can be deactivated while profiles/speakers still reference it by bare id.
- **Reactivate** = Edit → tick the **Active** checkbox → PUT with `IsActive = true`.

## Excel import/export logic (D-356)
- **Export** always covers the **current filtered grid** (empty Ids + current
  `GridQuery`), never a per-row selection — because the country key is `int`, not the
  `Guid` the generic export contract carries (`IdOf` → `Guid.Empty`). Capped at
  `MaxExportRows`.
- **Import** is **insert-only**: each row → `AdminCreateCountryRequest` → `CreateAsync`.
  A blank/non-positive id or a blank Code / Name / NameArabic is a **per-row error**
  (does not abort the batch); a duplicate id / code surfaces as a per-row conflict,
  never an update. Upload defence (size cap, ZIP-magic `.xlsx`, required sheet
  `Countries`, required headers `Id|Code|Name|NameArabic`, row cap) is enforced by the
  shared base.

## Data ↔ Identity separation (D-157)
`Country` lives in `SimfAppDbContext` **alongside** its consumers
(`UserProfile.NationalityId`, `Speaker.CountryId`), so the D-157 cross-context rule
does **not** force a second-query resolve here — those are same-context joins by
bare id. There is no relation to any `SIMF_Identity` entity.

## Audit
Each successful mutation writes an audit entry through `IAuditLog`
(`AuditEvents.CountryCreated` / `CountryUpdated` / `CountryDeactivated`) with the
actor id and a `Detail` string (e.g. `id=116; code=KH; name=Cambodia`). (Per the CP
reference doc; `IAuditLog` wiring not re-read this session.)

## App consumption contract (Page 007 nationality picker)
The app's `GET /api/v1/app/account/user-profile/countries`
(`ProfileCountriesEndpoint`) reads the **same table** with this exact logic
(verified this session):

```csharp
appDb.Countries.AsNoTracking()
    .Where(c => c.IsActive)
    .OrderBy(c => c.DisplayOrder)
    .ThenBy(c => c.Name)
    .Select(c => new CountryDto(c.Code, c.Name, c.NameArabic))
```

Consequences of CP edits on the app:
- **Active filter** — deactivating a country drops it from the app picker; the app
  never sees inactive rows.
- **Ordering** — the picker is ordered by the CP **Display order**, then English name.
- **Default SA** — Page 007 defaults the selection to **Saudi Arabia (SA)**; the
  picked alpha-2 `Code` is what the app stores (`NationalityCode`) and uses to choose
  the document path (SA → national-ID; else Iqama / Passport). The CP-set numeric `Id`
  is the key those codes resolve against, but the app endpoint does **not** expose
  `Id` or `PhonePrefix`.

## Edge cases / known limitations
- **Id immutable after create** — change = new row.
- **Deactivate has no in-use guard** — a referenced country can still be deactivated.
- **Code case-insensitive** — `sa`/`SA` collide; display preserves the upper form.
- **Export ignores row selection** — always the filtered grid (int ids).
- **Import insert-only** — cannot update; duplicate id/code → per-row error.
- **Missing dial code** renders `—` in the grid and details list, and is `null` over
  the wire.

## Verification note (read-only session)
Verified directly from source this session: the page (`CountriesList.razor`), both
forms (`CountryAddEdit.razor`, `CountryViewDelete.razor`), the CRUD endpoints
(`CountryEndpoints.cs`), the Excel endpoints (`CountriesExcelEndpoints.cs`), the
contracts (`Countries.cs`, `UserProfile.cs`), the permission codes
(`PermissionCatalog.cs`), the nav item (`CpNavigation.cs`), and the app read
(`ProfileCountriesEndpoint.cs`). **Not re-read this session** (cited from
`docs/pages/cp/admin-countries.md`): `AdminCountryService.Validate` internals, the
`IAuditLog` event names, and `ErrorCodes.cs` constant spellings — treat those as
the reference doc's statement rather than a fresh read.
