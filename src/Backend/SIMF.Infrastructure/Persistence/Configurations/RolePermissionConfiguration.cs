using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Identity;

namespace SIMF.Infrastructure.Persistence.Configurations;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasKey(rolePermission => new { rolePermission.RoleId, rolePermission.PermissionId });

        // R5g — D-093: role nav prop removed from the Domain entity; the FK
        // is expressed as a shadow relationship targeting the
        // Infrastructure-owned IdentitySimfRole shim.
        builder.HasOne<IdentitySimfRole>()
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rolePermission => rolePermission.Permission)
            .WithMany(permission => permission.RolePermissions)
            .HasForeignKey(rolePermission => rolePermission.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
