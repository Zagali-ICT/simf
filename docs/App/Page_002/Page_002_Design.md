# Page 002 — Design (التهيئة · Onboarding) — Flutter

Screen design for the Flutter app. As-built source:
`src/Mobile/simf_app/lib/features/onboarding/onboarding_screen.dart` — the
**KSA-Project redesign** (Figma frames 148:22 / 159:942 / 159:1052, **D-362**,
2026-06-11) plus the **D-373** background videos (2026-06-12). Rules in
[Page_002_Logic.md](Page_002_Logic.md); there is **no API** binding
([Page_002_API.md](Page_002_API.md)) — all media is bundled.

> **History.** D-362 (owner decision) dropped the never-delivered intro **videos**
> for the design's **three static panels**; the old placeholder screen is parked in
> `lib/features/_legacy_mockup/onboarding_screen.dart`. D-373 item 2 then added one
> looping, **muted background video per step** under the panels (decorative only —
> not the old YouTube intro-clip model). The first-run wiring (set
> `onboardingCompleted` → route to sign-in) is unchanged throughout.

## Layout (top → bottom, one `Stack` + `SafeArea` column)
1. **Background stage** (full-bleed, behind everything):
   - the active step's **looping muted background video** (`assets/videos/onboard_0{1..3}.mp4`,
     `BoxFit.cover`), once its decoder is ready;
   - until then / if the decoder fails: the **world-map photo**
     (`assets/images/onboarding_world_map.jpg`, cover) on **step 1**, plain navy
     (`SimfTokens.navy`, `#01132D` scaffold background) on **steps 2–3**;
   - always topped by the design's **90% navy overlay** (`#01132D` at 90% — `0xE601132D`).
2. **Top slot (48 high)** — a **back chevron** (`Icons.arrow_back_ios_new`, white, 20,
   forced **LTR** so it points left even in RTL) at the top-left on **steps 2–3** only;
   the fixed-height slot keeps the layout stable on step 1.
3. **`SimfLogo` (136)** — the palm-and-anchor brand mark, centred.
4. **Copy carousel** — a 170-high swipeable `PageView` (3 steps, 24 horizontal padding):
   - **one shared welcome title** on all three steps (`onboardingTitle1` — the design
     repeats it on every frame): white, 24, w600, line-height 1.5, centred;
   - the **per-step body** (`onboardingBody1..3`): `SimfTokens.beigeBorder` (`#C2B8A2`),
     18, line-height 1.5, centred.
5. **Pill page dots** — active **32×8** beige (`beigeBorder`), inactive **16×8** soft gold
   at 50% (`0x80D0AC77`), pill radius, 200 ms animated resize, forced **LTR** so the
   active dot progresses left→right exactly as in the frames.
6. **Full-width gold التالي** — a stretched `FilledButton` (label 16 w700) on **every**
   step; the design has **no** "ابدأ" last-step variant.
7. **تخطي link** — a `TextButton` (`SimfTokens.accent` gold `#C9A84C`, 18) in a 32-high
   slot under the button; **hidden on the last step** (frame 159:1052), the fixed-height
   slot keeps the button from jumping.

There is **no app bar** and **no bottom nav** on this screen — it is an immersive,
full-screen first-run sequence. There is **no sound toggle** (the background videos are
permanently muted) and **no loading image** distinct from the step-1 photo fallback.

## Component map
| UI element | Source / binding |
|---|---|
| Background video | bundled `assets/videos/onboard_01..03.mp4`, one `VideoPlayerController` at a time following the active step (looping, volume 0, autoplay) |
| Step-1 fallback photo | bundled `assets/images/onboarding_world_map.jpg` |
| Navy overlay | `Color(0xE601132D)` over the whole stage |
| Brand mark | shared `SimfLogo` widget (size 136) |
| Title / bodies | `AppL10n` getters `onboardingTitle1` (shared) + `onboardingBody1..3` |
| Page dots | local `_index` over the 3-step `PageView` (active beige 32×8 / inactive soft-gold 16×8, forced LTR) |
| التالي button | local action → next page (250 ms easeOut); on step 3 → complete (L-3) |
| تخطي link | local action → complete immediately (L-1/L-3); hidden on step 3 |
| Back chevron | local action → previous page; steps 2–3 only |

No field on this screen binds to a SIMF API response — the page makes no SIMF call.

## Actions & navigation
| Trigger | Behaviour |
|---|---|
| Tap التالي (steps 1–2) | Animate to the next step (250 ms, easeOut); dots update; background video swaps to the new step's clip. |
| Tap التالي (step 3) | Complete: set `onboardingCompleted` (L-1) → `goNamed` **`/sign-in`**. |
| Tap تخطي (steps 1–2) | Same completion immediately (flag + route to `/sign-in`). |
| Tap back chevron (steps 2–3) | Animate to the previous step. |
| Swipe the copy carousel | The `PageView` is swipeable; `onPageChanged` updates the index, dots and background video. |

Navigation is `context.goNamed(RouteNames.signIn)` — a stack **replacement**, so Back
does not return to this screen.

## States
- **Video loading / unavailable** — until the step's decoder is ready (or if `initialize()`
  throws — tests, unsupported runtime, missing asset), the static fallback shows: the
  world-map photo on step 1, plain navy on steps 2–3. The carousel is fully usable
  throughout; the user is never blocked on a video.
- **Steps 1–3** — title + per-step body over the background; dots reflect the index;
  تخطي visible on 1–2, back chevron visible on 2–3.
- **Error** — there is **no SIMF API error surface** here; the only failure mode is the
  local video decoder, handled by the silent static fallback. No retry dialog.
- **Success / Done** — التالي on step 3 or تخطي → first-run flag set → routed to
  `/sign-in`; the splash gate (L-1) never routes here again.

## Localization & direction
AR primary (RTL), EN secondary — all copy from `AppL10n`
(`lib/app/localization/app_l10n.dart`): `onboardingTitle1`, `onboardingBody1..3`,
`onboardingNext` (التالي / Next), `onboardingSkip` (تخطي / Skip). Three deliberate
**forced-LTR** exceptions match the frames, which keep LTR-pinned chrome even in the RTL
design: the page dots (active dot travels left→right), the back chevron (always points
left), and nothing else — the text column itself follows the active locale.
`onboardingGetStarted` (ابدأ) and `onboardingTitle2/3` exist in `AppL10n` but are used
**only** by the parked `_legacy_mockup` screen, not this one.

## Design notes
- First-run only — gated by the splash (`splash_controller.dart`): signed-out +
  `onboardingCompleted` unset → `/onboarding`, otherwise `/sign-in`.
- One video decoder at a time (D-373): the controller follows the active step; the old
  controller is disposed before the next loads.
- `onboard_02/03.mp4` currently ship as copies of the step-1 hero clip — the owner
  replaces them **in place** later (a content change, not a code change).
- Background videos are **muted by design** (volume 0, looping) — there is no sound toggle.
- No SIMF backend, no `ApiResult<T>`, no auth header on this screen.
