using SIMF.Domain.Common;

namespace SIMF.Domain.Exhibitors;

/// <summary>One login account provisioned under an <see cref="Exhibitor"/>. Lead capture needs an
/// active membership of an active exhibitor as well as the profile type, so deactivating this row
/// revokes the officer's badge scanning and their access to the booth's captured contacts.</summary>
public sealed class ExhibitorMembership : BaseAuditEntity
{
    public Guid ExhibitorId { get; set; }
    public Exhibitor? Exhibitor { get; set; }

    /// <summary>Bare Guid: the user lives in the Identity database, so no foreign key.</summary>
    public Guid UserId { get; set; }

    public string ContactName { get; set; } = string.Empty;

    /// <summary>Free-text role inside the company, such as "Booth Manager".</summary>
    public string? RoleLabel { get; set; }
}
