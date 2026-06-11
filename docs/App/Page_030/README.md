# Page 030 — معرض الصور والفيديوهات · Media gallery

Per-page documentation folder (App screen 30).

## Identity
| | |
|---|---|
| Route | `RouteNames.gallery` → `/media` (**guest+, anonymous**) |
| Titles | AR **معرض الصور والفيديوهات** · EN **Media gallery** |
| Section | 5 — Media coverage |
| Nature | **Grid of media tiles** (image / video) |
| Status | API **BUILT** (`GET /app/media`); **Flutter screen BUILT (D-309)**; **tile bitmaps rendered (D-342)** |

## API
`GET /api/v1/app/media` (`AllowAnonymous`) → `PublicMediaPage { items, total, skip, top }`.
`PublicMediaItem`: `id`, `kind` (int — Image=0/Video=1), `title`/`titleArabic`,
`album`/`albumArabic`, `imageUrl`, `thumbnailUrl`, `videoUrl`, `displayOrder`.
The binary `GET /api/v1/app/media/{id}/image` & `…/thumbnail` endpoints
(`AllowAnonymous`) stream the actual bytes (200), or 404 when the item carries
no stored bitmap. The app treats `imageUrl`/`thumbnailUrl` as **presence flags**
(they are server-relative and omit the `/app` segment) and builds the fetch URL
itself as `{baseUrl}/app/media/{id}/(thumbnail|image)`.

## Behaviour
A 2-column grid of media tiles: each tile shows its bitmap (thumbnail preferred,
full image fallback) fetched from the public binary route, with a spinner while
it loads and a graceful fall-back to the **kind icon** (image / video play) when
the item carries no bitmap or the fetch fails; a video tile overlays a play
glyph on its poster. Title + album caption below. Loading / empty / error+retry
on the list. Video *playback* (opening the external `videoUrl`) is still
deferred. `kind` decodes tolerantly (int or name).

## Tests
Widget + model `src/Mobile/simf_app/test/features/gallery/gallery_screen_test.dart`
(tiles, video icon, empty, error, `kind` decode, **`imageUrl`/`thumbnailUrl`
presence decode** — 5 tests). API `tests/SIMF.Api.Tests/MediaTests.cs`.
E2E: [`mobile-gallery.md`](../../tests/e2e/mobile-gallery.md).
