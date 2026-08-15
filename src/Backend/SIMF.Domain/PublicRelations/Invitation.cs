using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.PublicRelations;

/// <summary>An invitation from the public-relations team. One row per recipient and send,
/// so re-inviting the same person leaves the earlier row for the audit trail.</summary>
public sealed class Invitation : BaseAuditEntity
{
    /// <summary>The rep who created it. Bare Guid: the user lives in the Identity
    /// database, so the service checks it at write time because the database cannot.</summary>
    public Guid SentByUserId { get; set; }

    public Guid SentToUserProfileId { get; set; }

    /// <summary>Moved by the recipient, or overridden by an admin from the Control Panel.</summary>
    public InvitationState State { get; set; } = InvitationState.Pending;

    public string? Notes { get; set; }

    /// <summary>When <see cref="State"/> last moved off Pending.</summary>
    public DateTime? RespondedAt { get; set; }
}
