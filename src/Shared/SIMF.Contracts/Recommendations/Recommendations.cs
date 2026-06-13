namespace SIMF.Contracts.Recommendations;

/// <summary>D-170 (gap doc G9, PDF §2.8) — one matched person in
/// "Meet People Like You". Score is the Jaccard similarity over the
/// interest sets (|A ∩ B| / |A ∪ B|) tie-broken by intersection size
/// and a small same-ProfileType bonus.</summary>
public sealed record RecommendationEntry(
    Guid UserProfileId,
    string EnglishName,
    string ArabicName,
    string? JobTitle,
    string? ProfileTypeName,
    string? ProfileTypeNameArabic,
    IReadOnlyList<MatchedInterest> SharedInterests,
    int SharedInterestCount,
    double Score);

/// <summary>D-170 — one interest the two profiles share.</summary>
public sealed record MatchedInterest(
    Guid Id,
    string Name,
    string NameArabic);

/// <summary>D-170 — the public response shape for
/// <c>GET /api/v1/app/account/recommendations/meet-like-you</c>.</summary>
public sealed record RecommendationsResponse(
    IReadOnlyList<RecommendationEntry> Matches);
