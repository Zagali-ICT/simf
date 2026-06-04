# E2E test catalogue — `Registration status` (`registrationStatus`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7).
> First file of the **mobile** catalogue — the App build is now landing
> endpoints (D-249), so the mobile screens are catalogued as their backing
> APIs ship. Runner-agnostic Gherkin; the implementation for this screen's
> API lives in `tests/SIMF.Api.Tests/CurrentUserEndpointTests.cs`.

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
| E2E-MOB011-006 | Unknown `registrationStatus` value → Error state (no silent Pending) | resilience | P2 | authored (client) |
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

### E2E-MOB011-006 — Unknown status is an error, not a silent Pending

```gherkin
Scenario: An unrecognised registrationStatus is treated as Error
  Given the server returns a registrationStatus the client does not recognise
  When the screen maps the value
  Then it shows the Error state with a retry
  And it does NOT silently fall back to "Pending"
```

### E2E-MOB011-007 — RTL render

```gherkin
Scenario: Arabic state label renders right-to-left
  Given the device locale is Arabic
  When the Registration-Status screen renders the current state
  Then the status label reads "حالة التسجيل" and the layout is right-to-left
  And the approval reference number + date (decoration, D11) are not data-bound
```

---

_Last reviewed:_ `2026-06-03` by `SIMF Team`.
