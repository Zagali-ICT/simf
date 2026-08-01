using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

namespace SIMF.Infrastructure.Identity;

/// <summary>A1-19 (NCA) — disables Approved accounts inactive beyond the configured
/// threshold. Inactivity = time since the last successful sign-in, or the account's
/// creation time if it never signed in.</summary>
/// <remarks>
/// PRE-ENABLE GATE (D-494): the <c>LastSuccessfulSignInAt ?? CreatedAt</c>
/// fallback below is intentional and unit-tested, but it means the FIRST sweep
/// after the Wave-6d column shipped — when that column is still NULL for every
/// existing user — would fall back to <c>CreatedAt</c> and disable every
/// long-standing Approved non-admin at once. The sweep is therefore default-OFF
/// (<c>DormantAccountDisableDays &lt;= 0</c> returns early) and never touches
/// administrators. Do NOT set a positive <c>DormantAccountDisableDays</c> in
/// production until <c>LastSuccessfulSignInAt</c> has been backfilled (e.g.
/// stamped to deploy-time for existing approved users). See the merge-readiness
/// doc §5.1 and DECISIONS_LOG D-494.
/// </remarks>
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

        var now = timeProvider.SimfNow();
        var cutoff = now.AddDays(-days);

        var dormant = await dbContext.Users
            .Where(user => user.AccountState == AccountState.Approved
                // Never auto-disable administrators — the sweep must not be able to
                // lock out the (only) admin and brick the Control Panel, including
                // the seeded super-admin (Approved + UserType.Admin).
                && user.UserType != UserType.Admin
                && (user.LastSuccessfulSignInAt ?? user.CreatedAt) < cutoff)
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
