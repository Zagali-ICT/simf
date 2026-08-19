# Live hall (per-session monitor) - `/admin/sessions/live-hall`

| | |
|--|--|
| **Route** | `/admin/sessions/live-hall` |
| **Layout** | `CpShellLayout` (`@layout CpShellLayout`) |
| **Surface** | Control Panel |
| **Audience** | Any admin whose role holds `Attendance.View` **and** `Sessions.View`. See §2 - the page gate and the session picker do not use the same permission. |
| **Auth** | Page: `@attribute [RequirePermission(PermissionCatalog.Attendance.View)]`. API: `/seat-map` and `/present/list` both `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Attendance.View), nameof(AuthorizationPolicies.RequireApprovedAccount))`; the session picker's `/admin/attendance/sessions/list` carries the SAME `Attendance.View` gate, so the page's own permission is sufficient for every call it makes. |
| **Pattern** | **Not CRUD.** A read-only monitor: one `SimfSelect` picker, a Refresh button, a 4-state seat map and a plain roster table. No `SimfDataGrid`, no toolbar, no pager, no add / edit / delete, no `AuthorizedAction`. |
| **Status** | Real. Page comment dates it 2026-07-18 and labels it "CP page 2e"; the 15-second auto-refresh is labelled "QA B17" in the code and in the E2E catalogue. |
| **Implements use case(s)** | N/A - no `UC-` id anywhere under `docs/` names this route. Unverified: `SIMF-UCS-001` was not readable as an authored source for this page. |
| **Backend endpoints** | BFF `POST /account/api/admin/attendance/sessions/list`, `GET /account/api/admin/sessions/{sessionId}/seat-map`, `POST /account/api/admin/sessions/{sessionId}/present/list` → API `POST /api/v1/admin/attendance/sessions/list`, `GET /api/v1/admin/sessions/{sessionId:guid}/seat-map`, `POST /api/v1/admin/sessions/{sessionId:guid}/present/list`. |
| **Source file** | [`SessionLiveHall.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionLiveHall.razor) + [`.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionLiveHall.razor.cs) + [`.razor.css`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionLiveHall.razor.css); [`SessionAttendanceEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Attendance/SessionAttendanceEndpoints.cs); [`SessionAttendanceService.cs`](../../../src/Backend/SIMF.Infrastructure/Attendance/SessionAttendanceService.cs); [`SeatReservationService.cs`](../../../src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs) |
| **Backed by** | `HallAttendance`, `SeatReservation`, `UserProfile`, `HallSeatLayout` and `Hall` on `SimfAppDbContext`. Read-only - this page issues no write of any kind. |
| **Tests** | E2E [`docs/tests/e2e/cp-admin-session-live-hall.md`](../../tests/e2e/cp-admin-session-live-hall.md) (E2E-SLH-001..019 + two `-ELS-` element rows). bUnit [`tests/SIMF.ControlPanel.Tests/SessionLiveHallAutoRefreshTests.cs`](../../../tests/SIMF.ControlPanel.Tests/SessionLiveHallAutoRefreshTests.cs) (3 facts). API [`tests/SIMF.Api.Tests/SessionAttendanceTests.cs`](../../../tests/SIMF.Api.Tests/SessionAttendanceTests.cs) (5 facts touch this page's two endpoints). |
| **Last reviewed** | 2026-08-19 |

---

## 1. Purpose

The door-side view of one room. During an event an administrator needs to answer
two questions about a single hall at a glance - how full is it, and who is
actually inside right now - and neither the attendance dashboard nor the seat-plan
editor answers them together. This page pairs a colour-coded seat map with a named
roster of everyone currently in the hall, so the seat that is held but empty is
visually distinct from the seat whose holder has scanned in at the door. The
Admin Manual describes it as "the page to have open on a screen at the back of the
room" (`docs/manuals/Admin-Manual.md` §3A.3), which is why it re-reads itself every
15 seconds rather than waiting for a click. It is a monitor and nothing else:
there is no create, edit, delete or release action anywhere on it, and unlike the
seat-plan editor at `/admin/sessions/seat-plans` its seat cells are not clickable.

## 2. Audience + permissions

- **Who can reach the page:** any admin whose role holds `Attendance.View`.
  `Administrator` is the wildcard `"*"` (CLAUDE.md, D-207 / D-208), so it holds it.
- **Who can use the page fully:** anyone who can open it. `Attendance.View` alone
  is sufficient for all three calls.

  It was not always. The picker used to load from `/admin/sessions/list`, gated
  `Sessions.View`, whose baseline is ScientificCommittee - while this page's
  `Attendance.View` seeds to SecurityTeam. So a SecurityTeam operator passed the
  page gate, took a 403 on the picker load, and saw the
  `Admin.SessionLiveHall.LoadFailed` toast with no dropdown: the page was unusable
  for the exact role it exists for. The picker now reads
  `/admin/attendance/sessions/list`, which carries this page's own gate. The Admin
  Manual still says "Needs the **Attendance View** permission for the hall data,
  and the **Sessions View** permission for the session picker" - that sentence is
  now stale and is corrected with this change.
- **Who can write on it:** nobody. There is no write path.
- **Authorisation gates, all three layers:**

  | Layer | Gate |
  |-------|------|
  | Page | `@attribute [RequirePermission(PermissionCatalog.Attendance.View)]` |
  | Nav item | `CpNavigation.cs`, group `Nav.Overview`, item `Module.SessionLiveHall`, `RequiredPermission: PermissionCatalog.Attendance.View`, `Icon: "monitor"` |
  | API `GET /admin/sessions/{sessionId:guid}/seat-map` | `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Attendance.View), nameof(AuthorizationPolicies.RequireApprovedAccount))` |
  | API `POST /admin/sessions/{sessionId:guid}/present/list` | `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Attendance.View), nameof(AuthorizationPolicies.RequireApprovedAccount))` |
  | API `POST /admin/attendance/sessions/list` (picker) | `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Attendance.View), nameof(AuthorizationPolicies.RequireApprovedAccount))` |

- **Permission code:** `PermissionCatalog.Attendance.View = "Attendance.View"`
  (`PermissionCatalog.cs:741`). Its catalogue row is
  `new(Attendance.View, "Attendance", "View", "View the session-attendance dashboard", SecurityTeam)`
  (`PermissionCatalog.cs:1232`), and `SecurityTeam` is
  `[AppRoles.SecurityTeam]` (`PermissionCatalog.cs:851`). It is **not** seeded
  `AdminOnly` - `AdminOnly` is the empty list `[]` (`PermissionCatalog.cs:846`).
  The E2E catalogue's line "`Attendance.View` seeds as `AdminOnly`" has drifted
  from the source; the source above is authoritative.
- **No new permission is introduced by this page.** It reuses `Attendance.View`,
  which the attendance dashboard at `/admin/attendance` already carries.
- **What a signed-in admin without the permission sees:** the nav item is hidden
  (`CpNavigation` filters on `RequiredPermission`) and a direct navigation is sent
  to `/not-permitted` by `RedirectToNotPermitted`
  (`RedirectToNotPermitted.razor.cs:26`, `Nav.NavigateTo("/not-permitted")`).
- **What an unauthenticated user sees:** `/login`. The CP cookie scheme sets
  `options.LoginPath = "/login"` and `options.AccessDeniedPath = "/not-permitted"`
  (`Program.cs:76-77`). Independently, every BFF call on this page runs through
  `simfAccount`, whose envelope reader sends the browser to `/login` on HTTP 401
  (`simf-account.js:18-21`), so a session that expires while the 15-second poll is
  running ends in a redirect rather than a silent stream of failed ticks.
- **Enforcement is tested, not asserted:** `SessionAttendanceTests` holds
  `Present_is_forbidden_for_a_non_admin` and
  `Admin_seat_map_is_forbidden_for_a_non_admin`.
  Unverified: neither `/present/list` nor `/seat-map` appears in
  `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` and no `live-hall` route
  appears in `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`. Those
  two suites may cover the page through generic sweeps; the sweeps were not read
  here, so that coverage is not claimed.

## 3. Screenshots

**No screenshots of this page exist.** `docs/screenshots/` contains no file whose
name matches `live-hall`. The paths below are the names the E2E catalogue reserves
for them, listed so a later capture pass has a target; every one is uncaptured.

| State | File | Captured |
|-------|------|----------|
| Session picked, panels loading | `docs/screenshots/cp-admin-session-live-hall-golden-before.png` | Not captured |
| Seat map + present table | `docs/screenshots/cp-admin-session-live-hall-golden-after.png` | Not captured |
| Hall with no seat layout | `docs/screenshots/cp-admin-session-live-hall-no-layout.png` | Not captured |
| Nobody inside (present empty state) | `docs/screenshots/cp-admin-session-live-hall-empty.png` | Not captured |
| No active sessions (page empty state) | `docs/screenshots/cp-admin-session-live-hall-none.png` | Not captured |
| RTL (Arabic) | `docs/screenshots/cp-admin-session-live-hall-rtl.png` | Not captured |
| Error toast | `docs/screenshots/cp-admin-session-live-hall-error.png` | Not captured |

Only the first two are named in the E2E catalogue; the other five are proposed
here to match the states §4 documents.

## 4. UI affordances

The whole page is `SimfBanner` → `div.simf-page-wide` → `div.simf-surface`, and
inside that surface, in order: an optional `SimfAlert`, a hint paragraph, and then
one of three mutually exclusive branches - loading, the page empty state, or the
picker.

### 4.1 Banner / page header

`<SimfBanner Title="@L["Admin.SessionLiveHall.Title"]" />` - title only. No
subtitle, no actions slot. `Admin.SessionLiveHall.Title` is "Live hall" /
"القاعة المباشرة". `<PageTitle>` renders `@L["Admin.SessionLiveHall.Title"] · SIMF`.

Directly under the banner sits `<p class="simf-hint">@L["Admin.SessionLiveHall.Hint"]</p>`:

> "Pick a session to watch its hall live — the seat map (available / unavailable /
> reserved / confirmed) and everyone currently inside."

### 4.2 Toolbar (CRUD pages only)

N/A - not a CRUD list page. There is no toolbar, no multiselect, and no
Add / Edit / Details / Delete / Copy / Paste / Duplicate / Import / Export.

The page's only two interactive controls are:

| Control | Wired callback | Calls | Notes |
|---------|----------------|-------|-------|
| Session picker (`SimfSelect<AdminSessionSummary>`) | `ValueChanged="OnSessionChangedAsync"` | `/seat-map` + `/present/list` for the newly picked session | `Label` and `Placeholder` both `Admin.SessionLiveHall.Pick` ("Select a session" / "اختر جلسة"). `ValueFor` is `s => s.Id.ToString()`; `LabelFor` is `s => $"{s.Code} — {s.Title}"`. `Disabled="_busy"`. Rendered only when `_sessions.Count > 0`. |
| Refresh (`SimfButton Type="button"`) | `OnClick="RefreshAsync"` | the same two reads | Label `Admin.SessionLiveHall.Refresh` ("Refresh" / "تحديث"). `Loading="_busy"`, `LoadingLabel="@L["Grid.Working"]"` ("Working…" / "جارٍ التنفيذ…"). Rendered only when `_selected is not null`. |

Neither control is wrapped in `<AuthorizedAction>`, and correctly so: both drive
reads gated by the page's own `Attendance.View`, so an admin who can see the page
can use them.

### 4.3 Grid columns (CRUD pages only)

N/A - not a CRUD list page. The page hosts no `SimfDataGrid`. Its two data panels
are described below.

#### Panel 1 - the seat map (`Admin.SessionLiveHall.SeatMap.Heading`, "Seat map" / "خريطة المقاعد")

A read-only 4-state grid built from `SessionSeatMap`. `_map.RowLabels` drives the
rows; `SeatsInRow(r)` returns `_map.SeatCounts[r]` when the layout is ragged and
`_map.SeatsPerRow` otherwise, tolerating a short or absent `SeatCounts` so a
length-mismatched payload still renders. Each seat is a `<span class="seatmap__seat ...">`
carrying the seat number as text and a `title` tooltip.

| State | Condition in `SeatStateClass` | CSS class | Token | Label key (EN / AR) |
|-------|-------------------------------|-----------|-------|---------------------|
| Available | no `SessionSeatCell` for (row, seat) | `seatmap__seat--available` | `--color-seat-free` | `Admin.SessionLiveHall.Seat.Available` - "Available" / "متاح" |
| Unavailable | `cell.Kind == SeatReservationKind.AdminReservedRow` | `seatmap__seat--unavailable` | `--color-seat-admin` | `Admin.SessionLiveHall.Seat.Unavailable` - "Unavailable" / "غير متاح" |
| Reserved | a holder with `cell.CheckedIn == false` | `seatmap__seat--reserved` | `--color-seat-user` | `Admin.SessionLiveHall.Seat.Reserved` - "Reserved" / "محجوز" |
| Confirmed | a holder with `cell.CheckedIn == true` | `seatmap__seat--confirmed` | `--color-seat-confirmed` | `Admin.SessionLiveHall.Seat.Confirmed` - "Confirmed (checked in)" / "تم التأكيد" |

The tooltip is `SeatTitle`, formatting `Admin.SessionLiveHall.SeatTitle`
("Seat {0} — {1}" / "مقعد {0} — {1}") with `$"{rowLabel}{seatNumber}"` and the
state label. A legend under the grid repeats all four states with matching
swatches (`seatmap__swatch--available` ... `--confirmed`).

Colours come from `theme.tokens.css` tokens only, no literals in the page CSS:
`--color-seat-free: var(--color-surface-sunken)`, `--color-seat-user: #C9A84C`,
`--color-seat-admin: #01132D`, `--color-seat-confirmed: var(--color-success)`,
plus `--color-seat-contrast` and `--color-seat-user-contrast` for the labels.
D-767 records that these were aligned to the same palette the Flutter app uses,
so one seat state is one colour in both surfaces.

When `_map is null || _map.RowLabels.Count == 0` the panel renders
`<SimfEmptyState Title="@L["Admin.SessionLiveHall.SeatMap.NoLayout"]" />` -
"This hall has no seat layout, so there is no seat map to show." /
"لا يوجد تخطيط مقاعد لهذه القاعة، لذا لا توجد خريطة مقاعد لعرضها."

#### Panel 2 - "In the hall now" (`Admin.SessionLiveHall.Present.Heading`, "In the hall now" / "داخل القاعة الآن")

A plain `<table class="simf-table">` inside `div.simf-table-wrapper`. Seven
columns, no sort, no filter, no pager, no row actions.

| Column | Header key | Source field | Rendered by |
|--------|-----------|--------------|-------------|
| Name | `Admin.SessionLiveHall.Col.Name` ("Name" / "الاسم") | `Name`, falling back to `NameArabic` | `PresentName` - `string.IsNullOrWhiteSpace(attendee.Name) ? attendee.NameArabic : attendee.Name` |
| Organisation | `Admin.SessionLiveHall.Col.Organisation` ("Organisation" / "الجهة") | `OrganisationName` | direct |
| Type | `Admin.SessionLiveHall.Col.Type` ("Type" / "النوع") | `ProfileTypeName` | direct |
| Job title | `Admin.SessionLiveHall.Col.JobTitle` ("Job title" / "المسمى الوظيفي") | `JobTitle` | direct |
| Seat | `Admin.SessionLiveHall.Col.Seat` ("Seat" / "المقعد") | `RowLabel` + `SeatNumber` | `SeatLabel` - `$"{RowLabel}{SeatNumber}"` when both are non-null, otherwise `Admin.SessionLiveHall.OpenSeating` ("General admission" / "دخول عام") |
| Entered (Saudi time) | `Admin.SessionLiveHall.Col.Entered` ("Entered (Saudi time)" / "وقت الدخول (بتوقيت السعودية)") | `Enter` | `Entered` - `enter.FormatSaudi("dd-MM-yyyy hh:mm tt")` |
| Method | `Admin.SessionLiveHall.Col.Method` ("Method" / "الطريقة") | `Method` | `MethodLabel` - `AttendanceMethod.QrScan` → `Admin.SessionLiveHall.Method.QrScan` ("QR scan" / "مسح QR"); `AttendanceMethod.Geofence` → `Admin.SessionLiveHall.Method.Geofence` ("Geofence" / "تحديد الموقع"); any other value falls back to `method.ToString()` |

When `_present.Count == 0` the panel renders
`<SimfEmptyState Title="@L["Admin.SessionLiveHall.Present.None"]" />` -
"No one is inside the hall yet." / "لا يوجد أحد داخل القاعة بعد."

Below the table sits `<p class="simf-table__summary">` formatting
`Admin.SessionLiveHall.Present.Count` ("{0} present" / "عدد الحاضرين: {0}") with
`_presentTotal`, the server's count over the whole roster. When
`_present.Count < _presentTotal` a `span.simf-hint` beside it formats
`Grid.Summary` ("Showing {0}–{1} of {2}" / "عرض {0}–{1} من {2}") with
`1, _present.Count, _presentTotal`. The page comment gives the reason: the roster
is one server page, and a hall holding more than a page "has to read as a
truncated view rather than as the whole room".

### 4.4 Pager

N/A - not a CRUD list page. There is no pager, no page-size selector and no
`Showing X-Y of Z` caption control. The read behind the roster **is** paged (see
§5), but the window is fixed in code at `PresentPageSize = 200` and the admin has
no control that changes it. The truncation hint described in 4.3 is the only place
the paging becomes visible.

### 4.5 Form fields

N/A - the page hosts no form and no modal. The only input is the session picker
documented in 4.2, which posts nothing.

## 5. Data flow

```
OnInitializedAsync
  -> LoadSessionsAsync
  -> JS simfAccount.postJson("/account/api/admin/attendance/sessions/list", GridQuery{Top=200})
  -> BFF AccountEndpoints.FeedbackAndReports.cs  group.MapPost("/admin/attendance/sessions/list")
  -> SimfAdminClient.ListSessionAttendanceAsync
  -> API POST /api/v1/admin/attendance/sessions/list  (ListSessionAttendanceEndpoint, Attendance.View)
  -> ISessionAttendanceService.ListSessionAttendanceAsync  (Where(session => session.IsActive))
  -> ApiResult<GridPage<SessionAttendanceRow>>
  -> _sessions = Items.OrderBy(s => s.Code)

Session picked (OnSessionChangedAsync) / Refresh clicked (RefreshAsync)
  / 15-second PeriodicTimer tick (AutoRefreshLoopAsync)
  -> LoadHallAsync
  -> JS simfAccount.getJson("/account/api/admin/sessions/{id}/seat-map")
     -> BFF AccountEndpoints.SeatingAndMeetings.cs:106  group.MapGet(...)
     -> SimfAdminClient.GetAdminSessionSeatMapAsync
     -> API GET /api/v1/admin/sessions/{sessionId:guid}/seat-map
        (GetAdminSessionSeatMapEndpoint, Attendance.View)
     -> ISeatReservationService.GetSessionSeatMapAsync(sessionId, actorUserId: null)
     -> SimfAppDbContext: Sessions, HallSeatLayout, SeatReservations, HallAttendances, Halls
     -> ApiResult<SessionSeatMap>  -> _map
  -> JS simfAccount.postJson("/account/api/admin/sessions/{id}/present/list",
                             GridQuery{Top=200})
     -> BFF AccountEndpoints.SeatingAndMeetings.cs:114  group.MapPost(...)
     -> SimfAdminClient.ListSessionPresentAttendeesAsync
     -> API POST /api/v1/admin/sessions/{sessionId:guid}/present/list
        (ListSessionPresentAttendeesEndpoint, Attendance.View)
     -> ISessionAttendanceService.GetPresentAttendeesAsync
     -> SimfAppDbContext: HallAttendances, UserProfiles, SeatReservations
     -> ApiResult<GridPage<SessionPresentAttendee>>  -> _present, _presentTotal
  -> stale-response guard: if (_selected?.Id != session.Id) return;
  -> render + optional SimfAlert toast
```

Every call goes through the Control Panel BFF. The browser sends the auth cookie
same-origin and the BFF reads the access token with `http.GetTokenAsync("access_token")`
before forwarding, so the Blazor circuit never handles the bearer token
(`simf-account.js` header comment; `AccountEndpoints.SeatingAndMeetings.cs:109-111`
and `118-120`). A BFF handler with no token returns `Results.Unauthorized()`, and
`simfReadEnvelope` turns an HTTP 401 into a full-page navigation to `/login`.

### Every backend call this page makes

| When | Page call | BFF mapping | `SimfAdminClient` method | API endpoint + gate | Response shape |
|------|-----------|-------------|--------------------------|---------------------|----------------|
| `OnInitializedAsync` | `simfAccount.postJson`, body `new GridQuery { Top = 200 }` | `AccountEndpoints.FeedbackAndReports.cs` `MapPost("/admin/attendance/sessions/list")` | `ListSessionAttendanceAsync` | `POST /api/v1/admin/attendance/sessions/list` - `ListSessionAttendanceEndpoint`, `Attendance.View` + `RequireApprovedAccount` | `ApiResult<GridPage<SessionAttendanceRow>>` |
| Session picked / Refresh / 15 s tick | `simfAccount.getJson`, no body | `AccountEndpoints.SeatingAndMeetings.cs:106` `MapGet("/admin/sessions/{sessionId:guid}/seat-map")` | `GetAdminSessionSeatMapAsync` (relative `sessions/{sessionId}/seat-map`) | `GET /api/v1/admin/sessions/{sessionId:guid}/seat-map` - `GetAdminSessionSeatMapEndpoint`, `Attendance.View` + `RequireApprovedAccount` | `ApiResult<SessionSeatMap>` |
| Session picked / Refresh / 15 s tick | `simfAccount.postJson`, body `new GridQuery { Top = 200 }` | `AccountEndpoints.SeatingAndMeetings.cs:114` `MapPost("/admin/sessions/{sessionId:guid}/present/list")`, binding `GridQuery` | `ListSessionPresentAttendeesAsync` (relative `sessions/{sessionId}/present/list`) | `POST /api/v1/admin/sessions/{sessionId:guid}/present/list` - `ListSessionPresentAttendeesEndpoint`, `Attendance.View` + `RequireApprovedAccount` | `ApiResult<GridPage<SessionPresentAttendee>>` |

The API route prefix is `api/v1`, set once in `Program.cs:703`
(`config.Endpoints.RoutePrefix = "api/v1"`), which is why the FastEndpoint
declarations read `Get("/admin/sessions/{sessionId:guid}/seat-map")` with no
version segment and `SimfAdminClient` sends relative paths.

### Contract shapes

`SessionSeatMap` (`src/Shared/SIMF.Contracts/Sessions/SeatReservations.cs:10`):
`SessionId`, `HallId`, `HallCapacity`, `SessionCapacity`, `RowLabels`,
`SeatsPerRow`, `ReservedCells`, `MyCell`, `ActiveReservedCount`, then the
append-only tail `SessionTitle`, `SessionTitleArabic`, `Mode`, `SeatCounts`,
`SeatTiers` (line 39) and `CallerIsVip` (line 44). The admin endpoint passes
`actorUserId: null`, so `MyCell` is always null here - there is no "my seat" on a
monitor - and `CallerIsVip` is false for the same reason
(`SeatReservationService.cs:162`, `var callerIsVip = actorUserId is { } tierActor`).
The page reads neither, and it does not render `SeatTiers` either: the per-row tier
band is on the seat-plan editor (`SessionSeatPlan.razor:97`,
`seatgrid__row--@TierModifier(tier)`), not on this monitor.

`SessionSeatCell` (same file, line 61): `ReservationId`, `RowLabel`,
`SeatNumber`, `Kind`, `Status`, `CheckedIn`, `GuestHint`, `GuestHintArabic`. The
page reads `Kind` and `CheckedIn` only.

`SessionPresentAttendee` (`src/Shared/SIMF.Contracts/Attendance/AttendanceContracts.cs:52`):
`UserProfileId`, `Name`, `NameArabic`, `OrganisationName`, `ProfileTypeName`,
`JobTitle`, `RowLabel`, `SeatNumber`, `Enter`, `Method`.

### The paged roster contract

`GetPresentAttendeesAsync` scopes the query to
`attendance.SessionId == sessionId && attendance.Leave == null` **before** the
grid seam, so neither the session nor the still-inside test can be widened by a
request. The declared columns are minimal:

```csharp
private static readonly GridColumns<HallAttendance> PresentColumns =
    new GridColumns<HallAttendance>()
        .Add("enter", attendance => attendance.Enter)
        .DefaultOrder("enter")
        .PageSize(fallback: 50, max: 200);
```

One sortable key (`enter`, ascending by default - arrival order), no searchable
column, no filterable key, page size falling back to 50 and capped at 200. The
source comment gives the reason: name, organisation and seat "are resolved AFTER
paging, from other tables, so they are not sortable here and are not offered as
if they were". `ToGridPageAsync` is handed `attendance => attendance.Id` as the
tiebreak, which is what stops an attendee appearing on two pages when several
rows share an `Enter`.

### Cross-database discipline

Profile fields are resolved from `appDbContext.UserProfiles` only, matched by
**profile** id, which is the id the attendance row carries. The service comment is
explicit that this is never a cross-DB Identity join, and the CLAUDE.md D-157
Data ↔ Identity separation rule is what it is honouring. A consequence recorded
in the same comment: "a walk-in in the hall appears on this roster with their real
name rather than as a blank line". Seats come from a second App-DB query over
`SeatReservations` filtered to `ReleasedAt == null` and a non-null
`ReservedForProfileId`.

## 6. Validation + error handling

- **Client-side guards:** `LoadHallAsync` returns immediately unless
  `_selected is { } session`, and returns if `_inFlight` is already true - one
  in-flight guard shared by the Refresh button and the background tick so a slow
  pull can never overlap itself. After both awaits it re-checks
  `_selected?.Id != session.Id` and drops the response if the admin switched
  sessions mid-flight.
- **Server-side validation:** there is no validator, because there is no write and
  no user-authored input. The only thing the server rejects is a malformed grid
  request, and the page cannot produce one - it always sends
  `new GridQuery { Top = 200 }` with no `Sort`, `Search` or `Filters`. A
  hand-written request that names an undeclared key gets a 400 from the shared
  grid seam:

  | Condition | Code (`ErrorCodes.cs`) | HTTP |
  |-----------|------------------------|------|
  | Sort key not declared | `GRID_SORT_KEY_INVALID` (`GridColumns.cs:237`) | 400 |
  | Filter key not declared | `GRID_FILTER_KEY_INVALID` (`GridColumns.cs:258`, `GridQueryComposition.cs:91`) | 400 |
  | Filter value unparseable | `GRID_FILTER_VALUE_INVALID` | 400 |
  | Search term sent to a list with no searchable column | `GRID_SEARCH_NOT_SUPPORTED` (`GridQueryComposition.cs:156`) | 400 |

  The `ErrorCodes.cs` comment explains why these are 400s and not silent no-ops:
  "dropping one widens the result set, which is how several admin columns shipped
  looking sortable and were not."
- **Error envelope:** the standard `ApiResult<T>` with `Error.Code` and bilingual
  `Message` / `MessageArabic`. The page reads it through
  `env.Error.MessageForCurrentCulture()`.
- **Non-JSON responses:** `simfReadEnvelope` synthesises an `ApiResult` error with
  code `BAD_RESPONSE` and a bilingual message rather than throwing a `JSException`
  that would trip the global Blazor error UI (`simf-account.js:28-40`).
- **Toast strategy:** one `Toast(Variant, Message)` record rendered as
  `<SimfAlert Variant="@_toast.Variant">`, always `"error"` on this page. The
  fallback message is `Admin.SessionLiveHall.LoadFailed` - "Could not load the
  live hall. Please try again." / "تعذّر تحميل القاعة المباشرة. حاول مرة أخرى."
  It is raised in four places: a failed session list, a failed seat map, a failed
  roster, and the catch-all around both reads. The toast is cleared at the top of
  `OnSessionChangedAsync` and `RefreshAsync`, so a stale error never survives a
  session switch or a manual retry.
- **The two panels fail independently.** If the roster call fails while the seat
  map succeeds, `_present` is reset to empty and `_presentTotal` to 0 alongside
  the toast. The source comment says why: showing an empty hall silently, or
  leaving the previous roster on screen after a failed Refresh, would both be
  misleading.

## 7. Edge cases + known limitations

- **The picker no longer needs a second permission.** It used to: the page gates
  on `Attendance.View` while `/admin/sessions/list` gates on `Sessions.View`, so
  an operator holding only the page's own permission reached the page, took a 403
  on the picker load, and got the `LoadFailed` toast with no dropdown and no way
  forward. Both baselines made that concrete rather than theoretical:
  `Attendance.View` seeds to SecurityTeam, `Sessions.View` to
  ScientificCommittee. The picker now reads `/admin/attendance/sessions/list`,
  which carries this page's own gate.
- **The session list is one 200-row page, scoped server-side.**
  `LoadSessionsAsync` asks for `Top = 200`; the endpoint applies
  `Where(session => session.IsActive)` as the resource's own scope, ahead of the
  grid filters and where no request can widen it. Past 200 sessions the dropdown
  silently misses some: the 200 is the page size, and the scope is applied
  before it, so an event with more than 200 active sessions would truncate.
- **The placeholder option cannot be re-selected in a browser.** `SimfSelect`
  renders it as `<option value="" disabled ...>` (`SimfSelect.razor:26`). The
  "clear the selection" branch of `OnSessionChangedAsync` therefore has no UI
  route on this page; `SessionLiveHallAutoRefreshTests` exercises it by driving
  `cut.Find("select").Change(string.Empty)` directly. The code path is correct
  and tested, it is simply not reachable by clicking.
- **Entry times are Saudi local, rendered invariant.** `Entered` calls
  `enter.FormatSaudi("dd-MM-yyyy hh:mm tt")`, and `SaudiTime.FormatSaudi`
  documents itself as "No conversion — the value is already Saudi local", using
  `CultureInfo.InvariantCulture`. So the Arabic UI still shows Latin digits and
  `AM` / `PM`. D-765 records the sweep that relabelled this column from "(UTC)"
  to "(Saudi time)" because the value was Saudi wall-clock end to end and the
  old label was simply wrong.
- **The 15-second poll is silent.** `LoadHallAsync(interactive: false)` does not
  set `_busy`, so a background tick never disables the picker and never spins the
  Refresh button. The XML comment states this is deliberate - flipping the busy
  flag every 15 seconds would make the page unusable.
- **The poll cannot outlive the page.** `StopAutoRefresh` cancels and disposes
  both the `PeriodicTimer` and the `CancellationTokenSource`, and is called from
  `OnSessionChangedAsync` (before a new loop starts) and from `Dispose()`. The
  timer instance is passed into `AutoRefreshLoopAsync` by value so a restarted
  loop cannot dispose-race the new one. `AutoRefreshLoopAsync` swallows
  `OperationCanceledException`, `ObjectDisposedException` and a final catch-all,
  each with a comment naming the teardown it covers. The class comment states the
  stake plainly: a `PeriodicTimer` left running on a Blazor Server circuit is a
  real leak.
- **A transient failure does not kill the poll.** The catch-all in
  `LoadHallAsync` raises the toast and returns, so the next tick retries.
- **An open-seating attendee is on the roster but not on the map.** `SeatLabel`
  falls back to "General admission" / "دخول عام" when `RowLabel` or `SeatNumber`
  is null, and with no reservation row there is no seat cell to paint.
- **A present user with no `UserProfile` row renders as blank cells, not an
  error.** `profileById.GetValueOrDefault(...)` returns null and the row is built
  with `profile?.Name ?? string.Empty` and null organisation / type / job title.
- **A ragged layout renders even if `SeatCounts` is short.** `SeatsInRow` falls
  back to `SeatsPerRow` whenever `SeatCounts` is empty or the row index is past
  its end, so a length-mismatched payload degrades to uniform rows rather than
  throwing. D-767 is the decision that added the parallel `SeatCounts` CSV and
  kept `SeatsPerRow` as the uniform fallback.
- **`FindCell` is a linear scan per seat.** It runs
  `_map.ReservedCells.FirstOrDefault(...)` for every rendered seat, so the cost is
  seats × reserved cells. The code comment above it claims "O(1) per seat", which
  the implementation does not deliver. Not a correctness problem at hall scale,
  but the comment and the code disagree.
- **Nothing on this page writes.** There is no seat release, no check-out, no
  admin override. Those live on `/admin/sessions/seat-plans` and the hall-arrival
  desk.

## 8. i18n + RTL

- Every visible string comes from `Strings.resx` (EN) + `Strings.ar.resx` (AR) via
  `IStringLocalizer<Strings> L`. This page owns 27 keys, all present in both
  files: `Admin.SessionLiveHall.Title`, `.Hint`, `.Pick`, `.None`, `.Loading`,
  `.LoadFailed`, `.Refresh`, `.SeatMap.Heading`, `.SeatMap.NoLayout`,
  `.Seat.Available`, `.Seat.Unavailable`, `.Seat.Reserved`, `.Seat.Confirmed`,
  `.SeatTitle`, `.Present.Heading`, `.Present.None`, `.Present.Count`,
  `.Col.Name`, `.Col.Organisation`, `.Col.Type`, `.Col.JobTitle`, `.Col.Seat`,
  `.Col.Entered`, `.Col.Method`, `.Method.QrScan`, `.Method.Geofence`,
  `.OpenSeating`. Plus the nav label `Module.SessionLiveHall` ("Live hall" /
  "القاعة المباشرة") and two shared keys, `Grid.Working` and `Grid.Summary`.
- Three keys are format strings and must keep their placeholders in both
  languages: `.SeatTitle` ("Seat {0} — {1}" / "مقعد {0} — {1}"),
  `.Present.Count` ("{0} present" / "عدد الحاضرين: {0}"), and the shared
  `Grid.Summary` ("Showing {0}–{1} of {2}" / "عرض {0}–{1} من {2}").
- **Not localized:** the entry time (invariant culture, see §7) and the
  `MethodLabel` fallback `method.ToString()`, which would surface a bare enum name
  if a third `AttendanceMethod` value is ever added without a resx pair.
- RTL: the page carries no direction-specific CSS of its own. `.seatmap__row` and
  `.seatmap__legend` are flex containers, so both mirror with the document
  direction; `.seatmap` sets `overflow-x: auto`, so a hall wider than the surface
  scrolls inside the seat area rather than overflowing the page body.
- The Arabic seat-state vocabulary is متاح / غير متاح / محجوز / تم التأكيد, and
  the roster headers are الاسم / الجهة / النوع / المسمى الوظيفي / المقعد /
  وقت الدخول (بتوقيت السعودية) / الطريقة.

## 9. Accessibility

- **Session picker:** `SimfSelect` renders a native `<select>` with a
  `<label class="simf-field__label" for="@_id">` bound to it, so it is labelled,
  keyboard-operable and screen-reader-navigable without custom code - the
  component's own comment says "The native `<select>` stays accessible and
  keyboard-friendly for free; we add nothing custom."
- **Error toast:** `SimfAlert Variant="error"` renders `role="alert"`, so a load
  failure is announced assertively. The other two variants are `role="status"`
  with `aria-live="polite"`; this page only ever uses `error`.
- **Seat cells are not focusable and are not announced.** They are plain
  `<span>` elements with a `title` attribute, no `role`, no `tabindex`, no
  `aria-label`. The state is conveyed by background colour plus the tooltip, and
  a `title` is not reliably exposed to screen readers or reachable by keyboard.
  The legend under the grid gives the four colours their names in text, which is
  the only non-hover route to the mapping. **Known gap** - a screen-reader user
  cannot read an individual seat's state.
- **Roster table:** `<table class="simf-table">` with a `<thead>` of plain `<th>`
  cells. No `<caption>`, no `scope="col"`. **Known gap.**
- **Colour contrast:** the seat tokens pair each dark fill with an explicit label
  token (`--color-seat-contrast: #FFFFFF` on navy and steel,
  `--color-seat-user-contrast: #01132D` on the gold user seat), and
  `theme.tokens.css` redefines `--color-seat-admin` for the dark theme. Unverified:
  no contrast measurement for these pairs was found in the repository.
- **Focus indicators:** the `--focus-ring` token is defined in
  `theme.tokens.css` (line 97 on `:root`, redefined at 287 for `[data-theme="dark"]`
  and at 362 for `[data-theme="grey"]` - the grey theme is a neutral mid-grey
  surface set, not a high-contrast one, per its own header comment at line 330) and
  is consumed by both of the page's focusable controls: the picker through
  `.simf-field__control:focus-within` (`simf-components.css:527`, the wrapper
  `SimfSelect` puts round its native `<select>`) and the Refresh button through
  `.simf-button:focus-visible` (`simf-components.css:689`).

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| N/A | - | No `UC-` identifier under `docs/` names `/admin/sessions/live-hall`. The functional anchor found instead is **FR-506**, which `CpNavigation.cs` cites in a comment directly above the `Module.Attendance` and `Module.SessionLiveHall` nav entries. |

## 11. Related E2E test scenarios

Catalogue: [`docs/tests/e2e/cp-admin-session-live-hall.md`](../../tests/e2e/cp-admin-session-live-hall.md)
(note the singular `session` in that filename; this doc uses the plural route slug).

| Scenario | Id | Coverage |
|----------|----|----------|
| Golden path - pick a session, both panels render | E2E-SLH-001 | Picker → `/seat-map` + `/present/list`, seat states and the present summary |
| Four seat states with the right swatch and tooltip | E2E-SLH-002 | `SeatStateClass` / `SeatTitle` / the legend |
| Roster columns and arrival order | E2E-SLH-003 | The seven columns in §4.3 |
| Refresh re-pulls both panels | E2E-SLH-004 | `RefreshAsync` |
| Hall with no seat layout | E2E-SLH-005 | `Admin.SessionLiveHall.SeatMap.NoLayout` empty state |
| Nobody inside | E2E-SLH-006 | `Admin.SessionLiveHall.Present.None` empty state |
| No active sessions | E2E-SLH-007 | `Admin.SessionLiveHall.None` page empty state |
| Auth gate (`Attendance.View`) | E2E-SLH-008 | `/not-permitted`, hidden nav item, API 403 |
| Server 500 on `/seat-map` | E2E-SLH-009 | `Admin.SessionLiveHall.LoadFailed` toast |
| RTL render | E2E-SLH-010 | §8 |
| Open-seating attendee | E2E-SLH-011 | `SeatLabel` fallback |
| Session switch clears the prior hall | E2E-SLH-012 | `OnSessionChangedAsync` reset + the stale-response guard |
| Cross-DB safety (App-DB only) | E2E-SLH-013 | §5 "Cross-database discipline" |
| Auto-refresh surfaces a door scan | E2E-SLH-014 | `RefreshInterval` = 15 s |
| Poll lifetime and disposal | E2E-SLH-015 | `StartAutoRefresh` / `StopAutoRefresh` / `Dispose` |
| One page, true total | E2E-SLH-016 | `PresentPageSize` = 200, `PageSize(fallback: 50, max: 200)` |
| Undeclared sort / search / filter keys are 400s | E2E-SLH-017 | `PresentColumns`, the four `GRID_*` codes in §6 |
| Window advances without repeating an attendee | E2E-SLH-018 | The `attendance.Id` tiebreak |
| Total counts this session's open rows only | E2E-SLH-019 | `SessionId == sessionId && Leave == null` |
| Element inventory / element health | E2E-SLH-ELS-001, -ELS-002 | Not authored |

### Code-level tests

| Test | File | Covers |
|------|------|--------|
| `B17_no_session_selected_means_no_poll_timer` | `tests/SIMF.ControlPanel.Tests/SessionLiveHallAutoRefreshTests.cs` | E2E-SLH-015 |
| `B17_selecting_a_session_starts_the_poll_and_disposing_stops_it` | same | E2E-SLH-015 |
| `B17_clearing_the_selection_stops_the_poll` | same | E2E-SLH-015 |
| `Present_lists_currently_inside_attendees_with_profile_and_seat` | `tests/SIMF.Api.Tests/SessionAttendanceTests.cs` | E2E-SLH-001 / -003 |
| `Present_excludes_a_departed_attendee` | same | E2E-SLH-019 |
| `Present_is_forbidden_for_a_non_admin` | same | E2E-SLH-008 |
| `Admin_seat_map_marks_a_checked_in_reservation_confirmed` | same | E2E-SLH-002 |
| `Admin_seat_map_is_forbidden_for_a_non_admin` | same | E2E-SLH-008 |

**Two catalogue references do not resolve.** The catalogue marks E2E-SLH-013
"authored ✓ (API `Present_attendees_are_resolved_from_app_profiles_only`)", but no
test of that name exists anywhere under `tests/` - the App-DB-only guarantee is
carried by the service implementation and its comment, not by a named test. The
catalogue also lists E2E-SLH-016 to -019 as "authored", and no test under `tests/`
other than `SessionAttendanceTests.cs` calls `present/list`, so the paged-contract
scenarios are catalogue text rather than executable coverage.

`tests/SIMF.ControlPanel.Tests/TimerCallbackSafetyTests.cs` explicitly excludes
this page, and says why: `PeriodicTimer` loops "are not covered here because they
await inside a `Task`, where an escape is an unobserved task exception rather than
a process kill — a different, survivable bug."

## 12. Related docs

- **Admin Manual:** `docs/manuals/Admin-Manual.md` §3A.3 "Live hall —
  `/admin/sessions/live-hall`" (under 3A. Overview modules). It is the source for
  the two-permission requirement in §2.
- **Page index:** `docs/pages/PAGE-INDEX.md:141`. That row currently carries no
  doc-column link; it should point at this file.
- **E2E index:** `docs/tests/e2e/README.md:166`. That row reads "E2E-SLH-001..015"
  and predates E2E-SLH-016 to -019 and the two `-ELS-` rows.
- **Neighbouring pages:** [`admin-sessions-seat-plans.md`](admin-sessions-seat-plans.md)
  (the writable seat editor - where a seat is released), and
  [`admin-halls-seat-layouts.md`](admin-halls-seat-layouts.md) (where the
  `RowLabels` / `SeatsPerRow` / `SeatCounts` layout this map renders is authored).
- **Decisions:** D-767 (per-row `SeatCounts` and the alignment of the
  `--color-seat-*` tokens to the app palette, naming `SessionLiveHall` among its
  downstream files) and D-765 (the "(UTC)" → "(Saudi time)" label sweep, naming
  "Session-live-hall" among the 21 corrected values), both in
  `docs/decisions/DECISIONS_LOG.md`. The D-157 Data ↔ Identity separation rule is
  quoted from `CLAUDE.md`. No decision id was found that introduced this page; the
  code comments label it "CP page 2e" (2026-07-18) and "QA B17" (the auto-refresh).
- **Template authority:** `docs/pages/_TEMPLATE.md`, which cites D-133.
- **Architecture / API spec:** `SIMF-SAD-001` and `SIMF-API-001` were not read for
  this doc; no section is cited so none is claimed.
- **Per-page CP documentation set:** N/A - `docs/CP/` has no
  `admin-session-live-hall` folder.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-07-18 | "CP page 2e" (no D-id found) | Page created - the live per-session hall view: session picker, 4-state seat map from `GET /admin/sessions/{id}/seat-map`, and the "In the hall now" roster. Read-only, gated `Attendance.View`. |
| 2026-07-25 | D-767 | The `--color-seat-*` tokens were aligned to the Flutter app's palette (gold user, navy admin, steel random) so a seat state is one colour in both surfaces, and `SessionLiveHall` was updated for the additive per-row `SeatCounts`, which is what `SeatsInRow` reads today. |
| 2026-07-25 | D-765 | The "Entered" column label was corrected from "(UTC)" to "(Saudi time)" / "(بتوقيت السعودية)" as part of a 21-value sweep; the stored instant and the DTO names were unchanged. |
| 2026-07-26 | "QA B17" (no D-id found) | Auto-refresh - while a session is selected both reads re-run on a 15-second `PeriodicTimer` (`RefreshInterval`), silently (no busy flag), with the timer cancelled and disposed on session change and on `Dispose()`. E2E-SLH-014 / -015 and `SessionLiveHallAutoRefreshTests`. |
| 2026-08-18 | no D-id found | The roster moved onto the shared grid seam - `GET /admin/sessions/{id}/present` became `POST /admin/sessions/{id}/present/list` binding a `GridQuery`; the page now asks for `PresentPageSize = 200`, reads `Total` for the summary line and shows a `Grid.Summary` truncation hint when the room is larger than the page. E2E-SLH-016 to -019. Dated from the E2E catalogue's own review line. |
| 2026-08-19 | - | This reference doc authored. |

---

_Last reviewed:_ 2026-08-19 by Claude, from source only - no browser session, no
screenshots, no test run. Every endpoint, permission code, resx key, field name,
error code and CSS token above is quoted from a file read on that date. Where a
fact could not be verified it is marked "N/A" or "Unverified" rather than
inferred. If the page has changed and this doc has not been re-reviewed in 60
days, it is out of date - re-walk the page in a browser and update every section
that drifted.
