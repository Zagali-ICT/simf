using SIMF.Common.Enums;
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
    /// The same, for a code issued to an attendee who holds no account yet.
    ///
    /// <para>A separate method rather than a nullable parameter on the one
    /// above: both ids are <see cref="Guid"/>, so nothing but the parameter name
    /// would stop one being passed where the other belongs, and that mistake
    /// matches no row rather than failing - handing the caller "no code" for a
    /// code that is sitting right there.</para>
    /// </summary>
    Task<AccountCode?> GetLatestUnconsumedForProfileAsync(
        Guid userProfileId,
        AccountCodePurpose purpose,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the codes created for the user and purpose at or after the given
    /// time — used to cap how often a verification code may be re-issued.
    /// </summary>
    Task<int> CountCreatedSinceAsync(
        Guid userId,
        AccountCodePurpose purpose,
        DateTime since,
        CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing code (consumption, attempt count).</summary>
    Task UpdateAsync(AccountCode code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically consumes the code in a single conditional UPDATE, but only
    /// while it is still unconsumed. Returns <c>true</c> only for the caller that
    /// flips <see cref="AccountCode.ConsumedAt"/> from null to
    /// <paramref name="now"/> — so a code is single-use even under a concurrent
    /// double-submit (no read-modify-write race).
    /// </summary>
    Task<bool> TryConsumeAsync(
        Guid codeId, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments the wrong-value attempt counter in a single UPDATE
    /// and returns the new count, so a per-code attempt cap cannot be bypassed by
    /// a read-modify-write that loses increments under concurrency.
    /// </summary>
    Task<int> IncrementAttemptCountAsync(
        Guid codeId, CancellationToken cancellationToken = default);
}
