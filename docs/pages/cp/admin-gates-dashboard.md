# Gates operations dashboard - `/admin/gates/dashboard`

| | |
|--|--|
| **Route** | `/admin/gates/dashboard` |
| **Layout** | `CpShellLayout` (from the page's `@layout` directive) |
| **Surface** | Control Panel |
| **Audience** | Administrator (holds every code through the `"*"` wildcard, `PermissionCatalog.Wildcard`) and the `SecurityTeam` role, which is the baseline role seeded onto `Gates.Manage` |
| **Auth** | Page: `@attribute [RequirePermission(PermissionCatalog.Gates.Manage)]`. API: `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Manage), nameof(AuthorizationPolicies.RequireApprovedAccount))`. BFF: the whole `/account/api` group carries `.RequireAuthorization()`. |
| **Pattern** | Read-only overview page. **Not** a canonical CRUD list: no `SimfDataGrid`, no toolbar, no pager, no create / edit / delete. Two hand-written `<table class="simf-table">` blocks over two server-paged reads. |
| **Status** | Real (shipped by D-204, 2026-05-31) |
| **Implements use case(s)** | N/A - no use case in `docs/SIMF-UCS-001-Use-Case-Specifications.md` covers this page. See section 10 for what was searched. |
| **Backend endpoints** | BFF: `POST /account/api/admin/gates/reports/currently-inside/list`, `POST /account/api/admin/gates/list`. API: `POST /api/v1/admin/gates/reports/currently-inside/list`, `POST /api/v1/admin/gates/list`. |
| **Source file** | [`GatesOperationsDashboard.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/GatesOperationsDashboard.razor) + [`GatesOperationsDashboard.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/GatesOperationsDashboard.razor.cs) |
| **Tests** | [`docs/tests/e2e/cp-admin-gates-dashboard.md`](../../tests/e2e/cp-admin-gates-dashboard.md) (E2E-GDS-001..021 + two element scenarios); `tests/SIMF.Api.Tests/AdminGateCurrentlyInsideTests.cs`; `tests/SIMF.Api.Tests/AdminGatesTests.cs`; `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`; `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` |
| **Last reviewed** | 2026-08-19 |

---

## 1. Purpose

This page answers one question that nothing else in the Control Panel answers:
how many people are inside the venue right now, and which gates are up. Gate
configuration lives at `/admin/gates` and scanning lives at the operator console
`/admin/gates/operator`; neither shows the venue as a whole. So this page exists
as a summary surface over data those two already produce - it writes nothing.
An administrator or a member of the security team opens it expecting a headcount,
the roster behind that headcount (who came in, through which gate, and when), and
the gate roster with each gate's active state. The page does not poll: the numbers
are as fresh as the last load or the last **Refresh** click.

## 2. Audience + permissions

- **Who can reach it:** anyone whose role grants `PermissionCatalog.Gates.Manage`
  (`"Gates.Manage"`). Two roles do so out of the seeder: `Administrator`, which
  passes every check through the `"*"` wildcard, and `SecurityTeam`, the
  `BaselineRoles` entry on the catalogue row
  `new(Gates.Manage, "Gates", "Manage", "Manage gates", SecurityTeam)`.
- **Who can edit/write on it:** nobody. The page has no write path at all - the
  only interactive control is **Refresh**, which re-runs the two reads.
- **Authorisation gates**, all three layers, quoted from source:
  - CP page: `@attribute [RequirePermission(PermissionCatalog.Gates.Manage)]`.
    `RequirePermissionAttribute` is an `AuthorizeAttribute` whose `Policy` is
    `PermissionCatalog.PolicyFor(permissionCode)`, i.e. `perm:Gates.Manage`.
  - CP BFF: `routes.MapGroup("/account/api").RequireAuthorization()` in
    `AccountEndpoints.MapAccountEndpoints`. The two gate routes then read the
    access token out of the auth cookie (`http.GetTokenAsync("access_token")`)
    and return `Results.Unauthorized()` when there is none. The BFF does **not**
    re-check the permission itself; the upstream API does.
  - API endpoints, both of them:
    `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Manage), nameof(AuthorizationPolicies.RequireApprovedAccount))`.
    Note that the approval half is belt-and-braces: `PermissionPolicyProvider`
    builds every `perm:` policy with `.RequireClaim("account_state", "Approved")`
    already, its comment saying an admin endpoint "cannot be reached by a
    non-approved account even if it forgets to also chain RequireApprovedAccount".
- **Nav item:** `new("Module.GatesDashboard", "/admin/gates/dashboard", RequiredPermission: PermissionCatalog.Gates.Manage, Icon: "bar-chart")`,
  in the `Nav.Gates` group. (D-204 recorded it as going into `Nav.System`; the
  code puts it in `Nav.Gates` today. The code is the present state.)
- **What an unauthenticated user sees:** the `/account/api` group requires
  authorization, so an unauthenticated request never reaches the report. For a
  session whose cookie is present but whose upstream token is rejected, the
  browser helper handles it: `simfReadEnvelope` in
  `wwwroot/js/simf-account.js` checks `if (response.status === 401)` and calls
  `window.location.assign('/login')`, then returns a never-resolving promise so
  the page cannot act on a bogus body mid-navigation.
- **What a signed-in admin without `Gates.Manage` sees:** `/not-permitted`.
  `RedirectToNotPermitted` runs `Nav.NavigateTo("/not-permitted")` - an ordinary
  client-side navigation, deliberately not a `forceLoad`, because the circuit and
  the session are both healthy and a reload would read as "you were logged out".

## 3. Screenshots

**No screenshots of this page exist.** `docs/screenshots/` currently contains no
file matching `*gates-dashboard*`. The paths below are the names the E2E
catalogue expects a runner to capture; every row is uncaptured.

| State | File | Captured |
|-------|------|----------|
| Default (both rosters populated) | `docs/screenshots/cp-admin-gates-dashboard-golden.png` | Not captured |
| Loading (both calls in flight) | `docs/screenshots/cp-admin-gates-dashboard-loading.png` | Not captured |
| After Refresh | `docs/screenshots/cp-admin-gates-dashboard-refresh.png` | Not captured |
| Empty "Currently inside" roster | `docs/screenshots/cp-admin-gates-dashboard-inside-empty.png` | Not captured |
| Empty "Gates" roster | `docs/screenshots/cp-admin-gates-dashboard-gates-empty.png` | Not captured |
| Error alert (load failed) | `docs/screenshots/cp-admin-gates-dashboard-error.png` | Not captured |
| RTL (Arabic) | `docs/screenshots/cp-admin-gates-dashboard-rtl.png` | Not captured |

Add-modal / edit-modal / details-modal rows from the template do not apply: the
page has no modal.

## 4. UI affordances

### 4.1 Banner / page header

`<SimfBanner Title="@L["Admin.GatesDashboard.Title"]" />` - title only, no
`Subtitle` and no `Actions` slot. `SimfBanner` renders a `<section class="simf-banner">`
with the title as the page `<h1>`. The document title is
`<PageTitle>@L["Admin.GatesDashboard.Title"] · SIMF</PageTitle>`.

Everything below the banner sits in `<div class="simf-page-wide"><div class="simf-surface">`.
`.simf-surface` is documented in `simf-components.css` as the card with no width
cap, for pages that host a wide table.

**Above the content, conditionally:** `@if (_toast is not null)` renders
`<SimfAlert Variant="@_toast.Variant">@_toast.Message</SimfAlert>`. The page only
ever constructs `new Toast("error", ...)`, so in practice this is always the
error variant, which `SimfAlert` renders with `role="alert"`.

**Action row:** a single `SimfButton`:

| Control | Wired to | Parameters |
|---------|----------|------------|
| Refresh | `OnClick="LoadAsync"` | `Type="button"`, `Loading="_loading"`, `LoadingLabel="@L["Admin.GatesDashboard.Refreshing"]"`, label `@L["Admin.GatesDashboard.Refresh"]` |

While `Loading` is true, `SimfButton` replaces its label with
`<span class="simf-button__spinner" role="status" aria-label="@LoadingLabel">`,
sets `aria-busy="true"` and `disabled`, so a second refresh cannot be queued on
top of the first.

**Stat cards** (rendered only once `_loading` is false):

| Card | Title key | Value |
|------|-----------|-------|
| Currently inside | `Admin.GatesDashboard.Stat.Inside` | `_insideTotal.ToString(CultureInfo.InvariantCulture)` |
| Gates | `Admin.GatesDashboard.Stat.Gates` | `_gatesTotal.ToString(CultureInfo.InvariantCulture)` |

Both values come from the server's `GridPage<T>.Total`, never from
`Items.Count`. The page comment says so explicitly: "Both reports are
server-paged, so the figure is the server's Total for the whole set, never the
length of the page on screen."

**Loading placeholder:** while `_loading` is true the whole block below the
button collapses to `<p>@L["Admin.GatesDashboard.Loading"]</p>` - neither stat
card nor either table is rendered.

### 4.2 Toolbar (CRUD pages only)

N/A - not a CRUD list page. There is no toolbar, no Select all / Add / Edit /
Details / Delete / Copy / Paste / Duplicate / Import / Export. The single
`SimfButton` described in 4.1 is the whole action surface, and it is a re-read,
not a mutation. Gate CRUD is `/admin/gates`; check-in and check-out are
`/admin/gates/operator`.

### 4.3 Grid columns (CRUD pages only)

N/A - not a CRUD list page, and neither table is a `SimfDataGrid`. Both are
hand-written `<table class="simf-table">` blocks with fixed headers, no sort
affordance, no filter row, and no per-row actions. Recorded here because the
CP list-page standard would otherwise be read as violated: the standard governs
list pages with a CRUD surface, and this page has none.

**Table 1 - "Currently inside"** (heading `Admin.GatesDashboard.Inside.Title`),
one row per `AdminCurrentlyInsideRow` in `_inside`:

| Column header key | Rendered expression | Notes |
|-------------------|---------------------|-------|
| `Admin.GatesDashboard.Inside.Col.Name` | `@NameOf(row)` | Arabic UI picks `row.DisplayNameArabic`, falling back to `row.DisplayName` when blank; any other culture uses `row.DisplayName`. |
| `Admin.GatesDashboard.Inside.Col.ProfileType` | `row.ProfileTypeName`, or the literal `"—"` when null or whitespace | The em-dash here is a source literal in the `.razor`, not doc prose. |
| `Admin.GatesDashboard.Inside.Col.Gate` | `@row.LastCheckInGateCode` | The gate code, resolved server-side from `Gates`. |
| `Admin.GatesDashboard.Inside.Col.EnteredAt` | `@row.LastCheckInAt.FormatSaudi("dd-MM-yyyy hh:mm:ss tt")` | `SaudiTime.FormatSaudi` formats with `CultureInfo.InvariantCulture` and does **no** conversion - stored values are already Saudi wall-clock. |

Empty case: `<SimfEmptyState Title="@L["Admin.GatesDashboard.Inside.None"]" />`.
Non-empty case adds `<p class="simf-table__summary">` carrying
`string.Format(L["Admin.GatesDashboard.Inside.Summary"], _insideTotal)`.

**Table 2 - "Gates"** (heading `Admin.GatesDashboard.Gates.Title`), one row per
`AdminGateSummary` in `_gates`:

| Column header key | Rendered expression | Notes |
|-------------------|---------------------|-------|
| `Admin.GatesDashboard.Gates.Col.Code` | `@gate.Code` | |
| `Admin.GatesDashboard.Gates.Col.Name` | `@NameOf(gate)` | Arabic UI picks `gate.NameArabic` falling back to `gate.Name`. |
| `Admin.GatesDashboard.Gates.Col.Active` | `SimfPill` | `Variant="on"` + `Admin.GatesDashboard.Gates.Active.Yes` when `gate.IsActive`; `Variant="off"` + `...Active.No` otherwise. |

Empty case: `<SimfEmptyState Title="@L["Admin.GatesDashboard.Gates.None"]" />`.
Non-empty case adds the `simf-table__summary` line from
`Admin.GatesDashboard.Gates.Summary` with `_gatesTotal`.

`AdminGateSummary` also carries `DirectionMode`, `AllowedProfileTypeCount`,
`AssignedOperatorCount`, `CreatedAt`, `Description` and `DescriptionArabic`.
This page renders none of them; `/admin/gates` does.

### 4.4 Pager

N/A - there is no pager. The page-size decision is a constant in the
code-behind, `private const int PageSize = 200`, whose summary explains the
choice: "The dashboard is a read-only overview with no pager, so it shows the
most recent page and reports the true size of each set from the server's Total".
Both calls therefore send `new GridQuery { Top = PageSize }` with `Skip` left at
its default `0`, and no First / Prev / Next / Last, no page-size selector, and
no "Showing X-Y of Z" caption exist. The two summary lines are counts, not page
captions.

Consequence, stated because it is visible to a user: on a venue holding more
than 200 people inside, the stat card and the summary line still read the true
occupancy while the table shows 200 rows, and nothing on screen says the table
was truncated. See section 7.

### 4.5 Form fields (if the page hosts a form or modal)

N/A - the page hosts no form, no modal, and no input of any kind. There is
nothing to validate client-side.

## 5. Data flow

Three layers, in the SIMF BFF shape: the Blazor circuit never handles the access
token, the browser sends the auth cookie, and the CP forwards with the token.

```
First interactive render (OnAfterRenderAsync, firstRender only)
  or a click on Refresh
    -> LoadAsync()
    -> JS.InvokeAsync<ApiResult<GridPage<T>>>("simfAccount.postJson", url, new GridQuery { Top = 200 })
    -> browser fetch, credentials: 'same-origin'
    -> CP BFF  POST /account/api/admin/gates/reports/currently-inside/list
               (AccountEndpoints.Gates.cs -> MapGates)
               http.GetTokenAsync("access_token")  [401 -> Results.Unauthorized()]
    -> SimfAdminClient.ListCurrentlyInsideAsync(query, token)
               BasePath "api/v1/admin/" + "gates/reports/currently-inside/list"
    -> API      GateCurrentlyInsideEndpoint  (FastEndpoints)
    -> AdminGateService.ListCurrentlyInsideAsync
    -> SimfAppDbContext.GateScans (+ Gates, + UserProfiles)
       then IIdentityUserDirectory.GetDisplayNamesAsync against the Identity database
    -> ApiResult<GridPage<AdminCurrentlyInsideRow>>
    -> Forward(result) returns the upstream status and body verbatim
    -> _inside / _insideTotal assigned, or _toast set
    -> the same sequence again for /admin/gates/list
    -> StateHasChanged (explicit after the first-render load)
```

The two calls run **sequentially**, not in parallel: `LoadAsync` awaits the
occupancy call, handles its envelope, and only then issues the gates call.

Every backend call this page makes:

| When | Method + path (BFF) | Forwarded to (API) | Request body | Response shape |
|------|--------------------|--------------------|--------------|----------------|
| First interactive render, and every **Refresh** | `POST /account/api/admin/gates/reports/currently-inside/list` | `POST /api/v1/admin/gates/reports/currently-inside/list` | `GridQuery { Top = 200 }` | `ApiResult<GridPage<AdminCurrentlyInsideRow>>` |
| First interactive render, and every **Refresh** | `POST /account/api/admin/gates/list` | `POST /api/v1/admin/gates/list` | `GridQuery { Top = 200 }` | `ApiResult<GridPage<AdminGateSummary>>` |

**Why `OnAfterRenderAsync(firstRender)` and not `OnInitializedAsync`.** The
code-behind comment: "The simfAccount JS module is only available once the
interactive Blazor connection is up - running these calls in OnInitializedAsync
would throw on the SSR prerender pass and surface Blazor's unhandled-error
banner (same idiom as GateOperatorConsole)."

**What the occupancy report actually computes.** `AdminGateService` derives
"inside" as the latest allowed scan per visitor being a `CheckIn`, expressed as a
correlated `NOT EXISTS` with an `Id` tiebreak, and bounded by a rolling presence
window `StalePresenceWindow = TimeSpan.FromHours(16)`: a check-in older than that
with no later scan is treated as departed, because an in-only gate never emits a
`CheckOut`. That scope is composed onto the source **before** the grid, so the
grid's filters, ordering and page window apply to the occupancy set rather than
to the raw scan log.

**Declared grid contracts** (the keys each report accepts; anything else is a
400, not a silent ignore):

| Report | Sortable / filterable keys | Searchable | Natural order | Page size |
|--------|---------------------------|------------|---------------|-----------|
| `CurrentlyInsideColumns` | `gateId`, `scannedAt` | none | `scannedAt` descending | fallback 25, max 200 |
| `Columns` (gates list) | `code`, `name`, `nameArabic`, `directionMode`, `isActive` | `code`, `name`, `nameArabic` | `code` | fallback 25, max 200 |

The occupancy surface is narrow on purpose. Its declaration says so: the columns
are declared over `GateScan` "because that is what the query pages", and
everything the CP renders beyond the gate and the timestamp - the display name,
the Arabic name, the profile type - "is resolved AFTER the page, some of it out
of the other database. So the sortable and filterable surface is deliberately the
scan's own columns, and a key naming a resolved field is a 400 rather than a sort
that quietly does nothing."

This page sends neither a sort nor a filter nor a search, so it always receives
the natural order: newest check-in first.

## 6. Validation + error handling

- **Client-side guards:** none to speak of, because there is no input. The only
  guard is the re-entrancy one: `SimfButton` disables itself while
  `Loading="_loading"` is true, so **Refresh** cannot be double-fired.
- **Server-side validation:** the shared grid seam, not a FluentValidation
  validator. `GridQueryComposition` validates the `GridQuery` against the
  resource's declared `GridColumns<T>`; an undeclared key or an unparseable value
  is rejected with one of these codes from `src/Shared/SIMF.Common/ErrorCodes.cs`:

  | Constant | Value | Raised when |
  |----------|-------|-------------|
  | `ErrorCodes.GridSortKeyInvalid` | `GRID_SORT_KEY_INVALID` | `sort` names a column the resource does not declare |
  | `ErrorCodes.GridFilterKeyInvalid` | `GRID_FILTER_KEY_INVALID` | `filters` carries a key the resource does not declare |
  | `ErrorCodes.GridFilterValueInvalid` | `GRID_FILTER_VALUE_INVALID` | a declared key's value cannot be parsed (e.g. `gateId` that is not a Guid) |
  | `ErrorCodes.GridSearchNotSupported` | `GRID_SEARCH_NOT_SUPPORTED` | `search` is sent to a resource that declares no searchable column - which is exactly the occupancy report |

  None of these is reachable from this page's own UI, because the page sends a
  bare `{ Top = 200 }`. They are reachable by sending the query straight to the
  report endpoint, which is how E2E-GDS-014 drives them - at the HTTP layer,
  outside the browser runner, per the catalogue's own implementation note.
- **Error envelope:** the standard `ApiResult<T>` with `Error.Code` and
  bilingual `Message` / `MessageArabic`. The BFF's `Forward` returns
  `Results.Json(result.Body, statusCode: result.StatusCode)`, so the browser sees
  the upstream status and the upstream envelope unchanged.
- **How the page decides a call failed:** `if (insideEnv is { Success: true, Data: not null })`.
  Anything else - a false `Success`, a null `Data`, or a null envelope - takes the
  error branch.
- **Toast strategy:** one field, `Toast? _toast`, cleared at the top of every
  `LoadAsync`. On failure it is set to
  `new Toast("error", env?.Error?.MessageForCurrentCulture() ?? L["Admin.GatesDashboard.LoadFailed"])`,
  so the server's own bilingual message wins and the resx key is only the
  fallback. There is no success toast and no info toast - a successful load is
  reported by the numbers changing, not by a message.
- **Non-JSON responses** are already normalised before the page sees them:
  `simfReadEnvelope` turns a framework HTML error page into a synthetic envelope
  with `code: 'BAD_RESPONSE'`, so the page shows a toast instead of throwing a
  `JSException` into Blazor's global error UI.

## 7. Edge cases + known limitations

- **The headcount is the server's `Total`, not the row count.** Both stat cards
  read `insideEnv.Data.Total` / `gatesEnv.Data.Total`. On a set larger than one
  page the card and the table therefore disagree by design, and the card is the
  correct figure.
- **Presence is bounded at 16 hours.** `StalePresenceWindow = TimeSpan.FromHours(16)`.
  A visitor whose latest allowed scan is a check-in older than that is treated as
  departed and disappears from the roster, even though no check-out was ever
  recorded. Without this an in-only gate would count a visitor as inside forever.
- **A tie on `ScannedAt` still yields one row.** The `NOT EXISTS` predicate breaks
  ties on `Id` (`later.ScannedAt == s.ScannedAt && later.Id > s.Id`), which is
  what the previous `OrderByDescending(...).First()` gave for free. The grid's
  own tiebreak parameter is likewise required rather than optional, because a
  non-unique `ORDER BY` lets the same row appear on two pages while another
  appears on none.
- **The report returned HTTP 500 on every request until 2026-07-29 (D-794),** so
  this page never worked before then. The service built "latest allowed scan per
  visitor" as a filter over a `GroupBy` projection, which EF Core cannot
  translate - it throws `KeyNotFoundException('EmptyProjectionMember')` while
  building the SQL. Because that is a translation-time failure it was total and
  data-independent: an empty `GateScans` table 500s identically, which is why no
  seeding would have masked it. `AdminGateCurrentlyInsideTests` (5 cases) exists
  so it cannot come back.
- **An attendee with no account still appears, with a blank name.** The name
  lives in the Identity database and the profile in the App database, so the
  service resolves display names in a second round trip. Its comment: "Only the
  accounts are looked up, but EVERY profile stays in the map: an attendee with no
  account has no display name to fetch, and dropping them would take them off the
  occupancy roster while they are still inside." The Arabic name and profile type
  come off the profile row either way, so such a row renders with an empty Name
  cell in an English UI.
- **A scan whose profile row is missing is dropped from the page.** The
  projection is guarded by `.Where(scan => profiles.ContainsKey(scan.UserProfileId))`,
  so a scan pointing at a profile that no longer exists is silently omitted from
  `Items` while still being counted in `Total`. The two figures can therefore
  differ by that number.
- **Profile type may be blank.** `AdminCurrentlyInsideRow.ProfileTypeName` is
  nullable (`profile.ProfileType?.Name`), and the page renders the literal `"—"`
  in that case.
- **A second failing call overwrites the first call's message.** `_toast` is a
  single field written by both branches. If the occupancy call fails and the
  gates call fails too, only the gates message is shown. If the occupancy call
  fails and the gates call succeeds, the occupancy error stays on screen and the
  gates table still renders its rows, which is the behaviour E2E-GDS-008 and
  E2E-GDS-009 assert.
- **The page does not poll.** There is no timer. `LoadAsync` runs once on first
  interactive render and thereafter only on **Refresh**. A dashboard left open
  goes stale silently.
- **Truncation is not signposted.** With `PageSize = 200` and no pager, a venue
  holding more than 200 people renders 200 rows with no notice that the table was
  cut. The hall view form hosted from `/admin/halls` (`HallsViewDelete.razor`)
  solves the same problem for its session schedule, raising an info alert when
  `_scheduleTotal > _schedule.Count`; its source comment says why - "the endpoint
  caps the page, so say when the schedule is partial rather than letting a capped
  list read as the whole occupancy". This page has no equivalent.
- **The two stat cards ride a form-button row.** They are wrapped in
  `<div class="simf-form__actions">`, a flex row. `simf-components.css` records
  why that is wrong: `.simf-stat-grid` was added for the programme dashboard
  because "Dashboard tiles previously rode on `.simf-form__actions`, a flex row
  meant for form buttons, which gave them neither equal widths nor predictable
  wrapping." This page was not migrated onto `.simf-stat-grid`.
- **`.simf-table__summary` has no CSS rule.** A repo-wide search across every
  `.css` file returns no match for that class; it is used only by four `.razor`
  pages, this one among them. The two summary lines therefore render as default
  `<p>` text rather than as a styled table caption.
- **No export.** The scan report has an XLSX export
  (`POST /admin/gates/reports/scans.xlsx`, capped at `ScanExportRowCap = 10_000`),
  but nothing on this page calls it and the occupancy report has no export at all.

## 8. i18n + RTL

- Every visible string comes from `Strings.resx` (EN) + `Strings.ar.resx` (AR)
  through `IStringLocalizer<Strings> L`. The page hard-codes no user-facing text.
  The one literal on screen is the `"—"` placeholder for a missing profile type.
- The full key set, EN and AR, both verified present:

  | Key | EN | AR |
  |-----|----|----|
  | `Admin.GatesDashboard.Title` | Gates operations dashboard | لوحة عمليات البوابات |
  | `Admin.GatesDashboard.Refresh` | Refresh | تحديث |
  | `Admin.GatesDashboard.Refreshing` | Refreshing… | جارٍ التحديث… |
  | `Admin.GatesDashboard.Loading` | Loading… | جارٍ التحميل… |
  | `Admin.GatesDashboard.LoadFailed` | Could not load the gates dashboard. | تعذّر تحميل لوحة البوابات. |
  | `Admin.GatesDashboard.Stat.Inside` | Currently inside | داخل المعرض حاليًا |
  | `Admin.GatesDashboard.Stat.Gates` | Gates | البوابات |
  | `Admin.GatesDashboard.Inside.Title` | Currently inside | الموجودون بالداخل حاليًا |
  | `Admin.GatesDashboard.Inside.None` | No one is currently inside the venue. | لا يوجد أحد داخل المعرض حاليًا. |
  | `Admin.GatesDashboard.Inside.Col.Name` | Name | الاسم |
  | `Admin.GatesDashboard.Inside.Col.ProfileType` | Profile type | نوع الملف |
  | `Admin.GatesDashboard.Inside.Col.Gate` | Gate | البوابة |
  | `Admin.GatesDashboard.Inside.Col.EnteredAt` | Entered at | وقت الدخول |
  | `Admin.GatesDashboard.Inside.Summary` | `{0} currently inside` | `{0} داخل المعرض حاليًا` |
  | `Admin.GatesDashboard.Gates.Title` | Gates | البوابات |
  | `Admin.GatesDashboard.Gates.None` | No gates have been configured. | لم يتم إعداد أي بوابات. |
  | `Admin.GatesDashboard.Gates.Col.Code` | Code | الرمز |
  | `Admin.GatesDashboard.Gates.Col.Name` | Name | الاسم |
  | `Admin.GatesDashboard.Gates.Col.Active` | Active | نشطة |
  | `Admin.GatesDashboard.Gates.Active.Yes` | Active | نشطة |
  | `Admin.GatesDashboard.Gates.Active.No` | Inactive | غير نشطة |
  | `Admin.GatesDashboard.Gates.Summary` | `{0} gates` | `{0} بوابة` |

  The two `{0}` placeholders survive into both cultures, which matters because
  they are consumed by `string.Format`.
- **Data-side bilingualism.** `NameOf` is culture-aware in the code-behind, not
  the markup: for `CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"`
  it prefers `DisplayNameArabic` / `NameArabic` and falls back to the English
  field when the Arabic one is blank; any other culture takes the English field
  with no fallback.
- **RTL.** `App.razor` sets
  `<html lang="@CultureInfo.CurrentUICulture.TwoLetterISOLanguageName" dir="@(CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? "rtl" : "ltr")">`,
  so the whole document mirrors on the Arabic culture. The page adds no
  direction-specific markup of its own and relies entirely on that plus the
  shared component CSS.
- **Numbers stay Latin.** Both stat-card values are formatted with
  `CultureInfo.InvariantCulture`, so the digits do not switch with the UI
  culture. The counts inside the two summary lines go through `string.Format`
  with the ambient culture instead, so those two figures can render differently
  from the card that reports the same number. Recorded as observed, not judged.

## 9. Accessibility

- **Keyboard:** one focusable control on the page, the **Refresh** button. There
  is no modal, so there is no focus trap and nothing to restore focus to; ESC has
  no page-level handler. The two tables contain no interactive elements, so tab
  order is trivially the button then the page.
- **Screen reader:**
  - `SimfAlert` with `Variant="error"` renders `role="alert"`, which is announced
    assertively - the right choice for a load failure. Its info and success
    variants use `role="status"` + `aria-live="polite"`, but this page never
    constructs them.
  - `SimfButton` sets `aria-busy="true"` while loading and swaps its label for
    `<span class="simf-button__spinner" role="status" aria-label="@LoadingLabel">`,
    the label being the localised `Admin.GatesDashboard.Refreshing`.
  - `SimfBanner` renders the page title as `<h1>`; each roster is introduced by an
    `<h3>`. There is no `<h2>` between them, so the heading level jumps.
  - **Limitation:** the two tables are raw `<table class="simf-table">` markup
    with no `<caption>`, no `aria-label` and no `scope` on the header cells.
    `SimfDataGrid`, which this page does not use, is where the CP's grid
    accessibility affordances live. The `simf-table__summary` paragraph is not
    associated with either table programmatically.
  - The `"—"` placeholder for a missing profile type is read out as a dash rather
    than as "none".
- **Colour contrast:** inherited from `theme.tokens.css` through the shared
  component classes. The two pill variants (`on` / `off`) are the only
  colour-carrying status indicator, and each is paired with its own text label
  (`Active` / `Inactive`), so state is not conveyed by colour alone.
- **Focus indicators:** `.simf-button:focus-visible { outline: 0; box-shadow: var(--focus-ring); }`
  in `simf-components.css`, so the one focusable control carries the shared focus
  ring token.

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| N/A | N/A | No use case in `docs/SIMF-UCS-001-Use-Case-Specifications.md` covers this page. |

Searched: every occurrence of "gate" in `SIMF-UCS-001`. The hits are the seat
booking flow (UC-09 / UC-22, where a gate check-in confirms a provisional
reservation), the narrative step "the attendee checks in at the hall gate (a
staff QR scan, UC-35)", **UC-35 "Check an attendee in at a hall door"
(Staff / System, FR-305)**, one mention of "the venue gate", and one unrelated
use of "length gate". None of them is a read-only occupancy dashboard, and the
UCS has no gate-monitoring use case to point at. If one is authored later, add
its id here and to `PAGE-INDEX.md` in the same changeset.

## 11. Related E2E test scenarios

All scenarios live in one file:
[`docs/tests/e2e/cp-admin-gates-dashboard.md`](../../tests/e2e/cp-admin-gates-dashboard.md).
The catalogue uses `### E2E-GDS-nnn` headings rather than the template's slug
anchors, so scenarios are referenced by id.

| Scenario | Id | Coverage |
|----------|----|----------|
| Golden path - both tables + both stat cards render | E2E-GDS-001 | Both BFF calls fire and return 200; banner, cards, rows and both summary lines |
| Refresh re-fires both calls | E2E-GDS-002 | The loading label, two fresh 200s, and the occupancy count moving |
| Stat cards read the server `Total` | E2E-GDS-003 | 260 inside renders 200 rows and still reads 260 |
| Empty "Currently inside" roster | E2E-GDS-004 | `SimfEmptyState` + the bilingual copy |
| Empty "Gates" roster | E2E-GDS-005 | `SimfEmptyState` + the bilingual copy |
| Active / Inactive pill variant | E2E-GDS-006 | `SimfPill` `on` vs `off` |
| Auth gate (admin lacking `Gates.Manage`) | E2E-GDS-007 | Lands on `/not-permitted`, neither call fires |
| Server 500 on the occupancy call | E2E-GDS-008 | Fallback bilingual toast |
| Server 500 on the gates call | E2E-GDS-009 | Fallback bilingual toast, occupancy rows still rendered |
| Loading placeholder | E2E-GDS-010 | The `Loading…` paragraph while both calls are in flight |
| RTL / Arabic render | E2E-GDS-011 | `dir="rtl"`, every heading, column and pill in Arabic |
| Regression (D-794): the report is translatable | E2E-GDS-012 | 5 cases, automated by `tests/SIMF.Api.Tests/AdminGateCurrentlyInsideTests.cs`, recorded PASS 2026-07-29 |
| Occupancy report: one page + a server-side total | E2E-GDS-013 | API layer |
| Occupancy report: undeclared sort / search refused | E2E-GDS-014 | API layer; `GRID_SORT_KEY_INVALID`, `GRID_SEARCH_NOT_SUPPORTED` |
| Occupancy report: paging with a `scannedAt` tie | E2E-GDS-015 | API layer |
| Occupancy report: `gateId` filter and the filtered total | E2E-GDS-016 | API layer; `GRID_FILTER_VALUE_INVALID` for a non-Guid |
| Scan report: one page of a date window | E2E-GDS-017 | API layer, no CP page renders it |
| Scan report: `scannedTo` is half-open on the next midnight | E2E-GDS-018 | API layer |
| Scan report: undeclared keys refused | E2E-GDS-019 | API layer |
| Scan report: paging forward | E2E-GDS-020 | API layer |
| Scan XLSX export carries the grid's filters | E2E-GDS-021 | API layer |
| Element inventory (LTR + RTL) | E2E-GDS-ELS-001 | Element sweep |
| Element health (console, links, overflow) | E2E-GDS-ELS-002 | Element sweep |

Notes on the mapping:

- **E2E-GDS-013 to -021 are HTTP-level, not browser-level.** -013 to -016 cover
  the report this page renders; -017 to -021 cover the sibling scan report and
  its XLSX export, which share the `Gates.Manage` gate but have no Control Panel
  page of their own, so the catalogue hosts them here beside their sibling.
- The template's "validation failure on Add" and "conflict / duplicate" rows have
  no equivalent: the page has no write path. The catalogue says the same, and
  points at the `/admin/gates` catalogue for those.
- **One known drift in the catalogue, not corrected here.** E2E-GDS-001 asserts
  the Entered-at column is "formatted as `yyyy-MM-dd HH:mm:ss UTC`". The source
  renders `row.LastCheckInAt.FormatSaudi("dd-MM-yyyy hh:mm:ss tt")` - day first,
  12-hour, no UTC suffix, and no conversion, because stored values are already
  Saudi wall-clock. The source is the truth; the catalogue line is stale and
  should be fixed in its own changeset.

## 12. Related docs

- Manual chapter: `docs/manuals/Admin-Manual.md` § 9.4 "Gates operations
  dashboard - `/admin/gates/dashboard`" (most common tasks, troubleshooting
  table, and a "what you cannot do here yet" list).
- Sibling pages: [`admin-gates.md`](admin-gates.md) (gate CRUD, the page whose
  data this dashboard summarises) and `/admin/gates/operator`, the console where
  check-in and check-out actually happen. Its E2E catalogue is
  [`cp-admin-gates-operator.md`](../../tests/e2e/cp-admin-gates-operator.md).
- Page index: [`docs/pages/PAGE-INDEX.md`](../PAGE-INDEX.md), row
  `/admin/gates/dashboard`.
- Grid seam: [`docs/tests/e2e/cp-grid-contract.md`](../../tests/e2e/cp-grid-contract.md)
  is the cross-cutting contract for `POST {resource}/list`; E2E-GDS-014 and -019
  are the per-report half it cannot pin.
- Permission catalogue: [`docs/SIMF-Permission-Catalogue.md`](../../SIMF-Permission-Catalogue.md),
  the `Gates.Manage` row.
- Component catalogue: [`SIMF-CMP-001`](../../SIMF-CMP-001-Component-Catalog.md).
  Components used here: `SimfBanner`, `SimfAlert`, `SimfButton`, `SimfStatCard`,
  `SimfEmptyState`, `SimfPill`.
- Decisions: D-204 (built the page), D-794 (fixed the 500 that made it useless),
  D-148 / D-149 (the Gate Module the page reads from).
- Source: [`GatesOperationsDashboard.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/GatesOperationsDashboard.razor),
  [`GatesOperationsDashboard.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/GatesOperationsDashboard.razor.cs),
  [`AccountEndpoints.Gates.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.Gates.cs),
  [`SimfAdminClient.Gates.cs`](../../../src/Shared/SIMF.ApiClient/SimfAdminClient.Gates.cs),
  [`GateEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/GateEndpoints.cs),
  [`AdminGateService.cs`](../../../src/Backend/SIMF.Infrastructure/AccessControl/AdminGateService.cs).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-31 | D-204 | **Page shipped.** A read-only overview over endpoints that already existed - zero new backend, client, BFF or CSS. Two `CpNavigation` entries and 36 resx keys per culture were the only shared deltas across the two pages that landed together. Loads via `OnAfterRenderAsync(firstRender)` rather than `OnInitializedAsync`, because the `simfAccount` JS module does not exist during the SSR prerender pass. D-204 records the occupancy call as a `GET` and the nav item as landing in `Nav.System`; both have since changed, see the rows below and section 2. |
| 2026-07-29 | D-794 | **The occupancy report was fixed.** `GET /admin/gates/reports/currently-inside` had returned 500 on every request since it shipped, so this page had never worked. The "latest allowed scan per visitor" query filtered a `GroupBy` projection, which EF Core cannot translate; rewritten as a correlated `NOT EXISTS` with an `Id` tiebreak. `tests/SIMF.Api.Tests/AdminGateCurrentlyInsideTests.cs` (5 cases) added, whose first case asserts only HTTP 200 because that was the whole bug. |
| 2026-08-17 | commit `63294b9e3` (no decision id recorded) | **Both gate reports moved onto the shared grid seam.** The occupancy report had no `Skip` or `Take` at all: it read `GateScans`, the highest-write table in the system, materialised the whole 19-property scan entity to render three fields, and sorted in C#. It is now `POST {resource}/list` binding a `GridQuery`, with `CurrentlyInsideColumns` declaring `gateId` + `scannedAt`, natural order `scannedAt` descending, page size fallback 25 and cap 200. The page follows: it sends `{ Top = 200 }`, reads occupancy from `Data.Total`, and the BFF/API routes gain the `/list` suffix. The scan report and its XLSX export moved in the same commit, the export's cap dropping from 100,000 to `ScanExportRowCap = 10_000` and gaining filter/sort parity with the grid by construction. |
| 2026-08-19 | - | This reference doc authored from source. No live render was performed and no screenshot was captured. |
| 2026-08-19 | - | **Adversarial verification pass.** Every route, permission code, endpoint, column, button, error code, resx pair and cited decision re-checked against source. Two statements corrected: E2E-GDS-014 drives the report endpoint at the HTTP layer, not the BFF route (§6); and the info alert this page lacks belongs to the hall view form's session schedule, not to an occupancy view (§7). |

---

_Last reviewed:_ `2026-08-19` by Claude (authored from source: page, code-behind,
BFF passthrough, typed client, FastEndpoints, `AdminGateService`, both resx
files, the shared components and the E2E catalogue). If the page has changed and
this doc has not been re-reviewed in 60 days, it is **out of date**. Re-walk the
page in a browser and update every section that drifted - starting with section 3,
which has no captured evidence at all.
