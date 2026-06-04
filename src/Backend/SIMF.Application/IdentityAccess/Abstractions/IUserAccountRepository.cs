namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Repository over the <c>SimfUser</c> aggregate (R3 — D-076). The pre-R3
/// Application services injected <c>UserManager&lt;SimfUser&gt;</c> directly
/// — a framework primitive — which made the boundary between Application
/// orchestration and Identity / EF infrastructure leaky (Architecture
/// SEV-1.4). This interface is the seam: Application code asks for
/// <c>SimfUser</c>s through this contract; the Infrastructure implementation
/// wraps <c>UserManager</c>.
///
/// <para>H21 — D-082: methods that previously returned
/// <c>Microsoft.AspNetCore.Identity.IdentityResult</c> now return
/// <see cref="UserOperationResult"/> — a SIMF-owned record. Application
/// code no longer transitively depends on the Identity types it was
/// supposed to be decoupled from after R3.</para>
///
/// <para>R3.5 — D-094: the 22-method aggregate is split into five role-
/// cohesive sub-interfaces (<see cref="IUserAccountStore"/>,
/// <see cref="IUserCredentialStore"/>, <see cref="IUserLockoutTracker"/>,
/// <see cref="IUserRoleStore"/>, <see cref="IUserTwoFactorStore"/>).
/// <see cref="IUserAccountRepository"/> remains as a marker that inherits
/// all five so existing consumers compile unchanged; new code is encouraged
/// to inject only the narrower contract it actually uses, so a future
/// reader of the constructor signature can see at a glance which
/// concerns the service touches.</para>
/// </summary>
public interface IUserAccountRepository
    : IUserAccountStore,
      IUserCredentialStore,
      IUserLockoutTracker,
      IUserRoleStore,
      IUserTwoFactorStore
{
}
