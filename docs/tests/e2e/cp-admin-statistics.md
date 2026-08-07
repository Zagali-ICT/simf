# E2E test catalogue — Statistics dashboard (`/admin/statistics`)

| | |
|--|--|
| **Page** | [`cp/admin-statistics.md`](../../pages/cp/admin-statistics.md) |
| **Route** | `/admin/statistics` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **What this page is.** The Statistics dashboard (`StatisticsDashboard.razor`,
> D-202 Track-2) is a **read-only overview** of live event counts aggregated across
> the two DbContexts. On load it fires a single call —
> `GET /account/api/admin/statistics` (the CP BFF passthrough) → `ApiResult<StatisticsDashboard>`
> — and renders **14 `SimfStatCard` tiles**. It has **no create / edit / delete, no
> forms, no grids, no filters, no modals, and no toolbar actions**; every value is a
> live aggregate computed on demand by the backend (each metric is its own
> `COUNT` / `AVG` query, `AsNoTracking`). The page is gated by
> `@attribute [RequirePermission(PermissionCatalog.Statistics.View)]` (code
> `"Statistics.View"`, baseline `AdminOnly`), so the auth scenario is a real
> signed-in-but-unprivileged user landing on `/not-permitted`. The "functions" under
> test are therefore: the **golden load + render of all 14 tiles**, the three render
> branches the `@code` block exposes (**loading** `Admin.Statistics.Loading`,
> **null/none** `SimfEmptyState` with `Admin.Statistics.None`, **error** `SimfAlert`
> with `Admin.Statistics.LoadFailed` or the server message), the **average-rating
> formatting** (`"0.0"` invariant culture), the **permission gate**, **resilience**
> when the API 500s or returns `Success: false`, and **RTL/Arabic** render.

## The 14 stat tiles (render order)

The page renders these tiles in order, each `SimfStatCard Title=<resx> Value=<count>`.
The `Value` for every count is `int.ToString(InvariantCulture)`; `AverageRating` is
formatted `"0.0"` invariant.

| # | Contract field | Title resx (en) | Title resx (ar) | Backend source |
|---|----------------|-----------------|-----------------|----------------|
| 1 | `TotalAttendees` | Total attendees | إجمالي الحضور | Identity `Users` where `UserType==Visitor` |
| 2 | `ApprovedAttendees` | Approved attendees | الحضور المعتمدون | + `AccountState==Approved` |
| 3 | `PendingApprovals` | Pending approvals | الموافقات المعلقة | + `AccountState==PendingApproval` |
| 4 | `Sessions` | Sessions | الجلسات | App `Sessions` where `IsActive` |
| 5 | `Speakers` | Speakers | المتحدثون | App `Speakers` where `IsActive` |
| 6 | `Booths` | Booths | الأجنحة | App `Booths` where `IsActive` |
| 7 | `Sponsors` | Sponsors | الرعاة | App `Sponsors` where `IsActive` |
| 8 | `NewsArticles` | News articles | الأخبار | App `News` where `IsActive` |
| 9 | `MediaItems` | Media items | عناصر الوسائط | App `MediaItems` where `IsActive` |
| 10 | `Delegations` | Delegations | الوفود | App `Delegations` where `IsActive` |
| 11 | `RatingsCount` | Total ratings | إجمالي التقييمات | App `Ratings` where `IsActive` |
| 12 | `AverageRating` | Average rating | متوسط التقييم | `AVG(Stars)` over active `Ratings`, null → `0.0` |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-STA-001 | Golden path — admin loads `/admin/statistics`, all 14 tiles render with live counts | happy | P0 | _to author_ |
| E2E-STA-002 | Loading indicator shows `Admin.Statistics.Loading` while the call is in flight | function | P2 | _to author_ |
| E2E-STA-003 | Empty / none — API returns `Success:true` but `Data:null` → `SimfEmptyState` (`Admin.Statistics.None`) | happy | P1 | _to author_ |
| E2E-STA-004 | Zero-data event — all counts 0 and average "0.0" still render (tiles, not empty state) | happy | P1 | _to author_ |
| E2E-STA-005 | Auth gate — signed-in admin lacking `Statistics.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-STA-006 | Unauthenticated visitor → redirected to `/login` | auth | P0 | _to author_ |
| E2E-STA-007 | Average-rating formatting — `"0.0"` invariant (e.g. `4.5`, `0.0`, never `4,5`) | function | P1 | _to author_ |
| E2E-STA-008 | Server 500 on `/admin/statistics` → red `SimfAlert` fallback (`Admin.Statistics.LoadFailed`) | resilience | P2 | _to author_ |
| E2E-STA-009 | `Success:false` with server error → red `SimfAlert` shows the bilingual server message | error | P1 | _to author_ |
| E2E-STA-010 | Counts reflect live state — approve a pending attendee, reload, the two tiles move | happy | P1 | _to author_ |
| E2E-STA-011 | Read-only surface — no buttons / forms / grids / modals on the page | function | P2 | _to author_ |
| E2E-STA-012 | RTL / Arabic render — banner, tiles and nav rail mirror; Arabic titles | i18n | P1 | _to author_ |
| E2E-STA-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-STA-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-STA-001 — Golden path (load + render all 14 tiles)

```gherkin
Feature: Statistics dashboard overview
  As an Administrator
  I want a live overview of the event's headline counts
  So that I can read the platform's current state at a glance

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And the account superadmin@simrsnf.com is Approved with a paired TOTP authenticator
  And an Administrator has signed in via /login + /login/totp
  And they have landed on /admin/statistics

Scenario: All fourteen stat tiles render with live counts
  Given the database has live data across the event modules
  When the page initialises
  Then it fires exactly one GET /account/api/admin/statistics
  And the BFF forwards it to GET /api/v1/admin/statistics on the API
  And the API returns HTTP 200 with ApiResult.Success = true and a StatisticsDashboard payload
  And the browser tab title is "Statistics · SIMF"
  And the SimfBanner title reads "Statistics"
  And the surface renders 14 SimfStatCard tiles in this order:
    | Title              | bound field        |
    | Total attendees    | TotalAttendees     |
    | Approved attendees | ApprovedAttendees  |
    | Pending approvals  | PendingApprovals   |
    | Sessions           | Sessions           |
    | Speakers           | Speakers           |
    | Booths             | Booths             |
    | Sponsors           | Sponsors           |
    | News articles      | NewsArticles       |
    | Media items        | MediaItems         |
    | Delegations        | Delegations        |
    | Total ratings      | RatingsCount       |
    | Average rating     | AverageRating      |
  And each count tile shows the integer value from the payload (invariant culture, no thousands separator)
  And the "Average rating" tile shows the value formatted as "0.0" (e.g. "4.5")
  And no error SimfAlert is shown
  And the SimfEmptyState is NOT rendered
```

**Evidence captured:**
- Screenshot before (loading): `docs/screenshots/cp-admin-statistics-001-loading.png`
- Screenshot after (14 tiles): `docs/screenshots/cp-admin-statistics-001-after.png`
- Console errors: 0 expected
- Network: the single `GET /account/api/admin/statistics` call returns 200; no other data call fires from this page (the shell avatar/notification calls are out of scope)
- Audit row: none — reading the dashboard is a pure aggregate read and is **not** an audited operation (no `OperationLog` / `RowAudit` row expected)

### E2E-STA-002 — Loading indicator

```gherkin
Scenario: The loading copy shows while the statistics call is in flight
  Given the GET /account/api/admin/statistics response is artificially delayed
  When the administrator opens /admin/statistics
  Then while _loading is true and _dashboard is null the surface shows the paragraph "Loading statistics…"
  And neither the tiles nor the SimfEmptyState are rendered yet
  When the response arrives with Success:true and a payload
  Then the loading paragraph is replaced by the 14 tiles
```

### E2E-STA-003 — Empty / none branch (Data null)

```gherkin
Scenario: A successful response with a null payload shows the empty state
  Given the API returns HTTP 200 with ApiResult.Success = true and Data = null
  When the administrator opens /admin/statistics
  Then the page binds _dashboard = null (the Data-not-null guard fails)
  And the surface renders the SimfEmptyState component
  And the empty state title reads "No statistics are available yet." / "لا توجد إحصائيات متاحة بعد."
  And no tiles render
  And no error SimfAlert appears
```

### E2E-STA-004 — Zero-data event (all counts 0)

```gherkin
Scenario: A fresh event with no data still renders tiles (not the empty state)
  Given the event has no attendees, sessions, speakers, booths, sponsors, news, media, delegations or ratings
  And the API returns Success:true with every count = 0 and AverageRating = 0
  When the administrator opens /admin/statistics
  Then all 14 SimfStatCard tiles still render (the payload is non-null)
  And every count tile shows "0"
  And the "Average rating" tile shows "0.0"
  And the SimfEmptyState is NOT shown (it only appears when the payload itself is null)
```

### E2E-STA-005 — Auth gate (admin lacking `Statistics.View`)

```gherkin
Scenario: A signed-in admin without the Statistics.View permission is denied
  Given a signed-in, Approved admin whose role does NOT include "Statistics.View"
  And whose token does NOT carry the "*" wildcard
  When they navigate to /admin/statistics
  Then the RequirePermission(PermissionCatalog.Statistics.View) attribute denies access
  And they land on /not-permitted with HTTP 200
  And no GET /account/api/admin/statistics request fires
  And the "Statistics" item is also hidden from their side nav rail (RequiredPermission = Statistics.View)
```

### E2E-STA-006 — Auth gate (unauthenticated)

```gherkin
Scenario: An unauthenticated visitor cannot see the statistics dashboard
  Given no SIMF auth cookie is present in the browser
  When the visitor navigates to http://localhost:5158/admin/statistics
  Then they are redirected to /login
  And no banner, tiles or statistics call render
```

### E2E-STA-007 — Average-rating formatting (invariant culture)

```gherkin
Scenario: Average rating renders with the invariant "0.0" format regardless of locale
  Given the API returns AverageRating = 4.5
  When the administrator opens /admin/statistics in the en culture
  Then the "Average rating" tile shows "4.5" (dot decimal separator)
  When the same administrator switches to the ar culture and reloads
  Then the "Average rating" tile STILL shows "4.5" using a dot — never "4,5" or Arabic-Indic digits
  And a returned AverageRating of exactly 0 renders as "0.0", and 3 renders as "3.0"
```

### E2E-STA-008 — Server 500 fallback

```gherkin
Scenario: A 500 from the statistics endpoint shows the bilingual fallback alert
  Given the API is configured to return HTTP 500 on /api/v1/admin/statistics (e.g. DB unreachable)
  When the administrator opens /admin/statistics
  Then the loading paragraph shows first
  And when the failed envelope returns (Success:false / no Data) the surface shows a red SimfAlert
  And the alert message is the server Error.MessageForCurrentCulture() if present,
      otherwise the fallback "Could not load statistics. Please try again." / "تعذر تحميل الإحصائيات. حاول مرة أخرى."
  And no tiles render
  And the page does not crash to the unhandled-exception page
```

### E2E-STA-009 — Success:false with server error message

```gherkin
Scenario: A Success:false envelope surfaces the server's bilingual error
  Given the API returns HTTP 200 (or 4xx) with ApiResult.Success = false and an Error carrying a bilingual message
  When the administrator opens /admin/statistics
  Then the page takes the else branch (_dashboard stays null)
  And a red SimfAlert renders with env.Error.MessageForCurrentCulture()
  And no tiles render
  And the SimfEmptyState is NOT shown (the error alert takes precedence over the none-state because _toast is set and _dashboard is null → the alert plus the empty-state both follow the null branch; the alert is what communicates the failure)
```

### E2E-STA-010 — Counts reflect live state (round-trip)

```gherkin
Scenario: Approving a pending attendee moves the two attendee tiles on reload
  Given the dashboard shows "Pending approvals" = P and "Approved attendees" = A
  When an administrator approves one pending attendee via the Attendees page
  And then reloads /admin/statistics
  Then the page recomputes the aggregates (there is no cache / snapshot table)
  And "Pending approvals" now reads P - 1
  And "Approved attendees" now reads A + 1
  And "Total attendees" is unchanged (the account was already counted)
```

### E2E-STA-011 — Read-only surface (no actions)

```gherkin
Scenario: The page exposes no write actions
  Given an administrator has landed on /admin/statistics with the 14 tiles rendered
  Then there are no "Add" / "Edit" / "Delete" / "Save" buttons anywhere on the page
  And there are no form fields, grids, filters, toggles or modals
  And the page makes no POST / PUT / DELETE request
  And the only network call attributable to the page is the single GET /account/api/admin/statistics
```

### E2E-STA-012 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the statistics dashboard
  Given an administrator is on /admin/statistics in English with the 14 tiles rendered
  When they click the "العربية" language switch in the top bar
  Then the page reloads with <html dir="rtl" lang="ar">
  And the browser tab title is "الإحصائيات · SIMF"
  And the SimfBanner title reads "الإحصائيات"
  And the tile titles read their Arabic strings, e.g.
    "إجمالي الحضور", "الحضور المعتمدون", "الموافقات المعلقة", "الجلسات",
    "المتحدثون", "الأجنحة", "الرعاة", "الأخبار", "عناصر الوسائط", "الوفود",
    "التعليقات المعتمدة", "التعليقات المعلقة", "إجمالي التقييمات", "متوسط التقييم"
  And the tile grid flows right-to-left
  And the side nav rail mirrors to the right with Arabic labels
  And the "Average rating" value remains the invariant "0.0" format (Latin digits, dot separator)
  And there are 0 console errors
```

**Evidence captured (RTL):**
- Screenshot: `docs/screenshots/cp-admin-statistics-012-rtl.png`
- Console errors: 0 expected

---

## Implementation notes

- **Read-only, single call.** The page (`StatisticsDashboard.razor`) only calls
  `GET /account/api/admin/statistics` via the `simfAccount.getJson` JS interop and
  renders `SIMF.Contracts.Statistics.StatisticsDashboard` into 14 `SimfStatCard`
  tiles. There is no CRUD surface, so most error families collapse to the three
  render branches in the `@code` block: `_loading`, `_dashboard is null`
  (`SimfEmptyState`), and the `_toast` error `SimfAlert`.
- **BFF → API chain.** CP route `GET /account/api/admin/statistics`
  (`AccountEndpoints.cs`, group `MapGroup("/account/api")`) forwards to
  `SimfAdminClient.GetStatisticsAsync` → API `GET /api/v1/admin/statistics`
  (`GetStatisticsDashboardEndpoint`, gated
  `Policies(PolicyFor(Statistics.View), RequireApprovedAccount)`).
- **Permission.** `PermissionCatalog.Statistics.View` (`"Statistics.View"`,
  baseline `AdminOnly`) gates **both** the API endpoint and the CP page
  (`@attribute [RequirePermission(...)]`), and is the nav item's `RequiredPermission`
  (`new("Module.Statistics", "/admin/statistics", RequiredPermission: PermissionCatalog.Statistics.View)`).
  The nav-permission unit test
  `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs` and the API gate
  test `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` fail the build if a
  gate is missing.
- **Lower-layer coverage.** The endpoint and service both carry a
  `// Tests: SIMF.Api.Tests/StatisticsTests.cs` header, **but that file does not
  exist in the repo as of this review** (no `tests/**/Statistics*.cs` and no test
  references `GetDashboardAsync` / `StatisticsService` / `GetStatisticsDashboardEndpoint`).
  This is a coverage gap to flag: the per-metric `COUNT` / `AVG` aggregation logic
  in `StatisticsService` (Visitor-only attendee counts, `IsActive` filters, the
  nullable-cast `AverageAsync ?? 0`) has no automated test, so until that file is
  authored the E2E golden path (E2E-STA-001) + zero-data (E2E-STA-004) + round-trip
  (E2E-STA-010) are the only checks that the numbers are correct.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin scenario
  into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) plus
  a step-definition class. The Gherkin shape is already runner-agnostic.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
