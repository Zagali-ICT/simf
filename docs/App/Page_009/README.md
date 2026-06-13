# Page 009 — الشروط والأحكام · Terms & conditions

Per-page documentation folder. Everything about this app page lives here.

Last updated: **2026-06-13** (conformance pass to the as-built KSA-Project redesign — D-367, fidelity pass D-375).

The page presents the platform's **Terms & Conditions** content and a **consent
action**: it renders the published terms for the active locale as **bullet cards**
and closes with the design's single gold **موافق** button. Per the KSA-Project frame
(D-367) the old checkbox accept-gate is gone — the explicit **موافق tap IS the
consent**. Per **D8** the auditable acceptance **record is deferred** (the Identity
schema is frozen), so the accept is **client-side only** for now — no backend write,
no new endpoint.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_009_Function.md](Page_009_Function.md) | What the page does — elements, the موافق consent action, user actions, navigation, acceptance criteria |
| Logic | [Page_009_Logic.md](Page_009_Logic.md) | Client + server logic, state transitions, validation, error/empty/RTL handling, dependencies |
| API | [Page_009_API.md](Page_009_API.md) | The backend endpoint that serves this page (authoritative contract) + the deferred accept record |
| Design | [Page_009_Design.md](Page_009_Design.md) | Flutter screen design — layout, components, states, localization |

## Identity
| | |
|---|---|
| Mockup page | **9** (`Mockup.html`) — superseded visually by the KSA-Project Figma frame **505:1553** (D-367) |
| Route | `RouteNames.terms` → `/terms` (`?consent=1` selects consent mode) |
| Titles | AR **الشروط والأحكام** · EN **Terms & conditions** |
| Nature | **Content + consent action** (display published terms; the موافق tap is the consent where a flow requires it) |
| App privilege | **Guest** and above (readable by anyone; consent mode is selected by the caller via `?consent=1`) |
| Status | **🟢 Rebuilt to the KSA-Project design** (D-367, 2026-06-11; fidelity pass D-375) — Flutter `TermsScreen` wired to `GET /app/content/terms`; accept record **deferred (D8)** — client-side only |

## As built (Flutter, D-367 + D-375)
`TermsScreen` (route `terms` → `/terms`, anonymous — Guest and above, not in the
auth gate, excluded from cold-start resume) loads `GET /app/content/terms` (the
`terms` content key) and renders the KSA frame 505:1553: navy `navySurface` +
decorative rotated sweep, custom header (back chevron + centred **الشروط والأحكام**),
the **معلومات هامة لزوار الملتقى** heading, and each non-empty line of the localized
body (AR primary / EN fallback) as one **gold-hairline bullet card** (gold •,
selectable `beigeBorder` text). Per the frame there is **no last-updated line**
(removed in D-375) and **no checkbox**: the always-enabled gold **موافق** button shows
in **both modes** (Page_009 L-2, chosen by the caller via the `?consent=1` query
flag) — **standalone read**: موافق simply leaves the page (`pop`); **in-flow
consent**: the موافق tap IS the consent and returns `pop(true)`, while the back
chevron declines via `pop(false)`. Acceptance is **client-side only** (D8). A **404**
renders the empty state ("لا يوجد محتوى · No content") with retry; a transport/5xx
failure renders the error message + retry (L-6). The body lines are plain
**selectable text** — there is no HTML/markdown renderer and links in the body are
not tappable. Content layer: `ContentBlock` + `ContentRepository` over the shared
`simfApiClient`. Current entry points (More tile + the sign-up form's underlined
terms link) are both **standalone** — no caller passes `?consent=1` yet, so the
consent mode is implemented and dormant until a flow needs it. The previous
mockup-era screen is parked in `lib/features/_legacy_mockup/`.
Owner **page 009**. Visual source: KSA-Project Figma frame **505:1553**.

## Sources of truth
KSA-Project Figma frame **505:1553** (visual — D-367/D-375; supersedes `Mockup.html`
screen #9) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 9) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture) ·
`docs/decisions/DECISIONS_LOG.md` **D-367** (redesign + single-موافق consent),
**D-375** (fidelity pass — last-updated line removed, موافق in both modes),
**D8** (acceptance record deferred while Identity schema is frozen).

> This folder follows the per-page documentation structure (`docs/App/Page_NNN/`).
> It supersedes any per-screen detail that previously sat inside the monolithic
> SIMF-MOB-API-001 / SIMF-MOB-SDS-001, which now point here.
