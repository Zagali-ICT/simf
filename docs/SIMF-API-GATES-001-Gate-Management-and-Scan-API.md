# Gate Management and Scan API

| Field | Value |
|-------|-------|
| Document ID | SIMF-API-GATES-001 |
| Title | Gate Management and Scan API |
| Version | 1.0 |
| Status | Approved (pending kickoff) |
| Classification | Confidential |
| Prepared by | SIMF Engineering Team |
| Owner | SIMF Programme Owner |
| Approver | SIMF Programme Owner |
| Date issued | 2026-05-29 |
| Related documents | SIMF-API-001 (envelope, headers, error model), SIMF-Gate-Module-Plan (locked decisions L-1…L-29), SIMF-Gate-Module-Design-Notes (operational design), SIMF-DAT-001 §5.3 (data model), SIMF-FDS-003 (badge & access control), SIMF-RPM-001 (roles & permissions) |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-29 | SIMF Engineering Team | First issue. Build-ready contract for the Gate Management and Scan API increment. |

---

## 1. Purpose

This document is the build-ready API contract for the **Gate Management and
Scan** increment. It specifies every request and response under the two new
route groups — `/api/v1/admin/gates/*` (administration) and `/api/v1/gates/*`
(operator) — the idempotency contract, the error catalogue, and the headers
that complement the envelope defined in SIMF-API-001.

It does **not** restate the API conventions, the `ApiResult<T>` envelope, the
authentication header, or the device header — those live in SIMF-API-001 and
apply unchanged. It does **not** specify the data model or the constraint
engine; those live in SIMF-DAT-001 §5.3 and SIMF-FDS-003. This document
specifies the **wire contract** only.

## 2. Scope

In scope:

- Administration endpoints for `Gate`, `GateProfileTypeAllow`,
  `GateAssignment`, and the report endpoints (incl. XLSX export).
- Operator endpoints for fetching assignments, posting a scan, and the
  operator's own daily report.
- The `Idempotency-Key` contract for the scan endpoint.
- The error catalogue covering routing, authentication, idempotency, and
  the recorded denial outcomes.

Out of scope:

- Device authentication (the future `GateDevice` API-key flow — plan §11.5,
  reserved seam).
- Time-window resolution (plan §11.2, reserved hook at engine step 9.5).
- Booking-required gating (plan §11.3, reserved hook at engine step 11.5).
- The offline queue, late-denial alerts and on-device cached config — the
  device-side flow per design notes §5.4.

## 3. Conventions inherited from SIMF-API-001

The conventions from SIMF-API-001 apply in full and are not restated:

- Base URL and versioning (§4) — every route in this document lives under
  `/api/v1`.
- Standard request headers (§5) — `X-App-Key`, `X-Device-Type`,
  `Accept-Language`, `Authorization`, `X-Anti-Forgery`.
- Response envelope `ApiResult<T>` (§6) — every response, success or
  failure, success-or-denial, uses the envelope.
- Error model (§7) — `code`, `message`, `details`.
- HTTP status codes (§8) — except where this document gives a more specific
  rule (see §7 below on scan denials).
- Pagination, filtering, sorting (§9).

## 4. Authentication and authorisation

| Route group | Auth | Required permission(s) |
|-------------|------|------------------------|
| `/api/v1/admin/gates/*` | Bearer (Administrator) | `Gates.Manage` |
| `/api/v1/gates/my-assignments` | Bearer | `Gates.Operate` |
| `POST /api/v1/gates/{gateId}/scans` | Bearer | `Gates.Operate` |
| `/api/v1/gates/my-reports/*` | Bearer | `Gates.ViewOwnReports` |

The `GateOperator` baseline role carries `Gates.Operate` and
`Gates.ViewOwnReports`. `Administrator` carries `Gates.Manage` plus the two
operator permissions (so an admin can also operate a gate from the CP
console for testing).

A request that lacks authentication returns **401**. A request that is
authenticated but lacks the permission returns **403**.

## 5. New headers introduced by this document

| Header | Direction | Used on | Purpose |
|--------|-----------|---------|---------|
| `Idempotency-Key` | Request | `POST /api/v1/gates/{gateId}/scans` | Client-generated UUIDv4. Replays return the original outcome. May also be carried in the body as `idempotencyKey`; the header wins if both are present. |
| `X-Idempotent-Replay` | Response | `POST /api/v1/gates/{gateId}/scans` | `true` when the response is a recorded replay of a prior key. Absent / `false` on the first execution of a key. |
| `X-Gate-Failure-Circuit` | Response | `POST /api/v1/gates/{gateId}/scans` | `open` when the failure-rate circuit fired (≥10 denials per 60 s → 5-min lockout). The request is rejected with **429**. Absent when the circuit is closed. |
| `X-RateLimit-Limit`, `X-RateLimit-Remaining` | Response | All scan + admin endpoints | Standard rate-limit headers; emitted by the existing rate-limiter middleware. |

## 6. Administration surface — `/api/v1/admin/gates/*`

Permission: `Gates.Manage`.

### 6.1 List gates

```
GET /api/v1/admin/gates?skip=0&top=25&sort=code&desc=false&search=&isActive=
```

Returns a `GridPage<AdminGateSummary>`.

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "...",
        "code": "G-MAIN-1",
        "name": "Main Entrance North",
        "nameArabic": "البوابة الشمالية الرئيسية",
        "directionMode": "Both",
        "allowedProfileTypeCount": 0,
        "assignedOperatorCount": 3,
        "isActive": true,
        "createdAt": "2026-05-29T08:00:00Z"
      }
    ],
    "total": 1,
    "skip": 0,
    "top": 25
  },
  "error": null,
  "meta": null
}
```

Query parameters per SIMF-API-001 §9. Sort keys: `code`, `name`, `directionMode`, `createdAt`.

### 6.2 Get a gate

```
GET /api/v1/admin/gates/{id}
```

Returns `AdminGateDetail`. Includes the allowed profile-type list
(`Guid[]`) and the assigned-operator list (`Guid[]`). 404 if not found.

```json
{
  "success": true,
  "data": {
    "id": "...",
    "code": "G-VIP-1",
    "name": "VIP Lounge Door",
    "nameArabic": "بوابة صالة كبار الشخصيات",
    "description": null,
    "descriptionArabic": null,
    "directionMode": "In",
    "isActive": true,
    "allowedProfileTypeIds": ["...", "..."],
    "assignedOperatorUserIds": ["...", "...", "..."],
    "createdAt": "2026-05-29T08:00:00Z",
    "updatedAt": null
  },
  "error": null,
  "meta": null
}
```

### 6.3 Create a gate

```
POST /api/v1/admin/gates
```

Body:

```json
{
  "code": "G-MAIN-1",
  "name": "Main Entrance North",
  "nameArabic": "البوابة الشمالية الرئيسية",
  "description": null,
  "descriptionArabic": null,
  "directionMode": "Both",
  "allowedProfileTypeIds": [],
  "assignedOperatorUserIds": ["..."]
}
```

Validation:

| Field | Rule |
|-------|------|
| `code` | 2…16 chars; case-insensitive unique; uppercase-normalised |
| `name`, `nameArabic` | 1…128 chars |
| `description`, `descriptionArabic` | 0…1024 chars; trimmed null-if-blank |
| `directionMode` | `In` / `Out` / `Both` |
| `allowedProfileTypeIds` | Optional; each must be an active `ProfileType` |
| `assignedOperatorUserIds` | Optional; each must be a `GateOperator` or `Administrator` |

Returns **201** with `AdminGateDetail`. Duplicate code → **409 GATE_CODE_DUPLICATE**.

### 6.4 Update a gate

```
PUT /api/v1/admin/gates/{id}
```

Body: same shape as create plus `isActive`. Same validation. Same conflict
rule. 404 if not found.

### 6.5 Deactivate a gate

```
DELETE /api/v1/admin/gates/{id}
```

Soft-delete (sets `IsActive = false`). Idempotent — repeated deletes
return **200** with the current state. 404 if not found.

### 6.6 List a gate's allowed profile types

```
GET /api/v1/admin/gates/{id}/allowed-profile-types
```

Returns `Guid[]` of `ProfileTypeId`s. (Convenience companion to §6.2.)

### 6.7 List a gate's assigned operators

```
GET /api/v1/admin/gates/{id}/assignments
```

Returns the active `GateAssignment` list:

```json
{
  "success": true,
  "data": [
    { "userId": "...", "userDisplayName": "Ahmed Al-Rashid", "assignedAt": "...", "assignedByUserId": "..." }
  ],
  "error": null,
  "meta": null
}
```

### 6.8 Reports

```
GET /api/v1/admin/gates/reports/scans?from=&to=&gateId=&outcome=&directionMode=&skip=&top=&sort=&desc=
GET /api/v1/admin/gates/reports/scans.xlsx?from=&to=&gateId=&outcome=  (XLSX download)
GET /api/v1/admin/gates/reports/currently-inside
```

Filters: `from` / `to` ISO 8601 UTC; `gateId` Guid; `outcome` `Allowed` /
`Denied`; `directionMode` filters scans on gates with the given mode (cross-
reference only).

The XLSX endpoint returns `200` with
`Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
and a filename header — the body is **not** an `ApiResult` envelope (it
is a binary download). All other reports return `ApiResult<T>`.

`currently-inside` returns `AdminCurrentlyInsideRow[]` — derived per design
notes §3.3 from the most-recent-allowed scan across all gates per visitor.

## 7. Operator surface — `/api/v1/gates/*`

Permission: `Gates.Operate` (scans + my-assignments); `Gates.ViewOwnReports`
(daily report).

### 7.1 My assignments

```
GET /api/v1/gates/my-assignments
```

Returns `OperatorGateAssignment[]`:

```json
{
  "success": true,
  "data": [
    {
      "gateId": "...",
      "code": "G-MAIN-1",
      "name": "Main Entrance North",
      "nameArabic": "البوابة الشمالية الرئيسية",
      "directionMode": "Both",
      "isActive": true
    }
  ],
  "error": null,
  "meta": null
}
```

When the operator has exactly one active assignment, the operator console
auto-selects it. When the operator has multiple, the console asks them to
pick one for the shift. When the operator has none, the console shows a
"no assigned gate — please contact an administrator" empty state.

### 7.2 Post a scan

```
POST /api/v1/gates/{gateId}/scans
Headers:
  Idempotency-Key: <UUIDv4>   (optional; body field also accepted; header wins)
Body:
```

```json
{
  "qr": "AB12CD34EF56",
  "clientScannedAtUtc": "2026-05-29T08:15:00Z",
  "idempotencyKey": "...",
  "source": "Simulator"
}
```

| Field | Rule |
|-------|------|
| `qr` | 12-char QR string exactly as scanned. Required. Trimmed; case-sensitive. |
| `clientScannedAtUtc` | Optional. Device-asserted local scan time (UTC). Recorded but never authoritative. |
| `idempotencyKey` | Optional. UUIDv4. Header `Idempotency-Key` takes precedence if both are sent. |
| `source` | `Simulator` (CP only, dev-only) / `MobileApp` / `Kiosk`. Defaults to `MobileApp` when absent on a non-CP origin. |

#### 7.2.1 Success (allowed)

HTTP **200**. `X-Idempotent-Replay` absent on first call.

```json
{
  "success": true,
  "data": {
    "scanId": 482719,
    "outcome": "Allowed",
    "direction": "CheckIn",
    "scannedAtUtc": "2026-05-29T08:15:01.231Z",
    "userProfile": {
      "id": "...",
      "displayName": "Layla Al-Hassan",
      "displayNameArabic": "ليلى الحسن",
      "profileTypeId": "...",
      "profileTypeName": "VIP",
      "profileTypePageColor": "#C9A227"
    },
    "denialReasonCode": null,
    "denialMessage": null
  },
  "error": null,
  "meta": null
}
```

#### 7.2.2 Denial (recorded)

HTTP **200**. `success: true` in the envelope — the *request* succeeded
(the system did what it was asked: scan + record). The *scan outcome* lives
in the `data.outcome` field.

```json
{
  "success": true,
  "data": {
    "scanId": 482720,
    "outcome": "Denied",
    "direction": "CheckIn",
    "scannedAtUtc": "2026-05-29T08:15:02.118Z",
    "userProfile": {
      "id": "...",
      "displayName": "Test Visitor",
      "displayNameArabic": "زائر اختبار",
      "profileTypeId": "...",
      "profileTypeName": "Silver",
      "profileTypePageColor": "#9CA3AF"
    },
    "denialReasonCode": "PROFILE_TYPE_NOT_ALLOWED",
    "denialMessage": "This gate is for VIP / VVIP guests."
  },
  "error": null,
  "meta": null
}
```

`denialMessage` is localised by `Accept-Language` (EN / AR). The full list
of `denialReasonCode` values is in §8.2.

#### 7.2.3 Replay (idempotency hit)

Same `(Idempotency-Key, GateId)` posted a second time. HTTP **200**.
`X-Idempotent-Replay: true`. Body matches the original response exactly.

#### 7.2.4 Failures that are *not* recorded scans

| HTTP | Code | When |
|------|------|------|
| 400 | `VALIDATION_FAILED` | Missing `qr`, malformed `idempotencyKey`, unsupported `source` |
| 401 | `AUTH_*` | Missing / invalid bearer |
| 403 | `GATE_OPERATOR_NOT_ASSIGNED` | Caller has no active assignment for this `gateId` |
| 404 | `GATE_NOT_FOUND` | `gateId` does not exist |
| 409 | `IDEMPOTENCY_KEY_CONFLICT` | Same key, **different** payload (qr / gateId mismatch) |
| 429 | `RATE_LIMIT_EXCEEDED` | Standard rate-limiter |
| 429 | `GATE_FAILURE_CIRCUIT_OPEN` | Failure-rate circuit fired; `X-Gate-Failure-Circuit: open` |
| 503 | `GATE_INACTIVE` | The gate itself is `IsActive = false` |

A denial recorded in `GateScan` is **not** in this table — denials use the
HTTP 200 success-envelope path of §7.2.2 because the system did record the
event the operator asked for.

### 7.3 My report — today

```
GET /api/v1/gates/my-reports/today?gateId=
```

Permission: `Gates.ViewOwnReports`. Returns the operator's own scans for
the current day at the chosen gate (or all assigned gates if `gateId` is
omitted):

```json
{
  "success": true,
  "data": {
    "operatorUserId": "...",
    "fromUtc": "2026-05-29T00:00:00Z",
    "toUtc": "2026-05-29T23:59:59Z",
    "totals": { "allowed": 142, "denied": 7 },
    "denialBreakdown": [
      { "code": "PROFILE_TYPE_NOT_ALLOWED", "count": 5 },
      { "code": "QR_UNKNOWN", "count": 2 }
    ],
    "rows": [ { "scanId": 482719, "scannedAtUtc": "...", "outcome": "Allowed", "direction": "CheckIn", "visitorDisplayName": "Layla Al-Hassan", "denialReasonCode": null } ]
  },
  "error": null,
  "meta": null
}
```

## 8. Error catalogue

### 8.1 Routing / auth / state errors (HTTP 4xx / 5xx)

These are *envelope failures* (`success: false`).

| Code | HTTP | Meaning |
|------|------|---------|
| `GATE_INVALID` | 400 | Validation of a gate-management payload failed (code length, name length, direction mode, …) |
| `GATE_NOT_FOUND` | 404 | The addressed gate does not exist |
| `GATE_CODE_DUPLICATE` | 409 | Create or update would collide with an existing gate code |
| `GATE_INACTIVE` | 503 | Scan target gate is `IsActive = false` |
| `GATE_OPERATOR_NOT_ASSIGNED` | 403 | Caller has no active assignment for the addressed gate |
| `GATE_ASSIGNMENT_INVALID` | 400 | Assignment add/remove payload invalid |
| `GATE_PROFILE_TYPE_INVALID` | 400 | Allowed-profile-type id is missing, duplicated, or refers to a non-existent / inactive `ProfileType` |
| `IDEMPOTENCY_KEY_CONFLICT` | 409 | Same key, different payload — refusing to replay a different scan under a prior key |
| `GATE_FAILURE_CIRCUIT_OPEN` | 429 | Per-gate failure-rate circuit fired (≥10 denials per 60 s → 5-min lockout) |

### 8.2 Scan denial reasons (HTTP 200, recorded in `GateScan`)

These appear on a successful POST `/scans` response as `data.denialReasonCode`
when `data.outcome == "Denied"`. The constraint engine in SIMF-FDS-003 §X.X
emits exactly one of these on a denial.

| Code | Meaning | Emitted by engine step |
|------|---------|------------------------|
| `QR_UNKNOWN` | The QR resolved to no `UserProfile` | 3 |
| `GATE_INACTIVE_AT_SCAN` | Gate became inactive between request and validation. Recorded for forensic completeness; the HTTP path also returns 503 if reached pre-engine. | 5 |
| `HOLDER_NOT_APPROVED` | The visitor's account is not in `Approved` state | 6 |
| `HOLDER_DISABLED` | The visitor's account is `Disabled` | 7 |
| `HOLDER_LOCKED` | The visitor's account is `Locked` | 8 |
| `PROFILE_TYPE_INACTIVE` | The visitor's `ProfileType` is `IsActive = false` | 9 |
| `OUTSIDE_TIME_WINDOW` | Reserved (engine step 9.5) — emitted only when the time-window feature ships in a later increment | 9.5 |
| `PROFILE_TYPE_NOT_ALLOWED` | The visitor's `ProfileType` is not in the gate's allow-list (or the allow-list filtered empty per L-15) | 11 |
| `BOOKING_REQUIRED_MISSING` | Reserved (engine step 11.5) — emitted only when the booking-required feature ships in a later increment | 11.5 |
| `DUPLICATE_ABSORBED_5S` | Not a denial — this is the **replay path** for a duplicate scan within 5 s; the API returns the existing scan id with `outcome = Allowed` (or the original denial) and `X-Idempotent-Replay: false` (because the key is different, but the duplicate is absorbed) | 13 |

Localised message strings for every code live in `Strings.resx` /
`Strings.ar.resx` (per GATE-12). The wire code never changes — only the
message does.

## 9. Idempotency contract — detail

The scan endpoint is the only endpoint with an idempotency contract. The
two reasons are (i) safe retry from an offline drain (design notes §5),
and (ii) operator double-tap absorption (separate mechanism — design
notes §3.2).

Rules:

1. The key is a **UUIDv4** (36-char string with dashes). Anything else →
   **400 VALIDATION_FAILED**.
2. The key may be sent on the request header `Idempotency-Key` and/or in
   the body field `idempotencyKey`. If both are present, the header wins.
3. The store is `ScanIdempotency(Key, GateId, RequestHash, ResponseHash,
   StoredAt)` with a 24-hour retention. Replay returns the original
   response and sets `X-Idempotent-Replay: true`. A request with the
   same key but a different `qr` or `gateId` → **409
   IDEMPOTENCY_KEY_CONFLICT**.
4. A request **without** a key is accepted (offline-first clients are
   expected to send one; the CP simulator and kiosk fallback do not have
   to). Without a key, no replay protection — but the 5-second duplicate
   absorption (design notes §3.2) still applies.

## 10. Rate limiting and failure-rate circuit

| Mechanism | Where it sits | When it fires |
|-----------|---------------|---------------|
| Per-token rate limit | Existing middleware | Hit on every endpoint per SIMF-API-001 §9 |
| Per-gate failure-rate circuit | New, scoped to `POST /scans` | ≥ 10 denials within a rolling 60-second window for the same `gateId` → reject the next 5 minutes of scans on that gate with **429 GATE_FAILURE_CIRCUIT_OPEN** and the header `X-Gate-Failure-Circuit: open`. The circuit prevents a misconfigured allow-list from generating thousands of audit-log denial rows in a panic loop. |

The circuit emits one `OperationLog` row (`EventType = GateFailureCircuitOpened`)
on open and one on close (`GateFailureCircuitClosed`), so SOC can correlate the
short outage with the underlying denial pattern.

## 11. Forward-looking seams (reserved — not in this increment)

| Seam | Reserved at | Future increment |
|------|-------------|------------------|
| Device API-key authentication | §4 (gateAuth) | Flutter app / kiosk increment |
| Time-window constraint | Engine step 9.5 + DenialReasonCode `OUTSIDE_TIME_WINDOW` | Programme & Session increment |
| Booking-required constraint | Engine step 11.5 + DenialReasonCode `BOOKING_REQUIRED_MISSING` | Bookings increment |
| Offline queue drain | Design notes §5 + header `X-Idempotent-Replay` + 24h idempotency store | Flutter app increment |
| Materialised `VisitorPresence` table | Design notes §3.3 fallback | Reporting hardening (only if needed) |

All five seams are *contract-stable*: the wire surface ships in this
increment so the device side and the future increments plug into the
existing API without server-side change.

## 12. Acceptance criteria

1. Every endpoint in §6 and §7 responds with `ApiResult<T>` per SIMF-API-001.
2. `POST /scans` returns **200** for *any* recorded outcome (Allowed or
   Denied); 4xx / 5xx are reserved for non-recorded failures per §7.2.4.
3. `Idempotency-Key` replay returns byte-identical body + the header
   `X-Idempotent-Replay: true`.
4. The failure-rate circuit opens after 10 denials in 60 s and stays open
   for 5 min.
5. Every denial code in §8.2 is reachable through at least one test in
   `SIMF.Api.Tests/Gates/*` and carries a bilingual message string.
6. The XLSX report endpoint streams a binary spreadsheet with the same
   filter set as the JSON report.
7. The role / permission gate is enforced — Administrator hits admin
   endpoints; GateOperator hits operator endpoints; neither can take the
   other's role's actions without the corresponding permission.

## 13. Open items

| ID | Item | Resolution target |
|----|------|-------------------|
| OI-1 | Confirm whether `currently-inside` returns a paged or unbounded list once event volumes are real. Default plan = unbounded JSON; falls back to a paged contract under load. | Pre-event load test |
| OI-2 | Whether the XLSX export should also emit `Content-Disposition` charset hints for Arabic filenames. | Pre-event smoke |

---

End of document.
