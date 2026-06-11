# Page 035 — قابل أشخاص مثلك · Meet people

Per-page documentation folder (App screen 35).

## Identity
| | |
|---|---|
| Mockup page | **35** (`Mockup.html`) |
| Route | `RouteNames.meetPeople` → `/meet` (**auth-gated, approved account**) |
| Titles | AR **قابل أشخاص مثلك** · EN **Meet people** |
| Section | Networking |
| Nature | **Recommended profiles** — initials avatar, name, `jobTitle · profileType`, shared-interest chips + count |
| App privilege | **Approved account** (`RequireApprovedAccount`). Route 35 is already auth-gated. |
| Status | API **BUILT** (`GET /app/account/recommendations/meet-like-you`); **Flutter screen BUILT** |

## API (authoritative contract)
- `GET /api/v1/app/account/recommendations/meet-like-you` →
  `ApiResult<RecommendationsResponse>` where `RecommendationsResponse = { matches: RecommendationEntry[] }`.
- `RecommendationEntry`: `userProfileId`, `englishName`, `arabicName`,
  `jobTitle` (string?), `profileTypeName` / `profileTypeNameArabic` (string?),
  `sharedInterests: [{ id, name, nameArabic }]`, `sharedInterestCount` (int),
  `score` (double).
- `RequireApprovedAccount` — the read is authenticated; an unauthenticated /
  pending account is sent through the route-35 auth gate.

The Flutter layer decodes the envelope with `Recommendation.listFromData`
(`features/meet/data/meet_models.dart`) — no duplicate model.

## Behaviour
A card per match: an initials avatar, the localized name, a `jobTitle · profileType`
sub-line (each side omitted when blank), a `Wrap` of the shared-interest chips
(localized) and a "N shared interests" line. Loading / empty / error+retry states.
UI is interim (final visuals from SIMF-VID-001).

## Tests
- Models: `src/Mobile/simf_app/test/features/meet/meet_models_test.dart`
  (envelope decode, localized name/type fallback, tolerant defaults).
- Widget: `src/Mobile/simf_app/test/features/meet/meet_people_screen_test.dart`
  (list, empty, error — `FutureProvider` override).
- E2E: [`docs/tests/e2e/mobile-meet-people.md`](../../tests/e2e/mobile-meet-people.md).
