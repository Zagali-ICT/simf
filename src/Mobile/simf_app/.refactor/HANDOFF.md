# Clean-code refactor — session hand-off

**Branch `refactor/clean-code-2`** (off `feature/centralized-file-store` tip 283ef215,
pushed to origin 2026-07-03; Region fix `981ebee3` cherry-picked as def32866 so App
integration tests can run). Worktree **D:/SIMF/wt-app**. Toolchain: run flutter from
`src/Mobile/simf_app`; **never `git add -A`, never `dart format`**; gate = analyze
0 errors/warnings (2123 info baseline) + full suite green + goldens WITHOUT --update.

Full plan: `C:\Users\LOQ\.claude\plans\based-on-app-clean-prancy-pudding.md` (owner-approved
2026-07-03). Program docs: `~/.claude/skills/clean-code-skills/resources/`.

## Program state (supersedes older handoffs)

DONE before this branch: Phase 0 (Simf* shell rename, core/responsive, lint base, faq
pilot) + Module 1 sign_up_visitor (D-546) + all 10 account screens (D-549..D-558) +
staff register_visitor (D-559) + SimfFormScaffold cap 560 (D-560). 13 pages FROZEN,
~54 remaining. Next decision number: **D-597**.

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

## Baseline (captured 2026-07-03 on refactor/clean-code-2)

- flutter analyze: 2123 issues, **0 errors / 0 warnings** (all info)
- flutter test: **749/749 green** (29 goldens lock without --update)
- dotnet build -c Release SIMF.slnx: **0 warnings / 0 errors** (31.8s)
- Metrics: raw colors excl tokens **20** · inline TextStyle **527** · maxWidth caps **18**
  · files >400 lines **40** · biggest: sign_up_visitor 1571 (frozen), session_detail 1375,
  live_broadcast 1286, home 1268

## NEXT — Wave B: home

**WAVE A COMPLETE (D-597..D-601):** session_detail (0ec0d78d) · sessions
(4dc5cc36) · saved_sessions (67daf291 — its Figma node 1701:8928 was DELETED
from the file, golden is a render-lock; ditto my-meetings 1701:9406 for Wave E
— flag to owner) · my_seat+seat_picker (77d8225c — shared `HallSeatMapCard`,
my_seat overlay-verified vs 898:2873) · join_session_hub (D-601 — added
missing pull-to-refresh; fixed the RTL chevron double-mirror
[Icons.chevron_left carries matchTextDirection → use SimfSvgIcon ic_back.svg
for forward chevrons]; render-lock golden).
Next: **Wave B home_screen (1,268 lines, nodes 758:1134 signed-in /
758:2910 guest)** — decompose to widgets/, SimfSectionHeader exists in the
shell (REUSE, don't recreate), SimfNewsCard teaser candidate, L4 overlay both
states. Then Wave C live+questions+comments.

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
