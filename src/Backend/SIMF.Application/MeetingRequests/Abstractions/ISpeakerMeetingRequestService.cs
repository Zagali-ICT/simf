using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Application.MeetingRequests.Abstractions;

/// <summary>D-269 (Mockup page 20 "Speaker profile") — attendee meeting requests
/// TO a speaker (gated by <c>Speaker.AllowsMeetingRequests</c>). Login-required
/// submission + admin review. Was a sibling of the session-scoped screen-27 flow
/// (<c>IMeetingRequestService</c>, removed in D-278), kept separate so the two
/// request kinds never overloaded one model.</summary>
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

    /// <summary>R-1 — re-mint the speaker's Approve/Reject confirmation tokens for a
    /// request still AwaitingSpeaker (the previous pair expired or the email was never
    /// sent) and re-email the links. 409 when the request is not AwaitingSpeaker.</summary>
    Task ResendSpeakerConfirmationAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Bi-Meeting rework — an operator checks the meeting in at the hall,
    /// flipping a confirmed (Accepted) meeting to <see cref="MeetingRequestStatus.Done"/>
    /// and stamping <c>CheckedInAt</c>/<c>CheckedInByUserId</c>. 409 when the meeting is
    /// not confirmed.</summary>
    Task<AdminSpeakerMeetingRequestDetail> CheckInAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>QA B20 — put a Rejected / Cancelled request back to a clean
    /// <see cref="MeetingRequestStatus.Pending"/> so a mistaken decline or cancel is
    /// recoverable. 409 for any other status (the slot-holding states must not be
    /// reopened behind the parties' backs). Writes a
    /// <c>SpeakerMeetingRequest.Reopened</c> audit entry.</summary>
    Task<AdminSpeakerMeetingRequestDetail> ReopenAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default);
}
