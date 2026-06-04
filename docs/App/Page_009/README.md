# Page 009 — الشروط والأحكام · Terms & conditions

Per-page documentation folder. Everything about this app page lives here.

The page presents the platform's **Terms & Conditions** content and an **accept
gate**: it renders the published terms text for the active locale and, where a flow
requires consent, captures the user's acceptance before they continue. Per **D8** the
auditable acceptance **record is deferred** (the Identity schema is frozen), so the
accept is **client-side only** for now — no backend write, no new endpoint.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_009_Function.md](Page_009_Function.md) | What the page does — elements, the accept gate, user actions, navigation, acceptance criteria |
| Logic | [Page_009_Logic.md](Page_009_Logic.md) | Client + server logic, state transitions, validation, error/empty/RTL handling, dependencies |
| API | [Page_009_API.md](Page_009_API.md) | The backend endpoint that serves this page (authoritative contract) + the deferred accept record |
| Design | [Page_009_Design.md](Page_009_Design.md) | Flutter screen design — layout, components, states, localization |

## Identity
| | |
|---|---|
| Mockup page | **9** (`Mockup.html`) |
| Route | `RouteNames.terms` → `/terms` |
| Titles | AR **الشروط والأحكام** · EN **Terms & conditions** |
| Nature | **Content + accept gate** (display published terms, optionally capture consent) |
| App privilege | **Guest** and above (readable by anyone; the accept gate appears only inside a consent-requiring flow) |
| Status | **🟢 Screen built** (D-290) — Flutter `TermsScreen` wired to `GET /app/content/terms`; accept record **deferred (D8)** — client-side only |

## As built (Flutter, D-290)
`TermsScreen` (route `terms` → `/terms`, anonymous — Guest and above, not in the
auth gate) loads `GET /app/content/terms` (the `terms` content key) and renders the
localized body (AR primary / EN fallback) + an optional `Last updated · {date}`
line. **Two modes** (Page_009 L-2), chosen by the caller via the `?consent=1` query
flag: **standalone read** (body only) and **in-flow consent** (a bottom accept gate
— checkbox + Accept enabled only once ticked + Decline; acceptance is **client-side
only**, D8, and hands control back via `pop`). A **404** renders the empty state
("No content"); a transport/5xx failure renders the error message + retry (L-6). The
body is shown as plain **selectable text** in this interim UI — rich HTML/markdown
rendering lands with the final design (SIMF-VID-001), to avoid adding a renderer
package now. New app-local content layer (`ContentBlock` + `ContentRepository` over
the shared `simfApiClient`) — the first CMS-read consumer in the app. No caller wires
the `?consent=1` step yet (the current sign-up flow does not route through terms); the
gate is implemented and dormant until a flow needs it.
Owner **page 009**. Visual source `Mockup.html` (screen #9, الشروط والأحكام).

## Sources of truth
`Mockup.html` (visual) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 9) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture) ·
`docs/decisions/DECISIONS_LOG.md` **D8** (acceptance record deferred while Identity schema is frozen).

> This folder follows the per-page documentation structure (`docs/App/Page_NNN/`).
> It supersedes any per-screen detail that previously sat inside the monolithic
> SIMF-MOB-API-001 / SIMF-MOB-SDS-001, which now point here.
