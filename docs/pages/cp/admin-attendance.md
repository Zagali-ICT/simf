# Session attendance - `/admin/attendance`

| | |
|--|--|
| **Route** | `/admin/attendance` |
| **Layout** | `CpShellLayout` (`@layout CpShellLayout`) |
| **Surface** | Control Panel |
| **Audience** | Administrator (holds the `"*"` wildcard) and the seeded `SecurityTeam` role - `Attendance.View` carries `SecurityTeam` as its baseline grant in `PermissionCatalog.All` |
| **Auth** | Page: `@attribute [RequirePermission(PermissionCatalog.Attendance.View)]`. API: `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Attendance.View), nameof(AuthorizationPolicies.RequireApprovedAccount))` on both endpoints. BFF: the `/account/api` group is `.RequireAuthorization()` only. |
| **Pattern** | Read-only dashboard - three `SimfStatCard` tiles above a `SimfDataGrid` that wires no action callbacks. **Not** the canonical CRUD pattern; there is no toolbar, no selection and no row actions. |
| **Status** | Real (D-293, FR-506) |
| **Implements use case(s)** | N/A - no use case in `SIMF-UCS-001-Use-Case-Specifications.md` maps to FR-506. Grepped for `FR-506` and for attendance-shaped UC ids in that document; neither returned a match. UC-35 ("Check an attendee in at a hall door", FR-305) produces the data this page reads, but is a different surface. |
| **Backend endpoints** | BFF: `GET /account/api/admin/attendance/summary`, `POST /account/api/admin/attendance/sessions/list`. API: `GET /api/v1/admin/attendance/summary`, `POST /api/v1/admin/attendance/sessions/list` |
| **Source file** | [`AttendanceDashboard.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/AttendanceDashboard.razor) + [`AttendanceDashboard.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/AttendanceDashboard.razor.cs) |
| **Tests** | [`docs/tests/e2e/cp-admin-attendance.md`](../../tests/e2e/cp-admin-attendance.md) (E2E-ATND-001..014); `tests/SIMF.Api.Tests/SessionAttendanceTests.cs`; `tests/SIMF.Api.Tests/GridDateSortKeyTests.cs` |
| **Last reviewed** | 2026-08-19 |

---

## 1. Purpose

The event's arrival data already exists - every hall entry writes a `HallAttendance`
row - but before this page there was nowhere in the Control Panel to read it back.
This dashboard answers two operational questions at a glance: how many people are
inside a hall right now, and how many distinct people have arrived at each session.
It is deliberately a reporting surface and nothing else: the page comment states
"Every value is an on-demand aggregate over the existing HallAttendance arrival
records - no create / edit / delete, so the grid carries no row actions (filter +
sort + pager only)." An administrator or a member of the security team walks in
expecting to read counts, narrow the grid to a session they care about, and leave.
Nothing on the page writes, so opening it produces no `OperationLog` row.

## 2. Audience + permissions

- **Who can reach it:** any signed-in Control Panel account whose role grants
  `PermissionCatalog.Attendance.View` (`"Attendance.View"`). `PermissionCatalog.All`
  lists that entry as
  `new(Attendance.View, "Attendance", "View", "View the session-attendance dashboard", SecurityTeam)`,
  so the seeded `SecurityTeam` role holds it as a baseline grant. Administrator is
  never listed in a baseline list: `PermissionCatalog.Wildcard` (`"*"`) is resolved
  at token-mint time and satisfies every code.
- **Who can edit/write on it:** nobody. The page issues no `PUT`, `POST` (beyond
  the list query) or `DELETE`, and the `SimfDataGrid` is passed no `OnAdd`,
  `OnEditOne`, `OnDeleteOne`, `OnDeleteSelected`, `OnImport` or `OnExport`
  callback, so it renders no toolbar and no row-action cell.
- **Authorisation gates - all three layers:**
  - CP page: `@attribute [RequirePermission(PermissionCatalog.Attendance.View)]`.
    `RequirePermissionAttribute` is an `AuthorizeAttribute` whose `Policy` is
    `PermissionCatalog.PolicyFor(permissionCode)`.
  - BFF: `AccountEndpoints` maps the two routes into the `/account/api` group,
    which is created as `routes.MapGroup("/account/api").RequireAuthorization()`.
    The group requires a signed-in cookie; it does **not** re-check the permission.
    Each handler reads the access token with
    `await http.GetTokenAsync("access_token")` and returns `Results.Unauthorized()`
    when there is none.
  - API: both endpoints declare
    `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Attendance.View), nameof(AuthorizationPolicies.RequireApprovedAccount))`.
    This is the gate that actually enforces the permission on the data.
- **Nav visibility:** `CpNavigation.Groups` places
  `new("Module.Attendance", "/admin/attendance", RequiredPermission: PermissionCatalog.Attendance.View, Icon: "user-check")`
  in the `Nav.Overview` group, so a holder who lacks the code never sees the item.
- **What an unauthenticated user sees:** `Routes.razor` picks between
  `RedirectToLogin` and `RedirectToNotPermitted` on the authentication state (per
  the comment in `RedirectToNotPermitted.razor`). A signed-in admin who lacks the
  code is sent to `/not-permitted` by `Nav.NavigateTo("/not-permitted")`; the
  cookie handler's `options.AccessDeniedPath` is set to the same route in
  `Program.cs`. If a call reaches the BFF with a rejected session the API returns
  401 and `simf-account.js` runs `window.location.assign('/login')`.

## 3. Screenshots

No screenshots of this page exist in the repository - `docs/screenshots/` contains
no file matching `attend`. The table below is the intended capture set, not a
record of captures taken.

| State | File | Captured |
|-------|------|----------|
| Default (with rows) | `docs/screenshots/cp-admin-attendance-default.png` | Not captured |
| Empty state (no active sessions) | `docs/screenshots/cp-admin-attendance-empty.png` | Not captured |
| Error state (load failed) | `docs/screenshots/cp-admin-attendance-error.png` | Not captured |
| RTL (Arabic) | `docs/screenshots/cp-admin-attendance-rtl.png` | Not captured |

No Add / Edit / Details rows: the page hosts no modal.

## 4. UI affordances

### 4.1 Banner / page header

`<SimfBanner Title="@L["Admin.Attendance.Title"]" />` - title only. No `Subtitle`
and no `Actions` fragment are passed, so `SimfBanner` renders the `<h1>` and
nothing else. The browser tab is
`<PageTitle>@L["Admin.Attendance.Title"] · SIMF</PageTitle>`.

Below the banner, inside `div.simf-page-wide > div.simf-surface`, the page renders
in order:

1. `<SimfAlert Variant="@_toast.Variant">` when `_toast is not null`.
2. A `div.simf-form__actions` holding three `SimfStatCard` tiles, rendered only
   when `_summary is not null`. The razor comment says this is the "same stat-card
   layout as the Statistics dashboard".
3. The `SimfDataGrid`.

The three tiles, with their source fields and resx keys:

| Tile | `SessionAttendanceSummary` field | resx key | EN | AR |
|------|----------------------------------|----------|----|----|
| 1 | `LiveAttendeesNow` | `Admin.Attendance.Stat.LiveNow` | `Live attendees now` | `الحاضرون الآن` |
| 2 | `SessionsWithAttendance` | `Admin.Attendance.Stat.SessionsWithAttendance` | `Sessions with attendance` | `جلسات بها حضور` |
| 3 | `TotalArrivals` | `Admin.Attendance.Stat.TotalArrivals` | `Total arrivals` | `إجمالي الوصول` |

Each value goes through `private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);`,
so the digits are Latin in both languages. No `Href` or `Delta` is passed, so the
tiles are static `div.simf-stat` elements, not links.

### 4.2 Toolbar (CRUD pages only)

N/A - not a CRUD list page. The page passes `SimfDataGrid` only `TItem`, `Query`,
`Page`, `Loading`, `OnQueryChanged`, `Caption`, the pager/filter labels and the two
formatters. `SimfDataGrid.HasToolbar` is false unless `Multiselect` is set or at
least one action callback is wired, and none are, so no toolbar element is
rendered at all. `HasRowEndCell` is false for the same reason, so there is no
Actions column. There is therefore no `AddPermission` / `EditPermission` /
`DeletePermission` / `ImportPermission` / `ExportPermission` to set: those
parameters gate buttons this page never asks the grid to render.

### 4.3 Grid columns

Six columns, declared in this order. "Sortable" and "Filterable" below are the
`SimfDataGridColumn` parameters as written on the page, which is what the user can
click and type into.

| Column (key) | Source field | Sortable | Filterable | Notes |
|--------------|--------------|----------|------------|-------|
| Code (`code`) | `context.Code` | yes | yes | resx `Admin.Attendance.Col.Code` / `Code` / `الرمز` |
| Session (`title`) | `context.Title` | yes | yes | resx `Admin.Attendance.Col.Session` / `Session` / `الجلسة`. Binds the **English** title even in Arabic; see section 8. |
| Hall (`hall`) | `context.HallName` | no | no | resx `Admin.Attendance.Col.Hall` / `Hall` / `القاعة`. `hall` is not a declared server-side key, so it could not be sorted or filtered even if the header offered it. |
| Start (`start`) | `context.Start.FormatSaudi("dd-MM-yyyy hh:mm tt")` | yes | no | resx `Admin.Attendance.Col.Start` / `Start (Saudi time)` / `البداية (بتوقيت السعودية)`. `FormatSaudi` is the `SIMF.Common.SaudiTime` extension. |
| Total attendees (`total`) | `context.TotalAttendees` | no | no | resx `Admin.Attendance.Col.TotalAttendees` / `Total attendees` / `إجمالي الحضور` |
| Live now (`live`) | `context.LiveNow` | no | no | resx `Admin.Attendance.Col.LiveNow` / `Live now` / `الآن`. Renders `<SimfPill Variant="on">` when `context.LiveNow > 0`, otherwise a plain `<span>0</span>`. |

`SessionAttendanceRow` also carries `SessionId`, `TitleArabic`, `HallNameArabic`
and `End`, which no column renders.

The server's own column contract is `GridColumns<Session>` in
`SessionAttendanceService`, and it declares four keys - `code` (searchable),
`title` (searchable), `titleArabic` (searchable) and `start` - with
`.DefaultOrder("start")` and `.PageSize(fallback: 20, max: 200)`. Every key added
through `GridColumns.Add` gets both an `OrderBy` and a `Filter`, so all four are
sortable and filterable at the API even though the page exposes fewer. The service
comment explains why `total` and `live` are absent: "The two counter columns
(total, live) are computed per page after the rows are chosen, so they are neither
sortable nor filterable - the grid does not offer them as either."

Empty body: `<EmptyTemplate><SimfEmptyState Title="@L["Admin.Attendance.None"]" /></EmptyTemplate>`,
which reads `No attendance has been recorded yet.` / `لم يُسجَّل أي حضور بعد.`

### 4.4 Pager

Rendered by `SimfDataGrid`, which this page does not customise beyond labels:

- First / Prev / a numbered window / Next / Last. `PageNumbersToShow()` uses
  `const int window = 5`, centred on the current page and clamped to
  `[1..TotalPages]`.
- Page-size selector from the grid's default `PageSizeOptions` = `10, 20, 50, 100`.
  The page does not override it. The initial size is `_query = new() { Top = 20, Sort = "start" }`.
- Summary caption via `FormatSummary(skip, taken, total) => string.Format(L["Grid.Summary"], skip + 1, skip + taken, total)`
  - `Grid.Summary` is `Showing {0}–{1} of {2}` / `عرض {0}–{1} من {2}`.
- Page caption via `FormatPage(current, total) => string.Format(L["Grid.Page"], current, total)`
  - `Grid.Page` is `Page {0} of {1}` / `صفحة {0} من {1}`.
- Other labels passed through: `Grid.Prev`, `Grid.Next`, `Grid.First`
  (`First page`), `Grid.Last` (`Last page`), `Grid.PageSize` (`Show`),
  `Grid.FilterColumn` (`Filter column`), `Grid.FilterPlaceholder` (`Search`), and
  `LoadingLabel="@L["Admin.Attendance.Loading"]"` (`Loading attendance…`).

Every pager control and the page-size `<select>` are disabled while `Loading` is
true.

### 4.5 Form fields

N/A - the page hosts no form and no modal. The only inputs are the two per-column
filter `<input type="search">` elements the grid renders for `code` and `title`,
which are debounced 300 ms in `SimfDataGrid.OnFilterInputAsync` and reset
`Skip` to 0.

## 5. Data flow

```
OnInitializedAsync  -> _loading = true
                    -> LoadSummaryAsync()  -> JS simfAccount.getJson
                       -> GET  /account/api/admin/attendance/summary        (BFF, AccountEndpoints.MapFeedbackAndReports)
                       -> SimfAdminClient.GetSessionAttendanceSummaryAsync  ("attendance/summary")
                       -> GET  /api/v1/admin/attendance/summary             (GetSessionAttendanceSummaryEndpoint)
                       -> ISessionAttendanceService.GetSummaryAsync         (SessionAttendanceService, SimfAppDbContext)
                       -> ApiResult<SessionAttendanceSummary>               -> _summary -> 3 SimfStatCard tiles
                    -> LoadAsync()         -> JS simfAccount.postJson
                       -> POST /account/api/admin/attendance/sessions/list  (BFF)
                       -> SimfAdminClient.ListSessionAttendanceAsync        ("attendance/sessions/list")
                       -> POST /api/v1/admin/attendance/sessions/list       (ListSessionAttendanceEndpoint)
                       -> ISessionAttendanceService.ListSessionAttendanceAsync
                       -> ApiResult<GridPage<SessionAttendanceRow>>         -> _page -> SimfDataGrid

sort / filter / page / page-size -> SimfDataGrid.OnQueryChanged
                    -> OnQueryChangedAsync(next) -> _query = next -> LoadAsync()   (list only)
```

The `/api/v1` prefix comes from `config.Endpoints.RoutePrefix = "api/v1";` in
`src/Backend/SIMF.Api/Program.cs`; the endpoint classes themselves declare
`Get("/admin/attendance/summary")` and `Post("/admin/attendance/sessions/list")`.

Two things about the sequence are worth stating because they are easy to assume
otherwise. The two calls run **sequentially** - `OnInitializedAsync` awaits
`LoadSummaryAsync()` and then `LoadAsync()`. And `OnQueryChangedAsync` calls
`LoadAsync` only, so **the tiles never refresh** when the user sorts, filters or
pages; they hold the values fetched at page load until the page is reloaded.

| When | Method + path | Request body | Response shape |
|------|---------------|--------------|----------------|
| `OnInitializedAsync` | `GET /account/api/admin/attendance/summary` -> `GET /api/v1/admin/attendance/summary` | none | `ApiResult<SessionAttendanceSummary>` |
| `OnInitializedAsync`, then every sort / filter / page / page-size change | `POST /account/api/admin/attendance/sessions/list` -> `POST /api/v1/admin/attendance/sessions/list` | `GridQuery` (initially `{ Top = 20, Sort = "start" }`) | `ApiResult<GridPage<SessionAttendanceRow>>` |

How the numbers are computed, from `SessionAttendanceService`:

- `LiveAttendeesNow` - `HallAttendances.Where(a => a.Leave == null).Select(a => a.UserProfileId).Distinct().Count()`.
- `SessionsWithAttendance` - distinct `SessionId` over attendance rows whose
  session is active.
- `TotalArrivals` - distinct `(SessionId, UserProfileId)` pairs over active
  sessions.
- Per row, `TotalAttendees` - the page's session ids are grouped by
  `(SessionId, UserProfileId)` and counted per session, so a person who left and
  re-entered counts once. The comment records why the shape is what it is:
  "Kept to the page's ids so it never scans the whole table. (COUNT(DISTINCT) is
  intentionally avoided for portable EF SQL.)"
- Per row, `LiveNow` - open rows (`Leave == null`) grouped by `SessionId`.

Every query is `AsNoTracking` and nothing is written. The attendee is counted as
an opaque profile Guid and is never resolved against the Identity database, so
there is no cross-database join.

## 6. Validation + error handling

- **Client-side guards:** none in the usual sense - there is no form to validate.
  Both handlers accept the response only on `env is { Success: true, Data: not null }`
  and treat everything else as an error. `_page` is initialised to `new GridPage<SessionAttendanceRow>()`
  and `_summary` to `null`, so a failed call leaves the previous state rather than
  a null render.
- **Server-side validation:** neither endpoint declares a FluentValidation
  validator; `GetSessionAttendanceSummaryEndpoint` takes no request at all and
  `ListSessionAttendanceEndpoint` takes a `GridQuery`. The request is validated
  structurally by `GridQueryComposition.ApplyGrid` and `GridColumns`, which throw
  `ApiException` with HTTP 400:
  - more than 20 filter keys -> `ErrorCodes.ValidationFailed`;
  - the same filter column sent twice, or a filter key that is not a declared
    filterable column -> `ErrorCodes.GridFilterKeyInvalid`;
  - a sort key that is not a declared sortable column -> `ErrorCodes.GridSortKeyInvalid`.
    The message names the accepted keys.
  The page can only send `code`, `title` and `start`, all declared, so these 400s
  are reachable from a hand-crafted request rather than from the UI.
- **Error envelope:** the standard `ApiResult<T>` with `Error.Code` from
  `ErrorCodes` plus bilingual `Message` / `MessageArabic`. The page renders
  `env?.Error?.MessageForCurrentCulture()` and falls back to
  `L["Admin.Attendance.LoadFailed"]` when there is no envelope or no error object.
- **Non-2xx behaviour is envelope-shaped, not exception-shaped.** `simfAccount.getJson`
  and `postJson` both funnel through `simfReadEnvelope`, which does not check
  `response.ok`. A 500 carrying an `ApiResult` body parses into the envelope with
  `success: false`; a framework HTML error page is converted to a synthetic
  `{ code: 'BAD_RESPONSE', ... }` envelope, which the helper's own comment says
  exists "so the calling page shows a toast instead of throwing a JSException that
  trips the global Blazor error UI"; an empty body returns `null`, which the
  null-conditional chain turns into the `Admin.Attendance.LoadFailed` fallback.
  HTTP 401 is special-cased before any of that: `window.location.assign('/login')`
  followed by a promise that never resolves.
- **Toast strategy:** one `private record Toast(string Variant, string Message)`
  field, rendered as `<SimfAlert Variant="@_toast.Variant">`. Only `"error"` is
  ever constructed - a read-only page has no success message. The two resx keys
  involved are `Admin.Attendance.LoadFailed`
  (`Could not load attendance. Please try again.` / `تعذّر تحميل الحضور. حاول مرة أخرى.`)
  and, for the empty grid, `Admin.Attendance.None`.

## 7. Edge cases + known limitations

- **A re-entry counts once.** `TotalAttendees` groups on
  `new { attendance.SessionId, attendance.UserProfileId }` before counting, so a
  closed row plus a new open row for the same person is one attendee. `LiveNow`
  counts open rows only, so the same person is also counted in the live figure.
- **Active sessions only, and a request cannot widen it.** The list starts from
  `appDbContext.Sessions.Where(session => session.IsActive)` before
  `ToGridPageAsync`. The comment states the reason: "That is the resource's own
  scope, not one of the grid's filters, so it composes ahead of them and no
  request can widen it."
- **A session with no arrivals shows 0 / 0.** The per-session counts come from
  `totalBySession.GetValueOrDefault(session.Id)` and
  `liveBySession.GetValueOrDefault(session.Id)`, so a session missing from either
  dictionary yields 0 rather than being dropped from the grid. With
  `LiveNow == 0` the cell renders plain `0`, not a pill.
- **The tiles can be missing while the grid renders.** The tile block is inside
  `@if (_summary is not null)`. If the summary call fails and the list call
  succeeds, the page shows the error alert and the grid with no tiles above it.
- **One alert slot, two calls.** `_toast` is a single field written by both
  handlers. If both calls fail, `LoadAsync` runs second and its message replaces
  the summary's, so only one message is visible.
- **`LoadAsync` has no `catch`.** `LoadSummaryAsync` wraps its interop call in
  `try { ... } catch { _toast = new Toast("error", L["Admin.Attendance.LoadFailed"]); }`,
  but `LoadAsync` uses `try { ... } finally { _loading = false; }` with no catch
  clause. A JSException raised by the list interop call itself is therefore not
  converted into the fallback alert the way the summary's is. Envelope-shaped
  failures - which is what a 500 or a non-JSON error page produces, per section 6
  - are handled identically on both paths.
- **The spinner is armed before the first await.** `OnInitializedAsync` sets
  `_loading = true` before calling `LoadSummaryAsync`. The comment explains why:
  "OnInitializedAsync first yields at the summary fetch below, so without this the
  in-progress frame would render with `_loading == false` and the spinner would
  never show on first load."
- **Page size is capped at 200.** `.PageSize(fallback: 20, max: 200)` on the
  service's `GridColumns<Session>`. The pager offers 10 / 20 / 50 / 100, so the
  cap is only reachable by a hand-crafted request.
- **`titleArabic` is searchable at the API but is not a column.** The service
  comment says why it is declared: "Not a column on the page. Declared because the
  free-text search covered the Arabic title before this list moved onto the shared
  grid." The page never sets `GridQuery.Search`, so the free-text path is unused
  from this UI.
- **Every session has a hall.** The projection uses `session.Hall!.Name`; the
  null-forgiving operator is safe because `Session.HallId` is a non-nullable
  `Guid`.
- **No auto-refresh.** The page fetches on initialise and on grid interaction
  only. There is no timer, no SignalR push and no manual refresh button, so a
  "live" figure is as fresh as the last load. E2E-ATND-014 reflects this: it
  reloads the page to see a new arrival.
- **No export.** The eight `/admin/reports/*` pages each wire `OnExport` and
  produce XLSX; this one wires no `OnExport`, so there is no spreadsheet of these
  counts. Not every read-only surface exports, though - the sibling
  `/admin/statistics` dashboard wires no `OnExport` either. The
  separate `/admin/reports/attendance` page (permission `Reports.Attendance`) is
  the exportable report.

## 8. i18n + RTL

- Every visible string on the page comes from `Strings.resx` (EN) /
  `Strings.ar.resx` (AR) through `[Inject] private IStringLocalizer<Strings> L`.
  The page's own keys are `Admin.Attendance.Title`, `.Loading`, `.None`,
  `.LoadFailed`, the three `.Stat.*` keys and the six `.Col.*` keys; the grid
  chrome uses the shared `Grid.*` keys. All of them exist in both files (verified
  by reading both resx files).
- `Admin.Attendance.Title` is `Session attendance` / `حضور الجلسات`, so the tab
  title becomes `حضور الجلسات · SIMF` in Arabic.
- **Two data columns do not localise.** The `Session` cell binds `context.Title`
  and the `Hall` cell binds `context.HallName`, both of which are the English
  values, even though `SessionAttendanceRow` also carries `TitleArabic` and
  `HallNameArabic` and the API already returns them. An Arabic user therefore sees
  Arabic headers over English session and hall names. The data to fix it is
  already on the wire.
- Tile values are formatted with `CultureInfo.InvariantCulture`, so counts render
  as Latin digits in Arabic.
- Dates use `FormatSaudi("dd-MM-yyyy hh:mm tt")` - Saudi local time, 12-hour, per
  the column header `Start (Saudi time)` / `البداية (بتوقيت السعودية)`.
- RTL is a shell concern rather than a page concern: this page sets no `dir` of
  its own. `SimfDataGrid` does carry one RTL-specific fix, in
  `OnRowContextMenuAsync`, but the context menu never opens here because no
  context-menu callback is wired.

## 9. Accessibility

Everything below is read from `SimfDataGrid.razor`, `SimfAlert.razor` and the
page itself.

- **Keyboard:** the sortable headers are real `<button type="button">` elements,
  so Code / Session / Start are reachable and activatable by keyboard. The two
  filter inputs are `<input type="search">`. The pager's numbered pages are
  `<button>`s and the page-size control is a `<select>` inside a `<label>`. There
  is no modal on this page, so there is no focus trap, no ESC handling and no
  focus restoration to describe.
- **Screen reader:** the page passes `Caption="@L["Admin.Attendance.Title"]"`, which
  the grid renders as `<caption class="simf-visually-hidden">` - the parameter's
  own summary calls it an "optional visually-hidden caption announced by screen
  readers when the table receives focus". Every `<th>` carries
  `aria-sort` (`ascending` / `descending` / `none`). Each filter input's
  `aria-label` is `$"{FilterColumnLabel} {column.Header}"`, which resolves to
  "Filter column Code" and "Filter column Session". The current pager button
  carries `aria-current="page"`. The loading overlay is `aria-busy="true"` and
  wraps a `SimfSpinner` labelled `Loading attendance…`. The error alert renders as
  `<div class="simf-alert simf-alert--error" role="alert">`, so a load failure is
  announced assertively.
- **Not announced:** the three `SimfStatCard` tiles are plain `<p>` elements
  inside a `<div>` with no landmark, heading or live region, so a screen-reader
  user meets them as loose text between the banner and the table.
- **Colour contrast:** Unverified - no contrast measurement was taken for this
  page and the token values were not checked this session.
- **Focus indicators:** Unverified - not inspected this session.

## 10. Related use cases (UCS-001)

N/A - `SIMF-UCS-001-Use-Case-Specifications.md` contains no use case for FR-506.
Grepping that document for `FR-506` returns nothing, and its use-case tables
(sections 4.1 to 4.5) contain no attendance-dashboard entry.

For context only, two neighbouring use cases in that document touch the same data
without describing this page:

| UC ID | Title | Notes |
|-------|-------|-------|
| UC-35 | Check an attendee in at a hall door (Staff / System, FR-305) | Writes the `HallAttendance` rows this page aggregates. Different surface. |
| UC-30 | View the statistics dashboard (organising teams, FR-1101 to FR-1103) | The sibling read-only dashboard at `/admin/statistics`, a different route and a different permission (`Statistics.View`). |

## 11. Related E2E test scenarios

Full catalogue: [`docs/tests/e2e/cp-admin-attendance.md`](../../tests/e2e/cp-admin-attendance.md).
Scenario ids were renamespaced `ATT` -> `ATND` on 2026-07-28 because they collided
with `cp-admin-attendees.md`; that file records the collision and the fix.

| Scenario | File | Coverage |
|----------|------|----------|
| E2E-ATND-001 golden path - load renders 3 tiles + the grid | [`cp-admin-attendance.md`](../../tests/e2e/cp-admin-attendance.md) | Both calls fire, banner + tab title, one row asserted end to end. Backed by `SessionAttendanceTests.List_returns_distinct_attendee_and_live_now_per_session`. |
| E2E-ATND-002 re-entry dedupe; live counts open rows only | same | Backed by the same integration test. |
| E2E-ATND-003 session with no arrivals shows 0 / 0 | same | Backed by `SessionAttendanceTests.List_empty_session_has_zero_counts`. |
| E2E-ATND-004 top-line summary aggregates | same | Backed by `SessionAttendanceTests.Summary_lower_bounds_reflect_seeded_arrivals`. |
| E2E-ATND-005 empty grid renders `SimfEmptyState` | same | Covers `Admin.Attendance.None`. Marked _to author_. |
| E2E-ATND-006 auth gate at the API (403 on both) | same | Backed by `SessionAttendanceTests.Summary_is_forbidden_for_a_non_admin` and `List_is_forbidden_for_a_non_admin`. |
| E2E-ATND-007 auth gate in the CP (`/not-permitted` + nav hidden) | same | Marked _to author_. |
| E2E-ATND-008 per-column filter narrows the grid | same | `Filters["code"]` / `Filters["title"]`, `Skip` reset to 0. Marked _to author_. |
| E2E-ATND-009 column sort toggles | same | Backed by `GridDateSortKeyTests.Attendance_sorts_on_start_honours_the_descending_direction`; the catalogue notes Start must be clicked twice because ascending is the default. |
| E2E-ATND-010 live-now pill only when people are inside | same | Marked _to author_. |
| E2E-ATND-011 server 500 -> red `SimfAlert` | same | Marked _to author_. See section 6 for why this does not crash the circuit. |
| E2E-ATND-012 read-only surface - no write controls | same | Marked _to author_. |
| E2E-ATND-013 RTL / Arabic render | same | Marked _to author_. Note the untranslated Title / HallName cells recorded in section 8. |
| E2E-ATND-014 counts reflect live state after a reload | same | Marked _to author_. |
| E2E-ATND-ELS-001 / -002 element inventory + element health | same | Marked _to author_. |

Lower-layer test files: `tests/SIMF.Api.Tests/SessionAttendanceTests.cs` (10
`[Fact]` methods, of which five cover this page - the three count facts and the
two 403 facts; the other five cover the live-hall endpoints named in section 12)
and
`tests/SIMF.Api.Tests/GridDateSortKeyTests.cs`. The endpoint file
`SessionAttendanceEndpoints.cs` carries a `// Tests:` header naming
`SIMF.Api.Tests/SessionAttendanceTests.cs` only; the file naming both test files
is the service, `SessionAttendanceService.cs`. The BFF file
`AccountEndpoints.FeedbackAndReports.cs` carries no `// Tests:` header.

## 12. Related docs

- Admin Manual: [`docs/manuals/Admin-Manual.md`](../../manuals/Admin-Manual.md)
  § 3A.2 "Session attendance - `/admin/attendance`".
- Grid pattern: [`SIMF-Grid-Lists-Dev-Guide.md`](../../manuals/SIMF-Grid-Lists-Dev-Guide.md)
  and [`SIMF_TABLE_PATTERN.md`](../../dev/SIMF_TABLE_PATTERN.md).
- Permissions: [`SIMF-Auth-Permissions-Dev-Guide.md`](../../manuals/SIMF-Auth-Permissions-Dev-Guide.md)
  and [`SIMF-Permission-Catalogue.md`](../../SIMF-Permission-Catalogue.md).
- Architecture: [`SIMF-SAD-001`](../../SIMF-SAD-001-Software-Architecture-Document.md).
- API contract: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) - the
  `ApiResult<T>` envelope and the error model this page consumes.
- Requirement: FR-506 in
  [`SIMF-SRS-001`](../../SIMF-SRS-001-Software-Requirements-Specification.md)
  ("The system shall track session attendance from the hall-arrival ..."), listed
  as feeding the statistics in
  [`SIMF-FDS-005-Bookings-and-Attendance.md`](../../SIMF-FDS-005-Bookings-and-Attendance.md)
  and in
  [`SIMF-FDS-003-Badge-and-Access-Control.md`](../../SIMF-FDS-003-Badge-and-Access-Control.md).
- Component catalogue: [`SIMF-CMP-001`](../../SIMF-CMP-001-Component-Catalog.md).
  Components used here: `SimfBanner`, `SimfAlert`, `SimfStatCard`, `SimfDataGrid`,
  `SimfDataGridColumn`, `SimfPill`, `SimfEmptyState` (plus `SimfSpinner` and
  `SimfToolbarButton`, rendered by the grid).
- Page index row: [`docs/pages/PAGE-INDEX.md`](../PAGE-INDEX.md).
- Decisions: `docs/decisions/DECISIONS_LOG.md` D-293 (built) and D-752 (baseline
  role grant).
- Sibling pages: `/admin/sessions/live-hall` (`Module.SessionLiveHall`) carries the
  **same** `Attendance.View` permission and is served by the other two endpoints in
  `SessionAttendanceEndpoints.cs` -
  `POST /api/v1/admin/sessions/{sessionId:guid}/present/list` and
  `GET /api/v1/admin/sessions/{sessionId:guid}/seat-map`.
  `/admin/reports/attendance` is the separate exportable report, gated by
  `Reports.Attendance`.
- No 4-aspect per-page documentation set exists for this route: there is no
  `docs/CP/admin-attendance/` directory.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-05 | D-293 | **Built (FR-506).** New contracts `SessionAttendanceSummary` + `SessionAttendanceRow`, `ISessionAttendanceService` and its `AsNoTracking` aggregate implementation, the two API endpoints, the BFF passthroughs and `SimfAdminClient` methods, `AttendanceDashboard.razor` with three `SimfStatCard` tiles over a read-only `SimfDataGrid`, the `Module.Attendance` nav item, and EN + AR resx. New permission `Attendance.View`, seeded with no migration because the `Permission` / `RolePermission` tables pre-exist. No schema, no migration, no writes. The decision records that a **new** code was minted rather than reusing `HallArrivals.View`, which gates the operator scan console, because that "keeps the reporting page a distinct, separately-grantable action". D-293 shipped the code with an `AdminOnly` baseline. |
| 2026-07-20 | D-752 | **Baseline grant moved to `SecurityTeam`.** `Attendance.View` was one of the eight codes given to the new first-class `SecurityTeam` role, alongside the gate triad, `Gates.Export` / `Gates.Import` and `HallArrivals.View` / `HallArrivals.Record`. The current `PermissionCatalog.All` entry reads `SecurityTeam`, not `AdminOnly`. No schema or migration change - role rows and grants are seeded data. |

Not attributable this session: the field names in D-293's text (`UserId`,
`LeaveUtc`, `startUtc`) differ from the current source (`UserProfileId`, `Leave`,
`start`). Those renames landed in later work whose decision entries were not read
here, so no id is cited for them; this document describes the names as they stand
in the source today.

---

_Last reviewed:_ 2026-08-19 by Claude (first authoring of this per-page reference
doc; the route previously carried a dash placeholder in the doc column of
`PAGE-INDEX.md`, which D-293 recorded as a deliberate match to the sibling
read-only Statistics dashboard - that row still needs updating to point here).
If the page has changed and this doc has not been re-reviewed in 60
days, it is **out of date**. Re-walk the page in a browser and update every
section that drifted.
