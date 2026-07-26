# E2E test catalogue — Logo / photo boxes + the full-size viewer (cross-surface, app)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Owner 2026-07-26 — *"Make the
> logo size fit to box size in all logo views, and on-press/click on logo must
> show in full size."* This is a **cross-surface** catalogue: the behaviour lives
> in two shared widgets, so it is specified once here and referenced from every
> page that renders a logo or a photo, instead of being copied per page.
> Runner-agnostic Gherkin.

| | |
|--|--|
| **Widgets** | [`SimfLogoImage`](../../../src/Mobile/simf_app/lib/app/widgets/simf_logo_image.dart) (the box) · [`SimfImageViewer`](../../../src/Mobile/simf_app/lib/app/widgets/simf_image_viewer.dart) (the full-size route) |
| **Surfaces** | Exhibitor detail · Sponsor detail · Sponsors list + grid · Media partners · Booths (booth card header) · Speaker profile · Entry badge |
| **APIs** | The anonymous asset routes `GET /api/v1/app/assets/{ExhibitorLogo\|CompanyLogo\|SponsorLogo\|BoothLogo\|SpeakerPhoto}/{id}/image`; the **bearer-gated** `GET /api/v1/app/account/avatar` for the user's own badge photo (fetched as authenticated Dio bytes per D-422 — never a bare `Image.network`) |
| **Auth setup** | Public for the entity logos; a signed-in token for the badge photo. Obtain via the standard app sign-in; never a literal secret. |
| **Last reviewed** | 2026-07-26 (created for the owner logo request) |

## The two rules

1. **FIT.** A brand mark is shown **whole** inside its box — `SimfLogoImage`
   defaults to `BoxFit.contain`, replacing the per-page `BoxFit.cover` that
   cropped wide logos. A *photographic* subject (a speaker portrait, the badge
   photo) still passes `BoxFit.cover` explicitly, because a portrait must fill
   its circle / rounded square rather than letterbox.
2. **TAP → FULL SIZE.** Tapping opens `SimfImageViewer` — a full-screen navy
   route painting the **same `ImageProvider`** the box painted (so it opens from
   the image cache, and a bearer-gated avatar opens from its already-fetched
   bytes), with pinch/drag zoom (`InteractiveViewer`, 1×–5×), an accessible
   name, and a gold close control. It is **off** where the box sits inside a
   tappable row/card — there the row's own tap owns the navigation (see
   E2E-LOGO-005).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-LOGO-001 | A logo box shows the WHOLE mark (contain), never a crop | happy | P0 | authored ✓ (widget) |
| E2E-LOGO-002 | A portrait box still fills its frame (cover) | happy | P0 | authored ✓ (widget) |
| E2E-LOGO-003 | Tapping a detail-surface logo opens the full-size viewer; pinch zooms; close / back dismisses | happy | P0 | authored ✓ (widget) |
| E2E-LOGO-004 | The viewer paints the SAME provider — full resolution, no second download, works for a bearer-gated avatar | happy | P1 | authored ✓ (widget) |
| E2E-LOGO-005 | A logo inside a tappable row does NOT steal the row's tap | edge | P0 | authored ✓ (widget + booths screen test) |
| E2E-LOGO-006 | No logo / 404 / offline → the initials (or short-name) fallback, no viewer | resilience | P0 | authored ✓ (widget) |
| E2E-LOGO-007 | Accessibility — the box and the viewer are both named with the entity name | a11y | P1 | authored ✓ (widget) |
| E2E-LOGO-008 | RTL — the viewer's close control sits at the inline end under Arabic | i18n | P1 | spec |

## Scenarios

### E2E-LOGO-001 — A logo FITS its box

```gherkin
Feature: Logo boxes across the app
Scenario Outline: A wide brand mark is shown whole, not cropped
  Given <surface> renders <entity>'s uploaded logo in its box
  Then the whole mark is visible inside the box (BoxFit.contain), letterboxed if needed
  And no edge of the mark is cut off

  Examples:
    | surface                     | entity        |
    | Exhibitor detail (108×108)  | an exhibitor  |
    | Sponsor detail (108×108)    | a sponsor     |
    | Sponsors list / grid        | a sponsor     |
    | Media partners grid (48×48) | a partner     |
    | Booth card header (48×48)   | a booth       |
```

### E2E-LOGO-002 — A portrait still fills its frame

```gherkin
Scenario: A person photo covers its circle
  Given the speaker profile renders a speaker's uploaded photo in the 125px gold-ringed circle
  Then the photo FILLS the circle (BoxFit.cover) with no letterbox bars
  And the same holds for the 64px badge photo on the entry badge
# Fit is per context: contain for a mark, cover for a face.
```

### E2E-LOGO-003 — Tap opens the picture full size

```gherkin
Scenario: Pressing a logo shows it full size
  Given a user is on the sponsor detail page and the sponsor has a logo
  When they tap the logo
  Then a full-screen viewer opens over the page
  And the logo is shown at its full resolution, fitted to the screen
  When they pinch out
  Then the picture zooms (up to 5x) and can be dragged
  When they tap the close control (or press system back)
  Then the viewer dismisses and the sponsor detail is unchanged underneath
```

### E2E-LOGO-004 — The viewer reuses the fetched image

```gherkin
Scenario: No second download, and bearer-gated photos still open
  Given a logo box has already painted its picture
  When the viewer is opened from it
  Then it paints the SAME ImageProvider (the cached full-resolution image)
  And no additional network request is issued for the already-cached asset
  Given the user's own badge photo, which is fetched as authenticated bytes (D-422)
  When they tap it on the entry badge
  Then the viewer opens from those bytes (a MemoryImage), never a bare Image.network
```

### E2E-LOGO-005 — A logo inside a tappable row keeps the row's tap

```gherkin
Scenario Outline: The row's navigation wins over the viewer
  Given <surface> lists entities and each row navigates on tap
  When the user taps the row's small logo tile
  Then the app navigates to <destination>
  And NO full-size viewer opens

  Examples:
    | surface           | destination           |
    | Booths list       | the exhibitor detail  |
    | Sponsors list     | the sponsor detail    |
    | Sponsors grid     | the sponsor detail    |
# Regression: the shared widget's tap wrapper is opt-out (enableFullScreen:false)
# precisely so a dense list row never loses its own tap target.
```

### E2E-LOGO-006 — No logo / failed fetch

```gherkin
Scenario: The fallback shows and cannot be enlarged
  Given an entity has never uploaded a logo (the asset route 404s)
  When its page renders
  Then the initials tile (or the booth short name / the gold anchor placeholder) is shown
  And tapping it opens no viewer with a broken picture
  Given the device is offline
  Then the same fallback is shown rather than a broken-image glyph
```

### E2E-LOGO-007 — Accessibility

```gherkin
Scenario: Screen-reader names
  Given a logo box for the sponsor "SAMI"
  Then the box exposes an image semantics node named "SAMI"
  And when it is tappable it also exposes a button role
  When the viewer opens
  Then the picture is named "SAMI" and the close control is named "إغلاق الصورة / Close image"
```

### E2E-LOGO-008 — RTL

```gherkin
Scenario: The viewer mirrors under Arabic
  Given the app language is Arabic
  When the full-size viewer opens
  Then the close control sits at the inline END (the physical left edge)
  And the picture itself is not mirrored
```

---

_Last reviewed:_ `2026-07-26` by `SIMF Team` — created for the owner's
"logo fits its box + press to see it full size" request.
