using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Files;
using SIMF.Domain.Sponsors;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>Sponsor EF config. Mirrors
/// DelegationConfiguration / CountryConfiguration. <see cref="Sponsor.Tier"/>
/// is stored as its int value (the public sort weight). The composite index
/// matches the public read order (active rows, by tier, then display order).
/// Configs are auto-discovered by SimfAppDbContext.OnModelCreating via
/// ApplyConfigurationsFromAssembly, so no manual registration is needed.</summary>
internal sealed class SponsorConfiguration : IEntityTypeConfiguration<Sponsor>
{
    public void Configure(EntityTypeBuilder<Sponsor> builder)
    {
        builder.ToTable("Sponsors");
        builder.HasKey(sponsor => sponsor.Id);

        builder.Property(sponsor => sponsor.Name).HasMaxLength(256).IsRequired();
        builder.Property(sponsor => sponsor.NameArabic).HasMaxLength(256).IsRequired();

        // Persist the enum by its int value (the wire/sort contract), matching
        // the additive-only enum discipline. EF stores enums as int by default;
        // HasConversion<int>() makes that explicit and migration-stable.
        builder.Property(sponsor => sponsor.Tier).HasConversion<int>().IsRequired();

        // The sponsor logo, in the one file store. Restrict: deleting a file must never
        // delete the row that shows it.
        builder.HasIndex(sponsor => sponsor.LogoFileId);
        builder.HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(sponsor => sponsor.LogoFileId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(sponsor => sponsor.Url).HasMaxLength(512);

        // Optional bilingual tagline (<=256, mirrors the service-layer
        // validation + CP MaxLength). Additive nullable columns.
        builder.Property(sponsor => sponsor.Tagline).HasMaxLength(256);
        builder.Property(sponsor => sponsor.TaglineArabic).HasMaxLength(256);

        // Optional bilingual "نبذة عن الراعي" about paragraph (≤2048,
        // mirrors the CP MaxLength). Additive nullable columns.
        builder.Property(sponsor => sponsor.About).HasMaxLength(2048);
        builder.Property(sponsor => sponsor.AboutArabic).HasMaxLength(2048);

        // Contact identity-card fields inlined from the removed shared Contact
        // directory. All nullable. The Website
        // slot is the existing Url above. Latitude/Longitude are double? and need
        // no length.
        builder.Property(sponsor => sponsor.Email).HasMaxLength(320);
        builder.Property(sponsor => sponsor.PhonePrimary).HasMaxLength(32);
        builder.Property(sponsor => sponsor.PhoneSecondary).HasMaxLength(32);
        builder.Property(sponsor => sponsor.FacebookUrl).HasMaxLength(256);
        builder.Property(sponsor => sponsor.XUrl).HasMaxLength(256);
        builder.Property(sponsor => sponsor.LinkedInUrl).HasMaxLength(256);
        builder.Property(sponsor => sponsor.InstagramUrl).HasMaxLength(256);
        builder.Property(sponsor => sponsor.City).HasMaxLength(128);
        builder.Property(sponsor => sponsor.CityArabic).HasMaxLength(128);

        // Country inlined from the removed Contact directory — real DB FK on the
        // same-DB reference (App DB). Restrict matches the soft-delete policy
        // (countries are deactivated via IsActive, never hard-deleted under a
        // referrer); HasForeignKey creates the FK index.
        builder.HasOne(sponsor => sponsor.Country)
            .WithMany()
            .HasForeignKey(sponsor => sponsor.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Public read order: active first, by tier, then display order.
        builder.HasIndex(sponsor => new
        {
            sponsor.IsActive,
            sponsor.Tier,
            sponsor.DisplayOrder,
        });
    }
}
