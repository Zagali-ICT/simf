# Statistics dashboard - `/admin/statistics`

| | |
|--|--|
| **Route** | `/admin/statistics` |
| **Layout** | `CpShellLayout` (`@layout CpShellLayout` on the page; `CpShellLayout` itself declares `@layout MainLayout`) |
| **Surface** | Control Panel |
| **Audience** | Any signed-in Control Panel account whose role grants `Statistics.View`, plus the Administrator wildcard `*` |
| **Auth** | Auth cookie (CP) -> `@attribute [RequirePermission(PermissionCatalog.Statistics.View)]`; the API leg adds `RequireApprovedAccount` and a JWT bearer |
| **Pattern** | Read-only aggregate dashboard. **Not** a CRUD list page - no `SimfDataGrid`, no toolbar, no modals, no writes. |
| **Status** | Real |
| **Implements use case(s)** | UC-30 "View the statistics dashboard" (`SIMF-UCS-001`, mapped there to FR-1101-FR-1103) |
| **Backend endpoints** | `GET /account/api/admin/statistics` (CP BFF) -> `GET /api/v1/admin/statistics` (API). One call, on init. |
| **Source file** | [`StatisticsDashboard.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/StatisticsDashboard.razor) + [`StatisticsDashboard.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/StatisticsDashboard.razor.cs); tiles in [`StatisticsCards.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/StatisticsCards.razor) |
| **Tests** | [`docs/tests/e2e/cp-admin-statistics.md`](../../tests/e2e/cp-admin-statistics.md). No direct unit/integration test - see §11. |
| **Last reviewed** | 2026-08-19 |

---

## 1. Purpose

The page answers one question for an organiser: what does the platform hold
right now? It is a read-only overview of the headline event counts - how many
attendees exist, how many are approved, how many are still waiting for a
decision, and how much programme and exhibition content has been entered - so
an administrator can read the current state at a glance without opening eleven
module pages. Every figure is computed on demand when the page loads. There is
no stored aggregate table: `SIMF-FDS-011` Amendment B (version 1.2, 2026-08-18)
records that the specified `StatisticSnapshot` entity was **PROPOSED, NOT
BUILT**, and that "every figure the dashboard shows is computed on read from the
owning contexts". The page therefore never shows a stale number, and it also
never shows a historical one - there is no trend, no date range and no export
here. Each tile is a link into the module that owns the number, so the dashboard
is a jumping-off point as well as a readout.

## 2. Audience + permissions

- **Who can reach it:** any authenticated Control Panel principal holding a
  `perm` claim equal to `"Statistics.View"` or to the Administrator wildcard.
  `PermissionCatalog.Statistics.View` is declared as
  `public const string View = "Statistics.View";` and registered in
  `PermissionCatalog.All` as
  `new(Statistics.View, "Statistics", "View", "View the statistics dashboard", AdminOnly)`
  (`src/Shared/SIMF.Common/PermissionCatalog.cs`). Baseline role assignment is
  `AdminOnly`.
- **Who can edit/write on it:** nobody. The page issues no `POST` / `PUT` /
  `DELETE` and has no write endpoint behind it.
- **Authorisation gates - all three quoted from source:**
  - **CP page:** `@attribute [RequirePermission(PermissionCatalog.Statistics.View)]`
    (`StatisticsDashboard.razor` line 8). `RequirePermissionAttribute` is a thin
    `AuthorizeAttribute` whose `Policy` is `PermissionCatalog.PolicyFor(code)`
    (`src/ControlPanel/SIMF.ControlPanel/Authorization/PermissionAuthorization.cs`).
  - **CP BFF route:** the `/account/api` group is built as
    `routes.MapGroup("/account/api").RequireAuthorization()`
    (`AccountEndpoints.cs` line 33). The group requires authentication only; the
    permission itself is enforced at the API.
  - **API endpoint:** `GetStatisticsDashboardEndpoint.Configure()` declares
    `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Statistics.View), nameof(AuthorizationPolicies.RequireApprovedAccount))`
    (`src/Backend/SIMF.Api/Endpoints/Statistics/StatisticsEndpoints.cs`).
  - **Side nav:** the same code gates the menu row -
    `new("Module.Statistics", "/admin/statistics", RequiredPermission: PermissionCatalog.Statistics.View, Icon: "bar-chart")`
    (`CpNavigation.cs` line 49). The source comment beside it is worth keeping:
    the icon is `"bar-chart"`, **not** `"chart-bar"` - `SimfIcon` throws on an
    unknown name and this nav renders on every page, so the transposed name once
    broke 92 of 97 pages. `CpNavigationIconTests` pins every nav icon to the set
    `SimfIcon` knows.
- **What an unauthenticated user sees:** `AuthorizeRouteView` renders its
  `<NotAuthorized>` fragment for both the unauthenticated and the
  authenticated-but-forbidden case, and `Routes.razor` branches between them on
  `authenticationState.User.Identity?.IsAuthenticated`:
  - not signed in -> `<RedirectToLogin />` -> `Nav.NavigateTo("/login", forceLoad: true)`.
  - signed in without the permission -> `<RedirectToNotPermitted />` ->
    `Nav.NavigateTo("/not-permitted")`. The source comment records why the branch
    exists: with no branch, every permission denial on the gated CP pages
    force-reloaded a signed-in admin onto `/login`, which reads as "your session
    expired" rather than "you may not open this page".
  - A separate cookie-level path also exists: `Program.cs` line 77 sets
    `options.AccessDeniedPath = "/not-permitted"`.

## 3. Screenshots

**No screenshots have been captured for this page.** The table below records the
intended file paths only; the Captured column is honest about the state.

| State | File | Captured |
|-------|------|----------|
| Default (11 tiles, live counts) | `docs/screenshots/cp-admin-statistics-default.png` | Not captured |
| Loading | `docs/screenshots/cp-admin-statistics-loading.png` | Not captured |
| Load failure (alert + empty state) | `docs/screenshots/cp-admin-statistics-error.png` | Not captured |
| RTL (Arabic) | `docs/screenshots/cp-admin-statistics-rtl.png` | Not captured |
| Add modal | N/A - the page has no modals | Not captured |
| Edit modal | N/A - the page has no modals | Not captured |
| Details modal | N/A - the page has no modals | Not captured |

## 4. UI affordances

### 4.1 Banner / page header

- `<PageTitle>@L["Admin.Statistics.Title"] · SIMF</PageTitle>` - the browser tab
  reads `Statistics · SIMF` in English and `الإحصائيات · SIMF` in Arabic.
- `<SimfBanner Title="@L["Admin.Statistics.Title"]" />` - title only. No
  `Subtitle` and no `Actions` fragment is passed, so the banner renders a single
  `<h1 class="simf-banner__title">` inside `<section class="simf-banner">`.
- The body is `<div class="simf-page-wide"><div class="simf-surface">`, and the
  surface holds exactly three mutually-arranged pieces: an optional `SimfAlert`,
  and then one of the loading paragraph / `SimfEmptyState` / `StatisticsCards`.

**The tiles are links, so "no actions" needs qualifying.** Every card passes an
`Href`, and `SimfStatCard` renders `<a href="@Href" class="simf-stat simf-stat--clickable">`
when `Href is not null`. The page performs no write, but it does carry eleven
navigation anchors.

### 4.2 Toolbar (CRUD pages only)

N/A - not a CRUD list page. There is no toolbar, no Select all, no
Add / Edit / Details / Delete, no Copy / Paste / Duplicate, and no
Import / Export. `E2E-STA-011` exists to assert exactly this.

### 4.3 Grid columns (CRUD pages only)

N/A - not a CRUD list page; the page renders no `SimfDataGrid`. What it renders
instead is the stat-card set below.

**The eleven tiles, in render order** (`StatisticsCards.razor`). Every count is
`value.ToString(CultureInfo.InvariantCulture)` via the component's private
`Count` helper; `AverageRating` is
`Dashboard.AverageRating.ToString("0.0", CultureInfo.InvariantCulture)`.

| # | Contract field | Title key (`Admin.Statistics.Stat.*`) | EN | AR | `Href` | Backend source (`StatisticsService.GetDashboardAsync`) |
|---|----------------|----------------------------------------|----|----|--------|----------------------------------------------------------|
| 1 | `TotalAttendees` | `TotalAttendees` | Total attendees | إجمالي الحضور | `/admin/attendees` | `UserProfiles` where `IsActive && (ProfileType == null \|\| ProfileType.IsForVisitor)` |
| 2 | `ApprovedAttendees` | `ApprovedAttendees` | Approved attendees | الحضور المعتمدون | `/admin/attendees` | the same set, `+ AdmissionState == AccountState.Approved` |
| 3 | `PendingApprovals` | `PendingApprovals` | Pending approvals | الموافقات المعلقة | `/admin/visitors/pending` | the same set, `+ AdmissionState == AccountState.PendingApproval` |
| 4 | `Sessions` | `Sessions` | Sessions | الجلسات | `/admin/sessions` | `Sessions` where `IsActive` |
| 5 | `Speakers` | `Speakers` | Speakers | المتحدثون | `/admin/speakers` | `Speakers` where `IsActive` |
| 6 | `Booths` | `Booths` | Booths | الأجنحة | `/admin/booths` | `Booths` where `IsActive` |
| 7 | `Sponsors` | `Sponsors` | Sponsors | الرعاة | `/admin/sponsors` | `Sponsors` where `IsActive` |
| 8 | `NewsArticles` | `NewsArticles` | News articles | الأخبار | `/admin/news` | `News` where `IsActive` |
| 9 | `MediaItems` | `MediaItems` | Media items | عناصر الوسائط | `/admin/media-library` | `MediaItems` where `IsActive` |
| 10 | `RatingsCount` | `RatingsCount` | Total ratings | إجمالي التقييمات | `/admin/ratings` | `RatingResponses` where `IsActive` |
| 11 | `AverageRating` | `AverageRating` | Average rating | متوسط التقييم | `/admin/ratings` | `AVG` over `(double?)OverallStars` of active responses that carry one, `?? 0` |

The `StatisticsDashboard` contract record
(`src/Shared/SIMF.Contracts/Statistics/StatisticsContracts.cs`) has exactly
these eleven fields, so the tile set and the contract are 1:1.

**Why attendees come from profiles and not from Identity accounts.** The service
comment states it directly: "Counting Identity users would miss every attendee
who has no account - a walk-in registration or a pre-generated badge - which is
a large share of a real event, and would read approval from a row that no longer
decides it." Approval is read from the profile's own `AdmissionState`, "which is
what decides entry". This follows the commit that made the profile the attendee
record (`7be67b274`, 2026-08-13, citing D-877 and the D-881 freeze lift), whose
message records the concrete failure it fixed: "Statistics counted Identity
users for the headline and profiles for the breakdown in the same response, so a
walk-in day could report more people present than registered."

**Why a profile with no type still counts.** `p.ProfileType == null || p.ProfileType.IsForVisitor` -
the service comment explains that `IsForVisitor` itself defaults to true, so
requiring a non-null type "would quietly undercount everyone not yet
categorised", and `ExhibitorVisitorService` already reads an absent type the
same way.

**Why each metric is its own query.** The class comment: "Each metric is its own
COUNT / AVG query so one expensive or empty aggregate never affects the others,
and every query is `AsNoTracking` (these are pure reads - nothing is materialised
into the change tracker)."

**Why the nullable cast on the average.** `Select(r => (double?)r.OverallStars!.Value).AverageAsync(...) ?? 0` -
"The nullable cast makes an empty set return null (not throw on `AverageAsync`),
folded to 0."

**Layout note.** `StatisticsCards.razor` wraps the tiles in
`<div class="simf-form__actions">`, and its own comment says why: "there is no
dedicated stat-grid class in the component CSS." That comment is now **stale**.
`.simf-stat-grid` was added to `simf-components.css` (line 3051) precisely for
KPI tiles, and its comment records the defect: "Dashboard tiles previously rode
on `.simf-form__actions`, a flex row meant for form buttons, which gave them
neither equal widths nor predictable wrapping." The CP landing page (`Home.razor`)
uses `.simf-stat-grid`; this page still uses `.simf-form__actions`. Recorded here
as an observed inconsistency, not fixed by this doc.

### 4.4 Pager

N/A - the page renders a fixed set of eleven tiles from a single flat record.
There is no collection, so no pager, no page-size selector and no
"Showing X-Y of Z" caption.

### 4.5 Form fields

N/A - the page hosts no form, no modal and no input control of any kind. The
only interactive elements it renders are the eleven tile anchors.

## 5. Data flow

```
Page OnInitializedAsync -> LoadAsync()
  -> JS.InvokeAsync<ApiResult<StatisticsDashboard>>("simfAccount.getJson",
                                                    "/account/api/admin/statistics")
  -> browser fetch(url, { credentials: 'same-origin' }) -> simfReadEnvelope(response)
  -> CP BFF  GET /account/api/admin/statistics   (AccountEndpoints.FeedbackAndReports.cs:205,
                                                  group MapGroup("/account/api").RequireAuthorization())
       http.GetTokenAsync("access_token"); null -> Results.Unauthorized()
  -> SimfAdminClient.GetStatisticsAsync(token)   (BasePath "api/v1/admin/" + "statistics")
  -> API     GET /api/v1/admin/statistics        (GetStatisticsDashboardEndpoint,
                                                  Policies(PolicyFor(Statistics.View),
                                                           RequireApprovedAccount))
  -> IStatisticsService.GetDashboardAsync(ct)    (StatisticsService, SimfAppDbContext only)
  -> 11 separate AsNoTracking COUNT/AVG queries against SIMF_App
  -> ApiResult<StatisticsDashboard>.Ok(...)
  -> Forward(result) = Results.Json(result.Body, statusCode: result.StatusCode)
  -> _dashboard set, or _toast set -> render
```

**The Control Panel is a BFF, so all three layers are named above and all three
were verified in source.** There is no catch-all proxy: the CP route at
`AccountEndpoints.FeedbackAndReports.cs` line 205 is what makes the browser call
resolve at all.

Every backend call this page makes:

| When | Method + path | Request body | Response shape |
|------|---------------|--------------|----------------|
| `OnInitializedAsync` | `GET /account/api/admin/statistics` (CP BFF) | none | `ApiResult<StatisticsDashboard>` |
| forwarded, same request | `GET /api/v1/admin/statistics` (API, bearer) | none | `ApiResult<StatisticsDashboard>` |

That is the complete list. The page fires no other request, and there is no
refresh control - `LoadAsync` is called only from `OnInitializedAsync`, so a
fresh figure needs a browser reload or a re-navigation.

**Sibling endpoint, not this page's.** `GET /api/v1/admin/statistics/programme`
(`GetStatisticsProgrammeEndpoint`) carries the identical gate and is mapped as a
second BFF passthrough at `AccountEndpoints.FeedbackAndReports.cs` line 214, but
it is consumed by the CP landing page `Home.razor`, not by `/admin/statistics`.

**Rendering.** The CP runs `InteractiveServerNoPrerender`
(`App.razor`: `<Routes @rendermode="AppRenderMode.InteractiveServerNoPrerender" />`),
so the fetch happens once over the circuit rather than twice.

## 6. Validation + error handling

- **Client-side guards:** none, and none are needed - the page sends no input.
  The only branch is the envelope check
  `if (env is { Success: true, Data: not null })`.
- **Server-side validation:** N/A - there is no request body and no route
  parameter, so no FluentValidation validator exists for this endpoint.
- **Error envelope:** the standard `ApiResult<T>` with `Error.Code` and bilingual
  `Message` / `MessageArabic`. The page surfaces
  `env?.Error?.MessageForCurrentCulture()` and falls back to
  `L["Admin.Statistics.LoadFailed"]` when there is no server message.
- **JS-layer behaviours** (`wwwroot/js/simf-account.js`, `simfReadEnvelope`) -
  these fire before the Blazor code sees anything:
  - **HTTP 401** -> `window.location.assign('/login')` and then
    `return await new Promise(() => { })`. The source comment explains the
    never-resolving promise: it "stops the calling page from acting on a bogus
    body while the full-page navigation to `/login` is in flight". So a rejected
    session on this page is a redirect, never an alert.
  - **Empty body** -> `null`, which the page treats as a failure (`env?` is
    null-safe) and toasts `Admin.Statistics.LoadFailed`.
  - **Non-JSON body** (a framework HTML/text error page) -> a synthesised
    envelope `{ success: false, error: { code: 'BAD_RESPONSE', message: 'The server
    returned an unexpected response (HTTP <status>).', messageArabic: 'أعاد الخادم
    استجابة غير متوقعة (HTTP <status>).' } }`. The comment records why it is
    returned rather than thrown: so the calling page "shows a toast instead of
    throwing a JSException that trips the global Blazor error UI".
- **Toast strategy:** one variant only. `_toast` is a
  `private record Toast(string Variant, string Message)` and the sole assignment
  is `new Toast("error", ...)`, rendered as
  `<SimfAlert Variant="@_toast.Variant">@_toast.Message</SimfAlert>`. There is no
  success toast - a successful load is communicated by the tiles appearing.
- **Resx keys used by the page** (`Strings.resx` / `Strings.ar.resx`):

| Key | EN | AR |
|-----|----|----|
| `Admin.Statistics.Title` | `Statistics` | `الإحصائيات` |
| `Admin.Statistics.Loading` | `Loading statistics…` | `جارٍ تحميل الإحصائيات…` |
| `Admin.Statistics.None` | `No statistics are available yet.` | `لا توجد إحصائيات متاحة بعد.` |
| `Admin.Statistics.LoadFailed` | `Could not load statistics. Please try again.` | `تعذر تحميل الإحصائيات. حاول مرة أخرى.` |

Plus the eleven `Admin.Statistics.Stat.*` pairs listed in §4.3.

## 7. Edge cases + known limitations

- **A failure renders the alert AND the empty state together.** This is the one
  behaviour a reader is most likely to get wrong, so trace it precisely. The
  markup is:

  ```razor
  @if (_toast is not null)  { <SimfAlert Variant="@_toast.Variant">@_toast.Message</SimfAlert> }

  @if (_loading)            { <p>@L["Admin.Statistics.Loading"]</p> }
  else if (_dashboard is null) { <SimfEmptyState Title="@L["Admin.Statistics.None"]" /> }
  else                      { <StatisticsCards Dashboard="_dashboard" /> }
  ```

  The alert is an independent `@if`, not part of the chain. On any failure
  `_dashboard` stays null and `_loading` is false, so the `else if` also matches.
  The page therefore shows the red alert **and** "No statistics are available
  yet." at the same time. The E2E catalogue disagrees with the code here - see
  §11.
- **`Success: true` with `Data: null` is treated as an error, not as an empty
  state.** The guard is `env is { Success: true, Data: not null }`; a null
  payload fails it and falls into the `else`, which sets `_toast`. Since the
  server has sent no `Error`, `env?.Error?.MessageForCurrentCulture()` is null
  and the fallback `Admin.Statistics.LoadFailed` is shown. In practice
  `SimfEmptyState` never renders on its own: there are three reachable states -
  loading, eleven tiles, or alert-plus-empty-state.
- **A zero-data event still renders eleven tiles.** All counts `0` and
  `AverageRating` `0` produce a non-null payload, so the tiles branch is taken
  and every tile shows `0` / `0.0`. The empty state is not a "no rows" state.
- **Invariant formatting is deliberate.** Both `Count` and the average use
  `CultureInfo.InvariantCulture`, so an Arabic UI still shows `4.5` with a Latin
  dot rather than `4,5` or Arabic-Indic digits.
- **An empty ratings table does not throw.** `AverageAsync` over an empty
  sequence would; the `(double?)` projection makes it return null, folded to `0`
  by `?? 0`.
- **Ratings are averaged only over responses that carry an overall score.** The
  predicate is `r.IsActive && r.OverallStars != null`, so a response with no
  overall star rating is counted by `RatingsCount` but excluded from
  `AverageRating`. The two tiles are not derived from the same row set.
- **Soft-deleted rows are excluded everywhere they can be.** Every event-module
  count filters on `IsActive`, matching the public and admin list behaviour.
- **No refresh, no auto-poll, no SignalR.** `SIMF-FDS-011` §5.1 says live figures
  "refresh live over SignalR"; this page does not - it loads once in
  `OnInitializedAsync`. Recorded as a gap against the FDS, not as a defect in the
  page.
- **No date range, no trend, no export.** There is no snapshot table to trend
  against (`SIMF-FDS-011` Amendment B), and no export control is wired. For
  date-ranged, exportable views the Reporting module is the separate surface.
- **Companies are intentionally absent from the contract.** The contract's own
  XML comment: they "are owned by a separate vertical and may not be present; the
  count can be folded in later without changing the existing field shape
  (additive record extension)."
- **The cancellation token is not threaded from the page.** `GetDashboardAsync`
  accepts one and the endpoint passes FastEndpoints' `ct`, but the CP page calls
  through JS interop with no token, so navigating away mid-load does not cancel
  the request.
- **`StatisticsCards` was extracted to stop two pages drifting, and now has one
  consumer.** Its comment records the original problem: the eleven
  `<SimfStatCard>` "were duplicated verbatim between the Dashboard ("/") and the
  Statistics page ("/admin/statistics"): the same eleven `<SimfStatCard>` with
  the same titles, the same `Href` targets and the same `Count()` formatting, in
  two files. A metric added or relabelled in one would silently disagree with the
  other." `Home.razor` was genuinely moved onto the component (commit
  `c0d9055a8`, 2026-07-28, whose diff swaps its eleven inline cards for
  `<StatisticsCards Dashboard="_dashboard" />`), but it was later rebuilt around
  the programme dashboard and now renders its own tile set from `Dashboard.Stat.*`
  keys over both `StatisticsDashboard` and `StatisticsProgramme`. As of this
  review `<StatisticsCards` appears in exactly one file - this page. The
  extraction still holds the line it was built for, and
  `SilentFailureTests.The_statistics_cards_are_declared_once` still enforces it
  by failing the build if `Admin.Statistics.Stat.` appears more than once in more
  than one CP component file.

## 8. i18n + RTL

- Every visible string on the page comes from `IStringLocalizer<Strings> L` over
  `Strings.resx` (EN) + `Strings.ar.resx` (AR). The page hardcodes no user-facing
  text; the only literal in the markup is the ` · SIMF` suffix in `<PageTitle>`.
  All fifteen keys the page uses (four page keys + eleven tile keys) exist in
  **both** resx files - verified by reading them.
- The language toggle is not on the page. `CpShellLayout` renders
  `<SimfLanguageSwitch Label='@L["Shell.SwitchLanguage"]' />` in the shell's
  `<Controls>` slot, so it is present on every signed-in CP page including this
  one.
- **RTL:** `App.razor` sets the document direction from the current culture -
  `<html lang="@CultureInfo.CurrentUICulture.TwoLetterISOLanguageName" dir="@(CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? "rtl" : "ltr")">`
  so Arabic renders `dir="rtl" lang="ar"` and the shell, nav rail and tile flow
  mirror with it.
- **Numbers stay Latin in both directions.** This is intentional, not an RTL
  miss: `Count` and the average are formatted with `CultureInfo.InvariantCulture`
  regardless of UI culture.

## 9. Accessibility

- **Keyboard:** the eleven tiles are real anchors (`SimfStatCard` renders
  `<a href>` whenever `Href` is set), so they are in the tab order natively and
  activate with Enter. There is no modal on the page, so there is no focus trap
  and no ESC handling to describe.
- **Focus indicator - a real defect that was fixed, worth keeping visible.**
  `.simf-stat--clickable:focus-visible` is `outline: 2px solid var(--color-focus); outline-offset: 2px;`.
  The CSS comment above it records why it reads that way: "`--color-accent` is not
  a token that exists - `theme.tokens.css` defines `--color-accent-blue` /
  `--color-accent-gold` / `--color-focus`. An undefined custom property makes the
  whole declaration invalid at computed-value time, so the outline fell back to
  its initial value and these dashboard tiles had NO focus" indicator. There is
  also an explicit Forced Colors / High Contrast block further down the same
  stylesheet (attributed there to D-045 H2), because `box-shadow` is suppressed
  in that mode and `outline` is repainted in the system colour.
- **Focus on navigation:** `Routes.razor` carries
  `<FocusOnNavigate RouteData="routeData" Selector="h1" />`, and `SimfBanner`
  renders `<h1 class="simf-banner__title">`, so arriving on the page moves focus
  to the "Statistics" heading.
- **Screen reader:** the error alert is announced assertively - `SimfAlert`'s
  error variant renders `<div class="simf-alert simf-alert--error" role="alert">`
  (its info and success variants use `role="status" aria-live="polite"`, but this
  page only ever uses the error variant). `SimfEmptyState` renders its title as a
  plain `<p class="simf-empty__title">`, so the "No statistics are available yet."
  message is readable text and not a heading or a live region.
- **Tile accessible name:** each card's name is its text content - the title
  paragraph followed by the value paragraph. `SimfStatCard` sets no `aria-label`
  and no `title` attribute, so a screen reader announces, for example, "Total
  attendees 237, link".
- **Skip link and nav labelling** come from the shell: `CpShellLayout` passes
  `SkipNavLabel`, `NavLabel` and `ToggleNavLabel` into `SimfAppShell`, all from
  resx.
- **Colour contrast:** Unverified - no contrast measurement was run against the
  theme tokens for this page in this review.

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| UC-30 | View the statistics dashboard | `SIMF-UCS-001` maps it to actors "organising teams" and to FR-1101-FR-1103. `SIMF-FDS-011` §3 maps the same UC to FR-1101 (per-day statistics), FR-1102 (overall statistics) and FR-1103 (GPS-presence tracking and live attendance). **This page delivers the FR-1102 half only** - the per-day figures live on the CP landing page's programme dashboard, and live attendance on `/admin/attendance`. |

## 11. Related E2E test scenarios

The catalogue is [`docs/tests/e2e/cp-admin-statistics.md`](../../tests/e2e/cp-admin-statistics.md),
scenario ids `E2E-STA-001..012` plus `E2E-STA-ELS-001/002`. The index row in
`docs/tests/e2e/README.md` records the range as `E2E-STA-001..012`, so the two
element-sweep ids are not reflected there.

| Scenario | File | Coverage |
|----------|------|----------|
| `E2E-STA-001` Golden path - load + render the tiles | `docs/tests/e2e/cp-admin-statistics.md` | Single `GET`, BFF forward, banner, tile order, invariant values |
| `E2E-STA-002` Loading indicator | same | `Admin.Statistics.Loading` while `_loading` is true |
| `E2E-STA-003` `Success:true` with `Data:null` | same | **Drifted** - see the note below |
| `E2E-STA-004` Zero-data event | same | All counts `0`, average `0.0`, tiles not empty state |
| `E2E-STA-005` Auth gate, admin lacking `Statistics.View` | same | `/not-permitted`, nav row hidden, no request fired |
| `E2E-STA-006` Auth gate, unauthenticated | same | Redirect to `/login` |
| `E2E-STA-007` Average-rating formatting | same | `"0.0"` invariant in both cultures |
| `E2E-STA-008` Server 500 | same | Red `SimfAlert` with the fallback string |
| `E2E-STA-009` `Success:false` with a server error | same | **Drifted** - see the note below |
| `E2E-STA-010` Counts reflect live state | same | Approve a pending attendee, reload, two tiles move |
| `E2E-STA-011` Read-only surface | same | No write controls, no `POST`/`PUT`/`DELETE` |
| `E2E-STA-012` RTL / Arabic render | same | `dir="rtl"`, Arabic titles, Latin digits |
| `E2E-STA-ELS-001/002` Element inventory + health | same | Control inventory in LTR and RTL; no dead control, no console error, no horizontal overflow |

**The catalogue is stale in three places, recorded here rather than silently
inherited. This doc describes the page as built.**

1. **Tile count.** The catalogue says "14 `SimfStatCard` tiles" throughout and its
   RTL scenario lists fourteen Arabic titles. The page renders **eleven**. Its
   last-reviewed date is 2026-06-02, which predates both removals: commit
   `b3ba62628` (2026-06-04, D-277) dropped the Delegations count card and the
   `StatisticsDashboard.Delegations` field with the feature, and commit
   `b748c4ab0` (2026-07-04, decision recorded as D-589 dated 2026-07-04 - note
   that id also labels an unrelated 2026-07-02 entry) removed the two audience-
   comment metrics along with the comments feature. The catalogue's own tile
   table already lists only twelve rows against its "14" prose, so it was
   internally inconsistent before either removal was reflected.
2. **`E2E-STA-003`** asserts "no error `SimfAlert` appears" for a `Data:null`
   payload. Source says otherwise: the null-Data case fails the
   `Success: true, Data: not null` guard and lands in the `else`, which sets
   `_toast`. The alert does appear, carrying the `Admin.Statistics.LoadFailed`
   fallback.
3. **`E2E-STA-009`** asserts "the `SimfEmptyState` is NOT shown". Source says it
   is: the alert is an independent `@if`, and `_dashboard is null` still matches
   the chain's `else if`. Both render.

**Lower-layer coverage - the honest position.**

- `GetStatisticsDashboardEndpoint` carries
  `// Tests: SIMF.Api.Tests/StatisticsTests.cs`, and **that file does not exist**.
  `StatisticsService` does **not** carry that pointer - its header reads
  `// Tests: SIMF.Api.Tests/StatisticsProgrammeTests.cs`, and the file says the
  rest itself: "(the previously referenced
  `StatisticsTests.cs` does not exist - `GetDashboardAsync` has no direct
  coverage; noted rather than silently left pointing at nothing.)" So the eleven
  aggregate queries behind this page have **no direct automated test**.
- `tests/SIMF.Api.Tests/StatisticsProgrammeTests.cs` exists but pins
  `/api/v1/admin/statistics/programme` (the Saudi day-boundary bucketing behind
  the landing-page chart). It does **not** cover this page's endpoint.
- What is covered, generically:
  - `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` - proves the per-page
    permission gate cannot be bypassed at the API.
  - `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs` - every nav
    `RequiredPermission` is a real catalogue code, every real `/admin` nav item is
    gated, and each nav gate matches the gate on the page it links to.
  - `tests/SIMF.ControlPanel.Tests/CpNavigationIconTests.cs` - pins the nav icon
    name, the guard added after `"chart-bar"` broke the shell.
  - `tests/SIMF.ControlPanel.Tests/SilentFailureTests.cs`,
    `The_statistics_cards_are_declared_once` - fails the build if
    `Admin.Statistics.Stat.` appears more than once in more than one CP component
    file, so the tile set cannot be duplicated back into a second page.

## 12. Related docs

- **Authority spec:** [`SIMF-FDS-011-Statistics-and-Dashboards.md`](../../SIMF-FDS-011-Statistics-and-Dashboards.md)
  (version 1.2, 2026-08-18). Read Amendment B first: `StatisticSnapshot` is
  PROPOSED, NOT BUILT, and every figure is computed on read.
- **E2E catalogue:** [`docs/tests/e2e/cp-admin-statistics.md`](../../tests/e2e/cp-admin-statistics.md).
- **Page index:** [`docs/pages/PAGE-INDEX.md`](../PAGE-INDEX.md), row `/admin/statistics`.
- **Permissions:** [`docs/SIMF-Permission-Catalogue.md`](../../SIMF-Permission-Catalogue.md)
  and `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`; the source of truth is
  `src/Shared/SIMF.Common/PermissionCatalog.cs`.
- **API contract:** [`SIMF-API-001-API-Specification.md`](../../SIMF-API-001-API-Specification.md)
  for the `ApiResult<T>` envelope this endpoint returns.
- **Component catalogue:** [`SIMF-CMP-001-Component-Catalog.md`](../../SIMF-CMP-001-Component-Catalog.md).
  Components used here: `SimfBanner`, `SimfAlert`, `SimfEmptyState`,
  `SimfStatCard`, plus the CP-local `StatisticsCards`.
- **Decisions:** `docs/decisions/DECISIONS_LOG.md` - D-202 (2026-05-31, the
  original read-only aggregate endpoint), D-277 (2026-06-04, Delegations removed
  including its stat card), D-589 (2026-07-04 entry, audience comments removed
  including their two stat cards). Read the log's "Reading an ID" preamble: ids
  collide, and D-589 in particular labels two unrelated entries.
- **No per-page CP documentation set exists for this route.** `docs/CP/` has no
  `admin-statistics` folder, unlike the 4-aspect sets some CRUD pages carry.
- **Source:** [`StatisticsDashboard.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/StatisticsDashboard.razor),
  [`StatisticsDashboard.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/StatisticsDashboard.razor.cs),
  [`StatisticsCards.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/StatisticsCards.razor),
  [`StatisticsEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Statistics/StatisticsEndpoints.cs),
  [`StatisticsService.cs`](../../../src/Backend/SIMF.Infrastructure/Statistics/StatisticsService.cs),
  [`StatisticsContracts.cs`](../../../src/Shared/SIMF.Contracts/Statistics/StatisticsContracts.cs).

## 13. Changelog

Dates and subjects below are `git log` facts for the named commits; decision ids
are quoted only where the commit or the decisions log carries them.

| Date | Decision / commit | Change |
|------|-------------------|--------|
| 2026-05-31 | D-202 | The read-only aggregate shipped: `IStatisticsService` / `StatisticsService`, `GET /api/v1/admin/statistics`, and the `StatisticsDashboard` contract. No schema - the log entry records the choice as deliberate: "Statistics is correctly schema-free (live COUNT/AVG, not a snapshot table)". |
| 2026-06-04 | D-277 / `b3ba62628` | Delegations removed permanently. The commit body records the effect on this page: "dropped the Delegations count card + `StatisticsDashboard.Delegations` DTO field + its query (owner-confirmed)". |
| 2026-07-04 | D-589 (2026-07-04 entry) / `b748c4ab0` | Audience comments and likes removed from backend and CP. The commit body names "the two statistics metrics (service + `StatisticsDashboard` record + CP dashboard tiles + EN/AR resx)". Tile count reaches its current eleven. |
| 2026-07-14 | `b6ff9da2d` | "enhance dashboard stat cards with clickable links for navigation" - each tile gains its `Href`, turning `SimfStatCard` into an anchor. |
| 2026-07-28 | `c0d9055a8` (§6.16) | CP-wide sweep closing nav-gate, accessible-name and token findings. Three items touch this page: the `--color-accent` focus-ring fix on `.simf-stat--clickable`; the `NAV-001` split between `/login` and `/not-permitted`; and `NAV-008`, which extracted the eleven cards into `StatisticsCards.razor` and put `Home.razor` onto it (the diff swaps Home's eleven inline `<SimfStatCard Title="@L["Admin.Statistics.Stat.*"]">` lines for `<StatisticsCards Dashboard="_dashboard" />`). Two consumers at this point. The `§6.16` / `NAV-008` tags are quoted from `StatisticsCards.razor` and `SilentFailureTests.cs`; they are not decision ids. `NAV-008` itself appears in no other `docs/` file, but `§6.16` does - `docs/decisions/DECISIONS_LOG.md` carries it on D-782 / D-783 / D-784, and `docs/pages/PAGE-INDEX.md` carries it with the sibling `NAV-001` / `NAV-004` / `NAV-005` / `NAV-011` tags. |
| 2026-07-29 | `f5f0236a7` | The CP landing page was rebuilt as the programme dashboard (new `StatisticsProgramme` contract and `GET /admin/statistics/programme` behind the same `Statistics.View` gate). `StatisticsDashboard` was left "byte-identical", so this page was unaffected. `Home.razor` today renders its own `Dashboard.Stat.*` tiles and `<StatisticsCards` appears in exactly one file. **Which commit dropped the Home usage was not pinned:** `git log -S "<StatisticsCards" --full-history` on `Home.razor` reports only the `c0d9055a8` addition, and `f5f0236a7`'s own `Home.razor` diff contains no `StatisticsCards` line, so the removal most likely landed in a merge resolution rather than in a single commit. Recorded as unpinned rather than guessed. |
| 2026-08-13 | D-877 (freeze lift D-881) / `7be67b274` | Attendee figures moved off Identity accounts onto `UserProfile` + `UserProfile.AdmissionState`, so an accountless walk-in or pre-generated badge is counted and approval is read from the row that decides entry. `StatisticsService` no longer injects the Identity context. |
| 2026-08-18 | `SIMF-FDS-011` v1.2, Amendment B | The spec was corrected to as-built: `StatisticSnapshot` marked PROPOSED, NOT BUILT; the stored-aggregate half of the document is design intent, not build. |

---

_Last reviewed:_ 2026-08-19 by Claude (first issue of this page's reference doc;
authored from source). Every endpoint, permission code, field name, resx value,
component and test class above was read in this repository during that review.
Items explicitly **not** verified are marked "Unverified" or "N/A" in place. If
the page has changed and this doc has not been re-reviewed in 60 days, it is
**out of date** - re-walk the page in a browser and update every section that
drifted.
