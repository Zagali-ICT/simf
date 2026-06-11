# Page 002 — Design (التهيئة · Onboarding) — Flutter

Screen design for the Flutter app. Layout from `Mockup.html` (Screen 2);
rules in [Page_002_Logic.md](Page_002_Logic.md); there is **no API** binding
([Page_002_API.md](Page_002_API.md)) — all media is bundled/external.

> **As-built (KSA-Project redesign, 2026-06-11 — D-362,
> `features/onboarding/onboarding_screen.dart`).** Owner decision: the intro
> **videos are dropped** in favour of the design's **three static panels**
> (Figma 148:22 / 159:942 / 159:1052) — the world-map photo with a 90% navy
> overlay behind step 1 (`assets/images/onboarding_world_map.jpg`), plain navy
> behind steps 2–3; `SimfLogo` (136) + one shared welcome title + per-step
> body copy; **pill dots** (active 32×8 beige, inactive 16×8 soft gold,
> progressing left→right); a full-width gold **التالي** on every step (no
> "ابدأ" variant); **تخطي** under it (hidden on the last step); a back chevron
> on steps 2–3. The video-player design below is retained for history only;
> the old placeholder screen is parked in `lib/features/_legacy_mockup/`. The
> first-run wiring (set `onboardingCompleted` → route to sign-in) is unchanged.

## Layout (top → bottom)
1. **Full-bleed media stage** — fills the screen; hosts the loading image first, then the
   YouTube/video player for the current clip.
2. **Top-right Skip** — تخطّي text button (FE-5), overlaid on the media.
3. **Top-left Sound toggle** — optional mute/unmute (FE-7), muted by default.
4. **Bottom progress** — 3 segment dots indicating video 1 / 2 / 3; current segment filled.
5. **Bottom Next** — التالي / arrow to advance manually (FE-6); on the last clip it reads
   "ابدأ" / Start and finishes the sequence.

There is **no app bar** and **no bottom nav** on this screen — it is an immersive, full-screen
first-run sequence.

## Component map
| UI element | Source / binding |
|---|---|
| Loading image | bundled asset `introd_loading` |
| Video player | YouTube embed for `introd_001..003` (preferred); bundled clip fallback |
| Progress dots | local index over the ordered media list (L-2) |
| Skip button | local action → set first-run flag, route onward (L-3) |
| Next button | local action → advance index / finish |
| Sound toggle | local player mute state |

No field on this screen binds to a SIMF API response — the page makes no SIMF call.

## Actions & navigation
| Trigger | Behaviour |
|---|---|
| Video ends / Next | Advance to next clip; fill the next progress dot. |
| Skip | Set first-run flag (L-1) → route to next entry screen; pop this screen. |
| Finish video 3 | Same as Skip's terminal step: set flag → route onward → pop. |
| Sound toggle | Mute/unmute the active player. |

## States
- **Loading** — `introd_loading` image fills the stage while clip 1 buffers; Skip already available.
- **Playing** — current intro video plays full-bleed; progress dot reflects index.
- **Empty / media missing** — if a clip cannot load (no YouTube, no fallback), that clip is
  treated as finished and the sequence advances; the user is never blocked (L-4/L-5).
- **Error** — there is **no SIMF API error surface** here; the only failure is external video
  load, handled by the bundled fallback or silent advance. No retry dialog is needed.
- **Success / Done** — sequence complete or skipped → first-run flag set → app routes onward;
  the screen is not reachable again (first-run only).

## Localization & direction
AR primary (RTL), EN secondary. On-screen chrome (Skip / Next / Start) is localized from app
resources. In RTL the progress order and the Skip/Next controls mirror. Video content itself is
fixed media; any baked-in captions ship with the clip.

## Design notes
- First-run only — the screen self-removes from the back stack after completion.
- Media addressed by **stable names** (`introd_loading`, `introd_001..`) so swapping a clip is a
  content change, not a code change.
- Videos **muted by default** with an opt-in sound toggle.
- No SIMF backend, no `ApiResult<T>`, no auth header on this screen.
