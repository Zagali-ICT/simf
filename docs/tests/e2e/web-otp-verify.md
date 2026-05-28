# E2E test catalogue — Website OTP verify (`/login/verify`)

| | |
|--|--|
| **Page** | [`web/otp-verify.md`](../../pages/web/otp-verify.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-WEB-OTP-001 | Valid TOTP → /account/profile | P0 |
| E2E-WEB-OTP-002 | Wrong code → bilingual toast, retry | P0 |
| E2E-WEB-OTP-003 | Email-OTP path → code emailed and verified | P1 |

## Scenarios

### E2E-WEB-OTP-001 — Valid TOTP

```gherkin
Scenario: Valid TOTP completes Website sign-in
  Given V1 just passed /login (password step)
  When they fill the current 6-digit TOTP code
  Then they redirect to /account/profile
```

### E2E-WEB-OTP-002 — Wrong code

```gherkin
Scenario: Wrong code shows bilingual error, allows retry
  Given V1 is on /login/verify
  When they fill an invalid code and Verify
  Then a SimfAlert error renders
  And the URL stays /login/verify
  When they fill the correct code within the 5-min ticket TTL
  Then they sign in
```

### E2E-WEB-OTP-003 — Email-OTP

```gherkin
Scenario: Visitor opted into email-OTP receives + verifies a code
  Given V1's account has SecondFactorKind="EmailOtp"
  When V1 passes /login
  Then the server emails a 6-digit code (15-min TTL)
  And the user enters it on /login/verify
  And sign-in completes
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
