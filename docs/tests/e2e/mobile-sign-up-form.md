# E2E test catalogue — `Sign up` (`signUpForm`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Mobile screen #5 — sign-up
> step 1 (credentials). Spec: [`Page_005`](../../App/Page_005/README.md). Runner-agnostic
> Gherkin. The screen glue is widget-tested in
> `src/Mobile/simf_app/test/features/account/sign_up_form_screen_test.dart` (+ the
> golden `test/golden/sign_up_form_golden_test.dart`, 168:3454); the
> controller delegation in
> `src/Mobile/simf_app/packages/simf_auth_pkg/test/auth_controller_signup_test.dart`; the
> repository contract in
> `src/Mobile/simf_app/packages/simf_auth_pkg/test/auth_repository_impl_test.dart`.

| | |
|--|--|
| **Page** | [`Page_005`](../../App/Page_005/README.md) (App page docs) |
| **Route** | app screen #5 `signUpForm` → `/sign-up` |
| **APIs** | `POST /api/v1/app/auth/sign-up` — `SignUpRequest { email, password, confirmPassword }` → **generic 201** `SignUpResponse { email, codeExpiresInSeconds }` (enumeration-resistant, D-198/D-270) |
| **Surface** | Mobile (Flutter) — Guest (creates the account; does **not** sign in) |
| **Auth setup** | None. No token, no `Authorization` header — this screen creates a Guest account. |
| **Last reviewed** | 2026-06-30 (clean-code freeze D-551; behaviour unchanged, icons → SVG glyphs) |

> **Redesigned (D-370, 2026-06-12):** the screen now wears the KSA-Project
> login chrome (Figma 168:3454) — navy surface + sweep, back chevron + globe
> language toggle, logo header, beige card with bordered fields and the gold
> button. All behaviour below is unchanged; scenarios 009/010 cover the new
> chrome controls.

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
| E2E-MOB005-009 | Back chevron pops; with no history it falls back to sign-in | happy | P2 | authored ✓ (widget test, D-370) |
| E2E-MOB005-010 | Globe button toggles AR ↔ EN and persists the choice | i18n | P2 | authored ✓ (widget test, D-370) |
| E2E-MOB005-011 | Valid fields but the mandatory T&C box unchecked → terms error, **no** request sent | error | P0 | authored ✓ (widget test, D-719) |
| E2E-MOB005-012 | Ticking the T&C box clears the error and lets the submit through | happy | P0 | authored ✓ (widget test, D-719) |
| E2E-MOB005-013 | The terms link opens Page 009 (consent mode); موافق auto-checks the box | happy | P1 | authored ✓ (widget test, D-719) |
| E2E-MOB005-014 | Declining on Page 009 (back/رفض) leaves the box unchecked → submit still blocked | edge | P2 | authored ✓ (widget test, D-719) |
| E2E-MOB005-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB005-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

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

### E2E-MOB005-009 — Back chevron (D-370)

```gherkin
Scenario: Backing out of the sign-up form
  Given the guest opened the sign-up form from sign-in
  When they tap the back chevron (top-left, LTR-pinned)
  Then the app returns to the previous screen
  And with no navigation history it falls back to the sign-in screen
```

**Evidence:** `sign_up_form_screen_test` — "the back chevron with no history falls back to sign-in (D-370)".

### E2E-MOB005-010 — Globe language toggle (D-370)

```gherkin
Scenario: Switching language from the sign-up form
  Given the app is in Arabic
  When the guest taps the globe button (top-right)
  Then the UI switches to English
  And the choice is persisted as the preferred language
```

**Evidence:** `sign_up_form_screen_test` — "the globe button toggles and persists the language (D-370)".

### E2E-MOB005-011 — Mandatory T&C gate blocks submit (D-719)

```gherkin
Scenario: Registration requires an explicit accept of the terms
  Given the guest entered a valid email, password and matching confirm
  And the "I accept the terms and conditions" box is left unchecked
  When they tap "Create account"
  Then the terms message "You must accept the terms and conditions" is shown
  And no request is sent to /app/auth/sign-up
```

> The accept is a **checkbox** on registration (owner batch 2026-07-09), not the
> read-only link that the profile / More menu keeps. Consent is client-side only
> (D8) — nothing is added to the frozen sign-up wire contract.

**Evidence:** `sign_up_form_screen_test` — "valid fields but the T&C box unchecked blocks submit with the terms error and never calls sign-up (D-719)".

### E2E-MOB005-012 — Accepting the terms clears the gate (D-719)

```gherkin
Scenario: Ticking the box lets a valid submit through
  Given a blocked submit has shown the terms error
  When the guest ticks "I accept the terms and conditions"
  Then the terms error clears immediately
  And a subsequent "Create account" posts to /app/auth/sign-up
```

**Evidence:** `sign_up_form_screen_test` — "ticking the T&C box clears the error and lets the submit through (D-719)".

### E2E-MOB005-013 — Reading the terms auto-accepts (D-719)

```gherkin
Scenario: The terms link opens Page 009 and موافق ticks the box
  Given the guest is on the sign-up form
  When they tap the underlined "الشروط والأحكام / Terms & conditions" link
  Then the terms screen (Page 009) opens in consent mode
  And tapping موافق returns and auto-checks the accept box
  And "Create account" then proceeds
```

> Reuses the existing `TermsScreen(requireConsent:true)` gate (`/terms?consent=1`,
> D-375) — موافق pops `true`, the back chevron pops `false` (declines).

**Evidence:** `sign_up_form_screen_test` — "the terms link opens Page 009 and موافق auto-checks the box (D-719)".

### E2E-MOB005-014 — Declining the terms does not accept (D-719)

```gherkin
Scenario: Backing out / declining on Page 009 keeps the gate closed
  Given the guest opened the terms screen from the accept link
  When they decline (back chevron / رفض) — the screen pops false
  Then the accept box stays unchecked
  And "Create account" is still blocked with the terms error
```

**Evidence:** `sign_up_form_screen_test` — "declining on Page 009 leaves the box unchecked and still blocks submit (D-719)".

---

_Last reviewed:_ `2026-07-09` by `SIMF Team`.
