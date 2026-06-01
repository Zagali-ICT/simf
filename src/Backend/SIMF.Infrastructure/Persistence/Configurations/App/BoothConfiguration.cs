using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Companies;
using SIMF.Domain.Exhibition;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-199 — Booth (Exhibition) entity configuration. Bilingual,
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
        builder.Property(booth => booth.NameEn).HasMaxLength(128).IsRequired();
        builder.Property(booth => booth.NameAr).HasMaxLength(128).IsRequired();

        builder.Property(booth => booth.ExhibitorNameEn).HasMaxLength(256);
        builder.Property(booth => booth.ExhibitorNameAr).HasMaxLength(256);
        // B1 — D-222: booth-officer contact.
        builder.Property(booth => booth.OfficerName).HasMaxLength(256);
        builder.Property(booth => booth.OfficerPhone).HasMaxLength(32);
        builder.Property(booth => booth.OfficerEmail).HasMaxLength(320);
        builder.Property(booth => booth.SectorEn).HasMaxLength(128);
        builder.Property(booth => booth.SectorAr).HasMaxLength(128);
        builder.Property(booth => booth.DescriptionEn).HasMaxLength(2048);
        builder.Property(booth => booth.DescriptionAr).HasMaxLength(2048);

        builder.HasIndex(booth => booth.Code).IsUnique();

        // D-199 — real same-DB FK to Hall. Restrict matches the soft-delete
        // policy. HasForeignKey creates the FK index automatically.
        builder.HasOne<Hall>()
            .WithMany()
            .HasForeignKey(booth => booth.HallId)
            .OnDelete(DeleteBehavior.Restrict);

        // B1 — D-222: real same-DB FK to the exhibitor Company. Restrict
        // (admins soft-delete companies via IsActive, never hard-delete a row
        // a booth points at). HasForeignKey creates the FK index automatically.
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(booth => booth.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(booth => new { booth.IsActive });
    }
}
