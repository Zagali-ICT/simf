# sev-1-1-domain-purity — the refactor plan claimed a POCO split that is not on this branch

Item ref: `sev-1-1-domain-purity` (Track F, fix-all run 2026-07-30). Owner decision **Q15**.
Files touched: `docs/SIMF-Architecture-Refactor-Plan.md`, `tests/SIMF.Api.Tests/DomainPurityTests.cs` (new).

## DECISIONS_LOG

### D-NEXT — Arch SEV-1.1 re-opened: correct the refactor plan and pin the real state with an inverted guard test

`docs/SIMF-Architecture-Refactor-Plan.md` recorded R5 (pure-POCO Domain) as
`DONE — D-090→093` with `Arch SEV-1.1 fully closed`. **That is false on
`qa/programme-ws0`.** Verified by reading each file rather than trusting the log:

| Recorded as landed | Actually on this branch |
|---|---|
| R5f (D-092) — `SimfUser` is a pure POCO | `src/Backend/SIMF.Domain/IdentityAccess/SimfUser.cs:1,19` — `using Microsoft.AspNetCore.Identity;` / `public class SimfUser : IdentityUser<Guid>` |
| R5g (D-093) — `SimfRole` POCO-split out of Domain | `src/Backend/SIMF.Domain/IdentityAccess/SimfRole.cs:1,11` — still `public class SimfRole : IdentityRole<Guid>` in Domain |
| R5g (D-093) — Identity package reference dropped | `src/Backend/SIMF.Domain/SIMF.Domain.csproj:8` — `Microsoft.Extensions.Identity.Stores` 10.0.8 is the project's only package reference |
| R5a (D-090) — `IdentitySimfUser` shim | no such type anywhere under `src/` |
| R5b (D-091) — `IdentityUserMapper` | `src/Backend/SIMF.Infrastructure/Identity/IdentityUserMapper.cs` does not exist |
| R5a/R5g — `SimfIdentityDbContext` re-typed onto the shims | `SimfIdentityDbContext.cs:17-18` — `IdentityDbContext<SimfUser, SimfRole, Guid>`; EF tracks the **Domain** types directly |
| R5a/R5g — two snapshot-only rebind migrations | `Persistence/Migrations/Identity/` holds one migration (`20260713121829_20260712001`) plus the snapshot |
| D-093 — "New `DomainPurityTests` (3 Facts) pin Domain assembly purity" | no `DomainPurityTests` existed anywhere under `tests/` |

So **Arch SEV-1.1 is OPEN**: Domain depends on ASP.NET Core Identity, and every
layer above it inherits that dependency. How the branch diverged from the log is
**not established** — the slices are logged as landing 2026-05-26 and the Identity
migration tree has since been squashed to one migration, so a rebase or squash
that dropped them is the obvious candidate, but nothing here verified that and
nothing here asserts it.

**Decision (Q15): correct the plan and add a guard test. Do not attempt the POCO
split.** The split re-types `SimfIdentityDbContext`, re-points four FK
configurations and rewrites `UserAccountRepository` around a merge-into-tracked
mapper — it reaches straight into the D-110 frozen Identity schema, and `SimfUser`
is the row behind the shipped mobile wire contract. That is not a change to make
inside a defect-clearing round against a hard event deadline.

**The guard is written inverted, on purpose.** The honest assertion — "SIMF.Domain
references no Identity type" — fails today. A permanently red test is not a guard;
it is noise a suite learns to ignore, and it cannot be committed to a branch whose
build gate is green. So `tests/SIMF.Api.Tests/DomainPurityTests.cs` asserts the
**current known-bad state** in three Facts (`SimfUser.BaseType ==
typeof(IdentityUser<Guid>)`, `SimfRole.BaseType == typeof(IdentityRole<Guid>)`,
Domain's referenced assemblies include a `Microsoft.*Identity*` entry). They pass
now and go **red the moment someone actually does the split** — which is the
point: whoever does it is then forced to flip the assertions and update the plan's
status table in the same commit, so the "it's closed" claim can never again drift
away from the code. Each failure message names the file and the section to update.
A fourth Fact is **not** inverted and is green today: no Domain type **other than**
`SimfUser` and `SimfRole` derives from an Identity base, so the leak cannot widen
while SEV-1.1 is open.

The fixture sits in `tests/SIMF.Api.Tests/` rather than `tests/SIMF.Domain.Tests/`
(where D-093 said it lived) because the assertion needs the ASP.NET Identity types
to compare against, and `SIMF.Domain.Tests` references only `SIMF.Domain`. Adding
a package reference to a test project to host an architecture test would be a
project-settings change for a docs-and-guard item.

Plan changes: the R5 status row flips to **NOT DONE — OPEN**; a new
"R5 status correction — 2026-07-30" section carries the evidence table, the
undecided-provenance note and the flip instructions; the four R5 slice rows are
re-labelled **RECORDED CLOSED … NOT PRESENT on this branch** (kept verbatim
otherwise — the per-slice design notes are the useful part and still apply
whenever R5 is picked up); the `Arch SEV-1.1 fully closed` sentence is struck
in place rather than deleted, so a reader who remembers it sees why it went.

Not touched: `docs/SIMF-Follow-Up-Backlog.md:81` and
`docs/SIMF-Sprint1-Login-API-Completion.md:162,263` already describe SEV-1.1 as
open, and `docs/SIMF-Implementation-Gap-Report.md:168-171` lists it in the
architecture backlog. They were already correct; the refactor plan was the only
document making the false claim.

## PAGE-INDEX

No row. This item changes no page and no route — it corrects one architecture
document and adds one test fixture.

## E2E-README

No registry row. There is no page or user-facing action to author a per-page
Gherkin catalogue against; the executable proof for this item is the xUnit
fixture `tests/SIMF.Api.Tests/DomainPurityTests.cs` (4 Facts), which runs in the
normal suite.
