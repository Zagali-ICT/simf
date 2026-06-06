# Page 029 — الأخبار · News

Per-page documentation folder (App screen 29).

## Identity
| | |
|---|---|
| Route | `RouteNames.news` → `/news` (**guest+, anonymous**) |
| Titles | AR **الأخبار** · EN **News** |
| Section | 5 — Media coverage |
| Nature | **News list → article detail** |
| Status | API **BUILT** (`GET /app/news` + `/{id}`); **Flutter screen BUILT (D-308)** |

## API
- `GET /api/v1/app/news` (`AllowAnonymous`) → `PublicNewsPage { items, total, page, pageSize }`.
  `PublicNewsListItem`: `id`, `title`/`titleArabic`, `excerpt`/`excerptArabic`,
  `category`/`categoryArabic`, `imageRelativePath`, `publishedAt`.
- `GET /api/v1/app/news/{id}` (`AllowAnonymous`) → `PublicNewsArticle` (the list
  fields + `body`/`bodyArabic`). 404 when missing.

## Behaviour
A list of news cards (category chip · title · 2-line excerpt); tapping pushes the
article screen (`GET /app/news/{id}` → category · title · body). Loading / empty /
error+retry on both; article 404 → "not found". Hero images deferred to the asset
pass.

## Tests
Widget `src/Mobile/simf_app/test/features/news/news_screen_test.dart`
(list, empty, error, article decode). API `tests/SIMF.Api.Tests/NewsTests.cs`.
E2E: [`mobile-news.md`](../../tests/e2e/mobile-news.md).
