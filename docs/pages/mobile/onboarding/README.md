# Onboarding — التهيئة (Page 002, `#2`)

- **Route:** onboarding (first-run only, gated by the splash on `onboardingCompleted`). Access: **Guest** (pre-auth). No SIMF API.
- **Figma:** **148:22** / 159:942 / 159:1052 (D-362). **Clean-code freeze:** D-636 (2026-07-04).

## Purpose

The first-run three-step carousel: a looping muted background video per step
(D-373) under a navy scrim, over the world-map poster, the brand mark, one
welcome title + per-step body, pill page dots, the gold التالي button, a تخطي
skip (hidden on the last step) and a back chevron (steps 2–3). Finishing or
skipping sets `onboardingCompleted` and routes to sign-in.

## Background — the owner "video not working" fix (2026-07-26)

The owner reported the onboarding background video as *"not exist / not
working"*. Verified on this base: the three clips **do** ship
(`assets/videos/onboard_01..03.mp4`, all three the same hero clip per D-373),
they **are** declared in `pubspec.yaml` (`assets/videos/`), and the screen loads
them muted + looping + autoplaying with `BoxFit.cover`. Two real defects made the
background read as empty, both fixed here:

1. **No poster on steps 2–3.** The fallback was gated on `index == 0`, so any
   step whose decoder failed (a device that refuses the codec, or the test
   runtime) painted **plain navy** with nothing behind the copy. The world-map
   poster now backs **every** step and is always painted underneath the video —
   which also removes the navy flash while a step's decoder warms up.
2. **The video sat under the 90% photo overlay.** `SimfTokens.navyFill90` left
   roughly a tenth of the footage visible, so the motion read as "not there". A
   **playing** video now uses the new **`SimfTokens.navyFill60`** scrim; the
   still poster keeps the design's 90% (`navyFill90`), so the Figma 148:22 state
   is unchanged whenever no video is playing.

The silent `catch (_)` around `_loadVideo` also hid the reason a device refused
the clip; it now `debugPrint`s the asset + error (debug only — a decorative
background must never surface an error to a visitor). Android decode on
Huawei/HiSilicon parts is already covered by the vendored
`third_party/video_player_android` decoder-fallback patch (D-768).

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `onboarding_screen.dart` (241) | `OnboardingScreen` + State — the single-decoder video lifecycle (`_loadVideo` follows the active step), the `PageController` + next/back/skip/complete nav, and the `build` (background + top bar + logo + PageView step + dots + next). Keeps the `_videoAssets` list. |
| `widgets/onboarding_background.dart` (`OnboardingBackground`) | The per-step media layer — the world-map poster on EVERY step, the video `FittedBox` over it when playing, then the navy scrim (60% over video / 90% over the poster) — a `StackFit.expand` Stack rendered inside the screen's `Positioned.fill`. Owns the `_worldMapAsset` const. |
| `widgets/onboarding_top_bar.dart` (`OnboardingTopBar`) | The forced-LTR top bar — back chevron (steps 2–3) + تخطي skip (not the last step). |
| `widgets/onboarding_dots.dart` (`OnboardingDots`) | The forced-LTR pill page dots (active 32×8 beige, inactive 16×8 soft-gold). |

## Tokenisation (this freeze)

The two module-level raw `Color(0x..)` consts became tokens: `_photoOverlay =
Color(0xE601132D)` → new **`SimfTokens.navyFill90`** (navy 90%); `_dotInactive =
Color(0x80D0AC77)` → new **`SimfTokens.goldSoftFill50`** (goldSoft 50%). Exact ARGB
→ render-preserving.

## L4 Figma parity

**No golden** — the `video_player` background + the asset-image (world map / logo)
make a golden flaky/heavy, and this freeze is exact-token-swaps (both byte-identical)
+ verbatim widget extraction (the background's `Positioned.fill`→`StackFit.expand`
relocation is render-equivalent). The **onboarding widget tests are the render
baseline**: they drive the three-step paging, the skip-hides-on-last, the
back-chevron (hidden on step 1), and complete-on-third-next — all pass unchanged.
(Same no-golden call as badge D-633.)

## Level-F

Wired: next/back paging, skip → complete, third next → complete (sets
`onboardingCompleted` → sign-in); best-effort per-step video with graceful
image/navy fallback. No SIMF API.

## Tests

`test/features/onboarding/onboarding_screen_test.dart` (third-next completes, skip
hides on last, back-chevron steps back / hidden on step 1) and
`test/features/onboarding/onboarding_background_test.dart` (the poster backs every
step; the still-poster scrim stays at 90%; the video scrim is lighter). E2E:
`docs/tests/e2e/mobile-onboarding.md`.

## Related decisions

- **D-636** (this clean-code freeze — background/top-bar/dots widgets + 2 tokens).
- **D-362** (KSA static-panels redesign), **D-373** (per-step background video).
- **Owner 2026-07-26** — the "background video not working" fix (poster on every
  step, 60% video scrim, debug-visible decode failure).
