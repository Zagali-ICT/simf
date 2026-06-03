namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// Grants one <see cref="Permission"/> to one <see cref="SimfRole"/>
/// (SIMF-DAT-001 section 5.1).
/// </summary>
public class RolePermission
{
    public Guid RoleId { get; set; }

    public SimfRole Role { get; set; } = null!;


    public Guid PermissionId { get; set; }

    public Permission Permission { get; set; } = null!;
}
