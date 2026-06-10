# Control Panel — Business Meetings (`/admin/business-meetings`)

> **Authority:** SIMF-FDS-013 (D-248). Admin-arranged B2B/B2C business meetings.

## Purpose

Lets a Control Panel operator schedule a **bilateral or group meeting** between two
or more parties — exhibitor/sponsor **companies** and individual **visitors** — at a
**meeting table** for a from–to time-slot. Meetings are **admin-arranged only**
(there is no attendee request queue), so each is created **Confirmed** and can be
**Cancelled**. Confirmed meetings will surface on each participant's mobile dashboard
once that aggregate read ships (the union already reserves space for them).

## Access

| | |
|--|--|
| **Permission (page)** | `BusinessMeetings.View` |
| **Schedule** | `BusinessMeetings.Schedule` |
| **Cancel** | `BusinessMeetings.Cancel` |
| **Export** | `BusinessMeetings.Export` |
| **Nav** | Programme group → "Business Meetings" |

## Layout

- **Status filter** (All / Confirmed / Cancelled) + a **Schedule meeting** button.
- **Grid:** Hall · Table · Type (B2B/B2C) · Start · End · Participants · Status · View/Cancel.
- **Schedule modal:** hall picker → table picker (tables of the chosen Meeting/General
  hall) → type (B2B/B2C, admin-set) → Start/End (UTC) → participant builder (add any
  number of Company or Visitor parties; ≥ 2, ≤ table capacity) → optional Notes.
- **Cancel modal:** optional reason. **Detail modal:** participants + slot + reason.
- **Excel export (D-356):** a toolbar **Export** action downloads the grid as an
  `.xlsx` (selected rows, or the whole filtered set when nothing is ticked) via
  `POST /api/v1/admin/business-meetings/export`. The "BusinessMeetings" sheet has
  the columns Hall · Table · Type · Start · End · Parties · Status. This page is
  **export-only** — meetings are scheduled and cancelled through the page's bespoke
  modals, so there is **no import** path. It also keeps its bespoke modals (no
  D-353 Page↔Popup presentation toggle).

## Rules (server-enforced)

- ≥ 2 distinct participants, ≤ the table's capacity (`MEETING_CAPACITY_EXCEEDED`).
- The table must belong to a **Meeting** or **General** hall.
- **No table double-booking** for an overlapping slot (`BUSINESS_MEETING_TABLE_CONFLICT`).
- **No participant double-booking** across overlapping confirmed meetings
  (`BUSINESS_MEETING_PARTICIPANT_CONFLICT`).
- Cancel only a Confirmed meeting (`BUSINESS_MEETING_NOT_CONFIRMED`); the slot frees on cancel.

## Data & audit

- Tables: `BusinessMeeting`, `BusinessMeetingParticipant` on `SIMF_App`. Visitor refs
  are **bare Guids** to the Identity DB (resolved on read; no cross-DB FK, D-157/D-246);
  company refs are App FKs. Participant display names are captured as an immutable
  audit snapshot at schedule time.
- Notifications: `MeetingScheduled` / `MeetingCancelled` (in-app) to each visitor and
  to each company's `CompanyMembership` accounts.
- Audit: `BusinessMeeting.Scheduled`, `BusinessMeeting.Cancelled`.

## Tests

- Integration: `tests/SIMF.Api.Tests/BusinessMeetingsTests.cs`,
  `tests/SIMF.Api.Tests/BusinessMeetingsExcelTests.cs`
- E2E catalogue: [`e2e/cp-business-meetings.md`](../../tests/e2e/cp-business-meetings.md)

## Changelog

| Date | Change |
|------|--------|
| 2026-06-10 | D-356 — added Excel **export** (toolbar Export → `.xlsx`, "BusinessMeetings" sheet). Export-only: no import, and no D-353 Page↔Popup toggle (page keeps its bespoke schedule/cancel/detail modals). |

---

_Last reviewed:_ 2026-06-10 (D-356 Phase 5 — Excel export added; export-only).
