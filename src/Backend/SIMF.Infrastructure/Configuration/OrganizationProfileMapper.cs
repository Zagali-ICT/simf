using SIMF.Contracts;
using SIMF.Contracts.Organization;
using SIMF.Domain.Organization;

namespace SIMF.Infrastructure.Configuration;

/// <summary>D-495 — projects the singleton <see cref="OrganizationProfile"/> + its
/// active child lists to the public <see cref="OrganizationProfileResponse"/>. Shared
/// by the cached read service and the admin GET so the mapping lives in one place.
/// URLs are sanitised on read (D-467): only an absolute http(s) URL is surfaced.</summary>
internal static class OrganizationProfileMapper
{
    public static OrganizationProfileResponse ToResponse(
        OrganizationProfile p,
        IReadOnlyList<OrganizationAboutItem> about,
        IReadOnlyList<OrganizationDetail> details,
        string? logoUrl) =>
        new(
            p.Name,
            p.NameArabic,
            p.Title,
            p.TitleArabic,
            NullIfBlank(p.Slogan),
            NullIfBlank(p.SloganArabic),
            NullIfBlank(p.Bio),
            NullIfBlank(p.BioArabic),
            NullIfBlank(p.Version),
            p.VersionDate,
            NullIfBlank(p.SysVersion),
            p.ReleaseDate,
            p.EventStartDate,
            p.EventEndDate,
            p.CurrentYear,
            p.Status.ToString(),
            NullIfBlank(p.LocationText),
            NullIfBlank(p.LocationTextArabic),
            p.Latitude,
            p.Longitude,
            NullIfBlank(p.ContactPhone),
            NullIfBlank(p.ContactEmail),
            SafeUrl(p.ContactWebsite),
            SafeUrl(p.LiveStreamUrl),
            SafeUrl(p.BackgroundVideoUrl),
            new SocialLinks(
                SafeUrl(p.FacebookUrl),
                SafeUrl(p.XUrl),
                SafeUrl(p.InstagramUrl),
                SafeUrl(p.LinkedInUrl),
                SafeUrl(p.YouTubeUrl),
                SafeUrl(p.TikTokUrl),
                SafeUrl(p.SnapchatUrl)),
            logoUrl,
            about.Select(a => new OrganizationAboutItemDto(
                a.Id, a.Title, a.TitleArabic, a.Text, a.TextArabic, a.DisplayOrder)).ToList(),
            details.Select(d => new OrganizationDetailDto(
                d.Id, d.Name, d.NameArabic, d.Value, d.DisplayOrder)).ToList());

    private static string? NullIfBlank(string? v) =>
        string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    // D-467 — a URL is rendered as a link target (website footer / app launcher), so
    // only an absolute http(s) URL is surfaced; any other scheme drops to inert null.
    private static string? SafeUrl(string? v)
    {
        var t = NullIfBlank(v);
        return t is not null
            && Uri.TryCreate(t, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? t
            : null;
    }
}
