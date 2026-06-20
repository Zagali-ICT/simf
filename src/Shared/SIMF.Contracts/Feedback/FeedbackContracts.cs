using SIMF.Common;

namespace SIMF.Contracts.Feedback;

/// <summary>D-199 (Mockup screen 40) — attendee submits / revises their single
/// overall forum rating. <paramref name="Stars"/> is 1–5; <paramref name="Comment"/>
/// is the optional free-text comment. The four <c>*Stars</c> per-element scores
/// (Figma 1116:16894 "قيّم العناصر") are each an optional 1–5; null when the
/// attendee skips that element. Appended (nullable, defaulted) so the shipped
/// overall-only wire contract stays valid.</summary>
public sealed record RateRequest(
    int Stars,
    string? Comment,
    int? OrganizationStars = null,
    int? ContentStars = null,
    int? AppStars = null,
    int? VenueStars = null);

/// <summary>D-199 — the attendee's own rating as returned by the rate endpoint.
/// The four per-element scores echo back what was saved (null when skipped).</summary>
public sealed record RatingView(
    Guid Id,
    int Stars,
    string? Comment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int? OrganizationStars = null,
    int? ContentStars = null,
    int? AppStars = null,
    int? VenueStars = null);

/// <summary>D-199 — one row in the admin Ratings list. The four per-element
/// scores surface in the CP ratings grid (null when the attendee skipped them).</summary>
public sealed record AdminRatingSummary(
    Guid Id,
    Guid UserId,
    int Stars,
    string? Comment,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int? OrganizationStars = null,
    int? ContentStars = null,
    int? AppStars = null,
    int? VenueStars = null);

/// <summary>D-199 — admin Ratings page: the grid of ratings plus the headline
/// aggregate (average stars + how many active ratings it was computed from).
/// <paramref name="AverageStars"/> is 0 when there are no active ratings.</summary>
public sealed record AdminRatingsPage(
    GridPage<AdminRatingSummary> Ratings,
    double AverageStars,
    int RatingCount);
