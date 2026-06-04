# SIMF Gate Module — Design Notes

| Field | Value |
|-------|-------|
| Document ID | SIMF-Gate-Module-Design-Notes |
| Title | Gate Module — Design Notes |
| Version | 1.0 |
| Status | Approved (pending kickoff) |
| Classification | Confidential |
| Prepared by | SIMF Engineering Team |
| Owner | SIMF Programme Owner |
| Approver | SIMF Programme Owner |
| Date issued | 2026-05-29 |
| Related documents | SIMF-Gate-Module-Plan (the increment plan this expands on), SIMF-API-001 (API conventions), SIMF-DAT-001 §5.3 (data model), SIMF-FDS-003 (Badge & Access Control), CLAUDE.md (project rules) |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-29 | SIMF Engineering Team | First issue. Captures the four operational design questions raised during plan approval and the agreed answers, with a clean split between what this increment ships and what the next increments will add. |

---

## 1. Purpose

This document is a companion to `SIMF-Gate-Module-Plan.md`. The plan is the locked engineering contract; this document is the design narrative behind four operational concerns raised during plan approval:

1. How are gates defined, linked to halls and time schedules, and assigned to devices or staff?
2. How are check-in / check-out scans stored, and how are multiple-in / multiple-out events handled?
3. How are VIP / Normal rejections managed?
4. How does the system work on a device that has no network — offline first, then sync?

Each section is split into "Today (this increment)" and "Next increment(s)" so the boundary between what ships now and what comes later is explicit.

## 2. Defining gates, linking to halls and schedules, assigning to devices or staff

### 2.1 Today — this increment

A gate is one row in the `Gate` table. An administrator opens the master-detail page at `/admin/gates` and per gate sets:

| Field | What it does |
|-------|--------------|
| `Code`, `NameEn`, `NameAr` | Identity and bilingual display label. |
| `DirectionMode` | `In` / `Out` / `Both`. Drives the operator console UI: one fixed-direction button for `In`/`Out`, a single Scan button with server-side direction inference for `Both`. |
| Allowed profile types | Chip-picker pulling from active `ProfileTypes`. Empty list = all allowed (general gate). Non-empty list = restricted — see §4 below. |
| Assigned operators | User picker. Stored as `GateAssignment(GateId, UserId, IsActive)` — many-to-many so an operator can rotate across gates. When an operator opens `/admin/gates/operator`, the system reads their active assignments and either auto-selects (one assignment) or asks them to pick once per shift (multiple). |
| `IsActive` | Soft-delete via `Deactivate()`. |

The relationship between a gate and an employee/staff member is therefore the `GateAssignment` table. There is no single `GateId` column on `SimfUser` — assignments are first-class records, audited, with their own activation lifecycle, so admins can rotate operators between gates and the history survives.

### 2.2 Next increment — Programme & Session

Plan §11.2 reserves the seam. A new `Hall` entity is introduced; `Gate` gets a nullable `HallId`. A hall gate inherits its time schedule from the sessions in that hall via a new `GateTimeWindow` table with three kinds:

```
GateTimeWindow {
    Id, GateId, Kind,
    StartsAtUtc?, EndsAtUtc?,
    SessionId?, Note?
}

Kind:
    Fixed             — explicit clock window (e.g. 08:00–10:00 every day)
    DuringSession     — resolves at scan time from Session.StartAt..EndAt
    BetweenSessions   — resolves to "from end of session A to start of session B" in the same hall
```

The constraint engine plugs the time check in at step 9.5 (reserved in this increment's engine). Empty list = always open. When an admin schedules a session at 10:00–11:00 in Hall A and a `DuringSession` window is bound to that session for Gate-A1, the gate accepts scans automatically during 10:00–11:00; reschedule the session and the window follows — no manual edit.

### 2.3 Future — Device identity (kiosks / turnstiles)

Plan §11.5 reserves a `GateDevice { Id, Name, GateId, ApiKeyHash, IsActive, LastSeenUtc }` entity. A device authenticates with a long-lived API key (not a JWT), is bound to exactly one gate (`GateId`), and bypasses the operator-assignment check (replaced by the device-to-gate binding). Devices send `Source = Kiosk` on every scan. The scan endpoint and the constraint engine are unchanged; only the authentication path differs.

The API contract was designed so the staff app and the kiosk app both post to the same `POST /api/v1/gates/{gateId}/scans` endpoint. No new endpoint is needed when devices arrive.

## 3. Storing check-in / check-out logs, and handling multiple-in / multiple-out

### 3.1 Storage

Every scan is one row in `GateScan` — append-only, an INSTEAD-OF UPDATE/DELETE trigger refuses any mutation, and the table opts out of `RowAudit` because it is itself an audit log.

| Column | Notes |
|--------|-------|
| `Id` | `bigint IDENTITY` PK — monotonic, no fragmentation under high insert load. |
| `GateId` | Which gate. |
| `UserProfileId` | Nullable — null when the QR resolved to nothing (denied `QR_UNKNOWN`). |
| `QrIdAtScan` | The 12-char QR exactly as scanned. Forensic value — survives QR rotation. |
| `Direction` | `CheckIn` / `CheckOut`. |
| `Outcome`, `DenialReasonCode` | `Allowed` or `Denied` plus an enum reason (`PROFILE_TYPE_NOT_ALLOWED`, `HOLDER_NOT_APPROVED`, etc.). |
| `ScannedAtUtc` | Server clock at receive. The authoritative timestamp. |
| `ClientScannedAtUtc` | Nullable — when the device recorded the scan locally. Always treated as client-asserted, never authoritative. |
| `ScannedByUserId` | The operator (or system user for a kiosk). |
| `Source` | `Simulator` (dev only), `MobileApp`, or `Kiosk`. |
| `CorrelationId`, `IpAddress`, `UserAgent` | Tie the row back to the API log; investigate abuse. |
| `IdempotencyKey` | Nullable — set when the client sent one. Enables safe retry replays. |

On any denial, an additional `OperationLog` row is written (`EventType = GateScanDenied`, `DenialReasonCode`, `CorrelationId`) so the existing SOC dashboard surfaces denials without scanning the GateScan firehose. Successful scans are recorded only in `GateScan` to keep operation-log volume sane.

### 3.2 Multiple-in / multiple-out handling

Behaviour depends on the gate's mode:

| Mode | Behaviour on a "second scan in the same direction" |
|------|----------------------------------------------------|
| `Both` | Impossible by construction. The engine **infers** direction from the visitor's last allowed scan at this gate. The next scan is recorded as the opposite direction. Cold start (no prior scan at this gate) defaults to `CheckIn`. |
| `In` | Every scan is a `CheckIn`. Multiple `CheckIn`s are legitimate (visitor left through another gate, came back). Each is its own row. |
| `Out` | Symmetric — every scan is a `CheckOut`. |

**Duplicate-absorption — 5 seconds.** If the same `(GateId, UserProfileId)` scans within 5 s of the prior allowed scan, the engine returns the existing scan id without inserting a new row. This absorbs operator double-tap and barcode-reader double-fire. The key is `(GateId, UserProfileId)` **without** Direction so that the `Both`-mode race (two devices reading the same QR at the same instant) cannot produce two duplicate rows. Beyond 5 s, a fast follow-up is a legitimate event and recorded as a separate row.

### 3.3 Computing "currently inside"

The "currently inside" view is computed on demand from the most recent allowed scan **across all gates** for each visitor — not per-gate:

- Most recent allowed scan is `CheckIn` → visitor is inside.
- Most recent allowed scan is `CheckOut`, or no scan exists → visitor is outside.

The filtered index `(UserProfileId, ScannedAtUtc DESC) WHERE Outcome = Allowed` makes this a single-row seek per visitor even at the expected event-end volume (low millions of scans). If reporting load proves the index insufficient under live conditions, a materialised `VisitorPresence` table is the documented fallback (plan OI-5).

## 4. Managing VIP / Normal rejection

### 4.1 Configuration

Each gate has its own allowed-profile-type list. On the management page, the chip-picker pulls from the active `ProfileTypes` lookup (VVIP, VIP, Gold, Silver, Staff, Exhibitor, …) and the admin picks the subset the gate accepts. The selection is stored in the `GateProfileTypeAllow(GateId, ProfileTypeId)` table.

| Configuration | Effect |
|---------------|--------|
| Empty list (default for new gates) | All profile types allowed — general entrance gate. |
| `{ VVIP, VIP }` | Only VVIP and VIP visitors pass; everyone else denied. |
| `{ VIP }` where VIP is later deactivated by an admin | Filtered list becomes empty. The engine **denies all scans** on this gate (the L-15 rule). This is the safe default — better to deny visibly than silently flip a VIP gate to "everyone". The admin sees the gate denying everyone and re-picks. |

### 4.2 The engine — step 11

1. Load the `GateProfileTypeAllow` rows for the gate.
2. Inner-join with `ProfileType` on `IsActive = true`.
3. If the unfiltered allow-list was empty → pass (all allowed).
4. If non-empty and the visitor's `UserProfile.ProfileTypeId` is not in the filtered list (including the deactivation-empties-it case) → record denial `PROFILE_TYPE_NOT_ALLOWED`.

### 4.3 Operator console feedback on a VIP-gate denial

- A red denial card.
- Shows the visitor's actual profile-type chip using `ProfileType.PageColor` so the operator can see what the visitor presented.
- Shows the gate's allowed types on the page header so the operator can verbally redirect ("please use Gate B — VIP only on this door").
- Bilingual message — for example: EN "This gate is for VIP / VVIP guests." / AR "هذه البوابة لضيوف VIP / VVIP فقط.".

### 4.4 Audit

Both rows land in the same transaction:

- `GateScan` row with `Outcome = Denied`, `DenialReasonCode = PROFILE_TYPE_NOT_ALLOWED`.
- `OperationLog` row with `EventType = GateScanDenied` and the same code.

SOC can pull "all VIP-gate denials in the last hour" by querying either side.

## 5. Offline-capable device — works without network, syncs when network returns

The API contract is designed for offline operation from day one (plan §11.4), even though the device-side queue is built later with the Flutter app.

### 5.1 Server-side primitives this increment ships (enabling offline)

| Primitive | Purpose |
|-----------|---------|
| `ClientScannedAtUtc` column on `GateScan` | When the device saw the scan locally. Stored separately, marked client-asserted, never authoritative. |
| `IdempotencyKey` column on `GateScan` + `ScanIdempotency` 24h replay store | Same key sent twice (queue drain after a 3-hour outage) writes once — replay-safe. |
| `Source = MobileApp` accepted in any environment | Distinguishes mobile-app scans from CP simulator (dev-only) and kiosk. |
| `POST /api/v1/gates/{gateId}/scans` accepts both header `Idempotency-Key` and body `idempotencyKey` | Header wins if both are present. |

### 5.2 Device-side flow — built with the Flutter app increment

```
On sign-in:
    Fetch /api/v1/gates/my-assignments          → cache locally.
    Fetch /api/v1/admin/gates/{id}              → cache gate config (name, DirectionMode, allowed profile types).
    Refresh every N minutes when online.

On each scan:
    Generate idempotencyKey = UUIDv4.
    Stamp clientScannedAtUtc = device clock UTC.

    Online?
        Yes → POST /scans now; render the server's outcome.
        No  → enqueue the scan in local SQLite with idempotencyKey + clientScannedAtUtc.
              Run a local constraint check against cached gate config
                (gate active? direction inferable? profile-type acceptable
                 from any cached visitor record?).
              Show a tentative outcome + a yellow "Offline — to be confirmed" badge.

When network resumes:
    Background sync drains the queue oldest-first.
    For each row: POST /scans with the ORIGINAL idempotencyKey.
        Same key + same payload → server returns the original recorded outcome
                                  + X-Idempotent-Replay: true.
        Server's outcome may differ from the device's tentative outcome
        (a "late denial"):
            → device shows a yellow alert:
                "Late denial at <gate> for <visitor> — please act."
```

### 5.3 Trust model

- Server clock (`ScannedAtUtc`) is the authoritative timestamp on the audit log. A device with a skewed or manipulated clock cannot back-date scans authoritatively.
- `ClientScannedAtUtc` is recorded but always flagged client-asserted. Reports use it for the visitor's apparent movement timeline; SOC investigations use `ScannedAtUtc` for actor-action ordering.
- Idempotency makes drained queues replay-safe. A 3-hour outage's worth of queued scans replays without producing duplicates.
- A device may locally accept a scan that the server later denies (because the visitor's status changed while offline). That is the "late denial" alert path. The operator at the gate is told to find the person and address the issue. The realistic alternative — "deny everything while offline" — would be worse for the event.

### 5.4 Scope reminder — what this increment does NOT do

The following are deferred to the Flutter app increment (plan OI-9):

- The Flutter app itself.
- The local SQLite queue and its schema.
- The background sync service.
- The on-device cached config refresher.
- Operator-facing UI for late-denial alerts.

When the Flutter increment starts, the device side plugs into the existing API contract with **zero** server-side changes. The same `POST /api/v1/gates/{gateId}/scans` endpoint, the same idempotency contract, the same outcome enum.

The CP simulator and the CP operator console (Blazor Server) are always online and bypass the offline path entirely.

---

End of document.
