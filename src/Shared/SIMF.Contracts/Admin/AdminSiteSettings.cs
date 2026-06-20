namespace SIMF.Contracts.Admin;

/// <summary>D-464 — the CP "Site Settings" page payload: the registration welcome
/// message (bilingual) + the social-media links. Each field maps to a
/// <c>SiteSettingKeys</c> key; the admin service upserts them. A blank value
/// clears the setting (the public read treats blank as null → inert link).</summary>
public sealed class AdminUpdateSiteSettingsRequest
{
    public string? RegistrationMessageAr { get; set; }
    public string? RegistrationMessageEn { get; set; }
    public string? Facebook { get; set; }
    public string? X { get; set; }
    public string? Instagram { get; set; }
    public string? LinkedIn { get; set; }
    public string? YouTube { get; set; }
    public string? TikTok { get; set; }
    public string? Snapchat { get; set; }
}
