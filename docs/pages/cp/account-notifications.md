# Notifications inbox (CP) — `/account/notifications`

| | |
|--|--|
| **Route** | `/account/notifications` |
| **Layout** | `CpShellLayout` |
| **Audience** | Any signed-in CP user (Administrator only today; opens up when more roles ship) |
| **Auth** | `[Authorize]` |
| **Pattern** | D-117 + D-132 canonical CRUD shell. Reached from the header bell → "View all". |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/notifications/list`, `DELETE /account/api/notifications/{id}`, `POST /account/api/notifications/read-all` |
| **Source** | [`Notifications.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Account/Notifications.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Per-user notification inbox. The header bell shows the unread count + a
small menu with the latest few; **View all** lands here. The page lists
every notification, lets the user open Details, delete one, bulk-dismiss a
selection, and mark all as read. New (unread) notifications carry a small
"New" pill in the Title column.

## 4.2 Toolbar + actions

| Affordance | Behaviour |
|------------|-----------|
| Select all (toolbar) | Tick every row on the current page |
| Delete (toolbar, when selection) | Bulk-dismiss the selected rows (loops per-row delete — no bulk endpoint) |
| Per-row ⓘ Details | Modal with Title / Body / Severity / Received timestamp |
| Per-row 🗑 Delete | Single dismiss |
| Mark all read (button below the grid) | `POST /notifications/read-all` — flips every unread notification to read |

## 7. Edge cases

- **Empty** → `SimfEmptyState` with "No notifications." copy.
- **Bilingual title/body** → `TitleFor` / `BodyFor` helpers pick the
  current culture's variant.
- **Bulk-dismiss latency** — for N selected rows, fires N sequential
  delete requests. Acceptable because selection caps at the visible page
  (≤ 25 by default).
- **No bulk-read endpoint** — Mark-all-read covers the read-state op;
  bulk-delete is for actual dismissal.

## 11. E2E

| Scenario | ID |
|----------|----|
| Default render with mix of read + unread (New pill on unread) | E2E-NTF-001 |
| Per-row Details modal shows all fields | E2E-NTF-002 |
| Per-row Delete removes the row | E2E-NTF-003 |
| Select 3 + Delete → 3 rows dismiss + bulk-dismiss toast | E2E-NTF-004 |
| Mark all read → all New pills vanish | E2E-NTF-005 |
| Empty inbox → SimfEmptyState | E2E-NTF-006 |
| RTL | E2E-NTF-007 |

## 12. Related

- Decisions: D-053 (original), D-132 (canonical sweep — multiselect, Details modal, full pager, bulk-dismiss loop).
- Bell consumer: lives in `CpShellLayout` header.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-?? | D-053 | Original implementation. |
| 2026-05-28 | D-132 | Banner / Multiselect / Details modal / EmptyTemplate / full pager added; OnDeleteSelected wired as loop-dismiss. |

## D-809 — dismiss confirms first

The grid trash icon and the bulk-dismiss toolbar button both used to delete on
the first click (`SimfDataGrid` carries no built-in confirmation). Both now
stage a `SimfConfirm`: the single dialog names the notification, the bulk one
names the count. Nothing is sent until the operator accepts.

_Last reviewed:_ 2026-07-30 by Claude (D-809 destructive-action safety).
