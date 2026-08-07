using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Admin-side CRUD over the ProfileType lookup table. Read-only
/// surface for the CP picker lives in
/// <see cref="IAdminProfileTypeQueryService"/>; this interface adds the
/// mutating verbs (Create / Update / Deactivate) plus the paged list +
/// single-row get used by the admin CRUD page itself.
///
/// <para>Naming follows the InterestService convention — one service
/// covers list + get + create + update + deactivate so callers don't
/// have to compose two interfaces for a single management screen.</para>
/// </summary>
public interface IAdminProfileTypeCommandService
{
    /// <summary>One page of the admin grid — accepts the optional
    /// <c>userType</c> filter from <see cref="GridQuery.Filters"/>.</summary>
    Task<GridPage<AdminProfileTypeSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>One row by id, or null when missing.</summary>
    Task<AdminProfileTypeSummary?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new row. Throws on invalid UserType or duplicate
    /// name within the same UserType.</summary>
    Task<AdminProfileTypeSummary> CreateAsync(
        Guid actorUserId,
        AdminCreateProfileTypeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Updates Name / NameArabic / PageColor / IsActive. UserType
    /// is immutable post-creation.</summary>
    Task<AdminProfileTypeSummary> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateProfileTypeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes (deactivates) the row. Idempotent on
    /// already-inactive rows. Refuses if any UserProfile still references
    /// this id (returns ProfileTypeInUse, 409).</summary>
    Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default);
}
