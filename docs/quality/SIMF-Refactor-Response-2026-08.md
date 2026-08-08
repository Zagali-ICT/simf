# Response to the external code review, August 2026

| Field | Value |
|-------|-------|
| Document ID | SIMF-CQR-001 |
| Title | Response to the external code review, August 2026 |
| Version | 1.0 |
| Status | Complete, pending owner approval |
| Classification | Confidential, to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team |
| Owner | Solution Architect |
| Date issued | 2026-08-08 |
| Related documents | SIMF-CQP-001, SIMF-MAA-001, DECISIONS_LOG (D-545, D-219) |

### Revision history

| Version | Date | Summary of change |
|---------|------|-------------------|
| 0.1 | 2026-08-08 | Wave 0. Structure, verified counts, and the position taken on each category. |
| 1.0 | 2026-08-08 | Programme complete. 1056 findings to 18, eight of nine rules at zero, the remaining 18 recorded as an argued exception. |

---

## 1. Purpose

The review is accurate and is accepted. This document records, for every
category and item in it, what was found in the code, what was done, and where
our answer differs from the fix the review proposed.

It exists because fixing the code is not by itself a reply. Without a written
position per item, the same list can be raised again against code that has
already been corrected.

## 2. How to read this

Each item carries one of four positions.

| Position | Meaning |
|----------|---------|
| Fixed as proposed | The finding was correct and the proposed fix was applied. |
| Fixed differently | The finding was correct. A different fix was applied, and the reason is stated. |
| Already correct | The finding does not reproduce against the current code. |
| Not accepted | The finding is correct but the proposed fix is rejected, with the governing rule cited. |

## 3. Verification method

Every item was re-checked against the current head rather than accepted as
written, because several file names in the review are stale or misspelled, for
example `booths_comapny_header.dart`, `contact_empty_satae.dart`,
`share_my_cintact_screen.dart` and `deledation_stats_strip.dart`.

The verification is now automated. `tool/conventions` reproduces the review
mechanically and is run before every delivery. Its current output is in
`docs/quality/convention-report.md`.

## 4. Findings and outcome

| Category | Rule | Found | Now | Position |
|----------|------|-------|-----|----------|
| Raw numeric literals | SIMF-C1 | 599 | **0** | Fixed, partly differently, see 5.1 |
| Hardcoded endpoint and asset URLs | SIMF-C2 | 123 | **0** | Fixed differently, see 5.2 |
| Private widgets and build methods | SIMF-C3 | 192 | 18 | Fixed as proposed, see 5.3 and 5.6 |
| Hardcoded user facing strings | SIMF-C4 | 6 | **0** | Fixed as proposed |
| Hardcoded bundled asset paths | SIMF-C5 | 31 | **0** | Fixed as proposed |
| Models inside repository files | SIMF-C6 | 9 | **0** | Fixed as proposed |
| Raw form controls | SIMF-C7 | 12 | **0** | Fixed as proposed, see 5.4 |

The review covered the mobile application. The same programme additionally
covers the Control Panel and Website, which were not in the review: 17 inline
style attributes (SIMF-N1, now 0) and 67 raw hex colours outside the token
stylesheet (SIMF-N2, now 0).

**1056 findings at the start, 18 now.** Every private widget class named in the
review has its own file. The 18 remaining are `_build*` methods in five large
screens, held as an argued exception rather than forced to zero; section 5.6
explains why, and SIMF-CQP-001 section 10.1 records it in full.

Every change was verified the same way: the full application test suite green
including every golden image, so the work is behaviour preserving and pixel
preserving. The suite grew from 1351 tests to 1379, because several of the moves
made rules testable that previously were not.

## 5. Positions taken

### 5.1 Numeric literals: agreed, but not all into the token file

The review prescribes `simfTokens` for every numeric literal. That is correct
for design quantities and is applied. It is not accepted for four categories,
because applying it there would reduce quality rather than improve it.

| Literal | Our location | Reason |
|---------|--------------|--------|
| `maxLength` | `core/validation/field_limits.dart` | The value must mirror the backend FluentValidation `MaximumLength(N)` and Entity Framework `HasMaxLength(N)`. Holding a validation contract in a design token file breaks the alignment rule that keeps the three in step. The backend was checked field by field, and the sorted set of limits was proven identical before and after the move. |
| `Duration` | `core/motion/motion_durations.dart`, split into `MotionDurations` and `TimeoutPolicy` | An animation length and a deadline are different things: exceeding a deadline is a failure path, not an effect. Neither has design meaning. |
| `maxLines`, `minLines` | Left as written | `maxLines: 2` already states its own meaning. `TextClamp.cardTitle` adds a hop and no information. |
| `crossAxisCount` | Raised as a separate proposal | Deriving the column count from the breakpoint is the right answer AND it changes the layout on a tablet. That is a design decision, so it is put to the owner rather than made inside a cleanup pass. |

Tokens created by this work are named for their role rather than their value.

### 5.2 Endpoints: the objective is met, the single central file is not adopted

The review asks for one central `api_endpoint.dart`. This is not adopted.
SIMF-MAA-001 sections 5, 6 and 9.1, recorded as decision D-545, state that the
repository owns its endpoint path. A single central file would make every
feature depend on a file that changes whenever any unrelated feature changes.

The objective behind the finding, that no endpoint literal appears at a call
site, is met in full:

* each feature declares its own `*_endpoints.dart` beside its repository;
* one shared `core/net/asset_urls.dart` builds the asset URL family.

The second point addresses something the review identified only in part. The
expression `$baseUrl/app/assets/{Kind}/{id}/image` was written out at 18
separate sites across 14 files, with `SpeakerPhoto` alone repeated 6 times.
That duplication is removed.

Endpoint paths are copied without alteration. A changed path is a live outage
for installed application builds, so the wire contract under D-219 is verified
unchanged for every file touched.

### 5.3 Private widgets: accepted in full, with corrected names

All 192 findings are extracted. Two points of detail.

The engineering rules previously permitted a private helper widget under 60
lines used once. That exemption conflicted with this finding, so it has been
removed from `simf_app/CLAUDE.md` in the same change, and the rule and the code
now agree.

The new files use descriptive widget names rather than the names proposed in
the review. `_buildContent` in the biometric setup screen becomes
`BiometricSetupContent`, not `biometric_build_content.dart`. A file named after
the method that used to build it describes the old structure rather than the
thing itself. Where the review proposed a name containing a typo, such as
`attch_box.dart` or `booths_logo_title.dart` for a tile, the corrected spelling
is used.

### 5.4 Raw form controls: confirmed, and wider than reported

The review identified `TextFormField` in the badge activation and badge
password screens. Both are confirmed. The automated check found 12 occurrences
across 8 files, including `account_form_field.dart`, `mobile_field.dart`,
`place_of_birth_field.dart`, `plate_number_field.dart`, `contact_field.dart`
and `simf_labeled_text_field.dart`. All are addressed.

### 5.5 Items that do not reproduce

Some findings do not reproduce against the current code. `about_cards.dart` is
cited for a hardcoded font size of 22. That file uses design tokens throughout
and contains no numeric literal. Its only valid finding is the private widget
one, which is accepted. Items in this class are listed per wave in section 6
with the position "Already correct".

### 5.6 The 18 findings not forced to zero

All 18 are `_build*` methods in five screens. Every one reads instance state,
so converting it to a widget means passing that state through a constructor.
The cost was measured, not estimated: `_buildOrganisationField` reads 9
distinct pieces of state, `_buildProfileTypeField` and `_buildPlateField` 8
each, several of them callbacks. As widgets they would take eight or nine
constructor parameters, which is the same coupling with more ceremony, and
harder to read than what is there now.

Everything in those screens that genuinely did not belong in a widget has been
taken out, and each move is covered by tests that could not exist before it:
the visitor-profile validators (16 tests), the sign-in validators, the
device-key sign-in flow, and the session-detail eligibility rules (7 tests).

What is left is the screens themselves. `sign_up_visitor_screen` collects
around twenty fields across identity, documents, contact, vehicle and
photographs in a single form. Splitting that into form sections that own their
own state is a redesign of the most important registration flow in the product.
It is worth doing and it needs its own decision and its own verification; doing
it inside a cleanup pass would be the wrong way to change a flow that matters
this much.

The gate holds these 18 at exactly this count, so the number cannot grow while
that decision is taken.

### 5.7 Where our rules were wrong, not the code

Five of the nine rules had to be narrowed during the work, because the blunt
version condemned correct code. This is recorded because a gate that flags
correct code is a gate that gets switched off, and because three of these
narrowings changed the reported total without changing a line of product code.

| Rule | What it wrongly flagged | Corrected to |
|------|------------------------|--------------|
| SIMF-C7 | Field components, for wrapping `TextFormField` | Only screens that hand-roll a field |
| SIMF-N1 | `style="--fill:@Percent"`, a runtime value feeding a CSS custom property | Only static inline styling |
| SIMF-C1 | `const Duration saudiOffset = Duration(hours: 3)`, the named constant the rule asks for | Only inline call-site literals |
| SIMF-N2 | The `:root` token definitions themselves | Only use sites bypassing a token |
| SIMF-C3 | All 78 `_build*` methods | Only those in files over the size limit |

Each narrowing is pinned by a test asserting the rule stays SILENT on the
correct form, so the scope cannot quietly widen again.

## 6. Item level record

Completed per wave. Each row records the review's item, the file verified, the
position taken, and the commit that carries the change.

| Review section | Item | File verified | Position | Evidence |
|----------------|------|---------------|----------|----------|
| About | `_Card`, `_CardHeading` rename | `features/about/widgets/about_cards.dart` | Fixed as proposed | Wave 3 |
| About | hardcoded font size 22 | `features/about/widgets/about_cards.dart` | Already correct | No numeric literal present at head |
| Account | `TextFormField` in badge screens | `features/account/badge_activation_screen.dart`, `badge_password_screen.dart` | Fixed as proposed | Wave 3 |
| Account | hardcoded endpoints | `features/account/data/profile_repository.dart`, `region_repository.dart` | Fixed differently, see 5.2 | Wave 1 |
| Live | hardcoded `العربية` / `English` | `features/live/widgets/live_badges.dart` | Fixed as proposed | Wave 2 |
| Chatbot | hardcoded `AI` | `features/chatbot/widgets/chat_bubble.dart` | Fixed as proposed | Wave 2 |

Remaining rows are added as each wave completes.

## 7. Preventing recurrence

The substantive change is not the cleanup. It is that the review is now
executed by the build rather than by a reader.

Before this work, the only pipeline step touching the application was
`flutter analyze`, which is advisory by design and cannot detect any of the
seven categories in the review. No Dart lint can. The result was that the only
mechanism capable of catching these findings was manual reading.

`tool/conventions` now reproduces the review mechanically, runs as a failing
step in the build pipeline, and is run locally before delivery. The report it
produces uses the same structure as the review, so the two can be compared
directly.
