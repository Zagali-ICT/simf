# Plan B. Correct the documents

SIMF-HLD-004 v1.4, SIMF-LLD-003 v1.5, SIMF-BRD-001-EN v1.3.

The backlog is
[`docs/quality/document-defect-register-2026-08-31.md`](../quality/document-defect-register-2026-08-31.md),
which lists every open defect with the source file that settles it.

## Two rules

1. **The code decides what the system does.** Any statement about behaviour is
   checked against `src/`. A document is not evidence about the build, and
   neither is another document.
2. **A design document describes the agreed architecture. It must not claim an
   agreed target is already delivered.** Phase one is the agreed architecture, so
   the documents correctly describe MinIO, the on-site model and the caption call
   made from the Control Panel. [Plan A](phase-one-code-alignment-plan.md) makes
   the code match. Until it does, no sentence may say those already run.

## Order

The LLD data dictionary is closed: 26 rows named columns, types, nullability or
cardinality the schema does not have, because both EF histories were regenerated
under D-881, D-924, D-926 and D-929 without it.
`tools/check-lld-schema.py` now fails whenever the dictionary names an
identifier no migration creates.

| Step | Defect kind | Count | Why in this position |
|---|---|---|---|
| 1 | Claims the code disproves | 45 | The only kind a reviewer can prove wrong in the room |
| 2 | Internal contradictions | 14 | Two statements under one signature, one of them wrong |
| 3 | Undefined terms and double readings | 21 | Named by the customer as a complaint |
| 4 | Repetition and filler | 36 | Named by the customer, but not disprovable |
| 5 | Features named but not described | 3 | Do with step 3 |

## Checks to add as each step closes

`check-lld-schema.py` guards column names. Extend it, or the same class of
defect returns with the next change:

- **Counts.** Every count the documents state, against the source: navigation
  groups, mobile feature folders, e-mail template types, rate-limit policies,
  file-service categories.
- **Named artefacts.** Every file path, script, endpoint route and package the
  documents name still exists.
- **Entities.** Every entity the LLD names has a class or a table.

Each is a grep with an exit code.

## Reissue rule

A correction round produces the next version and leaves every earlier file at its
published bytes.

## Three items that need an owner decision

1. **The comments feature is not built.** No table in either `InitialCreate`, no
   entity, no endpoint. The BRD binds it with "shall" in two requirements and a
   business rule, the LLD names a `Comment` entity and a comment endpoint, and
   the HLD lists it in scope. Build it, or withdraw the requirement the way
   FR-807 was withdrawn.
2. **BR-07 and FR-708 require a hall check-in gate the build does not enforce.**
   `SessionQuestionService.cs` applies no lower time bound and gates on hall
   arrival only when the hall carries a geofence, and geofences are deferred
   under G-OI-2. As delivered, this is a requirement not met.
3. **`SIMF-HLD-004-Response-to-Technical-Review-v1.0.docx` was not reissued for
   phase one.** It says "egress point" nine times and names neither MinIO nor
   GPT OSS 120B, so it contradicts the HLD annex it accompanies. Reissue it, or
   record it as superseded by that annex.
