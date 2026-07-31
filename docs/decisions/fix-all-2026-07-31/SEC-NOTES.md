# SEC-NOTES — myComment.txt untracked, local copy preserved

Item ref: `SEC-NOTES` (Track F, fix-all run 2026-07-30). Owner decision **Q14**.
Files touched: `.gitignore`.

## ACTION REQUIRED BY THE ORCHESTRATOR

Build agents may not run `git`. The ignore rule alone does **not** untrack an
already-tracked file, so this item is only half done until the orchestrator runs,
once, from the worktree root:

```
git rm --cached myComment.txt
```

`--cached` is load-bearing: it removes the file from the index only and **leaves
it on disk**. Do **not** run `git rm` without it, and do not delete the file —
it is owner-authored and other work references it. Verify afterwards with
`git status --porcelain` (expect `D  myComment.txt` staged, and the file still
present on disk) and `git ls-files myComment.txt` (expect no output).

## DECISIONS_LOG

### D-NEXT — `myComment.txt` untracked from the repository; the file itself is kept

`myComment.txt` at the repo root is the owner's hand-written fix-list. It was
still git-tracked on 2026-07-30 (`git ls-files myComment.txt` returned it) and did
not appear in `.gitignore`.

**Decision (Q14): untrack it, keep it.** `myComment.txt` is added to `.gitignore`
and the orchestrator runs `git rm --cached myComment.txt` once. The file stays on
disk unchanged.

**Why untrack rather than delete or migrate into `docs/`.** The exposure is not
what the file contains today — it is that a plaintext working-notes file at the
repo root has **no review gate**. Whatever gets pasted into it next is committed
by the next `git add`, with nobody reading it. That is exactly how the sibling
scratch file `txt.txt` came to carry a plaintext production super-admin credential
twice over before it was removed on 2026-07-30. Untracking removes the path from
the commit surface without destroying an artefact the owner wrote and other work
still cites — non-destructive and reversible (`git add -f myComment.txt` restores
tracking if the decision is ever reversed).

`txt.txt`, the other file named in the SEC-NOTES defect, needs no action: it was
already removed and already ignored (`.gitignore`, same block). Only
`myComment.txt` remained.

The `.gitignore` entry carries the rationale inline and an explicit reminder that
the ignore rule does not untrack an already-tracked path, so a future reader does
not assume the rule alone did the job.

## PAGE-INDEX

No row. No page, route or API action is involved — this is a repository-hygiene
change to `.gitignore` plus a one-off index operation.

## E2E-README

No registry row. Nothing user-facing changed, so there is no page to author a
per-page Gherkin catalogue against. The verification for this item is the
`git ls-files` check in the ACTION REQUIRED block above, not a scenario.
