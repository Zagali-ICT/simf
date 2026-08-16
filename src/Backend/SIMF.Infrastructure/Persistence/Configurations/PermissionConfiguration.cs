using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Configurations;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.Code).HasMaxLength(150).IsRequired();

        // Code is "Page.Action", so this one unique index still enforces the
        // one-row-per-page-and-action rule the old (Page, Action) index did.
        builder.HasIndex(permission => permission.Code).IsUnique();
    }
}
