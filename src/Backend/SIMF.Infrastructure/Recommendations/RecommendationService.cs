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
            .Where(p => p.ShowInMeetLikeYou)
            .Select(p => new
            {
                p.Id,
                p.UserId,
                p.Name,
                p.NameArabic,
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

        // 3b) Shared-session overlap (D-451): the approved, un-released seats
        //     the caller and the candidate pool hold. SeatReservation owners are
        //     Identity user ids (like the pool), so the overlap is keyed on user
        //     id; one query covers the whole pool + the caller.
        var poolUserIds = approvedIds.ToHashSet();
        poolUserIds.Add(callerUserId);
        var reservations = await appDbContext.SeatReservations
            .AsNoTracking()
            .Where(r => r.ReservedForUserId != null
                && poolUserIds.Contains(r.ReservedForUserId.Value)
                && r.ReleasedAt == null
                && r.Status == BookingStatus.Approved)
            .Select(r => new { UserId = r.ReservedForUserId!.Value, r.SessionId })
            .ToListAsync(cancellationToken);
        var callerSessions = reservations
            .Where(r => r.UserId == callerUserId)
            .Select(r => r.SessionId)
            .ToHashSet();
        var sessionsByUser = reservations
            .Where(r => r.UserId != callerUserId)
            .GroupBy(r => r.UserId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.SessionId).ToHashSet());

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

            var sharedSessionCount = 0;
            if (callerSessions.Count > 0
                && sessionsByUser.TryGetValue(candidate.UserId, out var candidateSessions))
            {
                foreach (var sessionId in candidateSessions)
                {
                    if (callerSessions.Contains(sessionId)) { sharedSessionCount++; }
                }
            }
            var (reasonEn, reasonAr) = BuildMatchReason(sharedSessionCount, sharedSet);

            ranked.Add(new RecommendationEntry(
                candidate.Id,
                candidate.Name,
                candidate.NameArabic,
                candidate.JobTitle,
                candidate.ProfileTypeName,
                candidate.ProfileTypeNameArabic,
                sharedSet,
                sharedSet.Count,
                score,
                sharedSessionCount,
                reasonEn,
                reasonAr));
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

    /// <summary>D-451 — the bilingual "why this match" line (KSA frame
    /// 1072:13409): the session-overlap segment (when any) then the
    /// shared-interest summary, joined by " · ". A single shared interest names
    /// it ("shared interest in X"); two or more are summarised by count.
    /// Candidates with zero shared interests are skipped before this runs, so
    /// the interest segment is always present and the reason is never blank.</summary>
    private static (string En, string Ar) BuildMatchReason(
        int sharedSessions,
        IReadOnlyList<MatchedInterest> sharedInterests)
    {
        var en = new List<string>();
        var ar = new List<string>();

        if (sharedSessions == 1)
        {
            en.Add("attended a shared session");
            ar.Add("حضر نفس الجلسة");
        }
        else if (sharedSessions == 2)
        {
            en.Add("2 shared sessions");
            // "نفس جلستين" is the Figma-exact Arabic dual (frame 1072:13409);
            // keep this wording — do not "normalise" it to the numeric form.
            ar.Add("نفس جلستين");
        }
        else if (sharedSessions > 2)
        {
            en.Add($"{sharedSessions} shared sessions");
            ar.Add($"نفس {sharedSessions} جلسات");
        }

        if (sharedInterests.Count == 1)
        {
            var interest = sharedInterests[0];
            var enName = string.IsNullOrWhiteSpace(interest.Name)
                ? interest.NameArabic : interest.Name;
            var arName = string.IsNullOrWhiteSpace(interest.NameArabic)
                ? interest.Name : interest.NameArabic;
            en.Add($"shared interest in {enName}");
            ar.Add($"اهتمام مشترك في {arName}");
        }
        else if (sharedInterests.Count > 1)
        {
            en.Add($"{sharedInterests.Count} shared interests");
            ar.Add($"{sharedInterests.Count} اهتمامات مشتركة");
        }

        return (string.Join(" · ", en), string.Join(" · ", ar));
    }
}
