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

    /// <summary>When the target first answered, so it is stamped once and never moved:
    /// a later removal by either party does not rewrite the moment of the response.</summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>The same two users in sorted order, backing the filtered unique index that
    /// enforces one active connection per unordered pair. Filled on insert by
    /// NetworkingService.RequestAsync; both halves are null only on rows written before
    /// that, which the index filter excludes.</summary>
    public Guid? PairLowUserId { get; set; }

    public Guid? PairHighUserId { get; set; }
}
