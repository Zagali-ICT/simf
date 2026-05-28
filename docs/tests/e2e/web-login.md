# E2E test catalogue — Website sign-in (`/login`)

| | |
|--|--|
| **Page** | [`web/login.md`](../../pages/web/login.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-WEB-LGN-001 | Visitor signs in → /login/verify | P0 |
| E2E-WEB-LGN-002 | Admin tries Website login → "Use the Control Panel" toast | P0 |
| E2E-WEB-LGN-003 | Pending visitor → /account/pending | P0 |
| E2E-WEB-LGN-004 | Rate limit after 5 failed sign-ins | P1 |

## Scenarios

### E2E-WEB-LGN-001 — Visitor signs in

```gherkin
Scenario: Approved visitor signs in to the Website
  Given an Approved visitor V1 with paired TOTP
  When V1 opens /login
  And fills email + password
  And clicks Sign in
  Then they land on /login/verify
  When they fill the current 6-digit code
  And click Verify
  Then they land on /account/profile
  And the cookie holds a fresh access + refresh pair
```

### E2E-WEB-LGN-002 — Admin on Website login

```gherkin
Scenario: Administrator-roled account is rejected on Website /login
  Given Admin A holds Administrator role
  When A opens the Website /login
  And signs in successfully
  Then the server returns the bilingual "Account.SignIn.Error.NotVisitor" message
  And A is told to use the Control Panel at /login (CP)
```

### E2E-WEB-LGN-003 — Pending → /account/pending

```gherkin
Scenario: Pending visitor redirected to state page
  Given visitor V2 is PendingApproval
  When V2 signs in
  Then the server returns AuthRequiresApproval
  And the browser redirects to /account/pending
  And the page shows the holding-page copy
```

### E2E-WEB-LGN-004 — Rate limit

```gherkin
Scenario: 5 wrong passwords trip the per-IP rate limit
  Given V1 has never signed in today
  When V1 submits 5 wrong passwords for any email within 5 minutes from the same IP
  Then the 6th submit returns HTTP 429 + ApiResult.Error.Code="RateLimited"
  And the toast surfaces the bilingual retry-after copy
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
