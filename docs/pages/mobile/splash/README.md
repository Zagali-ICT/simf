# Splash / bootstrap — البداية (Page 001, `#1`)

- **Route:** `/splash` (`RouteNames.splash`). Access: **Guest (entry point)**.
- **API:** none directly — the controller warms `orgProfileProvider` (D-495) and
  runs the store-update check + auth cold-start restore, then routes out once.
- **Figma:** **159:573** (built D-361). **Clean-code freeze:** D-641 (2026-07-04).

## Purpose

Shows the brand lock-up on the navy primary surface while `SplashController`
runs the boot sequence (Page_001 Logic L-1..L-6): a minimum-display timer
(1200 ms) and the store-update check (5 s cap) run concurrently, then it waits
for the auth cold-start restore (8 s cap) and emits a one-shot route-out
decision — onboarding (first run) / sign-in / home / add-profile / OTP — or a
forced/soft update dialog.

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
`test/features/splash/splash_screen_test.dart` +
`test/features/splash/splash_controller_test.dart`. E2E:
`docs/tests/e2e/mobile-splash.md`.

## Related decisions

- **D-641** (this clean-code freeze — render-lock golden, no code change).
- **D-361** (built to Figma 159:573), **D-431** (always route to Home after
  launch), **D-495** (warm the org profile at splash).
