using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Exhibitors;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-199 #3 — Exhibitor EF config. Mirrors SponsorConfiguration /
/// DelegationConfiguration. The composite index matches the admin grid order
/// (active rows, then Arabic name). Auto-discovered by
/// SimfAppDbContext.OnModelCreating via ApplyConfigurationsFromAssembly.</summary>
internal sealed class ExhibitorConfiguration : IEntityTypeConfiguration<Exhibitor>
{
    public void Configure(EntityTypeBuilder<Exhibitor> builder)
    {
        builder.ToTable("Exhibitors");
        builder.HasKey(exhibitor => exhibitor.Id);

        builder.Property(exhibitor => exhibitor.Name).HasMaxLength(256).IsRequired();
        builder.Property(exhibitor => exhibitor.NameArabic).HasMaxLength(256).IsRequired();

        builder.Property(exhibitor => exhibitor.ContactEmail).HasMaxLength(320);
        builder.Property(exhibitor => exhibitor.ContactPhone).HasMaxLength(32);
        builder.Property(exhibitor => exhibitor.Website).HasMaxLength(512);

        builder.Property(exhibitor => exhibitor.PhoneSecondary).HasMaxLength(32);
        builder.Property(exhibitor => exhibitor.FacebookUrl).HasMaxLength(256);
        builder.Property(exhibitor => exhibitor.XUrl).HasMaxLength(256);
        builder.Property(exhibitor => exhibitor.LinkedInUrl).HasMaxLength(256);
        builder.Property(exhibitor => exhibitor.InstagramUrl).HasMaxLength(256);
        builder.Property(exhibitor => exhibitor.City).HasMaxLength(128);
        builder.Property(exhibitor => exhibitor.CityArabic).HasMaxLength(128);

        // Wave 3 (Figma 1439:11881) — optional exhibitor tier, stored by its int
        // value (additive-only enum discipline). Nullable → no tier pill when unset.
        builder.Property(exhibitor => exhibitor.Tier).HasConversion<int>();

        // Optional same-DB country. Restrict (a Country is a lookup, never
        // hard-deleted under a referrer). HasForeignKey creates the FK index.
        builder.HasOne(exhibitor => exhibitor.Country)
            .WithMany()
            .HasForeignKey(exhibitor => exhibitor.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(exhibitor => new
        {
            exhibitor.IsActive,
            exhibitor.NameArabic,
        });
    }
}
