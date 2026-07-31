# E2E test catalogue — `Splash` (`splash`)

> **Authority:** SIMF E2E test catalogue (D-133 / D-245). Mobile screen #1, the
> first screen on every cold launch. The boot logic is documented in
> [`Page_001`](../../App/Page_001/README.md); it reuses two already-shipped App
> endpoints plus the anonymous `GET /api/v1/app/version-policy` update policy
> (D-736). Runner-agnostic Gherkin. The Flutter
> boot decision is unit-tested in
> `src/Mobile/simf_app/test/features/splash/splash_controller_test.dart`, the
> cold-start restore in
> `src/Mobile/simf_app/packages/simf_auth_pkg/test/auth_controller_restore_test.dart`,
> and the server update policy (forced/soft/snooze/fail-open) in
> `src/Mobile/simf_app/test/core/startup/server_app_update_checker_test.dart`
> + the dialog glue in
> `src/Mobile/simf_app/test/features/splash/splash_screen_test.dart`.

| | |
|--|--|
| **Page** | [`Page_001`](../../App/Page_001/README.md) (App page docs) |
| **Route** | app screen #1 `/splash` · `POST /api/v1/app/auth/refresh` (E1) + `GET /api/v1/app/users/me` (E2) + `GET /api/v1/app/version-policy` (anonymous, D-736) |
| **Surface** | Mobile (Flutter) — bootstrap screen, non-interactive except the update dialog |
| **Test runner** | Flutter widget/unit test (boot decision) · device/emulator drive for the visual + route-out |
| **Auth setup** | A secure-storage seed (refresh token ± access token ± cached user). For a live device run, sign in once on screen #3, then relaunch. **No literal secrets** — the device-key/refresh token comes from a real prior sign-in. |
| **Last reviewed** | 2026-07-10 (D-736 — server version-policy update gate) |

> **KSA-Project redesign (D-361, Figma 159:573):** the splash now renders the
> brand lock-up — `SimfLogo` (136) over "SAUDI · MOD · RSNF", the forum name,
> and the two-line edition/date — on the navy primary surface, with **no
> spinner** (the design shows none; the L-1/L-6 timers bound the wait). The
> boot sequence, update dialogs and route-out contract are unchanged; the old
> placeholder screen is parked in `lib/features/_legacy_mockup/`. New widget
> tests: `splash_screen_test.dart` (lock-up render + route-out by name +
> resumed location).

> **Server version-policy update gate (D-736):** the launch update check is no
> longer store-native — `ServerAppUpdateChecker` fetches the anonymous
> `GET /api/v1/app/version-policy` (per-platform `minVersion` / `latestVersion`
> / `storeUrl`, sourced from the six `appUpdate.android.*` / `appUpdate.ios.*`
> SystemSettings keys an admin edits on `/admin/configuration`) with a
> **5-second fail-open cap** — any error/timeout continues the boot normally.
> The installed version (real `package_info_plus` version; pubspec `1.0.0+2`)
> is compared per-platform with semver (`pub_semver`, lenient leading-`v`):
> installed < `minVersion` **and** a usable store URL → the FORCED
> non-dismissible dialog; `minVersion` ≤ installed < `latestVersion` + store
> URL → the dismissible soft prompt, and dismissing it (any way) snoozes THAT
> version for 3 days (prefs `simf.prefs.app_update_snoozed_version` +
> `simf.prefs.app_update_snoozed_at_iso`). No store URL or unparseable values
> → that rule is off (anti-brick). A hard block only follows a live successful
> fetch, never a cached policy.

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
| E2E-MOB001-010 | Forced update (server policy) → non-dismissible dialog, boot blocked | edge | P1 | authored ✓ (`SplashController` test + checker test) |
| E2E-MOB001-011 | Optional update (server policy) → dismissible dialog, then continue | edge | P2 | authored ✓ (`SplashController` test + screen tests) |
| E2E-MOB001-012 | Minimum logo display time honoured (no sub-100 ms flash) | ux | P2 | authored (min-display provider) |
| E2E-MOB001-013 | RTL render of the splash + update dialog (Arabic primary) | i18n | P1 | authored (screen) |
| E2E-MOB001-014 | Forced-update gate — admin `appUpdate.android.minVersion` above installed + store URL → app blocked until updated (D-736) | edge | P0 | authored ✓ (`ServerAppUpdateChecker` test) |
| E2E-MOB001-015 | Soft update + snooze — "لاحقاً" continues; the same version stays quiet for 3 days; a newer version prompts again (D-736) | happy | P1 | authored ✓ (checker snooze tests + screen Later/scrim tests) |
| E2E-MOB001-016 | Fail-open — API stopped/unreachable → normal boot, no dialog (D-736) | resilience | P0 | authored ✓ (`ServerAppUpdateChecker` test) |
| E2E-MOB001-017 | Anti-brick — `minVersion` set but `storeUrl` EMPTY → no gate, normal boot (D-736) | resilience | P0 | authored ✓ (`ServerAppUpdateChecker` test) |
| E2E-MOB001-018 | **Edition line is data, not a literal (#40-residual):** the date/location line renders `OrganizationProfile.eventStartDate/eventEndDate` + `locationText` through the shared bilingual formatter; the bundled literal is the fallback only | happy | P1 | authored ✓ (screen — `the event line comes from the configured edition dates …`) |
| E2E-MOB001-019 | Configured edition line renders in Arabic, and drops the ` · ` separator when the edition has no location (#40-residual) | i18n | P1 | authored ✓ (screen — Arabic + no-location cases) |
| E2E-MOB001-020 | First-ever run / an edition with no dates set falls back to the bundled literal, so the splash is never blank (#40-residual) | resilience | P1 | authored ✓ (screen — `an edition with no dates falls back to the bundled literal`) |
| E2E-MOB001-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB001-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

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

### E2E-MOB001-010 — Forced update blocks boot

```gherkin
Scenario: A mandatory update gates entry
  Given GET /api/v1/app/version-policy reports this platform's minVersion above
        the installed version
  And the policy carries a usable (absolute http(s)) store URL
  When the app cold-starts
  Then a non-dismissible dialog titled "تحديث مطلوب" / "Update required" is
        shown over the logo
  And the only action "تحديث الآن" / "Update now" opens the store listing URL
  And the app does not route into its screens
```

> The update check is the server version policy (D-736 — `ServerAppUpdateChecker`
> against `GET /api/v1/app/version-policy`, 5 s fail-open cap; it replaced the
> pre-D-736 store-native check). A hard block only follows a live successful
> fetch on THIS launch — never a cached policy.

**Evidence:** `splash_controller_test` — "a forced update short-circuits to SplashUpdateRequired"; `server_app_update_checker_test` — "installed below the minimum → forced".

### E2E-MOB001-011 — Optional update is dismissible

```gherkin
Scenario: A soft update can be deferred
  Given GET /api/v1/app/version-policy reports the installed version at or above
        minVersion but below latestVersion, with a usable store URL
  When the app cold-starts and boot resolves
  Then a dismissible dialog titled "يتوفر تحديث" / "Update available" is shown
  And tapping "لاحقاً" / "Later" continues to the resolved destination
  And tapping "تحديث الآن" / "Update now" opens the store listing URL
```

**Evidence:** `splash_controller_test` — "an optional update flags the soft prompt"; `splash_screen_test` — the two soft-update dismissal tests (D-736).

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

### E2E-MOB001-014 — Forced-update gate from the admin policy (D-736)

```gherkin
Scenario: An admin-set minimum version above the installed one blocks the app
  Given the installed Android app version is "1.0.0" (the real package_info_plus
        version; pubspec 1.0.0+2)
  And an administrator on /admin/configuration sets
        appUpdate.android.minVersion = "2.0.0"
  And sets appUpdate.android.storeUrl to a valid Google Play listing URL
        (absolute https)
  When the app is relaunched
  Then GET /api/v1/app/version-policy returns android.minVersion "2.0.0" and
        the store URL
  And a non-dismissible dialog "تحديث مطلوب" / "Update required" opens over the
        splash
  And pressing the system back button does nothing — the dialog stays and the
        app never routes into its screens
  And the only action "تحديث الآن" / "Update now" opens the Google Play listing
  And the app is unusable until it is updated
```

**Evidence:** `server_app_update_checker_test` — "installed below the minimum → forced"; `splash_controller_test` — "a forced update short-circuits to SplashUpdateRequired".

### E2E-MOB001-015 — Soft update prompts once, then snoozes 3 days (D-736)

```gherkin
Scenario: A newer latest version prompts, "لاحقاً" snoozes that version
  Given the installed version is "1.0.0"
  And appUpdate.android.minVersion is empty (or at/below "1.0.0")
  And an administrator sets appUpdate.android.latestVersion = "1.1.0" and a
        valid store URL
  When the app is relaunched
  Then a dismissible dialog "يتوفر تحديث" / "Update available" is shown once
        boot resolves
  When the user taps "لاحقاً" / "Later"
  Then the app continues normally to its resolved destination
  And prefs simf.prefs.app_update_snoozed_version = "1.1.0" and
        simf.prefs.app_update_snoozed_at_iso are stored
  When the app is relaunched within the 3-day snooze window
  Then NO update prompt is shown (the snoozed version stays quiet)
  And when the admin later raises latestVersion to "1.2.0", the prompt shows
        again on the next launch (a newer version prompts immediately)
  # Dismissing the prompt ANY way (Later / scrim) snoozes; the snooze never
  # suppresses a FORCED update, and the About-the-app manual check ignores it.
```

**Evidence:** `server_app_update_checker_test` — the snooze group ("a snoozed version stays quiet inside the window", "an expired snooze prompts again", "a NEWER version than the snoozed one prompts immediately", "a snooze never suppresses a FORCED update"); `splash_screen_test` — 'soft update — "Later" snoozes the version and routes out' + 'a scrim dismiss also snoozes and routes out'.

### E2E-MOB001-016 — Fail-open when the policy is unreachable (D-736)

```gherkin
Scenario: An unreachable version-policy endpoint never blocks boot
  Given the API is stopped or unreachable
  When the app cold-starts
  Then the GET /api/v1/app/version-policy check fails open within its
        5-second cap
  And no update dialog is shown
  And the app boots and routes out normally on the cached/derived state
```

**Evidence:** `server_app_update_checker_test` — "an unreachable server fails open to upToDate" + "an unoverridden provider graph fails open too".

### E2E-MOB001-017 — Anti-brick: a minimum version without a store URL is off (D-736)

```gherkin
Scenario: A forced gate without an Update target is ignored
  Given appUpdate.android.minVersion = "2.0.0" is set
  But appUpdate.android.storeUrl is EMPTY
  When the app (installed version "1.0.0") is relaunched
  Then no forced-update dialog is shown
  And the app boots normally
  # No usable store URL → the rule is off (anti-brick): the app must never be
  # blocked without a working Update button. Unparseable version values are
  # likewise ignored (lenient semver; a leading 'v' is tolerated).
```

**Evidence:** `server_app_update_checker_test` — "below the minimum without a store URL → upToDate (anti-brick)".

### E2E-MOB001-018 — The edition line comes from the configured dates (#40-residual)

```gherkin
Feature: The splash edition line follows the configured edition
  As the forum operator
  I want the splash date/location line to come from the Organization Profile
  So that a new edition never ships behind a hardcoded date

Background:
  Given the Organization Profile carries eventStartDate = 2027-03-08
  And eventEndDate = 2027-03-10
  And locationText = "Jeddah" / locationTextArabic = "جدة"
  And the profile is already cached on the device (warmed at a previous splash)

Scenario: The splash renders the configured edition dates
  When the app launches and the splash lock-up paints
  Then the edition line reads "4th Edition\n8-10 March 2027 · Jeddah"
  And the string "23–25 Nov 2026" appears nowhere on the screen
```

**Evidence:** `splash_screen_test` — "the event line comes from the configured
edition dates, not the bundled literal (#40-residual)" (asserts both the new
value and the absence of the old literal).

### E2E-MOB001-019 — Arabic + the no-location case (#40-residual)

```gherkin
Scenario: The same edition renders in Arabic
  Given the app locale is Arabic
  When the splash lock-up paints
  Then the edition line reads "النسخة الرابعة\n8-10 مارس 2027 · جدة"

Scenario: An edition with dates but no location omits the separator
  Given the Organization Profile has no locationText in either language
  When the splash lock-up paints
  Then the edition line reads "4th Edition\n8-10 March 2027"
  And it does not end with a dangling " · "
```

**Evidence:** `splash_screen_test` — "the configured event line renders in
Arabic too" + "an edition with dates but no location omits the separator".

### E2E-MOB001-020 — First run / no dates falls back (#40-residual)

```gherkin
Scenario: A first-ever launch still shows an edition line
  Given the device has never cached an Organization Profile
  # or the profile is cached but its event dates are not set
  When the splash lock-up paints
  Then the edition line reads the bundled literal
  And the splash is never blank in that slot
```

**Evidence:** `splash_screen_test` — "an edition with no dates falls back to the
bundled literal (first run / offline)". The literal is retained deliberately as
the offline/first-run fallback; it is no longer the primary source.

---

_Last reviewed:_ `2026-07-30` by `SIMF Team` (#40-residual — the splash edition
line now renders the CP-configured dates; appended 018–020. Prior review
2026-07-10, D-736 — server version-policy update gate; rewrote
E2E-MOB001-010/011 off the old store-native contract, appended 014–017).
