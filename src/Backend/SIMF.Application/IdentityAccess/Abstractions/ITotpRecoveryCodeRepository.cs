using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Persistence for the single-use recovery codes that act as a fallback for
/// TOTP (decision D-040). Plaintext codes are never stored; the repository
/// only ever sees hashes.
/// </summary>
public interface ITotpRecoveryCodeRepository
{
    /// <summary>Persists a batch of hashes for a user (typically ten at a time).</summary>
    Task AddBatchAsync(
        Guid userId,
        IReadOnlyList<string> codeHashes,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the active (un-consumed) code matching the hash, or <c>null</c>
    /// when none does.
    /// </summary>
    Task<TotpRecoveryCode?> FindActiveAsync(
        Guid userId,
        string codeHash,
        CancellationToken cancellationToken = default);

    /// <summary>Marks the code as consumed at <paramref name="now"/>.</summary>
    Task ConsumeAsync(
        TotpRecoveryCode code,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>Counts the active (un-consumed) codes for the user.</summary>
    Task<int> CountActiveAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every recovery code for the user — used on disable and on
    /// regenerate.
    /// </summary>
    Task RevokeAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
