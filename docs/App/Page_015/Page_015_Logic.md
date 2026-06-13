# Page 015 — Logic (الخريطة · Venue map)

Business rules, client + server logic, state transitions, validation and edge-case
handling for the **2D venue map** (KSA Wave-2 rebuild, D-378 — the data contract
is unchanged from the D-298 build). The user flow is in
[Page_015_Function.md](Page_015_Function.md); the wire contract in
[Page_015_API.md](Page_015_API.md).

Last updated: 2026-06-13 (D-378 redesign, commit `cf7214e`).

## Data sources
| Source | Endpoint | Carries |
|--------|----------|---------|
| Nodes | `GET /app/venue-map` (D-230) | positioned node list — `id, label, labelArabic, kind, x, y, hallId?, boothId?` |
| Booths | `GET /app/booths` (D-199) | booth summary list — name/code/exhibitor/sector + `hallId?` |
| Booth detail | `GET /app/booths/{id}` (D-199) | adds `description` / `descriptionArabic` |

Both are **public, read-only** reads over existing tables. No schema/enum change
is introduced by this page.

## Rules

### L-1 — Two reads on open; lookup maps precomputed
On entry the app issues `GET /app/venue-map` and `GET /app/booths` together
(`Future.wait`). The node list drives the canvas; the booth list is the
**lookup** that fills the info card / detail sheet without a per-tap
round-trip. The screen is ready when **both** complete. On success two maps are
derived **once per load** (the lists are immutable afterwards): each node's
canvas position (`Map<String, Offset>`, L-4) and the booth summary by id
(`Map<String, BoothSummary>`).

### L-2 — Server returns the full set; pan/zoom is client-only
`/app/venue-map` returns **all active nodes** in one shot (no paging, no
viewport query). Panning and zooming are pure client transforms over the loaded
set — there is **no** map-tile or bbox endpoint, and none is planned for this
page. The zoom controls multiply the scale by **×1.3** per tap, clamped to
**0.3–4.0**, **re-anchored on the viewport centre** (the centre scene point is
preserved across the zoom). **أرشدني** sets the transform to centre the plane
on the selected node at **scale 1.5**; the locate control resets to the
identity transform.

### L-3 — Node kind drives the marker; locale drives the label
`kind` is the frozen `VenueMapNodeKind` enum:

| Value | Int | Meaning |
|-------|-----|---------|
| `Hall` | 0 | A hall footprint |
| `Zone` | 1 | A grouping / area |
| `Booth` | 2 | An exhibition booth (links a `boothId`) |
| `PointOfInterest` | 3 | POI (entrance, info, prayer room, etc.) |

The marker style is chosen per `kind`. The client decode is **tolerant**: the
wire value is an int today, but a string name also resolves, and an unknown
value falls back to `PointOfInterest` (a generic marker) rather than throwing
(D-219 wire-tolerance). The shown text is `labelArabic` when the app locale is
Arabic, else `label` (each falls back to the other when empty).

### L-4 — Coordinate space
`x` / `y` are `double`s in the map's own design space (set by the CP when the
node was placed, D-230). The client does **not** assume a fixed range — it
normalises each node against the **min/max bounds of the loaded set** and maps
the result onto a fixed **1000×1000 canvas with an 80px padding** inset,
computed once per load. A degenerate extent (all nodes share an X or Y) centres
that axis at 0.5.

### L-5 — Selection → info card; details sheet is lazy
**Every** node tap (any kind) sets the selection and shows the bottom **info
card**: the title is the matching `BoothSummary` name (found by `boothId` in
the precomputed lookup) or, when there is none, the node label; the
exhibitor · sector line and the code chip come from the summary. The card's
**عرض التفاصيل** action (booth nodes only) opens the **detail sheet**: the
cached summary renders immediately, and the **description paragraph** comes
from a lazy `GET /app/booths/{id}` fired as the sheet opens. If the booth list
and the node's `boothId` disagree (booth deactivated after the node loaded),
the card/sheet fall back to the node label and no detail call is made — see L-8.

### L-6 — D11: logo + hall-name are decoration, not data
The shipped booth DTOs (`PublicBoothSummary` / `PublicBoothDetail`) carry **no
logo URL** and only a `HallId` (a bare `Guid`), **not** a hall name. Therefore:
- A booth **logo image** in the card/sheet is **decoration** — there is no
  contract field. Do not render a placeholder as if it were real exhibitor
  branding.
- A booth **hall name** is **decoration** — only the `hallId` ships. Resolving
  it to a display name would need a hall lookup that this page does not call.

Both stay decoration **unless the contract is confirmed/extended** (would be a
new field → owner approval per the freeze rules). Flagged in
[README.md](README.md) and Function.

### L-7 — Hall-node deep link — **TO BUILD**
A Hall node carries `hallId`. Deep-linking from a Hall node into the
programme/agenda filtered by that hall is **not built on this page** — it depends
on the agenda screen accepting a hall filter. Until then a Hall-node tap shows
the info card with the label and **أرشدني** only. No new endpoint is required
for the map itself.

### L-8 — Empty / stale / missing handling
| Case | Behaviour |
|------|-----------|
| Empty node list | Show the empty state (Function #8), not a blank canvas |
| Booth node with `boothId` not in the loaded booth list | Info card / sheet use the node label; no detail call (L-5) |
| `GET /app/booths/{id}` → 404 (`BOOTH_NOT_FOUND`) or transport failure | The sheet keeps name/code/exhibitor/sector from the summary; the description is simply omitted (the lazy call swallows `ApiFailure` to null) |
| A node with null `x`/`y` | The decoder coerces null to **0** — the node renders at the min-bound edge (no node is skipped; CP should not emit null coordinates) |

## State transitions (client)
```
            open /map
                │
                ▼
          ┌──────────┐  both reads in flight (Future.wait)
          │ Loading  │
          └────┬─────┘
       success │           any read fails (ApiFailure)
        ┌──────┴───────┐        │
        ▼              ▼        ▼
   nodes > 0      nodes == 0   ┌────────┐
   ┌────────┐     ┌────────┐   │ Error  │──retry──▶ Loading
   │  Map   │     │ Empty  │   └────────┘
   └───┬────┘     └────────┘
       │ tap ANY node (selection + gold ring)
       ▼
   ┌──────────────┐  أرشدني → centre on node (scale 1.5)
   │  Info card   │  ✕ (non-booth) / tap another node → reselect
   └───┬──────────┘
       │ عرض التفاصيل (booth only; lazy GET /app/booths/{id})
       ▼
   ┌──────────────┐
   │ Detail sheet │──dismiss──▶ Map (selection kept)
   └──────────────┘
```

## Validation
No user input on this page → no client-side form validation. The only server
errors are transport / 404-on-detail (L-8) and the standard envelope errors
([Page_015_API.md](Page_015_API.md)).

## Error / empty / RTL
- **Error:** any read failure → `KsaErrorState` with retry that re-runs L-1.
- **Empty:** zero nodes → `KsaEmptyState`.
- **RTL:** when the locale is Arabic the chrome (info card, buttons, detail
  sheet) mirrors, and the floating map controls sit at the **directional end**
  (left in RTL — a recorded deviation from the frame's static right-side mock,
  D-378 close-out); the **map canvas geometry is NOT mirrored** — the canvas is
  wrapped in a forced-LTR `Directionality` because node `(x, y)` are physical
  venue positions. Only labels/text inside markers, the card and the sheet
  follow the locale; the booth code renders forced-LTR.

## Dependencies
- **Built:** `GET /app/venue-map` (D-230), `GET /app/booths` + `/{id}` (D-199);
  the D-378 KSA screen consumes them unchanged.
- **Not built (out of this page's scope):** Hall-node → agenda deep link (L-7);
  any booth logo / hall-name field (L-6, D11) — both require contract or screen
  work elsewhere and **owner approval** for new fields.
