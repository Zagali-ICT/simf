# Page 031 — الشركاء الإعلاميون · Media partners

Per-page documentation folder (App screen 31).

## Identity
| | |
|---|---|
| Route | `RouteNames.mediaPartners` → `/media-partners` (**guest+, anonymous**) |
| Titles | AR **الشركاء الإعلاميون** · EN **Media partners** |
| Section | 5 — Media coverage |
| Nature | **Flat media-partner list** — name + website |
| Status | API **BUILT** (reuse — `GET /app/media-partners`, D-199); **Flutter screen BUILT (D-306)** |

## API
`GET /api/v1/app/media-partners` (`AllowAnonymous`) → `ApiResult<PublicMediaPartners>`
= `{ items: PublicMediaPartnerItem[] }`. Item: `id`, `name`/`nameArabic`,
`logoRelativePath`, `url`, `displayOrder` + the optional contact cluster (D-287).

## Behaviour
A list of partner cards (name + the website URL line). Loading / empty /
error+retry. Interim UI — logo as icon (asset pass is SIMF-VID-001).

## Tests
Widget `src/Mobile/simf_app/test/features/media_partners/media_partners_screen_test.dart`
(list, empty, error). API `tests/SIMF.Api.Tests/MediaPartnersTests.cs`.
E2E: [`docs/tests/e2e/mobile-media-partners.md`](../../tests/e2e/mobile-media-partners.md).
