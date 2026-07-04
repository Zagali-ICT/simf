# Sponsors — الرعاة (Page 023, `#23`)

- **Route:** `/sponsors` (`RouteNames.sponsors`). Access: Public (Guest+).
- **Figma:** **922:2824** ("Shepherds"). Reads `GET /app/sponsors` (tier-grouped, D-199).
- **Clean-code freeze:** D-620 (2026-07-04). Built D-305/D-440.

## Purpose

Sponsors grouped by tier. The layout is **position-based** (faithful for any tier
naming): the **first** tier is the gold hero card, the **last** tier (when >1) is
the compact 3-column logo grid, and any tier **in between** is a navy premium card.
Tapping a sponsor opens the sponsor detail (`#221`).

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `sponsors_screen.dart` (145) | provider + `SponsorsScreen.build` — the three-band tier layout |
| `widgets/sponsor_logo.dart` | `SponsorLogo` (real logo / initials fallback, shared by card + grid) + `sponsorBadgeText` initials helper |
| `widgets/sponsor_card.dart` | `SponsorCard` (+ `_BadgeBox`) — hero/premium row card |
| `widgets/sponsor_grid.dart` | `SponsorGrid` (+ `_SponsorGridTile`) — the lowest-tier logo grid |

Tier labels use the shared `SimfSectionHeader` (D-620 replaced the local `_TierLabel`).

## L4 Figma parity (frame 922:2824)

`sponsors_922-2824` golden held without `--update` after the decomposition — layout
matches: gold hero card (SAMI), premium navy cards (GAMI/RSNF/GADD), 3-column
gold-tier grid, tier labels, bottom nav, all RTL. Logos are CP `Image.network` in
prod (initials fallback in the no-network golden).

## Level-F

Wired: hero/premium card + grid tile → sponsor detail, pull-to-refresh, retry.
Reads `GET /app/sponsors`. No missing API.

## Tests

`test/features/sponsors/sponsors_screen_test.dart` (10) + `test/golden/sponsors_golden_test.dart`.
E2E: `docs/tests/e2e/mobile-sponsors.md`.
