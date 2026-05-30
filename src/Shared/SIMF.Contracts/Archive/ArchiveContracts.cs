namespace SIMF.Contracts.Archive;

/// <summary>D-199 — public Archive / Past Editions payload (Mockup screen 24).
/// Returned by GET /archive. When the archive-visibility operations toggle
/// (D-166) is off, <see cref="Items"/> is empty.</summary>
public sealed record PublicArchive(IReadOnlyList<PublicArchiveEdition> Items);

public sealed record PublicArchiveEdition(
    Guid Id,
    int Year,
    string TitleEn,
    string TitleAr,
    string? SummaryEn,
    string? SummaryAr,
    int Attendees,
    int Sessions,
    int Speakers,
    string? CoverImageRelativePath);

/// <summary>D-199 — admin Archive edition CRUD contracts. Lengths mirror the
/// EF configuration (<c>ArchiveEditionConfiguration</c>) and FluentValidation
/// validators.</summary>
public sealed record AdminArchiveEditionSummary(
    Guid Id,
    int Year,
    string TitleEn,
    string TitleAr,
    string? SummaryEn,
    string? SummaryAr,
    int Attendees,
    int Sessions,
    int Speakers,
    string? CoverImageRelativePath,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record AdminArchiveEditionDetail(
    Guid Id,
    int Year,
    string TitleEn,
    string TitleAr,
    string? SummaryEn,
    string? SummaryAr,
    int Attendees,
    int Sessions,
    int Speakers,
    string? CoverImageRelativePath,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateArchiveEditionRequest
{
    public int Year { get; set; }
    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string? SummaryEn { get; set; }
    public string? SummaryAr { get; set; }
    public int Attendees { get; set; }
    public int Sessions { get; set; }
    public int Speakers { get; set; }
    public string? CoverImageRelativePath { get; set; }
}

// Not sealed: an endpoint binds {id}+body via a derived route class (D-199).
public record UpdateArchiveEditionRequest
{
    public int Year { get; set; }
    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string? SummaryEn { get; set; }
    public string? SummaryAr { get; set; }
    public int Attendees { get; set; }
    public int Sessions { get; set; }
    public int Speakers { get; set; }
    public string? CoverImageRelativePath { get; set; }
    public bool IsActive { get; set; } = true;
}
