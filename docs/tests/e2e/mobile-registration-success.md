# E2E test catalogue — `Registration success` (`registrationSuccess`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Mobile screen #10 — the
> terminal sign-up confirmation. Spec: [`Page_010`](../../App/Page_010/README.md).
> Runner-agnostic Gherkin. The screen glue is widget-tested in
> `src/Mobile/simf_app/test/features/registration/registration_success_screen_test.dart`.

| | |
|--|--|
| **Page** | [`Page_010`](../../App/Page_010/README.md) (App page docs) |
| **Route** | app screen #10 `registrationSuccess` → `/registration/success` (**auth-gated**; reached as a replacement) |
| **APIs** | **None** — static confirmation; the account was already created by the Page_007 profile save. The optional status poll lives on Page_011. |
| **Surface** | Mobile (Flutter) — signed-in, pending approval |
| **Auth setup** | A signed-in (pending) session — reached automatically after the profile save. |
| **Last reviewed** | 2026-06-05 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB010-001 | Renders the confirmation (title + under-review message + both actions) | happy | P0 | authored ✓ (widget test) |
| E2E-MOB010-002 | Primary "Registration status" → Page_011 | happy | P0 | authored ✓ (widget test) |
| E2E-MOB010-003 | Ghost "Go to home" → home | happy | P0 | authored ✓ (widget test) |
| E2E-MOB010-004 | Reached as a replacement — Back does not reopen the sign-up form | edge | P1 | authored (no app-bar back; `goNamed` replacement) |
| E2E-MOB010-005 | Offline-safe — renders with no network (no API call) | resilience | P1 | authored (no repository dependency) |
| E2E-MOB010-006 | Auth gate — a signed-out open redirects to sign-in | auth | P1 | authored (route 10 in the auth gate) |
| E2E-MOB010-007 | RTL render (Arabic) mirrors | i18n | P1 | authored (screen) |

## Scenarios

### E2E-MOB010-001 — Confirmation renders

```gherkin
Feature: Registration success
Scenario: The just-registered visitor sees the confirmation
  Given the profile save (Page_007) succeeded and the account is pending
  When the registration-success screen opens
  Then it shows the "Registration success" title and the under-admin-review message
  And a primary "Registration status" button and a ghost "Go to home" button
```

**Evidence:** `registration_success_screen_test` — "renders the confirmation + both actions".

### E2E-MOB010-002 — Primary → registration status

```gherkin
Scenario: The user checks their status
  When they tap "Registration status"
  Then the app navigates to the registration-status screen (Page_011)
```

**Evidence:** `registration_success_screen_test` — "primary button routes to the registration-status screen".

### E2E-MOB010-003 — Ghost → home

```gherkin
Scenario: The user goes to the home screen
  When they tap "Go to home"
  Then the app navigates to home (#13)
```

**Evidence:** `registration_success_screen_test` — "ghost button routes home".

### E2E-MOB010-004 — Replacement navigation

```gherkin
Scenario: Back does not re-open the sign-up form
  Given the screen was reached as a replacement of the profile form
  Then there is no app-bar back affordance
  And a system back does not return into the multi-step sign-up form
```

> By construction: the screen has no `AppBar`/back, and the profile save uses
> `context.goNamed` (replacement), so the sign-up steps are off the back stack.

### E2E-MOB010-005 — Offline-safe

```gherkin
Scenario: The confirmation renders without a network
  Given the device is offline
  When the registration-success screen opens
  Then it renders fully (it makes no API call)
```

### E2E-MOB010-006 — Auth gate

```gherkin
Scenario: A signed-out open is redirected
  Given no session
  When /registration/success is requested
  Then the router redirects to /sign-in (route 10 is auth-gated)
```

### E2E-MOB010-007 — RTL render (Arabic)

```gherkin
Scenario: The confirmation mirrors under Arabic
  Given the app language is Arabic
  Then the title, message, and buttons mirror right-to-left and the check stays centred
```

---

_Last reviewed:_ `2026-06-05` by `SIMF Team`.
