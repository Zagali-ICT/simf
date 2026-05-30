namespace SIMF.Contracts.Sponsors;

/// <summary>D-199 (Mockup page 23) — one sponsor on the public sponsors screen.
/// Served by <c>GET /api/v1/sponsors</c>.</summary>
public sealed record PublicSponsor(
    Guid Id,
    string NameEn,
    string NameAr,
    int Tier,
    string TierName,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder);

/// <summary>One tier section on the public sponsors screen — the heading plus
/// the sponsors that belong to it, already ordered.</summary>
public sealed record PublicSponsorTierGroup(
    int Tier,
    string TierName,
    IReadOnlyList<PublicSponsor> Sponsors);

/// <summary>Public sponsors response — sponsors grouped by tier (highest tier
/// first) so the client can render one section per group without re-sorting.</summary>
public sealed record PublicSponsors(IReadOnlyList<PublicSponsorTierGroup> Groups);
