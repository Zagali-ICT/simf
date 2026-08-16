using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.SeatReservations;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>SeatReservation EF config.
/// Real FKs to Session and to the holder's UserProfile; the ACTOR columns
/// (CreatedByUserId / ReleasedByUserId) stay logical FKs to SimfUser on the
/// Identity DB, because an actor is a signed-in account while a holder is an
/// attendee — and an attendee need not have an account.
/// <para>Filtered unique indexes enforce business invariants:</para>
/// <list type="number">
/// <item>One active reservation per (Session, RowLabel, SeatNumber)
/// — released rows are excluded so the seat is freed for re-use.</item>
/// <item>One active reservation per (Session, ReservedForProfileId)
/// — a visitor cannot hold two seats in the same session.</item>
/// </list></summary>
internal sealed class SeatReservationConfiguration : IEntityTypeConfiguration<SeatReservation>
{
    public void Configure(EntityTypeBuilder<SeatReservation> builder)
    {
        // A seat is identified by RowLabel AND SeatNumber together, or by neither:
        // an OpenSeating join and a walk-in hall hold carry null for both (general
        // admission, no specific seat), and every seat-specific kind sets both.
        // Seat numbers are 1-based within their row.
        builder.ToTable("SeatReservations", table =>
        {
            table.HasCheckConstraint(
                "CK_SeatReservations_SeatPair",
                "([RowLabel] IS NULL AND [SeatNumber] IS NULL) "
                + "OR ([RowLabel] IS NOT NULL AND [SeatNumber] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_SeatReservations_SeatNumber", "[SeatNumber] >= 1");
            // NO pair constraint on the release stamp, deliberately. One was
            // added here while ReviewedAt / ReviewedByUserId were an admin-only
            // pair that a single writer set together. That pair no longer
            // exists: ReviewedAt folded into ReleasedAt, which FIVE paths write
            // — a self-release, a seat change, a cancelled session and the
            // no-show sweep, none of which has an actor — against one writer of
            // ReleasedByUserId. Pinning them together now would reject every
            // release a person did not perform.
        });
        builder.HasKey(x => x.Id);

        // RowLabel/SeatNumber are optional: an OpenSeating join
        // carries null for both (general admission, no specific seat).
        builder.Property(x => x.RowLabel).HasMaxLength(8);

        // Status is deliberately left unconfigured. It must NOT get a
        // model-level default: with Pending = 0 = the CLR default,
        // HasDefaultValue would make EF treat every Pending insert as "unset"
        // and apply the store default instead. The service sets Status
        // explicitly on every create path.

        // The admin-typed VVIP guest hint (bilingual, both nullable).
        builder.Property(x => x.GuestHint).HasMaxLength(256);
        builder.Property(x => x.GuestHintArabic).HasMaxLength(256);

        // Restrict (was Cascade): deleting a Session must not
        // silently wipe its seat reservations; release/cancel them explicitly.
        builder.HasOne(x => x.Session)
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional: an admin block holds no attendee, so the column stays null
        // there and the constraint simply does not apply to those rows.
        builder.HasOne(x => x.ReservedForProfile)
            .WithMany()
            .HasForeignKey(x => x.ReservedForProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        // Active-seat uniqueness — a seat can be re-reserved after release.
        // Only seat-specific rows participate (RowLabel IS NOT NULL), so
        // multiple OpenSeating joins (null row/seat) don't collide on the NULLs.
        builder.HasIndex(x => new { x.SessionId, x.RowLabel, x.SeatNumber })
            .HasFilter("[ReleasedAt] IS NULL AND [RowLabel] IS NOT NULL")
            .IsUnique();

        // One active seat per visitor per session.
        builder.HasIndex(x => new { x.SessionId, x.ReservedForProfileId })
            .HasFilter("[ReleasedAt] IS NULL AND [ReservedForProfileId] IS NOT NULL")
            .IsUnique();

        // Lookup support for grid + per-attendee "my seat" queries.
        builder.HasIndex(x => new { x.ReservedForProfileId, x.ReleasedAt });
        builder.HasIndex(x => new { x.SessionId, x.ReleasedAt });

        // The booking approval queue lists Pending, held bookings.
        builder.HasIndex(x => new { x.Status, x.ReleasedAt });

        // M-6 — the expiry worker scans still-held bookings past their hold
        // window; index NoShowReleaseAt, narrowed to held rows that carry one.
        builder.HasIndex(x => x.NoShowReleaseAt)
            .HasFilter("[ReleasedAt] IS NULL AND [NoShowReleaseAt] IS NOT NULL");
    }
}
