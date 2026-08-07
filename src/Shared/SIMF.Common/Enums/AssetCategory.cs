namespace SIMF.Common.Enums;

/// <summary>Which entity family a unified media <c>Asset</c> belongs to.
/// One active asset exists per (category, owner). Persisted as an int;
/// append-only — never rename or reorder existing values: the enum-stability rule
/// still holds, even though the schema freeze was later lifted for new tables.</summary>
public enum AssetCategory
{
    SpeakerPhoto = 0,
    CompanyLogo = 1,
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
    /// independent of the linked <c>Contact</c>'s <see cref="CompanyLogo"/> — shown
    /// on the app exhibitor-detail screen. Public read.</summary>
    ExhibitorLogo = 10,
}
