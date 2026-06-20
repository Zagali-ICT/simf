using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.BusinessMeetings;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-474 (#11, Group G) — SpeakerAvailabilityWindow EF config. Real FK to
/// Speaker with cascade (a deleted speaker removes its windows). Indexed by
/// (SpeakerId, IsActive, StartUtc) for the slot-derivation read.</summary>
internal sealed class SpeakerAvailabilityWindowConfiguration
    : IEntityTypeConfiguration<SpeakerAvailabilityWindow>
{
    public void Configure(EntityTypeBuilder<SpeakerAvailabilityWindow> builder)
    {
        builder.ToTable("SpeakerAvailabilityWindows");
        builder.HasKey(w => w.Id);

        builder.HasOne(w => w.Speaker)
            .WithMany()
            .HasForeignKey(w => w.SpeakerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => new { w.SpeakerId, w.IsActive, w.StartUtc });
    }
}
