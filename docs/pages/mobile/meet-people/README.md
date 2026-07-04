# Meet people — قابل أشخاص مثلك (Page 035, `#35`)

- **Route:** `/meet` (`RouteNames.meetPeople`). Access: **Visitor (login-only, approved account)** — `RequireApprovedAccount`.
- **Figma:** **1072:13409** (D-448 parity). **Clean-code freeze:** D-632 (2026-07-04).

## Purpose

The "meet someone like you" recommendations for an approved visitor
(`GET /app/account/recommendations/meet-like-you`): a smart-suggestions header
card (title + subtitle + three topic chips) over per-match cards — the gold **%
match** (from the scorer's `score`) over the `تطابق` label, the name, the
profile-type line, the match reason (prefers the backend-generated bilingual
`matchReason`, D-451; falls back to the shared-interest count) and a gold initials
avatar.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `meet_people_screen.dart` (109) | `MeetPeopleScreen` (`ConsumerWidget`) — reads `meetRecommendationsProvider`, `onRefresh`, the loading / error / data dispatch, and the inline no-matches state (`_EmptyInline`). Re-exports `data/`. |
| `data/meet_repository.dart` | `meetRecommendationsProvider` (moved here from the screen — D-545), beside the existing `data/meet_models.dart` (`Recommendation`, `MatchedInterest`). |
| `widgets/meet_header_card.dart` (`MeetHeaderCard` + `_TopicChip`) | The smart-suggestions header card (title / subtitle / three topic chips). |
| `widgets/meet_match_card.dart` (`MeetMatchCard` + `_PercentBlock`/`_Avatar` + the `_percent`/`_reason`/`_avatarInitials` helpers) | One match card — the gold initials avatar, the name / profile-type / reason column, and the gold `% تطابق` block. |

The provider moved to `data/`; the card's pure helpers moved with the card; screen
was already fully tokenised (no raw `Color(0x..)`). Every file ≤400 lines.

## DRY (this freeze)

- The load-error `_Error` → the shared **`SimfErrorState`** (message + retry,
  already hosted inside `SimfPullableHost`) — the standard error state used across
  gallery/news; the error-state widget test passes unchanged.
- The no-matches `_EmptyInline` is **kept local (not `SimfEmptyState`)** — by
  design it renders **inline beneath the always-visible header card**, whereas the
  shared full-screen empty would replace the header too. The empty-state test
  ("keeps the header and shows the empty notice") passes unchanged.

## L4 Figma parity (frame 1072:13409)

Captured `meet_people_1072-13409.png` (@375×900, ar, 2 matches) as the **baseline
before** the refactor, then **held it WITHOUT `--update`** after — proving the
provider move + the 2-card extraction byte-identical. Golden read: قابل أشخاص مثلك
header, the smart-suggestions card (title + subtitle + 3 topic chips), two match
cards (gold avatar + name/type/interests right, gold `% تطابق` left), RTL, no tofu.

## Level-F

Wired: pull-to-refresh + retry re-fetch; each match card renders the score /
reason / avatar; the header topic chips are display-only (the frame shows them
static). Reads `GET /app/account/recommendations/meet-like-you`. No missing API.

## Tests

`test/golden/meet_people_golden_test.dart` (frame 1072:13409, @375×900, ar) +
`test/features/meet/meet_people_screen_test.dart` (header + match % score, empty
keeps header, error state, Arabic % block position) + `meet_models_test.dart`.
E2E: `docs/tests/e2e/mobile-meet-people.md`.

## Related decisions

- **D-632** (this clean-code freeze — provider move + 2 card widgets + `SimfErrorState` swap + first golden).
- **D-313** (screen built), **D-448** (1072:13409 parity), **D-451** (backend match reason).
