# Page 023 — الرعاة · Sponsors

Per-page documentation folder (App screen 23).

## Identity
| | |
|---|---|
| Route | `RouteNames.sponsors` → `/sponsors` (**guest+, anonymous**) |
| Titles | AR **الرعاة** · EN **Sponsors** |
| Section | 3 — Content & activities |
| Nature | **Tier-grouped sponsor list** — a section per tier, sponsor cards |
| Status | API **BUILT** (reuse — `GET /app/sponsors`, D-199); **Flutter screen BUILT (D-305)** |

## API
`GET /api/v1/app/sponsors` (`AllowAnonymous`) → `ApiResult<PublicSponsors>` =
`{ groups: [{ tier, tierName, sponsors: PublicSponsor[] }] }`. `PublicSponsor`:
`id`, `nameEn`/`nameAr`, `tierName`, `logoRelativePath`, `url`, `displayOrder` +
the optional contact cluster (`email`/`phonePrimary`/social — D-287).

## Behaviour
A section header per tier (`tierName`) with the sponsor cards (name + interim
logo icon + the website URL line). Loading / empty / error+retry. Interim UI —
logo as icon, social links not rendered (the asset/url-launch pass is SIMF-VID-001).

## Tests
Widget `src/Mobile/simf_app/test/features/sponsors/sponsors_screen_test.dart`
(tier headers + cards, empty, error). API `tests/SIMF.Api.Tests/SponsorsTests.cs`.
E2E: [`docs/tests/e2e/mobile-sponsors.md`](../../tests/e2e/mobile-sponsors.md).
