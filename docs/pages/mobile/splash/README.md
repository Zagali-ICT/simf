# Splash / bootstrap — البداية (Page 001, `#1`)

- **Route:** `/splash` (`RouteNames.splash`). Access: **Guest (entry point)**.
- **API:** `GET /app/version-policy` (anonymous — the D-736 update check); the
  controller also warms `orgProfileProvider` (D-495) and runs the auth
  cold-start restore, then routes out once.
- **Figma:** **159:573** (built D-361). **Clean-code freeze:** D-641 (2026-07-04).

## Purpose

Shows the brand lock-up on the navy primary surface while `SplashController`
runs the boot sequence (Page_001 Logic L-1..L-6): a minimum-display timer
(1200 ms) and the server version-policy update check (5 s cap, fail-open —
D-736) run concurrently, then it waits for the auth cold-start restore (8 s
cap) and emits a one-shot route-out decision — onboarding (first run) /
sign-in / home / add-profile / OTP — or a forced/soft update dialog.

## Edition line — data, not a literal (`#40-residual`, 2026-07-30)

The lock-up's last line used to be the bundled literal
`AppL10n.splashEventLine` ("4th Edition\n23–25 Nov 2026 · Riyadh"), so the very
first screen of the app hardcoded the 2026 edition's dates. It now renders the
CP-configured **Organization Profile** — `eventStartDate`/`eventEndDate` through
the shared bilingual `formatEventDateRange`, plus `locationText` — exactly as
the Home hero already did.

- The profile hydrates **synchronously from the on-device cache**
  (`OrgProfileController.build`), so the dates are already present on the first
  splash frame of every launch after the first; `warm()` then refreshes it.
- `splashEventLine` is retained as the **fallback only** (first-ever run, or an
  edition whose dates are not set), so the slot is never blank.
- The edition ordinal ("النسخة الرابعة / 4th Edition") stays a bundled literal
  (`AppL10n.splashEditionLine`): the profile carries no edition-ordinal field.
- E2E: `E2E-MOB001-018..020`. Tests: `splash_screen_test.dart` (four cases,
  including the absence of the old literal once dates are configured).

## App-update gate (D-736)

`ServerAppUpdateChecker` (`lib/core/startup/server_app_update_checker.dart` +
`app_version_policy.dart`) fetches the anonymous `GET /app/version-policy`
(per-platform `minVersion` / `latestVersion` / `storeUrl` from the six
`appUpdate.android.*` / `appUpdate.ios.*` SystemSettings keys, admin-edited on
the CP `/admin/configuration` page) and compares it against the REAL installed
version (`package_info_plus`, resolved once in `main()` into
`installedAppVersionProvider`; pubspec `1.0.0+2`) with lenient semver
(`pub_semver`, leading-`v` tolerated):

- **installed < `minVersion`** + usable store URL → the FORCED non-dismissible
  dialog "تحديث مطلوب / Update required"; the only action ("تحديث الآن /
  Update now") opens the store URL — the app is unusable until updated.
- **`minVersion` ≤ installed < `latestVersion`** + store URL → the dismissible
  soft prompt "يتوفر تحديث / Update available" (لاحقاً/Later · تحديث الآن/
  Update now). Dismissing it ANY way snoozes THAT version for 3 days (prefs
  `simf.prefs.app_update_snoozed_version` + `…_at_iso`); a newer version
  prompts immediately; a snooze never suppresses a FORCED update.
- **Fail-open everywhere:** any error/timeout (5 s cap), no store URL, or
  unparseable values → the rule is off and boot continues normally
  (anti-brick). A hard block only follows a live successful fetch on this
  launch — never a cached policy.

## Structure

| File | Holds |
|------|-------|
| `splash_screen.dart` (164) | `SplashScreen` (`ConsumerStatefulWidget`) — the centred lock-up (logo / tagline / forum name / edition + date) and the one-shot `_handle` glue that routes out or shows the update dialog exactly once. |
| `splash_controller.dart` | `SplashController` (`Notifier<SplashState>`) + the `SplashLoading/UpdateRequired/Ready` states + `minSplashDurationProvider`. |

## Clean-code freeze (D-641)

The screen was **already clean** — 164 lines, the boot orchestration already
lives in `splash_controller.dart`, fully tokenised (no raw `Color(0x..)`), uses
the shared `SimfLogo`. So this freeze is the **render-lock golden only** (no
code change), completing the per-page DoD.

## L4 Figma parity (frame 159:573)

Captured `splash_159-573.png` (@375×812, ar) and **read it** — the
palm-and-anchor brand mark, the "SAUDI · MOD · RSNF" tagline, the forum name
(الملتقى البحري السعودي الدولي), the النسخة الرابعة edition line and the
٢٥–٢٣ نوفمبر ٢٠٢٦ · الرياض date line, centred on navy. RTL, no tofu.

**Golden technique (two caveats):** the controller is pinned to `SplashLoading`
(via a stub `Notifier`) so the boot timers + one-shot route-out never fire —
only `pump()` is used, never `pumpAndSettle`. The `SimfLogo` is an `Image.asset`
PNG, which does not rasterise under a bare golden pump, so the test precaches it
(`precacheImage` inside `tester.runAsync`) then paints the resolved frame — the
first golden in the suite to render the brand mark.

## Level-F

Read-only entry screen — no user actions on the screen itself; the route-out /
update-dialog decisions are driven by `SplashController`. Covered by
`splash_controller_test.dart` (the sequence) + `splash_screen_test.dart` (the
render + one-shot route-out glue).

## Tests

`test/golden/splash_golden_test.dart` (frame 159:573, @375×812, ar) +
`test/features/splash/splash_screen_test.dart` (incl. the D-736 soft-update
Later/scrim snooze tests) +
`test/features/splash/splash_controller_test.dart` +
`test/core/startup/server_app_update_checker_test.dart` (D-736 —
forced/optional/snooze/fail-open/anti-brick). E2E:
`docs/tests/e2e/mobile-splash.md`.

## Related decisions

- **D-641** (this clean-code freeze — render-lock golden, no code change).
- **D-361** (built to Figma 159:573), **D-431** (always route to Home after
  launch), **D-495** (warm the org profile at splash).
- **D-736** (server version-policy update gate — `GET /app/version-policy`,
  forced/soft dialogs, 3-day soft snooze, fail-open anti-brick). Configuration +
  operator runbook: [`docs/manuals/SIMF-App-Update-Dev-Guide.md`](../../../manuals/SIMF-App-Update-Dev-Guide.md).
