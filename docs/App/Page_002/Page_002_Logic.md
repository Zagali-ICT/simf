# Page 002 — Logic (التهيئة · Onboarding)

Business rules behind the screen. There is **no server logic** for this page — all rules
are client-side. The backend contract is in [Page_002_API.md](Page_002_API.md).
As-built per **D-362** (static panels) + **D-373** item 2 (background videos).

## L-1 First-run gate
A single **client-side** boolean — `StorageKeys.onboardingCompleted`
(prefs key `simf.prefs.onboarding_completed`, via `simfPrefsStorageProvider`) — decides
whether this screen runs. The gate lives in the **splash**
(`lib/features/splash/splash_controller.dart`), for the signed-out path:
- **Unset / false** → splash routes to `/onboarding`.
- **True** → splash routes straight to `/sign-in`; the carousel is skipped entirely.

The flag is set to **true** the moment the user taps **التالي on the last step** or
**تخطي** (whichever comes first), then the screen routes to `RouteNames.signIn`
(`/sign-in`) with `goNamed` (stack replacement — Back does not return here).
Reinstalling or clearing app data resets it. No server round-trip.

## L-2 Step / asset model (fixed, bundled)
The carousel is a **fixed 3-step** sequence (`_stepCount = 3` in
`onboarding_screen.dart`); the assets are bundled and addressed by path:

| Step | Title | Body | Background video | Static fallback |
|------|-------|------|------------------|-----------------|
| 1 | `onboardingTitle1` (shared) | `onboardingBody1` | `assets/videos/onboard_01.mp4` | `assets/images/onboarding_world_map.jpg` |
| 2 | `onboardingTitle1` (shared) | `onboardingBody2` | `assets/videos/onboard_02.mp4` | plain navy (`SimfTokens.navy`) |
| 3 | `onboardingTitle1` (shared) | `onboardingBody3` | `assets/videos/onboard_03.mp4` | plain navy (`SimfTokens.navy`) |

Every step is topped by the 90% navy overlay (`0xE601132D`). `onboard_02/03.mp4`
currently ship as copies of the step-1 hero clip — the owner replaces them **in place**
(a content change, not a code change). Adding/removing a *step*, however, is a code
change (the count, bodies and video list are code constants). The old `introd_001..`
stable-name series is retired with the YouTube model (D-362).

## L-3 State transitions
```
Step 1 ──التالي / swipe──▶ Step 2 ──التالي / swipe──▶ Step 3 ──التالي──▶ Done
  │          ◀──back chevron──┘   ◀──back chevron──┘                      │
  └────────────── تخطي (steps 1–2 only) ─────────────────────────────────┤
                                                                          ▼
                                                          set onboardingCompleted = true
                                                          goNamed → /sign-in
```
- Advancement is **manual only** — التالي or a swipe; nothing auto-advances (the
  background videos loop and never drive navigation).
- تخطي from steps 1–2 jumps straight to **Done**; it is hidden on step 3.
- The back chevron (steps 2–3) steps backwards; step 1 has none.
- **Done** sets the first-run flag (L-1) and replaces the stack with `/sign-in`.

## L-4 Background-video rule (D-373)
- **One decoder at a time**: a single `VideoPlayerController` follows the active step —
  on page change the old controller is disposed and the new step's asset is loaded.
- Each clip plays **looping**, **volume 0** (muted by design — there is no sound toggle),
  starting automatically once `initialize()` succeeds.
- If initialization **throws** (tests, unsupported runtime, missing asset) the controller
  is disposed silently and the static fallback stays (world-map photo on step 1, navy on
  2–3). The videos are decorative; they never block or gate anything.
- A stale decode result (the user already moved on) is discarded, not shown.

## L-5 Edge cases
- **Offline on first run** → irrelevant to media: everything is bundled; the screen has
  no network dependency at all.
- **App killed mid-sequence** → flag not yet set, so the carousel replays on next launch
  (acceptable; it is set only on completion/skip).
- **Returning user** → never reaches this screen (L-1 splash gate).
- **No SIMF API** → there is no network error surface here; the only failure mode is the
  local video decoder, which degrades to the static fallback (L-4).

## L-6 Auth / privilege gate
- Runs **before** sign-in; the actor is **Guest**. No token, no auth header, no permission code.
- App authorization is expressed only in the four app roles (Guest/Visitor/Moderator/Staff);
  this screen sits at **Guest** and is therefore reachable by everyone on first run.

## L-7 Localization & direction
Arabic primary (RTL), English secondary. All on-screen text comes from `AppL10n`
(`lib/app/localization/app_l10n.dart`): the shared title `onboardingTitle1`
(«مرحباً بك في تطبيق الملتقى» / "Welcome to the SIMF app"), the three step bodies
`onboardingBody1..3`, `onboardingNext` («التالي» / "Next") and `onboardingSkip`
(«تخطي» / "Skip"). Two elements are deliberately **forced LTR** to match the frames
(which keep LTR-pinned chrome even in the RTL design): the pill page dots (the active
dot progresses left→right) and the back chevron (always points left). The text column
itself follows the active locale.
