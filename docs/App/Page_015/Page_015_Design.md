# Page 015 — Design (الخريطة · Venue map)

Flutter screen design for the **2D venue map**. Layout, components, data binding,
states, RTL and localization. Behaviour is in
[Page_015_Function.md](Page_015_Function.md); rules in
[Page_015_Logic.md](Page_015_Logic.md); contract in
[Page_015_API.md](Page_015_API.md).

## Identity
| | |
|---|---|
| Route | `RouteNames.venueMap` → `/map` |
| Titles | AR **الخريطة** · EN **Venue map** |
| Privilege | **Public** — Guest and above |

## Layout
```
┌──────────────────────────────────────────┐
│  ← App bar        الخريطة / Venue map      │
├──────────────────────────────────────────┤
│                                            │
│        ┌──────────────────────────┐        │
│        │                          │        │
│        │     2D MAP CANVAS        │        │
│        │   (pan + zoom, nodes)    │        │
│        │                          │        │
│        │   ▢ Hall   ◇ Zone        │        │
│        │   ● Booth  ★ POI         │        │
│        │                          │        │
│        └──────────────────────────┘        │
│                                            │
└──────────────────────────────────────────┘
        ▲ tap a Booth node ▲
┌──────────────────────────────────────────┐
│  Booth popup (bottom sheet)               │
│  ──────────────────────────────────────   │
│  Booth name (AR/EN)            [Code]      │
│  Exhibitor · Sector                        │
│  Description paragraph (detail call)       │
│                              [ Close ]     │
└──────────────────────────────────────────┘
```

## Components
| Component | Binds to | Notes |
|-----------|----------|-------|
| App bar | static titles | back nav; AR/EN by locale |
| Map canvas | `PublicVenueMapNode[]` (E1) | `InteractiveViewer`-style pan/zoom; nodes positioned by `(X, Y)` |
| Node marker | one per node | style by `Kind`; text = `LabelArabic` / `Label` by locale |
| Booth popup | `PublicBoothSummary` (+ optional `PublicBoothDetail`) | bottom sheet opened on Booth-node tap |
| Legend (optional) | the four `Kind` values | static key for marker styles |
| Loading skeleton | — | shimmer over the canvas while E1 + E2 load |
| Empty state | — | zero-node message + icon |
| Error state | — | message + **Retry** |

## Marker styling by `Kind`
| Kind | Int | Marker treatment |
|------|-----|------------------|
| Hall | 0 | footprint / labelled block |
| Zone | 1 | outlined area / chip |
| Booth | 2 | dot / pin — **tappable**, opens popup |
| PointOfInterest | 3 | icon pin (entrance, info, prayer room…) |

Use theme tokens for all colours and type — no hardcoded hex, no inline styles
(global CSS/theme rules; on Flutter use the app theme, not literal colours).

## Data binding
- **On open:** call E1 (`/app/venue-map`) + E2 (`/app/booths`) in parallel
  (Logic L-1). Hold both lists in screen state.
- **Render:** map each `PublicVenueMapNode` to a positioned marker.
- **Booth tap:** look up the `PublicBoothSummary` by `BoothId` from the E2 list;
  open the popup; lazily call E3 (`/app/booths/{id}`) for the description (Logic L-5).
- **Decoration (D11):** the popup shows **no** logo image and **no** hall name —
  neither ships in the contract (Logic L-6). Do not bind a placeholder image or a
  fabricated hall label.

## States
| State | Trigger | UI |
|-------|---------|-----|
| **Loading** | E1/E2 in flight | shimmer skeleton over canvas |
| **Success — map** | nodes > 0 | rendered canvas, markers tappable |
| **Empty** | nodes == 0 | empty illustration + AR/EN message |
| **Error** | any read fails | error message + **Retry** → re-run L-1 |
| **Popup — loading** | E3 in flight | popup shows summary fields, description shimmer |
| **Popup — ready** | E3 ok | full popup |
| **Popup — detail 404** | E3 → `BOOTH_NOT_FOUND` | popup keeps summary, hides description (Logic L-8) |

## RTL & localization
- AR locale mirrors the **chrome**: app bar, back arrow, popup, buttons,
  legend, text alignment.
- The **map canvas geometry is NOT mirrored** — node `(X, Y)` are physical venue
  positions and stay in venue orientation in both locales (Logic L-3 / RTL rule).
  Only the **text inside** markers and the popup follows the locale.
- All labels come from the locale-appropriate field (`LabelArabic` vs `Label`,
  `NameAr` vs `NameEn`, `DescriptionAr` vs `DescriptionEn`).
- Bilingual static strings (titles, empty/error/retry text) come from the app
  resource bundle, not hardcoded.

## Accessibility
- Booth markers are real tap targets with an accessible label (booth name).
- Popup is dismissible by gesture and by the explicit **Close** control.
- Error/empty states are announced; **Retry** is keyboard/focus reachable.
