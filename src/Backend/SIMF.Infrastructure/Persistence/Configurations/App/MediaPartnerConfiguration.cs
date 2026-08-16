using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Files;
using SIMF.Domain.PublicRelations;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>MediaPartner EF config. Bilingual name,
/// optional logo path + outbound URL, display order + active flag drive the
/// public grid. Mirrors CountryConfiguration / SpeakerConfiguration shape.
/// Auto-discovered by <c>ApplyConfigurationsFromAssembly</c> (App namespace).</summary>
internal sealed class MediaPartnerConfiguration : IEntityTypeConfiguration<MediaPartner>
{
    public void Configure(EntityTypeBuilder<MediaPartner> builder)
    {
        // Coordinates are a pair or nothing, and each half has a real range —
        // the rule AdminMediaPartnerService already enforces on write, now on
        // the table too. Each branch anchors on IS NULL / IS NOT NULL because a
        // bare comparison against a null column yields UNKNOWN, which a CHECK
        // passes.
        builder.ToTable("MediaPartners", table => table.HasCheckConstraint(
            "CK_MediaPartners_Coordinates",
            "([Latitude] IS NULL AND [Longitude] IS NULL) OR "
            + "([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL "
            + "AND [Latitude] >= -90 AND [Latitude] <= 90 "
            + "AND [Longitude] >= -180 AND [Longitude] <= 180)"));
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name).HasMaxLength(256).IsRequired();
        builder.Property(m => m.NameArabic).HasMaxLength(256).IsRequired();
        // The partner logo, in the one file store. Restrict: deleting a file must never
        // delete the row that shows it.
        builder.HasIndex(m => m.LogoFileId);
        builder.HasOne(m => m.LogoFile)
            .WithMany()
            .HasForeignKey(m => m.LogoFileId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(m => m.Url).HasMaxLength(512);

        // Contact identity-card fields inlined from the removed shared Contact
        // directory. Latitude/Longitude are
        // double? and need no length. The Website slot is Url above.
        builder.Property(m => m.Email).HasMaxLength(320);
        builder.Property(m => m.PhonePrimary).HasMaxLength(32);
        builder.Property(m => m.PhoneSecondary).HasMaxLength(32);
        builder.Property(m => m.FacebookUrl).HasMaxLength(256);
        builder.Property(m => m.XUrl).HasMaxLength(256);
        builder.Property(m => m.LinkedInUrl).HasMaxLength(256);
        builder.Property(m => m.InstagramUrl).HasMaxLength(256);
        builder.Property(m => m.City).HasMaxLength(128);
        builder.Property(m => m.CityArabic).HasMaxLength(128);

        // Real same-DB FK on the inlined country reference. OnDelete=Restrict
        // matches the soft-delete policy (countries are deactivated via
        // IsActive=false, never hard-deleted under a referrer). HasForeignKey
        // creates the FK index automatically.
        builder.HasOne(m => m.Country)
            .WithMany()
            .HasForeignKey(m => m.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.IsActive, m.DisplayOrder });

        // At most one partner per English name. AdminMediaPartnerService already
        // returns a 409 MediaPartnerNameDuplicate on that clash, but its check is
        // a read followed by a write, so two concurrent creates can both pass it
        // and land a duplicate on the public grid. This is the backstop that makes
        // the rule true of the table.
        //
        // Unfiltered, unlike the Sponsor index: that rule is scoped to active rows
        // because its service deliberately lets an inactive row keep an active
        // row's name, whereas the partner clash check queries the whole table with
        // no IsActive predicate. Filtering here would admit a pair the service
        // itself refuses.
        builder.HasIndex(m => m.Name).IsUnique();
    }
}
