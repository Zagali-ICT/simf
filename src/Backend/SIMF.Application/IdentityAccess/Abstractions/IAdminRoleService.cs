using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Admin CRUD over the SimfRole table. Built on top of
/// the existing <c>SimfRole</c> + <c>Permission</c> + <c>RolePermission</c>
/// entities — no schema change. The MVP surface covers list + create
/// (custom only) + rename (custom only) + delete (custom only AND no
/// user holds the role). Permission-grant editing + assign-to-user
/// flows are deliberately deferred to a follow-up commit so this slice
/// stays minimum-viable per CLAUDE.md §17.
/// </summary>
public interface IAdminRoleService
{
    /// <summary>One page of the admin grid. Search matches role name
    /// case-insensitively. Sort defaults to baseline first then name.</summary>
    Task<GridPage<AdminRoleSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>One role by id, or <c>null</c> when missing.</summary>
    Task<AdminRoleSummary?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new <i>custom</i> role (<c>IsBaseline = false</c>).
    /// The name must be unique across all roles (Identity already enforces
    /// this via the unique index on <c>NormalizedName</c>).
    /// Throws <c>RoleNameDuplicate</c> (409) on clash.</summary>
    Task<AdminRoleSummary> CreateAsync(
        Guid actorUserId,
        AdminCreateRoleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Renames a custom role. Throws <c>RoleIsBaseline</c> (409)
    /// for baseline roles and <c>RoleNameDuplicate</c> (409) on clash.</summary>
    Task<AdminRoleSummary> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateRoleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Hard-deletes a custom role. Throws <c>RoleIsBaseline</c>
    /// (409) for baseline roles + <c>RoleInUse</c> (409) when any user
    /// holds the role. Cascade-deletes the role's <c>RolePermission</c>
    /// rows.</summary>
    Task DeleteAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Issue-1 — the permission codes a role currently grants, plus
    /// its name + baseline flag. Returns <c>null</c> when the role is missing.</summary>
    Task<AdminRolePermissionsResponse?> GetPermissionsAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>Issue-1 — replaces a <i>custom</i> role's permission grants
    /// with exactly <paramref name="codes"/>. Throws <c>RoleIsBaseline</c>
    /// (409) for baseline roles (Administrator is the wildcard; GateOperator /
    /// PublicRelations are seeded) and <c>ValidationFailed</c> (400) when any
    /// code is not in the permission catalogue.</summary>
    Task SetPermissionsAsync(
        Guid actorUserId,
        Guid id,
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken = default);
}
