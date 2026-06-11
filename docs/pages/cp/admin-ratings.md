# Ratings / feedback — `/admin/ratings`

| | |
|--|--|
| **Route** | `/admin/ratings` |
| **Audience** | Administrator |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Ratings.View)]` (CP page) + API `PolicyFor(Ratings.View)` + `RequireApprovedAccount`; the export endpoint also carries `RequireRateLimiting("auth")` |
| **Pattern** | **Read-only review board** — NOT canonical CRUD. Ratings are owned by the attendees who submit them; the admin page is a viewer over the active set, plus a D-356 Excel **export**. |
| **Status** | ✅ Real (D-199, Mockup screen 40 "Rate the Forum") |
| **Backend endpoints** | List (BFF) `POST /account/api/admin/feedback/ratings` → API `POST /api/v1/admin/feedback/ratings`. Export (BFF) `POST /account/api/admin/ratings/export` → API `POST /api/v1/admin/ratings/export`. **No create / update / delete endpoint exists for this page.** |
| **Source** | [`RatingsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/RatingsList.razor), [`FeedbackEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Feedback/FeedbackEndpoints.cs) (`ListAdminRatingsEndpoint`), [`RatingsExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/RatingsExcelEndpoints.cs) (`ExportRatingsEndpoint`), [`RatingService.cs`](../../../src/Backend/SIMF.Infrastructure/Feedback/RatingService.cs) (`ListAllAsync`), [`AccountEndpoints.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs) (BFF passthrough + `MapGridExport("ratings")`), [`FeedbackContracts.cs`](../../../src/Shared/SIMF.Contracts/Feedback/FeedbackContracts.cs) |
| **Backed by** | Existing `dbo.Ratings` table (`SimfAppDbContext`) — read only on this page; no schema change for D-356. |
| **Tests** | [`docs/tests/e2e/cp-admin-ratings.md`](../../tests/e2e/cp-admin-ratings.md) |
| **Last reviewed** | 2026-06-11 |

## 1. Purpose

The admin Ratings page is a **read/review board** over the attendees' overall
forum ratings (D-199, Mockup screen 40 "Rate the Forum"). Each attendee submits
**one** overall rating — 1–5 stars plus an optional free-text comment — through
the public attendee API (`POST /api/v1/feedback/rate`, upserted one row per user).
The admin page renders the active set as a grid plus a two-card headline (average
stars + count) so the programme team can report how the forum was received.

It is deliberately **not** a CRUD page: there is no Add / Edit / Delete /
Deactivate. Ratings are owned by the attendees who wrote them, so the only
admin affordances are reading, filtering, sorting, paging, and the D-356 Excel
**export**. Soft-deleted (inactive) ratings are excluded by the service, so the
grid only ever shows active rows.

## 4. UI

- `SimfBanner` titled `Admin.Ratings.Title` ("Ratings" / "التقييمات").
- Two headline `SimfStatCard`s in the `.simf-form__actions` flex row:
  **Average rating** (`AverageStars`, formatted `"0.0"` with
  `CultureInfo.InvariantCulture` — a dot decimal even under Arabic) and
  **Total ratings** (`RatingCount`, invariant).
- A `SimfDataGrid` (`Multiselect="true"`) of `AdminRatingSummary` rows with the
  standard select-all + per-row checkbox column, the full pager (First / Prev /
  numbered / Next / Last + page-size selector) and the summary line
  "Showing {from}–{to} of {total}".
- **Read-only:** the grid wires **no** Add / Edit / Details / Delete actions and
  **no** bulk-action button. The only toolbar action is **Export** (`OnExport`).
  The select-all + row checkboxes drive only the export selection — they perform
  no write or bulk-edit.
- `SimfEmptyState` (`Admin.Ratings.None` — "No ratings yet." /
  "لا توجد تقييمات بعد.") in the grid's `EmptyTemplate` when the active set is empty.
- `SimfAlert` (`_toast`, `Variant="error"`) at the top of the surface on a
  load failure.
- **Excel export only (D-356):** the toolbar **Export** action calls the JS
  interop `simfAccount.downloadXlsx` against `/account/api/admin/ratings/export`
  (BFF → API) as a direct file download. There is **no Import action** — the
  page is export-only (no `OnImport` is wired; `MapGridExport` registers the
  paired import route generically, but it is unreachable from this read-only page
  and would have no write path on the Ratings surface).

## 4.5 Grid columns + filters

This page has **no create / edit form** (ratings are not authored in the CP). The
grid columns are:

| Column (key) | Render | Sortable | Filterable |
|--------------|--------|----------|------------|
| Stars (`stars`) | `{Stars} / 5` | yes | no |
| Comment (`comment`) | comment text; `—` when blank | no | yes (per-column) |
| Active (`isActive`) | `SimfPill` — "Active" (`Variant="on"`) / "Inactive" (`Variant="off"`); only ever "Active" here | no | no |
| Submitted at (`createdAt`) | `CreatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")` | yes | no |

- **Per-column filter** — only the Comment column is `Filterable="true"`. Typing
  posts `GridQuery.Filters["comment"]` and the service applies
  `.Where(r => r.Comment != null && r.Comment.Contains(v))`.
- **Sort** — Stars (`stars`) and Submitted at (`createdAt`); the default order is
  `CreatedAt` descending. Comment and Active expose no sort.

## 5. Data flow + endpoints

**List (on load + every grid query change):**
`RatingsList.razor` posts the `GridQuery` (`Top = 20` by default) via
`simfAccount.postJson` to BFF `POST /account/api/admin/feedback/ratings`
→ `SimfAdminClient.ListRatingsAsync` → API `POST /api/v1/admin/feedback/ratings`
(`ListAdminRatingsEndpoint`) → `RatingService.ListAllAsync` → `ApiResult<AdminRatingsPage>`.
`AdminRatingsPage` carries `Ratings` (`GridPage<AdminRatingSummary>`),
`AverageStars`, and `RatingCount`.

**Export (toolbar Export):** `OnExportAsync` posts an `AdminGridExportRequest`
(`Ids` = ticked rows; `Query` = the current `_query` when nothing is selected,
else `null`) to BFF `POST /account/api/admin/ratings/export`
→ `SimfAdminClient.ExportGridAsync("ratings", …)` → API `POST /api/v1/admin/ratings/export`
(`ExportRatingsEndpoint`, gated by `Ratings.Export`). The endpoint returns the
`.xlsx` bytes as `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
with a `Content-Disposition` filename `simf-ratings-{yyyyMMddHHmmss}.xlsx`,
sheet **"Ratings"**, header row **`Stars | Comment | IsActive | CreatedAt`**
(`CreatedAt` formatted `yyyy-MM-dd HH:mm:ss 'UTC'`). It reuses the same
`RatingService.ListAllAsync` the list endpoint uses.

## 6. Validation + error handling

- This page issues no writes, so there is no form validation here. (Attendee-side
  validation — 1–5 stars, comment ≤ 2000 chars — lives in `RatingService.RateAsync`,
  reached only via the public `POST /api/v1/feedback/rate`.)
- **Load failure** — when the list envelope is not `Success`, the page shows a
  `SimfAlert` with `env.Error.MessageForCurrentCulture()` or the fallback
  `Admin.Ratings.LoadFailed` ("Could not load ratings…" / "تعذّر تحميل التقييمات…");
  the stat cards stay at `0.0` / `0` and the grid renders the empty state.
- **Average guard** — `RatingService.ListAllAsync` returns `0d` for the average
  when the (filtered) active count is `0`, so `AverageAsync` is never called over
  an empty sequence.

## 7. Edge cases + known limitations

- **Inactive ratings excluded.** `ListAllAsync` applies
  `.Where(rating => rating.IsActive)`, so soft-deleted rows never appear and never
  skew the headline count or average. The Active pill is therefore always "Active".
- **One rating per attendee.** The attendee write upserts on `UserId`, so a
  re-submit revises the single existing row (and reactivates a previously
  soft-deleted one) rather than adding a second — the Total count is one per
  attendee.
- **Headline recomputes after filtering.** The average and count are computed over
  the *filtered* active set, so a Comment filter narrows the headline figures too.
- **Export cap is a silent clamp.** The generic export base (`AdminGridExportEndpoint`)
  forces `Skip = 0` and `Top = MaxExportRows` (5000) before listing — a set larger
  than 5000 is truncated to the first 5000 rows; it is **not** rejected with an
  error.
- **Average format is invariant.** `AverageStars` always renders with a Latin/dot
  decimal (e.g. `"5.0"`) regardless of UI culture, by design.

## 8. i18n + RTL

`Admin.Ratings.*` keys (Title, Stat.Average, Stat.Count, Col.Stars, Col.Comment,
Col.Active, Col.CreatedAt, None, Loading, LoadFailed) plus the shared `Grid.*`
keys (Active, Inactive, Export, Summary, Page, pager labels, filter labels) — EN
↔ AR parity. Under Arabic the page sets `dir="rtl"`, mirrors the stat-card row and
nav rail, and localises the headers, while the average value keeps its invariant
decimal point.

## 10. Use cases

- UC-RAT-VIEW (review the forum ratings + headline average), UC-RAT-FILTER
  (narrow by comment text), UC-RAT-SORT (by stars / submitted-at), UC-RAT-EXPORT
  (D-356 Excel download of the filtered set or the selected rows). No create /
  edit / delete use case — the surface is read-only.

## 11. E2E

See [`docs/tests/e2e/cp-admin-ratings.md`](../../tests/e2e/cp-admin-ratings.md):
E2E-RAT-001 golden read round-trip, 002 stat cards, 003 table columns, 004 pager
summary, 005 read-only guarantee (Export is the only toolbar action), 006 empty
state, 007 auth gate (`Ratings.View`, API 403), 008 inactive excluded, 009 server
500 load failure, 010 RTL / Arabic, 011 Comment per-column filter, 012 Stars /
Submitted-at sort, 013 Excel export (whole filtered set vs selected rows, D-356).
Lower-layer API integration tests: `tests/SIMF.Api.Tests/FeedbackRatingsTests.cs`
and `tests/SIMF.Api.Tests/RatingsExcelTests.cs`.

## 12. Related docs

- Permissions: `PermissionCatalog.Ratings.View` (`"Ratings.View"`) and
  `PermissionCatalog.Ratings.Export` (`"Ratings.Export"`), both `AdminOnly`
  baseline — `docs/SIMF-Permission-Catalogue.md`.
- Decisions: D-199 (Ratings read-only admin view), D-356 (grid Excel export wave).
- Authority spec: SIMF-FDS-004 (audience feedback / ratings); Mockup screen 40
  "Rate the Forum".
- Sibling: `admin-comments-moderation.md` (the other read/moderate feedback board).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-30 | D-199 | Original — read-only admin Ratings view (average-stars + count headline + grid of active ratings). No CRUD; ratings owned by attendees. |
| 2026-06-10 | D-356 | Excel **export only** added — toolbar Export → `/account/api/admin/ratings/export` (BFF → API), sheet "Ratings", columns `Stars \| Comment \| IsActive \| CreatedAt`, capped at 5000 rows. No import / no write action introduced. E2E catalogue extended with E2E-RAT-013. |

_Last reviewed:_ 2026-06-11 by Claude (D-356 reference doc — read-only Ratings board + Excel export-only).
