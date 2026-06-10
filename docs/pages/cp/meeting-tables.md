# Control Panel — Meeting Tables & Hall Allocation (`/admin/meeting-tables`)

> **Authority:** SIMF-FDS-013 (D-248). Flexible hall configuration.

## Purpose

Configures a hall for its role and reserves its space. An operator picks a hall and:

1. **Sets the hall purpose** — `General` / `Booth` / `Session` / `Meeting`. General is
   the default every pre-existing hall carries (un-specialised; may host anything).
2. **Defines / generates meeting tables** (for Meeting/General halls) — one-by-one, or
   in bulk **random-by-count** (auto-coded `T-001…`, stopping at the hall capacity) or
   **by row/column** from a CSV spec (e.g. `A1,A2,B3`), with an optional **Reset**
   that clears the hall's existing tables first.
3. **Reserves hall space** over a from–to time-slot by **whole** / **random-by-count**
   / **row/column** — the flexible allocation layer that keeps the same hall from
   being double-used across sessions, booths and meetings.

**Excel export (D-356).** The meeting-tables grid's toolbar carries an **Export**
action that downloads the selected hall's tables as an `.xlsx`
(`Code | Row | Column | Capacity`). With no rows selected it exports the hall's
current filtered set (the request rides the selected `hallId` in the query
filter); with rows ticked it exports just those, capped at 5000 rows. Export is
the only Excel direction on this page — there is **no** import, and the page does
**not** carry the D-353 Page↔Popup presentation toggle (the three forms stay
modal, and delete/release use the native confirm prompt).

## Access

| | |
|--|--|
| **Permission (page)** | `MeetingTables.View` |
| **Tables (add/edit/delete/generate)** | `MeetingTables.Edit` |
| **Allocations (reserve/release)** | `HallAllocations.View` / `HallAllocations.Edit` |
| **Set purpose** | `Halls.Edit` |
| **Nav** | Programme group → "Meeting Tables" |

## Rules (server-enforced)

- Meeting tables require a **Meeting** or **General** hall (`HALL_NOT_MEETING_PURPOSE`).
- Table code is 1–16 chars, unique among active tables in the hall
  (`MEETING_TABLE_CODE_DUPLICATE`); capacity 2–100.
- A table with upcoming confirmed meetings cannot be deleted (`MEETING_TABLE_INVALID`).
- A hall slot cannot be double-reserved — overlapping allocations are rejected
  (`HALL_ALLOCATION_OVERLAP`); random allocation needs a positive count, row/column a spec.

## Data & audit

- Tables: `MeetingTable`, `HallAllocation` on `SIMF_App`; `Hall.Purpose` column.
- Audit: `Hall.PurposeChanged`, `MeetingTable.Created/Updated/Deactivated/Generated`,
  `HallAllocation.Created/Released`.

## Tests

- Integration: `tests/SIMF.Api.Tests/BusinessMeetingsTests.cs`
- E2E catalogue: [`e2e/cp-meeting-tables.md`](../../tests/e2e/cp-meeting-tables.md)

## Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-10 | D-356 / D-353 | Tables grid gained an Excel **Export** action (export-only; no import). The D-353 Page↔Popup presentation toggle was **not** added to this page. |

_Last reviewed:_ 2026-06-10 (D-356 Phase 5 — Excel export).
