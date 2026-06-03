using SIMF.Domain.Common;

namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// One action on one page — the unit of authorisation in the SIMF
/// page-and-action permission model (SIMF-RPM-001 section 8,
/// SIMF-DAT-001 section 5.1).
/// </summary>
public class Permission : BaseEntity
{
    /// <summary>The stable code used in authorisation checks.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The human-readable name shown when assigning permissions.</summary>
    public string DisplayName { get; set; } = string.Empty;


    /// <summary>The page the action belongs to.</summary>
    public string Page { get; set; } = string.Empty;

    /// <summary>The action on the page.</summary>
    public string Action { get; set; } = string.Empty;



    /// <summary>The role grants that include this permission.</summary>
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
