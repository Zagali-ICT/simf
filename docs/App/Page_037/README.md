# Page 037 — عن الملتقى · About the forum

Per-page documentation folder (App screen 37).

## Identity
| | |
|---|---|
| Route | `RouteNames.aboutForum` → `/about` (**guest+, anonymous**) |
| Titles | AR **عن الملتقى** · EN **About the forum** |
| Section | 7 — Smart features / info |
| Nature | **Static CMS content** (the forum's about / themes) |
| Status | API **BUILT** (`GET /app/content/{key}`, D-173); **Flutter screen BUILT (D-311)** |

## API
`GET /api/v1/app/content/{key}` (`AllowAnonymous`) → `PublicContentBlock`
`{ key, content, contentArabic, lastUpdatedAt }`. This screen reads key **`about`**.
404 when the key is not seeded yet.

## Behaviour
Reuses the shipped content layer (`ContentRepository`, from Page 9 terms) — renders
the localized body as **selectable text**. A 404 (key not seeded) shows the
"content coming soon" empty state; a server error shows error+retry. Rich
HTML/markdown rendering deferred to the design pass (interim plain text).

## Tests
Widget `src/Mobile/simf_app/test/features/about/about_screen_test.dart`
(body, 404→empty, error→retry). API `tests/SIMF.Api.Tests/ContentBlocksTests.cs`.
E2E: [`mobile-about.md`](../../tests/e2e/mobile-about.md).
