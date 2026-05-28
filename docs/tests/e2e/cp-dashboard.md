# E2E test catalogue — Dashboard (`/`)

| | |
|--|--|
| **Page** | [`cp/dashboard.md`](../../pages/cp/dashboard.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-DASH-001 | Signed-in user lands on / after TOTP | P0 |
| E2E-DASH-002 | RTL render mirrors banner + welcome panel | P1 |
| E2E-DASH-003 | Unauthenticated → redirect to /login | P0 |

## Scenarios

### E2E-DASH-001 — Landing

```gherkin
Scenario: Signed-in user lands on Dashboard after TOTP
  Given the user has completed /login + /login/totp
  Then they land on /
  And see the SimfBanner with title "Dashboard"
  And see the simf-surface welcome panel
  And no toast errors
  And no console errors
```

### E2E-DASH-002 — RTL

```gherkin
Scenario: Arabic toggle mirrors banner + panel
  Given the admin is on / in English
  When they click "العربية"
  Then <html dir="rtl" lang="ar"> is set
  And the banner reads "لوحة المعلومات"
  And the welcome panel + intro text are Arabic
```

### E2E-DASH-003 — Unauth redirect

```gherkin
Scenario: Anonymous request to / redirects to /login
  Given no auth cookie is set
  When the browser GETs /
  Then it receives a 302 to /login
  And /login renders
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
