namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Repository over the <c>SimfUser</c> aggregate. Application services used to
/// inject <c>UserManager&lt;SimfUser&gt;</c> directly
/// — a framework primitive — which made the boundary between Application
/// orchestration and Identity / EF infrastructure leaky. This interface is
/// the seam: Application code asks for
/// <c>SimfUser</c>s through this contract; the Infrastructure implementation
/// wraps <c>UserManager</c>.
///
/// <para>Methods that previously returned
/// <c>Microsoft.AspNetCore.Identity.IdentityResult</c> now return
/// <see cref="UserOperationResult"/> — a SIMF-owned record. Application
/// code no longer transitively depends on the Identity types it is
/// supposed to be decoupled from.</para>
///
/// <para>The 22-method aggregate is split into five role-
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
