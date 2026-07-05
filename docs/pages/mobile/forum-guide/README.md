# Forum guide — دليل الملتقى (Page 200, `#200`)

- **Route:** `/forum-guide` (`RouteNames.forumGuide`). Access: **Guest+ (public)**. No API (static in-app copy).
- **Figma:** **1388:7493** (D-464). **Clean-code freeze:** D-638 (2026-07-04).

## Purpose

A static in-app guide: a gold intro banner, then five numbered step cards (gold
index badge + title + muted description) on the navy-deep card chrome. The steps
live in `AppL10n`; the caret on each card is decorative (the steps don't
navigate). Reached from المزيد → دليل الملتقى.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `forum_guide_screen.dart` (49) | `ForumGuideScreen` (`StatelessWidget`) — the five l10n step records + the `ListView` composing the banner + step cards. |
| `widgets/forum_guide_cards.dart` (`ForumGuideBanner` + `ForumGuideStep`) | The gold intro banner + one numbered step card. |

Already fully tokenised (no raw `Color(0x..)`), data-free, shared shell — so this
freeze is the two-card extraction + the first golden. Every file ≤400 lines.

## L4 Figma parity (frame 1388:7493)

Captured `forum_guide_1388-7493.png` (@375×900, ar) and **read it** — the gold
intro banner (welcome copy + book glyph), the five numbered step cards (gold badge
right, title + muted body, gold caret left), RTL, no tofu. The card extraction is
verbatim, so this golden locks the D-464 parity going forward.

## Level-F

Read-only static guide — back only; the step carets are decorative (design). No
API.

## Tests

`test/golden/forum_guide_golden_test.dart` (frame 1388:7493, @375×900, ar) +
`test/features/forum_guide/forum_guide_screen_test.dart`. E2E:
`docs/tests/e2e/mobile-forum-guide.md`.

## Related decisions

- **D-638** (this clean-code freeze — two-card extraction + first golden).
- **D-464** (built from ComingSoon → Figma 1388:7493).
