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
| Status | Content endpoint **exists**; accept record **deferred (D8)** — client-side only |

## Owner reference
Owner **page 009**. Visual source `Mockup.html` (screen #9, الشروط والأحكام).

## Sources of truth
`Mockup.html` (visual) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 9) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture) ·
`docs/decisions/DECISIONS_LOG.md` **D8** (acceptance record deferred while Identity schema is frozen).

> This folder follows the per-page documentation structure (`docs/App/Page_NNN/`).
> It supersedes any per-screen detail that previously sat inside the monolithic
> SIMF-MOB-API-001 / SIMF-MOB-SDS-001, which now point here.
