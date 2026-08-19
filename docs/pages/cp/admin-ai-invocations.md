# AI invocations log - `/admin/ai/invocations`

| | |
|--|--|
| **Route** | `/admin/ai/invocations` (`AiInvocationsLog.razor` line 6) |
| **Layout** | `CpShellLayout` (`@layout` on line 7) |
| **Surface** | Control Panel |
| **Audience** | Administrator. The permission's baseline role set is `AdminOnly` (`PermissionCatalog.cs:1140`). |
| **Auth** | Page: `@attribute [RequirePermission(PermissionCatalog.AiInvocations.View)]`. API: `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.AiInvocations.View), nameof(AuthorizationPolicies.RequireApprovedAccount))` on both endpoints. BFF: the whole `/account/api` group is `routes.MapGroup("/account/api").RequireAuthorization()` (`AccountEndpoints.cs:33`). |
| **Pattern** | Read-only append-only log on the canonical `SimfDataGrid`, plus one `CustomToolbar` toggle and one read-only row-action detail modal. **Not** a CRUD page - no Add / Edit / Delete is wired. |
| **Status** | Real |
| **Implements use case(s)** | N/A - no use case in `SIMF-UCS-001` covers the AI invocations log. See section 10. |
| **Backend endpoints** | `POST /account/api/admin/ai/invocations/list` -> `POST /api/v1/admin/ai/invocations/list`; `GET /account/api/admin/ai/invocations/{id:guid}` -> `GET /api/v1/admin/ai/invocations/{id:guid}`. Full three-layer trace in section 5. |
| **Source file** | [`AiInvocationsLog.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/AiInvocationsLog.razor) + [`AiInvocationsLog.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/AiInvocationsLog.razor.cs); BFF [`AccountEndpoints.AiAndEmail.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.AiAndEmail.cs); client [`SimfAdminClient.AiAndEmail.cs`](../../../src/Shared/SIMF.ApiClient/SimfAdminClient.AiAndEmail.cs); API [`AiPromptAdminEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/AiPromptAdminEndpoints.cs); service [`AdminAiPromptService.cs`](../../../src/Backend/SIMF.Infrastructure/Ai/AdminAiPromptService.cs); entity [`AiInvocation.cs`](../../../src/Backend/SIMF.Domain/Ai/AiInvocation.cs) |
| **Tests** | [`docs/tests/e2e/cp-admin-ai-invocations.md`](../../tests/e2e/cp-admin-ai-invocations.md) (**stale in four places - see section 11**), [`AiModuleTests.cs`](../../../tests/SIMF.Api.Tests/AiModuleTests.cs), [`AiHardeningTests.cs`](../../../tests/SIMF.Api.Tests/AiHardeningTests.cs), [`PermissionEnforcementTests.cs`](../../../tests/SIMF.Api.Tests/PermissionEnforcementTests.cs), [`CpNavigationPermissionTests.cs`](../../../tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs) |
| **Last reviewed** | 2026-08-19 |

---

## 1. Purpose

Every call the platform makes to an AI provider writes one `AiInvocation` row,
on failure as well as success - the entity's own summary is "One telemetry row
per AI call, written on failure as well as success". This page is the only
in-product way to read that table. An administrator walks in wanting to answer
an operational question that the aggregate on `/admin/ai` cannot: which prompt
key is erroring, which provider slowed down, who called it, and what exactly was
sent and returned. The grid answers the first three; the per-row Detail modal
answers the fourth by fetching the redacted `InputJson` and `OutputText`
that the list projection deliberately omits (both are redacted at write time -
see section 7). The page writes nothing - there is
no create, edit or delete, because the log is append-only telemetry and an
editable audit trail is not an audit trail.

## 2. Audience + permissions

- **Who can reach it:** any signed-in Control Panel user whose role grants
  `AiInvocations.View`, plus Administrator, which holds the `"*"` wildcard. The
  catalogue entry's baseline role set is `AdminOnly`
  (`new(AiInvocations.View, "AiInvocations", "View", "View AI invocations log", AdminOnly)`).
- **Who can edit/write on it:** nobody. The page has no write path.
- **Authorisation gates - all three layers carry the same code, `AiInvocations.View`:**

  | Layer | Gate |
  |-------|------|
  | CP page | `@attribute [RequirePermission(PermissionCatalog.AiInvocations.View)]`. `RequirePermissionAttribute` is an `AuthorizeAttribute` that sets `Policy = PermissionCatalog.PolicyFor(permissionCode)` (`PermissionAuthorization.cs:100-106`). |
  | CP nav item | `new("Module.AiInvocations", "/admin/ai/invocations", RequiredPermission: PermissionCatalog.AiInvocations.View, Icon: "list-tree")` (`CpNavigation.cs:131`). |
  | Row action | `<AuthorizedAction Permission="@PermissionCatalog.AiInvocations.View">` around the Detail button. The page comment gives the reason: "The endpoint gates on AiInvocations.View, so the action does too." |
  | BFF | Group-level `.RequireAuthorization()`; each handler additionally returns `Results.Unauthorized()` when `http.GetTokenAsync("access_token")` is null. The BFF does **not** re-check the permission code - it forwards the bearer token and lets the API decide. |
  | API - list | `ListAiInvocationsEndpoint`: `Policies(PolicyFor(AiInvocations.View), nameof(AuthorizationPolicies.RequireApprovedAccount))`. |
  | API - detail | `GetAiInvocationDetailEndpoint`: the same two policies, **plus** `Options(rb => rb.RequireRateLimiting("ai-test"))`. |

  This page is unusual in that the row action carries the *same* code as the
  page. That is correct here because the button calls an endpoint gated on that
  code; it is not the general case (see the CLAUDE.md hard rule).
- **What a denied user sees.** The two cases split in `Routes.razor`'s
  `<NotAuthorized>` branch, because `AuthorizeRouteView` renders it for both.
  An **unauthenticated** visitor gets `<RedirectToLogin />`, matching the cookie
  handler's `LoginPath = "/login"` (`Program.cs:76`) and the comment above it,
  "an unauthenticated request to a protected page is sent to the sign-in page".
  A **signed-in** admin who lacks the permission gets `<RedirectToNotPermitted />`,
  which is `Nav.NavigateTo("/not-permitted")`. The cookie handler's
  `AccessDeniedPath = "/not-permitted"` (`Program.cs:77`) covers the
  access-denied case, not the unauthenticated one, and the Blazor interactive
  router never goes through it anyway - which is the reason
  `RedirectToNotPermitted` exists, per its own summary. The nav item is hidden
  for that same user, because `RequiredPermission` is unmet.

## 3. Screenshots

**No screenshots have been captured for this page.** The table below records the
file names the E2E catalogue expects so a later capture pass has a target; every
row is uncaptured today. Do not treat any of these paths as existing files.

| State | File | Captured |
|-------|------|----------|
| Default (with rows) | `docs/screenshots/cp-admin-ai-invocations-golden.png` | Not captured |
| Loading | `docs/screenshots/cp-admin-ai-invocations-loading.png` | Not captured |
| Empty state | `docs/screenshots/cp-admin-ai-invocations-empty.png` | Not captured |
| Errors-only filter on | `docs/screenshots/cp-admin-ai-invocations-errors-only.png` | Not captured |
| Column filter applied | `docs/screenshots/cp-admin-ai-invocations-col-filter.png` | Not captured |
| Column sort applied | `docs/screenshots/cp-admin-ai-invocations-sort.png` | Not captured |
| Detail modal | `docs/screenshots/cp-admin-ai-invocations-detail-modal.png` | Not captured |
| RTL (Arabic) | `docs/screenshots/cp-admin-ai-invocations-rtl.png` | Not captured |
| Server 500 | `docs/screenshots/cp-admin-ai-invocations-500.png` | Not captured |
| Auth gate | `docs/screenshots/cp-admin-ai-invocations-not-permitted.png` | Not captured |

## 4. UI affordances

### 4.1 Banner / page header

`<SimfBanner Title="@L["Admin.AiInvocations.Title"]" />` - title only, no
subtitle and no actions slot. `<PageTitle>` renders
`@L["Admin.AiInvocations.Title"] · SIMF`. The resx pair is "AI invocations" /
"سجلّ استدعاءات الذكاء الاصطناعي".

Directly under the banner, inside `.simf-page-wide > .simf-surface`, the page
renders a single toast slot: when `_toast is not null`, a
`<div class="simf-toast"><SimfAlert Variant="@_toast.Variant">` holding the
message. `_toast` is a page-private `record Toast(string Variant, string Message)`
and is only ever constructed with `Variant = "error"`.

### 4.2 Toolbar

N/A as a CRUD toolbar - **no** Add / Edit / Delete / Duplicate / Import / Export
control renders. `SimfDataGrid` guards each of those on its delegate being wired
(`@if (OnAdd.HasDelegate)`, `@if (OnEditOne.HasDelegate)`,
`@if (OnDeleteSelected.HasDelegate)` at lines 60, 69 and 79), and this page wires
none of them. `Multiselect="false"`, so there is no select-all button and no
checkbox column either.

The page still passes `AddLabel`, `EditLabel` and `DeleteLabel` from the shared
`Grid.*` resx keys. Those are inert given no delegate is wired; they are label
parameters, not enablement switches.

What does render in the toolbar is the one `<CustomToolbar>` control:

| Control | Wired callback | Calls | Notes |
|---------|----------------|-------|-------|
| "Errors only" checkbox (`SimfCheckbox`, label `Admin.AiInvocations.Filter.ErrorsOnly` / "الأخطاء فقط") | `OnErrorsOnlyChangedAsync(bool)` | re-issues `POST /account/api/admin/ai/invocations/list` | Ticking sets `_query.Filters["errorOnly"] = "true"`; unticking **removes** the key rather than sending `"false"`. It also sets `_query.Skip = 0`, so the filter always lands the user on page 1. |

The one per-row action lives in `<RowActions>`, which makes `SimfDataGrid` render
an actions column headed by `ActionsHeader` (`Grid.Actions`):

| Row action | Wired callback | Calls | Gate |
|------------|----------------|-------|------|
| Detail (`SimfToolbarButton Icon="eye"`, title `Admin.AiInvocations.Detail` / "التفاصيل") | `OnDetailAsync(context)` | `GET /account/api/admin/ai/invocations/{row.Id}` | `<AuthorizedAction Permission="@PermissionCatalog.AiInvocations.View">` |

### 4.3 Grid columns

Eight columns, in render order. "Sortable" and "Filterable" below are what the
**UI** exposes; the server accepts more (see the note under the table).

| # | Column | Grid `Key` | Source field | Sortable | Filterable | Cell rendering |
|---|--------|-----------|--------------|----------|------------|----------------|
| 1 | Time | `createdAt` | `AdminAiInvocationRow.CreatedAt` | yes | no | `@context.CreatedAt.FormatSaudi("dd-MM-yyyy hh:mm:ss tt")` |
| 2 | Prompt key | `promptKey` | `.PromptKey` | yes | **yes** | wrapped in `<code>` |
| 3 | Feature | `feature` | `.Feature` (`AiFeature`) | yes | no | enum name, verbatim |
| 4 | Provider | `provider` | `.Provider` (`AiProvider`) | yes | no | enum name, verbatim |
| 5 | Caller | `callerKind` | `.CallerKind` | yes | **yes** | free text, bounded to five values by a CHECK constraint |
| 6 | Latency | `latencyMs` | `.LatencyMs` | yes | no | `@(context.LatencyMs)ms` |
| 7 | Tokens (in/out) | `tokens` | `.TokensInput` / `.TokensOutput` | no | no | `@(context.TokensInput?.ToString() ?? "—") / @(context.TokensOutput?.ToString() ?? "—")` |
| 8 | Error | `error` | `.ErrorCode` | no | no | `<SimfPill Variant="off">@context.ErrorCode</SimfPill>` when `ErrorCode is not null`; **nothing at all** when null |

Column headers come from `Admin.AiInvocations.Col.*`: Time / Prompt key /
Feature / Provider / Caller / Latency / Tokens (in/out) / Error, and in Arabic
الوقت / مفتاح المحفّز / الميزة / الموفّر / المستدعي / زمن الاستجابة /
الرموز (داخل/خارج) / الخطأ.

**Server-side contract, and why columns 7 and 8 are inert.** The allow-list is
`AdminAiPromptService.InvocationColumns`:

```
.Add("createdAt", …).Add("promptKey", …).Add("feature", …)
.Add("provider", …).Add("callerKind", …).Add("latencyMs", …)
.AddFilter("errorOnly", ErrorOnly)
.DefaultOrder("createdAt", descending: true)
.PageSize(fallback: 50, max: 500)
```

Every key declared with `Add` is both sortable and filterable server-side, so
the API would also honour a filter on `createdAt` (a Saudi calendar day,
half-open `[d, d+1)`), `feature` and `provider` (enum **by name**,
case-insensitive), and `latencyMs` (integer equality). The UI surfaces none of
those four. Conversely `tokens` and `error` exist only as UI keys - there is no
server column of either name, so marking them `Sortable` or `Filterable` would
produce a 400 (`ErrorCodes.GridSortKeyInvalid` /
`ErrorCodes.GridFilterKeyInvalid`), not a silently ignored predicate. That is
`GridColumns` working as designed: an unknown key is a loud failure rather than
a dropped filter.

`errorOnly` is hand-written rather than a column because it reads a nullable
error code as a boolean, which no column declaration can infer. Its semantics
are asymmetric and matter: `"true"` yields `ErrorCode != null`, and `"false"`
yields `ErrorCode == null` - that is, **success-only**, not "no filter". A value
that will not parse as a bool throws `GridFilters.ValueInvalid("errorOnly", raw, "true or false")`.

### 4.4 Pager

Rendered by `SimfDataGrid`, all labels supplied from the shared `Grid.*` keys:

- First / Prev / a 5-wide numbered window centred on the current page / Next /
  Last (`PageNumbersToShow()`, `const int window = 5`).
- Page-size selector with the component default `PageSizeOptions` of
  **10 / 20 / 50 / 100**, labelled `Grid.PageSize`. The page's own initial query
  is `new GridQuery { Top = 20 }`, so the grid opens on 20 even though the
  service's own fallback is 50; 100 is well inside the service's `max: 500` cap.
- Summary, via `FormatSummary(skip, taken, total)` -> `Grid.Summary`:
  `"Showing {0}–{1} of {2}"` / `"عرض {0}–{1} من {2}"`, formatted as
  `(skip + 1, skip + taken, total)`.
- Page label, via `FormatPage(current, total)` -> `Grid.Page`:
  `"Page {0} of {1}"` / `"صفحة {0} من {1}"`.
- Every navigation path resets or recomputes `Skip`: sort sets `Skip = 0`
  (line 880), a column filter sets `Skip = 0` (line 912), a page-size change
  sets `Skip = 0` (line 1012), First sets `Skip = 0` (line 981), Next/Prev step
  by `Top`, Last computes `(TotalPages - 1) * Top`.

Empty result: `<EmptyTemplate><SimfEmptyState Title="@L["Admin.AiInvocations.None"]" /></EmptyTemplate>`,
which reads "No invocations recorded yet." / "لا توجد استدعاءات حتى الآن."

### 4.5 Form fields

N/A - the page hosts no form and no editable field. What it hosts is a
**read-only detail modal**, rendered when `_detail is not null`:

`<SimfModal Open="true" Title="@L["Admin.AiInvocations.Detail.Title"]" OnClose="CloseDetail" CloseLabel="@L["Grid.Close"]">`
("Invocation detail" / "تفاصيل الاستدعاء"; close label "Close" / "إغلاق").

Its body is a `<dl class="simf-dl">` over `AdminAiInvocationDetail`:

| Term (resx key) | Value |
|-----------------|-------|
| `Admin.AiInvocations.Col.PromptKey` | `_detail.PromptKey` in `<code>` |
| `Admin.AiInvocations.Col.Feature` | `_detail.Feature` |
| `Admin.AiInvocations.Col.Provider` | `_detail.Provider · _detail.Model` - the only place `Model` is shown |
| `Admin.AiInvocations.Col.Caller` | `_detail.CallerKind` |
| `Admin.AiInvocations.Col.Latency` | `@(_detail.LatencyMs)ms` |
| `Admin.AiInvocations.Col.Tokens` | `in / out`, `—` where null |
| `Admin.AiInvocations.Col.Error` | `<SimfPill Variant="danger">`, rendered **only** when `ErrorCode is not null` |
| `Admin.AiInvocations.Detail.Input` ("Input (redacted)" / "المدخلات (مُخفّاة)") | `_detail.InputJson` in `<pre class="simf-pre--wrap">` |
| `Admin.AiInvocations.Detail.Output` ("Output" / "المخرجات") | `_detail.OutputText ?? "—"` in `<pre class="simf-pre--wrap">` |

The footer is a single `SimfButton Variant="secondary"` labelled
`Admin.AiInvocations.Detail.Close`, calling the same `CloseDetail` as the header
close button. `CloseDetail` is `_detail = null` - no server call on close.

`CallerUserId` is on both contracts but is rendered on **neither** the grid nor
the modal. Resolving it to a name would need a second query against the Identity
database, which the D-157 separation forbids doing as a join.

## 5. Data flow

```
Page init (OnInitializedAsync)
  -> LoadAsync()
  -> JS.InvokeAsync("simfAccount.postJson", "/account/api/admin/ai/invocations/list", _query)
  -> fetch(url, { credentials: 'same-origin' })            [simf-account.js]
  -> AccountEndpoints.MapAiAndEmail: group.MapPost("/admin/ai/invocations/list")
       token = http.GetTokenAsync("access_token")           [401 if absent]
  -> SimfAdminClient.ListAiInvocationsAsync(query, token)   [relative "ai/invocations/list"]
  -> POST /api/v1/admin/ai/invocations/list                 [ListAiInvocationsEndpoint]
  -> IAdminAiPromptService.ListInvocationsAsync(query, ct)
  -> appDbContext.AiInvocations.ToGridPageAsync(query, InvocationColumns, …)
  -> SIMF_App.dbo.AiInvocations
  -> ApiResult<GridPage<AdminAiInvocationRow>> -> _page -> grid re-renders

Detail button (OnDetailAsync(row))
  -> _toast = null
  -> JS.InvokeAsync("simfAccount.getJson", $"/account/api/admin/ai/invocations/{row.Id}")
  -> group.MapGet("/admin/ai/invocations/{id:guid}")
  -> SimfAdminClient.GetAiInvocationAsync(id, token)
  -> GET /api/v1/admin/ai/invocations/{id:guid}             [GetAiInvocationDetailEndpoint]
       rate limiter "ai-test"  +  auditLog.WriteSuccessAsync(AuditEvents.AiInvocationViewed, …)
  -> IAdminAiPromptService.GetInvocationAsync(id, ct)       [AsNoTracking, SingleOrDefaultAsync]
  -> ApiResult<AdminAiInvocationDetail> -> _detail -> SimfModal opens
```

Every backend call the page makes:

| When | CP page call | BFF route (`AccountEndpoints.AiAndEmail.cs`) | API endpoint | Request body | Response shape |
|------|--------------|---------------------------------------------|--------------|--------------|----------------|
| `OnInitializedAsync`; every `OnQueryChangedAsync` (sort, column filter, page, page size); every `OnErrorsOnlyChangedAsync` | `simfAccount.postJson` | `POST /account/api/admin/ai/invocations/list` -> `SimfAdminClient.ListAiInvocationsAsync` | `POST /api/v1/admin/ai/invocations/list` (`ListAiInvocationsEndpoint`) | `GridQuery` (`Skip`, `Top`, `Search`, `Sort`, `SortDescending`, `Filters`) | `ApiResult<GridPage<AdminAiInvocationRow>>` |
| Detail row action | `simfAccount.getJson` | `GET /account/api/admin/ai/invocations/{id:guid}` -> `SimfAdminClient.GetAiInvocationAsync` | `GET /api/v1/admin/ai/invocations/{id:guid}` (`GetAiInvocationDetailEndpoint`) | none (route id) | `ApiResult<AdminAiInvocationDetail>` |

`AdminAiInvocationRow` carries `Id, PromptKey, Feature, Provider, Model,
CallerKind, CallerUserId, TokensInput, TokensOutput, LatencyMs, ErrorCode,
CreatedAt`. `AdminAiInvocationDetail` is the same set **plus** `InputJson` and
`OutputText`. The split is deliberate: the contract's own summary says the grid
row "deliberately omits `InputJson` + `OutputText` so the admin grid stays light
and non-PII".

**Audit asymmetry worth knowing.** Listing writes no audit row. Opening the
detail writes one `AuditEvents.AiInvocationViewed` row per read, carrying
`invocationId`, `promptKey`, `feature` and `hasOutput`. The endpoint comment
gives the reason: "Without this, 'admin reads 50k invocations on Sunday night'
is invisible to SOC."

## 6. Validation + error handling

- **Client-side guards:** none. There is nothing to validate - the only inputs
  are a checkbox, the grid's own filter/sort/pager controls, and a row id the
  page took from a row it already rendered. The grid debounces filter typing by
  300 ms (`OnFilterInputAsync`) before issuing a request.
- **Server-side validation:** there is no FluentValidation validator for either
  endpoint. Validation is the grid contract itself, in `GridColumns<AiInvocation>`:
  an unknown sort key raises `ErrorCodes.GridSortKeyInvalid` (400) naming the
  sortable columns, an unknown filter key raises `ErrorCodes.GridFilterKeyInvalid`
  (400) naming the filterable ones, an unparseable `errorOnly` value raises the
  standard bilingual 400 through `GridFilters.ValueInvalid`, and an unparseable
  enum or date value likewise. `GridColumns` is explicit that a value that will
  not parse "is a 400, never a skipped predicate".
- **Detail 404:** an id with no row throws `ApiException(ErrorCodes.NotFound, 404,
  "AI invocation not found.", "لم يتم العثور على استدعاء الذكاء الاصطناعي.")`.
  Asserted by `AiHardeningTests.GetInvocationDetail_returns_404_for_unknown_id`.
- **Error envelope:** the standard `ApiResult<T>.Error` with `Code` and bilingual
  `Message` / `MessageArabic`. The page renders
  `env?.Error?.MessageForCurrentCulture()` so the admin sees the message in the
  active UI language.
- **Toast strategy:** error only. Both `LoadAsync` and `OnDetailAsync` fall back
  to `L["Admin.AiInvocations.LoadFailed"]` - "Could not load invocations." /
  "تعذّر تحميل سجلّ الاستدعاءات." - when the envelope carries no message. There
  is no success toast and no info toast, which is right for a read-only page.

## 7. Edge cases + known limitations

- **Unticking "Errors only" removes the key, it does not send `false`.**
  `OnErrorsOnlyChangedAsync` calls `_query.Filters.Remove("errorOnly")`. This is
  load-bearing, not tidiness: the server's `ErrorOnly` builder maps `"false"` to
  `invocation => invocation.ErrorCode == null`, so sending `false` would silently
  narrow the grid to successful calls only rather than restoring the full list.
- **The "Errors only" state survives grid interactions, and the page belts-and-braces
  it.** `OnQueryChangedAsync` re-applies (or removes) the `errorOnly` key on every
  query the grid hands back, and its comment states the grid "drops the errorOnly
  filter when it rebuilds the query". The grid's `CopyQuery`
  (`SimfDataGrid.razor:1024-1032`) does copy `Filters` into a new dictionary, and
  `OnFilterInputAsync` rebuilds its dictionary from `Query.Filters`, so on the
  current source the key would survive on its own. Either way the observable
  behaviour is the intended one - the toggle is combined with a column filter
  rather than lost - and the re-application keeps `_errorsOnly` and the query in
  step. The two statements have not been reconciled in code; treat the comment as
  possibly describing an earlier grid.
- **A stale error toast is not cleared by a later successful load.** `LoadAsync`
  assigns `_toast` only in its failure branch and never clears it on success, so
  a failed load followed by a successful reload leaves the red alert on screen
  above a correctly populated grid. `OnDetailAsync` does clear it (`_toast = null`
  first), so opening any Detail modal dismisses it.
- **The detail endpoint is rate-limited per admin, and the page can hit the cap.**
  `RequireRateLimiting("ai-test")` partitions a fixed window on the JWT `sub`
  claim; the defaults are `AiTestPermitLimit = 20` per `AiTestWindowSeconds = 3600`
  (`RateLimitOptions.cs:82-86`). An admin working through a page of rows one
  Detail at a time can exhaust that inside an hour, at which point the page shows
  the error toast. The endpoint comment states the intent plainly: so an admin
  "can't pull the whole invocation log one row at a time uncapped". A request
  with no `sub` falls into a tighter shared window (5 per minute).
- **The error pill is grey in the grid and red in the modal.** The grid renders
  `SimfPill Variant="off"`, whose class `.simf-pill--off` is
  `--color-surface-sunken` on `--color-text-muted`; the modal renders
  `Variant="danger"`, which is `--color-error-surface` on `--color-error`. The
  same fact is therefore muted in the scanning view and emphasised in the
  drill-down, which is the inverse of what a log reader scanning for failures
  would want. Recorded as observed, not fixed here.
- **A null `ErrorCode` renders an empty cell, not a "success" marker.** There is
  no `@else` branch. An empty Error column means the call succeeded.
- **Times are rendered with the invariant culture even in Arabic.** `FormatSaudi`
  is `value.ToString(format, CultureInfo.InvariantCulture)`, so the Time column
  keeps Latin digits and "AM"/"PM" under `dir="rtl"`. Deliberate per `SaudiTime`:
  one presentation contract, stable digits and separators. Note also that no
  timezone conversion happens - SIMF stores instants already on the Saudi wall
  clock, and converting again "would shift it by three hours, which is precisely
  the bug the conversion previously existed to prevent".
- **`CallerUserId` is never shown.** Both contracts carry it; neither surface
  renders it. Turning it into a name means a second query on the Identity
  database, since a cross-database join is forbidden.
- **The log is append-only and unbounded from the UI's side.** There is no purge,
  archive or retention control on this page, and no date-range picker - even
  though the service would accept a `createdAt` day filter. Reaching old rows is
  a paging exercise. `AiInvocationConfiguration` carries indexes on `CreatedAt`,
  `(Feature, CreatedAt)`, `(CallerUserId, CreatedAt)` and a filtered
  `(ErrorCode, CreatedAt) WHERE [ErrorCode] IS NOT NULL`, so the default sort and
  the errors-only filter are index-supported; a `promptKey` or `callerKind`
  substring filter is not.
- **`CallerKind` is constrained at the database, not just by convention.**
  `CK_AiInvocations_CallerKind` restricts it to `'Anonymous'`, `'Visitor'`,
  `'Staff'`, `'Admin'`, `'Moderator'`, "so a typo at any of those sites fails the
  insert rather than landing an unfilterable row in the telemetry log".
- **Both panes show what was stored, not what was sent or returned.** Input *and*
  output are redacted at write time, before persistence: `AiService` computes
  `redactedOutput = AiAuditDetail.RedactValue(providerResponse.OutputText)` and
  persists `InputJson = redacted.InputJson` alongside `OutputText = redactedOutput`
  (`AiService.cs:150-168`). Its comment gives the reason for the output half:
  "an LLM that echoes a user-pasted secret (or names a person verbatim from RAG
  context) would otherwise persist it." The detail endpoint's own XML comment
  still reads "output text is the LLM response verbatim and should be treated as
  restricted" - the "restricted" half holds, the "verbatim" half is stale. The
  commit that added output redaction (`19f067e65`) did not update it.
- **Orphaned resx pair.** `Admin.AiInvocations.Summary` exists in both
  `Strings.resx` and `Strings.ar.resx` with the same text as `Grid.Summary`, but
  the page formats through `Grid.Summary`. A repository-wide search for the key
  finds only the two resource files. It is dead weight, not a defect.

## 8. i18n + RTL

- Every visible string resolves through `IStringLocalizer<Strings> L`, injected
  in the code-behind. Page-specific keys are the `Admin.AiInvocations.*` family
  (19 keys, all present in both `Strings.resx` and `Strings.ar.resx`); shared
  grid furniture uses `Grid.FilterColumn`, `Grid.Prev`, `Grid.Next`, `Grid.First`,
  `Grid.Last`, `Grid.PageSize`, `Grid.FilterPlaceholder`, `Grid.Actions`,
  `Grid.SelectAll`, `Grid.SelectRow`, `Grid.Add`, `Grid.Edit`, `Grid.Delete`,
  `Grid.Summary`, `Grid.Page` and `Grid.Close`.
- There is **no hardcoded user-visible English word** on the page. The only
  literal strings in the markup are the `ms` latency suffix, the `/` between
  token counts, the `·` between provider and model, the `—` null placeholder,
  and the `SIMF` brand name in `<PageTitle>` - unit symbols, separators and a
  proper noun, none of which needs translating.
- RTL: the document is served `dir="rtl" lang="ar"` under the Arabic culture; the
  grid mirrors with it, and `SimfModal` is a plain flow container with no
  physical-direction offsets of its own. Enum values in the Feature and Provider
  columns render as **English identifiers** in both languages (`QuestionFilter`,
  `OpenAi`) because they are `.ToString()` of a CLR enum, not localised. Same for
  `CallerKind`, `Model` and `ErrorCode`.
- Time formatting stays invariant under Arabic - see section 7.

## 9. Accessibility

- **Grid semantics:** the `Caption` parameter renders
  `<caption class="simf-visually-hidden">` so a screen reader announces the table
  by name. Sortable headers carry
  `aria-sort="ascending|descending|none"`, and the sort glyph is
  `aria-hidden="true"` so the arrow is not read out. Per-column filter inputs get
  `aria-label="{FilterColumnLabel} {column.Header}"`, which is why the E2E
  catalogue can target "Filter column Prompt key".
- **Pager:** the current page button carries `aria-current="page"` and is
  `disabled`; First/Prev/Next/Last disable at the ends and while `Loading`. The
  loading overlay is `aria-busy="true"`.
- **Row action:** the Detail button is a `SimfToolbarButton` with
  `Title="@L["Admin.AiInvocations.Detail"]"`, so it has an accessible name rather
  than an icon alone.
- **Modal:** `role="dialog"`, `aria-modal="true"`, `aria-label="@Title"`,
  `tabindex="-1"` on the panel, with the focus trap, return-focus-on-close and
  ref-counted body scroll lock handled by `window.simfModal`. The close button
  has `aria-label="@CloseLabel"`. Backdrop click closes. `SimfModal` is
  explicitly **not stackable**, which this page does not exercise - it opens at
  most one modal, over the grid.
- **Colour contrast / focus:** inherited from `theme.tokens.css` and the
  `--focus-ring` token; not independently measured for this page. The grey error
  pill (section 7) uses `--color-text-muted`, which is the lowest-contrast text
  token in use on the page.
- **Not verified this session:** keyboard tab order through the grid toolbar into
  the row actions, and whether focus returns to the originating Detail button
  after close in practice (the mechanism exists in `simfModal`; it was not
  exercised).

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| N/A | - | No use case in `SIMF-UCS-001` covers reading the AI invocations log. Section 4.3 "Control Panel" runs UC-20 to UC-32 and contains no AI-telemetry or audit-read case. |
| UC-29 | Manage the FAQ knowledge and AI settings (Technical team, FR-806) | The nearest neighbour, and adjacent rather than matching: UC-29 is about configuring AI, this page is about reading what AI did. The prompt catalogue at `/admin/ai/prompts` is the UC-29 surface. |
| UC-17 | Use the AI assistant (Visitor / Guest, FR-805, FR-806) | The visitor-side use case whose calls *produce* the rows this page reads. Not an actor on this page. |

The gap is real and should be closed on the UCS side rather than papered over
here: the page exists to serve a SOC / product regression-hunting job that the
use-case catalogue never wrote down.

## 11. Related E2E test scenarios

Catalogue: [`docs/tests/e2e/cp-admin-ai-invocations.md`](../../tests/e2e/cp-admin-ai-invocations.md),
scenarios `E2E-AIV-001` to `E2E-AIV-012` plus `E2E-AIV-ELS-001/002`.

| Scenario | Coverage | Status against this source |
|----------|----------|----------------------------|
| `E2E-AIV-001` | Golden path - load, newest-first, 8 columns, summary footer | Valid, except the asserted time format. See drift note 2. |
| `E2E-AIV-002` | "Errors only" on -> error rows; off -> full list | Valid, and correctly asserts an empty `filters` object when unticked. |
| `E2E-AIV-003` | Error rows render the pill with the `ErrorCode` | Valid on the element; the asserted colour is wrong. See drift note 3. |
| `E2E-AIV-004` | Empty list renders `SimfEmptyState` | Valid. |
| `E2E-AIV-005` | Filter result empty -> empty state, no error toast | Valid. |
| `E2E-AIV-006` | Auth gate - admin lacking `AiInvocations.View` -> `/not-permitted` | Valid. |
| `E2E-AIV-007` | 20 rows per page, pager advances `Skip`, summary `1–20 of {Total}` | Valid (`new GridQuery { Top = 20 }`). |
| `E2E-AIV-008` | Read-only surface - **no per-row actions** | **Wrong.** See drift note 1. |
| `E2E-AIV-009` | Server 500 on `/list` -> bilingual fallback toast, no rows | Valid. |
| `E2E-AIV-010` | RTL / Arabic render | Valid; does not cover the invariant time format (section 8). |
| `E2E-AIV-011` | Per-column filter on Prompt key / Caller, and coexistence with the toolbar toggle | Valid. Its implementation note lists only `feature` as an unsurfaced server filter; `createdAt`, `provider` and `latencyMs` are also accepted (section 4.3). |
| `E2E-AIV-012` | Column sort toggles ascending / descending | Valid. |
| `E2E-AIV-ELS-001/002` | Element inventory and element health, LTR + RTL | Valid, but the inventory baseline must now include the Actions column and the Detail button. |

**Drift - the catalogue was last reviewed 2026-06-03 and the page changed on
2026-06-22 (commit `dc8497998`). Four claims in it are now false:**

1. Its page-shape preamble says the grid "has no `<RowActions>`" and that the
   detail endpoint is "**not** wired to any UI element on this page - do not
   author a 'click row → detail' scenario". Both are now untrue: `<RowActions>`
   exists and hosts the Detail button, and the modal is real. `E2E-AIV-008`
   asserts the absence directly and would fail against the built page.
2. `E2E-AIV-001` asserts the first row's Time renders as `"yyyy-MM-dd HH:mm:ss UTC"`.
   The page renders `FormatSaudi("dd-MM-yyyy hh:mm:ss tt")` - day-first, 12-hour,
   AM/PM, no "UTC" suffix.
3. `E2E-AIV-003` and the preamble describe "a red `SimfPill Variant="off"`".
   `Variant="off"` resolves to `.simf-pill--off`, which is muted grey; red is
   `Variant="danger"`, used only inside the modal.
4. `E2E-AIV-001`'s evidence note says "Audit row: **none**". That remains true for
   the list, but a Detail click now writes an `AiInvocation.Viewed` row, so the
   page as a whole can produce audit writes.

Additionally, the catalogue's implementation notes cite
`tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` as asserting the gate on
`POST /admin/ai/invocations/list`. There is **no route-named assertion** for it
in that file. The coverage is real but generic:
`Every_admin_endpoint_is_permission_and_approval_gated` enumerates every mapped
`/admin/*` route from the `EndpointDataSource` and fails the build if any is not
both permission-gated and approval-gated, so these two endpoints are covered by
enumeration rather than by name.

Lower-layer tests that do name this surface:

| Test | Asserts |
|------|---------|
| `AiModuleTests.Admin_invocations_log_lists_recent_calls` | `POST /api/v1/admin/ai/invocations/list` with `Top = 50` returns 200 and a non-empty page after one FAQ call. |
| `AiHardeningTests` (drill-down) | `GET /api/v1/admin/ai/invocations/{id}` returns 200, echoes the id, and its `InputJson` contains the submitted text with a non-null `OutputText`. |
| `AiHardeningTests.GetInvocationDetail_returns_404_for_unknown_id` | Unknown id returns 404 with `ErrorCodes.NotFound`. |
| `AiHardeningTests.AiInvocationSucceeded_audit_is_valid_json` | The `AiInvocation.Succeeded` audit detail parses as JSON. |
| `CpNavigationPermissionTests.Every_nav_gate_matches_the_gate_on_the_page_it_links_to` | The `Module.AiInvocations` nav gate equals the page's `[RequirePermission]` code. |

**No page-level bUnit test exists for `AiInvocationsLog`.** A search of
`tests/SIMF.ControlPanel.Tests/` finds no source file referencing it. The sibling
dashboard has `AiDashboardTests.cs`; this page has no equivalent, so the errors-only
toggle, the toast fallback and the Detail modal are unexercised at the component
layer.

## 12. Related docs

- Sibling AI pages: [`admin-ai-dashboard.md`](admin-ai-dashboard.md) (`/admin/ai`,
  the 24h roll-up that this page is the drill-down for),
  [`admin-ai-services.md`](admin-ai-services.md),
  [`admin-ai-service-detail.md`](admin-ai-service-detail.md),
  [`admin-ai-prompts.md`](admin-ai-prompts.md) (the catalogue whose `Key` values
  appear in this page's Prompt key column).
- Page index: [`PAGE-INDEX.md`](../PAGE-INDEX.md) - the route is listed as
  "✅ Real (D-176/D-179)" with no reference-doc link at the time of writing.
- E2E catalogue: [`cp-admin-ai-invocations.md`](../../tests/e2e/cp-admin-ai-invocations.md).
- Component catalogue: [`SIMF-CMP-001`](../../SIMF-CMP-001-Component-Catalog.md) -
  components used here are `SimfBanner`, `SimfAlert`, `SimfDataGrid`,
  `SimfDataGridColumn`, `SimfCheckbox`, `SimfPill`, `SimfToolbarButton`,
  `SimfEmptyState`, `SimfModal`, `SimfButton`, plus the CP-local
  `AuthorizedAction` (a thin wrapper over the shared `SimfActionGate`).
- Access control: `docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md` and
  [`SIMF-Permission-Catalogue.md`](../../SIMF-Permission-Catalogue.md).
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) - the
  `ApiResult<T>` envelope and error model this page consumes.
- Decisions: `docs/decisions/DECISIONS_LOG.md` carries a row for **D-176** (the
  AI module and both endpoint families) and **D-256** (the `SimfDataGrid`
  migration). It carries **no row for D-177 or D-179** - the log runs D-184 then
  jumps to D-176, and neither id appears there as a row or as a lettered
  variant. Both survive only in their commit messages: **D-179** in `19f067e65`
  ("G12 backend hardening"), which is where the drill-down endpoint, its
  read-audit, its rate limit and the write-time input + output redaction are
  written down, and **D-177** in `9d50f8199` ("G12-CP AI module Control Panel
  UI"), which is where this page is. That shape is expected, not a gap: the
  log's own "Reading an ID" preamble says to read a missing row as "not recorded
  in this log", never as "did not happen".
- **No per-page CP documentation set** exists for this route: there is no
  `docs/CP/admin-ai-invocations/` directory alongside the ones for halls,
  sessions, booths and the rest.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-30 | D-176 | AI module shipped (G12). `AiInvocation` append-only telemetry entity, the `AiInvocations` table with its four indexes, and `POST /admin/ai/invocations/list`. The decision explicitly deferred the CP Razor pages to "a polish pass". |
| 2026-05-30 | D-179 (commit `19f067e65`; no row in the decisions log) | SOC drill-down `GET /admin/ai/invocations/{id}` returning the full redacted payload, with the `AiInvocation.Viewed` read-audit and the `"ai-test"` per-admin rate limit added in the same review pass. Write-time redaction of `InputJson` and `OutputText`. |
| 2026-05-30 | D-177 (commit `9d50f8199`; no row in the decisions log) | The Control Panel page `AiInvocationsLog.razor` - read-only append-only log, "Errors only" filter over the existing `errorOnly` query filter, columns time / prompt key / feature / provider / caller kind / latency / tokens in-out / error code. |
| 2026-06-03 | D-256 | Grid-standard rollout batch 2 converted this page from a raw `simf-table` to `SimfDataGrid`: per-column sort and filter, full numbered pager, `SimfEmptyState`, and the "Errors only" checkbox re-homed into `<CustomToolbar>`. |
| 2026-06-22 | commit `dc8497998` (no decision id) | **Detail row action + modal.** A `<RowActions>` eye button, wrapped in `<AuthorizedAction Permission="AiInvocations.View">`, opens a `SimfModal` over the full redacted payload from the D-179 endpoint - a dataset that already existed in the API and contract but had no UI. New BFF route `GET /account/api/admin/ai/invocations/{id:guid}` and `SimfAdminClient.GetAiInvocationAsync`. No new permission, endpoint or migration. Bilingual resx added (`Admin.AiInvocations.Detail*`). The E2E catalogue was not updated with it, which is the drift recorded in section 11. |
| 2026-08-19 | - | This reference doc authored from source. |

---

_Last reviewed:_ `2026-08-19` by Claude (first authoring, from source). If the
page has changed and this doc has not been re-reviewed in 60 days, it is **out of
date**. Re-walk the page in a browser and update every section that drifted -
and update the E2E catalogue in the same pass, since its four stale claims
(section 11) are what this authoring found.
