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
    DateTimeOffset CreatedAt);

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
    DateTimeOffset? UpdatedAt);

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
}

/// <summary>Update payload (adds IsActive to the create shape).</summary>
public sealed class AdminUpdateSponsorRequest
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int Tier { get; set; }
    public string? LogoRelativePath { get; set; }
    public string? Url { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
