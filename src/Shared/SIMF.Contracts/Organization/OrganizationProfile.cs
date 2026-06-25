namespace SIMF.Contracts.Organization;

/// <summary>D-495 — the public Organization / About profile served by
/// <c>GET /api/v1/app/organization-profile</c>: the edition-generic forum config
/// (name, title, slogan, bio, dates, status, location, contact, live-stream link,
/// social links, logo) plus the variable about-items + details lists. Anonymous /
/// pre-login read; only public branding fields are projected (never audit columns).
/// The <c>Last-Modified</c> revalidation token rides the HTTP header, not the body.</summary>
public sealed record OrganizationProfileResponse(
    string Name,
    string NameArabic,
    string Title,
    string TitleArabic,
    string? Slogan,
    string? SloganArabic,
    string? Bio,
    string? BioArabic,
    string? Version,
    DateTimeOffset? VersionDate,
    string? SysVersion,
    DateTimeOffset? ReleaseDate,
    DateTimeOffset? EventStartDate,
    DateTimeOffset? EventEndDate,
    int CurrentYear,
    string Status,
    string? LocationText,
    string? LocationTextArabic,
    decimal? Latitude,
    decimal? Longitude,
    string? ContactPhone,
    string? ContactEmail,
    string? ContactWebsite,
    string? LiveStreamUrl,
    OrganizationSocialLinks Social,
    string? LogoUrl,
    IReadOnlyList<OrganizationAboutItemDto> AboutItems,
    IReadOnlyList<OrganizationDetailDto> Details);

/// <summary>The fixed set of social-media URLs (null = not set / not an http(s) URL).</summary>
public sealed record OrganizationSocialLinks(
    string? Facebook,
    string? X,
    string? Instagram,
    string? LinkedIn,
    string? YouTube,
    string? TikTok,
    string? Snapchat);

/// <summary>One "about" item — a bilingual title + body (e.g. mission / vision).</summary>
public sealed record OrganizationAboutItemDto(
    Guid Id,
    string Title,
    string TitleArabic,
    string Text,
    string TextArabic,
    int DisplayOrder);

/// <summary>One "detail" row — a bilingual label + a value (e.g. year / date / location).</summary>
public sealed record OrganizationDetailDto(
    Guid Id,
    string Name,
    string NameArabic,
    string Value,
    int DisplayOrder);
