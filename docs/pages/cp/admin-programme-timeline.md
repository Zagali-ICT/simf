# Programme run of show - `/admin/programme/timeline`

| | |
|--|--|
| **Route** | `/admin/programme/timeline` |
| **Layout** | `CpShellLayout` (`@layout CpShellLayout`) |
| **Surface** | Control Panel |
| **Audience** | Administrator (see §2 - the seeded baseline grants this code to no other role) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.ProgrammeTimeline.View)]` on the page; the BFF group is `MapGroup("/account/api").RequireAuthorization()`; the API endpoint it forwards to demands `Sessions.View` + `RequireApprovedAccount` |
| **Pattern** | Bespoke read-only overview (D-204). **Not** a canonical CRUD list - it hosts no `SimfDataGrid`, and `docs/tests/element-sweeps/predicted-inventory.json` records it as `"kind": "bespoke"` with `"note": "no <SimfDataGrid> — needs a hand-authored expected inventory"` |
| **Status** | Real |
| **Implements use case(s)** | N/A - no use case in `SIMF-UCS-001` covers this page. The nearest entry, `UC-08 Browse the agenda and a session` (Visitor, FR-408 / FR-409), is the public agenda, not this admin overview |
| **Backend endpoints** | `POST /account/api/admin/sessions/list` (BFF) forwarding to `POST /api/v1/admin/sessions/list` (API). One call, on init. No other call, and no write call at all |
| **Source file** | [`ProgrammeTimeline.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ProgrammeTimeline.razor) + [`ProgrammeTimeline.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ProgrammeTimeline.razor.cs) |
| **Tests** | [`docs/tests/e2e/cp-admin-programme-timeline.md`](../../tests/e2e/cp-admin-programme-timeline.md) (E2E-PTL-001..011 + E2E-PTL-ELS-001/002). No unit or bUnit test names this page - a repository-wide search of `tests/` for `ProgrammeTimeline` and for the route string returned no match. It is covered only by the generic sweeps described in §11 |
| **Last reviewed** | `2026-08-19` |

---

## 1. Purpose

This page is the whole programme on one screen, read only. The editable grid
lives at `/admin/sessions`; this page exists so an administrator preparing for a
briefing can see the run of show as an agenda - day by day, in start-time order -
without paging a CRUD grid or risking an accidental edit. The page's own header
comment states the intent: "The editable grid lives at /admin/sessions
(SessionsList); this is the at-a-glance overview only - no create / edit /
delete." It was shipped by D-204 as one of two read-only Control Panel overview
pages built entirely on endpoints that already existed, so it added no backend,
no typed-client method, no BFF route and no CSS - only a `CpNavigation` entry and
the resx keys. An administrator walks in expecting to read, filter to a single
day, and leave.

## 2. Audience + permissions

- **Who can reach it:** any signed-in Control Panel principal holding the `perm`
  claim `ProgrammeTimeline.View`, or the Administrator wildcard `*`
  (`PermissionCatalog.Wildcard`, matched in
  `PermissionAuthorizationHandler.HandleRequirementAsync`).
- **Who can edit/write on it:** nobody. The page issues no POST, PUT or DELETE
  other than the read `list` call, and renders no mutation control.
- **Authorisation gates**, all three layers:

| Layer | Gate, quoted from source | File |
|-------|--------------------------|------|
| CP page | `@attribute [RequirePermission(PermissionCatalog.ProgrammeTimeline.View)]` | `ProgrammeTimeline.razor` line 9 |
| CP nav item | `new("Module.ProgrammeTimeline", "/admin/programme/timeline", RequiredPermission: PermissionCatalog.ProgrammeTimeline.View, Icon: "clock")` in the `Nav.Programme` group | `CpNavigation.cs` line 77 |
| BFF route | the whole group is `routes.MapGroup("/account/api").RequireAuthorization()`; the handler additionally does `var token = await http.GetTokenAsync("access_token"); if (token is null) return Results.Unauthorized();` | `AccountEndpoints.cs` line 33, `AccountEndpoints.Programme.cs` line 75 |
| API endpoint | `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Sessions.View), nameof(AuthorizationPolicies.RequireApprovedAccount));` | `ListSessionsEndpoint.Configure`, `SessionEndpoints.cs` |

- **The two codes are different, deliberately, and that asymmetry cuts both
  ways.** The page code is `ProgrammeTimeline.View`; the endpoint code is
  `Sessions.View`. Their seeded baseline roles differ too:

  ```csharp
  new(ProgrammeTimeline.View, "ProgrammeTimeline", "View", "View the programme timeline", AdminOnly),
  new(Sessions.View,          "Sessions",          "View", "View sessions",               ScientificCommittee),
  ```

  `AdminOnly` is `private static readonly IReadOnlyList<string> AdminOnly = [];` -
  an empty baseline list, so no built-in non-Administrator role is granted
  `ProgrammeTimeline.View` at seed time. `Sessions.View` is granted to
  `AppRoles.ScientificCommittee`. The consequence: the **seeded** Scientific
  Committee role can call the list endpoint but never sees this page or its nav
  item, and the opposite case the E2E catalogue exercises in E2E-PTL-009 (holds
  `ProgrammeTimeline.View`, lacks `Sessions.View`, so the page opens and the call
  403s) requires a hand-built custom role, not a seeded one. Both directions are
  reachable; neither is a defect in the gates, but a reviewer changing either
  code must change it knowing the other exists.

- **What an unauthenticated user sees:** `Routes.razor` branches inside
  `<NotAuthorized>` on `authenticationState.User.Identity?.IsAuthenticated`. Not
  authenticated renders `<RedirectToLogin />`. Authenticated but lacking the
  permission renders `<RedirectToNotPermitted />`, whose
  `OnInitialized` is `Nav.NavigateTo("/not-permitted")`. Separately, if the
  browser is authenticated to the CP but the API rejects the token, the JS helper
  handles it: `simfReadEnvelope` calls `window.location.assign('/login')` on HTTP
  401 and then awaits a never-resolving promise, so the page never acts on a bogus
  body and never shows a toast for a 401.

## 3. Screenshots

**No screenshots of this page exist in the repository.** Only two of the paths
below are named in the E2E catalogue - `-golden-before.png` and
`-golden-after.png`, under E2E-PTL-001's "Evidence captured" (catalogue lines
79-80). The other four follow the naming convention the catalogue sets for a
manual smoke run, `docs/screenshots/cp-admin-programme-timeline-{scenario}.png`
(catalogue line 248); they are the expected names, not paths anyone has written
down. The capture state for every row is "Not captured". Nothing here has been
photographed; do not read the rows as evidence.

| State | File | Captured |
|-------|------|----------|
| Default (with rows) | `docs/screenshots/cp-admin-programme-timeline-golden-after.png` | Not captured |
| Loading | `docs/screenshots/cp-admin-programme-timeline-golden-before.png` | Not captured |
| Empty state | `docs/screenshots/cp-admin-programme-timeline-empty.png` | Not captured |
| Day filter applied | `docs/screenshots/cp-admin-programme-timeline-filter.png` | Not captured |
| Error toast | `docs/screenshots/cp-admin-programme-timeline-error.png` | Not captured |
| RTL (Arabic) | `docs/screenshots/cp-admin-programme-timeline-rtl.png` | Not captured |

## 4. UI affordances

### 4.1 Banner / page header

`<SimfBanner Title="@L["Admin.ProgrammeTimeline.Title"]" Subtitle="@L["Admin.ProgrammeTimeline.Subtitle"]" />`.
`SimfBanner` renders the title as the page's `<h1>` (`simf-banner__title`) and the
subtitle as a `<p>`; its `Actions` slot is not used here, so the banner carries no
buttons. The resx values are:

| Key | English | Arabic |
|-----|---------|--------|
| `Admin.ProgrammeTimeline.Title` | `Programme run of show` | `جدول سير الفعاليات` |
| `Admin.ProgrammeTimeline.Subtitle` | `The full agenda on one screen, grouped by day.` | `كامل جدول الأعمال على شاشة واحدة، مجمّعًا حسب اليوم.` |

`<PageTitle>` is `@L["Admin.ProgrammeTimeline.Title"] · SIMF`. The nav label is a
separate key, `Module.ProgrammeTimeline` = `Run of Show` / `جدول الفعاليات`.

Body layout is `<div class="simf-page-wide"><div class="simf-surface">`, both
declared in `src/Shared/SIMF.Components/wwwroot/css/simf-components.css`.

### 4.2 Toolbar (CRUD pages only)

N/A - not a CRUD list page. There is no toolbar, no Add / Edit / Details /
Delete / Copy / Paste / Duplicate / Import / Export control, and no selection
model. What the page renders instead, in source order:

| Element | Component / markup | Behaviour |
|---------|--------------------|-----------|
| Error toast | `<SimfAlert Variant="@_toast.Variant">` | Rendered only when `_toast is not null`. Only ever set to `Variant = "error"` in `LoadAsync` |
| Loading line | `<p>@L["Admin.ProgrammeTimeline.Loading"]</p>` | Rendered while `_loading` |
| Empty state | `<SimfEmptyState Title="@L["Admin.ProgrammeTimeline.None"]" />` | Rendered when `_days.Count == 0`. Title only; no `Description`, no `Action` |
| Two stat tiles | `<SimfStatCard>` x2 inside `<div class="simf-form__actions">` | Days = `_days.Count`, Sessions = `_total`, both via `ToString(CultureInfo.InvariantCulture)` |
| Day filter | `<select class="simf-field__input" value="@_selectedDayKey" @onchange="OnDayChanged">` inside a wrapping `<label class="simf-field simf-field--inline">` | Client-side only. See §4.5 |
| Per-day section | `<h2>@day.Heading</h2>` + `<table class="simf-table">` + a `<p class="simf-text-muted">` count line | One per visible day |

### 4.3 Grid columns (CRUD pages only)

N/A - not a CRUD list page and not a `SimfDataGrid`. The page hand-writes a plain
`<table class="simf-table">` per day, with four fixed columns and no sorting,
filtering, paging or row actions on the table itself:

| Header (resx key) | English | Arabic | Cell expression | Source field |
|-------------------|---------|--------|-----------------|--------------|
| `Admin.ProgrammeTimeline.Col.Time` | `Time` | `الوقت` | `@TimeWindow(s)` | `AdminSessionSummary.Start` + `.End` |
| `Admin.ProgrammeTimeline.Col.Code` | `Code` | `الرمز` | `@s.Code` | `AdminSessionSummary.Code` |
| `Admin.ProgrammeTimeline.Col.Title` | `Session` | `الجلسة` | `@SessionTitle(s)` | `AdminSessionSummary.Title` / `.TitleArabic` |
| `Admin.ProgrammeTimeline.Col.Hall` | `Hall` | `القاعة` | `@HallLabel(s)` | `AdminSessionSummary.HallName` / `.HallNameArabic` |

Row helpers, quoted from the code-behind:

```csharp
private static string TimeWindow(AdminSessionSummary s) =>
    $"{s.Start:hh:mm tt} – {s.End:hh:mm tt}";

private static string SessionTitle(AdminSessionSummary s) =>
    IsArabic ? s.TitleArabic : s.Title;

private static string HallLabel(AdminSessionSummary s) =>
    IsArabic ? s.HallNameArabic : s.HallName;
```

`IsArabic` is `CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"`.

Grouping and ordering are done in `BuildDays`, and the code comment explains the
one non-obvious choice - why the times are rendered verbatim rather than
projected:

```csharp
// Group by the Saudi calendar day of the start time, days ascending,
// sessions within a day ascending by start time. Start is already the
// Saudi wall clock, so the run-of-show renders it verbatim — there
// is no projection and adding one would shift every row by three hours.
```

The LINQ is `.OrderBy(s => s.Start).GroupBy(s => s.Start.Date).OrderBy(g => g.Key)`,
producing a `DayGroup(Key, Heading, Sessions)` per day where `Key` is
`"yyyy-MM-dd"` under `CultureInfo.InvariantCulture` (a stable filter value) and
`Heading` is `"dddd, d MMMM yyyy"` under `CultureInfo.CurrentUICulture` (so the
heading localises while the filter key does not).

### 4.4 Pager

N/A - there is no pager. The page issues one request with a fixed
`new GridQuery { Top = 500 }` and renders everything that comes back. Note the
clamp described in §7: the server returns at most 200 rows regardless.

### 4.5 Form fields (if the page hosts a form or modal)

There is no form and no modal. The single interactive control is the day filter:

| Field | Type | Required | MaxLength | Validation | Locale |
|-------|------|----------|-----------|------------|--------|
| Day | `<select>` | no | N/A | none - the value is either `""` or a `DayGroup.Key` the page itself emitted | Label `Admin.ProgrammeTimeline.Filter.Day` = `Day` / `اليوم`; first option `Admin.ProgrammeTimeline.Filter.AllDays` = `All days` / `كل الأيام` |

`OnDayChanged` is `_selectedDayKey = e.Value?.ToString() ?? string.Empty;` and
`VisibleDays()` is `string.IsNullOrEmpty(_selectedDayKey) ? _days : _days.Where(d => d.Key == _selectedDayKey)`.
The filter fires no request - it re-renders from the list already in memory - and
it does not change the stat cards, which read `_days.Count` and `_total`.

## 5. Data flow

```
Page opened
  → ProgrammeTimeline.OnInitializedAsync → LoadAsync()
  → JS.InvokeAsync<ApiResult<GridPage<AdminSessionSummary>>>(
        "simfAccount.postJson", "/account/api/admin/sessions/list",
        new GridQuery { Top = 500 })
  → wwwroot/js/simf-account.js  window.simfAccount.postJson
        fetch(POST, credentials: 'same-origin', Content-Type: application/json)
  → BFF  AccountEndpoints.Programme.cs  group.MapPost("/admin/sessions/list", ...)
        GetTokenAsync("access_token") → SimfAdminClient.ListSessionsAsync(body, token)
        → Forward(...) = Results.Json(result.Body, statusCode: result.StatusCode)
  → API  ListSessionsEndpoint  POST /api/v1/admin/sessions/list
        (RoutePrefix "api/v1" is set in SIMF.Api/Program.cs)
  → IAdminSessionService.ListAllAsync → dbContext.Sessions.ToGridPageAsync(...)
  → ApiResult<GridPage<AdminSessionSummary>>.Ok(...)
  → back through the same chain → BuildDays(env.Data.Items) → render
     (or, on failure, _toast = new Toast("error", ...) and no tables)
```

Every backend call this page makes - there is exactly one:

| When | Method + path | Request body | Response shape |
|------|---------------|--------------|----------------|
| `OnInitializedAsync` | BFF `POST /account/api/admin/sessions/list`, forwarded to API `POST /api/v1/admin/sessions/list` | `GridQuery { Top = 500 }` (`Skip` 0, `Search`/`Sort` null, `Filters` empty - only `Top` is set) | `ApiResult<GridPage<AdminSessionSummary>>` |

The typed client hop is
`SimfAdminClient.ListSessionsAsync(GridQuery query, string accessToken, CancellationToken)`
in `src/Shared/SIMF.ApiClient/SimfAdminClient.Programme.cs`, which posts to the
relative path `"sessions/list"` and returns
`ApiCallResult<GridPage<AdminSessionSummary>>` so the BFF can forward the upstream
status verbatim.

`AdminSessionSummary` carries far more than this page renders (capacity, status,
type, live-stream fields, arrival grace and more, several of them present only so
the Sessions Excel lane can round-trip them). This page reads seven members: `Code`,
`Title`, `TitleArabic`, `HallName`, `HallNameArabic`, `Start` and `End`.

## 6. Validation + error handling

- **Client-side guards:** none, and none are needed - the page sends no user
  input to the server. The request body is a constant.
- **Server-side validation:** the grid pipeline validates the request, not a
  FluentValidation validator. `GridQueryExtensions.ToGridPageAsync` calls
  `ApplyGrid`, which resolves every sort and filter key against the resource's
  `GridColumns<Session>` declaration; the declaration's own comment says "A key
  not declared here is a 400, not a silently ignored request." This page sends no
  keys, so that path cannot trigger from here.
- **Error envelope:** standard `ApiResult<T>` with an `ApiError` carrying `Code`,
  `Message` and `MessageArabic`. `ApiError` exists in both languages because
  "Returning both languages on every error is a customer requirement".
- **Toast strategy**, quoted from `LoadAsync`:

  ```csharp
  if (env is { Success: true, Data: not null })
  {
      BuildDays(env.Data.Items);
  }
  else
  {
      _toast = new Toast("error",
          env?.Error?.MessageForCurrentCulture()
          ?? L["Admin.ProgrammeTimeline.LoadFailed"]);
  }
  ```

  So: when the response parses and carries an `Error`, the toast shows that
  error's message in the current culture (`MessageForCurrentCulture()` picks
  Arabic when `CurrentUICulture.TwoLetterISOLanguageName` is `ar`, English
  otherwise). When it does not - a null envelope, or a `Success: false` with no
  `Error`, or a `Data: null` - the toast falls back to
  `Admin.ProgrammeTimeline.LoadFailed` = `Could not load the programme. Please
  try again.` / `تعذّر تحميل البرنامج. يرجى المحاولة مرة أخرى.`

  Two special cases are handled below the page, in `simfReadEnvelope`:
  **HTTP 401** never reaches the toast - the helper navigates to `/login` and
  returns a promise that never resolves, so the page does not act on a stale
  session. A **non-JSON body** (a framework HTML error page) is converted into a
  synthetic envelope with `code: 'BAD_RESPONSE'` and a bilingual message, so the
  page shows a toast instead of throwing a `JSException` into the global Blazor
  error UI.

  There is exactly one toast variant on this page. `Toast` is only ever
  constructed with `"error"`, so `SimfAlert`'s `success` and `info` branches are
  unreachable here.

- **Unverified:** which of the two branches fires on a bare **403** from the API.
  Whether a FastEndpoints policy denial returns an `ApiResult` body with an
  `Error`, or an empty body that `simfReadEnvelope` turns into `null`, was not
  traced this session. The observable outcome is a red toast either way; only the
  wording differs.

## 7. Edge cases + known limitations

- **The page asks for 500 rows and the server returns at most 200.** The request
  is `new GridQuery { Top = 500 }`, but `AdminSessionService.Columns` ends with
  `.PageSize(fallback: 25, max: 200)`, and `ToGridPageAsync` applies
  `query.ClampPage(columns.FallbackTop, columns.MaxTop)`, whose implementation is
  `Math.Clamp(Top is > 0 ? Top : fallbackTop, 1, maxTop)`. A programme with more
  than 200 sessions therefore renders partially. It renders partially *silently*:
  `GridPage<T>` carries a `Total` ("The total row count across every page, after
  filters"), but the page never reads it - `BuildDays` sets `_total = items.Count`
  from the returned page. So the "Sessions" stat tile would also show 200, not the
  true count. Recorded as a limitation, not a bug report: nothing was changed here
  and the behaviour has not been observed against a >200-session dataset.
- **Deactivated sessions appear in the timeline.** `isActive` is a declared,
  filterable column on the sessions grid, but this page sends no `Filters`, and
  `ListAllAsync` applies no default `IsActive` predicate. Compare
  `/admin/halls`, whose hall-occupancy panel is served by
  `GetHallScheduleEndpoint` in `SIMF.Api/Endpoints/Admin/HallEndpoints.cs` - the
  pin is server-side, not in the panel, and it is deliberate for the opposite
  reason: "Occupancy means ACTIVE occupancy." Unverified whether the absence of
  that filter here is intentional for a run of show; the source simply does not
  filter.
- **The page loads once and never refreshes.** `LoadAsync` is called only from
  `OnInitializedAsync`; there is no timer, no refresh button and no re-fetch on
  the filter change. The Admin Manual documents the consequence in its
  troubleshooting table: "A session you just created is missing / The page loads
  once, on open / Reload the page."
- **JS interop in `OnInitializedAsync` is safe here because the Control Panel
  disables prerendering.** `App.razor` uses
  `AppRenderMode.InteractiveServerNoPrerender` for both `HeadOutlet` and
  `Routes`, so there is no server-side prerender pass in which `window.simfAccount`
  would be absent. D-204 notes that the sibling gates dashboard used
  `OnAfterRenderAsync(firstRender)` for that reason; this page does not, and does
  not need to.
- **`_loading` is set true but never reset to false except in `finally`, and the
  toast is never cleared.** `LoadAsync` has `finally { _loading = false; }`, so
  the loading line always clears. `_toast` has no dismiss control and no clearing
  path, so once an error is shown it stays for the life of the circuit unless the
  user navigates away or reloads.
- **The day filter cannot select a day that has no sessions.** Options are built
  from `_days`, which is built from the returned sessions, so an empty programme
  day simply does not exist as an option. The Admin Manual states the rest
  plainly under "What you cannot do here yet": "Filter by hall, theme or
  speaker. Only the day filter exists." (`Admin-Manual.md` line 1445). The page
  renders no status filter either, which is an observation from the markup - the
  manual's list does not mention status.
- **No print or export.** The Admin Manual's "What you cannot do here yet" says
  "Print or export. Use the browser's own print."
- **The permission asymmetry in §2 is a live edge case in both directions**, and
  E2E-PTL-009 covers one of them.

## 8. i18n + RTL

- Every visible string on the page comes from `IStringLocalizer<Strings> L`,
  injected in the code-behind. The page itself renders fourteen keys, all
  `Admin.ProgrammeTimeline.*`. The fifteenth, `Module.ProgrammeTimeline`, is not
  a string this page emits: it is the nav label the shell renders from the
  `CpNavigation` entry (`CpNavigation.cs` line 77). All fifteen are present in
  both `src/ControlPanel/SIMF.ControlPanel/Resources/Strings.resx` and
  `Strings.ar.resx`, and are listed with their values in §4.1, §4.3 and §4.5. No
  string is hard-coded in the markup.
- One string carries a placeholder and is formatted rather than emitted directly:
  `@(string.Format(L["Admin.ProgrammeTimeline.Day.Count"], day.Sessions.Count))`,
  where the value is `{0} session(s) on this day` / `{0} جلسة في هذا اليوم`.
- The two localisation switches in the code-behind are the row helpers
  `SessionTitle` and `HallLabel`, which pick the Arabic member when
  `CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"`. The day heading
  localises through `CultureInfo.CurrentUICulture` in `BuildDays`; the day filter
  key stays `CultureInfo.InvariantCulture`, which is what keeps the `<option>`
  value stable across a culture switch.
- The language toggle itself is not on this page - it is `SimfLanguageSwitch` in
  `CpShellLayout`'s `<Controls>` slot, so it is present on every signed-in CP page.
- RTL mirroring is inherited from the shell and the shared stylesheet; nothing on
  this page opts out or overrides direction. Not verified in a browser this
  session - E2E-PTL-011 is the scenario that would prove it.

## 9. Accessibility

Only what was read in source this session:

- **Headings.** The banner renders the page `<h1>` (`simf-banner__title`); each
  day section is an `<h2>`. The order is correct and there are no skipped levels.
  `Routes.razor` carries `<FocusOnNavigate RouteData="routeData" Selector="h1" />`,
  so focus lands on that `<h1>` after navigation.
- **The day filter is implicitly labelled.** The `<select>` is nested inside
  `<label class="simf-field simf-field--inline">` whose first child is
  `<span class="simf-field__label">@L["Admin.ProgrammeTimeline.Filter.Day"]</span>`,
  so the label is programmatically associated by containment. There is no `id`,
  `for` or `aria-label` on the control.
- **The error toast announces assertively.** `SimfAlert`'s `error` branch renders
  `<div class="simf-alert simf-alert--error" role="alert">`; the component's own
  comment explains the split - error is assertive, info and success are polite
  `role="status"` with `aria-live="polite"`.
- **The tables carry no `<caption>`** and no `scope` attributes on the `<th>`
  cells. Recorded as observed, not as a judgement: the header row is a plain
  `<thead><tr><th>` group.
- **Skip link and landmarks** come from `SimfAppShell` via `CpShellLayout`
  (`SkipNavLabel='@L["Shell.SkipToMain"]'`), not from this page.
- **Colour contrast and focus indicators: unverified.** No contrast measurement
  or live focus-ring check was run against this page this session, so no WCAG
  claim is made here. The page uses only shared classes
  (`simf-page-wide`, `simf-surface`, `simf-form__actions`, `simf-field`,
  `simf-field--inline`, `simf-field__input`, `simf-table`, `simf-text-muted`), all
  of which are defined in `src/Shared/SIMF.Components/wwwroot/css/simf-components.css`,
  so whatever the shared theme guarantees applies here unchanged.

## 10. Related use cases (UCS-001)

N/A - `SIMF-UCS-001-Use-Case-Specifications.md` contains no use case for this
page. The only agenda-related entry is listed below for orientation; it describes
the visitor-facing agenda, not this Control Panel overview.

| UC ID | Title | Notes |
|-------|-------|-------|
| UC-08 | Browse the agenda and a session | Actor **Visitor**, requirements FR-408 / FR-409. Related subject matter, different surface and audience. Not implemented by this page |

## 11. Related E2E test scenarios

All scenarios live in one file:
[`docs/tests/e2e/cp-admin-programme-timeline.md`](../../tests/e2e/cp-admin-programme-timeline.md),
indexed in [`docs/tests/e2e/README.md`](../../tests/e2e/README.md) as
`E2E-PTL-001..011`.

| Scenario | ID | Coverage |
|----------|----|----------|
| Golden path - stats, day sections and tables render from the live round-trip | E2E-PTL-001 | The one `POST /account/api/admin/sessions/list` with `{"Top":500}`, banner + subtitle text, both stat cards, the filter, per-day `<h2>` + table + count line |
| Stat cards reflect the data | E2E-PTL-002 | Days = distinct local days, Sessions = total item count |
| Day filter | E2E-PTL-003 | All days → one day → all days, asserting **no** new request fires and the stats do not change |
| Day grouping and ordering | E2E-PTL-004 | Days ascending, rows ascending by start, two halls in the same slot as separate rows |
| Time window and per-day count rendering | E2E-PTL-005 | The `TimeWindow` format and the `Day.Count` placeholder |
| Empty state | E2E-PTL-006 | Empty `Items` → `SimfEmptyState`, no stats, no filter, no toast |
| Auth gate | E2E-PTL-007 | Missing `ProgrammeTimeline.View` → `/not-permitted`, no list call, nav item hidden |
| Read-only guarantee | E2E-PTL-008 | No Add / Edit / Delete / row action / bulk action anywhere |
| Permission asymmetry | E2E-PTL-009 | Holds `ProgrammeTimeline.View`, lacks `Sessions.View` → page opens, list 403s, error toast |
| Server 500 on the list call | E2E-PTL-010 | Bilingual fallback toast, no tables, no unhandled JS exception |
| RTL / Arabic render | E2E-PTL-011 | `dir="rtl"`, Arabic banner / stats / filter / headers / day headings / count line, Arabic title and hall per row |
| Element inventory | E2E-PTL-ELS-001 | Every control present and accessibly named, in LTR and RTL, against `tools/qa/predicted_inventory.py` |
| Element health | E2E-PTL-ELS-002 | No dead control, no broken image, every same-origin asset < 400, zero console errors, `scrollWidth == clientWidth` |

**Two assertions in that catalogue have drifted from the source and are recorded
here rather than corrected, because the catalogue is outside this document's
scope.** Its "Last reviewed" is 2026-06-02.

1. E2E-PTL-005 asserts a **24-hour** time window - "Time window formats as
   HH:mm – HH:mm" and `Then the OPN-01 row Time cell reads "09:00 – 10:15"`
   (catalogue lines 131-135). The code renders **12-hour with a meridiem**:
   `$"{s.Start:hh:mm tt} – {s.End:hh:mm tt}"`. It is the only scenario that
   asserts the rendered format: E2E-PTL-001 asserts only that "each row shows
   the session's time window", and E2E-PTL-004 writes its fixture times in
   24-hour notation but asserts day and row ordering, not the format.
2. The catalogue's grounding note and E2E-PTL-005 describe a
   **`Start.LocalDateTime` projection**. There is no projection in the code. The
   comment in `BuildDays`, quoted verbatim in §4.3, says why one must not be
   added: `Start` is already the Saudi wall clock, so the run of show renders it
   as-is, and adding a projection would shift every row by three hours.

Both changes are consistent with D-770 (2026-07-25), which made times 12-hour
AM/PM across the Control Panel and renamed the App-domain `StartUtc` / `EndUtc`
properties to `Start` / `End` as Saudi-local values. That decision row does not
name this page, so the link is inference from the contract it changed, not a
recorded fact about this file.

**Lower-layer tests.** No unit or bUnit test targets this page - see the header
table. The generic Control Panel suites that do cover it are
`tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs` (every nav
`RequiredPermission` is a real catalogue code; every non-stub `/admin` nav item is
gated; the nav gate matches the gate on the page it links to) and
`tests/SIMF.ControlPanel.Tests/E2eCatalogueIntegrityTests.cs` (structural guards
on the catalogue file, including scenario-id uniqueness). On the API side the
`/admin/sessions/list` surface is exercised indirectly by
`tests/SIMF.Api.Tests/AdminSessionsTests.cs`; the E2E catalogue notes "There is no
dedicated list-endpoint test today".

## 12. Related docs

- Admin Manual: [`docs/manuals/Admin-Manual.md`](../../manuals/Admin-Manual.md)
  § 5A.3, the "Run of Show" section for this route, including its troubleshooting
  table and its "What you cannot do here yet" list.
- Page index: [`docs/pages/PAGE-INDEX.md`](../PAGE-INDEX.md). The row for this
  route is marked Real and its **Doc** column points at this file.
- E2E catalogue: [`docs/tests/e2e/cp-admin-programme-timeline.md`](../../tests/e2e/cp-admin-programme-timeline.md)
  and its index row in [`docs/tests/e2e/README.md`](../../tests/e2e/README.md).
- Permission catalogue: [`docs/SIMF-Permission-Catalogue.md`](../../SIMF-Permission-Catalogue.md),
  which lists `ProgrammeTimeline.View` against this route. Source of truth is
  [`PermissionCatalog.cs`](../../../src/Shared/SIMF.Common/PermissionCatalog.cs).
- The editable counterpart: [`docs/CP/admin-sessions/README.md`](../../CP/admin-sessions/README.md),
  which points back here ("A read-only run-of-show view of the same data lives at
  `/admin/programme/timeline`"). There is no per-page CP documentation set
  (`docs/CP/admin-programme-timeline/`) for this page.
- Component catalogue: [`SIMF-CMP-001`](../../SIMF-CMP-001-Component-Catalog.md) -
  the components used here are `SimfBanner`, `SimfAlert`, `SimfEmptyState` and
  `SimfStatCard`, plus the `CpShellLayout` / `SimfAppShell` chrome.
- Pattern doc: [`SIMF_TABLE_PATTERN.md`](../../dev/SIMF_TABLE_PATTERN.md) applies
  to CRUD grid pages; this page is deliberately outside it.
- Architecture: [`SIMF-SAD-001`](../../SIMF-SAD-001-Software-Architecture-Document.md).
  API contract: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) for the
  `ApiResult<T>` envelope and the error model.
- Decisions log: [`docs/decisions/DECISIONS_LOG.md`](../../decisions/DECISIONS_LOG.md)
  D-204 (shipped this page), D-770 (Saudi-local 12-hour render and the
  `StartUtc` → `Start` rename this page's time cells depend on), D-133 (page
  reference docs), D-245 (E2E catalogue).
- Source: [`ProgrammeTimeline.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ProgrammeTimeline.razor),
  [`ProgrammeTimeline.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ProgrammeTimeline.razor.cs),
  [`AccountEndpoints.Programme.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.Programme.cs),
  [`SessionEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SessionEndpoints.cs),
  [`AdminSessionService.cs`](../../../src/Backend/SIMF.Infrastructure/Programme/AdminSessionService.cs).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-31 | D-204 | Page shipped. A read-only run of show over the existing `POST /account/api/admin/sessions/list`, with zero new backend, typed-client, BFF or CSS. The only shared deltas were the `CpNavigation` entry in the `Nav.Programme` group and the resx keys (EN + AR, `{0}` placeholders preserved in the count string). Verified at the time as `dotnet build SIMF.ControlPanel -c Release` 0/0 and `SIMF.ControlPanel.Tests` 47/47 |
| 2026-07-25 | D-770 | Not a change to this file, but the reason its time cells read as they do: Saudi-local everywhere with 12-hour AM/PM display, and the App-domain rename of `StartUtc` / `EndUtc` to `Start` / `End` as Saudi wall-clock values. The decision row does not name this page |
| 2026-08-19 | - | This reference doc authored from source. No code changed |

---

_Last reviewed:_ `2026-08-19` by Claude (first authoring, from source). If the
page has changed and this doc has not been re-reviewed in 60 days, it is **out of
date**. Re-walk the page in a browser and update every section that drifted -
starting with §3, which has no captured evidence at all, and §9, whose contrast
and focus claims were deliberately left unmade.
