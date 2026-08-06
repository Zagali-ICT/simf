namespace SIMF.Contracts.PublicRelations;

// -- Public (anonymous) read projection --

/// <summary>One item in the public media-partner list.
/// The extra contact cluster (phone / email / social / map
/// location) is sourced live from the linked <c>Contact</c> when set, null
/// otherwise; the fields are additive (append-only).</summary>
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

/// <summary>The public media-partner list payload
/// (active rows only, ordered by DisplayOrder then NameArabic).</summary>
public sealed record PublicMediaPartners(IReadOnlyList<PublicMediaPartnerItem> Items);

// -- Admin CRUD projections --

/// <summary>Admin list-row projection of a media partner.</summary>
public sealed record AdminMediaPartnerSummary(
    Guid Id,
    string Name,
    string NameArabic,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    // "an active MediaPartnerLogo asset exists" so the grid renders the
    // real logo thumbnail, else an initials tile (set on read via a batched query).
    bool HasLogo = false);

/// <summary>Admin detail projection of a media partner.
/// The contact identity-card fields (phone / email / social / city /
/// map location / country) are inlined onto the row (all optional).</summary>
public sealed record AdminMediaPartnerDetail(
    Guid Id,
    string Name,
    string NameArabic,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // Inlined contact identity-card fields (all optional).
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

/// <summary>Create payload (Id is server-assigned).</summary>
public sealed record AdminCreateMediaPartnerRequest(
    string Name,
    string NameArabic,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder,
    // Inlined contact identity-card fields (all optional).
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

/// <summary>Update payload (Id travels in the route).</summary>
public sealed record AdminUpdateMediaPartnerRequest
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? LogoRelativePath { get; set; }
    public string? Url { get; set; }
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    // Inlined contact identity-card fields (all optional).
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
