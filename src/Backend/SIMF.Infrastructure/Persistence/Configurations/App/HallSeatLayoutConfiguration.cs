using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.SeatReservations;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>HallSeatLayout EF config. Real FK
/// to Hall (cascade — a deleted hall removes its layout). Unique
/// index on HallId enforces 1:1.</summary>
internal sealed class HallSeatLayoutConfiguration : IEntityTypeConfiguration<HallSeatLayout>
{
    public void Configure(EntityTypeBuilder<HallSeatLayout> builder)
    {
        // The grid width the service validates on every write (1..80 seats per
        // row); the DB backstop for it.
        builder.ToTable("HallSeatLayouts", table => table.HasCheckConstraint(
            "CK_HallSeatLayouts_SeatsPerRow",
            "[SeatsPerRow] >= 1 AND [SeatsPerRow] <= 80"));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RowLabels).HasMaxLength(256).IsRequired();

        // Optional per-row seat-count CSV (nullable → nvarchar(256) NULL),
        // mirroring the RowLabels(256) convention. Null = uniform SeatsPerRow.
        builder.Property(x => x.SeatCounts).HasMaxLength(256);

        // Optional per-row seat-tier CSV (nullable → nvarchar(256) NULL),
        // same convention as SeatCounts. Null = an all-Normal (legacy) grid.
        builder.Property(x => x.SeatTiers).HasMaxLength(256);

        builder.HasOne(x => x.Hall)
            .WithMany()
            .HasForeignKey(x => x.HallId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.HallId).IsUnique();
    }
}
