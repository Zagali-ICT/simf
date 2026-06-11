# Page 024 — الأرشيف · Archive

Per-page documentation folder (App screen 24). The detail-endpoint spec lives in
the sibling [`Page_024-01`](../Page_024-01/README.md) (D-273).

## Identity
| | |
|---|---|
| Route | `RouteNames.archive` → `/archive` (**guest+, anonymous**) |
| Titles | AR **الأرشيف** · EN **Archive** |
| Section | 3 — Content & activities |
| Nature | **Past editions list** — year · title · stats; tap → detail sheet |
| Status | API **BUILT** (`GET /app/archive` + `/{id}`, D-273); **Flutter screen BUILT (D-307)** |

## API
- `GET /api/v1/app/archive` (`AllowAnonymous`) → `PublicArchive { items: PublicArchiveEdition[] }`
  — `id`, `year`, `titleEn`/`titleAr`, `summaryEn`/`summaryAr`, `attendees`,
  `sessions`, `speakers`, `coverImageRelativePath`.
- `GET /api/v1/app/archive/{id}` (`AllowAnonymous`) → `PublicArchiveEditionDetail`
  (adds `locationEn`/`Ar`, `dateLabelEn`/`Ar`).

## Behaviour
A list of edition cards (year-badge avatar · title · `attendees · sessions ·
speakers` stats); tapping an edition opens a bottom sheet that lazily loads the
fuller detail (date label · location · summary; a 404/failure keeps the list
summary). Loading / empty / error+retry. Cover images deferred to the asset pass.

## Tests
Widget `src/Mobile/simf_app/test/features/archive/archive_screen_test.dart`
(list+stats, empty, error, model decode). API
`tests/SIMF.Api.Tests/ArchiveTests.cs`. E2E:
[`mobile-archive.md`](../../tests/e2e/mobile-archive.md) (list) +
[`mobile-archive-detail.md`](../../tests/e2e/mobile-archive-detail.md) (detail).
