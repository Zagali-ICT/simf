namespace SIMF.Contracts.Delegations;

/// <summary>D-174 (gap doc G11, Mockup page 21) — public-facing
/// delegation entry. Served by <c>GET /api/v1/delegations</c>.</summary>
public sealed record PublicDelegation(
    Guid Id,
    string Name,
    string NameArabic,
    string? CountryName,
    string? CountryNameArabic,
    int MemberCount,
    bool IsPriority,
    bool IsInternational,
    int DisplayOrder);

public sealed record PublicDelegations(IReadOnlyList<PublicDelegation> Items);
