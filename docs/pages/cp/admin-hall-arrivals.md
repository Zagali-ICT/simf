# Hall arrivals (door scan) - `/admin/hall-arrivals`

| | |
|--|--|
| **Route** | `/admin/hall-arrivals` |
| **Layout** | `CpShellLayout` (`@layout CpShellLayout`) |
| **Surface** | Control Panel |
| **Audience** | Any signed-in admin holding `HallArrivals.View`. The catalogue seeds that code to the `SecurityTeam` baseline role; `Administrator` holds it through the `*` wildcard. |
| **Auth** | Page: `@attribute [RequirePermission(PermissionCatalog.HallArrivals.View)]`. Action block: `<AuthorizedAction Permission="@PermissionCatalog.HallArrivals.Record">`. API: `Policies(PolicyFor(HallArrivals.Record), nameof(AuthorizationPolicies.RequireApprovedAccount))` on both endpoints. |
| **Pattern** | Not a CRUD list. A single-action operator console: one picker, one text field, two buttons. No `SimfDataGrid`, no toolbar, no pager, no modals. |
| **Status** | Real (D-244, `docs/decisions/DECISIONS_LOG.md`) |
| **Implements use case(s)** | `UC-35`. `SIMF-UCS-001-Use-Case-Specifications.md` §4.4 "Field operations" lists it as "Check an attendee in at a hall door", primary actor "Staff / System", requirement `FR-305`. `SIMF-FDS-003` §5.4 names the same id for the door scan ("A Staff user, or a device at the door, scans the attendee's badge as they enter a session hall (`UC-35`)"), and its traceability row maps `FR-305 hall-arrival verification` to `UC-35 Check an attendee in at a hall door`. |
| **Backend endpoints** | `POST /account/api/admin/sessions/list`, `POST /account/api/admin/sessions/{sessionId}/arrivals`, `POST /account/api/admin/sessions/{sessionId}/departures` |
| **Source file** | [`HallArrivalsConsole.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/HallArrivalsConsole.razor) + [`HallArrivalsConsole.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/HallArrivalsConsole.razor.cs) |
| **Tests** | [`docs/tests/e2e/cp-admin-hall-arrivals.md`](../../tests/e2e/cp-admin-hall-arrivals.md); `tests/SIMF.Api.Tests/HallArrivalScanTests.cs`; `tests/SIMF.ControlPanel.Tests/HallArrivalsConsoleSessionPickerTests.cs`; `tests/SIMF.Api.Tests/HallAttendanceTests.cs`; `tests/SIMF.Api.Tests/Operations/HallAttendanceCloseoutWorkerTests.cs` |
| **Last reviewed** | `2026-08-19` |

> **How this doc was written.** Every statement below is taken from source read on
> 2026-08-19: the `.razor` + `.razor.cs`, `AccountEndpoints.Moderation.cs`,
> `SimfAdminClient.Moderation.cs`, `HallArrivalEndpoints.cs`,
> `HallAttendanceService.cs`, `PermissionCatalog.cs`, `CpNavigation.cs`, the two
> `Strings*.resx` files and the named test files. The page was **not** driven in a
> browser for this pass, so nothing here is a live-render observation.

---

## 1. Purpose

This is the hall-door console. An operator standing at the entrance to a session
hall picks the session on the door, then scans or types the attendee's badge QR
and records either an arrival or a departure. It exists because
`SIMF-FDS-003` §5.4 requires **two** means of recording hall arrival - the door
scan and the attendee's own GPS geofence claim - which merge into one
`HallAttendance` row rather than producing two. This page is the door-scan means;
the geofence means and the attendee's own self check-out run from the attendee's
device, not from here. The operator walks in expecting to answer one question per
person at the door - "is this badge good for this hall, and is that person now
in or out" - so the page answers by naming the attendee it resolved, which is the
only way an operator can tell they scanned the right badge.

The row this page opens is what the seat map renders as the confirmed state and
what the per-session question and rating gates read, so the check-out button is
not cosmetic: closing the row is what clears those.

## 2. Audience + permissions

- **Who can reach it:** any signed-in admin whose role grants
  `PermissionCatalog.HallArrivals.View` (`"HallArrivals.View"`), plus
  `Administrator`, which holds the `*` wildcard. The catalogue entry is
  `new(HallArrivals.View, "HallArrivals", "View", "View the hall-arrival console", SecurityTeam)`,
  so the seeded baseline role is `AppRoles.SecurityTeam`.
- **Who can record on it:** `PermissionCatalog.HallArrivals.Record`
  (`"HallArrivals.Record"`), catalogued as
  `"Record a hall arrival or departure by badge scan"` with the same `SecurityTeam`
  baseline. **One code covers both directions.** The catalogue's own XML comment
  gives the reason: the operator population is identical for check-in and
  check-out, so the codes are deliberately not split, and the description was
  reworded to name the departure because the old arrival-only wording did not
  tell an admin granting it that it also allows check-out (FR-CHK-003, as cited
  in `PermissionCatalog.cs`).
- **Also granted to app roles.** `HallArrivals.Record` appears in both
  `StaffAppPermissions` and `ModeratorAppPermissions`, so a partner-side Staff or
  Moderator tablet can drive the same two endpoints. The source comment states
  why: without it a tablet could scan a badge at a perimeter gate but could not
  record a hall arrival at all.
- **Authorisation gates:**
  - Page: `[RequirePermission(PermissionCatalog.HallArrivals.View)]`.
    `RequirePermissionAttribute` is an `AuthorizeAttribute` that sets
    `Policy = PermissionCatalog.PolicyFor(permissionCode)`.
  - Action block: `<AuthorizedAction Permission="@PermissionCatalog.HallArrivals.Record">`
    wraps the QR field **and both buttons**. `AuthorizedAction` delegates to
    `SimfActionGate`; its own comment says the API still enforces the same
    permission, so this is a UX layer and not the security boundary.
  - Nav: `new("Module.HallArrivals", "/admin/hall-arrivals", RequiredPermission: PermissionCatalog.HallArrivals.View, Icon: "log-in")`
    in the `Nav.Gates` group of `CpNavigation.cs`.
  - BFF: the `/account/api` route group is created as
    `routes.MapGroup("/account/api").RequireAuthorization()` - cookie-authenticated
    only. It carries **no** permission policy; the permission check happens at the
    API.
  - API: both `RecordQrArrivalEndpoint` and `RecordQrDepartureEndpoint` declare
    `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.HallArrivals.Record), nameof(AuthorizationPolicies.RequireApprovedAccount))`.
- **What an unauthenticated user sees:** the route is behind `AuthorizeRouteView`,
  whose `<NotAuthorized>` branch in `Routes.razor` splits on
  `authenticationState.User.Identity?.IsAuthenticated`. A signed-in admin lacking
  the permission gets `<RedirectToNotPermitted />` and lands on `/not-permitted`
  (an ordinary client-side `Nav.NavigateTo`, not a `forceLoad`, because the
  circuit and session are both healthy); anyone else gets `<RedirectToLogin />`.
  The comment above that branch records why the split exists: `AuthorizeRouteView`
  renders `<NotAuthorized>` for both cases, so without it a permission denial on
  any of the gated CP pages force-reloaded a signed-in admin onto `/login`, which
  reads as "your session expired" rather than "you may not open this page".
  `Program.cs` also sets `options.AccessDeniedPath = "/not-permitted"` for the
  non-Blazor path.
- **A View-only admin can open the page but cannot record.** The whole
  `<AuthorizedAction>` block - field and both buttons - simply does not render, and
  a forged request is refused 403 by the API policy
  (`HallArrivalScanTests.A_non_operator_account_is_forbidden`,
  `A_non_operator_cannot_record_a_departure`).

## 3. Screenshots

No screenshots have been captured for this page. The table records the intended
file names and states so a later capture pass has somewhere to land.

| State | File | Captured |
|-------|------|----------|
| Default (session picker populated) | `docs/screenshots/cp-admin-hall-arrivals-default.png` | Not captured |
| Empty state (no selectable sessions) | `docs/screenshots/cp-admin-hall-arrivals-empty.png` | Not captured |
| Success alert after Record arrival | `docs/screenshots/cp-admin-hall-arrivals-golden-after.png` | Not captured |
| Error alert (unknown badge QR) | `docs/screenshots/cp-admin-hall-arrivals-error.png` | Not captured |
| View-only admin (action block hidden) | `docs/screenshots/cp-admin-hall-arrivals-view-only.png` | Not captured |
| RTL (Arabic) | `docs/screenshots/cp-admin-hall-arrivals-rtl.png` | Not captured |

The E2E catalogue's E2E-HAR-001 asks for the first and third of these under the
names `cp-admin-hall-arrivals-golden-before.png` and
`cp-admin-hall-arrivals-golden-after.png`.

## 4. UI affordances

### 4.1 Banner / page header

`<SimfBanner Title="@L["Admin.HallArrivals.Title"]" />` - title only. No
`Subtitle`, no `Actions` slot. `SimfBanner` renders a `<section class="simf-banner">`
with the title as the page `<h1>`. The document title is
`@L["Admin.HallArrivals.Title"] · SIMF`.

Everything below the banner sits inside `<div class="simf-page-wide"><div class="simf-surface">`.

**Feedback line.** When `_toast` is non-null the page renders
`<SimfAlert Variant="@_toast.Variant">@_toast.Message</SimfAlert>` as the first
child of the surface. It is an **inline alert at the top of the panel**, not a
floating toast overlay - the E2E catalogue calls it a "toast", which is the same
thing in that document's vocabulary. `SimfAlert` uses `role="alert"` for the
`error` variant and `role="status" aria-live="polite"` for `success`. The page
only ever sets `"error"` and `"success"`. There is no dismiss control; the alert
persists until the next action replaces or clears it.

**Loading and empty branches.** While `_loading` the surface shows a plain
`<p>@L["Admin.HallArrivals.Loading"]</p>`. When loading has finished and
`_sessions.Count == 0`, it renders
`<SimfEmptyState Title="@L["Admin.HallArrivals.NoSessions"]" />` and **nothing
else** - no picker, no field, no buttons.

### 4.2 Toolbar (CRUD pages only)

N/A - not a CRUD list page. There is no toolbar, no Add / Edit / Details /
Delete / Copy / Paste / Duplicate / Import / Export, and no multiselect. The
page's entire interactive surface is the three controls in 4.5.

### 4.3 Grid columns (CRUD pages only)

N/A - not a CRUD list page. The page renders no `SimfDataGrid` and no table of
any kind. Recorded hall attendance is read elsewhere (the session-attendance
dashboard at `/admin/attendance`, per D-293 in the decisions log).

### 4.4 Pager

N/A - there is no grid, so there is no pager and no page-size selector. The one
paging decision on the page is invisible: the session list is fetched with
`new GridQuery { Top = 200, Sort = "start" }`, and `AdminSessionService`'s grid
spec declares `.PageSize(fallback: 25, max: 200)`, so 200 is exactly the ceiling
(see the limitation in §7).

### 4.5 Form fields

The controls are not inside an `EditForm` and there is no client-side validation
attribute anywhere on the page.

| Field | Type | Required | MaxLength | Validation | Locale |
|-------|------|----------|-----------|------------|--------|
| Session | `SimfSelect TValue="AdminSessionSummary"` | Yes, by the `RecordAsync` / `DepartAsync` guard | n/a | No validator. `ValueFor` is `s => s.Id.ToString()`; `LabelFor` is `s => $"{s.Title} · {s.Code}"`; `Placeholder` reuses the label string. `Disabled="_busy"`. | `Admin.HallArrivals.SelectSession` (label **and** placeholder) |
| Attendee badge QR | `SimfTextField` bound `@bind-Value="_qrId"` | Yes, by the guard (blank is a silent no-op) | `MaxLength="64"` | No client validator. The value is `.Trim()`ed before the call, and `HallAttendanceService` trims again server-side. | `Admin.HallArrivals.Field.Qr` (label) + `Admin.HallArrivals.Field.QrHint` (helper) |

`SimfTextField` renders the `maxlength` attribute on the input; its own XML
comment describes that as defence in depth alongside the server rule. There is
**no** `FluentValidation` validator for `RecordQrArrivalRequest` - see §6.

### 4.6 Actions

Both buttons live inside the single `<AuthorizedAction Permission="@PermissionCatalog.HallArrivals.Record">`
block, in `<div class="simf-form__actions">`.

| Button | Resx key (EN) | Wired callback | Calls | Notes |
|--------|---------------|----------------|-------|-------|
| Record arrival | `Admin.HallArrivals.Action.Record` / "Record arrival" | `RecordAsync` | `POST /account/api/admin/sessions/{sessionId}/arrivals` | `Type="button"`, `Loading="_busy"`, `LoadingLabel="@L["Grid.Working"]"`. Clears `_qrId` on success. |
| Record departure | `Admin.HallArrivals.Action.CheckOut` / "Record departure" | `DepartAsync` | `POST /account/api/admin/sessions/{sessionId}/departures` | Same wiring. Added 2026-07-18 per the code-behind comment. Clears `_qrId` on success. |

Both buttons bind `Loading` to the **same** `_busy` field, so while either call is
in flight both render their loading state and the picker and QR field are
`Disabled`.

## 5. Data flow

The Control Panel is a BFF: the page never calls the API directly. Three layers
are involved on every action.

```
Operator picks a session      -> SimfSelect ValueChanged -> _selected (AdminSessionSummary)
Operator scans / types a badge -> SimfTextField @bind-Value -> _qrId

Click "Record arrival"  -> HallArrivalsConsole.RecordAsync()
  -> JS  window.simfAccount.postJson(url, body)      [fetch, credentials: 'same-origin']
  -> BFF POST /account/api/admin/sessions/{id}/arrivals
         AccountEndpoints.Moderation.cs -> http.GetTokenAsync("access_token")
  -> API POST /api/v1/admin/sessions/{id}/arrivals
         SimfAdminClient.RecordQrArrivalAsync (BasePath "api/v1/admin/") with a bearer
         RecordQrArrivalEndpoint  [HallArrivals.Record + RequireApprovedAccount]
  -> IHallAttendanceService.RecordQrArrivalAsync(operatorUserId, sessionId, qrId)
         IQrResolver.ResolveAsync -> QrResolution (UserProfileId, UserId?, AccountState, ...)
         Sessions (SIMF_App)      -> live-window check (EnsureSessionLiveNow)
         HallAttendances (SIMF_App) -> OpenOrCreateArrivalAsync (merge or SERIALIZABLE insert)
         OperationLog             -> AuditEvents.HallArrivalRecorded, only when a row was created
  -> ApiResult<QrArrivalResult> back down the same three hops
  -> SimfAlert "success" naming envelope.Data.DisplayName; _qrId reset to ""
```

`DepartAsync` is the same chain with `/departures`,
`SimfAdminClient.RecordQrDepartureAsync` and
`IHallAttendanceService.RecordQrDepartureAsync`, ending in `CloseAttendeeRowAsync`
and an `AuditEvents.HallDepartureRecorded` entry.

### Every backend call this page makes

| When | CP page call (BFF) | API route reached | Gate on the API endpoint | Request body | Response shape |
|------|--------------------|-------------------|--------------------------|--------------|----------------|
| `OnInitializedAsync` | `POST /account/api/admin/sessions/list` (`AccountEndpoints.Programme.cs` -> `SimfAdminClient.ListSessionsAsync`) | `POST /api/v1/admin/sessions/list` (`ListSessionsEndpoint`) | `PolicyFor(Sessions.View)` + `RequireApprovedAccount` | `GridQuery { Top = 200, Sort = "start" }` | `ApiResult<GridPage<AdminSessionSummary>>` |
| Click **Record arrival** | `POST /account/api/admin/sessions/{sessionId:guid}/arrivals` (`AccountEndpoints.Moderation.cs` -> `SimfAdminClient.RecordQrArrivalAsync`) | `POST /api/v1/admin/sessions/{sessionId:guid}/arrivals` (`RecordQrArrivalEndpoint`) | `PolicyFor(HallArrivals.Record)` + `RequireApprovedAccount` + `RequireRateLimiting(RateLimitOptions.OperationalPolicy)` | `RecordQrArrivalRequest { QrId }` | `ApiResult<QrArrivalResult>` |
| Click **Record departure** | `POST /account/api/admin/sessions/{sessionId:guid}/departures` (`AccountEndpoints.Moderation.cs` -> `SimfAdminClient.RecordQrDepartureAsync`) | `POST /api/v1/admin/sessions/{sessionId:guid}/departures` (`RecordQrDepartureEndpoint`) | Same three as the arrival endpoint | `RecordQrArrivalRequest { QrId }` (the shapes are deliberately reused) | `ApiResult<QrArrivalResult>` |

`QrArrivalResult` is
`(Guid UserId, string DisplayName, string DisplayNameArabic, HallAttendanceStatus Status, Guid UserProfileId)`,
and `HallAttendanceStatus` is `(bool Arrived, DateTime? Enter, DateTime? Leave, AttendanceMethod? Method)`.
The contract's own XML comment warns that `UserId` is `Guid.Empty` for an attendee
who holds no Identity account (a walk-in or a bulk-minted badge) and must never be
used to look an attendee up; `UserProfileId` was appended beside it, not in place
of it, because the shipped mobile app decodes `UserId`.

**Which sessions the picker offers.** `LoadSessionsAsync` filters the returned page
client-side:

```csharp
var now = SimfClock.Now;
_sessions = envelope.Data.Items
    .Where(s => s.IsActive && now >= s.Start - GraceOf(s))
    .OrderBy(s => now <= s.End + GraceOf(s) ? 0 : 1)
    .ThenByDescending(s => s.Start)
    .ToList();
```

So: active sessions that have already opened for arrivals, live ones first, then
the most recently ended. `GraceOf` reads
`session.EffectiveArrivalGraceMinutes` - the value the **server** already resolved
through `WalkInModeOptions.ResolveArrivalGraceMinutes(sessionOverride, hall, global)`
(session override, else hall, else global, defaulting to 15 minutes). The
code-behind comment says explicitly that there is no constant here to keep in step
with `HallAttendanceService`, which is the point: the picker and the door read the
same resolved number, so they cannot disagree about which sessions are open.
`SimfClock.Now` is Saudi wall-clock (+03:00, no DST), evaluated on the Blazor
Server host.

## 6. Validation + error handling

- **Client-side guards** (identical in `RecordAsync` and `DepartAsync`, in this
  order):
  1. `if (_selected is null)` -> sets an `error` alert with
     `Admin.HallArrivals.NeedSession` and returns. No network call.
  2. `if (string.IsNullOrWhiteSpace(_qrId))` -> returns silently. **No alert, no
     network call**, and any alert already on screen is left alone, because
     `_toast = null` runs after this guard.
  Only after both does it set `_busy = true; _toast = null;`.
- **Server-side validation:** there is **no** FluentValidation validator for
  `RecordQrArrivalRequest` anywhere under `src/Backend` (searched for
  `AbstractValidator<RecordQrArrivalRequest>` this session; FastEndpoints
  auto-registers validators, so an existing one would apply). The rules live in
  `HallAttendanceService.RecordQrArrivalAsync` / `RecordQrDepartureAsync` and throw
  `ApiException` with a bilingual message pair. Blank or unresolvable QR is
  collapsed into one case: the service trims the input and treats a zero-length
  string as "did not resolve".
- **Error envelope:** the standard `ApiResult<T>` with `Error.Code` from
  `SIMF.Common.ErrorCodes` and bilingual `Message` / `MessageArabic`. The page
  surfaces `envelope?.Error?.MessageForCurrentCulture()`, falling back to
  `Admin.HallArrivals.Fallback` when the envelope carries no error at all.

| Code (constant) | Wire value | HTTP | Raised by | Message (EN / AR) |
|---|---|---|---|---|
| `ErrorCodes.AttendeeQrUnknown` | `ATTENDEE_QR_UNKNOWN` | 400 | arrival + departure, when the QR is blank or `IQrResolver.ResolveAsync` returns null | "That badge QR was not recognised." / "لم يتم التعرّف على رمز الشارة." |
| `ErrorCodes.AttendeeNotApproved` | `ATTENDEE_NOT_APPROVED` | 403 | **arrival only** - `AccountState != Approved`, or `IsLockedOut`, or a set-but-inactive profile type | "This attendee's account is not approved for entry." / "حساب هذا الحاضر غير معتمد للدخول." |
| `ErrorCodes.SessionNotFound` | `SESSION_NOT_FOUND` | 404 | both, when no active `Session` matches the id | "The session was not found." / "لم يتم العثور على الجلسة." |
| `ErrorCodes.SessionNotLive` | `SESSION_NOT_LIVE` | 409 | **arrival only** - `EnsureSessionLiveNow` | "This session is not open for arrivals right now." / "هذه الجلسة ليست مفتوحة لتسجيل الوصول حالياً." |
| `ErrorCodes.HallAtCapacity` | `HALL_AT_CAPACITY` | 409 | **arrival only**, and only for a *new* row | "This hall is at capacity." / "بلغت هذه القاعة سعتها القصوى." |

- **Success strategy:** `_toast = new Toast("success", $"{L[...]}: {envelope.Data.DisplayName}")`
  using `Admin.HallArrivals.Recorded` ("Arrival recorded") or
  `Admin.HallArrivals.CheckedOut` ("Departure recorded"), then `_qrId = string.Empty`
  so the field is ready for the next scan. Note the name shown is always
  `DisplayName`; the page does not switch to `DisplayNameArabic` under the Arabic
  culture (see §7).
- **Audit:** `AuditEvents.HallArrivalRecorded` (`"HallAttendance.ArrivalRecorded"`)
  is written **only when a row was newly created**, with the operator as actor and
  a detail string carrying `sessionId`, `hallId`, `method`, `attendeeProfileId` and
  `operator`. `AuditEvents.HallDepartureRecorded`
  (`"HallAttendance.DepartureRecorded"`) is written by `CloseAttendeeRowAsync` with
  the **departing attendee's account** as actor, or a null actor when they have
  none, because `Guid.Empty` already means "matches nobody" there.

## 7. Edge cases + known limitations

- **A re-scan merges; it never creates a second row.**
  `OpenOrCreateArrivalAsync` returns any existing open row first, so the operator
  gets a success alert and one `HallAttendance` row. The capacity check is skipped
  on that path by construction, so someone already inside is never denied re-entry
  on a full hall. Covered by `HallArrivalScanTests.Scan_merges_with_a_prior_geofence_arrival_one_open_row`.
- **A departure is idempotent.** `CloseAttendeeRowAsync` returns
  `new HallAttendanceStatus(false, null, null, null)` when there is no open row, so
  checking out someone who never checked in is a 200 and a green alert, not an
  error. The service comment gives the reason: an attendee must always be able to
  leave. Covered by `Departure_without_a_prior_arrival_is_an_idempotent_noop`.
- **An ended session stays in the picker, and that is deliberate.** The filter binds
  only the *lower* edge (`now >= s.Start - grace`). The code-behind comment records
  what went wrong before: applying the arrival window to both actions dropped a
  session out of the list the moment it ended, which is exactly when the hall has to
  be checked **out**. Pressing **Record arrival** on such a session still gets the
  server's `SESSION_NOT_LIVE` message in the error alert. Pinned by
  `HallArrivalsConsoleSessionPickerTests.A_session_that_ended_hours_ago_is_still_selectable_for_check_out`
  and `The_live_session_is_listed_before_an_ended_one`.
- **A not-yet-started or inactive session is hidden.** No attendance row can exist
  for a session that has not opened for arrivals.
  (`A_session_that_has_not_opened_for_arrivals_yet_is_not_offered`,
  `An_inactive_session_is_not_offered`.)
- **Check-out deliberately skips the admission checks that check-in runs.** The
  departure path does not re-test `AccountState`, lockout or profile-type activity,
  and applies no live-window bind. It does still verify the session exists, so a bad
  id is reported rather than silently no-op'ing.
- **Capacity is enforced under concurrency, not merely counted.**
  `InsertArrivalWithinCapacityAsync` runs the count and the insert inside one
  `Serializable` transaction through the EF execution strategy, so two concurrent
  arrivals cannot both read "one place left". A `DbUpdateException` is treated as a
  merge only for SQL Server error 2601/2627 (the one-open-row filtered unique
  index); everything else, above all the 1205 deadlock victim, is allowed to
  propagate so the strategy re-runs. The comment states why that matters: swallowing
  a deadlock turned ordinary contention into a wrong `HALL_AT_CAPACITY` at a
  half-empty hall. Covered by
  `HallAttendanceTests.Concurrent_arrivals_never_exceed_the_hall_capacity`.
- **The passive gate-door path is advisory, this one is not.** The same helper takes
  `enforceCapacity`; the gate-door caller passes `false` and logs a warning instead
  of refusing, because a person who physically passed a turnstile must still be
  counted. The operator console and the geofence path both enforce.
- **`Sessions.View` is required to populate the picker, and it is not the page's own
  permission.** `/admin/sessions/list` is gated by `PolicyFor(Sessions.View)`, whose
  catalogue baseline is `ScientificCommittee`, while `HallArrivals.View` seeds to
  `SecurityTeam`. A role granted only the two `HallArrivals` codes therefore opens
  the page and gets an error alert instead of a session list. `Administrator` is
  unaffected (wildcard). Recorded here as a real gap, not a design statement - no
  source comment addresses it.
- **The session picker is capped at 200.** The request asks for `Top = 200` and the
  service's grid spec maxes at 200, so a programme with more than 200 sessions
  silently loses the tail. The page shows no "list was capped" notice (unlike
  `/admin/halls`, whose occupancy view does).
- **The success alert always shows the English display name.** `RecordAsync` and
  `DepartAsync` interpolate `envelope.Data.DisplayName`; `DisplayNameArabic` is
  carried on the wire and never read by this page. E2E-HAR-014 expects the Arabic
  name under the Arabic culture, so the catalogue and the code disagree on that
  point today.
- **A blank QR leaves a stale alert on screen.** Because `_toast = null` runs after
  the two guards, clicking a button with an empty field neither clears nor replaces
  the previous alert. An operator can read a success message from the previous scan
  while nothing has happened for the current one.
- **Both buttons share `_busy`.** While either request is in flight, both buttons
  render their loading label (`Grid.Working`) and the picker and field are disabled,
  so it is not visually obvious which direction was pressed.
- **Nothing on this page reads back what was recorded.** There is no list of who is
  currently inside, no scan history and no undo. Reading attendance is the
  `/admin/attendance` dashboard's job (D-293).
- **End-of-day rows close themselves.** `HallAttendanceCloseoutWorker` polls once a
  minute and stamps `Leave = Session.End` on every open row whose session has ended,
  so an operator who never checks a hall out does not leave the count growing
  forever. Covered by `HallAttendanceCloseoutWorkerTests`.
- **Rate limiting.** Both endpoints carry
  `RequireRateLimiting(RateLimitOptions.OperationalPolicy)`, which is deliberately
  uncapped and also exempt from the global per-IP cap. D-838 records why: the CP is
  a server-side BFF, so every CP desk shares the CP host's single source IP, and the
  previous `"auth"` policy capped the whole Control Panel at 20 hall-door scans per
  minute.
- **Open item OI-4.** `SIMF-FDS-003` §5.4 still lists OI-4, "Confirm whether
  hall-door scanning is done by Staff, a fixed device, or both", as unresolved. This
  console is the Staff-operated answer; the fixed-device answer is the gate engine.

## 8. i18n + RTL

Every visible string on the page comes from `IStringLocalizer<Strings> L`, backed by
`src/ControlPanel/SIMF.ControlPanel/Resources/Strings.resx` (EN) and
`Strings.ar.resx` (AR). The twelve page-specific keys, plus the shared button
label, are:

| Key | English | Arabic |
|-----|---------|--------|
| `Admin.HallArrivals.Title` | Hall arrivals (door scan) | الوصول إلى القاعات (مسح الباب) |
| `Admin.HallArrivals.Loading` | Loading sessions… | جارٍ تحميل الجلسات… |
| `Admin.HallArrivals.NoSessions` | No active sessions to record arrivals for. | لا توجد جلسات نشطة لتسجيل الوصول إليها. |
| `Admin.HallArrivals.SelectSession` | Session | الجلسة |
| `Admin.HallArrivals.Field.Qr` | Attendee badge QR | رمز شارة الحاضر |
| `Admin.HallArrivals.Field.QrHint` | Scan or type the badge code, then record the arrival. | امسح أو أدخل رمز الشارة ثم سجّل الوصول. |
| `Admin.HallArrivals.Action.Record` | Record arrival | تسجيل الوصول |
| `Admin.HallArrivals.Action.CheckOut` | Record departure | تسجيل الخروج |
| `Admin.HallArrivals.Recorded` | Arrival recorded | تم تسجيل الوصول |
| `Admin.HallArrivals.CheckedOut` | Departure recorded | تم تسجيل الخروج |
| `Admin.HallArrivals.NeedSession` | Select a session first. | اختر جلسة أولاً. |
| `Admin.HallArrivals.Fallback` | Something went wrong. Please try again. | حدث خطأ ما. يرجى المحاولة مرة أخرى. |
| `Grid.Working` (shared, both buttons' `LoadingLabel`) | Working… | جارٍ التنفيذ… |

Server error messages are bilingual at the source: `HallAttendanceService` throws
`ApiException` with an English and an Arabic string, and the page renders
`Error.MessageForCurrentCulture()`, so the operator sees the message in the culture
they are running.

The option labels are built as `$"{s.Title} · {s.Code}"` from `AdminSessionSummary`,
which uses the **English** `Title`; `TitleArabic` exists on the contract and is not
read here. The same applies to the resolved attendee name (§7).

RTL comes from the shell, not from this page: it renders no direction-specific
markup, no fixed widths and no inline styles. The page's own layout is
`simf-page-wide` / `simf-surface` plus `simf-form__actions`, all of which mirror
with the document direction. E2E-HAR-014 covers the Arabic render.

## 9. Accessibility

- **Keyboard.** Every control is a native element: `SimfSelect` renders a plain
  `<select>` (its comment says the native element stays accessible and
  keyboard-friendly for free and that nothing custom is added), `SimfTextField`
  renders an `<input>`, and both actions are real `<button type="button">`. Tab order
  is the DOM order: session, QR field, Record arrival, Record departure. There are
  no modals on this page, so there is no focus trap and no ESC handling to get
  right. On navigation the router's `<FocusOnNavigate RouteData="routeData"
  Selector="h1" />` moves focus to the page `<h1>`, which here is the
  `SimfBanner` title.
- **Labelling.** `SimfSelect` renders `<label for>` bound to a generated id and wires
  `aria-describedby` to its helper or error paragraph; `SimfTextField` does the same
  and adds `aria-invalid` when in error. The QR field carries a visible helper
  (`Admin.HallArrivals.Field.QrHint`) that is referenced by `aria-describedby`.
- **Screen reader on result.** `SimfAlert` announces the error variant assertively
  (`role="alert"`) and the success variant politely
  (`role="status" aria-live="polite"`), so the resolved attendee's name is read out
  after a scan without the operator having to move focus. That is the behaviour the
  page depends on: at a door, the operator is looking at the person, not the screen.
- **Placeholder duplication.** The session picker passes the same string as both
  `Label` and `Placeholder`, so the disabled first `<option>` repeats the field
  label. Harmless but redundant.
- **Colour contrast and focus indicators.** Inherited from the shared theme tokens;
  this page defines no colours and no custom focus styling. Not independently
  verified for this doc.
- **Not verified live.** No axe or keyboard-walk pass was run for this revision.

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| `UC-35` | Check an attendee in at a hall door | `SIMF-UCS-001-Use-Case-Specifications.md` §4.4 "Field operations": primary actor "Staff / System", requirement `FR-305`. `SIMF-FDS-003` §5.4 names the same id for the QR-scan arrival means and describes the two actors as "A Staff user, or a device at the door"; its traceability row maps `FR-305 hall-arrival verification` to this use case. This console is the Staff-operated surface - which of the two the client wants is still open as OI-4 (§7). |

`UC-35` is also referenced inside UCS-001's seat-booking flow: "The attendee checks
in at the hall gate (a staff QR scan, `UC-35`), which confirms the held seat".

## 11. Related E2E test scenarios

All in [`docs/tests/e2e/cp-admin-hall-arrivals.md`](../../tests/e2e/cp-admin-hall-arrivals.md).

| Scenario | Id | Covers | Backing test |
|----------|----|--------|--------------|
| Golden path - select, scan, arrival recorded with the resolved name | E2E-HAR-001 | §4.6, §5, §6 success | `HallArrivalScanTests.Operator_scan_records_a_qr_arrival` |
| Picker lists active sessions as `{Title} · {Code}`, live first | E2E-HAR-002 | §5 filter | `HallArrivalsConsoleSessionPickerTests` |
| Record clears the QR field on success | E2E-HAR-003 | §6 success strategy | - |
| Guard: no session selected | E2E-HAR-004 | §6 guard 1 | - |
| Guard: blank QR is a silent no-op | E2E-HAR-005 | §6 guard 2 | - |
| Re-scan merges into the one open row | E2E-HAR-006 | §7 merge | `Scan_merges_with_a_prior_geofence_arrival_one_open_row` |
| Empty state, no selectable sessions | E2E-HAR-007 | §4.1 empty branch | - |
| Auth gate, View | E2E-HAR-008 | §2 page gate | `CpNavigationPermissionTests` (nav), `PermissionDeniedRoutingTests` (redirect) |
| Auth gate, Record | E2E-HAR-009 | §2 action gate | `A_non_operator_account_is_forbidden` |
| Unknown badge QR | E2E-HAR-010 | §6 `ATTENDEE_QR_UNKNOWN` | `Unknown_qr_is_400` |
| Attendee not approved | E2E-HAR-011 | §6 `ATTENDEE_NOT_APPROVED` | `Non_approved_attendee_is_403` |
| QR field capped at 64 | E2E-HAR-012 | §4.5 | - |
| Server 500 on `/sessions/list` | E2E-HAR-013 | §6 fallback | - |
| RTL render | E2E-HAR-014 | §8 (note the `DisplayNameArabic` mismatch in §7) | - |
| Arrival on a stale session -> `SESSION_NOT_LIVE` | E2E-HAR-015 | §6, §7 | - |
| Hall at capacity | E2E-HAR-016 | §6 `HALL_AT_CAPACITY` | - |
| Inactive profile type denied | E2E-HAR-017 | §6 `ATTENDEE_NOT_APPROVED` | - |
| Check-out closes the open row | E2E-HAR-018 | §4.6, §7 | `Operator_scan_records_a_departure` |
| Check-out with no prior arrival | E2E-HAR-019 | §7 idempotence | `Departure_without_a_prior_arrival_is_an_idempotent_noop` |
| Unknown QR on departure | E2E-HAR-020 | §6 | `Unknown_qr_departure_is_400` |
| Auth gate on departure | E2E-HAR-021 | §2 | `A_non_operator_cannot_record_a_departure` |
| Ended session still selectable | E2E-HAR-022 | §7 picker | `A_session_that_ended_hours_ago_is_still_selectable_for_check_out` |
| Not-yet-started / inactive stay hidden | E2E-HAR-023 | §7 picker | `A_session_that_has_not_opened_for_arrivals_yet_is_not_offered`, `An_inactive_session_is_not_offered` |
| Concurrent arrivals fill the hall exactly | E2E-HAR-024 | §7 capacity | `HallAttendanceTests.Concurrent_arrivals_never_exceed_the_hall_capacity` |
| A door scan alone unlocks the session rating | E2E-HAR-025 | §1 | `Operator_scan_makes_the_per_session_rating_submittable` |
| Element inventory / element health | E2E-HAR-ELS-001 / ELS-002 | §4, §9 | - |

The catalogue's own permission paragraph states that both codes "default to
`AdminOnly`". That is **stale**: `PermissionCatalog.All` seeds both to
`SecurityTeam` today, and D-244 (which shipped them as `AdminOnly`) predates the
change. Read §2 of this doc for the current values.

## 12. Related docs

- Authority spec: `docs/SIMF-FDS-003-Badge-and-Access-Control.md` §5.4 (hall-arrival
  verification, the two-means rule) and §5.5 (what the records feed).
- Sibling page docs: [`admin-halls.md`](admin-halls.md) - the hall entity, its
  capacity, and the per-hall `ArrivalGraceMinutes` this page's picker resolves
  through.
- E2E catalogue: [`docs/tests/e2e/cp-admin-hall-arrivals.md`](../../tests/e2e/cp-admin-hall-arrivals.md).
- Permissions: `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md` and
  `docs/SIMF-Permission-Catalogue.md`; the source of truth is
  `src/Shared/SIMF.Common/PermissionCatalog.cs`.
- Decisions log (`docs/decisions/DECISIONS_LOG.md`): **D-244** shipped this page,
  its two permissions, the arrivals endpoint and the QR-resolver reuse. **D-293**
  built `/admin/attendance` and records why a separate `Attendance.View` was created
  rather than reusing `HallArrivals.View`. **D-838** moved both hall-door routes onto
  `RateLimitOptions.OperationalPolicy`.
- API spec: `docs/SIMF-API-001-API-Specification.md` for the `ApiResult<T>` envelope
  and the standard error model used throughout §6.
- Component catalogue: `docs/SIMF-CMP-001-Component-Catalog.md` - components used
  here are `SimfBanner`, `SimfAlert`, `SimfEmptyState`, `SimfSelect`,
  `SimfTextField`, `SimfButton`, and the CP-local `AuthorizedAction`.
- Note: there is no per-page CP documentation set under `docs/CP/admin-hall-arrivals/`,
  though the sibling `docs/CP/admin-halls/` does exist, and `docs/pages/PAGE-INDEX.md` line 93 still
  carries a dash placeholder rather than a link in this route's doc column. Both
  are open follow-ups.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-02 | D-244 | Page shipped as P5.1d, the operator hall-door QR-scan arrival (FDS-003 §5.4, the second arrival means). New `HallArrivals.View` / `HallArrivals.Record` permissions, the `POST /admin/sessions/{id}/arrivals` endpoint, the typed client and BFF passthrough, and the `HallArrivalsConsole` page. No schema change; `HallAttendance` and the `QrScan` enum value shipped in D-241. |
| 2026-07-18 | Not recorded as a D-number in the decisions log | Staff check-OUT. `DepartAsync` + the **Record departure** button, the `/departures` BFF route, `SimfAdminClient.RecordQrDepartureAsync`, `RecordQrDepartureEndpoint` and `IHallAttendanceService.RecordQrDepartureAsync`. Dated from the code comments in `HallArrivalsConsole.razor.cs`, `AccountEndpoints.Moderation.cs`, `SimfAdminClient.Moderation.cs` and `HallArrivalEndpoints.cs`. |
| 2026-07-26 | DEF-CHK-003 (named in the E2E catalogue and in the header comment of `HallArrivalsConsoleSessionPickerTests.cs`; the id appears nowhere under `src/` - the page's own code comment records the rationale without the id - and no decisions-log row carries it) | Session picker stopped applying the arrival window to both actions. An ended session stays selectable so its hall can be checked out; only not-yet-started sessions are filtered. Live sessions sort first. Pinned by `HallArrivalsConsoleSessionPickerTests`. |
| 2026-08-04 | D-838 | Both hall-door routes moved from `RequireRateLimiting("auth")` to `RateLimitOptions.OperationalPolicy` and were added to the reviewed allow-list in `OperationalRateLimitExemptionTests`. Nothing else changed on this page. |
| 2026-08-04 | D-839 | The picker's grace stopped being a hard-coded 15 minutes and now reads `AdminSessionSummary.EffectiveArrivalGraceMinutes`, the value the server resolved through session override -> hall -> global. D-839 added `Hall.ArrivalGraceMinutes` + `Session.ArrivalGraceMinutesOverride` (both `null` = inherit, bounded 0..240) and states in terms: "The Hall-Arrivals console's hard-coded 15 is deleted." The contract comment says the hard-coded value could see neither of the two layers above it and so disagreed with the server about which sessions are open. |
| 2026-08-19 | - | This reference doc authored. |

---

_Last reviewed:_ `2026-08-19` by Claude, from source only - the page was not driven
in a browser for this pass, and no screenshots were captured. If the page has
changed and this doc has not been re-reviewed in 60 days, it is **out of date**.
Re-walk the page in a browser and update every section that drifted.
