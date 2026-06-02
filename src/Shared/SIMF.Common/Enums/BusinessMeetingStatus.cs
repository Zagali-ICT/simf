namespace SIMF.Common.Enums;

/// <summary>
/// SIMF-FDS-013 (owner, 2026-06-03) — the lifecycle state of an admin-arranged
/// business meeting. Admin-arranged meetings are created <see cref="Confirmed"/>
/// (there is no attendee request/approval queue) and can be
/// <see cref="Cancelled"/>.
/// </summary>
public enum BusinessMeetingStatus
{
    Confirmed = 0,
    Cancelled = 1,
}
