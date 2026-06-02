using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Venue;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>P2.5 — D-230: VenueMapNode EF config. Optional real FKs to Hall +
/// Booth (both Restrict — a hall/booth in use cannot be hard-deleted; they
/// soft-delete). Index on (IsActive) for the public map read.</summary>
internal sealed class VenueMapNodeConfiguration : IEntityTypeConfiguration<VenueMapNode>
{
    public void Configure(EntityTypeBuilder<VenueMapNode> builder)
    {
        builder.ToTable("VenueMapNodes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Label).HasMaxLength(128).IsRequired();
        builder.Property(x => x.LabelArabic).HasMaxLength(128).IsRequired();

        builder.HasOne(x => x.Hall)
            .WithMany()
            .HasForeignKey(x => x.HallId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Booth)
            .WithMany()
            .HasForeignKey(x => x.BoothId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.IsActive);
    }
}
