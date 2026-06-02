using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Programme;

namespace SIMF.Application.Venue.Abstractions;

/// <summary>P2.5 — D-230 (FR-605): the 2D venue map. Admin CRUD over nodes plus
/// the public read the Flutter app renders. Built on SimfAppDbContext.</summary>
public interface IVenueMapService
{
    Task<GridPage<AdminVenueMapNodeSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminVenueMapNodeDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminVenueMapNodeDetail> CreateAsync(
        Guid actorUserId, AdminCreateVenueMapNodeRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminVenueMapNodeDetail> UpdateAsync(
        Guid actorUserId, Guid id, AdminUpdateVenueMapNodeRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>All active nodes for the app's 2D map.</summary>
    Task<IReadOnlyList<PublicVenueMapNode>> ListPublicAsync(
        CancellationToken cancellationToken = default);
}
