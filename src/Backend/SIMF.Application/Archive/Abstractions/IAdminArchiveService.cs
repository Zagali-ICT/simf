using SIMF.Common;
using SIMF.Contracts.Archive;

namespace SIMF.Application.Archive.Abstractions;

/// <summary>D-199 — admin CRUD over <c>ArchiveEdition</c>. One edition per
/// year; the service enforces year uniqueness and maps a clash to a 409.</summary>
public interface IAdminArchiveService
{
    Task<GridPage<AdminArchiveEditionSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminArchiveEditionDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminArchiveEditionDetail> CreateAsync(
        Guid actorUserId, CreateArchiveEditionRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminArchiveEditionDetail> UpdateAsync(
        Guid actorUserId, Guid id, UpdateArchiveEditionRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default);
}
