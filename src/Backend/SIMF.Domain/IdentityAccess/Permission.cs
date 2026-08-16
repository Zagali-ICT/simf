namespace SIMF.Domain.IdentityAccess;

public class Permission
{
    public Guid Id { get; set; }

    /// <summary>"Page.Action"; seeded from and must match PermissionCatalog.
    /// The only persisted field of a permission: the page, action and display
    /// name a catalogue entry also carries are presentation metadata that the
    /// Control Panel reads straight off the in-process
    /// <c>PermissionCatalog</c>, so persisting them here only duplicated
    /// them.</summary>
    public string Code { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
