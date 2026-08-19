// Tests: SIMF.Api.Tests/RecommendationServiceTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Recommendations.Abstractions;
using SIMF.Common.Enums;
using SIMF.Contracts.Recommendations;
using SIMF.Infrastructure.IdentityAccess;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Recommendations;

/// <summary>
/// The "Meet People Like You" ranker.
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

    /// <summary>A recommendation requires a <b>&gt;=80% match</b>. Before this
    /// the ranker had no threshold at all: it sorted by score and simply took the
    /// top N, so the weakest possible overlap (one shared interest out of fifty)
    /// ranked as a "recommendation" whenever nothing better existed.</summary>
    internal const double StrongMatchThreshold = 0.80;

    /// <summary>How deep a strong-match pass ranks before filtering. The threshold
    /// is what decides inclusion; this only bounds the ranked list the filter runs
    /// over.</summary>
    private const int StrongMatchCandidatePool = 100;

    /// <summary>"80%" is a percentage, so it has to be compared against a
    /// number that cannot exceed 1.0. The raw <c>Score</c> can (a perfect Jaccard
    /// 1.0 plus <see cref="SameProfileTypeBonus"/>), so the threshold is applied to
    /// the score CLAMPED to [0,1]. The same-tier bonus is inside the comparison on
    /// purpose: a candidate in the caller's own tier IS the better match at equal
    /// overlap, which is why the bonus exists at all.</summary>
    internal static double NormaliseScore(double score) => Math.Clamp(score, 0d, 1d);

    public async Task<RecommendationsResponse> StrongMatchesAsync(
        Guid callerUserId, CancellationToken cancellationToken = default)
    {
        var ranked = await MeetPeopleLikeYouAsync(
            callerUserId, StrongMatchCandidatePool, cancellationToken);

        var strong = ranked.Matches
            .Where(match => NormaliseScore(match.Score) >= StrongMatchThreshold)
            .ToList();
        return new RecommendationsResponse(strong);
    }

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

        // 2) Shortlist the candidates on the App DB FIRST. A profile that shares
        //    no interest with the caller scores nothing and the ranker below
        //    drops it outright, so that filter belongs in SQL — otherwise every
        //    approved attendee's whole interest collection is materialised only
        //    to be discarded. Same set out, a fraction of the rows in.
        var callerInterestIds = caller.InterestIds;
        var shortlist = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId != null)
            .Where(p => p.ShowInMeetLikeYou)
            // Honour the per-type "Meet People" master switch too, so a
            // partner type an admin hid drops out of the recommender as well.
            .Where(p => p.ProfileType == null || p.ProfileType.ShowInPartnerDirectory)
            .Where(p => p.Interests.Any(i => callerInterestIds.Contains(i.Id)))
            .Select(p => new
            {
                p.Id,
                UserId = p.UserId!.Value,
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
        if (shortlist.Count == 0)
        {
            return new RecommendationsResponse(Array.Empty<RecommendationEntry>());
        }

        // 3) "Approved, non-Admin, not me" is an Identity fact, so ask Identity
        //    about the shortlist's account ids alone. Enumerating the whole
        //    approved pool and shipping it back as an IN (...) list moved every
        //    attendee id across the DB boundary on a screen every attendee opens.
        var shortlistAccountIds = shortlist.Select(p => p.UserId).Distinct().ToList();
        var approvedIds = (await identityDbContext.Users
            .AsNoTracking()
            .WhereApprovedNonAdmin(callerUserId)
            .Where(u => shortlistAccountIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();
        var candidates = shortlist.Where(p => approvedIds.Contains(p.UserId)).ToList();
        if (candidates.Count == 0)
        {
            return new RecommendationsResponse(Array.Empty<RecommendationEntry>());
        }

        // 3b) Shared-session overlap: the approved, un-released seats
        //     the caller and the candidate pool hold. A seat is held by an attendee
        //     PROFILE, and the candidates above already carry theirs, so the overlap
        //     is keyed on profile id; one query covers the whole pool + the caller.
        var poolProfileIds = candidates.Select(c => c.Id).ToHashSet();
        poolProfileIds.Add(caller.Id);
        var reservations = await appDbContext.SeatReservations
            .AsNoTracking()
            .Where(r => r.ReservedForProfileId != null
                && poolProfileIds.Contains(r.ReservedForProfileId.Value)
                && r.ReleasedAt == null
                && r.Status == BookingStatus.Approved)
            .Select(r => new { ProfileId = r.ReservedForProfileId!.Value, r.SessionId })
            .ToListAsync(cancellationToken);
        var callerSessions = reservations
            .Where(r => r.ProfileId == caller.Id)
            .Select(r => r.SessionId)
            .ToHashSet();
        var sessionsByProfile = reservations
            .Where(r => r.ProfileId != caller.Id)
            .GroupBy(r => r.ProfileId)
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
                && sessionsByProfile.TryGetValue(candidate.Id, out var candidateSessions))
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

    /// <summary>The bilingual "why this match" line (KSA frame
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
