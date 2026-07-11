# SIMF Production-Readiness Test Round (Round 1)

| | |
|--|--|
| **Title** | SIMF Production-Readiness Test Round — charter for a full App + Website + Control Panel pass |
| **Status** | Living plan document (not a controlled `SIMF-XXX-NNN` deliverable) |
| **Strategy parent** | [`SIMF-TST-001-Test-Plan.md`](../SIMF-TST-001-Test-Plan.md) — the controlled, approved Test Plan. This round is **subordinate** to it. |
| **E2E parent** | [`e2e/E2E-TEST-PLAN.md`](e2e/E2E-TEST-PLAN.md) — the per-page E2E execution playbook. |
| **Companions** | [`SIMF-Business-Flows.md`](SIMF-Business-Flows.md) (the new cross-page journeys) · [`SIMF-Production-Readiness-TestBook.xlsx`](SIMF-Production-Readiness-TestBook.xlsx) (the tester workbook) · [`e2e/README.md`](e2e/README.md) (per-page catalogue index) · [`../manuals/Test-Guide.md`](../manuals/Test-Guide.md) (how to run) |
| **Created** | 2026-07-11 |

> **Read this first.** SIMF already owns a deep test system — the controlled
> `SIMF-TST-001` strategy, a **164-file / 2,142-scenario** per-page Gherkin E2E
> catalogue under [`e2e/`](e2e/), the FDS per-feature `T-01…T-NN` scenarios, and
> the xUnit + Flutter automated suites. This round does **not** rebuild any of
> that. It adds the two things that were missing for a human-driven,
> production-readiness sign-off:
>
> 1. a **cross-page business-flow layer** — real end-to-end journeys that thread
>    across CP → App → Website (bulk delegation → badge activation → gate scan →
>    attendance → Q&A → reminder → rating → close-the-year), which the *per-page*
>    catalogue does not capture as single flows; and
> 2. a **consolidated Excel test book** a manual tester drives, with your working
>    columns (*Test / Comment / Time / Status / Developer comment*), that fuses the
>    2,142 existing per-page scenarios **and** the new business flows into one
>    prioritised workbook.

---

## 1. Purpose & goal

Prove the SIMF system is **production-ready with no open high-severity defect**
across all three front-ends (Flutter **App**, Blazor **Website**, Blazor
**Control Panel**) and the **App/Admin API** behind them, by:

- executing a **Round 1** pass over the highest-value flows and the critical
  (P0/P1) per-page scenarios,
- **fixing every bug found** at its root cause with a regression test, and
- leaving a **manual-tester Excel test book** so the QA team can repeat the full
  regression on demand.

This round feeds the `SIMF-TST-001` **per-release** and **UAT/go-live** gates
(§14 of that plan); it does not replace the security pen-test, the load test, or
UAT sign-off, which stay owned by `SIMF-TST-001` §§9–12.

## 2. Scope

### 2.1 In scope

| Surface | Stack | Local URL | How Round 1 exercises it |
|---------|-------|-----------|--------------------------|
| Control Panel | Blazor Server | `http://localhost:5158` | Live browser pass (Chrome DevTools MCP) + API layer |
| Website | Blazor SSR + interactive islands | `http://localhost:5115` | Live browser pass + API layer |
| App / Admin API | .NET 10 FastEndpoints | `http://localhost:5175` | Driven through the front-ends + asserted at the HTTP layer |
| Mobile App | Flutter (Android + iOS) | dev-run / tablet | **Manual** — driven by the human tester from the Excel App sheet (see §6) |

Coverage targets: the **15 business flows** (`E2E-BF-01…15`, see
[`SIMF-Business-Flows.md`](SIMF-Business-Flows.md)), **all P0 + P1** per-page
scenarios on every ✅ Real page, the **permission/security gate** on every gated
CP page and admin endpoint, the **bilingual/RTL** render on every screen, and a
**no-dead-button / no-crash** smoke over every route.

### 2.2 Out of scope for Round 1 (per `SIMF-TST-001` and the freeze notes)

- Performance/load, the NCA pen-test, the formal accessibility audit, and UAT
  sign-off (owned by `SIMF-TST-001` §§9–12).
- **Deferred features** — the attendee-facing **geofence self-check-in** (backend
  built, no app screen), the exact **statistics metric list** (pending D6), and
  the **dynamic `SessionCategory`** list (ships empty pending the client). Their
  cases are marked **Pending**, not Failed.
- **Provider stubs** — the **live-video** provider and the **AI question-filter**
  (a stub by default). Round 1 asserts SIMF-side wiring + the stubbed contract,
  not a real third-party integration.

## 3. Deliverables

1. **This charter** — scope, environment, criteria.
2. [`SIMF-Business-Flows.md`](SIMF-Business-Flows.md) — the 15 cross-page journeys
   in Gherkin, grounded in the real routes/endpoints/rules.
3. [`SIMF-Production-Readiness-TestBook.xlsx`](SIMF-Production-Readiness-TestBook.xlsx)
   — the tester workbook (§5).
4. A **Round-1 run report** (the `SIMF-Completion-Programme-E2E-Results` shape) +
   a populated **Defect log** with a root-cause and fix commit per bug.

## 4. Environment, accounts & data

### 4.1 The local stack

Round 1 runs against the full stack in `Development` against local
`SIMF_Identity` + `SIMF_App` — the recipe is in
[`Test-Guide.md`](../manuals/Test-Guide.md) §12.2 (start each front-end detached;
a green `GET /health` after the API applies migrations + seeder is the ready
signal). **Synthetic data only — no production personal data** (`SIMF-TST-001`
§7). Test rows are cleaned up (soft-delete) or namespaced with a run id so a
re-run finds the same baseline.

### 4.2 Accounts & the no-secrets rule

- **Super-admin** (`UserType = Admin`, TOTP second factor) — every CP scenario.
  Its TOTP code is generated at run time by the PowerShell **`Get-Totp`** helper;
  its email/secret are read from configuration, **never** written into this
  charter, the Excel, a screenshot, or a commit.
- **Visitor** (`UserType = Visitor`, email-OTP) — the Website + App golden paths;
  the OTP is read at run time from `SIMF_Identity.AccountCodes`.
- **Account-state fixtures** — `PendingApproval` / `Rejected` / `Registered` /
  `Disabled` accounts for the state-routing scenarios.
- **App-role fixtures** — a **Staff** and a **Moderator** account (created via
  `/admin/others` on a `ProfileType` whose `MobileAppRole` is `Staff` /
  `Moderator`), for the gate + moderation flows.

> **HARD RULE — no literal secrets, ever.** No file in this round may contain a
> literal TOTP secret, password, API key, token, or connection string. One legacy
> catalogue file (`e2e/cp-auth-flow.md`) still inlines a TOTP secret + password —
> that is a **Round-1 defect to scrub** (logged in the Defect sheet), not a
> pattern to copy.

## 5. The Excel test book

Generated by [`tools/testbook/build_testbook.py`](../../tools/testbook/build_testbook.py)
(Python + openpyxl) from the live catalogue, so it never drifts by hand-copy.
Re-run the script to regenerate after the catalogue changes.

**Choice for this round (owner-approved): _Complete + prioritised_** — every one
of the 2,142 per-page scenarios is present, Priority-tagged, with a
"Round-1 critical (P0/P1)" filter, alongside the fully-authored business flows.

| Sheet | Contents |
|-------|----------|
| **00 · How to use** | Instructions, the stack + accounts, the Status legend, column meanings. |
| **01 · Summary** | Live roll-up (COUNTIF) — totals + pass/fail/blocked per surface and per sheet. |
| **02 · Business Flows** | The 9 feature journeys `E2E-BF-01…09`, **fully authored step-by-step** with inline Gherkin (role, action, test data, expected result). |
| **03 · Control Panel** | Every `cp-*` scenario (id, page, route, title, type, priority, link to the catalogue file). |
| **04 · Website** | Every `web-*` scenario. |
| **05 · Mobile App** | Every `mobile-*` scenario (the human tester's sheet). |
| **06 · Cross-cutting** | The 6 cross-cutting sweeps `E2E-BF-10…15` (full-CP no-dead-button/no-crash + permission gate, full-App smoke, Website smoke, permission/security matrix, bilingual/RTL sweep, notification-kind inventory), inline Gherkin. |
| **07 · Defect log** | Every bug found in Round 1 — id, source scenario, severity, description, root cause, fix commit, status. |

**Columns (every test sheet):**
`ID · Surface · Role · Page/Route · Test (step / scenario) · Test data ·
Expected result (EN + AR) · Priority · Comment · Time (min) · Status ▾ ·
Developer comment · Evidence`.

The five you asked for — **Test, Comment, Time, Status, Developer comment** — are
present; the rest are the minimum a tester needs to run a step unaided. `Status`
is a dropdown (**Not Run / Pass / Fail / Blocked / N-A**) with conditional
formatting; the per-page rows carry a **link to the canonical `e2e/*.md`** file
for the full Gherkin (the `.md` catalogue stays the single source of truth — the
Excel does not duplicate multi-line Gherkin bodies), while the **Business Flows**
and **Cross-cutting** sheets carry the full authored steps inline.

## 6. Execution model — what is automated vs. manual

- **Automated by the driving agent in Round 1:** the **Control Panel** and
  **Website** via Chrome DevTools MCP, the **API layer**, and the existing
  **xUnit + Flutter** suites (`dotnet test SIMF.slnx -c Release`; the Flutter
  package tests). Time-based flows (the 30-min session reminder, end-of-day
  rating) are validated by **seeding times / advancing the worker clock**, not by
  waiting.
- **Manual (human tester on the tablet):** the **Flutter App UI** — the physical
  tablet cannot be driven from the agent session (emulator SurfaceView renders
  black; USB is manual). The Excel **05 · Mobile App** sheet is built for exactly
  this.

## 7. Grounding — the real model behind the flows (verified in code, 2026-07-11)

The business flows are written to the **real** system, which differs from a naive
mental model in a few places. Testers should know these before running:

| Concept | The real model |
|--------|----------------|
| VVIP / VIP / Normal "tier" | **`ProfileType` rows**, not an enum. |
| Bulk delegation badges | `/admin/delegates` → **count per tier** (max **1000**), creates **placeholder** badges (no real name/email) claimed later via **badge activation**. **Audience-only** — you cannot bulk-generate Staff/Moderator badges. There is **no** `/admin/delegations` page. |
| Staff / **Moderator** ("mediator") | An app role carried by **`ProfileType.MobileAppRole`** (created via `/admin/others`), **not** a per-user dropdown. Session moderation also needs a per-session grant at `/admin/session-moderators` (Administrator bypasses it). |
| Gates "for the hall / booth / main gate / …" | Gates have **no kind/type**. They are scoped by **direction (In/Out/Both)** + a **profile-type allow-list**, and are **not linked to halls**. The Main/Booth/Session/Meeting concept lives on **Halls** (`HallPurpose`). Operator = an assigned **admin account**; the app operator also needs the **`Gates.Operate`** grant — a known gap (D-406) worth a negative test. |
| Register + attend a **session** | Book a seat (`Pending` → admin-approve) → **operator hall-door QR arrival** (`/admin/hall-arrivals`) opens a `HallAttendance` row. Booking and attendance are **decoupled** (arriving does not consult the booking). The **app geofence self-check-in is deferred**. |
| Register + attend a **meeting** | Meetings (speaker / delegation / business) are **scheduled / confirmed only** — there is **no meeting check-in / attendance** concept. |
| Live Q&A vs pre-Q&A + AI filter + team filter | 3-stage pipeline: submit → **Pre/Live** phase (by clock) → **Committee queue** (approve/hide/escalate) → **moderator desk** (push/hide/reorder). Timing: **pre = open (no venue gate); live = must be at hall; after end = CLOSED**. The **AI filter is advisory and a STUB by default** — it never hides a question; the real filter needs `SessionQuestions:AiFilterEnabled=true`. |
| Session reminder | `SessionReminderWorker` — fires **30 min before**, in-app, to seat-holders, once per session. |
| Rate on leaving the hall + end of day | Both real: rate-on-leave (on departure, D-713), end-of-session, **end-of-day** (`DayRating` to those who checked in that day), **end-of-programme** (Event + Exhibition + App). |
| Close the exhibition + "history this year" | **No single close action.** It is **three ops**: `/admin/archive` **snapshot-current** (one edition per year, counters from live data) → **archive visibility** toggle → **manually** set the forum status to Archived. |

## 8. Entry / exit criteria

**Entry (before the pass starts):** release build clean
(`dotnet build -c Release` → 0/0); unit + integration + Flutter suites green; the
stack boots and `GET /health` → 200; the seeder has run.

**Exit (Round 1 is "passed"):** every **P0** scenario in scope passes; every
**P1** passes or has a logged defect with a fix; the **business flows** all pass;
**no open high-severity defect**; the Defect log is complete with a root-cause and
fix commit per bug; a run report is produced.

## 9. Defect handling (per `SIMF-TST-001` §13 + global §17)

1. **Log** every bug in the Defect sheet against the failing scenario/flow id,
   with expected-vs-actual, evidence, and a severity.
2. **Fix at the root cause** (not the symptom), **one issue per change**, no
   bundled refactors.
3. **Add a regression test** at the lowest layer that can hold it.
4. **Re-test** the scenario + the page's P0 set; flip the Status cell.
5. A failing scenario is **never** weakened or skipped to make a pass go green.

Owner-approved for this round: **fix every severity** found (not only P0/P1),
each with the discipline above.

---

_Created 2026-07-11 by the SIMF Team. Living document — tracks
[`SIMF-Business-Flows.md`](SIMF-Business-Flows.md) and the Excel test book; defers
to `SIMF-TST-001` for strategy._
