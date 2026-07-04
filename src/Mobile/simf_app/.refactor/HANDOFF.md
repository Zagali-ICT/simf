# Clean-code refactor — session hand-off

**Branch `refactor/clean-code-2`** (off `feature/centralized-file-store` tip 283ef215,
pushed to origin 2026-07-03; Region fix `981ebee3` cherry-picked as def32866 so App
integration tests can run). Worktree **D:/SIMF/wt-app**. Toolchain: run flutter from
`src/Mobile/simf_app`; **never `git add -A`, never `dart format`**; gate = analyze
0 errors/warnings (2123 info baseline) + full suite green + goldens WITHOUT --update.

Full plan: `C:\Users\LOQ\.claude\plans\based-on-app-clean-prancy-pudding.md` (owner-approved
2026-07-03). Program docs: `~/.claude/skills/clean-code-skills/resources/`.

## Program state (supersedes older handoffs)

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
