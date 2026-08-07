# STALE-GOLDEN-ARTIFACTS — 48 golden-failure PNGs were committed

Item ref: `STALE-GOLDEN-ARTIFACTS` (Track D-b, fix-all run 2026-07-30).
Files touched:
**deleted** `src/Mobile/simf_app/test/golden/failures/` (48 files) ·
`.gitignore` ·
`src/Mobile/simf_app/test/repo/platform_projects_tracked_test.dart`.

## DECISIONS_LOG

### D-NEXT — STALE-GOLDEN-ARTIFACTS: golden-comparison output is deleted and ignored, and a ratchet stops it coming back

`test/golden/failures/` is where `flutter test` writes the four comparison PNGs
(`isolatedDiff` / `maskedDiff` / `masterImage` / `testImage`) for each **failing**
golden. It is output of a red run, never an input to one. 48 files were tracked —
12 screens × 4 — frozen against a long-superseded revision while the suite ran
green, so anyone grepping the directory was reading obsolete debris and
"diffs exist" looked like "goldens are broken".

**Deleted from disk and ignored** (`src/Mobile/simf_app/test/golden/failures/` in
`.gitignore`). The golden **masters** are unaffected: they live in
`test/golden/goldens/` and stay tracked — that is the input.

Note for whoever lands this: the directory was *tracked*, so the ignore rule
alone does not untrack it. The deletion is staged as part of this changeset
(`git add -A` on that path, or `git rm -r --cached` followed by the ignore rule)
— exactly the mistake the `myComment.txt` entry records for its own file.

**Ratchet** in `test/repo/platform_projects_tracked_test.dart` (the repo-hygiene
suite that already guards the native platform projects, BUG-010/BUG-009): two
new cases assert that `.gitignore` still carries the rule (it does not on the
pre-fix tree, so this fails there) and that the golden **masters** in
`test/golden/goldens/` are still present — i.e. that the right directory was
deleted.

**Deliberately NOT asserted: "the failures directory is empty."** A golden that
fails in the very run executing this test writes into that directory while the
suite is going, so an emptiness check would turn one red golden into two
failures and point at the wrong cause. The ignore rule is the durable guard, and
it is the deterministic one: ignored ⇒ untracked ⇒ cannot be committed again.

## PAGE-INDEX

No row — this is repo hygiene, not a page. No change.

## E2E-README

No row — no page or API surface changed. No change.
