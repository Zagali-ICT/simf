# Page 001 — البداية · Splash

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_001_Function.md](Page_001_Function.md) | What the page does — launch steps, user actions (none), routing decision, acceptance criteria |
| Logic | [Page_001_Logic.md](Page_001_Logic.md) | Boot rules — version check, first-run vs resume, session load, routing, edge cases, dependencies |
| API | [Page_001_API.md](Page_001_API.md) | The backend endpoints this page touches (authoritative contract) — session resume + identity |
| Design | [Page_001_Design.md](Page_001_Design.md) | Flutter screen design — logo layout, states, RTL, transition out |

## Identity
| | |
|---|---|
| Mockup page | **1** (`Mockup.html`) — visuals superseded by the KSA-Project Figma frame **159:573** (D-361) |
| Route | `RouteNames.splash` → `/splash` |
| Titles | AR **البداية** · EN **Splash** |
| Section | 0 — Bootstrap / launch |
| Nature | **Splash / bootstrap** (brand lock-up + store-update check + session restore + route-out) |
| App privilege | **None** — runs before any privilege is known (Guest/Visitor/Moderator/Staff resolved here) |
| Status | **Built**; redesigned 2026-06-11 to the KSA-Project frame 159:573 (**D-361** — visuals only, boot logic unchanged); API spec **reuses shipped endpoints** (no new endpoint) |

## Sources of truth
**KSA-Project Figma frame 159:573** (visual as-built, D-361 — `docs/SIMF-App-Redesign-Program.md` board row 1) ·
`Mockup.html` (Screen 1 — superseded for visuals) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 1 — boot/sign-in flow) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture).

> Owner reference: **Page 001** (mockup Screen #1, "splash"). This is the app's first
> screen on every cold launch. It does **not** introduce a new SIMF endpoint — the
> store-update check is **store-native** (not a SIMF API) and the session/identity
> reads reuse the already-shipped `POST /app/auth/refresh` (only when the cached access
> token is missing/expired) and `GET /app/users/me`.
