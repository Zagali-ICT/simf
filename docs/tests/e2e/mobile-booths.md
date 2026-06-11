# E2E test catalogue — `Booths` (`booths`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> booth reads are already built + anonymous (D-199 / D-230); API tests in
> `tests/SIMF.Api.Tests/PublicBoothsTests.cs`. The **Flutter screen is built
> (D-304)** and widget-tested in
> `src/Mobile/simf_app/test/features/booths/booths_screen_test.dart` (list,
> tap→detail sheet, empty, error→retry). It reuses the venue-map booth models +
> `VenueMapRepository` (same wire contract, no duplicate model).

| | |
|--|--|
| **Page** | [`Page_022`](../../App/Page_022/README.md) |
| **Route** | `GET /api/v1/app/booths` · `/app/booths/{id}` · app screen #22 `/booths` |
| **Surface** | Mobile (Flutter) + App API |
| **Auth setup** | **None** — both reads are `AllowAnonymous` (a guest sees the booths). |
| **Last reviewed** | 2026-06-05 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB022-001 | Guest loads the booths list (name · exhibitor · sector · code) | happy | P0 | authored ✓ (screen `lists the booths`) |
| E2E-MOB022-002 | Tapping a booth opens the sheet + lazy description (`/booths/{id}`) | happy | P0 | authored ✓ (screen `tapping a booth opens the detail sheet`) |
| E2E-MOB022-003 | Empty list → empty state | edge | P1 | authored ✓ (screen `empty list shows the empty state`) |
| E2E-MOB022-004 | A read failure → error + Retry that re-fetches | resilience | P0 | authored ✓ (screen `error shows retry, which re-fetches`) |
| E2E-MOB022-005 | Booth detail 404 → keep the summary, drop the description | edge | P2 | covered (sheet `localizedDescription` null → omitted; mirrors venue-map L-8) |

## Scenarios

### E2E-MOB022-001 — Guest loads the booths

```gherkin
Feature: Booths (exhibition)
  As a guest (signed out)
  I want the list of exhibitor booths
  So that I can find an exhibitor

Scenario: The booths render without a token
  When the app calls GET /api/v1/app/booths
  Then it returns 200 with the active booths
  And each card shows the name, exhibitor, sector and the booth code
```

**Evidence:** screen test `lists the booths`; API `PublicBoothsTests`.

### E2E-MOB022-002 — Booth detail sheet

```gherkin
Scenario: Tapping a booth loads its description
  When the visitor taps a booth card
  Then a bottom sheet shows the name + exhibitor/sector
  And GET /api/v1/app/booths/{id} fills the description
```

**Evidence:** screen test `tapping a booth opens the detail sheet`.

### E2E-MOB022-003 — Empty / E2E-MOB022-004 — Error+retry / E2E-MOB022-005 — Detail 404

```gherkin
Scenario: No booths shows the empty state
  Given GET /api/v1/app/booths returns an empty list
  Then the screen shows the "No booths" placeholder

Scenario: A failed read offers a retry
  Given the booths read fails
  Then an error + Retry are shown, and Retry re-runs the read

Scenario: A booth detail 404 keeps the summary
  Given the detail call 404s
  Then the sheet keeps name/exhibitor/sector and shows no description
```

**Evidence:** screen tests `empty list shows the empty state`,
`error shows retry, which re-fetches`; the 404-keeps-summary path mirrors the
venue-map booth sheet (D-298).

---

_Last reviewed:_ `2026-06-05` by `SIMF Team`.
