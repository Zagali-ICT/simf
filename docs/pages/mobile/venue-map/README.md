# Venue map — الخريطة (Page 015, `#15`)

- **Route:** `/map` (`RouteNames.venueMap`) · also pushed with `targetBoothId` from a booth's "أرشدني" CTA (`boothMap`, #9).
- **Access:** Public (Guest+). Reads are `AllowAnonymous`.
- **Figma:** frame **215:562** "Location".
- **Clean-code freeze:** D-615 (2026-07-04). Legacy doc: `docs/App/Page_015/`.

## Purpose

An interactive 2D venue plane. The node list (`GET /app/venue-map`) and booth
summaries (`GET /app/booths`) load in parallel; tapping a node opens a bottom
info card, and a booth's **عرض التفاصيل** lazily fetches its description
(`GET /app/booths/{id}`). **أرشدني** centres the plane on the node.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `venue_map_screen.dart` (333) | `VenueMapScreen` + State: data load, canvas normalisation, `InteractiveViewer` pan/zoom (`_zoomBy`/`_centreOn`/`_resetView`/`_focusTargetBooth`), branch dispatch |
| `widgets/venue_map_geometry.dart` | `VenueMapBounds` — normalises node `(x, y)` into `[0, 1]` before the canvas map |
| `widgets/venue_map_controls.dart` | `VenueMapControl` — one gold locate/+/− control |
| `widgets/venue_map_marker.dart` | `VenueMapMarker` (+ `_MarkerStyle`) — a node marker styled by kind; gold ring when selected |
| `widgets/venue_map_info_card.dart` | `VenueMapInfoCard` — the bottom white card (code box · title · exhibitor·sector · أرشدني + عرض التفاصيل) |
| `widgets/venue_map_booth_sheet.dart` | `VenueMapBoothSheet` (+ `_SubLine`) — the lazy booth-description sheet |

## Actions (Level-F: all wired)

| Element | Handler |
|---------|---------|
| Marker tap | select node → info card |
| أرشدني | `_centreOn(node)` — centres the plane |
| عرض التفاصيل (booth only) | `_openDetails` → `VenueMapBoothSheet`, `getBoothDetail` (404 → summary only) |
| locate / + / − | `_resetView` / `_zoomBy` |
| `targetBoothId` push | `_focusTargetBooth` selects + centres the booth's node |

## L4 Figma parity (frame 215:562)

Chrome **matches** the frame: gold control stack, info card (code box, title,
exhibitor·sector, gold أرشدني + bordered عرض التفاصيل), bottom nav (الخريطة active).

Two **owner-decided deviations** (flagged, not changed):

1. The frame shows a **geographic map-tile background**; the app renders the
   **2D node-plane** — the **D-199 "2D venue map"** decision (no external map
   provider; egress/NCA-blocked).
~~2. The frame's card has a 60×60 gold **exhibitor-logo badge**; the app uses a
   close-**X** (no logo assets).~~ **Closed 2026-07-27 (FR-LGO-005)** — booths own
   real logo assets now (BoothLogo, D-357 / D-764), so the card renders the badge
   (the booth's own mark via the shared `SimfLogoImage`, booth short-name
   fallback) at the inline start, with the dismiss control keeping its own place
   beside it.

## Tests

`test/features/venuemap/venue_map_screen_test.dart` (10) +
`venue_map_models_test.dart` (7) — markers, info card, the exhibitor logo badge
(and its absence on a non-booth node), booth sheet, lazy detail, 404, empty,
error/retry, RTL geometry, and the wire model binding.
No golden (render covered by the widget tests; a fake-node plane would not
overlay on the geographic frame). E2E: `docs/tests/e2e/mobile-venue-map.md`.
