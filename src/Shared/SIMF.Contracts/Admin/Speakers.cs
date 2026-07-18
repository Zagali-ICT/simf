namespace SIMF.Contracts.Admin;

/// <summary>One row in the admin Speakers grid (D-151 — SIMF-DAT-001 §5.4).
/// D-153 — the country is carried as <c>CountryId</c> (ISO 3166-1 numeric,
/// FK to <c>Country.Id</c>) with the display names (<c>CountryNameEn</c> /
/// <c>CountryNameAr</c>) projected alongside so the grid renders the country
/// column without a second fetch.
/// <para>The redesigned grid additionally carries <c>CountryCode</c> (ISO
/// 3166-1 alpha-2, for the flag) and <c>HasPhoto</c> — a batched "an active
/// speaker-photo asset exists" flag so the grid renders the real thumbnail
/// only when one is present and falls back to an initials tile otherwise
/// (never a broken image).</para></summary>
public sealed record AdminSpeakerSummary(
    Guid Id,
    string Code,
    string Name,
    string NameArabic,
    string? Rank,
    string? RankArabic,
    int? CountryId,
    string? CountryNameEn,
    string? CountryNameAr,
    string? CountryCode,
    int DisplayOrder,
    bool IsActive,
    bool HasPhoto,
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
    string? RankArabic,
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
    string? WebsiteUrl,
    string? PhotoRelativePath,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid? ContactId);

public sealed class AdminCreateSpeakerRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? Rank { get; set; }
    public string? RankArabic { get; set; }
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
    public string? WebsiteUrl { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>SIMF-FDS-014 (D-281) — optional link to a shared <c>Contact</c>
    /// directory record. Must reference an existing active contact.</summary>
    public Guid? ContactId { get; set; }
}

public sealed class AdminUpdateSpeakerRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? Rank { get; set; }
    public string? RankArabic { get; set; }
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
    public string? WebsiteUrl { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>SIMF-FDS-014 (D-281) — optional link to a shared <c>Contact</c>
    /// directory record. Must reference an existing active contact.</summary>
    public Guid? ContactId { get; set; }

    public bool IsActive { get; set; } = true;
}
