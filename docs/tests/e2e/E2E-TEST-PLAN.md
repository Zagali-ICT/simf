# SIMF End-to-End (E2E) Test Plan

| | |
|--|--|
| **Title** | SIMF End-to-End (E2E) Test Plan — execution plan for the per-page catalogue |
| **Status** | Living plan document (not a controlled `SIMF-XXX-NNN` deliverable) |
| **Authority** | D-133 / D-245 (the E2E catalogue); project `CLAUDE.md` § "E2E test-case catalogue (D-133 / D-245)" |
| **Strategy parent** | [`SIMF-TST-001-Test-Plan.md`](../../SIMF-TST-001-Test-Plan.md) — the controlled, approved Test Plan. This plan is **subordinate** to it and adds detail for the E2E layer only; it does not restate or override it. |
| **Companions** | [`README.md`](README.md) (catalogue index) · [`_TEMPLATE.md`](_TEMPLATE.md) (per-page file template) · [`../../manuals/Test-Guide.md`](../../manuals/Test-Guide.md) (how to run + extend) |
| **Last reviewed** | 2026-06-04 |

> **Scope of this document.** `SIMF-TST-001` is the strategy across **all** test
> layers (unit, integration, E2E, security, performance, accessibility, UAT) and
> is the binding authority for coverage floors, tooling, gates and traceability.
> **This** document is the practical playbook for the **end-to-end layer only**:
> the *how / when / who / pass-fail* of running the per-page Gherkin catalogue
> under `docs/tests/e2e/` as a full regression pass that proves the system is
> production-ready. Where the two ever appear to disagree, `SIMF-TST-001` wins.

---

## 1. Purpose & scope

### 1.1 What this plan is for

SIMF maintains a **per-page E2E test-case catalogue** — one Gherkin file per page
under `docs/tests/e2e/` (`{cp|web|mobile}-{slug}.md`), each with a Coverage matrix
and concrete, data-bearing scenarios with stable ids `E2E-{NS}-{NNN}`. Those
files are the **executable cases**. This plan is the wrapper around them: it says
who runs them, on what stack, with what data, on what cadence, and what
pass / fail / done means.

The catalogue's stated purpose (project `CLAUDE.md`; catalogue `README.md`) is the
north star of this plan:

> After a batch of fixes, an agent reads **every** case and drives each page —
> enters real data, performs each CRUD/action, asserts each expected outcome — as
> a full regression pass that proves production-readiness.

### 1.2 Surfaces in scope

The E2E pass exercises the running system through its real front-ends and API:

| Surface | Stack | Local URL | Catalogue prefix |
|---------|-------|-----------|------------------|
| Control Panel | Blazor Server | `http://localhost:5158` | `cp-*.md` |
| Website | Blazor SSR + interactive auth islands | `http://localhost:5115` | `web-*.md` |
| Mobile app | Flutter (Android + iOS) | dev-run on emulator / device | `mobile-*.md` |
| App / Admin API | .NET 10 FastEndpoints | `http://localhost:5175` | (driven *through* the three front-ends; also asserted at the HTTP layer) |

The two physically separated databases — `SIMF_Identity` (`SimfIdentityDbContext`)
and `SIMF_App` (`SimfAppDbContext`) — back the API. The E2E pass treats them as a
black box reached through the API and only reads them directly for the specific
fixture/assertion hooks called out in §3 and §4 (for example, reading a one-time
email-OTP from `SIMF_Identity.AccountCodes`, or asserting an `OperationLog` /
`RowAudit` / `GateScan` audit row).

### 1.3 What "E2E" means here vs unit / integration

`SIMF-TST-001` §3 and SES-001 §11.1 define three layers. The boundary this plan
operates at:

- **Unit tests** — `tests/SIMF.Domain.Tests`, `tests/SIMF.Application.Tests`:
  pure entity / value-object rules and service logic with mocked repositories. No
  DB, no browser. **Not run by this plan** (they gate every commit).
- **Integration tests** — `tests/SIMF.Api.Tests`: each endpoint + its policy +
  its validator, hosted via `SimfApiFactory` (`WebApplicationFactory`) against a
  real SQL Server LocalDB with migrations + seeder applied. This is where **most**
  coverage lives, and every per-page catalogue file cross-references the xUnit
  cases that already cover its surface at this lower layer. **Not run by this
  plan** (they also gate every commit). Blazor component tests live in
  `tests/SIMF.ControlPanel.Tests` + `tests/SIMF.Web.Tests`; the typed client in
  `tests/SIMF.ApiClient.Tests`; Flutter widget/controller tests next to each
  package under `src/Mobile/`.
- **End-to-end (this plan)** — a **full user scenario driven through the real
  running front-end in a browser / on a device**, including failure and recovery:
  navigation, the cookie / token hand-off, inline validation text, the bilingual
  toast/banner copy, RTL mirroring, and the audit row written as a side effect —
  the assertions the lower layers structurally cannot reach. The canonical runner
  today is **Chrome DevTools MCP + the PowerShell `Get-Totp` helper**; per
  `SIMF-TST-001` §5 and the catalogue `README.md`, **Playwright** is the adopted
  web E2E tool and the runner-agnostic Gherkin shape copies 1-to-1 into
  `.feature` files under a future `tests/SIMF.E2E.Tests/` project when it lands.

---

## 2. Relationship to the per-page catalogue

The catalogue **is** the test suite; this plan governs running it.

- **The cases.** Each `docs/tests/e2e/{cp|web|mobile}-{slug}.md` file owns a
  unique 3–4 letter namespace and a stable id range `E2E-{NS}-{NNN}`
  (e.g. `E2E-INT-001` for `/admin/interests`, `E2E-WLG-001` for the Website
  `/login`, `E2E-MOB003-001` for mobile screen #3). Ids are **stable** — a
  removed scenario retires its id and the id is never reused.
- **The index.** [`README.md`](README.md) maps every route → file → scenario id
  range so ~1,000 scenarios across 74 pages are browsable without opening every
  file. It also carries the coverage gate: **every ✅ Real page in
  [`PAGE-INDEX.md`](../../pages/PAGE-INDEX.md) has a per-page catalogue file with
  ≥ 1 P0 scenario.**
- **The template.** [`_TEMPLATE.md`](_TEMPLATE.md) fixes the per-page shape —
  front-matter, Coverage matrix, then one Gherkin block per scenario — and the
  default coverage spread (golden / empty / auth-gate / validation / conflict /
  server-500 / RTL).
- **This plan** adds nothing to those files. It defines the operation that reads
  them and the criteria that close them. When a scenario passes or fails, the
  result is recorded back in **its own file's** Coverage-matrix `Status` column
  (§9), not here.

---

## 3. Test environment & toolchain

### 3.1 The local stack

The E2E pass runs against the full stack in `Development`, mirroring
[`Test-Guide.md`](../../manuals/Test-Guide.md) §12.2:

| Component | How it is started | Port |
|-----------|-------------------|------|
| API | `dotnet run -c Release` in `src/Backend/SIMF.Api` | `5175` |
| Control Panel | `dotnet run -c Release` in `src/ControlPanel/SIMF.ControlPanel` | `5158` |
| Website | `dotnet run -c Release` in `src/Website/SIMF.Web` | `5115` |
| Databases | local SQL Server (`Server=.`): `SIMF_Identity` + `SIMF_App` | — |

On first `/health`, the API applies the `InitialCreate` + additive migrations on
both contexts and runs the seeder (super-admin + permission catalogue + content),
so a green `/health` is the signal the stack is ready. Start each front-end
**detached** (so the agent harness does not kill it) and tear the stack down by
port when the pass ends — the exact `Start-Process` / `Get-NetTCPConnection`
recipe is in [`Test-Guide.md`](../../manuals/Test-Guide.md) §12.2.

### 3.2 Mobile dev-run

Flutter `mobile-*` scenarios run the app against the same local API. The app
reaches the host API on `http://10.0.2.2:5175` from the Android emulator (and
`http://localhost:5175` on a paired device), with cleartext allowed only in the
debug manifest. Widget + controller tests run from each package root under
`src/Mobile/` with the Flutter test framework; the on-device drive (and any
native-only step such as the biometric prompt) is the `simf-run` follow-up noted
in the mobile catalogue files. Do **not** run `dart format` in this tree (it
breaks `require_trailing_commas`).

### 3.3 Browser automation

The canonical web runner today is **Chrome DevTools MCP**: navigate the route,
fill fields, click, read the rendered toast/banner, inspect the network panel for
the expected `ApiResult<T>` status codes, and capture before/after screenshots
into `docs/screenshots/{slug}-{scenario}-{before|after}.png`. Keep every step
**tool-agnostic** in the catalogue so it ports straight to Playwright (the
adopted tool per `SIMF-TST-001` §5) when `tests/SIMF.E2E.Tests/` is created.

### 3.4 Seeded accounts, roles & the permission model

- **Super-admin** — the seeded Administrator (`UserType = Admin`), with a TOTP
  second factor. Used for every CP scenario. Its email + TOTP secret are read
  from configuration / the seeder, **never** written into this plan or any
  catalogue file (§3.5).
- **Visitor** — an approved `UserType = Visitor` account used for the Website and
  mobile golden paths; its second factor is **email-OTP**, read at run time from
  `SIMF_Identity.AccountCodes` (`Purpose = SignInOtp`, latest unconsumed). The
  2FA rule is: when 2FA is on, a visitor gets an emailed OTP and an admin gets a
  TOTP challenge (D-033).
- **Account-state fixtures** — additional Visitor/Admin accounts in
  `PendingApproval` / `Rejected` / `Registered` (unverified) / `Disabled` to
  exercise the state-routing scenarios.
- **Permission model.** The CP and admin API enforce a **per-page / per-action**
  permission system (D-207 / D-208): assignment is **roles-only**, permission
  codes are baked into the JWT, and `Administrator = "*"` (wildcard). The single
  source of truth is `src/Shared/SIMF.Common/PermissionCatalog.cs`. Every CP page
  is gated with `@attribute [RequirePermission(...)]` and every admin endpoint
  with `Policies(PermissionCatalog.PolicyFor(...), …)`. The **auth-gate** scenario
  on each CP page (a signed-in user *without* the page's permission →
  `/not-permitted`, HTTP 200) is the E2E proof that this gate is wired; the
  Website analogue is the **audience gate** (`AUTH_WRONG_SURFACE_WEB`) plus
  account-state routing, since `/login` is `AllowAnonymous`.

### 3.5 The `Get-Totp` helper — and the no-secrets rule

The TOTP second factor for the super-admin is generated at run time by the
PowerShell **`Get-Totp`** helper (the function sits at the head of every
chrome-devtools-mcp session in this repo; paste it from the Developer Guide
§20.4 / [`Test-Guide.md`](../../manuals/Test-Guide.md) §12.2):

```gherkin
When they generate a TOTP via the Get-Totp helper for the super-admin's secret
And they fill that 6-digit code
And they click "Verify"
```

**HARD RULE — no literal secrets, ever.** No catalogue file, this plan, a
screenshot, a log, or a commit may contain a literal TOTP secret, password, API
key, token, or connection string. Auth-setup lines reference the **`Get-Totp`
helper** and read OTPs from the DB at run time; they never inline a code. (One
legacy catalogue file still inlines credentials in its front-matter; that is a
defect to be scrubbed, not a pattern to copy — the rule here is binding.)

---

## 4. Test-data strategy

### 4.1 Real, data-bearing scenarios

Every scenario uses **concrete, realistic data**, not placeholders — real field
names, real values, and the **exact bilingual** toast/error/banner text the page
emits (e.g. create an interest `Name="Naval Engineering"` /
`Name (Arabic)="الهندسة البحرية"`; assert the green toast *Interest "Naval
Engineering" was created.*). Synthetic data only — **no production personal data**
in any test environment (`SIMF-TST-001` §7).

### 4.2 Idempotency & cleanup

The golden CRUD path is designed to **return the page to its starting state**:
a create is followed by edit → details → deactivate (soft-delete via
`entity.Deactivate()` → `IsActive = false`), so a re-run finds the same baseline.
Where a scenario leaves a row behind, the run notes it and the data is removed
before promotion to Production (`SIMF-TST-001` §7; SES-001 §12). Prefer
scenario-unique values (suffix a run id) so concurrent or repeated runs do not
collide on a unique-name constraint.

### 4.3 Respecting the SIMF_App ↔ SIMF_Identity separation (D-157)

The two databases are **physically separate** and that separation is permanent
(D-157): no cross-DB FK, no duplicated live data, no cross-DB transaction. The
E2E pass must honour it:

- A cross-context reference is a **bare `Guid`** resolved on read — assert it the
  way the app does (a second query on the other context), never expect a cross-DB
  JOIN.
- Reading the email-OTP from `SIMF_Identity.AccountCodes` or asserting an audit
  row is allowed (those are existing, intended hooks); do **not** invent fixtures
  that write Identity-owned data into `SIMF_App` or vice versa.
- The only sanctioned data copies are the immutable audit snapshots
  (`OperationLog` / `RowAudit` / `GateScan` capturing the actor's
  display-name/email at write time) — assert against them, don't extend the
  pattern.

### 4.4 Bilingual / RTL data

Every page carries an **RTL render** scenario: switch to Arabic, assert
`<html dir="rtl" lang="ar">`, the Arabic titles/labels, mirrored nav and reversed
form-action order, and that LTR-only fields (e.g. email) stay LTR. Arabic data is
entered for the bilingual `Name`/`NameArabic` fields so the round-trip and the
RTL render are both proven. SIMF is **Arabic-first**; the RTL scenario is P1, not
an afterthought.

---

## 5. Execution model

### 5.1 The agent-driven regression pass

The defining operation of this plan:

1. **Bring up the stack** (API :5175, CP :5158, Website :5115) in `Development`
   against local `SIMF_Identity` + `SIMF_App`; wait for `GET /health` → 200.
2. **Sign in** as the seeded super-admin — password, then TOTP via the
   **`Get-Totp`** helper (§3.5).
3. **For each `docs/tests/e2e/{page}.md`**, read every scenario and **drive the
   page**: enter the real data, perform each CRUD/action, and **assert each
   expected outcome** — rendered text, navigation, network status, console
   cleanliness, and the audit side effect. Capture before/after screenshots into
   `docs/screenshots/`.
4. **Record pass/fail** per `E2E-{NS}-{NNN}` id back into that page's
   Coverage-matrix `Status` column (§9).

This is exactly the operation described in
[`SIMF-Completion-Programme-E2E-Results.md`](../../SIMF-Completion-Programme-E2E-Results.md)
§3 ("How to run the full end-to-end browser pass"); that document is the template
for the run report this plan produces.

### 5.2 Cadence — when the pass runs

| Trigger | Scope of the pass |
|---------|-------------------|
| **After a single page is built / materially changed** | That page's catalogue file end-to-end (every `E2E-{NS}-{NNN}` for the page), plus any page whose data it touches. This is the per-page DoD check (§7). |
| **After a batch of fixes / a sprint** | The catalogue files for every page touched in the batch, plus a P0 sweep of adjacent pages — the "after a batch of fixes" regression the catalogue exists for. |
| **Before a release / handover** | The **full** catalogue — all P0 + P1 scenarios on all 74 pages — as the production-readiness proof. P2 (server-500 / throttle / double-submit resilience) at least once in the release window. |

This sits inside `SIMF-TST-001` §14 test gates: per-commit (unit + integration)
→ per-feature (FDS scenarios) → **per-release (E2E pass, this plan)** → go-live
(performance + security + UAT).

### 5.3 Lower-layer regression runs first

The E2E pass assumes the automated layers are already green. Before a batch/release
pass, run the xUnit + Flutter suites — `dotnet test SIMF.slnx -c Release` (0
warnings / 0 errors; all suites 0 failures) and the Flutter package tests — and
only then drive the browser/device. The last recorded automated snapshot is 836
passing / 0 failures (`SIMF-Completion-Programme-E2E-Results.md` §1); a red
lower layer blocks the E2E pass.

---

## 6. Coverage model

### 6.1 The per-page coverage spread

Every page's catalogue file covers, at minimum, the spread fixed by
[`_TEMPLATE.md`](_TEMPLATE.md) — the **golden CRUD path** plus **every distinct
function/action on the page** plus these standard edges:

| Type | What it proves | Default priority |
|------|----------------|------------------|
| `happy` — golden path | The primary CRUD round-trip (Add → Edit → Details → Deactivate) succeeds with real data and the right bilingual toast | **P0** |
| `happy` — empty state | An empty list renders `SimfEmptyState` with bilingual copy, no error toast | P1 |
| `auth` — auth gate | A user without the page's permission → `/not-permitted` (CP) / audience- or state-route (Website / mobile) | **P0** |
| `error` — validation | An invalid submit shows the inline / `SimfAlert` bilingual error and fires no write | P1 |
| `error` — conflict | A duplicate / illegal-state submit returns the right code (e.g. 409 `…NotUnique`) + bilingual server message | P1 |
| `resilience` — server 500 | An API 500 surfaces the bilingual fallback toast and renders no rows | P2 |
| `i18n` — RTL render | Arabic toggle mirrors page + modal, Arabic labels, reversed actions | P1 |

Pages add scenarios beyond this spread for every extra action they expose
(7–21 scenarios per page in practice; ~1,000 total across the catalogue).

### 6.2 Priority semantics for the pass

- **P0** must pass for the page to be considered working; a P0 failure blocks the
  release gate (§7).
- **P1** must pass for a release pass; a P1 failure is a defect with a fix-and-
  retest loop (§8) before go-live.
- **P2** (resilience) is exercised at least once in the release window; a P2
  failure is logged and risk-assessed but does not by itself block go-live unless
  it masks a P0/P1 defect.

### 6.3 The id scheme & file convention

- **Files:** `{cp|web|mobile}-{slug}.md`, one per page, under `docs/tests/e2e/`.
- **Ids:** `E2E-{NS}-{NNN}` — `{NS}` is the page's unique 3–4 letter namespace,
  `{NNN}` a zero-padded sequence (mobile screens carry the screen number, e.g.
  `E2E-MOB003-001`). Ids are stable and never reused.
- **Index:** every file + its id range is listed in [`README.md`](README.md) and
  cross-linked from [`PAGE-INDEX.md`](../../pages/PAGE-INDEX.md) (doc + test
  columns) and the per-page reference doc under `docs/pages/{cp|web}/{slug}.md`.

---

## 7. Entry / exit criteria & Definition-of-Done linkage

### 7.1 Entry criteria (before an E2E pass starts)

- Release build is clean (`dotnet build -c Release` → 0 warnings / 0 errors).
- The unit + integration + Flutter suites are **green** (§5.3).
- The stack boots and `GET /health` → 200 against `SIMF_Identity` + `SIMF_App`.
- The seeder has run (super-admin + permission catalogue + content present).
- Every page in scope has an **authored** catalogue file (not a stub).

### 7.2 Exit criteria (an E2E pass is "passed")

- For the pass scope (§5.2): **every P0 scenario passes**, every P1 scenario
  passes or has a tracked defect with a remediation owner, and P2 has been
  exercised in the release window.
- No console errors / network failures beyond those a scenario explicitly
  expects.
- Results are recorded per `E2E-{NS}-{NNN}` id (§9) and a run report (the
  `SIMF-Completion-Programme-E2E-Results.md` shape) is produced for a
  batch/release pass.
- No open **high-severity** defect against an in-scope page (mirrors
  `SIMF-TST-001` §14 per-release exit).

### 7.3 Definition-of-Done linkage (D-246)

A page / screen / Website page / admin API action is **not "done"** until — in the
**same changeset** (D-246; project `CLAUDE.md`):

1. its **docs** are updated (`PAGE-INDEX.md` + the per-page reference doc),
2. its **unit + integration tests** exist and pass, **and**
3. its **E2E catalogue file** exists, is **authored** (not a stub), is **indexed**
   in `README.md`, and is **cross-linked** from `PAGE-INDEX.md`.

This plan's per-page cadence (§5.2, row 1) is how (3) is verified: a shipped page
with no authored, *passing* catalogue file is an incomplete change. For a CP page
or admin action, the permission gate (D-207 / D-208) must also exist, be seeded,
and gate **both** API and CP — and the page's **auth-gate scenario** is the E2E
proof of it.

---

## 8. Roles & responsibilities; defect handling

### 8.1 Roles (within `SIMF-TST-001` §16)

| Role | E2E responsibility |
|------|--------------------|
| **QA Lead** | Owns this plan and the E2E gate; schedules the batch/release passes; signs off the run report. |
| **QA / Test Engineer (or the driving agent)** | Runs the pass — reads every case, drives each page, asserts each outcome, records pass/fail + evidence. |
| **Engineer (author of the change)** | Authors/updates the catalogue file in the same changeset (D-246); fixes E2E defects at root cause with a regression test. |
| **DevOps Engineer** | Keeps the stack reproducible (env, the two DBs, seed) and wires Playwright into the pipeline when `tests/SIMF.E2E.Tests/` is adopted. |
| **Project Owner** | Signs off the release / UAT gate the full E2E pass feeds (`SIMF-TST-001` §12, §14). |

### 8.2 Defect handling & re-test loop

1. **Log** the defect against the failing `E2E-{NS}-{NNN}` id, with the page, the
   expected-vs-actual, evidence (screenshots / network / console), and a severity
   — in Azure DevOps Boards per `SIMF-TST-001` §13.
2. **Fix at the root cause**, not the symptom (SES-001 §13), in the page's own
   code; bundle no unrelated changes.
3. **Add a regression test** at the lowest layer that can hold it (usually an
   `SIMF.Api.Tests` case or a widget test) so the bug cannot silently return, per
   SES-001 §11.2.
4. **Re-test** the failing scenario, then re-run the page's P0 set to confirm no
   regression, and flip the `Status` cell back to passing with the new date.
5. A failing scenario is **never** weakened or skipped to make a pass go green
   (`SIMF-TST-001` §13).

---

## 9. Reporting & traceability

### 9.1 Per-scenario result

The result of each scenario is recorded in **its own catalogue file's** Coverage
matrix `Status` column, keyed by the stable `E2E-{NS}-{NNN}` id — e.g.
`smoked manually 2026-05-28`, `authored ✓ (widget + controller tests)`, or a
dated pass/fail with a defect link. The `README.md` snapshot section carries the
roll-up (pages catalogued, total scenarios, authored/executed status) for the
catalogue as a whole.

### 9.2 Evidence

Each scenario's `**Evidence captured:**` block names what to capture: before/after
screenshots under `docs/screenshots/{slug}-{scenario}-{before|after}.png`, the
expected console-error count (0 unless stated), the expected network status codes
(the `ApiResult<T>` envelope per route), and the audit side effect
(`OperationLog` / `RowAudit` / `GateScan` row with the actor's id and the event
key). A batch/release pass produces a consolidated run report in the shape of
[`SIMF-Completion-Programme-E2E-Results.md`](../../SIMF-Completion-Programme-E2E-Results.md).

### 9.3 Traceability back to pages & requirements

- **Scenario → page:** the `{cp|web|mobile}-{slug}` file name and its
  `PAGE-INDEX.md` row tie every id to exactly one route.
- **Scenario → lower-layer tests:** each file's "Implementation notes" name the
  `tests/SIMF.Api.Tests/*` (and Blazor/Flutter) cases that cover the same surface
  without a browser, so the E2E layer and the integration layer are linked.
- **Scenario → requirement:** through the page and the FDS series — per-feature
  `FR-`/`NFR-`/`UC-` scenarios live in the SIMF-FDS specs and `SIMF-TST-001` §8;
  Azure DevOps Test Plans is the system of record that links requirements to
  test runs (`SIMF-TST-001` §6). This plan's E2E ids are the front-end, browser-
  level evidence under that traceability chain.

---

## 10. Risks & out of scope

### 10.1 Risks

- **No automated web E2E runner yet.** The pass is a manual Chrome DevTools MCP
  smoke today; it is repeatable but operator-driven, so it is slower and easier
  to skip than the xUnit suites. *Mitigation:* the Gherkin is runner-agnostic and
  ports 1-to-1 to Playwright; adopt `tests/SIMF.E2E.Tests/` and wire it into the
  pipeline (`SIMF-TST-001` §5).
- **Known flakes** (`Test-Guide.md` §14): the long-running xUnit run can exceed
  the access-token lifetime (`NotificationTests`), and cropper UX is not bUnit-
  covered (manual smoke is canonical). These are lower-layer issues but block the
  E2E entry criterion (§7.1) if the suite is red — run the affected tests in
  isolation / advance `FakeTimeProvider`.
- **Shared-branch concurrency.** Multiple workers on a branch can sweep unrelated
  files into a commit; the run report and any catalogue `Status` edits must be
  staged narrowly (never `git add -A`).
- **Test data leakage.** Synthetic data and test accounts must be removed before
  Production promotion (`SIMF-TST-001` §7; SES-001 §12); a residual test row is a
  go-live defect.

### 10.2 Explicitly out of scope for the E2E pass

- **Performance / load, security pen-test, accessibility audit, and UAT** — owned
  by `SIMF-TST-001` §§9–12, not by this E2E pass (the E2E pass *feeds* the release
  and UAT gates but does not replace them).
- **Deferred features** (project `CLAUDE.md` freeze-lift notes): the
  GPS geofence → arrival → attendance → movement chain + question-gating-on-arrival
  (pending the **G-OI-2** venue-boundary decision), the **exact statistics metric
  list** (pending **D6**), and the **dynamic `SessionCategory` list** (table ships
  empty pending the client's categories, OI-2) — their pages carry catalogue
  files, but scenarios that depend on the deferred input are marked pending, not
  failing.
- **Provider stubs** — the **live-video** provider (pending external procurement,
  **D7**) and the **AI** provider stub: E2E asserts the SIMF-side wiring and the
  stubbed contract, not a real third-party integration.
- **Native-only mobile steps** — the on-device biometric prompt and `local_auth`
  native config are the `simf-run` follow-up; the Dart client + the .NET↔Dart
  interop are proven at the unit / golden-vector layer (D-266), not in this pass.

---

_Last reviewed:_ 2026-06-04 by SIMF Team. This is a living plan; it tracks the
catalogue (`README.md`) and defers to `SIMF-TST-001` for test strategy.
