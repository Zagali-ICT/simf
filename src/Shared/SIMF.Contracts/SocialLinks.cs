namespace SIMF.Contracts;

/// <summary>
/// The fixed set of social-media URLs (null = not set / not a valid http(s) URL).
/// Shared by the public site-settings and organization-profile responses — one
/// wire shape, so both the mobile <c>social</c> objects decode identically.
/// </summary>
public sealed record SocialLinks(
    string? Facebook,
    string? X,
    string? Instagram,
    string? LinkedIn,
    string? YouTube,
    string? TikTok,
    string? Snapchat);
