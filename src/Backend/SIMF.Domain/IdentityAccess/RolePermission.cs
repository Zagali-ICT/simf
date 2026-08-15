namespace SIMF.Domain.IdentityAccess;

public class RolePermission
{
    public Guid RoleId { get; set; }

    public SimfRole Role { get; set; } = null!;

    public Guid PermissionId { get; set; }

    public Permission Permission { get; set; } = null!;
}
