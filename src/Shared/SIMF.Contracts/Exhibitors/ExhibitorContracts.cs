using SIMF.Common.Enums;

namespace SIMF.Contracts.Exhibitors;

/// <summary>
/// D-199 #3 — admin grid row for an exhibitor.
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
    DateTimeOffset CreatedAt);

/// <summary>D-199 #3 — full admin detail for one exhibitor.</summary>
public sealed record AdminExhibitorDetail(
    Guid Id,
    string NameEn,
    string NameAr,
    string? ContactEmail,
    string? ContactPhone,
    string? Website,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid? ContactId,
    // Wave 3 (Figma 1439:11881) — optional exhibitor tier; null renders no pill.
    ExhibitorTier? Tier = null);

/// <summary>
/// D-199 #3 — body of <c>POST /api/v1/admin/exhibitors</c>. Creates the exhibitor
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

    /// <summary>SIMF-FDS-014 (D-281) — optional link to a shared <c>Contact</c>
    /// directory record. Must reference an existing active contact.</summary>
    public Guid? ContactId { get; init; }

    /// <summary>Optional exhibitor tier (null = no tier).</summary>
    public ExhibitorTier? Tier { get; init; }
}

/// <summary>D-199 #3 — body of <c>PUT /api/v1/admin/exhibitors/{id}</c>.
/// Not sealed: the endpoint binds {id}+body via a derived route class (D-202).</summary>
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

    /// <summary>SIMF-FDS-014 (D-281) — optional link to a shared <c>Contact</c>
    /// directory record. Must reference an existing active contact.</summary>
    public Guid? ContactId { get; init; }

    /// <summary>Optional exhibitor tier (null = no tier).</summary>
    public ExhibitorTier? Tier { get; init; }

    /// <summary>Soft-delete / restore flag.</summary>
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// D-199 #3 — one provisioned login account under an exhibitor. <c>UserId</c> is
/// the SimfUser id on the Identity database (logical FK).
/// </summary>
public sealed record ExhibitorAccountSummary(
    Guid Id,
    Guid UserId,
    string ContactName,
    string Email,
    string? RoleLabel,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>
/// D-199 #3 — body of <c>POST /api/v1/admin/exhibitors/{id}/accounts</c>.
/// Provisions a least-privilege login account tagged to the exhibitor. The
/// account is created through the existing admin provisioning pipeline
/// (a Visitor account) and an <c>ExhibitorMembership</c> row links it.
/// Not sealed: the endpoint binds {id}+body via a derived route class (D-202).
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
