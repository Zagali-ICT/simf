using SIMF.Domain.Common;

namespace SIMF.Domain.Contacts;

/// <summary>
/// A contact one visitor saved after resolving another's share token. Both user ids are
/// bare Guids into the Identity database, so there is no navigation and no cross-database
/// join. None of the subject's details are copied: the card is projected live from their
/// profile on read, so a saved contact never goes stale.
/// </summary>
public sealed class SavedContact : BaseAuditEntity
{
    public Guid OwnerUserId { get; set; }

    public Guid SubjectUserId { get; set; }

    public string? Note { get; set; }
}
