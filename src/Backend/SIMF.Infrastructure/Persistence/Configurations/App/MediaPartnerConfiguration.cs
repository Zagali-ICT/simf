using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Contacts;
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

        builder.Property(m => m.NameEn).HasMaxLength(256).IsRequired();
        builder.Property(m => m.NameAr).HasMaxLength(256).IsRequired();
        builder.Property(m => m.LogoRelativePath).HasMaxLength(512);
        builder.Property(m => m.Url).HasMaxLength(512);

        // SIMF-FDS-014 — D-260: optional shared Contact link. Restrict (a Contact
        // is soft-deleted, never hard-deleted under a referrer). HasForeignKey
        // creates the FK index.
        builder.HasOne<Contact>()
            .WithMany()
            .HasForeignKey(m => m.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.IsActive, m.DisplayOrder });
    }
}
