namespace SIMF.Contracts.Admin;

/// <summary>One row in the admin Speakers grid (D-151 — SIMF-DAT-001 §5.4).</summary>
public sealed record AdminSpeakerSummary(
    Guid Id,
    string Code,
    string Name,
    string NameArabic,
    string? Rank,
    string? CountryCode,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>Full speaker detail (Details + Edit modals).</summary>
public sealed record AdminSpeakerDetail(
    Guid Id,
    string Code,
    string Name,
    string NameArabic,
    string? Rank,
    string? CountryCode,
    string? Bio,
    string? BioArabic,
    string? Qualifications,
    string? TrainingExperience,
    string? Awards,
    string? PhotoRelativePath,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed class AdminCreateSpeakerRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? Rank { get; set; }
    public string? CountryCode { get; set; }
    public string? Bio { get; set; }
    public string? BioArabic { get; set; }
    public string? Qualifications { get; set; }
    public string? TrainingExperience { get; set; }
    public string? Awards { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class AdminUpdateSpeakerRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? Rank { get; set; }
    public string? CountryCode { get; set; }
    public string? Bio { get; set; }
    public string? BioArabic { get; set; }
    public string? Qualifications { get; set; }
    public string? TrainingExperience { get; set; }
    public string? Awards { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
