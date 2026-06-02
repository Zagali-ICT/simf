# E2E test catalogue — Dashboard (`/`)

| | |
|--|--|
| **Page** | [`cp/dashboard.md`](../../pages/cp/dashboard.md) |
| **Route** | `/` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **What this page is.** The Dashboard is the **post-sign-in landing page**
> (`Home.razor`). It is a **placeholder** today — a banner plus a welcome card
> with a one-line intro string; the statistics + live-attendance KPIs are tracked
> under D-134. It has **no CRUD, no forms, no grids, and makes no API calls of its
> own**. The guard is `@attribute [Authorize]` only — the nav item
> `new("Module.Dashboard", "/")` carries **no `RequiredPermission`**, so every
> signed-in, **Approved** user lands here (an Administrator sees it via the `*`
> wildcard; so does a least-privilege admin holding zero permission codes).
> Because the page is ungated, the auth scenario here is **not** a `/not-permitted`
> case — it is the unauthenticated → `/login` redirect plus the `CpShellLayout`
> account-state guards (PendingApproval → `/auth/pending`, Rejected →
> `/auth/rejected`). The "functions" under test are therefore the **shell chrome**
> the page renders through `CpShellLayout` + `SimfAppShell`: the banner, the
> welcome card, the side nav rail (permission-filtered), the language switch, the
> theme toggle, the notification bell, the profile link, and sign-out.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-DSH-001 | Golden path — Approved admin signs in (login + TOTP) and lands on `/` with banner + welcome card | happy | P0 | _to author_ |
| E2E-DSH-002 | Welcome card renders the bilingual `Dashboard.Welcome` + `Dashboard.Intro` copy | happy | P1 | _to author_ |
| E2E-DSH-003 | Side nav rail renders, is permission-filtered, and the Dashboard item is always visible | nav | P0 | _to author_ |
| E2E-DSH-004 | Least-privilege admin (zero permission codes) still lands on `/` (ungated) | auth | P0 | _to author_ |
| E2E-DSH-005 | Theme toggle (light ↔ dark) persists on the Dashboard | function | P1 | _to author_ |
| E2E-DSH-006 | Notification bell opens dropdown / shows empty state on the Dashboard | function | P1 | _to author_ |
| E2E-DSH-007 | Profile link + avatar prefetch (`/account/api/profile`) in the top bar | function | P2 | _to author_ |
| E2E-DSH-008 | Sign-out from the Dashboard returns to `/login` | function | P1 | _to author_ |
| E2E-DSH-009 | Unauthenticated visitor → redirected to `/login` (the auth gate) | auth | P0 | _to author_ |
| E2E-DSH-010 | PendingApproval account → shell guard redirects to `/auth/pending` | auth | P1 | _to author_ |
| E2E-DSH-011 | Rejected account → shell guard redirects to `/auth/rejected` | auth | P1 | _to author_ |
| E2E-DSH-012 | Resilience — avatar prefetch `/account/api/profile` fails → placeholder icon, page still renders | resilience | P2 | _to author_ |
| E2E-DSH-013 | RTL / Arabic render — banner + welcome card + nav rail mirror | i18n | P1 | _to author_ |

## Scenarios

### E2E-DSH-001 — Golden path (sign in → land on Dashboard)

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
- Network: the page itself fires no data call; the shell's avatar prefetch `GET /account/api/profile` returns 200, and the notification bell's unread-count call returns 200
- Audit row: none — landing on the Dashboard is not an audited operation (no `OperationLog` / `RowAudit` row is expected)

### E2E-DSH-002 — Welcome card bilingual copy

```gherkin
Scenario: Welcome card renders the Dashboard.Welcome + Dashboard.Intro strings
  Given an Approved administrator has landed on /
  Then the surface card shows an <h2> equal to "Welcome to the SIMF Control Panel"
  And a paragraph equal to the Dashboard.Intro string
  And there are no buttons, forms, grids or modals on the page (it is a placeholder)
```

### E2E-DSH-003 — Side nav rail (permission-filtered)

```gherkin
Scenario: Nav rail renders and is filtered by permissions, Dashboard always shown
  Given an Approved administrator has landed on /
  Then the side nav rail renders the CpNavigation groups
  And the "Dashboard" item (Href "/") is always visible because its RequiredPermission is null
  And items whose RequiredPermission the user does not hold are hidden
  And an Administrator carrying the "*" wildcard sees every group and every item
  And not-yet-built stub items show the "Soon" badge
```

### E2E-DSH-004 — Least-privilege admin still lands (ungated page)

```gherkin
Scenario: Admin with zero permission codes still reaches the Dashboard
  Given an Approved admin user whose role grants no permission codes (and not the "*" wildcard)
  When they sign in and land on /
  Then they are NOT redirected to /not-permitted
  And the Dashboard banner + welcome card render normally
  And the side nav rail shows only the "Dashboard" item (all gated items are hidden)
```

### E2E-DSH-005 — Theme toggle persists

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

### E2E-DSH-006 — Notification bell

```gherkin
Scenario: Notification bell opens its dropdown from the Dashboard
  Given an Approved administrator has landed on /
  When they click the notification bell in the top bar
  Then a dropdown titled "Notifications" opens
  And when the user has no unread notifications the dropdown shows the empty-state copy
  And a "Mark all read" action and a "View all" link to /account/notifications are present
  And clicking "View all" navigates to /account/notifications
```

### E2E-DSH-007 — Profile link + avatar prefetch

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

### E2E-DSH-008 — Sign-out

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

### E2E-DSH-009 — Auth gate: unauthenticated visitor

```gherkin
Scenario: An unauthenticated visitor cannot see the Dashboard
  Given no SIMF auth cookie is present in the browser
  When the visitor navigates to http://localhost:5158/
  Then the [Authorize] attribute denies access
  And they are redirected to /login
  And no Dashboard banner or welcome card renders
```

### E2E-DSH-010 — Account-state guard: PendingApproval

```gherkin
Scenario: A signed-in but PendingApproval account is bounced from the Dashboard
  Given a user is authenticated but their account_state claim is "PendingApproval"
  When they navigate to /
  Then CpShellLayout.OnInitializedAsync detects the state before any module content renders
  And navigates them to /auth/pending
  And the Dashboard welcome card never renders
```

### E2E-DSH-011 — Account-state guard: Rejected

```gherkin
Scenario: A signed-in but Rejected account is bounced from the Dashboard
  Given a user is authenticated but their account_state claim is "Rejected"
  When they navigate to /
  Then CpShellLayout.OnInitializedAsync navigates them to /auth/rejected
  And the Dashboard welcome card never renders
```

### E2E-DSH-012 — Resilience: avatar prefetch fails

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

### E2E-DSH-013 — RTL / Arabic render

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

---

## Implementation notes

- **Placeholder page — no own API surface.** `Home.razor` renders only a
  `SimfBanner` + a static welcome/intro card from the `Dashboard.*` resx
  strings. There is no list/CRUD endpoint behind it, so there are no
  `tests/SIMF.Api.Tests` cases that target the Dashboard directly. The KPI /
  live-attendance content is deferred to D-134; when it ships, this catalogue
  must be extended with the real data scenarios (loading, empty, error,
  per-widget permission gating).
- **Lower-layer coverage of the guards.** The behaviours exercised here are the
  shared shell guards, not page-specific logic:
  - the `[Authorize]` → `/login` redirect and the `CpShellLayout` account-state
    routing (PendingApproval / Rejected) are the same guards every signed-in CP
    page inherits;
  - the nav-rail permission filter is enforced and unit-asserted by
    `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs` (it fails the
    build if a gated item is missing its `RequiredPermission`), so E2E only needs
    to confirm the visible/hidden behaviour in the browser.
- **Theme-toggle regression guard.** E2E-DSH-005 doubles as the browser
  regression check for the theme-toggle JSException + dark-mode flash fixed at
  commit `a35450d`.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin
  scenario into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be
  created) plus a step-definition class. The Gherkin shape is already
  runner-agnostic.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
