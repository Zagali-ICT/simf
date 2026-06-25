# CP page — Rating configuration (`/admin/rating-config`)

**Status:** ✅ Real (D-496) · **Permission:** `RatingConfig.View` (page) ·
`RatingConfig.{Create,Edit,Delete}` (API actions) · **Nav:** `Module.RatingConfig`
under *Engagement*.

## Purpose

Admin configuration of the dynamic rating system. Defines **rating types** (e.g.
App, Session, and any admin-created type), each with **question groups** and
**questions**, so the attendee rating forms are data-driven rather than hard-coded.
The submitted results are viewed read-only on [`/admin/ratings`](admin-ratings.md).

## Shape

Three-level master-detail (`SimfDataGrid`s, mirrors `FaqManager`):

1. **Rating types** grid — Code, Name, Scope (`Global` once-per-user / `PerSession`),
   Built-in, Groups, Questions, Responses, Active. Row action **Manage** selects a
   type and loads its groups + questions below.
2. **Question groups** grid (per selected type) — Name, Order, Questions, Active.
3. **Questions** grid (per selected type) — Question text, Group, Required, Order,
   Active. Each question is a fixed 1–5 star scale; wording is data.

CRUD runs through three `SimfModal`s. Soft-delete (Deactivate → `IsActive=false`).

## Rules

- **System types** (`App`, `Session`) are seeded by `RatingSeeder`, **cannot be
  deleted** (the API returns 400 `RATING_TYPE_IS_SYSTEM`), and their `Code`/`Scope`
  are locked in the edit modal.
- **Code** is unique (409 `RATING_TYPE_CODE_TAKEN`) and immutable after create.
- **Scope** drives the attendee form: `Global` = one submission per user (App);
  `PerSession` = one per user per session (the form needs a target session id).
- **Per-type config:** `HasOverallStars` (show the overall star bar) + `AllowComment`
  (the optional end comment) + optional bilingual comment label.
- Deleting a **group** leaves its questions flat (`SetNull`), never cascade-deletes
  them.

## Endpoints

`/admin/ratings/types/list|{id}` · `/admin/ratings/types` (POST/PUT/DELETE) ·
`/admin/ratings/types/{typeId}/groups/list` + `/admin/ratings/groups` ·
`/admin/ratings/types/{typeId}/questions/list` + `/admin/ratings/questions`.
All gated `RatingConfig.{View|Create|Edit|Delete}` + `RequireApprovedAccount`.

## Tests

- API: `tests/SIMF.Api.Tests/RatingConfigTests.cs` (CRUD, system-delete-block,
  duplicate-code, permission gate, required-question enforcement).
- E2E: [`e2e/cp-admin-rating-config.md`](../../tests/e2e/cp-admin-rating-config.md).

_Last reviewed:_ 2026-06-25 (D-496).
