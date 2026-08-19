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

## The baseline — retired 2026-08-19

There is no `baseline.json` any more. It existed to tolerate findings the
programme had not reached yet: 12 entries at the Wave 6 recording, all `_build*`
methods in three large screens, argued in SIMF-CQP-001 section 10.1. The
clean-code round took it 12 -> 1 as those screens were split, and the last entry
died with the file that carried it.

With the count at zero the file was the weaker gate: `--check` fails only on a
finding absent from the baseline, so a tolerated entry is a place a regression
can hide. `--check --strict` fails on any finding at all, which is what the
pipeline now runs.

**If a violation ever has to be tolerated again**, regenerate the file with
`--write-baseline` — never by hand — and review the diff, which should only ever
remove entries.
## `--check --strict` is the gate, and there is no baseline

Nothing blocks it. `tool/conventions` reports **zero** findings across every rule
in SIMF-CQP-001 section 6, `baseline.json` is **deleted**, and the pipeline step
runs `--check --strict`. A single new violation fails the build; there is no
longer a tolerated set for one to hide in.

The last finding was `_buildBody()` returning a Widget inside
`sign_up_visitor_screen.dart`. SIMF-C3's `_build*` leg only fires above 400
lines, so it cleared when that file went 875 -> 398 (2026-08-19) by moving its
non-widget half - load, apply-profile, submit assembly, lookup fetching - out to
`data/` and a feature-root helper. Two earlier attempts had tried to extract the
method itself and correctly refused, because that needs a 15-18 parameter
constructor; the file was the thing to shrink, not the method.

Verified before this section was written: injecting
`class _Probe extends StatelessWidget` into a screen makes strict mode print
`FAIL: 1 convention violations (strict mode)`, and removing it returns
`PASS: zero convention violations`. Run unpiped - `cmd | tail; echo $?` reports
`tail`'s status, not the checker's, which is how an earlier read of these exit
codes came out wrong.

**If a violation ever has to be tolerated again**, re-record with
`--write-baseline`, never by hand, and review the diff: it should only ever
remove entries.
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
