# SIMF App-Update Gate — Developer & Operator Guide

> **Status:** as-built reference for the D-736 app-update subsystem. Companion to
> the page docs [`docs/App/Page_001/`](../App/Page_001/README.md) (splash) +
> [`docs/pages/mobile/about-app/`](../pages/mobile/about-app/README.md), the E2E
> catalogues [`e2e/mobile-splash.md`](../tests/e2e/mobile-splash.md) /
> [`e2e/mobile-about-app.md`](../tests/e2e/mobile-about-app.md) /
> [`e2e/cp-admin-configuration.md`](../tests/e2e/cp-admin-configuration.md), and
> the decision log (D-736). This documents what shipped; where it disagrees with
> the code, the code wins — fix this doc.

## 1. Why it exists

The app must be able to **force or suggest an update** on both iOS and Android,
controlled by an admin without a redeploy — e.g. to push a security fix or retire
a broken build. Store-native mechanisms can't do this for SIMF: Play/App Store
offer no team-controlled minimum version, Play in-app-update is dead on the
client's GMS-free Huawei tablet, and store-scrape packages are fragile /
Firebase-leaning / AppGallery-blind. D-736 reverses the original Page_001 L-2
"store-native, never a SIMF version endpoint" contract and adds a tiny
server-driven policy the app checks on every launch.

## 2. How it works (one paragraph)

On launch the splash calls the anonymous `GET /api/v1/app/version-policy`, which
returns a per-platform `{ minVersion, latestVersion, storeUrl }`. The app compares
its **real installed version** (`package_info_plus`, semver) against the policy:
below `minVersion` → a **non-dismissible "Update required"** gate (the app is
unusable until updated); at/above `minVersion` but below `latestVersion` → a
**dismissible "Update available"** prompt (snoozed 3 days per version); otherwise
boot continues. The same endpoint backs a manual **"Check for updates"** row in
About-the-app. Everything **fails open** — any error/timeout means the app boots
normally.

## 3. The policy — six SystemSettings keys

The policy is **not** a new table or entity. It is six rows in the existing
`SystemSettings` key/value store (D-229), edited on the CP **System Configuration**
page (`/admin/configuration`, `PermissionCatalog.Configuration.*`). The keys are
centralised as constants in `SIMF.Common.AppUpdateSettingKeys`:

| Key | Meaning | Format | Empty ⇒ |
|-----|---------|--------|---------|
| `appUpdate.android.minVersion`    | Minimum supported Android version | semver, e.g. `1.2.0` | no forced gate |
| `appUpdate.android.latestVersion` | Latest released Android version   | semver, e.g. `1.4.0` | no soft prompt |
| `appUpdate.android.storeUrl`      | Google Play listing the Update button opens | absolute `https://…` | gate + prompt **both off** for Android |
| `appUpdate.ios.minVersion`        | Minimum supported iOS version     | semver | no forced gate |
| `appUpdate.ios.latestVersion`     | Latest released iOS version       | semver | no soft prompt |
| `appUpdate.ios.storeUrl`          | App Store listing the Update button opens | absolute `https://…` (or `itms-apps://` — but the server only serves http(s)) | gate + prompt **both off** for iOS |

The six rows are **seeded empty** by `DefaultContentSeeder` (with the meaning above
as each row's Description) so they appear on the CP grid ready to edit — an admin
never hand-types a key name (a typo'd key is silently ignored by the whitelist).
Seeding is idempotent, keyed on the key name alone: it never overwrites an admin
edit and never resurrects a soft-deleted key.

**The feature ships dormant.** Until an admin fills the values (all empty), the
policy is all-null and every app is "up to date" — nothing is gated.

## 4. Behaviour rules (the invariants)

- **Fail-open (never block on failure).** Any fetch error, timeout (5 s cap on the
  splash), malformed payload, or unparseable version resolves to *up-to-date*. A
  user who can't reach the server just uses the app.
- **Anti-brick: a gate/prompt needs a usable store URL.** `forced`/`optional` only
  fire when the platform's `storeUrl` is a non-empty absolute `http(s)` URL — a
  block screen can never be a dead end.
- **A hard block only follows a live, successful fetch** — never a cached policy,
  so a min-version you later roll back can't keep bricking offline users.
- **Semver, build metadata ignored.** Versions compare with `pub_semver`
  (`1.10.0 > 1.9.0`, not string order). Build metadata is dropped (SemVer 2.0.0
  §10) — a `minVersion` of `1.0.0+42` does **not** outrank an installed `1.0.0`
  (the store build's version name is also `1.0.0`), which would otherwise be an
  unrecoverable brick. A leading `v` is tolerated; anything unparseable disables
  that rule.
- **`min` and `latest` are separate knobs.** `latest` moves every release (soft
  nudge); `min` moves rarely and deliberately (hard gate).
- **Store-URL sanitisation (D-467).** The server drops any non-`http(s)` value
  (e.g. a `javascript:` string entered via the generic CRUD) to null before it
  ever reaches the app as a launch target.

## 5. Operator runbook — releasing an update

When you ship a new app build:

1. **Publish** the build to the store(s) and **wait until it is actually
   downloadable** — App Store CDN propagation and Play review/rollout both lag
   approval. Verify on a real device's store page, not just the console.
2. **Set the `storeUrl`** for each platform (once, at first release) to the live
   listing URL.
3. **Raise `latestVersion`** to the new version → existing users get the dismissible
   "Update available" prompt (snoozed 3 days per version).
4. **Raise `minVersion` ONLY when you must force the upgrade, and ONLY after the new
   build is live in EVERY channel at 100%** (both stores, no staged/phased
   rollout). Forcing users while a staged rollout still gates availability strands
   the ones who can't yet download it — the Update button would have nothing to
   install.
5. To **un-brick** in an emergency (a bad `minVersion`), just lower or blank it —
   the next launch's live fetch clears the gate immediately (no app redeploy).

Rule of thumb: **`latest` up on every release; `min` up only to force, only after
100% availability.**

## 6. Where the code lives

**Backend**
- Keys: `src/Shared/SIMF.Common/AppUpdateSettingKeys.cs`
- DTO: `src/Shared/SIMF.Contracts/Configuration/AppVersionPolicy.cs`
- Service: `IAppVersionPolicyService` / `AppVersionPolicyService` (`…/Configuration/`)
- Endpoint: `src/Backend/SIMF.Api/Endpoints/Public/AppVersionPolicyEndpoint.cs`
  (`GET /app/version-policy`, `AllowAnonymous`, `Tags("Public")`, **no** auth
  rate-limit bucket per D-731 — the global per-IP limiter applies)
- Seeder: `DefaultContentSeeder.EnsureAppUpdateSettingsAsync`

**App (`src/Mobile/simf_app`)**
- Policy + evaluator: `lib/core/startup/app_version_policy.dart`
  (`AppVersionPolicy`, `tryParseVersion`, `usableStoreUrl`, `evaluateVersionPolicy`)
- Checker: `lib/core/startup/server_app_update_checker.dart` (behind the
  `appUpdateCheckerProvider` seam in `app_update_checker.dart`; web keeps `Noop`)
- Splash gate/prompt: `lib/features/splash/splash_screen.dart` (+ `_controller`)
- Manual check: `lib/features/about/widgets/check_for_updates_row.dart`
  (+ the one-button `lib/app/widgets/simf_info_dialog.dart`)
- Snooze prefs: `StorageKeys.appUpdateSnoozedVersion` / `appUpdateSnoozedAtIso`

## 7. Testing

- Backend: `tests/SIMF.Api.Tests/AppVersionPolicyPublicTests.cs` (anonymous read,
  configured values, blank→null, non-http→null, deactivated-key ignored) +
  `DefaultContentSeederTests` (seeds 6 empty, idempotent, never resurrects a
  deactivated key).
- App: `test/core/startup/app_version_policy_test.dart` (semver / anti-brick /
  build-metadata / fail-open) + `server_app_update_checker_test.dart` (forced /
  optional / snooze window / platform branch / fail-open) + the splash and
  About widget tests (forced non-dismissible, soft snooze, manual 3-outcome, ar).
- E2E scenarios: `E2E-MOB001-010..017` (splash), `E2E-MOB207-004..006`
  (about-app), `E2E-CFG-024` (CP config).
