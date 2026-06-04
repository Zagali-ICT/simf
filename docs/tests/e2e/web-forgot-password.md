# E2E test catalogue — Forgot password (Web) (`/forgot-password`)

| | |
|--|--|
| **Page** | [`web/forgot-password.md`](../../pages/web/forgot-password.md) |
| **Route** | `/forgot-password` |
| **Surface** | Website |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | None — this is an **anonymous** Website page. To assert the round-trip (email → code → reset) you need a real visitor account; provision an Approved visitor `visitor@simf.test` (verified, 2FA on) and read the emitted reset code from `SIMF_Identity.AccountCodes` (`Purpose = PasswordReset`, plaintext in dev). The `superadmin@zagali-ict.com` + `Get-Totp` helper is only needed for the audit-row check on the CP audit page. |
| **Last reviewed** | 2026-06-02 |

> **Grounding note (why this is a rebuild).** The previous catalogue claimed the
> page shows in-place "If the email exists, a code was sent." success copy, a
> 15-minute TTL, and a "3 submits / minute" cap. All three are stale:
> - `ForgotPassword.razor` on success sets `SimfAuthSession.PendingEmail` and
>   **navigates to `/reset-password`** (`Nav.NavigateTo("/reset-password")`) — there
>   is no success banner on this page.
> - `PasswordService.ResetCodeLifetime = TimeSpan.FromMinutes(10)` → the response
>   carries `CodeExpiresInSeconds = 600` (**10 min**, not 15). The page's own
>   supporting text only promises "a 6-digit code" with no TTL.
> - Two distinct caps exist: the per-email **rate limit** is `EmailPermitLimit = 5`
>   per `EmailWindowSeconds = 60` (5/min, HTTP 429), and the per-account
>   **reset-code issuance** cap is `MaxResetCodesPerWindow = 5` per `ResetRequestWindow = 1 hour`
>   (silent — still HTTP 200, no code emailed past the 5th).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WFP-001 | Golden path — submit a known visitor email → redirect to `/reset-password`, code emailed (10-min TTL) | happy | P0 | _to author_ |
| E2E-WFP-002 | Anti-enumeration — unknown email behaves identically (redirect, no code) | happy | P0 | _to author_ |
| E2E-WFP-003 | "Send code" button — loading state + double-submit guard | happy | P1 | _to author_ |
| E2E-WFP-004 | "Back to sign in" link → `/login` | happy | P2 | _to author_ |
| E2E-WFP-005 | Language switch toggles culture + `redirectUri=/forgot-password` | i18n | P1 | _to author_ |
| E2E-WFP-006 | Theme toggle (light ↔ dark persists) | happy | P2 | _to author_ |
| E2E-WFP-007 | Empty email → client validation "Enter your email address." | error | P1 | _to author_ |
| E2E-WFP-008 | Email without `@` → client validation "Enter a valid email address." | error | P1 | _to author_ |
| E2E-WFP-009 | Anonymous access — page is reachable with no sign-in (no `/not-permitted`) | auth | P0 | _to author_ |
| E2E-WFP-010 | Disabled / Rejected account — same redirect, no code issued | error | P1 | _to author_ |
| E2E-WFP-011 | Per-account issuance cap — 6th request in an hour emails no code (still redirects) | error | P1 | _to author_ |
| E2E-WFP-012 | Per-email rate limit — 6th submit / minute → HTTP 429 + bilingual error alert | resilience | P1 | _to author_ |
| E2E-WFP-013 | Server unreachable / 500 → bilingual transport fallback alert, no redirect | resilience | P2 | _to author_ |
| E2E-WFP-014 | RTL / Arabic render mirrors the card + fields | i18n | P1 | _to author_ |

## Scenarios

### E2E-WFP-001 — Golden path (known email → `/reset-password`)

```gherkin
Feature: Forgot-password request golden path
  As a visitor who forgot their password
  I want to request a reset code by email
  So that I can set a new password on /reset-password

Background:
  Given the API is reachable on http://localhost:5175
  And the Website is reachable on http://localhost:5115
  And an Approved, email-verified visitor "visitor@simf.test" exists (2FA on)
  And no unconsumed PasswordReset AccountCode exists for that visitor
  And the browser is on /forgot-password (anonymous — no sign-in needed)

Scenario: A known visitor email is accepted and the browser moves to /reset-password
  Given the card title reads "Reset your password"
  And the supporting text reads "Enter your email and we'll send a 6-digit code."
  When the visitor fills Email="visitor@simf.test"
  And clicks "Send code"
  Then the button shows the loading label "Sending code"
  And the BFF forwards POST /api/v1/auth/forgot-password with body {"Email":"visitor@simf.test"}
  And the API returns HTTP 200 with ApiResult.Success = true
  And ApiResult.Data.CodeExpiresInSeconds = 600
  And the browser navigates to /reset-password
  And SimfAuthSession.PendingEmail = "visitor@simf.test" (the email is pre-bound on /reset-password)
  And exactly one new AccountCode row exists for the visitor with Purpose=PasswordReset, a 6-digit Code, and ExpiresAt = CreatedAt + 10 minutes
  And the FakeEmailSender (dev) captured one "SIMF password reset" email to visitor@simf.test
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/web-forgot-password-golden-before.png` (the form with the email filled)
- Screenshot after: `docs/screenshots/web-forgot-password-golden-after.png` (landed on `/reset-password`)
- Console errors: 0 expected
- Network: the single `POST /api/v1/auth/forgot-password` (via the Website BFF) returns 200
- Audit row: `OperationLog` row with `EventType = AuditEvents.ForgotPasswordRequested`, `Outcome = Success`, `SubjectEmail = visitor@simf.test`, and the resolved `SubjectUserId`
- In-app trail: a `NotificationKind.CredentialPasswordResetRequested` notification row for the visitor

### E2E-WFP-002 — Anti-enumeration (unknown email behaves identically)

```gherkin
Scenario: An unknown email produces the same outcome and reveals nothing
  Given no account exists for "ghost@example.com"
  When the visitor opens /forgot-password
  And fills Email="ghost@example.com"
  And clicks "Send code"
  Then the API returns HTTP 200 with ApiResult.Success = true
  And ApiResult.Data.CodeExpiresInSeconds = 600 (identical to the known-email response)
  And the browser navigates to /reset-password (identical to the golden path)
  And no AccountCode row is created
  And FakeEmailSender captured no email
  And the response body, status, timing and redirect are indistinguishable from E2E-WFP-001
  And an OperationLog row records EventType=ForgotPasswordRequested, Outcome=Failure, ErrorCode=AuthAccountNotFound (server-side only — not surfaced to the client)
```

### E2E-WFP-003 — "Send code" loading + double-submit guard

```gherkin
Scenario: A second click while a request is in flight is ignored
  Given the visitor has filled Email="visitor@simf.test"
  When they click "Send code"
  And immediately click "Send code" again before the first request resolves
  Then the button is in its Loading state showing "Sending code"
  And only ONE POST /api/v1/auth/forgot-password request is fired (HandleSubmitAsync returns early while _loading is true)
  And after the response the redirect to /reset-password happens once
```

### E2E-WFP-004 — "Back to sign in" link

```gherkin
Scenario: The secondary link returns to the sign-in page
  Given the visitor is on /forgot-password
  When they click the "Back to sign in" link
  Then the browser navigates to /login
  And no forgot-password request is fired
```

### E2E-WFP-005 — Language switch

```gherkin
Scenario: The language switch flips culture and returns to /forgot-password
  Given the visitor is on /forgot-password in English
  When they click the language switch link
  Then the browser is sent to /culture?culture=ar&redirectUri=%2Fforgot-password
  And after the culture cookie is set the browser returns to /forgot-password
  And the page renders in Arabic with <html dir="rtl" lang="ar">
  And the language switch link now targets culture=en
```

### E2E-WFP-006 — Theme toggle

```gherkin
Scenario: The theme toggle switches and persists the colour scheme
  Given the visitor is on /forgot-password in the light theme
  When they click the SimfThemeToggle control
  Then the document switches to data-theme="dark"
  And the choice persists across a page reload (no light-mode flash, per commit a35450d)
```

### E2E-WFP-007 — Empty email (client validation)

```gherkin
Scenario: Submitting an empty email shows the required-field message without a request
  Given the visitor is on /forgot-password
  When they leave Email blank
  And click "Send code"
  Then the SimfTextField shows the validation message "Enter your email address." (ar: "أدخل بريدك الإلكتروني.")
  And NO POST /api/v1/auth/forgot-password request is fired (client validation short-circuits before Api.ForgotPasswordAsync)
  And the message clears as soon as the field is edited (OnFieldChanged → ClearFieldError)
```

### E2E-WFP-008 — Email without `@` (client validation)

```gherkin
Scenario: An email missing the @ sign shows the invalid-format message without a request
  Given the visitor is on /forgot-password
  When they fill Email="visitorsimf.test"
  And click "Send code"
  Then the SimfTextField shows the validation message "Enter a valid email address." (ar: "أدخل بريدًا إلكترونيًا صالحًا.")
  And NO POST /api/v1/auth/forgot-password request is fired
```

### E2E-WFP-009 — Anonymous access (no auth gate)

```gherkin
Scenario: The page is reachable without any sign-in
  Given the browser has no SIMF auth cookie
  When it navigates directly to /forgot-password
  Then the page renders the reset-your-password form with HTTP 200
  And there is NO redirect to /login or /not-permitted (the endpoint is AllowAnonymous; the page has no RequirePermission attribute)
```

### E2E-WFP-010 — Disabled / Rejected account

```gherkin
Scenario: A disabled account is treated like an unknown one
  Given an account "disabled@simf.test" exists with AccountState=Disabled
  When the visitor submits Email="disabled@simf.test"
  Then the API returns HTTP 200 with ApiResult.Success = true (CodeExpiresInSeconds = 600)
  And the browser navigates to /reset-password
  And no AccountCode row is created (the service skips Disabled and Rejected states)
  And FakeEmailSender captured no email
```

### E2E-WFP-011 — Per-account issuance cap (silent, still 200)

```gherkin
Scenario: The 6th reset request for one account within an hour emails no code
  Given "visitor@simf.test" has already been issued 5 PasswordReset codes within the last hour
  When the visitor submits Email="visitor@simf.test" a 6th time
  Then the API still returns HTTP 200 with ApiResult.Success = true
  And the browser still navigates to /reset-password (the response never reveals the cap)
  And NO new AccountCode row is created (recentCodes >= MaxResetCodesPerWindow=5)
  And FakeEmailSender captured no new email
  And an OperationLog row records EventType=ForgotPasswordRequested, Outcome=Failure, ErrorCode=RATE_LIMIT_EXCEEDED
```

### E2E-WFP-012 — Per-email rate limit (HTTP 429)

```gherkin
Scenario: A 6th submit within 60 seconds for one email trips the per-email limiter
  Given the "auth-email" partition is configured EmailPermitLimit=5 per EmailWindowSeconds=60
  When the same email is submitted 6 times within 60 seconds (any source, any IP)
  Then the 6th POST /api/v1/auth/forgot-password returns HTTP 429
  And ApiResult.Success = false with ApiResult.Error.Code = "RATE_LIMIT_EXCEEDED"
  And the page does NOT redirect; the SimfAlert error renders the bilingual MessageForCurrentCulture()
  And the visitor stays on /forgot-password
```

### E2E-WFP-013 — Server unreachable / 500

```gherkin
Scenario: An unreachable API surfaces the transport fallback alert
  Given the API at http://localhost:5175 is stopped (or returns a non-JSON 500 page)
  When the visitor submits Email="visitor@simf.test"
  And clicks "Send code"
  Then SimfAuthClient maps the failure to a failed ApiResult (ErrorCodes.InternalError)
  And the page renders the SimfAlert error "The SIMF service could not be reached. Please try again." (ar: "تعذّر الوصول إلى خدمة SIMF. حاول مرة أخرى.")
  And the browser does NOT navigate to /reset-password
  And the button returns from its loading state (the finally block clears _loading)
```

### E2E-WFP-014 — RTL / Arabic render

```gherkin
Scenario: The Arabic culture mirrors the card and fields
  Given the visitor opens /forgot-password with the Arabic culture cookie set
  Then the document is <html dir="rtl" lang="ar">
  And the card title reads "إعادة تعيين كلمة المرور"
  And the supporting text reads "أدخل بريدك الإلكتروني وسنرسل لك رمزًا مكوّنًا من ستة أرقام."
  And the submit button reads "إرسال الرمز" (loading: "جارٍ الإرسال")
  And the secondary link reads "العودة إلى تسجيل الدخول"
  And the email field, icon and label are mirrored to the RTL layout
```

---

## Implementation notes

- **Lower-layer coverage already exists.** `tests/SIMF.Api.Tests/PasswordTests.cs`
  covers the API surface this page drives, without a browser:
  - `Forgot_password_gives_the_same_response_whether_or_not_the_account_exists`
    (E2E-WFP-001 / -002 anti-enumeration)
  - `Forgot_password_for_a_disabled_account_issues_no_code` (E2E-WFP-010)
  - `Forgot_password_stops_issuing_codes_after_the_per_account_cap`
    (E2E-WFP-011 — asserts exactly 5 codes after 7 requests)
  - `Reset_password_*` and `A_completed_password_reset_writes_an_audit_entry`
    cover the downstream `/reset-password` step (catalogued in
    [`web-reset-password.md`](web-reset-password.md)).
  These prove the service behaviour; the E2E scenarios above add the browser
  layer — the form, client validation, loading/redirect, 429 surfacing, theme
  and RTL — that the integration tests cannot see.
- **Reading the dev code.** In a local run the emitted 6-digit code is stored
  in plaintext in `SIMF_Identity.AccountCodes` (`Purpose = PasswordReset`); read
  it there to chain E2E-WFP-001 into the `/reset-password` flow without a real
  inbox. See the [SIMF mobile dev-run recipe] memory note for the same pattern.
- **Anonymous page — no permission gate.** Unlike CP admin pages there is no
  `RequirePermission` attribute and no `/not-permitted` redirect; the auth
  scenario (E2E-WFP-009) instead asserts the page is reachable signed-out.
- **Convert to Playwright** when the runner is adopted: each Gherkin block maps
  to a `.feature` scenario + step definitions under `tests/SIMF.E2E.Tests/`
  (project to be created). The steps are already runner-agnostic.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
