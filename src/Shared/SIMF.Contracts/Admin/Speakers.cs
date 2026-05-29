namespace SIMF.Contracts.Admin;

/// <summary>One row in the admin Speakers grid (D-151 — SIMF-DAT-001 §5.4).
/// D-153 — <c>CountryCode</c> field is replaced by <c>CountryId</c>
/// (ISO 3166-1 numeric, FK to <c>Country.Id</c>); a <c>CountryName</c> is
/// projected alongside for display so the grid does not need a second
/// fetch to render the country column.</summary>
public sealed record AdminSpeakerSummary(
    Guid Id,
    string Code,
    string Name,
    string NameArabic,
    string? Rank,
    int? CountryId,
    string? CountryNameEn,
    string? CountryNameAr,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>Full speaker detail (Details + Edit modals). D-153 carries
/// the full bilingual rich-text set + consent toggles + social URLs +
/// the optional <c>UserProfileId</c> link to a SIMF account.</summary>
public sealed record AdminSpeakerDetail(
    Guid Id,
    string Code,
    string Name,
    string NameArabic,
    string? Rank,
    int? CountryId,
    string? CountryNameEn,
    string? CountryNameAr,
    Guid? UserProfileId,
    string? Bio,
    string? BioArabic,
    string? Qualifications,
    string? QualificationsArabic,
    string? TrainingExperience,
    string? TrainingExperienceArabic,
    string? Awards,
    string? AwardsArabic,
    bool AllowsMeetingRequests,
    bool AllowsDataSharing,
    string? FacebookUrl,
    string? LinkedInUrl,
    string? XUrl,
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
    public int? CountryId { get; set; }
    public Guid? UserProfileId { get; set; }
    public string? Bio { get; set; }
    public string? BioArabic { get; set; }
    public string? Qualifications { get; set; }
    public string? QualificationsArabic { get; set; }
    public string? TrainingExperience { get; set; }
    public string? TrainingExperienceArabic { get; set; }
    public string? Awards { get; set; }
    public string? AwardsArabic { get; set; }

    /// <summary>D-153 — default false; admin must opt-in per speaker.</summary>
    public bool AllowsMeetingRequests { get; set; }

    /// <summary>D-153 — default false; admin must opt-in per speaker.</summary>
    public bool AllowsDataSharing { get; set; }

    public string? FacebookUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? XUrl { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class AdminUpdateSpeakerRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? Rank { get; set; }
    public int? CountryId { get; set; }
    public Guid? UserProfileId { get; set; }
    public string? Bio { get; set; }
    public string? BioArabic { get; set; }
    public string? Qualifications { get; set; }
    public string? QualificationsArabic { get; set; }
    public string? TrainingExperience { get; set; }
    public string? TrainingExperienceArabic { get; set; }
    public string? Awards { get; set; }
    public string? AwardsArabic { get; set; }
    public bool AllowsMeetingRequests { get; set; }
    public bool AllowsDataSharing { get; set; }
    public string? FacebookUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? XUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
