# Open Items Register

| Field | Value |
|-------|-------|
| Document ID | SIMF-OIR-001 |
| Title | Open Items Register |
| Version | 1.1 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Solution Architect |
| Approver | Project Owner (MoD / RSNF representative) |
| Date issued | 2026-05-21 |
| Related documents | All SIMF documents — see section 7 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-21 | Engineering & Architecture Team | First issue. Consolidates the open items from all 28 documents. |
| 1.1 | 2026-05-21 | Engineering & Architecture Team | All 14 owner decisions (OD-1…OD-14) answered in the client review; resolutions recorded in section 5. |

---

## 1. Purpose

This register collects every open item flagged across the SIMF document set into
one place. It is the punch-list for the client review: a reader works through
section 5 to clear the owner decisions, rather than hunting open items through
28 documents.

## 2. Scope

The register covers the open items (the `OI-` entries) in all 28 SIMF
documents as of 2026-05-21. It groups them so that a point raised in several
documents is one entry here, not many.

## 3. How to read this register

The open items fall into three kinds:

- **Owner decisions (section 5)** — points only the client or owner can settle.
  This is the review punch-list.
- **Engineering items (section 6)** — points the team settles itself during the
  low-level design and the build; they need no client decision.
- **Already resolved (section 7)** — items that the closed decisions D1–D12 have
  already answered, listed so the documents can be tidied.

Section 8 is the full index, every open item by its document.

## 4. Summary

The 28 documents carry **121 open-item entries**. De-duplicated, they are:

| Kind | Distinct items |
|------|----------------|
| Owner decisions (section 5) | 14 decision groups — all answered 2026-05-21 |
| Engineering items (section 6) | 11 items |
| Already resolved by D1–D12 (section 7) | 8 items |

The largest single saving: "confirm the document classification" appears in
**25 documents** and is **one** owner decision.

## 5. Owner decisions — the review punch-list

Each entry is a decision the client or owner makes. Clearing it closes the open
items listed against it.

**All fourteen owner decisions were answered in the client review of
2026-05-21.** The resolutions are below; each applies to the documents the
decision touched.

| OD | Resolution (2026-05-21) |
|----|--------------------------|
| OD-1 | Documents are handled and labelled **Confidential**, the working default (decision D9). |
| OD-2 | All external providers are **deferred** — kept provider-agnostic behind abstractions (decision D7). |
| OD-3 | The media / news field sets, the categories and the statistics list take the **mockup and analysis documents as the baseline**. |
| OD-4 | The SQL Server 2022 edition is **deferred** to the host confirmation (decision D8). |
| OD-5 | GPS-presence, operation-log and backup **retention align to the NCA / MoD data-retention policy**. |
| OD-6 | The outstanding brand assets are **deferred** — the build starts with the Regular font weight and the provided PDFs; the rest are collected before the UI is finalised. |
| OD-7 | The Control Panel derived colours, type scale and dark-theme tokens proposed in SIMF-CPD-001 are **accepted**. |
| OD-8 | The **Administrator holds all permissions at launch**; the team roles are built from the Control Panel afterwards. SIMF-RPM-001 Appendix A stays a reference, not a seeded configuration. |
| OD-9 | **Role titles are used for now**; the named stakeholders, the approver and the Azure DevOps / STC access are provided before Sprint 1. |
| OD-10 | The password policy, code lifetime, rate limits and the penetration-testing firm **align to the NCA / MoD security policy**. |
| OD-11 | Registration & access details — **the team proposes sensible defaults; the client reviews them during the build**. |
| OD-12 | Engagement & AI behaviour details — the team proposes defaults; the client reviews them during the build. |
| OD-13 | Booking, exhibition & notification details — the team proposes defaults; the client reviews them during the build. |
| OD-14 | Programme, content & operations details — the team proposes defaults; the client reviews them during the build. |

The entries below record what each decision covered and the documents it
closes.

### OD-1 — Document classification
Confirm the official classification labels and handling rules for the project.
One answer sets the Classification field in every document.
*Closes:* the classification open item in 25 documents (DMP, SES, SAD, API,
SRS, RPM, DAT, CPD, MAA, OPS, TST, PEP, BLG, FDS-001…012).

### OD-2 — External service providers
Confirm the providers for: the cognitive AI (decision D7 — including any
on-premises or sovereign requirement for an MoD system), the live-broadcast
platform, the email / SMS / WhatsApp channels, and the map / location service.
*Closes:* SAD OI-6, OI-7; SRS OI-2; FDS-007 OI-1; FDS-008 OI-1; FDS-009 OI-1;
FDS-006 OI-1; MAA OI-4.

### OD-3 — Content field sets (decision D6)
Confirm the field sets for media items and news items, the news categories, the
session categories, the statistics figure list, and the content-block keys.
*Closes:* SRS OI-1; DAT OI-1; CPD OI-4; FDS-010 OI-1; FDS-011 OI-1; FDS-004
OI-2; FDS-012 OI-1.

### OD-4 — SQL Server 2022 edition and licence (decision D8)
Confirm the SQL Server 2022 edition (Standard or Enterprise) and the licence.
*Closes:* DAT OI-4; OPS OI-2.

### OD-5 — Data retention rules
Confirm the retention rules for GPS-presence data, the operation log, and the
backups.
*Closes:* DAT OI-3; FDS-003 OI-3; FDS-011 OI-3; FDS-012 OI-3; OPS OI-4.

### OD-6 — Brand assets
Provide the FS Albert Arabic Bold (and any other) weights, and the vector (SVG)
assets — the SIMF forum logo, the pattern tile, and the RSNF emblem.
*Closes:* VID OI-1, OI-2.

### OD-7 — Control Panel visual review
Review and confirm the derived functional and state colours, the type scale,
and the dark-theme token values.
*Closes:* CPD OI-1, OI-2; VID OI-3, OI-4.

### OD-8 — Roles and permissions review
Review and confirm the page-and-action catalogue and the suggested starting
role configuration (SIMF-RPM-001 Appendix A).
*Closes:* RPM OI-1; CPD OI-3.

### OD-9 — Stakeholders, approvers and access
Confirm the named client and vendor stakeholders and the approval authority,
the approver roles in SIMF-DMP-001, and Azure DevOps access and the environment
hosting with STC.
*Closes:* PEP OI-1, OI-2; DMP OI-2; RDR OI-1; OPS OI-1, OI-3.

### OD-10 — Security and authentication parameters
Confirm the password policy, the verification / reset code lifetime and the
auth rate limits, the vulnerability scanner and the NCA-accredited
penetration-testing firm.
*Closes:* API OI-2; FDS-001 OI-2; TST OI-2.

### OD-11 — Registration and access details
Confirm: whether on-site registration is approved on the spot or follows the
standard review; the attachment file types and size limit; the mockup Screen 7
visitor seat/row pick; and whether hall-door scanning is by Staff, a fixed
device, or both.
*Closes:* FDS-002 OI-1, OI-2, OI-3; UCS OI-3; FDS-003 OI-4.

### OD-12 — Engagement and AI behaviour
Confirm: how the Riyadh-region restriction is determined; the AI comment-filter
rules; whether a moderator may act from the Control Panel; the matchmaking
inputs and score formula; and whether choosing an interest is mandatory at
registration.
*Closes:* FDS-007 OI-2, OI-3, OI-4; FDS-008 OI-3, OI-4; UCS OI-2.

### OD-13 — Booking, exhibition and notification details
Confirm: a booking cap per attendee and whether a held seat expires; whether a
booth may host more than one exhibitor; the sponsor ordering within a tier; the
default notification channel mix; and the failed-send retry policy.
*Closes:* FDS-005 OI-2, OI-3; FDS-006 OI-2, OI-3; FDS-009 OI-2, OI-3.

### OD-14 — Programme, content and operations details
Confirm: whether the seat grid supports irregular hall layouts; whether speakers
may belong to past editions; whether social content is pulled automatically or
entered by hand; the source of "student badges printed outside the Control
Panel"; the website sign-in scope; the monitoring tooling; the UAT schedule and
participants; and the VIP-only feature set and the Exhibitor/Moderator account
model.
*Closes:* FDS-004 OI-1, OI-3; FDS-010 OI-2; FDS-011 OI-2, OI-4; FDS-001 OI-3;
OPS OI-3; TST OI-1, OI-3; RPM OI-2, OI-3.

## 6. Engineering items — settled by the team

These need no client decision; the team settles them in the low-level design
and the build, and updates the affected document.

| ID | Item | Source |
|----|------|--------|
| EI-1 | Add the feature endpoint contracts to SIMF-API-001 now the gates are closed | API OI-1 |
| EI-2 | Fix the final .NET project breakdown | SES OI-3 |
| EI-3 | Fix the SIMF shared-constants namespace (the `AppRoles` equivalent) | SES OI-4 |
| EI-4 | Reconcile the `Smif*` shared component library with the designer's component handoff | SES OI-5 |
| EI-5 | Add a geofence to the `Hall` entity in SIMF-DAT-001 | FDS-003 OI-2 |
| EI-6 | Add `Rejected` to `Booking.Status` in SIMF-DAT-001 | FDS-005 OI-1 |
| EI-7 | Generalise `EmailVerificationCode` to an account-code entity with a `Purpose` field | FDS-001 OI-1 |
| EI-8 | Decide whether `MatchSuggestion` is stored or computed | DAT OI-2; FDS-008 OI-2 |
| EI-9 | Confirm the QR signed-token scheme | FDS-003 OI-1 |
| EI-10 | Fix the Flutter package list and versions; confirm the design tool/format with the designer | MAA OI-1, OI-2 |
| EI-11 | Confirm the system-configuration settings list, the snapshot refresh cycle, the seat-grid model, and the previous-edition-to-programme entity relation | FDS-012 OI-2; FDS-011 OI-4; FDS-010 OI-3 |

## 7. Already resolved by decisions D1–D12

These open items were answered by the closed decisions; the affected documents
can drop them at their next revision.

| Item | Resolved by |
|------|-------------|
| SAD OI-1 — per-user-type permissions | D1 (and SIMF-RPM-001) |
| SAD OI-2 — meaning of "direction / track" | D2 |
| SAD OI-3 — exhibitor / moderator / staff workflows | D3 |
| SAD OI-4 — booking and attendance; hall-arrival | D4 |
| SAD OI-5 — question mechanics; AI filter; AI levels | D5 |
| API OI-4 — whether an unapproved user may sign in | D1 (option B) |
| RDR OI-2 — a target date for closing D1–D6 | D1–D6 are closed |
| SRS OI-3 / UCS OI-1 — per-feature detail elaborated in the FDS specs | The FDS series is complete |

## 8. Full index by document

The complete list of open items, by document, is the source for this register.
It is held in the "Open items" section of each document; the consolidation above
is the working punch-list. The raw list (121 entries) was compiled on
2026-05-21 from the document set.

## 9. How this register is used

- The owner clears section 5 during the document review.
- As an owner decision is made, it is recorded in SIMF-RDR-001 where it is a
  formal decision, and the affected documents are updated and their open item
  closed.
- The team clears section 6 during the low-level design.
- Section 7 items are removed from their documents at the next revision.
- This register is updated as items close, so it always shows what is
  outstanding.

---

End of document.
