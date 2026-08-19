# SIMF convention checker

Enforces the rules in `docs/SIMF-CQP-001-Code-Quality-Programme.md` section 6.

It detects the code-quality classes that no Dart lint can see: magic numbers,
hardcoded endpoint paths, private widget placement, un-localized strings,
bundled asset paths, models declared inside repository files, raw form controls,
Razor inline styles and raw hex colours.

## Running it

From this directory:

```
dart pub get

dart run bin/simf_conventions.dart                 # print the report
dart run bin/simf_conventions.dart --out FILE      # write the report
dart run bin/simf_conventions.dart --check         # the CI gate
dart run bin/simf_conventions.dart --check --strict  # the Wave 6 end state
dart run bin/simf_conventions.dart --write-baseline  # re-record the baseline
```

Run `--check` before every delivery. The report is written in the same shape as
an external code review, so it can be read and handed over directly.

## The baseline

`baseline.json` records the findings tolerated today. `--check` fails only on a
finding that is NOT in it, so a newly introduced violation fails the build on
the commit that introduced it.

It now holds **one entry**, re-measured 2026-08-18 by running
`dart run bin/simf_conventions.dart` against the tree: `_buildBody()` in
`src/Mobile/simf_app/lib/features/account/sign_up_visitor_screen.dart`. Every
other rule the checker implements — SIMF-C1, C2, C4, C5, C6, C7 and the three
text rules SIMF-N1, N2, N3 — reports zero, so for those the baseline is
already equivalent to `--strict`.

It held **12 entries**, all `_build*` methods in three large screens, from the
Wave 6 recording until the 2026-08 clean-code round; the argument for tolerating
those, and the measurement behind it, is in SIMF-CQP-001 section 10.1.

**Nothing is ever added to the baseline by hand.** Re-record it with
`--write-baseline` only after a change has genuinely reduced the count, and
review the resulting diff: the diff should only ever remove entries.

## What still blocks `--check --strict`

Exactly one finding. Quoted from the report as it prints today:

```
Issue file : src/Mobile/simf_app/lib/features/account/sign_up_visitor_screen.dart
Issue : _buildBody() returning Widget in a 876-line file (limit 400)  (line 614, SIMF-C3)
Fix : split the file; move this and its state into a widget
```

`--check` prints `PASS: no NEW convention violations (1 pre-existing, tracked in
the baseline)` and exits **0**. `--check --strict` prints
`FAIL: 1 convention violations (strict mode)` and exits **1**. Both re-run
unpiped on 2026-08-18 — `cmd | tail; echo $?` reports `tail`'s status, not the
checker's, which is how an earlier read of these exit codes came out wrong.

**Why it was left rather than forced.** SIMF-C3's `_build*` leg only fires in a
file over 400 lines, so the finding is really the file, not the method: the
screen is 875 lines (`wc -l`; the checker counts 876). The 2026-08 round split
it from 1213 down to 875 by lifting out the parts a golden can prove. What
remains inside `_buildBody` is the form itself, wired to the screen's controllers
and to the face-capture path — and D-666 is this repo's banked case of a green
golden failing to catch a face-capture regression. Splitting further therefore
needs a sign-up run verified on a device, and no device was attached. Forcing
the split to clear a gate is exactly how that regression happens a second time,
so the entry stays in the baseline with this paragraph as its reason.

When that screen is split with on-device verification, delete `baseline.json`
and change the pipeline step to `--check --strict`.

## Design notes

The checker is a separate package with its own lock file. The application pins
`analyzer` transitively through `very_good_analysis`, and sharing one manifest
would make every analyzer upgrade a negotiation between the linter and the code
it lints.

Parsing is syntax only, without type resolution. That keeps a full scan to a few
seconds and lets the checker run without resolving the application's
dependencies. Two consequences follow, and both are load bearing:

1. An endpoint path inside a documentation comment does not produce a finding.
   A text search based checker reports every repository file, because each one
   carries `/// GET /app/...` in its header.
2. `TextFormField(...)` parses as a method invocation rather than an object
   creation, because only the `new` and `const` forms produce the latter. Rules
   must handle both node types. Getting this wrong made rule C7 report zero
   findings across the whole repository; see the regression tests in
   `test/conventions_test.dart`.

## Adding a rule

1. Add the detection to `lib/src/dart_rules.dart` or `lib/src/text_rules.dart`.
2. Add a `Remedy` constant naming where the value belongs.
3. Add tests, including a case that must NOT fire.
4. Re-record the baseline and review the diff.
5. Add the rule to SIMF-CQP-001 section 6.
