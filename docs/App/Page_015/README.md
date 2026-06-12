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

## As-built (D-298)

The Flutter `VenueMapScreen` (`features/venuemap/venue_map_screen.dart`) replaces
the `ComingSoonScreen` placeholder. On open it loads `GET /app/venue-map` +
`GET /app/booths` in parallel (`VenueMapRepository`), normalises each node's
`(x, y)` against the loaded set's bounds (L-4), and renders a kind-styled marker
per node on an `InteractiveViewer` pan/zoom plane. Tapping a **Booth** marker
opens a bottom-sheet popup composed from the cached `PublicBoothSummary` plus a
lazy `GET /app/booths/{id}` for the description — a 404/transport failure keeps the
summary and drops the description (L-5/L-8). A four-kind **legend** overlays the
map. The canvas is forced **LTR** so venue geometry is not mirrored in Arabic;
only the chrome/labels follow the locale (L-3). **Pre-build fix:** the booth DTO
field names in this folder were `nameEn/nameAr/…` but the shipped contract is
`name/nameArabic/exhibitorName/sector/description` — corrected here and bound
correctly. Logo + hall-name stay **decoration** (D11 / L-6). Tests:
`venue_map_screen_test.dart` (6) + `venue_map_models_test.dart` (7).
