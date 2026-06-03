using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Common;
using SIMF.Domain.Contacts;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>SIMF-FDS-014 — D-254: shared Contact directory EF config. Bilingual
/// name, relative logo path, fixed social columns, map lat/long, and a real
/// same-DB FK to <c>Country</c> (Restrict — countries are soft-deleted, never
/// hard-deleted under a contact), mirroring the Speaker→Country FK. The composite
/// index matches the directory/picker order (active rows, by Arabic name).
/// Auto-discovered by <c>ApplyConfigurationsFromAssembly</c> (App namespace).</summary>
internal sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("Contacts");
        builder.HasKey(contact => contact.Id);

        builder.Property(contact => contact.NameAr).HasMaxLength(256).IsRequired();
        builder.Property(contact => contact.NameEn).HasMaxLength(256);
        builder.Property(contact => contact.LogoRelativePath).HasMaxLength(512);
        builder.Property(contact => contact.PhonePrimary).HasMaxLength(32);
        builder.Property(contact => contact.PhoneSecondary).HasMaxLength(32);
        builder.Property(contact => contact.Email).HasMaxLength(320);
        builder.Property(contact => contact.Website).HasMaxLength(512);
        builder.Property(contact => contact.FacebookUrl).HasMaxLength(256);
        builder.Property(contact => contact.XUrl).HasMaxLength(256);
        builder.Property(contact => contact.LinkedInUrl).HasMaxLength(256);
        builder.Property(contact => contact.InstagramUrl).HasMaxLength(256);

        // Real same-DB FK to Country. Restrict matches the soft-delete policy
        // (admins deactivate countries via IsActive=false; they never hard-delete
        // a row a contact points at). HasForeignKey creates the FK index.
        builder.HasOne<Country>()
            .WithMany()
            .HasForeignKey(contact => contact.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(contact => new { contact.IsActive, contact.NameAr });
    }
}
