# Halls & seating — `/admin/halls`

| | |
|--|--|
| **Route** | `/admin/halls` |
| **Audience** | Administrator |
| **Auth** | `[Authorize(Roles = "Administrator")]` + `RequireApprovedAccount` + `RequireRateLimiting("auth")` (mutations) |
| **Pattern** | D-117 + D-132 canonical CRUD. |
| **Status** | ✅ Real (D-134 Sprint B / D-135) |
| **Backend endpoints** | `POST /account/api/admin/halls/list`, `GET /admin/halls/{id}`, `POST /admin/halls`, `PUT /admin/halls/{id}`, `DELETE /admin/halls/{id}`, `GET /admin/halls/{id}/schedule` (QA B16 — the hall occupancy view, `Halls.View`) |
| **Source** | [`HallsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/HallsList.razor), [`HallForm.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/HallForm.razor), [`AdminHallService`](../../../src/Backend/SIMF.Infrastructure/Programme/AdminHallService.cs), [`Hall`](../../../src/Backend/SIMF.Domain/Programme/Hall.cs) |
| **Backed by** | **New** `dbo.Halls` table (migration `AddHalls`, 2026-05-28). |
| **Tests** | [`docs/tests/e2e/cp-admin-halls.md`](../../tests/e2e/cp-admin-halls.md) |
| **Last reviewed** | 2026-07-27 |

## 1. Purpose

Venue halls / rooms per SIMF-FDS-004 §5.2. Halls host Sessions — the
Sessions module's hall picker (later in Sprint B) reads from
`/admin/halls/list?isActive=true`. Each hall has a stable **Code**
(e.g. "H1", "A201"), bilingual name, a numeric **Capacity** that drives
the Sessions booking cap, an optional Floor label, and free-text
equipment / accessibility notes.

## 4. UI

Canonical D-132 CRUD: SimfBanner + toolbar + grid with sortable Code /
Name / Capacity, Status pill, per-row Details / Edit / Deactivate.

**QA A40 - "Seat layout" row action.** The grid also carries a quiet-icon row action
(`SimfToolbarButton Icon="grid"`, title `Admin.Halls.Action.SeatLayout` /
"مخطط المقاعد") in the `<RowActions>` slot, before the grid's built-in
Details / Edit / Deactivate buttons. It navigates to
`/admin/halls/seat-layouts?hallId={row.Id}`, and the seat-layout editor reads that
query parameter and pre-selects the hall. Before A40 the editor was reachable only
from the side-menu item, which opens on a blank hall picker - an admin looking at a
hall had no way through to its seat map. The action is wrapped in
`<AuthorizedAction Permission="SeatLayouts.View">`, so an admin who cannot view seat
layouts is not offered a button that would only 403.

**D-356 / D-353 uniform CRUD.** The toolbar now also offers **Excel export +
import** (Export / Import → `.xlsx`, via `CrudGridExcel Resource="halls"` →
`/account/api/admin/halls/export` + `/import`, capped at 5000 rows; non-`.xlsx`
uploads are rejected with HTTP 400). Add / Edit / Details / Deactivate are framed
by **`CrudShell`** with a **Page↔Popup presentation toggle**
(`CrudPresentationToggle`, `PageKey="halls"`, persisted in localStorage
`simf.cp.prefs.halls`). Deactivate opens the read-only `HallsViewDelete` form
whose Deactivate button is gated by a **`SimfConfirm`** dialog (no more one-click
delete).

**QA B16 — hall occupancy view.** The Details / Deactivate form (`HallsViewDelete`)
now also renders **"Sessions in this hall"**: every session assigned to the hall,
with its Code, Title, **local** (Saudi +3, 12-hour) start and end, and its status
pill. It reads `GET /account/api/admin/halls/{id}/schedule` → API
`GET /admin/halls/{id}/schedule`, which reuses `AdminSessionService.ListAllAsync`
with the existing `hallId` grid filter (no second query) and is gated by the same
`Halls.View` permission the page already requires. An unbooked hall shows the
`SimfEmptyState` "No sessions are assigned to this hall." Before B16 there was no
hall-side schedule anywhere, so the "one session per hall at a time" rule only ever
surfaced as a 409 (`SESSION_HALL_TIME_OVERLAP`) from the Sessions editor.

The schedule is **active sessions only**: the endpoint pins the `isActive` grid
filter to `true`. `EnsureNoHallTimeOverlapAsync` matches on `other.IsActive`, so a
soft-deleted session is not occupancy and must not make the hall read as busy —
and the Status column shows the `SessionStatus` lifecycle, not `IsActive`, so a
leaked deactivated row would be indistinguishable from a live booking. The page is
capped at 200 rows (the `ClampPage` ceiling); when the hall's active-session
`Total` exceeds what came back, an info alert says the list is partial and points
at the Sessions grid filtered by the hall, rather than passing a truncated
schedule off as the whole occupancy.

## 4.5 Form fields

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Code | yes | 16 | 2–16; uppercased; unique |
| Name (English) | yes | 128 | 1–128 chars |
| Name (Arabic) | yes | 128 | 1–128 chars |
| Capacity | yes | n/a | integer ≥ 0 |
| Seat selection | yes | n/a | Assigned seat (pick a seat) / Open seating = general admission (D-485) |
| Floor | no | 32 | optional |
| Equipment notes | no | 1024 | optional |
| Active | (Edit only) | bool | — |

## 6. Validation + error handling

Server-side `AdminHallService.Validate`: code length, name length,
capacity non-negative, optional floor/notes length-gated; throws
bilingual `ApiException`. Duplicate code → 409 `HallCodeDuplicate`.
404 `HallNotFound`. 409 `HallInUse` when the hall is deactivated (Deactivate
action, or an edit clearing Active) while active sessions still use it — the
bilingual message names the session count (A37).

## 7. Edge cases + known limitations

- **Capacity = 0 is allowed** — useful for "overflow / TBA" placeholder
  halls that will be set later.
- **Deactivate is guarded** (A37): a hall that any active session still uses
  cannot be deactivated — 409 `HallInUse`, naming the count. Re-home or
  deactivate those sessions first. The same guard runs when the edit form
  clears the Active checkbox.
- **No drag-reorder** — sort by Code or Name from the column headers.

## 10. Use cases (UCS-001 — to author)

UC-HAL-CREATE-001, UC-HAL-EDIT-001, UC-HAL-DEACTIVATE-001, **UC-HAL-SEATLAYOUT-001**
(A40 - jump from a hall row to that hall's seat-layout editor).

## 11. E2E

[`docs/tests/e2e/cp-admin-halls.md`](../../tests/e2e/cp-admin-halls.md):
E2E-HAL-001..031 (**031** = the A40 "Seat layout" row action + its permission gate).
The deep-link is also pinned from the editor's side by E2E-HSL-024 / 025 in
[`cp-admin-halls-seat-layouts.md`](../../tests/e2e/cp-admin-halls-seat-layouts.md), and
by bUnit `tests/SIMF.ControlPanel.Tests/HallsListSeatLayoutActionTests.cs`.

## 12. Related docs

- Per-page CP documentation set (4-aspect + README, D-380):
  [`../../CP/admin-halls/README.md`](../../CP/admin-halls/README.md)
  (Function / Logic / API / Design).
- Admin Manual: `Admin-Manual.md § 5.2 Halls & seating`.
- Decisions: D-134-B2 (this commit).
- Authority spec: SIMF-FDS-004 §5.2.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-29 | D-134 Sprint B / D-135 | Original — Halls entity + EF migration `AddHalls` + canonical CRUD page. |
| 2026-06-10 | D-356 / D-353 | Uniform CRUD — added Excel export + import (`CrudGridExcel Resource="halls"`) and the Page↔Popup presentation toggle; CRUD forms hosted by `CrudShell` (`HallsAddEdit` / `HallsViewDelete`), Deactivate now gated by `SimfConfirm`. |
| 2026-07-26 | QA B16 | Hall occupancy view — `HallsViewDelete` lists the sessions assigned to the hall (code / title / local start + end / status) from the new `GET /admin/halls/{id}/schedule` (`Halls.View`, reusing `AdminSessionService.ListAllAsync`'s `hallId` filter). New E2E-HAL-026..028. |
| 2026-07-27 | QA B16 follow-up | The occupancy view pins `isActive=true`, so a soft-deleted session no longer renders as a live booking (the Status column shows the lifecycle enum, not `IsActive`); a schedule longer than one page now says it was capped. New E2E-HAL-029/030. |
| 2026-07-27 | QA A40 | **"Seat layout" row action.** A quiet-icon row action per hall opens `/admin/halls/seat-layouts?hallId={id}` with that hall pre-selected; gated by `<AuthorizedAction Permission="SeatLayouts.View">`. New resx pair `Admin.Halls.Action.SeatLayout` (EN + AR). New E2E-HAL-031. |

_Last reviewed:_ 2026-07-27 by Claude (QA B16 follow-up — active-only occupancy +
capped-page notice). Prior: 2026-07-26 by Claude (QA B16 — hall occupancy view).
Prior: 2026-06-10 by Claude (D-356 / D-353 uniform CRUD — Excel + toggle).
Prior: 2026-07-27 by Claude (QA A40 - the Halls grid now carries a "Seat layout" row action that deep-links each hall to its seat-layout editor; E2E-HAL-031).
