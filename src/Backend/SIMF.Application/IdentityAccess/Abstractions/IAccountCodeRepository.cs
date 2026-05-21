using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>Persistence for <see cref="AccountCode"/> records.</summary>
public interface IAccountCodeRepository
{
    /// <summary>Adds a new account code.</summary>
    Task AddAsync(AccountCode code, CancellationToken cancellationToken = default);

    /// <summary>
    /// The most recent unconsumed, unexpired code for the user and purpose;
    /// null if there is none.
    /// </summary>
    Task<AccountCode?> GetLatestActiveAsync(
        Guid userId,
        AccountCodePurpose purpose,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing code (consumption, attempt count).</summary>
    Task UpdateAsync(AccountCode code, CancellationToken cancellationToken = default);
}
