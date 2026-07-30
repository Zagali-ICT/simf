# Dashboard (`/`)

| | |
|--|--|
| **Route** | `/` |
| **Component** | [`Components/Pages/Home.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Home.razor) (+ [`.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Home.razor.cs)) |
| **Layout** | `CpShellLayout` |
| **Audience** | Any signed-in, Approved CP user. The figures render only for holders of `Statistics.View`. |
| **Auth** | `@attribute [Authorize]` on the page (so the welcome panel is ungated); the statistics block is gated in code on `PermissionCatalog.Statistics.View`, and both APIs carry `Policies(PolicyFor(Statistics.View), RequireApprovedAccount)` |
| **Pattern** | `SimfBanner` (D-132) + welcome surface + KPI stat grid + one inline-SVG grouped bar chart + one card per programme day. **Not a list page:** no grid, no CRUD, no forms, no mutations. |
| **Status** | Real (D-799 to D-803, 2026-07-29) |
| **Backend endpoints** | `GET /account/api/admin/statistics` -> `GET /api/v1/admin/statistics`; `GET /account/api/admin/statistics/programme` -> `GET /api/v1/admin/statistics/programme` |
| **Backed by** | Read-only aggregates computed on demand over `SimfIdentityDbContext` + `SimfAppDbContext`. **No schema change, no migration, no new permission, no seeding.** |
| **Source** | [`StatisticsEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Statistics/StatisticsEndpoints.cs), [`StatisticsService.cs`](../../../src/Backend/SIMF.Infrastructure/Statistics/StatisticsService.cs), [`StatisticsContracts.cs`](../../../src/Shared/SIMF.Contracts/Statistics/StatisticsContracts.cs), [`ChartGeometry.cs`](../../../src/Shared/SIMF.Components/Charts/ChartGeometry.cs), [`SimfGroupedBarChart.razor`](../../../src/Shared/SIMF.Components/Charts/SimfGroupedBarChart.razor), [`SimfBarGauge.razor`](../../../src/Shared/SIMF.Components/Charts/SimfBarGauge.razor) |
| **Tests** | [`ChartGeometryTests.cs`](../../../tests/SIMF.ControlPanel.Tests/ChartGeometryTests.cs) (41), [`StatisticsProgrammeTests.cs`](../../../tests/SIMF.Api.Tests/StatisticsProgrammeTests.cs) (21), E2E [`cp-dashboard.md`](../../tests/e2e/cp-dashboard.md) |
| **Last reviewed** | 2026-07-29 |

## 1. Purpose

The post-sign-in landing page. Every signed-in admin gets the banner plus a
welcome panel; an admin who holds `Statistics.View` also gets the live event
figures, in two halves that answer two different questions.

- **Standing totals** (how big is this event?) stay **plain numbers** on a stat
  grid. A bar chart of thirteen unrelated counts communicates nothing that the
  numbers do not.
- **The programme** (how did each forum day go?) is the **graphic**: a grouped
  bar chart across the days, then one card per day.

The split is deliberate and is the page's main design rule. The chart carries
only the three metrics that share the unit "people", so one axis is honest.
Sessions-per-day counts a different thing on a different scale (single digits
against hundreds or thousands of people) and is rendered as a **number on the
day card**, never as a fourth bar (D-802).

## 2. Permission gate

One existing permission covers the whole surface: **`PermissionCatalog.Statistics.View`**.
No new code was minted, so there is nothing to seed and no migration (D-800).

| Layer | Gate |
|-------|------|
| Page | `[Authorize]` only. The nav item `Module.Dashboard` carries **no** `RequiredPermission`, so every signed-in Approved user can reach `/`. |
| Statistics block | `Home.OnInitializedAsync` resolves `_canViewStats` through `IAuthorizationService.AuthorizeAsync(user, PolicyFor(Statistics.View))`. When it is false, **no fetch is issued** and the whole block is absent from the DOM. |
| API (both endpoints) | `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Statistics.View), nameof(AuthorizationPolicies.RequireApprovedAccount))` |

Because the page itself is ungated, the auth scenario here is **not** a
`/not-permitted` case: it is the unauthenticated redirect to `/login` plus the
`CpShellLayout` account-state guards (PendingApproval -> `/auth/pending`,
Rejected -> `/auth/rejected`). An admin without `Statistics.View` simply sees
the welcome panel and nothing else, with no error and no empty-state message.

## 3. Data flow + endpoints

Both reads go out from `OnInitializedAsync` through the same JS helper,
`simfAccount.getJson`, which does a `same-origin` fetch and unwraps the
`ApiResult<T>` envelope. `App.razor` renders `Routes` with
`InteractiveServerNoPrerender`, so there is no prerender pass to guard against.

The two calls are **independent and individually tolerant**: each one only
assigns its field when the envelope reports `Success: true` with non-null data,
so a failure of one still leaves the other rendered.

| CP call | API endpoint | Policy | Payload |
|---------|--------------|--------|---------|
| `GET /account/api/admin/statistics` | `GET /admin/statistics` | `Statistics.View` + `RequireApprovedAccount` | `ApiResult<StatisticsDashboard>` (flat scalars) |
| `GET /account/api/admin/statistics/programme` | `GET /admin/statistics/programme` | `Statistics.View` + `RequireApprovedAccount` | `ApiResult<StatisticsProgramme>` (headline counts + `Days`) |

Both API routes are declared **relative** (`Get("/admin/statistics/programme")`);
the FastEndpoints `RoutePrefix` supplies `api/v1` (the D-568 gotcha). The
programme read is a **new, additive** contract rather than a widening of
`StatisticsDashboard`: the existing record is byte-identical, so no existing
consumer changed (D-800).

The figures are a **snapshot taken at page load**. There is no refresh button
and no polling; reload the page to re-read.

## 4. UI

### 4.1 KPI stat grid (`.simf-stat-grid`)

Thirteen `SimfStatCard` tiles, each a clickable anchor into the desk that owns
the number. Counts format as `#,##0` in the current UI culture; the average
formats as `0.0` invariant-culture.

| Tile | Field | How the number is computed | Links to |
|------|-------|----------------------------|----------|
| Current users | `programme.CurrentUsers` | Identity DB: total `Users` row count, unfiltered (every account, admin included) | `/admin/attendees` |
| Visitors | `programme.Visitors` | App DB: active `UserProfiles` whose `ProfileType.IsForVisitor` | `/admin/visitors` |
| Pending approvals | `dashboard.PendingApprovals` | Identity DB: `UserType = Visitor` and `AccountState = PendingApproval` | `/admin/visitors/pending` |
| Staff | `programme.Staff` | App DB: active `UserProfiles` whose `ProfileType.MobileAppRole = Staff` | `/admin/others` |
| Moderators | `programme.Moderators` | App DB: active `UserProfiles` whose `ProfileType.MobileAppRole = Moderator` | `/admin/others` |
| Speakers | `programme.Speakers` | App DB: `Speakers` where `IsActive` | `/admin/speakers` |
| Sessions | `dashboard.Sessions` | App DB: `Sessions` where `IsActive` (whole event, not per day) | `/admin/sessions` |
| Exhibitors | `programme.Exhibitors` | App DB: `Exhibitors` where `IsActive`. These are the CP-managed **organisations**, not accounts. | `/admin/exhibitors` |
| Sponsors | `programme.Sponsors` | App DB: `Sponsors` where `IsActive` | `/admin/sponsors` |
| Booths | `programme.Booths` | App DB: `Booths` where `IsActive` | `/admin/booths` |
| Total attended | `programme.TotalAttended` | App DB: **distinct** `HallAttendance.UserId` across the whole event | `/admin/attendance` |
| Ratings | `dashboard.RatingsCount` | App DB: `RatingResponses` where `IsActive` | `/admin/ratings` |
| Average rating | `dashboard.AverageRating` | App DB: `AVG(OverallStars)` over active responses that carry a score. The nullable cast makes an empty set return null rather than throw, folded to `0`. | `/admin/ratings` |

Notes on the sourcing:

- The role counts (Visitors, Staff, Moderators) resolve through
  `UserProfile -> UserProfileType`, and **both tables live in the App DB**, so
  this is a single-database join and never a cross-database one (D-157). Which
  profile type counts as staff or as a visitor is admin-curated data
  (`ProfileType.MobileAppRole`, `ProfileType.IsForVisitor`), never a hardcoded
  role name.
- `StatisticsProgramme` also carries `ExhibitorAccounts` (active profiles with
  `MobileAppRole = Exhibitor`). It is **not** shown as a tile today: the page
  displays the organisation count instead. The field is on the contract so a
  later tile needs no API change.
- Tiles drawn from `_dashboard` and tiles drawn from `_programme` are rendered in
  separate `@if` blocks, so if only one call succeeded the grid renders that
  call's subset rather than blanking.

### 4.2 Grouped bar chart (`SimfGroupedBarChart`)

One cluster per programme day, in `DisplayOrder` then `Date` order. Hand-rolled
inline SVG in `SIMF.Components`, not a JS charting library and not a NuGet
package (D-799): the deployment is on-premises under an NCA posture, so a CDN
fetch is unavailable and every third-party runtime component is a fresh patching
obligation.

**The SVG carries geometry only.** The title, subtitle, legend, axis ticks and
category labels are ordinary HTML positioned around it, so every string
localises, wraps and mirrors like the rest of the page instead of being caught
in a transform that would also mirror the lettering.

Three series, always in this fixed order (colour follows the metric, never its
rank, so slot 1 is Registered on every day and on every gauge):

| # | Series | Per-day source |
|---|--------|----------------|
| 1 | Registered | Identity DB: `Users` with `UserType = Visitor` whose `CreatedAt` falls in that Saudi day |
| 2 | Present | App DB: **distinct** `GateScan.UserProfileId` where `Outcome = Allowed`, `Direction = CheckIn`, `ScannedAt` in the window. A visitor scanning twice counts once. |
| 3 | Attended | App DB: **distinct** `HallAttendance.UserId` whose `Enter` falls in the window |

Present and Attended are **sibling** figures, not nested sets: they are measured
from different tables keyed on **different identifiers** (a gate scan resolves a
`UserProfile.Id`, a hall arrival records the Identity `UserId`), so neither is a
subset of the other. That is why this is a **grouped** bar chart and never a
stacked one: stacking would assert that the parts sum to a whole, and their sum
has no meaning (D-802).

Chart mechanics, all delivered by the pure static `ChartGeometry`:

- Axis maximum is `NiceMax(MaxValue(groups))`, rounding up to 1, 2, 2.5 or 5
  times a power of ten so tick labels land on round numbers. Ticks are
  `AxisTicks(max, 4)` and always include zero.
- Bars are anchored to a **zero baseline** (`GroupedBars` computes
  `Y = plotHeight - height`). A bar drawn from 95 to 100 would exaggerate a five
  percent difference, so the rule is enforced in the geometry rather than left
  to each caller.
- Adjacent bars are separated by a real gap (`BarGap = 2` viewBox units) so two
  fills never touch, and each category slot reserves `GroupPadRatio = 0.18` of
  its width as padding so neighbouring clusters read as separate.
- The viewBox is `0 0 640 260` with `preserveAspectRatio="none"`, so the plot
  stretches to its container while the surrounding HTML text keeps its own size.

Accessibility, per the data-visualisation rules:

- `role="img"` plus an `aria-label` taken from `Dashboard.Programme.Description`,
  and `<title>` / `<desc>` inside the SVG.
- A legend whenever there are two or more series, so identity is never carried
  by colour alone.
- A **direct value label** above every bar, so magnitude is readable without
  decoding a hue.
- A per-bar `<title>` giving day, series and value on hover.
- A **visually hidden data table** (`.simf-visually-hidden`) carrying the same
  numbers, for assistive technology and for anyone who cannot separate the
  series by colour.

The subtitle is `Dashboard.Programme.Subtitle` formatted with the day count, and
is blank when there are no days.

### 4.3 Programme day cards (`.simf-day-grid` / `.simf-day-card`)

One `<article>` per day, rendered only when `Days` is non-empty:

- **Title**: the day's own bilingual title, `TitleArabic` under an Arabic UI
  culture falling back to `Title` when the Arabic one is blank.
- **Date**: `dd-MM-yyyy` (`SaudiTime.DateFormat`). `ProgrammeDay.Date` is already
  the event-local calendar day, so there is no instant to convert here, only a
  format to apply. No UTC string reaches the UI.
- **Three `SimfBarGauge` bars** (Registered, Present, Attended) in the same
  series order and the same colours as the chart. Each is a label, a track, a
  proportional fill and the value, marked up as `role="meter"` with
  `aria-valuenow` / `aria-valuemin` / `aria-valuemax`. The fill is pure HTML and
  CSS: the width is passed as the `--simf-gauge-fill` custom property rather
  than an inline style rule, so the stylesheet keeps control of presentation.
- **All the cards share one scale.** `_gaugeMax` is computed once as
  `NiceMax(MaxValue(chartGroups))` across every day, so a gauge on day 1 is
  directly comparable with the same gauge on day 3.
- **Sessions** is the last line: the label plus the count of active sessions
  whose `Start` falls in that day, as a **number**. Not a fourth gauge, and not
  a fourth bar on the chart (D-802).

## 5. The Saudi-day bucketing rule (D-801)

Instants are stored as **UTC**. Saudi Arabia is **UTC+03:00 with no DST**, and
the forum's days are **Saudi calendar days**. Grouping on the stored value's own
date would silently assign the last three hours of every Saudi day (21:00 to
23:59 UTC) to the previous day, which is a visibly wrong number on the first and
last day of a short forum, exactly where the client looks.

`StatisticsService.GetProgrammeAsync` therefore resolves each `ProgrammeDay.Date`
to an explicit UTC **half-open window** before counting:

```
startUtc = SaudiTime.FromSaudiWallClock(day.Date.ToDateTime(TimeOnly.MinValue))
endUtc   = startUtc.AddDays(1)
predicate: column >= startUtc && column < endUtc
```

Three consequences worth knowing:

1. A record stamped **21:00 UTC belongs to the NEXT Saudi day**. Five boundary
   tests in `StatisticsProgrammeTests.cs` pin this.
2. `SaudiTime.FromSaudiWallClock` is the single `+03:00` conversion point, and
   the comparison is a **plain range predicate against the raw stored column**,
   so an index is still usable. A per-row date-shift function in the `WHERE`
   clause would not be sargable.
3. Half-open `[start, end)` rather than `BETWEEN` removes the midnight
   double-count.

**Sessions are matched to a day BY DATE**, not by a foreign key. That is not an
oversight: `ProgrammeDay` deliberately carries no FK from `Session` (see the XML
documentation on the entity), and the app groups sessions the same way, so the
two surfaces cannot disagree about which session belongs to which day.

## 6. Colour, themes and CSS

Series colours are **tokens**, never named by a component: `--chart-series-1..3`
in [`theme.tokens.css`](../../../src/Shared/SIMF.Components/wwwroot/css/theme.tokens.css),
consumed through `.simf-chart__bar--N`, `.simf-chart__swatch--N` and
`.simf-gauge__fill--N`. The supporting tokens `--chart-grid`, `--chart-axis`,
`--chart-baseline` and `--chart-track` alias existing semantic tokens rather
than introducing new literals. Layout blocks (`.simf-stat-grid`, `.simf-chart*`,
`.simf-gauge*`, `.simf-day-*`) live in `simf-components.css`. No inline styles,
no hardcoded hex outside the token file.

Three themes ship: light (`:root`), `[data-theme="dark"]` and
`[data-theme="grey"]`. The series triple is overridden **only** under dark, so
grey inherits the light triple.

| Theme | Series 1 | Series 2 | Series 3 |
|-------|----------|----------|----------|
| Light and grey | `#2A6FB5` | `#C2410C` | `#1B8A63` |
| Dark | `#4A8CD4` | `#D9683C` | `#2E9D74` |

These were chosen by **computational validation, not by eye** (D-803). Candidate
triples were screened on four numeric gates: a bounded lightness band, a chroma
floor, a colour-vision-deficiency separation floor measured as OKLab dE between
adjacent series under simulated CVD, and a contrast floor against the surface
each triple sits on. Worst adjacent CVD separation is **9.4** (light) and **9.2**
(dark) against a target of 8 or more, and every series clears **3:1** contrast.
The raw brand tokens were tried first and **failed** these checks (too close in
hue and lightness to survive CVD simulation as three distinguishable
categories), so the brand stays in the page chrome and the chart uses a
purpose-built categorical triple. A later brand review is a one-file edit.

## 7. Edge cases, empty and failed states

| Situation | What the page does |
|-----------|--------------------|
| Caller lacks `Statistics.View` | The whole statistics block is absent. No fetch, no error, no empty state. Welcome panel only. |
| **Both** calls fail or return an unsuccessful envelope | The stats surface renders `Dashboard.Stats.Unavailable` ("The live figures could not be loaded. Refresh the page to try again."). |
| **One** call fails | The other still renders. The stat grid shows only that call's tiles; a failed programme call also removes the chart and the day cards. |
| Zero active programme days | The chart figure renders with its heading, legend and `Dashboard.Programme.None` in place of the plot; the day grid is not rendered; the subtitle is blank; `_gaugeMax` falls back to `1`. |
| A day with all-zero figures | `NiceMax(0)` returns `1`, so the bars draw flat on the baseline. This is the honest picture of "no data yet", not a crash and not a full-height bar. |
| Negative or `NaN` values | Coerced to `0` in `GroupedBars`; `GaugeFraction` returns `0` for a non-positive maximum and clamps the fraction to `0..1`. |
| A value above the axis maximum | Bar height is clamped to the plot height (`Math.Min(value / max, 1)`), so a bar can never overshoot the plot. |
| A group too narrow for its gaps | `GroupedBars` falls back to touching bars rather than emitting a negative width. |
| Blank `TitleArabic` under an Arabic UI | The card and the chart category fall back to the English `Title`. |

Known limitations:

- **No refresh and no polling.** The numbers are a load-time snapshot.
- **Query count grows with the number of days.** `GetProgrammeAsync` runs four
  counts per active programme day inside a loop, plus the headline counts. Every
  query is `AsNoTracking` and each is a single COUNT, and a forum has a handful
  of days, so this is bounded in practice, but it is linear rather than a single
  round trip.
- **`Current users` is unfiltered.** It counts every Identity account, including
  admins, which is intentional but is not the same population as `Visitors`.

The Control Panel is a BFF: it does not proxy `/account/api/*` by a catch-all,
every route is declared explicitly. The programme route therefore needed two
pieces of wiring beyond the API endpoint itself, both of which are in place:
`SimfAdminClient.GetStatisticsProgrammeAsync` (the typed call, relative route
`statistics/programme`) and the `group.MapGet("/admin/statistics/programme", ...)`
passthrough in `AccountEndpoints.cs`, which forwards the caller's access token.
Without them the page compiles, the API endpoint answers, and the browser still
gets a 404, so the chart silently disappears. That failure mode was found on a
live render, not by the test suite, which is why the E2E catalogue asserts the
chart is actually present rather than only that the page loads.

## 8. i18n + RTL

Fifteen `Dashboard.*` keys were added to both `Strings.resx` and
`Strings.ar.resx` (`Stats.Heading`, `Stats.Unavailable`, `Stat.CurrentUsers`,
`Stat.Visitors`, `Stat.Staff`, `Stat.Moderators`, `Stat.Exhibitors`,
`Stat.TotalAttended`, `Programme.Heading`, `Programme.Subtitle`,
`Programme.Day`, `Programme.None`, `Programme.Description`, `Series.Registered`,
`Series.Present`, `Series.Attended`), alongside the reused
`Admin.Statistics.Stat.*` keys for the tiles that predate this page. EN and AR
parity is maintained.

RTL behaviour:

- The **plot mirrors**, the text does not. `SimfGroupedBarChart` passes an `rtl`
  flag into `ChartGeometry.GroupedBars`, which mirrors each bar about the plot's
  vertical centre line, flipping both the group order and the bar order inside
  each group. The flag defaults to
  `CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft`, so a caller normally
  passes nothing.
- Because every string is HTML outside the SVG, the legend, axis ticks and
  category labels reorder with the document direction and the lettering is never
  transformed.
- **Numbers are written invariant-culture where they are geometry.** SVG
  coordinates use `0.##` invariant, and the gauge fill percentage likewise: an
  Arabic culture would otherwise emit a decimal comma, which is not a valid SVG
  coordinate or CSS length and would silently break the plot or collapse the
  fill to zero width. Displayed values keep the UI culture (`#,##0`).
- Dates render `dd-MM-yyyy` in every culture (D-770).

## 10. Use cases

- **UC-DASH-LAND** (any admin): sign in and land on a stable, branded home base.
- **UC-DASH-SCAN** (`Statistics.View`): read the standing totals of the event at
  a glance and jump straight into the owning desk from any tile.
- **UC-DASH-PROGRAMME** (`Statistics.View`): compare registered, present and
  attended across the forum days, and drill into a single day's card for its
  three figures plus its session count.

## 11. E2E

See [`docs/tests/e2e/cp-dashboard.md`](../../tests/e2e/cp-dashboard.md).

E2E-DSH-001 to 013 are authored, and cover the landing behaviour and the shell
chrome the page renders through `CpShellLayout` (banner, welcome card,
permission-filtered nav rail, theme toggle, notification bell, profile link,
sign-out, the `/login` redirect, the pending and rejected shell guards, and the
RTL render). Those scenarios were written against the placeholder page and are
still valid.

The statistics scenarios added by this wave (`Statistics.View` holder sees the
grid, chart and day cards; a holder of zero permission codes sees only the
welcome panel; both-calls-fail shows `Dashboard.Stats.Unavailable`;
one-call-fails still renders the other; zero programme days shows
`Dashboard.Programme.None`; Saudi-boundary record lands on the expected day; RTL
mirrors the plot but not the lettering; the three themes each render a legible
chart) continue the same `E2E-DSH-` numbering from 014 and are **not yet
authored**.

Unit coverage: `ChartGeometryTests.cs` (41 tests over `NiceMax`, `AxisTicks`,
`GroupedBars` including the RTL mirror and the degenerate inputs, `MaxValue` and
`GaugeFraction`) and `StatisticsProgrammeTests.cs` (21 tests, including the five
Saudi-day boundary cases).

## 12. Related docs

- Decisions: **D-799** (hand-rolled inline-SVG charts in `SIMF.Components`, and
  the `SimfSvgText` workaround for Razor reserving the literal tag name `text`,
  compiler error RZ1023), **D-800** (additive `GET /admin/statistics/programme`
  rather than widening `StatisticsDashboard`, and the reuse of
  `Statistics.View`), **D-801** (Saudi-day bucketing to explicit UTC windows),
  **D-802** (three series only, sessions as a number), **D-803** (computationally
  validated chart colours as tokens). Context: **D-770** (local time everywhere,
  `dd-MM-yyyy`), **D-157** (App and Identity database separation), **D-132**
  (banner swap).
- Sibling statistics surface: [`admin-statistics.md`](admin-statistics.md) at
  `/admin/statistics`, which reads the same `StatisticsDashboard` payload.
- The day entity this page aggregates over:
  [`admin-programme-days.md`](admin-programme-days.md).
- Chart tokens and the CSS single-source-of-truth rule:
  `docs/dev/CSS_THEME_RULES.md`.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-07-29 | D-799 to D-803 | Wave A. Placeholder replaced by the real dashboard: KPI stat grid, grouped bar chart of Registered / Present / Attended per programme day, and one card per day. New additive contracts `ProgrammeDayStats` + `StatisticsProgramme`, new read-only endpoint `GET /admin/statistics/programme` on the existing `Statistics.View` gate, new `SIMF.Components/Charts` (`ChartGeometry`, `SimfGroupedBarChart`, `SimfBarGauge`, `SimfSvgText`), new `--chart-*` tokens. Per-day counts bucketed on Saudi calendar days via explicit UTC windows. No schema change, no migration, no new permission. |
| 2026-05-28 | D-132 | Banner swapped from `SimfPageHeader` to `SimfBanner`. Page was a placeholder (welcome card only) pending D-134. |

_Last reviewed:_ 2026-07-29 by Claude (Wave A, the CP programme dashboard).
