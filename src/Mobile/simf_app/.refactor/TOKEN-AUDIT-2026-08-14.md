# SimfTokens usage audit — 2026-08-14 (Decision 6)

665 declarations measured across lib, test and integration_test.

  * 9 DEAD, now deleted (this commit).
  * 443 used EXACTLY ONCE.
  * 213 used twice or more.

## The measurement gotcha, banked

A token is referenced two different ways, and counting only one of them gives a
wrong answer that COMPILES right up until you act on it. Outside the file it is
`SimfTokens.textXxl`; INSIDE tokens.dart, where composite styles are built from
scale entries, it is the bare identifier `textXxl`. The first pass counted only
the qualified form, called 14 tokens dead, and deleting them broke the build on
5 — textXxl, timestampMuted, onGoldMuted and seatNumberSize are all used
internally. Count both, or do not delete.

## The 443 single-use names are NOT a defect list

Decision 6 deliberately splits this: the dead ones go, the single-use ones get
a report and their own decision. Folding 443 names back into their call sites
would be a very large diff in the highest blast-radius file in the app, and it
would reverse a completed wave — the tokenisation programme that took inline
TextStyle from 526 to 0 CREATED most of these by design. A name used once is
not automatically wrong: it is wrong when the name says less than the value it
hides, and right when it says more.

## Raw lists


