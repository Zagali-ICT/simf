# News — الأخبار / التغطية الإعلامية (Page 029, `#29`)

- **Route:** `/news` (`RouteNames.news`). Access: **Guest+ (public)** — `GET /app/news` is anonymous (D-199).
- **Figma:** **1049:12629** (المركز الاعلامي — media coverage; card 957:2197). **Clean-code freeze:** D-629 (2026-07-04).

## Purpose

The "احدث المستجدات" tab of the two-tab media-coverage hub (احدث المستجدات ·
الشركاء الإعلاميون — the inactive pill replaces to the media-partners route). The
body is the news list — each row a horizontal navy card (thumbnail + gold date +
bold title, no excerpt) — and tapping a row pushes the article screen
(`GET /app/news/{id}`).

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `news_screen.dart` (129) | `NewsScreen` (`ConsumerWidget`) — reads `newsListProvider` + the base URL, `_refresh`, and the tab strip + loading/error/empty/list dispatch. Re-exports `newsListProvider` so the Home carousel + tests keep resolving it off this screen. |
| `data/news_repository.dart` | `newsListProvider` (`GET /app/news`, moved here from the screen — D-545) alongside the existing `NewsRepository`/`newsRepositoryProvider` (article detail). |
| `widgets/news_card.dart` (`NewsCard` + `_NewsThumbnail`/`_NewsImageFallback`/`_CategoryChip`) | One news row — the navy card, the 155×85 thumbnail (network image + navy gradient + gold category chip + article-icon fallback). |
| `app/widgets/media_coverage_tabs.dart` (`MediaCoverageTabs` + `MediaCoverageTab` enum) | **Shared** two-tab strip (partners · latest-updates), extracted from the byte-identical `_MediaTabs` this screen and media-partners both carried. |

## DRY + tokenisation (this freeze)

- **Cross-screen DRY:** the local `_MediaTabs`/`_MediaTab` was **byte-identical**
  to `media_partners_screen.dart:_MediaTabs` (the class doc said so) → extracted
  ONE shared **`MediaCoverageTabs(active:)`** into the app catalogue; News wired
  now. **Follow-up (D-630, media-partners pass):** swap media-partners' local copy
  for the shared widget + delete it.
- The local `_RefreshableCentered` was a **byte-identical copy of
  `SimfPullableHost`** (verified) → replaced on the error/empty states.
- `_gradientNavy = Color(0xCC001030)` → the existing **`SimfTokens.bannerScrim`**
  (byte-identical). The data provider moved to `data/`.

## L4 Figma parity (frame 1049:12629)

Captured `news_1049-12629.png` (@375×750, ar, two items) as the **baseline
before** the refactor, then **held it WITHOUT `--update`** after — proving the
provider move + the `MediaCoverageTabs`/`NewsCard` extraction + the token swap
byte-identical. Golden read: المركز الاعلامي header, the tab strip (احدث
المستجدات active-gold left / الشركاء الإعلاميون right), two news cards (category +
gold date + title right, thumbnail + gold chip left), RTL, no tofu.

## Level-F

Wired: the inactive tab replaces to media-partners; each row pushes the article
screen; pull-to-refresh + retry re-fetch. Reads `GET /app/news`. No missing API.

## Tests

`test/golden/news_golden_test.dart` (frame 1049:12629, @375×750, ar) +
`test/features/news/news_screen_test.dart`. E2E: `docs/tests/e2e/mobile-news.md`.

## Related decisions

- **D-629** (this clean-code freeze — provider move + shared `MediaCoverageTabs` + `NewsCard` + `SimfPullableHost`/token swaps + first golden).
- **D-308** (screen built), **D-199** (public news endpoint), **D-357** (public asset route for the thumbnail).
