# Page 015 — الخريطة · Venue map

Per-page documentation folder. Everything about this app page lives here.

The page is the **2D venue map**: the app fetches a flat list of positioned
**nodes** (halls / zones / booths / points-of-interest) and renders them on a
single 2D plane. Tapping a booth node opens a **booth popup** sourced from the
public booths read. The whole screen is **public** content — no sign-in needed.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_015_Function.md](Page_015_Function.md) | What the page does — elements, user actions, navigation, acceptance criteria |
| Logic | [Page_015_Logic.md](Page_015_Logic.md) | Business rules — node kinds, popup composition, data sources, edge cases, dependencies |
| API | [Page_015_API.md](Page_015_API.md) | The backend endpoints + DTOs that serve this page (authoritative contract) |
| Design | [Page_015_Design.md](Page_015_Design.md) | Flutter screen design — layout, data binding, states, RTL, localization |

## Identity
| | |
|---|---|
| Mockup page | **15** (owner page 015) |
| Route | `RouteNames.venueMap` → `/map` |
| Titles | AR **الخريطة** · EN **Venue map** |
| Section | 2 — Core screens |
| Nature | **2D venue map** (positioned nodes + booth popups) |
| App privilege | **Public** (AllowAnonymous) — Guest and above |
| Status | API **built** (D-230 venue-map, D-199 booths); design **drafted** |

## Sources of truth
`Mockup.html` (visual) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 15) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture) ·
DECISIONS_LOG **D-230** (venue-map nodes) + **D-199 / D-222** (booths).

> Per-page documentation structure (`docs/App/Page_NNN/`). It supersedes the
> per-screen detail that previously sat inside the monolithic SIMF-MOB-API-001 §6 /
> SIMF-MOB-SDS-001, which now point here.
>
> **D11 — booth-popup decoration note:** a booth logo image and a resolved
> hall **name** in the popup are **decoration unless confirmed real**. The
> shipped booth DTOs carry no `LogoUrl` and only a `HallId` (a bare Guid),
> not a hall name — see [Page_015_Logic.md](Page_015_Logic.md) L-6.
