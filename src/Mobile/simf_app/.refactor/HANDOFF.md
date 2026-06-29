# Clean-code refactor — session hand-off

Branch `refactor/clean-code` (off `feature/app-cp-api-split` tip, tag
`refactor-baseline`). **Nothing pushed.** Working tree clean (the untracked
`lib/lib.rar` is a pre-existing stray — never stage it). Toolchain: run flutter
from `src/Mobile/simf_app`; **never `git add -A`, never `dart format`**; gate =
analyze 0 errors + the touched module's tests green.

## Done (commits, newest last)
| SHA | What |
|---|---|
| c83de15d | Phase 0a–0d: very_good@warning (0 errors, 1862 baseline), core/responsive, token audit |
| 07f1b452 | Phase 0e: Ksa*→Simf* rename (behaviour-frozen; 14 goldens identical; 676 green) |
| acbb20e4 | Phase 0f: census + parity ledger |
| 542db48f | Node-map merge from the App-Pages .docx (faq/profile/staff/gates/moderation/… bound) |
| 1eb9955b | Phase 0g: faq pilot (DoD-complete; golden faq_1388-7567.png) |
| 3e2b4b0b | Module 1 Slice A: 5 leaf widgets (field_label, beige_tabs, radio_pill, lookup_search_sheet) |
| 07968432 | Module 1 Slice B(1/3): complete_profile_notice, terms_and_next_buttons |

`sign_up_visitor_screen.dart`: **2,245 → 1,941 lines**. Profile tests **49/49 green**.

## NEXT — Module 1 Slice B batch 2 (resume here)
1. **Prerequisite:** extract the input-style helper from the State —
   `_inputStyle`, `_restingBorder`, `_focusedBorder`, `_fieldDecoration()`
   (orig lines ~1917–1956) → `features/profile/widgets/profile_field_style.dart`
   (or a top-level helper). The input-field widgets below all depend on it.
2. Then extract the 11 field widgets (stateless, receive controllers/state +
   callbacks; State keeps the logic): `name_fields`, `gender_pills_field`
   (uses RadioPill), `nationality_field`, `document_fields`, `mobile_field`,
   `date_of_birth_field`, `place_of_birth_field`, `profile_type_field`,
   `id_image_field`, `face_photo_field`, `sign_up_visitor_header`
   (needs `_buildHeaderAvatar` → pass face bytes/hasAvatar). See the full
   line-referenced map in this session's Explore output / the plan.
3. **Slice C (critical-risk):** `plate_field` (sync/parse logic — add unit tests
   for `_setPlateFromCode`/`_syncPlate`) + `organisation_field` (debounce stays
   in State; widget is pure display). Each its own commit + targeted test.
4. **Levels 3/4/5:** remove `maxWidth:400` (line ~750) for flexible width; verify
   vs Figma `168:2972` (MCP) + tokenise the 26 inline TextStyles (incremental,
   per the per-screen typography rule) + 2 raw colors (sweepTint, whitePill90);
   per-page doc folder `docs/pages/mobile/sign-up-visitor/` + E2E; freeze.

## Gotchas hit
- `_next()` returns **void** (not Future) — pass `onNext: _next`, never `unawaited(_next())`.
- `OutlineInputBorder` default borderRadius == `_radius4` (circular 4) — drop redundant `borderRadius:`.
- New widget files keep **relative imports** (match the codebase's 445; `always_use_package_imports` is the deferred ratchet rule).
- `sed -i` flips EOL to LF but `core.autocrlf=true` normalises on commit (no churn in the commit).
