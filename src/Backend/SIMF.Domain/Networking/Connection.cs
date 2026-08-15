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

    // The same pair of users in a canonical order, so "at most one active
    // connection per unordered pair" can be a unique index instead of only a
    // check-then-insert in the service. A connection is directed -- the requester
    // asked -- so (Requester, Target) cannot be indexed for that rule: the mirror
    // row (Target, Requester) is a different key and slips straight through.
    //
    // <para>A writer sorts the two ids with <see cref="Guid.CompareTo(Guid)"/> and
    // puts the smaller in <see cref="PairLowUserId"/>. The ordering only has to be
    // consistent, not to match SQL Server's own uniqueidentifier collation, because
    // nothing but .NET ever writes these.</para>
    //
    // <para>Nullable, and the unique index is filtered to exclude nulls. No
    // writer fills them yet, so every row is excluded and the index enforces
    // nothing today -- the columns and the index are the half of the rule that
    // lives in the schema, waiting on the insert path to sort the two ids. Once
    // every writer fills them the pair should become required and the IS NOT
    // NULL leg of the filter should go.</para>
    public Guid? PairLowUserId { get; set; }

    public Guid? PairHighUserId { get; set; }
}
