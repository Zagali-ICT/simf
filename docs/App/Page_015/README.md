# Page 015 — الخريطة · Venue map

Per-page documentation folder. Everything about this app page lives here.

The page is the **2D venue map**: the app fetches a flat list of positioned
**nodes** (halls / zones / booths / points-of-interest) and renders them on a
single pan/zoom 2D plane. Tapping **any** node selects it and shows a bottom
**info card** (sourced from the public booths read for booth nodes); the card's
**عرض التفاصيل** action opens the booth **detail sheet**. The whole screen is
**public** content — no sign-in needed.

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
| Status | **Flutter screen BUILT (D-298), redesigned to KSA Wave-2 frame 215:562 (D-378)** — venue 2D plane (NOT the frame's Google map, owner directive), gold zoom/recentre controls, node tap → bottom info card (أرشدني + عرض التفاصيل); old screen parked in `_legacy_mockup/`; API **built** (D-230 venue-map, D-199 booths) |

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

## As-built (D-298, redesigned D-378 — 2026-06-13, commit `cf7214e`)

The Flutter `VenueMapScreen` (`features/venuemap/venue_map_screen.dart`) was
rebuilt to the KSA Wave-2 frame **215:562 "Location"** — with the frame's
Google geographic map **replaced by the venue 2D node plane** per owner
directive (the `VenueMapNodes` data is the map). The data contract is unchanged
from D-298: on open it loads `GET /app/venue-map` + `GET /app/booths` in
parallel (`VenueMapRepository`), precomputes each node's canvas position
(normalised against the loaded set's bounds onto a 1000×1000 plane, L-4) and a
booth-by-id lookup, and renders a kind-styled marker per node on an
`InteractiveViewer` pan/zoom plane inside a **collapsed-header `KsaPage`**
(map tab active, no app bar). Tapping **any** marker selects it (gold ring) and
shows the bottom **white info card** (gold name box · title · exhibitor · sector
· code chip / close ✕) with **أرشدني** (centres the map on the node at scale
1.5) and — booth nodes only — **عرض التفاصيل**, which opens the detail sheet:
cached summary immediately plus a lazy `GET /app/booths/{id}` for the
description — a 404/transport failure keeps the summary and drops the
description (L-5/L-8). **40px gold zoom-in / zoom-out / reset controls** float
at the directional end (left in RTL — recorded deviation from the frame's
right-side mock). The old four-kind **legend was removed** in favour of the
info card. The canvas is forced **LTR** so venue geometry is not mirrored in
Arabic; only the chrome/labels follow the locale (L-3). Logo + hall-name stay
**decoration** (D11 / L-6). Old screen + test parked in `_legacy_mockup/`.
Tests: `venue_map_screen_test.dart` (8) + `venue_map_models_test.dart` (7).
