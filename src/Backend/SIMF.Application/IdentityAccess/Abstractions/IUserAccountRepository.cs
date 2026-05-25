using Microsoft.AspNetCore.Identity;
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
/// <para>The methods return <see cref="IdentityResult"/> for now — that
/// surface is part of the existing contract; replacing it with a
/// SIMF-owned result type is queued behind R5 (pure-POCO Domain), at
/// which point both <c>SimfUser</c> and the result shape become
/// SIMF-owned.</para>
///
/// <para>Migration is per-service: R3a migrates <c>RegistrationService</c>;
/// R3b–R3f migrate <c>PasswordService</c>, <c>SessionService</c>,
/// <c>UserProfileService</c>, <c>SignInService</c>, and
/// <c>AdminAccountService</c> respectively.</para>
/// </summary>
public interface IUserAccountRepository
{
    // -- Lookups -------------------------------------------------------------

    Task<SimfUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<SimfUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // -- Lifecycle -----------------------------------------------------------

    /// <summary>Creates a user with an initial password (sign-up).</summary>
    Task<IdentityResult> CreateAsync(SimfUser user, string password, CancellationToken cancellationToken = default);

    /// <summary>Creates a password-less user (admin invite path).</summary>
    Task<IdentityResult> CreateAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task<IdentityResult> UpdateAsync(SimfUser user, CancellationToken cancellationToken = default);

    // -- Credentials ---------------------------------------------------------

    Task<bool> CheckPasswordAsync(SimfUser user, string password, CancellationToken cancellationToken = default);

    Task<IdentityResult> AddPasswordAsync(SimfUser user, string password, CancellationToken cancellationToken = default);

    Task<IdentityResult> RemovePasswordAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task<IdentityResult> ChangePasswordAsync(SimfUser user, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    Task UpdateSecurityStampAsync(SimfUser user, CancellationToken cancellationToken = default);

    // -- Lockout / access tracking ------------------------------------------

    Task<bool> IsLockedOutAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task AccessFailedAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task ResetAccessFailedCountAsync(SimfUser user, CancellationToken cancellationToken = default);

    // -- Roles ---------------------------------------------------------------

    Task<IList<string>> GetRolesAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task<bool> IsInRoleAsync(SimfUser user, string role, CancellationToken cancellationToken = default);

    Task<IdentityResult> AddToRoleAsync(SimfUser user, string role, CancellationToken cancellationToken = default);

    Task<IdentityResult> RemoveFromRolesAsync(SimfUser user, IEnumerable<string> roles, CancellationToken cancellationToken = default);

    // -- TOTP / authenticator tokens ----------------------------------------

    Task<string?> GetAuthenticatorKeyAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task<IdentityResult> SetAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, string tokenValue, CancellationToken cancellationToken = default);

    Task<IdentityResult> SetTwoFactorEnabledAsync(SimfUser user, bool enabled, CancellationToken cancellationToken = default);
}
