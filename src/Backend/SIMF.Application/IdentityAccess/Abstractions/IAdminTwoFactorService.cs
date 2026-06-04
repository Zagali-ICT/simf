using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Admin-driven two-factor reset (R2 — D-075, split out of the
/// monolithic <c>IAdminAccountService</c> per Architecture SEV-1.2).
/// </summary>
public interface IAdminTwoFactorService
{
    /// <summary>
    /// Wipes the target user's authenticator key + recovery codes + flips
    /// <c>TwoFactorEnabled</c> off, rolls the security stamp and revokes
    /// every refresh token. Audited with both actor and subject ids and a
    /// mandatory free-text reason (D-041).
    /// </summary>
    Task ResetTwoFactorAsync(
        Guid actorUserId,
        AdminResetTwoFactorRequest request,
        CancellationToken cancellationToken = default);
}
