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

        // Owner 2026-07-06 — reasonable org-name cap (was 256), aligned with the
        // CP form MaxLength.
        builder.Property(organisation => organisation.NameArabic).HasMaxLength(150).IsRequired();
        builder.Property(organisation => organisation.Name).HasMaxLength(150);
        // Owner ask — 700, not the original 32. A government sheet carries more
        // than a bare CR number in this cell. nvarchar(700) is 1400 bytes, still
        // inside the 1700-byte key limit, so the filtered unique index below
        // survives the widening.
        builder.Property(organisation => organisation.CommercialRegistration).HasMaxLength(700);
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

        builder.HasIndex(organisation => new { organisation.IsActive, organisation.NameArabic });

        // "Other", so a visitor whose employer is not in this curated list can
        // still finish registering — organisation is required on the form,
        // and before this the picker said "no matches" and the
        // registration stopped there. They pick this row and type the real name
        // into UserProfile.OrganisationOther.
        //
        // Seeded in EF model data rather than by a service, for the same reason
        // BadgeBatch.DirectRegistrationId is: it has to exist the instant the
        // database does, because the first registration can need it.
        //
        // CommercialRegistration is deliberately null. The government Excel
        // import matches on that column, so a row without one can never be
        // updated or duplicated by a re-import.
        //
        // A fixed date, not "now" — HasData is compared into the migration, and
        // a moving value makes every model check report a pending change.
        builder.HasData(new Organisation
        {
            Id = Organisation.OtherId,
            Name = "Other",
            NameArabic = "أخرى",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0),
        });
    }
}
