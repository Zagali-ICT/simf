using SIMF.Common;
using SIMF.Contracts.Sessions;

namespace SIMF.Application.MeetingRequests.Abstractions;

/// <summary>D-174 (gap doc G11, Mockup page 27) — meeting/interview
/// request service. Public submission + admin response.</summary>
public interface IMeetingRequestService
{
    Task<MeetingRequestSubmitted> SubmitAsync(
        Guid sessionId, Guid requesterUserId,
        SubmitMeetingRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<GridPage<AdminMeetingRequestRow>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminMeetingRequestRow> RespondAsync(
        Guid actorUserId, Guid id,
        RespondToMeetingRequestRequest request,
        CancellationToken cancellationToken = default);
}
