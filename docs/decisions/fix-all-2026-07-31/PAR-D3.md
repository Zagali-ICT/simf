# PAR-D3 — the session detail is missing the category pill

Item ref: `PAR-D3` (Track D-a, fix-all run 2026-07-30).
Files touched:
`src/Mobile/simf_app/lib/features/sessions/widgets/session_header_card.dart` ·
`…/session_detail_screen.dart` (the stale doc comment) ·
`test/features/sessions/widgets/session_header_card_test.dart` ·
`docs/tests/e2e/mobile-session-detail.md` · `docs/pages/mobile/session-detail/README.md`.

## DECISIONS_LOG

### D-NEXT — PAR-D3: the session-detail category tag pill is restored under the title

`SessionDetail.localizedCategory(isArabic)` has existed since D-226 and was
called by nothing: the header card was badge+title, meta row, two action chips,
and the body added no pill — so the session's category was decoded on every
detail fetch and never shown. `FIGMA-PARITY-DEFECTS.md` PAR-D3 recorded it as
missing.

**Built:** a small gold-hairline pill (`_CategoryPill`, 4px radius,
`labelGoldSemiboldSm`) sits under the header-card title, bound to
`localizedCategory(isArabic)` and rendered **only** when that is non-null and
non-blank. The `SessionCategory` lookup ships empty pending the client's list
(OI-2 / D-226), so an uncategorised session keeps exactly the pre-PAR-D3 layout —
the pill cannot introduce an empty box. It is also suppressed on the #29 workshop
reduction.

**Only the category, not the hall.** The register allowed "optionally the hall
name". The hall is not rendered — the defect is about the category tag, and
adding a second pill would be a layout change against a frame nobody has
re-measured this round.

**A documented contradiction, resolved in favour of the parity audit.** The E2E
catalogue's D-449 re-skin note says "the prior hall/category tag pills are
**removed** (889:2715)", i.e. the removal was deliberate. The parity audit
(PAR-D3) says the tag is missing and should be there. The two documents disagree;
the fix-all plan (item D7) adjudicated in favour of restoring it. The catalogue
note is left in place and E2E-MOB017-033 records that it is now reversed, so the
next reader sees both sides rather than a silently rewritten history. **If the
Figma frame is re-measured and shows no pill, this is the entry to revert.**

**Stale comment fixed in the same change** (the second half of the item):
`session_detail_screen.dart` promised a header card with "hall + category tag
pills" that had not existed since D-449. It now describes the as-built card —
badge + ordinal, title, the category pill when present, the meta line and the two
actions — and additionally records the #29 workshop reduction and the PAR-P4a
host star.

**Golden impact:** `goldens/session_detail_889-2450.png` is rendered from a
categorised fixture (`جلسة رئيسية`), so it must be re-locked with
`--update-goldens`. No other golden renders a categorised session detail.

## PAGE-INDEX

Covered by the `#17 sessionDetail` row rewrite in `docs/_pending/29.md` — the
same single row carries #29, PAR-D3 and PAR-P4a. No second row.

## E2E-README

Covered by the `#17 sessionDetail` registry row rewrite in `docs/_pending/29.md`
(range `E2E-MOB017-001..034`, which includes `-033` for this item). No second row.
