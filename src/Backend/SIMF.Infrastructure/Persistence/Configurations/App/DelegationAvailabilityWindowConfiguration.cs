using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Common;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>Bi-Meeting rework — DelegationAvailabilityWindow EF config, mirroring
/// <see cref="SpeakerAvailabilityWindowConfiguration"/>. Real FK to the Country
/// lookup with Restrict (a country in use cannot be removed). Indexed by
/// (CountryId, IsActive, Start) for the slot-derivation read; one ACTIVE window
/// per (country, start).</summary>
internal sealed class DelegationAvailabilityWindowConfiguration
    : IEntityTypeConfiguration<DelegationAvailabilityWindow>
{
    public void Configure(EntityTypeBuilder<DelegationAvailabilityWindow> builder)
    {
        // A window must end after it starts, and divides into slots of the
        // 5..480 minute length the service validates.
        builder.ToTable("DelegationAvailabilityWindows", table =>
        {
            table.HasCheckConstraint(
                "CK_DelegationAvailabilityWindows_TimeWindow", "[End] > [Start]");
            table.HasCheckConstraint(
                "CK_DelegationAvailabilityWindows_SlotMinutes",
                "[SlotMinutes] >= 5 AND [SlotMinutes] <= 480");
        });
        builder.HasKey(w => w.Id);

        builder.HasOne(w => w.Country)
            .WithMany()
            .HasForeignKey(w => w.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(w => new { w.CountryId, w.IsActive, w.Start });

        // One ACTIVE window per (country, start): backstop for the "no duplicate
        // window" invariant (mirrors the speaker window index).
        builder.HasIndex(w => new { w.CountryId, w.Start })
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
