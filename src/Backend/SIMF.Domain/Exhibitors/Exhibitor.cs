using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Exhibitors;

/// <summary>An exhibiting company. Login accounts are provisioned under it afterwards,
/// each tracked as an <see cref="ExhibitorMembership"/>.</summary>
public sealed class Exhibitor : BaseAuditEntity
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;

    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Website { get; set; }

    public ExhibitorTier? Tier { get; set; }

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
