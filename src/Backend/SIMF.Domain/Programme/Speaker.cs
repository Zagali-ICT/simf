namespace SIMF.Domain.Programme;

/// <summary>
/// One programme speaker (SIMF-DAT-001 §5.4). Basic shape as applied by
/// the <c>AddSpeakers</c> migration. The enhancement migration (Phase C of
/// the D-151 work) adds CountryId FK, UserProfileId logical FK, Arabic
/// counterparts, consent toggles and social URLs.
/// </summary>
public class Speaker
{
    public Guid Id { get; set; }
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
    public string? PhotoRelativePath { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
