namespace SIMF.Contracts.Configuration;

/// <summary>D-461 — the public, CP-editable site/app branding settings served by
/// <c>GET /api/v1/app/site-settings</c>: the registration welcome message
/// (bilingual) and the social-media links. Absent settings fall back to the
/// in-code defaults; social links are null when not configured (the client keeps
/// the control inert, D-369).</summary>
public sealed record SiteSettingsResponse(
    string RegistrationSuccessMessageAr,
    string RegistrationSuccessMessageEn,
    SiteSocialLinks Social);

/// <summary>The configurable social-media URLs (null = not set).</summary>
public sealed record SiteSocialLinks(
    string? Facebook,
    string? X,
    string? Instagram,
    string? LinkedIn,
    string? YouTube,
    string? TikTok,
    string? Snapchat);
