# Code Quality Programme and Convention Enforcement

| Field | Value |
|-------|-------|
| Document ID | SIMF-CQP-001 |
| Title | Code Quality Programme and Convention Enforcement |
| Version | 1.0 |
| Status | Draft, pending owner approval |
| Classification | Confidential, to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team |
| Owner | Solution Architect |
| Approver | Solution Architect |
| Date issued | 2026-08-08 |
| Related documents | SIMF-SES-001, SIMF-MAA-001, SIMF-SAD-001, DECISIONS_LOG (D-545, D-694, D-110, D-219) |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-08-08 | Engineering & Architecture Team | First issue. Responds to the external code review of August 2026. |

---

## 1. Purpose

This document does two things.

1. It answers the external code review received in August 2026, item by item,
   with a verified position on each finding.
2. It replaces manual code review as the mechanism that protects code quality,
   by defining an automated convention gate that runs in the build pipeline.

The second point is the substantive one. The review was correct, and a
correct review is not the problem. The problem is that it was produced by a
person reading files, which means quality was being measured after delivery by
the client rather than before delivery by the build.

## 2. Background

The review lists findings across the Flutter application in seven categories:
raw numeric literals, hardcoded API endpoint paths, private widgets that should
live in their own files, hardcoded user facing strings, hardcoded bundled asset
paths, data models declared inside repository files, and use of a raw framework
form control where a shared component exists.

The findings were verified against the current head of `feat/cp-dashboard-reporting`
before this document was written. They are accurate. The file names quoted in the
review are in several cases stale or misspelled, for example `booths_comapny_header.dart`
and `contact_empty_satae.dart`, so each item is re-verified against the real file
rather than accepted as written.

## 3. Root cause

The project does not lack standards. The following already exist and are of good
quality:

* `src/Mobile/simf_app/CLAUDE.md`, thirteen sections of binding rules.
* `lib/app/theme/tokens.dart`, a design token system of 955 lines.
* `lib/app/theme/app_assets.dart`, a bundled asset constant class.
* `analysis_options.yaml`, `very_good_analysis` plus approximately sixty
  hand selected lint rules.

What is almost entirely missing is anything that executes them. One narrow
exception exists and is important, see 3.1. Otherwise the only pipeline step
that touches the application is in `azure-pipelines.yml`:

```
flutter analyze --no-fatal-infos --no-fatal-warnings lib test integration_test packages
```

That step is advisory by deliberate design, and the reason is documented in the
adoption note inside `analysis_options.yaml`. Separately, and more importantly,
it is structurally incapable of detecting any of the seven categories in the
review. No Dart lint rule, whether built in or supplied by `very_good_analysis`,
detects magic numbers, hardcoded endpoint paths, private widget placement,
un-localized strings or asset path literals.

The consequence is that the only mechanism capable of catching these findings was
human reading, and the only person doing that reading was the client. A previous
tokenisation sweep did not prevent this, because that sweep targeted Figma visual
literals and therefore never looked at `maxLength`, `Duration`, `maxLines` or
`crossAxisCount`.

### 3.1 The proof, inside this codebase

One mechanical convention gate already exists:
`test/repo/design_token_ratchet_test.dart`. It pins two rules, that no raw
colour literal appears outside `tokens.dart` and that swept files carry no
inline `TextStyle`, and because it is an ordinary test it already runs in the
pipeline through `flutter test`. Its own header records why it was written:
the rule "held nowhere except by review, so it kept drifting back".

Now compare that against the external review. Across 24 pages it raises **not
one** finding about a raw colour or an inline text style, in a codebase where
those were previously among the most common defects.

Where a mechanical gate exists, the reviewer found nothing. Where none exists,
the same reviewer found more than a thousand. The argument of this document is
therefore not a proposal to be evaluated: it has already been tested here, on
this code, and it worked. This programme generalises that one test into the
remaining categories.

The corrective action is therefore not another cleanup pass. It is to make the
review itself executable.

## 4. Verified baseline

Measured at head, not estimated.

| Finding | Occurrences | Files |
|---------|-------------|-------|
| Private widget classes | 114 | 74 |
| Hardcoded `/app/...` endpoint literals | 71 | 36 |
| Inline `$baseUrl/app/assets/{Kind}/{id}/image` construction | 18 | 14 |
| Bundled asset paths bypassing `AppAssets` | 31 | 11 |
| Inline `style="..."` in Razor components | 17 | 14 |
| Raw hex colours outside `theme.tokens.css` | 63 | 2 |

Counts for raw numeric literals and un-localized strings are produced by the
first run of the checker described in section 6, because no reliable manual
count is possible for those two categories.

## 5. Where a literal belongs

The review prescribes `simfTokens` for every numeric literal. That prescription
is correct for design quantities and incorrect for four other categories.
Applying it literally would degrade the codebase, for two reasons. A token named
after its own value, such as `maxLines2`, is a renamed magic number and carries
no more meaning than the literal it replaced. A validation limit placed in a
design token file breaks the rule that field limits must mirror the backend
FluentValidation and Entity Framework values.

The following taxonomy governs this programme.

| Literal | Correct location | Review prescribed |
|---------|------------------|-------------------|
| Spacing, radius, icon size, opacity, colour, typography | `SimfTokens`, under a semantic name | `simfTokens`, agreed |
| `maxLength` | `core/validation/field_limits.dart`, mirroring backend `MaximumLength(N)` and `HasMaxLength(N)` | `simfTokens`, not accepted |
| `Duration` for animation | `core/motion/motion_durations.dart` (`MotionDurations`) | `simfTokens`, not accepted |
| `Duration` for a deadline | the same file (`TimeoutPolicy`); exceeding one is a failure path, not an effect | `simfTokens`, not accepted |
| `maxLines`, `minLines` | left as written, see 5.1 | `simfTokens`, not accepted |
| `crossAxisCount` | a separate responsive proposal, see 5.2 | `simfTokens`, not accepted |
| API endpoint path | `features/<f>/data/*_endpoints.dart` | Single central file, not accepted, see section 8 |
| Bundled asset path | `AppAssets` | `app_assets.dart`, agreed |
| User facing string | `AppL10n` | Localization, agreed |
| External host such as `youtube.com` | A URL policy constant in `core/` | `simfTokens`, not accepted |

A literal that is ALREADY the value of a named declaration or a parameter
default is not a finding. `const Duration saudiOffset = Duration(hours: 3);`
and `this.tickInterval = const Duration(seconds: 15)` are the named constant
and the named default the rule asks for; flagging them would make the rule
unsatisfiable, because writing exactly that is what resolving a magic number
looks like. 87 of the original 599 were this.

### 5.1 Why `maxLines` is left alone

`maxLines: 2` already states its own meaning: at most two lines. Replacing it
with `TextClamp.cardTitle` adds a hop and no information. A rule earns its
place where the number is opaque, as in `height: 37`, not where the parameter
name already carries the meaning.

### 5.2 Why `crossAxisCount` is a separate proposal

Deriving the column count from `core/responsive/breakpoints.dart` is the right
answer and is worth doing. It also CHANGES the layout on a tablet, which is a
design decision, not a cleanup. Making it inside a refactor wave would be a
visual change disguised as tidying, so it is raised on its own.

Tokens created during this programme are named semantically from the outset.
Renaming the existing value named tokens, for example `gap5`, `radius10` and
`labelGoldBold9`, is not in scope, see section 11.

## 6. Rule catalogue

| Rule | Detects | Permitted location |
|------|---------|--------------------|
| SIMF-C1 | Raw numeric literal in a design or layout property | `tokens.dart` |
| SIMF-C2 | API path or asset URL literal | `*_endpoints.dart`, `asset_urls.dart` |
| SIMF-C3 | Private widget class, or `_build*` method returning a widget | none |
| SIMF-C4 | User facing string literal | `app/localization/` |
| SIMF-C5 | Bundled asset path literal | `app_assets.dart` |
| SIMF-C6 | Model with JSON mapping declared inside a repository file | none |
| SIMF-C7 | Raw `TextFormField` | `navi_form_field.dart` |
| SIMF-N1 | Inline `style="..."` in a Razor component | none |
| SIMF-N2 | Raw hex colour in a stylesheet | `theme.tokens.css` |

SIMF-C1 reports the applicable location from section 5 with each finding, so the
tool communicates the taxonomy rather than assuming the reader knows it.

Numeric values `0` and `1` are permitted everywhere. They are identity and
neutral values, such as `opacity: 0` and `maxLines: 1`, and are not design
decisions. Test sources, generated sources and the vendored `third_party`
plugin are not scanned.

## 7. The checker

The checker is a standalone Dart package at `tool/conventions`, with its own
manifest and lock file. It parses Dart sources with `package:analyzer` and
matches Razor and CSS sources textually.

Three design decisions are recorded here.

**It is a separate package.** The application pins `analyzer` transitively
through `very_good_analysis`. Sharing one manifest would make every analyzer
upgrade a negotiation between the linter and the code it lints.

**It does not use `custom_lint`.** That package pins `analyzer` tightly and
would collide with `very_good_analysis`, and the first party
`analysis_server_plugin` intended to replace it has not shipped. The cost of
this decision is that findings do not appear as editor warnings. The contract is
the command line and the pipeline.

**Parsing is syntax only, without type resolution.** This keeps a full scan to a
few seconds and allows the checker to run without resolving the application's
dependencies. It also means an endpoint path written inside a documentation
comment does not produce a finding, which a text search based checker would
report on every repository file.

## 8. Endpoint architecture

The review asks for a single central `api_endpoint.dart` holding every path.
This is not adopted, because SIMF-MAA-001 sections 5, 6 and 9.1, recorded as
decision D-545, state that the repository owns its endpoint path. A single
central file would make every feature depend on a file that changes whenever any
other feature changes.

The adopted design achieves the review's objective, which is that no endpoint
literal appears at a call site, without breaking the architecture:

* Each feature declares its own `*_endpoints.dart` beside its repository.
* One shared `core/net/asset_urls.dart` builds the asset URL family, removing
  the eighteen site duplication of `$baseUrl/app/assets/{Kind}/{id}/image`.

Endpoint paths are copied without alteration. A changed path is a production
outage for installed application builds, so the wire contract under D-219 is
verified as unchanged for every file touched.

## 9. Delivery waves

Work proceeds one feature per commit. There is no repository wide diff.

| Wave | Content | Gate |
|------|---------|------|
| W0 | Checker, recorded baseline, response document skeleton | Checker self tests pass |
| W1 | Endpoints across 36 files, plus the shared asset URL builder | Wire contract diff empty |
| W2 | Bundled assets and user facing strings | Goldens byte identical |
| W3 | Private widget extraction, 114 sites across 74 files | Goldens byte identical |
| W4 | Numeric literals, routed per section 5 | Goldens byte identical |
| W5 | Razor inline styles and raw hex colours | Live render check |
| W6 | Gate raised to zero, governing documents synchronised | Full suite |

Wave 5 covers rules N1 and N2. Ungated endpoints on the .NET side are already
covered by the existing `PermissionEnforcementTests` and
`CpNavigationPermissionTests`, so no new rule is required and Wave 5 confirms
those still execute. Further .NET rules, covering magic numbers, hardcoded font
families and duplicate `:root` blocks, follow as N3 and later, once the Wave 5
baseline exists and that surface has been measured in the same way the Flutter
surface was.

## 10. Pipeline gate

A new step is added to the existing `MobileApp` stage. It is fatal from the
outset, because it gates against a recorded baseline and the condition "no new
violations" is satisfiable immediately.

The existing `flutter analyze` step is not modified. Raising it to fatal today
would surface several thousand `very_good_analysis` findings, including
approximately 445 relative imports and 526 inline text styles, and the only way
to clear that in one action is the repository wide change that the engineering
rules prohibit. That ratchet remains tracked in `analysis_options.yaml` and is a
separate programme. It is recorded here as a known open item rather than left
unstated.

The gate progresses in three stages: no new violations, then zero violations in
each feature as its wave lands, then zero across the repository at Wave 6.

## 10.1 The remaining 18 findings, and why they are not being forced to zero

Eight of the nine rules report zero. SIMF-C3 reports 18, all of them `_build*`
methods in five screens:

| Screen | Lines | Findings |
|--------|-------|----------|
| `sign_up_visitor_screen.dart` | 1305 | 10 |
| `staff/register_visitor_screen.dart` | 1262 | 3 |
| `sign_up_interests_screen.dart` | 469 | 3 |
| `live_broadcast_screen.dart` | 507 | 1 |
| `session_detail_screen.dart` | 468 | 1 |

Every one of these methods reads instance state. The state each would need as a
widget was measured rather than estimated:

| Method | Distinct pieces of state |
|--------|--------------------------|
| `_buildOrganisationField` | 9 |
| `_buildProfileTypeField` | 8 |
| `_buildPlateField` | 8 |
| `_buildIdImageField` | 6 |

Converting these to widgets means eight or nine constructor parameters each,
several of them callbacks. That satisfies the rule and makes the code harder to
read: the same coupling, expressed with more ceremony. It would be a change made
for the metric rather than for the reader.

What has been taken out of these screens is everything that genuinely did not
belong in a widget, and each move was verified by tests that could not exist
before it:

* the 8 visitor-profile validators, now pure functions with 16 tests;
* the sign-in validators, with the load-bearing rule that sign-in does NOT
  apply the sign-up password policy, pinned by a test;
* the 100-line device-key sign-in flow;
* the session-detail eligibility rules, with 7 tests covering the defect ids
  they were filed under.

What remains is the screens themselves. `sign_up_visitor_screen` collects around
twenty fields spanning identity, documents, contact, vehicle and photographs in
one form. The honest fix is to split it into form sections that own their own
state, which is a redesign of the highest-traffic registration flow in the
product, not a refactor. It needs its own decision, its own plan and its own
verification.

These 18 are therefore recorded as a **known, argued exception** rather than
churned to zero. The gate holds them at exactly this count, so the number cannot
grow quietly while the redesign is decided.

## 11. Out of scope

* Renaming existing value named tokens. The review did not raise it, and
  `tokens.dart` is on the D-694 blast radius list. This requires a separate
  owner decision.
* The D-110 schema and enumeration freeze, and the D-219 wire contract.
* State management and data layer architecture. These are refined, not
  redesigned.
* The vendored `third_party` plugin and vendored stylesheets.

Reported separately and not acted upon in this programme: the .NET test stage in
`azure-pipelines.yml` is disabled, so no backend test currently gates the
pipeline. This is the same class of problem as the one this document addresses
and warrants its own decision.

## 12. Definition of done

The programme is complete when all of the following hold.

1. `dart run tool/conventions` reports zero violations.
2. That command is a fatal step in the pipeline.
3. `docs/quality/SIMF-Refactor-Response-2026-08.md` answers every item in the
   external review with a verified position and supporting evidence.
4. The governing documents, `simf_app/CLAUDE.md` and the decisions log, match
   the code.

Each wave is verified by the application test suite, the analyzer reporting no
new issues, a strictly decreasing violation count, and byte identical goldens,
since every wave is behaviour preserving and pixel preserving. Waves touching
shared foundations additionally run the role and route matrix test and the
application flow tests, because goldens prove rendering and not navigation.

## 13. References

* custom_lint package, https://pub.dev/packages/custom_lint
* Dart custom lint repository, https://github.com/invertase/dart_custom_lint
* Migrating to the Dart analyzer plugin system, https://leancode.co/blog/migrating-to-dart-analyzer-plugin-system
* Naming design tokens, https://specifyapp.com/blog/crafting-consistency-a-thoughtful-approach-for-naming-design-tokens
* Design token naming best practices, https://www.netguru.com/blog/design-token-naming-best-practices
* Authoring design tokens, GitLab Pajamas, https://design.gitlab.com/product-foundations/design-tokens-authoring/
