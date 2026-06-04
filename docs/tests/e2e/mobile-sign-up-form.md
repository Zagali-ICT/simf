# E2E test catalogue — `Sign up` (`signUpForm`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Mobile screen #5 — sign-up
> step 1 (credentials). Spec: [`Page_005`](../../App/Page_005/README.md). Runner-agnostic
> Gherkin. The screen glue is widget-tested in
> `src/Mobile/simf_app/test/features/auth/sign_up_form_screen_test.dart`; the
> controller delegation in
> `src/Mobile/packages/simf_auth_pkg/test/auth_controller_signup_test.dart`; the
> repository contract in
> `src/Mobile/packages/simf_auth_pkg/test/auth_repository_impl_test.dart`.

| | |
|--|--|
| **Page** | [`Page_005`](../../App/Page_005/README.md) (App page docs) |
| **Route** | app screen #5 `signUpForm` → `/sign-up` |
| **APIs** | `POST /api/v1/app/auth/sign-up` — `SignUpRequest { email, password, confirmPassword }` → **generic 201** `SignUpResponse { email, codeExpiresInSeconds }` (enumeration-resistant, D-198/D-270) |
| **Surface** | Mobile (Flutter) — Guest (creates the account; does **not** sign in) |
| **Auth setup** | None. No token, no `Authorization` header — this screen creates a Guest account. |
| **Last reviewed** | 2026-06-04 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB005-001 | Valid email + password + matching confirm → generic 201 → email-OTP screen carrying the trimmed/lower-cased email | happy | P0 | authored ✓ (widget test) |
| E2E-MOB005-002 | Confirm-password mismatch → inline error, **no** request sent | error | P0 | authored ✓ (widget test) |
| E2E-MOB005-003 | Invalid email → inline error, submit blocked, no request | error | P1 | authored ✓ (widget test) |
| E2E-MOB005-004 | Weak password (policy fail) → inline error, submit blocked, no request | error | P1 | authored ✓ (widget test) |
| E2E-MOB005-005 | Already-registered email → **same** generic 201 → same email-OTP screen (no "you already have an account") | edge | P0 | authored (D-198; repo `_guard` test) |
| E2E-MOB005-006 | Wire failure (network / 5xx / 429) → message shown, form kept, no navigation | resilience | P1 | authored ✓ (widget test) |
| E2E-MOB005-007 | "Have an account? Sign in" leaves the sign-up flow → sign-in | happy | P1 | authored ✓ (widget test) |
| E2E-MOB005-008 | RTL render (Arabic) — labels/errors/button mirror; the email field stays LTR | i18n | P1 | authored (screen) |

## Scenarios

### E2E-MOB005-001 — Golden path: create → email-OTP

```gherkin
Feature: Sign-up step 1 (credentials)
Scenario: A new visitor creates an account and is sent to verify their email
  Given a guest opens the sign-up form
  When they enter a valid email "Visitor@Example.SA"
  And a password "Password1"
  And a matching confirm password "Password1"
  And they tap "Create account"
  Then the app POSTs { email, password, confirmPassword } to /app/auth/sign-up
  And on the generic 201 it shows the "Check your email" toast
  And it navigates to the email-OTP screen (Page 006) carrying email "visitor@example.sa"
```

**Evidence:** `sign_up_form_screen_test` — "valid input creates the account and routes to the email-OTP screen carrying the trimmed/lower-cased email"; `auth_controller_signup_test` — "delegates to the repository and never signs the user in".

### E2E-MOB005-002 — Confirm-password mismatch is caught client-side

```gherkin
Scenario: Mismatched confirm blocks the call
  Given the guest entered a valid email and password "Password1"
  When they enter a confirm password "Password2"
  And they tap "Create account"
  Then the field shows "The passwords do not match."
  And no request is sent to /app/auth/sign-up
```

> Note (D-270): the confirm check is client-side for instant feedback **and**
> `confirmPassword` is included in the body when the call is made — the server
> re-validates `confirmPassword == password` (`SignUpRequestValidator`).

**Evidence:** `sign_up_form_screen_test` — "mismatched confirm shows the error and never calls sign-up".

### E2E-MOB005-003 — Invalid email blocks submit

```gherkin
Scenario: Malformed email is rejected locally
  Given the guest types "not-an-email"
  And a valid password + matching confirm
  When they tap "Create account"
  Then the email field shows "Invalid email"
  And no request is sent
```

**Evidence:** `sign_up_form_screen_test` — "an invalid email blocks submit".

### E2E-MOB005-004 — Weak password blocks submit

```gherkin
Scenario: A password failing the policy is rejected locally
  Given the guest enters a valid email
  And a password "short" (below the length/complexity policy)
  When they tap "Create account"
  Then the password field shows "Password does not meet the requirements"
  And no request is sent
```

> The client mirror is length ≥ 8 + a letter + a digit (SIMF-MOB-API-001); the
> server re-validates and is the authority.

**Evidence:** `sign_up_form_screen_test` — "a weak password blocks submit".

### E2E-MOB005-005 — Enumeration resistance (D-198)

```gherkin
Scenario: An already-registered email looks identical to a new one
  Given the email "taken@example.sa" already has an account
  When the guest submits valid credentials for it
  Then the server returns the same generic 201 (never a 409)
  And the app shows the same "Check your email" / email-OTP screen
  And there is no "you already have an account" message anywhere
```

> HARD RULE (Page_005 Logic L-4): the endpoint never returns `409` and never
> varies the body between new and existing; the Flutter "already registered"
> branch is dead code and must not be re-introduced.

**Evidence:** repository `_guard` behaviour + `RegistrationService.SignUpAsync`
(generic 201) covered by `tests/SIMF.Api.Tests` sign-up cases; client treats
every 201 identically (`SignUpFormScreen._submit`).

### E2E-MOB005-006 — A wire failure keeps the form

```gherkin
Scenario: The server rejects or the network is down
  Given the guest submits valid credentials
  When the call fails (network unavailable / 5xx / 429 / a 400 the client missed)
  Then the screen shows the failure message (or the offline message)
  And the form is kept with its values so the user can retry
  And it does not navigate to the email-OTP screen
```

**Evidence:** `sign_up_form_screen_test` — "a wire failure surfaces the message and keeps the form".

### E2E-MOB005-007 — Leave to sign-in

```gherkin
Scenario: A returning user bails out
  When the guest taps "Have an account? Sign in"
  Then the app navigates to the sign-in screen (Page 003)
```

**Evidence:** `sign_up_form_screen_test` — "the Sign in link leaves the sign-up flow".

### E2E-MOB005-008 — RTL render (Arabic)

```gherkin
Scenario: The form mirrors under Arabic
  Given the app language is Arabic
  When the sign-up form is shown
  Then labels, inline errors, and the "Create account" button mirror right-to-left
  And the email field keeps LTR so the address reads correctly
```

> By construction: the screen uses localized `AppL10n` strings + Material RTL;
> the email `TextFormField` pins `textDirection: TextDirection.ltr`.

---

_Last reviewed:_ `2026-06-04` by `SIMF Team`.
