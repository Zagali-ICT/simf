# Page 009 — Function (الشروط والأحكام · Terms & conditions)

Functional specification: what the page does for the user. Logic rules are in
[Page_009_Logic.md](Page_009_Logic.md); the backend contract is in
[Page_009_API.md](Page_009_API.md); the visual design is in
[Page_009_Design.md](Page_009_Design.md).

## Purpose
A **content + accept-gate** screen — it shows the platform's published **Terms &
Conditions** text for the active locale, and (when reached from a flow that requires
consent) presents an **Accept** action that lets the user continue. It is a read-only
content view; the user does not edit anything.

## Actors
- **Guest / anonymous** — can open and read the terms (e.g. from a footer / "More" link).
- **Signed-up / pending / Visitor and above** — same content; when the page is reached
  inside a consent-requiring flow (e.g. a sign-up step), the **accept gate** is shown and
  the user must accept to proceed.

## Functional elements
| # | Element | Behaviour |
|---|---------|-----------|
| FE-1 | Header | Back ‹ and centered title الشروط والأحكام. |
| FE-2 | Terms body | Scrollable rendered terms content (HTML/markdown) for the active locale, fetched from `GET /app/content/{key}` with the **`terms`** key. |
| FE-3 | Last-updated line | Optional "آخر تحديث · Last updated {date}" surfaced from the content payload when present. |
| FE-4 | Accept checkbox / toggle | أوافق على الشروط والأحكام — only shown in a consent-requiring flow; defaults unchecked. |
| FE-5 | Accept (continue) button | Enabled only once FE-4 is checked; records consent **client-side** (D8) and continues the flow. |
| FE-6 | Decline / back | Leaves the page without continuing; no consent recorded. |

## The accept gate (auth / privilege)
- **Reading is open** to any privilege (Guest and above) — no sign-in required to view.
- The **accept gate** (FE-4/FE-5) appears **only** when the page is entered as a consent
  step of a larger flow. In standalone "read the terms" mode it is hidden.
- Accepting is **client-side only** (D8): the app records the consent locally (and lets the
  hosting flow proceed). There is **no backend acceptance write** in this version.

## User actions & navigation
| Action | Result |
|--------|--------|
| Open page (standalone) | Renders the terms content; no accept gate. |
| Tick "أوافق" + tap Accept (in-flow) | Records consent client-side; returns control to the calling flow so it can continue. |
| Tap Decline / Back (in-flow) | Returns without recording consent; the calling flow stays blocked. |
| Tap Back (standalone) | Returns to the previous screen. |
| Tap an external link inside the body | Opens in the in-app browser / native handler. |

## Acceptance criteria (functional)
- AC-1 The terms body renders the published `terms` content for the active locale.
- AC-2 In standalone mode the page shows content only — no accept gate.
- AC-3 In a consent-requiring flow the Accept button is disabled until the checkbox is ticked.
- AC-4 Accepting records consent **client-side** and unblocks the calling flow (D8 — no server write).
- AC-5 Declining / leaving records nothing and keeps the calling flow blocked.
- AC-6 An empty / failed content fetch shows the empty / error state with retry, never a blank screen.
