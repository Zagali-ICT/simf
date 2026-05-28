# Feature Design Specification — Badge and Access Control

| Field | Value |
|-------|-------|
| Document ID | SIMF-FDS-003 |
| Title | Feature Design Specification — Badge and Access Control |
| Version | 1.1 |
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
| 1.1 | 2026-05-21 | Engineering & Architecture Team | Architecture-review amendment (see Amendment A): GPS-presence reporting interval and batched writes. |

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

### 5.6 Gate Module — venue access gate engine

The Gate Module operationalises §5.2 (venue entry verification) into a
first-class entity. A `Gate` is a configured point of access — venue main
entrance, hall door, VIP lounge door, anything that needs a controlled
scan check. Each gate has a `DirectionMode` (`In` / `Out` / `Both`), an
optional allow-list of `ProfileType`s, and a set of assigned operators.

#### 5.6.1 Constraint engine — 13 ordered steps

Every `POST /api/v1/gates/{gateId}/scans` runs through the engine. The
order is load-bearing: each step short-circuits to a recorded denial with
the named `DenialReasonCode`. Steps 9.5 and 11.5 are **reserved hooks** —
they are no-ops in this increment and become enforcement points when the
time-window and booking-required features ship.

| Step | Check | Denial code on failure |
|------|-------|------------------------|
| 1 | Caller is authenticated | (HTTP 401 — not recorded) |
| 2 | Caller has `Gates.Operate` and is an active assignee of this gate | `GATE_OPERATOR_NOT_ASSIGNED` (HTTP 403 — not recorded) |
| 3 | `IQrResolver` resolves the QR to a `UserProfile` | `QR_UNKNOWN` (recorded) |
| 4 | Idempotency-key check — replay returns prior outcome; conflict → 409 | `IDEMPOTENCY_KEY_CONFLICT` (HTTP 409 — not recorded) |
| 5 | Gate is `IsActive = true` | `GATE_INACTIVE_AT_SCAN` (recorded; pre-engine 503 also possible) |
| 6 | Holder account state is `Approved` | `HOLDER_NOT_APPROVED` (recorded) |
| 7 | Holder account state is not `Disabled` | `HOLDER_DISABLED` (recorded) |
| 8 | Holder account state is not `Locked` | `HOLDER_LOCKED` (recorded) |
| 9 | Holder's `ProfileType` is `IsActive = true` | `PROFILE_TYPE_INACTIVE` (recorded) |
| 9.5 | **Reserved** — time-window check (plan §11.2) | `OUTSIDE_TIME_WINDOW` (recorded; never fires in this increment) |
| 10 | Resolve direction. `DirectionMode = In` → `CheckIn`. `DirectionMode = Out` → `CheckOut`. `DirectionMode = Both` → **infer** from the visitor's last allowed scan **at this gate**: cold start = `CheckIn`; otherwise the opposite of the last allowed direction at this gate. (No denial; this is the inference step.) | — |
| 11 | If the gate has a non-empty `GateProfileTypeAllow` list filtered by active `ProfileType`, the holder's `ProfileTypeId` must be in the list. Empty allow-list = pass (general gate). Filtered-empty (allow-list referenced only inactive profile types) = deny **all** per the safe-default rule. | `PROFILE_TYPE_NOT_ALLOWED` (recorded) |
| 11.5 | **Reserved** — booking-required check (plan §11.3) | `BOOKING_REQUIRED_MISSING` (recorded; never fires in this increment) |
| 12 | Look up the prior allowed scan within the **5-second duplicate window** keyed `(GateId, UserProfileId)` (without `Direction`, so a `Both`-mode race between two devices reading the same QR at the same instant produces one row). If found, return the prior outcome (replay path) — record nothing new. | — (duplicate absorption; no new row) |
| 13 | Persist the `GateScan` row with `Outcome = Allowed`, the resolved `Direction`, the server clock `ScannedAtUtc`, and (when the client supplied them) `ClientScannedAtUtc` + `IdempotencyKey`. On a denial path, persist with `Outcome = Denied` + the named `DenialReasonCode`. **Audit:** every denial also emits one `OperationLog` row (`EventType = GateScanDenied`) so SOC can surface denials without scanning the `GateScan` firehose. Successful scans are recorded only in `GateScan` to keep operation-log volume sane. | — |

The engine is implemented as an ordered pipeline. The two reserved hooks
(9.5, 11.5) are present as no-op delegates in this increment so the
future increments plug in without renumbering or branching the engine.

#### 5.6.2 VIP / Normal rejection

Each gate has its own `GateProfileTypeAllow` list. The Control Panel
management page presents this as a chip-picker over active `ProfileType`s.

| Configuration | Effect on step 11 |
|---------------|-------------------|
| **Empty list** (default for new gates) | All profile types allowed — general entrance gate, pass |
| `{ VVIP, VIP }` | Only VVIP and VIP visitors pass; everyone else → `PROFILE_TYPE_NOT_ALLOWED` |
| `{ VIP }` where VIP is later deactivated by an admin | Filtered list becomes empty → the engine **denies all scans** on this gate (safe-default rule L-15: better to deny visibly than silently flip a VIP gate to "everyone") |

On a `PROFILE_TYPE_NOT_ALLOWED` denial, the operator console shows:

- a red denial card;
- the visitor's actual profile-type chip rendered with the
  `ProfileType.PageColor` (so the operator can see what the visitor
  presented);
- the gate's allowed types in the page header (so the operator can
  verbally redirect: "please use Gate B — VIP only on this door");
- a bilingual message — EN "This gate is for VIP / VVIP guests." / AR
  "هذه البوابة لضيوف VIP / VVIP فقط.".

#### 5.6.3 Multiple-in / multiple-out behaviour

| Mode | Behaviour on a second scan in the same direction |
|------|---------------------------------------------------|
| `Both` | Impossible by construction — the engine infers direction (step 10) |
| `In` | Every scan is a `CheckIn`; multiple check-ins are legitimate (visitor left through another gate, came back). Each is its own row. |
| `Out` | Symmetric — every scan is a `CheckOut` |

The 5-second duplicate window (step 12) absorbs operator double-tap and
barcode-reader double-fire. Beyond 5 s, a fast follow-up is recorded as a
legitimate event.

#### 5.6.4 "Currently inside" derivation

The "currently inside" view is computed on demand from the **most recent
allowed scan across all gates** for each visitor — not per-gate:

- Most recent allowed scan is `CheckIn` → visitor is inside.
- Most recent allowed scan is `CheckOut`, or no scan exists → visitor is outside.

The filtered index `(UserProfileId, ScannedAtUtc DESC) WHERE Outcome =
Allowed AND UserProfileId IS NOT NULL` (SIMF-DAT-001 §5.3.2) makes this a
single-row seek per visitor even at expected event-end volumes (low
millions of scans). If reporting load proves the index insufficient, a
materialised `VisitorPresence` table is the documented fallback (plan
OI-5).

#### 5.6.5 Idempotency contract

Every `POST /scans` may carry an `Idempotency-Key` UUIDv4 on the header
or in the body (header wins). The store `ScanIdempotency(Key, GateId,
RequestHash, ResponseHash, StoredAt)` keeps records 24 hours. Replay
returns the original response with `X-Idempotent-Replay: true`. Same
key with a different `(qr, gateId)` → **409
IDEMPOTENCY_KEY_CONFLICT**. Requests without a key are accepted (the
5-second duplicate window still protects against double-fire), but the
offline drain path expects keys and the device-side flow generates one
per scan.

#### 5.6.6 Failure-rate circuit

`GateScan` denials are tracked in a rolling 60-second window per gate.
At ≥ 10 denials in 60 s the circuit **opens** for 5 minutes — further
scans on that gate are rejected with **429
GATE_FAILURE_CIRCUIT_OPEN** + `X-Gate-Failure-Circuit: open`. The
circuit emits one `OperationLog` row on open and one on close, so SOC
can correlate the short outage with the underlying denial pattern. The
circuit guards against a misconfigured allow-list generating thousands
of audit-log denial rows in a panic loop.

## 6. Data

The feature uses these entities from SIMF-DAT-001 section 5.3: `Badge`,
`VenueEntry`, `HallAttendance`, `SavedContact`, `Gate`,
`GateProfileTypeAllow`, `GateAssignment`, `GateScan`, `ScanIdempotency`. It
reads `User`, `UserProfile`, `ProfileType`, `Hall` and `Session`.
`HallAttendance` is constrained so an attendee has one open attendance row per
session at a time (SIMF-DAT-001 section 8). `GateScan` is append-only with an
INSTEAD-OF UPDATE/DELETE trigger refusing mutation, and opts out of the
`RowAudit` interceptor because it is itself an append-only audit log
(D-148 rationale).

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

## Amendment A — Architecture review (2026-05-21)

The 150,000-user scalability review amends this feature.

**GPS-presence as telemetry.** The mobile app reports location on a **bounded
interval** — a point every 30–60 seconds, not a continuous stream — and posts a
**small batch of points per call**, not one row per request. `GpsPresence` is
append-only telemetry on a high-insert-tuned table, isolated from the
badge-scan and booking hot paths, with a rolling retention purge (SIMF-DAT-001
Amendment A.2). Hall-arrival detection still uses the QR scan and the geofence
as in §5.4; this amendment fixes only the write strategy and the cadence.

---

End of document.
