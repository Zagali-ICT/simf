using SIMF.Common.Enums;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.IdentityAccess;

/// <summary>
/// The "who counts as a person" rule for the app-facing people features. The
/// pool is every <b>Approved, non-Admin</b> Identity account (optionally minus
/// one caller). Factored so <see cref="Recommendations.RecommendationService"/>
/// ("Meet People Like You" ranker) and
/// <see cref="Networking.PartnerDirectoryService"/> (partner directory) share
/// one definition of the pool instead of repeating the predicate.
/// </summary>
internal static class ApprovedAccountQuery
{
    /// <summary>Filters the Identity users to the Approved, non-Admin pool. When
    /// <paramref name="excludeUserId"/> is supplied that account is dropped (a
    /// caller does not match themselves). The exclusion is applied as a separate
    /// <c>Where</c> only when set, so the emitted SQL is identical to the two
    /// call sites' original hand-written predicates.</summary>
    public static IQueryable<SimfUser> WhereApprovedNonAdmin(
        this IQueryable<SimfUser> users, Guid? excludeUserId = null)
    {
        var pool = users.Where(u => u.AccountState == AccountState.Approved
            && u.UserType != UserType.Admin);
        return excludeUserId is { } id ? pool.Where(u => u.Id != id) : pool;
    }
}
