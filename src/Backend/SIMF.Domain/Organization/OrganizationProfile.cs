using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Organization;

/// <summary>
/// The forum's one "project / about" record: a single row, edited in place from
/// the Control Panel. It is edition-generic, so the app and the website read the
/// forum's name, year, dates, status, location, contact and social links from
/// here and the same build serves a past or a future edition by editing data
/// rather than code.
///
/// <para>The constructor stamps <see cref="SingletonId"/> on every instance, so
/// the primary key is what holds the table to one row. The logo is deliberately
/// not a column here: it lives in the file store under that same id.</para>
///
/// <para><see cref="BaseAuditEntity.UpdatedAt"/> is the public read's
/// <c>Last-Modified</c> token, which the app's cached copy revalidates against
/// for a 304. An admin save is one full-document upsert, so editing nothing but
/// a child row still stamps it and the app still refreshes.</para>
/// </summary>
public sealed class OrganizationProfile : BaseAuditEntity
{
    /// <summary>The fixed singleton id. The literal lives in
    /// <see cref="SIMF.Common.OrganizationProfileIds"/> so the Control Panel — which
    /// cannot reference the domain — addresses the same row rather than keeping a
    /// second copy of the Guid that nothing would notice drifting.</summary>
    public static readonly Guid SingletonId = SIMF.Common.OrganizationProfileIds.Singleton;

    public OrganizationProfile()
    {
        Id = SingletonId;
    }

    // Name and Title are required, the rest optional; Title is the sub-heading
    // shown under the name, not a page title. Each field pairs with its Arabic twin.

    public string Name { get; set; } = string.Empty;

    public string NameArabic { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string TitleArabic { get; set; } = string.Empty;

    public string? Slogan { get; set; }

    public string? SloganArabic { get; set; }

    public string? Bio { get; set; }

    public string? BioArabic { get; set; }

    // Free-text labels for the About screens. Only Version reaches a screen: the
    // app's About page shows it when set and drops the row when it is blank.
    // SysVersion, VersionDate and ReleaseDate are editable in the Control Panel and
    // ride the public payload, but nothing renders them.

    public string? Version { get; set; }

    public DateTime? VersionDate { get; set; }

    public string? SysVersion { get; set; }

    public DateTime? ReleaseDate { get; set; }

    // The current edition. The two dates carry a calendar date only; readers take
    // the YYYY-MM-DD head, so the time part means nothing. Status is set by hand and
    // never derived from those dates, so an edition whose end date has passed still
    // reads Open until an admin archives it.

    public DateTime? EventStartDate { get; set; }

    public DateTime? EventEndDate { get; set; }

    public int CurrentYear { get; set; }

    public ForumStatus Status { get; set; } = ForumStatus.Open;

    // The venue. The coordinates are optional decimal degrees, refused on write
    // outside ±90 / ±180 and stored to six places; they ride the public payload but
    // no screen plots them today.

    public string? LocationText { get; set; }

    public string? LocationTextArabic { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    // There is exactly one website. Every URL column from here down is refused on
    // write unless it is an absolute http(s) URL, and the social links are sanitised
    // again on the public read, so a stored value is safe to render as a link.

    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }

    public string? ContactWebsite { get; set; }

    // Both are links an admin types; the hero-video upload simply points
    // BackgroundVideoUrl at the file it stored.

    /// <summary>The forum-wide live feed, distinct from the per-session
    /// <c>Session.LiveStreamFileId</c>. The app's live screen prefers a session's
    /// own feed and falls back to this one; with neither set it shows an empty
    /// state. A <c>StoredFiles</c> row, as every media reference is.</summary>
    public Guid? LiveStreamFileId { get; set; }

    /// <summary>The muted, looping video behind the home hero, as a
    /// <c>StoredFiles</c> row. The website plays a YouTube link or a direct
    /// MP4/HLS link; the app plays only a direct link and falls through to its
    /// banner carousel on a YouTube one. Null, or a link the hero code will not
    /// take, leaves the bundled hero media in place.
    ///
    /// <para>One column for both authoring paths. An upload streams bytes to the
    /// store and points here; a pasted link becomes an external-link row and
    /// points here too. Before this there were two: an upload wrote a served URL
    /// into the same free-text box an admin could paste into, and the only thing
    /// telling them apart was a full-string comparison on the server against a
    /// suffix test in the Control Panel - two predicates for one question, which
    /// disagreed whenever the configured base URL changed.</para></summary>
    public Guid? BackgroundVideoFileId { get; set; }

    // Seven fixed columns rather than a child table: the shipped SocialLinks wire
    // contract names each network, so adding one costs a column, a contract field
    // and an app release.

    public string? FacebookUrl { get; set; }
    public string? XUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? YouTubeUrl { get; set; }
    public string? TikTokUrl { get; set; }
    public string? SnapchatUrl { get; set; }

    // Blank in either language falls back to the in-code default message. These
    // two, and PartnerDirectoryEnabled below, are the odd ones out: the CP Site
    // Settings page owns them and the site-settings payload serves them, so the
    // Organization Profile save never writes them.

    public string? RegistrationSuccessMessage { get; set; }

    public string? RegistrationSuccessMessageArabic { get; set; }

    // Read off this row on each call, so a flip takes effect with no restart.

    /// <summary>The app's "Meet People Like You" partner directory. Off makes the
    /// directory endpoint return an empty list and the app hides its entry point.
    /// Fail-open: a missing profile row reads as enabled.</summary>
    public bool PartnerDirectoryEnabled { get; set; } = true;

    // The two variable-length lists, both child tables cascade-deleted with the
    // profile and both read in DisplayOrder: free-form "about" cards (mission,
    // vision) and "name : value" detail rows (year, dates, location). Anything
    // editorial that changes from edition to edition belongs here, not in a column.

    public ICollection<OrganizationAboutItem> AboutItems { get; set; } =
        new List<OrganizationAboutItem>();

    public ICollection<OrganizationDetail> Details { get; set; } =
        new List<OrganizationDetail>();
}
