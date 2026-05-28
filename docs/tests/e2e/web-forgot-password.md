# E2E test catalogue — Forgot password (Web) (`/forgot-password`)

| | |
|--|--|
| **Page** | [`web/forgot-password.md`](../../pages/web/forgot-password.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

Mirrors the CP forgot-password scenarios with namespace `WEB-FPW`:

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-WEB-FPW-001 | Submit valid email → success + email arrives | P0 |
| E2E-WEB-FPW-002 | Submit unknown email → success (anti-enumeration) | P0 |
| E2E-WEB-FPW-003 | Rate limit after 3 submits / minute / email | P1 |

## Scenarios

### E2E-WEB-FPW-001 — Valid email

```gherkin
Scenario: Visitor requests a password reset
  Given visitor V1 exists
  When V1 opens /forgot-password
  And submits V1's email
  Then the page shows the success copy "If the email exists, a code was sent."
  And FakeEmailSender captures a 6-digit code addressed to V1 (15-min TTL)
```

### E2E-WEB-FPW-002 — Anti-enumeration

```gherkin
Scenario: Unknown email shows the same success copy
  Given no visitor with email "ghost@example.com" exists
  When someone submits "ghost@example.com"
  Then the page shows the same success copy
  And no email is sent
  And no information leaks that the email is unknown
```

### E2E-WEB-FPW-003 — Rate limit

```gherkin
Scenario: 3 submits within a minute trip the rate limit
  When the same email is submitted 3 times within 60 seconds
  Then the 4th submit returns HTTP 429
  And the bilingual retry-after toast appears
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
