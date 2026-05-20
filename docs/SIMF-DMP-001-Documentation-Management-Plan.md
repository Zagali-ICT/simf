# Documentation Management Plan

| Field | Value |
|-------|-------|
| Document ID | SIMF-DMP-001 |
| Title | Documentation Management Plan |
| Version | 1.0 |
| Status | Draft |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Solution Architect |
| Approver | Project Owner (MoD / RSNF representative) |
| Date issued | 2026-05-20 |
| Related documents | SIMF-PGP-001, SIMF-CON-001, SIMF-SES-001 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. |

---

## 1. Purpose

This plan sets the rules for every document produced on the SIMF project: how a
document is identified, what it must contain, how it is reviewed and approved,
and how its versions are controlled. It exists so that the requirements,
architecture and design documents that the development team builds from are
consistent, traceable, and unambiguous about their own status.

A reader who picks up any SIMF document should be able to answer four questions
within the first page: what is this, is it approved, who approved it, and what
changed since last time. This plan makes those answers mandatory.

## 2. Scope

The plan applies to all engineering and project documents for SIMF V1.0.0 — the
concept, planning, architecture, requirements, design, standards, test and
operations documents. It does not govern source code, code comments, or commit
messages; those are covered by SIMF-SES-001 (Software Engineering Standards).

Marketing material, contracts and commercial correspondence are out of scope.

## 3. The controlled-document format

Every SIMF document is a controlled document. It is written in Markdown and
begins with two fixed blocks, in this order.

### 3.1 Document control block

A table with these fields, all of them filled in — no blanks:

- Document ID — the identifier defined in section 4.
- Title — the human-readable name.
- Version — see section 6.
- Status — one of Draft, In Review, Approved, Superseded.
- Classification — the handling classification (section 5).
- Prepared by — the team that wrote it.
- Owner — the role accountable for keeping it correct.
- Approver — the role that signs it off.
- Date issued — the date of the current version.
- Related documents — IDs of documents a reader needs alongside this one.

### 3.2 Revision history block

A table with one row per version: version number, date, author, and a short
description of what changed. The history is never rewritten. A correction to an
old entry is made by adding a new row, not by editing the old one.

### 3.3 Body

After the two blocks, the body uses numbered sections. Conventions:

- Headings are written in sentence case. Only proper nouns and acronyms keep
  their capitals.
- Diagrams are kept in the document as Mermaid or PlantUML text, so they version
  and diff like the rest of the document. Binary image files are avoided unless
  a diagram genuinely cannot be expressed as text.
- Acronyms are spelled out on first use in each document.
- Requirements, decisions and open issues are given stable identifiers (for
  example FR-012, AD-004, OI-7) so other documents can reference them.
- Where a statement depends on a decision that has not been made, the document
  says so explicitly and points to the open issue. It never fills the gap with
  an assumption. This is the NEVER GUESS rule, and it is enforced in review.

### 3.4 Document template

A blank template carrying the two control blocks and an empty section outline is
kept at `docs/_templates/CONTROLLED-DOCUMENT-TEMPLATE.md`. New documents start
from a copy of it.

## 4. Document identification

### 4.1 Identifier scheme

Every document has an identifier of the form:

```
SIMF-<TYPE>-<NNN>
```

- `SIMF` is the project code, fixed.
- `<TYPE>` is a three-letter document type from the table in section 4.2.
- `<NNN>` is a zero-padded sequence number within that type, starting at 001.

Example: `SIMF-SRS-001` is the first Software Requirements Specification.

The identifier never changes once assigned, even if the document is renamed or
superseded. A superseded document keeps its ID and its status becomes
Superseded; the replacement gets the next free number.

### 4.2 Document types

| Code | Document type |
|------|---------------|
| CON | Concept / vision |
| PGP | Programme / project plan |
| DMP | Documentation management plan |
| SES | Software engineering standards |
| SAD | Software architecture document |
| SRS | Software requirements specification |
| RPM | Roles and permissions specification |
| UCS | Use case specifications |
| DAT | Data model and database design |
| API | API specification |
| MAA | Mobile application architecture |
| CPD | Control panel design specification |
| FDS | Feature design specification (detailed, per feature) |
| TST | Test plan / test specification |
| OPS | Deployment and operations document |
| BSP | Base system plan |

New types are added to this table by updating SIMF-DMP-001, not by inventing a
code in a filename.

### 4.3 File naming

A document's file is named:

```
SIMF-<TYPE>-<NNN>-<short-title>.md
```

For example `SIMF-SRS-001-Software-Requirements-Specification.md`. The short
title uses hyphens, no spaces.

Three documents were written before this scheme was set. They keep their current
filenames and are registered with IDs in section 9; they will not be renamed,
to avoid breaking existing links.

## 5. Classification and handling

SIMF is a Ministry of Defense project. Every document carries a classification
in its control block and, where the owner's policy requires it, in a page
header or footer.

Until the owner confirms the classification scheme, all SIMF documents are
handled as **Confidential**: shared only with named project members on the
client and vendor sides, stored only in the approved project repository, and
not forwarded outside the project without the owner's approval.

Confirming the official classification labels and handling rules is an open
item — see OI-1 in section 11. The team will not guess them.

## 6. Versioning

### 6.1 Version numbers

Documents use a two-part version number, `MAJOR.MINOR`.

- The first issue of any document is version 1.0.
- A minor change — wording, corrections, added detail that does not change an
  approved decision — increments the minor part: 1.0 to 1.1.
- A major change — anything that changes an approved requirement, decision or
  scope statement — increments the major part and resets the minor part: 1.4 to
  2.0. A major change re-enters the review and approval cycle.

### 6.2 Draft versions

While a document is still in Draft and has never been approved, it stays at 1.x
and the Status field carries the weight. A document only earns the right to a
clean approved version once it has passed review.

### 6.3 Status lifecycle

```
Draft  →  In Review  →  Approved  →  (Superseded)
              ↑              |
              └──── rejected ─┘
```

- Draft — being written or revised.
- In Review — frozen for reviewers; no edits except those a reviewer requests.
- Approved — signed off by the approver named in the control block.
- Superseded — replaced by a newer document or version; kept for the record,
  never deleted.

## 7. Review and approval

### 7.1 Workflow

1. The author moves the document from Draft to In Review and notifies the
   reviewers listed for that document type (section 7.2).
2. Reviewers record comments. Each comment is either resolved by an edit or
   answered with a reason it was not actioned.
3. When all comments are resolved, the author submits the document to the
   approver.
4. The approver either approves it — at which point the Status becomes Approved
   and the revision history records the approval — or returns it to Draft with
   reasons.

### 7.2 Who reviews what

| Document type | Reviewers | Approver |
|---------------|-----------|----------|
| CON, PGP, DMP | Solution Architect, Product Owner | Project Owner |
| SES | Solution Architect, Lead Engineers | Solution Architect |
| SAD, DAT, API, MAA | Solution Architect, Lead Engineers, Security reviewer | Solution Architect |
| SRS, RPM, UCS | Product Owner, Solution Architect, Client representative | Project Owner |
| CPD | Solution Architect, Control Panel lead, Product Owner | Project Owner |
| FDS | Product Owner, Solution Architect, feature engineer | Product Owner |
| TST | QA Lead, Solution Architect | Project Owner |
| OPS | DevOps Engineer, Solution Architect | Project Owner |

### 7.3 The approval gate

A document that other work depends on must be Approved before that work starts.
The programme plan (SIMF-PGP-001) names the gates. The most important one: no
feature is built before its Feature Design Specification is Approved.

## 8. Storage and traceability

All documents live under `docs/` in the project repository and are versioned
with the code. The repository history is the authoritative record of who
changed what and when; the revision-history table is the human-readable summary
of the same thing.

Documents reference each other by ID. When document A states something that
originates in document B, A cites B's ID and, where useful, the specific item
(for example, "see SIMF-SRS-001, FR-012"). This is what makes a change in one
document traceable to everything it affects.

## 9. Document register

The current SIMF document set. Status reflects the position on 2026-05-20.

| ID | Title | File | Status |
|----|-------|------|--------|
| SIMF-BSP-001 | Base System Plan | base-system-plan.md | Draft |
| SIMF-CON-001 | System Concept Summary | SIMF-Concept-Summary.md | Draft (baseline) |
| SIMF-PGP-001 | Programme Plan | SIMF-Program-Plan.md | Approved 2026-05-20 |
| SIMF-DMP-001 | Documentation Management Plan | SIMF-DMP-001-Documentation-Management-Plan.md | Draft |
| SIMF-SES-001 | Software Engineering Standards | SIMF-SES-001-Software-Engineering-Standards.md | Draft |
| SIMF-SAD-001 | Software Architecture Document | SIMF-SAD-001-Software-Architecture-Document.md | Draft |
| SIMF-API-001 | API Specification | SIMF-API-001-API-Specification.md | Draft |
| SIMF-MAA-001 | Mobile Application Architecture | SIMF-MAA-001-Mobile-Application-Architecture.md | Draft |
| SIMF-SRS-001 | Software Requirements Specification | planned | Not started — blocked on gates D1–D6 |
| SIMF-RPM-001 | Roles and Permissions Specification | planned | Not started — blocked on gate D1 |
| SIMF-UCS-001 | Use Case Specifications | planned | Not started — blocked on gates D1–D6 |
| SIMF-DAT-001 | Data Model and Database Design | planned | Not started — blocked on gates D1–D6 |
| SIMF-CPD-001 | Control Panel Design Specification | planned | Not started |

Feature Design Specifications (SIMF-FDS-NNN), test documents (SIMF-TST-NNN) and
the operations document (SIMF-OPS-001) are added to this register as they are
created.

This register is the single place where the document set is listed. It is
updated whenever a document is added, approved or superseded.

## 10. Responsibilities

- The **author** writes the document, keeps the control and revision blocks
  correct, and drives it through review.
- The **owner** keeps the document accurate over time and raises a new version
  when reality moves on.
- The **approver** signs off and is accountable for the content being fit to
  build from.
- The **Solution Architect** keeps the document register current and owns this
  plan.

## 11. Open items

| ID | Item | Needed for |
|----|------|-----------|
| OI-1 | Official classification labels and handling rules from the owner | Section 5 |
| OI-2 | Confirmation of the approver roles named in section 7.2 against the actual project organisation | Section 7 |

---

End of document.
