using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Sponsors;

/// <summary>
/// One event sponsor, shown on the public sponsors screen and managed from the
/// Control Panel. The public surface groups by <see cref="Tier"/>, Platinum
/// first, and orders within a tier by <see cref="DisplayOrder"/> then
/// <see cref="NameArabic"/>. Soft-deleted through <see cref="IsActive"/>, which
/// the public list filters on.
/// </summary>
public sealed class Sponsor : BaseAuditEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The primary surface on the mobile app and the public website.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Drives both the grouping heading and the top-level ordering on
    /// the public screen.</summary>
    public SponsorTier Tier { get; set; } = SponsorTier.Bronze;

    /// <summary>Ascending sort key within a tier, tie-broken by
    /// <see cref="NameArabic"/>.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Path to the logo asset, resolved against the static asset root.
    /// Never an absolute URL.</summary>
    public string? LogoRelativePath { get; set; }

    /// <summary>Outbound link to the sponsor's own website.</summary>
    public string? Url { get; set; }

    /// <summary>A short strapline under the sponsor name, such as "الراعي
    /// الاستراتيجي". Omitted from the public projection when blank.</summary>
    public string? Tagline { get; set; }

    /// <summary>The primary surface.</summary>
    public string? TaglineArabic { get; set; }

    /// <summary>The full about paragraph on the sponsor-detail screen, distinct
    /// from the one-line <see cref="Tagline"/>.</summary>
    public string? About { get; set; }
    public string? AboutArabic { get; set; }

    // Contact-card fields, inlined when the shared contact directory was removed.
    // The website slot is the Url above, and the logo is LogoRelativePath; neither
    // was re-added here.
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

    /// <summary>ISO 3166-1 numeric country id. A real foreign key, since the
    /// country lives in the same database.</summary>
    public int? CountryId { get; set; }

    public Country? Country { get; set; }
}
