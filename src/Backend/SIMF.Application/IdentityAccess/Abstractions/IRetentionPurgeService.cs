namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// A4 (NCA data-minimisation) — purges dead security artifacts that are well
/// past any functional, replay, reuse-detection, rate-limit or forensic use:
/// expired refresh tokens, expired 2FA tickets, expired account codes, and
/// expired gate-scan idempotency records. Idempotent and set-based; safe to run
/// repeatedly and concurrently with normal traffic. Each table is swept
/// independently — there is no cross-database transaction.
/// </summary>
public interface IRetentionPurgeService
{
    /// <summary>Deletes every artifact past its retention window and returns the
    /// per-table counts removed.</summary>
    Task<RetentionPurgeResult> PurgeExpiredAsync(CancellationToken cancellationToken = default);
}

/// <summary>The number of rows removed per artifact table in one purge pass.</summary>
public sealed record RetentionPurgeResult(
    int RefreshTokens,
    int SecondFactorTokens,
    int AccountCodes,
    int ScanIdempotencies)
{
    public int Total =>
        RefreshTokens + SecondFactorTokens + AccountCodes + ScanIdempotencies;
}
