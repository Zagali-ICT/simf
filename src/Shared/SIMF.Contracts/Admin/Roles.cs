namespace SIMF.Contracts.Admin;

/// <summary>One row in the admin <c>Roles</c> grid (D-134 Sprint A —
/// builds on the existing SimfRole + Permission + RolePermission entities
/// — no schema change). Surfaces what an admin needs at-a-glance: the
/// role's name, whether it ships built-in, how many users hold it, and
/// how many permissions it grants.</summary>
public sealed record AdminRoleSummary(
    Guid Id,
    string Name,
    bool IsBaseline,
    int UserCount,
    int PermissionCount);

/// <summary>The body of <c>POST /api/v1/admin/roles</c> — creates a
/// custom (non-baseline) role. Built-in roles are seeded; this endpoint
/// is for admin-created roles only.</summary>
public sealed class AdminCreateRoleRequest
{
    /// <summary>The role display name (1–64 chars; unique across all roles).
    /// Maps to <c>SimfRole.Name</c> + <c>NormalizedName</c> via Identity.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>The body of <c>PUT /api/v1/admin/roles/{id}</c> — renames a
/// custom role. Baseline roles cannot be renamed.</summary>
/// <remarks>Not sealed: the admin update endpoint binds {id}+body via a derived
/// route class (D-505 / D-844) so it cannot drop a field at bind time.</remarks>
public class AdminUpdateRoleRequest
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>The response of <c>GET /api/v1/admin/roles/{id}/permissions</c>
/// (Issue-1) — the permission codes a role currently grants. The full
/// catalogue is not returned: the CP builds it from
/// <c>PermissionCatalog.All</c>. Baseline roles are read-only (their grants
/// are seeded / wildcard), so the editor disables them.</summary>
public sealed record AdminRolePermissionsResponse(
    Guid RoleId,
    string RoleName,
    bool IsBaseline,
    IReadOnlyList<string> GrantedCodes);

/// <summary>The body of <c>PUT /api/v1/admin/roles/{id}/permissions</c>
/// (Issue-1) — the complete set of permission codes the (custom) role should
/// grant. The server replaces the role's grants with exactly this set; every
/// code must exist in the permission catalogue.</summary>
public sealed class AdminSetRolePermissionsRequest
{
    public List<string> Codes { get; set; } = new();
}
