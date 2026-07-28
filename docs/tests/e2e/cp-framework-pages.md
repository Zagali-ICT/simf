# E2E test catalogue — CP framework pages (`/not-permitted`, `/not-found`, `/Error`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7).
> Grouped in one file the way [`cp-auth-flow.md`](cp-auth-flow.md) groups the six
> auth routes: these three pages share a shape (a title, one sentence, one way
> back), none of them owns a form or a grid, and all three were rewritten
> together in the §6.16 audit (NAV-004 / NAV-005, and NAV-001 for the routing
> that reaches `/not-permitted`).

| | |
|--|--|
| **Page** | [`PAGE-INDEX.md` → CP framework / error pages](../../pages/PAGE-INDEX.md) |
| **Route** | `/not-permitted` · `/not-found` · `/Error` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell driver _(or: Playwright when adopted)_ |
| **Auth setup** | Two identities are required — see below |
| **Last reviewed** | `2026-07-28` |

## Why this file needs two identities

`/not-permitted` cannot be reached by the account most CP tests use. The seeded
super-admin carries the `*` wildcard permission, which satisfies **every** gate,
so it can never be forbidden anything. Driving these scenarios needs a second,
**restricted** admin.

The seeder already creates non-Administrator CP roles with narrow grants
(`AppRoles.CpRoles`); `GateOperator` holds only `Gates.Operate` and
`Gates.ViewOwnReports`. The fixture is a clone of the super-admin identity row —
same `PasswordHash`, `SecurityStamp` and 2FA columns, so it signs in identically
— carrying `GateOperator` instead of `Administrator`. Full recipe, including the
`sqlcmd` `SET QUOTED_IDENTIFIER ON` requirement, is in the §6.16 audit evidence.

| Identity | Role | Used for |
|---|---|---|
| `superadmin@zagali-ict.com` + TOTP via `Get-Totp` | Administrator (`*`) | `/not-found`, `/Error` |
| restricted fixture (e.g. `qa-gate@…`) + same TOTP | GateOperator | every `/not-permitted` scenario |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-FRM-001 | A forbidden page sends a signed-in admin to `/not-permitted`, **not** `/login` | auth | P0 | ✅ passed 2026-07-28 |
| E2E-FRM-002 | The denied admin is still signed in — session survives the denial | auth | P0 | ✅ passed 2026-07-28 |
| E2E-FRM-003 | A page the role **does** hold still opens (not a blanket denial) | auth | P0 | ✅ passed 2026-07-28 |
| E2E-FRM-004 | The side menu offers only routes the role can open | auth | P0 | ✅ passed 2026-07-28 |
| E2E-FRM-005 | `/not-permitted` renders in the CP shell, localized, with a way back | i18n | P1 | ✅ passed 2026-07-28 |
| E2E-FRM-006 | An unknown `/m/{module}` slug is a 404, not a fake "Coming soon" | error | P1 | ✅ passed 2026-07-28 |
| E2E-FRM-007 | A declared stub module still shows "Coming soon" | happy | P1 | ✅ passed 2026-07-28 |
| E2E-FRM-008 | `/not-found` renders in the shell with a way back | error | P1 | _to author_ |
| E2E-FRM-009 | `/Error` renders in the shell and leaks no environment guidance | resilience | P2 | _to author_ |
| E2E-FRM-010 | All three render correctly in Arabic RTL with no horizontal overflow | i18n | P1 | ✅ passed 2026-07-28 (`/not-permitted`) |

## Scenarios

### E2E-FRM-001 — A forbidden page sends a signed-in admin to `/not-permitted`

```gherkin
Feature: Permission denial does not look like a logout
  As an admin whose role lacks a permission
  I want to be told I am not permitted
  So that I do not think my session expired and try to sign in again

Background:
  Given a CP admin account exists in the GateOperator role
  And that role holds Gates.Operate and Gates.ViewOwnReports only
  And the admin is signed in, having completed the TOTP second factor

Scenario Outline: a forbidden route lands on /not-permitted
  When the admin navigates to "<route>"
  Then the browser URL path is "/not-permitted"
  And the query carries "ReturnUrl=<route>"
  And the URL path is NOT "/login"

  Examples:
    | route                  |
    | /admin/site-settings   |
    | /admin/sessions        |
    | /admin/admins          |
    | /admin/statistics      |
    | /admin/announcements   |
```

> **Why this is P0.** Before §6.16 (NAV-001) the `<NotAuthorized>` branch of
> `AuthorizeRouteView` redirected to `/login` for *both* the unauthenticated and
> the authenticated-but-forbidden case, so a permission denial was
> indistinguishable from a session expiry — and `/not-permitted` was unreachable
> by page routing at all.

### E2E-FRM-002 — The denied admin is still signed in

```gherkin
Scenario: a denial does not end the session
  Given the admin is on "/not-permitted" after a forbidden navigation
  Then the header shows the signed-in user's display name
  And a "Sign out" control is present
  And no sign-in form is rendered
```

### E2E-FRM-003 — A permitted page still opens

```gherkin
Scenario: the denial is a permission decision, not a blanket block
  Given the admin is signed in in the GateOperator role
  When the admin navigates to "/admin/gates/operator"
  Then the page opens at "/admin/gates/operator"
  And the URL path is NOT "/not-permitted"
```

> Without this, E2E-FRM-001 would still pass against a build that forbade
> *everything* — which is why it is P0 alongside it.

### E2E-FRM-004 — The menu offers only what the role can open

```gherkin
Scenario: the side menu promises exactly what the pages honour
  Given the admin is signed in in the GateOperator role
  When the dashboard is rendered
  Then the side navigation links are exactly:
    | /                       |
    | /admin/gates/operator   |
    | /m/live-sessions        |
  And the navigation does NOT offer "/admin/site-settings"
  And the navigation does NOT offer "/admin/announcements"
  And the navigation does NOT offer "/admin/statistics"
```

> **This check is impossible as the super-admin.** Its `*` wildcard satisfies the
> menu gate and the page gate alike, so a disagreement between the two (NAV-006:
> nav weaker than page; NAV-007: nav stricter than page) looks correct to it.

### E2E-FRM-005 — `/not-permitted` renders in the shell, localized, with a way back

```gherkin
Scenario: the denial page is a CP page, not a bare framework stub
  Given the admin is on "/not-permitted"
  Then the CP shell chrome is present (header, side navigation)
  And the page title is the localized "not permitted" title, not a resx key
  And the body explains the denial and suggests contacting an administrator
  And a link back to the dashboard is present and resolves to "/"
```

### E2E-FRM-006 — An unknown module slug is a 404

```gherkin
Feature: The /m/{Module} catch-all does not invent modules
  The route is a catch-all and used to render "Coming soon" for ANY value, so a
  mistyped URL produced a confident, correctly-shelled page announcing a module
  that does not exist and is not coming — worse than a 404, because the reader
  waits for it.

Scenario: a slug CpNavigation does not declare is not-found
  Given an admin is signed in
  When the admin navigates to "/m/does-not-exist"
  Then the page title is the localized "page not found" title
  And the "Coming soon" panel is NOT rendered
  And a link back to the dashboard is present
```

### E2E-FRM-007 — A declared stub module still shows "Coming soon"

```gherkin
Scenario: a real stub is unaffected by the catch-all tightening
  Given an admin is signed in
  When the admin navigates to "/m/live-sessions"
  Then the page shows that module's own localized title
  And the "Coming soon" panel IS rendered
```

### E2E-FRM-008 — `/not-found` renders in the shell with a way back

```gherkin
Scenario: a mistyped CP URL is recoverable
  Given an admin is signed in
  When the admin navigates to "/admin/no-such-page"
  Then the not-found page renders inside the CP shell
  And its title and body are localized, not raw resx keys
  And a link back to the dashboard is present
```

> Before §6.16 (NAV-005) this was the untouched Blazor scaffold: a bare `<h3>`
> outside the CP shell, hardcoded English, with no way back.

### E2E-FRM-009 — `/Error` leaks no environment guidance

```gherkin
Scenario: the production error page addresses the admin, not a developer
  Given an unhandled exception has occurred
  When the admin is shown "/Error"
  Then the page renders inside the CP shell
  And its text is localized, not raw resx keys
  And the page does NOT mention "ASPNETCORE_ENVIRONMENT"
  And the page does NOT instruct the reader to switch to the Development environment
  And the request/correlation id is shown when one is available
```

> **Why the last two matter.** `UseExceptionHandler` targets this page from the
> non-Development branch, so it is what a Control Panel admin actually lands on
> in production. The scaffold text told the reader how to turn on Development
> mode — guidance addressed to a developer, which also advertises how to make a
> deployment leak exception detail (NAV-004 / LOC-008).

### E2E-FRM-010 — Arabic RTL render

```gherkin
Scenario Outline: the framework pages render correctly in Arabic
  Given the CP language is Arabic
  When "<route>" is rendered
  Then documentElement dir is "rtl"
  And scrollWidth equals clientWidth (no horizontal overflow)
  And no raw resx key is visible in the rendered text
  And the browser console reports zero errors

  Examples:
    | route           |
    | /not-permitted  |
    | /not-found      |
    | /Error          |
```

## Execution record

**2026-07-28 — E2E-FRM-001..007 and E2E-FRM-010 (`/not-permitted` only) executed**
against a local QA stack: throwaway LocalDB, API on 5275, CP on 5278, SMTP
pointed at localhost. Signed in through the real UI including the TOTP second
factor, as a `GateOperator` fixture account.

- Five forbidden routes each landed on `/not-permitted?ReturnUrl=…`; the one
  route the role holds opened normally.
- The denied page kept the signed-in header and a sign-out control.
- Side navigation offered exactly `/`, `/admin/gates/operator`, `/m/live-sessions`.
- `/m/does-not-exist` → localized "page not found"; `/m/live-sessions` → its own
  module title with the "Coming soon" panel.
- Console: zero errors. `scrollWidth == clientWidth`. `dir="rtl"`.

E2E-FRM-008 and E2E-FRM-009 are **authored but not executed** — `/not-found` was
exercised only through the `/m/` catch-all, and `/Error` needs a deliberately
induced unhandled exception, which was out of scope for that session.
