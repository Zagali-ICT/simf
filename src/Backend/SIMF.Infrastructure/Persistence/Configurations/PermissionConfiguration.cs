using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Configurations;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.Page).HasMaxLength(100).IsRequired();
        builder.Property(permission => permission.Action).HasMaxLength(100).IsRequired();
        builder.Property(permission => permission.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(permission => permission.Code).HasMaxLength(150).IsRequired();

        builder.HasIndex(permission => permission.Code).IsUnique();
        builder.HasIndex(permission => new { permission.Page, permission.Action }).IsUnique();
    }
}
