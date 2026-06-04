using SIMF.Common;

namespace SIMF.Contracts.Feedback;

/// <summary>D-199 (Mockup screen 40) — attendee submits / revises their single
/// overall forum rating. <paramref name="Stars"/> is 1–5; <paramref name="Comment"/>
/// is the optional free-text comment.</summary>
public sealed record RateRequest(int Stars, string? Comment);

/// <summary>D-199 — the attendee's own rating as returned by the rate endpoint.</summary>
public sealed record RatingView(
    Guid Id,
    int Stars,
    string? Comment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>D-199 — one row in the admin Ratings list.</summary>
public sealed record AdminRatingSummary(
    Guid Id,
    Guid UserId,
    int Stars,
    string? Comment,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>D-199 — admin Ratings page: the grid of ratings plus the headline
/// aggregate (average stars + how many active ratings it was computed from).
/// <paramref name="AverageStars"/> is 0 when there are no active ratings.</summary>
public sealed record AdminRatingsPage(
    GridPage<AdminRatingSummary> Ratings,
    double AverageStars,
    int RatingCount);
