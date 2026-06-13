# Page 009 — Function (الشروط والأحكام · Terms & conditions)

Functional specification: what the page does for the user. Logic rules are in
[Page_009_Logic.md](Page_009_Logic.md); the backend contract is in
[Page_009_API.md](Page_009_API.md); the visual design is in
[Page_009_Design.md](Page_009_Design.md).

Last updated: **2026-06-13** (conformance pass to the as-built KSA-Project redesign — D-367/D-375).

## Purpose
A **content + consent** screen — it shows the platform's published **Terms &
Conditions** for the active locale as bullet cards under the **معلومات هامة لزوار
الملتقى** heading, and closes with the single gold **موافق** button. When the page is
reached as a consent step (`?consent=1`) the explicit موافق tap IS the consent
(D-367 — the old checkbox gate is gone). It is a read-only content view; the user
does not edit anything.

## Actors
- **Guest / anonymous** — can open and read the terms (e.g. from the More page or
  the sign-up form's underlined terms link).
- **Signed-up / pending / Visitor and above** — same content; when the page is
  reached inside a consent-requiring flow (`?consent=1`) the موافق tap returns the
  consent result to that flow.

## Functional elements
| # | Element | Behaviour |
|---|---------|-----------|
| FE-1 | Header | Back chevron ‹ and centred title الشروط والأحكام. |
| FE-2 | Section heading | معلومات هامة لزوار الملتقى (fixed UI string). |
| FE-3 | Terms bullet cards | Each non-empty line of the localized body (fetched from `GET /app/content/{key}` with the **`terms`** key) renders as one gold-hairline bullet card; the list scrolls. |
| FE-4 | موافق button | Always shown, always enabled (both modes). Standalone: simply leaves the page. Consent mode: the tap IS the consent — records it **client-side only** (D8) and returns `true` to the calling flow. |
| FE-5 | Back chevron as decline | In consent mode the back chevron declines (returns `false`); standalone it just leaves the page. |
| FE-6 | Retry | The empty and error states show a retry button that re-runs the fetch. |

There is **no consent checkbox**, **no separate Decline link**, and **no
last-updated line** (D-367/D-375).

## The consent action (auth / privilege)
- **Reading is open** to any privilege (Guest and above) — no sign-in required to view.
- The **consent meaning** of موافق applies **only** when the caller opens the page
  with the `?consent=1` query flag. In standalone "read the terms" mode موافق just
  leaves the page.
- Accepting is **client-side only** (D8): موافق returns `true` to the hosting flow
  via `pop(true)`. There is **no backend acceptance write** in this version.
- **Currently dormant:** no caller passes `?consent=1` — the More tile and the
  sign-up form's terms link both open the page standalone. The consent mode is
  implemented and waiting for a flow that needs it.

## User actions & navigation
| Action | Result |
|--------|--------|
| Open page (standalone) | Renders the heading + bullet cards + موافق. |
| Tap موافق (standalone) | Leaves the page (back to the previous screen, or home when there is nothing to pop). |
| Tap موافق (consent mode) | Records consent client-side; returns `true` to the calling flow so it can continue. |
| Tap back chevron (consent mode) | Returns `false` — no consent; the calling flow stays blocked. |
| Tap back chevron (standalone) | Returns to the previous screen (or home when there is nothing to pop). |
| Tap retry (empty / error) | Re-runs the content fetch. |

Links inside the body are **not tappable** — the body renders as plain selectable
text (no HTML/markdown renderer).

## Acceptance criteria (functional)
- AC-1 The bullet cards render the published `terms` content for the active locale
  (one card per non-empty body line; cross-language fallback when the locale's body is empty).
- AC-2 The موافق button is always shown and always enabled, in both modes (D-367/D-375).
- AC-3 In standalone mode موافق simply leaves the page — no consent result is produced.
- AC-4 In consent mode (`?consent=1`) tapping موافق records consent **client-side**
  and returns `true` to the calling flow (D8 — no server write).
- AC-5 In consent mode the back chevron returns `false` and keeps the calling flow blocked.
- AC-6 An empty / failed content fetch shows the empty / error state with retry, never a blank screen.
