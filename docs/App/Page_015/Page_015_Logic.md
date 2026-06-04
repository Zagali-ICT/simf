# Page 015 — Logic (الخريطة · Venue map)

Business rules, client + server logic, state transitions, validation and edge-case
handling for the **2D venue map**. The user flow is in
[Page_015_Function.md](Page_015_Function.md); the wire contract in
[Page_015_API.md](Page_015_API.md).

## Data sources
| Source | Endpoint | Carries |
|--------|----------|---------|
| Nodes | `GET /app/venue-map` (D-230) | positioned node list — `Id, Label, LabelArabic, Kind, X, Y, HallId?, BoothId?` |
| Booths | `GET /app/booths` (D-199) | booth summary list — name/code/exhibitor/sector + `HallId?, MapX?, MapY?` |
| Booth detail | `GET /app/booths/{id}` (D-199) | adds `DescriptionEn` / `DescriptionAr` |

Both are **public, read-only** reads over existing tables. No schema/enum change
is introduced by this page.

## Rules

### L-1 — Two reads on open
On entry the app issues `GET /app/venue-map` and `GET /app/booths` together. The
node list drives the canvas; the booth list is the **lookup** that fills booth
popups without a per-tap round-trip. The screen is ready when **both** complete.

### L-2 — Server returns the full set; pan/zoom is client-only
`/app/venue-map` returns **all active nodes** in one shot (no paging, no
viewport query). Panning and zooming are pure client transforms over the loaded
set — there is **no** map-tile or bbox endpoint, and none is planned for this page.

### L-3 — Node kind drives the marker; locale drives the label
`Kind` is the frozen `VenueMapNodeKind` enum:

| Value | Int | Meaning |
|-------|-----|---------|
| `Hall` | 0 | A hall footprint |
| `Zone` | 1 | A grouping / area |
| `Booth` | 2 | An exhibition booth (links a `BoothId`) |
| `PointOfInterest` | 3 | POI (entrance, info, prayer room, etc.) |

The marker style is chosen per `Kind`. The shown text is `LabelArabic` when the
app locale is Arabic, else `Label`.

### L-4 — Coordinate space
`X` / `Y` are `double`s in the map's own design space (set by the CP when the node
was placed, D-230). The client maps them onto the canvas; it must **not** assume a
fixed range — it normalises against the min/max of the loaded set (or a known
design extent) before rendering.

### L-5 — Booth popup composition
A Booth node carries `BoothId`. On tap the app finds the matching
`PublicBoothSummary` (already loaded, L-1) by `Id` and shows name / code /
exhibitor / sector. For the **description paragraph** it calls
`GET /app/booths/{id}` (returns `PublicBoothDetail`). If the booth list and the
node's `BoothId` disagree (booth deactivated after the node loaded), the popup
falls back to the node `Label` and offers no detail — see L-8.

### L-6 — D11: logo + hall-name are decoration, not data
The shipped booth DTOs (`PublicBoothSummary` / `PublicBoothDetail`) carry **no
logo URL** and only a `HallId` (a bare `Guid`), **not** a hall name. Therefore:
- A booth **logo image** in the popup is **decoration** — there is no contract
  field. Do not render a placeholder as if it were real exhibitor branding.
- A booth **hall name** is **decoration** — only the `HallId` ships. Resolving it
  to a display name would need a hall lookup that this page does not call.

Both stay decoration **unless the contract is confirmed/extended** (would be a
new field → owner approval per the freeze rules). Flagged in
[README.md](README.md) and Function.

### L-7 — Hall-node deep link — **TO BUILD**
A Hall node carries `HallId`. Deep-linking from a Hall node into the
programme/agenda filtered by that hall is **not built on this page** — it depends
on the agenda screen accepting a hall filter. Until then a Hall-node tap shows
only the label. No new endpoint is required for the map itself.

### L-8 — Empty / stale / missing handling
| Case | Behaviour |
|------|-----------|
| Empty node list | Show the empty state (Function #6), not a blank canvas |
| Booth node with `BoothId` not in the loaded booth list | Popup uses node `Label`; no detail call (L-5) |
| `GET /app/booths/{id}` → 404 (`BOOTH_NOT_FOUND`) | Show name/code/sector from the summary; hide description |
| A node with null `X`/`Y` | Skip rendering that node (defensive; CP should not emit it) |

## State transitions (client)
```
            open /map
                │
                ▼
          ┌──────────┐  both reads in flight
          │ Loading  │
          └────┬─────┘
       success │           any read fails
        ┌──────┴───────┐        │
        ▼              ▼        ▼
   nodes > 0      nodes == 0   ┌────────┐
   ┌────────┐     ┌────────┐   │ Error  │──retry──▶ Loading
   │  Map   │     │ Empty  │   └────────┘
   └───┬────┘     └────────┘
       │ tap Booth node
       ▼
   ┌──────────────┐  (optional GET /app/booths/{id})
   │ Booth popup  │──dismiss──▶ Map
   └──────────────┘
```

## Validation
No user input on this page → no client-side form validation. The only server
errors are transport / 404-on-detail (L-8) and the standard envelope errors
([Page_015_API.md](Page_015_API.md)).

## Error / empty / RTL
- **Error:** any read failure → Error state with retry that re-runs L-1.
- **Empty:** zero nodes → Empty state.
- **RTL:** when the locale is Arabic the chrome (app bar, popup, buttons) mirrors;
  the **map canvas geometry is NOT mirrored** — node `(X, Y)` are physical venue
  positions and must stay in venue orientation regardless of text direction.
  Only labels/text inside markers and the popup follow the locale.

## Dependencies
- **Built:** `GET /app/venue-map` (D-230), `GET /app/booths` + `/{id}` (D-199).
- **Not built (out of this page's scope):** Hall-node → agenda deep link (L-7);
  any booth logo / hall-name field (L-6, D11) — both require contract or screen
  work elsewhere and **owner approval** for new fields.
