namespace SIMF.Contracts.Sponsors;

/// <summary>One sponsor on the public sponsors screen.
/// Served by <c>GET /api/v1/app/sponsors</c>.
///
/// <para>When the sponsor links a shared <c>Contact</c>,
/// the card's extra contact cluster (phone / email / social / map location) is
/// sourced live from that Contact; all are null when no contact is linked. The
/// fields are <b>additive</b> (append-only) so the shipped mobile wire
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
    double? Longitude = null,
    // Appended (append-only wire): the optional bilingual tagline shown
    // under the sponsor name.
    string? Tagline = null,
    string? TaglineArabic = null,
    // The sponsor's country (from the linked Contact) for the app's
    // corner flag. ISO 3166-1 numeric; the names are the label/fallback. Null
    // when no Contact is linked or it has no country. Appended (append-only).
    int? CountryId = null,
    string? CountryNameEn = null,
    string? CountryNameAr = null,
    // Whether a logo actually exists in the store for this sponsor. Appended
    // (append-only). LogoRelativePath above is retained for the shipped wire
    // and is now always null, so this is what a client must branch on: the
    // partner directory decides between a logo and an initials avatar on
    // exactly this question, and reading it off the retired path field would
    // mean no sponsor ever showed a logo again.
    bool HasLogo = false);

/// <summary>The full sponsor-detail view ("الراعي")
/// served by <c>GET /api/v1/app/sponsors/{id}</c> (anonymous). Adds the
/// "نبذة عن الراعي" about paragraph + the city to the card cluster; the website
/// (<see cref="Url"/>), tier and country are the same fields the list carries.
/// City + country are resolved from the linked <c>Contact</c>; the about is
/// sponsor-owned. All optional fields are null when unset.</summary>
public sealed record PublicSponsorDetail(
    Guid Id,
    string NameEn,
    string NameAr,
    int Tier,
    string TierName,
    string? LogoRelativePath,
    string? Url,
    string? About,
    string? AboutArabic,
    string? City,
    string? CityArabic,
    int? CountryId = null,
    string? CountryNameEn = null,
    string? CountryNameAr = null);

/// <summary>One tier section on the public sponsors screen — the heading plus
/// the sponsors that belong to it, already ordered.</summary>
public sealed record PublicSponsorTierGroup(
    int Tier,
    string TierName,
    IReadOnlyList<PublicSponsor> Sponsors);

/// <summary>Public sponsors response — sponsors grouped by tier (highest tier
/// first) so the client can render one section per group without re-sorting.</summary>
public sealed record PublicSponsors(IReadOnlyList<PublicSponsorTierGroup> Groups);
