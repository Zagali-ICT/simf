namespace SIMF.Common;

/// <summary>
/// The whitelisted <c>SystemSetting</c> keys exposed publicly through
/// the typed site-settings read-path (<c>GET /api/v1/app/site-settings</c>).
/// Only these keys are served publicly — the raw key/value store stays
/// admin-only. An admin edits them on the CP configuration page and the value
/// flows to the app + website. Keys that are absent fall back to the in-code
/// defaults below (so the API always returns a sensible value).
/// </summary>
public static class SiteSettingKeys
{
    // Requirement #6 — the registration-complete welcome message (bilingual),
    // shown on the app's registration-success screen.
    public const string RegistrationSuccessMessageAr = "registration.successMessage.ar";
    public const string RegistrationSuccessMessageEn = "registration.successMessage.en";

    // Requirement #7 — the public social-media links (same set as the main page).
    public const string SocialFacebook = "social.facebook";
    public const string SocialX = "social.x";
    public const string SocialInstagram = "social.instagram";
    public const string SocialLinkedIn = "social.linkedin";
    public const string SocialYouTube = "social.youtube";
    public const string SocialTikTok = "social.tiktok";
    public const string SocialSnapchat = "social.snapchat";

    /// <summary>Every key the public read-path resolves — so the service can
    /// fetch them in one query and the CP page can enumerate the editable set.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        RegistrationSuccessMessageAr,
        RegistrationSuccessMessageEn,
        SocialFacebook,
        SocialX,
        SocialInstagram,
        SocialLinkedIn,
        SocialYouTube,
        SocialTikTok,
        SocialSnapchat,
    ];

    /// <summary>The default registration welcome message until an admin
    /// overrides it on the CP configuration page (owner-supplied wording).</summary>
    public const string DefaultRegistrationMessageAr =
        "تهانينا، مرحباً بكم في الملتقى السعودي الرابع.";
    public const string DefaultRegistrationMessageEn =
        "Congratulations, welcome to the Fourth Saudi Forum.";
}
