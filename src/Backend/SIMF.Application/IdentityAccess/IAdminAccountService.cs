using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Admin-driven account-management use cases. Today: reset another user's
/// authenticator-app 2FA (decision D-041). Belongs to <c>myComment</c> #33
/// (CP User Management) — this is the first slice that ships ahead of the
/// rest of the module.
/// </summary>
public interface IAdminAccountService
{
    /// <summary>
    /// Wipes the target user's authenticator key + recovery codes + flips
    /// <c>TwoFactorEnabled</c> off, rolls the security stamp and revokes
    /// every refresh token. Audited with both actor and subject ids and a
    /// mandatory free-text reason.
    /// </summary>
    Task ResetTwoFactorAsync(
        Guid actorUserId,
        AdminResetTwoFactorRequest request,
        CancellationToken cancellationToken = default);
}
