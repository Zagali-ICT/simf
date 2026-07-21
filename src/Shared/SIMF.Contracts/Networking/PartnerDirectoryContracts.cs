namespace SIMF.Contracts.Networking;

/// <summary>Build #13 — "Meet People Like You" partner directory
/// (<c>GET /api/v1/app/networking/partner-directory</c>). A deduped, curated +
/// opt-in list: the exhibition Sponsors, Speakers and Booth companies, plus the
/// "Other"-type (non-visitor) user accounts that opted in via
/// <c>UserProfile.ShowInMeetLikeYou</c>. Normal / VIP visitors never appear.
/// Empty when the CP toggle <c>OrganizationProfile.PartnerDirectoryEnabled</c>
/// is off.</summary>
public sealed record PartnerDirectoryResponse(
    IReadOnlyList<PartnerDirectoryEntry> Entries);

/// <summary>Build #13 — one directory row. <see cref="Kind"/> is the
/// discriminator the app uses to route a tap and to pick the logo asset:
/// <c>speaker</c> → speaker profile, <c>sponsor</c> → sponsor detail,
/// <c>booth</c> → exhibitor detail, <c>person</c> → non-navigating card.
/// <see cref="Id"/> is the route id for that kind (speaker / sponsor / booth id,
/// or the opted-in account's user-profile id).
/// <para>Logos follow the existing projection convention — a relative path
/// (<see cref="LogoRelativePath"/>) or the owning contact id
/// (<see cref="LogoContactId"/>, for the central CompanyLogo store), never an
/// absolute URL; the client builds the asset URL per kind.</para></summary>
public sealed record PartnerDirectoryEntry(
    string Kind,
    Guid Id,
    string Name,
    string NameArabic,
    string? Subtitle,
    string? SubtitleArabic,
    string? LogoRelativePath,
    Guid? LogoContactId,
    int? CountryId,
    string? CountryNameEn,
    string? CountryNameAr);

/// <summary>Build #13 — the <see cref="PartnerDirectoryEntry.Kind"/> tokens (the
/// shipped wire values the app switches on).</summary>
public static class PartnerDirectoryKind
{
    public const string Speaker = "speaker";
    public const string Sponsor = "sponsor";
    public const string Booth = "booth";
    public const string Person = "person";
}
