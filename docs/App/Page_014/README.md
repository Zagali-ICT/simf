# Page 014 — منطقتي · My Area (dashboard)

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_014_Function.md](Page_014_Function.md) | What the page does — elements, user actions, navigation, acceptance criteria |
| Logic | [Page_014_Logic.md](Page_014_Logic.md) | Business rules — counter definitions, role gating, data sources, edge cases, dependencies |
| API | [Page_014_API.md](Page_014_API.md) | The backend endpoints + DTOs that serve this page (authoritative contract) |
| Design | [Page_014_Design.md](Page_014_Design.md) | Flutter screen design — layout, data binding, states, share intents, localization |

## Identity
| | |
|---|---|
| Mockup page | **14** (`Mockup.html`) |
| Route | `RouteNames.myArea` → `/my-area` |
| Titles | AR **منطقتي** · EN **My Area** |
| Section | 2 — Core screens |
| Nature | **Personal dashboard** (identity + counters + today's schedule + share) |
| App privilege | **Visitor** and above (signed-in-pending = limited) |
| Status | API spec **drafted, not built**; design **drafted** |

## Sources of truth
`Mockup.html` (visual) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 14) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture).

> This folder is the first instance of the per-page documentation structure
> (`docs/App/Page_NNN/`). It supersedes the per-screen detail that previously sat
> inside the monolithic SIMF-MOB-API-001 §6 / SIMF-MOB-SDS-001, which now point here.
