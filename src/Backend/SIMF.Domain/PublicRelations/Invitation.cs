using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.PublicRelations;

/// <summary>
/// D-168 (gap doc G5, PDF §2.7.3) — a public-relations team invitation.
/// One row per (recipient, send) — the PR team can send a new invitation
/// to the same recipient again (the old row stays for audit). Recipient
/// is identified by their <c>UserProfile</c> id (App-side; real FK).
/// Sender is identified by the PR rep's <c>SimfUser</c> id (Identity-side;
/// logical FK because cross-database FKs are not supported by SQL Server).
/// </summary>
public sealed class Invitation : BaseAuditEntity
{
    /// <summary>The PR rep / admin who created the invitation. Logical FK
    /// to <c>SimfUser.Id</c> on the Identity DB — enforced at write time
    /// by the service layer.</summary>
    public Guid SentByUserId { get; set; }

    /// <summary>The recipient's profile. Real FK to
    /// <see cref="SIMF.Domain.Profiles.UserProfile.Id"/> on the App DB
    /// (D-167 moved UserProfile to App so a real FK is possible).</summary>
    public Guid SentToUserProfileId { get; set; }

    /// <summary>Lifecycle state. Pending on create; updated when the
    /// recipient confirms or declines, or when an admin overrides the
    /// state from the CP.</summary>
    public InvitationState State { get; set; } = InvitationState.Pending;

    /// <summary>Free-text note from the PR rep (purpose, talking points,
    /// dietary notes, …). Up to 1000 characters; null when empty.</summary>
    public string? Notes { get; set; }

    /// <summary>When the recipient or admin last moved
    /// <see cref="State"/> off <see cref="InvitationState.Pending"/>.
    /// Null while still pending.</summary>
    public DateTimeOffset? RespondedAt { get; set; }
}
