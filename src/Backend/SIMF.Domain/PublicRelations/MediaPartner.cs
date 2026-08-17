using SIMF.Domain.Common;
using SIMF.Domain.Files;

namespace SIMF.Domain.PublicRelations;

/// <summary>One media partner in the app's partner grid, ordered by
/// <see cref="DisplayOrder"/> then <see cref="NameArabic"/>.</summary>
public sealed class MediaPartner : BaseAuditEntity
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>A card without a logo falls back to the name.</summary>
    public Guid? LogoFileId { get; set; }

    public StoredFile? LogoFile { get; set; }
    /// <summary>Outbound link to the partner's site; also the website slot of the contact card.</summary>
    public string? Url { get; set; }

    public int DisplayOrder { get; set; }

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

    public int? CountryId { get; set; }

    public Country? Country { get; set; }
}
