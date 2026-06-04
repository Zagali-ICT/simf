namespace SIMF.Contracts.Sponsors;

/// <summary>D-199 (Mockup page 23) — one sponsor on the public sponsors screen.
/// Served by <c>GET /api/v1/app/sponsors</c>.
///
/// <para>SIMF-FDS-014 (D-287) — when the sponsor links a shared <c>Contact</c>,
/// the card's extra contact cluster (phone / email / social / map location) is
/// sourced live from that Contact; all are null when no contact is linked. The
/// fields are <b>additive</b> (append-only, D-219) so the shipped mobile wire
/// contract is preserved.</para></summary>
public sealed record PublicSponsor(
    Guid Id,
    string NameEn,
    string NameAr,
    int Tier,
    string TierName,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder,
    string? PhonePrimary = null,
    string? Email = null,
    string? FacebookUrl = null,
    string? XUrl = null,
    string? LinkedInUrl = null,
    string? InstagramUrl = null,
    double? Latitude = null,
    double? Longitude = null);

/// <summary>One tier section on the public sponsors screen — the heading plus
/// the sponsors that belong to it, already ordered.</summary>
public sealed record PublicSponsorTierGroup(
    int Tier,
    string TierName,
    IReadOnlyList<PublicSponsor> Sponsors);

/// <summary>Public sponsors response — sponsors grouped by tier (highest tier
/// first) so the client can render one section per group without re-sorting.</summary>
public sealed record PublicSponsors(IReadOnlyList<PublicSponsorTierGroup> Groups);
