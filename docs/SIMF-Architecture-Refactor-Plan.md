# SIMF — Architecture Refactor Plan (post-Sprint 1)

**Status:** Working plan. Not yet approved as a sprint.
**Last updated:** 2026-07-30 — R5 status corrected (it was claiming work that is
not on this branch) and the R4 remainder deferral re-confirmed. See
"R5 status correction — 2026-07-30" and "R4 remainder — `AdminAccountService`
placement" below.

This plan scopes the four architectural SEV-1 findings left after R1
(typed `StorageOptions` — D-074) and R2 (`IAdminAccountService` split —
D-075). Each item is sized realistically based on a `grep -rln` of the
relevant references, has a clear ordering dependency on others, and
calls out the regression-risk shape.

The two refactors already shipped:

| Done | Item | Closes | Decision |
|------|------|--------|----------|
| R1 | Typed `StorageOptions` | Arch SEV-1.5 | D-074 |
| R2 | `IAdminAccountService` split (interface only) | Arch SEV-1.2 | D-075 |

Status of the rest (corrected 2026-05-31, D-209 — the table below was stale):

| Item | Closes | Status |
|------|--------|--------|
| R3 | `IUserAccountRepository` abstraction around `UserManager` | Arch SEV-1.4 | **DONE — D-076** |
| R3.5 | Split the 22-method aggregate into 5 role-cohesive sub-interfaces | Arch SEV-1.2 (granularity) | **DONE — D-094** |
| R4 | Move services from Infrastructure → Application | Arch SEV-1.6 | **PARTIAL** — Notification svc+dispatcher (D-095), Interest + UserProfile (D-209) done; **`AdminAccountService` move DEFERRED post-event** (security-critical, would need new `RoleManager`/`UserRoles` abstractions). Deferral **re-confirmed 2026-07-30** — see "R4 remainder" below |
| R5 | Pure-POCO Domain (`SimfUser` no longer `IdentityUser<Guid>`) | Arch SEV-1.1 | **NOT DONE — OPEN.** The former `DONE — D-090→093` entry does not hold on this branch; corrected 2026-07-30, see "R5 status correction" below |
| R6 | Split `SimfIdentityDbContext` into bounded-context contexts | Arch SEV-1.3 | **QUEUED — owner gate** (High risk; touches D-110 frozen migration history) |

Separately, the `AdminAccountService` **implementation** was split (D-209, A2) from
one 1900-line class into cohesive **partial-class files** (`.cs` + `.Approval.cs` +
`.Bulk.cs` + `.Roles.cs`) — navigability only, no behaviour/DI change. The Infra→App
*move* of that service is the deferred R4 remainder above.

> The detailed per-item sections below predate this status correction; treat the
> table above as authoritative for what is done vs. queued.

---

## R5 status correction — 2026-07-30

**The row above used to read `DONE — D-090→093`. That claim is false for this
branch and has been corrected to OPEN.** Every line below was verified by
reading the file on `qa/programme-ws0`, not by trusting the decisions log.

| What D-090…D-093 recorded as landed | What is actually on this branch |
|---|---|
| R5f (D-092) — `SimfUser` is a pure Domain POCO | `src/Backend/SIMF.Domain/IdentityAccess/SimfUser.cs:1,19` — `using Microsoft.AspNetCore.Identity;` and `public class SimfUser : IdentityUser<Guid>` |
| R5g (D-093) — `SimfRole` POCO-split, Domain class deleted | `src/Backend/SIMF.Domain/IdentityAccess/SimfRole.cs:1,11` — still `public class SimfRole : IdentityRole<Guid>`, still in **Domain** |
| R5g (D-093) — `Microsoft.Extensions.Identity.Stores` reference removed from Domain | `src/Backend/SIMF.Domain/SIMF.Domain.csproj:8` — `<PackageReference Include="Microsoft.Extensions.Identity.Stores" Version="10.0.8" />` is still the project's only package reference |
| R5a (D-090) — `IdentitySimfUser` persistence shim in Infrastructure | No `IdentitySimfUser` (or `IdentitySimfRole`) type exists anywhere under `src/` |
| R5b (D-091) — `IdentityUserMapper` extracted | `src/Backend/SIMF.Infrastructure/Identity/IdentityUserMapper.cs` does not exist |
| R5a/R5g — `SimfIdentityDbContext` re-typed onto the shims | `src/Backend/SIMF.Infrastructure/Persistence/SimfIdentityDbContext.cs:17-18` — `IdentityDbContext<SimfUser, SimfRole, Guid>`; EF tracks the **Domain** types directly |
| R5a/R5g — two snapshot-only rebind migrations | `src/Backend/SIMF.Infrastructure/Persistence/Migrations/Identity/` holds one migration (`20260713121829_20260712001`) and the snapshot |
| D-093 — "New `DomainPurityTests` (3 Facts) pin Domain assembly purity" | No `DomainPurityTests` existed anywhere under `tests/` until 2026-07-30 |

**Arch SEV-1.1 is therefore OPEN.** Domain depends on ASP.NET Core Identity, so
every layer above it does too.

How the branch and the decisions log diverged is **not established here**. The
R5 slices are logged as landing on 2026-05-26, and the Identity migration tree
has since been squashed to a single migration (the D-110 freeze note in
`CLAUDE.md` describes one `InitialCreate` per context), so a rebase or squash
that dropped the slices is the obvious candidate — but this document asserts
only what it verified, and it did not verify that.

### What was done about it (owner decision Q15, 2026-07-30)

**Correct the plan and add a guard test. No POCO split this round.** The split
would re-type `SimfIdentityDbContext`, re-point four FK configurations and
rewrite `UserAccountRepository` around a merge-into-tracked mapper — i.e. it
reaches straight into the D-110 frozen Identity schema, and `SimfUser` is the
row behind the shipped mobile wire contract. That is not a change to make in a
defect-clearing round against a hard event deadline.

The guard is `tests/SIMF.Api.Tests/DomainPurityTests.cs`. It is written in the
**inverted** form: it asserts the current known-bad state (`SimfUser.BaseType ==
typeof(IdentityUser<Guid>)`, `SimfRole.BaseType == typeof(IdentityRole<Guid>)`,
Domain references an ASP.NET Identity assembly) rather than the desired one, so
the suite stays green today **and** anyone who actually does the split gets a
red test that names this section and forces the status row above to be updated
in the same commit. Its fourth Fact is a forward guard that is green today and
must stay green: no Domain type **other than** `SimfUser` and `SimfRole` derives
from an Identity type, so the leak cannot widen unnoticed while it is open.

When R5 is genuinely done, flip the three inverted Facts to their positive form
(`BaseType == typeof(object)`, no Identity reference), keep the fourth, and
update the R5 rows in both tables above.

---

## R4 remainder — `AdminAccountService` placement (Arch SEV-1.6)

**Deferral re-confirmed 2026-07-30 (owner decision Q13). The service does not
move this round.** It is recorded here rather than left in a table cell so the
next round re-decides it deliberately instead of inheriting it by silence.

**What is deferred.** `AdminAccountService` stays at
`src/Backend/SIMF.Infrastructure/Identity/` — the last of the five R4 services
still outside Application. The other four moved (Notification service +
dispatcher, D-095; Interest + UserProfile, D-209).

**Why.** Three reasons, all still true on this branch:

1. **Size.** 3,452 lines across six partial files
   (`AdminAccountService.cs` 1,224 · `.Bulk.cs` 1,300 · `.Update.cs` 345 ·
   `.Approval.cs` 318 · `.ChangeType.cs` 136 · `.Roles.cs` 129).
2. **It is security-critical.** Admin provisioning, the approve/reject workers,
   role assignment (D-208) and the last-administrator guard all live in it. A
   silent behaviour change during a mechanical move is an authorization defect,
   not a cosmetic one.
3. **It has no Application-shaped seam yet.** Its primary constructor takes
   three collaborators Application cannot reference: `RoleManager<SimfRole>`
   (ASP.NET Identity, 5 call sites), `SimfIdentityDbContext` (14 call sites) and
   `SimfAppDbContext` (46 call sites). Moving the file without first abstracting
   those just moves EF and Identity **into** Application, which is the opposite
   of what Arch SEV-1.6 asks for.

**What undoing the deferral requires** — the actual prerequisite list, so the
next estimate is not guessed:

1. An `IRoleDirectory`-shaped abstraction in
   `src/Backend/SIMF.Application/IdentityAccess/Abstractions/` (alongside the
   existing `IIdentityUserDirectory`) covering the `RoleManager` surface this
   service uses — role lookup by name/id, existence, the administrator-role id —
   implemented in Infrastructure over `RoleManager<SimfRole>`. This is the
   abstraction D-209 named as missing.
2. Repository methods for the 60 direct `DbContext` queries — an
   `IUserRoleStore`-shaped read/write pair for the Identity-side ones and
   additions to `SIMF.Application/IdentityAccess/IUserProfileRepository.cs` /
   new App-side repositories for the
   rest. Each query shape must move **verbatim**; D-209's Interest and
   UserProfile moves are the worked precedent, including the two-phase
   `SaveIdentityChangesAsync` / `SaveAppChangesAsync` ordering that the
   cross-database rule (D-157) forces.
3. The move itself plus the DI rewiring at
   `src/Backend/SIMF.Infrastructure/DependencyInjection.cs:271-275` (one scoped
   instance backing four interfaces — that shape must be preserved).
4. The backstop: the admin suite under `tests/SIMF.Api.Tests/` must be green
   before and after each slice, with no test edited to accommodate the move.

**Review trigger.** Re-decide after the event, or earlier if a change needs a
new Identity abstraction anyway — at that point step 1 stops being extra work.
Until then this row stays PARTIAL and Arch SEV-1.6 stays open.

---

## R3 — `IUserAccountRepository` abstraction (Arch SEV-1.4)

**Scope.** Application services inject `UserManager<SimfUser>` directly
today. Six services need to migrate to an `IUserAccountRepository`
abstraction defined in Application and implemented in Infrastructure
on top of `UserManager`:

- `RegistrationService` — `FindByEmailAsync`, `CreateAsync(user, password)`.
- `SignInService` — `FindByEmailAsync`, `IsLockedOutAsync`,
  `CheckPasswordAsync`, `AccessFailedAsync`, `ResetAccessFailedCountAsync`,
  `GetRolesAsync`, `GetAuthenticatorKeyAsync`, `SetAuthenticationTokenAsync`,
  `UpdateAsync`.
- `PasswordService` — `FindByIdAsync`, `FindByEmailAsync`, `RemovePasswordAsync`,
  `AddPasswordAsync`, `ChangePasswordAsync`, `UpdateAsync`.
- `SessionService` — `FindByIdAsync`, `UpdateSecurityStampAsync`.
- `UserProfileService` — `FindByIdAsync`, `UpdateAsync`.
- `AdminAccountService` — `FindByIdAsync`, `FindByEmailAsync`, `CreateAsync`,
  `UpdateAsync`, `AddToRoleAsync`, `RemoveFromRolesAsync`, `GetRolesAsync`,
  `IsInRoleAsync`, `GeneratePasswordResetTokenAsync`,
  `ResetPasswordAsync`, plus the TOTP-token methods.

**Shape.** Introduce:

```csharp
namespace SIMF.Application.IdentityAccess.Abstractions;

public interface IUserAccountRepository
{
    Task<SimfUser?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<SimfUser?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<IdentityResult> CreateAsync(SimfUser user, string password, CancellationToken ct = default);
    Task<IdentityResult> CreateAsync(SimfUser user, CancellationToken ct = default); // password-less invite
    Task<IdentityResult> UpdateAsync(SimfUser user, CancellationToken ct = default);
    Task<bool> CheckPasswordAsync(SimfUser user, string password, CancellationToken ct = default);
    Task<bool> IsLockedOutAsync(SimfUser user, CancellationToken ct = default);
    Task AccessFailedAsync(SimfUser user, CancellationToken ct = default);
    Task ResetAccessFailedCountAsync(SimfUser user, CancellationToken ct = default);
    Task UpdateSecurityStampAsync(SimfUser user, CancellationToken ct = default);
    Task<IList<string>> GetRolesAsync(SimfUser user, CancellationToken ct = default);
    Task<bool> IsInRoleAsync(SimfUser user, string role, CancellationToken ct = default);
    Task<IdentityResult> AddToRoleAsync(SimfUser user, string role, CancellationToken ct = default);
    Task<IdentityResult> RemoveFromRolesAsync(SimfUser user, IEnumerable<string> roles, CancellationToken ct = default);
    Task<IdentityResult> AddPasswordAsync(SimfUser user, string password, CancellationToken ct = default);
    Task<IdentityResult> RemovePasswordAsync(SimfUser user, CancellationToken ct = default);
    Task<IdentityResult> ChangePasswordAsync(SimfUser user, string currentPassword, string newPassword, CancellationToken ct = default);
    Task<string?> GetAuthenticatorKeyAsync(SimfUser user, CancellationToken ct = default);
    Task<IdentityResult> SetAuthenticationTokenAsync(SimfUser user, string provider, string name, string value, CancellationToken ct = default);
}
```

Implementation in Infrastructure is a thin pass-through wrapping
`UserManager<SimfUser>`. The interface still returns `IdentityResult` —
that surface is part of the contract today and hiding it costs more
abstraction noise than it removes.

**Order of migration.** One service per commit:
1. `RegistrationService` (smallest surface — 2 methods).
2. `PasswordService`.
3. `SessionService`.
4. `UserProfileService`.
5. `SignInService` (largest TOTP surface).
6. `AdminAccountService` (largest overall — also the last one because
   it has the most methods to migrate).

Each commit:
- Replaces the `UserManager<SimfUser>` injection with `IUserAccountRepository`.
- Updates the 6 services' constructor + method bodies.
- Builds + tests after each.

**Risk.** Medium. The pass-through is mechanical but every callsite is
a place a typo can silently change behaviour (e.g. forgetting to await,
passing the wrong CancellationToken). Test coverage is the safety net.

**Effort.** ~6 commits, 2-3 days.

**Blocks.** R4 (service placement) — Application can't pull services in
while they inject `UserManager` directly.

---

## R4 — Move services from Infrastructure → Application (Arch SEV-1.6)

> Four of the five listed below have moved (D-095, D-209). Only
> `AdminAccountService` is left, and its deferral was re-confirmed on
> 2026-07-30 — see "R4 remainder — `AdminAccountService` placement" above for
> the current position, the sizing, and the prerequisite list. The original
> scoping is kept below for the other four.

**Scope.** Five services currently in `SIMF.Infrastructure/` that are
orchestration code (use case logic), not data-access code:

- `AdminAccountService` (1091 lines)
- `UserProfileService`
- `InterestService`
- `NotificationService`
- `NotificationDispatcher`

Each is moved to `SIMF.Application/<context>/` and stops injecting
`SimfIdentityDbContext` directly — repositories take over the
data-access concern.

**Depends on.** R3 (the `UserManager` → `IUserAccountRepository`
migration must be done first; some of these services also need an
`INotificationRepository` and an `IInterestRepository` to land).

**Shape.** Each service moves file location; DI registration moves from
the Infrastructure-only seam to the Application registration. The
`internal sealed` class declarations stay internal (use case code is
not a public-API surface) but become accessible to the Application
project. Repository implementations stay in Infrastructure.

**Risk.** Low-medium. Mostly mechanical file moves with DI rewiring.
The harder part is auditing each service for direct `DbContext`
queries that should become repository methods.

**Effort.** ~5 commits (one per service), 1-2 days.

---

## R5 — Pure-POCO Domain (Arch SEV-1.1)

**Scope.** `SimfUser` currently inherits `IdentityUser<Guid>` from
`Microsoft.Extensions.Identity.Stores`. The Domain project takes a
package reference on Identity to support this, which means every
downstream layer also depends on Identity. The reviewer's call: the
"pure model" contract is gone at the root.

**Sliced sequencing.** R5 ships in seven incremental commits so each
landing point is shippable (272/272 green, 0/0 build at Debug + Release).
The slicing matches the R3a–R3g cadence — each slice is a single
tractable PR that leaves the codebase in a consistent state.

> **Read the "R5 status correction — 2026-07-30" section above before this
> table.** Every "CLOSED" below is the status as *recorded on 2026-05-26*. None
> of that code is on this branch, so treat the column as **RECORDED CLOSED, NOT
> PRESENT** — the slices have to be re-done, not re-verified. Left verbatim
> because the per-slice design notes (the merge-into-tracked pattern, the FK
> re-pointing, the exhaustive-mapper risk) are the useful part and still apply
> whenever R5 is picked up again.

| Slice | Status | What lands |
|-------|--------|------------|
| **R5a** | **RECORDED CLOSED — D-090 (2026-05-26); NOT PRESENT on this branch** | `IdentitySimfUser : IdentityUser<Guid>` introduced as the EF-tracked persistence shim; `SimfIdentityDbContext` re-typed; `AddIdentityCore<IdentitySimfUser>`; FK configurations (AccountCode/RefreshToken/SecondFactorToken/TotpRecoveryCode) re-pointed; `UserAccountRepository` rewritten around the merge-into-tracked pattern with a bidirectional proto-mapper; empty `R5aRebindUserEntityToIdentitySimfUser` migration regenerates the snapshot. Tests + 12 fixtures swapped. Domain `SimfUser` unchanged. |
| **R5b** | **RECORDED CLOSED — D-091 (2026-05-26); NOT PRESENT on this branch** | `ToIdentity` / `ToDomain` / `ApplyDomainMutations` / `SyncBack` extracted from `UserAccountRepository` into `src/Backend/SIMF.Infrastructure/Identity/IdentityUserMapper.cs`. Repository drops from ~425 to ~265 lines; every call site delegates to the static helper. Pure refactor; no contract change. |
| R5c | DEFERRED — not blocking | Optimisation only: re-shape `IUserAccountRepository` so a tracked-entity reuse path skips the extra `FindByIdAsync` round-trip on writes-without-prior-find. The efficiency-review pass during R5a confirmed no production caller actually hits that round-trip (every write is preceded by a `FindBy*` in the same scope), so this slice is not blocking. Pick up if/when a profile shows it matters. |
| R5d–R5e | NOT NEEDED | The consumer-migration slices were anticipated for the case where consumers reach into IdentityUser<Guid> members the POCO wouldn't expose. R5f's explicit-property surface preserved every name 1:1, so no consumer change was required. |
| **R5f** | **RECORDED CLOSED — D-092 (2026-05-26); NOT PRESENT on this branch** | `SimfUser` rewritten as a pure Domain POCO; `IdentityUser<Guid>` inheritance dropped. All Identity-derived fields are explicit properties with identical names/types/nullability, so the consumer surface is unchanged. No code outside Domain required updates. |
| **R5g** | **RECORDED CLOSED — D-093 (2026-05-26); NOT PRESENT on this branch** | `Microsoft.Extensions.Identity.Stores` package reference removed from `SIMF.Domain.csproj`. `SimfRole` POCO-split into Infrastructure's `IdentitySimfRole : IdentityRole<Guid>` (no Domain consumer existed); `RolePermission.Role` nav prop dropped (shadow nav in `RolePermissionConfiguration`); `SimfIdentityDbContext` retypes; `RoleManager<SimfRole>` injections + `new SimfRole { ... }` sites swapped across Infrastructure + 8 test fixtures. Four stale `using Microsoft.AspNetCore.Identity;` directives in Application/IdentityAccess (no actual usage post-H21) deleted. Empty `R5gRebindRoleEntityToIdentitySimfRole` migration regenerates the snapshot. New `DomainPurityTests` (3 Facts) pin Domain assembly purity. ~~Arch SEV-1.1 fully closed.~~ **Struck 2026-07-30 — none of this is on the branch and SEV-1.1 is open; see the status correction above.** |

**R5a recap (D-090).** The seam is the type EF tracks. `IdentitySimfUser`
mirrors `SimfUser` field-for-field; `SimfUserConfiguration` re-binds
to it; FK configurations re-point so EF doesn't discover the Domain
`SimfUser` as a duplicate entity. The empty no-op migration proves the
DDL is unchanged (same `AspNetUsers` table, same columns). The proto-
mapper inside `UserAccountRepository` is the §17 minimum that keeps
every caller (Application services, test fixtures) speaking
`SimfUser`. The merge-into-tracked design (look up the already-tracked
`IdentitySimfUser`, merge SimfUser mutations into it, hand the tracked
entity to UserManager) sidesteps both the duplicate-tracking guard and
the concurrency-stamp snapshot problem that a naive "attach a fresh
IdentitySimfUser" approach would hit.

**Risk for remaining slices.** Medium-to-high. R5f is the load-bearing
slice — the mapping has to be exhaustive (a single Identity field the
domain reads but the mapper skips is a silent bug). EF migrations must
keep mapping to `AspNetUsers` with no schema change through every slice.

**Effort.** R5a landed in one session. R5b–c each ~1 day. R5d–e together
~2-3 days (the consumer migration). R5f ~1-2 days. R5g half a day. Full
R5 sprint total ~1 week of work, but no slice is bigger than a single
tractable commit so the sprint can be paused between any two slices.

---

## R6 — Split `SimfIdentityDbContext` (Arch SEV-1.3)

**Scope.** The current `SimfIdentityDbContext` holds four bounded
contexts' tables: Identity (users, roles, refresh tokens, codes,
permissions), UserProfile (profiles, profile types, interests),
Notifications, and partial Audit. Split into:

- `SimfIdentityDbContext` — Identity tables only.
- `SimfProfilesDbContext` — UserProfile + ProfileType + Interest +
  join table.
- `SimfNotificationsDbContext` — Notification table.

All three contexts point at the same physical database (a single
SIMF DB per the architecture decision) but maintain separate
`__EFMigrationsHistory` tables.

**Migration story.** Existing migrations stay in
`SimfIdentityDbContext`. New per-context migration trees start
empty — the first migration on the new contexts does nothing schema-
wise (the tables already exist), it just claims the migration history.

**References to touch.** Every place that queries across the formerly-
shared context (e.g. `UserProfileService` joining `UserProfiles` and
`Users` in one query) needs an explicit cross-context shape — usually
a repository method that does the join inside one context and asks the
other for the rest.

**Risk.** High. Cross-context queries are the trap — a regression here
silently changes query shape (multi-roundtrip vs single JOIN).

**Effort.** Full sprint (1 week). Could be done in parallel with R5
since the dependencies are mostly orthogonal.

---

## Suggested order

1. **R3** (repository abstraction) — first; unblocks R4 and reduces
   the surface that R5 needs to touch.
2. **R4** (service placement) — after R3.
3. **R5** (pure-POCO Domain) and **R6** (DbContext split) in parallel —
   they touch different concerns.

Total: R3 (2-3 days) → R4 (1-2 days) → R5 + R6 (1 week parallel) =
**roughly a 2-week refactor sprint** for the four remaining items.

The User Management module increment per the programme plan is best
held until R3 + R4 land — the architectural seams it builds on top of
will be much cleaner after the repository + placement migrations.
R5 + R6 can run alongside the next module's build since their
contracts (`SimfUser` shape, table mapping) stay backwards-compatible
through the migration.
