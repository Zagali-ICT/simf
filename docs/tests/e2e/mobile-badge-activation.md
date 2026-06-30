# E2E test catalogue — `Badge activation` (`badgeActivation`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Mobile aux auth screen
> (Part B, D-430) — activate a passwordless badge account. Spec:
> [`mobile/badge-activation/README.md`](../../pages/mobile/badge-activation/README.md).
> Runner-agnostic Gherkin. The screen glue is widget-tested in
> `src/Mobile/simf_app/test/features/account/badge_auth_screens_test.dart` (+ the
> render-lock golden `test/golden/badge_activation_golden_test.dart`).

| | |
|--|--|
| **Route** | aux auth `RouteNames.badgeActivation` — pushed from the badge-scan screen |
| **APIs** | `POST /app/auth/badge/activation/start` (`{ qrId, email? }` → masked email) · `POST /app/auth/badge/activation/complete` (`{ qrId, code, newPassword, confirmPassword }`) — both `AllowAnonymous`, `auth` limiter |
| **Surface** | Mobile (Flutter) — passwordless badge holder, not yet signed in |
| **Auth setup** | A scanned badge `qrId`. Codes via the email channel (read from `SIMF_Identity.AccountCodes`, latest unconsumed); **never** a literal code. |
| **Last reviewed** | 2026-06-30 (authored at clean-code freeze D-555) |

## Coverage matrix
| Function | Scenario id |
|---|---|
| Auto-send on open (account has email) → code step | E2E-MOBBADGE-001 |
| Manual email entry (`needsEmail`) → send → code step | E2E-MOBBADGE-002 |
| Complete with a valid code + matching password → sign-in | E2E-MOBBADGE-003 |
| Invalid / expired code → inline error, stays | E2E-MOBBADGE-004 |
| Password policy + confirm-mismatch validation | E2E-MOBBADGE-005 |
| Empty-email / malformed-email validation (manual step) | E2E-MOBBADGE-006 |
| RTL (Arabic) renders correctly | E2E-MOBBADGE-007 |

## Scenarios

```gherkin
Scenario: E2E-MOBBADGE-001 — a badge with an email auto-sends the code on open
  Given a scanned badge whose account already has an email
  When the badge-activation screen opens
  Then a verification code is sent to the masked email shown on screen
  And the code + new-password step is displayed

Scenario: E2E-MOBBADGE-002 — a badge with no email collects one first
  Given a scanned badge whose account has no email ("needsEmail")
  When I enter "holder@example.sa" and tap "send code"
  Then the code is sent to that address
  And the code + new-password step is displayed

Scenario: E2E-MOBBADGE-003 — activating with a valid code and password
  Given the code + new-password step with a valid emailed code
  When I enter the code, a policy-valid new password and a matching confirmation
  And I tap "activate"
  Then the account is activated
  And I am routed to the sign-in screen with a confirmation toast

Scenario: E2E-MOBBADGE-004 — a wrong code is rejected in place
  Given the code + new-password step
  When I enter an incorrect code and tap "activate"
  Then an inline bilingual error is shown
  And I stay on the activation screen

Scenario: E2E-MOBBADGE-005 — password policy and confirm match are enforced
  Given the code + new-password step
  When the new password is shorter than 8 chars or lacks a letter or a digit
  Then a policy error blocks "activate"
  And when the confirmation does not match the password, a mismatch error blocks it

Scenario: E2E-MOBBADGE-006 — the manual email is validated
  Given the email-entry step ("needsEmail")
  When the email is empty or malformed
  Then a required / invalid-email error blocks "send code"

Scenario: E2E-MOBBADGE-007 — RTL render
  Given the device language is Arabic
  Then the screen renders right-to-left with the brand font and no clipped text
```
