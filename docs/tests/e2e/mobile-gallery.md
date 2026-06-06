# E2E test catalogue — `Media gallery` (`gallery`)

> **Authority:** SIMF E2E template (D-133). Media read built + anonymous (API
> `tests/SIMF.Api.Tests/MediaTests.cs`). **Flutter screen built (D-309)** — widget
> tests in `src/Mobile/simf_app/test/features/gallery/gallery_screen_test.dart`.
> Interim — the tiles show the kind icon + caption; binary image/video rendering
> is deferred to the asset/media pass.

| | |
|--|--|
| **Page** | [`Page_030`](../../App/Page_030/README.md) |
| **Route** | `GET /api/v1/app/media` · app screen #30 `/media` |
| **Auth setup** | **None** — `AllowAnonymous`. |
| **Last reviewed** | 2026-06-05 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB030-001 | Guest loads the media grid (image / video tiles + captions) | happy | P0 | authored ✓ (screen `renders the media tiles`) |
| E2E-MOB030-002 | A video item shows the play icon | happy | P1 | authored ✓ (screen — play icon) |
| E2E-MOB030-003 | `kind` decodes tolerantly (int or name) | contract | P0 | authored ✓ (`MediaKind.fromJson`) |
| E2E-MOB030-004 | Empty → empty state | edge | P1 | authored ✓ (screen `empty shows the empty state`) |

## Scenarios

```gherkin
Scenario: Media tiles render without a token
  When the app calls GET /api/v1/app/media
  Then it returns 200 with items[] (kind, title, album)
  And the screen shows a 2-column grid; video items carry a play icon

Scenario: kind decodes whether int or name
  Given MediaKind serialises as an int (Image=0, Video=1)
  Then the client resolves int or name, defaulting unknown → image

Scenario: Empty → placeholder
  Given no media
  Then the screen shows "No media yet"
```

**Evidence:** `gallery_screen_test.dart` (3) + `MediaTests` (API).

---

_Last reviewed:_ `2026-06-05` by `SIMF Team`.
