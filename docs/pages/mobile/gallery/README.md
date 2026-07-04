# Media gallery — معرض الصور والفيديوهات (Page 030, `#30`)

- **Route:** `/media` (`RouteNames.gallery`). Access: **Guest+ (public)** — `GET /app/media` is anonymous (D-199).
- **Figma:** **947:3764** (المركز الاعلامي — the media-coverage hub). **Clean-code freeze:** D-626 (2026-07-04).

## Purpose

The media-coverage hub's **gallery** tab. A three-tab selector (الأخبار ·
الشركاء الإعلاميون · معرض الصور والفيديوهات — the app models each tab as its own
route) sits over the active tab's content; this screen owns the gallery tab. It
splits the media cache into two labelled sections — **الصور** (image tiles) and
**الفيديوهات** (video tiles with a centred play glyph) — each a two-up grid of
rounded tiles with a navy bottom-gradient. Tiles with an uploaded bitmap render
it from the public `…/app/media/{id}/(thumbnail|image)` route (thumbnail
preferred, image fallback) with a spinner and a graceful kind-icon fallback when
there is no bitmap or the fetch fails. Video *playback* (opening the external
`VideoUrl`) is still deferred.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `gallery_screen.dart` (105) | `GalleryScreen` (`ConsumerWidget`) — reads `mediaItemsProvider` + the base URL, `onRefresh`, and the loading / error / empty / data dispatch inside `SimfPageShell`. Re-exports `data/` so existing `MediaItem`/`MediaKind`/`mediaItemsProvider` imports (the gallery + archive tests) keep resolving. |
| `data/media_models.dart` | `MediaKind` (int wire Image=0/Video=1), `MediaItem` (+ `localizedTitle`/`localizedAlbum`, `fromJson` presence flags), the `_pick` bilingual helper. |
| `data/media_repository.dart` | `mediaItemsProvider` (`GET /app/media`, public, autoDispose). |
| `widgets/coverage_tabs.dart` (`CoverageTabs` + `_CoverageTab`) | The three-tab selector (gold-active gallery, the other two navigate to news / media-partners routes). |
| `widgets/gallery_body.dart` (`GalleryBody`) | The scrollable الصور-then-الفيديوهات sections (each rendered only when it has items), composing `SimfSectionHeader` + `MediaGrid`. |
| `widgets/media_grid.dart` (`MediaGrid` + `_MediaTile`/`_PlayGlyph`/`_Thumbnail`/`_PlaceholderBox`) | The two-up tile grid + the tile (bitmap + navy gradient + optional play glyph + overlaid title), the play circle, the network thumbnail (spinner + error fallback), and the no-bitmap kind-icon box. |

The data layer moved to `data/` per the D-545 rule; the tile's single-use leaves
are colocated with `MediaGrid` (booths/venue_map precedent). Every file ≤400 lines
(largest 194).

## DRY + tokenisation (this freeze)

- The local `_SectionLabel` (bare `Align(centerStart, Text white/textLg/w500)`) →
  the shared **`SimfSectionHeader`** (title-only) — the same consolidation proven
  pixel-identical in archive (D-617) / sponsors (D-620); the golden held here too.
- `_gradientNavy = Color(0xCC001030)` → the existing **`SimfTokens.bannerScrim`**
  (byte-identical); `_playCircleNavy = Color(0xB301132D)` → new
  **`SimfTokens.navyFill70`** (navy 70%). Render-preserving.

## L4 Figma parity (frame 947:3764)

Captured `gallery_947-3764.png` (@375×900, ar) as the **baseline before** the
refactor, then **held it WITHOUT `--update`** after — proving the decomposition +
the `SimfSectionHeader` swap + the token swaps are byte-identical. The golden was
read: header المركز الاعلامي, the three tabs (gold-active معرض الصور والفيديوهات
right-most), الصور + الفيديوهات sections with tiles (the video tile's play glyph),
bottom nav, RTL — all correct, no tofu.

## Level-F

Wired: the two sibling tabs navigate (news / media-partners); pull-to-refresh +
retry re-fetch; tiles render the bitmap (thumbnail→image) or the kind-icon
fallback. Reads `GET /app/media` (public). **Deferred (not a regression):** video
*playback* — video tiles show a play glyph but opening the external `VideoUrl` is
still deferred (SIMF-VID-001).

## Tests

`test/golden/gallery_golden_test.dart` (frame 947:3764, @375×900, ar) +
`test/features/gallery/gallery_screen_test.dart` (9 — header/tabs/sections,
images-only, both tab navigations, empty, error+retry, RTL, the two `fromJson`
model tests). E2E: `docs/tests/e2e/mobile-gallery.md`.

## Related decisions

- **D-626** (this clean-code freeze — data-layer extraction + widgets + `SimfSectionHeader` + tokens + first golden).
- **D-199** (public media endpoint), **D-309** (screen built), **D-342** (tile bitmaps), **D-545** (data-layer rule).
