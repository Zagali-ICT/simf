# Page 002 — Function (التهيئة · Onboarding)

Functional specification: what the screen does for the user. Logic rules are in
[Page_002_Logic.md](Page_002_Logic.md); the backend contract (there is none) is in
[Page_002_API.md](Page_002_API.md); the visual design is in
[Page_002_Design.md](Page_002_Design.md). As-built per **D-362** (static panels,
2026-06-11) + **D-373** item 2 (background videos, 2026-06-12).

## Purpose
A **first-run onboarding** carousel shown once, before any sign-in. It presents **three
welcome panels** (one shared title + a per-step body, to the KSA-Project Figma frames
148:22 / 159:942 / 159:1052), each over a looping, muted **background video**. After the
user finishes (التالي on the last step) or skips (تخطي), the app sets the first-run flag
and routes to **sign-in**, and never shows this sequence again on the device unless the
user reinstalls or clears app data.

## Actors
- **Guest** (anonymous, first launch) — sees the full carousel. This is the primary actor.
- **Any returning user** (Guest / Visitor / Moderator / Staff) — does **not** see this
  screen; the splash reads the first-run flag and routes straight to sign-in (or onward).

## Functional elements
| # | Element | Behaviour |
|---|---------|-----------|
| FE-1 | Background video | One looping, **muted** bundled clip per step (`assets/videos/onboard_01..03.mp4`); decorative only. Falls back silently to the step-1 world-map photo / plain navy while loading or if the decoder is unavailable. |
| FE-2 | Step 1 panel | Shared welcome title (`onboardingTitle1`) + body `onboardingBody1`, over the world-map photo fallback. |
| FE-3 | Step 2 panel | Shared welcome title + body `onboardingBody2`. |
| FE-4 | Step 3 panel | Shared welcome title + body `onboardingBody3`. |
| FE-5 | Skip control | **تخطي** — visible on steps 1–2 only (hidden on the last step); completes the sequence immediately. |
| FE-6 | Next / progress | Full-width gold **التالي** on every step + 3 pill page dots (active dot grows, progresses left→right). On step 3, التالي completes the sequence. |
| FE-7 | Back chevron | Steps 2–3 only — returns to the previous step. |

The carousel is also **swipeable** (the copy area is a `PageView`); swiping updates the
dots and the background video like التالي/back do.

## User actions & navigation
| Action | Result |
|--------|--------|
| Launch app (first run) | Splash routes to `/onboarding`; step 1 shows (photo, then its background video once decoded). |
| Tap التالي (steps 1–2) / swipe | Advances to the next panel; the active dot moves; the background video swaps. |
| Tap التالي (step 3) | Sets the first-run flag and routes to **`/sign-in`**. |
| Tap تخطي (steps 1–2) | Sets the first-run flag and routes to **`/sign-in`** immediately. |
| Tap back chevron (steps 2–3) | Returns to the previous panel. |
| Launch app (returning, signed out) | Onboarding is skipped; the splash routes straight to `/sign-in`. |

## Acceptance criteria (functional)
- AC-1 On first launch (signed out, flag unset) the splash routes to the onboarding
  carousel and step 1 renders with its title, body, dots, التالي and تخطي.
- AC-2 Each step plays its bundled background video muted and looping once decoded; while
  it is not available the static fallback (world-map photo on step 1, navy on 2–3) shows
  and the carousel stays fully usable.
- AC-3 تخطي at steps 1–2 sets the first-run flag and routes to `/sign-in`; تخطي is **not**
  shown on step 3.
- AC-4 التالي on step 3 sets the first-run flag and routes to `/sign-in` (there is no
  separate "ابدأ" label — التالي on every step).
- AC-5 On any subsequent launch the sequence is **not** shown again (first-run only,
  splash-gated on `onboardingCompleted`).
- AC-6 The screen requires **no sign-in** and makes **no SIMF API call** (see Page_002_API.md).
- AC-7 All media is bundled: `assets/images/onboarding_world_map.jpg` +
  `assets/videos/onboard_01.mp4` / `onboard_02.mp4` / `onboard_03.mp4` (02/03 are
  replace-in-place placeholders for the owner's real clips).
