using SIMF.Domain.Common;

namespace SIMF.Domain.Contacts;

/// <summary>
/// A contact one visitor saved after resolving another's share token. Both user
/// references are bare Guids: the users live in the Identity database, so there
/// is no navigation, no foreign key and no cross-database join.
///
/// <para>None of the subject's own details are copied here. The card is resolved
/// on read from their profile, so a saved contact never goes stale against the
/// live record, and a subject who is later deactivated shows as unavailable
/// rather than as a snapshot that no longer holds. Saving is idempotent per
/// owner and subject, and removal is a soft-delete.</para>
/// </summary>
public sealed class SavedContact : BaseAuditEntity
{
    /// <summary>The visitor who saved the contact.</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>The visitor who was saved.</summary>
    public Guid SubjectUserId { get; set; }

    /// <summary>A free-text note the owner attached.</summary>
    public string? Note { get; set; }
}
