using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Regions;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>Region (administrative-regions lookup) entity configuration.
/// Bilingual, soft-delete via IsActive. <c>Code</c> is the stable lookup key
/// and is unique across the rows so a re-seed updates / matches the existing
/// row rather than inserting a duplicate. Mirrors OrganisationConfiguration.
///
/// Lives in the <c>...Configurations.App</c> namespace so
/// <c>SimfAppDbContext.OnModelCreating</c>'s
/// <c>ApplyConfigurationsFromAssembly(... type.Namespace ==
/// "SIMF.Infrastructure.Persistence.Configurations.App")</c> picks it up
/// automatically — no manual registration needed.</summary>
internal sealed class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("Regions");
        builder.HasKey(region => region.Id);

        builder.Property(region => region.Code).HasMaxLength(16).IsRequired();
        builder.Property(region => region.NameArabic).HasMaxLength(256).IsRequired();
        builder.Property(region => region.Name).HasMaxLength(256);

        // The code is the stable lookup key — unique across regions so a re-seed
        // matches the existing row instead of duplicating it.
        builder.HasIndex(region => region.Code).IsUnique();

        builder.HasIndex(region => new { region.IsActive, region.SortOrder });
    }
}
