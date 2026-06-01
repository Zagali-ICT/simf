using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.Programme.Abstractions;

/// <summary>B9b — D-226: admin CRUD over the dynamic <c>SessionCategory</c>
/// lookup (FDS-004 §5.4). Mirrors IAdminOrganisationService (minus import).</summary>
public interface IAdminSessionCategoryService
{
    /// <summary>One page of category summaries for the admin grid.</summary>
    Task<GridPage<AdminSessionCategorySummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>One category by id, or null when it is missing.</summary>
    Task<AdminSessionCategoryDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new category and returns its full detail.</summary>
    Task<AdminSessionCategoryDetail> CreateAsync(
        Guid actorUserId, AdminCreateSessionCategoryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Updates an existing category and returns its full detail.</summary>
    Task<AdminSessionCategoryDetail> UpdateAsync(
        Guid actorUserId, Guid id, AdminUpdateSessionCategoryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes (deactivates) a category.</summary>
    Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default);
}
