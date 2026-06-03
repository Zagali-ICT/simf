# Page 002 — Function (التهيئة · Onboarding)

Functional specification: what the screen does for the user. Logic rules are in
[Page_002_Logic.md](Page_002_Logic.md); the backend contract (there is none) is in
[Page_002_API.md](Page_002_API.md); the visual design is in
[Page_002_Design.md](Page_002_Design.md).

## Purpose
A **first-run onboarding** sequence shown once, before any sign-in. It opens with a
**loading image** (splash/brand hold) and then plays **three short intro videos**
(preferred source **YouTube**) that introduce SIMF. After the user finishes or skips,
the app advances to the next entry screen and never shows this sequence again on the
device unless the user reinstalls or clears the first-run flag.

## Actors
- **Guest** (anonymous, first launch) — sees the full sequence. This is the primary actor.
- **Any returning user** (Guest / Visitor / Moderator / Staff) — does **not** see this
  screen; the first-run flag has already been set and the app skips straight past it.

## Functional elements
| # | Element | Behaviour |
|---|---------|-----------|
| FE-1 | Loading image | Brand/loading still (`introd_loading` asset) shown first while the first video buffers. |
| FE-2 | Intro video 1 | First intro clip — `introd_001` (preferred YouTube embed; bundled fallback). |
| FE-3 | Intro video 2 | Second intro clip — `introd_002`. |
| FE-4 | Intro video 3 | Third intro clip — `introd_003`. |
| FE-5 | Skip control | تخطّي — skips the rest of the sequence and advances immediately. |
| FE-6 | Next / progress | Advances to the next video; visual progress (3 dots / segments). |
| FE-7 | Mute / sound toggle | Optional sound control for the videos (muted-by-default). |

## User actions & navigation
| Action | Result |
|--------|--------|
| Launch app (first run) | Loading image (FE-1), then videos 1→2→3 auto-advance. |
| Tap Next / video ends | Advances to the next intro video; progress dot fills. |
| Tap Skip (FE-5) | Sets the first-run flag and advances to the next entry screen. |
| Finish video 3 | Sets the first-run flag and advances to the next entry screen. |
| Launch app (returning) | Onboarding is skipped entirely; app routes straight on. |

## Acceptance criteria (functional)
- AC-1 On first launch the loading image shows first, then the 3 intro videos play in order.
- AC-2 The preferred video source is **YouTube**; a bundled/local fallback plays if YouTube is unavailable.
- AC-3 Skip at any point advances immediately and sets the first-run flag.
- AC-4 Finishing video 3 advances and sets the first-run flag.
- AC-5 On any subsequent launch the sequence is **not** shown again (first-run only).
- AC-6 The screen requires **no sign-in** and makes **no SIMF API call** (see Page_002_API.md).
- AC-7 Media is addressed by the **stable names** `introd_loading`, `introd_001`, `introd_002`, `introd_003`.
