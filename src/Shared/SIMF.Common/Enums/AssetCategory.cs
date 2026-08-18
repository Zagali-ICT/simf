namespace SIMF.Common.Enums;

/// <summary>Which entity family a unified media <c>Asset</c> belongs to.
/// One active asset exists per (category, owner). Persisted as an int;
/// append-only — never rename or reorder existing values: the enum-stability rule
/// still holds, even though the schema freeze was later lifted for new tables.</summary>
public enum AssetCategory
{
    SpeakerPhoto = 0,

    // 1 is reserved - used to be `CompanyLogo`. Its Contact owner table was
    // removed, so the category could never resolve; the integer stays empty so a
    // persisted value never changes meaning.

    MediaPartnerLogo = 2,
    SponsorLogo = 3,
    ArchiveCover = 4,
    NewsImage = 5,
    ProgrammeDayImage = 6,

    /// <summary>The Organization Profile logo (owner =
    /// <c>OrganizationProfile.SingletonId</c>).</summary>
    OrganizationLogo = 7,

    /// <summary>An uploaded image for a home <c>Banner</c> row (owner =
    /// <c>Banner.Id</c>). Backs the rotating home hero; served publicly only
    /// while the banner is active and within its display window.</summary>
    Banner = 8,

    /// <summary>An exhibition <c>Booth</c>'s own logo (owner = <c>Booth.Id</c>).
    /// A booth now owns its logo directly rather than borrowing the linked
    /// exhibitor's — shown on the app booth card. Public read.</summary>
    BoothLogo = 9,

    /// <summary>An <c>Exhibitor</c>'s own logo (owner = <c>Exhibitor.Id</c>),
    /// shown on the app exhibitor-detail screen. Public read.</summary>
    ExhibitorLogo = 10,

    /// <summary>A past edition's speaker photo
    /// (owner = <c>ArchivePastSpeaker.Id</c>). Public read.</summary>
    ArchivePastSpeakerPhoto = 11,

    /// <summary>A past edition's gallery photo
    /// (owner = <c>ArchiveMediaItem.Id</c>). Public read. A gallery VIDEO is not
    /// an asset category: SIMF does not host it, so it is an external-link file
    /// rather than something uploaded through this pipeline.</summary>
    ArchiveGalleryImage = 12,
}
