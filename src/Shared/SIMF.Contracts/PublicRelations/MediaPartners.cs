namespace SIMF.Contracts.PublicRelations;

// -- Public (anonymous) read projection --

/// <summary>D-199 (Mockup page 31) — one item in the public media-partner list.
/// SIMF-FDS-014 (D-287): the extra contact cluster (phone / email / social / map
/// location) is sourced live from the linked <c>Contact</c> when set, null
/// otherwise; the fields are additive (append-only, D-219).</summary>
public sealed record PublicMediaPartnerItem(
    Guid Id,
    string Name,
    string NameArabic,
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

/// <summary>D-199 (Mockup page 31) — the public media-partner list payload
/// (active rows only, ordered by DisplayOrder then NameArabic).</summary>
public sealed record PublicMediaPartners(IReadOnlyList<PublicMediaPartnerItem> Items);

// -- Admin CRUD projections --

/// <summary>D-199 — admin list-row projection of a media partner.</summary>
public sealed record AdminMediaPartnerSummary(
    Guid Id,
    string Name,
    string NameArabic,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    // D-740 — "an active MediaPartnerLogo asset exists" so the grid renders the
    // real logo thumbnail, else an initials tile (set on read via a batched query).
    bool HasLogo = false);

/// <summary>D-199 — admin detail projection of a media partner.
/// D-766: the contact identity-card fields (phone / email / social / city /
/// map location / country) are inlined onto the row (all optional).</summary>
public sealed record AdminMediaPartnerDetail(
    Guid Id,
    string Name,
    string NameArabic,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    // D-766 — inlined contact identity-card fields (all optional).
    string? Email = null,
    string? PhonePrimary = null,
    string? PhoneSecondary = null,
    string? FacebookUrl = null,
    string? XUrl = null,
    string? LinkedInUrl = null,
    string? InstagramUrl = null,
    string? City = null,
    string? CityArabic = null,
    int? CountryId = null,
    string? CountryNameEn = null,
    string? CountryNameAr = null,
    double? Latitude = null,
    double? Longitude = null);

/// <summary>D-199 — create payload (Id is server-assigned).</summary>
public sealed record AdminCreateMediaPartnerRequest(
    string Name,
    string NameArabic,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder,
    // D-766 — inlined contact identity-card fields (all optional).
    string? Email = null,
    string? PhonePrimary = null,
    string? PhoneSecondary = null,
    string? FacebookUrl = null,
    string? XUrl = null,
    string? LinkedInUrl = null,
    string? InstagramUrl = null,
    string? City = null,
    string? CityArabic = null,
    int? CountryId = null,
    double? Latitude = null,
    double? Longitude = null);

/// <summary>D-199 — update payload (Id travels in the route).</summary>
public sealed record AdminUpdateMediaPartnerRequest
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? LogoRelativePath { get; set; }
    public string? Url { get; set; }
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    // D-766 — inlined contact identity-card fields (all optional).
    public string? Email { get; set; }
    public string? PhonePrimary { get; set; }
    public string? PhoneSecondary { get; set; }
    public string? FacebookUrl { get; set; }
    public string? XUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? City { get; set; }
    public string? CityArabic { get; set; }
    public int? CountryId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
