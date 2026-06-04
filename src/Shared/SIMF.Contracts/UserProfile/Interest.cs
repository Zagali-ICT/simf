namespace SIMF.Contracts.UserProfile;

/// <summary>One interest the visitor can pick (P9 — D-050; الاهتمامات).
/// Returned by <c>GET /api/v1/account/interests</c>. The list contains
/// only active rows, ordered by <c>DisplayOrder</c> then <c>Name</c>.</summary>
public sealed record InterestDto(
    Guid Id,
    string Name,
    string NameArabic,
    int DisplayOrder);

/// <summary>The body of <c>GET /api/v1/account/interests</c> (P9).</summary>
public sealed record InterestListResponse(IReadOnlyList<InterestDto> Interests);
