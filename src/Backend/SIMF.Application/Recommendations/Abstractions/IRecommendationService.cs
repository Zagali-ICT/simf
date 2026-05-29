using SIMF.Contracts.Recommendations;

namespace SIMF.Application.Recommendations.Abstractions;

/// <summary>
/// D-170 (gap doc G9, PDF §2.8) — "Meet People Like You" matcher.
/// Read-only service over the shared <see cref="SIMF.Domain.Profiles.UserProfile.Interests"/>
/// M-to-M. Ranks candidate profiles by Jaccard similarity over their
/// interest sets, with a small same-ProfileType bonus to break ties.
/// </summary>
public interface IRecommendationService
{
    /// <summary>Returns the top <paramref name="take"/> candidates
    /// most similar to the caller. Excludes the caller themselves,
    /// non-Approved users, users without a profile, and users with
    /// zero shared interests (score = 0). Caller must have a profile
    /// with at least one interest, else returns empty.</summary>
    Task<RecommendationsResponse> MeetPeopleLikeYouAsync(
        Guid callerUserId,
        int take,
        CancellationToken cancellationToken = default);
}
