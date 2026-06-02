# E2E test catalogue — Reset password (Web) (`/reset-password`)

| | |
|--|--|
| **Page** | [`web/reset-password.md`](../../pages/web/reset-password.md) |
| **Route** | `/reset-password` |
| **Surface** | Website |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | Anonymous page — no sign-in. A live 6-digit reset code is obtained from the `forgot-password` flow + the dev mailbox / `SIMF_Identity.AccountCodes` table (codes are plaintext in dev) |
| **Last reviewed** | 2026-06-02 |

> **Auth note.** Unlike a CP admin page, `/reset-password` is **anonymous** —
> the emailed 6-digit code is the bearer. There is **no `PermissionCatalog`
> gate and no `/not-permitted` redirect**. The endpoint
> `POST /api/v1/auth/reset-password` is `AllowAnonymous()` and the page renders
> `InteractiveServerNoPrerender`. The "auth gate" equivalent here is the
> server-side authorisation that the *code itself* enforces: a missing /
> wrong / expired / over-attempted code is rejected by the API
> (`AUTH_RESET_CODE_INVALID` / `AUTH_RESET_CODE_EXPIRED`), never the page.
> The page is reached by completing `/forgot-password` (which sets
> `Session.PendingEmail` and navigates here); it is also directly reachable.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WRS-001 | Golden path — valid code + strong matching password → confirmation card + "Go to sign in" → `/login` | happy | P0 | _to author_ |
| E2E-WRS-002 | Pre-filled email + masked info alert when arriving from `/forgot-password` | happy | P1 | _to author_ |
| E2E-WRS-003 | Direct visit with no `PendingEmail` — empty form, no info alert | happy | P2 | _to author_ |
| E2E-WRS-004 | Client validation — bad email format → field error, no POST | error | P1 | _to author_ |
| E2E-WRS-005 | Client validation — code not 6 digits → field error, no POST | error | P1 | _to author_ |
| E2E-WRS-006 | Client validation — weak password (too short / no digit / no letter) → field error, no POST | error | P1 | _to author_ |
| E2E-WRS-007 | Client validation — confirm password mismatch → field error, no POST | error | P1 | _to author_ |
| E2E-WRS-008 | Wrong code → API 400 `AUTH_RESET_CODE_INVALID` → bilingual error alert, form stays | error | P0 | _to author_ |
| E2E-WRS-009 | Expired code (> lifetime) → API 400 `AUTH_RESET_CODE_EXPIRED` → bilingual error alert | error | P1 | _to author_ |
| E2E-WRS-010 | Attempt-cap reached → API 400 `AUTH_RESET_CODE_INVALID` ("too many attempts") | error | P1 | _to author_ |
| E2E-WRS-011 | Server 500 / transport failure → bilingual fallback "The password could not be reset." | resilience | P2 | _to author_ |
| E2E-WRS-012 | "Back to sign in" secondary link → `/login` | nav | P2 | _to author_ |
| E2E-WRS-013 | Theme toggle + language switch in TopControls (round-trip preserves `/reset-password`) | i18n | P2 | _to author_ |
| E2E-WRS-014 | RTL / Arabic render — card, fields, alerts mirror to `dir="rtl"` | i18n | P1 | _to author_ |

## Scenarios

### E2E-WRS-001 — Golden path

```gherkin
Feature: Reset password golden path
  As a visitor who requested a password reset
  I want to set a new password with the emailed 6-digit code
  So that I can sign in again

Background:
  Given the API is reachable on http://localhost:5175
  And the Website is reachable on http://localhost:5115
  And an account "visitor@example.com" exists and is approved
  And a fresh password-reset code has been issued for that account
  And the latest unconsumed PasswordReset code is read from SIMF_Identity.AccountCodes (dev plaintext) as "482915"

Scenario: Valid code + strong matching password resets the password
  Given the visitor opens http://localhost:5115/reset-password
  Then the card title reads "Set a new password"
  And the supporting text reads "Enter the code we sent, then choose a new password."
  And four fields are shown: Email, Verification code, New password, Confirm new password
  And the submit button reads "Update password"
  When they fill Email="visitor@example.com"
  And they fill Verification code="482915"
  And they fill New password="Maritime2026"
  And they fill Confirm new password="Maritime2026"
  And they click "Update password"
  Then the button shows the loading label "Updating password"
  And a POST fires to http://localhost:5175/api/v1/auth/reset-password
  And the request body is { "email":"visitor@example.com", "code":"482915", "newPassword":"Maritime2026", "confirmPassword":"Maritime2026" }
  And the API responds HTTP 200 with ApiResult.Data = { "passwordReset": true }
  And the session pending state is cleared (Session.Clear())
  And the card swaps to the confirmation view
  And it shows the check-circle icon and the title "Password updated"
  And the text "You can now sign in with your new password."
  And a single button "Go to sign in"
  When they click "Go to sign in"
  Then the browser navigates to /login
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/web-reset-password-golden-before.png`
- Screenshot after (confirmation card): `docs/screenshots/web-reset-password-golden-after.png`
- Console errors: 0 expected
- Network: the single `POST /api/v1/auth/reset-password` returns 200; no `/account/api/*` call fires (the client posts straight to the API base on the server-side circuit)
- Audit row: audit row with `Event = 'PasswordResetCompleted'` (`AuditOutcome.Success`) and the account's id
- Side effects: the reset code row in `AccountCodes` is marked consumed (`ConsumedAt` set); a security-notice notification (`NotificationKind.AccountPasswordResetCompleted`, Warning) + email are dispatched; all sessions for the account are ended

### E2E-WRS-002 — Pre-filled email + masked info alert

```gherkin
Scenario: Arriving from /forgot-password pre-fills the email and shows the masked info alert
  Given the visitor completed /forgot-password for "visitor@example.com"
  And was redirected to /reset-password with Session.PendingEmail = "visitor@example.com"
  When the /reset-password page loads
  Then the Email field is pre-filled with "visitor@example.com" (Session.PendingEmail copied in OnInitialized)
  And a blue info alert reads "We've sent a code to v******r@example.com." (masked via SimfAuthSession.MaskEmail)
  And no error alert is shown
```

### E2E-WRS-003 — Direct visit, no pending email

```gherkin
Scenario: Direct visit shows an empty form and no info alert
  Given there is no Session.PendingEmail (the page is opened directly, fresh circuit)
  When the visitor opens /reset-password
  Then the Email field is empty
  And no info alert and no error alert are shown
  And the four fields and the "Update password" button are present and enabled
```

### E2E-WRS-004 — Bad email format (client validation)

```gherkin
Scenario: Invalid email is rejected client-side with no network call
  Given the visitor is on /reset-password
  When they fill Email="not-an-email"
  And they fill Verification code="482915"
  And they fill New password="Maritime2026"
  And they fill Confirm new password="Maritime2026"
  And they click "Update password"
  Then a field validation message appears under Email reading "Enter a valid email address."
  And no POST to /api/v1/auth/reset-password fires
  And the form stays on the same view
```

### E2E-WRS-005 — Code not 6 digits (client validation)

```gherkin
Scenario: A code that is not exactly 6 ASCII digits is rejected client-side
  Given the visitor is on /reset-password
  When they fill Email="visitor@example.com"
  And they fill Verification code="123"
  And they fill New password="Maritime2026"
  And they fill Confirm new password="Maritime2026"
  And they click "Update password"
  Then a field validation message appears under Verification code reading "Enter the 6-digit code."
  And no POST to /api/v1/auth/reset-password fires
```

### E2E-WRS-006 — Weak password (client validation)

```gherkin
Scenario Outline: Passwords failing the policy are rejected client-side
  Given the visitor is on /reset-password
  When they fill Email="visitor@example.com"
  And they fill Verification code="482915"
  And they fill New password="<password>"
  And they fill Confirm new password="<password>"
  And they click "Update password"
  Then a field validation message appears under New password reading "<message>"
  And no POST to /api/v1/auth/reset-password fires

  Examples:
    | password      | message                                  |
    | Ab1           | Password must be at least 8 characters.  |
    | abcdefghij    | Password must contain a number.          |
    | 1234567890    | Password must contain a letter.          |
```

### E2E-WRS-007 — Confirm password mismatch (client validation)

```gherkin
Scenario: Mismatched confirm password is rejected client-side
  Given the visitor is on /reset-password
  When they fill Email="visitor@example.com"
  And they fill Verification code="482915"
  And they fill New password="Maritime2026"
  And they fill Confirm new password="Maritime2027"
  And they click "Update password"
  Then a field validation message appears under Confirm new password reading "The passwords do not match."
  And no POST to /api/v1/auth/reset-password fires
```

### E2E-WRS-008 — Wrong code (server rejection)

```gherkin
Scenario: A well-formed but incorrect code is rejected by the API
  Given a fresh reset code exists for "visitor@example.com"
  And the visitor is on /reset-password
  When they fill Email="visitor@example.com"
  And they fill Verification code="000000" (passes the 6-digit client check but is not the real code)
  And they fill New password="Maritime2026"
  And they fill Confirm new password="Maritime2026"
  And they click "Update password"
  Then a POST fires to /api/v1/auth/reset-password
  And the API responds HTTP 400 with ApiResult.Error.Code = "AUTH_RESET_CODE_INVALID"
  And a red error alert (SimfAlert variant=error) appears at the top of the form
  And it reads the bilingual server message "The reset code is not correct." (EN) / "رمز إعادة التعيين غير صحيح." (AR)
  And the form stays on the same view with fields intact
  And the server increments the code's AttemptCount and writes an audit row Event = 'PasswordResetCodeIncorrect' (Failure)
```

### E2E-WRS-009 — Expired code (server rejection)

```gherkin
Scenario: A code past its lifetime is rejected as expired
  Given a reset code for "visitor@example.com" whose ExpiresAt is in the past
  And the visitor is on /reset-password
  When they fill Email="visitor@example.com"
  And they fill Verification code="482915"
  And they fill New password="Maritime2026"
  And they fill Confirm new password="Maritime2026"
  And they click "Update password"
  Then the API responds HTTP 400 with ApiResult.Error.Code = "AUTH_RESET_CODE_EXPIRED"
  And a red error alert reads "The reset code has expired. Request a new one." (EN) / "انتهت صلاحية رمز إعادة التعيين. اطلب رمزًا جديدًا." (AR)
  And the form stays on the same view
  And an audit row Event = 'PasswordResetCodeExpired' (Failure) is written
```

### E2E-WRS-010 — Attempt cap reached (server rejection)

```gherkin
Scenario: Once the attempt cap is hit the code is locked out
  Given the reset code for "visitor@example.com" already has AttemptCount >= MaxResetAttempts
  And the visitor is on /reset-password
  When they fill Email="visitor@example.com"
  And they fill Verification code="482915"
  And they fill New password="Maritime2026"
  And they fill Confirm new password="Maritime2026"
  And they click "Update password"
  Then the API responds HTTP 400 with ApiResult.Error.Code = "AUTH_RESET_CODE_INVALID"
  And a red error alert reads "Too many incorrect attempts. Request a new reset code." (EN) / "محاولات غير صحيحة كثيرة. اطلب رمز إعادة تعيين جديدًا." (AR)
  And an audit row Event = 'PasswordResetAttemptCapReached' (Failure) is written
```

### E2E-WRS-011 — Server 500 / transport failure (resilience)

```gherkin
Scenario: A 500 or transport failure surfaces the bilingual fallback
  Given the API is configured to return HTTP 500 on /api/v1/auth/reset-password (e.g. DB down)
  And the visitor is on /reset-password
  When they fill a valid Email + 6-digit Code + matching strong password
  And they click "Update password"
  Then SimfAuthClient maps the failure to a failed ApiResult envelope
  And a red error alert appears reading the fallback "The password could not be reset. Please try again." (EN) / "تعذّر إعادة تعيين كلمة المرور. حاول مرة أخرى." (AR)
  And the loading state clears and the button is interactive again
  And the form stays on the same view
```

### E2E-WRS-012 — Back to sign in

```gherkin
Scenario: The secondary link returns to the sign-in page
  Given the visitor is on /reset-password
  When they click the "Back to sign in" link
  Then the browser navigates to /login
```

### E2E-WRS-013 — Theme toggle + language switch

```gherkin
Scenario: Header controls work and the culture round-trip preserves the route
  Given the visitor is on /reset-password in English (light theme)
  When they click the SimfThemeToggle
  Then the theme switches (data-theme flips) with no full reload and no console error
  When they click the language switch labelled "العربية"
  Then the browser navigates to /culture?culture=ar&redirectUri=%2Freset-password
  And after the culture round-trip the visitor lands back on /reset-password in Arabic
```

### E2E-WRS-014 — RTL / Arabic render

```gherkin
Scenario: Arabic render mirrors the card to RTL
  Given the visitor is on /reset-password
  When they switch the language to Arabic (via the language switch)
  Then the document is <html dir="rtl" lang="ar">
  And the page title reads "تعيين كلمة مرور جديدة · ..." (Auth.PageTitle.Reset, AR)
  And the card title reads "تعيين كلمة مرور جديدة"
  And the supporting text reads "أدخل الرمز الذي أرسلناه، ثم اختر كلمة مرور جديدة."
  And the field labels are Arabic (كلمة المرور الجديدة / تأكيد كلمة المرور الجديدة / رمز التحقق)
  And the submit button reads "تحديث كلمة المرور"
  And the "Back to sign in" link reads its Arabic label
  And the form layout mirrors (icons / actions on the mirrored side)
  When a wrong code is submitted in Arabic
  Then the error alert renders the Arabic message "رمز إعادة التعيين غير صحيح."
```

---

## Implementation notes

- **No `/account/api` proxy on this path.** `SimfAuthClient` posts directly to
  the API base (`Api:BaseUrl`, http://localhost:5175) at the relative path
  `api/v1/auth/reset-password` — the call runs server-side on the Blazor Server
  circuit, so the network assertion is "one `POST /api/v1/auth/reset-password`
  returns 200", not a BFF call. Source:
  `src/Shared/SIMF.ApiClient/SimfAuthClient.cs` (`BasePath = "api/v1/auth/"`).
- **Client-side validation mirrors the API** (`ResetPassword.razor` →
  `ValidatePassword` + the email/code/confirm checks) so most validation
  scenarios (WRS-004..007) never reach the network. The server-side
  `StrongPassword` rule (`src/Backend/SIMF.Api/Endpoints/Auth/Validators/PasswordRules.cs`)
  and `ResetPasswordRequestValidator.cs` enforce the same policy (8–128 chars,
  ≥1 digit, ≥1 letter; code `^\d{6}$`; password ≠ email; confirm == new) as
  the second line of defence.
- **Error codes** are `AUTH_RESET_CODE_INVALID` and `AUTH_RESET_CODE_EXPIRED`
  (`src/Shared/SIMF.Common/ErrorCodes.cs`); both return HTTP 400. The service
  (`src/Backend/SIMF.Application/IdentityAccess/PasswordService.cs`)
  deliberately returns the same generic `AUTH_RESET_CODE_INVALID` for the
  not-found / wrong-code / attempt-capped cases while logging the specific
  audit event — so the catalogue asserts the audit `Event` key, not a
  per-reason error code.
- **API integration tests cover the same surface at a lower layer (no browser):**
  - `tests/SIMF.Api.Tests/PasswordTests.cs` — reset-password happy + error paths
    (posts `/api/v1/auth/reset-password`).
  - `tests/SIMF.Api.Tests/PasswordResetExpiryTests.cs` — expired-code path
    (`AUTH_RESET_CODE_EXPIRED`).
  When E2E covers a scenario, the matching `Api.Tests` case can eventually be
  thinned — but during the transition keep both layers.
- **Convert to Playwright** when adopted: each Gherkin scenario maps to a
  `.feature` + step-definition under `tests/SIMF.E2E.Tests/` (project to be
  created). The steps are already runner-agnostic.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
