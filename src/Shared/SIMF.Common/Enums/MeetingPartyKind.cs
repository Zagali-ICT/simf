namespace SIMF.Common.Enums;

/// <summary>
/// SIMF-FDS-013 (owner, 2026-06-03) — what a participant in a
/// <c>BusinessMeeting</c> is. A <see cref="Company"/> party is a real FK to
/// <c>Company.Id</c> on the App DB (exhibitor or sponsor — the account-bearing
/// commercial entity, OI-1); a <see cref="Visitor"/> party is a <b>bare
/// <c>Guid</c></b> logical FK to <c>SimfUser.Id</c> on the Identity DB (no
/// cross-DB navigation / no FK, D-157 / D-246).
/// </summary>
public enum MeetingPartyKind
{
    Company = 0,
    Visitor = 1,
}
