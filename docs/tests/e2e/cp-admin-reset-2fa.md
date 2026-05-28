# E2E test catalogue — Reset user 2FA (`/admin/reset-2fa`)

| | |
|--|--|
| **Page** | [`cp/admin-reset-2fa.md`](../../pages/cp/admin-reset-2fa.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-2FA-001 | Reset normal user → success + email sent + audit row | P0 |
| E2E-2FA-002 | Self-reset rejected (use /account/profile instead) | P0 |
| E2E-2FA-003 | Email not found → no rows + no API call | P1 |

## Scenarios

### E2E-2FA-001 — Reset normal user

```gherkin
Scenario: Reset another admin's 2FA
  Given Admin A is signed in
  And user B exists with paired TOTP + valid sessions
  When A opens /admin/reset-2fa
  And types "b@example" into the search field
  And picks B from the dropdown
  And clicks "Reset 2FA"
  Then a confirmation modal opens
  When A confirms
  Then POST /account/api/admin/users/{B.Id}/reset-2fa fires
  And B's authenticator secret + recovery codes are wiped
  And every active session for B is revoked
  And an email is sent to B (FakeEmailSender captures it in tests)
  And the audit log records Admin.UserTwoFactorReset by A on B
  And the toast confirms success
```

### E2E-2FA-002 — Self-reset blocked

```gherkin
Scenario: Cannot reset your own 2FA from here
  Given Admin A is signed in
  When A types their own email and clicks "Reset 2FA"
  Then the server returns HTTP 400 with bilingual "Self-reset must use /account/profile"
  And no session revocation happens
```

### E2E-2FA-003 — Email not found

```gherkin
Scenario: Unknown email yields no rows
  Given the admin searches for "nobody@nowhere.example"
  Then the dropdown shows "No user matches"
  And no /account/api/admin/users/search response body lists matches
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
