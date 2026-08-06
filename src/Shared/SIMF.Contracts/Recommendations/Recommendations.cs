namespace SIMF.Contracts.Recommendations;

/// <summary>One matched person in
/// "Meet People Like You". Score is the Jaccard similarity over the
/// interest sets (|A ∩ B| / |A ∪ B|) tie-broken by intersection size
/// and a small same-ProfileType bonus.
/// <para><see cref="SharedSessionCount"/> is the
/// number of sessions both the caller and this match hold an approved,
/// un-released seat in; <see cref="MatchReason"/> / <see cref="MatchReasonArabic"/>
/// are the generated bilingual "why this match" line the card shows (sessions
/// + shared interests). All three are appended, so the wire contract stays
/// additive: the existing JSON field names are unchanged.</para></summary>
public sealed record RecommendationEntry(
    Guid UserProfileId,
    string EnglishName,
    string ArabicName,
    string? JobTitle,
    string? ProfileTypeName,
    string? ProfileTypeNameArabic,
    IReadOnlyList<MatchedInterest> SharedInterests,
    int SharedInterestCount,
    double Score,
    int SharedSessionCount,
    string MatchReason,
    string MatchReasonArabic);

/// <summary>One interest the two profiles share.</summary>
public sealed record MatchedInterest(
    Guid Id,
    string Name,
    string NameArabic);

/// <summary>The public response shape for
/// <c>GET /api/v1/app/account/recommendations/meet-like-you</c>.</summary>
public sealed record RecommendationsResponse(
    IReadOnlyList<RecommendationEntry> Matches);
