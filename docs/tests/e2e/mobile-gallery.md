# E2E test catalogue — `Media gallery` (`gallery`)

> **Authority:** SIMF E2E template (D-133). Media read built + anonymous (API
> `tests/SIMF.Api.Tests/MediaTests.cs`). **Flutter screen built (D-309)** — widget
> tests in `src/Mobile/simf_app/test/features/gallery/gallery_screen_test.dart`.
> **Tile bitmaps now rendered (D-342)** — each tile fetches its image from the
> public binary route with a spinner + a graceful icon fall-back; video
> *playback* (opening the external `videoUrl`) is still deferred.
>
> **Re-skinned to Figma frame `947:3764` (KSA-Project, D-30x).** The screen is now
> the **التغطية الإعلامية / Media coverage** hub: a three-pill selector
> (الأخبار/News · الشركاء الإعلاميون/Media partners · معرض الصور والفيديوهات/Media
> gallery — the gallery pill active, solid gold) over the gallery content, which is
> split into two labelled sections — **الصور/Images** and **الفيديوهات/Videos**
> (video tiles carry the centred play glyph). The other two pills navigate to the
> existing `/news` and `/media-partners` routes.

| | |
|--|--|
| **Page** | [`Page_030`](../../App/Page_030/README.md) |
| **Route** | `GET /api/v1/app/media` · `GET /api/v1/app/media/{id}/(image\|thumbnail)` · app screen #30 `/media` |
| **Auth setup** | **None** — `AllowAnonymous`. |
| **Last reviewed** | 2026-06-16 |

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
| E2E-MOB030-008 | Coverage hub header + 3-pill selector (gallery pill active) | happy | P0 | _to author_ (frame `947:3764`) |
| E2E-MOB030-009 | الصور / الفيديوهات sections split the cache by kind | happy | P0 | _to author_ |
| E2E-MOB030-010 | Only-one-kind: a section renders only when it has items | edge | P1 | _to author_ |
| E2E-MOB030-011 | الأخبار pill navigates to /news | nav | P0 | _to author_ |
| E2E-MOB030-012 | الشركاء الإعلاميون pill navigates to /media-partners | nav | P0 | _to author_ |
| E2E-MOB030-013 | Active gallery pill is inert (no navigation) | edge | P1 | _to author_ |
| E2E-MOB030-014 | RTL — pills + sections lay out right-to-left in Arabic | i18n | P1 | _to author_ |

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

### Re-skin scenarios — Media-coverage hub (Figma frame `947:3764`)

```gherkin
Scenario: E2E-MOB030-008 — Coverage hub header + 3-pill selector, gallery pill active
  Given the app opens screen #30 /media
  Then the KsaPage header reads "التغطية الإعلامية" (EN "Media coverage")
  And a row of three pills shows, in order:
    | الأخبار              | News           |
    | الشركاء الإعلاميون   | Media partners |
    | معرض الصور والفيديوهات | Media gallery |
  And the gallery pill is the active pill (solid gold, white label)
  And the other two pills are bordered navy cards

Scenario: E2E-MOB030-009 — الصور / الفيديوهات sections split the cache by kind
  Given GET /api/v1/app/media returns items of both kinds (Image=0 and Video=1)
  Then the body shows the "الصور" (EN "Images") section first
  And under it a two-up grid of the image tiles
  And then the "الفيديوهات" (EN "Videos") section
  And each video tile overlays the centred play glyph
  And each tile with an uploaded bitmap renders it, else the kind icon on a navy box

Scenario: E2E-MOB030-010 — only one kind present → only that section renders
  Given the media cache carries video items but no image items
  Then the "الفيديوهات" section renders with its grid
  And the "الصور" section is not shown
  And no empty-state placeholder appears

Scenario: E2E-MOB030-011 — الأخبار pill navigates to /news
  Given the media-coverage hub is open
  When the user taps the "الأخبار" (News) pill
  Then the app navigates to the RouteNames.news (/news) route

Scenario: E2E-MOB030-012 — الشركاء الإعلاميون pill navigates to /media-partners
  Given the media-coverage hub is open
  When the user taps the "الشركاء الإعلاميون" (Media partners) pill
  Then the app navigates to the RouteNames.mediaPartners (/media-partners) route

Scenario: E2E-MOB030-013 — active gallery pill is inert
  Given the media-coverage hub is open with the gallery pill active
  When the user taps the "معرض الصور والفيديوهات" (Media gallery) pill
  Then nothing navigates (onTap is null) and the gallery stays in view

Scenario: E2E-MOB030-014 — RTL layout in Arabic
  Given the app locale is Arabic (isArabic = true)
  Then the three pills and both section labels lay out right-to-left
  And each section label ("الصور" / "الفيديوهات") aligns to the inline start (right)
  And each tile's overlaid title aligns to the inline start
```

**Evidence:** `gallery_screen_test.dart` (5: tiles, empty, error, `MediaKind.fromJson`,
`MediaItem.fromJson` presence) + `MediaTests` (API). Live contract checked
2026-06-08 against the running API (`/app/media` 200; a no-bytes item's
`/image` → 404, the documented fall-back path).

---

_Last reviewed:_ `2026-06-16` by `SIMF Team`.
