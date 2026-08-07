using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Networking;

/// <summary>
/// A visitor-to-visitor connection, held as one directed row: the requester
/// asked, and the target either accepts or the row is removed. Both user
/// references are bare Guids, since the users live in the Identity database, and
/// the service checks them at write time because the database cannot.
///
/// <para>An app-only feature with no Control Panel surface, so it carries no
/// permission code.</para>
/// </summary>
public sealed class Connection : BaseAuditEntity
{
    public Guid RequesterUserId { get; set; }

    public Guid TargetUserId { get; set; }

    public ConnectionState State { get; set; } = ConnectionState.Pending;

    /// <summary>When the target answered; null while still pending.</summary>
    public DateTime? RespondedAt { get; set; }
}
