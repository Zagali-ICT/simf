using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// R3.5 — D-094: lookups + lifecycle for the <see cref="SimfUser"/>
/// aggregate. Split out of the 22-method <see cref="IUserAccountRepository"/>
/// so an Application service that only needs to find a user (or create a
/// new one, or persist field changes) does not have to depend on the
/// credential / lockout / role / 2FA surface as well.
/// </summary>
public interface IUserAccountStore
{
    Task<SimfUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<SimfUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a user with an initial password (sign-up).</summary>
    Task<UserOperationResult> CreateAsync(SimfUser user, string password, CancellationToken cancellationToken = default);

    /// <summary>Creates a password-less user (admin invite path).</summary>
    Task<UserOperationResult> CreateAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task<UserOperationResult> UpdateAsync(SimfUser user, CancellationToken cancellationToken = default);
}
