using SIMF.Common;
using SIMF.Contracts.PublicRelations;

namespace SIMF.Application.PublicRelations.Abstractions;

/// <summary>D-199 — admin CRUD contract over News articles (PR / marketing).
/// Mirrors <c>IAdminDelegationService</c> / <c>IAdminSpeakerService</c>:
/// server-paged list, get-by-id, create, update, soft-delete; every mutation
/// is audited and stamped with the actor user id.</summary>
public interface IAdminNewsService
{
    Task<GridPage<AdminNewsSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminNewsDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminNewsDetail> CreateAsync(
        Guid actorUserId, CreateNewsRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminNewsDetail> UpdateAsync(
        Guid actorUserId, Guid id, UpdateNewsRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default);
}
