using SIMF.Common;
using SIMF.Contracts.Requests;

namespace SIMF.Application.Requests.Abstractions;

/// <summary>D-500 (Wave 5, الطلبات "طلب تحديث البادج") — badge job-title update
/// requests: login-required submission + admin review. On Accept the service
/// applies the requested title to the requester's profile. Mirrors
/// <c>ISpeakerMeetingRequestService</c>.</summary>
public interface IBadgeUpdateRequestService
{
    Task<BadgeUpdateRequestSubmitted> SubmitAsync(
        Guid requesterUserId, SubmitBadgeUpdateRequestBody request,
        CancellationToken cancellationToken = default);

    Task<GridPage<AdminBadgeUpdateRequestRow>> ListAllAsync(
        Guid actorUserId, GridQuery query,
        CancellationToken cancellationToken = default);

    Task<AdminBadgeUpdateRequestDetail> GetAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default);

    Task<AdminBadgeUpdateRequestDetail> RespondAsync(
        Guid actorUserId, Guid id,
        RespondToBadgeUpdateRequestRequest request,
        CancellationToken cancellationToken = default);
}
