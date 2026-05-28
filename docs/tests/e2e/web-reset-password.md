# E2E test catalogue — Reset password (Web) (`/reset-password`)

| | |
|--|--|
| **Page** | [`web/reset-password.md`](../../pages/web/reset-password.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-WEB-RST-001 | Valid code + strong password → success + redirect to /login | P0 |
| E2E-WEB-RST-002 | Wrong code → bilingual error | P0 |
| E2E-WEB-RST-003 | Weak password → bilingual complexity message | P1 |
| E2E-WEB-RST-004 | Expired code (>15 min) → invalid | P1 |

## Scenarios

### E2E-WEB-RST-001 — Reset golden

```gherkin
Scenario: Visitor resets password successfully
  Given V1 received a fresh 6-digit reset code in their email
  When V1 opens /reset-password
  And fills the code + a new password "Bb@123456789012" (12 chars + complexity)
  And clicks Reset password
  Then the server validates, replaces password atomically (D-014), revokes all prior sessions, audits Auth.PasswordReset
  And the page shows success + redirects to /login
  When V1 signs in with the new password
  Then sign-in succeeds (TOTP still required)
```

### E2E-WEB-RST-002 — Wrong code

```gherkin
Scenario: Wrong reset code is rejected
  Given V1 has a valid code, but submits a wrong one
  Then HTTP 400 + ApiResult.Error.Code="ResetCode.Invalid"
  And the password is not changed
```

### E2E-WEB-RST-003 — Weak password

```gherkin
Scenario: Weak new password fails complexity
  Given V1 has a valid code
  When V1 fills new password "weak"
  Then HTTP 400 + bilingual complexity message
  And the password is not changed
```

### E2E-WEB-RST-004 — Expired code

```gherkin
Scenario: Code older than 15 minutes is rejected
  Given V1's code is 16 minutes old
  When V1 attempts reset
  Then HTTP 400 + ApiResult.Error.Code="ResetCode.Expired"
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
