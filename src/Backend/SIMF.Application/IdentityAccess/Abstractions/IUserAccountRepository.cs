using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Repository over the <c>SimfUser</c> aggregate (R3 — D-076). The pre-R3
/// Application services injected <c>UserManager&lt;SimfUser&gt;</c> directly
/// — a framework primitive — which is what made the boundary between
/// Application orchestration and Identity / EF infrastructure leaky
/// (Architecture SEV-1.4). This interface is the seam: Application code
/// asks for <c>SimfUser</c>s through this contract; the Infrastructure
/// implementation wraps <c>UserManager</c>.
///
/// <para>H21 — D-082: methods that previously returned
/// <c>Microsoft.AspNetCore.Identity.IdentityResult</c> now return
/// <see cref="UserOperationResult"/> — a SIMF-owned record. Application
/// code no longer transitively depends on the Identity types it was
/// supposed to be decoupled from after R3.</para>
/// </summary>
public interface IUserAccountRepository
{
    // -- Lookups -------------------------------------------------------------

    Task<SimfUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<SimfUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // -- Lifecycle -----------------------------------------------------------

    /// <summary>Creates a user with an initial password (sign-up).</summary>
    Task<UserOperationResult> CreateAsync(SimfUser user, string password, CancellationToken cancellationToken = default);

    /// <summary>Creates a password-less user (admin invite path).</summary>
    Task<UserOperationResult> CreateAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task<UserOperationResult> UpdateAsync(SimfUser user, CancellationToken cancellationToken = default);

    // -- Credentials ---------------------------------------------------------

    Task<bool> CheckPasswordAsync(SimfUser user, string password, CancellationToken cancellationToken = default);

    Task<UserOperationResult> AddPasswordAsync(SimfUser user, string password, CancellationToken cancellationToken = default);

    Task<UserOperationResult> RemovePasswordAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task<UserOperationResult> ChangePasswordAsync(SimfUser user, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    Task UpdateSecurityStampAsync(SimfUser user, CancellationToken cancellationToken = default);

    // -- Lockout / access tracking ------------------------------------------

    Task<bool> IsLockedOutAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task AccessFailedAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task ResetAccessFailedCountAsync(SimfUser user, CancellationToken cancellationToken = default);

    // -- Roles ---------------------------------------------------------------

    Task<IList<string>> GetRolesAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task<bool> IsInRoleAsync(SimfUser user, string role, CancellationToken cancellationToken = default);

    Task<UserOperationResult> AddToRoleAsync(SimfUser user, string role, CancellationToken cancellationToken = default);

    Task<UserOperationResult> RemoveFromRolesAsync(SimfUser user, IEnumerable<string> roles, CancellationToken cancellationToken = default);

    // -- TOTP / authenticator tokens ----------------------------------------

    Task<string?> GetAuthenticatorKeyAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task<UserOperationResult> SetAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, string tokenValue, CancellationToken cancellationToken = default);

    Task<string?> GetAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, CancellationToken cancellationToken = default);

    Task<UserOperationResult> RemoveAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, CancellationToken cancellationToken = default);

    Task<UserOperationResult> SetTwoFactorEnabledAsync(SimfUser user, bool enabled, CancellationToken cancellationToken = default);
}
