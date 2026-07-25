using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Exhibitors;

/// <summary>
/// D-199 #3 (additive schema) — an exhibitor managed from the Control Panel.
/// The owner model is "create the EXHIBITOR NAME first, then create the login
/// ACCOUNTS under it"; the accounts are tracked as
/// <see cref="ExhibitorMembership"/> rows. Exhibitors are CP-created only.
/// Soft-deleted via <see cref="IsActive"/> — admin grids and pickers filter
/// <c>IsActive == true</c>.
/// </summary>
public sealed class Exhibitor : BaseAuditEntity
{

    /// <summary>English display name (1–256 chars).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic display name (1–256 chars) — the primary surface.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Optional primary contact email (≤320 chars). Retained per
    /// SIMF-FDS-014 (D-260): the entity keeps its own inline contact fields
    /// (the "identity-card" set is inlined directly onto the entity).</summary>
    public string? ContactEmail { get; set; }

    /// <summary>Optional primary contact phone (≤32 chars).</summary>
    public string? ContactPhone { get; set; }

    /// <summary>Optional website (≤512 chars).</summary>
    public string? Website { get; set; }

    /// <summary>Wave 3 (Figma 1439:11881) — the exhibitor's tier, shown as the
    /// pill on the exhibitor-detail screen ("عارض بريميوم"). Null = no tier pill.
    /// Additive (D-219).</summary>
    public ExhibitorTier? Tier { get; set; }

    /// <summary>Optional secondary contact phone (≤32 chars).</summary>
    public string? PhoneSecondary { get; set; }

    /// <summary>Optional Facebook profile URL (≤256 chars).</summary>
    public string? FacebookUrl { get; set; }

    /// <summary>Optional X (Twitter) profile URL (≤256 chars).</summary>
    public string? XUrl { get; set; }

    /// <summary>Optional LinkedIn profile URL (≤256 chars).</summary>
    public string? LinkedInUrl { get; set; }

    /// <summary>Optional Instagram profile URL (≤256 chars).</summary>
    public string? InstagramUrl { get; set; }

    /// <summary>Optional English city name (≤128 chars).</summary>
    public string? City { get; set; }

    /// <summary>Optional Arabic city name (≤128 chars).</summary>
    public string? CityArabic { get; set; }

    /// <summary>Optional map latitude.</summary>
    public double? Latitude { get; set; }

    /// <summary>Optional map longitude.</summary>
    public double? Longitude { get; set; }

    /// <summary>Optional same-DB country (logical + real FK to <see cref="Country"/>).
    /// Null until set.</summary>
    public int? CountryId { get; set; }
    public Country? Country { get; set; }

}
