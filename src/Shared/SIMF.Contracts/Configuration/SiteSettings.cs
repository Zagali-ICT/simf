namespace SIMF.Contracts.Configuration;

/// <summary>D-461 — the public, CP-editable site/app branding settings served by
/// <c>GET /api/v1/app/site-settings</c>: the registration welcome message
/// (bilingual) and the social-media links. Absent settings fall back to the
/// in-code defaults; social links are null when not configured (the client keeps
/// the control inert, D-369).
/// <para>Build #13 — <see cref="PartnerDirectoryEnabled"/> is the CP switch for
/// the "Meet People Like You" partner directory (append-only field; defaults to
/// true / fail-open so an older payload keeps the feature on).</para></summary>
public sealed record SiteSettingsResponse(
    string RegistrationSuccessMessageAr,
    string RegistrationSuccessMessageEn,
    SocialLinks Social,
    bool PartnerDirectoryEnabled = true);
