using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>Persistence for <see cref="AccountCode"/> records.</summary>
public interface IAccountCodeRepository
{
    /// <summary>Adds a new account code.</summary>
    Task AddAsync(AccountCode code, CancellationToken cancellationToken = default);

    /// <summary>
    /// The most recent unconsumed code for the user and purpose, regardless of
    /// whether it has expired; null if there is none. The caller decides what an
    /// expired code means, so "expired" and "no code" stay distinguishable.
    /// </summary>
    Task<AccountCode?> GetLatestUnconsumedAsync(
        Guid userId,
        AccountCodePurpose purpose,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the codes created for the user and purpose at or after the given
    /// time — used to cap how often a verification code may be re-issued.
    /// </summary>
    Task<int> CountCreatedSinceAsync(
        Guid userId,
        AccountCodePurpose purpose,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing code (consumption, attempt count).</summary>
    Task UpdateAsync(AccountCode code, CancellationToken cancellationToken = default);
}
