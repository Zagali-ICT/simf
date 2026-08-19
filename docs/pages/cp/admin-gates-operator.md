# Gate operator console - `/admin/gates/operator`

| | |
|--|--|
| **Route** | `/admin/gates/operator` (`@page` directive, `GateOperatorConsole.razor` line 3) |
| **Layout** | `CpShellLayout` (`@layout CpShellLayout`) |
| **Surface** | Control Panel |
| **Audience** | Holders of `Gates.Operate`. `PermissionCatalog.All` grants it to the baseline roles `GateOperatorOrSecurity` = `[AppRoles.GateOperator, AppRoles.SecurityTeam]`; Administrator passes on the `*` wildcard. |
| **Auth** | Page: `@attribute [RequirePermission(PermissionCatalog.Gates.Operate)]`. BFF: the `/account/api` group carries `.RequireAuthorization()` only. API: `Policies(nameof(AuthorizationPolicies.RequireApprovedAccount), PermissionCatalog.PolicyFor(...))` per endpoint - `Gates.Operate` on the assignments + scan endpoints, `Gates.ViewOwnReports` on the daily report. |
| **Pattern** | Not a CRUD list page. A single-surface operator console: gate picker, one input, one action, one result card, one read-only day log. |
| **Status** | Real (Gate Module, D-148 kickoff / D-149 shipped). |
| **Implements use case(s)** | Unverified - `docs/SIMF-UCS-001-Use-Case-Specifications.md` line 106 carries `UC-35` "Check an attendee in at a hall door" (Staff / System, FR-305), which a hall-door gate scanned from this console does perform, but no UCS-001 entry names this console or perimeter-gate scanning. See section 10. |
| **Backend endpoints** | `GET /account/api/gates/my-assignments`, `POST /account/api/gates/{gateId:guid}/scans`, `GET /account/api/gates/my-reports/today[?gateId=]` |
| **Source file** | [`GateOperatorConsole.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/GateOperatorConsole.razor) + [`GateOperatorConsole.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/GateOperatorConsole.razor.cs) |
| **Tests** | E2E: [`docs/tests/e2e/cp-admin-gates-operator.md`](../../tests/e2e/cp-admin-gates-operator.md) (E2E-GOP-001..017 + ELS-001/002). Source ratchet: `tests/SIMF.ControlPanel.Tests/CpMarkupHygieneTests.cs` (`Gate_operator_console_spaces_its_action_row_with_a_class_not_an_inline_style`). API layer: `tests/SIMF.Api.Tests/GateScanTests.cs`, `tests/SIMF.Api.Tests/GateHallDoorChainTests.cs`. |
| **Last reviewed** | 2026-08-19 |

---

## 1. Purpose

This is the door-side console: the screen an operator keeps open while people
arrive, so a badge can be turned into an allow-or-deny decision that the system
records. The operator picks which of their assigned gates they are standing at,
enters or scans a badge QR id, and reads one card that says whether the holder
may pass and, if not, why. Underneath it the console keeps a running log of that
operator's own day at that gate, so they can see what they have processed
without going to a report page. It is deliberately narrow: it does not create,
edit or delete anything, and an operator sees only gates an administrator has
assigned to them. The `ScanSource.Simulator` value the page sends is documented
in the enum as "The Control Panel scan simulator (dev / staging only)" - the
production door device is the Flutter staff app (`ScanSource.MobileApp`), and
this console is its browser-side counterpart.

## 2. Audience + permissions

- **Who can reach it:** anyone whose `perm` claim contains `Gates.Operate` or
  the wildcard. `PermissionCatalog.All` line 1237 registers
  `new(Gates.Operate, "Gates", "Operate", "Operate a gate", GateOperatorOrSecurity)`,
  and `GateOperatorOrSecurity` is `[AppRoles.GateOperator, AppRoles.SecurityTeam]`.
  `Gates.Operate` is also in `StaffAppPermissions` and `ModeratorAppPermissions`,
  which is how a partner-side Staff or Moderator app account gains gate scanning
  without being a Control Panel admin.
- **Who can edit/write on it:** the same set. The only write on the page is the
  scan itself, which is gated by the page's own code.
- **Authorisation gates, all three layers:**
  - Page: `@attribute [RequirePermission(PermissionCatalog.Gates.Operate)]`.
    `RequirePermissionAttribute` is an `AuthorizeAttribute` whose `Policy` is
    `PermissionCatalog.PolicyFor(code)`; `PermissionAuthorizationHandler` succeeds
    on a `perm` claim equal to the code or to `PermissionCatalog.Wildcard`.
  - Nav: `CpNavigation.cs` line 158 -
    `new("Module.GatesOperator", "/admin/gates/operator", RequiredPermission: PermissionCatalog.Gates.Operate, Icon: "scan")`,
    so the side-menu item is hidden from an admin who lacks the code.
  - BFF: `routes.MapGroup("/account/api").RequireAuthorization()`. That is
    authentication only - the three gate routes carry no permission check of
    their own, and each one re-reads the cookie's `access_token` and returns
    `Results.Unauthorized()` when it is absent.
  - API: each FastEndpoint declares `RequireApprovedAccount` plus its own
    permission policy. The scan and assignments endpoints require
    `Gates.Operate`; the daily report requires `Gates.ViewOwnReports`. An
    operator holding only `Gates.Operate` can therefore scan but will get an
    unsuccessful envelope on the report call.
- **What an unauthenticated user sees:** `Routes.razor` renders
  `AuthorizeRouteView`'s `<NotAuthorized>` for both the unauthenticated and the
  authenticated-but-forbidden case and branches on the authentication state. A
  signed-out visitor gets `<RedirectToLogin />`, which is
  `Nav.NavigateTo("/login", forceLoad: true)`. A signed-in admin without the
  permission gets `<RedirectToNotPermitted />`, which is
  `Nav.NavigateTo("/not-permitted")` with no force-load - the component's own
  comment records why: the circuit and the session are both healthy, and a
  force-reload onto the sign-in form "reads as 'you were logged out', not 'you
  may not open this page'".

## 3. Screenshots

No screenshots have been captured for this page. The table below is the agreed
file-name convention and the capture targets, not a record of existing files.

| State | File | Captured |
|-------|------|----------|
| Default (assignments loaded) | `docs/screenshots/cp-admin-gates-operator-default.png` | Not captured |
| Empty state (no active assignments) | `docs/screenshots/cp-admin-gates-operator-empty.png` | Not captured |
| Allowed result | `docs/screenshots/cp-admin-gates-operator-allowed.png` | Not captured |
| Denied result | `docs/screenshots/cp-admin-gates-operator-denied.png` | Not captured |
| Advisory notice under an Allowed result | `docs/screenshots/cp-admin-gates-operator-notice.png` | Not captured |
| RTL (Arabic) | `docs/screenshots/cp-admin-gates-operator-rtl.png` | Not captured |
| Error state (failed envelope) | `docs/screenshots/cp-admin-gates-operator-error.png` | Not captured |

## 4. UI affordances

### 4.1 Banner / page header

`<SimfBanner Title="@L["Admin.Gates.Operator.Title"]" />` - title only. No
`Subtitle`, no `Actions` slot. `SimfBanner` renders the title as
`<h1 class="simf-banner__title">`, which is the element `Routes.razor`'s
`<FocusOnNavigate Selector="h1" />` moves focus to on navigation. The browser tab
reads `<PageTitle>@L["Admin.Gates.Operator.Title"] · SIMF</PageTitle>`.

The whole console body sits in `<div class="simf-page-wide"><div class="simf-surface">`.
Both classes are defined in `src/Shared/SIMF.Components/wwwroot/css/simf-components.css`.
The page carries no inline style at all, which is a ratcheted rule rather than a
convention: `CpMarkupHygieneTests` asserts `Assert.DoesNotContain("style=", source)`
against this exact file, because BUG-026 found it carrying
`style="margin-top:1rem"`. The spacing now comes from
`.simf-form__actions--tight`, and a second test pins that modifier to
`margin-block-start: var(--space-4)` so it cannot regress to a magic value.

### 4.2 Toolbar (CRUD pages only)

N/A - not a CRUD list page. There is no toolbar, no multiselect, no Add / Edit /
Details / Delete, no Copy / Paste / Duplicate, and no Import / Export. The page
has exactly two interactive controls, both described in 4.5.

### 4.3 Grid columns (CRUD pages only)

N/A - not a CRUD list page. The page hosts no `SimfDataGrid`. The "My day so far"
log is a plain `<table class="simf-table">` written by the page itself, so it
carries none of the grid's sorting, filtering, selection or per-button permission
parameters (`AddPermission` / `EditPermission` / `DeletePermission` and the rest
do not apply here). Its five columns are:

| Column | Header key | Source field | Rendering |
|--------|-----------|--------------|-----------|
| At | `Admin.Gates.Operator.Report.At` | `row.ScannedAt` | `row.ScannedAt.FormatSaudi("hh:mm:ss tt")` - 12-hour with an AM/PM marker, invariant culture. `SaudiTime.FormatSaudi` does no conversion; the stored value is already Saudi local. |
| Outcome | `Admin.Gates.Operator.Report.Outcome` | `row.Outcome` | The `ScanOutcome` enum name, unlocalised. |
| Direction | `Admin.Gates.Operator.Report.Direction` | `row.Direction` | The `ScanDirection` enum name, unlocalised. |
| Visitor | `Admin.Gates.Operator.Report.Visitor` | `row.VisitorDisplayName` | Falls back to `"—"` when null. |
| Reason | `Admin.Gates.Operator.Report.Reason` | `row.DenialReasonCode` | `row.DenialReasonCode?.ToString() ?? "—"`, so an allowed row shows the dash. |

Rows come from `OperatorDailyReport.Rows` (`IReadOnlyList<OperatorScanRow>`) and
are rendered through `.Take(50)`. Above the table, `Totals` is formatted into
`Admin.Gates.Operator.MyReportSummary` (`"{0} allowed · {1} denied"`). The
contract also carries `DenialBreakdown` (`IReadOnlyList<OperatorDenialBucket>`),
which this page does not render.

### 4.4 Pager

N/A - there is no pager, no page-size selector and no "Showing X-Y of Z"
caption. The log is a fixed client-side cap of 50 rows
(`_report.Rows.Take(50)`) over whatever the endpoint returned for today; there is
no way to page past it from this console.

### 4.5 Form fields

| Field | Type | Required | MaxLength | Validation | Locale |
|-------|------|----------|-----------|------------|--------|
| Gate | `<select class="simf-select" @bind="_selectedGateId">` | yes, in effect - `OnScanAsync` returns early when `_selectedGateId == Guid.Empty` | n/a | Options are `_assignments.Where(a => a.IsActive)` only, so an assignment whose `IsActive` is false cannot be chosen. Option text is `@a.Code — @a.Name (@a.DirectionMode)`. `disabled="@_busy"` while a scan is in flight. | Label `Admin.Gates.Operator.PickGate` (EN "Gate" / AR "البوابة"). The option text itself is not localised: it uses `OperatorGateAssignment.Name`, never `NameArabic`, and appends the raw `DirectionMode` enum name. |
| QR id | `SimfTextField` with `@bind-Value="_qrInput"`, `Disabled="_busy"` | yes, in effect - `OnScanAsync` returns early on `string.IsNullOrWhiteSpace(_qrInput)` | `MaxLength="32"` | No client validator. The value is `Trim().ToUpperInvariant()`-ed before being sent. | Label `Admin.Gates.Operator.QrLabel` (EN "QR id" / AR "رمز QR"); helper `Admin.Gates.Operator.QrHint` (EN "Scan or type the 12-character QR id." / AR "امسح أو أدخل الرمز المكوّن من 12 حرفاً."). |

Two things about the QR field are worth stating plainly because they look like
contradictions in the source and are not:

- The input accepts up to 32 characters while the helper text says 12. Both are
  what the source says. Server-side the cap is different again: `GateOperatorService`
  denies a normalised value longer than `QrIdAtScanMaxLength = 96` as
  `DenialReasonCode.QrUnknown` rather than letting it truncate the append-only
  scan row on insert.
- `SimfTextField` is used here outside any `<EditForm>`, so no `EditContext`
  cascades. `SimfFieldBase.ErrorMessage` returns null when `EditContext` is null,
  which means the component renders its helper text and never a field-level
  validation message on this page. This is the established CP pattern rather than
  an oversight - 28 CP pages use `SimfTextField` with no surrounding `EditForm`.

The single action is `<SimfButton Loading="_busy" LoadingLabel="@L["Admin.Gates.Operator.Scanning"]" OnClick="OnScanAsync">`
carrying `Admin.Gates.Operator.ScanButton`.

**Result cards.** After a scan, `_last` drives one or two `SimfAlert`s:

| Condition | Variant passed | Content |
|-----------|----------------|---------|
| `_last.Outcome == ScanOutcome.Allowed` | `success` | `Admin.Gates.Operator.Allowed` + `_last.UserProfile?.DisplayName ?? "?"` + `_last.UserProfile?.ProfileTypeName ?? "?"` + `_last.Direction` |
| otherwise | `error` | `Admin.Gates.Operator.Denied` + `_last.DenialReasonCode` + `_last.DenialMessage`, plus a `<span>` with the display name and profile-type name when `_last.UserProfile is not null` |
| `!string.IsNullOrWhiteSpace(_last.NoticeMessage)` | `warning` | `_last.NoticeMessage`, rendered underneath whichever card above applies |

`SimfAlert` only branches on `"error"` and `"success"`; every other value,
`"warning"` included, falls through to the `else` arm and renders
`simf-alert--info` with the `info` icon and `role="status" aria-live="polite"`.
There is no `.simf-alert--warning` rule in `simf-components.css`. The advisory
notice therefore renders in the info style, not an amber one - see section 7.

**Empty state.** When `_assignments.Count == 0` the page renders
`<SimfEmptyState Title="@L["Admin.Gates.Operator.NoAssignments"]" />` and nothing
else - no picker, no field, no button, no report.

## 5. Data flow

```
Page load (interactive render only)
  OnAfterRenderAsync(firstRender)
    -> JS simfAccount.getJson("/account/api/gates/my-assignments")
    -> BFF MapGet("/gates/my-assignments") : cookie access_token -> SimfAdminClient.ListMyGateAssignmentsAsync
    -> API GET /api/v1/app/gates/my-assignments (MyAssignmentsEndpoint)
    -> IGateOperatorService.ListMyAssignmentsAsync(User.ActorId())
    -> ApiResult<IReadOnlyList<OperatorGateAssignment>>
    -> _assignments; _selectedGateId = first assignment with IsActive, else Guid.Empty
  then LoadReportAsync() -> StateHasChanged()

Scan click
  OnScanAsync guard (not busy, gate chosen, QR not blank)
    -> JS simfAccount.postJson("/account/api/gates/{gateId}/scans", GateScanRequest)
    -> BFF MapPost("/gates/{gateId:guid}/scans") -> SimfAdminClient.PostScanAsync
    -> API POST /api/v1/app/gates/{gateId}/scans (PostScanEndpoint)
    -> IGateOperatorService.RecordScanAsync(GateScanContext)  [13-step constraint engine]
    -> GateScan row written (allowed or denied) -> ApiResult<GateScanResponse>
    -> success: _last = data, _qrInput cleared, LoadReportAsync()
    -> failure: _last = synthetic Denied response carrying the error message
```

`simfAccount.getJson` / `postJson` are plain `fetch` calls with
`credentials: 'same-origin'`; the cookie is the CP session and the browser never
sees the bearer token. When the response body is not a readable envelope the JS
helper synthesises one with `success: false` and `error.code = 'BAD_RESPONSE'`.

The JS calls run in `OnAfterRenderAsync(firstRender)` rather than
`OnInitializedAsync`, and the code comment says why: "The simfAccount JS module
is only available once the interactive Blazor connection is up - running these
calls in OnInitializedAsync would throw on the SSR prerender pass and surface
Blazor's unhandled-error banner."

Every backend call the page makes:

| When | CP (BFF) call | ApiClient method | API endpoint + policy | Request body | Response shape |
|------|---------------|------------------|-----------------------|--------------|----------------|
| First interactive render | `GET /account/api/gates/my-assignments` | `SimfAdminClient.ListMyGateAssignmentsAsync` (base `api/v1/app/gates/`) | `GET /api/v1/app/gates/my-assignments` - `RequireApprovedAccount` + `PolicyFor(Gates.Operate)`. No rate-limit policy. | none | `ApiResult<IReadOnlyList<OperatorGateAssignment>>` |
| First interactive render, and after every **successful** scan | `GET /account/api/gates/my-reports/today` or `...?gateId={_selectedGateId}` | `SimfAdminClient.GetMyDailyReportAsync` | `GET /api/v1/app/gates/my-reports/today` - `RequireApprovedAccount` + `PolicyFor(Gates.ViewOwnReports)`. No rate-limit policy. | none (`gateId` is a query value) | `ApiResult<OperatorDailyReport>` |
| Scan button | `POST /account/api/gates/{gateId:guid}/scans` | `SimfAdminClient.PostScanAsync` | `POST /api/v1/app/gates/{gateId:guid}/scans` - `RequireApprovedAccount` + `PolicyFor(Gates.Operate)` + `RequireRateLimiting(RateLimitOptions.OperationalPolicy)` (`"operational"`) | `GateScanRequest { Qr, IdempotencyKey, Source }` | `ApiResult<GateScanResponse>` |

The scan body the console builds is exactly three fields:

- `Qr = _qrInput.Trim().ToUpperInvariant()`. The server normalises again -
  `QrId.Normalise` is "Trim + upper-case; the QR is case-insensitive on every
  scan path".
- `IdempotencyKey = Guid.NewGuid().ToString()` - a fresh key on every click, so
  this console never replays a key of its own accord.
- `Source = ScanSource.Simulator`.

`GateScanRequest` also carries `ClientScannedAt` and `RequestedDirection`; this
page sets neither. `RequestedDirection` is documented on the contract as the
operator's دخول/خروج toggle, "Honoured ONLY when the gate's DirectionMode is
Both", and "Null = the server infers direction from the holder's last allowed
scan". The console has no direction toggle, so a Both-mode gate always gets
server inference here. The toggle lives on the Flutter staff console.

`OperatorGateAssignment` is `(Guid GateId, string Code, string Name, string NameArabic, DirectionMode DirectionMode, bool IsActive)`.
`GateScanResponse` is `(long ScanId, ScanOutcome Outcome, ScanDirection Direction, DateTime ScannedAt, GateScanUserProfile? UserProfile, DenialReasonCode? DenialReasonCode, string? DenialMessage, string? NoticeMessage = null)`.

## 6. Validation + error handling

- **Client-side guards.** `OnScanAsync` opens with
  `if (_busy || _selectedGateId == Guid.Empty || string.IsNullOrWhiteSpace(_qrInput)) return;`.
  That single line is the whole client-side validation: it debounces a
  double-click, blocks a scan before a gate is chosen, and makes a blank or
  whitespace-only QR a silent no-op with no network call. `_busy` is set inside a
  `try` whose `finally { _busy = false; }` always clears it.
- **Server-side validation.** N/A as FluentValidation - no
  `AbstractValidator<PostScanRequest>` or `AbstractValidator<GateScanRequest>`
  exists anywhere in `src/`. The scan is validated by the gate constraint engine
  in `GateOperatorService.RecordScanAsync` instead, which is a policy decision
  rather than a shape check: it normalises the QR, denies an over-96-character
  value as `QrUnknown`, resolves the badge through `IQrResolver`, and then walks
  the engine steps that emit `DenialReasonCode`.
- **Denial reasons the engine writes** (`DenialReasonCode`, wire-stable values,
  bilingual messages from `GateOperatorService.DenialMessages`):

  | Code | Value | English message |
  |------|-------|-----------------|
  | `QrUnknown` | 0 | "This QR code is not recognised." |
  | `GateInactiveAtScan` | 1 | "This gate is currently inactive." |
  | `HolderNotApproved` | 2 | "This visitor's account has not been approved." |
  | `HolderDisabled` | 3 | "This visitor's account is disabled." |
  | `HolderLocked` | 4 | "This visitor's account is locked." |
  | `ProfileTypeInactive` | 5 | "This visitor's profile type is no longer active." |
  | `OutsideTimeWindow` | 6 | "This gate is closed at this time." (enum comment: reserved, engine step 9.5) |
  | `ProfileTypeNotAllowed` | 7 | "This gate is not open to this visitor's profile type." |
  | `BookingRequiredMissing` | 8 | "A booking is required for this gate." |

- **Operational faults**, which are the only cases that leave HTTP 200
  (`PostScanEndpoint`):

  | `GateScanResultKind` | Status | `ErrorCodes` constant | Wire code |
  |----------------------|--------|-----------------------|-----------|
  | `GateNotFound` | 404 | `ErrorCodes.GateNotFound` | `GATE_NOT_FOUND` |
  | `NotAssigned` | 403 | `ErrorCodes.GateOperatorNotAssigned` | `GATE_OPERATOR_NOT_ASSIGNED` |
  | `IdempotencyConflict` | 409 | `ErrorCodes.IdempotencyKeyConflict` | `IDEMPOTENCY_KEY_CONFLICT` |
  | `CircuitOpen` | 429 | `ErrorCodes.GateFailureCircuitOpen` | `GATE_FAILURE_CIRCUIT_OPEN`, plus the response header `X-Gate-Failure-Circuit: open` |

- **Error envelope.** Standard `ApiResult<T>.Error` with a `Code` from
  `ErrorCodes` and bilingual `Message` / `MessageArabic`. The page picks the
  right one with `envelope?.Error?.MessageForCurrentCulture()`.
- **Toast strategy.** N/A - this page raises no toast. Every message is an inline
  `SimfAlert`. On an unsuccessful envelope the page fabricates a local
  `GateScanResponse(0, ScanOutcome.Denied, ScanDirection.CheckIn, SimfClock.Now, null, null, message)`
  so the red card renders with the server's own bilingual text, falling back to
  `L["Admin.Gates.Fallback"]` ("The operation could not be completed." /
  "تعذّر إتمام العملية.") when no message came back.

## 7. Edge cases + known limitations

- **A denial is HTTP 200, not an error.** The `ScanOutcome` enum comment states
  it: "a denial does NOT raise an HTTP error - the request succeeded (the system
  did what it was asked: scan + record). The denial reason rides in
  `DenialReasonCode` on the response body." So the page's success branch handles
  both outcomes and only the four operational faults above reach the else branch.
- **An inactive gate is a recorded denial, not a 503.** `PostScanEndpoint`
  carries an explicit comment for the arm that is missing: "DEF-STF-008 - there
  is deliberately no GATE_INACTIVE (503) arm: an inactive gate is a RECORDED
  denial at HTTP 200 (`DenialReasonCode.GateInactiveAtScan`), so the attempt still
  lands in the append-only GateScan audit trail and the operator gets the designed
  denial card instead of an envelope failure."
- **Changing the gate in the picker does not reload the log.** The `<select>` is
  a plain `@bind="_selectedGateId"` with no `@bind:after` and no `onchange`
  handler, and `LoadReportAsync` is called from exactly two places: the first
  render, and the success branch of `OnScanAsync`. After switching gates the "My
  day so far" table keeps showing the previous gate's rows until the next
  successful scan. Note that E2E-GOP-002 asserts the report re-fetches on the
  picker change; the code as read does not do that.
- **A failed assignments call is indistinguishable from having no assignments.**
  `_assignments` is only assigned inside `if (envelope is { Success: true, Data: not null })`.
  Any other outcome leaves it as the initial `Array.Empty<OperatorGateAssignment>()`,
  and the page's only branch is `_assignments.Count == 0`, so a 500 or an expired
  session renders the same "You have no active gate assignments" empty state as a
  genuinely unassigned operator.
- **A failed report call leaves "Loading…" on screen forever.** `_report` is
  assigned only on a successful envelope, and the page shows
  `L["Admin.Gates.Operator.MyReportLoading"]` whenever `_report is null`. An
  operator holding `Gates.Operate` but not `Gates.ViewOwnReports` sees exactly
  that, permanently.
- **Assignments that exist but are inactive are invisible.** The picker iterates
  `_assignments.Where(a => a.IsActive)`, and the auto-select is
  `FirstOrDefault(a => a.IsActive)`. An operator whose only assignment has been
  deactivated gets a rendered but empty picker, `_selectedGateId == Guid.Empty`,
  and a Scan button that silently does nothing - the empty state does not trigger,
  because `_assignments.Count` is not zero.
- **The QR field clears only on success.** `_qrInput = string.Empty` sits in the
  success branch. After a failed envelope the typed value stays in the box, so
  the operator can retry without re-entering it.
- **Duplicate scans inside five seconds are absorbed.** `GateOperatorService`
  holds `DuplicateWindow = TimeSpan.FromSeconds(5)` keyed on (GateId,
  UserProfileId) and replays the prior allowed scan rather than writing a second
  row. The one documented exception is a deliberate direction switch on a
  `Both`-mode gate, which the comment calls "an intentional new movement, NOT an
  accidental duplicate" - but this console never sends `RequestedDirection`, so
  that exception cannot be reached from here.
- **Idempotent replay is signalled by a header.** `PostScanEndpoint` sets
  `X-Idempotent-Replay: true` (`GateProtocol.IdempotentReplayHeader`) when the
  result is a replay. The console does not read response headers, so a replay is
  indistinguishable from a fresh scan in the UI.
- **DEF-CHK-004 advisory - allowed, but nothing counted.** A hall-door gate scan
  can admit the holder while recording no `HallAttendance` row. `GateScanResponse.NoticeMessage`
  is documented as "An ADVISORY note about a scan that was still ALLOWED, already
  resolved to the caller's Accept-Language exactly like `DenialMessage`", covering
  the cases where no session was live in the hall or a check-out found no open row
  to close. It is "Append-only addition to the shipped wire contract - it never
  changes the allow/deny outcome". The page renders it as a second alert beneath
  the result card. The page's own comment records why it exists: the message
  "arrives already localized, like DenialMessage".
- **The advisory renders in the info style, not amber.** The page passes
  `Variant="warning"`, but `SimfAlert` recognises only `"error"` and `"success"`
  and routes everything else to `simf-alert--info`. No `.simf-alert--warning` rule
  exists in `simf-components.css`. The E2E catalogue describes this alert as amber;
  the components as read produce the blue info treatment with the `info` icon.
- **The log is capped at 50 rows with no way past it.** `Rows.Take(50)` is a view
  cap only - `Totals.Allowed` / `Totals.Denied` above the table still count the
  whole day, so the summary and the visible rows can legitimately disagree.
- **`DenialBreakdown` is fetched and discarded.** `OperatorDailyReport` carries
  `IReadOnlyList<OperatorDenialBucket> DenialBreakdown`; the console renders no
  part of it.
- **Enum names are shown raw.** Outcome, Direction and `DenialReasonCode` are
  rendered by `ToString()`, so an Arabic operator reads "Allowed", "CheckIn" and
  "ProfileTypeNotAllowed" in Latin script. Only `DenialMessage` and
  `NoticeMessage` arrive localised, and they come from the server, not the resx.
- **Gate option text is English-only.** `OperatorGateAssignment` carries both
  `Name` and `NameArabic`; the picker uses `Name`.
- **`Source` is `Simulator`, whose own enum comment says "dev / staging only".**
  That is the value this console sends in every environment. The enum summary also
  notes the source "Drives both the audit trail and the rate-limit posture", so
  scans recorded from the Control Panel are attributed as simulator scans in the
  audit trail.

## 8. i18n + RTL

Every visible string comes from `Strings.resx` / `Strings.ar.resx` via
`IStringLocalizer<Strings> L`. The full key set this page uses:

| Key | English | Arabic |
|-----|---------|--------|
| `Admin.Gates.Operator.Title` | Gate operator | مشغل البوابة |
| `Admin.Gates.Operator.NoAssignments` | You have no active gate assignments. Please contact an administrator. | لا توجد بوابات معيّنة لك حالياً. يرجى التواصل مع المسؤول. |
| `Admin.Gates.Operator.PickGate` | Gate | البوابة |
| `Admin.Gates.Operator.QrLabel` | QR id | رمز QR |
| `Admin.Gates.Operator.QrHint` | Scan or type the 12-character QR id. | امسح أو أدخل الرمز المكوّن من 12 حرفاً. |
| `Admin.Gates.Operator.ScanButton` | Scan | مسح |
| `Admin.Gates.Operator.Scanning` | Scanning… | جارٍ المسح… |
| `Admin.Gates.Operator.Allowed` | Allowed | مسموح |
| `Admin.Gates.Operator.Denied` | Denied | مرفوض |
| `Admin.Gates.Operator.MyReportTitle` | My day so far | يومي حتى الآن |
| `Admin.Gates.Operator.MyReportLoading` | Loading… | جارٍ التحميل… |
| `Admin.Gates.Operator.MyReportSummary` | {0} allowed · {1} denied | {0} مسموح · {1} مرفوض |
| `Admin.Gates.Operator.Report.At` | At | الوقت |
| `Admin.Gates.Operator.Report.Outcome` | Outcome | النتيجة |
| `Admin.Gates.Operator.Report.Direction` | Direction | الاتجاه |
| `Admin.Gates.Operator.Report.Visitor` | Visitor | الزائر |
| `Admin.Gates.Operator.Report.Reason` | Reason | السبب |
| `Admin.Gates.Fallback` | The operation could not be completed. | تعذّر إتمام العملية. |
| `Module.GatesOperator` (side-nav label) | Gate operator | مشغل البوابة |

Server-supplied text is localised server-side, not through these files:
`PostScanEndpoint` reads `Accept-Language` and sets
`AcceptLanguage = acceptLang.Contains("ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en"`,
which selects between the English and Arabic halves of `DenialMessages` and of
the DEF-CHK-004 notice.

Not localised on this page, as recorded in section 7: the gate option text
(`Code — Name (DirectionMode)`), and the Outcome / Direction / Reason enum names
in the report table.

RTL is unverified for this page specifically. No RTL render has been captured and
E2E-GOP-013 is still marked "_to author_". What is verifiable from source is that
the page contributes no direction-sensitive markup of its own: it uses the shared
`simf-page-wide` / `simf-surface` / `simf-field` / `simf-table` classes and no
inline style, so it inherits whatever the shell's RTL handling provides.

## 9. Accessibility

Only the following is verifiable from source. Nothing here was checked in a
browser or with a screen reader.

- **Heading and focus.** `SimfBanner` renders `<h1 class="simf-banner__title">`,
  and `Routes.razor` carries `<FocusOnNavigate RouteData="routeData" Selector="h1" />`,
  so focus moves to the page title on navigation.
- **Labels.** The gate picker's `<select>` is nested inside its
  `<label class="simf-field">`, which associates them implicitly. `SimfTextField`
  is explicit instead: it emits `<label ... for="@FieldElementId">` against an
  input whose id is a per-instance `simf-field-{Guid:N}`, and wires
  `aria-describedby` to the helper text element.
- **Announcements.** `SimfAlert`'s error variant is `role="alert"` (assertive);
  the success and info variants are `role="status" aria-live="polite"`. Because
  `Variant="warning"` falls through to the info arm, the DEF-CHK-004 advisory is
  announced politely.
- **Busy state.** Both inputs take `disabled="@_busy"` / `Disabled="_busy"` and
  the button uses `SimfButton`'s `Loading` / `LoadingLabel`, so the scanning state
  is conveyed by the control's own label rather than by colour alone.
- **Unverified - keyboard tab order and focus return after a scan.** The page
  opens no modal, so there is no focus trap to manage, but nothing in the source
  moves focus back to the QR field after it is cleared. Not observed in a browser.
- **Unverified - colour contrast.** The alert, field and table colours come from
  `theme.tokens.css` through the shared component classes. No contrast measurement
  was run for this page.
- **Unverified - the report table's accessible name.** It is a hand-written
  `<table class="simf-table">` with a `<thead>` row of plain `<th>` cells; it
  carries no `<caption>`, no `scope` attributes and no `aria-label` in the source.

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| `UC-35` | Check an attendee in at a hall door | The row at `docs/SIMF-UCS-001-Use-Case-Specifications.md` line 106 gives the actor as "Staff / System" and the requirement as FR-305. Line 213 calls it "a staff QR scan". A hall-door gate scanned from this console does record that arrival (`tests/SIMF.Api.Tests/GateHallDoorChainTests.cs`), so the console is one surface the use case runs on - but UCS-001 does not name it, and `admin-hall-arrivals.md` claims `UC-35` for the `/admin/hall-arrivals` console instead. |

No UCS-001 entry names the gate operator console itself, or perimeter-gate
scanning. `SIMF-UCS-001-Use-Case-Specifications.md` has six occurrences of
"gate": the seat-booking flow (lines 21, 221 and 226, where a gate check-in
confirms a provisional reservation), the UC-35 narrative step at line 213, "the
venue gate" at line 675, and an unrelated "Length gate" at line 738. If a
gate-operations use case is authored later, add its id here and to
`PAGE-INDEX.md` in the same changeset.

## 11. Related E2E test scenarios

All scenarios live in [`docs/tests/e2e/cp-admin-gates-operator.md`](../../tests/e2e/cp-admin-gates-operator.md).

| Scenario | Id | Coverage |
|----------|----|----------|
| Golden path - pick gate, scan a valid QR, Allowed alert, report increments | E2E-GOP-001 | The whole happy chain including the trimmed + upper-cased `Qr` and the fresh idempotency key |
| Gate picker switches gate | E2E-GOP-002 | Picker rebind. **Its report-reload assertion does not match the code** - see section 7 |
| Auto-select on a single assignment | E2E-GOP-003 | `FirstOrDefault(a => a.IsActive)` in `OnAfterRenderAsync` |
| Denial, `QR_UNKNOWN` | E2E-GOP-004 | HTTP 200 + red alert + no `UserProfile` span |
| Denial, `PROFILE_TYPE_NOT_ALLOWED` | E2E-GOP-005 | HTTP 200 + red alert **with** the `UserProfile` span |
| "My day so far" renders, `Take(50)` cap | E2E-GOP-006 | The report table. Its `HH:mm:ss` claim differs from the source's `hh:mm:ss tt` |
| Empty state | E2E-GOP-007 | `SimfEmptyState`, no picker / field / button / table |
| Auth gate | E2E-GOP-008 | `/not-permitted` + the hidden nav item |
| Blank or whitespace QR is a no-op | E2E-GOP-009 | The `OnScanAsync` guard, no POST fires |
| Idempotency key reuse | E2E-GOP-010 | 409 `IDEMPOTENCY_KEY_CONFLICT` -> synthetic Denied card, no report re-fetch |
| Not assigned to the gate | E2E-GOP-011 | 403 `GATE_OPERATOR_NOT_ASSIGNED` |
| API 500 on `/scans` | E2E-GOP-012 | `Admin.Gates.Fallback` alert, `_busy` cleared in `finally` |
| RTL render | E2E-GOP-013 | Arabic banner, labels, summary and table headers |
| DEF-CHK-004 advisory on a hall door with no live session | E2E-GOP-014 | Authored. API test `Hall_door_gate_with_no_live_session_returns_an_allowed_scan_carrying_a_notice` |
| No advisory on the normal paths | E2E-GOP-015 | Authored. `Hall_door_gate_bound_to_a_live_session_carries_no_notice`, `Perimeter_gate_carries_no_notice` |
| Fixed Out gate closing nothing | E2E-GOP-016 | Authored. `Fixed_out_gate_with_no_open_row_carries_the_advisory_notice` |
| Arrival whose attendance insert is rejected | E2E-GOP-017 | Authored. `Gate_door_arrival_that_persisted_no_row_does_not_report_attendance_recorded` |
| Element inventory, LTR + RTL | E2E-GOP-ELS-001 | Every control present, named and gated |
| Element health | E2E-GOP-ELS-002 | No dead control, no broken asset, no horizontal overflow |

## 12. Related docs

- Sibling pages: [`cp/admin-gates.md`](admin-gates.md) (gate CRUD and operator
  assignment, `/admin/gates`), and `/admin/gates/dashboard`, whose source comment
  in `GatesOperationsDashboard.razor` names this console and gate CRUD as the two
  surfaces it sits between.
- Mobile counterpart: `docs/tests/e2e/mobile-gate-scan.md` - the Flutter staff
  gate console over the same `GET /app/gates/my-assignments` and
  `POST /app/gates/{id}/scans` endpoints, which does carry the دخول/خروج
  direction toggle this page lacks.
- Component catalogue: [`SIMF-CMP-001`](../../SIMF-CMP-001-Component-Catalog.md) -
  `SimfTextField` (line 51), `SimfButton` (68), `SimfAlert` (75) and
  `SimfEmptyState` (80). `SimfBanner` is **not** in that catalogue - its Layout
  table lists `SimfPageHeader` and no banner component, and the word "banner"
  does not appear in the file at all.
- Architecture: [`SIMF-SAD-001`](../../SIMF-SAD-001-Software-Architecture-Document.md).
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) for the
  `ApiResult<T>` envelope. The gate module's own wire contract is
  `SIMF-API-GATES-001`, cited by `GateProtocol` ("SIMF-API-GATES-001 §5 + §10")
  and by `DenialReasonCode` ("SIMF-FDS-003 §5.6.1, SIMF-API-GATES-001 §8.2").
- Decisions: D-148 (Gate Module kickoff, 2026-05-29) and D-149 (Gate Module
  shipped, 2026-05-29), which lists "3 operator FastEndpoints" and the
  `GateOperator` baseline role among what landed.
- Source: [`GateOperatorConsole.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/GateOperatorConsole.razor),
  [`GateOperatorConsole.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/GateOperatorConsole.razor.cs),
  [`AccountEndpoints.Gates.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.Gates.cs),
  [`OperatorGateEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Gates/OperatorGateEndpoints.cs),
  [`GateOperatorService.cs`](../../../src/Backend/SIMF.Infrastructure/AccessControl/GateOperatorService.cs).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-29 | D-148 | Gate Module increment kicked off. The umbrella entry covering the `AddGates` migration and the module's five entities. |
| 2026-05-29 | D-149 | Gate Module shipped. Included the three operator FastEndpoints this page calls, the 13-step constraint engine, the `GateOperator` baseline role and its three seeded permission rows, and the CP commit that added this console. |
| 2026-07-26 | DEF-CHK-004 | `GateScanResponse.NoticeMessage` added as an append-only field and rendered by this page as a second alert under an Allowed result, so a hall-door scan that recorded no attendance stops being silent. Date taken from the E2E catalogue's own "Last reviewed" line; no `DECISIONS_LOG` row for DEF-CHK-004 was found. |
| Undated | BUG-026 | The action row's `style="margin-top:1rem"` replaced by the `.simf-form__actions--tight` class, now ratcheted by `CpMarkupHygieneTests`. No `DECISIONS_LOG` row for BUG-026 was found, so no date is claimed. |

---

_Last reviewed:_ 2026-08-19 by Claude (first authoring of this page reference
doc, written against source). If the page has changed and this doc has not been
re-reviewed in 60 days, it is out of date. Re-walk the page in a browser and
update every section that drifted - in particular sections 3 and 9, which are
still awaiting a live render.
