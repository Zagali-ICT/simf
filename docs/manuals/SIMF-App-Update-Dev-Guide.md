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
unusable until updated, though it does offer account deletion — see §4); at/above
`minVersion` but below `latestVersion` → a
**dismissible "Update available"** prompt (snoozed 3 days per version); otherwise
boot continues. The same endpoint backs a manual **"Check for updates"** row in
About-the-app. Everything **fails open** — any error/timeout means the app boots
normally.

## 3. The policy — eight SystemSettings keys

The policy is **not** a new table or entity. It is eight rows in the existing
`SystemSettings` key/value store (D-229), edited on the CP **System Configuration**
page (`/admin/configuration`, `PermissionCatalog.Configuration.*`). The keys are
centralised as constants in `SIMF.Common.AppUpdateSettingKeys`:

| Key | Meaning | Format | Empty ⇒ |
|-----|---------|--------|---------|
| `appUpdate.android.minVersion`    | Minimum supported Android version | semver, e.g. `1.2.0` | no forced gate |
| `appUpdate.android.minVersionEnforcedFrom` | **The date the Android minimum starts blocking.** Until it arrives, `minVersion` is not served at all | `yyyy-MM-dd` and nothing else, e.g. `2026-10-15` | **no forced gate — the minimum above does nothing** |
| `appUpdate.android.latestVersion` | Latest released Android version   | semver, e.g. `1.4.0` | falls back to `minVersion`, so a minimum alone still prompts |
| `appUpdate.android.storeUrl`      | Google Play listing the Update button opens | absolute `https://…` | gate + prompt **both off** for Android |
| `appUpdate.ios.minVersion`        | Minimum supported iOS version     | semver | no forced gate |
| `appUpdate.ios.minVersionEnforcedFrom` | **The date the iOS minimum starts blocking.** Same rule | `yyyy-MM-dd` | **no forced gate** |
| `appUpdate.ios.latestVersion`     | Latest released iOS version       | semver | falls back to `minVersion` |
| `appUpdate.ios.storeUrl`          | App Store listing the Update button opens | absolute `https://…` (or `itms-apps://` — but the server only serves http(s)) | gate + prompt **both off** for iOS |

> **A minimum with no enforced-from date blocks nobody.** This is the single most
> important line in this document. Setting `minVersion` used to take effect on the
> fleet's next launch; it now does nothing at all until you also set the date. If
> you need to force an upgrade *now*, enter **today's date or any past date**.
>
> The date is compared **on the server**, not on the device, so the grace period
> works on builds that shipped before the key existed. It is also why the format is
> unforgiving: `yyyy-MM-dd`, parsed `TryParseExact` + `InvariantCulture`. Anything
> else — `15/10/2026`, `2026/10/15`, a timestamp — is ignored and the gate stays
> **off**. Safe, but silent: if a gate you expected is not appearing, check the
> format first.

The eight rows are **seeded empty** by `DefaultContentSeeder` (with the meaning above
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
  nudge); `min` moves rarely and deliberately (hard gate). A blank `latest` now
  falls back to `min`, so setting only a minimum still warns rather than saying
  nothing until the block lands.
- **The enforced-from date fails open in every direction.** Absent, blank,
  deactivated, unparseable, or still in the future all mean *no gate*. Blank has
  to fail open: `/admin/configuration` refuses to save an empty value, so
  unticking **IsActive** is the operator's only off switch, and it is the first
  thing anyone reaches for when a forced update is blocking real users.
- **The hard gate offers account deletion.** The forced dialog is deliberately
  inescapable and is raised *before* sign-in resolves, so without a way out an
  account holder on an old build could not reach the in-app deletion at all —
  which is the App Store 5.1.1(v) failure the app was rejected for twice, simply
  relocated. A third action opens `/privacy#delete-account` and deliberately does
  **not** close the dialog: the user leaves to the browser and comes back to the
  same block, because the escape is from the dead end, not from the update.
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
4. **Raise `minVersion` when you intend to force the upgrade eventually** — this
   alone blocks nobody, so it is safe to set on release day. Leaving
   `latestVersion` blank is fine: it falls back to the minimum, so users get the
   dismissible prompt from day 0 rather than silence followed by a wall.
5. **Set `minVersionEnforcedFrom` to the date the block should begin.** Two rules,
   both learned the hard way:
   - **Never a date that falls while a rollout is still staged.** The build must be
     at 100% on Play and out of iOS Phased Release. Forcing users while a staged
     rollout still gates availability strands the ones who cannot yet download it —
     the Update button would have nothing to install.
   - **Allow at least 14 days.** Two to three days for auto-update to do its work,
     then a week or so of the dismissible prompt, and only then the block. Reserve a
     shorter window for something that genuinely warrants it, such as a security fix.
6. To **un-brick** in an emergency, blank or untick `minVersionEnforcedFrom` — the
   next launch's live fetch clears the gate immediately (no app redeploy). Lowering
   or blanking `minVersion` still works too. Note that unticking **IsActive** is the
   only way to empty a value from the CP: `/admin/configuration` refuses to save a
   blank one. That is exactly why an inactive key fails open.

Rule of thumb: **`latest` and `min` up on every release; the enforced-from date set
separately, only after 100% availability, and never less than 14 days out.**

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
  configured values, blank→null, non-http→null, deactivated-key ignored, plus the
  enforced-from gate: no date blocks nobody, the date is inclusive, any other
  format fails open, deactivating the date lifts the gate, and a minimum alone
  still prompts) + `DefaultContentSeederTests` (seeds every key empty, idempotent,
  never resurrects a deactivated key).
- App: `test/core/startup/app_version_policy_test.dart` (semver / anti-brick /
  build-metadata / fail-open) + `server_app_update_checker_test.dart` (forced /
  optional / snooze window / platform branch / fail-open) + the splash and
  About widget tests (forced non-dismissible, soft snooze, manual 3-outcome, ar).
- E2E scenarios: `E2E-MOB001-010..017` (splash), `E2E-MOB207-004..006`
  (about-app), `E2E-CFG-024` (CP config).
