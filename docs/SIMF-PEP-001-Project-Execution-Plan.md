# Project Execution Plan

| Field | Value |
|-------|-------|
| Document ID | SIMF-PEP-001 |
| Title | Project Execution Plan |
| Version | 1.0 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Product Owner |
| Approver | Project Owner (MoD / RSNF representative) |
| Date issued | 2026-05-21 |
| Related documents | SIMF-PGP-001, SIMF-OPS-001, SIMF-TST-001, SIMF-BLG-001, the SIMF-FDS series |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-21 | Engineering & Architecture Team | First issue. |

---

## 1. Purpose

This document turns the SIMF plan into an executable one. The Programme Plan
(SIMF-PGP-001) set the stages and gates; this plan sets the sprint schedule, who
builds what and when, the team, the stakeholders, and the DevOps setup. It is
the Stage 4 deliverable.

## 2. Scope

The document covers the execution schedule and milestones, the sprint-by-sprint
build plan, the team and role assignment, the stakeholder register, the DevOps
and environment setup, and the governance gates that bind the build.

It does not restate the architecture or the feature specifications. The detailed
backlog of stories and tasks is SIMF-BLG-001.

## 3. Constraints that shape the schedule

| Constraint | Value |
|------------|-------|
| Forum dates | 23–25 November 2026 — immovable |
| Operational target | The system live and stable about two months before the forum |
| Reference timeline | The 18-week plan in `Overall Time & Plan.pdf` |
| Cadence | Scrum, two-week sprints (SIMF-PGP-001) |
| Critical dependency | The mobile-app UI from the external designer |
| Gate | No build before the documents are approved and the per-feature specs exist |

## 4. Milestones

The milestones are carried from SIMF-PGP-001.

| ID | Milestone | Target |
|----|-----------|--------|
| M1 | Design approved | Day 6 |
| M2 | UI/UX complete | End of week 7 |
| M3 | Application development complete | End of week 12 |
| M4 | Testing complete | End of week 16 |
| M5 | Live environment ready | End of week 16 |
| M6 | Published to the stores | End of week 17 |
| M7 | Actual operation | Week 18 |

## 5. Sprint schedule

The build runs as nine two-week sprints across the 18-week reference timeline.
Sprint dates are aligned to the approved `Overall Time & Plan` and fixed at
Sprint 1 planning; the sequence below is the plan of work.

| Sprint | Focus | Key features |
|--------|-------|--------------|
| Sprint 1 | Foundation and authentication | Solution scaffold, the CI/CD pipeline, the four environments; **Login API** (SIMF-FDS-001); the Control Panel base — shell, theming, AR/EN, multi-theme (SIMF-CPD-001) |
| Sprint 2 | Registration and access | Registration & Approval (SIMF-FDS-002); Badge & Access Control (SIMF-FDS-003) |
| Sprint 3 | Programme | Forum Programme — themes, halls, speakers, sessions, the agenda (SIMF-FDS-004) |
| Sprint 4 | Bookings and exhibition | Bookings & Attendance (SIMF-FDS-005); Exhibition — booths, sponsors, venue map (SIMF-FDS-006) |
| Sprint 5 | Engagement | Live broadcast, questions, comment moderation (SIMF-FDS-007) |
| Sprint 6 | Networking, AI and notifications | Networking & Cognitive AI (SIMF-FDS-008); Notifications (SIMF-FDS-009) |
| Sprint 7 | Content and statistics | Media, News & Archive (SIMF-FDS-010); Statistics & Dashboards (SIMF-FDS-011); Control Panel Configuration (SIMF-FDS-012) |
| Sprint 8 | Hardening and integration | Defect burn-down; the App UI applied as the external design arrives; security testing; the live environment |
| Sprint 9 | Release | Store publication; the security clearances; the load and traffic tests; go-live preparation |

The **Backend and APIs run continuously** from Sprint 1 to the end; they are not
a sprint of their own (SIMF-PGP-001). The **22-day continuous testing** runs
across Sprints 6–8. The mobile-app structure is built from Sprint 1; the
external designer's visuals are applied as they are delivered (SIMF-MAA-001
section 12).

## 6. Workstreams in each sprint

Within a sprint the six workstreams from SIMF-PGP-001 run in parallel: Backend &
APIs, Control Panel, Mobile App, Documentation, DevOps, and QA & Security. A
feature is "done" only when its API, backend and Control Panel slices meet the
definition of done in SIMF-SES-001 section 14 and its FDS acceptance criteria
and test scenarios pass.

## 7. Team and roles

The delivery team, carried from SIMF-PGP-001 and the proposal:

| Role | Count | Responsibility |
|------|-------|----------------|
| Product Owner | 1 | The backlog, priorities, acceptance |
| Scrum Master | 1 | The process, removing blockers |
| Solution Architect | 1 | Design, technical direction, reviews |
| .NET / Backend Engineers | 2 | The backend, the APIs, the Control Panel, integration |
| Flutter Engineer | 1 | The mobile app |
| DevOps Engineer | 1 | The pipeline, the environments, deployments |
| AI Specialist | 1 | The cognitive-AI integration |
| QA / Test Engineers | up to 14 | Testing, intensively through the continuous-testing phase |
| External UI/UX Designer | external | The mobile-app visual design |

Task allocation to named people is done at sprint planning and tracked in
SIMF-BLG-001 and Azure DevOps Boards.

## 8. Stakeholder register

> Named representatives are confirmed by the owner — open item OI-1.

| Stakeholder | Side | Interest | RACI on delivery |
|-------------|------|----------|------------------|
| Project Owner (MoD / RSNF) | Client | Accountable for the system; approves releases and go-live | Accountable |
| RSNF organising sponsor | Client | The forum's success | Accountable / Consulted |
| PR team | Client | Exhibitors, VIP, guests, bookings | Consulted |
| Security team | Client | Registration vetting | Consulted |
| Technical team | Client | System administration, the cyber clearance | Consulted |
| Scientific team | Client | The programme content | Consulted |
| Logistics team | Client | Venue, halls, booths | Consulted |
| Product Owner (STARTIME) | Vendor | The backlog and delivery | Responsible |
| Solution Architect | Vendor | The architecture | Responsible |
| Scrum Master | Vendor | The process | Responsible |
| Delivery team | Vendor | Building the system | Responsible |
| External UI/UX Designer | External | The app visual design | Responsible (design) |
| NCA-accredited security firm | External | Penetration testing | Consulted |

Stakeholders see progress at the two-week sprint demos; the Azure DevOps Boards
are visible to the client throughout.

## 9. DevOps and environment setup

Set up at the start of Sprint 1, per SIMF-OPS-001:

- **Azure DevOps** — Repos, Boards, Pipelines and Test Plans for the project.
- **The repository** — the structure in SIMF-SES-001 section 4.1; branch
  policies requiring a reviewed pull request and a green build to merge.
- **The four environments** — Development, Test, Staging, Production
  (SIMF-OPS-001 section 4); the Test environment is stood up first.
- **The CI/CD pipeline** — Commit → Build → Test → Deploy → Monitor, with
  test-gated promotion (SIMF-OPS-001 section 5).
- **Configuration and secrets** — the environment-variable approach in
  SIMF-SES-001 section 4.4 and SIMF-OPS-001 section 6.

Confirming Azure DevOps access and the environment hosting with the client and
STC is open item OI-2.

## 10. Governance and gates

The build is bound by the gates in SIMF-PGP-001 and the rules in SIMF-SES-001:

- **No build before the documents are approved** and the per-feature specs
  exist (Gate G3). The documents are currently Draft; client approval through
  the SIMF-DMP-001 section 7 workflow is the gate into Sprint 1.
- **Change freeze** — requirements are settled before the testing phase; no
  change after publish; a post-gate change is re-planned and recorded.
- **Definition of done** — every feature, every sprint (SIMF-SES-001 section
  14).
- **Quality** — zero-warning builds, the coverage floor, peer review
  (SIMF-SES-001, SIMF-TST-001).

## 11. Risks

The risks from SIMF-PGP-001 section 9 carry into execution; the ones that bear
most on the schedule:

| Risk | Mitigation |
|------|------------|
| The external designer delays the App UI | Backend, Control Panel and the Flutter base proceed independently; the UI is applied late (Sprint 8) |
| Documents not approved in time | Approval is the Sprint 1 entry gate; it is tracked from now |
| The fixed November deadline | The schedule keeps a hardening and release margin (Sprints 8–9) before the two-months-early target |
| Store review delay | Store accounts and signing prepared in Sprint 1; publication in Sprint 9 with margin |
| Security clearance lead time | The QA & Security workstream engages from Sprint 1; the MoD review is booked early |

## 12. Open items

| ID | Item | Affects |
|----|------|---------|
| OI-1 | Confirm the named client and vendor stakeholders and the approval authority | Section 8 |
| OI-2 | Confirm Azure DevOps access and the environment hosting with the client and STC | Section 9 |
| OI-3 | Fix the Sprint 1 calendar date once the documents are approved | Section 5 |
| OI-4 | Confirm document classification with the owner | Control block |

---

End of document.
