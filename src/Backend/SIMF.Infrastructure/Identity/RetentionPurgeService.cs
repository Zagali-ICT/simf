// Tests: SIMF.Api.Tests/RetentionPurgeServiceTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// A4 (NCA data-minimisation) — periodic purge of dead security artifacts. Each
/// table is a single set-based <c>ExecuteDelete</c> against its own context, so
/// the Identity DB (refresh tokens, 2FA tickets, account codes) and the App DB
/// (gate-scan idempotency records) are swept independently — no cross-database
/// transaction (D-157). The retention windows are grace periods past each
/// artifact's functional lifetime, all clearly beyond any replay /
/// reuse-detection / rate-limit / forensic use.
/// </summary>
internal sealed class RetentionPurgeService(
    SimfIdentityDbContext identityDbContext,
    SimfAppDbContext appDbContext,
    TimeProvider timeProvider,
    ILogger<RetentionPurgeService> logger) : IRetentionPurgeService
{
    // Refresh tokens expire at the 24h session cap (D-443); keep them a further
    // week so reuse-detection + short-term forensics still resolve a presented
    // token before it is purged.
    private static readonly TimeSpan RefreshTokenRetention = TimeSpan.FromDays(7);

    // 2FA tickets live 5 minutes; account codes 10 minutes with a 1h re-issue
    // window. A day past expiry is well beyond both, so purging cannot affect a
    // live ticket or the rate-limit count (which keys off CreatedAt within 1h).
    private static readonly TimeSpan SecondFactorTokenRetention = TimeSpan.FromDays(1);
    private static readonly TimeSpan AccountCodeRetention = TimeSpan.FromDays(1);

    // Scan idempotency records replay for 24h; keep a further day so the full
    // replay window plus grace survives before purge.
    private static readonly TimeSpan ScanIdempotencyRetention = TimeSpan.FromDays(2);

    public async Task<RetentionPurgeResult> PurgeExpiredAsync(
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        // Precompute each cutoff as a plain DateTimeOffset so the query is an
        // unambiguous `column < @cutoff` (no in-query DateTimeOffset arithmetic).
        var refreshCutoff = now - RefreshTokenRetention;
        var secondFactorCutoff = now - SecondFactorTokenRetention;
        var accountCodeCutoff = now - AccountCodeRetention;
        var scanCutoff = now - ScanIdempotencyRetention;

        var refreshTokens = await identityDbContext.RefreshTokens
            .Where(token => token.ExpiresAt < refreshCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var secondFactorTokens = await identityDbContext.SecondFactorTokens
            .Where(token => token.ExpiresAt < secondFactorCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var accountCodes = await identityDbContext.AccountCodes
            .Where(code => code.ExpiresAt < accountCodeCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var scanIdempotencies = await appDbContext.ScanIdempotencies
            .Where(record => record.StoredAt < scanCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var result = new RetentionPurgeResult(
            refreshTokens, secondFactorTokens, accountCodes, scanIdempotencies);

        if (result.Total > 0)
        {
            logger.LogInformation(
                "Retention purge removed {RefreshTokens} refresh tokens, "
                    + "{SecondFactorTokens} 2FA tickets, {AccountCodes} account codes, "
                    + "{ScanIdempotencies} scan-idempotency records.",
                refreshTokens, secondFactorTokens, accountCodes, scanIdempotencies);
        }

        return result;
    }
}
