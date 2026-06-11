# E2E test catalogue — `Media gallery` (`gallery`)

> **Authority:** SIMF E2E template (D-133). Media read built + anonymous (API
> `tests/SIMF.Api.Tests/MediaTests.cs`). **Flutter screen built (D-309)** — widget
> tests in `src/Mobile/simf_app/test/features/gallery/gallery_screen_test.dart`.
> **Tile bitmaps now rendered (D-342)** — each tile fetches its image from the
> public binary route with a spinner + a graceful icon fall-back; video
> *playback* (opening the external `videoUrl`) is still deferred.

| | |
|--|--|
| **Page** | [`Page_030`](../../App/Page_030/README.md) |
| **Route** | `GET /api/v1/app/media` · `GET /api/v1/app/media/{id}/(image\|thumbnail)` · app screen #30 `/media` |
| **Auth setup** | **None** — `AllowAnonymous`. |
| **Last reviewed** | 2026-06-08 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB030-001 | Guest loads the media grid (tiles + captions) | happy | P0 | authored ✓ (screen `renders the media tiles`) |
| E2E-MOB030-002 | A tile with a bitmap renders the thumbnail (image fallback) | happy | P0 | authored ✓ (presence decode; URL `{base}/app/media/{id}/thumbnail`) |
| E2E-MOB030-003 | A tile with no bitmap / a failed fetch falls back to the kind icon | edge | P0 | authored ✓ (screen — placeholder icon; live 404 for a no-bytes item) |
| E2E-MOB030-004 | A video item overlays the play glyph | happy | P1 | authored ✓ (screen — play icon) |
| E2E-MOB030-005 | `kind` decodes tolerantly (int or name) | contract | P0 | authored ✓ (`MediaKind.fromJson`) |
| E2E-MOB030-006 | `imageUrl`/`thumbnailUrl` decode to presence flags | contract | P0 | authored ✓ (`MediaItem.fromJson`) |
| E2E-MOB030-007 | Empty → empty state | edge | P1 | authored ✓ (screen `empty shows the empty state`) |

## Scenarios

```gherkin
Scenario: Media tiles render without a token
  When the app calls GET /api/v1/app/media
  Then it returns 200 with items[] (kind, title, album, imageUrl, thumbnailUrl)
  And the screen shows a 2-column grid

Scenario: A tile with an uploaded bitmap shows it
  Given an item whose imageUrl/thumbnailUrl is non-null
  Then the tile requests {baseUrl}/app/media/{id}/thumbnail (image as fallback)
  And shows a spinner while it loads, then the bitmap

Scenario: A tile with no bitmap falls back to the kind icon
  Given an item whose imageUrl and thumbnailUrl are null (or the fetch 404s)
  Then the tile shows the kind icon (image / video play) on a navy box
  And makes no needless image request

Scenario: kind decodes whether int or name
  Given MediaKind serialises as an int (Image=0, Video=1)
  Then the client resolves int or name, defaulting unknown → image

Scenario: Empty → placeholder
  Given no media
  Then the screen shows "No media yet"
```

**Evidence:** `gallery_screen_test.dart` (5: tiles, empty, error, `MediaKind.fromJson`,
`MediaItem.fromJson` presence) + `MediaTests` (API). Live contract checked
2026-06-08 against the running API (`/app/media` 200; a no-bytes item's
`/image` → 404, the documented fall-back path).

---

_Last reviewed:_ `2026-06-08` by `SIMF Team`.
