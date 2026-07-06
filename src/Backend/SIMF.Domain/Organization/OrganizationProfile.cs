using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Organization;

/// <summary>
/// D-495 — the single, admin-editable "project / about" record for the forum.
/// One row, fixed <see cref="SingletonId"/>, updated in place by admins on the CP
/// Organization Profile page. It is the edition-generic source of truth: the app
/// + website read the forum's name, year, dates, status, location, contact, social
/// links, live-stream link and about/vision text from here, so the same build
/// serves previous and future editions by editing data, not code.
///
/// <para>The logo lives in the unified <c>Asset</c> table (category
/// <c>OrganizationLogo</c>, owner = <see cref="SingletonId"/>) — bytes stay out of
/// the row (D-357). Social links are 7 fixed columns (the shipped
/// <c>SocialLinks</c> wire contract); the single website is
/// <see cref="ContactWebsite"/>. The variable lists (<see cref="AboutItems"/>,
/// <see cref="Details"/>) are child tables.</para>
///
/// <para><see cref="BaseAuditEntity.UpdatedAt"/> drives the public read's
/// <c>Last-Modified</c> / <c>304</c> cache-revalidation (D-173); every admin write —
/// including a child mutation — touches it so the app refreshes.</para>
/// </summary>
public sealed class OrganizationProfile : BaseAuditEntity
{
    /// <summary>The one and only profile row's id.</summary>
    public static readonly Guid SingletonId =
        Guid.Parse("00000000-0000-0000-0000-000000000003");

    public OrganizationProfile()
    {
        Id = SingletonId;
    }

    // --- Identity (bilingual) ---

    /// <summary>English forum name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic forum name — paired with <see cref="Name"/>.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>English title / sub-heading shown under the name.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Arabic title — paired with <see cref="Title"/>.</summary>
    public string TitleArabic { get; set; } = string.Empty;

    /// <summary>English short tagline / slogan.</summary>
    public string? Slogan { get; set; }

    /// <summary>Arabic slogan — paired with <see cref="Slogan"/>.</summary>
    public string? SloganArabic { get; set; }

    /// <summary>English long-form biography / description.</summary>
    public string? Bio { get; set; }

    /// <summary>Arabic biography — paired with <see cref="Bio"/>.</summary>
    public string? BioArabic { get; set; }

    // --- Versioning ---

    /// <summary>Public product / app version (e.g. "1.0.0").</summary>
    public string? Version { get; set; }

    /// <summary>The date of <see cref="Version"/>.</summary>
    public DateTimeOffset? VersionDate { get; set; }

    /// <summary>System / build version.</summary>
    public string? SysVersion { get; set; }

    /// <summary>The general "date" of the edition / release.</summary>
    public DateTimeOffset? ReleaseDate { get; set; }

    // --- Edition state ---

    /// <summary>The forum's start date.</summary>
    public DateTimeOffset? EventStartDate { get; set; }

    /// <summary>The forum's end date.</summary>
    public DateTimeOffset? EventEndDate { get; set; }

    /// <summary>The active edition year (e.g. 2026).</summary>
    public int CurrentYear { get; set; }

    /// <summary>Whether the current edition is coming soon / open / archived.</summary>
    public ForumStatus Status { get; set; } = ForumStatus.Open;

    // --- Location ---

    /// <summary>English location / venue text.</summary>
    public string? LocationText { get; set; }

    /// <summary>Arabic location / venue text — paired with <see cref="LocationText"/>.</summary>
    public string? LocationTextArabic { get; set; }

    /// <summary>Venue latitude (decimal degrees, −90..90).</summary>
    public decimal? Latitude { get; set; }

    /// <summary>Venue longitude (decimal degrees, −180..180).</summary>
    public decimal? Longitude { get; set; }

    // --- Contact ---

    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }

    /// <summary>The single official website URL.</summary>
    public string? ContactWebsite { get; set; }

    // --- Media ---

    /// <summary>The main home-page live-stream link (a YouTube URL). Distinct from
    /// the per-session <c>Session.LiveStreamUrl</c> (D-349).</summary>
    public string? LiveStreamUrl { get; set; }

    // --- Social links (the fixed SocialLinks wire contract) ---

    public string? FacebookUrl { get; set; }
    public string? XUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? YouTubeUrl { get; set; }
    public string? TikTokUrl { get; set; }
    public string? SnapchatUrl { get; set; }

    // --- Registration welcome message (migrated from the SiteSettings keys) ---

    /// <summary>English registration-success message (migrated from
    /// <c>registration.successMessage.en</c>).</summary>
    public string? RegistrationSuccessMessage { get; set; }

    /// <summary>Arabic registration-success message (migrated from
    /// <c>registration.successMessage.ar</c>).</summary>
    public string? RegistrationSuccessMessageArabic { get; set; }

    // --- Variable lists (child tables) ---

    /// <summary>The ordered "about" items (title + text), e.g. mission / vision.</summary>
    public ICollection<OrganizationAboutItem> AboutItems { get; set; } =
        new List<OrganizationAboutItem>();

    /// <summary>The ordered "details" items (name + value), e.g. year / date / location.</summary>
    public ICollection<OrganizationDetail> Details { get; set; } =
        new List<OrganizationDetail>();
}
