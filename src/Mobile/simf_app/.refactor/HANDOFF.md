# Clean-code refactor — session hand-off

**Branch `refactor/clean-code-2`** (off `feature/centralized-file-store` tip 283ef215,
pushed to origin 2026-07-03; Region fix `981ebee3` cherry-picked as def32866 so App
integration tests can run). Worktree **D:/SIMF/wt-app**. Toolchain: run flutter from
`src/Mobile/simf_app`; **never `git add -A`, never `dart format`**; gate = analyze
0 errors/warnings (2123 info baseline) + full suite green + goldens WITHOUT --update.

Full plan: `C:\Users\LOQ\.claude\plans\based-on-app-clean-prancy-pudding.md` (owner-approved
2026-07-03). Program docs: `~/.claude/skills/clean-code-skills/resources/`.

## 2026-08-14 — remainder-closure run (supersedes everything below)

**Branch `refactor/app-clean-code-3`**, worktree **D:/swtcc**, base `c88e771b`
(`origin/main`). Verified before starting: `git diff origin/main HEAD -- .../lib`
is empty, so the worklist measured on the other checkout is valid against this
base. Nothing pushed yet.

Plan: `~/.claude/plans/groovy-stargazing-hearth.md` (owner-approved, eight
recorded decisions). Per-file worklist: `.refactor/WORKLIST-2026-08-13.md` —
2,407 rows over 591 files, merged from `flutter analyze`, `tool/conventions` and
a 13-agent read-only audit.

### The headline

| Metric | Session start | Now |
|---|---|---|
| `flutter analyze` infos (lib+test+integration_test) | 2137 | **0** |
| errors / warnings | 0 / 0 | **0 / 0** |
| `lines_longer_than_80_chars` | 1690 | **0** |
| `packages/` findings under the app's ruleset | 249 | **0** |
| `tool/conventions` SIMF-C3 | 12 | **11** (blocked, see below) |
| tests | 1401 | **1479** app + 46 auth_pkg + 15 data_pkg |
| files over 400 lines | 15 | 12 |

**The analyzer ratchet is CLOSED.** `flutter analyze lib test integration_test
packages` reads "No issues found!" and exits 0, so `--no-fatal-infos` was
dropped from the MobileApp CI stage - an info now fails the build. Both local
packages were moved off `flutter_lints` onto the app's own
`very_good_analysis` in the same wave, so the code the whole app depends on is
no longer the code held to the loosest standard.

The zero is **argued, not swept**: two rules are off with their measurement
recorded at the rule (`specify_nonobvious_property_types`,
`public_member_api_docs`), and ~two dozen sites carry a targeted `// ignore:`
with the reason above it. If you add one, write why the analyzer is WRONG
about that line, or fix it.

Every increment gated on: analyze 0 errors / 0 warnings, full suite green, and
**every golden holding without `--update-goldens`**.

### Done (15 commits)

Phase 0 — worklist made durable; baseline pinned in this worktree; the audit's
6-file coverage gap closed (`features/content/*` + `main.dart`).

Phase A — providers out of screens into `data/`; `exhibition/` widgets into
`widgets/`; `simf_page_shell` 527 -> 333 split into three re-exported groups;
`session_detail_screen` 466 -> 420 (closed one SIMF-C3);
`identity_verification_screen` 452 -> 397; the 3-copy 12-hour formatter and the
3-copy day grouping unified into `core/utils`; governing docs re-dated.

Phase B — the mechanical analyzer pass across every feature; the language flag
converted from a positional bool to `isArabic:` at ~350 call sites; the
formatter run on the touched files followed by restoring the trailing commas
(owner-authorised, see below); and `sessions` taken through its audit rows.

The sessions pass is the template for the remaining features: one search rule
instead of a tested-but-uncalled copy and an untested shipped one, the schedule
list made lazy, a per-rebuild map lifted into a derived provider, two hand-nested
compositions replaced with the shared widget, one widget moved to its own file.

### Three defects found and fixed while executing

### Defects found and fixed while executing

* **`tool/conventions` passed vacuously in any git worktree.** Its root walk-up
  tested `Directory('.git').existsSync()`, and in a worktree `.git` is a FILE.
  It scanned nothing and reported "No violations found". Fixed, with three
  regression tests (checker suite 31 -> 34).
* **`dart fix --code=use_decorated_box` is not behaviour-preserving here.**
  `Container` insets its child by `BoxDecoration.padding`, which is the border's
  dimensions; `DecoratedBox` does not. Caught by the share_my_contact golden
  (2.42%, 7366px). The second conversion had no golden at all.

### The four tools the line-length work left behind

All in `.refactor/`, all refuse rather than guess, all reusable:

* `wrap_comments.py` — re-flows comment blocks **per unit**: a blank line, a
  list item (hanging indent, so the continuation reads as markdown), an
  `// ignore:` directive (copied byte for byte, because wrapping one silently
  stops it suppressing anything) or a run of prose. Measures in **UTF-16 code
  units**, which is how the analyzer counts — a regional-indicator flag costs 4
  where Python's `len` sees 2.

  This is the merge of three scripts, and the merge is the lesson: they had
  drifted into three greedy wrappers with different width accounting, and the
  UTF-16 fix landed in only one of them. `wrap_ignore_reasons.py` existed only
  because the original refused directive blocks — and, being a separate copy,
  never got the fix, so it could not see the blocks it was written for. **If
  you need a fourth comment behaviour, add a unit kind here; do not fork.**
* `split_long_strings.py` — splits a literal into two adjacent literals, which
  Dart concatenates at compile time. Never inside a word, an escape or an
  interpolation.
* `lift_trailing_comments.py` — moves a trailing `// note` off a declaration
  into a `///` doc comment above it, the one comment shape that cannot be
  re-flowed in place because its indent belongs to the code.

### Audit rows RE-MEASURED against the current tree (2026-08-14)

The 2026-08-02 rows are stale in both directions — line numbers are all wrong
after the reformat, and several categories closed on their own. Measured, not
assumed:

| Category | Audit said | Actually now |
|---|---|---|
| RAW-STRING | 2 | **0** (`unnecessary_raw_strings` reads 0) |
| MISSING-CONST | 5 | **0** (`prefer_const_*` read 0) |
| DEAD-COMMENT-CODE | 14 | **0** — all 5 greppable hits are prose continuations (`// import of the shell keeps resolving.`), not commented-out code |
| NARRATION-COMMENT | 9 | **0** — the 3 hits are prose and a release number (`// Build #13 —`) |
| INLINE-TEXTSTYLE (raw size) | 5 | **0** (done, see the tokens commit) |
| RTL-DIRECTIONAL | 7 | **4**, all declined with cause — each pairs with an explicitly physical `Alignment.centerLeft`, so it is a Figma question (D-886) |
| `Image.network` unsized | 20 | **11 real** call sites; the other 9 were doc comments the audit counted as hits. Deferred to a device (D-886) |
| NON-LAZY-LIST | 11-12 | ~43 `ListView(` sites, most correctly static content pages; the data-driven ones still need per-site judgement |

**Do not act on a 2026-08-02 row without re-measuring it first.** Two of the
three categories I checked were already closed, and one (`Image.network`) was
nearly half phantom.

### Next, in order

1. **Decision 7 — the `AsyncValue` conversions. 22 of 24 DONE; the last 2 are
   device-blocked.**

   Only `sign_up_visitor_screen` and `register_visitor_screen` still hand-roll
   `bool _loading` — the same two screens D-666 blocks from being split, for the
   same reason: their face-capture path needs on-device verification that a
   green golden demonstrably does not provide. Convert them in the session that
   has a device attached, alongside the split.

   **Re-measured at the start: 24 screens, not the plan's 35.** Grep
   `^\s*bool _loading` across `lib` — 11 of the plan's set were converted or
   deleted in earlier waves. Do not work from the number.

   **22 of 24 converted**, every one with its existing tests passing
   **unchanged** — which is the signal that says the state machine is faithful,
   not merely green. Five shapes emerged, and picking the right one is the whole
   job:

   | Shape | When | Examples |
   |---|---|---|
   | **Fold to null** | a server outcome genuinely means "nothing to show" | `terms` (404 + empty body), `news_article` / `speaker_profile` (404 = gone) |
   | **No fold** | "empty" is already an empty list | `my_contacts`, `booths`, `sessions` |
   | **Stay an error, branch in `error`** | the outcome is a failure with its own copy | `my_visitors` (403 = "not linked to a booth yet") |
   | **Gate inside the provider** | the screen must not call the endpoint at all | `my_area` (Approved-only, L-5) |
   | **`AsyncNotifier`** | build and refresh must behave DIFFERENTLY, or the data is EDITED | `badge`, `session_moderate` |

   **The `AsyncNotifier` case is the one to recognise early.** `badge`'s first
   attempt used the gate-in-provider shape and its own test caught the
   regression: gating the refresh on the cached auth state left a pending user
   able to pull forever without ever leaving the state, because the dashboard's
   403 is HOW approval is discovered. `build()` must skip the call and
   `recheck()` must make it — a `FutureProvider` has only one path.
   `session_moderate` needs one for the other reason: five optimistic edits with
   rollback, which cannot be expressed by mutating a `FutureProvider`.

   **Side effects on load** (`notifications`' mark-all, `venue_map`'s camera
   focus, `gate_scan`'s backlog flush) all use the same shape: await the
   provider's FIRST future in `initState`. `ref.listen` would re-fire them on
   every pull-to-refresh.

   Orthogonally, the screen either becomes a `ConsumerWidget` (`terms`,
   `news_article`, `my_contacts`, `my_visitors`) or stays stateful because it
   owns real UI state — a search box, a sort toggle, a tab index (`speakers`,
   `booths`, `sessions`, `speaker_profile`, `my_area`).

   **Two findings worth carrying into the last two screens:**

   * **The plan's "wave 2 is just submit spinners" is wrong.** All five had a
     REAL data load with the submit flag beside it. The load converts; the flag
     stays. Expect the same of the two blocked screens.
   * **`session_detail` did NOT need its goldens re-locked**, though the plan
     said it would. Its render goes through a shared states widget taking
     `loading`/`notFound`/`failed` BOOLEANS, so feeding those from the
     `AsyncValue` leaves the tree untouched. The render only moves if you ALSO
     swap the host for `SimfRefreshableMessage`, which is a separate change this
     phase never needed. Check for that pattern before assuming a re-lock.

   **Editing tool note, learned twice.** Range-based Python replacements
   silently swallowed a `build` method (`live_broadcast`), two closing braces
   (`rate`) and a whole `_toggleInterest` (`sign_up_interests`) — every one
   caught by the analyzer, but each cost a revert or a recovery from git. On a
   screen whose branching lives inside `build` rather than a flat `_body`, use
   targeted `Edit` calls instead.

   **`terms_screen` is the template** (commit `0a6bf182`). What made it work,
   in order:
   1. A `FutureProvider.autoDispose` that folds the screen's EXTRA states into
      the data type. Terms had `_empty` beside `_loading`/`_error`; returning
      `ContentBlock?` and mapping both "nothing to show" cases (a 404, and a
      present-but-empty body) to null collapsed three server outcomes onto the
      three branches `when` already has.
   2. The screen usually stops needing to be stateful at all — terms went
      `ConsumerStatefulWidget` -> `ConsumerWidget`.
   3. `onRetry` = `ref.invalidate(provider)`; the PULL keeps
      `refreshAsync(ref, provider.future)`, whose future the RefreshIndicator
      awaits.
   4. Existing tests should pass **UNCHANGED** — that is the signal the state
      machine is faithful. Terms' 5 did.
   5. The data-state golden must hold **WITHOUT** `--update`. A data golden
      that moves is a conversion bug, not a re-lock.

   **Expect to find bugs.** The conversion forces you to look at the error
   branch, and terms was rendering `ApiFailure.message` raw — English exception
   text to Arabic users. The sweep that followed found two more
   (`my_area` avatar upload, `seat_picker` seat move); `gate_scan` and
   `register_visitor` looked like sites and are not. Check the RENDER before
   fixing: `register_visitor`'s `_loadError` is never displayed.

   **The 19 left**, wave 1 (genuine data loads) first, per the plan:

   | Wave 1 — data loads | Wave 2 — submit spinners (the weaker case) |
   |---|---|
   | `ai_summary/session_summary` | `account/sign_up_interests` |
   | `badge` * | `account/sign_up_visitor` **(device-blocked)** |
   | `exhibitor/my_visitors` | `contacts/share_my_contact` |
   | `gates/gate_scan` | `feedback/rate` |
   | `live/live_broadcast` | `myarea/my_mobile` |
   | `moderation/session_moderate` | `registration/registration_status` |
   | `myarea/my_area` | `staff/register_visitor` **(device-blocked)** |
   | `notifications` | |
   | `sessions/session_detail` * | |
   | `sessions/sessions` | |
   | `speakers/speaker_profile` | |
   | `venuemap/venue_map` | |

   \* carries extra care: `badge`'s four pull-to-refresh tests assert per-branch
   behaviour, and `session_detail` is the screen the plan flags where swapping
   to `SimfRefreshableMessage` **moves the render** (a bare `ListView`
   top-aligns the state; the shared host centres it in a viewport-tall box), so
   its goldens are re-locked in the same changeset with the diff inspected.

   Two more things to know:
   * `test/repo/pull_to_refresh_coverage_test.dart` keys on widget NAMES. A
     shared async-body widget that owns the refresh will need that regex
     extended, deliberately, in the same changeset.
   * The 21 screens whose `Perf:` line reads "builds every child up front" are
     a DIFFERENT population from these — list laziness is its own pass.
2. The surviving read-audit rows: DOC-HEADER (largely absorbed by Decision 5),
   NAMING, and the genuinely data-driven NON-LAZY-LIST subset.
   **ONE-WIDGET-PER-FILE is closed**: the three heterogeneous files are split
   (16 widgets, 16 files, originals removed) and the other 17 findings are
   cohesive groups that CLAUDE.md section 1 now explicitly permits.
3. ~~Decision 5~~ (71 headers), ~~Decision 6~~ (token audit),
   ~~Decision 8~~ (`packages/`), ~~Phase E~~ — **all done**.

### Tools left behind, all in `.refactor/`

Each refuses rather than guesses, and each exists because a simpler approach
guessed wrong once — the docstrings say which.

| Script | Does |
|---|---|
| `wrap_comments.py` | re-flows comment blocks per UNIT (prose / list item / `// ignore:` copied byte for byte); measures in UTF-16 code units. **The merge of three earlier wrappers** — do not fork a fourth, add a unit kind |
| `split_long_strings.py` | splits a literal into two adjacent literals, which Dart concatenates at compile time |
| `lift_trailing_comments.py` | moves a trailing `// note` off a declaration into a `///` above — the one shape no wrapper can touch |
| `collapse_refresh_pairs.py` | hand-nested `SimfPullToRefresh`+`SimfPullableHost` -> `SimfRefreshableMessage` |
| `screen_header_fields.py` | derives the section 9 `Route:`/`Data:`/`Perf:` fields from the code |
| `token_audit.py` | counts real `SimfTokens` use — **both** the qualified and the bare-inside-tokens.dart form |
| `drop_dead_tokens.py` | deletes a verified-unused token and its doc block |

### BLOCKED

`sign_up_visitor_screen` (1228) and `register_visitor_screen` (1265). Decision 1
reopened them, but D-666 is the banked case where a green golden did NOT catch a
face-capture regression, so they need on-device verification and `adb devices`
is empty (re-checked 2026-08-14). Everything else in Phase A is done. Until they
are split, SIMF-C3 stays at 11 — all 11 rows are `_build*()` helpers inside
those two files — and the pipeline cannot move to `--check --strict` or delete
`tool/conventions/baseline.json`.

Also still owner-gated, unchanged: the `getMySeat` deletion and the
`more_screen` adjudication (deletion needs owner confirmation, global rule 7).

**Two more went to the device list on 2026-08-14 (D-886), both because no test
can catch getting them wrong:**

* Sizing the 11 real `Image.network` call sites. Widget tests have no HTTP, so
  those images render their `errorBuilder` and a golden stays green whatever
  the decode resolution — a visual-only failure mode behind a green golden,
  which is the D-666 class exactly.
* The 4 `EdgeInsets.only(left:)` sites. Each pairs with an explicitly physical
  `Alignment.centerLeft` on the same back button, so converting the padding
  alone leaves the two disagreeing. Whether the back chevron belongs on the
  physical left in Arabic is a Figma question — section 13.5 says ask, never
  guess — and the Arabic goldens currently agree with what ships.

### Branch state (2026-08-14)

`origin/main` has **already merged this branch** (PR 334), so every code commit
listed above is in main. `origin/main` is then ~105 commits ahead with other
work, and this branch carries one commit main lacks (the D-246 docs). Someone
else's `600d22dd merge: bring main into refactor/app-clean-code-3, and re-close
the analyzer ratchet` is on main — read it before merging main in here, because
it means the ratchet has been re-closed once already after a merge.

---

## Program state (supersedes older handoffs)

### 2026-07-28 re-audit — the "code-complete" claim below is STALE

A 15-agent read-only audit of all 41 feature folders found **644 standards
violations across 245 files (175 high)**. 80 new Dart files and 6 new screens
landed after this branch closed, and the frozen state drifted. Corrections to
what is written further down:

- **Pull-to-refresh had regressed to 25 of 69 screens**, not "repo-wide". The
  2026-06-28 grep audit was wrong: `SimfPageShell` only DEFINES
  `SimfPullToRefresh`/`SimfPullableHost` — applying them is opt-in per screen, and
  **Home had none at all**. Restored across all 27 missing screens.
- **The refresh idiom itself was buggy repo-wide.** `ref.invalidate(p); await
  ref.read(p.future);` RETHROWS on a failing endpoint, rejecting the
  RefreshIndicator's future as an unhandled error on top of the error state the
  user can already see. 15 files had it. Use
  **`refreshAsync(ref, p.future)`** (`lib/core/utils/refresh.dart`) — never the
  raw idiom again.
- **`third_party/` was never excluded from analysis**, so 183 vendored errors
  buried anything real in `lib/`. Now excluded; the gate is readable.

Landed 2026-07-28 (branch `feat/badge-profile-type-color`):
`d6829c19` gate hygiene + 8 async-context crash paths + 2 red tests ·
`94c2e0c6` + `d929ad04` pull-to-refresh on all 27 screens ·
`b0ad65fc` refreshAsync retrofit (13 screens) ·
`c551bc0e` zero raw colours (59 -> 0) ·
`d2f8c2c1` 91 inline TextStyles tokenized (exact-match only) ·
`c1194895` news article routed + meeting_confirm / sponsor_detail widget tests.

Gate after: analyze errors+warnings **6 = the pre-existing baseline (all in
test/)**; `flutter test` **1108 pass / 0 fail** (was 1094/2); every golden held
WITHOUT `--update`.

Second run, same day (after the push):
`b207e698` + `7438200b` shell decomposition — `simf_page_shell.dart` **1142 -> 566**,
split into `simf_states` / `simf_refresh` / `simf_cards` / `simf_tiles`, with the
shell re-EXPORTING every group so the ~489 imports across the app never changed ·
`1007b235` all 38 `directives_ordering` violations (scoped `dart fix`) ·
`00560d4a` `SimfTokens.labelDangerSm` (the inline field error was hand-spelled 18x).

**Figma:** node `1116-16448` is **About the FORUM** (`about_screen.dart`) and is
already bound + at parity — verified 2026-07-28 by rendering the node against the
golden. `about_app_screen.dart` (About the APP) is a DIFFERENT screen and is still
unbound, as are `change_email`, `meetings`, `meeting_confirm`, `badge_password`.
Parity check found one open question for the owner: Figma shows **3** main themes,
the app ships **4**. The header language pill is a **documented deliberate
deviation** (owner 2026-07-05, noted in `simf_page_shell.dart`) — do not "fix" it.

Third run (owner items 1+2+3):
`1a326140` **new_request_sheet.dart DELETED** (D-780) — the D-703 orphan, +14 dead
l10n strings. Backend endpoints for document/badge creation are now unreachable
from the app: retiring them is a SEPARATE backend decision, do not assume it ·
`97290abb` OTP frame de-duplicated (`OtpSentTo` + `OtpCountdownLine` shared by
change_email / email_otp_verify / sign_up_email_verify) + §9 doc headers on
meeting_confirm / badge_password / about_app ·
`e0a2f7dc` **sign_up_visitor_screen 1714 -> 1389** — four field widgets extracted
(profile-type, organisation type-ahead, plate, place-of-birth).

**sign_up_visitor decomposition — the rule that made it safe:** the four widgets
are PRESENTATION ONLY. The screen kept all 13 controllers, every piece of lookup
state and every `setState`; nothing about state ownership, face capture or submit
moved. Proof is the **144 account behaviour tests** (D-371 lock, D-373 gate + SA
fallback, D-375 retry, load-failure retry) plus the golden — NOT the golden alone.
Reuse that constraint if you take more out of it.
**OPEN:** smoke-test sign-up end to end incl. face capture on the tablet before merge.

**Still open (the honest remainder):**
1. **130 inline TextStyles** left (was 239). A near-match pass (one differing
   property) identified ~85 more that could become a new named token or a
   `.copyWith`; the top remaining shapes are `labelBeigeMedium+color:surface` (7),
   `bodyInkMuted+color:surface` (7), `labelWhiteMediumLg+fontSize:24` (6). Prefer
   a NEW NAMED TOKEN over `base.copyWith(color:)` when the base name would lie
   about the role — that is how labelDangerSm was decided.
2. **Structural decomposition (S1)** — the shell is DONE (1142 -> 566). Still
   oversized: `sign_up_visitor_screen` 1711, `register_visitor_screen` 1075.
   **Do NOT decompose the sign-up screen from goldens alone** — its field builders
   are coupled to 13 controllers and the face-capture path, and this repo already
   banked that a green golden did NOT catch the D-666 face-capture regression.
   That one needs on-device verification of the sign-up flow.
   The barrel-re-export trick used on the shell is the safe pattern to reuse.
3. **S16 duplication** (117 findings / 180 occurrences), **S11** business logic in
   `build()` (41), **S14** missing doc headers (48), **S9** dead code (34, import
   ordering now cleared).
   **OWNER-GATED (§7):** `lib/features/requests/new_request_sheet.dart` — the
   ENTIRE 396-line file is unreachable; `showNewRequestSheet` has zero call sites.
   Reported, NOT deleted — dead-page deletion needs owner confirmation.
4. **Figma nodes MISSING** for `change_email`, `meetings`, `meeting_confirm`,
   `badge_password`, `about_app` — Level 4 is BLOCKED on the owner for these five
   (per §13.5, ask, never guess).
5. Pre-existing: 6 `test/` warnings (4 unused imports + 2 unused params).

**Verification gotcha banked:** `grep -E '^\s+(error|warning)'` silently matches
NOTHING (POSIX ERE has no `\s`). Use `grep -E '^ *(error|warning) - '`. A whole
run of per-module "0 errors" checks in this session was meaningless until caught.


**✅ THE CLEAN-CODE SWEEP IS CODE-COMPLETE (2026-07-04).** Every routed app screen
is now clean-code frozen. All commits are pushed to `origin/refactor/clean-code-2`
(branch 0 ahead, working tree clean). Next decision number: **D-648**.

DONE before this branch: Phase 0 + Module 1 sign_up_visitor (D-546) + 10 account
screens (D-549..D-558) + staff register_visitor (D-559) + SimfFormScaffold cap (D-560).
This branch drove D-597 → D-647 across Waves A–H, then the tail. See the DECISIONS_LOG
for the per-screen record.

## This run (owner directives 2026-07-03 — ONE consolidated plan)

Per page: 6-level loop **+ pixel-by-pixel Figma overlay compare on EVERY page**
(including re-verifying the 13 frozen; node ids from docs/pages/FIGMA-NODE-MAP.md;
missing node → ASK, never guess) **+ Level F functional completeness** (every button
wired, every value repo-backed → API; missing APIs built FastEndpoints-style per
SpeakerEndpoints.cs; EF additive-only, flagged per migration, Identity frozen, wire
contract append-only) + docs rename App/Page_NNN → real-named docs/pages/mobile/<slug>/
+ shared Simf* widget extraction (never page-local copies) + review-agents+simplify per
page + targeted-pathspec commit per page.

Wave order: A sessions (7) → B home → C live+questions+comments → D myarea+speakers →
E ai_summary+requests+contacts (ASK owner for contact node ids) → F venuemap gates
archive booths sponsorship → G moderation delegations notifications registration →
H long tail → de-dup sweep + unused-page report (owner confirm before delete).

## Final state (2026-07-04, refactor/clean-code-2 — all pushed)

- flutter analyze: **0 errors / 0 warnings** in touched files (info-lint baseline is
  the relative-import + line-length codebase idiom).
- flutter test: **739/739 green** (goldens lock WITHOUT --update).
- Metrics close-out: every file >400 lines + all 4 remaining raw `Color(0x)` in
  `features/` are in **already-frozen** territory — the account/staff cluster
  (D-546..D-560), the `session_models.dart` data file, the shared
  `entity_detail_scaffold`, and the home widgets (D-602). Per **freeze-after-done**
  these are NOT re-opened. **No un-frozen screen carries decomposition or
  tokenisation debt.**

## Sweep complete — Waves A–H + tail (D-597 → D-647)

Every screen frozen. Highlights of the final tail (D-638 → D-647, this session):
forum_guide · terms (surfaceTint token + shared SimfErrorState) · accessibility
(new `SimfTokens.labelWhiteMedium`) · splash (logo-precache golden) · my_visitors +
my_contacts (RefreshIndicator→SimfPullToRefresh) · scan_visitor + scan_contact
(golden `enableCamera:false`) · guest_mode · share_my_contact. All committed
per-screen with docs (PAGE-INDEX + `mobile/<slug>/` + DECISIONS_LOG) and pushed.

### Reusable lessons banked this program
- **Baseline-then-hold golden** proves a structural swap is byte-identical: capture
  the CURRENT render first, refactor, then run the golden WITHOUT --update (a HOLD).
- **`Image.asset` PNGs render EMPTY in goldens** under a bare pump → `precacheImage`
  inside `tester.runAsync` (context captured AFTER a settling `pump()`, outside
  runAsync), then pump to paint. First used on the splash logo.
- **Timer/boot screens** (splash/OTP/live): pin a fixed state and `pump()`, never
  `pumpAndSettle` (the real boot navigates away mid-frame).
- **Camera screens** (scan_*): golden with `enableCamera:false`.
- **Shared error/empty states use WHITE text** — safe on navy/dark scaffolds only;
  on light scaffolds, OR when the local state uses the theme-default text colour and
  you can't prove the swap identical from the golden, KEEP the local state.
- **`FilledButton.styleFrom(textStyle:)` drops the brand fontFamily → Arabic tofu.**
  An `--update` golden silently LOCKS the tofu — goldens must be READ, not just
  regenerated. (Fixed on live/send-question earlier in the program.)
- **`Icons.chevron_left` carries matchTextDirection → double-mirrors under RTL** —
  use `SimfSvgIcon ic_back.svg` for forward chevrons.

### Remaining (optional, owner call)
- **Open a PR** for `refactor/clean-code-2` → `feature/centralized-file-store`
  (or the intended target) when ready.
- **Flagged pre-existing gaps** (NOT introduced by the sweep): `my_visitors` +
  `scan_visitor` (D-426) have no E2E catalogue file under `docs/tests/e2e/`;
  `scan_visitor` also has no widget test. Authoring these is a DoD gap tracked here.
- **De-dup report:** because every screen is now frozen, any remaining cross-screen
  duplication can only be REPORTED, not fixed (fixing would re-open a freeze) —
  surface to the owner rather than retro-DRY.

## Gotchas carried forward

- `_next()` returns **void** — pass `onNext: _next`, never `unawaited(_next())`.
- Timer screens (OTP/live): `pump()`, never `pumpAndSettle`.
- Golden MUST set `theme: SimfTheme.dark()` + FontLoader harness (`golden_fonts.dart`)
  or Arabic renders tofu; frame size = exact Figma node size.
- Figma metadata-x RTL-inverts — trust the RENDER overlay, never metadata side-claims.
- New widget files keep **relative imports** (codebase convention).
- Frozen siblings keep local widget copies — extract shared for NEW screens, don't
  unfreeze to retro-DRY (unless the L4 overlay finds a real Figma mismatch).
- FastEndpoints: RoutePrefix is `api/v1` — use RELATIVE routes (D-568 double-prefix 404).
- simf_auth_pkg signUp test failure = pre-existing baseline (NOT a regression).

## Backend hand-off (owner routes backend items here; a separate session owns the backend)

- **D-609 (2026-07-04) — My-meetings / My-sessions / Saved-sessions removed from the app:
  NO backend action required.** The removal was app-side only (screens `.bk`-backed
  up, routes → ComingSoon, My-Area tiles + More row deleted). All three endpoints stay
  **in use by other app screens**, so none is orphaned:
  - `GET /app/my-requests` — still powers the **RequestsScreen** (only the read-only
    my-meetings *view* over it was removed).
  - `GET /app/account/sessions` — still powers the **AI session-summaries list**
    (`session_summary_list_screen` reads `mySessionsProvider`).
  - `GET /app/sessions/favourites` — still powers the **favourite hearts** across the
    sessions module.
  If the product later wants these features fully retired backend-side, that is a
  separate owner decision — flag it; do not drop the endpoints on the strength of the
  app removal alone.
- (D-605 audience-comments backend teardown remains outstanding for the backend session —
  destructive drop-table migration on the frozen schema + CP moderation removal.)
