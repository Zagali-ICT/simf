# Test Plan

| Field | Value |
|-------|-------|
| Document ID | SIMF-TST-001 |
| Title | Test Plan |
| Version | 1.1 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | QA Lead |
| Approver | Project Owner (MoD / RSNF representative) |
| Date issued | 2026-05-21 |
| Related documents | SIMF-SES-001, SIMF-SRS-001, SIMF-UCS-001, SIMF-OPS-001, SIMF-PGP-001, the SIMF-FDS series |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-21 | Engineering & Architecture Team | First issue. |
| 1.1 | 2026-05-21 | Engineering & Architecture Team | §5: stated explicitly that .NET test assertions use xUnit's built-in `Assert`; FluentAssertions is not adopted (its v8 commercial-licence model). |

---

## 1. Purpose

This document defines how SIMF is tested: the test layers, the coverage
expected, how tests trace to requirements, and the gates a release passes. It
fixes the coverage floor and the test tooling left open in SIMF-SES-001.

## 2. Scope

The plan covers functional testing of all SIMF features, security testing,
performance and load testing, accessibility and localisation testing, user
acceptance testing, defect management, and the test gates and deliverables.

It applies to the three deliverables — the backend and API, the Control Panel,
and the mobile app. The per-feature test scenarios live in the SIMF-FDS series;
this plan is the strategy above them.

## 3. Test approach

SIMF is tested at three layers, as set in SIMF-SES-001 section 11. No feature
ships unless all three pass and keep passing.

| Layer | Covers | Runs |
|-------|--------|------|
| Unit | A method or class in isolation — every branch, edge case and error path | On every commit |
| Integration | An API endpoint end to end — the happy path and every error code it returns | On every commit / pipeline run |
| End-to-end | A full user scenario, including failure and recovery | In the pipeline and before a release |

Two rules from SIMF-SES-001 hold: a behaviour change ships with its tests in the
same pull request, and every fixed bug gets a regression test that fails before
the fix and passes after it.

## 4. Coverage

- Coverage is measured and reported on every pipeline run.
- The **floor** is **80% line coverage on the Domain and Application layers** of
  the backend, and on the domain and repository code of the Flutter app.
- The floor is a floor, not the goal. Honest coverage of the paths that matter
  beats a high figure over shallow tests (SIMF-SES-001 section 11.3). A pull
  request that drops coverage below the floor does not merge.
- The API and Control Panel surface code is covered by the integration and
  end-to-end layers rather than by a line-coverage figure.

## 5. Test tooling

The tools below are the project's test toolset. They are mainstream choices;
a change is agreed and recorded here before it is made.

| Area | Tool |
|------|------|
| .NET unit and integration tests | xUnit |
| .NET test assertions | xUnit's built-in `Assert`. FluentAssertions is not used — its v8 licence model is commercial; xUnit's assertions are sufficient and dependency-free. |
| .NET coverage | Coverlet, reported in the pipeline |
| .NET integration tests | Run against a dedicated test database |
| Web end-to-end tests | Playwright |
| Flutter unit and widget tests | The Flutter test framework |
| Flutter integration tests | The Flutter integration-test framework |
| Test management and traceability | Azure DevOps Test Plans |
| Security testing | A vulnerability scanner plus penetration testing by an NCA-accredited firm |

## 6. Test traceability

- Every requirement (`FR-`, `NFR-`) and every use case (`UC-`) is traceable to
  the tests that cover it. The SIMF-FDS series carries the per-feature test
  scenarios; Azure DevOps Test Plans links them to the requirements.
- A changed backend file carries a `// Tests:` header naming the tests that
  cover the change, so the link from code to test is visible in the file
  (SIMF-SES-001 section 11.2).
- A test gap against an accepted requirement is itself a defect.

## 7. Test environment and data

- Functional and integration testing run in the **Test** environment; the
  end-to-end and acceptance testing rehearse in **Staging**, which mirrors
  Production (SIMF-OPS-001 section 4).
- The Test environment is stood up from day one of the project (SIMF-PGP-001).
- Test data is synthetic; no production personal data is used in a test
  environment. Test accounts and test data are removed before anything is
  promoted to Production (SIMF-SES-001 section 12).

## 8. Functional testing

Each feature is tested against the scenarios in its design specification:

| Feature | Spec | Scenarios |
|---------|------|-----------|
| Authentication & Login | SIMF-FDS-001 | T-01…T-20 |
| Registration & Approval | SIMF-FDS-002 | T-01…T-17 |
| Badge & Access Control | SIMF-FDS-003 | T-01…T-13 |
| Forum Programme | SIMF-FDS-004 | T-01…T-13 |
| Bookings & Attendance | SIMF-FDS-005 | T-01…T-14 |
| Exhibition | SIMF-FDS-006 | T-01…T-10 |
| Engagement | SIMF-FDS-007 | T-01…T-14 |
| Networking & Cognitive AI | SIMF-FDS-008 | T-01…T-12 |
| Notifications | SIMF-FDS-009 | T-01…T-10 |
| Media, News & Archive | SIMF-FDS-010 | T-01…T-09 |
| Statistics & Dashboards | SIMF-FDS-011 | T-01…T-10 |
| Control Panel Configuration | SIMF-FDS-012 | T-01…T-11 |

Each scenario is run at the layer that fits it, and each feature's acceptance
criteria must all pass before the feature is accepted (SIMF-DMP-001).

## 9. Security testing

Security testing implements the NCA Secure Application Development Standard
(SIMF-SES-001 section 12, SIMF-SAD-001 section 8):

- **Static and dynamic analysis** in the pipeline against the OWASP Top 10.
- A **peer security review** of the source code before it goes to production.
- **Vulnerability assessment and penetration testing** before and after the
  production deployment, by an NCA-accredited firm.
- A **secure-code review by the MoD cyber centre** before the code is submitted
  for penetration testing (SIMF-OPS-001 section 12).
- Authorisation is tested on every endpoint — a request without the required
  permission is rejected.

Security testing has its own defect track; a security defect is treated at the
severity its risk warrants.

## 10. Performance and load testing

Per the technical requirements and SIMF-OPS-001 section 11:

- a **load test** that adds a new registered user roughly every 30 seconds,
- a **traffic test** under real load before the launch.

Performance testing confirms the non-functional requirement NFR-04 and runs in
an environment that mirrors Production.

## 11. Accessibility and localisation testing

- Every screen is tested in **Arabic (RTL)** and **English (LTR)**; key screens
  are checked in Arabic for mirrored-layout faults (SIMF-MAA-001 section 14).
- No user-facing string is hardcoded; the localisation is verified.
- The mobile app's accessibility settings (font size, contrast, reduced motion,
  screen reader, captions) are tested.
- Colour is never the only signal of a state (SIMF-CPD-001 section 14).

## 12. User acceptance testing

- UAT is run with the client before go-live, against the requirements and the
  feature acceptance criteria.
- A feature is accepted at its sprint demo when its acceptance criteria pass and
  the client signs off (SIMF-PGP-001).
- UAT sign-off is recorded and is part of the test deliverables.

## 13. Defect management

- Defects are logged in Azure DevOps Boards, with a severity and a link to the
  requirement or scenario they breach.
- A defect is fixed at its root cause, not patched at the symptom
  (SIMF-SES-001 section 13), and the fix carries a regression test.
- A failing test is never weakened or skipped to make a build pass; it is
  investigated.

## 14. Test gates

| Gate | Entry | Exit |
|------|-------|------|
| Per commit | Code compiles | Unit and integration tests pass; coverage holds the floor |
| Per feature | The feature is built | All its FDS scenarios and acceptance criteria pass |
| Per release | The build is green | End-to-end tests pass; no open high-severity defect; security checks pass |
| Go-live | The release is ready | Performance tests pass; the security clearances are in hand (SIMF-OPS-001 section 12); UAT is signed off |

A gate that does not pass stops the work behind it.

## 15. Test deliverables

- coverage reports from the pipeline,
- the defect log,
- the test-run results in Azure DevOps Test Plans,
- the security testing report,
- the performance and load test results,
- the UAT sign-off records.

These are version-controlled and reviewed at the sprint demos (SIMF-PGP-001).

## 16. Roles and responsibilities

- The **QA Lead** owns this plan, the test strategy and the gates.
- **QA / Test Engineers** write and run the tests; the plan staffs 14 testers
  for the continuous-testing phase (SIMF-PGP-001).
- **Engineers** write the unit, integration and end-to-end tests for their
  changes (SIMF-SES-001).
- The **DevOps Engineer** keeps the test stages in the pipeline.
- An **NCA-accredited firm** performs the penetration testing.
- The **Project Owner** signs off UAT and go-live.

## 17. Open items

| ID | Item | Affects |
|----|------|---------|
| OI-1 | Confirm the coverage floor (80% proposed) with the Solution Architect | Section 4 |
| OI-2 | Confirm the vulnerability scanner and the NCA-accredited penetration-testing firm | Sections 5, 9 |
| OI-3 | Confirm the UAT schedule and the client's UAT participants | Section 12 |
| OI-4 | Confirm document classification with the owner | Control block |

---

End of document.
