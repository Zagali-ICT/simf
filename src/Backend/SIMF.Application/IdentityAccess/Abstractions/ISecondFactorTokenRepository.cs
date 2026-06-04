using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>Persistence for <see cref="SecondFactorToken"/> tickets.</summary>
public interface ISecondFactorTokenRepository
{
    Task AddAsync(SecondFactorToken token, CancellationToken cancellationToken = default);

    /// <summary>Finds a ticket by its hash; null if there is no match.</summary>
    Task<SecondFactorToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(SecondFactorToken token, CancellationToken cancellationToken = default);
}
