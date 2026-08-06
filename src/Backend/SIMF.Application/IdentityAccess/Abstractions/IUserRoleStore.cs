using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Role-membership surface for the <see cref="SimfUser"/>
/// aggregate. Split out of the 22-method
/// <see cref="IUserAccountRepository"/> so role-aware services (CP admin
/// operations, JWT minting) need only this seam.
/// </summary>
public interface IUserRoleStore
{
    Task<IList<string>> GetRolesAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task<bool> IsInRoleAsync(SimfUser user, string role, CancellationToken cancellationToken = default);

    Task<UserOperationResult> AddToRoleAsync(SimfUser user, string role, CancellationToken cancellationToken = default);

    Task<UserOperationResult> RemoveFromRolesAsync(SimfUser user, IEnumerable<string> roles, CancellationToken cancellationToken = default);
}
