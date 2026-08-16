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

    /// <summary>Optional per-booth override for the contact person's name. Blank means
    /// "use the account's own name", which readers resolve from the Identity database,
    /// so this column never holds a second copy of a fact that already lives there.
    /// It stays non-nullable because the EF mapping still declares it required; the
    /// blank string, not null, is what "no override" looks like on the row.</summary>
    public string ContactName { get; set; } = string.Empty;

    /// <summary>Free-text role inside the company, such as "Booth Manager".</summary>
    public string? RoleLabel { get; set; }
}
