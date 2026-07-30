# Clean-code refactor — session hand-off

**Branch `refactor/clean-code-2`** (off `feature/centralized-file-store` tip 283ef215,
pushed to origin 2026-07-03; Region fix `981ebee3` cherry-picked as def32866 so App
integration tests can run). Worktree **D:/SIMF/wt-app**. Toolchain: run flutter from
`src/Mobile/simf_app`; **never `git add -A`, never `dart format`**; gate = analyze
0 errors/warnings (2123 info baseline) + full suite green + goldens WITHOUT --update.

Full plan: `C:\Users\LOQ\.claude\plans\based-on-app-clean-prancy-pudding.md` (owner-approved
2026-07-03). Program docs: `~/.claude/skills/clean-code-skills/resources/`.

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
