namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// Grants one <see cref="Permission"/> to one role row (SIMF-DAT-001
/// section 5.1). The role is identified by <see cref="RoleId"/>; R5g
/// (D-093) dropped the navigation property because the role entity
/// (<c>IdentitySimfRole</c>) lives in Infrastructure and Domain is now
/// framework-free. The EF foreign-key relationship is expressed as a
/// shadow navigation in <c>RolePermissionConfiguration</c>.
/// </summary>
public class RolePermission
{
    public Guid RoleId { get; set; }

    public Guid PermissionId { get; set; }

    public Permission Permission { get; set; } = null!;
}
