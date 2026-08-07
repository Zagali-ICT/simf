using SIMF.Domain.Common;

namespace SIMF.Domain.Exhibitors;

/// <summary>
/// One login account provisioned under an <see cref="Exhibitor"/>. The account
/// itself is created through the ordinary admin provisioning pipeline and
/// carries the exhibitor profile type; this row ties it to its company.
///
/// <para>The row is half of the authorisation, not merely a label. The profile
/// type lives on the person and outlives the booth, so lead capture requires an
/// active membership of an active exhibitor as well as the role. Deactivating
/// this row, or the exhibitor, is what revokes an officer's badge scanning and
/// their access to the booth's captured contacts.</para>
/// </summary>
public sealed class ExhibitorMembership : BaseAuditEntity
{
    public Guid ExhibitorId { get; set; }
    public Exhibitor? Exhibitor { get; set; }

    /// <summary>A bare Guid: the user lives in the Identity database, so there is
    /// no navigation and no foreign key across the two.</summary>
    public Guid UserId { get; set; }

    /// <summary>The contact person's name on this account.</summary>
    public string ContactName { get; set; } = string.Empty;

    /// <summary>A free-text role inside the company, such as "Booth
    /// Manager".</summary>
    public string? RoleLabel { get; set; }
}
