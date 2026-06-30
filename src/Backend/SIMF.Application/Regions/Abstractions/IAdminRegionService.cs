using SIMF.Common;
using SIMF.Contracts.Regions;

namespace SIMF.Application.Regions.Abstractions;

/// <summary>Admin CRUD over <c>Region</c> (bilingual administrative-regions
/// lookup). Mirrors IAdminOrganisationService.</summary>
public interface IAdminRegionService
{
    /// <summary>One page of region summaries for the admin grid.</summary>
    Task<GridPage<AdminRegionSummary>> ListAsync(
        GridQuery query, CancellationToken ct = default);

    /// <summary>One region by id, or null when it is missing.</summary>
    Task<AdminRegionDetail?> GetAsync(
        Guid id, CancellationToken ct = default);

    /// <summary>Creates a new region and returns its full detail.</summary>
    Task<AdminRegionDetail> CreateAsync(
        Guid actorUserId, CreateRegionRequest request,
        CancellationToken ct = default);

    /// <summary>Updates an existing region and returns its full detail.</summary>
    Task<AdminRegionDetail> UpdateAsync(
        Guid actorUserId, Guid id, UpdateRegionRequest request,
        CancellationToken ct = default);

    /// <summary>Soft-deletes (deactivates) a region.</summary>
    Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken ct = default);
}
