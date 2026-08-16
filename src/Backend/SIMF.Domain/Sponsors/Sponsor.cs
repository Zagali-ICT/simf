using SIMF.Common.Enums;
using SIMF.Domain.Common;
using SIMF.Domain.Files;

namespace SIMF.Domain.Sponsors;

/// <summary>One event sponsor. The public surface groups by <see cref="Tier"/>, Platinum first,
/// and orders within a tier by <see cref="DisplayOrder"/> then <see cref="NameArabic"/>.</summary>
public sealed class Sponsor : BaseAuditEntity
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;

    public SponsorTier Tier { get; set; } = SponsorTier.Bronze;

    public int DisplayOrder { get; set; }

    public Guid? LogoFileId { get; set; }

    public StoredFile? LogoFile { get; set; }
    public string? Url { get; set; }

    /// <summary>A short strapline under the sponsor name; omitted from the public
    /// projection when blank. The long form is <see cref="About"/>.</summary>
    public string? Tagline { get; set; }
    public string? TaglineArabic { get; set; }

    public string? About { get; set; }
    public string? AboutArabic { get; set; }

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

    /// <summary>ISO 3166-1 numeric country id.</summary>
    public int? CountryId { get; set; }

    public Country? Country { get; set; }
}
