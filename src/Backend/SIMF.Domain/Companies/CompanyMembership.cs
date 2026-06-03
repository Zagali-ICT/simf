using SIMF.Domain.Common;
using System.ComponentModel.Design;

namespace SIMF.Domain.Companies;

/// <summary>
/// D-199 #3 (additive schema) — one login account provisioned under a
/// <see cref="Company"/>. <see cref="UserId"/> is a <b>logical</b> FK to
/// <c>SimfUser.Id</c> on the Identity database (decision D-157 keeps the two
/// physical databases separate, so there is NO navigation property and NO
/// DB-level FK constraint across the database boundary — the link is by Guid
/// only). The account itself is provisioned through the existing admin
/// provisioning pipeline (a least-privilege Visitor account); this row tags it
/// to its company. Soft-deleted via <see cref="IsActive"/>.
/// </summary>
public sealed class CompanyMembership : BaseAuditEntity
{

    /// <summary>FK to <see cref="Company.Id"/> on the App database.</summary>
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }

    /// <summary>Logical FK to <c>SimfUser.Id</c> on the Identity database.
    /// No navigation property, no DB-level FK constraint (cross-database).</summary>
    public Guid UserId { get; set; }

    /// <summary>The contact person's name on this account (1–256 chars).</summary>
    public string ContactName { get; set; } = string.Empty;

    /// <summary>Optional free-text role label inside the company
    /// (e.g. "Booth Manager") (≤128 chars).</summary>
    public string? RoleLabel { get; set; }

    
}
