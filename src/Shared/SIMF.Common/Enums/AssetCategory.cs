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
}
