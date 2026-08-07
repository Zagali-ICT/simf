# E2E test catalogue - Dashboard (`/`)

| | |
|--|--|
| **Page** | [`cp/dashboard.md`](../../pages/cp/dashboard.md) |
| **Route** | `/` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later - keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-07-29 |

> **What this page is.** The Dashboard is the **post-sign-in landing page**
> (`Home.razor` + `Home.razor.cs`). It has **two halves, gated differently**.
>
> **Half one - the welcome panel.** A `SimfBanner` plus a surface card holding
> the `Dashboard.Welcome` heading and the `Dashboard.Intro` paragraph. The page
> guard is `@attribute [Authorize]` only, and the nav item
> `new("Module.Dashboard", "/")` carries **no `RequiredPermission`**, so every
> signed-in, **Approved** user lands here and sees this half - an Administrator
> via the `*` wildcard, and equally a least-privilege admin holding zero
> permission codes. Because the page itself is ungated, the auth scenarios here
> are **not** `/not-permitted` cases; they are the unauthenticated redirect to
> `/login` plus the `CpShellLayout` account-state guards (PendingApproval to
> `/auth/pending`, Rejected to `/auth/rejected`).
>
> **Half two - the programme dashboard (Wave A).** Rendered only when the signed-in
> admin passes `PermissionCatalog.Statistics.View`, resolved in
> `OnInitializedAsync` through `IAuthorizationService` into the `_canViewStats`
> flag. It is three stacked blocks: a **KPI stat grid** of plain numbers
> (`SimfStatCard`), a **grouped bar chart** (`SimfGroupedBarChart`) with one
> cluster per forum day, and one **day card** per forum day carrying three
> `SimfBarGauge` bars plus the day's session count as a number. Two read-only
> API calls feed it, both tolerant of failure:
> `GET /account/api/admin/statistics` (`StatisticsDashboard`) and
> `GET /account/api/admin/statistics/programme` (`StatisticsProgramme`).
> No writes, no forms, no grids, no modals, no schema change, no new permission.
>
> **The design rules this page is asserted against.** Sessions-per-day is a
> different unit from the three people-metrics, so it is a **number on the day
> card and never a fourth bar** sharing their axis. Bars are anchored to a
> **zero baseline** (enforced in `ChartGeometry`, not left to the caller). Series
> identity is never carried by colour alone - there is a legend, a direct value
> label above every bar, and a visually-hidden data table. All chart text is HTML
> around the SVG, so Arabic mirrors as a CSS flip rather than a transform that
> would also mirror the lettering. Colours come only from
> `theme.tokens.css` (`--chart-series-1..3`), so the chart inherits light, dark
> and grey without naming a colour.
>
> The "functions" under test are therefore: the shell chrome
> (`CpShellLayout` + `SimfAppShell` banner, welcome card, permission-filtered nav
> rail, language switch, theme toggle, notification bell, profile link,
> sign-out), **and** the permission-gated programme dashboard.

## Fixture used by the Wave A scenarios (E2E-DSH-014 onward)

Every programme scenario below reads against this seed, so the numbers quoted
are the numbers the page must show. Three **active** `ProgrammeDay` rows,
`DisplayOrder` 1, 2, 3:

| Day | `Date` | `Title` | `TitleArabic` | Registered | Present | Sessions | Attended |
|-----|--------|---------|---------------|-----------:|--------:|---------:|---------:|
| 1 | `2026-11-17` | Day One | اليوم الأول | 420 | 310 | 6 | 275 |
| 2 | `2026-11-18` | Day Two | اليوم الثاني | 180 | 480 | 8 | 431 |
| 3 | `2026-11-19` | Day Three | اليوم الثالث | 40 | 0 | 0 | 0 |

Derived values the assertions depend on:

- Data maximum **480**, so `ChartGeometry.NiceMax(480)` gives an axis maximum of
  **500** and `AxisTicks(500)` gives the ticks **0, 125, 250, 375, 500**.
- The same 500 is `_gaugeMax`, so the day-card gauges share one scale across all
  three cards.
- The plot viewBox is `0 0 640 260`. With 3 groups and 3 series the bar width is
  **56.98** and the first bar of Day One sits at **x = 19.2** in LTR and at
  **x = 563.82** in RTL (`x = plotWidth - x - barWidth`).
- Day Three is the deliberate **zero row**: it is active, so it must still appear
  as a cluster, a card and a table row.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-DSH-001 | Golden path - Approved admin signs in (login + TOTP) and lands on `/` with banner + welcome card | happy | P0 | _to author_ |
| E2E-DSH-002 | Welcome card renders the bilingual `Dashboard.Welcome` + `Dashboard.Intro` copy | happy | P1 | _to author_ |
| E2E-DSH-003 | Side nav rail renders, is permission-filtered, and the Dashboard item is always visible | nav | P0 | _to author_ |
| E2E-DSH-004 | Least-privilege admin (zero permission codes) still lands on `/` (ungated) | auth | P0 | _to author_ |
| E2E-DSH-005 | Theme toggle (light to dark) persists on the Dashboard | function | P1 | _to author_ |
| E2E-DSH-006 | Notification bell opens dropdown / shows empty state on the Dashboard | function | P1 | _to author_ |
| E2E-DSH-007 | Profile link + avatar prefetch (`/account/api/profile`) in the top bar | function | P2 | _to author_ |
| E2E-DSH-008 | Sign-out from the Dashboard returns to `/login` | function | P1 | _to author_ |
| E2E-DSH-009 | Unauthenticated visitor is redirected to `/login` (the auth gate) | auth | P0 | _to author_ |
| E2E-DSH-010 | PendingApproval account - shell guard redirects to `/auth/pending` | auth | P1 | _to author_ |
| E2E-DSH-011 | Rejected account - shell guard redirects to `/auth/rejected` | auth | P1 | _to author_ |
| E2E-DSH-012 | Resilience - avatar prefetch `/account/api/profile` fails, placeholder icon, page still renders | resilience | P2 | _to author_ |
| E2E-DSH-013 | RTL / Arabic render - banner + welcome card + nav rail mirror | i18n | P1 | _to author_ |
| E2E-DSH-014 | Wave A golden path - an admin holding `Statistics.View` gets welcome + KPI grid + chart + day cards | happy | P0 | _to author_ |
| E2E-DSH-015 | Permission gate - an admin **without** `Statistics.View` sees the welcome panel only, and fires no statistics call | auth | P0 | authored ✓ (API `Non_admin_caller_is_forbidden`, `Anonymous_caller_is_rejected`) |
| E2E-DSH-016 | KPI stat grid renders every tile with the live number and links each tile to its module page | happy | P0 | authored ✓ (API `Headline_counts_are_present_and_non_negative`, `Staff_are_counted_from_the_profile_types_mobile_app_role`, `Exhibitor_accounts_are_counted_separately_from_exhibitor_companies`) |
| E2E-DSH-017 | Grouped bar chart renders one cluster per programme day, three bars per cluster, on a zero baseline | happy | P0 | authored ✓ (`ChartGeometryTests.Bars_are_anchored_to_the_zero_baseline`, `Bars_within_a_group_never_overlap`, `Groups_stay_inside_the_plot_and_do_not_collide`) |
| E2E-DSH-018 | A forum day with no activity still renders as a zero row (cluster, card and table row all present) | edge | P0 | authored ✓ (API `Every_active_day_is_returned_in_display_order_even_with_no_activity`; `ChartGeometryTests.An_all_zero_dataset_draws_flat_bars_rather_than_dividing_by_zero`) |
| E2E-DSH-019 | Arabic RTL - the plot mirrors, the lettering does not, and the first day sits at the right | i18n | P0 | authored ✓ (`ChartGeometryTests.Rtl_mirrors_the_plot_horizontally`, `Rtl_puts_the_first_group_on_the_right`, `Rtl_bars_stay_inside_the_plot`) |
| E2E-DSH-020 | Chart renders correctly in light, dark and grey - every colour resolves from `theme.tokens.css` | i18n | P1 | _to author_ |
| E2E-DSH-021 | Every displayed date reads `dd-MM-yyyy` Saudi local, and no UTC value reaches the DOM | function | P0 | authored ✓ (API `A_record_late_in_the_saudi_evening_stays_on_that_saudi_day`, `A_record_just_after_midnight_saudi_counts_on_that_saudi_day`, `A_record_at_the_exact_start_of_a_saudi_day_counts_on_that_day`) |
| E2E-DSH-022 | Resilience - the statistics calls fail, the welcome panel stays intact and the page does not crash | resilience | P0 | _to author_ |
| E2E-DSH-023 | The visually-hidden data table exposes the same numbers to screen readers | a11y | P0 | _to author_ |
| E2E-DSH-024 | Day cards - three gauges on one shared scale, and sessions as a number rather than a fourth bar | happy | P1 | authored ✓ (`ChartGeometryTests.GaugeFraction_clamps_between_zero_and_one`, `GaugeFraction_never_returns_NaN`) |
| E2E-DSH-025 | Empty state - no programme days configured, the chart shows `Dashboard.Programme.None` and no day cards render | happy | P1 | authored ✓ (`ChartGeometryTests.No_groups_produces_no_bars`, `NiceMax_falls_back_to_one_for_unusable_input`) |
| E2E-DSH-026 | Shell head - the tab carries the SIMF icon and no CP page requests `/favicon.ico` | element | P1 | authored ✓ (`CpHeadAssetsTests.The_shell_head_declares_a_favicon`, `Every_local_asset_the_shell_head_references_exists_on_disk`) |
| E2E-DSH-ELS-001 | Element inventory - every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-DSH-ELS-002 | Element health - no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-DSH-001 - Golden path (sign in to land on Dashboard)

```gherkin
Feature: Dashboard landing
  As an Administrator
  I want to be taken to the Dashboard after I authenticate
  So that I have a stable, branded home base for the Control Panel

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And the account superadmin@zagali-ict.com is Approved with a paired TOTP authenticator

Scenario: Approved admin lands on the Dashboard after login + TOTP
  Given the administrator opens http://localhost:5158/
  And they are not yet signed in
  Then they are redirected to /login
  When they enter email "superadmin@zagali-ict.com" and the account password
  And they submit the sign-in form
  Then they are taken to /login/totp
  When they enter the current 6-digit TOTP code from the Get-Totp helper
  And they submit
  Then they are redirected to / (the Dashboard)
  And the browser tab title is "Dashboard · SIMF"
  And the SimfBanner title reads "Dashboard"
  And the welcome card heading reads "Welcome to the SIMF Control Panel"
  And the intro paragraph reads "The operations console for the Saudi International Maritime Forum. Statistics and live attendance figures appear here once the event modules are activated."
  And the top bar shows the signed-in user's name, the notification bell, the language switch, the theme toggle and the "Sign out" button
  And the side nav rail is visible with the "Dashboard" item present
```

**Evidence captured:**
- Screenshot before (login screen): `docs/screenshots/cp-dashboard-001-before.png`
- Screenshot after (landed Dashboard): `docs/screenshots/cp-dashboard-001-after.png`
- Console errors: 0 expected
- Network: the shell's avatar prefetch `GET /account/api/profile` returns 200, the notification bell's unread-count call returns 200, and for a `Statistics.View` holder the two statistics calls of E2E-DSH-014 also fire
- Audit row: none - landing on the Dashboard is not an audited operation (no `OperationLog` / `RowAudit` row is expected)

### E2E-DSH-002 - Welcome card bilingual copy

```gherkin
Scenario: Welcome card renders the Dashboard.Welcome + Dashboard.Intro strings
  Given an Approved administrator has landed on /
  Then the first .simf-surface shows an <h2> equal to "Welcome to the SIMF Control Panel"
  And a paragraph equal to the Dashboard.Intro string
  And the welcome card itself carries no buttons, inputs, grids or modals
  And nothing below it is editable - the whole page is read-only
```

### E2E-DSH-003 - Side nav rail (permission-filtered)

```gherkin
Scenario: Nav rail renders and is filtered by permissions, Dashboard always shown
  Given an Approved administrator has landed on /
  Then the side nav rail renders the CpNavigation groups
  And the "Dashboard" item (Href "/") is always visible because its RequiredPermission is null
  And items whose RequiredPermission the user does not hold are hidden
  And an Administrator carrying the "*" wildcard sees every group and every item
  And not-yet-built stub items show the "Soon" badge
```

### E2E-DSH-004 - Least-privilege admin still lands (ungated page)

```gherkin
Scenario: Admin with zero permission codes still reaches the Dashboard
  Given an Approved admin user whose role grants no permission codes (and not the "*" wildcard)
  When they sign in and land on /
  Then they are NOT redirected to /not-permitted
  And the Dashboard banner + welcome card render normally
  And the side nav rail shows only the "Dashboard" item (all gated items are hidden)
  And the programme dashboard does not render (see E2E-DSH-015)
```

### E2E-DSH-005 - Theme toggle persists

```gherkin
Scenario: Toggling light/dark theme on the Dashboard persists across reload
  Given an Approved administrator has landed on / in the default (light) theme
  When they click the theme toggle in the top bar
  Then <html data-theme> switches to "dark"
  And the page repaints in dark mode with no flash-of-unstyled-content
  When they reload /
  Then the page renders directly in dark mode (no light-then-dark flash)
  And there are 0 console errors (regression guard for the theme-toggle JSException fixed at a35450d)
```

### E2E-DSH-006 - Notification bell

```gherkin
Scenario: Notification bell opens its dropdown from the Dashboard
  Given an Approved administrator has landed on /
  When they click the notification bell in the top bar
  Then a dropdown titled "Notifications" opens
  And when the user has no unread notifications the dropdown shows the empty-state copy
  And a "Mark all read" action and a "View all" link to /account/notifications are present
  And clicking "View all" navigates to /account/notifications
```

### E2E-DSH-007 - Profile link + avatar prefetch

```gherkin
Scenario: Top-bar profile chip links to the profile and shows the prefetched avatar
  Given an Approved administrator has landed on /
  Then the top bar shows a profile chip linking to /account/profile with the user's name
  And on first circuit boot the shell fires GET /account/api/profile to prefetch the avatar
  When the profile response carries an AvatarUrl
  Then the chip shows the avatar image
  When the profile has no avatar
  Then the chip shows the default user icon
  When the administrator clicks the profile chip
  Then they navigate to /account/profile
```

### E2E-DSH-008 - Sign-out

```gherkin
Scenario: Signing out from the Dashboard returns to the login screen
  Given an Approved administrator has landed on /
  When they click "Sign out" in the top bar
  Then the form POSTs to /auth/sign-out
  And the auth cookie is cleared
  And they are returned to /login
  When they then navigate back to /
  Then they are redirected to /login (they are no longer authenticated)
```

### E2E-DSH-009 - Auth gate: unauthenticated visitor

```gherkin
Scenario: An unauthenticated visitor cannot see the Dashboard
  Given no SIMF auth cookie is present in the browser
  When the visitor navigates to http://localhost:5158/
  Then the [Authorize] attribute denies access
  And they are redirected to /login
  And no Dashboard banner or welcome card renders
```

### E2E-DSH-010 - Account-state guard: PendingApproval

```gherkin
Scenario: A signed-in but PendingApproval account is bounced from the Dashboard
  Given a user is authenticated but their account_state claim is "PendingApproval"
  When they navigate to /
  Then CpShellLayout.OnInitializedAsync detects the state before any module content renders
  And navigates them to /auth/pending
  And the Dashboard welcome card never renders
```

### E2E-DSH-011 - Account-state guard: Rejected

```gherkin
Scenario: A signed-in but Rejected account is bounced from the Dashboard
  Given a user is authenticated but their account_state claim is "Rejected"
  When they navigate to /
  Then CpShellLayout.OnInitializedAsync navigates them to /auth/rejected
  And the Dashboard welcome card never renders
```

### E2E-DSH-012 - Resilience: avatar prefetch fails

```gherkin
Scenario: The Dashboard still renders when the avatar prefetch errors
  Given an Approved administrator is signing in for the first time on a fresh circuit
  And GET /account/api/profile returns a 500 (or the JS interop throws a JSException)
  When they land on /
  Then the shell swallows the failed prefetch (best-effort)
  And the top-bar profile chip shows the default user icon (placeholder)
  And the Dashboard banner + welcome card render normally
  And no error toast or unhandled-exception page appears
```

### E2E-DSH-013 - RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the Dashboard chrome
  Given an Approved administrator is on / in English
  When they click the "العربية" language switch in the top bar
  Then the request goes to /culture?culture=ar&redirectUri=%2F
  And the page reloads with <html dir="rtl" lang="ar">
  And the browser tab title is "لوحة المعلومات · SIMF"
  And the SimfBanner title reads "لوحة المعلومات"
  And the welcome heading reads "مرحبًا بك في لوحة تحكم الملتقى البحري السعودي الدولي"
  And the intro paragraph reads the Arabic Dashboard.Intro string
  And the side nav rail mirrors to the right with Arabic labels (the Dashboard item reads "لوحة المعلومات")
  And the top-bar controls appear in reverse order
```

**Evidence captured (RTL):**
- Screenshot: `docs/screenshots/cp-dashboard-013-rtl.png`
- Console errors: 0 expected

### E2E-DSH-014 - Wave A golden path (programme dashboard renders for a Statistics.View holder)

```gherkin
Feature: Programme dashboard on the Control Panel landing page
  As an organiser who holds Statistics.View
  I want the standing totals and the day-by-day programme figures on the landing page
  So that I can read the state of the forum without opening a report

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And the three active ProgrammeDay rows of the fixture table above are seeded
  And superadmin@zagali-ict.com is Approved and passes PermissionCatalog.Statistics.View

Scenario: The full programme dashboard renders under the welcome panel
  Given the administrator is signed in with login + TOTP
  When they land on /
  Then OnInitializedAsync resolves Statistics.View through IAuthorizationService and sets _canViewStats true
  And the page fires GET /account/api/admin/statistics
  And the page fires GET /account/api/admin/statistics/programme
  And both return HTTP 200 with an ApiResult envelope carrying "success": true
  And the welcome panel from E2E-DSH-002 is still the first .simf-surface on the page
  And a second .simf-surface follows it with the <h2> "Event at a glance"
  And that surface holds a .simf-stat-grid of SimfStatCard tiles (E2E-DSH-016)
  And the "The live figures could not be loaded. Refresh the page to try again." message is absent
  And a third .simf-surface follows holding a <figure class="simf-chart">
  And the figure's <h3> reads "The programme, day by day"
  And its subtitle reads "Registered, present and attended across 3 forum days"
  And a .simf-day-grid follows the chart with exactly 3 <article class="simf-day-card"> elements
  And the day cards appear in DisplayOrder: "Day One", then "Day Two", then "Day Three"
  And the page contains no <form>, no <input> and no SimfDataGrid - it is entirely read-only
```

**Evidence captured:**
- Screenshot (full page, light theme): `docs/screenshots/cp-dashboard-014-programme.png`
- Console errors: 0 expected
- Network: `GET /account/api/admin/statistics` 200 and `GET /account/api/admin/statistics/programme` 200, 0 failed requests, 0 broken images
- DOM: `document.documentElement.scrollWidth === document.documentElement.clientWidth` (no horizontal overflow at 1280px and at 390px)
- Audit row: none - both endpoints are read-only aggregates and write no `OperationLog` / `RowAudit` row

### E2E-DSH-015 - Permission gate: no Statistics.View means welcome panel only

```gherkin
Scenario: An admin without Statistics.View sees the welcome panel and nothing else
  Given an Approved admin whose role grants no permission codes and not the "*" wildcard
  And the same three ProgrammeDay rows are seeded
  When they sign in and land on /
  Then they are NOT redirected to /not-permitted - the page itself is ungated
  And the banner and the welcome card render exactly as in E2E-DSH-002
  And _canViewStats is false, so the page fires NEITHER statistics call
  And the network log contains no request to /account/api/admin/statistics
  And the network log contains no request to /account/api/admin/statistics/programme
  And there is no "Event at a glance" heading anywhere in the DOM
  And there is no element matching .simf-stat-grid
  And there is no element matching .simf-chart
  And there is no element matching .simf-day-card
  And no number from the fixture ("420", "480", "431") appears anywhere in the page text

Scenario: The API refuses the same caller directly
  Given the same admin's bearer token
  When they call GET http://localhost:5175/api/v1/admin/statistics/programme
  Then the response is HTTP 403
  And no StatisticsProgramme payload is returned
  When an anonymous caller calls the same route
  Then the response is HTTP 401
```

**Why this matters:** the page is ungated but the data is not. The gate lives in
exactly two places - `Home.razor.cs` for the render and
`Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Statistics.View), nameof(AuthorizationPolicies.RequireApprovedAccount))`
on `GetStatisticsProgrammeEndpoint` for the payload. Hiding the chart without the
API gate would be a security defect, so both halves are asserted.

### E2E-DSH-016 - KPI stat grid

```gherkin
Scenario: Every KPI tile shows its live number and links to its module page
  Given an admin holding Statistics.View has landed on / with both statistics calls returning 200
  Then the .simf-stat-grid renders the tiles below, in this order, each as an <a class="simf-stat simf-stat--clickable">

  | Tile title        | Source field                        | Links to                  |
  | Current users     | StatisticsProgramme.CurrentUsers    | /admin/attendees          |
  | Visitors          | StatisticsProgramme.Visitors        | /admin/visitors           |
  | Pending approvals | StatisticsDashboard.PendingApprovals| /admin/visitors/pending   |
  | Staff             | StatisticsProgramme.Staff           | /admin/others             |
  | Moderators        | StatisticsProgramme.Moderators      | /admin/others             |
  | Speakers          | StatisticsProgramme.Speakers        | /admin/speakers           |
  | Sessions          | StatisticsDashboard.Sessions        | /admin/sessions           |
  | Exhibitors        | StatisticsProgramme.Exhibitors      | /admin/exhibitors         |
  | Sponsors          | StatisticsProgramme.Sponsors        | /admin/sponsors           |
  | Booths            | StatisticsProgramme.Booths          | /admin/booths             |
  | Total attended    | StatisticsProgramme.TotalAttended   | /admin/attendance         |
  | Total ratings     | StatisticsDashboard.RatingsCount    | /admin/ratings            |
  | Average rating    | StatisticsDashboard.AverageRating   | /admin/ratings            |

  And every .simf-stat__value except "Average rating" is a whole number formatted "#,##0" in the UI culture
  And a count of 1234 therefore renders as "1,234" in English
  And "Average rating" renders with one decimal place, e.g. "4.2"
  And no tile shows "NaN", "null", an empty string or a negative number
  When the administrator clicks the "Visitors" tile
  Then they navigate to /admin/visitors

Scenario: The role tiles come from admin-curated profile types, not hardcoded names
  Given one ProfileType has MobileAppRole = Staff and one has MobileAppRole = Moderator
  And one active UserProfile is attached to each
  When the programme call returns
  Then the "Staff" tile counts the Staff-typed profile
  And the "Moderators" tile counts the Moderator-typed profile
  And the "Exhibitors" tile counts CP-managed Exhibitor organisations, NOT exhibitor user accounts
  And the two figures stay independent when one changes

Scenario: A partially available payload still renders what it has
  Given GET /account/api/admin/statistics returns 200
  And GET /account/api/admin/statistics/programme fails
  Then the tiles sourced from StatisticsDashboard still render (Pending approvals, Sessions, Total ratings, Average rating)
  And the tiles sourced from StatisticsProgramme are absent
  And the "Event at a glance" surface still renders (the Unavailable message shows only when BOTH payloads are missing)
```

### E2E-DSH-017 - Grouped bar chart, one cluster per programme day

```gherkin
Scenario: The chart draws three clusters of three bars on a zero baseline
  Given the fixture's three programme days are seeded
  And an admin holding Statistics.View has landed on / in English, light theme
  Then the figure .simf-chart carries the title "The programme, day by day"
  And a <ul class="simf-chart__legend"> lists exactly three items: "Registered", "Present", "Attended"
  And each legend item carries a .simf-chart__swatch--1 / --2 / --3 span marked aria-hidden="true"
  And the <svg class="simf-chart__svg"> has viewBox "0 0 640 260", role="img"
  And its aria-label reads "Grouped bar chart comparing the number of visitors registered, present at the venue, and attending sessions on each forum day."
  And the SVG carries a <title> equal to the chart title and a <desc> equal to that aria-label

  And the SVG contains exactly 9 <rect class="simf-chart__bar"> elements (3 days x 3 series)
  And the class suffix encodes the series, so Registered bars carry --1, Present --2, Attended --3
  And each rect carries a <title> naming the day, the series and the value, e.g. Day One, Registered, 420
  And the SVG contains exactly 9 <text class="simf-chart__value"> direct value labels
  And those labels read 420, 310, 275, 180, 480, 431, 40, 0, 0 in DOM order

  And the y axis <ul class="simf-chart__yaxis"> lists the ticks highest first: 500, 375, 250, 125, 0
  And the SVG contains 5 <line class="simf-chart__gridline"> elements, one per tick
  And exactly one <line class="simf-chart__baseline"> sits at y1 = y2 = 260

  And the x axis <ul class="simf-chart__xaxis"> lists three <li class="simf-chart__xtick">: "Day One", "Day Two", "Day Three"
  And no fourth series exists - Sessions is NOT a bar (see E2E-DSH-024)

Scenario: Bar geometry is honest
  Given the axis maximum is 500 and the plot height is 260
  Then Day Two's Present bar (480) is the tallest, with height 249.6 and y 10.4
  And Day One's Registered bar (420) has height 218.4 and y 41.6
  And every rect satisfies y + height = 260, so every bar is anchored to the zero baseline
  And no bar's height exceeds 260 even if a value were to exceed the axis maximum
  And within a cluster the three bars never overlap - each is 56.98 wide with a 2-unit gap
  And clusters do not collide: Day One's first bar starts at x 19.2 and Day Three's last bar ends inside x 640
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-dashboard-017-chart.png`
- DOM probe: `document.querySelectorAll('.simf-chart__bar').length === 9`
- DOM probe: every bar satisfies `Math.abs((+r.getAttribute('y')) + (+r.getAttribute('height')) - 260) < 0.01`
- Console errors: 0 expected

### E2E-DSH-018 - A forum day with no activity is still a zero row

```gherkin
Scenario: Day Three has no scans, no sessions and no arrivals but still appears everywhere
  Given ProgrammeDay "Day Three" (2026-11-19) is active
  And it has 40 registrations, 0 gate check-ins, 0 sessions and 0 hall arrivals
  When an admin holding Statistics.View lands on /
  Then the programme payload returns 3 days, not 2 - an inactive day is filtered, an idle day is not
  And the chart renders a third cluster labelled "Day Three"
  And that cluster's Registered bar has height 20.8 (40 of 500)
  And its Present and Attended bars have height 0 and y 260 - flat on the baseline, not missing
  And their direct value labels still read "0", clamped to y 254 so they stay inside the plot
  And a third .simf-day-card renders with the title "Day Three" and the date "19-11-2026"
  And its Present and Attended gauges show the value "0" with a 0% fill
  And its sessions line reads "Sessions" then "0"
  And the hidden data table carries a "Day Three" row reading 40, 0, 0

Scenario: A day that is deactivated disappears entirely
  Given ProgrammeDay "Day Three" is then deactivated (IsActive = false)
  When the administrator reloads /
  Then the payload returns 2 days
  And the chart renders 2 clusters
  And the subtitle reads "Registered, present and attended across 2 forum days"
  And only 2 .simf-day-card elements render
```

### E2E-DSH-019 - Arabic RTL mirrors the plot, not the lettering

```gherkin
Scenario: The chart reads right-to-left in Arabic
  Given an admin holding Statistics.View is on / in English with the fixture seeded
  When they switch the language to "العربية"
  Then the page reloads with <html dir="rtl" lang="ar">
  And SimfGroupedBarChart resolves IsRtl from CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft without the caller passing anything

  And the chart title reads "البرنامج يومًا بيوم"
  And the legend reads "المسجلون", "الحاضرون", "المشاركون" in that order
  And the subtitle reads "المسجلون والحاضرون والمشاركون خلال 3 من أيام الملتقى"
  And the x-axis ticks read "اليوم الأول", "اليوم الثاني", "اليوم الثالث"
  And each day label falls back to the English Title when TitleArabic is blank

  And the plot is mirrored by geometry, not by a CSS transform
  And Day One's Registered bar therefore moves from x 19.2 to x 563.82
  And Day One's cluster is the RIGHTMOST cluster on screen
  And Day Three's cluster is the LEFTMOST
  And every bar still satisfies 0 <= x and x + width <= 640
  And every bar still satisfies y + height = 260 - mirroring is horizontal only

  And no glyph is mirrored: the value labels, the tick labels and the legend text all read normally
  And the y-axis tick column moves to the right of the plot (the .simf-chart__body flex row follows the logical direction)
  And the SVG coordinate attributes still use a decimal POINT, never an Arabic decimal comma
  And the day-card dates still read "17-11-2026" style, not a Hijri or reversed form
  And there are 0 console errors and no horizontal page overflow
```

**Evidence captured (RTL):**
- Screenshot: `docs/screenshots/cp-dashboard-019-rtl-chart.png`
- DOM probe: the rect with the largest `x` belongs to the Day One cluster
- DOM probe: no coordinate attribute matches `/,/`
- Regression guard: a decimal comma in `x`, `y`, `width` or `height` collapses the plot silently, which is why `Coord()` is pinned to `InvariantCulture`

### E2E-DSH-020 - Light, dark and grey themes

```gherkin
Scenario Outline: The chart inherits every theme from the token layer
  Given an admin holding Statistics.View has landed on / with the fixture seeded
  When the document root is set to <theme>
  Then the three bar series resolve to <series1>, <series2> and <series3>
  And each colour comes from the CSS custom properties --chart-series-1..3, never from an inline fill
  And no <rect>, <line> or <text> in the SVG carries a hardcoded colour attribute or inline style
  And the gridlines resolve from --chart-grid and the baseline from --chart-baseline
  And the gauge tracks resolve from --chart-track
  And every series colour keeps at least 3:1 contrast against --color-surface for that theme
  And the legend swatch colour matches the bar colour of the same series index
  And a metric keeps ITS colour across themes - colour follows the entity, never its rank

  Examples:
    | theme                     | series1 | series2 | series3 |
    | (none, the light default) | #2A6FB5 | #C2410C | #1B8A63 |
    | data-theme="dark"         | #4A8CD4 | #D9683C | #2E9D74 |
    | data-theme="grey"         | #2A6FB5 | #C2410C | #1B8A63 |

Scenario: The grey theme deliberately has no series override
  Given the grey surface is #E9EAEB
  When data-theme="grey" is applied
  Then the light palette is reused unchanged, because it was validated against the grey surface too
  And the day-card gauges, the stat tiles and the chart all repaint together with no stale colour

Scenario: Switching theme at runtime does not break the plot
  Given the administrator is on / in the light theme
  When they click the theme toggle in the top bar
  Then <html data-theme> becomes "dark" and document.documentElement.style.colorScheme becomes "dark"
  And the chart repaints in the dark palette without a reload
  And the bar geometry is unchanged - only the fills differ
  And there are 0 console errors
```

**Evidence captured:**
- Screenshots: `docs/screenshots/cp-dashboard-020-light.png`, `-dark.png`, `-grey.png`
- Note: the top-bar toggle cycles light and dark only. Grey is reached with
  `simfTheme.setNamed("grey")` (documented in `simf-theme.js`, D-098), so the
  grey pass is driven by that call rather than by a click.
- Rule: if any value in the `--chart-series-*` block changes, re-run the
  computational validation. The palette was verified by measurement (worst
  adjacent CVD OKLab dE 9.4 light and grey, 9.2 dark, against a floor of 8.0);
  do not eyeball a replacement.

### E2E-DSH-021 - Dates read dd-MM-yyyy Saudi local, and no UTC leaks

```gherkin
Scenario: Every date on the page is a Saudi-local dd-MM-yyyy string
  Given the fixture's three programme days are seeded
  When an admin holding Statistics.View lands on /
  Then the day-card dates read exactly "17-11-2026", "18-11-2026" and "19-11-2026"
  And each matches the regular expression ^\d{2}-\d{2}-\d{4}$
  And no date on the page carries a time component
  And no text node anywhere on the page contains "UTC", "GMT", "Z" as an instant suffix, or a "T" ISO separator
  And no text node contains a +00:00 or Z-suffixed offset
  And switching the UI culture to Arabic does not change the date format

Scenario: The per-day counts are bucketed on the SAUDI calendar day, not the UTC day
  Given Saudi Arabia is UTC+03:00 with no daylight saving
  And a gate check-in is recorded at 2026-11-17 21:30 UTC
  Then that scan belongs to the SAUDI day 2026-11-18, because 21:30 UTC is 00:30 Saudi
  And it increments Day Two's "Present" figure, NOT Day One's
  And a check-in at 2026-11-17 20:59 UTC (23:59 Saudi) still belongs to Day One
  And a check-in at exactly 2026-11-16 21:00 UTC is the first instant of Saudi 2026-11-17 and belongs to Day One
  And the same window applies to registrations, sessions and hall arrivals
  And the window is half-open [start, end), so the end instant belongs to the NEXT day only

Scenario: A raw UTC instant never reaches the browser
  Given the programme payload is inspected in the network panel
  Then ProgrammeDayStats.Date is a plain calendar date (DateOnly), carrying no instant and no offset
  And the CP formats it with SaudiTime.DateFormat under InvariantCulture, so there is no instant to convert
```

**Why the bucketing matters:** counting on the stored UTC value would misfile
every record logged in the Saudi evening, quietly moving a whole evening of gate
traffic onto the wrong forum day. That is the failure mode the five boundary
tests in `tests/SIMF.Api.Tests/StatisticsProgrammeTests.cs` exist to catch.

### E2E-DSH-022 - Resilience: the statistics calls fail

```gherkin
Scenario: Both statistics calls fail and the page degrades to the welcome panel
  Given an admin holding Statistics.View signs in
  And GET /account/api/admin/statistics returns a 500 (or a non-JSON body)
  And GET /account/api/admin/statistics/programme returns a 500 (or a non-JSON body)
  When they land on /
  Then simfAccount.getJson turns each failure into an envelope with success false and error code BAD_RESPONSE
  And no JSException escapes OnInitializedAsync
  And no unhandled-exception page and no error toast appear
  And the banner and welcome card render normally
  And the "Event at a glance" surface renders the message "The live figures could not be loaded. Refresh the page to try again."
  And in Arabic that message reads "تعذر تحميل الأرقام المباشرة. حدّث الصفحة للمحاولة مرة أخرى."
  And no .simf-stat-grid renders
  And no .simf-chart renders
  And no .simf-day-card renders
  And the nav rail, the theme toggle and sign-out all still work

Scenario: Only the programme call fails
  Given GET /account/api/admin/statistics returns 200
  And GET /account/api/admin/statistics/programme fails
  When they land on /
  Then the Unavailable message does NOT appear, because one payload arrived
  And the four StatisticsDashboard tiles render
  And the chart and the day cards are absent
  And the page still reports 0 console errors

Scenario: The API endpoint itself is unreachable
  Given the backend API on :5175 is stopped
  When an admin holding Statistics.View lands on /
  Then the BFF passthrough returns a non-2xx result
  And the page still renders the welcome panel and remains navigable
```

### E2E-DSH-023 - The hidden data table serves screen readers

```gherkin
Scenario: The same numbers are available without seeing the chart
  Given the fixture's three programme days are seeded
  And an admin holding Statistics.View has landed on /
  Then a <table class="simf-visually-hidden"> follows the plot inside the same <figure>
  And it is clipped from view but NOT hidden from assistive technology
  And it carries no display:none, no visibility:hidden and no aria-hidden attribute
  And its <caption> reads "The programme, day by day"

  And its header row is a category column plus one column per series:
    | Forum day | Registered | Present | Attended |
  And there is NO Sessions column - the table mirrors the chart's three series exactly

  And its body rows read:
    | Day One   | 420 | 310 | 275 |
    | Day Two   | 180 | 480 | 431 |
    | Day Three |  40 |   0 |   0 |

  And each day cell is a <th scope="row"> and each figure a <td>
  And each column header is a <th scope="col">
  And every figure in the table equals the direct value label on the matching bar
  And a group with fewer values than series pads with 0 rather than shifting the columns

Scenario: The visual layer carries the same information without relying on colour
  Then the chart shows a legend, because there are two or more series
  And every bar carries its value as a direct text label, so magnitude never requires decoding a hue
  And every bar carries a <title> naming its day and series
  And each day-card gauge track is role="meter" with aria-valuenow, aria-valuemin="0" and aria-valuemax="500"
  And each gauge's aria-label is its series name, so the meter is self-describing

Scenario: The table is absent when there is nothing to tabulate
  Given no programme days are configured
  Then no .simf-visually-hidden table renders inside the chart figure (see E2E-DSH-025)
```

### E2E-DSH-024 - Day cards: shared scale, and sessions as a number

```gherkin
Scenario: Each day card compares its three metrics on one shared scale
  Given the fixture's three programme days are seeded, with an overall maximum of 480
  And the gauge maximum is therefore 500 for EVERY card
  When an admin holding Statistics.View lands on /
  Then each <article class="simf-day-card"> shows an <h3> title and a .simf-day-card__date
  And each card holds exactly three SimfBarGauge rows: Registered, Present, Attended
  And each gauge fill takes its width from the --simf-gauge-fill custom property, expressed as a percentage
  And Day One's gauges therefore fill 84%, 62% and 55%
  And Day Two's gauges fill 36%, 96% and 86.2%
  And Day Three's gauges fill 8%, 0% and 0%
  And because the max is shared, Day One's Registered bar is visibly longer than Day Two's - the cards are directly comparable
  And the fill percentage always uses a decimal POINT, so an Arabic culture cannot emit a comma and collapse the bar to zero width
  And a gauge fill is clamped to 100% and never exceeds its track
  And the gauge series colours match the chart: Registered 1, Present 2, Attended 3

Scenario: Sessions is a number, never a fourth bar
  Then each card ends with a <p class="simf-day-card__sessions"> holding the label "Sessions" and a .simf-day-card__sessions-value
  And Day One reads "6", Day Two reads "8" and Day Three reads "0"
  And the sessions figure does NOT appear as a bar in the chart
  And the chart has no second y axis
  And the legend still lists exactly three series
  And in Arabic the sessions label reads "الجلسات"

Scenario: The gauges survive a degenerate dataset
  Given every figure across every day is 0
  Then the gauge maximum falls back to 1 rather than dividing by zero
  And every gauge fill renders 0%, never NaN%
  And every card still renders with its title, date and sessions count
```

**Why sessions is not a bar:** the three people-metrics share the unit "people",
so one axis is honest. Sessions-per-day is a count on a different scale, roughly
two orders of magnitude smaller. Placing it on the same axis would either
flatten it to invisibility or force a second axis, and a second axis invites the
reader to infer a relationship that the data does not contain.

### E2E-DSH-025 - Empty state: no programme days configured

```gherkin
Scenario: A fresh installation with no forum days
  Given no ProgrammeDay rows exist (or every row is deactivated)
  And an admin holding Statistics.View lands on /
  Then GET /account/api/admin/statistics/programme returns 200 with an empty Days array
  And the headline participant tiles still render, because they do not depend on days
  And the chart figure still renders with its title "The programme, day by day"
  And the subtitle is empty, because there is no day count to state
  And in place of the plot the figure shows "No programme days have been set up yet."
  And in Arabic that reads "لم يتم إعداد أي أيام للبرنامج بعد."
  And no <svg class="simf-chart__svg"> is emitted
  And no <rect class="simf-chart__bar"> exists
  And no hidden data table is emitted
  And no .simf-day-grid and no .simf-day-card render
  And there is no error toast and no console error - an empty programme is a valid state, not a failure

Scenario: The first day added brings the chart to life
  Given one active ProgrammeDay is then created
  When the administrator reloads /
  Then the empty message is replaced by the plot
  And a single cluster renders, correctly laid out rather than stretched across the full width
  And the subtitle reads "Registered, present and attended across 1 forum days"
  And one day card renders
```

**Note on the singular:** `Dashboard.Programme.Subtitle` is a single
`{0}`-format string with no plural form, so a one-day programme reads
"across 1 forum days". That is the current, intended behaviour of the resx
string; if plural handling is added later this assertion changes with it.

---

### E2E-DSH-026 - Shell head: the tab carries the SIMF icon, nothing 404s

```gherkin
Feature: The shell head is rendered on every CP page
  Components/App.razor is the single document every Control Panel route is
  served inside, so one wrong href there is a 404 on every page load - not on
  one page. QA-LIVE-001 was exactly that: no <link rel="icon"> was declared, so
  the browser fell back to requesting /favicon.ico, which nothing served.

Background:
  Given the administrator is signed in

Scenario: the declared icon is served, and the fallback is never requested
  When the administrator loads "/"
  Then the document head contains a <link rel="icon"> whose href resolves
  And the network panel records a 200 for that icon
  And the network panel records NO request for "/favicon.ico"
  And the browser tab shows the SIMF mark rather than the blank default glyph

Scenario Outline: the same holds on every other CP route
  When the administrator loads "<route>"
  Then no request in the network panel returns 404
  And the browser console reports zero errors

  Examples:
    | route                  |
    | /admin/sessions        |
    | /admin/content-blocks  |
    | /account/profile       |
    | /not-permitted         |
```

> **Why this lives on the Dashboard file rather than its own.** The head belongs
> to the shell, not to a page; the Dashboard file already owns the shell chrome
> (see the page summary above). The second scenario is deliberately a sweep, so a
> future head edit that breaks one route class is caught here rather than on
> whichever page someone happened to open.

---

## Implementation notes

- **Two halves, two gates.** The route is ungated (`[Authorize]` only, nav item
  `RequiredPermission` null), so E2E-DSH-004 and E2E-DSH-015 must both pass: a
  zero-permission admin lands successfully AND sees no figures. The data gate is
  `PermissionCatalog.Statistics.View`, reused rather than newly minted, so Wave A
  added **no permission, no seeding and no migration**. The API endpoint carries
  the same policy plus `RequireApprovedAccount`.
- **Read-only, no schema.** `GetStatisticsProgrammeEndpoint` is an aggregate read
  over existing tables. There is nothing to audit and nothing to roll back, which
  is why every scenario above expects zero `OperationLog` / `RowAudit` rows.
- **Backing unit + integration tests.**
  - `tests/SIMF.ControlPanel.Tests/ChartGeometryTests.cs` (41 tests) pins the
    geometry rules asserted in E2E-DSH-017, -018, -019, -024 and -025: the zero
    baseline, non-overlapping bars, clusters inside the plot, the RTL mirror,
    clamping above the axis maximum, and the divide-by-zero guards in `NiceMax`
    and `GaugeFraction`.
  - `tests/SIMF.Api.Tests/StatisticsProgrammeTests.cs` (21 tests) pins the data
    rules asserted in E2E-DSH-015, -016, -018 and -021: the five Saudi-day
    boundary cases, distinct-person counting for Present and Attended, the
    exclusion of denied and check-out scans, display-order with idle days, the
    profile-type-driven role counts, and the 401/403 gate.
  - The browser pass therefore only needs to prove the render, the wiring and the
    themes - the arithmetic is already covered below the UI.
- **Present and Attended are siblings, not subsets.** `Present` counts distinct
  `GateScan.UserProfileId` where `Outcome = Allowed` and `Direction = CheckIn`;
  `Attended` counts distinct `HallAttendance.UserId`. They key on **different
  identifiers**, so neither is a subset of the other and neither should be
  asserted as bounded by the other. A day where Attended exceeds Present is not a
  defect.
- **Sessions are matched to a day by date.** `ProgrammeDay` deliberately has no FK
  from `Session`; the service buckets sessions by `Session.Start` falling inside
  the day's Saudi window, which is how the mobile app groups them too. The two
  surfaces cannot disagree about which session belongs to which day.
- **Single-database joins only.** The role counts walk `UserProfile` to
  `UserProfileType`, both in the App DB. `CurrentUsers` and `Registered` are
  counted separately against the Identity DB. There is no cross-database join and
  no cross-database FK anywhere in this feature (D-157).
- **Theme-toggle regression guard.** E2E-DSH-005 doubles as the browser regression
  check for the theme-toggle JSException + dark-mode flash fixed at commit
  `a35450d`. E2E-DSH-020 extends it to the chart repaint.
- **Honesty flag - branch state on `feat/cp-dashboard-reporting`.** The API
  endpoint, the service, the contracts, the chart components and both test suites
  are in place, but the Control Panel **BFF passthrough for the programme call is
  not**. `Home.razor.cs` requests `/account/api/admin/statistics/programme`, while
  `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs` maps only
  `/admin/statistics` inside the `/account/api` group, and `SimfAdminClient` has
  no `GetStatisticsProgrammeAsync`. The unmatched route returns 404, which
  `simfAccount.getJson` converts into a `BAD_RESPONSE` envelope, so `_programme`
  stays null and the page degrades exactly as E2E-DSH-022's "only the programme
  call fails" scenario describes. Consequence: **E2E-DSH-017, -018, -019, -020,
  -023, -024 and -025 are RED until the passthrough and the client method are
  added**, as is the programme half of -014, -016 and -021. They are authored
  here as the target specification. E2E-DSH-015 and -022 pass as written today.
  Re-run the whole DSH range once the passthrough lands.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin scenario
  into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created)
  plus a step-definition class. The Gherkin shape is already runner-agnostic.

---

_Last reviewed:_ 2026-07-31 by Claude (`QA-LIVE-001` shell-head favicon, E2E-DSH-026);
prior review 2026-07-29 (Wave A programme dashboard, E2E-DSH-014..025).
