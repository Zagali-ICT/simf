# SIMF — Architecture Refactor Plan (post-Sprint 1)

**Status:** Working plan. Not yet approved as a sprint.
**Last updated:** 2026-05-25

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

The four still queued:

| Queued | Item | Closes | Size |
|--------|------|--------|------|
| R3 | `IUserAccountRepository` abstraction around `UserManager` | Arch SEV-1.4 | Multi-day |
| R4 | Move services from Infrastructure → Application | Arch SEV-1.6 | Multi-day (depends on R3) |
| R5 | Pure-POCO Domain (`SimfUser` no longer `IdentityUser<Guid>`) | Arch SEV-1.1 | Full sprint |
| R6 | Split `SimfIdentityDbContext` into bounded-context contexts | Arch SEV-1.3 | Full sprint |

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

| Slice | Status | What lands |
|-------|--------|------------|
| **R5a** | **CLOSED — D-090 (2026-05-26)** | `IdentitySimfUser : IdentityUser<Guid>` introduced as the EF-tracked persistence shim; `SimfIdentityDbContext` re-typed; `AddIdentityCore<IdentitySimfUser>`; FK configurations (AccountCode/RefreshToken/SecondFactorToken/TotpRecoveryCode) re-pointed; `UserAccountRepository` rewritten around the merge-into-tracked pattern with a bidirectional proto-mapper; empty `R5aRebindUserEntityToIdentitySimfUser` migration regenerates the snapshot. Tests + 12 fixtures swapped. Domain `SimfUser` unchanged. |
| **R5b** | **CLOSED — D-091 (2026-05-26)** | `ToIdentity` / `ToDomain` / `ApplyDomainMutations` / `SyncBack` extracted from `UserAccountRepository` into `src/Backend/SIMF.Infrastructure/Identity/IdentityUserMapper.cs`. Repository drops from ~425 to ~265 lines; every call site delegates to the static helper. Pure refactor; no contract change. |
| R5c | DEFERRED — not blocking | Optimisation only: re-shape `IUserAccountRepository` so a tracked-entity reuse path skips the extra `FindByIdAsync` round-trip on writes-without-prior-find. The efficiency-review pass during R5a confirmed no production caller actually hits that round-trip (every write is preceded by a `FindBy*` in the same scope), so this slice is not blocking. Pick up if/when a profile shows it matters. |
| R5d–R5e | NOT NEEDED | The consumer-migration slices were anticipated for the case where consumers reach into IdentityUser<Guid> members the POCO wouldn't expose. R5f's explicit-property surface preserved every name 1:1, so no consumer change was required. |
| **R5f** | **CLOSED — D-092 (2026-05-26)** | `SimfUser` rewritten as a pure Domain POCO; `IdentityUser<Guid>` inheritance dropped. All Identity-derived fields are explicit properties with identical names/types/nullability, so the consumer surface is unchanged. No code outside Domain required updates. |
| **R5g** | **CLOSED — D-093 (2026-05-26)** | `Microsoft.Extensions.Identity.Stores` package reference removed from `SIMF.Domain.csproj`. `SimfRole` POCO-split into Infrastructure's `IdentitySimfRole : IdentityRole<Guid>` (no Domain consumer existed); `RolePermission.Role` nav prop dropped (shadow nav in `RolePermissionConfiguration`); `SimfIdentityDbContext` retypes; `RoleManager<SimfRole>` injections + `new SimfRole { ... }` sites swapped across Infrastructure + 8 test fixtures. Four stale `using Microsoft.AspNetCore.Identity;` directives in Application/IdentityAccess (no actual usage post-H21) deleted. Empty `R5gRebindRoleEntityToIdentitySimfRole` migration regenerates the snapshot. New `DomainPurityTests` (3 Facts) pin Domain assembly purity. **Arch SEV-1.1 fully closed.** |

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
