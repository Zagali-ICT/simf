# sev-1-6-service-placement — AdminAccountService stays in Infrastructure; deferral re-confirmed

Item ref: `sev-1-6-service-placement` (Track F, fix-all run 2026-07-30). Owner decision **Q13**.
Files touched: `docs/SIMF-Architecture-Refactor-Plan.md`.

## DECISIONS_LOG

### D-NEXT — Arch SEV-1.6: the `AdminAccountService` Infra→Application move is deferred again, in writing

`AdminAccountService` is the last of the five R4 services still outside
Application (`src/Backend/SIMF.Infrastructure/Identity/`). The other four moved:
Notification service + dispatcher (D-095), Interest + UserProfile (D-209). D-209
deferred this one "post-event"; the review flagged that a deferral living only in
a table cell gets inherited by silence rather than re-decided. **Re-confirmed
2026-07-30: it does not move this round, and the reasoning is now written down
where the next round will read it.**

**Why it stays.** Three reasons, all re-verified on this branch:

1. **Size** — 3,452 lines across six partial files: `AdminAccountService.cs`
   1,224 · `.Bulk.cs` 1,300 · `.Update.cs` 345 · `.Approval.cs` 318 ·
   `.ChangeType.cs` 136 · `.Roles.cs` 129.
2. **It is security-critical** — admin provisioning, the approve/reject workers,
   D-208 role assignment and the last-administrator guard all live in it. A silent
   behaviour change during a mechanical move is an authorization defect, not a
   cosmetic one.
3. **There is no Application-shaped seam yet** — its primary constructor takes
   three collaborators Application cannot reference: `RoleManager<SimfRole>`
   (ASP.NET Identity, 5 call sites), `SimfIdentityDbContext` (14 call sites) and
   `SimfAppDbContext` (46 call sites). Moving the file without abstracting those
   first would move EF and Identity **into** Application — the opposite of what
   Arch SEV-1.6 asks for. It would also be a fresh Identity dependency in
   Application at the same moment Arch SEV-1.1 (D-NEXT, `sev-1-1-domain-purity`)
   is confirmed still open.

**What undoing the deferral requires** — recorded so the next estimate is derived,
not guessed:

1. An `IRoleDirectory`-shaped abstraction in
   `src/Backend/SIMF.Application/IdentityAccess/Abstractions/` (alongside the
   existing `IIdentityUserDirectory`) covering the `RoleManager` surface actually
   used — role lookup by name/id, existence, the administrator-role id —
   implemented in Infrastructure over `RoleManager<SimfRole>`. This is the
   abstraction D-209 named as missing.
2. Repository methods for the 60 direct `DbContext` queries — an
   `IUserRoleStore`-shaped read/write pair for the Identity-side ones, and
   additions to `SIMF.Application/IdentityAccess/IUserProfileRepository.cs` /
   new App-side repositories for the rest.
   Every query shape moves **verbatim**. D-209's Interest and UserProfile moves are
   the worked precedent, including the two-phase `SaveIdentityChangesAsync` /
   `SaveAppChangesAsync` ordering the cross-database rule (D-157) forces — this
   service touches **both** databases, so that ordering is not optional.
3. The move plus the DI rewiring at
   `src/Backend/SIMF.Infrastructure/DependencyInjection.cs:271-275` — one scoped
   instance backing four interfaces (`IAdminTwoFactorService`,
   `IAdminUserApprovalService`, `IAdminUserProvisioningService`,
   `IAdminUserBulkService`); that shape must be preserved.
4. The backstop: the admin suite under `tests/SIMF.Api.Tests/` green before and
   after each slice, with **no test edited to accommodate the move**. A test that
   needs changing means behaviour changed.

**Review trigger.** Re-decide after the event, or earlier if some other change
needs an Identity abstraction in Application anyway — at that point step 1 stops
being extra work and the cost drops sharply. Until then the R4 row stays PARTIAL
and Arch SEV-1.6 stays open.

No code changed for this item. The deferral is recorded as a new
"R4 remainder — `AdminAccountService` placement" section in
`docs/SIMF-Architecture-Refactor-Plan.md`, the R4 status row points at it, and the
original R4 scoping section carries a pointer so a reader starting there is not
misled by the stale "(1091 lines)" sizing.

## PAGE-INDEX

No row. Nothing moved, no route changed, no page was added. This item is a
recorded deferral.

## E2E-README

No registry row. No page or action changed, so there is nothing to author a
per-page catalogue against. Deliberately **no** test was added: the correct
guard for this item is the existing admin suite staying green through any future
move (item 4 above), not a new assertion that a file currently sits in a
particular folder — that would freeze the placement it is trying to change.
