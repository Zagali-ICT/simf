using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>Persistence for <see cref="RefreshToken"/> records.</summary>
public interface IRefreshTokenRepository
{
    /// <summary>Adds a new refresh token.</summary>
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    /// <summary>Finds a token by its hash; null if there is no match.</summary>
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing token (for example, on rotation).</summary>
    Task UpdateAsync(RefreshToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every still-live refresh token for the user — used on sign-out
    /// and when reuse of a rotated token is detected.
    /// </summary>
    Task RevokeAllForUserAsync(
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default);
}
