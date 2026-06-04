using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Contacts;
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

        // SIMF-FDS-014 — D-260: optional shared Contact link. Restrict (a Contact
        // is soft-deleted, never hard-deleted under a referrer). HasForeignKey
        // creates the FK index.
        builder.HasOne(exhibitor => exhibitor.Contact)
            .WithMany()
            .HasForeignKey(exhibitor => exhibitor.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(exhibitor => new
        {
            exhibitor.IsActive,
            exhibitor.NameArabic,
        });
    }
}
