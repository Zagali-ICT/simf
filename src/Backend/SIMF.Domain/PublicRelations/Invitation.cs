using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.PublicRelations;

/// <summary>
/// An invitation from the public-relations team. One row per recipient and send,
/// so the team can invite the same person again and the earlier row stays for the
/// audit trail.
/// </summary>
public sealed class Invitation : BaseAuditEntity
{
    /// <summary>The rep who created the invitation. A bare Guid, since the user
    /// lives in the Identity database; the service checks it at write time,
    /// because the database cannot.</summary>
    public Guid SentByUserId { get; set; }

    /// <summary>The recipient's profile. A real foreign key: profiles live in
    /// this database.</summary>
    public Guid SentToUserProfileId { get; set; }

    /// <summary>Pending on create, then moved when the recipient confirms or
    /// declines, or when an admin overrides it from the Control Panel.</summary>
    public InvitationState State { get; set; } = InvitationState.Pending;

    /// <summary>A free-text note from the rep — purpose, talking points, dietary
    /// requirements and so on.</summary>
    public string? Notes { get; set; }

    /// <summary>When <see cref="State"/> last moved off Pending; null while it
    /// still is.</summary>
    public DateTime? RespondedAt { get; set; }
}
