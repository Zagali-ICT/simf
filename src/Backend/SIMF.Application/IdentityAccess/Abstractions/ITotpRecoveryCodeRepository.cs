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
    /// Atomically verifies + consumes the active (un-consumed) code matching the
    /// hash in one conditional UPDATE (<c>WHERE UserId AND CodeHash AND
    /// ConsumedAt IS NULL</c>). Returns <c>true</c> only for the caller that
    /// consumes it, so a recovery code is single-use even under a concurrent
    /// double-submit (replaces the old find-then-consume read-modify-write).
    /// </summary>
    Task<bool> TryConsumeAsync(
        Guid userId,
        string codeHash,
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
