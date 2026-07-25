namespace SIMF.Contracts.Admin;

/// <summary>D-199 (Mockup page 23) — one row in the admin Sponsors grid.
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
    DateTimeOffset CreatedAt,
    // D-502 — carried so the grid Excel export can round-trip them (not rendered
    // as grid columns). Optional; blank when unset.
    string? Tagline = null,
    string? TaglineArabic = null,
    string? About = null,
    string? AboutArabic = null,
    // D-740 — "an active SponsorLogo asset exists" so the grid renders the real
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
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    // D-432 — optional bilingual tagline (Figma 922:2824).
    string? Tagline = null,
    string? TaglineArabic = null,
    // Optional bilingual about paragraph (≤2048 chars each).
    string? About = null,
    string? AboutArabic = null);

/// <summary>Create payload for a sponsor. <c>Tier</c> is the int enum value
/// (10=Platinum, 20=Gold, 30=Silver, 40=Bronze).</summary>
public sealed class AdminCreateSponsorRequest
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int Tier { get; set; }
    public string? LogoRelativePath { get; set; }
    public string? Url { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>D-432 — optional bilingual tagline (≤256 chars each).</summary>
    public string? Tagline { get; set; }
    public string? TaglineArabic { get; set; }

    /// <summary>Optional bilingual about paragraph (≤2048 chars each).</summary>
    public string? About { get; set; }
    public string? AboutArabic { get; set; }
}

/// <summary>Update payload (adds IsActive to the create shape).
/// Not sealed: the admin update endpoint binds {id}+body via a derived route
/// class (D-504, mirroring <c>UpdateExhibitorRoute</c>) so it cannot drop a
/// field at bind time.</summary>
public class AdminUpdateSponsorRequest
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int Tier { get; set; }
    public string? LogoRelativePath { get; set; }
    public string? Url { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>D-432 — optional bilingual tagline (≤256 chars each).</summary>
    public string? Tagline { get; set; }
    public string? TaglineArabic { get; set; }

    /// <summary>Optional bilingual about paragraph (≤2048 chars each).</summary>
    public string? About { get; set; }
    public string? AboutArabic { get; set; }

    public bool IsActive { get; set; } = true;
}
