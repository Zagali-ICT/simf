# E2E test catalogue — `Venue map` (`venueMap`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Mobile
> catalogue — the public reads are built (D-230 venue-map, D-199 booths); the
> booth API is covered by `tests/SIMF.Api.Tests/PublicBoothsTests.cs`. The
> **Flutter screen is built** (D-298) and was **rebuilt to KSA Wave-2 frame
> 215:562 "Location"** (D-378 batch) with the frame's Google geographic map
> **replaced by the venue 2D node plane** (owner directive): full-bleed
> pan/zoom plane, floating gold zoom-in/zoom-out/recentre controls, node tap →
> the **bottom white info card** (gold box, name, exhibitor·sector, code chip,
> gold **أرشدني** centring the map on the node + **عرض التفاصيل** opening the
> lazy-description sheet). The old legend strip gave way to the frame's info
> card. Widget-tested in
> `src/Mobile/simf_app/test/features/venuemap/venue_map_screen_test.dart`
> (markers + controls, booth card + actions, details sheet with lazy detail,
> detail-404 fallback, non-booth card + close, empty, error→retry, LTR
> canvas); the model parsers (kind tolerant-decode, real booth field names)
> are covered in
> `src/Mobile/simf_app/test/features/venuemap/venue_map_models_test.dart`.
> The old mockup screen + test are parked in `_legacy_mockup/`.

| | |
|--|--|
| **Page** | [`Page_015`](../../App/Page_015/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/venue-map` · `/app/booths` · `/app/booths/{id}` · app screen #15 `/map` |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | **None** — all three reads are `AllowAnonymous` (a Guest sees the full map). No token, no permission code. |
| **Last reviewed** | 2026-06-05 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB015-001 | Guest loads the map: a marker per active node, styled by `Kind` + a legend | happy | P0 | authored ✓ (screen — markers + legend) |
| E2E-MOB015-002 | Tapping a Booth node opens the popup: name + code + exhibitor/sector, then the lazy description | happy | P0 | authored ✓ (screen — popup + detail) |
| E2E-MOB015-003 | Booth detail 404 (`BOOTH_NOT_FOUND`) → popup keeps the summary, drops the description | edge | P1 | authored ✓ (screen — 404 fallback) |
| E2E-MOB015-004 | Empty node list → empty state (not a blank canvas) | edge | P1 | authored ✓ (screen — empty) |
| E2E-MOB015-005 | Any read fails → error state + Retry that re-runs both reads | resilience | P0 | authored ✓ (screen — error→retry) |
| E2E-MOB015-006 | Arabic: chrome mirrors (RTL) but the map canvas geometry stays LTR (venue orientation) | i18n | P1 | authored ✓ (screen — RTL chrome / LTR canvas) |
| E2E-MOB015-007 | `kind` decodes tolerantly (int or name; unknown → a generic marker) | resilience | P2 | authored ✓ (model — `VenueMapNodeKind.fromJson`) |
| E2E-MOB015-008 | Booth fields bind the real wire names (`name`/`nameArabic`/`exhibitorName`/`sector`) | contract | P0 | authored ✓ (model — `BoothSummary.fromJson`) |
| E2E-MOB015-009 | **Map controls have accessible names (BUG-012):** the three floating gold controls announce "Reset the map view" / "Zoom in" / "Zoom out" (bilingual) instead of three unnamed views, so a screen-reader user can zoom and recentre | a11y | P2 | authored ✓ (`VenueMapControl` takes a required `label` → `Semantics(button: true, label:)`; strings `venueMapResetView` / `venueMapZoomIn` / `venueMapZoomOut`) |
| E2E-MOB015-010 | The booth info card carries the exhibitor logo badge (FR-LGO-005) | happy | P1 | authored ✓ (widget test) |
| E2E-MOB015-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB015-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-MOB015-001 — Guest loads the map

```gherkin
Feature: 2D venue map
  As a guest (signed out)
  I want to see the venue laid out with its halls, zones, booths and POIs
  So that I can find my way around

Background:
  Given the venue map has at least one active node
  And no user is signed in

Scenario: The map renders every active node
  When the app calls GET /api/v1/app/venue-map and GET /api/v1/app/booths
  Then both return 200 with success = true (anonymous)
  And every node is drawn at its (x, y) with a marker styled by Kind
  And a legend shows the four kinds (Hall / Zone / Booth / Point of interest)
```

**Evidence:** screen test `renders a marker per node + the legend`.

### E2E-MOB015-002 — Booth popup + lazy detail

```gherkin
Scenario: Tapping a booth opens its popup and loads the description
  Given a Booth node whose boothId matches a loaded PublicBoothSummary
  When the visitor taps the Booth marker
  Then a bottom-sheet popup shows the booth name, code and exhibitor/sector
  And GET /api/v1/app/booths/{id} fills the description paragraph
```

**Evidence:** screen test `tapping a booth node opens the popup with name, code, detail`.

### E2E-MOB015-003 — Detail 404 fallback

```gherkin
Scenario: A booth that 404s on detail keeps its summary
  Given a Booth node whose detail call returns 404 BOOTH_NOT_FOUND
  When the visitor opens the popup
  Then the popup keeps the summary (name / code / sector)
  And no description is shown (Page_015 L-8)
```

**Evidence:** screen test `a detail 404 keeps the summary and drops the description`.

### E2E-MOB015-004 — Empty state

```gherkin
Scenario: No nodes shows the empty state
  Given GET /api/v1/app/venue-map returns an empty list
  When the map screen opens
  Then an empty-state message is shown, not a blank canvas
```

**Evidence:** screen test `an empty node list shows the empty state`.

### E2E-MOB015-005 — Error + retry

```gherkin
Scenario: A failed read offers a working retry
  Given GET /api/v1/app/venue-map fails (transport / 5xx)
  When the map screen opens
  Then an error message + Retry are shown
  And tapping Retry re-runs both reads
```

**Evidence:** screen test `a load failure shows the error + retry, which re-fetches`.

### E2E-MOB015-006 — RTL chrome, LTR canvas

```gherkin
Scenario: Arabic mirrors the chrome but not the map geometry
  Given the device locale is Arabic
  When the map renders
  Then the chrome (app bar, legend) lays out right-to-left
  And the map canvas (node positions) stays in venue orientation (LTR)
  And only the text inside markers / the popup follows the locale (Page_015 L-3)
```

**Evidence:** screen test `chrome mirrors in Arabic but the canvas geometry does not`.

### E2E-MOB015-007 — Tolerant `kind` decode

```gherkin
Scenario: An unknown node kind does not crash the map
  Given VenueMapNodeKind serialises as an int today
  When the client decodes a kind it does not recognise (or a string name)
  Then it resolves the known values and falls back to a generic marker for the rest
```

**Evidence:** model test `VenueMapNodeKind.fromJson` (int, string, unknown→pointOfInterest).

### E2E-MOB015-008 — Booth wire-contract field names

```gherkin
Scenario: The popup binds the real booth field names
  Given PublicBoothSummary ships name / nameArabic / exhibitorName / sector
  When the client decodes a booth
  Then it binds those camelCase names (NOT nameEn / nameAr)
```

> Reality note: an earlier draft of `Page_015_API.md` named the booth fields
> `nameEn/nameAr/...`; the shipped contract is `Name/NameArabic/ExhibitorName/Sector`
> → corrected with D-298 and bound correctly.

**Evidence:** model test `BoothSummary / BoothDetail.fromJson binds the real wire field names`.

### E2E-MOB015-010 — The info card carries the exhibitor logo badge (FR-LGO-005)

```gherkin
Scenario: A booth node's card shows its logo
  Given the guest taps a booth node whose booth id is "b1"
  Then the white info card opens with a 60x60 logo badge at its inline start
  And the badge loads {base}/app/assets/BoothLogo/b1/image
  And while it loads (or when the booth has no logo) it shows the booth
      short name on the navy tile
  And the dismiss control keeps its own place beside it

Scenario: A hall / zone node has no badge
  Given the guest taps a non-booth node
  Then no logo badge is rendered (there is no exhibitor to badge)
```

> Reality note: the badge was previously listed as an owner-accepted deviation
> ("close-X instead of the logo badge") because booths had no logo assets. They
> do now (BoothLogo, D-357 / D-764), so the frame's badge is rendered and the
> deviation is closed.

**Evidence:** `venue_map_screen_test` — "FR-LGO-005 — a booth card carries the
exhibitor logo badge from the BoothLogo asset route", "FR-LGO-005 — a non-booth
node has no logo badge".

---

_Last reviewed:_ `2026-07-27` by `SIMF Team` — added E2E-MOB015-010 for FR-LGO-005
(the exhibitor logo badge is rendered; the "no logo assets" deviation is closed).
_Prior:_ `2026-07-26` BUG-012: the three floating map
controls were bare icon views with no accessible name; each now carries a
bilingual semantics label (E2E-MOB015-009). `2026-06-05` by `SIMF Team`.
