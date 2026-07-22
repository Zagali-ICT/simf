using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Application.MeetingRequests.Abstractions;

/// <summary>
/// D-478 (#11, Group G phase 2) — delegation↔delegation (G2G) meeting requests. A
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
}
