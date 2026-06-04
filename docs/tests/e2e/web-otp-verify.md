# E2E test catalogue — Verify OTP (Website) (`/login/verify`)

| | |
|--|--|
| **Page** | [`web/otp-verify.md`](../../pages/web/otp-verify.md) |
| **Route** | `/login/verify` |
| **Surface** | Website |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | A verified **visitor** account with 2FA on and **no** authenticator key paired → the email-OTP flavour. The 6-digit code is read from the dev DB: latest unconsumed row in `SIMF_Identity.AccountCodes` with `Purpose = SignInOtp` (plaintext in dev). `superadmin@zagali-ict.com` + TOTP is **not** used here — that account is an Admin and is barred from the visitor surfaces (`AuthWrongSurfaceWeb`). |
| **Last reviewed** | 2026-06-02 |

> **Flow context.** This page is the visitor second-factor step. It is reachable
> **only** mid-sign-in: the `/login` password step must have returned an
> `OtpToken` and set `Session.PendingOtpToken`. On a cold load with no pending
> token the page client-side-redirects to `/login` (see `OnInitialized`). The
> page renders a single 6-digit code input (`SimfCodeField`), a **Verify**
> button, a **Back to sign in** link, and the language/theme controls. On
> success it hands off to `/auth/complete?reference=…` (`forceLoad`) which writes
> the auth cookie and lands the visitor on `/account`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WOT-001 | Golden path — correct emailed code → cookie set → `/account` | happy | P0 | _to author_ |
| E2E-WOT-002 | "Code sent to {masked email}" info banner renders on entry | happy | P1 | _to author_ |
| E2E-WOT-003 | Cold load with no pending token redirects to `/login` (auth gate) | auth | P0 | _to author_ |
| E2E-WOT-004 | Client validation — non-6-digit code blocks submit, no network call | error | P1 | _to author_ |
| E2E-WOT-005 | Wrong code → `AuthOtpInvalid` 400 + bilingual error alert | error | P0 | _to author_ |
| E2E-WOT-006 | Expired code → `AuthOtpExpired` 400 + bilingual error alert | error | P1 | _to author_ |
| E2E-WOT-007 | Five wrong codes cap the ticket → `AuthOtpTokenInvalid` 400 | error | P1 | _to author_ |
| E2E-WOT-008 | "Back to sign in" link returns to `/login` | nav | P2 | _to author_ |
| E2E-WOT-009 | Server 500 on `/auth/verify-otp` → bilingual fallback alert | resilience | P2 | _to author_ |
| E2E-WOT-010 | RTL / Arabic render mirrors the card | i18n | P1 | _to author_ |

## Scenarios

### E2E-WOT-001 — Golden path

```gherkin
Feature: Visitor OTP verification golden path
  As a verified visitor with 2FA on (email-OTP flavour)
  I want to enter the 6-digit code emailed during sign-in
  So that I land on my account with a real session cookie

Background:
  Given the API is reachable on http://localhost:5175
  And the Website is reachable on http://localhost:5115
  And a verified visitor account exists with TwoFactorEnabled = true and no authenticator key
  And the visitor has completed the /login password step
  And the BFF set Session.PendingOtpToken and redirected the browser to /login/verify

Scenario: Correct emailed code completes the sign-in
  Given the page shows the "Verification" card with one "Verification code" field, helper "The code is six digits.", and a "Verify" button
  And an info SimfAlert reads "We've sent a code to {masked-email}." (e.g. "We've sent a code to v•••@example.com.")
  And I read the latest unconsumed code from SIMF_Identity.AccountCodes where Purpose = SignInOtp for this visitor (e.g. "482913")
  When I type "482913" into the "Verification code" field
  And I click "Verify"
  Then the button shows the "Verifying" loading label
  And POST /account/api/auth/verify-otp fires once with body { OtpToken, Code: "482913" } and returns 200
  And the ApiResult.Data contains a non-empty AccessToken, RefreshToken and TokenType = "Bearer"
  And the browser force-loads /auth/complete?reference={guid}
  And it then lands on /account with the visitor's session cookie set
  And the visitor's email appears in the account header
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/web-otp-verify-golden-before.png`
- Screenshot after: `docs/screenshots/web-otp-verify-golden-after.png`
- Console errors: 0 expected
- Network: the single `/account/api/auth/verify-otp` call returns 200; the
  `/auth/complete?reference=…` navigation returns 200 and ends on `/account`
- Audit row: no failure row for this flow; the consumed `AccountCodes` row is now
  `ConsumedAt`-stamped and the `SecondFactorToken` ticket is consumed (single-use)

### E2E-WOT-002 — Code-sent info banner

```gherkin
Scenario: The masked-email info banner renders on entry
  Given Session.PendingEmail holds the signing-in visitor's email
  And no error has occurred yet
  When the /login/verify page renders
  Then an info SimfAlert is shown above the code field
  And it reads "We've sent a code to {masked}." in English / "تم إرسال رمز إلى {masked}." in Arabic
  And the masked email is produced by SimfAuthSession.MaskEmail (first character + masked local part + domain kept)
```

### E2E-WOT-003 — Cold-load auth gate

```gherkin
Scenario: Opening /login/verify without a pending OTP token redirects to /login
  Given there is no active sign-in (Session.PendingOtpToken is null)
  When I navigate directly to /login/verify
  Then OnInitialized detects the missing token
  And the browser is redirected to /login
  And the verification card is never shown
  And no /account/api/auth/verify-otp request fires
```

### E2E-WOT-004 — Client-side validation

```gherkin
Scenario: A code that is not exactly six digits is blocked before any network call
  Given the /login/verify page is shown with a valid pending token
  When I type "123" into the "Verification code" field
  And I click "Verify"
  Then the field shows the inline error "Enter the 6-digit code." (EN) / "أدخل الرمز المكوّن من ستة أرقام." (AR)
  And the input is marked aria-invalid="true"
  And NO POST /account/api/auth/verify-otp request fires

  When I clear the field and type "12ab56"
  And I click "Verify"
  Then the same inline error is shown (non-digit characters rejected client-side: Code.All(char.IsAsciiDigit))
  And NO POST /account/api/auth/verify-otp request fires
```

### E2E-WOT-005 — Wrong code

```gherkin
Scenario: A wrong but well-formed code surfaces the bilingual server error
  Given the /login/verify page is shown with a valid pending token
  And the real emailed code is "482913"
  When I type "000000" into the "Verification code" field
  And I click "Verify"
  Then POST /account/api/auth/verify-otp fires once and the API returns HTTP 400
  And ApiResult.Error.Code = "AuthOtpInvalid"
  And an error SimfAlert appears at the top of the card reading "The code is not correct." (EN) / "الرمز غير صحيح." (AR)
  And the page stays on /login/verify
  And the ticket AttemptCount is incremented (one wrong attempt counted)
```

### E2E-WOT-006 — Expired code

```gherkin
Scenario: An expired code is rejected with the expiry message
  Given the /login/verify page is shown with a valid pending token
  And the latest SignInOtp AccountCodes row has ExpiresAt in the past
  When I type that expired code into the "Verification code" field
  And I click "Verify"
  Then POST /account/api/auth/verify-otp returns HTTP 400
  And ApiResult.Error.Code = "AuthOtpExpired"
  And an error SimfAlert reads "The code has expired. Sign in again to get a new one." (EN) / "انتهت صلاحية الرمز. سجّل الدخول مرة أخرى للحصول على رمز جديد." (AR)
  And the page stays on /login/verify
```

### E2E-WOT-007 — Attempt cap

```gherkin
Scenario: Five wrong codes cap the ticket and reject it
  Given the /login/verify page is shown with a valid pending token
  When I submit a wrong well-formed code "000001" five times in a row
  Then each of the first five calls returns 400 with Error.Code = "AuthOtpInvalid"
  And on the sixth submit the API returns 400 with Error.Code = "AuthOtpTokenInvalid"
  And the error SimfAlert reads "The sign-in session is not valid." (EN) / "جلسة تسجيل الدخول غير صالحة." (AR)
  And re-entering the correct code no longer works (the ticket AttemptCount has reached the cap of MaxSecondFactorAttempts = 5)
  And the visitor must use "Back to sign in" to obtain a fresh ticket and code
```

### E2E-WOT-008 — Back to sign in

```gherkin
Scenario: The secondary link returns to the password step
  Given the /login/verify page is shown
  When I click "Back to sign in" (EN) / "العودة إلى تسجيل الدخول" (AR)
  Then the browser navigates to /login
  And no /account/api/auth/verify-otp request fires
```

### E2E-WOT-009 — Server 500 resilience

```gherkin
Scenario: A 500 from verify-otp shows the bilingual fallback, not a crash
  Given the /login/verify page is shown with a valid pending token
  And the API is configured to return HTTP 500 on /auth/verify-otp (e.g. DB down)
  When I type a well-formed code and click "Verify"
  Then POST /account/api/auth/verify-otp returns 500
  And result.Success is false with no Data
  And an error SimfAlert reads the fallback "Verification could not be completed. Please try again." (EN) / "تعذّر إكمال التحقق. حاول مرة أخرى." (AR)
  And the page stays on /login/verify with the field re-enabled (the _loading flag is cleared in finally)
  And no unhandled exception bubbles to the browser console
```

### E2E-WOT-010 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the verification card
  Given the /login/verify page is shown in English with a valid pending token
  When I click the language switch in the top controls (Href "/culture?culture=ar&redirectUri=%2Flogin%2Fverify")
  Then the page reloads with <html dir="rtl" lang="ar"> and stays on /login/verify (redirectUri preserves the step)
  And the card title reads "التحقق"
  And the supporting text reads "أدخل الرمز المكوّن من ستة أرقام الذي أرسلناه إلى بريدك."
  And the field label reads "رمز التحقق" with helper "يتكوّن الرمز من ستة أرقام."
  And the submit button reads "تحقّق"
  And the "Back to sign in" link reads "العودة إلى تسجيل الدخول"
  And the 6-digit input is still entered left-to-right (SimfCodeField keeps LTR regardless of page direction)
```

---

## Implementation notes

- **API integration tests** at `tests/SIMF.Api.Tests/SignInTests.cs` cover the
  same `POST /api/v1/auth/verify-otp` surface at a lower layer (no browser):
  - `A_visitor_signs_in_and_completes_with_the_emailed_code` → E2E-WOT-001 (golden round-trip, 200 + tokens)
  - `Verify_otp_with_a_wrong_code_returns_400` → E2E-WOT-005 (`AuthOtpInvalid`)
  - `Verify_otp_rejects_the_ticket_after_five_wrong_codes` → E2E-WOT-007 (cap = 5 → `AuthOtpTokenInvalid`)
  - `An_otp_token_used_at_verify_totp_is_rejected` → cross-flow guard (an OTP
    ticket cannot be replayed at the TOTP endpoint)
  The browser E2E adds what the API tests cannot: the client-side 6-digit
  validation (E2E-WOT-004), the cold-load redirect (E2E-WOT-003), the masked-email
  banner (E2E-WOT-002), the cookie hand-off via `/auth/complete` (E2E-WOT-001),
  the 500 fallback alert (E2E-WOT-009), and the RTL render (E2E-WOT-010).
- **Email-OTP, not TOTP.** This is the visitor email-code flavour: the
  `SignInService` routes a visitor with no paired authenticator key and no roles
  to `SecondFactorKind.EmailOtp`. The code is a 6-digit numeric stored in
  `SIMF_Identity.AccountCodes` (`Purpose = SignInOtp`), readable in plaintext in
  dev. Visitors who DID pair an authenticator complete via TOTP at the CP-style
  flow instead — out of scope for this page.
- **Auth gate nuance.** Unlike a CP page, `/login/verify` is `AllowAnonymous`
  and is not gated by a `PermissionCatalog` permission. Its "auth gate" is the
  cold-load redirect to `/login` when there is no pending OTP token
  (E2E-WOT-003), plus the API-side audience guard (`AuthWrongSurfaceWeb`) that
  bars Admin accounts from the visitor surfaces.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin
  scenario into a `.feature` file under `tests/SIMF.E2E.Tests/`. The shapes are
  already runner-agnostic. Seed the pending-token state by driving `/login`
  first, then assert against `/login/verify`.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
