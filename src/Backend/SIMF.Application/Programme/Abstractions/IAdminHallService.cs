using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.Programme.Abstractions;

/// <summary>
/// D-134 Sprint B — admin CRUD over <see cref="SIMF.Domain.Programme.Hall"/>.
/// SIMF-FDS-004 §5.2. Halls host sessions; the Sessions module's hall
/// picker reads from <see cref="ListAllAsync"/> with an
/// <c>isActive=true</c> filter.
/// </summary>
public interface IAdminHallService
{
    Task<GridPage<AdminHallSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminHallDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminHallDetail> CreateAsync(
        Guid actorUserId,
        AdminCreateHallRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminHallDetail> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateHallRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default);
}
