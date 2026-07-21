namespace SIMF.Common.Enums;

/// <summary>D-357 — which entity family a unified media <c>Asset</c> belongs to.
/// One active asset exists per (category, owner). Persisted as an int;
/// append-only — never rename or reorder existing values (the D-110
/// enum-stability rule, which survived the D-199 freeze-lift for new tables).</summary>
public enum AssetCategory
{
    SpeakerPhoto = 0,
    CompanyLogo = 1,
    MediaPartnerLogo = 2,
    SponsorLogo = 3,
    ArchiveCover = 4,
    NewsImage = 5,
    ProgrammeDayImage = 6,

    /// <summary>D-495 — the Organization Profile logo (owner =
    /// <c>OrganizationProfile.SingletonId</c>).</summary>
    OrganizationLogo = 7,

    /// <summary>#43 — an uploaded image for a home <c>Banner</c> row (owner =
    /// <c>Banner.Id</c>). Backs the rotating home hero; served publicly only
    /// while the banner is active and within its display window.</summary>
    Banner = 8,
}
