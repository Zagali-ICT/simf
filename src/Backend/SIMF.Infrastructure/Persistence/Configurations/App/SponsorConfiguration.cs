using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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

        builder.Property(sponsor => sponsor.NameEn).HasMaxLength(256).IsRequired();
        builder.Property(sponsor => sponsor.NameAr).HasMaxLength(256).IsRequired();

        // Persist the enum by its int value (the wire/sort contract), matching
        // the additive-only enum discipline. EF stores enums as int by default;
        // HasConversion<int>() makes that explicit and migration-stable.
        builder.Property(sponsor => sponsor.Tier).HasConversion<int>().IsRequired();

        builder.Property(sponsor => sponsor.LogoRelativePath).HasMaxLength(256);
        builder.Property(sponsor => sponsor.Url).HasMaxLength(512);

        // Public read order: active first, by tier, then display order.
        builder.HasIndex(sponsor => new
        {
            sponsor.IsActive,
            sponsor.Tier,
            sponsor.DisplayOrder,
        });
    }
}
