# E2E test catalogue — `Home` (`home`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Mobile
> catalogue — Home's on-login bundle (`GET /app/bootstrap`) is built (D-251);
> the API implementation lives in `tests/SIMF.Api.Tests/AppBootstrapTests.cs`.

| | |
|--|--|
| **Page** | [`Page_013`](../../App/Page_013/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/bootstrap` · `GET /app/account/notifications/unread-count` · app screen #13 `/` |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | A signed-in visitor token (`AuthFlow.SignInVisitorWithoutTwoFactorAsync`); `AuthFlow.SetAccountState` for the approved case. **No literal secrets.** |
| **Last reviewed** | 2026-06-03 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB013-001 | Bootstrap returns user + unread count + server time | happy | P0 | authored ✓ (`Bootstrap_returns_the_current_user_unread_count_and_server_time`) |
| E2E-MOB013-002 | Bootstrap unread count reflects a dispatched notification | happy | P1 | authored ✓ (`Bootstrap_unread_count_reflects_a_dispatched_notification`) |
| E2E-MOB013-003 | Bootstrap reflects an approved account (pending → approved routing) | happy | P0 | authored ✓ (`Bootstrap_reflects_an_approved_account`) |
| E2E-MOB013-004 | No token → 401 | auth | P0 | authored ✓ (`Bootstrap_without_a_token_returns_401`) |
| E2E-MOB013-005 | Guest (no token) renders Home with privilege = Guest, no bootstrap call | happy | P1 | authored (screen) |
| E2E-MOB013-006 | Privilege from the JWT claim gates the tiles | auth | P1 | authored (screen) |
| E2E-MOB013-007 | RTL render of Home tiles + bell badge | i18n | P1 | authored (screen) |

## Scenarios

### E2E-MOB013-001 — Bootstrap golden path

```gherkin
Feature: On-login bootstrap bundle
  As a signed-in app user
  I want one call that returns who I am, my unread badge and the server clock
  So that the app caches everything it needs on login

Background:
  Given a visitor has signed up, verified their email and signed in

Scenario: Bootstrap returns the cached-on-login bundle
  When the app calls GET /api/v1/app/bootstrap
  Then the response is 200 with success = true
  And user.id, user.email match the signed-in account
  And user.appRole = "Visitor"
  And user.registrationStatus = "Pending"
  And unreadNotificationCount equals the dedicated unread-count endpoint
  And serverTimeUtc is a recent UTC instant
```

**Evidence:** `AppBootstrapTests.Bootstrap_returns_the_current_user_unread_count_and_server_time` (green).

### E2E-MOB013-002 — Unread count reflects notifications

```gherkin
Scenario: A new notification bumps the bootstrap unread count
  Given the visitor's current bootstrap unread count is N
  When a notification is dispatched to the visitor
  And the app calls GET /api/v1/app/bootstrap again
  Then unreadNotificationCount = N + 1
```

**Evidence:** `AppBootstrapTests.Bootstrap_unread_count_reflects_a_dispatched_notification` (green).

### E2E-MOB013-003 — Approved routing

```gherkin
Scenario: An approved account bootstraps as Approved
  Given a signed-in visitor whose account is set to Approved
  When the app calls GET /api/v1/app/bootstrap
  Then user.registrationStatus = "Approved"
  And the app routes into the full experience (not the pending screen)
```

**Evidence:** `AppBootstrapTests.Bootstrap_reflects_an_approved_account` (green).

### E2E-MOB013-004 — Auth gate

```gherkin
Scenario: No token is rejected
  Given no bearer token is supplied
  When the app calls GET /api/v1/app/bootstrap
  Then the response is 401 Unauthorized
```

**Evidence:** `AppBootstrapTests.Bootstrap_without_a_token_returns_401` (green).

### E2E-MOB013-005 — Guest Home (no bootstrap)

```gherkin
Scenario: A guest sees Home without bootstrapping
  Given no user is signed in
  When the Home screen opens
  Then the privilege is Guest (from the absent JWT)
  And no /app/bootstrap call is made
  And only guest-visible tiles render
```

### E2E-MOB013-006 — Privilege gating

```gherkin
Scenario: The cached privilege gates the tiles
  Given a signed-in user whose cached appRole is "Visitor"
  When Home renders
  Then Visitor+ tiles are enabled and Staff/Moderator-only tiles are hidden
```

### E2E-MOB013-007 — RTL render

```gherkin
Scenario: Home renders right-to-left in Arabic
  Given the device locale is Arabic
  When Home renders the tiles and the bell badge
  Then the layout is right-to-left
  And the unread badge is hidden when the count is 0
```

---

_Last reviewed:_ `2026-06-03` by `SIMF Team`.
