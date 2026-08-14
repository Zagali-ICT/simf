namespace SIMF.Contracts.Admin;

/// <summary>One row in the admin Sponsors grid.
/// Mirrors AdminDelegationSummary. <c>Tier</c> carries both the int value and a
/// display name so the grid renders the tier column without a second lookup.</summary>
public sealed record AdminSponsorSummary(
    Guid Id,
    string NameEn,
    string NameAr,
    int Tier,
    string TierName,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    // Carried so the grid Excel export can round-trip them (not rendered
    // as grid columns). Optional; blank when unset.
    string? Tagline = null,
    string? TaglineArabic = null,
    string? About = null,
    string? AboutArabic = null,
    // "an active SponsorLogo asset exists" so the grid renders the real
    // logo thumbnail, else an initials tile (set on read via a batched query).
    bool HasLogo = false);

/// <summary>Full sponsor detail (Details + Edit modals).</summary>
public sealed record AdminSponsorDetail(
    Guid Id,
    string NameEn,
    string NameAr,
    int Tier,
    string TierName,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // Optional bilingual tagline (Figma 922:2824).
    string? Tagline = null,
    string? TaglineArabic = null,
    // Optional bilingual about paragraph (≤2048 chars each).
    string? About = null,
    string? AboutArabic = null,
    // Contact identity-card fields inlined from the removed shared Contact
    // directory. All optional; nationality is CountryId with its
    // display names (CountryNameEn / CountryNameAr) projected alongside.
    // Trailing-optional so any other constructor call stays valid.
    int? CountryId = null,
    string? CountryNameEn = null,
    string? CountryNameAr = null,
    string? Email = null,
    string? PhonePrimary = null,
    string? PhoneSecondary = null,
    string? FacebookUrl = null,
    string? XUrl = null,
    string? LinkedInUrl = null,
    string? InstagramUrl = null,
    string? City = null,
    string? CityArabic = null,
    double? Latitude = null,
    double? Longitude = null);

/// <summary>Create payload for a sponsor. <c>Tier</c> is the int enum value
/// (10=Platinum, 20=Gold, 30=Silver, 40=Bronze).</summary>
public sealed class AdminCreateSponsorRequest
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int Tier { get; set; }
    public string? Url { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>Optional bilingual tagline (≤256 chars each).</summary>
    public string? Tagline { get; set; }
    public string? TaglineArabic { get; set; }

    /// <summary>Optional bilingual about paragraph (≤2048 chars each).</summary>
    public string? About { get; set; }
    public string? AboutArabic { get; set; }

    // Contact identity-card fields inlined from the removed shared Contact
    // directory. All optional; nationality is CountryId. The website
    // slot is the existing Url above (never re-added).
    public int? CountryId { get; set; }
    public string? Email { get; set; }
    public string? PhonePrimary { get; set; }
    public string? PhoneSecondary { get; set; }
    public string? FacebookUrl { get; set; }
    public string? XUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? City { get; set; }
    public string? CityArabic { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

/// <summary>Update payload (adds IsActive to the create shape).
/// Not sealed: the admin update endpoint binds {id}+body via a derived route
/// class (mirroring <c>UpdateExhibitorRoute</c>) so it cannot drop a
/// field at bind time.</summary>
public class AdminUpdateSponsorRequest
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int Tier { get; set; }
    public string? Url { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>Optional bilingual tagline (≤256 chars each).</summary>
    public string? Tagline { get; set; }
    public string? TaglineArabic { get; set; }

    /// <summary>Optional bilingual about paragraph (≤2048 chars each).</summary>
    public string? About { get; set; }
    public string? AboutArabic { get; set; }

    // Contact identity-card fields inlined from the removed shared Contact
    // directory. All optional; nationality is CountryId. The website
    // slot is the existing Url above (never re-added).
    public int? CountryId { get; set; }
    public string? Email { get; set; }
    public string? PhonePrimary { get; set; }
    public string? PhoneSecondary { get; set; }
    public string? FacebookUrl { get; set; }
    public string? XUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? City { get; set; }
    public string? CityArabic { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public bool IsActive { get; set; } = true;
}
