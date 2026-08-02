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
    DateTime AssignedAt);

/// <summary>DEF-MOD-005 — the two pickers behind the "assign a moderator"
/// dialog. The page used to take the session id and the user id as free-text
/// GUIDs, so a typo silently handed a moderation desk to an unrelated account
/// and there was no way to find the right person. Both lists are gated by
/// <c>SessionModerators.Assign</c> — the same permission as the write — so
/// whoever legitimately manages moderators can reach the lookups.</summary>
public sealed record SessionModeratorAssignOptions(
    IReadOnlyList<SessionModeratorSessionOption> Sessions,
    IReadOnlyList<SessionModeratorCandidate> Candidates);

/// <summary>DEF-MOD-005 — one active session offered by the session picker.</summary>
public sealed record SessionModeratorSessionOption(
    Guid Id,
    string Code,
    string Title,
    string TitleArabic);

/// <summary>DEF-MOD-005 — one account eligible to moderate: an APPROVED account
/// whose assigned partner profile type carries
/// <c>MobileAppRole.Moderator</c>. The profile-type name travels so the admin can
/// tell two same-named people apart.</summary>
public sealed record SessionModeratorCandidate(
    Guid UserId,
    string DisplayName,
    string? Email,
    string ProfileTypeName,
    string ProfileTypeNameArabic);
