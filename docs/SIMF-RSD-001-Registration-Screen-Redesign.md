# Registration Screens: Structural Redesign Scope

| Field | Value |
|-------|-------|
| Document ID | SIMF-RSD-001 |
| Title | Registration Screens: Structural Redesign Scope |
| Version | 1.1 |
| Status | In progress. Step 1 done; steps 2 to 6 pending owner approval. |
| Classification | Confidential, to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team |
| Owner | Solution Architect |
| Date issued | 2026-08-09 |
| Related documents | SIMF-CQP-001 section 10.1, SIMF-MAA-001, CLAUDE.md section 1, DECISIONS_LOG (D-219, D-545, D-694) |

---

## 1. Why this document exists

The August 2026 code-quality programme closed 1042 of 1056 convention findings.
The remaining 14 are `_build*` methods in two screens:

| Screen | Lines | Findings |
|--------|-------|----------|
| `features/account/sign_up_visitor_screen.dart` | 1304 | 10 |
| `features/staff/register_visitor_screen.dart` | 1262 | 3 |
| `features/sessions/session_detail_screen.dart` | 467 | 1 |

They were not forced to zero, and this document explains what closing them
properly would involve so the owner can decide whether it is worth doing.

**Step 1 (the no-behaviour-change move) is DONE.** Steps 2 to 6 change how
state is owned and are not started; each needs its own go-ahead.

## 2. Why the mechanical fix was refused

The obvious move is to turn each `_build*` method into a widget. That was
measured rather than assumed, and it makes the code worse.

| Method | Body | State it reads |
|--------|------|----------------|
| `_buildOrganisationField` | 17 lines | 9 |
| `_buildProfileTypeField` | 16 lines | 8 |
| `_buildPlateField` | 13 lines | 8 |
| `_buildIdImageField` | 15 lines | 6 |

A 15-line method becomes a widget with eight or nine constructor parameters,
several of them callbacks. That is the same coupling expressed with more
ceremony, and harder to read than what is there now. The rule applied throughout
the programme was:

> Extract when the extracted thing is large relative to what it needs. Leave it
> when the parameter list would be longer than the body it justifies.

By that rule these stay. The file size is the real defect, and the file is large
because **one screen owns twenty fields and thirty pieces of state**.

## 3. What the measurement found

Both screens were measured at head:

| | sign_up_visitor | register_visitor |
|--|-----------------|------------------|
| Lines | 1304 | 1262 |
| State fields | 30 | 29 |
| `setState` calls | 30 | 17 |

The finding that shapes this proposal is that they are **not two unrelated
screens**. They collect the same visitor profile through different chrome:

* **17 state fields are shared by name**, including `_arabicName`,
  `_englishName`, `_jobTitle`, `_jobTitleArabic`, `_nationalId`,
  `_documentNumber`, `_docType`, `_nationalityCode`, `_gender`,
  `_profileTypeId`, `_organisationId`, `_countries`, `_profileTypes`.
* **13 payload fields are byte-identical**, including the same conditional
  branching: `isSaudi ? nationalId : null`, the Iqama/passport split on
  `_docType`, and the Saudi/international mobile split.
* Both already share `VisitorDocType` and, in part, the same validators
  (unified 2026-08-08).

They submit to **different endpoints**, each with its own server contract:

| Screen | Endpoint | Dart model | Server DTO |
|--------|----------|------------|------------|
| sign_up_visitor | `/app/account/user-profile` | `UpsertUserProfileRequest` | `SIMF.Contracts/UserProfile/UserProfile.cs` |
| register_visitor | `/app/staff/visitors/register-onsite` | `StaffWalkInRequest` | `AdminWalkInRegistrationRequest`, `SIMF.Contracts/Authentication/AdminAccount.cs` |

`StaffWalkInRequest` is an app-side model that mirrors the CP desk's
`AdminWalkInRegistrationRequest` (D-509), trimmed to the fields the staff form
collects; the two server DTOs are separate classes in separate files. Nothing
proposed here changes an endpoint, a DTO or a JSON key, so the wire contract
(D-219) is untouched.

The duplication is in the FORM, not in the transport.

## 4. Proposed shape

Extract the shared form into section widgets, each owning its own state, and
let both screens compose them.

```
features/visitor_profile/         (new, shared)
  data/
    visitor_profile_validators.dart   the shared rules + VisitorDocType (moved
                                      in step 1)
    visitor_profile_form_state.dart   ChangeNotifier or Riverpod notifier:
                                      the 17 shared fields
  widgets/
    identity_section.dart             Arabic/English name, gender, job title
    nationality_section.dart          nationality, profile type, organisation
    document_section.dart             national id / Iqama / passport, doc type
    contact_section.dart              mobile (Saudi vs international)
```

**Not `features/registration/`,** which this document originally proposed. That
directory already exists and holds the post-signup APPROVAL flow (#11):
`registration_status_screen`, `registration_success_screen` and their widgets,
both routed. Putting the shared form there would file two unrelated concerns
under one name. `visitor_profile` is the domain's own term for this data: it is
what the l10n strings, the router comments and the API
(`/app/account/user-profile`, `UpsertUserProfileRequest`) already call it.

Each screen then keeps only what is genuinely its own:

* `sign_up_visitor_screen` adds date of birth, place of birth, plate number,
  ID image, face photo, and the draft-forward navigation to interests.
* `register_visitor_screen` adds email, the badge/print flow, and the
  per-field SERVER-ERROR echo (`_serverError`) that its validators layer on
  top of the shared rules. That echo is why the two validator sets were NOT
  merged on 2026-08-08, and it must survive this work.

Expected result: both screens well under the 400-line limit, the 14 findings
close, and one definition of "what a visitor profile is" instead of two.

## 5. What makes this a redesign and not a refactor

1. **State ownership moves.** Thirty `setState` calls in one screen become
   notifier updates observed by sections. That is a different execution model,
   not a code move.
2. **It touches the highest-traffic flow in the product.** Every visitor
   registers through one of these two screens. A regression here is not a
   cosmetic defect.
3. **The two screens' behaviour differs in ways that look like duplication.**
   The server-error echo is the known example. There will be others, and each
   is a decision, not a mechanical step.
4. **It is D-694 blast radius.** Shared foundations plus the sign-up
   face-capture path, which a green golden did NOT catch the last time it
   broke (D-666).

## 6. Verification this work must satisfy

The safety net already exists and is unusually strong for a change of this size:

| Suite | Tests |
|-------|-------|
| `sign_up_visitor_screen_test.dart` | 31 |
| `register_visitor_screen_test.dart` | 19 |
| `sign_up_visitor_golden_test.dart` | 1 golden |
| `staff_register_visitor_golden_test.dart` | 1 golden |

Required at every step, not only at the end:

* both goldens byte identical, since this changes structure and not pixels;
* all 52 screen tests green;
* the D-694 blast-radius set: `router_role_matrix_test`, `router_gate_test`,
  `integration_test/app_flows_test.dart`;
* the sign-up face-capture path re-verified ON A DEVICE, per D-694, because a
  golden did not catch the D-666 regression;
* `flutter analyze` 0 errors and 0 warnings, now enforced in CI;
* the convention gate, which should fall from 14 to 1 as sections land.

## 7. Sequence

Ordered so the risky step is last and every step is independently revertible.

| Step | Work | Ends when |
|------|------|-----------|
| 1 | **DONE.** Create `features/visitor_profile/`, move `VisitorDocType` and the shared validators into it. No behaviour change. | Suites green, both screens unchanged in behaviour |
| 2 | `visitor_profile_form_state` holding the 17 shared fields, used by sign_up ONLY. | sign_up suite + golden green |
| 3 | Extract `identity_section` and `contact_section`, sign_up only. | sign_up under 900 lines |
| 4 | **DONE.** Extract `nationality_section` and `document_section`, sign_up only. | 1252 to 1199 lines, findings 14 to 12 |
| 4b | Extract the SUBMIT PIPELINE to `visitor_profile/data`: the nine cross-field rules in `_next` (105 lines), plus `_buildRequest`, `_applyProfile` and `_load`. | sign_up under 400, its remaining findings close |
| 5 | Adopt the same sections in `register_visitor_screen`, preserving the server-error echo. | staff suite + golden green, its 3 findings close |
| 6 | Delete the duplicated state and validators left behind; re-record the convention baseline. | Gate at 1 finding; docs updated in the same changeset |

### 7.1 Correction: step 4 was scoped from a line count

The original step 4 promised "under 400 lines, its 10 findings close". It
delivered 1199 lines and closed 2. The estimate was wrong because it counted
lines without asking what they DO, and the remaining bulk is not UI:

| Method | Lines | What it is |
|--------|-------|------------|
| `_buildBody` | 143 | layout, the last genuinely UI part |
| `_next` | 105 | NINE cross-field submit rules, each with a decision id |
| `_applyProfile` | 44 | prefill mapping |
| `_load` | 36 | concurrent lookup + profile fetch |
| `_buildRequest` | 32 | payload assembly |

`_next` alone carries D-221 (organisation required), D-373 (nationality gates
the document section), D-723 (place of birth), D-471 (profile-type picker) and
the two-photo mandatory/optional split. Those are RULES. They belong beside the
validators in `visitor_profile/data`, where the walk-in desk can share them and
where they can be unit tested, not inside a section widget.

Step 4b is that work, and it is where the file actually gets under 400.

Steps 1 to 4 touch only the self-service flow. Step 5 is where the walk-in desk
changes, and it is deliberately last: if the owner stops after step 4, the
sign-up screen is fixed, the staff screen is untouched, and nothing is
half-migrated.

## 8. What is explicitly NOT in scope

* Any change to the two API endpoints, their request types, or any JSON key
  (D-219).
* Any visual change. The goldens are the contract: if a pixel moves, the step is
  wrong.
* Merging the two validator sets. The server-error echo makes them different;
  see SIMF-CQP-001 and the note in `visitor_profile_validators.dart`.
* `session_detail_screen`'s single finding. It is 467 lines, its remaining
  `_build*` reads 6 pieces of state, and its other candidates are three-to-
  eleven-line dialog wrappers. Extracting them adds files and removes nothing.

## 9. Recommendation

Worth doing, and worth doing in the order above rather than all at once.

The 14 findings are the least interesting reason. The real one is that the
definition of a visitor profile currently exists twice, in two 1300-line files,
and the two copies have already drifted once: the staff desk grew a server-error
echo the self-service screen does not have. A third registration surface, or a
new required field, means finding both again.

If the answer is no, the position stands as recorded in SIMF-CQP-001 section
10.1: 14 findings held at exactly that count by the baseline, with the
measurement behind the decision written down.
