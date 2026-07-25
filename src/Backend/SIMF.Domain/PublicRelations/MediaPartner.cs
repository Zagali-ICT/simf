using SIMF.Domain.Common;

namespace SIMF.Domain.PublicRelations;

/// <summary>
/// D-199 (Mockup page 31 — "شركاء النجاح" / "شركاء وسائل الإعلام") — one media
/// partner shown in the mobile app's media-partner grid. Each card renders a
/// logo (<see cref="LogoRelativePath"/>) and, optionally, links out to the
/// partner's site (<see cref="Url"/>). The public list is ordered by
/// <see cref="DisplayOrder"/> ascending, tie-broken by <see cref="NameArabic"/>.
/// </summary>
public sealed class MediaPartner : BaseAuditEntity
{
    /// <summary>English display name (1–256 chars).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic display name — the primary surface on the mobile app
    /// (1–256 chars).</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Relative path to the partner's logo asset (e.g.
    /// "media-partners/cnn.png"), resolved against the app's static asset
    /// root. Optional — a card with no logo falls back to the name.
    /// Stored as a relative path (never an absolute URL) so the asset host
    /// can change without rewriting rows. ≤ 512 chars.</summary>
    public string? LogoRelativePath { get; set; }

    /// <summary>Optional outbound link to the partner's website. ≤ 512 chars.
    /// Null when the partner has no public site to link to.</summary>
    public string? Url { get; set; }

    /// <summary>Sort key on the public list — ascending. Tie-broken by
    /// <see cref="NameArabic"/>. (≥ 0.)</summary>
    public int DisplayOrder { get; set; }

    // Contact identity-card fields inlined from the removed shared Contact
    // directory (supersedes SIMF-FDS-014 / D-260). All nullable. Website is not
    // re-added here — the existing <see cref="Url"/> above is reused as the
    // website slot.
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

    /// <summary>Optional country of the partner. Logical + physical same-DB FK
    /// to <see cref="Country"/> (App context). Null until set.</summary>
    public int? CountryId { get; set; }

    /// <summary>Navigation for <see cref="CountryId"/> (same FK).</summary>
    public Country? Country { get; set; }
}
