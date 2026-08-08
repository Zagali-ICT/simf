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

It now holds **14 entries**, all `_build*` methods in three large screens. They
are a deliberate, argued exception, not leftover debt: see SIMF-CQP-001 section
10.1 for the measurement behind the decision. Eight of the nine rules report
zero, so for those the baseline is already equivalent to `--strict`.

**Nothing is ever added to the baseline by hand.** Re-record it with
`--write-baseline` only after a change has genuinely reduced the count, and
review the resulting diff: the diff should only ever remove entries.

When the three screens are split, delete `baseline.json` and change the pipeline
step to `--check --strict`.

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
