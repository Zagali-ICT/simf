# Booths / Exhibition — المعرض (Page 022, `#22`)

- **Route:** `/booths` (`RouteNames.booths`). Access: Public (Guest+).
- **Figma:** **922:2458** ("Halls"). Reuses the booth reads `GET /app/booths` + `/{id}` (D-199/D-230, via `VenueMapRepository`).
- **Clean-code freeze:** D-618 (2026-07-04). Built D-304/D-432/D-440.

## Purpose

The exhibitor-booth list with a client-side search. Tapping a card opens the
full exhibitor detail (`#220`); the أرشدني CTA deep-links the venue map focused
on that booth (`boothMap`).

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `booths_screen.dart` (211) | `BoothsScreen`/State — load, client filter, nav callbacks, `_buildBody` |
| `widgets/booth_card.dart` | `BoothCard` — composes the header/hall/officer/contacts/guide |
| `widgets/booth_company_header.dart` | `BoothCompanyHeader` (+ `_LogoTile`, `_CountryFlagTile`) |
| `widgets/booth_hall_row.dart` | `BoothHallRow` (+ `_HallBox`, `_CodePill`) |
| `widgets/booth_officer_row.dart` | `BoothOfficerRow` (+ `_initials`) |
| `widgets/booth_contact_boxes.dart` | `BoothContactBoxes` (+ `_ContactBox`) |
| `widgets/booth_guide_button.dart` | `BoothGuideButton` |

The error / empty / no-match states use the shared `SimfPullableHost` (D-618
replaced three hand-inlined always-scrollable scaffolds).

## L4 Figma parity (frame 922:2458)

`booths_922-2458` golden held without `--update` after the decomposition — layout
matches: search field, cards with logo-right / name-middle / flag-left RTL header,
A-12 code pill + HALL A · القاعة الرئيسية hall box, gold أرشدني CTA, bottom nav.
Deviations: country flag is the 🇸🇦 emoji (app flag form) vs the frame's flag image;
logo is CP `Image.network` in prod (name fallback in the no-network golden).

## Level-F

Wired: search filter, card → exhibitor detail, أرشدني → booth map, pull-to-refresh,
retry. **Flagged (feature call):** officer contact boxes show mail/call glyphs but
wire no `mailto:`/`tel:` launch. No missing API.

## Card header — the company name is never printed twice (PAR-B4, 2026-07-30)

`BoothCompanyHeader` renders the exhibitor (full) name under the gold short name.
The shipped seed sets `Name` and `ExhibitorName` to the **same** string on every
booth (`docs/migrations/2026/SIMF_App_SeedGaps.sql`), so every seeded card showed
the company name twice. The header now skips the beige full-name line when it
trims to the same value as the short name above it; a genuinely distinct trading
vs legal name still renders both. Fixing the seed instead would leave the card
unprotected against the next duplicate row, so the guard lives in the widget.

## Tests

`test/features/booths/booths_screen_test.dart` (12) +
`test/features/booths/booth_company_header_test.dart` (4, PAR-B4) +
`test/golden/booths_golden_test.dart`.
E2E: `docs/tests/e2e/mobile-booths.md`.

## Logo / photo boxes (owner 2026-07-26)

Every logo / photo box on this page renders through the shared
[`SimfLogoImage`](../../../../src/Mobile/simf_app/lib/app/widgets/simf_logo_image.dart):
a brand mark FITS its box (`BoxFit.contain`, replacing the crop-happy
`BoxFit.cover`), a portrait still fills its frame (`BoxFit.cover`), and — where
the box is not inside a tappable row — pressing it opens the picture full size
in [`SimfImageViewer`](../../../../src/Mobile/simf_app/lib/app/widgets/simf_image_viewer.dart)
(pinch-zoom, named for a screen reader, close / back to dismiss). The rules and
their scenarios live once in [`e2e/mobile-logo-viewer.md`](../../../tests/e2e/mobile-logo-viewer.md)
(E2E-LOGO-001..008).

**DEF-LGO-002 (2026-07-27).** The card header's 48×48 booth-logo tile inset the
mark horizontally only, so its content box was 40×48 while the image still asked
for 48×48 — the tile's clip shaved 4px off each side of even a perfectly SQUARE
logo. The inset is square now (`EdgeInsets.all(space1)`) and the mark is painted
at the box's real 40×40, so nothing is cropped. Full-size-on-tap stays off here:
the tile sits inside the tappable booth card, whose tap owns the navigation to
the exhibitor detail, where the 108px identity logo IS tappable.
