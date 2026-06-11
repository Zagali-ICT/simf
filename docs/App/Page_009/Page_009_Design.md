# Page 009 — Design (الشروط والأحكام · Terms & conditions) — Flutter

Screen design for the Flutter app. Layout from `Mockup.html` (screen #9); data binds to
[Page_009_API.md](Page_009_API.md); rules in [Page_009_Logic.md](Page_009_Logic.md).

> **As-built (KSA-Project redesign, 2026-06-11 — D-367, Figma 505:1553):**
> navy `navySurface` + decorative sweep, custom header (back chevron +
> centred title); **معلومات هامة لزوار الملتقى** heading + the last-updated
> line; the server body rendered as **gold-hairline bullet cards** (radius 8,
> gold • + `beigeBorder` 14/21 text — each non-empty body line is one card);
> in consent mode a single always-enabled gold **موافق** button — the interim
> checkbox row + Decline link are gone: the explicit موافق tap IS the consent
> (still client-side only, D8; the back chevron declines via `pop(false)`).
> Load/empty/404/error+retry contract unchanged; the old screen is parked in
> `lib/features/_legacy_mockup/`.

## Layout (top → bottom)
1. **App bar** — back ‹, centered title الشروط والأحكام.
2. **Last-updated line** — "آخر تحديث · Last updated {date}" (from `lastUpdatedAt`, always present).
3. **Terms body** — a scrollable rendered-content area (HTML/markdown) filling the page;
   long content scrolls independently.
4. **Accept gate** (in-flow mode only, pinned to the bottom):
   - **Consent checkbox** — أوافق على الشروط والأحكام (defaults unchecked).
   - **Accept button** — متابعة / أوافق, full-width, **disabled** until the checkbox is ticked.
   - **Decline / back** — secondary action that leaves without consent.

In standalone read mode the accept gate (4) is **hidden** and the body fills to the bottom.

## Components
| Element | Component |
|---|---|
| Title bar | App bar with back button |
| Body | Scrollable rich-content view (HTML/markdown renderer) |
| Last-updated | Caption text line |
| Consent | Checkbox + label row |
| Continue | Primary brass button (disabled state until checked) |
| Decline | Text / secondary button |

## Data binding — `GET /app/content/terms`
| UI element | API field |
|---|---|
| Title | **fixed UI string** (الشروط والأحكام / Terms & conditions per active locale) — not a response field |
| Body | `content` / `contentArabic` (per active locale) |
| Last-updated line | `lastUpdatedAt` |

The accept gate binds to **no API** — acceptance is **client-side only** (D8).

## Actions & navigation
| Trigger | Behaviour |
|---|---|
| Tick consent checkbox | Enables the Accept button (in-flow mode). |
| Tap Accept | Sets the **local** consent flag and returns control to the calling flow (no server call — D8). |
| Tap Decline / Back | Leaves without consent; the calling flow stays blocked. |
| Tap external link in body | Opens in the in-app browser / native handler. |

## States
- **Loading** — skeleton: title bar + a few shimmer text lines for the body.
- **Loaded — read only** — content rendered, no accept gate (standalone mode).
- **Loaded — with gate** — content + the bottom accept gate (in-flow mode); Accept disabled until checked.
- **Empty** — content fetch returns no `terms` body → "لا يوجد محتوى · No content" + retry.
- **Error** — content fetch fails → single inline retry.
- **Accepted (client)** — brief confirmation, then control returns to the calling flow.

## Localization & direction
AR primary (RTL), EN secondary. Pick the localized title/body per active locale; render
the body RTL for Arabic, LTR for English. Last-updated date formatted in the device locale.

## Design notes
- The page **reads** content only; the accept gate writes **nothing to the server** (D8).
- Keep the Accept button's disabled→enabled transition tied strictly to the checkbox.
- Long terms must scroll without pushing the pinned accept gate off-screen (in-flow mode).
