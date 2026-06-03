using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Companies;
using SIMF.Domain.Contacts;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-199 #3 — Company EF config. Mirrors SponsorConfiguration /
/// DelegationConfiguration. <see cref="Company.Type"/> is stored as its int
/// value (the additive-only enum discipline). The composite index matches the
/// admin grid order (active rows, by type, then Arabic name). Auto-discovered
/// by SimfAppDbContext.OnModelCreating via ApplyConfigurationsFromAssembly.</summary>
internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.HasKey(company => company.Id);

        builder.Property(company => company.Name).HasMaxLength(256).IsRequired();
        builder.Property(company => company.NameArabic).HasMaxLength(256).IsRequired();

        // Persist the enum by its int value (the wire contract). EF stores
        // enums as int by default; HasConversion<int>() makes that explicit
        // and migration-stable.
        builder.Property(company => company.Type).HasConversion<int>().IsRequired();

        builder.Property(company => company.ContactEmail).HasMaxLength(320);
        builder.Property(company => company.ContactPhone).HasMaxLength(32);
        builder.Property(company => company.Website).HasMaxLength(512);

        // SIMF-FDS-014 — D-260: optional shared Contact link. Restrict (a Contact
        // is soft-deleted, never hard-deleted under a referrer). HasForeignKey
        // creates the FK index.
        builder.HasOne(company => company.Contact)
            .WithMany()
            .HasForeignKey(company => company.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(company => new
        {
            company.IsActive,
            company.Type,
            company.NameArabic,
        });
    }
}
