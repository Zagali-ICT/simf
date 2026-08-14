namespace SIMF.Contracts.Archive;

/// <summary>Public Archive / Past Editions payload.
/// Returned by GET /archive. When the archive-visibility operations toggle
/// is off, <see cref="Items"/> is empty.</summary>
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
    // Place + date label carried on the list too, so the Website's
    // per-year archive page renders the full detail without a second
    // round-trip. Appended with defaults → existing positional callers/wire
    // (and the mobile decoder, which reads by name) are unaffected.
    string? LocationEn = null,
    string? LocationAr = null,
    string? DateLabelEn = null,
    string? DateLabelAr = null,
    // True when the edition has an active ArchiveCover asset in the file store.
    // CoverImageRelativePath above is retained for the shipped wire and is always
    // null; this is what a client should branch on before building
    // /content/assets/ArchiveCover/{id}/image. Appended with a default, so the
    // positional wire is unchanged.
    bool HasCoverAsset = false);

/// <summary>One public gallery item ("الصور والفيديو").
/// <c>Kind</c> is the <c>ArchiveMediaKind</c> int (0 image, 1 video).
/// For an image, <c>Url</c> is an absolute photo URL the app renders directly.</summary>
public sealed record PublicArchiveMediaItem(
    int Kind, string Url, string? CaptionEn, string? CaptionAr);

/// <summary>One public session title ("عناوين الجلسات").</summary>
public sealed record PublicArchiveSessionTitle(string TitleEn, string TitleAr);

/// <summary>One public past speaker ("المتحدثون السابقون").
/// <c>PhotoRelativePath</c>, when an absolute URL, is the photo the
/// app renders directly (else initials).</summary>
public sealed record PublicArchivePastSpeaker(
    string NameEn, string NameAr, string? PhotoRelativePath,
    // The past speaker's country (ISO 3166-1 numeric) for the app's
    // corner flag; null when unset. Appended (append-only wire).
    int? CountryId = null);

/// <summary>Public detail for ONE
/// past edition ("تفاصيل النسخة"): title/summary + place + date label + counters
/// + cover, plus the rich lists (gallery, session titles, past speakers). Served by
/// <c>GET /api/v1/app/archive/{id}</c>; gated by the archive-visibility
/// operations toggle — 404 when the archive is hidden or the edition is
/// missing / inactive.</summary>
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
    string? CoverImageRelativePath,
    // Appended (append-only wire); empty when the edition has none.
    IReadOnlyList<PublicArchiveMediaItem>? Gallery = null,
    IReadOnlyList<PublicArchiveSessionTitle>? SessionTitles = null,
    IReadOnlyList<PublicArchivePastSpeaker>? PastSpeakers = null);

/// <summary>Admin Archive edition CRUD contracts. Lengths mirror the
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
    DateTime CreatedAt,
    // True when an active ArchiveCover asset exists, so the grid renders
    // the cover thumbnail (SimfIdentityCell), else an initials tile.
    bool HasCover,
    // Place + date label, so the CP edit form (which
    // populates straight from the grid row) carries them. Default null
    // preserves existing positional callers.
    string? LocationEn = null,
    string? LocationAr = null,
    string? DateLabelEn = null,
    string? DateLabelAr = null,
    // True when the edition has an active ArchiveCover asset in the file store.
    // CoverImageRelativePath above is retained for the shipped wire and is always
    // null; this is what a client should branch on before building
    // /content/assets/ArchiveCover/{id}/image. Appended with a default, so the
    // positional wire is unchanged.
    bool HasCoverAsset = false);

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
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // Place + date label (default null preserves callers).
    string? LocationEn = null,
    string? LocationAr = null,
    string? DateLabelEn = null,
    string? DateLabelAr = null,
    // The editable child lists, so the CP edit form pre-populates them.
    IReadOnlyList<ArchiveMediaItemInput>? Gallery = null,
    IReadOnlyList<ArchiveSessionTitleInput>? SessionTitles = null,
    IReadOnlyList<ArchivePastSpeakerInput>? PastSpeakers = null);

/// <summary>An editable gallery item for the admin create/update
/// (replace-all). <c>Kind</c> is the <c>ArchiveMediaKind</c> int.</summary>
public sealed class ArchiveMediaItemInput
{
    public int Kind { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? CaptionEn { get; set; }
    public string? CaptionAr { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>An editable session title for the admin create/update.</summary>
public sealed class ArchiveSessionTitleInput
{
    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

/// <summary>An editable past speaker for the admin create/update.</summary>
public sealed class ArchivePastSpeakerInput
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? PhotoRelativePath { get; set; }
    // Optional country (ISO 3166-1 numeric) set via the CP editor.
    public int? CountryId { get; set; }
    public int DisplayOrder { get; set; }
}

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
    // Place + date label for the edition detail.
    public string? LocationEn { get; set; }
    public string? LocationAr { get; set; }
    public string? DateLabelEn { get; set; }
    public string? DateLabelAr { get; set; }
    // The rich child lists. Null = "no lists / leave as-is"; a non-null
    // list (even empty) replaces all rows. Lets a caller that doesn't author the
    // lists omit them without wiping existing rows on update.
    public List<ArchiveMediaItemInput>? Gallery { get; set; }
    public List<ArchiveSessionTitleInput>? SessionTitles { get; set; }
    public List<ArchivePastSpeakerInput>? PastSpeakers { get; set; }
}

// Not sealed: an endpoint binds {id}+body via a derived route class.
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
    // Place + date label for the edition detail.
    public string? LocationEn { get; set; }
    public string? LocationAr { get; set; }
    public string? DateLabelEn { get; set; }
    public string? DateLabelAr { get; set; }
    // The rich child lists. Null = leave existing rows untouched; a
    // non-null list (even empty) replaces all rows for this edition.
    public List<ArchiveMediaItemInput>? Gallery { get; set; }
    public List<ArchiveSessionTitleInput>? SessionTitles { get; set; }
    public List<ArchivePastSpeakerInput>? PastSpeakers { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>"Make this year history": snapshot the current live
/// event into a new ArchiveEdition. Year + bilingual title are generated
/// server-side (the current Saudi year from <c>SimfClock</c>, "SIMF {year}" /
/// "سيمف {year}") and the three
/// counters (attendees = distinct gate-scan arrivals, sessions, speakers) are
/// computed from live data — none are client-supplied. The only input is whether
/// to reveal the archive immediately.</summary>
public sealed record SnapshotCurrentEditionRequest
{
    /// <summary>When true, flip the archive-visibility toggle on after
    /// creating the snapshot so the new edition shows on the public Archive.</summary>
    public bool MakeVisible { get; set; }
}
