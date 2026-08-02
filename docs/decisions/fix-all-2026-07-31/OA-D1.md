# OA-D1 — the Home greeting truncates Arabic compound given names

Item ref: `OA-D1` (Track D-b, fix-all run 2026-07-30).
Files touched:
`src/Mobile/simf_app/lib/features/home/widgets/greeting_header.dart` ·
`src/Mobile/simf_app/test/features/home/widgets/greeting_header_test.dart` (new) ·
`docs/tests/e2e/mobile-home.md` · `docs/pages/mobile/home/README.md`.

## DECISIONS_LOG

### D-NEXT — OA-D1: the Home greeting renders the full trimmed name, not the first space-delimited token

`GreetingHeader` computed `name.trim().split(' ').first` and greeted that. The
split implemented an owner instruction from 2026-07-21 ("first name only"), but
it had no way to know where an Arabic given name ends:

- `عبد الله` greeted as `عبد`
- `عبد الرحمن` greeted as `عبد`
- `أبو بكر` greeted as `أبو`
- any record stored family-name-first greeted the **wrong** name entirely

**Decision (owner Q3, 2026-07-30): greet the full trimmed name.** The split is
removed. The `Text` already carries `maxLines: 1` + `TextOverflow.ellipsis`, so
a long name degrades gracefully instead of wrapping or overflowing, and the
header height is unchanged.

**Why not a prefix heuristic.** Special-casing `عبد …` / `أبو …` / `عبد الـ…`
is an open-ended list (عبد + any of the 99 names, بنت, ابن, آل, أم …) and still
guesses. The durable fix is a captured `GivenName` field at sign-up; that is a
schema + registration-form change, not a greeting change, and nothing in the
codebase captures one today (a repo-wide grep for `givenName` / `firstName`
returns only these two lines). Removing a wrong guess is strictly better than
refining it.

**Golden impact.** `test/golden/home_golden_test.dart` renders the signed-in
Home with the fixture name `أحمد محمد`, which previously drew `أحمد 👋` and now
draws `أحمد محمد 👋`. `goldens/home_signed_in_758-1134.png` **must be re-locked**
with `flutter test --update-goldens test/golden/home_golden_test.dart`. This run
could not do it (build/test execution is the orchestrator's step), so it is
carried as an explicit follow-up rather than left to surprise the next reader.

**Tests:** `greeting_header_test.dart` — six cases: عبد الله, عبد الرحمن السالم,
a Latin multi-part name, whitespace trimming, the blank-name wave-only case, and
the `maxLines: 1` + ellipsis guarantee for a very long compound name. Each of the
first three fails against the old split.

## PAGE-INDEX

Replace the `#13 home` row (line ~247) with:

| #13 `home` (`GET …/notifications/unread-count`, signed-in only; `/app/bootstrap` is built but unused by the app) | ✅ Real — Figma 758:1134/2910; **clean-code frozen (D-602)** — 1271→111-line screen + 9 widgets, goldens both states. **OA-D1 (2026-07-30):** the greeting renders the FULL trimmed name (the old first-token split amputated every Arabic compound given name) | Guest+ | [mobile/home/](mobile/home/README.md) _(legacy: [App/Page_013](../App/Page_013/README.md))_ | [e2e/mobile-home.md](../tests/e2e/mobile-home.md) |

## E2E-README

Replace **both** `#13 home` rows (lines ~243 and ~244 — the file carries a
duplicate) with the single row:

| #13 `home` (`GET /app/bootstrap`; Moderator home also `GET /app/sessions/moderated` — FR-MOD-001 جلساتي) | [`mobile-home.md`](mobile-home.md) | E2E-MOB013-001..026 |

**Roll-up:** this item adds **+1** Coverage-matrix row (`E2E-MOB013-026`).
`E2eCatalogueIntegrityTests.The_index_roll_up_matches_the_catalogue_it_describes`
asserts `**Total scenarios:** N` equals the real row count, so bump it by 1 when
merging (Track D-b contributes **+10** across its four files in total; see the
other `docs/_pending/*.md` from this track).
