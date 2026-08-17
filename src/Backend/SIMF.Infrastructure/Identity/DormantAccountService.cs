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

/// <summary>NCA dormancy rule — disables Approved accounts inactive beyond the configured
/// threshold. Inactivity = time since the last successful sign-in, or the account's
/// creation time if it never signed in.</summary>
/// <remarks>
/// PRE-ENABLE GATE: the <c>LastSuccessfulSignInAt ?? CreatedAt</c>
/// fallback below is intentional and unit-tested, but it means the FIRST sweep
/// after that column shipped — when it is still NULL for every
/// existing user — would fall back to <c>CreatedAt</c> and disable every
/// long-standing Approved non-admin at once. The sweep is therefore default-OFF
/// (<c>DormantAccountDisableDays &lt;= 0</c> returns early) and never touches
/// administrators. Do NOT set a positive <c>DormantAccountDisableDays</c> in
/// production until <c>LastSuccessfulSignInAt</c> has been backfilled (e.g.
/// stamped to deploy-time for existing approved users).
/// </remarks>
internal sealed class DormantAccountService(
    SimfIdentityDbContext dbContext,
    SimfAppDbContext appDbContext,
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

        // Admission is decided on the attendee's profile, not on the account, so
        // the sweep has to withdraw it there as well; disabling the account alone
        // left the gate and the offline hall roster still admitting the holder.
        //
        // Deliberately BEFORE the Identity save. A failure here leaves every one
        // of these users still Approved, so the next sweep re-selects them and
        // retries the pair; the reverse order would leave them disabled, out of
        // the query for ever, and admitted at the door with nothing to notice it.
        var dormantUserIds = dormant.Select(user => user.Id).ToList();
        await appDbContext.UserProfiles
            .Where(profile => profile.UserId != null
                && dormantUserIds.Contains(profile.UserId.Value)
                && profile.AdmissionState != AccountState.Disabled)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(profile => profile.AdmissionState, AccountState.Disabled)
                    .SetProperty(profile => profile.StateChangedAt, now)
                    // No actor, exactly as the account-side stamp below records it.
                    .SetProperty(profile => profile.StateChangedByUserId, (Guid?)null),
                cancellationToken);

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
