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

    /// <summary>D-275 (§9) — snapshot the current live event into a new edition
    /// (year + title generated, counters computed from live data). Reuses
    /// <see cref="CreateAsync"/> so the one-edition-per-year 409 + audit apply.</summary>
    Task<AdminArchiveEditionDetail> SnapshotCurrentAsync(
        Guid actorUserId, SnapshotCurrentEditionRequest request,
        CancellationToken cancellationToken = default);
}
