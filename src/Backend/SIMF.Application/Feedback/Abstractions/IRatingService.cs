using SIMF.Common;
using SIMF.Contracts.Feedback;

namespace SIMF.Application.Feedback.Abstractions;

/// <summary>D-199 (Mockup screen 40) — forum rating operations: the attendee's
/// one-per-user upsert, and the admin list-with-average read.</summary>
public interface IRatingService
{
    /// <summary>Creates or revises the calling attendee's single rating
    /// (upsert on <c>UserId</c>). Re-rating an existing row updates it.</summary>
    Task<RatingView> RateAsync(
        Guid userId, RateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Admin list of ratings plus the average-stars aggregate over
    /// active ratings.</summary>
    Task<AdminRatingsPage> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);
}
