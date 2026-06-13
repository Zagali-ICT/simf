# Page 002 — التهيئة · Onboarding (welcome carousel)

Per-page documentation folder. Everything about this app page lives here.

> Last updated for the as-built **KSA-Project redesign** — D-362 (2026-06-11, static
> panels replace the intro videos, owner decision) + D-373 item 2 (2026-06-12, looping
> muted **background videos** behind the panels).

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_002_Function.md](Page_002_Function.md) | What the page does — the 3-step welcome carousel, user actions, first-run gate, acceptance criteria |
| Logic | [Page_002_Logic.md](Page_002_Logic.md) | Business rules — first-run gate, step/asset model, background-video fallback, edge cases, dependencies |
| API | [Page_002_API.md](Page_002_API.md) | The backend endpoints this page makes (**none**) + the optional future CMS read |
| Design | [Page_002_Design.md](Page_002_Design.md) | Flutter screen design — layout, components, states, RTL, localization |

## Identity
| | |
|---|---|
| Mockup page | **2** (`Mockup.html`) — owner page 002 |
| Route | `RouteNames.onboarding` → `/onboarding` |
| Titles | AR **التهيئة** · EN **Onboarding** |
| Section | 1 — Start & entry |
| Nature | **First-run welcome carousel** — 3 static panels to the KSA-Project Figma frames 148:22 / 159:942 / 159:1052 (D-362), each over a looping muted background video (D-373); shown once, then gated off by the first-run flag |
| App privilege | **Guest / not-logged-in** (runs before any sign-in; no auth gate) |
| Status | **Built** (`src/Mobile/simf_app/lib/features/onboarding/onboarding_screen.dart`); **No API** (owner); finishing or skipping sets `onboardingCompleted` → routes to `/sign-in` |

## Sources of truth
KSA-Project Figma frames 148:22 / 159:942 / 159:1052 (visual) ·
`docs/decisions/DECISIONS_LOG.md` **D-362** (static panels) + **D-373** item 2 (background videos) ·
`src/Mobile/simf_app/lib/features/onboarding/onboarding_screen.dart` (as-built screen) ·
`src/Mobile/simf_app/lib/app/router.dart` (route number / path / labels) ·
SIMF-MOB-API-001 (shared API conventions) · SIMF-MAA-001 (mobile architecture).

## Owner-ref note
The original owner capture in `docs/App/SIMF-APP-Page-Requirements.md` Page 002 —
"loading image, then 3 videos (preferred from a YouTube channel), shown first-time only,
stable media names `introd_001..`, **has NO API**" — is **superseded for the media model**:

- **D-362** (2026-06-11): the intro videos were **dropped** for the KSA-Project design's
  **three static panels** (owner decision; the old "videos" were never-delivered
  placeholders). The old placeholder screen is parked in
  `lib/features/_legacy_mockup/onboarding_screen.dart`.
- **D-373** item 2 (2026-06-12): a looping, muted **background video per step** was added
  under the panels — bundled assets `assets/videos/onboard_01..03.mp4` (the same hero clip
  ships as all three until the owner supplies the real 2nd/3rd clips; replace-in-place).
  This is decorative background, not the old YouTube intro-clip model.

What still holds from the capture: **first-run only** and **NO API**. Per D-249/D-362 the
App build added **no API** for this page; the only optional future remote swap is the
existing **read-only** CMS surface `GET /app/content/{key}` (see
[Page_002_API.md](Page_002_API.md)).
