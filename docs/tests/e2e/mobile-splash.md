# E2E test catalogue — `Splash` (`splash`)

> **Authority:** SIMF E2E test catalogue (D-133 / D-245). Mobile screen #1, the
> first screen on every cold launch. The boot logic is documented in
> [`Page_001`](../../App/Page_001/README.md); it reuses two already-shipped App
> endpoints and adds **no** new endpoint. Runner-agnostic Gherkin. The Flutter
> boot decision is unit-tested in
> `src/Mobile/simf_app/test/features/splash/splash_controller_test.dart` and the
> cold-start restore in
> `src/Mobile/packages/simf_auth_pkg/test/auth_controller_restore_test.dart`.

| | |
|--|--|
| **Page** | [`Page_001`](../../App/Page_001/README.md) (App page docs) |
| **Route** | app screen #1 `/splash` · `POST /api/v1/app/auth/refresh` (E1) + `GET /api/v1/app/users/me` (E2) |
| **Surface** | Mobile (Flutter) — bootstrap screen, non-interactive except the update dialog |
| **Test runner** | Flutter widget/unit test (boot decision) · device/emulator drive for the visual + route-out |
| **Auth setup** | A secure-storage seed (refresh token ± access token ± cached user). For a live device run, sign in once on screen #3, then relaunch. **No literal secrets** — the device-key/refresh token comes from a real prior sign-in. |
| **Last reviewed** | 2026-06-11 |

> **KSA-Project redesign (D-361, Figma 159:573):** the splash now renders the
> brand lock-up — `SimfLogo` (136) over "SAUDI · MOD · RSNF", the forum name,
> and the two-line edition/date — on the navy primary surface, with **no
> spinner** (the design shows none; the L-1/L-6 timers bound the wait). The
> boot sequence, update dialogs and route-out contract are unchanged; the old
> placeholder screen is parked in `lib/features/_legacy_mockup/`. New widget
> tests: `splash_screen_test.dart` (lock-up render + route-out by name +
> resumed location).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB001-001 | First launch (no session, onboarding not done) → Onboarding (#2) | happy | P0 | authored ✓ (`SplashController` test) |
| E2E-MOB001-002 | Returning signed-out user (onboarded) → Sign-in (#3) | happy | P0 | authored ✓ (`SplashController` test) |
| E2E-MOB001-003 | Valid cached session → restore + hydrate privilege → Home (#13) | happy | P0 | authored ✓ (`AuthController` restore test) |
| E2E-MOB001-004 | Expired access token → silent refresh → hydrate privilege → route out | happy | P0 | authored ✓ (`AuthController` restore test) |
| E2E-MOB001-005 | Privilege is read from `/app/users/me`, not the refresh payload | resilience | P0 | authored ✓ (restore test: cached Guest → Visitor) |
| E2E-MOB001-006 | Signed-in user resumes the last saved content screen | happy | P1 | authored ✓ (`SplashController` test) |
| E2E-MOB001-007 | A non-resumable saved location (auth/transient) falls back to Home | edge | P1 | authored ✓ (`SplashController` test) |
| E2E-MOB001-008 | Offline at launch (expired access) → degraded resume on cached identity | resilience | P0 | authored ✓ (restore test) |
| E2E-MOB001-009 | Expired / revoked refresh token → clear session → Sign-in | auth | P0 | authored ✓ (restore test) |
| E2E-MOB001-010 | Forced store update → non-dismissible dialog, boot blocked | edge | P1 | authored ✓ (`SplashController` test) |
| E2E-MOB001-011 | Optional store update → dismissible dialog, then continue | edge | P2 | authored ✓ (`SplashController` test) |
| E2E-MOB001-012 | Minimum logo display time honoured (no sub-100 ms flash) | ux | P2 | authored (min-display provider) |
| E2E-MOB001-013 | RTL render of the splash + update dialog (Arabic primary) | i18n | P1 | authored (screen) |

## Scenarios

### E2E-MOB001-001 — First launch routes to onboarding

```gherkin
Feature: Cold-start boot
  As a brand-new user opening the app for the first time
  I want the splash to take me to onboarding
  So that I see the introduction before signing in

Background:
  Given no session is stored in secure storage
  And the onboarding-completed flag is not set

Scenario: First run opens onboarding
  When the app cold-starts on the splash
  And the boot work resolves
  Then the app replace-navigates to Onboarding (#2 /onboarding)
  And the splash is removed from the back stack
```

**Evidence:** `splash_controller_test` — "a signed-out first run routes to onboarding".

### E2E-MOB001-002 — Returning signed-out user routes to sign-in

```gherkin
Scenario: A returning, signed-out user goes to sign-in
  Given no session is stored
  And the onboarding-completed flag IS set
  When the app cold-starts on the splash
  Then the app routes to Sign-in (#3 /sign-in)
```

**Evidence:** `splash_controller_test` — "a signed-out returning user routes to sign-in".

### E2E-MOB001-003 — Valid cached session restores and routes home

```gherkin
Scenario: A valid cached session lands on Home
  Given a non-expired access token, a refresh token and a cached user are stored
  When the app cold-starts
  Then the session is restored
  And GET /api/v1/app/users/me is called to refresh the authoritative privilege
  And when the hydrated profileComplete flag is false the app routes to the
      Page_007 profile form instead — even over a saved route (D-374)
  And the app routes to Home (#13 /) when complete and no resumable screen is saved
```

**Evidence:** `auth_controller_restore_test` — "a valid cached token restores, then hydrates the real privilege"; `splash_controller_test` — "a signed-in user with an incomplete profile is gated to the profile form, even over a saved route (D-374)".

### E2E-MOB001-004 — Expired access token triggers silent refresh

```gherkin
Scenario: An expired access token is silently refreshed
  Given a refresh token is stored but the access token is missing or expired
  When the app cold-starts
  Then POST /api/v1/app/auth/refresh is called with the stored refresh token
  And the rotated tokens are persisted
  And GET /api/v1/app/users/me hydrates the app-role and registration status
  And the app routes out per the resolved privilege
```

**Evidence:** `auth_controller_restore_test` — "an expired access token refreshes, then hydrates the real privilege".

### E2E-MOB001-005 — Privilege comes from /app/users/me, not the token payload

```gherkin
Scenario: The refresh payload's user does not decide privilege
  Given the refresh/token payload carries only id + email + displayName (AuthUser)
  And the cached user is wrongly Guest/Pending
  When GET /api/v1/app/users/me returns appRole "Visitor" and registrationStatus "Approved"
  Then the restored session reflects Visitor / Approved
  And route-out uses Visitor, not the defaulted Guest
```

> Guards the core defect this page fixed: `AuthUser` (refresh payload) omits the
> app-role, and `AppRole.fromJson(null)` defaults to Guest. See
> [Page_001_API.md](../../App/Page_001/Page_001_API.md) E2.

**Evidence:** `auth_controller_restore_test` (cached Guest → hydrated Visitor).

### E2E-MOB001-006 — Resume to the last saved screen

```gherkin
Scenario: A signed-in user resumes where they left off
  Given a valid cached session
  And the last saved location is "/sessions"
  When the app cold-starts
  Then the app routes to "/sessions"
```

**Evidence:** `splash_controller_test` — "a signed-in user resumes the last saved content route".

### E2E-MOB001-007 — Non-resumable saved location falls back to Home

```gherkin
Scenario: A transient saved location is not resumed
  Given a valid cached session
  And the last saved location is "/sign-up/otp" (a transient auth route)
  When the app cold-starts
  Then the saved location is ignored
  And the app routes to Home (#13 /)
```

**Evidence:** `splash_controller_test` — "a non-resumable saved route is ignored in favour of home"; `route_resume_test`.

### E2E-MOB001-008 — Offline degraded resume

```gherkin
Scenario: Offline at launch resumes on the cached identity
  Given a refresh token + an (expired) access token + a cached user are stored
  And the network is unreachable
  When the app cold-starts and the silent refresh fails with a network error
  Then the app resumes signed-in on the cached identity in a degraded state
  And it does NOT strand the user on the splash
  And it does NOT sign the user out
```

**Evidence:** `auth_controller_restore_test` — "an offline refresh resumes on the cached identity (degraded)".

### E2E-MOB001-009 — Expired refresh token signs out

```gherkin
Scenario: A dead refresh token ends the session
  Given a refresh token is stored but no valid access token
  When the silent refresh returns 401 (refresh token expired/revoked)
  Then the stored session is cleared
  And the app routes to Sign-in (#3)
  And it does not loop
```

**Evidence:** `auth_controller_restore_test` — "an expired refresh token signs out".

### E2E-MOB001-010 — Forced store update blocks boot

```gherkin
Scenario: A mandatory update gates entry
  Given the store-native update check reports a forced update
  When the app cold-starts
  Then a non-dismissible update dialog is shown over the logo
  And the only action opens the store listing
  And the app does not route into its screens
```

> The store check is store-native (Page_001 Logic L-2). Pre-launch the default
> `NoopAppUpdateChecker` reports up-to-date; this scenario drives a stub checker.

**Evidence:** `splash_controller_test` — "a forced update short-circuits to SplashUpdateRequired".

### E2E-MOB001-011 — Optional store update is dismissible

```gherkin
Scenario: A soft update can be deferred
  Given the store-native update check reports an optional update
  When the app cold-starts and boot resolves
  Then a dismissible update dialog is shown
  And tapping "Later" continues to the resolved destination
  And tapping "Update now" opens the store listing
```

**Evidence:** `splash_controller_test` — "an optional update flags the soft prompt".

### E2E-MOB001-012 — Minimum display time

```gherkin
Scenario: The logo is never flashed
  Given boot work completes faster than the minimum display duration
  When the app cold-starts
  Then the logo is held for at least the minimum display duration before route-out
```

### E2E-MOB001-013 — RTL render

```gherkin
Scenario: Arabic-primary splash renders right-to-left
  Given the device locale is Arabic
  When the splash renders
  Then the SIMF logo + name are centered (direction-neutral)
  And any update-dialog text reads in Arabic and the dialog mirrors for RTL
```

---

_Last reviewed:_ `2026-06-11` by `SIMF Team`.
