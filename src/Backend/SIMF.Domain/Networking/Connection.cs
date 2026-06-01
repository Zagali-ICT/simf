using SIMF.Common.Enums;

namespace SIMF.Domain.Networking;

/// <summary>
/// B6 — D-224 (PDF networking / FDS-008): a visitor-to-visitor connection.
/// One directed row — <see cref="RequesterUserId"/> asked to connect with
/// <see cref="TargetUserId"/>; the target accepts or it is removed. Both user
/// references are LOGICAL FKs to <c>SimfUser.Id</c> on the Identity DB (no SQL
/// constraint — cross-DB, D-157), enforced at write time. Soft-deleted via
/// <see cref="IsActive"/> (decline / remove). App-only: there is no admin/CP
/// surface, so it carries no permission code (matches SessionComment submit /
/// MeetPeopleLikeYou).
/// </summary>
public sealed class Connection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who sent the request. Logical FK to SimfUser.Id.</summary>
    public Guid RequesterUserId { get; set; }

    /// <summary>The user who received the request. Logical FK to SimfUser.Id.</summary>
    public Guid TargetUserId { get; set; }

    /// <summary>Lifecycle state — Pending → Accepted / Declined.</summary>
    public ConnectionState State { get; set; } = ConnectionState.Pending;

    /// <summary>When the request was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the target accepted / declined; null while Pending.</summary>
    public DateTimeOffset? RespondedAt { get; set; }

    /// <summary>Soft-delete flag — a removed / declined connection is excluded
    /// from every list but the row is retained.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Soft-delete transition (CLAUDE.md §7).</summary>
    public void Deactivate() => IsActive = false;
}
