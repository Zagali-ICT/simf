using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.SeatReservations;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-175 (gap doc G11) — HallSeatLayout EF config. Real FK
/// to Hall (cascade — a deleted hall removes its layout). Unique
/// index on HallId enforces 1:1.</summary>
internal sealed class HallSeatLayoutConfiguration : IEntityTypeConfiguration<HallSeatLayout>
{
    public void Configure(EntityTypeBuilder<HallSeatLayout> builder)
    {
        builder.ToTable("HallSeatLayouts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RowLabels).HasMaxLength(256).IsRequired();

        // D-767 — optional per-row seat-count CSV (nullable → nvarchar(256) NULL),
        // mirroring the RowLabels(256) convention. Null = uniform SeatsPerRow.
        builder.Property(x => x.SeatCounts).HasMaxLength(256);

        // D-771 — optional per-row seat-tier CSV (nullable → nvarchar(256) NULL),
        // same convention as SeatCounts. Null = an all-Normal (legacy) grid.
        builder.Property(x => x.SeatTiers).HasMaxLength(256);

        builder.HasOne(x => x.Hall)
            .WithMany()
            .HasForeignKey(x => x.HallId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.HallId).IsUnique();
    }
}
