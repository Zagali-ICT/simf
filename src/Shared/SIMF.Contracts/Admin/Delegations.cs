namespace SIMF.Contracts.Admin;

/// <summary>D-174 (gap doc G11, Mockup page 21) — admin grid row.</summary>
public sealed record AdminDelegationSummary(
    Guid Id,
    string Name,
    string NameArabic,
    int? CountryId,
    string? CountryName,
    string? CountryNameArabic,
    int MemberCount,
    bool IsPriority,
    bool IsInternational,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed class CreateDelegationRequest
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public int? CountryId { get; set; }
    public int MemberCount { get; set; }
    public bool IsPriority { get; set; }
    public bool IsInternational { get; set; }
    public int DisplayOrder { get; set; }
}

public class UpdateDelegationRequest
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public int? CountryId { get; set; }
    public int MemberCount { get; set; }
    public bool IsPriority { get; set; }
    public bool IsInternational { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
