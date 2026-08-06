using SIMF.Common.Enums;

namespace SIMF.Contracts.Exhibitors;

/// <summary>
/// Admin grid row for an exhibitor.
/// </summary>
public sealed record AdminExhibitorSummary(
    Guid Id,
    string NameEn,
    string NameAr,
    string? ContactEmail,
    string? ContactPhone,
    string? Website,
    int AccountCount,
    bool IsActive,
    DateTime CreatedAt,
    // Carried so the grid Excel export can round-trip the tier (the grid
    // does not render it as a column). Optional; null = no tier.
    ExhibitorTier? Tier = null,
    // The exhibitor owns its own ExhibitorLogo (owner = the exhibitor) — true when
    // it has an active ExhibitorLogo asset, so the grid renders its logo thumbnail
    // (else an initials tile). Appended trailing-optional (wire-safe).
    bool HasExhibitorLogo = false);

/// <summary>Full admin detail for one exhibitor.</summary>
public sealed record AdminExhibitorDetail(
    Guid Id,
    string NameEn,
    string NameAr,
    string? ContactEmail,
    string? ContactPhone,
    string? Website,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // Optional exhibitor tier; null renders no pill.
    ExhibitorTier? Tier = null,
    // Contact identity-card fields inlined from the removed shared Contact
    // directory. All optional; the email + primary phone reuse the
    // existing ContactEmail / ContactPhone above (no second slot). Trailing-
    // optional so the wire contract stays append-only.
    int? CountryId = null,
    string? CountryNameEn = null,
    string? CountryNameAr = null,
    string? PhoneSecondary = null,
    string? FacebookUrl = null,
    string? XUrl = null,
    string? LinkedInUrl = null,
    string? InstagramUrl = null,
    string? City = null,
    string? CityArabic = null,
    double? Latitude = null,
    double? Longitude = null);

/// <summary>
/// Body of <c>POST /api/v1/admin/exhibitors</c>. Creates the exhibitor
/// shell (name); accounts are provisioned afterwards via
/// <c>POST /api/v1/admin/exhibitors/{id}/accounts</c>.
/// </summary>
public sealed class CreateExhibitorRequest
{
    /// <summary>English display name (1–256 chars).</summary>
    public string NameEn { get; init; } = string.Empty;

    /// <summary>Arabic display name (1–256 chars).</summary>
    public string NameAr { get; init; } = string.Empty;

    /// <summary>Optional primary contact email (≤320 chars).</summary>
    public string? ContactEmail { get; init; }

    /// <summary>Optional primary contact phone (≤32 chars).</summary>
    public string? ContactPhone { get; init; }

    /// <summary>Optional website (≤512 chars).</summary>
    public string? Website { get; init; }

    /// <summary>Optional exhibitor tier (null = no tier).</summary>
    public ExhibitorTier? Tier { get; init; }

    // Contact identity-card fields inlined from the removed shared Contact
    // directory. All optional; the email + primary phone reuse the
    // existing ContactEmail / ContactPhone above (no second slot).
    /// <summary>Optional same-DB country FK (nationality).</summary>
    public int? CountryId { get; init; }

    /// <summary>Optional secondary contact phone (≤32 chars).</summary>
    public string? PhoneSecondary { get; init; }

    /// <summary>Optional Facebook profile URL (≤256 chars).</summary>
    public string? FacebookUrl { get; init; }

    /// <summary>Optional X (Twitter) profile URL (≤256 chars).</summary>
    public string? XUrl { get; init; }

    /// <summary>Optional LinkedIn profile URL (≤256 chars).</summary>
    public string? LinkedInUrl { get; init; }

    /// <summary>Optional Instagram profile URL (≤256 chars).</summary>
    public string? InstagramUrl { get; init; }

    /// <summary>Optional English city name (≤128 chars).</summary>
    public string? City { get; init; }

    /// <summary>Optional Arabic city name (≤128 chars).</summary>
    public string? CityArabic { get; init; }

    /// <summary>Optional map latitude.</summary>
    public double? Latitude { get; init; }

    /// <summary>Optional map longitude.</summary>
    public double? Longitude { get; init; }
}

/// <summary>Body of <c>PUT /api/v1/admin/exhibitors/{id}</c>.
/// Not sealed: the endpoint binds {id}+body via a derived route class.</summary>
public class UpdateExhibitorRequest
{
    /// <summary>English display name (1–256 chars).</summary>
    public string NameEn { get; init; } = string.Empty;

    /// <summary>Arabic display name (1–256 chars).</summary>
    public string NameAr { get; init; } = string.Empty;

    /// <summary>Optional primary contact email (≤320 chars).</summary>
    public string? ContactEmail { get; init; }

    /// <summary>Optional primary contact phone (≤32 chars).</summary>
    public string? ContactPhone { get; init; }

    /// <summary>Optional website (≤512 chars).</summary>
    public string? Website { get; init; }

    /// <summary>Optional exhibitor tier (null = no tier).</summary>
    public ExhibitorTier? Tier { get; init; }

    // Contact identity-card fields inlined from the removed shared Contact
    // directory. All optional; the email + primary phone reuse the
    // existing ContactEmail / ContactPhone above (no second slot).
    /// <summary>Optional same-DB country FK (nationality).</summary>
    public int? CountryId { get; init; }

    /// <summary>Optional secondary contact phone (≤32 chars).</summary>
    public string? PhoneSecondary { get; init; }

    /// <summary>Optional Facebook profile URL (≤256 chars).</summary>
    public string? FacebookUrl { get; init; }

    /// <summary>Optional X (Twitter) profile URL (≤256 chars).</summary>
    public string? XUrl { get; init; }

    /// <summary>Optional LinkedIn profile URL (≤256 chars).</summary>
    public string? LinkedInUrl { get; init; }

    /// <summary>Optional Instagram profile URL (≤256 chars).</summary>
    public string? InstagramUrl { get; init; }

    /// <summary>Optional English city name (≤128 chars).</summary>
    public string? City { get; init; }

    /// <summary>Optional Arabic city name (≤128 chars).</summary>
    public string? CityArabic { get; init; }

    /// <summary>Optional map latitude.</summary>
    public double? Latitude { get; init; }

    /// <summary>Optional map longitude.</summary>
    public double? Longitude { get; init; }

    /// <summary>Soft-delete / restore flag.</summary>
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// One provisioned login account under an exhibitor. <c>UserId</c> is
/// the SimfUser id on the Identity database (logical FK).
/// </summary>
public sealed record ExhibitorAccountSummary(
    Guid Id,
    Guid UserId,
    string ContactName,
    string Email,
    string? RoleLabel,
    bool IsActive,
    DateTime CreatedAt);

/// <summary>
/// Body of <c>POST /api/v1/admin/exhibitors/{id}/accounts</c>.
/// Provisions a least-privilege login account tagged to the exhibitor. The
/// account is created through the existing admin provisioning pipeline as a
/// partner-side account carrying the exhibitor profile type (so
/// the booth officer can actually use the lead-capture tools), and an
/// <c>ExhibitorMembership</c> row links it.
/// Not sealed: the endpoint binds {id}+body via a derived route class.
/// </summary>
public class ProvisionExhibitorAccountRequest
{
    /// <summary>The contact person's name; used as the account display name (1–256 chars).</summary>
    public string ContactName { get; init; } = string.Empty;

    /// <summary>The new account's email address; must not already be registered.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Optional free-text role label inside the exhibitor (≤128 chars).</summary>
    public string? RoleLabel { get; init; }
}

/// <summary>
/// Body of <c>POST /api/v1/admin/exhibitors/{id}/accounts/link</c>.
/// Attaches an <b>existing</b> account to the exhibitor by writing the missing
/// <c>ExhibitorMembership</c>. <c>ProvisionExhibitorAccountRequest</c> is the only
/// other writer of that row, so an exhibitor-typed account created through the
/// generic Others pipeline (<c>POST /admin/others</c>) or the Others walk-in desk
/// had no membership at all — and therefore 403 on badge scan and on My Visitors,
/// with no Control-Panel path to fix it (a current membership is half the
/// authorisation). This is that path.
/// Not sealed: the endpoint binds {id}+body via a derived route class.
/// </summary>
public class LinkExhibitorAccountRequest
{
    /// <summary>The existing account's login email (1–320 chars). Matched
    /// case-insensitively against the Identity database.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Optional contact name for the membership row (≤256 chars).
    /// Defaults to the account's display name, then to its email.</summary>
    public string? ContactName { get; init; }

    /// <summary>Optional free-text role label inside the exhibitor (≤128 chars).</summary>
    public string? RoleLabel { get; init; }
}
