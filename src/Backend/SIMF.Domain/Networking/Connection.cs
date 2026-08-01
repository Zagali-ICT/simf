using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Networking;

/// <summary>
/// B6 — D-224 (PDF networking / FDS-008): a visitor-to-visitor connection.
/// One directed row — <see cref="RequesterUserId"/> asked to connect with
/// <see cref="TargetUserId"/>; the target accepts or it is removed. Both user
/// references are LOGICAL FKs to <c>SimfUser.Id</c> on the Identity DB (no SQL
/// constraint — cross-DB, D-157), enforced at write time. Soft-deleted via
/// <see cref="IsActive"/> (decline / remove). App-only: there is no admin/CP
/// surface, so it carries no permission code (matches MeetPeopleLikeYou).
/// </summary>
public sealed class Connection : BaseAuditEntity
{
    /// <summary>The user who sent the request. Logical FK to SimfUser.Id.</summary>
    public Guid RequesterUserId { get; set; }

    /// <summary>The user who received the request. Logical FK to SimfUser.Id.</summary>
    public Guid TargetUserId { get; set; }

    /// <summary>Lifecycle state — Pending → Accepted / Declined.</summary>
    public ConnectionState State { get; set; } = ConnectionState.Pending;

    /// <summary>When the target accepted / declined; null while Pending.</summary>
    public DateTime? RespondedAt { get; set; }
}
