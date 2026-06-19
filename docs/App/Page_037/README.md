# Page 037 — عن الملتقى · About the forum

Per-page documentation folder (App screen 37).

## Identity
| | |
|---|---|
| Route | `RouteNames.aboutForum` → `/about` (**guest+, anonymous**) |
| Titles | AR **عن الملتقى** · EN **About the forum** |
| Section | 7 — Smart features / info |
| Nature | **Forum framing** — mission / vision / details / main themes (vision CMS-hydrated) |
| Status | API **BUILT** (`GET /app/content/{key}`, D-173); **Flutter screen BUILT (D-311); restructured Figma `1116:16448` (D-465)** |

## API
`GET /api/v1/app/content/{key}` (`AllowAnonymous`) → `PublicContentBlock`
`{ key, content, contentArabic, lastUpdatedAt }`. This screen reads key **`about`**
to hydrate the **الرؤية (vision)** paragraph; 404/empty → the static fallback.

## Behaviour
On the navy `KsaPage` shell (Figma `1116:16448`): an anchor-mark header
(`الملتقى الدولي البحري`), the **الرسالة (mission)** card (static line), the
**الرؤية (vision)** card (CMS `about` body, falling back to static copy on
404/500 — the page is always content-complete, no error screen), the **تفاصيل
الملتقى** card (السنة / الزمن / المكان — values mirror the mock; real event date is
an OI), and the **المحاور الرئيسية** card listing the four fixed numbered themes.

## Tests
Widget `src/Mobile/simf_app/test/features/about/about_screen_test.dart`
(mission/vision/themes render; 404→static fallback; 500→static; RTL theme number
sits inline-start of its title). API `tests/SIMF.Api.Tests/ContentBlocksTests.cs`.
E2E: [`mobile-about.md`](../../tests/e2e/mobile-about.md).
