# Page 015 — Function (الخريطة · Venue map)

What the user does on the **2D venue map** screen, step by step. Business rules
and data sourcing live in [Page_015_Logic.md](Page_015_Logic.md); the backend
contract is in [Page_015_API.md](Page_015_API.md).

## Identity
| | |
|---|---|
| Route | `RouteNames.venueMap` → `/map` |
| Titles | AR **الخريطة** · EN **Venue map** |
| App privilege | **Public** — Guest and above; no sign-in gate |

## Auth / privilege gate
The screen is **public content** (`AllowAnonymous`). Both backing reads —
`GET /app/venue-map` and `GET /app/booths` — are anonymous, so a **Guest**
(not signed in) sees the full map and every booth popup. No token, no permission
code, no `RequireApprovedAccount`. A signed-in visitor sees exactly the same page.

## Elements
| # | Element | Purpose |
|---|---------|---------|
| 1 | App bar | Title (AR/EN), back navigation |
| 2 | Map canvas | The 2D plane that renders every active node at its `(X, Y)` |
| 3 | Node markers | One marker per node, styled by `Kind` (Hall / Zone / Booth / PointOfInterest) |
| 4 | Booth popup | Bottom sheet / card opened by tapping a **Booth** node |
| 5 | Loading skeleton | While the two reads are in flight |
| 6 | Empty state | When the node list is empty |
| 7 | Error / retry | When a read fails |

## User actions — step by step
1. **Open the screen.** The app calls `GET /app/venue-map` (nodes) and
   `GET /app/booths` (booth summaries) — see Logic L-1. While both are in
   flight, the loading skeleton shows.
2. **View the map.** Each returned node is drawn at its `(X, Y)` with a marker
   styled by `Kind`. The node `Label` / `LabelArabic` is shown per the active
   locale (Logic L-3).
3. **Pan / zoom.** Standard 2D gestures move and scale the canvas client-side.
   No server call — the node set is already fully loaded (Logic L-2).
4. **Tap a Booth node.** The app opens the **booth popup** for that node. The
   node carries a `BoothId`; the app composes the popup from the matching
   `PublicBoothSummary` already in memory (Logic L-5). Optionally it may fetch
   `GET /app/booths/{id}` for the description paragraph (Logic L-5).
5. **Tap a Hall / Zone / POI node.** Shows the node label (and, for a Hall node,
   its `HallId` may deep-link to the relevant programme filter — Logic L-7,
   marked **TO BUILD** there).
6. **Dismiss the popup.** Returns to the map.
7. **Retry on error.** The error state offers a retry that re-runs step 1.

## Booth popup content
| Field | Source | Note |
|-------|--------|------|
| Name (AR/EN) | `PublicBoothSummary.NameArabic` / `Name` | always real |
| Code | `PublicBoothSummary.Code` | always real |
| Exhibitor (AR/EN) | `ExhibitorNameArabic` / `ExhibitorName` | nullable |
| Sector (AR/EN) | `SectorArabic` / `Sector` | nullable |
| Description | `PublicBoothDetail.DescriptionArabic` / `Description` | only via detail call |
| **Logo image** | — | **decoration — no DTO field (D11)** |
| **Hall name** | — | only `HallId` (Guid) ships; **name is decoration (D11)** |

## Navigation
- **In:** from the home / core navigation menu, mockup entry to Screen 15.
- **Out:** back to the previous screen; a Hall node *may* deep-link into the
  programme/agenda filtered by that hall (**TO BUILD**, Logic L-7).

## Acceptance criteria
- [ ] As a Guest (signed out), opening `/map` renders every active node at its position.
- [ ] Markers are visually distinguished by the four `Kind` values.
- [ ] Tapping a Booth node opens the popup with the booth's real name, code,
      exhibitor and sector.
- [ ] An empty node list shows the empty state, not a blank canvas.
- [ ] A failed read shows the error state with a working retry.
- [ ] Labels render in the active locale and the layout mirrors correctly in RTL.
- [ ] No logo image or hall **name** is presented as real data in the popup
      unless the contract is later extended (D11).
