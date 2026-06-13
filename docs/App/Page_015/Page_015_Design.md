# Page 015 — Design (الخريطة · Venue map)

Flutter screen design for the **2D venue map**, rebuilt to the KSA Wave-2 frame
**215:562 "Location"** (D-378) — with the frame's Google geographic map
**replaced by the venue 2D node plane** per owner directive. Layout, components,
data binding, states, RTL and localization. Behaviour is in
[Page_015_Function.md](Page_015_Function.md); rules in
[Page_015_Logic.md](Page_015_Logic.md); contract in
[Page_015_API.md](Page_015_API.md).

Last updated: 2026-06-13 (D-378 redesign, commit `cf7214e`).

## Identity
| | |
|---|---|
| Route | `RouteNames.venueMap` → `/map` |
| Titles | AR **الخريطة** · EN **Venue map** (route label only — the page renders **no header**) |
| Privilege | **Public** — Guest and above |

## Layout
```
┌──────────────────────────────────────────┐
│ (no app bar — KsaPage header COLLAPSES:   │
│  no title / back passed; navy surface)    │
│                                    [ ◎ ]  │  ← gold map controls
│        2D MAP CANVAS (full-bleed)  [ ＋ ]  │    (reset / zoom-in /
│        pan + zoom, node markers    [ － ]  │     zoom-out), 40px
│        ▢ Hall  ▢ Zone  ● Booth  ● POI     │
│                                            │
│  ┌──────────────────────────────────────┐  │
│  │ Info card (white) — selected node     │  │
│  │ [GOLD name box]  Name      [CODE] /✕  │  │
│  │                  Exhibitor · Sector   │  │
│  │ [ أرشدني ]        [ عرض التفاصيل ]     │  │
│  └──────────────────────────────────────┘  │
├──────────────────────────────────────────┤
│  KSA bottom nav — **map tab active**       │
└──────────────────────────────────────────┘
        ▲ عرض التفاصيل (booth nodes only) ▲
┌──────────────────────────────────────────┐
│  Booth detail sheet (modal bottom sheet,  │
│  drag handle)                             │
│  Booth name (AR/EN)              [Code]   │
│  Exhibitor · Sector                       │
│  Description paragraph (lazy detail call) │
└──────────────────────────────────────────┘
```
The old four-kind **legend strip was REMOVED** in the D-378 rebuild — the
bottom **info card** (which names the selection) took its place.

## Components
| Component | Binds to | Notes |
|-----------|----------|-------|
| `KsaPage` shell | `tab: SimfTab.map` | no `title`/`header`/`onBack` → the header row **collapses entirely** (full-bleed page); KSA bottom nav with the map tab active |
| Map canvas | `VenueMapNode[]` (E1) | `InteractiveViewer` (`constrained: false`, `minScale 0.3`, `maxScale 4`, `boundaryMargin 200`) over a fixed **1000×1000** design-space `SizedBox`; wrapped in `Directionality(TextDirection.ltr)` |
| `_NodeMarker` | one per node | 34×34 shape styled by `kind` + 9px white label below; **every** marker is tappable (selection); selected node carries a **gold ring** (3px `accent` border vs the 1.5px kind border) |
| `_MapControl` ×3 | — | **40×40 gold** (`SimfTokens.accent`) rounded buttons, navy 22px icon: `my_location` (reset view), `add` (zoom in ×1.3), `remove` (zoom out ÷1.3); stacked `PositionedDirectional(end, top)` |
| `_NodeInfoCard` | selected `VenueMapNode` + matching `BoothSummary?` | white card pinned to the bottom: gold **name box** (64×56, shows the localized name), title, "Exhibitor · Sector" line, **code chip** (booth) / **close ✕** (non-booth), gold **أرشدني** + bordered **عرض التفاصيل** |
| `_BoothSheet` | `BoothSummary?` + lazy `BoothDetail?` | `showModalBottomSheet` with drag handle, opened by **عرض التفاصيل**; name + code chip + exhibitor·sector + description (`FutureBuilder`) |
| Loading | — | centred `CircularProgressIndicator` while E1 + E2 load (no shimmer) |
| Empty state | — | `KsaEmptyState` — `Icons.map_outlined` + `venueMapEmpty` |
| Error state | — | `KsaErrorState` — `venueMapError` + **Retry** (`retryLabel`) |

## Marker styling by `kind`
| Kind | Int | Marker treatment (as built) |
|------|-----|------------------|
| Hall | 0 | navy **rectangle**, `meeting_room_outlined` icon (white), beige border |
| Zone | 1 | deep-navy **rectangle**, `crop_din` icon (beige), beige border |
| Booth | 2 | gold (`accent`) **circle**, `storefront` icon (navy) |
| PointOfInterest | 3 | white **circle**, `place` icon (red `danger`), red border |

All markers (not only Booth) are tappable — a tap selects the node and shows
the info card. The selected marker's border switches to the gold `accent` ring.
All colours/type come from `SimfTokens` — no hardcoded hex, no literal colours.

## Data binding
- **On open:** call E1 (`/app/venue-map`) + E2 (`/app/booths`) in parallel
  (Logic L-1). On success two lookup maps are **precomputed once**: each node's
  canvas position (`Map<String, Offset>`, normalised per L-4) and the booth
  summary by id (`Map<String, BoothSummary>`).
- **Render:** each `VenueMapNode` is drawn as an 80px-wide positioned marker at
  its precomputed canvas offset.
- **Node tap:** sets the **selection** → the bottom info card composes from the
  node label and (for booth nodes) the matching `BoothSummary` from the
  precomputed map (Logic L-5).
- **أرشدني:** client-only — animates nothing, sets the transform to **centre the
  plane on the selected node at scale 1.5**.
- **عرض التفاصيل (booth only):** opens the detail sheet; the description comes
  from the lazy E3 call (`/app/booths/{id}`) — a 404/transport failure keeps the
  summary and simply omits the description (Logic L-5 / L-8).
- **Card composition rule:** the gold name box and the code chip render only
  when a `BoothSummary` resolved for the node (`code != null`); otherwise the
  card shows the close ✕ in the chip's slot.
- **Decoration (D11):** neither the card nor the sheet shows a logo image or a
  hall name — neither ships in the contract (Logic L-6). Do not bind a
  placeholder image or a fabricated hall label.

## States
| State | Trigger | UI |
|-------|---------|-----|
| **Loading** | E1/E2 in flight | centred `CircularProgressIndicator` |
| **Success — map** | nodes > 0 | rendered canvas, all markers tappable, gold controls floating |
| **Selected** | any marker tapped | bottom info card + gold selection ring on the marker |
| **Empty** | nodes == 0 | `KsaEmptyState` (`Icons.map_outlined` + AR/EN message) |
| **Error** | any read fails | `KsaErrorState` + **Retry** → re-run L-1 |
| **Sheet — loading** | E3 in flight | sheet shows summary fields; description slot shows `loadingLabel` (**جارٍ التحميل…** / **Loading…**) |
| **Sheet — ready** | E3 ok | summary + description paragraph |
| **Sheet — detail 404** | E3 → `BOOTH_NOT_FOUND` | sheet keeps the summary, omits the description (Logic L-8) |

## RTL & localization
- AR locale mirrors the **chrome**: the info card, buttons, the detail sheet,
  text alignment, and the **map controls** — which sit at the **directional
  end** (`PositionedDirectional`), i.e. **left in RTL** / right in LTR. This is
  a recorded deviation from the frame's static mock (which shows them right);
  flagged to the designer per the W2 close-out (D-378).
- The **map canvas geometry is NOT mirrored** — the canvas is explicitly wrapped
  in `Directionality(textDirection: TextDirection.ltr)` because node `(x, y)`
  are physical venue positions (Logic L-3).
- The booth **code chip** text is forced `TextDirection.ltr` inside the card.
- All labels come from the locale-appropriate field (`labelArabic` vs `label`,
  `nameArabic` vs `name`, `descriptionArabic` vs `description`), with fallback
  to the other language when one side is empty.
- Bilingual static strings come from `AppL10n`: `venueMapError`
  (**تعذّر تحميل الخريطة.** / **Could not load the map.**), `venueMapEmpty`
  (**لا توجد عناصر على الخريطة بعد** / **No map items yet**), `retryLabel`
  (**إعادة المحاولة** / **Retry**), `venueMapDirectMe` (**أرشدني** /
  **Guide me**), `venueMapViewDetails` (**عرض التفاصيل** / **View details**),
  `loadingLabel` (**جارٍ التحميل…** / **Loading…**) — not hardcoded.

## Accessibility
- **Every** node marker is a real tap target with `Semantics(button: true)` and
  an accessible label (the localized node label).
- The info card's **close ✕** appears on non-booth selections; a booth selection
  shows the code chip instead — selecting another marker switches the card.
- The detail sheet is dismissible by gesture (drag handle) and barrier tap.
- Error/empty states are announced; **Retry** is keyboard/focus reachable.
