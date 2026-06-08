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
    string? CoverImageRelativePath,
    // D-347 — place + date label carried on the list too, so the Website's
    // per-year archive page renders the full mockup detail without a second
    // round-trip. Appended with defaults → existing positional callers/wire
    // (and the mobile decoder, which reads by name) are unaffected.
    string? LocationEn = null,
    string? LocationAr = null,
    string? DateLabelEn = null,
    string? DateLabelAr = null);

/// <summary>§9 (Mockup screen 24-01 "تفاصيل النسخة") — public detail for ONE
/// past edition: title/summary + place + date label + counters + cover. The
/// rich lists (gallery, session titles, past speakers, sponsors) are deferred
/// (ArchiveEdition entity TODO). Served by <c>GET /api/v1/app/archive/{id}</c>;
/// gated by the archive-visibility operations toggle (D-166) — 404 when the
/// archive is hidden or the edition is missing / inactive.</summary>
public sealed record PublicArchiveEditionDetail(
    Guid Id,
    int Year,
    string TitleEn,
    string TitleAr,
    string? SummaryEn,
    string? SummaryAr,
    string? LocationEn,
    string? LocationAr,
    string? DateLabelEn,
    string? DateLabelAr,
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
    DateTimeOffset CreatedAt,
    // §9 (screen 24-01) — place + date label, so the CP edit form (which
    // populates straight from the grid row) carries them. Default null
    // preserves existing positional callers.
    string? LocationEn = null,
    string? LocationAr = null,
    string? DateLabelEn = null,
    string? DateLabelAr = null);

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
    DateTimeOffset? UpdatedAt,
    // §9 (screen 24-01) — place + date label (default null preserves callers).
    string? LocationEn = null,
    string? LocationAr = null,
    string? DateLabelEn = null,
    string? DateLabelAr = null);

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
    // §9 (screen 24-01) — place + date label for the edition detail.
    public string? LocationEn { get; set; }
    public string? LocationAr { get; set; }
    public string? DateLabelEn { get; set; }
    public string? DateLabelAr { get; set; }
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
    // §9 (screen 24-01) — place + date label for the edition detail.
    public string? LocationEn { get; set; }
    public string? LocationAr { get; set; }
    public string? DateLabelEn { get; set; }
    public string? DateLabelAr { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>D-275 (§9) — "make this year history": snapshot the current live
/// event into a new ArchiveEdition. Year + bilingual title are generated
/// server-side (current UTC year, "SIMF {year}" / "سيمف {year}") and the three
/// counters (attendees = distinct gate-scan arrivals, sessions, speakers) are
/// computed from live data — none are client-supplied. The only input is whether
/// to reveal the archive immediately.</summary>
public sealed record SnapshotCurrentEditionRequest
{
    /// <summary>When true, flip the archive-visibility toggle (D-166) on after
    /// creating the snapshot so the new edition shows on the public Archive.</summary>
    public bool MakeVisible { get; set; }
}
