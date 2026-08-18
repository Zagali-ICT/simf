# Speakers list (المتحدثون) — mobile `/speakers`

| Field | Value |
|---|---|
| Route | `/speakers` (`RouteNames.speakers`, page #19) · Guest+ |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/speakers/speakers_screen.dart` (`SpeakersScreen`, 177 lines) |
| Widgets | `lib/features/speakers/widgets/` — `speaker_sort_control` (`SpeakerSortControl`) · `speaker_list_card` (`SpeakerListCard`) · `speaker_avatar` · `speaker_name_with_flag` (`SpeakerNameWithFlag` — the name + country-flag line, shared with the meeting-request target tile) |
| Figma node | `908:1744` |
| Shell | `SimfPageShell` (centred title المتحدثون) |
| API | `GET /app/speakers` (one read; client-side search + A→Z sort) · avatar `GET /app/assets/SpeakerPhoto/{id}/image` |
| Providers | `speakersRepositoryProvider` · `simfDataConfigProvider` |
| Tests | `test/features/speakers/speakers_screen_test.dart`; golden `test/golden/speakers_golden_test.dart` (`goldens/speakers_908-1744.png`); E2E [`mobile-speakers.md`](../../../tests/e2e/mobile-speakers.md) |
| Legacy detail | `docs/App/Page_019/` — retained as the historical spec |
| Status | ✅ Real — D-302 → 908:1744 parity (P4) → **clean-code frozen (D-608)** |

## 1. Purpose
The ordered speaker list: a shared search field + a sort control over a
pull-to-refreshable list of speaker cards (photo tile, name + country flag,
rank); tapping a card opens the profile (#20).

## 2. Audience & access
Guest+ (public read).

## 3. Button / action audit (Level F, 2026-07-04)
| Control | Handler | Backend |
|---|---|---|
| Back | `backOrHome` | — |
| Search field | client-side filter (setState) | — |
| Sort control | toggle A→Z (setState) | — |
| Speaker card | push `speakerProfile` #20 | — |
| Retry / pull-to-refresh | `_load()` | `GET /app/speakers` |

All data repo-backed; filter/sort are client-side over the one read.

## 4. Clean-code freeze (D-608)
**447 → 229-line screen** + 2 widget files (`SpeakerSortControl`,
`SpeakerListCard`). Render-preserving: the `speakers_908-1744` golden **held
without `--update`** (the P4 parity holds); 10 module tests green. The card is
kept local to speakers (not merged with the frozen session-detail speaker card
— different frame/shape; extract-on-genuine-duplicate).

## 5. Changelog
- **2026-08-18 (delivery clean-code programme, structure only):** 229 → **177**
  lines — the doc header collapsed to a one-line pointer at this folder, and the
  card's name + country-flag line became the public `SpeakerNameWithFlag`, now
  reused by the meeting-request target tile instead of being copied into it. The
  `speakers_908-1744` golden held **without** `--update-goldens`.
