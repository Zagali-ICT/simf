using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Application.MeetingRequests.Abstractions;

/// <summary>
/// Delegation↔delegation (G2G) meeting requests. A
/// delegate submits a request for their delegation to meet another invited
/// country's delegation; the team reviews + Accepts/Rejects; on accept the
/// requester is notified + emailed. Mirrors the speaker meeting-request service.
/// </summary>
public interface IDelegationMeetingRequestService
{
    /// <summary>Submit a request. 403 when the caller is not a delegate; 400 on an
    /// invalid/non-invited target or an invalid count/subject.</summary>
    Task<DelegationMeetingRequestSubmitted> SubmitAsync(
        Guid requesterUserId, SubmitDelegationMeetingRequestRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>The admin desk — paged, filterable by status / target country.</summary>
    Task<GridPage<AdminDelegationMeetingRequestRow>> ListAllAsync(
        Guid actorUserId, GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>One request's detail (adds the requester email, resolved on read).</summary>
    Task<AdminDelegationMeetingRequestDetail> GetAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Accept / reject; notifies (and on accept emails) the requester.</summary>
    Task<AdminDelegationMeetingRequestDetail> RespondAsync(
        Guid actorUserId, Guid id, RespondToDelegationMeetingRequestRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Bi-Meeting rework — the OTHER PARTY confirms an Approved (AwaitingSpeaker)
    /// meeting from the app (confirm-on-tap). 403 when the caller is not an eligible
    /// member of the target delegation; 409 when the request is not awaiting confirmation.
    /// Flips AwaitingSpeaker → Accepted (Confirmed) via a race-safe conditional update.</summary>
    Task<AdminDelegationMeetingRequestDetail> ConfirmByOtherPartyAsync(
        Guid callerUserId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>B8 — the OTHER PARTY DECLINES an Approved (AwaitingSpeaker) meeting from
    /// the app. The exact mirror of <see cref="ConfirmByOtherPartyAsync"/>: same
    /// authorization model (an eligible member of the target delegation), same audit
    /// event, same notification treatment. Flips AwaitingSpeaker →
    /// <see cref="SIMF.Common.Enums.MeetingRequestStatus.Rejected"/> via a race-safe conditional update and
    /// releases the held hall slot, so the target is no longer trapped between confirming
    /// and waiting for an admin cancel. 403 when the caller is not an eligible member;
    /// 409 when the request is not awaiting confirmation.</summary>
    Task<AdminDelegationMeetingRequestDetail> DeclineByOtherPartyAsync(
        Guid callerUserId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>B11 — retract the target delegation's live "please confirm" prompt for a
    /// meeting that is now dead. Every eligible member of the target delegation received a
    /// <c>MeetingRequested</c> card deep-linking to the confirm screen plus an emailed
    /// confirm link at approve time; once the meeting is off, those all 409, so each member
    /// is sent a cancelled notice instead. Called by the REQUESTER's own withdraw
    /// (<c>IMyRequestsService.CancelAsync</c>), which owns the status flip but has no
    /// delegation-notification surface of its own. Best-effort and idempotent-safe: a
    /// missing request is a no-op, never a throw, so a notification failure cannot undo an
    /// already-committed cancel.</summary>
    Task RetractTargetMemberPromptsAsync(
        Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Bi-Meeting rework — an operator checks the meeting in at the hall,
    /// flipping a confirmed (Accepted) meeting to <see cref="SIMF.Common.Enums.MeetingRequestStatus.Done"/>
    /// and stamping <c>CheckedInAt</c>/<c>CheckedInByUserId</c>. 409 when the meeting is
    /// not confirmed.</summary>
    Task<AdminDelegationMeetingRequestDetail> CheckInAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default);
}
