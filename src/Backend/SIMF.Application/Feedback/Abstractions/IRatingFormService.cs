using SIMF.Contracts.Feedback;

namespace SIMF.Application.Feedback.Abstractions;

/// <summary>App-facing dynamic rating operations: fetch the form an attendee
/// should render for a type (+ optional target), and upsert their submission.</summary>
public interface IRatingFormService
{
    /// <summary>Resolves the active rating form for the given type (by code or id)
    /// and optional target, including the attendee's existing submission (if any)
    /// for prefill.</summary>
    Task<RatingFormView> GetFormAsync(
        Guid userId, RatingFormRequest request, CancellationToken cancellationToken = default);

    /// <summary>Creates or revises the attendee's submission (upsert on
    /// <c>(UserId, RatingTypeId, TargetId)</c>). Re-submitting updates the
    /// overall score, comment and per-question answers.</summary>
    Task<RatingSubmissionView> SubmitAsync(
        Guid userId, SubmitRatingRequest request, CancellationToken cancellationToken = default);
}
