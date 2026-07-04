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

## Tests

`test/features/booths/booths_screen_test.dart` (12) + `test/golden/booths_golden_test.dart`.
E2E: `docs/tests/e2e/mobile-booths.md`.
