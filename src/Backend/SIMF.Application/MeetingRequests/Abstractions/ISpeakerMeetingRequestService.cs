using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Application.MeetingRequests.Abstractions;

/// <summary>D-269 (Mockup page 20 "Speaker profile") — attendee meeting requests
/// TO a speaker (gated by <c>Speaker.AllowsMeetingRequests</c>). Login-required
/// submission + admin review. Sibling of <see cref="IMeetingRequestService"/>
/// (the session-scoped screen-27 flow), kept separate so the two request kinds
/// never overload one model.</summary>
public interface ISpeakerMeetingRequestService
{
    Task<SpeakerMeetingRequestSubmitted> SubmitAsync(
        Guid speakerId, Guid requesterUserId,
        SubmitSpeakerMeetingRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<GridPage<AdminSpeakerMeetingRequestRow>> ListAllAsync(
        Guid actorUserId, GridQuery query,
        CancellationToken cancellationToken = default);

    Task<AdminSpeakerMeetingRequestDetail> GetAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default);

    Task<AdminSpeakerMeetingRequestDetail> RespondAsync(
        Guid actorUserId, Guid id,
        RespondToSpeakerMeetingRequestRequest request,
        CancellationToken cancellationToken = default);
}
