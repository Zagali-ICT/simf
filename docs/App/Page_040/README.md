# Page 040 — تقييم · Rate

Per-page documentation folder (App screen 40).

## Identity
| | |
|---|---|
| Route | `RouteNames.rate` → `/rate` (**Visitor login-only — auth-gated**) |
| Titles | AR **تقييم** · EN **Rate** |
| Section | 8 — Settings & legal |
| Nature | **Star rating + comment** feedback form |
| Status | API **BUILT** (`POST /app/feedback/rate`); **Flutter screen BUILT (D-310)** |

## API
`POST /api/v1/app/feedback/rate` (`RequireApprovedAccount`) — body
`{ stars: 1..5, comment?: <=2000 }` → `RatingView` (upsert; the caller's rating).
The route is auth-gated (added to the router's authenticated set); a guest is
redirected to sign-in.

## Behaviour
A 1–5 star selector + an optional comment → submit. Stars are required (a 0-star
submit prompts "pick a star rating"); a success shows a thank-you toast; a wire
failure shows the error toast. Approved-only.

## Tests
Widget `src/Mobile/simf_app/test/features/feedback/rate_screen_test.dart`
(no-stars prompt, pick+submit→sent, failure→toast). API
`tests/SIMF.Api.Tests/FeedbackTests.cs`. E2E:
[`mobile-rate.md`](../../tests/e2e/mobile-rate.md).
