using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.SeatReservations;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-175 (gap doc G11) — SeatReservation EF config.
/// Real FK to Session with cascade so a deleted session drops its
/// reservations; ReservedForUserId / CreatedByUserId are logical
/// FKs to SimfUser on the Identity DB.
/// <para>Filtered unique indexes enforce business invariants:</para>
/// <list type="number">
/// <item>One active reservation per (Session, RowLabel, SeatNumber)
/// — released rows are excluded so the seat is freed for re-use.</item>
/// <item>One active reservation per (Session, ReservedForUserId)
/// — a visitor cannot hold two seats in the same session.</item>
/// </list></summary>
internal sealed class SeatReservationConfiguration : IEntityTypeConfiguration<SeatReservation>
{
    public void Configure(EntityTypeBuilder<SeatReservation> builder)
    {
        builder.ToTable("SeatReservations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RowLabel).HasMaxLength(8).IsRequired();

        builder.HasOne(x => x.Session)
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Active-seat uniqueness — a seat can be re-reserved after release.
        builder.HasIndex(x => new { x.SessionId, x.RowLabel, x.SeatNumber })
            .HasFilter("[ReleasedAt] IS NULL")
            .IsUnique();

        // One active seat per visitor per session.
        builder.HasIndex(x => new { x.SessionId, x.ReservedForUserId })
            .HasFilter("[ReleasedAt] IS NULL AND [ReservedForUserId] IS NOT NULL")
            .IsUnique();

        // Lookup support for grid + per-user "my seat" queries.
        builder.HasIndex(x => new { x.ReservedForUserId, x.ReleasedAt });
        builder.HasIndex(x => new { x.SessionId, x.ReleasedAt });
    }
}
