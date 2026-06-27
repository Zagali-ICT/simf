using SIMF.Common;
using SIMF.Contracts.Requests;

namespace SIMF.Application.Requests.Abstractions;

/// <summary>D-500 (Wave 5, الطلبات "طلب وثيقة المشاركة") — participation-document
/// requests: login-required submission + admin review. Mirrors
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
