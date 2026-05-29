// Tests: SIMF.Api.Tests/RecommendationServiceTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Recommendations.Abstractions;
using SIMF.Common.Enums;
using SIMF.Contracts.Recommendations;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Recommendations;

/// <summary>
/// D-170 (gap doc G9, PDF §2.8) — "Meet People Like You" ranker.
/// In-memory Jaccard pass over the caller's and candidates' interest
/// sets — fine at SIMF scale (target attendance is in the low
/// thousands, the interest catalog stays under 100). A 24-hour
/// per-user cache is deferred; the current pass returns ranked
/// matches on every request and stays simple.
/// </summary>
internal sealed class RecommendationService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    ILogger<RecommendationService> logger) : IRecommendationService
{
    /// <summary>Same-ProfileType bonus added on top of the Jaccard
    /// score so a tie between two equal-overlap candidates is broken
    /// in favour of the one sharing the caller's tier (VIPs meet VIPs,
    /// Staff meet Staff). Small enough to never override real overlap.</summary>
    private const double SameProfileTypeBonus = 0.05;

    public async Task<RecommendationsResponse> MeetPeopleLikeYouAsync(
        Guid callerUserId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var clamped = Math.Clamp(take is > 0 ? take : 20, 1, 100);

        // 1) Load the caller's profile + interest ids. No profile or
        //    no interests → empty response (the matcher needs at least
        //    one of each side to compare).
        var caller = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == callerUserId)
            .Select(p => new
            {
                p.Id,
                p.ProfileTypeId,
                InterestIds = p.Interests.Select(i => i.Id).ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (caller is null || caller.InterestIds.Count == 0)
        {
            return new RecommendationsResponse(Array.Empty<RecommendationEntry>());
        }
        var callerInterests = caller.InterestIds.ToHashSet();

        // 2) Load every Approved non-Admin user id from Identity DB —
        //    the pool the matcher considers.
        var approvedIds = await identityDbContext.Users
            .AsNoTracking()
            .Where(u => u.AccountState == AccountState.Approved
                && u.UserType != UserType.Admin
                && u.Id != callerUserId)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        if (approvedIds.Count == 0)
        {
            return new RecommendationsResponse(Array.Empty<RecommendationEntry>());
        }

        // 3) Pull candidate profiles + interest sets + display fields.
        var candidates = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => approvedIds.Contains(p.UserId))
            .Select(p => new
            {
                p.Id,
                p.EnglishName,
                p.ArabicName,
                p.JobTitle,
                p.ProfileTypeId,
                ProfileTypeName = p.ProfileType != null ? p.ProfileType.Name : null,
                ProfileTypeNameArabic = p.ProfileType != null ? p.ProfileType.NameArabic : null,
                Interests = p.Interests.Select(i => new
                {
                    i.Id, i.Name, i.NameArabic,
                }).ToList(),
            })
            .ToListAsync(cancellationToken);

        // 4) Score each candidate by Jaccard over the interest sets;
        //    add the same-ProfileType bonus when applicable.
        var ranked = new List<RecommendationEntry>();
        foreach (var candidate in candidates)
        {
            if (candidate.Interests.Count == 0) { continue; }

            var sharedSet = new List<MatchedInterest>();
            foreach (var interest in candidate.Interests)
            {
                if (callerInterests.Contains(interest.Id))
                {
                    sharedSet.Add(new MatchedInterest(
                        interest.Id, interest.Name, interest.NameArabic));
                }
            }
            if (sharedSet.Count == 0) { continue; }

            var unionCount = callerInterests.Count
                + candidate.Interests.Count
                - sharedSet.Count;
            var jaccard = (double)sharedSet.Count / unionCount;
            var score = jaccard;
            if (candidate.ProfileTypeId.HasValue
                && candidate.ProfileTypeId == caller.ProfileTypeId)
            {
                score += SameProfileTypeBonus;
            }

            ranked.Add(new RecommendationEntry(
                candidate.Id,
                candidate.EnglishName,
                candidate.ArabicName,
                candidate.JobTitle,
                candidate.ProfileTypeName,
                candidate.ProfileTypeNameArabic,
                sharedSet,
                sharedSet.Count,
                score));
        }

        var top = ranked
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.SharedInterestCount)
            .ThenBy(r => r.EnglishName)
            .Take(clamped)
            .ToList();

        logger.LogDebug(
            "MeetPeopleLikeYou for {Caller}: {Total} candidates scored, top {Take} returned",
            callerUserId, ranked.Count, top.Count);

        return new RecommendationsResponse(top);
    }
}
