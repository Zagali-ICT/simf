using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.UserProfile;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Interests CRUD (الاهتمامات). Two read surfaces — the
/// visitor picker (active only, ordered for the UI) and the admin grid
/// (every row, paged + filtered). Mutations are admin-only; every one
/// writes an audit row.
/// </summary>
public interface IInterestService
{
    /// <summary>Active interests for the visitor picker — ordered by
    /// <c>DisplayOrder</c>, tie-broken by <c>Name</c>.</summary>
    Task<IReadOnlyList<InterestDto>> ListActiveAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Paged + filtered list for the admin CP grid.</summary>
    Task<GridPage<AdminInterestSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>One row, or null when not found.</summary>
    Task<AdminInterestSummary?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new interest; audited.</summary>
    Task<AdminInterestSummary> CreateAsync(
        Guid actorUserId,
        AdminCreateInterestRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Updates an existing interest; audited.</summary>
    Task<AdminInterestSummary> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateInterestRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes (deactivates) an interest. Idempotent — a
    /// second call on an already-inactive row is a no-op.</summary>
    Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default);
}
