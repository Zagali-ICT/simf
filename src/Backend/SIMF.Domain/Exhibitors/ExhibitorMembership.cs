using SIMF.Domain.Common;

namespace SIMF.Domain.Exhibitors;

/// <summary>
/// D-199 #3 (additive schema) — one login account provisioned under an
/// <see cref="Exhibitor"/>. <see cref="UserId"/> is a <b>logical</b> FK to
/// <c>SimfUser.Id</c> on the Identity database (decision D-157 keeps the two
/// physical databases separate, so there is NO navigation property and NO
/// DB-level FK constraint across the database boundary — the link is by Guid
/// only). The account itself is provisioned through the existing admin
/// provisioning pipeline as a partner-side account carrying the exhibitor
/// profile type (DEF-EXH-005 — the lead-capture tools authorise on
/// <c>ProfileType.MobileAppRole == Exhibitor</c>, D-519, so a type-less account
/// could never scan); this row tags it to its exhibitor. Soft-deleted via
/// <see cref="IsActive"/>.
/// </summary>
public sealed class ExhibitorMembership : BaseAuditEntity
{

    /// <summary>FK to <see cref="Exhibitor.Id"/> on the App database.</summary>
    public Guid ExhibitorId { get; set; }
    public Exhibitor? Exhibitor { get; set; }

    /// <summary>Logical FK to <c>SimfUser.Id</c> on the Identity database.
    /// No navigation property, no DB-level FK constraint (cross-database).</summary>
    public Guid UserId { get; set; }

    /// <summary>The contact person's name on this account (1–256 chars).</summary>
    public string ContactName { get; set; } = string.Empty;

    /// <summary>Optional free-text role label inside the exhibitor
    /// (e.g. "Booth Manager") (≤128 chars).</summary>
    public string? RoleLabel { get; set; }

}
