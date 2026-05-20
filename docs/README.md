# SIMF documentation

Engineering documentation for the SIMF system (Saudi International Maritime
Forum 2026), version V1.0.0.

How the documents work — their format, identifiers, review and approval — is
defined in SIMF-DMP-001. Start there if you are new to the set.

## Document register

| ID | Document | File | Status |
|----|----------|------|--------|
| SIMF-BSP-001 | Base System Plan | [base-system-plan.md](base-system-plan.md) | Draft |
| SIMF-CON-001 | System Concept Summary | [SIMF-Concept-Summary.md](SIMF-Concept-Summary.md) | Draft (baseline) |
| SIMF-PGP-001 | Programme Plan | [SIMF-Program-Plan.md](SIMF-Program-Plan.md) | Approved |
| SIMF-DMP-001 | Documentation Management Plan | [SIMF-DMP-001-Documentation-Management-Plan.md](SIMF-DMP-001-Documentation-Management-Plan.md) | Draft |
| SIMF-SES-001 | Software Engineering Standards | [SIMF-SES-001-Software-Engineering-Standards.md](SIMF-SES-001-Software-Engineering-Standards.md) | Draft |
| SIMF-SAD-001 | Software Architecture Document | [SIMF-SAD-001-Software-Architecture-Document.md](SIMF-SAD-001-Software-Architecture-Document.md) | Draft |
| SIMF-API-001 | API Specification | [SIMF-API-001-API-Specification.md](SIMF-API-001-API-Specification.md) | Draft |
| SIMF-MAA-001 | Mobile Application Architecture | [SIMF-MAA-001-Mobile-Application-Architecture.md](SIMF-MAA-001-Mobile-Application-Architecture.md) | Draft |
| SIMF-SRS-001 | Software Requirements Specification | planned | Blocked on gates D1–D6 |
| SIMF-RPM-001 | Roles and Permissions Specification | planned | Blocked on gate D1 |
| SIMF-UCS-001 | Use Case Specifications | planned | Blocked on gates D1–D6 |
| SIMF-DAT-001 | Data Model and Database Design | planned | Blocked on gates D1–D6 |
| SIMF-CPD-001 | Control Panel Design Specification | planned | Not started |

The register is maintained in SIMF-DMP-001 section 9; this table mirrors it for
quick access.

## Reading order

For the project background, read SIMF-CON-001 (what the system is) and
SIMF-PGP-001 (how it will be built). For engineering, read SIMF-DMP-001 and
SIMF-SES-001 before writing any code or document.

## Working files

`_extract/` holds intermediate text pulled from the original client documents
during analysis. `_templates/` holds the blank controlled-document template.
Neither is a deliverable.
