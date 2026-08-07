using SIMF.Common;
using SIMF.Contracts.Requests;

namespace SIMF.Application.Requests.Abstractions;

/// <summary>Participation-document requests on the الطلبات screen
/// ("طلب وثيقة المشاركة"): login-required submission + admin review. Mirrors
/// <c>ISpeakerMeetingRequestService</c>; no counterparty (the document is issued
/// off-band).</summary>
public interface IParticipationDocumentRequestService
{
    Task<ParticipationDocumentRequestSubmitted> SubmitAsync(
        Guid requesterUserId, SubmitParticipationDocumentRequestBody request,
        CancellationToken cancellationToken = default);

    Task<GridPage<AdminParticipationDocumentRequestRow>> ListAllAsync(
        Guid actorUserId, GridQuery query,
        CancellationToken cancellationToken = default);

    Task<AdminParticipationDocumentRequestDetail> GetAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default);

    Task<AdminParticipationDocumentRequestDetail> RespondAsync(
        Guid actorUserId, Guid id,
        RespondToParticipationDocumentRequestRequest request,
        CancellationToken cancellationToken = default);
}
