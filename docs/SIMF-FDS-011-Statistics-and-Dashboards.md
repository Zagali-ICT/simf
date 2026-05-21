# Feature Design Specification — Statistics and Dashboards

| Field | Value |
|-------|-------|
| Document ID | SIMF-FDS-011 |
| Title | Feature Design Specification — Statistics and Dashboards |
| Version | 1.0 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Product Owner |
| Date issued | 2026-05-20 |
| Related documents | SIMF-FDS-003, SIMF-FDS-007, SIMF-SRS-001, SIMF-UCS-001, SIMF-DAT-001, SIMF-CON-001, SIMF-CPD-001 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. The statistics and dashboards feature, build-ready. |

---

## 1. Purpose

This is the build-ready specification for statistics and the Control Panel
dashboard — the figures the organisers watch before and during the forum, and
the live picture of who is in the venue.

## 2. Scope

The feature covers:

- the Control Panel dashboard,
- the per-day statistics,
- the overall statistics,
- live attendance,
- GPS-presence tracking — movement, dwell time and routes inside the venue,
- how the figures are computed.

It does **not** produce the raw events the figures count — registrations,
badges, check-ins, questions come from their own features. This feature
**reads** them and presents the totals. The exact figure list is **proposed
here for the client to review** (decision D6, open item OI-1).

## 3. Requirements and use cases covered

| From SIMF-SRS-001 | From SIMF-UCS-001 |
|-------------------|-------------------|
| FR-1101 per-day statistics | UC-30 View the statistics dashboard |
| FR-1102 overall statistics | UC-30 |
| FR-1103 GPS-presence tracking and live attendance | UC-30 |
| FR-1104 the figure list confirmed per decision D6 | (open item OI-1) |

## 4. Feature overview

```
Registration · Badge & Access · Engagement · Programme  (the owning features)
        │  raw records
        ▼
  Statistics service ── computes ──▶ StatisticSnapshot
        │
        ▼
  Control Panel dashboard ── per-day · overall · live attendance · GPS presence
```

## 5. Detailed behaviour

### 5.1 The dashboard

- The Control Panel Dashboard page shows the statistics, grouped and readable,
  to any user holding the Dashboard page (every organising role in the
  suggested configuration).
- Figures that are live — attendance, check-ins — refresh live over SignalR;
  the rest refresh on a regular cycle.

### 5.2 Per-day statistics

For each forum day the dashboard shows (FR-1101, SIMF-CON-001 section 7.10):

- registrations recorded,
- badges printed,
- registered VIP count,
- media badges printed,
- total check-ins,
- the approximate total of entries.

### 5.3 Overall statistics

Across the forum the dashboard shows (FR-1102):

- the count of themes, topics and speakers,
- the count of participating countries,
- total registrations and total badges printed,
- student badges printed,
- check-ins per day and the total attendance,
- total broadcast hours,
- the total of audience questions.

### 5.4 Live attendance

- Live attendance is built from the venue-entry and hall-arrival records
  produced by Badge & Access Control (SIMF-FDS-003): how many people are in the
  venue, and in each hall, right now.
- The live-attendance panel updates over SignalR as scans and geofence events
  arrive.

### 5.5 GPS-presence tracking

- The system tracks attendee movement inside the venue from GPS presence
  (FR-1103): where attendees are, how long they dwell, and the routes they
  take.
- This is shown on the dashboard as movement and dwell-time views, within the
  Statistics context.
- GPS-presence data is **sensitive personal data**; section 10 covers its
  handling.

### 5.6 How the figures are computed

- The Statistics context **owns no source-of-truth records**; it reads from the
  other contexts (SIMF-SAD-001 section 5.1).
- A figure that is heavy to compute is calculated on a cycle and stored as a
  `StatisticSnapshot`, so the dashboard reads a snapshot rather than
  recomputing on every view. Live figures are computed close to real time.
- The figures are derived, never hand-entered; the only exception the source
  material notes is student badges printed outside the Control Panel, which is
  recorded as a figure to confirm (OI-1).

## 6. Data

The feature uses `StatisticSnapshot` and `GpsPresence` from SIMF-DAT-001 section
5.11. It reads, across contexts: `RegistrationRequest`, `Badge`, `VenueEntry`,
`HallAttendance`, `Session`, `SessionQuestion`, `Theme`, `Speaker` and the
`Category` data — through each owning context's application service, not by
cross-context queries.

## 7. User interface

| Surface | Screens |
|---------|---------|
| Control Panel | The Dashboard page — statistic cards, the per-day and overall figures, the live-attendance panel, and the GPS-presence movement views, per SIMF-CPD-001 section 13.6 |

The dashboard follows SIMF-CPD-001; all labels are localised, Arabic and
English; numbers follow the locale formatting in SIMF-MAA-001 (dates
`dd-MM-yyyy`, Latin digits); loading and error states are present.

## 8. Validation rules

| Item | Rule |
|------|------|
| Figures | Derived from the owning contexts; not hand-entered (except where OI-1 confirms an exception) |
| Snapshot | Each `StatisticSnapshot` records its scope, day, metric and value |
| Live panels | Reflect the live records; a stale panel shows its last-updated time |

## 9. Acceptance criteria

1. The dashboard shows the per-day statistics for each forum day.
2. The dashboard shows the overall statistics across the forum.
3. Live attendance — venue and per hall — updates live as scans and geofence
   events arrive.
4. GPS-presence movement and dwell-time views are shown.
5. Heavy figures are read from a snapshot, not recomputed on every view; live
   figures are close to real time.
6. Figures are derived from the owning contexts and are consistent with them.
7. The dashboard is permission-controlled and shown only to roles with the
   Dashboard page.
8. The dashboard renders in Arabic (RTL) and English (LTR); no hardcoded text.
9. The build is clean and the feature has unit, integration and end-to-end
   tests that pass.

## 10. Security and privacy considerations

- The dashboard is permission-controlled.
- GPS-presence data is **sensitive personal data**: it is collected only with
  the attendee's location permission, used only for attendance and movement
  statistics, encrypted at rest, and kept under a retention rule confirmed with
  the owner (SIMF-DAT-001 OI-3, SIMF-FDS-003 section 9).
- Movement and dwell-time views are shown to organisers in aggregate where
  individual tracking is not needed.
- Statistics are read-only on the dashboard; there is no figure a user edits.

## 11. Test scenarios

| # | Scenario | Expected |
|---|----------|----------|
| T-01 | Open the dashboard | per-day and overall statistics shown |
| T-02 | A badge is scanned at venue entry | live attendance rises |
| T-03 | An attendee enters and leaves a hall | the hall's live count updates both ways |
| T-04 | View the per-day figures across the three forum days | each day's figures shown |
| T-05 | View the overall figures | themes, speakers, countries, attendance, broadcast hours, questions |
| T-06 | View GPS-presence movement and dwell views | movement and dwell time shown |
| T-07 | A heavy figure is requested twice | the second read uses the snapshot |
| T-08 | A non-Dashboard role opens the dashboard route | access is refused |
| T-09 | Location permission denied for an attendee | their device contributes no GPS-presence data |
| T-10 | Render the dashboard in Arabic and English | correct layout, direction and number formatting |

## 12. Open items

| ID | Item | Affects |
|----|------|---------|
| OI-1 | Client confirmation of the exact statistics list and which figures appear on which dashboard (decision D6, FR-1104) | Sections 5.2, 5.3 |
| OI-2 | Confirm the source of "student badges printed outside the Control Panel" — derived or entered | Section 5.6 |
| OI-3 | Confirm the GPS-presence retention rule with the owner (SIMF-DAT-001 OI-3) | Section 10 |
| OI-4 | Confirm the refresh cycle for non-live snapshot figures | Section 5.6 |
| OI-5 | Confirm document classification with the owner | Control block |

---

End of document.
