# Themes & pillars — `/admin/themes`

| | |
|--|--|
| **Route** | `/admin/themes` |
| **Audience** | Administrator |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Themes.View)]` + `RequireApprovedAccount` + `RequireRateLimiting("auth")` (mutations) |
| **Pattern** | D-117 + D-132 canonical CRUD. **First D-135 freeze-lift module.** |
| **Status** | ✅ Real (D-134 Sprint B) |
| **Backend endpoints** | `POST /account/api/admin/themes/list`, `GET /admin/themes/{id}`, `POST /admin/themes`, `PUT /admin/themes/{id}`, `DELETE /admin/themes/{id}` |
| **Source** | [`ThemesList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ThemesList.razor), [`ThemeForm.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ThemeForm.razor), [`AdminThemeService`](../../../src/Backend/SIMF.Infrastructure/Programme/AdminThemeService.cs), [`Theme`](../../../src/Backend/SIMF.Domain/Programme/Theme.cs) |
| **Backed by** | **New** `dbo.Themes` table (migration `AddThemes`, 2026-05-28). |
| **Tests** | [`docs/tests/e2e/cp-admin-themes.md`](../../tests/e2e/cp-admin-themes.md) |
| **Last reviewed** | 2026-05-29 |

## 1. Purpose

Programme themes / pillars per SIMF-FDS-004 §5.1 — the top-level
grouping the agenda uses. Sessions reference a theme through `ThemeId`
in Sprint B's later commits. Each theme has a **Code** (the programme
team's stable identifier, e.g. "DEF", "TECH"), bilingual name +
description, a sort key, and an accent colour.

This is the **first new-schema module** under D-135 — the EF migration
`AddThemes` lands in the same commit per D-135's carry-forward rule
(a). The shape is identical to every CRUD page that came before it
(D-132 canonical pattern); the new ground is on the persistence side.

## 4. UI

- Banner + canonical D-132 toolbar (Select all + Add theme).
- Grid columns: Code, Name, Name (Arabic), Order, Color (swatch +
  literal text), Status (Active / Inactive pill).
- Per-row Details modal showing every field including descriptions.
- Per-row Edit modal hosting `ThemeForm` (Initial=row).
- Per-row Deactivate (soft-delete). Since D-353 the Deactivate action opens the
  reusable `ThemesViewDelete` form (hosted by `CrudShell` as a popup or full page)
  whose Deactivate button is gated by a `SimfConfirm` dialog — no longer a one-click
  delete from the list row.
- Sortable on Code, Name, Order.
- **Excel export + import (D-356):** the toolbar carries **Export** and **Import**
  actions. Export posts `AdminGridExportRequest { Ids, Query }` to
  `/account/api/admin/themes/export` (selected rows, else the whole filtered grid)
  and downloads `simf-themes-{timestamp}.xlsx` with the sheet "Themes" and header
  row `Code | Name | NameArabic | DisplayOrder | PageColor | IsActive`. Import
  (insert-only) posts an `.xlsx` to `/account/api/admin/themes/import` (required
  headers `Code | Name | NameArabic | PageColor`) and shows a result modal
  ("N created, N updated, N skipped" + per-row errors); a duplicate Code is a
  per-row error, not a batch abort. Both are capped at 5000 rows; a non-`.xlsx`
  upload is rejected with HTTP 400.
- **Page ↔ Popup presentation toggle (D-353):** the toolbar `CrudPresentationToggle`
  lets the admin host Add/Edit/View/Delete as a dialog or a full page; the choice
  persists in `localStorage` under `simf.cp.prefs.themes` and is restored on load.

## 4.5 Form fields

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Code | yes | 16 | 2–16 chars; uppercased server-side; unique |
| Name (English) | yes | 128 | 1–128 chars |
| Name (Arabic) | yes | 128 | 1–128 chars |
| Description (English) | no | 1024 | optional |
| Description (Arabic) | no | 1024 | optional |
| Display order | yes | n/a | integer ≥ 0 |
| Page color | yes | 32 | hex / CSS variable / free text |
| Active | (Edit only) | bool | — |

## 5. Data flow + endpoints

Identical canonical shape — see [`admin-roles.md`](admin-roles.md) §5.
Substitute `roles` → `themes`, `RoleManager` → EF `Themes` DbSet.

## 6. Validation + error handling

- **Server-side `AdminThemeService.ValidateAndNormalise`:** trims +
  upper-cases `Code` (case-insensitive uniqueness); length-gates Code
  (2–16), Name (1–128), NameArabic (1–128), PageColor (1–32);
  `DisplayOrder` ≥ 0.
- **Duplicate code:** 409 `ThemeCodeDuplicate` (bilingual; surfaces the
  code in the message).
- **Not found:** 404 `ThemeNotFound`.
- **Future Sessions in-use guard:** the `ThemeInUse` error code is
  registered now so the Sessions commit can wire the guard without
  contract surface churn.

## 7. Edge cases + known limitations

- **Deactivate is unconditional in Sprint B.** When Sessions ships, the
  Deactivate flow will refuse to deactivate a theme that any active
  session references; `ThemeInUse` is reserved for that.
- **Code uppercasing** — server normalises to ASCII upper before
  storing, so "Def" and "DEF" are the same code. Display preserves
  the canonical upper form.
- **Bilingual descriptions optional** — `null` allowed; UI renders "—"
  for empty values.

## 8. i18n + RTL

`Admin.Themes.*` keys (~60 per locale). EN ↔ AR parity preserved.

## 10. Use cases

- UC-THM-CREATE-001, UC-THM-EDIT-001, UC-THM-DEACTIVATE-001
  _(UCS detail entries authored under Sprint B's UCS expansion follow-up)_.

## 11. E2E

See [`docs/tests/e2e/cp-admin-themes.md`](../../tests/e2e/cp-admin-themes.md):
E2E-THM-001 create golden, 002 duplicate code 409, 003 edit, 004
deactivate, 005 details modal, 006 auth, 007 RTL.

## 12. Related docs

- Admin Manual: `Admin-Manual.md § 5.1 Themes & pillars`.
- Decisions: D-134 plan + D-135 freeze-lift + Sprint B Themes (this commit).
- Authority spec: SIMF-FDS-004 §5.1.
- Sibling Programme modules pending: Halls, Speakers, Sessions, Bookings.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-29 | D-134 Sprint B / D-135 | Original — Themes entity + EF migration `AddThemes` + canonical CRUD page. First D-135 freeze-lift module shipped. |
| 2026-06-10 | D-356 / D-353 | Excel export + import added (toolbar Export/Import → `.xlsx`, sheet "Themes"); CRUD forms split into `ThemesAddEdit` + `ThemesViewDelete` hosted by `CrudShell` with a `SimfConfirm`-gated Deactivate and a Page↔Popup presentation toggle persisted in `localStorage`. E2E catalogue extended with E2E-THM-019…024. |

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + D-353 toggle).
