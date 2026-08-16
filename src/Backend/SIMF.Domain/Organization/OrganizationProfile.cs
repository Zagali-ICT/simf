using SIMF.Common.Enums;
using SIMF.Domain.Common;
using SIMF.Domain.Files;

namespace SIMF.Domain.Organization;

/// <summary>The forum's single "project / about" row, edited in place from the Control
/// Panel and edition-generic, so one build serves any edition by editing data. The logo is
/// not a column: it lives in the file store under this row's id. UpdatedAt is the public
/// read's Last-Modified token.</summary>
public sealed class OrganizationProfile : BaseAuditEntity
{
    // In SIMF.Common so the Control Panel, which cannot reference the domain, hits the same row.
    public static readonly Guid SingletonId = SIMF.Common.OrganizationProfileIds.Singleton;

    public OrganizationProfile()
    {
        Id = SingletonId;
    }

    public string Name { get; set; } = string.Empty;

    public string NameArabic { get; set; } = string.Empty;

    // The sub-heading shown under the name, not a page title.
    public string Title { get; set; } = string.Empty;

    public string TitleArabic { get; set; } = string.Empty;

    public string? Slogan { get; set; }

    public string? SloganArabic { get; set; }

    public string? Bio { get; set; }

    public string? BioArabic { get; set; }

    // Version and SysVersion are both edited on the Control Panel's profile page;
    // VersionDate and ReleaseDate are carried on the admin and public contracts but no
    // screen writes or renders them. All four stay because the contracts are append-only.

    public string? Version { get; set; }

    public DateTime? VersionDate { get; set; }

    public string? SysVersion { get; set; }

    public DateTime? ReleaseDate { get; set; }

    // Date-only; Status is set by hand, never derived, so a finished edition still reads Open.

    public DateTime? EventStartDate { get; set; }

    public DateTime? EventEndDate { get; set; }

    public int CurrentYear { get; set; }

    public ForumStatus Status { get; set; } = ForumStatus.Open;

    // Coordinates are decimal degrees, refused on write outside ±90 / ±180 by the admin
    // save and by a check constraint behind it; nothing plots them yet.

    public string? LocationText { get; set; }

    public string? LocationTextArabic { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    // Every URL column below is refused on write unless absolute http(s), and sanitised again
    // on the public read, so a stored value is safe to render as a link.

    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }

    public string? ContactWebsite { get; set; }

    /// <summary>The organisation's logo, in the one file store.
    ///
    /// <para>Added last of the file pointers: this row already pointed at its
    /// live stream and its hero video, while the logo alone was reachable only
    /// through the store's polymorphic <c>OwnerEntityType</c>/<c>OwnerEntityId</c>
    /// pair - a bare Guid no foreign key can constrain. The two readers that
    /// wanted it (<c>OrganizationProfileReadService</c> and
    /// <c>OrganizationProfileAdminService</c>) each ran their own
    /// <c>AnyAsync</c> against <c>StoredFiles</c> to answer nothing more than
    /// "is there a logo".</para></summary>
    public Guid? LogoFileId { get; set; }

    public StoredFile? LogoFile { get; set; }
    /// <summary>The forum-wide fallback feed; the live screen prefers a session's own.</summary>
    public Guid? LiveStreamFileId { get; set; }

    public StoredFile? LiveStreamFile { get; set; }
    /// <summary>The muted, looping home-hero video. The website takes a YouTube or a direct
    /// MP4/HLS link; the app takes only a direct link, else falls through to its carousel.</summary>
    public Guid? BackgroundVideoFileId { get; set; }

    public StoredFile? BackgroundVideoFile { get; set; }
    // Fixed columns, not a child table: the shipped SocialLinks wire contract names each network.

    public string? FacebookUrl { get; set; }
    public string? XUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? YouTubeUrl { get; set; }
    public string? TikTokUrl { get; set; }
    public string? SnapchatUrl { get; set; }

    // These two and PartnerDirectoryEnabled are owned by the CP Site Settings page; the profile
    // save never writes them. Blank in either language falls back to the in-code default.

    public string? RegistrationSuccessMessage { get; set; }

    public string? RegistrationSuccessMessageArabic { get; set; }

    /// <summary>Off empties the directory endpoint; a missing row reads as enabled.</summary>
    public bool PartnerDirectoryEnabled { get; set; } = true;

    // Editorial content that varies by edition lives in these lists, read in DisplayOrder.

    public ICollection<OrganizationAboutItem> AboutItems { get; set; } =
        new List<OrganizationAboutItem>();

    public ICollection<OrganizationDetail> Details { get; set; } =
        new List<OrganizationDetail>();
}
