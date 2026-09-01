# Plan B. Bring the documents to the truth

SIMF-HLD-004, SIMF-LLD-003, SIMF-BRD-001 (English). Written 2026-08-31.

The backlog is [`docs/quality/document-defect-register-2026-08-31.md`](../quality/document-defect-register-2026-08-31.md):
146 open findings from a cover-to-cover read, in six clusters. This plan says in
what order to close them, what rule prevents them recurring, and which of them
are not mine to decide.

## The rule that failed, stated so it cannot fail silently again

Three verification passes reported PASS on these documents while 46 claims in
them were disprovable from the repository. Every check compared the text to
itself and to the deployment diagram. **None compared the text to the code.**
A checker that only looks for the defect its author already imagined will always
pass.

From here, two rules govern:

1. **The code is an authority, not an audience.** A statement about what the
   system does is checked against the source tree. A document is never evidence
   about the build, and neither is another document.
2. **A design document may describe the agreed target. It may not claim a target
   is already delivered.** This is the exact line that broke: the HLD said "the
   delivered configuration points at the on-site GPT OSS 120B endpoint" while
   `appsettings.json` shipped the offline stub and cloud URLs. Describing phase
   one is correct. Asserting it is already running is not.

Rule 2 is the interlock with [Plan A](phase-one-code-alignment-plan.md). The
documents describe MinIO, the on-site model and the caption call made from the
Control Panel. Those descriptions stay, because they are the agreed
architecture. Plan A makes them true. Until it does, no sentence may say they
already are.

## Order of work

**Cluster 1, the LLD data dictionary, is closed.** 26 rows described a schema
that does not exist, because both EF histories were regenerated under D-881,
D-924, D-926 and D-929 and this section was not regenerated with them. Fixed and
committed, and `tools/check-lld-schema.py` now fails the moment the dictionary
names an identifier no migration creates. That gate found eight the readers had
missed, including a phantom entity named four times.

Then, in this order:

| Step | Cluster | Count | Why here |
|---|---|---|---|
| 1 | Claims the code disproves | 46 | The only kind a reviewer can prove wrong in the room |
| 2 | Internal contradictions | 14 | Two statements, one of them wrong, both signed by us |
| 3 | Undefined terms | 21 | Cheap, and the customer named ambiguity specifically |
| 4 | Repetition and filler | 36 | Real, and the customer named it, but nobody can disprove it |
| 5 | Features named but not described | 3 | Fold into step 3 |

If time runs out, stop after step 2. Steps 3 and 4 improve a document nobody can
call false; steps 1 and 2 remove statements that cost credibility.

## Extend the gate as each step closes

`check-lld-schema.py` guards column names only. Each step should leave behind the
check that would have caught it, or the step is decoration:

- **Counts.** Assert every count the documents state against the source: the
  navigation groups, the mobile feature folders, the e-mail template types, the
  rate-limit policies, the file-service categories. Five of these were wrong.
- **Named artefacts.** Assert every file path, script name, endpoint route and
  package the documents name still exists. This catches `deploy/ops.ps1`,
  `pretty_dio_logger`, `/admin/contacts` and `/admin/companies`.
- **Entities.** Assert every entity the LLD names has a class or a table.

None of these needs an agent. They are greps with an exit code.

## Reissue discipline

Predecessor versions are never edited. A correction round produces the next
version and leaves every earlier file at its published bytes, which is the rule
that has held through v1.0 to v1.4 of the HLD and v1.1 to v1.5 of the LLD.

## Not in scope

- **The Arabic BRD**, by owner instruction of 2026-08-31. Note that it drifted
  from the English on three facts before it was set aside, so if it is ever
  reissued it needs a parity pass, not a spot fix.
- **`SIMF-HLD-004-Response-to-Technical-Review-v1.0.docx`**, which was never
  reissued for phase one. It still says "egress point" nine times and names
  neither MinIO nor GPT OSS 120B, so it contradicts the HLD annex it accompanies.
  It needs a decision before it needs work: reissue at v1.1, or declare it
  superseded by the HLD annex.

## Three things this plan will not decide

These are owner calls. They are listed here so they are decided rather than
absorbed.

1. **The comments feature does not exist.** No table in either `InitialCreate`,
   no entity, no endpoint. Three documents promise it: the BRD binds it with
   "shall" in two requirements and a business rule, the LLD names a `Comment`
   entity and a comment endpoint, and the HLD lists it in scope. No decision row
   records its removal, so the reason is unknown. Either it is built, or the
   requirement is withdrawn the way FR-807 was. **A reviewer who asks for a demo
   finds this in one minute.**
2. **BR-07 and FR-708 require a check-in gate the build does not enforce.**
   `SessionQuestionService.cs` applies no lower time bound and gates on hall
   arrival only when the hall carries a geofence, and geofences are deferred
   under G-OI-2. The HLD and LLD now describe what the code does. The BRD still
   states the requirement, so as delivered it is a requirement not met.
3. **Whether the counts above become build-failing tests or advisory scripts.**
   The standing directive keeps CI test gates off, so a new test does not gate
   anything today. A script that nobody runs is worth nothing; say which it is.
