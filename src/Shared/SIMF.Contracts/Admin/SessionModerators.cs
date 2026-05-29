namespace SIMF.Contracts.Admin;

/// <summary>D-169 (gap doc G6) — admin: assign a moderator to a
/// specific session. Composite-key (SessionId, UserId).</summary>
public sealed class AssignSessionModeratorRequest
{
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
}

/// <summary>D-169 — one row in the admin "session moderators" list.
/// Projects the session code/title + the moderator's display name +
/// email so the grid does not need cross-DB lookups per row.</summary>
public sealed record AdminSessionModeratorRow(
    Guid SessionId,
    string SessionCode,
    string SessionTitle,
    string SessionTitleArabic,
    Guid UserId,
    string ModeratorDisplayName,
    string? ModeratorEmail,
    Guid AssignedByUserId,
    string AssignedByDisplayName,
    DateTimeOffset AssignedAt);
