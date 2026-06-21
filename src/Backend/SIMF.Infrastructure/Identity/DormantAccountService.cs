using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>A1-19 (NCA) — disables Approved accounts inactive beyond the configured
/// threshold. Inactivity = time since the last successful sign-in, or the account's
/// creation time if it never signed in.</summary>
internal sealed class DormantAccountService(
    SimfIdentityDbContext dbContext,
    IRefreshTokenRepository refreshTokens,
    IAuditLog auditLog,
    IOptions<IdentityLifecycleOptions> options,
    TimeProvider timeProvider,
    ILogger<DormantAccountService> logger) : IDormantAccountService
{
    public async Task<int> DisableDormantAccountsAsync(CancellationToken cancellationToken = default)
    {
        var days = options.Value.DormantAccountDisableDays;
        if (days <= 0)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow();
        var cutoff = now.AddDays(-days);

        var dormant = await dbContext.Users
            .Where(user => user.AccountState == AccountState.Approved
                && (user.LastSuccessfulSignInAtUtc ?? user.CreatedAt) < cutoff)
            .ToListAsync(cancellationToken);
        if (dormant.Count == 0)
        {
            return 0;
        }

        foreach (var user in dormant)
        {
            user.AccountState = AccountState.Disabled;
            user.StateChangedAt = now;
            user.StateChangedByUserId = null; // system action, no actor
            user.UpdatedAt = now;
            // Roll the security stamp so any live access token is rejected at the
            // next call (the bearer validator compares the token stamp to this).
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var user in dormant)
        {
            await refreshTokens.RevokeAllForUserAsync(user.Id, now, cancellationToken);
            await auditLog.WriteAsync(
                new AuditEntry
                {
                    EventType = AuditEvents.AccountDormantDisabled,
                    Outcome = AuditOutcome.Success,
                    SubjectEmail = user.Email,
                    SubjectUserId = user.Id,
                    Detail = $"inactive >= {days}d",
                },
                cancellationToken);
        }

        logger.LogInformation(
            "Dormant-account sweep disabled {Count} account(s) inactive >= {Days} days.",
            dormant.Count, days);
        return dormant.Count;
    }
}
