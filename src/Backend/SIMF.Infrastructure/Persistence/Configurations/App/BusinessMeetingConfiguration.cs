using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.BusinessMeetings;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>SIMF-FDS-013 — D-248: BusinessMeeting EF config. Real FK to
/// MeetingTable (Restrict — tables are soft-deleted). Owns its participants
/// (cascade). Indexes support the per-table overlap check and the list/queue.
/// Time-overlap is enforced in the service (no SQL range constraint).</summary>
internal sealed class BusinessMeetingConfiguration : IEntityTypeConfiguration<BusinessMeeting>
{
    public void Configure(EntityTypeBuilder<BusinessMeeting> builder)
    {
        // D-611 (Wave B) — a meeting must end after it starts.
        builder.ToTable("BusinessMeetings", table => table.HasCheckConstraint(
            "CK_BusinessMeetings_TimeWindow", "[EndUtc] > [StartUtc]"));
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Notes).HasMaxLength(1024);
        builder.Property(m => m.CancellationReason).HasMaxLength(512);
        builder.Property(m => m.StartUtc).IsRequired();
        builder.Property(m => m.EndUtc).IsRequired();

        builder.HasOne(m => m.MeetingTable)
            .WithMany()
            .HasForeignKey(m => m.MeetingTableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(m => m.Participants)
            .WithOne(p => p.BusinessMeeting!)
            .HasForeignKey(p => p.BusinessMeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Per-table overlap lookups (Confirmed rows in a table) + the CP list.
        builder.HasIndex(m => new { m.MeetingTableId, m.Status });
        builder.HasIndex(m => new { m.Status, m.StartUtc });
    }
}
