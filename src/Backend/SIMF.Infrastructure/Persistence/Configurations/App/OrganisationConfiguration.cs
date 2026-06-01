using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Organisations;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>Organisation (Saudi companies lookup) entity configuration.
/// Bilingual, soft-delete via IsActive. <c>CommercialRegistration</c> is
/// unique among the rows that have one (filtered unique index) so a re-import
/// of the same government sheet updates the matching row rather than
/// inserting a duplicate. Mirrors BoothConfiguration.
///
/// Lives in the <c>...Configurations.App</c> namespace so
/// <c>SimfAppDbContext.OnModelCreating</c>'s
/// <c>ApplyConfigurationsFromAssembly(... type.Namespace ==
/// "SIMF.Infrastructure.Persistence.Configurations.App")</c> picks it up
/// automatically — no manual registration needed.</summary>
internal sealed class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    public void Configure(EntityTypeBuilder<Organisation> builder)
    {
        builder.ToTable("Organisations");
        builder.HasKey(organisation => organisation.Id);

        builder.Property(organisation => organisation.NameAr).HasMaxLength(256).IsRequired();
        builder.Property(organisation => organisation.NameEn).HasMaxLength(256);
        builder.Property(organisation => organisation.CommercialRegistration).HasMaxLength(32);
        builder.Property(organisation => organisation.Sector).HasMaxLength(128);
        builder.Property(organisation => organisation.City).HasMaxLength(128);
        builder.Property(organisation => organisation.Phone).HasMaxLength(32);
        builder.Property(organisation => organisation.Email).HasMaxLength(320);
        builder.Property(organisation => organisation.Website).HasMaxLength(512);

        // Unique only among rows that carry a commercial registration so a
        // re-import updates the matching row instead of duplicating it.
        builder.HasIndex(organisation => organisation.CommercialRegistration)
            .IsUnique()
            .HasFilter("[CommercialRegistration] IS NOT NULL");

        builder.HasIndex(organisation => new { organisation.IsActive, organisation.NameAr });
    }
}
