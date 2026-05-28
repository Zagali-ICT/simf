# SIMF Gate Module — Increment Plan

| Field | Value |
|-------|-------|
| Document ID | SIMF-Gate-Module-Plan |
| Title | Gate Module — Increment Plan |
| Version | 1.0 |
| Status | Approved (pending kickoff) |
| Classification | Confidential |
| Prepared by | SIMF Engineering Team |
| Owner | SIMF Programme Owner |
| Approver | SIMF Programme Owner |
| Date issued | 2026-05-28 |
| Related documents | SIMF-API-001 (API conventions), SIMF-DAT-001 §5.3 (data model), SIMF-FDS-003 (Badge & Access Control), SIMF-FDS-005 (Bookings & Attendance), SIMF-RPM-001 (Roles & Permissions), SIMF-SES-001 (Engineering Standards), CLAUDE.md (project rules) |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-28 | SIMF Engineering Team | First issue. Locked plan after a 5-reviewer panel (architecture, backend, security, database, technical writer). |

---

## 1. Purpose

This document is the locked engineering plan for the SIMF Gate Module increment. It captures the agreed design, the locked decisions, the table shapes, the API surface, the constraint engine, the reports, and the build order. It is the contract the implementation must match.

It is **not** the user-facing API specification — that document is `docs/SIMF-API-GATES-001-Gate-Management-and-Scan-API.md` and is the first artefact produced by the build (step 1 of the build order).

## 2. Scope

### 2.1 In scope (this increment)

- A Control Panel "Manage Gates" page (master–detail single page) where an administrator defines a gate, picks its direction mode (In / Out / Both), the profile types allowed through it, and the operators assigned to it.
- A Control Panel "Gate Operator Console" page (role-adaptive) where a gate operator scans QR codes and the system records check-in or check-out events under the gate's constraints.
- A scan API surface (`/api/v1/gates/*`) usable today by the Control Panel and tomorrow by a Flutter staff application, designed contract-first.
- A gate-management API surface (`/api/v1/admin/gates/*`) for CRUD and reports.
- Per-visitor and event-wide reports (XLSX export).
- A `GateOperator` role added to the existing ASP.NET Core Identity setup as seed data only (no Identity-context migration).

### 2.2 Out of scope (deferred — see §11)

- Halls, Sessions, Speakers, Bookings — the next increment ("Programme & Session").
- Time-window constraints on gates that depend on a Session's start/end.
- Booking-required constraints on hall gates.
- Signed-token QR payload (FDS-003 OI-1 stays open).
- Flutter staff application implementation.
- Offline scan queueing on devices (the API contract is designed to support it; the device-side queue is built with the Flutter app).
- Geofence-based hall arrival (FDS-003 §5.4).
- A dedicated Gate Device identity (kiosks, turnstiles).

## 3. Locked decisions

The decisions below are final for this increment. Reopening any of them is a change request.

| # | Decision |
|---|----------|
| L-1 | One Control Panel page to manage gates (master–detail), not three separate List / New / Edit pages. |
| L-2 | Gate direction is a single enum `DirectionMode { In, Out, Both }`. Default for new gates: `Both`. |
| L-3 | In `Both` mode the operator console shows a single **Scan** button; the server infers direction from the visitor's most recent allowed scan at the same gate. Cold start (no prior scan) → CheckIn. |
| L-4 | Time-window constraints (during a session, between sessions, fixed clock) are deferred to the Programme & Session increment. The deferred constraint plugs in at step 9.5 of the engine and is purely additive. |
| L-5 | Booking-required constraints are deferred to the Programme & Session increment. No `RequiresBooking` column on `Gate` today. |
| L-6 | Scan + my-assignments under `/api/v1/gates/*`; CRUD + reports under `/api/v1/admin/gates/*`. GateOperator is not an administrator; the path split reflects this. |
| L-7 | `GateScan` primary key is `bigint IDENTITY`. All other Gate-module tables keep the SIMF Guid-PK convention. Justification: GateScan is the highest-insert table (~4M rows/day peak) and random-Guid clustering causes 30–80% fragmentation. |
| L-8 | Scan responses are always HTTP 200 when a scan is recorded (allowed or denied). 4xx is reserved for malformed / unauthorised / not-found / rate-limited / idempotency-key-conflict cases. |
| L-9 | `Idempotency-Key` header (UUID/ULID, ≤64 chars) is part of the scan contract from day one. Server stores `(key → original response)` in `ScanIdempotency` for 24h. Same key + same payload → original response + `X-Idempotent-Replay: true`. Same key + different payload → `409 IDEMPOTENCY_KEY_CONFLICT`. |
| L-10 | Server clock only for `ScannedAtUtc`. A device may pass `clientScannedAtUtc` for offline-replay scenarios; it is stored separately and explicitly marked client-asserted. |
| L-11 | A new `GateOperator` SimfRole is seeded into the existing Identity database. No Identity-context migration. Permissions: `Gates.Operate`, `Gates.ViewOwnReports`. |
| L-12 | Operator-to-gate is a many-to-many assignment (`GateAssignment`), not a single FK on `SimfUser`. |
| L-13 | Operator with one active assignment auto-selects the gate. Operator with multiple active assignments picks once per session ("Switch gate" link to change). Administrators always get the free picker. |
| L-14 | Gate-operator landing page after sign-in remains the dashboard. No automatic redirect. The Gate nav entry is the first item for operator-only accounts. |
| L-15 | Profile-type allow-list semantics: filter the list by `ProfileType.IsActive`; if the unfiltered list is empty → all allowed; if non-empty but filtered list is empty → **deny**. |
| L-16 | `IQrResolver` is extracted as a single abstraction used by both `QrLookupEndpoint` and `IGateScanService`. Today the implementation is the bare 12-char Crockford lookup. When the signed-token migration lands (FDS-003 OI-1) only this one class swaps. |
| L-17 | `OperationLog` captures every denied scan in addition to `GateScan` itself, so the existing SOC dashboard surfaces denials without learning a second table. Successful scans stay in `GateScan` only (volume). |
| L-18 | Mask the QR id in application logs (first 4 / last 4 characters). The full QR never appears in `OperationLog` (`SubjectUserId` is the join key). |
| L-19 | `GateScan` has columns `IpAddress`, `UserAgent`, `CorrelationId` to correlate with `OperationLog` and to investigate abuse. |
| L-20 | `GateScan` retention rule: 24 months after the event, then anonymise `UserProfileId` to null and retain only the aggregates. Documented now; purge job is a backlog item. |
| L-21 | `X-App-Key` is not authentication. Documented explicitly. All authorisation decisions rest on the JWT and the server-side assignment check. |
| L-22 | `Source = Simulator` is rejected by the scan endpoint unless `IHostEnvironment.IsDevelopment()` is true. Tests enforce this. |
| L-23 | Per-operator, per-gate rate limit on `/scans`: 10 requests/second burst, 300/minute sustained. Returns `429 RATE_LIMITED_OPERATOR` + `Retry-After`. |
| L-24 | Failure-rate circuit: ≥10 denial outcomes for the same operator within 60s triggers a 5-minute lockout on that operator at that gate and writes an `OperationLog` row `GateScanAbuseSuspected`. |
| L-25 | `Gate` carries a `rowversion` for optimistic concurrency on admin master-detail edits. `GateAssignment` does not (single-cell add/remove). |
| L-26 | `GateScan` opts out of `RowAudit` (the table is itself an audit log). An INSTEAD-OF UPDATE/DELETE trigger refuses any update or delete; a unit test asserts the exclusion list contains `GateScan`. |
| L-27 | `Gate` config and the profile-type allow-list are cached in-process (`IMemoryCache`), keyed by `gate:{id}:v{rowversion}`. Cache is bumped on every gate write and pre-warmed on app startup. |
| L-28 | Reports are synchronous-only this increment. Date ranges greater than 7 days are rejected with `REPORT_RANGE_TOO_LARGE`. Async job + poll is a backlog item. |
| L-29 | The D-110 freeze on the App context is lifted exclusively for this increment: one additive migration adding five tables. The Identity context is untouched. |

## 4. Reviewer panel — convergent findings (background)

A 5-reviewer panel (architecture, backend, security, database, technical writer) reviewed the original simplified plan. The convergent findings are reflected in §3. The split votes and the position taken are recorded as L-7 (GateScan PK), L-28 (sync reports), L-25 (rowversion only on Gate), L-22 (Simulator dev-only).

Findings not adopted in this increment (with rationale):
- **Materialised `VisitorPresence` table** (DB reviewer) — the filtered index `(UserProfileId, ScannedAtUtc DESC) WHERE Outcome = Allowed` is expected to be sufficient at SIMF scale. Re-evaluate if "currently inside" reads slow under live load.
- **Restricted DB role split** (DB reviewer) — deployment-time concern, deferred to SIMF-OPS-001.
- **Per-operator anomaly dashboard** (Security) — beyond the failure-rate circuit, anomaly detection waits for post-event review.
- **Async XLSX job** (Backend) — see L-28.

## 5. Domain model (final)

### 5.1 Tables (all on `SimfAppDbContext`)

| Table | Columns | Notes |
|-------|---------|-------|
| `Gate` | `Id` (Guid PK), `Code` (string, unique), `NameEn`, `NameAr`, `DirectionMode` (enum int: 0=In, 1=Out, 2=Both), `IsActive` (bit), `RowVersion` (rowversion), `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` | One row per physical gate. |
| `GateProfileTypeAllow` | `GateId` (FK Guid), `ProfileTypeId` (Guid, cross-context logical FK) — composite PK | Empty list = all profile types allowed. |
| `GateAssignment` | `GateId` (FK Guid), `UserId` (Guid, cross-context logical FK to `AspNetUsers`) — composite PK, `IsActive` (bit), audit cols | Operator-to-gate; supports rotation. |
| `GateScan` | `Id` (`bigint IDENTITY` PK), `GateId` (FK), `UserProfileId` (Guid, nullable, cross-context logical FK), `QrIdAtScan` (string, 12 chars), `Direction` (enum int), `Outcome` (enum int), `DenialReasonCode` (enum int, nullable), `ScannedAtUtc` (DateTimeOffset), `ClientScannedAtUtc` (DateTimeOffset, nullable), `ScannedByUserId` (Guid, cross-context logical FK), `Source` (enum int), `CorrelationId` (string, ≤64, nullable), `IdempotencyKey` (string, ≤64, nullable), `IpAddress` (string, ≤45, nullable), `UserAgent` (string, ≤512, nullable) | Append-only. INSTEAD-OF UPDATE/DELETE trigger refuses mutation. Opts out of RowAudit. |
| `ScanIdempotency` | `Key` (string, ≤64, PK), `OperatorUserId` (Guid), `GateId` (FK), `RequestHash` (varbinary(32)), `ResponseBlob` (varbinary(max)), `ExpiresAt` (DateTimeOffset) | 24h TTL; daily cleanup job. |

### 5.2 Indexes on `GateScan` (final)

```
PK CLUSTERED (Id)                                                                -- bigint IDENTITY
NCI (GateId, UserProfileId, Direction, ScannedAtUtc DESC) INCLUDE (Outcome)      -- duplicate-absorption seek
NCI (GateId, UserProfileId, ScannedAtUtc DESC) INCLUDE (Direction, Outcome)      -- Both-mode direction inference
NCI (UserProfileId, ScannedAtUtc DESC) INCLUDE (GateId, Direction, Outcome)      -- per-visitor report
FILTERED NCI (UserProfileId, ScannedAtUtc DESC) WHERE Outcome = 0 /*Allowed*/    -- "currently inside"
UNIQUE FILTERED NCI (IdempotencyKey) WHERE IdempotencyKey IS NOT NULL            -- idempotency replay
Fill factor 90 on every NCI
```

### 5.3 Cross-context FKs

`ProfileTypeId`, `UserId`, `UserProfileId`, `ScannedByUserId` are logical FKs only (the referenced rows live in the Identity context). Guardrails:
- Service-layer validation on every write.
- A backlog item adds a nightly orphan-detection job that left-joins across contexts and writes findings to an `IntegrityFinding` table (build only if drift is observed).
- XML-doc on each property documents the logical FK.

## 6. API surface (final)

### 6.1 Administrator-only — under `/api/v1/admin/gates/*`

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/admin/gates` | List gates (paged). |
| GET | `/admin/gates/{id}` | Get one gate (with allow-list + assignments). |
| POST | `/admin/gates` | Create. |
| PUT | `/admin/gates/{id}` | Update. Requires `If-Match` (rowversion). |
| DELETE | `/admin/gates/{id}` | Soft-delete (`IsActive=false`). |
| GET | `/admin/gates/{id}/assignments` | List operators on a gate. |
| POST | `/admin/gates/{id}/assignments` | Assign an operator. Body: `{ userId }`. |
| DELETE | `/admin/gates/{id}/assignments/{userId}` | Unassign. |
| GET | `/admin/gates/reports/visitors` | Per-visitor report. Query: `?qrId=` XOR `?userId=` (both ⇒ `VAL_AMBIGUOUS_LOOKUP`). |
| GET | `/admin/gates/reports/event` | Event-wide aggregates. Query: date range (≤7d), gate, profile-type. |
| GET | `/admin/gates/reports/event.xlsx` | Same as above, XLSX response. |

### 6.2 Administrator OR GateOperator — under `/api/v1/gates/*`

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/gates/my-assignments` | The caller's active gate assignments. |
| POST | `/gates/{gateId}/scans` | Record a scan. Body: `{ qrId, direction?, idempotencyKey?, clientScannedAtUtc?, source? }`. Header `Idempotency-Key` accepted (wins over body). |
| GET | `/gates/my-reports/today` | "My gate today" — operator-scoped report (admins also accepted, scoped to their own user id). |

All endpoints inherit SIMF-API-001 conventions: `ApiResult<T>` envelope, bilingual error messages via `Accept-Language`, standard headers (`X-App-Key`, `X-Device-Type`, `Authorization`, `X-Anti-Forgery` for state-changing requests on CP cookie surfaces).

## 7. Constraint engine — ordered check list

Every `POST /gates/{gateId}/scans` runs these steps in this order. The first denial short-circuits the remainder (except as noted). The result is always one row in `GateScan` (and an additional row in `OperationLog` on denial outcomes).

1. **Gate exists**. If not → `404 GATE_NOT_FOUND`. (No scan row written; this is a routing-class error.)
2. **Gate active**. If `IsActive = false` → scan-recorded denial `GATE_NOT_ACTIVE`.
3. **Operator assignment recheck** (caller is GateOperator only). `(GateId, OperatorUserId) ∈ GateAssignment AND IsActive`. If not → `403 OPERATOR_NOT_ASSIGNED` (no scan written — auth class).
4. **Rate limit** `(operator, gate)`: 10/sec burst, 300/min sustained. If exceeded → `429 RATE_LIMITED_OPERATOR` + `Retry-After`.
5. **Failure-rate circuit**: if this operator has ≥10 denials in 60s on this gate → 5-min lockout. `429 RATE_LIMITED_OPERATOR` + `OperationLog GateScanAbuseSuspected`.
6. **Idempotency**: if `Idempotency-Key` present and seen in 24h with the same `RequestHash` → return original response + `X-Idempotent-Replay: true`. Same key + different payload → `409 IDEMPOTENCY_KEY_CONFLICT`.
7. **QR syntactic check**: 12-char Crockford alphabet. If not → scan-recorded denial `QR_MALFORMED`.
8. **`IQrResolver.Resolve(qrId)`** → `UserProfile + AccountState`. If null → scan-recorded denial `QR_UNKNOWN`.
9. **Holder is `Approved` and `User.IsActive`**. If not → scan-recorded denial `HOLDER_NOT_APPROVED`.
10. **Direction resolution**:
    - `Both` mode: infer direction from the last allowed scan at this gate (cold start → CheckIn).
    - `In` / `Out` mode: caller's requested direction must match. If not → scan-recorded denial `DIRECTION_INVALID_FOR_MODE`.
11. **Profile-type allow-list**: load allow-rows for this gate, filter by `ProfileType.IsActive`. If the unfiltered list is empty → pass. If non-empty and the filtered list does not contain the visitor's `ProfileTypeId` (including the case where filtering produced an empty list) → scan-recorded denial `PROFILE_TYPE_NOT_ALLOWED`.
12. **Duplicate absorption**: 5s window on `(GateId, UserProfileId)` (without Direction, to handle `Both`-mode races). If a prior allowed scan is within the window, return its scan id without inserting.
13. **INSERT** `GateScan` (with IP / UA / CorrelationId / IdempotencyKey / ClientScannedAtUtc if provided). On a denial outcome, also INSERT an `OperationLog` row (`EventType = GateScanDenied`, `DenialReasonCode`, `CorrelationId`).

*(Future plug-in point 9.5)* — **Time-window check** (next increment). If the gate has any active time windows, "now" must fall within at least one. Empty list = always open.

*(Future plug-in point 11.5)* — **Booking-required check** (next increment). If the gate carries `RequiresBooking = true` and is hall-attached, the visitor must have a `Booking` for an `Session` in this hall whose `StartAt ≤ now ≤ EndAt`.

## 8. Error code catalogue

| Code | HTTP | Notes |
|------|------|-------|
| `GATE_NOT_FOUND` | 404 | Routing. |
| `GATE_NOT_ACTIVE` | 200 (recorded denial) | Engine step 2. |
| `OPERATOR_NOT_ASSIGNED` | 403 | Engine step 3. |
| `RATE_LIMITED_OPERATOR` | 429 + `Retry-After` | Engine steps 4 + 5. |
| `IDEMPOTENCY_KEY_CONFLICT` | 409 | Engine step 6. |
| `QR_MALFORMED` | 200 (recorded denial) | Engine step 7. |
| `QR_UNKNOWN` | 200 (recorded denial) | Engine step 8. |
| `HOLDER_NOT_APPROVED` | 200 (recorded denial) | Engine step 9. |
| `DIRECTION_INVALID_FOR_MODE` | 200 (recorded denial) | Engine step 10 (non-`Both` modes). |
| `PROFILE_TYPE_NOT_ALLOWED` | 200 (recorded denial) | Engine step 11. |
| `VAL_AMBIGUOUS_LOOKUP` | 400 | Reports — both `qrId` and `userId` supplied. |
| `REPORT_RANGE_TOO_LARGE` | 400 | Reports — date range > 7 days. |
| `ASSIGNMENT_NOT_FOUND` | 404 | Assignment endpoints. |

All messages bilingual (EN + AR) via `Accept-Language`.

## 9. Audit + observability

| Channel | What it captures | Source |
|---------|------------------|--------|
| `OperationLog` | Gate CRUD (`GateCreated`/`Updated`/`Deactivated`), assignment changes (`GateOperatorAssigned`/`Unassigned`), every **denied** scan (`GateScanDenied`), failure-rate circuit trips (`GateScanAbuseSuspected`). | Existing `IAuditLog` service. |
| `GateScan` | Every scan attempt, allowed or denied, with full context (IP, UA, CorrelationId). The single source of truth for the access timeline. | The scan service writes directly. |
| `RowAudit` | INSERT/UPDATE/DELETE on `Gate`, `GateProfileTypeAllow`, `GateAssignment` (config tables). Excludes `GateScan`. | Existing `RowAuditingSaveChangesInterceptor`. |

## 10. Caching, rate limiting, idempotency

- **Cache** `IGateConfigCache` — singleton `IMemoryCache`, keys `gate:{id}:v{rowVersion}`, sliding 5 min / hard 30 min. Bumped on every config write. Pre-warmed at app startup with active gates.
- **Rate limiter** — FastEndpoints `Throttle()` or AspNetCoreRateLimit, per `(operatorId, gateId)`, 10/sec burst + 300/min sustained, `Retry-After` set.
- **Failure-rate circuit** — a small in-memory counter per `(operatorId, gateId)`, reset every 60s; tripping locks out for 5 minutes and writes `GateScanAbuseSuspected`.
- **Idempotency store** — `ScanIdempotency` table, 24h TTL, daily cleanup background service.

## 11. Forward-looking design seams

The seams below are explicitly designed now so that adding the deferred features is additive, not a refactor.

### 11.1 `IQrResolver`

`Application/IdentityAccess/IQrResolver.cs` is the only place that turns a scanned string into a `UserProfile`. Today it does the bare 12-char Crockford lookup. When FDS-003 OI-1 (signed-token QR) lands, only the implementation swaps; every call site stays untouched.

### 11.2 Halls + time-window constraints

The next increment adds `Hall` and `Session` entities. `Gate` gets an optional `HallId` (when the gate is hall-attached) and an optional `GateTimeWindow` table:

```
GateTimeWindow { Id, GateId, Kind, StartsAtUtc?, EndsAtUtc?, SessionId?, Note? }
   Kind: Fixed | DuringSession | BetweenSessions
```

The engine plugs the check in at step 9.5 (post profile-type, pre duplicate-absorption). Empty list = always open. The constraint engine signature `IGateConstraintCheck.CheckAsync(gate, visitor, nowUtc, direction)` does not change.

### 11.3 Booking-required constraints

`Gate` gets `RequiresBooking (bool, default false)`. The engine plugs the check in at step 11.5. Reads `Booking` (next-increment entity) where `UserProfileId = visitor AND SessionId IN (sessions in this hall active at now)`. Today there is no column on `Gate`; the next increment adds it via a normal additive migration.

### 11.4 Offline scan queueing

The API contract supports offline operation from day one even though no offline client exists yet:
- `clientScannedAtUtc` accepted on the request (server stores it separately from `ScannedAtUtc`).
- `idempotencyKey` accepted (the device generates one per logical scan and re-uses it on retry).
- `source` accepted (the device sends `MobileApp`; the server clock-stamps `ScannedAtUtc` on receive).

Device-side queue behaviour (to be built with the Flutter app):
- Online: post immediately.
- Offline: write to a local SQLite queue (one row per scan) with `clientScannedAtUtc` and `idempotencyKey`. Run a permissive local constraint check (active gate config cached on the device, last-known profile types). Show the operator a yellow "Offline" badge.
- Network resumes: drain the queue oldest-first, posting each scan with the same `idempotencyKey`. If the server returns a different outcome than the local check (a "late denial"), display a yellow alert so the operator can act.

Trust model: client clock is recorded but never authoritative. `ScannedAtUtc` is always server time. Idempotency makes drained queues replay-safe.

### 11.5 Device identity (kiosks / turnstiles)

A future `GateDevice { Id, Name, GateId, ApiClientId, ApiKeyHash, IsActive, LastSeenUtc }` entity supports unattended scan stations. Devices authenticate via a long-lived API key (or mTLS), call the same `/gates/{id}/scans` endpoint with `Source = Kiosk`, and bypass operator-assignment checks (replaced by device-to-gate binding). Out of scope this increment.

## 12. Build order

| # | Step | Notes |
|---|------|-------|
| 1 | Write `docs/SIMF-API-GATES-001-Gate-Management-and-Scan-API.md` in full | Doc-first; contract before code. |
| 2 | Append `DECISIONS_LOG.md` entries D-132 → D-147 (see §13) | Lock the rationale in the repo. |
| 3 | Amend `docs/SIMF-DAT-001-Data-Model-and-Database-Design.md` §5.3 | Add the five tables + indexes. |
| 4 | Amend `docs/SIMF-FDS-003-Badge-and-Access-Control.md` | Note the gate-side surface lands here. |
| 5 | Domain entities + enums in `SIMF.Domain` and `SIMF.Common.Enums` | No behaviour. |
| 6 | EF configs + single additive migration on `SimfAppDbContext` | App-context freeze lift recorded in `CLAUDE.md`. |
| 7 | Extract `IQrResolver`; refactor `QrLookupEndpoint` to use it | Behaviour identical; tests must still pass. |
| 8 | Application interfaces + Infrastructure services + memory cache + rate limiter + idempotency store | All wiring. |
| 9 | Contracts (`record` types, paired EN/Ar fields) + FastEndpoints endpoints + FluentValidators | Validator lengths match EF + CP per SES-001 §5.3. |
| 10 | Identity seeder edit — `GateOperator` role + permissions | Idempotent INSERT-if-missing. |
| 11 | CP pages — Gates management → Operator console → admin reports | Single-page master-detail; role-adaptive operator page. |
| 12 | Localisation `Strings.resx` / `Strings.ar.resx` — every label + every denial reason | Zero hardcoded text. |
| 13 | Tests — every constraint branch, idempotency replay, ambiguous lookup, operator revocation mid-session, `Both`-mode inference, rate limit, failure-rate circuit, allow-list-filter-empties-to-deny, deactivated ProfileType edge case | `// Tests:` headers per SES-001. |
| 14 | Run `simplify` skill; address findings | Per CLAUDE.md §17. |
| 15 | Commit | Clear, descriptive message. Do not push. |

## 13. Decisions log entries to add (titles)

- **D-132** — Gate increment scope: what ships now, what defers to Programme/Session.
- **D-133** — `GateScan` is the source-of-truth append-only event log; outcomes inside.
- **D-134** — Path split: scan + my-assignments under `/api/v1/gates/...`, CRUD + reports under `/api/v1/admin/gates/...`.
- **D-135** — Scan responses always HTTP 200 when recorded; 4xx reserved.
- **D-136** — `Idempotency-Key` header contract: 24h TTL, conflict policy, replay marker.
- **D-137** — `GateOperator` role is seed data only — no Identity migration.
- **D-138** — `Both`-mode direction inference rule; cold-start = CheckIn.
- **D-139** — Profile-type allow-list filters inactive types; empty-after-filter denies.
- **D-140** — `IQrResolver` extracted; signed-token migration becomes one-file swap.
- **D-141** — `GateScan` PK is `bigint IDENTITY` — exception to the Guid-PK convention, justified by clustering on the highest-insert table.
- **D-142** — `GateScan` retention: 24 months post-event, then `UserProfileId` anonymised; aggregates retained.
- **D-143** — `X-App-Key` is not authentication.
- **D-144** — D-110 App-context migration freeze lifted for one additive migration adding 5 tables.
- **D-145** — `Source = Simulator` accepted only in non-production environments.
- **D-146** — Failure-rate circuit on `/scans`: ≥10 denials/60s/operator → 5-min lockout + audit.
- **D-147** — Reports are synchronous-only this increment; `REPORT_RANGE_TOO_LARGE` for >7 day windows.

## 14. Open items / next-increment owners

| ID | Item | Owner |
|----|------|-------|
| OI-1 | Signed-token QR migration (closes FDS-003 OI-1) | Future security increment. |
| OI-2 | Async XLSX export job + poll for large ranges | Backlog. |
| OI-3 | Scheduled retention purge for `GateScan` (24 months → anonymise) | Backlog (build closer to first anniversary). |
| OI-4 | Per-(operator, subject) anomaly alerting + operator scan-volume dashboard | Post-event review. |
| OI-5 | `VisitorPresence` materialised table | Build only if "currently inside" reads slow under live load. |
| OI-6 | Restricted DB role split (app user has `INSERT, SELECT` on `GateScan`; migrations under a separate principal) | SIMF-OPS-001 deployment work. |
| OI-7 | Orphan-detection nightly job for cross-context logical FKs | Build only if drift is observed. |
| OI-8 | Hall + Session entities + time-window + booking-required constraints | Programme & Session increment. |
| OI-9 | Flutter staff app + offline scan queue | Mobile-app increment. |
| OI-10 | `GateDevice` entity for kiosks and turnstiles | Future kiosk increment. |
| OI-11 | Confirm document classification with the owner | Owner. |

---

End of document.
