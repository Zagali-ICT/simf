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

        // D-485 — RowLabel/SeatNumber are now optional: an OpenSeating join
        // carries null for both (general admission, no specific seat).
        builder.Property(x => x.RowLabel).HasMaxLength(8);

        // P2.2 — D-227: booking-approval state. NO model-level default: with
        // Pending = 0 = the CLR default, HasDefaultValue would make EF treat
        // every Pending insert as "unset" and apply the store default. The
        // service sets Status explicitly on every create; existing prod rows
        // are backfilled to Approved by the migration's one-time AddColumn
        // default (not a persisted model concern).
        builder.Property(x => x.RejectionReason).HasMaxLength(512);

        builder.HasOne(x => x.Session)
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Active-seat uniqueness — a seat can be re-reserved after release.
        // D-485: only seat-specific rows participate (RowLabel IS NOT NULL), so
        // multiple OpenSeating joins (null row/seat) don't collide on the NULLs.
        builder.HasIndex(x => new { x.SessionId, x.RowLabel, x.SeatNumber })
            .HasFilter("[ReleasedAt] IS NULL AND [RowLabel] IS NOT NULL")
            .IsUnique();

        // One active seat per visitor per session.
        builder.HasIndex(x => new { x.SessionId, x.ReservedForUserId })
            .HasFilter("[ReleasedAt] IS NULL AND [ReservedForUserId] IS NOT NULL")
            .IsUnique();

        // Lookup support for grid + per-user "my seat" queries.
        builder.HasIndex(x => new { x.ReservedForUserId, x.ReleasedAt });
        builder.HasIndex(x => new { x.SessionId, x.ReleasedAt });

        // P2.2 — D-227: the booking approval queue lists Pending, held bookings.
        builder.HasIndex(x => new { x.Status, x.ReleasedAt });
    }
}
