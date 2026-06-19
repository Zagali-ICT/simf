# Programme days — `/admin/programme-days`

| | |
|--|--|
| **Route** | `/admin/programme-days` |
| **Audience** | Administrator |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.ProgrammeDays.View)]` (page) + per-action `Create` / `Edit` / `Delete` policies + `RequireApprovedAccount` + `RequireRateLimiting("auth")` (mutations) |
| **Pattern** | D-452 date-keyed bilingual lookup CRUD. `SimfDataGrid`; D-353 `CrudShell` framing; D-357 logo via the unified media-asset pipeline. **No Excel.** |
| **Status** | ✅ Real (D-452) |
| **Backend endpoints** | BFF `/account/api/admin/programme-days/*` → API: `POST /admin/programme-days/list`, `GET /admin/programme-days/{id}`, `POST /admin/programme-days`, `PUT /admin/programme-days/{id}`, `DELETE /admin/programme-days/{id}` |
| **Backed by** | **New** `dbo.ProgrammeDays` table (migration `D452_AddProgrammeDays`) — `Date` (DateOnly) + `Title` / `TitleArabic` + `DisplayOrder`. The logo is the D-357 `Asset` row (`AssetCategory.ProgrammeDayImage`, `OwnerId = day.Id`), not a column. |
| **Source** | [`ProgrammeDaysList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ProgrammeDaysList.razor), [`ProgrammeDaysAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ProgrammeDaysAddEdit.razor), [`ProgrammeDaysViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ProgrammeDaysViewDelete.razor), [`ProgrammeDayEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/ProgrammeDayEndpoints.cs), [`AdminProgrammeDayService.cs`](../../../src/Backend/SIMF.Infrastructure/Programme/AdminProgrammeDayService.cs), [`ProgrammeDay.cs`](../../../src/Backend/SIMF.Domain/Programme/ProgrammeDay.cs) |
| **Tests** | [`docs/tests/e2e/cp-admin-programme-days.md`](../../tests/e2e/cp-admin-programme-days.md) + [`tests/SIMF.Api.Tests/ProgrammeDaysTests.cs`](../../../tests/SIMF.Api.Tests/ProgrammeDaysTests.cs) |
| **Last reviewed** | 2026-06-19 |

## 1. Purpose

A **programme day** (Figma 883:2308 "تفاصيل اليوم") is the entity that heads the
app's Sessions screen: a **date**, a **bilingual title** (e.g. "Opening Day" /
"يوم الافتتاح"), a **display order** (the day-strip sort key), and an optional
**logo** shown in the day banner. The app's `/app/programme/days` read buckets
each active session onto its event-local (KSA, UTC+3) calendar date and attaches
it to the authored day that owns that date.

This page is the admin CRUD over that lookup. It is modelled on the
session-category lookup — in-service validation, soft-delete via `IsActive`, one
audit row per mutation — plus a `Date`, a **one-active-day-per-date** uniqueness
guard, and a `HasImage` flag resolved from the `Asset` table.

While the table is empty the app **synthesises one day per distinct session date**
so the agenda never blanks (a strict superset of the old sessions screen); once a
day is authored its title + logo + grouping take over for that date.

## 4. UI

- Banner (`SimfBanner`, title `Admin.ProgrammeDays.Title`) + the canonical
  `SimfDataGrid`.
- Grid columns: **Date**, **Title (EN)**, **Title (AR)**, **Order**, **Logo**
  (`SimfPill` Set/None), **Active** (`SimfPill` on/off). Multiselect renders
  select-all / per-row checkboxes (cosmetic — no bulk endpoint).
- Server-paged with a numbered pager (`GridQuery { Top = 20 }`); per-column filter
  inputs on **Title (EN)** (`title`) and **Title (AR)** (`titlearabic`); column
  sort on **Date** / **Title (EN)** / **Order** / **Active** (`date` / `title` /
  `order` / `isactive`). Default order is `DisplayOrder` then `Date`.
- Toolbar **Add** + per-row **Edit** (pencil) / **Details** (eye) / **Delete**
  (trash) are quiet grid affordances.
- Add / Edit / View / Delete are hosted by `CrudShell` framing the reusable
  `ProgrammeDaysAddEdit` (Add/Edit) and `ProgrammeDaysViewDelete` (View/Delete).
  The Details (eye) action opens `ProgrammeDaysViewDelete` read-only; Edit
  re-fetches the row via `GET …/{id}` to pre-fill.
- Per-row Delete is a soft-delete (Deactivate) behind a `SimfConfirm` gate.
- **Logo (D-357), Edit-only.** The Add form carries **no** upload — the row must
  exist before bytes can be attached. The Edit form renders
  `<SimfImageUpload Category="ProgrammeDayImage" OwnerId="@Initial.Id">`; the grid
  Logo column reflects the resolved `HasImage`.
- **No Excel.** This page omits `OnExport` / `OnImport` / `CrudGridExcel` (tiny
  date-keyed lookup; the logo is not workbook data).
- **Page ↔ Popup presentation toggle (D-353):** `<CrudPresentationToggle
  PageKey="programme-days">` in the toolbar persists the choice in `localStorage`
  under `simf.cp.prefs.programme-days` (via `CpPreferences`), restored in
  `OnInitializedAsync`.

## 4.5 Form fields

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Date | yes | n/a | a calendar date; client guard ("A date is required."); one **active** day per date (server) |
| Title (English) | yes | 128 | 1–128 chars (client guard + server) |
| Title (Arabic) | yes | 128 | 1–128 chars (client guard + server) |
| Display order | yes | n/a | integer ≥ 0; non-numeric coerces to 0 (`int.TryParse` fallback) |
| Active | (Edit only) | bool | shown only when `IsEdit=true`; ticked by default on Add |
| Day logo | (Edit only) | image | `SimfImageUpload`, `AssetCategory.ProgrammeDayImage`; appears only once the row exists |

## 5. Data flow + endpoints

BFF passthroughs live in `AccountEndpoints.cs` under
`/account/api/admin/programme-days/*` and `Forward` to `SimfAdminClient`, which
calls the API.

| Verb + BFF route | API endpoint | Permission policy | Notes |
|------------------|--------------|-------------------|-------|
| `POST …/list` | `POST /admin/programme-days/list` | `ProgrammeDays.View` | `GridQuery` in → `GridPage<AdminProgrammeDaySummary>` |
| `GET …/{id}` | `GET /admin/programme-days/{id:guid}` | `ProgrammeDays.View` | `AdminProgrammeDayDetail`; 404 `PROGRAMME_DAY_NOT_FOUND` |
| `POST …` | `POST /admin/programme-days` | `ProgrammeDays.Create` | create; rate-limited (`auth`) |
| `PUT …/{id}` | `PUT /admin/programme-days/{id:guid}` | `ProgrammeDays.Edit` | update; id from route |
| `DELETE …/{id}` | `DELETE /admin/programme-days/{id:guid}` | `ProgrammeDays.Delete` | soft-delete (`Deactivate`); returns `ApiResult<bool>` |

All six permission codes (`View` / `Create` / `Edit` / `Delete` / `Export` /
`Import`, baseline `AdminOnly`) are defined on the `PermissionCatalog.ProgrammeDays`
nested class and registered in `PermissionCatalog.All` (Export/Import reserved —
no Excel UI yet). The nav item `Module.ProgrammeDays` carries `RequiredPermission:
ProgrammeDays.View`. The logo's serve/upload routes are gated through
`AssetPermissionRegistry[ProgrammeDayImage] = (View, Edit)`.

## 6. Validation + error handling

- **Client guard (`ProgrammeDaysAddEdit.HandleSubmitAsync`):** blank/over-128
  English or Arabic title → in-form `SimfAlert` (`Admin.ProgrammeDays.Required`);
  missing date → `Admin.ProgrammeDays.DateRequired`; no request fires. Display
  order is parsed with `int.TryParse` and coerced to 0 when blank/non-numeric.
- **Server-side `AdminProgrammeDayService`:** trims + length-gates both titles to
  1–128 (`PROGRAMME_DAY_INVALID`); `EnsureUniqueDateAsync` rejects a second
  **active** day on the same date (`PROGRAMME_DAY_INVALID`, bilingual "A programme
  day already exists for that date.").
- **Not found:** 404 `PROGRAMME_DAY_NOT_FOUND` (GET / PUT / DELETE on a missing id).

## 7. Edge cases + known limitations

- **One active day per date.** The uniqueness guard only counts active rows, so a
  deactivated date can be re-used by a new day.
- **Deactivate is idempotent.** `DeactivateAsync` returns early when the row is
  already inactive (no error, no audit row).
- **No active filter on the list.** The page sends `GridQuery { Top = 20 }` with no
  default active filter, so a soft-deleted row stays visible — its Active column
  flips from the on pill to the off pill rather than disappearing.
- **Logo is Edit-only.** Add cannot attach bytes (the asset is owned by the row id,
  which does not exist until Create). `HasImage` is resolved from the `Asset` table
  on list/detail; there is no logo column.
- **Synthesised fallback.** With zero authored rows the public read synthesises a
  day per session date (`HasImage=false`, deterministic per-date id) — authoring
  any row switches the app to the authored set.

## 8. i18n + RTL

`Admin.ProgrammeDays.*` keys (title, column headers, field labels, action labels,
toasts, empty/loading states) plus the shared `Grid.*` keys. EN ↔ AR parity is
maintained across both resx locales; the page mirrors under `<html dir="rtl">`
when Arabic is active.

## 9. Related — session Type (same changeset, D-452)

The same increment added an optional **session type** (`Workshop` / `Session` /
`Event`) to the session form (`SessionsAddEdit.razor`, a `SimfSelect` bound to
`AdminCreate/UpdateSessionRequest.Type`, labels `Admin.Sessions.Type.*`). The type
drives the app's Sessions type-tabs (الكل / ورش العمل / جلسات / احداث); an unset
type means the session shows under "All" only. See
[`cp/admin-sessions.md`](admin-sessions.md).

## 10. Use cases

- Author the programme days (date + bilingual title + logo + order) that head the
  app's Sessions screen, so visitors see the right day card and grouped sessions
  instead of the synthesised date-only fallback.

## 11. E2E

See [`docs/tests/e2e/cp-admin-programme-days.md`](../../tests/e2e/cp-admin-programme-days.md):
E2E-PGD-001 full CRUD round-trip, 002 empty list, 003 auth gate, 004 Add form, 005
Edit pre-fill + logo, 006 logo upload, 007 delete via ViewDelete + SimfConfirm, 008
cancel, 009 client blank-titles, 010 client missing-date, 011 same-date uniqueness
(400), 012 server over-128 (400), 013 action gating, 014 server-500, 015 RTL, 016
per-column filter, 017 column sort, 018 app parity (`/app/programme/days`).

## 12. Related docs

- Authority spec: SIMF-FDS-004 §5.3 (programme) + Figma 883:2308 ("تفاصيل اليوم").
- Decisions: D-452 (programme days + logo + session-type wiring); D-357 (unified
  media-asset pipeline — no per-entity logo columns); D-353 `CrudShell` /
  `SimfConfirm` framing + presentation toggle.
- Sibling lookups: session categories (`admin-session-categories.md`), Themes
  (`admin-themes.md`).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-19 | D-452 | Original — `ProgrammeDay` date-keyed bilingual lookup + EF migration `D452_AddProgrammeDays` + logo via `AssetCategory.ProgrammeDayImage` + admin CRUD page (no Excel). One-active-day-per-date guard. Same changeset wired optional `Session.Type` (Workshop/Session/Event) through the session form. |

_Last reviewed:_ 2026-06-19 by Claude (D-452 — programme days + logo + session-type wiring).
