# Page 022 — الأجنحة · Booths

Per-page documentation folder (App screen 22).

## Identity
| | |
|---|---|
| Mockup page | **22** (`Mockup.html`) |
| Route | `RouteNames.booths` → `/booths` (**guest+, anonymous**) |
| Titles | AR **الأجنحة** · EN **Booths** |
| Section | 3 — Content & activities |
| Nature | **List of exhibitor booths** — name, exhibitor, sector, code; tap → description sheet |
| App privilege | **Guest+ (anonymous).** The booth reads are `AllowAnonymous` (D-199 / D-230). |
| Status | API **BUILT** (reuse — `GET /app/booths` + `/{id}`, D-199/D-230); **Flutter screen BUILT (D-304)** |

## API (authoritative contract)
Reuses the shipped public booth reads (no new API):
- `GET /api/v1/app/booths` → `ApiResult<List<PublicBoothSummary>>` — `id`, `code`,
  `name`/`nameArabic`, `exhibitorName`/`exhibitorNameArabic`, `sector`/`sectorArabic`, `hallId`.
- `GET /api/v1/app/booths/{id}` → `PublicBoothDetail` (the summary + `description`/`descriptionArabic`).

Both `AllowAnonymous`. The Flutter layer reuses the venue-map booth models +
`VenueMapRepository.getBooths()`/`getBoothDetail()` (shipped D-298) — the same
wire contract; no duplicate model.

## Behaviour
List of booth cards (name · exhibitor · sector + a code chip); tapping a card
opens a bottom sheet that lazily loads the booth's description
(`GET /app/booths/{id}`; a 404/transport failure keeps the summary, drops the
description). Loading / empty / error+retry states. UI is interim (final visuals
from SIMF-VID-001).

## Tests
- Widget: `src/Mobile/simf_app/test/features/booths/booths_screen_test.dart`
  (list, tap→detail sheet, empty, error→retry).
- API: `tests/SIMF.Api.Tests/PublicBoothsTests.cs` (the shipped reads).
- E2E: [`docs/tests/e2e/mobile-booths.md`](../../tests/e2e/mobile-booths.md).
