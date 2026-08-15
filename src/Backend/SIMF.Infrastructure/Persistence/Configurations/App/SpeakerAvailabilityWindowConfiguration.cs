using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.BusinessMeetings;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>SpeakerAvailabilityWindow EF config. Real FK to
/// Speaker with cascade (a deleted speaker removes its windows). Indexed by
/// (SpeakerId, IsActive, Start) for the slot-derivation read.</summary>
internal sealed class SpeakerAvailabilityWindowConfiguration
    : IEntityTypeConfiguration<SpeakerAvailabilityWindow>
{
    public void Configure(EntityTypeBuilder<SpeakerAvailabilityWindow> builder)
    {
        // A window must end after it starts, and divides into slots of the
        // 5..480 minute length the service validates.
        builder.ToTable("SpeakerAvailabilityWindows", table =>
        {
            table.HasCheckConstraint(
                "CK_SpeakerAvailabilityWindows_TimeWindow", "[End] > [Start]");
            table.HasCheckConstraint(
                "CK_SpeakerAvailabilityWindows_SlotMinutes",
                "[SlotMinutes] >= 5 AND [SlotMinutes] <= 480");
        });
        builder.HasKey(w => w.Id);

        builder.HasOne(w => w.Speaker)
            .WithMany()
            .HasForeignKey(w => w.SpeakerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => new { w.SpeakerId, w.IsActive, w.Start });

        // One ACTIVE window per (speaker, start): backstop for
        // the "no duplicate window" invariant.
        builder.HasIndex(w => new { w.SpeakerId, w.Start })
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
