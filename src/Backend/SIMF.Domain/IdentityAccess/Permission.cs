namespace SIMF.Domain.IdentityAccess;

public class Permission
{
    public Guid Id { get; set; }

    public string Page { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>"Page.Action"; seeded from and must match PermissionCatalog.</summary>
    public string Code { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
