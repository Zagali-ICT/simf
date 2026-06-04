# Halls & seating — `/admin/halls`

| | |
|--|--|
| **Route** | `/admin/halls` |
| **Audience** | Administrator |
| **Auth** | `[Authorize(Roles = "Administrator")]` + `RequireApprovedAccount` + `RequireRateLimiting("auth")` (mutations) |
| **Pattern** | D-117 + D-132 canonical CRUD. |
| **Status** | ✅ Real (D-134 Sprint B / D-135) |
| **Backend endpoints** | `POST /account/api/admin/halls/list`, `GET /admin/halls/{id}`, `POST /admin/halls`, `PUT /admin/halls/{id}`, `DELETE /admin/halls/{id}` |
| **Source** | [`HallsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/HallsList.razor), [`HallForm.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/HallForm.razor), [`AdminHallService`](../../../src/Backend/SIMF.Infrastructure/Programme/AdminHallService.cs), [`Hall`](../../../src/Backend/SIMF.Domain/Programme/Hall.cs) |
| **Backed by** | **New** `dbo.Halls` table (migration `AddHalls`, 2026-05-28). |
| **Tests** | [`docs/tests/e2e/cp-admin-halls.md`](../../tests/e2e/cp-admin-halls.md) |
| **Last reviewed** | 2026-05-29 |

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

## 4.5 Form fields

| Field | Required | MaxLength | Validation |
|-------|----------|-----------|------------|
| Code | yes | 16 | 2–16; uppercased; unique |
| Name (English) | yes | 128 | 1–128 chars |
| Name (Arabic) | yes | 128 | 1–128 chars |
| Capacity | yes | n/a | integer ≥ 0 |
| Floor | no | 32 | optional |
| Equipment notes | no | 1024 | optional |
| Active | (Edit only) | bool | — |

## 6. Validation + error handling

Server-side `AdminHallService.Validate`: code length, name length,
capacity non-negative, optional floor/notes length-gated; throws
bilingual `ApiException`. Duplicate code → 409 `HallCodeDuplicate`.
404 `HallNotFound`. `HallInUse` reserved for Sessions in-use guard.

## 7. Edge cases + known limitations

- **Capacity = 0 is allowed** — useful for "overflow / TBA" placeholder
  halls that will be set later.
- **Deactivate is unconditional** in Sprint B. When Sessions ships, the
  flow will refuse to deactivate a hall that any active session uses;
  `HallInUse` is reserved for that.
- **No drag-reorder** — sort by Code or Name from the column headers.

## 10. Use cases (UCS-001 — to author)

UC-HAL-CREATE-001, UC-HAL-EDIT-001, UC-HAL-DEACTIVATE-001.

## 11. E2E

[`docs/tests/e2e/cp-admin-halls.md`](../../tests/e2e/cp-admin-halls.md):
E2E-HAL-001..007.

## 12. Related docs

- Admin Manual: `Admin-Manual.md § 5.2 Halls & seating`.
- Decisions: D-134-B2 (this commit).
- Authority spec: SIMF-FDS-004 §5.2.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-29 | D-134 Sprint B / D-135 | Original — Halls entity + EF migration `AddHalls` + canonical CRUD page. |

_Last reviewed:_ 2026-05-29 by Claude (D-134 Sprint B / D-135).
