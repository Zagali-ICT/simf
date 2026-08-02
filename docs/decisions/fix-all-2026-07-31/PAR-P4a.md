# PAR-P4a — the host STAR glyph is not rendered on the speakers list

Item ref: `PAR-P4a` (Track D-a, fix-all run 2026-07-30).
Files touched:
`src/Mobile/simf_app/lib/features/sessions/widgets/session_speaker_card.dart` ·
`…/session_detail_screen.dart` (doc) ·
`test/features/sessions/widgets/session_speaker_card_test.dart` ·
`docs/tests/e2e/mobile-session-detail.md` · `docs/pages/mobile/session-detail/README.md`.

## DECISIONS_LOG

### D-NEXT — PAR-P4a: the session host carries the gold star glyph, fulfilling the D-432 promise

The item offered two mutually exclusive resolutions: render the star, or correct
`speaker_list_card.dart:14-17` as a stale claim. **The comment is not stale — it
is accurate about the LIST and makes a promise about the DETAIL that the detail
did not keep**, so the star was built rather than the comment weakened.

- The list card's D-432 note says the host/speaker distinction is **per-session**
  (it lives on the session↔speaker join, not on the speaker), so the المتحدثون
  list correctly shows the same anchor for everyone. That half is right, and
  changing it would be wrong: a speaker who hosts one session is not globally a
  host.
- Its closing clause — "the host star appears on the session detail" — was the
  unmet part. On the detail, `SessionSpeakerCard` appended the plain text
  `l10n.hostLabel` ("المضيف") to the rank line, and a repo-wide grep for
  `Icons.star` / `ic_star` found star glyphs only in feedback, the more-menu,
  my-area and notifications — never on a speaker.

**Built:** when `speaker.role == SessionSpeakerRole.host` the card's rank line
renders `Icons.star_rounded` at 14px in `SimfTokens.accent` immediately before
the host label — `<rank> · ★ المضيف`. The star is **added beside** the label, not
swapped for it: the word is the accessible, translatable marker and the glyph is
the visual one, and dropping the text would have cost a screen-reader user the
information. A plain speaker's line is unchanged (bare rank, no marker), and a
host with no recorded rank shows `★ المضيف` alone. There is no `star.svg` in
`assets/icons/`, so the Material glyph is used — the same source every other star
in the app already uses.

`speaker_list_card.dart` is left **unedited**: its claim is now true.

## PAGE-INDEX

Covered by the `#17 sessionDetail` row rewrite in `docs/_pending/29.md`. The
`#20 speakerProfile` / speakers-list rows do not change — no list-surface
behaviour changed.

## E2E-README

Covered by the `#17 sessionDetail` registry row rewrite in `docs/_pending/29.md`
(range `E2E-MOB017-001..034`, which includes `-034` for this item). No second row.
