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

All three grids also offer a read-only **Details** view (D-835), which carries no
permission of its own — reading a row is what `RatingConfig.View` already bought.
It matters most on the **types** grid, where seven fields have no column at all:
the Arabic name, the overall-stars and comment settings with both comment labels,
the display order and the creation date. Before it, a holder without
`RatingConfig.Edit` could open a type through **Manage** and still not read any
of them.

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
- **Dialog validation surface (BUG-004).** The page-level `_toast` `SimfAlert`
  lives inside `.simf-surface`, which sits **under** the modal backdrop
  (`.simf-modal { position: fixed; inset: 0; z-index: 100 }`), so while a dialog
  is open a toast is invisible and Save read as a dead button. All three dialogs
  (type / group / question) now carry their own `_error`, rendered as a
  `SimfAlert Variant="error"` in the dialog body — the same shape the canonical
  CRUD forms use — and a blank required field is caught client-side before the
  request goes out (`Admin.RatingConfig.{Type|Group|Question}.Required`). `Code`
  is required on Create only, since it is locked on Edit. `_error` is cleared
  when a dialog is re-opened.

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
_Last reviewed:_ 2026-07-26 by Claude (BUG-004 — dialog validation surface).
