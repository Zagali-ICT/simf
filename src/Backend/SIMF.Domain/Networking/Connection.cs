using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Networking;

/// <summary>
/// A visitor-to-visitor connection, held as one directed row: the requester asked, and
/// the target either accepts or the row is removed. Both user ids are bare Guids into
/// the Identity database, so the service validates them at write time because the
/// database cannot.
/// </summary>
public sealed class Connection : BaseAuditEntity
{
    public Guid RequesterUserId { get; set; }

    public Guid TargetUserId { get; set; }

    public ConnectionState State { get; set; } = ConnectionState.Pending;

    public DateTime? RespondedAt { get; set; }

    /// <summary>The same two users in sorted order, backing the filtered unique index that enforces one active connection per unordered pair. No writer fills the pair yet, so every row is filtered out and the index enforces nothing today.</summary>
    public Guid? PairLowUserId { get; set; }

    public Guid? PairHighUserId { get; set; }
}
