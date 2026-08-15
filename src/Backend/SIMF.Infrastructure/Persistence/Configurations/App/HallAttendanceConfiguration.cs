using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>HallAttendance configuration. Real FKs
/// to Session + Hall + UserProfile (Restrict — sessions, halls and attendees are
/// soft-deleted, never hard-deleted under an attendance row). The attendee key is
/// the PROFILE, which every attendee has; it was the Identity account id, and was
/// therefore unrecordable for the accountless walk-in this door exists to admit.
/// A <b>filtered unique index</b> enforces the
/// "one open row per attendee per session" rule: a door
/// scan and a geofence crossing for the same session can only ever update the
/// single open row, never insert a second.</summary>
internal sealed class HallAttendanceConfiguration : IEntityTypeConfiguration<HallAttendance>
{
    public void Configure(EntityTypeBuilder<HallAttendance> builder)
    {
        // An attendee cannot leave before they arrived. Leave stays null while the
        // row is open, which is the ordinary state and the filtered unique index
        // below depends on; once stamped it must not precede Enter. The bound is
        // >= and not >, because a scan-in immediately followed by a scan-out lands
        // both stamps in the same second and is a legitimate record, not a defect.
        builder.ToTable("HallAttendances", table => table.HasCheckConstraint(
            "CK_HallAttendances_LeaveOrder",
            "[Leave] IS NULL OR [Leave] >= [Enter]"));
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

        builder.HasOne(a => a.UserProfile)
            .WithMany()
            .HasForeignKey(a => a.UserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        // At most one OPEN attendance row per attendee per session.
        builder.HasIndex(a => new { a.SessionId, a.UserProfileId })
            .IsUnique()
            .HasFilter("[Leave] IS NULL");

        // Live per-hall presence count rides this (open rows in a hall).
        builder.HasIndex(a => new { a.HallId, a.Leave });

        // The per-attendee attendance-history lookup.
        builder.HasIndex(a => a.UserProfileId);
    }
}
