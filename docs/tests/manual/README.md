# SIMF Manual (Human) Test Pack — 7-Day Production Rehearsal

> **What this is.** A human-run, team-executed acceptance / production-rehearsal
> test pack for the SIMF **Control Panel** and **Mobile App**. It is the
> *people-driven* counterpart to the agent-driven
> [`../e2e/E2E-TEST-PLAN.md`](../e2e/E2E-TEST-PLAN.md): real testers, on real
> devices, against the **live production** stack, over **7 days**, simulating a
> real event — visitor registration, **gate check-in / check-out**, staff,
> moderator, VIP, delegate and admin journeys, every constraint, every policy.
>
> **Authority.** Subordinate to the controlled, approved
> [`../../SIMF-TST-001-Test-Plan.md`](../../SIMF-TST-001-Test-Plan.md). Where the
> two ever disagree, `SIMF-TST-001` wins. This pack is a *living* execution plan,
> not a controlled `SIMF-XXX-NNN` deliverable.

## The three documents

| # | File | What it gives the team |
|---|------|------------------------|
| 1 | [`SIMF-Manual-Test-Plan-7Day.md`](SIMF-Manual-Test-Plan-7Day.md) | **The plan** — the 7-day staged schedule, the 3–4-tester lane model, the live-production rules of engagement, the gate-simulation method (in-app + physical), the device matrix, entry/exit gates, the defect + sign-off process, and the copy-paste **test-log / defect / cleanup** templates. |
| 2 | [`SIMF-Manual-Test-Cases.md`](SIMF-Manual-Test-Cases.md) | **The cases** — the authored-in-full human test cases that the catalogue does **not** cover: the role × permission **policy matrix** (`TC-P`), the cross-cutting **journeys** (`TC-J`, incl. gate check-in/out), the **constraint / validation** matrix (`TC-V`), and the full per-page **execution checklists** for every CP page and App screen, each pointing at its catalogue id range. |
| 3 | [`run-log/`](run-log/) | **The evidence** — one filled-in log per day per tester (copied from the template in doc 1). Created during execution; starts empty. |

## How the team should use it (one paragraph)

The **QA Lead** reads doc 1, stands up the fixtures on Day 1, and assigns lanes.
Each **tester** opens doc 2, runs the cases assigned to their lane for the day,
and records each result (PASS / FAIL / BLOCKED) in their day's run-log with
evidence. A **FAIL** becomes a defect (template in doc 1) logged the same hour.
Every per-page checklist row in doc 2 names the catalogue file under
[`../e2e/`](../e2e/README.md) — open it for the exact field names, the bilingual
toast text, and the error codes to assert. The catalogue **is** the detailed
test-case library; this pack is the *human operating procedure* over it plus the
journeys/policies/constraints that span many pages.

## Relationship to the rest of the test estate

```
SIMF-TST-001 (controlled strategy: all layers, gates, coverage floor)
│
├── tests/SIMF.*.Tests/  ............ unit + integration (gate every commit)
│
├── docs/tests/e2e/  ................ per-page Gherkin catalogue (~1,044 cases)
│     └── E2E-TEST-PLAN.md  ......... agent-driven browser/device pass
│
└── docs/tests/manual/  (THIS PACK)   human team, live prod, 7-day rehearsal
      ├── SIMF-Manual-Test-Plan-7Day.md
      ├── SIMF-Manual-Test-Cases.md
      └── run-log/
```

_Last reviewed: 2026-06-20 by SIMF Team._
