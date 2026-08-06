using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Configuration;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>SystemSetting EF config. Unique filtered index on
/// Key (active rows) so a deactivated key can be re-created.</summary>
internal sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("SystemSettings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(512);

        builder.HasIndex(x => x.Key)
            .HasFilter("[IsActive] = 1")
            .IsUnique();
    }
}
