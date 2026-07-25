using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.PublicRelations;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-199 (Mockup page 31) — MediaPartner EF config. Bilingual name,
/// optional logo path + outbound URL, display order + active flag drive the
/// public grid. Mirrors CountryConfiguration / SpeakerConfiguration shape.
/// Auto-discovered by <c>ApplyConfigurationsFromAssembly</c> (App namespace).</summary>
internal sealed class MediaPartnerConfiguration : IEntityTypeConfiguration<MediaPartner>
{
    public void Configure(EntityTypeBuilder<MediaPartner> builder)
    {
        builder.ToTable("MediaPartners");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name).HasMaxLength(256).IsRequired();
        builder.Property(m => m.NameArabic).HasMaxLength(256).IsRequired();
        builder.Property(m => m.LogoRelativePath).HasMaxLength(512);
        builder.Property(m => m.Url).HasMaxLength(512);

        // Contact identity-card fields inlined from the removed shared Contact
        // directory (supersedes SIMF-FDS-014 / D-260). Latitude/Longitude are
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
    }
}
