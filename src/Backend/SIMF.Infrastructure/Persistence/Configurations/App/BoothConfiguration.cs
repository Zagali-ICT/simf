using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Exhibition;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>Booth (Exhibition) entity configuration. Bilingual,
/// Code is unique, soft-delete via IsActive. Mirrors HallConfiguration /
/// SpeakerConfiguration. <c>HallId</c> is a real same-DB FK to
/// <c>Hall.Id</c> with <c>OnDelete.Restrict</c> (same soft-delete policy
/// as the Speaker→Country FK: admins deactivate halls via IsActive=false,
/// they never hard-delete a row a booth points at).
///
/// Lives in the <c>...Configurations.App</c> namespace so
/// <c>SimfAppDbContext.OnModelCreating</c>'s
/// <c>ApplyConfigurationsFromAssembly(... type.Namespace ==
/// "SIMF.Infrastructure.Persistence.Configurations.App")</c> picks it up
/// automatically — no manual registration needed.</summary>
internal sealed class BoothConfiguration : IEntityTypeConfiguration<Booth>
{
    public void Configure(EntityTypeBuilder<Booth> builder)
    {
        builder.ToTable("Booths");
        builder.HasKey(booth => booth.Id);

        builder.Property(booth => booth.Code).HasMaxLength(16).IsRequired();
        builder.Property(booth => booth.Name).HasMaxLength(128).IsRequired();
        builder.Property(booth => booth.NameArabic).HasMaxLength(128).IsRequired();

        builder.Property(booth => booth.ExhibitorName).HasMaxLength(256);
        builder.Property(booth => booth.ExhibitorNameArabic).HasMaxLength(256);
        // Booth-officer contact.
        builder.Property(booth => booth.OfficerName).HasMaxLength(256);
        builder.Property(booth => booth.OfficerPhone).HasMaxLength(32);
        builder.Property(booth => booth.OfficerEmail).HasMaxLength(320);
        builder.Property(booth => booth.OfficerNameArabic).HasMaxLength(256);
        builder.Property(booth => booth.OfficerPhoneSecondary).HasMaxLength(32);
        builder.Property(booth => booth.OfficerWebsite).HasMaxLength(512);
        builder.Property(booth => booth.OfficerFacebookUrl).HasMaxLength(256);
        builder.Property(booth => booth.OfficerXUrl).HasMaxLength(256);
        builder.Property(booth => booth.OfficerLinkedInUrl).HasMaxLength(256);
        builder.Property(booth => booth.OfficerInstagramUrl).HasMaxLength(256);
        builder.Property(booth => booth.OfficerCity).HasMaxLength(128);
        builder.Property(booth => booth.OfficerCityArabic).HasMaxLength(128);
        builder.Property(booth => booth.Sector).HasMaxLength(128);
        builder.Property(booth => booth.SectorArabic).HasMaxLength(128);
        builder.Property(booth => booth.Description).HasMaxLength(2048);
        builder.Property(booth => booth.DescriptionArabic).HasMaxLength(2048);

        builder.HasIndex(booth => booth.Code).IsUnique();

        // Real same-DB FK to Hall. Restrict matches the soft-delete
        // policy. HasForeignKey creates the FK index automatically.
        builder.HasOne(booth => booth.Hall)
            .WithMany()
            .HasForeignKey(booth => booth.HallId)
            .OnDelete(DeleteBehavior.Restrict);

        // Real same-DB FK to the Exhibitor. Restrict
        // (admins soft-delete exhibitors via IsActive, never hard-delete a row
        // a booth points at). HasForeignKey creates the FK index automatically.
        builder.HasOne(booth => booth.Exhibitor)
            .WithMany()
            .HasForeignKey(booth => booth.ExhibitorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Booth-officer country: real same-DB FK to Country. Restrict
        // (countries are soft-deleted, never hard-deleted under a referrer).
        // HasForeignKey creates the FK index automatically.
        builder.HasOne(booth => booth.OfficerCountry)
            .WithMany()
            .HasForeignKey(booth => booth.OfficerCountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(booth => new { booth.IsActive });
    }
}
