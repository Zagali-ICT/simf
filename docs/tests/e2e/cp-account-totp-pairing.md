# E2E test catalogue — TOTP pairing (`/account/totp-pairing`)

| | |
|--|--|
| **Page** | [`cp/account-totp-pairing.md`](../../pages/cp/account-totp-pairing.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-TPP-001 | Scan QR + enter code → success + 10 codes shown | P0 |
| E2E-TPP-002 | Manual-entry secret works (no scanner) | P1 |
| E2E-TPP-003 | Wrong code → retry within session | P1 |
| E2E-TPP-004 | Continue after recovery codes → / | P0 |

## Scenarios

### E2E-TPP-001 — Scan QR + verify

```gherkin
Scenario: First-time TOTP pairing via QR scan
  Given a fresh admin signs in for the first time
  And lands on /account/totp-pairing
  When they scan the rendered QR with Google Authenticator
  And the authenticator app starts producing 6-digit codes for SIMF
  When they fill the current code into the Verify field
  And click "Pair"
  Then POST /account/api/auth/totp/pair returns 200
  And the page reveals 10 single-use recovery codes
  And the user can copy / print them
  When they click "Continue"
  Then they land on / (the Dashboard)
```

### E2E-TPP-002 — Manual-entry secret

```gherkin
Scenario: Manual-entry secret works when scanner unavailable
  Given the QR is rendered
  When the user types the base32 secret into their authenticator manually
  And enters the resulting code
  Then pairing succeeds identically to E2E-TPP-001
```

### E2E-TPP-003 — Wrong code

```gherkin
Scenario: Wrong code allows retry within the session
  Given the QR is rendered
  When the user enters an invalid code
  And clicks "Pair"
  Then a SimfAlert error appears
  And the server-held pairing secret stays in the session
  When the user enters the correct code within the session TTL
  Then pairing succeeds
```

### E2E-TPP-004 — Continue

```gherkin
Scenario: Continue after recovery codes lands on Dashboard
  Given pairing succeeded and the 10 codes are shown
  When the user clicks "Continue"
  Then they land on /
  And the next sign-in will use /login/totp normally
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
