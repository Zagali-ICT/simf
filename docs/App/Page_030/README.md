# Page 030 — معرض الصور والفيديوهات · Media gallery

Per-page documentation folder (App screen 30).

## Identity
| | |
|---|---|
| Route | `RouteNames.gallery` → `/media` (**guest+, anonymous**) |
| Titles | AR **معرض الصور والفيديوهات** · EN **Media gallery** |
| Section | 5 — Media coverage |
| Nature | **Grid of media tiles** (image / video) |
| Status | API **BUILT** (`GET /app/media`); **Flutter screen BUILT (D-309)** |

## API
`GET /api/v1/app/media` (`AllowAnonymous`) → `PublicMediaPage { items, total, skip, top }`.
`PublicMediaItem`: `id`, `kind` (int — Image=0/Video=1), `title`/`titleArabic`,
`album`/`albumArabic`, `imageUrl`, `thumbnailUrl`, `videoUrl`, `displayOrder`.
The binary `…/media/{id}/image` & `…/thumbnail` endpoints exist for the actual
bytes.

## Behaviour
A 2-column grid of media tiles: a kind icon (image / video play) + the title +
album caption. Loading / empty / error+retry. **Interim** — the actual
image/thumbnail rendering (binary endpoints) and video playback are deferred to
the asset/media pass; tiles show the kind + caption. `kind` decodes tolerantly
(int or name).

## Tests
Widget `src/Mobile/simf_app/test/features/gallery/gallery_screen_test.dart`
(tiles, video icon, empty, kind decode). API `tests/SIMF.Api.Tests/MediaTests.cs`.
E2E: [`mobile-gallery.md`](../../tests/e2e/mobile-gallery.md).
