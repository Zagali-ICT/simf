# Feature Design Specification — Badge and Access Control

| Field | Value |
|-------|-------|
| Document ID | SIMF-FDS-003 |
| Title | Feature Design Specification — Badge and Access Control |
| Version | 1.0 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Product Owner |
| Date issued | 2026-05-20 |
| Related documents | SIMF-FDS-002, SIMF-SRS-001, SIMF-UCS-001, SIMF-API-001, SIMF-DAT-001, SIMF-RDR-001, SIMF-CON-001 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. The badge and access-control feature, build-ready. |

---

## 1. Purpose

This is the build-ready specification for badge and access control. The
registration feature (SIMF-FDS-002) issues a badge on approval; this feature
**operates** it — it shows the badge to the attendee, verifies it at the venue,
exchanges contacts, and records arrival at the halls.

## 2. Scope

The feature covers:

- the entry badge as the attendee sees it — the badge card and its QR,
- venue entry verification, where Staff scan a badge,
- attendee-to-attendee contact exchange by scanning a badge QR,
- hall-arrival verification — a QR scan at the hall door and a GPS geofence,
  recording an enter time and a leave time per session,
- the attendance records that feed the statistics and gate session engagement.

It does **not** issue the badge — that happens at approval (SIMF-FDS-002). It
does not specify the statistics dashboards (a later SIMF-FDS) or the session
questions (the Engagement feature); it produces the attendance data those rely
on.

## 3. Requirements and use cases covered

| From SIMF-SRS-001 | From SIMF-UCS-001 |
|-------------------|-------------------|
| FR-301 the badge and its QR | UC-11 View my badge and QR |
| FR-302 the badge colour by category | UC-11 |
| FR-303 verify a badge at venue entry | UC-33 Verify a badge at venue entry |
| FR-304 attendee-to-attendee contact exchange | UC-12 Scan another attendee's badge |
| FR-305 hall-arrival verification | UC-35 Check an attendee in at a hall door |
| FR-506 session attendance from hall-arrival records | (feeds Bookings & Attendance) |

Decision **D4** governs the hall-arrival design.

## 4. Feature overview

```
Badge issued (FDS-002)
        │
        ├─▶ Attendee views the badge + QR in the app
        │
        ├─▶ Staff scan the badge at venue entry  ──▶ VenueEntry recorded
        │
        ├─▶ Attendee scans another badge        ──▶ SavedContact recorded
        │
        └─▶ Hall arrival: QR at the door  +  GPS geofence
                    │
                    └─▶ HallAttendance: enter time, leave time
```

## 5. Detailed behaviour

### 5.1 The badge

- Every Approved user holds one `Badge` (issued by SIMF-FDS-002): a unique
  reference number `SIMF-2026-xxxx`, a QR payload, and a colour from the user's
  category.
- The app shows the badge as a card — a category-coloured strip, the user's
  photo, name and organisation, the QR, and the reference number (mockup
  Screen 32).
- The **QR payload is a signed token**, not the bare reference. It binds the
  badge reference and is verified server-side, so a copied or invented QR fails
  verification. The signing approach is confirmed in the low-level design
  (OI-1).

### 5.2 Venue entry verification

- A Staff user, signed in to the mobile app with field permissions, scans an
  attendee's badge QR or barcode at a venue entrance (`UC-33`).
- The system verifies the QR token, checks the badge is active and its holder
  is **Approved**, and records a `VenueEntry` — the scan time, the gate, and
  the direction.
- The Staff screen shows a clear success or failure result.
- **Failure:** an invalid or tampered QR, an inactive badge, or a holder who is
  not Approved → a clear failure; the attendee is directed to the registration
  desk (the on-site flow in SIMF-FDS-002 section 7).

### 5.3 Attendee-to-attendee contact exchange

- An attendee opens the **Scan** tab on the badge screen and scans another
  attendee's badge QR (`UC-12`, mockup Screen 32).
- The system verifies the scanned QR and records a `SavedContact` linking the
  scanning user to the scanned user.
- The scanning attendee can then see the saved contact in their contacts list.
- A QR that fails verification does not create a contact; the user is told.

### 5.4 Hall-arrival verification

Hall arrival is recorded by **two means together**, per decision D4:

1. **QR scan at the hall door.** A Staff user, or a device at the door, scans
   the attendee's badge as they enter a session hall (`UC-35`). This records a
   `HallAttendance` row with the enter time and `Method = QrScan`.
2. **GPS geofence around the hall.** Each hall has a geofence. When the
   attendee's device crosses into the geofence, the app reports it and the
   system records the arrival with `Method = Geofence` — so an attendee who
   entered **without** scanning at the door is still recorded.

The system holds, per attendee per session, an **enter time** and a **leave
time**. The leave time is set when the attendee leaves the geofence, or at
session end. If both means fire for the same attendee and session, they update
the one `HallAttendance` row rather than creating two.

### 5.5 What the attendance records feed

- **Statistics.** `HallAttendance` and `VenueEntry` feed the attendance figures
  and the live-attendance dashboard (the Statistics feature).
- **Engagement gating.** A session's questions open for an attendee only after
  that attendee has a `HallAttendance` enter record for the session (decision
  D5). This feature produces that record; the Engagement feature reads it.

## 6. Data

The feature uses these entities from SIMF-DAT-001 section 5.3: `Badge`,
`VenueEntry`, `HallAttendance`, `SavedContact`. It reads `User`, `Hall` and
`Session`. `HallAttendance` is constrained so an attendee has one open
attendance row per session at a time (SIMF-DAT-001 section 8).

Each hall needs a stored **geofence** — a centre and radius, or a polygon. This
is an addition to the `Hall` entity and is recorded as open item OI-2 against
SIMF-DAT-001.

## 7. User interface

| Surface | Screens |
|---------|---------|
| Mobile app | Screen 32 — the badge card (the "My badge" tab) and the QR scanner (the "Scan" tab); the saved-contacts list |
| Mobile app (Staff) | The venue-entry scanner and the hall-door scanner — the field-operations tools |
| Control Panel | No screen of its own; entry and attendance data appear on the Statistics dashboard |

Mobile visuals are the external designer's. Every screen has loading and error
states; a scan result is shown clearly as success or failure; all text is
localised, Arabic and English.

## 8. Validation rules

| Item | Rule |
|------|------|
| Badge QR | Must be a valid signed token; verified server-side |
| Badge | Must be active; its holder must be Approved |
| Venue entry | A scan records the time, gate and direction |
| Hall geofence | Each hall has a defined geofence before the event |
| Hall attendance | One open row per attendee per session; enter precedes leave |

## 9. Security and privacy considerations

- The badge QR is a **signed token**; a copied or forged QR fails verification.
  The badge reference alone is never trusted.
- Every verification — venue entry, hall arrival — is checked server-side; the
  client never decides validity.
- **Location is sensitive personal data.** GPS geofence data is collected only
  for attendance and presence statistics, only while the app needs it, and
  with the attendee's location permission. Its retention rule is confirmed with
  the owner — open item OI-3 — and it is encrypted at rest like other personal
  data (NFR-11).
- An attendee-to-attendee contact exchange records only what the badge already
  shows; it does not expose data the scanned attendee has not put on the badge.
- Scans, entries and attendance writes are auditable through the operation log
  where they are organiser actions.

## 10. Acceptance criteria

1. An Approved user sees their badge card with the category colour, their
   details, the QR and the reference number.
2. A Staff user can scan a badge at venue entry; a valid badge of an Approved
   holder records a `VenueEntry` and shows success.
3. A forged, tampered or inactive badge, or a non-Approved holder, fails entry
   verification with a clear message.
4. An attendee can scan another attendee's badge and save them as a contact;
   an invalid QR saves nothing.
5. A QR scan at a hall door records a `HallAttendance` enter with
   `Method = QrScan`.
6. Crossing a hall geofence records an arrival with `Method = Geofence`, even
   with no door scan.
7. The system holds an enter time and a leave time per attendee per session,
   in a single attendance row.
8. The attendance records are available to the statistics and to the engagement
   question-gating.
9. Location is collected only with permission and only while needed.
10. All screens render in Arabic (RTL) and English (LTR); no hardcoded text.
11. The build is clean and the feature has unit, integration and end-to-end
    tests that pass.

## 11. Test scenarios

| # | Scenario | Expected |
|---|----------|----------|
| T-01 | Approved user opens the badge screen | badge card with colour, details, QR, reference |
| T-02 | Staff scan a valid badge at entry | `VenueEntry` recorded; success shown |
| T-03 | Staff scan a forged or copied QR | verification fails; no entry; clear message |
| T-04 | Staff scan a badge of a non-Approved holder | entry refused; directed to the desk |
| T-05 | Attendee scans another attendee's badge | `SavedContact` created; contact visible |
| T-06 | Attendee scans an invalid QR for contact | nothing saved; user told |
| T-07 | QR scan at a hall door | `HallAttendance` enter, `Method = QrScan` |
| T-08 | Attendee enters a hall geofence without scanning | arrival recorded, `Method = Geofence` |
| T-09 | Attendee leaves the geofence / session ends | leave time set on the same row |
| T-10 | Both door scan and geofence fire for one session | one attendance row, not two |
| T-11 | Engagement checks attendance before opening questions | questions open only with an enter record |
| T-12 | Location permission denied | no geofence data; the door scan still works |
| T-13 | Render the badge and scanner screens in Arabic and English | correct layout and direction; no hardcoded text |

## 12. Open items

| ID | Item | Affects |
|----|------|---------|
| OI-1 | Confirm the QR signed-token scheme in the low-level design | Section 5.1 |
| OI-2 | Add a geofence (centre/radius or polygon) to the `Hall` entity in SIMF-DAT-001 | Section 6 |
| OI-3 | Confirm the retention rule for GPS / geofence data with the owner (SIMF-DAT-001 OI-3) | Section 9 |
| OI-4 | Confirm whether hall-door scanning is done by Staff, a fixed device, or both | Section 5.4 |
| OI-5 | Confirm document classification with the owner | Control block |

---

End of document.
