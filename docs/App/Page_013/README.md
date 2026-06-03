# Page 013 — الرئيسية · Home (router screen)

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_013_Function.md](Page_013_Function.md) | What the page does — elements, user actions, navigation, privilege gate, acceptance criteria |
| Logic | [Page_013_Logic.md](Page_013_Logic.md) | Client + server logic, state transitions, privilege gating, validation, error/empty/RTL handling |
| API | [Page_013_API.md](Page_013_API.md) | The backend calls behind this page (authoritative contract) |
| Design | [Page_013_Design.md](Page_013_Design.md) | Flutter screen design — layout, components, states, localization, RTL |

## Identity
| | |
|---|---|
| Mockup page | **13** (`Mockup.html`) — owner refers to it as **"Page 012"** |
| Route | `RouteNames.home` → `/` |
| Titles | AR **الرئيسية** · EN **Home** |
| Section | 1 — Entry / router screen |
| Nature | **Home landing** (router screen 13; entry surface after boot) |
| App privilege | **All privileges** — Guest / Visitor / Staff / Moderator. **No login required.** |
| Status | **No data for now**; features gated by app privilege; on-login bundle **(TO BUILD, in-progress, D9)** |

## Sources of truth
`Mockup.html` (visual) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 13) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture).

> **Owner-ref note:** the owner calls this screen **"Page 012"** in conversation,
> but it is router screen **#13** (`path=/`) in `Mockup.html`. This folder is keyed
> to the mockup number (013) to stay consistent with the rest of `docs/App/Page_NNN/`.
>
> Home is a **privilege-gated landing**. It needs **no login** to open and carries
> **no data of its own for now**. What it shows is shaped by the app privilege
> (`Guest`/`Visitor`/`Staff`/`Moderator`), which comes from the **JWT claim**. On a
> successful login the app fetches **all data + privileges once** and caches them
> (the on-login bootstrap bundle, `GET /app/bootstrap`, is **TO BUILD** this wave — D9).
