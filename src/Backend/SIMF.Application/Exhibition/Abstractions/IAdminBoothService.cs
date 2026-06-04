using SIMF.Common;
using SIMF.Contracts.Exhibition;

namespace SIMF.Application.Exhibition.Abstractions;

/// <summary>D-199 — admin CRUD over <c>Booth</c> (Exhibition module).
/// Mirrors IAdminSpeakerService.</summary>
public interface IAdminBoothService
{
    Task<GridPage<AdminBoothSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminBoothDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminBoothDetail> CreateAsync(
        Guid actorUserId, AdminCreateBoothRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminBoothDetail> UpdateAsync(
        Guid actorUserId, Guid id, AdminUpdateBoothRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default);
}
