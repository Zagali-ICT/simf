using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Contacts;
using SIMF.Domain.Sponsors;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-199 (Mockup page 23) — Sponsor EF config. Mirrors
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

        builder.Property(sponsor => sponsor.LogoRelativePath).HasMaxLength(256);
        builder.Property(sponsor => sponsor.Url).HasMaxLength(512);

        // D-432 — optional bilingual tagline (≤256, mirrors the validator + CP
        // MaxLength). Additive nullable columns (D-219 freeze-lift).
        builder.Property(sponsor => sponsor.Tagline).HasMaxLength(256);
        builder.Property(sponsor => sponsor.TaglineArabic).HasMaxLength(256);

        // SIMF-FDS-014 — D-260: optional shared Contact link. Restrict (a Contact
        // is soft-deleted, never hard-deleted under a referrer). HasForeignKey
        // creates the FK index.
        builder.HasOne(sponsor => sponsor.Contact)
            .WithMany()
            .HasForeignKey(sponsor => sponsor.ContactId)
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
