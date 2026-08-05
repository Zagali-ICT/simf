using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Exhibitors;

/// <summary>
/// An exhibiting company, created in the Control Panel only. The name comes
/// first and login accounts are provisioned under it afterwards, each tracked as
/// an <see cref="ExhibitorMembership"/>. Soft-deleted through
/// <see cref="IsActive"/>, which the admin grids and pickers filter on.
/// </summary>
public sealed class Exhibitor : BaseAuditEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The primary surface.</summary>
    public string NameArabic { get; set; } = string.Empty;

    // The contact-card set is held inline on the entity rather than in a shared
    // contact directory.
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Website { get; set; }

    /// <summary>Shown as the pill on the exhibitor-detail screen. Null shows no
    /// pill at all.</summary>
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

    /// <summary>A real foreign key, since the country lives in the same
    /// database.</summary>
    public int? CountryId { get; set; }
    public Country? Country { get; set; }
}
