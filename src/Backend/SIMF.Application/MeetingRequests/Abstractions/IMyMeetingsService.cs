using SIMF.Contracts.Programme;

namespace SIMF.Application.MeetingRequests.Abstractions;

/// <summary>
/// D-479 (#11 follow-up) — the read-only "My meetings" feed for the mobile app: the
/// meetings the signed-in user requested (speaker meeting requests, D-269/D-475, +
/// delegation meeting requests, D-478), newest first. Read-only — creation/management
/// of delegation meetings lives on the Control Panel.
/// </summary>
public interface IMyMeetingsService
{
    Task<IReadOnlyList<MyMeetingItem>> GetMyMeetingsAsync(
        Guid userId, CancellationToken cancellationToken = default);
}
