# Page 015 — Function (الخريطة · Venue map)

What the user does on the **2D venue map** screen, step by step — as rebuilt to
the KSA Wave-2 frame **215:562** (D-378). Business rules and data sourcing live
in [Page_015_Logic.md](Page_015_Logic.md); the backend contract is in
[Page_015_API.md](Page_015_API.md).

Last updated: 2026-06-13 (D-378 redesign, commit `cf7214e`).

## Identity
| | |
|---|---|
| Route | `RouteNames.venueMap` → `/map` |
| Titles | AR **الخريطة** · EN **Venue map** (route label — the page itself has no header) |
| App privilege | **Public** — Guest and above; no sign-in gate |

## Auth / privilege gate
The screen is **public content** (`AllowAnonymous`). All three backing reads —
`GET /app/venue-map`, `GET /app/booths` and `GET /app/booths/{id}` — are
anonymous, so a **Guest** (not signed in) sees the full map, the info card and
every booth detail sheet. No token, no permission code, no
`RequireApprovedAccount`. A signed-in visitor sees exactly the same page.

## Elements
| # | Element | Purpose |
|---|---------|---------|
| 1 | Collapsed-header `KsaPage` + bottom nav | No app bar / back button; the KSA bottom nav rides the page with the **map tab active** |
| 2 | Map canvas | Full-bleed pan/zoom 2D plane that renders every active node at its `(x, y)` |
| 3 | Node markers | One marker per node, styled by `kind` (Hall / Zone / Booth / PointOfInterest); **all tappable** |
| 4 | Gold map controls | 40px floating buttons at the directional end: reset view / zoom in / zoom out |
| 5 | Info card | White bottom card for the **selected** node: gold name box, title, exhibitor · sector, code chip / close ✕, **أرشدني** + **عرض التفاصيل** |
| 6 | Booth detail sheet | Modal bottom sheet opened by **عرض التفاصيل** (booth nodes only) |
| 7 | Loading indicator | Centred spinner while the two reads are in flight |
| 8 | Empty state | When the node list is empty |
| 9 | Error / retry | When a read fails |

## User actions — step by step
1. **Open the screen.** The app calls `GET /app/venue-map` (nodes) and
   `GET /app/booths` (booth summaries) in parallel — see Logic L-1. While both
   are in flight, a centred spinner shows.
2. **View the map.** Each returned node is drawn at its normalised canvas
   position (Logic L-4) with a marker styled by `kind`. The node `label` /
   `labelArabic` is shown per the active locale (Logic L-3).
3. **Pan / zoom.** Standard 2D gestures move and scale the canvas client-side
   (scale clamped 0.3–4.0). The gold **+** / **−** controls zoom by ×1.3 steps
   anchored on the viewport centre; the **locate** control resets the view to
   the identity transform. No server call — the node set is already fully
   loaded (Logic L-2).
4. **Tap ANY node** (Hall / Zone / Booth / POI). The marker gains the gold
   selection ring and the **info card** appears at the bottom, composed from
   the node label and — for a booth node — the matching `BoothSummary` already
   in memory (Logic L-5): name, exhibitor · sector, booth code.
5. **أرشدني (Guide me).** Centres the map on the selected node at scale 1.5 —
   pure client transform, no server call.
6. **عرض التفاصيل (View details)** — booth nodes only. Opens the **booth detail
   sheet**: the cached summary shows immediately; the description paragraph
   streams in from a lazy `GET /app/booths/{id}` (Logic L-5).
7. **Switch / dismiss the selection.** Tapping another marker switches the
   card; a non-booth card offers a **close ✕** (a booth card shows the code
   chip in that slot instead).
8. **Retry on error.** The error state offers a retry that re-runs step 1.

## Info card / detail sheet content
| Field | Source | Note |
|-------|--------|------|
| Name (AR/EN) | `BoothSummary.nameArabic` / `name` — falls back to the node `labelArabic` / `label` | always real |
| Code | `BoothSummary.code` | booth nodes; shown in the bordered code chip (and drives the gold name box) |
| Exhibitor (AR/EN) | `exhibitorNameArabic` / `exhibitorName` | nullable; joined with Sector by " · " |
| Sector (AR/EN) | `sectorArabic` / `sector` | nullable |
| Description | `BoothDetail.descriptionArabic` / `description` | detail sheet only, via the lazy E3 call |
| **Logo image** | — | **decoration — no DTO field (D11)** |
| **Hall name** | — | only `hallId` (Guid) ships; **name is decoration (D11)** |

## Navigation
- **In:** from the KSA bottom nav (map tab), the home quick tile, and the
  my-seat screen's navigate action.
- **Out:** via the bottom nav tabs (the page has no back button); the booth
  detail sheet is an in-page modal, not a route. A Hall node *may* later
  deep-link into the programme/agenda filtered by that hall (**TO BUILD**,
  Logic L-7).

## Acceptance criteria
- [ ] As a Guest (signed out), opening `/map` renders every active node at its position.
- [ ] Markers are visually distinguished by the four `kind` values.
- [ ] Tapping ANY node selects it (gold ring) and shows the info card; for a
      booth node the card shows the booth's real name, code, exhibitor and sector.
- [ ] **أرشدني** centres the map on the selected node; **عرض التفاصيل** (booth
      only) opens the detail sheet with the lazy description.
- [ ] The gold zoom in / zoom out / reset controls work and stay within the
      0.3–4.0 scale clamp.
- [ ] An empty node list shows the empty state, not a blank canvas.
- [ ] A failed read shows the error state with a working retry.
- [ ] Labels render in the active locale; the chrome mirrors in RTL while the
      **canvas geometry does not** (forced LTR).
- [ ] No logo image or hall **name** is presented as real data
      unless the contract is later extended (D11).
