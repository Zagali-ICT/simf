using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>HallAttendance configuration. Real FKs
/// to Session + Hall (Restrict — sessions/halls are soft-deleted, never hard-
/// deleted under an attendance row). A <b>filtered unique index</b> enforces the
/// "one open row per attendee per session" rule: a door
/// scan and a geofence crossing for the same session can only ever update the
/// single open row, never insert a second.</summary>
internal sealed class HallAttendanceConfiguration : IEntityTypeConfiguration<HallAttendance>
{
    public void Configure(EntityTypeBuilder<HallAttendance> builder)
    {
        builder.ToTable("HallAttendances");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Method).IsRequired();
        builder.Property(a => a.Enter).IsRequired();

        builder.HasOne(a => a.Session)
            .WithMany()
            .HasForeignKey(a => a.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Hall)
            .WithMany()
            .HasForeignKey(a => a.HallId)
            .OnDelete(DeleteBehavior.Restrict);

        // At most one OPEN attendance row per attendee per session.
        builder.HasIndex(a => new { a.SessionId, a.UserId })
            .IsUnique()
            .HasFilter("[Leave] IS NULL");

        // Live per-hall presence count rides this (open rows in a hall).
        builder.HasIndex(a => new { a.HallId, a.Leave });

        // The per-attendee attendance-history lookup by user.
        builder.HasIndex(a => a.UserId);
    }
}
