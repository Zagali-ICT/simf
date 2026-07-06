# E2E test catalogue — `Registration status` (`registrationStatus`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7).
> First file of the **mobile** catalogue — the App build is now landing
> endpoints (D-249), so the mobile screens are catalogued as their backing
> APIs ship. Runner-agnostic Gherkin; the API lives in
> `tests/SIMF.Api.Tests/CurrentUserEndpointTests.cs`. The **Flutter screen is built**
> (D-292) and widget-tested in
> `src/Mobile/simf_app/test/features/registration/registration_status_screen_test.dart`
> (pending / approved→home / rejected / error→retry / sign-out); the throwing
> `refreshCurrentUser` it uses is covered in
> `src/Mobile/packages/simf_auth_pkg/test/auth_controller_refresh_user_test.dart`.

| | |
|--|--|
| **Page** | [`Page_011`](../../App/Page_011/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/users/me` · app screen #11 `/registration/status` |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | A signed-in visitor token via the sign-up → verify-email flow (`AuthFlow.SignInVisitorWithoutTwoFactorAsync`); `AuthFlow.SetAccountState` forces the lifecycle state. **No literal secrets.** |
| **Last reviewed** | 2026-06-03 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB011-001 | Pending account reads `registrationStatus = Pending` + identity + `appRole = Visitor` | happy | P0 | authored ✓ (`Me_returns_identity_app_role_and_pending_status_for_a_verified_visitor`) |
| E2E-MOB011-002 | Approved account reads `Approved` → screen routes to Home | happy | P0 | authored ✓ (`Me_reflects_an_approved_account_as_Approved`) |
| E2E-MOB011-003 | Rejected account reads `Rejected` → screen routes to rejection | happy | P0 | authored ✓ (`Me_reflects_a_rejected_account_as_Rejected`) |
| E2E-MOB011-004 | Disabled account collapses to `Rejected` | edge | P1 | authored (mapping) |
| E2E-MOB011-005 | Missing / expired token → 401 → route to sign-in | auth | P0 | authored ✓ (`Me_without_a_bearer_token_returns_401`) |
| E2E-MOB011-006 | Unknown `registrationStatus` value → client coerces to Pending at the DTO layer (server only emits the 3 valid values) | resilience | P2 | authored (DTO coercion) |
| E2E-MOB011-007 | RTL render of the status indicator + Arabic state label | i18n | P1 | authored (screen) |

## Scenarios

### E2E-MOB011-001 — Pending golden path

```gherkin
Feature: Registration status read
  As a signed-in but not-yet-approved visitor
  I want to poll my approval state
  So that the app knows when to let me into the event

Background:
  Given a visitor has signed up and verified their email
  And the account is in the "EmailVerified" state (not yet approved)
  And the visitor holds a valid bearer token

Scenario: Pending account reads its own status
  When the app calls GET /api/v1/app/users/me with the bearer token
  Then the response is 200 with success = true
  And data.id equals the token subject
  And data.email equals the account email
  And data.appRole equals "Visitor"
  And data.preferredLanguage equals "ar"
  And data.registrationStatus equals "Pending"
```

**Evidence captured:**
- API: `CurrentUserEndpointTests.Me_returns_identity_app_role_and_pending_status_for_a_verified_visitor` (green).
- Screen: the Registration-Status indicator stays on the "under review" state.
- Console / network errors: 0 expected.

### E2E-MOB011-002 — Approved routes to Home

```gherkin
Scenario: Approved account is admitted
  Given a signed-in visitor whose account state is set to "Approved"
  When the app calls GET /api/v1/app/users/me
  Then data.registrationStatus equals "Approved"
  And the screen routes the user to Home (#13)
```

**Evidence:** `CurrentUserEndpointTests.Me_reflects_an_approved_account_as_Approved` (green).

### E2E-MOB011-003 — Rejected routes to rejection

```gherkin
Scenario: Rejected account is informed
  Given a signed-in visitor whose account state is set to "Rejected"
  When the app calls GET /api/v1/app/users/me
  Then data.registrationStatus equals "Rejected"
  And the screen shows the rejection state
```

**Evidence:** `CurrentUserEndpointTests.Me_reflects_a_rejected_account_as_Rejected` (green).

### E2E-MOB011-004 — Disabled collapses to Rejected

```gherkin
Scenario: A disabled account cannot proceed
  Given a signed-in visitor whose account state is set to "Disabled"
  When the app calls GET /api/v1/app/users/me
  Then data.registrationStatus equals "Rejected"
```

> The three-value app vocabulary has no "Disabled"; the server collapses it to
> `Rejected` (cannot proceed). See [Page_011_API.md](../../App/Page_011/Page_011_API.md).

### E2E-MOB011-005 — Auth gate

```gherkin
Scenario: No token is rejected
  Given no bearer token is supplied
  When the app calls GET /api/v1/app/users/me
  Then the response is 401 Unauthorized
  And the app routes to sign-in (the pending session is invalid)
```

**Evidence:** `CurrentUserEndpointTests.Me_without_a_bearer_token_returns_401` (green).

### E2E-MOB011-006 — Unknown status coercion (DTO layer)

```gherkin
Scenario: An unrecognised registrationStatus is coerced at the DTO layer
  Given the server only ever emits "Pending" / "Approved" / "Rejected"
  When (defensively) a value the client does not recognise is decoded
  Then RegistrationStatus.fromJson coerces it to "Pending" (its documented fallback)
  And the screen shows the Pending state
```

> Reality note: the app maps `registrationStatus` to the `RegistrationStatus` enum
> in the shared DTO (`RegistrationStatus.fromJson`), whose documented fallback is
> `Pending`; the screen therefore never sees a raw unknown string. The server's
> `AccountState → tri-state` mapping guarantees only the three valid values on the
> wire, so a true unknown does not occur. (The earlier "unknown → Error" wording did
> not match the shipped client and was corrected with D-292.)

### E2E-MOB011-007 — RTL render

```gherkin
Scenario: Arabic state label renders right-to-left
  Given the device locale is Arabic
  When the Registration-Status screen renders the current state
  Then the status label reads "حالة التسجيل" and the layout is right-to-left
  And the approval reference number + date (decoration, D11) are not data-bound
```

### E2E-MOB011-010 — Approved-state visual parity (Figma 1701:3789, D-591)

```gherkin
Scenario: The approved gate matches the frame
  Given a signed-in account whose registrationStatus is Approved
  When the حالة التسجيل screen renders
  Then the screen is a navy gate with NO bottom navigation bar
  And the header shows a back chevron on the left + the centred title "حالة التسجيل"
  And a 104px green ring surrounds a green check
  And the white headline reads "تم اعتماد حسابك" over a beige message
  And a full-width gold "متابعة" button sits below the message
  And a muted "تسجيل الخروج" link sits beneath the button (not in the header)
  When the user taps "متابعة"
  Then the app opens the home route
  # Covered by the golden test/golden/registration_status_golden_test.dart (1701:3789).
```

---

_Last reviewed:_ `2026-07-06` by `SIMF Team` (D-665 — "المراحل" stages card removed to match Figma 1701:3789; D-591 — approved-state redesign).
