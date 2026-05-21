using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// The session-lifecycle use cases — refresh-token rotation and sign-out
/// (SIMF-API-001 section 12.4, SIMF-FDS-001 section 5.3).
/// </summary>
public interface ISessionService
{
    Task<AuthTokens> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default);

    Task SignOutAsync(Guid userId, CancellationToken cancellationToken = default);
}
