# SIMF — Program Plan ("Plan of Plan")

> **Project:** SIMF — Saudi International Maritime Forum 2026
> **System version:** V1.0.0
> **Document type:** Program execution roadmap — the meta-plan that sequences all work
> **Status:** DRAFT — awaiting client/owner approval
> **Created:** 2026-05-20
> **Baseline:** `SIMF-Concept-Summary.md` (authoritative baseline = 2026-05-20 meeting)

This document defines **how the SIMF system will be planned, designed,
documented and built** — the order of stages, what each stage produces, and the
gates between them. It is not the architecture and not the schedule; it is the
plan that produces those.

---

## 1. How to Read This Document

- **Stages** (§5) run mostly in sequence; each has a gate that must pass before
  the next begins.
- **Workstreams** (§4) run in parallel across stages (e.g. Backend starts early
  and never stops).
- **Decision gates** (§6) list information that must be obtained from the client
  — these are the points where the rule **"NEVER GUESS"** is enforced.
- Anything not yet known is marked **`INPUT REQUIRED`**, never assumed.

---

## 2. Guiding Principles

1. **Documentation-first.** No feature is built before its functional spec is
   written and approved. Specs are the single source of build scope.
2. **NEVER GUESS.** Every gap is closed with the client before it enters a spec.
   Unknowns are tracked as decision gates, not assumptions.
3. **Mockup = App scope.** `Mockup.html` (41 screens) is the agreed structural
   scope of the **mobile app**. The **visual UI design is produced by an
   external UI/UX designer** — not by this team.
4. **Control Panel is designed in-house.** There is no external mockup for the
   CP; this team designs its information architecture, screens and UI.
5. **Every feature spans three layers** — every in-scope feature is delivered as
   **API + Backend + Control Panel** (and a Mobile App slice where applicable).
6. **Backend does not wait.** Backend, APIs, the CP, and the Flutter base
   architecture proceed while the external designer prepares the App UI.
7. **Standards & best practices.** DDD, FastEndpoints/`ApiResult<T>`, EF Core,
   NCA/OWASP security, zero-warning builds, tests per change, freeze governance —
   per the global engineering rules.
8. **Freeze governance.** An approved baseline is binding; changes after a gate
   trigger a re-plan and are treated as additional scope.

---

## 3. Fixed Constraints

| Constraint | Value |
|------------|-------|
| Forum dates | 23–25 November 2026 — immovable |
| Operational target | System live ~2 months before the forum |
| Reference plan | 18 weeks (per `Overall Time & Plan.pdf`) |
| Stack | .NET 10 + FastEndpoints, Blazor + MudBlazor, Flutter, SQL Server 2022 |
| Tenancy | Single-tenant |
| Mandatory compliance | NCA ECC-1:2018, CSCC-1:2019, OWASP Top 10 / ASVS |
| App UI ownership | External UI/UX designer |

---

## 4. Workstreams (Parallel Tracks)

| ID | Workstream | Starts | Notes |
|----|-----------|--------|-------|
| WS1 | Backend & APIs | Stage 2 | Runs continuously to project end |
| WS2 | Control Panel (Blazor + MudBlazor) | Stage 2 | Designed in-house; theming AR/EN + multi-theme |
| WS3 | Mobile App (Flutter) | Stage 2 (base) | Base architecture/packages/API layer first; UI integrated after the designer delivers |
| WS4 | Documentation & Architecture | Stage 1 | Produces the professional doc set and detailed specs |
| WS5 | DevOps & Environments | Stage 4 | Azure DevOps, CI/CD, 4 environments |
| WS6 | QA & Security | Stage 2 | Test strategy early; execution intensifies later |

---

## 5. Stages

### Stage 0 — Discovery & Baseline  ·  **STATUS: COMPLETE**
- **Objective:** understand the full document set; fix the concept baseline.
- **Done:** all 13 source documents read; `Mockup.html` confirmed as the app
  scope; `SIMF-Concept-Summary.md` produced (baseline = 2026-05-20 meeting).
- **Gate G0:** concept baseline accepted. ✅

### Stage 1 — Requirements Closure & Analysis
- **Objective:** turn the concept into closed, unambiguous, analysed
  requirements — **no open "guess" items** for any in-scope feature.
- **Inputs:** Concept Summary §15 (deferred items) and §16 (open confirmations).
- **Activities:**
  - Requirement workshops with the client to close every §15/§16 item:
    per-type permissions, "direction/track" meaning, exhibitor/moderator/staff
    workflows, booking & attendance, hall-arrival verification, question
    open/close mechanics, AI comment-filtering rules, AI setting levels, media
    coverage & news detail, statistics detail, legal texts, renamed sections.
  - Design the **Control Panel concept**: module list, information architecture,
    navigation, screen inventory (no external mockup exists for the CP).
  - Confirm the actor/role model and the dynamic-configuration scope.
- **Deliverables:** closed requirements register; CP screen inventory & IA;
  analysis sign-off.
- **Gate G1:** zero open assumptions for in-scope features; client sign-off.

### Stage 2 — Solution Architecture & Professional Documentation Set
- **Objective:** produce the professional engineering documentation a
  development company builds from — standards-grade, no guesswork.
- **Deliverables (the developer documentation package):**
  1. **System Architecture Document** — HLD + LLD: DDD bounded contexts,
     solution/project layout, integration & deployment architecture.
  2. **Functional Requirements Specification (FRS).**
  3. **User & Roles Specification + Permissions Matrix.**
  4. **Use-Case / User-Story Catalogue.**
  5. **Data Model / ERD.**
  6. **API Specification (OpenAPI)** — contract-first.
  7. **Mobile App Architecture & Package Baseline** (Flutter) — folder
     structure, state management, networking/API layer, packages.
  8. **Control Panel UI/UX Design Specification** (CP designed in-house).
  9. **Engineering Standards Pack** — coding standards, security standard
     alignment (NCA/OWASP), Git/branching, Definition of Done, test strategy.
- **Gate G2:** documentation set reviewed and approved; architecture baseline
  frozen for build.

### Stage 3 — Detailed Functional Specifications (CP & App)
- **Objective:** a **detailed functional document per feature** for the Control
  Panel and the App. **This set IS the build scope** — every later task traces
  to one of these specs.
- **Deliverables:** one detailed spec per feature/module, each covering its
  **API + Backend + CP** behaviour (and App screens where applicable):
  acceptance criteria, validation rules, states, permissions, error handling.
  Modules include — authentication, registration & approval, badge & access,
  themes/sessions/halls/speakers/booths/sponsors, venue map, live broadcast &
  questions/comments, networking & cognitive AI, notifications, media coverage
  & news, archive, statistics, and every Control Panel module.
- **Gate G3:** every in-scope feature has an approved detailed spec.

### Stage 4 — Planning, DevOps & Mobilisation
- **Objective:** convert the specs into an executable, resourced plan.
- **Activities:**
  - **Execution schedule / sprint plan** aligned to the November deadline.
  - **DevOps setup** — Azure DevOps repos, boards, pipelines; four environments
    (Dev → Test → Staging → Production); branch policies.
  - **Stakeholder register** — client and vendor stakeholders, RACI.
  - **Agile backlog** — epics → stories → tasks derived from Stage 3 specs.
  - **Team assignment** — roles staffed; tasks allocated to team members.
- **Deliverables:** project schedule, live CI/CD pipeline, stakeholder register,
  populated backlog, onboarded team.
- **Gate G4:** Sprint 1 ready to start.

### Stage 5 — Build & Execution (Sprints)
- **Objective:** build feature by feature; each feature delivered as
  API + Backend + CP, with the App slice integrated once the external designer
  delivers the corresponding UI.
- **Kickoff order (§8).**
- **While awaiting the designer:** WS1/WS2 (Backend, APIs, CP) and the WS3
  Flutter base architecture proceed without interruption.
- **Cadence:** two-week sprints, demo + retrospective each sprint.
- **Gate G5:** per sprint demos and the milestone set.

### Stage 6 — Testing, Hardening, Release & Operation
- **Objective:** 22-day continuous testing, security clearance, store
  publishing, live environment, and operation ~2 months before the forum.
- **Gate G6:** UAT sign-off; security clearance; go-live.

---

## 6. Decision Gates — Inputs Required Before Build

Build cannot proceed past these without client input (the **NEVER GUESS** rule).
All map to `SIMF-Concept-Summary.md` §15–§16:

| Gate | Input required | Needed by |
|------|----------------|-----------|
| D1 | Per-user-type permissions & screens | Stage 1 |
| D2 | "Direction / track" definition | Stage 1 |
| D3 | Exhibitor / Moderator / Staff workflows | Stage 1 |
| D4 | Booking & attendance rules; hall-arrival verification | Stage 1 |
| D5 | Question open/close mechanics; AI comment-filter rules; AI setting levels | Stage 1 |
| D6 | Media Coverage / News / Statistics detail; legal texts; renamed sections | Stage 1 |
| D7 | Cognitive-AI provider; live-broadcast provider; WhatsApp provider | Stage 2 |
| D8 | SQL Server 2022 edition/licensing for the host | Stage 4 |

---

## 7. Stakeholders & Team

> Names/roster to be confirmed at Stage 4 — listed here as structure, not guessed.

**Client / owner side:** MoD / RSNF project sponsor; PR team; Security team;
Technical team; Scientific team; Logistics team. `INPUT REQUIRED` — named
representatives & approval authority.

**Vendor side (STARTIME):** Product Owner, Scrum Master, Solution Architect,
.NET/Backend Engineers, Control Panel (Blazor) Engineer, Flutter Engineer,
DevOps Engineer, QA/Test Engineers, AI Specialist.

**External:** UI/UX Designer (App UI) — dependency on WS3.

---

## 8. Execution Kickoff Order

When Stage 5 begins, build starts in this order:

1. **Login API** — authentication endpoint (email + password), token issuance,
   the security middleware baseline.
2. **Control Panel base** — best-practice theme & template; **Arabic/English
   localization (RTL/LTR)**; **multi-theme** support; layout shell, navigation,
   permission-gated routing.

Subsequent features follow the Stage 3 spec order, each as API + Backend + CP.

---

## 9. Key Risks

| Risk | Mitigation |
|------|------------|
| External UI/UX designer delays the App UI | Backend/CP/Flutter-base proceed independently; App UI integrated late |
| Open §15 items not closed in time | Stage 1 gate G1 blocks build until closed |
| Scope added after a gate | Freeze governance — re-plan + treated as added scope |
| Fixed November deadline | Reference 18-week plan; deadline-aligned schedule at Stage 4 |
| Compliance clearance lead time | Security workstream (WS6) engaged from Stage 2 |

---

## 10. Immediate Next Step

On approval of this plan, **Stage 1 (Requirements Closure & Analysis)** begins —
starting with the requirement workshops to close decision gates D1–D6 and the
Control Panel concept design. No code is written before Gate G3.

---

*End of document — awaiting approval.*
