using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Delegations;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-174 (gap doc G11) — Delegation EF config. CountryId is
/// a real FK with Restrict (admins deactivate countries; they never
/// hard-delete a country a delegation points at).</summary>
internal sealed class DelegationConfiguration : IEntityTypeConfiguration<Delegation>
{
    public void Configure(EntityTypeBuilder<Delegation> builder)
    {
        builder.ToTable("Delegations");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name).HasMaxLength(256).IsRequired();
        builder.Property(d => d.NameArabic).HasMaxLength(256).IsRequired();

        builder.HasOne(d => d.Country)
            .WithMany()
            .HasForeignKey(d => d.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.IsActive, d.IsPriority, d.DisplayOrder });
        builder.HasIndex(d => d.CountryId);
    }
}
