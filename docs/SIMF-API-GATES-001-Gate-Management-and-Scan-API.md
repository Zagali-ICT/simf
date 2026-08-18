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
| 1.1 | 2026-05-29 | SIMF Engineering Team | D-160 — added §7.4 `POST /gates/{gateId}/visitors/list`: cursor-paged staff-app view of scans at a single gate, backed by the D-158 snapshot columns. |
| 1.2 | 2026-07-26 | SIMF Engineering Team | BUG-018 — corrected §4 to the owner's app-first operator model (a gate operator is an approved app account on an operational ProfileType carrying Staff/Moderator, not a CP RBAC-role holder); tightened the §6.3 `assignedOperatorUserIds` rule to that eligibility with a named-id 400; added §6.7a operator-candidates and §6.7b gate-form lookups; appended `userEmail` to the §6.7 assignment row. |
| 1.3 | 2026-08-18 | SIMF Engineering Team | Grid-seam alignment. Rewrote §6.8 Reports to the shipped contract: the scan report and the occupancy report are now POSTs binding a `GridQuery` (`.../reports/scans/list`, `.../reports/currently-inside/list`) and return `GridPage<T>`; `.../reports/scans.xlsx` keeps its route but binds the same `GridQuery` so the workbook holds filter parity with the grid. Listed the real filter, search and sort keys and the page-size fallback/cap for both reports, and replaced the removed `directionMode` report filter with the scan-level `direction`. §13 OI-1 is closed: the occupancy report is server-paged by contract, so it no longer waits on the load test. |
| 1.4 | 2026-08-18 | SIMF Engineering Team | Removed the staleness the 1.3 rewrite left behind, so the document is internally consistent again. §6.1 gate list corrected to `POST /admin/gates/list` binding a `GridQuery`, with the real declared keys, the searchable set, the default order and the page fallback / cap, and with the render-only columns named as such: the old text documented a `GET` with query parameters and a sort-key list that was wrong twice. §6.3 corrected to the shipped **200** on create. §6.6 marked never built: no route in `src` maps `allowed-profile-types`. Operator surface re-prefixed to the shipped `/api/v1/app/gates/*` throughout §1, §4, §5 and §7. §11 offline-queue-drain seam struck through and marked shipped: `GET /app/gates/offline-config` and `GET /app/gates/offline-roster` exist, so listing them as reserved was the same class of staleness. §12 criterion 5 re-pointed at the real flat test files (there is no `SIMF.Api.Tests/Gates/` folder), and criterion 7 restated in terms of permission holders rather than the "Administrator" / "GateOperator" role names BUG-018 had already retired in §4. §2 narrowed to say the offline queue is out of scope as a device-side flow while the server side of the cached config has shipped. §8.2's two "reserved" denial codes corrected: `GateOperatorService` writes both, and `OUTSIDE_TIME_WINDOW` no longer means a time window at all but a badge from a closed edition, the code being reused on purpose so a scan cannot tell the holder which check failed; `BOOKING_REQUIRED_MISSING` is emitted at a session-hall door for an unregistered attendee, on entries only and relaxed under walk-in mode. The matching §2 and §11 rows are struck through to agree. §12 criterion 5 records that `HOLDER_LOCKED` and `PROFILE_TYPE_INACTIVE` have no direct assertion, so it is documented as not yet met in full rather than asserted as passing. Every line this revision adds or edits is free of em-dash, en-dash and ellipsis per the owner rule; untouched lines keep theirs, since a wholesale purge is not a staleness correction. |

---

## 1. Purpose

This document is the build-ready API contract for the **Gate Management and
Scan** increment. It specifies every request and response under the two new
route groups `/api/v1/admin/gates/*` (administration) and `/api/v1/app/gates/*`
(operator), plus the idempotency contract, the error catalogue, and the headers
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
- ~~Time-window resolution (plan §11.2, reserved hook at engine step 9.5).~~
  Step 9.5 now has a writer, though not the one planned: it refuses a badge from
  a closed **edition** rather than resolving a time window. See §8.2.
- ~~Booking-required gating (plan §11.3, reserved hook at engine step 11.5).~~
  Shipped for session-hall doors. See §8.2.
- The offline queue and late-denial alerts as a **device-side** flow per design
  notes §5.4: how a scanner queues, drains and reconciles is a Flutter concern.
  The **server** side of the cached config is no longer out of scope. It shipped
  as `GET /app/gates/offline-config` and `GET /app/gates/offline-roster`, which
  §11's as-built note records.

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
| `/api/v1/app/gates/my-assignments` | Bearer | `Gates.Operate` |
| `POST /api/v1/app/gates/{gateId}/scans` | Bearer | `Gates.Operate` |
| `/api/v1/app/gates/my-reports/*` | Bearer | `Gates.ViewOwnReports` |

**Who a gate operator is (BUG-018, owner ruling).** Gate scanning happens
**through the mobile app**, not the Control Panel. A gate operator is therefore an
operational **non-admin app account**: an approved account whose `ProfileType` is a
partner/operational type (`IsForVisitor = false`) carrying a `MobileAppRole` that
confers `Gates.Operate` — `Staff` or `Moderator`. That grant flows from the
profile type alone (`PermissionCatalog.OperationalPermissionsForAppRole`), never
from a Control-Panel RBAC role, so a gate operator never needs a CP account.

The `GateOperator` and `SecurityTeam` baseline CP roles carry `Gates.Operate` and
`Gates.ViewOwnReports` for the **CP operator console** (`/admin/gates/operator`),
which is retained as a fallback/observation desk. `Administrator` carries
`Gates.Manage` plus the two operator permissions via the `*` wildcard.

A request that lacks authentication returns **401**. A request that is
authenticated but lacks the permission returns **403**.

## 5. New headers introduced by this document

| Header | Direction | Used on | Purpose |
|--------|-----------|---------|---------|
| `Idempotency-Key` | Request | `POST /api/v1/app/gates/{gateId}/scans` | Client-generated UUIDv4. Replays return the original outcome. May also be carried in the body as `idempotencyKey`; the header wins if both are present. |
| `X-Idempotent-Replay` | Response | `POST /api/v1/app/gates/{gateId}/scans` | `true` when the response is a recorded replay of a prior key. Absent / `false` on the first execution of a key. |
| `X-Gate-Failure-Circuit` | Response | `POST /api/v1/app/gates/{gateId}/scans` | `open` when the failure-rate circuit fired (≥10 denials per 60 s → 5-min lockout). The request is rejected with **429**. Absent when the circuit is closed. |
| `X-RateLimit-Limit`, `X-RateLimit-Remaining` | Response | All scan + admin endpoints | Standard rate-limit headers; emitted by the existing rate-limiter middleware. |

## 6. Administration surface: `/api/v1/admin/gates/*`

Permission: `Gates.Manage`.

### 6.1 List gates

```
POST /api/v1/admin/gates/list                           body: GridQuery
```

The gate list runs on the shared grid seam like the two reports in §6.8, so it is a
**POST** whose body is a `GridQuery` (`Skip` / `Top` / `Sort` / `Desc` / `Search` /
`Filters`) and whose payload is one `GridPage<AdminGateSummary>`.

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
        "createdAt": "2026-05-29T08:00:00Z",
        "description": null,
        "descriptionArabic": null
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

Declared keys, usable as both filter and sort: `code`, `name`, `nameArabic`,
`directionMode`, `isActive`. `code`, `name` and `nameArabic` are the searchable
keys, so `Search` matches across the three. Default order is `code` ascending; page
size falls back to 25 and is capped at 200. A key that is not on that list is a
bilingual **400**, not a silently ignored request, and key matching is
case-insensitive.

`isActive` is declared even though the grid renders that column without a filter
box, because the list has always honoured an `isActive` filter. `createdAt` is
**not** declared: the column is rendered but is neither sortable nor filterable.
`allowedProfileTypeCount` and `assignedOperatorCount` are likewise render-only.
They are correlated sub-queries rather than declared columns, which keeps the page
one SELECT and keeps both counts out of the ORDER BY. `description` and
`descriptionArabic` ride on the row so the grid's Excel export can round-trip the
bilingual description; neither is rendered as a grid column.

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
| `assignedOperatorUserIds` | Optional; each id must be an **eligible gate operator** (see §4): an approved app account on an operational `ProfileType` (`IsForVisitor=false`) whose `MobileAppRole` confers `Gates.Operate`, or an approved Control-Panel admin account for the CP operator console. An ineligible id is rejected with **400 GATE_ASSIGNMENT_INVALID**, and the message names the offending id(s). |

Returns **200** with `AdminGateDetail`. Duplicate code gives **409
GATE_CODE_DUPLICATE**.

**As-built.** This section read "returns 201". The endpoint answers **200** on the
envelope's success path like every other admin write in the system, and
`AdminGatesTests` pins that status, so the document is corrected to the shipped
behaviour rather than the endpoint to the document.

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
~~GET /api/v1/admin/gates/{id}/allowed-profile-types~~   Never built
```

**As-built.** This convenience companion to §6.2 was specified and never shipped.
Nothing in `src` maps the route, so a caller gets a 404 from the router rather than
a `Guid[]`. The allowed profile types are read from §6.2's
`allowedProfileTypeIds`, which the Control Panel's gate form already uses, and the
selectable options come from §6.7b. The section number is retained rather than
renumbered so the cross-references in the sibling documents keep resolving.

### 6.7 List a gate's assigned operators

```
GET /api/v1/admin/gates/{id}/assignments
```

Returns the active `GateAssignment` list. `userEmail` was appended by BUG-018 so
the CP detail view can list **who** is assigned instead of a bare count:

```json
{
  "success": true,
  "data": [
    { "userId": "...", "userDisplayName": "Ahmed Al-Rashid", "userEmail": "ahmed@example.sa", "assignedAt": "...", "assignedByUserId": "..." }
  ],
  "error": null,
  "meta": null
}
```

### 6.7a Gate-operator candidates (BUG-018)

```
POST /api/v1/admin/gates/operator-candidates/list
```

Permission: `Gates.Manage`. Body: the standard `GridQuery` (`skip`, `top`,
`search`). Returns `ApiResult<GridPage<AdminGateOperatorCandidate>>` — the accounts
that may be assigned as gate operators per §4:

```json
{
  "success": true,
  "data": {
    "items": [
      { "userId": "...", "email": "ops1@example.sa", "displayName": "Ahmed Al-Rashid", "profileTypeName": "Gate Staff", "mobileAppRole": "Staff" }
    ],
    "total": 1, "skip": 0, "top": 25
  },
  "error": null,
  "meta": null
}
```

Scoped to **approved** accounts only (deactivated / pending / rejected accounts are
never offered) and searched server-side on email + display name, so the CP picker
is not a blind top-200. Resolved as separate reads on `SIMF_App` then
`SIMF_Identity` and merged in memory — never a cross-database join (D-157).

### 6.7b Gate-form lookups (BUG-018)

```
GET /api/v1/admin/gates/form-options
```

Permission: `Gates.Manage`. Returns `ApiResult<AdminGateFormOptions>` — the active
`ProfileType` options for the allow-list and the active `Hall` options for the
hall-door binding:

```json
{
  "success": true,
  "data": {
    "profileTypes": [ { "id": "...", "name": "VIP", "nameArabic": "كبار الشخصيات" } ],
    "halls": [ { "id": "...", "name": "Hall A", "nameArabic": "القاعة أ" } ]
  },
  "error": null,
  "meta": null
}
```

The gate form previously read the shared `ProfileTypes.View` / `Halls.View` admin
lists, so a `Gates.Manage`-only holder (the Security team) saw silently empty
dropdowns. Serving both lookups under `Gates.Manage` keeps the gate form usable by
its own permission holder without widening the ProfileTypes / Halls surface.

### 6.8 Reports

Both reports run on the shared grid seam, so each is a **POST** whose body is a
`GridQuery` (`Skip` / `Top` / `Sort` / `Desc` / `Search` / `Filters`) and whose
payload is one `GridPage<T>`:

```
POST /api/v1/admin/gates/reports/scans/list             body: GridQuery
POST /api/v1/admin/gates/reports/scans.xlsx             body: GridQuery  (XLSX download)
POST /api/v1/admin/gates/reports/currently-inside/list  body: GridQuery
```

Scan-report filter keys: `gateId` Guid; `userProfileId` Guid; `direction`;
`outcome` `Allowed` / `Denied`; `denialReasonCode`; `source`; `scannedAt`; plus the
two hand-written day-range keys `scannedFrom` (inclusive) and `scannedTo`
(inclusive of the whole day). `qrIdAtScan` and `scannedDisplayName` are the
searchable keys. Default order is `scannedAt` descending; page size falls back to
50 and is capped at 200. Unknown or unparseable filter keys are now rejected rather
than silently ignored.

The XLSX endpoint keeps its route but binds the **same** `GridQuery` as the list,
so the workbook can never drift out of filter parity with the grid it came from. It
returns `200` with
`Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
and a filename header. The body is **not** an `ApiResult` envelope (it is a binary
download). All other reports return `ApiResult<GridPage<T>>`.

`currently-inside/list` returns `GridPage<AdminCurrentlyInsideRow>`, derived per
design notes §3.3 from the most-recent-allowed scan across all gates per visitor.
Its columns are declared over `GateScan` because that is what the query pages:
`gateId` and `scannedAt`, default order `scannedAt` descending, page size fallback
25 and cap 200. The display name, Arabic name and profile type the Control Panel
renders are resolved **after** the page is chosen, some of it out of the Identity
database, so they are neither sortable nor filterable.

## 7. Operator surface: `/api/v1/app/gates/*`

Permission: `Gates.Operate` (scans + my-assignments); `Gates.ViewOwnReports`
(daily report).

### 7.1 My assignments

```
GET /api/v1/app/gates/my-assignments
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
POST /api/v1/app/gates/{gateId}/scans
Headers:
  Idempotency-Key: <UUIDv4>   (optional; body field also accepted; header wins)
Body:
```

```json
{
  "qr": "AB12CD34EF56",
  "clientScannedAt": "2026-05-29T08:15:00Z",
  "idempotencyKey": "...",
  "source": "Simulator"
}
```

| Field | Rule |
|-------|------|
| `qr` | 12-char QR string exactly as scanned. Required. Trimmed; case-sensitive. |
| `clientScannedAt` | Optional. Device-asserted device-local scan time. Recorded but never authoritative. |
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
    "scannedAt": "2026-05-29T08:15:01.231Z",
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
    "scannedAt": "2026-05-29T08:15:02.118Z",
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
| ~~503~~ | ~~`GATE_INACTIVE`~~ | **Retired — see the as-built note below** |

A denial recorded in `GateScan` is **not** in this table — denials use the
HTTP 200 success-envelope path of §7.2.2 because the system did record the
event the operator asked for.

**As-built (DEF-STF-008).** `POST /scans` never returns `503 GATE_INACTIVE`.
A scan aimed at a gate with `IsActive = false` is denied by engine step 5 as a
**recorded** `GATE_INACTIVE_AT_SCAN` denial at HTTP **200** (§7.2.2 / §8.2), so
the attempt still lands in the append-only `GateScan` audit trail and the
operator gets the localised denial card rather than an envelope failure. The
inactive-gate check reads the same cached gate snapshot the rest of the engine
uses, so there is no separate "pre-engine" moment for a 503 to occupy; the
endpoint arm that handled that result kind was unreachable and has been removed.
`ErrorCodes.GateInactive` stays in the published vocabulary (marked obsolete)
but nothing emits it.

### 7.3 My report — today

```
GET /api/v1/app/gates/my-reports/today?gateId=
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
    "rows": [ { "scanId": 482719, "scannedAt": "...", "outcome": "Allowed", "direction": "CheckIn", "visitorDisplayName": "Layla Al-Hassan", "denialReasonCode": null } ]
  },
  "error": null,
  "meta": null
}
```

### 7.4 List visitors at a gate (D-160)

```
POST /api/v1/app/gates/{gateId}/visitors/list
```

Permission: `Gates.ViewOwnReports`. Cursor-paged list of scans recorded at
a single gate, designed for the staff app's "who's at this gate now" view
and polled every ~10 seconds with the previous response's `nextCursor`.

Request body — every field is optional except `pageSize` (which defaults
to 50 when zero/negative):

```json
{
  "cursor": null,
  "pageSize": 50,
  "direction": null,
  "outcome": "Allowed",
  "sinceUtc": null,
  "untilUtc": null
}
```

| Field      | Type                          | Notes                                                                                        |
|------------|-------------------------------|----------------------------------------------------------------------------------------------|
| cursor     | opaque string \| null         | `nextCursor` from the previous response. `null` = first page. Malformed → treated as `null`. |
| pageSize   | int                           | Server clamps to `1..200`. Default 50 when omitted / non-positive.                           |
| direction  | `CheckIn`\|`CheckOut`\|`null` | `null` = any direction.                                                                      |
| outcome    | `Allowed`\|`Denied`\|`null`   | **Default `Allowed`** when omitted — the "currently inside" use case.                        |
| sinceUtc   | ISO-8601 \| null              | Inclusive lower bound on `scannedAt`.                                                     |
| untilUtc   | ISO-8601 \| null              | Exclusive upper bound on `scannedAt`.                                                     |

Response body:

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "scanId": 482719,
        "scannedAt": "2026-05-29T14:03:00Z",
        "direction": "CheckIn",
        "outcome": "Allowed",
        "userProfileId": "abc-...",
        "qrIdAtScan": "ABCD1234EFGH",
        "displayName": "Ahmad Salem",
        "profileTypeName": "VVIP",
        "denialReasonCode": null
      }
    ],
    "nextCursor": "eyJsYXN0SWQiOjQ4MjcxOX0=",
    "asOfUtc": "2026-05-29T14:05:00Z"
  },
  "error": null,
  "meta": null
}
```

`displayName` and `profileTypeName` are **frozen snapshots** captured at
scan time (the `GateScan.ScannedDisplayName` and `ScannedProfileTypeName`
columns introduced by D-158). The endpoint never JOINs across to the
Identity DB. The columns therefore preserve the visitor's identity *as it
was at the moment of the scan*, even if the linked `UserProfile` in
`SIMF_Identity` has since been renamed or deleted.

**Pagination contract.** The cursor is an opaque base64 string the server
mints. The client must not parse it; future increments may extend the
encoding. Pagination is forward-only and stable under concurrent inserts
(the cursor encodes `lastSeenScanId`, and the bigint identity PK is
monotonic). When `nextCursor` is `null` the caller has reached the end of
the current view; a subsequent poll with the previously-returned cursor
fetches only items inserted since.

**PII excluded by design.** The list shape carries no email, no
national-id, no passport number, no phone, and no avatar — operational
glance does not need them, and dragging them through every poll inflates
the response without a UX win. A per-scan detail endpoint (out of scope
in this revision) can return the richer envelope when an operator taps a
row.

**Authorisation outcomes.**

| Caller state                                | Outcome                                      |
|---------------------------------------------|----------------------------------------------|
| Operator is assigned to the gate            | `200 OK` with the page                       |
| Operator is **not** assigned to the gate    | `403 GATE_OPERATOR_NOT_ASSIGNED`             |
| Gate id does not exist                      | `404 GATE_NOT_FOUND`                         |
| Cursor is malformed                         | `200 OK` with the first page (cursor reset)  |

## 8. Error catalogue

### 8.1 Routing / auth / state errors (HTTP 4xx / 5xx)

These are *envelope failures* (`success: false`).

| Code | HTTP | Meaning |
|------|------|---------|
| `GATE_INVALID` | 400 | Validation of a gate-management payload failed (code length, name length, direction mode, …) |
| `GATE_NOT_FOUND` | 404 | The addressed gate does not exist |
| `GATE_CODE_DUPLICATE` | 409 | Create or update would collide with an existing gate code |
| ~~`GATE_INACTIVE`~~ | ~~503~~ | **Retired (DEF-STF-008)** — an inactive scan target is denied at HTTP 200 with `GATE_INACTIVE_AT_SCAN` (§7.2.4 as-built note, §8.2) |
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
| `GATE_INACTIVE_AT_SCAN` | The scan target gate is `IsActive = false`. This is the ONLY outcome for an inactive gate (DEF-STF-008): recorded, so the attempt keeps its audit row, and localised so the operator sees "This gate is currently inactive." | 5 |
| `HOLDER_NOT_APPROVED` | The visitor's account is not in `Approved` state | 6 |
| `HOLDER_DISABLED` | The visitor's account is `Disabled` | 7 |
| `HOLDER_LOCKED` | The visitor's account is `Locked` | 8 |
| `PROFILE_TYPE_INACTIVE` | The visitor's `ProfileType` is `IsActive = false` | 9 |
| `OUTSIDE_TIME_WINDOW` | **No longer reserved, and no longer a time window.** Engine step 9.5 now emits it when the badge carries a **closed edition**: an attendee whose `EditionYear` is neither zero nor the open edition's year is refused, which is the only expiry a minted QR has. The code is deliberately reused rather than given a distinct message, because a scan must never tell the holder which half of the check failed. A zero on the record means the attendee predates the column and is left alone rather than locked out by a schema change. | 9.5 |
| `PROFILE_TYPE_NOT_ALLOWED` | The visitor's `ProfileType` is not in the gate's allow-list (or the allow-list filtered empty per L-15) | 11 |
| `BOOKING_REQUIRED_MISSING` | **No longer reserved.** Engine step 11.5 emits it at a **session-hall door** when the attendee is not registered for the session running behind it. It had no writer, so any valid badge opened every hall. It runs after the allow-list because it needs the resolved direction, applies to **entries only** (a departure is never blocked), and is relaxed while session walk-in mode is active. Eligibility is asked by profile, so an attendee with no account is answered on their real registration rather than assumed unregistered. | 11.5 |
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

## 11. Forward-looking seams (reserved at specification time)

| Seam | Reserved at | Future increment |
|------|-------------|------------------|
| Device API-key authentication | §4 (gateAuth) | Flutter app / kiosk increment |
| ~~Time-window constraint~~ | ~~Engine step 9.5 + DenialReasonCode `OUTSIDE_TIME_WINDOW`~~ | **Step 9.5 has a writer, see §8.2** |
| ~~Booking-required constraint~~ | ~~Engine step 11.5 + DenialReasonCode `BOOKING_REQUIRED_MISSING`~~ | **Shipped, see §8.2** |
| ~~Offline queue drain~~ | ~~Design notes §5 + header `X-Idempotent-Replay` + 24h idempotency store~~ | **Shipped, see the as-built note below** |
| Materialised `VisitorPresence` table | Design notes §3.3 fallback | Reporting hardening (only if needed) |

**As-built.** The offline row is no longer a reserved seam. Two operator
endpoints ship it, both `Gates.Operate` and both rate-limited on the operational
policy: `GET /api/v1/app/gates/offline-config`, the snapshot of scanning rules
plus the badge key a device caches so it can judge a badge with no network, and
`GET /api/v1/app/gates/offline-roster?since=`, the attendees an operator's doors
are expecting, pulled whole or as a delta from the previous response's
`issuedAt`. Both are deliberately their own call rather than fields on §7.1,
because each carries something revocable on its own: a secret in the first case,
attendee names and movements in the second. Every verdict a device reaches
offline stays **advisory**: the scan is still queued and §7.2 re-decides it
against live data, so the roster makes the offline answer better, not
authoritative. Their request and response shapes are not specified here; this
document is being corrected for staleness, and writing the contract for two
endpoints it never covered is a specification change rather than a correction.

The remaining seams are *contract-stable*: the wire surface ships in this
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
5. Every denial code in §8.2 is reachable through at least one test and
   carries a bilingual message string. The tests are flat files in
   `tests/SIMF.Api.Tests/`, not the `SIMF.Api.Tests/Gates/*` folder this
   criterion used to name. `GateScanTests.cs` asserts six of the codes
   (`QR_UNKNOWN`, `GATE_INACTIVE_AT_SCAN`, `HOLDER_NOT_APPROVED`,
   `HOLDER_DISABLED`, `PROFILE_TYPE_NOT_ALLOWED`, `OUTSIDE_TIME_WINDOW`),
   `GateRevokedBadgeTests.cs` and `GateScanIdempotencyRecoveryTests.cs`
   assert `HOLDER_DISABLED` and `HOLDER_NOT_APPROVED` on the revocation and
   replay paths, and `GateHallDoorChainTests.cs` asserts
   `BOOKING_REQUIRED_MISSING`. `HOLDER_LOCKED` and `PROFILE_TYPE_INACTIVE`
   have no direct assertion in that set, so the criterion is not yet met in
   full. The rest of the gate suite is `GateFailureCircuitTests.cs` (the
   failure-rate circuit as a unit, not its 429), `GateOfflineRosterTests.cs`,
   `GateOperatorModelTests.cs`, `GateVisitorsListTests.cs`, and
   `AdminGatesTests.cs` plus `AdminGateCurrentlyInsideTests.cs` and
   `GatesExcelTests.cs` for the §6 surface.
6. The XLSX report endpoint streams a binary spreadsheet with the same
   filter set as the JSON report.
7. The permission gate is enforced. A holder of `Gates.Manage` reaches the
   §6 admin surface; a holder of `Gates.Operate` reaches the §7 scan surface
   and `Gates.ViewOwnReports` the operator reports; neither can take the
   other's actions without the corresponding permission. The criterion named
   an "Administrator" and a "GateOperator" **role**, which BUG-018 had already
   corrected in §4: assignment is roles-only but enforcement is per-permission,
   and a gate operator is an approved app account, not an RBAC role name.

## 13. Open items

| ID | Item | Resolution target |
|----|------|-------------------|
| OI-1 | ~~Confirm whether `currently-inside` returns a paged or unbounded list once event volumes are real.~~ **Resolved:** the report moved onto the shared grid seam as `POST .../currently-inside/list`, so it is server-paged by contract (fallback 25, cap 200) and no longer depends on the load test to decide. | Resolved |
| OI-2 | Whether the XLSX export should also emit `Content-Disposition` charset hints for Arabic filenames. | Pre-event smoke |

---

End of document.
