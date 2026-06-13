# CP Session categories — Logic (`/admin/session-categories`)

The behaviour behind the page: field mapping, the two validation layers,
soft-delete idempotence, default ordering, audit, and how the lookup reaches the
app. Grounded in `AdminSessionCategoryService.cs`, the entity, the EF config and
the two CP forms.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## L-1 Data model

`SessionCategory : BaseAuditEntity` (`SIMF.Domain/Programme/SessionCategory.cs`):

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | PK (from `BaseAuditEntity`) |
| `Name` | `string` | English, 1–128 chars, required |
| `NameArabic` | `string` | Arabic, 1–128 chars, required |
| `DisplayOrder` | `int` | ascending sort key in the picker / grid |
| `IsActive` | `bool` | soft-delete flag (from base) |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` / `?` | audit stamps (from base) |

EF config (`SessionCategoryConfiguration.cs`): table `SessionCategories`; both
names `HasMaxLength(128).IsRequired()`; composite index
`(IsActive, DisplayOrder)` — the index the active-rows-ordered picker read uses.
Auto-discovered by `ApplyConfigurationsFromAssembly` on the `…Configurations.App`
namespace, so it lives on `SimfAppDbContext` (the App DB, per the D-157
Data ↔ Identity separation).

## L-2 Field mapping (DTO ↔ entity)

| Grid / form field | DTO field | Entity field |
|-------------------|-----------|--------------|
| Name (English) | `Name` | `Name` |
| Name (Arabic) | `NameArabic` | `NameArabic` |
| Display order | `DisplayOrder` | `DisplayOrder` |
| Active | `IsActive` | `IsActive` |
| (detail only) | `CreatedAt` / `UpdatedAt` | `CreatedAt` / `UpdatedAt` |

`ToDetail(...)` maps the entity 1:1 to `AdminSessionCategoryDetail`.

## L-3 Two validation layers

1. **Client guard** (`SessionCategoriesAddEdit.HandleSubmitAsync`): if either
   name is blank/whitespace **or** > 128 chars → in-form `SimfAlert`
   (`Admin.SessionCategories.Required`) and **no request fires**. The display
   order is parsed with `int.TryParse`; a blank / non-numeric / **negative**
   value coerces to `0`. The two name inputs also cap at `MaxLength="128"`.
2. **Server validation** (`AdminSessionCategoryService.ValidateAndNormalise`):
   trims each name and gates length to **1–128**; out of range →
   `ApiException(SESSION_CATEGORY_INVALID, 400, …)` with the bilingual English-
   or Arabic-name message. This is the authoritative guard (the client guard is
   UX only).

There is **no uniqueness check** — duplicate names are allowed, so no 409 path.

## L-4 CRUD behaviour

- **List** (`ListAsync`): `Skip = max(0, query.Skip)`,
  `Top = clamp(query.Top>0 ? query.Top : 25, 1, 200)` (the page sends
  `Top = 20`). Global `Search` matches `Name` **or** `NameArabic` via
  `EF.Functions.Like`. Per-column filters honoured: `name`, `namearabic`,
  `isactive` (`bool.TryParse`); **unknown columns are ignored**. (The UI only
  surfaces filter inputs for `name` + `namearabic`; `isactive` is honoured but
  not surfaced.) Sort keys: `name` / `namearabic` / `order` / `isactive`.
  **Default order = `DisplayOrder` then `Name`.** Returns
  `GridPage<AdminSessionCategorySummary>.Of(page, total, …)`.
- **Get** (`GetAsync`): `AsNoTracking().SingleOrDefault(...)`; null → the
  endpoint throws 404 `SESSION_CATEGORY_NOT_FOUND`.
- **Create** (`CreateAsync`): validate+normalise, new `Guid`,
  `IsActive = true`, `CreatedAt = now`, save, **audit
  `SessionCategory.Created`**, return detail.
- **Update** (`UpdateAsync`): load tracked row (404 if missing),
  validate+normalise, overwrite `Name` / `NameArabic` / `DisplayOrder` /
  `IsActive`, set `UpdatedAt = now`, save, **audit `SessionCategory.Updated`**
  (`Detail` carries `active=…`).
- **Deactivate** (`DeactivateAsync`): load tracked row (404 if missing);
  **idempotent** — if already inactive it returns early (no save, **no audit
  row**). Otherwise `category.Deactivate()` (sets `IsActive = false`),
  `UpdatedAt = now`, save, **audit `SessionCategory.Deactivated`**.

There is **no hard-delete** — the only delete path is soft. The list endpoint
applies **no default active filter**, so a deactivated row stays in the grid
with its Active pill flipped to `Inactive`.

## L-5 Audit

One `AuditEntry` per successful mutation (`IAuditLog.WriteAsync`):

| Event (`AuditEvents`) | Written by | `Detail` |
|-----------------------|------------|----------|
| `SessionCategoryCreated` | `CreateAsync` | `id={id}; name={name}` |
| `SessionCategoryUpdated` | `UpdateAsync` | `id={id}; name={name}; active={IsActive}` |
| `SessionCategoryDeactivated` | `DeactivateAsync` | `id={id}; name={name}` |

`ActorUserId` comes from the JWT `sub` claim (the endpoint rejects a
missing/unparseable `sub` with 401 before calling the service). These are
audit-trail rows, not duplicated live data (consistent with the D-157
audit-snapshot exception).

## L-6 Presentation preference (D-353)

`OnInitializedAsync` reads `Prefs.GetPresentationAsync("session-categories")`
(`CpPreferences`, persisted in `localStorage` key
`simf.cp.prefs.session-categories`). `CrudPresentation.Dialog` is the default.
When `Page`, an open form hides the grid + banner (`GridHidden`) and renders the
`CrudShell` full-page frame.

## L-7 How the lookup reaches the app

- A `Session` references a category by the **bare `Session.CategoryId`** — a
  logical FK (a `Guid`), resolved on read within the App context. No DB
  constraint crosses to Identity (D-157); the reference is App-internal.
- The public agenda projection (`GET /app/programme/sessions`, Page_016) carries
  `categoryId` / `categoryName` / `categoryNameArabic` per session so the app's
  agenda renders the "is-main-session / type" tag from the **cached programme**
  without a second fetch (Page_016_Logic L-4 / L-8: bilingual pair with
  cross-language fallback). The category does **not** render on the agenda list
  row; it rides the cache for the session-detail preview (Page_017).
- The CP session form (sibling [`admin-sessions.md`](../../pages/cp/admin-sessions.md))
  loads the **active** categories from `…/list` to fill its picker and resolves
  the name client-side (like the Hall / Company picker).

## L-8 Localization / RTL

All visible strings are `Admin.SessionCategories.*` (title, column headers,
field labels, action labels, toasts, empty/loading) plus shared `Grid.*` keys —
EN ↔ AR parity across both resx locales. The page mirrors under
`<html dir="rtl">` when Arabic is active. Server validation + not-found messages
are bilingual via `ApiException` (EN + AR), surfaced through
`MessageForCurrentCulture()`.

## L-9 Edge cases / known limitations

- **Ships empty (OI-2).** First render is the empty state until the team seeds
  rows once the client confirms the category list.
- **Deactivate is idempotent** — re-deleting an already-inactive row is a no-op
  (no error, no audit row).
- **Soft-deleted rows remain listed** — no active filter on the list; the Active
  column flips to `Inactive` rather than the row disappearing. Assert the pill,
  not row removal.
- **Display-order coercion** — invalid input resolves to `0` on both client and
  server (the create request defaults `DisplayOrder` to 0; negatives coerce
  client-side).
- **No uniqueness** — duplicate names are permitted (no 409).
- **`Session.CategoryId` is RESTRICT at the DB level** — a referenced category
  cannot be hard-deleted, but the page only ever soft-deletes, so this never
  surfaces as a guarded error here.

## Cross-links

- Contract / DTOs / errors: [admin-session-categories_API.md](admin-session-categories_API.md)
- What the admin does: [admin-session-categories_Function.md](admin-session-categories_Function.md)
- Screen layout: [admin-session-categories_Design.md](admin-session-categories_Design.md)
- Consumer (app agenda): [`docs/App/Page_016/Page_016_Logic.md`](../../App/Page_016/Page_016_Logic.md)
- E2E: [`docs/tests/e2e/cp-admin-session-categories.md`](../../tests/e2e/cp-admin-session-categories.md)
