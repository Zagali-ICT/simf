namespace SIMF.Common.Enums;

/// <summary>
/// What a participant in a
/// <c>BusinessMeeting</c> is. A <see cref="Company"/> party is a real FK to
/// <c>Company.Id</c> on the App DB (exhibitor or sponsor — the account-bearing
/// commercial entity); a <see cref="Visitor"/> party is a <b>bare
/// <c>Guid</c></b> logical FK to <c>SimfUser.Id</c> on the Identity DB, with no
/// cross-DB navigation and no FK.
/// </summary>
public enum MeetingPartyKind
{
    Company = 0,
    Visitor = 1,
}
