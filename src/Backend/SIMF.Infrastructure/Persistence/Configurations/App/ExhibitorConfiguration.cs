using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.Files;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>Exhibitor EF config. Mirrors SponsorConfiguration /
/// DelegationConfiguration. The composite index matches the admin grid order
/// (active rows, then Arabic name). Auto-discovered by
/// SimfAppDbContext.OnModelCreating via ApplyConfigurationsFromAssembly.</summary>
internal sealed class ExhibitorConfiguration : IEntityTypeConfiguration<Exhibitor>
{
    public void Configure(EntityTypeBuilder<Exhibitor> builder)
    {
        // Coordinates are a pair or nothing, and each half has a real range —
        // the rule AdminExhibitorService already enforces on write, now on the
        // table too. Each branch anchors on IS NULL / IS NOT NULL because a bare
        // comparison against a null column yields UNKNOWN, which a CHECK passes.
        builder.ToTable("Exhibitors", table => table.HasCheckConstraint(
            "CK_Exhibitors_Coordinates",
            "([Latitude] IS NULL AND [Longitude] IS NULL) OR "
            + "([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL "
            + "AND [Latitude] >= -90 AND [Latitude] <= 90 "
            + "AND [Longitude] >= -180 AND [Longitude] <= 180)"));
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
    
        // The exhibitor's file, in the one store. Restrict: deleting a file must never
        // delete the exhibitor. OwnerPointerSync keeps this column in step with the
        // store's own OwnerEntityType/OwnerEntityId pair, which stays because the
        // serve path and the permission policy both key off it.
        builder.HasIndex(x => x.LogoFileId);
        builder.HasOne(x => x.LogoFile)
            .WithMany()
            .HasForeignKey(x => x.LogoFileId)
            .OnDelete(DeleteBehavior.Restrict);
}
}
