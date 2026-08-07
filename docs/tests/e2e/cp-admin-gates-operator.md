# E2E test catalogue — Gate operator console (`/admin/gates/operator`)

| | |
|--|--|
| **Page** | [`cp/admin-gates-operator.md`](../../pages/cp/admin-gates-operator.md) |
| **Route** | `/admin/gates/operator` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-07-26 (DEF-CHK-004 advisory notice) |

> **Permission gate.** The page carries `@attribute [RequirePermission(PermissionCatalog.Gates.Operate)]`
> (`"Gates.Operate"`, baseline role `GateOperator`). The two backing API
> endpoints are gated `Gates.Operate` (scan + assignments) and
> `Gates.ViewOwnReports` (the daily report). `superadmin` holds the `*`
> wildcard so it passes all three; an admin who holds neither lands on
> `/not-permitted`.
>
> **Key behavioural fact.** A *denial* is NOT an HTTP error — the scan
> request returns **HTTP 200** with `Outcome = Denied` and a
> `DenialReasonCode`. Only operational faults (gate not found / not
> assigned / inactive / idempotency conflict / failure-circuit open) raise
> 4xx/5xx. The console renders a denial as a red `SimfAlert`, not a toast.
>
> **Advisory notice on an ALLOWED scan (DEF-CHK-004, 2026-07-26).** A scan on a
> **hall-door** gate (`Gate.HallId` set) also feeds `HallAttendance` for the
> session live in that hall. Three cases admit the holder while recording **no
> attendance** — no session is live in that hall, a fixed **Out** gate scan finds
> no open attendance row to close, or the arrival's insert is rejected by the
> store and no open row can be re-read — and all of them used to be completely
> silent. `GateScanResponse` now carries an additive, already-localized
> `NoticeMessage` (same shape as `DenialMessage`, resolved from
> `Accept-Language`), which the console renders as an amber `SimfAlert`
> **underneath** the green Allowed alert. The wording deliberately does not name
> a single cause (the server reports all three identically; the exact reason is
> in the server log). It is `null` on every ordinary scan and on every perimeter
> gate, and it never changes the allow/deny outcome.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-GOP-001 | Golden path — pick gate → scan a valid QR → Allowed alert + report increments | happy | P0 | _to author_ |
| E2E-GOP-002 | Gate picker — multi-assignment operator switches gate, report reloads | happy | P1 | _to author_ |
| E2E-GOP-003 | Auto-select — single-assignment operator lands with the gate pre-selected | happy | P1 | _to author_ |
| E2E-GOP-004 | Denial (QR_UNKNOWN) — unknown QR → 200 + red Denied alert | happy | P0 | _to author_ |
| E2E-GOP-005 | Denial (PROFILE_TYPE_NOT_ALLOWED) — disallowed profile → red Denied alert | happy | P1 | _to author_ |
| E2E-GOP-006 | "My day so far" report table renders + Take(50) cap | happy | P1 | _to author_ |
| E2E-GOP-007 | Empty state — operator with no active assignments → `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-GOP-008 | Auth gate — admin lacking `Gates.Operate` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-GOP-009 | Validation — blank/whitespace QR → Scan is a no-op (no POST) | error | P1 | _to author_ |
| E2E-GOP-010 | Conflict — reused idempotency key, different payload → 409 fallback alert | error | P1 | _to author_ |
| E2E-GOP-011 | Not-assigned — scan a gate you lost assignment to → 403 fallback alert | error | P1 | _to author_ |
| E2E-GOP-012 | Resilience — API 500 on `/scans` → bilingual fallback alert, field cleared check | resilience | P2 | _to author_ |
| E2E-GOP-013 | RTL render — Arabic toggle mirrors banner, picker, alert, report table | i18n | P1 | _to author_ |
| E2E-GOP-014 | DEF-CHK-004 — hall-door gate scanned with no session live → Allowed **plus** an amber advisory alert | happy | P0 | authored ✓ (API `Hall_door_gate_with_no_live_session_returns_an_allowed_scan_carrying_a_notice`) |
| E2E-GOP-015 | DEF-CHK-004 — a scan bound to a live session, and any perimeter-gate scan, carry no advisory | happy | P1 | authored ✓ (API `Hall_door_gate_bound_to_a_live_session_carries_no_notice`, `Perimeter_gate_carries_no_notice`) |
| E2E-GOP-016 | DEF-CHK-004 — fixed **Out** gate scanned for someone with no open attendance row → Allowed **plus** the amber advisory | edge | P1 | authored ✓ (API `Fixed_out_gate_with_no_open_row_carries_the_advisory_notice`, `Fixed_out_gate_that_closes_an_open_row_carries_no_notice`) |
| E2E-GOP-017 | DEF-CHK-004 — a check-IN whose attendance insert the store rejects → Allowed, and the chain reports "not recorded" (not success) | resilience | P1 | authored ✓ (API `Gate_door_arrival_that_persisted_no_row_does_not_report_attendance_recorded`) |
| E2E-GOP-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-GOP-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-GOP-014 — DEF-CHK-004: allowed, but no attendance recorded

```gherkin
Scenario: a hall-door gate scanned outside every session window
  Given the operator is assigned to gate "G-HALL-A" whose HallId is set to "Majlis A"
  And no session is running in "Majlis A" right now (nor within the 15 min grace)
  And an Approved visitor's badge QR is "AB12CD34EF56"
  When the operator scans that badge
  Then POST /account/api/gates/{gateId}/scans returns HTTP 200
  And the response Outcome is Allowed with DenialReasonCode = null
  And the response carries NoticeMessage
      "Entry allowed, but no session attendance was recorded for this scan."
      / "تم السماح بالدخول، ولكن لم يتم تسجيل حضور الجلسة لهذا المسح."
  And the console renders the green Allowed alert AND an amber advisory alert beneath it
  And a GateScan row is written; NO HallAttendance row is written
  # Before DEF-CHK-004 the operator saw only "Allowed" and the attendance was
  # lost silently. The allow/deny outcome is deliberately unchanged.
```

### E2E-GOP-015 — DEF-CHK-004: no advisory on the normal paths

```gherkin
Scenario: an ordinary scan reports nothing extra
  Given a session IS live in the hall behind gate "G-HALL-A"
  When the operator scans an Approved visitor's badge
  Then the response Outcome is Allowed and NoticeMessage is null
  And a HallAttendance row is opened for that session
  When the same visitor is scanned at the perimeter gate "G-MAIN" (HallId null)
  Then the response Outcome is Allowed and NoticeMessage is null
  And no advisory alert renders in the console
```

### E2E-GOP-016 — DEF-CHK-004: a check-out that closes nothing

```gherkin
Scenario: a fixed Out gate scanned for someone who never checked in
  Given gate "G-HALL-A-OUT" has DirectionMode = Out and HallId = "Majlis A"
  And a session IS live in "Majlis A"
  And an Approved visitor has NO open HallAttendance row for that session
  When the operator scans that visitor's badge at "G-HALL-A-OUT"
  Then the response Outcome is Allowed
  And the response carries the same amber advisory NoticeMessage
  And no HallAttendance row exists for that session
  # The check-out branch closed nothing, so nothing was recorded. It used to
  # report success, and the operator read the plain "Allowed" as "counted".

Scenario: the same gate when there IS an open row
  Given the visitor was checked in first at the fixed In gate "G-HALL-A-IN"
  When the operator scans them at "G-HALL-A-OUT"
  Then the response Outcome is Allowed and NoticeMessage is null
  And their HallAttendance row now has a non-null Leave
```

### E2E-GOP-017 — DEF-CHK-004: a check-IN whose attendance insert never lands

```gherkin
Scenario: the store rejects the arrival insert
  Given gate "G-HALL-A-IN" has DirectionMode = In and HallId = "Majlis A"
  And a session IS live in "Majlis A"
  And the HallAttendance insert is rejected by the store
      # a deadlock victim, a command timeout, or the one-open-row race
      # whose rival row has already closed
  When the operator scans an Approved visitor's badge at "G-HALL-A-IN"
  Then the response Outcome is Allowed — the person is still admitted
  And no HallAttendance row exists for that session
  And the chain reports "attendance not recorded", so the amber advisory renders
  And the rejected write is logged server-side with its reason
  # The arrival branch used to return success unconditionally, so this scan
  # reported a recorded attendance it did not have. Simulated at the DbContext
  # boundary in the API test — no store fault is deterministic against LocalDB.
```

### E2E-GOP-001 — Golden path

```gherkin
Feature: Gate operator console golden path
  As a gate operator (or superadmin holding Gates.Operate)
  I want to scan a visitor QR at my assigned gate
  So that the system records the entry/exit and decides allow vs deny

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And the operator is signed in via /login + /login/totp (Get-Totp helper)
  And the operator is assigned to at least one active gate "G-MAIN — Main entrance (Both)"
  And an Approved visitor exists whose ProfileType is allowed at that gate, with QR id "AB12CD34EF56"
  And they have landed on /admin/gates/operator

Scenario: Scan a valid QR and see an Allowed result
  Given GET /account/api/gates/my-assignments returned the active gate list
  And the gate "<select>" shows "G-MAIN — Main entrance (Both)" pre-selected
  And the "My day so far" summary reads "0 allowed · 0 denied"
  When the operator types "ab12cd34ef56" into the "QR id" field
  And clicks "Scan"
  Then the button shows the "Scanning…" loading label while busy
  And the BFF forwards POST /account/api/gates/{gateId}/scans with body Qr="AB12CD34EF56" (trimmed + upper-cased), Source=Simulator, a fresh IdempotencyKey GUID
  And the API returns HTTP 200 with ApiResult.Data.Outcome = Allowed
  Then a green SimfAlert appears reading "Allowed — {DisplayName} ({ProfileTypeName}) — CheckIn"
  And the "QR id" field is cleared
  And GET /account/api/gates/my-reports/today?gateId={gateId} is re-fetched
  And the "My day so far" summary now reads "1 allowed · 0 denied"
  And a new row appears at the top of the report table with Outcome=Allowed, Direction=CheckIn, Visitor={DisplayName}, Reason="—"
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-gates-operator-golden-before.png`
- Screenshot after: `docs/screenshots/cp-admin-gates-operator-golden-after.png`
- Console errors: 0 expected (the JS module calls run only in `OnAfterRenderAsync(firstRender)`, never on the SSR prerender pass)
- Network: every `/account/api/gates/...` call returns 200 — `my-assignments` (GET), `{gateId}/scans` (POST), `my-reports/today` (GET ×2: initial + post-scan)
- Audit/data: a `GateScan` row is written with `Outcome=Allowed`, `Direction=CheckIn`, `Source=Simulator`, the operator's `sub`, and the resolved `UserProfileId`

### E2E-GOP-002 — Gate picker switches gate

```gherkin
Scenario: Multi-assignment operator switches the active gate
  Given the operator is assigned to two active gates "G-MAIN — Main entrance (Both)" and "G-VIP — VIP lounge (In)"
  And the picker is pre-selected on the first active assignment
  When the operator changes the "<select>" to "G-VIP — VIP lounge (In)"
  Then _selectedGateId rebinds to the G-VIP gate id
  And the next scan POSTs to /account/api/gates/{G-VIP gateId}/scans
  And the report re-fetches with GET /account/api/gates/my-reports/today?gateId={G-VIP gateId}
  And only G-VIP's rows render in "My day so far"
```

### E2E-GOP-003 — Auto-select on a single assignment

```gherkin
Scenario: Single-assignment operator lands with the gate already chosen
  Given the operator is assigned to exactly one active gate "G-MAIN — Main entrance (Both)"
  When the page finishes its first render
  Then _selectedGateId equals that gate's id (FirstOrDefault active assignment)
  And the operator can scan immediately without touching the picker
  And the initial report fetch carries ?gateId={G-MAIN gateId}
```

### E2E-GOP-004 — Denial: QR_UNKNOWN

```gherkin
Scenario: An unknown QR records a denial without an HTTP error
  Given the picker is on "G-MAIN — Main entrance (Both)"
  When the operator types "ZZZZ99999999" into "QR id"
  And clicks "Scan"
  Then POST /account/api/gates/{gateId}/scans returns HTTP 200 (NOT a 4xx)
  And ApiResult.Data.Outcome = Denied with DenialReasonCode = QrUnknown (0)
  And a red SimfAlert appears reading "Denied — QrUnknown — {localised denial message}"
  And no UserProfile span is shown (the QR resolved to no profile)
  And the "My day so far" summary increments the denied count by 1
```

### E2E-GOP-005 — Denial: PROFILE_TYPE_NOT_ALLOWED

```gherkin
Scenario: A visitor whose profile type is not allowed at the gate is denied
  Given the gate "G-VIP — VIP lounge (In)" has an allow-list that excludes the visitor's ProfileType
  And an Approved visitor with QR id "GENERAL00001" of an excluded ProfileType exists
  When the operator selects "G-VIP — VIP lounge (In)"
  And scans "GENERAL00001"
  Then the response is HTTP 200, Outcome = Denied, DenialReasonCode = ProfileTypeNotAllowed (7)
  And the red SimfAlert reads "Denied — ProfileTypeNotAllowed — {localised message} ({DisplayName} / {ProfileTypeName})"
  And the UserProfile span IS shown because the holder resolved (only the gate policy denied them)
```

### E2E-GOP-006 — Report table renders and caps at 50

```gherkin
Scenario: "My day so far" lists today's scans, newest first, capped at 50
  Given the operator has recorded 60 scans at the selected gate today
  When the report loads (GET /account/api/gates/my-reports/today?gateId={gateId})
  Then the summary reads "{allowed} allowed · {denied} denied" from Totals
  And the table renders exactly 50 rows (Rows.Take(50))
  And each row shows ScannedAt as HH:mm:ss, Outcome, Direction, Visitor (or "—"), Reason (DenialReasonCode or "—")
  And rows with Outcome=Allowed show Reason="—"
```

### E2E-GOP-007 — Empty state

```gherkin
Scenario: An operator with no active gate assignments sees the empty state
  Given the operator holds Gates.Operate but has zero active gate assignments
  When they open /admin/gates/operator
  Then GET /account/api/gates/my-assignments returns an empty list
  And the SimfEmptyState renders with title "You have no active gate assignments. Please contact an administrator."
  And neither the gate picker, the QR field, the Scan button, nor the report table is shown
  And no /account/api/gates/{gateId}/scans request can fire
```

### E2E-GOP-008 — Auth gate

```gherkin
Scenario: A signed-in admin lacking Gates.Operate is denied
  Given a signed-in admin whose role grants neither Gates.Operate nor the * wildcard
  When they navigate to /admin/gates/operator
  Then RequirePermission(PermissionCatalog.Gates.Operate) fails
  And they land on /not-permitted with HTTP 200
  And no /account/api/gates/my-assignments request fires
  And the "Gate operator" nav item is not shown to them (CpNavigation RequiredPermission = Gates.Operate)
```

### E2E-GOP-009 — Validation: blank QR is a no-op

```gherkin
Scenario: Clicking Scan with an empty or whitespace QR does nothing
  Given the picker has a selected gate
  And the "QR id" field is empty (or contains only spaces)
  When the operator clicks "Scan"
  Then OnScanAsync returns immediately (guard: _selectedGateId != Empty AND _qrInput not whitespace)
  And NO POST /account/api/gates/{gateId}/scans request fires
  And no SimfAlert appears and the report is unchanged
```

### E2E-GOP-010 — Conflict: idempotency key reuse

```gherkin
Scenario: Reusing an idempotency key with a different payload returns 409
  Given a prior scan was recorded with idempotency key "K-123" and Qr "AB12CD34EF56"
  When a scan is submitted reusing key "K-123" but Qr "FF99FF99FF99"
  Then POST /account/api/gates/{gateId}/scans returns HTTP 409
  And ApiResult.Error.Code = "IDEMPOTENCY_KEY_CONFLICT"
  And because envelope.Success is false, the page builds a synthetic Denied GateScanResponse
  And a red SimfAlert surfaces the bilingual MessageForCurrentCulture()
    "An idempotency key was reused with a different payload." / "تم إعادة استخدام مفتاح idempotency بحمولة مختلفة."
  And the report is NOT re-fetched (only the success branch reloads it)
```

> Note: the console always sends a fresh `Guid.NewGuid()` idempotency key per
> click, so this conflict is driven at the API layer / via the staff-app
> replay path. Exercise it with a direct `/account/api/gates/{gateId}/scans`
> POST reusing a known key, or against the lower-layer integration test.
>
> G-6 (shared contract): the Flutter staff app now mints the same fresh
> per-scan UUIDv4 idempotency key (`randomUuidV4()`) that this console does
> (`Guid.NewGuid()`), so both consoles share one idempotency policy — a genuine
> re-entry is a new `GateScan`, and only a replay of the *same* key is a 409.

### E2E-GOP-011 — Not assigned to the gate

```gherkin
Scenario: Scanning a gate the operator is no longer assigned to returns 403
  Given the operator had the gate cached in the picker
  But an administrator removed that gate assignment in the meantime
  When the operator scans any QR at that gate
  Then POST /account/api/gates/{gateId}/scans returns HTTP 403
  And ApiResult.Error.Code = "GATE_OPERATOR_NOT_ASSIGNED"
  And a red SimfAlert reads "You are not assigned to this gate." / "أنت غير معيّن لهذه البوابة."
```

### E2E-GOP-012 — Server 500 resilience

```gherkin
Scenario: A 500 on the scan endpoint shows the bilingual fallback alert
  Given the API is forced to return 500 on POST /account/api/gates/{gateId}/scans (e.g. DB down)
  When the operator scans a QR
  Then envelope.Success is false (or the call throws and is surfaced as a failed envelope)
  And a red SimfAlert appears reading the fallback "The operation could not be completed." / "تعذّر إتمام العملية."
    (Admin.Gates.Fallback) when no server message is available
  And the loading state clears (_busy reset in the finally block)
  And no report re-fetch fires
```

### E2E-GOP-013 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the operator console
  Given the operator is on /admin/gates/operator in English
  When they switch the language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "مشغل البوابة"
  And the picker label reads "البوابة" and the QR label reads "رمز QR" with hint "امسح أو أدخل الرمز المكوّن من 12 حرفاً."
  And the Scan button reads "مسح" (loading "جارٍ المسح…")
  And after a scan the summary reads "{0} مسموح · {1} مرفوض"
  And the report headers read الوقت / النتيجة / الاتجاه / الزائر / السبب
  And the layout mirrors (controls and table flow right-to-left)
```

---

## Implementation notes

- **Lower-layer coverage.** `tests/SIMF.Api.Tests/GateScanTests.cs` already
  covers the constraint engine and protocol at the API layer (no browser):
  step 2 operator-not-assigned → 403 `GATE_OPERATOR_NOT_ASSIGNED`; step 3
  `QR_UNKNOWN` denial recorded as 200; step 6 `HOLDER_NOT_APPROVED`; step 11
  `PROFILE_TYPE_NOT_ALLOWED` (+ L-15 empty-filtered-list denies all); step 12
  the 5-second duplicate absorption + Both-mode direction inference; and §9
  idempotency replay + the 409 conflict. The E2E scenarios above add the
  CP UI + BFF-forwarding layer (the `simfAccount.getJson/postJson` JS bridge,
  the `SimfAlert` rendering, the picker auto-select, and the report reload)
  that the API tests cannot reach.
- **BFF routes** are in `src/ControlPanel/.../Endpoints/AccountEndpoints.cs`:
  `GET /account/api/gates/my-assignments`, `POST /account/api/gates/{gateId:guid}/scans`,
  `GET /account/api/gates/my-reports/today[?gateId=]` — each forwards to
  `SimfAdminClient` with the cookie-stored `access_token`.
- **API endpoints** are in `src/Backend/SIMF.Api/Endpoints/Gates/OperatorGateEndpoints.cs`.
  The scan endpoint is rate-limited (`RequireRateLimiting("auth")`); a denial
  is a 200, only operational faults map to 403/404/409/429/503.
- **Manual smoke as canonical run today.** Until Playwright is adopted, drive
  these via a Chrome DevTools MCP session per the SIMF smoke template — sign in
  via the steps above and capture screenshots into
  `docs/screenshots/cp-admin-gates-operator-*.png`. Convert each Gherkin block
  to a `.feature` + step-definition when the runner lands; the steps are
  already tool-agnostic.

---

_Last reviewed:_ 2026-07-27 by Claude (DEF-CHK-004 — the recorded-attendance signal is now honest on the ARRIVAL path too, E2E-GOP-017). Prior: 2026-07-27 (advisory also covers a check-out that closes nothing, E2E-GOP-016); 2026-07-26 (DEF-CHK-004 advisory NoticeMessage); 2026-06-02 (E2E catalogue rebuild).
