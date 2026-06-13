# Page 009 — Design (الشروط والأحكام · Terms & conditions) — Flutter

Screen design for the Flutter app. Visual source: the KSA-Project Figma frame
**505:1553** (D-367; supersedes `Mockup.html` screen #9). Data binds to
[Page_009_API.md](Page_009_API.md); rules in [Page_009_Logic.md](Page_009_Logic.md).

Last updated: **2026-06-13** (conformance pass to the as-built code — D-367, fidelity pass D-375).

> **As-built (KSA-Project redesign, 2026-06-11 — D-367, Figma 505:1553; fidelity
> pass 2026-06-12 — D-375):** navy `navySurface` + decorative rotated sweep, custom
> header (back chevron + centred title); the **معلومات هامة لزوار الملتقى** heading;
> the server body rendered as **gold-hairline bullet cards** (radius 8, gold • +
> `beigeBorder` 14/1.5 selectable text — each non-empty body line is one card); a
> single always-enabled gold **موافق** button shown in **BOTH modes** — the interim
> checkbox row + Decline link + last-updated line are gone: the explicit موافق tap
> IS the consent (still client-side only, D8; the back chevron declines via
> `pop(false)` in consent mode). Load/empty/404/error+retry contract unchanged; the
> old screen is parked in `lib/features/_legacy_mockup/`.

## Layout (top → bottom)
1. **Decorative sweep** — a rotated (28.28°) translucent-white rounded rectangle
   anchored top-right (Figma 505:1555), behind the content.
2. **Header band** (56 px, Figma 505:1558) — back chevron ‹ on the left (LTR icon in
   both locales), centred title **الشروط والأحكام** (white, 24 / w500).
3. **Section heading** — **معلومات هامة لزوار الملتقى** (white, 16 / w700,
   inline-start aligned).
4. **Bullet-card list** — one card per non-empty body line, in a scrollable column
   (content max-width 400, 16 px gaps); long content scrolls behind the button.
5. **موافق button** — full-width gold `FilledButton` (label 16 / w700), pinned at the
   bottom (padding 16/24). Per the frame (505:1684) it shows **unconditionally** in
   both modes and is **always enabled**.

There is **no last-updated line** (removed in D-375 — not in the frame), **no
checkbox**, and **no Decline link**.

## Components
| Element | Component |
|---|---|
| Header | Custom 56 px `Stack` band — `IconButton` (`Icons.arrow_back_ios_new`, white, 20, forced LTR) + centred title `Text` |
| Heading | `Text` — `termsImportantInfoTitle` |
| Bullet card | `_BulletCard` — `Container`, gold (`accent`) hairline border **0.2 px**, radius 8, padding 8; gold **•** at the inline start; `SelectableText` body (`beigeBorder`, 14, line-height 1.5) |
| موافق | Full-width `FilledButton` (theme gold), always enabled |
| Loading | Centred `CircularProgressIndicator` (gold `accent`) |
| Empty / error | Centred message (`txtSecondary`) + `FilledButton` retry (`retryLabel`) |

## Data binding — `GET /app/content/terms`
| UI element | API field |
|---|---|
| Title | **fixed UI string** `termsTitle` (الشروط والأحكام / Terms & conditions) — not a response field |
| Heading | **fixed UI string** `termsImportantInfoTitle` (معلومات هامة لزوار الملتقى / Important information for forum visitors) |
| Bullet cards | `content` / `contentArabic` — the localized body (active locale primary, other language as fallback when empty), split on newlines; each non-empty trimmed line is one card |
| — | `lastUpdatedAt` is decoded but **not rendered** (D-375) |

The موافق action binds to **no API** — acceptance is **client-side only** (D8).

## Actions & navigation
| Trigger | Behaviour |
|---|---|
| Tap موافق (standalone) | Leaves the page — `pop(null)`, or `go('/')` when there is nothing to pop. |
| Tap موافق (consent mode, `?consent=1`) | The tap IS the consent — returns `pop(true)` to the calling flow (no server call — D8). |
| Tap back chevron (standalone) | Leaves the page — `pop(null)`, or `go('/')` when there is nothing to pop. |
| Tap back chevron (consent mode) | Declines — returns `pop(false)`; the calling flow stays blocked. |
| Tap retry (empty / error state) | Re-runs the content fetch. |

Body text is plain `SelectableText` — there is **no** HTML/markdown renderer and
links inside the body are **not tappable**.

## States
- **Loading** — centred gold spinner (no skeleton/shimmer).
- **Loaded** — one layout for both modes: heading + bullet cards + the always-shown
  موافق button. Only the pop result differs by mode.
- **Empty** — `404` or a body with no text → "لا يوجد محتوى · No content" + retry.
- **Error** — transport/5xx failure → the failure message + retry.

There is no separate "accepted" confirmation state — موافق immediately pops.

## Localization & direction
AR primary (RTL), EN secondary. Fixed strings from `app_l10n.dart`: `termsTitle`,
`termsImportantInfoTitle`, `termsAcceptButton` (**موافق** / **Agree**), `termsEmpty`
(لا يوجد محتوى / No content), `retryLabel` (إعادة المحاولة / Retry). The body uses the
localized field with cross-language fallback (`localizedBody`); cards use
directional (start/end) padding so the bullet sits at the inline start in both
directions. The back-chevron icon is forced LTR. No date formatting on this page.

## Design notes
- The page **reads** content only; موافق writes **nothing to the server** (D8).
- موافق is **always enabled** — consent is the explicit tap, not a checkbox state
  (D-367); decline is the back chevron, not a separate link.
- The card hairline is kept at **0.2 px** (the frame's stroke) so it still
  rasterises on every phone density (D-375).
- Long terms scroll inside the list area without pushing the pinned موافق button
  off-screen.
