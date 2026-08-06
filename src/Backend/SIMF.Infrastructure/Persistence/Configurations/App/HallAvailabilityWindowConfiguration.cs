using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.BusinessMeetings;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>HallAvailabilityWindow EF config.
/// Real FK to Hall with cascade (a deleted hall removes its windows). Indexed by
/// (HallId, IsActive, Start) for the slot-derivation read. Mirrors
/// <see cref="SpeakerAvailabilityWindowConfiguration"/>.</summary>
internal sealed class HallAvailabilityWindowConfiguration
    : IEntityTypeConfiguration<HallAvailabilityWindow>
{
    public void Configure(EntityTypeBuilder<HallAvailabilityWindow> builder)
    {
        // A window must end after it starts.
        builder.ToTable("HallAvailabilityWindows", table => table.HasCheckConstraint(
            "CK_HallAvailabilityWindows_TimeWindow", "[End] > [Start]"));
        builder.HasKey(w => w.Id);

        builder.HasOne(w => w.Hall)
            .WithMany()
            .HasForeignKey(w => w.HallId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => new { w.HallId, w.IsActive, w.Start });

        // One ACTIVE window per (hall, start): backstop for the
        // "no duplicate window" invariant.
        builder.HasIndex(w => new { w.HallId, w.Start })
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
