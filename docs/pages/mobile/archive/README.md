# Archive — الأرشيف (Page 024, `#24`)

- **Route:** `/archive` (`RouteNames.archive`). Access: Public (Guest+).
- **Figma:** **925:3079** (list + edition detail 24-01 in one frame).
- **Clean-code freeze:** D-617 (2026-07-04). Built D-273/D-307/D-432/D-440.

## Purpose

Past editions of the forum. `GET /app/archive` loads the editions list once;
selecting a pill lazily loads that edition's fuller detail (`GET /app/archive/{id}`
— location, date label, gallery, session titles, past speakers).

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `archive_screen.dart` (252) | providers + `ArchiveScreen`/State (selection, pull-to-refresh) + `_ArchiveBody` detail-column assembly |
| `widgets/archive_notice_banner.dart` | `ArchiveNoticeBanner` |
| `widgets/archive_edition_pills.dart` | `ArchiveEditionPills` (+ `_EditionPill`) |
| `widgets/archive_bullet.dart` | `ArchiveBullet` — disc-bulleted list item |
| `widgets/archive_place_time_row.dart` | `ArchivePlaceTimeRow` (+ `_LabelledBullet`) |
| `widgets/archive_stat_row.dart` | `ArchiveStatRow` (+ `_StatTile`) — الفعاليات / المتحدثون |
| `widgets/archive_gallery_row.dart` | `ArchiveGalleryRow` (+ `_GalleryTile`) |
| `widgets/archive_session_title_card.dart` | `ArchiveSessionTitleCard` |
| `widgets/archive_past_speakers_row.dart` | `ArchivePastSpeakersRow` (+ cards, overflow, initials) |

Section labels use the shared `SimfSectionHeader`; the absolute-http(s) guard uses
the shared `core/utils/http_url.dart` `isHttpUrl` (also to be adopted by the
sponsor/exhibitor detail screens).

## L4 Figma parity (frame 925:3079)

`archive_925-3079` golden held without `--update` after the decomposition (render
unchanged) — chrome/layout/RTL all match: notice banner, pills, gold bulleted title,
نبذة, المكان/الزمن, the 250/30 stat tiles, gallery, session cards, past-speakers +
"+N آخرون" overflow. Gallery/speaker photos are real in the frame and CP
`Image.network` in production (placeholders in the no-network golden).

## Level-F — findings flagged (feature decisions, not fixed)

1. Gallery **video** tiles show a play glyph but have no tap handler — no lightbox/player.
2. Past-speaker cards are display-only (past speakers have no profile route).
3. `ArchivePastSpeaker.countryId` (D-456 corner flag) is decoded but not drawn.

Wired: pill select, pull-to-refresh, retry, both read endpoints. No missing API.

## Tests

`test/golden/archive_golden_test.dart` (render-lock @375×1293) + the archive model
tests. E2E: `docs/tests/e2e/mobile-archive.md`.
